#nullable enable

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace JonghyunKim.NativeToolkit.Runtime.Notification
{
    /// <summary>
    /// Builds JSON strings for Windows notification APIs.
    /// Enforces payload validation constraints before serialization.
    /// All output matches the schema expected by the native WindowsNotificationManager.
    /// </summary>
    public static class WindowsNotificationJsonBuilder
    {
        /// <summary>
        /// Validates a <see cref="WindowsNotificationPayload"/> against all constraints.
        /// </summary>
        /// <param name="payload">The payload to validate.</param>
        /// <returns>An error message string if validation fails; null if the payload is valid.</returns>
        public static string? Validate(WindowsNotificationPayload payload)
        {
            if (payload.Buttons != null && payload.Buttons.Count > 5)
                return "buttons count exceeds 5";

            if (payload.Audio != null && payload.Audio.Loop && payload.Duration != "long")
                return "audio.loop requires duration=long";

            if (payload.Buttons != null)
            {
                foreach (var button in payload.Buttons)
                {
                    if (button.Args != null && button.InvokeUri != null)
                        return "button cannot have both args and invokeUri";
                }
            }

            return null;
        }

        /// <summary>
        /// Builds the JSON payload string from a <see cref="WindowsNotificationPayload"/>.
        /// </summary>
        /// <param name="payload">The notification payload.</param>
        /// <returns>A JSON string matching the native WindowsNotificationManager schema.</returns>
        /// <exception cref="System.ArgumentException">Thrown when payload fails validation.</exception>
        public static string BuildNotificationPayload(WindowsNotificationPayload payload)
        {
            var error = Validate(payload);
            if (error != null)
                throw new System.ArgumentException(error, nameof(payload));

            var obj = new Dictionary<string, object?>();

            AddIfNotNullOrWhiteSpace(obj, "title", payload.Title);
            AddIfNotNullOrWhiteSpace(obj, "body", payload.Body);
            AddIfNotNullOrWhiteSpace(obj, "tag", payload.Tag);
            AddIfNotNullOrWhiteSpace(obj, "group", payload.Group);
            AddIfNotNullOrWhiteSpace(obj, "scenario", payload.Scenario);
            AddIfNotNullOrWhiteSpace(obj, "duration", payload.Duration);
            AddIfNotNullOrWhiteSpace(obj, "attribution", payload.Attribution);

            if (payload.Expiration.HasValue)
                obj["expiration"] = payload.Expiration.Value;
            if (payload.ExpiresOnReboot.HasValue)
                obj["expiresOnReboot"] = payload.ExpiresOnReboot.Value;
            if (payload.Timestamp.HasValue)
                obj["timestamp"] = payload.Timestamp.Value;

            if (payload.Buttons != null && payload.Buttons.Count > 0)
                obj["buttons"] = BuildButtonList(payload.Buttons);

            if (payload.TextBoxes != null && payload.TextBoxes.Count > 0)
                obj["textBoxes"] = BuildTextBoxList(payload.TextBoxes);

            if (payload.Audio != null)
                obj["audio"] = BuildAudioObject(payload.Audio);

            if (payload.Progress != null)
                obj["progress"] = BuildProgressObject(payload.Progress);

            return SerializeObject(obj);
        }

        private static List<object?> BuildButtonList(List<WindowsNotificationButtonPayload> buttons)
        {
            var list = new List<object?>();
            foreach (var button in buttons)
            {
                var obj = new Dictionary<string, object?> { ["label"] = button.Label };

                if (button.Args != null)
                {
                    var argsObj = new Dictionary<string, object?>();
                    foreach (var kv in button.Args)
                        argsObj[kv.Key] = kv.Value;
                    obj["args"] = argsObj;
                }
                else if (button.InvokeUri != null)
                {
                    obj["invokeUri"] = button.InvokeUri;
                }

                list.Add(obj);
            }
            return list;
        }

        private static List<object?> BuildTextBoxList(List<WindowsNotificationTextBoxPayload> textBoxes)
        {
            var list = new List<object?>();
            foreach (var tb in textBoxes)
            {
                var obj = new Dictionary<string, object?> { ["id"] = tb.Id };
                AddIfNotNullOrWhiteSpace(obj, "placeholder", tb.Placeholder);
                AddIfNotNullOrWhiteSpace(obj, "title", tb.Title);
                list.Add(obj);
            }
            return list;
        }

        private static Dictionary<string, object?> BuildAudioObject(WindowsNotificationAudioPayload audio)
        {
            var obj = new Dictionary<string, object?> { ["loop"] = audio.Loop };
            AddIfNotNullOrWhiteSpace(obj, "src", audio.Src);
            return obj;
        }

        private static Dictionary<string, object?> BuildProgressObject(WindowsNotificationProgressPayload progress)
        {
            var obj = new Dictionary<string, object?> { ["value"] = progress.Value };
            AddIfNotNullOrWhiteSpace(obj, "valueStr", progress.ValueStr);
            AddIfNotNullOrWhiteSpace(obj, "status", progress.Status);
            return obj;
        }

        private static void AddIfNotNullOrWhiteSpace(Dictionary<string, object?> target, string key, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                target[key] = value;
        }

        private static string SerializeObject(Dictionary<string, object?> obj)
        {
            var builder = new StringBuilder();
            AppendValue(builder, obj);
            return builder.ToString();
        }

        private static void AppendValue(StringBuilder builder, object? value)
        {
            switch (value)
            {
                case null:
                    builder.Append("null");
                    break;
                case string stringValue:
                    AppendEscapedString(builder, stringValue);
                    break;
                case bool boolValue:
                    builder.Append(boolValue ? "true" : "false");
                    break;
                case int intValue:
                    builder.Append(intValue.ToString(CultureInfo.InvariantCulture));
                    break;
                case long longValue:
                    builder.Append(longValue.ToString(CultureInfo.InvariantCulture));
                    break;
                case double doubleValue:
                    builder.Append(doubleValue.ToString(CultureInfo.InvariantCulture));
                    break;
                case Dictionary<string, object?> dictionaryValue:
                    AppendObject(builder, dictionaryValue);
                    break;
                case List<object?> listValue:
                    AppendArray(builder, listValue);
                    break;
                default:
                    AppendEscapedString(builder, value.ToString() ?? string.Empty);
                    break;
            }
        }

        private static void AppendObject(StringBuilder builder, Dictionary<string, object?> obj)
        {
            builder.Append('{');
            bool first = true;
            foreach (var pair in obj)
            {
                if (!first) builder.Append(',');
                first = false;
                AppendEscapedString(builder, pair.Key);
                builder.Append(':');
                AppendValue(builder, pair.Value);
            }
            builder.Append('}');
        }

        private static void AppendArray(StringBuilder builder, List<object?> values)
        {
            builder.Append('[');
            for (int i = 0; i < values.Count; i++)
            {
                if (i > 0) builder.Append(',');
                AppendValue(builder, values[i]);
            }
            builder.Append(']');
        }

        private static void AppendEscapedString(StringBuilder builder, string value)
        {
            builder.Append('"');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"':  builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\b': builder.Append("\\b");  break;
                    case '\f': builder.Append("\\f");  break;
                    case '\n': builder.Append("\\n");  break;
                    case '\r': builder.Append("\\r");  break;
                    case '\t': builder.Append("\\t");  break;
                    default:
                        if (c < 0x20)
                            builder.Append($"\\u{(int)c:x4}");
                        else
                            builder.Append(c);
                        break;
                }
            }
            builder.Append('"');
        }
    }
}
#endif
