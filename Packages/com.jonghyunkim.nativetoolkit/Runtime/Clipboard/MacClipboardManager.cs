#nullable enable

#if UNITY_STANDALONE_OSX || UNITY_EDITOR
namespace JonghyunKim.NativeToolkit.Runtime.Clipboard
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using System.Threading;
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
    using AOT;
#endif
    using JonghyunKim.NativeToolkit.Runtime.Common;
    using UnityEngine;

    /// <summary>
    /// Singleton manager for macOS native clipboard operations.
    /// Wraps <c>UnityMacClipboardManager</c> (Swift) via P/Invoke through the C ABI bridge.
    /// <para>
    /// <b>Threading.</b> Every member, including <see cref="Instance"/>, must be used from the
    /// Unity main thread. Calling an instance method from another thread is rejected with
    /// <see cref="MacClipboardErrorCodes.MainThreadRequired"/>; the <see cref="Instance"/> getter
    /// itself cannot be guarded, because it may create a GameObject before any check could run.
    /// </para>
    /// <para>
    /// <b>Concurrency.</b> The native ABI carries no request identifier, so two concurrent calls to
    /// the same operation would be indistinguishable when their callbacks arrive. Each operation is
    /// therefore single-flight: a second call while one is pending fails immediately with
    /// <see cref="MacClipboardErrorCodes.Busy"/> and leaves the pending call untouched. Different
    /// operations still run concurrently.
    /// </para>
    /// <para>
    /// <b>Lifetime.</b> Destroying this Manager is not supported. Once <c>OnDestroy</c> has run,
    /// every operation is rejected with <see cref="MacClipboardErrorCodes.ManagerDestroyed"/> and
    /// late native callbacks are discarded, because a callback from the destroyed lifetime would
    /// otherwise be delivered to a freshly started call. See <see cref="IsTerminated"/>.
    /// </para>
    /// <para>
    /// <b>Request size.</b> <see cref="MacClipboardLimits.MaxRequestBytes"/> is the effective limit
    /// callers see. It is smaller than the native limit and is enforced here, before the content is
    /// base64 encoded, because the encoded form is a third larger again.
    /// </para>
    /// <para>
    /// The class compiles for <c>UNITY_STANDALONE_OSX || UNITY_EDITOR</c> so it can be referenced
    /// and tested from the Editor regardless of build target; the native P/Invoke declarations and
    /// callbacks are restricted to <c>UNITY_STANDALONE_OSX &amp;&amp; !UNITY_EDITOR</c> (macOS
    /// Standalone player builds only).
    /// </para>
    /// <para>
    /// Intentional deviation from the "log every parameter" rule in csharp.md: clipboard content
    /// may hold passwords or tokens, so only shapes, counts and flags are logged, never values, and
    /// never a pasteboard name. This matches the native <c>ClipboardLog</c> redaction policy.
    /// </para>
    /// <para>
    /// Intentional deviation from the "log on the first line of every method" rule in csharp.md:
    /// the entry log is emitted after the guard chain, not before it. The main-thread check has to
    /// precede everything, including the log itself, because <c>Debug.Log</c> must not be called
    /// off the Unity main thread.
    /// </para>
    /// </summary>
    public class MacClipboardManager : MonoBehaviour
    {
        private const string LogTag = "MacClipboardManager";

        private const string ResponseParseFailedMessage = "The native result could not be parsed.";

        // ── Singleton and static state ──────────────────────────────────────────

        private static MacClipboardManager? _instance;

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
        /// <para>
        /// Fetching a fresh instance after the Manager was destroyed does not revive it. The new
        /// instance has no subscribers and every operation on it is still rejected with
        /// <see cref="MacClipboardErrorCodes.ManagerDestroyed"/>.
        /// </para>
        /// </summary>
        public static MacClipboardManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    Debug.Log($"[{LogTag}] Creating new instance of MacClipboardManager");
                    var go = new GameObject("MacClipboardManager");
                    _instance = go.AddComponent<MacClipboardManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        // ── Events ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Raised when copy or append completes, on success and failure alike. Inspect
        /// <see cref="MacClipboardOwnershipResult.Operation"/> to tell them apart.
        /// Always invoked before the per-call callback.
        /// <para>
        /// A rejected call raises this event on the instance the call was made on. A caller holding
        /// a reference taken before destruction therefore still receives the rejection; one that
        /// re-fetches <see cref="Instance"/> after destruction does not, because that is a
        /// different instance with no subscribers. The per-call callback fires in both cases.
        /// </para>
        /// </summary>
        public event Action<MacClipboardOwnershipResult>? OwnershipChanged;

        /// <summary>
        /// Raised when a read completes, on success and failure alike.
        /// Always invoked before the per-call callback. See <see cref="OwnershipChanged"/> for how
        /// this behaves after the Manager has been destroyed.
        /// </summary>
        public event Action<MacClipboardReadResult>? ReadCompleted;

        /// <summary>
        /// Raised when a typed data read completes, on success and failure alike.
        /// Always invoked before the per-call callback. See <see cref="OwnershipChanged"/> for how
        /// this behaves after the Manager has been destroyed.
        /// </summary>
        public event Action<MacClipboardReadDataResult>? ReadDataCompleted;

        /// <summary>
        /// Raised when a clear completes, on success and failure alike.
        /// Always invoked before the per-call callback. See <see cref="OwnershipChanged"/> for how
        /// this behaves after the Manager has been destroyed.
        /// </summary>
        public event Action<MacClipboardChangeCountResult>? ClearCompleted;

        /// <summary>
        /// Raised when an operation that returns no value completes, on success and failure alike.
        /// Inspect <see cref="MacClipboardOperationResult.Operation"/> to tell them apart.
        /// Always invoked before the per-call callback. See <see cref="OwnershipChanged"/> for how
        /// this behaves after the Manager has been destroyed.
        /// </summary>
        public event Action<MacClipboardOperationResult>? ClipboardOperationCompleted;

        /// <summary>
        /// Raised when a snapshot completes, on success and failure alike.
        /// Always invoked before the per-call callback.
        /// </summary>
        public event Action<MacClipboardSnapshotResult>? SnapshotCompleted;

        /// <summary>
        /// Raised when a pasteboard creation completes, on success and failure alike.
        /// Always invoked before the per-call callback.
        /// </summary>
        public event Action<MacPasteboardScopeResult>? PasteboardCreated;

        /// <summary>
        /// Raised when a pattern detection completes, on success and failure alike.
        /// Always invoked before the per-call callback.
        /// </summary>
        public event Action<MacClipboardDetectedPatternsResult>? PatternsDetected;

        /// <summary>
        /// Raised when a value detection completes, on success and failure alike.
        /// Always invoked before the per-call callback.
        /// </summary>
        public event Action<MacClipboardDetectedValuesResult>? ValuesDetected;

        /// <summary>
        /// Raised when a metadata detection completes, on success and failure alike.
        /// Always invoked before the per-call callback.
        /// </summary>
        public event Action<MacClipboardDetectedMetadataResult>? MetadataDetected;

        /// <summary>
        /// Raised when an access-behaviour query completes, on success and failure alike.
        /// Always invoked before the per-call callback.
        /// </summary>
        public event Action<MacClipboardAccessBehaviorResult>? AccessBehaviorChecked;

        /// <summary>
        /// Raised when a foreground-change check completes, on success and failure alike.
        /// Always invoked before the per-call callback.
        /// </summary>
        public event Action<MacClipboardForegroundChangeResult>? ForegroundChangeChecked;

        /// <summary>
        /// Raised for every clipboard change reported by an active observation.
        /// Always invoked before the per-call <c>onChanged</c> registration.
        /// <para>
        /// Only a successful <see cref="StartObserving"/> makes this fire, and only that call's
        /// registration receives events. A restart that is still in flight does not divert them.
        /// </para>
        /// </summary>
        public event Action<MacClipboardChangeEvent>? ClipboardChanged;

        // ── Native interop ──────────────────────────────────────────────────────

        // The C header declares isSuccess as Objective-C BOOL (1 byte: _Bool on arm64, signed char
        // on x86_64). C# marshals a bare bool as a 4-byte Win32 BOOL by default, so the width is
        // pinned explicitly. The delegate type itself is declared outside the narrow guard so the
        // Editor can compile the class; only the persistent instances and the [MonoPInvokeCallback]
        // bodies are player-only.
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void ClipboardJsonCallback(
            [MarshalAs(UnmanagedType.I1)] bool isSuccess,
            string? json,
            long errorCode,
            string? errorMessage);

        // Same contract minus the payload, for the operations that return no value.
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void ClipboardCallback(
            [MarshalAs(UnmanagedType.I1)] bool isSuccess,
            long errorCode,
            string? errorMessage);

        // Change notifications carry only a payload: there is no success flag, because the native
        // layer drops an event it cannot encode rather than reporting a failure.
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void ClipboardChangeCallback(string? eventJson);

#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void clipboardCopy(
            string contentJson, string? optionsJson, string scopeJson, ClipboardJsonCallback callback);

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void clipboardAppend(
            string contentJson, string ownershipJson, ClipboardJsonCallback callback);

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void clipboardRead(string scopeJson, ClipboardJsonCallback callback);

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void clipboardReadData(
            string utType, string scopeJson, ClipboardJsonCallback callback);

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void clipboardClear(string scopeJson, ClipboardJsonCallback callback);

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void clipboardSnapshot(
            string? matchingTypesJson, string scopeJson, ClipboardJsonCallback callback);

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void clipboardCreatePasteboard(
            string requestJson, ClipboardJsonCallback callback);

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void clipboardRemovePasteboard(string scopeJson, ClipboardCallback callback);

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void clipboardDetectPatterns(
            string patternsJson, string scopeJson, ClipboardJsonCallback callback);

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void clipboardDetectValues(
            string patternsJson, string scopeJson, ClipboardJsonCallback callback);

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void clipboardDetectMetadata(string scopeJson, ClipboardJsonCallback callback);

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void clipboardAccessBehavior(string scopeJson, ClipboardJsonCallback callback);

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void clipboardCheckForegroundChange(
            string scopeJson, ClipboardJsonCallback callback);

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void clipboardStartObserving(
            string scopeJson, double intervalSeconds, ClipboardCallback callback,
            ClipboardChangeCallback onChange);

        // Nullable: teardown issues this with no completion callback of its own.
        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void clipboardStopObserving(ClipboardCallback? callback);

        // Held in static readonly fields so the GC cannot collect the delegates while the native
        // side still holds their function pointers.
        private static readonly ClipboardJsonCallback s_copyDelegate = OnCopyResult;
        private static readonly ClipboardJsonCallback s_appendDelegate = OnAppendResult;
        private static readonly ClipboardJsonCallback s_readDelegate = OnReadResult;
        private static readonly ClipboardJsonCallback s_readDataDelegate = OnReadDataResult;
        private static readonly ClipboardJsonCallback s_clearDelegate = OnClearResult;
        private static readonly ClipboardJsonCallback s_snapshotDelegate = OnSnapshotResult;
        private static readonly ClipboardJsonCallback s_createPasteboardDelegate = OnCreatePasteboardResult;
        private static readonly ClipboardCallback s_removePasteboardDelegate = OnRemovePasteboardResult;
        private static readonly ClipboardJsonCallback s_detectPatternsDelegate = OnDetectPatternsResult;
        private static readonly ClipboardJsonCallback s_detectValuesDelegate = OnDetectValuesResult;
        private static readonly ClipboardJsonCallback s_detectMetadataDelegate = OnDetectMetadataResult;
        private static readonly ClipboardJsonCallback s_accessBehaviorDelegate = OnAccessBehaviorResult;
        private static readonly ClipboardJsonCallback s_checkForegroundChangeDelegate =
            OnCheckForegroundChangeResult;
        private static readonly ClipboardCallback s_startObservingDelegate = OnStartObservingResult;
        private static readonly ClipboardCallback s_stopObservingDelegate = OnStopObservingResult;
        private static readonly ClipboardChangeCallback s_changeDelegate = OnClipboardChanged;
