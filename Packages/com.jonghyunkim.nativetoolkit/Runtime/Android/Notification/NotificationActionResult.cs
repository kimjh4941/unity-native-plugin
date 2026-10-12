#nullable enable

#if UNITY_ANDROID || UNITY_EDITOR
using System.Collections.Generic;

namespace JonghyunKim.NativeToolkit.Runtime.Android.Notification
{
    /// <summary>
    /// Represents data delivered when a notification action is tapped.
    /// </summary>
    public readonly struct NotificationActionResult
    {
        /// <summary>
        /// Gets the tapped action identifier.
        /// </summary>
        public string ActionId { get; }

        /// <summary>
        /// Gets the notification identifier associated with the action.
        /// </summary>
        public int NotificationId { get; }

        /// <summary>
        /// Gets optional flat key-value payload data attached to the action.
        /// </summary>
        public IReadOnlyDictionary<string, string>? Data { get; }

        /// <summary>
        /// A result from what the native event carried: its action ID, the notification's ID, and the
        /// data entries as read (no escaping is left in the values; design J-N18). No entries is null,
        /// as in 1.x. A key written twice keeps its last value.
        /// </summary>
        internal NotificationActionResult(string actionId, int notificationId, IReadOnlyList<KeyValuePair<string, string>> data)
        {
            ActionId = actionId;
            NotificationId = notificationId;
            if (data.Count == 0)
            {
                Data = null;
            }
            else
            {
                var values = new Dictionary<string, string>();
                foreach (KeyValuePair<string, string> entry in data)
                    values[entry.Key] = entry.Value;
                Data = values;
            }
        }
    }
}
#endif
