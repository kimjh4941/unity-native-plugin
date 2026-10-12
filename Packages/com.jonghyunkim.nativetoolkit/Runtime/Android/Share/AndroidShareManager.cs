#nullable enable

#if UNITY_ANDROID || UNITY_EDITOR
namespace JonghyunKim.NativeToolkit.Runtime.Android.Share
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using JonghyunKim.NativeToolkit.Runtime.Android.Common;
    using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
    using System.Runtime.InteropServices;
    using AOT;
#endif

    /// <summary>
    /// Singleton manager for Android native share operations.
    /// </summary>
    /// <remarks>
    /// Calls native-toolkit's C ABI (libntk.so, <c>ntk_share_*</c>) since 2.0.0; 1.x called a Kotlin
    /// bridge through AndroidJavaObject with JSON. The public API is unchanged. Behaviour that changed
    /// is listed in artifact/android/share/designs/2026-10-11-android-share-design-v6.md, 5.3; in short:
    /// <list type="bullet">
    /// <item>A share from the background fails as "Failed to &lt;op&gt;: the app is not in the
    /// foreground."</item>
    /// <item>A null payload, file list, ID list or chooser action throws ArgumentNullException (1.x:
    /// NullReferenceException); a null element of a list fails as "... must not contain null".</item>
    /// <item>Chooser actions with the same intentAction show once; an icon that starts like an image
    /// but cannot be read fails the share.</item>
    /// <item>The methods may be called from any thread (1.x refused other threads); results always
    /// arrive on Unity's main thread. <see cref="Instance"/> creates a GameObject the first time, which
    /// Unity allows on its main thread only: get it there first.</item>
    /// <item>Direct Share targets are registered and removed on the calling thread, synchronously.</item>
    /// <item>A Base64 icon must be standard Base64 (white space around it is ignored). A string is cut
    /// at its first U+0000, and an unpaired surrogate becomes U+FFFD.</item>
    /// <item>Several error messages are fixed texts; Android requires IL2CPP and ARM64 (or x86_64).</item>
    /// <item>Texts and subjects are logged by length only.</item>
    /// </list>
    /// Every result, failures found before the native call included, arrives on Unity's main thread,
    /// first through the shared event and then the per-call callback; for a call made on Unity's main
    /// thread, after the method has returned.
    /// </remarks>
    public class AndroidShareManager : MonoBehaviour
    {
        private const string LogTag = nameof(AndroidShareManager);

        /// <summary>Native operation name for sharing plain text.</summary>
        public const string OperationShareText = "shareText";

        /// <summary>Native operation name for sharing a single image file.</summary>
        public const string OperationShareImage = "shareImage";

        /// <summary>Native operation name for sharing multiple image files.</summary>
        public const string OperationShareImages = "shareImages";

        /// <summary>Native operation name for sharing a single arbitrary file.</summary>
        public const string OperationShareFile = "shareFile";

        /// <summary>Native operation name for sharing multiple arbitrary files.</summary>
        public const string OperationShareFiles = "shareFiles";

        /// <summary>Native operation name for registering a Direct Share target.</summary>
        public const string OperationRegisterDirectShareTarget = "registerDirectShareTarget";

        /// <summary>Native operation name for removing Direct Share target shortcuts.</summary>
        public const string OperationRemoveDirectShareTargets = "removeDirectShareTargets";

        /// <summary>Native operation name for sharing text with an app-selection callback.</summary>
        public const string OperationShareWithCallback = "shareWithCallback";

        /// <summary>Native operation name for cancelling a pending share callback.</summary>
        public const string OperationCancelPendingShareCallback = "cancelPendingShareCallback";

        private static AndroidShareManager? _instance;

        private readonly ShareChooserActionCallbackCoordinator _chooserCoordinator =
            new ShareChooserActionCallbackCoordinator(AndroidNativeCallbackQueue.Post);

        // The live manager's coordinator, read by the chooser action callback on the Android main
        // thread (design v6, 4.3; K-2). Unity's == is not used off the main thread, hence a plain field.
        private static volatile ShareChooserActionCallbackCoordinator? s_coordinator;

        // The state the native callbacks touch, all under s_lock (J-S1, J-S5, J-S13, J-S14): the
        // per-call callbacks by operation, the onSelected callback, the calls in flight by key, and
        // the accepted selection requests. Only dictionary and list work happens under the lock.
        private static readonly object s_lock = new object();
        private static readonly Dictionary<string, Action<ShareOperationResult>?> s_pendingOperations = new();
        private static Action<ShareCallbackResult>? s_pendingSelected;
        private static readonly Dictionary<long, AndroidShareCApi.ShareCall> s_calls = new();
        private static long s_nextKey;
        private static readonly List<ulong> s_selectionIds = new();

        // The two event listeners, registered once per process under their own lock (J-S6).
        private static readonly object s_listenerLock = new object();
        private static bool s_chooserListenerAdded;
        private static bool s_selectionListenerAdded;

        /// <summary>
        /// Occurs when any native share operation completes. Fires for both success and failure on
        /// all operations including <c>shareWithCallback</c> launch result. Always fired before the
        /// per-call callback.
        /// </summary>
        public event Action<ShareOperationResult>? ShareOperationCompleted;

        /// <summary>
        /// Occurs when the user selects an application in the <c>shareWithCallback</c> chooser.
        /// Not fired when the user cancels or chooses Copy/Edit. Always fired before the per-call
        /// <c>onSelected</c> callback.
        /// </summary>
        public event Action<ShareCallbackResult>? ShareCallbackReceived;

        /// <summary>
        /// Occurs when the user taps a custom chooser action in the Sharesheet (API 34+, Android
        /// only). Chooser actions are only registered when using <see cref="ShareText"/>; this
        /// event is not fired for <see cref="ShareWithCallback"/> calls even if
        /// <see cref="ShareTextPayload.chooserActions"/> is set. No callback fires if the action
        /// is never tapped or on API levels below 34. Always fired before the per-call
        /// <c>onChooserAction</c> callback.
        /// </summary>
        public event Action<ShareChooserActionResult>? ShareChooserActionTapped
        {
            add => _chooserCoordinator.ChooserActionTapped += value;
            remove => _chooserCoordinator.ChooserActionTapped -= value;
        }

        /// <summary>
        /// Singleton instance. Creates a new <see cref="AndroidShareManager"/> GameObject if none
        /// exists and ensures it persists across scene loads. Get it on Unity's main thread first:
        /// creating the GameObject is allowed there only. Once it exists, the methods may be called
        /// from any thread.
        /// </summary>
        public static AndroidShareManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    Debug.Log($"[{LogTag}][{nameof(Instance)}] creating the instance");
                    GameObject singletonObject = new GameObject("AndroidShareManager");
                    _instance = singletonObject.AddComponent<AndroidShareManager>();
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
            // Only the live manager's coordinator receives chooser actions (K-2).
            s_coordinator = _chooserCoordinator;
            // Created here, on the main thread: a native callback must never create Unity objects.
            AndroidNativeCallbackQueue.EnsureCreated();
            Initialize();
        }

        private void OnDestroy()
        {
            Debug.Log($"[{LogTag}][{nameof(OnDestroy)}]");
            // A second manager destroyed in its Awake must not clear the live one's state (J-S15).
            if (_instance != this) return;

            s_coordinator = null;
            ulong[] selectionIds;
            lock (s_lock)
            {
                s_pendingOperations.Clear();
                s_pendingSelected = null;
                selectionIds = AndroidShareCApi.TakeSelectionIds(s_selectionIds);
            }
            _chooserCoordinator.Clear();
            CancelSelections(selectionIds);
            _instance = null;
        }

        /// <summary>
        /// Registers the chooser action and selection listeners with the native library, once per
        /// process. Called from <c>Awake</c>; calling it again is harmless (J-S6).
        /// </summary>
        public void Initialize()
        {
            Debug.Log($"[{LogTag}][{nameof(Initialize)}]");
            if (Application.platform != RuntimePlatform.Android)
            {
                Debug.Log($"[{LogTag}][{nameof(Initialize)}] not running on Android; nothing to register");
                return;
            }
#if UNITY_ANDROID && !UNITY_EDITOR
            if (AndroidNativeToolkitCApi.EnsureNativeAvailable() == AndroidNativeToolkitCApi.NativeState.Available)
                EnsureListeners();
#endif
        }

        /// <summary>
        /// Shares plain text via the Android Sharesheet.
        /// Custom chooser actions (API 34+) are registered only for this <see cref="ShareText"/>
        /// call; a tap is reported via <see cref="ShareChooserActionTapped"/> and
        /// <paramref name="onChooserAction"/>. Chooser actions passed to
        /// <see cref="ShareWithCallback"/> are NOT registered for callbacks. To receive a
        /// callback, each chooser action must use a unique, non-blank intentAction other than
        /// <c>android.intent.action.SEND</c>. No callback fires if the action is never tapped,
        /// or on API levels below 34.
        /// </summary>
        /// <param name="payload">Text content and optional metadata to share.</param>
        /// <param name="onResult">Optional callback invoked with the launch result. The global
        /// <see cref="ShareOperationCompleted"/> event always fires regardless of this parameter.</param>
        /// <param name="onChooserAction">Optional per-call callback fired when a custom chooser
        /// action is tapped. Replaces the previous per-call callback (last-registered wins);
        /// passing null clears the previous registration. The global
        /// <see cref="ShareChooserActionTapped"/> event always fires regardless of this
        /// parameter.</param>
        /// <exception cref="ArgumentNullException">The payload or one of its chooser actions is null.</exception>
        public void ShareText(
            ShareTextPayload payload,
            Action<ShareOperationResult>? onResult = null,
            Action<ShareChooserActionResult>? onChooserAction = null)
        {
            const string op = OperationShareText;
            CheckNotNull(payload, nameof(payload));
            CheckActionsNotNull(payload.chooserActions);
            AndroidNativeCallbackQueue.EnsureCreated();
            int actionCount = payload.chooserActions?.Length ?? 0;
            Debug.Log($"[{LogTag}][{nameof(ShareText)}] {Describe(payload)}, hasCallback: {onResult != null}, hasChooserCallback: {onChooserAction != null}, chooserActionCount: {actionCount}");
            if (actionCount > 5)
            {
                Debug.LogWarning($"[{LogTag}] Android shows at most 5 custom chooser actions; extra actions may be ignored");
            }
            _chooserCoordinator.Register(onChooserAction);
            SetPendingOperation(op, onResult);
            if (!OnDevice(op)) return;
            if (Rejected(op, AndroidShareCApi.CheckText(op, payload.text))) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!NativeReady(op)) return;
            EnsureListeners();
            List<AndroidShareCApi.NormalizedAction> actions = AndroidShareCApi.NormalizeActions(payload.chooserActions);
            if (actions.Count != actionCount)
                Debug.Log($"[{LogTag}][{nameof(ShareText)}] passing {actions.Count} of {actionCount} chooser action(s)");
            OpenNative(new AndroidShareCApi.ShareCall(op), allocated =>
            {
                AndroidShareCApi.TextContent content = AndroidShareCApi.BuildTextContent(payload, allocated);
                IntPtr block = AndroidShareCApi.BuildActions(actions, allocated);
                return key => AndroidShareCApi.ntk_share_text(ref content, block, new UIntPtr((uint)actions.Count), s_donePointer, key, s_releasePointer);
            });
