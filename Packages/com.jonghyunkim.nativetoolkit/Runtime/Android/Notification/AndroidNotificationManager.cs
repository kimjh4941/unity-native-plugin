#nullable enable

#if UNITY_ANDROID || UNITY_EDITOR
namespace JonghyunKim.NativeToolkit.Runtime.Android.Notification
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using JonghyunKim.NativeToolkit.Runtime.Android.Common;
    using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
    using System.Runtime.InteropServices;
    using AOT;
#endif

    /// <summary>
    /// Singleton manager for Android native notification operations. The operations take the JSON
    /// that <see cref="AndroidNotificationJsonBuilder"/> writes, and report their results through
    /// <see cref="NotificationOperationCompleted"/>.
    /// </summary>
    /// <remarks>
    /// Calls native-toolkit's C ABI (libntk.so, <c>ntk_notification_*</c>) since 2.0.0; 1.x called a
    /// Kotlin bridge through AndroidJavaObject. The public API is unchanged, and the JSON is read by the
    /// rules of 1.x's parser. Behaviour that changed is listed in
    /// artifact/android/notification/designs/2026-10-12-android-notification-design-v4.md, 5.3; in short:
    /// <list type="bullet">
    /// <item>Showing, updating, or scheduling for a time already past fails without the notification
    /// permission (Android 13 and later) or with the app's notifications turned off: "Failed to {op}:
    /// notifications are not allowed for this app (permission or app notification setting)." 1.x
    /// reported success and showed nothing.</item>
    /// <item>A resource the JSON names (an icon, a picture, a layout, a view) that the app does not have
    /// fails the call, and nothing is shown: "Failed to {op}: a resource named in the notification was
    /// not found." 1.x put the app's icon or the default style in its place. A resource is found by
    /// name, in drawable and then mipmap (layouts in layout, views in id); the <c>type</c> of a
    /// resource is not used. Keep the resources from being removed by resource shrinking.</item>
    /// <item>Values out of the platform's range (groupAlertBehavior, timestamps, timeouts,
    /// semanticAction, alarmType, negative vibration values, negative message timestamps) fail with "Invalid argument for {op}:
    /// Please verify the input parameters." 1.x passed them on.</item>
    /// <item>The settings screens open on the Android main thread and fail when the app is not in the
    /// foreground. The notification settings screen opens itself; 1.x fell back to the app's details.</item>
    /// <item>A tap on a notification made while no manager existed, even before the app started, is
    /// kept (up to 32) and delivered once the manager is first created, in a later frame: subscribe to
    /// <see cref="NotificationActionTapped"/> in the frame <see cref="Instance"/> is first read.</item>
    /// <item>The methods may be called from any thread (1.x refused other threads), except
    /// <see cref="RequestPermission"/>, which uses Unity's permission API. <see cref="Instance"/> creates
    /// a GameObject the first time, which Unity allows on its main thread only: get it there first.</item>
    /// <item>An exact schedule without the exact alarm permission is made inexact, as in 1.x.
    /// <see cref="IsNotificationScheduled"/> sees the schedules kept across a reboot only
    /// (<c>persistAcrossBoot</c>), as in 1.x.</item>
    /// <item>Data values arrive as they were sent (1.x left JSON escapes in them). Android requires IL2CPP
    /// and ARM64 (or x86_64).</item>
    /// </list>
    /// Every operation result, failures found before the native call included, arrives on Unity's main
    /// thread, after the method has returned for a call made there.
    /// </remarks>
    public class AndroidNotificationManager : MonoBehaviour
    {
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

        // C#-only names for the queries: they report no operation result, so they are used in logs only.
        private const string OperationHasPermission = "hasPermission";
        private const string OperationAreEnabled = "areNotificationsEnabled";
        private const string OperationCanScheduleExactAlarms = "canScheduleExactAlarms";
        private const string OperationIsScheduled = "isNotificationScheduled";

        private static AndroidNotificationManager? _instance;

        // The two listeners are registered once per process and never removed (design J-N12), each
        // with its own mark so that a failed one alone is tried again. The lock is for the registration
        // only; the callbacks never take it.
        private static readonly object s_listenerLock = new object();
        private static bool s_interactionAdded;
        private static bool s_shownAdded;

        // Test seams for the registrations (design 7.2). Null: the library.
        private static Func<int>? s_addInteractionForTests;
        private static Func<int>? s_addShownForTests;

        // Settings calls handed to the library and not completed yet, and the last result one completed
        // with (-1: none, or it failed). For the device tests (plan 6, AN-40).
        private static int s_pendingSettings;
        private static volatile int s_lastSettingsResult = -1;

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
        /// Creates a new instance if none exists and ensures it persists across scene loads. The first
        /// read must be on Unity's main thread.
        /// </summary>
        public static AndroidNotificationManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    Debug.Log($"[{LogTag}] Creating new instance");
                    GameObject singletonObject = new GameObject("AndroidNotificationManager");
                    _instance = singletonObject.AddComponent<AndroidNotificationManager>();
                }
                return _instance;
            }
        }

        /// <summary>
        /// Resets the static state when the app or the Editor's play mode starts. With the Editor's
        /// domain reload turned off, statics would otherwise keep the last play session's values.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnStart() => ResetStaticStateForTests();

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

            // Created here, on the main thread: a native callback must never create Unity objects.
            AndroidNativeCallbackQueue.EnsureCreated();
            Initialize();
        }

        private void OnDestroy()
        {
            Debug.Log($"[{LogTag}][{nameof(OnDestroy)}]");
            // The listeners stay registered (J-N20): results and events after this are dropped when
            // the queue runs, unless a new manager exists by then.
            if (_instance == this) _instance = null;
        }

        /// <summary>
        /// Registers the native listeners for taps and shown notifications, once per process. Called from
        /// <c>Awake</c>; calling it again is harmless. No-op on non-Android platforms.
        /// </summary>
        public void Initialize()
        {
            Debug.Log($"[{LogTag}][{nameof(Initialize)}]");
            if (Application.platform != RuntimePlatform.Android)
            {
                Debug.Log($"[{LogTag}] Not running on an Android device. Skipping native notification initialization.");
                return;
            }
            if (!NativeAvailable(nameof(Initialize))) return;

            EnsureListeners();
        }

        // ---- Queries ----

        /// <summary>
        /// Checks whether the app has notification permission.
        /// </summary>
        public bool HasPermission()
        {
            AndroidNativeCallbackQueue.EnsureCreated();
            Debug.Log($"[{LogTag}][{nameof(HasPermission)}]");
#if UNITY_ANDROID && !UNITY_EDITOR
            return Query(OperationHasPermission, _ => (AndroidNotificationCApi.ntk_notification_has_permission(out int value), value));
#else
            return Query(OperationHasPermission, _ => (AndroidNotificationCApi.ErrorNotSupported, 0));
#endif
        }

        /// <summary>
        /// Checks whether the app can schedule exact alarms (Android 12+ / API 31+).
        /// Returns true on Android 11 and below. Check before calling ScheduleNotification with exact=true.
        /// </summary>
        public bool CanScheduleExactAlarms()
        {
            AndroidNativeCallbackQueue.EnsureCreated();
            Debug.Log($"[{LogTag}][{nameof(CanScheduleExactAlarms)}]");
#if UNITY_ANDROID && !UNITY_EDITOR
            return Query(OperationCanScheduleExactAlarms, _ => (AndroidNotificationCApi.ntk_notification_can_schedule_exact_alarms(out int value), value));
#else
            return Query(OperationCanScheduleExactAlarms, _ => (AndroidNotificationCApi.ErrorNotSupported, 0));
#endif
        }

        /// <summary>
        /// Checks whether notifications are enabled for the app.
        /// </summary>
        public bool AreNotificationsEnabled()
        {
            AndroidNativeCallbackQueue.EnsureCreated();
            Debug.Log($"[{LogTag}][{nameof(AreNotificationsEnabled)}]");
#if UNITY_ANDROID && !UNITY_EDITOR
            return Query(OperationAreEnabled, _ => (AndroidNotificationCApi.ntk_notification_are_enabled(out int value), value));
#else
            return Query(OperationAreEnabled, _ => (AndroidNotificationCApi.ErrorNotSupported, 0));
#endif
        }

        /// <summary>
        /// Returns true if a notification with the given id (and optional tag) is still scheduled. Only
        /// the schedules kept across a reboot (<c>persistAcrossBoot</c>) are seen, as in 1.x.
        /// </summary>
        public bool IsNotificationScheduled(int id, string? tag = null)
        {
            AndroidNativeCallbackQueue.EnsureCreated();
            Debug.Log($"[{LogTag}][{nameof(IsNotificationScheduled)}] id: {id}, hasTag: {tag != null}");
#if UNITY_ANDROID && !UNITY_EDITOR
            return Query(OperationIsScheduled, allocated =>
                (AndroidNotificationCApi.ntk_notification_is_scheduled(id, AndroidNativeToolkitCApi.AllocUtf8(tag, allocated), out int value), value));
#else
            return Query(OperationIsScheduled, _ => (AndroidNotificationCApi.ErrorNotSupported, 0));
#endif
        }

        /// <summary>
        /// Requests POST_NOTIFICATIONS permission (Android 13+). Invokes onResult with true if granted.
        /// Falls through immediately with true if permission is already granted. Uses Unity's permission
        /// API: call it on Unity's main thread.
        /// </summary>
        public void RequestPermission(Action<bool>? onResult = null)
        {
            Debug.Log($"[{LogTag}][{nameof(RequestPermission)}] hasCallback: {onResult != null}");
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

        // ---- Settings screens (J-N9) ----

        /// <summary>
        /// Opens the system notification settings screen for this app. The screen opens on the Android
        /// main thread; the call fails when the app is not in the foreground.
        /// </summary>
        public void OpenNotificationSettings()
        {
            Debug.Log($"[{LogTag}][{nameof(OpenNotificationSettings)}]");
            OpenSettings(OperationOpenNotificationSettings, AndroidNotificationCApi.TargetNotifications);
        }

        /// <summary>
        /// Opens the app details settings screen. The call fails when the app is not in the foreground.
        /// </summary>
        public void OpenAppDetailsSettings()
        {
            Debug.Log($"[{LogTag}][{nameof(OpenAppDetailsSettings)}]");
            OpenSettings(OperationOpenAppDetailsSettings, AndroidNotificationCApi.TargetAppDetails);
        }

        /// <summary>
        /// Opens exact alarm settings (Android 12+). The call fails when the app is not in the foreground.
        /// </summary>
        public void OpenExactAlarmSettings()
        {
            Debug.Log($"[{LogTag}][{nameof(OpenExactAlarmSettings)}]");
            OpenSettings(OperationOpenExactAlarmSettings, AndroidNotificationCApi.TargetExactAlarm);
        }

        // ---- Channels ----

        /// <summary>
        /// Creates a notification channel using a JSON spec string.
        /// </summary>
        public void CreateChannel(string channelJson)
        {
            AndroidNativeCallbackQueue.EnsureCreated();
            Debug.Log($"[{LogTag}][{nameof(CreateChannel)}] jsonLength: {channelJson?.Length ?? 0}");
            const string op = OperationCreateChannel;
            if (!CanCall(op)) return;
            string? error = AndroidNotificationJsonMapping.ReadChannel(channelJson, op, out AndroidNotificationChannelSpec? spec);
            if (error != null)
            {
                Refuse(op, error);
                return;
            }
#if UNITY_ANDROID && !UNITY_EDITOR
            Run(op, allocated =>
            {
                int code = AndroidNotificationCApi.BuildChannel(spec!, allocated, out IntPtr channel, out string step);
                if (code != AndroidNotificationCApi.ErrorNone)
                {
                    LogBuilderFailure(op, step, code);
                    return code;
                }
                try
                {
                    return AndroidNotificationCApi.ntk_notification_create_channel(channel);
                }
                finally
                {
                    AndroidNotificationCApi.ntk_notification_channel_free(channel);
                }
            });
#endif
        }

        /// <summary>
        /// Deletes a notification channel. A null ID deletes the channel "".
        /// </summary>
        public void DeleteChannel(string channelId)
        {
            AndroidNativeCallbackQueue.EnsureCreated();
            Debug.Log($"[{LogTag}][{nameof(DeleteChannel)}] channelId: {channelId}");
            const string op = OperationDeleteChannel;
            if (!CanCall(op)) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            Run(op, allocated => AndroidNotificationCApi.ntk_notification_delete_channel(AndroidNativeToolkitCApi.AllocUtf8(channelId ?? "", allocated)));
#endif
        }

        // ---- Notifications ----

        /// <summary>
        /// Shows a notification using a JSON spec string. Fails without the notification permission or
        /// with the app's notifications turned off, and when a resource it names is missing.
        /// </summary>
        public void ShowNotification(string notificationJson)
        {
            Debug.Log($"[{LogTag}][{nameof(ShowNotification)}] jsonLength: {notificationJson?.Length ?? 0}");
#if UNITY_ANDROID && !UNITY_EDITOR
            Present(OperationShowNotification, notificationJson, content => AndroidNotificationCApi.ntk_notification_show(content));
#else
            Present(OperationShowNotification, notificationJson, null);
#endif
        }

        /// <summary>
        /// Updates an existing notification using a JSON spec string. Fails as
        /// <see cref="ShowNotification"/> does.
        /// </summary>
        public void UpdateNotification(string notificationJson)
        {
            Debug.Log($"[{LogTag}][{nameof(UpdateNotification)}] jsonLength: {notificationJson?.Length ?? 0}");
#if UNITY_ANDROID && !UNITY_EDITOR
            Present(OperationUpdateNotification, notificationJson, content => AndroidNotificationCApi.ntk_notification_update(content));
#else
            Present(OperationUpdateNotification, notificationJson, null);
#endif
        }

        /// <summary>
        /// Cancels a notification by id (and optional tag).
        /// </summary>
        public void CancelNotification(int id, string? tag = null)
        {
            AndroidNativeCallbackQueue.EnsureCreated();
            Debug.Log($"[{LogTag}][{nameof(CancelNotification)}] id: {id}, hasTag: {tag != null}");
            const string op = OperationCancelNotification;
            if (!CanCall(op)) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            Run(op, allocated => AndroidNotificationCApi.ntk_notification_remove(id, AndroidNativeToolkitCApi.AllocUtf8(tag, allocated)));
#endif
        }

        /// <summary>
        /// Cancels all shown notifications.
        /// </summary>
        public void CancelAllNotifications()
        {
            AndroidNativeCallbackQueue.EnsureCreated();
            Debug.Log($"[{LogTag}][{nameof(CancelAllNotifications)}]");
            const string op = OperationCancelAllNotifications;
            if (!CanCall(op)) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            Run(op, _ => AndroidNotificationCApi.ntk_notification_remove_all());
#endif
        }

        // ---- Schedules ----

        /// <summary>
        /// Schedules a notification using a JSON schedule spec string. An exact schedule without the exact
        /// alarm permission is made inexact, as in 1.x. A time already past shows the notification at once,
        /// and fails as <see cref="ShowNotification"/> does.
        /// </summary>
        public void ScheduleNotification(string scheduleJson)
        {
            AndroidNativeCallbackQueue.EnsureCreated();
            Debug.Log($"[{LogTag}][{nameof(ScheduleNotification)}] jsonLength: {scheduleJson?.Length ?? 0}");
            const string op = OperationScheduleNotification;
            if (!CanCall(op)) return;
            string? error = AndroidNotificationJsonMapping.ReadSchedule(scheduleJson, op, NowMillis(), out AndroidNotificationScheduleSpec? spec);
            if (error != null)
            {
                Refuse(op, error);
                return;
            }
            AndroidNotificationScheduleSpec schedule = spec!;
            LogSpec(op, schedule.Notification);
            EnsureListeners();
#if UNITY_ANDROID && !UNITY_EDITOR
            Run(op, allocated =>
            {
                int code = AndroidNotificationCApi.BuildContent(schedule.Notification, allocated, out IntPtr content, out string step);
                if (code != AndroidNotificationCApi.ErrorNone)
                {
                    LogBuilderFailure(op, step, code);
                    return code;
                }
                try
                {
                    return ScheduleWithFallback(AndroidNotificationCApi.InexactOf(schedule), inexact =>
                    {
                        AndroidNotificationCApi.ScheduleOptions options = AndroidNotificationCApi.BuildScheduleOptions(schedule, inexact);
                        return AndroidNotificationCApi.ntk_notification_schedule(content, ref options);
                    });
                }
                finally
                {
                    AndroidNotificationCApi.ntk_notification_content_free(content);
                }
            });
#endif
        }

        /// <summary>
        /// Cancels a scheduled notification by id (and optional tag).
        /// </summary>
        public void CancelScheduledNotification(int id, string? tag = null)
        {
            AndroidNativeCallbackQueue.EnsureCreated();
            Debug.Log($"[{LogTag}][{nameof(CancelScheduledNotification)}] id: {id}, hasTag: {tag != null}");
            const string op = OperationCancelScheduledNotification;
            if (!CanCall(op)) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            Run(op, allocated => AndroidNotificationCApi.ntk_notification_cancel_scheduled(id, AndroidNativeToolkitCApi.AllocUtf8(tag, allocated)));
#endif
        }

        /// <summary>
        /// Cancels all scheduled notifications that are kept across a reboot (<c>persistAcrossBoot</c>);
        /// the others cannot be found again, as in 1.x.
        /// </summary>
        public void CancelAllScheduledNotifications()
        {
            AndroidNativeCallbackQueue.EnsureCreated();
            Debug.Log($"[{LogTag}][{nameof(CancelAllScheduledNotifications)}]");
            const string op = OperationCancelAllScheduledNotifications;
            if (!CanCall(op)) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            Run(op, _ => AndroidNotificationCApi.ntk_notification_cancel_all_scheduled());
#endif
        }

        // ---- Progress ----

        /// <summary>
        /// Starts a progress foreground service notification using a JSON spec string, which must hold
        /// <c>progress</c>.
        /// </summary>
        public void StartProgressForegroundService(string notificationJson)
        {
            Debug.Log($"[{LogTag}][{nameof(StartProgressForegroundService)}] jsonLength: {notificationJson?.Length ?? 0}");
#if UNITY_ANDROID && !UNITY_EDITOR
            Progress(OperationStartProgressForegroundService, notificationJson, content => AndroidNotificationCApi.ntk_notification_start_progress(content));
#else
            Progress(OperationStartProgressForegroundService, notificationJson, null);
#endif
        }

        /// <summary>
        /// Updates a progress foreground service notification using a JSON spec string, which must hold
        /// <c>progress</c>.
        /// </summary>
        public void UpdateProgressForegroundService(string notificationJson)
        {
            Debug.Log($"[{LogTag}][{nameof(UpdateProgressForegroundService)}] jsonLength: {notificationJson?.Length ?? 0}");
#if UNITY_ANDROID && !UNITY_EDITOR
            Progress(OperationUpdateProgressForegroundService, notificationJson, content => AndroidNotificationCApi.ntk_notification_update_progress(content));
#else
            Progress(OperationUpdateProgressForegroundService, notificationJson, null);
#endif
        }

        /// <summary>
        /// Completes a progress foreground service notification using a JSON spec string, which must hold
        /// <c>progress</c>.
        /// </summary>
        public void CompleteProgressForegroundService(string notificationJson)
        {
            Debug.Log($"[{LogTag}][{nameof(CompleteProgressForegroundService)}] jsonLength: {notificationJson?.Length ?? 0}");
#if UNITY_ANDROID && !UNITY_EDITOR
            Progress(OperationCompleteProgressForegroundService, notificationJson, content => AndroidNotificationCApi.ntk_notification_complete_progress(content));
#else
            Progress(OperationCompleteProgressForegroundService, notificationJson, null);
#endif
        }

        /// <summary>
        /// Stops the progress foreground service notifications.
        /// </summary>
        public void StopProgressForegroundService()
        {
            AndroidNativeCallbackQueue.EnsureCreated();
            Debug.Log($"[{LogTag}][{nameof(StopProgressForegroundService)}]");
            const string op = OperationStopProgressForegroundService;
            if (!CanCall(op)) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            Run(op, _ => AndroidNotificationCApi.ntk_notification_stop_progress());
#endif
        }

        // ---- Shared call machinery ----

        /// <summary>
        /// Show and update: reads the JSON, builds the content, and calls <paramref name="call"/> with it
        /// (design 5.4). <paramref name="call"/> is null outside an Android player, where the call never
        /// gets that far.
        /// </summary>
        private static void Present(string operation, string? json, Func<IntPtr, int>? call)
        {
            AndroidNativeCallbackQueue.EnsureCreated();
            if (!CanCall(operation)) return;
            string? error = AndroidNotificationJsonMapping.ReadNotification(json, operation, NowMillis(), out AndroidNotificationSpec? spec);
            if (error != null)
            {
                Refuse(operation, error);
                return;
            }
            LogSpec(operation, spec!);
            EnsureListeners();
            BuildAndRun(operation, spec!, call!);
        }

        /// <summary>The three Progress calls that take a notification: <c>progress</c> is required (design 4.2).</summary>
        private static void Progress(string operation, string? json, Func<IntPtr, int>? call)
        {
            AndroidNativeCallbackQueue.EnsureCreated();
            if (!CanCall(operation)) return;
            string? error = AndroidNotificationJsonMapping.ReadNotification(json, operation, NowMillis(), out AndroidNotificationSpec? spec);
            if (error == null && !spec!.HasProgress) error = AndroidNotificationCApi.ProgressRequiredMessage(operation);
            if (error != null)
            {
                Refuse(operation, error);
                return;
            }
            LogSpec(operation, spec!);
            EnsureListeners();
            BuildAndRun(operation, spec!, call!);
        }

        private static void BuildAndRun(string operation, AndroidNotificationSpec spec, Func<IntPtr, int> call)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            Run(operation, allocated =>
            {
                int code = AndroidNotificationCApi.BuildContent(spec, allocated, out IntPtr content, out string step);
                if (code != AndroidNotificationCApi.ErrorNone)
                {
                    LogBuilderFailure(operation, step, code);
                    return code;
                }
                try
                {
                    return call(content);
                }
                finally
                {
                    AndroidNotificationCApi.ntk_notification_content_free(content);
                }
            });
#endif
        }

        private static void OpenSettings(string operation, int target)
        {
            AndroidNativeCallbackQueue.EnsureCreated();
            if (!CanCall(operation)) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            OpenSettingsCore(operation, () =>
                AndroidNotificationCApi.ntk_notification_open_settings_async(target, s_settingsPointer, (IntPtr)target, IntPtr.Zero));
#endif
        }

        /// <summary>
        /// Hands a settings call to the library through <paramref name="open"/> and counts it until its
        /// completion (<see cref="OnSettingsCore"/>). Refused at the entry, or thrown, it is not counted
        /// and its failure is posted here: no completion comes for it.
        /// </summary>
        internal static void OpenSettingsCore(string operation, Func<int> open)
        {
            Debug.Log($"[{LogTag}][{nameof(OpenSettingsCore)}] operation: {operation}");
            int code;
            try
            {
                // Counted before the call: the completion may run on the Android main thread before it returns.
                Interlocked.Increment(ref s_pendingSettings);
                code = open();
            }
            catch (Exception ex)
            {
                Interlocked.Decrement(ref s_pendingSettings);
                Debug.LogError($"[{LogTag}] {operation} error: {ex.Message}");
                Post(NotificationResult.Failure(operation, ex.Message));
                return;
            }
            if (code != AndroidNotificationCApi.ErrorNone)
            {
                Interlocked.Decrement(ref s_pendingSettings);
                LogFailure(operation, code);
                Post(NotificationResult.Failure(operation, AndroidNotificationCApi.FailureMessage(operation, code)));
            }
        }

        /// <summary>
        /// Whether an operation can reach the library. When not, posts 1.x's "{op} could not be
        /// started." failure (design 4.5, J-N16). Checked before the JSON is read.
        /// </summary>
        private static bool CanCall(string operation)
        {
            if (Application.platform != RuntimePlatform.Android)
            {
                Debug.LogWarning($"[{LogTag}] {operation} can only be called on an Android device.");
                Post(NotificationResult.Failure(operation, AndroidNotificationCApi.NotStartedMessage(operation)));
                return false;
            }
            if (!NativeAvailable(operation))
            {
                Post(NotificationResult.Failure(operation, AndroidNotificationCApi.NotStartedMessage(operation)));
                return false;
            }
            return true;
        }

        private static bool NativeAvailable(string operation)
        {
            if (AndroidNativeToolkitCApi.EnsureNativeAvailable() == AndroidNativeToolkitCApi.NativeState.Available) return true;
            Debug.LogWarning($"[{LogTag}][{operation}] the native library is not available (libntk.so ships for arm64-v8a and x86_64 only).");
            return false;
        }

        /// <summary>A query: the value when the library answers, else false (J-N11).</summary>
        private static bool Query(string operation, Func<List<IntPtr>, (int Code, int Value)> call)
        {
            if (Application.platform != RuntimePlatform.Android)
            {
                Debug.LogWarning($"[{LogTag}] {operation} can only be called on an Android device.");
                return false;
            }
            if (!NativeAvailable(operation)) return false;

            var allocated = new List<IntPtr>();
            try
            {
                (int code, int value) = call(allocated);
                if (code == AndroidNotificationCApi.ErrorNone) return value != 0;
                LogFailure(operation, code);
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{LogTag}] {operation} error: {ex.Message}");
                return false;
            }
            finally
            {
                AndroidNativeToolkitCApi.FreeAll(allocated);
            }
        }

        /// <summary>Runs one synchronous C call, frees what it allocated, and posts its result (J-N1).</summary>
        private static void Run(string operation, Func<List<IntPtr>, int> call)
        {
            List<IntPtr>? allocated = null;
            NotificationResult result;
            try
            {
                allocated = new List<IntPtr>();
                int code = call(allocated);
                if (code == AndroidNotificationCApi.ErrorNone)
                {
                    result = NotificationResult.Success(operation);
                }
                else
                {
                    LogFailure(operation, code);
                    result = NotificationResult.Failure(operation, AndroidNotificationCApi.FailureMessage(operation, code));
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{LogTag}] {operation} error: {ex.Message}");
                result = NotificationResult.Failure(operation, ex.Message);
            }
            finally
            {
                if (allocated != null) AndroidNativeToolkitCApi.FreeAll(allocated);
            }
            Post(result);
        }

        /// <summary>
        /// Decides whether to schedule again as inexact (design J-N8): first <c>schedule(inexact)</c>;
        /// again with 1 only when an exact schedule (<paramref name="inexact"/> 0) answered
        /// EXACT_ALARM_NOT_ALLOWED. The answer is the last call's.
        /// </summary>
        internal static int ScheduleWithFallback(int inexact, Func<int, int> schedule)
        {
            Debug.Log($"[{LogTag}][{nameof(ScheduleWithFallback)}] inexact: {inexact}");
            int code = schedule(inexact);
            if (inexact != 0 || code != AndroidNotificationCApi.ErrorExactAlarmNotAllowed) return code;
            Debug.Log($"[{LogTag}][{nameof(ScheduleWithFallback)}] exact alarms are not allowed; scheduling as inexact");
            return schedule(1);
        }

        /// <summary>A JSON the reading refused: the result, and a warning without the JSON.</summary>
        private static void Refuse(string operation, string message)
        {
            Debug.LogWarning($"[{LogTag}][{operation}] {message}");
            Post(NotificationResult.Failure(operation, message));
        }

        /// <summary>What a spec holds, as lengths and counts only (design 4.5): the text is the app's.</summary>
        private static void LogSpec(string operation, AndroidNotificationSpec spec) =>
            Debug.Log($"[{LogTag}][{operation}] id: {spec.Id}, titleLength: {spec.Title.Length}, messageLength: {spec.Message.Length}, " +
                      $"hasTag: {spec.Tag != null}, style: {spec.Style.Kind}, actions: {spec.Actions.Count}, data: {spec.Data.Count}, " +
                      $"hasProgress: {spec.HasProgress}");

        private static void LogFailure(string operation, int code)
        {
            if (AndroidNotificationCApi.IsRequestDefect(code))
                Debug.LogError($"[{LogTag}][{operation}] the request was rejected: {AndroidNotificationCApi.ErrorName(code)}");
            else if (code == AndroidNotificationCApi.ErrorNotInitialized)
                Debug.LogWarning($"[{LogTag}][{operation}] the native library is not initialized yet");
            else
                Debug.LogWarning($"[{LogTag}][{operation}] failed: {AndroidNotificationCApi.ErrorName(code)}");
        }

        /// <summary>Which setter refused (design 4.5). The failure itself is logged once, by <see cref="Run"/>.</summary>
        private static void LogBuilderFailure(string operation, string step, int code) =>
            Debug.Log($"[{LogTag}][{operation}] the builder refused {step}: {AndroidNotificationCApi.ErrorName(code)}");

        private static long NowMillis() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        private static void Post(NotificationResult result) => AndroidNativeCallbackQueue.Post(() => Deliver(result));

        /// <summary>Raises a result on Unity's main thread, on the manager alive then; without one it is dropped (J-N3).</summary>
        private static void Deliver(NotificationResult result)
        {
            AndroidNotificationManager? manager = _instance;
            if (manager == null)
            {
                Debug.Log($"[{LogTag}][{nameof(Deliver)}] no manager; dropping the result of {result.Operation}");
                return;
            }
            try
            {
                manager.NotificationOperationCompleted?.Invoke(result);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{LogTag}][{nameof(Deliver)}] {ex.Message}");
            }
        }

        // ---- The listeners and their events (J-N12 to J-N14) ----

        /// <summary>
        /// Registers whichever of the two listeners is not registered yet. A failure is logged and that
        /// listener alone is tried again on the next call. Returns whether both are registered. The first
        /// registration of taps delivers the taps kept until then (design 5.3).
        /// </summary>
        internal static bool EnsureListeners()
        {
            Debug.Log($"[{LogTag}][{nameof(EnsureListeners)}]");
            try
            {
                lock (s_listenerLock)
                {
                    if (!s_interactionAdded)
                    {
                        int code = AddInteractionNative();
                        s_interactionAdded = code == AndroidNotificationCApi.ErrorNone;
                        if (!s_interactionAdded)
                            Debug.LogWarning($"[{LogTag}][{nameof(EnsureListeners)}] the tap listener was not added: {AndroidNotificationCApi.ErrorName(code)}");
                    }
                    if (!s_shownAdded)
                    {
                        int code = AddShownNative();
                        s_shownAdded = code == AndroidNotificationCApi.ErrorNone;
                        if (!s_shownAdded)
                            Debug.LogWarning($"[{LogTag}][{nameof(EnsureListeners)}] the shown listener was not added: {AndroidNotificationCApi.ErrorName(code)}");
                    }
                    return s_interactionAdded && s_shownAdded;
                }
            }
            catch (Exception e)
            {
                // Not expected, so an error; it too is tried again on the next call.
                Debug.LogError($"[{LogTag}][{nameof(EnsureListeners)}] {e}");
                return false;
            }
        }

        private static int AddInteractionNative()
        {
            if (s_addInteractionForTests != null) return s_addInteractionForTests();
#if UNITY_ANDROID && !UNITY_EDITOR
            return AndroidNotificationCApi.ntk_notification_add_interaction_listener(s_interactionPointer, IntPtr.Zero, IntPtr.Zero, out IntPtr _);
#else
            return AndroidNotificationCApi.ErrorNone;
#endif
        }

        private static int AddShownNative()
        {
            if (s_addShownForTests != null) return s_addShownForTests();
#if UNITY_ANDROID && !UNITY_EDITOR
            return AndroidNotificationCApi.ntk_notification_add_shown_listener(s_shownPointer, IntPtr.Zero, IntPtr.Zero, out IntPtr _);
#else
            return AndroidNotificationCApi.ErrorNone;
#endif
        }

        /// <summary>
        /// A tap, action or dismissal, on the Android main thread: queued for Unity's main thread with
        /// the action ID it reaches the app with. An unknown kind and an action without an ID are logged
        /// and dropped (J-N13).
        /// </summary>
        internal static void OnInteractionCore(int kind, int notificationId, string? actionId, IReadOnlyList<KeyValuePair<string, string>> data)
        {
            Debug.Log($"[{LogTag}][{nameof(OnInteractionCore)}] kind: {kind}, notificationId: {notificationId}, hasActionId: {!string.IsNullOrEmpty(actionId)}, data: {data.Count}");
            string? id = AndroidNotificationCApi.ActionIdOf(kind, actionId);
            if (id == null)
            {
                Debug.LogWarning($"[{LogTag}][{nameof(OnInteractionCore)}] dropping an event of kind {kind} (hasActionId: {!string.IsNullOrEmpty(actionId)})");
                return;
            }
            var result = new NotificationActionResult(id, notificationId, data);
            AndroidNativeCallbackQueue.Post(() => RaiseTap(result));
        }

        /// <summary>A scheduled notification shown, on the Android main thread (J-N14).</summary>
        internal static void OnShownCore(int notificationId, string? tag, string? channelId)
        {
            Debug.Log($"[{LogTag}][{nameof(OnShownCore)}] notificationId: {notificationId}, hasTag: {!string.IsNullOrEmpty(tag)}, channelId: {channelId}");
            var result = new NotificationReceivedResult(notificationId, AndroidNotificationCApi.ShownTag(tag), channelId ?? "");
            AndroidNativeCallbackQueue.Post(() => RaiseReceived(result));
        }

        /// <summary>
        /// A settings call completed, on the Android main thread (J-N9): OPENED and OPENED_FALLBACK are
        /// success. <paramref name="target"/> is the call's user data.
        /// </summary>
        internal static void OnSettingsCore(int target, int error, int result)
        {
            Debug.Log($"[{LogTag}][{nameof(OnSettingsCore)}] target: {target}, error: {AndroidNotificationCApi.ErrorName(error)}, result: {result}");
            Interlocked.Decrement(ref s_pendingSettings);
            s_lastSettingsResult = error == AndroidNotificationCApi.ErrorNone ? result : -1;
            string? operation = target switch
            {
                AndroidNotificationCApi.TargetNotifications => OperationOpenNotificationSettings,
                AndroidNotificationCApi.TargetAppDetails => OperationOpenAppDetailsSettings,
                AndroidNotificationCApi.TargetExactAlarm => OperationOpenExactAlarmSettings,
                _ => null,
            };
            if (operation == null)
            {
                Debug.LogError($"[{LogTag}][{nameof(OnSettingsCore)}] a settings completion for an unknown target {target}");
                return;
            }
            if (error == AndroidNotificationCApi.ErrorNone)
            {
                Debug.Log($"[{LogTag}][{nameof(OnSettingsCore)}] {operation} opened{(result == AndroidNotificationCApi.ResultOpenedFallback ? " the app's details instead" : "")}");
                Post(NotificationResult.Success(operation));
            }
            else
            {
                LogFailure(operation, error);
                Post(NotificationResult.Failure(operation, AndroidNotificationCApi.FailureMessage(operation, error)));
            }
        }

        private static void RaiseTap(NotificationActionResult result)
        {
            AndroidNotificationManager? manager = _instance;
            if (manager == null)
            {
                Debug.Log($"[{LogTag}][{nameof(RaiseTap)}] no manager; dropping a tap on {result.NotificationId}");
                return;
            }
            try
            {
                manager.NotificationActionTapped?.Invoke(result);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{LogTag}][{nameof(RaiseTap)}] {ex.Message}");
            }
        }

        private static void RaiseReceived(NotificationReceivedResult result)
        {
            AndroidNotificationManager? manager = _instance;
            if (manager == null)
            {
                Debug.Log($"[{LogTag}][{nameof(RaiseReceived)}] no manager; dropping a shown notification {result.NotificationId}");
                return;
            }
            try
            {
                manager.NotificationReceived?.Invoke(result);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{LogTag}][{nameof(RaiseReceived)}] {ex.Message}");
            }
        }

        /// <summary>
        /// Reads an event the library handed over and frees it exactly once, whether the read succeeds
        /// or throws (the receiver owns the event, <c>Notification.h</c>).
        /// </summary>
        internal static T ReadAndFree<T>(IntPtr handle, Func<IntPtr, T> read, Action<IntPtr> free)
        {
            try
            {
                return read(handle);
            }
            finally
            {
                free(handle);
            }
        }

        // ---- Test seams (design 7.2) ----

        /// <summary>Settings calls handed to the library and not completed yet.</summary>
        internal static int PendingCallCountForTests => Volatile.Read(ref s_pendingSettings);

        /// <summary>The result the last settings call completed with (0 OPENED, 1 OPENED_FALLBACK), or null.</summary>
        internal static int? LastSettingsResultForTests => s_lastSettingsResult < 0 ? null : s_lastSettingsResult;

        /// <summary>Whether the tap and the shown listeners are registered.</summary>
        internal static (bool Interaction, bool Shown) ListenersRegisteredForTests
        {
            get
            {
                lock (s_listenerLock) return (s_interactionAdded, s_shownAdded);
            }
        }

        /// <summary>Replaces the two registrations with test functions; null restores the library.</summary>
        internal static void SetNativeForTests(Func<int>? addInteraction, Func<int>? addShown)
        {
            s_addInteractionForTests = addInteraction;
            s_addShownForTests = addShown;
        }

        /// <summary>
        /// Empties the static state, as at the start of the app (SubsystemRegistration). Not for the
        /// device tests: it forgets the registered listeners, so the next call would register them again.
        /// </summary>
        internal static void ResetStaticStateForTests()
        {
            s_addInteractionForTests = null;
            s_addShownForTests = null;
            lock (s_listenerLock)
            {
                s_interactionAdded = false;
                s_shownAdded = false;
            }
            Volatile.Write(ref s_pendingSettings, 0);
            s_lastSettingsResult = -1;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        // The callbacks, kept alive for the process and handed to the library as pointers.
        private static readonly AndroidNotificationCApi.SettingsCallback s_settings = OnSettings;
        private static readonly IntPtr s_settingsPointer = Marshal.GetFunctionPointerForDelegate(s_settings);
        private static readonly AndroidNotificationCApi.InteractionCallback s_interaction = OnInteraction;
        private static readonly IntPtr s_interactionPointer = Marshal.GetFunctionPointerForDelegate(s_interaction);
        private static readonly AndroidNotificationCApi.ShownCallback s_shown = OnShown;
        private static readonly IntPtr s_shownPointer = Marshal.GetFunctionPointerForDelegate(s_shown);

        // On the Android main thread. No exception may reach the library, where it would end the process.
        [MonoPInvokeCallback(typeof(AndroidNotificationCApi.SettingsCallback))]
        private static void OnSettings(IntPtr userData, int error, uint systemCode, int result)
        {
            try
            {
                OnSettingsCore(userData.ToInt32(), error, result);
            }
            catch (Exception e)
            {
                Debug.LogError($"[{LogTag}][{nameof(OnSettings)}] {e}");
            }
        }

        [MonoPInvokeCallback(typeof(AndroidNotificationCApi.InteractionCallback))]
        private static void OnInteraction(IntPtr userData, IntPtr interaction)
        {
            try
            {
                var (kind, id, actionId, data) = ReadAndFree(interaction, AndroidNotificationCApi.ReadInteraction,
                    handle => AndroidNotificationCApi.ntk_notification_interaction_free(handle));
                OnInteractionCore(kind, id, actionId, data);
            }
            catch (Exception e)
            {
                Debug.LogError($"[{LogTag}][{nameof(OnInteraction)}] {e}");
            }
        }

        [MonoPInvokeCallback(typeof(AndroidNotificationCApi.ShownCallback))]
        private static void OnShown(IntPtr userData, IntPtr shown)
        {
            try
            {
                var (id, tag, channelId) = ReadAndFree(shown, AndroidNotificationCApi.ReadShown,
                    handle => AndroidNotificationCApi.ntk_notification_shown_free(handle));
                OnShownCore(id, tag, channelId);
            }
            catch (Exception e)
            {
                Debug.LogError($"[{LogTag}][{nameof(OnShown)}] {e}");
            }
        }
#endif
    }
}
#endif
