using System.IO;
using System.Text;

namespace HungNT.DataSave
{
    /// <summary>
    /// Đọc/ghi file save (plain text UTF-8, không mã hóa — đọc và sửa được khi debug).
    /// Ghi <b>atomic</b>: ra file <c>.tmp</c> rồi thay thế file chính — crash giữa chừng không làm hỏng save cũ.
    /// </summary>
    internal static class DataSaveFileIO
    {
        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        /// <summary>
        /// Ghi atomic (tmp + replace). An toàn gọi từ background thread —
        /// caller tự đảm bảo không ghi song song cùng một file (DataSaveService lock).
        /// </summary>
        public static void WriteAtomic(string fullPath, string text)
        {
            var dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var tmpPath = fullPath + ".tmp";
            File.WriteAllText(tmpPath, text, Utf8NoBom);

            if (File.Exists(fullPath))
                File.Replace(tmpPath, fullPath, null); // atomic trên cùng volume
            else
                File.Move(tmpPath, fullPath);
        }

        /// <summary>Đọc toàn bộ nội dung file; null nếu file không tồn tại.</summary>
        public static string ReadAllText(string fullPath)
        {
            return File.Exists(fullPath) ? File.ReadAllText(fullPath, Utf8NoBom) : null;
        }

        public static void DeleteFileIfExists(string fullPath)
        {
            if (File.Exists(fullPath))
                File.Delete(fullPath);

            var tmpPath = fullPath + ".tmp";
            if (File.Exists(tmpPath))
                File.Delete(tmpPath);
        }
    }
}