#endif
        }

        /// <summary>
        /// Shares a single image file via the Android share sheet.
        /// The file must reside in a directory exposed by the native-toolkit FileProvider.
        /// </summary>
        /// <param name="payload">Image file path and optional MIME type.</param>
        /// <param name="onResult">Optional per-call result callback.</param>
        /// <exception cref="ArgumentNullException">The payload is null.</exception>
        public void ShareImage(ShareImagePayload payload, Action<ShareOperationResult>? onResult = null)
        {
            const string op = OperationShareImage;
            CheckNotNull(payload, nameof(payload));
            AndroidNativeCallbackQueue.EnsureCreated();
            Debug.Log($"[{LogTag}][{nameof(ShareImage)}] filePath: {payload.filePath}, mimeType: {payload.mimeType ?? "null"}, hasCallback: {onResult != null}");
            SetPendingOperation(op, onResult);
            if (!OnDevice(op)) return;
            if (Rejected(op, AndroidShareCApi.CheckPath(op, payload.filePath))) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!NativeReady(op)) return;
            string? mimeType = AndroidShareCApi.IsAbsent(payload.mimeType) ? null : payload.mimeType;
            OpenNative(new AndroidShareCApi.ShareCall(op, new[] { payload.filePath }, mimeType), allocated =>
            {
                IntPtr path = AndroidNativeToolkitCApi.AllocUtf8(payload.filePath, allocated);
                IntPtr mime = AndroidNativeToolkitCApi.AllocUtf8(mimeType, allocated);
                return key => AndroidShareCApi.ntk_share_image(path, mime, s_donePointer, key, s_releasePointer);
            });
