#nullable enable

#if UNITY_ANDROID
namespace JonghyunKim.NativeToolkit.Runtime.Clipboard
{
    using System;
    using System.Collections.Generic;
    using JonghyunKim.NativeToolkit.Runtime.Common;
    using UnityEngine;

    /// <summary>
    /// Singleton manager for Android native clipboard operations.
    /// Wraps <c>UnityAndroidClipboardManager</c> (Kotlin) via <c>AndroidJavaObject</c> calls.
    ///
    /// Read/HasClip/GetDescription are synchronous and return their result directly, matching the
    /// native layer which reports them the same way. Copy/Clear/StopObserving are asynchronous and
    /// report results through the <see cref="ClipboardOperationCompleted"/> event and an optional
    /// per-call callback. StartObserving reports no result at all (see its own documentation).
    /// </summary>
    public class AndroidClipboardManager : MonoBehaviour
    {
        private const string PluginClassName = "android.unity.clipboard.UnityAndroidClipboardManager";
        private const string LogTag = "AndroidClipboardManager";
        private const string BridgeUnavailableErrorCode = "CLIPBOARD_BRIDGE_UNAVAILABLE";

        /// <summary>Native operation name for copying plain text.</summary>
        public const string OperationCopyPlainText = "copyPlainText";

        /// <summary>Native operation name for copying HTML text.</summary>
        public const string OperationCopyHtmlText = "copyHtmlText";

        /// <summary>Native operation name for copying a URI.</summary>
        public const string OperationCopyUri = "copyUri";

        /// <summary>Native operation name for copying multiple plain-text items.</summary>
        public const string OperationCopyMultipleText = "copyMultipleText";

        /// <summary>Native operation name for clearing the clipboard.</summary>
        public const string OperationClear = "clear";

        /// <summary>Native operation name for stopping clipboard change observation.</summary>
        public const string OperationStopObserving = "stopObserving";

        // C#-only constant: the native layer has no OPERATION_* counterpart for startObserving
        // because it reports no result. Used for the native method name and log messages, never as
        // a callback key.
        private const string OperationStartObserving = "startObserving";

        private const string OperationRead = "read";
        private const string OperationHasClip = "hasClip";
        private const string OperationGetDescription = "getDescription";

        private static AndroidClipboardManager? _instance;
        private AndroidJavaObject? pluginInstance;
        private ClipboardOperationListenerProxy? operationListener;
        private ClipboardChangeListenerProxy? changeListener;

        // last-registered wins for same-operation concurrent calls (matches AndroidShareManager pattern)
        private readonly Dictionary<string, Action<ClipboardOperationResult>?> _pendingOperationCallbacks = new();

        /// <summary>
        /// Raised when a copy, clear, or stopObserving operation completes, on both success and
        /// failure. Always invoked before the per-call callback.
        /// </summary>
        public event Action<ClipboardOperationResult>? ClipboardOperationCompleted;

        /// <summary>
        /// Raised when the clipboard content changes while observing. Never raised before
        /// <see cref="StartObserving"/> is called.
        /// </summary>
        public event Action? ClipboardChanged;

