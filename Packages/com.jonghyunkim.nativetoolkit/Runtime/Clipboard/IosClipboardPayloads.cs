#nullable enable

#if UNITY_IOS || UNITY_EDITOR
// Intentional deviation from the "log every public method" rule in csharp.md: every factory in
// this file receives clipboard content or a pasteboard name, which may hold passwords, tokens, or
// user identifiers. These are pure value constructors with no side effect worth tracing, so they
// emit no logs at all rather than a shape-only line — the operations that use them already log
// kind, length and count at the Manager boundary. This matches the native ClipboardRedaction policy.

namespace JonghyunKim.NativeToolkit.Runtime.Clipboard
{
    using System;
    using System.Collections.Generic;

    /// <summary>Kind of pasteboard a <see cref="IosPasteboardScope"/> refers to.</summary>
    public enum IosPasteboardScopeKind
    {
        /// <summary>The systemwide general pasteboard. The only persistent pasteboard.</summary>
        General,

        /// <summary>A named pasteboard shared with apps of the same Team ID.</summary>
        Named,

        /// <summary>A pasteboard created with a system-generated unique name.</summary>
        Unique
    }

    /// <summary>
    /// Reference to an existing pasteboard.
    /// <para>
    /// Named and unique pasteboards are NOT persistent: they exist only while the app that created
    /// them is running. Never use them for persistent sharing.
    /// </para>
    /// </summary>
    public sealed class IosPasteboardScope
    {
        /// <summary>Which kind of pasteboard this scope refers to.</summary>
        public IosPasteboardScopeKind Kind { get; }

        /// <summary>Pasteboard name. <c>null</c> for <see cref="IosPasteboardScopeKind.General"/>.</summary>
        public string? Name { get; }

        /// <summary>The systemwide general pasteboard.</summary>
        public static IosPasteboardScope General { get; } = new(IosPasteboardScopeKind.General, null);

        /// <summary>
        /// Creates a scope referring to a named pasteboard.
        /// </summary>
        /// <param name="name">Pasteboard name. Must not be null, empty or whitespace.</param>
        /// <returns>A named <see cref="IosPasteboardScope"/>.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null, empty or whitespace.</exception>
        public static IosPasteboardScope Named(string name) =>
            new(IosPasteboardScopeKind.Named, RequireName(name, nameof(name)));

        /// <summary>
        /// Creates a scope referring to a unique pasteboard by its generated name.
        /// </summary>
        /// <param name="name">Generated pasteboard name, as returned by <c>CreatePasteboard</c>.</param>
        /// <returns>A unique <see cref="IosPasteboardScope"/>.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null, empty or whitespace.</exception>
        public static IosPasteboardScope Unique(string name) =>
            new(IosPasteboardScopeKind.Unique, RequireName(name, nameof(name)));

        private IosPasteboardScope(IosPasteboardScopeKind kind, string? name)
        {
            Kind = kind;
            Name = name;
        }

        // A blank name would serialize into a request the native parser rejects with
        // CLIPBOARD_INVALID_REQUEST, which is indistinguishable from other malformed input.
        // Failing here keeps the cause visible at the call site.
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
    public enum IosPasteboardCreationRequestKind
    {
        /// <summary>Create (or resolve an existing) named pasteboard.</summary>
        Named,

        /// <summary>Create a pasteboard with a system-generated unique name.</summary>
        Unique
    }

    /// <summary>
    /// Request passed to <see cref="IosClipboardManager.CreatePasteboard"/>.
    /// Separate from <see cref="IosPasteboardScope"/> because a unique pasteboard's name is an
    /// output, not an input.
    /// </summary>
    public sealed class IosPasteboardCreationRequest
    {
        /// <summary>Which kind of pasteboard to create.</summary>
        public IosPasteboardCreationRequestKind Kind { get; }

        /// <summary>Requested name. <c>null</c> for <see cref="IosPasteboardCreationRequestKind.Unique"/>.</summary>
        public string? Name { get; }

