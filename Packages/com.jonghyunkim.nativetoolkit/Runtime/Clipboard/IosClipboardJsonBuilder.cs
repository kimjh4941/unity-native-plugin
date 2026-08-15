#nullable enable

#if UNITY_IOS || UNITY_EDITOR
namespace JonghyunKim.NativeToolkit.Runtime.Clipboard
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text;

    /// <summary>
    /// Builds request JSON for the iOS clipboard native operations. Hand-written serializer with
    /// full control over optional keys, matching the <c>IosShareJsonBuilder</c> pattern.
    /// <para>
    /// One method per operation that takes a <c>requestJson</c> argument (13 of the 15 bridge
    /// functions). <c>cancelLoads</c> and <c>stopObserving</c> take no request and have no builder.
    /// </para>
    /// <para>
    /// Key omission is significant: the native parser resolves an omitted <c>scope</c> key to the
    /// general pasteboard, but rejects a present-but-malformed one. A null scope is therefore
    /// serialized by leaving the key out entirely, never by emitting null.
    /// </para>
    /// <para>
    /// Intentional deviation from the "log every public method" rule in csharp.md: these methods
    /// receive clipboard content, which may hold passwords or tokens. Nothing is logged here.
    /// </para>
    /// </summary>
    public static class IosClipboardJsonBuilder
    {
        /// <summary>
        /// Builds the request for <c>clipboardCopy</c>.
        /// </summary>
        /// <param name="content">Content to write.</param>
        /// <param name="scope">Target pasteboard, or <c>null</c> for the general pasteboard.</param>
        /// <param name="options">Privacy options, or <c>null</c> to use the native default.</param>
        /// <returns>Request JSON.</returns>
        public static string BuildCopyJson(
            IosClipboardContent content,
            IosPasteboardScope? scope,
            IosClipboardCopyOptions? options)
        {
            var obj = new Dictionary<string, object?>();
            AddScope(obj, scope);
            obj["content"] = BuildContent(content);
            if (options != null)
            {
                obj["options"] = BuildOptions(options);
            }
            return Serialize(obj);
        }

        /// <summary>
        /// Builds the request for <c>clipboardAppend</c>.
        /// <para>
        /// Never emits an <c>options</c> key: the native layer rejects an append request that
        /// carries one, whatever its value.
        /// </para>
        /// </summary>
        /// <param name="content">Content to append.</param>
        /// <param name="scope">Target pasteboard, or <c>null</c> for the general pasteboard.</param>
        /// <returns>Request JSON.</returns>
        public static string BuildAppendJson(IosClipboardContent content, IosPasteboardScope? scope)
        {
            var obj = new Dictionary<string, object?>();
            AddScope(obj, scope);
            obj["content"] = BuildContent(content);
            return Serialize(obj);
        }

        /// <summary>
        /// Builds the request for <c>clipboardRead</c>.
        /// </summary>
        /// <param name="scope">Target pasteboard, or <c>null</c> for the general pasteboard.</param>
        /// <returns>Request JSON.</returns>
        public static string BuildReadJson(IosPasteboardScope? scope) => BuildScopeOnlyJson(scope);

        /// <summary>
        /// Builds the request for <c>clipboardReadData</c>.
        /// </summary>
        /// <param name="utType">Uniform type identifier to read.</param>
        /// <param name="scope">Target pasteboard, or <c>null</c> for the general pasteboard.</param>
        /// <returns>Request JSON.</returns>
        public static string BuildReadDataJson(string utType, IosPasteboardScope? scope)
        {
            var obj = new Dictionary<string, object?>();
            AddScope(obj, scope);
            obj["utType"] = utType;
            return Serialize(obj);
        }

        /// <summary>
        /// Builds the request for <c>clipboardGetSnapshot</c>.
        /// </summary>
        /// <param name="scope">Target pasteboard, or <c>null</c> for the general pasteboard.</param>
        /// <param name="matchingTypes">Types to match, or <c>null</c>/empty to omit the key.</param>
        /// <returns>Request JSON.</returns>
        public static string BuildGetSnapshotJson(IosPasteboardScope? scope, string[]? matchingTypes)
        {
            var obj = new Dictionary<string, object?>();
            AddScope(obj, scope);
            if (matchingTypes != null && matchingTypes.Length > 0)
            {
                var list = new List<object?>(matchingTypes.Length);
                foreach (string type in matchingTypes)
                {
                    list.Add(type);
                }
                obj["matchingTypes"] = list;
            }
            return Serialize(obj);
        }

        /// <summary>
        /// Builds the request for <c>clipboardClear</c>.
        /// </summary>
        /// <param name="scope">Target pasteboard, or <c>null</c> for the general pasteboard.</param>
        /// <returns>Request JSON.</returns>
        public static string BuildClearJson(IosPasteboardScope? scope) => BuildScopeOnlyJson(scope);

        /// <summary>
        /// Builds the request for <c>clipboardCreatePasteboard</c>.
        /// </summary>
        /// <param name="request">Creation request.</param>
        /// <returns>Request JSON.</returns>
        public static string BuildCreatePasteboardJson(IosPasteboardCreationRequest request)
        {
            var inner = new Dictionary<string, object?>();
            if (request.Kind == IosPasteboardCreationRequestKind.Named)
            {
                inner["kind"] = "named";
                inner["name"] = request.Name;
            }
            else
            {
                inner["kind"] = "unique";
            }
            return Serialize(new Dictionary<string, object?> { ["request"] = inner });
        }

        /// <summary>
        /// Builds the request for <c>clipboardRemovePasteboard</c>.
        /// <para>
        /// Always emits the scope: omitting it would resolve to the general pasteboard, which the
        /// native layer refuses with CLIPBOARD_CANNOT_REMOVE_GENERAL.
        /// </para>
        /// </summary>
        /// <param name="scope">Pasteboard to invalidate.</param>
        /// <returns>Request JSON.</returns>
        public static string BuildRemovePasteboardJson(IosPasteboardScope scope) =>
            Serialize(new Dictionary<string, object?> { ["scope"] = BuildScope(scope) });

        /// <summary>
        /// Builds the request for <c>clipboardDetectPatterns</c>.
        /// </summary>
        /// <param name="patterns">Patterns to detect.</param>
        /// <param name="scope">Target pasteboard, or <c>null</c> for the general pasteboard.</param>
        /// <returns>Request JSON.</returns>
        public static string BuildDetectPatternsJson(
            IosClipboardDetectionPattern[] patterns,
            IosPasteboardScope? scope) => BuildPatternsJson(patterns, scope);

        /// <summary>
        /// Builds the request for <c>clipboardDetectValues</c>.
        /// </summary>
        /// <param name="patterns">Patterns to detect.</param>
        /// <param name="scope">Target pasteboard, or <c>null</c> for the general pasteboard.</param>
        /// <returns>Request JSON.</returns>
        public static string BuildDetectValuesJson(
            IosClipboardDetectionPattern[] patterns,
            IosPasteboardScope? scope) => BuildPatternsJson(patterns, scope);

        /// <summary>
        /// Builds the request for <c>clipboardLoadItem</c>.
        /// </summary>
        /// <param name="request">What to load.</param>
        /// <param name="scope">Target pasteboard, or <c>null</c> for the general pasteboard.</param>
        /// <returns>Request JSON.</returns>
        public static string BuildLoadItemJson(IosClipboardLoadRequest request, IosPasteboardScope? scope)
        {
            var inner = new Dictionary<string, object?>();
            switch (request.Kind)
            {
                case IosClipboardLoadRequestKind.Text:
                    inner["kind"] = "text";
                    break;
                case IosClipboardLoadRequestKind.Url:
                    inner["kind"] = "url";
                    break;
                case IosClipboardLoadRequestKind.Image:
                    inner["kind"] = "image";
                    break;
                default:
                    inner["kind"] = "file";
                    inner["utType"] = request.UtType;
                    break;
            }

            var obj = new Dictionary<string, object?>();
            AddScope(obj, scope);
            obj["request"] = inner;
            return Serialize(obj);
        }

        /// <summary>
        /// Builds the request for <c>clipboardStartObserving</c>.
        /// </summary>
        /// <param name="scope">Pasteboard to observe, or <c>null</c> for the general pasteboard.</param>
        /// <returns>Request JSON.</returns>
        public static string BuildStartObservingJson(IosPasteboardScope? scope) => BuildScopeOnlyJson(scope);

        /// <summary>
        /// Builds the request for <c>clipboardCheckForegroundChange</c>.
        /// </summary>
        /// <param name="scope">Target pasteboard, or <c>null</c> for the general pasteboard.</param>
        /// <returns>Request JSON.</returns>
        public static string BuildCheckForegroundChangeJson(IosPasteboardScope? scope) =>
            BuildScopeOnlyJson(scope);

        private static string BuildScopeOnlyJson(IosPasteboardScope? scope)
        {
            var obj = new Dictionary<string, object?>();
            AddScope(obj, scope);
            return Serialize(obj);
        }

        private static string BuildPatternsJson(
            IosClipboardDetectionPattern[] patterns,
            IosPasteboardScope? scope)
        {
            var list = new List<object?>(patterns.Length);
            foreach (IosClipboardDetectionPattern pattern in patterns)
            {
                list.Add(pattern.ToRawValue());
            }

            var obj = new Dictionary<string, object?>();
            AddScope(obj, scope);
            obj["patterns"] = list;
            return Serialize(obj);
        }

        private static void AddScope(Dictionary<string, object?> target, IosPasteboardScope? scope)
        {
            if (scope != null)
            {
                target["scope"] = BuildScope(scope);
            }
        }

        private static Dictionary<string, object?> BuildScope(IosPasteboardScope scope)
        {
            var obj = new Dictionary<string, object?>();
            switch (scope.Kind)
            {
                case IosPasteboardScopeKind.Named:
                    obj["kind"] = "named";
                    obj["name"] = scope.Name;
                    break;
                case IosPasteboardScopeKind.Unique:
                    obj["kind"] = "unique";
                    obj["name"] = scope.Name;
                    break;
                default:
                    obj["kind"] = "general";
                    break;
            }
            return obj;
        }

        private static Dictionary<string, object?> BuildOptions(IosClipboardCopyOptions options)
        {
            var obj = new Dictionary<string, object?> { ["localOnly"] = options.LocalOnly };
            if (options.ExpirationDate.HasValue)
            {
                // The native parser accepts ISO 8601 with or without fractional seconds. Emitting
                // the form without them avoids any rounding mismatch between the two sides.
                obj["expirationDate"] = options.ExpirationDate.Value
                    .ToUniversalTime()
                    .ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
            }
            return obj;
        }

        private static Dictionary<string, object?> BuildContent(IosClipboardContent content)
        {
            var obj = new Dictionary<string, object?>();
            switch (content.Kind)
            {
                case IosClipboardContentKind.PlainText:
                    obj["kind"] = "plainText";
                    obj["text"] = content.Text;
                    break;

                case IosClipboardContentKind.HtmlText:
                    obj["kind"] = "htmlText";
                    obj["plain"] = content.Plain;
                    obj["html"] = content.Html;
                    break;

                case IosClipboardContentKind.Url:
                    obj["kind"] = "url";
                    obj["urlString"] = content.UrlString;
                    break;

                case IosClipboardContentKind.ImageFile:
                    obj["kind"] = "imageFile";
                    obj["path"] = content.Path;
                    break;

                case IosClipboardContentKind.ImageData:
                    obj["kind"] = "imageData";
                    obj["base64"] = Convert.ToBase64String(content.Data!);
                    obj["utType"] = content.UtType;
                    break;

                case IosClipboardContentKind.Color:
                    obj["kind"] = "color";
                    obj["red"] = content.Red;
                    obj["green"] = content.Green;
                    obj["blue"] = content.Blue;
                    obj["alpha"] = content.Alpha;
                    break;

                case IosClipboardContentKind.CustomData:
                    obj["kind"] = "customData";
                    obj["base64"] = Convert.ToBase64String(content.Data!);
                    obj["utType"] = content.UtType;
                    break;

                case IosClipboardContentKind.MultipleText:
                {
                    obj["kind"] = "multipleText";
                    var texts = new List<object?>(content.Texts!.Length);
                    foreach (string text in content.Texts!)
                    {
                        texts.Add(text);
                    }
                    obj["texts"] = texts;
                    break;
                }

                default:
                {
                    obj["kind"] = "multiRepresentation";
                    var representations = new Dictionary<string, object?>();
                    foreach (var pair in content.Representations!)
                    {
                        representations[pair.Key] = Convert.ToBase64String(pair.Value);
                    }
                    obj["representations"] = representations;
                    break;
                }
            }
            return obj;
        }

        private static string Serialize(Dictionary<string, object?> obj)
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
                case double d:
                    // Non-finite values are rejected at the IosClipboardContent factory, so "R"
                    // always produces valid JSON here.
                    builder.Append(d.ToString("R", CultureInfo.InvariantCulture));
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
#endif
