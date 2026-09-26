#nullable enable

// Windows player only. The native boundary is compiled in only when UNITY_STANDALONE_WIN is set
// and UNITY_EDITOR is not (see WindowsClipboardManager), so this is the one place these calls
// reach the real library. In the Editor every operation would report PlatformUnavailable, which
// WindowsClipboardManagerIntegrationTests already covers there.
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
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
    /// Run with <c>-runTests -testPlatform StandaloneWindows64</c>; scripts/verify_unity_windows.sh
    /// does. They use no *ForTests hooks: those exist only under UNITY_EDITOR, and the point here is
    /// the unfaked path.
    /// </para>
    /// <para>
    /// The cases follow the manual checks in
    /// artifact/features/clipboard/results/2026-09-09-windows-clipboard-verify-manual-result-v1.md
    /// rather than inventing new ones, and reuse the sample's values where the sample has them.
    /// On the 1.x ABI they are the baseline the 2.0.0 migration is compared against
    /// (artifact/topics/windows-c-abi-2).
    /// </para>
    /// <para>
    /// Only the round trip writes to the system clipboard, and it writes a sample value, never user
    /// data. verify_unity_windows.sh reads the clipboard afterwards and expects that value, so the
    /// other cases must not write: every block D case is refused before anything is written.
    /// </para>
    /// </summary>
    public sealed class WindowsClipboardPlayerTests
    {
        private const float DefaultTimeoutSeconds = 5f;
        private const string SampleText = "NTK-W1N-7F3A-92QX";

        // The sample's own values for the "unknown id" buttons (WindowsClipboardManagerExampleController).
        private const string UnknownHistoryItemId = "nativetoolkit-sample-no-such-item";
        private const uint UnknownRequestId = uint.MaxValue;

        // ── Round trip ───────────────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator CopyThenPaste_RoundTripsThroughTheNativeLibrary()
        {
            WindowsClipboardManager manager = Running();

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

            yield return ShutDown(manager);
        }

        // ── Block D: rejections ──────────────────────────────────────────────────
        // In the Editor none of the argument checks below can be reached: Initialize reports
        // PlatformUnavailable there, so every call stops at the state guard first. Here the manager
        // is really running, so the argument checks are what answers.

        [Test]
        public void CopyPlainText_Null_IsRejectedAsInvalidArgument()
        {
            WindowsClipboardResult result = Running().CopyPlainText(null!);
            AssertRejected(result.IsSuccess, result.ErrorCode, result.ErrorMessage,
                WindowsClipboardErrorCode.InvalidArgument, "text was null");
        }

        [Test]
        public void CopyFiles_Empty_IsRejectedAsInvalidArgument()
        {
            WindowsClipboardResult result = Running().CopyFiles(Array.Empty<string>());
            AssertRejected(result.IsSuccess, result.ErrorCode, result.ErrorMessage,
                WindowsClipboardErrorCode.InvalidArgument, "paths was null or empty");
        }

        [Test]
        public void CopyCustomFormat_BlankName_IsRejectedAsInvalidArgument()
        {
            WindowsClipboardResult result = Running().CopyCustomFormat("   ", new byte[] { 1 });
            AssertRejected(result.IsSuccess, result.ErrorCode, result.ErrorMessage,
                WindowsClipboardErrorCode.InvalidArgument, "formatName was null or blank");
        }

        [Test]
        public void CopyMultipleFormats_Empty_IsRejectedAsInvalidArgument()
        {
            WindowsClipboardResult result =
                Running().CopyMultipleFormats(new List<WindowsClipboardFormatPayload>());
            AssertRejected(result.IsSuccess, result.ErrorCode, result.ErrorMessage,
                WindowsClipboardErrorCode.InvalidArgument, "items was null or empty");
        }

        [UnityTest]
        public IEnumerator RestoreHistoryItem_BlankId_IsRefusedBeforeARequestIsIssued()
        {
            WindowsClipboardResult? delivered = null;
            uint requestId = Running().RestoreHistoryItem("  ", result => delivered = result);

            Assert.AreEqual(0u, requestId, "a blank id is refused locally, so no request id is issued");
            yield return WaitFor(() => delivered != null, "the RestoreHistoryItem callback");
            AssertRejected(delivered!.Value.IsSuccess, delivered.Value.ErrorCode, delivered.Value.ErrorMessage,
                WindowsClipboardErrorCode.InvalidArgument, "itemId was null or blank");
        }

        [UnityTest]
        public IEnumerator RestoreHistoryItem_UnknownId_ReachesTheNativeLayerAndReportsItemDeleted()
        {
            WindowsClipboardManager manager = Running();

            // The OS answers differently when clipboard history is off (HistoryDisabled), and the
            // manual check was defined with it on. Ask through the product's own API rather than the
            // registry, so the precondition is read the same way the feature reads it.
            WindowsClipboardAvailabilityResult? availability = null;
            manager.GetHistoryAvailability(result => availability = result);
            yield return WaitFor(() => availability != null, "the GetHistoryAvailability callback");
            Assert.IsTrue(availability!.Value.IsSuccess,
                $"GetHistoryAvailability: {availability.Value.ErrorCode} {availability.Value.ErrorMessage}");
            if (!availability.Value.HistoryEnabled)
            {
                Assert.Ignore("Clipboard history (Win+V) is off on this machine; this case needs it on. " +
                              "Settings > System > Clipboard > Clipboard history.");
            }

            WindowsClipboardResult? delivered = null;
            uint requestId = manager.RestoreHistoryItem(UnknownHistoryItemId, result => delivered = result);

            Assert.AreNotEqual(0u, requestId, "an unknown but well-formed id is accepted and sent on");
            yield return WaitFor(() => delivered != null, "the RestoreHistoryItem callback");
            AssertRejected(delivered!.Value.IsSuccess, delivered.Value.ErrorCode, delivered.Value.ErrorMessage,
                WindowsClipboardErrorCode.ItemDeleted, null);
        }

        [Test]
        public void CancelRequest_UnknownId_IsRejectedByTheNativeLayer()
        {
            WindowsClipboardResult result = Running().CancelRequest(UnknownRequestId);
            AssertRejected(result.IsSuccess, result.ErrorCode, result.ErrorMessage,
                WindowsClipboardErrorCode.InvalidParameter, null);
        }

        [UnityTest]
        public IEnumerator CopyPlainText_AfterShutdown_IsRejectedAndShutdownStaysComplete()
        {
            WindowsClipboardManager manager = Running();
            yield return ShutDown(manager);

            WindowsClipboardResult result = manager.CopyPlainText("NTK-AFTER-SHUTDOWN");
            AssertRejected(result.IsSuccess, result.ErrorCode, result.ErrorMessage,
                WindowsClipboardErrorCode.NotInitializedByHost, null);

            WindowsClipboardResult again = manager.TryShutdown(out bool completed);
            Assert.IsTrue(again.IsSuccess, $"TryShutdown after shutdown: {again.ErrorCode} {again.ErrorMessage}");
            Assert.IsTrue(completed, "a second shutdown has nothing left to do");
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the manager initialized for real. Tests share it, and some end with a shutdown, so
        /// every test initializes rather than assuming the state the previous one left.
        /// </summary>
        private static WindowsClipboardManager Running()
        {
            WindowsClipboardManager manager = WindowsClipboardManager.Instance;
            WindowsClipboardResult initialized = manager.Initialize();
            Assert.IsTrue(initialized.IsSuccess,
                $"Initialize: {initialized.ErrorCode} {initialized.ErrorMessage}");
            return manager;
        }

        private static IEnumerator ShutDown(WindowsClipboardManager manager)
        {
            WindowsClipboardResult? shutdown = null;
            manager.ShutdownWithDrain(result => shutdown = result);
            yield return WaitFor(() => shutdown != null, "ShutdownWithDrain");
            Assert.IsTrue(shutdown!.Value.IsSuccess,
                $"ShutdownWithDrain: {shutdown.Value.ErrorCode} {shutdown.Value.ErrorMessage}");
        }

        private static void AssertRejected(bool isSuccess, WindowsClipboardErrorCode actual, string? message,
            WindowsClipboardErrorCode expected, string? detail)
        {
            Assert.IsFalse(isSuccess, $"expected {expected}, got success");
            Assert.AreEqual(expected, actual, message);
            Assert.IsFalse(string.IsNullOrEmpty(message), "every failure carries an ErrorMessage (manual check S-3)");
            if (detail != null)
            {
                StringAssert.Contains(detail, message);
            }
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
