#nullable enable

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
namespace JonghyunKim.NativeToolkit.Runtime.Notification
{
    /// <summary>
    /// Represents the result of a Windows notification operation.
    /// </summary>
    public readonly struct WindowsNotificationResult
    {
        /// <summary>Gets the name of the operation that produced this result.</summary>
        public string Operation { get; }

        /// <summary>Gets whether the operation succeeded.</summary>
        public bool IsSuccess { get; }

        /// <summary>Gets the native error code (NOTIFICATION_ERROR_* constant). Zero on success.</summary>
        public int ErrorCode { get; }

        /// <summary>Gets the human-readable error message. Null on success.</summary>
        public string? ErrorMessage { get; }

        /// <summary>Creates a successful result.</summary>
        /// <param name="operation">The name of the operation.</param>
        public static WindowsNotificationResult Success(string operation) =>
            new(operation, true, 0, null);

        /// <summary>Creates a failure result.</summary>
        /// <param name="operation">The name of the operation.</param>
        /// <param name="errorCode">The native NOTIFICATION_ERROR_* code.</param>
        public static WindowsNotificationResult Failure(string operation, int errorCode) =>
            new(operation, false, errorCode, ErrorCodeToMessage(errorCode));

        private WindowsNotificationResult(string operation, bool isSuccess, int errorCode, string? errorMessage)
        {
            Operation    = operation;
            IsSuccess    = isSuccess;
            ErrorCode    = errorCode;
            ErrorMessage = errorMessage;
        }

        private static string ErrorCodeToMessage(int code) => code switch
        {
            1 => "Not initialized",
            2 => "Notifications are disabled",
            3 => "Invalid JSON payload",
            4 => "Progress notification not found",
            5 => "WinRT HRESULT failure",
            6 => "Badge operation failed",
            7 => "Invalid parameter",
            8 => "This operation is not supported for the current app type",
            _ => $"Unknown error ({code})"
        };
    }
}
#endif
