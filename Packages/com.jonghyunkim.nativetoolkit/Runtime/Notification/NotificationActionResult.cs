#nullable enable

#if UNITY_ANDROID
using System.Collections.Generic;

namespace JonghyunKim.NativeToolkit.Runtime.Notification
{
    public readonly struct NotificationActionResult
    {
        public string ActionId { get; }
        public int NotificationId { get; }
        public IReadOnlyDictionary<string, string>? Data { get; }

        internal NotificationActionResult(string actionId, int notificationId, string? dataJson)
        {
            ActionId = actionId;
            NotificationId = notificationId;
            Data = ParseFlatJsonObject(dataJson);
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
