#nullable enable

// Class guard: compiled in the editor as well, so the public API and its EditMode tests build on
// any active build target. The native boundary sits behind a second, narrower guard below.
#if UNITY_STANDALONE_WIN || UNITY_EDITOR
namespace JonghyunKim.NativeToolkit.Runtime.Clipboard
{
    using System;
    using System.Collections;
    using System.Runtime.InteropServices;
    using System.Threading;
    using AOT;
    using JonghyunKim.NativeToolkit.Runtime.Common;
    using UnityEngine;

    /// <summary>
    /// Lifecycle state of <see cref="WindowsClipboardManager"/>.
    /// </summary>
    internal enum WindowsClipboardManagerState
    {
        /// <summary>Initialize has not succeeded yet.</summary>
        Uninitialized,

        /// <summary>Initialized and accepting operations.</summary>
        Running,

        /// <summary>A shutdown is in progress; new operations are rejected.</summary>
        Draining,

        /// <summary>Shutdown completed and the native resources were released.</summary>
        ShutDown,

        /// <summary>Shutdown hit a terminal failure or ran out of budget; native resources are kept.</summary>
        ShutdownFailed
    }

    /// <summary>
    /// How one shutdown attempt ended.
    /// </summary>
    internal enum WindowsClipboardShutdownProgress
    {
        /// <summary>The native manager reported that it finished.</summary>
        Completed,

        /// <summary>Not finished yet. Pump messages and call again.</summary>
        NotYet,

        /// <summary>Calling again cannot help.</summary>
        Terminal
    }

    /// <summary>
    /// Which COM reference this layer owns on the Unity main thread.
    /// </summary>
    internal enum WindowsClipboardComOwnership
    {
        /// <summary>This layer initialized nothing and must release nothing.</summary>
        None,

        /// <summary>CoInitializeEx returned S_OK: this layer established the apartment.</summary>
        Initialized,

        /// <summary>CoInitializeEx returned S_FALSE: this layer only added a reference.</summary>
        RefCounted
    }

    /// <summary>
    /// Singleton manager for the Windows clipboard.
    /// Wraps the Clipboard bridge exported by the native toolkit DLL.
    /// <para>
    /// Every public API is Unity main thread only. A synchronous API returns its result directly;
    /// events and per-call callbacks are delivered outside the caller's stack through
    /// <see cref="UnityMainThreadDispatcher"/>, which can still be within the same frame.
    /// </para>
    /// <para>
    /// This file currently carries the lifecycle only. The clipboard operations themselves are
    /// added in the following steps of the design's build order.
    /// </para>
    /// </summary>
    public class WindowsClipboardManager : MonoBehaviour
    {
        private const string LogTag = "WindowsClipboardManager";

        // The package's PreBuildProcessor copies the DLL out of native-toolkit/dist and renames it
        // per build configuration, so a development build only ever holds the "-debug" name.
#if DEVELOPMENT_BUILD
        private const string DLL_NAME = "unity-windows-native-toolkit-debug";
#else
        private const string DLL_NAME = "unity-windows-native-toolkit";
#endif

        // ── Operation constants ──────────────────────────────────────────────────

        /// <summary>Operation name for Initialize.</summary>
        public const string OperationInitialize = "initClipboardManager";

        /// <summary>Operation name for the shutdown APIs.</summary>
        public const string OperationShutdown = "uninitClipboardManager";

        /// <summary>Operation name for CanShutdownNow.</summary>
        public const string OperationCanShutdown = "canDestroyClipboardManager";

        // ── Singleton ────────────────────────────────────────────────────────────

        private static WindowsClipboardManager? _instance;

        /// <summary>
        /// Singleton instance. Creates and persists a new GameObject if none exists.
        /// <para>
        /// Unity main thread only: the getter may create a GameObject, which cannot be done from a
        /// background thread. It does not check <see cref="IsTerminated"/> either; an instance
        /// recreated after destruction rejects every operation instead.
        /// </para>
        /// </summary>
        public static WindowsClipboardManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    Debug.Log($"[{LogTag}] Creating new instance of WindowsClipboardManager");
                    var go = new GameObject("WindowsClipboardManager");
                    _instance = go.AddComponent<WindowsClipboardManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        /// <summary>
        /// Whether the manager has been destroyed. Every operation is rejected from that point on,
        /// and a recreated instance does not clear it.
        /// </summary>
        public static bool IsTerminated => s_isTerminated;

