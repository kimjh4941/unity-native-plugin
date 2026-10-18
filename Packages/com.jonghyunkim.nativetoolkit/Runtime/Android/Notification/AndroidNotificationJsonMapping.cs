#nullable enable

#if UNITY_ANDROID || UNITY_EDITOR
namespace JonghyunKim.NativeToolkit.Runtime.Android.Notification
{
    using System;
    using System.Collections.Generic;

    /// <summary>A channel as 1.x read it (its <c>UnityNotificationChannelSpec</c>), with 1.x's corrections applied.</summary>
    internal sealed class AndroidNotificationChannelSpec
    {
        internal string Id = string.Empty;
        internal string Name = string.Empty;
        /// <summary>0 to 4; anything else read as 3 (design J-N5).</summary>
        internal int Importance = 3;
        internal string? Description;
        internal bool ShowBadge = true;
        internal bool EnableLights = true;
        internal uint? LightColor;
        internal bool EnableVibration = true;
        internal long[]? VibrationPattern;
        internal string? SoundUri;
        /// <summary>-1 to 1; anything else read as 1 (design J-N5).</summary>
        internal int LockscreenVisibility = 1;
        internal string? GroupId;
        /// <summary>The group's name: the group ID when the JSON has none, as 1.x's library did.</summary>
        internal string? GroupName;
    }

    /// <summary>An action button (1.x's <c>UnityNotificationActionSpec</c>).</summary>
    internal sealed class AndroidNotificationActionSpec
    {
        internal string Title = string.Empty;
        internal string ActionId = string.Empty;
        internal string? IconName;
        internal bool LaunchApp;
        internal bool AllowGeneratedReplies;
        internal int SemanticAction;
        internal bool Contextual;
        internal bool ShowsUserInterface = true;
    }

    /// <summary>The styles the builder can set; <see cref="None"/> sets none (1.x's Default style).</summary>
    internal enum AndroidNotificationStyleKind
    {
        None,
        BigText,
        Inbox,
        BigPicture,
        Messaging,
        CustomView,
    }

    /// <summary>A message of the messaging style.</summary>
    internal readonly struct AndroidNotificationMessageSpec
    {
        internal AndroidNotificationMessageSpec(string text, long timestampMillis, string? senderName)
        {
            Text = text;
            TimestampMillis = timestampMillis;
            SenderName = senderName;
        }

        internal string Text { get; }
        internal long TimestampMillis { get; }
        internal string? SenderName { get; }
    }

    /// <summary>A style, read by its type (1.x's <c>UnityNotificationStyleSpec</c>).</summary>
    internal sealed class AndroidNotificationStyleSpec
    {
        internal AndroidNotificationStyleKind Kind = AndroidNotificationStyleKind.None;
        internal string? BigText;
        internal string? SummaryText;
        internal string? BigContentTitle;
        internal List<string> Lines = new();
        internal string? PictureName;
        internal string? PictureUri;
        internal string? LargeIconName;
        internal bool HideExpandedLargeIcon;
        internal string UserDisplayName = "You";
        internal string? ConversationTitle;
        /// <summary>-1: the key is absent; 0: present and not true; 1: true.</summary>
        internal int GroupConversation = -1;
        internal List<AndroidNotificationMessageSpec> Messages = new();
        internal string? Layout;
        internal string? BigLayout;
        /// <summary>The click handlers of the custom view, as (view ID, action ID).</summary>
        internal List<KeyValuePair<string, string>> ViewClicks = new();
    }

    /// <summary>A notification as 1.x read it (its <c>UnityNotificationSpec</c>), with 1.x's corrections applied.</summary>
    internal sealed class AndroidNotificationSpec
    {
        internal int Id;
        internal string Title = string.Empty;
        internal string Message = string.Empty;
        internal string? Tag;
        internal AndroidNotificationChannelSpec Channel = new();
        internal string? SmallIconName;
        internal string? LargeIconName;
        /// <summary>Clamped to -2..2 (design J-N5).</summary>
        internal int Priority;
        internal bool AutoCancel = true;
        internal bool Ongoing;
        internal string? SubText;
        internal bool ShowTimestamp = true;
        internal long? TimestampMillis;
        internal string? SoundUri;
        internal string? Category;
        /// <summary>-1 to 1; anything else read as 1 (design J-N5).</summary>
        internal int Visibility = 1;
        internal uint? Color;
        internal int? Number;
        internal string? Ticker;
        internal string? GroupKey;
        internal bool IsGroupSummary;
        internal int GroupAlertBehavior;
        internal string? SortKey;
        internal bool OnlyAlertOnce;
        internal bool LocalOnly;
        internal bool Silent;
        internal bool UsesChronometer;
        internal long? TimeoutAfterMillis;
        internal bool HasProgress;
        /// <summary>When not indeterminate: at least 0, and <see cref="ProgressCurrent"/> within 0..max (design J-N5).</summary>
        internal int ProgressMax;
        internal int ProgressCurrent;
        internal bool ProgressIndeterminate;
        internal AndroidNotificationStyleSpec Style = new();
        internal bool LaunchAppOnTap = true;
        internal List<AndroidNotificationActionSpec> Actions = new();
        internal bool FullScreenIntent;
        /// <summary>The data entries whose values are strings, in document order; empty when there are none.</summary>
        internal List<KeyValuePair<string, string>> Data = new();
    }