#endif

        // ── Per-call callback slots ─────────────────────────────────────────────
        // One slot per operation. Single-flight caps the pending calls per operation at one, so a
        // single slot is enough. Touched only from the Unity main thread.

        private static Action<MacClipboardOwnershipResult>? s_onCopy;
        private static Action<MacClipboardOwnershipResult>? s_onAppend;
        private static Action<MacClipboardReadResult>? s_onRead;
        private static Action<MacClipboardReadDataResult>? s_onReadData;
        private static Action<MacClipboardChangeCountResult>? s_onClear;
        private static Action<MacClipboardSnapshotResult>? s_onSnapshot;
        private static Action<MacPasteboardScopeResult>? s_onCreatePasteboard;
        private static Action<MacClipboardOperationResult>? s_onRemovePasteboard;
        private static Action<MacClipboardDetectedPatternsResult>? s_onDetectPatterns;
        private static Action<MacClipboardDetectedValuesResult>? s_onDetectValues;
        private static Action<MacClipboardDetectedMetadataResult>? s_onDetectMetadata;
        private static Action<MacClipboardAccessBehaviorResult>? s_onAccessBehavior;
        private static Action<MacClipboardForegroundChangeResult>? s_onCheckForegroundChange;
        private static Action<MacClipboardOperationResult>? s_onStartObserving;
        private static Action<MacClipboardOperationResult>? s_onStopObserving;

        // Change registrations, kept in two slots rather than one (5.6.5). The native layer leaves
        // an existing observation running when a restart fails, so the previous registration must
        // stay live until a restart actually succeeds. Only the active slot receives events.
        private static Action<MacClipboardChangeEvent>? s_onChanged;
        private static Action<MacClipboardChangeEvent>? s_pendingOnChanged;

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
                Debug.LogError(
                    $"[{LogTag}][{nameof(Awake)}] Recreated after destruction; all operations are rejected.");
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
                managedCleanup: () =>
                {
                    try { ClearAllPendingCallbacks(); }
                    finally { _instance = null; }
                });

            // s_dispatcher is deliberately left set: post-destruction rejections still need it.
        }

        /// <summary>
        /// Stops native observation during teardown, bypassing the guard chain.
        /// <para>
        /// Bypassing is safe for managed state: the tombstone is already set, so no new operation
        /// can start and every arriving callback is discarded. It is <b>not</b> safe for native
        /// state on its own, because the native start and stop tasks carry no ordering guarantee: a
        /// start submitted just before teardown can run after this stop and leave the poller
        /// running. That gap is closed by reissuing this stop when a late successful start arrives
        /// (see <see cref="HandleObservationControlCallback"/>).
        /// </para>
        /// </summary>
        private static void StopObservingForTeardown()
        {
#if UNITY_EDITOR
            // The P/Invoke below compiles to nothing in the Editor, so the call itself is what
            // tests observe. Counts every issuance, teardown's own included (see the seam docs).
            TeardownStopIssueCountForTests++;
#endif
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
            if (Application.platform == RuntimePlatform.OSXPlayer)
            {
                clipboardStopObserving(null);
            }
#endif
        }

        /// <summary>
        /// Runs the two teardown steps with the exception boundary the destroy contract needs:
        /// managedCleanup always runs, even when stop throws.
        /// <para>
        /// Pure with respect to Manager state, so tests can pass throwing actions directly instead
        /// of the Manager exposing a mutable, swappable hook that would also ship in player builds.
        /// </para>
        /// </summary>
        /// <param name="stop">Stops native change observation.</param>
        /// <param name="managedCleanup">Clears managed state. Always invoked.</param>
        internal static void RunDestroyCleanup(Action stop, Action managedCleanup)
        {
            try
            {
                stop();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{LogTag}][{nameof(RunDestroyCleanup)}] stop: {ex.Message}");
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
        /// rejected before reaching P/Invoke, unless a test opts in through
        /// <see cref="BridgeAvailableOverrideForTests"/>, and even then the P/Invoke compiles to
        /// nothing), so no callback can be outstanding. Compiled out of player builds precisely
        /// because that reasoning does not hold on device.
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
            MaxRequestBytesOverrideForTests = null;
            TeardownStopIssueCountForTests = 0;
#endif
            ClearAllPendingCallbacks();
        }

        private static void ClearAllPendingCallbacks()
        {
            s_onCopy = null;
            s_onAppend = null;
            s_onRead = null;
            s_onReadData = null;
            s_onClear = null;
            s_onSnapshot = null;
            s_onCreatePasteboard = null;
            s_onRemovePasteboard = null;
            s_onDetectPatterns = null;
            s_onDetectValues = null;
            s_onDetectMetadata = null;
            s_onAccessBehavior = null;
            s_onCheckForegroundChange = null;
            s_onStartObserving = null;
            s_onStopObserving = null;
            s_onChanged = null;
            s_pendingOnChanged = null;

            s_inFlight.Clear();
        }

        // ── Single-flight ───────────────────────────────────────────────────────

        /// <summary>
        /// Marks an operation as in flight.
        /// </summary>
        /// <param name="inFlight">Set tracking pending operations.</param>
        /// <param name="inFlightKey">Single-flight key.</param>
        /// <returns><c>false</c> when a call for the same key is already pending.</returns>
        internal static bool TryBeginOperation(HashSet<string> inFlight, string inFlightKey) =>
            inFlight.Add(inFlightKey);

        /// <summary>
        /// Releases an operation's in-flight marker. Safe to call when it is not marked.
        /// </summary>
        /// <param name="inFlight">Set tracking pending operations.</param>
        /// <param name="inFlightKey">Single-flight key.</param>
        internal static void EndOperation(HashSet<string> inFlight, string inFlightKey) =>
            inFlight.Remove(inFlightKey);

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

        /// <summary>
        /// Test-only override of the request size limit, so the 
        /// <see cref="MacClipboardErrorCodes.RequestTooLarge"/> path can be driven with a small
        /// payload. <see cref="MacClipboardLimits.MaxRequestBytes"/> is a const and cannot be
        /// swapped, and allocating the real 32 MiB would make the test needlessly heavy.
        /// </summary>
        internal static long? MaxRequestBytesOverrideForTests { get; set; }
