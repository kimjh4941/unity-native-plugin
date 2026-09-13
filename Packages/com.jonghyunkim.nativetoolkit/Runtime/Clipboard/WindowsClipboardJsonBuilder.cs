#nullable enable

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
namespace JonghyunKim.NativeToolkit.Runtime.Clipboard
{
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// Builds the JSON strings the native Windows clipboard APIs take: the file list of CopyFiles,
    /// the item array of CopyMultipleFormats, and the format-name array of ReserveDeferredFormats.
    /// <para>
    /// Hand-written rather than JsonUtility, matching AndroidClipboardJsonBuilder: the native side
    /// parses these with the WinRT JSON parser, which rejects a trailing comma or an unescaped
    /// control character, so the escaping has to be explicit.
    /// </para>
    /// <para>
    /// Intentional deviation from csharp.md's "log every public method" rule: these methods receive
    /// raw clipboard content, which may hold passwords or tokens, so no entry log is emitted at all.
    /// The caller (WindowsClipboardManager) logs a redacted summary instead.
    /// </para>
    /// </summary>
    public static class WindowsClipboardJsonBuilder
    {
        /// <summary>
        /// Builds the JSON array of file paths taken by CopyFiles.
        /// </summary>
        /// <param name="paths">Absolute file paths, in the order they should appear.</param>
        /// <returns>A JSON array string such as ["C:\\a.txt","C:\\b.png"].</returns>
        public static string BuildPathsJson(IReadOnlyList<string> paths) => BuildStringArray(paths);

        /// <summary>
        /// Builds the JSON array of format names taken by ReserveDeferredFormats.
        /// </summary>
        /// <param name="formatNames">Clipboard format names to reserve.</param>
        /// <returns>A JSON array string such as ["CF_UNICODETEXT","MyApp Format"].</returns>
        public static string BuildFormatNamesJson(IReadOnlyList<string> formatNames) =>
            BuildStringArray(formatNames);

        /// <summary>
        /// Builds the JSON array of items taken by CopyMultipleFormats.
        /// </summary>
        /// <param name="items">Format payloads, richest format first.</param>
        /// <returns>
        /// A JSON array string whose entries hold a "format" key and exactly one payload key.
        /// </returns>
        public static string BuildMultiFormatItemsJson(IReadOnlyList<WindowsClipboardFormatPayload> items)
        {
            var sb = new StringBuilder();
            sb.Append('[');
            if (items != null)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    WindowsClipboardFormatPayload item = items[i];
                    sb.Append("{\"format\":");
                    AppendEscaped(sb, item.Format);
                    sb.Append(",\"").Append(item.PayloadKey).Append("\":");
                    AppendEscaped(sb, item.Value);
                    sb.Append('}');
                }
            }
            sb.Append(']');
            return sb.ToString();
        }

        private static string BuildStringArray(IReadOnlyList<string> values)
        {
            var sb = new StringBuilder();
            sb.Append('[');
            if (values != null)
            {
                for (int i = 0; i < values.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    AppendEscaped(sb, values[i]);
                }
            }
            sb.Append(']');
            return sb.ToString();
        }

        /// <summary>
        /// Whether the character at this position is a surrogate with no partner.
        /// </summary>
        /// <param name="value">The string being written.</param>
        /// <param name="index">Position of the character to judge.</param>
        /// <returns>True for a high surrogate with no low one after it, or the reverse.</returns>
        private static bool IsLoneSurrogate(string value, int index)
        {
            char c = value[index];
            if (char.IsHighSurrogate(c))
            {
                return index + 1 >= value.Length || !char.IsLowSurrogate(value[index + 1]);
            }
            if (char.IsLowSurrogate(c))
            {
                return index == 0 || !char.IsHighSurrogate(value[index - 1]);
            }
            return false;
        }

        private static void AppendEscaped(StringBuilder sb, string? value)
        {
            sb.Append('"');
            if (value != null)
            {
                for (int i = 0; i < value.Length; i++)
                {
                    char c = value[i];
                    switch (c)
                    {
                        case '"': sb.Append("\\\""); break;
                        case '\\': sb.Append("\\\\"); break;
                        case '\b': sb.Append("\\b"); break;
                        case '\f': sb.Append("\\f"); break;
                        case '\n': sb.Append("\\n"); break;
                        case '\r': sb.Append("\\r"); break;
                        case '\t': sb.Append("\\t"); break;
                        default:
                            // A surrogate only means anything as half of a pair. On its own it is
                            // not valid UTF-16, and writing it raw hands the native parser text it
                            // will reject; escaped, the payload stays well formed and the value
                            // survives for whoever has to work out where it came from.
                            // Everything else non-ASCII passes through: the boundary is UTF-16 on
                            // both sides.
                            if (c < 0x20 || IsLoneSurrogate(value, i))
                            {
                                sb.Append("\\u").Append(((int)c).ToString("x4"));
                            }
                            else
                            {
                                sb.Append(c);
                            }
                            break;
                    }
                }
            }
            sb.Append('"');
        }
    }
}
#endif
