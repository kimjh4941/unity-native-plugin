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
    /// front. Block C is presses 57-64 of session 1 and 1-9 of session 2, with clipboard history
    /// turned off as the manual run did in Settings. Quit (press 65, M-19) ends the player and is
    /// <see cref="NotYetAutomated"/>, which the checker reports rather than passes.
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
        /// Buttons no test presses yet. Quit ends the player, and the test run with it; M-19 needs
        /// the player started on its own. verify_unity_windows.sh reads this constant and hands it
        /// to the log checker, which lists them as not automated instead of passing S-2.
        /// </summary>
        internal const string NotYetAutomated = "Quit";

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

        // Session 1, presses 57-64: block C, history turned off after the first. The manual run
        // unsubscribed from history events first, since stopping them fails once history is off
        // (MonitorRegisterFailed, which then sticks; design 2.9).
        private static readonly string[] BlockCSession1 =
        {
            "Clipboard", "Initialize", "DisableHistoryEvents", "GetHistoryAvailability", "GetHistory",
            "CopyPlainText", "PastePlainText", "ReserveDeferredFormats", "PastePlainText",
            "ReserveDeferredFormats",
        };
        private const int HistoryOffAfterPress = 3;
        private const int AnotherAppPastesAfterPress = 9;

        // Session 2, presses 1-9: block C again, after the Quit that ended session 1.
        private static readonly string[] BlockCSession2 =
        {
            "Clipboard", "Initialize", "CanShutdownNow", "ShutdownWhileDisabled", "Initialize",
            "RecoverDeferredState", "ReserveDeferredFormats", "RecoverDeferredState", "PastePlainText",
        };

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

        /// <summary>The history switch before a block C test turned it off; put back in teardown.</summary>
        private uint? _historyBefore;
        private bool _historyTurnedOff;

        [UnitySetUp]
        public IEnumerator LoadTheSample()
        {
            _lines.Clear();
            _historyTurnedOff = false;
            yield return TakeForegroundAndLog(TestContext.CurrentContext.Test.Name);
            Application.logMessageReceived += Record;
            yield return LoadAtTopMenu();
        }

        [UnityTearDown]
        public IEnumerator UnloadTheSample()
        {
            // First, so that nothing below can leave the developer's history off.
            if (_historyTurnedOff) WriteHistorySetting(_historyBefore);
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
        public IEnumerator BlockC_Session1_WithHistoryOff() => Run("blockC1", BlockCSession1, new Dictionary<int, Func<IEnumerator>>
        {
            [HistoryOffAfterPress] = TurnHistoryOff,
            [AnotherAppPastesAfterPress] = AnotherAppPastes,
        });

        [UnityTest, Timeout(RunTimeoutMilliseconds)]
        public IEnumerator BlockC_Session2_WithHistoryOff() => Run("blockC2", BlockCSession2, new Dictionary<int, Func<IEnumerator>>
        {
            [0] = TurnHistoryOff,
        });

        [UnityTest, Timeout(RunTimeoutMilliseconds)]
        public IEnumerator BlockD_PressesSession2InOrder() => Run("blockD", BlockD);

        /// <summary>What the manual run did in Settings: clipboard history off.</summary>
        private IEnumerator TurnHistoryOff()
        {
            _historyBefore = ReadHistorySetting();
            _historyTurnedOff = true;
            WriteHistorySetting(0);
            Note("test.historyOff", $"before={(_historyBefore?.ToString() ?? "absent")}");
            yield return null;
        }

        /// <summary>
        /// M-18: another application pastes what the reservation holds, after the sample's own
        /// paste. The manual run pasted into Notepad and read the provider counts off the screen;
        /// each provider must have been asked once however many applications read the format.
        /// Only the length goes into the log (S-4).
        /// </summary>
        private IEnumerator AnotherAppPastes()
        {
            string? text = null;
            yield return ReadClipboardFromAnotherProcess(read => text = read);
            string? ownLength = _lines.LastOrDefault(line => line.Contains("[call] pastePlainText OK")) is { } paste
                ? Regex.Match(paste, @" length=(-?\d+)").Groups[1].Value
                : null;
            Note("test.anotherAppPastes",
                $"empty={string.IsNullOrEmpty(text)} length={text?.Length.ToString() ?? "n/a"} " +
                $"sameLengthAsOwnPaste={text != null && text.Length.ToString() == ownLength}");

            // The screen picks up the counts in Update.
            yield return null;
            yield return null;
            Match render = Regex.Match(StatusLine() ?? "", @"Render: text=(\d+) image=(\d+)");
            Note("test.screenRender", render.Success
                ? $"text={render.Groups[1].Value} image={render.Groups[2].Value}"
                : "text=n/a image=n/a");
        }

        /// <summary>
        /// A line in the sample's own [local] form, so the checker reads it with the press before
        /// it. It goes through Debug.Log and so into the run log like the sample's lines.
        /// </summary>
        private static void Note(string what, string detail) =>
            Debug.Log($"[SampleRun] #0 [local] {what} {detail}");

        private IEnumerator Run(string name, string[] presses, IReadOnlyDictionary<int, Func<IEnumerator>>? after = null)
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
                // Hook 0 runs before the first press; hook N after press N has settled.
                if (after != null && after.TryGetValue(0, out Func<IEnumerator>? first)) yield return first();

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
                    if (after != null && after.TryGetValue(step + 1, out Func<IEnumerator>? hook)) yield return hook();
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
