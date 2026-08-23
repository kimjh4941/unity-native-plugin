#nullable enable

#if UNITY_IOS || UNITY_EDITOR
// Intentional deviation from the "log every public method" rule in csharp.md: these result types
// carry clipboard content and native error detail. The factories are pure value constructors, so
// they emit no logs at all rather than a shape-only line — the Manager already logs operation,
// status and error code at the dispatch boundary. This matches the native ClipboardRedaction policy.

namespace JonghyunKim.NativeToolkit.Runtime.Clipboard
{
    /// <summary>
    /// Error detail carried by every failed iOS clipboard result.
    /// <para>
    /// <see cref="Code"/> mirrors the native <c>ClipboardError.errorCode</c> (a stable
    /// <c>CLIPBOARD_*</c> string) for failures raised by the native layer, or one of the
    /// bridge-only codes raised by <see cref="IosClipboardManager"/> itself.
    /// </para>
    /// <para>
    /// <see cref="Domain"/> and <see cref="NativeCode"/> come from the optional
    /// <c>error.details</c> object, which the native layer attaches only to
    /// <c>CLIPBOARD_LOAD_FAILED</c>, <c>CLIPBOARD_FILE_COPY_FAILED</c>,
    /// <c>CLIPBOARD_DETECTION_FAILED</c> and <c>CLIPBOARD_UNKNOWN</c>.
    /// </para>
    /// </summary>
    public readonly struct IosClipboardErrorInfo
    {
        /// <summary>Fallback code used when the native layer reported no usable error code.</summary>
        public const string UnknownErrorCode = "CLIPBOARD_UNKNOWN";

        /// <summary>Fallback message used when the native layer reported no usable message.</summary>
        public const string UnknownErrorMessage = "An unknown error occurred.";

        /// <summary>Stable error code. Never null or blank.</summary>
        public string Code { get; }

        /// <summary>Human-readable, English error message. Never null or blank.</summary>
        public string Message { get; }

        /// <summary>Diagnostic error domain, or <c>null</c> when the failure carried no details.</summary>
        public string? Domain { get; }

        /// <summary>Diagnostic numeric code, or <c>null</c> when the failure carried no details.</summary>
        public int? NativeCode { get; }

        /// <summary>
        /// Creates an error info, normalizing a null or blank code/message to the
        /// <c>CLIPBOARD_UNKNOWN</c> defaults so both properties are always usable.
        /// </summary>
        /// <param name="code">Stable error code reported by the native or bridge layer.</param>
        /// <param name="message">Error message reported by the native or bridge layer.</param>
        /// <param name="domain">Optional diagnostic domain from <c>error.details</c>.</param>
        /// <param name="nativeCode">Optional diagnostic numeric code from <c>error.details</c>.</param>
        /// <returns>A normalized <see cref="IosClipboardErrorInfo"/>.</returns>
        public static IosClipboardErrorInfo Create(
            string? code,
            string? message,
            string? domain = null,
            int? nativeCode = null)
        {
            return new IosClipboardErrorInfo(
                string.IsNullOrWhiteSpace(code) ? UnknownErrorCode : code!,
                string.IsNullOrWhiteSpace(message) ? UnknownErrorMessage : message!,
                domain,
                nativeCode);
        }

        private IosClipboardErrorInfo(string code, string message, string? domain, int? nativeCode)
        {
            Code = code;
            Message = message;
            Domain = domain;
            NativeCode = nativeCode;
        }
    }
}
#endif
