#nullable enable

#if UNITY_STANDALONE_OSX || UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using JonghyunKim.NativeToolkit.Runtime.Clipboard;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// EditMode tests for the macOS clipboard sample's pure logic: the observation state machine,
    /// the error redaction, and the two judgements the manual pass relies on.
    /// </summary>
    public sealed class MacClipboardSampleStateTests
    {
        // ── observation state machine ───────────────────────────────────────────

        [Test]
        public void CompleteStart_Success_MarksObserving()
        {
            var state = new MacClipboardSampleObservationState();
            int owner = state.BeginStart();

            Assert.IsTrue(state.ControlPending);
            Assert.IsTrue(state.CompleteStart(owner, true));
            Assert.IsTrue(state.IsObserving);
            Assert.IsFalse(state.ControlPending);
        }

        [Test]
        public void CompleteStart_FirstStartFails_StaysNotObserving()
        {
            var state = new MacClipboardSampleObservationState();
            int owner = state.BeginStart();

            state.CompleteStart(owner, false);

            Assert.IsFalse(state.IsObserving);
        }

        [Test]
        public void CompleteStart_RestartFails_KeepsObserving()
        {
            // The native monitor validates the interval and resolves the scope before touching the
            // running observation, so a rejected restart leaves the previous one alive. Reporting
            // "stopped" here would walk away from a live poller.
            var state = new MacClipboardSampleObservationState();
            state.CompleteStart(state.BeginStart(), true);

            int restart = state.BeginStart();
            state.CompleteStart(restart, false);

            Assert.IsTrue(state.IsObserving, "a failed restart must not clear the previous observation");
            Assert.IsFalse(state.ControlPending);
        }

        [Test]
        public void CompleteStop_Failure_KeepsObserving()
        {
            var state = new MacClipboardSampleObservationState();
            state.CompleteStart(state.BeginStart(), true);

            state.CompleteStop(state.BeginStop(), false);

            Assert.IsTrue(state.IsObserving, "native did not stop, so the screen must not claim it did");
        }

        [Test]
        public void CompleteStop_Success_ClearsObserving()
        {
            var state = new MacClipboardSampleObservationState();
            state.CompleteStart(state.BeginStart(), true);

            state.CompleteStop(state.BeginStop(), true);

            Assert.IsFalse(state.IsObserving);
        }

        [Test]
        public void BeginStart_WhileControlPending_ReturnsNonOwningToken()
        {
            var state = new MacClipboardSampleObservationState();
            state.BeginStart();

            Assert.AreEqual(MacClipboardSampleObservationState.NonOwningToken, state.BeginStart());
        }

        [Test]
        public void CompleteStart_WithNonOwningToken_ChangesNothing()
        {
            // The rejected second call receives its own 9001 callback. Acting on it would clear
            // the pending flag while the first start is still running natively.
            var state = new MacClipboardSampleObservationState();
            state.BeginStart();
            int rejected = state.BeginStart();

            Assert.IsFalse(state.CompleteStart(rejected, true));
            Assert.IsTrue(state.ControlPending);
            Assert.IsFalse(state.IsObserving);
        }

        [Test]
        public void CompleteStart_WithAStaleToken_ChangesNothing()
        {
            var state = new MacClipboardSampleObservationState();
            int first = state.BeginStart();
            state.CompleteStart(first, true);
            state.BeginStop();

            Assert.IsFalse(state.CompleteStart(first, false), "the first start no longer owns the state");
            Assert.IsTrue(state.IsObserving);
        }

        // ── deferred stop (manual check S-4) ────────────────────────────────────

        [Test]
        public void DeferredStop_AfterAFailedRestart_IsStillIssued()
        {
            // Leaving the screen while a restart is pending, then having that restart fail. macOS
            // keeps the old observation running, so a stop is still owed. Keying the decision off
            // the completion's success would skip it and leak the observation.
            var state = new MacClipboardSampleObservationState();
            state.CompleteStart(state.BeginStart(), true);

            int restart = state.BeginStart();
            state.RequestStop();
            Assert.IsTrue(state.StopRequestedAfterControl);

            state.CompleteStart(restart, false);

            Assert.IsTrue(state.TakeDeferredStop(), "the previous observation is still running");
        }

        [Test]
        public void DeferredStop_AfterASuccessfulRestart_IsIssued()
        {
            var state = new MacClipboardSampleObservationState();
            int start = state.BeginStart();
            state.RequestStop();
            state.CompleteStart(start, true);

            Assert.IsTrue(state.TakeDeferredStop());
        }

        [Test]
        public void DeferredStop_AfterAFailedFirstStart_IsNotIssued()
        {
            // Nothing was ever observed, so there is nothing to stop.
            var state = new MacClipboardSampleObservationState();
            int start = state.BeginStart();
            state.RequestStop();
            state.CompleteStart(start, false);

            Assert.IsFalse(state.TakeDeferredStop());
        }

        [Test]
        public void DeferredStop_IsConsumedOnce()
        {
            var state = new MacClipboardSampleObservationState();
            int start = state.BeginStart();
            state.RequestStop();
            state.CompleteStart(start, true);

            Assert.IsTrue(state.TakeDeferredStop());
            Assert.IsFalse(state.TakeDeferredStop(), "a second completion must not issue another stop");
        }

        [Test]
        public void DeferredStop_WhileStillPending_IsNotIssued()
        {
            var state = new MacClipboardSampleObservationState();
            state.CompleteStart(state.BeginStart(), true);
            state.BeginStart();
            state.RequestStop();

            Assert.IsFalse(state.TakeDeferredStop(), "the pending call has not completed yet");
        }

        [Test]
        public void RequestStop_WithNoPendingControl_DoesNotDefer()
        {
            var state = new MacClipboardSampleObservationState();
            state.CompleteStart(state.BeginStart(), true);

            state.RequestStop();

            Assert.IsFalse(state.StopRequestedAfterControl);
            Assert.IsTrue(state.ShouldIssueStopNow(), "the screen can stop it directly");
        }

        // ── error redaction ─────────────────────────────────────────────────────

        [Test]
        public void EveryDefinedErrorCode_HasAReasonToken()
        {
            // The native message is never shown, so an unmapped code loses all detail. Driving
            // this from the constants means a new code fails the test instead of going unnoticed.
            var unmapped = new List<int>();
            foreach (FieldInfo field in typeof(MacClipboardErrorCodes)
                         .GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (field.FieldType != typeof(int) || !field.IsLiteral) continue;
                int code = (int)field.GetRawConstantValue()!;
                if (!MacClipboardSampleResult.HasReason(code)) unmapped.Add(code);
            }

            CollectionAssert.IsEmpty(unmapped, "every MacClipboardErrorCodes constant needs a token");
        }

        [Test]
        public void FailureLine_CarriesTheCodeAndATokenButNoNativeMessage()
        {
            // 1507's native message embeds the pasteboard name, and the probe button exists to
            // reach exactly that code.
            var context = new MacClipboardSampleResultContext(4, "scope.probeRemoved", MacPasteboardScope.General);
            MacClipboardErrorInfo error = MacClipboardErrorInfo.Create(
                MacClipboardErrorCodes.PasteboardUnavailable,
                "Pasteboard is unavailable: com.example.secret-board.");

            string line = MacClipboardSampleResult.FormatFailure(context, error);

            StringAssert.Contains("code=1507", line);
            StringAssert.Contains("reason=pasteboardUnavailable", line);
            Assert.IsFalse(line.Contains("com.example.secret-board"), "the pasteboard name must not appear");
            Assert.IsFalse(line.Contains("unavailable:"), "the native message must not appear");
        }

        [Test]
        public void FailureLine_ForAStandardPasteboard_DoesNotNameIt()
        {
            var context = new MacClipboardSampleResultContext(1, "err.removeGeneral", MacPasteboardScope.General);
            MacClipboardErrorInfo error = MacClipboardErrorInfo.Create(
                MacClipboardErrorCodes.CannotReleaseStandardPasteboard,
                "Standard pasteboard cannot be released: Apple CFPasteboard general.");

            string line = MacClipboardSampleResult.FormatFailure(context, error);

            StringAssert.Contains("reason=standardPasteboard", line);
            Assert.IsFalse(line.Contains("Apple CFPasteboard"));
        }

        [Test]
        public void UnknownCode_FallsBackToUnmapped()
        {
            Assert.AreEqual(
                MacClipboardSampleResult.UnmappedReason, MacClipboardSampleResult.ReasonFor(4242));
        }

        [Test]
        public void DescribeException_DoesNotQuoteTheMessage()
        {
            string described = MacClipboardSampleResult.DescribeException(
                new ArgumentException("Pasteboard name must not be blank.", "name"));

            Assert.AreEqual("ArgumentException", described);
        }

        [Test]
        public void ScopeLabel_ShowsTheKindAndLengthButNotTheName()
        {
            string label = MacClipboardSampleResult.FormatScopeLabel(
                MacPasteboardScope.Named("com.example.board"));

            Assert.AreEqual("named(len=17)", label);
            Assert.IsFalse(label.Contains("com.example"));
        }

        // ── tracked codes ───────────────────────────────────────────────────────

        [Test]
        public void TrackedCodes_CoverEveryCodeTheManualItemsName()
        {
            // From v12 section 7.5: the ten items that assert a specific code.
            var expected = new[] { 1302, 1503, 1508, 1511, 1512, 1513, 1514, 1515, 1523, 9007 };
            CollectionAssert.AreEquivalent(expected, MacClipboardSampleResult.TrackedErrorCodes);
        }

        [Test]
        public void ReachedCodes_CountOnlyTheTrackedOnes()
        {
            // 9002 is reached constantly in the Editor but is not one of the ten, so it must be
            // listed without moving the counter.
            string line = MacClipboardSampleResult.FormatReachedCodes(new[] { 1508, 1512, 9002 });

            StringAssert.Contains("(2/10)", line);
            StringAssert.Contains("1508,1512,9002", line);
        }

        [Test]
        public void ReachedCodes_WhenNoneSeen_ShowsADash()
        {
            StringAssert.StartsWith("- (0/10)", MacClipboardSampleResult.FormatReachedCodes(Array.Empty<int>()));
        }

        // ── judgements the manual pass relies on ────────────────────────────────

        [Test]
        public void Derived_WhenThePasteboardChangedSinceOurWrite_IsNotApplicable()
        {
            // Another app copying between our Copy and our Read would otherwise be reported as a
            // derivation result for content we never wrote.
            Assert.AreEqual(
                MacClipboardSampleResult.NotApplicable,
                MacClipboardSampleResult.FormatDerived(fresh: false, singleWrittenItem: true, 1, 3));
        }

        [Test]
        public void Derived_WithMoreTypesRead_IsTrue()
        {
            Assert.AreEqual(
                "true", MacClipboardSampleResult.FormatDerived(true, true, writtenTypes: 1, readTypes: 3));
        }

        [Test]
        public void Derived_WithTheSameTypes_IsFalse()
        {
            Assert.AreEqual(
                "false", MacClipboardSampleResult.FormatDerived(true, true, writtenTypes: 2, readTypes: 2));
        }

        [Test]
        public void Derived_AfterAMultiItemWrite_IsNotApplicable()
        {
            Assert.AreEqual(
                MacClipboardSampleResult.NotApplicable,
                MacClipboardSampleResult.FormatDerived(true, singleWrittenItem: false, 1, 3));
        }

        [Test]
        public void RoundTrip_WhenThePasteboardChanged_IsNotApplicable()
        {
            Assert.AreEqual(
                MacClipboardSampleResult.NotApplicable,
                MacClipboardSampleResult.FormatRoundTrip(fresh: false, sameTypeFound: true, hashMatches: true));
        }

        [Test]
        public void RoundTrip_WithoutTheWrittenType_IsNotApplicableRatherThanDiffer()
        {
            // Nothing was compared, so calling it a failed round trip would be a false accusation.
            Assert.AreEqual(
                MacClipboardSampleResult.NotApplicable,
                MacClipboardSampleResult.FormatRoundTrip(true, sameTypeFound: false, hashMatches: false));
        }

        [Test]
        public void RoundTrip_MatchingHashes_IsMatch()
        {
            Assert.AreEqual("match", MacClipboardSampleResult.FormatRoundTrip(true, true, true));
        }

        [Test]
        public void RoundTrip_DifferingHashes_IsDiffer()
        {
            Assert.AreEqual("differ", MacClipboardSampleResult.FormatRoundTrip(true, true, false));
        }

        // ── deferred stop, continued ────────────────────────────────────────────

        [Test]
        public void DeferredStop_IsNotReissuedAfterAFailedStop()
        {
            // Recorded rather than fixed: the request is consumed either way. The screen is gone
            // by then, so there is no one left to retry, and keeping the flag set would make a
            // later completion stop an observation that no longer exists.
            var state = new MacClipboardSampleObservationState();
            state.CompleteStart(state.BeginStart(), true);

            int stop = state.BeginStop();
            state.RequestStop();
            state.CompleteStop(stop, false);

            Assert.IsTrue(state.IsObserving, "the native stop failed, so it is still observing");
            Assert.IsTrue(state.TakeDeferredStop(), "the deferred stop is issued once");
            Assert.IsFalse(state.TakeDeferredStop(), "and not retried");
        }

        // ── freshness (manual checks 4 and 25) ──────────────────────────────────

        [Test]
        public void IsFresh_SameScopeAndChangeCount_IsTrue()
        {
            Assert.IsTrue(MacClipboardSampleResult.IsFresh(
                MacPasteboardScope.General, 5, MacPasteboardScope.General, 5));
        }

        [Test]
        public void IsFresh_DifferentPasteboardWithTheSameChangeCount_IsFalse()
        {
            // The regression this exists for: a change count is only unique within one pasteboard,
            // so two of them can carry the same number and judge another app's content as ours.
            Assert.IsFalse(MacClipboardSampleResult.IsFresh(
                MacPasteboardScope.General, 5, MacPasteboardScope.Named("board"), 5));
        }

        [Test]
        public void IsFresh_SameKindDifferentName_IsFalse()
        {
            Assert.IsFalse(MacClipboardSampleResult.IsFresh(
                MacPasteboardScope.Named("a"), 5, MacPasteboardScope.Named("b"), 5));
        }

        [Test]
        public void IsFresh_SameScopeDifferentChangeCount_IsFalse()
        {
            Assert.IsFalse(MacClipboardSampleResult.IsFresh(
                MacPasteboardScope.General, 5, MacPasteboardScope.General, 6));
        }

        [Test]
        public void IsFresh_WithoutAPriorWrite_IsFalse()
        {
            Assert.IsFalse(MacClipboardSampleResult.IsFresh(null, null, MacPasteboardScope.General, 5));
            Assert.IsFalse(MacClipboardSampleResult.IsFresh(MacPasteboardScope.General, null, MacPasteboardScope.General, 5));
        }

        // ── registration counts (manual check 16) ───────────────────────────────

        [Test]
        public void RegistrationCounts_ShowAReplacedRegistrationSittingAtZero()
        {
            // Showing only the registration that fired cannot tell "correctly replaced, so zero"
            // from "the counter never worked".
            string line = MacClipboardSampleResult.FormatRegistrationCounts(new[]
            {
                new KeyValuePair<string, int>("observe.start#1", 0),
                new KeyValuePair<string, int>("observe.restart#2", 1),
            });

            Assert.AreEqual("observe.start#1=0 observe.restart#2=1", line);
        }

        [Test]
        public void RegistrationCounts_WhenNoneRegistered_ShowsADash()
        {
            Assert.AreEqual(
                "-", MacClipboardSampleResult.FormatRegistrationCounts(Array.Empty<KeyValuePair<string, int>>()));
        }

        [Test]
        public void Status_IncludesTheRegistrationCounts()
        {
            string status = MacClipboardSampleResult.FormatStatus(
                MacPasteboardScope.General, null, true, false, 1, Array.Empty<int>(),
                new[] { new KeyValuePair<string, int>("observe.start#1", 0) });

            StringAssert.Contains("Registrations: observe.start#1=0", status);
        }

        // ── status line ─────────────────────────────────────────────────────────

        [Test]
        public void Status_ShowsTheObservedScopeWhenItDiffersFromTheActiveOne()
        {
            string status = MacClipboardSampleResult.FormatStatus(
                MacPasteboardScope.General,
                MacPasteboardScope.Named("board"),
                isObserving: true,
                controlPending: false,
                eventCount: 2,
                reachedCodes: Array.Empty<int>(),
                registrationCounts: Array.Empty<KeyValuePair<string, int>>());

            StringAssert.Contains("general (observing named(len=5))", status);
            StringAssert.Contains("Observing: on", status);
            StringAssert.Contains("Events: 2", status);
        }

        [Test]
        public void Status_WhileAControlCallIsPending_SaysSo()
        {
            Assert.AreEqual(
                MacClipboardSampleResult.StatusObservingPending,
                MacClipboardSampleResult.FormatObservingState(isObserving: true, controlPending: true));
            Assert.AreEqual(
                MacClipboardSampleResult.StatusObservingStarting,
                MacClipboardSampleResult.FormatObservingState(isObserving: false, controlPending: true));
        }
    }
}
#endif
