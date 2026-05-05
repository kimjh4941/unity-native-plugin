#nullable enable

#if UNITY_IOS
namespace JonghyunKim.NativeToolkit.Runtime.Notification
{
    /// <summary>
    /// Represents the completion result of an iOS native notification operation.
    /// </summary>
    public readonly struct IosNotificationResult
    {
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
        /// <returns>A successful <see cref="IosNotificationResult"/>.</returns>
        public static IosNotificationResult Success() =>
            new(true, null);

        /// <summary>
        /// Creates a failed operation result.
        /// </summary>
        /// <param name="error">The error message returned by the native layer.</param>
        /// <returns>A failed <see cref="IosNotificationResult"/>.</returns>
        public static IosNotificationResult Failure(string? error) =>
            new(false, error);

        private IosNotificationResult(bool isSuccess, string? errorMessage)
        {
            IsSuccess = isSuccess;
            ErrorMessage = errorMessage;
        }
    }
}
#endif
