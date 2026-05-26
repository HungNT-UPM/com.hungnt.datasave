using System;
using System.Collections.Generic;
using System.IO;
using HungNT;
using Sirenix.OdinInspector;
using UnityEngine;

namespace HungNT.Datasave
{
    /// <summary>
    /// Service: cache + một file / miền (<see cref="BaseSaveData"/>) dưới persistent.
    /// Serialize: <b>Odin</b> <see cref="Sirenix.Serialization.DataFormat.JSON"/>. Đĩa: <c>DEBUG</c> = text; release = <see cref="DatasaveDiskCodec.EncryptedExtension"/>.
    /// </summary>
    public class DatasaveService : MonoBehaviour, IDatasaveService
    {
        public const string RelativeDirectory = "Datasave";

        private Dictionary<Type, BaseSaveData> _cache;
        private Dictionary<Type, string> _fullPathByType;
        private Dictionary<string, Type> _pathOwner;

        [ShowInInspector, ReadOnly]
        private IReadOnlyDictionary<Type, BaseSaveData> CachedDomains
        {
            get
            {
                EnsureCaches();
                return _cache;
            }
        }

        private void Awake() => EnsureCaches();

        public void Initialize()
        {
            EnsureCaches();
            var root = Path.Combine(Application.persistentDataPath, RelativeDirectory);
            this.Log($"Save folder root: {root.Bold()}");
        }

        public void LateInitialize()
        {
        }

        /// <summary>Đọc cache / đĩa, không gắn <see cref="IDatasaveService"/> (Editor / công cụ tự <c>BindService</c>).</summary>
        public BaseSaveData GetOrLoadDomain(Type type)
        {
            EnsureCaches();

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

        /// <summary>Ghi payload xuống đĩa và cập nhật cache (không đổi <c>BindService</c>).</summary>
        public void WriteDomain(BaseSaveData data)
        {
            if (data == null)
            {
                this.LogError($"{nameof(WriteDomain)}: null payload.");
                return;
            }

            EnsureCaches();

            var concreteType = data.GetType();
            var fullPath = GetOrCreateFullPath(concreteType, data);
            WriteToDisk(data, fullPath);
            _cache[concreteType] = data;
        }

        public void Save(BaseSaveData data)
        {
            if (data == null)
            {
                this.LogError($"{nameof(Save)}: null payload.");
                return;
            }

            EnsureCaches();
            data.BindService(this);
            WriteDomain(data);
        }

        public void Save<T>() where T : BaseSaveData, new() => Save(GetData<T>());

        public void SaveAll(bool hasLog = false)
        {
            EnsureCaches();
            foreach (var kvp in _cache)
            {
                var fullPath = GetOrCreateFullPath(kvp.Key, kvp.Value);
                WriteToDisk(kvp.Value, fullPath);
            }

            if (hasLog)
                this.Log($"SaveAll: {_cache.Count} file(s).");
        }

        public void EvictCachedDomains()
        {
            EnsureCaches();
            var types = new List<Type>(_cache.Keys);
            foreach (var type in types)
                EvictDomain(type);
        }

        [Button(ButtonSizes.Medium)]
        public void ReloadFromDisk()
        {
            EnsureCaches();
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
            EnsureCaches();
            var type = typeof(T);

            var sample = _cache.TryGetValue(type, out var cached) ? cached : new T();
            var fullPath = GetOrCreateFullPath(type, sample);

            DatasaveDiskCodec.DeleteFileIfExists(fullPath);

            UntrackPaths(type);
            _cache.Remove(type);
            this.Log($"Deleted {type.Name}");
        }

        public void DeleteAll()
        {
            EnsureCaches();
            var seen = new HashSet<string>();
            foreach (var full in _fullPathByType.Values)
            {
                if (!seen.Add(full))
                    continue;
                DatasaveDiskCodec.DeleteFileIfExists(full);
            }

            _cache.Clear();
            _fullPathByType.Clear();
            _pathOwner.Clear();

            this.Log("DeleteAll: cleared tracked files.");
        }

        private void EnsureCaches()
        {
            _cache ??= new Dictionary<Type, BaseSaveData>();
            _fullPathByType ??= new Dictionary<Type, string>();
            _pathOwner ??= new Dictionary<string, Type>();
        }

        private void EvictDomain(Type type)
        {
            if (type == null)
                return;
            _cache.Remove(type);
            UntrackPaths(type);
        }

        private void WriteToDisk(BaseSaveData data, string fullPath)
        {
            var text = DatasaveOdinIO.SerializeToOdinJsonText(data);
            DatasaveDiskCodec.WriteJsonFile(fullPath, text);
            this.Log($"{data.GetType().Name.Color("cyan")} → {Path.GetFileName(fullPath).Bold()}");
        }

        private BaseSaveData ReadFromDiskOrCreate(Type type, string fullPath)
        {
            if (!File.Exists(fullPath))
                return (BaseSaveData)Activator.CreateInstance(type);

            try
            {
                var text = DatasaveDiskCodec.ReadJsonFile(fullPath);
                if (string.IsNullOrEmpty(text))
                    return (BaseSaveData)Activator.CreateInstance(type);

                var loaded = DatasaveOdinIO.DeserializeFromOdinJsonText(type, text);
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

        private void UntrackPaths(Type type)
        {
            if (!_fullPathByType.TryGetValue(type, out var full))
                return;

            _fullPathByType.Remove(type);

            if (_pathOwner.TryGetValue(full, out var owner) && owner == type)
                _pathOwner.Remove(full);
        }

        private string GetOrCreateFullPath(Type type, BaseSaveData sample)
        {
            if (_fullPathByType.TryGetValue(type, out var existing))
                return existing;

            var relativePath = ComposeRelativeSavePath(sample);
            var full = Path.Combine(Application.persistentDataPath, relativePath);

            if (_pathOwner.TryGetValue(full, out var otherType) && otherType != type)
                this.LogWarning($"{type.Name} và {otherType.Name} trùng file `{full}`.");

            _pathOwner[full] = type;
            _fullPathByType[type] = full;
            return full;
        }

        private static string ComposeRelativeSavePath(BaseSaveData sample)
        {
            var file = sample.SaveFileName?.Trim();
            if (string.IsNullOrWhiteSpace(file))
            {
                DebugEx.LogError($"[{nameof(DatasaveService)}] {sample.GetType().Name}.{nameof(BaseSaveData.SaveFileName)} rỗng — fallback.");
                file = $"{SaveDataNaming.ToSnakeStem(sample.GetType())}_fallback.json";
            }

            file = DatasaveDiskCodec.ToPhysicalSaveFileName(file);
            var root = RelativeDirectory.Trim().Replace('\\', '/').Trim('/');
            var normalized = file.Trim().Replace('\\', '/').TrimStart('/');
            return string.IsNullOrEmpty(root) ? normalized : $"{root}/{normalized}";
        }

        private void OnApplicationPause(bool pause)
        {
            if (pause)
                SaveAll();
        }

        private void OnApplicationQuit() => SaveAll(true);
    }
}