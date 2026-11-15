#nullable enable

#if UNITY_IOS || UNITY_EDITOR
namespace JonghyunKim.NativeToolkit.Runtime.iOS.Clipboard
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using System.Threading;
#if UNITY_IOS && !UNITY_EDITOR
    using AOT;
#endif
    using JonghyunKim.NativeToolkit.Runtime.Common;
    using JonghyunKim.NativeToolkit.Runtime.iOS.Common;
    using UnityEngine;
    using Op = IosClipboardCApi.Op;

    /// <summary>
    /// Singleton manager for iOS native clipboard operations.
    /// <para>
    /// Calls native-toolkit's C ABI 2.0.0 (<c>Clipboard.h</c>; artifact/ios/clipboard, C ABI design v5).
    /// Results keep 1.x's codes and messages, but <see cref="IosClipboardErrorInfo.Domain"/> and
    /// <see cref="IosClipboardErrorInfo.NativeCode"/> are always null: the C ABI carries an error number only.
    /// A request the library rejects at once fails like any other failure, on a later Update, and the
    /// operation stays busy until then, as in 1.x. Values written to the clipboard end at a U+0000 (with a
    /// warning); a pasteboard name, a type identifier or an image path holding U+0000 fails with
    /// <c>CLIPBOARD_INVALID_REQUEST</c>; text read back has U+FFFD for U+0000. In the Editor every operation
    /// that passes the argument checks fails with <c>CLIPBOARD_BRIDGE_UNAVAILABLE</c> and
    /// "&lt;operation&gt; is only available on an iOS device.".
    /// </para>
    /// <para>
    /// <b>Threading.</b> Every member, including <see cref="Instance"/>, must be used from the
    /// Unity main thread. Calling an instance method from another thread is rejected with
    /// <c>CLIPBOARD_MAIN_THREAD_REQUIRED</c>; the <see cref="Instance"/> getter itself cannot be
    /// guarded, because it may create a GameObject before any check could run.
    /// </para>
    /// <para>
    /// <b>Concurrency.</b> Each operation is single-flight, as in 1.x: a second call while one is
    /// pending fails with <c>CLIPBOARD_BUSY</c> and leaves the pending call untouched. Different
    /// operations still run concurrently.
    /// </para>
    /// <para>
    /// <b>Lifetime.</b> Destroying this Manager is not supported. Once <c>OnDestroy</c> has run,
    /// every operation is rejected with <c>CLIPBOARD_MANAGER_DESTROYED</c> and late native
    /// callbacks are discarded, because a callback from the destroyed lifetime would otherwise be
    /// delivered to a freshly started call. See <see cref="IsTerminated"/>.
    /// </para>
    /// <para>
    /// The class compiles for <c>UNITY_IOS || UNITY_EDITOR</c> so it can be referenced and tested
    /// from the Editor regardless of build target.
    /// </para>
    /// <para>
    /// Intentional deviation from the "log every parameter" rule in csharp.md: clipboard content
    /// may hold passwords or tokens, so only shapes, counts and flags are logged, never values.
    /// This matches the native <c>ClipboardRedaction</c> policy and the Android manager.
    /// </para>
    /// </summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "JonghyunKim.NativeToolkit.Runtime.Clipboard")]
    public class IosClipboardManager : MonoBehaviour
    {
        private const string LogTag = "IosClipboardManager";

        // ── Error codes raised by this bridge layer (not present in the native ClipboardError) ──

        /// <summary>Error code reported when the same operation is already awaiting a native callback.</summary>
        public const string BusyErrorCode = "CLIPBOARD_BUSY";

        /// <summary>Error code reported when the native bridge cannot be reached.</summary>
        public const string BridgeUnavailableErrorCode = "CLIPBOARD_BRIDGE_UNAVAILABLE";

        /// <summary>Error code reported when an instance method is called off the Unity main thread.</summary>
        public const string MainThreadRequiredErrorCode = "CLIPBOARD_MAIN_THREAD_REQUIRED";

        /// <summary>Error code reported after the Manager has been destroyed.</summary>
        public const string ManagerDestroyedErrorCode = "CLIPBOARD_MANAGER_DESTROYED";

        private const string InvalidRequestErrorCode = "CLIPBOARD_INVALID_REQUEST";
        private const string EmptyPatternsErrorCode = "CLIPBOARD_EMPTY_PATTERNS";
        private const string EmptyPatternsMessage = "No detection patterns were specified.";

        // ── Operation names ─────────────────────────────────────────────────────

        /// <summary>Native operation name for writing content to the clipboard.</summary>
        public const string OperationCopy = "copy";

        /// <summary>Native operation name for appending content to the clipboard.</summary>
        public const string OperationAppend = "append";

        /// <summary>Native operation name for clearing the clipboard.</summary>
        public const string OperationClear = "clear";

        /// <summary>Native operation name for invalidating a named or unique pasteboard.</summary>
        public const string OperationRemovePasteboard = "removePasteboard";

        /// <summary>Native operation name for cancelling every pending item load.</summary>
        public const string OperationCancelLoads = "cancelLoads";

        /// <summary>Native operation name for starting clipboard change observation.</summary>
        public const string OperationStartObserving = "startObserving";

        /// <summary>Native operation name for stopping clipboard change observation.</summary>
        public const string OperationStopObserving = "stopObserving";

        internal const string OperationRead = "read";
        internal const string OperationReadData = "readData";
        internal const string OperationGetSnapshot = "getSnapshot";
        internal const string OperationCreatePasteboard = "createPasteboard";
        internal const string OperationDetectPatterns = "detectPatterns";
        internal const string OperationDetectValues = "detectValues";
        internal const string OperationLoadItem = "loadItem";
        internal const string OperationCheckForegroundChange = "checkForegroundChange";

        /// <summary>
        /// Single-flight key shared by <see cref="StartObserving"/> and <see cref="StopObserving"/>.
        /// Both mutate the same native subscription, so serializing them prevents a stop completion
        /// from landing after a newer start and clearing its registration.
        /// </summary>
        internal const string ObservationControlKey = "observation";

        // ── Singleton and static state ──────────────────────────────────────────

        private static IosClipboardManager? _instance;

        // Captured on the main thread in Awake so every dispatch can enqueue without touching
        // UnityMainThreadDispatcher.Instance, whose getter creates a GameObject and is main-thread
        // only. The dispatcher owns itself (DontDestroyOnLoad); this is a non-owning reference.
        private static UnityMainThreadDispatcher? s_dispatcher;
        private static int s_mainThreadId;
        private static bool s_isTerminated;

        /// <summary>
        /// Whether the Manager has been destroyed. Every operation is rejected from that point on,
        /// and a recreated instance does not clear it. Reset only at the start of a new Play
        /// session or app launch.
        /// </summary>
        public static bool IsTerminated => s_isTerminated;

        /// <summary>
        /// Singleton instance. Creates and persists a new GameObject if none exists.
        /// <para>
        /// Unity main thread only: the getter may create a GameObject, which cannot be done from a
        /// background thread and cannot be guarded before it happens.
        /// </para>
        /// </summary>
        public static IosClipboardManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    Debug.Log($"[{LogTag}] Creating new instance of IosClipboardManager");
                    var go = new GameObject("IosClipboardManager");
                    _instance = go.AddComponent<IosClipboardManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        // ── Events ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Raised when copy, append, clear, removePasteboard, cancelLoads, startObserving or
        /// stopObserving completes, on success and failure alike. Inspect
        /// <see cref="IosClipboardOperationResult.Operation"/> to tell them apart.
        /// Always invoked before the per-call callback.
        /// </summary>
        public event Action<IosClipboardOperationResult>? ClipboardOperationCompleted;

        /// <summary>
        /// Raised for each clipboard change while observation is active. Never raised before
        /// <see cref="StartObserving"/> succeeds. Events that cannot be parsed are dropped rather
        /// than delivered.
        /// </summary>
        public event Action<IosClipboardChangeEvent>? ClipboardChanged;

        /// <summary>Raised when <see cref="Read"/> completes.</summary>
        public event Action<IosClipboardReadResult>? ReadCompleted;

        /// <summary>Raised when <see cref="ReadData"/> completes.</summary>
        public event Action<IosClipboardReadDataResult>? ReadDataCompleted;

        /// <summary>Raised when <see cref="GetSnapshot"/> completes.</summary>
        public event Action<IosClipboardSnapshotResult>? SnapshotCompleted;

        /// <summary>Raised when <see cref="CreatePasteboard"/> completes.</summary>
        public event Action<IosPasteboardScopeResult>? PasteboardCreated;

        /// <summary>Raised when <see cref="DetectPatterns"/> completes.</summary>
        public event Action<IosClipboardDetectedPatternsResult>? PatternsDetected;

        /// <summary>Raised when <see cref="DetectValues"/> completes.</summary>
        public event Action<IosClipboardDetectedValuesResult>? ValuesDetected;

        /// <summary>Raised when <see cref="LoadItem"/> completes.</summary>
        public event Action<IosClipboardLoadedItemResult>? ItemLoaded;

        /// <summary>Raised when <see cref="CheckForegroundChange"/> completes.</summary>
        public event Action<IosClipboardForegroundChangeResult>? ForegroundChangeChecked;

        // ── Per-call callback slots ─────────────────────────────────────────────
        // At most one call per operation can be pending (single-flight), so a single slot per
        // operation is enough and a result can never reach another call's callback.

        private static Action<IosClipboardOperationResult>? s_onCopy;
        private static Action<IosClipboardOperationResult>? s_onAppend;
        private static Action<IosClipboardOperationResult>? s_onClear;
        private static Action<IosClipboardOperationResult>? s_onRemovePasteboard;
        private static Action<IosClipboardOperationResult>? s_onCancelLoads;
        private static Action<IosClipboardOperationResult>? s_onStartObserving;
        private static Action<IosClipboardOperationResult>? s_onStopObserving;
        private static Action<IosClipboardReadResult>? s_onRead;
        private static Action<IosClipboardReadDataResult>? s_onReadData;
        private static Action<IosClipboardSnapshotResult>? s_onSnapshot;
        private static Action<IosPasteboardScopeResult>? s_onCreatePasteboard;
        private static Action<IosClipboardDetectedPatternsResult>? s_onDetectPatterns;
        private static Action<IosClipboardDetectedValuesResult>? s_onDetectValues;
        private static Action<IosClipboardLoadedItemResult>? s_onLoadItem;
        private static Action<IosClipboardForegroundChangeResult>? s_onCheckForegroundChange;
        private static Action<IosClipboardChangeEvent>? s_onChanged;

        // Generation of the registration currently held in s_onChanged, its monotonic source, and
        // the generation the single pending observation-control call is responsible for.
        private static ulong s_observingGeneration;
        private static ulong s_onChangedGeneration;
        private static ulong s_pendingObservationGeneration;

        // Operations awaiting a native callback. Touched only from the Unity main thread (public
        // API is guarded, native callbacks arrive on the main thread), so no lock is needed.
        private static readonly HashSet<string> s_inFlight = new();

        // Whether this Manager's own observation is on (design D-6): set by a successful start, cleared by a
        // stop result, a failed start and destroy. Changes are dropped while it is off, also when another
        // component in the process started observing.
        private static bool s_isObserving;

        // The change listener, registered once before the first start and removed on destroy (D-6, D-7).
        private static IntPtr s_listener;

        /// <summary>
        /// How many times the C ABI released the change listener (D-7). Read by the UI harness (CB-5) by
        /// reflection after destroy; a field, so a player build keeps it.
        /// </summary>
        internal static int s_listenerReleaseCount;

        // The request id of the pending LoadItem (0: none), for CancelLoads (D-5).
        private static ulong s_loadRequestId;

        // What the C ABI does not return and the result needs (table 2.1).
        private static string s_readDataUtType = "";
        private static IosPasteboardCreationRequestKind s_createKind;

        // Bumped by every reset and destroy, so a result decided during a call (D-12) cannot reach a call of a
        // later lifetime.
        private static int s_lifetime;

        // Bumped by every reset only: a native result already known still goes out after destroy (as in 1.x),
        // but never into a later Play session or test (implementation review v1).
        private static int s_resetCount;

        // The registrations of starts rejected at entry: a change that arrived meanwhile reaches only the common
        // event (design v5, D-12). Any other registration gets the changes that arrived while it was current,
        // as in 1.x.
        private static readonly HashSet<ulong> s_revokedRegistrations = new();

        // Results decided during a call. IosClipboardLaterPump, on the dispatcher's GameObject, hands them to the
        // dispatcher's queue once the frame they were queued in has passed (design v5, D-12). Once one is
        // waiting, other results join the same list behind it, so a native completion cannot overtake it
        // (CP-31). Main thread only.
        private readonly struct Later
        {
            internal Later(int frame, string? operation, int lifetime, Action deliver)
            {
                Frame = frame;
                Operation = operation;
                Lifetime = lifetime;
                Deliver = deliver;
            }

            internal int Frame { get; }           // the frame it was queued in; int.MinValue: due at once
            internal string? Operation { get; }   // non-null: decided during a call, dropped after destroy
            internal int Lifetime { get; }        // s_lifetime for a decided result, s_resetCount for a native one
            internal Action Deliver { get; }
        }

        private static readonly List<Later> s_later = new();

        // Set for one Dispatch while a result from the list is delivered: that Dispatch invokes at once instead of
        // queuing again (a native result handed over behind it would otherwise overtake it). Used once, so a
        // call made from the user's callback goes through the list again.
        private static bool s_deliverInline;

        private static IosClipboardLaterPump? s_pump;

        // ── Lifecycle ───────────────────────────────────────────────────────────

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
            if (_instance != this) return;

            // Set the tombstone before any native call. A P/Invoke below can throw, and a
            // partially applied teardown would leave the Manager recreatable with late callbacks
            // undiscarded, defeating the guarantee this tombstone exists to provide.
            s_isTerminated = true;

            RunDestroyCleanup(
                stop: StopObservingForTeardown,
                removeListener: RemoveListenerForTeardown,
                cancel: CancelLoadsForTeardown,
                managedCleanup: () =>
                {
                    try
                    {
                        ClearAllPendingCallbacks();
                        HandOverLaterResults();
                    }
                    finally { _instance = null; }
                });

            // s_dispatcher is deliberately left set: post-destruction rejections still need it.
        }

        /// <summary>
        /// Runs the teardown steps with the exception boundaries the destroy contract needs:
        /// stop, removeListener and cancel are isolated from each other, and managedCleanup always runs.
        /// <para>
        /// Pure with respect to Manager state, so tests can pass throwing actions directly instead
        /// of the Manager exposing a mutable, swappable hook that would also ship in player builds.
        /// </para>
        /// </summary>
        /// <param name="stop">Stops native change observation.</param>
        /// <param name="removeListener">Removes the native change listener.</param>
        /// <param name="cancel">Cancels pending native item loads.</param>
        /// <param name="managedCleanup">Clears managed state. Always invoked.</param>
        internal static void RunDestroyCleanup(Action stop, Action removeListener, Action cancel, Action managedCleanup)
        {
            try
            {
                try
                {
                    stop();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[{LogTag}][{nameof(RunDestroyCleanup)}] stop: {ex.Message}");
                }

                try
                {
                    removeListener();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[{LogTag}][{nameof(RunDestroyCleanup)}] removeListener: {ex.Message}");
                }

                try
                {
                    cancel();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[{LogTag}][{nameof(RunDestroyCleanup)}] cancel: {ex.Message}");
                }
            }
            finally
            {
                try
                {
                    managedCleanup();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[{LogTag}][{nameof(RunDestroyCleanup)}] managed: {ex.Message}");
                }
            }
        }

        // Destroy (design D-7). Each step runs only on an iOS player; in the Editor nothing native exists.
        private static void StopObservingForTeardown()
        {
#if UNITY_IOS && !UNITY_EDITOR
            // user_data 0: the completion is dropped (D-2).
            IosClipboardCApi.ntk_clipboard_stop_observing_async(s_donePointer, IntPtr.Zero, IntPtr.Zero);
#endif
        }

        private static void RemoveListenerForTeardown()
        {
            IntPtr listener = s_listener;
            s_listener = IntPtr.Zero; // removing twice is undefined
#if UNITY_IOS && !UNITY_EDITOR
            if (listener != IntPtr.Zero) IosClipboardCApi.ntk_clipboard_listener_remove(listener);
#endif
        }

        private static void CancelLoadsForTeardown()
        {
            ulong id = s_loadRequestId;
            s_loadRequestId = 0;
#if UNITY_IOS && !UNITY_EDITOR
            if (id != 0) IosClipboardCApi.ntk_clipboard_cancel_load(id);
#endif
        }

        // Static state outlives an Editor Play session when "Reload Domain" is disabled, so a test
        // that destroys the Manager would leave every later test rejected. Reset at the start of
        // each Play session and app launch, where no native call can be outstanding yet.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState() => ResetCore();

