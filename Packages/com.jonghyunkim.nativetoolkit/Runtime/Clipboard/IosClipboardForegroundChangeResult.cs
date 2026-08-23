#nullable enable

#if UNITY_IOS || UNITY_EDITOR
// Intentional deviation from the "log every public method" rule in csharp.md: these result types
// carry clipboard content and native error detail. The factories are pure value constructors, so
// they emit no logs at all rather than a shape-only line — the Manager already logs operation,
// status and error code at the dispatch boundary. This matches the native ClipboardRedaction policy.

namespace JonghyunKim.NativeToolkit.Runtime.Clipboard
{
    /// <summary>
    /// Result of <see cref="IosClipboardManager.CheckForegroundChange"/>.
    /// <para>
    /// The native operation never fails, so a failure here always originates from the C# bridge
    /// layer (unsupported platform, destroyed Manager, malformed response).
    /// </para>
    /// </summary>
    public readonly struct IosClipboardForegroundChangeResult
    {
        /// <summary>Whether the check completed.</summary>
        public bool IsSuccess { get; }

        /// <summary>Error detail. Non-null if and only if <see cref="IsSuccess"/> is <c>false</c>.</summary>
        public IosClipboardErrorInfo? Error { get; }

        /// <summary>Whether the clipboard changed since the last check. Always <c>false</c> on failure.</summary>
        public bool Changed { get; }

        /// <summary>
        /// Creates a successful result.
        /// </summary>
        /// <param name="changed">Whether a change was detected.</param>
        /// <returns>A successful <see cref="IosClipboardForegroundChangeResult"/>.</returns>
        internal static IosClipboardForegroundChangeResult Success(bool changed) =>
            new(true, null, changed);

        /// <summary>
        /// Creates a failed result.
        /// </summary>
        /// <param name="errorCode">Stable error code.</param>
        /// <param name="errorMessage">Error message.</param>
        /// <returns>A failed <see cref="IosClipboardForegroundChangeResult"/>.</returns>
        internal static IosClipboardForegroundChangeResult Failure(string? errorCode, string? errorMessage) =>
            Failure(IosClipboardErrorInfo.Create(errorCode, errorMessage));

        /// <summary>
        /// Creates a failed result from an already-built error info.
        /// </summary>
        /// <param name="error">Error detail to attach.</param>
        /// <returns>A failed <see cref="IosClipboardForegroundChangeResult"/>.</returns>
        internal static IosClipboardForegroundChangeResult Failure(IosClipboardErrorInfo error) =>
            new(false, error, false);

        private IosClipboardForegroundChangeResult(bool isSuccess, IosClipboardErrorInfo? error, bool changed)
        {
            IsSuccess = isSuccess;
            Error = error;
            Changed = changed;
        }
    }
}
#endif
