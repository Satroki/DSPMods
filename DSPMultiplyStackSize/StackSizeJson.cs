using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace DSPMultiplyStackSize
{
    /// <summary>
    /// Minimal serializer for the { "itemId": stackSize } config file.
    /// Kept dependency-free so the plugin does not need an extra JSON library.
    /// </summary>
    internal static class StackSizeJson
    {
        public static string ToJson(Dictionary<int, int> values)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append('{');
            bool first = true;
            foreach (KeyValuePair<int, int> pair in values)
            {
                if (!first)
                {
                    sb.Append(',');
                }
                first = false;
                sb.Append('"').Append(pair.Key.ToString(CultureInfo.InvariantCulture)).Append("\":").Append(pair.Value.ToString(CultureInfo.InvariantCulture));
            }
            sb.Append('}');
            return sb.ToString();
        }

        public static Dictionary<int, int> FromJson(string json)
        {
            Dictionary<int, int> result = new Dictionary<int, int>();
            if (string.IsNullOrEmpty(json))
            {
                return result;
            }

            int i = 0;
            while (i < json.Length && json[i] != '{')
            {
                i++;
            }
            i++;

            while (i < json.Length)
            {
                while (i < json.Length && (char.IsWhiteSpace(json[i]) || json[i] == ','))
                {
                    i++;
                }
                if (i >= json.Length || json[i] == '}')
                {
                    break;
                }
                if (json[i] != '"')
                {
                    i++;
                    continue;
                }

                int keyStart = ++i;
                while (i < json.Length && json[i] != '"')
                {
                    i++;
                }
                string keyText = json.Substring(keyStart, i - keyStart);
                if (i < json.Length)
                {
                    i++;
                }

                while (i < json.Length && (char.IsWhiteSpace(json[i]) || json[i] == ':'))
                {
                    i++;
                }

                int valueStart = i;
                while (i < json.Length && (char.IsDigit(json[i]) || json[i] == '-'))
                {
                    i++;
                }

                if (int.TryParse(keyText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int key) &&
                    int.TryParse(json.Substring(valueStart, i - valueStart), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
                {
                    result[key] = value;
                }
            }

            return result;
        }
    }
}
