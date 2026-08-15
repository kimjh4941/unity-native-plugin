#nullable enable

#if UNITY_IOS || UNITY_EDITOR
// Intentional deviation from the "log every public method" rule in csharp.md: these result types
// carry clipboard content and native error detail. The factories are pure value constructors, so
// they emit no logs at all rather than a shape-only line — the Manager already logs operation,
// status and error code at the dispatch boundary. This matches the native ClipboardRedaction policy.

namespace JonghyunKim.NativeToolkit.Runtime.Clipboard
{
    /// <summary>
    /// Result of <see cref="IosClipboardManager.CreatePasteboard"/>.
    /// <para>
    /// The returned <see cref="Scope"/> can be passed straight back to any scoped operation. For a
    /// unique pasteboard this is the only way to learn its generated name.
    /// </para>
    /// </summary>
    public readonly struct IosPasteboardScopeResult
    {
        /// <summary>Whether the pasteboard was created or resolved.</summary>
        public bool IsSuccess { get; }

        /// <summary>Error detail. Non-null if and only if <see cref="IsSuccess"/> is <c>false</c>.</summary>
        public IosClipboardErrorInfo? Error { get; }

        /// <summary>Created scope. Non-null if and only if <see cref="IsSuccess"/> is <c>true</c>.</summary>
        public IosPasteboardScope? Scope { get; }

        /// <summary>
        /// Creates a successful result.
        /// </summary>
        /// <param name="scope">Scope referring to the created or resolved pasteboard.</param>
        /// <returns>A successful <see cref="IosPasteboardScopeResult"/>.</returns>
        internal static IosPasteboardScopeResult Success(IosPasteboardScope scope) =>
            new(true, null, scope);

        /// <summary>
        /// Creates a failed result.
        /// </summary>
        /// <param name="errorCode">Stable error code.</param>
        /// <param name="errorMessage">Error message.</param>
        /// <returns>A failed <see cref="IosPasteboardScopeResult"/>.</returns>
        internal static IosPasteboardScopeResult Failure(string? errorCode, string? errorMessage) =>
            Failure(IosClipboardErrorInfo.Create(errorCode, errorMessage));

        /// <summary>
        /// Creates a failed result from an already-built error info.
        /// </summary>
        /// <param name="error">Error detail to attach.</param>
        /// <returns>A failed <see cref="IosPasteboardScopeResult"/>.</returns>
        internal static IosPasteboardScopeResult Failure(IosClipboardErrorInfo error) =>
            new(false, error, null);

        private IosPasteboardScopeResult(bool isSuccess, IosClipboardErrorInfo? error, IosPasteboardScope? scope)
        {
            IsSuccess = isSuccess;
            Error = error;
            Scope = scope;
        }
    }
}
#endif
