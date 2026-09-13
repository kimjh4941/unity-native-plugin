#nullable enable

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
namespace JonghyunKim.NativeToolkit.Runtime.Clipboard
{
    /// <summary>
    /// Error codes reported by the Windows clipboard APIs.
    /// <para>
    /// Values 0-19 mirror the native <c>CLIPBOARD_ERROR_*</c> constants exported by
    /// WindowsLibrary.dll and are returned through the native <c>pError</c> out parameter.
    /// Values from 1000 up are produced by this C# layer alone and never come from the native
    /// side, so a caller can always tell which layer rejected a call.
    /// </para>
    /// </summary>
    public enum WindowsClipboardErrorCode
    {
        /// <summary>The operation succeeded.</summary>
        None = 0,

        /// <summary>A native argument was null, zero-sized, or failed JSON validation.</summary>
        InvalidParameter = 1,

        /// <summary>The native manager was not initialized, or was already shut down.</summary>
        NotInitialized = 2,

        /// <summary>Another process holds the clipboard open.</summary>
        Busy = 3,

        /// <summary>The clipboard, or the clipboard history, held nothing.</summary>
        Empty = 4,

        /// <summary>The requested format is not present on the clipboard.</summary>
        FormatUnavailable = 5,

        /// <summary>Clipboard data failed the native boundary validation.</summary>
        InvalidData = 6,

        /// <summary>The output buffer was absent or too small; the required size was returned.</summary>
        BufferTooSmall = 7,

        /// <summary>An allocation failed on the native side.</summary>
        OutOfMemory = 8,

        /// <summary>Access to the clipboard history was denied.</summary>
        AccessDenied = 9,

        /// <summary>Windows clipboard history is turned off.</summary>
        HistoryDisabled = 10,

        /// <summary>The history item was already deleted.</summary>
        ItemDeleted = 11,

        /// <summary>Registering or unregistering the clipboard listener failed.</summary>
        MonitorRegisterFailed = 12,

        /// <summary>A rollback failed and the clipboard may hold partial content.</summary>
        PartialState = 13,

        /// <summary>A UI-thread-limited API was called from another thread.</summary>
        WrongThread = 14,

        /// <summary>The request was canceled, or was drained by shutdown.</summary>
        Canceled = 15,

        /// <summary>The operation has no equivalent on Windows.</summary>
        NotSupported = 16,

        /// <summary>The process is not in the foreground, which the WinRT history APIs require.</summary>
        NotForeground = 17,

        /// <summary>The calling thread is not an initialized STA.</summary>
        WrongApartment = 18,

        /// <summary>An unexpected native failure. The raw code is written to the native log.</summary>
        Unknown = 19,

        /// <summary>Not running on a Windows player build. The editor always reports this.</summary>
        PlatformUnavailable = 1000,

        /// <summary>The native clipboard bridge could not be loaded.</summary>
        BridgeUnavailable = 1001,

        /// <summary>The API was called from a thread other than the Unity main thread.</summary>
        MainThreadRequired = 1002,

        /// <summary>The manager was destroyed and rejects every further operation.</summary>
        ManagerDestroyed = 1003,

        /// <summary>Initialize has not succeeded yet.</summary>
        NotInitializedByHost = 1004,

        /// <summary>An argument failed the managed validation performed before the native call.</summary>
        InvalidArgument = 1005,

        /// <summary>The shutdown drain exceeded its retry budget.</summary>
        ShutdownTimeout = 1006,

        /// <summary>A payload returned by the native side could not be parsed.</summary>
        ResultParseFailed = 1007,

        /// <summary>A request was rejected without a native error code. Defensive only.</summary>
        RequestRejected = 1008,

        /// <summary>The main thread could not be made an STA, so the manager cannot initialize.</summary>
        ApartmentUnavailable = 1009,

        /// <summary>The same operation is already in flight.</summary>
        OperationBusy = 1010,

        /// <summary>The manager is shutting down and accepts no new operations.</summary>
        ShuttingDown = 1011
    }

