#nullable enable

#if UNITY_IOS || UNITY_EDITOR
// Intentional deviation from the "log every public method" rule in csharp.md: these result types
// carry clipboard content and native error detail. The factories are pure value constructors, so
// they emit no logs at all rather than a shape-only line — the Manager already logs operation,
// status and error code at the dispatch boundary. This matches the native ClipboardRedaction policy.

namespace JonghyunKim.NativeToolkit.Runtime.Clipboard
{
    /// <summary>
    /// Result of <see cref="IosClipboardManager.ReadData"/>.
    /// <para>
    /// "No data for the requested type" is a successful result with <see cref="HasData"/> set to
    /// <c>false</c>, not a failure: the native layer reports it as <c>{"ok":true,"data":null}</c>.
    /// </para>
    /// </summary>
    public readonly struct IosClipboardReadDataResult
    {
        /// <summary>Whether the read succeeded.</summary>
        public bool IsSuccess { get; }

        /// <summary>Error detail. Non-null if and only if <see cref="IsSuccess"/> is <c>false</c>.</summary>
        public IosClipboardErrorInfo? Error { get; }

        /// <summary>Whether data was present for the requested type. Always <c>false</c> on failure.</summary>
        public bool HasData { get; }

        /// <summary>Uniform type identifier of the returned data, or <c>null</c> when <see cref="HasData"/> is <c>false</c>.</summary>
        public string? UtType { get; }

        /// <summary>Decoded bytes, or <c>null</c> when <see cref="HasData"/> is <c>false</c>.</summary>
        public byte[]? Data { get; }

        /// <summary>Length of <see cref="Data"/>. Zero when <see cref="HasData"/> is <c>false</c>.</summary>
        public int ByteCount { get; }

        /// <summary>
        /// Creates a successful result carrying data.
        /// </summary>
        /// <param name="utType">Uniform type identifier of the data.</param>
        /// <param name="data">Decoded bytes.</param>
        /// <returns>A successful <see cref="IosClipboardReadDataResult"/> with <c>HasData == true</c>.</returns>
        internal static IosClipboardReadDataResult Success(string utType, byte[] data) =>
            new(true, null, true, utType, data, data.Length);

        /// <summary>
        /// Creates a successful result for a type that had no data on the pasteboard.
        /// </summary>
        /// <returns>A successful <see cref="IosClipboardReadDataResult"/> with <c>HasData == false</c>.</returns>
        internal static IosClipboardReadDataResult NoData() =>
            new(true, null, false, null, null, 0);

        /// <summary>
        /// Creates a failed result.
        /// </summary>
        /// <param name="errorCode">Stable error code.</param>
        /// <param name="errorMessage">Error message.</param>
        /// <returns>A failed <see cref="IosClipboardReadDataResult"/>.</returns>
        internal static IosClipboardReadDataResult Failure(string? errorCode, string? errorMessage) =>
            Failure(IosClipboardErrorInfo.Create(errorCode, errorMessage));

        /// <summary>
        /// Creates a failed result from an already-built error info.
        /// </summary>
        /// <param name="error">Error detail to attach.</param>
        /// <returns>A failed <see cref="IosClipboardReadDataResult"/>.</returns>
        internal static IosClipboardReadDataResult Failure(IosClipboardErrorInfo error) =>
            new(false, error, false, null, null, 0);

        private IosClipboardReadDataResult(
            bool isSuccess,
            IosClipboardErrorInfo? error,
            bool hasData,
            string? utType,
            byte[]? data,
            int byteCount)
        {
            IsSuccess = isSuccess;
            Error = error;
            HasData = hasData;
            UtType = utType;
            Data = data;
            ByteCount = byteCount;
        }
    }
}
#endif
