#nullable enable

#if UNITY_STANDALONE_OSX
namespace JonghyunKim.NativeToolkit.Runtime.Notification
{
    using System;
    using System.Runtime.InteropServices;
    using AOT;
    using JonghyunKim.NativeToolkit.Runtime.Common;
    using UnityEngine;

    /// <summary>
    /// Singleton manager for macOS native notification operations.
    /// Wraps UnityMacNotificationManager (Swift) via P/Invoke through the C ABI bridge.
    /// All callbacks are dispatched to the Unity main thread via <see cref="UnityMainThreadDispatcher"/>.
    /// </summary>
    public class MacNotificationManager : MonoBehaviour
    {
        private const string LogTag = "MacNotificationManager";

        public const string OperationRequestPermission    = "requestPermission";
        public const string OperationGetAuthorizationStatus = "getAuthorizationStatus";
        public const string OperationOpenSettings         = "openSettings";
        public const string OperationShow                 = "showNotification";
        public const string OperationSchedule             = "scheduleNotification";
        public const string OperationUpdate               = "updateNotification";
        public const string OperationGetScheduled         = "getScheduledNotifications";
        public const string OperationGetDelivered         = "getDeliveredNotifications";
        public const string OperationSetBadgeCount        = "setBadgeCount";
        public const string OperationRegisterCategory     = "registerCategory";
        public const string OperationRemoveCategory       = "removeCategory";

        private static MacNotificationManager? _instance;

        /// <summary>Singleton instance. Creates and persists a new GameObject if none exists.</summary>
        public static MacNotificationManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    Debug.Log($"[{LogTag}] Creating new instance of MacNotificationManager");
                    var go = new GameObject("MacNotificationManager");
                    _instance = go.AddComponent<MacNotificationManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        // ── Events ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Raised when any SimpleCallback operation (show, schedule, update, permission, openSettings,
        /// badge, registerCategory, removeCategory) completes.
        /// </summary>
        public event Action<MacNotificationResult>? NotificationOperationCompleted;

        /// <summary>Raised when the user taps a notification action button.</summary>
        public event Action<MacNotificationActionResult>? NotificationActionReceived;

        /// <summary>Raised when the user submits a text input notification action.</summary>
        public event Action<MacNotificationTextInputActionResult>? NotificationTextInputActionReceived;

        // ── Delegate types (IL2CPP / AOT safe) ──────────────────────────────────

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void NotificationSimpleCallback(bool isSuccess, int errorCode, string? errorMessage);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void NotificationJsonCallback(string? json, int errorCode, string? errorMessage);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void NotificationBoolCallback(bool value);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void NotificationActionCallback(string notificationId, string actionId, string? userInfoJson);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void NotificationTextInputActionCallback(string notificationId, string actionId, string userText, string? userInfoJson);

        // ── DllImport declarations (PascalCase to match macOS bridge symbols) ────

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void NotificationSetup();

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void NotificationRequestPermission(NotificationSimpleCallback callback);

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void NotificationGetAuthorizationStatus(NotificationJsonCallback callback);

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void NotificationOpenSettings(NotificationSimpleCallback callback);

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void NotificationShow(string contentJson, string? triggerJson, NotificationSimpleCallback callback);

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void NotificationUpdate(string identifier, string contentJson, string? triggerJson, NotificationSimpleCallback callback);

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void NotificationSchedule(string contentJson, string triggerJson, NotificationSimpleCallback callback);

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void NotificationCancelScheduled(string identifier);

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void NotificationCancelAllScheduled();

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void NotificationGetScheduled(NotificationJsonCallback callback);

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void NotificationGetDelivered(NotificationJsonCallback callback);

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void NotificationRemoveDelivered(string identifier);

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void NotificationRemoveAllDelivered();

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void NotificationRegisterCategory(string categoryJson, NotificationSimpleCallback callback);

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void NotificationRemoveCategory(string identifier, NotificationSimpleCallback callback);

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void NotificationSetActionReceivedCallback(NotificationActionCallback? callback);

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void NotificationSetTextInputActionReceivedCallback(NotificationTextInputActionCallback? callback);

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void NotificationSetBadgeCount(int count, NotificationSimpleCallback callback);

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void NotificationHasPermission(NotificationBoolCallback callback);

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void NotificationCancel(string identifier);

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void NotificationCancelAll();

