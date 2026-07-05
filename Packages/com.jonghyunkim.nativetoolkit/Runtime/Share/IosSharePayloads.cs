#nullable enable

using System;

namespace JonghyunKim.NativeToolkit.Runtime.Share
{
    /// <summary>
    /// Represents a single item to be shared through the iOS system share sheet.
    /// </summary>
    [Serializable]
    public sealed class IosShareItem
    {
        /// <summary>Item type. One of <c>"text"</c>, <c>"url"</c>, <c>"image"</c>, <c>"file"</c>.</summary>
        public string type = "text";

        /// <summary>Item value. Interpretation depends on <see cref="type"/>.</summary>
        public string value = string.Empty;

        /// <summary>Creates a plain text share item.</summary>
        /// <param name="value">Text content to share.</param>
        /// <returns>An <see cref="IosShareItem"/> of type <c>"text"</c>.</returns>
        public static IosShareItem Text(string value) => new() { type = "text", value = value };

        /// <summary>Creates a URL share item.</summary>
        /// <param name="value">URL string to share.</param>
        /// <returns>An <see cref="IosShareItem"/> of type <c>"url"</c>.</returns>
        public static IosShareItem Url(string value) => new() { type = "url", value = value };

        /// <summary>Creates an image file share item.</summary>
        /// <param name="path">Local file path to the image.</param>
        /// <returns>An <see cref="IosShareItem"/> of type <c>"image"</c>.</returns>
        public static IosShareItem Image(string path) => new() { type = "image", value = path };

        /// <summary>Creates an arbitrary file share item.</summary>
        /// <param name="path">Local file path to the file.</param>
        /// <returns>An <see cref="IosShareItem"/> of type <c>"file"</c>.</returns>
        public static IosShareItem File(string path) => new() { type = "file", value = path };
    }

    /// <summary>
    /// Payload for presenting the iOS system share sheet via <c>IosShareManager.Share</c>.
    /// </summary>
    /// <remarks>
    /// <see cref="items"/> is expected to contain non-null entries under normal usage. Null
    /// entries are tolerated defensively (e.g. from Unity serialization or externally generated
    /// data): <c>IosShareJsonBuilder</c> excludes them rather than throwing.
    /// </remarks>
    [Serializable]
    public sealed class IosShareContentPayload
    {
        /// <summary>Items to share. Required, must be non-empty.</summary>
        public IosShareItem[] items = Array.Empty<IosShareItem>();

        /// <summary>Optional subject, used by Mail and similar activities.</summary>
        public string? subject;

        /// <summary>Optional preview title shown in the share sheet header.</summary>
        public string? previewTitle;

        /// <summary>
        /// Activity types to exclude, as raw identifiers (e.g.
        /// <c>"com.apple.UIKit.activity.PostToFacebook"</c>).
        /// </summary>
        public string[]? excludedActivityTypes;
    }
}
