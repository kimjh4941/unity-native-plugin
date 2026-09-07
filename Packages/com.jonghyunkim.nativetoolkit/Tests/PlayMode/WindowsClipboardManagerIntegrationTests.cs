#nullable enable

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using JonghyunKim.NativeToolkit.Runtime.Clipboard;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// PlayMode integration tests for <c>WindowsClipboardManager</c>.
    /// <para>
    /// These need a running player loop: creating the Manager calls DontDestroyOnLoad, and results
    /// reach their subscribers through <c>UnityMainThreadDispatcher</c>, which only flushes from
    /// Update. EditMode covers the pure classifiers and the guard seam instead.
    /// </para>
    /// <para>
    /// The native boundary is compiled out in the Editor, so every operation that gets past the
    /// guard reports PlatformUnavailable. That is what makes the rejection paths and the delivery
    /// contract testable here without a Windows player.
    /// </para>
    /// </summary>
    public sealed class WindowsClipboardManagerIntegrationTests
    {
        [TearDown]
        public void TearDown()
        {
            // A test that forced the Running state has no native manager behind it, so clear the
            // state before destroying: OnDestroy would otherwise run a shutdown for something that
            // was never initialized.
            WindowsClipboardManager.SetStateForTests(WindowsClipboardManagerState.Uninitialized);

            // Destroy first, then reset: ResetForTests clears the captured main-thread id and the
            // dispatcher, which only Awake re-establishes.
            DestroyManagerIfPresent();
            WindowsClipboardManager.ResetForTests();
        }

        private static void DestroyManagerIfPresent()
        {
            foreach (WindowsClipboardManager manager in
                     Object.FindObjectsByType<WindowsClipboardManager>(FindObjectsInactive.Include))
            {
                Object.DestroyImmediate(manager.gameObject);
            }
        }

        /// <summary>Creates the Manager and puts it into the state operations are accepted in.</summary>
        private static WindowsClipboardManager RunningManager()
        {
            WindowsClipboardManager manager = WindowsClipboardManager.Instance;
            WindowsClipboardManager.SetStateForTests(WindowsClipboardManagerState.Running);
            return manager;
        }

        // ── Rejection paths ──────────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator BeforeInitialize_EveryOperationIsRejectedWithoutReachingTheNativeSide()
        {
            WindowsClipboardManager manager = WindowsClipboardManager.Instance;
            yield return null;

            Assert.AreEqual(WindowsClipboardErrorCode.NotInitializedByHost,
                manager.CopyPlainText("a").ErrorCode);
            Assert.AreEqual(WindowsClipboardErrorCode.NotInitializedByHost,
                manager.PastePlainText().ErrorCode);
            Assert.AreEqual(WindowsClipboardErrorCode.NotInitializedByHost,
                manager.GetFormats().ErrorCode);
            Assert.AreEqual(WindowsClipboardErrorCode.NotInitializedByHost,
                manager.HasFormat("CF_UNICODETEXT").ErrorCode);
        }

        [UnityTest]
        public IEnumerator WhileDraining_OperationsAreRejectedAsShuttingDown()
        {
            WindowsClipboardManager manager = WindowsClipboardManager.Instance;
            WindowsClipboardManager.SetStateForTests(WindowsClipboardManagerState.Draining);
            yield return null;

            Assert.AreEqual(WindowsClipboardErrorCode.ShuttingDown, manager.Clear().ErrorCode);
            Assert.AreEqual(WindowsClipboardErrorCode.ShuttingDown, manager.PasteImage().ErrorCode);
        }

        [UnityTest]
        public IEnumerator ARejectedReadIsNeverReportedAsAnEmptyClipboard()
        {
            WindowsClipboardManager manager = WindowsClipboardManager.Instance;
            yield return null;

            WindowsClipboardTextResult text = manager.PastePlainText();
            WindowsClipboardBytesResult bytes = manager.PasteImage();
            WindowsClipboardStringListResult list = manager.PasteFiles();

            Assert.IsFalse(text.IsSuccess);
            Assert.IsFalse(text.IsEmpty, "a rejection must not look like an empty clipboard");
            Assert.IsFalse(bytes.IsEmpty);
            Assert.IsFalse(list.IsEmpty);
        }

        // ── Argument validation ──────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator ArgumentValidationRunsBeforeTheNativeCallAndNamesTheOffender()
        {
            WindowsClipboardManager manager = RunningManager();
            yield return null;

            Assert.AreEqual(WindowsClipboardErrorCode.InvalidArgument,
                manager.CopyPlainText(null!).ErrorCode);
            Assert.AreEqual(WindowsClipboardErrorCode.InvalidArgument,
                manager.CopyFiles(new string[0]).ErrorCode);
            Assert.AreEqual(WindowsClipboardErrorCode.InvalidArgument,
                manager.CopyImage(new byte[0]).ErrorCode);
            Assert.AreEqual(WindowsClipboardErrorCode.InvalidArgument,
                manager.HasFormat("  ").ErrorCode);

            WindowsClipboardResult blankPath = manager.CopyFiles(new[] { "  " });
            StringAssert.Contains("paths[0]", blankPath.ErrorMessage!);

            WindowsClipboardResult badPayload = manager.CopyMultipleFormats(new[]
            {
                WindowsClipboardFormatPayload.Text("CF_UNICODETEXT", "ok"),
                WindowsClipboardFormatPayload.Bytes("CF_DIB", null!)
            });
            StringAssert.Contains("items[1]", badPayload.ErrorMessage!);
        }

        // ── Editor compilation ───────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator PastTheGuard_TheEditorReportsPlatformUnavailable()
        {
            // Proves the second compile guard did its job: reaching the DLL from the Editor would
            // raise DllNotFoundException instead of returning a result.
            WindowsClipboardManager manager = RunningManager();
            yield return null;

            Assert.AreEqual(WindowsClipboardErrorCode.PlatformUnavailable,
                manager.CopyPlainText("a").ErrorCode);
            Assert.AreEqual(WindowsClipboardErrorCode.PlatformUnavailable,
                manager.PastePlainText().ErrorCode);
            Assert.AreEqual(WindowsClipboardErrorCode.PlatformUnavailable,
                manager.HasFormat("CF_UNICODETEXT").ErrorCode);
            Assert.AreEqual(WindowsClipboardErrorCode.PlatformUnavailable,
                manager.CanShutdownNow().ErrorCode);
        }

        // ── Delivery ─────────────────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator AResultReachesTheCommonEventBeforeThePerCallCallback()
        {
            WindowsClipboardManager manager = RunningManager();
            var order = new List<string>();
            manager.ClipboardOperationCompleted += _ => order.Add("common");
            yield return null;

            manager.CopyPlainText("a", WindowsClipboardWriteOptions.None, _ => order.Add("perCall"));

            // Delivery happens outside the caller's stack, so nothing has arrived yet.
            Assert.AreEqual(0, order.Count);

            yield return null;
            Assert.AreEqual(new[] { "common", "perCall" }, order.ToArray());
        }

        [UnityTest]
        public IEnumerator EachReadKindReachesItsOwnEvent()
        {
            WindowsClipboardManager manager = RunningManager();
            var seen = new List<string>();
            manager.TextReadCompleted += _ => seen.Add("text");
            manager.StringListReadCompleted += _ => seen.Add("list");
            manager.BytesReadCompleted += _ => seen.Add("bytes");
            manager.FormatPresenceChecked += _ => seen.Add("presence");
            manager.FlagChecked += _ => seen.Add("flag");
            yield return null;

            manager.PastePlainText();
            manager.PasteFiles();
            manager.PasteImage();
            manager.HasFormat("CF_UNICODETEXT");
            manager.CanShutdownNow();
            yield return null;

            CollectionAssert.AreEquivalent(
                new[] { "text", "list", "bytes", "presence", "flag" }, seen);
        }

        [UnityTest]
        public IEnumerator ADestroyedManagerRejectsEveryOperation()
        {
            WindowsClipboardManager manager = WindowsClipboardManager.Instance;
            yield return null;

            Object.DestroyImmediate(manager.gameObject);
            yield return null;

            Assert.IsTrue(WindowsClipboardManager.IsTerminated);

            // Recreating the manager after destruction is designed to complain and then reject
            // everything, rather than silently coming back to life.
            LogAssert.Expect(LogType.Error, new Regex("Recreated after destruction"));
            Assert.AreEqual(WindowsClipboardErrorCode.ManagerDestroyed,
                WindowsClipboardManager.Instance.CopyPlainText("a").ErrorCode);
        }

        [UnityTest]
        public IEnumerator ADuplicateManagerDestroysItselfWithoutTerminatingTheLiveOne()
        {
            WindowsClipboardManager manager = RunningManager();
            yield return null;

            var extra = new GameObject("duplicate").AddComponent<WindowsClipboardManager>();
            yield return null;

            // The duplicate removes itself in Awake, and its OnDestroy must not set the tombstone
            // that would make the live singleton reject everything.
            Assert.IsFalse(WindowsClipboardManager.IsTerminated);
            Assert.AreEqual(WindowsClipboardErrorCode.PlatformUnavailable,
                manager.CopyPlainText("a").ErrorCode);
            if (extra != null) Object.DestroyImmediate(extra.gameObject);
        }
    }
}
#endif
