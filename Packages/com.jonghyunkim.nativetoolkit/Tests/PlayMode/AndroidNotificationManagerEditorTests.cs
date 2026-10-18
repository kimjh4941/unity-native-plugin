#nullable enable

// Editor only: in a player the manager reaches the native library and the real notifications.
#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using JonghyunKim.NativeToolkit.Runtime.Android.Common;
using JonghyunKim.NativeToolkit.Runtime.Android.Notification;
using Api = JonghyunKim.NativeToolkit.Runtime.Android.Notification.AndroidNotificationCApi;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// Layer 2a tests of <see cref="AndroidNotificationManager"/> in the Editor
    /// (artifact/android/notification/designs/2026-10-12-android-notification-design-v4.md, 7.2): the
    /// Editor's "not on Android" results, delivery through <see cref="AndroidNativeCallbackQueue"/> to
    /// the manager alive then, the three event paths, the settings completions, the schedule retry, the
    /// listener registrations, and the event handles freed once, through the manager's test seams.
    /// </summary>
    public sealed class AndroidNotificationManagerEditorTests
    {
        private readonly List<string> _events = new();

        [SetUp]
        public void SetUp()
        {
            _events.Clear();
            DestroyManagers();
            AndroidNativeCallbackQueue.ResetForTests();
            AndroidNotificationManager.ResetStaticStateForTests();
        }

        [TearDown]
        public void TearDown()
        {
            DestroyManagers();
            AndroidNativeCallbackQueue.ResetForTests();
            AndroidNotificationManager.ResetStaticStateForTests();
        }

        private static void DestroyManagers()
        {
            foreach (var manager in UnityEngine.Object.FindObjectsByType<AndroidNotificationManager>(FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(manager.gameObject);
        }

        private static int ManagerCount() =>
            UnityEngine.Object.FindObjectsByType<AndroidNotificationManager>(FindObjectsSortMode.None).Length;

        private AndroidNotificationManager Manager()
        {
            var manager = AndroidNotificationManager.Instance;
            manager.NotificationOperationCompleted += r => _events.Add($"Op|{r.Operation}|{r.IsSuccess}|{r.ErrorMessage ?? "null"}");
            manager.NotificationActionTapped += r => _events.Add($"Tap|{r.ActionId}|{r.NotificationId}|{Describe(r.Data)}");
            manager.NotificationReceived += r => _events.Add($"Recv|{r.NotificationId}|{r.Tag ?? "null"}|{r.ChannelId}");
            return manager;
        }

        private static string Describe(IReadOnlyDictionary<string, string>? data)
        {
            if (data == null) return "null";
            var entries = new List<string>();
            foreach (KeyValuePair<string, string> entry in data) entries.Add($"{entry.Key}={entry.Value}");
            entries.Sort(StringComparer.Ordinal);
            return string.Join(",", entries);
        }

        /// <summary>Lets the callback queue run (it runs on the frames after an action is posted).</summary>
        private static IEnumerator Frames(int count = 3)
        {
            for (int i = 0; i < count; i++) yield return null;
        }

        private static string NotStarted(string op) => $"Op|{op}|False|{op} could not be started.";

        private static readonly List<KeyValuePair<string, string>> NoData = new();

        // ── The Editor (J-N16) ───────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator Editor_EveryOperationFailsAfterTheCall()
        {
            var manager = Manager();
            const string json = "{\"id\":1,\"title\":\"t\",\"message\":\"m\",\"channel\":{\"id\":\"c\",\"name\":\"C\"}}";
            var actions = new (string op, Action call)[]
            {
                (AndroidNotificationManager.OperationOpenNotificationSettings, manager.OpenNotificationSettings),
                (AndroidNotificationManager.OperationOpenAppDetailsSettings, manager.OpenAppDetailsSettings),
                (AndroidNotificationManager.OperationOpenExactAlarmSettings, manager.OpenExactAlarmSettings),
                (AndroidNotificationManager.OperationCreateChannel, () => manager.CreateChannel("{\"id\":\"c\",\"name\":\"C\"}")),
                (AndroidNotificationManager.OperationDeleteChannel, () => manager.DeleteChannel("c")),
                (AndroidNotificationManager.OperationShowNotification, () => manager.ShowNotification(json)),
                (AndroidNotificationManager.OperationUpdateNotification, () => manager.UpdateNotification(json)),
                (AndroidNotificationManager.OperationCancelNotification, () => manager.CancelNotification(1, "t")),
                (AndroidNotificationManager.OperationCancelAllNotifications, manager.CancelAllNotifications),
                (AndroidNotificationManager.OperationScheduleNotification, () => manager.ScheduleNotification("{\"notification\":" + json + ",\"schedule\":{\"triggerAtMillis\":1}}")),
                (AndroidNotificationManager.OperationCancelScheduledNotification, () => manager.CancelScheduledNotification(1)),
                (AndroidNotificationManager.OperationCancelAllScheduledNotifications, manager.CancelAllScheduledNotifications),
                (AndroidNotificationManager.OperationStartProgressForegroundService, () => manager.StartProgressForegroundService(json)),
                (AndroidNotificationManager.OperationUpdateProgressForegroundService, () => manager.UpdateProgressForegroundService(json)),
                (AndroidNotificationManager.OperationCompleteProgressForegroundService, () => manager.CompleteProgressForegroundService(json)),
                (AndroidNotificationManager.OperationStopProgressForegroundService, manager.StopProgressForegroundService),
            };
            foreach (var (op, call) in actions)
            {
                _events.Clear();
                call();
                Assert.IsEmpty(_events, $"{op}: an event came inside the call");
                yield return Frames();
                Assert.AreEqual(new[] { NotStarted(op) }, _events, op);
            }
        }

        [UnityTest]
        public IEnumerator Editor_TheJsonIsNotReadBeforeThePlatformIsChecked()
        {
            var manager = Manager();
            manager.ShowNotification("{not json");
            manager.ScheduleNotification(null!);
            manager.CreateChannel("");
            manager.StartProgressForegroundService("{}");
            yield return Frames();
            Assert.AreEqual(new[]
            {
                NotStarted(AndroidNotificationManager.OperationShowNotification),
                NotStarted(AndroidNotificationManager.OperationScheduleNotification),
                NotStarted(AndroidNotificationManager.OperationCreateChannel),
                NotStarted(AndroidNotificationManager.OperationStartProgressForegroundService),
            }, _events);
        }

        [UnityTest]
        public IEnumerator Editor_QueriesAreFalseAndRequestPermissionDoesNothing()
        {
            var manager = Manager();
            Assert.IsFalse(manager.HasPermission());
            Assert.IsFalse(manager.AreNotificationsEnabled());
            Assert.IsFalse(manager.CanScheduleExactAlarms());
            Assert.IsFalse(manager.IsNotificationScheduled(1, "t"));
            bool called = false;
            manager.RequestPermission(_ => called = true);
            yield return Frames();
            Assert.IsFalse(called, "RequestPermission's callback");
            Assert.IsEmpty(_events);
        }

        // ── Delivery (J-N3) ──────────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator Delivery_ManagerDestroyedBeforeTheQueueRuns_DropsAndCreatesNone()
        {
            var manager = Manager();
            manager.CancelAllNotifications();
            UnityEngine.Object.DestroyImmediate(manager.gameObject);
            yield return Frames();
            Assert.IsEmpty(_events);
            Assert.AreEqual(0, ManagerCount(), "no manager was created to deliver to");
        }

        [UnityTest]
        public IEnumerator Delivery_ManagerRecreated_GetsWhatWasQueuedBefore()
        {
            var first = AndroidNotificationManager.Instance;
            first.CancelAllNotifications();
            UnityEngine.Object.DestroyImmediate(first.gameObject);
            Manager();
            yield return Frames();
            Assert.AreEqual(new[] { NotStarted(AndroidNotificationManager.OperationCancelAllNotifications) }, _events);
        }

        // ── Events (J-N13, J-N14) ────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator Interaction_EachKindReachesTheApp()
        {
            Manager();
            AndroidNotificationManager.OnInteractionCore(Api.KindBodyTap, 7, null, new List<KeyValuePair<string, string>> { new("k", "v") });
            AndroidNotificationManager.OnInteractionCore(Api.KindAction, 8, "play", NoData);
            AndroidNotificationManager.OnInteractionCore(Api.KindDismiss, 9, null, NoData);
            Assert.IsEmpty(_events, "nothing inside the callback");
            yield return Frames();
            Assert.AreEqual(new[]
            {
                $"Tap|{AndroidNotificationManager.ActionBodyTap}|7|k=v",
                "Tap|play|8|null",
                $"Tap|{AndroidNotificationManager.ActionNotificationDismissed}|9|null",
            }, _events);
        }

        [UnityTest]
        public IEnumerator Interaction_DataKeepsTheValuesAsRead()
        {
            Manager();
            AndroidNotificationManager.OnInteractionCore(Api.KindBodyTap, 1, null, new List<KeyValuePair<string, string>>
            {
                new("quote", "a\"b"), new("slash", "c/d"), new("jp", "日本語"), new("quote", "last"),
            });
            yield return Frames();
            Assert.AreEqual(new[] { $"Tap|{AndroidNotificationManager.ActionBodyTap}|1|jp=日本語,quote=last,slash=c/d" }, _events);
        }

        [UnityTest]
        public IEnumerator Interaction_UnknownKindOrActionWithoutId_IsDropped()
        {
            Manager();
            AndroidNotificationManager.OnInteractionCore(3, 1, "x", NoData);
            AndroidNotificationManager.OnInteractionCore(Api.KindAction, 2, null, NoData);
            AndroidNotificationManager.OnInteractionCore(Api.KindAction, 3, "", NoData);
            yield return Frames();
            Assert.IsEmpty(_events);
        }

        [UnityTest]
        public IEnumerator Interaction_WithoutAManager_IsDropped()
        {
            AndroidNativeCallbackQueue.EnsureCreated();
            AndroidNotificationManager.OnInteractionCore(Api.KindBodyTap, 1, null, NoData);
            yield return Frames();
            Assert.AreEqual(0, ManagerCount(), "no manager was created to deliver to");
        }

        [UnityTest]
        public IEnumerator Shown_AnEmptyTagIsNone()
        {
            Manager();
            AndroidNotificationManager.OnShownCore(1, "", "c");
            AndroidNotificationManager.OnShownCore(2, "t", "c");
            AndroidNotificationManager.OnShownCore(3, null, null);
            yield return Frames();
            Assert.AreEqual(new[] { "Recv|1|null|c", "Recv|2|t|c", "Recv|3|null|" }, _events);
        }

        // ── Settings completions (J-N9) ──────────────────────────────────────────

        [UnityTest]
        public IEnumerator Settings_OpenedAndFallbackSucceed_OthersFailAsWorded()
        {
            Manager();
            AndroidNotificationManager.OnSettingsCore(Api.TargetNotifications, Api.ErrorNone, Api.ResultOpened);
            Assert.AreEqual(0, AndroidNotificationManager.LastSettingsResultForTests);
            AndroidNotificationManager.OnSettingsCore(Api.TargetAppDetails, Api.ErrorNone, Api.ResultOpenedFallback);
            Assert.AreEqual(1, AndroidNotificationManager.LastSettingsResultForTests);
            AndroidNotificationManager.OnSettingsCore(Api.TargetExactAlarm, Api.ErrorNotForeground, 0);
            Assert.IsNull(AndroidNotificationManager.LastSettingsResultForTests, "a failure leaves no result");
            AndroidNotificationManager.OnSettingsCore(Api.TargetNotifications, Api.ErrorSettingsNotOpened, 0);
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("unknown target 7"));
            AndroidNotificationManager.OnSettingsCore(7, Api.ErrorNone, Api.ResultOpened);
            yield return Frames();
            Assert.AreEqual(new[]
            {
                "Op|openNotificationSettings|True|null",
                "Op|openAppDetailsSettings|True|null",
                "Op|openExactAlarmSettings|False|Failed to openExactAlarmSettings: the app is not in the foreground.",
                "Op|openNotificationSettings|False|No Activity found to handle openNotificationSettings. Please check device settings support.",
            }, _events, "an unknown target is dropped");
        }

        [UnityTest]
        public IEnumerator Settings_ACallIsCountedUntilItsCompletion()
        {
            Manager();
            AndroidNotificationManager.OpenSettingsCore(AndroidNotificationManager.OperationOpenNotificationSettings, () =>
            {
                Assert.AreEqual(1, AndroidNotificationManager.PendingCallCountForTests, "counted before the call");
                return Api.ErrorNone;
            });
            Assert.AreEqual(1, AndroidNotificationManager.PendingCallCountForTests, "waiting for its completion");
            AndroidNotificationManager.OnSettingsCore(Api.TargetNotifications, Api.ErrorNone, Api.ResultOpened);
            Assert.AreEqual(0, AndroidNotificationManager.PendingCallCountForTests, "completed");
            yield return Frames();
            Assert.AreEqual(new[] { "Op|openNotificationSettings|True|null" }, _events);
        }

        [UnityTest]
        public IEnumerator Settings_ARefusedOrThrowingCallIsNotCounted_AndFailsAtOnce()
        {
            Manager();
            AndroidNotificationManager.OpenSettingsCore(AndroidNotificationManager.OperationOpenAppDetailsSettings, () => Api.ErrorNotInitialized);
            Assert.AreEqual(0, AndroidNotificationManager.PendingCallCountForTests, "refused at the entry: no completion comes");
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("boom"));
            AndroidNotificationManager.OpenSettingsCore(AndroidNotificationManager.OperationOpenExactAlarmSettings, () => throw new InvalidOperationException("boom"));
            Assert.AreEqual(0, AndroidNotificationManager.PendingCallCountForTests, "thrown: no completion comes");
            yield return Frames();
            Assert.AreEqual(new[]
            {
                "Op|openAppDetailsSettings|False|openAppDetailsSettings could not be started.",
                "Op|openExactAlarmSettings|False|boom",
            }, _events);
        }

        // ── The schedule retry (J-N8) ────────────────────────────────────────────

        [Test]
        public void ScheduleWithFallback_ExactNotAllowed_RetriesOnceAsInexact()
        {
            var calls = new List<int>();
            int code = AndroidNotificationManager.ScheduleWithFallback(0, inexact =>
            {
                calls.Add(inexact);
                return inexact == 0 ? Api.ErrorExactAlarmNotAllowed : Api.ErrorNone;
            });
            Assert.AreEqual(Api.ErrorNone, code);
            Assert.AreEqual(new[] { 0, 1 }, calls);
        }

        [Test]
        public void ScheduleWithFallback_TheRetrysFailureIsTheAnswer()
        {
            var calls = new List<int>();
            int code = AndroidNotificationManager.ScheduleWithFallback(0, inexact =>
            {
                calls.Add(inexact);
                return inexact == 0 ? Api.ErrorExactAlarmNotAllowed : Api.ErrorStorageFailed;
            });
            Assert.AreEqual(Api.ErrorStorageFailed, code);
            Assert.AreEqual(new[] { 0, 1 }, calls);
        }

        [TestCase(Api.ErrorNone)]
        [TestCase(Api.ErrorPermissionDenied)]
        [TestCase(Api.ErrorInvalidParameter)]
        public void ScheduleWithFallback_OtherAnswers_AreNotRetried(int answer)
        {
            var calls = new List<int>();
            Assert.AreEqual(answer, AndroidNotificationManager.ScheduleWithFallback(0, inexact => { calls.Add(inexact); return answer; }));
            Assert.AreEqual(new[] { 0 }, calls);
        }

        [Test]
        public void ScheduleWithFallback_AnInexactSchedule_IsCalledOnceWithOne()
        {
            var calls = new List<int>();
            Assert.AreEqual(Api.ErrorExactAlarmNotAllowed,
                AndroidNotificationManager.ScheduleWithFallback(1, inexact => { calls.Add(inexact); return Api.ErrorExactAlarmNotAllowed; }));
            Assert.AreEqual(new[] { 1 }, calls);
        }

        // ── Listener registration (J-N12) ────────────────────────────────────────

        [Test]
        public void Listeners_EachRegisteredOnce()
        {
            int interactions = 0, shown = 0;
            AndroidNotificationManager.SetNativeForTests(() => { interactions++; return Api.ErrorNone; }, () => { shown++; return Api.ErrorNone; });
            Assert.IsTrue(AndroidNotificationManager.EnsureListeners());
            Assert.IsTrue(AndroidNotificationManager.EnsureListeners());
            Assert.AreEqual((1, 1), (interactions, shown));
            Assert.AreEqual((true, true), AndroidNotificationManager.ListenersRegisteredForTests);
        }

        [Test]
        public void Listeners_OnlyTheFailedOneIsTriedAgain()
        {
            int interactions = 0, shown = 0;
            bool shownFails = true;
            AndroidNotificationManager.SetNativeForTests(
                () => { interactions++; return Api.ErrorNone; },
                () => { shown++; return shownFails ? Api.ErrorNotInitialized : Api.ErrorNone; });
            Assert.IsFalse(AndroidNotificationManager.EnsureListeners());
            Assert.AreEqual((true, false), AndroidNotificationManager.ListenersRegisteredForTests);
            shownFails = false;
            Assert.IsTrue(AndroidNotificationManager.EnsureListeners());
            Assert.AreEqual((1, 2), (interactions, shown), "the tap listener is not registered twice");
        }

        [Test]
        public void Listeners_AThrowingRegistrationIsTriedAgain()
        {
            int calls = 0;
            AndroidNotificationManager.SetNativeForTests(() => { if (calls++ == 0) throw new InvalidOperationException("boom"); return Api.ErrorNone; }, () => Api.ErrorNone);
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("boom"));
            Assert.IsFalse(AndroidNotificationManager.EnsureListeners());
            Assert.IsTrue(AndroidNotificationManager.EnsureListeners());
            Assert.AreEqual(2, calls);
        }

        // ── Event handles (E-17) ─────────────────────────────────────────────────

        [Test]
        public void ReadAndFree_FreesOnceWhenTheReadSucceeds()
        {
            var freed = new List<IntPtr>();
            int value = AndroidNotificationManager.ReadAndFree(new IntPtr(42), handle => handle.ToInt32() + 1, freed.Add);
            Assert.AreEqual(43, value);
            Assert.AreEqual(new[] { new IntPtr(42) }, freed);
        }

        [Test]
        public void ReadAndFree_FreesOnceWhenTheReadThrows()
        {
            var freed = new List<IntPtr>();
            Assert.Throws<InvalidOperationException>(() =>
                AndroidNotificationManager.ReadAndFree<int>(new IntPtr(42), _ => throw new InvalidOperationException(), freed.Add));
            Assert.AreEqual(new[] { new IntPtr(42) }, freed);
        }
    }
}
#endif