#if UNITY_EDITOR
        /// <summary>
        /// Test-only reset of the lifetime tombstone and all static call state.
        /// <para>
        /// Safe in the Editor because no native call is ever issued there (every operation is
        /// rejected before reaching P/Invoke), so no callback can be outstanding. Compiled out of
        /// player builds precisely because that reasoning does not hold on device.
        /// </para>
        /// </summary>
        internal static void ResetForTests() => ResetCore();
#endif

        // Single reset core shared by the Play-session hook and the Editor-only test seam so the
        // two can never drift apart. Resets the call state; left alone are _instance (owned by Unity), the
        // listener and its release count (only destroy removes the listener), the pump (it lives on the
        // dispatcher's GameObject) and the request kind kept for CreatePasteboard (set by every call).
        private static void ResetCore()
        {
            s_later.Clear();
            s_resetCount++;
            s_isTerminated = false;
            s_dispatcher = null;
            s_mainThreadId = 0;
#if UNITY_EDITOR
            BridgeAvailableOverrideForTests = false;
            NativeCallThrowsForTests = false;
#endif
            ClearAllPendingCallbacks();
        }

        private static void ClearAllPendingCallbacks()
        {
            s_onCopy = null;
            s_onAppend = null;
            s_onClear = null;
            s_onRemovePasteboard = null;
            s_onCancelLoads = null;
            s_onStartObserving = null;
            s_onStopObserving = null;
            s_onRead = null;
            s_onReadData = null;
            s_onSnapshot = null;
            s_onCreatePasteboard = null;
            s_onDetectPatterns = null;
            s_onDetectValues = null;
            s_onLoadItem = null;
            s_onCheckForegroundChange = null;
            s_onChanged = null;

            s_observingGeneration = 0;
            s_onChangedGeneration = 0;
            s_pendingObservationGeneration = 0;

            s_inFlight.Clear();

            // s_listener is left alone: only destroy removes it (D-7).
            s_isObserving = false;
            s_loadRequestId = 0;
            s_readDataUtType = "";
            s_revokedRegistrations.Clear();
            s_lifetime++;
        }

        // ── Single-flight ───────────────────────────────────────────────────────

        /// <summary>
        /// Marks an operation as in flight.
        /// </summary>
        /// <param name="inFlight">Set tracking pending operations.</param>
        /// <param name="operation">Single-flight key.</param>
        /// <returns><c>false</c> when a call for the same key is already pending.</returns>
        internal static bool TryBeginOperation(HashSet<string> inFlight, string operation) =>
            inFlight.Add(operation);

        /// <summary>
        /// Releases an operation's in-flight marker. Safe to call when it is not marked.
        /// </summary>
        /// <param name="inFlight">Set tracking pending operations.</param>
        /// <param name="operation">Single-flight key.</param>
        internal static void EndOperation(HashSet<string> inFlight, string operation) =>
            inFlight.Remove(operation);

        private static bool IsMainThread() => Thread.CurrentThread.ManagedThreadId == s_mainThreadId;

#if UNITY_EDITOR
        /// <summary>
        /// Test-only switch that makes the guard chain treat the bridge as reachable, so tests can
        /// drive the real pending slots, in-flight set and rejection paths instead of a stand-in
        /// state model.
        /// <para>
        /// It does not disable any safety guard: the native call compiles to nothing in the Editor,
        /// so an operation started this way stays pending until a test delivers its result through
        /// one of the Complete*ForTests seams. Compiled out of player builds.
        /// </para>
        /// </summary>
        internal static bool BridgeAvailableOverrideForTests { get; set; }
