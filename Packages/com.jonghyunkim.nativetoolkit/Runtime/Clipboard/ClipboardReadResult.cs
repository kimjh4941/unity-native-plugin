#nullable enable

using System.Collections.Generic;

namespace JonghyunKim.NativeToolkit.Runtime.Clipboard
{
    /// <summary>
    /// The outcome category shared by <see cref="ClipboardReadResult"/> and
    /// <see cref="ClipboardDescriptionResult"/>. An empty clipboard is a normal outcome, distinct
    /// from a failed read.
    /// </summary>
    public enum ClipboardReadStatus
    {
        /// <summary>The clipboard held content and it was read successfully.</summary>
        HasContent,

        /// <summary>The clipboard was empty. This is a normal outcome, not a failure.</summary>
        Empty,

        /// <summary>The read failed. See the result's ErrorCode / ErrorMessage.</summary>
        Failed
    }

    /// <summary>
    /// A single clip item read from the clipboard.
    /// The native layer omits JSON keys whose value is null, so an absent value is returned as
    /// null here. An empty string is never returned; it is normalized to null.
    /// </summary>
    public sealed class ClipItem
    {
        /// <summary>Plain text, if the item holds text.</summary>
        public string? Text { get; }

        /// <summary>HTML text, if the item holds HTML.</summary>
        public string? HtmlText { get; }

        /// <summary>URI string, if the item holds a URI.</summary>
        public string? Uri { get; }

        /// <summary>A best-effort plain-text fallback for this item.</summary>
        public string? CoercedText { get; }

        internal ClipItem(string? text, string? htmlText, string? uri, string? coercedText)
        {
            Text = text;
            HtmlText = htmlText;
            Uri = uri;
            CoercedText = coercedText;
        }
    }

    /// <summary>
    /// Clipboard content returned by <see cref="AndroidClipboardManager.Read"/> when the clipboard
    /// held content.
    /// </summary>
    public sealed class ClipContents
    {
        /// <summary>Clip label, if present.</summary>
        public string? Label { get; }

        /// <summary>MIME types available on the clip. Never null; empty when the native layer omitted the key.</summary>
        public IReadOnlyList<string> MimeTypes { get; }

        /// <summary>Clip items, in clipboard order. Never null; empty when the native layer omitted the key.</summary>
        public IReadOnlyList<ClipItem> Items { get; }

        internal ClipContents(string? label, IReadOnlyList<string> mimeTypes, IReadOnlyList<ClipItem> items)
        {
            Label = label;
            MimeTypes = mimeTypes;
            Items = items;
        }
    }

    /// <summary>
    /// Represents the outcome of <see cref="AndroidClipboardManager.Read"/>.
    /// </summary>
    public readonly struct ClipboardReadResult
    {
        /// <summary>Gets the outcome category of this read.</summary>
        public ClipboardReadStatus Status { get; }

        /// <summary>Non-null only when <see cref="Status"/> is <see cref="ClipboardReadStatus.HasContent"/>.</summary>
        public ClipContents? Contents { get; }

        /// <summary>Non-null only when <see cref="Status"/> is <see cref="ClipboardReadStatus.Failed"/>.</summary>
        public string? ErrorCode { get; }

        /// <summary>Non-null only when <see cref="Status"/> is <see cref="ClipboardReadStatus.Failed"/>.</summary>
        public string? ErrorMessage { get; }

        /// <summary>Gets a value indicating whether the read did not fail (HasContent or Empty).</summary>
        public bool IsSuccess => Status != ClipboardReadStatus.Failed;

        /// <summary>
        /// Creates a result for a clipboard that held content.
        /// </summary>
        /// <param name="contents">The clip content that was read.</param>
        /// <returns>A <see cref="ClipboardReadResult"/> with <see cref="ClipboardReadStatus.HasContent"/>.</returns>
        internal static ClipboardReadResult FromContents(ClipContents contents) =>
            new(ClipboardReadStatus.HasContent, contents, null, null);

        /// <summary>
        /// Creates a result for an empty clipboard.
        /// </summary>
        /// <returns>A <see cref="ClipboardReadResult"/> with <see cref="ClipboardReadStatus.Empty"/>.</returns>
        internal static ClipboardReadResult Empty() =>
            new(ClipboardReadStatus.Empty, null, null, null);

        /// <summary>
        /// Creates a result for a failed read.
        /// </summary>
        /// <param name="errorCode">Stable error code (see design 1.7).</param>
        /// <param name="errorMessage">Human-readable error message.</param>
        /// <returns>A <see cref="ClipboardReadResult"/> with <see cref="ClipboardReadStatus.Failed"/>.</returns>
        internal static ClipboardReadResult Failed(string errorCode, string errorMessage) =>
            new(ClipboardReadStatus.Failed, null, errorCode, errorMessage);

        private ClipboardReadResult(ClipboardReadStatus status, ClipContents? contents, string? errorCode, string? errorMessage)
        {
            Status = status;
            Contents = contents;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
        }
    }
}
