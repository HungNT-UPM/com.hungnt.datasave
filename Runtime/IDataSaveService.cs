namespace HungNT.DataSave
{
    /// <summary>
    /// Mỗi kiểu <see cref="BaseSaveData"/> tương ứng một file JSON trong thư mục persistent.
    /// <para><see cref="Save(BaseSaveData)"/> = đánh dấu dirty, ghi gộp theo chu kỳ flush + khi pause/quit
    /// (background thread, atomic). Cần ghi ngay: <see cref="SaveImmediate"/> / <see cref="FlushDirty"/>.</para>
    /// </summary>
    public interface IDataSaveService : IService
    {
        T GetData<T>() where T : BaseSaveData, new();

        /// <summary>Đánh dấu domain dirty — ghi ở lần flush kế tiếp (interval / pause / quit). Gọi nhiều lần không tốn IO.</summary>
        void Save(BaseSaveData data);

        /// <summary>Đánh dấu domain <typeparamref name="T"/> dirty (load nếu chưa cache).</summary>
        void Save<T>() where T : BaseSaveData, new();

        /// <summary>Ghi một domain xuống đĩa ngay (đồng bộ, atomic).</summary>
        void SaveImmediate(BaseSaveData data);

        /// <summary>Ghi ngay mọi domain đang dirty (đồng bộ). Gọi trước thao tác rủi ro nếu cần chắc chắn dữ liệu đã trên đĩa.</summary>
        void FlushDirty();

        /// <summary>Ghi ngay TOÀN BỘ domain đã cache xuống đĩa, dirty hay không (đồng bộ).</summary>
        void SaveAll(bool hasLog);

        void ReloadFromDisk();

        void Delete<T>() where T : BaseSaveData, new();

        void DeleteAll();
    }
}