        /// <summary>
        /// Singleton instance. Creates a new <see cref="AndroidClipboardManager"/> GameObject if
        /// none exists and ensures it persists across scene loads.
        /// </summary>
        public static AndroidClipboardManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    Debug.Log($"[{LogTag}] Creating new instance");
                    GameObject singletonObject = new GameObject("AndroidClipboardManager");
                    _instance = singletonObject.AddComponent<AndroidClipboardManager>();
                }
                return _instance;
            }
        }

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
            Initialize();
        }

        private void OnDestroy()
        {
            Debug.Log($"[{LogTag}][{nameof(OnDestroy)}]");
            if (_instance == this)
            {
                ClearClipboardChangeListener();
                ClearClipboardOperationListener();
                pluginInstance?.Dispose();
                pluginInstance = null;
                _pendingOperationCallbacks.Clear();
                _instance = null;
            }
        }

        /// <summary>
        /// Initializes the native Android plugin interface. No-op on non-Android platforms.
        /// Called automatically from <c>Awake</c>.
        /// </summary>
        public void Initialize()
        {
            Debug.Log($"[{LogTag}][{nameof(Initialize)}]");
            if (Application.platform != RuntimePlatform.Android)
            {
                Debug.Log($"[{LogTag}] Not running on Android. Skipping initialization.");
                return;
            }

            using (AndroidJavaClass pluginClass = new AndroidJavaClass(PluginClassName))
            {
                pluginInstance = pluginClass.CallStatic<AndroidJavaObject>("getInstance");
                if (pluginInstance == null)
                {
                    Debug.LogError($"[{LogTag}] Failed to initialize pluginInstance.");
                    return;
                }

                operationListener ??= new ClipboardOperationListenerProxy(this);
                pluginInstance.Call("setClipboardOperationListener", operationListener);

                changeListener ??= new ClipboardChangeListenerProxy(this);
                pluginInstance.Call("setClipboardChangeListener", changeListener);

                Debug.Log($"[{LogTag}] pluginInstance initialized successfully.");
            }
        }

        // ---- Async: copy / clear / stopObserving ----

        /// <summary>
        /// Copies plain text to the clipboard. A blank <see cref="CopyPlainTextPayload.text"/> is
        /// accepted and succeeds; it is not treated as an error.
        /// </summary>
        /// <param name="payload">Plain text content and optional metadata to copy.</param>
        /// <param name="onResult">Optional per-call callback invoked with the result. The global
        /// <see cref="ClipboardOperationCompleted"/> event always fires first, regardless of this
        /// parameter.</param>
        public void CopyPlainText(CopyPlainTextPayload payload, Action<ClipboardOperationResult>? onResult = null)
        {
            // Log lengths and flags only: clipboard content may hold passwords or tokens.
            // This intentionally deviates from the "log every parameter" rule in csharp.md, and
            // matches the native-side masking in UnityAndroidClipboardManager.maskJson.
            Debug.Log($"[{LogTag}][{nameof(CopyPlainText)}] textLength: {payload.text?.Length ?? 0}, " +
                      $"hasLabel: {!string.IsNullOrWhiteSpace(payload.label)}, isSensitive: {payload.isSensitive}, " +
                      $"hasCallback: {onResult != null}");
            string json = AndroidClipboardJsonBuilder.BuildCopyPlainTextJson(payload);
            CallOperation(OperationCopyPlainText, json, requiresActivity: true, onResult);
        }

        /// <summary>
        /// Copies HTML text to the clipboard. A blank <see cref="CopyHtmlTextPayload.htmlText"/>
        /// fails with the CLIPBOARD_EMPTY_CONTENT error.
        /// </summary>
        /// <param name="payload">HTML text content and optional metadata to copy.</param>
        /// <param name="onResult">Optional per-call result callback.</param>
        public void CopyHtmlText(CopyHtmlTextPayload payload, Action<ClipboardOperationResult>? onResult = null)
        {
            Debug.Log($"[{LogTag}][{nameof(CopyHtmlText)}] plainTextLength: {payload.plainText?.Length ?? 0}, " +
                      $"htmlTextLength: {payload.htmlText?.Length ?? 0}, hasLabel: {!string.IsNullOrWhiteSpace(payload.label)}, " +
                      $"isSensitive: {payload.isSensitive}, hasCallback: {onResult != null}");
            string json = AndroidClipboardJsonBuilder.BuildCopyHtmlTextJson(payload);
            CallOperation(OperationCopyHtmlText, json, requiresActivity: true, onResult);
        }

        /// <summary>
        /// Copies a URI (content:// scheme, including image/file references) to the clipboard.
        /// </summary>
        /// <param name="payload">URI content and optional metadata to copy.</param>
        /// <param name="onResult">Optional per-call result callback.</param>
        public void CopyUri(CopyUriPayload payload, Action<ClipboardOperationResult>? onResult = null)
        {
            Debug.Log($"[{LogTag}][{nameof(CopyUri)}] hasUri: {!string.IsNullOrEmpty(payload.uri)}, " +
                      $"hasLabel: {!string.IsNullOrWhiteSpace(payload.label)}, isSensitive: {payload.isSensitive}, " +
                      $"hasCallback: {onResult != null}");
            string json = AndroidClipboardJsonBuilder.BuildCopyUriJson(payload);
            CallOperation(OperationCopyUri, json, requiresActivity: true, onResult);
        }

        /// <summary>
        /// Copies multiple plain-text items (same form) to the clipboard. Individual empty strings
        /// inside <see cref="CopyMultipleTextPayload.texts"/> are accepted; only an empty array fails.
        /// </summary>
        /// <param name="payload">Text items and optional metadata to copy.</param>
        /// <param name="onResult">Optional per-call result callback.</param>
        public void CopyMultipleText(CopyMultipleTextPayload payload, Action<ClipboardOperationResult>? onResult = null)
        {
            Debug.Log($"[{LogTag}][{nameof(CopyMultipleText)}] itemCount: {payload.texts?.Length ?? 0}, " +
                      $"hasLabel: {!string.IsNullOrWhiteSpace(payload.label)}, isSensitive: {payload.isSensitive}, " +
                      $"hasCallback: {onResult != null}");
            string json = AndroidClipboardJsonBuilder.BuildCopyMultipleTextJson(payload);
            CallOperation(OperationCopyMultipleText, json, requiresActivity: true, onResult);
        }

        /// <summary>
        /// Clears the clipboard.
        /// </summary>
        /// <param name="onResult">Optional per-call result callback.</param>
        public void Clear(Action<ClipboardOperationResult>? onResult = null)
        {
            Debug.Log($"[{LogTag}][{nameof(Clear)}] hasCallback: {onResult != null}");
            CallOperation(OperationClear, null, requiresActivity: true, onResult);
        }

        /// <summary>
        /// Stops observing clipboard changes.
        /// </summary>
        /// <param name="onResult">Optional per-call result callback.</param>
        public void StopObserving(Action<ClipboardOperationResult>? onResult = null)
        {
            Debug.Log($"[{LogTag}][{nameof(StopObserving)}] hasCallback: {onResult != null}");
            CallOperation(OperationStopObserving, null, requiresActivity: false, onResult);
        }

        // ---- Sync: read / hasClip / getDescription ----

        /// <summary>
        /// Reads the current clipboard content. Synchronous: does not raise
        /// <see cref="ClipboardOperationCompleted"/>. Must be called from the Unity main thread.
        /// </summary>
        /// <returns>The clipboard content, an empty result, or a failure with an error code.</returns>
        public ClipboardReadResult Read()
        {
            Debug.Log($"[{LogTag}][{nameof(Read)}]");
            if (!TryPrepareCall(OperationRead, null, requiresActivity: true, out object?[] fullArgs, out AndroidJavaObject? activity))
            {
                return ClipboardReadResult.Failed(BridgeUnavailableErrorCode, $"{OperationRead} could not be started.");
            }

            using (activity)
            {
                try
                {
                    string raw = pluginInstance!.Call<string>(OperationRead, fullArgs);
                    var result = AndroidClipboardJsonParser.ParseReadResult(raw);
                    Debug.Log($"[{LogTag}][{nameof(Read)}] status: {result.Status}, errorCode: {result.ErrorCode}");
                    return result;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[{LogTag}] {OperationRead} error: {ex.Message}");
                    return ClipboardReadResult.Failed(BridgeUnavailableErrorCode, ex.Message);
                }
            }
        }

        /// <summary>
        /// Returns whether the clipboard currently holds data. Synchronous: does not raise
        /// <see cref="ClipboardOperationCompleted"/>. Must be called from the Unity main thread.
        /// </summary>
        /// <returns><c>true</c> if the clipboard holds data. Returns <c>false</c> if the check could
        /// not be performed; this is indistinguishable from an empty clipboard.</returns>
        public bool HasClip()
        {
            Debug.Log($"[{LogTag}][{nameof(HasClip)}]");
            if (!TryPrepareCall(OperationHasClip, null, requiresActivity: true, out object?[] fullArgs, out AndroidJavaObject? activity))
            {
                Debug.LogWarning($"[{LogTag}] {OperationHasClip} could not be started.");
                return false;
            }

            using (activity)
            {
                try
                {
                    string raw = pluginInstance!.Call<string>(OperationHasClip, fullArgs);
                    return AndroidClipboardJsonParser.ParseHasClip(raw);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[{LogTag}] {OperationHasClip} error: {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>
        /// Reads clipboard metadata without touching the clip body. Synchronous: does not raise
        /// <see cref="ClipboardOperationCompleted"/>. Must be called from the Unity main thread.
        /// </summary>
        /// <returns>The clipboard metadata, an empty result, or a failure with an error code.</returns>
        public ClipboardDescriptionResult GetDescription()
        {
            Debug.Log($"[{LogTag}][{nameof(GetDescription)}]");
            if (!TryPrepareCall(OperationGetDescription, null, requiresActivity: true, out object?[] fullArgs, out AndroidJavaObject? activity))
            {
                return ClipboardDescriptionResult.Failed(BridgeUnavailableErrorCode, $"{OperationGetDescription} could not be started.");
            }

            using (activity)
            {
                try
                {
                    string raw = pluginInstance!.Call<string>(OperationGetDescription, fullArgs);
                    var result = AndroidClipboardJsonParser.ParseDescriptionResult(raw);
                    Debug.Log($"[{LogTag}][{nameof(GetDescription)}] status: {result.Status}, errorCode: {result.ErrorCode}");
                    return result;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[{LogTag}] {OperationGetDescription} error: {ex.Message}");
                    return ClipboardDescriptionResult.Failed(BridgeUnavailableErrorCode, ex.Message);
                }
            }
        }

        // ---- Observation ----

        /// <summary>
        /// Starts observing clipboard changes. Reports no result: the native startObserving does
        /// not notify the operation listener. Changes are delivered through
        /// <see cref="ClipboardChanged"/>. A second call while already observing is a no-op on the
        /// native side. Observation is only reliable while the app is in the foreground (Android
        /// 10+ restriction). If the native layer cannot obtain the system ClipboardManager,
        /// observation silently does not start; this cannot be detected from C#.
        /// </summary>
        public void StartObserving()
        {
            Debug.Log($"[{LogTag}][{nameof(StartObserving)}]");

            // startObserving reports no result through the operation listener, so failures are
            // logged and swallowed here. Nothing is raised on ClipboardOperationCompleted or
            // ClipboardChanged.
            if (Application.platform != RuntimePlatform.Android)
            {
                Debug.LogWarning($"[{LogTag}] {OperationStartObserving} can only be called on Android.");
                return;
            }

            if (pluginInstance == null)
            {
                Debug.LogError($"[{LogTag}] {OperationStartObserving} failed: pluginInstance is null.");
                return;
            }

            using (AndroidJavaObject? activity = GetCurrentActivity())
            {
                if (activity == null)
                {
                    Debug.LogError($"[{LogTag}] {OperationStartObserving} failed: currentActivity is null.");
                    return;
                }

                try
                {
                    pluginInstance.Call(OperationStartObserving, activity);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[{LogTag}] {OperationStartObserving} error: {ex.Message}");
                }
            }
        }

        // ---- Shared call machinery ----

        // requiresActivity == false is used only by stopObserving, whose native signature takes no Context.
        // On success, `activity` is non-null iff requiresActivity is true; the caller disposes it either way.
        private bool TryPrepareCall(
            string methodName,
            string? json,
            bool requiresActivity,
            out object?[] fullArgs,
            out AndroidJavaObject? activity)
        {
            fullArgs = Array.Empty<object?>();
            activity = null;

            if (Application.platform != RuntimePlatform.Android)
            {
                Debug.LogWarning($"[{LogTag}] {methodName} can only be called on Android.");
                return false;
            }

            if (pluginInstance == null)
            {
                Debug.LogError($"[{LogTag}] {methodName} failed: pluginInstance is null.");
                return false;
            }

            if (!requiresActivity)
            {
                // json is always null on this path; stopObserving takes no arguments at all.
                fullArgs = Array.Empty<object?>();
                return true;
            }

            activity = GetCurrentActivity();
            if (activity == null)
            {
                Debug.LogError($"[{LogTag}] {methodName} failed: currentActivity is null.");
                return false;
            }

            fullArgs = json != null ? new object?[] { activity, json } : new object?[] { activity };
            return true;
        }

        private void CallOperation(
            string operationName,
            string? json,
            bool requiresActivity,
            Action<ClipboardOperationResult>? onResult)
        {
            _pendingOperationCallbacks[operationName] = onResult;

            if (!TryPrepareCall(operationName, json, requiresActivity, out object?[] fullArgs, out AndroidJavaObject? activity))
            {
                FireOperationResult(ClipboardOperationResult.Failure(operationName, $"{operationName} could not be started."));
                return;
            }

            // `activity` is null when requiresActivity is false; `using (null)` is a no-op in C#.
            using (activity)
            {
                try
                {
                    pluginInstance!.Call(operationName, fullArgs);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[{LogTag}] {operationName} error: {ex.Message}");
                    FireOperationResult(ClipboardOperationResult.Failure(operationName, ex.Message));
                }
            }
        }

        private AndroidJavaObject? GetCurrentActivity()
        {
            try
            {
                using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                {
                    return unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{LogTag}] Failed to get currentActivity: {ex.Message}");
                return null;
            }
        }

        private void FireOperationResult(ClipboardOperationResult result)
        {
            // Snapshot the callback before dispatching so a subsequent call to the same operation
            // cannot cause this result to invoke a different callback.
            _pendingOperationCallbacks.TryGetValue(result.Operation, out var cb);
            _pendingOperationCallbacks.Remove(result.Operation);
            UnityMainThreadDispatcher.Instance.Enqueue(() => InvokeInOrder(result, ClipboardOperationCompleted, cb));
        }

        /// <summary>
        /// Invokes the common event followed by the per-call callback, swallowing any exception
        /// raised by either. Extracted as a pure, Unity-lifecycle-independent helper so dispatch
        /// order and exception handling can be verified directly from EditMode tests.
        /// </summary>
        /// <param name="result">Result to dispatch.</param>
        /// <param name="common">Common event snapshot (invoked first).</param>
        /// <param name="perCall">Per-call callback snapshot (invoked second).</param>
        internal static void InvokeInOrder(
            ClipboardOperationResult result,
            Action<ClipboardOperationResult>? common,
            Action<ClipboardOperationResult>? perCall)
        {
            // Separate try/catch blocks: an exception from `common` must not prevent `perCall`
            // from being invoked, and vice versa.
            try
            {
                common?.Invoke(result);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{LogTag}][{nameof(InvokeInOrder)}] {ex.Message}");
            }

            try
            {
                perCall?.Invoke(result);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{LogTag}][{nameof(InvokeInOrder)}] {ex.Message}");
            }
        }

        private void FireClipboardChanged()
        {
            UnityMainThreadDispatcher.Instance.Enqueue(() =>
            {
                try
                {
                    ClipboardChanged?.Invoke();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[{LogTag}][{nameof(FireClipboardChanged)}] {ex.Message}");
                }
            });
        }

        private void ClearClipboardOperationListener()
        {
            if (pluginInstance == null)
            {
                return;
            }

            try
            {
                pluginInstance.Call("clearClipboardOperationListener");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[{LogTag}] clearClipboardOperationListener failed: {ex.Message}");
            }
        }

        private void ClearClipboardChangeListener()
        {
            if (pluginInstance == null)
            {
                return;
            }

            try
            {
                pluginInstance.Call("clearClipboardChangeListener");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[{LogTag}] clearClipboardChangeListener failed: {ex.Message}");
            }
        }

        private sealed class ClipboardOperationListenerProxy : AndroidJavaProxy
        {
            private readonly AndroidClipboardManager owner;

            public ClipboardOperationListenerProxy(AndroidClipboardManager owner)
                : base("android.unity.clipboard.UnityAndroidClipboardManager$ClipboardOperationListener")
            {
                this.owner = owner;
            }

            // Must be public so IL2CPP can resolve the call from Java.
            // Method name and signature must exactly match the native interface.
            public void onClipboardOperation(string operation, bool isSuccessful, string? errorMessage)
            {
                ClipboardOperationResult result = isSuccessful
                    ? ClipboardOperationResult.Success(operation)
                    : ClipboardOperationResult.Failure(operation, errorMessage ?? string.Empty);
                owner.FireOperationResult(result);
            }
        }

        private sealed class ClipboardChangeListenerProxy : AndroidJavaProxy
        {
            private readonly AndroidClipboardManager owner;

            public ClipboardChangeListenerProxy(AndroidClipboardManager owner)
                : base("android.unity.clipboard.UnityAndroidClipboardManager$ClipboardChangeListener")
            {
                this.owner = owner;
            }

            // Must be public so IL2CPP can resolve the call from Java.
            public void onClipboardChanged()
            {
                owner.FireClipboardChanged();
            }
        }
    }
}
#endif
