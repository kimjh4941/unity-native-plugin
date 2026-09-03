#nullable enable

#if UNITY_STANDALONE_OSX || UNITY_EDITOR
// Intentional deviation from the "log every public method" rule in csharp.md: these result types
// carry clipboard content and native error detail. The factories are pure value constructors, so
// they emit no logs at all rather than a shape-only line. The Manager already logs operation,
// success and error code at the dispatch boundary.

namespace JonghyunKim.NativeToolkit.Runtime.Clipboard
{
    /// <summary>
    /// Result of asking whether the pasteboard changed since this app last looked.
    /// <para>
    /// The first call for a scope reports <c>true</c>, but only when that scope is not being
    /// observed: observation shares the same tracker, so during observation this returns
    /// <c>false</c> almost always. Use it instead of observation, not alongside it.
    /// </para>
    /// <para><c>default</c> is an uninitialised value, not a failure.</para>
    /// </summary>
    public readonly struct MacClipboardForegroundChangeResult
    {
        /// <summary>Whether the query succeeded.</summary>
        public bool IsSuccess { get; }

        /// <summary>Error detail. Non-null if and only if <see cref="IsSuccess"/> is <c>false</c>.</summary>
        public MacClipboardErrorInfo? Error { get; }

        /// <summary>Whether the pasteboard changed. <c>false</c> on failure.</summary>
        public bool Changed { get; }

        /// <summary>
        /// Creates a successful result.
        /// </summary>
        /// <param name="changed">Whether the pasteboard changed.</param>
        /// <returns>A successful <see cref="MacClipboardForegroundChangeResult"/>.</returns>
        public static MacClipboardForegroundChangeResult Success(bool changed) => new(true, null, changed);

        /// <summary>
        /// Creates a failed result.
        /// </summary>
        /// <param name="code">Numeric error code.</param>
        /// <param name="message">Error message.</param>
        /// <returns>A failed <see cref="MacClipboardForegroundChangeResult"/>.</returns>
        public static MacClipboardForegroundChangeResult Failure(long code, string? message) =>
            new(false, MacClipboardErrorInfo.Create(code, message), false);

        private MacClipboardForegroundChangeResult(bool isSuccess, MacClipboardErrorInfo? error, bool changed)
        {
            IsSuccess = isSuccess;
            Error = error;
            Changed = changed;
        }
    }
}
#endif
