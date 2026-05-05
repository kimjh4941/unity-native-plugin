#nullable enable

#if UNITY_IOS
namespace JonghyunKim.NativeToolkit.Runtime.Notification
{
    using System;
    using System.Runtime.InteropServices;
    using AOT;
    using JonghyunKim.NativeToolkit.Runtime.Common;
    using UnityEngine;

    /// <summary>
    /// Singleton manager for iOS native notification operations.
    /// Wraps UnityIosNotificationManager (Swift) via P/Invoke through the C ABI bridge.
    /// All callbacks are dispatched to the Unity main thread via <see cref="UnityMainThreadDispatcher"/>.
    /// </summary>
    public class IosNotificationManager : MonoBehaviour
    {
        private const string LogTag = "IosNotificationManager";

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

        /// <summary>
        /// Raised when the user taps a notification action button.
        /// </summary>
        public event Action<IosNotificationActionResult>? NotificationActionReceived;

        /// <summary>
        /// Raised when the user submits a text input notification action.
        /// </summary>
        public event Action<IosNotificationTextInputActionResult>? NotificationTextInputActionReceived;

        // ── Delegate types (IL2CPP / AOT safe) ──────────────────────────────────

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void NotificationCallback(bool isSuccess, string? errorMessage);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void NotificationJsonCallback(string json);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void NotificationStatusCallback(string status);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void NotificationBoolCallback(bool value);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void NotificationActionCallback(string notificationId, string actionId, string userInfoJson);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void NotificationTextInputActionCallback(string notificationId, string actionId, string userText, string userInfoJson);

        // ── DllImport declarations ───────────────────────────────────────────────

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void notificationSetup();

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void showNotification(string contentJson, string? triggerJson, NotificationCallback callback);

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void scheduleNotification(string contentJson, string triggerJson, string identifier, NotificationCallback callback);

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void updateNotification(string identifier, string contentJson, string? triggerJson, NotificationCallback callback);

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void cancelNotification(string identifier);

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void cancelAllNotifications();

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void removeDeliveredNotification(string identifier);

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void removeAllDeliveredNotifications();

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void cancelScheduledNotification(string identifier);

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void cancelAllScheduledNotifications();

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void getScheduledNotifications(NotificationJsonCallback callback);

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void getDeliveredNotifications(NotificationJsonCallback callback);

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void requestNotificationPermission(NotificationCallback callback);

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void getNotificationAuthorizationStatus(NotificationStatusCallback callback);

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void openNotificationSettings();

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void setNotificationBadgeCount(int count, NotificationCallback callback);

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void registerNotificationCategory(string categoryJson, NotificationCallback callback);

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void removeNotificationCategory(string identifier);

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void hasNotificationPermission(NotificationBoolCallback callback);

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void setNotificationActionReceivedCallback(NotificationActionCallback? callback);

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void setNotificationTextInputActionReceivedCallback(NotificationTextInputActionCallback? callback);

        // ── Static delegate storage (GC prevention) ──────────────────────────────
        // Persistent delegates: stored permanently to prevent GC collection of the native function pointer.
        private static readonly NotificationActionCallback s_persistentActionDelegate =
            OnActionReceived;
        private static readonly NotificationTextInputActionCallback s_persistentTextInputDelegate =
            OnTextInputActionReceived;

        // Per-operation static delegates: one stable function pointer per operation type.
        private static readonly NotificationCallback s_showNotificationDelegate =
            OnShowNotificationResult;
        private static readonly NotificationCallback s_scheduleNotificationDelegate =
            OnScheduleNotificationResult;
        private static readonly NotificationCallback s_updateNotificationDelegate =
            OnUpdateNotificationResult;
        private static readonly NotificationCallback s_requestPermissionDelegate =
            OnRequestPermissionResult;
        private static readonly NotificationCallback s_setBadgeCountDelegate =
            OnSetBadgeCountResult;
        private static readonly NotificationCallback s_registerCategoryDelegate =
            OnRegisterCategoryResult;
        private static readonly NotificationJsonCallback s_getScheduledDelegate =
            OnGetScheduledNotifications;
        private static readonly NotificationJsonCallback s_getDeliveredDelegate =
            OnGetDeliveredNotifications;
        private static readonly NotificationStatusCallback s_getAuthStatusDelegate =
            OnGetAuthorizationStatus;
        private static readonly NotificationBoolCallback s_hasPermissionDelegate =
            OnHasPermission;

