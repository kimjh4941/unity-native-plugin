#nullable enable

#if UNITY_STANDALONE_OSX || UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace JonghyunKim.NativeToolkit.Runtime.Notification
{
    /// <summary>
    /// Builds JSON strings for macOS notification APIs.
    /// All methods produce output matching the schemas expected by UnityMacNotificationJsonParser.
    /// Note: macOS category schema uses "id" keys (not "identifier" as iOS does).
    /// Note: macOS time interval trigger schema uses "seconds" key (not "interval" as iOS does).
    /// </summary>
    public static class MacNotificationJsonBuilder
    {
        /// <summary>
        /// Builds JSON for notification content.
        /// </summary>
        public static string BuildContentJson(NotificationContentPayload content)
        {
            var result = new Dictionary<string, object?>
            {
                ["id"] = content.id,
                ["title"] = content.title
            };

            AddIfNotNullOrWhiteSpace(result, "subtitle", content.subtitle);
            AddIfNotNullOrWhiteSpace(result, "body", content.body);
            AddIfHasValue(result, "badge", content.badge);
            AddIfNotNullOrWhiteSpace(result, "categoryIdentifier", content.categoryIdentifier);

            return SerializeObject(result);
        }

        /// <summary>
        /// Builds JSON for a time interval trigger.
        /// macOS parser expects "seconds" key (iOS uses "interval").
        /// </summary>
        public static string BuildTimeIntervalTriggerJson(TimeIntervalTriggerPayload trigger)
        {
            var result = new Dictionary<string, object?>
            {
                ["type"] = "timeInterval",
                ["seconds"] = trigger.interval,
                ["repeats"] = trigger.repeats
            };

            return SerializeObject(result);
        }

        /// <summary>
        /// Builds JSON for a calendar trigger.
        /// </summary>
        public static string BuildCalendarTriggerJson(CalendarTriggerPayload trigger)
        {
            var result = new Dictionary<string, object?>
            {
                ["type"] = "calendar",
                ["repeats"] = trigger.repeats
            };

            AddIfHasValue(result, "year", trigger.year);
            AddIfHasValue(result, "month", trigger.month);
            AddIfHasValue(result, "day", trigger.day);
            AddIfHasValue(result, "hour", trigger.hour);
            AddIfHasValue(result, "minute", trigger.minute);
            AddIfHasValue(result, "second", trigger.second);

            return SerializeObject(result);
        }

        /// <summary>
        /// Builds JSON for a macOS notification category.
        /// Uses "id" key for category and actions (iOS uses "identifier").
        /// </summary>
        public static string BuildCategoryJson(MacNotificationCategoryPayload category)
        {
            var result = new Dictionary<string, object?>
            {
                ["id"] = category.id
            };

            var actionsList = BuildActionObjects(category.actions);
            if (actionsList.Count > 0)
                result["actions"] = actionsList;

            return SerializeObject(result);
        }

        private static List<object?> BuildActionObjects(MacNotificationActionPayload[] actions)
        {
            var result = new List<object?>();
            foreach (var action in actions)
            {
                if (string.IsNullOrWhiteSpace(action.id) || string.IsNullOrWhiteSpace(action.title))
                    continue;

                var obj = new Dictionary<string, object?>
                {
                    ["id"] = action.id,
                    ["title"] = action.title,
                    ["isForeground"] = action.isForeground,
                    ["isTextInput"] = action.isTextInput
                };

                if (!string.IsNullOrEmpty(action.textInputPlaceholder))
                    obj["textInputPlaceholder"] = action.textInputPlaceholder;

                result.Add(obj);
            }

            return result;
        }

        private static void AddIfNotNullOrWhiteSpace(Dictionary<string, object?> target, string key, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                target[key] = value;
        }

        private static void AddIfHasValue<T>(Dictionary<string, object?> target, string key, T? value) where T : struct
        {
            if (value.HasValue)
                target[key] = value.Value;
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
                case string stringValue:
                    AppendEscapedString(builder, stringValue);
                    break;
                case bool boolValue:
                    builder.Append(boolValue ? "true" : "false");
                    break;
                case int intValue:
                    builder.Append(intValue.ToString(CultureInfo.InvariantCulture));
                    break;
                case double doubleValue:
                    builder.Append(doubleValue.ToString(CultureInfo.InvariantCulture));
                    break;
                case Dictionary<string, object?> dictionaryValue:
                    AppendObject(builder, dictionaryValue);
                    break;
                case List<object?> listValue:
                    AppendArray(builder, listValue);
                    break;
                default:
                    AppendEscapedString(builder, value.ToString() ?? string.Empty);
                    break;
            }
        }

        private static void AppendObject(StringBuilder builder, Dictionary<string, object?> obj)
        {
            builder.Append('{');
            bool first = true;
            foreach (var pair in obj)
            {
                if (!first) builder.Append(',');
                first = false;
                AppendEscapedString(builder, pair.Key);
                builder.Append(':');
                AppendValue(builder, pair.Value);
            }
            builder.Append('}');
        }

        private static void AppendArray(StringBuilder builder, List<object?> values)
        {
            builder.Append('[');
            for (int i = 0; i < values.Count; i++)
            {
                if (i > 0) builder.Append(',');
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
                            builder.Append($"\\u{(int)c:x4}");
                        else
                            builder.Append(c);
                        break;
                }
            }
            builder.Append('"');
        }
    }
}
#endif