#endif
        }

        /// <summary>
        /// Shares multiple image files via the Android share sheet.
        /// Each file must reside in a directory exposed by the native-toolkit FileProvider.
        /// </summary>
        /// <param name="payload">Image file paths.</param>
        /// <param name="onResult">Optional per-call result callback.</param>
        /// <exception cref="ArgumentNullException">The payload or its file list is null.</exception>
        public void ShareImages(ShareImagesPayload payload, Action<ShareOperationResult>? onResult = null)
        {
            CheckNotNull(payload, nameof(payload));
            SharePaths(OperationShareImages, payload.filePaths, onResult, nameof(ShareImages));
        }

        /// <summary>
        /// Shares a single arbitrary file via the Android share sheet.
        /// The file must reside in a directory exposed by the native-toolkit FileProvider.
        /// </summary>
        /// <param name="payload">File path to share.</param>
        /// <param name="onResult">Optional per-call result callback.</param>
        /// <exception cref="ArgumentNullException">The payload is null.</exception>
        public void ShareFile(ShareFilePayload payload, Action<ShareOperationResult>? onResult = null)
        {
            const string op = OperationShareFile;
            CheckNotNull(payload, nameof(payload));
            AndroidNativeCallbackQueue.EnsureCreated();
            Debug.Log($"[{LogTag}][{nameof(ShareFile)}] filePath: {payload.filePath}, hasCallback: {onResult != null}");
            SetPendingOperation(op, onResult);
            if (!OnDevice(op)) return;
            if (Rejected(op, AndroidShareCApi.CheckPath(op, payload.filePath))) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!NativeReady(op)) return;
            OpenNative(new AndroidShareCApi.ShareCall(op, new[] { payload.filePath }), allocated =>
            {
                IntPtr path = AndroidNativeToolkitCApi.AllocUtf8(payload.filePath, allocated);
                return key => AndroidShareCApi.ntk_share_file(path, s_donePointer, key, s_releasePointer);
            });
