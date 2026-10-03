#nullable enable

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using JonghyunKim.NativeToolkit.Runtime.Windows.Clipboard;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// EditMode tests for the parts of the Windows clipboard manager that hold no native state:
    /// the delivery order, the shutdown classification, and the lifecycle state machine.
    /// <para>
    /// No test creates a manager instance. Awake touches the dispatcher and the native boundary,
    /// which belongs to PlayMode; everything verified here is reachable through internal statics.
    /// </para>
    /// </summary>
    public sealed class WindowsClipboardManagerDispatchTests
    {
        private const string Op = "copyPlainText";

        [TearDown]
        public void TearDown() => WindowsClipboardManager.ResetForTests();

        // ── InvokeInOrder ────────────────────────────────────────────────────────

        [Test]
        public void InvokeInOrder_CallsTheCommonEventBeforeThePerCallCallback()
        {
            var order = new List<string>();

            WindowsClipboardManager.InvokeInOrder(
                WindowsClipboardResult.Success(Op),
                _ => order.Add("common"),
                _ => order.Add("perCall"));

            Assert.AreEqual(new[] { "common", "perCall" }, order.ToArray());
        }

        [Test]
        public void InvokeInOrder_DeliversTheSameResultToBoth()
        {
            WindowsClipboardResult common = default;
            WindowsClipboardResult perCall = default;
            WindowsClipboardResult expected =
                WindowsClipboardResult.Failure(Op, WindowsClipboardErrorCode.Busy);

            WindowsClipboardManager.InvokeInOrder(expected, r => common = r, r => perCall = r);

            Assert.AreEqual(expected.ErrorCode, common.ErrorCode);
            Assert.AreEqual(expected.ErrorCode, perCall.ErrorCode);
        }

        [Test]
        public void InvokeInOrder_AThrowingCommonSubscriberDoesNotStopThePerCallCallback()
        {
            bool perCallRan = false;

            WindowsClipboardManager.InvokeInOrder<WindowsClipboardResult>(
                WindowsClipboardResult.Success(Op),
                _ => throw new InvalidOperationException("subscriber"),
                _ => perCallRan = true);

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("a subscriber threw"));
            Assert.IsTrue(perCallRan, "one bad subscriber must not swallow the other delivery");
        }

        [Test]
        public void InvokeInOrder_AThrowingPerCallCallbackIsContained()
        {
            WindowsClipboardManager.InvokeInOrder<WindowsClipboardResult>(
                WindowsClipboardResult.Success(Op),
                null,
                _ => throw new InvalidOperationException("callback"));

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("per-call callback threw"));
        }

        [Test]
        public void InvokeInOrder_BothNullIsHarmless()
        {
            Assert.DoesNotThrow(() => WindowsClipboardManager.InvokeInOrder(
                WindowsClipboardResult.Success(Op), null, null));
        }

        // ── Shutdown classification ──────────────────────────────────────────────

        [Test]
        public void ClassifyShutdown_CompletedWins()
        {
            Assert.AreEqual(WindowsClipboardShutdownProgress.Completed,
                WindowsClipboardManager.ClassifyShutdown(true, WindowsClipboardErrorCode.None));
        }

        [TestCase(WindowsClipboardErrorCode.None)]
        [TestCase(WindowsClipboardErrorCode.Busy)]
        [TestCase(WindowsClipboardErrorCode.MonitorRegisterFailed)]
        [TestCase(WindowsClipboardErrorCode.Canceled)]
        [TestCase(WindowsClipboardErrorCode.PartialState)]
        public void ClassifyShutdown_TheseCodesMeanNotYet(WindowsClipboardErrorCode code)
        {
            // The native manager reports why it is still busy through the same out parameter it
            // uses for failures, so treating any non-zero code as failure would abandon a shutdown
            // that only needed another pumped frame.
            Assert.AreEqual(WindowsClipboardShutdownProgress.NotYet,
                WindowsClipboardManager.ClassifyShutdown(false, code));
        }

        [Test]
        public void ClassifyShutdown_WrongThreadIsTerminal()
        {
            // Retrying from the wrong thread can never complete, so the drain must stop instead of
            // burning its whole budget.
            Assert.AreEqual(WindowsClipboardShutdownProgress.Terminal,
                WindowsClipboardManager.ClassifyShutdown(false, WindowsClipboardErrorCode.WrongThread));
        }

        [TestCase(WindowsClipboardErrorCode.InvalidParameter)]
        [TestCase(WindowsClipboardErrorCode.Unknown)]
        [TestCase(WindowsClipboardErrorCode.BridgeUnavailable)]
        [TestCase(WindowsClipboardErrorCode.PlatformUnavailable)]
        public void ClassifyShutdown_OtherCodesAreTerminal(WindowsClipboardErrorCode code)
        {
            Assert.AreEqual(WindowsClipboardShutdownProgress.Terminal,
                WindowsClipboardManager.ClassifyShutdown(false, code));
        }

        // ── Deferred rendering (design v8 2.8, and v12 J-8 for the one-step render) ──

        private static Dictionary<string, Func<byte[]>> Provider(string format, byte[] payload) =>
            new() { [format] = () => payload };

        [Test]
        public void Reservation_OnSuccess_ReplacesTheLiveProviders()
        {
            WindowsClipboardManager.SetRenderProvidersForTests(Provider("OLD", new byte[] { 1 }));

            WindowsClipboardManager.ApplyReservationOutcomeForTests(
                WindowsClipboardErrorCode.None, Provider("NEW", new byte[] { 2 }));

            CollectionAssert.AreEquivalent(
                new[] { "NEW" }, WindowsClipboardManager.RenderProviderNamesForTests);
        }

        [TestCase(WindowsClipboardErrorCode.Busy)]
        [TestCase(WindowsClipboardErrorCode.InvalidParameter)]
        [TestCase(WindowsClipboardErrorCode.Unknown)]
        [TestCase(WindowsClipboardErrorCode.OutOfMemory)]
        public void Reservation_OnFailure_KeepsThePreviousProvidersUntouched(
            WindowsClipboardErrorCode code)
        {
            // The native side may still hold the previous renderers on these paths, and Unknown
            // covers two failure sites that cannot be told apart, so the old generation has to
            // survive or a reserved format would be dropped without a word.
            WindowsClipboardManager.SetRenderProvidersForTests(Provider("OLD", new byte[] { 1 }));

            WindowsClipboardManager.ApplyReservationOutcomeForTests(
                code, Provider("NEW", new byte[] { 2 }));

            CollectionAssert.AreEquivalent(
                new[] { "OLD" }, WindowsClipboardManager.RenderProviderNamesForTests);
        }

        [Test]
        public void Reservation_OnPartialState_KeepsBothGenerations()
        {
            // The native side kept the new table here, but the formats it no longer names may still
            // be asked for.
            WindowsClipboardManager.SetRenderProvidersForTests(Provider("OLD", new byte[] { 1 }));

            WindowsClipboardManager.ApplyReservationOutcomeForTests(
                WindowsClipboardErrorCode.PartialState, Provider("NEW", new byte[] { 2 }));

            CollectionAssert.AreEquivalent(
                new[] { "OLD", "NEW" }, WindowsClipboardManager.RenderProviderNamesForTests);
        }

        [Test]
        public void Render_HandsTheProvidersBytesToTheTargetOnce()
        {
            int providerCalls = 0;
            var providers = new Dictionary<string, Func<byte[]>>
            {
                ["F"] = () => { providerCalls++; return new byte[] { 7, 8 }; }
            };
            WindowsClipboardManager.SetRenderProvidersForTests(providers);

            var handed = new List<byte[]>();
            WindowsClipboardErrorCode code = WindowsClipboardManager.RenderForTests("F", bytes =>
            {
                handed.Add(bytes);
                return WindowsClipboardErrorCode.None;
            });

            Assert.AreEqual(WindowsClipboardErrorCode.None, code);
            Assert.AreEqual(1, providerCalls, "the provider runs once per render, as in 1.x");
            Assert.AreEqual(1, handed.Count);
            CollectionAssert.AreEqual(new byte[] { 7, 8 }, handed[0]);
        }

        [Test]
        public void Render_AZeroLengthPayloadIsReportedRatherThanPlaced()
        {
            WindowsClipboardManager.SetRenderProvidersForTests(Provider("F", new byte[0]));
            bool reached = false;

            WindowsClipboardErrorCode code = WindowsClipboardManager.RenderForTests("F", _ =>
            {
                reached = true;
                return WindowsClipboardErrorCode.None;
            });

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("F: produced no bytes"));
            Assert.AreEqual(WindowsClipboardErrorCode.InvalidData, code);
            Assert.IsFalse(reached, "nothing reaches the target");
        }

        [Test]
        public void Render_AnUnknownFormatFails()
        {
            WindowsClipboardManager.SetRenderProvidersForTests(new Dictionary<string, Func<byte[]>>());

            WindowsClipboardErrorCode code = WindowsClipboardManager.RenderForTests("MISSING", _ => WindowsClipboardErrorCode.None);

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("MISSING: no provider"));
            Assert.AreEqual(WindowsClipboardErrorCode.InvalidParameter, code);
        }

        [Test]
        public void Render_AThrowingProviderIsContained()
        {
            var providers = new Dictionary<string, Func<byte[]>>
            {
                ["F"] = () => throw new InvalidOperationException("boom")
            };
            WindowsClipboardManager.SetRenderProvidersForTests(providers);

            WindowsClipboardErrorCode code = WindowsClipboardManager.RenderForTests("F", _ => WindowsClipboardErrorCode.None);

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("InvalidOperationException"));
            Assert.AreEqual(WindowsClipboardErrorCode.Unknown, code);
        }

        [TestCase(WindowsClipboardErrorCode.InvalidParameter)]
        [TestCase(WindowsClipboardErrorCode.OutOfMemory)]
        public void Render_ATargetFailureIsReturnedAsItIs(WindowsClipboardErrorCode targetCode)
        {
            // Not overwritten with None: the native side logs what the render reports.
            WindowsClipboardManager.SetRenderProvidersForTests(Provider("F", new byte[] { 1 }));

            WindowsClipboardErrorCode code = WindowsClipboardManager.RenderForTests("F", _ => targetCode);

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("render_target_set"));
            Assert.AreEqual(targetCode, code);
        }

        // ── Reads (design v12 4.3) ───────────────────────────────────────────────
        // Driven through a stand-in for the native call, guard included.

        private static Func<(WindowsClipboardErrorCode, string?)> Answers(WindowsClipboardErrorCode code, string? text) =>
            () => (code, text);

        [Test]
        public void ReadText_AnEmptyPasteIsAnEmptyStringAndNotAnEmptyClipboard()
        {
            // Something was copied and can be pasted back. Reporting it as empty would fold that
            // together with "there is no text at all", and the caller cannot tell them apart again.
            WindowsClipboardManager.PlatformAvailableForTests = true;
            WindowsClipboardManager.SetStateForTests(WindowsClipboardManagerState.Running);

            WindowsClipboardTextResult result = WindowsClipboardManager.ReadTextForTests(
                WindowsClipboardManager.OperationPastePlainText, Answers(WindowsClipboardErrorCode.None, string.Empty));

            Assert.IsTrue(result.IsSuccess);
            Assert.IsFalse(result.IsEmpty);
            Assert.AreEqual(string.Empty, result.Text);
        }

        [Test]
        public void ReadText_GetPreferredFormat_TreatsAnEmptyStringAsNothingMatched()
        {
            // The odd one out: this call answers with an empty string when none of the candidates
            // were present, and never reports the native Empty code.
            WindowsClipboardManager.PlatformAvailableForTests = true;
            WindowsClipboardManager.SetStateForTests(WindowsClipboardManagerState.Running);

            WindowsClipboardTextResult result = WindowsClipboardManager.ReadTextForTests(
                WindowsClipboardManager.OperationGetPreferredFormat, Answers(WindowsClipboardErrorCode.None, string.Empty));

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.IsEmpty);
        }

        [Test]
        public void ReadText_APasteWithContentIsUnaffected()
        {
            WindowsClipboardManager.PlatformAvailableForTests = true;
            WindowsClipboardManager.SetStateForTests(WindowsClipboardManagerState.Running);

            WindowsClipboardTextResult result = WindowsClipboardManager.ReadTextForTests(
                WindowsClipboardManager.OperationPastePlainText, Answers(WindowsClipboardErrorCode.None, "kept"));

            Assert.IsTrue(result.IsSuccess);
            Assert.IsFalse(result.IsEmpty);
            Assert.AreEqual("kept", result.Text);
        }

        [TestCase(WindowsClipboardErrorCode.Empty)]
        [TestCase(WindowsClipboardErrorCode.FormatUnavailable)]
        public void ReadText_EmptyAndFormatUnavailable_AreAnEmptyClipboard(WindowsClipboardErrorCode code)
        {
            WindowsClipboardManager.PlatformAvailableForTests = true;
            WindowsClipboardManager.SetStateForTests(WindowsClipboardManagerState.Running);

            WindowsClipboardTextResult result = WindowsClipboardManager.ReadTextForTests(
                WindowsClipboardManager.OperationPastePlainText, Answers(code, null));

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.IsEmpty);
        }

        [TestCase(WindowsClipboardErrorCode.NotInitialized)]
        [TestCase(WindowsClipboardErrorCode.Busy)]
        [TestCase(WindowsClipboardErrorCode.InvalidData)]
        public void ReadText_AFailureIsNeverNormalizedToEmpty(WindowsClipboardErrorCode code)
        {
            // The native read fails without a value for a lease failure too, so trusting the value
            // instead of the code would turn a real failure into "the clipboard is empty".
            WindowsClipboardManager.PlatformAvailableForTests = true;
            WindowsClipboardManager.SetStateForTests(WindowsClipboardManagerState.Running);

            WindowsClipboardTextResult result = WindowsClipboardManager.ReadTextForTests(
                WindowsClipboardManager.OperationPastePlainText, Answers(code, null));

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("read failed"));
            Assert.IsFalse(result.IsSuccess);
            Assert.IsFalse(result.IsEmpty, "a rejected read is not an empty clipboard");
            Assert.AreEqual(code, result.ErrorCode);
        }

        [Test]
        public void ReadText_CutsAtTheFirstNul()
        {
            // As 1.x's PtrToStringUni read it; the C ABI hands back a length, so a NUL would survive.
            WindowsClipboardManager.PlatformAvailableForTests = true;
            WindowsClipboardManager.SetStateForTests(WindowsClipboardManagerState.Running);

            WindowsClipboardTextResult result = WindowsClipboardManager.ReadTextForTests(
                WindowsClipboardManager.OperationPastePlainText, Answers(WindowsClipboardErrorCode.None, "ab\0cd"));

            Assert.AreEqual("ab", result.Text);
        }

        [Test]
        public void InvokeInOrder_OneCommonSubscriberThrowingDoesNotSilenceTheNext()
        {
            // A multicast delegate invoked in one go stops at the first exception, and the
            // subscribers behind it stay silent on every raise from then on - not just this one.
            var seen = new List<string>();
            Action<string> first = _ => throw new InvalidOperationException("boom");
            Action<string> second = seen.Add;

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("a subscriber threw"));
            WindowsClipboardManager.InvokeInOrder("payload", first + second, null);

            CollectionAssert.AreEqual(new[] { "payload" }, seen);
        }


        // ── Operation guard (design 7.5) ─────────────────────────────────────────
        // The public operations are instance methods, and creating the Manager needs a player
        // loop, so EditMode covers the guard through its seam. The API-level rejections live in
        // the PlayMode integration tests.

        // The order is the contract, so it is checked on the classifier directly rather than
        // through the manager: the editor is never a Windows player, so every state-dependent
        // answer would otherwise be hidden behind PlatformUnavailable.
        // Separate methods rather than TestCases: the state enum is internal, and a public test
        // method cannot take a parameter of a less accessible type.

        [Test]
        public void Guard_OnAWorkerThread_ReportsMainThreadRequiredBeforeAnythingElse()
        {
            // Every other condition is also wrong here; the thread still wins.
            Assert.AreEqual(WindowsClipboardErrorCode.MainThreadRequired,
                WindowsClipboardManager.ClassifyOperationGuard(
                    isMainThread: false,
                    terminated: true,
                    platformAvailable: false,
                    state: WindowsClipboardManagerState.Draining));
        }

        [Test]
        public void Guard_AfterDestruction_ReportsManagerDestroyedEvenOnAnUnsupportedPlatform()
        {
            // The tombstone outranks both the platform and the state: a recreated manager must
            // stay inert, and saying why it is inert matters more than saying where it runs.
            Assert.AreEqual(WindowsClipboardErrorCode.ManagerDestroyed,
                WindowsClipboardManager.ClassifyOperationGuard(
                    isMainThread: true,
                    terminated: true,
                    platformAvailable: false,
                    state: WindowsClipboardManagerState.Running));
        }

        [Test]
        public void Guard_OutsideAWindowsPlayer_ReportsPlatformUnavailableRatherThanNotInitialized()
        {
            // Initialize cannot succeed off a Windows player, so NotInitializedByHost would send
            // the caller after an Initialize that could never have worked.
            Assert.AreEqual(WindowsClipboardErrorCode.PlatformUnavailable,
                WindowsClipboardManager.ClassifyOperationGuard(
                    isMainThread: true,
                    terminated: false,
                    platformAvailable: false,
                    state: WindowsClipboardManagerState.Uninitialized));
        }

        [Test]
        public void Guard_WhileDraining_ReportsShuttingDown()
        {
            Assert.AreEqual(WindowsClipboardErrorCode.ShuttingDown,
                WindowsClipboardManager.ClassifyOperationGuard(
                    isMainThread: true,
                    terminated: false,
                    platformAvailable: true,
                    state: WindowsClipboardManagerState.Draining));
        }

        [Test]
        public void Guard_AfterAFailedShutdown_ReportsShuttingDown()
        {
            Assert.AreEqual(WindowsClipboardErrorCode.ShuttingDown,
                WindowsClipboardManager.ClassifyOperationGuard(
                    isMainThread: true,
                    terminated: false,
                    platformAvailable: true,
                    state: WindowsClipboardManagerState.ShutdownFailed));
        }

        [Test]
        public void Guard_BeforeInitialize_ReportsNotInitializedByHost()
        {
            // Stopping here keeps a caller that forgot to initialize from reaching the native side
            // just to receive its NotInitialized.
            Assert.AreEqual(WindowsClipboardErrorCode.NotInitializedByHost,
                WindowsClipboardManager.ClassifyOperationGuard(
                    isMainThread: true,
                    terminated: false,
                    platformAvailable: true,
                    state: WindowsClipboardManagerState.Uninitialized));
        }

        [Test]
        public void Guard_AfterAFinishedShutdown_ReportsNotInitializedByHost()
        {
            Assert.AreEqual(WindowsClipboardErrorCode.NotInitializedByHost,
                WindowsClipboardManager.ClassifyOperationGuard(
                    isMainThread: true,
                    terminated: false,
                    platformAvailable: true,
                    state: WindowsClipboardManagerState.ShutDown));
        }

        [Test]
        public void Guard_WhileRunningOnAWindowsPlayer_Passes()
        {
            Assert.AreEqual(WindowsClipboardErrorCode.None,
                WindowsClipboardManager.ClassifyOperationGuard(
                    isMainThread: true,
                    terminated: false,
                    platformAvailable: true,
                    state: WindowsClipboardManagerState.Running));
        }

        // The two below check that the live guard is wired to the classifier at all.

        [Test]
        public void OperationGuard_InTheEditor_ReportsPlatformUnavailable()
        {
            WindowsClipboardManager.ResetForTests();
            WindowsClipboardManager.SetStateForTests(WindowsClipboardManagerState.Running);

            Assert.AreEqual(WindowsClipboardErrorCode.PlatformUnavailable,
                WindowsClipboardManager.CheckOperationGuardForTests());
        }

        [Test]
        public void OperationGuard_AfterDestruction_ReportsManagerDestroyedEvenWhileRunning()
        {
            WindowsClipboardManager.SetStateForTests(WindowsClipboardManagerState.Running);
            WindowsClipboardManager.SetTerminatedForTests(true);

            Assert.AreEqual(WindowsClipboardErrorCode.ManagerDestroyed,
                WindowsClipboardManager.CheckOperationGuardForTests());
        }


        [Test]
        public void Shutdown_AnAttemptFromAFinishedManagerDoesNotReportASecondCompletion()
        {
            // OnDestroy now runs an attempt whatever the state, and the native uninit is
            // idempotent, so a finished manager must be left exactly as it was.
            WindowsClipboardManager.SetStateForTests(WindowsClipboardManagerState.Running);
            WindowsClipboardManager.SetComOwnershipForTests(WindowsClipboardComOwnership.Initialized);
            WindowsClipboardManager.InjectShutdownResultForTests(true, WindowsClipboardErrorCode.None);
            Assert.AreEqual(1, WindowsClipboardManager.ComReleaseCountForTests);

            WindowsClipboardManager.InjectShutdownResultForTests(true, WindowsClipboardErrorCode.None);

            Assert.AreEqual(WindowsClipboardManagerState.ShutDown, WindowsClipboardManager.StateForTests);
            Assert.AreEqual(1, WindowsClipboardManager.ComReleaseCountForTests,
                "a second attempt on a finished manager must not release again");
        }

        [Test]
        public void Shutdown_AnAttemptOnAManagerThatNeverStartedIsNotATerminalFailure()
        {
            WindowsClipboardManager.ResetForTests();

            // WrongThread is terminal for a live manager, but there is nothing here to fail.
            WindowsClipboardManager.InjectShutdownResultForTests(
                false, WindowsClipboardErrorCode.WrongThread);

            Assert.AreEqual(WindowsClipboardManagerState.Uninitialized,
                WindowsClipboardManager.StateForTests);
        }

        // ── Lifecycle state machine ──────────────────────────────────────────────

        [Test]
        public void ShutdownAttempt_FromRunning_CompletedReachesShutDown()
        {
            WindowsClipboardManager.SetStateForTests(WindowsClipboardManagerState.Running);

            WindowsClipboardManager.InjectShutdownResultForTests(true, WindowsClipboardErrorCode.None);

            Assert.AreEqual(WindowsClipboardManagerState.ShutDown, WindowsClipboardManager.StateForTests);
        }

        [Test]
        public void ShutdownAttempt_FromRunning_NotYetStaysDraining()
        {
            WindowsClipboardManager.SetStateForTests(WindowsClipboardManagerState.Running);

            WindowsClipboardManager.InjectShutdownResultForTests(false, WindowsClipboardErrorCode.Busy);

            // The native gate is already closed at this point, so staying Running would let new
            // operations through that the native side would reject as NotInitialized.
            Assert.AreEqual(WindowsClipboardManagerState.Draining, WindowsClipboardManager.StateForTests);
        }

        [Test]
        public void ShutdownAttempt_FromRunning_TerminalFailureReachesShutdownFailed()
        {
            WindowsClipboardManager.SetStateForTests(WindowsClipboardManagerState.Running);

            WindowsClipboardManager.InjectShutdownResultForTests(false, WindowsClipboardErrorCode.WrongThread);

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("terminal shutdown failure"));
            Assert.AreEqual(WindowsClipboardManagerState.ShutdownFailed, WindowsClipboardManager.StateForTests);
        }

        [Test]
        public void ShutdownAttempt_FromShutdownFailed_CanStillReachShutDown()
        {
            WindowsClipboardManager.SetStateForTests(WindowsClipboardManagerState.ShutdownFailed);

            WindowsClipboardManager.InjectShutdownResultForTests(true, WindowsClipboardErrorCode.None);

            Assert.AreEqual(WindowsClipboardManagerState.ShutDown, WindowsClipboardManager.StateForTests);
        }

        // ── COM ownership ────────────────────────────────────────────────────────

        [Test]
        public void CompletedShutdown_ReleasesTheComReferenceThisLayerOwns()
        {
            WindowsClipboardManager.SetStateForTests(WindowsClipboardManagerState.Running);
            WindowsClipboardManager.SetComOwnershipForTests(WindowsClipboardComOwnership.Initialized);

            WindowsClipboardManager.InjectShutdownResultForTests(true, WindowsClipboardErrorCode.None);

            Assert.AreEqual(WindowsClipboardComOwnership.None, WindowsClipboardManager.ComOwnershipForTests);
            Assert.AreEqual(1, WindowsClipboardManager.ComReleaseCountForTests, "released exactly once");
        }

        [Test]
        public void IncompleteShutdown_KeepsTheComReference()
        {
            WindowsClipboardManager.SetStateForTests(WindowsClipboardManagerState.Running);
            WindowsClipboardManager.SetComOwnershipForTests(WindowsClipboardComOwnership.RefCounted);

            WindowsClipboardManager.InjectShutdownResultForTests(false, WindowsClipboardErrorCode.Busy);

            // The native side may still hold COM objects and the hidden window, so folding the
            // apartment first would be undefined behaviour.
            Assert.AreEqual(WindowsClipboardComOwnership.RefCounted,
                WindowsClipboardManager.ComOwnershipForTests);
            Assert.AreEqual(0, WindowsClipboardManager.ComReleaseCountForTests);
        }

        [Test]
        public void TerminalShutdownFailure_KeepsTheComReference()
        {
            WindowsClipboardManager.SetStateForTests(WindowsClipboardManagerState.Running);
            WindowsClipboardManager.SetComOwnershipForTests(WindowsClipboardComOwnership.Initialized);

            WindowsClipboardManager.InjectShutdownResultForTests(false, WindowsClipboardErrorCode.WrongThread);

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("terminal shutdown failure"));
            Assert.AreEqual(WindowsClipboardComOwnership.Initialized,
                WindowsClipboardManager.ComOwnershipForTests);
            Assert.AreEqual(0, WindowsClipboardManager.ComReleaseCountForTests);
        }

        [Test]
        public void RepeatedShutdownAttempts_ReleaseTheComReferenceOnlyOnce()
        {
            WindowsClipboardManager.SetStateForTests(WindowsClipboardManagerState.Running);
            WindowsClipboardManager.SetComOwnershipForTests(WindowsClipboardComOwnership.Initialized);

            WindowsClipboardManager.InjectShutdownResultForTests(true, WindowsClipboardErrorCode.None);
            WindowsClipboardManager.InjectShutdownResultForTests(true, WindowsClipboardErrorCode.None);

            Assert.AreEqual(1, WindowsClipboardManager.ComReleaseCountForTests);
        }

        [Test]
        public void ClipboardChangedSeam_WithoutADispatcherIsHarmless()
        {
            // ResetForTests drops the cached dispatcher, which is the state a change notification
            // can arrive in after a teardown. It must be dropped rather than throwing.
            WindowsClipboardManager.ResetForTests();

            Assert.DoesNotThrow(() => WindowsClipboardManager.InjectClipboardChangedForTests());
        }

        [Test]
        public void ResetForTests_ClearsTheLifecycleState()
        {
            WindowsClipboardManager.SetStateForTests(WindowsClipboardManagerState.Draining);
            WindowsClipboardManager.SetComOwnershipForTests(WindowsClipboardComOwnership.RefCounted);

            WindowsClipboardManager.ResetForTests();

            Assert.AreEqual(WindowsClipboardManagerState.Uninitialized, WindowsClipboardManager.StateForTests);
            Assert.AreEqual(WindowsClipboardComOwnership.None, WindowsClipboardManager.ComOwnershipForTests);
            Assert.IsFalse(WindowsClipboardManager.IsTerminated);
            Assert.IsFalse(WindowsClipboardManager.QuitHandlerSubscribedForTests);
        }
    }
}
#endif
