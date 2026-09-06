#nullable enable

#if UNITY_STANDALONE_OSX || UNITY_EDITOR
// Intentional deviation from the "log every public method" rule in csharp.md: these result types
// carry clipboard content and native error detail. The factories are pure value constructors, so
// they emit no logs at all rather than a shape-only line. The Manager already logs operation,
// success and error code at the dispatch boundary.

namespace JonghyunKim.NativeToolkit.Runtime.Clipboard
{
    /// <summary>
    /// Result of asking how the system treats this app's pasteboard access.
    /// <para>
    /// Below macOS 15.4 the reporting API does not exist, which is reported as success with
    /// <see cref="MacClipboardAccessBehavior.Unavailable"/> rather than as a failure.
    /// </para>
    /// <para><c>default</c> is an uninitialised value, not a failure.</para>
    /// </summary>
    public readonly struct MacClipboardAccessBehaviorResult
    {
        /// <summary>Whether the query succeeded.</summary>
        public bool IsSuccess { get; }

        /// <summary>Error detail. Non-null if and only if <see cref="IsSuccess"/> is <c>false</c>.</summary>
        public MacClipboardErrorInfo? Error { get; }

        /// <summary>
        /// Reported behaviour. <see cref="MacClipboardAccessBehavior.Unknown"/> on failure, and
        /// also when the native layer reports a value this package does not know.
        /// </summary>
        public MacClipboardAccessBehavior Behavior { get; }

        /// <summary>
        /// Creates a successful result.
        /// </summary>
        /// <param name="behavior">Behaviour reported by the native layer.</param>
        /// <returns>A successful <see cref="MacClipboardAccessBehaviorResult"/>.</returns>
        public static MacClipboardAccessBehaviorResult Success(MacClipboardAccessBehavior behavior) =>
            new(true, null, behavior);

        /// <summary>
        /// Creates a failed result.
        /// </summary>
        /// <param name="code">Numeric error code.</param>
        /// <param name="message">Error message.</param>
        /// <returns>A failed <see cref="MacClipboardAccessBehaviorResult"/>.</returns>
        public static MacClipboardAccessBehaviorResult Failure(long code, string? message) =>
            new(false, MacClipboardErrorInfo.Create(code, message), MacClipboardAccessBehavior.Unknown);

        private MacClipboardAccessBehaviorResult(
            bool isSuccess, MacClipboardErrorInfo? error, MacClipboardAccessBehavior behavior)
        {
            IsSuccess = isSuccess;
            Error = error;
            Behavior = behavior;
        }
    }
}
#endif
