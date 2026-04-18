#nullable enable

#if UNITY_ANDROID
namespace JonghyunKim.NativeToolkit.Runtime.Notification
{
    public readonly struct NotificationActionResult
    {
        public string ActionId { get; }
        public int NotificationId { get; }

        internal NotificationActionResult(string actionId, int notificationId)
        {
            ActionId = actionId;
            NotificationId = notificationId;
        }
    }
}
#endif