        // ── Events ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Raised when an operation that carries no payload completes, on success and failure
        /// alike. Always invoked before the per-call callback.
        /// </summary>
        public event Action<WindowsClipboardResult>? ClipboardOperationCompleted;

        /// <summary>Raised when a flag query such as CanShutdownNow completes.</summary>
        public event Action<WindowsClipboardFlagResult>? FlagChecked;

        /// <summary>
        /// Raised when the clipboard contents change in another process.
        /// <para>
        /// One external copy can raise this more than once: placing several formats, and the
        /// Windows clipboard history service re-writing the content, each advance the clipboard
        /// sequence number. Treat it as "something changed" and debounce rather than counting.
        /// Writes made through this manager do not raise it.
        /// </para>
        /// </summary>
        public event Action? ClipboardChanged;

        // ── Static state (main thread only) ──────────────────────────────────────

        // Captured on the main thread in Awake so dispatching never touches
        // UnityMainThreadDispatcher.Instance, whose getter creates a GameObject.
        private static UnityMainThreadDispatcher? s_dispatcher;
        private static int s_mainThreadId;
        private static bool s_isTerminated;

        private static WindowsClipboardManagerState s_state = WindowsClipboardManagerState.Uninitialized;
        private static WindowsClipboardComOwnership s_comOwnership = WindowsClipboardComOwnership.None;
        private static bool s_quitHandlerSubscribed;
        private static bool s_quitDrainStarted;
        private static bool s_quitDrainCompleted;

        // ── Shutdown budget ──────────────────────────────────────────────────────

        private const int ShutdownRetryFrameBudget = 60;
        private const float ShutdownRetrySecondBudget = 2f;

        // ── Test seams ───────────────────────────────────────────────────────────

#if UNITY_EDITOR
        /// <summary>Replaces Application.Quit so a test can count the resumed quits.</summary>
        internal static Action? QuitActionForTests;

        /// <summary>How often the COM reference this layer owned was released.</summary>
        internal static int ComReleaseCountForTests;

        /// <summary>Current lifecycle state, for assertions.</summary>
        internal static WindowsClipboardManagerState StateForTests => s_state;

        /// <summary>Current COM ownership, for assertions.</summary>
        internal static WindowsClipboardComOwnership ComOwnershipForTests => s_comOwnership;

        /// <summary>Whether the quit handler is subscribed, for the idempotent-Initialize test.</summary>
        internal static bool QuitHandlerSubscribedForTests => s_quitHandlerSubscribed;

        /// <summary>Drives the state machine without the native bridge.</summary>
        internal static void InjectShutdownResultForTests(bool completed, WindowsClipboardErrorCode code)
        {
            WindowsClipboardResult result = completed
                ? WindowsClipboardResult.Success(OperationShutdown)
                : WindowsClipboardResult.Failure(OperationShutdown, code);
            FinishShutdownAttempt(ShutdownOrigin.PublicApi, result, completed);
        }

        /// <summary>Puts the manager into a state a test needs without touching the native side.</summary>
        internal static void SetStateForTests(WindowsClipboardManagerState state) => s_state = state;

        /// <summary>
        /// Raises ClipboardChanged through the same dispatcher path the native callback uses.
        /// The editor never reaches that callback, so without this seam the event could not be
        /// covered at all.
        /// </summary>
        internal static void InjectClipboardChangedForTests() => RaiseClipboardChanged();

        /// <summary>Records that this layer owns a COM reference, for the release tests.</summary>
        internal static void SetComOwnershipForTests(WindowsClipboardComOwnership ownership) =>
            s_comOwnership = ownership;
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

            s_mainThreadId = Thread.CurrentThread.ManagedThreadId;
            s_dispatcher = UnityMainThreadDispatcher.Instance;

            if (s_isTerminated)
            {
                Debug.LogError($"[{LogTag}][{nameof(Awake)}] Recreated after destruction; all operations are rejected.");
            }
        }

