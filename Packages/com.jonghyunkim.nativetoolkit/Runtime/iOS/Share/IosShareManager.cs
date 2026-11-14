#nullable enable

#if UNITY_IOS || UNITY_EDITOR
namespace JonghyunKim.NativeToolkit.Runtime.iOS.Share
{
    using System;
    using JonghyunKim.NativeToolkit.Runtime.Common;
    using UnityEngine;

    /// <summary>
    /// Singleton manager for iOS native share sheet operations.
    /// Wraps <c>UnityIosShareManager</c> (Swift) via P/Invoke through the C ABI bridge.
    /// The class itself compiles for <c>UNITY_IOS || UNITY_EDITOR</c> so it can be referenced and
    /// tested from the Editor regardless of build target; the native P/Invoke declarations and
    /// callback are restricted to <c>UNITY_IOS &amp;&amp; !UNITY_EDITOR</c> (device IL2CPP builds only).
    /// </summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "JonghyunKim.NativeToolkit.Runtime.Share")]
    public class IosShareManager : MonoBehaviour
    {
        private const string LogTag = "IosShareManager";

        /// <summary>Native operation name for presenting the share sheet.</summary>
        public const string OperationShare = "share";

        /// <summary>The failure every share gets until Share moves to the C ABI (stage 3-S).</summary>
        internal const string UnavailableMessage = "iOS share is only available on an iOS device.";

        private static IosShareManager? _instance;

        /// <summary>
        /// Singleton instance. Creates and persists a new GameObject if none exists.
        /// </summary>
        public static IosShareManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    Debug.Log($"[{LogTag}] Creating new instance of IosShareManager");
                    var go = new GameObject("IosShareManager");
                    _instance = go.AddComponent<IosShareManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        /// <summary>
        /// Raised when a <see cref="Share"/> operation completes, whether successfully or not.
        /// </summary>
        public event Action<IosShareResult>? ShareCompleted;

        // Per-call user callback (stored between native call and result callback).
        // Note: last-registered callback wins when Share is called concurrently.
        private static Action<IosShareResult>? s_onShare;

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
            _instance = null;
        }

        /// <summary>
        /// Presents the iOS system share sheet for the given content.
        /// </summary>
        /// <param name="payload">Content to share. A <c>null</c> payload or empty
        /// <see cref="IosShareContentPayload.items"/> results in an immediate failure without a
        /// native call.</param>
        /// <param name="onResult">Optional per-call callback invoked with the result. The global
        /// <see cref="ShareCompleted"/> event always fires first, regardless of this parameter.</param>
        public void Share(IosShareContentPayload? payload, Action<IosShareResult>? onResult = null)
        {
            Debug.Log($"[{LogTag}][{nameof(Share)}] itemCount: {payload?.items?.Length ?? 0}, hasCallback: {onResult != null}");

            s_onShare = onResult;

            if (payload == null || payload.items == null || payload.items.Length == 0)
            {
                FireResult(IosShareResult.Failure("No shareable items were provided."));
                return;
            }

            // Stage 3-S of artifact/topics/ios-c-abi-2: Share has not moved to native-toolkit's C ABI 2.0.0
            // yet, so every platform answers as the Editor always did (the 1.x bridge is gone).
            FireResult(IosShareResult.Failure(UnavailableMessage));
        }

        private static void FireResult(IosShareResult result)
        {
            var cb = s_onShare; // snapshot (guards against overlapping calls)
            s_onShare = null;
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
        internal static void InvokeInOrder(IosShareResult result, Action<IosShareResult>? common, Action<IosShareResult>? perCall)
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
