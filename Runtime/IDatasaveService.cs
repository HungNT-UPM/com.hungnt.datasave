namespace HungNT.Datasave
{
    /// <summary>Mỗi kiểu <see cref="BaseSaveData"/> tương ứng một file trong thư mục persistent (Odin Serialization).</summary>
    public interface IDatasaveService : IService
    {
        T GetData<T>() where T : BaseSaveData, new();

        void Save(BaseSaveData data);

        void Save<T>() where T : BaseSaveData, new();

        void SaveAll(bool hasLog);

        void ReloadFromDisk();

        void Delete<T>() where T : BaseSaveData, new();

        void DeleteAll();
    }
}