        private void OnDestroy()
        {
            Debug.Log($"[{LogTag}][{nameof(OnDestroy)}]");

            // A duplicate instance destroys itself in Awake. Letting its OnDestroy run the teardown
            // would shut down the live singleton.
            if (_instance != this) return;

            // Set the tombstone before any native call: a P/Invoke below can throw, and a partially
            // applied teardown would leave the manager recreatable with late callbacks undiscarded.
            s_isTerminated = true;

            UnsubscribeQuitHandler();

            if (s_state == WindowsClipboardManagerState.Running ||
                s_state == WindowsClipboardManagerState.Draining ||
                s_state == WindowsClipboardManagerState.ShutdownFailed)
            {
                WindowsClipboardResult result = TryShutdownCore(out bool completed);
                FinishShutdownAttempt(ShutdownOrigin.Destroy, result, completed);
                if (!completed)
                {
                    Debug.LogWarning(
                        $"[{LogTag}][{nameof(OnDestroy)}] shutdown did not complete; native resources are retained. " +
                        $"result: {result.ErrorCode}");
                }
            }

            _instance = null;
            // s_dispatcher is deliberately left set: post-destruction rejections still need it.
        }

        // ── Public API: initialization ───────────────────────────────────────────

        /// <summary>
        /// Initializes the native clipboard manager on the Unity main thread, which becomes the
        /// owner UI thread the native layer requires.
        /// </summary>
        /// <param name="enableChangeEvents">
        /// Whether to register for <see cref="ClipboardChanged"/>. When true, a failure to register
        /// the OS listener fails the whole initialization; when false, that failure is swallowed by
        /// the native layer and the event never fires, with no way to enable it short of shutting
        /// down and initializing again.
        /// </param>
        /// <param name="onResult">Per-call callback. The common event fires as well.</param>
        /// <returns>The result, also delivered to the event and the callback.</returns>
        public WindowsClipboardResult Initialize(
            bool enableChangeEvents = true, Action<WindowsClipboardResult>? onResult = null)
        {
            Debug.Log($"[{LogTag}][{nameof(Initialize)}] enableChangeEvents: {enableChangeEvents}, onResult: {onResult != null}");

            // Step 0: an idempotent Initialize must not touch COM or the native side, because the
            // apartment probe would then overwrite an ownership record this layer still has to
            // release at shutdown.
            if (s_state == WindowsClipboardManagerState.Running)
            {
                return Deliver(WindowsClipboardResult.Success(OperationInitialize), onResult);
            }
            if (s_state == WindowsClipboardManagerState.Draining ||
                s_state == WindowsClipboardManagerState.ShutdownFailed)
            {
                return Deliver(
                    WindowsClipboardResult.Failure(OperationInitialize, WindowsClipboardErrorCode.ShuttingDown),
                    onResult);
            }

            if (!IsMainThread())
            {
                return Deliver(
                    WindowsClipboardResult.Failure(OperationInitialize, WindowsClipboardErrorCode.MainThreadRequired),
                    onResult);
            }
            if (s_isTerminated)
            {
                return Deliver(
                    WindowsClipboardResult.Failure(OperationInitialize, WindowsClipboardErrorCode.ManagerDestroyed),
                    onResult);
            }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (Application.platform != RuntimePlatform.WindowsPlayer)
            {
                return Deliver(
                    WindowsClipboardResult.Failure(OperationInitialize, WindowsClipboardErrorCode.PlatformUnavailable),
                    onResult);
            }

            WindowsClipboardResult apartment = EnsureStaApartment();
            if (!apartment.IsSuccess) return Deliver(apartment, onResult);

            int pError;
            try
            {
                initClipboardManager(enableChangeEvents ? s_changedDelegate : null, out pError);
            }
            catch (Exception ex) when (ex is DllNotFoundException || ex is EntryPointNotFoundException)
            {
                Debug.LogError($"[{LogTag}][{nameof(Initialize)}] {ex.GetType().Name}: {ex.Message}");
                // The native side holds nothing when the bridge could not be reached, so the COM
                // reference this layer took is safe to give back right away.
                ReleaseOwnedComReference();
                return Deliver(
                    WindowsClipboardResult.Failure(OperationInitialize, WindowsClipboardErrorCode.BridgeUnavailable),
                    onResult);
            }

            WindowsClipboardResult result = WindowsClipboardResult.FromNative(OperationInitialize, pError);
            if (!result.IsSuccess)
            {
                // The native layer rolls itself back to uninitialized on a failed init, so nothing
                // there depends on the apartment any more.
                ReleaseOwnedComReference();
                return Deliver(result, onResult);
            }

            s_state = WindowsClipboardManagerState.Running;
            SubscribeQuitHandler();
            return Deliver(result, onResult);
#else
            return Deliver(
                WindowsClipboardResult.Failure(OperationInitialize, WindowsClipboardErrorCode.PlatformUnavailable),
                onResult);
#endif
        }

