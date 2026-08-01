#nullable enable

using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace JonghyunKim.NativeToolkit.Runtime.Clipboard
{
    /// <summary>
    /// Builds JSON strings for Android clipboard copy APIs with full control over optional fields.
    /// Does not use external JSON libraries; matches the hand-written serializer pattern used by
    /// <c>AndroidShareJsonBuilder</c>.
    ///
    /// Intentional deviation from csharp.md's "log every public/internal method" rule: methods here
    /// receive raw clipboard content (may hold passwords or tokens), so no entry log is emitted at
    /// all, matching the same rationale as <see cref="AndroidClipboardManager"/> (5.7.2 of the
    /// clipboard design). Only the caller (AndroidClipboardManager) logs a redacted summary.
    /// </summary>
    public static class AndroidClipboardJsonBuilder
    {
        /// <summary>
        /// Builds JSON for the <c>copyPlainText</c> native operation.
        /// </summary>
        /// <param name="payload">Plain-text copy payload to serialize.</param>
        /// <returns>JSON string matching the <c>UnityClipboardJsonParser</c> copyPlainText schema.</returns>
        public static string BuildCopyPlainTextJson(CopyPlainTextPayload payload)
        {
            // "text" is always emitted, even when blank: the native parser calls getString("text"),
            // so a missing key (not a blank value) would turn into CLIPBOARD_UNKNOWN.
            var obj = new Dictionary<string, object?> { ["text"] = payload.text, ["isSensitive"] = payload.isSensitive };
            AddIfNotNullOrWhiteSpace(obj, "label", payload.label);
            return SerializeObject(obj);
        }

        /// <summary>
        /// Builds JSON for the <c>copyHtmlText</c> native operation.
        /// </summary>
        /// <param name="payload">HTML-text copy payload to serialize.</param>
        /// <returns>JSON string matching the <c>UnityClipboardJsonParser</c> copyHtmlText schema.</returns>
        public static string BuildCopyHtmlTextJson(CopyHtmlTextPayload payload)
        {
            var obj = new Dictionary<string, object?>
            {
                ["plainText"] = payload.plainText,
                ["htmlText"] = payload.htmlText,
                ["isSensitive"] = payload.isSensitive
            };
            AddIfNotNullOrWhiteSpace(obj, "label", payload.label);
            return SerializeObject(obj);
        }

        /// <summary>
        /// Builds JSON for the <c>copyUri</c> native operation.
        /// </summary>
        /// <param name="payload">URI copy payload to serialize.</param>
        /// <returns>JSON string matching the <c>UnityClipboardJsonParser</c> copyUri schema.</returns>
        public static string BuildCopyUriJson(CopyUriPayload payload)
        {
            var obj = new Dictionary<string, object?> { ["uri"] = payload.uri, ["isSensitive"] = payload.isSensitive };
            AddIfNotNullOrWhiteSpace(obj, "label", payload.label);
            return SerializeObject(obj);
        }

        /// <summary>
        /// Builds JSON for the <c>copyMultipleText</c> native operation.
        /// </summary>
        /// <param name="payload">Multiple plain-text copy payload to serialize.</param>
        /// <returns>JSON string matching the <c>UnityClipboardJsonParser</c> copyMultipleText schema.</returns>
        public static string BuildCopyMultipleTextJson(CopyMultipleTextPayload payload)
        {
            var obj = new Dictionary<string, object?>
            {
                ["texts"] = BuildStringList(payload.texts),
                ["isSensitive"] = payload.isSensitive
            };
            AddIfNotNullOrWhiteSpace(obj, "label", payload.label);
            return SerializeObject(obj);
        }

        private static void AddIfNotNullOrWhiteSpace(Dictionary<string, object?> target, string key, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                target[key] = value;
            }
        }

        private static List<object?> BuildStringList(string[] values)
        {
            var list = new List<object?>(values.Length);
            foreach (var v in values)
            {
                list.Add(v);
            }
            return list;
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
