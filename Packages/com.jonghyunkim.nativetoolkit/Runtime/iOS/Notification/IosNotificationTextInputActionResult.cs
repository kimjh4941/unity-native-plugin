#nullable enable

#if UNITY_IOS || UNITY_EDITOR
using System.Collections.Generic;

namespace JonghyunKim.NativeToolkit.Runtime.iOS.Notification
{
    /// <summary>
    /// Represents data delivered when a text input notification action is submitted on iOS.
    /// </summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "JonghyunKim.NativeToolkit.Runtime.Notification")]
    public readonly struct IosNotificationTextInputActionResult
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
        /// Gets the text entered by the user in the text input field.
        /// </summary>
        public string UserText { get; }

        /// <summary>
        /// Gets optional flat key-value user info data attached to the notification.
        /// </summary>
        public IReadOnlyDictionary<string, string>? UserInfo { get; }

        internal IosNotificationTextInputActionResult(string notificationId, string actionId, string userText, string? userInfoJson)
        {
            NotificationId = notificationId;
            ActionId = actionId;
            UserText = userText;
            UserInfo = IosNotificationUserInfo.ParseFlat(userInfoJson);
        }
    }
}
#endif
