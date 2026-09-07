#nullable enable

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using JonghyunKim.NativeToolkit.Runtime.Clipboard;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// EditMode tests for the parts of the Windows clipboard manager that hold no native state:
    /// the delivery order, the shutdown classification, and the lifecycle state machine.
    /// <para>
    /// No test creates a manager instance. Awake touches the dispatcher and the native boundary,
    /// which belongs to PlayMode; everything verified here is reachable through internal statics.
    /// </para>
    /// </summary>
    public sealed class WindowsClipboardManagerDispatchTests
    {
        private const string Op = "copyPlainText";

        [TearDown]
        public void TearDown() => WindowsClipboardManager.ResetForTests();

        // ── InvokeInOrder ────────────────────────────────────────────────────────

        [Test]
        public void InvokeInOrder_CallsTheCommonEventBeforeThePerCallCallback()
        {
            var order = new List<string>();

            WindowsClipboardManager.InvokeInOrder(
                WindowsClipboardResult.Success(Op),
                _ => order.Add("common"),
                _ => order.Add("perCall"));

            Assert.AreEqual(new[] { "common", "perCall" }, order.ToArray());
        }

        [Test]
        public void InvokeInOrder_DeliversTheSameResultToBoth()
        {
            WindowsClipboardResult common = default;
            WindowsClipboardResult perCall = default;
            WindowsClipboardResult expected =
                WindowsClipboardResult.Failure(Op, WindowsClipboardErrorCode.Busy);

            WindowsClipboardManager.InvokeInOrder(expected, r => common = r, r => perCall = r);

            Assert.AreEqual(expected.ErrorCode, common.ErrorCode);
            Assert.AreEqual(expected.ErrorCode, perCall.ErrorCode);
        }

        [Test]
        public void InvokeInOrder_AThrowingCommonSubscriberDoesNotStopThePerCallCallback()
        {
            bool perCallRan = false;

            WindowsClipboardManager.InvokeInOrder<WindowsClipboardResult>(
                WindowsClipboardResult.Success(Op),
                _ => throw new InvalidOperationException("subscriber"),
                _ => perCallRan = true);

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("common event subscriber threw"));
            Assert.IsTrue(perCallRan, "one bad subscriber must not swallow the other delivery");
        }

        [Test]
        public void InvokeInOrder_AThrowingPerCallCallbackIsContained()
        {
            WindowsClipboardManager.InvokeInOrder<WindowsClipboardResult>(
                WindowsClipboardResult.Success(Op),
                null,
                _ => throw new InvalidOperationException("callback"));

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("per-call callback threw"));
        }

        [Test]
        public void InvokeInOrder_BothNullIsHarmless()
        {
            Assert.DoesNotThrow(() => WindowsClipboardManager.InvokeInOrder(
                WindowsClipboardResult.Success(Op), null, null));
        }

        // ── Shutdown classification ──────────────────────────────────────────────

        [Test]
        public void ClassifyShutdown_CompletedWins()
        {
            Assert.AreEqual(WindowsClipboardShutdownProgress.Completed,
                WindowsClipboardManager.ClassifyShutdown(true, WindowsClipboardErrorCode.None));
        }

        [TestCase(WindowsClipboardErrorCode.None)]
        [TestCase(WindowsClipboardErrorCode.Busy)]
        [TestCase(WindowsClipboardErrorCode.MonitorRegisterFailed)]
        [TestCase(WindowsClipboardErrorCode.Canceled)]
        [TestCase(WindowsClipboardErrorCode.PartialState)]
        public void ClassifyShutdown_TheseCodesMeanNotYet(WindowsClipboardErrorCode code)
        {
            // The native manager reports why it is still busy through the same out parameter it
            // uses for failures, so treating any non-zero code as failure would abandon a shutdown
            // that only needed another pumped frame.
            Assert.AreEqual(WindowsClipboardShutdownProgress.NotYet,
                WindowsClipboardManager.ClassifyShutdown(false, code));
        }

        [Test]
        public void ClassifyShutdown_WrongThreadIsTerminal()
        {
            // Retrying from the wrong thread can never complete, so the drain must stop instead of
            // burning its whole budget.
            Assert.AreEqual(WindowsClipboardShutdownProgress.Terminal,
                WindowsClipboardManager.ClassifyShutdown(false, WindowsClipboardErrorCode.WrongThread));
        }

        [TestCase(WindowsClipboardErrorCode.InvalidParameter)]
        [TestCase(WindowsClipboardErrorCode.Unknown)]
        [TestCase(WindowsClipboardErrorCode.BridgeUnavailable)]
        [TestCase(WindowsClipboardErrorCode.PlatformUnavailable)]
        public void ClassifyShutdown_OtherCodesAreTerminal(WindowsClipboardErrorCode code)
        {
            Assert.AreEqual(WindowsClipboardShutdownProgress.Terminal,
                WindowsClipboardManager.ClassifyShutdown(false, code));
        }

        // ── Read classification (design 7.5) ─────────────────────────────────────

        [TestCase(WindowsClipboardErrorCode.Empty)]
        [TestCase(WindowsClipboardErrorCode.FormatUnavailable)]
        public void ClassifyFirstRead_TheseCodesMeanAnEmptyClipboard(WindowsClipboardErrorCode code)
        {
            Assert.AreEqual(WindowsClipboardReadDecision.EmptySuccess,
                WindowsClipboardManager.ClassifyFirstRead(code, 0, isByteApi: false));
            Assert.AreEqual(WindowsClipboardReadDecision.EmptySuccess,
                WindowsClipboardManager.ClassifyFirstRead(code, 12, isByteApi: true));
        }

        [Test]
        public void ClassifyFirstRead_NoneWithASizeNeedsABuffer()
        {
            Assert.AreEqual(WindowsClipboardReadDecision.NeedsBuffer,
                WindowsClipboardManager.ClassifyFirstRead(WindowsClipboardErrorCode.None, 5, false));
        }

        [Test]
        public void ClassifyFirstRead_NoneWithoutASizeIsEmpty()
        {
            Assert.AreEqual(WindowsClipboardReadDecision.EmptySuccess,
                WindowsClipboardManager.ClassifyFirstRead(WindowsClipboardErrorCode.None, 0, false));
        }

        [Test]
        public void ClassifyFirstRead_BufferTooSmallWithASizeNeedsABuffer()
        {
            // This is the normal path: the sizing call always reports the size this way.
            Assert.AreEqual(WindowsClipboardReadDecision.NeedsBuffer,
                WindowsClipboardManager.ClassifyFirstRead(WindowsClipboardErrorCode.BufferTooSmall, 21, false));
        }

        [Test]
        public void ClassifyFirstRead_ZeroBytesIsAnEmptySuccessOnlyForTheByteApis()
        {
            // The byte APIs report a zero-length payload as size 0 plus BufferTooSmall. A string
            // API always needs room for the terminator, so the same pair means something is wrong.
            Assert.AreEqual(WindowsClipboardReadDecision.EmptySuccess,
                WindowsClipboardManager.ClassifyFirstRead(WindowsClipboardErrorCode.BufferTooSmall, 0, isByteApi: true));
            Assert.AreEqual(WindowsClipboardReadDecision.Failure,
                WindowsClipboardManager.ClassifyFirstRead(WindowsClipboardErrorCode.BufferTooSmall, 0, isByteApi: false));
        }

        [TestCase(WindowsClipboardErrorCode.NotInitialized)]
        [TestCase(WindowsClipboardErrorCode.Busy)]
        [TestCase(WindowsClipboardErrorCode.InvalidData)]
        [TestCase(WindowsClipboardErrorCode.OutOfMemory)]
        [TestCase(WindowsClipboardErrorCode.WrongThread)]
        public void ClassifyFirstRead_AFailureWithZeroSizeIsNeverNormalizedToEmpty(
            WindowsClipboardErrorCode code)
        {
            // The native read APIs return 0 for a lease failure too, so trusting the size instead
            // of the code would turn a real failure into "the clipboard is empty".
            Assert.AreEqual(WindowsClipboardReadDecision.Failure,
                WindowsClipboardManager.ClassifyFirstRead(code, 0, isByteApi: false));
            Assert.AreEqual(WindowsClipboardReadDecision.Failure,
                WindowsClipboardManager.ClassifyFirstRead(code, 0, isByteApi: true));
        }

        [Test]
        public void ClassifySecondRead_NoneMeansTheBufferMayBeRead()
        {
            Assert.AreEqual(WindowsClipboardSecondReadDecision.Read,
                WindowsClipboardManager.ClassifySecondRead(WindowsClipboardErrorCode.None));
        }

        [TestCase(WindowsClipboardErrorCode.Empty)]
        [TestCase(WindowsClipboardErrorCode.FormatUnavailable)]
        public void ClassifySecondRead_TheClipboardChangingBetweenCallsIsAnEmptySuccess(
            WindowsClipboardErrorCode code)
        {
            Assert.AreEqual(WindowsClipboardSecondReadDecision.EmptySuccess,
                WindowsClipboardManager.ClassifySecondRead(code));
        }

        [Test]
        public void ClassifySecondRead_BufferTooSmallMeansTheContentGrew()
        {
            Assert.AreEqual(WindowsClipboardSecondReadDecision.Retry,
                WindowsClipboardManager.ClassifySecondRead(WindowsClipboardErrorCode.BufferTooSmall));
        }

        [TestCase(WindowsClipboardErrorCode.Busy)]
        [TestCase(WindowsClipboardErrorCode.InvalidData)]
        [TestCase(WindowsClipboardErrorCode.Unknown)]
        public void ClassifySecondRead_OtherCodesFailBeforeTheBufferIsTouched(
            WindowsClipboardErrorCode code)
        {
            Assert.AreEqual(WindowsClipboardSecondReadDecision.Failure,
                WindowsClipboardManager.ClassifySecondRead(code));
        }

        // ── Deferred rendering (design 2.8 and 7.7) ──────────────────────────────

        private static Dictionary<string, Func<byte[]>> Provider(string format, byte[] payload) =>
            new() { [format] = () => payload };

        [Test]
        public void Reservation_OnSuccess_ReplacesTheLiveProviders()
        {
            WindowsClipboardManager.SetRenderProvidersForTests(Provider("OLD", new byte[] { 1 }));

            WindowsClipboardManager.ApplyReservationOutcomeForTests(
                WindowsClipboardErrorCode.None, Provider("NEW", new byte[] { 2 }));

            CollectionAssert.AreEquivalent(
                new[] { "NEW" }, WindowsClipboardManager.RenderProviderNamesForTests);
        }

        [TestCase(WindowsClipboardErrorCode.Busy)]
        [TestCase(WindowsClipboardErrorCode.InvalidParameter)]
        [TestCase(WindowsClipboardErrorCode.Unknown)]
        [TestCase(WindowsClipboardErrorCode.OutOfMemory)]
        public void Reservation_OnFailure_KeepsThePreviousProvidersUntouched(
            WindowsClipboardErrorCode code)
        {
            // The native side may still hold the previous renderers on these paths, and Unknown
            // covers two failure sites that cannot be told apart, so the old generation has to
            // survive or a reserved format would be dropped without a word.
            WindowsClipboardManager.SetRenderProvidersForTests(Provider("OLD", new byte[] { 1 }));

            WindowsClipboardManager.ApplyReservationOutcomeForTests(
                code, Provider("NEW", new byte[] { 2 }));

            CollectionAssert.AreEquivalent(
                new[] { "OLD" }, WindowsClipboardManager.RenderProviderNamesForTests);
        }

        [Test]
        public void Reservation_OnPartialState_KeepsBothGenerations()
        {
            // The native side kept the new table here, but the formats it no longer names may still
            // be asked for.
            WindowsClipboardManager.SetRenderProvidersForTests(Provider("OLD", new byte[] { 1 }));

            WindowsClipboardManager.ApplyReservationOutcomeForTests(
                WindowsClipboardErrorCode.PartialState, Provider("NEW", new byte[] { 2 }));

            CollectionAssert.AreEquivalent(
                new[] { "OLD", "NEW" }, WindowsClipboardManager.RenderProviderNamesForTests);
        }

        [Test]
        public void Render_FirstPhase_ReportsTheSizeAndAsksToBeCalledAgain()
        {
            WindowsClipboardManager.SetRenderProvidersForTests(Provider("F", new byte[] { 1, 2, 3 }));

            uint code = WindowsClipboardManager.RenderForTests("F", IntPtr.Zero, 0, out uint required);

            Assert.AreEqual((uint)WindowsClipboardErrorCode.BufferTooSmall, code);
            Assert.AreEqual(3u, required);
        }

        [Test]
        public void Render_SecondPhase_WritesTheBytesAndRepeatsTheSameSize()
        {
            // The native layer discards a format whose second answer differs from the first, so the
            // payload is produced once and reused rather than regenerated.
            WindowsClipboardManager.SetRenderProvidersForTests(Provider("F", new byte[] { 7, 8 }));
            WindowsClipboardManager.RenderForTests("F", IntPtr.Zero, 0, out uint first);

            IntPtr buffer = Marshal.AllocHGlobal((int)first);
            try
            {
                uint code = WindowsClipboardManager.RenderForTests("F", buffer, first, out uint second);

                Assert.AreEqual((uint)WindowsClipboardErrorCode.None, code);
                Assert.AreEqual(first, second, "the two phases must agree on the size");
                var written = new byte[second];
                Marshal.Copy(buffer, written, 0, (int)second);
                Assert.AreEqual(new byte[] { 7, 8 }, written);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        [Test]
        public void Render_SecondPhase_UsesTheCachedBytesEvenWhenTheProviderWouldChange()
        {
            int calls = 0;
            var providers = new Dictionary<string, Func<byte[]>>
            {
                ["F"] = () => { calls++; return calls == 1 ? new byte[] { 1, 2, 3 } : new byte[] { 9 }; }
            };
            WindowsClipboardManager.SetRenderProvidersForTests(providers);

            WindowsClipboardManager.RenderForTests("F", IntPtr.Zero, 0, out uint first);
            IntPtr buffer = Marshal.AllocHGlobal((int)first);
            try
            {
                WindowsClipboardManager.RenderForTests("F", buffer, first, out uint second);

                Assert.AreEqual(1, calls, "the provider runs once per render, not once per phase");
                Assert.AreEqual(first, second);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        [Test]
        public void Render_AZeroLengthPayloadIsReportedRatherThanPlaced()
        {
            WindowsClipboardManager.SetRenderProvidersForTests(Provider("F", new byte[0]));

            uint code = WindowsClipboardManager.RenderForTests("F", IntPtr.Zero, 0, out uint required);

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("produced no bytes"));
            Assert.AreEqual((uint)WindowsClipboardErrorCode.InvalidData, code);
            Assert.AreEqual(0u, required);
        }

        [Test]
        public void Render_AnUnknownFormatFails()
        {
            WindowsClipboardManager.SetRenderProvidersForTests(new Dictionary<string, Func<byte[]>>());

            uint code = WindowsClipboardManager.RenderForTests("MISSING", IntPtr.Zero, 0, out uint required);

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("no provider for MISSING"));
            Assert.AreEqual((uint)WindowsClipboardErrorCode.InvalidParameter, code);
            Assert.AreEqual(0u, required);
        }

        [Test]
        public void Render_SecondPhaseWithoutACachedPayloadFailsInsteadOfRegenerating()
        {
            WindowsClipboardManager.SetRenderProvidersForTests(Provider("F", new byte[] { 1 }));

            IntPtr buffer = Marshal.AllocHGlobal(1);
            try
            {
                uint code = WindowsClipboardManager.RenderForTests("F", buffer, 1, out uint required);

                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("no cached payload"));
                Assert.AreEqual((uint)WindowsClipboardErrorCode.Unknown, code);
                Assert.AreEqual(0u, required);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        [Test]
        public void Render_ATooSmallBufferReportsTheRealSize()
        {
            WindowsClipboardManager.SetRenderProvidersForTests(Provider("F", new byte[] { 1, 2, 3, 4 }));
            WindowsClipboardManager.RenderForTests("F", IntPtr.Zero, 0, out uint first);

            IntPtr buffer = Marshal.AllocHGlobal(1);
            try
            {
                uint code = WindowsClipboardManager.RenderForTests("F", buffer, 1, out uint required);

                Assert.AreEqual((uint)WindowsClipboardErrorCode.BufferTooSmall, code);
                Assert.AreEqual(first, required, "the size reported must stay the one promised");
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        [Test]
        public void Render_AThrowingProviderIsContainedAndReportsNoSize()
        {
            var providers = new Dictionary<string, Func<byte[]>>
            {
                ["F"] = () => throw new InvalidOperationException("boom")
            };
            WindowsClipboardManager.SetRenderProvidersForTests(providers);

            uint code = WindowsClipboardManager.RenderForTests("F", IntPtr.Zero, 0, out uint required);

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("InvalidOperationException"));
            Assert.AreEqual((uint)WindowsClipboardErrorCode.Unknown, code);
            Assert.AreEqual(0u, required, "an exception must not leave a stale size behind");
        }


        // ── The two-call read protocol (design 7.5) ──────────────────────────────
        // Driven through a stand-in for the native side. Behind the compile guard none of this
        // could be reached, and the editor's answer came from the guard rather than the protocol.

        private static Func<IntPtr, uint, (uint, WindowsClipboardErrorCode)> TextSource(string value)
        {
            uint needed = (uint)value.Length + 1;
            return (buffer, bufferSize) =>
            {
                if (buffer == IntPtr.Zero) return (needed, WindowsClipboardErrorCode.None);
                if (bufferSize < needed) return (needed, WindowsClipboardErrorCode.BufferTooSmall);
                for (int i = 0; i < value.Length; i++)
                {
                    Marshal.WriteInt16(buffer, i * 2, (short)value[i]);
                }
                Marshal.WriteInt16(buffer, value.Length * 2, 0);
                return (needed, WindowsClipboardErrorCode.None);
            };
        }

        [Test]
        public void Read_ReturnsTheTextTheSecondCallWrote()
        {
            var outcome = WindowsClipboardManager.ReadRawForTests(
                isByteApi: false, TextSource("hello"));

            Assert.IsTrue(outcome.isSuccess);
            Assert.IsFalse(outcome.isEmpty);
            Assert.AreEqual("hello", outcome.text);
            Assert.AreEqual(2, outcome.calls, "one sizing call and one fill call");
        }

        [Test]
        public void Read_AnEmptyStringIsAValueRatherThanAnEmptyClipboard()
        {
            // The native side reports a size of one for an empty string, not zero. Something was
            // copied and can be pasted back, which is not the same as nothing being there.
            var outcome = WindowsClipboardManager.ReadRawForTests(
                isByteApi: false, TextSource(string.Empty));

            Assert.IsTrue(outcome.isSuccess);
            Assert.IsFalse(outcome.isEmpty, "the clipboard holds an empty string, not nothing");
            Assert.AreEqual(string.Empty, outcome.text);
        }

        [Test]
        public void Read_WhenTheContentGrowsBetweenTheCalls_SizesItAgain()
        {
            int call = 0;
            var growing = TextSource("larger");
            var outcome = WindowsClipboardManager.ReadRawForTests(
                isByteApi: false,
                (buffer, bufferSize) =>
                {
                    call++;
                    // The second call finds the content no longer fits what the first promised.
                    if (call == 2) return (0u, WindowsClipboardErrorCode.BufferTooSmall);
                    return growing(buffer, bufferSize);
                });

            Assert.IsTrue(outcome.isSuccess);
            Assert.AreEqual("larger", outcome.text);
            Assert.AreEqual(4, outcome.calls, "sizing and filling, twice");
        }

        [Test]
        public void Read_ThatKeepsResizingGivesUpRatherThanLoopingForever()
        {
            var outcome = WindowsClipboardManager.ReadRawForTests(
                isByteApi: false,
                (buffer, bufferSize) => buffer == IntPtr.Zero
                    ? (4u, WindowsClipboardErrorCode.None)
                    : (0u, WindowsClipboardErrorCode.BufferTooSmall));

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("kept resizing"));
            Assert.IsFalse(outcome.isSuccess);
            Assert.AreEqual(WindowsClipboardErrorCode.BufferTooSmall, outcome.code);
            Assert.AreEqual(6, outcome.calls, "three attempts of two calls each, then it stops");
        }

        [Test]
        public void Read_AFailureOnTheSecondCallIsReportedWithoutTouchingTheBuffer()
        {
            var outcome = WindowsClipboardManager.ReadRawForTests(
                isByteApi: true,
                (buffer, bufferSize) => buffer == IntPtr.Zero
                    ? (8u, WindowsClipboardErrorCode.None)
                    // Nothing was written, so reading the buffer would hand back uninitialized memory.
                    : (0u, WindowsClipboardErrorCode.Busy));

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("read failed"));
            Assert.IsFalse(outcome.isSuccess);
            Assert.AreEqual(WindowsClipboardErrorCode.Busy, outcome.code);
            Assert.IsNull(outcome.data);
        }

        [Test]
        public void Read_AFailureOnTheFirstCallNeverAllocatesAtAll()
        {
            var outcome = WindowsClipboardManager.ReadRawForTests(
                isByteApi: true,
                (buffer, bufferSize) => (0u, WindowsClipboardErrorCode.NotInitialized));

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("sizing failed"));
            Assert.IsFalse(outcome.isSuccess);
            Assert.IsFalse(outcome.isEmpty, "a rejected read is not an empty clipboard");
            Assert.AreEqual(WindowsClipboardErrorCode.NotInitialized, outcome.code);
            Assert.AreEqual(1, outcome.calls);
        }

        [Test]
        public void Read_ByteApi_ReturnsExactlyWhatWasWritten()
        {
            byte[] payload = { 9, 8, 7 };
            var outcome = WindowsClipboardManager.ReadRawForTests(
                isByteApi: true,
                (buffer, bufferSize) =>
                {
                    if (buffer == IntPtr.Zero) return ((uint)payload.Length, WindowsClipboardErrorCode.None);
                    Marshal.Copy(payload, 0, buffer, payload.Length);
                    return ((uint)payload.Length, WindowsClipboardErrorCode.None);
                });

            Assert.IsTrue(outcome.isSuccess);
            Assert.AreEqual(payload, outcome.data);
        }


        [Test]
        public void ReadText_AnEmptyPasteIsAnEmptyStringAndNotAnEmptyClipboard()
        {
            // Something was copied and can be pasted back. Reporting it as empty would fold that
            // together with "there is no text at all", and the caller cannot tell them apart again.
            WindowsClipboardManager.PlatformAvailableForTests = true;
            WindowsClipboardManager.SetStateForTests(WindowsClipboardManagerState.Running);

            WindowsClipboardTextResult result = WindowsClipboardManager.ReadTextForTests(
                WindowsClipboardManager.OperationPastePlainText, TextSource(string.Empty));

            Assert.IsTrue(result.IsSuccess);
            Assert.IsFalse(result.IsEmpty);
            Assert.AreEqual(string.Empty, result.Text);
        }

        [Test]
        public void ReadText_GetPreferredFormat_TreatsAnEmptyStringAsNothingMatched()
        {
            // The odd one out, and the only reason the normalization exists: this call answers with
            // an empty string when none of the candidates were present, and never reports the
            // native Empty code.
            WindowsClipboardManager.PlatformAvailableForTests = true;
            WindowsClipboardManager.SetStateForTests(WindowsClipboardManagerState.Running);

            WindowsClipboardTextResult result = WindowsClipboardManager.ReadTextForTests(
                WindowsClipboardManager.OperationGetPreferredFormat, TextSource(string.Empty));

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.IsEmpty);
        }

        [Test]
        public void ReadText_APasteWithContentIsUnaffected()
        {
            WindowsClipboardManager.PlatformAvailableForTests = true;
            WindowsClipboardManager.SetStateForTests(WindowsClipboardManagerState.Running);

            WindowsClipboardTextResult result = WindowsClipboardManager.ReadTextForTests(
                WindowsClipboardManager.OperationPastePlainText, TextSource("kept"));

            Assert.IsTrue(result.IsSuccess);
            Assert.IsFalse(result.IsEmpty);
            Assert.AreEqual("kept", result.Text);
        }

        [Test]
        public void Render_AProviderGenerationChangeTakesTheCacheWithIt()
        {
            // Every path that installs a generation for real clears the cache too. If the seam did
            // not, a test could ask the second phase to answer from the previous generation's
            // bytes - a state the manager itself cannot reach.
            WindowsClipboardManager.SetRenderProvidersForTests(Provider("F", new byte[] { 1, 2, 3 }));
            WindowsClipboardManager.RenderForTests("F", IntPtr.Zero, 0, out uint first);
            CollectionAssert.Contains(WindowsClipboardManager.RenderCacheNamesForTests, "F");

            WindowsClipboardManager.SetRenderProvidersForTests(Provider("F", new byte[] { 9 }));

            CollectionAssert.IsEmpty(WindowsClipboardManager.RenderCacheNamesForTests);

            IntPtr buffer = Marshal.AllocHGlobal((int)first);
            try
            {
                uint code = WindowsClipboardManager.RenderForTests("F", buffer, first, out uint required);

                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("no cached payload"));
                Assert.AreEqual((uint)WindowsClipboardErrorCode.Unknown, code);
                Assert.AreEqual(0u, required);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        [Test]
        public void Read_ASizeTooLargeToAllocateIsRefusedRatherThanTruncated()
        {
            // An unchecked cast turns this into a negative byte count, and the allocation that
            // follows is smaller than what the second call is told it may write into.
            var outcome = WindowsClipboardManager.ReadRawForTests(
                isByteApi: false,
                (buffer, bufferSize) => (0x7FFFFFFFu, WindowsClipboardErrorCode.None));

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("too large to allocate"));
            Assert.IsFalse(outcome.isSuccess);
            Assert.AreEqual(WindowsClipboardErrorCode.OutOfMemory, outcome.code);
            Assert.AreEqual(1, outcome.calls, "nothing was allocated, so nothing was filled");
        }

        [Test]
        public void Reservation_OnFailure_KeepsTheCachedBytesAsWellAsTheProviders()
        {
            // The two go together. Dropping the cache while keeping the providers leaves a render
            // that already promised a size unable to answer with it, and the native side discards
            // the format for disagreeing with itself.
            WindowsClipboardManager.SetRenderProvidersForTests(Provider("OLD", new byte[] { 1, 2 }));
            WindowsClipboardManager.RenderForTests("OLD", IntPtr.Zero, 0, out uint first);
            CollectionAssert.Contains(WindowsClipboardManager.RenderCacheNamesForTests, "OLD");

            WindowsClipboardManager.ApplyReservationOutcomeForTests(
                WindowsClipboardErrorCode.Busy, Provider("NEW", new byte[] { 3 }));

            CollectionAssert.AreEquivalent(
                new[] { "OLD" }, WindowsClipboardManager.RenderProviderNamesForTests);
            CollectionAssert.Contains(WindowsClipboardManager.RenderCacheNamesForTests, "OLD");

            IntPtr buffer = Marshal.AllocHGlobal((int)first);
            try
            {
                uint code = WindowsClipboardManager.RenderForTests("OLD", buffer, first, out uint second);

                Assert.AreEqual((uint)WindowsClipboardErrorCode.None, code);
                Assert.AreEqual(first, second, "the size promised before the failed reservation still holds");
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }


        // ── Operation guard (design 7.5) ─────────────────────────────────────────
        // The public operations are instance methods, and creating the Manager needs a player
        // loop, so EditMode covers the guard through its seam. The API-level rejections live in
        // the PlayMode integration tests.

        // The order is the contract, so it is checked on the classifier directly rather than
        // through the manager: the editor is never a Windows player, so every state-dependent
        // answer would otherwise be hidden behind PlatformUnavailable.
        // Separate methods rather than TestCases: the state enum is internal, and a public test
        // method cannot take a parameter of a less accessible type.

        [Test]
        public void Guard_OnAWorkerThread_ReportsMainThreadRequiredBeforeAnythingElse()
        {
            // Every other condition is also wrong here; the thread still wins.
            Assert.AreEqual(WindowsClipboardErrorCode.MainThreadRequired,
                WindowsClipboardManager.ClassifyOperationGuard(
                    isMainThread: false,
                    terminated: true,
                    platformAvailable: false,
                    state: WindowsClipboardManagerState.Draining));
        }

        [Test]
        public void Guard_AfterDestruction_ReportsManagerDestroyedEvenOnAnUnsupportedPlatform()
        {
            // The tombstone outranks both the platform and the state: a recreated manager must
            // stay inert, and saying why it is inert matters more than saying where it runs.
            Assert.AreEqual(WindowsClipboardErrorCode.ManagerDestroyed,
                WindowsClipboardManager.ClassifyOperationGuard(
                    isMainThread: true,
                    terminated: true,
                    platformAvailable: false,
                    state: WindowsClipboardManagerState.Running));
        }

        [Test]
        public void Guard_OutsideAWindowsPlayer_ReportsPlatformUnavailableRatherThanNotInitialized()
        {
            // Initialize cannot succeed off a Windows player, so NotInitializedByHost would send
            // the caller after an Initialize that could never have worked.
            Assert.AreEqual(WindowsClipboardErrorCode.PlatformUnavailable,
                WindowsClipboardManager.ClassifyOperationGuard(
                    isMainThread: true,
                    terminated: false,
                    platformAvailable: false,
                    state: WindowsClipboardManagerState.Uninitialized));
        }

        [Test]
        public void Guard_WhileDraining_ReportsShuttingDown()
        {
            Assert.AreEqual(WindowsClipboardErrorCode.ShuttingDown,
                WindowsClipboardManager.ClassifyOperationGuard(
                    isMainThread: true,
                    terminated: false,
                    platformAvailable: true,
                    state: WindowsClipboardManagerState.Draining));
        }

        [Test]
        public void Guard_AfterAFailedShutdown_ReportsShuttingDown()
        {
            Assert.AreEqual(WindowsClipboardErrorCode.ShuttingDown,
                WindowsClipboardManager.ClassifyOperationGuard(
                    isMainThread: true,
                    terminated: false,
                    platformAvailable: true,
                    state: WindowsClipboardManagerState.ShutdownFailed));
        }

        [Test]
        public void Guard_BeforeInitialize_ReportsNotInitializedByHost()
        {
            // Stopping here keeps a caller that forgot to initialize from reaching the native side
            // just to receive its NotInitialized.
            Assert.AreEqual(WindowsClipboardErrorCode.NotInitializedByHost,
                WindowsClipboardManager.ClassifyOperationGuard(
                    isMainThread: true,
                    terminated: false,
                    platformAvailable: true,
                    state: WindowsClipboardManagerState.Uninitialized));
        }

        [Test]
        public void Guard_AfterAFinishedShutdown_ReportsNotInitializedByHost()
        {
            Assert.AreEqual(WindowsClipboardErrorCode.NotInitializedByHost,
                WindowsClipboardManager.ClassifyOperationGuard(
                    isMainThread: true,
                    terminated: false,
                    platformAvailable: true,
                    state: WindowsClipboardManagerState.ShutDown));
        }

        [Test]
        public void Guard_WhileRunningOnAWindowsPlayer_Passes()
        {
            Assert.AreEqual(WindowsClipboardErrorCode.None,
                WindowsClipboardManager.ClassifyOperationGuard(
                    isMainThread: true,
                    terminated: false,
                    platformAvailable: true,
                    state: WindowsClipboardManagerState.Running));
        }

        // The two below check that the live guard is wired to the classifier at all.

        [Test]
        public void OperationGuard_InTheEditor_ReportsPlatformUnavailable()
        {
            WindowsClipboardManager.ResetForTests();
            WindowsClipboardManager.SetStateForTests(WindowsClipboardManagerState.Running);

            Assert.AreEqual(WindowsClipboardErrorCode.PlatformUnavailable,
                WindowsClipboardManager.CheckOperationGuardForTests());
        }

        [Test]
        public void OperationGuard_AfterDestruction_ReportsManagerDestroyedEvenWhileRunning()
        {
            WindowsClipboardManager.SetStateForTests(WindowsClipboardManagerState.Running);
            WindowsClipboardManager.SetTerminatedForTests(true);

            Assert.AreEqual(WindowsClipboardErrorCode.ManagerDestroyed,
                WindowsClipboardManager.CheckOperationGuardForTests());
        }


        [Test]
        public void Shutdown_AnAttemptFromAFinishedManagerDoesNotReportASecondCompletion()
        {
            // OnDestroy now runs an attempt whatever the state, and the native uninit is
            // idempotent, so a finished manager must be left exactly as it was.
            WindowsClipboardManager.SetStateForTests(WindowsClipboardManagerState.Running);
            WindowsClipboardManager.SetComOwnershipForTests(WindowsClipboardComOwnership.Initialized);
            WindowsClipboardManager.InjectShutdownResultForTests(true, WindowsClipboardErrorCode.None);
            Assert.AreEqual(1, WindowsClipboardManager.ComReleaseCountForTests);

            WindowsClipboardManager.InjectShutdownResultForTests(true, WindowsClipboardErrorCode.None);

            Assert.AreEqual(WindowsClipboardManagerState.ShutDown, WindowsClipboardManager.StateForTests);
            Assert.AreEqual(1, WindowsClipboardManager.ComReleaseCountForTests,
                "a second attempt on a finished manager must not release again");
        }

        [Test]
        public void Shutdown_AnAttemptOnAManagerThatNeverStartedIsNotATerminalFailure()
        {
            WindowsClipboardManager.ResetForTests();

            // WrongThread is terminal for a live manager, but there is nothing here to fail.
            WindowsClipboardManager.InjectShutdownResultForTests(
                false, WindowsClipboardErrorCode.WrongThread);

            Assert.AreEqual(WindowsClipboardManagerState.Uninitialized,
                WindowsClipboardManager.StateForTests);
        }

        // ── Lifecycle state machine ──────────────────────────────────────────────

        [Test]
        public void ShutdownAttempt_FromRunning_CompletedReachesShutDown()
        {
            WindowsClipboardManager.SetStateForTests(WindowsClipboardManagerState.Running);

            WindowsClipboardManager.InjectShutdownResultForTests(true, WindowsClipboardErrorCode.None);

            Assert.AreEqual(WindowsClipboardManagerState.ShutDown, WindowsClipboardManager.StateForTests);
        }

        [Test]
        public void ShutdownAttempt_FromRunning_NotYetStaysDraining()
        {
            WindowsClipboardManager.SetStateForTests(WindowsClipboardManagerState.Running);

            WindowsClipboardManager.InjectShutdownResultForTests(false, WindowsClipboardErrorCode.Busy);

            // The native gate is already closed at this point, so staying Running would let new
            // operations through that the native side would reject as NotInitialized.
            Assert.AreEqual(WindowsClipboardManagerState.Draining, WindowsClipboardManager.StateForTests);
        }

        [Test]
        public void ShutdownAttempt_FromRunning_TerminalFailureReachesShutdownFailed()
        {
            WindowsClipboardManager.SetStateForTests(WindowsClipboardManagerState.Running);

            WindowsClipboardManager.InjectShutdownResultForTests(false, WindowsClipboardErrorCode.WrongThread);

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("terminal shutdown failure"));
            Assert.AreEqual(WindowsClipboardManagerState.ShutdownFailed, WindowsClipboardManager.StateForTests);
        }

        [Test]
        public void ShutdownAttempt_FromShutdownFailed_CanStillReachShutDown()
        {
            WindowsClipboardManager.SetStateForTests(WindowsClipboardManagerState.ShutdownFailed);

            WindowsClipboardManager.InjectShutdownResultForTests(true, WindowsClipboardErrorCode.None);

            Assert.AreEqual(WindowsClipboardManagerState.ShutDown, WindowsClipboardManager.StateForTests);
        }

        // ── COM ownership ────────────────────────────────────────────────────────

        [Test]
        public void CompletedShutdown_ReleasesTheComReferenceThisLayerOwns()
        {
            WindowsClipboardManager.SetStateForTests(WindowsClipboardManagerState.Running);
            WindowsClipboardManager.SetComOwnershipForTests(WindowsClipboardComOwnership.Initialized);

            WindowsClipboardManager.InjectShutdownResultForTests(true, WindowsClipboardErrorCode.None);

            Assert.AreEqual(WindowsClipboardComOwnership.None, WindowsClipboardManager.ComOwnershipForTests);
            Assert.AreEqual(1, WindowsClipboardManager.ComReleaseCountForTests, "released exactly once");
        }

        [Test]
        public void IncompleteShutdown_KeepsTheComReference()
        {
            WindowsClipboardManager.SetStateForTests(WindowsClipboardManagerState.Running);
            WindowsClipboardManager.SetComOwnershipForTests(WindowsClipboardComOwnership.RefCounted);

            WindowsClipboardManager.InjectShutdownResultForTests(false, WindowsClipboardErrorCode.Busy);

            // The native side may still hold COM objects and the hidden window, so folding the
            // apartment first would be undefined behaviour.
            Assert.AreEqual(WindowsClipboardComOwnership.RefCounted,
                WindowsClipboardManager.ComOwnershipForTests);
            Assert.AreEqual(0, WindowsClipboardManager.ComReleaseCountForTests);
        }

        [Test]
        public void TerminalShutdownFailure_KeepsTheComReference()
        {
            WindowsClipboardManager.SetStateForTests(WindowsClipboardManagerState.Running);
            WindowsClipboardManager.SetComOwnershipForTests(WindowsClipboardComOwnership.Initialized);

            WindowsClipboardManager.InjectShutdownResultForTests(false, WindowsClipboardErrorCode.WrongThread);

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("terminal shutdown failure"));
            Assert.AreEqual(WindowsClipboardComOwnership.Initialized,
                WindowsClipboardManager.ComOwnershipForTests);
            Assert.AreEqual(0, WindowsClipboardManager.ComReleaseCountForTests);
        }

        [Test]
        public void RepeatedShutdownAttempts_ReleaseTheComReferenceOnlyOnce()
        {
            WindowsClipboardManager.SetStateForTests(WindowsClipboardManagerState.Running);
            WindowsClipboardManager.SetComOwnershipForTests(WindowsClipboardComOwnership.Initialized);

            WindowsClipboardManager.InjectShutdownResultForTests(true, WindowsClipboardErrorCode.None);
            WindowsClipboardManager.InjectShutdownResultForTests(true, WindowsClipboardErrorCode.None);

            Assert.AreEqual(1, WindowsClipboardManager.ComReleaseCountForTests);
        }

        [Test]
        public void ClipboardChangedSeam_WithoutADispatcherIsHarmless()
        {
            // ResetForTests drops the cached dispatcher, which is the state a change notification
            // can arrive in after a teardown. It must be dropped rather than throwing.
            WindowsClipboardManager.ResetForTests();

            Assert.DoesNotThrow(() => WindowsClipboardManager.InjectClipboardChangedForTests());
        }

        [Test]
        public void ResetForTests_ClearsTheLifecycleState()
        {
            WindowsClipboardManager.SetStateForTests(WindowsClipboardManagerState.Draining);
            WindowsClipboardManager.SetComOwnershipForTests(WindowsClipboardComOwnership.RefCounted);

            WindowsClipboardManager.ResetForTests();

            Assert.AreEqual(WindowsClipboardManagerState.Uninitialized, WindowsClipboardManager.StateForTests);
            Assert.AreEqual(WindowsClipboardComOwnership.None, WindowsClipboardManager.ComOwnershipForTests);
            Assert.IsFalse(WindowsClipboardManager.IsTerminated);
            Assert.IsFalse(WindowsClipboardManager.QuitHandlerSubscribedForTests);
        }
    }
}
#endif
