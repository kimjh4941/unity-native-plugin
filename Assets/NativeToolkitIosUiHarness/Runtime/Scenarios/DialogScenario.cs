#nullable enable

using JonghyunKim.NativeToolkit.Runtime.iOS.Dialog;

namespace JonghyunKim.NativeToolkit.IosUiHarness
{
    /// <summary>
    /// Dialog: the rows of the Dialog UI test plan v3 (artifact/ios/dialog/designs/2026-11-14-ios-dialog-ui-test-plan-v3.md).
    /// Each event writes one result line under its dialog kind (dialog, confirm, destructive, sheet, text,
    /// login), so a test can count them. Values tell null from empty; a password shows only its length.
    /// </summary>
    internal static class DialogScenario
    {
        public static void Register()
        {
            var m = IosDialogManager.Instance;
            m.DialogResult += (b, ok, e) => Line("dialog", ok, $"button={IosUiHarness.Value(b)}", e);
            m.ConfirmDialogResult += (b, ok, e) => Line("confirm", ok, $"button={IosUiHarness.Value(b)}", e);
            m.DestructiveDialogResult += (b, ok, e) => Line("destructive", ok, $"button={IosUiHarness.Value(b)}", e);
            m.ActionSheetResult += (b, ok, e) => Line("sheet", ok, $"button={IosUiHarness.Value(b)}", e);
            m.TextInputDialogResult += (b, input, ok, e) =>
                Line("text", ok, $"button={IosUiHarness.Value(b)} input={IosUiHarness.Value(input)}", e);
            m.LoginDialogResult += (b, user, password, ok, e) =>
                Line("login", ok, $"button={IosUiHarness.Value(b)} username={IosUiHarness.Value(user)} " +
                    (password == null ? "password=<null>" : $"password-length={password.Length}"), e);

            IosUiHarness.Row("show-dialog", () => m.ShowDialog("NTK", "basic", "OK"));                         // ID-1, ID-15
            IosUiHarness.Row("show-dialog-ja", () => m.ShowDialog("NTK", "custom", "了解"));                   // ID-2
            IosUiHarness.Row("show-confirm", () => m.ShowConfirmDialog("NTK", "confirm", "OK", "Cancel"));      // ID-3, ID-4
            IosUiHarness.Row("show-destructive", () => m.ShowDestructiveDialog("NTK", "destructive", "Delete", "Cancel")); // ID-5, ID-6
            IosUiHarness.Row("show-sheet", () => m.ShowActionSheet("NTK", "sheet", new[] { "A", "B", "C" }, "Cancel")); // ID-7, ID-8
            IosUiHarness.Row("show-text", () => m.ShowTextInputDialog("NTK", "text", "placeholder", "OK", "Cancel", false)); // ID-9, ID-10
            IosUiHarness.Row("show-text-empty-ok", () => m.ShowTextInputDialog("NTK", "text", "placeholder", "OK", "Cancel", true)); // ID-11
            IosUiHarness.Row("show-login", () => m.ShowLoginDialog("NTK", "login", "Username", "Password", "Login", "Cancel", false)); // ID-12, ID-13
            IosUiHarness.Row("show-login-empty-ok", () => m.ShowLoginDialog("NTK", "login", "Username", "Password", "Login", "Cancel", true)); // ID-16
            IosUiHarness.Row("twice-now", () =>                                                                  // ID-14a
            {
                m.ShowConfirmDialog("NTK", "first", "OK1", "Cancel");
                m.ShowConfirmDialog("NTK", "second", "OK2", "Cancel");
            });
            IosUiHarness.Hidden("twice-later-second", () =>
            {
                // The first dialog must be on screen by now, or this is ID-14a's case again.
                IosUiHarness.Append("twice", "event", $"first-shown={(IosUiHarness.Presenting() ? "yes" : "no")}");
                m.ShowConfirmDialog("NTK", "second", "OK2", "Cancel");
            });
            IosUiHarness.Row("twice-later", () =>                                                                // ID-14b
            {
                m.ShowConfirmDialog("NTK", "first", "OK1", "Cancel");
                IosUiHarness.After(1.0, "twice-later-second");
            });
            IosUiHarness.Hidden("late-fire", () =>                                                               // ID-17
            {
                IosUiHarness.Append("late", "event", $"state={IosUiHarness.ActivationState()}");
                m.ShowDialog("NTK", "late", "OK");
            });
            IosUiHarness.Row("late", () => IosUiHarness.After(3.0, "late-fire"));
        }

        // A success carries no error; if it ever does, the line shows it and no expected line matches.
        private static void Line(string id, bool ok, string value, string? error) =>
            IosUiHarness.Append(id, ok ? "ok" : "fail", ok && error == null ? value : $"{value} error={IosUiHarness.Value(error)}");
    }
}
