using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using VContainer.Unity;

namespace HungNT.DataSave
{
    /// <summary>
    /// Cache + một file JSON cho mỗi miền <see cref="BaseSaveData"/> dưới thư mục persistent.
    /// File là plain text nên đọc/sửa được khi debug.
    /// <para><see cref="Save"/> chỉ đánh dấu dirty nên gọi mỗi frame cũng không tốn IO: service gộp ghi
    /// mỗi 5 giây, serialize trên main thread rồi ghi atomic (<c>.tmp</c> + replace) trên background
    /// thread. Khi pause hoặc quit thì ghi đồng bộ toàn bộ.</para>
    /// </summary>
    public class DataSaveService : IDataSaveService, ITickable, IDisposable
    {
        public const string RelativeDirectory = "DataSave";

        /// <summary>Chu kỳ gộp ghi các domain dirty xuống đĩa (giây).</summary>
        private const float FlushIntervalSeconds = 5f;

        private readonly IAppLifecycleService _appLifecycle;

        private readonly Dictionary<Type, BaseSaveData> _cache = new();
        private readonly Dictionary<Type, string> _fullPathByType = new();
        private readonly HashSet<Type> _dirty = new();

        // .tmp + replace không được chạy song song trên cùng file (flush nền vs ghi sync khi pause/quit).
        private readonly object _ioLock = new object();
        private float _flushTimer;
        private volatile bool _flushInFlight;

        /// <param name="appLifecycle">Nguồn sự kiện pause/quit. Ngoài container dùng <see cref="NullAppLifecycleService"/>.</param>
        public DataSaveService(IAppLifecycleService appLifecycle)
        {
            _appLifecycle = appLifecycle;
            _appLifecycle.OnPaused += HandlePaused;
            _appLifecycle.OnQuitting += HandleQuitting;

            this.Log($"Save folder root: {Path.Combine(Application.persistentDataPath, RelativeDirectory).Bold()}");
        }

        public void Dispose()
        {
            _appLifecycle.OnPaused -= HandlePaused;
            _appLifecycle.OnQuitting -= HandleQuitting;
            FlushDirty();
        }

        // ── Get / Load ───────────────────────────────────────────────────────

        /// <summary>Đọc từ cache, không có thì load từ đĩa (hoặc tạo mới nếu file chưa tồn tại).</summary>
        public BaseSaveData GetOrLoadDomain(Type type)
        {
            if (type == null || type.IsAbstract || !typeof(BaseSaveData).IsAssignableFrom(type))
                throw new ArgumentException($"Invalid type: {type}", nameof(type));
            
            if (type.GetConstructor(Type.EmptyTypes) == null)
                throw new ArgumentException($"Type {type.Name} needs a parameterless constructor.", nameof(type));

            if (_cache.TryGetValue(type, out var cached))
                return cached;

            var stub = (BaseSaveData)Activator.CreateInstance(type);
            var fullPath = GetOrCreateFullPath(type, stub);
            var data = ReadFromDiskOrCreate(type, fullPath);
            data.OnAfterLoad();

            _cache[type] = data;
            return data;
        }

        public T GetData<T>() where T : BaseSaveData, new() => (T)GetOrLoadDomain(typeof(T));

        // ── Save: dirty-flag + flush ─────────────────────────────────────────

        /// <summary>Đánh dấu dirty — ghi ở lần flush kế tiếp (chu kỳ / pause / quit).</summary>
        public void Save(BaseSaveData data)
        {
            if (data == null)
            {
                this.LogError($"{nameof(Save)}: null payload.");
                return;
            }

            var type = data.GetType();
            _cache[type] = data;
            _dirty.Add(type);
        }

        public void Save<T>() where T : BaseSaveData, new() => Save(GetData<T>());

        /// <summary>Ghi một domain xuống đĩa ngay (đồng bộ, atomic). Dùng cho dữ liệu quan trọng như sau IAP.</summary>
        public void SaveImmediate(BaseSaveData data)
        {
            if (data == null)
            {
                this.LogError($"{nameof(SaveImmediate)}: null payload.");
                return;
            }

            WriteDomain(data);
            _dirty.Remove(data.GetType());
        }

        /// <summary>Ghi payload xuống đĩa ngay và cập nhật cache. API cấp thấp cho Editor/tool.</summary>
        public void WriteDomain(BaseSaveData data)
        {
            if (data == null)
            {
                this.LogError($"{nameof(WriteDomain)}: null payload.");
                return;
            }

            var concreteType = data.GetType();
            var fullPath = GetOrCreateFullPath(concreteType, data);
            WriteToDisk(data, fullPath);
            _cache[concreteType] = data;
        }

        /// <summary>Ghi ngay mọi domain đang dirty (đồng bộ).</summary>
        public void FlushDirty()
        {
            if (_dirty.Count == 0)
                return;

            foreach (var type in _dirty)
            {
                if (_cache.TryGetValue(type, out var data))
                    WriteToDisk(data, GetOrCreateFullPath(type, data));
            }

            _dirty.Clear();
        }

        public void SaveAll(bool hasLog = false)
        {
            foreach (var kvp in _cache)
            {
                var fullPath = GetOrCreateFullPath(kvp.Key, kvp.Value);
                WriteToDisk(kvp.Value, fullPath);
            }

            _dirty.Clear();

            if (hasLog)
                this.Log($"SaveAll: {_cache.Count} file(s).");
        }

        // ── Flush nền theo chu kỳ ────────────────────────────────────────────

