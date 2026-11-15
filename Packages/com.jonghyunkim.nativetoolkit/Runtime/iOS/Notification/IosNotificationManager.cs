#nullable enable

#if UNITY_IOS
namespace JonghyunKim.NativeToolkit.Runtime.iOS.Notification
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
#if !UNITY_EDITOR
    using AOT;
#endif
    using JonghyunKim.NativeToolkit.Runtime.Common;
    using JonghyunKim.NativeToolkit.Runtime.iOS.Common;
    using UnityEngine;
    using Api = IosNotificationCApi;
    using Req = IosNotificationRequests;

    /// <summary>
    /// Singleton manager for iOS native notification operations.
    /// All callbacks are dispatched to the Unity main thread via <see cref="UnityMainThreadDispatcher"/>.
    /// <para>
    /// Calls native-toolkit's C ABI 2.0.0 (<c>Notification.h</c>; artifact/ios/notification, C ABI design v4). The
    /// JSON of the public API is read as 1.x read it. Every call gets its own result, once, on a later Update than
    /// the call: the call's callback first, then <see cref="NotificationOperationCompleted"/> (1.x kept one callback
    /// per operation, so of two calls in a row only the later one got its result). Differences from 1.x:
    /// failures carry fixed English messages (1.x gave the system's generic, localized text); showing, scheduling
    /// or updating without the permission fails with "Notification permission was denied." (1.x reported success
    /// and added nothing); registering a category again replaces it; a tap that launched the app reaches the first
    /// manager; a string member of userInfo after a non-string one is no longer lost; the settings page is no longer
    /// opened by itself when the system asks for the app's notification settings; creating the manager no longer
    /// installs the delegate again (call <see cref="Initialize"/> when another component replaced it).
    /// </para>
    /// <para>
    /// The notification center delegate is installed at launch by the package's NtkNotificationInstaller.mm.
    /// <see cref="Initialize"/> installs it again (for example after another plugin asked for the permission with
    /// its own delegate); creating the manager only registers the event listeners.
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

        // The calls without an operation event (design D-2).
        internal const string OperationHasPermission = "hasPermission";
        internal const string OperationGetAuthorizationStatus = "getAuthorizationStatus";
        internal const string OperationGetScheduled = "getScheduledNotifications";
        internal const string OperationGetDelivered = "getDeliveredNotifications";

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
        /// Raised when the user taps a notification, one of its action buttons, or dismisses it (when its category
        /// asks for the dismiss action).
        /// </summary>
        public event Action<IosNotificationActionResult>? NotificationActionReceived;

        /// <summary>
        /// Raised when the user submits a text input notification action.
        /// </summary>
        public event Action<IosNotificationTextInputActionResult>? NotificationTextInputActionReceived;

        /// <summary>The error every call reports when the C ABI 2.0.0 is not available (the Editor, another version).</summary>
        internal const string UnavailableMessage = "Notification is not available yet on this build.";

        /// <summary>The per-call callbacks of the accepted calls (design D-2). Never cleared.</summary>
        internal static readonly IosNotificationPendingCalls s_pending = new();

        private static IntPtr s_interactionListener;

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
            IosNextFrameQueue.EnsurePump();
            // Only the listeners: the delegate is NtkNotificationInstaller.mm's (or the host's) to install (D-13).
            RegisterListeners();
        }

        private void OnDestroy()
        {
            Debug.Log($"[{LogTag}][{nameof(OnDestroy)}]");
            if (_instance != this) return;
            // The listeners stay for the process; events without a manager are dropped (D-12). Pending calls keep
            // their callbacks, as 1.x did (D-2).
            _instance = null;
        }

        private static bool Available
        {
            get
            {
#if UNITY_EDITOR
                return false;
#else
                return IosNativeToolkitCApi.IsAvailable;
#endif
            }
        }

        // ── Public API ───────────────────────────────────────────────────────────

        /// <summary>
        /// Installs the library's notification center delegate again (keeping the current one as the previous
        /// delegate) and registers the event listeners if they are not yet. The package installs the delegate at
        /// launch; call this after another component set its own delegate, for example after Unity's Mobile
        /// Notifications asked for the permission. Unity main thread only.
        /// </summary>
        public void Initialize()
        {
            Debug.Log($"[{LogTag}][{nameof(Initialize)}]");
            if (!Available) return;
            if (!IosNextFrameQueue.IsMainThread)
            {
                Debug.LogError($"[{LogTag}][{nameof(Initialize)}] must be called on Unity's main thread; nothing was done");
                return;
            }
#if !UNITY_EDITOR
            int error = Api.ntk_notification_install();
            if (error != Api.ErrorNone)
                Debug.LogError($"[{LogTag}][{nameof(Initialize)}] install failed: {error} (main thread only)");
#endif
            RegisterListeners();
        }

        /// <summary>
        /// Requests notification authorization from the user.
        /// </summary>
        /// <param name="onResult">Optional callback receiving the result.</param>
        public void RequestPermission(Action<IosNotificationResult>? onResult = null)
        {
            Debug.Log($"[{LogTag}][{nameof(RequestPermission)}] onResult: {onResult}");
            if (!Available)
            {
                DeferOperation(OperationRequestPermission, onResult, UnavailableMessage);
                return;
            }
#if !UNITY_EDITOR
            // 0: alert, sound and badge, as 1.x asked (D-8).
            Begin(OperationRequestPermission, onResult, id =>
                Api.ntk_notification_request_permission_async(0, s_permissionPointer, new IntPtr(id), IntPtr.Zero));
#endif
        }

        /// <summary>
        /// Checks whether the app currently has notification permission.
        /// </summary>
        /// <param name="onResult">Callback receiving the boolean result.</param>
        public void HasPermission(Action<bool> onResult)
        {
            Debug.Log($"[{LogTag}][{nameof(HasPermission)}] onResult: {onResult}");
            if (!Available)
            {
                DeferValue(OperationHasPermission, onResult, false);
                return;
            }
#if !UNITY_EDITOR
            Begin(OperationHasPermission, onResult, id =>
                Api.ntk_notification_has_permission_async(s_permissionPointer, new IntPtr(id), IntPtr.Zero));
#endif
        }

        /// <summary>
        /// Returns the current notification authorization status.
        /// </summary>
        /// <param name="onResult">Callback receiving the authorization status.</param>
        public void GetAuthorizationStatus(Action<IosNotificationAuthorizationStatus> onResult)
        {
            Debug.Log($"[{LogTag}][{nameof(GetAuthorizationStatus)}] onResult: {onResult}");
            if (!Available)
            {
                DeferValue(OperationGetAuthorizationStatus, onResult, IosNotificationAuthorizationStatus.Unknown);
                return;
            }
#if !UNITY_EDITOR
            Begin(OperationGetAuthorizationStatus, onResult, id =>
                Api.ntk_notification_get_authorization_status_async(s_statusPointer, new IntPtr(id), IntPtr.Zero));
#endif
        }

        /// <summary>
        /// Opens the app's notification settings page in the system Settings app.
        /// </summary>
        public void OpenNotificationSettings()
        {
            Debug.Log($"[{LogTag}][{nameof(OpenNotificationSettings)}]");
            if (!Available) return;
#if !UNITY_EDITOR
            CallIgnoringResult(nameof(OpenNotificationSettings), () =>
                Api.ntk_notification_open_settings_async(s_ignoredDonePointer, IntPtr.Zero, IntPtr.Zero));
#endif
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
            const string op = OperationShowNotification;
            if (!Available)
            {
                DeferOperation(op, onResult, UnavailableMessage);
                return;
            }
            if (!TryReadContentAndTrigger(op, contentJson, triggerJson, null, Api.ContentParseFailed, triggerRequired: false,
                    onResult, out Req.Content content, out Req.Trigger? trigger))
            {
                return;
            }
#if !UNITY_EDITOR
            StartWithContent(op, onResult, content, trigger, null, (c, t, _, id) =>
                Api.ntk_notification_show_async(c, t, s_donePointer, new IntPtr(id), IntPtr.Zero));
#endif
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
            const string op = OperationScheduleNotification;
            if (!Available)
            {
                DeferOperation(op, onResult, UnavailableMessage);
                return;
            }
            if (!TryReadContentAndTrigger(op, contentJson, triggerJson, identifier, Api.ContentOrTriggerParseFailed, triggerRequired: true,
                    onResult, out Req.Content content, out Req.Trigger? trigger))
            {
                return;
            }
#if !UNITY_EDITOR
            StartWithContent(op, onResult, content, trigger, identifier, (c, t, i, id) =>
                Api.ntk_notification_schedule_async(i, c, t, s_donePointer, new IntPtr(id), IntPtr.Zero));
#endif
        }

        /// <summary>
        /// Updates an existing pending notification.
        /// </summary>
        /// <param name="identifier">Identifier of the pending notification to update.</param>
        /// <param name="contentJson">JSON string with updated notification content.</param>
        /// <param name="triggerJson">JSON string with updated trigger; null (or a trigger that cannot be read) delivers the
        /// notification at once, as in 1.x.</param>
        /// <param name="onResult">Optional callback receiving the result.</param>
        public void UpdateNotification(string identifier, string contentJson, string? triggerJson = null, Action<IosNotificationResult>? onResult = null)
        {
            Debug.Log($"[{LogTag}][{nameof(UpdateNotification)}] identifier: {identifier}, contentJson: {contentJson}, triggerJson: {triggerJson}, onResult: {onResult}");
            const string op = OperationUpdateNotification;
            if (!Available)
            {
                DeferOperation(op, onResult, UnavailableMessage);
                return;
            }
            if (!TryReadContentAndTrigger(op, contentJson, triggerJson, identifier, Api.ContentParseFailed, triggerRequired: false,
                    onResult, out Req.Content content, out Req.Trigger? trigger))
            {
                return;
            }
#if !UNITY_EDITOR
            StartWithContent(op, onResult, content, trigger, identifier, (c, t, i, id) =>
                Api.ntk_notification_update_async(i, c, t, s_donePointer, new IntPtr(id), IntPtr.Zero));
#endif
        }

        /// <summary>
        /// Cancels a specific pending notification.
        /// </summary>
        /// <param name="identifier">Identifier of the notification to cancel.</param>
        public void CancelNotification(string identifier)
        {
            Debug.Log($"[{LogTag}][{nameof(CancelNotification)}] identifier: {identifier}");
            CallWithIdentifierIgnoringResult(nameof(CancelNotification), identifier, CancelScheduled);
        }

        /// <summary>
        /// Cancels all pending notifications.
        /// </summary>
        public void CancelAllNotifications()
        {
            Debug.Log($"[{LogTag}][{nameof(CancelAllNotifications)}]");
            CancelAllScheduled(nameof(CancelAllNotifications));
        }

        /// <summary>
        /// Removes a specific delivered notification from Notification Center.
        /// </summary>
        /// <param name="identifier">Identifier of the delivered notification to remove.</param>
        public void RemoveDeliveredNotification(string identifier)
        {
            Debug.Log($"[{LogTag}][{nameof(RemoveDeliveredNotification)}] identifier: {identifier}");
            CallWithIdentifierIgnoringResult(nameof(RemoveDeliveredNotification), identifier, RemoveDelivered);
        }

        /// <summary>
        /// Removes all delivered notifications from Notification Center.
        /// </summary>
        public void RemoveAllDeliveredNotifications()
        {
            Debug.Log($"[{LogTag}][{nameof(RemoveAllDeliveredNotifications)}]");
            if (!Available) return;
#if !UNITY_EDITOR
            CallIgnoringResult(nameof(RemoveAllDeliveredNotifications), () =>
                Api.ntk_notification_remove_all_async(s_ignoredDonePointer, IntPtr.Zero, IntPtr.Zero));
#endif
        }

        /// <summary>
        /// Cancels a specific scheduled notification.
        /// </summary>
        /// <param name="identifier">Identifier of the scheduled notification to cancel.</param>
        public void CancelScheduledNotification(string identifier)
        {
            Debug.Log($"[{LogTag}][{nameof(CancelScheduledNotification)}] identifier: {identifier}");
            CallWithIdentifierIgnoringResult(nameof(CancelScheduledNotification), identifier, CancelScheduled);
        }

        /// <summary>
        /// Cancels all scheduled notifications.
        /// </summary>
        public void CancelAllScheduledNotifications()
        {
            Debug.Log($"[{LogTag}][{nameof(CancelAllScheduledNotifications)}]");
            CancelAllScheduled(nameof(CancelAllScheduledNotifications));
        }

        /// <summary>
        /// Returns all pending notification requests as a JSON array string.
        /// </summary>
        /// <param name="onResult">Callback receiving the JSON array string.</param>
        public void GetScheduledNotifications(Action<string> onResult)
        {
            Debug.Log($"[{LogTag}][{nameof(GetScheduledNotifications)}] onResult: {onResult}");
            if (!Available)
            {
                DeferValue(OperationGetScheduled, onResult, "[]");
                return;
            }
#if !UNITY_EDITOR
            Begin(OperationGetScheduled, onResult, id =>
                Api.ntk_notification_get_scheduled_async(s_listPointer, new IntPtr(id), IntPtr.Zero));
#endif
        }

        /// <summary>
        /// Returns all delivered notifications as a JSON array string.
        /// </summary>
        /// <param name="onResult">Callback receiving the JSON array string.</param>
        public void GetDeliveredNotifications(Action<string> onResult)
        {
            Debug.Log($"[{LogTag}][{nameof(GetDeliveredNotifications)}] onResult: {onResult}");
            if (!Available)
            {
                DeferValue(OperationGetDelivered, onResult, "[]");
                return;
            }
#if !UNITY_EDITOR
            Begin(OperationGetDelivered, onResult, id =>
                Api.ntk_notification_get_delivered_async(s_listPointer, new IntPtr(id), IntPtr.Zero));
#endif
        }

        /// <summary>
        /// Sets the app icon badge count. Pass 0 to clear the badge. A negative count fails.
        /// </summary>
        /// <param name="count">Badge count to display on the app icon. 0 clears the badge.</param>
        /// <param name="onResult">Optional callback receiving the result.</param>
        public void SetBadgeCount(int count, Action<IosNotificationResult>? onResult = null)
        {
            Debug.Log($"[{LogTag}][{nameof(SetBadgeCount)}] count: {count}, onResult: {onResult}");
            if (!Available)
            {
                DeferOperation(OperationSetBadgeCount, onResult, UnavailableMessage);
                return;
            }
#if !UNITY_EDITOR
            Begin(OperationSetBadgeCount, onResult, id =>
                Api.ntk_notification_set_badge_count_async(count, s_donePointer, new IntPtr(id), IntPtr.Zero));
#endif
        }

        /// <summary>
        /// Registers a notification category for use with action buttons. Registering the same identifier again
        /// replaces it.
        /// </summary>
        /// <param name="categoryJson">JSON string describing the category and its actions.</param>
        /// <param name="onResult">Optional callback receiving the result.</param>
        public void RegisterCategory(string categoryJson, Action<IosNotificationResult>? onResult = null)
        {
            Debug.Log($"[{LogTag}][{nameof(RegisterCategory)}] categoryJson: {categoryJson}, onResult: {onResult}");
            const string op = OperationRegisterCategory;
            if (!Available)
            {
                DeferOperation(op, onResult, UnavailableMessage);
                return;
            }
            Req.Problem problem = Req.ReadCategory(categoryJson, out Req.Category category, out bool valueWithNul);
            if (problem != Req.Problem.None)
            {
                DeferOperation(op, onResult, problem == Req.Problem.Unreadable ? Api.CategoryParseFailed : Api.ErrorMessage(Api.ErrorInvalidParameter));
                return;
            }
            if (valueWithNul) Debug.LogWarning($"[{LogTag}] {Api.NulValueWarning}");
#if !UNITY_EDITOR
            Begin(op, onResult, id =>
            {
                var allocated = new List<IntPtr>();
                try
                {
                    IntPtr built = BuildCategory(category, allocated);
                    return Api.ntk_notification_register_category_async(built, s_donePointer, new IntPtr(id), IntPtr.Zero);
                }
                finally
                {
                    IosNativeToolkitCApi.FreeAll(allocated);
                }
            });
#endif
        }

        /// <summary>
        /// Removes a registered notification category.
        /// </summary>
        /// <param name="identifier">Identifier of the category to remove.</param>
        public void RemoveCategory(string identifier)
        {
            Debug.Log($"[{LogTag}][{nameof(RemoveCategory)}] identifier: {identifier}");
            CallWithIdentifierIgnoringResult(nameof(RemoveCategory), identifier, RemoveCategoryNative);
        }

        // ── Reading the requests (D-4, D-5, D-16) ────────────────────────────────

        private static bool TryReadContentAndTrigger(
            string op, string? contentJson, string? triggerJson, string? identifier, string unreadableMessage,
            bool triggerRequired, Action<IosNotificationResult>? onResult, out Req.Content content, out Req.Trigger? trigger)
        {
            trigger = null;
            string invalid = Api.ErrorMessage(Api.ErrorInvalidParameter);
            Req.Problem problem = Req.ReadContent(contentJson, identifier, out content, out bool valueWithNul);
            if (problem != Req.Problem.None)
            {
                DeferOperation(op, onResult, problem == Req.Problem.Unreadable ? unreadableMessage : invalid);
                return false;
            }
            // An identifier argument holding U+0000 is cut, as 1.x's bridge (const char*) cut it (D-16).

            Req.Problem triggerProblem = Req.ReadTrigger(triggerJson, out trigger);
            if (triggerProblem == Req.Problem.Invalid)
            {
                DeferOperation(op, onResult, invalid);
                return false;
            }
            if (triggerProblem == Req.Problem.Unreadable && triggerRequired)
            {
                DeferOperation(op, onResult, unreadableMessage);
                return false;
            }
            // Show and Update deliver at once when 1.x could not read the trigger (D-4).
            if (valueWithNul) Debug.LogWarning($"[{LogTag}] {Api.NulValueWarning}");
            return true;
        }

        // ── Results (D-2, D-3) ───────────────────────────────────────────────────

        /// <summary>A result decided during the call: delivered on a later frame (D-3).</summary>
        private static void DeferOperation(string op, Action<IosNotificationResult>? onResult, string error)
        {
            Debug.LogWarning($"[{LogTag}] {op}: {error}");
            IosNotificationResult result = IosNotificationResult.Failure(op, error);
            IosNextFrameQueue.Enqueue(() => InvokeOperation(result, onResult));
        }

        private static void DeferValue<T>(string op, Action<T>? onResult, T value)
        {
            IosNextFrameQueue.Enqueue(() => InvokeValue(op, onResult, value));
        }

        /// <summary>The call's callback first, then <see cref="NotificationOperationCompleted"/>, each guarded (D-3).</summary>
        internal static void InvokeOperation(IosNotificationResult result, Action<IosNotificationResult>? onResult)
        {
            try
            {
                onResult?.Invoke(result);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{LogTag}][{result.Operation}] callback: {ex.Message}");
            }

            try
            {
                _instance?.NotificationOperationCompleted?.Invoke(result);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{LogTag}][{result.Operation}] {nameof(NotificationOperationCompleted)}: {ex.Message}");
            }
        }

        internal static void InvokeValue<T>(string op, Action<T>? onResult, T value)
        {
            try
            {
                onResult?.Invoke(value);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{LogTag}][{op}] callback: {ex.Message}");
            }
        }

        /// <summary>
        /// Records the call, makes it, and turns a rejection or an exception into its failure on a later frame
        /// (D-2, D-3). The C ABI gets the call's number as <c>user_data</c> and no <c>release</c>.
        /// </summary>
        internal static void Begin(string op, Delegate? onResult, Func<long, int> call)
        {
            Debug.Log($"[{LogTag}][{nameof(Begin)}] operation: {op}");
            // A listener that could not be registered in Awake (the delegate was not installed yet) is tried again.
            if (IosNextFrameQueue.IsMainThread) RegisterListeners();
            long id = s_pending.Add(op, onResult);
            int error;
            try
            {
                error = call(id);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{LogTag}][{op}] {ex.Message}");
                error = Api.ErrorUnknown;
            }
            if (error == Api.ErrorNone) return;

            s_pending.TryTake(id, out _, out Delegate? callback);
            DeferFailure(op, callback, error);
        }

        /// <summary>The failure of a call, by its kind: the operation's failure, false, Unknown or "[]".</summary>
        private static void DeferFailure(string op, Delegate? callback, int error)
        {
            switch (callback)
            {
                case Action<bool> has:
                    DeferValue(op, has, false);
                    return;
                case Action<IosNotificationAuthorizationStatus> status:
                    DeferValue(op, status, IosNotificationAuthorizationStatus.Unknown);
                    return;
                case Action<string> list:
                    DeferValue(op, list, "[]");
                    return;
                default:
                    if (op == OperationHasPermission || op == OperationGetAuthorizationStatus ||
                        op == OperationGetScheduled || op == OperationGetDelivered)
                    {
                        return; // no callback and no event for these
                    }
                    DeferOperation(op, callback as Action<IosNotificationResult>, Api.ErrorMessage(error));
                    return;
            }
        }

        private static void CallWithIdentifierIgnoringResult(string what, string identifier, Func<IntPtr, int> call)
        {
            if (!Available) return;
            var allocated = new List<IntPtr>();
            try
            {
                IntPtr text = IosNativeToolkitCApi.AllocUtf8(identifier, allocated);
                CallIgnoringResult(what, () => call(text));
            }
            finally
            {
                IosNativeToolkitCApi.FreeAll(allocated);
            }
        }

        private static void CallIgnoringResult(string what, Func<int> call)
        {
            try
            {
                int error = call();
                if (error != Api.ErrorNone) Debug.LogWarning($"[{LogTag}][{what}] rejected: {Api.ErrorMessage(error)}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{LogTag}][{what}] {ex.Message}");
            }
        }

        private static void CancelAllScheduled(string what)
        {
            if (!Available) return;
#if !UNITY_EDITOR
            CallIgnoringResult(what, () =>
                Api.ntk_notification_cancel_all_scheduled_async(s_ignoredDonePointer, IntPtr.Zero, IntPtr.Zero));
#endif
        }

