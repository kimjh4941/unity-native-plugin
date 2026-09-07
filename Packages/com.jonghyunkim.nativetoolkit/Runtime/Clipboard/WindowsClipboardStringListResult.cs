#nullable enable

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
namespace JonghyunKim.NativeToolkit.Runtime.Clipboard
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Result of a clipboard read that yields a list of strings: PasteFiles and GetFormats.
    /// <para>
    /// GetFormats never reports the native Empty code: an empty clipboard returns an empty JSON
    /// array, so emptiness is decided by the parsed element count.
    /// </para>
    /// </summary>
    public readonly struct WindowsClipboardStringListResult
    {
        private static readonly IReadOnlyList<string> NoValues = Array.Empty<string>();

        /// <summary>Native operation name that produced this result.</summary>
        public string Operation { get; }

        /// <summary>Whether the read completed. True even when the clipboard held nothing.</summary>
        public bool IsSuccess { get; }

        /// <summary>Whether the read produced no elements.</summary>
        public bool IsEmpty { get; }

        /// <summary>
        /// The values that were read. Never null, including for a default-constructed value:
        /// a readonly struct cannot run a field initializer, so the accessor substitutes the
        /// empty list rather than handing back null.
        /// </summary>
        public IReadOnlyList<string> Values => _values ?? NoValues;

        private readonly IReadOnlyList<string>? _values;

        /// <summary>Error code. None on success.</summary>
        public WindowsClipboardErrorCode ErrorCode { get; }

        /// <summary>Human-readable message. Null on success.</summary>
        public string? ErrorMessage { get; }

        /// <summary>Creates a result for a read that returned values.</summary>
        /// <param name="operation">Native operation name.</param>
        /// <param name="values">The values that were read. An empty list yields an empty result.</param>
        /// <returns>A successful result.</returns>
        public static WindowsClipboardStringListResult Success(string operation, IReadOnlyList<string> values)
        {
            IReadOnlyList<string> safe = values ?? NoValues;
            return new WindowsClipboardStringListResult(
                operation, true, safe.Count == 0, safe, WindowsClipboardErrorCode.None, null);
        }

        /// <summary>Creates a result for a clipboard that held nothing.</summary>
        /// <param name="operation">Native operation name.</param>
        /// <returns>A successful, empty result.</returns>
        public static WindowsClipboardStringListResult Empty(string operation) =>
            new(operation, true, true, NoValues, WindowsClipboardErrorCode.None, null);

        /// <summary>Creates a failed result.</summary>
        /// <param name="operation">Native operation name.</param>
        /// <param name="code">The error code.</param>
        /// <param name="detail">Extra text for InvalidArgument.</param>
        /// <returns>A failed result whose message is never null.</returns>
        public static WindowsClipboardStringListResult Failure(
            string operation, WindowsClipboardErrorCode code, string? detail = null)
        {
            WindowsClipboardErrorCode effective =
                code == WindowsClipboardErrorCode.None ? WindowsClipboardErrorCode.Unknown : code;
            return new WindowsClipboardStringListResult(
                operation, false, false, NoValues, effective, effective.ToMessage(operation, detail));
        }

        private WindowsClipboardStringListResult(string operation, bool isSuccess, bool isEmpty,
            IReadOnlyList<string> values, WindowsClipboardErrorCode errorCode, string? errorMessage)
        {
            Operation = operation;
            IsSuccess = isSuccess;
            IsEmpty = isEmpty;
            _values = values;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
        }
    }
}
#endif
