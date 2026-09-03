#nullable enable

#if UNITY_STANDALONE_OSX || UNITY_EDITOR
// Intentional deviation from the "log every public method" rule in csharp.md: these result types
// carry clipboard content and native error detail. The factories are pure value constructors, so
// they emit no logs at all rather than a shape-only line. The Manager already logs operation,
// success and error code at the dispatch boundary.

namespace JonghyunKim.NativeToolkit.Runtime.Clipboard
{
    /// <summary>
    /// Result of reading the bytes for one uniform type identifier.
    /// <para>
    /// <b>The only result type where success with a null payload is normal.</b> A type the
    /// pasteboard does not hold is reported as success with <see cref="Data"/> null. An invalid
    /// uniform type identifier lands here too: the native layer does not validate it on this path,
    /// so a typo is indistinguishable from an absent type.
    /// </para>
    /// <para><c>default</c> is an uninitialised value, not a failure.</para>
    /// </summary>
    public readonly struct MacClipboardReadDataResult
    {
        /// <summary>Whether the read succeeded.</summary>
        public bool IsSuccess { get; }

        /// <summary>Error detail. Non-null if and only if <see cref="IsSuccess"/> is <c>false</c>.</summary>
        public MacClipboardErrorInfo? Error { get; }

        /// <summary>
        /// Bytes for the requested type, or <c>null</c> when the pasteboard has no such type.
        /// Null does not imply failure; check <see cref="IsSuccess"/> first.
        /// </summary>
        public byte[]? Data { get; }

        /// <summary>
        /// Creates a successful result.
        /// </summary>
        /// <param name="data">Bytes read, or <c>null</c> when the type was absent.</param>
        /// <returns>A successful <see cref="MacClipboardReadDataResult"/>.</returns>
        public static MacClipboardReadDataResult Success(byte[]? data) => new(true, null, data);

        /// <summary>
        /// Creates a failed result.
        /// </summary>
        /// <param name="code">Numeric error code.</param>
        /// <param name="message">Error message.</param>
        /// <returns>A failed <see cref="MacClipboardReadDataResult"/>.</returns>
        public static MacClipboardReadDataResult Failure(long code, string? message) =>
            new(false, MacClipboardErrorInfo.Create(code, message), null);

        private MacClipboardReadDataResult(bool isSuccess, MacClipboardErrorInfo? error, byte[]? data)
        {
            IsSuccess = isSuccess;
            Error = error;
            Data = data;
        }
    }
}
#endif
