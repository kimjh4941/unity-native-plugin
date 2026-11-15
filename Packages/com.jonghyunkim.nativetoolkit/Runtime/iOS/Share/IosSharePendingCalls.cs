#nullable enable

#if UNITY_IOS || UNITY_EDITOR
namespace JonghyunKim.NativeToolkit.Runtime.iOS.Share
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The per-call callbacks of the shares the C ABI accepted, by the number passed as <c>user_data</c>
    /// (artifact/ios/share, C ABI design v2, D-1). Each call gets its own number, so two calls in a row each
    /// get their own result (1.x kept one slot and lost one of them). The lock guards the dictionary only;
    /// no native call is made under it.
    /// </summary>
    internal sealed class IosSharePendingCalls
    {
        private readonly object _gate = new();
        private readonly Dictionary<long, Action<IosShareResult>?> _calls = new();
        private long _last;

        /// <summary>Records a call's callback and returns its number (1, 2, …).</summary>
        internal long Add(Action<IosShareResult>? onResult)
        {
            lock (_gate)
            {
                long id = ++_last;
                _calls[id] = onResult;
                return id;
            }
        }

        /// <summary>Takes a call's callback out. False when the number is not there (already taken, or cleared).</summary>
        internal bool TryTake(long id, out Action<IosShareResult>? onResult)
        {
            lock (_gate)
            {
                if (_calls.TryGetValue(id, out onResult))
                {
                    _calls.Remove(id);
                    return true;
                }
                onResult = null;
                return false;
            }
        }

        /// <summary>Forgets every call (the manager was destroyed, as 1.x cleared its slot).</summary>
        internal void Clear()
        {
            lock (_gate) _calls.Clear();
        }

        internal int Count
        {
            get { lock (_gate) return _calls.Count; }
        }
    }
}
#endif
