#nullable enable

using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace JonghyunKim.NativeToolkit.Runtime.Share
{
    /// <summary>
    /// Builds JSON strings for the iOS <c>shareContent</c> native operation with full control over
    /// optional fields. Does not use external JSON libraries; matches the hand-written serializer
    /// pattern used by <c>AndroidShareJsonBuilder</c>.
    /// </summary>
    public static class IosShareJsonBuilder
    {
        /// <summary>
        /// Builds JSON for the <c>shareContent</c> native operation.
        /// </summary>
        /// <param name="payload">Share content payload to serialize.</param>
        /// <returns>JSON string matching the <c>UnityIosShareJsonParser</c> schema.</returns>
        public static string BuildShareContentJson(IosShareContentPayload payload)
        {
            var obj = new Dictionary<string, object?> { ["items"] = BuildItemsList(payload.items) };
            AddIfNotNullOrWhiteSpace(obj, "subject", payload.subject);
            AddIfNotNullOrWhiteSpace(obj, "previewTitle", payload.previewTitle);
            AddExcludedActivityTypes(obj, payload.excludedActivityTypes);
            return SerializeObject(obj);
        }

        private static List<object?> BuildItemsList(IosShareItem[] items)
        {
            var list = new List<object?>(items.Length);
            foreach (var item in items)
            {
                if (item == null)
                {
                    // Defensive: null entries are excluded rather than serialized or thrown on.
                    continue;
                }

                list.Add(new Dictionary<string, object?>
                {
                    ["type"] = item.type,
                    ["value"] = item.value
                });
            }
            return list;
        }

        private static void AddExcludedActivityTypes(Dictionary<string, object?> target, string[]? excludedActivityTypes)
        {
            if (excludedActivityTypes == null || excludedActivityTypes.Length == 0)
            {
                return;
            }

            var list = new List<object?>(excludedActivityTypes.Length);
            foreach (var value in excludedActivityTypes)
            {
                list.Add(value);
            }
            target["excludedActivityTypes"] = list;
        }

        private static void AddIfNotNullOrWhiteSpace(Dictionary<string, object?> target, string key, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                target[key] = value;
            }
        }

        private static string SerializeObject(Dictionary<string, object?> obj)
        {
            var builder = new StringBuilder();
            AppendValue(builder, obj);
            return builder.ToString();
        }

        private static void AppendValue(StringBuilder builder, object? value)
        {
            switch (value)
            {
                case null:
                    builder.Append("null");
                    break;
                case string s:
                    AppendEscapedString(builder, s);
                    break;
                case bool b:
                    builder.Append(b ? "true" : "false");
                    break;
                case int i:
                    builder.Append(i.ToString(CultureInfo.InvariantCulture));
                    break;
                case long l:
                    builder.Append(l.ToString(CultureInfo.InvariantCulture));
                    break;
                case Dictionary<string, object?> dict:
                    AppendObject(builder, dict);
                    break;
                case List<object?> list:
                    AppendArray(builder, list);
                    break;
            }
        }

        private static void AppendObject(StringBuilder builder, Dictionary<string, object?> obj)
        {
            builder.Append('{');
            bool isFirst = true;
            foreach (var pair in obj)
            {
                if (!isFirst)
                {
                    builder.Append(',');
                }
                AppendEscapedString(builder, pair.Key);
                builder.Append(':');
                AppendValue(builder, pair.Value);
                isFirst = false;
            }
            builder.Append('}');
        }

        private static void AppendArray(StringBuilder builder, List<object?> values)
        {
            builder.Append('[');
            for (int i = 0; i < values.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(',');
                }
                AppendValue(builder, values[i]);
            }
            builder.Append(']');
        }

        private static void AppendEscapedString(StringBuilder builder, string value)
        {
            builder.Append('"');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\b': builder.Append("\\b"); break;
                    case '\f': builder.Append("\\f"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                        {
                            builder.Append("\\u");
                            builder.Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            builder.Append(c);
                        }
                        break;
                }
            }
            builder.Append('"');
        }
    }
}
