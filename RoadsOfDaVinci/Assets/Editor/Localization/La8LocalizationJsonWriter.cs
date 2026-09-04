using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Creobit.LA8.EditorTools.Localization
{
    internal static class La8LocalizationJsonWriter
    {
        internal static void Write(string filePath, Dictionary<string, string> data)
        {
            var builder = new StringBuilder();
            builder.AppendLine("{");
            builder.AppendLine("  \"Data\": {");

            int index = 0;
            int count = data.Count;
            foreach (var pair in data)
            {
                index++;
                builder.Append("    \"");
                builder.Append(EscapeJson(pair.Key));
                builder.Append("\": \"");
                builder.Append(EscapeJson(pair.Value));
                builder.Append('"');
                if (index < count)
                    builder.Append(',');

                builder.AppendLine();
            }

            builder.AppendLine("  }");
            builder.Append('}');

            File.WriteAllText(filePath, builder.ToString(), new UTF8Encoding(false));
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t");
        }
    }
}
