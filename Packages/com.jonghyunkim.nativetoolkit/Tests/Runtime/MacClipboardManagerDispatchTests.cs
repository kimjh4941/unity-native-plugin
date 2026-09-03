#nullable enable

#if UNITY_STANDALONE_OSX || UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using JonghyunKim.NativeToolkit.Runtime.Clipboard;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// EditMode tests for the macOS Manager's pure static helpers: dispatch ordering, the
    /// single-flight guard, and the destroy cleanup contract.
    /// <para>
    /// No Manager instance is created here: <c>Awake</c> captures the dispatcher and the main
    /// thread id, so instance-level behaviour belongs to the PlayMode suite.
    /// </para>
    /// </summary>
    public sealed class MacClipboardManagerDispatchTests
    {
        // ── InvokeInOrder ───────────────────────────────────────────────────────

        [Test]
        public void InvokeInOrder_CommonThenPerCall_InvokedOnceEachInOrder()
        {
            var order = new List<string>();
            MacClipboardChangeCountResult result = MacClipboardChangeCountResult.Success(7);

            MacClipboardManager.InvokeInOrder(
                result,
                common: _ => order.Add("common"),
                perCall: _ => order.Add("perCall"));

            Assert.AreEqual(new[] { "common", "perCall" }, order.ToArray());
        }

        [Test]
        public void InvokeInOrder_PassesTheSameResultToBoth()
        {
            MacClipboardOwnershipResult result = MacClipboardOwnershipResult.Failure(
                MacClipboardOperations.Copy, MacClipboardErrorCodes.WriteRejected, "rejected");
            MacClipboardOwnershipResult? common = null;
            MacClipboardOwnershipResult? perCall = null;

            MacClipboardManager.InvokeInOrder(result, r => common = r, r => perCall = r);

            Assert.AreEqual(MacClipboardErrorCodes.WriteRejected, common!.Value.Error!.Value.Code);
            Assert.AreEqual(MacClipboardErrorCodes.WriteRejected, perCall!.Value.Error!.Value.Code);
            Assert.AreEqual(MacClipboardOperations.Copy, perCall.Value.Operation);
        }

        [Test]
        public void InvokeInOrder_ThrowingCommon_StillInvokesPerCall()
        {
            // The per-call callback is the only channel a caller controls directly, so a throwing
            // common subscriber must not be able to silence it.
            LogAssert.Expect(LogType.Error, new Regex(".*InvokeInOrder.*"));

            bool perCallInvoked = false;
            MacClipboardManager.InvokeInOrder(
                MacClipboardChangeCountResult.Success(1),
                _ => throw new InvalidOperationException("subscriber"),
                _ => perCallInvoked = true);

            Assert.IsTrue(perCallInvoked);
        }

        [Test]
        public void InvokeInOrder_ThrowingPerCall_DoesNotEscape()
        {
            // An escaping exception would unwind into the native stack frame that delivered the
            // callback, which is undefined behaviour.
            LogAssert.Expect(LogType.Error, new Regex(".*InvokeInOrder.*"));

            bool commonInvoked = false;
            Assert.DoesNotThrow(() => MacClipboardManager.InvokeInOrder(
                MacClipboardChangeCountResult.Success(1),
                _ => commonInvoked = true,
                _ => throw new InvalidOperationException("subscriber")));

            Assert.IsTrue(commonInvoked);
        }

        [Test]
        public void InvokeInOrder_BothThrowing_ReportsBothAndDoesNotEscape()
        {
            LogAssert.Expect(LogType.Error, new Regex(".*InvokeInOrder.*common.*"));
            LogAssert.Expect(LogType.Error, new Regex(".*InvokeInOrder.*perCall.*"));

            Assert.DoesNotThrow(() => MacClipboardManager.InvokeInOrder(
                MacClipboardChangeCountResult.Success(1),
                _ => throw new InvalidOperationException("a"),
                _ => throw new InvalidOperationException("b")));
        }

        [Test]
        public void InvokeInOrder_BothNull_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => MacClipboardManager.InvokeInOrder(
                MacClipboardReadDataResult.Success(null), null, null));
        }

        // ── single-flight ───────────────────────────────────────────────────────

        [Test]
        public void TryBeginOperation_SecondCallForTheSameKey_IsRejected()
        {
            var inFlight = new HashSet<string>();

            Assert.IsTrue(MacClipboardManager.TryBeginOperation(inFlight, MacClipboardOperations.Copy));
            Assert.IsFalse(MacClipboardManager.TryBeginOperation(inFlight, MacClipboardOperations.Copy));
        }

        [Test]
        public void TryBeginOperation_AfterEndOperation_CanBeTakenAgain()
        {
            var inFlight = new HashSet<string>();

            MacClipboardManager.TryBeginOperation(inFlight, MacClipboardOperations.Read);
            MacClipboardManager.EndOperation(inFlight, MacClipboardOperations.Read);

            Assert.IsTrue(MacClipboardManager.TryBeginOperation(inFlight, MacClipboardOperations.Read));
        }

        [Test]
        public void EndOperation_ForAKeyThatWasNeverTaken_IsHarmless()
        {
            var inFlight = new HashSet<string>();

            Assert.DoesNotThrow(() => MacClipboardManager.EndOperation(inFlight, MacClipboardOperations.Clear));
            Assert.AreEqual(0, inFlight.Count);
        }

        [Test]
        public void TryBeginOperation_DifferentOperations_DoNotInterfere()
        {
            var inFlight = new HashSet<string>();

            Assert.IsTrue(MacClipboardManager.TryBeginOperation(inFlight, MacClipboardOperations.Copy));
            Assert.IsTrue(MacClipboardManager.TryBeginOperation(inFlight, MacClipboardOperations.Append));
            Assert.IsTrue(MacClipboardManager.TryBeginOperation(inFlight, MacClipboardOperations.Read));
            Assert.IsTrue(MacClipboardManager.TryBeginOperation(inFlight, MacClipboardOperations.ReadData));
            Assert.IsTrue(MacClipboardManager.TryBeginOperation(inFlight, MacClipboardOperations.Clear));

            Assert.AreEqual(5, inFlight.Count);
        }

        [Test]
        public void CopyAndAppend_AreSeparateSingleFlightKeys()
        {
            // They share a result type but not a slot, so they must not share a marker either:
            // appending to what was just copied is the normal sequence, not a conflict.
            var inFlight = new HashSet<string>();

            Assert.IsTrue(MacClipboardManager.TryBeginOperation(inFlight, MacClipboardOperations.Copy));
            Assert.IsTrue(MacClipboardManager.TryBeginOperation(inFlight, MacClipboardOperations.Append));
        }

        // ── RunDestroyCleanup ───────────────────────────────────────────────────

        [Test]
        public void RunDestroyCleanup_InvokesStopThenManaged()
        {
            var order = new List<string>();

            MacClipboardManager.RunDestroyCleanup(
                stop: () => order.Add("stop"),
                managedCleanup: () => order.Add("managed"));

            Assert.AreEqual(new[] { "stop", "managed" }, order.ToArray());
        }

        [Test]
        public void RunDestroyCleanup_ThrowingStop_StillRunsManagedCleanup()
        {
            // The managed cleanup is what clears the pending slots and the instance reference.
            // Skipping it after a failed native stop would leave the Manager half torn down.
            LogAssert.Expect(LogType.Error, new Regex(".*RunDestroyCleanup.*stop.*"));

            bool managedRan = false;
            MacClipboardManager.RunDestroyCleanup(
                stop: () => throw new InvalidOperationException("native"),
                managedCleanup: () => managedRan = true);

            Assert.IsTrue(managedRan);
        }

        [Test]
        public void RunDestroyCleanup_ThrowingManagedCleanup_DoesNotEscape()
        {
            LogAssert.Expect(LogType.Error, new Regex(".*RunDestroyCleanup.*managed.*"));

            Assert.DoesNotThrow(() => MacClipboardManager.RunDestroyCleanup(
                stop: () => { },
                managedCleanup: () => throw new InvalidOperationException("managed")));
        }

        [Test]
        public void RunDestroyCleanup_NoExceptionEverEscapesTheUnityLifecycleCallback()
        {
            // OnDestroy is called by Unity; an exception escaping it aborts the rest of the
            // engine's destruction sequence for the scene.
            LogAssert.Expect(LogType.Error, new Regex(".*RunDestroyCleanup.*stop.*"));
            LogAssert.Expect(LogType.Error, new Regex(".*RunDestroyCleanup.*managed.*"));

            Assert.DoesNotThrow(() => MacClipboardManager.RunDestroyCleanup(
                stop: () => throw new InvalidOperationException("a"),
                managedCleanup: () => throw new InvalidOperationException("b")));
        }
    }
}
#endif
