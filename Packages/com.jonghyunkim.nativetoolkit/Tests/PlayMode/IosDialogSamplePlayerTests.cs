#nullable enable

// iOS player only: the sample scene and the UI driver.
#if UNITY_IOS && !UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;
using static JonghyunKim.NativeToolkit.Tests.IosSampleScreenDriver;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// The iOS Dialog sample screen end to end (artifact/topics/ios-c-abi-2, UI driver design v3, 11 step 4):
    /// a sample button is pressed from C#, the UI driver presses the alert it opens, and the screen shows the
    /// result. The first user of the iOS UI driver.
    /// </summary>
    [Category(IosPlayerTestSupport.Category)]
    [Category(IosUiDriver.Category)]
    [Category(IosUiDriver.SampleScreenCategory)]
    public sealed class IosDialogSamplePlayerTests
    {
        private const string ResultLabel = "ResultTextBlock";

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return IosUiDriver.WaitForPending();
            yield return Unload();
        }

        [UnityTest]
        public IEnumerator ID01_SampleShowDialog_Ok() => SampleAndAnswer("ShowDialogButton", "OK",
            "OK\nShowDialog buttonText: OK, isSuccess: True, errorMessage: null");

        [UnityTest]
        public IEnumerator ID02_SampleConfirm_Yes() => SampleAndAnswer("ShowConfirmDialogButton", "Yes",
            "OK\nShowConfirmDialog buttonText: Yes, isSuccess: True, errorMessage: null");

        [UnityTest]
        public IEnumerator ID03_SampleConfirm_No() => SampleAndAnswer("ShowConfirmDialogButton", "No",
            "OK\nShowConfirmDialog buttonText: No, isSuccess: True, errorMessage: null");

        [UnityTest]
        public IEnumerator ID04_SampleDestructive_Delete() => SampleAndAnswer("ShowDestructiveDialogButton", "Delete",
            "OK\nShowDestructiveDialog buttonText: Delete, isSuccess: True, errorMessage: null");

        [UnityTest]
        public IEnumerator ID05_SampleDestructive_Cancel() => SampleAndAnswer("ShowDestructiveDialogButton", "Cancel",
            "OK\nShowDestructiveDialog buttonText: Cancel, isSuccess: True, errorMessage: null");

        [UnityTest]
        public IEnumerator ID06_SampleActionSheet_Option() => SampleAndAnswer("ShowActionSheetButton", "Photo Library",
            "OK\nShowActionSheet buttonText: Photo Library, isSuccess: True, errorMessage: null");

        [UnityTest]
        public IEnumerator ID07_SampleActionSheet_Cancel() => SampleAndAnswer("ShowActionSheetButton", "Cancel",
            "OK\nShowActionSheet buttonText: Cancel, isSuccess: True, errorMessage: null");

        [UnityTest]
        public IEnumerator ID08_SampleTextInput_Ok() => SampleAndAnswer("ShowTextInputDialogButton", "OK",
            "OK\nShowTextInputDialog buttonText: OK, inputText: ntk42, isSuccess: True, errorMessage: null",
            ("text", "0", "ntk42"));

        [UnityTest]
        public IEnumerator ID09_SampleTextInput_Cancel() => SampleAndAnswer("ShowTextInputDialogButton", "Cancel",
            "OK\nShowTextInputDialog buttonText: Cancel, inputText: null, isSuccess: True, errorMessage: null");

        // Login dialogs only on the simulator (UI driver design v3, chapter 7: password saving and AutoFill could
        // reach the user's keychain on a device; the harness's ID-12 and ID-16 are simulator-only too).

        [UnityTest, Category(IosUiDriver.SimulatorOnlyCategory)]
        public IEnumerator ID10_SampleLogin_Login() => SampleAndAnswer("ShowLoginDialogButton", "Login",
            "OK\nShowLoginDialog buttonText: Login, username: user1, password: pw1234, isSuccess: True, errorMessage: null",
            ("text", "0", "user1"), ("secure", "0", "pw1234"));

        [UnityTest, Category(IosUiDriver.SimulatorOnlyCategory)]
        public IEnumerator ID11_SampleLogin_Cancel() => SampleAndAnswer("ShowLoginDialogButton", "Cancel",
            "OK\nShowLoginDialog buttonText: Cancel, username: null, password: null, isSuccess: True, errorMessage: null");

        /// <summary>
        /// Opens the Dialog screen, sends the driver's requests (typing into fields first, then the button),
        /// presses the sample button, waits for the driver, then for the screen's result.
        /// </summary>
        private static IEnumerator SampleAndAnswer(string sampleButton, string alertButton, string expected,
            params (string Kind, string Index, string Text)[] typed)
        {
            yield return LoadAtTopMenu(TopMenuDialog);
            yield return Open(TopMenuDialog, sampleButton);
            int first = 0;
            foreach (var field in typed)
            {
                yield return IosUiDriver.Send("alert-type", field.Kind, field.Index, IosUiDriver.Text(field.Text));
                if (first == 0) first = IosUiDriver.LastSent;
            }
            yield return IosUiDriver.Send("alert-tap", IosUiDriver.Text(alertButton));
            int last = IosUiDriver.LastSent;
            Press(sampleButton);
            for (int request = first == 0 ? last : first; request <= last; request++)
                yield return IosUiDriver.Wait(request, IosUiDriverProtocol.AnswerTimeoutSeconds);
            yield return WaitFor(() => LabelText(ResultLabel) == expected, $"the result \"{expected.Replace("\n", " / ")}\"");
        }
    }
}
#endif
