#nullable enable

#if UNITY_ANDROID
#if !UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using UnityEngine;
using UnityEngine.TestTools;
using JonghyunKim.NativeToolkit.Runtime.Android.Notification;
using static JonghyunKim.NativeToolkit.Tests.AndroidNotificationTestSupport;
using static JonghyunKim.NativeToolkit.Tests.AndroidSampleScreenDriver;
using Ui = JonghyunKim.NativeToolkit.Tests.AndroidUiDriver;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// Layer 2b tests of the Android Notification on a device: the manager, the notification shade,
    /// the permission dialog, the settings screens and the sample screen, with the OS UI worked by the
    /// host-side UI driver. Test IDs (AN-02 to AN-55) and expectations are in
    /// artifact/android/notification/designs/2026-10-12-android-notification-ui-test-plan-v4.md.
    /// </summary>
    /// <remarks>
    /// One fixture, so that the order holds (plan 0): the rows that need a fresh install (chapter 1)
    /// carry [Order] and run first; the others carry none, and each starts by granting the permission
    /// and exact alarms. The bundled NUnit's [Order] cannot go on a class.
    /// <para>
    /// The 1.x values of the rows whose 1.x behaviour differs were recorded on the 1.x bridge on
    /// 2026-10-12 (the plan's 1.x column); the 1.x bridge left with stage 4, so only the C ABI is checked.
    /// </para>
    /// </remarks>
    [Category(AndroidUiDriver.Category)]
    public sealed class AndroidNotificationPlayerTests
    {
        private const string TopMenuNotificationButton = "NotificationFeatureButton";
        private const string ResultLabel = "ResultTextBlock";
        private const string Settings = "com.android.settings";

        // Android 16 opens the app's details and the exact alarm screen in Settings' SPA activity, which
        // Settings starts itself, with no action: the two cannot be told apart by what the UI driver
        // reads (V-25, 2026-10-12). The notification settings screen is an activity of its own.
        private const string SpaScreen = "com.android.settings/.spa.SpaActivity -";

        // One screen command: its 200 s wait, and the rest of the test (plan 0).
        private const int OneScreen = 300000;
        private const int TwoScreens = 500000;

        private static string Ok(string operation) => $"Op|{operation}|True|null";
        private static string Failed(string operation, string message) => $"Op|{operation}|False|{message}";
        private const string NotAllowed = "notifications are not allowed for this app (permission or app notification setting).";

        private AndroidNotificationEvents _events = new();
        private AndroidSharePauseRecorder? _pauses;

        // What the test expects the events to come to: its own calls only (plan 0; the teardown adds its own).
        private int _ops;
        private int _taps;
        private int _received;

        // What the teardown undoes or skips. Kept here, not undone in a finally of the test: when a step
        // the test yields fails, the Test Framework never resumes the test's own code.
        private bool _ordered;
        private bool _sampleLoaded;
        private Thread? _background;
        private CancellationTokenSource? _abandonBackground;
        private float _lastAnswerAt;

        [SetUp]
        public void SetUp()
        {
            if (Application.platform != RuntimePlatform.Android)
                Assert.Ignore("Device-only.");
            Assert.IsTrue(OnCApi, "AndroidNotificationManager has no PendingCallCountForTests: the test seam is gone or renamed");
            _events.Unsubscribe();
            _events = new AndroidNotificationEvents();
            _events.Subscribe();
            _pauses = AndroidSharePauseRecorder.Create();
            _ops = _taps = _received = 0;
            _ordered = false;
            _sampleLoaded = false;
            _lastAnswerAt = 0f;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            var errors = new List<Exception>();

            // 1. What needs no UI: the other thread a test started.
            try
            {
                StopBackgroundCall(errors);
            }
            catch (Exception e)
            {
                errors.Add(e);
            }

            // 2. Requests not collected: a test that collects them all leaves none.
            string? leftRequests = Ui.PendingCount == 0 ? null : $"{Ui.PendingCount} UI driver request(s) were not collected by the test";

            // 3. The UI steps: collect, and unload the sample. A screen left open pauses Unity, so the
            // teardown does not get here before the driver has closed it.
            yield return AndroidShareTestSupport.Safely(Ui.WaitForPending(), errors);
            if (_sampleLoaded)
            {
                yield return AndroidShareTestSupport.Safely(Unload(), errors);
                _sampleLoaded = false;
            }

            // 4. What the test left showing, scheduled or running, after the requests are collected (so
            // a cancel does not take away what the driver is still looking for). Counted. Not for the
            // fresh-install rows: AN-02 to AN-03 have no permission, and what a cancel does then is not
            // designed; AN-05 cancels its own.
            if (!_ordered)
            {
                try
                {
                    Manager.CancelAllNotifications();
                    Manager.CancelAllScheduledNotifications();
                    Manager.StopProgressForegroundService();
                    _ops += 3;
                }
                catch (Exception e)
                {
                    errors.Add(e);
                }
            }

            // 5. The counts, once everything has come, and a little after: from the latest of the last
            // event, the last UI driver answer, and the player's resume (plan 6).
            float deadline = Time.realtimeSinceStartup + TearDownTimeoutSeconds;
            while (!_events.Reached(_ops, _taps, _received) && Time.realtimeSinceStartup < deadline)
                yield return null;
            float since = Math.Max(Math.Max(_pauses?.ResumedAt ?? 0f, _events.LastEventAt ?? 0f), _lastAnswerAt);
            while (Time.realtimeSinceStartup < since + 0.5f)
                yield return null;
            yield return new WaitForSecondsRealtime(0.5f);
            var failures = errors.Where(e => !(e is IgnoreException)).Select(e => e.Message).ToList();
            string? mismatch = _events.Mismatch(_ops, _taps, _received);
            if (mismatch != null) failures.Add(mismatch);
            string? offThread = _events.OffMainThread();
            if (offThread != null) failures.Add(offThread);
            if (leftRequests != null) failures.Add(leftRequests);

            // 6. The C ABI's calls all completed.
            deadline = Time.realtimeSinceStartup + TearDownTimeoutSeconds;
            while (PendingCallCount() != 0 && Time.realtimeSinceStartup < deadline)
                yield return null;
            if (PendingCallCount() != 0)
                failures.Add($"{PendingCallCount()} call(s) still waiting for their completion");

            // 7. Always.
            _events.Unsubscribe();
            yield return null;
            yield return null;
            if (_pauses != null) UnityEngine.Object.Destroy(_pauses.gameObject);
            _pauses = null;

            if (failures.Count > 0) Assert.Fail(string.Join("\n", failures));
            Exception? ignored = errors.FirstOrDefault(e => e is IgnoreException);
            if (ignored != null && TestContext.CurrentContext.Result.Outcome.Status != TestStatus.Failed)
                ExceptionDispatchInfo.Capture(ignored).Throw();
        }

        private void StopBackgroundCall(List<Exception> errors)
        {
            if (_background == null) return;
            Thread thread = _background;
            _background = null;
            _abandonBackground?.Cancel();
            if (!thread.Join(TimeSpan.FromSeconds(30)))
                errors.Add(new AssertionException("the test's thread did not return within 30 s"));
            _abandonBackground?.Dispose();
            _abandonBackground = null;
        }

        // ── 1. A fresh install (plan 1). [Order]: these run first, before any row grants. ──

        [UnityTest, Order(1)]
        public IEnumerator AN02_FreshInstall_Show()
        {
            _ordered = true;
            if (Manager.HasPermission())
                Assert.Fail("not a fresh install: the notification permission is already granted (V-27)");
            Expect(ops: 1);
            Show(Notification(7002, Title("02")));
            yield return ExpectOp(Failed("showNotification", $"Failed to showNotification: {NotAllowed}"));
            yield return StaysAway(7002);
        }

        [UnityTest, Order(2)]
        public IEnumerator AN02b_FreshInstall_Update()
        {
            _ordered = true;
            Expect(ops: 1);
            Manager.UpdateNotification(AndroidNotificationJsonBuilder.BuildNotificationJson(Notification(7002, Title("02b"))));
            yield return ExpectOp(Failed("updateNotification", $"Failed to updateNotification: {NotAllowed}"));
        }

        [UnityTest, Order(3)]
        public IEnumerator AN02c_FreshInstall_SchedulePast()
        {
            _ordered = true;
            Expect(ops: 1);
            Schedule(Notification(7003, Title("02c")), DateTimeOffset.UtcNow.AddMinutes(-1), exact: false);
            yield return ExpectOp(Failed("scheduleNotification", $"Failed to scheduleNotification: {NotAllowed}"));
        }

        [UnityTest, Order(4), Timeout(OneScreen)]
        public IEnumerator AN03_RequestPermission_Deny()
        {
            _ordered = true;
            bool? granted = null;
            yield return SendThen(() => Manager.RequestPermission(g => granted = g), "permission-answer", "deny");
            yield return Until(() => granted.HasValue);
            Assert.AreEqual(false, granted, "the callback");
            Assert.IsFalse(Manager.HasPermission());
        }

        [UnityTest, Order(5), Timeout(OneScreen)]
        public IEnumerator AN04_RequestPermission_Allow()
        {
            _ordered = true;
            bool? granted = null;
            yield return SendThen(() => Manager.RequestPermission(g => granted = g), "permission-answer", "allow");
            yield return Until(() => granted.HasValue);
            Assert.AreEqual(true, granted, "the callback");
            Assert.IsTrue(Manager.HasPermission());
        }

        [UnityTest, Order(6)]
        public IEnumerator AN05_ExactScheduleWithoutTheAlarmPermission()
        {
            _ordered = true;
            if (Manager.CanScheduleExactAlarms())
            {
                Debug.Log("AN-05: CanScheduleExactAlarms was true on a fresh install (V-24); the row is not run");
                yield break;
            }
            Expect(ops: 2);
            Schedule(Notification(7005, Title("05")), DateTimeOffset.UtcNow.AddMinutes(10), exact: true);
            yield return ExpectOp(Ok("scheduleNotification"));
            Assert.IsTrue(Manager.IsNotificationScheduled(7005), "scheduled (as inexact)");
            Manager.CancelScheduledNotification(7005);
            yield return ExpectOp(Ok("cancelScheduledNotification"));
        }

        [UnityTest, Order(7)]
        public IEnumerator AN06_GrantPermissionAndExactAlarms()
        {
            _ordered = true;
            yield return EnsureGranted();
            Assert.IsTrue(Manager.HasPermission(), "the permission");
            Assert.IsTrue(Manager.CanScheduleExactAlarms(), "exact alarms");
        }

        // ── 3. The manager, without the shade (plan 3) ───────────────────────────

        [UnityTest]
        public IEnumerator AN10_Show()
        {
            yield return EnsureGranted();
            Expect(ops: 1);
            Show(Notification(7010, Title("10")));
            Assert.AreEqual(0, _events.Ops.Count, "a result inside the call");
            yield return ExpectOp(Ok("showNotification"));
            yield return Shows(7010);
        }

        [UnityTest]
        public IEnumerator AN11_Update()
        {
            yield return EnsureGranted();
            Expect(ops: 1);
            Manager.UpdateNotification(AndroidNotificationJsonBuilder.BuildNotificationJson(Notification(7011, Title("11"))));
            yield return ExpectOp(Ok("updateNotification"));
        }

        [UnityTest]
        public IEnumerator AN12_Cancel_AndCancelAll()
        {
            yield return EnsureGranted();
            Expect(ops: 4);
            AndroidNotificationPayload tagged = Notification(7012, Title("12a"));
            tagged.tag = "t";
            Show(tagged);
            yield return ExpectOps("showNotification", 1);
            yield return Shows(7012, "t");
            Manager.CancelNotification(7012, "t");
            yield return ExpectOp(Ok("cancelNotification"));
            yield return Goes(7012, "t");
            Show(Notification(7013, Title("12b")));
            yield return ExpectOps("showNotification", 2);
            yield return Shows(7013);
            Manager.CancelAllNotifications();
            yield return ExpectOp(Ok("cancelAllNotifications"));
            yield return Goes(7013);
        }

        [UnityTest]
        public IEnumerator AN13_CreateChannel_AndDelete()
        {
            yield return EnsureGranted();
            Expect(ops: 2);
            Manager.CreateChannel(AndroidNotificationJsonBuilder.BuildChannelJson(new AndroidChannelPayload
            {
                id = "ntk_test_13", name = "NTK 13", importance = 4, description = "NTK channel 13", showBadge = false,
                enableLights = true, lightColor = unchecked((int)0xFF00FF00), enableVibration = true,
                vibrationPattern = new long[] { 0, 100, 50, 100 }, lockscreenVisibility = 0, groupId = "ntk_group_13", groupName = "NTK group 13",
            }));
            yield return ExpectOp(Ok("createChannel"));
            Manager.DeleteChannel("ntk_test_13");
            yield return ExpectOp(Ok("deleteChannel"));
        }

        [UnityTest]
        public IEnumerator AN14_Schedule_IsScheduled_Cancel()
        {
            yield return EnsureGranted();
            Expect(ops: 2);
            Schedule(Notification(7014, Title("14")), DateTimeOffset.UtcNow.AddMinutes(10), exact: false);
            yield return ExpectOp(Ok("scheduleNotification"));
            Assert.IsTrue(Manager.IsNotificationScheduled(7014), "scheduled");
            Manager.CancelScheduledNotification(7014);
            yield return ExpectOp(Ok("cancelScheduledNotification"));
            Assert.IsFalse(Manager.IsNotificationScheduled(7014), "cancelled");
        }

        [UnityTest]
        public IEnumerator AN15_CancelAllScheduled()
        {
            yield return EnsureGranted();
            Expect(ops: 1);
            Manager.CancelAllScheduledNotifications();
            yield return ExpectOp(Ok("cancelAllScheduledNotifications"));
        }

        [UnityTest]
        public IEnumerator AN16_Progress()
        {
            yield return EnsureGranted();
            Expect(ops: 4);
            Manager.StartProgressForegroundService(ProgressJson(7016, 15));
            yield return ExpectOp(Ok("startProgressForegroundService"));
            Manager.UpdateProgressForegroundService(ProgressJson(7016, 50));
            yield return ExpectOp(Ok("updateProgressForegroundService"));
            Manager.CompleteProgressForegroundService(ProgressJson(7016, 100));
            yield return ExpectOp(Ok("completeProgressForegroundService"));
            Manager.StopProgressForegroundService();
            yield return ExpectOp(Ok("stopProgressForegroundService"));
        }

        [UnityTest]
        public IEnumerator AN17_TitleMissing()
        {
            yield return EnsureGranted();
            Expect(ops: 1);
            Show(Notification(7017, "   "));
            yield return ExpectOp(Failed("showNotification", "Invalid argument for showNotification: title is required."));
        }

        [UnityTest]
        public IEnumerator AN18_BrokenJson()
        {
            yield return EnsureGranted();
            Expect(ops: 1);
            Manager.ShowNotification("{\"id\":");
            yield return ExpectOp(Failed("showNotification", "Failed to showNotification: the JSON could not be read."));
        }

        [UnityTest]
        public IEnumerator AN19_OutOfRange()
        {
            yield return EnsureGranted();
            Expect(ops: 2);
            AndroidNotificationPayload payload = Notification(7019, Title("19"));
            payload.groupAlertBehavior = 3;
            Show(payload);
            yield return ExpectOps("showNotification", 1);
            Assert.AreEqual(Failed("showNotification", "Invalid argument for showNotification: Please verify the input parameters."),
                _events.Ops[0]);
            yield return Anchor(7019);
            Assert.IsFalse(IsActive(7019), "nothing shows");
        }

        [UnityTest]
        public IEnumerator AN20_IconNotFound()
        {
            yield return EnsureGranted();
            Expect(ops: 2);
            AndroidNotificationPayload payload = Notification(7020, Title("20"));
            payload.smallIcon = new AndroidNotificationResourcePayload { name = "ntk_no_such_icon", type = "drawable" };
            Show(payload);
            yield return ExpectOps("showNotification", 1);
            Assert.AreEqual(Failed("showNotification", "Failed to showNotification: a resource named in the notification was not found."),
                _events.Ops[0]);
            yield return Anchor(7020);
            Assert.IsFalse(IsActive(7020), "nothing shows");
        }

        [UnityTest]
        public IEnumerator AN21_ProgressMissing()
        {
            yield return EnsureGranted();
            Expect(ops: 1);
            Manager.StartProgressForegroundService(AndroidNotificationJsonBuilder.BuildNotificationJson(Notification(7021, Title("21"))));
            yield return ExpectOp(Failed("startProgressForegroundService",
                "Invalid argument for startProgressForegroundService: progress is required for Progress FGS operations."));
        }

        [UnityTest]
        public IEnumerator AN22_FromAnotherThread()
        {
            yield return EnsureGranted();
            Expect(ops: 1);
            Exception? thrown = null;
            string json = AndroidNotificationJsonBuilder.BuildNotificationJson(Notification(7022, Title("22")));
            var thread = new Thread(() =>
            {
                try
                {
                    Manager.ShowNotification(json);
                }
                catch (Exception e)
                {
                    thrown = e;
                }
            });
            thread.Start();
            thread.Join();
            Assert.IsNull(thrown, thrown?.ToString());
            yield return ExpectOps("showNotification", 1);
            // 1.x could not be called from another thread, as with Share's AS-35 (recorded, 2026-10-12).
            Assert.AreEqual(Ok("showNotification"), _events.Ops[0]);
        }

        [UnityTest]
        public IEnumerator AN23_DeleteChannelNull()
        {
            yield return EnsureGranted();
            Expect(ops: 1);
            // 1.x's Kotlin took a non-null channel ID, threw, and reported the exception (recorded, 2026-10-12).
            Manager.DeleteChannel(null!);
            yield return ExpectOp(Ok("deleteChannel"));
        }

        [UnityTest]
        public IEnumerator AN24_Queries()
        {
            yield return EnsureGranted();
            Assert.IsTrue(Manager.HasPermission(), "HasPermission");
            Assert.IsTrue(Manager.AreNotificationsEnabled(), "AreNotificationsEnabled");
            Assert.IsTrue(Manager.CanScheduleExactAlarms(), "CanScheduleExactAlarms");
            Assert.IsFalse(Manager.IsNotificationScheduled(7024), "IsNotificationScheduled");
        }

        [UnityTest]
        public IEnumerator AN25_Payloads()
        {
            yield return EnsureGranted();
            var immediate = ImmediatePayloads().ToList();
            var scheduled = ScheduledPayloads().ToList();
            Expect(ops: immediate.Count * 2 + scheduled.Count);
            int shows = 0, cancels = 0, schedules = 0;
            foreach ((string name, AndroidNotificationPayload payload) in immediate)
            {
                Show(payload);
                yield return ExpectOps("showNotification", ++shows);
                Assert.AreEqual(Ok("showNotification"), _events.Ops.Last(o => o.StartsWith("Op|showNotification|", StringComparison.Ordinal)), name);
                yield return Shows(payload.id, payload.tag);
                Manager.CancelNotification(payload.id, payload.tag);
                yield return ExpectOps("cancelNotification", ++cancels);
                yield return Goes(payload.id, payload.tag);
            }
            foreach ((string name, AndroidScheduledNotificationEnvelopePayload payload) in scheduled)
            {
                Manager.ScheduleNotification(AndroidNotificationJsonBuilder.BuildScheduledNotificationJson(payload));
                yield return ExpectOps("scheduleNotification", ++schedules);
                Assert.AreEqual(Ok("scheduleNotification"), _events.Ops.Last(o => o.StartsWith("Op|scheduleNotification|", StringComparison.Ordinal)), name);
                Assert.IsTrue(Manager.IsNotificationScheduled(payload.notification.id, payload.notification.tag), name);
            }
            Assert.IsTrue(_events.Ops.All(o => o.Contains("|True|")), _events.Describe());
        }

        // ── 4. The shade and the screens (plan 4) ────────────────────────────────

        [UnityTest, Timeout(OneScreen)]
        public IEnumerator AN30_ShowsInTheShade()
        {
            yield return EnsureGranted();
            Expect(ops: 1);
            string title = Title("30");
            Show(Notification(7030, title));
            yield return ExpectOp(Ok("showNotification"));
            yield return Driver("notification-present", Ui.Base64(title));
        }

        [UnityTest, Timeout(OneScreen)]
        public IEnumerator AN31_TapOpensTheApp()
        {
            yield return EnsureGranted();
            Expect(ops: 1, taps: 1);
            string title = Title("31");
            AndroidNotificationPayload payload = Notification(7031, title);
            payload.launchAppOnTap = true;
            payload.data = new[] { new AndroidNotificationDataEntryPayload { key = "k", value = "v" } };
            yield return ShowAndWait(payload);
            yield return Driver("notification-tap", Ui.Base64(title), "front");
            // 1.x brought no app to the front from a notification, opening or not (recorded, 2026-10-12; design K-1).
            Assert.AreEqual("front", Ui.LastValue, "the tap opened the app");
            yield return ExpectTap($"Tap|{AndroidNotificationManager.ActionBodyTap}|7031|k=v");
        }

        [UnityTest, Timeout(OneScreen)]
        public IEnumerator AN32_TapWithoutOpening()
        {
            yield return EnsureGranted();
            Expect(ops: 1, taps: 1);
            string title = Title("32");
            AndroidNotificationPayload payload = Notification(7032, title);
            payload.launchAppOnTap = false;
            yield return ShowAndWait(payload);
            yield return Driver("notification-tap", Ui.Base64(title), "front");
            Assert.AreEqual("not-front", Ui.LastValue, "the tap did not open the app");
            yield return ExpectTap($"Tap|{AndroidNotificationManager.ActionBodyTap}|7032|", prefix: true);
        }

        [UnityTest, Timeout(OneScreen)]
        public IEnumerator AN33_ActionWithoutOpening()
        {
            yield return EnsureGranted();
            Expect(ops: 1, taps: 1);
            string title = Title("33");
            string suffix = title.Substring(title.Length - 8);
            yield return ShowAndWait(TwoActions(7033, title, suffix));
            yield return Driver("notification-action", Ui.Base64(title), Ui.Base64($"Later-{suffix}"), "front");
            Assert.AreEqual("not-front", Ui.LastValue, "the action did not open the app");
            yield return ExpectTap("Tap|later|7033|", prefix: true);
        }

        [UnityTest, Timeout(OneScreen)]
        public IEnumerator AN34_ActionOpensTheApp()
        {
            yield return EnsureGranted();
            Expect(ops: 1, taps: 1);
            string title = Title("34");
            string suffix = title.Substring(title.Length - 8);
            yield return ShowAndWait(TwoActions(7034, title, suffix));
            yield return Driver("notification-action", Ui.Base64(title), Ui.Base64($"Play-{suffix}"), "front");
            Assert.AreEqual("front", Ui.LastValue, "the action opened the app");
            yield return ExpectTap("Tap|play|7034|", prefix: true);
        }

        [UnityTest, Timeout(OneScreen)]
        public IEnumerator AN35_Dismiss()
        {
            yield return EnsureGranted();
            Expect(ops: 1, taps: 1);
            string title = Title("35");
            yield return ShowAndWait(Notification(7035, title));
            yield return Driver("notification-dismiss", Ui.Base64(title));
            yield return ExpectTap($"Tap|{AndroidNotificationManager.ActionNotificationDismissed}|7035|", prefix: true);
            yield return Goes(7035);
        }

        [UnityTest, Timeout(OneScreen)]
        public IEnumerator AN36_FullScreen()
        {
            yield return EnsureGranted();
            Expect(ops: 1);
            string title = Title("36");
            AndroidNotificationPayload payload = Notification(7036, title);
            payload.fullScreenIntent = true;
            payload.priority = 2;
            payload.category = "call";
            Show(payload);
            yield return ExpectOp(Ok("showNotification"));
            yield return Driver("notification-present", Ui.Base64(title));
        }

        [UnityTest, Timeout(OneScreen)]
        public IEnumerator AN37_ScheduledFires()
        {
            yield return EnsureGranted();
            Expect(ops: 1, received: 1);
            string title = Title("37");
            AndroidNotificationPayload payload = Notification(7037, title);
            payload.tag = "t37";
            Schedule(payload, DateTimeOffset.UtcNow.AddSeconds(5), exact: true);
            yield return ExpectOp(Ok("scheduleNotification"));
            yield return new WaitForSecondsRealtime(10f);
            yield return Driver("notification-present", Ui.Base64(title));
            yield return Until(() => _events.Received.Count >= 1);
            Assert.AreEqual($"Recv|7037|t37|{ChannelId}", _events.Received.FirstOrDefault(), _events.Describe());
        }

        [UnityTest]
        public IEnumerator AN38_CancelWithTag()
        {
            yield return EnsureGranted();
            Expect(ops: 2);
            AndroidNotificationPayload payload = Notification(7038, Title("38"));
            payload.tag = "t";
            Show(payload);
            yield return ExpectOp(Ok("showNotification"));
            yield return Shows(7038, "t");
            Manager.CancelNotification(7038, "t");
            yield return ExpectOp(Ok("cancelNotification"));
            yield return Goes(7038, "t");
        }

        [UnityTest, Timeout(OneScreen)]
        public IEnumerator AN39_DataRoundTrip()
        {
            yield return EnsureGranted();
            Expect(ops: 1, taps: 1);
            string title = Title("39");
            AndroidNotificationPayload payload = Notification(7039, title);
            var data = new Dictionary<string, string> { ["quote"] = "a\"b", ["slash"] = "c/d", ["jp"] = "日本語の値" };
            payload.data = data.Select(p => new AndroidNotificationDataEntryPayload { key = p.Key, value = p.Value }).ToArray();
            yield return ShowAndWait(payload);
            yield return Driver("notification-tap", Ui.Base64(title), "stay");
            yield return Until(() => _events.TapResults.Count >= 1);
            AndroidNotificationActionResult tap = _events.TapResults.First();
            Assert.IsNotNull(tap.Data, "the data");
            // 1.x handed the values over as they were escaped in its JSON (recorded, 2026-10-12; design 5.3).
            CollectionAssert.AreEquivalent(data, tap.Data!.ToDictionary(p => p.Key, p => p.Value), _events.Describe());
        }

        [UnityTest, Timeout(OneScreen)]
        public IEnumerator AN40_NotificationSettings() => SettingsScreen(Manager.OpenNotificationSettings, "openNotificationSettings",
            // 1.x fell back to the app's details (design 5.3's open question; recorded, 2026-10-12).
            $"{Settings}/.Settings$AppNotificationSettingsActivity android.settings.APP_NOTIFICATION_SETTINGS", true);

        [UnityTest, Timeout(OneScreen)]
        public IEnumerator AN41_AppDetailsSettings() => SettingsScreen(Manager.OpenAppDetailsSettings, "openAppDetailsSettings", SpaScreen, false);

        [UnityTest, Timeout(OneScreen)]
        public IEnumerator AN42_ExactAlarmSettings() => SettingsScreen(Manager.OpenExactAlarmSettings, "openExactAlarmSettings", SpaScreen, true);

        [UnityTest]
        public IEnumerator AN43_SettingsInTheBackground()
        {
            yield return EnsureGranted();
            _ = Manager;
            Expect(ops: 1);
            int number = Ui.NextNumber;
            string? threadError = null;
            var abandonSource = new CancellationTokenSource();
            CancellationToken abandon = abandonSource.Token;
            var thread = new Thread(() =>
            {
                try
                {
                    if (!Ui.WaitForAnswerFile(number, TimeSpan.FromSeconds(20), abandon))
                    {
                        threadError = "the UI driver did not report the app stopped";
                        return;
                    }
                    if (abandon.IsCancellationRequested) return;
                    Manager.OpenNotificationSettings();
                }
                catch (Exception e)
                {
                    threadError = e.ToString();
                }
            });
            thread.Start();
            // Kept only once started: the teardown stops the thread, also when this test fails.
            _abandonBackground = abandonSource;
            _background = thread;
            yield return Driver("background", "3000");
            yield return Until(() => !thread.IsAlive);
            Assert.IsFalse(thread.IsAlive, "the thread did not finish");
            Assert.IsNull(threadError, threadError);
            yield return ExpectOps("openNotificationSettings", 1);
            // 1.x could not be called from another thread (recorded, 2026-10-12).
            Assert.AreEqual(Failed("openNotificationSettings", "Failed to openNotificationSettings: the app is not in the foreground."),
                _events.Ops[0]);
        }

        // ── 5. The sample screen (plan 5) ────────────────────────────────────────

        [UnityTest, Category(AndroidUiDriver.SampleScreenCategory), Timeout(OneScreen)]
        public IEnumerator AN50_SampleShowUpdateCancel()
        {
            yield return EnsureGranted();
            yield return OpenSample();
            Expect(ops: 3);
            Press(FindButton("ShowNotificationButton")!);
            yield return ExpectLabel("ShowNotification: Success\nShowing notification 1101: Energy Refilled");
            yield return Driver("notification-present", Ui.Base64("Energy Refilled"));
            Press(FindButton("UpdateNotificationButton")!);
            yield return ExpectLabel("UpdateNotification: Success\nUpdating notification 1101: Daily Reward Ready");
            Press(FindButton("CancelNotificationButton")!);
            yield return ExpectLabel("CancelNotification: Success\nCancelling notification 1101 with tag energy");
        }

        [UnityTest, Category(AndroidUiDriver.SampleScreenCategory)]
        public IEnumerator AN51_SampleSchedule()
        {
            yield return EnsureGranted();
            yield return OpenSample();
            Expect(ops: 2);
            // The sample schedules 15 s ahead: cancel well before it fires.
            Press(FindButton("ScheduleNotificationButton")!);
            yield return ExpectLabelStart("ScheduleNotification: Success\n");
            Press(FindButton("IsScheduledNotificationButton")!);
            Assert.AreEqual("IsNotificationScheduled(1201, guild-battle): True", LabelText(ResultLabel));
            Press(FindButton("CancelScheduledNotificationButton")!);
            yield return ExpectLabel("CancelScheduledNotification: Success\nCancelling scheduled notification 1201 with tag guild-battle");
        }

        [UnityTest, Category(AndroidUiDriver.SampleScreenCategory)]
        public IEnumerator AN52_SampleProgress()
        {
            yield return EnsureGranted();
            yield return OpenSample();
            Expect(ops: 4);
            Press(FindButton("StartProgressButton")!);
            yield return ExpectLabel("StartProgressForegroundService: Success\nStarting foreground progress notification 1301 at 15%.");
            Press(FindButton("UpdateProgressButton")!);
            yield return ExpectLabel("UpdateProgressForegroundService: Success\nUpdating foreground progress notification 1301 to 50%.");
            Press(FindButton("CompleteProgressButton")!);
            yield return ExpectLabel("CompleteProgressForegroundService: Success\nCompleting foreground progress notification 1301.");
            Press(FindButton("StopProgressButton")!);
            yield return ExpectLabel("StopProgressForegroundService: Success\nStopping foreground progress notification 1301.");
        }

        [UnityTest, Category(AndroidUiDriver.SampleScreenCategory), Timeout(OneScreen)]
        public IEnumerator AN53_SampleActionButton()
        {
            yield return EnsureGranted();
            yield return OpenSample();
            Expect(ops: 1, taps: 1);
            Press(FindButton("ShowActionNotificationButton")!);
            yield return ExpectLabel("ShowNotification: Success\nShowing match alert 1401 with action buttons.");
            yield return Driver("notification-action", Ui.Base64("Match Found"), Ui.Base64("Dismiss"), "stay");
            yield return ExpectLabelStart("Notification Action Button\nNotification ID: 1401\nAction: com.jonghyunkim.nativetoolkit.ACTION_DISMISS");
        }

        [UnityTest, Category(AndroidUiDriver.SampleScreenCategory)]
        public IEnumerator AN54_SampleFullScreenAndCustomView()
        {
            yield return EnsureGranted();
            yield return OpenSample();
            Expect(ops: 4);
            Press(FindButton("ShowFullScreenNotificationButton")!);
            yield return ExpectLabel("ShowNotification: Success\nShowing full-screen match start notification 1501.");
            Press(FindButton("CancelAllNotificationsButton")!);
            yield return ExpectLabel("CancelAllNotifications: Success\nCancelling all visible gameplay notifications.");
            Press(FindButton("ShowDecoratedCustomViewButton")!);
            yield return ExpectLabel("ShowNotification: Success\nShowing custom view notification 1601.");
            Press(FindButton("CancelAllNotificationsButton")!);
            yield return ExpectLabel("CancelAllNotifications: Success\nCancelling all visible gameplay notifications.");
        }

        [UnityTest, Category(AndroidUiDriver.SampleScreenCategory)]
        public IEnumerator AN55_SampleQueries()
        {
            yield return EnsureGranted();
            yield return OpenSample();
            Press(FindButton("HasPermissionButton")!);
            Assert.AreEqual("HasPermission: True", LabelText(ResultLabel));
            Press(FindButton("NotificationsEnabledButton")!);
            Assert.AreEqual("AreNotificationsEnabled: True", LabelText(ResultLabel));
            Press(FindButton("CanScheduleExactAlarmsButton")!);
            Assert.AreEqual("CanScheduleExactAlarms: True", LabelText(ResultLabel));
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private static AndroidNotificationManager Manager => AndroidNotificationManager.Instance;

        private void Expect(int ops = 0, int taps = 0, int received = 0)
        {
            _ops = ops;
            _taps = taps;
            _received = received;
        }

        private static void Show(AndroidNotificationPayload payload) =>
            Manager.ShowNotification(AndroidNotificationJsonBuilder.BuildNotificationJson(payload));

        private static void Schedule(AndroidNotificationPayload payload, DateTimeOffset at, bool exact) =>
            Manager.ScheduleNotification(AndroidNotificationJsonBuilder.BuildScheduledNotificationJson(new AndroidScheduledNotificationEnvelopePayload
            {
                notification = payload,
                // persistAcrossBoot: IsNotificationScheduled sees only the schedules kept for a reboot, in
                // 1.x and in 2.0.0 alike (native-toolkit's NotificationRepositoryImpl.schedule stores
                // nothing else; found on the device, 2026-10-12).
                schedule = new AndroidNotificationSchedulePayload { triggerAtMillis = at.ToUnixTimeMilliseconds(), exact = exact, allowWhileIdle = true, persistAcrossBoot = true },
            }));

        private static string ProgressJson(int id, int current)
        {
            AndroidNotificationPayload payload = Notification(id, "NTK progress");
            payload.ongoing = current < 100;
            payload.autoCancel = current >= 100;
            payload.progress = new AndroidNotificationProgressPayload { max = 100, current = current };
            return AndroidNotificationJsonBuilder.BuildNotificationJson(payload);
        }

        private static AndroidNotificationPayload TwoActions(int id, string title, string suffix)
        {
            AndroidNotificationPayload payload = Notification(id, title);
            payload.actions = new[]
            {
                new AndroidNotificationActionPayload { title = $"Play-{suffix}", actionId = "play", launchApp = true, showsUserInterface = true },
                new AndroidNotificationActionPayload { title = $"Later-{suffix}", actionId = "later", launchApp = false, showsUserInterface = false },
            };
            return payload;
        }

        private IEnumerator ShowAndWait(AndroidNotificationPayload payload)
        {
            Show(payload);
            yield return ExpectOp(Ok("showNotification"));
            yield return Shows(payload.id, payload.tag);
        }

        /// <summary>
        /// The anchor (plan 0): a notification with another ID, shown after the call that must show
        /// nothing; once it shows, what that call would have shown has shown too (V-29). Counted.
        /// </summary>
        private IEnumerator Anchor(int id)
        {
            int anchor = id + 50000;
            int before = _events.Ops.Count(o => o.StartsWith("Op|showNotification|", StringComparison.Ordinal));
            Show(Notification(anchor, Title("anchor")));
            yield return ExpectOps("showNotification", before + 1);
            Assert.AreEqual(Ok("showNotification"), _events.Ops.Last(), "the anchor");
            yield return Shows(anchor);
        }

        /// <summary>Grants the permission and exact alarms through the UI driver; any number of times (plan 0).</summary>
        private IEnumerator EnsureGranted()
        {
            yield return Driver("grant", "android.permission.POST_NOTIFICATIONS");
            yield return Driver("appop-allow", "SCHEDULE_EXACT_ALARM");
        }

        /// <summary>A UI driver request and its answer, noting when the answer came (plan 6).</summary>
        private IEnumerator Driver(string command, params string[] args)
        {
            yield return Ui.Do(command, args);
            _lastAnswerAt = Time.realtimeSinceStartup;
        }

        /// <summary>
        /// Sends the UI driver request, makes the call in the same frame (the dialog or screen pauses
        /// Unity as soon as it shows), and waits for the answer.
        /// </summary>
        private IEnumerator SendThen(Action call, string command, params string[] args)
        {
            yield return Ui.Send(command, args);
            int number = Ui.LastSent;
            call();
            yield return Ui.Wait(number, Ui.AnswerTimeoutFor(command));
            _lastAnswerAt = Time.realtimeSinceStartup;
        }

        private IEnumerator SettingsScreen(Action open, string operation, string screen, bool checkOpened)
        {
            yield return EnsureGranted();
            _ = Manager;
            Expect(ops: 1);
            yield return SendThen(open, "close-screen", Ui.Base64(Settings));
            Assert.AreEqual(screen, Ui.LastValue, "the screen that opened");
            yield return ExpectOp(Ok(operation));
            // The exact alarm screen and the app's details look the same to the UI driver: the
            // manager's record tells whether it fell back (plan AN-40, AN-42).
            if (checkOpened)
                Assert.AreEqual(0, LastSettingsResult(), "OPENED, not OPENED_FALLBACK");
        }

        private IEnumerator OpenSample()
        {
            if (!SampleSceneIsInThePlayer())
                Assert.Ignore("The sample scene is not in this player (the Test Android™ Profile builds the test scene only).");
            _sampleLoaded = true;
            yield return LoadAtTopMenu(TopMenuNotificationButton);
            yield return Open(TopMenuNotificationButton, "ShowNotificationButton");
        }

        private static IEnumerator Until(Func<bool> condition, float timeoutSeconds = EventTimeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline)
                yield return null;
        }

        private IEnumerator ExpectOp(string expected)
        {
            yield return Until(() => _events.Ops.Contains(expected));
            Assert.That(_events.Ops, Does.Contain(expected), _events.Describe());
        }

        private IEnumerator ExpectOps(string operation, int count)
        {
            yield return Until(() => _events.Ops.Count(o => o.StartsWith($"Op|{operation}|", StringComparison.Ordinal)) >= count);
            Assert.GreaterOrEqual(_events.Ops.Count(o => o.StartsWith($"Op|{operation}|", StringComparison.Ordinal)), count, _events.Describe());
        }

        private IEnumerator ExpectTap(string expected, bool prefix = false)
        {
            bool Seen() => _events.Taps.Any(t => prefix ? t.StartsWith(expected, StringComparison.Ordinal) : t == expected);
            yield return Until(Seen);
            Assert.IsTrue(Seen(), $"expected {expected}{(prefix ? "..." : "")}: {_events.Describe()}");
        }

        private static IEnumerator ExpectLabel(string expected)
        {
            yield return Until(() => LabelText(ResultLabel) == expected);
            Assert.AreEqual(expected, LabelText(ResultLabel), "what the sample screen shows");
        }

        private static IEnumerator ExpectLabelStart(string expected)
        {
            yield return Until(() => LabelText(ResultLabel)?.StartsWith(expected, StringComparison.Ordinal) == true);
            StringAssert.StartsWith(expected, LabelText(ResultLabel) ?? "", "what the sample screen shows");
        }
    }
}
#endif
#endif
