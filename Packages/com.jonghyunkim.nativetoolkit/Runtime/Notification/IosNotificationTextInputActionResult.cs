#nullable enable

#if UNITY_IOS
using System.Collections.Generic;

namespace JonghyunKim.NativeToolkit.Runtime.Notification
{
    /// <summary>
    /// Represents data delivered when a text input notification action is submitted on iOS.
    /// </summary>
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
            UserInfo = ParseFlatJsonObject(userInfoJson);
        }

        private static IReadOnlyDictionary<string, string>? ParseFlatJsonObject(string? json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            json = json.Trim();
            if (json.Length < 2 || json[0] != '{' || json[json.Length - 1] != '}') return null;

            var result = new Dictionary<string, string>();
            int i = 1;
            while (i < json.Length - 1)
            {
                while (i < json.Length - 1 && json[i] != '"') i++;
                if (i >= json.Length - 1) break;

                string key = ReadQuotedString(json, ref i);

                while (i < json.Length && json[i] != ':') i++;
                i++;
                while (i < json.Length && (json[i] == ' ' || json[i] == '\t' || json[i] == '\r' || json[i] == '\n')) i++;

                if (i >= json.Length || json[i] != '"') break;

                string value = ReadQuotedString(json, ref i);
                result[key] = value;

                while (i < json.Length && json[i] != ',' && json[i] != '}') i++;
                if (i < json.Length && json[i] == ',') i++;
            }

            return result.Count > 0 ? result : null;
        }

        private static string ReadQuotedString(string json, ref int i)
        {
            i++; // skip opening "
            int start = i;
            while (i < json.Length && json[i] != '"')
            {
                if (json[i] == '\\') i++;
                i++;
            }
            string result = json.Substring(start, i - start);
            if (i < json.Length) i++; // skip closing "
            return result;
        }
    }
}
#endif
