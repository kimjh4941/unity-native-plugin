#nullable enable

#if UNITY_ANDROID || UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using NUnit.Framework;
using JonghyunKim.NativeToolkit.Runtime.Android.Clipboard;
using JonghyunKim.NativeToolkit.Runtime.Android.Common;
using Api = JonghyunKim.NativeToolkit.Runtime.Android.Clipboard.AndroidClipboardCApi;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// Layer 1 tests for <see cref="AndroidClipboardCApi"/>: the options struct against the C ABI's
    /// layout, the arguments as passed, the failure messages and codes as 1.x worded them, and the
    /// read results normalized as 1.x's parser did
    /// (artifact/android/clipboard/designs/2026-10-12-android-clipboard-design-v4.md, 7.1).
    /// </summary>
    public sealed class AndroidClipboardCApiTests
    {
        private static readonly string[] AsyncOperations =
        {
            AndroidClipboardManager.OperationCopyPlainText, AndroidClipboardManager.OperationCopyHtmlText,
            AndroidClipboardManager.OperationCopyUri, AndroidClipboardManager.OperationCopyMultipleText,
            AndroidClipboardManager.OperationClear, AndroidClipboardManager.OperationStopObserving,
        };

        private readonly List<IntPtr> _allocated = new();

        [TearDown]
        public void FreeAllocated() => AndroidNativeToolkitCApi.FreeAll(_allocated);

        private static string? Utf8At(IntPtr pointer)
        {
            if (pointer == IntPtr.Zero) return null;
            var bytes = new List<byte>();
            for (int i = 0; ; i++)
            {
                byte b = Marshal.ReadByte(pointer, i);
                if (b == 0) break;
                bytes.Add(b);
            }
            return Encoding.UTF8.GetString(bytes.ToArray());
        }

        // ── Layout (native-toolkit android_library_capi_test StructLayoutTest.cpp:18-23) ──────────

        [Test]
        public void CopyOptions_Layout()
        {
            Assert.AreEqual(24, Marshal.SizeOf<Api.CopyOptions>());
            Assert.AreEqual(0, Marshal.OffsetOf<Api.CopyOptions>("StructSize").ToInt32());
            Assert.AreEqual(4, Marshal.OffsetOf<Api.CopyOptions>("Reserved0").ToInt32());
            Assert.AreEqual(8, Marshal.OffsetOf<Api.CopyOptions>("Label").ToInt32());
            Assert.AreEqual(16, Marshal.OffsetOf<Api.CopyOptions>("Sensitive").ToInt32());
            Assert.AreEqual(20, Marshal.OffsetOf<Api.CopyOptions>("Reserved1").ToInt32());
        }

        [Test]
        public void ChangeCallback_TakesUserDataOnly()
        {
            MethodInfo invoke = typeof(Api.ChangeCallback).GetMethod("Invoke")!;
            CollectionAssert.AreEqual(new[] { typeof(IntPtr) }, invoke.GetParameters().Select(p => p.ParameterType).ToArray());
            Assert.AreEqual(typeof(void), invoke.ReturnType);
        }

        // ── Arguments (4.2) ───────────────────────────────────────────────────

        [Test]
        public void BuildOptions_FillsTheHeaderAndTheFlag()
        {
            Api.CopyOptions options = Api.BuildOptions("NTK label", sensitive: true, _allocated);
            Assert.AreEqual(24u, options.StructSize);
            Assert.AreEqual(0u, options.Reserved0);
            Assert.AreEqual(0u, options.Reserved1);
            Assert.AreEqual(1, options.Sensitive);
            Assert.AreEqual("NTK label", Utf8At(options.Label));

            Assert.AreEqual(0, Api.BuildOptions("x", sensitive: false, _allocated).Sensitive);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("\u3000")]
        public void BuildOptions_ABlankLabelIsNoLabel(string? label)
        {
            Assert.AreEqual(IntPtr.Zero, Api.BuildOptions(label, sensitive: false, _allocated).Label);
        }

        [Test]
        public void AsPassed_NullIsEmpty_OthersUnchanged()
        {
            Assert.AreEqual("", Api.AsPassed((string?)null));
            Assert.AreEqual("", Api.AsPassed(""));
            Assert.AreEqual(" a ", Api.AsPassed(" a "));
        }

        [Test]
        public void AsPassed_List_KeepsTheOrderAndMakesNullsEmpty()
        {
            string[] passed = Api.AsPassed(new[] { "a", null!, "", "c" });
            CollectionAssert.AreEqual(new[] { "a", "", "", "c" }, passed);
            CollectionAssert.IsEmpty(Api.AsPassed(Array.Empty<string>()));
        }

        // ── Errors (4.4) ──────────────────────────────────────────────────────

        [Test]
        public void ErrorName_NamesEveryValue()
        {
            CollectionAssert.AreEqual(
                new[] { "NONE", "INVALID_PARAMETER", "NOT_INITIALIZED", "NOT_SUPPORTED", "UNKNOWN", "OUT_OF_MEMORY",
                        "EMPTY_CONTENT", "EMPTY_ITEMS", "INVALID_URI", "UNAVAILABLE", "READ_NOT_ALLOWED", "SECURITY" },
                Enumerable.Range(0, 12).Select(Api.ErrorName).ToArray());
            Assert.AreEqual("ERROR_42", Api.ErrorName(42));
            Assert.AreEqual("ERROR_-1", Api.ErrorName(-1));
        }

        [Test]
        public void IsRequestDefect_OnlyInvalidParameterAndNotSupported()
        {
            int[] defects = Enumerable.Range(0, 13).Where(Api.IsRequestDefect).ToArray();
            CollectionAssert.AreEqual(new[] { Api.ErrorInvalidParameter, Api.ErrorNotSupported }, defects);
        }

        [Test]
        public void OperationFailureMessage_EveryValue_EveryOperation()
        {
            foreach (string op in AsyncOperations)
            {
                Assert.AreEqual($"Failed: INVALID_PARAMETER", Api.OperationFailureMessage(op, 1), op);
                Assert.AreEqual($"{op} could not be started.", Api.OperationFailureMessage(op, 2), op);
                Assert.AreEqual("Failed: NOT_SUPPORTED", Api.OperationFailureMessage(op, 3), op);
                Assert.AreEqual("Failed: UNKNOWN", Api.OperationFailureMessage(op, 4), op);
                Assert.AreEqual("Failed: OUT_OF_MEMORY", Api.OperationFailureMessage(op, 5), op);
                Assert.AreEqual("Clipboard content is empty. Please provide text or HTML.", Api.OperationFailureMessage(op, 6), op);
                Assert.AreEqual("No items provided for clipboard copy.", Api.OperationFailureMessage(op, 7), op);
                Assert.AreEqual("Invalid URI: ", Api.OperationFailureMessage(op, 8), op);
                Assert.AreEqual("Clipboard service is unavailable.", Api.OperationFailureMessage(op, 9), op);
                Assert.AreEqual("Clipboard read is not allowed. The app must be in the foreground.", Api.OperationFailureMessage(op, 10), op);
                Assert.AreEqual("Security restriction while accessing clipboard.", Api.OperationFailureMessage(op, 11), op);
                Assert.AreEqual("Failed: ERROR_99", Api.OperationFailureMessage(op, 99), op);
            }
        }

        [Test]
        public void OperationFailureMessage_InvalidUri_NamesTheUriAsPassed()
        {
            Assert.AreEqual("Invalid URI: http://example.com",
                Api.OperationFailureMessage(AndroidClipboardManager.OperationCopyUri, Api.ErrorInvalidUri, "http://example.com"));
            Assert.AreEqual("Invalid URI: ",
                Api.OperationFailureMessage(AndroidClipboardManager.OperationCopyUri, Api.ErrorInvalidUri, null));
        }

        [TestCase("read")]
        [TestCase("getDescription")]
        public void ReadFailure_EveryValue(string op)
        {
            var expected = new (string Code, string Message)[]
            {
                ("CLIPBOARD_UNKNOWN", "Failed: NONE"),
                ("CLIPBOARD_UNKNOWN", "Failed: INVALID_PARAMETER"),
                ("CLIPBOARD_BRIDGE_UNAVAILABLE", $"{op} could not be started."),
                ("CLIPBOARD_UNKNOWN", "Failed: NOT_SUPPORTED"),
                ("CLIPBOARD_UNKNOWN", "Failed: UNKNOWN"),
                ("CLIPBOARD_UNKNOWN", "Failed: OUT_OF_MEMORY"),
                ("CLIPBOARD_UNKNOWN", "Failed: EMPTY_CONTENT"),
                ("CLIPBOARD_UNKNOWN", "Failed: EMPTY_ITEMS"),
                ("CLIPBOARD_UNKNOWN", "Failed: INVALID_URI"),
                ("CLIPBOARD_UNAVAILABLE", "Clipboard service is unavailable."),
                ("CLIPBOARD_READ_NOT_ALLOWED", "Clipboard read is not allowed. The app must be in the foreground."),
                ("CLIPBOARD_SECURITY", "Security restriction while accessing clipboard."),
            };
            for (int error = 0; error < expected.Length; error++)
                Assert.AreEqual(expected[error], Api.ReadFailure(op, error), $"error {error}");
            Assert.AreEqual(("CLIPBOARD_UNKNOWN", "Failed: ERROR_99"), Api.ReadFailure(op, 99));
        }

        // ── Read results (4.3, J-C4) ──────────────────────────────────────────

        [Test]
        public void ToClipContents_EmptyAndMissingValuesAreNull()
        {
            ClipContents contents = Api.ToClipContents("", new[] { "text/plain" }, new[]
            {
                new Api.RawItem("", null, "", null),
                new Api.RawItem(null, "", null, ""),
            });

            Assert.IsNull(contents.Label);
            foreach (ClipItem item in contents.Items)
            {
                Assert.IsNull(item.Text);
                Assert.IsNull(item.HtmlText);
                Assert.IsNull(item.Uri);
                Assert.IsNull(item.CoercedText);
            }
        }

        [Test]
        public void ToClipContents_KeepsValuesAndOrder()
        {
            ClipContents contents = Api.ToClipContents("NTK label", new[] { "text/plain", "text/html" }, new[]
            {
                new Api.RawItem("a", "<b>a</b>", null, "a"),
                new Api.RawItem(null, null, "content://x/1", "content://x/1"),
                new Api.RawItem(" ", null, null, " "),
            });

            Assert.AreEqual("NTK label", contents.Label);
            CollectionAssert.AreEqual(new[] { "text/plain", "text/html" }, contents.MimeTypes);
            CollectionAssert.AreEqual(new[] { "a", null, " " }, contents.Items.Select(i => i.Text).ToArray());
            Assert.AreEqual("<b>a</b>", contents.Items[0].HtmlText);
            Assert.AreEqual("content://x/1", contents.Items[1].Uri);
            Assert.AreEqual("content://x/1", contents.Items[1].CoercedText);
        }

        [Test]
        public void ToClipContents_NoItems()
        {
            ClipContents contents = Api.ToClipContents(null, Array.Empty<string>(), Array.Empty<Api.RawItem>());
            CollectionAssert.IsEmpty(contents.Items);
            CollectionAssert.IsEmpty(contents.MimeTypes);
        }

        [TestCase(-1, null)]
        [TestCase(0, null)]
        [TestCase(1, 1)]
        [TestCase(2, 2)]
        [TestCase(3, 3)]
        public void ToDescriptionInfo_Classification(int classification, int? expected)
        {
            Assert.AreEqual(expected, Api.ToDescriptionInfo("L", new[] { "text/plain" }, 0, classification).ClassificationStatus);
        }

        [Test]
        public void ToDescriptionInfo_LabelAndStyled()
        {
            ClipDescriptionInfo info = Api.ToDescriptionInfo("", new[] { "text/html" }, 1, -1);
            Assert.IsNull(info.Label);
            Assert.IsTrue(info.IsStyledText);
            CollectionAssert.AreEqual(new[] { "text/html" }, info.MimeTypes);
            Assert.IsFalse(Api.ToDescriptionInfo("NTK", Array.Empty<string>(), 0, -1).IsStyledText);
            Assert.AreEqual("NTK", Api.ToDescriptionInfo("NTK", Array.Empty<string>(), 0, -1).Label);
        }
    }
}
#endif