        // Per-call user callbacks (stored between native call and result callback).
        // Note: last-registered callback wins when the same operation is called concurrently.
        private static Action<IosNotificationResult>? s_onShowNotification;
        private static Action<IosNotificationResult>? s_onScheduleNotification;
        private static Action<IosNotificationResult>? s_onUpdateNotification;
        private static Action<IosNotificationResult>? s_onRequestPermission;
        private static Action<IosNotificationResult>? s_onSetBadgeCount;
        private static Action<IosNotificationResult>? s_onRegisterCategory;
        private static Action<string>? s_onGetScheduledNotifications;
        private static Action<string>? s_onGetDeliveredNotifications;
        private static Action<IosNotificationAuthorizationStatus>? s_onGetAuthorizationStatus;
        private static Action<bool>? s_onHasPermission;

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

            if (Application.platform == RuntimePlatform.IPhonePlayer)
            {
                setNotificationActionReceivedCallback(null);
                setNotificationTextInputActionReceivedCallback(null);
            }

            _instance = null;
        }

        // ── Public API ───────────────────────────────────────────────────────────

        /// <summary>
        /// Registers IosNotificationManager as UNUserNotificationCenterDelegate and sets persistent callbacks.
        /// Called automatically in Awake. Call manually only if you need to reinitialize.
        /// </summary>
        public void Initialize()
        {
            Debug.Log($"[{LogTag}][{nameof(Initialize)}]");
            if (Application.platform != RuntimePlatform.IPhonePlayer)
            {
                Debug.Log($"[{LogTag}][{nameof(Initialize)}] Not running on iOS. Skipping native notification initialization.");
                return;
            }

            notificationSetup();
            setNotificationActionReceivedCallback(s_persistentActionDelegate);
            setNotificationTextInputActionReceivedCallback(s_persistentTextInputDelegate);
        }

        /// <summary>
        /// Requests notification authorization from the user.
        /// </summary>
        /// <param name="onResult">Optional callback receiving the result.</param>
        public void RequestPermission(Action<IosNotificationResult>? onResult = null)
        {
            Debug.Log($"[{LogTag}][{nameof(RequestPermission)}] onResult: {onResult}");
            s_onRequestPermission = onResult;
            if (Application.platform != RuntimePlatform.IPhonePlayer) return;
            requestNotificationPermission(s_requestPermissionDelegate);
        }

        /// <summary>
        /// Checks whether the app currently has notification permission.
        /// </summary>
        /// <param name="onResult">Callback receiving the boolean result.</param>
        public void HasPermission(Action<bool> onResult)
        {
            Debug.Log($"[{LogTag}][{nameof(HasPermission)}] onResult: {onResult}");
            s_onHasPermission = onResult;
            if (Application.platform != RuntimePlatform.IPhonePlayer) return;
            hasNotificationPermission(s_hasPermissionDelegate);
        }

        /// <summary>
        /// Returns the current notification authorization status.
        /// </summary>
        /// <param name="onResult">Callback receiving the authorization status.</param>
        public void GetAuthorizationStatus(Action<IosNotificationAuthorizationStatus> onResult)
        {
            Debug.Log($"[{LogTag}][{nameof(GetAuthorizationStatus)}] onResult: {onResult}");
            s_onGetAuthorizationStatus = onResult;
            if (Application.platform != RuntimePlatform.IPhonePlayer) return;
            getNotificationAuthorizationStatus(s_getAuthStatusDelegate);
        }

        /// <summary>
        /// Opens the app's notification settings page in the system Settings app.
        /// </summary>
        public void OpenNotificationSettings()
        {
            Debug.Log($"[{LogTag}][{nameof(OpenNotificationSettings)}]");
            if (Application.platform != RuntimePlatform.IPhonePlayer) return;
            openNotificationSettings();
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
            s_onShowNotification = onResult;
            if (Application.platform != RuntimePlatform.IPhonePlayer) return;
            showNotification(contentJson, triggerJson, s_showNotificationDelegate);
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
            s_onScheduleNotification = onResult;
            if (Application.platform != RuntimePlatform.IPhonePlayer) return;
            scheduleNotification(contentJson, triggerJson, identifier, s_scheduleNotificationDelegate);
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
            s_onUpdateNotification = onResult;
            if (Application.platform != RuntimePlatform.IPhonePlayer) return;
            updateNotification(identifier, contentJson, triggerJson, s_updateNotificationDelegate);
        }