        // ── Public API: shutdown ─────────────────────────────────────────────────

        /// <summary>
        /// Runs one shutdown attempt, matching the shape of the native API.
        /// <para>
        /// Deliberately fires neither the common event nor a callback: the drain helper calls this
        /// once per frame, and every attempt would otherwise reach every subscriber.
        /// </para>
        /// </summary>
        /// <param name="completed">True when the native manager finished releasing everything.</param>
        /// <returns>The attempt's result. While completed is false this describes progress, not failure.</returns>
        public WindowsClipboardResult TryShutdown(out bool completed)
        {
            Debug.Log($"[{LogTag}][{nameof(TryShutdown)}] state: {s_state}");

            if (!IsMainThread())
            {
                completed = false;
                return WindowsClipboardResult.Failure(OperationShutdown, WindowsClipboardErrorCode.MainThreadRequired);
            }
            if (s_state == WindowsClipboardManagerState.Uninitialized ||
                s_state == WindowsClipboardManagerState.ShutDown)
            {
                // The native uninit is idempotent, so there is nothing to do and nothing to report.
                completed = true;
                return WindowsClipboardResult.Success(OperationShutdown);
            }

            WindowsClipboardResult result = TryShutdownCore(out completed);
            FinishShutdownAttempt(ShutdownOrigin.PublicApi, result, completed);
            return result;
        }

        /// <summary>
        /// Shuts the native manager down across frames, pumping messages between attempts.
        /// <para>
        /// The native contract asks the caller to keep calling until it reports completion, which
        /// cannot be done inside one frame. This is not a synchronous API turned asynchronous: it
        /// is a loop over the synchronous <see cref="TryShutdown"/>. Spinning instead would block
        /// the very message pump the native side is waiting on.
        /// </para>
        /// </summary>
        /// <param name="onResult">Per-call callback for the final outcome. The common event fires too.</param>
        public void ShutdownWithDrain(Action<WindowsClipboardResult>? onResult = null)
        {
            Debug.Log($"[{LogTag}][{nameof(ShutdownWithDrain)}] state: {s_state}, onResult: {onResult != null}");

            if (!IsMainThread())
            {
                Deliver(
                    WindowsClipboardResult.Failure(OperationShutdown, WindowsClipboardErrorCode.MainThreadRequired),
                    onResult);
                return;
            }
            if (s_state == WindowsClipboardManagerState.Uninitialized ||
                s_state == WindowsClipboardManagerState.ShutDown)
            {
                Deliver(WindowsClipboardResult.Success(OperationShutdown), onResult);
                return;
            }

            StartCoroutine(DrainRoutine(ShutdownOrigin.Drain, onResult));
        }

        /// <summary>
        /// Asks the native layer whether a shutdown could complete right now.
        /// This is a state query: it does not promise that the next shutdown succeeds.
        /// </summary>
        /// <param name="onResult">Per-call callback. <see cref="FlagChecked"/> fires as well.</param>
        /// <returns>The result, also delivered to the event and the callback.</returns>
        public WindowsClipboardFlagResult CanShutdownNow(Action<WindowsClipboardFlagResult>? onResult = null)
        {
            Debug.Log($"[{LogTag}][{nameof(CanShutdownNow)}] state: {s_state}, onResult: {onResult != null}");

            if (!IsMainThread())
            {
                return DeliverFlag(
                    WindowsClipboardFlagResult.Failure(OperationCanShutdown, WindowsClipboardErrorCode.MainThreadRequired),
                    onResult);
            }
            if (s_isTerminated)
            {
                return DeliverFlag(
                    WindowsClipboardFlagResult.Failure(OperationCanShutdown, WindowsClipboardErrorCode.ManagerDestroyed),
                    onResult);
            }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            try
            {
                bool canDestroy = canDestroyClipboardManager(out int pError);
                WindowsClipboardFlagResult result = pError == 0
                    ? WindowsClipboardFlagResult.Success(OperationCanShutdown, canDestroy)
                    : WindowsClipboardFlagResult.Failure(OperationCanShutdown, (WindowsClipboardErrorCode)pError);
                return DeliverFlag(result, onResult);
            }
            catch (Exception ex) when (ex is DllNotFoundException || ex is EntryPointNotFoundException)
            {
                Debug.LogError($"[{LogTag}][{nameof(CanShutdownNow)}] {ex.GetType().Name}: {ex.Message}");
                return DeliverFlag(
                    WindowsClipboardFlagResult.Failure(OperationCanShutdown, WindowsClipboardErrorCode.BridgeUnavailable),
                    onResult);
            }
#else
            return DeliverFlag(
                WindowsClipboardFlagResult.Failure(OperationCanShutdown, WindowsClipboardErrorCode.PlatformUnavailable),
                onResult);
#endif
        }

