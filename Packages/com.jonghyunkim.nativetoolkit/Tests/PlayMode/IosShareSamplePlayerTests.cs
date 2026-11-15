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
    /// The iOS Share sample screen end to end (artifact/topics/ios-c-abi-2, UI driver design v3): each sample
    /// button that opens the share sheet gets it closed by the UI driver without choosing an activity (only the
    /// Close button is pressed, as in the harness's tests, so no share target on a device is touched), and the
    /// screen shows the cancelled result. The buttons that must fail show their error and open no sheet. The
    /// error texts are the C ABI 2.0.0 ones the harness's SH-9 to SH-12 expect.
    /// </summary>
    [Category(IosPlayerTestSupport.Category)]
    [Category(IosUiDriver.Category)]
    [Category(IosUiDriver.SampleScreenCategory)]
    public sealed class IosShareSamplePlayerTests
    {
        private const string ResultLabel = "ResultTextBlock";
        private const string NoSheetSpanMs = "3000";

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return IosUiDriver.WaitForPending();
            yield return Unload();
        }

        [UnityTest, Timeout(IosUiDriverProtocol.ScreenTestTimeoutMilliseconds)] public IEnumerator SS01_Text() => Cancelled("ShareTextButton", "Share Text");
        [UnityTest, Timeout(IosUiDriverProtocol.ScreenTestTimeoutMilliseconds)] public IEnumerator SS02_Url() => Cancelled("ShareUrlButton", "Share URL");
        [UnityTest, Timeout(IosUiDriverProtocol.ScreenTestTimeoutMilliseconds)] public IEnumerator SS03_UrlPreview() => Cancelled("ShareUrlPreviewButton", "Share URL with Preview");
        [UnityTest, Timeout(IosUiDriverProtocol.ScreenTestTimeoutMilliseconds)] public IEnumerator SS04_Image() => Cancelled("ShareImageButton", "Share Image");
        [UnityTest, Timeout(IosUiDriverProtocol.ScreenTestTimeoutMilliseconds)] public IEnumerator SS05_Images() => Cancelled("ShareImagesButton", "Share Multiple Images");
        [UnityTest, Timeout(IosUiDriverProtocol.ScreenTestTimeoutMilliseconds)] public IEnumerator SS06_File() => Cancelled("ShareFileButton", "Share File");
        [UnityTest, Timeout(IosUiDriverProtocol.ScreenTestTimeoutMilliseconds)] public IEnumerator SS07_Files() => Cancelled("ShareFilesButton", "Share Multiple Files");
        [UnityTest, Timeout(IosUiDriverProtocol.ScreenTestTimeoutMilliseconds)] public IEnumerator SS08_Multiple() => Cancelled("ShareMultipleButton", "Share Text + URL");
        [UnityTest, Timeout(IosUiDriverProtocol.ScreenTestTimeoutMilliseconds)] public IEnumerator SS09_WithSubject() => Cancelled("ShareWithSubjectButton", "Share with Subject");
        [UnityTest, Timeout(IosUiDriverProtocol.ScreenTestTimeoutMilliseconds)] public IEnumerator SS10_ExcludingActivities() => Cancelled("ShareExcludingActivitiesButton", "Share Excluding Activities");

        [UnityTest, Timeout(IosUiDriverProtocol.ScreenTestTimeoutMilliseconds)] public IEnumerator SS11_InvalidUrl() => Fails("ShareInvalidUrlButton", "Share Invalid URL", "Invalid URL.");
        [UnityTest, Timeout(IosUiDriverProtocol.ScreenTestTimeoutMilliseconds)] public IEnumerator SS12_MissingFile() => Fails("ShareMissingFileButton", "Share Missing File", "File not found.");
        [UnityTest, Timeout(IosUiDriverProtocol.ScreenTestTimeoutMilliseconds)] public IEnumerator SS13_MissingImage() => Fails("ShareMissingImageButton", "Share Missing Image", "Failed to load image.");
        [UnityTest, Timeout(IosUiDriverProtocol.ScreenTestTimeoutMilliseconds)] public IEnumerator SS14_Empty() => Fails("ShareEmptyButton", "Share Empty (error)", null);

        /// <summary>
        /// V-11 (UI driver design v3, 3.1): the driver closes the sheet and drops the connection before answering;
        /// the player connects again with the same run and sends the request again; the driver answers from
        /// memory instead of closing a sheet that is no longer there (which could not succeed a second time).
        /// </summary>
        [UnityTest, Timeout(IosUiDriverProtocol.ScreenTestTimeoutMilliseconds)]
        public IEnumerator SS15_AnswerLostToADroppedConnection_ComesFromMemory()
        {
            int reconnects = IosUiDriver.ReconnectCount;
            yield return IosUiDriver.Do("drop-next");
            yield return Cancelled("ShareTextButton", "Share Text");
            Assert.AreEqual(reconnects + 1, IosUiDriver.ReconnectCount, "the connection was not dropped and made again");
        }

        private static IEnumerator Cancelled(string button, string title)
        {
            yield return LoadAtTopMenu(TopMenuShare);
            yield return Open(TopMenuShare, button);
            yield return IosUiDriver.Send("sheet-close");
            int request = IosUiDriver.LastSent;
            Press(button);
            yield return IosUiDriver.Wait(request, IosUiDriverProtocol.ScreenAnswerTimeoutSeconds);
            string expected = $"[{title}] completed=false (cancelled)";
            yield return WaitFor(() => LabelText(ResultLabel) == expected, $"the result \"{expected}\"");
        }

        /// <summary>A button that must fail: no sheet, and the error (the whole text, or any when null).</summary>
        private static IEnumerator Fails(string button, string title, string? error)
        {
            yield return LoadAtTopMenu(TopMenuShare);
            yield return Open(TopMenuShare, button);
            yield return IosUiDriver.Send("sheet-absent", NoSheetSpanMs);
            int request = IosUiDriver.LastSent;
            Press(button);
            string prefix = $"[{title}] Error: ";
            yield return WaitFor(() => LabelText(ResultLabel)?.StartsWith(prefix, System.StringComparison.Ordinal) == true,
                $"an error result for {title}");
            if (error != null)
                Assert.AreEqual(prefix + error, LabelText(ResultLabel));
            yield return IosUiDriver.Wait(request, IosUiDriverProtocol.AnswerTimeoutFor("sheet-absent", new[] { NoSheetSpanMs }));
        }
    }
}
#endif