#if UNITY_EDITOR
        private static int CancelScheduled(IntPtr identifier) => Api.ErrorNotSupported;
        private static int RemoveDelivered(IntPtr identifier) => Api.ErrorNotSupported;
        private static int RemoveCategoryNative(IntPtr identifier) => Api.ErrorNotSupported;
        private static void RegisterListeners() { }
#else
        private static int CancelScheduled(IntPtr identifier) =>
            Api.ntk_notification_cancel_scheduled_async(identifier, s_ignoredDonePointer, IntPtr.Zero, IntPtr.Zero);
        private static int RemoveDelivered(IntPtr identifier) =>
            Api.ntk_notification_remove_async(identifier, s_ignoredDonePointer, IntPtr.Zero, IntPtr.Zero);
        private static int RemoveCategoryNative(IntPtr identifier) =>
            Api.ntk_notification_remove_category_async(identifier, s_ignoredDonePointer, IntPtr.Zero, IntPtr.Zero);

        /// <summary>
        /// Registers the interaction listener once for the process (D-12). No settings-request listener: the library
        /// keeps requests until the first listener, so one would open the settings page later, out of the blue. Before the delegate
        /// is installed the C ABI answers NOT_INITIALIZED; <see cref="Initialize"/> tries again.
        /// </summary>
        private static void RegisterListeners()
        {
            if (!Available) return;
            if (s_interactionListener == IntPtr.Zero)
            {
                int error = Api.ntk_notification_add_interaction_listener(s_interactionPointer, IntPtr.Zero, IntPtr.Zero, out IntPtr listener);
                if (error == Api.ErrorNone) s_interactionListener = listener;
                else Debug.LogWarning($"[{LogTag}] the interaction listener was not registered: {Api.ErrorMessage(error)}");
            }
        }

        // ── Building the requests (player only) ──────────────────────────────────

        private delegate int ContentCall(IntPtr content, IntPtr trigger, IntPtr identifier, long id);

        /// <summary>
        /// Builds the content and the trigger, makes the call and frees what it built. A builder that rejects a value
        /// fails the call without calling the operation (D-5 (c)).
        /// </summary>
        private static void StartWithContent(string op, Action<IosNotificationResult>? onResult, Req.Content content,
            Req.Trigger? trigger, string? identifier, ContentCall call)
        {
            Begin(op, onResult, id =>
            {
                var allocated = new List<IntPtr>();
                IntPtr built = IntPtr.Zero;
                try
                {
                    int error = BuildContent(content, allocated, out built);
                    if (error != Api.ErrorNone) return error;
                    IntPtr triggerPointer = trigger == null ? IntPtr.Zero : BuildTrigger(trigger, allocated);
                    IntPtr identifierPointer = identifier == null ? IntPtr.Zero : IosNativeToolkitCApi.AllocUtf8(identifier, allocated);
                    return call(built, triggerPointer, identifierPointer, id);
                }
                finally
                {
                    if (built != IntPtr.Zero) Api.ntk_notification_content_free(built);
                    IosNativeToolkitCApi.FreeAll(allocated);
                }
            });
        }

        private static int BuildContent(Req.Content c, List<IntPtr> allocated, out IntPtr content)
        {
            IntPtr Utf8(string? value) => IosNativeToolkitCApi.AllocUtf8(value, allocated);

            int error = Api.ntk_notification_content_create(Utf8(c.Id), Utf8(c.Title), out content);
            if (error != Api.ErrorNone) return error;
            IntPtr built = content;
            var steps = new List<Func<int>>();
            if (c.Subtitle != null) steps.Add(() => Api.ntk_notification_content_set_subtitle(built, Utf8(c.Subtitle)));
            if (c.Body != null) steps.Add(() => Api.ntk_notification_content_set_body(built, Utf8(c.Body)));
            if (c.Badge.HasValue) steps.Add(() => Api.ntk_notification_content_set_badge(built, c.Badge.Value));
            if (c.SoundKind != Api.SoundDefault) steps.Add(() => Api.ntk_notification_content_set_sound(built, c.SoundKind, Utf8(c.SoundName)));
            if (c.Category != null) steps.Add(() => Api.ntk_notification_content_set_category(built, Utf8(c.Category)));
            if (c.InterruptionLevel.HasValue) steps.Add(() => Api.ntk_notification_content_set_interruption_level(built, c.InterruptionLevel.Value));
            if (c.Thread != null) steps.Add(() => Api.ntk_notification_content_set_thread(built, Utf8(c.Thread)));
            if (c.TargetContent != null) steps.Add(() => Api.ntk_notification_content_set_target_content(built, Utf8(c.TargetContent)));
            if (c.RelevanceScore.HasValue) steps.Add(() => Api.ntk_notification_content_set_relevance_score(built, c.RelevanceScore.Value));
            if (c.FilterCriteria != null) steps.Add(() => Api.ntk_notification_content_set_filter_criteria(built, Utf8(c.FilterCriteria)));
            if (c.UserInfoJson != null) steps.Add(() => Api.ntk_notification_content_set_user_info_json(built, Utf8(c.UserInfoJson)));
            foreach (var attachment in c.Attachments)
                steps.Add(() => Api.ntk_notification_content_add_attachment(built, Utf8(attachment.Identifier), Utf8(attachment.FileUrl)));
            foreach (Func<int> step in steps)
            {
                error = step();
                if (error != Api.ErrorNone) return error;
            }
            return Api.ErrorNone;
        }

        private static IntPtr BuildTrigger(Req.Trigger t, List<IntPtr> allocated)
        {
            var value = new Api.Trigger
            {
                StructSize = (uint)Marshal.SizeOf<Api.Trigger>(),
                Kind = t.Kind,
                Repeats = t.Repeats ? 1 : 0,
                IntervalSeconds = t.IntervalSeconds,
                CalendarFields = t.CalendarFields,
                Year = t.Year,
                Month = t.Month,
                Day = t.Day,
                Hour = t.Hour,
                Minute = t.Minute,
                Second = t.Second,
                LocationIdentifier = IosNativeToolkitCApi.AllocUtf8(t.LocationIdentifier, allocated),
                Latitude = t.Latitude,
                Longitude = t.Longitude,
                Radius = t.Radius,
                NotNotifyOnEntry = t.NotifyOnEntry ? 0 : 1,
                NotifyOnExit = t.NotifyOnExit ? 1 : 0,
            };
            return AllocStruct(value, allocated);
        }

        private static IntPtr BuildCategory(Req.Category c, List<IntPtr> allocated)
        {
            int actionSize = Marshal.SizeOf<Api.NotificationAction>();
            IntPtr actions = IosNativeToolkitCApi.Alloc(actionSize * Math.Max(c.Actions.Count, 1), allocated);
            for (int i = 0; i < c.Actions.Count; i++)
            {
                Req.ActionItem a = c.Actions[i];
                Marshal.StructureToPtr(new Api.NotificationAction
                {
                    StructSize = (uint)actionSize,
                    Identifier = IosNativeToolkitCApi.AllocUtf8(a.Identifier, allocated),
                    Title = IosNativeToolkitCApi.AllocUtf8(a.Title, allocated),
                    SfSymbolName = IosNativeToolkitCApi.AllocUtf8(a.SfSymbolName, allocated),
                    Options = a.Options,
                }, IntPtr.Add(actions, i * actionSize), false);
            }

            int inputSize = Marshal.SizeOf<Api.TextInputAction>();
            IntPtr inputs = IosNativeToolkitCApi.Alloc(inputSize * Math.Max(c.TextInputActions.Count, 1), allocated);
            for (int i = 0; i < c.TextInputActions.Count; i++)
            {
                Req.TextInputItem a = c.TextInputActions[i];
                Marshal.StructureToPtr(new Api.TextInputAction
                {
                    StructSize = (uint)inputSize,
                    Identifier = IosNativeToolkitCApi.AllocUtf8(a.Identifier, allocated),
                    Title = IosNativeToolkitCApi.AllocUtf8(a.Title, allocated),
                    ButtonTitle = IosNativeToolkitCApi.AllocUtf8(a.ButtonTitle, allocated),
                    Placeholder = IosNativeToolkitCApi.AllocUtf8(a.Placeholder, allocated),
                }, IntPtr.Add(inputs, i * inputSize), false);
            }

            return AllocStruct(new Api.Category
            {
                StructSize = (uint)Marshal.SizeOf<Api.Category>(),
                Options = c.Options,
                Identifier = IosNativeToolkitCApi.AllocUtf8(c.Identifier, allocated),
                Actions = c.Actions.Count == 0 ? IntPtr.Zero : actions,
                ActionCount = new UIntPtr((uint)c.Actions.Count),
                TextInputActions = c.TextInputActions.Count == 0 ? IntPtr.Zero : inputs,
                TextInputActionCount = new UIntPtr((uint)c.TextInputActions.Count),
            }, allocated);
        }

        private static IntPtr AllocStruct<T>(T value, List<IntPtr> allocated) where T : struct
        {
            IntPtr pointer = IosNativeToolkitCApi.Alloc(Marshal.SizeOf<T>(), allocated);
            Marshal.StructureToPtr(value, pointer, false);
            return pointer;
        }

        // ── The completions (main thread). Kept for the life of the process, so the pointers handed to the C ABI
        // stay valid. No exception leaves; a handle is freed on every path. ──

        private static readonly Api.DoneCallback s_done = OnDone;
        internal static readonly IntPtr s_donePointer = Marshal.GetFunctionPointerForDelegate(s_done);
        private static readonly Api.DoneCallback s_ignoredDone = OnIgnoredDone;
        internal static readonly IntPtr s_ignoredDonePointer = Marshal.GetFunctionPointerForDelegate(s_ignoredDone);
        private static readonly Api.PermissionCallback s_permission = OnPermission;
        internal static readonly IntPtr s_permissionPointer = Marshal.GetFunctionPointerForDelegate(s_permission);
        private static readonly Api.StatusCallback s_status = OnStatus;
        internal static readonly IntPtr s_statusPointer = Marshal.GetFunctionPointerForDelegate(s_status);
        private static readonly Api.ListCallback s_list = OnList;
        internal static readonly IntPtr s_listPointer = Marshal.GetFunctionPointerForDelegate(s_list);
        private static readonly Api.InteractionCallback s_interaction = OnInteraction;
        internal static readonly IntPtr s_interactionPointer = Marshal.GetFunctionPointerForDelegate(s_interaction);


        [MonoPInvokeCallback(typeof(Api.DoneCallback))]
        private static void OnDone(IntPtr userData, int error, uint systemCode) =>
            Guard(nameof(OnDone), () => CompleteDone(userData.ToInt64(), error));

        [MonoPInvokeCallback(typeof(Api.DoneCallback))]
        private static void OnIgnoredDone(IntPtr userData, int error, uint systemCode)
        {
            if (error != Api.ErrorNone) SafeLog(() => Debug.LogWarning($"[{LogTag}] a call without a result failed: {Api.ErrorMessage(error)}"));
        }

        [MonoPInvokeCallback(typeof(Api.PermissionCallback))]
        private static void OnPermission(IntPtr userData, int error, uint systemCode, int granted) =>
            Guard(nameof(OnPermission), () => CompletePermission(userData.ToInt64(), error, granted));

        [MonoPInvokeCallback(typeof(Api.StatusCallback))]
        private static void OnStatus(IntPtr userData, int error, uint systemCode, int status) =>
            Guard(nameof(OnStatus), () => CompleteStatus(userData.ToInt64(), error, status));

        [MonoPInvokeCallback(typeof(Api.ListCallback))]
        private static void OnList(IntPtr userData, int error, uint systemCode, IntPtr list)
        {
            try
            {
                List<Api.ListItem>? items = null;
                if (error == Api.ErrorNone && list != IntPtr.Zero)
                {
                    try
                    {
                        items = ReadList(list);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[{LogTag}][{nameof(OnList)}] could not read the list: {ex.Message}");
                    }
                }
                CompleteList(userData.ToInt64(), error, items);
            }
            catch (Exception ex)
            {
                SafeLog(() => Debug.LogError($"[{LogTag}][{nameof(OnList)}] {ex.Message}"));
            }
            finally
            {
                if (list != IntPtr.Zero) Api.ntk_notification_list_free(list);
            }
        }

        private static void Guard(string where, Action body)
        {
            try
            {
                body();
            }
            catch (Exception ex)
            {
                SafeLog(() => Debug.LogError($"[{LogTag}][{where}] {ex.Message}"));
            }
        }

        private static string? Utf8At(IntPtr data, UIntPtr size) => IosNativeToolkitCApi.ReadUtf8OrNull(data, size);

        private static List<Api.ListItem> ReadList(IntPtr list)
        {
            ulong count = Api.ntk_notification_list_count(list).ToUInt64();
            var items = new List<Api.ListItem>((int)Math.Min(count, 4096));
            for (ulong i = 0; i < count; i++)
            {
                var index = new UIntPtr(i);
                items.Add(new Api.ListItem(
                    Utf8At(Api.ntk_notification_list_identifier_at(list, index, out UIntPtr idSize), idSize) ?? "",
                    Utf8At(Api.ntk_notification_list_title_at(list, index, out UIntPtr titleSize), titleSize) ?? "",
                    Utf8At(Api.ntk_notification_list_subtitle_at(list, index, out UIntPtr subtitleSize), subtitleSize),
                    Utf8At(Api.ntk_notification_list_body_at(list, index, out UIntPtr bodySize), bodySize),
                    Utf8At(Api.ntk_notification_list_category_at(list, index, out UIntPtr categorySize), categorySize) ?? "",
                    Api.ntk_notification_list_date_millis_at(list, index)));
            }
            return items;
        }

        /// <summary>A response to a notification: read off the handle, which is freed here; delivered by the core.</summary>
        [MonoPInvokeCallback(typeof(Api.InteractionCallback))]
        private static void OnInteraction(IntPtr userData, IntPtr interaction)
        {
            try
            {
                if (interaction == IntPtr.Zero) return;
                if (_instance == null)
                {
                    Debug.Log($"[{LogTag}] a notification response came without a manager; dropped");
                    return;
                }
                int kind = Api.ntk_notification_interaction_kind(interaction);
                DeliverInteraction(
                    kind,
                    Utf8At(Api.ntk_notification_interaction_notification_id(interaction, out UIntPtr idSize), idSize) ?? "",
                    Utf8At(Api.ntk_notification_interaction_action_id(interaction, out UIntPtr actionSize), actionSize) ?? "",
                    kind == Api.EventTextInput
                        ? Utf8At(Api.ntk_notification_interaction_user_text(interaction, out UIntPtr textSize), textSize) ?? ""
                        : null,
                    Utf8At(Api.ntk_notification_interaction_user_info_json(interaction, out UIntPtr infoSize), infoSize));
            }
            catch (Exception ex)
            {
                SafeLog(() => Debug.LogError($"[{LogTag}][{nameof(OnInteraction)}] {ex.Message}"));
            }
            finally
            {
                if (interaction != IntPtr.Zero) Api.ntk_notification_interaction_free(interaction);
            }
        }