        // ── Shutdown internals ───────────────────────────────────────────────────

        /// <summary>
        /// Where a shutdown attempt came from. The origin decides who hears about the outcome and
        /// whether a pending quit has to be resumed.
        /// </summary>
        private enum ShutdownOrigin
        {
            PublicApi,
            Drain,
            Destroy,
            Quit
        }

        /// <summary>
        /// One native shutdown attempt, bypassing the guards that the ordinary APIs apply.
        /// <para>
        /// The tombstone and the Draining state both reject ordinary operations, and the shutdown
        /// paths run precisely when those hold, so routing them through the ordinary pre-checks
        /// would stop the shutdown from ever reaching the native side.
        /// </para>
        /// </summary>
        private WindowsClipboardResult TryShutdownCore(out bool completed)
        {
            if (!IsMainThread())
            {
                completed = false;
                return WindowsClipboardResult.Failure(OperationShutdown, WindowsClipboardErrorCode.MainThreadRequired);
            }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            try
            {
                completed = uninitClipboardManager(out int pError);
                return WindowsClipboardResult.FromNative(OperationShutdown, pError);
            }
            catch (Exception ex) when (ex is DllNotFoundException || ex is EntryPointNotFoundException)
            {
                Debug.LogError($"[{LogTag}][{nameof(TryShutdownCore)}] {ex.GetType().Name}: {ex.Message}");
                completed = false;
                return WindowsClipboardResult.Failure(OperationShutdown, WindowsClipboardErrorCode.BridgeUnavailable);
            }
#else
            completed = false;
            return WindowsClipboardResult.Failure(OperationShutdown, WindowsClipboardErrorCode.PlatformUnavailable);
#endif
        }

        /// <summary>
        /// The single place every shutdown attempt ends, whatever started it.
        /// Advances the state, drains the request registry once, releases what may be released, and
        /// resumes a pending quit.
        /// </summary>
        private static void FinishShutdownAttempt(
            ShutdownOrigin origin, WindowsClipboardResult result, bool completed)
        {
            // The first attempt closes the door before anything else. Draining the registry after
            // that means a callback cannot start a new operation that would outlive the shutdown.
            if (s_state == WindowsClipboardManagerState.Running)
            {
                s_state = WindowsClipboardManagerState.Draining;
                DrainRequestRegistry();
            }

            WindowsClipboardShutdownProgress progress = ClassifyShutdown(completed, result.ErrorCode);
            switch (progress)
            {
                case WindowsClipboardShutdownProgress.Completed:
                    ReleaseOwnedComReference();
                    s_state = WindowsClipboardManagerState.ShutDown;
                    break;
                case WindowsClipboardShutdownProgress.NotYet:
                    s_state = WindowsClipboardManagerState.Draining;
                    break;
                default:
                    Debug.LogError(
                        $"[{LogTag}][{nameof(FinishShutdownAttempt)}] terminal shutdown failure. " +
                        $"origin: {origin}, error: {result.ErrorCode}. Native resources are retained.");
                    s_state = WindowsClipboardManagerState.ShutdownFailed;
                    break;
            }

            if (origin == ShutdownOrigin.Quit)
            {
                ResumeQuit(progress, result);
            }
        }

