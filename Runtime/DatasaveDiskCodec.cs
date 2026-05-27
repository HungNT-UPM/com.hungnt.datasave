using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace HungNT.Datasave
{
    /// <summary>
    /// Đĩa DEBUG: text Odin JSON (đọc được); release: AES bọc cùng chuỗi Odin JSON.
    /// </summary>
    internal static class DatasaveDiskCodec
    {
        private static readonly byte[] Magic = Encoding.ASCII.GetBytes("HNT1");

        private const byte PayloadFormatVersion = 1;

        private const int IvLengthBytes = 16;

        /// <summary>Đổi đuôi <c>.json</c> → <c>.datasave</c> khi build không DEBUG để tránh nhầm file text.</summary>
        public const string EncryptedExtension = ".datasave";

        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        /// <summary>True khi build có symbol DEBUG (thường là Editor + nhiều Development build).</summary>
        public static bool UsesPlainTextOnDisk
        {
#if DEBUG
            get => true;
#else
            get => false;
#endif
        }

        public static string ToPhysicalSaveFileName(string saveFileName)
        {
            if (string.IsNullOrWhiteSpace(saveFileName))
                return saveFileName;

            var t = saveFileName.Trim();
#if DEBUG
            return t;
#else
            if (t.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                return t.Substring(0, t.Length - 5) + EncryptedExtension;

            return Path.ChangeExtension(t, EncryptedExtension.TrimStart('.'));
#endif
        }

        public static void WriteJsonFile(string fullPath, string json)
        {
            var dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

#if DEBUG
            File.WriteAllText(fullPath, json, Utf8NoBom);
#else
            var blob = EncryptToBlob(json);
            File.WriteAllBytes(fullPath, blob);
#endif
        }

        /// <summary>Đọc chuỗi payload (Odin JSON khi DEBUG; sau giải mã khi release).</summary>
        public static string ReadJsonFile(string fullPath)
        {
            if (!File.Exists(fullPath))
                return null;

#if DEBUG
            return File.ReadAllText(fullPath, Utf8NoBom);
#else
            var bytes = File.ReadAllBytes(fullPath);
            if (bytes.Length == 0)
                return null;

            if (HasMagic(bytes))
                return DecryptFromBlob(bytes);

            throw new InvalidDataException("Datasave: expected HNT1 encrypted payload.");
#endif
        }

#if !DEBUG
        private static readonly Lazy<byte[]> KeyLazy = new Lazy<byte[]>(DeriveKey);

        private static byte[] DeriveKey()
        {
            const string password = "HungNT.Datasave.v1";
            var salt = Encoding.UTF8.GetBytes("HungNT.Datasave.Pbkdf2Salt.v1");
            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 100_000, HashAlgorithmName.SHA256);
            return pbkdf2.GetBytes(32);
        }

        private static bool HasMagic(byte[] bytes)
        {
            if (bytes == null || bytes.Length < Magic.Length)
                return false;
            for (var i = 0; i < Magic.Length; i++)
            {
                if (bytes[i] != Magic[i])
                    return false;
            }

            return true;
        }

        private static byte[] EncryptToBlob(string plainText)
        {
            var key = KeyLazy.Value;
            using var aes = Aes.Create();
            aes.Key = key;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.GenerateIV();
            if (aes.IV.Length != IvLengthBytes)
                throw new InvalidOperationException("AES IV length mismatch.");

            using var encryptor = aes.CreateEncryptor();
            var plain = Encoding.UTF8.GetBytes(plainText);
            var cipher = encryptor.TransformFinalBlock(plain, 0, plain.Length);

            var len = Magic.Length + 1 + IvLengthBytes + cipher.Length;
            var result = new byte[len];
            Buffer.BlockCopy(Magic, 0, result, 0, Magic.Length);
            result[Magic.Length] = PayloadFormatVersion;
            Buffer.BlockCopy(aes.IV, 0, result, Magic.Length + 1, IvLengthBytes);
            Buffer.BlockCopy(cipher, 0, result, Magic.Length + 1 + IvLengthBytes, cipher.Length);
            return result;
        }

        private static string DecryptFromBlob(byte[] bytes)
        {
            var min = Magic.Length + 1 + IvLengthBytes + 1;
            if (bytes.Length < min || !HasMagic(bytes))
                throw new InvalidDataException("Invalid datasave header.");

            if (bytes[Magic.Length] != PayloadFormatVersion)
                throw new InvalidDataException($"Unsupported datasave format version: {bytes[Magic.Length]}.");

            var key = KeyLazy.Value;
            var iv = new byte[IvLengthBytes];
            Buffer.BlockCopy(bytes, Magic.Length + 1, iv, 0, IvLengthBytes);
            var cipherLen = bytes.Length - (Magic.Length + 1 + IvLengthBytes);
            var cipher = new byte[cipherLen];
            Buffer.BlockCopy(bytes, Magic.Length + 1 + IvLengthBytes, cipher, 0, cipherLen);

            using var aes = Aes.Create();
            aes.Key = key;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.IV = iv;
            using var decryptor = aes.CreateDecryptor();
            var plain = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
            return Utf8NoBom.GetString(plain);
        }
#endif

        public static void DeleteFileIfExists(string fullPath)
        {
            if (File.Exists(fullPath))
                File.Delete(fullPath);
        }
    }
}
