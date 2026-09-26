#nullable enable

// Windows player only. The native boundary is compiled in only when UNITY_STANDALONE_WIN is set
// and UNITY_EDITOR is not (see WindowsClipboardManager), so this is the one place these calls
// reach the real library. In the Editor every operation would report PlatformUnavailable, which
// WindowsClipboardManagerIntegrationTests already covers there.
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JonghyunKim.NativeToolkit.Runtime.Clipboard;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// Layer 2b tests (see agent-rules/coding-rules/testing.md): PlayMode tests that run on a
    /// Windows player to verify the P/Invoke path that no other layer reaches.
    /// <para>
    /// Run with <c>-runTests -testPlatform StandaloneWindows64</c>; scripts/verify_unity_windows.sh
    /// does. They use no *ForTests hooks: those exist only under UNITY_EDITOR, and the point here is
    /// the unfaked path.
    /// </para>
    /// <para>
    /// The cases follow the manual checks in
    /// artifact/features/clipboard/results/2026-09-09-windows-clipboard-verify-manual-result-v1.md
    /// rather than inventing new ones, and reuse the sample's values where the sample has them.
    /// On the 1.x ABI they are the baseline the 2.0.0 migration is compared against
    /// (artifact/topics/windows-c-abi-2).
    /// </para>
    /// <para>
    /// Everything written is a sample value, never user data. verify_unity_windows.sh reads the
    /// system clipboard after the run and expects <see cref="SampleText"/>, so every test ends by
    /// putting it back (<see cref="PutSampleBack"/>), whichever test runs last and whatever it wrote.
    /// </para>
    /// <para>
    /// The chapter 9 cases read, and some change, the machine's real clipboard history (Win+V). They
    /// touch only items they created, and remove those items again, because history holds 25 items
    /// and every one added pushes out one of the developer's. Clear Unpinned cannot be limited that
    /// way, so it is in the <see cref="DestructiveCategory"/> category, which the script leaves out
    /// unless told otherwise.
    /// </para>
    /// </summary>
    public sealed class WindowsClipboardPlayerTests
    {
        private const float DefaultTimeoutSeconds = 5f;
        private const float HistoryPollSeconds = 0.25f;
        private const string SampleText = "NTK-W1N-7F3A-92QX";

        /// <summary>Tests that change what the developer keeps in clipboard history.</summary>
        public const string DestructiveCategory = "Destructive";

        private const string HistoryOffMessage =
            "Clipboard history (Win+V) is off on this machine; this case needs it on. " +
            "Settings > System > Clipboard > Clipboard history.";

        // The sample's own values for the "unknown id" buttons (WindowsClipboardManagerExampleController).
        private const string UnknownHistoryItemId = "nativetoolkit-sample-no-such-item";
        private const uint UnknownRequestId = uint.MaxValue;

        /// <summary>
        /// Leaves <see cref="SampleText"/> on the clipboard for the layer 3 check. Written with
        /// Sensitive so it adds nothing to history or the cloud clipboard; the round trip is what puts
        /// a plain, history-visible copy through, and this only restores the value it expects.
        /// </summary>
        [TearDown]
        public void PutSampleBack()
        {
            WindowsClipboardResult result = Running().CopyPlainText(SampleText, WindowsClipboardWriteOptions.Sensitive);
            Assert.IsTrue(result.IsSuccess, $"putting the sample back: {result.ErrorCode} {result.ErrorMessage}");
        }

        // ── Round trip ───────────────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator CopyThenPaste_RoundTripsThroughTheNativeLibrary()
        {
            WindowsClipboardManager manager = Running();

            WindowsClipboardResult copied = manager.CopyPlainText(SampleText);
            Assert.IsTrue(copied.IsSuccess, $"CopyPlainText: {copied.ErrorCode} {copied.ErrorMessage}");

            // The return value and the callback travel different paths: the first comes straight
            // back from the native call, the second is queued on the dispatcher and needs a later
            // Update. Both are checked, because the second is what a test player might not provide.
            WindowsClipboardTextResult? delivered = null;
            WindowsClipboardTextResult pasted = manager.PastePlainText(result => delivered = result);
            Assert.IsTrue(pasted.IsSuccess, $"PastePlainText: {pasted.ErrorCode} {pasted.ErrorMessage}");
            Assert.AreEqual(SampleText, pasted.Text, "returned by PastePlainText");

            yield return WaitFor(() => delivered != null, "the PastePlainText callback");
            Assert.AreEqual(SampleText, delivered!.Value.Text, "delivered to the PastePlainText callback");

            yield return ShutDown(manager);
        }

        // ── Block D: rejections ──────────────────────────────────────────────────
        // In the Editor none of the argument checks below can be reached: Initialize reports
        // PlatformUnavailable there, so every call stops at the state guard first. Here the manager
        // is really running, so the argument checks are what answers.

        [Test]
        public void CopyPlainText_Null_IsRejectedAsInvalidArgument()
        {
            WindowsClipboardResult result = Running().CopyPlainText(null!);
            AssertRejected(result.IsSuccess, result.ErrorCode, result.ErrorMessage,
                WindowsClipboardErrorCode.InvalidArgument, "text was null");
        }

        [Test]
        public void CopyFiles_Empty_IsRejectedAsInvalidArgument()
        {
            WindowsClipboardResult result = Running().CopyFiles(Array.Empty<string>());
            AssertRejected(result.IsSuccess, result.ErrorCode, result.ErrorMessage,
                WindowsClipboardErrorCode.InvalidArgument, "paths was null or empty");
        }

        [Test]
        public void CopyCustomFormat_BlankName_IsRejectedAsInvalidArgument()
        {
            WindowsClipboardResult result = Running().CopyCustomFormat("   ", new byte[] { 1 });
            AssertRejected(result.IsSuccess, result.ErrorCode, result.ErrorMessage,
                WindowsClipboardErrorCode.InvalidArgument, "formatName was null or blank");
        }

        [Test]
        public void CopyMultipleFormats_Empty_IsRejectedAsInvalidArgument()
        {
            WindowsClipboardResult result =
                Running().CopyMultipleFormats(new List<WindowsClipboardFormatPayload>());
            AssertRejected(result.IsSuccess, result.ErrorCode, result.ErrorMessage,
                WindowsClipboardErrorCode.InvalidArgument, "items was null or empty");
        }

        [UnityTest]
        public IEnumerator RestoreHistoryItem_BlankId_IsRefusedBeforeARequestIsIssued()
        {
            WindowsClipboardResult? delivered = null;
            uint requestId = Running().RestoreHistoryItem("  ", result => delivered = result);

            Assert.AreEqual(0u, requestId, "a blank id is refused locally, so no request id is issued");
            yield return WaitFor(() => delivered != null, "the RestoreHistoryItem callback");
            AssertRejected(delivered!.Value.IsSuccess, delivered.Value.ErrorCode, delivered.Value.ErrorMessage,
                WindowsClipboardErrorCode.InvalidArgument, "itemId was null or blank");
        }

        [UnityTest]
        public IEnumerator RestoreHistoryItem_UnknownId_ReachesTheNativeLayerAndReportsItemDeleted()
        {
            WindowsClipboardManager manager = Running();

            // The OS answers differently when clipboard history is off (HistoryDisabled), and the
            // manual check was defined with it on. Ask through the product's own API rather than the
            // registry, so the precondition is read the same way the feature reads it.
            WindowsClipboardAvailabilityResult? availability = null;
            manager.GetHistoryAvailability(result => availability = result);
            yield return WaitFor(() => availability != null, "the GetHistoryAvailability callback");
            Assert.IsTrue(availability!.Value.IsSuccess,
                $"GetHistoryAvailability: {availability.Value.ErrorCode} {availability.Value.ErrorMessage}");
            if (!availability.Value.HistoryEnabled)
            {
                Assert.Ignore("Clipboard history (Win+V) is off on this machine; this case needs it on. " +
                              "Settings > System > Clipboard > Clipboard history.");
            }

            WindowsClipboardResult? delivered = null;
            uint requestId = manager.RestoreHistoryItem(UnknownHistoryItemId, result => delivered = result);

            Assert.AreNotEqual(0u, requestId, "an unknown but well-formed id is accepted and sent on");
            yield return WaitFor(() => delivered != null, "the RestoreHistoryItem callback");
            AssertRejected(delivered!.Value.IsSuccess, delivered.Value.ErrorCode, delivered.Value.ErrorMessage,
                WindowsClipboardErrorCode.ItemDeleted, null);
        }

        [Test]
        public void CancelRequest_UnknownId_IsRejectedByTheNativeLayer()
        {
            WindowsClipboardResult result = Running().CancelRequest(UnknownRequestId);
            AssertRejected(result.IsSuccess, result.ErrorCode, result.ErrorMessage,
                WindowsClipboardErrorCode.InvalidParameter, null);
        }

        [UnityTest]
        public IEnumerator CopyPlainText_AfterShutdown_IsRejectedAndShutdownStaysComplete()
        {
            WindowsClipboardManager manager = Running();
            yield return ShutDown(manager);

            WindowsClipboardResult result = manager.CopyPlainText("NTK-AFTER-SHUTDOWN");
            AssertRejected(result.IsSuccess, result.ErrorCode, result.ErrorMessage,
                WindowsClipboardErrorCode.NotInitializedByHost, null);

            WindowsClipboardResult again = manager.TryShutdown(out bool completed);
            Assert.IsTrue(again.IsSuccess, $"TryShutdown after shutdown: {again.ErrorCode} {again.ErrorMessage}");
            Assert.IsTrue(completed, "a second shutdown has nothing left to do");
        }

        // ── S-7: calls from a worker thread ──────────────────────────────────────

        [UnityTest]
        public IEnumerator CopyPlainText_FromAWorkerThread_IsRefusedAndAnsweredOnTheMainThread()
        {
            // Captured here, as the sample does: Instance may create a GameObject, which a worker
            // thread cannot, and the test would then be observing that failure instead.
            WindowsClipboardManager manager = Running();
            int mainThread = Thread.CurrentThread.ManagedThreadId;

            WindowsClipboardResult? returned = null;
            WindowsClipboardResult? delivered = null;
            int callbackThread = 0;
            Exception? thrown = null;
            Task worker = Task.Run(() =>
            {
                try
                {
                    returned = manager.CopyPlainText("NTK-FROM-WORKER", WindowsClipboardWriteOptions.None, result =>
                    {
                        callbackThread = Thread.CurrentThread.ManagedThreadId;
                        delivered = result;
                    });
                }
                catch (Exception exception)
                {
                    thrown = exception;
                }
            });

            yield return WaitFor(() => worker.IsCompleted, "the worker thread");
            Assert.IsNull(thrown, $"the call threw on the worker thread: {thrown?.GetType().Name}");
            AssertRejected(returned!.Value.IsSuccess, returned.Value.ErrorCode, returned.Value.ErrorMessage,
                WindowsClipboardErrorCode.MainThreadRequired, null);

            yield return WaitFor(() => delivered != null, "the CopyPlainText callback");
            Assert.AreEqual(mainThread, callbackThread, "the callback must run on the main thread");
            AssertRejected(delivered!.Value.IsSuccess, delivered.Value.ErrorCode, delivered.Value.ErrorMessage,
                WindowsClipboardErrorCode.MainThreadRequired, null);
        }

        // ── Chapter 9: history through the Awaitable API ─────────────────────────

        [UnityTest]
        public IEnumerator GetHistoryAvailabilityAsync_Answers()
        {
            WindowsClipboardAvailabilityResult? result = null;
            yield return Await(Running().GetHistoryAvailabilityAsync(), "GetHistoryAvailabilityAsync", r => result = r);
            Assert.IsTrue(result!.Value.IsSuccess, $"GetHistoryAvailabilityAsync: {result.Value.ErrorCode} {result.Value.ErrorMessage}");
        }

        [UnityTest]
        public IEnumerator GetHistoryAsync_AlreadyCancelled_ReturnsCanceledWithoutThrowing()
        {
            // Manual check S-9. The sample cancels the token before the call, so this does too.
            using var source = new CancellationTokenSource();
            source.Cancel();

            WindowsClipboardHistoryResult? result = null;
            yield return Await(Running().GetHistoryAsync(source.Token), "GetHistoryAsync", r => result = r);
            AssertRejected(result!.Value.IsSuccess, result.Value.ErrorCode, result.Value.ErrorMessage,
                WindowsClipboardErrorCode.Canceled, null);
        }

        [UnityTest]
        public IEnumerator GetHistoryAsync_FindsWhatWasJustCopied_CapturedMinutesAgoAtMost()
        {
            WindowsClipboardManager manager = Running();
            WindowsClipboardAvailabilityResult availability = default;
            yield return Await(manager.GetHistoryAvailabilityAsync(), "GetHistoryAvailabilityAsync", r => availability = r);
            RequireHistory(availability);

            string marker = NewMarker("GET");
            WindowsClipboardHistoryItem? item = null;
            yield return CopyIntoHistory(manager, marker, found => item = found);
            Assert.IsNotNull(item, $"the copied value did not show up in history within {DefaultTimeoutSeconds}s");

            // S-2. 1.x sends FILETIME ticks and 2.0.0 Unix milliseconds; a conversion left as it is
            // lands in January 1601. Checking that the capture time is close to now catches that
            // without depending on which unit either side uses.
            DateTimeOffset? captured = item!.ToUtcTime();
            Assert.IsNotNull(captured, $"timestamp {item.Timestamp} does not convert to a time");
            double minutesAway = Math.Abs((DateTimeOffset.UtcNow - captured!.Value).TotalMinutes);
            Assert.Less(minutesAway, 10.0, $"captured at {captured.Value:O}, not within 10 minutes of now");

            bool removed = false;
            yield return RemoveFromHistory(manager, marker, done => removed = done);
            Assert.IsTrue(removed, $"the item this test added was still in history after {DefaultTimeoutSeconds}s");
        }

        [UnityTest]
        public IEnumerator RestoreHistoryItemAsync_PutsAnOlderItemBackOnTheClipboard()
        {
            WindowsClipboardManager manager = Running();
            WindowsClipboardAvailabilityResult availability = default;
            yield return Await(manager.GetHistoryAvailabilityAsync(), "GetHistoryAvailabilityAsync", r => availability = r);
            RequireHistory(availability);

            string marker = NewMarker("RESTORE");
            WindowsClipboardHistoryItem? item = null;
            yield return CopyIntoHistory(manager, marker, found => item = found);
            Assert.IsNotNull(item, $"the copied value did not show up in history within {DefaultTimeoutSeconds}s");

            // Something else becomes current, so restoring has something to undo. It must be an
            // ordinary copy, as in native-toolkit's own restore test: when the content being replaced
            // was written with ExcludeHistory (Sensitive includes it), Windows reports the restore as
            // Success and leaves the clipboard untouched. Seen here first, then reproduced by
            // native-toolkit and documented on ntk_clipboard_restore_history_item (feature/NTKIT-16,
            // 67217afb). This test does not pin that behaviour; it only avoids it.
            string newer = NewMarker("NEWER");
            WindowsClipboardHistoryItem? newerItem = null;
            yield return CopyIntoHistory(manager, newer, found => newerItem = found);
            Assert.IsNotNull(newerItem, $"the second value did not show up in history within {DefaultTimeoutSeconds}s");

            WindowsClipboardResult? restored = null;
            yield return Await(manager.RestoreHistoryItemAsync(item!.Id), "RestoreHistoryItemAsync", r => restored = r);
            Assert.IsTrue(restored!.Value.IsSuccess, $"RestoreHistoryItemAsync: {restored.Value.ErrorCode} {restored.Value.ErrorMessage}");

            // Checked twice: the product's own paste, and what any other application now gets. Values
            // are compared, never printed - if something other than a marker is there, it may be
            // whatever the developer copied meanwhile.
            bool inside = false, outside = false;
            yield return Eventually(() =>
            {
                inside = manager.PastePlainText().Text == marker;
                outside = ReadClipboardFromAnotherProcess() == marker;
                return inside && outside;
            }, _ => { });
            Assert.IsTrue(inside, $"PastePlainText did not return the restored item within {DefaultTimeoutSeconds}s");
            Assert.IsTrue(outside, $"another process did not see the restored item within {DefaultTimeoutSeconds}s");

            bool removed = false;
            yield return RemoveFromHistory(manager, marker, done => removed = done);
            bool newerRemoved = false;
            yield return RemoveFromHistory(manager, newer, done => newerRemoved = done);
            Assert.IsTrue(removed && newerRemoved, $"an item this test added was still in history after {DefaultTimeoutSeconds}s");
        }

        [UnityTest]
        public IEnumerator DeleteHistoryItemAsync_RemovesTheItem()
        {
            WindowsClipboardManager manager = Running();
            WindowsClipboardAvailabilityResult availability = default;
            yield return Await(manager.GetHistoryAvailabilityAsync(), "GetHistoryAvailabilityAsync", r => availability = r);
            RequireHistory(availability);

            string marker = NewMarker("DELETE");
            WindowsClipboardHistoryItem? item = null;
            yield return CopyIntoHistory(manager, marker, found => item = found);
            Assert.IsNotNull(item, $"the copied value did not show up in history within {DefaultTimeoutSeconds}s");

            WindowsClipboardResult? deleted = null;
            yield return Await(manager.DeleteHistoryItemAsync(item!.Id), "DeleteHistoryItemAsync", r => deleted = r);
            Assert.IsTrue(deleted!.Value.IsSuccess, $"DeleteHistoryItemAsync: {deleted.Value.ErrorCode} {deleted.Value.ErrorMessage}");

            bool gone = false;
            yield return WaitUntilGone(manager, marker, result => gone = result);
            Assert.IsTrue(gone, $"the item was still in history {DefaultTimeoutSeconds}s after deleting it");
        }

        [UnityTest, Category(DestructiveCategory)]
        public IEnumerator ClearUnpinnedHistoryAsync_RemovesUnpinnedItems()
        {
            // Destructive: this empties every unpinned item in the developer's history, not only the
            // one it adds. verify_unity_windows.sh runs it only with --include-destructive.
            WindowsClipboardManager manager = Running();
            WindowsClipboardAvailabilityResult availability = default;
            yield return Await(manager.GetHistoryAvailabilityAsync(), "GetHistoryAvailabilityAsync", r => availability = r);
            RequireHistory(availability);

            string marker = NewMarker("CLEAR");
            WindowsClipboardHistoryItem? item = null;
            yield return CopyIntoHistory(manager, marker, found => item = found);
            Assert.IsNotNull(item, $"the copied value did not show up in history within {DefaultTimeoutSeconds}s");

            WindowsClipboardResult? cleared = null;
            yield return Await(manager.ClearUnpinnedHistoryAsync(), "ClearUnpinnedHistoryAsync", r => cleared = r);
            Assert.IsTrue(cleared!.Value.IsSuccess, $"ClearUnpinnedHistoryAsync: {cleared.Value.ErrorCode} {cleared.Value.ErrorMessage}");

            bool gone = false;
            yield return WaitUntilGone(manager, marker, result => gone = result);
            Assert.IsTrue(gone, $"the unpinned item was still in history {DefaultTimeoutSeconds}s after clearing");
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the manager initialized for real. Tests share it, and some end with a shutdown, so
        /// every test initializes rather than assuming the state the previous one left.
        /// </summary>
        private static WindowsClipboardManager Running()
        {
            WindowsClipboardManager manager = WindowsClipboardManager.Instance;
            WindowsClipboardResult initialized = manager.Initialize();
            Assert.IsTrue(initialized.IsSuccess,
                $"Initialize: {initialized.ErrorCode} {initialized.ErrorMessage}");
            return manager;
        }

        private static IEnumerator ShutDown(WindowsClipboardManager manager)
        {
            WindowsClipboardResult? shutdown = null;
            manager.ShutdownWithDrain(result => shutdown = result);
            yield return WaitFor(() => shutdown != null, "ShutdownWithDrain");
            Assert.IsTrue(shutdown!.Value.IsSuccess,
                $"ShutdownWithDrain: {shutdown.Value.ErrorCode} {shutdown.Value.ErrorMessage}");
        }

        private static void AssertRejected(bool isSuccess, WindowsClipboardErrorCode actual, string? message,
            WindowsClipboardErrorCode expected, string? detail)
        {
            Assert.IsFalse(isSuccess, $"expected {expected}, got success");
            Assert.AreEqual(expected, actual, message);
            Assert.IsFalse(string.IsNullOrEmpty(message), "every failure carries an ErrorMessage (manual check S-3)");
            if (detail != null)
            {
                StringAssert.Contains(detail, message);
            }
        }

        /// <summary>
        /// Reads the system clipboard the way any other application would: from a separate process,
        /// not through this library. A copy-then-paste round trip inside the player passes even when
        /// both directions are broken the same way; this does not. It is layer 3 at the moment the
        /// test needs it, rather than once after the whole run.
        /// <para>
        /// PowerShell's output is forced to UTF-8, which the default console code page is not. The
        /// markers are ASCII today, but this is what should catch a broken UTF-8 conversion after
        /// the 2.0.0 migration, so it must not mangle non-ASCII text itself.
        /// </para>
        /// </summary>
        private static string? ReadClipboardFromAnotherProcess()
        {
            var info = new System.Diagnostics.ProcessStartInfo(
                "powershell.exe",
                "-NoProfile -Command \"[Console]::OutputEncoding = [System.Text.Encoding]::UTF8; Get-Clipboard -Raw\"")
            {
                UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
            };
            using var process = System.Diagnostics.Process.Start(info);
            if (process == null) return null;
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);
            return output.TrimEnd('\r', '\n');
        }

        /// <summary>A value no earlier run wrote, so the item found in history is this test's own.</summary>
        private static string NewMarker(string purpose) => $"NTK-W1N-{purpose}-{Guid.NewGuid():N}";

        private static IEnumerator Await<T>(Awaitable<T> awaitable, string what, Action<T> onResult)
        {
            var awaiter = awaitable.GetAwaiter();
            yield return WaitFor(() => awaiter.IsCompleted, what);
            onResult(awaiter.GetResult());
        }

        /// <summary>
        /// Polls <paramref name="condition"/> until it holds or the timeout passes, and reports which.
        /// The test asserts on the answer itself: an assertion thrown inside a nested coroutine is
        /// logged as a coroutine exception rather than reported as the test's own failure.
        /// </summary>
        private static IEnumerator Eventually(Func<bool> condition, Action<bool> met)
        {
            float deadline = Time.realtimeSinceStartup + DefaultTimeoutSeconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > deadline)
                {
                    met(false);
                    yield break;
                }
                yield return new WaitForSecondsRealtime(HistoryPollSeconds);
            }
            met(true);
        }

        /// <summary>
        /// Skips only when clipboard history really is off. When the question itself fails, the test
        /// fails with the code: the likeliest cause is NotForeground, because the history API answers
        /// only a window in the foreground, and a skip would hide that the run could not test history.
        /// </summary>
        private static void RequireHistory(WindowsClipboardAvailabilityResult availability)
        {
            string hint = availability.ErrorCode == WindowsClipboardErrorCode.NotForeground
                ? " - the test player window was not in the foreground; do not use the machine during the run"
                : "";
            Assert.IsTrue(availability.IsSuccess,
                $"GetHistoryAvailabilityAsync: {availability.ErrorCode} {availability.ErrorMessage}{hint}");
            if (!availability.HistoryEnabled) Assert.Ignore(HistoryOffMessage);
        }

        /// <summary>
        /// Copies <paramref name="text"/> and waits for it to appear in history, which the OS updates
        /// on its own schedule. Kept out of the cloud clipboard; it has to be in history to be found.
        /// </summary>
        private static IEnumerator CopyIntoHistory(
            WindowsClipboardManager manager, string text, Action<WindowsClipboardHistoryItem?> found)
        {
            WindowsClipboardResult copied = manager.CopyPlainText(text, WindowsClipboardWriteOptions.ExcludeRoaming);
            Assert.IsTrue(copied.IsSuccess, $"CopyPlainText: {copied.ErrorCode} {copied.ErrorMessage}");

            float deadline = Time.realtimeSinceStartup + DefaultTimeoutSeconds;
            while (true)
            {
                WindowsClipboardHistoryItem? match = null;
                yield return ReadHistory(manager, items => match = FindText(items, text));
                if (match != null || Time.realtimeSinceStartup > deadline)
                {
                    found(match);
                    yield break;
                }
                yield return new WaitForSecondsRealtime(HistoryPollSeconds);
            }
        }

        /// <summary>
        /// Deletes every history item holding <paramref name="text"/> - always a test's own marker -
        /// and reports whether history ended up without one. Every match is deleted, not only the
        /// item first found: restoring an item may leave a second entry with the same text.
        /// </summary>
        private static IEnumerator RemoveFromHistory(WindowsClipboardManager manager, string text, Action<bool> removed)
        {
            float deadline = Time.realtimeSinceStartup + DefaultTimeoutSeconds;
            while (true)
            {
                WindowsClipboardHistoryItem? match = null;
                yield return ReadHistory(manager, items => match = FindText(items, text));
                if (match == null)
                {
                    removed(true);
                    yield break;
                }
                if (Time.realtimeSinceStartup > deadline)
                {
                    removed(false);
                    yield break;
                }

                WindowsClipboardResult? deleted = null;
                yield return Await(manager.DeleteHistoryItemAsync(match.Id), "DeleteHistoryItemAsync", r => deleted = r);
                if (!deleted!.Value.IsSuccess)
                {
                    Debug.LogWarning($"DeleteHistoryItemAsync: {deleted.Value.ErrorCode} {deleted.Value.ErrorMessage}");
                }
                yield return new WaitForSecondsRealtime(HistoryPollSeconds);
            }
        }

        private static IEnumerator WaitUntilGone(WindowsClipboardManager manager, string text, Action<bool> gone)
        {
            float deadline = Time.realtimeSinceStartup + DefaultTimeoutSeconds;
            while (true)
            {
                bool present = true;
                yield return ReadHistory(manager, items => present = FindText(items, text) != null);
                if (!present || Time.realtimeSinceStartup > deadline)
                {
                    gone(!present);
                    yield break;
                }
                yield return new WaitForSecondsRealtime(HistoryPollSeconds);
            }
        }

        /// <summary>
        /// Reads history. The items are the developer's as well as the test's, so nothing here ever
        /// prints an item's text; callers look for their own marker and nothing else.
        /// </summary>
        private static IEnumerator ReadHistory(
            WindowsClipboardManager manager, Action<IReadOnlyList<WindowsClipboardHistoryItem>> onItems)
        {
            WindowsClipboardHistoryResult? result = null;
            yield return Await(manager.GetHistoryAsync(), "GetHistoryAsync", r => result = r);
            Assert.IsTrue(result!.Value.IsSuccess, $"GetHistoryAsync: {result.Value.ErrorCode} {result.Value.ErrorMessage}");
            onItems(result.Value.Items);
        }

        private static WindowsClipboardHistoryItem? FindText(IReadOnlyList<WindowsClipboardHistoryItem> items, string text)
        {
            foreach (WindowsClipboardHistoryItem item in items)
            {
                if (item.Text == text) return item;
            }
            return null;
        }

        private static IEnumerator WaitFor(Func<bool> condition, string what)
        {
            float deadline = Time.realtimeSinceStartup + DefaultTimeoutSeconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > deadline)
                {
                    Assert.Fail($"Timed out after {DefaultTimeoutSeconds}s waiting for {what}.");
                }
                yield return null;
            }
        }
    }
}
#endif
