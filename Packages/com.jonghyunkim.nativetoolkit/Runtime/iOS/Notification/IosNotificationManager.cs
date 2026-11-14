#nullable enable

#if UNITY_IOS
namespace JonghyunKim.NativeToolkit.Runtime.iOS.Notification
{
    using System;
    using JonghyunKim.NativeToolkit.Runtime.Common;
    using UnityEngine;

    /// <summary>
    /// Singleton manager for iOS native notification operations.
    /// All callbacks are dispatched to the Unity main thread via <see cref="UnityMainThreadDispatcher"/>.
    /// <para>
    /// <b>Unavailable for now.</b> native-toolkit 2.0.0 removed the 1.x bridge this Manager called, and
    /// the Manager does not call the 2.0.0 C ABI yet (artifact/topics/ios-c-abi-2, stage 3-S). Until it
    /// moves (stage 3), every API that returns a result answers once, on the next Update, with a fixed
    /// value: the operations fail with <see cref="UnavailableMessage"/>, <see cref="HasPermission"/>
    /// gives false, <see cref="GetAuthorizationStatus"/> gives Unknown and the two lists give "[]". The
    /// per-call callback runs first, then <see cref="NotificationOperationCompleted"/>. Cancelling,
    /// removing, <see cref="RemoveCategory"/>, <see cref="OpenNotificationSettings"/> and
    /// <see cref="Initialize"/> do nothing; the notification center delegate is installed at launch by
    /// the package's NtkNotificationInstaller.mm.
    /// </para>
    /// </summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "JonghyunKim.NativeToolkit.Runtime.Notification")]
    public class IosNotificationManager : MonoBehaviour
    {
        private const string LogTag = "IosNotificationManager";

        /// <summary>Native operation name for requesting notification permission.</summary>
        public const string OperationRequestPermission = "requestPermission";

        /// <summary>Native operation name for showing a notification.</summary>
        public const string OperationShowNotification = "showNotification";

        /// <summary>Native operation name for scheduling a notification.</summary>
        public const string OperationScheduleNotification = "scheduleNotification";

        /// <summary>Native operation name for updating a notification.</summary>
        public const string OperationUpdateNotification = "updateNotification";

        /// <summary>Native operation name for setting the badge count.</summary>
        public const string OperationSetBadgeCount = "setBadgeCount";

        /// <summary>Native operation name for registering a notification category.</summary>
        public const string OperationRegisterCategory = "registerCategory";

        private static IosNotificationManager? _instance;

        /// <summary>
        /// Singleton instance. Creates and persists a new GameObject if none exists.
        /// </summary>
        public static IosNotificationManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    Debug.Log($"[{LogTag}] Creating new instance of IosNotificationManager");
                    var go = new GameObject("IosNotificationManager");
                    _instance = go.AddComponent<IosNotificationManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        // ── Events ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Raised when any async notification operation (show, schedule, update, permission, badge, category) completes.
        /// </summary>
        public event Action<IosNotificationResult>? NotificationOperationCompleted;

        // Not raised until Notification moves to the C ABI (stage 3); kept so that callers still compile.
#pragma warning disable CS0067
        /// <summary>
        /// Raised when the user taps a notification action button.
        /// </summary>
        public event Action<IosNotificationActionResult>? NotificationActionReceived;

        /// <summary>
        /// Raised when the user submits a text input notification action.
        /// </summary>
        public event Action<IosNotificationTextInputActionResult>? NotificationTextInputActionReceived;
#pragma warning restore CS0067

        /// <summary>The error the operations report until Notification moves to the C ABI (stage 3).</summary>
        internal const string UnavailableMessage = "Notification is not available yet on this build.";

        // ── Lifecycle ────────────────────────────────────────────────────────────

        private void Awake()
        {
            Debug.Log($"[{LogTag}][{nameof(Awake)}]");
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _ = UnityMainThreadDispatcher.Instance;
            Initialize();
        }

        private void OnDestroy()
        {
            Debug.Log($"[{LogTag}][{nameof(OnDestroy)}]");
            if (_instance != this) return;
            _instance = null;
        }

        // ── Public API ───────────────────────────────────────────────────────────

        /// <summary>
        /// Called automatically in Awake. Does nothing for now: the notification center delegate is
        /// installed at launch by NtkNotificationInstaller.mm, and action callbacks arrive once
        /// Notification moves to the C ABI (stage 3).
        /// </summary>
        public void Initialize()
        {
            Debug.Log($"[{LogTag}][{nameof(Initialize)}]");
        }

