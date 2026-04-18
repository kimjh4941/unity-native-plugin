#nullable enable

#if UNITY_ANDROID
namespace JonghyunKim.NativeToolkit.Runtime.Notification
{
    public readonly struct NotificationResult
    {
        public string Operation { get; }
        public bool IsSuccess { get; }
        public string? ErrorMessage { get; }

        public static NotificationResult Success(string operation) =>
            new(operation, true, null);

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
