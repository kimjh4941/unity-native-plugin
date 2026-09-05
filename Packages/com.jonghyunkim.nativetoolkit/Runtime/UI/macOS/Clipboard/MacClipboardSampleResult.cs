#nullable enable

#if UNITY_STANDALONE_OSX || UNITY_EDITOR
using System;
using System.Collections.Generic;
using JonghyunKim.NativeToolkit.Runtime.Clipboard;

// Intentional deviation from the "log every internal method" rule in csharp.md: every member here
// is a pure formatter called once per result line. Logging inside them would duplicate the
// caller's own entry, and several describe clipboard content shape, which must not be recorded
// more often than necessary.

/// <summary>
/// Immutable identity of one <c>MacClipboardManager</c> call, captured by that call's per-call
/// callback.
/// </summary>
/// <remarks>
/// The Manager serializes only same-operation calls, so Read and Snapshot genuinely overlap. A
/// single "pending marker" field would label a completing call with whichever marker was set
/// last. Capturing the context per call is the only way to keep a result line correlated with the
/// call that produced it.
/// </remarks>
internal readonly struct MacClipboardSampleResultContext
{
    internal int Sequence { get; }

    internal string Marker { get; }

    internal MacClipboardSampleResultContext(int sequence, string marker)
    {
        Sequence = sequence;
        Marker = marker;
    }
}

/// <summary>
/// Pure formatters for the sample's result line and status line, plus the error redaction the
/// screen depends on.
/// </summary>
/// <remarks>
/// Kept separate from the controller so the redaction rules and the correlation rules can be
/// tested in EditMode without a UIDocument.
/// </remarks>
internal static class MacClipboardSampleResult
{
    internal const string StatusObservingOff = "off";
    internal const string StatusObservingStarting = "starting";
    internal const string StatusObservingOn = "on";
    internal const string StatusObservingPending = "on (pending)";

    /// <summary>Shown when a judgement could not be made rather than when it failed.</summary>
    internal const string NotApplicable = "n/a";

    /// <summary>Reason token for a code this package does not know.</summary>
    internal const string UnmappedReason = "unmapped";

    /// <summary>
    /// Error codes whose arrival the manual pass is meant to confirm, from v12 section 7.5.
    /// </summary>
    /// <remarks>
    /// The denominator stays at ten even though two of them need a specific environment: 1513
    /// only appears below macOS 15.4 and 1514 only when the user denies the detection prompt.
    /// Shrinking the set per machine would hide why the counter stops short.
    /// </remarks>
    internal static readonly int[] TrackedErrorCodes =
    {
        MacClipboardErrorCodes.ContractViolation,               // 1302, item 17c
        MacClipboardErrorCodes.EmptyDetectionPatterns,          // 1503, item 17b
        MacClipboardErrorCodes.CannotReleaseStandardPasteboard, // 1508, item 10
        MacClipboardErrorCodes.OwnershipLost,                   // 1511, item 3
        MacClipboardErrorCodes.EmptyTypeFilter,                 // 1512, item 7
        MacClipboardErrorCodes.DetectionUnavailable,            // 1513, items 11 and 13
        MacClipboardErrorCodes.DetectionDenied,                 // 1514, item 12
        MacClipboardErrorCodes.DetectionFailed,                 // 1515, item 13
        MacClipboardErrorCodes.InvalidConfiguration,            // 1523, item 17
        MacClipboardErrorCodes.RequestTooLarge,                 // 9007, item 23
    };

