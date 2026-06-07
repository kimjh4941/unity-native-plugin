#nullable enable

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
namespace JonghyunKim.NativeToolkit.Runtime.Notification
{
    using System;
    using System.Runtime.InteropServices;
    using AOT;
    using JonghyunKim.NativeToolkit.Runtime.Common;
    using UnityEngine;

    /// <summary>
    /// Singleton manager for Windows native notification operations.
    /// Wraps the native WindowsNotificationManager DLL via P/Invoke.
    /// All callbacks are dispatched to the Unity main thread via <see cref="UnityMainThreadDispatcher"/>.
    /// </summary>
    public class WindowsNotificationManager : MonoBehaviour
    {
        private const string LogTag = "WindowsNotificationManager";

#if DEVELOPMENT_BUILD
        private const string DLL_NAME = "unity-windows-native-toolkit-debug";
#else
        private const string DLL_NAME = "unity-windows-native-toolkit";
#endif

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
        /// Raised when any pError-based operation completes (show, schedule, badge, remove, etc.).
        /// </summary>
        public event Action<WindowsNotificationResult>? NotificationOperationCompleted;

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

        // ── Delegate types (IL2CPP / AOT safe) ──────────────────────────────────

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void NotificationInvokedCallback(
            [MarshalAs(UnmanagedType.LPWStr)] string argsJson);

        // ── DllImport declarations ───────────────────────────────────────────────

        [DllImport(DLL_NAME)]
        private static extern void initWinAppSdk(uint majorMinorVersion, out int pError);

        [DllImport(DLL_NAME, CharSet = CharSet.Unicode)]
        private static extern void initNotificationManager(
            NotificationInvokedCallback callback,
            [MarshalAs(UnmanagedType.Bool)] bool isPackaged,
            [MarshalAs(UnmanagedType.LPWStr)] string? displayName,
            [MarshalAs(UnmanagedType.LPWStr)] string? iconUri,
            out int pError);

        [DllImport(DLL_NAME)]
        private static extern void uninitNotificationManager();

        [DllImport(DLL_NAME, CharSet = CharSet.Unicode)]
        private static extern void showNotification(
            [MarshalAs(UnmanagedType.LPWStr)] string jsonPayload, out int pError);

        [DllImport(DLL_NAME, CharSet = CharSet.Unicode)]
        private static extern void scheduleNotification(
            [MarshalAs(UnmanagedType.LPWStr)] string jsonPayload,
            long scheduledTimeUnixMs, out int pError);

        [DllImport(DLL_NAME, CharSet = CharSet.Unicode)]
        private static extern void cancelScheduledNotification(
            [MarshalAs(UnmanagedType.LPWStr)] string tag,
            [MarshalAs(UnmanagedType.LPWStr)] string group, out int pError);

        [DllImport(DLL_NAME, CharSet = CharSet.Unicode)]
        private static extern void updateNotificationProgress(
            [MarshalAs(UnmanagedType.LPWStr)] string tag,
            [MarshalAs(UnmanagedType.LPWStr)] string group,
            double value,
            [MarshalAs(UnmanagedType.LPWStr)] string valueStr,
            [MarshalAs(UnmanagedType.LPWStr)] string status,
            uint sequenceNumber, out int pError);

        [DllImport(DLL_NAME)]
        private static extern void setBadge(int value, out int pError);

        [DllImport(DLL_NAME)]
        private static extern void removeNotificationById(uint notificationId, out int pError);

        [DllImport(DLL_NAME, CharSet = CharSet.Unicode)]
        private static extern void removeNotificationsByTag(
            [MarshalAs(UnmanagedType.LPWStr)] string tag,
            [MarshalAs(UnmanagedType.LPWStr)] string group, out int pError);

        [DllImport(DLL_NAME)]
        private static extern void removeAllNotifications(out int pError);

        [DllImport(DLL_NAME, CharSet = CharSet.Unicode)]
        private static extern void getAllNotifications(
            IntPtr outJson, uint bufferSize, out int pError);

        [DllImport(DLL_NAME)]
        private static extern int getNotificationSetting();

        [DllImport(DLL_NAME)]
        private static extern void openNotificationSettings(out int pError);

        // ── Static delegate storage (GC prevention) ──────────────────────────────

        private static readonly NotificationInvokedCallback s_persistentInvokedDelegate = OnNotificationInvoked;

