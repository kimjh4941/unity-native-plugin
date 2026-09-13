#nullable enable

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
namespace JonghyunKim.NativeToolkit.Runtime.Clipboard
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Result of GetHistory.
    /// </summary>
    public readonly struct WindowsClipboardHistoryResult
    {
        private static readonly IReadOnlyList<WindowsClipboardHistoryItem> NoItems =
            Array.Empty<WindowsClipboardHistoryItem>();

        /// <summary>Native operation name that produced this result.</summary>
        public string Operation { get; }

        /// <summary>Whether the request completed. True even when the history held nothing.</summary>
        public bool IsSuccess { get; }

        /// <summary>Whether the history held no items.</summary>
        public bool IsEmpty { get; }

        /// <summary>
        /// The history items, in the order Windows returned them. Never null, including for a
        /// default-constructed value: a readonly struct cannot run a field initializer, so the
        /// accessor substitutes the empty list rather than handing back null.
        /// </summary>
        public IReadOnlyList<WindowsClipboardHistoryItem> Items => _items ?? NoItems;

        private readonly IReadOnlyList<WindowsClipboardHistoryItem>? _items;

        /// <summary>Error code. None on success.</summary>
        public WindowsClipboardErrorCode ErrorCode { get; }

        /// <summary>Human-readable message. Null on success.</summary>
        public string? ErrorMessage { get; }

        /// <summary>Creates a successful result.</summary>
        /// <param name="operation">Native operation name.</param>
        /// <param name="items">The parsed history items.</param>
        /// <returns>A successful result, empty when the list holds no items.</returns>
        public static WindowsClipboardHistoryResult Success(
            string operation, IReadOnlyList<WindowsClipboardHistoryItem> items)
        {
            IReadOnlyList<WindowsClipboardHistoryItem> safe = items ?? NoItems;
            return new WindowsClipboardHistoryResult(
                operation, true, safe.Count == 0, safe, WindowsClipboardErrorCode.None, null);
        }

        /// <summary>Creates a failed result.</summary>
        /// <param name="operation">Native operation name.</param>
        /// <param name="code">The error code.</param>
        /// <param name="detail">Extra text for InvalidArgument.</param>
        /// <returns>A failed result whose message is never null.</returns>
        public static WindowsClipboardHistoryResult Failure(
            string operation, WindowsClipboardErrorCode code, string? detail = null)
        {
            WindowsClipboardErrorCode effective =
                code == WindowsClipboardErrorCode.None ? WindowsClipboardErrorCode.Unknown : code;
            return new WindowsClipboardHistoryResult(
                operation, false, false, NoItems, effective, effective.ToMessage(operation, detail));
        }

        private WindowsClipboardHistoryResult(string operation, bool isSuccess, bool isEmpty,
            IReadOnlyList<WindowsClipboardHistoryItem> items,
            WindowsClipboardErrorCode errorCode, string? errorMessage)
        {
            Operation = operation;
            IsSuccess = isSuccess;
            IsEmpty = isEmpty;
            _items = items;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
        }
    }
}
#endif
