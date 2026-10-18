#nullable enable

using System;
using UnityEngine.Scripting.APIUpdating;

namespace JonghyunKim.NativeToolkit.Runtime.Android.Notification
{
    /// <summary>
    /// Represents an Android resource reference by name and optional type.
    /// </summary>
    [Serializable]
    [MovedFrom(false, sourceNamespace: "JonghyunKim.NativeToolkit.Runtime.Notification", sourceClassName: "NotificationResourcePayload")]
    public sealed class AndroidNotificationResourcePayload
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
    [MovedFrom(false, sourceNamespace: "JonghyunKim.NativeToolkit.Runtime.Notification", sourceClassName: "ChannelPayload")]
    public sealed class AndroidChannelPayload
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
    [MovedFrom(false, sourceNamespace: "JonghyunKim.NativeToolkit.Runtime.Notification", sourceClassName: "NotificationPayload")]
    public class AndroidNotificationPayload
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
        public AndroidChannelPayload channel = new AndroidChannelPayload();

        /// <summary>Optional small icon resource.</summary>
        public AndroidNotificationResourcePayload? smallIcon;

        /// <summary>Optional large icon resource.</summary>
        public AndroidNotificationResourcePayload? largeIcon;

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
        public AndroidNotificationProgressPayload? progress;

        /// <summary>Optional style configuration.</summary>
        public AndroidNotificationStylePayload? style;

        /// <summary>Whether tapping the body should launch app UI.</summary>
        public bool launchAppOnTap = true;

        /// <summary>Optional custom launch action delivered on body tap.</summary>
        public string? launchAction;

        /// <summary>Whether to use full-screen intent.</summary>
        public bool fullScreenIntent;

        /// <summary>Optional action buttons.</summary>
        public AndroidNotificationActionPayload[] actions = Array.Empty<AndroidNotificationActionPayload>();

        /// <summary>
        /// Optional custom key-value payload.
        /// JsonUtility cannot serialize dictionary directly; for dynamic keys inject data manually.
        /// </summary>
        public AndroidNotificationDataEntryPayload[] data = Array.Empty<AndroidNotificationDataEntryPayload>();
    }

    /// <summary>
    /// Represents an action button displayed in a notification.
    /// </summary>
    [Serializable]
    [MovedFrom(false, sourceNamespace: "JonghyunKim.NativeToolkit.Runtime.Notification", sourceClassName: "NotificationActionPayload")]
    public sealed class AndroidNotificationActionPayload
    {
        /// <summary>Action button label.</summary>
        public string title = string.Empty;

        /// <summary>Action identifier returned when tapped.</summary>
        public string actionId = string.Empty;

        /// <summary>Optional action icon resource.</summary>
        public AndroidNotificationResourcePayload? icon;

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
    [MovedFrom(false, sourceNamespace: "JonghyunKim.NativeToolkit.Runtime.Notification", sourceClassName: "ScheduledNotificationEnvelopePayload")]
    public sealed class AndroidScheduledNotificationEnvelopePayload
    {
        /// <summary>Notification content.</summary>
        public AndroidNotificationPayload notification = new AndroidNotificationPayload();

        /// <summary>Schedule configuration.</summary>
        public AndroidNotificationSchedulePayload schedule = new AndroidNotificationSchedulePayload();
    }

    /// <summary>
    /// Represents scheduling options for a notification.
    /// </summary>
    [Serializable]
    [MovedFrom(false, sourceNamespace: "JonghyunKim.NativeToolkit.Runtime.Notification", sourceClassName: "NotificationSchedulePayload")]
    public sealed class AndroidNotificationSchedulePayload
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
    [MovedFrom(false, sourceNamespace: "JonghyunKim.NativeToolkit.Runtime.Notification", sourceClassName: "NotificationProgressPayload")]
    public sealed class AndroidNotificationProgressPayload
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
    [MovedFrom(false, sourceNamespace: "JonghyunKim.NativeToolkit.Runtime.Notification", sourceClassName: "NotificationStylePayload")]
    public sealed class AndroidNotificationStylePayload
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
        public AndroidNotificationResourcePayload? picture;

        /// <summary>Big picture URI string.</summary>
        public string? pictureUri;

        /// <summary>Optional large icon override for big picture style.</summary>
        public AndroidNotificationResourcePayload? largeIcon;

        /// <summary>Whether to hide expanded large icon.</summary>
        public bool hideExpandedLargeIcon;

        /// <summary>User display name for messaging style.</summary>
        public string? userDisplayName;

        /// <summary>Conversation title for messaging style.</summary>
        public string? conversationTitle;

        /// <summary>Whether messaging style represents a group conversation.</summary>
        public bool? isGroupConversation;

        /// <summary>Message entries for messaging style.</summary>
        public AndroidNotificationMessagePayload[] messages = Array.Empty<AndroidNotificationMessagePayload>();

        /// <summary>Custom collapsed layout resource name.</summary>
        public string? customViewLayout;

        /// <summary>Custom expanded layout resource name.</summary>
        public string? bigCustomViewLayout;

        /// <summary>Custom view action bindings.</summary>
        public AndroidNotificationViewActionPayload[] viewActions = Array.Empty<AndroidNotificationViewActionPayload>();
    }

    /// <summary>
    /// Represents a messaging-style notification entry.
    /// </summary>
    [Serializable]
    [MovedFrom(false, sourceNamespace: "JonghyunKim.NativeToolkit.Runtime.Notification", sourceClassName: "NotificationMessagePayload")]
    public sealed class AndroidNotificationMessagePayload
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
    [MovedFrom(false, sourceNamespace: "JonghyunKim.NativeToolkit.Runtime.Notification", sourceClassName: "NotificationDataEntryPayload")]
    public sealed class AndroidNotificationDataEntryPayload
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
    [MovedFrom(false, sourceNamespace: "JonghyunKim.NativeToolkit.Runtime.Notification", sourceClassName: "NotificationViewActionPayload")]
    public sealed class AndroidNotificationViewActionPayload
    {
        /// <summary>Binding action type.</summary>
        public string type = string.Empty;

        /// <summary>Target Android view identifier.</summary>
        public string viewId = string.Empty;

        /// <summary>Action identifier triggered by the binding.</summary>
        public string? actionId;
    }
}
