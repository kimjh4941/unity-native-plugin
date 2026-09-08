#nullable enable

// Class guard: compiled in the editor as well, so the public API and its EditMode tests build on
// any active build target. The native boundary sits behind a second, narrower guard below.
#if UNITY_STANDALONE_WIN || UNITY_EDITOR
namespace JonghyunKim.NativeToolkit.Runtime.Clipboard
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
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
    /// Which result type an asynchronous request completes with.
    /// </summary>
    internal enum WindowsClipboardRequestKind
    {
        /// <summary>GetHistory, which yields a list of items.</summary>
        History,

        /// <summary>GetHistoryAvailability, which yields two flags.</summary>
        Availability,

        /// <summary>An operation that only reports success or failure.</summary>
        Status
    }

    /// <summary>
    /// What the first call of a two-call read should lead to.
    /// </summary>
    internal enum WindowsClipboardReadDecision
    {
        /// <summary>The clipboard holds nothing for this format. A success, not a failure.</summary>
        EmptySuccess,

        /// <summary>Allocate the reported size and call again.</summary>
        NeedsBuffer,

        /// <summary>Report the error. Never normalize this to an empty clipboard.</summary>
        Failure
    }

    /// <summary>
    /// What the second call of a two-call read should lead to.
    /// </summary>
    internal enum WindowsClipboardSecondReadDecision
    {
        /// <summary>The buffer holds the payload and may be read.</summary>
        Read,

        /// <summary>The clipboard was emptied between the two calls.</summary>
        EmptySuccess,

        /// <summary>The content grew between the two calls; allocate again.</summary>
        Retry,

        /// <summary>Report the error without touching the buffer.</summary>
        Failure
    }

    /// <summary>
    /// Which COM reference this layer owns on the Unity main thread.
    /// </summary>
    /// <summary>
    /// Where one drain has got to.
    /// <para>
    /// A drain used to be described by four separate flags, whose combinations could neither tell
    /// a live drain from one whose runner had died, nor a queued delivery from a finished one.
    /// </para>
    /// </summary>
    internal enum WindowsClipboardDrainPhase
    {
        /// <summary>Attempts are still being made, one per frame.</summary>
        Running,

        /// <summary>The result is known and is being handed over on the current stack.</summary>
        Settling,

        /// <summary>The result is known and its delivery is waiting on the dispatcher.</summary>
        DeliveryQueued
    }

    /// <summary>How far a quit request has got.</summary>
    internal enum WindowsClipboardQuitState
    {
        /// <summary>No quit has been asked for.</summary>
        None,

        /// <summary>A quit was refused once and is waiting for a drain to finish.</summary>
        WaitingForDrain,

        /// <summary>The quit has been let through; a second request goes straight past.</summary>
        Resumed
    }

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
    /// Logging deviation: this file logs the shape of what passes through it - lengths, counts,
    /// format names, error codes - and never the clipboard content itself, nor the error message
    /// built from a native payload. What a user copied is not this layer's to record.
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

        /// <summary>Operation name for CopyPlainText.</summary>
        public const string OperationCopyPlainText = "copyPlainText";

        /// <summary>Operation name for CopyHtml.</summary>
        public const string OperationCopyHtml = "copyHtml";

        /// <summary>Operation name for CopyFiles.</summary>
        public const string OperationCopyFiles = "copyFiles";

        /// <summary>Operation name for CopyImage.</summary>
        public const string OperationCopyImage = "copyImage";

        /// <summary>Operation name for CopyCustomFormat.</summary>
        public const string OperationCopyCustomFormat = "copyCustomFormat";

        /// <summary>Operation name for CopyMultipleFormats.</summary>
        public const string OperationCopyMultipleFormats = "copyMultipleFormats";

        /// <summary>Operation name for PastePlainText.</summary>
        public const string OperationPastePlainText = "pastePlainText";

        /// <summary>Operation name for PasteHtml.</summary>
        public const string OperationPasteHtml = "pasteHtml";

        /// <summary>Operation name for PasteFiles.</summary>
        public const string OperationPasteFiles = "pasteFiles";

        /// <summary>Operation name for PasteImage.</summary>
        public const string OperationPasteImage = "pasteImage";

        /// <summary>Operation name for PasteCustomFormat.</summary>
        public const string OperationPasteCustomFormat = "pasteCustomFormat";

        /// <summary>Operation name for HasFormat.</summary>
        public const string OperationHasFormat = "hasClipboardFormat";

        /// <summary>Operation name for GetFormats.</summary>
        public const string OperationGetFormats = "getClipboardFormats";

        /// <summary>Operation name for GetPreferredFormat.</summary>
        public const string OperationGetPreferredFormat = "getPreferredClipboardFormat";

        /// <summary>Operation name for Clear.</summary>
        public const string OperationClear = "clearClipboard";

        /// <summary>Operation name for GetHistory.</summary>
        public const string OperationGetHistory = "getClipboardHistory";

        /// <summary>Operation name for RestoreHistoryItem.</summary>
        public const string OperationRestoreHistoryItem = "restoreHistoryItem";

        /// <summary>Operation name for DeleteHistoryItem.</summary>
        public const string OperationDeleteHistoryItem = "deleteHistoryItem";

        /// <summary>Operation name for ClearUnpinnedHistory.</summary>
        public const string OperationClearUnpinnedHistory = "clearUnpinnedHistory";

        /// <summary>Operation name for GetHistoryAvailability.</summary>
        public const string OperationGetHistoryAvailability = "getClipboardHistoryAvailability";

        /// <summary>Operation name for CancelRequest.</summary>
        public const string OperationCancelRequest = "cancelClipboardRequest";

        /// <summary>Operation name for SetHistoryEventsEnabled.</summary>
        public const string OperationSetHistoryEvents = "setClipboardHistoryCallbacks";

        /// <summary>Operation name for ReserveDeferredFormats.</summary>
        public const string OperationReserveDeferredFormats = "reserveDeferredFormats";

        /// <summary>Operation name for RecoverDeferredState.</summary>
        public const string OperationRecoverDeferredState = "recoverDeferredState";

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

        /// <summary>
        /// Raised when a read that yields text completes: PastePlainText, PasteHtml and
        /// GetPreferredFormat.
        /// </summary>
        public event Action<WindowsClipboardTextResult>? TextReadCompleted;

        /// <summary>Raised when PasteFiles or GetFormats completes.</summary>
        public event Action<WindowsClipboardStringListResult>? StringListReadCompleted;

        /// <summary>Raised when PasteImage or PasteCustomFormat completes.</summary>
        public event Action<WindowsClipboardBytesResult>? BytesReadCompleted;

        /// <summary>Raised when HasFormat completes.</summary>
        public event Action<WindowsClipboardFormatPresenceResult>? FormatPresenceChecked;

        /// <summary>Raised when GetHistory completes.</summary>
        public event Action<WindowsClipboardHistoryResult>? HistoryReadCompleted;

        /// <summary>Raised when GetHistoryAvailability completes.</summary>
        public event Action<WindowsClipboardAvailabilityResult>? HistoryAvailabilityChecked;

        /// <summary>
        /// Raised when a new item is added to the Windows clipboard history.
        /// <para>
        /// Additions only. Deletions and a cleared history are not guaranteed to raise it, so
        /// re-query the history after your own delete or clear rather than waiting for this.
        /// </para>
        /// </summary>
        public event Action? HistoryChanged;

        /// <summary>
        /// Raised when the clipboard history setting is toggled.
        /// <para>
        /// Not reliable, and must not drive behaviour: the underlying WinRT event was measured to
        /// fire at most once per process, and never at all when the registration was made while
        /// history was disabled. Call GetHistoryAvailability whenever the current setting matters.
        /// </para>
        /// </summary>
        public event Action<bool>? HistoryEnabledChanged;

        /// <summary>
        /// Raised when the cloud clipboard setting is toggled. Carries the same unreliability as
        /// <see cref="HistoryEnabledChanged"/>.
        /// </summary>
        public event Action<bool>? RoamingEnabledChanged;

        // ── Static state (main thread only) ──────────────────────────────────────

        // Captured on the main thread in Awake so dispatching never touches
        // UnityMainThreadDispatcher.Instance, whose getter creates a GameObject.
        private static UnityMainThreadDispatcher? s_dispatcher;
        private static int s_mainThreadId;
        private static bool s_isTerminated;

        /// <summary>
        /// Everything needed to deliver one asynchronous request, keyed by the ticket the registry
        /// issued. The registry owns the lifecycle and decides who delivers; this holds what to
        /// deliver.
        /// </summary>
        private sealed class PendingRequest
        {
            internal string Operation = string.Empty;
            internal WindowsClipboardRequestKind Kind;
            internal uint NativeRequestId;
            internal string? InFlightKey;
            internal CancellationTokenRegistration Registration;

            // Filled in when the outcome is known, before delivery is queued.
            internal WindowsClipboardErrorCode Code = WindowsClipboardErrorCode.None;
            internal string? Json;

            internal Action<WindowsClipboardHistoryResult>? OnHistory;
            internal Action<WindowsClipboardAvailabilityResult>? OnAvailability;
            internal Action<WindowsClipboardResult>? OnStatus;
        }

        private static readonly WindowsClipboardRequestTable s_registry = new();
        private static readonly Dictionary<uint, PendingRequest> s_pending = new();

        // One in-flight marker per operation that changes the clipboard or the history, so a second
        // call cannot make the first one's result ambiguous.
        private static readonly HashSet<string> s_inFlight = new();

        private static WindowsClipboardManagerState s_state = WindowsClipboardManagerState.Uninitialized;
        private static WindowsClipboardComOwnership s_comOwnership = WindowsClipboardComOwnership.None;
        // Reachable from a static delegate the native side holds, so these have to be static
        // too. A caller's Func stays referenced until a later reservation succeeds or shutdown
        // completes.
        private static Dictionary<string, Func<byte[]>> s_renderProviders = new();
        private static readonly Dictionary<string, byte[]> s_renderCache = new();

        // Published only while a reservation call is in flight: the native side swaps its renderer
        // table before it finishes placing the formats, so a render request arriving inside that
        // window has to resolve against both generations.
        private static Dictionary<string, Func<byte[]>>? s_renderStaging;

        private static bool s_quitHandlerSubscribed;

        /// <summary>
        /// One run of the drain, from the first attempt to the moment its result is handed over.
        /// </summary>
        /// <remarks>
        /// A single object rather than a set of flags. The flags could not express the states this
        /// actually reaches - a runner that died, a delivery queued but not yet made, a settle in
        /// progress - and every combination they could not express turned into a defect.
        /// </remarks>
        private sealed class DrainSession
        {
            /// <summary>Identifies this run, so a queued delivery cannot settle a later one.</summary>
            internal uint Generation;

            internal WindowsClipboardDrainPhase Phase = WindowsClipboardDrainPhase.Running;
            internal int Attempts;
            internal float Deadline;
            internal bool RecoveryTried;

            /// <summary>Whether anyone asked for the result. A quit alone does not.</summary>
            internal bool DeliveryRequested;

            // A list rather than a multicast delegate: each caller asked separately and is owed its
            // own answer, so one that throws must not take the others' results with it. Indexed
            // during the settle, so a caller that joins while it runs is still served.
            internal readonly List<Action<WindowsClipboardResult>> Waiters = new();

            internal WindowsClipboardResult Result;
            internal WindowsClipboardShutdownProgress Progress = WindowsClipboardShutdownProgress.NotYet;
        }

        private static DrainSession? s_drain;

        // Never rewound, for the same reason request tickets are not: a delivery queued before a
        // reset still carries its generation, and reusing it would let that stale delivery settle
        // a later drain.
        private static uint s_drainGeneration;

        private static WindowsClipboardQuitState s_quitState;

        // True while a settle is running that a quit follows. Nothing pumps the dispatcher after
        // the quit, so anything delivered during it has to be handed over on this stack - not only
        // the drain's own result, but whatever a callback starts while it runs.
        private static bool s_deliveringBeforeQuit;

        // ── Shutdown budget ──────────────────────────────────────────────────────

        private const int ShutdownRetryFrameBudget = 60;
        private const float ShutdownRetrySecondBudget = 2f;

        // Held in fields rather than read from the constants directly, so a test can shorten the
        // budget instead of spending the real two seconds to reach the timeout.
        private static int s_retryFrameBudget = ShutdownRetryFrameBudget;
        private static float s_retrySecondBudget = ShutdownRetrySecondBudget;

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

        /// <summary>Which path a shutdown attempt came from, for the seams below.</summary>
        internal enum ShutdownOriginForTests
        {
            PublicApi,
            Drain,
            Destroy,
            Quit
        }

        /// <summary>
        /// Drives the state machine without the native bridge, as if the attempt had come from the
        /// given path. The origin matters: only a quit-started shutdown resumes the quit.
        /// </summary>
        internal static WindowsClipboardShutdownProgress InjectShutdownResultForTests(
            bool completed,
            WindowsClipboardErrorCode code,
            ShutdownOriginForTests origin = ShutdownOriginForTests.PublicApi)
        {
            WindowsClipboardResult result = completed
                ? WindowsClipboardResult.Success(OperationShutdown)
                : WindowsClipboardResult.Failure(OperationShutdown, code);
            return FinishShutdownAttempt((ShutdownOrigin)origin, result, completed);
        }

        /// <summary>
        /// Pretends the captured main thread is a different one, so the thread guard can be
        /// exercised without actually leaving the main thread.
        /// </summary>
        internal static void SetMainThreadIdForTests(int threadId) => s_mainThreadId = threadId;

        /// <summary>Whether a drain coroutine is currently running.</summary>
        internal static bool DrainRunningForTests =>
            s_drain != null && s_drain.Phase == WindowsClipboardDrainPhase.Running;

        /// <summary>The current drain's phase, or null when no drain is in flight.</summary>
        internal static WindowsClipboardDrainPhase? DrainPhaseForTests => s_drain?.Phase;

        /// <summary>
        /// Makes one drain attempt without waiting for Update, so a test can stop between the
        /// moment a delivery is queued and the moment it is claimed.
        /// </summary>
        internal static void StepDrainForTests()
        {
            DrainSession? session = s_drain;
            if (session == null || _instance == null) return;
            if (session.Phase != WindowsClipboardDrainPhase.Running) return;
            _instance.AdvanceDrain(session, force: false);
        }

        /// <summary>
        /// Stands in for the native shutdown call, so the paths that only exist while the native
        /// side refuses to finish - the retry loop, the budget timeout, the recovery from a
        /// half-applied reservation - can be reached at all. The editor otherwise reports
        /// completion on the first attempt and none of them ever run.
        /// </summary>
        internal static Func<(bool completed, WindowsClipboardErrorCode code)>? NativeShutdownForTests;

        /// <summary>How often the native recovery call was made.</summary>
        internal static int RecoverDeferredCallCountForTests;

        /// <summary>Shortens the drain budget so a timeout test does not take two seconds.</summary>
        internal static void SetShutdownBudgetForTests(int frames, float seconds)
        {
            s_retryFrameBudget = frames;
            s_retrySecondBudget = seconds;
        }

        /// <summary>
        /// Stands in for the platform check, which the editor can never satisfy.
        /// <para>
        /// Without it no operation could pass the guard here, and the whole accepted half of the
        /// request lifecycle - in-flight markers, completions, teardown drains - would be
        /// unreachable. Only the guard's own answer is affected; the native boundary stays
        /// compiled out.
        /// </para>
        /// </summary>
        internal static bool? PlatformAvailableForTests;

        /// <summary>Runs the quit handler without an actual quit request.</summary>
        internal static bool InvokeWantsToQuitForTests() => OnWantsToQuit();

        /// <summary>Whether the quit drain has started and finished, for the resume tests.</summary>
        internal static bool QuitDrainCompletedForTests =>
            s_quitState == WindowsClipboardQuitState.Resumed;

        /// <summary>How far a quit request has got.</summary>
        internal static WindowsClipboardQuitState QuitStateForTests => s_quitState;

        /// <summary>Puts the manager into a state a test needs without touching the native side.</summary>
        internal static void SetStateForTests(WindowsClipboardManagerState state) => s_state = state;

        /// <summary>
        /// Raises ClipboardChanged through the same dispatcher path the native callback uses.
        /// The editor never reaches that callback, so without this seam the event could not be
        /// covered at all.
        /// </summary>
        internal static void InjectClipboardChangedForTests() => RaiseClipboardChanged();

        /// <summary>
        /// Runs the shared operation guard and reports what it decided.
        /// The public operations are instance methods on a MonoBehaviour, which EditMode cannot
        /// construct, so the ordering is covered through this seam instead.
        /// </summary>
        internal static WindowsClipboardErrorCode CheckOperationGuardForTests()
        {
            return CanRunOperation("test", out WindowsClipboardErrorCode code)
                ? WindowsClipboardErrorCode.None
                : code;
        }

        /// <summary>Sets the tombstone so a test can cover the destroyed rejection.</summary>
        internal static void SetTerminatedForTests(bool terminated) => s_isTerminated = terminated;

        /// <summary>Number of requests the registry still tracks.</summary>
        internal static int PendingRequestCountForTests => s_registry.Count;

        /// <summary>Whether an operation currently holds its in-flight marker.</summary>
        internal static bool IsInFlightForTests(string operation) => s_inFlight.Contains(operation);

        /// <summary>
        /// Stands in for the native request call: while set, the next request is accepted with this
        /// id instead of reaching the bridge.
        /// <para>
        /// The editor compiles the native boundary out, so every request would otherwise be
        /// rejected before acceptance and the accepted half of the lifecycle - in-flight markers,
        /// completions, teardown drains - could never be exercised.
        /// </para>
        /// </summary>
        internal static uint? NextNativeRequestIdForTests;

        /// <summary>
        /// Makes the next request throw from where the native call would be, so the paths that
        /// only run when the bridge misbehaves can be reached.
        /// </summary>
        internal static Exception? RequestExceptionForTests;

        /// <summary>Drops the dispatcher, for the delivery paths that have to survive without one.</summary>
        internal static void ClearDispatcherForTests() => s_dispatcher = null;

        /// <summary>The ticket of the single tracked request, for tests that start exactly one.</summary>
        internal static uint OnlyPendingTicketForTests()
        {
            foreach (uint ticket in s_pending.Keys) return ticket;
            return 0;
        }

        /// <summary>
        /// How many cancellation registrations were actually disposed. Counted after the disposal
        /// rather than at the decision to make one, so a release that never happens is visible.
        /// </summary>
        internal static int CancellationRegistrationsReleasedForTests;

        /// <summary>Keeps a released registration alive, to reproduce the id-reuse window.</summary>
        internal static bool SuppressRegistrationDisposalForTests;

        /// <summary>Whether a cancellation registration is still held for a request.</summary>
        internal static bool HasCancellationRegistrationForTests(uint ticket) =>
            s_pending.TryGetValue(ticket, out PendingRequest? pending) &&
            pending.Registration != default;

        /// <summary>Raises the history events through the same path the native callbacks use.</summary>
        internal static void InjectHistoryChangedForTests() => RaiseHistoryChanged();

        /// <summary>Raises the history-setting event through the native callbacks' own path.</summary>
        internal static void InjectHistoryEnabledChangedForTests(bool enabled) =>
            RaiseHistoryEnabledChanged(enabled);

        /// <summary>Raises the roaming-setting event through the native callbacks' own path.</summary>
        internal static void InjectRoamingEnabledChangedForTests(bool enabled) =>
            RaiseRoamingEnabledChanged(enabled);

        /// <summary>Format names whose providers are currently live.</summary>
        internal static IReadOnlyCollection<string> RenderProviderNamesForTests => s_renderProviders.Keys;

        /// <summary>Runs the two-phase render callback without the native side.</summary>
        internal static uint RenderForTests(
            string formatName, IntPtr buffer, uint bufferSize, out uint requiredSize) =>
            RenderDeferredFormat(formatName, buffer, bufferSize, out requiredSize);

        /// <summary>Applies a reservation outcome without the native side.</summary>
        internal static void ApplyReservationOutcomeForTests(
            WindowsClipboardErrorCode code, Dictionary<string, Func<byte[]>> next) =>
            ApplyReservationOutcome(code, next);

        /// <summary>Installs a live provider generation without a reservation call.</summary>
        internal static void SetRenderProvidersForTests(Dictionary<string, Func<byte[]>> providers)
        {
            // The cache goes with it. Every path that installs a generation for real clears the
            // cache too, so leaving it would let a test build a state the manager cannot reach:
            // last generation's bytes answering this generation's second phase.
            s_renderProviders = providers;
            s_renderCache.Clear();
        }

        /// <summary>Format names whose rendered bytes are currently cached.</summary>
        internal static IReadOnlyCollection<string> RenderCacheNamesForTests => s_renderCache.Keys;

        /// <summary>Drives the native completion callback without the native side.</summary>
        internal static void InjectCompletionForTests(uint requestId, int error, string? json) =>
            OnRequestCompletedNative(requestId, error, json);

        /// <summary>Runs the teardown drain on its own, without a shutdown.</summary>
        internal static void DrainForTests() => DrainRequestRegistry();

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

            // No state gate. A request rejected before Initialize still has a queued delivery, and
            // the drain inside the attempt is the last chance to hand it over: nothing pumps the
            // dispatcher after this. The attempt itself is safe in every state because the native
            // uninit is idempotent and the termination leaves a finished manager alone.
            WindowsClipboardResult result = RunShutdownAttempt(
                ShutdownOrigin.Destroy, out bool completed, out _);
            if (!completed)
            {
                Debug.LogWarning(
                    $"[{LogTag}][{nameof(OnDestroy)}] shutdown did not complete; native resources are retained. " +
                    $"result: {result.ErrorCode}");
            }

            // Nothing will call Update or run a queued delivery after this, so a drain still in
            // flight is ended here - including the quit it may be carrying.
            AbortDrain();

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

            // The order matches CanRunOperation on purpose. A destroyed manager reports
            // ManagerDestroyed whatever state it was left in: OnDestroy can leave the state at
            // Draining, and checking the state first would answer a destroyed manager with
            // ShuttingDown and never recover from it.
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

            // An idempotent Initialize must not touch COM or the native side, because the apartment
            // probe would then overwrite an ownership record this layer still has to release at
            // shutdown.
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
            catch (Exception ex)
            {
                Debug.LogError($"[{LogTag}][{nameof(Initialize)}] {ex.GetType().Name}: {ex.Message}");

                // Every exception, not only the two that name a missing bridge. The native side
                // holds nothing when the call did not complete, so the COM reference this layer
                // took is safe to give back right away - and it is the only chance to: the state
                // is still Uninitialized, which TryShutdown answers as an idempotent success
                // without ever reaching the release.
                ReleaseOwnedComReference();
                return Deliver(
                    WindowsClipboardResult.Failure(
                        OperationInitialize,
                        ex is DllNotFoundException || ex is EntryPointNotFoundException
                            ? WindowsClipboardErrorCode.BridgeUnavailable
                            : WindowsClipboardErrorCode.Unknown),
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

            return RunShutdownAttempt(ShutdownOrigin.PublicApi, out completed, out _);
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

            // Joining an existing drain, whatever stage it is at. A caller that arrives while
            // the result is being handed over is picked up by that same pass.
            DrainSession session = s_drain ?? StartDrain();
            session.DeliveryRequested = true;
            if (onResult != null) session.Waiters.Add(onResult);
            if (!ReferenceEquals(session, s_drain) || session.Phase != WindowsClipboardDrainPhase.Running)
            {
                return;
            }

            // Update drives the attempts, and a disabled object gets none. Rather than leave the
            // caller waiting on a drain that cannot advance, make the one attempt that is possible.
            if (!isActiveAndEnabled)
            {
                Debug.LogWarning(
                    $"[{LogTag}][{nameof(ShutdownWithDrain)}] the manager is not active; " +
                    "draining in one attempt instead of across frames.");
                AdvanceDrain(session, force: true);
            }
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

        // ── Read classification ──────────────────────────────────────────────────

        /// <summary>
        /// Classifies the sizing call of a two-call read.
        /// <para>
        /// The error code decides, never the returned size. The native read APIs return zero for a
        /// lease failure and for an internal read failure as well, so treating a zero size as "the
        /// clipboard is empty" would turn NotInitialized, Busy or InvalidData into a silent empty
        /// success that the caller cannot tell from a genuinely empty clipboard.
        /// </para>
        /// </summary>
        /// <param name="code">The error code the sizing call reported.</param>
        /// <param name="requiredSize">The size it returned, in wchar_t or bytes.</param>
        /// <param name="isByteApi">True for PasteImage and PasteCustomFormat.</param>
        /// <returns>Whether the read is empty, needs a buffer, or failed.</returns>
        internal static WindowsClipboardReadDecision ClassifyFirstRead(
            WindowsClipboardErrorCode code, uint requiredSize, bool isByteApi)
        {
            switch (code)
            {
                case WindowsClipboardErrorCode.Empty:
                case WindowsClipboardErrorCode.FormatUnavailable:
                    return WindowsClipboardReadDecision.EmptySuccess;

                case WindowsClipboardErrorCode.None:
                    return requiredSize == 0
                        ? WindowsClipboardReadDecision.EmptySuccess
                        : WindowsClipboardReadDecision.NeedsBuffer;

                case WindowsClipboardErrorCode.BufferTooSmall:
                    if (requiredSize > 0) return WindowsClipboardReadDecision.NeedsBuffer;
                    // Zero bytes is a real payload for the byte APIs. A string API always needs at
                    // least the terminator, so a zero there means something went wrong.
                    return isByteApi
                        ? WindowsClipboardReadDecision.EmptySuccess
                        : WindowsClipboardReadDecision.Failure;

                default:
                    return WindowsClipboardReadDecision.Failure;
            }
        }

        /// <summary>
        /// Classifies the filling call of a two-call read, before the buffer is touched.
        /// </summary>
        /// <param name="code">The error code the filling call reported.</param>
        /// <returns>Whether to read the buffer, report empty, retry, or fail.</returns>
        internal static WindowsClipboardSecondReadDecision ClassifySecondRead(WindowsClipboardErrorCode code)
        {
            return code switch
            {
                WindowsClipboardErrorCode.None => WindowsClipboardSecondReadDecision.Read,
                // The clipboard can change between the two calls; neither outcome is an error.
                WindowsClipboardErrorCode.Empty => WindowsClipboardSecondReadDecision.EmptySuccess,
                WindowsClipboardErrorCode.FormatUnavailable => WindowsClipboardSecondReadDecision.EmptySuccess,
                WindowsClipboardErrorCode.BufferTooSmall => WindowsClipboardSecondReadDecision.Retry,
                _ => WindowsClipboardSecondReadDecision.Failure
            };
        }

        // ── Operation guard ──────────────────────────────────────────────────────

        /// <summary>
        /// Runs the checks every clipboard operation shares, in the order the design fixes.
        /// The shutdown paths deliberately do not come through here (see RunShutdownAttempt).
        /// </summary>
        /// <param name="operation">Native operation name, used by the rejection message.</param>
        /// <param name="code">The rejection code when this returns false.</param>
        /// <returns>True when the operation may proceed.</returns>
        private static bool CanRunOperation(string operation, out WindowsClipboardErrorCode code)
        {
            bool platformAvailable = Application.platform == RuntimePlatform.WindowsPlayer;
#if UNITY_EDITOR
            if (PlatformAvailableForTests is bool forced) platformAvailable = forced;
#endif
            code = ClassifyOperationGuard(IsMainThread(), s_isTerminated, platformAvailable, s_state);
            if (code == WindowsClipboardErrorCode.None) return true;

            Debug.Log($"[{LogTag}][{nameof(CanRunOperation)}] {operation} rejected: {code}");
            return false;
        }

        /// <summary>
        /// Decides what the shared operation guard answers, given everything it looks at.
        /// <para>
        /// Kept free of state so the order can be checked directly. The order itself is the point:
        /// each condition describes a different thing the caller has to fix, and answering with a
        /// later one first sends them after the wrong problem.
        /// </para>
        /// </summary>
        /// <param name="isMainThread">Whether the caller is on the thread that owns the native window.</param>
        /// <param name="terminated">Whether the manager has been destroyed.</param>
        /// <param name="platformAvailable">Whether this is a Windows player build.</param>
        /// <param name="state">The lifecycle state.</param>
        /// <returns>None when the operation may proceed, otherwise the rejection code.</returns>
        internal static WindowsClipboardErrorCode ClassifyOperationGuard(
            bool isMainThread,
            bool terminated,
            bool platformAvailable,
            WindowsClipboardManagerState state)
        {
            if (!isMainThread) return WindowsClipboardErrorCode.MainThreadRequired;
            if (terminated) return WindowsClipboardErrorCode.ManagerDestroyed;

            // Before the state, because outside a Windows player build no state is reachable: the
            // editor cannot initialize, and answering NotInitializedByHost there would send the
            // caller looking for a missing Initialize that could never have succeeded.
            if (!platformAvailable) return WindowsClipboardErrorCode.PlatformUnavailable;

            if (state == WindowsClipboardManagerState.Draining ||
                state == WindowsClipboardManagerState.ShutdownFailed)
            {
                return WindowsClipboardErrorCode.ShuttingDown;
            }
            if (state != WindowsClipboardManagerState.Running)
            {
                // Stopping here keeps a caller that forgot to initialize from reaching the native
                // side just to receive its NotInitialized.
                return WindowsClipboardErrorCode.NotInitializedByHost;
            }

            return WindowsClipboardErrorCode.None;
        }

        // ── Public API: writing ──────────────────────────────────────────────────

        /// <summary>
        /// Places plain text on the clipboard.
        /// </summary>
        /// <param name="text">The text to place. Must not be null.</param>
        /// <param name="options">Whether to keep the content out of history or the cloud clipboard.</param>
        /// <param name="onResult">Per-call callback. The common event fires as well.</param>
        /// <returns>The result, also delivered to the event and the callback.</returns>
        public WindowsClipboardResult CopyPlainText(
            string text,
            WindowsClipboardWriteOptions options = WindowsClipboardWriteOptions.None,
            Action<WindowsClipboardResult>? onResult = null)
        {
            // Clipboard content may hold passwords or tokens, so the value never reaches the log.
            Debug.Log($"[{LogTag}][{nameof(CopyPlainText)}] length: {text?.Length ?? -1}, options: {options}, onResult: {onResult != null}");

            if (!CanRunOperation(OperationCopyPlainText, out WindowsClipboardErrorCode rejected))
            {
                return Deliver(WindowsClipboardResult.Failure(OperationCopyPlainText, rejected), onResult);
            }
            if (text == null)
            {
                return Deliver(WindowsClipboardResult.Failure(
                    OperationCopyPlainText, WindowsClipboardErrorCode.InvalidArgument, "text was null"), onResult);
            }

            return Deliver(InvokeWrite(OperationCopyPlainText,
                pError => CopyPlainTextNative(text, (uint)options, out pError)), onResult);
        }

        /// <summary>
        /// Places an HTML fragment on the clipboard, with a plain-text fallback for readers that
        /// do not take HTML.
        /// </summary>
        /// <param name="htmlFragment">The HTML fragment. Must not be null.</param>
        /// <param name="plainText">The fallback text, or null for none.</param>
        /// <param name="options">Whether to keep the content out of history or the cloud clipboard.</param>
        /// <param name="onResult">Per-call callback. The common event fires as well.</param>
        /// <returns>The result, also delivered to the event and the callback.</returns>
        public WindowsClipboardResult CopyHtml(
            string htmlFragment,
            string? plainText = null,
            WindowsClipboardWriteOptions options = WindowsClipboardWriteOptions.None,
            Action<WindowsClipboardResult>? onResult = null)
        {
            Debug.Log($"[{LogTag}][{nameof(CopyHtml)}] htmlLength: {htmlFragment?.Length ?? -1}, hasPlainText: {plainText != null}, options: {options}, onResult: {onResult != null}");

            if (!CanRunOperation(OperationCopyHtml, out WindowsClipboardErrorCode rejected))
            {
                return Deliver(WindowsClipboardResult.Failure(OperationCopyHtml, rejected), onResult);
            }
            if (htmlFragment == null)
            {
                return Deliver(WindowsClipboardResult.Failure(
                    OperationCopyHtml, WindowsClipboardErrorCode.InvalidArgument, "htmlFragment was null"), onResult);
            }

            return Deliver(InvokeWrite(OperationCopyHtml,
                pError => CopyHtmlNative(htmlFragment, plainText, (uint)options, out pError)), onResult);
        }

        /// <summary>
        /// Places a list of file paths on the clipboard, the way Explorer copies files.
        /// </summary>
        /// <param name="paths">Absolute file paths. Must hold at least one entry.</param>
        /// <param name="options">Whether to keep the content out of history or the cloud clipboard.</param>
        /// <param name="onResult">Per-call callback. The common event fires as well.</param>
        /// <returns>The result, also delivered to the event and the callback.</returns>
        public WindowsClipboardResult CopyFiles(
            IReadOnlyList<string> paths,
            WindowsClipboardWriteOptions options = WindowsClipboardWriteOptions.None,
            Action<WindowsClipboardResult>? onResult = null)
        {
            Debug.Log($"[{LogTag}][{nameof(CopyFiles)}] count: {paths?.Count ?? -1}, options: {options}, onResult: {onResult != null}");

            if (!CanRunOperation(OperationCopyFiles, out WindowsClipboardErrorCode rejected))
            {
                return Deliver(WindowsClipboardResult.Failure(OperationCopyFiles, rejected), onResult);
            }
            if (paths == null || paths.Count == 0)
            {
                return Deliver(WindowsClipboardResult.Failure(
                    OperationCopyFiles, WindowsClipboardErrorCode.InvalidArgument, "paths was null or empty"), onResult);
            }
            for (int i = 0; i < paths.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(paths[i]))
                {
                    return Deliver(WindowsClipboardResult.Failure(
                        OperationCopyFiles, WindowsClipboardErrorCode.InvalidArgument,
                        $"paths[{i}] was null or blank"), onResult);
                }
            }

            string json = WindowsClipboardJsonBuilder.BuildPathsJson(paths);
            return Deliver(InvokeWrite(OperationCopyFiles,
                pError => CopyFilesNative(json, (uint)options, out pError)), onResult);
        }

        /// <summary>
        /// Places a device-independent bitmap on the clipboard.
        /// </summary>
        /// <param name="dib">The DIB bytes. Encoding a texture into this format is the caller's job.</param>
        /// <param name="options">Whether to keep the content out of history or the cloud clipboard.</param>
        /// <param name="onResult">Per-call callback. The common event fires as well.</param>
        /// <returns>The result, also delivered to the event and the callback.</returns>
        public WindowsClipboardResult CopyImage(
            byte[] dib,
            WindowsClipboardWriteOptions options = WindowsClipboardWriteOptions.None,
            Action<WindowsClipboardResult>? onResult = null)
        {
            Debug.Log($"[{LogTag}][{nameof(CopyImage)}] size: {dib?.Length ?? -1}, options: {options}, onResult: {onResult != null}");

            if (!CanRunOperation(OperationCopyImage, out WindowsClipboardErrorCode rejected))
            {
                return Deliver(WindowsClipboardResult.Failure(OperationCopyImage, rejected), onResult);
            }
            if (dib == null || dib.Length == 0)
            {
                return Deliver(WindowsClipboardResult.Failure(
                    OperationCopyImage, WindowsClipboardErrorCode.InvalidArgument, "dib was null or empty"), onResult);
            }

            return Deliver(InvokeWrite(OperationCopyImage,
                pError => CopyImageNative(dib, (uint)dib.Length, (uint)options, out pError)), onResult);
        }

        /// <summary>
        /// Places raw bytes on the clipboard under a registered format name.
        /// </summary>
        /// <param name="formatName">Format name, either a CF_* constant name or a registered name.</param>
        /// <param name="data">The bytes to place. Must hold at least one byte.</param>
        /// <param name="options">Whether to keep the content out of history or the cloud clipboard.</param>
        /// <param name="onResult">Per-call callback. The common event fires as well.</param>
        /// <returns>The result, also delivered to the event and the callback.</returns>
        public WindowsClipboardResult CopyCustomFormat(
            string formatName,
            byte[] data,
            WindowsClipboardWriteOptions options = WindowsClipboardWriteOptions.None,
            Action<WindowsClipboardResult>? onResult = null)
        {
            Debug.Log($"[{LogTag}][{nameof(CopyCustomFormat)}] format: {formatName}, size: {data?.Length ?? -1}, options: {options}, onResult: {onResult != null}");

            if (!CanRunOperation(OperationCopyCustomFormat, out WindowsClipboardErrorCode rejected))
            {
                return Deliver(WindowsClipboardResult.Failure(OperationCopyCustomFormat, rejected), onResult);
            }
            if (string.IsNullOrWhiteSpace(formatName))
            {
                return Deliver(WindowsClipboardResult.Failure(
                    OperationCopyCustomFormat, WindowsClipboardErrorCode.InvalidArgument,
                    "formatName was null or blank"), onResult);
            }
            if (data == null || data.Length == 0)
            {
                return Deliver(WindowsClipboardResult.Failure(
                    OperationCopyCustomFormat, WindowsClipboardErrorCode.InvalidArgument,
                    "data was null or empty"), onResult);
            }

            return Deliver(InvokeWrite(OperationCopyCustomFormat,
                pError => CopyCustomFormatNative(formatName, data, (uint)data.Length, (uint)options, out pError)),
                onResult);
        }

        /// <summary>
        /// Places several formats of the same content on the clipboard in one operation, so each
        /// reader can take the richest form it understands.
        /// </summary>
        /// <param name="items">
        /// The formats, richest first. The native layer rejects duplicate formats and a payload
        /// kind that does not fit its format before anything is placed.
        /// </param>
        /// <param name="options">Whether to keep the content out of history or the cloud clipboard.</param>
        /// <param name="onResult">Per-call callback. The common event fires as well.</param>
        /// <returns>
        /// The result. PartialState means a placement failed and the rollback failed as well, so
        /// the clipboard may hold part of the content; RecoverDeferredState does not cover this.
        /// </returns>
        public WindowsClipboardResult CopyMultipleFormats(
            IReadOnlyList<WindowsClipboardFormatPayload> items,
            WindowsClipboardWriteOptions options = WindowsClipboardWriteOptions.None,
            Action<WindowsClipboardResult>? onResult = null)
        {
            Debug.Log($"[{LogTag}][{nameof(CopyMultipleFormats)}] count: {items?.Count ?? -1}, options: {options}, onResult: {onResult != null}");

            if (!CanRunOperation(OperationCopyMultipleFormats, out WindowsClipboardErrorCode rejected))
            {
                return Deliver(WindowsClipboardResult.Failure(OperationCopyMultipleFormats, rejected), onResult);
            }
            if (items == null || items.Count == 0)
            {
                return Deliver(WindowsClipboardResult.Failure(
                    OperationCopyMultipleFormats, WindowsClipboardErrorCode.InvalidArgument,
                    "items was null or empty"), onResult);
            }
            for (int i = 0; i < items.Count; i++)
            {
                if (!items[i].TryValidate(out string? detail))
                {
                    return Deliver(WindowsClipboardResult.Failure(
                        OperationCopyMultipleFormats, WindowsClipboardErrorCode.InvalidArgument,
                        $"items[{i}]: {detail}"), onResult);
                }
            }

            string json = WindowsClipboardJsonBuilder.BuildMultiFormatItemsJson(items);
            return Deliver(InvokeWrite(OperationCopyMultipleFormats,
                pError => CopyMultipleFormatsNative(json, (uint)options, out pError)), onResult);
        }

        /// <summary>
        /// Empties the clipboard.
        /// </summary>
        /// <param name="onResult">Per-call callback. The common event fires as well.</param>
        /// <returns>The result, also delivered to the event and the callback.</returns>
        public WindowsClipboardResult Clear(Action<WindowsClipboardResult>? onResult = null)
        {
            Debug.Log($"[{LogTag}][{nameof(Clear)}] onResult: {onResult != null}");

            if (!CanRunOperation(OperationClear, out WindowsClipboardErrorCode rejected))
            {
                return Deliver(WindowsClipboardResult.Failure(OperationClear, rejected), onResult);
            }

            return Deliver(InvokeWrite(OperationClear, pError => ClearNative(out pError)), onResult);
        }

        // ── Public API: reading ──────────────────────────────────────────────────

        /// <summary>
        /// Reads plain text from the clipboard.
        /// </summary>
        /// <param name="onResult">Per-call callback. <see cref="TextReadCompleted"/> fires as well.</param>
        /// <returns>
        /// The result. An empty clipboard is a success with IsEmpty set; only a real failure
        /// carries an error.
        /// </returns>
        public WindowsClipboardTextResult PastePlainText(Action<WindowsClipboardTextResult>? onResult = null)
        {
            Debug.Log($"[{LogTag}][{nameof(PastePlainText)}] onResult: {onResult != null}");
            return DeliverText(ReadText(OperationPastePlainText, PastePlainTextNative), onResult);
        }

        /// <summary>
        /// Reads the HTML fragment from the clipboard, decoded from the CF_HTML payload.
        /// </summary>
        /// <param name="onResult">Per-call callback. <see cref="TextReadCompleted"/> fires as well.</param>
        /// <returns>The result. An empty clipboard is a success with IsEmpty set.</returns>
        public WindowsClipboardTextResult PasteHtml(Action<WindowsClipboardTextResult>? onResult = null)
        {
            Debug.Log($"[{LogTag}][{nameof(PasteHtml)}] onResult: {onResult != null}");
            return DeliverText(ReadText(OperationPasteHtml, PasteHtmlNative), onResult);
        }

        /// <summary>
        /// Reads the copied file list from the clipboard.
        /// </summary>
        /// <param name="onResult">Per-call callback. <see cref="StringListReadCompleted"/> fires as well.</param>
        /// <returns>The result. An empty clipboard is a success with no values.</returns>
        public WindowsClipboardStringListResult PasteFiles(
            Action<WindowsClipboardStringListResult>? onResult = null)
        {
            Debug.Log($"[{LogTag}][{nameof(PasteFiles)}] onResult: {onResult != null}");
            return DeliverStringList(ReadStringList(OperationPasteFiles, PasteFilesNative), onResult);
        }

        /// <summary>
        /// Lists the formats the clipboard currently holds.
        /// </summary>
        /// <param name="onResult">Per-call callback. <see cref="StringListReadCompleted"/> fires as well.</param>
        /// <returns>
        /// The result. An empty clipboard reports no values rather than the native Empty code,
        /// because the native API answers with an empty array.
        /// </returns>
        public WindowsClipboardStringListResult GetFormats(
            Action<WindowsClipboardStringListResult>? onResult = null)
        {
            Debug.Log($"[{LogTag}][{nameof(GetFormats)}] onResult: {onResult != null}");
            return DeliverStringList(ReadStringList(OperationGetFormats, GetFormatsNative), onResult);
        }

        /// <summary>
        /// Reads a device-independent bitmap from the clipboard.
        /// </summary>
        /// <param name="onResult">Per-call callback. <see cref="BytesReadCompleted"/> fires as well.</param>
        /// <returns>The result. An empty clipboard is a success with no bytes.</returns>
        public WindowsClipboardBytesResult PasteImage(Action<WindowsClipboardBytesResult>? onResult = null)
        {
            Debug.Log($"[{LogTag}][{nameof(PasteImage)}] onResult: {onResult != null}");
            return DeliverBytes(ReadBytes(OperationPasteImage, PasteImageNative), onResult);
        }

        /// <summary>
        /// Reads raw bytes stored under a registered format name.
        /// </summary>
        /// <param name="formatName">Format name, either a CF_* constant name or a registered name.</param>
        /// <param name="onResult">Per-call callback. <see cref="BytesReadCompleted"/> fires as well.</param>
        /// <returns>The result. A format the clipboard does not hold is an empty success.</returns>
        public WindowsClipboardBytesResult PasteCustomFormat(
            string formatName, Action<WindowsClipboardBytesResult>? onResult = null)
        {
            Debug.Log($"[{LogTag}][{nameof(PasteCustomFormat)}] format: {formatName}, onResult: {onResult != null}");

            if (!CanRunOperation(OperationPasteCustomFormat, out WindowsClipboardErrorCode rejected))
            {
                return DeliverBytes(
                    WindowsClipboardBytesResult.Failure(OperationPasteCustomFormat, rejected), onResult);
            }
            if (string.IsNullOrWhiteSpace(formatName))
            {
                return DeliverBytes(WindowsClipboardBytesResult.Failure(
                    OperationPasteCustomFormat, WindowsClipboardErrorCode.InvalidArgument,
                    "formatName was null or blank"), onResult);
            }

            return DeliverBytes(
                ReadBytes(OperationPasteCustomFormat,
                    (IntPtr buffer, uint size, out int pError) =>
                        PasteCustomFormatNative(formatName, buffer, size, out pError),
                    skipGuard: true),
                onResult);
        }

        /// <summary>
        /// Reads the most descriptive format the clipboard holds.
        /// </summary>
        /// <param name="onResult">Per-call callback. <see cref="TextReadCompleted"/> fires as well.</param>
        /// <returns>
        /// The result. The native candidate list is fixed to CF_UNICODETEXT, CF_HDROP, CF_DIB and
        /// CF_BITMAP, so HTML is never reported here even when the clipboard holds it. An empty
        /// clipboard yields an empty string rather than the native Empty code.
        /// </returns>
        public WindowsClipboardTextResult GetPreferredFormat(
            Action<WindowsClipboardTextResult>? onResult = null)
        {
            Debug.Log($"[{LogTag}][{nameof(GetPreferredFormat)}] onResult: {onResult != null}");
            return DeliverText(ReadText(OperationGetPreferredFormat, GetPreferredFormatNative), onResult);
        }

        /// <summary>
        /// Asks whether the clipboard holds a format.
        /// </summary>
        /// <param name="formatName">Format name, either a CF_* constant name or a registered name.</param>
        /// <param name="onResult">Per-call callback. <see cref="FormatPresenceChecked"/> fires as well.</param>
        /// <returns>
        /// The result. IsSuccess says whether the query worked and HasFormat whether the format is
        /// there; the native API answers FALSE for both cases, so they stay separate here.
        /// </returns>
        public WindowsClipboardFormatPresenceResult HasFormat(
            string formatName, Action<WindowsClipboardFormatPresenceResult>? onResult = null)
        {
            Debug.Log($"[{LogTag}][{nameof(HasFormat)}] format: {formatName}, onResult: {onResult != null}");

            if (!CanRunOperation(OperationHasFormat, out WindowsClipboardErrorCode rejected))
            {
                return DeliverPresence(
                    WindowsClipboardFormatPresenceResult.Failure(OperationHasFormat, rejected), onResult);
            }
            if (string.IsNullOrWhiteSpace(formatName))
            {
                return DeliverPresence(WindowsClipboardFormatPresenceResult.Failure(
                    OperationHasFormat, WindowsClipboardErrorCode.InvalidArgument,
                    "formatName was null or blank"), onResult);
            }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            try
            {
                bool has = hasClipboardFormat(formatName, out int pError);
                WindowsClipboardFormatPresenceResult result = pError == 0
                    ? WindowsClipboardFormatPresenceResult.Success(OperationHasFormat, has)
                    : WindowsClipboardFormatPresenceResult.Failure(
                        OperationHasFormat, (WindowsClipboardErrorCode)pError);
                return DeliverPresence(result, onResult);
            }
            catch (Exception ex) when (ex is DllNotFoundException || ex is EntryPointNotFoundException)
            {
                Debug.LogError($"[{LogTag}][{nameof(HasFormat)}] {ex.GetType().Name}: {ex.Message}");
                return DeliverPresence(WindowsClipboardFormatPresenceResult.Failure(
                    OperationHasFormat, WindowsClipboardErrorCode.BridgeUnavailable), onResult);
            }
#else
            return DeliverPresence(WindowsClipboardFormatPresenceResult.Failure(
                OperationHasFormat, WindowsClipboardErrorCode.PlatformUnavailable), onResult);
#endif
        }

        // ── Read helpers ─────────────────────────────────────────────────────────

        /// <summary>
        /// One call of the native two-call read protocol.
        /// </summary>
        private delegate uint NativeSizedRead(IntPtr buffer, uint bufferSize, out int pError);

        private const int SizeChangedRetryBudget = 2;

        private static WindowsClipboardTextResult ReadText(string operation, NativeSizedRead call)
        {
            if (!CanRunOperation(operation, out WindowsClipboardErrorCode rejected))
            {
                return WindowsClipboardTextResult.Failure(operation, rejected);
            }

            ReadOutcome outcome = ReadRaw(operation, call, isByteApi: false);
            if (!outcome.IsSuccess) return WindowsClipboardTextResult.Failure(operation, outcome.Code);
            if (outcome.IsEmpty) return WindowsClipboardTextResult.Empty(operation);

            string text = outcome.Text ?? string.Empty;

            // Only here. GetPreferredFormat answers with an empty string when nothing matches its
            // candidates and never reports the native Empty code, so the empty string is its way
            // of saying nothing was found. For the paste operations an empty string is a value:
            // text that was copied and can be pasted back, which is not the same as an empty
            // clipboard, and folding the two together loses a distinction the caller can act on.
            if (operation == OperationGetPreferredFormat && text.Length == 0)
            {
                return WindowsClipboardTextResult.Empty(operation);
            }

            return WindowsClipboardTextResult.Success(operation, text);
        }

        private WindowsClipboardStringListResult ReadStringList(string operation, NativeSizedRead call)
        {
            if (!CanRunOperation(operation, out WindowsClipboardErrorCode rejected))
            {
                return WindowsClipboardStringListResult.Failure(operation, rejected);
            }

            ReadOutcome outcome = ReadRaw(operation, call, isByteApi: false);
            if (!outcome.IsSuccess) return WindowsClipboardStringListResult.Failure(operation, outcome.Code);
            if (outcome.IsEmpty) return WindowsClipboardStringListResult.Empty(operation);

            if (!WindowsClipboardJsonParser.TryParseStringArray(outcome.Text, out IReadOnlyList<string> values))
            {
                return WindowsClipboardStringListResult.Failure(
                    operation, WindowsClipboardErrorCode.ResultParseFailed);
            }
            return WindowsClipboardStringListResult.Success(operation, values);
        }

        private WindowsClipboardBytesResult ReadBytes(
            string operation, NativeSizedRead call, bool skipGuard = false)
        {
            if (!skipGuard && !CanRunOperation(operation, out WindowsClipboardErrorCode rejected))
            {
                return WindowsClipboardBytesResult.Failure(operation, rejected);
            }

            ReadOutcome outcome = ReadRaw(operation, call, isByteApi: true);
            if (!outcome.IsSuccess) return WindowsClipboardBytesResult.Failure(operation, outcome.Code);
            if (outcome.IsEmpty) return WindowsClipboardBytesResult.Empty(operation);
            return WindowsClipboardBytesResult.Success(operation, outcome.Data ?? Array.Empty<byte>());
        }

        private readonly struct ReadOutcome
        {
            internal bool IsSuccess { get; }
            internal bool IsEmpty { get; }
            internal string? Text { get; }
            internal byte[]? Data { get; }
            internal WindowsClipboardErrorCode Code { get; }

            internal static ReadOutcome Empty() => new(true, true, null, null, WindowsClipboardErrorCode.None);
            internal static ReadOutcome FromText(string text) =>
                new(true, false, text, null, WindowsClipboardErrorCode.None);
            internal static ReadOutcome FromData(byte[] data) =>
                new(true, false, null, data, WindowsClipboardErrorCode.None);
            internal static ReadOutcome Failed(WindowsClipboardErrorCode code) =>
                new(false, false, null, null, code);

            private ReadOutcome(bool isSuccess, bool isEmpty, string? text, byte[]? data,
                WindowsClipboardErrorCode code)
            {
                IsSuccess = isSuccess;
                IsEmpty = isEmpty;
                Text = text;
                Data = data;
                Code = code;
            }
        }

        /// <summary>
        /// Runs the native two-call read protocol: ask for the size, allocate, then fill.
        /// The error code is classified before the buffer is ever read, on both calls.
        /// </summary>
        private static ReadOutcome ReadRaw(string operation, NativeSizedRead call, bool isByteApi)
        {
            // Deliberately outside the native guard. Nothing here names an import: the two calls go
            // through the delegate the caller supplies, and Marshal is available everywhere. Behind
            // the guard the retry, the budget and the size arithmetic could not be reached by any
            // test, and the editor's answer came from the guard rather than from this protocol.
            try
            {
                for (int attempt = 0; attempt <= SizeChangedRetryBudget; attempt++)
                {
                    uint required = call(IntPtr.Zero, 0, out int sizeError);
                    var sizeCode = (WindowsClipboardErrorCode)sizeError;

                    switch (ClassifyFirstRead(sizeCode, required, isByteApi))
                    {
                        case WindowsClipboardReadDecision.EmptySuccess:
                            return ReadOutcome.Empty();
                        case WindowsClipboardReadDecision.Failure:
                            Debug.LogError($"[{LogTag}][{nameof(ReadRaw)}] {operation} sizing failed: {sizeCode}");
                            return ReadOutcome.Failed(sizeCode);
                    }

                    // A string size counts wchar_t including the terminator, a byte size counts
                    // bytes. The multiplication is checked: an unchecked cast turns a size past two
                    // gigabytes into a negative byte count, and the allocation that follows is then
                    // smaller than what the second call is told it may write into.
                    long byteCount = isByteApi ? required : (long)required * 2;
                    if (byteCount > int.MaxValue)
                    {
                        Debug.LogError(
                            $"[{LogTag}][{nameof(ReadRaw)}] {operation} wants {byteCount} bytes; too large to allocate.");
                        return ReadOutcome.Failed(WindowsClipboardErrorCode.OutOfMemory);
                    }
                    IntPtr buffer = Marshal.AllocHGlobal((int)byteCount);
                    try
                    {
                        uint written = call(buffer, required, out int readError);
                        var readCode = (WindowsClipboardErrorCode)readError;

                        switch (ClassifySecondRead(readCode))
                        {
                            case WindowsClipboardSecondReadDecision.EmptySuccess:
                                return ReadOutcome.Empty();
                            case WindowsClipboardSecondReadDecision.Retry:
                                // The clipboard grew between the two calls; size it again.
                                continue;
                            case WindowsClipboardSecondReadDecision.Failure:
                                Debug.LogError($"[{LogTag}][{nameof(ReadRaw)}] {operation} read failed: {readCode}");
                                return ReadOutcome.Failed(readCode);
                        }

                        if (isByteApi)
                        {
                            var data = new byte[written];
                            if (written > 0) Marshal.Copy(buffer, data, 0, (int)written);
                            return ReadOutcome.FromData(data);
                        }

                        string? text = Marshal.PtrToStringUni(buffer);
                        return text == null ? ReadOutcome.Empty() : ReadOutcome.FromText(text);
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(buffer);
                    }
                }

                Debug.LogError($"[{LogTag}][{nameof(ReadRaw)}] {operation} kept resizing; giving up.");
                return ReadOutcome.Failed(WindowsClipboardErrorCode.BufferTooSmall);
            }
            catch (Exception ex) when (ex is DllNotFoundException || ex is EntryPointNotFoundException)
            {
                Debug.LogError($"[{LogTag}][{nameof(ReadRaw)}] {ex.GetType().Name}: {ex.Message}");
                return ReadOutcome.Failed(WindowsClipboardErrorCode.BridgeUnavailable);
            }
            catch (OutOfMemoryException)
            {
                Debug.LogError($"[{LogTag}][{nameof(ReadRaw)}] {operation} could not allocate its buffer.");
                return ReadOutcome.Failed(WindowsClipboardErrorCode.OutOfMemory);
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Runs the text read, guard included, against a stand-in for the native side, so the rule
        /// that decides what an empty string means can be covered.
        /// </summary>
        internal static WindowsClipboardTextResult ReadTextForTests(
            string operation, Func<IntPtr, uint, (uint written, WindowsClipboardErrorCode code)> fake)
        {
            return ReadText(operation, (IntPtr buffer, uint bufferSize, out int pError) =>
            {
                (uint written, WindowsClipboardErrorCode code) = fake(buffer, bufferSize);
                pError = (int)code;
                return written;
            });
        }

        /// <summary>
        /// Runs the two-call read protocol against a stand-in for the native side.
        /// <para>
        /// The outcome is flattened into a tuple so the protocol can be covered without making the
        /// internal read types part of the package's surface.
        /// </para>
        /// </summary>
        /// <param name="isByteApi">Whether sizes count bytes rather than wchar_t.</param>
        /// <param name="fake">Answers one call: what to write, how much, and with which code.</param>
        /// <returns>The outcome, plus how many times the stand-in was called.</returns>
        internal static (bool isSuccess, bool isEmpty, string? text, byte[]? data,
            WindowsClipboardErrorCode code, int calls) ReadRawForTests(
            bool isByteApi, Func<IntPtr, uint, (uint written, WindowsClipboardErrorCode code)> fake)
        {
            int calls = 0;
            ReadOutcome outcome = ReadRaw(
                "test",
                (IntPtr buffer, uint bufferSize, out int pError) =>
                {
                    calls++;
                    (uint written, WindowsClipboardErrorCode code) = fake(buffer, bufferSize);
                    pError = (int)code;
                    return written;
                },
                isByteApi);
            return (outcome.IsSuccess, outcome.IsEmpty, outcome.Text, outcome.Data,
                outcome.Code, calls);
        }
#endif

        /// <summary>
        /// Runs a native call that reports through pError alone.
        /// </summary>
        private WindowsClipboardResult InvokeWrite(string operation, Func<int, int> call)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            try
            {
                return WindowsClipboardResult.FromNative(operation, call(0));
            }
            catch (Exception ex) when (ex is DllNotFoundException || ex is EntryPointNotFoundException)
            {
                Debug.LogError($"[{LogTag}][{nameof(InvokeWrite)}] {ex.GetType().Name}: {ex.Message}");
                return WindowsClipboardResult.Failure(operation, WindowsClipboardErrorCode.BridgeUnavailable);
            }
#else
            return WindowsClipboardResult.Failure(operation, WindowsClipboardErrorCode.PlatformUnavailable);
#endif
        }

        private static WindowsClipboardTextResult DeliverText(
            WindowsClipboardTextResult result, Action<WindowsClipboardTextResult>? onResult)
        {
            Dispatch(result, _instance?.TextReadCompleted, onResult);
            return result;
        }

        private static WindowsClipboardStringListResult DeliverStringList(
            WindowsClipboardStringListResult result, Action<WindowsClipboardStringListResult>? onResult)
        {
            Dispatch(result, _instance?.StringListReadCompleted, onResult);
            return result;
        }

        private static WindowsClipboardBytesResult DeliverBytes(
            WindowsClipboardBytesResult result, Action<WindowsClipboardBytesResult>? onResult)
        {
            Dispatch(result, _instance?.BytesReadCompleted, onResult);
            return result;
        }

        private static WindowsClipboardFormatPresenceResult DeliverPresence(
            WindowsClipboardFormatPresenceResult result,
            Action<WindowsClipboardFormatPresenceResult>? onResult)
        {
            Dispatch(result, _instance?.FormatPresenceChecked, onResult);
            return result;
        }

        // ── Public API: clipboard history (asynchronous) ─────────────────────────

        /// <summary>
        /// Requests the Windows clipboard history.
        /// </summary>
        /// <param name="onResult">Per-call callback. <see cref="HistoryReadCompleted"/> fires as well.</param>
        /// <returns>
        /// The native request id, or zero when the request was rejected before it was accepted.
        /// The result is delivered exactly once either way.
        /// </returns>
        public uint GetHistory(Action<WindowsClipboardHistoryResult>? onResult = null)
        {
            Debug.Log($"[{LogTag}][{nameof(GetHistory)}] onResult: {onResult != null}");
            return StartRequest(
                OperationGetHistory, WindowsClipboardRequestKind.History, inFlightKey: null,
                onHistory: onResult, onAvailability: null, onStatus: null,
                call: cb => GetHistoryNative(cb, out int e) is var id ? (id, e) : (0u, 0));
        }

        /// <summary>
        /// Requests the history availability flags.
        /// </summary>
        /// <param name="onResult">
        /// Per-call callback. <see cref="HistoryAvailabilityChecked"/> fires as well.
        /// </param>
        /// <returns>The native request id, or zero when the request was rejected.</returns>
        public uint GetHistoryAvailability(Action<WindowsClipboardAvailabilityResult>? onResult = null)
        {
            Debug.Log($"[{LogTag}][{nameof(GetHistoryAvailability)}] onResult: {onResult != null}");
            return StartRequest(
                OperationGetHistoryAvailability, WindowsClipboardRequestKind.Availability, inFlightKey: null,
                onHistory: null, onAvailability: onResult, onStatus: null,
                call: cb => GetHistoryAvailabilityNative(cb, out int e) is var id ? (id, e) : (0u, 0));
        }

        /// <summary>
        /// Restores a history item as the current clipboard content.
        /// <para>
        /// Success also raises <see cref="ClipboardChanged"/>: the Windows history service performs
        /// the write, so it does not go through this manager's self-write suppression.
        /// </para>
        /// </summary>
        /// <param name="itemId">Id taken from a <see cref="WindowsClipboardHistoryItem"/>.</param>
        /// <param name="onResult">Per-call callback. The common event fires as well.</param>
        /// <returns>The native request id, or zero when the request was rejected.</returns>
        public uint RestoreHistoryItem(string itemId, Action<WindowsClipboardResult>? onResult = null)
        {
            Debug.Log($"[{LogTag}][{nameof(RestoreHistoryItem)}] onResult: {onResult != null}");
            if (!ValidateItemId(OperationRestoreHistoryItem, itemId, onResult, out uint rejected)) return rejected;

            return StartRequest(
                OperationRestoreHistoryItem, WindowsClipboardRequestKind.Status,
                inFlightKey: OperationRestoreHistoryItem,
                onHistory: null, onAvailability: null, onStatus: onResult,
                call: cb => RestoreHistoryItemNative(itemId, cb, out int e) is var id ? (id, e) : (0u, 0));
        }

        /// <summary>
        /// Deletes one item from the clipboard history.
        /// </summary>
        /// <param name="itemId">Id taken from a <see cref="WindowsClipboardHistoryItem"/>.</param>
        /// <param name="onResult">Per-call callback. The common event fires as well.</param>
        /// <returns>The native request id, or zero when the request was rejected.</returns>
        public uint DeleteHistoryItem(string itemId, Action<WindowsClipboardResult>? onResult = null)
        {
            Debug.Log($"[{LogTag}][{nameof(DeleteHistoryItem)}] onResult: {onResult != null}");
            if (!ValidateItemId(OperationDeleteHistoryItem, itemId, onResult, out uint rejected)) return rejected;

            return StartRequest(
                OperationDeleteHistoryItem, WindowsClipboardRequestKind.Status,
                inFlightKey: OperationDeleteHistoryItem,
                onHistory: null, onAvailability: null, onStatus: onResult,
                call: cb => DeleteHistoryItemNative(itemId, cb, out int e) is var id ? (id, e) : (0u, 0));
        }

        /// <summary>
        /// Clears the clipboard history. Pinned items stay, which is how Windows behaves.
        /// </summary>
        /// <param name="onResult">Per-call callback. The common event fires as well.</param>
        /// <returns>The native request id, or zero when the request was rejected.</returns>
        public uint ClearUnpinnedHistory(Action<WindowsClipboardResult>? onResult = null)
        {
            Debug.Log($"[{LogTag}][{nameof(ClearUnpinnedHistory)}] onResult: {onResult != null}");
            return StartRequest(
                OperationClearUnpinnedHistory, WindowsClipboardRequestKind.Status,
                inFlightKey: OperationClearUnpinnedHistory,
                onHistory: null, onAvailability: null, onStatus: onResult,
                call: cb => ClearUnpinnedHistoryNative(cb, out int e) is var id ? (id, e) : (0u, 0));
        }

        /// <summary>
        /// Asks the native layer to cancel a request that has not completed yet.
        /// <para>
        /// A request that is already on its way still completes: cancellation is a request, not a
        /// guarantee, and the result arrives exactly once either way.
        /// </para>
        /// </summary>
        /// <param name="requestId">The id returned by the operation that started the request.</param>
        /// <param name="onResult">Per-call callback for the cancel call itself.</param>
        /// <returns>The result of asking, not of the cancellation.</returns>
        public WindowsClipboardResult CancelRequest(
            uint requestId, Action<WindowsClipboardResult>? onResult = null)
        {
            Debug.Log($"[{LogTag}][{nameof(CancelRequest)}] requestId: {requestId}, onResult: {onResult != null}");

            if (!CanRunOperation(OperationCancelRequest, out WindowsClipboardErrorCode rejected))
            {
                return Deliver(WindowsClipboardResult.Failure(OperationCancelRequest, rejected), onResult);
            }
            if (requestId == 0)
            {
                return Deliver(WindowsClipboardResult.Failure(
                    OperationCancelRequest, WindowsClipboardErrorCode.InvalidArgument,
                    "requestId was zero"), onResult);
            }

            return Deliver(InvokeWrite(OperationCancelRequest,
                pError => CancelRequestNative(requestId, out pError)), onResult);
        }

        /// <summary>
        /// Starts or stops watching the Windows clipboard history.
        /// </summary>
        /// <param name="enabled">
        /// True registers <see cref="HistoryChanged"/>, <see cref="HistoryEnabledChanged"/> and
        /// <see cref="RoamingEnabledChanged"/>; false stops watching and clears the registration.
        /// </param>
        /// <param name="onResult">Per-call callback. The common event fires as well.</param>
        /// <returns>
        /// The result. MonitorRegisterFailed can be sticky: when a stop fails to revoke the
        /// underlying event tokens, the native layer refuses to register again until a later stop
        /// succeeds. Retrying later is the only recovery.
        /// </returns>
        public WindowsClipboardResult SetHistoryEventsEnabled(
            bool enabled, Action<WindowsClipboardResult>? onResult = null)
        {
            Debug.Log($"[{LogTag}][{nameof(SetHistoryEventsEnabled)}] enabled: {enabled}, onResult: {onResult != null}");

            if (!CanRunOperation(OperationSetHistoryEvents, out WindowsClipboardErrorCode rejected))
            {
                return Deliver(WindowsClipboardResult.Failure(OperationSetHistoryEvents, rejected), onResult);
            }

            WindowsClipboardResult result = InvokeWrite(OperationSetHistoryEvents, pError =>
                SetHistoryCallbacksNative(
                    enabled ? s_historyChangedDelegate : null,
                    enabled ? s_historyEnabledDelegate : null,
                    enabled ? s_roamingEnabledDelegate : null,
                    out pError));

            return Deliver(result, onResult);
        }

        /// <summary>
        /// Reserves formats the clipboard renders on demand, so a large payload is only produced
        /// when something actually pastes it.
        /// <para>
        /// <b>This empties the clipboard.</b> The native reservation clears the clipboard before
        /// placing its promises, so whatever was there is gone.
        /// </para>
        /// <para>
        /// Each provider runs on the Unity main thread, synchronously, inside the message the
        /// system sends to ask for the format. It must not call Unity APIs, must not touch the
        /// clipboard, must not block, and must not throw: the dispatcher cannot help there, because
        /// its queue only drains from Update. Capture the values it needs by value rather than
        /// reaching for a MonoBehaviour or a Texture, which may already be gone: providers also run
        /// while the application is shutting down.
        /// </para>
        /// <para>
        /// A provider may be asked for its bytes immediately: with Windows clipboard history
        /// enabled, the history service materializes every reserved format as soon as it is
        /// reserved.
        /// </para>
        /// <para>
        /// The reservation only survives while this process owns the clipboard, and the formats are
        /// materialized while the owner window is destroyed, which only happens from a shutdown.
        /// A process that simply exits drops them.
        /// </para>
        /// </summary>
        /// <param name="providers">Format name to byte producer. Must hold at least one entry.</param>
        /// <param name="onResult">Per-call callback. The common event fires as well.</param>
        /// <returns>The result, also delivered to the event and the callback.</returns>
        public WindowsClipboardResult ReserveDeferredFormats(
            IReadOnlyDictionary<string, Func<byte[]>> providers,
            Action<WindowsClipboardResult>? onResult = null)
        {
            Debug.Log($"[{LogTag}][{nameof(ReserveDeferredFormats)}] count: {providers?.Count ?? -1}, onResult: {onResult != null}");

            if (!CanRunOperation(OperationReserveDeferredFormats, out WindowsClipboardErrorCode rejected))
            {
                return Deliver(
                    WindowsClipboardResult.Failure(OperationReserveDeferredFormats, rejected), onResult);
            }
            if (providers == null || providers.Count == 0)
            {
                return Deliver(WindowsClipboardResult.Failure(
                    OperationReserveDeferredFormats, WindowsClipboardErrorCode.InvalidArgument,
                    "providers was null or empty"), onResult);
            }

            var next = new Dictionary<string, Func<byte[]>>();
            foreach (KeyValuePair<string, Func<byte[]>> entry in providers)
            {
                if (string.IsNullOrWhiteSpace(entry.Key))
                {
                    return Deliver(WindowsClipboardResult.Failure(
                        OperationReserveDeferredFormats, WindowsClipboardErrorCode.InvalidArgument,
                        "a format name was null or blank"), onResult);
                }
                if (entry.Value == null)
                {
                    return Deliver(WindowsClipboardResult.Failure(
                        OperationReserveDeferredFormats, WindowsClipboardErrorCode.InvalidArgument,
                        $"the provider of format {entry.Key} was null"), onResult);
                }
                next[entry.Key] = entry.Value;
            }

            string json = WindowsClipboardJsonBuilder.BuildFormatNamesJson(new List<string>(next.Keys));

            // Publish both generations for the duration of the call, so a render request that
            // arrives while the native side is swapping its table still resolves.
            s_renderStaging = Merge(s_renderProviders, next);
            WindowsClipboardResult result;
            try
            {
                result = InvokeWrite(OperationReserveDeferredFormats,
                    pError => ReserveDeferredFormatsNative(json, s_renderDelegate, IntPtr.Zero, out pError));
            }
            finally
            {
                s_renderStaging = null;
            }

            ApplyReservationOutcome(result.ErrorCode, next);
            return Deliver(result, onResult);
        }

        /// <summary>
        /// Retries the recovery the native layer performs after a failed rollback left the deferred
        /// state partial.
        /// </summary>
        /// <param name="onResult">Per-call callback. The common event fires as well.</param>
        /// <returns>
        /// The result. Success does not mean a recovery happened: the native call also reports
        /// success when there was nothing partial to recover, so the providers are kept either way.
        /// </returns>
        public WindowsClipboardResult RecoverDeferredState(Action<WindowsClipboardResult>? onResult = null)
        {
            Debug.Log($"[{LogTag}][{nameof(RecoverDeferredState)}] onResult: {onResult != null}");

            if (!CanRunOperation(OperationRecoverDeferredState, out WindowsClipboardErrorCode rejected))
            {
                return Deliver(
                    WindowsClipboardResult.Failure(OperationRecoverDeferredState, rejected), onResult);
            }

            // Deliberately does not touch the providers or the cache: a success here can mean
            // "recovered", "there was nothing partial", or "the reservation is already gone", and
            // dropping the providers on the strength of that code would strand a live renderer.
            return Deliver(InvokeWrite(OperationRecoverDeferredState,
                pError => RecoverDeferredStateNative(out pError)), onResult);
        }

        // ── Awaitable wrappers ───────────────────────────────────────────────────

        /// <summary>
        /// Awaitable form of <see cref="GetHistory"/>.
        /// </summary>
        /// <param name="cancellationToken">
        /// Cancels the underlying native request. A MonoBehaviour normally passes its
        /// destroyCancellationToken.
        /// </param>
        /// <returns>The same result the callback form delivers. Awaited once only.</returns>
        public Awaitable<WindowsClipboardHistoryResult> GetHistoryAsync(
            CancellationToken cancellationToken = default)
        {
            Debug.Log($"[{LogTag}][{nameof(GetHistoryAsync)}]");
            var source = new AwaitableCompletionSource<WindowsClipboardHistoryResult>();
            if (cancellationToken.IsCancellationRequested)
            {
                RejectRequest(
                    OperationGetHistory, WindowsClipboardRequestKind.History,
                    WindowsClipboardErrorCode.Canceled, null,
                    result => source.TrySetResult(result), null, null);
                return source.Awaitable;
            }

            uint requestId = GetHistory(result => source.TrySetResult(result));
            RegisterCancellation(requestId, cancellationToken);
            return source.Awaitable;
        }

        /// <summary>
        /// Awaitable form of <see cref="GetHistoryAvailability"/>.
        /// </summary>
        /// <param name="cancellationToken">Cancels the underlying native request.</param>
        /// <returns>The same result the callback form delivers. Awaited once only.</returns>
        public Awaitable<WindowsClipboardAvailabilityResult> GetHistoryAvailabilityAsync(
            CancellationToken cancellationToken = default)
        {
            Debug.Log($"[{LogTag}][{nameof(GetHistoryAvailabilityAsync)}]");
            var source = new AwaitableCompletionSource<WindowsClipboardAvailabilityResult>();
            if (cancellationToken.IsCancellationRequested)
            {
                RejectRequest(
                    OperationGetHistoryAvailability, WindowsClipboardRequestKind.Availability,
                    WindowsClipboardErrorCode.Canceled, null,
                    null, result => source.TrySetResult(result), null);
                return source.Awaitable;
            }

            uint requestId = GetHistoryAvailability(result => source.TrySetResult(result));
            RegisterCancellation(requestId, cancellationToken);
            return source.Awaitable;
        }

        /// <summary>
        /// Awaitable form of <see cref="RestoreHistoryItem"/>.
        /// </summary>
        /// <param name="itemId">Id taken from a <see cref="WindowsClipboardHistoryItem"/>.</param>
        /// <param name="cancellationToken">Cancels the underlying native request.</param>
        /// <returns>The same result the callback form delivers. Awaited once only.</returns>
        public Awaitable<WindowsClipboardResult> RestoreHistoryItemAsync(
            string itemId, CancellationToken cancellationToken = default)
        {
            Debug.Log($"[{LogTag}][{nameof(RestoreHistoryItemAsync)}]");
            var source = new AwaitableCompletionSource<WindowsClipboardResult>();
            if (cancellationToken.IsCancellationRequested)
            {
                RejectRequest(
                    OperationRestoreHistoryItem, WindowsClipboardRequestKind.Status,
                    WindowsClipboardErrorCode.Canceled, null,
                    null, null, result => source.TrySetResult(result));
                return source.Awaitable;
            }

            uint requestId = RestoreHistoryItem(itemId, result => source.TrySetResult(result));
            RegisterCancellation(requestId, cancellationToken);
            return source.Awaitable;
        }

        /// <summary>
        /// Awaitable form of <see cref="DeleteHistoryItem"/>.
        /// </summary>
        /// <param name="itemId">Id taken from a <see cref="WindowsClipboardHistoryItem"/>.</param>
        /// <param name="cancellationToken">Cancels the underlying native request.</param>
        /// <returns>The same result the callback form delivers. Awaited once only.</returns>
        public Awaitable<WindowsClipboardResult> DeleteHistoryItemAsync(
            string itemId, CancellationToken cancellationToken = default)
        {
            Debug.Log($"[{LogTag}][{nameof(DeleteHistoryItemAsync)}]");
            var source = new AwaitableCompletionSource<WindowsClipboardResult>();
            if (cancellationToken.IsCancellationRequested)
            {
                RejectRequest(
                    OperationDeleteHistoryItem, WindowsClipboardRequestKind.Status,
                    WindowsClipboardErrorCode.Canceled, null,
                    null, null, result => source.TrySetResult(result));
                return source.Awaitable;
            }

            uint requestId = DeleteHistoryItem(itemId, result => source.TrySetResult(result));
            RegisterCancellation(requestId, cancellationToken);
            return source.Awaitable;
        }

        /// <summary>
        /// Awaitable form of <see cref="ClearUnpinnedHistory"/>.
        /// </summary>
        /// <param name="cancellationToken">Cancels the underlying native request.</param>
        /// <returns>The same result the callback form delivers. Awaited once only.</returns>
        public Awaitable<WindowsClipboardResult> ClearUnpinnedHistoryAsync(
            CancellationToken cancellationToken = default)
        {
            Debug.Log($"[{LogTag}][{nameof(ClearUnpinnedHistoryAsync)}]");
            var source = new AwaitableCompletionSource<WindowsClipboardResult>();
            if (cancellationToken.IsCancellationRequested)
            {
                RejectRequest(
                    OperationClearUnpinnedHistory, WindowsClipboardRequestKind.Status,
                    WindowsClipboardErrorCode.Canceled, null,
                    null, null, result => source.TrySetResult(result));
                return source.Awaitable;
            }

            uint requestId = ClearUnpinnedHistory(result => source.TrySetResult(result));
            RegisterCancellation(requestId, cancellationToken);
            return source.Awaitable;
        }

        // ── Deferred rendering internals ─────────────────────────────────────────

        /// <summary>
        /// Decides which providers stay live after a reservation attempt.
        /// <para>
        /// The decision is made from the error code alone. The native layer reports two different
        /// failure sites with the same Unknown code - one that leaves the previous renderers in
        /// place and one that clears them - so a rule that tried to tell them apart could not be
        /// implemented. Keeping the previous generation is safe for both: the native side never
        /// holds the new one on those paths, and providers nothing asks for cost only memory,
        /// while a missing provider silently drops a reserved format.
        /// </para>
        /// </summary>
        /// <param name="code">The code the reservation reported.</param>
        /// <param name="next">The generation this call tried to install.</param>
        internal static void ApplyReservationOutcome(
            WindowsClipboardErrorCode code, Dictionary<string, Func<byte[]>> next)
        {
            switch (code)
            {
                case WindowsClipboardErrorCode.None:
                    s_renderProviders = next;
                    s_renderCache.Clear();
                    break;

                case WindowsClipboardErrorCode.PartialState:
                    // The native side kept the new table, so it wins; the previous formats it does
                    // not name may still be asked for.
                    s_renderProviders = Merge(s_renderProviders, next);
                    s_renderCache.Clear();
                    break;

                default:
                    // The previous generation stays exactly as it was, and so does its cache.
                    break;
            }
        }

        private static Dictionary<string, Func<byte[]>> Merge(
            Dictionary<string, Func<byte[]>> baseline, Dictionary<string, Func<byte[]>> overrides)
        {
            var merged = new Dictionary<string, Func<byte[]>>(baseline);
            foreach (KeyValuePair<string, Func<byte[]>> entry in overrides) merged[entry.Key] = entry.Value;
            return merged;
        }

        private static bool TryResolveProvider(string formatName, out Func<byte[]>? provider)
        {
            Dictionary<string, Func<byte[]>>? staging = s_renderStaging;
            if (staging != null && staging.TryGetValue(formatName, out provider)) return true;
            return s_renderProviders.TryGetValue(formatName, out provider);
        }

        /// <summary>
        /// Produces the bytes of one reserved format, in the two phases the native side asks for.
        /// <para>
        /// The size the first phase reports and the size the second phase writes have to match
        /// exactly: the native layer drops a format whose second answer differs, without an error.
        /// The bytes are therefore produced once and cached, never regenerated.
        /// </para>
        /// </summary>
        internal static uint RenderDeferredFormat(
            string formatName, IntPtr buffer, uint bufferSize, out uint requiredSize)
        {
            requiredSize = 0;
            try
            {
                if (buffer == IntPtr.Zero)
                {
                    if (!TryResolveProvider(formatName, out Func<byte[]>? provider) || provider == null)
                    {
                        Debug.LogError($"[{LogTag}][{nameof(RenderDeferredFormat)}] no provider for {formatName}");
                        return (uint)WindowsClipboardErrorCode.InvalidParameter;
                    }

                    byte[] produced = provider() ?? Array.Empty<byte>();
                    if (produced.Length == 0)
                    {
                        // A zero-length payload cannot be placed, and answering zero would make the
                        // native side drop the format without saying why.
                        Debug.LogError($"[{LogTag}][{nameof(RenderDeferredFormat)}] {formatName} produced no bytes");
                        return (uint)WindowsClipboardErrorCode.InvalidData;
                    }

                    s_renderCache[formatName] = produced;
                    requiredSize = (uint)produced.Length;
                    return (uint)WindowsClipboardErrorCode.BufferTooSmall;
                }

                if (!s_renderCache.TryGetValue(formatName, out byte[]? cached))
                {
                    // Regenerating here could yield a different length than the first phase
                    // promised, which the native side discards silently. Failing loudly is better.
                    Debug.LogError($"[{LogTag}][{nameof(RenderDeferredFormat)}] no cached payload for {formatName}");
                    return (uint)WindowsClipboardErrorCode.Unknown;
                }

                requiredSize = (uint)cached.Length;
                if (bufferSize < cached.Length)
                {
                    return (uint)WindowsClipboardErrorCode.BufferTooSmall;
                }

                Marshal.Copy(cached, 0, buffer, cached.Length);
                return (uint)WindowsClipboardErrorCode.None;
            }
            catch (Exception ex)
            {
                // out parameters are not written back when an exception leaves the method, so the
                // size is reset explicitly before reporting the failure.
                requiredSize = 0;
                Debug.LogError($"[{LogTag}][{nameof(RenderDeferredFormat)}] {formatName}: {ex.GetType().Name}");
                return (uint)WindowsClipboardErrorCode.Unknown;
            }
        }

        // ── Request plumbing ─────────────────────────────────────────────────────

        private bool ValidateItemId(
            string operation, string itemId, Action<WindowsClipboardResult>? onResult, out uint rejected)
        {
            rejected = 0;
            if (!string.IsNullOrWhiteSpace(itemId)) return true;

            RejectRequest(operation, WindowsClipboardRequestKind.Status,
                WindowsClipboardErrorCode.InvalidArgument, "itemId was null or blank",
                null, null, onResult);
            return false;
        }

        /// <summary>
        /// Starts one asynchronous request: runs the guards, issues a ticket, calls the native
        /// entry point, and records the request so its single completion can find its way back.
        /// </summary>
        private uint StartRequest(
            string operation,
            WindowsClipboardRequestKind kind,
            string? inFlightKey,
            Action<WindowsClipboardHistoryResult>? onHistory,
            Action<WindowsClipboardAvailabilityResult>? onAvailability,
            Action<WindowsClipboardResult>? onStatus,
            Func<ClipboardRequestCallback, (uint requestId, int pError)> call)
        {
            if (!CanRunOperation(operation, out WindowsClipboardErrorCode rejected))
            {
                RejectRequest(operation, kind, rejected, null, onHistory, onAvailability, onStatus);
                return 0;
            }
            if (inFlightKey != null && s_inFlight.Contains(inFlightKey))
            {
                // The running call keeps its result; the new caller is told to try later rather
                // than having the two completions race for the same meaning.
                RejectRequest(operation, kind, WindowsClipboardErrorCode.OperationBusy, null,
                    onHistory, onAvailability, onStatus);
                return 0;
            }

            uint ticket = s_registry.IssueTicket();
            var pending = new PendingRequest
            {
                Operation = operation,
                Kind = kind,
                InFlightKey = inFlightKey,
                OnHistory = onHistory,
                OnAvailability = onAvailability,
                OnStatus = onStatus
            };
            s_pending[ticket] = pending;
            if (inFlightKey != null) s_inFlight.Add(inFlightKey);

            // Initialized because the editor seam below can supply them instead of the call, and
            // the compiler cannot see that one of the two always runs.
            uint requestId = 0;
            int pError = 0;
            bool answered = false;
#if UNITY_EDITOR
            if (NextNativeRequestIdForTests is uint injected)
            {
                // Consumed rather than left standing, so a test can start two requests under two
                // different ids and cover the concurrency the design allows for the read-only
                // operations. Everything after this point is the real path, rejection included.
                NextNativeRequestIdForTests = null;
                requestId = injected;
                pError = 0;
                answered = true;
            }
#endif
            // Skipped only when a test already supplied what the bridge would have answered.
            if (!answered)
            {
                try
                {
#if UNITY_EDITOR
                    if (RequestExceptionForTests != null)
                    {
                        Exception forcedFailure = RequestExceptionForTests;
                        RequestExceptionForTests = null;
                        throw forcedFailure;
                    }
#endif
                    (requestId, pError) = call(s_requestDelegate);
                }
                catch (Exception ex)
                {
                    // Every exception, not only the two that name a missing bridge. The ticket is
                    // not in the registry yet, so anything that escapes here would strand the
                    // in-flight marker as well as the caller: the teardown drain has nothing to
                    // find, and the operation would report Busy for the rest of the session.
                    Debug.LogError($"[{LogTag}][{nameof(StartRequest)}] {ex.GetType().Name}: {ex.Message}");
                    ResolveAndQueue(
                        ticket,
                        ex is DllNotFoundException || ex is EntryPointNotFoundException
                            ? WindowsClipboardErrorCode.BridgeUnavailable
                            : WindowsClipboardErrorCode.Unknown,
                        null);
                    return 0;
                }
            }

            if (requestId == 0)
            {
                // Rejected before acceptance: the native callback will never fire, so this layer
                // owns the single delivery.
                var code = (WindowsClipboardErrorCode)pError;
                if (code == WindowsClipboardErrorCode.None) code = WindowsClipboardErrorCode.RequestRejected;
                ResolveAndQueue(ticket, code, null);
                return 0;
            }

            pending.NativeRequestId = requestId;
            s_registry.RegisterAwaitingNative(ticket, requestId);
            return requestId;
        }

        /// <summary>
        /// Records a rejection that never reached the native side and queues its delivery, so a
        /// rejected request is still delivered exactly once and an awaiting caller completes.
        /// </summary>
        private void RejectRequest(
            string operation,
            WindowsClipboardRequestKind kind,
            WindowsClipboardErrorCode code,
            string? detail,
            Action<WindowsClipboardHistoryResult>? onHistory,
            Action<WindowsClipboardAvailabilityResult>? onAvailability,
            Action<WindowsClipboardResult>? onStatus)
        {
            uint ticket = s_registry.IssueTicket();
            s_pending[ticket] = new PendingRequest
            {
                Operation = operation,
                Kind = kind,
                Code = code,
                Json = detail,
                OnHistory = onHistory,
                OnAvailability = onAvailability,
                OnStatus = onStatus
            };
            s_registry.RegisterUndelivered(ticket);
            QueueDelivery(ticket);
        }

        private static void ResolveAndQueue(uint ticket, WindowsClipboardErrorCode code, string? json)
        {
            if (s_pending.TryGetValue(ticket, out PendingRequest? pending))
            {
                pending.Code = code;
                pending.Json = json;
            }
            s_registry.RegisterUndelivered(ticket);
            QueueDelivery(ticket);
        }

        private static void QueueDelivery(uint ticket)
        {
            UnityMainThreadDispatcher? dispatcher = s_dispatcher;
            if (dispatcher == null)
            {
                // Delivering in place here would run the callback before the call that started it
                // had returned, which the contract rules out, and a callback that starts another
                // request would re-enter this layer mid-update. The ticket stays claimable, so the
                // teardown drain still owes it its one delivery.
                Debug.LogWarning(
                    $"[{LogTag}][{nameof(QueueDelivery)}] no dispatcher yet; " +
                    $"request {ticket} waits for the teardown drain.");
                return;
            }
            dispatcher.Enqueue(() => DeliverIfClaimed(ticket));
        }

        /// <summary>
        /// Gives a cancellation registration back without waiting for a callback that may already
        /// be running.
        /// <para>
        /// Dispose blocks until a registered callback running on another thread returns. Delivery
        /// runs from the dispatcher, which holds its queue lock while it invokes what it dequeued,
        /// and the callback needs that same lock to hand its work to the main thread. Disposing
        /// here would leave the two waiting on each other and hang the main thread, so the wait is
        /// handed to the pool instead.
        /// </para>
        /// </summary>
        private static void ReleaseCancellationRegistration(PendingRequest pending)
        {
            CancellationTokenRegistration registration = pending.Registration;
            if (registration == default) return;

            pending.Registration = default;
#if UNITY_EDITOR
            // Held open on request, so a test can reach the window this matching exists for: a
            // registration outliving the request it belonged to, because the disposal is not
            // instant. Without it the pool wins the race and the window never opens.
            if (SuppressRegistrationDisposalForTests) return;
#endif
            ThreadPool.QueueUserWorkItem(_ =>
            {
                registration.Dispose();
#if UNITY_EDITOR
                Interlocked.Increment(ref CancellationRegistrationsReleasedForTests);
#endif
            });
        }

        private static void DeliverIfClaimed(uint ticket)
        {
            // Whoever wins the claim delivers. A teardown that already drained this ticket makes
            // the queued action a no-op instead of a second delivery.
            if (!s_registry.TryClaim(ticket)) return;
            DeliverClaimed(ticket, null);
        }

        /// <summary>
        /// Delivers a request whose ticket the caller has already claimed.
        /// </summary>
        /// <param name="ticket">The claimed ticket.</param>
        /// <param name="overrideCode">Set by a teardown that has to end a request that never completed.</param>
        private static void DeliverClaimed(uint ticket, WindowsClipboardErrorCode? overrideCode)
        {
            if (!s_pending.TryGetValue(ticket, out PendingRequest? pending)) return;
            s_pending.Remove(ticket);

            if (pending.InFlightKey != null) s_inFlight.Remove(pending.InFlightKey);
            ReleaseCancellationRegistration(pending);

            WindowsClipboardErrorCode code = overrideCode ?? pending.Code;

            switch (pending.Kind)
            {
                case WindowsClipboardRequestKind.History:
                {
                    WindowsClipboardHistoryResult result;
                    if (code != WindowsClipboardErrorCode.None)
                    {
                        result = WindowsClipboardHistoryResult.Failure(pending.Operation, code, pending.Json);
                    }
                    else if (WindowsClipboardJsonParser.TryParseHistoryItems(
                                 pending.Json, out IReadOnlyList<WindowsClipboardHistoryItem> items))
                    {
                        result = WindowsClipboardHistoryResult.Success(pending.Operation, items);
                    }
                    else
                    {
                        result = WindowsClipboardHistoryResult.Failure(
                            pending.Operation, WindowsClipboardErrorCode.ResultParseFailed);
                    }
                    InvokeInOrder(result, _instance?.HistoryReadCompleted, pending.OnHistory);
                    break;
                }

                case WindowsClipboardRequestKind.Availability:
                {
                    WindowsClipboardAvailabilityResult result;
                    if (code != WindowsClipboardErrorCode.None)
                    {
                        result = WindowsClipboardAvailabilityResult.Failure(pending.Operation, code, pending.Json);
                    }
                    else if (WindowsClipboardJsonParser.TryParseAvailability(
                                 pending.Json, out bool historyEnabled, out bool roamingEnabled))
                    {
                        result = WindowsClipboardAvailabilityResult.Success(
                            pending.Operation, historyEnabled, roamingEnabled);
                    }
                    else
                    {
                        result = WindowsClipboardAvailabilityResult.Failure(
                            pending.Operation, WindowsClipboardErrorCode.ResultParseFailed);
                    }
                    InvokeInOrder(result, _instance?.HistoryAvailabilityChecked, pending.OnAvailability);
                    break;
                }

                default:
                {
                    WindowsClipboardResult result = code == WindowsClipboardErrorCode.None
                        ? WindowsClipboardResult.Success(pending.Operation)
                        : WindowsClipboardResult.Failure(pending.Operation, code, pending.Json);
                    InvokeInOrder(result, _instance?.ClipboardOperationCompleted, pending.OnStatus);
                    break;
                }
            }
        }

        /// <summary>
        /// The native completion callback. Runs on the owner UI thread, exactly once per accepted
        /// request.
        /// </summary>
        [MonoPInvokeCallback(typeof(ClipboardRequestCallback))]
        private static void OnRequestCompletedNative(uint requestId, int error, string? json)
        {
            try
            {
                if (!s_registry.TryResolveNativeId(requestId, out uint ticket))
                {
                    // An id this layer no longer tracks: already delivered, or drained by a
                    // teardown. Dropping it is the correct outcome, not an error.
                    Debug.LogWarning($"[{LogTag}][{nameof(OnRequestCompletedNative)}] unknown request id {requestId}");
                    return;
                }

                if (s_pending.TryGetValue(ticket, out PendingRequest? pending))
                {
                    pending.Code = (WindowsClipboardErrorCode)error;
                    pending.Json = json;
                }
                // Move to Undelivered rather than removing: a teardown before the queued delivery
                // runs still has to find this result.
                s_registry.MarkUndelivered(ticket);
                QueueDelivery(ticket);
            }
            catch (Exception ex)
            {
                // Nothing may cross the C ABI boundary.
                Debug.LogError($"[{LogTag}][{nameof(OnRequestCompletedNative)}] {ex.Message}");
            }
        }

        /// <summary>
        /// Marshals a cancellation onto the main thread and only cancels the request it was
        /// registered for.
        /// </summary>
        private void RegisterCancellation(uint requestId, CancellationToken cancellationToken)
        {
            if (!cancellationToken.CanBeCanceled) return;

            if (requestId == 0)
            {
                // Already rejected; its delivery is queued and needs no cancellation.
                return;
            }

            // Resolved before registering, so the callback can name the request it belongs to.
            // Matching on the native id alone is not enough: the native side reuses ids, and this
            // registration can outlive its own request, because the disposal that would have taken
            // it away is handed to the pool and may not have run yet.
            if (!s_registry.TryResolveNativeId(requestId, out uint ticket) ||
                !s_pending.TryGetValue(ticket, out PendingRequest? pending))
            {
                // Already delivered; there is nothing left to cancel.
                return;
            }

            pending.Registration = cancellationToken.Register(() =>
            {
                // The callback runs on whichever thread cancelled, and the native API is main
                // thread only here, so hop before touching it.
                UnityMainThreadDispatcher? dispatcher = s_dispatcher;
                if (dispatcher == null) return;
                dispatcher.Enqueue(() =>
                {
                    // Cancel only while this id still belongs to the ticket registered here.
                    if (!s_registry.TryResolveNativeId(requestId, out uint owner)) return;
                    if (owner != ticket) return;
                    CancelRequest(requestId);
                });
            });
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
        private WindowsClipboardResult InvokeNativeShutdown(out bool completed)
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
            catch (Exception ex)
            {
                // Every exception, not only the two that name a missing bridge. OnDestroy calls
                // this and has nowhere to put a throw, so anything escaping here would surface as
                // an unhandled exception during teardown and skip the rest of it.
                Debug.LogError($"[{LogTag}][{nameof(InvokeNativeShutdown)}] {ex.GetType().Name}: {ex.Message}");
                completed = false;
                return WindowsClipboardResult.Failure(
                    OperationShutdown,
                    ex is DllNotFoundException || ex is EntryPointNotFoundException
                        ? WindowsClipboardErrorCode.BridgeUnavailable
                        : WindowsClipboardErrorCode.Unknown);
            }
#else
#if UNITY_EDITOR
            if (NativeShutdownForTests != null)
            {
                (bool forced, WindowsClipboardErrorCode forcedCode) = NativeShutdownForTests();
                completed = forced;
                return forced
                    ? WindowsClipboardResult.Success(OperationShutdown)
                    : WindowsClipboardResult.Failure(OperationShutdown, forcedCode);
            }
#endif
            // Nothing was ever initialized here, so there is nothing to release. Reporting a
            // failure instead would make every editor teardown look like a terminal shutdown
            // failure, which is what that classification is meant to flag.
            completed = true;
            return WindowsClipboardResult.Success(OperationShutdown);
#endif
        }

        /// <summary>
        /// Runs one shutdown attempt together with everything that has to follow it.
        /// <para>
        /// The native call and the termination are deliberately not separable. An earlier revision
        /// exposed them separately, and the drain loop then called the attempt on its own and
        /// reached the termination only once at the end, so the manager kept reporting Running for
        /// the whole drain and let new operations through.
        /// </para>
        /// </summary>
        /// <param name="origin">What started the shutdown, for the diagnostics.</param>
        /// <param name="completed">True when the native manager finished releasing everything.</param>
        /// <param name="progress">Whether to stop, retry, or give up.</param>
        /// <returns>The attempt's result.</returns>
        private WindowsClipboardResult RunShutdownAttempt(
            ShutdownOrigin origin, out bool completed, out WindowsClipboardShutdownProgress progress)
        {
            WindowsClipboardResult result = InvokeNativeShutdown(out completed);
            progress = FinishShutdownAttempt(origin, result, completed);
            return result;
        }

        /// <summary>
        /// The single place every shutdown attempt ends, whatever started it.
        /// Advances the state, drains the request registry, and releases what may be released.
        /// </summary>
        /// <returns>How the attempt was classified.</returns>
        private static WindowsClipboardShutdownProgress FinishShutdownAttempt(
            ShutdownOrigin origin, WindowsClipboardResult result, bool completed)
        {
            // The first attempt closes the door before anything else, so a callback cannot start a
            // new operation that would outlive the shutdown.
            if (s_state == WindowsClipboardManagerState.Running)
            {
                s_state = WindowsClipboardManagerState.Draining;
            }

            // Unconditional. A request rejected while the manager was never initialized still holds
            // a queued delivery, and its caller is owed that delivery whatever state we are in.
            // ClaimAll empties the registry, so repeating this on every attempt costs nothing.
            DrainRequestRegistry();

            WindowsClipboardShutdownProgress progress = ClassifyShutdown(completed, result.ErrorCode);

            // A manager that never started, or that already finished, has nothing left to advance.
            // Running the release again would report a second completion for the same shutdown, and
            // classifying an idempotent native uninit would flag a terminal failure that is not one.
            if (s_state == WindowsClipboardManagerState.Uninitialized ||
                s_state == WindowsClipboardManagerState.ShutDown)
            {
                return progress;
            }

            switch (progress)
            {
                case WindowsClipboardShutdownProgress.Completed:
                    ReleaseOwnedComReference();
                    // Only now: an unfinished shutdown means the native side can still send
                    // WM_RENDERALLFORMATS, and dropping the providers first loses those formats.
                    s_renderProviders = new Dictionary<string, Func<byte[]>>();
                    s_renderCache.Clear();
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

            return progress;
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

        /// <summary>
        /// Opens a drain. The first attempt happens on the next Update, never inside this call.
        /// </summary>
        private static DrainSession StartDrain()
        {
            var session = new DrainSession
            {
                Generation = ++s_drainGeneration,
                Deadline = Time.realtimeSinceStartup + s_retrySecondBudget,
                Result = WindowsClipboardResult.Success(OperationShutdown)
            };
            s_drain = session;
            return session;
        }

        /// <summary>
        /// Drives the drain, one attempt per frame.
        /// <para>
        /// Deliberately not a coroutine. A coroutine dies with StopAllCoroutines or a disabled
        /// object, silently and with no transition anyone can observe, which left callers waiting
        /// on a drain that no longer existed. Update either runs or the object is gone, and the
        /// second case has its own transition.
        /// </para>
        /// </summary>
        private void Update()
        {
            DrainSession? session = s_drain;
            if (session == null || _instance != this) return;
            if (session.Phase != WindowsClipboardDrainPhase.Running) return;

            AdvanceDrain(session, force: false);
        }

        /// <summary>
        /// Makes one shutdown attempt and decides whether the drain is over.
        /// </summary>
        /// <param name="session">The drain in flight.</param>
        /// <param name="force">
        /// True when this is the only attempt there will be, because nothing will call Update again.
        /// </param>
        private void AdvanceDrain(DrainSession session, bool force)
        {
            try
            {
                session.Attempts++;
                session.Result = RunShutdownAttempt(
                    ShutdownOrigin.Drain, out _, out WindowsClipboardShutdownProgress progress);
                session.Progress = progress;

                if (progress != WindowsClipboardShutdownProgress.NotYet)
                {
                    FinishDrain(session);
                    return;
                }

                // A half-applied reservation makes the native uninit refuse to finish for as long
                // as it stands, so recovering from it is part of the drain rather than something
                // the caller has to know to do. Once only: a second call would spend the budget on
                // an answer we already have.
                if (!session.RecoveryTried &&
                    session.Result.ErrorCode == WindowsClipboardErrorCode.PartialState)
                {
                    session.RecoveryTried = true;
                    var recovery = (WindowsClipboardErrorCode)RecoverDeferredStateNative(out _);
                    Debug.Log($"[{LogTag}][{nameof(AdvanceDrain)}] partial state; recovery reported: {recovery}");
                }

                bool spent = session.Attempts >= s_retryFrameBudget ||
                             Time.realtimeSinceStartup >= session.Deadline;
                if (force || spent)
                {
                    Debug.LogError(
                        $"[{LogTag}][{nameof(AdvanceDrain)}] shutdown exceeded its budget after {session.Attempts} attempts.");
                    session.Result = WindowsClipboardResult.Failure(
                        OperationShutdown, WindowsClipboardErrorCode.ShutdownTimeout);
                    session.Progress = WindowsClipboardShutdownProgress.Terminal;
                    // The last attempt left the state at Draining, which claims a shutdown is still
                    // making progress. It is not, and operations must keep being refused.
                    s_state = WindowsClipboardManagerState.ShutdownFailed;
                    FinishDrain(session);
                    return;
                }

                Debug.Log($"[{LogTag}][{nameof(AdvanceDrain)}] attempt {session.Attempts} not finished yet: {session.Result.ErrorCode}");
            }
            catch (Exception ex)
            {
                // An attempt that throws must still end the drain. Leaving it Running would hold
                // every waiter, and any quit, on something that will never progress.
                Debug.LogError($"[{LogTag}][{nameof(AdvanceDrain)}] {ex.GetType().Name}: {ex.Message}");
                session.Result = WindowsClipboardResult.Failure(
                    OperationShutdown, WindowsClipboardErrorCode.Unknown);
                session.Progress = WindowsClipboardShutdownProgress.Terminal;
                FinishDrain(session);
            }
        }

        /// <summary>
        /// Ends a drain that reached a conclusion, and hands the result over or queues it.
        /// </summary>
        private static void FinishDrain(DrainSession session)
        {
            // A quit resumes right after this and the application may be gone before the dispatcher
            // runs again, so the result is handed over on this stack rather than queued.
            if (s_quitState == WindowsClipboardQuitState.WaitingForDrain)
            {
                s_deliveringBeforeQuit = true;
                try
                {
                    SettleDrain(session);
                }
                finally
                {
                    s_deliveringBeforeQuit = false;
                }
                ResumeQuit(session.Progress, session.Result);
                return;
            }

            UnityMainThreadDispatcher? dispatcher = s_dispatcher;
            if (dispatcher == null)
            {
                SettleDrain(session);
                return;
            }

            // The session stays until the delivery is claimed, so a teardown in between can take
            // it over instead of finding no trace of a result that was never handed out.
            session.Phase = WindowsClipboardDrainPhase.DeliveryQueued;
            uint generation = session.Generation;
            dispatcher.Enqueue(() => ClaimDrainDelivery(generation));
        }

        /// <summary>
        /// Delivers a queued drain result, unless a teardown already claimed it.
        /// </summary>
        private static void ClaimDrainDelivery(uint generation)
        {
            DrainSession? session = s_drain;
            if (session == null || session.Generation != generation) return;
            if (session.Phase != WindowsClipboardDrainPhase.DeliveryQueued) return;
            SettleDrain(session);
        }

        /// <summary>
        /// Ends a drain that will not finish: the manager is going away, so nothing will call
        /// Update again and nothing will run a queued delivery.
        /// </summary>
        private static void AbortDrain()
        {
            DrainSession? session = s_drain;
            if (session == null) return;

            if (session.Progress == WindowsClipboardShutdownProgress.NotYet)
            {
                // No attempt ever concluded, so the drain ran out of time rather than finishing.
                session.Result = WindowsClipboardResult.Failure(
                    OperationShutdown, WindowsClipboardErrorCode.ShutdownTimeout);
                session.Progress = WindowsClipboardShutdownProgress.Terminal;
            }

            bool quitting = s_quitState == WindowsClipboardQuitState.WaitingForDrain;
            s_deliveringBeforeQuit = quitting;
            try
            {
                SettleDrain(session);
            }
            finally
            {
                s_deliveringBeforeQuit = false;
            }

            if (quitting)
            {
                // The quit was refused once and nothing else is left to let it through.
                ResumeQuit(session.Progress, session.Result);
            }
        }

        /// <summary>
        /// Hands a finished drain's result to everyone waiting on it, exactly once.
        /// </summary>
        /// <remarks>
        /// The single exit of a drain, whatever ended it. The session is cleared only at the end,
        /// so a caller arriving while this runs joins the same pass instead of waiting on a
        /// delivery that has already happened.
        /// </remarks>
        private static void SettleDrain(DrainSession session)
        {
            if (!ReferenceEquals(s_drain, session)) return;
            session.Phase = WindowsClipboardDrainPhase.Settling;

            WindowsClipboardResult result = session.Result;
            if (session.DeliveryRequested)
            {
                try
                {
                    _instance?.ClipboardOperationCompleted?.Invoke(result);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[{LogTag}][{nameof(SettleDrain)}] a common event subscriber threw: {ex.Message}");
                }
            }

            // Indexed rather than foreach: a callback may call ShutdownWithDrain again, and while
            // this drain is settling that caller joins this list and has to be answered here too.
            for (int i = 0; i < session.Waiters.Count; i++)
            {
                // Contained one by one: every caller of ShutdownWithDrain is owed the result it
                // asked for, whatever the caller before it did with its own.
                try
                {
                    session.Waiters[i](result);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[{LogTag}][{nameof(SettleDrain)}] a drain callback threw: {ex.Message}");
                }
            }

            if (ReferenceEquals(s_drain, session)) s_drain = null;
        }

        /// <summary>
        /// Delivers every request the registry still holds, synchronously.
        /// <para>
        /// The dispatcher is deliberately bypassed: a teardown runs when no further Update is
        /// guaranteed, so a queued delivery would never arrive and an awaiting caller would hang.
        /// Both states are drained - a request still waiting for the native side, and one whose
        /// result is known but whose delivery is still queued.
        /// </para>
        /// </summary>
        private static void DrainRequestRegistry()
        {
            IReadOnlyList<KeyValuePair<uint, WindowsClipboardRequestState>> claimed = s_registry.ClaimAll();
            if (claimed.Count == 0) return;

            Debug.Log($"[{LogTag}][{nameof(DrainRequestRegistry)}] draining {claimed.Count} request(s)");
            foreach (KeyValuePair<uint, WindowsClipboardRequestState> entry in claimed)
            {
                // A request that never completed ends as canceled; one that already has an outcome
                // keeps it, because that outcome is what the caller was about to receive.
                WindowsClipboardErrorCode? overrideCode =
                    entry.Value == WindowsClipboardRequestState.AwaitingNative
                        ? WindowsClipboardErrorCode.Canceled
                        : (WindowsClipboardErrorCode?)null;
                DeliverClaimed(entry.Key, overrideCode);
            }
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
            Debug.Log($"[{LogTag}][{nameof(OnWantsToQuit)}] state: {s_quitState}");

            if (s_quitState == WindowsClipboardQuitState.Resumed) return true;

            // A drain is already carrying this quit and will resume it when it ends.
            if (s_quitState == WindowsClipboardQuitState.WaitingForDrain) return false;

            if (_instance == null)
            {
                // Nothing can drive a drain, so let the quit through rather than hanging.
                s_quitState = WindowsClipboardQuitState.Resumed;
                return true;
            }

            s_quitState = WindowsClipboardQuitState.WaitingForDrain;
            DrainSession session = s_drain ?? StartDrain();

            // A disabled manager gets no Update, so the drain would never advance and the quit
            // would never be let through. Refusing to exit is worse than shutting down without
            // the retries, so take the one attempt that is possible and go.
            if (!_instance.isActiveAndEnabled && session.Phase == WindowsClipboardDrainPhase.Running)
            {
                Debug.LogWarning(
                    $"[{LogTag}][{nameof(OnWantsToQuit)}] the manager is not active; " +
                    "draining in one attempt instead of across frames.");
                _instance.AdvanceDrain(session, force: true);
                return s_quitState == WindowsClipboardQuitState.Resumed;
            }

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

            s_quitState = WindowsClipboardQuitState.Resumed;
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
        /// Invokes the common event and then the per-call callback, containing the exceptions of
        /// each so that one cannot swallow the other's result.
        /// <para>
        /// The isolation is between those two, not between the subscribers of the event: they share
        /// one invocation, so the first to throw stops the ones behind it. That is the behaviour
        /// every manager in this package has, and changing it belongs to all of them at once
        /// rather than here.
        /// </para>
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

            // A queued delivery needs a later Update, and a quit that is about to go through means
            // there will not be one.
            if (s_deliveringBeforeQuit)
            {
                InvokeInOrder(result, common, perCall);
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

        // ── Native call wrappers ─────────────────────────────────────────────────

        private static uint GetHistoryNative(ClipboardRequestCallback cb, out int pError)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            return getClipboardHistory(cb, out pError);
#else
            pError = (int)WindowsClipboardErrorCode.PlatformUnavailable;
            return 0;
#endif
        }

        private static uint GetHistoryAvailabilityNative(ClipboardRequestCallback cb, out int pError)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            return getClipboardHistoryAvailability(cb, out pError);
#else
            pError = (int)WindowsClipboardErrorCode.PlatformUnavailable;
            return 0;
#endif
        }

        private static uint RestoreHistoryItemNative(
            string itemId, ClipboardRequestCallback cb, out int pError)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            return restoreHistoryItem(itemId, cb, out pError);
#else
            pError = (int)WindowsClipboardErrorCode.PlatformUnavailable;
            return 0;
#endif
        }

        private static uint DeleteHistoryItemNative(
            string itemId, ClipboardRequestCallback cb, out int pError)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            return deleteHistoryItem(itemId, cb, out pError);
#else
            pError = (int)WindowsClipboardErrorCode.PlatformUnavailable;
            return 0;
#endif
        }

        private static uint ClearUnpinnedHistoryNative(ClipboardRequestCallback cb, out int pError)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            return clearUnpinnedHistory(cb, out pError);
#else
            pError = (int)WindowsClipboardErrorCode.PlatformUnavailable;
            return 0;
#endif
        }

        private static int ReserveDeferredFormatsNative(
            string formatNamesJson, ClipboardRenderCallback provider, IntPtr context, out int pError)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            reserveDeferredFormats(formatNamesJson, provider, context, out pError);
#else
            pError = (int)WindowsClipboardErrorCode.PlatformUnavailable;
#endif
            return pError;
        }

        private static int RecoverDeferredStateNative(out int pError)
        {
#if UNITY_EDITOR
            RecoverDeferredCallCountForTests++;
#endif
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            recoverDeferredState(out pError);
#else
            pError = (int)WindowsClipboardErrorCode.PlatformUnavailable;
#endif
            return pError;
        }

        private static int SetHistoryCallbacksNative(
            ClipboardHistoryChangedCallback? onHistoryChanged,
            ClipboardFlagChangedCallback? onHistoryEnabledChanged,
            ClipboardFlagChangedCallback? onRoamingEnabledChanged,
            out int pError)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            setClipboardHistoryCallbacks(
                onHistoryChanged, onHistoryEnabledChanged, onRoamingEnabledChanged, out pError);
