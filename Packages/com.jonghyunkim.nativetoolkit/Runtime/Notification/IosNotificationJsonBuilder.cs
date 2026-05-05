#nullable enable

using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace JonghyunKim.NativeToolkit.Runtime.Notification
{
    /// <summary>
    /// Builds JSON strings for iOS notification APIs.
    /// All methods produce output matching the schemas expected by UnityIosNotificationJsonParser.
    /// </summary>
    public static class IosNotificationJsonBuilder
    {
        /// <summary>
        /// Builds JSON for notification content.
        /// </summary>
        /// <param name="content">Content payload to serialize.</param>
        /// <returns>JSON string matching the iOS notification content schema.</returns>
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
            AddIfNotNullOrWhiteSpace(result, "sound", content.sound);
            AddIfNotNullOrWhiteSpace(result, "categoryIdentifier", content.categoryIdentifier);
            AddIfNotNullOrWhiteSpace(result, "interruptionLevel", content.interruptionLevel);
            AddIfNotNullOrWhiteSpace(result, "threadIdentifier", content.threadIdentifier);
            AddIfNotNullOrWhiteSpace(result, "targetContentIdentifier", content.targetContentIdentifier);
            AddIfHasValue(result, "relevanceScore", content.relevanceScore);
            AddIfNotNullOrWhiteSpace(result, "filterCriteria", content.filterCriteria);
            AddIfListHasItems(result, "attachments", BuildAttachmentObjects(content.attachments));

            return SerializeObject(result);
        }

        /// <summary>
        /// Builds JSON for a time interval trigger.
        /// </summary>
        /// <param name="trigger">Time interval trigger payload to serialize.</param>
        /// <returns>JSON string matching the iOS time interval trigger schema.</returns>
        public static string BuildTimeIntervalTriggerJson(TimeIntervalTriggerPayload trigger)
        {
            var result = new Dictionary<string, object?>
            {
                ["type"] = "timeInterval",
                ["interval"] = trigger.interval,
                ["repeats"] = trigger.repeats
            };

            return SerializeObject(result);
        }

        /// <summary>
        /// Builds JSON for a calendar trigger.
        /// </summary>
        /// <param name="trigger">Calendar trigger payload to serialize.</param>
        /// <returns>JSON string matching the iOS calendar trigger schema.</returns>
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
        /// Builds JSON for a location trigger.
        /// </summary>
        /// <param name="trigger">Location trigger payload to serialize.</param>
        /// <returns>JSON string matching the iOS location trigger schema.</returns>
        public static string BuildLocationTriggerJson(LocationTriggerPayload trigger)
        {
            var result = new Dictionary<string, object?>
            {
                ["type"] = "location",
                ["identifier"] = trigger.identifier,
                ["latitude"] = trigger.latitude,
                ["longitude"] = trigger.longitude,
                ["radius"] = trigger.radius,
                ["notifyOnEntry"] = trigger.notifyOnEntry,
                ["notifyOnExit"] = trigger.notifyOnExit
            };

            return SerializeObject(result);
        }

        /// <summary>
        /// Builds JSON for a notification category with associated action buttons.
        /// </summary>
        /// <param name="category">Category payload to serialize.</param>
        /// <returns>JSON string matching the iOS notification category schema.</returns>
        public static string BuildCategoryJson(IosNotificationCategoryPayload category)
        {
            var result = new Dictionary<string, object?>
            {
                ["identifier"] = category.identifier
            };

            AddIfListHasItems(result, "actions", BuildActionObjects(category.actions));
            AddIfListHasItems(result, "textInputActions", BuildTextInputActionObjects(category.textInputActions));
            AddIfArrayHasItems(result, "options", category.options);

            return SerializeObject(result);
        }

        private static List<object?> BuildAttachmentObjects(NotificationAttachmentPayload[] attachments)
        {
            var result = new List<object?>();
            foreach (var attachment in attachments)
            {
                if (string.IsNullOrWhiteSpace(attachment.identifier) || string.IsNullOrWhiteSpace(attachment.fileURL))
                    continue;

                result.Add(new Dictionary<string, object?>
                {
                    ["identifier"] = attachment.identifier,
                    ["fileURL"] = attachment.fileURL
                });
            }

            return result;
        }

        private static List<object?> BuildActionObjects(IosNotificationActionPayload[] actions)
        {
            var result = new List<object?>();
            foreach (var action in actions)
            {
                if (string.IsNullOrWhiteSpace(action.identifier) || string.IsNullOrWhiteSpace(action.title))
                    continue;

                var actionObject = new Dictionary<string, object?>
                {
                    ["identifier"] = action.identifier,
                    ["title"] = action.title
                };
                AddIfNotNullOrWhiteSpace(actionObject, "sfSymbolName", action.sfSymbolName);
                AddIfArrayHasItems(actionObject, "options", action.options);
                result.Add(actionObject);
            }

            return result;
        }

        private static List<object?> BuildTextInputActionObjects(IosNotificationTextInputActionPayload[] actions)
        {
            var result = new List<object?>();
            foreach (var action in actions)
            {
                if (string.IsNullOrWhiteSpace(action.identifier) || string.IsNullOrWhiteSpace(action.title))
                    continue;

                result.Add(new Dictionary<string, object?>
                {
                    ["identifier"] = action.identifier,
                    ["title"] = action.title,
                    ["buttonTitle"] = action.buttonTitle,
                    ["textInputPlaceholder"] = action.textInputPlaceholder
                });
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

        private static void AddIfArrayHasItems<T>(Dictionary<string, object?> target, string key, T[]? values)
        {
            if (values == null || values.Length == 0) return;
            var list = new List<object?>(values.Length);
            foreach (var value in values)
                list.Add(value);
            target[key] = list;
        }

        private static void AddIfListHasItems(Dictionary<string, object?> target, string key, List<object?> values)
        {
            if (values.Count > 0)
                target[key] = values;
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
