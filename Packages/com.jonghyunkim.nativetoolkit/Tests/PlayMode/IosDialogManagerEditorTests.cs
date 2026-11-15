#nullable enable

#if UNITY_IOS && UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using JonghyunKim.NativeToolkit.Runtime.iOS.Dialog;
using Api = JonghyunKim.NativeToolkit.Runtime.iOS.Dialog.IosDialogCApi;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// <see cref="IosDialogManager"/> in the Editor with the iOS target (artifact/ios/dialog, C ABI design
    /// v2, D-6 (a) and D-7): the native library is not there, so every Show* that passes the C# checks
    /// fails once inside the call; and a subscriber's exception does not leave the manager.
    /// </summary>
    public sealed class IosDialogManagerEditorTests
    {
        private readonly List<(string kind, bool valuesNull, bool ok, string? error)> _seen = new();
        private Action? _unsubscribe;

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
            _unsubscribe = () =>
            {
                m.DialogResult -= dialog;
                m.ConfirmDialogResult -= confirm;
                m.DestructiveDialogResult -= destructive;
                m.ActionSheetResult -= sheet;
                m.TextInputDialogResult -= text;
                m.LoginDialogResult -= login;
            };
        }

        [TearDown]
        public void TearDown() => _unsubscribe?.Invoke();

        private void ExpectInside(string kind, string function, Action call)
        {
            int before = _seen.Count;
            call();
            Assert.AreEqual(before + 1, _seen.Count, $"{kind}: not exactly one event inside the call");
            var e = _seen[before];
            Assert.AreEqual(kind, e.kind);
            Assert.IsTrue(e.valuesNull, $"{kind}: a value was not null");
            Assert.IsFalse(e.ok, kind);
            Assert.AreEqual($"Failed to {function}: the native library is not available.", e.error, kind);
        }

        [UnityTest]
        public IEnumerator EveryShow_FailsInsideTheCall_WithoutTheNativeLibrary()
        {
            var m = IosDialogManager.Instance;
            ExpectInside("dialog", "showDialog", () => m.ShowDialog("NTK", "editor"));
            ExpectInside("confirm", "showConfirmDialog", () => m.ShowConfirmDialog("NTK", "editor"));
            ExpectInside("destructive", "showDestructiveDialog", () => m.ShowDestructiveDialog("NTK", "editor"));
            ExpectInside("sheet", "showActionSheet", () => m.ShowActionSheet("NTK", "editor", new[] { "A" }));
            ExpectInside("text", "showTextInputDialog", () => m.ShowTextInputDialog("NTK", "editor"));
            ExpectInside("login", "showLoginDialog", () => m.ShowLoginDialog("NTK", "editor"));

            float deadline = Time.realtimeSinceStartup + 1f;
            while (Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(6, _seen.Count, "an event came after the call");
        }

        [Test]
        public void NullOption_IsRejectedInCSharp()
        {
            LogAssert.Expect(LogType.Error, Api.OptionsContainNull);
            int before = _seen.Count;
            IosDialogManager.Instance.ShowActionSheet("NTK", "editor", new[] { "A", null! });
            Assert.AreEqual(before + 1, _seen.Count);
            Assert.AreEqual(("sheet", true, false, Api.OptionsContainNull), _seen[before]);
        }

        [Test]
        public void SubscriberException_IsLoggedAndDoesNotLeave()
        {
            var m = IosDialogManager.Instance;
            Action<string?, bool, string?> thrower = (_, _, _) => throw new InvalidOperationException("boom");
            m.ConfirmDialogResult += thrower;
            try
            {
                LogAssert.Expect(LogType.Error, "ConfirmDialogResult callback error: boom");
                Assert.DoesNotThrow(() => m.Raise(new Api.Answer(Api.Kind.Confirm, Api.ErrorNone, "OK")));
            }
            finally
            {
                m.ConfirmDialogResult -= thrower;
            }
        }

        [Test]
        public void Raise_CarriesACompletion()
        {
            IosDialogManager.Instance.Raise(new Api.Answer(Api.Kind.Login, Api.ErrorNone, "Login", username: "u", password: "p"));
            Assert.AreEqual(("login", false, true, (string?)null), _seen[_seen.Count - 1]);
        }
    }
}
#endif