        // ── Static delegate storage (GC prevention) ──────────────────────────────

        private static readonly NotificationActionCallback s_persistentActionDelegate =
            OnActionReceived;
        private static readonly NotificationTextInputActionCallback s_persistentTextInputDelegate =
            OnTextInputActionReceived;

        private static readonly NotificationSimpleCallback s_requestPermissionDelegate =
            OnRequestPermissionResult;
        private static readonly NotificationSimpleCallback s_openSettingsDelegate =
            OnOpenSettingsResult;
        private static readonly NotificationSimpleCallback s_showDelegate =
            OnShowResult;
        private static readonly NotificationSimpleCallback s_scheduleDelegate =
            OnScheduleResult;
        private static readonly NotificationSimpleCallback s_updateDelegate =
            OnUpdateResult;
        private static readonly NotificationSimpleCallback s_setBadgeCountDelegate =
            OnSetBadgeCountResult;
        private static readonly NotificationSimpleCallback s_registerCategoryDelegate =
            OnRegisterCategoryResult;
        private static readonly NotificationSimpleCallback s_removeCategoryDelegate =
            OnRemoveCategoryResult;

        private static readonly NotificationJsonCallback s_getAuthStatusDelegate =
            OnGetAuthorizationStatusResult;
        private static readonly NotificationJsonCallback s_getScheduledDelegate =
            OnGetScheduledResult;
        private static readonly NotificationJsonCallback s_getDeliveredDelegate =
            OnGetDeliveredResult;

        private static readonly NotificationBoolCallback s_hasPermissionDelegate =
            OnHasPermissionResult;

        // Per-call user callbacks
        private static Action<MacNotificationResult>? s_onRequestPermission;
        private static Action<MacNotificationResult>? s_onOpenSettings;
        private static Action<MacNotificationResult>? s_onShow;
        private static Action<MacNotificationResult>? s_onSchedule;
        private static Action<MacNotificationResult>? s_onUpdate;
        private static Action<MacNotificationResult>? s_onSetBadgeCount;
        private static Action<MacNotificationResult>? s_onRegisterCategory;
        private static Action<MacNotificationResult>? s_onRemoveCategory;

        private static Action<MacNotificationJsonResult>? s_onGetAuthorizationStatus;
        private static Action<MacNotificationJsonResult>? s_onGetScheduled;
        private static Action<MacNotificationJsonResult>? s_onGetDelivered;

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
            _instance = null;
        }

        // ── Public API ───────────────────────────────────────────────────────────

        /// <summary>
        /// Registers MacNotificationManager as UNUserNotificationCenterDelegate and sets persistent callbacks.
        /// Called automatically in Awake.
        /// </summary>
        public void Initialize()
        {
            Debug.Log($"[{LogTag}][{nameof(Initialize)}]");
            if (Application.platform != RuntimePlatform.OSXPlayer)
            {
                Debug.Log($"[{LogTag}][{nameof(Initialize)}] Not running on macOS Standalone. Skipping native notification initialization.");
                return;
            }

            NotificationSetup();
            NotificationSetActionReceivedCallback(s_persistentActionDelegate);
            NotificationSetTextInputActionReceivedCallback(s_persistentTextInputDelegate);
        }

        /// <summary>Requests notification authorization from the user.</summary>
        public void RequestPermission(Action<MacNotificationResult>? onResult = null)
        {
            Debug.Log($"[{LogTag}][{nameof(RequestPermission)}] onResult: {onResult != null}");
            s_onRequestPermission = onResult;
            if (Application.platform != RuntimePlatform.OSXPlayer) return;
            NotificationRequestPermission(s_requestPermissionDelegate);
        }

