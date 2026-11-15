#nullable enable

#if UNITY_IOS || UNITY_EDITOR
namespace JonghyunKim.NativeToolkit.Runtime.iOS.Share
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
#if UNITY_IOS && !UNITY_EDITOR
    using AOT;
#endif
    using JonghyunKim.NativeToolkit.Runtime.Common;
    using JonghyunKim.NativeToolkit.Runtime.iOS.Common;
    using UnityEngine;

    /// <summary>
    /// Singleton manager for iOS native share sheet operations.
    /// </summary>
    /// <remarks>
    /// Calls native-toolkit's C ABI 2.0.0 (<c>Share.h</c>; artifact/ios/share, C ABI design v3). Every result,
    /// also a failure decided before the native call, comes on the Update after it is known:
    /// <see cref="ShareCompleted"/> first, then the call's own callback. A result reaches the call it belongs
    /// to (1.x kept one slot, so a result could reach another call's callback). A share made while another
    /// sheet is being shown may never complete: native-toolkit 2.0.0 shows one sheet and gives the other
    /// call no result (measured on the simulator and an iPhone; Share.h promises one). Items are passed on as 1.x
    /// did: null items, unknown types and null values are left out, with a warning. In the Editor every
    /// share fails with "iOS share is only available on an iOS device.". The class compiles for
    /// <c>UNITY_IOS || UNITY_EDITOR</c> so it can be referenced and tested from the Editor.
    /// </remarks>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "JonghyunKim.NativeToolkit.Runtime.Share")]
    public class IosShareManager : MonoBehaviour
    {
        private const string LogTag = "IosShareManager";

        /// <summary>Native operation name for presenting the share sheet.</summary>
        public const string OperationShare = "share";

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

        /// <summary>The per-call callbacks of accepted shares (design v3, D-1).</summary>
        internal static readonly IosSharePendingCalls s_pending = new();

        /// <summary>The native call of <see cref="Begin"/>: a stand-in in the Editor's tests (D-13).</summary>
        internal delegate int NativeShare(ref IosShareCApi.ShareRequest request, IntPtr userData);

#if UNITY_IOS && !UNITY_EDITOR
        // Kept for the life of the process, so the function pointer handed to the C ABI stays valid.
        // Internal for the player tests, which call the C ABI and the completion with it.
        private static readonly IosShareCApi.ResultCallback s_resultCallback = OnResult;
        internal static readonly IntPtr s_resultCallbackPointer = Marshal.GetFunctionPointerForDelegate(s_resultCallback);
#endif

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

            // As 1.x cleared its slot: a share completing later reaches no per-call callback (D-1).
            s_pending.Clear();
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

            // D-6: the order of 1.x's checks, then the library.
            if (payload == null || payload.items == null || payload.items.Length == 0)
            {
                FireResult(IosShareResult.Failure(IosShareCApi.NoItems), onResult);
                return;
            }
            if (!IosNativeToolkitCApi.IsAvailable)
            {
                FireResult(IosShareResult.Failure(IosShareCApi.Unavailable), onResult);
                return;
            }

            IosShareCApi.Prepared prepared = IosShareCApi.Prepare(payload);
            foreach (string warning in IosShareCApi.Warnings(prepared))
                Debug.LogWarning($"[{LogTag}] {warning}");
            if (prepared.Items.Count == 0)
            {
                FireResult(IosShareResult.Failure(IosShareCApi.NoItems), onResult);
                return;
            }

#if UNITY_IOS && !UNITY_EDITOR
            Begin(prepared, onResult, (ref IosShareCApi.ShareRequest request, IntPtr userData) =>
                IosShareCApi.ntk_share_content_async(ref request, s_resultCallbackPointer, userData, IntPtr.Zero));
#endif
        }

        /// <summary>
        /// Builds the request, records the call, and makes the native call (D-1, D-6, D-13). A rejection or an
        /// exception takes the call back out and gives its callback a failure on the next Update; an accepted
        /// call completes through the completion callback.
        /// </summary>
        internal static void Begin(IosShareCApi.Prepared prepared, Action<IosShareResult>? onResult, NativeShare call)
        {
            var allocated = new List<IntPtr>();
            try
            {
                IosShareCApi.ShareRequest request;
                try
                {
                    request = IosShareCApi.BuildRequest(prepared, allocated);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[{LogTag}][{nameof(Share)}] {ex.Message}");
                    FireResult(IosShareResult.Failure($"Internal error: {ex.Message}"), onResult);
                    return;
                }

                // Recorded before the call: called off the main thread, the completion may come first.
                long id = s_pending.Add(onResult);
                int error;
                try
                {
                    error = call(ref request, new IntPtr(id));
                }
                catch (Exception ex)
                {
                    s_pending.TryTake(id, out _);
                    Debug.LogError($"[{LogTag}][{nameof(Share)}] {ex.Message}");
                    FireResult(IosShareResult.Failure($"Internal error: {ex.Message}"), onResult);
                    return;
                }

                if (error != IosShareCApi.ErrorNone)
                {
                    // The result goes out even when the number is gone (the manager was destroyed meanwhile).
                    s_pending.TryTake(id, out Action<IosShareResult>? rejected);
                    Debug.LogError($"[{LogTag}][{nameof(Share)}] rejected: {IosShareCApi.ErrorName(error)}");
                    FireResult(IosShareResult.Failure(IosShareCApi.EntryFailureMessage(error)), rejected);
                }
            }
            finally
            {
                IosNativeToolkitCApi.FreeAll(allocated);
            }
        }

        /// <summary>
        /// Delivers a completion to its call (D-7). A number no longer recorded (the manager was destroyed)
        /// raises only <see cref="ShareCompleted"/> of a live manager, if there is one.
        /// </summary>
        internal static void Complete(long id, IosShareResult result)
        {
            s_pending.TryTake(id, out Action<IosShareResult>? onResult);
            FireResult(result, onResult);
        }

#if UNITY_IOS && !UNITY_EDITOR
        /// <summary>The completion of every share (on the main thread). No exception leaves.</summary>
        [MonoPInvokeCallback(typeof(IosShareCApi.ResultCallback))]
        internal static void OnResult(IntPtr userData, int error, uint systemCode, IntPtr result)
        {
            try
            {
                IosShareResult read;
                try
                {
                    read = IosShareCApi.ReadResult(error, result);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[{LogTag}] could not read a share result: {ex.Message}");
                    read = IosShareResult.Failure(IosShareCApi.CompletionFailureMessage(IosShareCApi.ErrorUnknown));
                }
                Complete(userData.ToInt64(), read);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{LogTag}] share completion error: {ex.Message}");
            }
            finally
            {
                if (result != IntPtr.Zero)
                    IosShareCApi.ntk_share_result_free(result);
            }
        }
#endif

        /// <summary>
        /// Queues a result for the next Update: <see cref="ShareCompleted"/> of the manager alive now, then
        /// <paramref name="perCall"/>.
        /// </summary>
        private static void FireResult(IosShareResult result, Action<IosShareResult>? perCall)
        {
            var common = _instance?.ShareCompleted;
            UnityMainThreadDispatcher.Instance.Enqueue(() => InvokeInOrder(result, common, perCall));
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
