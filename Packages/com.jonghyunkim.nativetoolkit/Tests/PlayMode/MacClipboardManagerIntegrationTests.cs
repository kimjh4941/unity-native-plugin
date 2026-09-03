#nullable enable

#if UNITY_STANDALONE_OSX || UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using JonghyunKim.NativeToolkit.Runtime.Clipboard;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// PlayMode integration tests for <c>MacClipboardManager</c>.
    /// <para>
    /// These need a running player loop because <c>UnityMainThreadDispatcher</c> only flushes its
    /// queue from <c>Update</c>.
    /// </para>
    /// <para>
    /// Tests that must reach the guard chain past the platform check enable
    /// <c>BridgeAvailableOverrideForTests</c>. The native call itself compiles to nothing in the
    /// Editor, so the operation really does own the pending slot and the in-flight marker until the
    /// test delivers a result through a Complete*ForTests seam. Everything under test is therefore
    /// the production state machine, not a stand-in model.
    /// </para>
    /// <para>
    /// Change observation is not covered here: this stage exposes no way to start it.
    /// </para>
    /// </summary>
    public sealed class MacClipboardManagerIntegrationTests
    {
        private const string OwnershipJson = "{\"scope\":{\"kind\":\"general\"},\"changeCount\":12}";
        private const string ReadJson =
            "{\"changeCount\":3,\"items\":[{\"representations\":{\"public.utf8-plain-text\":\"aGk=\"}}]}";
        private const string ReadDataJson = "{\"data\":\"aGk=\"}";
        private const string ChangeCountJson = "{\"changeCount\":9}";

        [TearDown]
        public void TearDown()
        {
            // Destroy first, then reset: ResetForTests clears the captured main-thread id and
            // dispatcher, which only Awake re-establishes.
            DestroyManagerIfPresent();
            MacClipboardManager.ResetForTests();
            LogAssert.ignoreFailingMessages = false;
        }

        private static void DestroyManagerIfPresent()
        {
            foreach (MacClipboardManager manager in
                     UnityEngine.Object.FindObjectsByType<MacClipboardManager>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                UnityEngine.Object.DestroyImmediate(manager.gameObject);
            }
        }

        /// <summary>Creates the Manager and makes the guard chain run past the platform check.</summary>
        private static MacClipboardManager BridgedManager()
        {
            MacClipboardManager manager = MacClipboardManager.Instance;
            MacClipboardManager.BridgeAvailableOverrideForTests = true;
            return manager;
        }

        private static MacClipboardContent OneByte() =>
            MacClipboardContent.Single(MacClipboardContentItem.Data("public.data", new byte[] { 0x61 }));

        private static MacPasteboardOwnership GeneralOwnership() =>
            new(MacPasteboardScope.General, 12);

        // ── guard chain stage 4: no bridge in the Editor ────────────────────────

        [UnityTest]
        public IEnumerator EveryOperation_InEditor_FailsWithBridgeUnavailable()
        {
            MacClipboardManager manager = MacClipboardManager.Instance;

            var codes = new List<int>();
            manager.Copy(OneByte(), null, null, r => codes.Add(r.Error!.Value.Code));
            manager.Append(OneByte(), GeneralOwnership(), r => codes.Add(r.Error!.Value.Code));
            manager.Read(null, r => codes.Add(r.Error!.Value.Code));
            manager.ReadData("public.data", null, r => codes.Add(r.Error!.Value.Code));
            manager.Clear(null, r => codes.Add(r.Error!.Value.Code));

            yield return null;

            Assert.AreEqual(5, codes.Count, "all five operations report a result");
            foreach (int code in codes)
            {
                Assert.AreEqual(MacClipboardErrorCodes.BridgeUnavailable, code);
            }
            Assert.AreEqual(0, MacClipboardManager.InFlightCountForTests);
            Assert.IsFalse(MacClipboardManager.HasAnyPendingCallbackForTests);
        }

        [UnityTest]
        public IEnumerator BridgeUnavailable_MessageNamesTheMacOsStandalonePlayer()
        {
            MacClipboardManager manager = MacClipboardManager.Instance;

            MacClipboardReadResult? result = null;
            manager.Read(null, r => result = r);

            yield return null;

            StringAssert.Contains("macOS Standalone player", result!.Value.Error!.Value.Message);
        }

        // ── guard chain stage 1: off the main thread ────────────────────────────

        [UnityTest]
        public IEnumerator OffThreadCall_OnACachedInstance_IsRejected()
        {
            LogAssert.ignoreFailingMessages = true;

            // The reference is taken on the main thread: the Instance getter itself is main-thread
            // only and cannot be guarded, so only instance methods are covered.
            MacClipboardManager manager = BridgedManager();

            MacClipboardReadResult? result = null;
            var worker = new Thread(() => manager.Read(null, r => result = r));
            worker.Start();
            worker.Join();

            yield return null;

            Assert.IsNotNull(result, "the rejection is still delivered on the main thread");
            Assert.AreEqual(MacClipboardErrorCodes.MainThreadRequired, result!.Value.Error!.Value.Code);
            StringAssert.Contains("main thread", result.Value.Error.Value.Message);
        }

        [UnityTest]
        public IEnumerator OffThreadCall_DoesNotPolluteTheInFlightState()
        {
            LogAssert.ignoreFailingMessages = true;

            MacClipboardManager manager = BridgedManager();

            var worker = new Thread(() => manager.Read());
            worker.Start();
            worker.Join();

            yield return null;

            Assert.IsFalse(MacClipboardManager.IsInFlightForTests(MacClipboardOperations.Read));

            // If the rejected off-thread call had taken the marker, this would report busy.
            MacClipboardReadResult? result = null;
            manager.Read(null, r => result = r);
            MacClipboardManager.CompleteReadForTests(true, ReadJson);

            yield return null;

            Assert.IsTrue(result!.Value.IsSuccess);
        }

        // ── guard chain stage 3: arguments ──────────────────────────────────────

        [UnityTest]
        public IEnumerator NullContent_FailsWithInvalidRequest()
        {
            LogAssert.ignoreFailingMessages = true;
            MacClipboardManager manager = BridgedManager();

            MacClipboardOwnershipResult? result = null;
            manager.Copy(null!, null, null, r => result = r);

            yield return null;

            Assert.AreEqual(MacClipboardErrorCodes.InvalidRequest, result!.Value.Error!.Value.Code);
            StringAssert.Contains("content must not be null", result.Value.Error.Value.Message);
            Assert.AreEqual(MacClipboardOperations.Copy, result.Value.Operation);
        }

        [UnityTest]
        public IEnumerator NullOwnership_FailsWithInvalidRequest()
        {
            LogAssert.ignoreFailingMessages = true;
            MacClipboardManager manager = BridgedManager();

            MacClipboardOwnershipResult? result = null;
            manager.Append(OneByte(), null!, r => result = r);

            yield return null;

            Assert.AreEqual(MacClipboardErrorCodes.InvalidRequest, result!.Value.Error!.Value.Code);
            StringAssert.Contains("ownership must not be null", result.Value.Error.Value.Message);
            Assert.AreEqual(MacClipboardOperations.Append, result.Value.Operation);
        }

        [UnityTest]
        public IEnumerator OversizedContent_FailsWithRequestTooLarge()
        {
            LogAssert.ignoreFailingMessages = true;
            MacClipboardManager manager = BridgedManager();

            // The real limit is 32 MiB; allocating it would make this test needlessly heavy.
            MacClipboardManager.MaxRequestBytesOverrideForTests = 4;

            MacClipboardOwnershipResult? result = null;
            manager.Copy(
                MacClipboardContent.Single(
                    MacClipboardContentItem.Data("public.data", new byte[] { 1, 2, 3, 4, 5 })),
                null, null, r => result = r);

            yield return null;

            Assert.AreEqual(MacClipboardErrorCodes.RequestTooLarge, result!.Value.Error!.Value.Code);
            StringAssert.Contains("4 byte request limit", result.Value.Error.Value.Message);
        }

        [UnityTest]
        public IEnumerator ContentExactlyAtTheLimit_IsAccepted()
        {
            MacClipboardManager manager = BridgedManager();
            MacClipboardManager.MaxRequestBytesOverrideForTests = 4;

            manager.Copy(
                MacClipboardContent.Single(
                    MacClipboardContentItem.Data("public.data", new byte[] { 1, 2, 3, 4 })));

            yield return null;

            Assert.IsTrue(
                MacClipboardManager.IsInFlightForTests(MacClipboardOperations.Copy),
                "the limit is a maximum, not an exclusive bound");
        }

        [UnityTest]
        public IEnumerator SizeLimit_IsSummedAcrossItemsAndRepresentations()
        {
            LogAssert.ignoreFailingMessages = true;
            MacClipboardManager manager = BridgedManager();
            MacClipboardManager.MaxRequestBytesOverrideForTests = 4;

            // Neither item exceeds the limit on its own; together they do.
            MacClipboardOwnershipResult? result = null;
            manager.Copy(
                MacClipboardContent.Multiple(new[]
                {
                    MacClipboardContentItem.Data("a", new byte[] { 1, 2, 3 }),
                    MacClipboardContentItem.Data("b", new byte[] { 4, 5, 6 }),
                }),
                null, null, r => result = r);

            yield return null;

            Assert.AreEqual(MacClipboardErrorCodes.RequestTooLarge, result!.Value.Error!.Value.Code);
        }

        // ── guard chain stage 5: request building ───────────────────────────────

        [UnityTest]
        public IEnumerator UnbuildableContent_FailsWithBridgeUnavailableAndTakesNoMarker()
        {
            LogAssert.ignoreFailingMessages = true;
            MacClipboardManager manager = BridgedManager();

            // A null representation passes the size check as zero bytes by design, then throws in
            // Convert.ToBase64String. That is the documented entry point into stage 5.
            var representations = new Dictionary<string, byte[]> { ["public.data"] = null! };
            MacClipboardOwnershipResult? result = null;
            manager.Copy(
                MacClipboardContent.Single(MacClipboardContentItem.FromRepresentations(representations)),
                null, null, r => result = r);

            yield return null;

            Assert.AreEqual(MacClipboardErrorCodes.BridgeUnavailable, result!.Value.Error!.Value.Code);
            StringAssert.Contains("could not be started", result.Value.Error.Value.Message);
            Assert.IsFalse(
                MacClipboardManager.IsInFlightForTests(MacClipboardOperations.Copy),
                "building happens before the marker is taken, so a build failure cannot strand it");
            Assert.IsFalse(MacClipboardManager.HasAnyPendingCallbackForTests);
        }

        // ── guard chain stage 6: single flight ──────────────────────────────────

        [UnityTest]
        public IEnumerator SecondCall_WhileOnePending_IsRejectedAsBusy()
        {
            MacClipboardManager manager = BridgedManager();

            MacClipboardReadResult? first = null;
            MacClipboardReadResult? second = null;
            manager.Read(null, r => first = r);
            manager.Read(null, r => second = r);

            yield return null;

            Assert.IsNull(first, "the first call is still awaiting its native result");
            Assert.AreEqual(MacClipboardErrorCodes.Busy, second!.Value.Error!.Value.Code);
            StringAssert.Contains("already in progress", second.Value.Error.Value.Message);

            MacClipboardManager.CompleteReadForTests(true, ReadJson);
            yield return null;

            Assert.IsTrue(first!.Value.IsSuccess, "the rejection left the pending call untouched");
        }

        [UnityTest]
        public IEnumerator BusyRejection_DoesNotStealThePendingCallback()
        {
            MacClipboardManager manager = BridgedManager();

            int firstCallbackCount = 0;
            manager.Read(null, _ => firstCallbackCount++);
            manager.Read(null, _ => { });
            manager.Read(null, _ => { });

            yield return null;

            Assert.AreEqual(0, firstCallbackCount);

            MacClipboardManager.CompleteReadForTests(true, ReadJson);
            yield return null;

            Assert.AreEqual(1, firstCallbackCount, "exactly the pending call's callback is invoked");
        }

        [UnityTest]
        public IEnumerator DifferentOperations_RunConcurrently()
        {
            MacClipboardManager manager = BridgedManager();

            manager.Read();
            manager.ReadData("public.data");
            manager.Clear();

            yield return null;

            Assert.AreEqual(3, MacClipboardManager.InFlightCountForTests);
        }

        // ── rejections own nothing ──────────────────────────────────────────────

        [UnityTest]
        public IEnumerator RejectedCalls_NeverTouchPendingOrInFlightState()
        {
            LogAssert.ignoreFailingMessages = true;
            MacClipboardManager manager = BridgedManager();

            int pendingCallbackCount = 0;
            manager.Read(null, _ => pendingCallbackCount++);

            // An argument rejection and an off-thread rejection, neither of which owns anything.
            manager.Copy(null!, null, null, _ => { });
            var worker = new Thread(() => manager.Read());
            worker.Start();
            worker.Join();

            yield return null;

            Assert.AreEqual(0, pendingCallbackCount);
            Assert.IsTrue(MacClipboardManager.IsInFlightForTests(MacClipboardOperations.Read));
            Assert.IsFalse(
                MacClipboardManager.IsInFlightForTests(MacClipboardOperations.Copy),
                "an argument rejection must not leave a marker behind");

            MacClipboardManager.CompleteReadForTests(true, ReadJson);
            yield return null;

            Assert.AreEqual(1, pendingCallbackCount);
        }

        // ── dispatch contract ───────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator CommonEventFiresBeforeThePerCallCallback()
        {
            MacClipboardManager manager = BridgedManager();

            var order = new List<string>();
            void OnCommon(MacClipboardReadResult _) => order.Add("common");
            manager.ReadCompleted += OnCommon;

            manager.Read(null, _ => order.Add("perCall"));
            MacClipboardManager.CompleteReadForTests(true, ReadJson);

            yield return null;

            manager.ReadCompleted -= OnCommon;
            Assert.AreEqual(new[] { "common", "perCall" }, order.ToArray());
        }

        [UnityTest]
        public IEnumerator OperationWithoutAPerCallCallback_StillFiresTheCommonEvent()
        {
            MacClipboardManager manager = BridgedManager();

            int commonCount = 0;
            void OnCommon(MacClipboardChangeCountResult _) => commonCount++;
            manager.ClearCompleted += OnCommon;

            manager.Clear();
            MacClipboardManager.CompleteClearForTests(true, ChangeCountJson);

            yield return null;

            manager.ClearCompleted -= OnCommon;
            Assert.AreEqual(1, commonCount);
        }

        [UnityTest]
        public IEnumerator ResultsAreNotDeliveredSynchronously()
        {
            MacClipboardManager manager = BridgedManager();

            bool delivered = false;
            manager.Clear(null, _ => delivered = true);
            MacClipboardManager.CompleteClearForTests(true, ChangeCountJson);

            Assert.IsFalse(delivered, "the result goes through the dispatcher, not the caller's frame");

            yield return null;

            Assert.IsTrue(delivered);
        }

        [UnityTest]
        public IEnumerator CallbackMayRestartTheSameOperation()
        {
            // The completion releases the in-flight marker, so a subscriber can start the same
            // operation again from inside its own callback. Only the release is observable here:
            // Dispatch enqueues rather than invoking, so its order against EndOperation cannot be
            // told apart from the outside.
            MacClipboardManager manager = BridgedManager();

            bool restartAccepted = false;
            manager.Read(null, _ =>
            {
                manager.Read(null, r => restartAccepted = r.IsSuccess);
                MacClipboardManager.CompleteReadForTests(true, ReadJson);
            });
            MacClipboardManager.CompleteReadForTests(true, ReadJson);

            yield return null;
            yield return null;

            Assert.IsTrue(restartAccepted, "the restart must not be rejected as busy");
        }

        // ── copy and append share a result type but not a slot ──────────────────

        [UnityTest]
        public IEnumerator CopyCompletion_InvokesOnlyTheCopyCallback()
        {
            MacClipboardManager manager = BridgedManager();

            int copyCount = 0;
            int appendCount = 0;
            manager.Copy(OneByte(), null, null, _ => copyCount++);
            manager.Append(OneByte(), GeneralOwnership(), _ => appendCount++);

            MacClipboardManager.CompleteOwnershipForTests(MacClipboardOperations.Copy, true, OwnershipJson);

            yield return null;

            Assert.AreEqual(1, copyCount);
            Assert.AreEqual(0, appendCount, "append's callback belongs to a different slot");
            Assert.IsFalse(MacClipboardManager.IsInFlightForTests(MacClipboardOperations.Copy));
            Assert.IsTrue(MacClipboardManager.IsInFlightForTests(MacClipboardOperations.Append));
        }

        [UnityTest]
        public IEnumerator AppendCompletion_InvokesOnlyTheAppendCallback()
        {
            // The mirror of the test above. Checking result.Operation alone would pass even if the
            // copy slot were hard-coded, so both directions are driven.
            MacClipboardManager manager = BridgedManager();

            int copyCount = 0;
            int appendCount = 0;
            manager.Copy(OneByte(), null, null, _ => copyCount++);
            manager.Append(OneByte(), GeneralOwnership(), _ => appendCount++);

            MacClipboardManager.CompleteOwnershipForTests(MacClipboardOperations.Append, true, OwnershipJson);

            yield return null;

            Assert.AreEqual(0, copyCount);
            Assert.AreEqual(1, appendCount);
            Assert.IsTrue(MacClipboardManager.IsInFlightForTests(MacClipboardOperations.Copy));
            Assert.IsFalse(MacClipboardManager.IsInFlightForTests(MacClipboardOperations.Append));
        }

        [UnityTest]
        public IEnumerator OwnershipResult_CarriesTheOperationThatProducedIt()
        {
            MacClipboardManager manager = BridgedManager();

            MacClipboardOwnershipResult? result = null;
            manager.Append(OneByte(), GeneralOwnership(), r => result = r);
            MacClipboardManager.CompleteOwnershipForTests(MacClipboardOperations.Append, true, OwnershipJson);

            yield return null;

            Assert.AreEqual(MacClipboardOperations.Append, result!.Value.Operation);
            Assert.AreEqual(12, result.Value.Ownership!.ChangeCount);
        }

        // ── native results ──────────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator NativeFailure_IsReportedWithTheNativeCode()
        {
            MacClipboardManager manager = BridgedManager();

            MacClipboardOwnershipResult? result = null;
            manager.Copy(OneByte(), null, null, r => result = r);
            MacClipboardManager.CompleteOwnershipForTests(
                MacClipboardOperations.Copy, false, null,
                MacClipboardErrorCodes.WriteRejected, "The clipboard rejected the write.");

            yield return null;

            Assert.AreEqual(MacClipboardErrorCodes.WriteRejected, result!.Value.Error!.Value.Code);
            Assert.IsFalse(MacClipboardManager.IsInFlightForTests(MacClipboardOperations.Copy));
            Assert.IsFalse(MacClipboardManager.HasAnyPendingCallbackForTests);
        }

        [UnityTest]
        public IEnumerator UnparsableSuccessPayload_FailsWithResponseParseFailed()
        {
            LogAssert.ignoreFailingMessages = true;
            MacClipboardManager manager = BridgedManager();

            MacClipboardReadResult? result = null;
            manager.Read(null, r => result = r);
            MacClipboardManager.CompleteReadForTests(true, "{\"changeCount\":3}");

            yield return null;

            Assert.AreEqual(MacClipboardErrorCodes.ResponseParseFailed, result!.Value.Error!.Value.Code);
            Assert.IsFalse(
                MacClipboardManager.IsInFlightForTests(MacClipboardOperations.Read),
                "a parse failure is still this call's own completion, so it releases the marker");
        }

        [UnityTest]
        public IEnumerator ReadData_WithNoDataForTheType_IsASuccess()
        {
            MacClipboardManager manager = BridgedManager();

            MacClipboardReadDataResult? result = null;
            manager.ReadData("public.png", null, r => result = r);
            MacClipboardManager.CompleteReadDataForTests(true, "{\"data\":null}");

            yield return null;

            Assert.IsTrue(result!.Value.IsSuccess, "a missing type is not an error");
            Assert.IsNull(result.Value.Data);
        }

        [UnityTest]
        public IEnumerator ReadData_WithData_DecodesThePayload()
        {
            MacClipboardManager manager = BridgedManager();

            MacClipboardReadDataResult? result = null;
            manager.ReadData("public.data", null, r => result = r);
            MacClipboardManager.CompleteReadDataForTests(true, ReadDataJson);

            yield return null;

            Assert.AreEqual(new byte[] { 0x68, 0x69 }, result!.Value.Data);
        }

        [UnityTest]
        public IEnumerator Read_DeliversTheDecodedRepresentations()
        {
            MacClipboardManager manager = BridgedManager();

            MacClipboardReadResult? result = null;
            manager.Read(null, r => result = r);
            MacClipboardManager.CompleteReadForTests(true, ReadJson);

            yield return null;

            Assert.AreEqual(3, result!.Value.Contents!.ChangeCount);
            Assert.AreEqual(1, result.Value.Contents.Items.Count);
            Assert.AreEqual(
                new byte[] { 0x68, 0x69 },
                result.Value.Contents.Items[0].Representations["public.utf8-plain-text"]);
        }

        [UnityTest]
        public IEnumerator Clear_DeliversTheNewChangeCount()
        {
            MacClipboardManager manager = BridgedManager();

            MacClipboardChangeCountResult? result = null;
            manager.Clear(null, r => result = r);
            MacClipboardManager.CompleteClearForTests(true, ChangeCountJson);

            yield return null;

            Assert.AreEqual(9, result!.Value.ChangeCount);
        }

        // ── tombstone ───────────────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator AfterDestroy_AllFiveOperationsAreRejected()
        {
            LogAssert.ignoreFailingMessages = true;
            MacClipboardManager manager = BridgedManager();

            UnityEngine.Object.DestroyImmediate(manager.gameObject);

            var codes = new List<int>();
            manager.Copy(OneByte(), null, null, r => codes.Add(r.Error!.Value.Code));
            manager.Append(OneByte(), GeneralOwnership(), r => codes.Add(r.Error!.Value.Code));
            manager.Read(null, r => codes.Add(r.Error!.Value.Code));
            manager.ReadData("public.data", null, r => codes.Add(r.Error!.Value.Code));
            manager.Clear(null, r => codes.Add(r.Error!.Value.Code));

            yield return null;

            Assert.AreEqual(5, codes.Count);
            foreach (int code in codes)
            {
                Assert.AreEqual(MacClipboardErrorCodes.ManagerDestroyed, code);
            }
            Assert.IsTrue(MacClipboardManager.IsTerminated);
        }

        [UnityTest]
        public IEnumerator AfterDestroy_TheRejectionIsRefusedBeforeArgumentsAreLookedAt()
        {
            LogAssert.ignoreFailingMessages = true;
            MacClipboardManager manager = BridgedManager();

            UnityEngine.Object.DestroyImmediate(manager.gameObject);

            MacClipboardOwnershipResult? result = null;
            manager.Copy(null!, null, null, r => result = r);

            yield return null;

            Assert.AreEqual(
                MacClipboardErrorCodes.ManagerDestroyed, result!.Value.Error!.Value.Code,
                "the tombstone is checked before the arguments");
        }

        [UnityTest]
        public IEnumerator AfterDestroy_ARetainedReferenceStillReceivesItsCommonEvent()
        {
            LogAssert.ignoreFailingMessages = true;
            MacClipboardManager manager = BridgedManager();

            var results = new List<MacClipboardReadResult>();
            void OnCommon(MacClipboardReadResult r) => results.Add(r);
            manager.ReadCompleted += OnCommon;

            UnityEngine.Object.DestroyImmediate(manager.gameObject);
            manager.Read();

            yield return null;

            manager.ReadCompleted -= OnCommon;
            Assert.AreEqual(1, results.Count, "the rejection goes to the instance the call was made on");
            Assert.AreEqual(MacClipboardErrorCodes.ManagerDestroyed, results[0].Error!.Value.Code);
        }

        [UnityTest]
        public IEnumerator AfterDestroy_AFreshInstanceRejectsButTheOldSubscriberHearsNothing()
        {
            LogAssert.ignoreFailingMessages = true;
            MacClipboardManager old = BridgedManager();

            int oldSubscriberCount = 0;
            void OnCommon(MacClipboardReadResult _) => oldSubscriberCount++;
            old.ReadCompleted += OnCommon;

            UnityEngine.Object.DestroyImmediate(old.gameObject);

            // Re-fetching Instance creates a different object with no subscribers. The per-call
            // callback is the only channel that still reports the rejection.
            MacClipboardManager revived = MacClipboardManager.Instance;
            Assert.AreNotSame(old, revived);

            MacClipboardReadResult? perCall = null;
            revived.Read(null, r => perCall = r);

            yield return null;

            old.ReadCompleted -= OnCommon;
            Assert.AreEqual(MacClipboardErrorCodes.ManagerDestroyed, perCall!.Value.Error!.Value.Code);
            Assert.AreEqual(0, oldSubscriberCount, "the revived instance has no subscribers");
        }

        [UnityTest]
        public IEnumerator LateNativeResult_AfterDestroy_IsDiscarded()
        {
            LogAssert.ignoreFailingMessages = true;
            MacClipboardManager manager = BridgedManager();

            bool delivered = false;
            manager.Read(null, _ => delivered = true);

            UnityEngine.Object.DestroyImmediate(manager.gameObject);
            MacClipboardManager.CompleteReadForTests(true, ReadJson);

            yield return null;

            Assert.IsFalse(delivered, "a callback from a destroyed lifetime has no live caller");
        }

        [UnityTest]
        public IEnumerator Destroy_ClearsEveryPendingSlotAndMarker()
        {
            LogAssert.ignoreFailingMessages = true;
            MacClipboardManager manager = BridgedManager();

            manager.Read(null, _ => { });
            manager.Clear(null, _ => { });

            UnityEngine.Object.DestroyImmediate(manager.gameObject);

            yield return null;

            Assert.IsFalse(MacClipboardManager.HasAnyPendingCallbackForTests);
            Assert.AreEqual(0, MacClipboardManager.InFlightCountForTests);
        }

        [UnityTest]
        public IEnumerator ResetForTests_ClearsTheTombstoneAndTheOverrides()
        {
            LogAssert.ignoreFailingMessages = true;
            MacClipboardManager manager = BridgedManager();
            MacClipboardManager.MaxRequestBytesOverrideForTests = 4;

            UnityEngine.Object.DestroyImmediate(manager.gameObject);
            Assert.IsTrue(MacClipboardManager.IsTerminated);

            MacClipboardManager.ResetForTests();

            Assert.IsFalse(MacClipboardManager.IsTerminated);
            Assert.IsFalse(MacClipboardManager.BridgeAvailableOverrideForTests);
            Assert.IsNull(MacClipboardManager.MaxRequestBytesOverrideForTests);
            yield return null;
        }

        [UnityTest]
        public IEnumerator InstanceLifecycle_CreateDestroyRecreate_DoesNotThrow()
        {
            LogAssert.ignoreFailingMessages = true;

            MacClipboardManager first = MacClipboardManager.Instance;
            Assert.DoesNotThrow(() => UnityEngine.Object.DestroyImmediate(first.gameObject));

            yield return null;

            MacClipboardManager second = MacClipboardManager.Instance;
            Assert.AreNotSame(first, second);
        }

        // ── dispatcher lifetime ─────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator DestroyedDispatcher_IsDetectedAndTheResultIsNotSilentlyQueued()
        {
            LogAssert.ignoreFailingMessages = true;

            MacClipboardManager manager = MacClipboardManager.Instance;

            // Destroying the dispatcher leaves a non-null managed wrapper behind. Only Unity's null
            // operator sees that it is dead; a plain reference check would enqueue into a queue
            // that Update will never flush again.
            GameObject? dispatcherObject = GameObject.Find("UnityMainThreadDispatcher");
            Assert.IsNotNull(dispatcherObject, "Awake creates the dispatcher");
            UnityEngine.Object.DestroyImmediate(dispatcherObject);

            bool delivered = false;
            manager.Clear(null, _ => delivered = true);

            yield return null;
            yield return null;

            Assert.IsFalse(delivered, "the result is reported as dropped rather than queued forever");
        }
    }
}
#endif
