#nullable enable

#if UNITY_ANDROID
#if !UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using UnityEngine;
using JonghyunKim.NativeToolkit.Runtime.Android.Notification;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// What the Android Notification device tests share: the notifications the app has showing, the
    /// manager's test seams on the C ABI, and the payloads copied for AN-25. The test plan is
    /// artifact/android/notification/designs/2026-10-12-android-notification-ui-test-plan-v4.md.
    /// </summary>
    internal static class AndroidNotificationTestSupport
    {
        internal const float EventTimeoutSeconds = 10f;
        internal const float TearDownTimeoutSeconds = 5f;

        /// <summary>How long a notification may take to show or go (plan 0: the post can come after the result).</summary>
        internal const float ActiveTimeoutSeconds = 5f;

        /// <summary>How long a check that nothing shows reads again, where no anchor can be shown (plan 0).</summary>
        internal const float SettleSeconds = 2f;

        internal const string ChannelId = "ntk_test";
        internal const string ChannelName = "NTK Tests";

        /// <summary>The player's own launcher icon, which every player has.</summary>
        internal static NotificationResourcePayload AppIcon() => new() { name = "app_icon", type = "mipmap" };

        internal static ChannelPayload Channel() => new() { id = ChannelId, name = ChannelName, importance = 3 };

        /// <summary>A unique title: NTK-AN-&lt;row&gt;-&lt;8 hex digits&gt; (plan 0).</summary>
        internal static string Title(string row) => $"NTK-AN-{row}-{Guid.NewGuid().ToString("N").Substring(0, 8)}";

        /// <summary>A notification with the fields every test needs, as the tests change it.</summary>
        internal static NotificationPayload Notification(int id, string title) => new()
        {
            id = id,
            title = title,
            message = "NTK notification test",
            channel = Channel(),
            smallIcon = AppIcon(),
        };

        // ── The C ABI's seams ────────────────────────────────────────────────────

        /// <summary>
        /// Whether the manager runs on native-toolkit's C ABI: its test seam is there. The fixtures check
        /// it first, so a seam that is gone or renamed fails them rather than passing in silence.
        /// </summary>
        internal static bool OnCApi => Seam("PendingCallCountForTests") != null;

        /// <summary>The manager's calls still waiting for their completion.</summary>
        internal static int? PendingCallCount() => (int?)Seam("PendingCallCountForTests")?.GetValue(null);

        /// <summary>The result the last settings call completed with (0 OPENED, 1 OPENED_FALLBACK), or null.</summary>
        internal static int? LastSettingsResult() => (int?)Seam("LastSettingsResultForTests")?.GetValue(null);

        private static PropertyInfo? Seam(string name) => typeof(AndroidNotificationManager).GetProperty(
            name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        // ── The notifications showing (plan 0) ──────────────────────────────────

        /// <summary>The app's notifications showing now, as (id, tag), from NotificationManager.getActiveNotifications.</summary>
        internal static List<(int Id, string? Tag)> Active()
        {
            var found = new List<(int, string?)>();
            using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
            using var manager = activity.Call<AndroidJavaObject>("getSystemService", "notification");
            AndroidJavaObject[] shown = manager.Call<AndroidJavaObject[]>("getActiveNotifications");
            foreach (AndroidJavaObject notification in shown)
            {
                using (notification)
                    found.Add((notification.Call<int>("getId"), notification.Call<string>("getTag")));
            }
            return found;
        }

        internal static bool IsActive(int id, string? tag = null) => Active().Any(n => n.Id == id && (tag == null || n.Tag == tag));

        /// <summary>Reads until the notification shows, for up to <see cref="ActiveTimeoutSeconds"/>.</summary>
        internal static IEnumerator Shows(int id, string? tag = null)
        {
            float deadline = Time.realtimeSinceStartup + ActiveTimeoutSeconds;
            while (!IsActive(id, tag) && Time.realtimeSinceStartup < deadline)
                yield return null;
            NUnit.Framework.Assert.IsTrue(IsActive(id, tag), $"notification {id} ({tag ?? "no tag"}) did not show within {ActiveTimeoutSeconds} s");
        }

        /// <summary>Reads until the notification has gone, for up to <see cref="ActiveTimeoutSeconds"/>.</summary>
        internal static IEnumerator Goes(int id, string? tag = null, float timeoutSeconds = ActiveTimeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (IsActive(id, tag) && Time.realtimeSinceStartup < deadline)
                yield return null;
            NUnit.Framework.Assert.IsFalse(IsActive(id, tag), $"notification {id} ({tag ?? "no tag"}) did not go within {timeoutSeconds} s");
        }

        /// <summary>
        /// Without an anchor (no permission: plan 0): reads for <see cref="SettleSeconds"/> and fails if
        /// the notification shows at any point.
        /// </summary>
        internal static IEnumerator StaysAway(int id)
        {
            float until = Time.realtimeSinceStartup + SettleSeconds;
            while (Time.realtimeSinceStartup < until)
            {
                NUnit.Framework.Assert.IsFalse(IsActive(id), $"notification {id} showed");
                yield return null;
            }
        }

        // ── AN-25's payloads (plan AN-25) ────────────────────────────────────────
        // Copied: AndroidNotificationJsonBuilderTests is in an Editor-only assembly, and the sample
        // builds its JSON in private methods; neither can be referenced from here. When either source
        // changes, change the copy (Tests/Runtime/AndroidNotificationJsonBuilderTests.cs and
        // Runtime/UI/Android/Notification/AndroidNotificationManagerExampleController.cs, Build*Json).

        /// <summary>The notifications AN-25 shows and cancels, by name.</summary>
        internal static IEnumerable<(string Name, NotificationPayload Payload)> ImmediatePayloads()
        {
            // AndroidNotificationJsonBuilderTests.BuildNotificationJson_SerializesOptionalFieldsAndData
            yield return ("builder 101", new NotificationPayload
            {
                id = 101, title = "Boss Alert", message = "The world boss has spawned.", tag = "boss",
                channel = new ChannelPayload { id = "gameplay", name = "Gameplay", importance = 4, groupId = "events", groupName = "Events" },
                smallIcon = AppIcon(), largeIcon = AppIcon(), subText = "Limited Event", category = "alarm",
                ticker = "Boss alert", number = 7, color = unchecked((int)0xFF4CAF50), groupKey = "live-events",
                sortKey = "boss-001", timeoutAfterMillis = 30000, launchAction = "open_boss_screen",
                style = new NotificationStylePayload
                {
                    type = "bigPicture", picture = AppIcon(), largeIcon = AppIcon(),
                    bigContentTitle = "Boss Alert", summaryText = "Limited Event",
                },
                actions = new[]
                {
                    new NotificationActionPayload
                    {
                        title = "Join", actionId = "action.join", icon = AppIcon(), launchApp = true,
                        allowGeneratedReplies = false, semanticAction = 10, contextual = true, showsUserInterface = true,
                    },
                },
                data = new[]
                {
                    new NotificationDataEntryPayload { key = "screen", value = "boss" },
                    new NotificationDataEntryPayload { key = "bossId", value = "boss_001" },
                },
            });
            // AndroidNotificationJsonBuilderTests.BuildNotificationJson_OmitsUnsetOptionalFields
            yield return ("builder 303", new NotificationPayload
            {
                id = 303, title = "Simple", message = "Minimal payload.",
                channel = new ChannelPayload { id = "default", name = "Default" },
            });
            // The sample's BuildImmediateNotificationJson
            yield return ("sample 1101", new NotificationPayload
            {
                id = 1101, title = "Energy Refilled", message = "Your squad is fully rested. Jump back in and clear the next raid.",
                tag = "energy", channel = SampleChannel(), smallIcon = AppIcon(), largeIcon = AppIcon(), subText = "Raid Ready",
                autoCancel = true, priority = 1, category = "recommendation", ticker = "Energy refilled", number = 3,
                style = new NotificationStylePayload
                {
                    type = "bigText",
                    bigText = "Your squad is fully rested. Jump back in and clear the next raid before the stamina cap overflows.",
                    bigContentTitle = "Energy Refilled", summaryText = "Raid Ready",
                },
            });
            // The sample's BuildUpdatedNotificationJson
            yield return ("sample 1101 updated", new NotificationPayload
            {
                id = 1101, title = "Daily Reward Ready", message = "Your login streak chest is waiting in town.", tag = "energy",
                channel = SampleChannel(), smallIcon = AppIcon(), largeIcon = AppIcon(), subText = "Town Reward",
                autoCancel = true, priority = 1,
                style = new NotificationStylePayload
                {
                    type = "bigPicture", picture = AppIcon(), largeIcon = AppIcon(), hideExpandedLargeIcon = false,
                    bigText = "Your login streak chest is waiting in town. Claim it now to keep your reward multiplier active.",
                    bigContentTitle = "Daily Reward Ready", summaryText = "Town Reward",
                },
            });
            // The sample's BuildActionNotificationJson
            yield return ("sample 1401", new NotificationPayload
            {
                id = 1401, title = "Match Found", message = "A ranked match is ready. Accept within 30 seconds.",
                channel = SampleChannel(), smallIcon = AppIcon(), autoCancel = true, priority = 1, launchAction = "open_battle_screen",
                actions = new[]
                {
                    new NotificationActionPayload { title = "Play Now", actionId = "com.jonghyunkim.nativetoolkit.ACTION_PLAY_NOW", icon = AppIcon(), launchApp = true, showsUserInterface = true },
                    new NotificationActionPayload { title = "Dismiss", actionId = "com.jonghyunkim.nativetoolkit.ACTION_DISMISS", launchApp = false, showsUserInterface = false },
                },
                data = new[]
                {
                    new NotificationDataEntryPayload { key = "screen", value = "battle" },
                    new NotificationDataEntryPayload { key = "matchId", value = "match_5678" },
                },
            });
            // The sample's BuildFullScreenNotificationJson
            yield return ("sample 1501", new NotificationPayload
            {
                id = 1501, title = "Match Starting", message = "Your match begins now. Launching game screen.",
                channel = SampleChannel(), smallIcon = AppIcon(), autoCancel = true, priority = 2, category = "call", fullScreenIntent = true,
            });
        }

        /// <summary>The scheduled notifications AN-25 schedules, ten minutes ahead (plan AN-25, R1-14).</summary>
        internal static IEnumerable<(string Name, ScheduledNotificationEnvelopePayload Payload)> ScheduledPayloads()
        {
            long later = DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeMilliseconds();
            // AndroidNotificationJsonBuilderTests.BuildScheduledNotificationJson_SerializesNestedScheduleObject
            yield return ("builder 202", new ScheduledNotificationEnvelopePayload
            {
                notification = new NotificationPayload { id = 202, title = "Guild Battle", message = "Starts soon.", channel = new ChannelPayload { id = "events", name = "Events" } },
                schedule = new NotificationSchedulePayload { triggerAtMillis = later, exact = true, allowWhileIdle = true, persistAcrossBoot = true, alarmType = 0 },
            });
            // The sample's BuildScheduledNotificationJson
            yield return ("sample 1201", new ScheduledNotificationEnvelopePayload
            {
                notification = new NotificationPayload
                {
                    id = 1201, title = "Guild Battle Starts Soon",
                    message = "Your team queue opens in 15 seconds. Rally your guild and prepare to deploy.", tag = "guild-battle",
                    channel = SampleChannel(), smallIcon = AppIcon(), autoCancel = true, priority = 1, groupKey = "guild-events", sortKey = "001",
                    style = new NotificationStylePayload
                    {
                        type = "bigText",
                        bigText = "Your team queue opens in 15 seconds. Rally your guild, finalize your loadout, and prepare to deploy.",
                        bigContentTitle = "Guild Battle Starts Soon", summaryText = "Guild Event",
                    },
                },
                schedule = new NotificationSchedulePayload { triggerAtMillis = later, exact = true, allowWhileIdle = true, persistAcrossBoot = true, alarmType = 0 },
            });
        }

        private static ChannelPayload SampleChannel() => new() { id = "native_toolkit_gameplay", name = "Gameplay Alerts" };
    }

    /// <summary>
    /// The manager's three events as a test saw them: operation results (<c>Op</c>), taps and
    /// dismissals (<c>Tap</c>) and notifications received in the foreground (<c>Recv</c>), each as a
    /// string, with the thread each was raised on. The teardown checks each kind against the test's
    /// expected count (plan 6).
    /// </summary>
    internal sealed class AndroidNotificationEvents
    {
        internal readonly List<string> Ops = new();
        internal readonly List<string> Taps = new();
        internal readonly List<string> Received = new();
        internal readonly List<NotificationActionResult> TapResults = new();
        private readonly List<int> _threads = new();
        private readonly int _mainThread = Thread.CurrentThread.ManagedThreadId;
        private AndroidNotificationManager? _manager;

        /// <summary>When the last event arrived (Time.realtimeSinceStartup), or null.</summary>
        internal float? LastEventAt { get; private set; }

        internal void Subscribe()
        {
            if (_manager != null) return;
            _manager = AndroidNotificationManager.Instance;
            _manager.NotificationOperationCompleted += OnOperation;
            _manager.NotificationActionTapped += OnTap;
            _manager.NotificationReceived += OnReceived;
        }

        internal void Unsubscribe()
        {
            if (_manager == null) return;
            _manager.NotificationOperationCompleted -= OnOperation;
            _manager.NotificationActionTapped -= OnTap;
            _manager.NotificationReceived -= OnReceived;
            _manager = null;
        }

        private void Note(List<string> list, string entry)
        {
            _threads.Add(Thread.CurrentThread.ManagedThreadId);
            list.Add(entry);
            LastEventAt = Time.realtimeSinceStartup;
        }

        private void OnOperation(NotificationResult r) => Note(Ops, $"Op|{r.Operation}|{r.IsSuccess}|{r.ErrorMessage ?? "null"}");

        private void OnTap(NotificationActionResult r)
        {
            TapResults.Add(r);
            string data = r.Data == null ? "null" : string.Join(",", r.Data.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => $"{p.Key}={p.Value}"));
            Note(Taps, $"Tap|{r.ActionId}|{r.NotificationId}|{data}");
        }

        private void OnReceived(NotificationReceivedResult r) => Note(Received, $"Recv|{r.NotificationId}|{r.Tag ?? "null"}|{r.ChannelId}");

        internal bool Reached(int ops, int taps, int received) =>
            Ops.Count >= ops && Taps.Count >= taps && Received.Count >= received;

        internal string? Mismatch(int ops, int taps, int received) =>
            Ops.Count == ops && Taps.Count == taps && Received.Count == received
                ? null
                : $"expected {ops} / {taps} / {received} (Op / Tap / Recv), saw {Ops.Count} / {Taps.Count} / {Received.Count}: {Describe()}";

        internal string? OffMainThread() => _threads.All(id => id == _mainThread)
            ? null
            : $"an event was raised off Unity's main thread (threads {string.Join(", ", _threads)}, main {_mainThread})";

        internal string Describe() => string.Join(" | ", Ops.Concat(Taps).Concat(Received));
    }
}
#endif
#endif