        // Per-call user callbacks
        private static Action<WindowsNotificationResult>? s_onInitialize;
        private static Action<WindowsNotificationResult>? s_onShow;
        private static Action<WindowsNotificationResult>? s_onSchedule;
        private static Action<WindowsNotificationResult>? s_onCancelScheduled;
        private static Action<WindowsNotificationResult>? s_onUpdateProgress;
        private static Action<WindowsNotificationResult>? s_onSetBadge;
        private static Action<WindowsNotificationResult>? s_onRemoveById;
        private static Action<WindowsNotificationResult>? s_onRemoveByTag;
        private static Action<WindowsNotificationResult>? s_onRemoveAll;
        private static Action<string?, WindowsNotificationResult>? s_onGetAll;
        private static Action<WindowsNotificationResult>? s_onOpenSettings;

        // ── Buffer constants ─────────────────────────────────────────────────────

        private const uint DefaultBufferSize = 4096;
        private const uint MaxBufferSize     = 65536;

        // ── State ────────────────────────────────────────────────────────────────

        private bool _initialized;

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
        }

        private void OnDestroy()
        {
            Debug.Log($"[{LogTag}][{nameof(OnDestroy)}]");
            if (_instance != this) return;
            if (_initialized)
            {
                uninitNotificationManager();
                _initialized = false;
            }
            _instance = null;
        }

        // ── Public API ───────────────────────────────────────────────────────────

        /// <summary>
        /// Initializes the native notification manager and registers the invoked callback.
        /// </summary>
        /// <param name="isPackaged">True if the app is packaged (MSIX). False for standalone Unity builds.</param>
        /// <param name="displayName">Display name shown in notifications. Required for unpackaged apps; ignored when isPackaged is true.</param>
        /// <param name="iconUri">Icon URI shown in notifications, e.g. "file:///C:/path/app.ico". Optional for unpackaged apps; ignored when isPackaged is true.</param>
        /// <param name="onResult">Per-call result callback. Also fires <see cref="NotificationOperationCompleted"/>.</param>
        public void Initialize(bool isPackaged = false, string? displayName = null, string? iconUri = null,
            Action<WindowsNotificationResult>? onResult = null)
        {
            Debug.Log($"[{LogTag}][{nameof(Initialize)}] isPackaged: {isPackaged}, displayName: {displayName}, iconUri: {iconUri}, onResult: {onResult != null}");
            s_onInitialize = onResult;
            if (Application.platform != RuntimePlatform.WindowsPlayer) return;
            initWinAppSdk(0x00010007, out int bootstrapError);
            if (bootstrapError != 0)
            {
                Debug.LogError($"[{LogTag}][{nameof(Initialize)}] initWinAppSdk failed. error: {bootstrapError}");
                FireResult(OperationInitialize, bootstrapError, s_onInitialize);
                s_onInitialize = null;
                return;
            }
            initNotificationManager(s_persistentInvokedDelegate, isPackaged, displayName, iconUri, out int pError);
            if (pError == 0) _initialized = true;
            FireResult(OperationInitialize, pError, s_onInitialize);
            s_onInitialize = null;
        }

        /// <summary>
        /// Shows a notification immediately using the given JSON payload.
        /// </summary>
        /// <param name="jsonPayload">JSON string built by <see cref="WindowsNotificationJsonBuilder.BuildNotificationPayload"/>.</param>
        /// <param name="onResult">Per-call result callback.</param>
        public void ShowNotification(string jsonPayload, Action<WindowsNotificationResult>? onResult = null)
        {
            Debug.Log($"[{LogTag}][{nameof(ShowNotification)}] jsonPayload: {jsonPayload}, onResult: {onResult != null}");
            s_onShow = onResult;
            if (Application.platform != RuntimePlatform.WindowsPlayer) return;
            showNotification(jsonPayload, out int pError);
            FireResult(OperationShow, pError, s_onShow);
            s_onShow = null;
        }

