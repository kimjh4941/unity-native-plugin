#nullable enable

#if UNITY_IOS || UNITY_EDITOR
namespace JonghyunKim.NativeToolkit.Runtime.Clipboard
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using System.Threading;
#if UNITY_IOS && !UNITY_EDITOR
    using AOT;
#endif
    using JonghyunKim.NativeToolkit.Runtime.Common;
    using UnityEngine;

    /// <summary>
    /// Singleton manager for iOS native clipboard operations.
    /// Wraps <c>UnityIosClipboardManager</c> (Swift) via P/Invoke through the C ABI bridge.
    /// <para>
    /// <b>Threading.</b> Every member, including <see cref="Instance"/>, must be used from the
    /// Unity main thread. Calling an instance method from another thread is rejected with
    /// <c>CLIPBOARD_MAIN_THREAD_REQUIRED</c>; the <see cref="Instance"/> getter itself cannot be
    /// guarded, because it may create a GameObject before any check could run.
    /// </para>
    /// <para>
    /// <b>Concurrency.</b> The native ABI carries no request identifier, so two concurrent calls to
    /// the same operation would be indistinguishable when their callbacks arrive. Each operation is
    /// therefore single-flight: a second call while one is pending fails immediately with
    /// <c>CLIPBOARD_BUSY</c> and leaves the pending call untouched. Different operations still run
    /// concurrently.
    /// </para>
    /// <para>
    /// <b>Lifetime.</b> Destroying this Manager is not supported. Once <c>OnDestroy</c> has run,
    /// every operation is rejected with <c>CLIPBOARD_MANAGER_DESTROYED</c> and late native
    /// callbacks are discarded, because a callback from the destroyed lifetime would otherwise be
    /// delivered to a freshly started call. See <see cref="IsTerminated"/>.
    /// </para>
    /// <para>
    /// The class compiles for <c>UNITY_IOS || UNITY_EDITOR</c> so it can be referenced and tested
    /// from the Editor regardless of build target; the native P/Invoke declarations and callbacks
    /// are restricted to <c>UNITY_IOS &amp;&amp; !UNITY_EDITOR</c> (device IL2CPP builds only).
    /// </para>
    /// <para>
    /// Intentional deviation from the "log every parameter" rule in csharp.md: clipboard content
    /// may hold passwords or tokens, so only shapes, counts and flags are logged, never values.
    /// This matches the native <c>ClipboardRedaction</c> policy and the Android manager.
    /// </para>
    /// </summary>
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

        // ── Native callback delegates ───────────────────────────────────────────

        // The C header declares the first argument as C `bool` (stdbool, 1 byte). C# marshals a
        // bare bool as a 4-byte Win32 BOOL by default, so the width is pinned explicitly.
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void ClipboardOperationCallback(
            [MarshalAs(UnmanagedType.I1)] bool isSuccess,
            string? errorCode,
            string? errorMessage);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void ClipboardJsonCallback(string? json);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void ClipboardChangeCallback(string? eventJson);

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void clipboardCopy(string requestJson, ClipboardOperationCallback callback);

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void clipboardAppend(string requestJson, ClipboardOperationCallback callback);

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void clipboardRead(string requestJson, ClipboardJsonCallback callback);

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void clipboardReadData(string requestJson, ClipboardJsonCallback callback);

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void clipboardGetSnapshot(string requestJson, ClipboardJsonCallback callback);

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void clipboardClear(string requestJson, ClipboardOperationCallback callback);

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void clipboardCreatePasteboard(string requestJson, ClipboardJsonCallback callback);

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void clipboardRemovePasteboard(string requestJson, ClipboardOperationCallback callback);

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void clipboardDetectPatterns(string requestJson, ClipboardJsonCallback callback);

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void clipboardDetectValues(string requestJson, ClipboardJsonCallback callback);

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void clipboardLoadItem(string requestJson, ClipboardJsonCallback callback);

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void clipboardCancelLoads(ClipboardOperationCallback? callback);

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void clipboardStartObserving(
            string requestJson,
            ClipboardChangeCallback changeCallback,
            ClipboardOperationCallback startCallback);

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void clipboardStopObserving(ClipboardOperationCallback? callback);

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void clipboardCheckForegroundChange(string requestJson, ClipboardJsonCallback callback);

        // Persistent delegates: stored permanently so the native function pointers stay valid.
        private static readonly ClipboardOperationCallback s_copyDelegate = OnCopyResult;
        private static readonly ClipboardOperationCallback s_appendDelegate = OnAppendResult;
        private static readonly ClipboardOperationCallback s_clearDelegate = OnClearResult;
        private static readonly ClipboardOperationCallback s_removePasteboardDelegate = OnRemovePasteboardResult;
        private static readonly ClipboardOperationCallback s_cancelLoadsDelegate = OnCancelLoadsResult;
        private static readonly ClipboardOperationCallback s_startObservingDelegate = OnStartObservingResult;
        private static readonly ClipboardOperationCallback s_stopObservingDelegate = OnStopObservingResult;
        private static readonly ClipboardJsonCallback s_readDelegate = OnReadResult;
        private static readonly ClipboardJsonCallback s_readDataDelegate = OnReadDataResult;
        private static readonly ClipboardJsonCallback s_snapshotDelegate = OnSnapshotResult;
        private static readonly ClipboardJsonCallback s_createPasteboardDelegate = OnCreatePasteboardResult;
        private static readonly ClipboardJsonCallback s_detectPatternsDelegate = OnDetectPatternsResult;
        private static readonly ClipboardJsonCallback s_detectValuesDelegate = OnDetectValuesResult;
        private static readonly ClipboardJsonCallback s_loadItemDelegate = OnLoadItemResult;
        private static readonly ClipboardJsonCallback s_checkForegroundChangeDelegate = OnCheckForegroundChangeResult;

        // Invoked repeatedly for the lifetime of an observation, so it must never be collected.
        private static readonly ClipboardChangeCallback s_changeDelegate = OnClipboardChanged;
