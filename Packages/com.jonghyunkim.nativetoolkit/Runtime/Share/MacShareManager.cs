#nullable enable

#if UNITY_STANDALONE_OSX || UNITY_EDITOR
namespace JonghyunKim.NativeToolkit.Runtime.Share
{
    using System;
    using System.Runtime.InteropServices;
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
    using AOT;
#endif
    using JonghyunKim.NativeToolkit.Runtime.Common;
    using UnityEngine;

    /// <summary>
    /// Singleton manager for macOS native sharing service operations.
    /// Wraps <c>UnityMacShareManager</c> (Swift) via P/Invoke through the C ABI bridge.
    /// The class itself compiles for <c>UNITY_STANDALONE_OSX || UNITY_EDITOR</c> so it can be
    /// referenced and tested from the Editor regardless of build target; the native P/Invoke
    /// declarations and callbacks are restricted to <c>UNITY_STANDALONE_OSX &amp;&amp; !UNITY_EDITOR</c>
    /// (macOS Standalone builds only).
    /// </summary>
    public class MacShareManager : MonoBehaviour
    {
        private const string LogTag = "MacShareManager";

        /// <summary>Native operation name for presenting the share picker.</summary>
        public const string OperationShare = "share";

        /// <summary>Native operation name for performing a named service directly.</summary>
        public const string OperationShareViaService = "shareViaService";

        private static MacShareManager? _instance;

        /// <summary>
        /// Singleton instance. Creates and persists a new GameObject if none exists.
        /// </summary>
        public static MacShareManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    Debug.Log($"[{LogTag}] Creating new instance of MacShareManager");
                    var go = new GameObject("MacShareManager");
                    _instance = go.AddComponent<MacShareManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        /// <summary>
        /// Raised when a <see cref="Share"/> or <see cref="ShareViaService"/> operation
        /// completes, whether successfully or not.
        /// </summary>
        public event Action<MacShareResult>? ShareCompleted;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void ShareCallback(bool isSuccess, bool completed, string? serviceName, string? errorMessage);

#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void shareContent(string contentJson, ShareCallback callback);

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void shareViaService(string serviceName, string contentJson, ShareCallback callback);

        // Persistent delegates: stored permanently to prevent GC collection of the native function pointers.
        private static readonly ShareCallback s_shareDelegate = OnShareResult;
        private static readonly ShareCallback s_shareViaServiceDelegate = OnShareViaServiceResult;
#endif

        // Per-call user callbacks (stored between native call and result callback).
        // Note: last-registered callback wins when the same operation is called concurrently.
        private static Action<MacShareResult>? s_onShare;
        private static Action<MacShareResult>? s_onShareViaService;

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

            s_onShare = null;
            s_onShareViaService = null;
            _instance = null;
        }

