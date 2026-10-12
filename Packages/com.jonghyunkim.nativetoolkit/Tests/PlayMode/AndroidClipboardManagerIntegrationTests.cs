#nullable enable

#if UNITY_ANDROID
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using NUnit.Framework;
#if !UNITY_EDITOR
using System.Runtime.InteropServices;
using AOT;
using JonghyunKim.NativeToolkit.Runtime.Android.Common;
#endif
using UnityEngine;
using UnityEngine.TestTools;
using JonghyunKim.NativeToolkit.Runtime.Android.Clipboard;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// Layer 2b tests (see agent-rules/coding-rules/testing.md): PlayMode tests that run on an
    /// actual Android Player to verify the native path (the C ABI's P/Invoke, or the 1.x bridge's
    /// JNI) under IL2CPP, which no other layer can reach.
    ///
    /// Unlike <c>IosShareManagerIntegrationTests</c> (which asserts the Editor fallback path),
    /// these tests assert real on-device behaviour and are skipped anywhere else. Running them in
    /// the Editor would only exercise the "not an Android device" failure branch, which is already
    /// covered by the EditMode suite.
    ///
    /// This suite calls the Manager directly and does not drive the sample scene UI, so it needs
    /// no scene fixture and no UI click helper. Button-name wiring is covered separately by
    /// <c>AndroidClipboardSampleSceneWiringTests</c> (EditMode).
    ///
    /// All values used here are sample values, never real user data, so plain assertions are safe
    /// (testing.md section 6).
    ///
    /// The AC-* tests are the device table of the Clipboard design
    /// (artifact/android/clipboard/designs/2026-10-12-android-clipboard-design-v4.md, 7.3). They run
    /// on the 1.x bridge first, to record its behaviour, and on the C ABI after; where the two are
    /// known to differ, <see cref="OnCApi"/> picks the expectation. Values the design only records
    /// are logged with the prefix "[ClipboardRecord]".
    /// </summary>
    public sealed class AndroidClipboardManagerIntegrationTests
    {
        private const float DefaultTimeoutSeconds = 5f;
        private const string SampleText = "NTK-7F3A-92QX";

        // A content URI no provider serves: the clip is built from the string alone, and a URI clip
        // is reported to change listeners exactly once (no text classification follows it).
        private const string SampleUri = "content://com.jonghyunkim.nativetoolkit.test/none/1";

        // How long a test waits after StartObserving before it copies (observing starts later, on
        // the Android main thread), and how long it waits for change events to arrive.
        private const float ObserveSettleSeconds = 0.5f;
        private const float ChangeWindowSeconds = 3f;

        // Results are queued rather than overwritten. Manager events are delivered through a queue
        // run on Unity's main thread, so an operation started late in one test (notably the teardown
        // cleanup) can surface a frame later, i.e. during the next test. Matching on the operation
        // name instead of "whatever arrived last" keeps tests independent of that timing.
        private readonly List<ClipboardOperationResult> _results = new();
        private ClipboardOperationResult? _lastResult;
        private bool _subscribed;
        private AndroidClipboardManager? _manager;
        private int _changes;
        private readonly List<int> _eventThreads = new();
        private int _mainThread;

        /// <summary>
        /// Whether the manager runs on native-toolkit's C ABI. The 1.x bridge has no test seam, so the
        /// same tests record the 1.x behaviour first and check the C ABI after (design 7.3).
        /// </summary>
        private static bool OnCApi => typeof(AndroidClipboardManager).GetProperty(
            "IsObservingForTests", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic) != null;

        [SetUp]
        public void SetUp()
        {
            if (Application.platform != RuntimePlatform.Android)
            {
                Assert.Ignore(
                    "Device-only. These tests verify the real native behaviour under IL2CPP and must run on an " +
                    "Android Player (testing.md layer 2b).");
            }

            ResetResult();
            _mainThread = Thread.CurrentThread.ManagedThreadId;
            _changes = 0;
            _eventThreads.Clear();
            Subscribe(AndroidClipboardManager.Instance);
        }

        private void Subscribe(AndroidClipboardManager manager)
        {
            manager.ClipboardOperationCompleted += OnOperationCompleted;
            manager.ClipboardChanged += OnChanged;
            _manager = manager;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (_manager != null)
            {
                _manager.ClipboardOperationCompleted -= OnOperationCompleted;
                _manager.ClipboardChanged -= OnChanged;
            }
            _manager = null;
            _subscribed = false;
        }

        /// <summary>
        /// Restores device state and, crucially, waits for the cleanup operations to complete before
        /// unsubscribing. The cleanup itself never fails the test, except for one check at the end:
        /// every event of the test came on Unity's main thread. Returning a coroutine (UnityTearDown) is what makes that wait possible; a
        /// plain [TearDown] would leave the stopObserving / clear events in flight and they would be
        /// observed by the following test.
        /// </summary>
        [UnityTearDown]
        public IEnumerator TearDownCoroutine()
        {
            if (!_subscribed)
            {
                yield break;
            }

            _results.Clear();

            // A test may have destroyed the manager and made a new one (AC-31): clean up through
            // the live one.
            if (_manager != AndroidClipboardManager.Instance)
            {
                Unsubscribe();
                Subscribe(AndroidClipboardManager.Instance);
            }

            AndroidClipboardManager.Instance.StopObserving();
            yield return DrainOperation(AndroidClipboardManager.OperationStopObserving);

            // Drop the sample value from the real system clipboard.
            AndroidClipboardManager.Instance.Clear();
            yield return DrainOperation(AndroidClipboardManager.OperationClear);

            Unsubscribe();
            // Every event of the test and of its cleanup came on Unity's main thread (design 7.3).
            AssertEventsOnMainThread();
        }

        private void OnOperationCompleted(ClipboardOperationResult result)
        {
            _eventThreads.Add(Thread.CurrentThread.ManagedThreadId);
            _results.Add(result);
        }

        private void OnChanged()
        {
            _eventThreads.Add(Thread.CurrentThread.ManagedThreadId);
            _changes++;
        }

        private static IEnumerator WaitSeconds(float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < deadline)
                yield return null;
        }

        private static void Record(string id, string what) => Debug.Log($"[ClipboardRecord] {id} {what}");

        private void AssertEventsOnMainThread() =>
            Assert.IsTrue(_eventThreads.All(id => id == _mainThread),
                $"an event was raised off Unity's main thread (threads {string.Join(", ", _eventThreads)}, main {_mainThread})");

        private IEnumerator Copy(CopyPlainTextPayload payload)
        {
            AndroidClipboardManager.Instance.CopyPlainText(payload);
            yield return WaitForOperation(AndroidClipboardManager.OperationCopyPlainText);
        }

        private IEnumerator CopyUriAndWait(string uri = SampleUri)
        {
            AndroidClipboardManager.Instance.CopyUri(new CopyUriPayload { uri = uri });
            yield return WaitForOperation(AndroidClipboardManager.OperationCopyUri);
        }

        private ClipItem ReadFirstItem()
        {
            var read = AndroidClipboardManager.Instance.Read();
            Assert.AreEqual(ClipboardReadStatus.HasContent, read.Status, $"errorCode: {read.ErrorCode}, message: {read.ErrorMessage}");
            Assert.GreaterOrEqual(read.Contents!.Items.Count, 1, "Expected at least one clip item.");
            return read.Contents.Items[0];
        }

        /// <summary>
        /// 1.x reads the current activity through JNI, which a thread that is not attached to the
        /// JVM cannot do: it logs two errors and fails the call (the Share recorded the same,
        /// UI test plan v6 AS-35).
        /// </summary>
        private static void ExpectOffThreadErrors()
        {
            LogAssert.Expect(LogType.Error, new Regex(@"\[AndroidClipboardManager\] Failed to get currentActivity"));
            LogAssert.Expect(LogType.Error, new Regex(@"\[AndroidClipboardManager\] \w+ failed: currentActivity is null\."));
        }

        private static Exception? RunOnAnotherThread(Action action)
        {
            Exception? thrown = null;
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception e)
                {
                    thrown = e;
                }
            });
            thread.Start();
            thread.Join();
            return thrown;
        }

        private void ResetResult()
        {
            _results.Clear();
            _lastResult = null;
        }

        /// <summary>
        /// Waits for a <c>ClipboardOperationCompleted</c> event for the given operation, failing on
        /// timeout rather than waiting a fixed number of frames (testing.md layer 2b).
        /// Events for other operations are skipped rather than mistaken for this one.
        /// </summary>
        private IEnumerator WaitForOperation(string expectedOperation, float timeoutSeconds = DefaultTimeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                int index = _results.FindIndex(r => r.Operation == expectedOperation);
                if (index >= 0)
                {
                    _lastResult = _results[index];
                    _results.RemoveAt(index);
                    yield break;
                }
                yield return null;
            }

            Assert.Fail(
                $"No ClipboardOperationCompleted for '{expectedOperation}' within {timeoutSeconds}s. " +
                $"Received: [{string.Join(", ", _results.Select(r => r.Operation))}]");
        }

        /// <summary>
        /// Teardown-only variant: waits for a cleanup operation to land but never fails the test,
        /// so a cleanup hiccup cannot mask the real assertion result.
        /// </summary>
        private IEnumerator DrainOperation(string operation, float timeoutSeconds = DefaultTimeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                int index = _results.FindIndex(r => r.Operation == operation);
                if (index >= 0)
                {
                    _results.RemoveAt(index);
                    yield break;
                }
                yield return null;
            }

            Debug.LogWarning($"[Test] Cleanup operation '{operation}' did not complete within {timeoutSeconds}s.");
        }

        private void AssertSucceeded()
        {
            Assert.IsTrue(
                _lastResult!.Value.IsSuccess,
                $"Expected success but failed: {_lastResult.Value.ErrorMessage}");
            Assert.IsNull(_lastResult.Value.ErrorMessage, "ErrorMessage must be null when IsSuccess is true.");
        }

        // ---- Copy: the basic round trip through the native layer ----

        [UnityTest]
        public IEnumerator CopyPlainText_OnDevice_Succeeds()
        {
            AndroidClipboardManager.Instance.CopyPlainText(new CopyPlainTextPayload
            {
                text = SampleText,
                label = "sample"
            });

            yield return WaitForOperation(AndroidClipboardManager.OperationCopyPlainText);

            // Reaching a Success here proves the native call resolved under IL2CPP and its result
            // came back to managed code.
            AssertSucceeded();
        }

        [UnityTest]
        public IEnumerator CopyPlainText_BlankText_Succeeds()
        {
            // Blank plain text is explicitly allowed by the native use case (design v4, 1.10).
            // Guards against a regression that would start rejecting it.
            AndroidClipboardManager.Instance.CopyPlainText(new CopyPlainTextPayload { text = "" });

            yield return WaitForOperation(AndroidClipboardManager.OperationCopyPlainText);

            AssertSucceeded();
        }

        // ---- Read / HasClip / GetDescription: synchronous path against real clipboard state ----

        [UnityTest]
        public IEnumerator CopyPlainText_ThenRead_ReturnsCopiedText()
        {
            AndroidClipboardManager.Instance.CopyPlainText(new CopyPlainTextPayload { text = SampleText });
            yield return WaitForOperation(AndroidClipboardManager.OperationCopyPlainText);
            AssertSucceeded();

            var read = AndroidClipboardManager.Instance.Read();

            Assert.AreEqual(ClipboardReadStatus.HasContent, read.Status, $"errorCode: {read.ErrorCode}");
            Assert.IsNotNull(read.Contents);
            Assert.GreaterOrEqual(read.Contents!.Items.Count, 1, "Expected at least one clip item.");
            Assert.AreEqual(SampleText, read.Contents.Items[0].Text);
        }

        [UnityTest]
        public IEnumerator CopyPlainText_ThenHasClip_ReturnsTrue()
        {
            AndroidClipboardManager.Instance.CopyPlainText(new CopyPlainTextPayload { text = SampleText });
            yield return WaitForOperation(AndroidClipboardManager.OperationCopyPlainText);
            AssertSucceeded();

            Assert.IsTrue(AndroidClipboardManager.Instance.HasClip());
        }

        [UnityTest]
        public IEnumerator CopyPlainText_ThenGetDescription_ReturnsHasContent()
        {
            AndroidClipboardManager.Instance.CopyPlainText(new CopyPlainTextPayload { text = SampleText });
            yield return WaitForOperation(AndroidClipboardManager.OperationCopyPlainText);
            AssertSucceeded();

            var description = AndroidClipboardManager.Instance.GetDescription();

            Assert.AreEqual(ClipboardReadStatus.HasContent, description.Status, $"errorCode: {description.ErrorCode}");
            Assert.IsNotNull(description.Description);
            Assert.GreaterOrEqual(description.Description!.MimeTypes.Count, 1, "Expected at least one MIME type.");
        }

        // ---- Clear: empty clipboard is a normal outcome, not a failure ----

        [UnityTest]
        public IEnumerator Clear_ThenRead_ReturnsEmptyNotFailed()
        {
            AndroidClipboardManager.Instance.CopyPlainText(new CopyPlainTextPayload { text = SampleText });
            yield return WaitForOperation(AndroidClipboardManager.OperationCopyPlainText);
            AssertSucceeded();

            ResetResult();
            AndroidClipboardManager.Instance.Clear();
            yield return WaitForOperation(AndroidClipboardManager.OperationClear);
            AssertSucceeded();

            var read = AndroidClipboardManager.Instance.Read();

            Assert.AreEqual(
                ClipboardReadStatus.Empty,
                read.Status,
                $"An empty clipboard must report Empty, not Failed. errorCode: {read.ErrorCode}");
            Assert.IsTrue(read.IsSuccess, "Empty must not be treated as a failure.");
            Assert.IsFalse(AndroidClipboardManager.Instance.HasClip());
        }

        // ---- StopObserving: the call with no arguments ----

        [UnityTest]
        public IEnumerator StopObserving_ZeroArgumentJniCall_Resolves()
        {
            // StopObserving is the only asynchronous operation with no arguments. On the 1.x bridge
            // this proved the zero-argument JNI overload resolved; on the C ABI it is the stop being
            // accepted. The test keeps its 1.x name.
            AndroidClipboardManager.Instance.StopObserving();

            yield return WaitForOperation(AndroidClipboardManager.OperationStopObserving);

            AssertSucceeded();
        }

        // ---- StartObserving: reports no result at all ----

        [UnityTest]
        public IEnumerator StartObserving_DoesNotFireOperationEvent()
        {
            AndroidClipboardManager.Instance.StartObserving();

            // Give the native side ample time to (incorrectly) report something.
            float deadline = Time.realtimeSinceStartup + 1f;
            while (Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.IsEmpty(
                _results.Select(r => r.Operation).ToArray(),
                "StartObserving must not raise ClipboardOperationCompleted; it reports no result.");
        }

        // ---- Error contract: the exact native error messages reach managed code ----

        [UnityTest]
        public IEnumerator CopyHtmlText_BlankHtml_FailsWithNativeMessage()
        {
            AndroidClipboardManager.Instance.CopyHtmlText(new CopyHtmlTextPayload
            {
                plainText = "Hello",
                htmlText = ""
            });

            yield return WaitForOperation(AndroidClipboardManager.OperationCopyHtmlText);

            Assert.IsFalse(_lastResult!.Value.IsSuccess);
            Assert.AreEqual(
                "Clipboard content is empty. Please provide text or HTML.",
                _lastResult.Value.ErrorMessage);
        }

        [UnityTest]
        public IEnumerator CopyMultipleText_EmptyArray_FailsWithNativeMessage()
        {
            AndroidClipboardManager.Instance.CopyMultipleText(new CopyMultipleTextPayload
            {
                texts = System.Array.Empty<string>()
            });

            yield return WaitForOperation(AndroidClipboardManager.OperationCopyMultipleText);

            Assert.IsFalse(_lastResult!.Value.IsSuccess);
            Assert.AreEqual("No items provided for clipboard copy.", _lastResult.Value.ErrorMessage);
        }

        [UnityTest]
        public IEnumerator CopyUri_BlankUri_FailsWithNativeMessage()
        {
            AndroidClipboardManager.Instance.CopyUri(new CopyUriPayload { uri = "" });

            yield return WaitForOperation(AndroidClipboardManager.OperationCopyUri);

            Assert.IsFalse(_lastResult!.Value.IsSuccess);
            StringAssert.StartsWith("Invalid URI:", _lastResult.Value.ErrorMessage);
        }

        // ---- AC-12 to AC-31: the design's device table (7.3) ----

        [UnityTest]
        public IEnumerator AC12_CopyHtmlText_ThenRead_ReturnsBothForms()
        {
            AndroidClipboardManager.Instance.CopyHtmlText(new CopyHtmlTextPayload { plainText = "Hello plain", htmlText = "<b>Hello</b>" });
            yield return WaitForOperation(AndroidClipboardManager.OperationCopyHtmlText);
            AssertSucceeded();

            var read = AndroidClipboardManager.Instance.Read();
            Assert.AreEqual(ClipboardReadStatus.HasContent, read.Status, $"errorCode: {read.ErrorCode}");
            Assert.AreEqual("Hello plain", read.Contents!.Items[0].Text);
            Assert.AreEqual("<b>Hello</b>", read.Contents.Items[0].HtmlText);
            CollectionAssert.Contains(read.Contents.MimeTypes, "text/html");
        }

        [UnityTest]
        public IEnumerator AC13_CopyHtmlText_EmptyPlainText_ReadsTextAsNull()
        {
            AndroidClipboardManager.Instance.CopyHtmlText(new CopyHtmlTextPayload { plainText = "", htmlText = "<i>only html</i>" });
            yield return WaitForOperation(AndroidClipboardManager.OperationCopyHtmlText);
            AssertSucceeded();

            ClipItem item = ReadFirstItem();
            Assert.IsNull(item.Text, "An empty string is never returned; it is normalized to null.");
            Assert.AreEqual("<i>only html</i>", item.HtmlText);
        }

        [UnityTest]
        public IEnumerator AC14_CopyUri_ThenRead_ReturnsTheUri()
        {
            yield return CopyUriAndWait();
            AssertSucceeded();

            ClipItem item = ReadFirstItem();
            Assert.AreEqual(SampleUri, item.Uri);
            Assert.AreEqual(SampleUri, item.CoercedText, "coercedText is text, else the URI");
            Assert.IsNull(item.Text);
        }

        [UnityTest]
        public IEnumerator AC15_CopyUri_HttpScheme_FailsWithTheUriInTheMessage()
        {
            yield return CopyUriAndWait("http://example.com");

            Assert.IsFalse(_lastResult!.Value.IsSuccess);
            Assert.AreEqual("Invalid URI: http://example.com", _lastResult.Value.ErrorMessage);
        }

        [UnityTest]
        public IEnumerator AC16_CopyMultipleText_ThenRead_KeepsEveryItem()
        {
            AndroidClipboardManager.Instance.CopyMultipleText(new CopyMultipleTextPayload { texts = new[] { "a", "", "c" } });
            yield return WaitForOperation(AndroidClipboardManager.OperationCopyMultipleText);
            AssertSucceeded();

            var read = AndroidClipboardManager.Instance.Read();
            Assert.AreEqual(ClipboardReadStatus.HasContent, read.Status, $"errorCode: {read.ErrorCode}");
            CollectionAssert.AreEqual(new[] { "a", null, "c" }, read.Contents!.Items.Select(i => i.Text).ToArray());
        }

        [UnityTest]
        public IEnumerator AC17_Label_IsReadBackByDescriptionAndRead()
        {
            yield return Copy(new CopyPlainTextPayload { text = SampleText, label = "NTK label", isSensitive = true });
            AssertSucceeded();

            var description = AndroidClipboardManager.Instance.GetDescription();
            Assert.AreEqual(ClipboardReadStatus.HasContent, description.Status, $"errorCode: {description.ErrorCode}");
            Assert.AreEqual("NTK label", description.Description!.Label);
            var read = AndroidClipboardManager.Instance.Read();
            Assert.AreEqual("NTK label", read.Contents!.Label);
        }

        [UnityTest]
        public IEnumerator AC18_BlankLabel_IsReadBackAsNull()
        {
            yield return Copy(new CopyPlainTextPayload { text = SampleText, label = "   " });
            AssertSucceeded();

            Assert.IsNull(AndroidClipboardManager.Instance.GetDescription().Description!.Label);
            Assert.IsNull(AndroidClipboardManager.Instance.Read().Contents!.Label);
        }

        [UnityTest]
        public IEnumerator AC19_Clear_ThenHasClipAndDescription_AreEmpty()
        {
            yield return Copy(new CopyPlainTextPayload { text = SampleText });
            AssertSucceeded();
            AndroidClipboardManager.Instance.Clear();
            yield return WaitForOperation(AndroidClipboardManager.OperationClear);
            AssertSucceeded();

            Assert.IsFalse(AndroidClipboardManager.Instance.HasClip());
            var description = AndroidClipboardManager.Instance.GetDescription();
            Assert.AreEqual(ClipboardReadStatus.Empty, description.Status, $"errorCode: {description.ErrorCode}");
        }

        [UnityTest]
        public IEnumerator AC20_Result_ComesAfterTheCall_CommonEventFirst_OnMainThread()
        {
            var order = new List<string>();
            void OnCommon(ClipboardOperationResult r) => order.Add("common");
            AndroidClipboardManager.Instance.ClipboardOperationCompleted += OnCommon;
            try
            {
                AndroidClipboardManager.Instance.CopyPlainText(new CopyPlainTextPayload { text = SampleText }, r => order.Add("perCall"));
                Assert.IsEmpty(order, "No result is raised inside the call.");

                yield return WaitForOperation(AndroidClipboardManager.OperationCopyPlainText);
                yield return null;
                CollectionAssert.AreEqual(new[] { "common", "perCall" }, order);
                AssertEventsOnMainThread();
            }
            finally
            {
                AndroidClipboardManager.Instance.ClipboardOperationCompleted -= OnCommon;
            }
        }

        [UnityTest]
        public IEnumerator AC21a_AfterObservingStarts_AUriCopyIsReportedExactlyOnce()
        {
            // Initialize twice: a second registration of the change listener would report twice.
            AndroidClipboardManager.Instance.Initialize();
            AndroidClipboardManager.Instance.Initialize();
            AndroidClipboardManager.Instance.StartObserving();
            yield return WaitSeconds(ObserveSettleSeconds);
            _changes = 0;

            yield return CopyUriAndWait();
            AssertSucceeded();
            yield return WaitSeconds(ChangeWindowSeconds);

            Record("AC-21a", $"changes={_changes}");
            Assert.AreEqual(1, _changes);
            AssertEventsOnMainThread();
        }

        [UnityTest]
        public IEnumerator AC21b_CopyInTheSameFrameAsStartObserving_IsRecorded()
        {
            AndroidClipboardManager.Instance.StartObserving();
            AndroidClipboardManager.Instance.CopyUri(new CopyUriPayload { uri = SampleUri });
            yield return WaitForOperation(AndroidClipboardManager.OperationCopyUri);
            AssertSucceeded();
            yield return WaitSeconds(ChangeWindowSeconds);

            // A race on the C ABI (design 5.3, J-11's exception): recorded, never asserted.
            Record("AC-21b", $"changes={_changes}");
        }

        [UnityTest]
        public IEnumerator AC21c_AfterObservingStarts_ATextCopyIsReported()
        {
            AndroidClipboardManager.Instance.StartObserving();
            yield return WaitSeconds(ObserveSettleSeconds);
            _changes = 0;

            yield return Copy(new CopyPlainTextPayload { text = SampleText });
            AssertSucceeded();
            yield return WaitSeconds(ChangeWindowSeconds);

            // Android 12 and later may report a text clip again after classifying it.
            Record("AC-21c", $"changes={_changes}");
            Assert.GreaterOrEqual(_changes, 1);
        }

        [UnityTest]
        public IEnumerator AC22_AfterStopObserving_NoChangeIsReported()
        {
            AndroidClipboardManager.Instance.StartObserving();
            yield return WaitSeconds(ObserveSettleSeconds);
            AndroidClipboardManager.Instance.StopObserving();
            yield return WaitForOperation(AndroidClipboardManager.OperationStopObserving);
            AssertSucceeded();
            yield return WaitSeconds(ObserveSettleSeconds);
            _changes = 0;

            yield return CopyUriAndWait();
            yield return WaitSeconds(ChangeWindowSeconds);

            Assert.AreEqual(0, _changes);
        }

        [UnityTest]
        public IEnumerator AC23_WithoutStartObserving_NoChangeIsReported()
        {
            yield return CopyUriAndWait();
            AssertSucceeded();
            yield return WaitSeconds(ChangeWindowSeconds);

            Assert.AreEqual(0, _changes);
        }

        [UnityTest]
        public IEnumerator AC24_CopyFromAnotherThread()
        {
            var manager = AndroidClipboardManager.Instance;
            if (!OnCApi) ExpectOffThreadErrors();

            Exception? thrown = RunOnAnotherThread(() => manager.CopyPlainText(new CopyPlainTextPayload { text = SampleText }));
            Assert.IsNull(thrown, $"the call threw: {thrown}");
            yield return WaitForOperation(AndroidClipboardManager.OperationCopyPlainText);

            Record("AC-24", $"isSuccess={_lastResult!.Value.IsSuccess}, message={_lastResult.Value.ErrorMessage ?? "null"}");
            if (OnCApi)
                AssertSucceeded();
            else
                Assert.AreEqual("copyPlainText could not be started.", _lastResult.Value.ErrorMessage);
            AssertEventsOnMainThread();
        }

        [UnityTest]
        public IEnumerator AC25_ReadFromAnotherThread()
        {
            yield return Copy(new CopyPlainTextPayload { text = SampleText });
            AssertSucceeded();
            var manager = AndroidClipboardManager.Instance;
            if (!OnCApi) ExpectOffThreadErrors();

            ClipboardReadResult read = default;
            Exception? thrown = RunOnAnotherThread(() => read = manager.Read());
            Assert.IsNull(thrown, $"the call threw: {thrown}");

            Record("AC-25", $"status={read.Status}, errorCode={read.ErrorCode ?? "null"}, message={read.ErrorMessage ?? "null"}");
            if (OnCApi)
            {
                Assert.AreEqual(ClipboardReadStatus.HasContent, read.Status, $"errorCode: {read.ErrorCode}");
                Assert.AreEqual(SampleText, read.Contents!.Items[0].Text);
            }
            else
            {
                Assert.AreEqual(ClipboardReadStatus.Failed, read.Status);
                Assert.AreEqual("read could not be started.", read.ErrorMessage);
            }
        }

        [UnityTest]
        public IEnumerator AC26a_NullText()
        {
            yield return Copy(new CopyPlainTextPayload { text = null! });
            AssertSucceeded();

            // 1.x is expected to have carried JSON null as the string "null" (design 5.3).
            ClipItem item = ReadFirstItem();
            Record("AC-26a", $"text={item.Text ?? "(null)"}");
            Assert.AreEqual(OnCApi ? null : "null", item.Text);
        }

        [UnityTest]
        public IEnumerator AC26b_NullPlainText()
        {
            AndroidClipboardManager.Instance.CopyHtmlText(new CopyHtmlTextPayload { plainText = null!, htmlText = "<b>html</b>" });
            yield return WaitForOperation(AndroidClipboardManager.OperationCopyHtmlText);
            AssertSucceeded();

            ClipItem item = ReadFirstItem();
            Record("AC-26b", $"text={item.Text ?? "(null)"}, html={item.HtmlText ?? "(null)"}");
            Assert.AreEqual(OnCApi ? null : "null", item.Text);
            Assert.AreEqual("<b>html</b>", item.HtmlText);
        }

        [UnityTest]
        public IEnumerator AC26c_NullHtmlText()
        {
            AndroidClipboardManager.Instance.CopyHtmlText(new CopyHtmlTextPayload { plainText = "plain", htmlText = null! });
            yield return WaitForOperation(AndroidClipboardManager.OperationCopyHtmlText);

            Record("AC-26c", $"isSuccess={_lastResult!.Value.IsSuccess}, message={_lastResult.Value.ErrorMessage ?? "null"}");
            if (OnCApi)
            {
                Assert.IsFalse(_lastResult.Value.IsSuccess);
                Assert.AreEqual("Clipboard content is empty. Please provide text or HTML.", _lastResult.Value.ErrorMessage);
            }
            else
            {
                AssertSucceeded();
                Assert.AreEqual("null", ReadFirstItem().HtmlText);
            }
        }

        [UnityTest]
        public IEnumerator AC26d_NullUri()
        {
            AndroidClipboardManager.Instance.CopyUri(new CopyUriPayload { uri = null! });
            yield return WaitForOperation(AndroidClipboardManager.OperationCopyUri);

            Record("AC-26d", $"isSuccess={_lastResult!.Value.IsSuccess}, message={_lastResult.Value.ErrorMessage ?? "null"}");
            Assert.IsFalse(_lastResult.Value.IsSuccess);
            Assert.AreEqual(OnCApi ? "Invalid URI: " : "Invalid URI: null", _lastResult.Value.ErrorMessage);
        }

        [UnityTest]
        public IEnumerator AC26e_NullListElement()
        {
            AndroidClipboardManager.Instance.CopyMultipleText(new CopyMultipleTextPayload { texts = new[] { "a", null!, "c" } });
            yield return WaitForOperation(AndroidClipboardManager.OperationCopyMultipleText);
            AssertSucceeded();

            var texts = AndroidClipboardManager.Instance.Read().Contents!.Items.Select(i => i.Text).ToArray();
            Record("AC-26e", $"texts={string.Join("|", texts.Select(t => t ?? "(null)"))}");
            CollectionAssert.AreEqual(new[] { "a", OnCApi ? null : "null", "c" }, texts);
        }

        [Test]
        public void AC27_NullPayload_Throws()
        {
            var thrown = Assert.Catch<Exception>(() => AndroidClipboardManager.Instance.CopyPlainText(null!));
            Record("AC-27", $"type={thrown.GetType().Name}");
            Assert.IsInstanceOf(OnCApi ? typeof(ArgumentNullException) : typeof(NullReferenceException), thrown);
        }

        [UnityTest]
        public IEnumerator AC28_ReadRightAfterCopy_WithoutWaitingForTheResult()
        {
            // 1.x copies later, on the Android main thread, so what a read right after the call sees
            // is a race there: it is counted over several rounds and recorded only.
            const int rounds = 10;
            int sawNew = 0;
            for (int i = 0; i < rounds; i++)
            {
                yield return Copy(new CopyPlainTextPayload { text = $"NTK-OLD-{i}" });
                AssertSucceeded();

                AndroidClipboardManager.Instance.CopyPlainText(new CopyPlainTextPayload { text = $"NTK-NEW-{i}" });
                var read = AndroidClipboardManager.Instance.Read();
                yield return WaitForOperation(AndroidClipboardManager.OperationCopyPlainText);
                AssertSucceeded();

                if (read.Contents?.Items.FirstOrDefault()?.Text == $"NTK-NEW-{i}") sawNew++;
            }

            Record("AC-28", $"new={sawNew}/{rounds}");
            if (OnCApi) Assert.AreEqual(rounds, sawNew);
        }

        [UnityTest]
        public IEnumerator AC29_ClassificationStatus_IsRecorded()
        {
            yield return Copy(new CopyPlainTextPayload { text = SampleText });
            AssertSucceeded();
            yield return WaitSeconds(1f);

            var description = AndroidClipboardManager.Instance.GetDescription();
            Assert.AreEqual(ClipboardReadStatus.HasContent, description.Status, $"errorCode: {description.ErrorCode}");
            Record("AC-29", $"classification={description.Description!.ClassificationStatus?.ToString() ?? "null"}, styled={description.Description.IsStyledText}");
        }

        [UnityTest]
        public IEnumerator AC30_JapaneseAndEmoji_RoundTrip()
        {
            const string text = "\u65E5\u672C\u8A9E\u306E\u30C6\u30AD\u30B9\u30C8 \uD83C\uDF89 NTK";
            yield return Copy(new CopyPlainTextPayload { text = text });
            AssertSucceeded();

            Assert.AreEqual(text, ReadFirstItem().Text);
        }

        [UnityTest]
        public IEnumerator AC31_ARecreatedManager_ObservesAgain()
        {
            AndroidClipboardManager.Instance.StartObserving();
            yield return WaitSeconds(ObserveSettleSeconds);

            Unsubscribe();
            UnityEngine.Object.Destroy(AndroidClipboardManager.Instance.gameObject);
            yield return null;
            Subscribe(AndroidClipboardManager.Instance);

            AndroidClipboardManager.Instance.StartObserving();
            yield return WaitSeconds(ObserveSettleSeconds);
            _changes = 0;

            yield return CopyUriAndWait();
            AssertSucceeded();
            yield return WaitSeconds(ChangeWindowSeconds);

            Record("AC-31", $"changes={_changes}");
            Assert.AreEqual(1, _changes);
        }

#if !UNITY_EDITOR
        // ---- AC-32 to AC-34: the C ABI only (the externs exist in an Android player only) ----

        private static int s_nativeChanges;
        private static readonly AndroidClipboardCApi.ChangeCallback s_testChange = OnTestChange;
        private static readonly IntPtr s_testChangePointer = Marshal.GetFunctionPointerForDelegate(s_testChange);

        [MonoPInvokeCallback(typeof(AndroidClipboardCApi.ChangeCallback))]
        private static void OnTestChange(IntPtr userData) => Interlocked.Increment(ref s_nativeChanges);

        // Not used by the manager, so the bridge does not declare it (design 4.1).
        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void ntk_clipboard_listener_remove(IntPtr listener);

        [UnityTest]
        public IEnumerator AC32_AnotherUsersObserving_IsNotRaised()
        {
            // native-toolkit's Kotlin API or another plugin in the same app, started without Unity.
            Assert.AreEqual(AndroidClipboardCApi.ErrorNone, AndroidClipboardCApi.ntk_clipboard_start_observing());
            try
            {
                yield return WaitSeconds(ObserveSettleSeconds);
                _changes = 0;
                yield return CopyUriAndWait();
                AssertSucceeded();
                yield return WaitSeconds(ChangeWindowSeconds);
                Assert.AreEqual(0, _changes);
            }
            finally
            {
                AndroidClipboardCApi.ntk_clipboard_stop_observing();
            }
        }

        [Test]
        public void AC33_ExternSignatures_MatchClipboardH()
        {
            Type ptr = typeof(IntPtr), size = typeof(UIntPtr), i32 = typeof(int), none = typeof(void);
            Type outPtr = ptr.MakeByRefType(), outSize = size.MakeByRefType(), outI32 = i32.MakeByRefType();
            Type refOptions = typeof(AndroidClipboardCApi.CopyOptions).MakeByRefType();
            var expected = new Dictionary<string, (Type ret, Type[] args)>
            {
                ["ntk_clipboard_copy_text"] = (i32, new[] { ptr, refOptions }),
                ["ntk_clipboard_copy_html"] = (i32, new[] { ptr, ptr, refOptions }),
                ["ntk_clipboard_copy_uri"] = (i32, new[] { ptr, refOptions }),
                ["ntk_clipboard_copy_texts"] = (i32, new[] { ptr, size, refOptions }),
                ["ntk_clipboard_clear"] = (i32, Type.EmptyTypes),
                ["ntk_clipboard_read"] = (i32, new[] { outPtr }),
                ["ntk_clipboard_has_clip"] = (i32, new[] { outI32 }),
                ["ntk_clipboard_get_description"] = (i32, new[] { outPtr }),
                ["ntk_clipboard_start_observing"] = (i32, Type.EmptyTypes),
                ["ntk_clipboard_stop_observing"] = (i32, Type.EmptyTypes),
                ["ntk_clipboard_add_change_listener"] = (i32, new[] { ptr, ptr, ptr, outPtr }),
                ["ntk_clipboard_content_label"] = (ptr, new[] { ptr, outSize }),
                ["ntk_clipboard_content_mime_type_count"] = (size, new[] { ptr }),
                ["ntk_clipboard_content_mime_type_at"] = (ptr, new[] { ptr, size, outSize }),
                ["ntk_clipboard_content_item_count"] = (size, new[] { ptr }),
                ["ntk_clipboard_content_item_text_at"] = (ptr, new[] { ptr, size, outSize }),
                ["ntk_clipboard_content_item_html_at"] = (ptr, new[] { ptr, size, outSize }),
                ["ntk_clipboard_content_item_uri_at"] = (ptr, new[] { ptr, size, outSize }),
                ["ntk_clipboard_content_item_coerced_text_at"] = (ptr, new[] { ptr, size, outSize }),
                ["ntk_clipboard_content_free"] = (none, new[] { ptr }),
                ["ntk_clipboard_description_label"] = (ptr, new[] { ptr, outSize }),
                ["ntk_clipboard_description_mime_type_count"] = (size, new[] { ptr }),
                ["ntk_clipboard_description_mime_type_at"] = (ptr, new[] { ptr, size, outSize }),
                ["ntk_clipboard_description_is_styled_text"] = (i32, new[] { ptr }),
                ["ntk_clipboard_description_classification_status"] = (i32, new[] { ptr }),
                ["ntk_clipboard_description_free"] = (none, new[] { ptr }),
            };

            var externs = typeof(AndroidClipboardCApi)
                .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(m => (m.Attributes & MethodAttributes.PinvokeImpl) != 0)
                .ToDictionary(m => m.Name);
            CollectionAssert.AreEquivalent(expected.Keys, externs.Keys);
            foreach (var (name, (ret, args)) in expected.Select(e => (e.Key, e.Value)))
            {
                MethodInfo method = externs[name];
                Assert.IsTrue(method.IsStatic && method.IsAssembly, $"{name}: internal static");
                Assert.AreEqual(ret, method.ReturnType, $"{name}: return type");
                CollectionAssert.AreEqual(args, method.GetParameters().Select(p => p.ParameterType).ToArray(), $"{name}: parameters");
                // The library name and the calling convention are not checked: DllImportAttribute is a
                // pseudo-attribute, and IL2CPP hands reflection one with an empty library name
                // (measured on 2026-10-12, design 9 K-6). Every call on the device uses the name.
            }
        }

        [UnityTest]
        public IEnumerator AC34_StopObservingAndDestroy_StopTheNativeObserving()
        {
            s_nativeChanges = 0;
            Assert.AreEqual(AndroidClipboardCApi.ErrorNone,
                AndroidClipboardCApi.ntk_clipboard_add_change_listener(s_testChangePointer, IntPtr.Zero, IntPtr.Zero, out IntPtr listener));
            try
            {
                // (1) Unity's observing reaches the test's own listener.
                AndroidClipboardManager.Instance.StartObserving();
                yield return WaitSeconds(ObserveSettleSeconds);
                Interlocked.Exchange(ref s_nativeChanges, 0);
                yield return CopyUriAndWait();
                yield return WaitSeconds(ChangeWindowSeconds);
                Assert.AreEqual(1, Volatile.Read(ref s_nativeChanges), "(1) observing");

                // (2) StopObserving stops the native observing, not only Unity's flag.
                AndroidClipboardManager.Instance.StopObserving();
                yield return WaitForOperation(AndroidClipboardManager.OperationStopObserving);
                AssertSucceeded();
                yield return WaitSeconds(ObserveSettleSeconds);
                Interlocked.Exchange(ref s_nativeChanges, 0);
                yield return CopyUriAndWait();
                yield return WaitSeconds(ChangeWindowSeconds);
                Assert.AreEqual(0, Volatile.Read(ref s_nativeChanges), "(2) after StopObserving");

                // (3) Destroying the observing manager stops it too.
                AndroidClipboardManager.Instance.StartObserving();
                yield return WaitSeconds(ObserveSettleSeconds);
                Unsubscribe();
                UnityEngine.Object.Destroy(AndroidClipboardManager.Instance.gameObject);
                yield return null;
                Subscribe(AndroidClipboardManager.Instance);
                yield return WaitSeconds(ObserveSettleSeconds);
                Interlocked.Exchange(ref s_nativeChanges, 0);
                yield return CopyUriAndWait();
                yield return WaitSeconds(ChangeWindowSeconds);
                Assert.AreEqual(0, Volatile.Read(ref s_nativeChanges), "(3) after the manager was destroyed");
            }
            finally
            {
                ntk_clipboard_listener_remove(listener);
            }
        }
#endif
    }
}
#endif
