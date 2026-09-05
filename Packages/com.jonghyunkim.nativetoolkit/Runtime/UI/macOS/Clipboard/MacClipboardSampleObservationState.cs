#nullable enable

#if UNITY_STANDALONE_OSX || UNITY_EDITOR

// Intentional deviation from the "log every internal method" rule in csharp.md: this is a pure
// state machine with no side effects. The controller logs every transition it drives, so logging
// here would only duplicate those entries.

/// <summary>
/// Pure state machine for the macOS sample's observation lifecycle.
/// </summary>
/// <remarks>
/// <para>
/// StartObserving and StopObserving share a single-flight key, so a call issued while another is
/// pending is rejected with 9001 - and that rejection arrives on the rejected call's own callback.
/// Only the call that took ownership may change the state; otherwise a rejected second start would
/// clear <see cref="ControlPending"/> while the first start is still running natively, and a later
/// screen teardown would fail to stop the observation it eventually creates.
/// </para>
/// <para>
/// <b>A failed start does not mean "not observing" on macOS.</b> The native monitor validates the
/// interval and resolves the scope before it touches the running observation, so a rejected
/// restart leaves the previous one alive. The iOS sample sets IsObserving to false on any failed
/// start; copying that here would report "stopped" while the poller keeps running, and the
/// deferred stop below would never be issued.
/// </para>
/// </remarks>
internal struct MacClipboardSampleObservationState
{
    /// <summary>Token for a call that must not change this state, such as a start rejected as busy.</summary>
    internal const int NonOwningToken = 0;

    private int _nextToken;
    private int _controlOwner;

    /// <summary>Whether a successful start is currently observing.</summary>
    internal bool IsObserving { get; private set; }

    /// <summary>Whether a start or stop is awaiting its native completion.</summary>
    internal bool ControlPending => _controlOwner != NonOwningToken;

    /// <summary>
    /// Whether the screen was left while a control call was pending, so that call must issue the
    /// stop once it completes.
    /// </summary>
    internal bool StopRequestedAfterControl { get; private set; }

    internal bool CanStartObserving => !IsObserving && !ControlPending;

    internal bool CanRestartObserving => IsObserving && !ControlPending;

    internal bool CanStopObserving => IsObserving && !ControlPending;

    internal bool CanChangeScope => !IsObserving && !ControlPending;

    /// <summary>
    /// Takes ownership for a start, or returns <see cref="NonOwningToken"/> when another control
    /// call is already pending.
    /// </summary>
    /// <returns>The owner token, or <see cref="NonOwningToken"/>.</returns>
    internal int BeginStart()
    {
        if (ControlPending) return NonOwningToken;
        _controlOwner = ++_nextToken;
        return _controlOwner;
    }

    /// <summary>
    /// Takes ownership for a stop, or returns <see cref="NonOwningToken"/> when another control
    /// call is already pending.
    /// </summary>
    /// <returns>The owner token, or <see cref="NonOwningToken"/>.</returns>
    internal int BeginStop()
    {
        if (ControlPending) return NonOwningToken;
        _controlOwner = ++_nextToken;
        return _controlOwner;
    }

    /// <summary>
    /// Applies a start completion.
    /// </summary>
    /// <param name="owner">Owner token captured when the start was issued.</param>
    /// <param name="isSuccess">Whether the native start succeeded.</param>
    /// <returns><c>true</c> when this callback owned the state, so the caller may act on it.</returns>
    internal bool CompleteStart(int owner, bool isSuccess)
    {
        if (owner == NonOwningToken || owner != _controlOwner) return false;
        _controlOwner = NonOwningToken;

        // Only a success changes what is being observed. A failure leaves IsObserving exactly as
        // it was: false for a first start that failed, and true for a restart that failed while
        // the previous observation kept running natively.
        if (isSuccess)
        {
            IsObserving = true;
        }
        return true;
    }

    /// <summary>
    /// Applies a stop completion.
    /// </summary>
    /// <param name="owner">Owner token captured when the stop was issued.</param>
    /// <param name="isSuccess">Whether the native stop succeeded.</param>
    /// <returns><c>true</c> when this callback owned the state, so the caller may act on it.</returns>
    internal bool CompleteStop(int owner, bool isSuccess)
    {
        if (owner == NonOwningToken || owner != _controlOwner) return false;
        _controlOwner = NonOwningToken;

        // A failed stop did not stop anything natively, so the observation stays.
        if (isSuccess)
        {
            IsObserving = false;
        }
        return true;
    }

    /// <summary>
    /// Records that the screen is going away. A stop cannot be issued while another control call
    /// is pending, so the pending call issues it instead once it completes.
    /// </summary>
    internal void RequestStop()
    {
        if (ControlPending)
        {
            StopRequestedAfterControl = true;
        }
    }

    /// <summary>Whether a stop can be issued right now.</summary>
    /// <returns><c>true</c> when something is being observed and no control call is pending.</returns>
    internal bool ShouldIssueStopNow() => IsObserving && !ControlPending;

    /// <summary>
    /// Whether the completing control call must issue the deferred stop on the screen's behalf.
    /// <para>
    /// The decision is made on what is still being observed, <b>not</b> on whether the completion
    /// succeeded. A restart that failed leaves the previous observation running on macOS, so
    /// keying this off success would walk away from a live poller.
    /// </para>
    /// </summary>
    /// <returns><c>true</c> when the caller must issue a stop now.</returns>
    internal bool TakeDeferredStop()
    {
        if (!StopRequestedAfterControl || ControlPending)
        {
            return false;
        }

        // Consumed either way: the control call this was waiting on has completed, so the request
        // has been answered. Leaving it set when there is nothing to stop would make a later
        // completion issue a stop for an observation that no longer exists.
        StopRequestedAfterControl = false;
        return IsObserving;
    }
}
#endif
