#nullable enable

using System;

namespace JonghyunKim.NativeToolkit.Runtime.Share
{
    /// <summary>
    /// Represents a single item to be shared through the macOS sharing service picker.
    /// </summary>
    [Serializable]
    public sealed class MacShareItem
    {
        /// <summary>Item type. One of <c>"text"</c>, <c>"url"</c>, <c>"image"</c>, <c>"file"</c>.</summary>
        public string type = "text";

        /// <summary>Item value. Interpretation depends on <see cref="type"/>.</summary>
        public string value = string.Empty;

        /// <summary>Creates a plain text share item.</summary>
        /// <param name="value">Text content to share.</param>
        /// <returns>A <see cref="MacShareItem"/> of type <c>"text"</c>.</returns>
        public static MacShareItem Text(string value) => new() { type = "text", value = value };

        /// <summary>Creates a URL share item.</summary>
        /// <param name="value">URL string to share.</param>
        /// <returns>A <see cref="MacShareItem"/> of type <c>"url"</c>.</returns>
        public static MacShareItem Url(string value) => new() { type = "url", value = value };

        /// <summary>Creates an image file share item.</summary>
        /// <param name="path">Local file path to the image.</param>
        /// <returns>A <see cref="MacShareItem"/> of type <c>"image"</c>.</returns>
        public static MacShareItem Image(string path) => new() { type = "image", value = path };

        /// <summary>Creates an arbitrary file share item.</summary>
        /// <param name="path">Local file path to the file.</param>
        /// <returns>A <see cref="MacShareItem"/> of type <c>"file"</c>.</returns>
        public static MacShareItem File(string path) => new() { type = "file", value = path };
    }

    /// <summary>
    /// Payload for presenting the macOS sharing service via <c>MacShareManager.Share</c> /
    /// <c>MacShareManager.ShareViaService</c>.
    /// </summary>
    /// <remarks>
    /// <see cref="items"/> is expected to contain non-null entries under normal usage. Null
    /// entries are tolerated defensively (e.g. from Unity serialization or externally generated
    /// data): <c>MacShareJsonBuilder</c> excludes them rather than throwing.
    /// </remarks>
    [Serializable]
    public sealed class MacShareContentPayload
    {
        /// <summary>Items to share. Required, must be non-empty.</summary>
        public MacShareItem[] items = Array.Empty<MacShareItem>();

        /// <summary>Optional recipients, used by Mail and similar services.</summary>
        public string[]? recipients;

        /// <summary>Optional subject, used by Mail and similar services.</summary>
        public string? subject;

        /// <summary>
        /// Service display titles to exclude, matched on a best-effort basis against
        /// <c>NSSharingService.title</c>.
        /// </summary>
        public string[]? excludedServiceTitles;
    }

    /// <summary>
    /// Well-known raw <c>NSSharingService.Name</c> identifiers for use with
    /// <c>MacShareManager.ShareViaService</c>.
    /// </summary>
    /// <remarks>
    /// These are input identifiers, NOT the display names returned in
    /// <see cref="MacShareResult.ServiceName"/>. Confirm each value against the target SDK's
    /// <c>NSSharingService.Name.*.rawValue</c> before relying on it; an incorrect value surfaces
    /// as a <c>serviceUnavailable</c> error at runtime. This list is not exhaustive; arbitrary raw
    /// identifier strings may still be passed directly.
    /// </remarks>
    public static class MacShareServiceNames
    {
        /// <summary>Compose a new Mail message. (<c>NSSharingService.Name.composeEmail</c>)</summary>
        public const string MailCompose = "com.apple.share.Mail.compose";

        /// <summary>Compose a new Messages message. (<c>NSSharingService.Name.composeMessage</c>)</summary>
        public const string Message = "com.apple.messages.ShareExtension";
    }
}
