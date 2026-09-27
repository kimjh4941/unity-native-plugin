#nullable enable

// Editor only. These tests rest on the native library never being called in the Editor: every
// call returns without reporting anything (design v8 J-4). The PlayMode assembly is also built
// into the player, where the same calls reach the native library; the player's tests are in
// WindowsNotificationSamplePlayerTests.
#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using JonghyunKim.NativeToolkit.Runtime.Windows.Notification;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// PlayMode tests for <c>WindowsNotificationManager</c> in the Editor
    /// (artifact/features/notification/designs/2026-09-27-windows-notification-design-v8.md, 7.2):
    /// nothing reaches the native library, and nothing is reported.
    /// </summary>
    public sealed class WindowsNotificationManagerEditorTests
    {
        /// <summary>Frames to wait for a report that must not come.</summary>
        private const int QuietFrames = 5;

        private WindowsNotificationManager _manager = null!;
        private readonly List<string> _reports = new();

        [SetUp]
        public void SetUp()
        {
            _manager = WindowsNotificationManager.Instance;
            _manager.NotificationOperationCompleted += r => _reports.Add("event " + r.Operation);
            _manager.GetAllNotificationsCompleted += (_, r) => _reports.Add("getAll event " + r.Operation);
            _manager.NotificationInvoked += args => _reports.Add("invoked " + args);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (WindowsNotificationManager manager in
                     Object.FindObjectsByType<WindowsNotificationManager>(FindObjectsInactive.Include))
            {
                Object.DestroyImmediate(manager.gameObject);
            }
        }

        private void Report(WindowsNotificationResult result) => _reports.Add("per-call " + result.Operation);

        private IEnumerator AssertNothingReported()
        {
            for (int i = 0; i < QuietFrames; i++) yield return null;
            CollectionAssert.IsEmpty(_reports);
        }

        [UnityTest]
        public IEnumerator Initialize_ReportsNothing()
        {
            _manager.Initialize(false, "App", "file:///C:/icon.png", Report);
            _manager.Initialize(onResult: Report);
            yield return AssertNothingReported();
        }

        [UnityTest]
        public IEnumerator ShowAndSchedule_ReportNothing_EvenForAPayloadThatIsNotJson()
        {
            _manager.ShowNotification("{\"title\":\"T\"}", Report);
            _manager.ShowNotification("not json", Report);
            _manager.ShowNotification(null!, Report);
            _manager.ScheduleNotification("{\"title\":\"T\"}", 0, Report);
            yield return AssertNothingReported();
        }

        [UnityTest]
        public IEnumerator TheOtherOperations_ReportNothing()
        {
            _manager.CancelScheduledNotification("t", "g", Report);
            _manager.UpdateNotificationProgress("t", "g", 0.5, "50%", "s", 1, Report);
            _manager.SetBadge(1, Report);
            _manager.RemoveNotificationById(1, Report);
            _manager.RemoveNotificationsByTag("t", "g", Report);
            _manager.RemoveAllNotifications(Report);
            _manager.GetAllNotifications((_, r) => Report(r));
            _manager.OpenNotificationSettings(Report);
            yield return AssertNothingReported();
        }

        /// <remarks>
        /// The badge value is checked after the platform: in the Editor even a value below the
        /// lowest glyph reports nothing, rather than 7.
        /// </remarks>
        [UnityTest]
        public IEnumerator SetBadge_BelowTheLowestGlyph_ReportsNothing()
        {
            _manager.SetBadge(-7, Report);
            yield return AssertNothingReported();
        }

        [UnityTest]
        public IEnumerator GetNotificationSetting_IsUnknown()
        {
            Assert.AreEqual(WindowsNotificationSetting.Unknown, _manager.GetNotificationSetting());
            _manager.Initialize(false, "App", "file:///C:/icon.png");
            Assert.AreEqual(WindowsNotificationSetting.Unknown, _manager.GetNotificationSetting());
            yield return AssertNothingReported();
        }
    }
}
#endif
