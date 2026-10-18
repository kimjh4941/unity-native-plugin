#nullable enable

#if UNITY_ANDROID || UNITY_EDITOR
using UnityEngine.Scripting.APIUpdating;

namespace JonghyunKim.NativeToolkit.Runtime.Android.Notification
{
    /// <summary>
    /// Represents data reported when a notification is shown while the app is in foreground.
    /// </summary>
    [MovedFrom(false, sourceNamespace: "JonghyunKim.NativeToolkit.Runtime.Notification", sourceClassName: "NotificationReceivedResult")]
    public readonly struct AndroidNotificationReceivedResult
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

        internal AndroidNotificationReceivedResult(int notificationId, string? tag, string channelId)
        {
            NotificationId = notificationId;
            Tag = string.IsNullOrEmpty(tag) ? null : tag;
            ChannelId = channelId;
        }
    }
}
#endif