    // Every code in MacClipboardErrorCodes maps to a token the screen owns. The native message is
    // never shown: it embeds pasteboard names, uniform type identifiers and OS supplied reasons,
    // and which of its cases are dynamic is a native implementation detail that can change
    // without this package noticing. A token table cannot leak whatever native decides to write.
    private static readonly Dictionary<int, string> ReasonTokens = new()
    {
        [MacClipboardErrorCodes.ParseFailed] = "parseFailed",
        [MacClipboardErrorCodes.ContractViolation] = "contractViolation",
        [MacClipboardErrorCodes.EmptyContent] = "emptyContent",
        [MacClipboardErrorCodes.EmptyRepresentations] = "emptyRepresentations",
        [MacClipboardErrorCodes.EmptyDetectionPatterns] = "emptyPatterns",
        [MacClipboardErrorCodes.InvalidTypeIdentifier] = "invalidTypeIdentifier",
        [MacClipboardErrorCodes.InvalidPasteboardName] = "invalidPasteboardName",
        [MacClipboardErrorCodes.ContentTooLarge] = "contentTooLarge",
        [MacClipboardErrorCodes.PasteboardUnavailable] = "pasteboardUnavailable",
        [MacClipboardErrorCodes.CannotReleaseStandardPasteboard] = "standardPasteboard",
        [MacClipboardErrorCodes.WriteRejected] = "writeRejected",
        [MacClipboardErrorCodes.AppendRejected] = "appendRejected",
        [MacClipboardErrorCodes.OwnershipLost] = "ownershipLost",
        [MacClipboardErrorCodes.EmptyTypeFilter] = "emptyTypeFilter",
        [MacClipboardErrorCodes.DetectionUnavailable] = "detectionUnavailable",
        [MacClipboardErrorCodes.DetectionDenied] = "detectionDenied",
        [MacClipboardErrorCodes.DetectionFailed] = "detectionFailed",
        [MacClipboardErrorCodes.PasteLoadFailed] = "pasteLoadFailed",
        [MacClipboardErrorCodes.PasteLoadTimedOut] = "pasteLoadTimedOut",
        [MacClipboardErrorCodes.InvalidConfiguration] = "invalidConfiguration",
        [MacClipboardErrorCodes.Cancelled] = "cancelled",
        [MacClipboardErrorCodes.Unknown] = "unknown",
        [MacClipboardErrorCodes.Busy] = "busy",
        [MacClipboardErrorCodes.BridgeUnavailable] = "bridgeUnavailable",
        [MacClipboardErrorCodes.MainThreadRequired] = "mainThreadRequired",
        [MacClipboardErrorCodes.ManagerDestroyed] = "managerDestroyed",
        [MacClipboardErrorCodes.InvalidRequest] = "invalidRequest",
        [MacClipboardErrorCodes.ResponseParseFailed] = "responseParseFailed",
        [MacClipboardErrorCodes.RequestTooLarge] = "requestTooLarge",
    };

    /// <summary>
    /// Maps an error code to the token shown in place of the native message.
    /// </summary>
    /// <param name="code">Numeric error code from a result.</param>
    /// <returns>The token, or <see cref="UnmappedReason"/> for a code this package does not know.</returns>
    internal static string ReasonFor(int code) =>
        ReasonTokens.TryGetValue(code, out string? token) ? token : UnmappedReason;

    /// <summary>Whether a code has a token, used by the table completeness test.</summary>
    /// <param name="code">Numeric error code.</param>
    /// <returns><c>true</c> when the code is mapped.</returns>
    internal static bool HasReason(int code) => ReasonTokens.ContainsKey(code);

    internal static string FormatRunning(in MacClipboardSampleResultContext context) =>
        $"#{context.Sequence} [{context.Marker}] ...";

    internal static string FormatSuccess(in MacClipboardSampleResultContext context, string payload) =>
        string.IsNullOrEmpty(payload)
            ? $"#{context.Sequence} [{context.Marker}] OK"
            : $"#{context.Sequence} [{context.Marker}] OK {payload}";

    /// <summary>
    /// Formats a failure. The native message is deliberately absent.
    /// </summary>
    /// <param name="context">Identity of the call that failed.</param>
    /// <param name="error">Error detail from the result.</param>
    /// <returns>A line carrying the code and a locally owned reason token.</returns>
    internal static string FormatFailure(
        in MacClipboardSampleResultContext context, MacClipboardErrorInfo error) =>
        $"#{context.Sequence} [{context.Marker}] NG code={error.Code} reason={ReasonFor(error.Code)}";

    /// <summary>Local rejection, such as a factory that threw before the Manager was reached.</summary>
    /// <param name="context">Identity of the call.</param>
    /// <param name="detail">Short description that must not quote an exception message.</param>
    /// <returns>A line marked as a local rejection rather than a native failure.</returns>
    internal static string FormatLocal(in MacClipboardSampleResultContext context, string detail) =>
        $"#{context.Sequence} [{context.Marker}] -- local={detail}";

    /// <summary>
    /// Describes an exception without quoting its message.
    /// </summary>
    /// <remarks>
    /// Argument exceptions name the parameter, and a pasteboard name is one of the values that
    /// reaches them. The type name is enough to tell the failure modes apart.
    /// </remarks>
    /// <param name="exception">Exception to describe.</param>
    /// <returns>The exception type name.</returns>
    internal static string DescribeException(Exception exception) => exception.GetType().Name;