        /// <summary>
        /// Requests notification authorization from the user.
        /// </summary>
        /// <param name="onResult">Optional callback receiving the result.</param>
        public void RequestPermission(Action<IosNotificationResult>? onResult = null)
        {
            Debug.Log($"[{LogTag}][{nameof(RequestPermission)}] onResult: {onResult}");
            DispatchFailure(OperationRequestPermission, onResult);
        }

        /// <summary>
        /// Checks whether the app currently has notification permission.
        /// </summary>
        /// <param name="onResult">Callback receiving the boolean result.</param>
        public void HasPermission(Action<bool> onResult)
        {
            Debug.Log($"[{LogTag}][{nameof(HasPermission)}] onResult: {onResult}");
            DispatchValue(nameof(HasPermission), onResult, false);
        }

        /// <summary>
        /// Returns the current notification authorization status.
        /// </summary>
        /// <param name="onResult">Callback receiving the authorization status.</param>
        public void GetAuthorizationStatus(Action<IosNotificationAuthorizationStatus> onResult)
        {
            Debug.Log($"[{LogTag}][{nameof(GetAuthorizationStatus)}] onResult: {onResult}");
            DispatchValue(nameof(GetAuthorizationStatus), onResult, IosNotificationAuthorizationStatus.Unknown);
        }

        /// <summary>
        /// Opens the app's notification settings page in the system Settings app.
        /// </summary>
        public void OpenNotificationSettings()
        {
            Debug.Log($"[{LogTag}][{nameof(OpenNotificationSettings)}]");
        }

        /// <summary>
        /// Immediately shows a notification.
        /// </summary>
        /// <param name="contentJson">JSON string for notification content (required).</param>
        /// <param name="triggerJson">JSON string for notification trigger (null for immediate delivery).</param>
        /// <param name="onResult">Optional callback receiving the result.</param>
        public void ShowNotification(string contentJson, string? triggerJson = null, Action<IosNotificationResult>? onResult = null)
        {
            Debug.Log($"[{LogTag}][{nameof(ShowNotification)}] contentJson: {contentJson}, triggerJson: {triggerJson}, onResult: {onResult}");
            DispatchFailure(OperationShowNotification, onResult);
        }

        /// <summary>
        /// Schedules a notification for future delivery.
        /// </summary>
        /// <param name="contentJson">JSON string for notification content (required).</param>
        /// <param name="triggerJson">JSON string for notification trigger (required).</param>
        /// <param name="identifier">Unique identifier for the notification request.</param>
        /// <param name="onResult">Optional callback receiving the result.</param>
        public void ScheduleNotification(string contentJson, string triggerJson, string identifier, Action<IosNotificationResult>? onResult = null)
        {
            Debug.Log($"[{LogTag}][{nameof(ScheduleNotification)}] contentJson: {contentJson}, triggerJson: {triggerJson}, identifier: {identifier}, onResult: {onResult}");
            DispatchFailure(OperationScheduleNotification, onResult);
        }

        /// <summary>
        /// Updates an existing pending notification.
        /// </summary>
        /// <param name="identifier">Identifier of the pending notification to update.</param>
        /// <param name="contentJson">JSON string with updated notification content.</param>
        /// <param name="triggerJson">JSON string with updated trigger, or null to keep the original trigger.</param>
        /// <param name="onResult">Optional callback receiving the result.</param>
        public void UpdateNotification(string identifier, string contentJson, string? triggerJson = null, Action<IosNotificationResult>? onResult = null)
        {
            Debug.Log($"[{LogTag}][{nameof(UpdateNotification)}] identifier: {identifier}, contentJson: {contentJson}, triggerJson: {triggerJson}, onResult: {onResult}");
            DispatchFailure(OperationUpdateNotification, onResult);
        }

        /// <summary>
        /// Cancels a specific pending notification.
        /// </summary>
        /// <param name="identifier">Identifier of the notification to cancel.</param>
        public void CancelNotification(string identifier)
        {
            Debug.Log($"[{LogTag}][{nameof(CancelNotification)}] identifier: {identifier}");
        }

        /// <summary>
        /// Cancels all pending notifications.
        /// </summary>
        public void CancelAllNotifications()
        {
            Debug.Log($"[{LogTag}][{nameof(CancelAllNotifications)}]");
        }

        /// <summary>
        /// Removes a specific delivered notification from Notification Center.
        /// </summary>
        /// <param name="identifier">Identifier of the delivered notification to remove.</param>
        public void RemoveDeliveredNotification(string identifier)
        {
            Debug.Log($"[{LogTag}][{nameof(RemoveDeliveredNotification)}] identifier: {identifier}");
        }