        /// <summary>Container gọi mỗi frame để đếm chu kỳ flush.</summary>
        public void Tick()
        {
            if (_dirty.Count == 0)
            {
                _flushTimer = 0f;
                return;
            }

            _flushTimer += Time.unscaledDeltaTime;
            if (_flushTimer < FlushIntervalSeconds || _flushInFlight)
                return;

            _flushTimer = 0f;
            FlushDirtyInBackground();
        }

        /// <summary>Serialize trên main thread (data không thread-safe), IO trên background thread.</summary>
        private void FlushDirtyInBackground()
        {
            var jobs = new List<(string path, string json)>(_dirty.Count);
            foreach (var type in _dirty)
            {
                if (_cache.TryGetValue(type, out var data))
                    jobs.Add((GetOrCreateFullPath(type, data), DataSaveJsonIO.Serialize(data)));
            }

            _dirty.Clear();

            if (jobs.Count == 0)
                return;

            _flushInFlight = true;
            Task.Run(() =>
            {
                try
                {
                    foreach (var (path, json) in jobs)
                    {
                        lock (_ioLock)
                            DataSaveFileIO.WriteAtomic(path, json);
                    }
                }
                catch (Exception ex)
                {
                    DebugEx.LogError($"[{nameof(DataSaveService)}] Background flush failed: {ex}");
                }
                finally
                {
                    _flushInFlight = false;
                }
            });
        }

        // ── Cache / Reload / Delete ──────────────────────────────────────────

        public void EvictCachedDomains()
        {
            var types = new List<Type>(_cache.Keys);
            foreach (var type in types)
                EvictDomain(type);
        }

        public void ReloadFromDisk()
        {
            var types = new List<Type>(_cache.Keys);
            EvictCachedDomains();
            foreach (var type in types)
                GetOrLoadDomain(type);

            this.Log($"{nameof(ReloadFromDisk)}: {types.Count} domain(s).");
        }

        public void Delete<T>() where T : BaseSaveData, new()
        {
            var type = typeof(T);

            var sample = _cache.TryGetValue(type, out var cached) ? cached : new T();
            var fullPath = GetOrCreateFullPath(type, sample);

            lock (_ioLock)
                DataSaveFileIO.DeleteFileIfExists(fullPath);

            _fullPathByType.Remove(type);
            _cache.Remove(type);
            _dirty.Remove(type);
            this.Log($"Deleted {type.Name}");
        }

        public void DeleteAll()
        {
            var seen = new HashSet<string>();
            foreach (var full in _fullPathByType.Values)
            {
                if (!seen.Add(full))
                    continue;

                lock (_ioLock)
                    DataSaveFileIO.DeleteFileIfExists(full);
            }

            _cache.Clear();
            _fullPathByType.Clear();
            _dirty.Clear();

            this.Log("DeleteAll: cleared tracked files.");
        }

        // ── Private ──────────────────────────────────────────────────────────

        // Pause: ghi đồng bộ TOÀN BỘ (không chỉ dirty) — an toàn trước case code mutate data mà quên Save().
        // Trên mobile đây là hook tin cậy hơn quit rất nhiều.
        private void HandlePaused(bool pause)
        {
            if (pause)
                SaveAll();
        }

        private void HandleQuitting() => SaveAll(true);

        private void EvictDomain(Type type)
        {
            if (type == null)
                return;

            _cache.Remove(type);
            _fullPathByType.Remove(type);
            _dirty.Remove(type);
        }

        private void WriteToDisk(BaseSaveData data, string fullPath)
        {
            var json = DataSaveJsonIO.Serialize(data);
            lock (_ioLock)
                DataSaveFileIO.WriteAtomic(fullPath, json);

            this.Log($"{data.GetType().Name.Color("cyan")} → {Path.GetFileName(fullPath).Bold()}");
        }

        private BaseSaveData ReadFromDiskOrCreate(Type type, string fullPath)
        {
            try
            {
                var text = DataSaveFileIO.ReadAllText(fullPath);
                if (string.IsNullOrEmpty(text))
                    return (BaseSaveData)Activator.CreateInstance(type);

                var loaded = DataSaveJsonIO.Deserialize(type, text);
                if (loaded == null)
                    return (BaseSaveData)Activator.CreateInstance(type);

                return loaded;
            }
            catch (Exception ex)
            {
                this.LogError($"{nameof(ReadFromDiskOrCreate)} `{fullPath}`: {ex.Message}");
                return (BaseSaveData)Activator.CreateInstance(type);
            }
        }

        private string GetOrCreateFullPath(Type type, BaseSaveData sample)
        {
            if (_fullPathByType.TryGetValue(type, out var existing))
                return existing;

            var relativePath = ComposeRelativeSavePath(sample);
            var full = Path.Combine(Application.persistentDataPath, relativePath);

            foreach (var kvp in _fullPathByType)
            {
                if (kvp.Value == full)
                {
                    this.LogWarning($"{type.Name} và {kvp.Key.Name} trùng file `{full}`.");
                    break;
                }
            }

            _fullPathByType[type] = full;
            return full;
        }

        private static string ComposeRelativeSavePath(BaseSaveData sample)
        {
            var file = sample.SaveFileName?.Trim();
            if (string.IsNullOrWhiteSpace(file))
            {
                DebugEx.LogError($"[{nameof(DataSaveService)}] {sample.GetType().Name}.{nameof(BaseSaveData.SaveFileName)} rỗng — fallback.");
                file = $"{SaveDataNaming.ToSnakeStem(sample.GetType())}_fallback.json";
            }

            var root = RelativeDirectory.Trim().Replace('\\', '/').Trim('/');
            var normalized = file.Trim().Replace('\\', '/').TrimStart('/');
            return string.IsNullOrEmpty(root) ? normalized : $"{root}/{normalized}";
        }
    }
}
