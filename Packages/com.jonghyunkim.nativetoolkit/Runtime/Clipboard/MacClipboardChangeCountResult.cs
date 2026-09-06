#nullable enable

#if UNITY_STANDALONE_OSX || UNITY_EDITOR
// Intentional deviation from the "log every public method" rule in csharp.md: these result types
// carry clipboard content and native error detail. The factories are pure value constructors, so
// they emit no logs at all rather than a shape-only line. The Manager already logs operation,
// success and error code at the dispatch boundary.

namespace JonghyunKim.NativeToolkit.Runtime.Clipboard
{
    /// <summary>
    /// Result of clearing a pasteboard, reporting the change count after the clear.
    /// <para>
    /// <see cref="ChangeCount"/> is a value type, so it is 0 on failure rather than null. Check
    /// <see cref="IsSuccess"/> before reading it.
    /// </para>
    /// <para><c>default</c> is an uninitialised value, not a failure.</para>
    /// </summary>
    public readonly struct MacClipboardChangeCountResult
    {
        /// <summary>Whether the clear succeeded.</summary>
        public bool IsSuccess { get; }

        /// <summary>Error detail. Non-null if and only if <see cref="IsSuccess"/> is <c>false</c>.</summary>
        public MacClipboardErrorInfo? Error { get; }

        /// <summary>Change count after the clear. 0 on failure.</summary>
        public long ChangeCount { get; }

        /// <summary>
        /// Creates a successful result.
        /// </summary>
        /// <param name="changeCount">Change count reported by the native layer.</param>
        /// <returns>A successful <see cref="MacClipboardChangeCountResult"/>.</returns>
        public static MacClipboardChangeCountResult Success(long changeCount) => new(true, null, changeCount);

        /// <summary>
        /// Creates a failed result.
        /// </summary>
        /// <param name="code">Numeric error code.</param>
        /// <param name="message">Error message.</param>
        /// <returns>A failed <see cref="MacClipboardChangeCountResult"/>.</returns>
        public static MacClipboardChangeCountResult Failure(long code, string? message) =>
            new(false, MacClipboardErrorInfo.Create(code, message), 0);

        private MacClipboardChangeCountResult(bool isSuccess, MacClipboardErrorInfo? error, long changeCount)
        {
            IsSuccess = isSuccess;
            Error = error;
            ChangeCount = changeCount;
        }
    }
}
#endif
