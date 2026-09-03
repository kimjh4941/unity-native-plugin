#nullable enable

#if UNITY_STANDALONE_OSX || UNITY_EDITOR
// Intentional deviation from the "log every public method" rule in csharp.md: every factory here
// receives clipboard content or a pasteboard name, which may hold passwords, tokens or user
// identifiers. These are pure value constructors, so they emit no logs at all rather than a
// shape-only line. The operations that use them already log kind, length and count at the Manager
// boundary. This matches the native ClipboardLog redaction policy.

namespace JonghyunKim.NativeToolkit.Runtime.Clipboard
{
    using System;
    using System.Collections.Generic;

    /// <summary>Kind of pasteboard a <see cref="MacPasteboardScope"/> refers to.</summary>
    public enum MacPasteboardScopeKind
    {
        /// <summary>The system-wide general pasteboard, also the one Universal Clipboard syncs.</summary>
        General,

        /// <summary>A pasteboard shared by name.</summary>
        Named,

        /// <summary>A pasteboard created with a system generated unique name.</summary>
        Unique
    }

    /// <summary>
    /// Reference to a pasteboard.
    /// <para>
    /// Named and unique pasteboards live in the pasteboard server and <b>outlive this process</b>.
    /// Release a unique one with <c>RemovePasteboard</c>, and never place confidential data on a
    /// named one.
    /// </para>
    /// </summary>
    public sealed class MacPasteboardScope
    {
        /// <summary>Which kind of pasteboard this scope refers to.</summary>
        public MacPasteboardScopeKind Kind { get; }

        /// <summary>Pasteboard name. <c>null</c> for <see cref="MacPasteboardScopeKind.General"/>.</summary>
        public string? Name { get; }

        /// <summary>The system-wide general pasteboard.</summary>
        public static MacPasteboardScope General { get; } = new(MacPasteboardScopeKind.General, null);

        /// <summary>
        /// Creates a scope referring to a named pasteboard.
        /// </summary>
        /// <param name="name">Pasteboard name. Must not be null, empty or whitespace.</param>
        /// <returns>A named <see cref="MacPasteboardScope"/>.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is blank.</exception>
        public static MacPasteboardScope Named(string name) =>
            new(MacPasteboardScopeKind.Named, RequireName(name, nameof(name)));

        /// <summary>
        /// Creates a scope referring to a unique pasteboard by its generated name.
        /// </summary>
        /// <param name="name">Generated name, as returned by <c>CreatePasteboard</c>.</param>
        /// <returns>A unique <see cref="MacPasteboardScope"/>.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is blank.</exception>
        public static MacPasteboardScope Unique(string name) =>
            new(MacPasteboardScopeKind.Unique, RequireName(name, nameof(name)));

        private MacPasteboardScope(MacPasteboardScopeKind kind, string? name)
        {
            Kind = kind;
            Name = name;
        }

