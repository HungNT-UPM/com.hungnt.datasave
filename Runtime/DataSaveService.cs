using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using VContainer.Unity;

namespace HungNT.DataSave
{
    /// <summary>
    /// Service: cache + một file JSON / miền (<see cref="BaseSaveData"/>) dưới persistent — plain text, đọc/sửa được.
    /// <para><b>Ghi đĩa:</b> <see cref="Save(BaseSaveData)"/> chỉ đánh dấu dirty; mỗi <see cref="_flushInterval"/> giây
    /// service serialize trên main thread rồi ghi atomic (<c>.tmp</c> + replace) trên background thread — không lag game
    /// kể cả khi code gọi Save() mỗi frame. Pause/quit: ghi đồng bộ toàn bộ để chắc chắn không mất dữ liệu.</para>
    /// <para><b>Vòng đời:</b> plain C# do container tạo. <see cref="Tick"/> thay <c>Update</c>,
    /// <see cref="IAppLifecycle"/> thay <c>OnApplicationPause</c>/<c>OnApplicationQuit</c>,
    /// <see cref="Dispose"/> thay <c>OnDestroy</c>.</para>
    /// </summary>
    public class DataSaveService : IDataSaveService, ITickable, IDisposable
    {
        public const string RelativeDirectory = "DataSave";

        /// <summary>Chu kỳ gộp ghi các domain dirty xuống đĩa (giây).</summary>
        public const float DefaultFlushInterval = 5f;

        private readonly IAppLifecycle _lifecycle;
        private readonly float _flushInterval;

        private readonly Dictionary<Type, BaseSaveData> _cache = new();
        private readonly Dictionary<Type, string> _fullPathByType = new();
        private readonly HashSet<Type> _dirty = new();

        // .tmp + replace không được chạy song song trên cùng file (flush nền vs ghi sync khi pause/quit).
        private readonly object _ioLock = new object();
        private float _flushTimer;
        private volatile bool _flushInFlight;

        /// <param name="lifecycle">Nguồn sự kiện pause/quit. Ngoài container dùng <see cref="NullAppLifecycle"/>.</param>
        /// <param name="flushInterval">Chu kỳ gộp ghi (giây), tối thiểu 0.5.</param>
        public DataSaveService(IAppLifecycle lifecycle, float flushInterval = DefaultFlushInterval)
        {
            _lifecycle = lifecycle;
            _flushInterval = Mathf.Max(0.5f, flushInterval);

            _lifecycle.Paused += OnPaused;
            _lifecycle.Quitting += OnQuitting;

            this.Log($"Save folder root: {Path.Combine(Application.persistentDataPath, RelativeDirectory).Bold()}");
        }

        public void Dispose()
        {
            _lifecycle.Paused -= OnPaused;
            _lifecycle.Quitting -= OnQuitting;
            FlushDirty();
        }

        // ── Get / Load ───────────────────────────────────────────────────────

        /// <summary>Đọc cache / đĩa, không gắn <see cref="IDataSaveService"/> (Editor / công cụ tự <c>BindService</c>).</summary>
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
            this.Log($"{type.Name.Color("cyan")} → {Path.GetFileName(fullPath).Bold()}");
            return data;
        }

        public T GetData<T>() where T : BaseSaveData, new()
        {
            var data = (T)GetOrLoadDomain(typeof(T));
            data.BindService(this);
            return data;
        }

        // ── Save: dirty-flag + flush ─────────────────────────────────────────

        /// <summary>Đánh dấu dirty — ghi ở lần flush kế (interval / pause / quit). Gọi mỗi frame cũng không tốn IO.</summary>
        public void Save(BaseSaveData data)
        {
            if (data == null)
            {
                this.LogError($"{nameof(Save)}: null payload.");
                return;
            }

            data.BindService(this);

            var type = data.GetType();
            _cache[type] = data;
            _dirty.Add(type);
        }

        public void Save<T>() where T : BaseSaveData, new() => Save(GetData<T>());

        /// <summary>Ghi một domain xuống đĩa ngay (đồng bộ, atomic).</summary>
        public void SaveImmediate(BaseSaveData data)
        {
            if (data == null)
            {
                this.LogError($"{nameof(SaveImmediate)}: null payload.");
                return;
            }

            data.BindService(this);
            WriteDomain(data);
            _dirty.Remove(data.GetType());
        }

        /// <summary>Ghi payload xuống đĩa ngay và cập nhật cache (không đổi <c>BindService</c>). API cấp thấp cho Editor/tool.</summary>
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

        /// <summary>Container gọi mỗi frame (thay cho <c>Update</c> của MonoBehaviour).</summary>
        public void Tick()
        {
            if (_dirty.Count == 0)
            {
                _flushTimer = 0f;
                return;
            }

            _flushTimer += Time.unscaledDeltaTime;
            if (_flushTimer < _flushInterval || _flushInFlight)
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
            {
                var data = GetOrLoadDomain(type);
                data.BindService(this);
            }

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
        private void OnPaused(bool pause)
        {
            if (pause)
                SaveAll();
        }

        private void OnQuitting() => SaveAll(true);

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
