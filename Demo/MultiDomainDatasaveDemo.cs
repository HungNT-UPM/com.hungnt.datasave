using HungNT;
using Sirenix.OdinInspector;
using UnityEngine;

namespace HungNT.DataSave.Demo
{
    /// <summary>
    /// Kịch bản minh hoạ điều khiển ba domain song song: chung, booster, daily login; phụ thuộc Service Locator đã đăng ký <see cref="IDataSaveService"/>.
    /// </summary>
    public class MultiDomainDatasaveDemo : MonoBehaviour
    {
        [ShowInInspector, ReadOnly, FoldoutGroup("General")]
        private GeneralSaveData _general;

        private IDataSaveService _datasave;

        private void Start()
        {
            _datasave = ServiceLocator.Instance.Get<IDataSaveService>();
            RefreshViews();
        }

        [Button("Refresh views"), FoldoutGroup("Actions")]
        private void RefreshViews()
        {
            if (_datasave == null)
                _datasave = ServiceLocator.Instance.Get<IDataSaveService>();

            _general = _datasave.GetData<GeneralSaveData>();
        }
    }
}