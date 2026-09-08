#nullable enable

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
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
            // Undo only the thread seam: a shutdown from the wrong thread cannot complete, and
            // OnDestroy would report a terminal failure that belongs to the seam rather than to
            // anything under test.
            WindowsClipboardManager.SetMainThreadIdForTests(
                System.Threading.Thread.CurrentThread.ManagedThreadId);

            // The state is deliberately left as the test set it. OnDestroy runs a real shutdown
            // attempt, and clearing the state first would mean no test ever exercised it. The
            // editor's attempt reports completion without a native manager behind it, so this is
            // safe as well as more faithful.
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
        /// <remarks>
        /// The platform seam is part of that state: the editor is never a Windows player, so
        /// without it the guard would stop every operation before the state was ever consulted.
        /// The native boundary stays compiled out either way.
        /// </remarks>
        private static WindowsClipboardManager RunningManager()
        {
            WindowsClipboardManager manager = WindowsClipboardManager.Instance;
            WindowsClipboardManager.PlatformAvailableForTests = true;
            WindowsClipboardManager.SetStateForTests(WindowsClipboardManagerState.Running);
            return manager;
        }


        // ── Lifecycle, driven rather than injected (design 7.3 and 7.4) ──────────

        [UnityTest]
        public IEnumerator Initialize_InTheEditor_FailsWithoutSubscribingTheQuitHandler()
        {
            WindowsClipboardManager manager = WindowsClipboardManager.Instance;
            yield return null;

            WindowsClipboardResult result = manager.Initialize();

            Assert.AreEqual(WindowsClipboardErrorCode.PlatformUnavailable, result.ErrorCode);
            Assert.AreEqual(WindowsClipboardManagerState.Uninitialized,
                WindowsClipboardManager.StateForTests, "a failed init must not claim to be running");
            Assert.IsFalse(WindowsClipboardManager.QuitHandlerSubscribedForTests,
                "nothing was initialized, so nothing has to be drained before a quit");
        }

        [UnityTest]
        public IEnumerator Initialize_AfterDestruction_ReportsManagerDestroyedRatherThanShuttingDown()
        {
            // OnDestroy can leave the state at Draining. Reading the state before the tombstone
            // would answer ShuttingDown forever, with nothing left to finish the shutdown.
            WindowsClipboardManager manager = WindowsClipboardManager.Instance;
            yield return null;
            WindowsClipboardManager.SetStateForTests(WindowsClipboardManagerState.Draining);
            WindowsClipboardManager.SetTerminatedForTests(true);

            WindowsClipboardResult result = manager.Initialize();

            Assert.AreEqual(WindowsClipboardErrorCode.ManagerDestroyed, result.ErrorCode);
        }

        [UnityTest]
        public IEnumerator Initialize_WhileDraining_IsRejectedAsShuttingDown()
        {
            WindowsClipboardManager manager = WindowsClipboardManager.Instance;
            yield return null;
            WindowsClipboardManager.SetStateForTests(WindowsClipboardManagerState.Draining);

            WindowsClipboardResult result = manager.Initialize();

            Assert.AreEqual(WindowsClipboardErrorCode.ShuttingDown, result.ErrorCode);
        }

        [UnityTest]
        public IEnumerator Initialize_OffTheMainThread_IsRejectedBeforeTheState()
        {
            WindowsClipboardManager manager = WindowsClipboardManager.Instance;
            yield return null;
            WindowsClipboardManager.SetStateForTests(WindowsClipboardManagerState.Running);
            // Standing in for a worker thread: the captured id no longer matches this one.
            WindowsClipboardManager.SetMainThreadIdForTests(
                System.Threading.Thread.CurrentThread.ManagedThreadId + 1);

            WindowsClipboardResult result = manager.Initialize();

            Assert.AreEqual(WindowsClipboardErrorCode.MainThreadRequired, result.ErrorCode,
                "Running would otherwise answer with an idempotent success");
        }

        [UnityTest]
        public IEnumerator OnDestroy_DeliversAQueuedRejectionEvenWhenNothingWasInitialized()
        {
            // Nothing pumps the dispatcher after the manager is gone, so the teardown drain is the
            // only thing standing between a queued rejection and a caller that waits forever.
            WindowsClipboardManager manager = WindowsClipboardManager.Instance;
            var results = new List<WindowsClipboardHistoryResult>();
            yield return null;

            manager.GetHistory(results.Add);
            Assert.AreEqual(0, results.Count, "the delivery is queued, not immediate");

            Object.DestroyImmediate(manager.gameObject);

            Assert.AreEqual(1, results.Count, "the teardown owed this caller its one delivery");
            Assert.IsFalse(results[0].IsSuccess);

            yield return null;
            Assert.AreEqual(1, results.Count, "the queued delivery must not run a second time");
        }

        [UnityTest]
        public IEnumerator EveryShutdownAttemptDrainsTheRegistry_NotJustTheFirst()
        {
            // The drain loop reaches the termination once per attempt. A request registered while
            // the manager was already Draining still has to be handed back.
            WindowsClipboardManager manager = RunningManager();
            var results = new List<WindowsClipboardHistoryResult>();
            yield return null;

            WindowsClipboardManager.NextNativeRequestIdForTests = 91;
            manager.GetHistory(results.Add);
            WindowsClipboardManager.SetStateForTests(WindowsClipboardManagerState.Draining);
            Assert.AreEqual(1, WindowsClipboardManager.PendingRequestCountForTests);

            WindowsClipboardManager.InjectShutdownResultForTests(false, WindowsClipboardErrorCode.Busy);

            Assert.AreEqual(0, WindowsClipboardManager.PendingRequestCountForTests,
                "an attempt that made no progress still owes the registry its drain");
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(WindowsClipboardErrorCode.Canceled, results[0].ErrorCode);
        }

        [UnityTest]
        public IEnumerator Quit_IsHeldBackOnceAndThenResumedExactlyOnce()
        {
            WindowsClipboardManager manager = RunningManager();
            int quits = 0;
            WindowsClipboardManager.QuitActionForTests = () => quits++;
            yield return null;

            bool firstAnswer = WindowsClipboardManager.InvokeWantsToQuitForTests();

            Assert.IsFalse(firstAnswer, "the quit waits for the drain");
            yield return null;
            yield return null;

            Assert.AreEqual(1, quits, "the drain resumes the quit exactly once");
            Assert.IsTrue(WindowsClipboardManager.QuitDrainCompletedForTests);
            Assert.IsFalse(WindowsClipboardManager.DrainRunningForTests);
            Assert.AreEqual(WindowsClipboardManagerState.ShutDown, WindowsClipboardManager.StateForTests);

            Assert.IsTrue(WindowsClipboardManager.InvokeWantsToQuitForTests(),
                "a second request must go straight through rather than wait again");
            Assert.AreEqual(1, quits);
        }

        [UnityTest]
        public IEnumerator Quit_WithoutAManagerGoesStraightThrough()
        {
            // Refusing a quit that nothing can ever resume would leave the application unable to
            // exit, which is worse than shutting down without the drain.
            WindowsClipboardManager.ResetForTests();
            yield return null;

            Assert.IsTrue(WindowsClipboardManager.InvokeWantsToQuitForTests());
            Assert.IsTrue(WindowsClipboardManager.QuitDrainCompletedForTests);
        }

        [UnityTest]
        public IEnumerator ShutdownWithDrain_AfterItFinished_IsAnIdempotentSuccess()
        {
            WindowsClipboardManager manager = RunningManager();
            var results = new List<WindowsClipboardResult>();
            yield return null;

            manager.ShutdownWithDrain(results.Add);
            yield return null;
            yield return null;
            Assert.AreEqual(1, results.Count);
            Assert.IsTrue(results[0].IsSuccess);
            Assert.AreEqual(WindowsClipboardManagerState.ShutDown, WindowsClipboardManager.StateForTests);

            manager.ShutdownWithDrain(results.Add);
            yield return null;
            yield return null;

            Assert.AreEqual(2, results.Count);
            Assert.IsTrue(results[1].IsSuccess, "there is nothing left to release");
            Assert.IsFalse(WindowsClipboardManager.DrainRunningForTests);
        }


        [UnityTest]
        public IEnumerator Drain_RetriesAcrossFramesWhileTheNativeSideReportsProgress()
        {
            WindowsClipboardManager manager = RunningManager();
            int attempts = 0;
            // Refuses twice, then finishes: the shape the native contract asks callers to expect.
            WindowsClipboardManager.NativeShutdownForTests =
                () => ++attempts < 3
                    ? (false, WindowsClipboardErrorCode.Busy)
                    : (true, WindowsClipboardErrorCode.None);
            var results = new List<WindowsClipboardResult>();
            yield return null;

            manager.ShutdownWithDrain(results.Add);

            for (int i = 0; i < 6 && results.Count == 0; i++) yield return null;

            Assert.AreEqual(3, attempts, "the drain keeps trying until the native side finishes");
            Assert.AreEqual(1, results.Count);
            Assert.IsTrue(results[0].IsSuccess);
            Assert.AreEqual(WindowsClipboardManagerState.ShutDown, WindowsClipboardManager.StateForTests);
        }

        [UnityTest]
        public IEnumerator Drain_WhileDraining_OperationsAreRefusedAsShuttingDown()
        {
            // The state has to advance on the first attempt, not once the drain is over: for the
            // whole retry window an operation would otherwise reach the native side.
            WindowsClipboardManager manager = RunningManager();
            WindowsClipboardManager.SetShutdownBudgetForTests(6, 5f);
            WindowsClipboardManager.NativeShutdownForTests =
                () => (false, WindowsClipboardErrorCode.Busy);
            var results = new List<WindowsClipboardResult>();
            yield return null;

            // Expected before the fact: LogAssert only matches messages logged after the call.
            LogAssert.Expect(LogType.Error, new Regex("exceeded its budget"));
            manager.ShutdownWithDrain(results.Add);
            yield return null;
            yield return null;

            Assert.AreEqual(WindowsClipboardManagerState.Draining,
                WindowsClipboardManager.StateForTests,
                "the state has to advance on the first attempt, not once the drain is over");
            Assert.AreEqual(WindowsClipboardErrorCode.ShuttingDown, manager.Clear().ErrorCode);
            Assert.AreEqual(WindowsClipboardErrorCode.ShuttingDown, manager.PasteImage().ErrorCode);

            // Let it run out so the coroutine is not left alive past this test.
            for (int i = 0; i < 10 && results.Count == 0; i++) yield return null;
        }

        [UnityTest]
        public IEnumerator Drain_ThatNeverFinishes_EndsAsShutdownTimeoutAndKeepsRefusing()
        {
            WindowsClipboardManager manager = RunningManager();
            WindowsClipboardManager.SetShutdownBudgetForTests(3, 5f);
            WindowsClipboardManager.NativeShutdownForTests =
                () => (false, WindowsClipboardErrorCode.Busy);
            var results = new List<WindowsClipboardResult>();
            yield return null;

            // Expected before the fact: LogAssert only matches messages logged after the call.
            LogAssert.Expect(LogType.Error, new Regex("exceeded its budget"));
            manager.ShutdownWithDrain(results.Add);
            for (int i = 0; i < 8 && results.Count == 0; i++) yield return null;

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(WindowsClipboardErrorCode.ShutdownTimeout, results[0].ErrorCode);
            Assert.AreEqual(WindowsClipboardManagerState.ShutdownFailed,
                WindowsClipboardManager.StateForTests,
                "Draining would claim a shutdown is still making progress");
            Assert.AreEqual(WindowsClipboardErrorCode.ShuttingDown, manager.Clear().ErrorCode);
        }

        [UnityTest]
        public IEnumerator Drain_OnAPartialReservation_TriesTheRecoveryOnceBeforeGivingUp()
        {
            // The native uninit refuses to finish for as long as a reservation is half applied, so
            // the recovery is part of the drain. Without it the reserved formats are dropped with
            // no error at all, and the public RecoverDeferredState cannot help: it is refused
            // while the manager is draining.
            WindowsClipboardManager manager = RunningManager();
            WindowsClipboardManager.SetShutdownBudgetForTests(4, 5f);
            WindowsClipboardManager.NativeShutdownForTests =
                () => (false, WindowsClipboardErrorCode.PartialState);
            var results = new List<WindowsClipboardResult>();
            yield return null;

            // Expected before the fact: LogAssert only matches messages logged after the call.
            LogAssert.Expect(LogType.Error, new Regex("exceeded its budget"));
            manager.ShutdownWithDrain(results.Add);
            for (int i = 0; i < 10 && results.Count == 0; i++) yield return null;

            Assert.AreEqual(1, WindowsClipboardManager.RecoverDeferredCallCountForTests,
                "the recovery is tried once, not once per frame and not never");
        }

        [UnityTest]
        public IEnumerator Drain_OnAnOrdinaryRefusal_DoesNotReachForTheRecovery()
        {
            WindowsClipboardManager manager = RunningManager();
            WindowsClipboardManager.SetShutdownBudgetForTests(3, 5f);
            WindowsClipboardManager.NativeShutdownForTests =
                () => (false, WindowsClipboardErrorCode.Busy);
            var results = new List<WindowsClipboardResult>();
            yield return null;

            // Expected before the fact: LogAssert only matches messages logged after the call.
            LogAssert.Expect(LogType.Error, new Regex("exceeded its budget"));
            manager.ShutdownWithDrain(results.Add);
            for (int i = 0; i < 8 && results.Count == 0; i++) yield return null;

            Assert.AreEqual(0, WindowsClipboardManager.RecoverDeferredCallCountForTests,
                "nothing is partial here, so there is nothing to recover from");
        }


        // ── Cancellation and completion (design 7.6.1, 7.6.5 and 8.2) ────────────

        [UnityTest]
        public IEnumerator AnAlreadyCancelledTokenCompletesWithoutReachingTheNativeSide()
        {
            WindowsClipboardManager manager = RunningManager();
            var cts = new CancellationTokenSource();
            cts.Cancel();
            // If the request reached the bridge it would take this id with it.
            WindowsClipboardManager.NextNativeRequestIdForTests = 61;
            yield return null;

            Awaitable<WindowsClipboardHistoryResult> awaitable = manager.GetHistoryAsync(cts.Token);
            yield return null;

            Assert.AreEqual(61u, WindowsClipboardManager.NextNativeRequestIdForTests,
                "a token that is already cancelled must not start a native request");
            Assert.IsTrue(awaitable.GetAwaiter().IsCompleted);
            Assert.AreEqual(WindowsClipboardErrorCode.Canceled,
                awaitable.GetAwaiter().GetResult().ErrorCode);
        }

        [UnityTest]
        public IEnumerator CancellingFromAWorkerThreadDoesNotReportMainThreadRequired()
        {
            // The registered callback runs on whichever thread cancelled, and the native cancel is
            // main thread only, so it has to hop before it touches anything.
            WindowsClipboardManager manager = RunningManager();
            var seen = new List<WindowsClipboardResult>();
            manager.ClipboardOperationCompleted += seen.Add;
            var cts = new CancellationTokenSource();
            WindowsClipboardManager.NextNativeRequestIdForTests = 62;
            yield return null;

            manager.GetHistoryAsync(cts.Token);
            Task.Run(() => cts.Cancel());

            for (int i = 0; i < 30 && seen.Count == 0; i++) yield return null;

            Assert.AreEqual(1, seen.Count, "the cancel reached the manager");
            Assert.AreEqual(WindowsClipboardManager.OperationCancelRequest, seen[0].Operation);
            Assert.AreNotEqual(WindowsClipboardErrorCode.MainThreadRequired, seen[0].ErrorCode,
                "the callback ran on the cancelling thread instead of hopping to the main one");
        }

        [UnityTest]
        public IEnumerator ADeliveredRequestGivesBackItsCancellationRegistration()
        {
            WindowsClipboardManager manager = RunningManager();
            var cts = new CancellationTokenSource();
            WindowsClipboardManager.NextNativeRequestIdForTests = 63;
            yield return null;

            manager.GetHistoryAsync(cts.Token);
            uint ticket = WindowsClipboardManager.OnlyPendingTicketForTests();
            Assert.IsTrue(WindowsClipboardManager.HasCancellationRegistrationForTests(ticket));

            WindowsClipboardManager.InjectCompletionForTests(63, 0, "[]");
            yield return null;

            Assert.AreEqual(0, WindowsClipboardManager.PendingRequestCountForTests);

            // The disposal is handed to the pool, so wait for it rather than assume the frame.
            for (int i = 0;
                 i < 60 && WindowsClipboardManager.CancellationRegistrationsReleasedForTests == 0;
                 i++)
            {
                yield return null;
            }

            Assert.AreEqual(1, WindowsClipboardManager.CancellationRegistrationsReleasedForTests,
                "a registration left behind keeps its token source alive for the whole session");
        }

        [UnityTest]
        public IEnumerator TwoReadOnlyRequestsAreAcceptedAtOnceAndEachKeepsItsOwnResult()
        {
            // The design allows the read-only operations to run concurrently, so two accepted
            // requests have to keep their own native ids and their own callbacks.
            WindowsClipboardManager manager = RunningManager();
            var history = new List<WindowsClipboardHistoryResult>();
            var availability = new List<WindowsClipboardAvailabilityResult>();
            yield return null;

            WindowsClipboardManager.NextNativeRequestIdForTests = 71;
            manager.GetHistory(history.Add);
            WindowsClipboardManager.NextNativeRequestIdForTests = 72;
            manager.GetHistoryAvailability(availability.Add);

            Assert.AreEqual(2, WindowsClipboardManager.PendingRequestCountForTests);

            WindowsClipboardManager.InjectCompletionForTests(
                72, 0, "{\"historyEnabled\":true,\"roamingEnabled\":false}");
            WindowsClipboardManager.InjectCompletionForTests(71, 0, "[]");
            yield return null;

            Assert.AreEqual(1, history.Count);
            Assert.AreEqual(1, availability.Count);
            Assert.IsTrue(availability[0].IsSuccess);
            Assert.IsTrue(availability[0].HistoryEnabled);
            Assert.IsTrue(history[0].IsSuccess);
        }

        [UnityTest]
        public IEnumerator AFailedCompletionKeepsItsNativeCodeRatherThanBecomingAParseFailure()
        {
            // The native side reports these after it has accepted the request, and the payload is
            // null when it does. Parsing that null and reporting ResultParseFailed would hide
            // every one of them behind the same code.
            WindowsClipboardErrorCode[] codes =
            {
                WindowsClipboardErrorCode.AccessDenied,
                WindowsClipboardErrorCode.HistoryDisabled,
                WindowsClipboardErrorCode.ItemDeleted,
                WindowsClipboardErrorCode.Canceled,
                WindowsClipboardErrorCode.NotForeground
            };

            WindowsClipboardManager manager = RunningManager();
            yield return null;

            uint id = 80;
            foreach (WindowsClipboardErrorCode code in codes)
            {
                id++;
                var results = new List<WindowsClipboardHistoryResult>();
                WindowsClipboardManager.NextNativeRequestIdForTests = id;
                manager.GetHistory(results.Add);

                WindowsClipboardManager.InjectCompletionForTests(id, (int)code, null);
                yield return null;

                Assert.AreEqual(1, results.Count, code + " was never delivered");
                Assert.AreEqual(code, results[0].ErrorCode);
                Assert.IsFalse(results[0].IsSuccess);
                Assert.IsNotNull(results[0].ErrorMessage);
            }
        }

        [UnityTest]
        public IEnumerator WithoutADispatcherARejectionWaitsForTheTeardownRatherThanRunningInline()
        {
            WindowsClipboardManager manager = WindowsClipboardManager.Instance;
            var results = new List<WindowsClipboardHistoryResult>();
            yield return null;
            WindowsClipboardManager.ClearDispatcherForTests();

            manager.GetHistory(results.Add);

            Assert.AreEqual(0, results.Count,
                "delivering here would run the callback before the call that started it returned");

            WindowsClipboardManager.DrainForTests();

            Assert.AreEqual(1, results.Count, "the teardown still owes this caller its delivery");
        }

        [UnityTest]
        public IEnumerator AnExceptionWhereTheNativeCallWouldBeStillDeliversAndFreesTheOperation()
        {
            // Nothing is in the registry yet at that point, so an exception that escaped would
            // strand both the caller and the in-flight marker for the rest of the session.
            WindowsClipboardManager manager = RunningManager();
            var results = new List<WindowsClipboardResult>();
            yield return null;

            WindowsClipboardManager.RequestExceptionForTests = new System.BadImageFormatException("boom");
            LogAssert.Expect(LogType.Error, new Regex("BadImageFormatException"));

            uint id = manager.RestoreHistoryItem("item-1", results.Add);
            yield return null;

            Assert.AreEqual(0u, id);
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(WindowsClipboardErrorCode.Unknown, results[0].ErrorCode);
            Assert.IsFalse(
                WindowsClipboardManager.IsInFlightForTests(
                    WindowsClipboardManager.OperationRestoreHistoryItem),
                "the operation would otherwise report Busy for the rest of the session");
        }


        [UnityTest]
        public IEnumerator TheHistoryEventsReachASubscriberThatOnlyListensToThem()
        {
            // A caller can subscribe to the common event instead of passing a callback, and for
            // these two kinds that was the only way left untested.
            WindowsClipboardManager manager = RunningManager();
            var history = new List<WindowsClipboardHistoryResult>();
            var availability = new List<WindowsClipboardAvailabilityResult>();
            manager.HistoryReadCompleted += history.Add;
            manager.HistoryAvailabilityChecked += availability.Add;
            yield return null;

            WindowsClipboardManager.NextNativeRequestIdForTests = 101;
            manager.GetHistory();
            WindowsClipboardManager.NextNativeRequestIdForTests = 102;
            manager.GetHistoryAvailability();

            WindowsClipboardManager.InjectCompletionForTests(101, 0, "[]");
            WindowsClipboardManager.InjectCompletionForTests(
                102, 0, "{\"historyEnabled\":true,\"roamingEnabled\":true}");
            yield return null;

            Assert.AreEqual(1, history.Count, "HistoryReadCompleted never fired");
            Assert.AreEqual(1, availability.Count, "HistoryAvailabilityChecked never fired");
            Assert.IsTrue(availability[0].HistoryEnabled);
        }

        [UnityTest]
        public IEnumerator TryShutdown_FiresNeitherTheEventNorACallback()
        {
            // The drain calls this once per frame. Delivering here would reach every subscriber on
            // every attempt, which is why it is the one operation that reports only by returning.
            WindowsClipboardManager manager = RunningManager();
            var seen = new List<WindowsClipboardResult>();
            manager.ClipboardOperationCompleted += seen.Add;
            yield return null;

            WindowsClipboardResult result = manager.TryShutdown(out bool completed);
            yield return null;
            yield return null;

            Assert.IsTrue(completed);
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(0, seen.Count, "TryShutdown reports by returning, and only by returning");
        }

        [UnityTest]
        public IEnumerator ARequestTheBridgeRefusesWithoutACodeIsAFailureRatherThanASuccess()
        {
            // Defensive: the native contract says this cannot happen. If it does, the alternative
            // is delivering a success - an empty history, or a restore that never restored.
            WindowsClipboardManager manager = RunningManager();
            var results = new List<WindowsClipboardHistoryResult>();
            yield return null;

            WindowsClipboardManager.NextNativeRequestIdForTests = 0;
            manager.GetHistory(results.Add);
            yield return null;

            Assert.AreEqual(1, results.Count);
            Assert.IsFalse(results[0].IsSuccess);
            Assert.AreEqual(WindowsClipboardErrorCode.RequestRejected, results[0].ErrorCode);
        }


        // ── What the fixes for review v2 brought with them ───────────────────────

        [UnityTest]
        public IEnumerator ACancellationOnlyCancelsTheRequestItWasRegisteredFor()
        {
            // The registration outlives its own request: the disposal that would remove it is
            // handed to the pool. Matching on the native id alone would then let a stale callback
            // cancel whichever request inherited that id.
            WindowsClipboardManager manager = RunningManager();
            var seen = new List<WindowsClipboardResult>();
            manager.ClipboardOperationCompleted += seen.Add;
            var cts = new CancellationTokenSource();
            // Otherwise the pool disposes the registration before the cancel and the window this
            // test is about never opens.
            WindowsClipboardManager.SuppressRegistrationDisposalForTests = true;
            yield return null;

            WindowsClipboardManager.NextNativeRequestIdForTests = 63;
            manager.GetHistoryAsync(cts.Token);

            // A completes, then B is accepted under the id the native side reused.
            WindowsClipboardManager.InjectCompletionForTests(63, 0, "[]");
            yield return null;
            var second = new List<WindowsClipboardHistoryResult>();
            WindowsClipboardManager.NextNativeRequestIdForTests = 63;
            manager.GetHistory(second.Add);
            seen.Clear();

            cts.Cancel();
            for (int i = 0; i < 10; i++) yield return null;

            Assert.AreEqual(0, seen.Count,
                "the stale registration cancelled a request that was never its own");
            Assert.AreEqual(1, WindowsClipboardManager.PendingRequestCountForTests,
                "the second request is still running");
        }

        [UnityTest]
        public IEnumerator ADrainOnAnInactiveManagerStillAnswersItsCaller()
        {
            // Update drives the attempts and a disabled object gets none, so the drain would never
            // advance. Making the one attempt that is possible beats leaving the caller waiting.
            WindowsClipboardManager manager = RunningManager();
            var results = new List<WindowsClipboardResult>();
            yield return null;
            manager.gameObject.SetActive(false);

            manager.ShutdownWithDrain(results.Add);
            yield return null;

            Assert.IsNull(WindowsClipboardManager.DrainPhaseForTests,
                "a drain that cannot advance must not be left in flight");
            Assert.AreEqual(1, results.Count, "the caller is still owed its result");

            manager.gameObject.SetActive(true);
        }

        [UnityTest]
        public IEnumerator ADrainInterruptedByDestructionStillAnswersItsCaller()
        {
            WindowsClipboardManager manager = RunningManager();
            WindowsClipboardManager.NativeShutdownForTests =
                () => (false, WindowsClipboardErrorCode.Busy);
            var results = new List<WindowsClipboardResult>();
            yield return null;

            manager.ShutdownWithDrain(results.Add);
            yield return null;
            Assert.IsTrue(WindowsClipboardManager.DrainRunningForTests);

            // The coroutine dies with the object, so nothing would ever reach its tail.
            Object.DestroyImmediate(manager.gameObject);

            Assert.AreEqual(1, results.Count, "the teardown owes this caller its one answer");
            Assert.IsFalse(WindowsClipboardManager.DrainRunningForTests);
        }

        [UnityTest]
        public IEnumerator AQuitHandsOverTheShutdownResultBeforeItLetsTheQuitThrough()
        {
            // Nothing is guaranteed to pump the dispatcher after the quit resumes, so a queued
            // delivery would never arrive.
            WindowsClipboardManager manager = RunningManager();
            var results = new List<WindowsClipboardResult>();
            int quitsWhenDelivered = -1;
            int quits = 0;
            WindowsClipboardManager.QuitActionForTests = () => quits++;
            yield return null;

            manager.ShutdownWithDrain(r =>
            {
                results.Add(r);
                quitsWhenDelivered = quits;
            });
            Assert.IsFalse(WindowsClipboardManager.InvokeWantsToQuitForTests());

            for (int i = 0; i < 10 && results.Count == 0; i++) yield return null;

            Assert.AreEqual(1, results.Count, "the drain callback never arrived");
            Assert.AreEqual(0, quitsWhenDelivered,
                "the result has to be handed over before the quit is let through");
            Assert.AreEqual(1, quits);
        }

        [UnityTest]
        public IEnumerator OneDrainCallbackThrowingDoesNotSwallowAnother()
        {
            // Each caller asked separately and is owed its own answer.
            WindowsClipboardManager manager = RunningManager();
            var second = new List<WindowsClipboardResult>();
            yield return null;

            manager.ShutdownWithDrain(_ => throw new System.InvalidOperationException("boom"));
            manager.ShutdownWithDrain(second.Add);
            LogAssert.Expect(LogType.Error, new Regex("drain callback threw"));

            for (int i = 0; i < 10 && second.Count == 0; i++) yield return null;

            Assert.AreEqual(1, second.Count,
                "the second caller lost its result to the first caller's exception");
        }


        // ── The drain session (review v4) ────────────────────────────────────────
        // The drain used to be four separate flags. These cover the states that could not be
        // told apart before: a runner that died, a delivery queued but not yet made, and a
        // settle in progress.

        [UnityTest]
        public IEnumerator ADrainSurvivesStopAllCoroutines()
        {
            // It used to be a coroutine, which anything could stop without leaving a trace, and
            // the caller would then wait forever on a drain that no longer existed.
            WindowsClipboardManager manager = RunningManager();
            int attempts = 0;
            WindowsClipboardManager.NativeShutdownForTests =
                () => ++attempts < 3
                    ? (false, WindowsClipboardErrorCode.Busy)
                    : (true, WindowsClipboardErrorCode.None);
            var results = new List<WindowsClipboardResult>();
            yield return null;

            manager.ShutdownWithDrain(results.Add);
            yield return null;
            manager.StopAllCoroutines();

            for (int i = 0; i < 10 && results.Count == 0; i++) yield return null;

            Assert.AreEqual(1, results.Count, "the drain no longer depends on a coroutine");
            Assert.IsTrue(results[0].IsSuccess);
        }

        [UnityTest]
        public IEnumerator AnAttemptThatThrowsEndsTheDrainRatherThanStallingIt()
        {
            WindowsClipboardManager manager = RunningManager();
            // Throws once: the teardown makes its own attempt, and a second throw there would be
            // this seam talking rather than anything under test.
            bool thrown = false;
            WindowsClipboardManager.NativeShutdownForTests = () =>
            {
                if (thrown) return (true, WindowsClipboardErrorCode.None);
                thrown = true;
                throw new System.InvalidOperationException("boom");
            };
            var results = new List<WindowsClipboardResult>();
            yield return null;

            LogAssert.Expect(LogType.Error, new Regex("InvalidOperationException"));
            manager.ShutdownWithDrain(results.Add);

            for (int i = 0; i < 10 && results.Count == 0; i++) yield return null;

            Assert.AreEqual(1, results.Count, "a throwing attempt still owes the caller an answer");
            Assert.IsFalse(results[0].IsSuccess);
            Assert.IsNull(WindowsClipboardManager.DrainPhaseForTests);
        }

        [UnityTest]
        public IEnumerator AQuitPendingWhenTheManagerIsDestroyedIsStillResumed()
        {
            // The quit was refused once. If the drain carrying it disappears without resuming it,
            // nothing else will, and the application can never exit.
            WindowsClipboardManager manager = RunningManager();
            WindowsClipboardManager.NativeShutdownForTests =
                () => (false, WindowsClipboardErrorCode.Busy);
            int quits = 0;
            WindowsClipboardManager.QuitActionForTests = () => quits++;
            yield return null;

            Assert.IsFalse(WindowsClipboardManager.InvokeWantsToQuitForTests());
            yield return null;
            Assert.AreEqual(WindowsClipboardQuitState.WaitingForDrain,
                WindowsClipboardManager.QuitStateForTests);

            LogAssert.Expect(LogType.Error, new Regex("quitting with an incomplete shutdown"));
            Object.DestroyImmediate(manager.gameObject);

            Assert.AreEqual(1, quits, "the destroyed drain still had a quit to let through");
            Assert.AreEqual(WindowsClipboardQuitState.Resumed,
                WindowsClipboardManager.QuitStateForTests);
        }

        [UnityTest]
        public IEnumerator ATeardownClaimsADeliveryThatWasQueuedButNotYetMade()
        {
            // Between the drain finishing and the dispatcher running it, the result exists but has
            // not been handed over. That used to look exactly like a finished drain, so a teardown
            // in this window dropped the result.
            WindowsClipboardManager manager = RunningManager();
            var results = new List<WindowsClipboardResult>();
            yield return null;

            manager.ShutdownWithDrain(results.Add);
            WindowsClipboardManager.StepDrainForTests();

            Assert.AreEqual(WindowsClipboardDrainPhase.DeliveryQueued,
                WindowsClipboardManager.DrainPhaseForTests);
            Assert.AreEqual(0, results.Count, "the delivery is queued, not yet made");

            Object.DestroyImmediate(manager.gameObject);

            Assert.AreEqual(1, results.Count, "the teardown owed this caller the queued result");

            yield return null;
            Assert.AreEqual(1, results.Count, "the queued delivery must not run a second time");
        }

        [UnityTest]
        public IEnumerator ACallerJoiningAFailedDrainMidSettleIsAnsweredByTheSamePass()
        {
            // After a terminal failure the manager is not ShutDown, so a callback calling
            // ShutdownWithDrain again does not take the idempotent-success shortcut: it joins the
            // session that is settling right now, and has to be served by that same pass.
            WindowsClipboardManager manager = RunningManager();
            // Fails terminally once. The teardown makes its own attempt, and failing that one too
            // would be this seam talking rather than anything under test.
            bool failed = false;
            WindowsClipboardManager.NativeShutdownForTests = () =>
            {
                if (failed) return (true, WindowsClipboardErrorCode.None);
                failed = true;
                return (false, WindowsClipboardErrorCode.WrongThread);
            };
            var first = new List<WindowsClipboardResult>();
            var joined = new List<WindowsClipboardResult>();
            yield return null;

            LogAssert.Expect(LogType.Error, new Regex("terminal shutdown failure"));
            manager.ShutdownWithDrain(r =>
            {
                first.Add(r);
                manager.ShutdownWithDrain(joined.Add);
            });

            for (int i = 0; i < 10 && first.Count == 0; i++) yield return null;

            Assert.AreEqual(1, first.Count);
            Assert.AreEqual(WindowsClipboardManagerState.ShutdownFailed,
                WindowsClipboardManager.StateForTests, "the shortcut must not apply here");
            Assert.AreEqual(1, joined.Count,
                "a caller that joined while the settle ran was left without an answer");
        }

        [UnityTest]
        public IEnumerator ACallerArrivingWhileTheDrainSettlesIsAnsweredByTheSamePass()
        {
            // A callback may start another shutdown. During a quit settle there is no next frame
            // to deliver it on, so it has to join the pass that is already running.
            WindowsClipboardManager manager = RunningManager();
            var first = new List<WindowsClipboardResult>();
            var second = new List<WindowsClipboardResult>();
            WindowsClipboardManager.QuitActionForTests = () => { };
            yield return null;

            manager.ShutdownWithDrain(r =>
            {
                first.Add(r);
                manager.ShutdownWithDrain(second.Add);
            });
            Assert.IsFalse(WindowsClipboardManager.InvokeWantsToQuitForTests());

            for (int i = 0; i < 10 && first.Count == 0; i++) yield return null;

            Assert.AreEqual(1, first.Count);
            Assert.AreEqual(1, second.Count,
                "the re-entrant caller would otherwise wait for a frame that never comes");
        }

        // ── Rejection paths ──────────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator EveryOperationIsRejectedWithoutReachingTheNativeSide()
        {
            // The editor is not a Windows player, so the guard stops every operation with
            // PlatformUnavailable. What this test pins is that none of them reach the bridge; the
            // ordering behind the answer is checked on the classifier in EditMode.
            WindowsClipboardManager manager = WindowsClipboardManager.Instance;
            yield return null;

            Assert.AreEqual(WindowsClipboardErrorCode.PlatformUnavailable,
                manager.CopyPlainText("a").ErrorCode);
            Assert.AreEqual(WindowsClipboardErrorCode.PlatformUnavailable,
                manager.PastePlainText().ErrorCode);
            Assert.AreEqual(WindowsClipboardErrorCode.PlatformUnavailable,
                manager.GetFormats().ErrorCode);
            Assert.AreEqual(WindowsClipboardErrorCode.PlatformUnavailable,
                manager.HasFormat("CF_UNICODETEXT").ErrorCode);
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

            // The read protocol runs for real here and reports the refusal it got, so the error it
            // logs is part of the expected behaviour rather than a surprise.
            LogAssert.Expect(LogType.Error, new Regex("sizing failed: PlatformUnavailable"));
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
            Assert.AreEqual(WindowsClipboardErrorCode.PlatformUnavailable, results[0].ErrorCode);

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
            WindowsClipboardManager.NextNativeRequestIdForTests = 77;
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

            WindowsClipboardManager.NextNativeRequestIdForTests = 11;
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

            WindowsClipboardManager.NextNativeRequestIdForTests = 12;
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

            WindowsClipboardManager.NextNativeRequestIdForTests = 13;
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

            WindowsClipboardManager.NextNativeRequestIdForTests = 21;
            manager.RestoreHistoryItem("item-1", results.Add);
            Assert.IsTrue(WindowsClipboardManager.IsInFlightForTests(
                WindowsClipboardManager.OperationRestoreHistoryItem));

            // The running call keeps its result; the second caller is told to try later.
            WindowsClipboardManager.NextNativeRequestIdForTests = 22;
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

            WindowsClipboardManager.NextNativeRequestIdForTests = 31;
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

            WindowsClipboardManager.NextNativeRequestIdForTests = 41;
            manager.GetHistory(results.Add);
            WindowsClipboardManager.InjectCompletionForTests(41, 0, "{\"not\":\"an array\"}");
            yield return null;

            Assert.AreEqual(WindowsClipboardErrorCode.ResultParseFailed, results[0].ErrorCode);
            Assert.IsFalse(results[0].IsEmpty, "a broken payload must not read as an empty history");
        }

        // ── History events ───────────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator HistoryEventsReachTheirSubscribersThroughTheDispatcher()
        {
            WindowsClipboardManager manager = RunningManager();
            var seen = new List<string>();
            manager.HistoryChanged += () => seen.Add("history");
            manager.HistoryEnabledChanged += enabled => seen.Add($"historyEnabled:{enabled}");
            manager.RoamingEnabledChanged += enabled => seen.Add($"roamingEnabled:{enabled}");
            yield return null;

            WindowsClipboardManager.InjectHistoryChangedForTests();
            WindowsClipboardManager.InjectHistoryEnabledChangedForTests(true);
            WindowsClipboardManager.InjectRoamingEnabledChangedForTests(false);

            Assert.AreEqual(0, seen.Count, "native callbacks are delivered outside their own stack");

            yield return null;
            Assert.AreEqual(new[] { "history", "historyEnabled:True", "roamingEnabled:False" }, seen.ToArray());
        }

        [UnityTest]
        public IEnumerator SetHistoryEventsEnabled_IsRejectedBeforeTheGuardLetsItThrough()
        {
            WindowsClipboardManager manager = WindowsClipboardManager.Instance;
            var seen = new List<WindowsClipboardResult>();
            manager.ClipboardOperationCompleted += seen.Add;
            yield return null;

            WindowsClipboardResult result = manager.SetHistoryEventsEnabled(true);
            yield return null;

            Assert.AreEqual(WindowsClipboardErrorCode.PlatformUnavailable, result.ErrorCode);
            Assert.AreEqual(1, seen.Count, "a rejection is delivered like any other result");
            Assert.AreEqual(WindowsClipboardManager.OperationSetHistoryEvents, seen[0].Operation);
        }

        [UnityTest]
        public IEnumerator SetHistoryEventsEnabled_ReportsTheFailureWhenTheNativeCallFails()
        {
            WindowsClipboardManager manager = RunningManager();
            yield return null;

            // The editor compiles the native boundary out, so this stands in for any failure.
            WindowsClipboardResult result = manager.SetHistoryEventsEnabled(true);

            Assert.AreEqual(WindowsClipboardErrorCode.PlatformUnavailable, result.ErrorCode);
            Assert.IsNotNull(result.ErrorMessage);
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

            // Each read reports the refusal the stand-in native side gives it.
            for (int i = 0; i < 3; i++)
            {
                LogAssert.Expect(LogType.Error, new Regex("sizing failed: PlatformUnavailable"));
            }

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
