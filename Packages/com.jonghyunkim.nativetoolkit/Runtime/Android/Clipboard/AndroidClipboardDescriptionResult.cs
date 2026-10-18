#nullable enable

using System.Collections.Generic;
using UnityEngine.Scripting.APIUpdating;

namespace JonghyunKim.NativeToolkit.Runtime.Android.Clipboard
{
    /// <summary>
    /// Clipboard metadata returned by <see cref="AndroidClipboardManager.GetDescription"/>, obtained
    /// without touching the clip body.
    /// </summary>
    [MovedFrom(false, sourceNamespace: "JonghyunKim.NativeToolkit.Runtime.Clipboard", sourceClassName: "ClipDescriptionInfo")]
    public sealed class AndroidClipDescriptionInfo
    {
        /// <summary>Clip label, if present.</summary>
        public string? Label { get; }

        /// <summary>MIME types available on the clip. Never null; empty when the clip has none.</summary>
        public IReadOnlyList<string> MimeTypes { get; }

        /// <summary>Whether the clip is styled text (API 31+).</summary>
        public bool IsStyledText { get; }

        /// <summary>
        /// Raw ClipDescription.CLASSIFICATION_* value. Null when the OS did not report one.
        /// </summary>
        public int? ClassificationStatus { get; }

        internal AndroidClipDescriptionInfo(string? label, IReadOnlyList<string> mimeTypes, bool isStyledText, int? classificationStatus)
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
    [MovedFrom(false, sourceNamespace: "JonghyunKim.NativeToolkit.Runtime.Clipboard", sourceClassName: "ClipboardDescriptionResult")]
    public readonly struct AndroidClipboardDescriptionResult
    {
        /// <summary>Gets the outcome category of this read.</summary>
        public AndroidClipboardReadStatus Status { get; }

        /// <summary>Non-null only when <see cref="Status"/> is <see cref="AndroidClipboardReadStatus.HasContent"/>.</summary>
        public AndroidClipDescriptionInfo? Description { get; }

        /// <summary>Non-null only when <see cref="Status"/> is <see cref="AndroidClipboardReadStatus.Failed"/>.</summary>
        public string? ErrorCode { get; }

        /// <summary>Non-null only when <see cref="Status"/> is <see cref="AndroidClipboardReadStatus.Failed"/>.</summary>
        public string? ErrorMessage { get; }

        /// <summary>Gets a value indicating whether the read did not fail (HasContent or Empty).</summary>
        public bool IsSuccess => Status != AndroidClipboardReadStatus.Failed;

        /// <summary>
        /// Creates a result for a clipboard that had metadata available.
        /// </summary>
        /// <param name="description">The clip metadata that was read.</param>
        /// <returns>A <see cref="AndroidClipboardDescriptionResult"/> with <see cref="AndroidClipboardReadStatus.HasContent"/>.</returns>
        internal static AndroidClipboardDescriptionResult FromDescription(AndroidClipDescriptionInfo description) =>
            new(AndroidClipboardReadStatus.HasContent, description, null, null);

        /// <summary>
        /// Creates a result for an empty clipboard.
        /// </summary>
        /// <returns>A <see cref="AndroidClipboardDescriptionResult"/> with <see cref="AndroidClipboardReadStatus.Empty"/>.</returns>
        internal static AndroidClipboardDescriptionResult Empty() =>
            new(AndroidClipboardReadStatus.Empty, null, null, null);

        /// <summary>
        /// Creates a result for a failed read.
        /// </summary>
        /// <param name="errorCode">Stable error code (see design 1.7).</param>
        /// <param name="errorMessage">Human-readable error message.</param>
        /// <returns>A <see cref="AndroidClipboardDescriptionResult"/> with <see cref="AndroidClipboardReadStatus.Failed"/>.</returns>
        internal static AndroidClipboardDescriptionResult Failed(string errorCode, string errorMessage) =>
            new(AndroidClipboardReadStatus.Failed, null, errorCode, errorMessage);

        private AndroidClipboardDescriptionResult(AndroidClipboardReadStatus status, AndroidClipDescriptionInfo? description, string? errorCode, string? errorMessage)
        {
            Status = status;
            Description = description;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
        }
    }
}
