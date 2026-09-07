#nullable enable

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
namespace JonghyunKim.NativeToolkit.Runtime.Clipboard
{
    /// <summary>
    /// Result of a query that answers with a single flag, such as CanShutdownNow.
    /// <para>
    /// As with HasFormat, the native API answers FALSE both for "no" and for a failed query, so
    /// <see cref="Value"/> is meaningful only when <see cref="IsSuccess"/> is true.
    /// </para>
    /// </summary>
    public readonly struct WindowsClipboardFlagResult
    {
        /// <summary>Native operation name that produced this result.</summary>
        public string Operation { get; }

        /// <summary>Whether the query itself completed.</summary>
        public bool IsSuccess { get; }

        /// <summary>The queried flag. Meaningful only when <see cref="IsSuccess"/> is true.</summary>
        public bool Value { get; }

        /// <summary>Error code. None on success.</summary>
        public WindowsClipboardErrorCode ErrorCode { get; }

        /// <summary>Human-readable message. Null on success.</summary>
        public string? ErrorMessage { get; }

        /// <summary>Creates a successful query result.</summary>
        /// <param name="operation">Native operation name.</param>
        /// <param name="value">The queried flag.</param>
        /// <returns>A successful result.</returns>
        public static WindowsClipboardFlagResult Success(string operation, bool value) =>
            new(operation, true, value, WindowsClipboardErrorCode.None, null);

        /// <summary>Creates a failed result.</summary>
        /// <param name="operation">Native operation name.</param>
        /// <param name="code">The error code.</param>
        /// <param name="detail">Extra text for InvalidArgument.</param>
        /// <returns>A failed result whose message is never null.</returns>
        public static WindowsClipboardFlagResult Failure(
            string operation, WindowsClipboardErrorCode code, string? detail = null)
        {
            WindowsClipboardErrorCode effective =
                code == WindowsClipboardErrorCode.None ? WindowsClipboardErrorCode.Unknown : code;
            return new WindowsClipboardFlagResult(
                operation, false, false, effective, effective.ToMessage(operation, detail));
        }

        private WindowsClipboardFlagResult(string operation, bool isSuccess, bool value,
            WindowsClipboardErrorCode errorCode, string? errorMessage)
        {
            Operation = operation;
            IsSuccess = isSuccess;
            Value = value;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
        }
    }
}
#endif
