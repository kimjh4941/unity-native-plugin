#nullable enable

#if UNITY_STANDALONE_OSX || UNITY_EDITOR
namespace JonghyunKim.NativeToolkit.Runtime.Clipboard
{
    /// <summary>
    /// Operation names reported by every macOS clipboard result, and the single-flight keys the
    /// Manager serialises on.
    /// <para>
    /// These live outside the Manager on purpose. The parser and the result types name operations
    /// too, and they are built before the Manager exists; putting the constants here keeps that
    /// layer from depending on the Manager.
    /// </para>
    /// </summary>
    public static class MacClipboardOperations
    {
        /// <summary>Replaces the pasteboard contents.</summary>
        public const string Copy = "copy";

        /// <summary>Adds items to a pasteboard this app still owns.</summary>
        public const string Append = "append";

        /// <summary>Reads every item and representation.</summary>
        public const string Read = "read";

        /// <summary>Reads the bytes for one uniform type identifier.</summary>
        public const string ReadData = "readData";

        /// <summary>Describes the pasteboard's types without reading any payload.</summary>
        public const string Snapshot = "snapshot";

        /// <summary>Empties the pasteboard.</summary>
        public const string Clear = "clear";

        /// <summary>Creates or fetches a pasteboard.</summary>
        public const string CreatePasteboard = "createPasteboard";

        /// <summary>Releases a pasteboard's server side resources.</summary>
        public const string RemovePasteboard = "removePasteboard";

        /// <summary>Reports which of the requested patterns the pasteboard matches.</summary>
        public const string DetectPatterns = "detectPatterns";

        /// <summary>Reads the matched values.</summary>
        public const string DetectValues = "detectValues";

        /// <summary>Reads limited metadata.</summary>
        public const string DetectMetadata = "detectMetadata";

        /// <summary>Current pasteboard access behaviour.</summary>
        public const string AccessBehavior = "accessBehavior";

        /// <summary>Starts reporting pasteboard changes.</summary>
        public const string StartObserving = "startObserving";

        /// <summary>Stops reporting pasteboard changes.</summary>
        public const string StopObserving = "stopObserving";

        /// <summary>Whether the pasteboard changed since this app last looked.</summary>
        public const string CheckForegroundChange = "checkForegroundChange";

        /// <summary>
        /// Single-flight key shared by <see cref="StartObserving"/> and <see cref="StopObserving"/>.
        /// Both mutate the same native subscription, so serialising them keeps a stop from landing
        /// between a start and its completion.
        /// </summary>
        public const string ObservationControlKey = "observation";
    }

    /// <summary>
    /// Size thresholds and defaults applied by the macOS clipboard Manager.
    /// </summary>
    public static class MacClipboardLimits
    {
        /// <summary>
        /// Upper bound on the total bytes of a copy or append request.
        /// <para>
        /// This is the limit callers actually meet: the native layer allows far more (100 MiB per
        /// representation, 200 MiB in total), but base64 inflates the payload by 4/3 on the way
        /// out, so the managed string is what runs out of memory first. Provisional until measured.
        /// </para>
        /// </summary>
        public const long MaxRequestBytes = 32L * 1024 * 1024;

        /// <summary>
        /// Upper bound on one base64 representation decoded from a native response.
        /// <para>
        /// Checked from the encoded length before any buffer is allocated. It does not bound the
        /// marshalled response string itself, which is already in memory by then. Provisional.
        /// </para>
        /// </summary>
        public const long MaxResponseBytesPerRepresentation = 32L * 1024 * 1024;

        /// <summary>
        /// Default polling interval for change observation, matching the native default.
        /// The native layer accepts <c>0 &lt; interval &lt;= 60</c> seconds.
        /// </summary>
        public const double DefaultObservationInterval = 0.5;
    }
}
#endif
