#nullable enable

using System.Collections.Generic;
using UnityEngine.Scripting.APIUpdating;

namespace JonghyunKim.NativeToolkit.Runtime.Android.Clipboard
{
    /// <summary>
    /// The outcome category shared by <see cref="AndroidClipboardReadResult"/> and
    /// <see cref="AndroidClipboardDescriptionResult"/>. An empty clipboard is a normal outcome, distinct
    /// from a failed read.
    /// </summary>
    [MovedFrom(false, sourceNamespace: "JonghyunKim.NativeToolkit.Runtime.Clipboard", sourceClassName: "ClipboardReadStatus")]
    public enum AndroidClipboardReadStatus
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
    /// A value the item does not hold is returned as null. An empty string is never returned; it is
    /// normalized to null.
    /// </summary>
    [MovedFrom(false, sourceNamespace: "JonghyunKim.NativeToolkit.Runtime.Clipboard", sourceClassName: "ClipItem")]
    public sealed class AndroidClipItem
    {
        /// <summary>Plain text, if the item holds text.</summary>
        public string? Text { get; }

        /// <summary>HTML text, if the item holds HTML.</summary>
        public string? HtmlText { get; }

        /// <summary>URI string, if the item holds a URI.</summary>
        public string? Uri { get; }

        /// <summary>A best-effort plain-text fallback for this item.</summary>
        public string? CoercedText { get; }

        internal AndroidClipItem(string? text, string? htmlText, string? uri, string? coercedText)
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
    [MovedFrom(false, sourceNamespace: "JonghyunKim.NativeToolkit.Runtime.Clipboard", sourceClassName: "ClipContents")]
    public sealed class AndroidClipContents
    {
        /// <summary>Clip label, if present.</summary>
        public string? Label { get; }

        /// <summary>MIME types available on the clip. Never null; empty when the clip has none.</summary>
        public IReadOnlyList<string> MimeTypes { get; }

        /// <summary>Clip items, in clipboard order. Never null; empty when the clip has none.</summary>
        public IReadOnlyList<AndroidClipItem> Items { get; }

        internal AndroidClipContents(string? label, IReadOnlyList<string> mimeTypes, IReadOnlyList<AndroidClipItem> items)
        {
            Label = label;
            MimeTypes = mimeTypes;
            Items = items;
        }
    }

    /// <summary>
    /// Represents the outcome of <see cref="AndroidClipboardManager.Read"/>.
    /// </summary>
    [MovedFrom(false, sourceNamespace: "JonghyunKim.NativeToolkit.Runtime.Clipboard", sourceClassName: "ClipboardReadResult")]
    public readonly struct AndroidClipboardReadResult
    {
        /// <summary>Gets the outcome category of this read.</summary>
        public AndroidClipboardReadStatus Status { get; }

        /// <summary>Non-null only when <see cref="Status"/> is <see cref="AndroidClipboardReadStatus.HasContent"/>.</summary>
        public AndroidClipContents? Contents { get; }

        /// <summary>Non-null only when <see cref="Status"/> is <see cref="AndroidClipboardReadStatus.Failed"/>.</summary>
        public string? ErrorCode { get; }

        /// <summary>Non-null only when <see cref="Status"/> is <see cref="AndroidClipboardReadStatus.Failed"/>.</summary>
        public string? ErrorMessage { get; }

        /// <summary>Gets a value indicating whether the read did not fail (HasContent or Empty).</summary>
        public bool IsSuccess => Status != AndroidClipboardReadStatus.Failed;

        /// <summary>
        /// Creates a result for a clipboard that held content.
        /// </summary>
        /// <param name="contents">The clip content that was read.</param>
        /// <returns>A <see cref="AndroidClipboardReadResult"/> with <see cref="AndroidClipboardReadStatus.HasContent"/>.</returns>
        internal static AndroidClipboardReadResult FromContents(AndroidClipContents contents) =>
            new(AndroidClipboardReadStatus.HasContent, contents, null, null);

        /// <summary>
        /// Creates a result for an empty clipboard.
        /// </summary>
        /// <returns>A <see cref="AndroidClipboardReadResult"/> with <see cref="AndroidClipboardReadStatus.Empty"/>.</returns>
        internal static AndroidClipboardReadResult Empty() =>
            new(AndroidClipboardReadStatus.Empty, null, null, null);

        /// <summary>
        /// Creates a result for a failed read.
        /// </summary>
        /// <param name="errorCode">Stable error code (see design 1.7).</param>
        /// <param name="errorMessage">Human-readable error message.</param>
        /// <returns>A <see cref="AndroidClipboardReadResult"/> with <see cref="AndroidClipboardReadStatus.Failed"/>.</returns>
        internal static AndroidClipboardReadResult Failed(string errorCode, string errorMessage) =>
            new(AndroidClipboardReadStatus.Failed, null, errorCode, errorMessage);

        private AndroidClipboardReadResult(AndroidClipboardReadStatus status, AndroidClipContents? contents, string? errorCode, string? errorMessage)
        {
            Status = status;
            Contents = contents;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
        }
    }
}