        /// <summary>
        /// Cancels a specific pending notification.
        /// </summary>
        /// <param name="identifier">Identifier of the notification to cancel.</param>
        public void CancelNotification(string identifier)
        {
            Debug.Log($"[{LogTag}][{nameof(CancelNotification)}] identifier: {identifier}");
            if (Application.platform != RuntimePlatform.IPhonePlayer) return;
            cancelNotification(identifier);
        }

        /// <summary>
        /// Cancels all pending notifications.
        /// </summary>
        public void CancelAllNotifications()
        {
            Debug.Log($"[{LogTag}][{nameof(CancelAllNotifications)}]");
            if (Application.platform != RuntimePlatform.IPhonePlayer) return;
            cancelAllNotifications();
        }

        /// <summary>
        /// Removes a specific delivered notification from Notification Center.
        /// </summary>
        /// <param name="identifier">Identifier of the delivered notification to remove.</param>
        public void RemoveDeliveredNotification(string identifier)
        {
            Debug.Log($"[{LogTag}][{nameof(RemoveDeliveredNotification)}] identifier: {identifier}");
            if (Application.platform != RuntimePlatform.IPhonePlayer) return;
            removeDeliveredNotification(identifier);
        }

        /// <summary>
        /// Removes all delivered notifications from Notification Center.
        /// </summary>
        public void RemoveAllDeliveredNotifications()
        {
            Debug.Log($"[{LogTag}][{nameof(RemoveAllDeliveredNotifications)}]");
            if (Application.platform != RuntimePlatform.IPhonePlayer) return;
            removeAllDeliveredNotifications();
        }

        /// <summary>
        /// Cancels a specific scheduled notification.
        /// </summary>
        /// <param name="identifier">Identifier of the scheduled notification to cancel.</param>
        public void CancelScheduledNotification(string identifier)
        {
            Debug.Log($"[{LogTag}][{nameof(CancelScheduledNotification)}] identifier: {identifier}");
            if (Application.platform != RuntimePlatform.IPhonePlayer) return;
            cancelScheduledNotification(identifier);
        }

        /// <summary>
        /// Cancels all scheduled notifications.
        /// </summary>
        public void CancelAllScheduledNotifications()
        {
            Debug.Log($"[{LogTag}][{nameof(CancelAllScheduledNotifications)}]");
            if (Application.platform != RuntimePlatform.IPhonePlayer) return;
            cancelAllScheduledNotifications();
        }

        /// <summary>
        /// Returns all pending notification requests as a JSON array string.
        /// </summary>
        /// <param name="onResult">Callback receiving the JSON array string.</param>
        public void GetScheduledNotifications(Action<string> onResult)
        {
            Debug.Log($"[{LogTag}][{nameof(GetScheduledNotifications)}] onResult: {onResult}");
            s_onGetScheduledNotifications = onResult;
            if (Application.platform != RuntimePlatform.IPhonePlayer) return;
            getScheduledNotifications(s_getScheduledDelegate);
        }

        /// <summary>
        /// Returns all delivered notifications as a JSON array string.
        /// </summary>
        /// <param name="onResult">Callback receiving the JSON array string.</param>
        public void GetDeliveredNotifications(Action<string> onResult)
        {
            Debug.Log($"[{LogTag}][{nameof(GetDeliveredNotifications)}] onResult: {onResult}");
            s_onGetDeliveredNotifications = onResult;
            if (Application.platform != RuntimePlatform.IPhonePlayer) return;
            getDeliveredNotifications(s_getDeliveredDelegate);
        }

        /// <summary>
        /// Sets the app icon badge count. Pass 0 to clear the badge.
        /// </summary>
        /// <param name="count">Badge count to display on the app icon. 0 clears the badge.</param>
        /// <param name="onResult">Optional callback receiving the result.</param>
        public void SetBadgeCount(int count, Action<IosNotificationResult>? onResult = null)
        {
            Debug.Log($"[{LogTag}][{nameof(SetBadgeCount)}] count: {count}, onResult: {onResult}");
            s_onSetBadgeCount = onResult;
            if (Application.platform != RuntimePlatform.IPhonePlayer) return;
            setNotificationBadgeCount(count, s_setBadgeCountDelegate);
        }

