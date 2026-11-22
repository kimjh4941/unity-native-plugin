#nullable enable

#if UNITY_IOS || UNITY_EDITOR
namespace JonghyunKim.NativeToolkit.Runtime.iOS.Notification
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text;
    using JonghyunKim.NativeToolkit.Runtime.iOS.Common;

    /// <summary>
    /// Reads the public API's JSON (content, trigger, category) as native-toolkit 1.13.0's Unity bridge read it, and
    /// adjusts what 1.x accepted but the C ABI rejects (artifact/ios/notification, C ABI design v4, D-4, D-5, D-16).
    /// Pure: no native call, so EditMode checks every rule.
    /// </summary>
    internal static class IosNotificationRequests
    {
        /// <summary>The content of a notification, ready for the C ABI's builder.</summary>
        internal sealed class Content
        {
            internal string Id = "";
            internal string Title = "";
            internal string? Subtitle;
            internal string? Body;
            internal int? Badge;                 // null: not set
            internal int SoundKind = IosNotificationCApi.SoundDefault;
            internal string? SoundName;
            internal string? Category;
            internal int? InterruptionLevel;     // null: not set
            internal string? Thread;
            internal string? TargetContent;
            internal double? RelevanceScore;
            internal string? FilterCriteria;
            internal string? UserInfoJson;       // null: not set
            internal readonly List<(string Identifier, string FileUrl)> Attachments = new();
        }

        /// <summary>A trigger: the struct's values, and the location identifier kept as a string.</summary>
        internal sealed class Trigger
        {
            internal int Kind;
            internal bool Repeats;
            internal double IntervalSeconds;
            internal uint CalendarFields;
            internal int Year, Month, Day, Hour, Minute, Second;
            internal string? LocationIdentifier;
            internal double Latitude, Longitude, Radius;
            internal bool NotifyOnEntry = true;
            internal bool NotifyOnExit;
        }

        internal sealed class ActionItem
        {
            internal string Identifier = "";
            internal string Title = "";
            internal string? SfSymbolName;
            internal uint Options;
        }

        internal sealed class TextInputItem
        {
            internal string Identifier = "";
            internal string Title = "";
            internal string ButtonTitle = "";
            internal string Placeholder = "";
        }

        internal sealed class Category
        {
            internal string Identifier = "";
            internal uint Options;
            internal readonly List<ActionItem> Actions = new();
            internal readonly List<TextInputItem> TextInputActions = new();
        }

        /// <summary>What reading found wrong: nothing, a JSON 1.x could not read, or a value C# rejects (U+0000).</summary>
        internal enum Problem
        {
            None,
            Unreadable,
            Invalid,
        }

        // ── Content (D-4, D-5 (a), D-16) ──────────────────────────────────

        /// <summary>
        /// Reads content JSON. <paramref name="fallbackId"/> replaces an empty <c>id</c> (Schedule and Update pass
        /// their identifier; the C ABI does not use the content's id there). <paramref name="valueWithNul"/> tells
        /// that a displayed string holds U+0000 (passed on, cut; the caller warns without the value).
        /// </summary>
        internal static Problem ReadContent(string? json, string? fallbackId, out Content content, out bool valueWithNul)
        {
            content = new Content();
            valueWithNul = false;
            JsonValue? root = ParseLike1x(json);
            if (root == null || root.Kind != JsonValueKind.Object) return Problem.Unreadable;
            if (!TryString(root, "id", out string id) || !TryString(root, "title", out string title)) return Problem.Unreadable;

            // Schedule and Update (a fallback is given): the C ABI does not use the content's id, so it is not
            // checked for U+0000; it is cut there, and an empty one is the identifier argument, cut as the C string
            // it is passed as (design v4, D-5 (a), D-16). Show uses the id and rejects U+0000 below.
            bool idUnused = fallbackId != null;
            content.Id = idUnused ? CutAtNul(id) : id;
            if (idUnused && content.Id.Length == 0) content.Id = CutAtNul(fallbackId!);
            content.Title = title;
            content.Subtitle = StringOrNull(root, "subtitle");
            content.Body = StringOrNull(root, "body");
            content.Category = StringOrNull(root, "categoryIdentifier");
            content.Thread = StringOrNull(root, "threadIdentifier");
            content.TargetContent = StringOrNull(root, "targetContentIdentifier");
            content.FilterCriteria = StringOrNull(root, "filterCriteria");

            // A negative badge was passed on by 1.x; the C ABI's "not set" (-1) is the closest (D-5 (a)).
            if (TryIntLike(root, "badge", out int badge)) content.Badge = badge < 0 ? (int?)null : badge;

            if (root.TryGetMember("sound", out JsonValue sound) && sound.TryGetString(out string soundName))
            {
                switch (soundName)
                {
                    case "default":
                    case "": // 1.x passed an empty name on; the C ABI needs a name, so the default sound (D-5 (a))
                        content.SoundKind = IosNotificationCApi.SoundDefault;
                        break;
                    case "defaultCritical":
                        content.SoundKind = IosNotificationCApi.SoundDefaultCritical;
                        break;
                    default:
                        content.SoundKind = IosNotificationCApi.SoundNamed;
                        content.SoundName = soundName;
                        break;
                }
            }

            if (root.TryGetMember("interruptionLevel", out JsonValue level) && level.TryGetString(out string levelName))
            {
                content.InterruptionLevel = levelName switch
                {
                    "passive" => IosNotificationCApi.InterruptionPassive,
                    "timeSensitive" => IosNotificationCApi.InterruptionTimeSensitive,
                    "critical" => IosNotificationCApi.InterruptionCritical,
                    _ => IosNotificationCApi.InterruptionActive, // 1.x: any other string is active
                };
            }

            if (TryDoubleLike(root, "relevanceScore", out double score))
                content.RelevanceScore = double.IsNaN(score) ? 0 : Math.Max(0, Math.Min(1, score));

            if (root.TryGetMember("userInfo", out JsonValue userInfo))
                content.UserInfoJson = userInfo.Kind == JsonValueKind.Object ? WriteWithoutNulls(userInfo) : "{}";

            if (TryObjectArray(root, "attachments", out IReadOnlyList<JsonValue> attachments))
            {
                foreach (JsonValue attachment in attachments)
                {
                    // 1.x dropped an element without both strings, or whose URL did not read (an empty one).
                    if (!TryString(attachment, "identifier", out string attachmentId) ||
                        !TryString(attachment, "fileURL", out string fileUrl) || fileUrl.Length == 0)
                    {
                        continue;
                    }
                    content.Attachments.Add((attachmentId, fileUrl));
                }
            }

            // Cut at U+0000, these would name another notification, file (moved when added) or sound (D-16).
            if ((!idUnused && HasNul(content.Id)) || HasNul(content.Category) || HasNul(content.Thread) || HasNul(content.TargetContent) ||
                HasNul(content.SoundName))
            {
                return Problem.Invalid;
            }
            foreach (var attachment in content.Attachments)
            {
                if (HasNul(attachment.Identifier) || HasNul(attachment.FileUrl)) return Problem.Invalid;
            }
            valueWithNul |= HasNul(content.Title) || HasNul(content.Subtitle) || HasNul(content.Body) ||
                            HasNul(content.FilterCriteria);
            return Problem.None;
        }

        // ── Trigger (D-4) ─────────────────────────────────────────────────

        /// <summary>
        /// Reads trigger JSON. Null (with <see cref="Problem.Unreadable"/>) when 1.x could not read it: Show and
        /// Update then deliver at once, Schedule fails.
        /// </summary>
        internal static Problem ReadTrigger(string? json, out Trigger? trigger)
        {
            trigger = null;
            JsonValue? root = ParseLike1x(json);
            if (root == null || root.Kind != JsonValueKind.Object || !TryString(root, "type", out string type))
                return Problem.Unreadable;

            var t = new Trigger();
            switch (type)
            {
                case "timeInterval":
                    if (!TryDoubleLike(root, "interval", out t.IntervalSeconds)) return Problem.Unreadable;
                    t.Kind = IosNotificationCApi.TriggerTimeInterval;
                    t.Repeats = BoolLike(root, "repeats", false);
                    break;
                case "calendar":
                    t.Kind = IosNotificationCApi.TriggerCalendar;
                    t.Repeats = BoolLike(root, "repeats", false);
                    if (TryIntLike(root, "year", out t.Year)) t.CalendarFields |= IosNotificationCApi.CalendarYear;
                    if (TryIntLike(root, "month", out t.Month)) t.CalendarFields |= IosNotificationCApi.CalendarMonth;
                    if (TryIntLike(root, "day", out t.Day)) t.CalendarFields |= IosNotificationCApi.CalendarDay;
                    if (TryIntLike(root, "hour", out t.Hour)) t.CalendarFields |= IosNotificationCApi.CalendarHour;
                    if (TryIntLike(root, "minute", out t.Minute)) t.CalendarFields |= IosNotificationCApi.CalendarMinute;
                    if (TryIntLike(root, "second", out t.Second)) t.CalendarFields |= IosNotificationCApi.CalendarSecond;
                    break;
                case "location":
                    if (!TryString(root, "identifier", out string locationId) ||
                        !TryDoubleLike(root, "latitude", out t.Latitude) ||
                        !TryDoubleLike(root, "longitude", out t.Longitude) ||
                        !TryDoubleLike(root, "radius", out t.Radius))
                    {
                        return Problem.Unreadable;
                    }
                    t.Kind = IosNotificationCApi.TriggerLocation;
                    t.LocationIdentifier = locationId;
                    t.NotifyOnEntry = BoolLike(root, "notifyOnEntry", true);
                    t.NotifyOnExit = BoolLike(root, "notifyOnExit", false);
                    t.Repeats = false; // 1.x: a location trigger never repeated
                    break;
                default:
                    return Problem.Unreadable;
            }
            trigger = t;
            return HasNul(t.LocationIdentifier) ? Problem.Invalid : Problem.None;
        }

        // ── Category (D-4, D-16) ──────────────────────────────────────────

        internal static Problem ReadCategory(string? json, out Category category, out bool valueWithNul)
        {
            category = new Category();
            valueWithNul = false;
            JsonValue? root = ParseLike1x(json);
            if (root == null || root.Kind != JsonValueKind.Object || !TryString(root, "identifier", out string identifier))
                return Problem.Unreadable;
            category.Identifier = identifier;

            if (TryObjectArray(root, "actions", out IReadOnlyList<JsonValue> actions))
            {
                foreach (JsonValue element in actions)
                {
                    if (!TryString(element, "identifier", out string id) || id.Length == 0 ||
                        !TryString(element, "title", out string title))
                    {
                        continue;
                    }
                    var action = new ActionItem { Identifier = id, Title = title, SfSymbolName = StringOrNull(element, "sfSymbolName") };
                    foreach (string option in StringArray(element, "options"))
                    {
                        action.Options |= option switch
                        {
                            "authenticationRequired" => IosNotificationCApi.ActionAuthenticationRequired,
                            "destructive" => IosNotificationCApi.ActionDestructive,
                            "foreground" => IosNotificationCApi.ActionForeground,
                            _ => 0u,
                        };
                    }
                    category.Actions.Add(action);
                }
            }

            if (TryObjectArray(root, "textInputActions", out IReadOnlyList<JsonValue> textInputs))
            {
                foreach (JsonValue element in textInputs)
                {
                    if (!TryString(element, "identifier", out string id) || id.Length == 0 ||
                        !TryString(element, "title", out string title) ||
                        !TryString(element, "buttonTitle", out string buttonTitle) ||
                        !TryString(element, "textInputPlaceholder", out string placeholder))
                    {
                        continue;
                    }
                    category.TextInputActions.Add(new TextInputItem
                    {
                        Identifier = id, Title = title, ButtonTitle = buttonTitle, Placeholder = placeholder,
                    });
                }
            }

            foreach (string option in StringArray(root, "options"))
            {
                category.Options |= option switch
                {
                    "customDismissAction" => IosNotificationCApi.CategoryCustomDismissAction,
                    "allowInCarPlay" => IosNotificationCApi.CategoryAllowInCarPlay,
                    "hiddenPreviewsShowTitle" => IosNotificationCApi.CategoryHiddenPreviewsShowTitle,
                    "allowAnnouncement" => IosNotificationCApi.CategoryAllowAnnouncement,
                    _ => 0u,
                };
            }

            if (HasNul(category.Identifier)) return Problem.Invalid;
            foreach (ActionItem action in category.Actions)
            {
                if (HasNul(action.Identifier)) return Problem.Invalid;
                valueWithNul |= HasNul(action.Title) || HasNul(action.SfSymbolName);
            }
            foreach (TextInputItem action in category.TextInputActions)
            {
                if (HasNul(action.Identifier)) return Problem.Invalid;
                valueWithNul |= HasNul(action.Title) || HasNul(action.ButtonTitle) || HasNul(action.Placeholder);
            }
            return Problem.None;
        }

        // ── Readers that follow 1.x's Swift casts (F-17) ──────────────────

        internal static bool HasNul(string? value) => value != null && value.IndexOf('\0') >= 0;

        /// <summary>The nesting 1.x's JSONSerialization read (much deeper than the Clipboard reader's default).</summary>
        internal const int MaxDepth = 512;

        /// <summary>Reads JSON as 1.x did: a leading byte order mark is skipped, and deep documents are read.</summary>
        private static JsonValue? ParseLike1x(string? json)
        {
            if (json != null && json.Length > 0 && json[0] == '\uFEFF') json = json.Substring(1);
            return IosJsonReader.Parse(json, MaxDepth);
        }

        private static string CutAtNul(string value)
        {
            int nul = value.IndexOf('\0');
            return nul < 0 ? value : value.Substring(0, nul);
        }

        private static bool TryString(JsonValue obj, string key, out string value)
        {
            if (obj.TryGetMember(key, out JsonValue member) && member.TryGetString(out value)) return true;
            value = "";
            return false;
        }

        private static string? StringOrNull(JsonValue obj, string key) => TryString(obj, key, out string value) ? value : null;

        /// <summary>
        /// An integer as Swift's <c>as? Int</c> read it through NSNumber: a bool (true is 1) or a number whose value
        /// is exactly an integer (<c>2.0</c>, <c>1e2</c>); a fraction is not one. Outside int32 it is not read (the C
        /// ABI takes int32; D-4).
        /// </summary>
        private static bool TryIntLike(JsonValue obj, string key, out int value)
        {
            value = 0;
            if (!obj.TryGetMember(key, out JsonValue member)) return false;
            if (member.TryGetBool(out bool flag))
            {
                value = flag ? 1 : 0;
                return true;
            }
            if (!member.TryGetDouble(out double number) || number != Math.Floor(number) ||
                number < int.MinValue || number > int.MaxValue)
            {
                return false;
            }
            value = (int)number;
            return true;
        }

        /// <summary>A double as Swift's <c>as? Double</c> read it: a number or a bool.</summary>
        private static bool TryDoubleLike(JsonValue obj, string key, out double value)
        {
            value = 0;
            if (!obj.TryGetMember(key, out JsonValue member)) return false;
            if (member.TryGetBool(out bool flag))
            {
                value = flag ? 1 : 0;
                return true;
            }
            // 1.x's as? Double gave nil for a number JSONSerialization could not read (1e400) and for an integer a
            // double cannot hold exactly (above 2^53); such a value is not read (design v4, D-4).
            if (!member.TryGetDouble(out value) || double.IsNaN(value) || double.IsInfinity(value)) return false;
            string? raw = member.RawNumber;
            if (raw != null && raw.IndexOfAny(new[] { '.', 'e', 'E' }) < 0 && Math.Abs(value) >= 9007199254740992d &&
                (!System.Numerics.BigInteger.TryParse(raw, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var exact) ||
                 exact != new System.Numerics.BigInteger(value)))
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// A bool as Swift's <c>as? Bool</c> read it through NSNumber: a bool, or a number whose value is 0 or 1
        /// (<c>1.0</c> too). Any other value is not a bool, so the default applies (measured on macOS; D-4).
        /// </summary>
        private static bool BoolLike(JsonValue obj, string key, bool fallback)
        {
            if (!obj.TryGetMember(key, out JsonValue member)) return fallback;
            if (member.TryGetBool(out bool flag)) return flag;
            if (member.TryGetDouble(out double number) && (number == 0 || number == 1)) return number == 1;
            return fallback;
        }

        /// <summary>An array whose elements are all objects (1.x's <c>as? [[String: Any]]</c> was all or nothing).</summary>
        private static bool TryObjectArray(JsonValue obj, string key, out IReadOnlyList<JsonValue> elements)
        {
            elements = Array.Empty<JsonValue>();
            if (!obj.TryGetMember(key, out JsonValue member) || member.Kind != JsonValueKind.Array) return false;
            foreach (JsonValue element in member.AsArray())
            {
                if (element.Kind != JsonValueKind.Object) return false;
            }
            elements = member.AsArray();
            return true;
        }

        /// <summary>The strings of an array whose elements are all strings; none otherwise (1.x's <c>as? [String]</c>).</summary>
        private static IReadOnlyList<string> StringArray(JsonValue obj, string key)
        {
            if (!obj.TryGetMember(key, out JsonValue member) || member.Kind != JsonValueKind.Array) return Array.Empty<string>();
            var strings = new List<string>();
            foreach (JsonValue element in member.AsArray())
            {
                if (!element.TryGetString(out string value)) return Array.Empty<string>();
                strings.Add(value);
            }
            return strings;
        }

        // ── userInfo (D-5 (a)): written back without null, keys escaped, numbers as written ──

        internal static string WriteWithoutNulls(JsonValue value)
        {
            var builder = new StringBuilder();
            Write(builder, value);
            return builder.ToString();
        }

        private static void Write(StringBuilder builder, JsonValue value)
        {
            switch (value.Kind)
            {
                case JsonValueKind.Object:
                    builder.Append('{');
                    bool first = true;
                    foreach (var member in value.Members)
                    {
                        if (member.Value.Kind == JsonValueKind.Null) continue;
                        if (!first) builder.Append(',');
                        first = false;
                        AppendString(builder, member.Key);
                        builder.Append(':');
                        Write(builder, member.Value);
                    }
                    builder.Append('}');
                    break;
                case JsonValueKind.Array:
                    builder.Append('[');
                    bool firstElement = true;
                    foreach (JsonValue element in value.AsArray())
                    {
                        if (element.Kind == JsonValueKind.Null) continue;
                        if (!firstElement) builder.Append(',');
                        firstElement = false;
                        Write(builder, element);
                    }
                    builder.Append(']');
                    break;
                case JsonValueKind.String:
                    value.TryGetString(out string text);
                    AppendString(builder, text);
                    break;
                case JsonValueKind.Number:
                    builder.Append(value.RawNumber);
                    break;
                case JsonValueKind.Bool:
                    value.TryGetBool(out bool flag);
                    builder.Append(flag ? "true" : "false");
                    break;
                default:
                    builder.Append("null");
                    break;
            }
        }

        /// <summary>
        /// A JSON string: <c>\"</c>, <c>\\</c>, <c>\b \f \n \r \t</c>, other U+0000–U+001F as <c>\u00xx</c>
        /// (lower case); <c>/</c> and U+2028 as they are (design D-9).
        /// </summary>
        internal static void AppendString(StringBuilder builder, string value)
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
                        if (c < 0x20) builder.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else builder.Append(c);
                        break;
                }
            }
            builder.Append('"');
        }
    }
}
#endif
