#nullable enable

#if UNITY_IOS || UNITY_EDITOR
using System;
using JonghyunKim.NativeToolkit.Runtime.Clipboard;

// Intentional deviation from the "log every internal method" rule in csharp.md: this is a pure
// state machine with no side effects. The controller logs every transition it drives, so logging
// here would only duplicate those entries.

/// <summary>
/// Pure state machine for the sample's observation lifecycle.
/// </summary>
/// <remarks>
/// <para>
/// StartObserving and StopObserving share a single-flight key, so a call issued while another is
/// pending is rejected with CLIPBOARD_BUSY - and that rejection arrives on the rejected call's own
/// callback. Only the call that took ownership may change the state; otherwise a rejected second
/// start would clear <see cref="ControlPending"/> while the first start is still running natively,
/// and a later screen teardown would fail to stop the observation it eventually creates.
/// </para>
/// <para>
/// A failed start always means "not observing": the native manager stops the previous observation
/// before it resolves the new scope, so even a failed replacement leaves nothing subscribed.
/// </para>
/// </remarks>
internal struct IosClipboardSampleObservationState
{
    /// Token for a call that must not change this state (the rejected second start of the busy demo).
    internal const int NonOwningToken = 0;

    private int _nextToken;
    private int _controlOwner;

    internal bool IsObserving { get; private set; }

    internal bool ControlPending => _controlOwner != NonOwningToken;

    /// True when the screen was left while a start was still pending, so that start must stop itself.
    internal bool StopRequestedAfterStart { get; private set; }

    internal bool CanStartObserving => !IsObserving && !ControlPending;

    internal bool CanRestartObserving => IsObserving && !ControlPending;

    internal bool CanStopObserving => IsObserving && !ControlPending;

    internal bool CanChangeScope => !IsObserving && !ControlPending;

    /// <summary>
    /// Takes ownership for a start, or returns <see cref="NonOwningToken"/> when another control
    /// call is already pending.
    /// </summary>
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
    internal int BeginStop()
    {
        if (ControlPending) return NonOwningToken;
        _controlOwner = ++_nextToken;
        return _controlOwner;
    }

    /// <returns>True when this callback owned the state, so the caller may act on the transition.</returns>
    internal bool CompleteStart(int owner, bool isSuccess)
    {
        if (owner == NonOwningToken || owner != _controlOwner) return false;
        _controlOwner = NonOwningToken;
        if (isSuccess)
        {
            IsObserving = true;
        }
        else
        {
            // Native stops the previous observation before resolving the new scope, so a failed
            // start - first or replacement - leaves nothing to stop.
            IsObserving = false;
            StopRequestedAfterStart = false;
        }
        return true;
    }

    /// <returns>True when this callback owned the state, so the caller may act on the transition.</returns>
    internal bool CompleteStop(int owner, bool isSuccess)
    {
        if (owner == NonOwningToken || owner != _controlOwner) return false;
        _controlOwner = NonOwningToken;
        // Consumed in both outcomes: the deferred stop is issued at most once. Native stopObserving
        // cannot fail, so a failure here is a managed rejection that a retry would repeat.
        StopRequestedAfterStart = false;
        if (isSuccess)
        {
            IsObserving = false;
        }
        return true;
    }

    /// <summary>
    /// Records that the screen is going away. A stop cannot be issued while another control call is
    /// pending, so the pending call issues it instead once it completes.
    /// </summary>
    internal void RequestStop()
    {
        if (ControlPending)
        {
            StopRequestedAfterStart = true;
        }
    }

    internal bool ShouldIssueStopNow() => IsObserving && !ControlPending;
}

/// <summary>
/// One StartObserving call, described before it is issued.
/// </summary>
internal readonly struct IosClipboardSampleStartRequest
{
    internal string Marker { get; }

    /// Owner token from <see cref="IosClipboardSampleObservationState"/>, or NonOwningToken.
    internal int Owner { get; }

    /// The scope this call asks for. Not read from the controller later: a CreatePasteboard issued
    /// earlier can change the active scope while this start is pending.
    internal IosPasteboardScope TargetScope { get; }

    internal IosClipboardSampleStartRequest(string marker, int owner, IosPasteboardScope targetScope)
    {
        Marker = marker;
        Owner = owner;
        TargetScope = targetScope;
    }
}

/// <summary>
/// Builds the StartObserving requests the sample issues.
/// </summary>
/// <remarks>
/// Extracted from the controller so the marker, the ownership and - most importantly - which scope
/// each button aims at can be pinned in EditMode. The missing-named error button in particular must
/// not aim at the active scope, which is usually general and would make that button succeed.
/// </remarks>
internal static class IosClipboardSampleObservationRequests
{
    internal const string StartMarker = "observe.start";
    internal const string RestartMarker = "observe.restart";
    internal const string BusyFirstMarker = "observe.busy#1";
    internal const string BusySecondMarker = "observe.busy#2";
    internal const string MissingNamedMarker = "observe.err.missingNamed";

    internal const string MissingScopeNamePrefix = "com.jonghyunkim.nativetoolkit.example.missing.";

    internal static IosClipboardSampleStartRequest Start(
        ref IosClipboardSampleObservationState state, IosPasteboardScope activeScope) =>
        new(StartMarker, state.BeginStart(), activeScope);

    /// A second successful start replaces the previous observation without stopping it first.
    internal static IosClipboardSampleStartRequest Restart(
        ref IosClipboardSampleObservationState state, IosPasteboardScope activeScope) =>
        new(RestartMarker, state.BeginStart(), activeScope);

    /// <summary>
    /// The busy demo: the first call owns the state, the second exists only to be rejected with
    /// CLIPBOARD_BUSY and must never change it.
    /// </summary>
    internal static (IosClipboardSampleStartRequest First, IosClipboardSampleStartRequest Second) BusyPair(
        ref IosClipboardSampleObservationState state, IosPasteboardScope activeScope) =>
        (new IosClipboardSampleStartRequest(BusyFirstMarker, state.BeginStart(), activeScope),
         new IosClipboardSampleStartRequest(
             BusySecondMarker, IosClipboardSampleObservationState.NonOwningToken, activeScope));

    /// <summary>
    /// Targets a pasteboard that was never created, so resolving it fails natively with
    /// CLIPBOARD_UNAVAILABLE. A new name per call keeps a previous run from making it resolvable.
    /// The name is never shown or logged.
    /// </summary>
    internal static IosClipboardSampleStartRequest MissingNamed(ref IosClipboardSampleObservationState state) =>
        new(MissingNamedMarker,
            state.BeginStart(),
            IosPasteboardScope.Named(MissingScopeNamePrefix + Guid.NewGuid().ToString("N")));
}
#endif
