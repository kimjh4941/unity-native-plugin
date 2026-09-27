#nullable enable

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
namespace JonghyunKim.NativeToolkit.Runtime.Windows.Notification
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using AOT;
    using JonghyunKim.NativeToolkit.Runtime.Common;
    using JonghyunKim.NativeToolkit.Runtime.Windows.Common;
    using UnityEngine;

    /// <summary>
    /// Singleton manager for Windows native notification operations, over native-toolkit's C ABI 2.0.0.
    /// Every operation is synchronous: its result reaches the per-call callback and then the event
    /// within the call. Only <see cref="NotificationInvoked"/> arrives later, on the Unity main thread
    /// via <see cref="UnityMainThreadDispatcher"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// In the Unity Editor nothing reaches the native library: no call reports anything, and
    /// <see cref="GetNotificationSetting"/> is <see cref="WindowsNotificationSetting.Unknown"/>.
    /// </para>
    /// <para>
    /// Changes from native-toolkit 1.x: a clicked button now reaches a running unpackaged app;
    /// a payload key that is missing where required, of another type, or <c>null</c> is 7 (was 5);
    /// a number a double cannot hold, nesting deeper than 512, or a <c>null</c> payload is 3; a
    /// <c>timestamp</c>, <c>expiration</c> or scheduled time out of range is 7; <c>GetAllNotifications</c>
    /// no longer cuts a long list short; <c>UpdateNotificationProgress</c> sets a null
    /// <c>valueStr</c> or <c>status</c> to empty; a missing or mismatched native library is -4 from
    /// <see cref="Initialize"/> instead of an exception; other C# exceptions are 5.
    /// </para>
    /// </remarks>
    public class WindowsNotificationManager : MonoBehaviour
    {
        private const string LogTag = "WindowsNotificationManager";

        // ── Operation constants ──────────────────────────────────────────────────

        /// <summary>Operation name for Initialize.</summary>
        public const string OperationInitialize      = "initialize";
        /// <summary>Operation name for ShowNotification.</summary>
        public const string OperationShow            = "showNotification";
        /// <summary>Operation name for ScheduleNotification.</summary>
        public const string OperationSchedule        = "scheduleNotification";
        /// <summary>Operation name for CancelScheduledNotification.</summary>
        public const string OperationCancelScheduled = "cancelScheduledNotification";
        /// <summary>Operation name for UpdateNotificationProgress.</summary>
        public const string OperationUpdateProgress  = "updateNotificationProgress";
        /// <summary>Operation name for SetBadge.</summary>
        public const string OperationSetBadge        = "setBadge";
        /// <summary>Operation name for RemoveNotificationById.</summary>
        public const string OperationRemoveById      = "removeNotificationById";
        /// <summary>Operation name for RemoveNotificationsByTag.</summary>
        public const string OperationRemoveByTag     = "removeNotificationsByTag";
        /// <summary>Operation name for RemoveAllNotifications.</summary>
        public const string OperationRemoveAll       = "removeAllNotifications";
        /// <summary>Operation name for GetAllNotifications.</summary>
        public const string OperationGetAll          = "getAllNotifications";
        /// <summary>Operation name for OpenNotificationSettings.</summary>
        public const string OperationOpenSettings    = "openNotificationSettings";

        private static WindowsNotificationManager? _instance;

        /// <summary>Singleton instance. Creates and persists a new GameObject if none exists.</summary>
        public static WindowsNotificationManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    Debug.Log($"[{LogTag}] Creating new instance of WindowsNotificationManager");
                    var go = new GameObject("WindowsNotificationManager");
                    _instance = go.AddComponent<WindowsNotificationManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        // ── Events ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Raised when any operation completes (show, schedule, badge, remove, etc.), after its
        /// per-call callback. Not raised by GetAllNotifications or GetNotificationSetting.
        /// </summary>
        public event Action<WindowsNotificationResult>? NotificationOperationCompleted;

        // In the Editor these two are never raised (nothing reaches the native library, design v8
        // J-4), which the compiler reports as CS0067.
#pragma warning disable CS0067
        /// <summary>
        /// Raised when the user interacts with a notification.
        /// The argsJson string contains the merged action arguments and user input as JSON.
        /// Key structure is application-defined; parsing is the responsibility of the caller.
        /// </summary>
        public event Action<string>? NotificationInvoked;

        /// <summary>
        /// Raised when GetAllNotifications completes.
        /// The first argument is the JSON array string on success; null on failure.
        /// </summary>
        public event Action<string?, WindowsNotificationResult>? GetAllNotificationsCompleted;
#pragma warning restore CS0067

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        // ── Native state (Windows player only) ──────────────────────────────────

        /// <summary>The activation handler, kept alive for the whole process: an activation may still arrive after close.</summary>
        private static readonly WindowsNotificationCApi.InvokedCallback s_invokedCallback = OnNotificationInvoked;

        /// <summary>
        /// Taken on the main thread in Awake, so the native thread never touches
        /// <see cref="UnityMainThreadDispatcher.Instance"/>, whose getter would create a GameObject there.
        /// </summary>
        private static UnityMainThreadDispatcher? s_dispatcher;

        /// <summary>The Windows App SDK runtime, kept until OnDestroy even when creating the manager fails (design v8 J-9).</summary>
        private IntPtr _runtime;

        /// <summary>The native manager. Zero until Initialize succeeds; every operation is 1 without it.</summary>
        private IntPtr _manager;
#endif

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
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            s_dispatcher = UnityMainThreadDispatcher.Instance;
#else
            _ = UnityMainThreadDispatcher.Instance;
#endif
        }

        private void OnDestroy()
        {
            Debug.Log($"[{LogTag}][{nameof(OnDestroy)}]");
            if (_instance != this) return;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            // The manager before the runtime: the runtime unloads once no manager is open.
            try
            {
                WindowsNotificationCApi.FreeManager(_manager);
                WindowsNotificationCApi.FreeRuntime(_runtime);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[{LogTag}][{nameof(OnDestroy)}] {ex.GetType().Name}: {ex.Message}");
            }
            _manager = IntPtr.Zero;
            _runtime = IntPtr.Zero;
#endif
            _instance = null;
        }

        // ── Public API ───────────────────────────────────────────────────────────

        /// <summary>
        /// Initializes the native notification manager and registers the invoked callback.
        /// A second call while initialized succeeds without doing anything; its arguments are ignored.
        /// </summary>
        /// <param name="isPackaged">True if the app is packaged (MSIX). False for standalone Unity builds.</param>
        /// <param name="displayName">
        /// Display name shown in notifications. Required for unpackaged apps; ignored when isPackaged is true.
        /// It is the app's identity for Windows: two apps with the same name take each other's button clicks.
        /// </param>
        /// <param name="iconUri">Icon URI shown in notifications, e.g. "file:///C:/path/app.ico". Required for unpackaged apps; ignored when isPackaged is true.</param>
        /// <param name="onResult">Per-call result callback. Also fires <see cref="NotificationOperationCompleted"/>.</param>
        /// <remarks>
        /// Error 7 when an unpackaged app has no name or icon; -4 when the native library is missing,
        /// incomplete, or of another major version. Whether the Windows App SDK runtime is loaded
        /// follows the process's package identity, not <paramref name="isPackaged"/>; the packaged
        /// path has not been tested on a device.
        /// </remarks>
        public void Initialize(bool isPackaged = false, string? displayName = null, string? iconUri = null,
            Action<WindowsNotificationResult>? onResult = null)
        {
            Debug.Log($"[{LogTag}][{nameof(Initialize)}] isPackaged: {isPackaged}, displayName: {displayName}, iconUri: {iconUri}, onResult: {onResult != null}");
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (Application.platform != RuntimePlatform.WindowsPlayer) return;
            if (_manager != IntPtr.Zero)
            {
                FireResult(OperationInitialize, WindowsNotificationCApi.ErrorNone, onResult);
                return;
            }

            int code;
            try
            {
                code = WindowsNativeToolkitCApi.EnsureNativeAvailable() == WindowsNativeToolkitCApi.NativeState.Available
                    ? InitializeNative(isPackaged, displayName, iconUri)
                    : WindowsNotificationCApi.NativeUnavailable;
            }
            catch (Exception ex)
            {
                code = Caught(nameof(Initialize), ex);
            }
            FireResult(OperationInitialize, code, onResult);
#endif
        }

        /// <summary>
        /// Shows a notification immediately using the given JSON payload.
        /// </summary>
        /// <param name="jsonPayload">JSON string built by <see cref="WindowsNotificationJsonBuilder.BuildNotificationPayload"/>.</param>
        /// <param name="onResult">Per-call result callback.</param>
        /// <remarks>Checked in this order: 1 not initialized, 2 notifications off, 3 not a JSON object, 7 invalid payload.</remarks>
        public void ShowNotification(string jsonPayload, Action<WindowsNotificationResult>? onResult = null)
        {
            Debug.Log($"[{LogTag}][{nameof(ShowNotification)}] jsonPayload: {jsonPayload}, onResult: {onResult != null}");
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (Application.platform != RuntimePlatform.WindowsPlayer) return;
            FireResult(OperationShow, Deliver(nameof(ShowNotification), jsonPayload,
                (List<WindowsNotificationCApi.ContentStep> steps, out uint systemCode) => WindowsNotificationCApi.Show(_manager, steps, out systemCode)),
                onResult);
#endif
        }

        /// <summary>
        /// Schedules a notification for delivery at the specified Unix epoch time.
        /// </summary>
        /// <param name="jsonPayload">JSON string built by <see cref="WindowsNotificationJsonBuilder.BuildNotificationPayload"/>.</param>
        /// <param name="scheduledTimeUnixMs">Delivery time as Unix epoch milliseconds. Beyond about 29,000 years either side is error 7.</param>
        /// <param name="onResult">Per-call result callback.</param>
        public void ScheduleNotification(string jsonPayload, long scheduledTimeUnixMs,
            Action<WindowsNotificationResult>? onResult = null)
        {
            Debug.Log($"[{LogTag}][{nameof(ScheduleNotification)}] jsonPayload: {jsonPayload}, scheduledTimeUnixMs: {scheduledTimeUnixMs}, onResult: {onResult != null}");
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (Application.platform != RuntimePlatform.WindowsPlayer) return;
            FireResult(OperationSchedule, Deliver(nameof(ScheduleNotification), jsonPayload,
                (List<WindowsNotificationCApi.ContentStep> steps, out uint systemCode) => WindowsNotificationCApi.Schedule(_manager, steps, scheduledTimeUnixMs, out systemCode)),
                onResult);
#endif
        }

        /// <summary>
        /// Cancels a scheduled notification identified by tag and group.
        /// </summary>
        /// <param name="tag">The notification tag.</param>
        /// <param name="group">The notification group.</param>
        /// <param name="onResult">Per-call result callback.</param>
        public void CancelScheduledNotification(string tag, string group,
            Action<WindowsNotificationResult>? onResult = null)
        {
            Debug.Log($"[{LogTag}][{nameof(CancelScheduledNotification)}] tag: {tag}, group: {group}, onResult: {onResult != null}");
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (Application.platform != RuntimePlatform.WindowsPlayer) return;
            Run(OperationCancelScheduled, nameof(CancelScheduledNotification), onResult,
                manager => WindowsNotificationCApi.CancelScheduled(manager, tag, group));
#endif
        }

        /// <summary>
        /// Updates the progress bar of an existing notification.
        /// </summary>
        /// <param name="tag">The notification tag.</param>
        /// <param name="group">The notification group.</param>
        /// <param name="value">Progress value between 0.0 and 1.0.</param>
        /// <param name="valueStr">Human-readable progress string (e.g., "50%"). Null sets it empty.</param>
        /// <param name="status">Status label text. Null sets it empty.</param>
        /// <param name="sequenceNumber">Must be greater than the previous sequence number.</param>
        /// <param name="onResult">Per-call result callback.</param>
        public void UpdateNotificationProgress(string tag, string group, double value, string valueStr,
            string status, uint sequenceNumber, Action<WindowsNotificationResult>? onResult = null)
        {
            Debug.Log($"[{LogTag}][{nameof(UpdateNotificationProgress)}] tag: {tag}, group: {group}, value: {value}, valueStr: {valueStr}, status: {status}, sequenceNumber: {sequenceNumber}, onResult: {onResult != null}");
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (Application.platform != RuntimePlatform.WindowsPlayer) return;
            Run(OperationUpdateProgress, nameof(UpdateNotificationProgress), onResult,
                manager => WindowsNotificationCApi.UpdateProgress(manager, tag, group, value, valueStr, status, sequenceNumber));
#endif
        }

        /// <summary>
        /// Sets the taskbar badge. Pass a positive integer for a numeric badge, or use <see cref="WindowsBadgeValue"/> for glyphs.
        /// Not supported for unpackaged apps (error 8).
        /// </summary>
        /// <param name="value">Badge value. Positive = numeric, 0 = clear, negative = glyph (see <see cref="WindowsBadgeValue"/>).</param>
        /// <param name="onResult">Per-call result callback.</param>
        public void SetBadge(int value, Action<WindowsNotificationResult>? onResult = null)
        {
            Debug.Log($"[{LogTag}][{nameof(SetBadge)}] value: {value}, onResult: {onResult != null}");
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (Application.platform != RuntimePlatform.WindowsPlayer) return;
            // Below the lowest glyph is 7 before anything else, as in 1.x.
            int invalid = WindowsNotificationCApi.CheckBadgeValue(value);
            if (invalid != WindowsNotificationCApi.ErrorNone)
            {
                Debug.LogWarning($"[{LogTag}][{nameof(SetBadge)}] {value} is below the lowest glyph ({WindowsNotificationCApi.LowestBadgeValue})");
                FireResult(OperationSetBadge, invalid, onResult);
                return;
            }
            Run(OperationSetBadge, nameof(SetBadge), onResult, manager => WindowsNotificationCApi.SetBadge(manager, value));
#endif
        }

        /// <summary>
        /// Removes a specific notification by its ID. Not supported for unpackaged apps (error 8).
        /// </summary>
        /// <param name="notificationId">The notification ID to remove.</param>
        /// <param name="onResult">Per-call result callback.</param>
        public void RemoveNotificationById(uint notificationId, Action<WindowsNotificationResult>? onResult = null)
        {
            Debug.Log($"[{LogTag}][{nameof(RemoveNotificationById)}] notificationId: {notificationId}, onResult: {onResult != null}");
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (Application.platform != RuntimePlatform.WindowsPlayer) return;
            Run(OperationRemoveById, nameof(RemoveNotificationById), onResult,
                manager => WindowsNotificationCApi.RemoveById(manager, notificationId));
#endif
        }

        /// <summary>
        /// Removes all notifications matching the given tag and group.
        /// </summary>
        /// <param name="tag">The notification tag.</param>
        /// <param name="group">The notification group.</param>
        /// <param name="onResult">Per-call result callback.</param>
        public void RemoveNotificationsByTag(string tag, string group,
            Action<WindowsNotificationResult>? onResult = null)
        {
            Debug.Log($"[{LogTag}][{nameof(RemoveNotificationsByTag)}] tag: {tag}, group: {group}, onResult: {onResult != null}");
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (Application.platform != RuntimePlatform.WindowsPlayer) return;
            Run(OperationRemoveByTag, nameof(RemoveNotificationsByTag), onResult,
                manager => WindowsNotificationCApi.RemoveByTag(manager, tag, group));
#endif
        }

        /// <summary>
        /// Removes all notifications from Action Center.
        /// </summary>
        /// <param name="onResult">Per-call result callback.</param>
        public void RemoveAllNotifications(Action<WindowsNotificationResult>? onResult = null)
        {
            Debug.Log($"[{LogTag}][{nameof(RemoveAllNotifications)}] onResult: {onResult != null}");
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (Application.platform != RuntimePlatform.WindowsPlayer) return;
            Run(OperationRemoveAll, nameof(RemoveAllNotifications), onResult, WindowsNotificationCApi.RemoveAll);
#endif
        }

        /// <summary>
        /// Retrieves all current notifications as a JSON array string,
        /// <c>[{"id":N,"tag":"…","group":"…"}]</c>. Not supported for unpackaged apps (error 8).
        /// </summary>
        /// <param name="onResult">
        /// Per-call result callback. The first argument is the JSON array string on success; null on failure.
        /// Also fires <see cref="GetAllNotificationsCompleted"/>, not <see cref="NotificationOperationCompleted"/>.
        /// </param>
        public void GetAllNotifications(Action<string?, WindowsNotificationResult>? onResult = null)
        {
            Debug.Log($"[{LogTag}][{nameof(GetAllNotifications)}] onResult: {onResult != null}");
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (Application.platform != RuntimePlatform.WindowsPlayer) return;

            string? json = null;
            int code;
            try
            {
                if (_manager == IntPtr.Zero)
                {
                    code = WindowsNotificationCApi.ErrorNotInitialized;
                }
                else
                {
                    code = WindowsNotificationCApi.GetAll(_manager, out List<WindowsNotificationCApi.ListedNotification>? notifications, out uint systemCode);
                    if (code == WindowsNotificationCApi.ErrorNone) json = WindowsNotificationCApi.BuildGetAllJson(notifications!);
                    else Debug.LogWarning($"[{LogTag}][{nameof(GetAllNotifications)}] error {code}, system code 0x{systemCode:X8}");
                }
            }
            catch (Exception ex)
            {
                code = Caught(nameof(GetAllNotifications), ex);
            }

            var result = WindowsNotificationCApi.ToResult(OperationGetAll, code);
            try
            {
                onResult?.Invoke(json, result);
                GetAllNotificationsCompleted?.Invoke(json, result);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{LogTag}][{nameof(GetAllNotifications)}] {ex.Message}");
            }
