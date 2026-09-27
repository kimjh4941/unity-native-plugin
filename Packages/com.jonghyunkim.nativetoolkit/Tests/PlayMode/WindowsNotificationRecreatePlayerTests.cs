#nullable enable

// Windows player only, like the other tests that drive the sample.
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System.Collections;
using JonghyunKim.NativeToolkit.Runtime.Windows.Notification;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static JonghyunKim.NativeToolkit.Tests.WindowsClipboardSampleScreenDriver;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// The notification manager destroyed and made again in the same process
    /// (artifact/features/notification/designs/2026-09-27-windows-notification-design-v8.md, 7.3):
    /// a new manager starts uninitialized (E-1), an unpackaged Initialize without a name or icon is
    /// refused (E-2), and the next Initialize is a first one again (J-3), after which toasts show.
    /// <para>
    /// In its own category, which verify_unity_windows.sh leaves out of every other run and runs
    /// alone, in its own player, with --include-destructive. Should it fail between destroying the
    /// manager and initializing the new one, the manager stays uninitialized and every later
    /// set-up's Initialize would fail for the same reason; a process of its own keeps that from
    /// taking other tests with it. Destructive as well: it shows a toast and rewrites the app's
    /// registration under HKCU, as every Initialize does.
    /// </para>
    /// </summary>
    [Category(WindowsClipboardPlayerTests.DestructiveCategory)]
    [Category(RecreateCategory)]
    public sealed class WindowsNotificationRecreatePlayerTests
    {
        /// <summary>Read by verify_unity_windows.sh, which excludes it from the main run.</summary>
        public const string RecreateCategory = "RecreatesTheNotificationManager";

        private const string RecreatedTitle = "Recreated manager";

        private readonly WindowsNotificationCenter _center = new();

        [UnitySetUp]
        public IEnumerator LoadTheSample()
        {
            yield return TakeForegroundAndLog(TestContext.CurrentContext.Test.Name);
            yield return LoadAtTopMenu();
            yield return WindowsNotificationSamplePlayerTests.OpenNotificationScreen();

            // As in WindowsNotificationSamplePlayerTests: press until the screen answers.
            string? initialized = null;
            yield return Eventually(() =>
            {
                Press(FindButton("InitializeButton")!);
                initialized = WindowsNotificationSamplePlayerTests.ResultText();
                return initialized?.StartsWith("✓ Initialize") == true;
            }, _ => { });
            StringAssert.StartsWith("✓ Initialize", initialized, "Initialize before the test");

            // Not before Initialize: without a manager the setting reads Unknown (design v8, 8.2).
            Assert.AreEqual(WindowsNotificationSetting.Enabled, WindowsNotificationManager.Instance.GetNotificationSetting(),
                "notifications are off for this PC or this app; turn them on in Settings > System > Notifications " +
                "(Notifications, and Unity NativeToolkit), or every toast test fails with error 2");
        }

        [UnityTearDown]
        public IEnumerator UnloadTheSample()
        {
            _center.Stop();
            WindowsNotificationManager.Instance.RemoveAllNotifications();
            yield return Unload();
        }

        [UnityTest]
        public IEnumerator ANewManager_StartsUninitialized_RefusesAnEmptyName_AndInitializesAgain()
        {
            Object.DestroyImmediate(WindowsNotificationManager.Instance.gameObject);
            WindowsNotificationManager manager = WindowsNotificationManager.Instance;

            string json = "{\"title\":\"" + RecreatedTitle + "\",\"body\":\"Shown by a manager made again.\",\"tag\":\"ntk-recreate\",\"group\":\"ntk-fixture-group\"}";

            // E-1: nothing is initialized yet.
            WindowsNotificationSamplePlayerTests.AssertOneResult(r => manager.ShowNotification(json, r), 1);
            Assert.AreEqual(WindowsNotificationSetting.Unknown, manager.GetNotificationSetting(), "setting before Initialize");

            // E-2: an unpackaged app needs a name and an icon.
            WindowsNotificationSamplePlayerTests.AssertOneResult(r => manager.Initialize(false, "", "", r), 7);

            // J-3: the new manager's Initialize is a first one, with the sample's arguments.
            WindowsNotificationSamplePlayerTests.AssertOneResult(
                r => manager.Initialize(false, Application.productName, WindowsNotificationSamplePlayerTests.IconUri, r), 0);
            Assert.AreEqual(WindowsNotificationSetting.Enabled, manager.GetNotificationSetting(), "setting after Initialize");

            WindowsNotificationSamplePlayerTests.AssertOneResult(r => manager.ShowNotification(json, r), 0);
            yield return _center.Work($"find {RecreatedTitle}");
        }
    }
}
#endif