        /// <summary>
        /// Removes all delivered notifications from Notification Center.
        /// </summary>
        public void RemoveAllDeliveredNotifications()
        {
            Debug.Log($"[{LogTag}][{nameof(RemoveAllDeliveredNotifications)}]");
        }

        /// <summary>
        /// Cancels a specific scheduled notification.
        /// </summary>
        /// <param name="identifier">Identifier of the scheduled notification to cancel.</param>
        public void CancelScheduledNotification(string identifier)
        {
            Debug.Log($"[{LogTag}][{nameof(CancelScheduledNotification)}] identifier: {identifier}");
        }

        /// <summary>
        /// Cancels all scheduled notifications.
        /// </summary>
        public void CancelAllScheduledNotifications()
        {
            Debug.Log($"[{LogTag}][{nameof(CancelAllScheduledNotifications)}]");
        }

        /// <summary>
        /// Returns all pending notification requests as a JSON array string.
        /// </summary>
        /// <param name="onResult">Callback receiving the JSON array string.</param>
        public void GetScheduledNotifications(Action<string> onResult)
        {
            Debug.Log($"[{LogTag}][{nameof(GetScheduledNotifications)}] onResult: {onResult}");
            DispatchValue(nameof(GetScheduledNotifications), onResult, "[]");
        }

        /// <summary>
        /// Returns all delivered notifications as a JSON array string.
        /// </summary>
        /// <param name="onResult">Callback receiving the JSON array string.</param>
        public void GetDeliveredNotifications(Action<string> onResult)
        {
            Debug.Log($"[{LogTag}][{nameof(GetDeliveredNotifications)}] onResult: {onResult}");
            DispatchValue(nameof(GetDeliveredNotifications), onResult, "[]");
        }

        /// <summary>
        /// Sets the app icon badge count. Pass 0 to clear the badge.
        /// </summary>
        /// <param name="count">Badge count to display on the app icon. 0 clears the badge.</param>
        /// <param name="onResult">Optional callback receiving the result.</param>
        public void SetBadgeCount(int count, Action<IosNotificationResult>? onResult = null)
        {
            Debug.Log($"[{LogTag}][{nameof(SetBadgeCount)}] count: {count}, onResult: {onResult}");
            DispatchFailure(OperationSetBadgeCount, onResult);
        }

        /// <summary>
        /// Registers a notification category for use with action buttons.
        /// </summary>
        /// <param name="categoryJson">JSON string describing the category and its actions.</param>
        /// <param name="onResult">Optional callback receiving the result.</param>
        public void RegisterCategory(string categoryJson, Action<IosNotificationResult>? onResult = null)
        {
            Debug.Log($"[{LogTag}][{nameof(RegisterCategory)}] categoryJson: {categoryJson}, onResult: {onResult}");
            DispatchFailure(OperationRegisterCategory, onResult);
        }

        /// <summary>
        /// Removes a registered notification category.
        /// </summary>
        /// <param name="identifier">Identifier of the category to remove.</param>
        public void RemoveCategory(string identifier)
        {
            Debug.Log($"[{LogTag}][{nameof(RemoveCategory)}] identifier: {identifier}");
        }

        // ── Temporary result paths (until stage 3) ───────────────────────────────

        /// <summary>
        /// Answers one call with <see cref="UnavailableMessage"/> on the next Update: this call's
        /// callback first, then <see cref="NotificationOperationCompleted"/>, as 1.x did. The callback
        /// is captured per call, so two calls in a row each get their own answer.
        /// </summary>
        private static void DispatchFailure(string operation, Action<IosNotificationResult>? onResult)
        {
            var result = IosNotificationResult.Failure(operation, UnavailableMessage);
            Debug.LogWarning($"[{LogTag}] {operation}: {UnavailableMessage}");
            UnityMainThreadDispatcher.Instance.Enqueue(() =>
            {
                try
                {
                    onResult?.Invoke(result);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[{LogTag}][{operation}] callback: {ex.Message}");
                }

                try
                {
                    _instance?.NotificationOperationCompleted?.Invoke(result);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[{LogTag}][{operation}] {nameof(NotificationOperationCompleted)}: {ex.Message}");
                }
            });
        }

        /// <summary>Answers one call of an API without an event with a fixed value on the next Update.</summary>
        private static void DispatchValue<T>(string operation, Action<T>? onResult, T value)
        {
            UnityMainThreadDispatcher.Instance.Enqueue(() =>
            {
                try
                {
                    onResult?.Invoke(value);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[{LogTag}][{operation}] callback: {ex.Message}");
                }
            });
        }
    }
}
#endif
