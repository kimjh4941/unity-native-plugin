#nullable enable

#if UNITY_STANDALONE_OSX || UNITY_EDITOR
namespace JonghyunKim.NativeToolkit.Runtime.Clipboard
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text;

    /// <summary>Kind of a parsed JSON value.</summary>
    internal enum MacJsonValueKind
    {
        Object,
        Array,
        String,
        Number,
        Bool,
        Null
    }

    /// <summary>Outcome of decoding a base64 string member.</summary>
    internal enum MacJsonBase64Status
    {
        /// <summary>Decoded successfully.</summary>
        Success,

        /// <summary>The value was not a JSON string.</summary>
        NotAString,

        /// <summary>The value was not canonical base64 (bad length, bad padding, bad characters).</summary>
        Malformed,

        /// <summary>The decoded length would exceed the caller's limit. Nothing was allocated.</summary>
        TooLarge
    }

    /// <summary>
    /// A parsed JSON value.
    /// <para>
    /// Strings and numbers keep an offset into the source text rather than an eagerly cut
    /// substring. For a 64 MiB payload the base64 member alone would otherwise cost roughly
    /// 170 MB of managed UTF-16 on top of the source string, so
    /// <see cref="TryGetBase64Bytes"/> decodes straight from the source span.
    /// </para>
    /// </summary>
    internal sealed class MacJsonValue
    {
        internal MacJsonValueKind Kind { get; }

        private readonly string? _source;
        private readonly int _start;
        private readonly int _length;
        private readonly bool _hasEscapes;
        private readonly bool _boolValue;
        private readonly Dictionary<string, MacJsonValue>? _members;
        private readonly List<MacJsonValue>? _elements;

        private MacJsonValue(MacJsonValueKind kind)
        {
            Kind = kind;
        }

        private MacJsonValue(MacJsonValueKind kind, string source, int start, int length, bool hasEscapes)
        {
            Kind = kind;
            _source = source;
            _start = start;
            _length = length;
            _hasEscapes = hasEscapes;
        }

        private MacJsonValue(bool value)
        {
            Kind = MacJsonValueKind.Bool;
            _boolValue = value;
        }

        private MacJsonValue(Dictionary<string, MacJsonValue> members)
        {
            Kind = MacJsonValueKind.Object;
            _members = members;
        }

        private MacJsonValue(List<MacJsonValue> elements)
        {
            Kind = MacJsonValueKind.Array;
            _elements = elements;
        }

        internal static MacJsonValue Null { get; } = new(MacJsonValueKind.Null);

        internal static MacJsonValue FromBool(bool value) => new(value);

        internal static MacJsonValue FromString(string source, int start, int length, bool hasEscapes) =>
            new(MacJsonValueKind.String, source, start, length, hasEscapes);

        internal static MacJsonValue FromNumber(string source, int start, int length) =>
            new(MacJsonValueKind.Number, source, start, length, false);

        internal static MacJsonValue FromObject(Dictionary<string, MacJsonValue> members) => new(members);

        internal static MacJsonValue FromArray(List<MacJsonValue> elements) => new(elements);

        /// <summary>True when this value is JSON <c>null</c>.</summary>
        internal bool IsNull => Kind == MacJsonValueKind.Null;

        /// <summary>
        /// Looks up an object member. Returns false for a non-object, an absent key, or a null
        /// <paramref name="key"/>. Required-versus-optional is decided by the caller.
        /// </summary>
        internal bool TryGetMember(string key, out MacJsonValue value)
        {
            if (Kind == MacJsonValueKind.Object && _members!.TryGetValue(key, out MacJsonValue? found))
            {
                value = found;
                return true;
            }
            value = Null;
            return false;
        }

        /// <summary>Returns the member, or null when absent or JSON null.</summary>
        internal MacJsonValue? GetMemberOrNull(string key)
        {
            if (!TryGetMember(key, out MacJsonValue value) || value.IsNull)
            {
                return null;
            }
            return value;
        }

        /// <summary>
        /// Member names of an object, in no particular order. Empty for a non-object.
        /// <para>
        /// Needed because <c>representations</c> is keyed by uniform type identifier, so the keys
        /// are data rather than a fixed schema and cannot be read by name.
        /// </para>
        /// </summary>
        internal IReadOnlyCollection<string> MemberNames() =>
            Kind == MacJsonValueKind.Object ? _members!.Keys : Array.Empty<string>();

        /// <summary>Array elements. Empty for a non-array.</summary>
        internal IReadOnlyList<MacJsonValue> AsArray() =>
            Kind == MacJsonValueKind.Array ? _elements! : Array.Empty<MacJsonValue>();

        /// <summary>Materializes a string value, decoding escapes only when the scan found any.</summary>
        internal bool TryGetString(out string value)
        {
            if (Kind != MacJsonValueKind.String)
            {
                value = string.Empty;
                return false;
            }

            value = _hasEscapes ? Unescape(_source!, _start, _length) : _source!.Substring(_start, _length);
            return true;
        }

        internal bool TryGetBool(out bool value)
        {
            if (Kind != MacJsonValueKind.Bool)
            {
                value = false;
                return false;
            }
            value = _boolValue;
            return true;
        }

        internal bool TryGetDouble(out double value)
        {
            if (Kind != MacJsonValueKind.Number)
            {
                value = 0;
                return false;
            }
            return double.TryParse(NumberSpan(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>
        /// Reads a 64-bit integer. The native side declares Swift's <c>Int</c>, which is 64-bit,
        /// for every <c>changeCount</c>; <see cref="TryGetInt"/> fails outside the <c>int</c>
        /// range, which would turn a valid response into a parse failure.
        /// </summary>
        /// <param name="value">Parsed value, or 0 when this is not an integral number.</param>
        /// <returns><c>false</c> when the value is not a number or does not fit in a <c>long</c>.</returns>
        internal bool TryGetInt64(out long value)
        {
            value = 0;
            if (Kind != MacJsonValueKind.Number)
            {
                return false;
            }
            // Accept an integral double ("3.0") as well: JSONSerialization may emit either form.
            if (long.TryParse(NumberSpan(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                return true;
            }

            if (double.TryParse(NumberSpan(), NumberStyles.Float, CultureInfo.InvariantCulture, out double asDouble)
                && asDouble >= long.MinValue && asDouble <= long.MaxValue
                && Math.Abs(asDouble - Math.Round(asDouble)) < double.Epsilon)
            {
                value = (long)Math.Round(asDouble);
                return true;
            }

            return false;
        }

        internal bool TryGetInt(out int value)
        {
            value = 0;
            if (Kind != MacJsonValueKind.Number)
            {
                return false;
            }
            // Accept an integral double ("3.0") as well: JSONSerialization may emit either form.
            if (long.TryParse(NumberSpan(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed))
            {
                if (parsed < int.MinValue || parsed > int.MaxValue)
                {
                    return false;
                }
                value = (int)parsed;
                return true;
            }

            if (double.TryParse(NumberSpan(), NumberStyles.Float, CultureInfo.InvariantCulture, out double asDouble)
                && asDouble >= int.MinValue && asDouble <= int.MaxValue
                && Math.Abs(asDouble - Math.Round(asDouble)) < double.Epsilon)
            {
                value = (int)Math.Round(asDouble);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Decodes a base64 string member without materializing it as a managed string.
        /// The limit is checked from the exact decoded length before any buffer is allocated, so
        /// an oversized payload never costs memory. A token whose slashes arrive escaped is copied
        /// into a char buffer first, which is the one case that does allocate ahead of the check.
        /// </summary>
        /// <param name="maxDecodedLength">Inclusive upper bound on the decoded byte count.</param>
        /// <param name="bytes">Decoded bytes on success, otherwise null.</param>
        /// <returns>The outcome of the decode attempt.</returns>
        internal MacJsonBase64Status TryGetBase64Bytes(long maxDecodedLength, out byte[]? bytes)
        {
            bytes = null;
            if (Kind != MacJsonValueKind.String)
            {
                return MacJsonBase64Status.NotAString;
            }

            // '/' is part of the base64 alphabet and JSON producers may escape it as "\/", which
            // Apple's JSONSerialization does unless .withoutEscapingSlashes is set. Such a payload
            // is valid base64, so it is unescaped into a buffer first; only "\/" can appear, and any
            // other escape means the token is not canonical base64.
            ReadOnlySpan<char> span;
            if (_hasEscapes)
            {
                if (!TryUnescapeSlashes(_source!, _start, _length, out char[]? unescaped, out int unescapedLength))
                {
                    return MacJsonBase64Status.Malformed;
                }
                span = unescaped!.AsSpan(0, unescapedLength);
            }
            else
            {
                span = _source!.AsSpan(_start, _length);
            }

            if (!TryGetDecodedLength(span, out long decodedLength))
            {
                return MacJsonBase64Status.Malformed;
            }

            if (decodedLength > maxDecodedLength)
            {
                return MacJsonBase64Status.TooLarge;
            }

            var buffer = new byte[decodedLength];
            if (!Convert.TryFromBase64Chars(span, buffer, out int written) || written != decodedLength)
            {
                return MacJsonBase64Status.Malformed;
            }

            bytes = buffer;
            return MacJsonBase64Status.Success;
        }

        /// <summary>
        /// Copies a string token with only "\/" escapes into a buffer, dropping the backslashes.
        /// </summary>
        /// <remarks>
        /// Used only for base64, where '/' is the single escapable character the alphabet contains.
        /// Any other escape means the token is not base64 at all, so it is rejected rather than
        /// decoded generically: that keeps this path from allocating for a value it cannot use.
        /// </remarks>
        private static bool TryUnescapeSlashes(string source, int start, int length, out char[]? buffer, out int written)
        {
            buffer = null;
            written = 0;
            var result = new char[length];
            int end = start + length;
            int index = 0;
            for (int i = start; i < end; i++)
            {
                char current = source[i];
                if (current != '\\')
                {
                    result[index++] = current;
                    continue;
                }

                if (i + 1 >= end || source[i + 1] != '/')
                {
                    return false;
                }
                result[index++] = '/';
                i++;
            }

            buffer = result;
            written = index;
            return true;
        }

        /// <summary>
        /// Exact decoded length of a canonical base64 token.
        /// <para>
        /// <c>(length / 4) * 3</c> alone overestimates by the padding count, which would reject a
        /// legal payload sitting exactly on the size limit: 64 MiB encodes to 89,478,488 chars,
        /// and the naive formula yields 67,108,866 rather than 67,108,864.
        /// </para>
        /// </summary>
        /// <param name="base64">Base64 characters, without surrounding quotes.</param>
        /// <param name="decodedLength">Exact decoded byte count on success.</param>
        /// <returns><c>true</c> when the token has a canonical length and padding.</returns>
        internal static bool TryGetDecodedLength(ReadOnlySpan<char> base64, out long decodedLength)
        {
            decodedLength = 0;
            if (base64.Length == 0)
            {
                return true;
            }

            if (base64.Length % 4 != 0)
            {
                return false;
            }

            int padding = 0;
            if (base64[base64.Length - 1] == '=')
            {
                padding++;
                if (base64.Length >= 2 && base64[base64.Length - 2] == '=')
                {
                    padding++;
                }
            }

            // Three or more padding characters is never canonical base64.
            if (base64.Length >= 3 && base64[base64.Length - 3] == '=')
            {
                return false;
            }

            decodedLength = ((long)base64.Length / 4) * 3 - padding;
            return true;
        }

        private ReadOnlySpan<char> NumberSpan() =>
            Kind == MacJsonValueKind.Number ? _source!.AsSpan(_start, _length) : ReadOnlySpan<char>.Empty;

        private static string Unescape(string source, int start, int length)
        {
            var builder = new StringBuilder(length);
            int i = start;
            int end = start + length;
            while (i < end)
            {
                char c = source[i];
                if (c != '\\')
                {
                    builder.Append(c);
                    i++;
                    continue;
                }

                i++;
                if (i >= end)
                {
                    break;
                }

                char escape = source[i++];
                switch (escape)
                {
                    case '"': builder.Append('"'); break;
                    case '\\': builder.Append('\\'); break;
                    case '/': builder.Append('/'); break;
                    case 'b': builder.Append('\b'); break;
                    case 'f': builder.Append('\f'); break;
                    case 'n': builder.Append('\n'); break;
                    case 'r': builder.Append('\r'); break;
                    case 't': builder.Append('\t'); break;
                    case 'u':
                        if (i + 4 <= end && TryParseHex4(source, i, out char decoded))
                        {
                            // Surrogate pairs arrive as two consecutive \uXXXX escapes; appending
                            // each unit in order reconstructs the pair without special handling.
                            builder.Append(decoded);
                            i += 4;
                        }
                        break;
                    default:
                        builder.Append(escape);
                        break;
                }
            }
            return builder.ToString();
        }

        private static bool TryParseHex4(string source, int index, out char value)
        {
            int result = 0;
            for (int offset = 0; offset < 4; offset++)
            {
                int digit = HexDigit(source[index + offset]);
                if (digit < 0)
                {
                    value = '\0';
                    return false;
                }
                result = (result << 4) | digit;
            }
            value = (char)result;
            return true;
        }

        private static int HexDigit(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'a' && c <= 'f') return c - 'a' + 10;
            if (c >= 'A' && c <= 'F') return c - 'A' + 10;
            return -1;
        }
    }

    /// <summary>
    /// Minimal recursive-descent JSON reader for the macOS clipboard bridge responses.
    /// <para>
    /// <c>JsonUtility</c> cannot be used here: the snapshot payload contains <c>itemTypes</c>
    /// (an array of arrays), <c>readData</c> reports "no data" as a null <c>data</c> member, the
    /// detection payloads write every absent value as an explicit null rather than omitting the
    /// key, and structural problems must fail rather than be filled in with default values.
    /// </para>
    /// <para>
    /// Accepts exactly what <c>JSONSerialization</c> emits. Comments, trailing commas, single
    /// quotes, NaN and Infinity are rejected. Never throws: a malformed document returns null.
    /// </para>
    /// <para>
    /// Intentional deviation from the "log every internal method" rule in csharp.md: this type
    /// receives raw clipboard payloads, which may hold passwords or tokens. It emits no logs at
    /// all, matching the native <c>ClipboardRedaction</c> policy.
    /// </para>
    /// </summary>
    internal static class MacClipboardJsonReader
    {
        /// <summary>Maximum nesting depth accepted, to bound recursion on hostile input.</summary>
        internal const int MaxDepth = 64;

        /// <summary>
        /// Parses a JSON document.
        /// </summary>
        /// <param name="json">Document text.</param>
        /// <returns>The root value, or <c>null</c> when the document is malformed or exceeds <see cref="MaxDepth"/>.</returns>
        internal static MacJsonValue? Parse(string? json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            int index = 0;
            MacJsonValue? value = ParseValue(json!, ref index, 0);
            if (value == null)
            {
                return null;
            }

            SkipWhitespace(json!, ref index);
            return index == json!.Length ? value : null;
        }

        private static MacJsonValue? ParseValue(string json, ref int index, int depth)
        {
            if (depth > MaxDepth)
            {
                return null;
            }

            SkipWhitespace(json, ref index);
            if (index >= json.Length)
            {
                return null;
            }

            return json[index] switch
            {
                '{' => ParseObject(json, ref index, depth),
                '[' => ParseArray(json, ref index, depth),
                '"' => ParseString(json, ref index),
                't' => ParseLiteral(json, ref index, "true", MacJsonValue.FromBool(true)),
                'f' => ParseLiteral(json, ref index, "false", MacJsonValue.FromBool(false)),
                'n' => ParseLiteral(json, ref index, "null", MacJsonValue.Null),
                _ => ParseNumber(json, ref index)
            };
        }

        private static MacJsonValue? ParseObject(string json, ref int index, int depth)
        {
            index++; // '{'
            var members = new Dictionary<string, MacJsonValue>(StringComparer.Ordinal);

            SkipWhitespace(json, ref index);
            if (index < json.Length && json[index] == '}')
            {
                index++;
                return MacJsonValue.FromObject(members);
            }

            while (true)
            {
                SkipWhitespace(json, ref index);
                if (index >= json.Length || json[index] != '"')
                {
                    return null;
                }

                MacJsonValue? key = ParseString(json, ref index);
                if (key == null || !key.TryGetString(out string keyText))
                {
                    return null;
                }

                SkipWhitespace(json, ref index);
                if (index >= json.Length || json[index] != ':')
                {
                    return null;
                }
                index++;

                MacJsonValue? value = ParseValue(json, ref index, depth + 1);
                if (value == null)
                {
                    return null;
                }
                members[keyText] = value;

                SkipWhitespace(json, ref index);
                if (index >= json.Length)
                {
                    return null;
                }

                if (json[index] == ',')
                {
                    index++;
                    continue;
                }

                if (json[index] == '}')
                {
                    index++;
                    return MacJsonValue.FromObject(members);
                }

                return null;
            }
        }

        private static MacJsonValue? ParseArray(string json, ref int index, int depth)
        {
            index++; // '['
            var elements = new List<MacJsonValue>();

            SkipWhitespace(json, ref index);
            if (index < json.Length && json[index] == ']')
            {
                index++;
                return MacJsonValue.FromArray(elements);
            }

            while (true)
            {
                MacJsonValue? value = ParseValue(json, ref index, depth + 1);
                if (value == null)
                {
                    return null;
                }
                elements.Add(value);

                SkipWhitespace(json, ref index);
                if (index >= json.Length)
                {
                    return null;
                }

                if (json[index] == ',')
                {
                    index++;
                    continue;
                }

                if (json[index] == ']')
                {
                    index++;
                    return MacJsonValue.FromArray(elements);
                }

                return null;
            }
        }

        /// <summary>
        /// Scans a string token, validating it against the JSON grammar.
        /// <para>
        /// Escapes are checked here rather than during materialization: an unknown escape, a short
        /// <c>\u</c>, or a raw control character means the document is malformed, and the envelope
        /// contract requires that to fail rather than be silently repaired into a usable value.
        /// </para>
        /// </summary>
        private static MacJsonValue? ParseString(string json, ref int index)
        {
            index++; // opening quote
            int start = index;
            bool hasEscapes = false;

            while (index < json.Length)
            {
                char c = json[index];
                if (c == '"')
                {
                    var value = MacJsonValue.FromString(json, start, index - start, hasEscapes);
                    index++;
                    return value;
                }

                if (c == '\\')
                {
                    hasEscapes = true;
                    index++;
                    if (index >= json.Length)
                    {
                        return null;
                    }

                    switch (json[index])
                    {
                        case '"':
                        case '\\':
                        case '/':
                        case 'b':
                        case 'f':
                        case 'n':
                        case 'r':
                        case 't':
                            index++;
                            break;

                        case 'u':
                            if (index + 4 >= json.Length)
                            {
                                return null;
                            }
                            for (int offset = 1; offset <= 4; offset++)
                            {
                                if (!IsHexDigit(json[index + offset]))
                                {
                                    return null;
                                }
                            }
                            index += 5;
                            break;

                        default:
                            return null; // not a JSON escape sequence
                    }
                    continue;
                }

                // Control characters must be escaped in JSON.
                if (c < 0x20)
                {
                    return null;
                }

                index++;
            }

            return null; // unterminated
        }

        /// <summary>
        /// Scans a number token against the JSON grammar
        /// <c>-?(0|[1-9][0-9]*)(\.[0-9]+)?([eE][+-]?[0-9]+)?</c>.
        /// <para>
        /// A leading <c>+</c>, a leading zero such as <c>01</c>, and a trailing <c>.</c> or
        /// <c>e</c> are all rejected: accepting them would let a malformed document reach the
        /// parser as a successful value.
        /// </para>
        /// </summary>
        private static MacJsonValue? ParseNumber(string json, ref int index)
        {
            int start = index;

            if (index < json.Length && json[index] == '-')
            {
                index++;
            }

            if (index >= json.Length)
            {
                return null;
            }

            if (json[index] == '0')
            {
                index++;
            }
            else if (json[index] >= '1' && json[index] <= '9')
            {
                while (index < json.Length && IsDigit(json[index]))
                {
                    index++;
                }
            }
            else
            {
                return null;
            }

            if (index < json.Length && json[index] == '.')
            {
                index++;
                if (index >= json.Length || !IsDigit(json[index]))
                {
                    return null;
                }
                while (index < json.Length && IsDigit(json[index]))
                {
                    index++;
                }
            }

            if (index < json.Length && (json[index] == 'e' || json[index] == 'E'))
            {
                index++;
                if (index < json.Length && (json[index] == '+' || json[index] == '-'))
                {
                    index++;
                }
                if (index >= json.Length || !IsDigit(json[index]))
                {
                    return null;
                }
                while (index < json.Length && IsDigit(json[index]))
                {
                    index++;
                }
            }

            return MacJsonValue.FromNumber(json, start, index - start);
        }

        private static bool IsDigit(char c) => c >= '0' && c <= '9';

        private static bool IsHexDigit(char c) =>
            (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');

        private static MacJsonValue? ParseLiteral(string json, ref int index, string literal, MacJsonValue value)
        {
            if (index + literal.Length > json.Length ||
                string.CompareOrdinal(json, index, literal, 0, literal.Length) != 0)
            {
                return null;
            }
            index += literal.Length;
            return value;
        }

        private static void SkipWhitespace(string json, ref int index)
        {
            while (index < json.Length)
            {
                char c = json[index];
                if (c == ' ' || c == '\t' || c == '\n' || c == '\r')
                {
                    index++;
                    continue;
                }
                break;
            }
        }
    }
}
#endif
