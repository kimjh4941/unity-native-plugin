#nullable enable

#if UNITY_STANDALONE_OSX || UNITY_EDITOR
namespace JonghyunKim.NativeToolkit.Runtime.Clipboard
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text;

    /// <summary>
    /// Builds the request JSON for the macOS clipboard native operations. Hand-written serializer,
    /// because which keys are present is part of the contract and <c>JsonUtility</c> cannot express
    /// "omit this key" or a dictionary.
    /// <para>
    /// One method per argument the C ABI takes. <c>stopObserving</c> takes none and has no builder.
    /// </para>
    /// <para>
    /// Returning <c>null</c> is meaningful: the native layer treats a null <c>optionsJson</c> as
    /// "use the defaults" and a null <c>matchingTypesJson</c> as "no filter". Those cases return a
    /// C# null rather than an empty string, so the P/Invoke passes a null pointer.
    /// </para>
    /// <para>
    /// Intentional deviation from the "log every public method" rule in csharp.md: these methods
    /// receive clipboard content, which may hold passwords or tokens. Nothing is logged here.
    /// </para>
    /// </summary>
    public static class MacClipboardJsonBuilder
    {
        /// <summary>
        /// Builds the <c>scopeJson</c> argument.
        /// </summary>
        /// <param name="scope">Pasteboard to target.</param>
        /// <returns>Scope JSON. The <c>name</c> key is omitted for the general pasteboard.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="scope"/> is null.</exception>
        public static string BuildScopeJson(MacPasteboardScope scope)
        {
            if (scope == null) { throw new ArgumentNullException(nameof(scope)); }
            var sb = new StringBuilder();
            AppendScope(sb, scope);
            return sb.ToString();
        }

        /// <summary>
        /// Builds the <c>contentJson</c> argument for a copy or append.
        /// </summary>
        /// <param name="content">Content to write.</param>
        /// <returns>Content JSON with every representation base64 encoded.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="content"/> is null.</exception>
        public static string BuildContentJson(MacClipboardContent content)
        {
            if (content == null) { throw new ArgumentNullException(nameof(content)); }

            var sb = new StringBuilder();
            sb.Append("{\"items\":[");
            for (int i = 0; i < content.Items.Count; i++)
            {
                if (i > 0) { sb.Append(','); }
                sb.Append("{\"representations\":{");

                MacClipboardContentItem item = content.Items[i];
                // Sorted so the same content always produces the same string: a dictionary's
                // enumeration order is not defined, and the builder tests compare exact text.
                // The native side decodes into a dictionary and does not care about order.
                var keys = new List<string>(item.Representations.Keys);
                keys.Sort(StringComparer.Ordinal);

                for (int k = 0; k < keys.Count; k++)
                {
                    if (k > 0) { sb.Append(','); }
                    AppendString(sb, keys[k]);
                    sb.Append(':');
                    AppendString(sb, Convert.ToBase64String(item.Representations[keys[k]]));
                }
                sb.Append("}}");
            }
            sb.Append("]}");
            return sb.ToString();
        }

        /// <summary>
        /// Builds the <c>optionsJson</c> argument for a copy.
        /// </summary>
        /// <param name="options">Options, or <c>null</c> to use the native defaults.</param>
        /// <returns>Options JSON, or <c>null</c> when <paramref name="options"/> is null.</returns>
        public static string? BuildOptionsJson(MacClipboardCopyOptions? options)
        {
            if (options == null) { return null; }
            return "{\"localOnly\":" + (options.LocalOnly ? "true" : "false") + "}";
        }

        /// <summary>
        /// Builds the <c>ownershipJson</c> argument for an append.
        /// </summary>
        /// <param name="ownership">Ownership returned by the previous copy or append.</param>
        /// <returns>Ownership JSON.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="ownership"/> is null.</exception>
        public static string BuildOwnershipJson(MacPasteboardOwnership ownership)
        {
            if (ownership == null) { throw new ArgumentNullException(nameof(ownership)); }
            var sb = new StringBuilder();
            sb.Append("{\"scope\":");
            AppendScope(sb, ownership.Scope);
            sb.Append(",\"changeCount\":");
            sb.Append(ownership.ChangeCount.ToString(CultureInfo.InvariantCulture));
            sb.Append('}');
            return sb.ToString();
        }

        /// <summary>
        /// Builds the <c>requestJson</c> argument for creating a pasteboard.
        /// </summary>
        /// <param name="request">What to create.</param>
        /// <returns>Request JSON. The <c>name</c> key is omitted for a unique pasteboard.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
        public static string BuildCreateRequestJson(MacPasteboardCreationRequest request)
        {
            if (request == null) { throw new ArgumentNullException(nameof(request)); }
            var sb = new StringBuilder();
            sb.Append("{\"kind\":");
            AppendString(sb, request.Kind == MacPasteboardCreationRequestKind.Named ? "named" : "unique");
            if (request.Name != null)
            {
                sb.Append(",\"name\":");
                AppendString(sb, request.Name);
            }
            sb.Append('}');
            return sb.ToString();
        }

        /// <summary>
        /// Builds the <c>matchingTypesJson</c> argument for a snapshot.
        /// </summary>
        /// <param name="types">Types to match, or <c>null</c> for no filter.</param>
        /// <returns>
        /// A JSON array, or <c>null</c> when <paramref name="types"/> is null. An empty list is
        /// serialized as <c>[]</c>, which the native layer rejects with
        /// <see cref="MacClipboardErrorCodes.EmptyTypeFilter"/>.
        /// </returns>
        public static string? BuildMatchingTypesJson(IReadOnlyList<string>? types)
        {
            if (types == null) { return null; }
            var sb = new StringBuilder();
            sb.Append('[');
            for (int i = 0; i < types.Count; i++)
            {
                if (i > 0) { sb.Append(','); }
                AppendString(sb, types[i]);
            }
            sb.Append(']');
            return sb.ToString();
        }

        /// <summary>
        /// Builds the <c>patternsJson</c> argument for a detection call.
        /// </summary>
        /// <param name="patterns">Patterns to look for.</param>
        /// <returns>
        /// A sorted JSON array of native raw values. An empty collection is serialized as
        /// <c>[]</c>, which the native layer rejects with
        /// <see cref="MacClipboardErrorCodes.EmptyDetectionPatterns"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="patterns"/> is null.</exception>
        public static string BuildPatternsJson(IReadOnlyCollection<MacClipboardDetectionPattern> patterns)
        {
            if (patterns == null) { throw new ArgumentNullException(nameof(patterns)); }

            var raw = new List<string>(patterns.Count);
            foreach (MacClipboardDetectionPattern pattern in patterns)
            {
                raw.Add(pattern.ToRawValue());
            }
            raw.Sort(StringComparer.Ordinal);

            var sb = new StringBuilder();
            sb.Append('[');
            for (int i = 0; i < raw.Count; i++)
            {
                if (i > 0) { sb.Append(','); }
                AppendString(sb, raw[i]);
            }
            sb.Append(']');
            return sb.ToString();
        }

        private static void AppendScope(StringBuilder sb, MacPasteboardScope scope)
        {
            sb.Append("{\"kind\":");
            AppendString(sb, scope.Kind switch
            {
                MacPasteboardScopeKind.Named => "named",
                MacPasteboardScopeKind.Unique => "unique",
                _ => "general",
            });
            // Omitted rather than emitted as null: the native parser ignores name for the general
            // pasteboard, and leaving it out keeps the request the same shape the native side
            // produces when it encodes a scope back.
            if (scope.Name != null)
            {
                sb.Append(",\"name\":");
                AppendString(sb, scope.Name);
            }
            sb.Append('}');
        }

        // Non-ASCII is written through as UTF-8 rather than escaped to \uXXXX: both are valid JSON,
        // and passing the characters straight through keeps the payload smaller.
        private static void AppendString(StringBuilder sb, string value)
        {
            sb.Append('"');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                        {
                            sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            sb.Append('"');
        }
    }
}
#endif
