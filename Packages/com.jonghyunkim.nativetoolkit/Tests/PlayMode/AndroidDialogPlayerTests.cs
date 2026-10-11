#nullable enable

#if UNITY_ANDROID
#if !UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using JonghyunKim.NativeToolkit.Runtime.Android.Dialog;
using static JonghyunKim.NativeToolkit.Tests.AndroidSampleScreenDriver;
using Ui = JonghyunKim.NativeToolkit.Tests.AndroidUiDriver;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// Layer 2b tests of the Android Dialog on a device: the sample screen and the manager, with the
    /// OS dialogs pressed by the host-side UI driver. Test IDs (AD-xx) and expectations are in
    /// artifact/android/dialog/designs/2026-10-11-android-dialog-ui-test-plan-v1.md.
    /// </summary>
    /// <remarks>
    /// These expectations are the 1.x bridge's, recorded before the move to the C ABI (Dialog design
    /// v4, 5.5 step 3). The migration changes only the ones listed as known differences there.
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

        private readonly List<string> _events = new();
        private bool _subscribed;

        [SetUp]
        public void SetUp()
        {
            if (Application.platform != RuntimePlatform.Android)
                Assert.Ignore("Device-only.");
            _events.Clear();
            Subscribe();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // Unsubscribe first: when a later step fails, the next test must not count this one's
            // events twice.
            Unsubscribe();

            // A test that failed with its dialog open would leave it for the next one to press.
            // Back closes a cancelable one; one that is not cancelable needs a button.
            yield return Ui.Do("count", Ui.Res(Title));
            if (Ui.LastValue != "0")
            {
                yield return Ui.Do("back", Ui.Res(Title));
                yield return new WaitForSecondsRealtime(1f);
                yield return Ui.Do("count", Ui.Res(Title));
                if (Ui.LastValue != "0")
                    yield return Ui.Do("tap", Ui.Res(Positive));
                yield return Ui.Do("wait-gone", Ui.Res(Title));
            }
            // A dialog closed here reports on a later frame; let it arrive before the next test.
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Unload();
        }

        // ── 1. The sample screen ─────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator AD01_SampleShowDialog_Ok() => SampleAndPress("ShowDialogButton", "Hello from Unity", Positive,
            "OK\nShowDialog buttonText: OK, isSuccess: True, errorMessage: null");

        [UnityTest]
        public IEnumerator AD02_SampleConfirm_Yes() => SampleAndPress("ShowConfirmDialogButton", "Confirmation", Positive,
            "OK\nShowConfirmDialog buttonText: Yes, isSuccess: True, errorMessage: null");

        [UnityTest]
        public IEnumerator AD03_SampleConfirm_No() => SampleAndPress("ShowConfirmDialogButton", "Confirmation", Negative,
            "OK\nShowConfirmDialog buttonText: No, isSuccess: True, errorMessage: null");

        [UnityTest]
        public IEnumerator AD04_SampleSingleChoice_SecondOption()
        {
            yield return OpenSample();
            Press(FindButton("ShowSingleChoiceItemDialogButton")!);
            yield return Ui.Do("expect", Ui.Res(Title), "text", Ui.Base64("Please select one"));
            yield return Ui.Do("tap", Ui.Text("Option 2"));
            yield return Ui.Do("expect", Ui.Text("Option 2"), "checked", Ui.Base64("true"));
            yield return Ui.Do("tap", Ui.Res(Positive));
            yield return ExpectLabel("OK\nShowSingleChoiceItemDialog buttonText: OK, checkedItem: 1, isSuccess: True, errorMessage: null");
        }

        [UnityTest]
        public IEnumerator AD05_SampleMultiChoice_TapFirst()
        {
            yield return OpenSample();
            Press(FindButton("ShowMultiChoiceItemDialogButton")!);
            yield return Ui.Do("expect", Ui.Res(Title), "text", Ui.Base64("Multiple Selection"));
            yield return Ui.Do("tap", Ui.Text("Item 1"));
            yield return Ui.Do("tap", Ui.Res(Positive));
            yield return ExpectLabel("OK\nShowMultiChoiceItemDialog buttonText: OK, checkedItems: [True,True,False,True], isSuccess: True, errorMessage: null");
        }

        [UnityTest]
        public IEnumerator AD06_SampleTextInput_Typed()
        {
            yield return OpenSample();
            Press(FindButton("ShowTextInputDialogButton")!);
            yield return Ui.Do("expect", Ui.Res(Positive), "enabled", Ui.Base64("false"));
            yield return Ui.Do("type", Ui.Class(EditText), "abc123");
            yield return Ui.Do("expect", Ui.Res(Positive), "enabled", Ui.Base64("true"));
            yield return Ui.Do("tap", Ui.Res(Positive));
            yield return ExpectLabel("OK\nShowTextInputDialog buttonText: OK, inputText: abc123, isSuccess: True, errorMessage: null");
        }

        [UnityTest]
        public IEnumerator AD07_SampleLogin_Typed()
        {
            yield return OpenSample();
            Press(FindButton("ShowLoginDialogButton")!);
            yield return Ui.Do("expect", Ui.Res(Positive), "enabled", Ui.Base64("false"));
            yield return Ui.Do("type", Ui.Class(EditText, 0), "user01");
            yield return Ui.Do("expect", Ui.Res(Positive), "enabled", Ui.Base64("false"));
            yield return Ui.Do("type", Ui.Class(EditText, 1), "pass02");
            yield return Ui.Do("tap", Ui.Res(Positive));
            // 1.x shows the password as typed; the migration shows its length (UI test plan, AD-07).
            yield return ExpectLabel("OK\nShowLoginDialog buttonText: Login, username: user01, password: pass02, isSuccess: True, errorMessage: null");
        }

        [UnityTest]
        public IEnumerator AD08_SampleConfirm_NotCancelable()
        {
            yield return OpenSample();
            Press(FindButton("ShowConfirmDialogButton")!);
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

        [UnityTest]
        public IEnumerator AD09_Home_AndOpenAgain()
        {
            yield return OpenSample();
            Press(FindButton("HomeButton")!);
            yield return WaitFor(() => FindButton(TopMenuDialogButton) != null && FindButton("ShowDialogButton") == null, "the top menu");
            yield return Open(TopMenuDialogButton, "ShowDialogButton");
            Press(FindButton("ShowDialogButton")!);
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
            // 1.x reports the current selection; the C ABI does not (design 5.3, J-D5).
            "SingleChoice|Cancel|1|True|");

        [UnityTest]
        public IEnumerator AD13_SingleChoice_Negative() => ShowAndAct(
            () => Manager.ShowSingleChoiceItemDialog("t13", new[] { "a", "b", "c" }), () => Ui.Do("tap", Ui.Res(Negative)),
            "SingleChoice|Cancel|null|True|null");

        [UnityTest]
        public IEnumerator AD14_MultiChoice_TapThenBack() => ShowAndAct(
            () => Manager.ShowMultiChoiceItemDialog("t14", new[] { "a", "b", "c" }),
            () => Sequence(Ui.Do("tap", Ui.Text("a")), Back()),
            // 1.x reports the current states; the C ABI does not (design 5.3, J-D5).
            "MultiChoice|Cancel|True,False,False|True|");

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
        public IEnumerator AD22_MultiChoice_LongerCheckedItems_Recorded() => ShowAndRecord(
            () => Manager.ShowMultiChoiceItemDialog("t22", new[] { "a", "b" }, new[] { true, false, true, false }));

        [UnityTest]
        public IEnumerator AD23_SingleChoice_NoneChecked_Recorded() => ShowAndRecord(
            () => Manager.ShowSingleChoiceItemDialog("t23", new[] { "a", "b" }, checkedItem: -1));

        [UnityTest]
        public IEnumerator AD24_NonAscii_IsShown()
        {
            const string text = "ダイアログ-𠮷";
            Manager.ShowConfirmDialog(text, text + "-m", "no", text);
            yield return Ui.Do("expect", Ui.Res(Title), "text", Ui.Base64(text));
            yield return Ui.Do("expect", Ui.Res(Message), "text", Ui.Base64(text + "-m"));
            yield return Ui.Do("expect", Ui.Res(Positive), "text", Ui.Base64(text));
            yield return Ui.Do("tap", Ui.Res(Positive));
            yield return ExpectEvent($"Confirm|{text}|True|null");

            Manager.ShowSingleChoiceItemDialog(text, new[] { text + "-1", "b" });
            yield return Ui.Do("expect", Ui.Text(text + "-1"), "checked", Ui.Base64("true"));
            yield return Ui.Do("tap", Ui.Res(Positive));
            yield return ExpectEvent("SingleChoice|OK|0|True|null");
        }

        // ── 2.3 Timing ───────────────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator AD25_NoEventBeforeShowReturns()
        {
            Manager.ShowConfirmDialog("t25", "m25");
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
            ScreenOrientation before = Screen.orientation;
            yield return Ui.Do("rotation");
            string original = Ui.LastValue;
            try
            {
                Manager.ShowConfirmDialog("t28", "m28");
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
            finally
            {
                Screen.orientation = before;
            }
            // Let the display settle before the next test reads the screen.
            float settle = Time.realtimeSinceStartup + EventTimeoutSeconds;
            do
            {
                yield return Ui.Do("rotation");
            }
            while (Ui.LastValue != original && Time.realtimeSinceStartup < settle);
        }

        // ── 2.4 Synchronous failures ─────────────────────────────────────────────

        [Test]
        public void AD29_EmptyTitle()
        {
            // 1.x logs these as errors; the migration lowers them to warnings (design J-D13).
            LogAssert.Expect(LogType.Error, "Title cannot be null or empty.");
            Manager.ShowDialog("", "message");
            Assert.AreEqual(new[] { "Dialog|null|False|Title cannot be null or empty." }, _events);
        }

        [Test]
        public void AD30_EmptyMessage()
        {
            LogAssert.Expect(LogType.Error, "Message cannot be null or empty.");
            Manager.ShowConfirmDialog("title", "");
            Assert.AreEqual(new[] { "Confirm|null|False|Message cannot be null or empty." }, _events);
        }

        [Test]
        public void AD31_EmptyItems()
        {
            LogAssert.Expect(LogType.Error, "singleChoiceItems cannot be null or empty.");
            Manager.ShowSingleChoiceItemDialog("title", new string[0]);
            Assert.AreEqual(new[] { "SingleChoice|null|null|False|singleChoiceItems cannot be null or empty." }, _events);
        }

        [UnityTest]
        public IEnumerator AD33_SingleChoice_OutOfRange_Recorded() => ShowAndRecord(
            () => Manager.ShowSingleChoiceItemDialog("t33", new[] { "a", "b" }, checkedItem: 2));

        // ── Helpers ──────────────────────────────────────────────────────────────

        private static AndroidDialogManager Manager => AndroidDialogManager.Instance;

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
            Press(FindButton(button)!);
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
            show();
            yield return Ui.Do("wait", Ui.Res(Title));
            yield return act();
            yield return ExpectEvent(expected);
        }

        /// <summary>
        /// Shows a dialog whose 1.x behaviour was not known, records what happens, and closes it.
        /// The record goes to the test output; the UI test plan's 1.x column is filled from it.
        /// </summary>
        private IEnumerator ShowAndRecord(Action show)
        {
            show();
            yield return Ui.Do("wait", Ui.Res(Title));
            yield return Ui.Do("tap", Ui.Res(Positive));
            yield return Eventually(() => _events.Count > 0, _ => { }, EventTimeoutSeconds);
            TestContext.WriteLine($"[AD-record] {TestContext.CurrentContext.Test.Name}: {string.Join(" | ", _events)}");
            Debug.Log($"[AD-record] {TestContext.CurrentContext.Test.Name}: {string.Join(" | ", _events)}");
            Assert.AreEqual(1, _events.Count, "one event per dialog: " + string.Join(" | ", _events));
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

        private void OnDialog(string? button, bool ok, string? error) => _events.Add($"Dialog|{Value(button)}|{ok}|{Value(error)}");
        private void OnConfirm(string? button, bool ok, string? error) => _events.Add($"Confirm|{Value(button)}|{ok}|{Value(error)}");
        private void OnSingle(string? button, int? index, bool ok, string? error) => _events.Add($"SingleChoice|{Value(button)}|{Value(index)}|{ok}|{Value(error)}");
        private void OnMulti(string? button, bool[]? states, bool ok, string? error) => _events.Add($"MultiChoice|{Value(button)}|{Value(states)}|{ok}|{Value(error)}");
        private void OnTextInput(string? button, string? text, bool ok, string? error) => _events.Add($"TextInput|{Value(button)}|{Value(text)}|{ok}|{Value(error)}");
        private void OnLogin(string? button, string? user, string? password, bool ok, string? error) => _events.Add($"Login|{Value(button)}|{Value(user)}|{Value(password)}|{ok}|{Value(error)}");

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
