#nullable enable

using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace JonghyunKim.NativeToolkit.Runtime.Share
{
    /// <summary>
    /// Builds JSON strings for Android share APIs with full control over optional fields.
    /// Does not use external JSON libraries; matches the hand-written serializer pattern used by
    /// <c>AndroidNotificationJsonBuilder</c>.
    /// </summary>
    public static class AndroidShareJsonBuilder
    {
        /// <summary>
        /// Builds JSON for the <c>shareText</c> and <c>shareWithCallback</c> native operations.
        /// </summary>
        /// <param name="payload">Text share payload to serialize.</param>
        /// <returns>JSON string matching the <c>UnityShareJsonParser</c> shareText schema.</returns>
        public static string BuildShareTextJson(ShareTextPayload payload)
        {
            var obj = new Dictionary<string, object?> { ["text"] = payload.text };
            AddIfNotNullOrWhiteSpace(obj, "title", payload.title);
            AddIfNotNullOrWhiteSpace(obj, "subject", payload.subject);
            AddIfNotNullOrWhiteSpace(obj, "mimeType", payload.mimeType);
            AddIfNotNullOrWhiteSpace(obj, "previewTitle", payload.previewTitle);
            AddIfNotNullOrWhiteSpace(obj, "previewThumbnailPath", payload.previewThumbnailPath);
            AddChooserActions(obj, payload.chooserActions);
            return SerializeObject(obj);
        }

        /// <summary>
        /// Builds JSON for the <c>shareImage</c> native operation.
        /// </summary>
        /// <param name="payload">Image share payload to serialize.</param>
        /// <returns>JSON string matching the <c>UnityShareJsonParser</c> shareImage schema.</returns>
        public static string BuildShareImageJson(ShareImagePayload payload)
        {
            var obj = new Dictionary<string, object?> { ["filePath"] = payload.filePath };
            AddIfNotNullOrWhiteSpace(obj, "mimeType", payload.mimeType);
            return SerializeObject(obj);
        }

        /// <summary>
        /// Builds JSON for the <c>shareImages</c> native operation.
        /// </summary>
        /// <param name="payload">Multiple-image share payload to serialize.</param>
        /// <returns>JSON string matching the <c>UnityShareJsonParser</c> shareImages schema.</returns>
        public static string BuildShareImagesJson(ShareImagesPayload payload)
        {
            var obj = new Dictionary<string, object?> { ["filePaths"] = BuildStringList(payload.filePaths) };
            return SerializeObject(obj);
        }

        /// <summary>
        /// Builds JSON for the <c>shareFile</c> native operation.
        /// </summary>
        /// <param name="payload">File share payload to serialize.</param>
        /// <returns>JSON string matching the <c>UnityShareJsonParser</c> shareFile schema.</returns>
        public static string BuildShareFileJson(ShareFilePayload payload)
        {
            return SerializeObject(new Dictionary<string, object?> { ["filePath"] = payload.filePath });
        }

        /// <summary>
        /// Builds JSON for the <c>shareFiles</c> native operation.
        /// </summary>
        /// <param name="payload">Multiple-file share payload to serialize.</param>
        /// <returns>JSON string matching the <c>UnityShareJsonParser</c> shareFiles schema.</returns>
        public static string BuildShareFilesJson(ShareFilesPayload payload)
        {
            var obj = new Dictionary<string, object?> { ["filePaths"] = BuildStringList(payload.filePaths) };
            return SerializeObject(obj);
        }

        /// <summary>
        /// Builds JSON for the <c>registerDirectShareTarget</c> native operation.
        /// </summary>
        /// <param name="payload">Direct Share target payload to serialize.</param>
        /// <returns>JSON string matching the <c>UnityShareJsonParser</c> registerDirectShareTarget schema.</returns>
        public static string BuildDirectShareTargetJson(DirectShareTargetPayload payload)
        {
            var obj = new Dictionary<string, object?>
            {
                ["id"] = payload.id,
                ["label"] = payload.label,
                ["iconBase64"] = payload.iconBase64
            };
            AddIfNotNullOrWhiteSpace(obj, "category", payload.category);
            return SerializeObject(obj);
        }

        /// <summary>
        /// Builds JSON for the <c>removeDirectShareTargets</c> native operation.
        /// </summary>
        /// <param name="payload">Shortcut removal payload to serialize.</param>
        /// <returns>JSON string matching the <c>UnityShareJsonParser</c> removeDirectShareTargets schema.</returns>
        public static string BuildRemoveDirectShareTargetsJson(RemoveDirectShareTargetsPayload payload)
        {
            var obj = new Dictionary<string, object?> { ["ids"] = BuildStringList(payload.ids) };
            return SerializeObject(obj);
        }

        private static void AddIfNotNullOrWhiteSpace(Dictionary<string, object?> target, string key, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                target[key] = value;
            }
        }

        private static void AddChooserActions(Dictionary<string, object?> target, ChooserActionPayload[]? actions)
        {
            if (actions == null || actions.Length == 0)
            {
                return;
            }

            var list = new List<object?>(actions.Length);
            foreach (var action in actions)
            {
                var actionObj = new Dictionary<string, object?>
                {
                    ["label"] = action.label,
                    ["iconBase64"] = action.iconBase64
                };
                AddIfNotNullOrWhiteSpace(actionObj, "intentAction", action.intentAction);
                list.Add(actionObj);
            }
            target["chooserActions"] = list;
        }

        private static List<object?> BuildStringList(string[] values)
        {
            var list = new List<object?>(values.Length);
            foreach (var v in values)
            {
                list.Add(v);
            }
            return list;
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
                case string s:
                    AppendEscapedString(builder, s);
                    break;
                case bool b:
                    builder.Append(b ? "true" : "false");
                    break;
                case int i:
                    builder.Append(i.ToString(CultureInfo.InvariantCulture));
                    break;
                case long l:
                    builder.Append(l.ToString(CultureInfo.InvariantCulture));
                    break;
                case Dictionary<string, object?> dict:
                    AppendObject(builder, dict);
                    break;
                case List<object?> list:
                    AppendArray(builder, list);
                    break;
            }
        }

        private static void AppendObject(StringBuilder builder, Dictionary<string, object?> obj)
        {
            builder.Append('{');
            bool isFirst = true;
            foreach (var pair in obj)
            {
                if (!isFirst)
                {
                    builder.Append(',');
                }
                AppendEscapedString(builder, pair.Key);
                builder.Append(':');
                AppendValue(builder, pair.Value);
                isFirst = false;
            }
            builder.Append('}');
        }

        private static void AppendArray(StringBuilder builder, List<object?> values)
        {
            builder.Append('[');
            for (int i = 0; i < values.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(',');
                }
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
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\b': builder.Append("\\b"); break;
                    case '\f': builder.Append("\\f"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                        {
                            builder.Append("\\u");
                            builder.Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            builder.Append(c);
                        }
                        break;
                }
            }
            builder.Append('"');
        }
    }
}