#endif

        private static bool IsBridgeAvailable()
        {
#if UNITY_EDITOR
            return BridgeAvailableOverrideForTests;
#else
            return IosNativeToolkitCApi.IsAvailable;
#endif
        }

        // ── Rejection dispatch ──────────────────────────────────────────────────

        /// <summary>
        /// Dispatches a result for a call that was rejected before it ever became the pending call.
        /// <para>
        /// This path must not touch the pending per-call slot or the in-flight set: the rejected
        /// call never owned either, and the in-flight marker belongs to a different call that is
        /// still running. Routing a rejection through the normal path would steal the in-flight
        /// owner's callback and release its marker.
        /// </para>
        /// </summary>
        private static void DispatchRejectedResult<TResult>(
            TResult result,
            Action<TResult>? common,
            Action<TResult>? rejectedCallback)
        {
            // Main-thread path: Unity's null operator is used deliberately, so a dispatcher whose
            // GameObject was destroyed is detected. Enqueuing onto a destroyed dispatcher would
            // succeed silently and the result would never be flushed by Update.
            UnityMainThreadDispatcher? dispatcher = s_dispatcher;
            if (dispatcher == null)
            {
                Debug.LogError($"[{LogTag}][{nameof(DispatchRejectedResult)}] No dispatcher; result dropped.");
                return;
            }
            dispatcher.Enqueue(() => InvokeInOrder(result, common, rejectedCallback));
        }

        /// <summary>
        /// Rejection path for a call that arrived off the Unity main thread.
        /// <para>
        /// Everything needing Unity state (the common event snapshot and the log line) happens
        /// inside the enqueued closure, which runs on the main thread. Off the main thread this
        /// touches only a plain reference read and the dispatcher's lock-protected Enqueue. The
        /// Unity <c>==</c> overload is deliberately avoided: it inspects the native object.
        /// </para>
        /// </summary>
        private static void DispatchOffThreadRejection<TResult>(
            TResult result,
            Func<Action<TResult>?> commonSelector,
            Action<TResult>? rejectedCallback,
            string operation)
        {
            UnityMainThreadDispatcher? dispatcher = s_dispatcher;
            if ((object?)dispatcher == null)
            {
                return; // nothing can be done safely from this thread
            }

            dispatcher!.Enqueue(() =>
            {
                Debug.LogError($"[{LogTag}] {operation} was called off the Unity main thread; rejected.");
                InvokeInOrder(result, commonSelector(), rejectedCallback);
            });
        }

        /// <summary>
        /// Invokes the common event first, then the per-call callback. Each is wrapped in its own
        /// try/catch so a throwing subscriber cannot suppress the other, and so no exception
        /// escapes into the native caller.
        /// Extracted as a pure, Unity-lifecycle-independent helper for EditMode tests.
        /// </summary>
        /// <typeparam name="TResult">Result type being dispatched.</typeparam>
        /// <param name="result">Result to dispatch.</param>
        /// <param name="common">Common event snapshot, invoked first.</param>
        /// <param name="perCall">Per-call callback snapshot, invoked second.</param>
        internal static void InvokeInOrder<TResult>(TResult result, Action<TResult>? common, Action<TResult>? perCall)
        {
            try
            {
                common?.Invoke(result);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{LogTag}][{nameof(InvokeInOrder)}] common: {ex.Message}");
            }

            try
            {
                perCall?.Invoke(result);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{LogTag}][{nameof(InvokeInOrder)}] perCall: {ex.Message}");
            }
        }

        // ── Guard chain ─────────────────────────────────────────────────────────

        private static string MainThreadMessage(string operation) =>
            $"{operation} must be called from the Unity main thread.";

        private static string DestroyedMessage(string operation) =>
            $"{operation} is unavailable: IosClipboardManager has been destroyed.";

        private static string UnavailableMessage(string operation) =>
            $"{operation} is only available on an iOS device.";

        private static string BusyMessage(string operation) =>
            $"{operation} is already in progress.";

        private static string CouldNotStartMessage(string operation) =>
            $"{operation} could not be started.";

        /// <summary>
        /// Runs the pre-native guard chain in the order the destroy / thread / busy contracts
        /// require. Returns true only when the caller owns the in-flight marker and may proceed.
        /// </summary>
        private static bool TryStartOperation<TResult>(
            string operation,
            string inFlightKey,
            Action<TResult>? onResult,
            Func<Action<TResult>?> commonSelector,
            Func<string, string, TResult> failure,
            Func<(string Code, string Message)?>? validate = null)
        {
            // 1. Main thread. Must precede everything else, including the entry log.
            if (!IsMainThread())
            {
                DispatchOffThreadRejection(
                    failure(MainThreadRequiredErrorCode, MainThreadMessage(operation)),
                    commonSelector,
                    onResult,
                    operation);
                return false;
            }

            // 2. Destroyed. Rejected regardless of argument validity.
            if (s_isTerminated)
            {
                Debug.LogWarning($"[{LogTag}] {operation} rejected: the Manager has been destroyed.");
                DispatchRejectedResult(
                    failure(ManagerDestroyedErrorCode, DestroyedMessage(operation)),
                    commonSelector(),
                    onResult);
                return false;
            }

            // 3. Arguments.
            if (validate != null)
            {
                (string Code, string Message)? invalid = validate();
                if (invalid.HasValue)
                {
                    Debug.LogError($"[{LogTag}] {operation} rejected: {invalid.Value.Message}");
                    DispatchRejectedResult(
                        failure(invalid.Value.Code, invalid.Value.Message),
                        commonSelector(),
                        onResult);
                    return false;
                }
            }

            // 4. Platform.
            if (!IsBridgeAvailable())
            {
                Debug.LogWarning($"[{LogTag}] {operation} rejected: not running on an iOS device.");
                DispatchRejectedResult(
                    failure(BridgeUnavailableErrorCode, UnavailableMessage(operation)),
                    commonSelector(),
                    onResult);
                return false;
            }

            // 5. Single-flight. From here on the caller owns the marker.
            if (!TryBeginOperation(s_inFlight, inFlightKey))
            {
                Debug.LogWarning($"[{LogTag}] {operation} rejected: already in progress.");
                DispatchRejectedResult(
                    failure(BusyErrorCode, BusyMessage(operation)),
                    commonSelector(),
                    onResult);
                return false;
            }

            return true;
        }

        // ── Result dispatch (normal path: this call owns slot and marker) ───────

        private static void FireOperationResult(IosClipboardOperationResult result, string inFlightKey)
        {
            Action<IosClipboardOperationResult>? perCall = TakeOperationCallback(result.Operation);
            EndOperation(s_inFlight, inFlightKey);
            Dispatch(result, _instance?.ClipboardOperationCompleted, perCall);
        }

        private static Action<IosClipboardOperationResult>? TakeOperationCallback(string operation)
        {
            switch (operation)
            {
                case OperationCopy: { var cb = s_onCopy; s_onCopy = null; return cb; }
                case OperationAppend: { var cb = s_onAppend; s_onAppend = null; return cb; }
                case OperationClear: { var cb = s_onClear; s_onClear = null; return cb; }
                case OperationRemovePasteboard: { var cb = s_onRemovePasteboard; s_onRemovePasteboard = null; return cb; }
                case OperationCancelLoads: { var cb = s_onCancelLoads; s_onCancelLoads = null; return cb; }
                case OperationStartObserving: { var cb = s_onStartObserving; s_onStartObserving = null; return cb; }
                case OperationStopObserving: { var cb = s_onStopObserving; s_onStopObserving = null; return cb; }
                default: return null;
            }
        }

        private static void Dispatch<TResult>(TResult result, Action<TResult>? common, Action<TResult>? perCall)
        {
            // Native callbacks arrive on the main thread, so Unity's null operator applies here too
            // and catches a destroyed dispatcher (see DispatchRejectedResult).
            if (s_deliverInline)
            {
                s_deliverInline = false;
                InvokeInOrder(result, common, perCall);
                return;
            }
            UnityMainThreadDispatcher? dispatcher = s_dispatcher;
            if (dispatcher == null)
            {
                Debug.LogError($"[{LogTag}][{nameof(Dispatch)}] No dispatcher; result dropped.");
                DropLaterWithoutDispatcher();
                return;
            }
            if (s_later.Count > 0)
            {
                // Behind a result decided during a call: keep the order (D-12).
                s_later.Add(new Later(int.MinValue, null, s_resetCount, () => InvokeInOrder(result, common, perCall)));
                return;
            }
            dispatcher.Enqueue(() => InvokeInOrder(result, common, perCall));
        }

        private static void FireReadResult(IosClipboardReadResult result)
        {
            var perCall = s_onRead;
            s_onRead = null;
            EndOperation(s_inFlight, OperationRead);
            Dispatch(result, _instance?.ReadCompleted, perCall);
        }

        private static void FireReadDataResult(IosClipboardReadDataResult result)
        {
            var perCall = s_onReadData;
            s_onReadData = null;
            EndOperation(s_inFlight, OperationReadData);
            Dispatch(result, _instance?.ReadDataCompleted, perCall);
        }

        private static void FireSnapshotResult(IosClipboardSnapshotResult result)
        {
            var perCall = s_onSnapshot;
            s_onSnapshot = null;
            EndOperation(s_inFlight, OperationGetSnapshot);
            Dispatch(result, _instance?.SnapshotCompleted, perCall);
        }

        private static void FireCreatePasteboardResult(IosPasteboardScopeResult result)
        {
            var perCall = s_onCreatePasteboard;
            s_onCreatePasteboard = null;
            EndOperation(s_inFlight, OperationCreatePasteboard);
            Dispatch(result, _instance?.PasteboardCreated, perCall);
        }

        private static void FireDetectPatternsResult(IosClipboardDetectedPatternsResult result)
        {
            var perCall = s_onDetectPatterns;
            s_onDetectPatterns = null;
            EndOperation(s_inFlight, OperationDetectPatterns);
            Dispatch(result, _instance?.PatternsDetected, perCall);
        }

        private static void FireDetectValuesResult(IosClipboardDetectedValuesResult result)
        {
            var perCall = s_onDetectValues;
            s_onDetectValues = null;
            EndOperation(s_inFlight, OperationDetectValues);
            Dispatch(result, _instance?.ValuesDetected, perCall);
        }

        private static void FireLoadItemResult(IosClipboardLoadedItemResult result)
        {
            var perCall = s_onLoadItem;
            s_onLoadItem = null;
            EndOperation(s_inFlight, OperationLoadItem);
            Dispatch(result, _instance?.ItemLoaded, perCall);
        }

        private static void FireForegroundChangeResult(IosClipboardForegroundChangeResult result)
        {
            var perCall = s_onCheckForegroundChange;
            s_onCheckForegroundChange = null;
            EndOperation(s_inFlight, OperationCheckForegroundChange);
            Dispatch(result, _instance?.ForegroundChangeChecked, perCall);
        }

        private static void FireClipboardChanged(IosClipboardChangeEvent changeEvent)
        {
            // The registration current when the change arrived gets it, as in 1.x, unless it belongs to a start
            // rejected at entry in the meantime: that one gets no change after its failure (design v5, D-12).
            Action<IosClipboardChangeEvent>? registered = s_onChanged;
            ulong generation = s_onChangedGeneration;
            Dispatch(changeEvent, _instance?.ClipboardChanged, registered == null
                ? null
                : e =>
                {
                    if (!s_revokedRegistrations.Contains(generation)) registered(e);
                });
        }

        /// <summary>
        /// Clears the change-callback registration only when the pending observation-control call
        /// is the one that created it. A newer StartObserving must never be undone by an older
        /// completion. Reference equality cannot be used here: the same delegate instance may be
        /// registered twice, making it impossible to tell the registrations apart.
        /// </summary>
        private static void ReleaseChangeRegistrationIfOwned()
        {
            if (s_onChangedGeneration != 0 && s_onChangedGeneration <= s_pendingObservationGeneration)
            {
                s_onChanged = null;
                s_onChangedGeneration = 0;
            }
        }

        // ── Public API: operations without a payload ────────────────────────────

        /// <summary>
        /// Writes content to the clipboard, replacing existing items.
        /// Unity main thread only.
        /// </summary>
        /// <param name="content">Content to write.</param>
        /// <param name="scope">Target pasteboard, or <c>null</c> for the general pasteboard.</param>
        /// <param name="options">Privacy options, or <c>null</c> to use the native privacy-preserving default.</param>
        /// <param name="onResult">Optional per-call callback. <see cref="ClipboardOperationCompleted"/> always fires first.</param>
        public void Copy(
            IosClipboardContent content,
            IosPasteboardScope? scope = null,
            IosClipboardCopyOptions? options = null,
            Action<IosClipboardOperationResult>? onResult = null)
        {
            if (!TryStartOperation(
                    OperationCopy,
                    OperationCopy,
                    onResult,
                    () => _instance?.ClipboardOperationCompleted,
                    (code, message) => IosClipboardOperationResult.Failure(OperationCopy, code, message),
                    () => content == null
                        ? (InvalidRequestErrorCode, "content must not be null.")
                        : ((string, string)?)null))
            {
                return;
            }

            Debug.Log($"[{LogTag}][{nameof(Copy)}] kind: {content.Kind}, hasScope: {scope != null}, " +
                      $"hasOptions: {options != null}, hasCallback: {onResult != null}");

            s_onCopy = onResult;
            if (IosClipboardCApi.ScopeHasNul(scope) || IosClipboardCApi.ContentTypeHasNul(content))
            {
                RejectAtEntry(OperationCopy, InvalidRequest);
                return;
            }
            WarnIfValueHasNul(content);
            InvokeNative(
                OperationCopy,
                OperationCopy,
                () =>
                {
#if UNITY_IOS && !UNITY_EDITOR
                    CallNative(OperationCopy, allocated => IosClipboardCApi.ntk_clipboard_copy_async(
                        IosClipboardCApi.BuildContent(content, allocated),
                        IosClipboardCApi.BuildOptions(options, allocated),
                        IosClipboardCApi.BuildScope(scope, allocated),
                        s_donePointer, OpData(Op.Copy), IntPtr.Zero));
#endif
                });
        }

        /// <summary>
        /// Appends content to the clipboard.
        /// <para>
        /// Cannot carry privacy options, and does not inherit options set by a prior
        /// <see cref="Copy"/>. Always use <see cref="Copy"/> for sensitive data.
        /// </para>
        /// Unity main thread only.
        /// </summary>
        /// <param name="content">Content to append.</param>
        /// <param name="scope">Target pasteboard, or <c>null</c> for the general pasteboard.</param>
        /// <param name="onResult">Optional per-call callback. <see cref="ClipboardOperationCompleted"/> always fires first.</param>
        public void Append(
            IosClipboardContent content,
            IosPasteboardScope? scope = null,
            Action<IosClipboardOperationResult>? onResult = null)
        {
            if (!TryStartOperation(
                    OperationAppend,
                    OperationAppend,
                    onResult,
                    () => _instance?.ClipboardOperationCompleted,
                    (code, message) => IosClipboardOperationResult.Failure(OperationAppend, code, message),
                    () => content == null
                        ? (InvalidRequestErrorCode, "content must not be null.")
                        : ((string, string)?)null))
            {
                return;
            }

            Debug.Log($"[{LogTag}][{nameof(Append)}] kind: {content.Kind}, hasScope: {scope != null}, " +
                      $"hasCallback: {onResult != null}");

            s_onAppend = onResult;
            if (IosClipboardCApi.ScopeHasNul(scope) || IosClipboardCApi.ContentTypeHasNul(content))
            {
                RejectAtEntry(OperationAppend, InvalidRequest);
                return;
            }
            WarnIfValueHasNul(content);
            InvokeNative(
                OperationAppend,
                OperationAppend,
                () =>
                {
#if UNITY_IOS && !UNITY_EDITOR
                    CallNative(OperationAppend, allocated => IosClipboardCApi.ntk_clipboard_append_async(
                        IosClipboardCApi.BuildContent(content, allocated),
                        IosClipboardCApi.BuildScope(scope, allocated),
                        s_donePointer, OpData(Op.Append), IntPtr.Zero));
#endif
                });
        }

        /// <summary>
        /// Clears all items from the clipboard. Unity main thread only.
        /// </summary>
        /// <param name="scope">Target pasteboard, or <c>null</c> for the general pasteboard.</param>
        /// <param name="onResult">Optional per-call callback. <see cref="ClipboardOperationCompleted"/> always fires first.</param>
        public void Clear(IosPasteboardScope? scope = null, Action<IosClipboardOperationResult>? onResult = null)
        {
            if (!TryStartOperation(
                    OperationClear,
                    OperationClear,
                    onResult,
                    () => _instance?.ClipboardOperationCompleted,
                    (code, message) => IosClipboardOperationResult.Failure(OperationClear, code, message)))
            {
                return;
            }

            Debug.Log($"[{LogTag}][{nameof(Clear)}] hasScope: {scope != null}, hasCallback: {onResult != null}");

            s_onClear = onResult;
            if (IosClipboardCApi.ScopeHasNul(scope))
            {
                RejectAtEntry(OperationClear, InvalidRequest);
                return;
            }
            InvokeNative(
                OperationClear,
                OperationClear,
                () =>
                {
#if UNITY_IOS && !UNITY_EDITOR
                    CallNative(OperationClear, allocated => IosClipboardCApi.ntk_clipboard_clear_async(
                        IosClipboardCApi.BuildScope(scope, allocated),
                        s_donePointer, OpData(Op.Clear), IntPtr.Zero));
#endif
                });
        }

        /// <summary>
        /// Invalidates a named or unique pasteboard. The general pasteboard cannot be removed.
        /// Unity main thread only.
        /// </summary>
        /// <param name="scope">Pasteboard to invalidate. Required.</param>
        /// <param name="onResult">Optional per-call callback. <see cref="ClipboardOperationCompleted"/> always fires first.</param>
        public void RemovePasteboard(IosPasteboardScope scope, Action<IosClipboardOperationResult>? onResult = null)
        {
            if (!TryStartOperation(
                    OperationRemovePasteboard,
                    OperationRemovePasteboard,
                    onResult,
                    () => _instance?.ClipboardOperationCompleted,
                    (code, message) => IosClipboardOperationResult.Failure(OperationRemovePasteboard, code, message),
                    () => scope == null
                        ? (InvalidRequestErrorCode, "scope must not be null.")
                        : ((string, string)?)null))
            {
                return;
            }

            Debug.Log($"[{LogTag}][{nameof(RemovePasteboard)}] scopeKind: {scope.Kind}, hasCallback: {onResult != null}");

            s_onRemovePasteboard = onResult;
            if (IosClipboardCApi.ScopeHasNul(scope))
            {
                RejectAtEntry(OperationRemovePasteboard, InvalidRequest);
                return;
            }
            InvokeNative(
                OperationRemovePasteboard,
                OperationRemovePasteboard,
                () =>
                {
#if UNITY_IOS && !UNITY_EDITOR
                    // The general pasteboard too is a struct here: remove rejects NULL (D-8 (3)).
                    CallNative(OperationRemovePasteboard, allocated => IosClipboardCApi.ntk_clipboard_remove_pasteboard_async(
                        IosClipboardCApi.BuildRequiredScope(scope, allocated),
                        s_donePointer, OpData(Op.RemovePasteboard), IntPtr.Zero));
#endif
                });
        }

        /// <summary>
        /// Cancels every pending <see cref="LoadItem"/> request. Cancelled loads report
        /// <c>CLIPBOARD_CANCELLED</c>, which callers may treat as a normal outcome.
        /// Unity main thread only.
        /// </summary>
        /// <param name="onResult">Optional per-call callback. <see cref="ClipboardOperationCompleted"/> always fires first.</param>
        public void CancelLoads(Action<IosClipboardOperationResult>? onResult = null)
        {
            if (!TryStartOperation(
                    OperationCancelLoads,
                    OperationCancelLoads,
                    onResult,
                    () => _instance?.ClipboardOperationCompleted,
                    (code, message) => IosClipboardOperationResult.Failure(OperationCancelLoads, code, message)))
            {
                return;
            }

            Debug.Log($"[{LogTag}][{nameof(CancelLoads)}] hasCallback: {onResult != null}");

            s_onCancelLoads = onResult;
            InvokeNative(
                OperationCancelLoads,
                OperationCancelLoads,
                () =>
                {
#if UNITY_IOS && !UNITY_EDITOR
                    // D-5: the C ABI cancels one request; at most one load is pending (single-flight). The load
                    // completes later with CLIPBOARD_CANCELLED, after this result (D-12).
                    if (s_loadRequestId != 0) IosClipboardCApi.ntk_clipboard_cancel_load(s_loadRequestId);
                    CompleteLater(OperationCancelLoads, () => FireOperationResult(
                        IosClipboardOperationResult.Success(OperationCancelLoads), OperationCancelLoads));
#endif
                });
        }

        /// <summary>
        /// Starts observing clipboard changes.
        /// <para>
        /// Shares a single-flight key with <see cref="StopObserving"/>, so one cannot start while
        /// the other is pending. A second successful start replaces the previous observation.
        /// </para>
        /// Unity main thread only.
        /// </summary>
        /// <param name="scope">Pasteboard to observe, or <c>null</c> for the general pasteboard.</param>
        /// <param name="onChanged">Optional per-registration change callback. <see cref="ClipboardChanged"/> always fires first.</param>
        /// <param name="onStarted">Optional per-call callback for the start result. <see cref="ClipboardOperationCompleted"/> always fires first.</param>
        public void StartObserving(
            IosPasteboardScope? scope = null,
            Action<IosClipboardChangeEvent>? onChanged = null,
            Action<IosClipboardOperationResult>? onStarted = null)
        {
            if (!TryStartOperation(
                    OperationStartObserving,
                    ObservationControlKey,
                    onStarted,
                    () => _instance?.ClipboardOperationCompleted,
                    (code, message) => IosClipboardOperationResult.Failure(OperationStartObserving, code, message)))
            {
                return;
            }

            Debug.Log($"[{LogTag}][{nameof(StartObserving)}] hasScope: {scope != null}, " +
                      $"hasChangeCallback: {onChanged != null}, hasCallback: {onStarted != null}");

            s_onStartObserving = onStarted;
            s_onChanged = onChanged;
            s_onChangedGeneration = ++s_observingGeneration;
            s_pendingObservationGeneration = s_onChangedGeneration;

            if (IosClipboardCApi.ScopeHasNul(scope))
            {
                RejectAtEntry(OperationStartObserving, InvalidRequest);
                return;
            }

            InvokeNative(
                OperationStartObserving,
                ObservationControlKey,
                () =>
                {
#if UNITY_IOS && !UNITY_EDITOR
                    // D-6: the listener first, once; the C ABI runs the two in this order on the main queue.
                    if (s_listener == IntPtr.Zero)
                    {
                        int registered = IosClipboardCApi.ntk_clipboard_add_change_listener(
                            s_changePointer, IntPtr.Zero, s_listenerReleasePointer, out IntPtr listener);
                        if (registered != IosClipboardCApi.ErrorNone)
                        {
                            RejectAtEntry(OperationStartObserving, IosClipboardCApi.ToErrorInfo(registered));
                            return;
                        }
                        s_listener = listener;
                    }
                    CallNative(OperationStartObserving, allocated => IosClipboardCApi.ntk_clipboard_start_observing_async(
                        IosClipboardCApi.BuildScope(scope, allocated),
                        s_donePointer, OpData(Op.StartObserving), IntPtr.Zero));
#endif
                },
                onNativeFailure: () =>
                {
                    ReleaseChangeRegistrationIfOwned();
                    s_pendingObservationGeneration = 0;
                });
        }

        /// <summary>
        /// Stops observing clipboard changes. No further change events are delivered once the
        /// result arrives. Shares a single-flight key with <see cref="StartObserving"/>.
        /// Unity main thread only.
        /// </summary>
        /// <param name="onResult">Optional per-call callback. <see cref="ClipboardOperationCompleted"/> always fires first.</param>
        public void StopObserving(Action<IosClipboardOperationResult>? onResult = null)
        {
            if (!TryStartOperation(
                    OperationStopObserving,
                    ObservationControlKey,
                    onResult,
                    () => _instance?.ClipboardOperationCompleted,
                    (code, message) => IosClipboardOperationResult.Failure(OperationStopObserving, code, message)))
            {
                return;
            }

            Debug.Log($"[{LogTag}][{nameof(StopObserving)}] hasCallback: {onResult != null}");

            s_onStopObserving = onResult;
            // Stop creates no registration, so the generation is not advanced; it only becomes
            // responsible for whatever registration exists right now.
            s_pendingObservationGeneration = s_observingGeneration;

            InvokeNative(
                OperationStopObserving,
                ObservationControlKey,
                () =>
                {
#if UNITY_IOS && !UNITY_EDITOR
                    CallNative(OperationStopObserving, _ => IosClipboardCApi.ntk_clipboard_stop_observing_async(
                        s_donePointer, OpData(Op.StopObserving), IntPtr.Zero));
#endif
                },
                onNativeFailure: () =>
                {
                    ReleaseChangeRegistrationIfOwned();
                    s_pendingObservationGeneration = 0;
                });
        }

        // ── Public API: operations returning a payload ──────────────────────────

        /// <summary>
        /// Reads all clipboard items, without their large payloads.
        /// <para>
        /// Reading content written by another app may present the system paste permission UI.
        /// Use <see cref="GetSnapshot"/> when only metadata is needed.
        /// </para>
        /// Unity main thread only.
        /// </summary>
        /// <param name="scope">Target pasteboard, or <c>null</c> for the general pasteboard.</param>
        /// <param name="onResult">Optional per-call callback. <see cref="ReadCompleted"/> always fires first.</param>
        public void Read(IosPasteboardScope? scope = null, Action<IosClipboardReadResult>? onResult = null)
        {
            if (!TryStartOperation(
                    OperationRead,
                    OperationRead,
                    onResult,
                    () => _instance?.ReadCompleted,
                    IosClipboardReadResult.Failure))
            {
                return;
            }

            Debug.Log($"[{LogTag}][{nameof(Read)}] hasScope: {scope != null}, hasCallback: {onResult != null}");

            s_onRead = onResult;
            if (IosClipboardCApi.ScopeHasNul(scope))
            {
                RejectAtEntry(OperationRead, InvalidRequest);
                return;
            }
            InvokeNative(
                OperationRead,
                OperationRead,
                () =>
                {
#if UNITY_IOS && !UNITY_EDITOR
                    CallNative(OperationRead, allocated => IosClipboardCApi.ntk_clipboard_read_async(
                        IosClipboardCApi.BuildScope(scope, allocated),
                        s_readPointer, OpData(Op.Read), IntPtr.Zero));
#endif
                },
                onNativeFailureResult: message =>
                    FireReadResult(IosClipboardReadResult.Failure(BridgeUnavailableErrorCode, message)));
        }

        /// <summary>
        /// Reads the raw data for a uniform type identifier.
        /// A type with no data is a success with <c>HasData == false</c>, not a failure.
        /// Unity main thread only.
        /// </summary>
        /// <param name="utType">Uniform type identifier to read.</param>
        /// <param name="scope">Target pasteboard, or <c>null</c> for the general pasteboard.</param>
        /// <param name="onResult">Optional per-call callback. <see cref="ReadDataCompleted"/> always fires first.</param>
        public void ReadData(
            string utType,
            IosPasteboardScope? scope = null,
            Action<IosClipboardReadDataResult>? onResult = null)
        {
            if (!TryStartOperation(
                    OperationReadData,
                    OperationReadData,
                    onResult,
                    () => _instance?.ReadDataCompleted,
                    IosClipboardReadDataResult.Failure,
                    () => utType == null
                        ? (InvalidRequestErrorCode, "utType must not be null.")
                        : ((string, string)?)null))
            {
                return;
            }

            Debug.Log($"[{LogTag}][{nameof(ReadData)}] utType: {utType}, hasScope: {scope != null}, " +
                      $"hasCallback: {onResult != null}");

            s_onReadData = onResult;
            s_readDataUtType = utType;
            if (IosClipboardCApi.ScopeHasNul(scope) || IosClipboardCApi.HasNul(utType))
            {
                RejectAtEntry(OperationReadData, InvalidRequest);
                return;
            }
            InvokeNative(
                OperationReadData,
                OperationReadData,
                () =>
                {
#if UNITY_IOS && !UNITY_EDITOR
                    CallNative(OperationReadData, allocated => IosClipboardCApi.ntk_clipboard_read_data_async(
                        IosNativeToolkitCApi.AllocUtf8(utType, allocated),
                        IosClipboardCApi.BuildScope(scope, allocated),
                        s_dataPointer, OpData(Op.ReadData), IntPtr.Zero));
#endif
                },
                onNativeFailureResult: message =>
                    FireReadDataResult(IosClipboardReadDataResult.Failure(BridgeUnavailableErrorCode, message)));
        }

        /// <summary>
        /// Reads clipboard metadata using only system APIs documented to avoid user prompts.
        /// Unity main thread only.
        /// </summary>
        /// <param name="scope">Target pasteboard, or <c>null</c> for the general pasteboard.</param>
        /// <param name="matchingTypes">Types to match, or <c>null</c> to request none.</param>
        /// <param name="onResult">Optional per-call callback. <see cref="SnapshotCompleted"/> always fires first.</param>
        public void GetSnapshot(
            IosPasteboardScope? scope = null,
            string[]? matchingTypes = null,
            Action<IosClipboardSnapshotResult>? onResult = null)
        {
            if (!TryStartOperation(
                    OperationGetSnapshot,
                    OperationGetSnapshot,
                    onResult,
                    () => _instance?.SnapshotCompleted,
                    IosClipboardSnapshotResult.Failure))
            {
                return;
            }

            Debug.Log($"[{LogTag}][{nameof(GetSnapshot)}] hasScope: {scope != null}, " +
                      $"matchingTypeCount: {matchingTypes?.Length ?? 0}, hasCallback: {onResult != null}");

            s_onSnapshot = onResult;
            if (IosClipboardCApi.ScopeHasNul(scope) ||
                (matchingTypes != null && Array.Exists(matchingTypes, IosClipboardCApi.HasNul)))
            {
                RejectAtEntry(OperationGetSnapshot, InvalidRequest);
                return;
            }
            InvokeNative(
                OperationGetSnapshot,
                OperationGetSnapshot,
                () =>
                {
#if UNITY_IOS && !UNITY_EDITOR
                    // An empty array asks for no matching, as 1.x (matching was done only for a non-empty list). A null
                    // type becomes a NULL pointer, which the library rejects (CLIPBOARD_INVALID_REQUEST, as 1.x).
                    CallNative(OperationGetSnapshot, allocated => IosClipboardCApi.ntk_clipboard_get_snapshot_async(
                        matchingTypes == null || matchingTypes.Length == 0
                            ? IntPtr.Zero
                            : IosNativeToolkitCApi.AllocStringArray(matchingTypes, allocated),
                        new UIntPtr((uint)(matchingTypes?.Length ?? 0)),
                        IosClipboardCApi.BuildScope(scope, allocated),
                        s_snapshotPointer, OpData(Op.GetSnapshot), IntPtr.Zero));
#endif
                },
                onNativeFailureResult: message =>
                    FireSnapshotResult(IosClipboardSnapshotResult.Failure(BridgeUnavailableErrorCode, message)));
        }

        /// <summary>
        /// Creates a named pasteboard, resolves an existing one, or creates a uniquely named one.
        /// <para>
        /// Named and unique pasteboards are not persistent: they exist only while the creating app
        /// is running.
        /// </para>
        /// Unity main thread only.
        /// </summary>
        /// <param name="request">What to create.</param>
        /// <param name="onResult">Optional per-call callback. <see cref="PasteboardCreated"/> always fires first.</param>
        public void CreatePasteboard(
            IosPasteboardCreationRequest request,
            Action<IosPasteboardScopeResult>? onResult = null)
        {
            if (!TryStartOperation(
                    OperationCreatePasteboard,
                    OperationCreatePasteboard,
                    onResult,
                    () => _instance?.PasteboardCreated,
                    IosPasteboardScopeResult.Failure,
                    () => request == null
                        ? (InvalidRequestErrorCode, "request must not be null.")
                        : ((string, string)?)null))
            {
                return;
            }

            Debug.Log($"[{LogTag}][{nameof(CreatePasteboard)}] requestKind: {request.Kind}, " +
                      $"hasCallback: {onResult != null}");

            s_onCreatePasteboard = onResult;
            s_createKind = request.Kind;
            if (IosClipboardCApi.HasNul(request.Name))
            {
                RejectAtEntry(OperationCreatePasteboard, InvalidRequest);
                return;
            }
            InvokeNative(
                OperationCreatePasteboard,
                OperationCreatePasteboard,
                () =>
                {
#if UNITY_IOS && !UNITY_EDITOR
                    CallNative(OperationCreatePasteboard, allocated => IosClipboardCApi.ntk_clipboard_create_pasteboard_async(
                        request.Kind == IosPasteboardCreationRequestKind.Named ? IosClipboardCApi.ScopeNamed : IosClipboardCApi.ScopeUnique,
                        IosNativeToolkitCApi.AllocUtf8(request.Name, allocated),
                        s_namePointer, OpData(Op.CreatePasteboard), IntPtr.Zero));
#endif
                },
                onNativeFailureResult: message =>
                    FireCreatePasteboardResult(IosPasteboardScopeResult.Failure(BridgeUnavailableErrorCode, message)));
        }

        /// <summary>
        /// Detects which patterns are present, without reading their matched values.
        /// Unity main thread only.
        /// </summary>
        /// <param name="patterns">Patterns to look for. Must not be null or empty.</param>
        /// <param name="scope">Target pasteboard, or <c>null</c> for the general pasteboard.</param>
        /// <param name="onResult">Optional per-call callback. <see cref="PatternsDetected"/> always fires first.</param>
        public void DetectPatterns(
            IosClipboardDetectionPattern[] patterns,
            IosPasteboardScope? scope = null,
            Action<IosClipboardDetectedPatternsResult>? onResult = null)
        {
            if (!TryStartOperation(
                    OperationDetectPatterns,
                    OperationDetectPatterns,
                    onResult,
                    () => _instance?.PatternsDetected,
                    IosClipboardDetectedPatternsResult.Failure,
                    () => ValidatePatterns(patterns)))
            {
                return;
            }

            Debug.Log($"[{LogTag}][{nameof(DetectPatterns)}] patternCount: {patterns.Length}, " +
                      $"hasScope: {scope != null}, hasCallback: {onResult != null}");

            s_onDetectPatterns = onResult;
            if (!IosClipboardCApi.TryPatternBits(patterns, out uint patternBits) || IosClipboardCApi.ScopeHasNul(scope))
            {
                RejectAtEntry(OperationDetectPatterns, InvalidRequest);
                return;
            }
            InvokeNative(
                OperationDetectPatterns,
                OperationDetectPatterns,
                () =>
                {
#if UNITY_IOS && !UNITY_EDITOR
                    CallNative(OperationDetectPatterns, allocated => IosClipboardCApi.ntk_clipboard_detect_patterns_async(
                        patternBits,
                        IosClipboardCApi.BuildScope(scope, allocated),
                        s_patternsPointer, OpData(Op.DetectPatterns), IntPtr.Zero));
#endif
                },
                onNativeFailureResult: message => FireDetectPatternsResult(
                    IosClipboardDetectedPatternsResult.Failure(BridgeUnavailableErrorCode, message)));
        }

        /// <summary>
        /// Detects patterns and reads their matched values.
        /// Unity main thread only.
        /// </summary>
        /// <param name="patterns">Patterns to look for. Must not be null or empty.</param>
        /// <param name="scope">Target pasteboard, or <c>null</c> for the general pasteboard.</param>
        /// <param name="onResult">Optional per-call callback. <see cref="ValuesDetected"/> always fires first.</param>
        public void DetectValues(
            IosClipboardDetectionPattern[] patterns,
            IosPasteboardScope? scope = null,
            Action<IosClipboardDetectedValuesResult>? onResult = null)
        {
            if (!TryStartOperation(
                    OperationDetectValues,
                    OperationDetectValues,
                    onResult,
                    () => _instance?.ValuesDetected,
                    IosClipboardDetectedValuesResult.Failure,
                    () => ValidatePatterns(patterns)))
            {
                return;
            }

            Debug.Log($"[{LogTag}][{nameof(DetectValues)}] patternCount: {patterns.Length}, " +
                      $"hasScope: {scope != null}, hasCallback: {onResult != null}");

            s_onDetectValues = onResult;
            if (!IosClipboardCApi.TryPatternBits(patterns, out uint patternBits) || IosClipboardCApi.ScopeHasNul(scope))
            {
                RejectAtEntry(OperationDetectValues, InvalidRequest);
                return;
            }
            InvokeNative(
                OperationDetectValues,
                OperationDetectValues,
                () =>
                {
#if UNITY_IOS && !UNITY_EDITOR
                    CallNative(OperationDetectValues, allocated => IosClipboardCApi.ntk_clipboard_detect_values_async(
                        patternBits,
                        IosClipboardCApi.BuildScope(scope, allocated),
                        s_valuesPointer, OpData(Op.DetectValues), IntPtr.Zero));
#endif
                },
                onNativeFailureResult: message => FireDetectValuesResult(
                    IosClipboardDetectedValuesResult.Failure(BridgeUnavailableErrorCode, message)));
        }

        /// <summary>
        /// Loads a single item from the pasteboard's item providers asynchronously.
        /// <para>
        /// May take up to 15 seconds. A second load is rejected with <c>CLIPBOARD_BUSY</c> while
        /// one is pending; load different types sequentially.
        /// </para>
        /// Unity main thread only.
        /// </summary>
        /// <param name="request">What to load.</param>
        /// <param name="scope">Target pasteboard, or <c>null</c> for the general pasteboard.</param>
        /// <param name="onResult">Optional per-call callback. <see cref="ItemLoaded"/> always fires first.</param>
        public void LoadItem(
            IosClipboardLoadRequest request,
            IosPasteboardScope? scope = null,
            Action<IosClipboardLoadedItemResult>? onResult = null)
        {
            if (!TryStartOperation(
                    OperationLoadItem,
                    OperationLoadItem,
                    onResult,
                    () => _instance?.ItemLoaded,
                    IosClipboardLoadedItemResult.Failure,
                    () => request == null
                        ? (InvalidRequestErrorCode, "request must not be null.")
                        : ((string, string)?)null))
            {
                return;
            }

            Debug.Log($"[{LogTag}][{nameof(LoadItem)}] requestKind: {request.Kind}, hasScope: {scope != null}, " +
                      $"hasCallback: {onResult != null}");

            s_onLoadItem = onResult;
            if (IosClipboardCApi.ScopeHasNul(scope) || IosClipboardCApi.HasNul(request.UtType))
            {
                RejectAtEntry(OperationLoadItem, InvalidRequest);
                return;
            }
            InvokeNative(
                OperationLoadItem,
                OperationLoadItem,
                () =>
                {
#if UNITY_IOS && !UNITY_EDITOR
                    CallNative(OperationLoadItem, allocated =>
                    {
                        int error = IosClipboardCApi.ntk_clipboard_load_item_async(
                            IosClipboardCApi.ItemKindOf(request.Kind),
                            IosNativeToolkitCApi.AllocUtf8(request.UtType, allocated),
                            IosClipboardCApi.BuildScope(scope, allocated),
                            s_loadPointer, OpData(Op.LoadItem), IntPtr.Zero, out ulong requestId);
                        if (error == IosClipboardCApi.ErrorNone) s_loadRequestId = requestId;
                        return error;
                    });
#endif
                },
                onNativeFailureResult: message => FireLoadItemResult(
                    IosClipboardLoadedItemResult.Failure(BridgeUnavailableErrorCode, message)));
        }

        /// <summary>
        /// Checks whether the clipboard changed since the last check, by comparing the change
        /// count. Never fails natively. Unity main thread only.
        /// </summary>
        /// <param name="scope">Target pasteboard, or <c>null</c> for the general pasteboard.</param>
        /// <param name="onResult">Optional per-call callback. <see cref="ForegroundChangeChecked"/> always fires first.</param>
        public void CheckForegroundChange(
            IosPasteboardScope? scope = null,
            Action<IosClipboardForegroundChangeResult>? onResult = null)
        {
            if (!TryStartOperation(
                    OperationCheckForegroundChange,
                    OperationCheckForegroundChange,
                    onResult,
                    () => _instance?.ForegroundChangeChecked,
                    IosClipboardForegroundChangeResult.Failure))
            {
                return;
            }

            Debug.Log($"[{LogTag}][{nameof(CheckForegroundChange)}] hasScope: {scope != null}, " +
                      $"hasCallback: {onResult != null}");

            s_onCheckForegroundChange = onResult;
            if (IosClipboardCApi.ScopeHasNul(scope))
            {
                RejectAtEntry(OperationCheckForegroundChange, InvalidRequest);
                return;
            }
            InvokeNative(
                OperationCheckForegroundChange,
                OperationCheckForegroundChange,
                () =>
                {
#if UNITY_IOS && !UNITY_EDITOR
                    CallNative(OperationCheckForegroundChange, allocated => IosClipboardCApi.ntk_clipboard_check_foreground_change_async(
                        IosClipboardCApi.BuildScope(scope, allocated),
                        s_foregroundPointer, OpData(Op.CheckForegroundChange), IntPtr.Zero));
#endif
                },
                onNativeFailureResult: message => FireForegroundChangeResult(
                    IosClipboardForegroundChangeResult.Failure(BridgeUnavailableErrorCode, message)));
        }

        private static (string Code, string Message)? ValidatePatterns(IosClipboardDetectionPattern[] patterns)
        {
            if (patterns == null)
            {
                return (InvalidRequestErrorCode, "patterns must not be null.");
            }
            if (patterns.Length == 0)
            {
                // Same code and wording the native layer would return, one round trip earlier.
                return (EmptyPatternsErrorCode, EmptyPatternsMessage);
            }
            return null;
        }

        /// <summary>
        /// Issues the native call. This call already owns the in-flight marker and the per-call
        /// slot, so a P/Invoke failure takes the normal release path rather than the rejected one.
        /// </summary>
        private static void InvokeNative(
            string operation,
            string inFlightKey,
            Action nativeCall,
            Action? onNativeFailure = null,
            Action<string>? onNativeFailureResult = null)
        {
            try
            {
#if UNITY_EDITOR
                if (NativeCallThrowsForTests) throw new InvalidOperationException("native call failed (test)");
#endif
                nativeCall();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{LogTag}][{nameof(InvokeNative)}] {operation}: {ex.Message}");
                // A later frame, as every result decided during the call (design v5, D-12).
                CompleteLater(operation, () =>
                {
                    onNativeFailure?.Invoke();

                    if (onNativeFailureResult != null)
                    {
                        onNativeFailureResult(CouldNotStartMessage(operation));
                        return;
                    }

                    FireOperationResult(
                        IosClipboardOperationResult.Failure(
                            operation, BridgeUnavailableErrorCode, CouldNotStartMessage(operation)),
                        inFlightKey);
                });
            }
        }

        // ── Native callbacks ────────────────────────────────────────────────────

        /// <summary>
        /// Drops a callback belonging to a destroyed Manager lifetime.
        /// <para>
        /// <c>user_data</c> names the operation only, not the Manager's lifetime, so a late callback is
        /// indistinguishable from a fresh one here. The tombstone guarantees no new operation can
        /// have started after destruction, so discarding is always correct: no live caller is
        /// waiting for this result.
        /// </para>
        /// </summary>
        private static bool DiscardIfTerminated(string operation)
        {
            if (!s_isTerminated)
            {
                return false;
            }
            Debug.LogWarning($"[{LogTag}] Discarded a late {operation} callback from a destroyed lifetime.");
            return true;
        }


        private static void HandleOperationCallback(
            string operation,
            string inFlightKey,
            bool isSuccess,
            string? errorCode,
            string? errorMessage,
            bool guarded = false)
        {
            if (!guarded && DiscardIfTerminated(operation))
            {
                return;
            }

            FireOperationResult(
                isSuccess
                    ? IosClipboardOperationResult.Success(operation)
                    : IosClipboardOperationResult.Failure(operation, errorCode, errorMessage),
                inFlightKey);
        }

        /// <summary>
        /// Completion path shared by startObserving and stopObserving. Both use the observation
        /// control key, and both must release the change registration they were responsible for
        /// before the result is dispatched.
        /// </summary>
        private static void HandleObservationControlCallback(
            string operation,
            bool isSuccess,
            string? errorCode,
            string? errorMessage,
            bool acceptedByLibrary = true)
        {
            if (DiscardIfTerminated(operation))
            {
                return;
            }

            // A failed start never began observing, and any stop ends it: both give up the
            // registration, but only if this call is the one that created it.
            if (!isSuccess || operation == OperationStopObserving)
            {
                ReleaseChangeRegistrationIfOwned();
            }
            s_pendingObservationGeneration = 0;

            // D-6: changes are delivered only while this Manager's own observation is on. A call rejected at
            // entry changed nothing natively (a previous observation goes on), so it leaves the state alone.
            if (acceptedByLibrary)
                s_isObserving = operation == OperationStartObserving && isSuccess;

            HandleOperationCallback(operation, ObservationControlKey, isSuccess, errorCode, errorMessage, guarded: true);
        }

        // ── C ABI 2.0.0 (artifact/ios/clipboard, C ABI design v5) ─────────────────

        private static IosClipboardErrorInfo InvalidRequest =>
            IosClipboardCApi.ToErrorInfo(IosClipboardCApi.ErrorInvalidParameter);

        private static IntPtr OpData(Op op) => new IntPtr((int)op);

        /// <summary>The operation name of a <c>user_data</c> (D-2); null for 0 (destroy's stop) and unknown values.</summary>
        internal static string? OperationNameOf(Op op) => op switch
        {
            Op.Copy => OperationCopy,
            Op.Append => OperationAppend,
            Op.Clear => OperationClear,
            Op.RemovePasteboard => OperationRemovePasteboard,
            Op.StartObserving => OperationStartObserving,
            Op.StopObserving => OperationStopObserving,
            Op.Read => OperationRead,
            Op.ReadData => OperationReadData,
            Op.GetSnapshot => OperationGetSnapshot,
            Op.CreatePasteboard => OperationCreatePasteboard,
            Op.DetectPatterns => OperationDetectPatterns,
            Op.DetectValues => OperationDetectValues,
            Op.LoadItem => OperationLoadItem,
            Op.CheckForegroundChange => OperationCheckForegroundChange,
            _ => null,
        };

        /// <summary>A value of the content that ends at a U+0000 is passed on with a warning (D-8 (5)).</summary>
        private static void WarnIfValueHasNul(IosClipboardContent content)
        {
            if (IosClipboardCApi.ContentValueHasNul(content))
                Debug.LogWarning($"[{LogTag}] {IosClipboardCApi.NulValueWarning}");
        }

        /// <summary>
        /// Makes one C ABI call with the memory it needs, and frees it afterwards (the library copies what it
        /// keeps). A rejection becomes the operation's failure on a later Update (D-4). An exception goes to
        /// <see cref="InvokeNative"/>.
        /// </summary>
        private static void CallNative(string operation, Func<List<IntPtr>, int> call)
        {
            Debug.Log($"[{LogTag}][{nameof(CallNative)}] operation: {operation}");
            var allocated = new List<IntPtr>();
            int error;
            try
            {
                error = call(allocated);
            }
            finally
            {
                IosNativeToolkitCApi.FreeAll(allocated);
            }
            if (error != IosClipboardCApi.ErrorNone)
                RejectAtEntry(operation, IosClipboardCApi.ToErrorInfo(error));
        }

        /// <summary>
        /// A failure decided during the call: the library's rejection, or C#'s own check (D-4, D-8). The call
        /// keeps its slot and the busy mark until it is delivered (D-12). A warning, not an error: the input
        /// may be the user's.
        /// </summary>
        private static void RejectAtEntry(string operation, IosClipboardErrorInfo error)
        {
            Debug.LogWarning($"[{LogTag}] {operation} rejected: {error.Code}");
            CompleteLater(operation, () => CompleteWithError(operation, error));
        }

        /// <summary>
        /// Delivers a result decided during the call on a later frame, through the operation's normal
        /// completion path (D-12): never in the frame it was decided in, so a callback that calls again cannot
        /// keep one Update going. Dropped when the Manager was destroyed or reset meanwhile.
        /// </summary>
        private static void CompleteLater(string operation, Action complete)
        {
            Debug.Log($"[{LogTag}][{nameof(CompleteLater)}] operation: {operation}, frame: {Time.frameCount}");
            if (!EnsurePump())
            {
                DropLaterWithoutDispatcher();
                complete(); // no dispatcher: releases the mark; Dispatch reports the dropped result
                return;
            }
            s_later.Add(new Later(Time.frameCount, operation, s_lifetime, complete));
        }

        /// <summary>The pump on the dispatcher's GameObject; false when there is no live dispatcher.</summary>
        private static bool EnsurePump()
        {
            UnityMainThreadDispatcher? dispatcher = s_dispatcher;
            if (dispatcher == null) return false;
            if (s_pump == null)
            {
                IosClipboardLaterPump pump = dispatcher.GetComponent<IosClipboardLaterPump>();
                s_pump = pump != null ? pump : dispatcher.gameObject.AddComponent<IosClipboardLaterPump>();
            }
            return true;
        }

        /// <summary>
        /// Hands the waiting results, in order, to the end of the dispatcher's queue, up to the first one queued
        /// in <paramref name="frame"/> or later (also one queued by a callback). Called by
        /// <see cref="IosClipboardLaterPump"/> every frame; internal for the Editor's tests.
        /// </summary>
        internal static void DeliverLater(int frame)
        {
            if (s_later.Count == 0 || s_later[0].Frame >= frame) return;
            UnityMainThreadDispatcher? dispatcher = s_dispatcher;
            if (dispatcher == null)
            {
                DropLaterWithoutDispatcher();
                return;
            }
            int handed = 0;
            while (s_later.Count > 0 && s_later[0].Frame < frame)
            {
                Later later = s_later[0];
                s_later.RemoveAt(0);
                dispatcher.Enqueue(() => RunLater(later));
                handed++;
            }
            Debug.Log($"[{LogTag}][{nameof(DeliverLater)}] frame: {frame}, handed: {handed}, waiting: {s_later.Count}");
        }

        /// <summary>
        /// The dispatcher is gone: every waiting result is dropped, as <see cref="Dispatch"/> drops one. A result
        /// decided during a call still runs its completion path, so its slot and busy mark are released (its
        /// delivery is then reported as dropped).
        /// </summary>
        private static void DropLaterWithoutDispatcher()
        {
            if (s_later.Count == 0) return;
            Debug.LogError($"[{LogTag}][{nameof(DropLaterWithoutDispatcher)}] No dispatcher; {s_later.Count} waiting result(s) dropped.");
            var waiting = new List<Later>(s_later);
            s_later.Clear();
            foreach (Later later in waiting)
            {
                if (later.Operation == null || s_isTerminated || later.Lifetime != s_lifetime) continue;
                try
                {
                    later.Deliver();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[{LogTag}][{nameof(DropLaterWithoutDispatcher)}] {ex.Message}");
                }
            }
        }

        private static void RunLater(Later later)
        {
            if (later.Operation != null
                    ? DiscardIfTerminated(later.Operation) || later.Lifetime != s_lifetime
                    : later.Lifetime != s_resetCount)
            {
                return;
            }
            s_deliverInline = later.Operation != null;
            try
            {
                later.Deliver();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{LogTag}][{nameof(RunLater)}] {ex.Message}");
            }
            finally
            {
                s_deliverInline = false;
            }
        }

        /// <summary>
        /// On destroy: results that were already known still go out through the dispatcher, in order; those
        /// decided during a call are dropped, as late completions are.
        /// </summary>
        private static void HandOverLaterResults()
        {
            var waiting = new List<Later>(s_later);
            s_later.Clear();
            UnityMainThreadDispatcher? dispatcher = s_dispatcher;
            Debug.Log($"[{LogTag}][{nameof(HandOverLaterResults)}] waiting: {waiting.Count}");
            foreach (Later later in waiting)
            {
                if (later.Operation != null) continue;
                if (dispatcher == null)
                {
                    Debug.LogError($"[{LogTag}][{nameof(HandOverLaterResults)}] No dispatcher; result dropped.");
                    continue;
                }
                Later known = later;
                dispatcher.Enqueue(() => RunLater(known)); // dropped by a reset before it runs
            }
        }

        /// <summary>Completes an operation with a failure through its normal path (slot, mark, generation).</summary>
        private static void CompleteWithError(string operation, IosClipboardErrorInfo error)
        {
            Debug.Log($"[{LogTag}][{nameof(CompleteWithError)}] operation: {operation}, code: {error.Code}");
            switch (operation)
            {
                case OperationStartObserving:
                case OperationStopObserving:
                    if (operation == OperationStartObserving && s_pendingObservationGeneration != 0)
                        s_revokedRegistrations.Add(s_pendingObservationGeneration);
                    HandleObservationControlCallback(operation, false, error.Code, error.Message, acceptedByLibrary: false);
                    return;
                case OperationRead:
                    FireReadResult(IosClipboardReadResult.Failure(error));
                    return;
                case OperationReadData:
                    FireReadDataResult(IosClipboardReadDataResult.Failure(error));
                    return;
                case OperationGetSnapshot:
                    FireSnapshotResult(IosClipboardSnapshotResult.Failure(error));
                    return;
                case OperationCreatePasteboard:
                    FireCreatePasteboardResult(IosPasteboardScopeResult.Failure(error));
                    return;
                case OperationDetectPatterns:
                    FireDetectPatternsResult(IosClipboardDetectedPatternsResult.Failure(error));
                    return;
                case OperationDetectValues:
                    FireDetectValuesResult(IosClipboardDetectedValuesResult.Failure(error));
                    return;
                case OperationLoadItem:
                    s_loadRequestId = 0;
                    FireLoadItemResult(IosClipboardLoadedItemResult.Failure(error));
                    return;
                case OperationCheckForegroundChange:
                    FireForegroundChangeResult(IosClipboardForegroundChangeResult.Failure(error));
                    return;
                default:
                    FireOperationResult(IosClipboardOperationResult.Failure(operation, error), operation);
                    return;
            }
        }

        /// <summary>
        /// Whether a change is dropped (D-6): after destroy, or while this Manager's own observation is off.
        /// Shared by the player's listener and the Editor's test seam.
        /// </summary>
        private static bool DropsChange() => DiscardIfTerminated(OperationStartObserving + " change") || !s_isObserving;

        /// <summary>
        /// The result of a completion that carries a handle (D-9): the error, UNKNOWN for NONE without a handle,
        /// or what <paramref name="read"/> builds; UNKNOWN when reading throws. The caller frees the handle.
        /// </summary>
        internal static TResult ResultOf<TResult>(
            int error, IntPtr handle, Func<IosClipboardErrorInfo, TResult> failure, Func<IntPtr, TResult> read)
        {
            if (error != IosClipboardCApi.ErrorNone) return failure(IosClipboardCApi.ToErrorInfo(error));
            if (handle == IntPtr.Zero) return failure(IosClipboardCApi.MissingResult);
            try
            {
                return read(handle);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{LogTag}] could not read a clipboard result: {ex.Message}");
                return failure(IosClipboardCApi.ToErrorInfo(IosClipboardCApi.ErrorUnknown));
            }
        }

