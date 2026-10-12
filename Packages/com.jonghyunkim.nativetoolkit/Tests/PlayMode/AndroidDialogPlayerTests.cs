#nullable enable

#if UNITY_ANDROID
#if !UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using UnityEngine;
using UnityEngine.TestTools;
using JonghyunKim.NativeToolkit.Runtime.Android.Common;
using JonghyunKim.NativeToolkit.Runtime.Android.Dialog;
using Api = JonghyunKim.NativeToolkit.Runtime.Android.Dialog.AndroidDialogCApi;
using static JonghyunKim.NativeToolkit.Tests.AndroidSampleScreenDriver;
using Ui = JonghyunKim.NativeToolkit.Tests.AndroidUiDriver;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// Layer 2b tests of the Android Dialog on a device: the sample screen and the manager, with the
    /// OS dialogs pressed by the host-side UI driver. Test IDs (AD-xx) and expectations are in
    /// artifact/android/dialog/designs/2026-10-11-android-dialog-ui-test-plan-v2.md.
    /// </summary>
    /// <remarks>
    /// The expectations were first recorded on the 1.x bridge (Dialog design v4, 5.5 step 3; the UI
    /// test plan's 1.x column). The move to the C ABI changed only the ones listed as known
    /// differences in the design's 5.3, each marked "(5.3)" below, and added the cases only the C ABI
    /// has (AD-21, 26, 27, 32, 34, 36, 37).
    /// </remarks>
    [Category(AndroidUiDriver.Category)]
    public sealed class AndroidDialogPlayerTests
    {
        private const string TopMenuDialogButton = "DialogFeatureButton";
        private const string ResultLabel = "ResultTextBlock";
        private const string Positive = "android:id/button1";
        private const string Negative = "android:id/button2";
        private const string Title = "android:id/alertTitle";
        private const string Message = "android:id/message";
        private const string EditText = "android.widget.EditText";
        private const float EventTimeoutSeconds = 10f;
        private const float TearDownTimeoutSeconds = 5f;

        private readonly List<string> _events = new();
        // The thread each event was raised on: the teardown checks they are all Unity's main thread (J-D2).
        private readonly List<int> _eventThreads = new();
        private int _mainThread;
        private bool _subscribed;

        // The calls this test made that report through an event: each accepted request completes once,
        // and each call refused before the library reports at once. The teardown checks the events
        // against it (design 7.3).
        private int _requests;

        // What AD-27 and AD-28 leave for the teardown to undo. They are kept here, not undone in a
        // finally of the test: when a step the test yields fails, the Test Framework never resumes the
        // test's own code, finally blocks included (review v2, R2-1).
        private Thread? _background;
        private CancellationTokenSource? _abandonBackground;
        private int _backgroundCode = -1;
        private ScreenOrientation? _orientationBefore;
        private string? _rotationBefore;

        [SetUp]
        public void SetUp()
        {
            if (Application.platform != RuntimePlatform.Android)
                Assert.Ignore("Device-only.");
            // In case the last teardown did not get this far.
            Unsubscribe();
            AndroidNativeCallbackQueue.ResetForTests();
            _events.Clear();
            _eventThreads.Clear();
            _mainThread = Thread.CurrentThread.ManagedThreadId;
            _requests = 0;
            Subscribe();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // Each step runs so that what it throws is kept in errors and the cleanup below always
            // runs: UI steps through Safely, the rest in try/catch. All failures are reported at the end.
            var errors = new List<Exception>();

            // What needs no UI first.
            try
            {
                StopBackgroundCall(errors);
                if (_orientationBefore is ScreenOrientation orientation)
                {
                    _orientationBefore = null;
                    Screen.orientation = orientation;
                }
            }
            catch (Exception e)
            {
                errors.Add(e);
            }
            if (_rotationBefore != null)
            {
                yield return Safely(SettleRotation(_rotationBefore), errors);
                _rotationBefore = null;
            }
            yield return Safely(CloseLeftoverDialog(), errors);

            // Every request completes exactly once. Wait for the last completion (a dialog closed
            // above reports on a later frame), then a little longer, for a second one that should not
            // come. Only then is nothing of this test left to reach the next one.
            float deadline = Time.realtimeSinceStartup + TearDownTimeoutSeconds;
            while (_events.Count < _requests && Time.realtimeSinceStartup < deadline)
                yield return null;
            yield return new WaitForSecondsRealtime(0.5f);
            string? mismatch = _events.Count == _requests
                ? null
                : $"{_requests} call(s) reported {_events.Count} event(s): {string.Join(" | ", _events)}";
            string? offThread = _eventThreads.All(id => id == _mainThread)
                ? null
                : $"an event was raised off Unity's main thread (threads {string.Join(", ", _eventThreads)}, main {_mainThread})";

            Unsubscribe();
            AndroidNativeCallbackQueue.ResetForTests();
            yield return Safely(Unload(), errors);

            // Every failure, the count check included, in one message. An Ignore (no UI driver running)
            // ignores the test, unless the test itself has already failed: it must not hide that.
            var failures = errors.Where(e => !(e is IgnoreException)).Select(e => e.Message).ToList();
            if (mismatch != null) failures.Add(mismatch);
            if (offThread != null) failures.Add(offThread);
            if (failures.Count > 0) Assert.Fail(string.Join("\n", failures));
            Exception? ignored = errors.FirstOrDefault(e => e is IgnoreException);
            if (ignored != null && TestContext.CurrentContext.Result.Outcome.Status != TestStatus.Failed)
                ExceptionDispatchInfo.Capture(ignored).Throw();
        }

        /// <summary>
        /// A test that failed with its dialog open would leave it for the next one to press. Back
        /// closes a cancelable one; one that is not cancelable needs a button.
        /// </summary>
        private static IEnumerator CloseLeftoverDialog()
        {
            yield return Ui.Do("count", Ui.Res(Title));
            if (Ui.LastValue == "0") yield break;
            yield return Ui.Do("back", Ui.Res(Title));
            yield return new WaitForSecondsRealtime(1f);
            yield return Ui.Do("count", Ui.Res(Title));
            if (Ui.LastValue != "0")
                yield return Ui.Do("tap", Ui.Res(Positive));
            yield return Ui.Do("wait-gone", Ui.Res(Title));
        }

        /// <summary>Waits until the display is back at <paramref name="rotation"/>, so the next test does not read a turned screen.</summary>
        private static IEnumerator SettleRotation(string rotation)
        {
            float deadline = Time.realtimeSinceStartup + EventTimeoutSeconds;
            do
            {
                yield return Ui.Do("rotation");
            }
            while (Ui.LastValue != rotation && Time.realtimeSinceStartup < deadline);
            Assert.AreEqual(rotation, Ui.LastValue, "the display did not return to its rotation; later tests would read a turned screen");
        }

        /// <summary>
        /// AD-27: tells its thread to give up if it has not called yet, waits for it (not for ever: a
        /// call into the library that never returns is reported, not hung on), and counts its call.
        /// </summary>
        private void StopBackgroundCall(List<Exception> errors)
        {
            if (_background == null) return;
            Thread thread = _background;
            _background = null;
            _abandonBackground!.Cancel();
            if (!thread.Join(TimeSpan.FromSeconds(30)))
            {
                errors.Add(new AssertionException("AD-27's thread did not return within 30 s (a call into the library that does not return?)"));
                return;
            }
            if (_backgroundCode == Api.ErrorNone) _requests++;
            _abandonBackground.Dispose();
            _abandonBackground = null;
        }

        /// <summary>
        /// Runs a step, and the steps it yields, catching what any of them throws into
        /// <paramref name="errors"/>. The Test Framework flattens nested steps itself and, when one
        /// throws, stops without resuming or disposing the method that yielded it; walking the nesting
        /// here keeps the exception inside, so the caller goes on.
        /// </summary>
        private static IEnumerator Safely(IEnumerator step, List<Exception> errors)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(step);
            while (stack.Count > 0)
            {
                IEnumerator top = stack.Peek();
                bool moved;
                Exception? error = null;
                try
                {
                    moved = top.MoveNext();
                }
                catch (Exception e)
                {
                    error = e;
                    moved = false;
                }
                if (error != null)
                {
                    errors.Add(error);
                    yield break;
                }
                if (!moved)
                {
                    stack.Pop();
                    continue;
                }
                if (top.Current is IEnumerator nested)
                    stack.Push(nested);
                else
                    yield return top.Current;
            }
        }

        // ── 1. The sample screen ─────────────────────────────────────────────────

        [UnityTest, Category(AndroidUiDriver.SampleScreenCategory)]
        public IEnumerator AD01_SampleShowDialog_Ok() => SampleAndPress("ShowDialogButton", "Hello from Unity", Positive,
            "OK\nShowDialog buttonText: OK, isSuccess: True, errorMessage: null");

        [UnityTest, Category(AndroidUiDriver.SampleScreenCategory)]
        public IEnumerator AD02_SampleConfirm_Yes() => SampleAndPress("ShowConfirmDialogButton", "Confirmation", Positive,
            "OK\nShowConfirmDialog buttonText: Yes, isSuccess: True, errorMessage: null");

        [UnityTest, Category(AndroidUiDriver.SampleScreenCategory)]
        public IEnumerator AD03_SampleConfirm_No() => SampleAndPress("ShowConfirmDialogButton", "Confirmation", Negative,
            "OK\nShowConfirmDialog buttonText: No, isSuccess: True, errorMessage: null");

        [UnityTest, Category(AndroidUiDriver.SampleScreenCategory)]
        public IEnumerator AD04_SampleSingleChoice_SecondOption()
        {
            yield return OpenSample();
            Request(() => Press(FindButton("ShowSingleChoiceItemDialogButton")!));
            yield return Ui.Do("expect", Ui.Res(Title), "text", Ui.Base64("Please select one"));
            yield return Ui.Do("tap", Ui.Text("Option 2"));
            yield return Ui.Do("expect", Ui.Text("Option 2"), "checked", Ui.Base64("true"));
            yield return Ui.Do("tap", Ui.Res(Positive));
            yield return ExpectLabel("OK\nShowSingleChoiceItemDialog buttonText: OK, checkedItem: 1, isSuccess: True, errorMessage: null");
        }

        [UnityTest, Category(AndroidUiDriver.SampleScreenCategory)]
        public IEnumerator AD05_SampleMultiChoice_TapFirst()
        {
            yield return OpenSample();
            Request(() => Press(FindButton("ShowMultiChoiceItemDialogButton")!));
            yield return Ui.Do("expect", Ui.Res(Title), "text", Ui.Base64("Multiple Selection"));
            yield return Ui.Do("tap", Ui.Text("Item 1"));
            yield return Ui.Do("tap", Ui.Res(Positive));
            yield return ExpectLabel("OK\nShowMultiChoiceItemDialog buttonText: OK, checkedItems: [True,True,False,True], isSuccess: True, errorMessage: null");
        }

        [UnityTest, Category(AndroidUiDriver.SampleScreenCategory)]
        public IEnumerator AD06_SampleTextInput_Typed()
        {
            yield return OpenSample();
            Request(() => Press(FindButton("ShowTextInputDialogButton")!));
            yield return Ui.Do("expect", Ui.Res(Positive), "enabled", Ui.Base64("false"));
            yield return Ui.Do("type", Ui.Class(EditText), "abc123");
            yield return Ui.Do("expect", Ui.Res(Positive), "enabled", Ui.Base64("true"));
            yield return Ui.Do("tap", Ui.Res(Positive));
            yield return ExpectLabel("OK\nShowTextInputDialog buttonText: OK, inputText: abc123, isSuccess: True, errorMessage: null");
        }

        [UnityTest, Category(AndroidUiDriver.SampleScreenCategory)]
        public IEnumerator AD07_SampleLogin_Typed()
        {
            yield return OpenSample();
            Request(() => Press(FindButton("ShowLoginDialogButton")!));
            yield return Ui.Do("expect", Ui.Res(Positive), "enabled", Ui.Base64("false"));
            yield return Ui.Do("type", Ui.Class(EditText, 0), "user01");
            yield return Ui.Do("expect", Ui.Res(Positive), "enabled", Ui.Base64("false"));
            yield return Ui.Do("type", Ui.Class(EditText, 1), "pass02");
            yield return Ui.Do("tap", Ui.Res(Positive));
            // The sample shows the password by its length (5.3; 1.x showed it as typed).
            yield return ExpectLabel("OK\nShowLoginDialog buttonText: Login, username: user01, password: (6 characters), isSuccess: True, errorMessage: null");
        }

        [UnityTest, Category(AndroidUiDriver.SampleScreenCategory)]
        public IEnumerator AD08_SampleConfirm_NotCancelable()
        {
            yield return OpenSample();
            Request(() => Press(FindButton("ShowConfirmDialogButton")!));
            yield return Ui.Do("wait", Ui.Res(Title));
            yield return Ui.Do("back", Ui.Res(Title));
            yield return new WaitForSecondsRealtime(1f);
            yield return Ui.Do("count", Ui.Res(Title));
            Assert.AreEqual("1", Ui.LastValue, "Back closed a dialog that is not cancelable");
            yield return Ui.Do("tap-outside", Ui.Res(Title));
            yield return new WaitForSecondsRealtime(1f);
            yield return Ui.Do("count", Ui.Res(Title));
            Assert.AreEqual("1", Ui.LastValue, "an outside tap closed a dialog that is not cancelable on touch outside");
            Assert.IsEmpty(_events, "a dialog that stayed open reported a result");
            yield return Ui.Do("tap", Ui.Res(Positive));
            yield return ExpectLabel("OK\nShowConfirmDialog buttonText: Yes, isSuccess: True, errorMessage: null");
            Assert.AreEqual(1, _events.Count, string.Join(" | ", _events));
        }

        [UnityTest, Category(AndroidUiDriver.SampleScreenCategory)]
        public IEnumerator AD09_Home_AndOpenAgain()
        {
            yield return OpenSample();
            Press(FindButton("HomeButton")!);
            yield return WaitFor(() => FindButton(TopMenuDialogButton) != null && FindButton("ShowDialogButton") == null, "the top menu");
            yield return Open(TopMenuDialogButton, "ShowDialogButton");
            Request(() => Press(FindButton("ShowDialogButton")!));
            yield return Ui.Do("tap", Ui.Res(Positive));
            yield return ExpectLabel("OK\nShowDialog buttonText: OK, isSuccess: True, errorMessage: null");
        }

        // ── 2.1 Dismissed, negative, outside tap ─────────────────────────────────

        [UnityTest]
        public IEnumerator AD10_Dialog_Back() => ShowAndAct(
            () => Manager.ShowDialog("t10", "m10"), Back, "Dialog|Cancel|True|");

        [UnityTest]
        public IEnumerator AD11_Confirm_OutsideTap() => ShowAndAct(
            () => Manager.ShowConfirmDialog("t11", "m11"), OutsideTap, "Confirm|Cancel|True|");

        [UnityTest]
        public IEnumerator AD12_SingleChoice_SelectThenBack() => ShowAndAct(
            () => Manager.ShowSingleChoiceItemDialog("t12", new[] { "a", "b", "c" }),
            () => Sequence(Ui.Do("tap", Ui.Text("b")), Back()),
            // The selection of a dismissed dialog is not reported (5.3, J-D5; 1.x reported 1).
            "SingleChoice|Cancel|null|True|");

        [UnityTest]
        public IEnumerator AD13_SingleChoice_Negative() => ShowAndAct(
            () => Manager.ShowSingleChoiceItemDialog("t13", new[] { "a", "b", "c" }), () => Ui.Do("tap", Ui.Res(Negative)),
            "SingleChoice|Cancel|null|True|null");

        [UnityTest]
        public IEnumerator AD14_MultiChoice_TapThenBack() => ShowAndAct(
            () => Manager.ShowMultiChoiceItemDialog("t14", new[] { "a", "b", "c" }),
            () => Sequence(Ui.Do("tap", Ui.Text("a")), Back()),
            // The states of a dismissed dialog are not reported (5.3, J-D5; 1.x reported True,False,False).
            "MultiChoice|Cancel|null|True|");

        [UnityTest]
        public IEnumerator AD15_MultiChoice_Negative() => ShowAndAct(
            () => Manager.ShowMultiChoiceItemDialog("t15", new[] { "a", "b", "c" }), () => Ui.Do("tap", Ui.Res(Negative)),
            "MultiChoice|Cancel|null|True|null");

        [UnityTest]
        public IEnumerator AD16_TextInput_Back() => ShowAndAct(
            () => Manager.ShowTextInputDialog("t16", "m16"), Back, "TextInput|Cancel||True|");

        [UnityTest]
        public IEnumerator AD17_TextInput_Negative() => ShowAndAct(
            () => Manager.ShowTextInputDialog("t17", "m17"), () => Ui.Do("tap", Ui.Res(Negative)),
            "TextInput|Cancel|null|True|null");

        [UnityTest]
        public IEnumerator AD18_Login_Back() => ShowAndAct(
            () => Manager.ShowLoginDialog("t18", "m18"), Back, "Login|Cancel|||True|");

        [UnityTest]
        public IEnumerator AD19_Login_Negative() => ShowAndAct(
            () => Manager.ShowLoginDialog("t19", "m19"), () => Ui.Do("tap", Ui.Res(Negative)),
            "Login|Cancel|null|null|True|null");

        [UnityTest]
        public IEnumerator AD20_TextInput_EmptyAllowed() => ShowAndAct(
            () => Manager.ShowTextInputDialog("t20", "m20", enablePositiveButtonWhenEmpty: true),
            () => Ui.Do("tap", Ui.Res(Positive)), "TextInput|OK||True|null");

        // ── 2.2 Values ───────────────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator AD21_MultiChoice_ShorterCheckedItems_ArePadded() => ShowAndAct(
            () => Manager.ShowMultiChoiceItemDialog("t21", new[] { "a", "b", "c", "d" }, new[] { true, false }),
            () => Ui.Do("tap", Ui.Res(Positive)), "MultiChoice|OK|True,False,False,False|True|null");

        [UnityTest]
        public IEnumerator AD22_MultiChoice_LongerCheckedItems_AreCut() => ShowAndAct(
            () => Manager.ShowMultiChoiceItemDialog("t22", new[] { "a", "b" }, new[] { true, false, true, false }),
            // Cut to the items (5.3, J-D4; 1.x returned all four).
            () => Ui.Do("tap", Ui.Res(Positive)), "MultiChoice|OK|True,False|True|null");

        [UnityTest]
        public IEnumerator AD23_SingleChoice_NoneChecked() => ShowAndAct(
            () => Manager.ShowSingleChoiceItemDialog("t23", new[] { "a", "b" }, checkedItem: -1),
            () => Ui.Do("tap", Ui.Res(Positive)), "SingleChoice|OK|null|True|null");

        [UnityTest]
        public IEnumerator AD24_NonAscii_IsShown()
        {
            const string text = "ダイアログ-𠮷";
            Request(() => Manager.ShowConfirmDialog(text, text + "-m", "no", text));
            yield return Ui.Do("expect", Ui.Res(Title), "text", Ui.Base64(text));
            yield return Ui.Do("expect", Ui.Res(Message), "text", Ui.Base64(text + "-m"));
            yield return Ui.Do("expect", Ui.Res(Positive), "text", Ui.Base64(text));
            yield return Ui.Do("tap", Ui.Res(Positive));
            yield return ExpectEvent($"Confirm|{text}|True|null");

            Request(() => Manager.ShowSingleChoiceItemDialog(text, new[] { text + "-1", "b" }));
            yield return Ui.Do("expect", Ui.Text(text + "-1"), "checked", Ui.Base64("true"));
            yield return Ui.Do("tap", Ui.Res(Positive));
            yield return ExpectEvent("SingleChoice|OK|0|True|null");
        }

        // ── 2.3 Timing ───────────────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator AD25_NoEventBeforeShowReturns()
        {
            Request(() => Manager.ShowConfirmDialog("t25", "m25"));
            Assert.IsEmpty(_events, "an event arrived before ShowConfirmDialog returned");
            yield return Ui.Do("tap", Ui.Res(Positive));
            yield return ExpectEvent("Confirm|Yes|True|null");
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.AreEqual(1, _events.Count, string.Join(" | ", _events));
        }

        [UnityTest]
        public IEnumerator AD28_Rotate_DialogStays()
        {
            // The app turns the display itself. Auto-rotation follows the sensor, so with the device
            // lying on a desk the UI driver's user_rotation would not turn it (2026-10-11).
            // The teardown turns the display back and waits for it to settle.
            _orientationBefore = Screen.orientation;
            yield return Ui.Do("rotation");
            string original = Ui.LastValue;
            _rotationBefore = original;
            Request(() => Manager.ShowConfirmDialog("t28", "m28"));
            yield return Ui.Do("wait", Ui.Res(Title));
            Screen.orientation = original == "1" ? ScreenOrientation.Portrait : ScreenOrientation.LandscapeLeft;
            string turned = original;
            float deadline = Time.realtimeSinceStartup + EventTimeoutSeconds;
            while (turned == original && Time.realtimeSinceStartup < deadline)
            {
                yield return Ui.Do("rotation");
                turned = Ui.LastValue;
            }
            Assert.AreNotEqual(original, turned, "the display did not turn");
            yield return Ui.Do("count", Ui.Res(Title));
            Assert.AreEqual("1", Ui.LastValue, "the dialog did not survive the rotation");
            yield return Ui.Do("tap", Ui.Res(Positive));
            yield return ExpectEvent("Confirm|Yes|True|null");
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.AreEqual(1, _events.Count, string.Join(" | ", _events));
        }

        // ── 2.4 Synchronous failures ─────────────────────────────────────────────

        // These are logged as warnings (5.3, J-D13; 1.x logged errors), which the Test Framework does not fail.

        [Test]
        public void AD29_EmptyTitle()
        {
            Request(() => Manager.ShowDialog("", "message"));
            Assert.AreEqual(new[] { "Dialog|null|False|Title cannot be null or empty." }, _events);
        }

        [Test]
        public void AD30_EmptyMessage()
        {
            Request(() => Manager.ShowConfirmDialog("title", ""));
            Assert.AreEqual(new[] { "Confirm|null|False|Message cannot be null or empty." }, _events);
        }

        [Test]
        public void AD31_EmptyItems()
        {
            Request(() => Manager.ShowSingleChoiceItemDialog("title", new string[0]));
            Assert.AreEqual(new[] { "SingleChoice|null|null|False|singleChoiceItems cannot be null or empty." }, _events);
        }

        [UnityTest]
        public IEnumerator AD32_NullItem_FailsAndShowsNothing() => FailsAndShowsNothing(
            () => Manager.ShowSingleChoiceItemDialog("t32", new[] { "a", null! }),
            "SingleChoice|null|null|False|singleChoiceItems cannot contain null.");

        [UnityTest]
        public IEnumerator AD33_CheckedItemOutOfRange_FailsAndShowsNothing() => FailsAndShowsNothing(
            // 1.x showed the dialog and returned 2 (5.3, J-D3).
            () => Manager.ShowSingleChoiceItemDialog("t33", new[] { "a", "b" }, checkedItem: 2),
            "SingleChoice|null|null|False|checkedItem is out of range.");

        [UnityTest]
        public IEnumerator AD34_MultiChoiceNullItem_FailsAndShowsNothing() => FailsAndShowsNothing(
            () => Manager.ShowMultiChoiceItemDialog("t34", new[] { null!, "b" }),
            "MultiChoice|null|null|False|multiChoiceItems cannot contain null.");

        // ── 2.3 Only the C ABI ───────────────────────────────────────────────────────────────────

        /// <summary>AD-26: the completion path, without the UI: the library's cancel.</summary>
        [UnityTest]
        public IEnumerator AD26_Cancel_CompletesOnceAsCanceled()
        {
            _ = Manager;
            var allocated = new List<IntPtr>();
            IntPtr requestId = Marshal.AllocHGlobal(sizeof(ulong));
            int code;
            ulong id;
            try
            {
                Api.ConfirmRequest request = Api.BuildConfirm("t26", "m26", "No", "Yes", true, true, allocated);
                code = Api.ntk_dialog_show_confirm_async(ref request, AndroidDialogManager.s_callbackPointer,
                    AndroidDialogManager.KindData(Api.Kind.Confirm), IntPtr.Zero, requestId);
                id = unchecked((ulong)Marshal.ReadInt64(requestId));
            }
            finally
            {
                Marshal.FreeHGlobal(requestId);
                AndroidNativeToolkitCApi.FreeAll(allocated);
            }
            if (code == Api.ErrorNone) _requests++;
            Assert.AreEqual(Api.ErrorNone, code);
            Assert.AreNotEqual(0UL, id, "the request ID");
            yield return Ui.Do("wait", Ui.Res(Title));
            Assert.AreEqual(Api.ErrorNone, Api.ntk_dialog_cancel(id));
            // The completion reaches the callback on the Android main thread while Unity's main
            // thread sleeps here; the event must still wait for the queue's next run (J-D14).
            Thread.Sleep(300);
            Assert.IsEmpty(_events, "the completion was raised outside Unity's main thread's queue");
            yield return ExpectEvent("Confirm|null|False|Failed to showConfirmDialog: the dialog was canceled.");
            yield return Ui.Do("wait-gone", Ui.Res(Title));
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.AreEqual(1, _events.Count, string.Join(" | ", _events));
        }

        /// <summary>
        /// AD-27 (E-8): a call made while the app is in the background completes as NOT_FOREGROUND.
        /// Unity's main thread is paused then, so another thread makes the call once the UI driver
        /// reports the app's activity STOPPED, and the event arrives when the app is back in front.
        /// </summary>
        [UnityTest]
        public IEnumerator AD27_InTheBackground_CompletesAsNotForeground()
        {
            _ = Manager;
            // The thread works out the answer file's path, which exists once the UI driver is ready.
            yield return Ui.Do("count", Ui.Res(Title));
            int number = Ui.NextNumber;
            string? threadError = null;
            _backgroundCode = -1;
            var abandonSource = new CancellationTokenSource();
            CancellationToken abandon = abandonSource.Token;
            var thread = new Thread(() =>
            {
                try
                {
                    if (!Ui.WaitForAnswerFile(number, TimeSpan.FromSeconds(20), abandon))
                    {
                        threadError = "the UI driver did not report the app stopped";
                        return;
                    }
                    if (abandon.IsCancellationRequested) return;
                    var allocated = new List<IntPtr>();
                    try
                    {
                        Api.AlertRequest request = Api.BuildAlert("t27", "m27", "OK", true, true, allocated);
                        _backgroundCode = Api.ntk_dialog_show_alert_async(ref request, AndroidDialogManager.s_callbackPointer,
                            AndroidDialogManager.KindData(Api.Kind.Alert), IntPtr.Zero, IntPtr.Zero);
                    }
                    finally
                    {
                        AndroidNativeToolkitCApi.FreeAll(allocated);
                    }
                }
                catch (Exception e)
                {
                    threadError = e.ToString();
                }
            });
            thread.Start();
            // Kept only once started: the teardown stops the thread and counts its call, also when this
            // test fails.
            _abandonBackground = abandonSource;
            _background = thread;
            yield return Ui.Do("background", "3000");
            yield return Eventually(() => !thread.IsAlive, _ => { }, EventTimeoutSeconds);
            Assert.IsFalse(thread.IsAlive, "the thread did not finish");
            Assert.IsNull(threadError, threadError);
            Assert.AreEqual(Api.ErrorNone, _backgroundCode, "the call was accepted");
            yield return ExpectEvent("Dialog|null|False|Failed to showDialog: the app is not in the foreground.");
            yield return Ui.Do("count", Ui.Res(Title));
            Assert.AreEqual("0", Ui.LastValue, "no dialog was shown");
        }

        /// <summary>
        /// AD-36: when code has destroyed the callback queue's object, the next Show* makes it again,
        /// so the result still arrives (and no stranded-callback error is logged, which would fail the test).
        /// </summary>
        [UnityTest]
        public IEnumerator AD36_QueueObjectDestroyed_ShowMakesItAgain()
        {
            _ = Manager;
            var pump = UnityEngine.Object.FindFirstObjectByType<AndroidNativeCallbackQueue>();
            Assert.IsNotNull(pump, "the manager's Awake made the queue's object");
            UnityEngine.Object.DestroyImmediate(pump!.gameObject);
            Request(() => Manager.ShowConfirmDialog("t36", "m36"));
            yield return Ui.Do("tap", Ui.Res(Positive));
            yield return ExpectEvent("Confirm|Yes|True|null");
        }

        /// <summary>
        /// AD-37: Show* called from another thread, as 1.x allowed: no exception reaches the caller,
        /// and the result arrives on Unity's main thread.
        /// </summary>
        [UnityTest]
        public IEnumerator AD37_ShowFromAnotherThread()
        {
            _ = Manager;
            Exception? thrown = null;
            var thread = new Thread(() =>
            {
                try
                {
                    Manager.ShowConfirmDialog("t37", "m37");
                }
                catch (Exception e)
                {
                    thrown = e;
                }
            });
            thread.Start();
            thread.Join();
            Assert.IsNull(thrown, thrown?.ToString());
            _requests++;
            yield return Ui.Do("tap", Ui.Res(Positive));
            yield return ExpectEvent("Confirm|Yes|True|null");
            // The teardown checks that the event was raised on Unity's main thread.
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private static AndroidDialogManager Manager => AndroidDialogManager.Instance;

        /// <summary>Makes a call that reports through one event, and counts it for the teardown.</summary>
        private void Request(Action call)
        {
            call();
            // Counted after the call: a call that throws reports nothing, and should not add a second failure.
            _requests++;
        }

        private IEnumerator OpenSample()
        {
            if (!SampleSceneIsInThePlayer())
                Assert.Ignore("The sample scene is not in this player (the Test Android™ Profile builds the test scene only).");
            yield return LoadAtTopMenu(TopMenuDialogButton);
            yield return Open(TopMenuDialogButton, "ShowDialogButton");
        }

        private IEnumerator SampleAndPress(string button, string title, string answer, string expected)
        {
            yield return OpenSample();
            Request(() => Press(FindButton(button)!));
            yield return Ui.Do("expect", Ui.Res(Title), "text", Ui.Base64(title));
            yield return Ui.Do("tap", Ui.Res(answer));
            yield return ExpectLabel(expected);
        }

        private static IEnumerator ExpectLabel(string expected)
        {
            yield return Eventually(() => LabelText(ResultLabel) == expected, _ => { }, EventTimeoutSeconds);
            Assert.AreEqual(expected, LabelText(ResultLabel), "what the sample screen shows");
        }

        private IEnumerator ShowAndAct(Action show, Func<IEnumerator> act, string expected)
        {
            Request(show);
            yield return Ui.Do("wait", Ui.Res(Title));
            yield return act();
            yield return ExpectEvent(expected);
        }

        /// <summary>Calls that fail before any native call: the event arrives inside the call, and no dialog appears.</summary>
        private IEnumerator FailsAndShowsNothing(Action show, string expected)
        {
            Request(show);
            Assert.AreEqual(new[] { expected }, _events);
            yield return new WaitForSecondsRealtime(1f);
            yield return Ui.Do("count", Ui.Res(Title));
            Assert.AreEqual("0", Ui.LastValue, "no dialog was shown");
        }

        private IEnumerator ExpectEvent(string expected)
        {
            yield return Eventually(() => _events.Contains(expected), _ => { }, EventTimeoutSeconds);
            Assert.That(_events, Does.Contain(expected));
        }

        private static IEnumerator Back() => Ui.Do("back", Ui.Res(Title));

        private static IEnumerator OutsideTap() => Ui.Do("tap-outside", Ui.Res(Title));

        private static IEnumerator Sequence(params IEnumerator[] steps)
        {
            foreach (IEnumerator step in steps) yield return step;
        }

        private static string Value(string? text) => text ?? "null";

        private static string Value(int? number) => number?.ToString() ?? "null";

        private static string Value(bool[]? states) => states == null ? "null" : string.Join(",", states);

        private void Add(string entry)
        {
            _eventThreads.Add(Thread.CurrentThread.ManagedThreadId);
            _events.Add(entry);
        }

        private void OnDialog(string? button, bool ok, string? error) => Add($"Dialog|{Value(button)}|{ok}|{Value(error)}");
        private void OnConfirm(string? button, bool ok, string? error) => Add($"Confirm|{Value(button)}|{ok}|{Value(error)}");
        private void OnSingle(string? button, int? index, bool ok, string? error) => Add($"SingleChoice|{Value(button)}|{Value(index)}|{ok}|{Value(error)}");
        private void OnMulti(string? button, bool[]? states, bool ok, string? error) => Add($"MultiChoice|{Value(button)}|{Value(states)}|{ok}|{Value(error)}");
        private void OnTextInput(string? button, string? text, bool ok, string? error) => Add($"TextInput|{Value(button)}|{Value(text)}|{ok}|{Value(error)}");
        private void OnLogin(string? button, string? user, string? password, bool ok, string? error) => Add($"Login|{Value(button)}|{Value(user)}|{Value(password)}|{ok}|{Value(error)}");

        private void Subscribe()
        {
            if (_subscribed) return;
            Manager.DialogResult += OnDialog;
            Manager.ConfirmDialogResult += OnConfirm;
            Manager.SingleChoiceItemDialogResult += OnSingle;
            Manager.MultiChoiceItemDialogResult += OnMulti;
            Manager.TextInputDialogResult += OnTextInput;
            Manager.LoginDialogResult += OnLogin;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            Manager.DialogResult -= OnDialog;
            Manager.ConfirmDialogResult -= OnConfirm;
            Manager.SingleChoiceItemDialogResult -= OnSingle;
            Manager.MultiChoiceItemDialogResult -= OnMulti;
            Manager.TextInputDialogResult -= OnTextInput;
            Manager.LoginDialogResult -= OnLogin;
            _subscribed = false;
        }
    }
}
#endif
#endif