#endif
        }

        /// <summary>
        /// Shares multiple arbitrary files via the Android share sheet.
        /// Each file must reside in a directory exposed by the native-toolkit FileProvider.
        /// </summary>
        /// <param name="payload">File paths to share.</param>
        /// <param name="onResult">Optional per-call result callback.</param>
        /// <exception cref="ArgumentNullException">The payload or its file list is null.</exception>
        public void ShareFiles(ShareFilesPayload payload, Action<ShareOperationResult>? onResult = null)
        {
            CheckNotNull(payload, nameof(payload));
            SharePaths(OperationShareFiles, payload.filePaths, onResult, nameof(ShareFiles));
        }

        /// <summary>
        /// Registers an Android Direct Share shortcut target.
        /// </summary>
        /// <param name="payload">Shortcut metadata including id, label, and Base64 icon.</param>
        /// <param name="onResult">Optional per-call result callback.</param>
        /// <exception cref="ArgumentNullException">The payload is null.</exception>
        public void RegisterDirectShareTarget(DirectShareTargetPayload payload, Action<ShareOperationResult>? onResult = null)
        {
            const string op = OperationRegisterDirectShareTarget;
            CheckNotNull(payload, nameof(payload));
            AndroidNativeCallbackQueue.EnsureCreated();
            Debug.Log($"[{LogTag}][{nameof(RegisterDirectShareTarget)}] id: {payload.id}, label: {payload.label}, category: {payload.category ?? "null"}, hasCallback: {onResult != null}");
            SetPendingOperation(op, onResult);
            if (!OnDevice(op)) return;
            if (Rejected(op, AndroidShareCApi.CheckDirectTarget(payload, out byte[]? icon))) return;
            Debug.Log($"[{LogTag}][{nameof(RegisterDirectShareTarget)}] icon: ({icon!.Length} bytes)");
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!NativeReady(op)) return;
            var call = new AndroidShareCApi.ShareCall(op, directTargetId: payload.id);
            CallDirect(call, allocated =>
            {
                AndroidShareCApi.DirectTarget target = AndroidShareCApi.BuildDirectTarget(payload, icon!, allocated);
                return AndroidShareCApi.ntk_share_register_direct_target(ref target);
            });
#endif
        }

        /// <summary>
        /// Removes previously registered Android Direct Share shortcut targets.
        /// </summary>
        /// <param name="payload">Shortcut identifiers to remove.</param>
        /// <param name="onResult">Optional per-call result callback.</param>
        /// <exception cref="ArgumentNullException">The payload or its ID list is null.</exception>
        public void RemoveDirectShareTargets(RemoveDirectShareTargetsPayload payload, Action<ShareOperationResult>? onResult = null)
        {
            const string op = OperationRemoveDirectShareTargets;
            CheckNotNull(payload, nameof(payload));
            CheckNotNull(payload.ids, "payload.ids");
            AndroidNativeCallbackQueue.EnsureCreated();
            Debug.Log($"[{LogTag}][{nameof(RemoveDirectShareTargets)}] ids: {string.Join(", ", payload.ids.Select(id => id ?? "null"))}, hasCallback: {onResult != null}");
            SetPendingOperation(op, onResult);
            if (!OnDevice(op)) return;
            if (Rejected(op, AndroidShareCApi.CheckIds(payload.ids))) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!NativeReady(op)) return;
            CallDirect(new AndroidShareCApi.ShareCall(op), allocated =>
            {
                IntPtr ids = AndroidNativeToolkitCApi.AllocStringArray(payload.ids, allocated);
                return AndroidShareCApi.ntk_share_remove_direct_targets(ids, new UIntPtr((uint)payload.ids.Length));
            });
#endif
        }

        /// <summary>
        /// Shares plain text with an app-selection callback. The chooser launch result is delivered
        /// via <paramref name="onStarted"/> (and <see cref="ShareOperationCompleted"/>). If the user
        /// selects an app, the package name is delivered via <paramref name="onSelected"/> (and
        /// <see cref="ShareCallbackReceived"/>). Cancelling or choosing Copy/Edit does not fire
        /// <paramref name="onSelected"/>.
        /// <para>
        /// Note: <see cref="ShareTextPayload.chooserActions"/> are NOT registered for callbacks
        /// when using this method. Use <see cref="ShareText"/> with an
        /// <c>onChooserAction</c> callback to receive chooser action taps.
        /// </para>
        /// </summary>
        /// <param name="payload">Text content and optional metadata to share.</param>
        /// <param name="onStarted">Optional callback for the chooser launch result.</param>
        /// <param name="onSelected">Optional callback fired when the user selects a target app.</param>
        /// <exception cref="ArgumentNullException">The payload or one of its chooser actions is null.</exception>
        public void ShareWithCallback(
            ShareTextPayload payload,
            Action<ShareOperationResult>? onStarted = null,
            Action<ShareCallbackResult>? onSelected = null)
        {
            const string op = OperationShareWithCallback;
            CheckNotNull(payload, nameof(payload));
            CheckActionsNotNull(payload.chooserActions);
            AndroidNativeCallbackQueue.EnsureCreated();
            Debug.Log($"[{LogTag}][{nameof(ShareWithCallback)}] {Describe(payload)}, hasStartedCallback: {onStarted != null}, hasSelectedCallback: {onSelected != null}");
            if (payload.chooserActions != null && payload.chooserActions.Length > 0)
            {
                Debug.LogWarning($"[{LogTag}] chooser actions are ignored by ShareWithCallback; use ShareText to receive chooser action callbacks");
            }
            lock (s_lock)
            {
                s_pendingOperations[op] = onStarted;
                s_pendingSelected = onSelected;
            }
            if (!OnDevice(op)) return;
            if (Rejected(op, AndroidShareCApi.CheckText(op, payload.text))) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!NativeReady(op)) return;
            EnsureListeners();
            ulong requestId = 0;
            bool accepted = OpenNative(new AndroidShareCApi.ShareCall(op), allocated =>
            {
                AndroidShareCApi.TextContent content = AndroidShareCApi.BuildTextContent(payload, allocated);
                return key => AndroidShareCApi.ntk_share_text_for_selection(ref content, s_donePointer, key, s_releasePointer, out requestId);
            });
            if (accepted)
            {
                Debug.Log($"[{LogTag}][{nameof(ShareWithCallback)}] requestId: {requestId}");
                lock (s_lock) AndroidShareCApi.AddSelectionId(s_selectionIds, requestId);
            }
