#nullable enable

#if UNITY_STANDALONE_OSX || UNITY_EDITOR
// Intentional deviation from the "log every public method" rule in csharp.md: these result types
// carry clipboard content and native error detail. The factories are pure value constructors, so
// they emit no logs at all rather than a shape-only line. The Manager already logs operation,
// success and error code at the dispatch boundary.

namespace JonghyunKim.NativeToolkit.Runtime.Clipboard
{
    using System.Collections.Generic;

    /// <summary>
    /// What a pasteboard holds, without its payloads.
    /// <para>
    /// Reading types rather than bytes is an optimisation, not a privacy contract: macOS may still
    /// tell the person using the app.
    /// </para>
    /// </summary>
    public sealed class MacClipboardSnapshot
    {
        /// <summary>Change count at the time of the snapshot.</summary>
        public long ChangeCount { get; }

        /// <summary>Uniform type identifiers per item, in pasteboard order. Never null.</summary>
        public IReadOnlyList<IReadOnlyList<string>> ItemTypes { get; }

        /// <summary>
        /// Indexes into <see cref="ItemTypes"/> that matched the requested filter. Never null;
        /// empty when no filter was supplied or nothing matched. <c>int</c> because these index a
        /// managed collection.
        /// </summary>
        public IReadOnlyList<int> MatchingItemIndexes { get; }

        internal MacClipboardSnapshot(
            long changeCount,
            IReadOnlyList<IReadOnlyList<string>> itemTypes,
            IReadOnlyList<int> matchingItemIndexes)
        {
            ChangeCount = changeCount;
            ItemTypes = itemTypes;
            MatchingItemIndexes = matchingItemIndexes;
        }
    }

    /// <summary>
    /// Result of a snapshot.
    /// <para><c>default</c> is an uninitialised value, not a failure.</para>
    /// </summary>
    public readonly struct MacClipboardSnapshotResult
    {
        /// <summary>Whether the snapshot succeeded.</summary>
        public bool IsSuccess { get; }

        /// <summary>Error detail. Non-null if and only if <see cref="IsSuccess"/> is <c>false</c>.</summary>
        public MacClipboardErrorInfo? Error { get; }

        /// <summary>Snapshot. Non-null if and only if <see cref="IsSuccess"/> is <c>true</c>.</summary>
        public MacClipboardSnapshot? Snapshot { get; }

        /// <summary>
        /// Creates a successful result.
        /// </summary>
        /// <param name="snapshot">Snapshot that was taken.</param>
        /// <returns>A successful <see cref="MacClipboardSnapshotResult"/>.</returns>
        public static MacClipboardSnapshotResult Success(MacClipboardSnapshot snapshot) =>
            new(true, null, snapshot);

        /// <summary>
        /// Creates a failed result.
        /// </summary>
        /// <param name="code">Numeric error code.</param>
        /// <param name="message">Error message.</param>
        /// <returns>A failed <see cref="MacClipboardSnapshotResult"/>.</returns>
        public static MacClipboardSnapshotResult Failure(long code, string? message) =>
            new(false, MacClipboardErrorInfo.Create(code, message), null);

        private MacClipboardSnapshotResult(bool isSuccess, MacClipboardErrorInfo? error, MacClipboardSnapshot? snapshot)
        {
            IsSuccess = isSuccess;
            Error = error;
            Snapshot = snapshot;
        }
    }
}
#endif