#if UNITY_IOS && !UNITY_EDITOR
        // ── The completions (main thread). One per C ABI callback type; user_data names the operation (D-2).
        // Kept for the life of the process, so the pointers handed to the C ABI stay valid. Internal for the
        // player tests. No exception leaves; a handle is freed on every path. ──

        private static readonly IosClipboardCApi.DoneCallback s_done = OnDone;
        internal static readonly IntPtr s_donePointer = Marshal.GetFunctionPointerForDelegate(s_done);
        private static readonly IosClipboardCApi.HandleCallback s_read = OnRead;
        internal static readonly IntPtr s_readPointer = Marshal.GetFunctionPointerForDelegate(s_read);
        private static readonly IosClipboardCApi.HandleCallback s_data = OnData;
        internal static readonly IntPtr s_dataPointer = Marshal.GetFunctionPointerForDelegate(s_data);
        private static readonly IosClipboardCApi.HandleCallback s_snapshot = OnSnapshot;
        internal static readonly IntPtr s_snapshotPointer = Marshal.GetFunctionPointerForDelegate(s_snapshot);
        private static readonly IosClipboardCApi.HandleCallback s_name = OnName;
        internal static readonly IntPtr s_namePointer = Marshal.GetFunctionPointerForDelegate(s_name);
        private static readonly IosClipboardCApi.PatternsCallback s_patterns = OnPatterns;
        internal static readonly IntPtr s_patternsPointer = Marshal.GetFunctionPointerForDelegate(s_patterns);
        private static readonly IosClipboardCApi.HandleCallback s_values = OnValues;
        internal static readonly IntPtr s_valuesPointer = Marshal.GetFunctionPointerForDelegate(s_values);
        private static readonly IosClipboardCApi.LoadCallback s_load = OnLoad;
        internal static readonly IntPtr s_loadPointer = Marshal.GetFunctionPointerForDelegate(s_load);
        private static readonly IosClipboardCApi.ForegroundChangeCallback s_foreground = OnForegroundChange;
        internal static readonly IntPtr s_foregroundPointer = Marshal.GetFunctionPointerForDelegate(s_foreground);
        private static readonly IosClipboardCApi.ChangeCallback s_change = OnChange;
        internal static readonly IntPtr s_changePointer = Marshal.GetFunctionPointerForDelegate(s_change);
        private static readonly IosClipboardCApi.ReleaseCallback s_listenerRelease = OnListenerReleased;
        internal static readonly IntPtr s_listenerReleasePointer = Marshal.GetFunctionPointerForDelegate(s_listenerRelease);

        [MonoPInvokeCallback(typeof(IosClipboardCApi.DoneCallback))]
        private static void OnDone(IntPtr userData, int error, uint systemCode)
        {
            try
            {
                var op = (Op)userData.ToInt64();
                string? operation = OperationNameOf(op);
                if (operation == null) return; // destroy's stop (D-2)

                IosClipboardOperationResult result = IosClipboardCApi.OperationResultOf(operation, error);
                string? code = result.Error?.Code;
                string? message = result.Error?.Message;
                if (op == Op.StartObserving || op == Op.StopObserving)
                    HandleObservationControlCallback(operation, result.IsSuccess, code, message);
                else
                    HandleOperationCallback(operation, operation, result.IsSuccess, code, message);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{LogTag}][{nameof(OnDone)}] {ex.Message}");
            }
        }

        [MonoPInvokeCallback(typeof(IosClipboardCApi.HandleCallback))]
        private static void OnRead(IntPtr userData, int error, uint systemCode, IntPtr handle)
        {
            try
            {
                if (DiscardIfTerminated(OperationRead)) return;
                FireReadResult(ResultOf<IosClipboardReadResult>(error, handle, IosClipboardReadResult.Failure, IosClipboardCApi.ReadReadResult));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{LogTag}][{nameof(OnRead)}] {ex.Message}");
            }
            finally
            {
                if (handle != IntPtr.Zero) IosClipboardCApi.ntk_clipboard_read_result_free(handle);
            }
        }

        [MonoPInvokeCallback(typeof(IosClipboardCApi.HandleCallback))]
        private static void OnData(IntPtr userData, int error, uint systemCode, IntPtr bytes)
        {
            try
            {
                if (DiscardIfTerminated(OperationReadData)) return;
                IosClipboardReadDataResult result;
                if (error != IosClipboardCApi.ErrorNone)
                {
                    result = IosClipboardReadDataResult.Failure(IosClipboardCApi.ToErrorInfo(error));
                }
                else
                {
                    // NULL is "no data", not a missing result (table 2.1).
                    try
                    {
                        result = IosClipboardCApi.ReadReadData(bytes, s_readDataUtType);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[{LogTag}] could not read a clipboard result: {ex.Message}");
                        result = IosClipboardReadDataResult.Failure(IosClipboardCApi.ToErrorInfo(IosClipboardCApi.ErrorUnknown));
                    }
                }
                FireReadDataResult(result);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{LogTag}][{nameof(OnData)}] {ex.Message}");
            }
            finally
            {
                if (bytes != IntPtr.Zero) IosClipboardCApi.ntk_bytes_free(bytes);
            }
        }

        [MonoPInvokeCallback(typeof(IosClipboardCApi.HandleCallback))]
        private static void OnSnapshot(IntPtr userData, int error, uint systemCode, IntPtr handle)
        {
            try
            {
                if (DiscardIfTerminated(OperationGetSnapshot)) return;
                FireSnapshotResult(ResultOf<IosClipboardSnapshotResult>(error, handle, IosClipboardSnapshotResult.Failure,
                    h => IosClipboardSnapshotResult.Success(IosClipboardCApi.ReadSnapshot(h))));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{LogTag}][{nameof(OnSnapshot)}] {ex.Message}");
            }
            finally
            {
                if (handle != IntPtr.Zero) IosClipboardCApi.ntk_clipboard_snapshot_free(handle);
            }
        }

        [MonoPInvokeCallback(typeof(IosClipboardCApi.HandleCallback))]
        private static void OnName(IntPtr userData, int error, uint systemCode, IntPtr name)
        {
            try
            {
                if (DiscardIfTerminated(OperationCreatePasteboard)) return;
                FireCreatePasteboardResult(ResultOf<IosPasteboardScopeResult>(error, name, IosPasteboardScopeResult.Failure,
                    h => IosPasteboardScopeResult.Success(
                        IosClipboardCApi.CreatedScopeOf(s_createKind, IosClipboardCApi.ReadString(h)))));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{LogTag}][{nameof(OnName)}] {ex.Message}");
            }
            finally
            {
                if (name != IntPtr.Zero) IosClipboardCApi.ntk_string_free(name);
            }
        }

        [MonoPInvokeCallback(typeof(IosClipboardCApi.PatternsCallback))]
        private static void OnPatterns(IntPtr userData, int error, uint systemCode, uint patterns)
        {
            try
            {
                if (DiscardIfTerminated(OperationDetectPatterns)) return;
                FireDetectPatternsResult(error == IosClipboardCApi.ErrorNone
                    ? IosClipboardDetectedPatternsResult.Success(IosClipboardCApi.PatternsOf(patterns))
                    : IosClipboardDetectedPatternsResult.Failure(IosClipboardCApi.ToErrorInfo(error)));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{LogTag}][{nameof(OnPatterns)}] {ex.Message}");
            }
        }

        [MonoPInvokeCallback(typeof(IosClipboardCApi.HandleCallback))]
        private static void OnValues(IntPtr userData, int error, uint systemCode, IntPtr json)
        {
            try
            {
                if (DiscardIfTerminated(OperationDetectValues)) return;
                FireDetectValuesResult(ResultOf<IosClipboardDetectedValuesResult>(error, json, IosClipboardDetectedValuesResult.Failure,
                    h => IosClipboardJsonParser.ParseBareDetectedValues(IosClipboardCApi.ReadString(h))));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{LogTag}][{nameof(OnValues)}] {ex.Message}");
            }
            finally
            {
                if (json != IntPtr.Zero) IosClipboardCApi.ntk_string_free(json);
            }
        }

        [MonoPInvokeCallback(typeof(IosClipboardCApi.LoadCallback))]
        private static void OnLoad(IntPtr userData, ulong requestId, int error, uint systemCode, IntPtr item)
        {
            try
            {
                if (requestId == s_loadRequestId) s_loadRequestId = 0;
                if (DiscardIfTerminated(OperationLoadItem)) return;
                FireLoadItemResult(ResultOf<IosClipboardLoadedItemResult>(error, item, IosClipboardLoadedItemResult.Failure,
                    h => IosClipboardLoadedItemResult.Success(IosClipboardCApi.ReadLoadedItem(h))));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{LogTag}][{nameof(OnLoad)}] {ex.Message}");
            }
            finally
            {
                if (item != IntPtr.Zero) IosClipboardCApi.ntk_clipboard_loaded_item_free(item);
            }
        }

        [MonoPInvokeCallback(typeof(IosClipboardCApi.ForegroundChangeCallback))]
        private static void OnForegroundChange(IntPtr userData, int error, uint systemCode, int changed)
        {
            try
            {
                if (DiscardIfTerminated(OperationCheckForegroundChange)) return;
                FireForegroundChangeResult(error == IosClipboardCApi.ErrorNone
                    ? IosClipboardForegroundChangeResult.Success(changed != 0)
                    : IosClipboardForegroundChangeResult.Failure(IosClipboardCApi.ToErrorInfo(error)));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{LogTag}][{nameof(OnForegroundChange)}] {ex.Message}");
            }
        }

        /// <summary>A change of the observed pasteboard (D-6): dropped after destroy or while the Manager is not observing.</summary>
        [MonoPInvokeCallback(typeof(IosClipboardCApi.ChangeCallback))]
        private static void OnChange(IntPtr userData, IntPtr change)
        {
            try
            {
                if (DropsChange() || change == IntPtr.Zero) return;
                IosClipboardChangeEvent changeEvent;
                try
                {
                    changeEvent = IosClipboardCApi.ReadChange(change);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[{LogTag}] dropped a clipboard change that could not be read: {ex.Message}");
                    return;
                }
                FireClipboardChanged(changeEvent);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{LogTag}][{nameof(OnChange)}] {ex.Message}");
            }
            finally
            {
                if (change != IntPtr.Zero) IosClipboardCApi.ntk_clipboard_change_free(change);
            }
        }

        [MonoPInvokeCallback(typeof(IosClipboardCApi.ReleaseCallback))]
        private static void OnListenerReleased(IntPtr userData) => Interlocked.Increment(ref s_listenerReleaseCount);