#endif

        private static bool IsBridgeAvailable()
        {
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
            return Application.platform == RuntimePlatform.OSXPlayer;
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
            $"{operation} is unavailable: MacClipboardManager has been destroyed.";

        private static string UnavailableMessage(string operation) =>
            $"{operation} is only available on a macOS Standalone player.";

        private static string BusyMessage(string operation) =>
            $"{operation} is already in progress.";

        private static string CouldNotStartMessage(string operation) =>
            $"{operation} could not be started.";

        // The observation operations share one single-flight key, so the generic
        // "startObserving is already in progress." would be a lie when a stop is the one running.
        private static string ObservationBusyMessage() =>
            "Another observation control call is already in progress.";

        private static string RequestTooLargeMessage(long limitBytes) =>
            $"Clipboard content exceeds the {limitBytes} byte request limit.";

        /// <summary>
        /// Runs stages 1 to 4 of the guard chain: main thread, tombstone, arguments, platform.
        /// <para>
        /// It deliberately stops short of the in-flight marker so the caller can build its request
        /// JSON in between. Building before taking the marker means a failure there cannot strand
        /// the marker and make the operation permanently busy for the rest of the process.
        /// </para>
        /// </summary>
        /// <typeparam name="TResult">Result type of the operation.</typeparam>
        /// <param name="operation">Operation name, used in messages and logs.</param>
        /// <param name="onResult">Per-call callback of the rejected call.</param>
        /// <param name="commonSelector">
        /// Reads the common event off the instance the call was made on. Evaluated lazily so the
        /// off-thread path can read it from the main thread.
        /// </param>
        /// <param name="failure">Builds a failure result from a code and a message.</param>
        /// <param name="validate">Optional argument validation, run as stage 3.</param>
        /// <returns><c>false</c> when the call was rejected and already dispatched.</returns>
        private static bool TryPassGuards<TResult>(
            string operation,
            Action<TResult>? onResult,
            Func<Action<TResult>?> commonSelector,
            Func<int, string, TResult> failure,
            Func<(int Code, string Message)?>? validate = null)
        {
            // 1. Main thread. Must precede everything else, including the entry log.
            if (!IsMainThread())
            {
                DispatchOffThreadRejection(
                    failure(MacClipboardErrorCodes.MainThreadRequired, MainThreadMessage(operation)),
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
                    failure(MacClipboardErrorCodes.ManagerDestroyed, DestroyedMessage(operation)),
                    commonSelector(),
                    onResult);
                return false;
            }

            // 3. Arguments.
            if (validate != null)
            {
                (int Code, string Message)? invalid = validate();
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
                Debug.LogWarning($"[{LogTag}] {operation} rejected: not running on a macOS Standalone player.");
                DispatchRejectedResult(
                    failure(MacClipboardErrorCodes.BridgeUnavailable, UnavailableMessage(operation)),
                    commonSelector(),
                    onResult);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Effective request size limit. The Editor reads a test override first, because
        /// <see cref="MacClipboardLimits.MaxRequestBytes"/> is a const and cannot be swapped.
        /// </summary>
        private static long EffectiveMaxRequestBytes()
        {
#if UNITY_EDITOR
            return MaxRequestBytesOverrideForTests ?? MacClipboardLimits.MaxRequestBytes;
#else
            return MacClipboardLimits.MaxRequestBytes;
#endif
        }

        /// <summary>
        /// Returns a RequestTooLarge rejection when the total representation size exceeds the
        /// effective limit, or <c>null</c> when it fits.
        /// <para>
        /// The total is accumulated as <c>long</c> on purpose: an <c>int</c> sum would overflow on
        /// exactly the input this check exists to reject, and the exception would escape through
        /// the public API instead of being returned as a result.
        /// </para>
        /// <para>
        /// Null items and null representation values count as zero rather than being rejected.
        /// Throwing here would break the "rejections come back as results" contract, and the
        /// base64 step later on already turns them into a clean BridgeUnavailable failure.
        /// </para>
        /// </summary>
        private static (int Code, string Message)? ValidateRequestSize(MacClipboardContent content)
        {
            long limit = EffectiveMaxRequestBytes();
            long total = 0;

            foreach (MacClipboardContentItem item in content.Items)
            {
                if (item == null) continue;

                foreach (KeyValuePair<string, byte[]> representation in item.Representations)
                {
                    total += representation.Value?.Length ?? 0;
                    if (total > limit)
                    {
                        return (MacClipboardErrorCodes.RequestTooLarge, RequestTooLargeMessage(limit));
                    }
                }
            }

            return null;
        }

        // ── Result dispatch (normal path: this call owns slot and marker) ───────

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

        /// <summary>
        /// Takes the per-call callback belonging to a shared-result-type operation.
        /// <para>
        /// Copy and append return the same result type but own different slots, so the slot has to
        /// be selected by operation. Hard-coding the copy slot would leave append's callback
        /// uninvoked and its slot leaked.
        /// </para>
        /// </summary>
        private static Action<MacClipboardOwnershipResult>? TakeOwnershipCallback(string operation)
        {
            switch (operation)
            {
                case MacClipboardOperations.Copy: { var cb = s_onCopy; s_onCopy = null; return cb; }
                case MacClipboardOperations.Append: { var cb = s_onAppend; s_onAppend = null; return cb; }
                default: return null;
            }
        }

        // Each Fire* helper follows the same contract: take and clear the slot, release the
        // in-flight marker, then dispatch. Releasing before dispatch lets a subscriber start the
        // same operation again from inside its own callback.

        private static void FireOwnershipResult(MacClipboardOwnershipResult result, string inFlightKey)
        {
            Action<MacClipboardOwnershipResult>? perCall = TakeOwnershipCallback(result.Operation);
            EndOperation(s_inFlight, inFlightKey);
            Dispatch(result, _instance?.OwnershipChanged, perCall);
        }

        private static void FireReadResult(MacClipboardReadResult result)
        {
            Action<MacClipboardReadResult>? perCall = s_onRead;
            s_onRead = null;
            EndOperation(s_inFlight, MacClipboardOperations.Read);
            Dispatch(result, _instance?.ReadCompleted, perCall);
        }

        private static void FireReadDataResult(MacClipboardReadDataResult result)
        {
            Action<MacClipboardReadDataResult>? perCall = s_onReadData;
            s_onReadData = null;
            EndOperation(s_inFlight, MacClipboardOperations.ReadData);
            Dispatch(result, _instance?.ReadDataCompleted, perCall);
        }

        private static void FireClearResult(MacClipboardChangeCountResult result)
        {
            Action<MacClipboardChangeCountResult>? perCall = s_onClear;
            s_onClear = null;
            EndOperation(s_inFlight, MacClipboardOperations.Clear);
            Dispatch(result, _instance?.ClearCompleted, perCall);
        }

        /// <summary>
        /// Takes the per-call callback for an operation that returns no value. Selected by
        /// operation name for the same reason as <see cref="TakeOwnershipCallback"/>: several
        /// operations share this result type but own separate slots.
        /// </summary>
        private static Action<MacClipboardOperationResult>? TakeOperationCallback(string operation)
        {
            switch (operation)
            {
                case MacClipboardOperations.RemovePasteboard:
                    { var cb = s_onRemovePasteboard; s_onRemovePasteboard = null; return cb; }
                case MacClipboardOperations.StartObserving:
                    { var cb = s_onStartObserving; s_onStartObserving = null; return cb; }
                case MacClipboardOperations.StopObserving:
                    { var cb = s_onStopObserving; s_onStopObserving = null; return cb; }
                default: return null;
            }
        }

        private static void FireOperationResult(MacClipboardOperationResult result, string inFlightKey)
        {
            Action<MacClipboardOperationResult>? perCall = TakeOperationCallback(result.Operation);
            EndOperation(s_inFlight, inFlightKey);
            Dispatch(result, _instance?.ClipboardOperationCompleted, perCall);
        }

        private static void FireSnapshotResult(MacClipboardSnapshotResult result)
        {
            Action<MacClipboardSnapshotResult>? perCall = s_onSnapshot;
            s_onSnapshot = null;
            EndOperation(s_inFlight, MacClipboardOperations.Snapshot);
            Dispatch(result, _instance?.SnapshotCompleted, perCall);
        }

        private static void FireCreatePasteboardResult(MacPasteboardScopeResult result)
        {
            Action<MacPasteboardScopeResult>? perCall = s_onCreatePasteboard;
            s_onCreatePasteboard = null;
            EndOperation(s_inFlight, MacClipboardOperations.CreatePasteboard);
            Dispatch(result, _instance?.PasteboardCreated, perCall);
        }

        private static void FireDetectPatternsResult(MacClipboardDetectedPatternsResult result)
        {
            Action<MacClipboardDetectedPatternsResult>? perCall = s_onDetectPatterns;
            s_onDetectPatterns = null;
            EndOperation(s_inFlight, MacClipboardOperations.DetectPatterns);
            Dispatch(result, _instance?.PatternsDetected, perCall);
        }

        private static void FireDetectValuesResult(MacClipboardDetectedValuesResult result)
        {
            Action<MacClipboardDetectedValuesResult>? perCall = s_onDetectValues;
            s_onDetectValues = null;
            EndOperation(s_inFlight, MacClipboardOperations.DetectValues);
            Dispatch(result, _instance?.ValuesDetected, perCall);
        }

        private static void FireDetectMetadataResult(MacClipboardDetectedMetadataResult result)
        {
            Action<MacClipboardDetectedMetadataResult>? perCall = s_onDetectMetadata;
            s_onDetectMetadata = null;
            EndOperation(s_inFlight, MacClipboardOperations.DetectMetadata);
            Dispatch(result, _instance?.MetadataDetected, perCall);
        }

        private static void FireAccessBehaviorResult(MacClipboardAccessBehaviorResult result)
        {
            Action<MacClipboardAccessBehaviorResult>? perCall = s_onAccessBehavior;
            s_onAccessBehavior = null;
            EndOperation(s_inFlight, MacClipboardOperations.AccessBehavior);
            Dispatch(result, _instance?.AccessBehaviorChecked, perCall);
        }

        private static void FireForegroundChangeResult(MacClipboardForegroundChangeResult result)
        {
            Action<MacClipboardForegroundChangeResult>? perCall = s_onCheckForegroundChange;
            s_onCheckForegroundChange = null;
            EndOperation(s_inFlight, MacClipboardOperations.CheckForegroundChange);
            Dispatch(result, _instance?.ForegroundChangeChecked, perCall);
        }

        /// <summary>
        /// Delivers a change event. Unlike the operation results this owns no in-flight marker and
        /// clears no slot: a registration serves every event until observation is replaced or
        /// stopped. Only the active registration is used, never the pending one.
        /// </summary>
        private static void FireClipboardChanged(MacClipboardChangeEvent changeEvent) =>
            Dispatch(changeEvent, _instance?.ClipboardChanged, s_onChanged);

        // ── Public API ──────────────────────────────────────────────────────────

        /// <summary>
        /// Writes content to a pasteboard, replacing everything already on it.
        /// Unity main thread only.
        /// <para>
        /// The returned ownership is what a following <see cref="Append"/> needs. Hold on to it if
        /// more items are to be added to the same pasteboard contents.
        /// </para>
        /// <para>
        /// <b>A single item larger than 10 MiB is written lazily.</b> Only its type goes on the
        /// pasteboard, and the bytes are supplied when a reader asks for them, which requires this
        /// process to still be alive. A successful copy of such an item therefore does not
        /// guarantee it can still be pasted after the player quits. Splitting the content across
        /// several items avoids the lazy path entirely, whatever the total size.
        /// </para>
        /// </summary>
        /// <param name="content">Content to write. Must not be null.</param>
        /// <param name="scope">Target pasteboard, or <c>null</c> for the general pasteboard.</param>
        /// <param name="options">
        /// Privacy options, or <c>null</c> for the privacy-preserving default. The native layer
        /// states that the effect of <c>LocalOnly</c> on Universal Clipboard is unverified on real
        /// hardware, so do not rely on it to keep content off another device.
        /// </param>
        /// <param name="onResult">Optional per-call callback. <see cref="OwnershipChanged"/> always fires first.</param>
        public void Copy(
            MacClipboardContent content,
            MacPasteboardScope? scope = null,
            MacClipboardCopyOptions? options = null,
            Action<MacClipboardOwnershipResult>? onResult = null)
        {
            const string op = MacClipboardOperations.Copy;

            // Lifted into locals because stages 5 and 6 dispatch their own rejections and need the
            // same two pieces; written inline they would not survive past TryPassGuards.
            Func<Action<MacClipboardOwnershipResult>?> commonSelector = () => this.OwnershipChanged;
            Func<int, string, MacClipboardOwnershipResult> failure =
                (code, message) => MacClipboardOwnershipResult.Failure(op, code, message);

            if (!TryPassGuards(
                    op,
                    onResult,
                    commonSelector,
                    failure,
                    validate: () => content == null
                        ? (MacClipboardErrorCodes.InvalidRequest, "content must not be null.")
                        : ValidateRequestSize(content)))
            {
                return;
            }

            Debug.Log($"[{LogTag}][{nameof(Copy)}] itemCount: {content.Items.Count}, " +
                      $"hasScope: {scope != null}, hasOptions: {options != null}, " +
                      $"hasCallback: {onResult != null}");

            // Stage 5. Failing here has not taken the in-flight marker yet, so it is a rejection.
            string contentJson;
            string scopeJson;
            string? optionsJson;
            try
            {
                contentJson = MacClipboardJsonBuilder.BuildContentJson(content);
                scopeJson = MacClipboardJsonBuilder.BuildScopeJson(scope ?? MacPasteboardScope.General);
                optionsJson = MacClipboardJsonBuilder.BuildOptionsJson(options);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{LogTag}][{nameof(Copy)}] build: {ex.Message}");
                DispatchRejectedResult(
                    failure(MacClipboardErrorCodes.BridgeUnavailable, CouldNotStartMessage(op)),
                    commonSelector(), onResult);
                return;
            }

            // Stage 6. From here on this call owns the marker.
            if (!TryBeginOperation(s_inFlight, op))
            {
                Debug.LogWarning($"[{LogTag}] {op} rejected: already in progress.");
                DispatchRejectedResult(
                    failure(MacClipboardErrorCodes.Busy, BusyMessage(op)),
                    commonSelector(), onResult);
                return;
            }

            // Stage 7.
            s_onCopy = onResult;
            InvokeNative(
                op,
                op,
                nativeCall: () =>
                {
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
                    clipboardCopy(contentJson, optionsJson, scopeJson, s_copyDelegate);
#endif
                },
                // Copy does not return MacClipboardOperationResult, so the default fallback would
                // send the wrong result type to the wrong event and leak s_onCopy.
                onNativeFailureResult: message => FireOwnershipResult(
                    MacClipboardOwnershipResult.Failure(op, MacClipboardErrorCodes.BridgeUnavailable, message),
                    op));
        }

        /// <summary>
        /// Adds content to a pasteboard this app already owns, keeping what is already on it.
        /// Unity main thread only.
        /// <para>
        /// A successful append leaves the change count untouched, so the same
        /// <paramref name="ownership"/> can be reused for the next append. Once another app takes
        /// the pasteboard the call fails with
        /// <see cref="MacClipboardErrorCodes.OwnershipLost"/>.
        /// </para>
        /// </summary>
        /// <param name="content">Content to append. Must not be null.</param>
        /// <param name="ownership">Ownership returned by <see cref="Copy"/>. Must not be null.</param>
        /// <param name="onResult">Optional per-call callback. <see cref="OwnershipChanged"/> always fires first.</param>
        public void Append(
            MacClipboardContent content,
            MacPasteboardOwnership ownership,
            Action<MacClipboardOwnershipResult>? onResult = null)
        {
            const string op = MacClipboardOperations.Append;

            Func<Action<MacClipboardOwnershipResult>?> commonSelector = () => this.OwnershipChanged;
            Func<int, string, MacClipboardOwnershipResult> failure =
                (code, message) => MacClipboardOwnershipResult.Failure(op, code, message);

            if (!TryPassGuards(
                    op,
                    onResult,
                    commonSelector,
                    failure,
                    validate: () =>
                    {
                        if (content == null)
                        {
                            return (MacClipboardErrorCodes.InvalidRequest, "content must not be null.");
                        }
                        if (ownership == null)
                        {
                            return (MacClipboardErrorCodes.InvalidRequest, "ownership must not be null.");
                        }
                        return ValidateRequestSize(content);
                    }))
            {
                return;
            }

            Debug.Log($"[{LogTag}][{nameof(Append)}] itemCount: {content.Items.Count}, " +
                      $"scopeKind: {ownership.Scope.Kind}, hasCallback: {onResult != null}");

            string contentJson;
            string ownershipJson;
            try
            {
                contentJson = MacClipboardJsonBuilder.BuildContentJson(content);
                ownershipJson = MacClipboardJsonBuilder.BuildOwnershipJson(ownership);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{LogTag}][{nameof(Append)}] build: {ex.Message}");
                DispatchRejectedResult(
                    failure(MacClipboardErrorCodes.BridgeUnavailable, CouldNotStartMessage(op)),
                    commonSelector(), onResult);
                return;
            }

            if (!TryBeginOperation(s_inFlight, op))
            {
                Debug.LogWarning($"[{LogTag}] {op} rejected: already in progress.");
                DispatchRejectedResult(
                    failure(MacClipboardErrorCodes.Busy, BusyMessage(op)),
                    commonSelector(), onResult);
                return;
            }

            s_onAppend = onResult;
            InvokeNative(
                op,
                op,
                nativeCall: () =>
                {
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
                    clipboardAppend(contentJson, ownershipJson, s_appendDelegate);
#endif
                },
                onNativeFailureResult: message => FireOwnershipResult(
                    MacClipboardOwnershipResult.Failure(op, MacClipboardErrorCodes.BridgeUnavailable, message),
                    op));
        }

        /// <summary>
        /// Reads every item on a pasteboard, with all their representations.
        /// Unity main thread only.
        /// <para>
        /// The payload is materialised twice on the way in, once as base64 and once decoded, so a
        /// large pasteboard costs several times its own size in memory. That cost cannot be capped
        /// from C#. When the pasteboard may be large, call the snapshot API first and read only the
        /// types that are actually needed.
        /// </para>
        /// <para>
        /// <b>Reading is not the mirror of writing.</b> The pasteboard derives types, so content
        /// written as one type may come back under several. Do not assume the result matches what
        /// <see cref="Copy"/> was given.
        /// </para>
        /// <para>
        /// No read is guaranteed to be invisible to the user. Reading may surface a system
        /// notification, and neither this API nor the native layer can promise otherwise.
        /// </para>
        /// </summary>
        /// <param name="scope">Target pasteboard, or <c>null</c> for the general pasteboard.</param>
        /// <param name="onResult">Optional per-call callback. <see cref="ReadCompleted"/> always fires first.</param>
        public void Read(MacPasteboardScope? scope = null, Action<MacClipboardReadResult>? onResult = null)
        {
            const string op = MacClipboardOperations.Read;

            Func<Action<MacClipboardReadResult>?> commonSelector = () => this.ReadCompleted;
            Func<int, string, MacClipboardReadResult> failure =
                (code, message) => MacClipboardReadResult.Failure(code, message);

            if (!TryPassGuards(op, onResult, commonSelector, failure))
            {
                return;
            }

            Debug.Log($"[{LogTag}][{nameof(Read)}] hasScope: {scope != null}, hasCallback: {onResult != null}");

            string scopeJson;
            try
            {
                scopeJson = MacClipboardJsonBuilder.BuildScopeJson(scope ?? MacPasteboardScope.General);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{LogTag}][{nameof(Read)}] build: {ex.Message}");
                DispatchRejectedResult(
                    failure(MacClipboardErrorCodes.BridgeUnavailable, CouldNotStartMessage(op)),
                    commonSelector(), onResult);
                return;
            }

            if (!TryBeginOperation(s_inFlight, op))
            {
                Debug.LogWarning($"[{LogTag}] {op} rejected: already in progress.");
                DispatchRejectedResult(
                    failure(MacClipboardErrorCodes.Busy, BusyMessage(op)),
                    commonSelector(), onResult);
                return;
            }

            s_onRead = onResult;
            InvokeNative(
                op,
                op,
                nativeCall: () =>
                {
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
                    clipboardRead(scopeJson, s_readDelegate);
#endif
                },
                onNativeFailureResult: message => FireReadResult(
                    MacClipboardReadResult.Failure(MacClipboardErrorCodes.BridgeUnavailable, message)));
        }

        /// <summary>
        /// Reads the raw bytes stored for one uniform type identifier.
        /// Unity main thread only.
        /// <para>
        /// A type that is not on the pasteboard is a success with <c>Data == null</c>, not a
        /// failure. So is a type identifier that does not exist at all: the native layer does not
        /// validate it, and neither can this side.
        /// </para>
        /// <para>
        /// As with <see cref="Read"/>, no read is guaranteed to be invisible to the user.
        /// </para>
        /// </summary>
        /// <param name="utType">Uniform type identifier to read.</param>
        /// <param name="scope">Target pasteboard, or <c>null</c> for the general pasteboard.</param>
        /// <param name="onResult">Optional per-call callback. <see cref="ReadDataCompleted"/> always fires first.</param>
        public void ReadData(
            string utType,
            MacPasteboardScope? scope = null,
            Action<MacClipboardReadDataResult>? onResult = null)
        {
            const string op = MacClipboardOperations.ReadData;

            Func<Action<MacClipboardReadDataResult>?> commonSelector = () => this.ReadDataCompleted;
            Func<int, string, MacClipboardReadDataResult> failure =
                (code, message) => MacClipboardReadDataResult.Failure(code, message);

            // utType is deliberately not validated here: a null or empty one is rejected natively
            // with ContractViolation, and duplicating that check would give one condition two
            // different error codes.
            if (!TryPassGuards(op, onResult, commonSelector, failure))
            {
                return;
            }

            Debug.Log($"[{LogTag}][{nameof(ReadData)}] hasUtType: {utType != null}, " +
                      $"hasScope: {scope != null}, hasCallback: {onResult != null}");

            string scopeJson;
            try
            {
                scopeJson = MacClipboardJsonBuilder.BuildScopeJson(scope ?? MacPasteboardScope.General);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{LogTag}][{nameof(ReadData)}] build: {ex.Message}");
                DispatchRejectedResult(
                    failure(MacClipboardErrorCodes.BridgeUnavailable, CouldNotStartMessage(op)),
                    commonSelector(), onResult);
                return;
            }

            if (!TryBeginOperation(s_inFlight, op))
            {
                Debug.LogWarning($"[{LogTag}] {op} rejected: already in progress.");
                DispatchRejectedResult(
                    failure(MacClipboardErrorCodes.Busy, BusyMessage(op)),
                    commonSelector(), onResult);
                return;
            }

            s_onReadData = onResult;
            InvokeNative(
                op,
                op,
                nativeCall: () =>
                {
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
                    clipboardReadData(utType, scopeJson, s_readDataDelegate);
#endif
                },
                onNativeFailureResult: message => FireReadDataResult(
                    MacClipboardReadDataResult.Failure(MacClipboardErrorCodes.BridgeUnavailable, message)));
        }

        /// <summary>
        /// Removes everything from a pasteboard and reports its new change count.
        /// Unity main thread only.
        /// </summary>
        /// <param name="scope">Target pasteboard, or <c>null</c> for the general pasteboard.</param>
        /// <param name="onResult">Optional per-call callback. <see cref="ClearCompleted"/> always fires first.</param>
        public void Clear(
            MacPasteboardScope? scope = null,
            Action<MacClipboardChangeCountResult>? onResult = null)
        {
            const string op = MacClipboardOperations.Clear;

            Func<Action<MacClipboardChangeCountResult>?> commonSelector = () => this.ClearCompleted;
            Func<int, string, MacClipboardChangeCountResult> failure =
                (code, message) => MacClipboardChangeCountResult.Failure(code, message);

            if (!TryPassGuards(op, onResult, commonSelector, failure))
            {
                return;
            }

            Debug.Log($"[{LogTag}][{nameof(Clear)}] hasScope: {scope != null}, hasCallback: {onResult != null}");

            string scopeJson;
            try
            {
                scopeJson = MacClipboardJsonBuilder.BuildScopeJson(scope ?? MacPasteboardScope.General);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{LogTag}][{nameof(Clear)}] build: {ex.Message}");
                DispatchRejectedResult(
                    failure(MacClipboardErrorCodes.BridgeUnavailable, CouldNotStartMessage(op)),
                    commonSelector(), onResult);
                return;
            }

            if (!TryBeginOperation(s_inFlight, op))
            {
                Debug.LogWarning($"[{LogTag}] {op} rejected: already in progress.");
                DispatchRejectedResult(
                    failure(MacClipboardErrorCodes.Busy, BusyMessage(op)),
                    commonSelector(), onResult);
                return;
            }

            s_onClear = onResult;
            InvokeNative(
                op,
                op,
                nativeCall: () =>
                {
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
                    clipboardClear(scopeJson, s_clearDelegate);
#endif
                },
                onNativeFailureResult: message => FireClearResult(
                    MacClipboardChangeCountResult.Failure(MacClipboardErrorCodes.BridgeUnavailable, message)));
        }

        /// <summary>
        /// Reads pasteboard metadata without touching the payload: the change count, the types on
        /// each item, and which items matched <paramref name="matchingTypes"/>.
        /// Unity main thread only.
        /// <para>
        /// Not reading the payload is an optimisation, not a privacy contract. Like every read,
        /// this may still be surfaced to the user.
        /// </para>
        /// <para>
        /// Use this before <see cref="Read"/> when the pasteboard may be large: the types come back
        /// without paying the memory cost of the content.
        /// </para>
        /// </summary>
        /// <param name="matchingTypes">
        /// Types to match, or <c>null</c> to request no matching. An empty list is not the same as
        /// <c>null</c>: the native layer rejects it with
        /// <see cref="MacClipboardErrorCodes.EmptyTypeFilter"/>.
        /// </param>
        /// <param name="scope">Target pasteboard, or <c>null</c> for the general pasteboard.</param>
        /// <param name="onResult">Optional per-call callback. <see cref="SnapshotCompleted"/> always fires first.</param>
        public void Snapshot(
            IReadOnlyList<string>? matchingTypes = null,
            MacPasteboardScope? scope = null,
            Action<MacClipboardSnapshotResult>? onResult = null)
        {
            const string op = MacClipboardOperations.Snapshot;

            Func<Action<MacClipboardSnapshotResult>?> commonSelector = () => this.SnapshotCompleted;
            Func<int, string, MacClipboardSnapshotResult> failure =
                (code, message) => MacClipboardSnapshotResult.Failure(code, message);

            if (!TryPassGuards(op, onResult, commonSelector, failure))
            {
                return;
            }

            Debug.Log($"[{LogTag}][{nameof(Snapshot)}] matchingTypeCount: {matchingTypes?.Count ?? 0}, " +
                      $"hasMatchingTypes: {matchingTypes != null}, hasScope: {scope != null}, " +
                      $"hasCallback: {onResult != null}");

            string scopeJson;
            string? matchingTypesJson;
            try
            {
                scopeJson = MacClipboardJsonBuilder.BuildScopeJson(scope ?? MacPasteboardScope.General);
                matchingTypesJson = MacClipboardJsonBuilder.BuildMatchingTypesJson(matchingTypes);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{LogTag}][{nameof(Snapshot)}] build: {ex.Message}");
                DispatchRejectedResult(
                    failure(MacClipboardErrorCodes.BridgeUnavailable, CouldNotStartMessage(op)),
                    commonSelector(), onResult);
                return;
            }

            if (!TryBeginOperation(s_inFlight, op))
            {
                Debug.LogWarning($"[{LogTag}] {op} rejected: already in progress.");
                DispatchRejectedResult(
                    failure(MacClipboardErrorCodes.Busy, BusyMessage(op)),
                    commonSelector(), onResult);
                return;
            }

            s_onSnapshot = onResult;
            InvokeNative(
                op,
                op,
                nativeCall: () =>
                {
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
                    clipboardSnapshot(matchingTypesJson, scopeJson, s_snapshotDelegate);
#endif
                },
                onNativeFailureResult: message => FireSnapshotResult(
                    MacClipboardSnapshotResult.Failure(MacClipboardErrorCodes.BridgeUnavailable, message)));
        }

        /// <summary>
        /// Creates a named or uniquely named pasteboard and returns the scope that refers to it.
        /// Unity main thread only.
        /// <para>
        /// The pasteboard lives in the pasteboard server and <b>outlives this process</b>. Releasing
        /// a unique one with <see cref="RemovePasteboard"/> is the caller's responsibility; this
        /// Manager does not track what it created. Never put confidential data on a named
        /// pasteboard, whose name any other process can guess.
        /// </para>
        /// </summary>
        /// <param name="request">What to create. Must not be null.</param>
        /// <param name="onResult">Optional per-call callback. <see cref="PasteboardCreated"/> always fires first.</param>
        public void CreatePasteboard(
            MacPasteboardCreationRequest request,
            Action<MacPasteboardScopeResult>? onResult = null)
        {
            const string op = MacClipboardOperations.CreatePasteboard;

            Func<Action<MacPasteboardScopeResult>?> commonSelector = () => this.PasteboardCreated;
            Func<int, string, MacPasteboardScopeResult> failure =
                (code, message) => MacPasteboardScopeResult.Failure(code, message);

            if (!TryPassGuards(
                    op, onResult, commonSelector, failure,
                    validate: () => request == null
                        ? (MacClipboardErrorCodes.InvalidRequest, "request must not be null.")
                        : ((int Code, string Message)?)null))
            {
                return;
            }

            Debug.Log($"[{LogTag}][{nameof(CreatePasteboard)}] kind: {request.Kind}, " +
                      $"hasCallback: {onResult != null}");

            string requestJson;
            try
            {
                requestJson = MacClipboardJsonBuilder.BuildCreateRequestJson(request);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{LogTag}][{nameof(CreatePasteboard)}] build: {ex.Message}");
                DispatchRejectedResult(
                    failure(MacClipboardErrorCodes.BridgeUnavailable, CouldNotStartMessage(op)),
                    commonSelector(), onResult);
                return;
            }

            if (!TryBeginOperation(s_inFlight, op))
            {
                Debug.LogWarning($"[{LogTag}] {op} rejected: already in progress.");
                DispatchRejectedResult(
                    failure(MacClipboardErrorCodes.Busy, BusyMessage(op)),
                    commonSelector(), onResult);
                return;
            }

            s_onCreatePasteboard = onResult;
            InvokeNative(
                op,
                op,
                nativeCall: () =>
                {
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
                    clipboardCreatePasteboard(requestJson, s_createPasteboardDelegate);
#endif
                },
                onNativeFailureResult: message => FireCreatePasteboardResult(
                    MacPasteboardScopeResult.Failure(MacClipboardErrorCodes.BridgeUnavailable, message)));
        }

        /// <summary>
        /// Releases a named or unique pasteboard from the pasteboard server.
        /// Unity main thread only.
        /// <para>
        /// The standard pasteboards cannot be released and fail with
        /// <see cref="MacClipboardErrorCodes.CannotReleaseStandardPasteboard"/>. That covers the
        /// general pasteboard and the font, ruler, find and drag pasteboards, and it is decided by
        /// name: passing one of those names as a unique scope is rejected too.
        /// </para>
        /// </summary>
        /// <param name="scope">Pasteboard to release. Must not be null.</param>
        /// <param name="onResult">
        /// Optional per-call callback. <see cref="ClipboardOperationCompleted"/> always fires first.
        /// </param>
        public void RemovePasteboard(
            MacPasteboardScope scope,
            Action<MacClipboardOperationResult>? onResult = null)
        {
            const string op = MacClipboardOperations.RemovePasteboard;

            Func<Action<MacClipboardOperationResult>?> commonSelector = () => this.ClipboardOperationCompleted;
            Func<int, string, MacClipboardOperationResult> failure =
                (code, message) => MacClipboardOperationResult.Failure(op, code, message);

            if (!TryPassGuards(
                    op, onResult, commonSelector, failure,
                    validate: () => scope == null
                        ? (MacClipboardErrorCodes.InvalidRequest, "scope must not be null.")
                        : ((int Code, string Message)?)null))
            {
                return;
            }

            Debug.Log($"[{LogTag}][{nameof(RemovePasteboard)}] scopeKind: {scope.Kind}, " +
                      $"hasCallback: {onResult != null}");

            string scopeJson;
            try
            {
                scopeJson = MacClipboardJsonBuilder.BuildScopeJson(scope);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{LogTag}][{nameof(RemovePasteboard)}] build: {ex.Message}");
                DispatchRejectedResult(
                    failure(MacClipboardErrorCodes.BridgeUnavailable, CouldNotStartMessage(op)),
                    commonSelector(), onResult);
                return;
            }

            if (!TryBeginOperation(s_inFlight, op))
            {
                Debug.LogWarning($"[{LogTag}] {op} rejected: already in progress.");
                DispatchRejectedResult(
                    failure(MacClipboardErrorCodes.Busy, BusyMessage(op)),
                    commonSelector(), onResult);
                return;
            }

            s_onRemovePasteboard = onResult;
            // No onNativeFailureResult: this operation's result type is the one InvokeNative falls
            // back to building, so the default path is already correct here.
            InvokeNative(
                op,
                op,
                nativeCall: () =>
                {
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
                    clipboardRemovePasteboard(scopeJson, s_removePasteboardDelegate);
#endif
                });
        }

        /// <summary>
        /// Reports which of the requested patterns appear on the pasteboard, without returning the
        /// matched text. Unity main thread only.
        /// <para>
        /// Requires macOS 15.4 or later; earlier versions fail with
        /// <see cref="MacClipboardErrorCodes.DetectionUnavailable"/>. Nothing matching is a success
        /// with an empty list, not a failure.
        /// </para>
        /// </summary>
        /// <param name="patterns">
        /// Patterns to look for. Must not be null. An empty collection is rejected natively with
        /// <see cref="MacClipboardErrorCodes.EmptyDetectionPatterns"/>.
        /// </param>
        /// <param name="scope">Target pasteboard, or <c>null</c> for the general pasteboard.</param>
        /// <param name="onResult">Optional per-call callback. <see cref="PatternsDetected"/> always fires first.</param>
        public void DetectPatterns(
            IReadOnlyCollection<MacClipboardDetectionPattern> patterns,
            MacPasteboardScope? scope = null,
            Action<MacClipboardDetectedPatternsResult>? onResult = null)
        {
            const string op = MacClipboardOperations.DetectPatterns;

            Func<Action<MacClipboardDetectedPatternsResult>?> commonSelector = () => this.PatternsDetected;
            Func<int, string, MacClipboardDetectedPatternsResult> failure =
                (code, message) => MacClipboardDetectedPatternsResult.Failure(code, message);

            if (!TryPassGuards(
                    op, onResult, commonSelector, failure,
                    validate: () => patterns == null
                        ? (MacClipboardErrorCodes.InvalidRequest, "patterns must not be null.")
                        : ((int Code, string Message)?)null))
            {
                return;
            }

            Debug.Log($"[{LogTag}][{nameof(DetectPatterns)}] patternCount: {patterns.Count}, " +
                      $"hasScope: {scope != null}, hasCallback: {onResult != null}");

            string patternsJson, scopeJson;
            try
            {
                patternsJson = MacClipboardJsonBuilder.BuildPatternsJson(patterns);
                scopeJson = MacClipboardJsonBuilder.BuildScopeJson(scope ?? MacPasteboardScope.General);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{LogTag}][{nameof(DetectPatterns)}] build: {ex.Message}");
                DispatchRejectedResult(
                    failure(MacClipboardErrorCodes.BridgeUnavailable, CouldNotStartMessage(op)),
                    commonSelector(), onResult);
                return;
            }

            if (!TryBeginOperation(s_inFlight, op))
            {
                Debug.LogWarning($"[{LogTag}] {op} rejected: already in progress.");
                DispatchRejectedResult(
                    failure(MacClipboardErrorCodes.Busy, BusyMessage(op)),
                    commonSelector(), onResult);
                return;
            }

            s_onDetectPatterns = onResult;
            InvokeNative(
                op,
                op,
                nativeCall: () =>
                {
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
                    clipboardDetectPatterns(patternsJson, scopeJson, s_detectPatternsDelegate);
#endif
                },
                onNativeFailureResult: message => FireDetectPatternsResult(
                    MacClipboardDetectedPatternsResult.Failure(
                        MacClipboardErrorCodes.BridgeUnavailable, message)));
        }

        /// <summary>
        /// Returns the values behind the requested patterns, such as the detected links or phone
        /// numbers themselves. Unity main thread only.
        /// <para>
        /// Requires macOS 15.4 or later; earlier versions fail with
        /// <see cref="MacClipboardErrorCodes.DetectionUnavailable"/>. Unlike
        /// <see cref="DetectPatterns"/>, this returns clipboard content, so treat the result as
        /// sensitive.
        /// </para>
        /// </summary>
        /// <param name="patterns">Patterns to look for. Must not be null.</param>
        /// <param name="scope">Target pasteboard, or <c>null</c> for the general pasteboard.</param>
        /// <param name="onResult">Optional per-call callback. <see cref="ValuesDetected"/> always fires first.</param>
        public void DetectValues(
            IReadOnlyCollection<MacClipboardDetectionPattern> patterns,
            MacPasteboardScope? scope = null,
            Action<MacClipboardDetectedValuesResult>? onResult = null)
        {
            const string op = MacClipboardOperations.DetectValues;

            Func<Action<MacClipboardDetectedValuesResult>?> commonSelector = () => this.ValuesDetected;
            Func<int, string, MacClipboardDetectedValuesResult> failure =
                (code, message) => MacClipboardDetectedValuesResult.Failure(code, message);

            if (!TryPassGuards(
                    op, onResult, commonSelector, failure,
                    validate: () => patterns == null
                        ? (MacClipboardErrorCodes.InvalidRequest, "patterns must not be null.")
                        : ((int Code, string Message)?)null))
            {
                return;
            }

            Debug.Log($"[{LogTag}][{nameof(DetectValues)}] patternCount: {patterns.Count}, " +
                      $"hasScope: {scope != null}, hasCallback: {onResult != null}");

            string patternsJson, scopeJson;
            try
            {
                patternsJson = MacClipboardJsonBuilder.BuildPatternsJson(patterns);
                scopeJson = MacClipboardJsonBuilder.BuildScopeJson(scope ?? MacPasteboardScope.General);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{LogTag}][{nameof(DetectValues)}] build: {ex.Message}");
                DispatchRejectedResult(
                    failure(MacClipboardErrorCodes.BridgeUnavailable, CouldNotStartMessage(op)),
                    commonSelector(), onResult);
                return;
            }

            if (!TryBeginOperation(s_inFlight, op))
            {
                Debug.LogWarning($"[{LogTag}] {op} rejected: already in progress.");
                DispatchRejectedResult(
                    failure(MacClipboardErrorCodes.Busy, BusyMessage(op)),
                    commonSelector(), onResult);
                return;
            }

            s_onDetectValues = onResult;
            InvokeNative(
                op,
                op,
                nativeCall: () =>
                {
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
                    clipboardDetectValues(patternsJson, scopeJson, s_detectValuesDelegate);
#endif
                },
                onNativeFailureResult: message => FireDetectValuesResult(
                    MacClipboardDetectedValuesResult.Failure(
                        MacClipboardErrorCodes.BridgeUnavailable, message)));
        }

        /// <summary>
        /// Reports the kind of content on the pasteboard, such as a web page or an image.
        /// Unity main thread only.
        /// <para>
        /// Requires macOS 15.4 or later; earlier versions fail with
        /// <see cref="MacClipboardErrorCodes.DetectionUnavailable"/>.
        /// </para>
        /// <para>
        /// <b>Plain text fails</b> with <see cref="MacClipboardErrorCodes.DetectionFailed"/>, so a
        /// failure here does not distinguish "nothing to report" from "could not report".
        /// </para>
        /// </summary>
        /// <param name="scope">Target pasteboard, or <c>null</c> for the general pasteboard.</param>
        /// <param name="onResult">Optional per-call callback. <see cref="MetadataDetected"/> always fires first.</param>
        public void DetectMetadata(
            MacPasteboardScope? scope = null,
            Action<MacClipboardDetectedMetadataResult>? onResult = null)
        {
            const string op = MacClipboardOperations.DetectMetadata;

            Func<Action<MacClipboardDetectedMetadataResult>?> commonSelector = () => this.MetadataDetected;
            Func<int, string, MacClipboardDetectedMetadataResult> failure =
                (code, message) => MacClipboardDetectedMetadataResult.Failure(code, message);

            if (!TryPassGuards(op, onResult, commonSelector, failure))
            {
                return;
            }

            Debug.Log($"[{LogTag}][{nameof(DetectMetadata)}] hasScope: {scope != null}, " +
                      $"hasCallback: {onResult != null}");

            string scopeJson;
            try
            {
                scopeJson = MacClipboardJsonBuilder.BuildScopeJson(scope ?? MacPasteboardScope.General);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{LogTag}][{nameof(DetectMetadata)}] build: {ex.Message}");
                DispatchRejectedResult(
                    failure(MacClipboardErrorCodes.BridgeUnavailable, CouldNotStartMessage(op)),
                    commonSelector(), onResult);
                return;
            }

            if (!TryBeginOperation(s_inFlight, op))
            {
                Debug.LogWarning($"[{LogTag}] {op} rejected: already in progress.");
                DispatchRejectedResult(
                    failure(MacClipboardErrorCodes.Busy, BusyMessage(op)),
                    commonSelector(), onResult);
                return;
            }

            s_onDetectMetadata = onResult;
            InvokeNative(
                op,
                op,
                nativeCall: () =>
                {
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
                    clipboardDetectMetadata(scopeJson, s_detectMetadataDelegate);
#endif
                },
                onNativeFailureResult: message => FireDetectMetadataResult(
                    MacClipboardDetectedMetadataResult.Failure(
                        MacClipboardErrorCodes.BridgeUnavailable, message)));
        }

        /// <summary>
        /// Reports how the system expects this app to access the pasteboard.
        /// Unity main thread only.
        /// <para>
        /// On macOS earlier than 15.4 this <b>succeeds</b> with
        /// <see cref="MacClipboardAccessBehavior.Unavailable"/> rather than failing.
        /// </para>
        /// </summary>
        /// <param name="scope">Target pasteboard, or <c>null</c> for the general pasteboard.</param>
        /// <param name="onResult">
        /// Optional per-call callback. <see cref="AccessBehaviorChecked"/> always fires first.
        /// </param>
        public void GetAccessBehavior(
            MacPasteboardScope? scope = null,
            Action<MacClipboardAccessBehaviorResult>? onResult = null)
        {
            const string op = MacClipboardOperations.AccessBehavior;

            Func<Action<MacClipboardAccessBehaviorResult>?> commonSelector = () => this.AccessBehaviorChecked;
            Func<int, string, MacClipboardAccessBehaviorResult> failure =
                (code, message) => MacClipboardAccessBehaviorResult.Failure(code, message);

            if (!TryPassGuards(op, onResult, commonSelector, failure))
            {
                return;
            }

            Debug.Log($"[{LogTag}][{nameof(GetAccessBehavior)}] hasScope: {scope != null}, " +
                      $"hasCallback: {onResult != null}");

            string scopeJson;
            try
            {
                scopeJson = MacClipboardJsonBuilder.BuildScopeJson(scope ?? MacPasteboardScope.General);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{LogTag}][{nameof(GetAccessBehavior)}] build: {ex.Message}");
                DispatchRejectedResult(
                    failure(MacClipboardErrorCodes.BridgeUnavailable, CouldNotStartMessage(op)),
                    commonSelector(), onResult);
                return;
            }

            if (!TryBeginOperation(s_inFlight, op))
            {
                Debug.LogWarning($"[{LogTag}] {op} rejected: already in progress.");
                DispatchRejectedResult(
                    failure(MacClipboardErrorCodes.Busy, BusyMessage(op)),
                    commonSelector(), onResult);
                return;
            }

            s_onAccessBehavior = onResult;
            InvokeNative(
                op,
                op,
                nativeCall: () =>
                {
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
                    clipboardAccessBehavior(scopeJson, s_accessBehaviorDelegate);
#endif
                },
                onNativeFailureResult: message => FireAccessBehaviorResult(
                    MacClipboardAccessBehaviorResult.Failure(
                        MacClipboardErrorCodes.BridgeUnavailable, message)));
        }

        /// <summary>
        /// Reports whether the pasteboard changed since this app was last in the foreground.
        /// Unity main thread only.
        /// <para>
        /// <b>Use this instead of change observation, not alongside it.</b> Both share the same
        /// per-scope tracker, so while a scope is being observed this reports <c>false</c> almost
        /// always. The first call on a scope that is not being observed reports <c>true</c>.
        /// </para>
        /// </summary>
        /// <param name="scope">Target pasteboard, or <c>null</c> for the general pasteboard.</param>
        /// <param name="onResult">
        /// Optional per-call callback. <see cref="ForegroundChangeChecked"/> always fires first.
        /// </param>
        public void CheckForegroundChange(
            MacPasteboardScope? scope = null,
            Action<MacClipboardForegroundChangeResult>? onResult = null)
        {
            const string op = MacClipboardOperations.CheckForegroundChange;

            Func<Action<MacClipboardForegroundChangeResult>?> commonSelector = () => this.ForegroundChangeChecked;
            Func<int, string, MacClipboardForegroundChangeResult> failure =
                (code, message) => MacClipboardForegroundChangeResult.Failure(code, message);

            if (!TryPassGuards(op, onResult, commonSelector, failure))
            {
                return;
            }

            Debug.Log($"[{LogTag}][{nameof(CheckForegroundChange)}] hasScope: {scope != null}, " +
                      $"hasCallback: {onResult != null}");

            string scopeJson;
            try
            {
                scopeJson = MacClipboardJsonBuilder.BuildScopeJson(scope ?? MacPasteboardScope.General);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{LogTag}][{nameof(CheckForegroundChange)}] build: {ex.Message}");
                DispatchRejectedResult(
                    failure(MacClipboardErrorCodes.BridgeUnavailable, CouldNotStartMessage(op)),
                    commonSelector(), onResult);
                return;
            }

            if (!TryBeginOperation(s_inFlight, op))
            {
                Debug.LogWarning($"[{LogTag}] {op} rejected: already in progress.");
                DispatchRejectedResult(
                    failure(MacClipboardErrorCodes.Busy, BusyMessage(op)),
                    commonSelector(), onResult);
                return;
            }

            s_onCheckForegroundChange = onResult;
            InvokeNative(
                op,
                op,
                nativeCall: () =>
                {
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
                    clipboardCheckForegroundChange(scopeJson, s_checkForegroundChangeDelegate);
#endif
                },
                onNativeFailureResult: message => FireForegroundChangeResult(
                    MacClipboardForegroundChangeResult.Failure(
                        MacClipboardErrorCodes.BridgeUnavailable, message)));
        }

        /// <summary>
        /// Starts polling a pasteboard for changes. Unity main thread only.
        /// <para>
        /// Calling this again replaces the previous observation and its <paramref name="onChanged"/>
        /// registration, silently. A restart that <b>fails</b> changes nothing: the native layer
        /// validates the interval and resolves the scope before touching the running observation,
        /// so a mistyped scope does not cost you the observation you already had.
        /// </para>
        /// <para>
        /// <see cref="StartObserving"/> and <see cref="StopObserving"/> share one single-flight
        /// key, because both mutate the same native subscription. A second control call while one
        /// is pending is rejected with <see cref="MacClipboardErrorCodes.Busy"/>.
        /// </para>
        /// <para>
        /// Observation pauses while the app is not active and catches up when it returns. Do not
        /// combine it with <see cref="CheckForegroundChange"/> on the same scope: they share a
        /// tracker, and the check will then almost always report no change.
        /// </para>
        /// <para>
        /// An event the native layer cannot encode is dropped there without notice, so a missing
        /// event is not observable from here.
        /// </para>
        /// </summary>
        /// <param name="scope">Pasteboard to watch, or <c>null</c> for the general pasteboard.</param>
        /// <param name="intervalSeconds">
        /// Polling interval. Must be greater than 0 and at most 60; outside that the call fails
        /// with <see cref="MacClipboardErrorCodes.InvalidConfiguration"/>.
        /// </param>
        /// <param name="onChanged">
        /// Optional registration for change events, replaced by the next successful start.
        /// <see cref="ClipboardChanged"/> fires whether or not this is supplied.
        /// </param>
        /// <param name="onStarted">
        /// Optional per-call callback for the start itself.
        /// <see cref="ClipboardOperationCompleted"/> always fires first.
        /// </param>
        public void StartObserving(
            MacPasteboardScope? scope = null,
            double intervalSeconds = MacClipboardLimits.DefaultObservationInterval,
            Action<MacClipboardChangeEvent>? onChanged = null,
            Action<MacClipboardOperationResult>? onStarted = null)
        {
            const string op = MacClipboardOperations.StartObserving;
            const string key = MacClipboardOperations.ObservationControlKey;

            Func<Action<MacClipboardOperationResult>?> commonSelector = () => this.ClipboardOperationCompleted;
            Func<int, string, MacClipboardOperationResult> failure =
                (code, message) => MacClipboardOperationResult.Failure(op, code, message);

            // intervalSeconds is deliberately not validated here: the native layer rejects an
            // out-of-range value with InvalidConfiguration, and checking it twice would give one
            // condition two different error codes.
            if (!TryPassGuards(op, onStarted, commonSelector, failure))
            {
                return;
            }

            Debug.Log($"[{LogTag}][{nameof(StartObserving)}] hasScope: {scope != null}, " +
                      $"hasOnChanged: {onChanged != null}, hasCallback: {onStarted != null}");

            string scopeJson;
            try
            {
                scopeJson = MacClipboardJsonBuilder.BuildScopeJson(scope ?? MacPasteboardScope.General);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{LogTag}][{nameof(StartObserving)}] build: {ex.Message}");
                DispatchRejectedResult(
                    failure(MacClipboardErrorCodes.BridgeUnavailable, CouldNotStartMessage(op)),
                    commonSelector(), onStarted);
                return;
            }

            if (!TryBeginOperation(s_inFlight, key))
            {
                Debug.LogWarning($"[{LogTag}] {op} rejected: another observation control call is pending.");
                DispatchRejectedResult(
                    failure(MacClipboardErrorCodes.Busy, ObservationBusyMessage()),
                    commonSelector(), onStarted);
                return;
            }

            // Stage 7. The new registration goes to the pending slot only. The active one keeps
            // receiving events until the native layer confirms the switch, matching the native
            // behaviour of leaving the old observation running on a failed restart.
            s_onStartObserving = onStarted;
            s_pendingOnChanged = onChanged;

            InvokeNative(
                op,
                key,
                nativeCall: () =>
                {
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
                    clipboardStartObserving(
                        scopeJson, intervalSeconds, s_startObservingDelegate, s_changeDelegate);
#endif
                },
                // The native layer never saw this call, so the pending registration is abandoned
                // and the active one is left exactly as it was.
                onNativeFailure: () => s_pendingOnChanged = null);
        }

        /// <summary>
        /// Stops change observation. Unity main thread only.
        /// <para>
        /// Idempotent: stopping when nothing is being observed succeeds. The registration is
        /// released only once the native layer confirms the stop, so a failed stop leaves events
        /// flowing, which is what the native layer is still doing.
        /// </para>
        /// <para>
        /// Shares a single-flight key with <see cref="StartObserving"/>.
        /// </para>
        /// </summary>
        /// <param name="onResult">
        /// Optional per-call callback. <see cref="ClipboardOperationCompleted"/> always fires first.
        /// </param>
        public void StopObserving(Action<MacClipboardOperationResult>? onResult = null)
        {
            const string op = MacClipboardOperations.StopObserving;
            const string key = MacClipboardOperations.ObservationControlKey;

            Func<Action<MacClipboardOperationResult>?> commonSelector = () => this.ClipboardOperationCompleted;
            Func<int, string, MacClipboardOperationResult> failure =
                (code, message) => MacClipboardOperationResult.Failure(op, code, message);

            if (!TryPassGuards(op, onResult, commonSelector, failure))
            {
                return;
            }

            Debug.Log($"[{LogTag}][{nameof(StopObserving)}] hasCallback: {onResult != null}");

            // No request JSON to build, so stage 5 does not apply here.
            if (!TryBeginOperation(s_inFlight, key))
            {
                Debug.LogWarning($"[{LogTag}] {op} rejected: another observation control call is pending.");
                DispatchRejectedResult(
                    failure(MacClipboardErrorCodes.Busy, ObservationBusyMessage()),
                    commonSelector(), onResult);
                return;
            }

            // No pending registration: a stop has nothing to promote, and the active registration
            // is released on successful completion rather than here.
            s_onStopObserving = onResult;

            InvokeNative(
                op,
                key,
                nativeCall: () =>
                {
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
                    clipboardStopObserving(s_stopObservingDelegate);
#endif
                });
        }

        // ── Native invocation ───────────────────────────────────────────────────

        /// <summary>
        /// Issues the native call. This call already owns the in-flight marker and the per-call
        /// slot, so a P/Invoke failure takes the normal release path rather than the rejected one.
        /// <para>
        /// <paramref name="onNativeFailureResult"/> is mandatory in practice for every operation
        /// that does not return <see cref="MacClipboardOperationResult"/>. The fallback below can
        /// only build that one type, so omitting it elsewhere would send the wrong result to the
        /// wrong event and leave the per-call slot occupied forever.
        /// </para>
        /// </summary>
        /// <param name="operation">Operation name, used in messages and logs.</param>
        /// <param name="inFlightKey">Single-flight key held by this call.</param>
        /// <param name="nativeCall">The P/Invoke itself.</param>
        /// <param name="onNativeFailure">
        /// Operation-specific rollback, run before the result is dispatched. Used to drop a pending
        /// observation registration that never reached the native layer.
        /// </param>
        /// <param name="onNativeFailureResult">
        /// Builds and dispatches the failure result, which is also what releases the in-flight
        /// marker and the per-call slot. Omit it only for operations whose result type is
        /// <see cref="MacClipboardOperationResult"/>.
        /// </param>
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
                    MacClipboardOperationResult.Failure(
                        operation, MacClipboardErrorCodes.BridgeUnavailable, CouldNotStartMessage(operation)),
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

