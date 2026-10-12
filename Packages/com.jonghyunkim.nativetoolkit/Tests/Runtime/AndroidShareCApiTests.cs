#nullable enable

#if UNITY_ANDROID || UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using NUnit.Framework;
using JonghyunKim.NativeToolkit.Runtime.Android.Common;
using JonghyunKim.NativeToolkit.Runtime.Android.Share;
using Api = JonghyunKim.NativeToolkit.Runtime.Android.Share.AndroidShareCApi;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// Layer 1 tests for <see cref="AndroidShareCApi"/>: the structs against the C ABI's layout, the
    /// struct building, the checks with 1.x's messages, the chooser action rules, and the failure
    /// messages (artifact/android/share/designs/2026-10-11-android-share-design-v6.md, 7.1).
    /// </summary>
    public sealed class AndroidShareCApiTests
    {
        private static readonly byte[] Png = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00 };
        private static readonly string PngBase64 = Convert.ToBase64String(Png);
        private readonly List<IntPtr> _allocated = new();

        [TearDown]
        public void FreeAllocated() => AndroidNativeToolkitCApi.FreeAll(_allocated);

        // ── Layout (native-toolkit android_library_capi_test StructLayoutTest.cpp:118-141) ─────────

        private static void AssertLayout<T>(int size, params (string field, int offset)[] fields) where T : struct
        {
            Assert.AreEqual(size, Marshal.SizeOf<T>(), $"sizeof {typeof(T).Name}");
            foreach (var (field, offset) in fields)
                Assert.AreEqual(offset, Marshal.OffsetOf<T>(field).ToInt32(), $"{typeof(T).Name}.{field}");
        }

        [Test]
        public void TextContent_Layout() => AssertLayout<Api.TextContent>(56,
            ("StructSize", 0), ("Reserved0", 4), ("Text", 8), ("Title", 16), ("Subject", 24),
            ("MimeType", 32), ("PreviewTitle", 40), ("PreviewThumbnailPath", 48));

        [Test]
        public void ChooserAction_Layout() => AssertLayout<Api.ChooserAction>(32,
            ("Id", 0), ("Label", 8), ("Icon", 16), ("IconSize", 24));

        [Test]
        public void DirectTarget_Layout() => AssertLayout<Api.DirectTarget>(48,
            ("StructSize", 0), ("Reserved0", 4), ("Id", 8), ("Label", 16), ("Category", 24),
            ("Icon", 32), ("IconSize", 40));

        [Test]
        public void SelectionCallback_RequestIdIs64Bit()
        {
            ParameterInfo[] parameters = typeof(Api.SelectionCallback).GetMethod("Invoke")!.GetParameters();
            Assert.AreEqual(typeof(ulong), parameters[1].ParameterType);
        }

        // ── Building ─────────────────────────────────────────────────────────

        private static string? Read(IntPtr pointer) =>
            pointer == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(pointer);

        [Test]
        public void BuildTextContent_PassesValuesAndLeavesAbsentOnesNull()
        {
            Api.TextContent content = Api.BuildTextContent(new ShareTextPayload
            {
                text = "本文",
                title = "T",
                subject = "  ",
                mimeType = null,
                previewTitle = "\u3000",
                previewThumbnailPath = "/p.png",
            }, _allocated);
            Assert.AreEqual(56u, content.StructSize);
            Assert.AreEqual(0u, content.Reserved0);
            Assert.AreEqual("本文", Read(content.Text));
            Assert.AreEqual("T", Read(content.Title));
            Assert.IsNull(Read(content.Subject), "blank subject");
            Assert.IsNull(Read(content.MimeType), "null MIME type");
            Assert.IsNull(Read(content.PreviewTitle), "ideographic space only");
            Assert.AreEqual("/p.png", Read(content.PreviewThumbnailPath));
        }

        [Test]
        public void BuildActions_WritesEachActionInOrder()
        {
            var actions = new List<Api.NormalizedAction>
            {
                new("ntk.a", "A", new byte[] { 1, 2, 3 }),
                new("ntk.b", "B", new byte[] { 4 }),
            };
            IntPtr block = Api.BuildActions(actions, _allocated);
            int size = Marshal.SizeOf<Api.ChooserAction>();
            for (int i = 0; i < actions.Count; i++)
            {
                var action = Marshal.PtrToStructure<Api.ChooserAction>(block + i * size);
                Assert.AreEqual(actions[i].Id, Read(action.Id));
                Assert.AreEqual(actions[i].Label, Read(action.Label));
                Assert.AreEqual((ulong)actions[i].Icon.Length, action.IconSize.ToUInt64());
                var icon = new byte[actions[i].Icon.Length];
                Marshal.Copy(action.Icon, icon, 0, icon.Length);
                Assert.AreEqual(actions[i].Icon, icon);
            }
        }

        [Test]
        public void BuildActions_None_IsNotNull() =>
            Assert.AreNotEqual(IntPtr.Zero, Api.BuildActions(new List<Api.NormalizedAction>(), _allocated));

        [Test]
        public void BuildDirectTarget_LeavesABlankCategoryNull()
        {
            var icon = new byte[] { 9, 8 };
            Api.DirectTarget target = Api.BuildDirectTarget(
                new DirectShareTargetPayload { id = "id1", label = "L", iconBase64 = "x", category = " " }, icon, _allocated);
            Assert.AreEqual(48u, target.StructSize);
            Assert.AreEqual(0u, target.Reserved0);
            Assert.AreEqual("id1", Read(target.Id));
            Assert.AreEqual("L", Read(target.Label));
            Assert.IsNull(Read(target.Category));
            Assert.AreEqual(2ul, target.IconSize.ToUInt64());
        }

        [Test]
        public void AllocBytes_ReadsBack()
        {
            var bytes = new byte[] { 0, 255, 7 };
            IntPtr pointer = Api.AllocBytes(bytes, _allocated);
            var back = new byte[3];
            Marshal.Copy(pointer, back, 0, 3);
            Assert.AreEqual(bytes, back);
            Assert.AreNotEqual(IntPtr.Zero, Api.AllocBytes(new byte[0], _allocated), "never NULL");
        }

        // ── Blank and absent (J-S17) ─────────────────────────────────────────

        [TestCase(null, true)]
        [TestCase("", true)]
        [TestCase(" ", true)]
        [TestCase("\u3000", true)]
        [TestCase("\u001C\u001F\t\r\n", true)]
        [TestCase("\u00A0", true)]
        [TestCase("\u2028\u2029", true)]
        [TestCase("\u0085", false)]
        [TestCase(" a ", false)]
        public void IsBlank(string? value, bool expected) => Assert.AreEqual(expected, Api.IsBlank(value));

        [TestCase("\u0085", true)]
        [TestCase(" ", true)]
        [TestCase(null, true)]
        [TestCase("x", false)]
        public void IsAbsent(string? value, bool expected) => Assert.AreEqual(expected, Api.IsAbsent(value));

        // ── Checks (4.2) ─────────────────────────────────────────────────────

        [Test]
        public void Checks_UseThe1xMessages()
        {
            Assert.AreEqual("Failed to shareText: text is required", Api.CheckText("shareText", " "));
            Assert.AreEqual("Failed to shareWithCallback: text is required", Api.CheckText("shareWithCallback", null));
            Assert.IsNull(Api.CheckText("shareText", "a"));
            Assert.AreEqual("Failed to shareImage: filePath is required", Api.CheckPath("shareImage", ""));
            Assert.AreEqual("Failed to shareFile: filePath is required", Api.CheckPath("shareFile", null));
            Assert.AreEqual("Failed to shareFiles: filePaths must not be empty", Api.CheckPaths("shareFiles", new string[0]));
            Assert.AreEqual("Failed to shareImages: filePaths must not contain null", Api.CheckPaths("shareImages", new[] { "a", null! }));
            Assert.IsNull(Api.CheckPaths("shareFiles", new[] { "a" }));
            Assert.AreEqual("Failed to removeDirectShareTargets: ids must not be empty", Api.CheckIds(new string[0]));
            Assert.AreEqual("Failed to removeDirectShareTargets: ids must not contain null", Api.CheckIds(new string[] { null! }));
            Assert.IsNull(Api.CheckIds(new[] { "a" }));
        }

        [Test]
        public void CheckDirectTarget_InThe1xOrder()
        {
            string? Check(string? id, string? label, string? icon) =>
                Api.CheckDirectTarget(new DirectShareTargetPayload { id = id!, label = label!, iconBase64 = icon! }, out _);
            Assert.AreEqual("Failed to registerDirectShareTarget: id is required", Check(" ", "", ""));
            Assert.AreEqual("Failed to registerDirectShareTarget: label is required", Check("i", null, ""));
            Assert.AreEqual("Failed to registerDirectShareTarget: iconBase64 is required", Check("i", "l", " "));
            Assert.AreEqual("Invalid icon data for Direct Share target: i", Check("i", "l", "!!!"));
            Assert.IsNull(Check("i", "l", PngBase64));
        }

        [Test]
        public void DecodeIcon()
        {
            Assert.AreEqual(Png, Api.DecodeIcon(PngBase64));
            Assert.AreEqual(Png, Api.DecodeIcon($"  {PngBase64}\n"), "surrounding white space");
            Assert.IsNull(Api.DecodeIcon("@@@"));
            Assert.IsNull(Api.DecodeIcon(null));
        }

        [TestCase(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, true)]
        [TestCase(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, true)]
        [TestCase(new byte[] { 0x52, 0x49, 0x46, 0x46, 0, 0, 0, 0, 0x57, 0x45, 0x42, 0x50 }, true)]
        [TestCase(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 }, true)]
        [TestCase(new byte[] { 0x42, 0x4D, 0 }, true)]
        [TestCase(new byte[] { 0x00, 0x00, 0x01, 0x00 }, true)]
        [TestCase(new byte[] { 0, 0, 0, 0x18, 0x66, 0x74, 0x79, 0x70 }, true)]
        [TestCase(new byte[] { 0x89, 0x50 }, false)]
        [TestCase(new byte[] { 0x52, 0x49, 0x46, 0x46, 0, 0, 0, 0, 0x57, 0x41, 0x56, 0x45 }, false)]
        [TestCase(new byte[] { 0x61, 0x62, 0x63 }, false)]
        public void LooksLikeImage(byte[] bytes, bool expected) => Assert.AreEqual(expected, Api.LooksLikeImage(bytes));

        // ── Chooser actions (4.2.1) ──────────────────────────────────────────

        private static ChooserActionPayload Action(string? label, string? icon, string? intent) =>
            new() { label = label!, iconBase64 = icon!, intentAction = intent };

        [Test]
        public void NormalizeActions_DropsWhatCannotShow_AndKeepsTheOrder()
        {
            List<Api.NormalizedAction> result = Api.NormalizeActions(new[]
            {
                Action("A", PngBase64, "ntk.a"),
                Action(" ", PngBase64, "ntk.blank"),
                Action("B", "@@@", "ntk.b"),
                Action("C", Convert.ToBase64String(Encoding.ASCII.GetBytes("abc")), "ntk.c"),
                Action("D", PngBase64, null),
                Action("E", null, "ntk.e"),
                Action("F", PngBase64, "ntk.a"),
                Action("S", PngBase64, "android.intent.action.SEND"),
                Action("G", PngBase64, "ntk.g"),
            });
            Assert.AreEqual(new[] { "A|ntk.a", "S|android.intent.action.SEND", "G|ntk.g" },
                result.Select(a => $"{a.Label}|{a.Id}").ToArray());
            Assert.AreEqual(Png, result[0].Icon);
        }

        [Test]
        public void NormalizeActions_ComparesIdsAsPassed()
        {
            List<Api.NormalizedAction> result = Api.NormalizeActions(new[]
            {
                Action("A", PngBase64, "ntk.x\0one"),
                Action("B", PngBase64, "ntk.x\0two"),
                Action("C", PngBase64, "\0hidden"),
                Action("D", PngBase64, "ntk.\uD800"),
            });
            Assert.AreEqual(new[] { "A|ntk.x", "D|ntk.\uFFFD" }, result.Select(a => $"{a.Label}|{a.Id}").ToArray());
        }

        [Test]
        public void NormalizeActions_None() => Assert.IsEmpty(Api.NormalizeActions(null));

        [Test]
        public void IsNotifiedActionId()
        {
            Assert.IsFalse(Api.IsNotifiedActionId("android.intent.action.SEND"));
            Assert.IsTrue(Api.IsNotifiedActionId("ntk.a"));
        }

        // ── The selection requests to cancel (J-S13) ─────────────────────────

        [Test]
        public void SelectionIds_KeepTheNewest16_AndEmptyWhenTaken()
        {
            var ids = new List<ulong>();
            for (ulong id = 1; id <= 18; id++) Api.AddSelectionId(ids, id);
            ulong[] taken = Api.TakeSelectionIds(ids);
            Assert.AreEqual(Enumerable.Range(3, 16).Select(i => (ulong)i).ToArray(), taken);
            Assert.IsEmpty(ids);
        }

        // ── Messages (4.4) ───────────────────────────────────────────────────

        [Test]
        public void ErrorName_CoversEveryValue()
        {
            string[] names =
            {
                "NONE", "INVALID_PARAMETER", "NOT_INITIALIZED", "NOT_SUPPORTED", "UNKNOWN", "OUT_OF_MEMORY",
                "NOT_FOREGROUND", "EMPTY_CONTENT", "NO_SHARE_TARGET", "FILE_NOT_FOUND", "ILLEGAL_FILE_ACCESS",
                "INVALID_MIME_TYPE", "DIRECT_SHARE_REGISTRATION_FAILED", "EMPTY_ID_LIST", "EMPTY_FILE_LIST",
                "INVALID_ICON", "INVALID_CHOOSER_ACTION",
            };
            for (int i = 0; i < names.Length; i++) Assert.AreEqual(names[i], Api.ErrorName(i));
            Assert.AreEqual("99", Api.ErrorName(99));
        }

        [Test]
        public void IsRequestDefect()
        {
            Assert.IsTrue(Api.IsRequestDefect(Api.ErrorInvalidParameter));
            Assert.IsTrue(Api.IsRequestDefect(Api.ErrorNotSupported));
            Assert.IsTrue(Api.IsRequestDefect(Api.ErrorInvalidChooserAction));
            Assert.IsFalse(Api.IsRequestDefect(Api.ErrorNotInitialized));
        }

        private static string Message(int error, bool entry = false, string op = "shareText", string[]? paths = null,
            string? mime = null, string? id = null, Func<string, bool>? exists = null) =>
            Api.FailureMessage(new Api.ShareCall(op, paths, mime, id), error, entry, exists ?? (_ => true));

        [Test]
        public void FailureMessage_EveryValue()
        {
            Assert.AreEqual("Failed to shareFile: the native library is not initialized yet.", Message(Api.ErrorNotInitialized, true, "shareFile"));
            Assert.AreEqual("Failed to shareText: the request was rejected (INVALID_PARAMETER).", Message(Api.ErrorInvalidParameter, true));
            Assert.AreEqual("Failed to shareText: the request was rejected (INVALID_PARAMETER).", Message(Api.ErrorInvalidParameter));
            Assert.AreEqual("Failed to shareText: the request was rejected (NOT_SUPPORTED).", Message(Api.ErrorNotSupported, true));
            Assert.AreEqual("Failed to shareText: the request was rejected (INVALID_CHOOSER_ACTION).", Message(Api.ErrorInvalidChooserAction, true));
            Assert.AreEqual("Failed to shareText: a chooser action icon could not be read as an image.", Message(Api.ErrorInvalidChooserAction));
            Assert.AreEqual("Failed to shareWithCallback: text is required", Message(Api.ErrorEmptyContent, op: "shareWithCallback"));
            Assert.AreEqual("Failed to shareImages: filePaths must not be empty", Message(Api.ErrorEmptyFileList, true, "shareImages"));
            Assert.AreEqual("Failed to removeDirectShareTargets: ids must not be empty", Message(Api.ErrorEmptyIdList, true, "removeDirectShareTargets"));
            Assert.AreEqual("No app available to handle this share request.", Message(Api.ErrorNoShareTarget));
            Assert.AreEqual("File not found: /a", Message(Api.ErrorFileNotFound, op: "shareFile", paths: new[] { "/a" }));
            Assert.AreEqual("File cannot be shared: /a. Ensure the file is in a supported directory.", Message(Api.ErrorIllegalFileAccess, op: "shareImage", paths: new[] { "/a" }));
            Assert.AreEqual("File cannot be shared: /a, /b. Ensure the file is in a supported directory.", Message(Api.ErrorIllegalFileAccess, op: "shareFiles", paths: new[] { "/a", "/b" }));
            Assert.AreEqual("Invalid MIME type: x/", Message(Api.ErrorInvalidMimeType, op: "shareImage", mime: "x/"));
            Assert.AreEqual("Failed to register Direct Share target: the system did not accept the shortcut.", Message(Api.ErrorDirectShareRegistrationFailed, op: "registerDirectShareTarget"));
            Assert.AreEqual("Invalid icon data for Direct Share target: t1", Message(Api.ErrorInvalidIcon, op: "registerDirectShareTarget", id: "t1"));
            Assert.AreEqual("Failed to shareFile: the app is not in the foreground.", Message(Api.ErrorNotForeground, op: "shareFile"));
            Assert.AreEqual("Failed to shareText: OUT_OF_MEMORY.", Message(Api.ErrorOutOfMemory));
            Assert.AreEqual("Failed to shareText: UNKNOWN.", Message(Api.ErrorUnknown));
            Assert.AreEqual("Failed to shareText: 42.", Message(42));
        }

        [Test]
        public void FileNotFound_ForSeveralPaths_NamesTheFirstMissing()
        {
            string[] paths = { "/a", "/b", "/c" };
            Assert.AreEqual("File not found: /b", Message(Api.ErrorFileNotFound, op: "shareFiles", paths: paths, exists: p => p != "/b" && p != "/c"));
            Assert.AreEqual("File not found: /a, /b, /c", Message(Api.ErrorFileNotFound, op: "shareFiles", paths: paths, exists: _ => true));
        }

        [Test]
        public void MissingPath_ReadsTheFileSystem()
        {
            string existing = System.IO.Path.GetTempFileName();
            string directory = System.IO.Path.GetTempPath();
            try
            {
                string missing = existing + ".none";
                Assert.AreEqual(missing, Api.FailureMessage(new Api.ShareCall("shareFiles", new[] { existing, directory, missing }),
                    Api.ErrorFileNotFound, entry: false).Substring("File not found: ".Length), "a directory counts as existing");
            }
            finally
            {
                System.IO.File.Delete(existing);
            }
        }

        [Test]
        public void NativeUnavailableMessage() => Assert.AreEqual(
            "Failed to shareFiles: the native library is not available (libntk.so ships for arm64-v8a and x86_64 only).",
            Api.NativeUnavailableMessage("shareFiles"));
    }
}
#endif
