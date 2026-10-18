#nullable enable

using UnityEngine.Scripting.APIUpdating;

namespace JonghyunKim.NativeToolkit.Runtime.Android.Clipboard
{
    /// <summary>
    /// Represents the completion result of a copy, clear, or stopObserving clipboard operation.
    /// </summary>
    [MovedFrom(false, sourceNamespace: "JonghyunKim.NativeToolkit.Runtime.Clipboard", sourceClassName: "ClipboardOperationResult")]
    public readonly struct AndroidClipboardOperationResult
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
        /// <returns>A successful <see cref="AndroidClipboardOperationResult"/>.</returns>
        public static AndroidClipboardOperationResult Success(string operation) =>
            new(operation, true, null);

        /// <summary>
        /// Creates a failed operation result.
        /// </summary>
        /// <param name="operation">The native operation name.</param>
        /// <param name="error">The error message returned by the native layer.</param>
        /// <returns>A failed <see cref="AndroidClipboardOperationResult"/>.</returns>
        public static AndroidClipboardOperationResult Failure(string operation, string error) =>
            new(operation, false, error);

        private AndroidClipboardOperationResult(string operation, bool isSuccess, string? errorMessage)
        {
            Operation = operation;
            IsSuccess = isSuccess;
            ErrorMessage = errorMessage;
        }
    }
}