        /// <summary>Requests a pasteboard with a system-generated unique name.</summary>
        public static IosPasteboardCreationRequest Unique { get; } =
            new(IosPasteboardCreationRequestKind.Unique, null);

        /// <summary>
        /// Requests a named pasteboard, resolving an existing one with the same name.
        /// </summary>
        /// <param name="name">Pasteboard name. Must not be null, empty or whitespace.</param>
        /// <returns>A named <see cref="IosPasteboardCreationRequest"/>.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null, empty or whitespace.</exception>
        public static IosPasteboardCreationRequest Named(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Pasteboard name must not be blank.", nameof(name));
            }
            return new IosPasteboardCreationRequest(IosPasteboardCreationRequestKind.Named, name);
        }

        private IosPasteboardCreationRequest(IosPasteboardCreationRequestKind kind, string? name)
        {
            Kind = kind;
            Name = name;
        }
    }

    /// <summary>Kind of content written to the pasteboard.</summary>
    public enum IosClipboardContentKind
    {
        /// <summary>Plain text.</summary>
        PlainText,

        /// <summary>HTML with a plain-text fallback.</summary>
        HtmlText,

        /// <summary>A URL.</summary>
        Url,

        /// <summary>An image referenced by local file path.</summary>
        ImageFile,

        /// <summary>Raw image bytes with a uniform type identifier.</summary>
        ImageData,

        /// <summary>An RGBA color.</summary>
        Color,

        /// <summary>Arbitrary bytes with a uniform type identifier.</summary>
        CustomData,

        /// <summary>Multiple plain-text items of the same form.</summary>
        MultipleText,

        /// <summary>One item carrying several representations keyed by uniform type identifier.</summary>
        MultiRepresentation
    }

    /// <summary>
    /// Content written by <see cref="IosClipboardManager.Copy"/> / <see cref="IosClipboardManager.Append"/>.
    /// <para>
    /// Values that the native layer validates (blank text, malformed URL, out-of-range color
    /// components, oversized payloads) are deliberately NOT re-validated here so that a single,
    /// stable error contract applies. Only call-site bugs throw.
    /// </para>
    /// <para>
    /// Large binary payloads are base64-encoded into the request JSON. Prefer
    /// <see cref="ImageFile"/> for large images to avoid the encoding overhead.
    /// </para>
    /// </summary>
    public sealed class IosClipboardContent
    {
        /// <summary>Which kind of content this instance carries.</summary>
        public IosClipboardContentKind Kind { get; }

        internal string? Text { get; private set; }
        internal string? Plain { get; private set; }
        internal string? Html { get; private set; }
        internal string? UrlString { get; private set; }
        internal string? Path { get; private set; }
        internal byte[]? Data { get; private set; }
        internal string? UtType { get; private set; }
        internal double Red { get; private set; }
        internal double Green { get; private set; }
        internal double Blue { get; private set; }
        internal double Alpha { get; private set; }
        internal string[]? Texts { get; private set; }
        internal IReadOnlyDictionary<string, byte[]>? Representations { get; private set; }

        /// <summary>
        /// Creates plain-text content. Blank text is accepted by the native layer.
        /// </summary>
        /// <param name="text">Text to copy.</param>
        /// <returns>A plain-text <see cref="IosClipboardContent"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
        public static IosClipboardContent PlainText(string text) =>
            new(IosClipboardContentKind.PlainText) { Text = RequireNotNull(text, nameof(text)) };

        /// <summary>
        /// Creates HTML content with a plain-text fallback.
        /// </summary>
        /// <param name="plain">Plain-text fallback.</param>
        /// <param name="html">HTML markup.</param>
        /// <returns>An HTML <see cref="IosClipboardContent"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when either argument is null.</exception>
        public static IosClipboardContent HtmlText(string plain, string html) =>
            new(IosClipboardContentKind.HtmlText)
            {
                Plain = RequireNotNull(plain, nameof(plain)),
                Html = RequireNotNull(html, nameof(html))
            };

