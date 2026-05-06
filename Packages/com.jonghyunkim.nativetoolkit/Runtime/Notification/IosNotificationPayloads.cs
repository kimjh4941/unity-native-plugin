#nullable enable

using System;

namespace JonghyunKim.NativeToolkit.Runtime.Notification
{
    /// <summary>
    /// Represents the content of an iOS notification.
    /// </summary>
    [Serializable]
    public sealed class NotificationContentPayload
    {
        /// <summary>Unique identifier for the notification.</summary>
        public string id = string.Empty;

        /// <summary>Title text of the notification.</summary>
        public string title = string.Empty;

        /// <summary>Optional subtitle text shown below the title.</summary>
        public string? subtitle;

        /// <summary>Optional body text of the notification.</summary>
        public string? body;

        /// <summary>Optional badge count to display on the app icon. Omitted when null.</summary>
        public int? badge;

        /// <summary>Optional sound name. Use "default", "defaultCritical", or a custom sound file name.</summary>
        public string? sound;

        /// <summary>Optional identifier of a registered category that provides action buttons.</summary>
        public string? categoryIdentifier;

        /// <summary>Optional interruption level. One of: "passive", "active", "timeSensitive", "critical".</summary>
        public string? interruptionLevel;

        /// <summary>Optional thread identifier for visually grouping related notifications.</summary>
        public string? threadIdentifier;

        /// <summary>Optional target content identifier used to route the notification to a specific scene.</summary>
        public string? targetContentIdentifier;

        /// <summary>Optional relevance score between 0.0 and 1.0 that determines notification summary ordering.</summary>
        public double? relevanceScore;

        /// <summary>Optional filter criteria used to group notifications in the summary.</summary>
        public string? filterCriteria;

        /// <summary>Optional list of media attachments associated with the notification.</summary>
        public NotificationAttachmentPayload[] attachments = Array.Empty<NotificationAttachmentPayload>();
    }

    /// <summary>
    /// Represents a media attachment for an iOS notification.
    /// </summary>
    [Serializable]
    public sealed class NotificationAttachmentPayload
    {
        /// <summary>Unique identifier for the attachment.</summary>
        public string identifier = string.Empty;

        /// <summary>File URL pointing to the attachment resource.</summary>
        public string fileURL = string.Empty;
    }

    /// <summary>
    /// Trigger that fires a notification after a specified time interval elapses.
    /// </summary>
    [Serializable]
    public sealed class TimeIntervalTriggerPayload
    {
        /// <summary>Number of seconds until the notification fires. Must be greater than zero.</summary>
        public double interval = 1.0;

        /// <summary>Whether the trigger repeats indefinitely after each interval.</summary>
        public bool repeats = false;
    }

    /// <summary>
    /// Trigger that fires a notification at a specific calendar date and time.
    /// </summary>
    [Serializable]
    public sealed class CalendarTriggerPayload
    {
        /// <summary>Optional year component of the trigger date.</summary>
        public int? year;

        /// <summary>Optional month component of the trigger date (1–12).</summary>
        public int? month;

        /// <summary>Optional day component of the trigger date (1–31).</summary>
        public int? day;

        /// <summary>Optional hour component of the trigger time (0–23).</summary>
        public int? hour;

        /// <summary>Optional minute component of the trigger time (0–59).</summary>
        public int? minute;

        /// <summary>Optional second component of the trigger time (0–59).</summary>
        public int? second;

        /// <summary>Whether the trigger repeats on the matching calendar components.</summary>
        public bool repeats = false;
    }

    /// <summary>
    /// Trigger that fires a notification when the device enters or exits a geographic region.
    /// </summary>
    [Serializable]
    public sealed class LocationTriggerPayload
    {
        /// <summary>Unique identifier for the geographic region.</summary>
        public string identifier = string.Empty;

        /// <summary>Latitude of the center of the monitored region.</summary>
        public double latitude = 0.0;

        /// <summary>Longitude of the center of the monitored region.</summary>
        public double longitude = 0.0;

        /// <summary>Radius of the monitored region in meters.</summary>
        public double radius = 100.0;

        /// <summary>Whether to deliver the notification when the device enters the region.</summary>
        public bool notifyOnEntry = true;

        /// <summary>Whether to deliver the notification when the device exits the region.</summary>
        public bool notifyOnExit = false;
    }

    /// <summary>
    /// Represents an iOS notification category that groups related action buttons.
    /// </summary>
    [Serializable]
    public sealed class IosNotificationCategoryPayload
    {
        /// <summary>Unique identifier for the category. Must match the <c>categoryIdentifier</c> in content.</summary>
        public string identifier = string.Empty;

        /// <summary>Standard action buttons associated with this category.</summary>
        public IosNotificationActionPayload[] actions = Array.Empty<IosNotificationActionPayload>();

        /// <summary>Text input action buttons associated with this category.</summary>
        public IosNotificationTextInputActionPayload[] textInputActions = Array.Empty<IosNotificationTextInputActionPayload>();

        /// <summary>
        /// Category behavior options. Valid values: "customDismissAction", "allowInCarPlay",
        /// "hiddenPreviewsShowTitle", "allowAnnouncement".
        /// </summary>
        public string[] options = Array.Empty<string>();
    }

    /// <summary>
    /// Represents a standard action button in an iOS notification category.
    /// </summary>
    [Serializable]
    public sealed class IosNotificationActionPayload
    {
        /// <summary>Unique identifier for the action.</summary>
        public string identifier = string.Empty;

        /// <summary>Display title shown on the action button.</summary>
        public string title = string.Empty;

        /// <summary>Optional SF Symbol name used as the action button icon (iOS 15+).</summary>
        public string? sfSymbolName;

        /// <summary>
        /// Action behavior options. Valid values: "authenticationRequired", "destructive", "foreground".
        /// </summary>
        public string[] options = Array.Empty<string>();
    }

    /// <summary>
    /// Represents a text input action button in an iOS notification category.
    /// </summary>
    [Serializable]
    public sealed class IosNotificationTextInputActionPayload
    {
        /// <summary>Unique identifier for the action.</summary>
        public string identifier = string.Empty;

        /// <summary>Display title shown on the action button.</summary>
        public string title = string.Empty;

        /// <summary>Text displayed on the send button of the inline text input field.</summary>
        public string buttonTitle = string.Empty;

        /// <summary>Placeholder text shown in the text input field before the user types.</summary>
        public string textInputPlaceholder = string.Empty;
    }
}
