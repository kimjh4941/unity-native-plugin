#nullable enable

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
namespace JonghyunKim.NativeToolkit.Runtime.Windows.Notification
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text;

    /// <summary>Kind of a parsed JSON value.</summary>
    internal enum WindowsJsonValueKind
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
    internal sealed class WindowsJsonValue
    {
        internal WindowsJsonValueKind Kind { get; }

        private readonly string? _string;
        private readonly double _number;
        private readonly bool _bool;
        private readonly List<KeyValuePair<string, WindowsJsonValue>>? _members;
        private readonly Dictionary<string, int>? _memberIndex;
        private readonly List<WindowsJsonValue>? _elements;

        private WindowsJsonValue(WindowsJsonValueKind kind, string? text = null, double number = 0, bool flag = false)
        {
            Kind = kind;
            _string = text;
            _number = number;
            _bool = flag;
        }

        private WindowsJsonValue(List<KeyValuePair<string, WindowsJsonValue>> members, Dictionary<string, int> index)
        {
            Kind = WindowsJsonValueKind.Object;
            _members = members;
            _memberIndex = index;
        }

        private WindowsJsonValue(List<WindowsJsonValue> elements)
        {
            Kind = WindowsJsonValueKind.Array;
            _elements = elements;
        }

        internal static WindowsJsonValue Null { get; } = new(WindowsJsonValueKind.Null);

        internal static WindowsJsonValue FromBool(bool value) => new(WindowsJsonValueKind.Bool, flag: value);

        internal static WindowsJsonValue FromString(string value) => new(WindowsJsonValueKind.String, text: value);

        internal static WindowsJsonValue FromNumber(double value) => new(WindowsJsonValueKind.Number, number: value);

        internal static WindowsJsonValue FromObject(List<KeyValuePair<string, WindowsJsonValue>> members, Dictionary<string, int> index) =>
            new(members, index);

        internal static WindowsJsonValue FromArray(List<WindowsJsonValue> elements) => new(elements);

        /// <summary>True when this value is JSON <c>null</c>.</summary>
        internal bool IsNull => Kind == WindowsJsonValueKind.Null;

        /// <summary>
        /// Looks up an object member. False only when the key is absent (or this is not an object);
        /// a member whose value is JSON <c>null</c> is present and comes back as <see cref="Null"/>.
        /// The two are kept apart because 1.x read a present <c>null</c> as a type error.
        /// </summary>
        internal bool TryGetMember(string key, out WindowsJsonValue value)
        {
            if (Kind == WindowsJsonValueKind.Object && _memberIndex!.TryGetValue(key, out int at))
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
        internal IReadOnlyList<KeyValuePair<string, WindowsJsonValue>> Members =>
            Kind == WindowsJsonValueKind.Object ? _members! : Array.Empty<KeyValuePair<string, WindowsJsonValue>>();

        /// <summary>Array elements. Empty for a non-array.</summary>
        internal IReadOnlyList<WindowsJsonValue> Elements =>
            Kind == WindowsJsonValueKind.Array ? _elements! : Array.Empty<WindowsJsonValue>();

        /// <summary>The string, when this is a string.</summary>
        internal bool TryGetString(out string value)
        {
            value = _string ?? string.Empty;
            return Kind == WindowsJsonValueKind.String;
        }

        /// <summary>The number, when this is a number.</summary>
        internal bool TryGetNumber(out double value)
        {
            value = _number;
            return Kind == WindowsJsonValueKind.Number;
        }

        /// <summary>The boolean, when this is <c>true</c> or <c>false</c>.</summary>
        internal bool TryGetBool(out bool value)
        {
            value = _bool;
            return Kind == WindowsJsonValueKind.Bool;
        }
    }

    /// <summary>
    /// Recursive-descent JSON reader for the notification payloads that
    /// <c>WindowsNotificationManager.ShowNotification</c> and <c>ScheduleNotification</c> take.
    /// <para>
    /// It stands in for <c>Windows.Data.Json</c>, which the 1.x native library parsed them with,
    /// and follows it where the design measured it (design v8, 4.2): a key written twice keeps
    /// the later value; the document may nest up to <see cref="MaxContainers"/> objects and arrays,
    /// counting the outermost. It is stricter in one place: a number that does not survive
    /// conversion to <see cref="double"/> (<c>1e400</c>, or a non-zero literal that rounds to 0)
    /// fails the document even under a key nobody reads, where 1.x failed some of them with a
    /// different code and let others through as 0.
    /// </para>
    /// <para>
    /// Strict JSON otherwise: no comments, trailing commas, single quotes, NaN or Infinity, and no
    /// unescaped control characters in strings. Never throws; a document it refuses is null.
    /// </para>
    /// <para>
    /// No logs: the payload is the app's own text and may be long; the manager logs it once.
    /// </para>
    /// </summary>
    internal static class WindowsNotificationJsonReader
    {
        /// <summary>The most objects and arrays one document may nest, the outermost included (1.x's limit).</summary>
        internal const int MaxContainers = 512;

        /// <summary>Parses a document. Null when it is null, malformed, too deep, or holds an unrepresentable number.</summary>
        internal static WindowsJsonValue? Parse(string? json)
        {
            if (json == null) return null;

            int index = 0;
            WindowsJsonValue? value = ParseValue(json, ref index, 0);
            if (value == null) return null;

            SkipWhitespace(json, ref index);
            return index == json.Length ? value : null;
        }

        /// <param name="containers">How many objects and arrays enclose the value.</param>
        private static WindowsJsonValue? ParseValue(string json, ref int index, int containers)
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
                    return ParseString(json, ref index, out string? text) ? WindowsJsonValue.FromString(text!) : null;
                case 't':
                    return ParseLiteral(json, ref index, "true", WindowsJsonValue.FromBool(true));
                case 'f':
                    return ParseLiteral(json, ref index, "false", WindowsJsonValue.FromBool(false));
                case 'n':
                    return ParseLiteral(json, ref index, "null", WindowsJsonValue.Null);
                default:
                    return ParseNumber(json, ref index);
            }
        }

        private static WindowsJsonValue? ParseObject(string json, ref int index, int containers)
        {
            index++; // '{'
            var members = new List<KeyValuePair<string, WindowsJsonValue>>();
            var memberIndex = new Dictionary<string, int>(StringComparer.Ordinal);

            SkipWhitespace(json, ref index);
            if (index < json.Length && json[index] == '}')
            {
                index++;
                return WindowsJsonValue.FromObject(members, memberIndex);
            }

            while (true)
            {
                SkipWhitespace(json, ref index);
                if (index >= json.Length || json[index] != '"') return null;
                if (!ParseString(json, ref index, out string? key)) return null;

                SkipWhitespace(json, ref index);
                if (index >= json.Length || json[index] != ':') return null;
                index++;

                WindowsJsonValue? value = ParseValue(json, ref index, containers);
                if (value == null) return null;

                if (memberIndex.TryGetValue(key!, out int at))
                {
                    members[at] = new KeyValuePair<string, WindowsJsonValue>(key!, value);
                }
                else
                {
                    memberIndex[key!] = members.Count;
                    members.Add(new KeyValuePair<string, WindowsJsonValue>(key!, value));
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
                    return WindowsJsonValue.FromObject(members, memberIndex);
                }
                return null;
            }
        }

        private static WindowsJsonValue? ParseArray(string json, ref int index, int containers)
        {
            index++; // '['
            var elements = new List<WindowsJsonValue>();

            SkipWhitespace(json, ref index);
            if (index < json.Length && json[index] == ']')
            {
                index++;
                return WindowsJsonValue.FromArray(elements);
            }

            while (true)
            {
                WindowsJsonValue? value = ParseValue(json, ref index, containers);
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
                    return WindowsJsonValue.FromArray(elements);
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
        private static WindowsJsonValue? ParseNumber(string json, ref int index)
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

            return WindowsJsonValue.FromNumber(value);
        }

        private static bool IsDigit(char c) => c >= '0' && c <= '9';

        private static int HexDigit(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'a' && c <= 'f') return c - 'a' + 10;
            if (c >= 'A' && c <= 'F') return c - 'A' + 10;
            return -1;
        }

        private static WindowsJsonValue? ParseLiteral(string json, ref int index, string literal, WindowsJsonValue value)
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
