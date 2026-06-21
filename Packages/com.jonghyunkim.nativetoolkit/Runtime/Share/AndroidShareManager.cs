#nullable enable

#if UNITY_ANDROID
namespace JonghyunKim.NativeToolkit.Runtime.Share
{
    using System;
    using System.Collections.Generic;
    using JonghyunKim.NativeToolkit.Runtime.Common;
    using UnityEngine;

    /// <summary>
    /// Singleton manager for Android native share operations.
    /// Wraps <c>UnityAndroidShareManager</c> (Kotlin) via <c>AndroidJavaObject</c> calls.
    /// All share APIs accept JSON strings built by <c>AndroidShareJsonBuilder</c>.
    /// </summary>
    public class AndroidShareManager : MonoBehaviour
    {
        private const string PluginClassName = "android.unity.share.UnityAndroidShareManager";
        private const string LogTag = "AndroidShareManager";

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
        private AndroidJavaObject? pluginInstance;
        private ShareOperationListenerProxy? operationListener;

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

        // last-registered wins for same-operation concurrent calls (matches IosNotificationManager pattern)
        private readonly Dictionary<string, Action<ShareOperationResult>?> _pendingOperationCallbacks = new();
        private Action<ShareCallbackResult>? _pendingShareSelectedCallback;

        /// <summary>
        /// Singleton instance. Creates a new <see cref="AndroidShareManager"/> GameObject if none
        /// exists and ensures it persists across scene loads.
        /// </summary>
        public static AndroidShareManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    Debug.Log($"[{LogTag}] Creating new instance");
                    GameObject singletonObject = new GameObject("AndroidShareManager");
                    _instance = singletonObject.AddComponent<AndroidShareManager>();
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
                ClearShareOperationListener();
                pluginInstance?.Dispose();
                pluginInstance = null;
                _pendingOperationCallbacks.Clear();
                _pendingShareSelectedCallback = null;
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