#else
            pError = (int)WindowsClipboardErrorCode.PlatformUnavailable;
#endif
            return pError;
        }

        private static int CancelRequestNative(uint requestId, out int pError)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            cancelClipboardRequest(requestId, out pError);
#else
            pError = (int)WindowsClipboardErrorCode.PlatformUnavailable;
#endif
            return pError;
        }

        // Named consistently so the public API reads the same in both compilations. Outside the
        // Windows player they are never reached: the callers return PlatformUnavailable first.

        private static int CopyPlainTextNative(string text, uint options, out int pError)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            copyPlainText(text, options, out pError);
#else
            pError = (int)WindowsClipboardErrorCode.PlatformUnavailable;
#endif
            return pError;
        }

        private static int CopyHtmlNative(string html, string? plainText, uint options, out int pError)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            copyHtml(html, plainText, options, out pError);
#else
            pError = (int)WindowsClipboardErrorCode.PlatformUnavailable;
#endif
            return pError;
        }

        private static int CopyFilesNative(string pathsJson, uint options, out int pError)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            copyFiles(pathsJson, options, out pError);
#else
            pError = (int)WindowsClipboardErrorCode.PlatformUnavailable;
#endif
            return pError;
        }

        private static int CopyImageNative(byte[] dib, uint size, uint options, out int pError)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            copyImage(dib, size, options, out pError);