    /// <summary>A scheduled notification (1.x's <c>UnityScheduledNotificationSpec</c>).</summary>
    internal sealed class AndroidNotificationScheduleSpec
    {
        internal AndroidNotificationSpec Notification = new();
        internal long TriggerAtMillis;
        internal bool Exact = true;
        internal bool AllowWhileIdle = true;
        internal bool PersistAcrossBoot = true;
        internal int AlarmType;
    }

    /// <summary>
    /// Reads the JSON <c>AndroidNotificationManager</c> takes into the specs above, by the rules of
    /// 1.x's Kotlin parser (<c>UnityNotificationJsonParser</c>; design v4, 4.2): the required keys and
    /// the order they are checked in, the defaults, blank strings read as absent, the entries it
    /// dropped, and the corrections the 1.x bridge applied (design J-N5).
    /// <para>
    /// Unlike org.json, a value of another type than the one the builder writes is absent (a number
    /// is not read as a string, nor a string as a number); a fraction is cut toward 0, and a number
    /// out of the field's range is absent (design 5.3).
    /// </para>
    /// <para>
    /// No logs: the functions are pure, and the manager logs the call and what it read (lengths and
    /// counts only).
    /// </para>
    /// <para>
    /// Each function returns null and its spec, or the result's error message: "Invalid argument for
    /// {op}: {key} is required." for a missing key (1.x's wording), and "Failed to {op}: the JSON could
    /// not be read." for a document that is not a JSON object.
    /// </para>
    /// </summary>
    internal static class AndroidNotificationJsonMapping
    {
        internal static string Unreadable(string operation) => $"Failed to {operation}: the JSON could not be read.";

        internal static string Invalid(string operation, string detail) => $"Invalid argument for {operation}: {detail}";

        /// <summary>A notification (<c>ShowNotification</c>, <c>UpdateNotification</c> and the Progress calls).</summary>
        internal static string? ReadNotification(string? json, string operation, long nowMillis, out AndroidNotificationSpec? spec)
        {
            spec = null;
            AndroidJsonValue? root = AndroidNotificationJsonReader.Parse(json);
            if (root == null || root.Kind != AndroidJsonValueKind.Object) return Unreadable(operation);
            string? missing = Notification(root, nowMillis, out spec);
            return missing == null ? null : Invalid(operation, missing);
        }

        /// <summary>A scheduled notification: <c>{"notification": {...}, "schedule": {...}}</c>.</summary>
        internal static string? ReadSchedule(string? json, string operation, long nowMillis, out AndroidNotificationScheduleSpec? spec)
        {
            spec = null;
            AndroidJsonValue? root = AndroidNotificationJsonReader.Parse(json);
            if (root == null || root.Kind != AndroidJsonValueKind.Object) return Unreadable(operation);
            AndroidJsonValue? notification = ObjectOf(root, "notification");
            if (notification == null) return Invalid(operation, "notification is required.");
            AndroidJsonValue? schedule = ObjectOf(root, "schedule");
            if (schedule == null) return Invalid(operation, "schedule is required.");

            string? missing = Notification(notification, nowMillis, out AndroidNotificationSpec? content);
            if (missing != null) return Invalid(operation, missing);
            long? trigger = Long(schedule, "triggerAtMillis");
            if (trigger == null || trigger <= 0) return Invalid(operation, "schedule.triggerAtMillis must be greater than 0.");

            spec = new AndroidNotificationScheduleSpec
            {
                Notification = content!,
                TriggerAtMillis = trigger.Value,
                Exact = Bool(schedule, "exact", true),
                AllowWhileIdle = Bool(schedule, "allowWhileIdle", true),
                PersistAcrossBoot = Bool(schedule, "persistAcrossBoot", true),
                AlarmType = Int(schedule, "alarmType") ?? 0,
            };
            return null;
        }