        /// <summary>Checks whether the app currently has notification permission.</summary>
        public void HasPermission(Action<bool> onResult)
        {
            Debug.Log($"[{LogTag}][{nameof(HasPermission)}] onResult: {onResult != null}");
            s_onHasPermission = onResult;
            if (Application.platform != RuntimePlatform.OSXPlayer) return;
            NotificationHasPermission(s_hasPermissionDelegate);
        }

        /// <summary>Returns the current notification authorization status as a JSON result.</summary>
        public void GetAuthorizationStatus(Action<MacNotificationJsonResult> onResult)
        {
            Debug.Log($"[{LogTag}][{nameof(GetAuthorizationStatus)}] onResult: {onResult != null}");
            s_onGetAuthorizationStatus = onResult;
            if (Application.platform != RuntimePlatform.OSXPlayer) return;
            NotificationGetAuthorizationStatus(s_getAuthStatusDelegate);
        }

        /// <summary>Opens the app's notification settings page in System Preferences.</summary>
        public void OpenSettings(Action<MacNotificationResult>? onResult = null)
        {
            Debug.Log($"[{LogTag}][{nameof(OpenSettings)}] onResult: {onResult != null}");
            s_onOpenSettings = onResult;
            if (Application.platform != RuntimePlatform.OSXPlayer) return;
            NotificationOpenSettings(s_openSettingsDelegate);
        }

        /// <summary>Immediately shows a notification.</summary>
        public void ShowNotification(string contentJson, string? triggerJson = null, Action<MacNotificationResult>? onResult = null)
        {
            Debug.Log($"[{LogTag}][{nameof(ShowNotification)}] contentJson: {contentJson}, triggerJson: {triggerJson}, onResult: {onResult != null}");
            s_onShow = onResult;
            if (Application.platform != RuntimePlatform.OSXPlayer) return;
            NotificationShow(contentJson, triggerJson, s_showDelegate);
        }

        /// <summary>Schedules a notification for future delivery.</summary>
        public void ScheduleNotification(string contentJson, string triggerJson, Action<MacNotificationResult>? onResult = null)
        {
            Debug.Log($"[{LogTag}][{nameof(ScheduleNotification)}] contentJson: {contentJson}, triggerJson: {triggerJson}, onResult: {onResult != null}");
            s_onSchedule = onResult;
            if (Application.platform != RuntimePlatform.OSXPlayer) return;
            NotificationSchedule(contentJson, triggerJson, s_scheduleDelegate);
        }

        /// <summary>Updates an existing pending notification.</summary>
        public void UpdateNotification(string identifier, string contentJson, string? triggerJson = null, Action<MacNotificationResult>? onResult = null)
        {
            Debug.Log($"[{LogTag}][{nameof(UpdateNotification)}] identifier: {identifier}, contentJson: {contentJson}, triggerJson: {triggerJson}, onResult: {onResult != null}");
            s_onUpdate = onResult;
            if (Application.platform != RuntimePlatform.OSXPlayer) return;
            NotificationUpdate(identifier, contentJson, triggerJson, s_updateDelegate);
        }

        /// <summary>Cancels a specific pending notification (fire-and-forget).</summary>
        public void CancelNotification(string identifier)
        {
            Debug.Log($"[{LogTag}][{nameof(CancelNotification)}] identifier: {identifier}");
            if (Application.platform != RuntimePlatform.OSXPlayer) return;
            NotificationCancel(identifier);
        }

        /// <summary>Cancels all pending notifications (fire-and-forget).</summary>
        public void CancelAllNotifications()
        {
            Debug.Log($"[{LogTag}][{nameof(CancelAllNotifications)}]");
            if (Application.platform != RuntimePlatform.OSXPlayer) return;
            NotificationCancelAll();
        }

        /// <summary>Cancels a specific scheduled notification (fire-and-forget).</summary>
        public void CancelScheduledNotification(string identifier)
        {
            Debug.Log($"[{LogTag}][{nameof(CancelScheduledNotification)}] identifier: {identifier}");
            if (Application.platform != RuntimePlatform.OSXPlayer) return;
            NotificationCancelScheduled(identifier);
        }

