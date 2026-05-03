#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace JonghyunKim.NativeToolkit.Runtime.Notification
{
    /// <summary>
    /// Builds JSON strings for Android notification APIs with full control over optional fields.
    /// </summary>
    public static class AndroidNotificationJsonBuilder
    {
        /// <summary>
        /// Builds JSON for creating a notification channel.
        /// </summary>
        /// <param name="channel">Channel payload to serialize.</param>
        /// <returns>JSON string matching the Android notification channel schema.</returns>
        public static string BuildChannelJson(ChannelPayload channel)
        {
            return SerializeObject(BuildChannelObject(channel));
        }

        /// <summary>
        /// Builds JSON for showing or updating a notification.
        /// </summary>
        /// <param name="notification">Notification payload to serialize.</param>
        /// <returns>JSON string matching the Android notification schema.</returns>
        public static string BuildNotificationJson(NotificationPayload notification)
        {
            return SerializeObject(BuildNotificationObject(notification));
        }

        /// <summary>
        /// Builds JSON for scheduling a notification.
        /// </summary>
        /// <param name="scheduledNotification">Scheduled notification payload to serialize.</param>
        /// <returns>JSON string matching the scheduled notification schema.</returns>
        public static string BuildScheduledNotificationJson(ScheduledNotificationEnvelopePayload scheduledNotification)
        {
            var root = new Dictionary<string, object?>
            {
                ["notification"] = BuildNotificationObject(scheduledNotification.notification),
                ["schedule"] = BuildScheduleObject(scheduledNotification.schedule)
            };

            return SerializeObject(root);
        }

        private static Dictionary<string, object?> BuildChannelObject(ChannelPayload channel)
        {
            var result = new Dictionary<string, object?>
            {
                ["id"] = channel.id,
                ["name"] = channel.name,
                ["importance"] = channel.importance,
                ["showBadge"] = channel.showBadge,
                ["enableLights"] = channel.enableLights,
                ["enableVibration"] = channel.enableVibration,
                ["lockscreenVisibility"] = channel.lockscreenVisibility
            };

            AddIfNotNullOrWhiteSpace(result, "description", channel.description);
            AddIfHasValue(result, "lightColor", channel.lightColor);
            AddIfArrayHasItems(result, "vibrationPattern", channel.vibrationPattern);
            AddIfNotNullOrWhiteSpace(result, "soundUri", channel.soundUri);
            AddIfNotNullOrWhiteSpace(result, "groupId", channel.groupId);
            AddIfNotNullOrWhiteSpace(result, "groupName", channel.groupName);
            return result;
        }

        private static Dictionary<string, object?> BuildNotificationObject(NotificationPayload notification)
        {
            var result = new Dictionary<string, object?>
            {
                ["id"] = notification.id,
                ["title"] = notification.title,
                ["message"] = notification.message,
                ["channel"] = BuildChannelObject(notification.channel),
                ["priority"] = notification.priority,
                ["autoCancel"] = notification.autoCancel,
                ["ongoing"] = notification.ongoing,
                ["showTimestamp"] = notification.showTimestamp,
                ["visibility"] = notification.visibility,
                ["isGroupSummary"] = notification.isGroupSummary,
                ["groupAlertBehavior"] = notification.groupAlertBehavior,
                ["onlyAlertOnce"] = notification.onlyAlertOnce,
                ["localOnly"] = notification.localOnly,
                ["silent"] = notification.silent,
                ["usesChronometer"] = notification.usesChronometer,
                ["launchAppOnTap"] = notification.launchAppOnTap,
                ["fullScreenIntent"] = notification.fullScreenIntent
            };

            AddIfNotNullOrWhiteSpace(result, "tag", notification.tag);
            AddIfNotNullOrWhiteSpace(result, "subText", notification.subText);
            AddIfNotNullOrWhiteSpace(result, "soundUri", notification.soundUri);
            AddIfNotNullOrWhiteSpace(result, "category", notification.category);
            AddIfHasValue(result, "timestampMillis", notification.timestampMillis);
            AddIfHasValue(result, "color", notification.color);
            AddIfHasValue(result, "number", notification.number);
            AddIfNotNullOrWhiteSpace(result, "ticker", notification.ticker);
            AddIfNotNullOrWhiteSpace(result, "groupKey", notification.groupKey);
            AddIfNotNullOrWhiteSpace(result, "sortKey", notification.sortKey);
            AddIfHasValue(result, "timeoutAfterMillis", notification.timeoutAfterMillis);
            AddIfObjectNotNull(result, "smallIcon", BuildResourceObject(notification.smallIcon));
            AddIfObjectNotNull(result, "largeIcon", BuildResourceObject(notification.largeIcon));
            AddIfObjectNotNull(result, "progress", BuildProgressObject(notification.progress));
            AddIfObjectNotNull(result, "style", BuildStyleObject(notification.style));
            AddIfNotNullOrWhiteSpace(result, "launchAction", notification.launchAction);
            AddIfListHasItems(result, "actions", BuildActionObjects(notification.actions));
            AddIfObjectNotNull(result, "data", BuildDataObject(notification.data));
            return result;
        }

        private static Dictionary<string, object?> BuildScheduleObject(NotificationSchedulePayload schedule)
        {
            return new Dictionary<string, object?>
            {
                ["triggerAtMillis"] = schedule.triggerAtMillis,
                ["exact"] = schedule.exact,
                ["allowWhileIdle"] = schedule.allowWhileIdle,
                ["persistAcrossBoot"] = schedule.persistAcrossBoot,
                ["alarmType"] = schedule.alarmType
            };
        }

        private static Dictionary<string, object?>? BuildResourceObject(NotificationResourcePayload? resource)
        {
            if (resource == null || string.IsNullOrWhiteSpace(resource.name))
            {
                return null;
            }

            var result = new Dictionary<string, object?>
            {
                ["name"] = resource.name
            };
            AddIfNotNullOrWhiteSpace(result, "type", resource.type);
            return result;
        }

        private static Dictionary<string, object?>? BuildProgressObject(NotificationProgressPayload? progress)
        {
            if (progress == null)
            {
                return null;
            }

            return new Dictionary<string, object?>
            {
                ["max"] = progress.max,
                ["current"] = progress.current,
                ["indeterminate"] = progress.indeterminate
            };
        }

        private static Dictionary<string, object?>? BuildStyleObject(NotificationStylePayload? style)
        {
            if (style == null)
            {
                return null;
            }

            var result = new Dictionary<string, object?>
            {
                ["type"] = string.IsNullOrWhiteSpace(style.type) ? "default" : style.type,
                ["hideExpandedLargeIcon"] = style.hideExpandedLargeIcon
            };

            AddIfNotNullOrWhiteSpace(result, "bigText", style.bigText);
            AddIfNotNullOrWhiteSpace(result, "summaryText", style.summaryText);
            AddIfNotNullOrWhiteSpace(result, "bigContentTitle", style.bigContentTitle);
            AddIfArrayHasItems(result, "lines", style.lines);
            AddIfObjectNotNull(result, "picture", BuildResourceObject(style.picture));
            AddIfNotNullOrWhiteSpace(result, "pictureUri", style.pictureUri);
            AddIfObjectNotNull(result, "largeIcon", BuildResourceObject(style.largeIcon));
            AddIfNotNullOrWhiteSpace(result, "userDisplayName", style.userDisplayName);
            AddIfNotNullOrWhiteSpace(result, "conversationTitle", style.conversationTitle);
            AddIfHasValue(result, "isGroupConversation", style.isGroupConversation);
            AddIfListHasItems(result, "messages", BuildMessageObjects(style.messages));
            AddIfNotNullOrWhiteSpace(result, "customViewLayout", style.customViewLayout);
            AddIfNotNullOrWhiteSpace(result, "bigCustomViewLayout", style.bigCustomViewLayout);
            AddIfListHasItems(result, "viewActions", BuildViewActionObjects(style.viewActions));
            return result;
        }

        private static List<object?> BuildActionObjects(NotificationActionPayload[] actions)
        {
            var result = new List<object?>();
            foreach (var action in actions)
            {
                if (string.IsNullOrWhiteSpace(action.title) || string.IsNullOrWhiteSpace(action.actionId))
                {
                    continue;
                }

                var actionObject = new Dictionary<string, object?>
                {
                    ["title"] = action.title,
                    ["actionId"] = action.actionId,
                    ["launchApp"] = action.launchApp,
                    ["allowGeneratedReplies"] = action.allowGeneratedReplies,
                    ["semanticAction"] = action.semanticAction,
                    ["contextual"] = action.contextual,
                    ["showsUserInterface"] = action.showsUserInterface
                };
                AddIfObjectNotNull(actionObject, "icon", BuildResourceObject(action.icon));
                result.Add(actionObject);
            }

            return result;
        }

        private static List<object?> BuildMessageObjects(NotificationMessagePayload[] messages)
        {
            var result = new List<object?>();
            foreach (var message in messages)
            {
                if (string.IsNullOrWhiteSpace(message.text))
                {
                    continue;
                }

                var messageObject = new Dictionary<string, object?>
                {
                    ["text"] = message.text
                };
                AddIfHasValue(messageObject, "timestampMillis", message.timestampMillis);
                AddIfNotNullOrWhiteSpace(messageObject, "senderName", message.senderName);
                result.Add(messageObject);
            }

            return result;
        }

        private static List<object?> BuildViewActionObjects(NotificationViewActionPayload[] viewActions)
        {
            var result = new List<object?>();
            foreach (var viewAction in viewActions)
            {
                if (string.IsNullOrWhiteSpace(viewAction.type) || string.IsNullOrWhiteSpace(viewAction.viewId))
                {
                    continue;
                }

                var viewActionObject = new Dictionary<string, object?>
                {
                    ["type"] = viewAction.type,
                    ["viewId"] = viewAction.viewId
                };
                AddIfNotNullOrWhiteSpace(viewActionObject, "actionId", viewAction.actionId);
                result.Add(viewActionObject);
            }

            return result;
        }

        private static Dictionary<string, object?>? BuildDataObject(NotificationDataEntryPayload[] dataEntries)
        {
            var result = new Dictionary<string, object?>();
            foreach (var entry in dataEntries)
            {
                if (string.IsNullOrWhiteSpace(entry.key))
                {
                    continue;
                }

                result[entry.key] = entry.value;
            }

            return result.Count > 0 ? result : null;
        }

        private static void AddIfNotNullOrWhiteSpace(Dictionary<string, object?> target, string key, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                target[key] = value;
            }
        }

        private static void AddIfHasValue<T>(Dictionary<string, object?> target, string key, T? value) where T : struct
        {
            if (value.HasValue)
            {
                target[key] = value.Value;
            }
        }

        private static void AddIfArrayHasItems<T>(Dictionary<string, object?> target, string key, T[]? values)
        {
            if (values == null || values.Length == 0)
            {
                return;
            }

            var list = new List<object?>(values.Length);
            foreach (var value in values)
            {
                list.Add(value);
            }
            target[key] = list;
        }

        private static void AddIfListHasItems(Dictionary<string, object?> target, string key, List<object?> values)
        {
            if (values.Count > 0)
            {
                target[key] = values;
            }
        }

        private static void AddIfObjectNotNull(Dictionary<string, object?> target, string key, Dictionary<string, object?>? value)
        {
            if (value != null && value.Count > 0)
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
                case string stringValue:
                    AppendEscapedString(builder, stringValue);
                    break;
                case bool boolValue:
                    builder.Append(boolValue ? "true" : "false");
                    break;
                case int intValue:
                    builder.Append(intValue.ToString(CultureInfo.InvariantCulture));
                    break;
                case long longValue:
                    builder.Append(longValue.ToString(CultureInfo.InvariantCulture));
                    break;
                case Dictionary<string, object?> dictionaryValue:
                    AppendObject(builder, dictionaryValue);
                    break;
                case List<object?> listValue:
                    AppendArray(builder, listValue);
                    break;
                default:
                    throw new NotSupportedException($"Unsupported JSON value type: {value.GetType().FullName}");
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
            for (int index = 0; index < values.Count; index++)
            {
                if (index > 0)
                {
                    builder.Append(',');
                }
                AppendValue(builder, values[index]);
            }
            builder.Append(']');
        }

        private static void AppendEscapedString(StringBuilder builder, string value)
        {
            builder.Append('"');
            foreach (char character in value)
            {
                switch (character)
                {
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '\b':
                        builder.Append("\\b");
                        break;
                    case '\f':
                        builder.Append("\\f");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        if (character < 0x20)
                        {
                            builder.Append("\\u");
                            builder.Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            builder.Append(character);
                        }
                        break;
                }
            }
            builder.Append('"');
        }
    }
}
