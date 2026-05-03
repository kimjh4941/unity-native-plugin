#nullable enable

#if UNITY_ANDROID
namespace JonghyunKim.NativeToolkit.Runtime.Notification
{
    /// <summary>
    /// Represents data reported when a notification is shown while the app is in foreground.
    /// </summary>
    public readonly struct NotificationReceivedResult
    {
        /// <summary>
        /// Gets the shown notification identifier.
        /// </summary>
        public int NotificationId { get; }

        /// <summary>
        /// Gets the optional notification tag.
        /// </summary>
        public string? Tag { get; }

        /// <summary>
        /// Gets the channel identifier used to display the notification.
        /// </summary>
        public string ChannelId { get; }

        internal NotificationReceivedResult(int notificationId, string? tag, string channelId)
        {
            NotificationId = notificationId;
            Tag = string.IsNullOrEmpty(tag) ? null : tag;
            ChannelId = channelId;
        }
    }
}
#endif
