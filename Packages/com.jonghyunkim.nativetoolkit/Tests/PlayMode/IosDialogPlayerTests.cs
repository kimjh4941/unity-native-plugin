#nullable enable

#if UNITY_IOS
#if !UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.TestTools;
using JonghyunKim.NativeToolkit.Runtime.iOS.Common;
using JonghyunKim.NativeToolkit.Runtime.iOS.Dialog;
using static JonghyunKim.NativeToolkit.Tests.IosPlayerTestSupport;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// iOS player: the Dialog calls rejected in C# before any alert is shown (Dialog UI test plan v3,
    /// ID-20 to ID-22, artifact/ios/dialog/designs/2026-11-14-ios-dialog-ui-test-plan-v3.md; ID-22b from
    /// the C ABI design v2, D-4). Each call fires its event exactly once inside the call, with every
    /// value null, and nothing more comes within a second (an alert shown after the rejection would
    /// answer later). Error logs are not asserted; failing log messages are ignored during these tests.
    /// Every path that reaches the native side shows an alert, which is left to the UI automation
    /// (scripts/test_ios_ui.sh).
    /// </summary>
    [Category(IosPlayerTestSupport.Category)]
    public class IosDialogPlayerTests
    {
        private const string NoTitle = "Title cannot be null or empty.";
        private const string NoMessage = "Message cannot be null or empty.";
        private const string NoOptions = "Options cannot be null or empty.";
        private const string NullOption = "Options cannot contain null.";

        /// <summary>One event: its kind, whether every value argument was null, success, error.</summary>
        private readonly List<(string kind, bool valuesNull, bool ok, string? error)> _seen = new();
        private readonly List<Action> _unsubscribe = new();

        [SetUp]
        public void SetUp()
        {
            _seen.Clear();
            var m = IosDialogManager.Instance;
            Action<string?, bool, string?> dialog = (b, ok, e) => _seen.Add(("dialog", b == null, ok, e));
            Action<string?, bool, string?> confirm = (b, ok, e) => _seen.Add(("confirm", b == null, ok, e));
            Action<string?, bool, string?> destructive = (b, ok, e) => _seen.Add(("destructive", b == null, ok, e));
            Action<string?, bool, string?> sheet = (b, ok, e) => _seen.Add(("sheet", b == null, ok, e));
            Action<string?, string?, bool, string?> text = (b, i, ok, e) => _seen.Add(("text", b == null && i == null, ok, e));
            Action<string?, string?, string?, bool, string?> login = (b, u, p, ok, e) => _seen.Add(("login", b == null && u == null && p == null, ok, e));
            m.DialogResult += dialog;
            m.ConfirmDialogResult += confirm;
            m.DestructiveDialogResult += destructive;
            m.ActionSheetResult += sheet;
            m.TextInputDialogResult += text;
            m.LoginDialogResult += login;
            _unsubscribe.Add(() =>
            {
                m.DialogResult -= dialog;
                m.ConfirmDialogResult -= confirm;
                m.DestructiveDialogResult -= destructive;
                m.ActionSheetResult -= sheet;
                m.TextInputDialogResult -= text;
                m.LoginDialogResult -= login;
            });
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var unsubscribe in _unsubscribe)
                unsubscribe();
            _unsubscribe.Clear();
            LogAssert.ignoreFailingMessages = false;
        }

        /// <summary>Runs the call and requires exactly one rejected event of the kind inside it.</summary>
        private void ExpectRejectedInside(string kind, string error, Action call)
        {
            // Set inside the test body: the test framework resets it when the body starts (set in [SetUp],
            // it did not hold on the player, 2026-11-14).
            LogAssert.ignoreFailingMessages = true;
            int before = _seen.Count;
            call();
            Assert.AreEqual(before + 1, _seen.Count, $"{kind}: not exactly one event inside the call");
            var e = _seen[before];
            Assert.AreEqual(kind, e.kind);
            Assert.IsTrue(e.valuesNull, $"{kind}: a value argument was not null");
            Assert.IsFalse(e.ok, kind);
            Assert.AreEqual(error, e.error, kind);
        }

        private void RejectAll(string title, string message, string error)
        {
            var m = IosDialogManager.Instance;
            ExpectRejectedInside("dialog", error, () => m.ShowDialog(title, message));
            ExpectRejectedInside("confirm", error, () => m.ShowConfirmDialog(title, message));
            ExpectRejectedInside("destructive", error, () => m.ShowDestructiveDialog(title, message));
            ExpectRejectedInside("sheet", error, () => m.ShowActionSheet(title, message, new[] { "A" }));
            ExpectRejectedInside("text", error, () => m.ShowTextInputDialog(title, message));
            ExpectRejectedInside("login", error, () => m.ShowLoginDialog(title, message));
        }

        private IEnumerator NothingMoreWithinASecond(int count)
        {
            yield return WaitFor(() => false, 1f);
            Assert.AreEqual(count, _seen.Count, "an event came after the rejection");
        }

        [UnityTest]
        public IEnumerator ID20_EmptyTitleIsRejectedInsideTheCall()
        {
            RejectAll("", "player test", NoTitle);
            yield return NothingMoreWithinASecond(6);
        }

        [UnityTest]
        public IEnumerator ID21_EmptyMessageIsRejectedInsideTheCall()
        {
            RejectAll("NTK", "", NoMessage);
            yield return NothingMoreWithinASecond(6);
        }

        [UnityTest]
        public IEnumerator ID22_ActionSheetWithAnEmptyOptionsArrayIsRejected()
        {
            ExpectRejectedInside("sheet", NoOptions, () => IosDialogManager.Instance.ShowActionSheet("NTK", "player test", new string[0]));
            yield return NothingMoreWithinASecond(1);
        }

        [UnityTest]
        public IEnumerator ID22_ActionSheetWithANullOptionsArrayIsRejected()
        {
            ExpectRejectedInside("sheet", NoOptions, () => IosDialogManager.Instance.ShowActionSheet("NTK", "player test", null!));
            yield return NothingMoreWithinASecond(1);
        }

        /// <summary>
        /// ID-22b: an options array holding null is rejected in C# since the move to the C ABI (which
        /// would reject it, with no completion). 1.x failed inside the call too, with "Internal error: …"
        /// from its UTF-8 conversion (C ABI design v2, D-4): only the message changed.
        /// </summary>
        [UnityTest]
        public IEnumerator ID22b_ActionSheetWithANullOptionIsRejected()
        {
            ExpectRejectedInside("sheet", NullOption, () => IosDialogManager.Instance.ShowActionSheet("NTK", "player test", new[] { "A", null! }));
            yield return NothingMoreWithinASecond(1);
        }
    }

    /// <summary>
    /// iOS player: the C ABI paths of <see cref="IosDialogManager"/> that no alert reaches
    /// (implementation review v1 of the Dialog move, R-4 and R2-2). The library rejects a malformed
    /// request with no completion; a completion raises its event on the next Update, not inside the
    /// callback (C ABI design v2, D-6 and D-7).
    /// </summary>
    [Category(IosPlayerTestSupport.Category)]
    public class IosDialogCApiPlayerTests
    {
        private readonly List<(bool valuesNull, bool ok, string? error)> _confirm = new();
        private readonly List<(bool valuesNull, bool ok, string? error)> _alert = new();
        private Action? _unsubscribe;

        [SetUp]
        public void SetUp()
        {
            _confirm.Clear();
            _alert.Clear();
            var m = IosDialogManager.Instance;
            Action<string?, bool, string?> confirm = (b, ok, e) => _confirm.Add((b == null, ok, e));
            Action<string?, bool, string?> alert = (b, ok, e) => _alert.Add((b == null, ok, e));
            m.ConfirmDialogResult += confirm;
            m.DialogResult += alert;
            _unsubscribe = () => { m.ConfirmDialogResult -= confirm; m.DialogResult -= alert; };
        }

        [TearDown]
        public void TearDown() => _unsubscribe?.Invoke();

        [UnityTest]
        public IEnumerator MalformedRequest_IsRejectedWithoutACompletion()
        {
            var allocated = new List<IntPtr>();
            int error;
            try
            {
                IosDialogCApi.AlertRequest request = IosDialogCApi.BuildAlert("NTK", "player test", "OK", allocated);
                request.StructSize = 0;
                error = IosDialogCApi.ntk_dialog_show_alert_async(ref request, IosDialogManager.s_resultCallbackPointer,
                    new IntPtr((int)IosDialogCApi.Kind.Alert), IntPtr.Zero, IntPtr.Zero);
            }
            finally
            {
                IosNativeToolkitCApi.FreeAll(allocated);
            }
            Assert.AreEqual(IosDialogCApi.ErrorInvalidParameter, error);
            yield return WaitFor(() => false, 1f);
            Assert.AreEqual(0, _alert.Count, "a rejected request completed");
        }

        [UnityTest]
        public IEnumerator Completion_IsRaisedOnTheNextUpdate()
        {
            IosDialogManager.OnResult(new IntPtr((int)IosDialogCApi.Kind.Confirm), 1, IosDialogCApi.ErrorNotForeground, 0, IntPtr.Zero);
            Assert.AreEqual(0, _confirm.Count, "raised inside the callback");
            yield return null;
            CollectionAssert.AreEqual(new[] { (true, false, (string?)"Failed to showConfirmDialog: the app is not in the foreground.") }, _confirm);
            yield return WaitFor(() => false, 1f);
            Assert.AreEqual(1, _confirm.Count, "raised again");
        }

        [UnityTest]
        public IEnumerator AcceptedWithoutAResult_CompletesAsUnknown()
        {
            IosDialogManager.OnResult(new IntPtr((int)IosDialogCApi.Kind.Alert), 2, IosDialogCApi.ErrorNone, 0, IntPtr.Zero);
            yield return null;
            CollectionAssert.AreEqual(new[] { (true, false, (string?)"Failed to showDialog: UNKNOWN.") }, _alert);
        }
    }
}
#endif
#endif
