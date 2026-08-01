using System;
using Newtonsoft.Json;

namespace HungNT.DataSave
{
    /// <summary>
    /// Một miền dữ liệu serializable trong một file JSON riêng (Newtonsoft, plain text).
    /// Tên file = <see cref="SaveFileStem"/><c>.json</c> (stem mặc định snake_case theo <c>GetType()</c>).
    /// <para><see cref="Save"/> chỉ <b>đánh dấu dirty</b> — service gộp ghi theo chu kỳ flush và khi pause/quit,
    /// nên gọi Save() mỗi frame cũng không tốn IO. Cần ghi ngay: <see cref="SaveImmediate"/>.</para>
    /// </summary>
    [Serializable]
    public abstract class BaseSaveData
    {
        protected virtual string SaveFileStem => null;

        [JsonIgnore]
        public virtual string SaveFileName => $"{ResolveStem()}.json";

        public virtual void OnAfterLoad()
        {
        }

        [NonSerialized]
        private IDataSaveService _service;

        /// <summary>Gắn service để <see cref="Save"/> hoạt động. Gọi từ <see cref="IDataSaveService"/> / Editor, không cần gọi từ game trực tiếp.</summary>
        public void BindService(IDataSaveService service) => _service = service;

        /// <summary>
        /// Đánh dấu domain này cần ghi — service flush trong chu kỳ kế tiếp (hoặc khi pause/quit).
        /// Yêu cầu đã <see cref="BindService"/>: lấy data qua <c>IDataSaveService.GetData&lt;T&gt;()</c> là tự bind.
        /// </summary>
        public void Save()
        {
            if (_service == null)
            {
                LogUnboundService();
                return;
            }

            _service.Save(this);
        }

        /// <summary>Ghi domain này xuống đĩa ngay lập tức (đồng bộ, atomic). Dùng cho dữ liệu quan trọng (sau IAP, ...).</summary>
        public void SaveImmediate()
        {
            if (_service == null)
            {
                LogUnboundService();
                return;
            }

            _service.SaveImmediate(this);
        }

        private void LogUnboundService()
        {
            this.LogError(
                $"Chưa bind {nameof(IDataSaveService)} — hãy lấy data bằng {nameof(IDataSaveService)}.GetData<T>() " +
                "(inject service qua constructor) thay vì new trực tiếp.");
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
