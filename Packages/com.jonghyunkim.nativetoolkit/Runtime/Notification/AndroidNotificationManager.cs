#nullable enable

#if UNITY_ANDROID
namespace JonghyunKim.NativeToolkit.Runtime.Notification
{
    using System;
    using JonghyunKim.NativeToolkit.Runtime.Common;
    using UnityEngine;

    /// <summary>
    /// Singleton manager for Android native notification operations.
    /// Wraps UnityAndroidNotificationManager (Kotlin) via AndroidJavaObject calls.
    /// All APIs accept JSON strings matching UnityNotificationJsonParser/Specs.
    /// </summary>
    public class AndroidNotificationManager : MonoBehaviour
    {
        private const string PluginClassName = "android.unity.notification.UnityAndroidNotificationManager";
        private const string LogTag = "AndroidNotificationManager";

        /// <summary>
        /// Action identifier for tapping the notification body.
        /// </summary>
        public const string ActionBodyTap = "android.unity.notification.ACTION_BODY_TAP";

        /// <summary>
        /// Action identifier for dismissing a notification.
        /// </summary>
        public const string ActionNotificationDismissed = "android.unity.notification.ACTION_NOTIFICATION_DISMISSED";

        /// <summary>
        /// Native operation name for opening app notification settings.
        /// </summary>
        public const string OperationOpenNotificationSettings = "openNotificationSettings";

        /// <summary>
        /// Native operation name for opening app details settings.
        /// </summary>
        public const string OperationOpenAppDetailsSettings = "openAppDetailsSettings";

        /// <summary>
        /// Native operation name for opening exact alarm settings.
        /// </summary>
        public const string OperationOpenExactAlarmSettings = "openExactAlarmSettings";

        /// <summary>
        /// Native operation name for creating a notification channel.
        /// </summary>
        public const string OperationCreateChannel = "createChannel";

        /// <summary>
        /// Native operation name for deleting a notification channel.
        /// </summary>
        public const string OperationDeleteChannel = "deleteChannel";

        /// <summary>
        /// Native operation name for showing a notification.
        /// </summary>
        public const string OperationShowNotification = "showNotification";

        /// <summary>
        /// Native operation name for updating a notification.
        /// </summary>
        public const string OperationUpdateNotification = "updateNotification";

        /// <summary>
        /// Native operation name for cancelling a notification.
        /// </summary>
        public const string OperationCancelNotification = "cancelNotification";

        /// <summary>
        /// Native operation name for cancelling all shown notifications.
        /// </summary>
        public const string OperationCancelAllNotifications = "cancelAllNotifications";

        /// <summary>
        /// Native operation name for scheduling a notification.
        /// </summary>
        public const string OperationScheduleNotification = "scheduleNotification";

        /// <summary>
        /// Native operation name for cancelling a scheduled notification.
        /// </summary>
        public const string OperationCancelScheduledNotification = "cancelScheduledNotification";

        /// <summary>
        /// Native operation name for cancelling all scheduled notifications.
        /// </summary>
        public const string OperationCancelAllScheduledNotifications = "cancelAllScheduledNotifications";

        /// <summary>
        /// Native operation name for starting a progress foreground service.
        /// </summary>
        public const string OperationStartProgressForegroundService = "startProgressForegroundService";

        /// <summary>
        /// Native operation name for updating a progress foreground service.
        /// </summary>
        public const string OperationUpdateProgressForegroundService = "updateProgressForegroundService";

        /// <summary>
        /// Native operation name for completing a progress foreground service.
        /// </summary>
        public const string OperationCompleteProgressForegroundService = "completeProgressForegroundService";

        /// <summary>
        /// Native operation name for stopping a progress foreground service.
        /// </summary>
        public const string OperationStopProgressForegroundService = "stopProgressForegroundService";

        private static AndroidNotificationManager? _instance;
        private AndroidJavaObject? pluginInstance;
        private NotificationOperationListenerProxy? operationListener;
        private NotificationActionListenerProxy? actionListener;
        private NotificationShownListenerProxy? shownListener;

