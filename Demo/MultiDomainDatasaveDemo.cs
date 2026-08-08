using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

namespace HungNT.DataSave.Demo
{
    /// <summary>
    /// Minh hoạ đọc và ghi một save domain.
    /// Cần đăng ký <see cref="IDataSaveService"/> và component này ở LifetimeScope của scene.
    /// </summary>
    public class MultiDomainDatasaveDemo : MonoBehaviour
    {
        [ShowInInspector, ReadOnly, FoldoutGroup("General")]
        private GeneralSaveData _general;

        private IDataSaveService _dataSave;

        [Inject]
        public void Construct(IDataSaveService dataSave)
        {
            _dataSave = dataSave;
        }

        private void Start()
        {
            RefreshViews();
        }

        [Button("Refresh views"), FoldoutGroup("Actions")]
        private void RefreshViews()
        {
            _general = _dataSave.GetData<GeneralSaveData>();
        }
    }
}