        /// <summary>
        /// Registers a notification category for use with action buttons.
        /// </summary>
        /// <param name="categoryJson">JSON string describing the category and its actions.</param>
        /// <param name="onResult">Optional callback receiving the result.</param>
        public void RegisterCategory(string categoryJson, Action<IosNotificationResult>? onResult = null)
        {
            Debug.Log($"[{LogTag}][{nameof(RegisterCategory)}] categoryJson: {categoryJson}, onResult: {onResult}");
            s_onRegisterCategory = onResult;
            if (Application.platform != RuntimePlatform.IPhonePlayer) return;
            registerNotificationCategory(categoryJson, s_registerCategoryDelegate);
        }

        /// <summary>
        /// Removes a registered notification category.
        /// </summary>
        /// <param name="identifier">Identifier of the category to remove.</param>
        public void RemoveCategory(string identifier)
        {
            Debug.Log($"[{LogTag}][{nameof(RemoveCategory)}] identifier: {identifier}");
            if (Application.platform != RuntimePlatform.IPhonePlayer) return;
            removeNotificationCategory(identifier);
        }

        // ── Static AOT callbacks ──────────────────────────────────────────────────

        [MonoPInvokeCallback(typeof(NotificationCallback))]
        private static void OnShowNotificationResult(bool isSuccess, string? errorMessage)
        {
            var result = isSuccess ? IosNotificationResult.Success() : IosNotificationResult.Failure(errorMessage);
            var cb = s_onShowNotification;
            s_onShowNotification = null;
            UnityMainThreadDispatcher.Instance.Enqueue(() =>
            {
                try
                {
                    cb?.Invoke(result);
                    _instance?.NotificationOperationCompleted?.Invoke(result);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[{LogTag}][{nameof(OnShowNotificationResult)}] {ex.Message}");
                }
            });
        }

        [MonoPInvokeCallback(typeof(NotificationCallback))]
        private static void OnScheduleNotificationResult(bool isSuccess, string? errorMessage)
        {
            var result = isSuccess ? IosNotificationResult.Success() : IosNotificationResult.Failure(errorMessage);
            var cb = s_onScheduleNotification;
            s_onScheduleNotification = null;
            UnityMainThreadDispatcher.Instance.Enqueue(() =>
            {
                try
                {
                    cb?.Invoke(result);
                    _instance?.NotificationOperationCompleted?.Invoke(result);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[{LogTag}][{nameof(OnScheduleNotificationResult)}] {ex.Message}");
                }
            });
        }

        [MonoPInvokeCallback(typeof(NotificationCallback))]
        private static void OnUpdateNotificationResult(bool isSuccess, string? errorMessage)
        {
            var result = isSuccess ? IosNotificationResult.Success() : IosNotificationResult.Failure(errorMessage);
            var cb = s_onUpdateNotification;
            s_onUpdateNotification = null;
            UnityMainThreadDispatcher.Instance.Enqueue(() =>
            {
                try
                {
                    cb?.Invoke(result);
                    _instance?.NotificationOperationCompleted?.Invoke(result);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[{LogTag}][{nameof(OnUpdateNotificationResult)}] {ex.Message}");
                }
            });
        }

        [MonoPInvokeCallback(typeof(NotificationCallback))]
        private static void OnRequestPermissionResult(bool isSuccess, string? errorMessage)
        {
            var result = isSuccess ? IosNotificationResult.Success() : IosNotificationResult.Failure(errorMessage);
            var cb = s_onRequestPermission;
            s_onRequestPermission = null;
            UnityMainThreadDispatcher.Instance.Enqueue(() =>
            {
                try
                {
                    cb?.Invoke(result);
                    _instance?.NotificationOperationCompleted?.Invoke(result);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[{LogTag}][{nameof(OnRequestPermissionResult)}] {ex.Message}");
                }
            });
        }

