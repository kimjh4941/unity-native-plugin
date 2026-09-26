#nullable enable

// Windows player only. The native boundary is compiled in only when UNITY_STANDALONE_WIN is set
// and UNITY_EDITOR is not (see WindowsClipboardManager), so this is the one place these calls
// reach the real library. In the Editor every operation would report PlatformUnavailable, which
// WindowsClipboardManagerIntegrationTests already covers there.
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.Collections;
using JonghyunKim.NativeToolkit.Runtime.Clipboard;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// Layer 2b tests (see agent-rules/coding-rules/testing.md): PlayMode tests that run on a
    /// Windows player to verify the P/Invoke path that no other layer reaches.
    /// <para>
    /// Run with <c>-runTests -testPlatform StandaloneWindows64</c>. They use no *ForTests hooks:
    /// those exist only under UNITY_EDITOR, and the point here is the unfaked path.
    /// </para>
    /// <para>
    /// This first test is deliberately small. It establishes three things the Editor cannot:
    /// that the native library loads and answers, that results queued on
    /// <c>UnityMainThreadDispatcher</c> are flushed in a test player, and that the main thread of
    /// a test player can own the clipboard (Initialize fails with WrongApartment otherwise).
    /// On the 1.x ABI it is the baseline the 2.0.0 migration is compared against
    /// (artifact/topics/windows-c-abi-2).
    /// </para>
    /// <para>
    /// The test writes a sample value to the real system clipboard and leaves it there. It is a
    /// sample value, never user data, so plain assertions are safe (testing.md section 6).
    /// </para>
    /// </summary>
    public sealed class WindowsClipboardPlayerTests
    {
        private const float DefaultTimeoutSeconds = 5f;
        private const string SampleText = "NTK-W1N-7F3A-92QX";

        [UnityTest]
        public IEnumerator CopyThenPaste_RoundTripsThroughTheNativeLibrary()
        {
            WindowsClipboardManager manager = WindowsClipboardManager.Instance;

            WindowsClipboardResult initialized = manager.Initialize();
            Assert.IsTrue(initialized.IsSuccess,
                $"Initialize: {initialized.ErrorCode} {initialized.ErrorMessage}");

            WindowsClipboardResult copied = manager.CopyPlainText(SampleText);
            Assert.IsTrue(copied.IsSuccess, $"CopyPlainText: {copied.ErrorCode} {copied.ErrorMessage}");

            // The return value and the callback travel different paths: the first comes straight
            // back from the native call, the second is queued on the dispatcher and needs a later
            // Update. Both are checked, because the second is what a test player might not provide.
            WindowsClipboardTextResult? delivered = null;
            WindowsClipboardTextResult pasted = manager.PastePlainText(result => delivered = result);
            Assert.IsTrue(pasted.IsSuccess, $"PastePlainText: {pasted.ErrorCode} {pasted.ErrorMessage}");
            Assert.AreEqual(SampleText, pasted.Text, "returned by PastePlainText");

            yield return WaitFor(() => delivered != null, "the PastePlainText callback");
            Assert.AreEqual(SampleText, delivered!.Value.Text, "delivered to the PastePlainText callback");

            WindowsClipboardResult? shutdown = null;
            manager.ShutdownWithDrain(result => shutdown = result);
            yield return WaitFor(() => shutdown != null, "ShutdownWithDrain");
            Assert.IsTrue(shutdown!.Value.IsSuccess,
                $"ShutdownWithDrain: {shutdown.Value.ErrorCode} {shutdown.Value.ErrorMessage}");
        }

        private static IEnumerator WaitFor(Func<bool> condition, string what)
        {
            float deadline = Time.realtimeSinceStartup + DefaultTimeoutSeconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > deadline)
                {
                    Assert.Fail($"Timed out after {DefaultTimeoutSeconds}s waiting for {what}.");
                }
                yield return null;
            }
        }
    }
}
#endif
