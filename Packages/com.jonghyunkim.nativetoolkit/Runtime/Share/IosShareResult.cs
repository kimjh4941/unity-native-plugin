#nullable enable

#if UNITY_IOS || UNITY_EDITOR
namespace JonghyunKim.NativeToolkit.Runtime.Share
{
    /// <summary>
    /// Represents the completion result of an iOS native share sheet presentation.
    /// </summary>
    public readonly struct IosShareResult
    {
        /// <summary>
        /// Gets a value indicating whether the share sheet could be presented.
        /// </summary>
        public bool IsSuccess { get; }

        /// <summary>
        /// Gets a value indicating whether the user completed an activity. <c>false</c> means the
        /// user cancelled the share sheet, which is not treated as an error.
        /// </summary>
        public bool Completed { get; }

        /// <summary>
        /// Gets the raw identifier of the activity the user selected, or <c>null</c> when
        /// cancelled, unknown, or when the operation failed.
        /// </summary>
        public string? ActivityType { get; }

        /// <summary>
        /// Gets the error message when the operation failed, otherwise <c>null</c>.
        /// </summary>
        public string? ErrorMessage { get; }

        /// <summary>
        /// Creates a successful share result.
        /// </summary>
        /// <param name="completed">Whether the user completed an activity.</param>
        /// <param name="activityType">The selected activity's raw identifier, or <c>null</c>.</param>
        /// <returns>A successful <see cref="IosShareResult"/>.</returns>
        public static IosShareResult Success(bool completed, string? activityType) =>
            new(true, completed, activityType, null);

        /// <summary>
        /// Creates a failed share result.
        /// </summary>
        /// <param name="error">The error message describing the failure.</param>
        /// <returns>A failed <see cref="IosShareResult"/>.</returns>
        public static IosShareResult Failure(string? error) =>
            new(false, false, null, error);

        private IosShareResult(bool isSuccess, bool completed, string? activityType, string? errorMessage)
        {
            IsSuccess = isSuccess;
            Completed = completed;
            ActivityType = activityType;
            ErrorMessage = errorMessage;
        }
    }
}
#endif