    /// <summary>
    /// Describes a scope without disclosing its name.
    /// </summary>
    /// <param name="scope">Scope to describe, or <c>null</c>.</param>
    /// <returns>The kind, with a name length for the kinds that carry one.</returns>
    internal static string FormatScopeLabel(MacPasteboardScope? scope)
    {
        if (scope == null) return "(none)";
        return scope.Kind switch
        {
            MacPasteboardScopeKind.General => "general",
            MacPasteboardScopeKind.Named => $"named(len={scope.Name?.Length ?? 0})",
            MacPasteboardScopeKind.Unique => $"unique(len={scope.Name?.Length ?? 0})",
            _ => "(unknown)"
        };
    }

    /// <summary>
    /// Judges manual check 4: did the pasteboard derive representations beyond what was written.
    /// </summary>
    /// <param name="fresh">
    /// Whether the pasteboard still holds this app's write. Established by comparing the change
    /// count captured at Copy with the one Read returned; another app copying in between makes
    /// the comparison meaningless rather than false.
    /// </param>
    /// <param name="singleWrittenItem">Whether the write placed exactly one item.</param>
    /// <param name="writtenTypes">Representation count that was written.</param>
    /// <param name="readTypes">Representation count on the first item that was read.</param>
    /// <returns><c>true</c>, <c>false</c>, or <see cref="NotApplicable"/>.</returns>
    internal static string FormatDerived(
        bool fresh, bool singleWrittenItem, int writtenTypes, int readTypes)
    {
        if (!fresh || !singleWrittenItem) return NotApplicable;
        return readTypes > writtenTypes ? "true" : "false";
    }

    /// <summary>
    /// Judges manual check 25: did non-ASCII text survive the round trip.
    /// </summary>
    /// <param name="fresh">As in <see cref="FormatDerived"/>.</param>
    /// <param name="sameTypeFound">Whether the read contained the type that was written.</param>
    /// <param name="hashMatches">Whether the read bytes hash to the written bytes' hash.</param>
    /// <returns><c>match</c>, <c>differ</c>, or <see cref="NotApplicable"/>.</returns>
    /// <remarks>
    /// A missing type is reported as not applicable rather than as a mismatch: nothing was
    /// compared, so calling it a failed round trip would be a false accusation.
    /// </remarks>
    internal static string FormatRoundTrip(bool fresh, bool sameTypeFound, bool hashMatches)
    {
        if (!fresh || !sameTypeFound) return NotApplicable;
        return hashMatches ? "match" : "differ";
    }

    internal static string FormatObservingState(bool isObserving, bool controlPending)
    {
        if (isObserving) return controlPending ? StatusObservingPending : StatusObservingOn;
        return controlPending ? StatusObservingStarting : StatusObservingOff;
    }

    /// <summary>
    /// Formats the reached-code counter.
    /// </summary>
    /// <param name="reached">Codes seen so far, in ascending order.</param>
    /// <returns>The codes and how many of the tracked set they cover.</returns>
    internal static string FormatReachedCodes(IReadOnlyCollection<int> reached)
    {
        int tracked = 0;
        foreach (int code in TrackedErrorCodes)
        {
            if (Contains(reached, code)) tracked++;
        }
        string list = reached.Count == 0 ? "-" : string.Join(",", reached);
        return $"{list} ({tracked}/{TrackedErrorCodes.Length})";
    }

    private static bool Contains(IReadOnlyCollection<int> values, int value)
    {
        foreach (int v in values)
        {
            if (v == value) return true;
        }
        return false;
    }

    /// <remarks>
    /// The observed scope is shown next to the active one because a CreatePasteboard issued before
    /// a start can complete while that start is still pending, leaving the two different.
    /// </remarks>
    internal static string FormatStatus(
        MacPasteboardScope activeScope,
        MacPasteboardScope? observedScope,
        bool isObserving,
        bool controlPending,
        int eventCount,
        IReadOnlyCollection<int> reachedCodes)
    {
        string scopeText = observedScope == null
            ? FormatScopeLabel(activeScope)
            : $"{FormatScopeLabel(activeScope)} (observing {FormatScopeLabel(observedScope)})";
        return $"Scope: {scopeText} | Observing: {FormatObservingState(isObserving, controlPending)} " +
               $"| Events: {eventCount} | Codes: {FormatReachedCodes(reachedCodes)}";
    }
}
#endif
