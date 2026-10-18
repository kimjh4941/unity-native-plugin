#nullable enable

#if UNITY_ANDROID || UNITY_EDITOR
namespace JonghyunKim.NativeToolkit.Runtime.Android.Common
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading;
    using UnityEngine;

    /// <summary>
    /// Carries work from native-toolkit's C ABI callbacks, which arrive on the Android main (UI)
    /// thread, to Unity's main thread.
    /// </summary>
    /// <remarks>
    /// The C ABI callbacks must not block the Android main thread (C ABI design part 1, 1.3.2): it is
    /// the UI thread, and a stall there becomes an ANR. <c>UnityMainThreadDispatcher</c> runs queued
    /// actions while holding the lock that <c>Enqueue</c> takes, so a callback would wait for every
    /// subscriber to finish (artifact/topics/dispatcher-runs-under-lock). This queue takes no lock
    /// across the actions it runs.
    /// <para>
    /// Like the dispatcher path the 1.x bridge used, a GameObject of its own pumps it, so a disabled
    /// or destroyed manager does not stop delivery, and an action that throws leaves the rest for the
    /// next frame. Unlike it, each frame runs only the actions that were queued when the frame's
    /// <see cref="Update"/> began (the dispatcher also ran those added while it ran), which bounds the
    /// work of one frame.
    /// </para>
    /// <para>
    /// Every Android feature that calls the C ABI posts here (artifact/topics/android-c-abi-2, J-9).
    /// </para>
    /// </remarks>
    internal sealed class AndroidNativeCallbackQueue : MonoBehaviour
    {
        private const string LogTag = nameof(AndroidNativeCallbackQueue);

        private static readonly ConcurrentQueue<Action> s_actions = new ConcurrentQueue<Action>();
        private static AndroidNativeCallbackQueue? s_pump;

        // Whether the pump is enabled, readable from any thread (s_pump is a Unity object, which only
        // the main thread may compare with null). Set in OnEnable, cleared in OnDisable, which also
        // runs before OnDestroy.
        private static volatile bool s_pumping;

        // Set when the app quits: callbacks that arrive after the pump is gone are expected then.
        private static volatile bool s_quitting;

        // Unity's main thread, recorded when the app (or the Editor's play mode) starts. 0 until then.
        private static int s_mainThreadId;

        /// <summary>
        /// Resets the static state when the app or the Editor's play mode starts. With the Editor's
        /// domain reload turned off, statics would otherwise keep the last play session's values.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnStart()
        {
            s_mainThreadId = Thread.CurrentThread.ManagedThreadId;
            s_quitting = false;
            s_pumping = false;
            s_pump = null;
            while (s_actions.TryDequeue(out _)) { }
        }

        /// <summary>
        /// Queues an action to run on Unity's main thread. Safe from any thread; never waits for the
        /// actions already queued. When the queue's object has been destroyed or disabled (by code
        /// that destroys every object, for instance), the action waits until a manager restores it,
        /// and this is logged as an error.
        /// </summary>
        internal static void Post(Action action)
        {
            Debug.Log($"[{LogTag}][{nameof(Post)}] queued: {s_actions.Count}");
            // Enqueue first, then (after a full fence) read the flag. OnDisable clears the flag first,
            // then looks at the queue. Whichever way the two interleave, at least one of them sees the
            // stranded action.
            s_actions.Enqueue(action);
            Interlocked.MemoryBarrier();
            if (!s_pumping) ReportStranded(nameof(Post));
        }

        /// <summary>
        /// Creates the object that runs the queue, or enables it again, if needed. Called from a
        /// manager's <c>Awake</c> and before each call into the library. Off Unity's main thread it
        /// does nothing: Unity objects may be touched only from the main thread, and a caller that
        /// ignores the managers' threading note (1.x worked from other threads) must not get an
        /// exception for it.
        /// </summary>
        internal static void EnsureCreated()
        {
            Debug.Log($"[{LogTag}][{nameof(EnsureCreated)}]");
            if (s_mainThreadId != 0 && Thread.CurrentThread.ManagedThreadId != s_mainThreadId)
            {
                Debug.Log($"[{LogTag}][{nameof(EnsureCreated)}] not on Unity's main thread; not checked");
                return;
            }
            if (s_pump != null)
            {
                if (s_pump.isActiveAndEnabled) return;
                Debug.LogWarning($"[{LogTag}][{nameof(EnsureCreated)}] the queue's object was disabled; enabling it again");
                s_pump.gameObject.SetActive(true);
                s_pump.enabled = true;
                return;
            }

            Debug.Log($"[{LogTag}][{nameof(EnsureCreated)}] creating the queue's object");
            var gameObject = new GameObject(nameof(AndroidNativeCallbackQueue));
            DontDestroyOnLoad(gameObject);
            s_pump = gameObject.AddComponent<AndroidNativeCallbackQueue>();
        }

        /// <summary>Drops every queued action. Tests use it so one test's late completions do not reach the next.</summary>
        internal static void ResetForTests()
        {
            Debug.Log($"[{LogTag}][{nameof(ResetForTests)}] dropping: {s_actions.Count}");
            while (s_actions.TryDequeue(out _)) { }
        }

        private static void ReportStranded(string where)
        {
            if (s_quitting) return;
            Debug.LogError($"[{LogTag}][{where}] the object that runs native callbacks on Unity's main thread is missing or disabled; queued callbacks run when a manager is next used");
        }

        private void Awake()
        {
            Debug.Log($"[{LogTag}][{nameof(Awake)}]");
            if (s_pump != null && s_pump != this)
            {
                Destroy(gameObject);
                return;
            }
            s_pump = this;
            Application.quitting += OnQuitting;
        }

        private void OnEnable()
        {
            Debug.Log($"[{LogTag}][{nameof(OnEnable)}]");
            if (s_pump != this) return;
            s_pumping = true;
        }

        private void OnDisable()
        {
            Debug.Log($"[{LogTag}][{nameof(OnDisable)}]");
            if (s_pump != this) return;
            s_pumping = false;
            // Clear the flag, then look: see Post.
            Interlocked.MemoryBarrier();
            if (!s_actions.IsEmpty) ReportStranded(nameof(OnDisable));
        }

        private void OnDestroy()
        {
            Debug.Log($"[{LogTag}][{nameof(OnDestroy)}]");
            if (s_pump != this) return;
            s_pump = null;
            Application.quitting -= OnQuitting;
        }

        private static void OnQuitting() => s_quitting = true;

        /// <summary>
        /// Runs the actions that were queued when this frame began. Actions posted while these run wait
        /// for the next frame, which bounds the work of one frame. No log on frames with nothing to do,
        /// so the device log is not flooded every frame.
        /// </summary>
        private void Update()
        {
            // A duplicate waiting for its Destroy does not run the queue.
            if (s_pump != this || s_actions.IsEmpty) return;
            int count = s_actions.Count;

            Debug.Log($"[{LogTag}][{nameof(Update)}] running {count} queued native callback(s)");
            for (int i = 0; i < count && s_actions.TryDequeue(out Action? action); i++)
            {
                action();
            }
        }
    }
}
#endif