        /// <summary>
        /// Schedules a notification for delivery at the specified Unix epoch time.
        /// </summary>
        /// <param name="jsonPayload">JSON string built by <see cref="WindowsNotificationJsonBuilder.BuildNotificationPayload"/>.</param>
        /// <param name="scheduledTimeUnixMs">Delivery time as Unix epoch milliseconds.</param>
        /// <param name="onResult">Per-call result callback.</param>
        public void ScheduleNotification(string jsonPayload, long scheduledTimeUnixMs,
            Action<WindowsNotificationResult>? onResult = null)
        {
            Debug.Log($"[{LogTag}][{nameof(ScheduleNotification)}] jsonPayload: {jsonPayload}, scheduledTimeUnixMs: {scheduledTimeUnixMs}, onResult: {onResult != null}");
            s_onSchedule = onResult;
            if (Application.platform != RuntimePlatform.WindowsPlayer) return;
            scheduleNotification(jsonPayload, scheduledTimeUnixMs, out int pError);
            FireResult(OperationSchedule, pError, s_onSchedule);
            s_onSchedule = null;
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
            s_onCancelScheduled = onResult;
            if (Application.platform != RuntimePlatform.WindowsPlayer) return;
            cancelScheduledNotification(tag, group, out int pError);
            FireResult(OperationCancelScheduled, pError, s_onCancelScheduled);
            s_onCancelScheduled = null;
        }

        /// <summary>
        /// Updates the progress bar of an existing notification.
        /// </summary>
        /// <param name="tag">The notification tag.</param>
        /// <param name="group">The notification group.</param>
        /// <param name="value">Progress value between 0.0 and 1.0.</param>
        /// <param name="valueStr">Human-readable progress string (e.g., "50%").</param>
        /// <param name="status">Status label text.</param>
        /// <param name="sequenceNumber">Must be greater than the previous sequence number.</param>
        /// <param name="onResult">Per-call result callback.</param>
        public void UpdateNotificationProgress(string tag, string group, double value, string valueStr,
            string status, uint sequenceNumber, Action<WindowsNotificationResult>? onResult = null)
        {
            Debug.Log($"[{LogTag}][{nameof(UpdateNotificationProgress)}] tag: {tag}, group: {group}, value: {value}, valueStr: {valueStr}, status: {status}, sequenceNumber: {sequenceNumber}, onResult: {onResult != null}");
            s_onUpdateProgress = onResult;
            if (Application.platform != RuntimePlatform.WindowsPlayer) return;
            updateNotificationProgress(tag, group, value, valueStr, status, sequenceNumber, out int pError);
            FireResult(OperationUpdateProgress, pError, s_onUpdateProgress);
            s_onUpdateProgress = null;
        }

        /// <summary>
        /// Sets the taskbar badge. Pass a positive integer for a numeric badge, or use <see cref="WindowsBadgeValue"/> for glyphs.
        /// </summary>
        /// <param name="value">Badge value. Positive = numeric, 0 = clear, negative = glyph (see <see cref="WindowsBadgeValue"/>).</param>
        /// <param name="onResult">Per-call result callback.</param>
        public void SetBadge(int value, Action<WindowsNotificationResult>? onResult = null)
        {
            Debug.Log($"[{LogTag}][{nameof(SetBadge)}] value: {value}, onResult: {onResult != null}");
            s_onSetBadge = onResult;
            if (Application.platform != RuntimePlatform.WindowsPlayer) return;
            setBadge(value, out int pError);
            FireResult(OperationSetBadge, pError, s_onSetBadge);
            s_onSetBadge = null;
        }

        /// <summary>
        /// Removes a specific notification by its ID.
        /// </summary>
        /// <param name="notificationId">The notification ID to remove.</param>
        /// <param name="onResult">Per-call result callback.</param>
        public void RemoveNotificationById(uint notificationId, Action<WindowsNotificationResult>? onResult = null)
        {
            Debug.Log($"[{LogTag}][{nameof(RemoveNotificationById)}] notificationId: {notificationId}, onResult: {onResult != null}");
            s_onRemoveById = onResult;
            if (Application.platform != RuntimePlatform.WindowsPlayer) return;
            removeNotificationById(notificationId, out int pError);
            FireResult(OperationRemoveById, pError, s_onRemoveById);
            s_onRemoveById = null;
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
            s_onRemoveByTag = onResult;
            if (Application.platform != RuntimePlatform.WindowsPlayer) return;
            removeNotificationsByTag(tag, group, out int pError);
            FireResult(OperationRemoveByTag, pError, s_onRemoveByTag);
            s_onRemoveByTag = null;
        }

        /// <summary>
        /// Removes all notifications from Action Center.
        /// </summary>
        /// <param name="onResult">Per-call result callback.</param>
        public void RemoveAllNotifications(Action<WindowsNotificationResult>? onResult = null)
        {
            Debug.Log($"[{LogTag}][{nameof(RemoveAllNotifications)}] onResult: {onResult != null}");
            s_onRemoveAll = onResult;
            if (Application.platform != RuntimePlatform.WindowsPlayer) return;
            removeAllNotifications(out int pError);
            FireResult(OperationRemoveAll, pError, s_onRemoveAll);
            s_onRemoveAll = null;
        }