        /// <summary>A channel (<c>CreateChannel</c>).</summary>
        internal static string? ReadChannel(string? json, string operation, out AndroidNotificationChannelSpec? spec)
        {
            spec = null;
            AndroidJsonValue? root = AndroidNotificationJsonReader.Parse(json);
            if (root == null || root.Kind != AndroidJsonValueKind.Object) return Unreadable(operation);
            string? missing = Channel(root, out spec);
            return missing == null ? null : Invalid(operation, missing);
        }

        // ── The objects, in 1.x's order ──────────────────────────────────────────

        /// <summary>1.x checked channel, id, title, message, then the channel's own keys, then the style.</summary>
        private static string? Notification(AndroidJsonValue json, long nowMillis, out AndroidNotificationSpec? spec)
        {
            spec = null;
            AndroidJsonValue? channel = ObjectOf(json, "channel");
            if (channel == null) return "channel is required.";
            int? id = Int(json, "id");
            if (id == null || id == int.MinValue) return "id is required.";
            string? title = Text(json, "title");
            if (title == null) return "title is required.";
            string? message = Text(json, "message");
            if (message == null) return "message is required.";
            string? tag = Text(json, "tag");
            string? channelMissing = Channel(channel, out AndroidNotificationChannelSpec? channelSpec);
            if (channelMissing != null) return channelMissing;
            string? styleMissing = Style(ObjectOf(json, "style"), nowMillis, out AndroidNotificationStyleSpec style);
            if (styleMissing != null) return styleMissing;

            var result = new AndroidNotificationSpec
            {
                Id = id.Value,
                Title = title,
                Message = message,
                Tag = tag,
                Channel = channelSpec!,
                SmallIconName = ResourceName(json, "smallIcon"),
                LargeIconName = ResourceName(json, "largeIcon"),
                Priority = Math.Max(-2, Math.Min(2, Int(json, "priority") ?? 0)),
                AutoCancel = Bool(json, "autoCancel", true),
                Ongoing = Bool(json, "ongoing", false),
                SubText = Text(json, "subText"),
                ShowTimestamp = Bool(json, "showTimestamp", true),
                TimestampMillis = Long(json, "timestampMillis"),
                SoundUri = Text(json, "soundUri"),
                Category = Text(json, "category"),
                Visibility = Visibility(Int(json, "visibility")),
                Color = Argb(json, "color"),
                Number = Int(json, "number"),
                Ticker = Text(json, "ticker"),
                GroupKey = Text(json, "groupKey"),
                IsGroupSummary = Bool(json, "isGroupSummary", false),
                GroupAlertBehavior = Int(json, "groupAlertBehavior") ?? 0,
                SortKey = Text(json, "sortKey"),
                OnlyAlertOnce = Bool(json, "onlyAlertOnce", false),
                LocalOnly = Bool(json, "localOnly", false),
                Silent = Bool(json, "silent", false),
                UsesChronometer = Bool(json, "usesChronometer", false),
                TimeoutAfterMillis = Long(json, "timeoutAfterMillis"),
                Style = style,
                LaunchAppOnTap = Bool(json, "launchAppOnTap", true),
                FullScreenIntent = Bool(json, "fullScreenIntent", false),
            };

            AndroidJsonValue? progress = ObjectOf(json, "progress");
            if (progress != null)
            {
                result.HasProgress = true;
                result.ProgressIndeterminate = Bool(progress, "indeterminate", false);
                int max = Int(progress, "max") ?? 0;
                int current = Int(progress, "current") ?? 0;
                if (!result.ProgressIndeterminate)
                {
                    max = Math.Max(0, max);
                    current = Math.Max(0, Math.Min(max, current));
                }
                result.ProgressMax = max;
                result.ProgressCurrent = current;
            }

            if (json.TryGetMember("actions", out AndroidJsonValue actions))
            {
                foreach (AndroidJsonValue element in actions.Elements)
                {
                    if (element.Kind != AndroidJsonValueKind.Object) continue;
                    string? actionTitle = Text(element, "title");
                    string? actionId = Text(element, "actionId");
                    if (actionTitle == null || actionId == null) continue;
                    result.Actions.Add(new AndroidNotificationActionSpec
                    {
                        Title = actionTitle,
                        ActionId = actionId,
                        IconName = ResourceName(element, "icon"),
                        LaunchApp = Bool(element, "launchApp", false),
                        AllowGeneratedReplies = Bool(element, "allowGeneratedReplies", false),
                        SemanticAction = Int(element, "semanticAction") ?? 0,
                        Contextual = Bool(element, "contextual", false),
                        ShowsUserInterface = Bool(element, "showsUserInterface", true),
                    });
                }
            }

            AndroidJsonValue? data = ObjectOf(json, "data");
            if (data != null)
            {
                foreach (KeyValuePair<string, AndroidJsonValue> entry in data.Members)
                {
                    // 1.x turned every value into a string (null into "null"); only strings are kept (design 5.3).
                    if (entry.Value.TryGetString(out string value))
                        result.Data.Add(new KeyValuePair<string, string>(entry.Key, value));
                }
            }

            spec = result;
            return null;
        }

