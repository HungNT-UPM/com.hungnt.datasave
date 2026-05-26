using System;
using System.IO;
using System.Text;
using Sirenix.Serialization;

namespace HungNT.Datasave
{
    /// <summary>
    /// Serialize / deserialize <see cref="BaseSaveData"/> bằng <b>Odin</b> (<see cref="DataFormat.JSON"/>).
    /// </summary>
    internal static class DatasaveOdinIO
    {
        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        public static string SerializeToOdinJsonText(BaseSaveData data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            using var ms = new MemoryStream();
            SerializationUtility.SerializeValue(data, ms, DataFormat.JSON, new SerializationContext());
            return Utf8NoBom.GetString(ms.ToArray());
        }

        public static BaseSaveData DeserializeFromOdinJsonText(Type expectedConcreteType, string text)
        {
            if (string.IsNullOrEmpty(text))
                return null;

            var bytes = Utf8NoBom.GetBytes(text);
            using var ms = new MemoryStream(bytes, writable: false);

            var loaded = SerializationUtility.DeserializeValueWeak(ms, DataFormat.JSON, new DeserializationContext());
            if (loaded is not BaseSaveData b)
                return null;
            if (!expectedConcreteType.IsInstanceOfType(b))
                return null;
            return b;
        }
    }
}