#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        [MonoPInvokeCallback(typeof(ClipboardJsonCallback))]
        private static void OnCopyResult(bool isSuccess, string? json, long errorCode, string? errorMessage) =>
            HandleOwnershipCallback(MacClipboardOperations.Copy, isSuccess, json, errorCode, errorMessage);

        [MonoPInvokeCallback(typeof(ClipboardJsonCallback))]
        private static void OnAppendResult(bool isSuccess, string? json, long errorCode, string? errorMessage) =>
            HandleOwnershipCallback(MacClipboardOperations.Append, isSuccess, json, errorCode, errorMessage);

        [MonoPInvokeCallback(typeof(ClipboardJsonCallback))]
        private static void OnReadResult(bool isSuccess, string? json, long errorCode, string? errorMessage) =>
            HandleReadCallback(isSuccess, json, errorCode, errorMessage);

        [MonoPInvokeCallback(typeof(ClipboardJsonCallback))]
        private static void OnReadDataResult(bool isSuccess, string? json, long errorCode, string? errorMessage) =>
            HandleReadDataCallback(isSuccess, json, errorCode, errorMessage);

        [MonoPInvokeCallback(typeof(ClipboardJsonCallback))]
        private static void OnClearResult(bool isSuccess, string? json, long errorCode, string? errorMessage) =>
            HandleClearCallback(isSuccess, json, errorCode, errorMessage);

        [MonoPInvokeCallback(typeof(ClipboardJsonCallback))]
        private static void OnSnapshotResult(bool isSuccess, string? json, long errorCode, string? errorMessage) =>
            HandleSnapshotCallback(isSuccess, json, errorCode, errorMessage);

        [MonoPInvokeCallback(typeof(ClipboardJsonCallback))]
        private static void OnCreatePasteboardResult(
            bool isSuccess, string? json, long errorCode, string? errorMessage) =>
            HandleCreatePasteboardCallback(isSuccess, json, errorCode, errorMessage);

        [MonoPInvokeCallback(typeof(ClipboardCallback))]
        private static void OnRemovePasteboardResult(bool isSuccess, long errorCode, string? errorMessage) =>
            HandleRemovePasteboardCallback(isSuccess, errorCode, errorMessage);

        [MonoPInvokeCallback(typeof(ClipboardJsonCallback))]
        private static void OnDetectPatternsResult(
            bool isSuccess, string? json, long errorCode, string? errorMessage) =>
            HandleDetectPatternsCallback(isSuccess, json, errorCode, errorMessage);

        [MonoPInvokeCallback(typeof(ClipboardJsonCallback))]
        private static void OnDetectValuesResult(
            bool isSuccess, string? json, long errorCode, string? errorMessage) =>
            HandleDetectValuesCallback(isSuccess, json, errorCode, errorMessage);

        [MonoPInvokeCallback(typeof(ClipboardJsonCallback))]
        private static void OnDetectMetadataResult(
            bool isSuccess, string? json, long errorCode, string? errorMessage) =>
            HandleDetectMetadataCallback(isSuccess, json, errorCode, errorMessage);

        [MonoPInvokeCallback(typeof(ClipboardJsonCallback))]
        private static void OnAccessBehaviorResult(
            bool isSuccess, string? json, long errorCode, string? errorMessage) =>
            HandleAccessBehaviorCallback(isSuccess, json, errorCode, errorMessage);

        [MonoPInvokeCallback(typeof(ClipboardJsonCallback))]
        private static void OnCheckForegroundChangeResult(
            bool isSuccess, string? json, long errorCode, string? errorMessage) =>
            HandleForegroundChangeCallback(isSuccess, json, errorCode, errorMessage);

        [MonoPInvokeCallback(typeof(ClipboardCallback))]
        private static void OnStartObservingResult(bool isSuccess, long errorCode, string? errorMessage) =>
            HandleObservationControlCallback(
                MacClipboardOperations.StartObserving, isSuccess, errorCode, errorMessage);

        [MonoPInvokeCallback(typeof(ClipboardCallback))]
        private static void OnStopObservingResult(bool isSuccess, long errorCode, string? errorMessage) =>
            HandleObservationControlCallback(
                MacClipboardOperations.StopObserving, isSuccess, errorCode, errorMessage);

        [MonoPInvokeCallback(typeof(ClipboardChangeCallback))]
        private static void OnClipboardChanged(string? eventJson) => HandleChangeEvent(eventJson);