#endif
        }

        /// <summary>
        /// Cancels a pending share callback receiver registered by <see cref="ShareWithCallback"/>.
        /// </summary>
        /// <param name="onResult">Optional per-call result callback.</param>
        public void CancelPendingShareCallback(Action<ShareOperationResult>? onResult = null)
        {
            const string op = OperationCancelPendingShareCallback;
            lock (s_lock) s_pendingSelected = null;
            AndroidNativeCallbackQueue.EnsureCreated();
            Debug.Log($"[{LogTag}][{nameof(CancelPendingShareCallback)}] hasCallback: {onResult != null}");
            SetPendingOperation(op, onResult);
            if (!OnDevice(op)) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!NativeReady(op)) return;
            ulong[] ids;
            lock (s_lock) ids = AndroidShareCApi.TakeSelectionIds(s_selectionIds);
            CancelSelections(ids);
            PostImmediateResult(op, null);
#endif
        }

        // ── Delivery (J-S2, J-S4, J-S5) ──────────────────────────────────────

        /// <summary>Puts the per-call callback in its operation's slot (last-registered wins; null too).</summary>
        private static void SetPendingOperation(string operation, Action<ShareOperationResult>? callback)
        {
            lock (s_lock) s_pendingOperations[operation] = callback;
        }

        /// <summary>
        /// Takes the operation's per-call callback now (and onSelected for a failed shareWithCallback)
        /// and queues the result with it (J-S5, J-S14). For results decided on the calling thread.
        /// </summary>
        private static void PostImmediateResult(string operation, string? error)
        {
            Action<ShareOperationResult>? callback = TakePending(operation, error == null);
            ShareOperationResult result = error == null
                ? ShareOperationResult.Success(operation)
                : ShareOperationResult.Failure(operation, error);
            AndroidNativeCallbackQueue.Post(() => Deliver(result, callback));
        }

        private static Action<ShareOperationResult>? TakePending(string operation, bool success)
        {
            lock (s_lock)
            {
                s_pendingOperations.TryGetValue(operation, out Action<ShareOperationResult>? callback);
                s_pendingOperations.Remove(operation);
                if (operation == OperationShareWithCallback && !success) s_pendingSelected = null;
                return callback;
            }
        }

        /// <summary>
        /// Raises a result on Unity's main thread: the shared event, then the per-call callback. Goes
        /// to the manager alive when the queue runs; without one, the result is dropped (J-S4).
        /// </summary>
        private static void Deliver(ShareOperationResult result, Action<ShareOperationResult>? callback)
        {
            AndroidShareManager? manager = _instance;
            if (manager == null)
            {
                Debug.Log($"[{LogTag}][{nameof(Deliver)}] no manager; dropping the result of {result.Operation}");
                return;
            }
            Debug.Log($"[{LogTag}][{nameof(Deliver)}] operation: {result.Operation}, isSuccess: {result.IsSuccess}, errorMessage: {result.ErrorMessage ?? "null"}");
            manager.ShareOperationCompleted?.Invoke(result);
            callback?.Invoke(result);
        }

        // ── The completion path, with seams for the Editor tests (J-S1) ───────

        /// <summary>
        /// Calls the library for an opening operation: records the call under a new key just before
        /// the call, passes the key as user_data, and reports a rejection or an exception through
        /// <see cref="PostImmediateResult"/>. Returns whether the library accepted the call; its
        /// completion then comes later, or has already come (5.4).
        /// </summary>
        internal static bool CallNative(AndroidShareCApi.ShareCall call, Func<IntPtr, int> invoke)
        {
            string? error = null;
            long key = 0;
            bool returned = false;
            try
            {
                key = AddCall(call);
                int code = invoke(new IntPtr(key));   // the completion and release may come before this returns
                returned = true;
                if (code != AndroidShareCApi.ErrorNone)
                {
                    error = AndroidShareCApi.FailureMessage(call, code, entry: true);
                    if (AndroidShareCApi.IsRequestDefect(code))
                        Debug.LogError($"[{LogTag}][{nameof(CallNative)}] {error}");
                    else
                        Debug.LogWarning($"[{LogTag}][{nameof(CallNative)}] {error}");
                }
            }
            catch (Exception e)
            {
                error = e.Message;
                Debug.LogError($"[{LogTag}][{nameof(CallNative)}] {call.Operation}: {e}");
                // The library was not entered, so no release will come for the key.
                if (key != 0 && !returned) RemoveCall(key);
            }
            if (error != null) PostImmediateResult(call.Operation, error);
            return error == null;
        }

        internal static long AddCall(AndroidShareCApi.ShareCall call)
        {
            lock (s_lock)
            {
                long key = ++s_nextKey;
                s_calls[key] = call;
                return key;
            }
        }

        private static void RemoveCall(long key)
        {
            lock (s_lock) s_calls.Remove(key);
        }

        /// <summary>
        /// A completion, on the Android main thread: reads the call (release removes it), takes the
        /// per-call callback now (J-S5), and queues the result.
        /// </summary>
        internal static void OnDoneCore(IntPtr userData, int error)
        {
            long key = userData.ToInt64();
            AndroidShareCApi.ShareCall? call;
            lock (s_lock) s_calls.TryGetValue(key, out call);
            if (call == null)
            {
                Debug.LogError($"[{LogTag}][{nameof(OnDoneCore)}] no call for key {key}");
                return;
            }
            Debug.Log($"[{LogTag}][{nameof(OnDoneCore)}] operation: {call.Operation}, error: {AndroidShareCApi.ErrorName(error)}");
            Action<ShareOperationResult>? callback = TakePending(call.Operation, error == AndroidShareCApi.ErrorNone);
            AndroidNativeCallbackQueue.Post(() => RaiseOperation(call, error, callback));
        }

        /// <summary>The release of a call: the library is done with its user_data.</summary>
        internal static void OnReleaseCore(IntPtr userData) => RemoveCall(userData.ToInt64());

        /// <summary>Words a completion on Unity's main thread (the file checks of J-S9 run here) and raises it.</summary>
        private static void RaiseOperation(AndroidShareCApi.ShareCall call, int error, Action<ShareOperationResult>? callback)
        {
            if (error == AndroidShareCApi.ErrorNone)
            {
                Deliver(ShareOperationResult.Success(call.Operation), callback);
                return;
            }
            string message = AndroidShareCApi.FailureMessage(call, error, entry: false);
            Debug.LogWarning($"[{LogTag}][{nameof(RaiseOperation)}] {message}");
            Deliver(ShareOperationResult.Failure(call.Operation, message), callback);
        }

        /// <summary>
        /// A selected app, from the selection listener: raised as it comes, without matching the
        /// request (the library delivers only the current wait's selection; J-S12).
        /// </summary>
        internal static void OnSelectionCore(ulong requestId, string? packageName)
        {
            Debug.Log($"[{LogTag}][{nameof(OnSelectionCore)}] requestId: {requestId}, packageName: {packageName ?? "null"}");
            AndroidNativeCallbackQueue.Post(() =>
            {
                AndroidShareManager? manager = _instance;
                if (manager == null)
                {
                    Debug.Log($"[{LogTag}][{nameof(OnSelectionCore)}] no manager; dropping the selection");
                    return;
                }
                var result = new ShareCallbackResult(OperationShareWithCallback, packageName);
                manager.ShareCallbackReceived?.Invoke(result);
                Action<ShareCallbackResult>? callback;
                lock (s_lock)
                {
                    callback = s_pendingSelected;
                    s_pendingSelected = null;
                }
                callback?.Invoke(result);
            });
        }

        /// <summary>
        /// A tapped chooser action, on the Android main thread: fired through the live manager's
        /// coordinator now, which keeps the per-call callback of this moment (as 1.x's proxy did; K-1).
        /// SEND is not reported, as in 1.x. Without a manager the tap is dropped.
        /// </summary>
        internal static void OnChooserActionCore(string actionId)
        {
            Debug.Log($"[{LogTag}][{nameof(OnChooserActionCore)}] actionId: {actionId}");
            if (!AndroidShareCApi.IsNotifiedActionId(actionId)) return;
            ShareChooserActionCallbackCoordinator? coordinator = s_coordinator;
            if (coordinator == null)
            {
                Debug.Log($"[{LogTag}][{nameof(OnChooserActionCore)}] no manager; dropping the action");
                return;
            }
            coordinator.Fire(new ShareChooserActionResult(actionId));
        }

        // ── Test seams ────────────────────────────────────────────────────────

        /// <summary>The calls whose release has not come yet.</summary>
        internal static int PendingCallCountForTests
        {
            get
            {
                lock (s_lock) return s_calls.Count;
            }
        }

        internal static int SelectionIdCountForTests
        {
            get
            {
                lock (s_lock) return s_selectionIds.Count;
            }
        }

        internal static void AddSelectionIdForTests(ulong id)
        {
            lock (s_lock) AndroidShareCApi.AddSelectionId(s_selectionIds, id);
        }

        internal static void SetPendingOperationForTests(string operation, Action<ShareOperationResult>? callback) =>
            SetPendingOperation(operation, callback);

        internal static void SetPendingSelectedForTests(Action<ShareCallbackResult>? callback)
        {
            lock (s_lock) s_pendingSelected = callback;
        }

        /// <summary>Empties the static state, as at the start of the app (SubsystemRegistration).</summary>
        internal static void ResetStaticStateForTests()
        {
            lock (s_lock)
            {
                s_pendingOperations.Clear();
                s_pendingSelected = null;
                s_calls.Clear();
                s_selectionIds.Clear();
            }
            s_coordinator = _instance != null ? _instance._chooserCoordinator : null;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static void CheckNotNull(object? value, string name)
        {
            if (value == null) throw new ArgumentNullException(name);
        }

        private static void CheckActionsNotNull(ChooserActionPayload[]? actions)
        {
            if (actions == null) return;
            foreach (ChooserActionPayload? action in actions)
            {
                if (action == null) throw new ArgumentNullException("payload.chooserActions", "A chooser action is null.");
            }
        }

        /// <summary>The text and subject by length only (J-S10).</summary>
        private static string Describe(ShareTextPayload payload) =>
            $"text: ({payload.text?.Length ?? 0} characters), title: {payload.title ?? "null"}, subject: ({payload.subject?.Length ?? 0} characters), mimeType: {payload.mimeType ?? "null"}, previewTitle: {payload.previewTitle ?? "null"}, previewThumbnailPath: {payload.previewThumbnailPath ?? "null"}";

        /// <summary>
        /// Off an Android player (the Editor included) every operation fails as 1.x did, whatever the
        /// input (J-S3).
        /// </summary>
        private static bool OnDevice(string operation)
        {
            if (Application.platform == RuntimePlatform.Android) return true;
            Debug.LogWarning($"[{LogTag}] {operation} can only be called on Android.");
            PostImmediateResult(operation, $"{operation} could not be started.");
            return false;
        }

        /// <summary>Reports a failed check. Returns whether it failed.</summary>
        private static bool Rejected(string operation, string? error)
        {
            if (error == null) return false;
            Debug.LogWarning($"[{LogTag}][{operation}] {error}");
            PostImmediateResult(operation, error);
            return true;
        }

        /// <summary>ShareImages and ShareFiles: the same checks and call, with their own operation.</summary>
        private static void SharePaths(string op, string[] filePaths, Action<ShareOperationResult>? onResult, string method)
        {
            CheckNotNull(filePaths, "payload.filePaths");
            AndroidNativeCallbackQueue.EnsureCreated();
            Debug.Log($"[{LogTag}][{method}] filePaths: {string.Join(", ", filePaths.Select(p => p ?? "null"))}, hasCallback: {onResult != null}");
            SetPendingOperation(op, onResult);
            if (!OnDevice(op)) return;
            if (Rejected(op, AndroidShareCApi.CheckPaths(op, filePaths))) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!NativeReady(op)) return;
            OpenNative(new AndroidShareCApi.ShareCall(op, filePaths), allocated =>
            {
                IntPtr paths = AndroidNativeToolkitCApi.AllocStringArray(filePaths, allocated);
                var count = new UIntPtr((uint)filePaths.Length);
                return key => op == OperationShareImages
                    ? AndroidShareCApi.ntk_share_images(paths, count, s_donePointer, key, s_releasePointer)
                    : AndroidShareCApi.ntk_share_files(paths, count, s_donePointer, key, s_releasePointer);
            });
#endif
        }

        private static void CancelSelections(ulong[] ids)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (ids.Length == 0 || AndroidNativeToolkitCApi.EnsureNativeAvailable() != AndroidNativeToolkitCApi.NativeState.Available) return;
            foreach (ulong id in ids)
            {
                try
                {
                    int code = AndroidShareCApi.ntk_share_cancel_selection(id);
                    Debug.Log($"[{LogTag}][{nameof(CancelSelections)}] requestId: {id}, result: {AndroidShareCApi.ErrorName(code)}");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[{LogTag}][{nameof(CancelSelections)}] {e}");
                }
            }
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        // The callbacks, kept alive for the process and handed to the library as pointers.
        private static readonly AndroidShareCApi.DoneCallback s_done = OnDone;
        private static readonly AndroidShareCApi.ReleaseCallback s_release = OnRelease;
        private static readonly AndroidShareCApi.ChooserActionCallback s_chooserAction = OnChooserAction;
        private static readonly AndroidShareCApi.SelectionCallback s_selection = OnSelection;
        private static readonly IntPtr s_donePointer = Marshal.GetFunctionPointerForDelegate(s_done);
        private static readonly IntPtr s_releasePointer = Marshal.GetFunctionPointerForDelegate(s_release);
        private static readonly IntPtr s_chooserActionPointer = Marshal.GetFunctionPointerForDelegate(s_chooserAction);
        private static readonly IntPtr s_selectionPointer = Marshal.GetFunctionPointerForDelegate(s_selection);

        /// <summary>Whether the native library can be used; when not, reports the failure (4.4).</summary>
        private static bool NativeReady(string operation)
        {
            if (AndroidNativeToolkitCApi.EnsureNativeAvailable() == AndroidNativeToolkitCApi.NativeState.Available) return true;
            string error = AndroidShareCApi.NativeUnavailableMessage(operation);
            Debug.LogWarning($"[{LogTag}][{operation}] {error}");
            PostImmediateResult(operation, error);
            return false;
        }

        /// <summary>
        /// Builds the native arguments and calls the library through <see cref="CallNative"/>, freeing
        /// what was allocated afterwards. A C# exception while building reports the failure.
        /// </summary>
        private static bool OpenNative(AndroidShareCApi.ShareCall call, Func<List<IntPtr>, Func<IntPtr, int>> build)
        {
            var allocated = new List<IntPtr>();
            try
            {
                Func<IntPtr, int> invoke;
                try
                {
                    invoke = build(allocated);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[{LogTag}][{nameof(OpenNative)}] {call.Operation}: {e}");
                    PostImmediateResult(call.Operation, e.Message);
                    return false;
                }
                return CallNative(call, invoke);
            }
            finally
            {
                AndroidNativeToolkitCApi.FreeAll(allocated);
            }
        }

        /// <summary>A Direct Share call: synchronous, its return value is the result (4.3).</summary>
        private static void CallDirect(AndroidShareCApi.ShareCall call, Func<List<IntPtr>, int> invoke)
        {
            var allocated = new List<IntPtr>();
            string? error = null;
            try
            {
                int code = invoke(allocated);
                if (code != AndroidShareCApi.ErrorNone)
                {
                    error = AndroidShareCApi.FailureMessage(call, code, entry: false);
                    Debug.LogWarning($"[{LogTag}][{call.Operation}] {error}");
                }
            }
            catch (Exception e)
            {
                error = e.Message;
                Debug.LogError($"[{LogTag}][{call.Operation}] {e}");
            }
            finally
            {
                AndroidNativeToolkitCApi.FreeAll(allocated);
            }
            PostImmediateResult(call.Operation, error);
        }

        /// <summary>Registers the two listeners once; a failed one is tried again on the next call (J-S6).</summary>
        private static void EnsureListeners()
        {
            try
            {
                AddListeners();
            }
            catch (Exception e)
            {
                // A failed registration (an error code) is a warning and is tried again; an exception
                // is not expected, so it is an error, but it too is tried again on the next call.
                Debug.LogError($"[{LogTag}][{nameof(EnsureListeners)}] {e}");
            }
        }

        private static void AddListeners()
        {
            lock (s_listenerLock)
            {
                if (!s_chooserListenerAdded)
                {
                    int code = AndroidShareCApi.ntk_share_add_chooser_action_listener(s_chooserActionPointer, IntPtr.Zero, IntPtr.Zero, out IntPtr _);
                    s_chooserListenerAdded = code == AndroidShareCApi.ErrorNone;
                    if (!s_chooserListenerAdded)
                        Debug.LogWarning($"[{LogTag}][{nameof(EnsureListeners)}] the chooser action listener was not added: {AndroidShareCApi.ErrorName(code)}");
                }
                if (!s_selectionListenerAdded)
                {
                    int code = AndroidShareCApi.ntk_share_add_selection_listener(s_selectionPointer, IntPtr.Zero, IntPtr.Zero, out IntPtr _);
                    s_selectionListenerAdded = code == AndroidShareCApi.ErrorNone;
                    if (!s_selectionListenerAdded)
                        Debug.LogWarning($"[{LogTag}][{nameof(EnsureListeners)}] the selection listener was not added: {AndroidShareCApi.ErrorName(code)}");
                }
            }
        }

        // The native callbacks, on the Android main thread (or, for a release of a rejected call, the
        // calling thread). They copy what they receive, free the handles, and let no exception reach
        // the library, where it would end the process.

        [MonoPInvokeCallback(typeof(AndroidShareCApi.DoneCallback))]
        private static void OnDone(IntPtr userData, int error, uint systemCode)
        {
            try
            {
                OnDoneCore(userData, error);
            }
            catch (Exception e)
            {
                Debug.LogError($"[{LogTag}][{nameof(OnDone)}] {e}");
            }
        }

        [MonoPInvokeCallback(typeof(AndroidShareCApi.ReleaseCallback))]
        private static void OnRelease(IntPtr userData)
        {
            try
            {
                OnReleaseCore(userData);
            }
            catch (Exception e)
            {
                Debug.LogError($"[{LogTag}][{nameof(OnRelease)}] {e}");
            }
        }

        [MonoPInvokeCallback(typeof(AndroidShareCApi.ChooserActionCallback))]
        private static void OnChooserAction(IntPtr userData, IntPtr actionId)
        {
            try
            {
                string id;
                try
                {
                    id = AndroidNativeToolkitCApi.ReadString(actionId);
                }
                finally
                {
                    AndroidNativeToolkitCApi.ntk_string_free(actionId);
                }
                OnChooserActionCore(id);
            }
            catch (Exception e)
            {
                Debug.LogError($"[{LogTag}][{nameof(OnChooserAction)}] {e}");
            }
        }

        [MonoPInvokeCallback(typeof(AndroidShareCApi.SelectionCallback))]
        private static void OnSelection(IntPtr userData, ulong requestId, IntPtr packageName)
        {
            try
            {
                string? package = null;
                if (packageName != IntPtr.Zero)
                {
                    try
                    {
                        package = AndroidNativeToolkitCApi.ReadString(packageName);
                    }
                    finally
                    {
                        AndroidNativeToolkitCApi.ntk_string_free(packageName);
                    }
                }
                OnSelectionCore(requestId, package);
            }
            catch (Exception e)
            {
                Debug.LogError($"[{LogTag}][{nameof(OnSelection)}] {e}");
            }
        }
#endif
    }
}
#endif
