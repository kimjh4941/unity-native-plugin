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

        /// <summary>
        /// Opens the Dialog screen, sends the driver's request, presses the sample button, waits for the
        /// driver, then for the screen's result.
        /// </summary>
        private static IEnumerator SampleAndAnswer(string sampleButton, string alertButton, string expected)
        {
            yield return LoadAtTopMenu(TopMenuDialog);
            yield return Open(TopMenuDialog, sampleButton);
            yield return IosUiDriver.Send("alert-tap", IosUiDriver.Text(alertButton));
            int request = IosUiDriver.LastSent;
            Press(sampleButton);
            yield return IosUiDriver.Wait(request, IosUiDriverProtocol.AnswerTimeoutSeconds);
            yield return WaitFor(() => LabelText(ResultLabel) == expected, $"the result \"{expected.Replace("\n", " / ")}\"");
        }
    }
}
#endif