        /// <summary>Cancels all scheduled notifications (fire-and-forget).</summary>
        public void CancelAllScheduledNotifications()
        {
            Debug.Log($"[{LogTag}][{nameof(CancelAllScheduledNotifications)}]");
            if (Application.platform != RuntimePlatform.OSXPlayer) return;
            NotificationCancelAllScheduled();
        }

        /// <summary>Returns all pending notification requests as a JSON result.</summary>
        public void GetScheduledNotifications(Action<MacNotificationJsonResult> onResult)
        {
            Debug.Log($"[{LogTag}][{nameof(GetScheduledNotifications)}] onResult: {onResult != null}");
            s_onGetScheduled = onResult;
            if (Application.platform != RuntimePlatform.OSXPlayer) return;
            NotificationGetScheduled(s_getScheduledDelegate);
        }

        /// <summary>Returns all delivered notifications as a JSON result.</summary>
        public void GetDeliveredNotifications(Action<MacNotificationJsonResult> onResult)
        {
            Debug.Log($"[{LogTag}][{nameof(GetDeliveredNotifications)}] onResult: {onResult != null}");
            s_onGetDelivered = onResult;
            if (Application.platform != RuntimePlatform.OSXPlayer) return;
            NotificationGetDelivered(s_getDeliveredDelegate);
        }

        /// <summary>Removes a specific delivered notification from Notification Center (fire-and-forget).</summary>
        public void RemoveDeliveredNotification(string identifier)
        {
            Debug.Log($"[{LogTag}][{nameof(RemoveDeliveredNotification)}] identifier: {identifier}");
            if (Application.platform != RuntimePlatform.OSXPlayer) return;
            NotificationRemoveDelivered(identifier);
        }

        /// <summary>Removes all delivered notifications from Notification Center (fire-and-forget).</summary>
        public void RemoveAllDeliveredNotifications()
        {
            Debug.Log($"[{LogTag}][{nameof(RemoveAllDeliveredNotifications)}]");
            if (Application.platform != RuntimePlatform.OSXPlayer) return;
            NotificationRemoveAllDelivered();
        }

        /// <summary>Registers a notification category for use with action buttons.</summary>
        public void RegisterCategory(string categoryJson, Action<MacNotificationResult>? onResult = null)
        {
            Debug.Log($"[{LogTag}][{nameof(RegisterCategory)}] categoryJson: {categoryJson}, onResult: {onResult != null}");
            s_onRegisterCategory = onResult;
            if (Application.platform != RuntimePlatform.OSXPlayer) return;
            NotificationRegisterCategory(categoryJson, s_registerCategoryDelegate);
        }

        /// <summary>Removes a registered notification category.</summary>
        public void RemoveCategory(string identifier, Action<MacNotificationResult>? onResult = null)
        {
            Debug.Log($"[{LogTag}][{nameof(RemoveCategory)}] identifier: {identifier}, onResult: {onResult != null}");
            s_onRemoveCategory = onResult;
            if (Application.platform != RuntimePlatform.OSXPlayer) return;
            NotificationRemoveCategory(identifier, s_removeCategoryDelegate);
        }

        /// <summary>Sets the app icon badge count. Pass 0 to clear the badge.</summary>
        public void SetBadgeCount(int count, Action<MacNotificationResult>? onResult = null)
        {
            Debug.Log($"[{LogTag}][{nameof(SetBadgeCount)}] count: {count}, onResult: {onResult != null}");
            s_onSetBadgeCount = onResult;
            if (Application.platform != RuntimePlatform.OSXPlayer) return;
            NotificationSetBadgeCount(count, s_setBadgeCountDelegate);
        }

        // ── Static AOT callbacks ──────────────────────────────────────────────────

