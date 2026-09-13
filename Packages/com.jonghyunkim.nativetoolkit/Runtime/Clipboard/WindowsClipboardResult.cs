#nullable enable

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
namespace JonghyunKim.NativeToolkit.Runtime.Clipboard
{
    /// <summary>
    /// Result of a Windows clipboard operation that returns no payload, such as a copy, a clear,
    /// or a lifecycle call.
    /// </summary>
    public readonly struct WindowsClipboardResult
    {
        /// <summary>Native operation name that produced this result.</summary>
        public string Operation { get; }

        /// <summary>Whether the operation succeeded.</summary>
        public bool IsSuccess { get; }

        /// <summary>Error code. <see cref="WindowsClipboardErrorCode.None"/> on success.</summary>
        public WindowsClipboardErrorCode ErrorCode { get; }

        /// <summary>Human-readable message. Null on success.</summary>
        public string? ErrorMessage { get; }

        /// <summary>Creates a successful result.</summary>
        /// <param name="operation">Native operation name.</param>
        /// <returns>A successful result.</returns>
        public static WindowsClipboardResult Success(string operation) =>
            new(operation, true, WindowsClipboardErrorCode.None, null);

        /// <summary>Creates a failed result.</summary>
        /// <param name="operation">Native operation name.</param>
        /// <param name="code">The error code. Passing None is treated as Unknown.</param>
        /// <param name="detail">Extra text for InvalidArgument.</param>
        /// <returns>A failed result whose message is never null.</returns>
        public static WindowsClipboardResult Failure(
            string operation, WindowsClipboardErrorCode code, string? detail = null)
        {
            // None would produce a null message and break the invariant that a failure always
            // carries one, so a caller that mistakes the two still gets a usable result.
            WindowsClipboardErrorCode effective =
                code == WindowsClipboardErrorCode.None ? WindowsClipboardErrorCode.Unknown : code;
            return new WindowsClipboardResult(
                operation, false, effective, effective.ToMessage(operation, detail));
        }

        /// <summary>Creates a result from a native pError value.</summary>
        /// <param name="operation">Native operation name.</param>
        /// <param name="pError">The value written by the native out parameter.</param>
        /// <returns>Success when pError is zero, otherwise a failure carrying that code.</returns>
        public static WindowsClipboardResult FromNative(string operation, int pError) =>
            pError == 0 ? Success(operation) : Failure(operation, (WindowsClipboardErrorCode)pError);

        private WindowsClipboardResult(
            string operation, bool isSuccess, WindowsClipboardErrorCode errorCode, string? errorMessage)
        {
            Operation = operation;
            IsSuccess = isSuccess;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
        }
    }
}
#endif
