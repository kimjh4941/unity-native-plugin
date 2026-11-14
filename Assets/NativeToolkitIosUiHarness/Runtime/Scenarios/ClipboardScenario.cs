#nullable enable

using JonghyunKim.NativeToolkit.Runtime.Clipboard;

namespace JonghyunKim.NativeToolkit.IosUiHarness
{
    /// <summary>Clipboard: a named pasteboard round trip. The general pasteboard is never touched.</summary>
    internal static class ClipboardScenario
    {
        private const string Name = "com.jonghyunkim.nativetoolkit.uiharness.roundtrip";

        public static void Register()
        {
            IosUiHarness.Row("named", () =>
            {
                var manager = IosClipboardManager.Instance;
                var scope = IosPasteboardScope.Named(Name);
                manager.CreatePasteboard(IosPasteboardCreationRequest.Named(Name), created =>
                    manager.Copy(IosClipboardContent.PlainText("ntkh-roundtrip"), scope, null, copied =>
                        manager.Read(scope, read =>
                        {
                            bool match = read.IsSuccess && read.Items.Count == 1 && read.Items[0].Text == "ntkh-roundtrip";
                            IosUiHarness.Append("named", match ? "ok" : "fail", $"items={read.Items.Count} match={match}");
                            manager.RemovePasteboard(scope);
                        })));
            });
        }
    }
}
