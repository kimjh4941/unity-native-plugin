#nullable enable

#if UNITY_STANDALONE_OSX || UNITY_EDITOR
// Intentional deviation from the "log every public method" rule in csharp.md: these result types
// carry clipboard content and native error detail. The factories are pure value constructors, so
// they emit no logs at all rather than a shape-only line. The Manager already logs operation,
// success and error code at the dispatch boundary.

namespace JonghyunKim.NativeToolkit.Runtime.Clipboard
{
    /// <summary>
    /// Result of a copy or append. Both return the ownership needed to append again.
    /// <para>
    /// <see cref="Operation"/> is required here because copy and append share this type: without
    /// it a subscriber cannot tell which write completed, and cannot tell that
    /// <see cref="MacClipboardErrorCodes.OwnershipLost"/> belongs to append.
    /// </para>
    /// <para><c>default</c> is an uninitialised value, not a failure.</para>
    /// </summary>
    public readonly struct MacClipboardOwnershipResult
    {
        /// <summary>Operation that produced this result: copy or append.</summary>
        public string Operation { get; }

        /// <summary>Whether the write succeeded.</summary>
        public bool IsSuccess { get; }

        /// <summary>Error detail. Non-null if and only if <see cref="IsSuccess"/> is <c>false</c>.</summary>
        public MacClipboardErrorInfo? Error { get; }

        /// <summary>
        /// Ownership of the pasteboard after the write. Non-null if and only if
        /// <see cref="IsSuccess"/> is <c>true</c>. Pass it to the next append.
        /// </summary>
        public MacPasteboardOwnership? Ownership { get; }

        /// <summary>
        /// Creates a successful result.
        /// </summary>
        /// <param name="operation">Operation that succeeded.</param>
        /// <param name="ownership">Ownership reported by the native layer.</param>
        /// <returns>A successful <see cref="MacClipboardOwnershipResult"/>.</returns>
        public static MacClipboardOwnershipResult Success(string operation, MacPasteboardOwnership ownership) =>
            new(operation, true, null, ownership);

        /// <summary>
        /// Creates a failed result.
        /// </summary>
        /// <param name="operation">Operation that failed.</param>
        /// <param name="code">Numeric error code.</param>
        /// <param name="message">Error message.</param>
        /// <returns>A failed <see cref="MacClipboardOwnershipResult"/>.</returns>
        public static MacClipboardOwnershipResult Failure(string operation, long code, string? message) =>
            new(operation, false, MacClipboardErrorInfo.Create(code, message), null);

        private MacClipboardOwnershipResult(
            string operation, bool isSuccess, MacClipboardErrorInfo? error, MacPasteboardOwnership? ownership)
        {
            Operation = operation;
            IsSuccess = isSuccess;
            Error = error;
            Ownership = ownership;
        }
    }
}
#endif
