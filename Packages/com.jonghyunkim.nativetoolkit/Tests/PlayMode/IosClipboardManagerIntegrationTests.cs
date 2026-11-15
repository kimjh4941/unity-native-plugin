#nullable enable

#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using JonghyunKim.NativeToolkit.Runtime.iOS.Clipboard;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// PlayMode integration tests for <c>IosClipboardManager</c>.
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
    /// </summary>
    public sealed class IosClipboardManagerIntegrationTests
    {
        [TearDown]
        public void TearDown()
        {
            // Destroy first, then reset: ResetForTests clears the captured main-thread id and
            // dispatcher, which only Awake re-establishes.
            DestroyManagerIfPresent();
            IosClipboardManager.ResetForTests();
            LogAssert.ignoreFailingMessages = false;
        }

        private static void DestroyManagerIfPresent()
        {
            foreach (IosClipboardManager manager in
                     UnityEngine.Object.FindObjectsByType<IosClipboardManager>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                UnityEngine.Object.DestroyImmediate(manager.gameObject);
            }
        }

        /// <summary>Creates the Manager and makes the guard chain run past the platform check.</summary>
        private static IosClipboardManager BridgedManager()
        {
            IosClipboardManager manager = IosClipboardManager.Instance;
            IosClipboardManager.BridgeAvailableOverrideForTests = true;
            return manager;
        }

        // ── bridge unavailable in the Editor (B-1) ──────────────────────────────

        [UnityTest]
        public IEnumerator EveryOperation_InEditor_FailsWithBridgeUnavailable()
        {
            var operationResults = new List<IosClipboardOperationResult>();
            IosClipboardManager manager = IosClipboardManager.Instance;
            manager.ClipboardOperationCompleted += operationResults.Add;

            var payloadErrors = new List<IosClipboardErrorInfo?>();
            InvokeAllPayloadOperations(manager, payloadErrors);
            InvokeAllOperationOnlyCalls(manager);

            yield return null;

            manager.ClipboardOperationCompleted -= operationResults.Add;

            Assert.AreEqual(7, operationResults.Count, "all seven payload-less operations report a result");
            foreach (IosClipboardOperationResult result in operationResults)
            {
                Assert.IsFalse(result.IsSuccess, result.Operation);
                Assert.AreEqual(
                    IosClipboardManager.BridgeUnavailableErrorCode, result.Error!.Value.Code, result.Operation);
                StringAssert.Contains("only available on an iOS device", result.Error.Value.Message);
            }

            Assert.AreEqual(8, payloadErrors.Count, "all eight payload operations report a result");
            foreach (IosClipboardErrorInfo? error in payloadErrors)
            {
                Assert.IsNotNull(error);
                Assert.AreEqual(IosClipboardManager.BridgeUnavailableErrorCode, error!.Value.Code);
            }
        }

        private static void InvokeAllOperationOnlyCalls(IosClipboardManager manager)
        {
            manager.Copy(IosClipboardContent.PlainText("x"));
            manager.Append(IosClipboardContent.PlainText("x"));
            manager.Clear();
            manager.RemovePasteboard(IosPasteboardScope.Named("group.a"));
            manager.CancelLoads();
            manager.StartObserving();
            manager.StopObserving();
        }

        private static void InvokeAllPayloadOperations(
            IosClipboardManager manager, List<IosClipboardErrorInfo?> errors)
        {
            manager.Read(null, r => errors.Add(r.Error));
            manager.ReadData("public.png", null, r => errors.Add(r.Error));
            manager.GetSnapshot(null, null, r => errors.Add(r.Error));
            manager.CreatePasteboard(IosPasteboardCreationRequest.Unique, r => errors.Add(r.Error));
            manager.DetectPatterns(new[] { IosClipboardDetectionPattern.Link }, null, r => errors.Add(r.Error));
            manager.DetectValues(new[] { IosClipboardDetectionPattern.Link }, null, r => errors.Add(r.Error));
            manager.LoadItem(IosClipboardLoadRequest.Text, null, r => errors.Add(r.Error));
            manager.CheckForegroundChange(null, r => errors.Add(r.Error));
        }

        // ── dispatch contract ───────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator Operation_CommonEventFiresBeforePerCallCallback()
        {
            var order = new List<string>();
            IosClipboardManager manager = IosClipboardManager.Instance;

            void OnCompleted(IosClipboardOperationResult r) => order.Add("common");
            manager.ClipboardOperationCompleted += OnCompleted;

            manager.Clear(null, _ => order.Add("perCall"));

            yield return null;

            manager.ClipboardOperationCompleted -= OnCompleted;
            Assert.AreEqual(new[] { "common", "perCall" }, order.ToArray());
        }

        [UnityTest]
        public IEnumerator Operation_WithoutPerCallCallback_StillFiresTheCommonEvent()
        {
            int commonCount = 0;
            IosClipboardManager manager = IosClipboardManager.Instance;

            void OnCompleted(IosClipboardOperationResult r) => commonCount++;
            manager.ClipboardOperationCompleted += OnCompleted;

            manager.Clear();

            yield return null;

            manager.ClipboardOperationCompleted -= OnCompleted;
            Assert.AreEqual(1, commonCount);
        }

        [UnityTest]
        public IEnumerator PayloadOperation_FiresItsOwnTypedEventBeforeThePerCallCallback()
        {
            var order = new List<string>();
            IosClipboardManager manager = IosClipboardManager.Instance;

            void OnRead(IosClipboardReadResult r) => order.Add("common");
            manager.ReadCompleted += OnRead;

            manager.Read(null, _ => order.Add("perCall"));

            yield return null;

            manager.ReadCompleted -= OnRead;
            Assert.AreEqual(new[] { "common", "perCall" }, order.ToArray());
        }

        [UnityTest]
        public IEnumerator ResultsAreNotDeliveredSynchronously()
        {
            bool delivered = false;
            IosClipboardManager.Instance.Clear(null, _ => delivered = true);

            Assert.IsFalse(delivered, "results always go through the main-thread dispatcher");

            yield return null;

            Assert.IsTrue(delivered);
        }

        // ── single-flight through the production state machine (B-0) ────────────

        [UnityTest]
        public IEnumerator SecondCall_WhileOnePending_IsRejectedAsBusy()
        {
            LogAssert.ignoreFailingMessages = true;
            IosClipboardManager manager = BridgedManager();

            IosClipboardReadResult? first = null;
            manager.Read(null, r => first = r);
            Assert.IsTrue(IosClipboardManager.IsInFlightForTests(IosClipboardManager.OperationRead));

            IosClipboardReadResult? second = null;
            manager.Read(null, r => second = r);

            yield return null;

            Assert.IsNotNull(second, "the rejected call receives its own result");
            Assert.AreEqual(IosClipboardManager.BusyErrorCode, second!.Value.Error!.Value.Code);
            Assert.IsNull(first, "the pending call must not be completed by the rejection");
            Assert.IsTrue(
                IosClipboardManager.IsInFlightForTests(IosClipboardManager.OperationRead),
                "a busy rejection must not release the in-flight marker it does not own");

            // The pending call's own result still finds its callback intact.
            IosClipboardManager.CompleteReadForTests(
                "{\"ok\":true,\"data\":{\"numberOfItems\":1,\"items\":[{\"text\":\"first\"}]}}");

            yield return null;

            Assert.IsNotNull(first);
            Assert.IsTrue(first!.Value.IsSuccess);
            Assert.AreEqual("first", first.Value.Items[0].Text);
            Assert.AreEqual(
                IosClipboardManager.BusyErrorCode, second.Value.Error!.Value.Code,
                "the rejected call never receives the pending call's payload");
            Assert.IsFalse(IosClipboardManager.IsInFlightForTests(IosClipboardManager.OperationRead));
        }

        [UnityTest]
        public IEnumerator BusyRejection_DoesNotStealThePendingCallbackEvenAfterRepeatedRejections()
        {
            LogAssert.ignoreFailingMessages = true;
            IosClipboardManager manager = BridgedManager();

            int firstCallbackCount = 0;
            manager.Read(null, _ => firstCallbackCount++);

            for (int i = 0; i < 3; i++)
            {
                manager.Read(null, _ => { });
            }

            yield return null;

            Assert.AreEqual(0, firstCallbackCount, "rejections must not complete the pending call");
            Assert.IsTrue(IosClipboardManager.IsInFlightForTests(IosClipboardManager.OperationRead));

            IosClipboardManager.CompleteReadForTests("{\"ok\":true,\"data\":{\"numberOfItems\":0,\"items\":[]}}");

            yield return null;

            Assert.AreEqual(1, firstCallbackCount, "the pending call completes exactly once");
        }

        [UnityTest]
        public IEnumerator BusyRejection_StillFiresTheCommonEvent()
        {
            LogAssert.ignoreFailingMessages = true;
            IosClipboardManager manager = BridgedManager();

            var results = new List<IosClipboardReadResult>();
            manager.ReadCompleted += results.Add;

            manager.Read();
            manager.Read();

            yield return null;

            manager.ReadCompleted -= results.Add;

            Assert.AreEqual(1, results.Count, "only the rejected call has produced a result so far");
            Assert.AreEqual(IosClipboardManager.BusyErrorCode, results[0].Error!.Value.Code);

            IosClipboardManager.CompleteReadForTests("{\"ok\":true,\"data\":{\"numberOfItems\":0,\"items\":[]}}");
            yield return null;
        }

        [UnityTest]
        public IEnumerator DifferentOperations_RunConcurrently()
        {
            IosClipboardManager manager = BridgedManager();

            manager.Read();
            manager.GetSnapshot();

            yield return null;

            Assert.IsTrue(IosClipboardManager.IsInFlightForTests(IosClipboardManager.OperationRead));
            Assert.IsTrue(IosClipboardManager.IsInFlightForTests("getSnapshot"));

            IosClipboardManager.CompleteReadForTests("{\"ok\":true,\"data\":{\"numberOfItems\":0,\"items\":[]}}");
            IosClipboardManager.CompleteSnapshotForTests(
                "{\"ok\":true,\"data\":{\"hasStrings\":false,\"hasURLs\":false,\"hasImages\":false,\"hasColors\":false," +
                "\"numberOfItems\":0,\"typeIdentifiers\":[],\"allTypeIdentifiers\":[]}}");
            yield return null;

            Assert.IsFalse(IosClipboardManager.IsInFlightForTests(IosClipboardManager.OperationRead));
            Assert.IsFalse(IosClipboardManager.IsInFlightForTests("getSnapshot"));
        }

        [UnityTest]
        public IEnumerator RejectedCalls_NeverTouchPendingOrInFlightState()
        {
            LogAssert.ignoreFailingMessages = true;
            IosClipboardManager manager = BridgedManager();

            int pendingCallbackCount = 0;
            manager.Read(null, _ => pendingCallbackCount++);

            // B-3 (invalid argument) and B-9 (off-thread) both reject before owning anything.
            manager.ReadData(null!, null, _ => { });
            var worker = new Thread(() => manager.Read());
            worker.Start();
            worker.Join();

            yield return null;

            Assert.AreEqual(0, pendingCallbackCount);
            Assert.IsTrue(IosClipboardManager.IsInFlightForTests(IosClipboardManager.OperationRead));
            Assert.IsFalse(
                IosClipboardManager.IsInFlightForTests("readData"),
                "an argument rejection must not leave a marker behind");

            IosClipboardManager.CompleteReadForTests("{\"ok\":true,\"data\":{\"numberOfItems\":0,\"items\":[]}}");
            yield return null;

            Assert.AreEqual(1, pendingCallbackCount);
        }

        [UnityTest]
        public IEnumerator CallbackMayRestartTheSameOperation()
        {
            IosClipboardManager manager = BridgedManager();

            bool restarted = false;
            manager.Read(null, _ =>
            {
                manager.Read(null, __ => { });
                restarted = IosClipboardManager.IsInFlightForTests(IosClipboardManager.OperationRead);
            });

            IosClipboardManager.CompleteReadForTests("{\"ok\":true,\"data\":{\"numberOfItems\":0,\"items\":[]}}");

            yield return null;

            Assert.IsTrue(restarted, "the marker is released before dispatch, so a restart is not busy");

            IosClipboardManager.CompleteReadForTests("{\"ok\":true,\"data\":{\"numberOfItems\":0,\"items\":[]}}");
            yield return null;
        }

        // ── argument validation (B-3 / B-4) ─────────────────────────────────────

        [UnityTest]
        public IEnumerator NullContent_FailsWithInvalidRequest()
        {
            LogAssert.ignoreFailingMessages = true;

            IosClipboardOperationResult? result = null;
            IosClipboardManager.Instance.Copy(null!, null, null, r => result = r);

            yield return null;

            Assert.IsNotNull(result);
            Assert.IsFalse(result!.Value.IsSuccess);
            Assert.AreEqual("CLIPBOARD_INVALID_REQUEST", result.Value.Error!.Value.Code);
        }

        [UnityTest]
        public IEnumerator EmptyPatterns_FailWithTheNativeEmptyPatternsContract()
        {
            LogAssert.ignoreFailingMessages = true;

            IosClipboardDetectedPatternsResult? result = null;
            IosClipboardManager.Instance.DetectPatterns(
                Array.Empty<IosClipboardDetectionPattern>(), null, r => result = r);

            yield return null;

            Assert.IsNotNull(result);
            Assert.IsFalse(result!.Value.IsSuccess);
            // Same code and wording the native layer would return, one round trip earlier.
            Assert.AreEqual("CLIPBOARD_EMPTY_PATTERNS", result.Value.Error!.Value.Code);
            Assert.AreEqual("No detection patterns were specified.", result.Value.Error.Value.Message);
        }

        // ── observation ─────────────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator StartObserving_ReportsItsOwnOperationNameOverTheSharedKey()
        {
            IosClipboardManager manager = BridgedManager();

            var results = new List<IosClipboardOperationResult>();
            manager.ClipboardOperationCompleted += results.Add;

            manager.StartObserving();
            Assert.IsTrue(IosClipboardManager.IsInFlightForTests(IosClipboardManager.ObservationControlKey));
            Assert.IsFalse(IosClipboardManager.IsInFlightForTests(IosClipboardManager.OperationStartObserving));

            IosClipboardManager.CompleteObservationControlForTests(
                IosClipboardManager.OperationStartObserving, isSuccess: true);

            yield return null;

            manager.ClipboardOperationCompleted -= results.Add;
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(IosClipboardManager.OperationStartObserving, results[0].Operation);
            Assert.IsTrue(results[0].IsSuccess);
        }

        [UnityTest]
        public IEnumerator StopObserving_WhileStartPending_IsBusyOnTheSharedKey()
        {
            LogAssert.ignoreFailingMessages = true;
            IosClipboardManager manager = BridgedManager();

            manager.StartObserving();

            IosClipboardOperationResult? stopResult = null;
            manager.StopObserving(r => stopResult = r);

            yield return null;

            Assert.IsNotNull(stopResult);
            Assert.AreEqual(IosClipboardManager.BusyErrorCode, stopResult!.Value.Error!.Value.Code);
            Assert.AreEqual(IosClipboardManager.OperationStopObserving, stopResult.Value.Operation);

            IosClipboardManager.CompleteObservationControlForTests(
                IosClipboardManager.OperationStartObserving, isSuccess: true);
            yield return null;
        }

        [UnityTest]
        public IEnumerator FailedStart_ReleasesItsOwnChangeRegistration()
        {
            LogAssert.ignoreFailingMessages = true;
            IosClipboardManager manager = BridgedManager();

            manager.StartObserving(null, _ => { });
            Assert.IsTrue(IosClipboardManager.HasChangeRegistrationForTests);
            Assert.AreNotEqual(0UL, IosClipboardManager.PendingObservationGenerationForTests);

            IosClipboardManager.CompleteObservationControlForTests(
                IosClipboardManager.OperationStartObserving, isSuccess: false,
                errorCode: "CLIPBOARD_UNAVAILABLE", errorMessage: "The requested pasteboard is unavailable.");

            yield return null;

            Assert.IsFalse(IosClipboardManager.HasChangeRegistrationForTests);
            Assert.AreEqual(0UL, IosClipboardManager.PendingObservationGenerationForTests,
                "the pending generation is cleared on every completion path");
        }

        [UnityTest]
        public IEnumerator SuccessfulStart_KeepsItsChangeRegistration()
        {
            IosClipboardManager manager = BridgedManager();

            manager.StartObserving(null, _ => { });
            IosClipboardManager.CompleteObservationControlForTests(
                IosClipboardManager.OperationStartObserving, isSuccess: true);

            yield return null;

            Assert.IsTrue(IosClipboardManager.HasChangeRegistrationForTests);
            Assert.AreEqual(0UL, IosClipboardManager.PendingObservationGenerationForTests);
        }

        [UnityTest]
        public IEnumerator StopObserving_ReleasesTheChangeRegistration()
        {
            IosClipboardManager manager = BridgedManager();

            manager.StartObserving(null, _ => { });
            IosClipboardManager.CompleteObservationControlForTests(
                IosClipboardManager.OperationStartObserving, isSuccess: true);
            yield return null;

            manager.StopObserving();
            IosClipboardManager.CompleteObservationControlForTests(
                IosClipboardManager.OperationStopObserving, isSuccess: true);
            yield return null;

            Assert.IsFalse(IosClipboardManager.HasChangeRegistrationForTests);
        }

        [UnityTest]
        public IEnumerator OlderStopCompletion_DoesNotClearANewerRegistration()
        {
            // The same delegate instance is registered twice on purpose: reference equality could
            // not tell the two registrations apart, which is why a generation counter is used.
            IosClipboardManager manager = BridgedManager();
            Action<IosClipboardChangeEvent> shared = _ => { };

            manager.StartObserving(null, shared);
            IosClipboardManager.CompleteObservationControlForTests(
                IosClipboardManager.OperationStartObserving, isSuccess: true);
            yield return null;

            manager.StopObserving();
            manager.StartObserving(null, shared);   // busy: the stop still owns the shared key
            yield return null;

            IosClipboardManager.CompleteObservationControlForTests(
                IosClipboardManager.OperationStopObserving, isSuccess: true);
            yield return null;

            // A fresh start after the stop completed must survive.
            manager.StartObserving(null, shared);
            IosClipboardManager.CompleteObservationControlForTests(
                IosClipboardManager.OperationStartObserving, isSuccess: true);
            yield return null;

            Assert.IsTrue(IosClipboardManager.HasChangeRegistrationForTests);
        }

        [UnityTest]
        public IEnumerator ChangeEvents_ReachTheCommonEventAndTheRegistration()
        {
            IosClipboardManager manager = BridgedManager();

            var order = new List<string>();
            void OnChanged(IosClipboardChangeEvent e) => order.Add("common");
            manager.ClipboardChanged += OnChanged;

            manager.StartObserving(null, _ => order.Add("perRegistration"));
            IosClipboardManager.CompleteObservationControlForTests(
                IosClipboardManager.OperationStartObserving, isSuccess: true);
            yield return null;

            IosClipboardManager.DeliverChangeEventForTests(
                "{\"scope\":{\"kind\":\"general\"},\"kind\":\"changed\",\"typesAdded\":[\"a\"]}");

            yield return null;

            manager.ClipboardChanged -= OnChanged;
            Assert.AreEqual(new[] { "common", "perRegistration" }, order.ToArray());
        }

        [UnityTest]
        public IEnumerator UnparsableChangeEvent_IsDropped()
        {
            LogAssert.ignoreFailingMessages = true;
            IosClipboardManager manager = BridgedManager();

            int changedCount = 0;
            void OnChanged(IosClipboardChangeEvent e) => changedCount++;
            manager.ClipboardChanged += OnChanged;

            manager.StartObserving(null, _ => changedCount++);
            IosClipboardManager.CompleteObservationControlForTests(
                IosClipboardManager.OperationStartObserving, isSuccess: true);
            yield return null;

            IosClipboardManager.DeliverChangeEventForTests("not json");
            IosClipboardManager.DeliverChangeEventForTests("{\"scope\":{\"kind\":\"general\"}}");

            yield return null;
            yield return null;

            manager.ClipboardChanged -= OnChanged;
            Assert.AreEqual(0, changedCount);
        }

        [UnityTest]
        public IEnumerator Observation_InEditorWithoutBridge_NeverRaisesClipboardChanged()
        {
            int changedCount = 0;
            IosClipboardManager manager = IosClipboardManager.Instance;

            void OnChanged(IosClipboardChangeEvent e) => changedCount++;
            manager.ClipboardChanged += OnChanged;

            manager.StartObserving(null, _ => changedCount++);

            yield return null;
            yield return null;

            manager.ClipboardChanged -= OnChanged;
            Assert.AreEqual(0, changedCount);
        }

        // ── main-thread contract (B-9) ──────────────────────────────────────────

        [UnityTest]
        public IEnumerator OffThreadCall_OnACachedInstance_IsRejected()
        {
            LogAssert.ignoreFailingMessages = true;

            // The reference is taken on the main thread: the Instance getter itself is main-thread
            // only and cannot be guarded, so only instance methods are covered by B-9.
            IosClipboardManager manager = IosClipboardManager.Instance;

            IosClipboardReadResult? result = null;
            var worker = new Thread(() => manager.Read(null, r => result = r));
            worker.Start();
            worker.Join();

            yield return null;

            Assert.IsNotNull(result, "the rejection is still delivered on the main thread");
            Assert.IsFalse(result!.Value.IsSuccess);
            Assert.AreEqual(IosClipboardManager.MainThreadRequiredErrorCode, result.Value.Error!.Value.Code);
            StringAssert.Contains("main thread", result.Value.Error.Value.Message);
        }

        [UnityTest]
        public IEnumerator OffThreadCall_DoesNotPolluteTheInFlightState()
        {
            LogAssert.ignoreFailingMessages = true;

            IosClipboardManager manager = BridgedManager();

            var worker = new Thread(() => manager.Read());
            worker.Start();
            worker.Join();

            yield return null;

            Assert.IsFalse(IosClipboardManager.IsInFlightForTests(IosClipboardManager.OperationRead));

            // If the rejected off-thread call had taken the marker, this would report busy.
            IosClipboardReadResult? result = null;
            manager.Read(null, r => result = r);
            IosClipboardManager.CompleteReadForTests("{\"ok\":true,\"data\":{\"numberOfItems\":0,\"items\":[]}}");

            yield return null;

            Assert.IsNotNull(result);
            Assert.IsTrue(result!.Value.IsSuccess);
        }

        // ── lifetime contract (B-11) ────────────────────────────────────────────

        [UnityTest]
        public IEnumerator AfterDestroy_AllFifteenOperationsAreRejected()
        {
            LogAssert.ignoreFailingMessages = true;

            IosClipboardManager manager = IosClipboardManager.Instance;
            Assert.IsFalse(IosClipboardManager.IsTerminated);

            UnityEngine.Object.DestroyImmediate(manager.gameObject);
            Assert.IsTrue(IosClipboardManager.IsTerminated);

            IosClipboardManager recreated = IosClipboardManager.Instance;
            Assert.IsTrue(IosClipboardManager.IsTerminated, "recreating must not clear the tombstone");

            var operationResults = new List<IosClipboardOperationResult>();
            recreated.ClipboardOperationCompleted += operationResults.Add;

            var payloadErrors = new List<IosClipboardErrorInfo?>();
            InvokeAllPayloadOperations(recreated, payloadErrors);
            InvokeAllOperationOnlyCalls(recreated);

            yield return null;

            recreated.ClipboardOperationCompleted -= operationResults.Add;

            Assert.AreEqual(7, operationResults.Count);
            foreach (IosClipboardOperationResult result in operationResults)
            {
                Assert.AreEqual(
                    IosClipboardManager.ManagerDestroyedErrorCode, result.Error!.Value.Code, result.Operation);
                StringAssert.Contains("has been destroyed", result.Error.Value.Message);
            }

            Assert.AreEqual(8, payloadErrors.Count);
            foreach (IosClipboardErrorInfo? error in payloadErrors)
            {
                Assert.AreEqual(IosClipboardManager.ManagerDestroyedErrorCode, error!.Value.Code);
            }
        }

        [UnityTest]
        public IEnumerator LateNativeResult_AfterDestroy_IsDiscarded()
        {
            LogAssert.ignoreFailingMessages = true;
            IosClipboardManager manager = BridgedManager();

            // Assertions are made outside the callback: an assertion thrown inside would be caught
            // by the production InvokeInOrder and never surface as a test failure.
            int deliveredCount = 0;
            manager.Read(null, _ => deliveredCount++);
            Assert.IsTrue(IosClipboardManager.IsInFlightForTests(IosClipboardManager.OperationRead));

            UnityEngine.Object.DestroyImmediate(manager.gameObject);

            IosClipboardManager.CompleteReadForTests("{\"ok\":true,\"data\":{\"numberOfItems\":0,\"items\":[]}}");

            yield return null;
            yield return null;

            Assert.AreEqual(0, deliveredCount, "a callback from a destroyed lifetime is discarded");
            Assert.IsTrue(IosClipboardManager.IsTerminated);
            Assert.AreEqual(0, IosClipboardManager.InFlightCountForTests);
            Assert.IsFalse(IosClipboardManager.HasAnyPendingCallbackForTests);
        }

        [UnityTest]
        public IEnumerator LateChangeEvent_AfterDestroy_IsDiscarded()
        {
            LogAssert.ignoreFailingMessages = true;
            IosClipboardManager manager = BridgedManager();

            int changedCount = 0;
            void OnChanged(IosClipboardChangeEvent e) => changedCount++;
            manager.ClipboardChanged += OnChanged;

            manager.StartObserving(null, _ => changedCount++);
            IosClipboardManager.CompleteObservationControlForTests(
                IosClipboardManager.OperationStartObserving, isSuccess: true);
            yield return null;

            UnityEngine.Object.DestroyImmediate(manager.gameObject);

            IosClipboardManager.DeliverChangeEventForTests(
                "{\"scope\":{\"kind\":\"general\"},\"kind\":\"changed\"}");

            yield return null;
            yield return null;

            manager.ClipboardChanged -= OnChanged;
            Assert.AreEqual(0, changedCount);
        }

        /// <summary>
        /// The composite scenario the lifetime tombstone exists to prevent: an old call's result
        /// arriving after the Manager was recreated must never reach a newly started call.
        /// </summary>
        [UnityTest]
        public IEnumerator OldResult_CannotReachACallStartedAfterRecreation()
        {
            LogAssert.ignoreFailingMessages = true;
            IosClipboardManager manager = BridgedManager();

            int oldCallbackCount = 0;
            manager.Read(null, _ => oldCallbackCount++);

            UnityEngine.Object.DestroyImmediate(manager.gameObject);

            IosClipboardManager recreated = IosClipboardManager.Instance;
            IosClipboardReadResult? newResult = null;
            recreated.Read(null, r => newResult = r);

            yield return null;

            Assert.IsNotNull(newResult, "the new call is rejected outright");
            Assert.AreEqual(IosClipboardManager.ManagerDestroyedErrorCode, newResult!.Value.Error!.Value.Code);

            // The old native result now arrives.
            IosClipboardManager.CompleteReadForTests(
                "{\"ok\":true,\"data\":{\"numberOfItems\":1,\"items\":[{\"text\":\"stale\"}]}}");

            yield return null;
            yield return null;

            Assert.AreEqual(0, oldCallbackCount, "the old callback was dropped at destruction");
            Assert.AreEqual(
                IosClipboardManager.ManagerDestroyedErrorCode, newResult.Value.Error!.Value.Code,
                "the new call never receives the stale payload");
        }

        [UnityTest]
        public IEnumerator ResetForTests_ClearsEveryMutableStatic()
        {
            LogAssert.ignoreFailingMessages = true;
            IosClipboardManager manager = BridgedManager();

            manager.Read(null, _ => { });
            manager.StartObserving(null, _ => { });
            manager.Copy(IosClipboardContent.PlainText("x"));
            IosClipboardManager.RejectAtEntryForTests(IosClipboardManager.OperationCopy, EmptyItems);
            IosClipboardManager.NativeCallThrowsForTests = true;
            Assert.AreEqual(1, IosClipboardManager.LaterCountForTests);
            Assert.IsTrue(IosClipboardManager.HasAnyPendingCallbackForTests);
            Assert.Greater(IosClipboardManager.InFlightCountForTests, 0);

            UnityEngine.Object.DestroyImmediate(manager.gameObject);
            Assert.IsTrue(IosClipboardManager.IsTerminated);

            IosClipboardManager.ResetForTests();

            Assert.IsFalse(IosClipboardManager.IsTerminated);
            Assert.IsFalse(IosClipboardManager.HasAnyPendingCallbackForTests);
            Assert.AreEqual(0, IosClipboardManager.InFlightCountForTests);
            Assert.IsFalse(IosClipboardManager.HasChangeRegistrationForTests);
            Assert.AreEqual(0UL, IosClipboardManager.PendingObservationGenerationForTests);
            Assert.IsFalse(IosClipboardManager.BridgeAvailableOverrideForTests);
            Assert.IsFalse(IosClipboardManager.NativeCallThrowsForTests);
            Assert.AreEqual(0, IosClipboardManager.LaterCountForTests);
            Assert.IsFalse(IosClipboardManager.IsObservingForTests);

            // Awake re-establishes the captured main-thread id and dispatcher that reset cleared.
            IosClipboardOperationResult? result = null;
            IosClipboardManager.Instance.Clear(null, r => result = r);

            yield return null;

            Assert.IsNotNull(result);
            Assert.AreEqual(IosClipboardManager.BridgeUnavailableErrorCode, result!.Value.Error!.Value.Code);
        }

        [UnityTest]
        public IEnumerator InstanceLifecycle_CreateDestroyRecreate_DoesNotThrow()
        {
            LogAssert.ignoreFailingMessages = true;

            IosClipboardManager first = IosClipboardManager.Instance;
            Assert.IsNotNull(first);

            Assert.DoesNotThrow(() => UnityEngine.Object.DestroyImmediate(first.gameObject));

            yield return null;

            IosClipboardManager second = IosClipboardManager.Instance;
            Assert.IsNotNull(second);
            Assert.AreNotSame(first, second);
        }

        // ── dispatcher lifetime ─────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator DestroyedDispatcher_IsDetectedAndTheResultIsNotSilentlyQueued()
        {
            LogAssert.ignoreFailingMessages = true;

            IosClipboardManager manager = IosClipboardManager.Instance;

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

        // ── C ABI 2.0.0: results decided during the call (design v3, D-4, D-12) ──

        private const int EmptyItems = 7;      // NTK_CLIPBOARD_ERROR_EMPTY_ITEMS
        private const int InvalidParameter = 1; // NTK_CLIPBOARD_ERROR_INVALID_PARAMETER

        [UnityTest]
        public IEnumerator RejectedAtEntry_KeepsTheMarkUntilALaterFrame()
        {
            IosClipboardManager manager = BridgedManager();
            var results = new List<IosClipboardOperationResult>();

            manager.Copy(IosClipboardContent.PlainText("x"), null, null, results.Add);
            IosClipboardManager.RejectAtEntryForTests(IosClipboardManager.OperationCopy, EmptyItems);
            manager.Copy(IosClipboardContent.PlainText("y"), null, null, results.Add);

            // The gate itself: nothing is handed over in the frame of the call, and it is in the next.
            Assert.AreEqual(1, IosClipboardManager.LaterCountForTests);
            IosClipboardManager.DeliverLater(Time.frameCount);
            Assert.AreEqual(1, IosClipboardManager.LaterCountForTests, "nothing is handed over in the frame of the call");
            Assert.AreEqual(0, results.Count);
            Assert.IsTrue(IosClipboardManager.IsInFlightForTests(IosClipboardManager.OperationCopy));
            IosClipboardManager.DeliverLater(Time.frameCount + 1);
            Assert.AreEqual(0, IosClipboardManager.LaterCountForTests, "handed over once the frame has passed");

            yield return null;
            yield return null;

            Assert.AreEqual(2, results.Count);
            Assert.IsTrue(results.Exists(r => r.Error?.Code == IosClipboardManager.BusyErrorCode), "the second call is busy");
            IosClipboardOperationResult rejected = results.Find(r => r.Error?.Code == "CLIPBOARD_EMPTY_ITEMS");
            Assert.AreEqual("No items provided for clipboard copy.", rejected.Error?.Message);
            Assert.IsNull(rejected.Error?.Domain);
            Assert.IsNull(rejected.Error?.NativeCode);
            Assert.IsFalse(IosClipboardManager.IsInFlightForTests(IosClipboardManager.OperationCopy));
        }

        [UnityTest]
        public IEnumerator RejectedAtEntry_CalledAgainFromItsCallback_WaitsForAnotherFrame()
        {
            IosClipboardManager manager = BridgedManager();
            var frames = new List<int>();

            void OnResult(IosClipboardOperationResult r)
            {
                frames.Add(Time.frameCount);
                if (frames.Count >= 3) return;
                manager.Copy(IosClipboardContent.PlainText("again"), null, null, OnResult);
                IosClipboardManager.RejectAtEntryForTests(IosClipboardManager.OperationCopy, EmptyItems);
            }

            manager.Copy(IosClipboardContent.PlainText("x"), null, null, OnResult);
            IosClipboardManager.RejectAtEntryForTests(IosClipboardManager.OperationCopy, EmptyItems);

            for (int i = 0; i < 10 && frames.Count < 3; i++) yield return null;

            Assert.AreEqual(3, frames.Count);
            Assert.Less(frames[0], frames[1], "a call from the callback is not delivered in the same frame");
            Assert.Less(frames[1], frames[2]);
        }

        [UnityTest]
        public IEnumerator NativeResult_BehindAResultDecidedDuringACall_KeepsTheOrder()
        {
            IosClipboardManager manager = BridgedManager();
            var order = new List<string>();

            manager.Copy(IosClipboardContent.PlainText("x"), null, null, _ => order.Add("copy"));
            manager.Append(IosClipboardContent.PlainText("y"), null, _ => order.Add("append"));
            IosClipboardManager.RejectAtEntryForTests(IosClipboardManager.OperationCopy, EmptyItems);
            IosClipboardManager.CompleteOperationForTests(IosClipboardManager.OperationAppend, isSuccess: true);

            yield return null;
            yield return null;

            Assert.AreEqual(new[] { "copy", "append" }, order.ToArray());
        }

        [UnityTest]
        public IEnumerator ChangeQueuedBefore_IsDeliveredBeforeALaterDecidedResult()
        {
            // Design v4, D-12 (Codex R4-1): a result from the list joins the dispatcher behind what is there.
            IosClipboardManager manager = BridgedManager();
            var order = new List<string>();
            void OnChanged(IosClipboardChangeEvent e) => order.Add("change");
            manager.ClipboardChanged += OnChanged;

            manager.StartObserving(null, null);
            IosClipboardManager.CompleteObservationControlForTests(IosClipboardManager.OperationStartObserving, isSuccess: true);
            yield return null;
            yield return null;

            IosClipboardManager.DeliverChangeEventForTests(ChangeJson);
            manager.Copy(IosClipboardContent.PlainText("x"), null, null, _ => order.Add("copy"));
            manager.Append(IosClipboardContent.PlainText("y"), null, _ => order.Add("append"));
            IosClipboardManager.RejectAtEntryForTests(IosClipboardManager.OperationCopy, EmptyItems);
            IosClipboardManager.CompleteOperationForTests(IosClipboardManager.OperationAppend, isSuccess: true);
            // Hand over now, while the change still waits in the dispatcher: the order then does not depend on
            // which Update runs first.
            IosClipboardManager.DeliverLater(Time.frameCount + 1);

            for (int i = 0; i < 4; i++) yield return null;

            manager.ClipboardChanged -= OnChanged;
            Assert.AreEqual(new[] { "change", "copy", "append" }, order.ToArray());
        }

        [UnityTest]
        public IEnumerator DisabledManager_StillDeliversAndReleasesTheMark()
        {
            // Design v4, D-12 (Codex R4-2): the pump does not depend on the Manager being enabled.
            IosClipboardManager manager = BridgedManager();
            manager.enabled = false;
            IosClipboardOperationResult? result = null;

            manager.Copy(IosClipboardContent.PlainText("x"), null, null, r => result = r);
            IosClipboardManager.RejectAtEntryForTests(IosClipboardManager.OperationCopy, EmptyItems);
            for (int i = 0; i < 3; i++) yield return null;

            manager.enabled = true;
            Assert.AreEqual("CLIPBOARD_EMPTY_ITEMS", result?.Error?.Code);
            Assert.IsFalse(IosClipboardManager.IsInFlightForTests(IosClipboardManager.OperationCopy));
        }

        [UnityTest]
        public IEnumerator RejectedStart_ItsRegistrationGetsNoChangeAfterTheFailure()
        {
            // Design v4, D-12 (subagent R-2): the registration is checked when the change is delivered.
            IosClipboardManager manager = BridgedManager();
            var order = new List<string>();
            void OnChanged(IosClipboardChangeEvent e) => order.Add("common");
            manager.ClipboardChanged += OnChanged;

            manager.StartObserving(null, _ => order.Add("a"));
            IosClipboardManager.CompleteObservationControlForTests(IosClipboardManager.OperationStartObserving, isSuccess: true);
            yield return null;
            yield return null;

            manager.StartObserving(null, _ => order.Add("b"), r => order.Add("start:" + r.Error?.Code));
            IosClipboardManager.RejectAtEntryForTests(IosClipboardManager.OperationStartObserving, InvalidParameter);
            IosClipboardManager.DeliverChangeEventForTests(ChangeJson);
            for (int i = 0; i < 4; i++) yield return null;

            manager.ClipboardChanged -= OnChanged;
            Assert.AreEqual(new[] { "start:CLIPBOARD_INVALID_REQUEST", "common" }, order.ToArray());
        }

        [UnityTest]
        public IEnumerator Destroy_DropsDecidedResultsAndHandsOverNativeOnes()
        {
            LogAssert.ignoreFailingMessages = true;
            IosClipboardManager manager = BridgedManager();
            var order = new List<string>();

            manager.Copy(IosClipboardContent.PlainText("x"), null, null, _ => order.Add("copy"));
            manager.Append(IosClipboardContent.PlainText("y"), null, _ => order.Add("append"));
            IosClipboardManager.RejectAtEntryForTests(IosClipboardManager.OperationCopy, EmptyItems);
            IosClipboardManager.CompleteOperationForTests(IosClipboardManager.OperationAppend, isSuccess: true);
            UnityEngine.Object.DestroyImmediate(manager.gameObject);

            for (int i = 0; i < 3; i++) yield return null;

            Assert.AreEqual(new[] { "append" }, order.ToArray());
        }

        [UnityTest]
        public IEnumerator NulInAnImagePath_FailsAsAnInvalidRequest()
        {
            IosClipboardManager manager = BridgedManager();
            IosClipboardErrorInfo? error = null;

            manager.Copy(IosClipboardContent.ImageFile("/tmp/ntk.png\0.tmp"), null, null, r => error = r.Error);
            yield return null;
            yield return null;

            Assert.AreEqual("CLIPBOARD_INVALID_REQUEST", error?.Code);
        }

        [UnityTest]
        public IEnumerator ChangeBeforeTheStopResult_ReachesTheRegistration()
        {
            // Implementation review v1 (Codex R-1, subagent R-1): as in 1.x, a change that arrived while a
            // registration was current reaches it, even when the stop result comes before it is delivered.
            IosClipboardManager manager = BridgedManager();
            int common = 0, perRegistration = 0;
            void OnChanged(IosClipboardChangeEvent e) => common++;
            manager.ClipboardChanged += OnChanged;

            manager.StartObserving(null, _ => perRegistration++);
            IosClipboardManager.CompleteObservationControlForTests(IosClipboardManager.OperationStartObserving, isSuccess: true);
            yield return null;
            yield return null;

            IosClipboardManager.DeliverChangeEventForTests(ChangeJson);
            manager.StopObserving();
            IosClipboardManager.CompleteObservationControlForTests(IosClipboardManager.OperationStopObserving, isSuccess: true);
            yield return null;
            yield return null;

            manager.ClipboardChanged -= OnChanged;
            Assert.AreEqual(1, common);
            Assert.AreEqual(1, perRegistration);
        }

        [UnityTest]
        public IEnumerator ChangeBeforeANewStart_ReachesTheRegistrationItArrivedFor()
        {
            IosClipboardManager manager = BridgedManager();
            int first = 0, second = 0;

            manager.StartObserving(null, _ => first++);
            IosClipboardManager.CompleteObservationControlForTests(IosClipboardManager.OperationStartObserving, isSuccess: true);
            yield return null;
            yield return null;

            IosClipboardManager.DeliverChangeEventForTests(ChangeJson);
            manager.StartObserving(null, _ => second++);
            yield return null;
            yield return null;

            Assert.AreEqual(1, first);
            Assert.AreEqual(0, second);
        }

        [UnityTest]
        public IEnumerator NativeCallThrowing_FailsOnALaterFrameAndStaysBusyUntilThen()
        {
            // Design v5, D-12: a native call that throws is a result decided during the call.
            LogAssert.ignoreFailingMessages = true;
            IosClipboardManager manager = BridgedManager();
            IosClipboardManager.NativeCallThrowsForTests = true;
            var results = new List<IosClipboardOperationResult>();
            int callFrame = Time.frameCount, resultFrame = -1;

            manager.Copy(IosClipboardContent.PlainText("x"), null, null, r => { results.Add(r); resultFrame = Time.frameCount; });
            manager.Copy(IosClipboardContent.PlainText("y"), null, null, results.Add);
            Assert.AreEqual(0, results.Count, "nothing inside the call");
            Assert.AreEqual(1, IosClipboardManager.LaterCountForTests);

            for (int i = 0; i < 3; i++) yield return null;

            Assert.AreEqual(2, results.Count);
            Assert.IsTrue(results.Exists(r => r.Error?.Code == IosClipboardManager.BusyErrorCode), "busy until delivered");
            IosClipboardOperationResult failed = results.Find(r => r.Error?.Code == IosClipboardManager.BridgeUnavailableErrorCode);
            Assert.AreEqual("copy could not be started.", failed.Error?.Message);
            Assert.Greater(resultFrame, callFrame);
            Assert.IsFalse(IosClipboardManager.IsInFlightForTests(IosClipboardManager.OperationCopy));
        }

        [UnityTest]
        public IEnumerator NativeCallThrowing_StartGivesItsRegistrationUpWhenDelivered()
        {
            LogAssert.ignoreFailingMessages = true;
            IosClipboardManager manager = BridgedManager();
            IosClipboardManager.NativeCallThrowsForTests = true;
            IosClipboardOperationResult? started = null;

            manager.StartObserving(null, _ => { }, r => started = r);
            Assert.IsTrue(IosClipboardManager.HasChangeRegistrationForTests, "kept until the failure is delivered");

            for (int i = 0; i < 3; i++) yield return null;

            Assert.AreEqual(IosClipboardManager.BridgeUnavailableErrorCode, started?.Error?.Code);
            Assert.IsFalse(IosClipboardManager.HasChangeRegistrationForTests);
            Assert.IsFalse(IosClipboardManager.IsInFlightForTests(IosClipboardManager.ObservationControlKey));
        }

        [UnityTest]
        public IEnumerator NativeResultHandedOverOnDestroy_IsDroppedByAReset()
        {
            // Implementation review v1 (Codex R-2, subagent R-7): no result crosses into a later test.
            LogAssert.ignoreFailingMessages = true;
            IosClipboardManager manager = BridgedManager();
            int delivered = 0;

            manager.Copy(IosClipboardContent.PlainText("x"), null, null, _ => delivered++);
            manager.Append(IosClipboardContent.PlainText("y"), null, _ => delivered++);
            IosClipboardManager.RejectAtEntryForTests(IosClipboardManager.OperationCopy, EmptyItems);
            IosClipboardManager.CompleteOperationForTests(IosClipboardManager.OperationAppend, isSuccess: true);
            UnityEngine.Object.DestroyImmediate(manager.gameObject);
            IosClipboardManager.ResetForTests();

            for (int i = 0; i < 3; i++) yield return null;

            Assert.AreEqual(0, delivered);
        }

        [UnityTest]
        public IEnumerator DispatcherDestroyed_WaitingResultsAreDroppedAndTheMarkReleased()
        {
            // Implementation review v1 (Codex R-3, subagent R-6).
            LogAssert.ignoreFailingMessages = true;
            IosClipboardManager manager = BridgedManager();

            manager.Copy(IosClipboardContent.PlainText("x"));
            IosClipboardManager.RejectAtEntryForTests(IosClipboardManager.OperationCopy, EmptyItems);
            Assert.AreEqual(1, IosClipboardManager.LaterCountForTests);

            GameObject? dispatcherObject = GameObject.Find("UnityMainThreadDispatcher");
            Assert.IsNotNull(dispatcherObject);
            UnityEngine.Object.DestroyImmediate(dispatcherObject);
            manager.Append(IosClipboardContent.PlainText("y"));
            IosClipboardManager.CompleteOperationForTests(IosClipboardManager.OperationAppend, isSuccess: true);

            yield return null;

            Assert.AreEqual(0, IosClipboardManager.LaterCountForTests);
            Assert.IsFalse(IosClipboardManager.IsInFlightForTests(IosClipboardManager.OperationCopy), "the busy mark is released");
            Assert.IsFalse(IosClipboardManager.IsInFlightForTests(IosClipboardManager.OperationAppend));
        }

        [UnityTest]
        public IEnumerator RejectedAtEntry_AfterDestroy_IsDropped()
        {
            LogAssert.ignoreFailingMessages = true;
            IosClipboardManager manager = BridgedManager();
            bool delivered = false;

            manager.Copy(IosClipboardContent.PlainText("x"), null, null, _ => delivered = true);
            IosClipboardManager.RejectAtEntryForTests(IosClipboardManager.OperationCopy, EmptyItems);
            UnityEngine.Object.DestroyImmediate(manager.gameObject);

            yield return null;
            yield return null;

            Assert.IsFalse(delivered);
        }

        [UnityTest]
        public IEnumerator StartRejectedAtEntry_WhileObserving_LeavesTheObservationOn()
        {
            // Design v3, D-6: the library did nothing for a rejected start, so the previous observation goes on
            // natively and its changes still reach the common event. The rejected call's own registration is
            // released, as in 1.x.
            IosClipboardManager manager = BridgedManager();
            int common = 0;
            void OnChanged(IosClipboardChangeEvent e) => common++;
            manager.ClipboardChanged += OnChanged;
            var results = new List<IosClipboardOperationResult>();

            manager.StartObserving(null, null, results.Add);
            IosClipboardManager.CompleteObservationControlForTests(IosClipboardManager.OperationStartObserving, isSuccess: true);
            yield return null;

            manager.StartObserving(null, _ => { }, results.Add);
            IosClipboardManager.RejectAtEntryForTests(IosClipboardManager.OperationStartObserving, InvalidParameter);
            yield return null;
            yield return null;

            Assert.AreEqual(2, results.Count);
            Assert.AreEqual("CLIPBOARD_INVALID_REQUEST", results[1].Error?.Code);
            Assert.IsTrue(IosClipboardManager.IsObservingForTests);
            Assert.IsFalse(IosClipboardManager.HasChangeRegistrationForTests);
            Assert.IsFalse(IosClipboardManager.IsInFlightForTests(IosClipboardManager.ObservationControlKey));

            IosClipboardManager.DeliverChangeEventForTests(ChangeJson);
            yield return null;
            yield return null;
            manager.ClipboardChanged -= OnChanged;
            Assert.AreEqual(1, common);
        }

        [UnityTest]
        public IEnumerator StartFailingAfterAcceptance_LowersTheObservation()
        {
            IosClipboardManager manager = BridgedManager();

            manager.StartObserving(null, null);
            IosClipboardManager.CompleteObservationControlForTests(IosClipboardManager.OperationStartObserving, isSuccess: true);
            yield return null;
            manager.StartObserving(null, null);
            IosClipboardManager.CompleteObservationControlForTests(
                IosClipboardManager.OperationStartObserving, isSuccess: false, "CLIPBOARD_UNAVAILABLE", "The requested pasteboard is unavailable.");
            yield return null;

            Assert.IsFalse(IosClipboardManager.IsObservingForTests);
        }

        // ── C ABI 2.0.0: the Manager's own observation (design v3, D-6) ──

        private const string ChangeJson = "{\"scope\":{\"kind\":\"general\"},\"kind\":\"changed\"}";

        [UnityTest]
        public IEnumerator ChangeBeforeTheStartSucceeds_IsDropped()
        {
            IosClipboardManager manager = BridgedManager();
            int changed = 0;
            void OnChanged(IosClipboardChangeEvent e) => changed++;
            manager.ClipboardChanged += OnChanged;

            manager.StartObserving(null, _ => changed++);
            IosClipboardManager.DeliverChangeEventForTests(ChangeJson);
            yield return null;
            yield return null;

            manager.ClipboardChanged -= OnChanged;
            Assert.AreEqual(0, changed);
        }

        [UnityTest]
        public IEnumerator ChangeAfterTheStopResult_IsDropped()
        {
            IosClipboardManager manager = BridgedManager();
            int changed = 0;
            void OnChanged(IosClipboardChangeEvent e) => changed++;
            manager.ClipboardChanged += OnChanged;

            manager.StartObserving(null, null);
            IosClipboardManager.CompleteObservationControlForTests(IosClipboardManager.OperationStartObserving, isSuccess: true);
            yield return null;
            Assert.IsTrue(IosClipboardManager.IsObservingForTests);

            manager.StopObserving();
            IosClipboardManager.CompleteObservationControlForTests(IosClipboardManager.OperationStopObserving, isSuccess: true);
            yield return null;
            IosClipboardManager.DeliverChangeEventForTests(ChangeJson);
            yield return null;
            yield return null;

            manager.ClipboardChanged -= OnChanged;
            Assert.IsFalse(IosClipboardManager.IsObservingForTests);
            Assert.AreEqual(0, changed);
        }

        [UnityTest]
        public IEnumerator BusyStop_LeavesTheObservationOn()
        {
            IosClipboardManager manager = BridgedManager();
            int common = 0, perRegistration = 0;
            void OnChanged(IosClipboardChangeEvent e) => common++;
            manager.ClipboardChanged += OnChanged;
            IosClipboardOperationResult? stopped = null;

            manager.StartObserving(null, _ => perRegistration++);
            manager.StopObserving(r => stopped = r);
            IosClipboardManager.CompleteObservationControlForTests(IosClipboardManager.OperationStartObserving, isSuccess: true);
            yield return null;
            yield return null;

            Assert.AreEqual(IosClipboardManager.BusyErrorCode, stopped?.Error?.Code);
            Assert.IsTrue(IosClipboardManager.IsObservingForTests);
            IosClipboardManager.DeliverChangeEventForTests(ChangeJson);
            yield return null;
            yield return null;

            manager.ClipboardChanged -= OnChanged;
            Assert.AreEqual(1, common);
            Assert.AreEqual(1, perRegistration);
        }

        // ── C ABI 2.0.0: checks in C# (design v3, D-8) ──

        [UnityTest]
        public IEnumerator NulInAPasteboardName_FailsAsAnInvalidRequest()
        {
            IosClipboardManager manager = BridgedManager();
            IosClipboardErrorInfo? error = null;

            manager.Read(IosPasteboardScope.Named("a\0b"), r => error = r.Error);
            yield return null;
            yield return null;

            Assert.AreEqual("CLIPBOARD_INVALID_REQUEST", error?.Code);
            Assert.AreEqual("The request is invalid.", error?.Message);
            Assert.IsFalse(IosClipboardManager.IsInFlightForTests(IosClipboardManager.OperationRead));
        }

        [UnityTest]
        public IEnumerator PatternOutsideTheEnum_FailsAsAnInvalidRequest()
        {
            IosClipboardManager manager = BridgedManager();
            IosClipboardErrorInfo? error = null;

            manager.DetectPatterns(new[] { (IosClipboardDetectionPattern)35 }, null, r => error = r.Error);
            yield return null;
            yield return null;

            Assert.AreEqual("CLIPBOARD_INVALID_REQUEST", error?.Code);
        }

        [UnityTest]
        public IEnumerator NulInAValue_WarnsWithoutTheValueAndGoesOn()
        {
            IosClipboardManager manager = BridgedManager();
            // The whole warning: it names no value.
            LogAssert.Expect(LogType.Warning, "[IosClipboardManager] " + IosClipboardCApi.NulValueWarning);
            StringAssert.DoesNotContain("secret", IosClipboardCApi.NulValueWarning);

            manager.Copy(IosClipboardContent.PlainText("secret\0tail"));
            yield return null;

            Assert.IsTrue(IosClipboardManager.IsInFlightForTests(IosClipboardManager.OperationCopy), "the call goes on");
        }
    }
}
#endif
