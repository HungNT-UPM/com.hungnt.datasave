// using Sirenix.OdinInspector.Editor;
// using UnityEditor;
// using UnityEngine;
//
// namespace HungNT.Datasave.Editor
// {
//     [CustomEditor(typeof(DatasaveService))]
//     public sealed class DatasaveServiceEditor : OdinEditor
//     {
//         public override void OnInspectorGUI()
//         {
//             var svc = (DatasaveService)target;
//
//             if (GUILayout.Button("Reload Data", GUILayout.Height(26f)))
//                 svc.ReloadFromDisk();
//
//             EditorGUILayout.Space(4f);
//
//             base.OnInspectorGUI();
//         }
//     }
// }
