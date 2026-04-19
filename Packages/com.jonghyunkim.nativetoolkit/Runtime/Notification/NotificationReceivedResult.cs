#nullable enable

#if UNITY_ANDROID
namespace JonghyunKim.NativeToolkit.Runtime.Notification
{
    public readonly struct NotificationReceivedResult
    {
        public int NotificationId { get; }
        public string? Tag { get; }
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
