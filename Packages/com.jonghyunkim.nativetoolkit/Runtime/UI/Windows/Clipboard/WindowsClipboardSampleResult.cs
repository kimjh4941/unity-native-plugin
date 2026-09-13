#nullable enable

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using JonghyunKim.NativeToolkit.Runtime.Clipboard;

// Intentional deviation from the "log every internal method" rule in csharp.md: every member here
// is a pure formatter called once per result line. Logging inside them would duplicate the
// caller's own entry, and several describe clipboard content shape, which must not be recorded
// more often than necessary.

/// <summary>
/// The lifecycle state the screen tracks on the Manager's behalf.
/// </summary>
/// <remarks>
/// The Manager answers <c>Draining</c> and <c>ShutdownFailed</c> with the same ShuttingDown(1011),
/// so an error code cannot tell "a shutdown is still making progress" from "a shutdown gave up and
/// nothing short of a restart will recover it". Rather than add a state query to the product code
/// for the sample's benefit, the screen follows the transitions it can see from its own calls.
/// </remarks>
internal enum WindowsClipboardSampleState
{
    /// <summary>Nothing has been observed yet. Not a claim that the Manager is uninitialised.</summary>
    Unknown,

    Running,

    Draining,

    ShutDown,

    ShutdownFailed
}

/// <summary>
/// Identity of one call, captured when it is issued so the completion can quote it back.
/// </summary>
/// <remarks>
/// Asynchronous history calls overlap, and several of the checks deliberately issue two in one
/// frame. A single pending-marker field would label whichever completion arrived first with the
/// marker set last, which on a verification harness reads as a check that ran against the wrong
/// operation.
/// </remarks>
internal readonly struct WindowsClipboardSampleCall
{
    internal int Sequence { get; }

    /// <summary>The button-side label. Used for local rejections that never reach a result.</summary>
    internal string Marker { get; }

    internal WindowsClipboardSampleCall(int sequence, string marker)
    {
        Sequence = sequence;
        Marker = marker;
    }
}

/// <summary>
/// Pure formatters for the Windows clipboard sample's result log, status line and state line.
/// </summary>
/// <remarks>
/// <para>
/// Separated from the controller so the two rules the screen depends on can be tested in EditMode
/// without a UIDocument: that a line never carries clipboard content, and that sequence numbers
/// increase so the delivery order a manual check reads is the real one.
/// </para>
/// <para>
/// Unlike the macOS sample, the native error message is shown. Every
/// <c>WindowsClipboardResult.Failure</c> detail in this package is a fixed literal with no path
/// from clipboard content into it, so there is nothing to redact and hiding it would only make a
/// device log harder to read.
/// </para>
/// </remarks>
internal static class WindowsClipboardSampleResult
{
    /// <summary>A synchronous return value.</summary>
    internal const string KindCall = "call";

    /// <summary>An asynchronous request that was accepted, carrying its request id.</summary>
    internal const string KindAccept = "accept";

    /// <summary>An asynchronous completion, whether it succeeded, failed or was rejected.</summary>
    internal const string KindDone = "done";

    /// <summary>A common event, mixed into the same timeline as the calls.</summary>
    internal const string KindEvent = "event";

    /// <summary>The screen rejected the operation before the Manager was reached.</summary>
    internal const string KindLocal = "local";

    /// <summary>Shown when a judgement could not be made, rather than when it came out false.</summary>
    internal const string NotApplicable = "n/a";

    /// <summary>
    /// Formats one line of the result log.
    /// </summary>
    /// <param name="line">Line number. Increases with every line written, without exception.</param>
    /// <param name="kind">One of the Kind constants.</param>
    /// <param name="subject">Operation name from the result, or an event name.</param>
    /// <param name="body">Everything after the subject. May be empty.</param>
    /// <param name="call">
    /// Line number of the call this line belongs to, or <c>0</c> for a line that belongs to none.
    /// </param>
    /// <returns>The line.</returns>
    /// <remarks>
    /// The line number and the call it belongs to are separate. Reusing one number for a call and
    /// its completion made the log run 1, 2, 1: the common event that landed between them took a
    /// number of its own, and the completion went back to the caller's. Delivery order is the only
    /// reason these numbers exist, so the ordering rules the design promises - that a result
    /// arrives outside the caller's stack, that the event precedes the callback - could not be read
    /// off a log that counts backwards.
    /// </remarks>
    internal static string FormatLine(
        int line, string kind, string subject, string body, int call = 0)
    {
        string origin = call == 0 || call == line ? string.Empty : $" call=#{call}";
        string tail = string.IsNullOrEmpty(body) ? string.Empty : $" {body}";
        return $"#{line} [{kind}] {subject}{origin}{tail}";
    }