#endif

        // The callback bodies live outside the narrow guard so the Editor completion seams can
        // drive exactly the same code a native callback would.

        private static void HandleOwnershipCallback(
            string operation, bool isSuccess, string? json, long errorCode, string? errorMessage)
        {
            if (DiscardIfTerminated(operation)) return;

            if (!isSuccess)
            {
                FireOwnershipResult(
                    MacClipboardOwnershipResult.Failure(operation, errorCode, errorMessage), operation);
                return;
            }

            if (!MacClipboardJsonParser.TryParseOwnership(json, out MacPasteboardOwnership? ownership) ||
                ownership == null)
            {
                LogParseFailure(operation);
                FireOwnershipResult(
                    MacClipboardOwnershipResult.Failure(
                        operation, MacClipboardErrorCodes.ResponseParseFailed, ResponseParseFailedMessage),
                    operation);
                return;
            }

            FireOwnershipResult(MacClipboardOwnershipResult.Success(operation, ownership), operation);
        }

        private static void HandleReadCallback(bool isSuccess, string? json, long errorCode, string? errorMessage)
        {
            const string op = MacClipboardOperations.Read;
            if (DiscardIfTerminated(op)) return;

            if (!isSuccess)
            {
                FireReadResult(MacClipboardReadResult.Failure(errorCode, errorMessage));
                return;
            }

            if (!MacClipboardJsonParser.TryParseReadResult(json, out MacClipboardReadContents? contents) ||
                contents == null)
            {
                LogParseFailure(op);
                FireReadResult(MacClipboardReadResult.Failure(
                    MacClipboardErrorCodes.ResponseParseFailed, ResponseParseFailedMessage));
                return;
            }

            FireReadResult(MacClipboardReadResult.Success(contents));
        }

        private static void HandleReadDataCallback(bool isSuccess, string? json, long errorCode, string? errorMessage)
        {
            const string op = MacClipboardOperations.ReadData;
            if (DiscardIfTerminated(op)) return;

            if (!isSuccess)
            {
                FireReadDataResult(MacClipboardReadDataResult.Failure(errorCode, errorMessage));
                return;
            }

            // A null payload here is the documented "no data for that type" success, so only a
            // parse failure is an error.
            if (!MacClipboardJsonParser.TryParseReadData(json, out byte[]? data))
            {
                LogParseFailure(op);
                FireReadDataResult(MacClipboardReadDataResult.Failure(
                    MacClipboardErrorCodes.ResponseParseFailed, ResponseParseFailedMessage));
                return;
            }

            FireReadDataResult(MacClipboardReadDataResult.Success(data));
        }

        private static void HandleClearCallback(bool isSuccess, string? json, long errorCode, string? errorMessage)
        {
            const string op = MacClipboardOperations.Clear;
            if (DiscardIfTerminated(op)) return;

            if (!isSuccess)
            {
                FireClearResult(MacClipboardChangeCountResult.Failure(errorCode, errorMessage));
                return;
            }

            if (!MacClipboardJsonParser.TryParseChangeCount(json, out long changeCount))
            {
                LogParseFailure(op);
                FireClearResult(MacClipboardChangeCountResult.Failure(
                    MacClipboardErrorCodes.ResponseParseFailed, ResponseParseFailedMessage));
                return;
            }

            FireClearResult(MacClipboardChangeCountResult.Success(changeCount));
        }

        private static void HandleSnapshotCallback(
            bool isSuccess, string? json, long errorCode, string? errorMessage)
        {
            const string op = MacClipboardOperations.Snapshot;
            if (DiscardIfTerminated(op)) return;

            if (!isSuccess)
            {
                FireSnapshotResult(MacClipboardSnapshotResult.Failure(errorCode, errorMessage));
                return;
            }

            if (!MacClipboardJsonParser.TryParseSnapshot(json, out MacClipboardSnapshot? snapshot)
                || snapshot == null)
            {
                LogParseFailure(op);
                FireSnapshotResult(MacClipboardSnapshotResult.Failure(
                    MacClipboardErrorCodes.ResponseParseFailed, ResponseParseFailedMessage));
                return;
            }

            FireSnapshotResult(MacClipboardSnapshotResult.Success(snapshot));
        }

        private static void HandleCreatePasteboardCallback(
            bool isSuccess, string? json, long errorCode, string? errorMessage)
        {
            const string op = MacClipboardOperations.CreatePasteboard;
            if (DiscardIfTerminated(op)) return;

            if (!isSuccess)
            {
                FireCreatePasteboardResult(MacPasteboardScopeResult.Failure(errorCode, errorMessage));
                return;
            }

            if (!MacClipboardJsonParser.TryParseScopeResult(json, out MacPasteboardScope? scope)
                || scope == null)
            {
                LogParseFailure(op);
                FireCreatePasteboardResult(MacPasteboardScopeResult.Failure(
                    MacClipboardErrorCodes.ResponseParseFailed, ResponseParseFailedMessage));
                return;
            }

            FireCreatePasteboardResult(MacPasteboardScopeResult.Success(scope));
        }

        // No payload to parse, so this is the one completion path with no 9006 branch.
        private static void HandleRemovePasteboardCallback(
            bool isSuccess, long errorCode, string? errorMessage)
        {
            const string op = MacClipboardOperations.RemovePasteboard;
            if (DiscardIfTerminated(op)) return;

            FireOperationResult(
                isSuccess
                    ? MacClipboardOperationResult.Success(op)
                    : MacClipboardOperationResult.Failure(op, errorCode, errorMessage),
                op);
        }

        private static void HandleDetectPatternsCallback(
            bool isSuccess, string? json, long errorCode, string? errorMessage)
        {
            const string op = MacClipboardOperations.DetectPatterns;
            if (DiscardIfTerminated(op)) return;

            if (!isSuccess)
            {
                FireDetectPatternsResult(MacClipboardDetectedPatternsResult.Failure(errorCode, errorMessage));
                return;
            }

            // An empty array is a valid success: nothing matched.
            if (!MacClipboardJsonParser.TryParsePatterns(
                    json, out IReadOnlyList<MacClipboardDetectionPattern> patterns))
            {
                LogParseFailure(op);
                FireDetectPatternsResult(MacClipboardDetectedPatternsResult.Failure(
                    MacClipboardErrorCodes.ResponseParseFailed, ResponseParseFailedMessage));
                return;
            }

            FireDetectPatternsResult(MacClipboardDetectedPatternsResult.Success(patterns));
        }

        private static void HandleDetectValuesCallback(
            bool isSuccess, string? json, long errorCode, string? errorMessage)
        {
            const string op = MacClipboardOperations.DetectValues;
            if (DiscardIfTerminated(op)) return;

            if (!isSuccess)
            {
                FireDetectValuesResult(MacClipboardDetectedValuesResult.Failure(errorCode, errorMessage));
                return;
            }

            if (!MacClipboardJsonParser.TryParseDetectedValues(json, out MacClipboardDetectedValues? values)
                || values == null)
            {
                LogParseFailure(op);
                FireDetectValuesResult(MacClipboardDetectedValuesResult.Failure(
                    MacClipboardErrorCodes.ResponseParseFailed, ResponseParseFailedMessage));
                return;
            }

            FireDetectValuesResult(MacClipboardDetectedValuesResult.Success(values));
        }

        private static void HandleDetectMetadataCallback(
            bool isSuccess, string? json, long errorCode, string? errorMessage)
        {
            const string op = MacClipboardOperations.DetectMetadata;
            if (DiscardIfTerminated(op)) return;

            if (!isSuccess)
            {
                FireDetectMetadataResult(MacClipboardDetectedMetadataResult.Failure(errorCode, errorMessage));
                return;
            }

            if (!MacClipboardJsonParser.TryParseDetectedMetadata(
                    json, out MacClipboardDetectedMetadata? metadata) || metadata == null)
            {
                LogParseFailure(op);
                FireDetectMetadataResult(MacClipboardDetectedMetadataResult.Failure(
                    MacClipboardErrorCodes.ResponseParseFailed, ResponseParseFailedMessage));
                return;
            }

            FireDetectMetadataResult(MacClipboardDetectedMetadataResult.Success(metadata));
        }

        private static void HandleAccessBehaviorCallback(
            bool isSuccess, string? json, long errorCode, string? errorMessage)
        {
            const string op = MacClipboardOperations.AccessBehavior;
            if (DiscardIfTerminated(op)) return;

            if (!isSuccess)
            {
                FireAccessBehaviorResult(MacClipboardAccessBehaviorResult.Failure(errorCode, errorMessage));
                return;
            }

            // An unknown behaviour string parses to Unknown rather than failing, so only a
            // malformed response reaches the 9006 branch.
            if (!MacClipboardJsonParser.TryParseAccessBehavior(json, out MacClipboardAccessBehavior behavior))
            {
                LogParseFailure(op);
                FireAccessBehaviorResult(MacClipboardAccessBehaviorResult.Failure(
                    MacClipboardErrorCodes.ResponseParseFailed, ResponseParseFailedMessage));
                return;
            }

            FireAccessBehaviorResult(MacClipboardAccessBehaviorResult.Success(behavior));
        }

        private static void HandleForegroundChangeCallback(
            bool isSuccess, string? json, long errorCode, string? errorMessage)
        {
            const string op = MacClipboardOperations.CheckForegroundChange;
            if (DiscardIfTerminated(op)) return;

            if (!isSuccess)
            {
                FireForegroundChangeResult(
                    MacClipboardForegroundChangeResult.Failure(errorCode, errorMessage));
                return;
            }

            if (!MacClipboardJsonParser.TryParseBool(json, out bool changed))
            {
                LogParseFailure(op);
                FireForegroundChangeResult(MacClipboardForegroundChangeResult.Failure(
                    MacClipboardErrorCodes.ResponseParseFailed, ResponseParseFailedMessage));
                return;
            }

            FireForegroundChangeResult(MacClipboardForegroundChangeResult.Success(changed));
        }

        /// <summary>
        /// Completion path shared by startObserving and stopObserving, which share a single-flight
        /// key and both mutate the change registration.
        /// <para>
        /// Single-flight makes the owner unambiguous: at most one control call is outstanding, so
        /// the completion that arrives can only belong to the pending slot's owner. That is why no
        /// generation counter is needed to tell registrations apart.
        /// </para>
        /// </summary>
        private static void HandleObservationControlCallback(
            string operation, bool isSuccess, long errorCode, string? errorMessage)
        {
            if (DiscardIfTerminated(operation))
            {
                // The managed side is safe to drop, but a start that succeeded has left a native
                // poller running: teardown's own stop may have been executed before it. Reissuing
                // the stop closes that window. stopObserving is idempotent, so reissuing when the
                // two did not cross is harmless. A failed start began nothing, so it needs none.
                if (isSuccess && operation == MacClipboardOperations.StartObserving)
                {
                    Debug.LogWarning(
                        $"[{LogTag}] A start that completed after destruction left native observation " +
                        "running; reissuing the teardown stop.");
                    StopObservingForTeardown();
                }
                return;
            }

            if (operation == MacClipboardOperations.StartObserving)
            {
                // Promote on success, abandon on failure. Either way the pending slot is cleared,
                // and on failure the active registration is left untouched because the native
                // layer has left the old observation running.
                if (isSuccess)
                {
                    s_onChanged = s_pendingOnChanged;
                }
                s_pendingOnChanged = null;
            }
            else if (isSuccess)
            {
                // A failed stop did not stop anything natively, so the registration stays.
                s_onChanged = null;
            }

            FireOperationResult(
                isSuccess
                    ? MacClipboardOperationResult.Success(operation)
                    : MacClipboardOperationResult.Failure(operation, errorCode, errorMessage),
                MacClipboardOperations.ObservationControlKey);
        }

        private static void HandleChangeEvent(string? eventJson)
        {
            // Guarded first: a payload from a destroyed lifetime is not even parsed.
            if (DiscardIfTerminated(MacClipboardOperations.StartObserving)) return;

            if (!MacClipboardJsonParser.TryParseChangeEvent(
                    eventJson, out MacClipboardChangeEvent? changeEvent) || changeEvent == null)
            {
                // Dropped rather than surfaced: a change event carries no per-call contract to
                // fail, and there is no operation waiting on it.
                Debug.LogError(
                    $"[{LogTag}] A change event could not be parsed and was dropped.");
                return;
            }

            FireClipboardChanged(changeEvent);
        }

        // The payload itself is never logged: it may hold clipboard content.
        private static void LogParseFailure(string operation) =>
            Debug.LogError($"[{LogTag}] {operation}: {ResponseParseFailedMessage}");