#else
            pError = (int)WindowsClipboardErrorCode.PlatformUnavailable;
#endif
            return pError;
        }

        private static int CopyCustomFormatNative(
            string formatName, byte[] data, uint size, uint options, out int pError)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            copyCustomFormat(formatName, data, size, options, out pError);
#else
            pError = (int)WindowsClipboardErrorCode.PlatformUnavailable;
#endif
            return pError;
        }

        private static int CopyMultipleFormatsNative(string itemsJson, uint options, out int pError)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            copyMultipleFormats(itemsJson, options, out pError);
#else
            pError = (int)WindowsClipboardErrorCode.PlatformUnavailable;
#endif
            return pError;
        }

        private static int ClearNative(out int pError)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            clearClipboard(out pError);
#else
            pError = (int)WindowsClipboardErrorCode.PlatformUnavailable;
#endif
            return pError;
        }

        private static uint PastePlainTextNative(IntPtr buffer, uint size, out int pError)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            return pastePlainText(buffer, size, out pError);
#else
            pError = (int)WindowsClipboardErrorCode.PlatformUnavailable;
            return 0;
#endif
        }

        private static uint PasteHtmlNative(IntPtr buffer, uint size, out int pError)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            return pasteHtml(buffer, size, out pError);
#else
            pError = (int)WindowsClipboardErrorCode.PlatformUnavailable;
            return 0;
