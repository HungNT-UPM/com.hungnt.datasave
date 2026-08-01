using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

namespace HungNT.DataSave.Demo
{
    /// <summary>
    /// Minh hoạ đọc/ghi save domain. Cần scope gốc gọi <c>builder.InstallDataSave()</c> và
    /// <c>builder.RegisterComponentInHierarchy&lt;MultiDomainDatasaveDemo&gt;()</c>.
    /// </summary>
    public class MultiDomainDatasaveDemo : MonoBehaviour
    {
        [ShowInInspector, ReadOnly, FoldoutGroup("General")]
        private GeneralSaveData _general;

        private IDataSaveService _datasave;

        [Inject]
        public void Construct(IDataSaveService datasave)
        {
            _datasave = datasave;
        }

        private void Start()
        {
            RefreshViews();
        }

        [Button("Refresh views"), FoldoutGroup("Actions")]
        private void RefreshViews()
        {
            _general = _datasave.GetData<GeneralSaveData>();
        }
    }
}