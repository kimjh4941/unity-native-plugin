#nullable enable

#if UNITY_IOS
#if !UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;
using JonghyunKim.NativeToolkit.Runtime.Clipboard;
using static JonghyunKim.NativeToolkit.Tests.IosPlayerTestSupport;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// iOS player: a named pasteboard round trip through the native bridge. Uses a named pasteboard only,
    /// never the general one (artifact/topics/ios-c-abi-2, plan design v4, I-13).
    /// </summary>
    [Category(IosPlayerTestSupport.Category)]
    public class IosClipboardPlayerTests
    {
        private const string PasteboardName = "com.jonghyunkim.nativetoolkit.playertests.roundtrip";

        [UnityTest]
        public IEnumerator NamedPasteboardRoundTripsPlainText()
        {
            var manager = IosClipboardManager.Instance;
            var scope = IosPasteboardScope.Named(PasteboardName);
            try
            {
                IosPasteboardScopeResult? created = null;
                manager.CreatePasteboard(IosPasteboardCreationRequest.Named(PasteboardName), r => created = r);
                yield return WaitFor(() => created != null);
                Assert.IsNotNull(created, "no CreatePasteboard result within the timeout");
                Assert.IsTrue(created!.Value.IsSuccess, $"CreatePasteboard failed: {created.Value.Error}");

                IosClipboardOperationResult? copied = null;
                manager.Copy(IosClipboardContent.PlainText("ntk player round trip"), scope, null, r => copied = r);
                yield return WaitFor(() => copied != null);
                Assert.IsNotNull(copied, "no Copy result within the timeout");
                Assert.IsTrue(copied!.Value.IsSuccess, $"Copy failed: {copied.Value.Error}");

                IosClipboardReadResult? read = null;
                manager.Read(scope, r => read = r);
                yield return WaitFor(() => read != null);
                Assert.IsNotNull(read, "no Read result within the timeout");
                Assert.IsTrue(read!.Value.IsSuccess, $"Read failed: {read.Value.Error}");
                Assert.AreEqual(1, read.Value.Items.Count);
                Assert.AreEqual("ntk player round trip", read.Value.Items[0].Text);
            }
            finally
            {
                manager.RemovePasteboard(scope);
            }
        }
    }
}
#endif
#endif
