#nullable enable

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
namespace JonghyunKim.NativeToolkit.Runtime.Clipboard
{
    using System;

    /// <summary>
    /// Result of a clipboard read that yields raw bytes: PasteImage and PasteCustomFormat.
    /// <para>
    /// A zero-length payload is reported by the native layer as a required size of zero together
    /// with BufferTooSmall, which this layer normalizes to an empty success.
    /// </para>
    /// </summary>
    public readonly struct WindowsClipboardBytesResult
    {
        private static readonly byte[] NoData = Array.Empty<byte>();

        /// <summary>Native operation name that produced this result.</summary>
        public string Operation { get; }

        /// <summary>Whether the read completed. True even when the clipboard held nothing.</summary>
        public bool IsSuccess { get; }

        /// <summary>Whether the read produced no bytes.</summary>
        public bool IsEmpty { get; }

        /// <summary>
        /// The bytes that were read. Never null, including for a default-constructed value:
        /// a readonly struct cannot run a field initializer, so the accessor substitutes the
        /// empty array rather than handing back null.
        /// </summary>
        public byte[] Data => _data ?? NoData;

        private readonly byte[]? _data;

        /// <summary>Error code. None on success.</summary>
        public WindowsClipboardErrorCode ErrorCode { get; }

        /// <summary>Human-readable message. Null on success.</summary>
        public string? ErrorMessage { get; }

        /// <summary>Creates a result for a read that returned bytes.</summary>
        /// <param name="operation">Native operation name.</param>
        /// <param name="data">The bytes that were read. An empty array yields an empty result.</param>
        /// <returns>A successful result.</returns>
        public static WindowsClipboardBytesResult Success(string operation, byte[] data)
        {
            byte[] safe = data ?? NoData;
            return new WindowsClipboardBytesResult(
                operation, true, safe.Length == 0, safe, WindowsClipboardErrorCode.None, null);
        }

        /// <summary>Creates a result for a clipboard that held nothing.</summary>
        /// <param name="operation">Native operation name.</param>
        /// <returns>A successful, empty result.</returns>
        public static WindowsClipboardBytesResult Empty(string operation) =>
            new(operation, true, true, NoData, WindowsClipboardErrorCode.None, null);

        /// <summary>Creates a failed result.</summary>
        /// <param name="operation">Native operation name.</param>
        /// <param name="code">The error code.</param>
        /// <param name="detail">Extra text for InvalidArgument.</param>
        /// <returns>A failed result whose message is never null.</returns>
        public static WindowsClipboardBytesResult Failure(
            string operation, WindowsClipboardErrorCode code, string? detail = null)
        {
            WindowsClipboardErrorCode effective =
                code == WindowsClipboardErrorCode.None ? WindowsClipboardErrorCode.Unknown : code;
            return new WindowsClipboardBytesResult(
                operation, false, false, NoData, effective, effective.ToMessage(operation, detail));
        }

        private WindowsClipboardBytesResult(string operation, bool isSuccess, bool isEmpty, byte[] data,
            WindowsClipboardErrorCode errorCode, string? errorMessage)
        {
            Operation = operation;
            IsSuccess = isSuccess;
            IsEmpty = isEmpty;
            _data = data;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
        }
    }
}
#endif
