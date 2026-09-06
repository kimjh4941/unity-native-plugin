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
    /// One item read from a pasteboard.
    /// <para>
    /// The pasteboard derives convertible types, so this can contain representations that were
    /// never written: text written as RTF also reads back as plain text. Do not assume a read
    /// mirrors a write.
    /// </para>
    /// </summary>
    public sealed class MacClipboardItem
    {
        /// <summary>Uniform type identifier to raw bytes. Never null; empty when the item had none.</summary>
        public IReadOnlyDictionary<string, byte[]> Representations { get; }

        internal MacClipboardItem(IReadOnlyDictionary<string, byte[]> representations)
        {
            Representations = representations;
        }
    }

    /// <summary>
    /// Everything a read returned.
    /// <para>
    /// Nested rather than flattened onto the result, unlike the iOS equivalent: the change count
    /// and the items describe the pasteboard at one instant, the same pairing ownership carries.
    /// </para>
    /// </summary>
    public sealed class MacClipboardReadContents
    {
        /// <summary>Change count at the time of the read. 64-bit; see <see cref="MacPasteboardOwnership.ChangeCount"/>.</summary>
        public long ChangeCount { get; }

        /// <summary>Items in pasteboard order. Never null; empty for an empty pasteboard.</summary>
        public IReadOnlyList<MacClipboardItem> Items { get; }

        internal MacClipboardReadContents(long changeCount, IReadOnlyList<MacClipboardItem> items)
        {
            ChangeCount = changeCount;
            Items = items;
        }
    }

    /// <summary>
    /// Result of a read. An empty pasteboard is a success with no items, not a failure.
    /// <para><c>default</c> is an uninitialised value, not a failure.</para>
    /// </summary>
    public readonly struct MacClipboardReadResult
    {
        /// <summary>Whether the read succeeded.</summary>
        public bool IsSuccess { get; }

        /// <summary>Error detail. Non-null if and only if <see cref="IsSuccess"/> is <c>false</c>.</summary>
        public MacClipboardErrorInfo? Error { get; }

        /// <summary>Contents. Non-null if and only if <see cref="IsSuccess"/> is <c>true</c>.</summary>
        public MacClipboardReadContents? Contents { get; }

        /// <summary>
        /// Creates a successful result.
        /// </summary>
        /// <param name="contents">Contents that were read.</param>
        /// <returns>A successful <see cref="MacClipboardReadResult"/>.</returns>
        public static MacClipboardReadResult Success(MacClipboardReadContents contents) =>
            new(true, null, contents);

        /// <summary>
        /// Creates a failed result.
        /// </summary>
        /// <param name="code">Numeric error code.</param>
        /// <param name="message">Error message.</param>
        /// <returns>A failed <see cref="MacClipboardReadResult"/>.</returns>
        public static MacClipboardReadResult Failure(long code, string? message) =>
            new(false, MacClipboardErrorInfo.Create(code, message), null);

        private MacClipboardReadResult(bool isSuccess, MacClipboardErrorInfo? error, MacClipboardReadContents? contents)
        {
            IsSuccess = isSuccess;
            Error = error;
            Contents = contents;
        }
    }
}
#endif
