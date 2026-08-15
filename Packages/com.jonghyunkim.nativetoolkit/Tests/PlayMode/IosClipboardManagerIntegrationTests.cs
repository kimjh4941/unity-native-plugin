#nullable enable

#if UNITY_IOS || UNITY_EDITOR
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
    }
}
#endif