#endif
        }

        private static uint PasteFilesNative(IntPtr buffer, uint size, out int pError)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            return pasteFiles(buffer, size, out pError);
#else
            pError = (int)WindowsClipboardErrorCode.PlatformUnavailable;
            return 0;
#endif
        }

        private static uint PasteImageNative(IntPtr buffer, uint size, out int pError)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            return pasteImage(buffer, size, out pError);
#else
            pError = (int)WindowsClipboardErrorCode.PlatformUnavailable;
            return 0;
#endif
        }

        private static uint PasteCustomFormatNative(
            string formatName, IntPtr buffer, uint size, out int pError)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            return pasteCustomFormat(formatName, buffer, size, out pError);
#else
            pError = (int)WindowsClipboardErrorCode.PlatformUnavailable;
            return 0;
#endif
        }

        private static uint GetFormatsNative(IntPtr buffer, uint size, out int pError)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            return getClipboardFormats(buffer, size, out pError);
#else
            pError = (int)WindowsClipboardErrorCode.PlatformUnavailable;
            return 0;
#endif
        }

        private static uint GetPreferredFormatNative(IntPtr buffer, uint size, out int pError)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            return getPreferredClipboardFormat(buffer, size, out pError);
#else
            pError = (int)WindowsClipboardErrorCode.PlatformUnavailable;
            return 0;
