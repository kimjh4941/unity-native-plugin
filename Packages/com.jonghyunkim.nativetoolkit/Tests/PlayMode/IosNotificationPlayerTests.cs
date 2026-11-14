#nullable enable

#if UNITY_IOS
#if !UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using JonghyunKim.NativeToolkit.Runtime.Notification;
using JonghyunKim.NativeToolkit.Runtime.iOS.Notification;
using static JonghyunKim.NativeToolkit.Tests.IosPlayerTestSupport;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// iOS player: the Notification plan v4, section 1 (artifact/ios/notification/designs/2026-11-14-ios-notification-ui-test-plan-v4.md).
    /// The player never asks for the permission (nobody would answer the prompt), so it stays
    /// NotDetermined and nothing is shown. Requests use the ids ntkp-&lt;n&gt;; the teardown cancels and
    /// removes them. Lists are read for the test's own ids only. Values the plan says to record are
    /// written with <see cref="Record"/>.
    /// <para>
    /// NTK_IOS_CABI_2_NOTIFICATION is defined when Notification itself moves to the C ABI 2.0.0 (stage 3,
    /// the last feature; not at the 3-S switch, where Notification still runs a temporary path). Under it,
    /// the expectations that wait for the mapping design (2c) fail with "write the 2.0.0 expectation"
    /// instead of passing on a partial check (implementation review v1, R-2, R-3, R-10).
    /// </para>
    /// </summary>
    [Category(IosPlayerTestSupport.Category)]
    public class IosNotificationPlayerTests
    {
        private static IosNotificationManager M => IosNotificationManager.Instance;
        private readonly List<string> _ids = new();
        private readonly List<IosNotificationResult> _events = new();
        private Action<IosNotificationResult>? _onEvent;

        private readonly List<string> _order = new();

        /// <summary>On the C ABI, fails where the 2.0.0 expectation is not written yet.</summary>
        private static void PendingOn2(string what)
        {
#if NTK_IOS_CABI_2_NOTIFICATION
            Assert.Fail($"write the 2.0.0 expectation of {what} (stage 3, the Notification mapping design 2c)");
#endif
        }

        private static void Record(string what) => Debug.Log($"[NTK-NOTIFICATION-RECORD] {TestContext.CurrentContext.Test.Name} {what}");

        private static string Content(string id, string title = "ntk player", string extra = "") =>
            "{\"id\":\"" + id + "\",\"title\":\"" + title + "\"" + extra + "}";

        private static string Interval(double seconds) =>
            IosNotificationJsonBuilder.BuildTimeIntervalTriggerJson(new TimeIntervalTriggerPayload { interval = seconds });

        private sealed class Box<T>
        {
            public T Value = default!;
            public int Count;
        }

        [SetUp]
        public void SetUp()
        {
            _ids.Clear();
            _events.Clear();
            _order.Clear();
            _onEvent = r => { _events.Add(r); _order.Add("event"); };
            M.NotificationOperationCompleted += _onEvent;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            M.NotificationOperationCompleted -= _onEvent;
            foreach (string id in _ids)
            {
                M.CancelScheduledNotification(id);
                M.RemoveDeliveredNotification(id);
            }
            // The categories of NP-15 and NP-16, also when the test failed before removing them.
            foreach (string category in new[] { "ntkp-15", "ntkp-16a", "ntkp-16b", "ntkp-16c" })
                M.RemoveCategory(category);
            yield return WaitFor(() => false, 0.5f);
        }

        private string Id(string id)
        {
            _ids.Add(id);
            return id;
        }

        private static IEnumerator Await<T>(Box<T> box) => WaitFor(() => box.Count > 0);

        private Box<IosNotificationResult> Schedule(string id, string content, string trigger)
        {
            var box = new Box<IosNotificationResult>();
            M.ScheduleNotification(content, trigger, id, r => { box.Value = r; box.Count++; _order.Add("callback"); });
            return box;
        }

        private Box<IosNotificationResult> Show(string content, string? trigger = null)
        {
            var box = new Box<IosNotificationResult>();
            M.ShowNotification(content, trigger, r => { box.Value = r; box.Count++; _order.Add("callback"); });
            return box;
        }

        private Box<IosNotificationResult> Callback(Action<Action<IosNotificationResult>> call)
        {
            var box = new Box<IosNotificationResult>();
            call(r => { box.Value = r; box.Count++; _order.Add("callback"); });
            return box;
        }

        private static Box<string> Scheduled()
        {
            var box = new Box<string>();
            M.GetScheduledNotifications(json => { box.Value = json; box.Count++; });
            return box;
        }

        private static Box<string> Delivered()
        {
            var box = new Box<string>();
            M.GetDeliveredNotifications(json => { box.Value = json; box.Count++; });
            return box;
        }

        private static bool Lists(string json, string id) => json != null && json.Contains("\"" + id + "\"");

        /// <summary>A list callback came exactly once with a JSON array (else "not listed" would pass on nothing).</summary>
        private static void RequireList(Box<string> list, string what)
        {
            Assert.AreEqual(1, list.Count, $"the {what} list did not come exactly once");
            Assert.IsTrue(list.Value != null && list.Value.TrimStart().StartsWith("["), $"the {what} list is not a JSON array");
        }

        private static string Describe(IosNotificationResult r) => r.IsSuccess ? "ok" : $"fail error={r.ErrorMessage ?? "<null>"}";

        /// <summary>The callback came once, and the operation event came once after it (plan v4: the event follows the callback).</summary>
        private IEnumerator ExpectOneResultAndEvent<T>(Box<T> box)
        {
            yield return WaitFor(() => box.Count > 0);
            yield return WaitFor(() => false, 1f);
            Assert.AreEqual(1, box.Count, "the callback did not come exactly once");
            Assert.AreEqual(1, _events.Count, "the operation event did not come exactly once");
            CollectionAssert.AreEqual(new[] { "callback", "event" }, _order, "the event must follow the callback");
            if (box.Value is IosNotificationResult r)
            {
                Assert.AreEqual(r.Operation, _events[0].Operation, "the event is of another operation");
                Assert.AreEqual(r.IsSuccess, _events[0].IsSuccess, "the event has another result");
                Assert.AreEqual(r.ErrorMessage, _events[0].ErrorMessage, "the event has another message");
            }
        }

        [UnityTest, Order(1)]
        public IEnumerator NP1_StatusIsNotDetermined()
        {
            var status = new Box<IosNotificationAuthorizationStatus>();
            M.GetAuthorizationStatus(s => { status.Value = s; status.Count++; });
            yield return Await(status);
            Assert.AreEqual(1, status.Count);
            Assert.AreEqual(IosNotificationAuthorizationStatus.NotDetermined, status.Value);
        }

        [UnityTest, Order(2)]
        public IEnumerator NP2_HasPermissionIsFalse()
        {
            var has = new Box<bool>();
            M.HasPermission(h => { has.Value = h; has.Count++; });
            yield return Await(has);
            Assert.AreEqual(1, has.Count);
            Assert.IsFalse(has.Value);
        }

        /// <summary>Two HasPermission calls in a row, 10 times: the shapes (callbacks to A and B) are recorded.</summary>
        [UnityTest, Order(3)]
        public IEnumerator NP3_HasPermissionTwiceInARow()
        {
            var shapes = new Dictionary<string, int>();
            for (int i = 0; i < 10; i++)
            {
                int a = 0, b = 0;
                M.HasPermission(_ => a++);
                M.HasPermission(_ => b++);
                yield return WaitFor(() => a + b >= 2, 3f);
                yield return WaitFor(() => false, 0.5f);
                string shape = $"A={a},B={b}";
                shapes[shape] = shapes.TryGetValue(shape, out int n) ? n + 1 : 1;
            }
            Record("shapes=" + string.Join(";", shapes.Select(s => $"{s.Key}x{s.Value}")));
#if NTK_IOS_CABI_2_NOTIFICATION
            CollectionAssert.AreEqual(new[] { "A=1,B=1" }, shapes.Keys, "one callback per request (NT-19)");
#else
            // 1.x, measured on the simulator (2026-11-15): A=0,B=1 ten times out of ten. A=1,B=1 is possible
            // when the first completion comes before the second call (plan v4, NP-3).
            Assert.IsTrue(shapes.Keys.All(s => s == "A=0,B=1" || s == "A=1,B=1" || s == "A=0,B=2"), string.Join(";", shapes.Keys));
#endif
        }

        [UnityTest, Order(4)]
        public IEnumerator NP4_ScheduleWithoutPermission()
        {
            var scheduled = Schedule(Id("ntkp-4"), Content("ntkp-4"), Interval(60));
            yield return ExpectOneResultAndEvent(scheduled);
            Record($"schedule={Describe(scheduled.Value)}");
            var list = Scheduled();
            yield return Await(list);
            RequireList(list, "pending");
            Record($"in-pending={Lists(list.Value, "ntkp-4")}");
            Assert.IsFalse(Lists(list.Value, "ntkp-4"), "nothing is pending without the permission (M2; 2.0.0 adds nothing)");
#if NTK_IOS_CABI_2_NOTIFICATION
            PendingOn2("NP-4 (PERMISSION_DENIED or success)");
#else
            Assert.IsTrue(scheduled.Value.IsSuccess);
#endif
        }

        [UnityTest, Order(5)]
        public IEnumerator NP5_ShowWithoutPermission()
        {
            var shown = Show(Content(Id("ntkp-5")));
            yield return ExpectOneResultAndEvent(shown);
            yield return WaitFor(() => false, 1f);
            var delivered = Delivered();
            yield return Await(delivered);
            RequireList(delivered, "delivered");
            Record($"show={Describe(shown.Value)} in-delivered={Lists(delivered.Value, "ntkp-5")}");
#if NTK_IOS_CABI_2_NOTIFICATION
            Assert.IsFalse(Lists(delivered.Value, "ntkp-5"), "2.0.0 adds nothing without the permission");
            PendingOn2("NP-5 (PERMISSION_DENIED or success)");
#else
            // 1.x (M2): the immediate request of a NotDetermined app is delivered.
            Assert.IsTrue(shown.Value.IsSuccess);
            Assert.IsTrue(Lists(delivered.Value, "ntkp-5"));
#endif
        }

        [UnityTest, Order(6)]
        public IEnumerator NP5b_UpdateWithoutPermission()
        {
            var updated = new Box<IosNotificationResult>();
            M.UpdateNotification(Id("ntkp-5b"), Content("ntkp-5b"), Interval(60), r => { updated.Value = r; updated.Count++; _order.Add("callback"); });
            yield return ExpectOneResultAndEvent(updated);
            var pending = Scheduled();
            yield return Await(pending);
            RequireList(pending, "pending");
            var delivered = Delivered();
            yield return Await(delivered);
            RequireList(delivered, "delivered");
            Record($"update={Describe(updated.Value)} in-pending={Lists(pending.Value, "ntkp-5b")} in-delivered={Lists(delivered.Value, "ntkp-5b")}");
            Assert.IsFalse(Lists(pending.Value, "ntkp-5b"));
            Assert.IsFalse(Lists(delivered.Value, "ntkp-5b"));
#if NTK_IOS_CABI_2_NOTIFICATION
            PendingOn2("NP-5b (PERMISSION_DENIED or success)");
#else
            Assert.IsTrue(updated.Value.IsSuccess);
#endif
        }

        [UnityTest, Order(7)]
        public IEnumerator NP6_ShowWithAnUnknownTrigger()
        {
            var shown = Show(Content(Id("ntkp-6")), "{\"type\":\"unknown\"}");
            yield return ExpectOneResultAndEvent(shown);
            Record($"show={Describe(shown.Value)}");
#if NTK_IOS_CABI_2_NOTIFICATION
            Assert.IsFalse(shown.Value.IsSuccess, "2.0.0 rejects an unknown trigger");
            PendingOn2("NP-6 (the INVALID_PARAMETER message)");
#else
            Assert.IsTrue(shown.Value.IsSuccess, "1.x shows it at once");
#endif
        }

        [UnityTest, Order(8)]
        public IEnumerator NP7_ScheduleWithAnUnknownTrigger()
        {
            var scheduled = Schedule(Id("ntkp-7"), Content("ntkp-7"), "{\"type\":\"unknown\"}");
            yield return ExpectOneResultAndEvent(scheduled);
            Record($"schedule={Describe(scheduled.Value)}");
            Assert.IsFalse(scheduled.Value.IsSuccess);
            Assert.AreEqual("Failed to parse notification content or trigger JSON", scheduled.Value.ErrorMessage);
        }

        private const string GenericError1 = "The operation couldn’t be completed. (IosLibrary.NotificationError error 1.)";
        private const string GenericError2 = "The operation couldn’t be completed. (IosLibrary.NotificationError error 2.)";

        /// <summary>Runs one call and records its result; the callback and the event must each come once.</summary>
        private IEnumerator RunCase<T>(string label, Func<Box<T>> call, Func<T, string> describe, List<string> lines)
        {
            _events.Clear();
            _order.Clear();
            var box = call();
            yield return WaitFor(() => box.Count > 0);
            yield return WaitFor(() => false, 1f);
            string order = string.Join(">", _order);
            lines.Add($"{label}: {(box.Count == 1 ? describe(box.Value) : "callbacks=" + box.Count)} events={_events.Count}" +
                      (order == "callback>event" ? "" : $" order={order}"));
        }

        private static readonly string T60 = "{\"type\":\"timeInterval\",\"interval\":60,\"repeats\":false}";

        /// <summary>NP-8: the values of the plan's table 1.1, raw JSON, one at a time (with the valid pairs).</summary>
        [UnityTest, Order(9)]
        public IEnumerator NP8_ValuesTheCAbiRejects()
        {
            var cases = new (string label, string op, string id, string content, string? trigger)[]
            {
                ("show-empty-content-id", "show", "", Content(""), null),
                ("schedule-empty-identifier", "schedule", "", Content("ntkp-8a"), T60),
                ("schedule-empty-content-id", "schedule", "ntkp-8b", Content(""), T60),
                ("badge-minus-2", "schedule", "ntkp-8c", Content("ntkp-8c", extra: ",\"badge\":-2"), T60),
                ("badge-0-valid", "schedule", "ntkp-8d", Content("ntkp-8d", extra: ",\"badge\":0"), T60),
                ("relevance-1.5", "schedule", "ntkp-8e", Content("ntkp-8e", extra: ",\"relevanceScore\":1.5"), T60),
                ("relevance-1.0-valid", "schedule", "ntkp-8f", Content("ntkp-8f", extra: ",\"relevanceScore\":1.0"), T60),
                ("attachment-https", "schedule", "ntkp-8g", Content("ntkp-8g", extra: ",\"attachments\":[{\"identifier\":\"a\",\"fileURL\":\"https://example.com/a.png\"}]"), T60),
                ("calendar-without-components", "schedule", "ntkp-8h", Content("ntkp-8h"), "{\"type\":\"calendar\",\"repeats\":false}"),
                ("location-empty-identifier", "schedule", "ntkp-8i", Content("ntkp-8i"),
                    "{\"type\":\"location\",\"identifier\":\"\",\"latitude\":35,\"longitude\":139,\"radius\":100,\"notifyOnEntry\":true,\"notifyOnExit\":false}"),
                ("userinfo-not-an-object", "schedule", "ntkp-8j", Content("ntkp-8j", extra: ",\"userInfo\":[1]"), T60),
                ("userinfo-reserved-key", "schedule", "ntkp-8k", Content("ntkp-8k", extra: ",\"userInfo\":{\"com.jonghyunkim.nativetoolkit\":1}"), T60),
                ("interruption-level-unknown", "schedule", "ntkp-8l", Content("ntkp-8l", extra: ",\"interruptionLevel\":\"loud\""), T60),
            };
            var lines = new List<string>();
            foreach (var c in cases)
            {
                if (c.id.Length > 0) Id(c.id);
                yield return RunCase(c.label, () => c.op == "show" ? Show(c.content, c.trigger) : Schedule(c.id, c.content, c.trigger!), Describe, lines);
            }
            foreach (string line in lines) Record(line);
            Assert.AreEqual(cases.Length, lines.Count(l => l.EndsWith(" events=1") && !l.Contains("callbacks=")),
                "a case did not give one callback and one event:\n" + string.Join("\n", lines));
#if NTK_IOS_CABI_2_NOTIFICATION
            PendingOn2("NP-8 (INVALID_PARAMETER, a message other than the valid pairs' PERMISSION_DENIED)");
#else
            // 1.x, measured on the simulator (2026-11-15): only the https attachment fails (M4).
            foreach (string line in lines)
            {
                string expected = line.StartsWith("attachment-https:") ? $"attachment-https: fail error={GenericError1} events=1" : line.Split(':')[0] + ": ok events=1";
                Assert.AreEqual(expected, line);
            }
#endif
        }

#if NTK_IOS_CABI_2_NOTIFICATION
        /// <summary>
        /// NP-9: crashes 1.x (M5, M8), so it is compiled only with NTK_IOS_CABI_2_NOTIFICATION, defined when the
        /// package moves to the C ABI 2.0.0 (stage 3-S). The plan's [Explicit] would leave a not-run case
        /// in the result, which scripts/ios_player_results.py counts as not passed.
        /// </summary>
        [UnityTest, Order(15)]
        public IEnumerator NP9_IntervalOutOfRange()
        {
            var lines = new List<string>();
            foreach (var (seconds, repeats) in new[] { (0.0, false), (-1.0, false), (30.0, true), (59.9, true) })
            {
                string trigger = IosNotificationJsonBuilder.BuildTimeIntervalTriggerJson(new TimeIntervalTriggerPayload { interval = seconds, repeats = repeats });
                yield return RunCase($"{seconds}/{repeats}", () => Schedule(Id("ntkp-9"), Content("ntkp-9"), trigger), Describe, lines);
            }
            foreach (string line in lines) Record(line);
            Assert.IsTrue(lines.All(l => l.Contains(": fail ")), string.Join("\n", lines));
            PendingOn2("NP-9 (INVALID_PARAMETER, not only a failure)");
        }
#endif

        /// <summary>NP-9b: a non-finite interval, through the C# builder, with Schedule (1.x cannot parse the JSON).</summary>
        [UnityTest, Order(10)]
        public IEnumerator NP9b_NonFiniteInterval()
        {
            var lines = new List<string>();
            foreach (double seconds in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
                yield return RunCase($"interval={seconds}", () => Schedule(Id("ntkp-9b"), Content("ntkp-9b"), Interval(seconds)), Describe, lines);
            foreach (string line in lines) Record(line);
            Assert.IsTrue(lines.All(l => l.Contains(": fail ") && l.EndsWith(" events=1")), string.Join("\n", lines));
#if NTK_IOS_CABI_2_NOTIFICATION
            PendingOn2("NP-9b (rejected in C# or INVALID_PARAMETER)");
#else
            Assert.IsTrue(lines.All(l => l.Contains("error=Failed to parse notification content or trigger JSON ")), string.Join("\n", lines));
#endif
        }

        [UnityTest, Order(11)]
        public IEnumerator NP13_NP14_SetBadgeCount()
        {
            var lines = new List<string>();
            foreach (int count in new[] { -1, 0 })
            {
                yield return RunCase($"count={count}", () =>
                {
                    var set = new Box<IosNotificationResult>();
                    M.SetBadgeCount(count, r => { set.Value = r; set.Count++; _order.Add("callback"); });
                    return set;
                }, Describe, lines);
            }
            foreach (string line in lines) Record(line);
            Assert.IsTrue(lines.All(l => l.EndsWith(" events=1")), string.Join("\n", lines));
#if NTK_IOS_CABI_2_NOTIFICATION
            PendingOn2("NP-13 (the INVALID_PARAMETER message)");
#else
            // 1.x, measured (2026-11-15): a negative badge already fails (the plan v4 expected success).
            CollectionAssert.AreEqual(new[] { $"count=-1: fail error={GenericError2} events=1", "count=0: ok events=1" }, lines);
#endif
        }

        [UnityTest, Order(12)]
        public IEnumerator NP15_RegisterCategoryCompletesInALaterFrame()
        {
            var registered = new Box<IosNotificationResult>();
            int frame = Time.frameCount;
            int callbackFrame = -1;
            bool insideTheCall = true;
            M.RegisterCategory("{\"identifier\":\"ntkp-15\",\"actions\":[{\"identifier\":\"open\",\"title\":\"Open\"}]}",
                r => { registered.Value = r; registered.Count++; callbackFrame = Time.frameCount; _order.Add("callback"); });
            insideTheCall = registered.Count > 0;
            yield return ExpectOneResultAndEvent(registered);
            Record($"result={Describe(registered.Value)} inside-the-call={insideTheCall} frames-later={callbackFrame - frame}");
            M.RemoveCategory("ntkp-15");
            Assert.IsTrue(registered.Value.IsSuccess);
            Assert.IsFalse(insideTheCall, "the callback came inside the call");
        }

        [UnityTest, Order(13)]
        public IEnumerator NP16_CategoryValuesTheCAbiRejects()
        {
            var cases = new (string label, string json)[]
            {
                ("missing-title", "{\"identifier\":\"ntkp-16a\",\"actions\":[{\"identifier\":\"open\"}]}"),
                ("empty-action-id", "{\"identifier\":\"ntkp-16b\",\"actions\":[{\"identifier\":\"\",\"title\":\"Open\"}]}"),
                ("unknown-option", "{\"identifier\":\"ntkp-16c\",\"actions\":[{\"identifier\":\"open\",\"title\":\"Open\",\"options\":[\"loud\"]}]}"),
            };
            var lines = new List<string>();
            foreach (var c in cases)
            {
                yield return RunCase(c.label, () =>
                {
                    var registered = new Box<IosNotificationResult>();
                    M.RegisterCategory(c.json, r => { registered.Value = r; registered.Count++; _order.Add("callback"); });
                    return registered;
                }, Describe, lines);
            }
            foreach (string line in lines) Record(line);
            foreach (var id in new[] { "ntkp-16a", "ntkp-16b", "ntkp-16c" }) M.RemoveCategory(id);
            Assert.IsTrue(lines.All(l => l.EndsWith(" events=1")), string.Join("\n", lines));
#if NTK_IOS_CABI_2_NOTIFICATION
            PendingOn2("NP-16 ((a) and (b) INVALID_PARAMETER, (c) 2c)");
#else
            Assert.IsTrue(lines.All(l => l.Contains(": ok ")), "1.x drops or keeps them and succeeds:\n" + string.Join("\n", lines));
#endif
        }

        [UnityTest, Order(14)]
        public IEnumerator NP18_ShowTwiceInARow()
        {
            var shapes = new Dictionary<string, int>();
            for (int i = 0; i < 10; i++)
            {
                int a = 0, b = 0;
                int events = _events.Count;
                M.ShowNotification(Content(Id($"ntkp-18a-{i}")), null, _ => a++);
                M.ShowNotification(Content(Id($"ntkp-18b-{i}")), null, _ => b++);
                yield return WaitFor(() => _events.Count - events >= 2, 3f);
                yield return WaitFor(() => false, 0.5f);
                string shape = $"A={a},B={b},events={_events.Count - events}";
                shapes[shape] = shapes.TryGetValue(shape, out int n) ? n + 1 : 1;
            }
            Record("shapes=" + string.Join(";", shapes.Select(s => $"{s.Key}x{s.Value}")));
#if NTK_IOS_CABI_2_NOTIFICATION
            CollectionAssert.AreEqual(new[] { "A=1,B=1,events=2" }, shapes.Keys);
#else
            Assert.IsTrue(shapes.Keys.All(s => s == "A=0,B=1,events=2" || s == "A=1,B=1,events=2" || s == "A=0,B=2,events=2"), string.Join(";", shapes.Keys));
#endif
        }
    }
}
#endif
#endif