#endif

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
                cancel: CancelLoadsForTeardown,
                managedCleanup: () =>
                {
                    try { ClearAllPendingCallbacks(); }
                    finally { _instance = null; }
                });

            // s_dispatcher is deliberately left set: post-destruction rejections still need it.
        }

        /// <summary>
        /// Runs the three teardown steps with the exception boundaries the destroy contract needs:
        /// stop and cancel are isolated from each other, and managedCleanup always runs.
        /// <para>
        /// Pure with respect to Manager state, so tests can pass throwing actions directly instead
        /// of the Manager exposing a mutable, swappable hook that would also ship in player builds.
        /// </para>
        /// </summary>
        /// <param name="stop">Stops native change observation.</param>
        /// <param name="cancel">Cancels pending native item loads.</param>
        /// <param name="managedCleanup">Clears managed state. Always invoked.</param>
        internal static void RunDestroyCleanup(Action stop, Action cancel, Action managedCleanup)
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

        private static void StopObservingForTeardown()
        {
#if UNITY_IOS && !UNITY_EDITOR
            if (Application.platform == RuntimePlatform.IPhonePlayer)
            {
                clipboardStopObserving(null);
            }
#endif
        }

        private static void CancelLoadsForTeardown()
        {
#if UNITY_IOS && !UNITY_EDITOR
            if (Application.platform == RuntimePlatform.IPhonePlayer)
            {
                clipboardCancelLoads(null);
            }
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
        // two can never drift apart. Resets every mutable static except _instance, whose lifetime
        // is owned by Unity.
        private static void ResetCore()
        {
            s_isTerminated = false;
            s_dispatcher = null;
            s_mainThreadId = 0;
#if UNITY_EDITOR
            BridgeAvailableOverrideForTests = false;
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
#if UNITY_IOS && !UNITY_EDITOR
            return Application.platform == RuntimePlatform.IPhonePlayer;
#elif UNITY_EDITOR
            return BridgeAvailableOverrideForTests;
#else
            return false;
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
            UnityMainThreadDispatcher? dispatcher = s_dispatcher;
            if (dispatcher == null)
            {
                Debug.LogError($"[{LogTag}][{nameof(Dispatch)}] No dispatcher; result dropped.");
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
            Dispatch(changeEvent, _instance?.ClipboardChanged, s_onChanged);
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
            InvokeNative(
                OperationCopy,
                OperationCopy,
                () =>
                {
#if UNITY_IOS && !UNITY_EDITOR
                    clipboardCopy(IosClipboardJsonBuilder.BuildCopyJson(content, scope, options), s_copyDelegate);
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
            InvokeNative(
                OperationAppend,
                OperationAppend,
                () =>
                {
#if UNITY_IOS && !UNITY_EDITOR
                    clipboardAppend(IosClipboardJsonBuilder.BuildAppendJson(content, scope), s_appendDelegate);
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
            InvokeNative(
                OperationClear,
                OperationClear,
                () =>
                {
#if UNITY_IOS && !UNITY_EDITOR
                    clipboardClear(IosClipboardJsonBuilder.BuildClearJson(scope), s_clearDelegate);
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
            InvokeNative(
                OperationRemovePasteboard,
                OperationRemovePasteboard,
                () =>
                {
#if UNITY_IOS && !UNITY_EDITOR
                    clipboardRemovePasteboard(
                        IosClipboardJsonBuilder.BuildRemovePasteboardJson(scope), s_removePasteboardDelegate);
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
                    clipboardCancelLoads(s_cancelLoadsDelegate);
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

            InvokeNative(
                OperationStartObserving,
                ObservationControlKey,
                () =>
                {
#if UNITY_IOS && !UNITY_EDITOR
                    clipboardStartObserving(
                        IosClipboardJsonBuilder.BuildStartObservingJson(scope),
                        s_changeDelegate,
                        s_startObservingDelegate);
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
                    clipboardStopObserving(s_stopObservingDelegate);
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
            InvokeNative(
                OperationRead,
                OperationRead,
                () =>
                {
#if UNITY_IOS && !UNITY_EDITOR
                    clipboardRead(IosClipboardJsonBuilder.BuildReadJson(scope), s_readDelegate);
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
            InvokeNative(
                OperationReadData,
                OperationReadData,
                () =>
                {
#if UNITY_IOS && !UNITY_EDITOR
                    clipboardReadData(IosClipboardJsonBuilder.BuildReadDataJson(utType, scope), s_readDataDelegate);
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
            InvokeNative(
                OperationGetSnapshot,
                OperationGetSnapshot,
                () =>
                {
#if UNITY_IOS && !UNITY_EDITOR
                    clipboardGetSnapshot(
                        IosClipboardJsonBuilder.BuildGetSnapshotJson(scope, matchingTypes), s_snapshotDelegate);
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
            InvokeNative(
                OperationCreatePasteboard,
                OperationCreatePasteboard,
                () =>
                {
#if UNITY_IOS && !UNITY_EDITOR
                    clipboardCreatePasteboard(
                        IosClipboardJsonBuilder.BuildCreatePasteboardJson(request), s_createPasteboardDelegate);
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
            InvokeNative(
                OperationDetectPatterns,
                OperationDetectPatterns,
                () =>
                {
#if UNITY_IOS && !UNITY_EDITOR
                    clipboardDetectPatterns(
                        IosClipboardJsonBuilder.BuildDetectPatternsJson(patterns, scope), s_detectPatternsDelegate);
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
            InvokeNative(
                OperationDetectValues,
                OperationDetectValues,
                () =>
                {
#if UNITY_IOS && !UNITY_EDITOR
                    clipboardDetectValues(
                        IosClipboardJsonBuilder.BuildDetectValuesJson(patterns, scope), s_detectValuesDelegate);
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
            InvokeNative(
                OperationLoadItem,
                OperationLoadItem,
                () =>
                {
#if UNITY_IOS && !UNITY_EDITOR
                    clipboardLoadItem(IosClipboardJsonBuilder.BuildLoadItemJson(request, scope), s_loadItemDelegate);
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
            InvokeNative(
                OperationCheckForegroundChange,
                OperationCheckForegroundChange,
                () =>
                {
#if UNITY_IOS && !UNITY_EDITOR
                    clipboardCheckForegroundChange(
                        IosClipboardJsonBuilder.BuildCheckForegroundChangeJson(scope),
                        s_checkForegroundChangeDelegate);
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
                nativeCall();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{LogTag}][{nameof(InvokeNative)}] {operation}: {ex.Message}");
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
            }
        }

        // ── Native callbacks ────────────────────────────────────────────────────

        /// <summary>
        /// Drops a callback belonging to a destroyed Manager lifetime.
        /// <para>
        /// The native ABI carries no request or lifetime identifier, so a late callback is
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

#if UNITY_IOS && !UNITY_EDITOR
        [MonoPInvokeCallback(typeof(ClipboardOperationCallback))]
        private static void OnCopyResult(bool isSuccess, string? errorCode, string? errorMessage) =>
            HandleOperationCallback(OperationCopy, OperationCopy, isSuccess, errorCode, errorMessage);

        [MonoPInvokeCallback(typeof(ClipboardOperationCallback))]
        private static void OnAppendResult(bool isSuccess, string? errorCode, string? errorMessage) =>
            HandleOperationCallback(OperationAppend, OperationAppend, isSuccess, errorCode, errorMessage);

        [MonoPInvokeCallback(typeof(ClipboardOperationCallback))]
        private static void OnClearResult(bool isSuccess, string? errorCode, string? errorMessage) =>
            HandleOperationCallback(OperationClear, OperationClear, isSuccess, errorCode, errorMessage);

        [MonoPInvokeCallback(typeof(ClipboardOperationCallback))]
        private static void OnRemovePasteboardResult(bool isSuccess, string? errorCode, string? errorMessage) =>
            HandleOperationCallback(
                OperationRemovePasteboard, OperationRemovePasteboard, isSuccess, errorCode, errorMessage);

        [MonoPInvokeCallback(typeof(ClipboardOperationCallback))]
        private static void OnCancelLoadsResult(bool isSuccess, string? errorCode, string? errorMessage) =>
            HandleOperationCallback(OperationCancelLoads, OperationCancelLoads, isSuccess, errorCode, errorMessage);

        [MonoPInvokeCallback(typeof(ClipboardOperationCallback))]
        private static void OnStartObservingResult(bool isSuccess, string? errorCode, string? errorMessage) =>
            HandleObservationControlCallback(OperationStartObserving, isSuccess, errorCode, errorMessage);

        [MonoPInvokeCallback(typeof(ClipboardOperationCallback))]
        private static void OnStopObservingResult(bool isSuccess, string? errorCode, string? errorMessage) =>
            HandleObservationControlCallback(OperationStopObserving, isSuccess, errorCode, errorMessage);

        [MonoPInvokeCallback(typeof(ClipboardJsonCallback))]
        private static void OnReadResult(string? json)
        {
            if (DiscardIfTerminated(OperationRead)) return;
            FireReadResult(IosClipboardJsonParser.ParseReadResult(json));
        }

        [MonoPInvokeCallback(typeof(ClipboardJsonCallback))]
        private static void OnReadDataResult(string? json)
        {
            if (DiscardIfTerminated(OperationReadData)) return;
            FireReadDataResult(IosClipboardJsonParser.ParseReadDataResult(json));
        }

        [MonoPInvokeCallback(typeof(ClipboardJsonCallback))]
        private static void OnSnapshotResult(string? json)
        {
            if (DiscardIfTerminated(OperationGetSnapshot)) return;
            FireSnapshotResult(IosClipboardJsonParser.ParseSnapshotResult(json));
        }

        [MonoPInvokeCallback(typeof(ClipboardJsonCallback))]
        private static void OnCreatePasteboardResult(string? json)
        {
            if (DiscardIfTerminated(OperationCreatePasteboard)) return;
            FireCreatePasteboardResult(IosClipboardJsonParser.ParsePasteboardScopeResult(json));
        }

        [MonoPInvokeCallback(typeof(ClipboardJsonCallback))]
        private static void OnDetectPatternsResult(string? json)
        {
            if (DiscardIfTerminated(OperationDetectPatterns)) return;
            FireDetectPatternsResult(IosClipboardJsonParser.ParseDetectedPatternsResult(json));
        }

        [MonoPInvokeCallback(typeof(ClipboardJsonCallback))]
        private static void OnDetectValuesResult(string? json)
        {
            if (DiscardIfTerminated(OperationDetectValues)) return;
            FireDetectValuesResult(IosClipboardJsonParser.ParseDetectedValuesResult(json));
        }

        [MonoPInvokeCallback(typeof(ClipboardJsonCallback))]
        private static void OnLoadItemResult(string? json)
        {
            if (DiscardIfTerminated(OperationLoadItem)) return;
            FireLoadItemResult(IosClipboardJsonParser.ParseLoadedItemResult(json));
        }

        [MonoPInvokeCallback(typeof(ClipboardJsonCallback))]
        private static void OnCheckForegroundChangeResult(string? json)
        {
            if (DiscardIfTerminated(OperationCheckForegroundChange)) return;
            FireForegroundChangeResult(IosClipboardJsonParser.ParseForegroundChangeResult(json));
        }

        [MonoPInvokeCallback(typeof(ClipboardChangeCallback))]
        private static void OnClipboardChanged(string? eventJson)
        {
            // Guard first: a payload from a destroyed lifetime is not parsed at all.
            if (DiscardIfTerminated(OperationStartObserving)) return;

            IosClipboardChangeEvent? changeEvent = IosClipboardJsonParser.ParseChangeEvent(eventJson);
            if (changeEvent == null)
            {
                return; // dropped; see IosClipboardJsonParser.ParseChangeEvent
            }
            FireClipboardChanged(changeEvent);
        }
#endif

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
            string? errorMessage)
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

            HandleOperationCallback(operation, ObservationControlKey, isSuccess, errorCode, errorMessage, guarded: true);
        }

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
            if (DiscardIfTerminated(OperationStartObserving)) return;
            IosClipboardChangeEvent? changeEvent = IosClipboardJsonParser.ParseChangeEvent(eventJson);
            if (changeEvent == null) return;
            FireClipboardChanged(changeEvent);
        }

        internal static bool IsInFlightForTests(string key) => s_inFlight.Contains(key);

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
