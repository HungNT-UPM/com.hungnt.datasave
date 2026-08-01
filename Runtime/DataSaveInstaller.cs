using VContainer;
using VContainer.Unity;

namespace HungNT.DataSave
{
    /// <summary>Đăng ký <see cref="IDataSaveService"/>. Gọi ở scope gốc — save data dùng chung toàn game.</summary>
    public static class DataSaveInstaller
    {
        /// <summary>
        /// Cần <see cref="IAppLifecycle"/> đã đăng ký trước (gọi <c>builder.InstallCore()</c>).
        /// <c>RegisterEntryPoint</c> để container tự chạy <c>Tick</c> (flush theo chu kỳ) và <c>Dispose</c>.
        /// </summary>
        /// <param name="flushInterval">Chu kỳ gộp ghi xuống đĩa (giây).</param>
        public static IContainerBuilder InstallDataSave(
            this IContainerBuilder builder,
            float flushInterval = DataSaveService.DefaultFlushInterval)
        {
            builder.RegisterEntryPoint<DataSaveService>()
                .WithParameter("flushInterval", flushInterval);

            return builder;
        }
    }
}
