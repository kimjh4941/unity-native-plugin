#nullable enable

using System.Collections.Generic;

namespace JonghyunKim.NativeToolkit.Runtime.Clipboard
{
    /// <summary>
    /// Clipboard metadata returned by <see cref="AndroidClipboardManager.GetDescription"/>, obtained
    /// without touching the clip body.
    /// </summary>
    public sealed class ClipDescriptionInfo
    {
        /// <summary>Clip label, if present.</summary>
        public string? Label { get; }

        /// <summary>MIME types available on the clip. Never null; empty when the native layer omitted the key.</summary>
        public IReadOnlyList<string> MimeTypes { get; }

        /// <summary>Whether the clip is styled text (API 31+).</summary>
        public bool IsStyledText { get; }

        /// <summary>
        /// Raw ClipDescription.CLASSIFICATION_* value. Null when unavailable; the native layer
        /// omits the key on API levels below 31.
        /// </summary>
        public int? ClassificationStatus { get; }

        internal ClipDescriptionInfo(string? label, IReadOnlyList<string> mimeTypes, bool isStyledText, int? classificationStatus)
        {
            Label = label;
            MimeTypes = mimeTypes;
            IsStyledText = isStyledText;
            ClassificationStatus = classificationStatus;
        }
    }

    /// <summary>
    /// Represents the outcome of <see cref="AndroidClipboardManager.GetDescription"/>.
    /// </summary>
    public readonly struct ClipboardDescriptionResult
    {
        /// <summary>Gets the outcome category of this read.</summary>
        public ClipboardReadStatus Status { get; }

        /// <summary>Non-null only when <see cref="Status"/> is <see cref="ClipboardReadStatus.HasContent"/>.</summary>
        public ClipDescriptionInfo? Description { get; }

        /// <summary>Non-null only when <see cref="Status"/> is <see cref="ClipboardReadStatus.Failed"/>.</summary>
        public string? ErrorCode { get; }

        /// <summary>Non-null only when <see cref="Status"/> is <see cref="ClipboardReadStatus.Failed"/>.</summary>
        public string? ErrorMessage { get; }

        /// <summary>Gets a value indicating whether the read did not fail (HasContent or Empty).</summary>
        public bool IsSuccess => Status != ClipboardReadStatus.Failed;

        /// <summary>
        /// Creates a result for a clipboard that had metadata available.
        /// </summary>
        /// <param name="description">The clip metadata that was read.</param>
        /// <returns>A <see cref="ClipboardDescriptionResult"/> with <see cref="ClipboardReadStatus.HasContent"/>.</returns>
        internal static ClipboardDescriptionResult FromDescription(ClipDescriptionInfo description) =>
            new(ClipboardReadStatus.HasContent, description, null, null);

        /// <summary>
        /// Creates a result for an empty clipboard.
        /// </summary>
        /// <returns>A <see cref="ClipboardDescriptionResult"/> with <see cref="ClipboardReadStatus.Empty"/>.</returns>
        internal static ClipboardDescriptionResult Empty() =>
            new(ClipboardReadStatus.Empty, null, null, null);

        /// <summary>
        /// Creates a result for a failed read.
        /// </summary>
        /// <param name="errorCode">Stable error code (see design 1.7).</param>
        /// <param name="errorMessage">Human-readable error message.</param>
        /// <returns>A <see cref="ClipboardDescriptionResult"/> with <see cref="ClipboardReadStatus.Failed"/>.</returns>
        internal static ClipboardDescriptionResult Failed(string errorCode, string errorMessage) =>
            new(ClipboardReadStatus.Failed, null, errorCode, errorMessage);

        private ClipboardDescriptionResult(ClipboardReadStatus status, ClipDescriptionInfo? description, string? errorCode, string? errorMessage)
        {
            Status = status;
            Description = description;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
        }
    }
}
