using System;
using Newtonsoft.Json;

namespace HungNT.DataSave
{
    /// <summary>
    /// Một miền dữ liệu serializable, tương ứng một file JSON riêng trong thư mục persistent.
    /// Tên file mặc định là snake_case của tên class; override <see cref="SaveFileStem"/> để đổi.
    /// <para>Đây là dữ liệu thuần — không giữ tham chiếu tới service. Muốn ghi thì gọi
    /// <see cref="IDataSaveService.Save"/> / <see cref="IDataSaveService.SaveImmediate"/>
    /// trên service đã được inject.</para>
    /// </summary>
    [Serializable]
    public abstract class BaseSaveData
    {
        protected virtual string SaveFileStem => null;

        [JsonIgnore]
        public virtual string SaveFileName => $"{ResolveStem()}.json";

        /// <summary>Chạy sau khi đọc xong từ đĩa — dùng để dựng lại state phái sinh.</summary>
        public virtual void OnAfterLoad()
        {
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
