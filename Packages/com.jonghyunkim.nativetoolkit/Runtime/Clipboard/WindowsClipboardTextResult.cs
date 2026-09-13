#nullable enable

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
namespace JonghyunKim.NativeToolkit.Runtime.Clipboard
{
    /// <summary>
    /// Result of a clipboard read that yields text: PastePlainText, PasteHtml and GetPreferredFormat.
    /// <para>
    /// An empty clipboard is a successful outcome, not a failure: check <see cref="IsEmpty"/>
    /// rather than treating an empty <see cref="Text"/> as an error.
    /// </para>
    /// </summary>
    public readonly struct WindowsClipboardTextResult
    {
        /// <summary>Native operation name that produced this result.</summary>
        public string Operation { get; }

        /// <summary>Whether the read completed. True even when the clipboard held nothing.</summary>
        public bool IsSuccess { get; }

        /// <summary>Whether the clipboard held nothing for this format.</summary>
        public bool IsEmpty { get; }

        /// <summary>The text that was read. Null when the read failed or was empty.</summary>
        public string? Text { get; }

        /// <summary>Error code. None on success.</summary>
        public WindowsClipboardErrorCode ErrorCode { get; }

        /// <summary>Human-readable message. Null on success.</summary>
        public string? ErrorMessage { get; }

        /// <summary>Creates a result for a read that returned text.</summary>
        /// <param name="operation">Native operation name.</param>
        /// <param name="text">The text that was read.</param>
        /// <returns>A successful, non-empty result.</returns>
        public static WindowsClipboardTextResult Success(string operation, string text) =>
            new(operation, true, false, text, WindowsClipboardErrorCode.None, null);

        /// <summary>Creates a result for a clipboard that held nothing.</summary>
        /// <param name="operation">Native operation name.</param>
        /// <returns>A successful, empty result.</returns>
        public static WindowsClipboardTextResult Empty(string operation) =>
            new(operation, true, true, null, WindowsClipboardErrorCode.None, null);

        /// <summary>Creates a failed result.</summary>
        /// <param name="operation">Native operation name.</param>
        /// <param name="code">The error code.</param>
        /// <param name="detail">Extra text for InvalidArgument.</param>
        /// <returns>A failed result whose message is never null.</returns>
        public static WindowsClipboardTextResult Failure(
            string operation, WindowsClipboardErrorCode code, string? detail = null)
        {
            WindowsClipboardErrorCode effective =
                code == WindowsClipboardErrorCode.None ? WindowsClipboardErrorCode.Unknown : code;
            return new WindowsClipboardTextResult(
                operation, false, false, null, effective, effective.ToMessage(operation, detail));
        }

        private WindowsClipboardTextResult(string operation, bool isSuccess, bool isEmpty, string? text,
            WindowsClipboardErrorCode errorCode, string? errorMessage)
        {
            Operation = operation;
            IsSuccess = isSuccess;
            IsEmpty = isEmpty;
            Text = text;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
        }
    }
}
#endif
