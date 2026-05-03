#nullable enable

using System;

namespace JonghyunKim.NativeToolkit.Runtime.Notification
{
    /// <summary>
    /// Represents an Android resource reference by name and optional type.
    /// </summary>
    [Serializable]
    public sealed class NotificationResourcePayload
    {
        /// <summary>Resource entry name.</summary>
        public string name = string.Empty;

        /// <summary>Optional resource type (for example, drawable).</summary>
        public string? type;
    }

    /// <summary>
    /// Represents Android notification channel metadata used in notification payloads.
    /// </summary>
    [Serializable]
    public sealed class ChannelPayload
    {
        /// <summary>Channel identifier.</summary>
        public string id = string.Empty;

        /// <summary>Channel display name.</summary>
        public string name = string.Empty;

        /// <summary>Channel importance level.</summary>
        public int importance = 3;

        /// <summary>Channel description shown in system settings.</summary>
        public string? description;

        /// <summary>Whether to show badge for this channel.</summary>
        public bool showBadge = true;

        /// <summary>Whether to enable notification lights.</summary>
        public bool enableLights = true;

        /// <summary>ARGB light color.</summary>
        public int? lightColor;

        /// <summary>Whether to enable vibration.</summary>
        public bool enableVibration = true;

        /// <summary>Vibration pattern in milliseconds.</summary>
        public long[]? vibrationPattern;

        /// <summary>Optional custom sound URI.</summary>
        public string? soundUri;

        /// <summary>Lockscreen visibility value.</summary>
        public int lockscreenVisibility = 1;

        /// <summary>Channel group identifier.</summary>
        public string? groupId;

        /// <summary>Channel group display name.</summary>
        public string? groupName;
    }

    /// <summary>
    /// Represents the primary notification payload sent to Android native APIs.
    /// </summary>
    [Serializable]
    public class NotificationPayload
    {
        /// <summary>Notification identifier.</summary>
        public int id;

        /// <summary>Notification title text.</summary>
        public string title = string.Empty;

        /// <summary>Notification message text.</summary>
        public string message = string.Empty;

        /// <summary>Notification tag used with ID for grouping/replacement.</summary>
        public string? tag;

        /// <summary>Channel metadata for this notification.</summary>
        public ChannelPayload channel = new ChannelPayload();

        /// <summary>Optional small icon resource.</summary>
        public NotificationResourcePayload? smallIcon;

        /// <summary>Optional large icon resource.</summary>
        public NotificationResourcePayload? largeIcon;

        /// <summary>Optional sub text.</summary>
        public string? subText;

        /// <summary>Whether to show timestamp on the notification.</summary>
        public bool showTimestamp = true;

        /// <summary>Optional custom notification sound URI.</summary>
        public string? soundUri;

        /// <summary>Optional Android category string.</summary>
        public string? category;

        /// <summary>Notification visibility on lockscreen.</summary>
        public int visibility = 1;

        /// <summary>Optional timestamp in Unix milliseconds.</summary>
        public long? timestampMillis;

        /// <summary>Optional ARGB accent color.</summary>
        public int? color;

        /// <summary>Optional badge/count number.</summary>
        public int? number;

        /// <summary>Optional ticker text.</summary>
        public string? ticker;

        /// <summary>Optional notification group key.</summary>
        public string? groupKey;

        /// <summary>Whether this notification is a group summary.</summary>
        public bool isGroupSummary;

        /// <summary>Android group alert behavior.</summary>
        public int groupAlertBehavior;

        /// <summary>Optional sort key within a notification group.</summary>
        public string? sortKey;

        /// <summary>Optional timeout in milliseconds.</summary>
        public long? timeoutAfterMillis;

        /// <summary>Whether tapping the notification automatically dismisses it.</summary>
        public bool autoCancel = true;

        /// <summary>Notification priority value.</summary>
        public int priority;

        /// <summary>Whether this notification is ongoing.</summary>
        public bool ongoing;

        /// <summary>Whether to alert only once for updates.</summary>
        public bool onlyAlertOnce;

        /// <summary>Whether this notification is local only.</summary>
        public bool localOnly;

        /// <summary>Whether this notification should be silent.</summary>
        public bool silent;

        /// <summary>Whether to use a chronometer display.</summary>
        public bool usesChronometer;

        /// <summary>Optional progress configuration.</summary>
        public NotificationProgressPayload? progress;

        /// <summary>Optional style configuration.</summary>
        public NotificationStylePayload? style;

        /// <summary>Whether tapping the body should launch app UI.</summary>
        public bool launchAppOnTap = true;

        /// <summary>Optional custom launch action delivered on body tap.</summary>
        public string? launchAction;

        /// <summary>Whether to use full-screen intent.</summary>
        public bool fullScreenIntent;

        /// <summary>Optional action buttons.</summary>
        public NotificationActionPayload[] actions = Array.Empty<NotificationActionPayload>();

        /// <summary>
        /// Optional custom key-value payload.
        /// JsonUtility cannot serialize dictionary directly; for dynamic keys inject data manually.
        /// </summary>
        public NotificationDataEntryPayload[] data = Array.Empty<NotificationDataEntryPayload>();
    }

