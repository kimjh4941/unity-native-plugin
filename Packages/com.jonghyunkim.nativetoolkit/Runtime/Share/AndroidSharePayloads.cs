#nullable enable

using System;

namespace JonghyunKim.NativeToolkit.Runtime.Share
{
    /// <summary>
    /// Represents a single custom action shown in the Android share chooser (API 34+).
    /// </summary>
    [Serializable]
    public sealed class ChooserActionPayload
    {
        /// <summary>Label displayed for this chooser action.</summary>
        public string label = string.Empty;

        /// <summary>Base64-encoded PNG/JPEG icon for this chooser action.</summary>
        public string iconBase64 = string.Empty;

        /// <summary>
        /// Intent action broadcast when this chooser action is tapped.
        /// Defaults to <c>android.intent.action.SEND</c> on the native side when omitted.
        /// The receiving broadcast receiver must be declared in the app's manifest.
        /// </summary>
        public string? intentAction;
    }

    /// <summary>
    /// Payload for sharing plain text via the Android share sheet.
    /// Maps to the <c>shareText</c> and <c>shareWithCallback</c> native operations.
    /// </summary>
    [Serializable]
    public sealed class ShareTextPayload
    {
        /// <summary>Text content to share. Required.</summary>
        public string text = string.Empty;

        /// <summary>Optional chooser dialog title.</summary>
        public string? title;

        /// <summary>Optional email subject when sharing to email apps.</summary>
        public string? subject;

        /// <summary>MIME type of the shared content. Defaults to <c>text/plain</c> on the native side when omitted.</summary>
        public string? mimeType;

        /// <summary>Custom actions shown in the chooser (API 34+ only; ignored on lower API levels).</summary>
        public ChooserActionPayload[]? chooserActions;

        /// <summary>Preview title shown in the chooser content preview area.</summary>
        public string? previewTitle;

        /// <summary>Absolute file path to a thumbnail image shown in the chooser preview area.</summary>
        public string? previewThumbnailPath;
    }

    /// <summary>
    /// Payload for sharing a single image file via the Android share sheet.
    /// Maps to the <c>shareImage</c> native operation.
    /// </summary>
    [Serializable]
    public sealed class ShareImagePayload
    {
        /// <summary>Absolute file path to the image. Must be within a FileProvider-accessible directory.</summary>
        public string filePath = string.Empty;

        /// <summary>MIME type of the image. Defaults to <c>image/*</c> on the native side when omitted.</summary>
        public string? mimeType;
    }

    /// <summary>
    /// Payload for sharing multiple image files via the Android share sheet.
    /// Maps to the <c>shareImages</c> native operation.
    /// </summary>
    [Serializable]
    public sealed class ShareImagesPayload
    {
        /// <summary>Absolute file paths to the images. Must not be empty. Each file must be within a FileProvider-accessible directory.</summary>
        public string[] filePaths = Array.Empty<string>();
    }

    /// <summary>
    /// Payload for sharing a single arbitrary file via the Android share sheet.
    /// Maps to the <c>shareFile</c> native operation.
    /// </summary>
    [Serializable]
    public sealed class ShareFilePayload
    {
        /// <summary>Absolute file path to the file. Must be within a FileProvider-accessible directory.</summary>
        public string filePath = string.Empty;
    }

    /// <summary>
    /// Payload for sharing multiple arbitrary files via the Android share sheet.
    /// Maps to the <c>shareFiles</c> native operation.
    /// </summary>
    [Serializable]
    public sealed class ShareFilesPayload
    {
        /// <summary>Absolute file paths to the files. Must not be empty. Each file must be within a FileProvider-accessible directory.</summary>
        public string[] filePaths = Array.Empty<string>();
    }

    /// <summary>
    /// Payload for registering an Android Direct Share target shortcut.
    /// Maps to the <c>registerDirectShareTarget</c> native operation.
    /// </summary>
    [Serializable]
    public sealed class DirectShareTargetPayload
    {
        /// <summary>Unique shortcut identifier for this Direct Share target.</summary>
        public string id = string.Empty;

        /// <summary>Display label shown in the Direct Share row.</summary>
        public string label = string.Empty;

        /// <summary>Base64-encoded PNG/JPEG icon for this Direct Share target.</summary>
        public string iconBase64 = string.Empty;

        /// <summary>
        /// Shortcut category. Defaults to <c>android.shortcut.conversation</c> on the native side when omitted.
        /// </summary>
        public string? category;
    }

    /// <summary>
    /// Payload for removing previously registered Android Direct Share target shortcuts.
    /// Maps to the <c>removeDirectShareTargets</c> native operation.
    /// </summary>
    [Serializable]
    public sealed class RemoveDirectShareTargetsPayload
    {
        /// <summary>Shortcut identifiers to remove. Must not be empty.</summary>
        public string[] ids = Array.Empty<string>();
    }
}
