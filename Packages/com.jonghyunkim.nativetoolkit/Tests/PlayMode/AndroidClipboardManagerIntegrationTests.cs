#nullable enable

#if UNITY_ANDROID
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using JonghyunKim.NativeToolkit.Runtime.Clipboard;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// Layer 2b tests (see agent-rules/coding-rules/testing.md): PlayMode tests that run on an
    /// actual Android Player to verify the JNI / IL2CPP path that no other layer can reach.
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
    /// </summary>
    public sealed class AndroidClipboardManagerIntegrationTests
    {
        private const float DefaultTimeoutSeconds = 5f;
        private const string SampleText = "NTK-7F3A-92QX";

        // Results are queued rather than overwritten. Manager events are delivered through
        // UnityMainThreadDispatcher, so an operation started late in one test (notably the teardown
        // cleanup) can surface a frame later, i.e. during the next test. Matching on the operation
        // name instead of "whatever arrived last" keeps tests independent of that timing.
        private readonly List<ClipboardOperationResult> _results = new();
        private ClipboardOperationResult? _lastResult;
        private bool _subscribed;

        [SetUp]
        public void SetUp()
        {
            if (Application.platform != RuntimePlatform.Android)
            {
                Assert.Ignore(
                    "Device-only. These tests verify real JNI / IL2CPP behaviour and must run on an " +
                    "Android Player (testing.md layer 2b).");
            }

            ResetResult();
            AndroidClipboardManager.Instance.ClipboardOperationCompleted += OnOperationCompleted;
            _subscribed = true;
        }

        /// <summary>
        /// Restores device state and, crucially, waits for the cleanup operations to complete before
        /// unsubscribing. Returning a coroutine (UnityTearDown) is what makes that wait possible; a
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

            AndroidClipboardManager.Instance.StopObserving();
            yield return DrainOperation(AndroidClipboardManager.OperationStopObserving);

            // Drop the sample value from the real system clipboard.
            AndroidClipboardManager.Instance.Clear();
            yield return DrainOperation(AndroidClipboardManager.OperationClear);

            AndroidClipboardManager.Instance.ClipboardOperationCompleted -= OnOperationCompleted;
            _subscribed = false;
        }

        private void OnOperationCompleted(ClipboardOperationResult result)
        {
            _results.Add(result);
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

        // ---- Copy: basic JNI + AndroidJavaProxy round trip ----

        [UnityTest]
        public IEnumerator CopyPlainText_OnDevice_Succeeds()
        {
            AndroidClipboardManager.Instance.CopyPlainText(new CopyPlainTextPayload
            {
                text = SampleText,
                label = "sample"
            });

            yield return WaitForOperation(AndroidClipboardManager.OperationCopyPlainText);

            // Reaching a Success here proves the JNI method resolved and the AndroidJavaProxy
            // callback was invoked back into managed code under IL2CPP.
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

        // ---- StopObserving: zero-argument JNI resolution (design v4, 要検証 9.3) ----

        [UnityTest]
        public IEnumerator StopObserving_ZeroArgumentJniCall_Resolves()
        {
            // StopObserving is the only native method called with no arguments at all
            // (fullArgs = Array.Empty<object?>()). If JNI cannot resolve that overload, the call
            // throws and CallOperation reports Failure with the exception message, so asserting
            // Success here is what proves the zero-arg path works on device.
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
    }
}
#endif
