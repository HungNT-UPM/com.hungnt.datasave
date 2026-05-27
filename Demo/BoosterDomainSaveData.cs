// using System;
// using System.Collections.Generic;
// using Sirenix.OdinInspector;
//
// namespace HungNT.Datasave.Demo
// {
//     public enum BoosterType
//     {
//         Speed = 0,
//         Magnet = 1,
//         Shield = 2
//     }
//
//     [Serializable]
//     public class BoosterState
//     {
//         public BoosterType Type;
//
//         public bool IsUnlocked;
//
//         public int Quantity;
//
//         public static BoosterState Seed(BoosterType type) =>
//             new()
//             {
//                 Type = type,
//                 IsUnlocked = type == BoosterType.Speed,
//                 Quantity = type == BoosterType.Speed ? 3 : 0
//             };
//     }
//
//     [Serializable]
//     public class BoosterDomainSaveData : BaseSaveData
//     {
//         [DictionaryDrawerSettings(KeyLabel = "Booster", ValueLabel = "Data")]
//         public Dictionary<BoosterType, BoosterState> BoosterStates = new();
//
//         public BoosterDomainSaveData()
//         {
//             foreach (BoosterType type in Enum.GetValues(typeof(BoosterType)))
//                 BoosterStates[type] = BoosterState.Seed(type);
//         }
//
//         public override void OnAfterLoad()
//         {
//             BoosterStates ??= new Dictionary<BoosterType, BoosterState>();
//             foreach (BoosterType type in Enum.GetValues(typeof(BoosterType)))
//             {
//                 if (!BoosterStates.ContainsKey(type))
//                     BoosterStates[type] = BoosterState.Seed(type);
//             }
//         }
//     }
// }