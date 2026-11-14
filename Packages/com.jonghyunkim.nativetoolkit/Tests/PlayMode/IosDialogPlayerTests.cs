#nullable enable

#if UNITY_IOS
#if !UNITY_EDITOR
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using JonghyunKim.NativeToolkit.Runtime.iOS.Dialog;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// iOS player: the Dialog calls rejected in C# before any alert is shown (Dialog UI test plan v3,
    /// ID-20 to ID-22, artifact/ios/dialog/designs/2026-11-14-ios-dialog-ui-test-plan-v3.md). Each event
    /// fires inside the call. Every path that reaches the native side shows an alert, which is left to the
    /// UI automation (scripts/test_ios_ui.sh); so is an options array holding a null element (plan v3,
    /// R-7: it may reach the native side).
    /// </summary>
    [Category(IosPlayerTestSupport.Category)]
    public class IosDialogPlayerTests
    {
        private const string NoTitle = "Title cannot be null or empty.";
        private const string NoMessage = "Message cannot be null or empty.";
        private const string NoOptions = "Options cannot be null or empty.";

        /// <summary>Calls each of the six Show methods and returns (kind, success, error) per event, in call order.</summary>
        private static List<(string kind, bool ok, string? error)> ShowAll(string title, string message)
        {
            var m = IosDialogManager.Instance;
            var seen = new List<(string, bool, string?)>();
            Action<string?, bool, string?> dialog = (_, ok, e) => seen.Add(("dialog", ok, e));
            Action<string?, bool, string?> confirm = (_, ok, e) => seen.Add(("confirm", ok, e));
            Action<string?, bool, string?> destructive = (_, ok, e) => seen.Add(("destructive", ok, e));
            Action<string?, bool, string?> sheet = (_, ok, e) => seen.Add(("sheet", ok, e));
            Action<string?, string?, bool, string?> text = (_, _, ok, e) => seen.Add(("text", ok, e));
            Action<string?, string?, string?, bool, string?> login = (_, _, _, ok, e) => seen.Add(("login", ok, e));
            m.DialogResult += dialog;
            m.ConfirmDialogResult += confirm;
            m.DestructiveDialogResult += destructive;
            m.ActionSheetResult += sheet;
            m.TextInputDialogResult += text;
            m.LoginDialogResult += login;
            try
            {
                m.ShowDialog(title, message);
                m.ShowConfirmDialog(title, message);
                m.ShowDestructiveDialog(title, message);
                m.ShowActionSheet(title, message, new[] { "A" });
                m.ShowTextInputDialog(title, message);
                m.ShowLoginDialog(title, message);
            }
            finally
            {
                m.DialogResult -= dialog;
                m.ConfirmDialogResult -= confirm;
                m.DestructiveDialogResult -= destructive;
                m.ActionSheetResult -= sheet;
                m.TextInputDialogResult -= text;
                m.LoginDialogResult -= login;
            }
            return seen;
        }

        private static void ExpectAllRejected(List<(string kind, bool ok, string? error)> seen, string error)
        {
            CollectionAssert.AreEqual(
                new[] { "dialog", "confirm", "destructive", "sheet", "text", "login" },
                seen.ConvertAll(s => s.kind));
            foreach (var s in seen)
            {
                Assert.IsFalse(s.ok, s.kind);
                Assert.AreEqual(error, s.error, s.kind);
            }
        }

        [Test]
        public void ID20_EmptyTitleIsRejectedInsideTheCall()
        {
            for (int i = 0; i < 6; i++)
                LogAssert.Expect(LogType.Error, NoTitle);
            ExpectAllRejected(ShowAll("", "player test"), NoTitle);
        }

        [Test]
        public void ID21_EmptyMessageIsRejectedInsideTheCall()
        {
            for (int i = 0; i < 6; i++)
                LogAssert.Expect(LogType.Error, NoMessage);
            ExpectAllRejected(ShowAll("NTK", ""), NoMessage);
        }

        [TestCase(false, TestName = "ID22_ActionSheetWithoutOptionsIsRejected(empty)")]
        [TestCase(true, TestName = "ID22_ActionSheetWithoutOptionsIsRejected(null)")]
        public void ID22_ActionSheetWithoutOptionsIsRejected(bool nullArray)
        {
            string? error = null;
            bool? success = null;
            void OnResult(string? button, bool ok, string? message) { success = ok; error = message; }
            var manager = IosDialogManager.Instance;
            manager.ActionSheetResult += OnResult;
            try
            {
                LogAssert.Expect(LogType.Error, NoOptions);
                manager.ShowActionSheet("NTK", "player test", nullArray ? null! : new string[0]);
            }
            finally
            {
                manager.ActionSheetResult -= OnResult;
            }
            Assert.AreEqual(false, success);
            Assert.AreEqual(NoOptions, error);
        }
    }
}
#endif
#endif
