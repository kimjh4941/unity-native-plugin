#nullable enable

using JonghyunKim.NativeToolkit.Runtime.Share;

namespace JonghyunKim.NativeToolkit.IosUiHarness
{
    /// <summary>Share: a text the test shares or dismisses. The value says whether an activity was chosen, not which.</summary>
    internal static class ShareScenario
    {
        public static void Register()
        {
            IosUiHarness.Row("text", () => IosShareManager.Instance.Share(
                new IosShareContentPayload { items = new[] { IosShareItem.Text("ntk ui harness share") } },
                r => IosUiHarness.Append("text", r.IsSuccess ? "ok" : "fail",
                    $"completed={r.Completed} activity={(r.ActivityType == null ? "none" : "set")}")));
        }
    }
}
