#nullable enable

#if UNITY_ANDROID
namespace JonghyunKim.NativeToolkit.Runtime.Notification
{
    using UnityEngine;
    using System;
    using JonghyunKim.NativeToolkit.Runtime.Common;

    /// <summary>
    /// Singleton manager for Android native notification operations.
    /// Wraps UnityAndroidNotificationManager (Kotlin) via AndroidJavaObject calls.
    /// All APIs accept JSON strings matching UnityNotificationJsonParser/Specs.
    /// </summary>
    public class AndroidNotificationManager : MonoBehaviour
    {
        private const string PluginClassName = "android.unity.notification.UnityAndroidNotificationManager";
        private static AndroidNotificationManager? _instance;
        private AndroidJavaObject? pluginInstance;

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
                    DontDestroyOnLoad(singletonObject);
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
            }

            // Ensure Dispatcher is created on main thread for safe cross-thread enqueueing
            _ = UnityMainThreadDispatcher.Instance;

            Initialize();
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
        public bool OpenNotificationSettings()
        {
            return CallBool("openNotificationSettings");
        }

        /// <summary>
        /// Opens the app details settings screen.
        /// </summary>
        public bool OpenAppDetailsSettings()
        {
            return CallBool("openAppDetailsSettings");
        }

        /// <summary>
        /// Opens exact alarm settings (Android 12+).
        /// </summary>
        public bool OpenExactAlarmSettings()
        {
            return CallBool("openExactAlarmSettings");
        }

        /// <summary>
        /// Creates a notification channel using a JSON spec string.
        /// </summary>
        public bool CreateChannel(string channelJson)
        {
            return CallBool("createChannel", channelJson);
        }

        /// <summary>
        /// Deletes a notification channel.
        /// </summary>
        public void DeleteChannel(string channelId)
        {
            CallVoid("deleteChannel", channelId);
        }

        /// <summary>
        /// Shows a notification using a JSON spec string.
        /// </summary>
        public bool ShowNotification(string notificationJson)
        {
            return CallBool("showNotification", notificationJson);
        }

        /// <summary>
        /// Updates an existing notification using a JSON spec string.
        /// </summary>
        public bool UpdateNotification(string notificationJson)
        {
            return CallBool("updateNotification", notificationJson);
        }

        /// <summary>
        /// Cancels a notification by id (and optional tag).
        /// </summary>
        public void CancelNotification(int id, string? tag = null)
        {
            CallVoid("cancelNotification", id, tag);
        }

        /// <summary>
        /// Cancels all shown notifications.
        /// </summary>
        public void CancelAllNotifications()
        {
            CallVoid("cancelAllNotifications");
        }

        /// <summary>
        /// Schedules a notification using a JSON schedule spec string.
        /// </summary>
        public bool ScheduleNotification(string scheduleJson)
        {
            return CallBool("scheduleNotification", scheduleJson);
        }

        /// <summary>
        /// Cancels a scheduled notification by id (and optional tag).
        /// </summary>
        public void CancelScheduledNotification(int id, string? tag = null)
        {
            CallVoid("cancelScheduledNotification", id, tag);
        }

        /// <summary>
        /// Cancels all scheduled notifications.
        /// </summary>
        public void CancelAllScheduledNotifications()
        {
            CallVoid("cancelAllScheduledNotifications");
        }

        /// <summary>
        /// Starts a progress foreground service notification using a JSON spec string.
        /// </summary>
        public bool StartProgressForegroundService(string notificationJson)
        {
            return CallBool("startProgressForegroundService", notificationJson);
        }

        /// <summary>
        /// Updates a progress foreground service notification using a JSON spec string.
        /// </summary>
        public bool UpdateProgressForegroundService(string notificationJson)
        {
            return CallBool("updateProgressForegroundService", notificationJson);
        }

        /// <summary>
        /// Completes a progress foreground service notification using a JSON spec string.
        /// </summary>
        public bool CompleteProgressForegroundService(string notificationJson)
        {
            return CallBool("completeProgressForegroundService", notificationJson);
        }

        /// <summary>
        /// Stops the progress foreground service notifications.
        /// </summary>
        public void StopProgressForegroundService()
        {
            CallVoid("stopProgressForegroundService");
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
            if (!EnsureAndroidReady(methodName))
            {
                return false;
            }

            try
            {
                AndroidJavaObject? activity = GetCurrentActivity();
                if (activity == null)
                {
                    Debug.LogError($"[AndroidNotificationManager] {methodName} failed: currentActivity is null");
                    return false;
                }

                object?[] fullArgs = PrependContext(activity, args);
                return pluginInstance!.Call<bool>(methodName, fullArgs);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AndroidNotificationManager] {methodName} error: {ex.Message}");
                return false;
            }
        }

        private void CallVoid(string methodName, params object?[] args)
        {
            if (!EnsureAndroidReady(methodName))
            {
                return;
            }

            try
            {
                AndroidJavaObject? activity = GetCurrentActivity();
                if (activity == null)
                {
                    Debug.LogError($"[AndroidNotificationManager] {methodName} failed: currentActivity is null");
                    return;
                }

                object?[] fullArgs = PrependContext(activity, args);
                pluginInstance!.Call(methodName, fullArgs);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AndroidNotificationManager] {methodName} error: {ex.Message}");
            }
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
    }
}
#endif