    /// <summary>
    /// Formats the outcome half of a line.
    /// </summary>
    /// <param name="isSuccess">Whether the operation succeeded.</param>
    /// <param name="code">Error code from the result.</param>
    /// <param name="message">Error message from the result.</param>
    /// <param name="shape">Content-free description of what came back. May be empty.</param>
    /// <returns>The outcome text.</returns>
    /// <remarks>
    /// A success whose code is not None, and a failure with no message, are both contract breaks
    /// (design 8.4). Neither is silently normalised here: the line shows what the result actually
    /// carried, because the About section asks the operator to treat exactly those as defects.
    /// </remarks>
    internal static string FormatOutcome(
        bool isSuccess, WindowsClipboardErrorCode code, string? message, string shape)
    {
        string head = isSuccess ? "OK" : "NG";
        string codeText = $"code={code}";
        string messageText = message == null ? string.Empty : $" msg={message}";
        string shapeText = string.IsNullOrEmpty(shape) ? string.Empty : $" {shape}";
        return $"{head} {codeText}{messageText}{shapeText}";
    }

    /// <summary>Formats the accept line of an asynchronous call.</summary>
    /// <param name="line">Line number for this line.</param>
    /// <param name="operation">Native operation name.</param>
    /// <param name="requestId">The id the Manager returned. Zero means it was rejected.</param>
    /// <param name="call">Line number of the call this belongs to.</param>
    /// <returns>The line.</returns>
    /// <remarks>
    /// Zero is called out rather than shown as a number: it is the only value that means "no
    /// request exists", and the completion still arrives, so the two look alike in the log.
    /// </remarks>
    internal static string FormatAccept(int line, string operation, uint requestId, int call) =>
        FormatLine(
            line, KindAccept, operation,
            requestId == 0
                ? "requestId=0 (rejected; the outcome arrives on the callback)"
                : $"requestId={requestId.ToString(CultureInfo.InvariantCulture)}",
            call);

    /// <summary>Formats a rejection this screen made before calling the Manager.</summary>
    /// <param name="call">Identity of the call.</param>
    /// <param name="detail">Short reason. Must not quote clipboard content.</param>
    /// <returns>The line.</returns>
    internal static string FormatLocal(int line, in WindowsClipboardSampleCall call, string detail) =>
        FormatLine(line, KindLocal, call.Marker, detail, call.Sequence);

    /// <summary>Shape fields for a read that never happened.</summary>
    /// <param name="sizeField">Name of the size-like field this result carries.</param>
    /// <remarks>
    /// A failed read measured nothing, so reporting a number claims a measurement.
    /// "empty=False count=0" was the shape it printed before, which reads as "not empty,
    /// and zero items" - the operator has to know the library's defaults to see that
    /// neither half is an observation. n/a is the same answer the comparison already gave.
    /// </remarks>
    private static string NothingWasRead(string sizeField) =>
        $"empty={NotApplicable} {sizeField}={NotApplicable} match={NotApplicable}";

    /// <summary>Describes a text read without disclosing it.</summary>
    /// <param name="result">The read result.</param>
    /// <param name="expectedHash">Digest of what was last written, or 0 when nothing was.</param>
    /// <returns>Length, emptiness and whether it matches what this screen wrote.</returns>
    /// <remarks>
    /// A failed read compared nothing, so it reports the same "not applicable" as a read with no
    /// anchor. Putting its absent text through the comparison would print "differ" beside the
    /// error, accusing the round trip of losing content that was never handed back.
    /// </remarks>
    internal static string DescribeText(in WindowsClipboardTextResult result, ulong expectedHash)
    {
        if (!result.IsSuccess) return NothingWasRead("length");

        int length = result.Text?.Length ?? -1;
        string match = MatchLabel(expectedHash, WindowsClipboardSampleFixtures.HashOf(result.Text));
        return $"empty={result.IsEmpty} length={length} match={match}";
    }

    /// <summary>Describes a byte read without disclosing it.</summary>
    /// <param name="result">The read result.</param>
    /// <param name="expectedHash">Digest of what was last written, or 0 when nothing was.</param>
    /// <returns>Size, emptiness and whether it matches what this screen wrote.</returns>
    /// <remarks>As in <see cref="DescribeText"/>, a failed read reports no comparison.</remarks>
    internal static string DescribeBytes(in WindowsClipboardBytesResult result, ulong expectedHash)
    {
        if (!result.IsSuccess) return NothingWasRead("size");

        string match = MatchLabel(expectedHash, WindowsClipboardSampleFixtures.HashOf(result.Data));
        return $"empty={result.IsEmpty} size={result.Data.Length} match={match}";
    }