    /// <summary>
    /// Turns a <see cref="WindowsClipboardErrorCode"/> into the English message carried by a result.
    /// </summary>
    public static class WindowsClipboardErrorCodeExtensions
    {
        /// <summary>
        /// Builds the message for an error code.
        /// </summary>
        /// <param name="code">The code to describe.</param>
        /// <param name="operation">Native operation name, used by the messages of the 1000 range.</param>
        /// <param name="detail">Extra text for <see cref="WindowsClipboardErrorCode.InvalidArgument"/>.</param>
        /// <returns>Null for <see cref="WindowsClipboardErrorCode.None"/>, otherwise the message.</returns>
        public static string? ToMessage(this WindowsClipboardErrorCode code, string operation, string? detail = null)
        {
            return code switch
            {
                WindowsClipboardErrorCode.None => null,

                // Native codes keep the wording of the native design so that a message found in a
                // Unity log can be matched against a message found in the native log.
                WindowsClipboardErrorCode.InvalidParameter => "Invalid parameter",
                WindowsClipboardErrorCode.NotInitialized => "Clipboard manager is not initialized",
                WindowsClipboardErrorCode.Busy => "Clipboard is held by another process",
                WindowsClipboardErrorCode.Empty => "Clipboard is empty",
                WindowsClipboardErrorCode.FormatUnavailable => "Requested format is not available",
                WindowsClipboardErrorCode.InvalidData => "Clipboard data failed validation",
                WindowsClipboardErrorCode.BufferTooSmall => "Buffer too small; required size returned",
                WindowsClipboardErrorCode.OutOfMemory => "Out of memory",
                WindowsClipboardErrorCode.AccessDenied => "Access to clipboard history is denied",
                WindowsClipboardErrorCode.HistoryDisabled => "Clipboard history is disabled",
                WindowsClipboardErrorCode.ItemDeleted => "History item was already deleted",
                WindowsClipboardErrorCode.MonitorRegisterFailed => "Failed to register or unregister the clipboard listener",
                WindowsClipboardErrorCode.PartialState => "Rollback failed; clipboard may hold partial content",
                WindowsClipboardErrorCode.WrongThread => "API must be called from the owner UI thread",
                WindowsClipboardErrorCode.Canceled => "Request was canceled",
                WindowsClipboardErrorCode.NotSupported => "Operation is not supported on Windows",
                WindowsClipboardErrorCode.NotForeground => "App is not in the foreground",
                WindowsClipboardErrorCode.WrongApartment => "Calling thread is not an initialized STA",
                WindowsClipboardErrorCode.Unknown => "Unexpected failure (raw code logged)",

                WindowsClipboardErrorCode.PlatformUnavailable =>
                    $"{operation} is available only on Windows player builds.",
                WindowsClipboardErrorCode.BridgeUnavailable =>
                    $"{operation} could not be started; the native clipboard bridge is unavailable.",
                WindowsClipboardErrorCode.MainThreadRequired =>
                    $"{operation} must be called from the Unity main thread.",
                WindowsClipboardErrorCode.ManagerDestroyed =>
                    $"{operation} was rejected because the manager has been destroyed.",
                WindowsClipboardErrorCode.NotInitializedByHost =>
                    $"{operation} requires Initialize to succeed first.",
                WindowsClipboardErrorCode.InvalidArgument =>
                    $"{operation} received an invalid argument: {detail ?? "unspecified"}",
                WindowsClipboardErrorCode.ShutdownTimeout =>
                    "Shutdown did not complete within the retry budget.",
                WindowsClipboardErrorCode.ResultParseFailed =>
                    $"{operation} returned a payload that could not be parsed.",
                WindowsClipboardErrorCode.RequestRejected =>
                    $"{operation} was rejected without a native error code.",
                WindowsClipboardErrorCode.ApartmentUnavailable =>
                    $"{operation} requires an STA thread; the Unity main thread could not be initialized as STA.",
                WindowsClipboardErrorCode.OperationBusy =>
                    $"{operation} is already in progress.",
                WindowsClipboardErrorCode.ShuttingDown =>
                    $"{operation} was rejected because the manager is shutting down.",

                _ => $"Unknown error ({(int)code})"
            };
        }

        /// <summary>
        /// Whether the code came from the native layer rather than from this C# layer.
        /// </summary>
        /// <param name="code">The code to classify.</param>
        /// <returns>True for the native range 0-19.</returns>
        public static bool IsNative(this WindowsClipboardErrorCode code) =>
            (int)code >= 0 && (int)code <= 19;
    }
}
#endif
