#nullable enable

#if UNITY_IOS || UNITY_EDITOR
// Intentional deviation from the "log every public method" rule in csharp.md: these result types
// carry clipboard content and native error detail. The factories are pure value constructors, so
// they emit no logs at all rather than a shape-only line — the Manager already logs operation,
// status and error code at the dispatch boundary. This matches the native ClipboardRedaction policy.

namespace JonghyunKim.NativeToolkit.Runtime.Clipboard
{
    using System.Collections.Generic;

    /// <summary>
    /// Metadata-only view of the pasteboard, built by the native layer exclusively from system
    /// APIs documented as avoiding user notifications and prompts.
    /// </summary>
    public sealed class IosClipboardSnapshot
    {
        /// <summary>Whether the pasteboard holds string content.</summary>
        public bool HasStrings { get; }

        /// <summary>Whether the pasteboard holds URL content.</summary>
        public bool HasUrls { get; }

        /// <summary>Whether the pasteboard holds image content.</summary>
        public bool HasImages { get; }

        /// <summary>Whether the pasteboard holds color content.</summary>
        public bool HasColors { get; }

        /// <summary>Number of items on the pasteboard.</summary>
        public int NumberOfItems { get; }

        /// <summary>Representation types of the first item. Never null.</summary>
        public IReadOnlyList<string> TypeIdentifiers { get; }

        /// <summary>Representation types of every item, in clipboard order. Never null.</summary>
        public IReadOnlyList<IReadOnlyList<string>> AllTypeIdentifiers { get; }

        /// <summary>
        /// Indexes of items matching the requested <c>matchingTypes</c>.
        /// <c>null</c> means no <c>matchingTypes</c> were requested; an empty list means they were
        /// requested and nothing matched. The two are deliberately distinguishable.
        /// </summary>
        public IReadOnlyList<int>? MatchingItemIndexes { get; }

        internal IosClipboardSnapshot(
            bool hasStrings,
            bool hasUrls,
            bool hasImages,
            bool hasColors,
            int numberOfItems,
            IReadOnlyList<string> typeIdentifiers,
            IReadOnlyList<IReadOnlyList<string>> allTypeIdentifiers,
            IReadOnlyList<int>? matchingItemIndexes)
        {
            HasStrings = hasStrings;
            HasUrls = hasUrls;
            HasImages = hasImages;
            HasColors = hasColors;
            NumberOfItems = numberOfItems;
            TypeIdentifiers = typeIdentifiers;
            AllTypeIdentifiers = allTypeIdentifiers;
            MatchingItemIndexes = matchingItemIndexes;
        }
    }

    /// <summary>
    /// Result of <see cref="IosClipboardManager.GetSnapshot"/>.
    /// </summary>
    public readonly struct IosClipboardSnapshotResult
    {
        /// <summary>Whether the snapshot succeeded.</summary>
        public bool IsSuccess { get; }

        /// <summary>Error detail. Non-null if and only if <see cref="IsSuccess"/> is <c>false</c>.</summary>
        public IosClipboardErrorInfo? Error { get; }

        /// <summary>Snapshot data. Non-null if and only if <see cref="IsSuccess"/> is <c>true</c>.</summary>
        public IosClipboardSnapshot? Snapshot { get; }

        /// <summary>
        /// Creates a successful snapshot result.
        /// </summary>
        /// <param name="snapshot">Snapshot data.</param>
        /// <returns>A successful <see cref="IosClipboardSnapshotResult"/>.</returns>
        internal static IosClipboardSnapshotResult Success(IosClipboardSnapshot snapshot) =>
            new(true, null, snapshot);

        /// <summary>
        /// Creates a failed snapshot result.
        /// </summary>
        /// <param name="errorCode">Stable error code.</param>
        /// <param name="errorMessage">Error message.</param>
        /// <returns>A failed <see cref="IosClipboardSnapshotResult"/>.</returns>
        internal static IosClipboardSnapshotResult Failure(string? errorCode, string? errorMessage) =>
            Failure(IosClipboardErrorInfo.Create(errorCode, errorMessage));

        /// <summary>
        /// Creates a failed snapshot result from an already-built error info.
        /// </summary>
        /// <param name="error">Error detail to attach.</param>
        /// <returns>A failed <see cref="IosClipboardSnapshotResult"/>.</returns>
        internal static IosClipboardSnapshotResult Failure(IosClipboardErrorInfo error) =>
            new(false, error, null);

        private IosClipboardSnapshotResult(bool isSuccess, IosClipboardErrorInfo? error, IosClipboardSnapshot? snapshot)
        {
            IsSuccess = isSuccess;
            Error = error;
            Snapshot = snapshot;
        }
    }
}
#endif
