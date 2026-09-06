#nullable enable

#if UNITY_STANDALONE_OSX || UNITY_EDITOR
// Intentional deviation from the "log every public method" rule in csharp.md: these result types
// carry clipboard content and native error detail. The factories are pure value constructors, so
// they emit no logs at all rather than a shape-only line. The Manager already logs operation,
// success and error code at the dispatch boundary.

namespace JonghyunKim.NativeToolkit.Runtime.Clipboard
{
    /// <summary>
    /// Completion of a macOS clipboard operation that returns no payload:
    /// <c>removePasteboard</c>, <c>startObserving</c> and <c>stopObserving</c>.
    /// <para>
    /// <c>default</c> is an uninitialised value, not a failure: it carries no error and no
    /// operation. Only values produced by <see cref="Success"/> and <see cref="Failure"/> satisfy
    /// the invariants documented here.
    /// </para>
    /// </summary>
    public readonly struct MacClipboardOperationResult
    {
        /// <summary>Operation that produced this result. See <see cref="MacClipboardOperations"/>.</summary>
        public string Operation { get; }

        /// <summary>Whether the operation succeeded.</summary>
        public bool IsSuccess { get; }

        /// <summary>Error detail. Non-null if and only if <see cref="IsSuccess"/> is <c>false</c>.</summary>
        public MacClipboardErrorInfo? Error { get; }

        /// <summary>
        /// Creates a successful result.
        /// </summary>
        /// <param name="operation">Operation that succeeded.</param>
        /// <returns>A successful <see cref="MacClipboardOperationResult"/>.</returns>
        public static MacClipboardOperationResult Success(string operation) =>
            new(operation, true, null);

        /// <summary>
        /// Creates a failed result.
        /// </summary>
        /// <param name="operation">Operation that failed.</param>
        /// <param name="code">Numeric error code.</param>
        /// <param name="message">Error message.</param>
        /// <returns>A failed <see cref="MacClipboardOperationResult"/>.</returns>
        public static MacClipboardOperationResult Failure(string operation, long code, string? message) =>
            new(operation, false, MacClipboardErrorInfo.Create(code, message));

        private MacClipboardOperationResult(string operation, bool isSuccess, MacClipboardErrorInfo? error)
        {
            Operation = operation;
            IsSuccess = isSuccess;
            Error = error;
        }
    }
}
#endif
