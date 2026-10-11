#nullable enable

#if UNITY_ANDROID || UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using NUnit.Framework;
using JonghyunKim.NativeToolkit.Runtime.Android.Common;
using JonghyunKim.NativeToolkit.Runtime.Android.Dialog;
using Api = JonghyunKim.NativeToolkit.Runtime.Android.Dialog.AndroidDialogCApi;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// Layer 1 tests for <see cref="AndroidDialogCApi"/>: the request structs against the C ABI's
    /// layout, the request building, the manager's checks, and the conversion of a completion into
    /// the event arguments (artifact/android/dialog/designs/2026-10-11-android-dialog-design-v4.md, 7.1).
    /// </summary>
    public sealed class AndroidDialogCApiTests
    {
        private readonly List<IntPtr> _allocated = new();

        [TearDown]
        public void FreeAllocated() => AndroidNativeToolkitCApi.FreeAll(_allocated);

        // ── Layout (native-toolkit android_library_capi_test StructLayoutTest.cpp:25-94) ──────────

        private static void AssertLayout<T>(int size, params (string field, int offset)[] fields) where T : struct
        {
            Assert.AreEqual(size, Marshal.SizeOf<T>(), $"sizeof {typeof(T).Name}");
            foreach (var (field, offset) in fields)
                Assert.AreEqual(offset, Marshal.OffsetOf<T>(field).ToInt32(), $"{typeof(T).Name}.{field}");
        }

        [Test]
        public void AlertRequest_Layout() => AssertLayout<Api.AlertRequest>(40,
            ("StructSize", 0), ("Reserved0", 4), ("Title", 8), ("Message", 16), ("ButtonText", 24),
            ("NotCancelable", 32), ("NotCancelableOnTouchOutside", 36));

        [Test]
        public void ConfirmRequest_Layout() => AssertLayout<Api.ConfirmRequest>(48,
            ("StructSize", 0), ("Reserved0", 4), ("Title", 8), ("Message", 16), ("NegativeText", 24),
            ("PositiveText", 32), ("NotCancelable", 40), ("NotCancelableOnTouchOutside", 44));

        [Test]
        public void SingleChoiceRequest_Layout() => AssertLayout<Api.SingleChoiceRequest>(64,
            ("StructSize", 0), ("Reserved0", 4), ("Title", 8), ("Items", 16), ("ItemCount", 24),
            ("CheckedIndex", 32), ("NotCancelable", 36), ("NotCancelableOnTouchOutside", 40), ("Reserved1", 44),
            ("NegativeText", 48), ("PositiveText", 56));

        [Test]
        public void MultiChoiceRequest_Layout() => AssertLayout<Api.MultiChoiceRequest>(64,
            ("StructSize", 0), ("Reserved0", 4), ("Title", 8), ("Items", 16), ("ItemCount", 24),
            ("Checked", 32), ("NegativeText", 40), ("PositiveText", 48), ("NotCancelable", 56),
            ("NotCancelableOnTouchOutside", 60));

        [Test]
        public void TextInputRequest_Layout() => AssertLayout<Api.TextInputRequest>(64,
            ("StructSize", 0), ("Reserved0", 4), ("Title", 8), ("Message", 16), ("Hint", 24),
            ("NegativeText", 32), ("PositiveText", 40), ("EnablePositiveWhenEmpty", 48), ("NotCancelable", 52),
            ("NotCancelableOnTouchOutside", 56), ("Reserved1", 60));

        [Test]
        public void LoginRequest_Layout() => AssertLayout<Api.LoginRequest>(72,
            ("StructSize", 0), ("Reserved0", 4), ("Title", 8), ("Message", 16), ("UsernameHint", 24),
            ("PasswordHint", 32), ("NegativeText", 40), ("PositiveText", 48), ("EnablePositiveWhenEmpty", 56),
            ("NotCancelable", 60), ("NotCancelableOnTouchOutside", 64), ("Reserved1", 68));

        /// <summary>
        /// The request ID is uint64_t on Android (uint32_t on Windows). The externs are compiled into
        /// a player only, so the callback's delegate type is what the Editor can check.
        /// </summary>
        [Test]
        public void ResultCallback_TakesA64BitRequestId()
        {
            var parameters = typeof(Api.ResultCallback).GetMethod("Invoke")!.GetParameters();
            Assert.AreEqual(5, parameters.Length);
            Assert.AreEqual(typeof(IntPtr), parameters[0].ParameterType, "user_data");
            Assert.AreEqual(typeof(ulong), parameters[1].ParameterType, "request_id");
            Assert.AreEqual(typeof(int), parameters[2].ParameterType, "error");
            Assert.AreEqual(typeof(uint), parameters[3].ParameterType, "system_code");
            Assert.AreEqual(typeof(IntPtr), parameters[4].ParameterType, "result");
        }

        // ── Building requests ────────────────────────────────────────────────────────────────────

        private static string ReadCString(IntPtr pointer)
        {
            var bytes = new List<byte>();
            for (int i = 0; ; i++)
            {
                byte b = Marshal.ReadByte(pointer, i);
                if (b == 0) break;
                bytes.Add(b);
            }
            return Encoding.UTF8.GetString(bytes.ToArray());
        }

        [Test]
        public void BuildAlert_SetsSizeTextsAndInvertedFlags()
        {
            Api.AlertRequest request = Api.BuildAlert("タイトル", "msg", "OK", cancelableOnTouchOutside: true, cancelable: false, _allocated);

            Assert.AreEqual((uint)Marshal.SizeOf<Api.AlertRequest>(), request.StructSize);
            Assert.AreEqual(0u, request.Reserved0);
            Assert.AreEqual("タイトル", ReadCString(request.Title));
            Assert.AreEqual("msg", ReadCString(request.Message));
            Assert.AreEqual("OK", ReadCString(request.ButtonText));
            Assert.AreEqual(0, request.NotCancelableOnTouchOutside, "cancelableOnTouchOutside true is not_cancelable_on_touch_outside 0");
            Assert.AreEqual(1, request.NotCancelable, "cancelable false is not_cancelable 1");
        }

        [Test]
        public void BuildConfirm_SetsSizeTextsAndInvertedFlags()
        {
            Api.ConfirmRequest request = Api.BuildConfirm("t", "m", "No", "Yes", cancelableOnTouchOutside: false, cancelable: true, _allocated);

            Assert.AreEqual((uint)Marshal.SizeOf<Api.ConfirmRequest>(), request.StructSize);
            Assert.AreEqual(0u, request.Reserved0);
            Assert.AreEqual("t", ReadCString(request.Title));
            Assert.AreEqual("m", ReadCString(request.Message));
            Assert.AreEqual("No", ReadCString(request.NegativeText));
            Assert.AreEqual("Yes", ReadCString(request.PositiveText));
            Assert.AreEqual(0, request.NotCancelable);
            Assert.AreEqual(1, request.NotCancelableOnTouchOutside);
        }

        [Test]
        public void Builders_PutNegativeAndPositiveTextsInTheirFields()
        {
            Api.SingleChoiceRequest single = Api.BuildSingleChoice("t", new[] { "a" }, 0, "n1", "p1", true, true, _allocated);
            Assert.AreEqual(("n1", "p1"), (ReadCString(single.NegativeText), ReadCString(single.PositiveText)));
            Api.MultiChoiceRequest multi = Api.BuildMultiChoice("t", new[] { "a" }, null, "n2", "p2", true, true, _allocated);
            Assert.AreEqual(("n2", "p2"), (ReadCString(multi.NegativeText), ReadCString(multi.PositiveText)));
            Api.TextInputRequest input = Api.BuildTextInput("t", "m", "h", "n3", "p3", false, true, true, _allocated);
            Assert.AreEqual(("h", "n3", "p3"), (ReadCString(input.Hint), ReadCString(input.NegativeText), ReadCString(input.PositiveText)));
            Api.LoginRequest login = Api.BuildLogin("t", "m", "u", "pw", "n4", "p4", false, true, true, _allocated);
            Assert.AreEqual(("u", "pw", "n4", "p4"),
                (ReadCString(login.UsernameHint), ReadCString(login.PasswordHint), ReadCString(login.NegativeText), ReadCString(login.PositiveText)));
        }

        [Test]
        public void BuildSingleChoice_WritesTheItemArray()
        {
            Api.SingleChoiceRequest request = Api.BuildSingleChoice("t", new[] { "a", "𠮷" }, 1, "n", "p", true, true, _allocated);

            Assert.AreEqual(2u, request.ItemCount.ToUInt32());
            Assert.AreEqual("a", ReadCString(Marshal.ReadIntPtr(request.Items, 0)));
            Assert.AreEqual("𠮷", ReadCString(Marshal.ReadIntPtr(request.Items, IntPtr.Size)));
            Assert.AreEqual(1, request.CheckedIndex);
            Assert.AreEqual(0u, request.Reserved1);
            Assert.AreEqual(0, request.NotCancelable);
        }

        [TestCase(null, new[] { 0, 0, 0 })]
        [TestCase(new bool[0], new[] { 0, 0, 0 })]
        [TestCase(new[] { true }, new[] { 1, 0, 0 })]
        [TestCase(new[] { true, false, true }, new[] { 1, 0, 1 })]
        [TestCase(new[] { false, true, true, true, true }, new[] { 0, 1, 1 })]
        public void BuildMultiChoice_FitsCheckedToTheItems(bool[]? checkedItems, int[] expected)
        {
            Api.MultiChoiceRequest request = Api.BuildMultiChoice("t", new[] { "a", "b", "c" }, checkedItems, "n", "p", true, true, _allocated);

            var actual = new int[3];
            Marshal.Copy(request.Checked, actual, 0, 3);
            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void BuildTextInputAndLogin_SetEnablePositiveWhenEmpty()
        {
            Assert.AreEqual(1, Api.BuildTextInput("t", "m", "", "n", "p", true, true, true, _allocated).EnablePositiveWhenEmpty);
            Assert.AreEqual(0, Api.BuildTextInput("t", "m", "", "n", "p", false, true, true, _allocated).EnablePositiveWhenEmpty);
            Api.LoginRequest login = Api.BuildLogin("t", "m", "u", "pw", "n", "p", true, true, true, _allocated);
            Assert.AreEqual(1, login.EnablePositiveWhenEmpty);
            Assert.AreEqual(0u, login.Reserved1);
            Assert.AreEqual("u", ReadCString(login.UsernameHint));
        }

        // ── Checks (the 1.x messages, and J-D3 / J-D12) ──────────────────────────────────────────

        [TestCase(null, "m", true, "Title cannot be null or empty.")]
        [TestCase("", "m", true, "Title cannot be null or empty.")]
        [TestCase("t", "", true, "Message cannot be null or empty.")]
        [TestCase("t", null, false, null)]
        [TestCase("t", "m", true, null)]
        public void CheckTitleAndMessage(string? title, string? message, bool checkMessage, string? expected)
        {
            Assert.AreEqual(expected, Api.CheckTitleAndMessage(title, message, checkMessage));
        }

        [Test]
        public void CheckItems()
        {
            Assert.AreEqual("singleChoiceItems cannot be null or empty.", Api.CheckItems(null, "singleChoiceItems"));
            Assert.AreEqual("multiChoiceItems cannot be null or empty.", Api.CheckItems(new string[0], "multiChoiceItems"));
            Assert.AreEqual("singleChoiceItems cannot contain null.", Api.CheckItems(new[] { "a", null! }, "singleChoiceItems"));
            Assert.IsNull(Api.CheckItems(new[] { "a" }, "singleChoiceItems"));
        }

        [TestCase(-2, false)]
        [TestCase(-1, true)]
        [TestCase(0, true)]
        [TestCase(2, true)]
        [TestCase(3, false)]
        public void IsCheckedItemInRange_ForThreeItems(int checkedItem, bool expected)
        {
            Assert.AreEqual(expected, Api.IsCheckedItemInRange(checkedItem, 3));
        }

        // ── Completion → event arguments (design 4.3) ────────────────────────────────────────────

        private static Api.Answer Button(Api.Kind kind, string text, int index = -1, bool[]? states = null,
            string? input = null, string? user = null, string? password = null) =>
            new Api.Answer(kind, Api.ErrorNone, false, text, index, states, input, user, password);

        private static Api.Answer Dismissed(Api.Kind kind) => new Api.Answer(kind, Api.ErrorNone, isDismissed: true);

        private static Api.Answer Failed(Api.Kind kind, int error) => new Api.Answer(kind, error);

        [Test]
        public void Alert_ButtonDismissedAndFailure()
        {
            Assert.AreEqual(("OK", true, (string?)null), Api.ToButtonArgs(Button(Api.Kind.Alert, "OK")));
            Assert.AreEqual(("Cancel", true, ""), Api.ToButtonArgs(Dismissed(Api.Kind.Alert)));
            Assert.AreEqual(((string?)null, false, "Failed to showDialog: the app is not in the foreground."),
                Api.ToButtonArgs(Failed(Api.Kind.Alert, Api.ErrorNotForeground)));
        }

        [Test]
        public void Confirm_PositiveNegativeDismissedAndFailure()
        {
            Assert.AreEqual(("Yes", true, (string?)null), Api.ToButtonArgs(Button(Api.Kind.Confirm, "Yes")));
            Assert.AreEqual(("No", true, (string?)null), Api.ToButtonArgs(Button(Api.Kind.Confirm, "No")));
            Assert.AreEqual(("Cancel", true, ""), Api.ToButtonArgs(Dismissed(Api.Kind.Confirm)));
            Assert.AreEqual(((string?)null, false, "Failed to showConfirmDialog: the dialog was canceled."),
                Api.ToButtonArgs(Failed(Api.Kind.Confirm, Api.ErrorCanceled)));
        }

        [Test]
        public void SingleChoice_IndexNoneDismissedAndFailure()
        {
            Assert.AreEqual(("OK", (int?)1, true, (string?)null), Api.ToSingleChoiceArgs(Button(Api.Kind.SingleChoice, "OK", 1)));
            Assert.AreEqual(("Cancel", (int?)null, true, (string?)null), Api.ToSingleChoiceArgs(Button(Api.Kind.SingleChoice, "Cancel", -1)));
            Assert.AreEqual(("Cancel", (int?)null, true, ""), Api.ToSingleChoiceArgs(Dismissed(Api.Kind.SingleChoice)));
            Assert.AreEqual(((string?)null, (int?)null, false, "Failed to showSingleChoiceItemDialog: the dialog was closed by the system."),
                Api.ToSingleChoiceArgs(Failed(Api.Kind.SingleChoice, Api.ErrorCanceledBySystem)));
        }

        [Test]
        public void MultiChoice_StatesNegativeDismissedAndFailure()
        {
            var positive = Api.ToMultiChoiceArgs(Button(Api.Kind.MultiChoice, "OK", states: new[] { true, false }));
            Assert.AreEqual(new[] { true, false }, positive.checkedItems);
            Assert.IsTrue(positive.isSuccessful);
            Assert.IsNull(Api.ToMultiChoiceArgs(Button(Api.Kind.MultiChoice, "Cancel")).checkedItems, "negative");
            var dismissed = Api.ToMultiChoiceArgs(Dismissed(Api.Kind.MultiChoice));
            Assert.AreEqual(("Cancel", true, ""), (dismissed.buttonText, dismissed.isSuccessful, dismissed.errorMessage));
            Assert.IsNull(dismissed.checkedItems, "the C ABI does not report the states of a dismissed dialog (J-D5)");
            Assert.AreEqual(((string?)null, (bool[]?)null, false, "Failed to showMultiChoiceItemDialog: the dialog could not be shown."),
                Api.ToMultiChoiceArgs(Failed(Api.Kind.MultiChoice, Api.ErrorShowFailed)));
        }

        [Test]
        public void TextInput_TextNegativeNullDismissedEmpty()
        {
            Assert.AreEqual(("OK", "abc", true, (string?)null), Api.ToTextInputArgs(Button(Api.Kind.TextInput, "OK", input: "abc")));
            Assert.AreEqual(("OK", "", true, (string?)null), Api.ToTextInputArgs(Button(Api.Kind.TextInput, "OK", input: "")));
            Assert.AreEqual(("Cancel", (string?)null, true, (string?)null), Api.ToTextInputArgs(Button(Api.Kind.TextInput, "Cancel")), "negative is null, not empty");
            Assert.AreEqual(("Cancel", "", true, ""), Api.ToTextInputArgs(Dismissed(Api.Kind.TextInput)));
            Assert.AreEqual(((string?)null, (string?)null, false, "Failed to showTextInputDialog: the app is not in the foreground."),
                Api.ToTextInputArgs(Failed(Api.Kind.TextInput, Api.ErrorNotForeground)));
        }

        [Test]
        public void Login_ValuesNegativeNullDismissedEmpty()
        {
            Assert.AreEqual(("Login", "u", "p", true, (string?)null), Api.ToLoginArgs(Button(Api.Kind.Login, "Login", user: "u", password: "p")));
            Assert.AreEqual(("Cancel", (string?)null, (string?)null, true, (string?)null), Api.ToLoginArgs(Button(Api.Kind.Login, "Cancel")));
            Assert.AreEqual(("Cancel", "", "", true, ""), Api.ToLoginArgs(Dismissed(Api.Kind.Login)));
            Assert.AreEqual(((string?)null, (string?)null, (string?)null, false, "Failed to showLoginDialog: the dialog was canceled."),
                Api.ToLoginArgs(Failed(Api.Kind.Login, Api.ErrorCanceled)));
        }

        // ── Error names and messages (design 4.4) ────────────────────────────────────────────────

        [TestCase(Api.ErrorNone, "NONE")]
        [TestCase(Api.ErrorInvalidParameter, "INVALID_PARAMETER")]
        [TestCase(Api.ErrorNotInitialized, "NOT_INITIALIZED")]
        [TestCase(Api.ErrorNotSupported, "NOT_SUPPORTED")]
        [TestCase(Api.ErrorUnknown, "UNKNOWN")]
        [TestCase(Api.ErrorOutOfMemory, "OUT_OF_MEMORY")]
        [TestCase(Api.ErrorCanceled, "CANCELED")]
        [TestCase(Api.ErrorCanceledBySystem, "CANCELED_BY_SYSTEM")]
        [TestCase(Api.ErrorNotForeground, "NOT_FOREGROUND")]
        [TestCase(Api.ErrorHostStartFailed, "HOST_START_FAILED")]
        [TestCase(Api.ErrorShowFailed, "SHOW_FAILED")]
        [TestCase(42, "42")]
        public void ErrorName(int error, string expected) => Assert.AreEqual(expected, Api.ErrorName(error));

        [Test]
        public void EntryFailureMessages()
        {
            Assert.AreEqual("Failed to showDialog: the native library is not initialized yet.", Api.EntryFailureMessage(Api.Kind.Alert, Api.ErrorNotInitialized));
            Assert.AreEqual("Failed to showConfirmDialog: the request was rejected (INVALID_PARAMETER).", Api.EntryFailureMessage(Api.Kind.Confirm, Api.ErrorInvalidParameter));
            Assert.AreEqual("Failed to showLoginDialog: the request was rejected (NOT_SUPPORTED).", Api.EntryFailureMessage(Api.Kind.Login, Api.ErrorNotSupported));
            Assert.AreEqual("Failed to showTextInputDialog: OUT_OF_MEMORY.", Api.EntryFailureMessage(Api.Kind.TextInput, Api.ErrorOutOfMemory));
            Assert.IsTrue(Api.IsRequestDefect(Api.ErrorInvalidParameter));
            Assert.IsTrue(Api.IsRequestDefect(Api.ErrorNotSupported));
            Assert.IsFalse(Api.IsRequestDefect(Api.ErrorOutOfMemory));
        }

        [Test]
        public void CompletionFailureMessages()
        {
            Assert.AreEqual("Failed to showDialog: the dialog host could not be started.", Api.CompletionFailureMessage(Api.Kind.Alert, Api.ErrorHostStartFailed));
            Assert.AreEqual("Failed to showMultiChoiceItemDialog: the dialog could not be shown.", Api.CompletionFailureMessage(Api.Kind.MultiChoice, Api.ErrorShowFailed));
            Assert.AreEqual("Failed to showDialog: UNKNOWN.", Api.CompletionFailureMessage(Api.Kind.Alert, Api.ErrorUnknown));
            Assert.AreEqual("Failed to showTextInputDialog: NOT_INITIALIZED.", Api.CompletionFailureMessage(Api.Kind.TextInput, Api.ErrorNotInitialized));
            Assert.AreEqual("Failed to showLoginDialog: 42.", Api.CompletionFailureMessage(Api.Kind.Login, 42));
            Assert.AreEqual("Failed to showConfirmDialog: 42.", Api.EntryFailureMessage(Api.Kind.Confirm, 42));
            Assert.AreEqual("Failed to showSingleChoiceItemDialog: the native library is not available (libntk.so ships for arm64-v8a and x86_64 only).",
                Api.NativeUnavailableMessage(Api.Kind.SingleChoice));
        }
    }
}
#endif
