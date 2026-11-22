#nullable enable

#if UNITY_IOS || UNITY_EDITOR
namespace JonghyunKim.NativeToolkit.Runtime.iOS.Notification
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The per-call callbacks of the notification calls the C ABI accepted, by the number passed as <c>user_data</c>
    /// (artifact/ios/notification, C ABI design v4, D-2). Each call gets its own number, so every call gets its own
    /// result (1.x kept one slot per operation and the later call won). Not cleared when the manager is destroyed:
    /// 1.x kept its callbacks in statics and called them after destroy too. Numbers are never reused. The callback
    /// is kept as a <see cref="Delegate"/> with the operation name, so this compiles without the iOS-only types.
    /// The lock guards the dictionary only; no native call is made under it.
    /// </summary>
    internal sealed class IosNotificationPendingCalls
    {
        private readonly object _gate = new();
        private readonly Dictionary<long, (string Operation, Delegate? Callback)> _calls = new();
        private long _last;

        /// <summary>Records a call and returns its number (1, 2, …).</summary>
        internal long Add(string operation, Delegate? callback)
        {
            lock (_gate)
            {
                long id = ++_last;
                _calls[id] = (operation, callback);
                return id;
            }
        }

        /// <summary>Takes a call out. False when the number is not there (already taken).</summary>
        internal bool TryTake(long id, out string operation, out Delegate? callback)
        {
            lock (_gate)
            {
                if (_calls.TryGetValue(id, out var call))
                {
                    _calls.Remove(id);
                    operation = call.Operation;
                    callback = call.Callback;
                    return true;
                }
                operation = "";
                callback = null;
                return false;
            }
        }

        internal int Count
        {
            get { lock (_gate) return _calls.Count; }
        }
    }
}
#endif
