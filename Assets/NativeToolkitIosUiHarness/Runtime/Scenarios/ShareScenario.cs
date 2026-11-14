#nullable enable

using System.IO;
using System.Runtime.InteropServices;
using JonghyunKim.NativeToolkit.Runtime.iOS.Share;
using UnityEngine;

namespace JonghyunKim.NativeToolkit.IosUiHarness
{
    /// <summary>
    /// Share: the rows of the Share UI test plan v3 (artifact/ios/share/designs/2026-11-14-ios-share-ui-test-plan-v3.md).
    /// </summary>
    /// <remarks>
    /// Lines come from the per-call callback under the id <c>share</c>; SH-13 writes its two callbacks as
    /// <c>share1</c> and <c>share2</c>. Every ShareCompleted event also writes a <c>share-event</c> line, so
    /// SH-13 can count where each result went (1.x keeps one callback slot). Activity types are written
    /// only for Apple's own activities (others are <c>&lt;other&gt;</c>), and the app's data path is shown
    /// as <c>&lt;data&gt;</c>.
    /// </remarks>
    internal static class ShareScenario
    {
        private const string CopyActivity = "com.apple.UIKit.activity.CopyToPasteboard";

        [DllImport("__Internal")] private static extern int ntkh_abi();

        public static void Register()
        {
            IosUiHarness.Append("abi", "event", ntkh_abi() == 1 ? "2" : "1");   // which ABI the package runs on (3-S)
            var m = IosShareManager.Instance;
            m.ShareCompleted += r => Line("share-event", r);
            string data = Application.persistentDataPath;

            Row("sh-text", P(IosShareItem.Text("ntk ui harness share")));                                   // SH-1, 6, 14
            Row("sh-url", P(IosShareItem.Url("https://example.com/ntk")));                                  // SH-2
            Row("sh-text-url", P(IosShareItem.Text("ntk"), IosShareItem.Url("https://example.com/ntk")));    // SH-3
            IosUiHarness.Row("sh-image", () =>                                                              // SH-4
            {
                string path = Path.Combine(data, "ntk-share.png");
                var texture = new Texture2D(2, 2);
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Share(P(IosShareItem.Image(path)));
            });
            IosUiHarness.Row("sh-file", () =>                                                               // SH-5
            {
                string path = Path.Combine(data, "ntk-share.txt");
                File.WriteAllText(path, "ntk ui harness file");
                Share(P(IosShareItem.File(path)));
            });
            Row("sh-exclude-copy", new IosShareContentPayload                                               // SH-7
            {
                items = new[] { IosShareItem.Text("ntk") },
                excludedActivityTypes = new[] { CopyActivity },
            });
            Row("sh-preview", new IosShareContentPayload                                                    // SH-8
            {
                items = new[] { IosShareItem.Text("ntk") },
                previewTitle = "NTK Preview",
            });
            Row("sh-bad-url", P(IosShareItem.Url("not a url")));                                            // SH-9
            Row("sh-text-bad-url", P(IosShareItem.Text("ntk"), IosShareItem.Url("not a url")));             // SH-10
            Row("sh-missing-image", P(IosShareItem.Image(Path.Combine(data, "missing.png"))));              // SH-11
            Row("sh-missing-file", P(IosShareItem.File(Path.Combine(data, "missing.txt"))));                // SH-12
            IosUiHarness.Row("sh-twice", () =>                                                              // SH-13
            {
                m.Share(P(IosShareItem.Text("first")), r => Line("share1", r));
                m.Share(P(IosShareItem.Text("second")), r => Line("share2", r));
            });
            IosUiHarness.Hidden("sh-late-fire", () =>                                                       // SH-15
            {
                IosUiHarness.Append("late", "event", $"state={IosUiHarness.ActivationState()}");
                Share(P(IosShareItem.Text("late")));
            });
            IosUiHarness.Row("sh-late", () => IosUiHarness.After(3.0, "sh-late-fire"));
            Row("sh-unknown-type", P(IosShareItem.Text("ntk"), new IosShareItem { type = "unknown", value = "x" })); // SH-16
            Row("sh-exclude-null", new IosShareContentPayload                                               // SH-17
            {
                items = new[] { IosShareItem.Text("ntk") },
                excludedActivityTypes = new[] { null!, CopyActivity },
            });
            IosUiHarness.Hidden("sh-background-fire", () =>                                                 // SH-18
            {
                IosUiHarness.Append("late", "event", $"state={IosUiHarness.ActivationState()}");
                // The state when the result arrives too (the plan: "the state after coming back").
                IosShareManager.Instance.Share(P(IosShareItem.Text("background")), r =>
                {
                    Line("share", r);
                    IosUiHarness.Append("late", "result", $"state={IosUiHarness.ActivationState()}");
                });
            });
            IosUiHarness.Row("sh-background", () => IosUiHarness.OnBackground("sh-background-fire"));
        }

        private static IosShareContentPayload P(params IosShareItem[] items) => new() { items = items };

        private static void Row(string id, IosShareContentPayload payload) => IosUiHarness.Row(id, () => Share(payload));

        private static void Share(IosShareContentPayload payload) => IosShareManager.Instance.Share(payload, r => Line("share", r));

        private static void Line(string id, IosShareResult r)
        {
            string activity = r.ActivityType == null ? "<null>"
                : r.ActivityType.StartsWith("com.apple.UIKit.activity.") ? r.ActivityType : "<other>";
            string value = $"completed={r.Completed} activity={activity}";
            if (r.ErrorMessage != null)
                value += $" error={r.ErrorMessage.Replace(Application.persistentDataPath, "<data>")}";
            IosUiHarness.Append(id, r.IsSuccess ? "ok" : "fail", value);
        }
    }
}
