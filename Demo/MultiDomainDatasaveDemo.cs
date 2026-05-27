using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace HungNT.Datasave.Demo
{
    /// <summary>
    /// Kịch bản minh hoạ điều khiển ba domain song song: chung, booster, daily login; phụ thuộc Service Locator đã đăng ký <see cref="IDatasaveService"/>.
    /// </summary>
    public class MultiDomainDatasaveDemo : MonoBehaviour
    {
        [ShowInInspector, ReadOnly, FoldoutGroup("General")]
        private GeneralSaveData _general;

        // [ShowInInspector, ReadOnly, FoldoutGroup("Boosters")]
        // private BoosterDomainSaveData _booster;

        private IDatasaveService _datasave;

        private void Start()
        {
            _datasave = this.GetService<IDatasaveService>();
            RefreshViews();
        }

        [Button("Refresh views"), FoldoutGroup("Actions")]
        private void RefreshViews()
        {
            if (_datasave == null)
                _datasave = this.GetService<IDatasaveService>();

            _general = _datasave.GetData<GeneralSaveData>();
            // _booster = _datasave.GetData<BoosterDomainSaveData>();
        }

        // [Button("Grant +1 random booster"), FoldoutGroup("Actions")]
        // private void GrantRandomBooster()
        // {
        //     var data = _datasave.GetData<BoosterDomainSaveData>();
        //     var types = (BoosterType[])Enum.GetValues(typeof(BoosterType));
        //     var pick = types[UnityEngine.Random.Range(0, types.Length)];
        //     var row = data.BoosterStates[pick];
        //     row.IsUnlocked = true;
        //     row.Quantity += 1;
        //     data.BoosterStates[pick] = row;
        //     data.Save();
        //     _booster = data;
        // }
    }
}