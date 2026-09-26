#nullable enable

// Windows player only, like the other tests that drive the sample.
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static JonghyunKim.NativeToolkit.Tests.WindowsClipboardSampleScreenDriver;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// Presses the Windows clipboard sample's buttons in the order the manual verification pressed
    /// them, so that S-2 (every button, nothing throws, the operations read right), S-4 (no
    /// clipboard content in the log) and S-8 (every accept has exactly one done) can be judged
    /// by scripts/check_windows_clipboard_sample_log.py, the same checker the manual run used.
    /// <para>
    /// The orders are the recorded manual sessions, not a plan: presses 1-55 of
    /// artifact/features/clipboard/results/logs/2026-09-09-windows-clipboard-verify-manual-session1.log
    /// are block A of the sample-scene design (history on, foreground), and presses 10-40 of
    /// session2.log are block D (the errors, last) followed by the extra S checks. They were pulled
    /// from those logs mechanically. Block B is press 56 of session 1, made with another window in
    /// front. Block C (history off) needs the OS put into another state first and is not driven
    /// here yet; the buttons only it presses are <see cref="NotYetAutomated"/>, which the checker
    /// reports rather than passes.
    /// </para>
    /// <para>
    /// Destructive: the copy buttons add ordinary items to the machine's clipboard history, some
    /// fifteen a run, and history holds 25, so the developer's own items are pushed out; Clear
    /// Unpinned and the Restore / Delete buttons act on whatever history holds. Run it only on a
    /// machine whose history nobody needs, with verify_unity_windows.sh --include-destructive.
    /// </para>
    /// <para>
    /// A person looked at each result before pressing the next button. Here, each press waits
    /// until every request the sample has accepted is done, read from the sample's own
    /// "#N [accept]" and "[done] ... call=#N" lines.
    /// </para>
    /// </summary>
    [Category(WindowsClipboardPlayerTests.DestructiveCategory)]
    public sealed class WindowsClipboardSampleRunPlayerTests
    {
        /// <summary>
        /// Buttons pressed only in block C. verify_unity_windows.sh reads this constant and hands it
        /// to the log checker, which lists them as not automated instead of passing S-2.
        /// </summary>
        internal const string NotYetAutomated =
            "CanShutdownNow,DisableHistoryEvents,Quit,RecoverDeferredState,ReserveDeferredFormats,ShutdownWhileDisabled";

        /// <summary>
        /// Brackets each run's log in the test output. The output reaches the result file, which is
        /// where the log checker reads it (--test-results). A file next to the player would not do:
        /// the player is built under the project's Temp, and the editor deletes Temp as it exits,
        /// before the script can look (2026-09-26).
        /// </summary>
        internal const string RunLogBegin = "[SampleRun] begin ";
        internal const string RunLogEnd = "[SampleRun] end ";

        private const float SettleSeconds = 10f;

        /// <summary>
        /// The pause after a press that changes what the clipboard holds. Windows keeps little of
        /// what is replaced at once: pressed back to back, the ten copies before block A's first
        /// Get History left nothing in history, where the manual run, a few seconds a press, left
        /// fourteen (2026-09-26). Restore Last and Delete Last then had no id to act on. Three
        /// seconds stands in for a person's pace; it is a first value, judged by the history count.
        /// </summary>
        private const float ClipboardWriteSeconds = 3f;

        // Buttons that change what the clipboard or its history holds. The Err buttons are
        // rejected before anything is written, so they need no pause.
        private static readonly string[] WritesClipboard = { "Copy", "Restore", "Clear" };
        private static readonly string[] AlsoWritesClipboard = { "PasteHtmlTextOnly", "PastePlainTextAfterClear" };

        // Session 1, presses 1-55: block A.
        private static readonly string[] BlockA =
        {
            "Clipboard", "Initialize", "CopyImage", "PasteImage", "CreateTempFiles", "CopyFiles",
            "PasteFiles", "CopyMultipleFormats", "CopyMultipleFormatsWithImage",
            "CopyMultipleFormatsAnsi", "CopyMultipleFormatsDuplicate", "CopyPlainText", "Clear",
            "PastePlainText", "CopyHtml", "PasteHtml", "CopyLargeText", "PastePlainText",
            "CopySensitive", "CopyExcludeHistory", "CopyExcludeRoaming", "GetHistoryAvailability",
            "GetHistory", "RestoreLast", "DeleteLast", "GetHistory", "RestoreTwiceInOneFrame",
            "IssueAndCancelInOneFrame", "RestoreLast", "ResetEventCounters", "EnableHistoryEvents",
            "CopyMultipleFormats", "GetHistoryAwait", "GetHistoryAwaitCancel", "GetAvailabilityAwait",
            "RestoreAwait", "DeleteAwait", "ClearUnpinnedAwait", "CopyCustomFormat",
            "PasteCustomFormat", "PasteCustomFormatUnknown", "GetFormats", "GetPreferredFormat",
            "HasFormat", "Clear", "CopyMultipleFormats", "GetPreferredFormat", "CopyFiles",
            "GetFormats", "GetPreferredFormat", "CopyImage", "GetPreferredFormat", "CreateTempFiles",
            "CopyFiles", "GetPreferredFormat",
        };

        // Session 1, press 56: block B. Into the screen and initialized first, as this test starts
        // from the top menu where the manual session had just finished block A.
        private static readonly string[] BlockB = { "Clipboard", "Initialize", "DelayedHistoryCall" };

        /// <summary>
        /// Presses after which the player steps behind another window until the request they lead
        /// to is answered. Delayed History Call issues its GetHistory five seconds after the press
        /// (DelayedCallSeconds in the sample), for the person to bring another window up meanwhile.
        /// </summary>
        private static readonly string[] StepBackAfter = { "DelayedHistoryCall" };
        private const float DelayedCallSeconds = 5f;

        // Session 2: into the screen and initialized, then presses 10-40, block D and the extras.
        private static readonly string[] BlockD =
        {
            "Clipboard", "Initialize", "ErrCopyPlainTextNull", "ErrCopyFilesEmpty",
            "ErrCopyCustomFormatBlankName", "ErrCopyMultipleFormatsEmpty", "ErrRestoreBlankId",
            "ErrRestoreUnknownId", "ErrCancelUnknownId", "ErrCopyAfterShutdown", "Initialize",
            "ForceInitializeWhileDraining", "Initialize", "CopyFromWorkerThread",
            "GetHistoryFromWorkerThread", "CopyPlainTextEmpty", "PasteHtmlTextOnly", "CancelLast",
            "ClearUnpinned", "PastePlainTextAfterClear", "DeleteTempFiles",
            "RequestAndImmediateShutdown", "Initialize", "TryShutdown", "Initialize",
            "ShutdownWithDrain", "Home", "Clipboard", "ResetEventCounters", "Home", "Clipboard",
            "Initialize", "ResetEventCounters",
        };

        private static readonly Regex AcceptLine = new(@"#(\d+) \[accept\] ");
        private static readonly Regex DoneLine = new(@"#\d+ \[done\] \S+ call=#(\d+)");

        private readonly List<string> _lines = new();

        [UnitySetUp]
        public IEnumerator LoadTheSample()
        {
            _lines.Clear();
            yield return TakeForegroundAndLog(TestContext.CurrentContext.Test.Name);
            Application.logMessageReceived += Record;
            yield return LoadAtTopMenu();
        }

        [UnityTearDown]
        public IEnumerator UnloadTheSample()
        {
            Application.logMessageReceived -= Record;
            yield return Unload();
            WindowsClipboardPlayerTests.LeaveSampleOnClipboard();
        }

        // The Test Framework stops a test after 180 s. Block A takes about 100 s at a person's pace,
        // and a request that never answers adds SettleSeconds to every press after it.
        private const int RunTimeoutMilliseconds = 15 * 60 * 1000;

        [UnityTest, Timeout(RunTimeoutMilliseconds)]
        public IEnumerator BlockA_PressesSession1InOrder() => Run("blockA", BlockA);

        [UnityTest, Timeout(RunTimeoutMilliseconds)]
        public IEnumerator BlockB_DelayedHistoryCallFromBehind() => Run("blockB", BlockB);

        [UnityTest, Timeout(RunTimeoutMilliseconds)]
        public IEnumerator BlockD_PressesSession2InOrder() => Run("blockD", BlockD);

        private IEnumerator Run(string name, string[] presses)
        {
            var unsettled = new List<string>();
            // Every press is made in the foreground, block B's included: it is the request after it
            // that must meet another window in front. Pressed from behind, every history button
            // answers NotForeground, block B's outcome, and S-2 alone would still pass.
            var background = new List<string>();
            var stayedInFront = new List<string>();
            // The log goes out even when a press fails, so the checker can show how far the run got.
            try
            {
                for (int step = 0; step < presses.Length; step++)
                {
                    string press = presses[step];
                    string buttonName = press == "Clipboard" ? TopMenuClipboardButton : press + "Button";

                    // A screen change (Clipboard, Home) takes a frame or two to put the next button up.
                    bool found = false;
                    yield return Eventually(() => FindButton(buttonName) != null, ok => found = ok);
                    Assert.IsTrue(found, $"{name} press {step + 1} ({press}): no {buttonName} on the screen");

                    if (!IsForeground()) background.Add($"{step + 1}:{press}");
                    Press(FindButton(buttonName)!);
                    yield return null;

                    if (StepBackAfter.Contains(press))
                    {
                        int acceptedBefore = Accepted();
                        IntPtr window = IntPtr.Zero;
                        yield return LeaveForeground(w => window = w);
                        if (IsForeground()) stayedInFront.Add($"{step + 1}:{press}");

                        // The request comes later than the press; wait for it to be accepted first.
                        bool issued = false;
                        yield return Eventually(() => Accepted() > acceptedBefore, ok => issued = ok,
                            DelayedCallSeconds + SettleSeconds);
                        bool answered = false;
                        yield return Eventually(() => Outstanding() == 0, ok => answered = ok, SettleSeconds);
                        if (!issued || !answered) unsettled.Add($"{step + 1}:{press}");

                        yield return ComeBack(window);
                        continue;
                    }

                    bool settled = false;
                    yield return Eventually(() => Outstanding() == 0, ok => settled = ok, SettleSeconds);
                    if (!settled) unsettled.Add($"{step + 1}:{press}");
                    yield return null;

                    if (ChangesClipboard(press)) yield return new WaitForSecondsRealtime(ClipboardWriteSeconds);
                }
            }
            finally
            {
                WriteRunLog(name);
            }

            Assert.IsEmpty(background, $"{name}: pressed without the foreground ({FocusNote()})");
            Assert.IsEmpty(stayedInFront, $"{name}: the player kept the foreground after minimizing ({FocusNote()})");
            Assert.IsEmpty(unsettled, $"{name}: requests still outstanding {SettleSeconds}s after these presses");
        }

        private static bool ChangesClipboard(string press) =>
            WritesClipboard.Any(prefix => press.StartsWith(prefix, StringComparison.Ordinal))
            || AlsoWritesClipboard.Contains(press);

        private int Accepted() => _lines.Count(line => AcceptLine.IsMatch(line));

        /// <summary>Accepted requests with no done line yet, from the sample's own log lines.</summary>
        private int Outstanding()
        {
            var accepted = new HashSet<string>();
            var done = new HashSet<string>();
            foreach (string line in _lines)
            {
                Match accept = AcceptLine.Match(line);
                if (accept.Success) accepted.Add(accept.Groups[1].Value);
                Match finished = DoneLine.Match(line);
                if (finished.Success) done.Add(finished.Groups[1].Value);
            }
            return accepted.Count(call => !done.Contains(call));
        }

        /// <summary>
        /// Keeps what the checker reads. Errors and exceptions are written with a prefix the checker
        /// looks for, since a captured message alone would not say it was one.
        /// </summary>
        private void Record(string message, string stackTrace, LogType type)
        {
            _lines.Add(type switch
            {
                LogType.Exception => "Exception: " + message,
                LogType.Error or LogType.Assert => "LogError: " + message,
                _ => message,
            });
        }

        private void WriteRunLog(string name)
        {
            TestContext.WriteLine(RunLogBegin + name);
            foreach (string line in _lines) TestContext.WriteLine(line);
            TestContext.WriteLine(RunLogEnd + name);
        }
    }
}
#endif