        /// <summary>
        /// Classifies one shutdown attempt.
        /// <para>
        /// While the native manager is still winding down it reports the reason as an error code,
        /// which describes progress rather than failure. WrongThread is the exception: no number of
        /// retries from the wrong thread will ever complete.
        /// </para>
        /// </summary>
        /// <param name="completed">The native return value.</param>
        /// <param name="code">The native error code from the same call.</param>
        /// <returns>Whether to stop, retry, or give up.</returns>
        internal static WindowsClipboardShutdownProgress ClassifyShutdown(
            bool completed, WindowsClipboardErrorCode code)
        {
            if (completed) return WindowsClipboardShutdownProgress.Completed;

            return code switch
            {
                WindowsClipboardErrorCode.None => WindowsClipboardShutdownProgress.NotYet,
                WindowsClipboardErrorCode.Busy => WindowsClipboardShutdownProgress.NotYet,
                WindowsClipboardErrorCode.MonitorRegisterFailed => WindowsClipboardShutdownProgress.NotYet,
                WindowsClipboardErrorCode.Canceled => WindowsClipboardShutdownProgress.NotYet,
                WindowsClipboardErrorCode.PartialState => WindowsClipboardShutdownProgress.NotYet,
                _ => WindowsClipboardShutdownProgress.Terminal
            };
        }

        private IEnumerator DrainRoutine(ShutdownOrigin origin, Action<WindowsClipboardResult>? onResult)
        {
            int attempts = 0;
            float deadline = Time.realtimeSinceStartup + ShutdownRetrySecondBudget;
            WindowsClipboardResult result = WindowsClipboardResult.Success(OperationShutdown);
            bool completed = false;

            while (attempts < ShutdownRetryFrameBudget && Time.realtimeSinceStartup < deadline)
            {
                attempts++;
                result = TryShutdownCore(out completed);
                WindowsClipboardShutdownProgress progress = ClassifyShutdown(completed, result.ErrorCode);
                if (progress != WindowsClipboardShutdownProgress.NotYet) break;

                Debug.Log($"[{LogTag}][{nameof(DrainRoutine)}] attempt {attempts} not finished yet: {result.ErrorCode}");
                yield return null;
            }

            if (!completed && ClassifyShutdown(completed, result.ErrorCode) == WindowsClipboardShutdownProgress.NotYet)
            {
                Debug.LogError($"[{LogTag}][{nameof(DrainRoutine)}] shutdown exceeded its budget after {attempts} attempts.");
                result = WindowsClipboardResult.Failure(OperationShutdown, WindowsClipboardErrorCode.ShutdownTimeout);
            }

            FinishShutdownAttempt(origin, result, completed);
            if (origin == ShutdownOrigin.Drain) Deliver(result, onResult);
        }

        private static void DrainRequestRegistry()
        {
            // The request registry arrives with the asynchronous history APIs, which are not part
            // of this step. The drain hook is placed here so every shutdown origin already passes
            // through the one point that will own it.
        }

        // ── Quit handling ────────────────────────────────────────────────────────

        private void SubscribeQuitHandler()
        {
            // The native init is idempotent, so Initialize can succeed repeatedly. Without this
            // flag the same handler would pile up and a single unsubscribe would not remove it.
            if (s_quitHandlerSubscribed) return;
            Application.wantsToQuit += OnWantsToQuit;
            s_quitHandlerSubscribed = true;
        }

        private static void UnsubscribeQuitHandler()
        {
            if (!s_quitHandlerSubscribed) return;
            Application.wantsToQuit -= OnWantsToQuit;
            s_quitHandlerSubscribed = false;
        }

        /// <summary>
        /// Holds the quit back until the native manager has shut down.
        /// <para>
        /// Formats reserved for delayed rendering are materialized while the native window is being
        /// destroyed, which only happens from the shutdown path. A process that simply exits drops
        /// them.
        /// </para>
        /// </summary>
        private static bool OnWantsToQuit()
        {
            Debug.Log($"[{LogTag}][{nameof(OnWantsToQuit)}] started: {s_quitDrainStarted}, completed: {s_quitDrainCompleted}");

            if (s_quitDrainCompleted) return true;
            if (s_quitDrainStarted) return false;

            s_quitDrainStarted = true;
            if (_instance == null)
            {
                // Nothing can run a coroutine, so let the quit through rather than hanging.
                s_quitDrainCompleted = true;
                return true;
            }

            _instance.StartCoroutine(_instance.DrainRoutine(ShutdownOrigin.Quit, null));
            return false;
        }