        [MonoPInvokeCallback(typeof(NotificationSimpleCallback))]
        private static void OnRequestPermissionResult(bool isSuccess, int errorCode, string? errorMessage)
        {
            var result = isSuccess
                ? MacNotificationResult.Success(OperationRequestPermission)
                : MacNotificationResult.Failure(OperationRequestPermission, errorCode, errorMessage);
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

        [MonoPInvokeCallback(typeof(NotificationSimpleCallback))]
        private static void OnOpenSettingsResult(bool isSuccess, int errorCode, string? errorMessage)
        {
            var result = isSuccess
                ? MacNotificationResult.Success(OperationOpenSettings)
                : MacNotificationResult.Failure(OperationOpenSettings, errorCode, errorMessage);
            var cb = s_onOpenSettings;
            s_onOpenSettings = null;
            UnityMainThreadDispatcher.Instance.Enqueue(() =>
            {
                try
                {
                    cb?.Invoke(result);
                    _instance?.NotificationOperationCompleted?.Invoke(result);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[{LogTag}][{nameof(OnOpenSettingsResult)}] {ex.Message}");
                }
            });
        }

        [MonoPInvokeCallback(typeof(NotificationSimpleCallback))]
        private static void OnShowResult(bool isSuccess, int errorCode, string? errorMessage)
        {
            var result = isSuccess
                ? MacNotificationResult.Success(OperationShow)
                : MacNotificationResult.Failure(OperationShow, errorCode, errorMessage);
            var cb = s_onShow;
            s_onShow = null;
            UnityMainThreadDispatcher.Instance.Enqueue(() =>
            {
                try
                {
                    cb?.Invoke(result);
                    _instance?.NotificationOperationCompleted?.Invoke(result);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[{LogTag}][{nameof(OnShowResult)}] {ex.Message}");
                }
            });
        }

        [MonoPInvokeCallback(typeof(NotificationSimpleCallback))]
        private static void OnScheduleResult(bool isSuccess, int errorCode, string? errorMessage)
        {
            var result = isSuccess
                ? MacNotificationResult.Success(OperationSchedule)
                : MacNotificationResult.Failure(OperationSchedule, errorCode, errorMessage);
            var cb = s_onSchedule;
            s_onSchedule = null;
            UnityMainThreadDispatcher.Instance.Enqueue(() =>
            {
                try
                {
                    cb?.Invoke(result);
                    _instance?.NotificationOperationCompleted?.Invoke(result);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[{LogTag}][{nameof(OnScheduleResult)}] {ex.Message}");
                }
            });
        }

        [MonoPInvokeCallback(typeof(NotificationSimpleCallback))]
        private static void OnUpdateResult(bool isSuccess, int errorCode, string? errorMessage)
        {
            var result = isSuccess
                ? MacNotificationResult.Success(OperationUpdate)
                : MacNotificationResult.Failure(OperationUpdate, errorCode, errorMessage);
            var cb = s_onUpdate;
            s_onUpdate = null;
            UnityMainThreadDispatcher.Instance.Enqueue(() =>
            {
                try
                {
                    cb?.Invoke(result);
                    _instance?.NotificationOperationCompleted?.Invoke(result);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[{LogTag}][{nameof(OnUpdateResult)}] {ex.Message}");
                }
            });
        }

        [MonoPInvokeCallback(typeof(NotificationSimpleCallback))]
        private static void OnSetBadgeCountResult(bool isSuccess, int errorCode, string? errorMessage)
        {
            var result = isSuccess
                ? MacNotificationResult.Success(OperationSetBadgeCount)
                : MacNotificationResult.Failure(OperationSetBadgeCount, errorCode, errorMessage);
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

        [MonoPInvokeCallback(typeof(NotificationSimpleCallback))]
        private static void OnRegisterCategoryResult(bool isSuccess, int errorCode, string? errorMessage)
        {
            var result = isSuccess
                ? MacNotificationResult.Success(OperationRegisterCategory)
                : MacNotificationResult.Failure(OperationRegisterCategory, errorCode, errorMessage);
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

        [MonoPInvokeCallback(typeof(NotificationSimpleCallback))]
        private static void OnRemoveCategoryResult(bool isSuccess, int errorCode, string? errorMessage)
        {
            var result = isSuccess
                ? MacNotificationResult.Success(OperationRemoveCategory)
                : MacNotificationResult.Failure(OperationRemoveCategory, errorCode, errorMessage);
            var cb = s_onRemoveCategory;
            s_onRemoveCategory = null;
            UnityMainThreadDispatcher.Instance.Enqueue(() =>
            {
                try
                {
                    cb?.Invoke(result);
                    _instance?.NotificationOperationCompleted?.Invoke(result);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[{LogTag}][{nameof(OnRemoveCategoryResult)}] {ex.Message}");
                }
            });
        }

        [MonoPInvokeCallback(typeof(NotificationJsonCallback))]
        private static void OnGetAuthorizationStatusResult(string? json, int errorCode, string? errorMessage)
        {
            var result = json != null
                ? MacNotificationJsonResult.Success(OperationGetAuthorizationStatus, json)
                : MacNotificationJsonResult.Failure(OperationGetAuthorizationStatus, errorCode, errorMessage);
            var cb = s_onGetAuthorizationStatus;
            s_onGetAuthorizationStatus = null;
            UnityMainThreadDispatcher.Instance.Enqueue(() =>
            {
                try
                {
                    cb?.Invoke(result);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[{LogTag}][{nameof(OnGetAuthorizationStatusResult)}] {ex.Message}");
                }
            });
        }

        [MonoPInvokeCallback(typeof(NotificationJsonCallback))]
        private static void OnGetScheduledResult(string? json, int errorCode, string? errorMessage)
        {
            var result = json != null
                ? MacNotificationJsonResult.Success(OperationGetScheduled, json)
                : MacNotificationJsonResult.Failure(OperationGetScheduled, errorCode, errorMessage);
            var cb = s_onGetScheduled;
            s_onGetScheduled = null;
            UnityMainThreadDispatcher.Instance.Enqueue(() =>
            {
                try
                {
                    cb?.Invoke(result);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[{LogTag}][{nameof(OnGetScheduledResult)}] {ex.Message}");
                }
            });
        }

        [MonoPInvokeCallback(typeof(NotificationJsonCallback))]
        private static void OnGetDeliveredResult(string? json, int errorCode, string? errorMessage)
        {
            var result = json != null
                ? MacNotificationJsonResult.Success(OperationGetDelivered, json)
                : MacNotificationJsonResult.Failure(OperationGetDelivered, errorCode, errorMessage);
            var cb = s_onGetDelivered;
            s_onGetDelivered = null;
            UnityMainThreadDispatcher.Instance.Enqueue(() =>
            {
                try
                {
                    cb?.Invoke(result);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[{LogTag}][{nameof(OnGetDeliveredResult)}] {ex.Message}");
                }
            });
        }

        [MonoPInvokeCallback(typeof(NotificationBoolCallback))]
        private static void OnHasPermissionResult(bool value)
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
                    Debug.LogError($"[{LogTag}][{nameof(OnHasPermissionResult)}] {ex.Message}");
                }
            });
        }

        [MonoPInvokeCallback(typeof(NotificationActionCallback))]
        private static void OnActionReceived(string notificationId, string actionId, string? userInfoJson)
        {
            try
            {
                var result = new MacNotificationActionResult(notificationId, actionId, userInfoJson);
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
            catch (Exception ex)
            {
                Debug.LogError($"[{LogTag}][{nameof(OnActionReceived)}] {ex.Message}");
            }
        }

        [MonoPInvokeCallback(typeof(NotificationTextInputActionCallback))]
        private static void OnTextInputActionReceived(string notificationId, string actionId, string userText, string? userInfoJson)
        {
            try
            {
                var result = new MacNotificationTextInputActionResult(notificationId, actionId, userText, userInfoJson);
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
            catch (Exception ex)
            {
                Debug.LogError($"[{LogTag}][{nameof(OnTextInputActionReceived)}] {ex.Message}");
            }
        }
    }
}
#endif