#endif

#if UNITY_EDITOR
        // ── Editor-only completion seams ────────────────────────────────────────
        // These drive the same production completion paths a native callback would, so tests can
        // observe real pending-slot, in-flight and generation transitions. Compiled out of player
        // builds; they never bypass DiscardIfTerminated or the single-flight release.

        internal static void CompleteOperationForTests(
            string operation, bool isSuccess, string? errorCode = null, string? errorMessage = null) =>
            HandleOperationCallback(operation, operation, isSuccess, errorCode, errorMessage);

        internal static void CompleteObservationControlForTests(
            string operation, bool isSuccess, string? errorCode = null, string? errorMessage = null) =>
            HandleObservationControlCallback(operation, isSuccess, errorCode, errorMessage);

        internal static void CompleteReadForTests(string? json)
        {
            if (DiscardIfTerminated(OperationRead)) return;
            FireReadResult(IosClipboardJsonParser.ParseReadResult(json));
        }

        internal static void CompleteSnapshotForTests(string? json)
        {
            if (DiscardIfTerminated(OperationGetSnapshot)) return;
            FireSnapshotResult(IosClipboardJsonParser.ParseSnapshotResult(json));
        }

        internal static void DeliverChangeEventForTests(string? eventJson)
        {
            if (DropsChange()) return;
            IosClipboardChangeEvent? changeEvent = IosClipboardJsonParser.ParseChangeEvent(eventJson);
            if (changeEvent == null) return;
            FireClipboardChanged(changeEvent);
        }

        internal static bool IsInFlightForTests(string key) => s_inFlight.Contains(key);

        /// <summary>Drives D-12 as the library's rejection of a pending call would (C ABI error number).</summary>
        internal static void RejectAtEntryForTests(string operation, int error) =>
            RejectAtEntry(operation, IosClipboardCApi.ToErrorInfo(error));

        internal static bool IsObservingForTests => s_isObserving;

        internal static int LaterCountForTests => s_later.Count;

        /// <summary>Makes the native call of every operation throw, as a missing symbol would (D-12).</summary>
        internal static bool NativeCallThrowsForTests { get; set; }

        internal static bool HasChangeRegistrationForTests => s_onChanged != null;

        internal static ulong PendingObservationGenerationForTests => s_pendingObservationGeneration;

        internal static bool HasAnyPendingCallbackForTests =>
            s_onCopy != null || s_onAppend != null || s_onClear != null || s_onRemovePasteboard != null ||
            s_onCancelLoads != null || s_onStartObserving != null || s_onStopObserving != null ||
            s_onRead != null || s_onReadData != null || s_onSnapshot != null || s_onCreatePasteboard != null ||
            s_onDetectPatterns != null || s_onDetectValues != null || s_onLoadItem != null ||
            s_onCheckForegroundChange != null || s_onChanged != null;

        internal static int InFlightCountForTests => s_inFlight.Count;
#endif
    }
}
#endif
