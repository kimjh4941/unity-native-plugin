#nullable enable

#if UNITY_IOS || UNITY_EDITOR
// Intentional deviation from the "log every public method" rule in csharp.md: these result types
// carry clipboard content and native error detail. The factories are pure value constructors, so
// they emit no logs at all rather than a shape-only line — the Manager already logs operation,
// status and error code at the dispatch boundary. This matches the native ClipboardRedaction policy.

namespace JonghyunKim.NativeToolkit.Runtime.Clipboard
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A single pasteboard item as reported by a synchronous read.
    /// Large payloads (image bytes) are not included; only the uniform type identifier is
    /// reported. Use <c>ReadData</c> or <c>LoadItem</c> to retrieve the body.
    /// </summary>
    public sealed class IosClipboardItem
    {
        /// <summary>Representation types of this item. Never null; empty when the key was absent.</summary>
        public IReadOnlyList<string> TypeIdentifiers { get; }

        /// <summary>Plain text, or <c>null</c> when the item holds no text.</summary>
        public string? Text { get; }

        /// <summary>URL string, or <c>null</c> when the item holds no URL.</summary>
        public string? UrlString { get; }

        /// <summary>Uniform type identifier of the item's image data, or <c>null</c>.</summary>
        public string? ImageDataUtType { get; }

        internal IosClipboardItem(
            IReadOnlyList<string> typeIdentifiers,
            string? text,
            string? urlString,
            string? imageDataUtType)
        {
            TypeIdentifiers = typeIdentifiers;
            Text = text;
            UrlString = urlString;
            ImageDataUtType = imageDataUtType;
        }
    }

    /// <summary>
    /// Result of <see cref="IosClipboardManager.Read"/>.
    /// An empty clipboard is a successful result with zero <see cref="Items"/>, not a failure.
    /// </summary>
    public readonly struct IosClipboardReadResult
    {
        /// <summary>Whether the read succeeded.</summary>
        public bool IsSuccess { get; }

        /// <summary>Error detail. Non-null if and only if <see cref="IsSuccess"/> is <c>false</c>.</summary>
        public IosClipboardErrorInfo? Error { get; }

        /// <summary>Number of items the pasteboard reported. Zero on failure.</summary>
        public int NumberOfItems { get; }

        /// <summary>Items in clipboard order. Never null; empty on failure.</summary>
        public IReadOnlyList<IosClipboardItem> Items { get; }

        /// <summary>
        /// Creates a successful read result.
        /// </summary>
        /// <param name="numberOfItems">Item count reported by the pasteboard.</param>
        /// <param name="items">Items in clipboard order.</param>
        /// <returns>A successful <see cref="IosClipboardReadResult"/>.</returns>
        internal static IosClipboardReadResult Success(int numberOfItems, IReadOnlyList<IosClipboardItem> items) =>
            new(true, null, numberOfItems, items);

        /// <summary>
        /// Creates a failed read result.
        /// </summary>
        /// <param name="errorCode">Stable error code.</param>
        /// <param name="errorMessage">Error message.</param>
        /// <returns>A failed <see cref="IosClipboardReadResult"/>.</returns>
        internal static IosClipboardReadResult Failure(string? errorCode, string? errorMessage) =>
            Failure(IosClipboardErrorInfo.Create(errorCode, errorMessage));

        /// <summary>
        /// Creates a failed read result from an already-built error info.
        /// </summary>
        /// <param name="error">Error detail to attach.</param>
        /// <returns>A failed <see cref="IosClipboardReadResult"/>.</returns>
        internal static IosClipboardReadResult Failure(IosClipboardErrorInfo error) =>
            new(false, error, 0, Array.Empty<IosClipboardItem>());

        private IosClipboardReadResult(
            bool isSuccess,
            IosClipboardErrorInfo? error,
            int numberOfItems,
            IReadOnlyList<IosClipboardItem> items)
        {
            IsSuccess = isSuccess;
            Error = error;
            NumberOfItems = numberOfItems;
            Items = items;
        }
    }
}
#endif
