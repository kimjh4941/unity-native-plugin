#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using NUnit.Framework;
using JonghyunKim.NativeToolkit.Runtime.iOS.Common;
using Api = JonghyunKim.NativeToolkit.Runtime.iOS.Dialog.IosDialogCApi;
using Kind = JonghyunKim.NativeToolkit.Runtime.iOS.Dialog.IosDialogCApi.Kind;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// Layer 1 tests for <c>IosDialogCApi</c>: the request structs against <c>Dialog.h</c>'s layout
    /// (<c>#pragma pack 8</c>), the request building, the manager's checks, and the conversion of a
    /// completion into the event arguments (artifact/ios/dialog, C ABI design v2, 4 and V-1).
    /// </summary>
    public sealed class IosDialogCApiTests
    {
        private readonly List<IntPtr> _allocated = new();

        [TearDown]
        public void FreeAllocated() => IosNativeToolkitCApi.FreeAll(_allocated);

        // ── Layout (Dialog.h; every field, so a swapped pair of the same size is caught) ──────────

        private static void AssertLayout<T>(int size, params (string field, int offset)[] fields) where T : struct
        {
            Assert.AreEqual(size, Marshal.SizeOf<T>(), $"sizeof {typeof(T).Name}");
            Assert.AreEqual(fields.Length, typeof(T).GetFields().Length, $"{typeof(T).Name} field count");
            foreach (var (field, offset) in fields)
                Assert.AreEqual(offset, Marshal.OffsetOf<T>(field).ToInt32(), $"{typeof(T).Name}.{field}");
        }

        [Test]
        public void AlertRequest_Layout() => AssertLayout<Api.AlertRequest>(32,
            ("StructSize", 0), ("Reserved0", 4), ("Title", 8), ("Message", 16), ("ButtonText", 24));

        [Test]
        public void ConfirmRequest_Layout() => AssertLayout<Api.ConfirmRequest>(40,
            ("StructSize", 0), ("Reserved0", 4), ("Title", 8), ("Message", 16), ("ConfirmText", 24), ("CancelText", 32));

        [Test]
        public void DestructiveRequest_Layout() => AssertLayout<Api.DestructiveRequest>(40,
            ("StructSize", 0), ("Reserved0", 4), ("Title", 8), ("Message", 16), ("DestructiveText", 24), ("CancelText", 32));

        [Test]
        public void ActionSheetRequest_Layout() => AssertLayout<Api.ActionSheetRequest>(48,
            ("StructSize", 0), ("Reserved0", 4), ("Title", 8), ("Message", 16), ("Options", 24), ("OptionCount", 32), ("CancelText", 40));

        [Test]
        public void TextInputRequest_Layout() => AssertLayout<Api.TextInputRequest>(56,
            ("StructSize", 0), ("Reserved0", 4), ("Title", 8), ("Message", 16), ("Placeholder", 24), ("ConfirmText", 32),
            ("CancelText", 40), ("ConfirmDisabledWhenEmpty", 48), ("Reserved1", 52));

        [Test]
        public void LoginRequest_Layout() => AssertLayout<Api.LoginRequest>(64,
            ("StructSize", 0), ("Reserved0", 4), ("Title", 8), ("Message", 16), ("UsernamePlaceholder", 24),
            ("PasswordPlaceholder", 32), ("LoginText", 40), ("CancelText", 48), ("LoginDisabledWhenEmpty", 56), ("Reserved1", 60));

        [Test]
        public void OptionCount_IsPointerSized()
        {
            // size_t: an int here would keep the size 48 and shift nothing, but read garbage high bits.
            Assert.AreEqual(typeof(UIntPtr), typeof(Api.ActionSheetRequest).GetField("OptionCount")!.FieldType);
        }

        // ── Building the requests ──────────────────────────────────────────────

        private static string? Utf8(IntPtr p) => p == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(p);

        [Test]
        public void BuildAlert_FillsEveryText()
        {
            Api.AlertRequest r = Api.BuildAlert("題", "本文", "了解", _allocated);
            Assert.AreEqual(32u, r.StructSize);
            Assert.AreEqual(0u, r.Reserved0);
            Assert.AreEqual("題", Utf8(r.Title));
            Assert.AreEqual("本文", Utf8(r.Message));
            Assert.AreEqual("了解", Utf8(r.ButtonText));
        }

        [Test]
        public void BuildConfirmAndDestructive_KeepTheButtonsApart()
        {
            Api.ConfirmRequest c = Api.BuildConfirm("t", "m", "Yes", "No", _allocated);
            Assert.AreEqual(("Yes", "No"), (Utf8(c.ConfirmText), Utf8(c.CancelText)));
            Api.DestructiveRequest d = Api.BuildDestructive("t", "m", "Erase", "Keep", _allocated);
            Assert.AreEqual(("Erase", "Keep"), (Utf8(d.DestructiveText), Utf8(d.CancelText)));
        }

        [Test]
        public void BuildActionSheet_PassesTheOptionsInOrder()
        {
            Api.ActionSheetRequest r = Api.BuildActionSheet("t", "m", new[] { "A", "B", "日本" }, "Cancel", _allocated);
            Assert.AreEqual(48u, r.StructSize);
            Assert.AreEqual(3UL, r.OptionCount.ToUInt64());
            for (int i = 0; i < 3; i++)
                Assert.AreEqual(new[] { "A", "B", "日本" }[i], Utf8(Marshal.ReadIntPtr(r.Options, i * IntPtr.Size)));
            Assert.AreEqual("Cancel", Utf8(r.CancelText));
        }

        [TestCase(false, 1)]
        [TestCase(true, 0)]
        public void BuildTextInput_InvertsTheEmptyFlag(bool enableConfirmWhenEmpty, int disabledWhenEmpty)
        {
            Api.TextInputRequest r = Api.BuildTextInput("t", "m", "", "OK", "Cancel", enableConfirmWhenEmpty, _allocated);
            Assert.AreEqual(disabledWhenEmpty, r.ConfirmDisabledWhenEmpty);
            Assert.AreEqual(0u, r.Reserved1);
            Assert.AreEqual("", Utf8(r.Placeholder), "1.x's default placeholder is empty, not NULL");
        }

        [TestCase(false, 1)]
        [TestCase(true, 0)]
        public void BuildLogin_InvertsTheEmptyFlag(bool enableLoginWhenEmpty, int disabledWhenEmpty)
        {
            Api.LoginRequest r = Api.BuildLogin("t", "m", "Username", "Password", "Login", "Cancel", enableLoginWhenEmpty, _allocated);
            Assert.AreEqual(disabledWhenEmpty, r.LoginDisabledWhenEmpty);
            Assert.AreEqual(("Username", "Password"), (Utf8(r.UsernamePlaceholder), Utf8(r.PasswordPlaceholder)));
            Assert.AreEqual(("Login", "Cancel"), (Utf8(r.LoginText), Utf8(r.CancelText)));
        }

        // ── The manager's checks (1.x's order and messages, and D-4) ──────────────

        [TestCase("", "m", "Title cannot be null or empty.")]
        [TestCase(null, "m", "Title cannot be null or empty.")]
        [TestCase("", "", "Title cannot be null or empty.")]
        [TestCase("t", "", "Message cannot be null or empty.")]
        [TestCase("t", null, "Message cannot be null or empty.")]
        [TestCase("t", "m", null)]
        public void CheckTitleAndMessage(string? title, string? message, string? expected)
        {
            Assert.AreEqual(expected, Api.CheckTitleAndMessage(title, message));
        }

        [Test]
        public void CheckOptions()
        {
            Assert.AreEqual("Options cannot be null or empty.", Api.CheckOptions(null));
            Assert.AreEqual("Options cannot be null or empty.", Api.CheckOptions(new string[0]));
            Assert.AreEqual("Options cannot contain null.", Api.CheckOptions(new[] { "A", null! }));
            Assert.IsNull(Api.CheckOptions(new[] { "A", "" }));
        }

        // ── Completions into event arguments (D-8, D-9) ──────────────────────────

        [Test]
        public void ButtonArgs_SuccessCarriesTheLabelAndNoError()
        {
            Assert.AreEqual(("B", true, (string?)null), Api.ToButtonArgs(new Api.Answer(Kind.ActionSheet, Api.ErrorNone, "B")));
        }

        [Test]
        public void TextInputArgs_CancelHasNoText_EmptyConfirmHasEmptyText()
        {
            Assert.AreEqual(("Cancel", (string?)null, true, (string?)null), Api.ToTextInputArgs(new Api.Answer(Kind.TextInput, Api.ErrorNone, "Cancel")));
            Assert.AreEqual(("OK", "", true, (string?)null), Api.ToTextInputArgs(new Api.Answer(Kind.TextInput, Api.ErrorNone, "OK", text: "")));
        }

        [Test]
        public void LoginArgs_CarryBothValues()
        {
            Assert.AreEqual(("Login", "u", "p", true, (string?)null),
                Api.ToLoginArgs(new Api.Answer(Kind.Login, Api.ErrorNone, "Login", username: "u", password: "p")));
        }

        [TestCase(8, "Failed to showDialog: the app is not in the foreground.")]
        [TestCase(10, "Failed to showDialog: the dialog could not be shown.")]
        [TestCase(7, "Failed to showDialog: the dialog was closed by the system.")]
        [TestCase(6, "Failed to showDialog: the dialog was canceled.")]
        [TestCase(4, "Failed to showDialog: UNKNOWN.")]
        [TestCase(99, "Failed to showDialog: 99.")]
        public void ButtonArgs_FailureLeavesValuesNull(int error, string message)
        {
            Assert.AreEqual(((string?)null, false, message), Api.ToButtonArgs(new Api.Answer(Kind.Alert, error, "ignored")));
        }

        [Test]
        public void FailureArgs_OfTextAndLogin_LeaveEveryValueNull()
        {
            Assert.AreEqual(((string?)null, (string?)null, false, "Failed to showTextInputDialog: the app is not in the foreground."),
                Api.ToTextInputArgs(new Api.Answer(Kind.TextInput, Api.ErrorNotForeground, "OK", text: "x")));
            Assert.AreEqual(((string?)null, (string?)null, (string?)null, false, "Failed to showLoginDialog: the dialog could not be shown."),
                Api.ToLoginArgs(new Api.Answer(Kind.Login, Api.ErrorShowFailed, "Login", username: "u", password: "p")));
        }

        [Test]
        public void EntryMessages_NameThe1xFunction()
        {
            Assert.AreEqual("Failed to showActionSheet: the native library is not available.", Api.NativeUnavailableMessage(Kind.ActionSheet));
            Assert.AreEqual("Failed to showConfirmDialog: the request was rejected (INVALID_PARAMETER).", Api.EntryFailureMessage(Kind.Confirm, Api.ErrorInvalidParameter));
            CollectionAssert.AreEqual(
                new[] { "showDialog", "showConfirmDialog", "showDestructiveDialog", "showActionSheet", "showTextInputDialog", "showLoginDialog" },
                new[] { Kind.Alert, Kind.Confirm, Kind.Destructive, Kind.ActionSheet, Kind.TextInput, Kind.Login }.Select(Api.MethodName));
        }
    }

    /// <summary>The UTF-8 helpers of <see cref="IosNativeToolkitCApi"/> (C ABI design v2, D-11).</summary>
    public sealed class IosNativeToolkitCApiStringTests
    {
        [TestCase("")]
        [TestCase("ascii")]
        [TestCase("日本語と絵文字 😀")]
        public void AllocAndRead_RoundTrip(string value)
        {
            var allocated = new List<IntPtr>();
            try
            {
                IntPtr p = IosNativeToolkitCApi.AllocUtf8(value, allocated);
                int size = System.Text.Encoding.UTF8.GetByteCount(value);
                Assert.AreEqual(0, Marshal.ReadByte(p, size), "NUL-terminated");
                Assert.AreEqual(value, IosNativeToolkitCApi.ReadUtf8OrNull(p, new UIntPtr((uint)size)));
            }
            finally
            {
                IosNativeToolkitCApi.FreeAll(allocated);
            }
            Assert.AreEqual(0, allocated.Count);
        }

        [Test]
        public void Null_IsZeroAndReadsAsNull()
        {
            var allocated = new List<IntPtr>();
            Assert.AreEqual(IntPtr.Zero, IosNativeToolkitCApi.AllocUtf8(null, allocated));
            Assert.AreEqual(0, allocated.Count);
            Assert.IsNull(IosNativeToolkitCApi.ReadUtf8OrNull(IntPtr.Zero, UIntPtr.Zero));
        }

        [Test]
        public void EmptyArray_StillGetsASlot()
        {
            var allocated = new List<IntPtr>();
            try
            {
                Assert.AreNotEqual(IntPtr.Zero, IosNativeToolkitCApi.AllocStringArray(new string[0], allocated));
            }
            finally
            {
                IosNativeToolkitCApi.FreeAll(allocated);
            }
        }
    }
}