        /// <summary>
        /// Lets the quit proceed after a drain, whatever its outcome.
        /// A shutdown that failed must not leave the application unable to exit.
        /// </summary>
        private static void ResumeQuit(WindowsClipboardShutdownProgress progress, WindowsClipboardResult result)
        {
            if (progress != WindowsClipboardShutdownProgress.Completed)
            {
                Debug.LogError(
                    $"[{LogTag}][{nameof(ResumeQuit)}] quitting with an incomplete shutdown: {result.ErrorCode}");
            }

            s_quitDrainCompleted = true;
#if UNITY_EDITOR
            if (QuitActionForTests != null)
            {
                QuitActionForTests.Invoke();
                return;
            }
#endif
            Application.Quit();
        }

        // ── COM apartment ────────────────────────────────────────────────────────

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        /// <summary>
        /// Makes sure the calling thread is an STA, which the native manager requires.
        /// On a Unity Windows player the main thread already is one, so the fallback below is a
        /// defensive path rather than the normal one.
        /// </summary>
        private static WindowsClipboardResult EnsureStaApartment()
        {
            int hr = CoGetApartmentType(out ApartmentType apartment, out _);
            bool isSta = hr == S_OK &&
                         (apartment == ApartmentType.Sta || apartment == ApartmentType.MainSta);
            if (isSta)
            {
                Debug.Log($"[{LogTag}][{nameof(EnsureStaApartment)}] apartment: {apartment}");
                // Never downgrade an ownership this layer already holds: it still has to be
                // released at shutdown.
                return WindowsClipboardResult.Success(OperationInitialize);
            }

            int initHr = CoInitializeEx(IntPtr.Zero, COINIT_APARTMENTTHREADED);
            if (initHr == S_OK || initHr == S_FALSE)
            {
                if (s_comOwnership == WindowsClipboardComOwnership.None)
                {
                    s_comOwnership = initHr == S_OK
                        ? WindowsClipboardComOwnership.Initialized
                        : WindowsClipboardComOwnership.RefCounted;
                }
                Debug.Log($"[{LogTag}][{nameof(EnsureStaApartment)}] initialized an STA. ownership: {s_comOwnership}");
                return WindowsClipboardResult.Success(OperationInitialize);
            }

            Debug.LogError($"[{LogTag}][{nameof(EnsureStaApartment)}] CoInitializeEx failed. hr: 0x{initHr:X8}");
            return WindowsClipboardResult.Failure(
                OperationInitialize, WindowsClipboardErrorCode.ApartmentUnavailable);
        }
#endif

        /// <summary>
        /// Gives back the COM reference this layer took, if it took one.
        /// Must run on the thread that acquired it, after the native side has let go.
        /// </summary>
        private static void ReleaseOwnedComReference()
        {
            if (s_comOwnership == WindowsClipboardComOwnership.None) return;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            CoUninitialize();
#endif
            s_comOwnership = WindowsClipboardComOwnership.None;
#if UNITY_EDITOR
            ComReleaseCountForTests++;
#endif
        }

        // ── Result delivery ──────────────────────────────────────────────────────

        /// <summary>
        /// Invokes the common event and then the per-call callback, isolating each from the other's
        /// exceptions so one bad subscriber cannot swallow the other's result.
        /// </summary>
        /// <typeparam name="TResult">The result type being delivered.</typeparam>
        /// <param name="result">The result to hand out.</param>
        /// <param name="common">The common event's invocation list, or null.</param>
        /// <param name="perCall">The per-call callback, or null.</param>
        internal static void InvokeInOrder<TResult>(
            TResult result, Action<TResult>? common, Action<TResult>? perCall)
        {
            try
            {
                common?.Invoke(result);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{LogTag}][{nameof(InvokeInOrder)}] a common event subscriber threw: {ex.Message}");
            }

