#nullable enable

#if UNITY_STANDALONE_OSX || UNITY_EDITOR
namespace JonghyunKim.NativeToolkit.Runtime.Notification
{
    /// <summary>
    /// Represents the result of a macOS notification query using JsonCallback (json, errorCode, errorMessage).
    /// Used for GetAuthorizationStatus, GetScheduled, and GetDelivered operations.
    /// </summary>
    public readonly struct MacNotificationJsonResult
    {
        /// <summary>Gets the name of the operation that produced this result.</summary>
        public string Operation { get; }

        /// <summary>Gets the JSON string returned by the native layer. Null on failure.</summary>
        public string? Json { get; }

        /// <summary>Gets whether the operation succeeded.</summary>
        public bool IsSuccess { get; }

        /// <summary>Gets the native error code. Zero on success.</summary>
        public int ErrorCode { get; }

        /// <summary>Gets the error message. Null on success.</summary>
        public string? ErrorMessage { get; }

        /// <summary>Creates a successful result with JSON payload.</summary>
        public static MacNotificationJsonResult Success(string operation, string json) =>
            new(operation, json, true, 0, null);

        /// <summary>Creates a failure result.</summary>
        public static MacNotificationJsonResult Failure(string operation, int errorCode, string? errorMessage) =>
            new(operation, null, false, errorCode, errorMessage);

        private MacNotificationJsonResult(string operation, string? json, bool isSuccess, int errorCode, string? errorMessage)
        {
            Operation = operation;
            Json = json;
            IsSuccess = isSuccess;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
        }
    }
}
#endif