        /// <summary>
        /// Creates URL content.
        /// </summary>
        /// <param name="urlString">URL string. Validity is checked by the native layer.</param>
        /// <returns>A URL <see cref="IosClipboardContent"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="urlString"/> is null.</exception>
        public static IosClipboardContent Url(string urlString) =>
            new(IosClipboardContentKind.Url) { UrlString = RequireNotNull(urlString, nameof(urlString)) };

        /// <summary>
        /// Creates image content referenced by local file path. Preferred over
        /// <see cref="ImageData"/> for large images: no base64 payload crosses the bridge.
        /// </summary>
        /// <param name="path">Local file path to the image.</param>
        /// <returns>An image-file <see cref="IosClipboardContent"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> is null.</exception>
        public static IosClipboardContent ImageFile(string path) =>
            new(IosClipboardContentKind.ImageFile) { Path = RequireNotNull(path, nameof(path)) };

        /// <summary>
        /// Creates image content from raw bytes.
        /// </summary>
        /// <param name="data">Image bytes. Base64-encoded into the request JSON.</param>
        /// <param name="utType">Uniform type identifier of the image data.</param>
        /// <returns>An image-data <see cref="IosClipboardContent"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when either argument is null.</exception>
        public static IosClipboardContent ImageData(byte[] data, string utType) =>
            new(IosClipboardContentKind.ImageData)
            {
                Data = RequireNotNull(data, nameof(data)),
                UtType = RequireNotNull(utType, nameof(utType))
            };

        /// <summary>
        /// Creates color content. Component range (0.0 to 1.0) is validated by the native layer.
        /// </summary>
        /// <param name="red">Red component.</param>
        /// <param name="green">Green component.</param>
        /// <param name="blue">Blue component.</param>
        /// <param name="alpha">Alpha component.</param>
        /// <returns>A color <see cref="IosClipboardContent"/>.</returns>
        /// <exception cref="ArgumentException">Thrown when any component is NaN or infinity.</exception>
        public static IosClipboardContent Color(double red, double green, double blue, double alpha)
        {
            // Non-finite doubles cannot be represented in JSON. Emitting them would produce
            // malformed JSON that the native parser reports as CLIPBOARD_INVALID_REQUEST, hiding
            // the real cause (which would be CLIPBOARD_INVALID_COLOR).
            RequireFinite(red, nameof(red));
            RequireFinite(green, nameof(green));
            RequireFinite(blue, nameof(blue));
            RequireFinite(alpha, nameof(alpha));
            return new IosClipboardContent(IosClipboardContentKind.Color)
            {
                Red = red,
                Green = green,
                Blue = blue,
                Alpha = alpha
            };
        }

        /// <summary>
        /// Creates arbitrary binary content.
        /// </summary>
        /// <param name="data">Bytes. Base64-encoded into the request JSON.</param>
        /// <param name="utType">Uniform type identifier of the data.</param>
        /// <returns>A custom-data <see cref="IosClipboardContent"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when either argument is null.</exception>
        public static IosClipboardContent CustomData(byte[] data, string utType) =>
            new(IosClipboardContentKind.CustomData)
            {
                Data = RequireNotNull(data, nameof(data)),
                UtType = RequireNotNull(utType, nameof(utType))
            };

        /// <summary>
        /// Creates several plain-text items of the same form.
        /// </summary>
        /// <param name="texts">Text items. An empty array fails with CLIPBOARD_EMPTY_ITEMS natively.</param>
        /// <returns>A multiple-text <see cref="IosClipboardContent"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="texts"/> is null.</exception>
        public static IosClipboardContent MultipleText(string[] texts) =>
            new(IosClipboardContentKind.MultipleText) { Texts = RequireNotNull(texts, nameof(texts)) };

