#nullable enable

#if UNITY_IOS || UNITY_EDITOR
namespace JonghyunKim.NativeToolkit.Runtime.iOS.Notification
{
    using System.Collections.Generic;

    /// <summary>
    /// Reads the top-level string members of a notification's userInfo JSON for the event results, as 1.x did: keys
    /// and values are taken as written, without undoing escapes. A member whose value is not a string is skipped and
    /// the reading goes on (artifact/ios/notification, C ABI design v4, D-12): 1.x stopped there, and since the C ABI
    /// sorts the keys, stopping would change which keys remain. Null when no string member is found.
    /// </summary>
    internal static class IosNotificationUserInfo
    {
        internal static IReadOnlyDictionary<string, string>? ParseFlat(string? json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            json = json!.Trim();
            if (json.Length < 2 || json[0] != '{' || json[json.Length - 1] != '}') return null;

            var result = new Dictionary<string, string>();
            int i = 1;
            int end = json.Length - 1;
            while (i < end)
            {
                while (i < end && json[i] != '"') i++;
                if (i >= end) break;

                string key = ReadQuotedString(json, ref i);

                while (i < end && json[i] != ':') i++;
                i++;
                while (i < end && char.IsWhiteSpace(json[i])) i++;
                if (i >= end) break;

                if (json[i] == '"')
                {
                    result[key] = ReadQuotedString(json, ref i);
                }
                else
                {
                    SkipValue(json, ref i, end);
                }

                while (i < end && json[i] != ',') i++;
                if (i < end) i++;
            }

            return result.Count > 0 ? result : null;
        }

        private static string ReadQuotedString(string json, ref int i)
        {
            i++; // skip the opening quote
            int start = i;
            while (i < json.Length && json[i] != '"')
            {
                if (json[i] == '\\') i++;
                i++;
            }
            string result = json.Substring(start, i - start);
            if (i < json.Length) i++; // skip the closing quote
            return result;
        }

        /// <summary>Skips a value that is not a string (a number, true, false, null, an object or an array).</summary>
        private static void SkipValue(string json, ref int i, int end)
        {
            int depth = 0;
            while (i < end)
            {
                char c = json[i];
                if (c == '"')
                {
                    ReadQuotedString(json, ref i);
                    continue;
                }
                if (c == '{' || c == '[') depth++;
                else if (c == '}' || c == ']') depth--;
                else if (c == ',' && depth == 0) return;
                i++;
            }
        }
    }
}
#endif
