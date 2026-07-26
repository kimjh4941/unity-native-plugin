#nullable enable

using System;

namespace JonghyunKim.NativeToolkit.Runtime.Clipboard
{
    /// <summary>
    /// Payload for copying plain text to the clipboard via <see cref="AndroidClipboardManager.CopyPlainText"/>.
    /// </summary>
    [Serializable]
    public sealed class CopyPlainTextPayload
    {
        /// <summary>
        /// Text to copy. A blank value is accepted by the native layer; unlike
        /// <see cref="CopyHtmlTextPayload.htmlText"/>, blank plain text never fails.
        /// </summary>
        public string text = string.Empty;

        /// <summary>Optional clip label. Omitted from the JSON when null or blank; the native layer falls back to "".</summary>
        public string? label;

        /// <summary>Sensitive-content display hint (Android 13+ preview suppression).</summary>
        public bool isSensitive;
    }

    /// <summary>
    /// Payload for copying HTML text to the clipboard via <see cref="AndroidClipboardManager.CopyHtmlText"/>.
    /// </summary>
    [Serializable]
    public sealed class CopyHtmlTextPayload
    {
        /// <summary>Plain-text fallback. A blank value is accepted.</summary>
        public string plainText = string.Empty;

        /// <summary>
        /// HTML representation. Required; a blank value fails with the CLIPBOARD_EMPTY_CONTENT
        /// error, unlike <see cref="CopyPlainTextPayload.text"/> which accepts blank values.
        /// </summary>
        public string htmlText = string.Empty;

        /// <summary>Optional clip label.</summary>
        public string? label;

        /// <summary>Sensitive-content display hint (Android 13+ preview suppression).</summary>
        public bool isSensitive;
    }

    /// <summary>
    /// Payload for copying a URI (content://, including image/file references) to the clipboard via
    /// <see cref="AndroidClipboardManager.CopyUri"/>.
    /// </summary>
    [Serializable]
    public sealed class CopyUriPayload
    {
        /// <summary>URI string. Required; blank or unparseable values fail with CLIPBOARD_INVALID_URI.</summary>
        public string uri = string.Empty;

        /// <summary>Optional clip label.</summary>
        public string? label;

        /// <summary>Sensitive-content display hint (Android 13+ preview suppression).</summary>
        public bool isSensitive;
    }

    /// <summary>
    /// Payload for copying multiple plain-text items (same form) to the clipboard via
    /// <see cref="AndroidClipboardManager.CopyMultipleText"/>.
    /// </summary>
    [Serializable]
    public sealed class CopyMultipleTextPayload
    {
        /// <summary>
        /// Text items. Required; an empty array fails with CLIPBOARD_EMPTY_ITEMS. Individual empty
        /// strings inside the array are accepted.
        /// </summary>
        public string[] texts = Array.Empty<string>();

        /// <summary>Optional clip label.</summary>
        public string? label;

        /// <summary>Sensitive-content display hint (Android 13+ preview suppression).</summary>
        public bool isSensitive;
    }
}