            try
            {
                perCall?.Invoke(result);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{LogTag}][{nameof(InvokeInOrder)}] a per-call callback threw: {ex.Message}");
            }
        }

        private static WindowsClipboardResult Deliver(
            WindowsClipboardResult result, Action<WindowsClipboardResult>? onResult)
        {
            Dispatch(result, _instance?.ClipboardOperationCompleted, onResult);
            return result;
        }

        private static WindowsClipboardFlagResult DeliverFlag(
            WindowsClipboardFlagResult result, Action<WindowsClipboardFlagResult>? onResult)
        {
            Dispatch(result, _instance?.FlagChecked, onResult);
            return result;
        }

        private static void Dispatch<TResult>(
            TResult result, Action<TResult>? common, Action<TResult>? perCall)
        {
            UnityMainThreadDispatcher? dispatcher = s_dispatcher;
            if (dispatcher == null)
            {
                Debug.LogError($"[{LogTag}][{nameof(Dispatch)}] no dispatcher; result dropped.");
                return;
            }
            dispatcher.Enqueue(() => InvokeInOrder(result, common, perCall));
        }

        /// <summary>
        /// Hands a clipboard change to the subscribers through the dispatcher.
        /// Shared by the native callback and the editor seam so both take the same path.
        /// </summary>
        private static void RaiseClipboardChanged()
        {
            UnityMainThreadDispatcher? dispatcher = s_dispatcher;
            if (dispatcher == null) return;
            dispatcher.Enqueue(() =>
            {
                try
                {
                    _instance?.ClipboardChanged?.Invoke();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[{LogTag}][{nameof(RaiseClipboardChanged)}] subscriber threw: {ex.Message}");
                }
            });
        }

        private static bool IsMainThread() =>
            s_mainThreadId == 0 || Thread.CurrentThread.ManagedThreadId == s_mainThreadId;

        // ── Static reset ─────────────────────────────────────────────────────────

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState() => ResetCore(keepInstance: true);

#if UNITY_EDITOR
        /// <summary>
        /// Clears every static, including the tombstone and the instance, so a test starts fresh.
        /// </summary>
        internal static void ResetForTests() => ResetCore(keepInstance: false);
#endif

        private static void ResetCore(bool keepInstance)
        {
            UnsubscribeQuitHandler();

            s_state = WindowsClipboardManagerState.Uninitialized;
            s_comOwnership = WindowsClipboardComOwnership.None;
            s_quitDrainStarted = false;
            s_quitDrainCompleted = false;
            s_isTerminated = false;

#if UNITY_EDITOR
            QuitActionForTests = null;
            ComReleaseCountForTests = 0;
#endif

            if (keepInstance && _instance != null)
            {
                // With domain reloading disabled the statics are wiped while the manager object
                // survives, so the captured main thread and dispatcher have to be taken again
                // rather than dropped along with the instance.
                s_mainThreadId = Thread.CurrentThread.ManagedThreadId;
                s_dispatcher = UnityMainThreadDispatcher.Instance;
                return;
            }

            s_mainThreadId = 0;
            s_dispatcher = null;
            _instance = null;
        }

        // ── Native boundary ──────────────────────────────────────────────────────
        // Everything below is compiled only for the Windows player: the editor never resolves the
        // DLL, so a missing plugin cannot turn into a DllNotFoundException during editor tests.

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private const int S_OK = 0;
        private const int S_FALSE = 1;
        private const uint COINIT_APARTMENTTHREADED = 0x2;

        private enum ApartmentType
        {
            Current = -1,
            Sta = 0,
            Mta = 1,
            Na = 2,
            MainSta = 3
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void ClipboardChangedCallback();

        // Held in a static field for the lifetime of the manager: the native side keeps the
        // pointer until uninit reports completion, and a collected delegate would crash it.
        private static readonly ClipboardChangedCallback s_changedDelegate = OnClipboardChangedNative;

        [MonoPInvokeCallback(typeof(ClipboardChangedCallback))]
        private static void OnClipboardChangedNative()
        {
            try
            {
                RaiseClipboardChanged();
            }
            catch (Exception ex)
            {
                // Nothing may cross the C ABI boundary.
                Debug.LogError($"[{LogTag}][{nameof(OnClipboardChangedNative)}] {ex.Message}");
            }
        }

        [DllImport("ole32.dll")]
        private static extern int CoGetApartmentType(out ApartmentType pAptType, out int pAptQualifier);

        [DllImport("ole32.dll")]
        private static extern int CoInitializeEx(IntPtr reserved, uint coInit);

        [DllImport("ole32.dll")]
        private static extern void CoUninitialize();

        [DllImport(DLL_NAME, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true)]
        private static extern void initClipboardManager(ClipboardChangedCallback? onChanged, out int pError);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool uninitClipboardManager(out int pError);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool canDestroyClipboardManager(out int pError);
#endif
    }
}
#endif
