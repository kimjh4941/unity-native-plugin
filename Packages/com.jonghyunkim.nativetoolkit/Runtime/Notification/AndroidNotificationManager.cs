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

        public const string OperationOpenNotificationSettings = "openNotificationSettings";
        public const string OperationOpenAppDetailsSettings = "openAppDetailsSettings";
        public const string OperationOpenExactAlarmSettings = "openExactAlarmSettings";
        public const string OperationCreateChannel = "createChannel";
        public const string OperationDeleteChannel = "deleteChannel";
        public const string OperationShowNotification = "showNotification";
        public const string OperationUpdateNotification = "updateNotification";
        public const string OperationCancelNotification = "cancelNotification";
        public const string OperationCancelAllNotifications = "cancelAllNotifications";
        public const string OperationScheduleNotification = "scheduleNotification";
        public const string OperationCancelScheduledNotification = "cancelScheduledNotification";
        public const string OperationCancelAllScheduledNotifications = "cancelAllScheduledNotifications";
        public const string OperationStartProgressForegroundService = "startProgressForegroundService";
        public const string OperationUpdateProgressForegroundService = "updateProgressForegroundService";
        public const string OperationCompleteProgressForegroundService = "completeProgressForegroundService";
        public const string OperationStopProgressForegroundService = "stopProgressForegroundService";

        private static AndroidNotificationManager? _instance;
        private AndroidJavaObject? pluginInstance;
        private NotificationOperationListenerProxy? operationListener;
        private NotificationActionListenerProxy? actionListener;

        public event Action<NotificationResult>? NotificationOperationCompleted;
        public event Action<NotificationActionResult>? NotificationActionTapped;

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
            Debug.Log("AndroidNotificationManager Awake");
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
            if (_instance == this)
            {
                ClearOperationListener();
                ClearActionListener();
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
                    Debug.Log("notification pluginInstance initialized successfully.");
                }
            }
        }

        /// <summary>
        /// Checks whether the app has notification permission.
        /// </summary>
        public bool HasPermission()
        {
            return CallBool("hasPermission");
        }

        /// <summary>
        /// Checks whether notifications are enabled for the app.
        /// </summary>
        public bool AreNotificationsEnabled()
        {
            return CallBool("areNotificationsEnabled");
        }

        /// <summary>
        /// Opens the system notification settings screen for this app.
        /// </summary>
        public void OpenNotificationSettings()
        {
            CallOperation(OperationOpenNotificationSettings);
        }

        /// <summary>
        /// Opens the app details settings screen.
        /// </summary>
        public void OpenAppDetailsSettings()
        {
            CallOperation(OperationOpenAppDetailsSettings);
        }

        /// <summary>
        /// Opens exact alarm settings (Android 12+).
        /// </summary>
        public void OpenExactAlarmSettings()
        {
            CallOperation(OperationOpenExactAlarmSettings);
        }

        /// <summary>
        /// Creates a notification channel using a JSON spec string.
        /// </summary>
        public void CreateChannel(string channelJson)
        {
            CallOperation(OperationCreateChannel, channelJson);
        }

        /// <summary>
        /// Deletes a notification channel.
        /// </summary>
        public void DeleteChannel(string channelId)
        {
            CallOperation(OperationDeleteChannel, channelId);
        }

        /// <summary>
        /// Shows a notification using a JSON spec string.
        /// </summary>
        public void ShowNotification(string notificationJson)
        {
            CallOperation(OperationShowNotification, notificationJson);
        }

        /// <summary>
        /// Updates an existing notification using a JSON spec string.
        /// </summary>
        public void UpdateNotification(string notificationJson)
        {
            CallOperation(OperationUpdateNotification, notificationJson);
        }

        /// <summary>
        /// Cancels a notification by id (and optional tag).
        /// </summary>
        public void CancelNotification(int id, string? tag = null)
        {
            CallOperation(OperationCancelNotification, id, tag);
        }

        /// <summary>
        /// Cancels all shown notifications.
        /// </summary>
        public void CancelAllNotifications()
        {
            CallOperation(OperationCancelAllNotifications);
        }

        /// <summary>
        /// Schedules a notification using a JSON schedule spec string.
        /// </summary>
        public void ScheduleNotification(string scheduleJson)
        {
            CallOperation(OperationScheduleNotification, scheduleJson);
        }

        /// <summary>
        /// Cancels a scheduled notification by id (and optional tag).
        /// </summary>
        public void CancelScheduledNotification(int id, string? tag = null)
        {
            CallOperation(OperationCancelScheduledNotification, id, tag);
        }

        /// <summary>
        /// Cancels all scheduled notifications.
        /// </summary>
        public void CancelAllScheduledNotifications()
        {
            CallOperation(OperationCancelAllScheduledNotifications);
        }

        /// <summary>
        /// Starts a progress foreground service notification using a JSON spec string.
        /// </summary>
        public void StartProgressForegroundService(string notificationJson)
        {
            CallOperation(OperationStartProgressForegroundService, notificationJson);
        }

        /// <summary>
        /// Updates a progress foreground service notification using a JSON spec string.
        /// </summary>
        public void UpdateProgressForegroundService(string notificationJson)
        {
            CallOperation(OperationUpdateProgressForegroundService, notificationJson);
        }

        /// <summary>
        /// Completes a progress foreground service notification using a JSON spec string.
        /// </summary>
        public void CompleteProgressForegroundService(string notificationJson)
        {
            CallOperation(OperationCompleteProgressForegroundService, notificationJson);
        }

        /// <summary>
        /// Stops the progress foreground service notifications.
        /// </summary>
        public void StopProgressForegroundService()
        {
            CallOperation(OperationStopProgressForegroundService);
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

            public void onNotificationAction(string actionId, int notificationId)
            {
                owner.PublishActionResult(new NotificationActionResult(actionId, notificationId));
            }
        }
    }
}
#endif