        // The native parser only rejects an *empty* name, so " " reaches NSPasteboard and a
        // pasteboard is actually created. This check is the only thing that stops it, and is a
        // deliberate exception to leaving validation to the native layer.
        private static string RequireName(string name, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Pasteboard name must not be blank.", parameterName);
            }
            return name;
        }
    }

    /// <summary>Kind of pasteboard creation request.</summary>
    public enum MacPasteboardCreationRequestKind
    {
        /// <summary>Create, or fetch, a pasteboard with the given name.</summary>
        Named,

        /// <summary>Create a pasteboard whose name the system guarantees to be unique.</summary>
        Unique
    }

    /// <summary>
    /// Request passed to <c>CreatePasteboard</c>. Separate from <see cref="MacPasteboardScope"/>
    /// because a unique pasteboard's name is an output, not an input.
    /// </summary>
    public sealed class MacPasteboardCreationRequest
    {
        /// <summary>Which kind of pasteboard to create.</summary>
        public MacPasteboardCreationRequestKind Kind { get; }

        /// <summary>Requested name. <c>null</c> for <see cref="MacPasteboardCreationRequestKind.Unique"/>.</summary>
        public string? Name { get; }

        /// <summary>Requests a pasteboard with a system generated unique name.</summary>
        public static MacPasteboardCreationRequest Unique { get; } =
            new(MacPasteboardCreationRequestKind.Unique, null);

        /// <summary>
        /// Requests a named pasteboard, resolving an existing one with the same name.
        /// </summary>
        /// <param name="name">Pasteboard name. Must not be null, empty or whitespace.</param>
        /// <returns>A named <see cref="MacPasteboardCreationRequest"/>.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is blank.</exception>
        public static MacPasteboardCreationRequest Named(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Pasteboard name must not be blank.", nameof(name));
            }
            return new MacPasteboardCreationRequest(MacPasteboardCreationRequestKind.Named, name);
        }

        private MacPasteboardCreationRequest(MacPasteboardCreationRequestKind kind, string? name)
        {
            Kind = kind;
            Name = name;
        }
    }

    /// <summary>
    /// Proof that this app owned the pasteboard at a point in time. Required to append.
    /// <para>
    /// A successful append leaves the change count untouched, so the same ownership can be used
    /// for the next append. Once another app takes the pasteboard, append fails with
    /// <see cref="MacClipboardErrorCodes.OwnershipLost"/>.
    /// </para>
    /// </summary>
    public sealed class MacPasteboardOwnership
    {
        /// <summary>The pasteboard the ownership refers to.</summary>
        public MacPasteboardScope Scope { get; }

        /// <summary>
        /// Change count reported when ownership was taken.
        /// <para>
        /// 64-bit because the native side declares Swift's <c>Int</c>. Narrowing to <c>int</c>
        /// would turn a valid response into a parse failure once the counter passes
        /// <c>int.MaxValue</c>.
        /// </para>
        /// </summary>
        public long ChangeCount { get; }

        /// <summary>
        /// Creates an ownership token from its parts.
        /// </summary>
        /// <param name="scope">Pasteboard the ownership refers to.</param>
        /// <param name="changeCount">Change count reported when ownership was taken.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="scope"/> is null.</exception>
        public MacPasteboardOwnership(MacPasteboardScope scope, long changeCount)
        {
            Scope = scope ?? throw new ArgumentNullException(nameof(scope));
            ChangeCount = changeCount;
        }
    }

    /// <summary>
    /// One item written to a pasteboard: a set of representations keyed by uniform type identifier.
    /// <para>
    /// Named to match the direction of travel. The read side is
    /// <see cref="MacClipboardItem"/>; this is the write side.
    /// </para>
    /// </summary>
    public sealed class MacClipboardContentItem
    {
        /// <summary>Uniform type identifier to raw bytes. Never null.</summary>
        public IReadOnlyDictionary<string, byte[]> Representations { get; }

        /// <summary>
        /// Creates an item from representations supplied by the caller.
        /// </summary>
        /// <param name="representations">Uniform type identifier to bytes.</param>
        /// <returns>A <see cref="MacClipboardContentItem"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="representations"/> is null.</exception>
        public static MacClipboardContentItem FromRepresentations(
            IReadOnlyDictionary<string, byte[]> representations) =>
            new(representations ?? throw new ArgumentNullException(nameof(representations)));

        /// <summary>
        /// Creates a UTF-8 plain text item.
        /// </summary>
        /// <param name="text">Text to write.</param>
        /// <returns>A <see cref="MacClipboardContentItem"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
        public static MacClipboardContentItem PlainText(string text)
        {
            if (text == null) { throw new ArgumentNullException(nameof(text)); }
            return new MacClipboardContentItem(new Dictionary<string, byte[]>
            {
                [MacClipboardTypes.PlainText] = System.Text.Encoding.UTF8.GetBytes(text),
            });
        }

        /// <summary>
        /// Creates an HTML item, optionally with a plain text fallback in the same item.
        /// </summary>
        /// <param name="html">HTML markup.</param>
        /// <param name="plainFallback">Plain text fallback, or <c>null</c> for none.</param>
        /// <returns>A <see cref="MacClipboardContentItem"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="html"/> is null.</exception>
        public static MacClipboardContentItem Html(string html, string? plainFallback = null)
        {
            if (html == null) { throw new ArgumentNullException(nameof(html)); }
            var map = new Dictionary<string, byte[]>
            {
                [MacClipboardTypes.Html] = System.Text.Encoding.UTF8.GetBytes(html),
            };
            if (plainFallback != null)
            {
                map[MacClipboardTypes.PlainText] = System.Text.Encoding.UTF8.GetBytes(plainFallback);
            }
            return new MacClipboardContentItem(map);
        }

        /// <summary>
        /// Creates a URL item.
        /// </summary>
        /// <param name="url">Absolute URL string. Not validated here; the pasteboard decides.</param>
        /// <returns>A <see cref="MacClipboardContentItem"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="url"/> is null.</exception>
        public static MacClipboardContentItem Url(string url)
        {
            if (url == null) { throw new ArgumentNullException(nameof(url)); }
            return new MacClipboardContentItem(new Dictionary<string, byte[]>
            {
                [MacClipboardTypes.Url] = System.Text.Encoding.UTF8.GetBytes(url),
            });
        }

        /// <summary>
        /// Creates an item holding raw bytes for one uniform type identifier.
        /// </summary>
        /// <param name="utType">Uniform type identifier. Validated by the native layer.</param>
        /// <param name="bytes">Raw bytes.</param>
        /// <returns>A <see cref="MacClipboardContentItem"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when either argument is null.</exception>
        public static MacClipboardContentItem Data(string utType, byte[] bytes)
        {
            if (utType == null) { throw new ArgumentNullException(nameof(utType)); }
            if (bytes == null) { throw new ArgumentNullException(nameof(bytes)); }
            return new MacClipboardContentItem(new Dictionary<string, byte[]> { [utType] = bytes });
        }

        private MacClipboardContentItem(IReadOnlyDictionary<string, byte[]> representations)
        {
            Representations = representations;
        }
    }

    /// <summary>Content written by a copy or append.</summary>
    public sealed class MacClipboardContent
    {
        /// <summary>Items to write, in pasteboard order. Never null.</summary>
        public IReadOnlyList<MacClipboardContentItem> Items { get; }

        /// <summary>
        /// Wraps a single item.
        /// </summary>
        /// <param name="item">Item to write.</param>
        /// <returns>A <see cref="MacClipboardContent"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="item"/> is null.</exception>
        public static MacClipboardContent Single(MacClipboardContentItem item)
        {
            if (item == null) { throw new ArgumentNullException(nameof(item)); }
            return new MacClipboardContent(new[] { item });
        }

        /// <summary>
        /// Wraps several items.
        /// </summary>
        /// <param name="items">Items to write, in pasteboard order.</param>
        /// <returns>A <see cref="MacClipboardContent"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="items"/> is null.</exception>
        public static MacClipboardContent Multiple(IReadOnlyList<MacClipboardContentItem> items) =>
            new(items ?? throw new ArgumentNullException(nameof(items)));

        /// <summary>
        /// Shorthand for a single UTF-8 plain text item.
        /// </summary>
        /// <param name="text">Text to write.</param>
        /// <returns>A <see cref="MacClipboardContent"/>.</returns>
        public static MacClipboardContent PlainText(string text) =>
            Single(MacClipboardContentItem.PlainText(text));

        private MacClipboardContent(IReadOnlyList<MacClipboardContentItem> items)
        {
            Items = items;
        }
    }

    /// <summary>Privacy options for a copy.</summary>
    public sealed class MacClipboardCopyOptions
    {
        /// <summary>
        /// Whether the contents should stay on this device.
        /// <para>
        /// The native layer expresses this through <c>NSPasteboard.ContentsOptions</c>, but its
        /// effect on Universal Clipboard has not been confirmed on real hardware.
        /// </para>
        /// </summary>
        public bool LocalOnly { get; }

        /// <summary>The native default: local only.</summary>
        public static MacClipboardCopyOptions PrivacyPreservingDefault { get; } = new(true);

        /// <summary>
        /// Creates options with an explicit choice.
        /// </summary>
        /// <param name="localOnly">Whether the contents should stay on this device.</param>
        /// <returns>A <see cref="MacClipboardCopyOptions"/>.</returns>
        public static MacClipboardCopyOptions Create(bool localOnly) => new(localOnly);

        private MacClipboardCopyOptions(bool localOnly)
        {
            LocalOnly = localOnly;
        }
    }

    /// <summary>A pattern the data detection system can look for on the pasteboard.</summary>
    public enum MacClipboardDetectionPattern
    {
        /// <summary>A string the system judges to be a web URL.</summary>
        ProbableWebUrl,

        /// <summary>A string the system judges to be a web search term.</summary>
        ProbableWebSearch,

        /// <summary>A number.</summary>
        Number,

        /// <summary>Web links.</summary>
        Links,

        /// <summary>Phone numbers.</summary>
        PhoneNumbers,

        /// <summary>Email addresses.</summary>
        EmailAddresses,

        /// <summary>Postal addresses.</summary>
        PostalAddresses,

        /// <summary>Calendar events.</summary>
        CalendarEvents,

        /// <summary>Parcel tracking numbers.</summary>
        ShipmentTrackingNumbers,

        /// <summary>Flight numbers.</summary>
        FlightNumbers,

        /// <summary>Amounts of money.</summary>
        MoneyAmounts
    }

    /// <summary>A metadata type the detection system can report without reading the contents.</summary>
    public enum MacClipboardMetadataType
    {
        /// <summary>Content type of a file reference.</summary>
        ContentType
    }

    /// <summary>Current pasteboard access behaviour for this app.</summary>
    public enum MacClipboardAccessBehavior
    {
        /// <summary>Never triggered an access alert; not listed in System Settings.</summary>
        Default,

        /// <summary>The system asks before granting programmatic access.</summary>
        Ask,

        /// <summary>All access is allowed without notifying.</summary>
        AlwaysAllow,

        /// <summary>All access is denied without notifying.</summary>
        AlwaysDeny,

        /// <summary>The reporting API is unavailable below macOS 15.4.</summary>
        Unavailable,

        /// <summary>The native layer reported a value this package does not know.</summary>
        Unknown
    }

    /// <summary>Uniform type identifiers used often enough to be worth naming.</summary>
    public static class MacClipboardTypes
    {
        /// <summary>UTF-8 plain text.</summary>
        public const string PlainText = "public.utf8-plain-text";

        /// <summary>HTML markup.</summary>
        public const string Html = "public.html";

        /// <summary>Rich text.</summary>
        public const string Rtf = "public.rtf";

        /// <summary>A URL.</summary>
        public const string Url = "public.url";

        /// <summary>A file URL.</summary>
        public const string FileUrl = "public.file-url";

        /// <summary>PNG image data.</summary>
        public const string Png = "public.png";

        /// <summary>TIFF image data.</summary>
        public const string Tiff = "public.tiff";
    }

    /// <summary>
    /// Conversions between this package's enums and the raw values the native layer uses.
    /// <para>
    /// The macOS raw values are plural (<c>phoneNumbers</c>), unlike the iOS ones. Copying the iOS
    /// table would produce requests the native layer rejects outright, so the mapping is spelled
    /// out here rather than derived from the enum name.
    /// </para>
    /// </summary>
    internal static class MacClipboardDetectionPatternExtensions
    {
        private static readonly Dictionary<MacClipboardDetectionPattern, string> s_patternRawValues =
            new()
            {
                [MacClipboardDetectionPattern.ProbableWebUrl] = "probableWebURL",
                [MacClipboardDetectionPattern.ProbableWebSearch] = "probableWebSearch",
                [MacClipboardDetectionPattern.Number] = "number",
                [MacClipboardDetectionPattern.Links] = "links",
                [MacClipboardDetectionPattern.PhoneNumbers] = "phoneNumbers",
                [MacClipboardDetectionPattern.EmailAddresses] = "emailAddresses",
                [MacClipboardDetectionPattern.PostalAddresses] = "postalAddresses",
                [MacClipboardDetectionPattern.CalendarEvents] = "calendarEvents",
                [MacClipboardDetectionPattern.ShipmentTrackingNumbers] = "shipmentTrackingNumbers",
                [MacClipboardDetectionPattern.FlightNumbers] = "flightNumbers",
                [MacClipboardDetectionPattern.MoneyAmounts] = "moneyAmounts",
            };

        private static readonly Dictionary<MacClipboardAccessBehavior, string> s_behaviorRawValues =
            new()
            {
                [MacClipboardAccessBehavior.Default] = "default",
                [MacClipboardAccessBehavior.Ask] = "ask",
                [MacClipboardAccessBehavior.AlwaysAllow] = "alwaysAllow",
                [MacClipboardAccessBehavior.AlwaysDeny] = "alwaysDeny",
                [MacClipboardAccessBehavior.Unavailable] = "unavailable",
            };

        private const string MetadataContentTypeRawValue = "contentType";

        internal static string ToRawValue(this MacClipboardDetectionPattern pattern) =>
            s_patternRawValues[pattern];

        internal static bool TryParsePattern(string? rawValue, out MacClipboardDetectionPattern pattern)
        {
            foreach (var pair in s_patternRawValues)
            {
                if (pair.Value == rawValue)
                {
                    pattern = pair.Key;
                    return true;
                }
            }
            pattern = default;
            return false;
        }

        // A value this package does not know maps to Unknown rather than failing: the native layer
        // may gain a case before this package does, and an access behaviour is advisory.
        internal static MacClipboardAccessBehavior ParseAccessBehavior(string? rawValue)
        {
            foreach (var pair in s_behaviorRawValues)
            {
                if (pair.Value == rawValue)
                {
                    return pair.Key;
                }
            }
            return MacClipboardAccessBehavior.Unknown;
        }

        internal static bool TryParseMetadataType(string? rawValue, out MacClipboardMetadataType type)
        {
            if (rawValue == MetadataContentTypeRawValue)
            {
                type = MacClipboardMetadataType.ContentType;
                return true;
            }
            type = default;
            return false;
        }
    }
}
#endif
