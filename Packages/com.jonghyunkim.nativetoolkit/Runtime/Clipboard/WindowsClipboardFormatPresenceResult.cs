#nullable enable

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
namespace JonghyunKim.NativeToolkit.Runtime.Clipboard
{
    /// <summary>
    /// Result of HasFormat.
    /// <para>
    /// The native API returns FALSE both when the format is absent and when the query itself
    /// failed, so <see cref="IsSuccess"/> and <see cref="HasFormat"/> are independent:
    /// <see cref="HasFormat"/> carries no meaning unless <see cref="IsSuccess"/> is true.
    /// </para>
    /// </summary>
    public readonly struct WindowsClipboardFormatPresenceResult
    {
        /// <summary>Native operation name that produced this result.</summary>
        public string Operation { get; }

        /// <summary>Whether the query itself completed.</summary>
        public bool IsSuccess { get; }

        /// <summary>Whether the format is present. Meaningful only when <see cref="IsSuccess"/> is true.</summary>
        public bool HasFormat { get; }

        /// <summary>Error code. None on success.</summary>
        public WindowsClipboardErrorCode ErrorCode { get; }

        /// <summary>Human-readable message. Null on success.</summary>
        public string? ErrorMessage { get; }

        /// <summary>Creates a successful query result.</summary>
        /// <param name="operation">Native operation name.</param>
        /// <param name="hasFormat">Whether the clipboard holds the format.</param>
        /// <returns>A successful result.</returns>
        public static WindowsClipboardFormatPresenceResult Success(string operation, bool hasFormat) =>
            new(operation, true, hasFormat, WindowsClipboardErrorCode.None, null);

        /// <summary>Creates a failed result.</summary>
        /// <param name="operation">Native operation name.</param>
        /// <param name="code">The error code.</param>
        /// <param name="detail">Extra text for InvalidArgument.</param>
        /// <returns>A failed result whose message is never null.</returns>
        public static WindowsClipboardFormatPresenceResult Failure(
            string operation, WindowsClipboardErrorCode code, string? detail = null)
        {
            WindowsClipboardErrorCode effective =
                code == WindowsClipboardErrorCode.None ? WindowsClipboardErrorCode.Unknown : code;
            return new WindowsClipboardFormatPresenceResult(
                operation, false, false, effective, effective.ToMessage(operation, detail));
        }

        private WindowsClipboardFormatPresenceResult(string operation, bool isSuccess, bool hasFormat,
            WindowsClipboardErrorCode errorCode, string? errorMessage)
        {
            Operation = operation;
            IsSuccess = isSuccess;
            HasFormat = hasFormat;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
        }
    }
}
#endif
