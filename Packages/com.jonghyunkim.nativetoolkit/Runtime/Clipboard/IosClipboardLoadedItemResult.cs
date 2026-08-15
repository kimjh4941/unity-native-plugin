#nullable enable

#if UNITY_IOS || UNITY_EDITOR
// Intentional deviation from the "log every public method" rule in csharp.md: these result types
// carry clipboard content and native error detail. The factories are pure value constructors, so
// they emit no logs at all rather than a shape-only line — the Manager already logs operation,
// status and error code at the dispatch boundary. This matches the native ClipboardRedaction policy.

namespace JonghyunKim.NativeToolkit.Runtime.Clipboard
{
    /// <summary>Kind of item returned by <see cref="IosClipboardManager.LoadItem"/>.</summary>
    public enum IosClipboardLoadedItemKind
    {
        /// <summary>Text.</summary>
        Text,

        /// <summary>A URL.</summary>
        Url,

        /// <summary>Image bytes, re-encoded as PNG by the native layer.</summary>
        ImageData,

        /// <summary>A file copied to a temporary location.</summary>
        File,

        /// <summary>
        /// A kind this version does not recognize. Reported by the native layer as a successful
        /// result, so it is surfaced as success rather than converted into an error.
        /// </summary>
        Unknown
    }

    /// <summary>
    /// An item loaded from the pasteboard's item providers.
    /// Only the properties matching <see cref="Kind"/> are non-null.
    /// </summary>
    public sealed class IosClipboardLoadedItem
    {
        /// <summary>Which kind of item this instance carries.</summary>
        public IosClipboardLoadedItemKind Kind { get; }

        /// <summary>Text. Non-null only when <see cref="Kind"/> is <see cref="IosClipboardLoadedItemKind.Text"/>.</summary>
        public string? Text { get; }

        /// <summary>URL string. Non-null only when <see cref="Kind"/> is <see cref="IosClipboardLoadedItemKind.Url"/>.</summary>
        public string? UrlString { get; }

        /// <summary>Image bytes. Non-null only when <see cref="Kind"/> is <see cref="IosClipboardLoadedItemKind.ImageData"/>.</summary>
        public byte[]? Data { get; }

        /// <summary>Uniform type identifier. Non-null only when <see cref="Kind"/> is <see cref="IosClipboardLoadedItemKind.ImageData"/>.</summary>
        public string? UtType { get; }

        /// <summary>
        /// Temporary file path. Non-null only when <see cref="Kind"/> is
        /// <see cref="IosClipboardLoadedItemKind.File"/>. The caller owns the file and is
        /// responsible for deleting it.
        /// </summary>
        public string? Path { get; }

        internal static IosClipboardLoadedItem FromText(string text) =>
            new(IosClipboardLoadedItemKind.Text, text, null, null, null, null);

        internal static IosClipboardLoadedItem FromUrl(string urlString) =>
            new(IosClipboardLoadedItemKind.Url, null, urlString, null, null, null);

        internal static IosClipboardLoadedItem FromImageData(byte[] data, string utType) =>
            new(IosClipboardLoadedItemKind.ImageData, null, null, data, utType, null);

        internal static IosClipboardLoadedItem FromFile(string path) =>
            new(IosClipboardLoadedItemKind.File, null, null, null, null, path);

        internal static IosClipboardLoadedItem UnknownKind() =>
            new(IosClipboardLoadedItemKind.Unknown, null, null, null, null, null);

        private IosClipboardLoadedItem(
            IosClipboardLoadedItemKind kind,
            string? text,
            string? urlString,
            byte[]? data,
            string? utType,
            string? path)
        {
            Kind = kind;
            Text = text;
            UrlString = urlString;
            Data = data;
            UtType = utType;
            Path = path;
        }
    }

    /// <summary>
    /// Result of <see cref="IosClipboardManager.LoadItem"/>.
    /// <para>
    /// A cancelled load fails with <c>CLIPBOARD_CANCELLED</c>. The native layer documents that as a
    /// normal, ignorable outcome rather than a real error.
    /// </para>
    /// </summary>
    public readonly struct IosClipboardLoadedItemResult
    {
        /// <summary>Whether the load succeeded.</summary>
        public bool IsSuccess { get; }

        /// <summary>Error detail. Non-null if and only if <see cref="IsSuccess"/> is <c>false</c>.</summary>
        public IosClipboardErrorInfo? Error { get; }

        /// <summary>Loaded item. Non-null if and only if <see cref="IsSuccess"/> is <c>true</c>.</summary>
        public IosClipboardLoadedItem? Item { get; }

        /// <summary>
        /// Creates a successful result.
        /// </summary>
        /// <param name="item">Loaded item.</param>
        /// <returns>A successful <see cref="IosClipboardLoadedItemResult"/>.</returns>
        internal static IosClipboardLoadedItemResult Success(IosClipboardLoadedItem item) =>
            new(true, null, item);

        /// <summary>
        /// Creates a failed result.
        /// </summary>
        /// <param name="errorCode">Stable error code.</param>
        /// <param name="errorMessage">Error message.</param>
        /// <returns>A failed <see cref="IosClipboardLoadedItemResult"/>.</returns>
        internal static IosClipboardLoadedItemResult Failure(string? errorCode, string? errorMessage) =>
            Failure(IosClipboardErrorInfo.Create(errorCode, errorMessage));

        /// <summary>
        /// Creates a failed result from an already-built error info.
        /// </summary>
        /// <param name="error">Error detail to attach.</param>
        /// <returns>A failed <see cref="IosClipboardLoadedItemResult"/>.</returns>
        internal static IosClipboardLoadedItemResult Failure(IosClipboardErrorInfo error) =>
            new(false, error, null);

        private IosClipboardLoadedItemResult(bool isSuccess, IosClipboardErrorInfo? error, IosClipboardLoadedItem? item)
        {
            IsSuccess = isSuccess;
            Error = error;
            Item = item;
        }
    }
}
#endif
