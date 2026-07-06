using System;
using Newtonsoft.Json;

namespace HungNT.DataSave
{
    /// <summary>
    /// Serialize / deserialize <see cref="BaseSaveData"/> bằng <b>Newtonsoft Json</b> —
    /// indented cho dễ đọc, hỗ trợ Dictionary, không nhúng $type (file sạch, không phụ thuộc assembly name).
    /// Save model nên là plain C# data (field public / property có setter), không tham chiếu UnityEngine.Object.
    /// </summary>
    internal static class DataSaveJsonIO
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            TypeNameHandling = TypeNameHandling.None,
            // Collection khởi tạo sẵn trong constructor được thay hẳn bằng nội dung file
            // (tránh merge trùng phần tử); seed giá trị thiếu trong OnAfterLoad.
            ObjectCreationHandling = ObjectCreationHandling.Replace,
        };

        public static string Serialize(BaseSaveData data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            return JsonConvert.SerializeObject(data, Settings);
        }

        /// <summary>Trả về null nếu text rỗng hoặc không parse được về đúng <paramref name="expectedConcreteType"/>.</summary>
        public static BaseSaveData Deserialize(Type expectedConcreteType, string text)
        {
            if (string.IsNullOrEmpty(text))
                return null;

            return JsonConvert.DeserializeObject(text, expectedConcreteType, Settings) as BaseSaveData;
        }
    }
}