#if UNITY_EDITOR
        // ── Editor-only completion seams ────────────────────────────────────────
        // These drive the same production completion paths a native callback would, so tests can
        // observe real pending-slot and in-flight transitions. Compiled out of player builds; they
        // never bypass DiscardIfTerminated or the single-flight release.

        internal static void CompleteOwnershipForTests(
            string operation, bool isSuccess, string? json = null,
            long errorCode = 0, string? errorMessage = null) =>
            HandleOwnershipCallback(operation, isSuccess, json, errorCode, errorMessage);

        internal static void CompleteReadForTests(
            bool isSuccess, string? json = null, long errorCode = 0, string? errorMessage = null) =>
            HandleReadCallback(isSuccess, json, errorCode, errorMessage);

        internal static void CompleteReadDataForTests(
            bool isSuccess, string? json = null, long errorCode = 0, string? errorMessage = null) =>
            HandleReadDataCallback(isSuccess, json, errorCode, errorMessage);

        internal static void CompleteClearForTests(
            bool isSuccess, string? json = null, long errorCode = 0, string? errorMessage = null) =>
            HandleClearCallback(isSuccess, json, errorCode, errorMessage);

        internal static void CompleteSnapshotForTests(
            bool isSuccess, string? json = null, long errorCode = 0, string? errorMessage = null) =>
            HandleSnapshotCallback(isSuccess, json, errorCode, errorMessage);

        internal static void CompleteCreatePasteboardForTests(
            bool isSuccess, string? json = null, long errorCode = 0, string? errorMessage = null) =>
            HandleCreatePasteboardCallback(isSuccess, json, errorCode, errorMessage);

        // removePasteboard is the only value-less operation outside change observation, so this
        // seam needs no operation argument.
        internal static void CompleteOperationForTests(
            bool isSuccess, long errorCode = 0, string? errorMessage = null) =>
            HandleRemovePasteboardCallback(isSuccess, errorCode, errorMessage);

        internal static void CompleteDetectPatternsForTests(
            bool isSuccess, string? json = null, long errorCode = 0, string? errorMessage = null) =>
            HandleDetectPatternsCallback(isSuccess, json, errorCode, errorMessage);

        internal static void CompleteDetectValuesForTests(
            bool isSuccess, string? json = null, long errorCode = 0, string? errorMessage = null) =>
            HandleDetectValuesCallback(isSuccess, json, errorCode, errorMessage);

        internal static void CompleteDetectMetadataForTests(
            bool isSuccess, string? json = null, long errorCode = 0, string? errorMessage = null) =>
            HandleDetectMetadataCallback(isSuccess, json, errorCode, errorMessage);

        internal static void CompleteAccessBehaviorForTests(
            bool isSuccess, string? json = null, long errorCode = 0, string? errorMessage = null) =>
            HandleAccessBehaviorCallback(isSuccess, json, errorCode, errorMessage);

        internal static void CompleteForegroundChangeForTests(
            bool isSuccess, string? json = null, long errorCode = 0, string? errorMessage = null) =>
            HandleForegroundChangeCallback(isSuccess, json, errorCode, errorMessage);

        /// <summary>
        /// Completes startObserving or stopObserving. Not interchangeable with
        /// <see cref="CompleteOperationForTests"/>: only this path runs the active / pending
        /// transitions, so only this one can drive the restart cases.
        /// </summary>
        internal static void CompleteObservationControlForTests(
            string operation, bool isSuccess, long errorCode = 0, string? errorMessage = null) =>
            HandleObservationControlCallback(operation, isSuccess, errorCode, errorMessage);

        internal static void DeliverChangeEventForTests(string? eventJson) => HandleChangeEvent(eventJson);

        internal static bool HasChangeRegistrationForTests => s_onChanged != null;

        internal static bool HasPendingChangeRegistrationForTests => s_pendingOnChanged != null;

        /// <summary>
        /// How many times teardown has issued <c>clipboardStopObserving</c>.
        /// <para>
        /// <b>This is the total, not the reissues alone.</b> <c>OnDestroy</c> issues one itself, so
        /// the value is already 1 right after destruction. A test that only asserts 1 would
        /// therefore pass even with the reissue rule missing; assert 2 after injecting a late
        /// successful start, and 1 after a late failed one.
        /// </para>
        /// </summary>
        internal static int TeardownStopIssueCountForTests { get; set; }

        internal static bool IsInFlightForTests(string key) => s_inFlight.Contains(key);

        internal static int InFlightCountForTests => s_inFlight.Count;

        internal static bool HasAnyPendingCallbackForTests =>
            s_onCopy != null || s_onAppend != null || s_onRead != null ||
            s_onReadData != null || s_onClear != null || s_onSnapshot != null ||
            s_onCreatePasteboard != null || s_onRemovePasteboard != null ||
            s_onDetectPatterns != null || s_onDetectValues != null ||
            s_onDetectMetadata != null || s_onAccessBehavior != null ||
            s_onCheckForegroundChange != null || s_onStartObserving != null ||
            s_onStopObserving != null || s_onChanged != null || s_pendingOnChanged != null;
#endif
    }
}
#endif