    /// <summary>
    /// Represents an action button displayed in a notification.
    /// </summary>
    [Serializable]
    public sealed class NotificationActionPayload
    {
        /// <summary>Action button label.</summary>
        public string title = string.Empty;

        /// <summary>Action identifier returned when tapped.</summary>
        public string actionId = string.Empty;

        /// <summary>Optional action icon resource.</summary>
        public NotificationResourcePayload? icon;

        /// <summary>Whether action should launch the app.</summary>
        public bool launchApp;

        /// <summary>Whether generated replies are allowed.</summary>
        public bool allowGeneratedReplies;

        /// <summary>Android semantic action value.</summary>
        public int semanticAction;

        /// <summary>Whether this action is contextual.</summary>
        public bool contextual;

        /// <summary>Whether action should show UI when triggered.</summary>
        public bool showsUserInterface = true;
    }

    /// <summary>
    /// Wraps a notification payload with schedule options.
    /// </summary>
    [Serializable]
    public sealed class ScheduledNotificationEnvelopePayload
    {
        /// <summary>Notification content.</summary>
        public NotificationPayload notification = new NotificationPayload();

        /// <summary>Schedule configuration.</summary>
        public NotificationSchedulePayload schedule = new NotificationSchedulePayload();
    }

    /// <summary>
    /// Represents scheduling options for a notification.
    /// </summary>
    [Serializable]
    public sealed class NotificationSchedulePayload
    {
        /// <summary>Trigger time in Unix milliseconds.</summary>
        public long triggerAtMillis;

        /// <summary>Whether to schedule as an exact alarm.</summary>
        public bool exact = true;

        /// <summary>Whether alarm can fire while device is idle.</summary>
        public bool allowWhileIdle = true;

        /// <summary>Whether to restore this schedule after reboot.</summary>
        public bool persistAcrossBoot = true;

        /// <summary>Android alarm type value.</summary>
        public int alarmType;
    }

    /// <summary>
    /// Represents progress settings for foreground-service notifications.
    /// </summary>
    [Serializable]
    public sealed class NotificationProgressPayload
    {
        /// <summary>Maximum progress value.</summary>
        public int max;

        /// <summary>Current progress value.</summary>
        public int current;

        /// <summary>Whether progress is indeterminate.</summary>
        public bool indeterminate;
    }

    /// <summary>
    /// Represents style customization for notification rendering.
    /// </summary>
    [Serializable]
    public sealed class NotificationStylePayload
    {
        /// <summary>Style type name.</summary>
        public string type = "default";

        /// <summary>Expanded body text for big text style.</summary>
        public string? bigText;

        /// <summary>Summary text shown in expanded style.</summary>
        public string? summaryText;

        /// <summary>Expanded content title.</summary>
        public string? bigContentTitle;

        /// <summary>Line entries for inbox style.</summary>
        public string[] lines = Array.Empty<string>();

        /// <summary>Big picture resource reference.</summary>
        public NotificationResourcePayload? picture;

        /// <summary>Big picture URI string.</summary>
        public string? pictureUri;

        /// <summary>Optional large icon override for big picture style.</summary>
        public NotificationResourcePayload? largeIcon;

        /// <summary>Whether to hide expanded large icon.</summary>
        public bool hideExpandedLargeIcon;

        /// <summary>User display name for messaging style.</summary>
        public string? userDisplayName;

        /// <summary>Conversation title for messaging style.</summary>
        public string? conversationTitle;

        /// <summary>Whether messaging style represents a group conversation.</summary>
        public bool? isGroupConversation;

        /// <summary>Message entries for messaging style.</summary>
        public NotificationMessagePayload[] messages = Array.Empty<NotificationMessagePayload>();

        /// <summary>Custom collapsed layout resource name.</summary>
        public string? customViewLayout;

        /// <summary>Custom expanded layout resource name.</summary>
        public string? bigCustomViewLayout;

        /// <summary>Custom view action bindings.</summary>
        public NotificationViewActionPayload[] viewActions = Array.Empty<NotificationViewActionPayload>();
    }

    /// <summary>
    /// Represents a messaging-style notification entry.
    /// </summary>
    [Serializable]
    public sealed class NotificationMessagePayload
    {
        /// <summary>Message body text.</summary>
        public string text = string.Empty;

        /// <summary>Message timestamp in Unix milliseconds.</summary>
        public long? timestampMillis;

        /// <summary>Optional sender display name.</summary>
        public string? senderName;
    }

    /// <summary>
    /// Represents a custom key-value entry for notification data payload.
    /// </summary>
    [Serializable]
    public sealed class NotificationDataEntryPayload
    {
        /// <summary>Data key.</summary>
        public string key = string.Empty;

        /// <summary>Data value.</summary>
        public string value = string.Empty;
    }

    /// <summary>
    /// Represents an action binding for a specific custom notification view element.
    /// </summary>
    [Serializable]
    public sealed class NotificationViewActionPayload
    {
        /// <summary>Binding action type.</summary>
        public string type = string.Empty;

        /// <summary>Target Android view identifier.</summary>
        public string viewId = string.Empty;

        /// <summary>Action identifier triggered by the binding.</summary>
        public string? actionId;
    }
}