        /// <summary>
        /// Occurs when a native notification operation completes.
        /// </summary>
        public event Action<NotificationResult>? NotificationOperationCompleted;

        /// <summary>
        /// Occurs when a notification action or body tap is received.
        /// </summary>
        public event Action<NotificationActionResult>? NotificationActionTapped;

        /// <summary>
        /// Occurs when a notification is shown while the app is in foreground.
        /// </summary>
        public event Action<NotificationReceivedResult>? NotificationReceived;

        /// <summary>
        /// Singleton instance property for AndroidNotificationManager.
        /// Creates a new instance if none exists and ensures it persists across scene loads.
        /// </summary>
        public static AndroidNotificationManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    Debug.Log("Creating new instance of AndroidNotificationManager");
                    GameObject singletonObject = new GameObject("AndroidNotificationManager");
                    _instance = singletonObject.AddComponent<AndroidNotificationManager>();
                }
                return _instance;
            }
        }

        /// <summary>
        /// Initialize the singleton instance and set up the main thread dispatcher.
        /// Ensures only one instance exists and persists across scene changes.
        /// </summary>
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

            // Ensure Dispatcher is created on main thread for safe cross-thread enqueueing
            _ = UnityMainThreadDispatcher.Instance;

            Initialize();
        }

        private void OnDestroy()
        {
            Debug.Log($"[{LogTag}][{nameof(OnDestroy)}]");
            if (_instance == this)
            {
                ClearOperationListener();
                ClearActionListener();
                ClearShownListener();
                pluginInstance?.Dispose();
                pluginInstance = null;
                _instance = null;
            }
        }

        /// <summary>
        /// Initialize the native Android plugin interface.
        /// Only initializes on Android platform, logs warning on other platforms.
        /// </summary>
        public void Initialize()
        {
            Debug.Log($"[{LogTag}][{nameof(Initialize)}]");
            if (Application.platform != RuntimePlatform.Android)
            {
                Debug.Log("Not running on an Android device. Skipping native notification initialization.");
                return;
            }

            Debug.Log("AndroidNotificationManager Initialize");
            using (AndroidJavaClass pluginClass = new AndroidJavaClass(PluginClassName))
            {
                if (pluginClass == null)
                {
                    Debug.LogError("Failed to find the Android notification plugin class.");
                    return;
                }

                pluginInstance = pluginClass.CallStatic<AndroidJavaObject>("getInstance");

                if (pluginInstance == null)
                {
                    Debug.LogError("Failed to initialize notification pluginInstance.");
                }
                else
                {
                    operationListener ??= new NotificationOperationListenerProxy(this);
                    pluginInstance.Call("setNotificationOperationListener", operationListener);
                    actionListener ??= new NotificationActionListenerProxy(this);
                    pluginInstance.Call("setNotificationActionListener", actionListener);
                    shownListener ??= new NotificationShownListenerProxy(this);
                    pluginInstance.Call("setNotificationShownListener", shownListener);
                    Debug.Log("notification pluginInstance initialized successfully.");
                }
            }
        }

        /// <summary>
        /// Checks whether the app has notification permission.
        /// </summary>
        public bool HasPermission()
        {
            Debug.Log($"[{LogTag}][{nameof(HasPermission)}]");
            return CallBool("hasPermission");
        }

        /// <summary>
        /// Checks whether the app can schedule exact alarms (Android 12+ / API 31+).
        /// Returns true on Android 11 and below. Check before calling ScheduleNotification with exact=true.
        /// </summary>
        public bool CanScheduleExactAlarms()
        {
            Debug.Log($"[{LogTag}][{nameof(CanScheduleExactAlarms)}]");
            return CallBool("canScheduleExactAlarms");
        }

        /// <summary>
        /// Checks whether notifications are enabled for the app.
        /// </summary>
        public bool AreNotificationsEnabled()
        {
            Debug.Log($"[{LogTag}][{nameof(AreNotificationsEnabled)}]");
            return CallBool("areNotificationsEnabled");
        }

        /// <summary>
        /// Requests POST_NOTIFICATIONS permission (Android 13+). Invokes onResult with true if granted.
        /// Falls through immediately with true if permission is already granted.
        /// </summary>
        public void RequestPermission(Action<bool>? onResult = null)
        {
            Debug.Log($"[{LogTag}][{nameof(RequestPermission)}] onResult: {onResult}");
#if UNITY_ANDROID && !UNITY_EDITOR
            if (HasPermission())
            {
                onResult?.Invoke(true);
                return;
            }

            var callbacks = new UnityEngine.Android.PermissionCallbacks();
            callbacks.PermissionGranted += _ => onResult?.Invoke(true);
            callbacks.PermissionDenied += _ => onResult?.Invoke(false);
            callbacks.PermissionDeniedAndDontAskAgain += _ => onResult?.Invoke(false);
            UnityEngine.Android.Permission.RequestUserPermission("android.permission.POST_NOTIFICATIONS", callbacks);
#endif
        }

        /// <summary>
        /// Opens the system notification settings screen for this app.
        /// </summary>
        public void OpenNotificationSettings()
        {
            Debug.Log($"[{LogTag}][{nameof(OpenNotificationSettings)}]");
            CallOperation(OperationOpenNotificationSettings);
        }

        /// <summary>
        /// Opens the app details settings screen.
        /// </summary>
        public void OpenAppDetailsSettings()
        {
            Debug.Log($"[{LogTag}][{nameof(OpenAppDetailsSettings)}]");
            CallOperation(OperationOpenAppDetailsSettings);
        }

        /// <summary>
        /// Opens exact alarm settings (Android 12+).
        /// </summary>
        public void OpenExactAlarmSettings()
        {
            Debug.Log($"[{LogTag}][{nameof(OpenExactAlarmSettings)}]");
            CallOperation(OperationOpenExactAlarmSettings);
        }

        /// <summary>
        /// Creates a notification channel using a JSON spec string.
        /// </summary>
        public void CreateChannel(string channelJson)
        {
            Debug.Log($"[{LogTag}][{nameof(CreateChannel)}] channelJson: {channelJson}");
            CallOperation(OperationCreateChannel, channelJson);
        }

        /// <summary>
        /// Deletes a notification channel.
        /// </summary>
        public void DeleteChannel(string channelId)
        {
            Debug.Log($"[{LogTag}][{nameof(DeleteChannel)}] channelId: {channelId}");
            CallOperation(OperationDeleteChannel, channelId);
        }

        /// <summary>
        /// Shows a notification using a JSON spec string.
        /// </summary>
        public void ShowNotification(string notificationJson)
        {
            Debug.Log($"[{LogTag}][{nameof(ShowNotification)}] notificationJson: {notificationJson}");
            CallOperation(OperationShowNotification, notificationJson);
        }

        /// <summary>
        /// Updates an existing notification using a JSON spec string.
        /// </summary>
        public void UpdateNotification(string notificationJson)
        {
            Debug.Log($"[{LogTag}][{nameof(UpdateNotification)}] notificationJson: {notificationJson}");
            CallOperation(OperationUpdateNotification, notificationJson);
        }

        /// <summary>
        /// Cancels a notification by id (and optional tag).
        /// </summary>
        public void CancelNotification(int id, string? tag = null)
        {
            Debug.Log($"[{LogTag}][{nameof(CancelNotification)}] id: {id}, tag: {tag}");
            CallOperation(OperationCancelNotification, id, tag);
        }

        /// <summary>
        /// Cancels all shown notifications.
        /// </summary>
        public void CancelAllNotifications()
        {
            Debug.Log($"[{LogTag}][{nameof(CancelAllNotifications)}]");
            CallOperation(OperationCancelAllNotifications);
        }

        /// <summary>
        /// Schedules a notification using a JSON schedule spec string.
        /// </summary>
        public void ScheduleNotification(string scheduleJson)
        {
            Debug.Log($"[{LogTag}][{nameof(ScheduleNotification)}] scheduleJson: {scheduleJson}");
            CallOperation(OperationScheduleNotification, scheduleJson);
        }

        /// <summary>
        /// Cancels a scheduled notification by id (and optional tag).
        /// </summary>
        public void CancelScheduledNotification(int id, string? tag = null)
        {
            Debug.Log($"[{LogTag}][{nameof(CancelScheduledNotification)}] id: {id}, tag: {tag}");
            CallOperation(OperationCancelScheduledNotification, id, tag);
        }

        /// <summary>
        /// Cancels all scheduled notifications.
        /// </summary>
        public void CancelAllScheduledNotifications()
        {
            Debug.Log($"[{LogTag}][{nameof(CancelAllScheduledNotifications)}]");
            CallOperation(OperationCancelAllScheduledNotifications);
        }

        /// <summary>
        /// Starts a progress foreground service notification using a JSON spec string.
        /// </summary>
        public void StartProgressForegroundService(string notificationJson)
        {
            Debug.Log($"[{LogTag}][{nameof(StartProgressForegroundService)}] notificationJson: {notificationJson}");
            CallOperation(OperationStartProgressForegroundService, notificationJson);
        }

        /// <summary>
        /// Updates a progress foreground service notification using a JSON spec string.
        /// </summary>
        public void UpdateProgressForegroundService(string notificationJson)
        {
            Debug.Log($"[{LogTag}][{nameof(UpdateProgressForegroundService)}] notificationJson: {notificationJson}");
            CallOperation(OperationUpdateProgressForegroundService, notificationJson);
        }

        /// <summary>
        /// Completes a progress foreground service notification using a JSON spec string.
        /// </summary>
        public void CompleteProgressForegroundService(string notificationJson)
        {
            Debug.Log($"[{LogTag}][{nameof(CompleteProgressForegroundService)}] notificationJson: {notificationJson}");
            CallOperation(OperationCompleteProgressForegroundService, notificationJson);
        }

        /// <summary>
        /// Stops the progress foreground service notifications.
        /// </summary>
        public void StopProgressForegroundService()
        {
            Debug.Log($"[{LogTag}][{nameof(StopProgressForegroundService)}]");
            CallOperation(OperationStopProgressForegroundService);
        }

        /// <summary>
        /// Returns true if a notification with the given id (and optional tag) is still scheduled.
        /// </summary>
        public bool IsNotificationScheduled(int id, string? tag = null)
        {
            Debug.Log($"[{LogTag}][{nameof(IsNotificationScheduled)}] id: {id}, tag: {tag}");
            return CallBool("isNotificationScheduled", id, tag);
        }

        private AndroidJavaObject? GetCurrentActivity()
        {
            try
            {
                using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                {
                    return unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AndroidNotificationManager] Failed to get currentActivity: {ex.Message}");
                return null;
            }
        }

        private bool CallBool(string methodName, params object?[] args)
        {
            if (!TryPrepareCall(methodName, out object?[] fullArgs, out AndroidJavaObject? activity, args))
            {
                return false;
            }

            using (activity)
            {
                try
                {
                    return pluginInstance!.Call<bool>(methodName, fullArgs);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[AndroidNotificationManager] {methodName} error: {ex.Message}");
                    return false;
                }
            }
        }

        private void CallOperation(string operationName, params object?[] args)
        {
            if (!TryPrepareCall(operationName, out object?[] fullArgs, out AndroidJavaObject? activity, args))
            {
                PublishOperationResult(NotificationResult.Failure(operationName, $"{operationName} could not be started."));
                return;
            }

            using (activity)
            {
                try
                {
                    pluginInstance!.Call(operationName, fullArgs);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[AndroidNotificationManager] {operationName} error: {ex.Message}");
                    PublishOperationResult(NotificationResult.Failure(operationName, ex.Message));
                }
            }
        }

        private bool TryPrepareCall(string methodName, out object?[] fullArgs, out AndroidJavaObject? activity, params object?[] args)
        {
            fullArgs = Array.Empty<object?>();
            activity = null;

            if (!EnsureAndroidReady(methodName))
            {
                return false;
            }

            activity = GetCurrentActivity();
            if (activity == null)
            {
                Debug.LogError($"[AndroidNotificationManager] {methodName} failed: currentActivity is null");
                return false;
            }

            fullArgs = PrependContext(activity, args);
            return true;
        }

        private bool EnsureAndroidReady(string methodName)
        {
            if (Application.platform != RuntimePlatform.Android)
            {
                Debug.LogWarning($"[AndroidNotificationManager] {methodName} can only be called on an Android device.");
                return false;
            }

            if (pluginInstance == null)
            {
                Debug.LogError($"[AndroidNotificationManager] {methodName} failed: pluginInstance is null. Call Initialize() first.");
                return false;
            }

            return true;
        }

        private void ClearOperationListener()
        {
            if (pluginInstance == null)
            {
                return;
            }

            try
            {
                pluginInstance.Call("clearNotificationOperationListener");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AndroidNotificationManager] clearNotificationOperationListener failed: {ex.Message}");
            }
        }

        private void ClearActionListener()
        {
            if (pluginInstance == null)
            {
                return;
            }

            try
            {
                pluginInstance.Call("clearNotificationActionListener");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AndroidNotificationManager] clearNotificationActionListener failed: {ex.Message}");
            }
        }

        private void ClearShownListener()
        {
            if (pluginInstance == null)
            {
                return;
            }

            try
            {
                pluginInstance.Call("clearNotificationShownListener");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AndroidNotificationManager] clearNotificationShownListener failed: {ex.Message}");
            }
        }

        private void PublishOperationResult(NotificationResult result)
        {
            UnityMainThreadDispatcher.Instance.Enqueue(() =>
            {
                NotificationOperationCompleted?.Invoke(result);
            });
        }

        private void PublishActionResult(NotificationActionResult result)
        {
            UnityMainThreadDispatcher.Instance.Enqueue(() =>
            {
                NotificationActionTapped?.Invoke(result);
            });
        }

        private void PublishReceivedResult(NotificationReceivedResult result)
        {
            UnityMainThreadDispatcher.Instance.Enqueue(() =>
            {
                NotificationReceived?.Invoke(result);
            });
        }

        private static object?[] PrependContext(AndroidJavaObject activity, object?[] args)
        {
            object?[] fullArgs = new object?[args.Length + 1];
            fullArgs[0] = activity;
            for (int i = 0; i < args.Length; i++)
            {
                fullArgs[i + 1] = args[i];
            }
            return fullArgs;
        }

        private sealed class NotificationOperationListenerProxy : AndroidJavaProxy
        {
            private readonly AndroidNotificationManager owner;

            public NotificationOperationListenerProxy(AndroidNotificationManager owner)
                : base("android.unity.notification.UnityAndroidNotificationManager$NotificationOperationListener")
            {
                this.owner = owner;
            }

            public void onNotificationOperation(string operation, bool isSuccessful, string? errorMessage)
            {
                NotificationResult result = isSuccessful
                    ? NotificationResult.Success(operation)
                    : NotificationResult.Failure(operation, errorMessage ?? string.Empty);
                owner.PublishOperationResult(result);
            }
        }

        private sealed class NotificationActionListenerProxy : AndroidJavaProxy
        {
            private readonly AndroidNotificationManager owner;

            public NotificationActionListenerProxy(AndroidNotificationManager owner)
                : base("android.unity.notification.NotificationActionReceiver$NotificationActionListener")
            {
                this.owner = owner;
            }

            public void onNotificationAction(string actionId, int notificationId, string? dataJson)
            {
                owner.PublishActionResult(new NotificationActionResult(actionId, notificationId, dataJson));
            }
        }

        private sealed class NotificationShownListenerProxy : AndroidJavaProxy
        {
            private readonly AndroidNotificationManager owner;

            public NotificationShownListenerProxy(AndroidNotificationManager owner)
                : base("android.library.notification.NotificationShownSupport$NotificationShownListener")
            {
                this.owner = owner;
            }

            public void onNotificationShown(int notificationId, string? tag, string channelId)
            {
                owner.PublishReceivedResult(new NotificationReceivedResult(notificationId, tag, channelId));
            }
        }
    }
}
#endif
