using System;
using System.Text;

namespace HungNT.Datasave
{
    internal static class SaveDataNaming
    {
        public static string ToSnakeStem(Type payloadType)
        {
            if (!typeof(BaseSaveData).IsAssignableFrom(payloadType))
            {
                DebugEx.LogError($"[SaveDataNaming] {payloadType?.Name} không kế thừa {nameof(BaseSaveData)} — stem fallback.");
                return "unknown_save_data";
            }

            return PascalCaseToSnake(payloadType.Name);
        }

        private static string PascalCaseToSnake(string name)
        {
            if (string.IsNullOrEmpty(name))
                return string.Empty;

            var sb = new StringBuilder(name.Length + Math.Min(name.Length, 16));
            sb.Append(char.ToLowerInvariant(name[0]));
            for (var i = 1; i < name.Length; i++)
            {
                var c = name[i];
                if (char.IsUpper(c))
                    sb.Append('_').Append(char.ToLowerInvariant(c));
                else
                    sb.Append(c);
            }

            return sb.ToString();
        }
    }
}