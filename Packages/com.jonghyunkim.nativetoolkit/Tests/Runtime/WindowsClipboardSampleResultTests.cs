#nullable enable

// Guarded to match the types under test (common.md: a test's compile guard follows its subject).
// The assembly is Editor-only, so this changes nothing about what runs; it keeps the pair readable
// as a pair.
#if UNITY_STANDALONE_WIN || UNITY_EDITOR
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

        /// <remarks>
        /// A line number and the call it belongs to are different things. Sharing one made a
        /// history read print 1, 2, 1: the event that landed between the accept and the done took
        /// its own number and the done went back to the caller's, so the log counted backwards.
        /// </remarks>
        [Test]
        public void ALineFromAnEarlierCallSaysWhichCall()
        {
            string line = WindowsClipboardSampleResult.FormatLine(
                9, WindowsClipboardSampleResult.KindDone, "getClipboardHistory", "OK code=None", call: 4);

            Assert.AreEqual("#9 [done] getClipboardHistory call=#4 OK code=None", line);
        }

        /// <remarks>
        /// The first line of a call is its own origin, so repeating the number would be noise.
        /// </remarks>
        [Test]
        public void ALineThatOpensItsOwnCallDoesNotRepeatTheNumber()
        {
            Assert.AreEqual(
                "#4 [call] copyPlainText OK code=None",
                WindowsClipboardSampleResult.FormatLine(
                    4, WindowsClipboardSampleResult.KindCall, "copyPlainText", "OK code=None", call: 4));
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
                WindowsClipboardSampleResult.FormatAccept(3, "getClipboardHistory", 42, call: 3));
        }

        /// <remarks>
        /// Zero is the one value that means no request exists. The outcome still arrives on the
        /// callback, so without saying so the line looks like an ordinary accept.
        /// </remarks>
        [Test]
        public void ARejectedRequestSaysSoRatherThanShowingZero()
        {
            string line = WindowsClipboardSampleResult.FormatAccept(3, "getClipboardHistory", 0, call: 3);

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
            byte[] data = System.Text.Encoding.UTF8.GetBytes("battery-staple-bytes");
            WindowsClipboardBytesResult result =
                WindowsClipboardBytesResult.Success(WindowsClipboardManager.OperationPasteImage, data);

            string described = WindowsClipboardSampleResult.DescribeBytes(
                result, WindowsClipboardSampleFixtures.HashOf(data));

            StringAssert.Contains($"size={data.Length}", described);
            StringAssert.Contains("match=match", described);
            Assert.IsFalse(
                described.Contains("battery-staple-bytes"),
                "the bytes themselves must not appear: " + described);
        }

        /// <remarks>
        /// A failed read handed nothing back, so nothing was compared. Running its absent content
        /// through the comparison printed "differ" beside the error code and read as a round trip
        /// that had lost something, which is the same wrong verdict the no-anchor case is written
        /// to avoid.
        /// </remarks>
        [Test]
        public void AFailedReadReportsNoComparisonRatherThanAMismatch()
        {
            WindowsClipboardTextResult text = WindowsClipboardTextResult.Failure(
                WindowsClipboardManager.OperationPastePlainText, WindowsClipboardErrorCode.FormatUnavailable);
            WindowsClipboardBytesResult bytes = WindowsClipboardBytesResult.Failure(
                WindowsClipboardManager.OperationPasteImage, WindowsClipboardErrorCode.FormatUnavailable);

            StringAssert.Contains(
                "match=" + WindowsClipboardSampleResult.NotApplicable,
                WindowsClipboardSampleResult.DescribeText(text, 12345UL));
            StringAssert.Contains(
                "match=" + WindowsClipboardSampleResult.NotApplicable,
                WindowsClipboardSampleResult.DescribeBytes(bytes, 12345UL));
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
        /// A shutdown reporting no error is not a finished one: ClassifyShutdown maps None with
        /// completed:false to NotYet and the Manager stays at Draining. Taking success to mean done
        /// put the state line one step ahead of the Manager for the rest of the session, and the
        /// next operation then came back ShuttingDown against a line reading ShutDown. The Editor
        /// pins completed to true, so this only ever diverged on a device.
        /// </remarks>
        [Test]
        public void AShutdownThatReportedNoErrorButDidNotFinishIsStillDraining()
        {
            Assert.AreEqual(
                WindowsClipboardSampleState.Draining,
                WindowsClipboardSampleResult.Advance(
                    WindowsClipboardSampleState.Running,
                    WindowsClipboardManager.OperationShutdown,
                    true, WindowsClipboardErrorCode.None, completed: false));

            Assert.AreEqual(
                WindowsClipboardSampleState.ShutDown,
                WindowsClipboardSampleResult.Advance(
                    WindowsClipboardSampleState.Running,
                    WindowsClipboardManager.OperationShutdown,
                    true, WindowsClipboardErrorCode.None, completed: true));
        }

        /// <remarks>
        /// ClassifyShutdown keeps a drain alive for exactly five codes and treats everything else
        /// as terminal, latching ShutdownFailed. Naming ShutdownTimeout alone left every other
        /// terminal failure - a bridge that could not be reached, an unknown native fault - shown
        /// as "still making progress", which inverts the distinction this state exists to draw.
        /// </remarks>
        [Test]
        public void EveryTerminalShutdownFailureIsShownAsTerminal()
        {
            foreach (WindowsClipboardErrorCode code in new[]
                     {
                         WindowsClipboardErrorCode.None,
                         WindowsClipboardErrorCode.Busy,
                         WindowsClipboardErrorCode.MonitorRegisterFailed,
                         WindowsClipboardErrorCode.Canceled,
                         WindowsClipboardErrorCode.PartialState,
                     })
            {
                Assert.IsTrue(WindowsClipboardSampleResult.KeepsDraining(code), code.ToString());
                Assert.AreEqual(
                    WindowsClipboardSampleState.Draining,
                    WindowsClipboardSampleResult.Advance(
                        WindowsClipboardSampleState.Running,
                        WindowsClipboardManager.OperationShutdown, false, code),
                    code.ToString());
            }

            foreach (WindowsClipboardErrorCode code in new[]
                     {
                         WindowsClipboardErrorCode.ShutdownTimeout,
                         WindowsClipboardErrorCode.BridgeUnavailable,
                         WindowsClipboardErrorCode.Unknown,
                         WindowsClipboardErrorCode.WrongThread,
                         WindowsClipboardErrorCode.AccessDenied,
                     })
            {
                Assert.IsFalse(WindowsClipboardSampleResult.KeepsDraining(code), code.ToString());
                Assert.AreEqual(
                    WindowsClipboardSampleState.ShutdownFailed,
                    WindowsClipboardSampleResult.Advance(
                        WindowsClipboardSampleState.Running,
                        WindowsClipboardManager.OperationShutdown, false, code),
                    code.ToString());
            }
        }

        /// <remarks>
        /// A shutdown refused before any native attempt leaves the Manager exactly where it was.
        /// Recording a transition would describe a shutdown that never happened.
        /// </remarks>
        [Test]
        public void AShutdownRefusedBeforeItWasTriedMovesNothing()
        {
            Assert.AreEqual(
                WindowsClipboardSampleState.Running,
                WindowsClipboardSampleResult.Advance(
                    WindowsClipboardSampleState.Running,
                    WindowsClipboardManager.OperationShutdown,
                    false, WindowsClipboardErrorCode.MainThreadRequired));
        }

        /// <remarks>
        /// Pinned from a state other than the one the neighbouring tests use, so that replacing the
        /// body with a fixed return value cannot pass.
        /// </remarks>
        [Test]
        public void AFailedInitializeLeavesWhateverStateWasAlreadyObserved()
        {
            Assert.AreEqual(
                WindowsClipboardSampleState.Running,
                WindowsClipboardSampleResult.Advance(
                    WindowsClipboardSampleState.Running,
                    WindowsClipboardManager.OperationInitialize,
                    false, WindowsClipboardErrorCode.ShuttingDown));

            Assert.AreEqual(
                WindowsClipboardSampleState.ShutdownFailed,
                WindowsClipboardSampleResult.Advance(
                    WindowsClipboardSampleState.ShutdownFailed,
                    WindowsClipboardManager.OperationCopyPlainText,
                    false, WindowsClipboardErrorCode.ShuttingDown));
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
            string status = WindowsClipboardSampleResult.FormatStatus(2, 9, 41, 3, 1, 0, 932, true);

            StringAssert.Contains("Pending: 2", status);
            StringAssert.Contains("Events: 9", status);
            StringAssert.Contains("Changed: #41 (x3)", status);
            StringAssert.Contains("ACP: 932", status);
            StringAssert.Contains("HistoryId: held", status);
        }

        /// <remarks>
        /// The id itself is an opaque handle the history service owns and tells a reader nothing,
        /// but whether one is held is the precondition for Restore and Delete. Without it on
        /// screen, those buttons look ready when they are not.
        /// </remarks>
        [Test]
        public void TheStatusLineSaysWhetherAHistoryIdIsHeld()
        {
            StringAssert.Contains(
                "HistoryId: none",
                WindowsClipboardSampleResult.FormatStatus(0, 0, 0, 0, 0, 0, 932, false));
        }

        /// <remarks>
        /// Per format, not a total. Each provider is asked once, so the number to watch is that
        /// neither goes above one; a sum reads as two as soon as the receiving application wants
        /// both formats, and correct behaviour then looks like a duplicate call.
        /// </remarks>
        [Test]
        public void TheStatusLineSeparatesTheTwoDeferredProviders()
        {
            string status = WindowsClipboardSampleResult.FormatStatus(0, 0, 0, 0, 1, 0, 932, false);

            StringAssert.Contains("Render: text=1 image=0", status);
            Assert.IsFalse(status.Contains("Render: 1 "), "a single total cannot answer the check");
        }

        /// <summary>
        /// A failed read reports no shape at all, not a shape of zero.
        /// </summary>
        /// <remarks>
        /// A device run produced "empty=False count=0" beside an error, which reads as a
        /// successful read of nothing. Neither half was an observation: the library returns a
        /// default result on failure, so False and 0 are what the struct starts as. The
        /// comparison already said n/a in that case; the rest now says it too.
        /// </remarks>
        [Test]
        public void AFailedReadReportsNoShapeRatherThanAShapeOfZero()
        {
            WindowsClipboardTextResult text = WindowsClipboardTextResult.Failure(
                WindowsClipboardManager.OperationPastePlainText,
                WindowsClipboardErrorCode.NotInitialized);
            WindowsClipboardStringListResult files = WindowsClipboardStringListResult.Failure(
                WindowsClipboardManager.OperationPasteFiles,
                WindowsClipboardErrorCode.NotInitialized);
            WindowsClipboardHistoryResult history = WindowsClipboardHistoryResult.Failure(
                WindowsClipboardManager.OperationGetHistory,
                WindowsClipboardErrorCode.NotInitialized);

            string describedText = WindowsClipboardSampleResult.DescribeText(text, 0UL);
            string describedFiles = WindowsClipboardSampleResult.DescribeStringList(files);
            string describedHistory = WindowsClipboardSampleResult.DescribeHistory(history, 0L, 0UL);

            foreach (string described in new[] { describedText, describedFiles, describedHistory })
            {
                StringAssert.Contains("empty=n/a", described);
                Assert.IsFalse(described.Contains("empty=False"),
                    $"a read that did not happen is not a read of something: {described}");
                Assert.IsFalse(described.Contains("=0"),
                    $"zero is a measurement, and none was taken: {described}");
            }
        }

        [Test]
        public void TheStatusLineShowsNoChangeAsADashRatherThanAsZero()
        {
            StringAssert.Contains(
                "Changed: -", WindowsClipboardSampleResult.FormatStatus(0, 0, 0, 0, 0, 0, 65001, false));
        }
    }
}
#endif
