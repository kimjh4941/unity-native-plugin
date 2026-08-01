#nullable enable

namespace JonghyunKim.NativeToolkit.Runtime.Clipboard
{
    /// <summary>
    /// Represents the completion result of a copy, clear, or stopObserving clipboard operation.
    /// </summary>
    public readonly struct ClipboardOperationResult
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
        /// Guaranteed to be null when <see cref="IsSuccess"/> is true.
        /// </summary>
        public string? ErrorMessage { get; }

        /// <summary>
        /// Creates a successful operation result.
        /// </summary>
        /// <param name="operation">The native operation name.</param>
        /// <returns>A successful <see cref="ClipboardOperationResult"/>.</returns>
        public static ClipboardOperationResult Success(string operation) =>
            new(operation, true, null);

        /// <summary>
        /// Creates a failed operation result.
        /// </summary>
        /// <param name="operation">The native operation name.</param>
        /// <param name="error">The error message returned by the native layer.</param>
        /// <returns>A failed <see cref="ClipboardOperationResult"/>.</returns>
        public static ClipboardOperationResult Failure(string operation, string error) =>
            new(operation, false, error);

        private ClipboardOperationResult(string operation, bool isSuccess, string? errorMessage)
        {
            Operation = operation;
            IsSuccess = isSuccess;
            ErrorMessage = errorMessage;
        }
    }
}
