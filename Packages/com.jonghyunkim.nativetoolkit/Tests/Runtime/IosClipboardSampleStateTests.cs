#nullable enable

#if UNITY_EDITOR
using JonghyunKim.NativeToolkit.Runtime.Clipboard;
using NUnit.Framework;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// EditMode tests for the iOS clipboard sample's pure helpers.
    /// </summary>
    /// <remarks>
    /// The controller itself is a MonoBehaviour that resolves a UIDocument in Start, so the state
    /// rules that matter - a completion is labelled with its own call context, and only the owning
    /// callback may change the observation state - are verified through the extracted helpers.
    /// </remarks>
    public sealed class IosClipboardSampleStateTests
    {
        private const string CancelledCode = "CLIPBOARD_CANCELLED";

        // ── Result formatting ────────────────────────────────────────────────

        [Test]
        public void FormatSuccess_KeepsSequenceAndMarker()
        {
            var context = new IosClipboardSampleResultContext(12, "copy.plainText");

            string line = IosClipboardSampleResult.FormatSuccess(context, "items=2");

            Assert.AreEqual("#12 [copy.plainText] OK items=2", line);
        }

        [Test]
        public void FormatSuccess_WithoutPayload_OmitsTrailingSpace()
        {
            var context = new IosClipboardSampleResultContext(3, "clear");

            Assert.AreEqual("#3 [clear] OK", IosClipboardSampleResult.FormatSuccess(context, string.Empty));
        }

        [Test]
        public void FormatRunning_MarksTheCallAsPending()
        {
            var context = new IosClipboardSampleResultContext(1, "read");

            Assert.AreEqual("#1 [read] ...", IosClipboardSampleResult.FormatRunning(context));
        }

        [Test]
        public void FormatFailure_UsesNgForOrdinaryErrors()
        {
            var context = new IosClipboardSampleResultContext(7, "copy.plainText");
            var error = IosClipboardErrorInfo.Create("CLIPBOARD_EMPTY_CONTENT", "Clipboard content is empty.");

            string line = IosClipboardSampleResult.FormatFailure(context, error);

            Assert.AreEqual("#7 [copy.plainText] NG code=CLIPBOARD_EMPTY_CONTENT message=Clipboard content is empty.", line);
        }

        [Test]
        public void FormatFailure_UsesDashesForCancellation()
        {
            var context = new IosClipboardSampleResultContext(9, "load.image");
            var error = IosClipboardErrorInfo.Create(CancelledCode, "The clipboard load was cancelled.");

            StringAssert.StartsWith("#9 [load.image] --", IosClipboardSampleResult.FormatFailure(context, error));
        }

        [Test]
        public void FormatFailure_AppendsDetailsOnlyWhenPresent()
        {
            var context = new IosClipboardSampleResultContext(4, "read");
            var withoutDetails = IosClipboardErrorInfo.Create("CLIPBOARD_UNAVAILABLE", "Unavailable.");
            var withDetails = IosClipboardErrorInfo.Create("CLIPBOARD_UNAVAILABLE", "Unavailable.", "NSCocoaErrorDomain", 260);

            StringAssert.DoesNotContain("details=", IosClipboardSampleResult.FormatFailure(context, withoutDetails));
            StringAssert.EndsWith("details=NSCocoaErrorDomain:260", IosClipboardSampleResult.FormatFailure(context, withDetails));
        }

        /// <summary>
        /// The reason the sample carries a per-call context at all: different operations overlap, so
        /// a completion must be labelled with its own call even when it finishes out of order.
        /// </summary>
        [Test]
        public void Contexts_StayCorrelated_WhenCompletionsArriveOutOfOrder()
        {
            var first = new IosClipboardSampleResultContext(1, "load.image");
            var second = new IosClipboardSampleResultContext(2, "load.cancel");

            string secondLine = IosClipboardSampleResult.FormatSuccess(second, string.Empty);
            string firstLine = IosClipboardSampleResult.FormatFailure(
                first, IosClipboardErrorInfo.Create(CancelledCode, "The clipboard load was cancelled."));

            Assert.AreEqual("#2 [load.cancel] OK", secondLine);
            StringAssert.StartsWith("#1 [load.image] --", firstLine);
        }

        // ── File cleanup display ─────────────────────────────────────────────

        [Test]
        public void FormatFileOutcome_CoversEveryPath()
        {
            Assert.AreEqual("fileSize=64 cleanup=ok", IosClipboardSampleResult.FormatFileOutcome(64, true));
            Assert.AreEqual("fileSize=64 cleanup=failed", IosClipboardSampleResult.FormatFileOutcome(64, false));
            Assert.AreEqual("fileSize=-1 cleanup=ok", IosClipboardSampleResult.FormatFileOutcome(-1, true));
            Assert.AreEqual("fileSize=-1 cleanup=failed", IosClipboardSampleResult.FormatFileOutcome(-1, false));
        }

        // ── Scope and status display ─────────────────────────────────────────

        [Test]
        public void FormatScopeLabel_ShowsLengthInsteadOfName()
        {
            Assert.AreEqual("general", IosClipboardSampleResult.FormatScopeLabel(IosPasteboardScope.General));
            Assert.AreEqual("named(len=6)", IosClipboardSampleResult.FormatScopeLabel(IosPasteboardScope.Named("sample")));
            Assert.AreEqual("unique(len=4)", IosClipboardSampleResult.FormatScopeLabel(IosPasteboardScope.Unique("abcd")));
            Assert.AreEqual("(none)", IosClipboardSampleResult.FormatScopeLabel(null));
        }

        [Test]
        public void FormatStatus_ShowsObservedScopeOnlyWhileObserving()
        {
            string idle = IosClipboardSampleResult.FormatStatus(
                IosPasteboardScope.General, null, isObserving: false, controlPending: false, eventCount: 0);
            string observing = IosClipboardSampleResult.FormatStatus(
                IosPasteboardScope.General, IosPasteboardScope.Named("sample"),
                isObserving: true, controlPending: false, eventCount: 3);

            Assert.AreEqual("Scope: general | Observing: off | Events: 0", idle);
            Assert.AreEqual("Scope: general (observing named(len=6)) | Observing: on | Events: 3", observing);
        }

        [Test]
        public void FormatObservingState_DistinguishesPendingTransitions()
        {
            Assert.AreEqual("off", IosClipboardSampleResult.FormatObservingState(false, false));
            Assert.AreEqual("starting", IosClipboardSampleResult.FormatObservingState(false, true));
            Assert.AreEqual("on", IosClipboardSampleResult.FormatObservingState(true, false));
            Assert.AreEqual("on (pending)", IosClipboardSampleResult.FormatObservingState(true, true));
        }

        [Test]
        public void FormatPatternKinds_NamesTheDetectedKinds()
        {
            var patterns = new[]
            {
                IosClipboardDetectionPattern.Number,
                IosClipboardDetectionPattern.ProbableWebSearch
            };

            Assert.AreEqual("Number,ProbableWebSearch", IosClipboardSampleResult.FormatPatternKinds(patterns));
            Assert.AreEqual("-", IosClipboardSampleResult.FormatPatternKinds(System.Array.Empty<IosClipboardDetectionPattern>()));
        }

        // ── Observation ownership ────────────────────────────────────────────

        [Test]
        public void BeginStart_WhileAnotherControlIsPending_ReturnsNonOwningToken()
        {
            var state = new IosClipboardSampleObservationState();

            int first = state.BeginStart();
            int second = state.BeginStart();

            Assert.AreNotEqual(IosClipboardSampleObservationState.NonOwningToken, first);
            Assert.AreEqual(IosClipboardSampleObservationState.NonOwningToken, second);
        }

        /// <summary>
        /// The busy demo's rejected second start must not clear ControlPending while the first start
        /// is still running natively.
        /// </summary>
        [Test]
        public void RejectedSecondStart_DoesNotReleaseTheFirstStart()
        {
            var state = new IosClipboardSampleObservationState();
            int owner = state.BeginStart();

            bool owned = state.CompleteStart(IosClipboardSampleObservationState.NonOwningToken, isSuccess: false);

            Assert.IsFalse(owned);
            Assert.IsTrue(state.ControlPending);
            Assert.IsFalse(state.IsObserving);

            Assert.IsTrue(state.CompleteStart(owner, isSuccess: true));
            Assert.IsTrue(state.IsObserving);
            Assert.IsFalse(state.ControlPending);
        }

        [Test]
        public void StaleOwner_CannotChangeState()
        {
            var state = new IosClipboardSampleObservationState();
            int first = state.BeginStart();
            state.CompleteStart(first, isSuccess: true);
            int second = state.BeginStop();

            Assert.IsFalse(state.CompleteStop(first, isSuccess: true), "A stale owner must not own the state.");
            Assert.IsTrue(state.IsObserving);
            Assert.IsTrue(state.CompleteStop(second, isSuccess: true));
            Assert.IsFalse(state.IsObserving);
        }

        // ── Teardown ─────────────────────────────────────────────────────────

        [Test]
        public void LeavingDuringPendingStart_DefersTheStopToThatStart()
        {
            var state = new IosClipboardSampleObservationState();
            int startOwner = state.BeginStart();

            state.RequestStop();
            Assert.IsTrue(state.StopRequestedAfterStart);
            Assert.IsFalse(state.ShouldIssueStopNow(), "No stop may be issued while the start is pending.");

            Assert.IsTrue(state.CompleteStart(startOwner, isSuccess: true));
            Assert.IsTrue(state.ShouldIssueStopNow());

            int stopOwner = state.BeginStop();
            Assert.IsTrue(state.CompleteStop(stopOwner, isSuccess: true));
            Assert.IsFalse(state.IsObserving);
            Assert.IsFalse(state.ControlPending);
            Assert.IsFalse(state.StopRequestedAfterStart);
        }

        [Test]
        public void BusyStartThenLeave_StillStopsTheObservationTheFirstStartCreates()
        {
            var state = new IosClipboardSampleObservationState();
            int owner = state.BeginStart();
            state.CompleteStart(IosClipboardSampleObservationState.NonOwningToken, isSuccess: false);

            state.RequestStop();
            state.CompleteStart(owner, isSuccess: true);

            Assert.IsTrue(state.StopRequestedAfterStart);
            Assert.IsTrue(state.ShouldIssueStopNow());
        }

        /// <summary>
        /// Mirrors the controller's deferred-stop rule: issue on an owned successful start only, and
        /// never again from the stop callback.
        /// </summary>
        [Test]
        public void DeferredStop_IsIssuedExactlyOnce_EvenWhenTheStopFails()
        {
            var state = new IosClipboardSampleObservationState();
            int startOwner = state.BeginStart();
            state.RequestStop();

            int issued = 0;
            int stopOwner = IosClipboardSampleObservationState.NonOwningToken;

            bool owned = state.CompleteStart(startOwner, isSuccess: true);
            if (owned && state.StopRequestedAfterStart && state.ShouldIssueStopNow())
            {
                issued++;
                stopOwner = state.BeginStop();
            }

            bool stopOwned = state.CompleteStop(stopOwner, isSuccess: false);
            if (stopOwned && state.StopRequestedAfterStart && state.ShouldIssueStopNow())
            {
                issued++;
            }

            Assert.AreEqual(1, issued);
            Assert.IsFalse(state.StopRequestedAfterStart);
        }

        [Test]
        public void FailedStartAfterLeaving_IssuesNoStopAtAll()
        {
            var state = new IosClipboardSampleObservationState();
            int owner = state.BeginStart();
            state.RequestStop();

            Assert.IsTrue(state.CompleteStart(owner, isSuccess: false));

            Assert.IsFalse(state.IsObserving);
            Assert.IsFalse(state.StopRequestedAfterStart);
            Assert.IsFalse(state.ShouldIssueStopNow(), "Native stops the old observation before it fails, so nothing is left to stop.");
        }

        [Test]
        public void DuplicateStopRequest_WhileStopPending_IssuesNoSecondStop()
        {
            var state = new IosClipboardSampleObservationState();
            int startOwner = state.BeginStart();
            state.CompleteStart(startOwner, isSuccess: true);
            state.BeginStop();

            state.RequestStop();

            Assert.IsFalse(state.ShouldIssueStopNow());
        }

        // ── Restart ──────────────────────────────────────────────────────────

        [Test]
        public void Restart_KeepsObservingWhileTheReplacementIsPending()
        {
            var state = new IosClipboardSampleObservationState();
            int first = state.BeginStart();
            state.CompleteStart(first, isSuccess: true);

            int restart = state.BeginStart();
            Assert.AreNotEqual(IosClipboardSampleObservationState.NonOwningToken, restart);
            Assert.IsTrue(state.IsObserving);
            Assert.IsTrue(state.ControlPending);

            Assert.IsTrue(state.CompleteStart(restart, isSuccess: true));
            Assert.IsTrue(state.IsObserving);
            Assert.IsFalse(state.ControlPending);
        }

        /// <summary>
        /// Native stops the previous observation before it resolves the new scope, so a failed
        /// replacement leaves nothing subscribed.
        /// </summary>
        [Test]
        public void FailedRestart_FallsBackToNotObserving()
        {
            var state = new IosClipboardSampleObservationState();
            int first = state.BeginStart();
            state.CompleteStart(first, isSuccess: true);
            int restart = state.BeginStart();

            Assert.IsTrue(state.CompleteStart(restart, isSuccess: false));

            Assert.IsFalse(state.IsObserving);
            Assert.IsFalse(state.ControlPending);
            Assert.IsTrue(state.CanStartObserving);
            Assert.IsFalse(state.CanRestartObserving);
            Assert.IsFalse(state.CanStopObserving);
        }

        [Test]
        public void FailedStop_LeavesTheScreenAbleToRetry()
        {
            var state = new IosClipboardSampleObservationState();
            int start = state.BeginStart();
            state.CompleteStart(start, isSuccess: true);
            int stop = state.BeginStop();

            Assert.IsTrue(state.CompleteStop(stop, isSuccess: false));

            Assert.IsTrue(state.IsObserving);
            Assert.IsFalse(state.ControlPending);
            Assert.IsTrue(state.CanStopObserving);
        }

        // ── Enabled contract ─────────────────────────────────────────────────

        [Test]
        public void EnabledContract_MatchesEveryObservationState()
        {
            var state = new IosClipboardSampleObservationState();
            AssertEnabled(state, canStart: true, canRestart: false, canStop: false, canChangeScope: true);

            int start = state.BeginStart();
            AssertEnabled(state, canStart: false, canRestart: false, canStop: false, canChangeScope: false);

            state.CompleteStart(start, isSuccess: true);
            AssertEnabled(state, canStart: false, canRestart: true, canStop: true, canChangeScope: false);

            int restart = state.BeginStart();
            AssertEnabled(state, canStart: false, canRestart: false, canStop: false, canChangeScope: false);

            state.CompleteStart(restart, isSuccess: true);
            int stop = state.BeginStop();
            AssertEnabled(state, canStart: false, canRestart: false, canStop: false, canChangeScope: false);

            state.CompleteStop(stop, isSuccess: true);
            AssertEnabled(state, canStart: true, canRestart: false, canStop: false, canChangeScope: true);
        }

        private static void AssertEnabled(
            IosClipboardSampleObservationState state, bool canStart, bool canRestart, bool canStop, bool canChangeScope)
        {
            Assert.AreEqual(canStart, state.CanStartObserving, nameof(state.CanStartObserving));
            Assert.AreEqual(canRestart, state.CanRestartObserving, nameof(state.CanRestartObserving));
            Assert.AreEqual(canStop, state.CanStopObserving, nameof(state.CanStopObserving));
            Assert.AreEqual(canChangeScope, state.CanChangeScope, nameof(state.CanChangeScope));
        }

        // ── Start requests ───────────────────────────────────────────────────

        [Test]
        public void StartRequest_TakesOwnershipAndTargetsTheActiveScope()
        {
            var state = new IosClipboardSampleObservationState();
            var active = IosPasteboardScope.Named("sample");

            var request = IosClipboardSampleObservationRequests.Start(ref state, active);

            Assert.AreEqual(IosClipboardSampleObservationRequests.StartMarker, request.Marker);
            Assert.AreNotEqual(IosClipboardSampleObservationState.NonOwningToken, request.Owner);
            Assert.AreSame(active, request.TargetScope);
            Assert.IsTrue(state.ControlPending);
        }

        [Test]
        public void RestartRequest_RecapturesTheActiveScope()
        {
            var state = new IosClipboardSampleObservationState();
            var first = IosPasteboardScope.General;
            var startRequest = IosClipboardSampleObservationRequests.Start(ref state, first);
            state.CompleteStart(startRequest.Owner, isSuccess: true);

            var second = IosPasteboardScope.Named("sample");
            var restart = IosClipboardSampleObservationRequests.Restart(ref state, second);

            Assert.AreEqual(IosClipboardSampleObservationRequests.RestartMarker, restart.Marker);
            Assert.AreSame(second, restart.TargetScope);
            Assert.AreNotEqual(IosClipboardSampleObservationState.NonOwningToken, restart.Owner);
        }

        [Test]
        public void BusyPair_OnlyTheFirstCallOwnsTheState()
        {
            var state = new IosClipboardSampleObservationState();
            var active = IosPasteboardScope.General;

            var (first, second) = IosClipboardSampleObservationRequests.BusyPair(ref state, active);

            Assert.AreNotEqual(IosClipboardSampleObservationState.NonOwningToken, first.Owner);
            Assert.AreEqual(IosClipboardSampleObservationState.NonOwningToken, second.Owner);
            Assert.AreSame(active, first.TargetScope);
            Assert.AreSame(active, second.TargetScope, "Both calls must race for the same scope.");
        }

        /// <summary>
        /// The missing-named error button must aim at a pasteboard that was never created. Using the
        /// active scope - general while not observing - would make the button succeed instead.
        /// </summary>
        [Test]
        public void MissingNamedRequest_IgnoresTheActiveScopeAndUsesAFreshName()
        {
            var state = new IosClipboardSampleObservationState();

            var first = IosClipboardSampleObservationRequests.MissingNamed(ref state);
            state.CompleteStart(first.Owner, isSuccess: false);
            var second = IosClipboardSampleObservationRequests.MissingNamed(ref state);

            Assert.AreEqual(IosClipboardSampleObservationRequests.MissingNamedMarker, first.Marker);
            Assert.AreEqual(IosPasteboardScopeKind.Named, first.TargetScope.Kind);
            Assert.AreNotSame(IosPasteboardScope.General, first.TargetScope);
            StringAssert.StartsWith(
                IosClipboardSampleObservationRequests.MissingScopeNamePrefix, first.TargetScope.Name);
            Assert.AreNotEqual(
                first.TargetScope.Name, second.TargetScope.Name,
                "A new name per click keeps a previous run from making it resolvable.");
            Assert.AreNotEqual(IosClipboardSampleObservationState.NonOwningToken, first.Owner);
        }

        // ── Log redaction ────────────────────────────────────────────────────

        /// <summary>
        /// File exceptions carry the failing path in their message, and the sample must not disclose
        /// the temporary path the native layer returned.
        /// </summary>
        [Test]
        public void DescribeException_DropsThePathBearingMessage()
        {
            var exception = new System.IO.FileNotFoundException(
                "Could not find file '/private/var/mobile/tmp/clipboard-request-1/payload.dat'.");

            string described = IosClipboardSampleResult.DescribeException(exception);

            Assert.AreEqual(nameof(System.IO.FileNotFoundException), described);
            StringAssert.DoesNotContain("/", described);
        }
    }
}
#endif
