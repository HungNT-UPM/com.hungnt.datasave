using VContainer;
using VContainer.Unity;

namespace HungNT.DataSave
{
    /// <summary>Đăng ký <see cref="IDataSaveService"/>. Gọi ở scope gốc — save data dùng chung toàn game.</summary>
    public static class DataSaveInstaller
    {
        /// <summary>
        /// Cần <see cref="IAppLifecycleService"/> đã đăng ký trước (<c>InstallCore</c>).
        /// Đăng ký dạng entry point để container tự chạy flush theo chu kỳ và ghi nốt khi scope kết thúc.
        /// </summary>
        public static IContainerBuilder InstallDataSave(this IContainerBuilder builder)
        {
            builder.RegisterEntryPoint<DataSaveService>();
            return builder;
        }
    }
}
