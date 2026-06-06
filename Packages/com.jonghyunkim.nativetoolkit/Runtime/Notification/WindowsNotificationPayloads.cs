#nullable enable

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
using System.Collections.Generic;

namespace JonghyunKim.NativeToolkit.Runtime.Notification
{
    /// <summary>
    /// Notification permission setting values returned by getNotificationSetting.
    /// This is a special synchronous API that does not use the WindowsNotificationResult contract.
    /// </summary>
    public enum WindowsNotificationSetting
    {
        /// <summary>Notifications are enabled.</summary>
        Enabled                = 0,
        /// <summary>Notifications are disabled by application settings.</summary>
        DisabledForApplication = 1,
        /// <summary>Notifications are disabled by user settings.</summary>
        DisabledForUser        = 2,
        /// <summary>Notifications are disabled by group policy.</summary>
        DisabledByGroupPolicy  = 3,
        /// <summary>Notifications are disabled by manifest.</summary>
        DisabledByManifest     = 4,
        /// <summary>Failed to retrieve setting (WinRT exception).</summary>
        Unknown                = -1
    }

    /// <summary>
    /// Badge glyph values for setBadge. Values greater than zero set a numeric badge.
    /// </summary>
    public enum WindowsBadgeValue
    {
        /// <summary>Clears the badge.</summary>
        Clear      = 0,
        /// <summary>Alert glyph.</summary>
        Alert      = -1,
        /// <summary>Activity glyph.</summary>
        Activity   = -2,
        /// <summary>New message glyph.</summary>
        NewMessage = -3,
        /// <summary>Available glyph.</summary>
        Available  = -4,
        /// <summary>Busy glyph.</summary>
        Busy       = -5,
        /// <summary>Away glyph.</summary>
        Away       = -6
    }

    /// <summary>
    /// Payload for a Windows Toast notification button.
    /// Exactly one of <see cref="Args"/> or <see cref="InvokeUri"/> must be set.
    /// </summary>
    public sealed class WindowsNotificationButtonPayload
    {
        /// <summary>Gets or sets the button label text.</summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>Gets or sets the activation arguments map. Mutually exclusive with <see cref="InvokeUri"/>.</summary>
        public Dictionary<string, string>? Args { get; set; }

        /// <summary>Gets or sets the URI to invoke on button press. Mutually exclusive with <see cref="Args"/>.</summary>
        public string? InvokeUri { get; set; }
    }

    /// <summary>
    /// Payload for a Windows Toast text input box.
    /// </summary>
    public sealed class WindowsNotificationTextBoxPayload
    {
        /// <summary>Gets or sets the unique input element identifier.</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>Gets or sets the placeholder text shown when the input is empty.</summary>
        public string? Placeholder { get; set; }

        /// <summary>Gets or sets the label shown above the input.</summary>
        public string? Title { get; set; }
    }

    /// <summary>
    /// Payload for a Windows Toast audio configuration.
    /// </summary>
    public sealed class WindowsNotificationAudioPayload
    {
        /// <summary>Gets or sets the audio source URI (e.g., "ms-winsoundevent:Notification.Default").</summary>
        public string? Src { get; set; }

        /// <summary>Gets or sets whether to loop the audio. Requires <see cref="WindowsNotificationPayload.Duration"/> = "long".</summary>
        public bool Loop { get; set; }
    }

    /// <summary>
    /// Payload for a Windows Toast progress bar.
    /// </summary>
    public sealed class WindowsNotificationProgressPayload
    {
        /// <summary>Gets or sets the progress value between 0.0 and 1.0.</summary>
        public double Value { get; set; }

        /// <summary>Gets or sets the human-readable value string (e.g., "50%").</summary>
        public string? ValueStr { get; set; }

        /// <summary>Gets or sets the status label text.</summary>
        public string? Status { get; set; }
    }

    /// <summary>
    /// Top-level payload for showNotification and scheduleNotification.
    /// Pass to <see cref="WindowsNotificationJsonBuilder.BuildNotificationPayload"/> to produce the JSON string.
    /// </summary>
    public sealed class WindowsNotificationPayload
    {
        /// <summary>Gets or sets the notification title.</summary>
        public string? Title { get; set; }

        /// <summary>Gets or sets the notification body text.</summary>
        public string? Body { get; set; }

        /// <summary>Gets or sets the tag used to identify and deduplicate notifications.</summary>
        public string? Tag { get; set; }

        /// <summary>Gets or sets the group used to identify and deduplicate notifications.</summary>
        public string? Group { get; set; }

        /// <summary>Gets or sets the scenario (e.g., "reminder", "alarm", "urgent", "incomingCall").</summary>
        public string? Scenario { get; set; }

        /// <summary>Gets or sets the duration ("long" required when audio loop is true).</summary>
        public string? Duration { get; set; }

        /// <summary>Gets or sets the expiration time in seconds.</summary>
        public int? Expiration { get; set; }

        /// <summary>Gets or sets whether the notification expires on reboot.</summary>
        public bool? ExpiresOnReboot { get; set; }

        /// <summary>Gets or sets the Unix timestamp to display in the notification.</summary>
        public long? Timestamp { get; set; }

        /// <summary>Gets or sets the attribution text shown below the notification.</summary>
        public string? Attribution { get; set; }

        /// <summary>Gets or sets the action buttons. Maximum 5 buttons.</summary>
        public List<WindowsNotificationButtonPayload>? Buttons { get; set; }

        /// <summary>Gets or sets the text input boxes.</summary>
        public List<WindowsNotificationTextBoxPayload>? TextBoxes { get; set; }

        /// <summary>Gets or sets the audio configuration.</summary>
        public WindowsNotificationAudioPayload? Audio { get; set; }

        /// <summary>Gets or sets the progress bar configuration.</summary>
        public WindowsNotificationProgressPayload? Progress { get; set; }
    }
}
#endif
