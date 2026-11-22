#nullable enable

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using NUnit.Framework;
using UnityEngine.TestTools;
using JonghyunKim.NativeToolkit.Runtime.iOS.Clipboard;
using JonghyunKim.NativeToolkit.Runtime.iOS.Common;
using Api = JonghyunKim.NativeToolkit.Runtime.iOS.Clipboard.IosClipboardCApi;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// Layer 1 tests for <c>IosClipboardCApi</c> (artifact/ios/clipboard, C ABI design v5): the structs against
    /// <c>Clipboard.h</c>'s layout (native-toolkit's StructLayoutTests.m), the requests (D-8), the errors (D-3),
    /// and the pure part that builds the results from the values read off the handles (D-9, table 2.1, D-13).
    /// </summary>
    public sealed class IosClipboardCApiTests
    {
        private readonly List<IntPtr> _allocated = new();

        [TearDown]
        public void FreeAllocated() => IosNativeToolkitCApi.FreeAll(_allocated);

        private static void AssertLayout<T>(int size, params (string field, int offset)[] fields) where T : struct
        {
            Assert.AreEqual(size, Marshal.SizeOf<T>(), $"sizeof {typeof(T).Name}");
            Assert.AreEqual(fields.Length, typeof(T).GetFields().Length, $"{typeof(T).Name} field count");
            foreach (var (field, offset) in fields)
                Assert.AreEqual(offset, Marshal.OffsetOf<T>(field).ToInt32(), $"{typeof(T).Name}.{field}");
        }

        // ── Structs (F-3) ──

        [Test]
        public void Scope_Layout() => AssertLayout<Api.Scope>(24,
            ("StructSize", 0), ("Reserved0", 4), ("Kind", 8), ("Reserved1", 12), ("Name", 16));

        [Test]
        public void Representation_Layout() => AssertLayout<Api.Representation>(32,
            ("StructSize", 0), ("Reserved0", 4), ("UtType", 8), ("Data", 16), ("DataSize", 24));

        [Test]
        public void WriteContent_Layout() => AssertLayout<Api.WriteContent>(136,
            ("StructSize", 0), ("Reserved0", 4), ("Kind", 8), ("Reserved1", 12), ("Text", 16), ("Html", 24),
            ("Url", 32), ("Path", 40), ("Data", 48), ("DataSize", 56), ("UtType", 64), ("Red", 72),
            ("Green", 80), ("Blue", 88), ("Alpha", 96), ("Texts", 104), ("TextCount", 112),
            ("Representations", 120), ("RepresentationCount", 128));

        [Test]
        public void CopyOptions_Layout() => AssertLayout<Api.CopyOptions>(24,
            ("StructSize", 0), ("Reserved0", 4), ("NotLocalOnly", 8), ("HasExpiration", 12), ("ExpirationUnixSeconds", 16));

        // ── Errors (D-3) ──

        [TestCase(Api.ErrorInvalidParameter, "CLIPBOARD_INVALID_REQUEST", "The request is invalid.")]
        [TestCase(Api.ErrorInvalidUri, "CLIPBOARD_INVALID_URL", "The URL is invalid.")]
        [TestCase(Api.ErrorCanceled, "CLIPBOARD_CANCELLED", "The clipboard load was cancelled.")]
        [TestCase(Api.ErrorCannotRemoveGeneral, "CLIPBOARD_CANNOT_REMOVE_GENERAL", "The general pasteboard cannot be removed.")]
        [TestCase(Api.ErrorEmptyItems, "CLIPBOARD_EMPTY_ITEMS", "No items provided for clipboard copy.")]
        [TestCase(Api.ErrorContentTooLarge, "CLIPBOARD_CONTENT_TOO_LARGE", "The clipboard content exceeds the configured size limit.")]
        [TestCase(Api.ErrorUnknown, "CLIPBOARD_UNKNOWN", "An unknown error occurred.")]
        [TestCase(Api.ErrorNotInitialized, "CLIPBOARD_UNKNOWN", "An unknown error occurred.")]
        [TestCase(Api.ErrorOutOfMemory, "CLIPBOARD_UNKNOWN", "An unknown error occurred.")]
        [TestCase(10, "CLIPBOARD_UNKNOWN", "An unknown error occurred.")]
        [TestCase(999, "CLIPBOARD_UNKNOWN", "An unknown error occurred.")]
        public void ToErrorInfo_KeepsThe1xCodeAndMessage(int error, string code, string message)
        {
            IosClipboardErrorInfo info = Api.ToErrorInfo(error);
            Assert.AreEqual(code, info.Code);
            Assert.AreEqual(message, info.Message);
            Assert.IsNull(info.Domain);
            Assert.IsNull(info.NativeCode);
        }

        // ── Patterns (D-8 (4)) ──

        [Test]
        public void PatternBits_FollowTheHeadersOrder()
        {
            var all = (IosClipboardDetectionPattern[])Enum.GetValues(typeof(IosClipboardDetectionPattern));
            Assert.AreEqual(11, all.Length);
            for (int bit = 0; bit < all.Length; bit++)
            {
                Assert.IsTrue(Api.TryPatternBits(new[] { all[bit] }, out uint bits));
                Assert.AreEqual(1u << bit, bits, all[bit].ToString());
            }
            Assert.IsTrue(Api.TryPatternBits(new[] { IosClipboardDetectionPattern.Link, IosClipboardDetectionPattern.ProbableWebUrl }, out uint two));
            Assert.AreEqual((1u << 3) | 1u, two);
        }

        [TestCase(11)]
        [TestCase(35)]
        [TestCase(-1)]
        public void PatternOutsideTheEnum_IsNotTakenForAnother(int value)
        {
            Assert.IsFalse(Api.TryPatternBits(new[] { IosClipboardDetectionPattern.Number, (IosClipboardDetectionPattern)value }, out _));
        }

        [Test]
        public void PatternsOf_ReadsTheBitsAndSkipsUnknownOnes()
        {
            CollectionAssert.AreEqual(
                new[] { IosClipboardDetectionPattern.ProbableWebUrl, IosClipboardDetectionPattern.ShipmentTrackingNumber },
                Api.PatternsOf(1u | (1u << 10) | (1u << 20)));
            CollectionAssert.IsEmpty(Api.PatternsOf(0));
        }

        // ── U+0000 (D-8 (5)) ──

        [Test]
        public void Nul_InNamesTypesAndPaths_IsFound()
        {
            Assert.IsTrue(Api.ScopeHasNul(IosPasteboardScope.Named("a\0b")));
            Assert.IsFalse(Api.ScopeHasNul(IosPasteboardScope.Named("ab")));
            Assert.IsFalse(Api.ScopeHasNul(null));
            Assert.IsFalse(Api.ScopeHasNul(IosPasteboardScope.General));
            Assert.IsTrue(Api.ContentTypeHasNul(IosClipboardContent.CustomData(new byte[] { 1 }, "com.x\0y")));
            Assert.IsTrue(Api.ContentTypeHasNul(IosClipboardContent.ImageData(new byte[] { 1 }, "public.png\0")));
            Assert.IsTrue(Api.ContentTypeHasNul(IosClipboardContent.ImageFile("/tmp/a.png\0.tmp")), "a path cut at U+0000 could name another file");
            Assert.IsTrue(Api.ContentTypeHasNul(IosClipboardContent.MultiRepresentation(
                new Dictionary<string, byte[]> { ["com.a\0b"] = new byte[] { 1 } })));
            Assert.IsFalse(Api.ContentTypeHasNul(IosClipboardContent.PlainText("a\0b")), "a value is not a name");
        }

        [Test]
        public void Nul_InValues_IsFoundForTheWarning()
        {
            Assert.IsTrue(Api.ContentValueHasNul(IosClipboardContent.PlainText("a\0b")));
            Assert.IsTrue(Api.ContentValueHasNul(IosClipboardContent.HtmlText("a", "<b>\0</b>")));
            Assert.IsTrue(Api.ContentValueHasNul(IosClipboardContent.Url("https://e.com/\0")));
            Assert.IsTrue(Api.ContentValueHasNul(IosClipboardContent.MultipleText(new[] { "a", "b\0" })));
            Assert.IsFalse(Api.ContentValueHasNul(IosClipboardContent.PlainText("ab")));
            Assert.IsFalse(Api.ContentValueHasNul(IosClipboardContent.ImageFile("/tmp/a.png\0")), "a path is rejected, not warned about");
            StringAssert.DoesNotContain("{", Api.NulValueWarning);
        }

        // ── Requests (D-8) ──

        [Test]
        public void Scope_IsNullForGeneralAndAStructOtherwise()
        {
            Assert.AreEqual(IntPtr.Zero, Api.BuildScope(null, _allocated));
            Assert.AreEqual(IntPtr.Zero, Api.BuildScope(IosPasteboardScope.General, _allocated));

            IntPtr named = Api.BuildScope(IosPasteboardScope.Named("ntk.name"), _allocated);
            var scope = Marshal.PtrToStructure<Api.Scope>(named);
            Assert.AreEqual(24u, scope.StructSize);
            Assert.AreEqual(Api.ScopeNamed, scope.Kind);
            Assert.AreEqual("ntk.name", Marshal.PtrToStringAnsi(scope.Name));

            IntPtr unique = Api.BuildScope(IosPasteboardScope.Unique("u"), _allocated);
            Assert.AreEqual(Api.ScopeUnique, Marshal.PtrToStructure<Api.Scope>(unique).Kind);
        }

        [Test]
        public void RemoveScope_PassesGeneralAsAStruct()
        {
            // remove rejects NULL; GENERAL reaches the library's CANNOT_REMOVE_GENERAL (CP-21).
            IntPtr general = Api.BuildRequiredScope(IosPasteboardScope.General, _allocated);
            Assert.AreNotEqual(IntPtr.Zero, general);
            var scope = Marshal.PtrToStructure<Api.Scope>(general);
            Assert.AreEqual(24u, scope.StructSize);
            Assert.AreEqual(Api.ScopeGeneral, scope.Kind);
            Assert.AreEqual(IntPtr.Zero, scope.Name);
        }

        [Test]
        public void ContentKinds_AreTheHeadersValues()
        {
            var cases = new (IosClipboardContent content, int kind)[]
            {
                (IosClipboardContent.PlainText("a"), 0),
                (IosClipboardContent.HtmlText("a", "<b>a</b>"), 1),
                (IosClipboardContent.Url("https://e.com"), 2),
                (IosClipboardContent.ImageFile("/tmp/a.png"), 3),
                (IosClipboardContent.ImageData(new byte[] { 1 }, "public.png"), 4),
                (IosClipboardContent.Color(0, 0, 0, 1), 5),
                (IosClipboardContent.CustomData(new byte[] { 1 }, "com.x"), 6),
                (IosClipboardContent.MultipleText(new[] { "a" }), 7),
                (IosClipboardContent.MultiRepresentation(new Dictionary<string, byte[]> { ["com.x"] = new byte[] { 1 } }), 8),
            };
            foreach (var (content, kind) in cases)
            {
                Api.WriteContent built = Api.BuildContentStruct(content, _allocated);
                Assert.AreEqual(136u, built.StructSize);
                Assert.AreEqual(kind, built.Kind, content.Kind.ToString());
            }
        }

        [Test]
        public void Content_CarriesItsFields()
        {
            Api.WriteContent html = Api.BuildContentStruct(IosClipboardContent.HtmlText("plain", "<b>x</b>"), _allocated);
            Assert.AreEqual("plain", Marshal.PtrToStringAnsi(html.Text));
            Assert.AreEqual("<b>x</b>", Marshal.PtrToStringAnsi(html.Html));

            Api.WriteContent data = Api.BuildContentStruct(IosClipboardContent.CustomData(new byte[] { 1, 2, 3 }, "com.x"), _allocated);
            Assert.AreEqual(3u, data.DataSize.ToUInt32());
            Assert.AreEqual("com.x", Marshal.PtrToStringAnsi(data.UtType));

            Api.WriteContent color = Api.BuildContentStruct(IosClipboardContent.Color(0.25, 0.5, 0.75, 1), _allocated);
            Assert.AreEqual((0.25, 0.5, 0.75, 1.0), (color.Red, color.Green, color.Blue, color.Alpha));

            Api.WriteContent texts = Api.BuildContentStruct(IosClipboardContent.MultipleText(new[] { "a", "b" }), _allocated);
            Assert.AreEqual(2u, texts.TextCount.ToUInt32());

            Api.WriteContent reps = Api.BuildContentStruct(IosClipboardContent.MultiRepresentation(
                new Dictionary<string, byte[]> { ["com.a"] = new byte[] { 1 }, ["com.b"] = new byte[] { 2, 3 } }), _allocated);
            Assert.AreEqual(2u, reps.RepresentationCount.ToUInt32());
            var second = Marshal.PtrToStructure<Api.Representation>(IntPtr.Add(reps.Representations, 32));
            Assert.AreEqual(32u, second.StructSize);
        }

        [Test]
        public void Options_InvertLocalOnly()
        {
            Assert.AreEqual(0, Api.BuildOptionsStruct(IosClipboardCopyOptions.Create(true)).NotLocalOnly);
            Assert.AreEqual(1, Api.BuildOptionsStruct(IosClipboardCopyOptions.Create(false)).NotLocalOnly);
            Assert.AreEqual(0, Api.BuildOptionsStruct(IosClipboardCopyOptions.Create(true)).HasExpiration);
            Assert.AreEqual(IntPtr.Zero, Api.BuildOptions(null, _allocated));
        }

        [Test]
        public void Expiration_IsWholeUnixSecondsAsIn1x()
        {
            var whole = new DateTime(2030, 1, 2, 3, 4, 5, DateTimeKind.Utc);
            double expected = (whole - DateTime.UnixEpoch).Ticks / TimeSpan.TicksPerSecond;
            Assert.AreEqual(expected, Api.UnixSeconds(whole));
            Assert.AreEqual(expected, Api.UnixSeconds(whole.AddMilliseconds(999)), "the fraction is dropped");
            Assert.AreEqual(expected, Api.UnixSeconds(whole.ToLocalTime()), "a local time is converted");
            Assert.AreEqual(-1, Api.UnixSeconds(DateTime.UnixEpoch.AddMilliseconds(-1)), "before 1970, toward the past");

            Api.CopyOptions options = Api.BuildOptionsStruct(IosClipboardCopyOptions.Create(true, whole.AddMilliseconds(500)));
            Assert.AreEqual(1, options.HasExpiration);
            Assert.AreEqual(expected, options.ExpirationUnixSeconds);
        }

        [Test]
        public void LoadKinds_AreTheHeadersValues()
        {
            Assert.AreEqual(0, Api.ItemKindOf(IosClipboardLoadRequestKind.Text));
            Assert.AreEqual(1, Api.ItemKindOf(IosClipboardLoadRequestKind.Url));
            Assert.AreEqual(2, Api.ItemKindOf(IosClipboardLoadRequestKind.Image));
            Assert.AreEqual(3, Api.ItemKindOf(IosClipboardLoadRequestKind.File));
        }

        // ── Results (table 2.1) ──

        [Test]
        public void OperationResult_SuccessAndFailure()
        {
            Assert.IsTrue(Api.OperationResultOf("copy", Api.ErrorNone).IsSuccess);
            IosClipboardOperationResult failed = Api.OperationResultOf("copy", Api.ErrorInvalidColor);
            Assert.AreEqual("copy", failed.Operation);
            Assert.AreEqual("CLIPBOARD_INVALID_COLOR", failed.Error?.Code);
        }

        [Test]
        public void ReadData_NoBytesIsNoDataAndTooLargeIsNotCopied()
        {
            Assert.IsFalse(Api.ReadDataResultOf("com.x", false, 0, () => throw new AssertionException("copied")).HasData);

            IosClipboardReadDataResult tooLarge = Api.ReadDataResultOf(
                "com.x", true, (ulong)Api.MaxReadDataBytes + 1, () => throw new AssertionException("copied"));
            Assert.AreEqual("CLIPBOARD_CONTENT_TOO_LARGE", tooLarge.Error?.Code);
            Assert.AreEqual("The clipboard content exceeds the configured size limit.", tooLarge.Error?.Message);

            IosClipboardReadDataResult atLimit = Api.ReadDataResultOf("com.x", true, (ulong)Api.MaxReadDataBytes, () => new byte[] { 7 });
            Assert.IsTrue(atLimit.IsSuccess);

            IosClipboardReadDataResult ok = Api.ReadDataResultOf("com.asked", true, 2, () => new byte[] { 1, 2 });
            Assert.AreEqual("com.asked", ok.UtType, "the type that was asked for");
            CollectionAssert.AreEqual(new byte[] { 1, 2 }, ok.Data);
        }

        [Test]
        public void Snapshot_MatchingIsNullWhenNotAskedAndDropsOutOfRange()
        {
            var rows = new List<IReadOnlyList<string>> { new[] { "public.plain-text" }, new[] { "public.url" } };
            IosClipboardSnapshot none = Api.SnapshotOf(true, true, false, false, 2, new[] { "public.plain-text" }, rows, false, new long[0]);
            Assert.IsNull(none.MatchingItemIndexes);
            Assert.AreEqual(2, none.AllTypeIdentifiers.Count);
            Assert.IsTrue(none.HasStrings && none.HasUrls && !none.HasImages && !none.HasColors);

            IosClipboardSnapshot some = Api.SnapshotOf(false, false, false, false, 2, new string[0], rows, true, new long[] { 1, -1 });
            CollectionAssert.AreEqual(new[] { 1 }, some.MatchingItemIndexes);

            IosClipboardSnapshot empty = Api.SnapshotOf(false, false, false, false, 0, new string[0], new List<IReadOnlyList<string>>(), true, new long[0]);
            CollectionAssert.IsEmpty(empty.MatchingItemIndexes);
        }

        [Test]
        public void CreatedScope_TakesTheKindFromTheRequest()
        {
            IosPasteboardScope named = Api.CreatedScopeOf(IosPasteboardCreationRequestKind.Named, "n");
            Assert.AreEqual((IosPasteboardScopeKind.Named, "n"), (named.Kind, named.Name));
            IosPasteboardScope unique = Api.CreatedScopeOf(IosPasteboardCreationRequestKind.Unique, "u");
            Assert.AreEqual((IosPasteboardScopeKind.Unique, "u"), (unique.Kind, unique.Name));
        }

        [Test]
        public void LoadedItem_ByKind()
        {
            Assert.AreEqual("t", Api.LoadedItemOf(Api.ItemText, "t", null, null, null, null).Text);
            Assert.AreEqual("https://e.com", Api.LoadedItemOf(Api.ItemUrl, null, "https://e.com", null, null, null).UrlString);
            IosClipboardLoadedItem image = Api.LoadedItemOf(Api.ItemImage, null, null, new byte[] { 1 }, "public.png", null);
            Assert.AreEqual(IosClipboardLoadedItemKind.ImageData, image.Kind);
            Assert.AreEqual("public.png", image.UtType);
            Assert.AreEqual("/tmp/f", Api.LoadedItemOf(Api.ItemFile, null, null, null, null, "/tmp/f").Path);
            Assert.AreEqual(IosClipboardLoadedItemKind.Unknown, Api.LoadedItemOf(-1, null, null, null, null, null).Kind);
            Assert.AreEqual(IosClipboardLoadedItemKind.Unknown, Api.LoadedItemOf(9, null, null, null, null, null).Kind);
        }

        [Test]
        public void Change_KindAndScope()
        {
            var added = new[] { "public.plain-text" };
            var removed = new string[0];
            IosClipboardChangeEvent general = Api.ChangeOf(Api.ChangeChanged, Api.ScopeGeneral, null, added, removed);
            Assert.AreEqual(IosClipboardChangeEventKind.Changed, general.Kind);
            Assert.AreEqual(IosPasteboardScopeKind.General, general.Scope?.Kind);
            CollectionAssert.AreEqual(added, general.TypesAdded);

            IosClipboardChangeEvent named = Api.ChangeOf(Api.ChangeRemoved, Api.ScopeNamed, "n", added, removed);
            Assert.AreEqual(IosClipboardChangeEventKind.Removed, named.Kind);
            Assert.AreEqual((IosPasteboardScopeKind.Named, "n"), (named.Scope!.Kind, named.Scope.Name));

            Assert.AreEqual(IosPasteboardScopeKind.Unique, Api.ChangeOf(0, Api.ScopeUnique, "u", added, removed).Scope?.Kind);
            Assert.IsNull(Api.ChangeOf(0, -1, null, added, removed).Scope);
            Assert.IsNull(Api.ChangeOf(0, Api.ScopeNamed, null, added, removed).Scope);
            Assert.AreEqual(IosClipboardChangeEventKind.Unknown, Api.ChangeOf(5, 0, null, added, removed).Kind);
        }

        // ── The manager's completion core (D-9 (1)(2)) ──

        [Test]
        public void ResultOf_ErrorMissingHandleAndThrowingRead()
        {
            IosClipboardReadResult Read(IntPtr _) => IosClipboardReadResult.Success(0, new List<IosClipboardItem>());

            Assert.AreEqual("CLIPBOARD_UNAVAILABLE",
                IosClipboardManager.ResultOf<IosClipboardReadResult>(Api.ErrorUnavailable, IntPtr.Zero, IosClipboardReadResult.Failure, Read).Error?.Code);
            Assert.AreEqual("CLIPBOARD_UNKNOWN",
                IosClipboardManager.ResultOf<IosClipboardReadResult>(Api.ErrorNone, IntPtr.Zero, IosClipboardReadResult.Failure, Read).Error?.Code,
                "NONE without the handle");
            Assert.IsTrue(IosClipboardManager.ResultOf<IosClipboardReadResult>(Api.ErrorNone, new IntPtr(1), IosClipboardReadResult.Failure, Read).IsSuccess);

            LogAssert.Expect(UnityEngine.LogType.Error, new System.Text.RegularExpressions.Regex("could not read a clipboard result"));
            IosClipboardReadResult thrown = IosClipboardManager.ResultOf<IosClipboardReadResult>(
                Api.ErrorNone, new IntPtr(1), IosClipboardReadResult.Failure, _ => throw new OutOfMemoryException());
            Assert.AreEqual("CLIPBOARD_UNKNOWN", thrown.Error?.Code);
        }

        [Test]
        public void OperationNames_ForEveryUserData()
        {
            Assert.IsNull(IosClipboardManager.OperationNameOf(0), "0 is destroy's stop");
            Assert.IsNull(IosClipboardManager.OperationNameOf((Api.Op)15));
            Assert.AreEqual(IosClipboardManager.OperationCopy, IosClipboardManager.OperationNameOf(Api.Op.Copy));
            Assert.AreEqual(IosClipboardManager.OperationStopObserving, IosClipboardManager.OperationNameOf(Api.Op.StopObserving));
            Assert.AreEqual("checkForegroundChange", IosClipboardManager.OperationNameOf(Api.Op.CheckForegroundChange));
            foreach (Api.Op op in Enum.GetValues(typeof(Api.Op)))
                Assert.IsNotNull(IosClipboardManager.OperationNameOf(op), op.ToString());
        }

        // ── DetectValues (D-11) ──

        [Test]
        public void BareDetectedValues_AreRead()
        {
            const string json = "{\"detectedPatterns\":[\"probableWebURL\"],\"probableWebURL\":\"https://e.com\"," +
                                "\"probableWebSearch\":null,\"number\":null,\"links\":[],\"emailAddresses\":[],\"phoneNumbers\":[]," +
                                "\"postalAddresses\":[],\"calendarEvents\":[],\"flightNumbers\":[],\"moneyAmounts\":[]," +
                                "\"shipmentTrackingNumbers\":[]}";
            IosClipboardDetectedValuesResult result = IosClipboardJsonParser.ParseBareDetectedValues(json);
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("https://e.com", result.Values!.ProbableWebUrl);
            CollectionAssert.AreEqual(new[] { IosClipboardDetectionPattern.ProbableWebUrl }, result.Values.DetectedPatterns);
        }
    }
}
