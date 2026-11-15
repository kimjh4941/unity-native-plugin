#nullable enable

#if UNITY_IOS || UNITY_EDITOR
namespace JonghyunKim.NativeToolkit.Runtime.iOS.Common
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using JonghyunKim.NativeToolkit.Runtime.Common;
    using UnityEngine;

    /// <summary>
    /// Runs results decided during a call on a later frame than the call (artifact/ios/notification, C ABI design
    /// v4, D-3): never in the frame they were queued in, so a callback that calls again cannot keep one Update of
    /// <see cref="UnityMainThreadDispatcher"/> going (it drains what is queued during its own Update).
    /// </summary>
    /// <remarks>
    /// <see cref="IosNextFrameQueuePump"/>, on the dispatcher's GameObject, hands the due items to the end of the
    /// dispatcher's queue, so they run after what is already there. While items wait, results that are already
    /// known (<see cref="EnqueueBehind"/>) line up behind them, so results come in the order of the calls. Any
    /// thread may queue: an item queued off the main thread gets its frame on the pump's next turn, since
    /// <c>Time.frameCount</c> is for the main thread only. <see cref="EnsurePump"/> must have run on the main thread.
    /// </remarks>
    internal static class IosNextFrameQueue
    {
        private const string LogTag = "IosNextFrameQueue";

        // Frame: the frame it was queued in; Unstamped: queued off the main thread; Due: runs as soon as reached.
        private const int Unstamped = int.MaxValue;
        private const int Due = int.MinValue;

        private static readonly object s_gate = new();
        private static readonly List<(int Frame, Action Run)> s_waiting = new();
        private static IosNextFrameQueuePump? s_pump;
        private static int s_mainThreadId;

        /// <summary>Adds the pump to the dispatcher's GameObject. Main thread (the managers call it in Awake).</summary>
        internal static void EnsurePump()
        {
            s_mainThreadId = Thread.CurrentThread.ManagedThreadId;
            UnityMainThreadDispatcher dispatcher = UnityMainThreadDispatcher.Instance;
            if (s_pump != null) return;
            IosNextFrameQueuePump pump = dispatcher.GetComponent<IosNextFrameQueuePump>();
            s_pump = pump != null ? pump : dispatcher.gameObject.AddComponent<IosNextFrameQueuePump>();
        }

        /// <summary>Queues an action for a frame after this one.</summary>
        internal static void Enqueue(Action run)
        {
            bool onMain = Thread.CurrentThread.ManagedThreadId == s_mainThreadId;
            if (onMain) EnsurePump();
            int frame = onMain ? Time.frameCount : Unstamped;
            Debug.Log($"[{LogTag}][{nameof(Enqueue)}] frame: {(onMain ? frame.ToString() : "next")}");
            lock (s_gate) s_waiting.Add((frame, run));
        }

        /// <summary>
        /// Runs an action whose result is already known, through the dispatcher: behind the waiting items when there
        /// are any, at the dispatcher's next turn otherwise.
        /// </summary>
        internal static void EnqueueBehind(Action run)
        {
            // On the main thread, a dispatcher made again (its pump went with the old one) gets a pump here.
            if (Thread.CurrentThread.ManagedThreadId == s_mainThreadId) EnsurePump();
            lock (s_gate)
            {
                if (s_waiting.Count > 0)
                {
                    s_waiting.Add((Due, run));
                    return;
                }
            }
            UnityMainThreadDispatcher.Instance.Enqueue(run);
        }

        /// <summary>
        /// Hands the items queued before <paramref name="frame"/> to the dispatcher, in order, up to the first one
        /// queued in this frame. Items queued off the main thread get this frame first. Called by the pump.
        /// </summary>
        internal static void HandOver(int frame)
        {
            var handed = new List<Action>();
            lock (s_gate)
            {
                if (s_waiting.Count == 0) return;
                for (int i = 0; i < s_waiting.Count; i++)
                {
                    if (s_waiting[i].Frame == Unstamped) s_waiting[i] = (frame, s_waiting[i].Run);
                }
                while (s_waiting.Count > 0 && (s_waiting[0].Frame == Due || s_waiting[0].Frame < frame))
                {
                    handed.Add(s_waiting[0].Run);
                    s_waiting.RemoveAt(0);
                }
            }
            if (handed.Count == 0) return;
            UnityMainThreadDispatcher dispatcher = UnityMainThreadDispatcher.Instance;
            foreach (Action run in handed) dispatcher.Enqueue(run);
            Debug.Log($"[{LogTag}][{nameof(HandOver)}] frame: {frame}, handed: {handed.Count}, waiting: {CountForTests}");
        }

        internal static int CountForTests
        {
            get { lock (s_gate) return s_waiting.Count; }
        }

        // A Play session or app launch starts empty (Enter Play Mode without a domain reload keeps statics).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            lock (s_gate) s_waiting.Clear();
            s_pump = null;
            s_mainThreadId = Thread.CurrentThread.ManagedThreadId;
        }
    }
}
#endif
