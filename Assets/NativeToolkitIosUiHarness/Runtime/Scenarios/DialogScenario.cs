#nullable enable

using JonghyunKim.NativeToolkit.Runtime.Dialog;

namespace JonghyunKim.NativeToolkit.IosUiHarness
{
    /// <summary>Dialog: a confirm dialog the test answers with OK or Cancel.</summary>
    internal static class DialogScenario
    {
        public static void Register()
        {
            IosDialogManager.Instance.ConfirmDialogResult += (button, ok, error) =>
                IosUiHarness.Append("confirm", ok ? "ok" : "fail", button ?? error ?? "null");
            IosUiHarness.Row("confirm", () =>
                IosDialogManager.Instance.ShowConfirmDialog("NTK UI Harness", "Confirm?", "OK", "Cancel"));
        }
    }
}
