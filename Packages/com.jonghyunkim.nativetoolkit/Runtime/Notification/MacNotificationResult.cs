#nullable enable

#if UNITY_STANDALONE_OSX || UNITY_EDITOR
namespace JonghyunKim.NativeToolkit.Runtime.Notification
{
    /// <summary>
    /// Represents the result of a macOS notification operation using SimpleCallback (isSuccess, errorCode, errorMessage).
    /// </summary>
    public readonly struct MacNotificationResult
    {
        /// <summary>Gets the name of the operation that produced this result.</summary>
        public string Operation { get; }

        /// <summary>Gets whether the operation succeeded.</summary>
        public bool IsSuccess { get; }

        /// <summary>Gets the native error code. Zero on success.</summary>
        public int ErrorCode { get; }

        /// <summary>Gets the error message. Null on success.</summary>
        public string? ErrorMessage { get; }

        /// <summary>Creates a successful result.</summary>
        public static MacNotificationResult Success(string operation) =>
            new(operation, true, 0, null);

        /// <summary>Creates a failure result.</summary>
        public static MacNotificationResult Failure(string operation, int errorCode, string? errorMessage) =>
            new(operation, false, errorCode, errorMessage);

        private MacNotificationResult(string operation, bool isSuccess, int errorCode, string? errorMessage)
        {
            Operation = operation;
            IsSuccess = isSuccess;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
        }
    }
}
#endif
