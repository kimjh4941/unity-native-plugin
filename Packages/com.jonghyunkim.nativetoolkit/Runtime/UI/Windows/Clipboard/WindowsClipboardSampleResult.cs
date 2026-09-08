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
    /// <param name="sequence">Monotonic sequence number.</param>
    /// <param name="kind">One of the Kind constants.</param>
    /// <param name="subject">Operation name from the result, or an event name.</param>
    /// <param name="body">Everything after the subject. May be empty.</param>
    /// <returns>The line.</returns>
    internal static string FormatLine(int sequence, string kind, string subject, string body) =>
        string.IsNullOrEmpty(body)
            ? $"#{sequence} [{kind}] {subject}"
            : $"#{sequence} [{kind}] {subject} {body}";

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
    /// <param name="sequence">Sequence number of the issuing call.</param>
    /// <param name="operation">Native operation name.</param>
    /// <param name="requestId">The id the Manager returned. Zero means it was rejected.</param>
    /// <returns>The line.</returns>
    /// <remarks>
    /// Zero is called out rather than shown as a number: it is the only value that means "no
    /// request exists", and the completion still arrives, so the two look alike in the log.
    /// </remarks>
    internal static string FormatAccept(int sequence, string operation, uint requestId) =>
        FormatLine(
            sequence, KindAccept, operation,
            requestId == 0
                ? "requestId=0 (rejected; the outcome arrives on the callback)"
                : $"requestId={requestId.ToString(CultureInfo.InvariantCulture)}");

    /// <summary>Formats a rejection this screen made before calling the Manager.</summary>
    /// <param name="call">Identity of the call.</param>
    /// <param name="detail">Short reason. Must not quote clipboard content.</param>
    /// <returns>The line.</returns>
    internal static string FormatLocal(in WindowsClipboardSampleCall call, string detail) =>
        FormatLine(call.Sequence, KindLocal, call.Marker, detail);

    /// <summary>Describes a text read without disclosing it.</summary>
    /// <param name="result">The read result.</param>
    /// <param name="expectedHash">Digest of what was last written, or 0 when nothing was.</param>
    /// <returns>Length, emptiness and whether it matches what this screen wrote.</returns>
    internal static string DescribeText(in WindowsClipboardTextResult result, ulong expectedHash)
    {
        int length = result.Text?.Length ?? -1;
        return $"empty={result.IsEmpty} length={length} " +
               $"match={MatchLabel(expectedHash, WindowsClipboardSampleFixtures.HashOf(result.Text))}";
    }

    /// <summary>Describes a byte read without disclosing it.</summary>
    /// <param name="result">The read result.</param>
    /// <param name="expectedHash">Digest of what was last written, or 0 when nothing was.</param>
    /// <returns>Size, emptiness and whether it matches what this screen wrote.</returns>
    internal static string DescribeBytes(in WindowsClipboardBytesResult result, ulong expectedHash) =>
        $"empty={result.IsEmpty} size={result.Data.Length} " +
        $"match={MatchLabel(expectedHash, WindowsClipboardSampleFixtures.HashOf(result.Data))}";

    /// <summary>Describes a string list read by shape only.</summary>
    /// <param name="result">The read result.</param>
    /// <returns>Count and emptiness. The values themselves are format names or paths and are not shown.</returns>
    internal static string DescribeStringList(in WindowsClipboardStringListResult result) =>
        $"empty={result.IsEmpty} count={result.Values.Count}";

    /// <summary>
    /// Describes a history read by shape only.
    /// </summary>
    /// <param name="result">The read result.</param>
    /// <returns>Count, emptiness, and the age of the newest item in seconds.</returns>
    /// <remarks>
    /// The age is what makes the list comparable against Win+V without showing an item's text:
    /// "the newest entry is a few seconds old" is checkable by eye, the text is not needed.
    /// </remarks>
    internal static string DescribeHistory(in WindowsClipboardHistoryResult result, long nowUnixSeconds)
    {
        string newest = NotApplicable;
        if (result.Items.Count > 0)
        {
            System.DateTimeOffset? at = result.Items[0].ToUtcTime();
            if (at != null)
            {
                newest = (nowUnixSeconds - at.Value.ToUnixTimeSeconds())
                    .ToString(CultureInfo.InvariantCulture);
            }
        }
        return $"empty={result.IsEmpty} count={result.Items.Count} newestAgeSec={newest}";
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
        WindowsClipboardErrorCode code)
    {
        if (operation == WindowsClipboardManager.OperationInitialize)
        {
            return isSuccess ? WindowsClipboardSampleState.Running : current;
        }

        if (operation != WindowsClipboardManager.OperationShutdown) return current;

        if (isSuccess) return WindowsClipboardSampleState.ShutDown;

        // The drain gave up. Operations keep being refused from here and no call reopens it, which
        // is the difference the ShuttingDown code cannot express.
        if (code == WindowsClipboardErrorCode.ShutdownTimeout)
        {
            return WindowsClipboardSampleState.ShutdownFailed;
        }

        // Anything else is a shutdown that has not finished yet, including the not-yet answers a
        // drain makes on its way through.
        return current == WindowsClipboardSampleState.ShutDown
            ? current
            : WindowsClipboardSampleState.Draining;
    }

    /// <summary>
    /// Formats the status line.
    /// </summary>
    /// <param name="pending">Accepted asynchronous requests that have not completed.</param>
    /// <param name="events">How many common events have arrived.</param>
    /// <param name="lastChangedSequence">Sequence of the last ClipboardChanged, or 0.</param>
    /// <param name="changedCount">How many ClipboardChanged events have arrived.</param>
    /// <param name="renderCount">How many times a deferred provider has run.</param>
    /// <param name="ansiCodePage">The culture's ANSI code page.</param>
    /// <returns>The line.</returns>
    /// <remarks>
    /// The change count is deliberately not the headline. One external copy fires it about three
    /// times, so a prominent counter makes correct behaviour look broken on every single check.
    /// The sequence of the last one is what a manual check actually needs.
    /// </remarks>
    internal static string FormatStatus(
        int pending,
        int events,
        int lastChangedSequence,
        int changedCount,
        int renderCount,
        int ansiCodePage)
    {
        string changed = lastChangedSequence == 0
            ? "-"
            : $"#{lastChangedSequence.ToString(CultureInfo.InvariantCulture)} (x{changedCount})";
        return $"Pending: {pending} | Events: {events} | Changed: {changed} " +
               $"| Render: {renderCount} | ACP: {ansiCodePage}";
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
