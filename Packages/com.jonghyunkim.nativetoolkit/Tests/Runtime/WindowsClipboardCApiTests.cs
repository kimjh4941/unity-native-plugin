#nullable enable

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using JonghyunKim.NativeToolkit.Runtime.Windows.Clipboard;
using JonghyunKim.NativeToolkit.Runtime.Windows.Common;
using NUnit.Framework;
using static JonghyunKim.NativeToolkit.Runtime.Windows.Clipboard.WindowsClipboardCApi;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// EditMode tests for the Windows clipboard bridge's conversions to and from native-toolkit's C
    /// ABI (artifact/features/clipboard/designs/2026-09-27-windows-clipboard-design-v12.md, 7.1).
    /// </summary>
    public sealed class WindowsClipboardCApiTests
    {
        // ── Structures (native-toolkit CApiLayoutTest.cpp:79-91) ─────────────────

        [Test]
        public void Structures_HaveTheCApiSizes()
        {
            Assert.AreEqual(24, Marshal.SizeOf<SessionOptions>());
            Assert.AreEqual(40, Marshal.SizeOf<HistoryHandlers>());
        }

        [TestCase(nameof(SessionOptions.StructSize), 0)]
        [TestCase(nameof(SessionOptions.Reserved0), 4)]
        [TestCase(nameof(SessionOptions.OnClipboardChanged), 8)]
        [TestCase(nameof(SessionOptions.UserData), 16)]
        public void SessionOptions_FieldOffsets(string field, int offset) =>
            Assert.AreEqual(offset, Marshal.OffsetOf<SessionOptions>(field).ToInt32());

        [TestCase(nameof(HistoryHandlers.StructSize), 0)]
        [TestCase(nameof(HistoryHandlers.Reserved0), 4)]
        [TestCase(nameof(HistoryHandlers.OnHistoryChanged), 8)]
        [TestCase(nameof(HistoryHandlers.OnHistoryEnabledChanged), 16)]
        [TestCase(nameof(HistoryHandlers.OnRoamingEnabledChanged), 24)]
        [TestCase(nameof(HistoryHandlers.UserData), 32)]
        public void HistoryHandlers_FieldOffsets(string field, int offset) =>
            Assert.AreEqual(offset, Marshal.OffsetOf<HistoryHandlers>(field).ToInt32());

        [Test]
        public void BuildSessionOptions_CarriesTheListenerAndNoUserData()
        {
            SessionOptions options = BuildSessionOptions(new IntPtr(5));
            Assert.AreEqual((24u, 0u), (options.StructSize, options.Reserved0));
            Assert.AreEqual(new IntPtr(5), options.OnClipboardChanged);
            Assert.AreEqual(IntPtr.Zero, options.UserData);
            Assert.AreEqual(IntPtr.Zero, BuildSessionOptions(IntPtr.Zero).OnClipboardChanged);
        }

        [Test]
        public void BuildHistoryHandlers_CarriesTheThreeListenersAndNoUserData()
        {
            HistoryHandlers handlers = BuildHistoryHandlers(new IntPtr(1), new IntPtr(2), new IntPtr(3));
            Assert.AreEqual((40u, 0u), (handlers.StructSize, handlers.Reserved0));
            Assert.AreEqual((new IntPtr(1), new IntPtr(2), new IntPtr(3)),
                (handlers.OnHistoryChanged, handlers.OnHistoryEnabledChanged, handlers.OnRoamingEnabledChanged));
            Assert.AreEqual(IntPtr.Zero, handlers.UserData);
        }

        // ── base64 (J-3) ─────────────────────────────────────────────────────────

        [TestCase("QUJD", "ABC")]
        [TestCase("QUJDRA==", "ABCD")]
        [TestCase("QUJDREU=", "ABCDE")]
        [TestCase("QU JD\r\n\tRA==", "ABCD")]
        [TestCase(" QUJD ", "ABC")]
        [TestCase("+/+/", "ûÿ¿")]
        public void TryDecodeBase64_Decodes(string base64, string latin1)
        {
            Assert.IsTrue(TryDecodeBase64(base64, out byte[] bytes), base64);
            CollectionAssert.AreEqual(latin1.Select(c => (byte)c).ToArray(), bytes);
        }

        [Test]
        public void TryDecodeBase64_IgnoresTheUnusedBitsOfTheLastGroup()
        {
            // "QR==" and "QQ==" both carry 'A'; 1.x did not check the leftover bits either.
            Assert.IsTrue(TryDecodeBase64("QR==", out byte[] bytes));
            CollectionAssert.AreEqual(new byte[] { 0x41 }, bytes);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" \r\n\t")]
        [TestCase("QQ=")]
        [TestCase("QUJ")]
        [TestCase("@@@@")]
        [TestCase("Q=Q=")]
        [TestCase("QQ=Q")]
        [TestCase("=QQQ")]
        [TestCase("QQ==QUJD")]
        [TestCase("QUJ=QUJD")]
        [TestCase("QU-_")]
        [TestCase("QUJé")]
        [TestCase("QU\vJD")]
        [TestCase("QU\fJD")]
        [TestCase("QU JD")]
        [TestCase("====")]
        public void TryDecodeBase64_RefusesWhat1xRefused(string? base64)
        {
            Assert.IsFalse(TryDecodeBase64(base64, out byte[] bytes), base64);
            CollectionAssert.IsEmpty(bytes);
        }

        [Test]
        public void TryDecodeBase64_ReadsWhatConvertWrites()
        {
            var random = new Random(7);
            for (int length = 1; length < 40; length++)
            {
                var data = new byte[length];
                random.NextBytes(data);
                Assert.IsTrue(TryDecodeBase64(Convert.ToBase64String(data), out byte[] bytes), $"length {length}");
                CollectionAssert.AreEqual(data, bytes, $"length {length}");
            }
        }

        // ── History timestamp (J-1) ──────────────────────────────────────────────

        [Test]
        public void ToFileTimeTicks_OfZero_IsZero() => Assert.AreEqual(0L, ToFileTimeTicks(0));

        [Test]
        public void ToFileTimeTicks_MatchesDateTimeOffset()
        {
            var moment = new DateTimeOffset(2026, 9, 27, 12, 34, 56, 789, TimeSpan.Zero);
            Assert.AreEqual(moment.ToFileTime(), ToFileTimeTicks(moment.ToUnixTimeMilliseconds()));
        }

        [Test]
        public void ToFileTimeTicks_OfOneMillisecond_IsTheEpochPlusTenThousand() =>
            Assert.AreEqual(116_444_736_000_010_000L, ToFileTimeTicks(1));

        [Test]
        public void ToFileTimeTicks_BeforeTheUnixEpoch_StaysAFileTime()
        {
            var moment = new DateTimeOffset(1960, 1, 1, 0, 0, 0, TimeSpan.Zero);
            Assert.AreEqual(moment.ToFileTime(), ToFileTimeTicks(moment.ToUnixTimeMilliseconds()));
        }

        [Test]
        public void ToFileTimeTicks_ThatOverflows_IsZero()
        {
            Assert.AreEqual(0L, ToFileTimeTicks(long.MaxValue));
            Assert.AreEqual(0L, ToFileTimeTicks(long.MaxValue / 10_000));
        }

        // ── Read results ─────────────────────────────────────────────────────────

        [TestCase(WindowsClipboardErrorCode.None, ReadClass.Value)]
        [TestCase(WindowsClipboardErrorCode.Empty, ReadClass.EmptySuccess)]
        [TestCase(WindowsClipboardErrorCode.FormatUnavailable, ReadClass.EmptySuccess)]
        [TestCase(WindowsClipboardErrorCode.Busy, ReadClass.Failure)]
        [TestCase(WindowsClipboardErrorCode.NotInitialized, ReadClass.Failure)]
        [TestCase(WindowsClipboardErrorCode.InvalidData, ReadClass.Failure)]
        [TestCase(WindowsClipboardErrorCode.OutOfMemory, ReadClass.Failure)]
        public void ClassifyRead_OnlyEmptyAndFormatUnavailableAreEmptySuccesses(WindowsClipboardErrorCode code, object expected) =>
            Assert.AreEqual((ReadClass)expected, ClassifyRead(code));

        [TestCase("abc", "abc")]
        [TestCase("ab\0cd", "ab")]
        [TestCase("\0", "")]
        [TestCase("", "")]
        public void CutAtFirstNul(string text, string expected) => Assert.AreEqual(expected, WindowsClipboardCApi.CutAtFirstNul(text));

        [Test]
        public void ToTextResult_AnEmptyStringIsAValue_ExceptForThePreferredFormat()
        {
            WindowsClipboardTextResult pasted = ToTextResult("paste", WindowsClipboardErrorCode.None, "", emptyTextIsEmpty: false);
            Assert.IsTrue(pasted.IsSuccess);
            Assert.IsFalse(pasted.IsEmpty);
            Assert.AreEqual("", pasted.Text);

            WindowsClipboardTextResult preferred = ToTextResult("preferred", WindowsClipboardErrorCode.None, "", emptyTextIsEmpty: true);
            Assert.IsTrue(preferred.IsSuccess);
            Assert.IsTrue(preferred.IsEmpty);
        }

        [Test]
        public void ToTextResult_CutsAtTheFirstNul() =>
            Assert.AreEqual("ab", ToTextResult("paste", WindowsClipboardErrorCode.None, "ab\0cd", false).Text);

        [TestCase(WindowsClipboardErrorCode.Empty)]
        [TestCase(WindowsClipboardErrorCode.FormatUnavailable)]
        public void ToResults_EmptyAndFormatUnavailable_AreEmptySuccesses(WindowsClipboardErrorCode code)
        {
            WindowsClipboardTextResult text = ToTextResult("t", code, null, false);
            Assert.IsTrue(text.IsSuccess && text.IsEmpty);
            WindowsClipboardStringListResult list = ToListResult("l", code, null);
            Assert.IsTrue(list.IsSuccess && list.IsEmpty);
            WindowsClipboardBytesResult bytes = ToBytesResult("b", code, null);
            Assert.IsTrue(bytes.IsSuccess && bytes.IsEmpty);
        }

        [TestCase(WindowsClipboardErrorCode.Busy)]
        [TestCase(WindowsClipboardErrorCode.InvalidData)]
        [TestCase(WindowsClipboardErrorCode.NotInitialized)]
        public void ToResults_OtherFailures_StayFailures(WindowsClipboardErrorCode code)
        {
            WindowsClipboardTextResult text = ToTextResult("t", code, "ignored", false);
            Assert.AreEqual((false, code), (text.IsSuccess, text.ErrorCode));
            WindowsClipboardStringListResult list = ToListResult("l", code, new[] { "ignored" });
            Assert.AreEqual((false, code), (list.IsSuccess, list.ErrorCode));
            WindowsClipboardBytesResult bytes = ToBytesResult("b", code, new byte[] { 1 });
            Assert.AreEqual((false, code), (bytes.IsSuccess, bytes.ErrorCode));
        }

        [Test]
        public void ToListResult_NoEntries_IsASuccess_AsThe1xEmptyArrayWas()
        {
            // 1.x parsed "[]" into Success with no values, which the result type marks IsEmpty.
            WindowsClipboardStringListResult formats = ToListResult("formats", WindowsClipboardErrorCode.None, Array.Empty<string>());
            Assert.IsTrue(formats.IsSuccess);
            Assert.AreEqual(WindowsClipboardStringListResult.Success("formats", Array.Empty<string>()).IsEmpty, formats.IsEmpty);
            CollectionAssert.IsEmpty(formats.Values);
        }

        [Test]
        public void ToBytesResult_CarriesTheBytes() =>
            CollectionAssert.AreEqual(new byte[] { 1, 2 }, ToBytesResult("b", WindowsClipboardErrorCode.None, new byte[] { 1, 2 }).Data);

        // ── History items ────────────────────────────────────────────────────────

        [Test]
        public void ToHistoryItem_CarriesEveryField()
        {
            var moment = new DateTimeOffset(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);
            WindowsClipboardHistoryItem? item = ToHistoryItem("id-1", "text", new[] { "Text", "Html" }, moment.ToUnixTimeMilliseconds());
            Assert.IsNotNull(item);
            Assert.AreEqual(("id-1", "text"), (item!.Id, item.Text));
            CollectionAssert.AreEqual(new[] { "Text", "Html" }, item.ContentTypes);
            Assert.AreEqual(moment, item.ToUtcTime());
        }

        [TestCase(null)]
        [TestCase("")]
        public void ToHistoryItem_WithoutAnId_IsDropped(string? id) =>
            Assert.IsNull(ToHistoryItem(id, "text", null, 1));

        [TestCase(null)]
        [TestCase("")]
        public void ToHistoryItem_AbsentOrEmptyText_IsNull(string? text) =>
            Assert.IsNull(ToHistoryItem("id", text, null, 1)!.Text);

        [Test]
        public void ToHistoryItem_WithoutContentTypes_HasAnEmptyList() =>
            CollectionAssert.IsEmpty(ToHistoryItem("id", null, null, 1)!.ContentTypes);

        [Test]
        public void ToHistoryItem_WithoutATime_KeepsZero() =>
            Assert.AreEqual(0L, ToHistoryItem("id", null, null, 0)!.Timestamp);

        [Test]
        public void ToUtcTime_OutOfRangeValueReturnsNullInsteadOfThrowing()
        {
            // Moved from WindowsClipboardJsonParserTests: the public type still guards its own range.
            var item = new WindowsClipboardHistoryItem("a", null, null, long.MaxValue);
            Assert.IsNull(item.ToUtcTime());
        }

        // ── Deferred rendering (J-8) ─────────────────────────────────────────────

        [Test]
        public void Render_HandsTheBytesToTheTargetOnce_AndReturnsItsCode()
        {
            int providerCalls = 0, targetCalls = 0;
            byte[]? handed = null;
            WindowsClipboardErrorCode code = Render(() => { providerCalls++; return new byte[] { 9 }; },
                bytes => { targetCalls++; handed = bytes; return WindowsClipboardErrorCode.None; }, out string? failure);
            Assert.AreEqual(WindowsClipboardErrorCode.None, code);
            Assert.AreEqual((1, 1), (providerCalls, targetCalls));
            CollectionAssert.AreEqual(new byte[] { 9 }, handed);
            Assert.IsNull(failure);
        }

        [TestCase(WindowsClipboardErrorCode.InvalidParameter)]
        [TestCase(WindowsClipboardErrorCode.OutOfMemory)]
        public void Render_ATargetFailure_IsReturnedAsItIs(WindowsClipboardErrorCode targetCode)
        {
            Assert.AreEqual(targetCode, Render(() => new byte[] { 1 }, _ => targetCode, out string? failure));
            Assert.IsNotNull(failure);
        }

        [Test]
        public void Render_WithoutAProvider_IsInvalidParameter() =>
            Assert.AreEqual(WindowsClipboardErrorCode.InvalidParameter, Render(null, _ => WindowsClipboardErrorCode.None, out _));

        [Test]
        public void Render_NullOrEmptyBytes_AreInvalidData_AndReachNoTarget()
        {
            bool reached = false;
            Assert.AreEqual(WindowsClipboardErrorCode.InvalidData, Render(() => null!, _ => { reached = true; return 0; }, out _));
            Assert.AreEqual(WindowsClipboardErrorCode.InvalidData, Render(Array.Empty<byte>, _ => { reached = true; return 0; }, out _));
            Assert.IsFalse(reached);
        }

        [Test]
        public void Render_AProviderThatThrows_IsUnknown()
        {
            Assert.AreEqual(WindowsClipboardErrorCode.Unknown,
                Render(() => throw new InvalidOperationException(), _ => WindowsClipboardErrorCode.None, out string? failure));
            Assert.AreEqual(nameof(InvalidOperationException), failure);
        }

        // ── Codes ────────────────────────────────────────────────────────────────

        [Test]
        public void FromLifecycleException_LoadFailuresAreBridgeUnavailable_OthersUnknown()
        {
            Assert.AreEqual(WindowsClipboardErrorCode.BridgeUnavailable, FromLifecycleException(new DllNotFoundException()));
            Assert.AreEqual(WindowsClipboardErrorCode.BridgeUnavailable, FromLifecycleException(new EntryPointNotFoundException()));
            Assert.AreEqual(WindowsClipboardErrorCode.BridgeUnavailable, FromLifecycleException(new BadImageFormatException()));
            Assert.AreEqual(WindowsClipboardErrorCode.Unknown, FromLifecycleException(new OutOfMemoryException()));
            Assert.IsTrue(IsBridgeFailure(new BadImageFormatException()));
            Assert.IsFalse(IsBridgeFailure(new InvalidOperationException()));
        }

        [Test]
        public void StatusFor_TheNativeStates()
        {
            Assert.AreEqual(WindowsClipboardErrorCode.None, StatusFor(WindowsNativeToolkitCApi.NativeState.Available));
            Assert.AreEqual(WindowsClipboardErrorCode.BridgeUnavailable, StatusFor(WindowsNativeToolkitCApi.NativeState.NativeUnavailable));
            Assert.AreEqual(WindowsClipboardErrorCode.PlatformUnavailable, StatusFor(WindowsNativeToolkitCApi.NativeState.PlatformUnavailable));
        }

        [Test]
        public void ShouldSkipNativeClose_OnlyWithoutASession()
        {
            Assert.IsTrue(ShouldSkipNativeClose(IntPtr.Zero));
            Assert.IsFalse(ShouldSkipNativeClose(new IntPtr(1)));
        }

        [Test]
        public void ErrorCodes_MatchTheCApiByNumber()
        {
            // ntk_clipboard_error (Clipboard.h:41-63) shares names and numbers with the public enum.
            Assert.AreEqual(0, (int)WindowsClipboardErrorCode.None);
            Assert.AreEqual(7, (int)WindowsClipboardErrorCode.BufferTooSmall);
            Assert.AreEqual(15, (int)WindowsClipboardErrorCode.Canceled);
            Assert.AreEqual(19, (int)WindowsClipboardErrorCode.Unknown);
        }
    }
}
#endif