#endif
        }

        /// <summary>
        /// Returns the current notification permission setting as a <see cref="WindowsNotificationSetting"/> enum.
        /// This is a synchronous special API that does not use the result/event contract.
        /// </summary>
        /// <returns>The current notification setting, or <see cref="WindowsNotificationSetting.Unknown"/> before Initialize, on error, and in the Editor.</returns>
        public WindowsNotificationSetting GetNotificationSetting()
        {
            Debug.Log($"[{LogTag}][{nameof(GetNotificationSetting)}]");
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (Application.platform != RuntimePlatform.WindowsPlayer || _manager == IntPtr.Zero)
                return WindowsNotificationSetting.Unknown;
            try
            {
                int error = WindowsNotificationCApi.GetSetting(_manager, out int raw);
                return WindowsNotificationCApi.SettingFrom(error, raw);
            }
            catch (Exception ex)
            {
                Caught(nameof(GetNotificationSetting), ex);
                return WindowsNotificationSetting.Unknown;
            }
#else
            return WindowsNotificationSetting.Unknown;
#endif
        }

        /// <summary>
        /// Opens the Windows notification settings page (ms-settings:notifications).
        /// </summary>
        /// <param name="onResult">Per-call result callback.</param>
        public void OpenNotificationSettings(Action<WindowsNotificationResult>? onResult = null)
        {
            Debug.Log($"[{LogTag}][{nameof(OpenNotificationSettings)}] onResult: {onResult != null}");
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (Application.platform != RuntimePlatform.WindowsPlayer) return;
            Run(OperationOpenSettings, nameof(OpenNotificationSettings), onResult, WindowsNotificationCApi.OpenSettings);
#endif
        }

        // ── Internal helpers ─────────────────────────────────────────────────────

        /// <summary>
        /// Reports a result as 1.x did: the per-call callback, then the event, in one try. A callback
        /// that throws is logged, and the event does not come.
        /// </summary>
        private void FireResult(string operation, int code, Action<WindowsNotificationResult>? perCallCallback)
        {
            var result = WindowsNotificationCApi.ToResult(operation, code);
            try
            {
                perCallCallback?.Invoke(result);
                NotificationOperationCompleted?.Invoke(result);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{LogTag}][{nameof(FireResult)}] operation: {operation}, ex: {ex.Message}");
            }
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        /// <summary>
        /// The runtime (when this process has no package identity and none is loaded yet), then the
        /// manager. A runtime that loaded is kept even when the manager fails (design v8 J-9).
        /// </summary>
        private int InitializeNative(bool isPackaged, string? displayName, string? iconUri)
        {
            if (_runtime == IntPtr.Zero)
            {
                int identity = WindowsNotificationCApi.QueryPackageIdentity();
                switch (WindowsNotificationCApi.ClassifyPackage(identity))
                {
                    case WindowsNotificationCApi.PackageIdentity.Unknown:
                        Debug.LogWarning($"[{LogTag}][{nameof(Initialize)}] GetCurrentPackageFullName returned {identity}; cannot tell whether to load the Windows App SDK runtime");
                        return WindowsNotificationCApi.ErrorHresultFailure;
                    case WindowsNotificationCApi.PackageIdentity.Unpackaged:
                        int bootstrap = WindowsNotificationCApi.InitializeRuntime(out IntPtr runtime, out uint bootstrapCode);
                        if (bootstrap != WindowsNotificationCApi.ErrorNone)
                        {
                            // As in 1.x, the one failure logged as an error.
                            Debug.LogError($"[{LogTag}][{nameof(Initialize)}] runtime initialize failed. error: {bootstrap}, system code: 0x{bootstrapCode:X8}");
                            return bootstrap;
                        }
                        _runtime = runtime;
                        break;
                }
            }

            int created = WindowsNotificationCApi.CreateManager(
                Marshal.GetFunctionPointerForDelegate(s_invokedCallback), isPackaged, displayName, iconUri,
                out IntPtr manager, out uint systemCode);
            if (created != WindowsNotificationCApi.ErrorNone)
            {
                Debug.LogWarning($"[{LogTag}][{nameof(Initialize)}] manager create failed. error: {created}, system code: 0x{systemCode:X8}");
                return created;
            }
            _manager = manager;
            return WindowsNotificationCApi.ErrorNone;
        }

        /// <summary>
        /// Show and Schedule: 1 without a manager, 2 when notifications are off (both before the JSON,
        /// as in 1.x, design v8 J-11), 3 or 7 for the payload, then the native call.
        /// </summary>
        private int Deliver(string method, string jsonPayload, ContentDelivery deliver)
        {
            try
            {
                bool hasManager = _manager != IntPtr.Zero;
                int settingError = WindowsNotificationCApi.ErrorNone;
                int setting = 0;
                if (hasManager) settingError = WindowsNotificationCApi.GetSetting(_manager, out setting);
                int gate = WindowsNotificationCApi.ShowGate(hasManager, settingError, setting);
                if (gate != WindowsNotificationCApi.ErrorNone)
                {
                    Debug.LogWarning($"[{LogTag}][{method}] error {gate} (setting error {settingError}, setting {setting})");
                    return gate;
                }

                int planned = WindowsNotificationCApi.PlanContent(jsonPayload, out List<WindowsNotificationCApi.ContentStep> steps);
                if (planned != WindowsNotificationCApi.ErrorNone)
                {
                    Debug.LogWarning($"[{LogTag}][{method}] the payload is refused with {planned} ({(planned == WindowsNotificationCApi.ErrorInvalidPayload ? "not a JSON object" : "a key missing, of another type, or null")})");
                    return planned;
                }

                int error = deliver(steps, out uint systemCode);
                if (error != WindowsNotificationCApi.ErrorNone)
                    Debug.LogWarning($"[{LogTag}][{method}] error {error}, system code 0x{systemCode:X8}");
                return error;
            }
            catch (Exception ex)
            {
                return Caught(method, ex);
            }
        }

        /// <summary>The native show or schedule of built content.</summary>
        private delegate int ContentDelivery(List<WindowsNotificationCApi.ContentStep> steps, out uint systemCode);

        /// <summary>An operation on the manager: 1 without one, else the native result.</summary>
        private void Run(string operation, string method, Action<WindowsNotificationResult>? onResult, Func<IntPtr, int> call)
        {
            int code;
            try
            {
                code = _manager == IntPtr.Zero ? WindowsNotificationCApi.ErrorNotInitialized : call(_manager);
                if (code != WindowsNotificationCApi.ErrorNone)
                    Debug.LogWarning($"[{LogTag}][{method}] error {code}");
            }
            catch (Exception ex)
            {
                code = Caught(method, ex);
            }
            FireResult(operation, code, onResult);
        }

        /// <summary>The code for an exception from the native side: -4 when the library could not be used, else 5.</summary>
        private static int Caught(string method, Exception ex)
        {
            int code = WindowsNotificationCApi.FromException(ex);
            Debug.LogWarning($"[{LogTag}][{method}] {ex.GetType().Name}: {ex.Message} (error {code})");
            return code;
        }

        // ── Static AOT callbacks ──────────────────────────────────────────────────

        /// <summary>
        /// Runs on a thread the OS picks. Copies the arguments while the activation is alive and
        /// queues the event for the main thread; never lets an exception back into the native
        /// library, and never closes or waits on the manager from here.
        /// </summary>
        [MonoPInvokeCallback(typeof(WindowsNotificationCApi.InvokedCallback))]
        private static void OnNotificationInvoked(IntPtr userData, IntPtr activation)
        {
            try
            {
                string arguments = WindowsNotificationCApi.ReadActivationArguments(activation);
                UnityMainThreadDispatcher? dispatcher = s_dispatcher;
                // Unity's == reads whether the dispatcher was destroyed; for a MonoBehaviour that is
                // a field read, safe on this thread.
                if (dispatcher == null)
                {
                    Debug.LogWarning($"[{LogTag}][{nameof(OnNotificationInvoked)}] no dispatcher; the activation is dropped");
                    return;
                }
                dispatcher.Enqueue(() =>
                    WindowsNotificationCApi.DeliverInvoked(_instance != null ? _instance.NotificationInvoked : null, arguments));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{LogTag}][{nameof(OnNotificationInvoked)}] {ex.Message}");
            }
        }
#endif
    }
}
#endif
