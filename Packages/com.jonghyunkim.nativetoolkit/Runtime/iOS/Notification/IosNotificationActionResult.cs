#nullable enable

#if UNITY_IOS || UNITY_EDITOR
using System.Collections.Generic;

namespace JonghyunKim.NativeToolkit.Runtime.iOS.Notification
{
    /// <summary>
    /// Represents data delivered when a notification action button is tapped on iOS.
    /// </summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "JonghyunKim.NativeToolkit.Runtime.Notification")]
    public readonly struct IosNotificationActionResult
    {
        /// <summary>
        /// Gets the identifier of the notification that was acted upon.
        /// </summary>
        public string NotificationId { get; }

        /// <summary>
        /// Gets the identifier of the tapped action.
        /// </summary>
        public string ActionId { get; }

        /// <summary>
        /// Gets optional flat key-value user info data attached to the notification.
        /// </summary>
        public IReadOnlyDictionary<string, string>? UserInfo { get; }

        internal IosNotificationActionResult(string notificationId, string actionId, string? userInfoJson)
        {
            NotificationId = notificationId;
            ActionId = actionId;
            UserInfo = IosNotificationUserInfo.ParseFlat(userInfoJson);
        }
    }
}
#endif
