#nullable enable

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
namespace JonghyunKim.NativeToolkit.Runtime.Clipboard
{
    using System;

    /// <summary>
    /// Write options shared by every copy API. They map to the native
    /// <c>CLIPBOARD_WRITE_OPTION_*</c> bit flags.
    /// <para>
    /// ReserveDeferredFormats does not take these: the native reservation API has no options
    /// parameter, so a deferred reservation cannot be excluded from history or roaming.
    /// </para>
    /// </summary>
    [Flags]
    public enum WindowsClipboardWriteOptions : uint
    {
        /// <summary>Default placement.</summary>
        None = 0x0,

        /// <summary>Keep the content out of the Windows clipboard history.</summary>
        ExcludeHistory = 0x1,

        /// <summary>Keep the content out of the cloud clipboard.</summary>
        ExcludeRoaming = 0x2,

        /// <summary>Both exclusions, for content such as passwords.</summary>
        Sensitive = ExcludeHistory | ExcludeRoaming
    }

    /// <summary>
    /// Which payload a <see cref="WindowsClipboardFormatPayload"/> carries.
    /// </summary>
    public enum WindowsClipboardPayloadKind
    {
        /// <summary>Plain text, for text-shaped formats only.</summary>
        Text,

        /// <summary>An HTML fragment. The native layer builds the full CF_HTML payload.</summary>
        Html,

        /// <summary>Base64-encoded raw bytes, valid for any format.</summary>
        Base64
    }

    /// <summary>
    /// One entry of a CopyMultipleFormats call: a clipboard format together with exactly one
    /// payload.
    /// <para>
    /// Entries are placed richest format first. The native layer rejects duplicate formats and a
    /// payload kind that does not fit the target format before anything is placed.
    /// </para>
    /// <para>
    /// CF_TEXT is encoded by the native layer with the system ANSI code page, so non-ASCII text
    /// can be lost for that format. Use CF_UNICODETEXT when the content may be non-ASCII.
    /// </para>
    /// </summary>
    public readonly struct WindowsClipboardFormatPayload
    {
        /// <summary>Clipboard format name, either a CF_* constant name or a registered name.</summary>
        public string Format { get; }

        /// <summary>Which payload this entry carries.</summary>
        public WindowsClipboardPayloadKind Kind { get; }

        /// <summary>The payload value, interpreted according to <see cref="Kind"/>.</summary>
        public string Value { get; }

        /// <summary>Creates a text entry.</summary>
        /// <param name="format">Clipboard format name.</param>
        /// <param name="text">The plain text to place.</param>
        /// <returns>A text payload entry.</returns>
        public static WindowsClipboardFormatPayload Text(string format, string text) =>
            new(format, WindowsClipboardPayloadKind.Text, text);

        /// <summary>Creates an HTML entry.</summary>
        /// <param name="format">Clipboard format name, normally "HTML Format".</param>
        /// <param name="html">The HTML fragment to place.</param>
        /// <returns>An HTML payload entry.</returns>
        public static WindowsClipboardFormatPayload Html(string format, string html) =>
            new(format, WindowsClipboardPayloadKind.Html, html);

        /// <summary>Creates a raw-bytes entry.</summary>
        /// <param name="format">Clipboard format name.</param>
        /// <param name="base64">The payload, already base64 encoded.</param>
        /// <returns>A base64 payload entry.</returns>
        public static WindowsClipboardFormatPayload Base64(string format, string base64) =>
            new(format, WindowsClipboardPayloadKind.Base64, base64);

        /// <summary>Creates a raw-bytes entry from bytes.</summary>
        /// <param name="format">Clipboard format name.</param>
        /// <param name="data">The bytes to place. They are base64 encoded here.</param>
        /// <returns>A base64 payload entry.</returns>
        public static WindowsClipboardFormatPayload Bytes(string format, byte[] data) =>
            // A null array is not the same as empty data: encoding it as an empty payload would
            // place a zero-length format on the clipboard instead of reporting the mistake, so it
            // is left null here and rejected by TryValidate.
            new(format, WindowsClipboardPayloadKind.Base64,
                data == null ? null! : Convert.ToBase64String(data));

        /// <summary>
        /// The JSON key this entry uses inside the CopyMultipleFormats payload.
        /// </summary>
        internal string PayloadKey => Kind switch
        {
            WindowsClipboardPayloadKind.Text => "text",
            WindowsClipboardPayloadKind.Html => "html",
            _ => "base64"
        };

        /// <summary>
        /// Validates the entry before it reaches the native layer.
        /// </summary>
        /// <param name="detail">Set to the reason when validation fails, otherwise null.</param>
        /// <returns>True when the entry can be serialized.</returns>
        internal bool TryValidate(out string? detail)
        {
            if (string.IsNullOrWhiteSpace(Format))
            {
                detail = "a format name was null or blank";
                return false;
            }
            if (Value == null)
            {
                detail = $"the payload of format {Format} was null";
                return false;
            }
            detail = null;
            return true;
        }

        private WindowsClipboardFormatPayload(string format, WindowsClipboardPayloadKind kind, string value)
        {
            Format = format;
            Kind = kind;
            Value = value;
        }
    }
}
#endif
