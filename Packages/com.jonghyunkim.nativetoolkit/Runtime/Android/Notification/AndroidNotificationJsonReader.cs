#nullable enable

#if UNITY_ANDROID || UNITY_EDITOR
namespace JonghyunKim.NativeToolkit.Runtime.Android.Notification
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text;

    /// <summary>Kind of a parsed JSON value.</summary>
    internal enum AndroidJsonValueKind
    {
        Object,
        Array,
        String,
        Number,
        Bool,
        Null
    }

    /// <summary>
    /// A parsed JSON value. Strings are unescaped and numbers converted to <see cref="double"/>
    /// while the document is read, so a value that cannot be represented fails the whole document
    /// rather than the key that reads it.
    /// </summary>
    internal sealed class AndroidJsonValue
    {
        internal AndroidJsonValueKind Kind { get; }

        private readonly string? _string;
        private readonly double _number;
        private readonly bool _bool;
        private readonly List<KeyValuePair<string, AndroidJsonValue>>? _members;
        private readonly Dictionary<string, int>? _memberIndex;
        private readonly List<AndroidJsonValue>? _elements;

        private AndroidJsonValue(AndroidJsonValueKind kind, string? text = null, double number = 0, bool flag = false)
        {
            Kind = kind;
            _string = text;
            _number = number;
            _bool = flag;
        }

        private AndroidJsonValue(List<KeyValuePair<string, AndroidJsonValue>> members, Dictionary<string, int> index)
        {
            Kind = AndroidJsonValueKind.Object;
            _members = members;
            _memberIndex = index;
        }

        private AndroidJsonValue(List<AndroidJsonValue> elements)
        {
            Kind = AndroidJsonValueKind.Array;
            _elements = elements;
        }

        internal static AndroidJsonValue Null { get; } = new(AndroidJsonValueKind.Null);

        internal static AndroidJsonValue FromBool(bool value) => new(AndroidJsonValueKind.Bool, flag: value);

        internal static AndroidJsonValue FromString(string value) => new(AndroidJsonValueKind.String, text: value);

        internal static AndroidJsonValue FromNumber(double value) => new(AndroidJsonValueKind.Number, number: value);

        internal static AndroidJsonValue FromObject(List<KeyValuePair<string, AndroidJsonValue>> members, Dictionary<string, int> index) =>
            new(members, index);

        internal static AndroidJsonValue FromArray(List<AndroidJsonValue> elements) => new(elements);

        /// <summary>True when this value is JSON <c>null</c>.</summary>
        internal bool IsNull => Kind == AndroidJsonValueKind.Null;

        /// <summary>
        /// Looks up an object member. False only when the key is absent (or this is not an object);
        /// a member whose value is JSON <c>null</c> is present and comes back as <see cref="Null"/>.
        /// The two are kept apart because 1.x told them apart in one place: <c>isGroupConversation</c>
        /// present but null read as false, absent as "not set" (design 4.2).
        /// </summary>
        internal bool TryGetMember(string key, out AndroidJsonValue value)
        {
            if (Kind == AndroidJsonValueKind.Object && _memberIndex!.TryGetValue(key, out int at))
            {
                value = _members![at].Value;
                return true;
            }
            value = Null;
            return false;
        }

        /// <summary>
        /// Object members in document order. A key written twice keeps the place of its first
        /// occurrence and the value of its last. Empty for a non-object.
        /// </summary>
        internal IReadOnlyList<KeyValuePair<string, AndroidJsonValue>> Members =>
            Kind == AndroidJsonValueKind.Object ? _members! : Array.Empty<KeyValuePair<string, AndroidJsonValue>>();

        /// <summary>Array elements. Empty for a non-array.</summary>
        internal IReadOnlyList<AndroidJsonValue> Elements =>
            Kind == AndroidJsonValueKind.Array ? _elements! : Array.Empty<AndroidJsonValue>();

        /// <summary>The string, when this is a string.</summary>
        internal bool TryGetString(out string value)
        {
            value = _string ?? string.Empty;
            return Kind == AndroidJsonValueKind.String;
        }

        /// <summary>The number, when this is a number.</summary>
        internal bool TryGetNumber(out double value)
        {
            value = _number;
            return Kind == AndroidJsonValueKind.Number;
        }

        /// <summary>The boolean, when this is <c>true</c> or <c>false</c>.</summary>
        internal bool TryGetBool(out bool value)
        {
            value = _bool;
            return Kind == AndroidJsonValueKind.Bool;
        }
    }

    /// <summary>
    /// Recursive-descent JSON reader for the payloads <c>AndroidNotificationManager</c> takes
    /// (<c>ShowNotification</c>, <c>ScheduleNotification</c>, <c>CreateChannel</c> and the Progress
    /// calls). A copy of the Windows reader kept for Android (agent-rules: no files shared across OSes).
    /// <para>
    /// It stands in for org.json (Android's libcore), which the 1.x Kotlin bridge parsed them with
    /// (design v4, 4.2): a key written twice keeps the later value, as libcore does. It is stricter
    /// than libcore, which accepts unquoted keys, single quotes, comments and <c>=</c> / <c>=&gt;</c>
    /// separators; those fail here (design 5.3). Nesting is limited to <see cref="MaxContainers"/>
    /// objects and arrays, the outermost included; libcore has no limit (a deep one can overflow its
    /// stack), and 512 is the Windows reader's limit, taken for safety. A number that does not survive
    /// conversion to <see cref="double"/> fails the document.
    /// </para>
    /// <para>
    /// Strict JSON otherwise: no comments, trailing commas, single quotes, NaN or Infinity, and no
    /// unescaped control characters in strings. Never throws; a document it refuses is null.
    /// </para>
    /// <para>
    /// No logs: the payload is the app's own text, and the manager logs only its lengths.
    /// </para>
    /// </summary>
    internal static class AndroidNotificationJsonReader
    {
        /// <summary>The most objects and arrays one document may nest, the outermost included (the Windows reader's limit).</summary>
        internal const int MaxContainers = 512;

        /// <summary>Parses a document. Null when it is null, malformed, too deep, or holds an unrepresentable number.</summary>
        internal static AndroidJsonValue? Parse(string? json)
        {
            if (json == null) return null;

            int index = 0;
            AndroidJsonValue? value = ParseValue(json, ref index, 0);
            if (value == null) return null;

            SkipWhitespace(json, ref index);
            return index == json.Length ? value : null;
        }

        /// <param name="containers">How many objects and arrays enclose the value.</param>
        private static AndroidJsonValue? ParseValue(string json, ref int index, int containers)
        {
            SkipWhitespace(json, ref index);
            if (index >= json.Length) return null;

            switch (json[index])
            {
                case '{':
                    return containers < MaxContainers ? ParseObject(json, ref index, containers + 1) : null;
                case '[':
                    return containers < MaxContainers ? ParseArray(json, ref index, containers + 1) : null;
                case '"':
                    return ParseString(json, ref index, out string? text) ? AndroidJsonValue.FromString(text!) : null;
                case 't':
                    return ParseLiteral(json, ref index, "true", AndroidJsonValue.FromBool(true));
                case 'f':
                    return ParseLiteral(json, ref index, "false", AndroidJsonValue.FromBool(false));
                case 'n':
                    return ParseLiteral(json, ref index, "null", AndroidJsonValue.Null);
                default:
                    return ParseNumber(json, ref index);
            }
        }

        private static AndroidJsonValue? ParseObject(string json, ref int index, int containers)
        {
            index++; // '{'
            var members = new List<KeyValuePair<string, AndroidJsonValue>>();
            var memberIndex = new Dictionary<string, int>(StringComparer.Ordinal);

            SkipWhitespace(json, ref index);
            if (index < json.Length && json[index] == '}')
            {
                index++;
                return AndroidJsonValue.FromObject(members, memberIndex);
            }

            while (true)
            {
                SkipWhitespace(json, ref index);
                if (index >= json.Length || json[index] != '"') return null;
                if (!ParseString(json, ref index, out string? key)) return null;

                SkipWhitespace(json, ref index);
                if (index >= json.Length || json[index] != ':') return null;
                index++;

                AndroidJsonValue? value = ParseValue(json, ref index, containers);
                if (value == null) return null;

                if (memberIndex.TryGetValue(key!, out int at))
                {
                    members[at] = new KeyValuePair<string, AndroidJsonValue>(key!, value);
                }
                else
                {
                    memberIndex[key!] = members.Count;
                    members.Add(new KeyValuePair<string, AndroidJsonValue>(key!, value));
                }

                SkipWhitespace(json, ref index);
                if (index >= json.Length) return null;
                if (json[index] == ',')
                {
                    index++;
                    continue;
                }
                if (json[index] == '}')
                {
                    index++;
                    return AndroidJsonValue.FromObject(members, memberIndex);
                }
                return null;
            }
        }

        private static AndroidJsonValue? ParseArray(string json, ref int index, int containers)
        {
            index++; // '['
            var elements = new List<AndroidJsonValue>();

            SkipWhitespace(json, ref index);
            if (index < json.Length && json[index] == ']')
            {
                index++;
                return AndroidJsonValue.FromArray(elements);
            }

            while (true)
            {
                AndroidJsonValue? value = ParseValue(json, ref index, containers);
                if (value == null) return null;
                elements.Add(value);

                SkipWhitespace(json, ref index);
                if (index >= json.Length) return null;
                if (json[index] == ',')
                {
                    index++;
                    continue;
                }
                if (json[index] == ']')
                {
                    index++;
                    return AndroidJsonValue.FromArray(elements);
                }
                return null;
            }
        }

        /// <summary>
        /// Reads a string token. An unknown escape, a short <c>\u</c>, an unescaped control
        /// character or a missing closing quote fails it. <c>\u</c> escapes are copied unit by
        /// unit, so a surrogate pair comes back whole and an unpaired one stays unpaired.
        /// </summary>
        private static bool ParseString(string json, ref int index, out string? value)
        {
            value = null;
            index++; // opening quote
            var builder = new StringBuilder();

            while (index < json.Length)
            {
                char c = json[index];
                if (c == '"')
                {
                    index++;
                    value = builder.ToString();
                    return true;
                }

                if (c < 0x20) return false;

                if (c != '\\')
                {
                    builder.Append(c);
                    index++;
                    continue;
                }

                index++;
                if (index >= json.Length) return false;
                char escape = json[index++];
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
                        if (index + 4 > json.Length) return false;
                        int unit = 0;
                        for (int offset = 0; offset < 4; offset++)
                        {
                            int digit = HexDigit(json[index + offset]);
                            if (digit < 0) return false;
                            unit = (unit << 4) | digit;
                        }
                        builder.Append((char)unit);
                        index += 4;
                        break;
                    default:
                        return false;
                }
            }

            return false; // unterminated
        }

        /// <summary>
        /// Reads a number token (<c>-?(0|[1-9][0-9]*)(\.[0-9]+)?([eE][+-]?[0-9]+)?</c>) and converts
        /// it to <see cref="double"/>. Fails when the result is infinite, or is 0 although the
        /// digits before the exponent are not all 0 (<c>1e-400</c>, or <c>0.</c> followed by 323
        /// zeros and a 1). <c>-0</c> and <c>0e5</c> are 0.
        /// </summary>
        private static AndroidJsonValue? ParseNumber(string json, ref int index)
        {
            int start = index;
            bool nonZeroDigit = false;

            if (index < json.Length && json[index] == '-') index++;
            if (index >= json.Length) return null;

            if (json[index] == '0')
            {
                index++;
            }
            else if (json[index] >= '1' && json[index] <= '9')
            {
                nonZeroDigit = true;
                while (index < json.Length && IsDigit(json[index])) index++;
            }
            else
            {
                return null;
            }

            if (index < json.Length && json[index] == '.')
            {
                index++;
                if (index >= json.Length || !IsDigit(json[index])) return null;
                while (index < json.Length && IsDigit(json[index]))
                {
                    if (json[index] != '0') nonZeroDigit = true;
                    index++;
                }
            }

            if (index < json.Length && (json[index] == 'e' || json[index] == 'E'))
            {
                index++;
                if (index < json.Length && (json[index] == '+' || json[index] == '-')) index++;
                if (index >= json.Length || !IsDigit(json[index])) return null;
                while (index < json.Length && IsDigit(json[index])) index++;
            }

            // Mono's parser reports an overflow as a failure where newer runtimes return infinity;
            // both are refused.
            if (!double.TryParse(json.Substring(start, index - start), NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                return null;
            if (double.IsInfinity(value) || double.IsNaN(value)) return null;
            if (value == 0 && nonZeroDigit) return null;

            return AndroidJsonValue.FromNumber(value);
        }

        private static bool IsDigit(char c) => c >= '0' && c <= '9';

        private static int HexDigit(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'a' && c <= 'f') return c - 'a' + 10;
            if (c >= 'A' && c <= 'F') return c - 'A' + 10;
            return -1;
        }

        private static AndroidJsonValue? ParseLiteral(string json, ref int index, string literal, AndroidJsonValue value)
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
                if (c != ' ' && c != '\t' && c != '\n' && c != '\r') break;
                index++;
            }
        }
    }
}
#endif
