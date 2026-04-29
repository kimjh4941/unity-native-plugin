#nullable enable

#if UNITY_ANDROID
namespace JonghyunKim.NativeToolkit.Runtime.Notification
{
    /// <summary>
    /// Represents the completion result of a native notification operation.
    /// </summary>
    public readonly struct NotificationResult
    {
        /// <summary>
        /// Gets the native operation name that produced this result.
        /// </summary>
        public string Operation { get; }

        /// <summary>
        /// Gets a value indicating whether the operation succeeded.
        /// </summary>
        public bool IsSuccess { get; }

        /// <summary>
        /// Gets the error message when the operation failed, otherwise <c>null</c>.
        /// </summary>
        public string? ErrorMessage { get; }

        /// <summary>
        /// Creates a successful operation result.
        /// </summary>
        /// <param name="operation">The native operation name.</param>
        /// <returns>A successful <see cref="NotificationResult"/>.</returns>
        public static NotificationResult Success(string operation) =>
            new(operation, true, null);

        /// <summary>
        /// Creates a failed operation result.
        /// </summary>
        /// <param name="operation">The native operation name.</param>
        /// <param name="error">The error message returned by the native layer.</param>
        /// <returns>A failed <see cref="NotificationResult"/>.</returns>
        public static NotificationResult Failure(string operation, string error) =>
            new(operation, false, error);

        private NotificationResult(string operation, bool isSuccess, string? errorMessage)
        {
            Operation = operation;
            IsSuccess = isSuccess;
            ErrorMessage = errorMessage;
        }
    }
}
#endif
