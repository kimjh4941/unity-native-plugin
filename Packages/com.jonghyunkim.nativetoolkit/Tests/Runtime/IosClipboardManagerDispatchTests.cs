#nullable enable

#if UNITY_IOS || UNITY_EDITOR
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using JonghyunKim.NativeToolkit.Runtime.Clipboard;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// EditMode tests for the Manager's pure static helpers: dispatch ordering, the single-flight
    /// guard, and the destroy cleanup contract.
    /// <para>
    /// No Manager instance is created here: <c>Awake</c> touches P/Invoke-adjacent state, so
    /// instance-level behaviour belongs to the PlayMode suite.
    /// </para>
    /// </summary>
    public sealed class IosClipboardManagerDispatchTests
    {
        // ── InvokeInOrder ───────────────────────────────────────────────────────

        [Test]
        public void InvokeInOrder_CommonThenPerCall_InvokedOnceEachInOrder()
        {
            var order = new List<string>();
            IosClipboardOperationResult result =
                IosClipboardOperationResult.Success(IosClipboardManager.OperationCopy);

            IosClipboardManager.InvokeInOrder(
                result,
                common: _ => order.Add("common"),
                perCall: _ => order.Add("perCall"));

            Assert.AreEqual(new[] { "common", "perCall" }, order.ToArray());
        }

        [Test]
        public void InvokeInOrder_PassesTheSameResultToBoth()
        {
            IosClipboardReadResult result = IosClipboardReadResult.Failure("CLIPBOARD_UNAVAILABLE", "unavailable");
            IosClipboardReadResult? common = null;
            IosClipboardReadResult? perCall = null;

            IosClipboardManager.InvokeInOrder(result, r => common = r, r => perCall = r);

            Assert.AreEqual("CLIPBOARD_UNAVAILABLE", common!.Value.Error!.Value.Code);
            Assert.AreEqual("CLIPBOARD_UNAVAILABLE", perCall!.Value.Error!.Value.Code);
        }

        [Test]
        public void InvokeInOrder_ThrowingCommon_StillInvokesPerCall()
        {
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*InvokeInOrder.*"));

            bool perCallInvoked = false;
            IosClipboardManager.InvokeInOrder(
                IosClipboardOperationResult.Success(IosClipboardManager.OperationClear),
                common: _ => throw new InvalidOperationException("boom"),
                perCall: _ => perCallInvoked = true);

            Assert.IsTrue(perCallInvoked, "a throwing subscriber must not suppress the other");
        }

        [Test]
        public void InvokeInOrder_ThrowingPerCall_DoesNotEscape()
        {
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*InvokeInOrder.*"));

            bool commonInvoked = false;
            Assert.DoesNotThrow(() => IosClipboardManager.InvokeInOrder(
                IosClipboardOperationResult.Success(IosClipboardManager.OperationClear),
                common: _ => commonInvoked = true,
                perCall: _ => throw new InvalidOperationException("boom")));

            Assert.IsTrue(commonInvoked);
        }

        [Test]
        public void InvokeInOrder_BothNull_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => IosClipboardManager.InvokeInOrder(
                IosClipboardOperationResult.Success(IosClipboardManager.OperationCancelLoads),
                common: null,
                perCall: null));
        }

        // ── single-flight ───────────────────────────────────────────────────────

        [Test]
        public void TryBeginOperation_SecondCallForTheSameKey_IsRejected()
        {
            var inFlight = new HashSet<string>();

            Assert.IsTrue(IosClipboardManager.TryBeginOperation(inFlight, IosClipboardManager.OperationRead));
            Assert.IsFalse(IosClipboardManager.TryBeginOperation(inFlight, IosClipboardManager.OperationRead));
        }

        [Test]
        public void TryBeginOperation_AfterEndOperation_CanBeTakenAgain()
        {
            var inFlight = new HashSet<string>();

            Assert.IsTrue(IosClipboardManager.TryBeginOperation(inFlight, IosClipboardManager.OperationRead));
            IosClipboardManager.EndOperation(inFlight, IosClipboardManager.OperationRead);
            Assert.IsTrue(IosClipboardManager.TryBeginOperation(inFlight, IosClipboardManager.OperationRead));
        }

        [Test]
        public void EndOperation_ForAKeyThatWasNeverTaken_IsHarmless()
        {
            var inFlight = new HashSet<string>();
            Assert.DoesNotThrow(() => IosClipboardManager.EndOperation(inFlight, IosClipboardManager.OperationRead));
            Assert.AreEqual(0, inFlight.Count);
        }

        [Test]
        public void TryBeginOperation_DifferentOperations_DoNotInterfere()
        {
            var inFlight = new HashSet<string>();

            Assert.IsTrue(IosClipboardManager.TryBeginOperation(inFlight, IosClipboardManager.OperationRead));
            Assert.IsTrue(IosClipboardManager.TryBeginOperation(inFlight, IosClipboardManager.OperationCopy));
            Assert.IsTrue(IosClipboardManager.TryBeginOperation(inFlight, IosClipboardManager.OperationLoadItem));

            IosClipboardManager.EndOperation(inFlight, IosClipboardManager.OperationCopy);
            Assert.IsFalse(IosClipboardManager.TryBeginOperation(inFlight, IosClipboardManager.OperationRead));
        }

        [Test]
        public void StartAndStopObserving_ShareOneObservationKey()
        {
            // Both mutate the same native subscription, so they must serialize.
            var inFlight = new HashSet<string>();

            Assert.IsTrue(IosClipboardManager.TryBeginOperation(inFlight, IosClipboardManager.ObservationControlKey));
            Assert.IsFalse(
                IosClipboardManager.TryBeginOperation(inFlight, IosClipboardManager.ObservationControlKey),
                "a stop must not start while a start is pending");

            Assert.AreNotEqual(IosClipboardManager.OperationStartObserving, IosClipboardManager.ObservationControlKey);
            Assert.AreNotEqual(IosClipboardManager.OperationStopObserving, IosClipboardManager.ObservationControlKey);
        }

        /// <summary>
        /// Reproduces the misdelivery the single-flight guard exists to prevent, using a pure state
        /// model rather than a switch on the production Manager.
        /// </summary>
        [Test]
        public void SingleFlight_OutOfOrderCompletion_CannotDeliverOneCallsResultToAnother()
        {
            var inFlight = new HashSet<string>();
            const string operation = IosClipboardManager.OperationRead;

            // Call A takes the slot.
            Assert.IsTrue(IosClipboardManager.TryBeginOperation(inFlight, operation));
            Action<string>? slot = _ => { };
            string? deliveredToA = null;
            slot = payload => deliveredToA = payload;

            // Call B is rejected while A is pending: it must not touch A's slot.
            Assert.IsFalse(IosClipboardManager.TryBeginOperation(inFlight, operation));
            string? deliveredToB = null;
            Action<string> rejectedCallback = payload => deliveredToB = payload;
            IosClipboardManager.InvokeInOrder("busy", common: null, perCall: rejectedCallback);

            Assert.AreEqual("busy", deliveredToB, "the rejected call receives its own result");
            Assert.IsNull(deliveredToA, "the pending call must not receive the rejection");

            // A's own native result arrives afterwards and still finds its callback intact.
            Action<string>? taken = slot;
            slot = null;
            IosClipboardManager.EndOperation(inFlight, operation);
            IosClipboardManager.InvokeInOrder("A-result", common: null, perCall: taken);

            Assert.AreEqual("A-result", deliveredToA);
            Assert.AreEqual("busy", deliveredToB, "B never sees A's payload");
            Assert.IsNull(slot);
        }

        // ── RunDestroyCleanup ───────────────────────────────────────────────────

        [Test]
        public void RunDestroyCleanup_InvokesStopThenCancelThenManaged()
        {
            var order = new List<string>();

            IosClipboardManager.RunDestroyCleanup(
                stop: () => order.Add("stop"),
                cancel: () => order.Add("cancel"),
                managedCleanup: () => order.Add("managed"));

            Assert.AreEqual(new[] { "stop", "cancel", "managed" }, order.ToArray());
        }

        [Test]
        public void RunDestroyCleanup_ThrowingStop_StillRunsCancelAndManaged()
        {
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*RunDestroyCleanup.*stop.*"));

            bool cancelled = false;
            bool cleaned = false;

            IosClipboardManager.RunDestroyCleanup(
                stop: () => throw new DllNotFoundException("__Internal"),
                cancel: () => cancelled = true,
                managedCleanup: () => cleaned = true);

            Assert.IsTrue(cancelled, "a failing stop must not skip cancel");
            Assert.IsTrue(cleaned, "managed cleanup always runs");
        }

        [Test]
        public void RunDestroyCleanup_ThrowingCancel_StillRunsManaged()
        {
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*RunDestroyCleanup.*cancel.*"));

            bool cleaned = false;

            IosClipboardManager.RunDestroyCleanup(
                stop: () => { },
                cancel: () => throw new EntryPointNotFoundException("clipboardCancelLoads"),
                managedCleanup: () => cleaned = true);

            Assert.IsTrue(cleaned);
        }

        [Test]
        public void RunDestroyCleanup_BothNativeStepsThrowing_StillRunsManaged()
        {
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*RunDestroyCleanup.*stop.*"));
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*RunDestroyCleanup.*cancel.*"));

            bool cleaned = false;

            Assert.DoesNotThrow(() => IosClipboardManager.RunDestroyCleanup(
                stop: () => throw new InvalidOperationException("stop"),
                cancel: () => throw new InvalidOperationException("cancel"),
                managedCleanup: () => cleaned = true));

            Assert.IsTrue(cleaned);
        }

        [Test]
        public void RunDestroyCleanup_ThrowingManagedCleanup_DoesNotEscape()
        {
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*RunDestroyCleanup.*managed.*"));

            Assert.DoesNotThrow(() => IosClipboardManager.RunDestroyCleanup(
                stop: () => { },
                cancel: () => { },
                managedCleanup: () => throw new InvalidOperationException("managed")));
        }

        [Test]
        public void RunDestroyCleanup_NoExceptionEverEscapesTheUnityLifecycleCallback()
        {
            LogAssert.ignoreFailingMessages = true;
            try
            {
                Assert.DoesNotThrow(() => IosClipboardManager.RunDestroyCleanup(
                    stop: () => throw new Exception("a"),
                    cancel: () => throw new Exception("b"),
                    managedCleanup: () => throw new Exception("c")));
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
            }
        }
    }
}
#endif
