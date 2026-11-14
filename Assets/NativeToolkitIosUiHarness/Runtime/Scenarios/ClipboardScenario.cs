#nullable enable

using System.Linq;
using System.Runtime.InteropServices;
using JonghyunKim.NativeToolkit.Runtime.iOS.Clipboard;
using UnityEngine;

namespace JonghyunKim.NativeToolkit.IosUiHarness
{
    /// <summary>
    /// Clipboard: section 2 of the Clipboard test plan v4 (artifact/ios/clipboard/designs/2026-11-14-ios-clipboard-ui-test-plan-v4.md).
    /// </summary>
    /// <remarks>
    /// The general-pasteboard rows (CB-1 to CB-4) read what the UI test runner wrote there; they run on the
    /// simulator only. Their lines carry lengths and counts, never the text read. The after-destroy row
    /// (CB-5) uses a named pasteboard only.
    /// </remarks>
    internal static class ClipboardScenario
    {
        private const string Named = "com.jonghyunkim.nativetoolkit.uiharness.afterdestroy";
        private const string LateChange = "Discarded a late startObserving callback";

        [DllImport("__Internal")] private static extern void ntkh_pasteboard_write(string name);
        [DllImport("__Internal")] private static extern int ntkh_abi();
        [DllImport("__Internal")] private static extern int ntkh_native_listener_add();
        [DllImport("__Internal")] private static extern int ntkh_native_listener_count();

        private static int s_lateChanges;
        private static int s_listener = -1;
        private static bool s_terminatedWhenWritten;

        public static void Register()
        {
            var general = IosPasteboardScope.General;

            IosUiHarness.Row("read-general", () => IosClipboardManager.Instance.Read(general, r =>          // CB-1, CB-2
            {
                if (!r.IsSuccess) { Fail("read", r.Error); return; }
                var first = r.Items.FirstOrDefault();
                IosUiHarness.Append("read", "ok",
                    $"items={r.Items.Count} types={first?.TypeIdentifiers.Count ?? 0} text-length={Length(first?.Text)}");
            }));

            IosUiHarness.Row("load-general", () => IosClipboardManager.Instance.LoadItem(                    // CB-2b
                IosClipboardLoadRequest.Text, general, r =>
                {
                    if (!r.IsSuccess) { Fail("load", r.Error); return; }
                    IosUiHarness.Append("load", "ok", $"text-length={Length(r.Item?.Text)}");
                }));

            IosUiHarness.Row("detect-values-general", () => IosClipboardManager.Instance.DetectValues(       // CB-3
                new[] { IosClipboardDetectionPattern.ProbableWebUrl }, general, r =>
                {
                    if (!r.IsSuccess) { Fail("detect", r.Error); return; }
                    IosUiHarness.Append("detect", "ok", $"url-length={Length(r.Values?.ProbableWebUrl)}");
                }));

            IosUiHarness.Row("detect-patterns-general", () => IosClipboardManager.Instance.DetectPatterns(   // CB-4
                new[] { IosClipboardDetectionPattern.ProbableWebUrl }, general, r =>
                {
                    if (!r.IsSuccess) { Fail("patterns", r.Error); return; }
                    IosUiHarness.Append("patterns", "ok", $"patterns={string.Join(",", r.Patterns)}");
                }));

            // CB-5: observe a named pasteboard, destroy the manager, write natively 0.5 s later, report 3 s later.
            IosUiHarness.Row("after-destroy", () =>
            {
                var m = IosClipboardManager.Instance;
                var scope = IosPasteboardScope.Named(Named);
                m.CreatePasteboard(IosPasteboardCreationRequest.Named(Named), created =>
                {
                    if (!created.IsSuccess) { Fail("after-destroy", created.Error); return; }
                    m.StartObserving(scope, null, started =>
                    {
                        if (!started.IsSuccess) { Fail("after-destroy", started.Error); return; }
                        s_listener = ntkh_native_listener_add();
                        Application.logMessageReceivedThreaded += CountLateChange;
                        Object.Destroy(m.gameObject);
                        IosUiHarness.After(0.5, "after-destroy-write");
                    });
                });
            });
            IosUiHarness.Hidden("after-destroy-write", () =>
            {
                s_terminatedWhenWritten = IosClipboardManager.IsTerminated;
                ntkh_pasteboard_write(Named);
                IosUiHarness.After(3.0, "after-destroy-report");
            });
            IosUiHarness.Hidden("after-destroy-report", () =>
            {
                Application.logMessageReceivedThreaded -= CountLateChange;
                string listener = s_listener == -1 ? "none" : s_listener == -2 ? "add-failed" : ntkh_native_listener_count().ToString();
                IosUiHarness.Append("after-destroy", "event",
                    $"terminated={s_terminatedWhenWritten} abi={(ntkh_abi() == 1 ? "present" : "none")} " +
                    $"late-change={System.Threading.Volatile.Read(ref s_lateChanges)} native-listener={listener}");
            });
        }

        private static void CountLateChange(string message, string stackTrace, LogType type)
        {
            if (message.Contains(LateChange))
                System.Threading.Interlocked.Increment(ref s_lateChanges);
        }

        private static string Length(string? value) => value == null ? "<null>" : value.Length.ToString();

        private static void Fail(string id, IosClipboardErrorInfo? error) =>
            IosUiHarness.Append(id, "fail",
                $"code={error?.Code ?? "<null>"} domain={error?.Domain ?? "<null>"} native-code={error?.NativeCode?.ToString() ?? "<null>"}");
    }
}