    /// <summary>Describes a string list read by shape only.</summary>
    /// <param name="result">The read result.</param>
    /// <returns>Count and emptiness. The values themselves are format names or paths and are not shown.</returns>
    internal static string DescribeStringList(in WindowsClipboardStringListResult result) =>
        result.IsSuccess
            ? $"empty={result.IsEmpty} count={result.Values.Count}"
            : $"empty={NotApplicable} count={NotApplicable}";

    /// <summary>
    /// Describes a history read by shape only.
    /// </summary>
    /// <param name="result">The read result.</param>
    /// <param name="nowUnixSeconds">Current time, for the age of the newest item.</param>
    /// <param name="expectedHash">Digest of what was last written, or 0 when nothing was.</param>
    /// <returns>Count, emptiness, the newest item's age, and whether it is this screen's own write.</returns>
    /// <remarks>
    /// Age alone cannot answer what the history check asks. Another application copying a moment
    /// earlier also leaves a newest entry a few seconds old, so "recent" reads as a pass while the
    /// list being held up against Win+V belongs to someone else. The match flag separates the two,
    /// and it costs nothing in disclosure: the item's text is hashed, never shown.
    /// </remarks>
    internal static string DescribeHistory(
        in WindowsClipboardHistoryResult result, long nowUnixSeconds, ulong expectedHash)
    {
        if (!result.IsSuccess)
        {
            return $"empty={NotApplicable} count={NotApplicable} " +
                   $"newestAgeSec={NotApplicable} newestMatch={NotApplicable}";
        }

        string newest = NotApplicable;
        string match = NotApplicable;
        if (result.Items.Count > 0)
        {
            System.DateTimeOffset? at = result.Items[0].ToUtcTime();
            if (at != null)
            {
                newest = (nowUnixSeconds - at.Value.ToUnixTimeSeconds())
                    .ToString(CultureInfo.InvariantCulture);
            }
            match = MatchLabel(expectedHash, WindowsClipboardSampleFixtures.HashOf(result.Items[0].Text));
        }
        return $"empty={result.IsEmpty} count={result.Items.Count} " +
               $"newestAgeSec={newest} newestMatch={match}";
    }

    /// <summary>
    /// Compares two digests.
    /// </summary>
    /// <param name="expected">Digest of what was written, or 0 when nothing was.</param>
    /// <param name="actual">Digest of what came back.</param>
    /// <returns><c>match</c>, <c>differ</c>, or <see cref="NotApplicable"/>.</returns>
    /// <remarks>
    /// No anchor means nothing was compared. Reporting that as a mismatch would accuse the round
    /// trip of losing content it was never given, which is how a check reaches a wrong verdict
    /// without anyone noticing it never ran.
    /// </remarks>
    internal static string MatchLabel(ulong expected, ulong actual)
    {
        if (expected == 0UL) return NotApplicable;
        return expected == actual ? "match" : "differ";
    }

    /// <summary>
    /// Advances the tracked lifecycle state.
    /// </summary>
    /// <param name="current">State before this result.</param>
    /// <param name="operation">Native operation name from the result.</param>
    /// <param name="isSuccess">Whether it succeeded.</param>
    /// <param name="code">Error code from the result.</param>
    /// <param name="completed">
    /// The out argument of <c>TryShutdown</c>, or <c>null</c> for the calls that have none.
    /// </param>
    /// <returns>The state after this result.</returns>
    /// <remarks>
    /// Only shutdown and initialize move it. Every other operation can fail for reasons that say
    /// nothing about the lifecycle, and letting those move the state would make the line drift away
    /// from the Manager it is meant to describe.
    /// </remarks>
    internal static WindowsClipboardSampleState Advance(
        WindowsClipboardSampleState current,
        string operation,
        bool isSuccess,
        WindowsClipboardErrorCode code,
        bool? completed = null)
    {
        if (operation == WindowsClipboardManager.OperationInitialize)
        {
            return isSuccess ? WindowsClipboardSampleState.Running : current;
        }

        if (operation != WindowsClipboardManager.OperationShutdown) return current;

        // Refused before any native attempt was made, so the Manager's state never moved. Claiming
        // a transition here would leave the line describing a shutdown that was never tried.
        if (code == WindowsClipboardErrorCode.MainThreadRequired ||
            code == WindowsClipboardErrorCode.ManagerDestroyed)
        {
            return current;
        }

        if (isSuccess)
        {
            // A shutdown reporting no error is not the same as a finished one. ClassifyShutdown
            // maps None with completed:false to NotYet, and FinishShutdownAttempt then leaves the
            // Manager at Draining. Reading that as ShutDown puts this line one state ahead of the
            // Manager, and the next operation comes back ShuttingDown for no visible reason.
            // The Editor pins completed to true, so the disagreement only appears on a device.
            return completed == false
                ? WindowsClipboardSampleState.Draining
                : WindowsClipboardSampleState.ShutDown;
        }

        // Which failures leave a drain able to continue is not ours to decide: ClassifyShutdown
        // owns that list and FinishShutdownAttempt latches ShutdownFailed for everything outside
        // it. Singling out ShutdownTimeout would call a terminal failure "still making progress",
        // inverting the one distinction this enum exists to draw.
        return KeepsDraining(code)
            ? WindowsClipboardSampleState.Draining
            : WindowsClipboardSampleState.ShutdownFailed;
    }

