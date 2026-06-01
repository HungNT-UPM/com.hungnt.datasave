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
        }
    }
}