        private static string? Channel(AndroidJsonValue json, out AndroidNotificationChannelSpec? spec)
        {
            spec = null;
            string? id = Text(json, "id");
            if (id == null) return "id is required.";
            string? name = Text(json, "name");
            if (name == null) return "name is required.";

            int importance = Int(json, "importance") ?? 3;
            string? groupId = Text(json, "groupId");
            var result = new AndroidNotificationChannelSpec
            {
                Id = id,
                Name = name,
                Importance = importance >= 0 && importance <= 4 ? importance : 3,
                Description = Text(json, "description"),
                ShowBadge = Bool(json, "showBadge", true),
                EnableLights = Bool(json, "enableLights", true),
                LightColor = Argb(json, "lightColor"),
                EnableVibration = Bool(json, "enableVibration", true),
                SoundUri = Text(json, "soundUri"),
                LockscreenVisibility = Visibility(Int(json, "lockscreenVisibility")),
                GroupId = groupId,
                GroupName = groupId == null ? null : Text(json, "groupName") ?? groupId,
            };

            if (json.TryGetMember("vibrationPattern", out AndroidJsonValue pattern) && pattern.Kind == AndroidJsonValueKind.Array)
            {
                var values = new List<long>();
                foreach (AndroidJsonValue element in pattern.Elements)
                {
                    long? value = AsLong(element);
                    if (value != null) values.Add(value.Value);
                }
                if (values.Count > 0) result.VibrationPattern = values.ToArray();
            }

            spec = result;
            return null;
        }

        private static string? Style(AndroidJsonValue? json, long nowMillis, out AndroidNotificationStyleSpec style)
        {
            style = new AndroidNotificationStyleSpec();
            if (json == null) return null;
            switch (Text(json, "type"))
            {
                case "bigText":
                    string? bigText = Text(json, "bigText");
                    if (bigText == null) return "bigText is required.";
                    style.Kind = AndroidNotificationStyleKind.BigText;
                    style.BigText = bigText;
                    style.SummaryText = Text(json, "summaryText");
                    style.BigContentTitle = Text(json, "bigContentTitle");
                    return null;

                case "inbox":
                    style.Kind = AndroidNotificationStyleKind.Inbox;
                    if (json.TryGetMember("lines", out AndroidJsonValue lines))
                    {
                        foreach (AndroidJsonValue line in lines.Elements)
                        {
                            if (line.TryGetString(out string value) && !IsBlank(value)) style.Lines.Add(value);
                        }
                    }
                    style.SummaryText = Text(json, "summaryText");
                    style.BigContentTitle = Text(json, "bigContentTitle");
                    return null;

                case "bigPicture":
                    string? picture = ResourceName(json, "picture");
                    string? pictureUri = Text(json, "pictureUri");
                    // 1.x showed the Default style when it had neither (design 4.2).
                    if (picture == null && pictureUri == null) return null;
                    style.Kind = AndroidNotificationStyleKind.BigPicture;
                    style.PictureName = picture;
                    style.PictureUri = pictureUri;
                    style.SummaryText = Text(json, "summaryText");
                    style.BigContentTitle = Text(json, "bigContentTitle");
                    style.LargeIconName = ResourceName(json, "largeIcon");
                    style.HideExpandedLargeIcon = Bool(json, "hideExpandedLargeIcon", false);
                    return null;

                case "messaging":
                    style.Kind = AndroidNotificationStyleKind.Messaging;
                    style.UserDisplayName = Text(json, "userDisplayName") ?? "You";
                    style.ConversationTitle = Text(json, "conversationTitle");
                    if (json.TryGetMember("isGroupConversation", out AndroidJsonValue group))
                        style.GroupConversation = group.TryGetBool(out bool isGroup) && isGroup ? 1 : 0;
                    if (json.TryGetMember("messages", out AndroidJsonValue messages))
                    {
                        foreach (AndroidJsonValue element in messages.Elements)
                        {
                            if (element.Kind != AndroidJsonValueKind.Object) continue;
                            string? text = Text(element, "text");
                            if (text == null) continue;
                            style.Messages.Add(new AndroidNotificationMessageSpec(
                                text, Long(element, "timestampMillis") ?? nowMillis, Text(element, "senderName")));
                        }
                    }
                    return null;

                case "decoratedCustomView":
                    string? layout = Text(json, "customViewLayout");
                    // 1.x showed the Default style without a layout (design 4.2).
                    if (layout == null) return null;
                    style.Kind = AndroidNotificationStyleKind.CustomView;
                    style.Layout = layout;
                    style.BigLayout = Text(json, "bigCustomViewLayout");
                    if (json.TryGetMember("viewActions", out AndroidJsonValue viewActions))
                    {
                        foreach (AndroidJsonValue element in viewActions.Elements)
                        {
                            if (element.Kind != AndroidJsonValueKind.Object) continue;
                            string? viewId = Text(element, "viewId");
                            string? actionId = Text(element, "actionId");
                            if (Text(element, "type") != "setClickIntent" || viewId == null || actionId == null) continue;
                            style.ViewClicks.Add(new KeyValuePair<string, string>(viewId, actionId));
                        }
                    }
                    return null;

                default:
                    // Absent, blank, "default" or unknown: the Default style.
                    return null;
            }
        }