    /// <summary>
    /// Whether a shutdown failure leaves the drain able to continue.
    /// </summary>
    /// <param name="code">Error code from a shutdown result.</param>
    /// <returns><c>true</c> when another attempt can still finish the shutdown.</returns>
    /// <remarks>
    /// Mirrors the retry set in <c>WindowsClipboardManager.ClassifyShutdown</c>. Kept as its own
    /// function so the two lists can be read side by side rather than inferred from a chain of
    /// conditions.
    /// </remarks>
    internal static bool KeepsDraining(WindowsClipboardErrorCode code) =>
        code == WindowsClipboardErrorCode.None ||
        code == WindowsClipboardErrorCode.Busy ||
        code == WindowsClipboardErrorCode.MonitorRegisterFailed ||
        code == WindowsClipboardErrorCode.Canceled ||
        code == WindowsClipboardErrorCode.PartialState;

    /// <summary>
    /// Formats the status line.
    /// </summary>
    /// <param name="pending">Accepted asynchronous requests that have not completed.</param>
    /// <param name="events">How many common events have arrived.</param>
    /// <param name="lastChangedSequence">Sequence of the last ClipboardChanged, or 0.</param>
    /// <param name="changedCount">How many ClipboardChanged events have arrived.</param>
    /// <param name="renderText">How many times the text provider has run.</param>
    /// <param name="renderImage">How many times the image provider has run.</param>
    /// <param name="ansiCodePage">The culture's ANSI code page.</param>
    /// <param name="hasHistoryItemId">Whether a history id from a previous read is held.</param>
    /// <returns>The line.</returns>
    /// <remarks>
    /// <para>
    /// The change count is deliberately not the headline. One external copy fires it about three
    /// times, so a prominent counter makes correct behaviour look broken on every single check.
    /// The sequence of the last one is what a manual check actually needs.
    /// </para>
    /// <para>
    /// The render counts are per format rather than a total. Each provider is asked once, so what
    /// is being watched is that no single format goes above one. A sum reads as two the moment the
    /// receiving application asks for both formats, and correct behaviour then looks like a
    /// duplicate call.
    /// </para>
    /// </remarks>
    internal static string FormatStatus(
        int pending,
        int events,
        int lastChangedSequence,
        int changedCount,
        int renderText,
        int renderImage,
        int ansiCodePage,
        bool hasHistoryItemId = false)
    {
        string changed = lastChangedSequence == 0
            ? "-"
            : $"#{lastChangedSequence.ToString(CultureInfo.InvariantCulture)} (x{changedCount})";
        return $"Pending: {pending} | Events: {events} | Changed: {changed} " +
               $"| Render: text={renderText} image={renderImage} | ACP: {ansiCodePage} " +
               $"| HistoryId: {(hasHistoryItemId ? "held" : "none")}";
    }

    /// <summary>Formats the lifecycle line.</summary>
    /// <param name="state">The tracked state.</param>
    /// <param name="managerEnabled">Whether the Manager component is enabled.</param>
    /// <returns>The line.</returns>
    internal static string FormatState(WindowsClipboardSampleState state, bool managerEnabled) =>
        $"State: {state} | Manager enabled: {managerEnabled}";

    /// <summary>Joins the parts of a shape description, skipping the empty ones.</summary>
    /// <param name="parts">Parts to join.</param>
    /// <returns>The joined text.</returns>
    internal static string Join(params string[] parts)
    {
        var kept = new List<string>(parts.Length);
        foreach (string part in parts)
        {
            if (!string.IsNullOrEmpty(part)) kept.Add(part);
        }
        return string.Join(" ", kept);
    }
}
#endif
