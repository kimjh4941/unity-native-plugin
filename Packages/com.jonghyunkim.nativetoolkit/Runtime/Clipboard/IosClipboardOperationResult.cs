#nullable enable

#if UNITY_IOS || UNITY_EDITOR
// Intentional deviation from the "log every public method" rule in csharp.md: these result types
// carry clipboard content and native error detail. The factories are pure value constructors, so
// they emit no logs at all rather than a shape-only line — the Manager already logs operation,
// status and error code at the dispatch boundary. This matches the native ClipboardRedaction policy.

namespace JonghyunKim.NativeToolkit.Runtime.Clipboard
{
    /// <summary>
    /// Completion result of an iOS clipboard operation that returns no payload
    /// (copy, append, clear, removePasteboard, cancelLoads, startObserving, stopObserving).
    /// <para>
    /// Distinct from the Android <c>ClipboardOperationResult</c>: the iOS bridge reports a stable
    /// error code alongside the message, so failures expose an <see cref="IosClipboardErrorInfo"/>.
    /// </para>
    /// </summary>
    public readonly struct IosClipboardOperationResult
    {
        /// <summary>Operation name that produced this result. Never null.</summary>
        public string Operation { get; }

        /// <summary>Whether the operation succeeded.</summary>
        public bool IsSuccess { get; }

        /// <summary>Error detail. Non-null if and only if <see cref="IsSuccess"/> is <c>false</c>.</summary>
        public IosClipboardErrorInfo? Error { get; }

        /// <summary>
        /// Creates a successful result.
        /// </summary>
        /// <param name="operation">Operation name that succeeded.</param>
        /// <returns>A successful <see cref="IosClipboardOperationResult"/>.</returns>
        public static IosClipboardOperationResult Success(string operation) =>
            new(operation, true, null);

        /// <summary>
        /// Creates a failed result from a raw code and message, normalizing both.
        /// </summary>
        /// <param name="operation">Operation name that failed.</param>
        /// <param name="errorCode">Stable error code, or <c>null</c> to fall back to CLIPBOARD_UNKNOWN.</param>
        /// <param name="errorMessage">Error message, or <c>null</c> to fall back to the default message.</param>
        /// <returns>A failed <see cref="IosClipboardOperationResult"/>.</returns>
        public static IosClipboardOperationResult Failure(string operation, string? errorCode, string? errorMessage) =>
            new(operation, false, IosClipboardErrorInfo.Create(errorCode, errorMessage));

        /// <summary>
        /// Creates a failed result from an already-built error info.
        /// </summary>
        /// <param name="operation">Operation name that failed.</param>
        /// <param name="error">Error detail to attach.</param>
        /// <returns>A failed <see cref="IosClipboardOperationResult"/>.</returns>
        public static IosClipboardOperationResult Failure(string operation, IosClipboardErrorInfo error) =>
            new(operation, false, error);

        private IosClipboardOperationResult(string operation, bool isSuccess, IosClipboardErrorInfo? error)
        {
            Operation = operation;
            IsSuccess = isSuccess;
            Error = error;
        }
    }
}
#endif
