#nullable enable

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using JonghyunKim.NativeToolkit.Runtime.Clipboard;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// PlayMode integration tests for <c>WindowsClipboardManager</c>.
    /// <para>
    /// These need a running player loop: creating the Manager calls DontDestroyOnLoad, and results
    /// reach their subscribers through <c>UnityMainThreadDispatcher</c>, which only flushes from
    /// Update. EditMode covers the pure classifiers and the guard seam instead.
    /// </para>
    /// <para>
    /// The native boundary is compiled out in the Editor, so every operation that gets past the
    /// guard reports PlatformUnavailable. That is what makes the rejection paths and the delivery
    /// contract testable here without a Windows player.
    /// </para>
    /// </summary>
    public sealed class WindowsClipboardManagerIntegrationTests
    {
        [TearDown]
        public void TearDown()
        {
            // A test that forced the Running state has no native manager behind it, so clear the
            // state before destroying: OnDestroy would otherwise run a shutdown for something that
            // was never initialized.
            WindowsClipboardManager.SetStateForTests(WindowsClipboardManagerState.Uninitialized);

            // Destroy first, then reset: ResetForTests clears the captured main-thread id and the
            // dispatcher, which only Awake re-establishes.
            DestroyManagerIfPresent();
            WindowsClipboardManager.ResetForTests();
        }

        private static void DestroyManagerIfPresent()
        {
            foreach (WindowsClipboardManager manager in
                     Object.FindObjectsByType<WindowsClipboardManager>(FindObjectsInactive.Include))
            {
                Object.DestroyImmediate(manager.gameObject);
            }
        }

        /// <summary>Creates the Manager and puts it into the state operations are accepted in.</summary>
        private static WindowsClipboardManager RunningManager()
        {
            WindowsClipboardManager manager = WindowsClipboardManager.Instance;
            WindowsClipboardManager.SetStateForTests(WindowsClipboardManagerState.Running);
            return manager;
        }

        // ── Rejection paths ──────────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator BeforeInitialize_EveryOperationIsRejectedWithoutReachingTheNativeSide()
        {
            WindowsClipboardManager manager = WindowsClipboardManager.Instance;
            yield return null;

            Assert.AreEqual(WindowsClipboardErrorCode.NotInitializedByHost,
                manager.CopyPlainText("a").ErrorCode);
            Assert.AreEqual(WindowsClipboardErrorCode.NotInitializedByHost,
                manager.PastePlainText().ErrorCode);
            Assert.AreEqual(WindowsClipboardErrorCode.NotInitializedByHost,
                manager.GetFormats().ErrorCode);
            Assert.AreEqual(WindowsClipboardErrorCode.NotInitializedByHost,
                manager.HasFormat("CF_UNICODETEXT").ErrorCode);
        }

        [UnityTest]
        public IEnumerator WhileDraining_OperationsAreRejectedAsShuttingDown()
        {
            WindowsClipboardManager manager = WindowsClipboardManager.Instance;
            WindowsClipboardManager.SetStateForTests(WindowsClipboardManagerState.Draining);
            yield return null;

            Assert.AreEqual(WindowsClipboardErrorCode.ShuttingDown, manager.Clear().ErrorCode);
            Assert.AreEqual(WindowsClipboardErrorCode.ShuttingDown, manager.PasteImage().ErrorCode);
        }

        [UnityTest]
        public IEnumerator ARejectedReadIsNeverReportedAsAnEmptyClipboard()
        {
            WindowsClipboardManager manager = WindowsClipboardManager.Instance;
            yield return null;

            WindowsClipboardTextResult text = manager.PastePlainText();
            WindowsClipboardBytesResult bytes = manager.PasteImage();
            WindowsClipboardStringListResult list = manager.PasteFiles();

            Assert.IsFalse(text.IsSuccess);
            Assert.IsFalse(text.IsEmpty, "a rejection must not look like an empty clipboard");
            Assert.IsFalse(bytes.IsEmpty);
            Assert.IsFalse(list.IsEmpty);
        }

        // ── Argument validation ──────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator ArgumentValidationRunsBeforeTheNativeCallAndNamesTheOffender()
        {
            WindowsClipboardManager manager = RunningManager();
            yield return null;

            Assert.AreEqual(WindowsClipboardErrorCode.InvalidArgument,
                manager.CopyPlainText(null!).ErrorCode);
            Assert.AreEqual(WindowsClipboardErrorCode.InvalidArgument,
                manager.CopyFiles(new string[0]).ErrorCode);
            Assert.AreEqual(WindowsClipboardErrorCode.InvalidArgument,
                manager.CopyImage(new byte[0]).ErrorCode);
            Assert.AreEqual(WindowsClipboardErrorCode.InvalidArgument,
                manager.HasFormat("  ").ErrorCode);

            WindowsClipboardResult blankPath = manager.CopyFiles(new[] { "  " });
            StringAssert.Contains("paths[0]", blankPath.ErrorMessage!);

            WindowsClipboardResult badPayload = manager.CopyMultipleFormats(new[]
            {
                WindowsClipboardFormatPayload.Text("CF_UNICODETEXT", "ok"),
                WindowsClipboardFormatPayload.Bytes("CF_DIB", null!)
            });
            StringAssert.Contains("items[1]", badPayload.ErrorMessage!);
        }

        // ── Editor compilation ───────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator PastTheGuard_TheEditorReportsPlatformUnavailable()
        {
            // Proves the second compile guard did its job: reaching the DLL from the Editor would
            // raise DllNotFoundException instead of returning a result.
            WindowsClipboardManager manager = RunningManager();
            yield return null;

            Assert.AreEqual(WindowsClipboardErrorCode.PlatformUnavailable,
                manager.CopyPlainText("a").ErrorCode);
            Assert.AreEqual(WindowsClipboardErrorCode.PlatformUnavailable,
                manager.PastePlainText().ErrorCode);
            Assert.AreEqual(WindowsClipboardErrorCode.PlatformUnavailable,
                manager.HasFormat("CF_UNICODETEXT").ErrorCode);
            Assert.AreEqual(WindowsClipboardErrorCode.PlatformUnavailable,
                manager.CanShutdownNow().ErrorCode);
        }

        // ── Asynchronous history requests ────────────────────────────────────────

        [UnityTest]
        public IEnumerator ARejectedRequestStillDeliversExactlyOnce()
        {
            WindowsClipboardManager manager = WindowsClipboardManager.Instance;
            var results = new List<WindowsClipboardHistoryResult>();
            yield return null;

            // Rejected before initialization, so the native callback will never fire and this
            // layer owns the only delivery.
            uint id = manager.GetHistory(results.Add);
            Assert.AreEqual(0u, id);
            Assert.AreEqual(0, results.Count, "delivery happens outside the caller's stack");

            yield return null;
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(WindowsClipboardErrorCode.NotInitializedByHost, results[0].ErrorCode);

            yield return null;
            Assert.AreEqual(1, results.Count, "a queued delivery must not run twice");
        }

        [UnityTest]
        public IEnumerator AnAwaitableCompletesEvenWhenTheRequestIsRejected()
        {
            WindowsClipboardManager manager = WindowsClipboardManager.Instance;
            yield return null;

            Awaitable<WindowsClipboardHistoryResult> pending = manager.GetHistoryAsync();
            yield return null;
            yield return null;

            // Awaitable exposes completion through its awaiter, and it may only be awaited once.
            Assert.IsTrue(pending.GetAwaiter().IsCompleted,
                "a rejected request must not leave an awaiter hanging");
        }

        [UnityTest]
        public IEnumerator AnAcceptedRequestIsDeliveredOnceWhenItsCompletionArrives()
        {
            WindowsClipboardManager manager = RunningManager();
            var results = new List<WindowsClipboardHistoryResult>();
            yield return null;

            // The editor cannot reach the native side, so stand in for the acceptance it would
            // have reported and then for the completion it would have raised.
            WindowsClipboardManager.AcceptRequestsWithIdForTests = 77;
            manager.GetHistory(results.Add);
            WindowsClipboardManager.InjectCompletionForTests(
                77, 0, "[{\"id\":\"a\",\"text\":\"hi\",\"timestamp\":\"1\"}]");
            yield return null;

            Assert.AreEqual(1, results.Count);
            Assert.IsTrue(results[0].IsSuccess);
            Assert.AreEqual(1, results[0].Items.Count);
            Assert.AreEqual("a", results[0].Items[0].Id);
            Assert.AreEqual(0, WindowsClipboardManager.PendingRequestCountForTests);
        }

        [UnityTest]
        public IEnumerator ACompletionForAnUnknownIdIsHarmless()
        {
            WindowsClipboardManager manager = RunningManager();
            yield return null;

            LogAssert.Expect(LogType.Warning, new Regex("unknown request id"));
            WindowsClipboardManager.InjectCompletionForTests(999, 0, "[]");
            yield return null;

            Assert.AreEqual(0, WindowsClipboardManager.PendingRequestCountForTests);
        }

        [UnityTest]
        public IEnumerator ATeardownDeliversARequestWhoseResultWasQueuedButNotYetHandedOver()
        {
            WindowsClipboardManager manager = RunningManager();
            var results = new List<WindowsClipboardHistoryResult>();
            yield return null;

            WindowsClipboardManager.AcceptRequestsWithIdForTests = 11;
            manager.GetHistory(results.Add);
            WindowsClipboardManager.InjectCompletionForTests(11, 0, "[]");

            // The completion is known but its delivery is still queued. A teardown here would drop
            // the result if the registry only tracked requests up to their completion.
            WindowsClipboardManager.DrainForTests();

            Assert.AreEqual(1, results.Count, "the teardown delivers synchronously");
            Assert.IsTrue(results[0].IsSuccess, "the outcome that was already known is kept");

            yield return null;
            Assert.AreEqual(1, results.Count, "the queued delivery becomes a no-op");
        }

        [UnityTest]
        public IEnumerator ATeardownCancelsARequestThatNeverCompleted()
        {
            WindowsClipboardManager manager = RunningManager();
            var results = new List<WindowsClipboardHistoryResult>();
            yield return null;

            WindowsClipboardManager.AcceptRequestsWithIdForTests = 12;
            manager.GetHistory(results.Add);

            WindowsClipboardManager.DrainForTests();

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(WindowsClipboardErrorCode.Canceled, results[0].ErrorCode);
        }

        [UnityTest]
        public IEnumerator DrainingTwiceDeliversOnlyOnce()
        {
            WindowsClipboardManager manager = RunningManager();
            var results = new List<WindowsClipboardHistoryResult>();
            yield return null;

            WindowsClipboardManager.AcceptRequestsWithIdForTests = 13;
            manager.GetHistory(results.Add);

            WindowsClipboardManager.DrainForTests();
            WindowsClipboardManager.DrainForTests();

            Assert.AreEqual(1, results.Count);
        }

        [UnityTest]
        public IEnumerator AMutatingHistoryOperationRejectsASecondCallWhileTheFirstIsInFlight()
        {
            WindowsClipboardManager manager = RunningManager();
            var results = new List<WindowsClipboardResult>();
            yield return null;

            WindowsClipboardManager.AcceptRequestsWithIdForTests = 21;
            manager.RestoreHistoryItem("item-1", results.Add);
            Assert.IsTrue(WindowsClipboardManager.IsInFlightForTests(
                WindowsClipboardManager.OperationRestoreHistoryItem));

            // The running call keeps its result; the second caller is told to try later.
            WindowsClipboardManager.AcceptRequestsWithIdForTests = 22;
            manager.RestoreHistoryItem("item-2", results.Add);
            yield return null;

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(WindowsClipboardErrorCode.OperationBusy, results[0].ErrorCode);

            WindowsClipboardManager.InjectCompletionForTests(21, 0, null);
            yield return null;

            Assert.AreEqual(2, results.Count, "the first call still receives its own result");
            Assert.IsFalse(WindowsClipboardManager.IsInFlightForTests(
                WindowsClipboardManager.OperationRestoreHistoryItem));
        }

        [UnityTest]
        public IEnumerator AReadOnlyHistoryOperationAllowsConcurrentCalls()
        {
            WindowsClipboardManager manager = RunningManager();
            var results = new List<WindowsClipboardAvailabilityResult>();
            yield return null;

            manager.GetHistoryAvailability(results.Add);
            manager.GetHistoryAvailability(results.Add);
            yield return null;

            // Both are rejected in the editor, but neither is rejected as OperationBusy: reads
            // carry no side effect that two callers could disagree about.
            Assert.AreEqual(2, results.Count);
            CollectionAssert.DoesNotContain(
                new[] { results[0].ErrorCode, results[1].ErrorCode },
                WindowsClipboardErrorCode.OperationBusy);
        }

        [UnityTest]
        public IEnumerator AnItemIdIsValidatedBeforeTheRequestStarts()
        {
            WindowsClipboardManager manager = RunningManager();
            var results = new List<WindowsClipboardResult>();
            yield return null;

            uint id = manager.DeleteHistoryItem("  ", results.Add);
            yield return null;

            Assert.AreEqual(0u, id);
            Assert.AreEqual(WindowsClipboardErrorCode.InvalidArgument, results[0].ErrorCode);
            Assert.IsFalse(WindowsClipboardManager.IsInFlightForTests(
                WindowsClipboardManager.OperationDeleteHistoryItem),
                "a rejected call must not leave the marker behind");
        }

        [UnityTest]
        public IEnumerator AnAvailabilityPayloadIsParsedIntoItsFlags()
        {
            WindowsClipboardManager manager = RunningManager();
            var results = new List<WindowsClipboardAvailabilityResult>();
            yield return null;

            WindowsClipboardManager.AcceptRequestsWithIdForTests = 31;
            manager.GetHistoryAvailability(results.Add);
            WindowsClipboardManager.InjectCompletionForTests(
                31, 0, "{\"historyEnabled\":true,\"roamingEnabled\":false}");
            yield return null;

            Assert.AreEqual(1, results.Count);
            Assert.IsTrue(results[0].HistoryEnabled);
            Assert.IsFalse(results[0].RoamingEnabled);
        }

        [UnityTest]
        public IEnumerator AMalformedPayloadFailsInsteadOfLookingEmpty()
        {
            WindowsClipboardManager manager = RunningManager();
            var results = new List<WindowsClipboardHistoryResult>();
            yield return null;

            WindowsClipboardManager.AcceptRequestsWithIdForTests = 41;
            manager.GetHistory(results.Add);
            WindowsClipboardManager.InjectCompletionForTests(41, 0, "{\"not\":\"an array\"}");
            yield return null;

            Assert.AreEqual(WindowsClipboardErrorCode.ResultParseFailed, results[0].ErrorCode);
            Assert.IsFalse(results[0].IsEmpty, "a broken payload must not read as an empty history");
        }

        // ── Delivery ─────────────────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator AResultReachesTheCommonEventBeforeThePerCallCallback()
        {
            WindowsClipboardManager manager = RunningManager();
            var order = new List<string>();
            manager.ClipboardOperationCompleted += _ => order.Add("common");
            yield return null;

            manager.CopyPlainText("a", WindowsClipboardWriteOptions.None, _ => order.Add("perCall"));

            // Delivery happens outside the caller's stack, so nothing has arrived yet.
            Assert.AreEqual(0, order.Count);

            yield return null;
            Assert.AreEqual(new[] { "common", "perCall" }, order.ToArray());
        }

        [UnityTest]
        public IEnumerator EachReadKindReachesItsOwnEvent()
        {
            WindowsClipboardManager manager = RunningManager();
            var seen = new List<string>();
            manager.TextReadCompleted += _ => seen.Add("text");
            manager.StringListReadCompleted += _ => seen.Add("list");
            manager.BytesReadCompleted += _ => seen.Add("bytes");
            manager.FormatPresenceChecked += _ => seen.Add("presence");
            manager.FlagChecked += _ => seen.Add("flag");
            yield return null;

            manager.PastePlainText();
            manager.PasteFiles();
            manager.PasteImage();
            manager.HasFormat("CF_UNICODETEXT");
            manager.CanShutdownNow();
            yield return null;

            CollectionAssert.AreEquivalent(
                new[] { "text", "list", "bytes", "presence", "flag" }, seen);
        }

        [UnityTest]
        public IEnumerator ADestroyedManagerRejectsEveryOperation()
        {
            WindowsClipboardManager manager = WindowsClipboardManager.Instance;
            yield return null;

            Object.DestroyImmediate(manager.gameObject);
            yield return null;

            Assert.IsTrue(WindowsClipboardManager.IsTerminated);

            // Recreating the manager after destruction is designed to complain and then reject
            // everything, rather than silently coming back to life.
            LogAssert.Expect(LogType.Error, new Regex("Recreated after destruction"));
            Assert.AreEqual(WindowsClipboardErrorCode.ManagerDestroyed,
                WindowsClipboardManager.Instance.CopyPlainText("a").ErrorCode);
        }

        [UnityTest]
        public IEnumerator ADuplicateManagerDestroysItselfWithoutTerminatingTheLiveOne()
        {
            WindowsClipboardManager manager = RunningManager();
            yield return null;

            var extra = new GameObject("duplicate").AddComponent<WindowsClipboardManager>();
            yield return null;

            // The duplicate removes itself in Awake, and its OnDestroy must not set the tombstone
            // that would make the live singleton reject everything.
            Assert.IsFalse(WindowsClipboardManager.IsTerminated);
            Assert.AreEqual(WindowsClipboardErrorCode.PlatformUnavailable,
                manager.CopyPlainText("a").ErrorCode);
            if (extra != null) Object.DestroyImmediate(extra.gameObject);
        }
    }
}
#endif
