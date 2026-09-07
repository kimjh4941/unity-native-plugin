#nullable enable

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
using System;
using System.Collections.Generic;
using JonghyunKim.NativeToolkit.Runtime.Clipboard;
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

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("common event subscriber threw"));
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
