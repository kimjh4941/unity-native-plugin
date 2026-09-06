#nullable enable

#if UNITY_STANDALONE_OSX || UNITY_EDITOR
// Intentional deviation from the "log every public method" rule in csharp.md: these result types
// carry clipboard content and native error detail. The factories are pure value constructors, so
// they emit no logs at all rather than a shape-only line. The Manager already logs operation,
// success and error code at the dispatch boundary.

namespace JonghyunKim.NativeToolkit.Runtime.Clipboard
{
    /// <summary>
    /// Result of creating or fetching a pasteboard.
    /// <para>
    /// For a unique pasteboard this is the only way to learn its generated name, and therefore the
    /// only way to release it later. Keep the scope until you call <c>RemovePasteboard</c>; the
    /// Manager does not track it.
    /// </para>
    /// <para><c>default</c> is an uninitialised value, not a failure.</para>
    /// </summary>
    public readonly struct MacPasteboardScopeResult
    {
        /// <summary>Whether the pasteboard was created or resolved.</summary>
        public bool IsSuccess { get; }

        /// <summary>Error detail. Non-null if and only if <see cref="IsSuccess"/> is <c>false</c>.</summary>
        public MacClipboardErrorInfo? Error { get; }

        /// <summary>Created scope. Non-null if and only if <see cref="IsSuccess"/> is <c>true</c>.</summary>
        public MacPasteboardScope? Scope { get; }

        /// <summary>
        /// Creates a successful result.
        /// </summary>
        /// <param name="scope">Scope referring to the created or resolved pasteboard.</param>
        /// <returns>A successful <see cref="MacPasteboardScopeResult"/>.</returns>
        public static MacPasteboardScopeResult Success(MacPasteboardScope scope) => new(true, null, scope);

        /// <summary>
        /// Creates a failed result.
        /// </summary>
        /// <param name="code">Numeric error code.</param>
        /// <param name="message">Error message.</param>
        /// <returns>A failed <see cref="MacPasteboardScopeResult"/>.</returns>
        public static MacPasteboardScopeResult Failure(long code, string? message) =>
            new(false, MacClipboardErrorInfo.Create(code, message), null);

        private MacPasteboardScopeResult(bool isSuccess, MacClipboardErrorInfo? error, MacPasteboardScope? scope)
        {
            IsSuccess = isSuccess;
            Error = error;
            Scope = scope;
        }
    }
}
#endif
