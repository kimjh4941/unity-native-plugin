#nullable enable

// iOS player only: the sample scene.
#if UNITY_IOS && !UNITY_EDITOR
using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;
using static JonghyunKim.NativeToolkit.Tests.IosSampleScreenDriver;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// The iOS Clipboard sample screen (artifact/topics/ios-c-abi-2, UI driver design v3): the buttons are pressed
    /// on the sample's fixed named pasteboard only, and each result line is read back. A named pasteboard opens
    /// no system screen, so the UI driver is not needed; the general pasteboard is never touched (the screen
    /// starts on it, so the first press switches; <see cref="IosSampleScreenDriver.Press(string)"/> refuses
    /// anything else on a device while the scope reads "general").
    /// </summary>
    [Category(IosPlayerTestSupport.Category)]
    [Category(IosUiDriver.SampleScreenCategory)]
    public sealed class IosClipboardSamplePlayerTests
    {
        private const string ResultLabel = "ResultTextBlock";
        private const string StatusLabel = IosSampleScreenDriver.ClipboardStatusLabel;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return Unload();
        }

        [UnityTest]
        public IEnumerator CS01_NamedScope_CopiesReadsAndDetects()
        {
            yield return OpenOnNamedScope();
            yield return PressAndExpect("CopyPlainTextButton", "copy.plainText", "OK");
            yield return PressAndExpect("ReadButton", "read", "OK");
            yield return PressAndExpect("CopyHtmlTextButton", "copy.htmlText", "OK");
            yield return PressAndExpect("CopyUrlButton", "copy.url", "OK");
            yield return PressAndExpect("CopyMultipleTextButton", "copy.multipleText", "OK");
            yield return PressAndExpect("CopyMultiRepresentationButton", "copy.multiRepresentation", "OK");
            yield return PressAndExpect("CopyColorButton", "copy.color", "OK");
            yield return PressAndExpect("CopyCustomDataButton", "copy.customData", "OK");
            yield return PressAndExpect("SnapshotButton", "snapshot", "OK");
            yield return PressAndExpect("CopyDetectionFixtureButton", "copy.detectionFixture", "OK");
            yield return PressAndExpect("DetectValuesButton", "detect.values", "OK");
            yield return PressAndExpect("DetectPatternsButton", "detect.patterns", "OK");
        }

        [UnityTest]
        public IEnumerator CS02_NamedScope_RejectsBadInput()
        {
            // The Manager logs each rejection as an error; that is what these buttons are for.
            LogAssert.ignoreFailingMessages = true;
            yield return OpenOnNamedScope();
            yield return PressAndExpect("ErrCopyInvalidUrlButton", "err.copyInvalidUrl", "NG");
            yield return PressAndExpect("ErrCopyInvalidUtiButton", "err.copyInvalidUti", "NG");
            yield return PressAndExpect("ErrCopyMultipleEmptyButton", "err.copyMultipleEmpty", "NG");
            yield return PressAndExpect("ErrCopyMultiRepEmptyButton", "err.copyMultiRepEmpty", "NG");
            yield return PressAndExpect("ErrCopyColorOutOfRangeButton", "err.copyColorOutOfRange", "NG");
            yield return PressAndExpect("ErrDetectEmptyPatternsButton", "err.detectEmptyPatterns", "NG");
            yield return PressAndExpect("ErrReadDataInvalidUtiButton", "err.readDataInvalidUti", "NG");
        }

        private static IEnumerator OpenOnNamedScope()
        {
            yield return LoadAtTopMenu(TopMenuClipboard);
            yield return Open(TopMenuClipboard, "CreateNamedPasteboardButton");
            // Creating the named pasteboard also switches the screen to it (UseFixedNamedScope only switches,
            // and a copy to a pasteboard nobody created fails with CLIPBOARD_UNAVAILABLE).
            yield return PressAndExpect("CreateNamedPasteboardButton", "scope.createNamed", "OK");
            string? status = LabelText(StatusLabel);
            Assert.IsTrue(status != null && status.IndexOf("Scope: named", StringComparison.Ordinal) >= 0,
                "the screen did not switch to the named pasteboard");
        }

        /// <summary>Presses a button and waits for its own result line ("[marker] OK", "NG" or "--").</summary>
        private static IEnumerator PressAndExpect(string button, string marker, string mark)
        {
            string wanted = $"[{marker}] {mark}";
            Press(button);
            yield return WaitFor(() => LabelText(ResultLabel)?.Contains(wanted) == true,
                $"\"{wanted}\" after {button} (the screen shows the result line only)");
        }
    }
}
#endif