#endif

        // ── The completion core (D-2): the values read off the handles in, the result out on a later Update. Built
        // in the Editor too, so the Editor's tests drive it. ──

        internal static void CompleteDone(long id, int error)
        {
            if (!s_pending.TryTake(id, out string op, out Delegate? callback)) return;
            Debug.Log($"[{LogTag}][{nameof(CompleteDone)}] operation: {op}, error: {error}");
            IosNotificationResult result = error == Api.ErrorNone
                ? IosNotificationResult.Success(op)
                : IosNotificationResult.Failure(op, Api.ErrorMessage(error));
            var onResult = callback as Action<IosNotificationResult>;
            IosNextFrameQueue.EnqueueBehind(() => InvokeOperation(result, onResult));
        }

        internal static void CompletePermission(long id, int error, int granted)
        {
            if (!s_pending.TryTake(id, out string op, out Delegate? callback)) return;
            Debug.Log($"[{LogTag}][{nameof(CompletePermission)}] operation: {op}, error: {error}, granted: {granted}");
            if (op == OperationHasPermission)
            {
                bool has = error == Api.ErrorNone && granted != 0;
                var onHas = callback as Action<bool>;
                IosNextFrameQueue.EnqueueBehind(() => InvokeValue(op, onHas, has));
                return;
            }
            IosNotificationResult result = error == Api.ErrorNone && granted != 0
                ? IosNotificationResult.Success(op)
                : IosNotificationResult.Failure(op, error == Api.ErrorNone ? Api.PermissionDenied : Api.ErrorMessage(error));
            var onResult = callback as Action<IosNotificationResult>;
            IosNextFrameQueue.EnqueueBehind(() => InvokeOperation(result, onResult));
        }

        internal static void CompleteStatus(long id, int error, int status)
        {
            if (!s_pending.TryTake(id, out string op, out Delegate? callback)) return;
            Debug.Log($"[{LogTag}][{nameof(CompleteStatus)}] error: {error}, status: {status}");
            IosNotificationAuthorizationStatus value = IosNotificationAuthorizationStatusParser.Parse(
                error == Api.ErrorNone ? Api.StatusName(status) : "unknown");
            var onStatus = callback as Action<IosNotificationAuthorizationStatus>;
            IosNextFrameQueue.EnqueueBehind(() => InvokeValue(op, onStatus, value));
        }

        internal static void CompleteList(long id, int error, IReadOnlyList<Api.ListItem>? items)
        {
            if (!s_pending.TryTake(id, out string op, out Delegate? callback)) return;
            Debug.Log($"[{LogTag}][{nameof(CompleteList)}] operation: {op}, error: {error}, count: {items?.Count ?? -1}");
            string json = "[]";
            if (error == Api.ErrorNone && items != null)
            {
                try
                {
                    json = Api.ListJson(items, op == OperationGetDelivered);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[{LogTag}][{nameof(CompleteList)}] could not write the list: {ex.Message}");
                }
            }
            var onList = callback as Action<string>;
            IosNextFrameQueue.EnqueueBehind(() => InvokeValue(op, onList, json));
        }

        /// <summary>
        /// A response to a notification (D-12): text input to <see cref="NotificationTextInputActionReceived"/>, a
        /// tap, an action or a dismissal to <see cref="NotificationActionReceived"/>, on a later Update. Dropped when
        /// there is no manager (as 1.x) and for a kind this version does not know.
        /// </summary>
        internal static void DeliverInteraction(int kind, string notificationId, string actionId, string? userText, string? userInfoJson)
        {
            Debug.Log($"[{LogTag}][{nameof(DeliverInteraction)}] kind: {kind}");
            if (_instance == null) return;
            switch (kind)
            {
                case Api.EventBodyTap:
                case Api.EventAction:
                case Api.EventDismiss:
                {
                    var result = new IosNotificationActionResult(notificationId, actionId, userInfoJson);
                    IosNextFrameQueue.EnqueueBehind(() => RaiseAction(result));
                    break;
                }
                case Api.EventTextInput:
                {
                    var result = new IosNotificationTextInputActionResult(notificationId, actionId, userText ?? "", userInfoJson);
                    IosNextFrameQueue.EnqueueBehind(() => RaiseTextInput(result));
                    break;
                }
                default:
                    Debug.LogWarning($"[{LogTag}] a notification response of an unknown kind ({kind}) was dropped");
                    break;
            }
        }

#if UNITY_EDITOR
        /// <summary>Records a call as an accepted one would be (the Editor makes no native call).</summary>
        internal static long RecordCallForTests(string operation, Delegate? callback) => s_pending.Add(operation, callback);
#endif

        /// <summary>Logs without letting a throwing log handler escape into the native caller.</summary>
        private static void SafeLog(Action log)
        {
            try
            {
                log();
            }
            catch
            {
                // nothing more can be done from inside a native callback
            }
        }

        private static void RaiseAction(IosNotificationActionResult result)
        {
            try
            {
                _instance?.NotificationActionReceived?.Invoke(result);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{LogTag}][{nameof(NotificationActionReceived)}] {ex.Message}");
            }
        }

        private static void RaiseTextInput(IosNotificationTextInputActionResult result)
        {
            try
            {
                _instance?.NotificationTextInputActionReceived?.Invoke(result);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{LogTag}][{nameof(NotificationTextInputActionReceived)}] {ex.Message}");
            }
        }
    }
}
#endif
