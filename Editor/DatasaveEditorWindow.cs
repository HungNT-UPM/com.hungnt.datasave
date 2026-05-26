using System;
using System.Collections.Generic;
using System.IO;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace HungNT.Datasave.Editor
{
    /// <summary>Odin EditorWindow: chỉnh <see cref="BaseSaveData"/> trên persistent (cùng codec với runtime).</summary>
    public sealed class DatasaveEditorWindow : OdinEditorWindow
    {
        private const float LeftPanelWidth = 288f;

        [MenuItem("HungNT/Datasave Editor")]
        private static void Open()
        {
            var w = GetWindow<DatasaveEditorWindow>();
            w.titleContent = new GUIContent("Datasave Editor");
            w.minSize = new Vector2(640f, 420f);
        }

        private readonly List<Type> _domainTypes = new();

        private GameObject _editorSessionRoot;
        private DatasaveService _datasave;
        private EditorDatasaveBinder _binder;
        private PropertyTree _propertyTree;
        private int _selectedIndex = -1;
        private Vector2 _leftScroll;

        private Type SelectedType =>
            _selectedIndex >= 0 && _selectedIndex < _domainTypes.Count ? _domainTypes[_selectedIndex] : null;

        protected override void OnEnable()
        {
            base.OnEnable();

            RefreshDomainTypes();
            var createdNewDomains = false;
            if (_datasave == null || _binder == null)
            {
                EnsureEditorSession();
                createdNewDomains = true;
            }

            if (_domainTypes.Count > 0)
            {
                _selectedIndex = Mathf.Clamp(_selectedIndex, 0, _domainTypes.Count - 1);
                if (createdNewDomains)
                    ReloadAllFromDisk();
                else
                    RefreshPropertyTree();
            }
            else
            {
                _selectedIndex = -1;
                DisposePropertyTree();
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            DisposePropertyTree();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (_editorSessionRoot == null)
                return;
            DestroyImmediate(_editorSessionRoot);
            _editorSessionRoot = null;
            _datasave = null;
            _binder = null;
        }

        private void RefreshDomainTypes()
        {
            _domainTypes.Clear();
            foreach (var t in TypeCache.GetTypesDerivedFrom<BaseSaveData>())
            {
                if (t.IsAbstract)
                    continue;
                if (t.GetConstructor(Type.EmptyTypes) == null)
                    continue;
                _domainTypes.Add(t);
            }

            _domainTypes.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
        }

        [OnInspectorGUI]
        [PropertyOrder(-1000)]
        private void DrawMainLayout()
        {
            if (_domainTypes.Count == 0)
            {
                EditorGUILayout.HelpBox("Không tìm thấy kiểu nào kế thừa BaseSaveData (constructor không tham số).", MessageType.Warning);
                return;
            }

            EditorGUILayout.BeginHorizontal();

            _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll, GUILayout.Width(LeftPanelWidth), GUILayout.ExpandHeight(true));
            DrawDomainList();
            EditorGUILayout.EndScrollView();

            DrawVerticalSeparator();

            DrawPayloadPanel();

            EditorGUILayout.EndHorizontal();

            DrawFooterActions();
        }

        private void DrawDomainList()
        {
            for (var i = 0; i < _domainTypes.Count; i++)
            {
                var t = _domainTypes[i];
                var fileName = TryGetSaveFileName(t);
                var isSel = i == _selectedIndex;
                var prev = GUI.backgroundColor;
                if (isSel)
                    GUI.backgroundColor = new Color(0.55f, 0.78f, 1f, 1f);

                var label = $"{t.Name}\n({fileName})";
                if (GUILayout.Button(label, GUILayout.MinHeight(44f), GUILayout.ExpandWidth(true)))
                {
                    if (_selectedIndex != i)
                    {
                        _selectedIndex = i;
                        RefreshPropertyTree();
                    }
                }

                GUI.backgroundColor = prev;
            }
        }

        private static string TryGetSaveFileName(Type t)
        {
            try
            {
                var x = (BaseSaveData)Activator.CreateInstance(t);
                return x.SaveFileName;
            }
            catch
            {
                return "?";
            }
        }

        private static void DrawVerticalSeparator()
        {
            var lineColor = EditorGUIUtility.isProSkin
                ? new Color(1f, 1f, 1f, 0.15f)
                : new Color(0f, 0f, 0f, 0.22f);
            const float pad = 4f;
            GUILayout.Space(pad);
            var rect = EditorGUILayout.GetControlRect(GUILayout.Width(1f), GUILayout.ExpandHeight(true));
            rect.width = 1f;
            EditorGUI.DrawRect(rect, lineColor);
            GUILayout.Space(pad);
        }

        private void DrawPayloadPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            if (SelectedType == null)
            {
                EditorGUILayout.HelpBox("Chọn một domain.", MessageType.Info);
            }
            else
            {
                EnsureEditorSession();
                if (_propertyTree != null)
                {
                    _propertyTree.UpdateTree();
                    _propertyTree.Draw(false);
                }
                else
                    EditorGUILayout.HelpBox("Không tạo được PropertyTree cho miền này.", MessageType.Warning);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawFooterActions()
        {
            GUILayout.Space(6f);

            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = new Color(0.35f, 0.7f, 0.45f);
            if (GUILayout.Button("Reload", GUILayout.Height(26f)))
                ReloadAllFromDisk();
            GUI.backgroundColor = Color.white;

            if (GUILayout.Button("Save all", GUILayout.Height(26f)))
            {
                EnsureEditorSession();
                _datasave.SaveAll(true);
            }

            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                if (GUILayout.Button("Reload services", GUILayout.Height(26f)))
                    ReloadPlayingDatasaveServices();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Open persistent folder", GUILayout.Height(24f)))
                EditorUtility.RevealInFinder(Application.persistentDataPath);

            GUI.backgroundColor = new Color(0.95f, 0.45f, 0.35f);
            if (GUILayout.Button("Clear persistent", GUILayout.Height(24f)))
                TryClearEntirePersistentFolder();
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();
        }

        private void ReloadPlayingDatasaveServices()
        {
            if (!Application.isPlaying)
                return;

            var services = UnityEngine.Object.FindObjectsByType<DatasaveService>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var svc in services)
                svc.ReloadFromDisk();

            this.Log("ReloadFromDisk on DatasaveService.".Color("lime"));
        }

        private void EnsureEditorSession()
        {
            if (_datasave != null && _binder != null)
                return;

            _editorSessionRoot = new GameObject("[HungNT.Datasave.EditorSession]")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            _datasave = _editorSessionRoot.AddComponent<DatasaveService>();
            _binder = new EditorDatasaveBinder(_datasave);
        }

        private void ReloadAllFromDisk()
        {
            if (_domainTypes.Count == 0)
                return;
            EnsureEditorSession();
            _datasave.EvictCachedDomains();
            foreach (var t in _domainTypes)
            {
                var d = _datasave.GetOrLoadDomain(t);
                d.BindService(_binder);
            }

            RefreshPropertyTree();
            Repaint();
            // this.Log("Reloaded all domains from disk.");
        }

        private void RefreshPropertyTree()
        {
            DisposePropertyTree();
            if (SelectedType == null || _datasave == null || _binder == null)
                return;

            var payload = _datasave.GetOrLoadDomain(SelectedType);
            payload.BindService(_binder);
            _propertyTree = PropertyTree.Create(payload, SerializationBackend.Odin);
        }

        private void TryClearEntirePersistentFolder()
        {
            // if (!EditorUtility.DisplayDialog(
            //         "Xóa dữ liệu",
            //         "Bạn có muốn xoá toàn bộ dữ liệu không?",
            //         "Có",
            //         "Không"))
            //     return;

            ClearPersistentContents();
            EnsureEditorSession();
            _datasave.EvictCachedDomains();
            ReloadAllFromDisk();
        }

        private static void ClearPersistentContents()
        {
            var root = Application.persistentDataPath;
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
                return;

            foreach (var path in Directory.GetFileSystemEntries(root))
            {
                try
                {
                    if (File.Exists(path))
                        File.Delete(path);
                    else
                        Directory.Delete(path, recursive: true);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[DatasaveEditor] Skip delete `{path}`: {ex.Message}");
                }
            }

            DebugEx.Log("Cleared persistentDataPath contents.".Color("orange"));
        }

        private void DisposePropertyTree()
        {
            if (_propertyTree == null)
                return;
            _propertyTree.Dispose();
            _propertyTree = null;
        }

        private sealed class EditorDatasaveBinder : IDatasaveService
        {
            private readonly DatasaveService _datasave;

            public EditorDatasaveBinder(DatasaveService datasave)
            {
                _datasave = datasave;
            }

            public void Initialize()
            {
            }

            public void LateInitialize()
            {
            }

            public T GetData<T>() where T : BaseSaveData, new()
            {
                var data = (T)_datasave.GetOrLoadDomain(typeof(T));
                data.BindService(this);
                return data;
            }

            public void Save(BaseSaveData data)
            {
                if (data == null)
                    return;
                data.BindService(this);
                _datasave.WriteDomain(data);
            }

            public void Save<T>() where T : BaseSaveData, new() => Save(GetData<T>());

            public void SaveAll(bool hasLog) => _datasave.SaveAll(hasLog);

            public void ReloadFromDisk()
            {
                // Binder chỉ dùng trong EditorWindow; reload qua window.
            }

            public void Delete<T>() where T : BaseSaveData, new() => _datasave.Delete<T>();

            public void DeleteAll() => _datasave.DeleteAll();
        }
    }
}