        /// <summary>
        /// Retrieves all current notifications as a JSON array string.
        /// Automatically retries with a larger buffer if the initial buffer is insufficient.
        /// </summary>
        /// <param name="onResult">
        /// Per-call result callback. The first argument is the JSON array string on success; null on failure.
        /// Also fires <see cref="GetAllNotificationsCompleted"/>.
        /// </param>
        public void GetAllNotifications(Action<string?, WindowsNotificationResult>? onResult = null)
        {
            Debug.Log($"[{LogTag}][{nameof(GetAllNotifications)}] onResult: {onResult != null}");
            s_onGetAll = onResult;
            if (Application.platform != RuntimePlatform.WindowsPlayer) return;

            string? json = GetAllNotificationsInternal(out int pError);
            var result = pError == 0
                ? WindowsNotificationResult.Success(OperationGetAll)
                : WindowsNotificationResult.Failure(OperationGetAll, pError);
            try
            {
                onResult?.Invoke(json, result);
                GetAllNotificationsCompleted?.Invoke(json, result);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{LogTag}][{nameof(GetAllNotifications)}] {ex.Message}");
            }
            s_onGetAll = null;
        }

        /// <summary>
        /// Returns the current notification permission setting as a <see cref="WindowsNotificationSetting"/> enum.
        /// This is a synchronous special API that does not use the result/event contract.
        /// </summary>
        /// <returns>The current notification setting, or <see cref="WindowsNotificationSetting.Unknown"/> on error.</returns>
        public WindowsNotificationSetting GetNotificationSetting()
        {
            Debug.Log($"[{LogTag}][{nameof(GetNotificationSetting)}]");
            if (Application.platform != RuntimePlatform.WindowsPlayer)
                return WindowsNotificationSetting.Unknown;
            int raw = getNotificationSetting();
            return Enum.IsDefined(typeof(WindowsNotificationSetting), raw)
                ? (WindowsNotificationSetting)raw
                : WindowsNotificationSetting.Unknown;
        }

        /// <summary>
        /// Opens the Windows notification settings page (ms-settings:notifications).
        /// </summary>
        /// <param name="onResult">Per-call result callback.</param>
        public void OpenNotificationSettings(Action<WindowsNotificationResult>? onResult = null)
        {
            Debug.Log($"[{LogTag}][{nameof(OpenNotificationSettings)}] onResult: {onResult != null}");
            s_onOpenSettings = onResult;
            if (Application.platform != RuntimePlatform.WindowsPlayer) return;
            openNotificationSettings(out int pError);
            FireResult(OperationOpenSettings, pError, s_onOpenSettings);
            s_onOpenSettings = null;
        }

        // ── Internal helpers ─────────────────────────────────────────────────────

        private string? GetAllNotificationsInternal(out int pError)
        {
            uint bufferSize = DefaultBufferSize;
            while (bufferSize <= MaxBufferSize)
            {
                // bufferSize is in wchar_t units; AllocHGlobal requires bytes (* 2)
                IntPtr buf = Marshal.AllocHGlobal((int)bufferSize * 2);
                try
                {
                    getAllNotifications(buf, bufferSize, out pError);
                    if (pError == 0)
                        return Marshal.PtrToStringUni(buf);
                    if (pError == 5) // NOTIFICATION_ERROR_HRESULT_FAILURE: possible buffer overflow
                    {
                        bufferSize *= 2;
                        continue;
                    }
                    return null;
                }
                finally
                {
                    Marshal.FreeHGlobal(buf);
                }
            }
            pError = 5;
            return null;
        }

        private void FireResult(string operation, int pError, Action<WindowsNotificationResult>? perCallCallback)
        {
            var result = pError == 0
                ? WindowsNotificationResult.Success(operation)
                : WindowsNotificationResult.Failure(operation, pError);
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

        // ── Static AOT callbacks ──────────────────────────────────────────────────

        [MonoPInvokeCallback(typeof(NotificationInvokedCallback))]
        private static void OnNotificationInvoked(string argsJson)
        {
            try
            {
                UnityMainThreadDispatcher.Instance.Enqueue(() =>
                {
                    try
                    {
                        _instance?.NotificationInvoked?.Invoke(argsJson);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[{LogTag}][{nameof(OnNotificationInvoked)}] {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{LogTag}][{nameof(OnNotificationInvoked)}] {ex.Message}");
            }
        }
    }
}
#endif
