#nullable enable

namespace JonghyunKim.NativeToolkit.Runtime.Common
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    /// <summary>
    /// Thread-safe dispatcher for executing actions on Unity's main thread.
    /// Provides a mechanism for background threads to safely invoke Unity API calls
    /// by queuing actions that are executed during the main thread's Update cycle.
    /// Essential for native plugin callbacks that originate from non-Unity threads.
    /// </summary>
    public class UnityMainThreadDispatcher : MonoBehaviour
    {
        private static UnityMainThreadDispatcher? _instance;
        private readonly Queue<Action> _executionQueue = new Queue<Action>();

        /// <summary>
        /// Singleton instance property for UnityMainThreadDispatcher.
        /// Creates a new instance if none exists and ensures it persists across scene loads.
        /// </summary>
        public static UnityMainThreadDispatcher Instance
        {
            get
            {
                if (_instance == null)
                {
                    var gameObject = new GameObject("UnityMainThreadDispatcher");
                    _instance = gameObject.AddComponent<UnityMainThreadDispatcher>();
                    DontDestroyOnLoad(gameObject);
                }
                return _instance;
            }
        }

        /// <summary>
        /// Enqueues an action to be executed on the main Unity thread.
        /// Thread-safe method that can be called from any thread.
        /// </summary>
        /// <param name="action">Action to execute on the main thread</param>
        public void Enqueue(Action action)
        {
            lock (_executionQueue)
            {
                _executionQueue.Enqueue(action);
            }
        }

        /// <summary>
        /// Processes the execution queue during Unity's Update cycle.
        /// Executes all queued actions on the main thread in FIFO order.
        /// </summary>
        private void Update()
        {
            lock (_executionQueue)
            {
                while (_executionQueue.Count > 0)
                {
                    _executionQueue.Dequeue().Invoke();
                }
            }
        }
    }
}