#endif
        }

        // Declared outside the native guard so the shared request plumbing can name the type in
        // both compilations; only its use crosses the ABI.
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void ClipboardRequestCallback(
            uint requestId, int error, [MarshalAs(UnmanagedType.LPWStr)] string? json);

        // Held for the lifetime of the manager: the native side keeps the pointer until every
        // accepted request has completed.
        private static readonly ClipboardRequestCallback s_requestDelegate = OnRequestCompletedNative;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate uint ClipboardRenderCallback(
            [MarshalAs(UnmanagedType.LPWStr)] string formatName, IntPtr context, IntPtr buffer,
            uint bufferSize, out uint requiredSize);

        // The native side keeps this pointer for as long as a reservation stands.
        private static readonly ClipboardRenderCallback s_renderDelegate = OnRenderFormatNative;

        [MonoPInvokeCallback(typeof(ClipboardRenderCallback))]
        private static uint OnRenderFormatNative(
            string formatName, IntPtr context, IntPtr buffer, uint bufferSize, out uint requiredSize)
        {
            // Runs synchronously inside WM_RENDERFORMAT on the owner UI thread. The dispatcher
            // cannot be used here: its queue only drains from Update, which cannot run until this
            // returns.
            try
            {
                return RenderDeferredFormat(formatName, buffer, bufferSize, out requiredSize);
            }
            catch (Exception ex)
            {
                // The layer below contains provider failures already, but this runs while Unity is
                // tearing down and the reporting it does there can throw in turn. Nothing may
                // cross the C ABI boundary, and requiredSize has to carry a value either way: an
                // unwritten out parameter leaves the native side reading whatever the stack held
                // and comparing it against the size promised in the first phase.
                requiredSize = 0;
                try
                {
                    Debug.LogError($"[{LogTag}][{nameof(OnRenderFormatNative)}] {ex.GetType().Name}");
                }
                catch
                {
                    // Reporting is best effort here; failing to log must not escape either.
                }
                return (uint)WindowsClipboardErrorCode.Unknown;
            }
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void ClipboardHistoryChangedCallback();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void ClipboardFlagChangedCallback([MarshalAs(UnmanagedType.Bool)] bool enabled);

        // The native side keeps these pointers until they are replaced or until shutdown reports
        // completion, so they have to outlive every call that hands them over.
        private static readonly ClipboardHistoryChangedCallback s_historyChangedDelegate =
            OnHistoryChangedNative;
        private static readonly ClipboardFlagChangedCallback s_historyEnabledDelegate =
            OnHistoryEnabledChangedNative;
        private static readonly ClipboardFlagChangedCallback s_roamingEnabledDelegate =
            OnRoamingEnabledChangedNative;

        [MonoPInvokeCallback(typeof(ClipboardHistoryChangedCallback))]
        private static void OnHistoryChangedNative()
        {
            try
            {
                RaiseHistoryChanged();
            }
            catch (Exception ex)
            {
                // Nothing may cross the C ABI boundary.
                Debug.LogError($"[{LogTag}][{nameof(OnHistoryChangedNative)}] {ex.Message}");
            }
        }

        [MonoPInvokeCallback(typeof(ClipboardFlagChangedCallback))]
        private static void OnHistoryEnabledChangedNative(bool enabled)
        {
            try
            {
                RaiseHistoryEnabledChanged(enabled);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{LogTag}][{nameof(OnHistoryEnabledChangedNative)}] {ex.Message}");
            }
        }

        [MonoPInvokeCallback(typeof(ClipboardFlagChangedCallback))]
        private static void OnRoamingEnabledChangedNative(bool enabled)
        {
            try
            {
                RaiseRoamingEnabledChanged(enabled);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{LogTag}][{nameof(OnRoamingEnabledChangedNative)}] {ex.Message}");
            }
        }

        /// <summary>Hands a history addition to the subscribers through the dispatcher.</summary>
        private static void RaiseHistoryChanged() => RaiseOnMainThread(
            nameof(RaiseHistoryChanged), () => _instance?.HistoryChanged?.Invoke());

        /// <summary>Hands a history-setting change to the subscribers through the dispatcher.</summary>
        private static void RaiseHistoryEnabledChanged(bool enabled) => RaiseOnMainThread(
            nameof(RaiseHistoryEnabledChanged), () => _instance?.HistoryEnabledChanged?.Invoke(enabled));

        /// <summary>Hands a roaming-setting change to the subscribers through the dispatcher.</summary>
        private static void RaiseRoamingEnabledChanged(bool enabled) => RaiseOnMainThread(
            nameof(RaiseRoamingEnabledChanged), () => _instance?.RoamingEnabledChanged?.Invoke(enabled));

        private static void RaiseOnMainThread(string origin, Action raise)
        {
            UnityMainThreadDispatcher? dispatcher = s_dispatcher;
            if (dispatcher == null)
            {
                // Said out loud, as a dropped result is: an event that silently never arrives is
                // indistinguishable from one the native side never sent.
                Debug.LogWarning($"[{LogTag}][{origin}] no dispatcher; event dropped.");
                return;
            }
            dispatcher.Enqueue(() =>
            {
                try
                {
                    raise();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[{LogTag}][{origin}] subscriber threw: {ex.Message}");
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

            // Anything still tracked would otherwise keep an awaiting caller waiting forever.
            DrainRequestRegistry();
            foreach (PendingRequest pending in s_pending.Values) pending.Registration.Dispose();
            s_pending.Clear();
            s_inFlight.Clear();
            s_registry.Reset();

            s_state = WindowsClipboardManagerState.Uninitialized;
            s_comOwnership = WindowsClipboardComOwnership.None;
            s_renderProviders = new Dictionary<string, Func<byte[]>>();
            s_renderCache.Clear();
            s_renderStaging = null;
            s_drain = null;
            s_quitState = WindowsClipboardQuitState.None;
            s_deliveringBeforeQuit = false;
            s_isTerminated = false;

#if UNITY_EDITOR
            QuitActionForTests = null;
            ComReleaseCountForTests = 0;
            NextNativeRequestIdForTests = null;
            PlatformAvailableForTests = null;
            NativeShutdownForTests = null;
            RecoverDeferredCallCountForTests = 0;
            RequestExceptionForTests = null;
            CancellationRegistrationsReleasedForTests = 0;
            SuppressRegistrationDisposalForTests = false;
#endif
            s_retryFrameBudget = ShutdownRetryFrameBudget;
            s_retrySecondBudget = ShutdownRetrySecondBudget;

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

        // Clipboard operations. The wrappers below exist so the public API can pass a delegate
        // without the extern signatures leaking out of this guarded region.

        [DllImport(DLL_NAME, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true)]
        private static extern void copyPlainText(
            [MarshalAs(UnmanagedType.LPWStr)] string text, uint options, out int pError);

        [DllImport(DLL_NAME, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true)]
        private static extern void copyHtml(
            [MarshalAs(UnmanagedType.LPWStr)] string htmlFragment,
            [MarshalAs(UnmanagedType.LPWStr)] string? plainText, uint options, out int pError);

        [DllImport(DLL_NAME, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true)]
        private static extern void copyFiles(
            [MarshalAs(UnmanagedType.LPWStr)] string pathsJson, uint options, out int pError);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private static extern void copyImage(byte[] dib, uint dibSize, uint options, out int pError);

        [DllImport(DLL_NAME, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true)]
        private static extern void copyCustomFormat(
            [MarshalAs(UnmanagedType.LPWStr)] string formatName, byte[] data, uint size, uint options,
            out int pError);

        [DllImport(DLL_NAME, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true)]
        private static extern void copyMultipleFormats(
            [MarshalAs(UnmanagedType.LPWStr)] string itemsJson, uint options, out int pError);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private static extern void clearClipboard(out int pError);

        [DllImport(DLL_NAME, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true)]
        private static extern uint pastePlainText(IntPtr buffer, uint bufferSize, out int pError);

        [DllImport(DLL_NAME, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true)]
        private static extern uint pasteHtml(IntPtr buffer, uint bufferSize, out int pError);

        [DllImport(DLL_NAME, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true)]
        private static extern uint pasteFiles(IntPtr buffer, uint bufferSize, out int pError);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private static extern uint pasteImage(IntPtr buffer, uint bufferSize, out int pError);

        [DllImport(DLL_NAME, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true)]
        private static extern uint pasteCustomFormat(
            [MarshalAs(UnmanagedType.LPWStr)] string formatName, IntPtr buffer, uint bufferSize,
            out int pError);

        [DllImport(DLL_NAME, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool hasClipboardFormat(
            [MarshalAs(UnmanagedType.LPWStr)] string formatName, out int pError);

        [DllImport(DLL_NAME, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true)]
        private static extern uint getClipboardFormats(IntPtr buffer, uint bufferSize, out int pError);

        [DllImport(DLL_NAME, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true)]
        private static extern uint getPreferredClipboardFormat(
            IntPtr buffer, uint bufferSize, out int pError);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private static extern uint getClipboardHistory(ClipboardRequestCallback cb, out int pError);

        [DllImport(DLL_NAME, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true)]
        private static extern uint restoreHistoryItem(
            [MarshalAs(UnmanagedType.LPWStr)] string itemId, ClipboardRequestCallback cb, out int pError);

        [DllImport(DLL_NAME, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true)]
        private static extern uint deleteHistoryItem(
            [MarshalAs(UnmanagedType.LPWStr)] string itemId, ClipboardRequestCallback cb, out int pError);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private static extern uint clearUnpinnedHistory(ClipboardRequestCallback cb, out int pError);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private static extern uint getClipboardHistoryAvailability(
            ClipboardRequestCallback cb, out int pError);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool cancelClipboardRequest(uint requestId, out int pError);

        [DllImport(DLL_NAME, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true)]
        private static extern void reserveDeferredFormats(
            [MarshalAs(UnmanagedType.LPWStr)] string formatNamesJson, ClipboardRenderCallback provider,
            IntPtr context, out int pError);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private static extern void recoverDeferredState(out int pError);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private static extern void setClipboardHistoryCallbacks(
            ClipboardHistoryChangedCallback? onHistoryChanged,
            ClipboardFlagChangedCallback? onHistoryEnabledChanged,
            ClipboardFlagChangedCallback? onRoamingEnabledChanged,
            out int pError);
#endif
    }
}
#endif
