#nullable enable

#if UNITY_STANDALONE_OSX || UNITY_EDITOR
namespace JonghyunKim.NativeToolkit.Runtime.Share
{
    /// <summary>
    /// Represents the completion result of a macOS native share operation
    /// (<c>shareContent</c> / <c>shareViaService</c>).
    /// </summary>
    public readonly struct MacShareResult
    {
        /// <summary>Gets the native operation name that produced this result.</summary>
        public string Operation { get; }

        /// <summary>
        /// Gets a value indicating whether the share could be presented/performed.
        /// </summary>
        public bool IsSuccess { get; }

        /// <summary>
        /// Gets a value indicating whether the user completed a service. <c>false</c> means the
        /// user cancelled, which is not treated as an error.
        /// </summary>
        public bool Completed { get; }

        /// <summary>
        /// Gets the chosen service's display name (<c>NSSharingService.title</c>), or <c>null</c>
        /// when cancelled, unknown, or when the operation failed.
        /// </summary>
        /// <remarks>
        /// This is a display name, not the raw <c>NSSharingService.Name</c> identifier used as
        /// input to <c>MacShareManager.ShareViaService</c>. Do not reuse this value as the
        /// <c>serviceName</c> argument of a subsequent <c>ShareViaService</c> call; use
        /// <see cref="MacShareServiceNames"/> for known raw identifiers instead.
        /// </remarks>
        public string? ServiceName { get; }

        /// <summary>
        /// Gets the error message when the operation failed. Guaranteed to be non-null whenever
        /// <see cref="IsSuccess"/> is <c>false</c>.
        /// </summary>
        public string? ErrorMessage { get; }

        /// <summary>
        /// Creates a successful share result.
        /// </summary>
        /// <param name="operation">The native operation name.</param>
        /// <param name="completed">Whether the user completed a service.</param>
        /// <param name="serviceName">The chosen service's display name, or <c>null</c>.</param>
        /// <returns>A successful <see cref="MacShareResult"/>.</returns>
        public static MacShareResult Success(string operation, bool completed, string? serviceName) =>
            new(operation, true, completed, serviceName, null);

        /// <summary>
        /// Creates a failed share result. A <c>null</c> or blank <paramref name="error"/> is
        /// normalized to a default message so <see cref="ErrorMessage"/> is always non-null when
        /// <see cref="IsSuccess"/> is <c>false</c>.
        /// </summary>
        /// <param name="operation">The native operation name.</param>
        /// <param name="error">The error message describing the failure.</param>
        /// <returns>A failed <see cref="MacShareResult"/>.</returns>
        public static MacShareResult Failure(string operation, string? error) =>
            new(operation, false, false, null, string.IsNullOrWhiteSpace(error) ? "Unknown error." : error);

        private MacShareResult(string operation, bool isSuccess, bool completed, string? serviceName, string? errorMessage)
        {
            Operation = operation;
            IsSuccess = isSuccess;
            Completed = completed;
            ServiceName = serviceName;
            ErrorMessage = errorMessage;
        }
    }
}
#endif