                operationListener ??= new ShareOperationListenerProxy(this);
                pluginInstance.Call("setShareOperationListener", operationListener);
                Debug.Log($"[{LogTag}] pluginInstance initialized successfully.");
            }
        }

        /// <summary>
        /// Shares plain text via the Android share sheet.
        /// </summary>
        /// <param name="payload">Text content and optional metadata to share.</param>
        /// <param name="onResult">Optional callback invoked with the launch result. The global
        /// <see cref="ShareOperationCompleted"/> event always fires regardless of this parameter.</param>
        public void ShareText(ShareTextPayload payload, Action<ShareOperationResult>? onResult = null)
        {
            string json = AndroidShareJsonBuilder.BuildShareTextJson(payload);
            Debug.Log($"[{LogTag}][{nameof(ShareText)}] json: {json}, hasCallback: {onResult != null}");
            CallOperation(OperationShareText, json, onResult);
        }

        /// <summary>
        /// Shares a single image file via the Android share sheet.
        /// The file must reside in a directory exposed by the native-toolkit FileProvider.
        /// </summary>
        /// <param name="payload">Image file path and optional MIME type.</param>
        /// <param name="onResult">Optional per-call result callback.</param>
        public void ShareImage(ShareImagePayload payload, Action<ShareOperationResult>? onResult = null)
        {
            string json = AndroidShareJsonBuilder.BuildShareImageJson(payload);
            Debug.Log($"[{LogTag}][{nameof(ShareImage)}] json: {json}, hasCallback: {onResult != null}");
            CallOperation(OperationShareImage, json, onResult);
        }

        /// <summary>
        /// Shares multiple image files via the Android share sheet.
        /// Each file must reside in a directory exposed by the native-toolkit FileProvider.
        /// </summary>
        /// <param name="payload">Image file paths.</param>
        /// <param name="onResult">Optional per-call result callback.</param>
        public void ShareImages(ShareImagesPayload payload, Action<ShareOperationResult>? onResult = null)
        {
            string json = AndroidShareJsonBuilder.BuildShareImagesJson(payload);
            Debug.Log($"[{LogTag}][{nameof(ShareImages)}] json: {json}, hasCallback: {onResult != null}");
            CallOperation(OperationShareImages, json, onResult);
        }

        /// <summary>
        /// Shares a single arbitrary file via the Android share sheet.
        /// The file must reside in a directory exposed by the native-toolkit FileProvider.
        /// </summary>
        /// <param name="payload">File path to share.</param>
        /// <param name="onResult">Optional per-call result callback.</param>
        public void ShareFile(ShareFilePayload payload, Action<ShareOperationResult>? onResult = null)
        {
            string json = AndroidShareJsonBuilder.BuildShareFileJson(payload);
            Debug.Log($"[{LogTag}][{nameof(ShareFile)}] json: {json}, hasCallback: {onResult != null}");
            CallOperation(OperationShareFile, json, onResult);
        }

        /// <summary>
        /// Shares multiple arbitrary files via the Android share sheet.
        /// Each file must reside in a directory exposed by the native-toolkit FileProvider.
        /// </summary>
        /// <param name="payload">File paths to share.</param>
        /// <param name="onResult">Optional per-call result callback.</param>
        public void ShareFiles(ShareFilesPayload payload, Action<ShareOperationResult>? onResult = null)
        {
            string json = AndroidShareJsonBuilder.BuildShareFilesJson(payload);
            Debug.Log($"[{LogTag}][{nameof(ShareFiles)}] json: {json}, hasCallback: {onResult != null}");
            CallOperation(OperationShareFiles, json, onResult);
        }

        /// <summary>
        /// Registers an Android Direct Share shortcut target.
        /// </summary>
        /// <param name="payload">Shortcut metadata including id, label, and Base64 icon.</param>
        /// <param name="onResult">Optional per-call result callback.</param>
        public void RegisterDirectShareTarget(DirectShareTargetPayload payload, Action<ShareOperationResult>? onResult = null)
        {
            string json = AndroidShareJsonBuilder.BuildDirectShareTargetJson(payload);
            Debug.Log($"[{LogTag}][{nameof(RegisterDirectShareTarget)}] json: {json}, hasCallback: {onResult != null}");
            CallOperation(OperationRegisterDirectShareTarget, json, onResult);
        }

        /// <summary>
        /// Removes previously registered Android Direct Share shortcut targets.
        /// </summary>
        /// <param name="payload">Shortcut identifiers to remove.</param>
        /// <param name="onResult">Optional per-call result callback.</param>
        public void RemoveDirectShareTargets(RemoveDirectShareTargetsPayload payload, Action<ShareOperationResult>? onResult = null)
        {
            string json = AndroidShareJsonBuilder.BuildRemoveDirectShareTargetsJson(payload);
            Debug.Log($"[{LogTag}][{nameof(RemoveDirectShareTargets)}] json: {json}, hasCallback: {onResult != null}");
            CallOperation(OperationRemoveDirectShareTargets, json, onResult);
        }

        /// <summary>
        /// Shares plain text with an app-selection callback. The chooser launch result is delivered
        /// via <paramref name="onStarted"/> (and <see cref="ShareOperationCompleted"/>). If the user
        /// selects an app, the package name is delivered via <paramref name="onSelected"/> (and
        /// <see cref="ShareCallbackReceived"/>). Cancelling or choosing Copy/Edit does not fire
        /// <paramref name="onSelected"/>.
        /// </summary>
        /// <param name="payload">Text content and optional metadata to share.</param>
        /// <param name="onStarted">Optional callback for the chooser launch result.</param>
        /// <param name="onSelected">Optional callback fired when the user selects a target app.</param>
        public void ShareWithCallback(
            ShareTextPayload payload,
            Action<ShareOperationResult>? onStarted = null,
            Action<ShareCallbackResult>? onSelected = null)
        {
            string json = AndroidShareJsonBuilder.BuildShareTextJson(payload);
            Debug.Log($"[{LogTag}][{nameof(ShareWithCallback)}] json: {json}, hasStartedCallback: {onStarted != null}, hasSelectedCallback: {onSelected != null}");
            _pendingShareSelectedCallback = onSelected;
            CallOperation(OperationShareWithCallback, json, onStarted);
        }

        /// <summary>
        /// Cancels a pending share callback receiver registered by <see cref="ShareWithCallback"/>.
        /// </summary>
        /// <param name="onResult">Optional per-call result callback.</param>
        public void CancelPendingShareCallback(Action<ShareOperationResult>? onResult = null)
        {
            Debug.Log($"[{LogTag}][{nameof(CancelPendingShareCallback)}] hasCallback: {onResult != null}");
            _pendingShareSelectedCallback = null;
            CallOperation(OperationCancelPendingShareCallback, null, onResult);
        }

        private void CallOperation(string operationName, string? json, Action<ShareOperationResult>? onResult)
        {
            _pendingOperationCallbacks[operationName] = onResult;

            if (!TryPrepareCall(operationName, json, out object?[] fullArgs, out AndroidJavaObject? activity))
            {
                FireOperationResult(ShareOperationResult.Failure(operationName, $"{operationName} could not be started."));
                return;
            }

            using (activity)
            {
                try
                {
                    pluginInstance!.Call(operationName, fullArgs);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[{LogTag}] {operationName} error: {ex.Message}");
                    FireOperationResult(ShareOperationResult.Failure(operationName, ex.Message));
                }
            }
        }

        private bool TryPrepareCall(string methodName, string? json, out object?[] fullArgs, out AndroidJavaObject? activity)
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

            activity = GetCurrentActivity();
            if (activity == null)
            {
                Debug.LogError($"[{LogTag}] {methodName} failed: currentActivity is null.");
                return false;
            }

            fullArgs = json != null
                ? new object?[] { activity, json }
                : new object?[] { activity };
            return true;
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

        private void FireOperationResult(ShareOperationResult result)
        {
            string operation = result.Operation;

            // Snapshot the callback now so a subsequent call to the same operation before main-thread
            // dispatch cannot cause this result to invoke a different callback.
            _pendingOperationCallbacks.TryGetValue(operation, out Action<ShareOperationResult>? cb);
            _pendingOperationCallbacks.Remove(operation);

            // shareWithCallback failure: clear the pending selected callback so it is not
            // delivered to a future successful call's onSelected handler.
            if (operation == OperationShareWithCallback && !result.IsSuccess)
            {
                _pendingShareSelectedCallback = null;
            }

            UnityMainThreadDispatcher.Instance.Enqueue(() =>
            {
                ShareOperationCompleted?.Invoke(result);
                cb?.Invoke(result);
            });
        }

        private void FireCallbackResult(ShareCallbackResult result)
        {
            UnityMainThreadDispatcher.Instance.Enqueue(() =>
            {
                ShareCallbackReceived?.Invoke(result);
                var cb = _pendingShareSelectedCallback;
                _pendingShareSelectedCallback = null;
                cb?.Invoke(result);
            });
        }

        private void ClearShareOperationListener()
        {
            if (pluginInstance == null)
            {
                return;
            }

            try
            {
                pluginInstance.Call("clearShareOperationListener");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[{LogTag}] clearShareOperationListener failed: {ex.Message}");
            }
        }

        private sealed class ShareOperationListenerProxy : AndroidJavaProxy
        {
            private readonly AndroidShareManager owner;

            public ShareOperationListenerProxy(AndroidShareManager owner)
                : base("android.unity.share.UnityAndroidShareManager$ShareOperationListener")
            {
                this.owner = owner;
            }

            // Must be public so IL2CPP can resolve the call from Java.
            // Method name and signature must exactly match the native interface.
            public void onShareOperation(string operation, bool isSuccessful, string? errorMessage)
            {
                ShareOperationResult result = isSuccessful
                    ? ShareOperationResult.Success(operation)
                    : ShareOperationResult.Failure(operation, errorMessage ?? string.Empty);
                owner.FireOperationResult(result);
            }

            public void onShareResult(string operation, string? selectedPackageName)
            {
                owner.FireCallbackResult(new ShareCallbackResult(operation, selectedPackageName));
            }
        }
    }
}
#endif
