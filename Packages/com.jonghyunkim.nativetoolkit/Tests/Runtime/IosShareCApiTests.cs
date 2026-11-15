#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using NUnit.Framework;
using JonghyunKim.NativeToolkit.Runtime.iOS.Common;
using JonghyunKim.NativeToolkit.Runtime.iOS.Share;
using Api = JonghyunKim.NativeToolkit.Runtime.iOS.Share.IosShareCApi;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// Layer 1 tests for <c>IosShareCApi</c> and <c>IosSharePendingCalls</c>: the structs against
    /// <c>Share.h</c>'s layout, how a payload is passed on as 1.x did, the request, the messages, and the
    /// per-call table (artifact/ios/share, C ABI design v3, 4).
    /// </summary>
    public sealed class IosShareCApiTests
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

        [Test]
        public void ShareItem_Layout() => AssertLayout<Api.ShareItem>(24,
            ("StructSize", 0), ("Reserved0", 4), ("Kind", 8), ("Reserved1", 12), ("Value", 16));

        [Test]
        public void ShareRequest_Layout() => AssertLayout<Api.ShareRequest>(56,
            ("StructSize", 0), ("Reserved0", 4), ("Items", 8), ("ItemCount", 16), ("Subject", 24),
            ("PreviewTitle", 32), ("ExcludedActivityTypes", 40), ("ExcludedActivityTypeCount", 48));

        [Test]
        public void Counts_ArePointerSized()
        {
            Assert.AreEqual(typeof(UIntPtr), typeof(Api.ShareRequest).GetField("ItemCount")!.FieldType);
            Assert.AreEqual(typeof(UIntPtr), typeof(Api.ShareRequest).GetField("ExcludedActivityTypeCount")!.FieldType);
        }

        // ── Passing a payload on as 1.x did (D-2 to D-5) ──────────────────────────

        [TestCase("text", 0)]
        [TestCase("url", 1)]
        [TestCase("image", 2)]
        [TestCase("file", 3)]
        [TestCase("Text", -1)]
        [TestCase("unknown", -1)]
        [TestCase(null, -1)]
        public void KindOf_IsCaseSensitive(string? type, int kind) => Assert.AreEqual(kind, Api.KindOf(type));

        [Test]
        public void Prepare_LeavesOutNullItemsUnknownTypesAndNullValues()
        {
            var payload = new IosShareContentPayload
            {
                items = new[]
                {
                    IosShareItem.Text("a"), null!, new IosShareItem { type = "unknown", value = "x" },
                    new IosShareItem { type = "url", value = null! }, IosShareItem.File("/f"), IosShareItem.Text(""),
                },
            };
            Api.Prepared p = Api.Prepare(payload);
            CollectionAssert.AreEqual(new[] { (0, "a"), (3, "/f"), (0, "") }, p.Items.Select(i => (i.Kind, i.Value)));
            Assert.AreEqual(3, p.DroppedItems);
            Assert.AreEqual("Share: left out 3 item(s) of type <null>, unknown, url (null, unknown type or null value), as 1.x did.", Api.Warnings(p).Single());
        }

        [Test]
        public void Warnings_NeverShowValuesOrPaths()
        {
            Api.Prepared p = Api.Prepare(new IosShareContentPayload
            {
                items = new[] { new IosShareItem { type = "unknown", value = "/private/secret.txt" }, IosShareItem.Text("kept\0x") },
                excludedActivityTypes = new[] { null!, "a.b" },
            });
            foreach (string warning in Api.Warnings(p))
            {
                StringAssert.DoesNotContain("secret", warning);
                StringAssert.DoesNotContain("kept", warning);
            }
            Assert.AreEqual(3, Api.Warnings(p).Count());
        }

        [Test]
        public void Prepare_DropsAnExcludedListHoldingNull_AndKeepsAWholeOne()
        {
            Api.Prepared dropped = Api.Prepare(new IosShareContentPayload
            {
                items = new[] { IosShareItem.Text("a") },
                excludedActivityTypes = new[] { null!, "com.apple.UIKit.activity.CopyToPasteboard" },
            });
            Assert.IsNull(dropped.ExcludedActivityTypes);
            Assert.IsTrue(dropped.DroppedExcludedList);
            Assert.AreEqual("Share: excludedActivityTypes holds null, so the whole list is ignored, as 1.x did.", Api.Warnings(dropped).Single());

            Api.Prepared kept = Api.Prepare(new IosShareContentPayload
            {
                items = new[] { IosShareItem.Text("a") },
                excludedActivityTypes = new[] { "com.apple.UIKit.activity.CopyToPasteboard" },
            });
            CollectionAssert.AreEqual(new[] { "com.apple.UIKit.activity.CopyToPasteboard" }, kept.ExcludedActivityTypes);
            CollectionAssert.IsEmpty(Api.Warnings(kept));
        }

        [TestCase(null, null)]
        [TestCase("", null)]
        [TestCase("  ", null)]
        [TestCase("Hi", "Hi")]
        public void Prepare_LeavesOutABlankSubjectAndPreviewTitle(string? given, string? passed)
        {
            Api.Prepared p = Api.Prepare(new IosShareContentPayload { items = new[] { IosShareItem.Text("a") }, subject = given, previewTitle = given });
            Assert.AreEqual(passed, p.Subject);
            Assert.AreEqual(passed, p.PreviewTitle);
        }

        [Test]
        public void Prepare_WarnsOfU0000()
        {
            Api.Prepared p = Api.Prepare(new IosShareContentPayload { items = new[] { IosShareItem.Text("secret\0B") } });
            Assert.IsTrue(p.HasNul);
            string warning = Api.Warnings(p).Single();
            StringAssert.Contains("U+0000", warning);
            StringAssert.DoesNotContain("secret", warning, "a warning must not show the value");
            Assert.IsFalse(Api.Prepare(new IosShareContentPayload { items = new[] { IosShareItem.Text("AB") } }).HasNul);
        }

        // ── The request (D-10) ──────────────────────────────────────────────

        private static string? Utf8(IntPtr p) => p == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(p);

        [Test]
        public void BuildRequest_LaysOutTheItemsAndOptions()
        {
            Api.Prepared p = Api.Prepare(new IosShareContentPayload
            {
                items = new[] { IosShareItem.Text("日本"), IosShareItem.Url("https://example.com") },
                subject = "S",
                excludedActivityTypes = new[] { "x.y" },
            });
            Api.ShareRequest r = Api.BuildRequest(p, _allocated);
            Assert.AreEqual(56u, r.StructSize);
            Assert.AreEqual(2UL, r.ItemCount.ToUInt64());
            for (int i = 0; i < 2; i++)
            {
                var item = Marshal.PtrToStructure<Api.ShareItem>(IntPtr.Add(r.Items, i * 24));
                Assert.AreEqual(24u, item.StructSize);
                Assert.AreEqual((0u, 0u), (item.Reserved0, item.Reserved1));
                Assert.AreEqual(new[] { (0, "日本"), (1, "https://example.com") }[i], (item.Kind, Utf8(item.Value)));
            }
            Assert.AreEqual("S", Utf8(r.Subject));
            Assert.AreEqual(IntPtr.Zero, r.PreviewTitle);
            Assert.AreEqual(1UL, r.ExcludedActivityTypeCount.ToUInt64());
            Assert.AreEqual("x.y", Utf8(Marshal.ReadIntPtr(r.ExcludedActivityTypes)));
        }

        [Test]
        public void BuildRequest_WithoutExclusions_PassesNullAndZero()
        {
            Api.ShareRequest r = Api.BuildRequest(Api.Prepare(new IosShareContentPayload { items = new[] { IosShareItem.Text("a") } }), _allocated);
            Assert.AreEqual(IntPtr.Zero, r.ExcludedActivityTypes);
            Assert.AreEqual(0UL, r.ExcludedActivityTypeCount.ToUInt64());
        }

        // ── Messages (D-6, D-8) ──────────────────────────────────────────────

        [TestCase(17, "Invalid URL.")]
        [TestCase(18, "Failed to load image.")]
        [TestCase(9, "File not found.")]
        [TestCase(6, "No root view controller available to present the share sheet.")]
        [TestCase(19, "Failed to present the share sheet.")]
        [TestCase(21, "Failed to present the share sheet: the share activity failed.")]
        [TestCase(20, "The share sheet was dismissed without a result.")]
        [TestCase(4, "Failed to share: UNKNOWN.")]
        [TestCase(99, "Failed to share: 99.")]
        public void CompletionFailureMessage(int error, string message) => Assert.AreEqual(message, Api.CompletionFailureMessage(error));

        [Test]
        public void ToResult()
        {
            IosShareResult ok = Api.ToResult(Api.ErrorNone, true, "com.apple.UIKit.activity.CopyToPasteboard");
            Assert.AreEqual((true, true, "com.apple.UIKit.activity.CopyToPasteboard", (string?)null), (ok.IsSuccess, ok.Completed, ok.ActivityType, ok.ErrorMessage));
            IosShareResult fail = Api.ToResult(Api.ErrorInvalidUri, true, "ignored");
            Assert.AreEqual((false, false, (string?)null, "Invalid URL."), (fail.IsSuccess, fail.Completed, fail.ActivityType, fail.ErrorMessage));
            Assert.AreEqual("Failed to share: the request was rejected (INVALID_PARAMETER).", Api.EntryFailureMessage(Api.ErrorInvalidParameter));
        }

        // ── The per-call table (D-1) ──────────────────────────────────────────

        [Test]
        public void PendingCalls_EachCallHasItsOwnNumber()
        {
            var table = new IosSharePendingCalls();
            Action<IosShareResult> a = _ => { }, b = _ => { };
            long first = table.Add(a), second = table.Add(b);
            Assert.AreEqual((1L, 2L), (first, second));
            Assert.IsTrue(table.TryTake(second, out var gotB));
            Assert.AreSame(b, gotB);
            Assert.IsTrue(table.TryTake(first, out var gotA));
            Assert.AreSame(a, gotA);
            Assert.IsFalse(table.TryTake(first, out var none), "taken twice");
            Assert.IsNull(none);
            Assert.AreEqual(0, table.Count);
        }

        [Test]
        public void PendingCalls_ClearForgetsEveryCall_AndNumbersKeepGrowing()
        {
            var table = new IosSharePendingCalls();
            long first = table.Add(null);
            table.Clear();
            Assert.IsFalse(table.TryTake(first, out _));
            Assert.AreEqual(2L, table.Add(null), "a number is never reused");
        }
    }
}