        [MonoPInvokeCallback(typeof(NotificationCallback))]
        private static void OnSetBadgeCountResult(bool isSuccess, string? errorMessage)
        {
            var result = isSuccess ? IosNotificationResult.Success() : IosNotificationResult.Failure(errorMessage);
            var cb = s_onSetBadgeCount;
            s_onSetBadgeCount = null;
            UnityMainThreadDispatcher.Instance.Enqueue(() =>
            {
                try
                {
                    cb?.Invoke(result);
                    _instance?.NotificationOperationCompleted?.Invoke(result);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[{LogTag}][{nameof(OnSetBadgeCountResult)}] {ex.Message}");
                }
            });
        }

        [MonoPInvokeCallback(typeof(NotificationCallback))]
        private static void OnRegisterCategoryResult(bool isSuccess, string? errorMessage)
        {
            var result = isSuccess ? IosNotificationResult.Success() : IosNotificationResult.Failure(errorMessage);
            var cb = s_onRegisterCategory;
            s_onRegisterCategory = null;
            UnityMainThreadDispatcher.Instance.Enqueue(() =>
            {
                try
                {
                    cb?.Invoke(result);
                    _instance?.NotificationOperationCompleted?.Invoke(result);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[{LogTag}][{nameof(OnRegisterCategoryResult)}] {ex.Message}");
                }
            });
        }

        [MonoPInvokeCallback(typeof(NotificationJsonCallback))]
        private static void OnGetScheduledNotifications(string json)
        {
            var cb = s_onGetScheduledNotifications;
            s_onGetScheduledNotifications = null;
            UnityMainThreadDispatcher.Instance.Enqueue(() =>
            {
                try
                {
                    cb?.Invoke(json);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[{LogTag}][{nameof(OnGetScheduledNotifications)}] {ex.Message}");
                }
            });
        }

        [MonoPInvokeCallback(typeof(NotificationJsonCallback))]
        private static void OnGetDeliveredNotifications(string json)
        {
            var cb = s_onGetDeliveredNotifications;
            s_onGetDeliveredNotifications = null;
            UnityMainThreadDispatcher.Instance.Enqueue(() =>
            {
                try
                {
                    cb?.Invoke(json);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[{LogTag}][{nameof(OnGetDeliveredNotifications)}] {ex.Message}");
                }
            });
        }

        [MonoPInvokeCallback(typeof(NotificationStatusCallback))]
        private static void OnGetAuthorizationStatus(string status)
        {
            var parsed = IosNotificationAuthorizationStatusParser.Parse(status);
            var cb = s_onGetAuthorizationStatus;
            s_onGetAuthorizationStatus = null;
            UnityMainThreadDispatcher.Instance.Enqueue(() =>
            {
                try
                {
                    cb?.Invoke(parsed);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[{LogTag}][{nameof(OnGetAuthorizationStatus)}] {ex.Message}");
                }
            });
        }

        [MonoPInvokeCallback(typeof(NotificationBoolCallback))]
        private static void OnHasPermission(bool value)
        {
            var cb = s_onHasPermission;
            s_onHasPermission = null;
            UnityMainThreadDispatcher.Instance.Enqueue(() =>
            {
                try
                {
                    cb?.Invoke(value);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[{LogTag}][{nameof(OnHasPermission)}] {ex.Message}");
                }
            });
        }

        [MonoPInvokeCallback(typeof(NotificationActionCallback))]
        private static void OnActionReceived(string notificationId, string actionId, string userInfoJson)
        {
            var result = new IosNotificationActionResult(notificationId, actionId, userInfoJson);
            UnityMainThreadDispatcher.Instance.Enqueue(() =>
            {
                try
                {
                    _instance?.NotificationActionReceived?.Invoke(result);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[{LogTag}][{nameof(OnActionReceived)}] {ex.Message}");
                }
            });
        }

        [MonoPInvokeCallback(typeof(NotificationTextInputActionCallback))]
        private static void OnTextInputActionReceived(string notificationId, string actionId, string userText, string userInfoJson)
        {
            var result = new IosNotificationTextInputActionResult(notificationId, actionId, userText, userInfoJson);
            UnityMainThreadDispatcher.Instance.Enqueue(() =>
            {
                try
                {
                    _instance?.NotificationTextInputActionReceived?.Invoke(result);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[{LogTag}][{nameof(OnTextInputActionReceived)}] {ex.Message}");
                }
            });
        }
    }
}
#endif
