#nullable enable

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
namespace JonghyunKim.NativeToolkit.Runtime.Clipboard
{
    /// <summary>
    /// Result of GetHistoryAvailability.
    /// <para>
    /// Query this whenever the current setting matters: the native flag callbacks fire at most
    /// once per process and never when they are registered while history is disabled. The query
    /// itself requires the process to be in the foreground and reports NotForeground otherwise.
    /// </para>
    /// </summary>
    public readonly struct WindowsClipboardAvailabilityResult
    {
        /// <summary>Native operation name that produced this result.</summary>
        public string Operation { get; }

        /// <summary>Whether the query completed.</summary>
        public bool IsSuccess { get; }

        /// <summary>Whether Windows clipboard history is on. Meaningful only on success.</summary>
        public bool HistoryEnabled { get; }

        /// <summary>Whether cloud sync is on. Meaningful only on success.</summary>
        public bool RoamingEnabled { get; }

        /// <summary>Error code. None on success.</summary>
        public WindowsClipboardErrorCode ErrorCode { get; }

        /// <summary>Human-readable message. Null on success.</summary>
        public string? ErrorMessage { get; }

        /// <summary>Creates a successful result.</summary>
        /// <param name="operation">Native operation name.</param>
        /// <param name="historyEnabled">Whether clipboard history is on.</param>
        /// <param name="roamingEnabled">Whether cloud sync is on.</param>
        /// <returns>A successful result.</returns>
        public static WindowsClipboardAvailabilityResult Success(
            string operation, bool historyEnabled, bool roamingEnabled) =>
            new(operation, true, historyEnabled, roamingEnabled, WindowsClipboardErrorCode.None, null);

        /// <summary>Creates a failed result.</summary>
        /// <param name="operation">Native operation name.</param>
        /// <param name="code">The error code.</param>
        /// <param name="detail">Extra text for InvalidArgument.</param>
        /// <returns>A failed result whose message is never null.</returns>
        public static WindowsClipboardAvailabilityResult Failure(
            string operation, WindowsClipboardErrorCode code, string? detail = null)
        {
            WindowsClipboardErrorCode effective =
                code == WindowsClipboardErrorCode.None ? WindowsClipboardErrorCode.Unknown : code;
            return new WindowsClipboardAvailabilityResult(
                operation, false, false, false, effective, effective.ToMessage(operation, detail));
        }

        private WindowsClipboardAvailabilityResult(string operation, bool isSuccess,
            bool historyEnabled, bool roamingEnabled,
            WindowsClipboardErrorCode errorCode, string? errorMessage)
        {
            Operation = operation;
            IsSuccess = isSuccess;
            HistoryEnabled = historyEnabled;
            RoamingEnabled = roamingEnabled;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
        }
    }
}
#endif
