#nullable enable

using System;

namespace JonghyunKim.NativeToolkit.Runtime.Share
{
    /// <summary>
    /// Coordinates chooser-action callback delivery: fires the global
    /// <see cref="ChooserActionTapped"/> event first, then the per-call callback registered via
    /// <see cref="Register"/>. Enforces last-registered-wins semantics and is exception-safe.
    /// Platform-agnostic — has no Unity or Android guards — so it can be tested in EditMode.
    /// </summary>
    public sealed class ShareChooserActionCallbackCoordinator
    {
        private readonly Action<Action> _dispatch;
        private Action<ShareChooserActionResult>? _pending;

        /// <summary>
        /// Occurs when a custom chooser action is tapped. Always fired before the per-call
        /// callback registered via <see cref="Register"/>.
        /// </summary>
        public event Action<ShareChooserActionResult>? ChooserActionTapped;

        /// <summary>
        /// Initializes a new instance with the provided dispatcher.
        /// </summary>
        /// <param name="dispatch">
        /// Enqueues work on the target thread. In production this is
        /// <c>UnityMainThreadDispatcher.Instance.Enqueue</c>; in EditMode tests pass
        /// <c>a => a()</c> for synchronous execution.
        /// </param>
        public ShareChooserActionCallbackCoordinator(Action<Action> dispatch) => _dispatch = dispatch;

        /// <summary>
        /// Registers (or replaces) the per-call callback. Passing null clears the previous
        /// registration (last-registered-wins; null is treated as a valid registration value).
        /// </summary>
        public void Register(Action<ShareChooserActionResult>? onChooserAction) => _pending = onChooserAction;

        /// <summary>
        /// Clears the per-call callback. Call this on teardown to prevent stale references.
        /// </summary>
        public void Clear() => _pending = null;

        /// <summary>
        /// Dispatches <paramref name="result"/> to <see cref="ChooserActionTapped"/> and then
        /// to the registered per-call callback. The per-call callback is NOT cleared after firing
        /// because the same registration may receive multiple taps.
        /// Each invocation is wrapped in its own try/catch so that a throwing global-event subscriber
        /// never prevents the per-call callback from firing, and vice versa.
        /// Note: if <paramref name="_dispatch"/> itself throws, that exception propagates to the caller.
        /// No logging is performed here — this class is kept UnityEngine-free for EditMode testability.
        /// </summary>
        public void Fire(ShareChooserActionResult result)
        {
            var cb = _pending; // snapshot: not cleared so same registration handles multiple taps
            _dispatch(() =>
            {
                try { ChooserActionTapped?.Invoke(result); }
                catch (Exception) { }

                try { cb?.Invoke(result); }
                catch (Exception) { }
            });
        }
    }
}