        /// <summary>
        /// Presents the macOS sharing service picker for the given content.
        /// </summary>
        /// <param name="payload">Content to share. A <c>null</c> payload or empty
        /// <see cref="MacShareContentPayload.items"/> results in an immediate failure without a
        /// native call.</param>
        /// <param name="onResult">Optional per-call callback invoked with the result. The global
        /// <see cref="ShareCompleted"/> event always fires first, regardless of this parameter.</param>
        /// <remarks>
        /// Must be invoked from a user-initiated action (e.g. a button click); presenting the
        /// picker outside of a mouseDown event context may result in unstable presentation. See
        /// design doc for details (mouseDown requirement, best-effort / requires manual
        /// verification). Prefer <see cref="ShareViaService"/> when reliability matters.
        /// </remarks>
        public void Share(MacShareContentPayload? payload, Action<MacShareResult>? onResult = null)
        {
            Debug.Log($"[{LogTag}][{nameof(Share)}] itemCount: {payload?.items?.Length ?? 0}, hasCallback: {onResult != null}");

            s_onShare = onResult;

            if (payload == null || payload.items == null || payload.items.Length == 0)
            {
                FireShareResult(MacShareResult.Failure(OperationShare, "No shareable items were provided."));
                return;
            }

#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
            if (Application.platform != RuntimePlatform.OSXPlayer)
            {
                FireShareResult(MacShareResult.Failure(OperationShare, "macOS share is only available on a macOS Standalone player."));
                return;
            }

            string json = MacShareJsonBuilder.BuildShareContentJson(payload);
            try
            {
                shareContent(json, s_shareDelegate);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{LogTag}][{nameof(Share)}] {ex.Message}");
                FireShareResult(MacShareResult.Failure(OperationShare, $"Internal error: {ex.Message}"));
            }
#else
            FireShareResult(MacShareResult.Failure(OperationShare, "macOS share is only available on a macOS Standalone player."));
#endif
        }

        /// <summary>
        /// Performs a single named sharing service directly (no picker UI).
        /// </summary>
        /// <param name="serviceName">Raw <c>NSSharingService.Name</c> value (required, e.g.
        /// <see cref="MacShareServiceNames.MailCompose"/>). Must not be null or empty.</param>
        /// <param name="payload">Content to share. A <c>null</c> payload or empty
        /// <see cref="MacShareContentPayload.items"/> results in an immediate failure without a
        /// native call.</param>
        /// <param name="onResult">Optional per-call callback invoked with the result. The global
        /// <see cref="ShareCompleted"/> event always fires first, regardless of this parameter.</param>
        public void ShareViaService(string serviceName, MacShareContentPayload? payload, Action<MacShareResult>? onResult = null)
        {
            Debug.Log($"[{LogTag}][{nameof(ShareViaService)}] serviceName: {serviceName}, itemCount: {payload?.items?.Length ?? 0}, hasCallback: {onResult != null}");

            s_onShareViaService = onResult;

            if (string.IsNullOrEmpty(serviceName))
            {
                FireShareViaServiceResult(MacShareResult.Failure(OperationShareViaService, "Sharing service name must not be empty."));
                return;
            }

            if (payload == null || payload.items == null || payload.items.Length == 0)
            {
                FireShareViaServiceResult(MacShareResult.Failure(OperationShareViaService, "No shareable items were provided."));
                return;
            }

#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
            if (Application.platform != RuntimePlatform.OSXPlayer)
            {
                FireShareViaServiceResult(MacShareResult.Failure(OperationShareViaService, "macOS share is only available on a macOS Standalone player."));
                return;
            }

            string json = MacShareJsonBuilder.BuildShareContentJson(payload);
            try
            {
                shareViaService(serviceName, json, s_shareViaServiceDelegate);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{LogTag}][{nameof(ShareViaService)}] {ex.Message}");
                FireShareViaServiceResult(MacShareResult.Failure(OperationShareViaService, $"Internal error: {ex.Message}"));
            }
#else
            FireShareViaServiceResult(MacShareResult.Failure(OperationShareViaService, "macOS share is only available on a macOS Standalone player."));
#endif
        }

#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        [MonoPInvokeCallback(typeof(ShareCallback))]
        private static void OnShareResult(bool isSuccess, bool completed, string? serviceName, string? errorMessage)
        {
            var result = isSuccess
                ? MacShareResult.Success(OperationShare, completed, serviceName)
                : MacShareResult.Failure(OperationShare, errorMessage);
            FireShareResult(result);
        }

        [MonoPInvokeCallback(typeof(ShareCallback))]
        private static void OnShareViaServiceResult(bool isSuccess, bool completed, string? serviceName, string? errorMessage)
        {
            var result = isSuccess
                ? MacShareResult.Success(OperationShareViaService, completed, serviceName)
                : MacShareResult.Failure(OperationShareViaService, errorMessage);
            FireShareViaServiceResult(result);
        }
#endif

        private static void FireShareResult(MacShareResult result)
        {
            var cb = s_onShare; // snapshot (guards against overlapping calls)
            s_onShare = null;
            var common = _instance?.ShareCompleted;
            UnityMainThreadDispatcher.Instance.Enqueue(() => InvokeInOrder(result, common, cb));
        }

        private static void FireShareViaServiceResult(MacShareResult result)
        {
            var cb = s_onShareViaService; // snapshot (guards against overlapping calls)
            s_onShareViaService = null;
            var common = _instance?.ShareCompleted;
            UnityMainThreadDispatcher.Instance.Enqueue(() => InvokeInOrder(result, common, cb));
        }

        /// <summary>
        /// Invokes the common event followed by the per-call callback, swallowing any exception
        /// raised by either. Extracted as a pure, Unity-lifecycle-independent helper so dispatch
        /// order and exception handling can be verified directly from EditMode tests.
        /// </summary>
        /// <param name="result">Result to dispatch.</param>
        /// <param name="common">Common event snapshot (invoked first).</param>
        /// <param name="perCall">Per-call callback snapshot (invoked second).</param>
        internal static void InvokeInOrder(MacShareResult result, Action<MacShareResult>? common, Action<MacShareResult>? perCall)
        {
            try
            {
                common?.Invoke(result);
                perCall?.Invoke(result);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{LogTag}][{nameof(InvokeInOrder)}] {ex.Message}");
            }
        }
    }
}
#endif
