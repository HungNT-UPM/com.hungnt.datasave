using System;

namespace HungNT.DataSave
{
    /// <summary>
    /// Một miền dữ liệu serializable trong một file riêng (<b>Odin Serialization</b>, <c>DataFormat.JSON</c> trên đĩa khi DEBUG).
    /// Tên file = <see cref="SaveFileStem"/><c>.json</c> (stem mặc định snake_case theo <c>GetType()</c>).
    /// Sau <see cref="IDataSaveService.GetData{T}"/>, service đã gắn — gọi <see cref="Save"/>.
    /// </summary>
    [Serializable]
    public abstract class BaseSaveData
    {
        protected virtual string SaveFileStem => null;

        public virtual string SaveFileName => $"{ResolveStem()}.json";

        public virtual void OnAfterLoad()
        {
        }

        [NonSerialized]
        private IDataSaveService _service;

        /// <summary>Gắn service để <see cref="Save"/> hoạt động. Gọi từ <see cref="IDataSaveService"/> / Editor, không cần gọi từ game trực tiếp.</summary>
        public void BindService(IDataSaveService service) => _service = service;

        public void Save()
        {
            if (_service == null)
            {
                this.LogError($"Chưa gắn {nameof(IDataSaveService)} — gọi {nameof(IDataSaveService.GetData)} hoặc {nameof(DataSaveService)}.{nameof(IDataSaveService.Save)}(…) trước.");
                return;
            }

            _service.Save(this);
        }

        private static void LogStemInvalidChars()
        {
            DebugEx.LogError($"[{nameof(BaseSaveData)}] {nameof(SaveFileStem)} không được chứa '/' hay '.'.");
        }

        private string ResolveStem()
        {
            var pinned = NormalizePinnedStem(SaveFileStem);
            return string.IsNullOrEmpty(pinned) ? SaveDataNaming.ToSnakeStem(GetType()) : pinned;
        }

        private static string NormalizePinnedStem(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return null;

            var t = raw.Trim().Replace('\\', '/');
            foreach (var c in t)
            {
                if (c is '/' or '.')
                {
                    LogStemInvalidChars();
                    return null;
                }
            }

            return t;
        }
    }
}