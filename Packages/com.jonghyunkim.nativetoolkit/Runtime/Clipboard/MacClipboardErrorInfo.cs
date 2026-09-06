#nullable enable

#if UNITY_STANDALONE_OSX || UNITY_EDITOR
// Intentional deviation from the "log every public method" rule in csharp.md: this type carries
// native error detail for payloads that may hold passwords or tokens. It is a pure value
// constructor with no side effect worth tracing, so it emits no logs at all rather than a
// shape-only line. The Manager already logs operation, success and error code at the dispatch
// boundary. This matches the native ClipboardLog redaction policy.

namespace JonghyunKim.NativeToolkit.Runtime.Clipboard
{
    /// <summary>
    /// Error detail carried by every failed macOS clipboard result.
    /// <para>
    /// <see cref="Code"/> is numeric, unlike the string codes the iOS bridge reports. Values in
    /// 1301-1599 come from the native layer unchanged; 9001 and above are produced by this
    /// package and never appear in a native response.
    /// </para>
    /// </summary>
    public readonly struct MacClipboardErrorInfo
    {
        /// <summary>Message used when the native layer reported no usable text.</summary>
        public const string UnknownErrorMessage = "An unknown clipboard error occurred.";

        /// <summary>Numeric error code. Never 0 on a failed result.</summary>
        public int Code { get; }

        /// <summary>Human-readable, English error message. Never null or blank.</summary>
        public string Message { get; }

        /// <summary>
        /// Whether this code was produced by the Unity bridge rather than by the native layer.
        /// <para>
        /// Not to be confused with the native <c>BridgeError</c> codes 1301 and 1302, which are
        /// raised inside the native bridge and are therefore native codes.
        /// </para>
        /// </summary>
        public bool IsManagedCode => Code >= 9000;

        /// <summary>
        /// Creates an error info, narrowing the native 64-bit code and normalising a null or blank
        /// message so both properties are always usable.
        /// </summary>
        /// <param name="code">Code reported by the native or bridge layer. The native ABI declares
        /// this as <c>NSInteger</c>, so it arrives as a 64-bit value.</param>
        /// <param name="message">Message reported by the native or bridge layer.</param>
        /// <returns>A normalised <see cref="MacClipboardErrorInfo"/>.</returns>
        public static MacClipboardErrorInfo Create(long code, string? message)
        {
            // An unchecked cast would wrap a value outside the int range into a plausible-looking
            // code, so the range is tested rather than truncated.
            int narrowed = code < int.MinValue || code > int.MaxValue
                ? MacClipboardErrorCodes.Unknown
                : (int)code;

            return new MacClipboardErrorInfo(
                narrowed,
                string.IsNullOrWhiteSpace(message) ? UnknownErrorMessage : message!);
        }

        private MacClipboardErrorInfo(int code, string message)
        {
            Code = code;
            Message = message;
        }
    }

    /// <summary>
    /// Every error code the macOS clipboard can report.
    /// <para>
    /// 1301-1599 mirror the native <c>BridgeError</c> and <c>ClipboardError</c> definitions and are
    /// listed in full so a caller can switch on them without consulting the native sources.
    /// 9001 and above exist only in this package.
    /// </para>
    /// </summary>
    public static class MacClipboardErrorCodes
    {
        // ── Native: BridgeError ──────────────────────────────────────────────

        /// <summary>An argument was supplied but could not be parsed as JSON.</summary>
        public const int ParseFailed = 1301;

        /// <summary>A required argument was missing, or a required event callback was null.</summary>
        public const int ContractViolation = 1302;

        // ── Native: ClipboardError ───────────────────────────────────────────

        /// <summary>No items were supplied to a copy or append.</summary>
        public const int EmptyContent = 1501;

        /// <summary>An item carries no representations.</summary>
        public const int EmptyRepresentations = 1502;

        /// <summary>An empty pattern set was passed to a detection API.</summary>
        public const int EmptyDetectionPatterns = 1503;

        /// <summary>The string is not a usable uniform type identifier.</summary>
        public const int InvalidTypeIdentifier = 1504;

        /// <summary>The pasteboard name is empty or otherwise unusable.</summary>
        public const int InvalidPasteboardName = 1505;

        /// <summary>A representation, or the whole payload, exceeds the native hard limit.</summary>
        public const int ContentTooLarge = 1506;

        /// <summary>The pasteboard could not be read. Only Read and Snapshot reach this.</summary>
        public const int PasteboardUnavailable = 1507;

        /// <summary>Release was requested for the general or another standard pasteboard.</summary>
        public const int CannotReleaseStandardPasteboard = 1508;

        /// <summary>The pasteboard rejected a copy.</summary>
        public const int WriteRejected = 1509;

        /// <summary>The pasteboard rejected an append.</summary>
        public const int AppendRejected = 1510;

        /// <summary>The pasteboard changed owner, so append is no longer possible.</summary>
        public const int OwnershipLost = 1511;

        /// <summary>An empty type filter was supplied. Pass null to disable filtering instead.</summary>
        public const int EmptyTypeFilter = 1512;

        /// <summary>Pasteboard detection is unavailable below macOS 15.4.</summary>
        public const int DetectionUnavailable = 1513;

        /// <summary>The user denied access to the pasteboard contents during detection.</summary>
        public const int DetectionDenied = 1514;

        /// <summary>Detection failed for a reason other than denial. Plain text reaches this.</summary>
        public const int DetectionFailed = 1515;

        /// <summary>Loading one pasted item provider failed. Not reachable through the C ABI.</summary>
        public const int PasteLoadFailed = 1521;

        /// <summary>Loading pasted items timed out. Not reachable through the C ABI.</summary>
        public const int PasteLoadTimedOut = 1522;

        /// <summary>A configuration value violates its constraints, such as the poll interval.</summary>
        public const int InvalidConfiguration = 1523;

        /// <summary>A detection API was cancelled.</summary>
        public const int Cancelled = 1524;

        /// <summary>Any other native failure, including a result the native layer could not encode.</summary>
        public const int Unknown = 1599;

        // ── Unity bridge only ────────────────────────────────────────────────

        /// <summary>The same operation is already awaiting a native callback.</summary>
        public const int Busy = 9001;

        /// <summary>The native bridge cannot be reached, or the native call itself threw.</summary>
        public const int BridgeUnavailable = 9002;

        /// <summary>An instance method was called off the Unity main thread.</summary>
        public const int MainThreadRequired = 9003;

        /// <summary>The Manager has been destroyed.</summary>
        public const int ManagerDestroyed = 9004;

        /// <summary>A required argument was null. Has no native equivalent.</summary>
        public const int InvalidRequest = 9005;

        /// <summary>A successful native response could not be parsed.</summary>
        public const int ResponseParseFailed = 9006;

        /// <summary>The request exceeds <see cref="MacClipboardLimits.MaxRequestBytes"/>.</summary>
        public const int RequestTooLarge = 9007;
    }
}
#endif
