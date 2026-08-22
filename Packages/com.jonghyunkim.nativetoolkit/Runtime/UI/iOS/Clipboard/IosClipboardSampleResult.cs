#nullable enable

#if UNITY_IOS || UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using JonghyunKim.NativeToolkit.Runtime.Clipboard;

// Intentional deviation from the "log every internal method" rule in csharp.md: every member here
// is a pure string formatter called once per result line. Logging inside them would duplicate the
// caller's own log entry, and several of them describe clipboard content shape, which must not be
// logged more often than necessary.

/// <summary>
/// Immutable identity of one <c>IosClipboardManager</c> call, captured by that call's per-call
/// callback.
/// </summary>
/// <remarks>
/// The Manager serializes only same-operation calls, so Read and GetSnapshot - or LoadItem and
/// CancelLoads - genuinely overlap. A single "pending marker" field would therefore label a
/// completing call with whichever marker was set last. Capturing the context per call is the only
/// way to keep the result line correlated with the call that produced it.
/// </remarks>
internal readonly struct IosClipboardSampleResultContext
{
    internal int Sequence { get; }

    internal string Marker { get; }

    internal IosClipboardSampleResultContext(int sequence, string marker)
    {
        Sequence = sequence;
        Marker = marker;
    }
}

/// <summary>
/// Pure formatters for the sample's result line and status line.
/// </summary>
/// <remarks>
/// Kept separate from the controller so the correlation rules (a completion is labelled with its
/// own context, never with the newest one) can be tested in EditMode without a UIDocument.
/// </remarks>
internal static class IosClipboardSampleResult
{
    /// Native code for a load that was cancelled on purpose. Shown as "--" rather than "NG".
    internal const string CancelledErrorCode = "CLIPBOARD_CANCELLED";

    internal const string StatusObservingOff = "off";
    internal const string StatusObservingStarting = "starting";
    internal const string StatusObservingOn = "on";
    internal const string StatusObservingPending = "on (pending)";

    internal static string FormatRunning(in IosClipboardSampleResultContext context) =>
        $"#{context.Sequence} [{context.Marker}] ...";

    internal static string FormatSuccess(in IosClipboardSampleResultContext context, string payload) =>
        string.IsNullOrEmpty(payload)
            ? $"#{context.Sequence} [{context.Marker}] OK"
            : $"#{context.Sequence} [{context.Marker}] OK {payload}";

    internal static string FormatFailure(in IosClipboardSampleResultContext context, IosClipboardErrorInfo error)
    {
        string mark = error.Code == CancelledErrorCode ? "--" : "NG";
        var builder = new StringBuilder();
        builder.Append('#').Append(context.Sequence)
               .Append(" [").Append(context.Marker).Append("] ")
               .Append(mark)
               .Append(" code=").Append(error.Code)
               .Append(" message=").Append(error.Message);
        if (error.Domain != null || error.NativeCode != null)
        {
            builder.Append(" details=").Append(error.Domain ?? "-").Append(':').Append(error.NativeCode?.ToString() ?? "-");
        }
        return builder.ToString();
    }

    /// Local (pre-Manager) rejection, such as a precondition the screen itself checks.
    internal static string FormatLocal(in IosClipboardSampleResultContext context, string message) =>
        $"#{context.Sequence} [{context.Marker}] -- local={message}";

    /// <summary>
    /// Describes an exception for the log without quoting its message.
    /// </summary>
    /// <remarks>
    /// File APIs put the path they failed on into the message - FileNotFoundException,
    /// DirectoryNotFoundException, UnauthorizedAccessException and IOException all do - and this
    /// sample must never disclose the temporary path the native layer returned. The type name is
    /// enough to tell the failure modes apart.
    /// </remarks>
    internal static string DescribeException(Exception exception) => exception.GetType().Name;

    /// <summary>
    /// Joins detected pattern kinds for display.
    /// </summary>
    /// <remarks>
    /// Kinds are enum names, not clipboard content: DetectValues already shows the same
    /// information as per-category counts. The detected values themselves are never shown.
    /// Without this, a result of "patterns=2" cannot say which two were found, so the manual
    /// check that number and probableWebSearch only appear on their own fixtures is unverifiable.
    /// </remarks>
    internal static string FormatPatternKinds(IReadOnlyList<IosClipboardDetectionPattern> patterns) =>
        patterns.Count == 0 ? "-" : string.Join(",", patterns);

    /// <remarks>
    /// The size read and the directory cleanup are reported independently: cleanup must be
    /// attempted even when the size could not be read, so every combination has a display.
    /// </remarks>
    internal static string FormatFileOutcome(long fileSize, bool cleanupSucceeded) =>
        $"fileSize={fileSize} cleanup={(cleanupSucceeded ? "ok" : "failed")}";

    /// <remarks>
    /// Pasteboard names can identify the app that created them, so only the length is shown.
    /// </remarks>
    internal static string FormatScopeLabel(IosPasteboardScope? scope)
    {
        if (scope == null) return "(none)";
        return scope.Kind switch
        {
            IosPasteboardScopeKind.General => "general",
            IosPasteboardScopeKind.Named => $"named(len={scope.Name?.Length ?? 0})",
            IosPasteboardScopeKind.Unique => $"unique(len={scope.Name?.Length ?? 0})",
            _ => "(unknown)"
        };
    }

    internal static string FormatObservingState(bool isObserving, bool controlPending)
    {
        if (isObserving) return controlPending ? StatusObservingPending : StatusObservingOn;
        return controlPending ? StatusObservingStarting : StatusObservingOff;
    }

    /// <remarks>
    /// The observed scope is shown next to the active one because a CreatePasteboard issued before
    /// a start can complete while that start is still pending, leaving the two different.
    /// </remarks>
    internal static string FormatStatus(
        IosPasteboardScope activeScope,
        IosPasteboardScope? observedScope,
        bool isObserving,
        bool controlPending,
        int eventCount)
    {
        string scopeText = observedScope == null
            ? FormatScopeLabel(activeScope)
            : $"{FormatScopeLabel(activeScope)} (observing {FormatScopeLabel(observedScope)})";
        return $"Scope: {scopeText} | Observing: {FormatObservingState(isObserving, controlPending)} | Events: {eventCount}";
    }
}
#endif