        // ── Values ───────────────────────────────────────────────────────────────

        private static AndroidJsonValue? ObjectOf(AndroidJsonValue json, string key) =>
            json.TryGetMember(key, out AndroidJsonValue value) && value.Kind == AndroidJsonValueKind.Object ? value : null;

        /// <summary>A string that is not blank (1.x's <c>optStringOrNull</c>), or null.</summary>
        private static string? Text(AndroidJsonValue json, string key) =>
            json.TryGetMember(key, out AndroidJsonValue value) && value.TryGetString(out string text) && !IsBlank(text)
                ? text
                : null;

        /// <summary>
        /// Whether a string is blank as Kotlin's <c>isBlank</c> has it, which 1.x used: every character is
        /// Java's <c>Character.isWhitespace</c> or <c>isSpaceChar</c>. That is .NET's
        /// <c>char.IsWhiteSpace</c> less U+0085, plus U+001C to U+001F.
        /// </summary>
        internal static bool IsBlank(string text)
        {
            foreach (char c in text)
            {
                bool blank = (char.IsWhiteSpace(c) && c != '\u0085') || (c >= '\u001C' && c <= '\u001F');
                if (!blank) return false;
            }
            return true;
        }

        /// <summary>The name of a resource object <c>{name, type?}</c>; the type is not passed on (design 5.3).</summary>
        private static string? ResourceName(AndroidJsonValue json, string key)
        {
            AndroidJsonValue? resource = ObjectOf(json, key);
            return resource == null ? null : Text(resource, "name");
        }

        private static bool Bool(AndroidJsonValue json, string key, bool fallback) =>
            json.TryGetMember(key, out AndroidJsonValue value) && value.TryGetBool(out bool flag) ? flag : fallback;

        /// <summary>A number cut toward 0 that fits an int, or null.</summary>
        private static int? Int(AndroidJsonValue json, string key)
        {
            if (!json.TryGetMember(key, out AndroidJsonValue value) || !value.TryGetNumber(out double number)) return null;
            double cut = Math.Truncate(number);
            return cut >= int.MinValue && cut <= int.MaxValue ? (int)cut : (int?)null;
        }

        private static long? Long(AndroidJsonValue json, string key) =>
            json.TryGetMember(key, out AndroidJsonValue value) ? AsLong(value) : null;

        /// <summary>A number cut toward 0 that fits a long, or null.</summary>
        private static long? AsLong(AndroidJsonValue value)
        {
            if (!value.TryGetNumber(out double number)) return null;
            double cut = Math.Truncate(number);
            // 2^63 itself is not a long; doubles just below it are.
            return cut >= -9223372036854775808.0 && cut < 9223372036854775808.0 ? (long)cut : (long?)null;
        }

        /// <summary>
        /// An ARGB color: an integer from int.MinValue to uint.MaxValue (1.x's payloads write negative
        /// ARGB values as ints), kept as its 32 bits; anything else is absent (design 4.2).
        /// </summary>
        private static uint? Argb(AndroidJsonValue json, string key)
        {
            long? value = Long(json, key);
            if (value == null || value < int.MinValue || value > uint.MaxValue) return null;
            return unchecked((uint)value.Value);
        }

        /// <summary>-1, 0 or 1; anything else (and absent) is 1, as the 1.x bridge corrected it.</summary>
        private static int Visibility(int? value) => value is >= -1 and <= 1 ? value.Value : 1;
    }
}
#endif