        /// <summary>
        /// Creates one item carrying several representations, keyed by uniform type identifier.
        /// </summary>
        /// <param name="representations">Uniform type identifier to bytes. Each value is base64-encoded.</param>
        /// <returns>A multi-representation <see cref="IosClipboardContent"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="representations"/> is null.</exception>
        public static IosClipboardContent MultiRepresentation(IReadOnlyDictionary<string, byte[]> representations) =>
            new(IosClipboardContentKind.MultiRepresentation)
            {
                Representations = RequireNotNull(representations, nameof(representations))
            };

        private IosClipboardContent(IosClipboardContentKind kind)
        {
            Kind = kind;
        }

        private static T RequireNotNull<T>(T value, string parameterName) where T : class =>
            value ?? throw new ArgumentNullException(parameterName);

        private static void RequireFinite(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentException("Color components must be finite.", parameterName);
            }
        }
    }

    /// <summary>
    /// Privacy options for <see cref="IosClipboardManager.Copy"/>.
    /// <para>
    /// Deliberately a class rather than a struct: <c>default(T)</c> of a struct would yield
    /// <c>LocalOnly == false</c>, silently inverting the privacy-preserving default.
    /// </para>
    /// <para>
    /// <see cref="IosClipboardManager.Append"/> cannot carry these options, and does not inherit
    /// options set by a prior copy. Always use <c>Copy</c> for sensitive data.
    /// </para>
    /// </summary>
    public sealed class IosClipboardCopyOptions
    {
        /// <summary>When <c>true</c>, content is not transferred to nearby devices via Universal Clipboard.</summary>
        public bool LocalOnly { get; }

        /// <summary>When set, the system removes the item after this date. Must be in the future.</summary>
        public DateTime? ExpirationDate { get; }

        /// <summary>
        /// The safe default matching the native <c>ClipboardCopyOptions.default</c>: local only,
        /// no expiration. Equivalent to passing no options at all.
        /// </summary>
        public static IosClipboardCopyOptions PrivacyPreservingDefault { get; } = new(true, null);

        /// <summary>
        /// Creates copy options.
        /// </summary>
        /// <param name="localOnly">Whether to keep the content off Universal Clipboard.</param>
        /// <param name="expirationDate">Optional expiry. Converted to UTC when serialized.</param>
        /// <returns>An <see cref="IosClipboardCopyOptions"/>.</returns>
        public static IosClipboardCopyOptions Create(bool localOnly, DateTime? expirationDate = null) =>
            new(localOnly, expirationDate);

        private IosClipboardCopyOptions(bool localOnly, DateTime? expirationDate)
        {
            LocalOnly = localOnly;
            ExpirationDate = expirationDate;
        }
    }

    /// <summary>Kind of asynchronous item load.</summary>
    public enum IosClipboardLoadRequestKind
    {
        /// <summary>Load text.</summary>
        Text,

        /// <summary>Load a URL.</summary>
        Url,

        /// <summary>Load an image, re-encoded as PNG by the native layer.</summary>
        Image,

        /// <summary>Load a file of a specific uniform type identifier.</summary>
        File
    }

    /// <summary>
    /// Request passed to <see cref="IosClipboardManager.LoadItem"/>.
    /// </summary>
    public sealed class IosClipboardLoadRequest
    {
        /// <summary>Which kind of item to load.</summary>
        public IosClipboardLoadRequestKind Kind { get; }

        /// <summary>Uniform type identifier. Non-null only for <see cref="IosClipboardLoadRequestKind.File"/>.</summary>
        public string? UtType { get; }

        /// <summary>Loads text from the pasteboard's item providers.</summary>
        public static IosClipboardLoadRequest Text { get; } = new(IosClipboardLoadRequestKind.Text, null);

        /// <summary>Loads a URL from the pasteboard's item providers.</summary>
        public static IosClipboardLoadRequest Url { get; } = new(IosClipboardLoadRequestKind.Url, null);

        /// <summary>Loads an image, re-encoded as PNG on a background executor.</summary>
        public static IosClipboardLoadRequest Image { get; } = new(IosClipboardLoadRequestKind.Image, null);

        /// <summary>
        /// Loads a file of the given uniform type identifier. The native layer copies it to a
        /// temporary location and returns its path.
        /// </summary>
        /// <param name="utType">Uniform type identifier to load.</param>
        /// <returns>A file <see cref="IosClipboardLoadRequest"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="utType"/> is null.</exception>
        public static IosClipboardLoadRequest File(string utType) =>
            new(IosClipboardLoadRequestKind.File, utType ?? throw new ArgumentNullException(nameof(utType)));

        private IosClipboardLoadRequest(IosClipboardLoadRequestKind kind, string? utType)
        {
            Kind = kind;
            UtType = utType;
        }
    }

    /// <summary>
    /// Content pattern the data detection system can identify on the pasteboard without reading
    /// (and thus without prompting for) its body.
    /// </summary>
    public enum IosClipboardDetectionPattern
    {
        /// <summary>A string that is probably a web URL.</summary>
        ProbableWebUrl,

        /// <summary>A string that is probably a web search term.</summary>
        ProbableWebSearch,

        /// <summary>A number.</summary>
        Number,

        /// <summary>A link.</summary>
        Link,

        /// <summary>An email address.</summary>
        EmailAddress,

        /// <summary>A phone number.</summary>
        PhoneNumber,

        /// <summary>A postal address.</summary>
        PostalAddress,

        /// <summary>A calendar event.</summary>
        CalendarEvent,

        /// <summary>A flight number.</summary>
        FlightNumber,

        /// <summary>A money amount.</summary>
        MoneyAmount,

        /// <summary>A shipment tracking number.</summary>
        ShipmentTrackingNumber
    }

    /// <summary>
    /// Maps <see cref="IosClipboardDetectionPattern"/> to and from the native raw values.
    /// The strings must match <c>ClipboardDetectionPattern</c> exactly: the native parser rejects
    /// the whole request if a single value is unknown.
    /// </summary>
    internal static class IosClipboardDetectionPatternExtensions
    {
        private static readonly Dictionary<IosClipboardDetectionPattern, string> RawValues = new()
        {
            { IosClipboardDetectionPattern.ProbableWebUrl, "probableWebURL" },
            { IosClipboardDetectionPattern.ProbableWebSearch, "probableWebSearch" },
            { IosClipboardDetectionPattern.Number, "number" },
            { IosClipboardDetectionPattern.Link, "link" },
            { IosClipboardDetectionPattern.EmailAddress, "emailAddress" },
            { IosClipboardDetectionPattern.PhoneNumber, "phoneNumber" },
            { IosClipboardDetectionPattern.PostalAddress, "postalAddress" },
            { IosClipboardDetectionPattern.CalendarEvent, "calendarEvent" },
            { IosClipboardDetectionPattern.FlightNumber, "flightNumber" },
            { IosClipboardDetectionPattern.MoneyAmount, "moneyAmount" },
            { IosClipboardDetectionPattern.ShipmentTrackingNumber, "shipmentTrackingNumber" }
        };

        private static readonly Dictionary<string, IosClipboardDetectionPattern> Patterns = BuildReverse();

        internal static string ToRawValue(this IosClipboardDetectionPattern pattern) =>
            RawValues.TryGetValue(pattern, out string? raw) ? raw : pattern.ToString();

        internal static bool TryParse(string? rawValue, out IosClipboardDetectionPattern pattern)
        {
            if (rawValue != null && Patterns.TryGetValue(rawValue, out pattern))
            {
                return true;
            }
            pattern = default;
            return false;
        }

        private static Dictionary<string, IosClipboardDetectionPattern> BuildReverse()
        {
            var reverse = new Dictionary<string, IosClipboardDetectionPattern>(RawValues.Count, StringComparer.Ordinal);
            foreach (var pair in RawValues)
            {
                reverse[pair.Value] = pair.Key;
            }
            return reverse;
        }
    }
}
#endif
