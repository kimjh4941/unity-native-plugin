#nullable enable

#if UNITY_EDITOR
using JonghyunKim.NativeToolkit.Runtime.Clipboard;
using NUnit.Framework;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// Tests for the Windows clipboard sample's formatters and its lifecycle tracking.
    /// </summary>
    /// <remarks>
    /// Two rules the screen depends on are pinned here. Content never reaches a line, and a
    /// comparison with no anchor is reported as not applicable rather than as a mismatch: the
    /// second is what stops a round trip being blamed for losing something it was never given.
    /// </remarks>
    public sealed class WindowsClipboardSampleResultTests
    {
        [Test]
        public void ALineCarriesItsSequenceAndKind()
        {
            string line = WindowsClipboardSampleResult.FormatLine(
                7, WindowsClipboardSampleResult.KindCall, "copyPlainText", "OK code=None");

            Assert.AreEqual("#7 [call] copyPlainText OK code=None", line);
        }

        [Test]
        public void ALineWithNothingToSayStopsAfterItsSubject()
        {
            string line = WindowsClipboardSampleResult.FormatLine(
                2, WindowsClipboardSampleResult.KindEvent, "HistoryChanged", string.Empty);

            Assert.AreEqual("#2 [event] HistoryChanged", line);
        }

        /// <remarks>
        /// The About section asks the operator to treat a success carrying a code, and a failure
        /// carrying no message, as defects. Normalising either here would hide the very thing the
        /// screen is watching for.
        /// </remarks>
        [Test]
        public void AContradictoryResultIsShownAsItArrived()
        {
            string success = WindowsClipboardSampleResult.FormatOutcome(
                true, WindowsClipboardErrorCode.Busy, null, string.Empty);
            string failure = WindowsClipboardSampleResult.FormatOutcome(
                false, WindowsClipboardErrorCode.InvalidArgument, null, string.Empty);

            StringAssert.Contains("OK code=Busy", success);
            StringAssert.Contains("NG code=InvalidArgument", failure);
            Assert.IsFalse(failure.Contains("msg="), "a failure with no message must not invent one");
        }

        [Test]
        public void AnAcceptedRequestShowsItsId()
        {
            Assert.AreEqual(
                "#3 [accept] getClipboardHistory requestId=42",
                WindowsClipboardSampleResult.FormatAccept(3, "getClipboardHistory", 42));
        }

        /// <remarks>
        /// Zero is the one value that means no request exists. The outcome still arrives on the
        /// callback, so without saying so the line looks like an ordinary accept.
        /// </remarks>
        [Test]
        public void ARejectedRequestSaysSoRatherThanShowingZero()
        {
            string line = WindowsClipboardSampleResult.FormatAccept(3, "getClipboardHistory", 0);

            StringAssert.Contains("requestId=0", line);
            StringAssert.Contains("rejected", line);
        }

        [Test]
        public void AComparisonWithoutAnAnchorIsNotAMismatch()
        {
            Assert.AreEqual(
                WindowsClipboardSampleResult.NotApplicable,
                WindowsClipboardSampleResult.MatchLabel(0UL, 12345UL));
        }

        [Test]
        public void AComparisonAgainstAnAnchorReportsWhatItFound()
        {
            Assert.AreEqual("match", WindowsClipboardSampleResult.MatchLabel(99UL, 99UL));
            Assert.AreEqual("differ", WindowsClipboardSampleResult.MatchLabel(99UL, 100UL));
        }

        /// <remarks>
        /// The read result carries the text, and this describes it. If the description ever grew a
        /// substring of the content, every device log would become a transcript of the clipboard.
        /// </remarks>
        [Test]
        public void ATextDescriptionNeverQuotesWhatWasRead()
        {
            const string secret = "correct-horse-battery-staple";
            WindowsClipboardTextResult result =
                WindowsClipboardTextResult.Success(WindowsClipboardManager.OperationPastePlainText, secret);

            string described = WindowsClipboardSampleResult.DescribeText(result, 0UL);

            Assert.IsFalse(described.Contains(secret), described);
            StringAssert.Contains($"length={secret.Length}", described);
        }

        [Test]
        public void AByteDescriptionReportsSizeAndMatchOnly()
        {
            byte[] data = { 1, 2, 3, 4 };
            WindowsClipboardBytesResult result =
                WindowsClipboardBytesResult.Success(WindowsClipboardManager.OperationPasteImage, data);

            string described = WindowsClipboardSampleResult.DescribeBytes(
                result, WindowsClipboardSampleFixtures.HashOf(data));

            StringAssert.Contains("size=4", described);
            StringAssert.Contains("match=match", described);
        }

        /// <remarks>
        /// Two different successes that a single number would flatten into one. A clipboard holding
        /// nothing comes back through the Empty factory with no text at all, while a clipboard
        /// holding an empty string comes back through Success with a length of zero. The design
        /// says as much: a zero return does not mean empty. Showing only the length would report
        /// both as the same thing, and the operator would have no way to tell which happened.
        /// </remarks>
        [Test]
        public void AnEmptyClipboardIsNotTheSameAsAnEmptyString()
        {
            WindowsClipboardTextResult nothing =
                WindowsClipboardTextResult.Empty(WindowsClipboardManager.OperationPastePlainText);
            WindowsClipboardTextResult emptyString =
                WindowsClipboardTextResult.Success(WindowsClipboardManager.OperationPastePlainText, string.Empty);

            Assert.IsTrue(nothing.IsSuccess, "an empty clipboard is a successful read");
            Assert.IsTrue(emptyString.IsSuccess);

            string describedNothing = WindowsClipboardSampleResult.DescribeText(nothing, 0UL);
            string describedEmptyString = WindowsClipboardSampleResult.DescribeText(emptyString, 0UL);

            StringAssert.Contains("empty=True", describedNothing);
            StringAssert.Contains("length=-1", describedNothing);

            StringAssert.Contains("empty=False", describedEmptyString);
            StringAssert.Contains("length=0", describedEmptyString);

            Assert.AreNotEqual(describedNothing, describedEmptyString);
        }

        // ── Lifecycle tracking ───────────────────────────────────────────────

        [Test]
        public void ASuccessfulInitializeMovesTheStateToRunning()
        {
            Assert.AreEqual(
                WindowsClipboardSampleState.Running,
                WindowsClipboardSampleResult.Advance(
                    WindowsClipboardSampleState.Unknown,
                    WindowsClipboardManager.OperationInitialize,
                    true, WindowsClipboardErrorCode.None));
        }

        [Test]
        public void ASuccessfulShutdownMovesTheStateToShutDown()
        {
            Assert.AreEqual(
                WindowsClipboardSampleState.ShutDown,
                WindowsClipboardSampleResult.Advance(
                    WindowsClipboardSampleState.Running,
                    WindowsClipboardManager.OperationShutdown,
                    true, WindowsClipboardErrorCode.None));
        }

        /// <remarks>
        /// The distinction the error code cannot carry. Draining still has a shutdown in progress;
        /// ShutdownFailed has given up and nothing short of a restart brings the Manager back.
        /// Both answer later operations with the same ShuttingDown code.
        /// </remarks>
        [Test]
        public void ATimedOutShutdownIsDistinguishedFromOneStillDraining()
        {
            Assert.AreEqual(
                WindowsClipboardSampleState.ShutdownFailed,
                WindowsClipboardSampleResult.Advance(
                    WindowsClipboardSampleState.Draining,
                    WindowsClipboardManager.OperationShutdown,
                    false, WindowsClipboardErrorCode.ShutdownTimeout));

            Assert.AreEqual(
                WindowsClipboardSampleState.Draining,
                WindowsClipboardSampleResult.Advance(
                    WindowsClipboardSampleState.Running,
                    WindowsClipboardManager.OperationShutdown,
                    false, WindowsClipboardErrorCode.Busy));
        }

        /// <remarks>
        /// A copy can fail for a dozen reasons that say nothing about the lifecycle. Letting those
        /// move the state would make the line drift away from the Manager it describes, and the
        /// operator would be reading a state that was never true.
        /// </remarks>
        [Test]
        public void AnUnrelatedFailureLeavesTheStateAlone()
        {
            Assert.AreEqual(
                WindowsClipboardSampleState.Running,
                WindowsClipboardSampleResult.Advance(
                    WindowsClipboardSampleState.Running,
                    WindowsClipboardManager.OperationCopyPlainText,
                    false, WindowsClipboardErrorCode.InvalidArgument));
        }

        [Test]
        public void AFailedInitializeDoesNotClaimTheManagerIsRunning()
        {
            Assert.AreEqual(
                WindowsClipboardSampleState.Unknown,
                WindowsClipboardSampleResult.Advance(
                    WindowsClipboardSampleState.Unknown,
                    WindowsClipboardManager.OperationInitialize,
                    false, WindowsClipboardErrorCode.PlatformUnavailable));
        }

        // ── Status line ──────────────────────────────────────────────────────

        /// <remarks>
        /// One external copy raises ClipboardChanged about three times. A prominent counter would
        /// make correct behaviour look broken on every check, so the sequence of the last one leads
        /// and the count sits behind it.
        /// </remarks>
        [Test]
        public void TheStatusLineLeadsWithTheLastChangeRatherThanTheCount()
        {
            string status = WindowsClipboardSampleResult.FormatStatus(2, 9, 41, 3, 1, 932);

            StringAssert.Contains("Pending: 2", status);
            StringAssert.Contains("Changed: #41 (x3)", status);
            StringAssert.Contains("Render: 1", status);
            StringAssert.Contains("ACP: 932", status);
        }

        [Test]
        public void TheStatusLineShowsNoChangeAsADashRatherThanAsZero()
        {
            StringAssert.Contains("Changed: -", WindowsClipboardSampleResult.FormatStatus(0, 0, 0, 0, 0, 65001));
        }
    }
}
#endif
