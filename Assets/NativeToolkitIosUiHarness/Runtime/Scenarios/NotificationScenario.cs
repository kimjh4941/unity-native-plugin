#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using JonghyunKim.NativeToolkit.Runtime.Notification;
using JonghyunKim.NativeToolkit.Runtime.iOS.Notification;

namespace JonghyunKim.NativeToolkit.IosUiHarness
{
    /// <summary>
    /// Notification: section 2 of the Notification test plan v4 (artifact/ios/notification/designs/2026-11-14-ios-notification-ui-test-plan-v4.md).
    /// </summary>
    /// <remarks>
    /// Every notification has the title <see cref="Title"/> and a body of its own (<c>ntk NU-&lt;n&gt;</c>), so the
    /// test finds exactly one banner by both. Ids start with <c>ntkh-</c>; lists are written for those ids only.
    /// Scheduled notifications fire 10 s after the row (the plan's NU-4), time for the test to go to the home
    /// screen. The first line, <c>[abi@event] 1|2</c>, tells the test which ABI the package runs on, so the UI
    /// tests can tell 1.x expectations from 2.0.0 ones (implementation review v1, R-8).
    /// Events: <c>[tap@event] id=… action=… userInfo=…</c> and <c>[reply@event] id=… action=… text=…</c>.
    /// </remarks>
    internal static class NotificationScenario
    {
        public const string Title = "NTK UI Harness";
        public const string Category = "ntkh.events";
        public const string ReplyPlaceholder = "NTK UI Harness reply";
        private static IosNotificationManager M => IosNotificationManager.Instance;
        private static readonly Regex IdPattern = new("\"identifier\":\"(ntkh-[^\"]*)\"");
        private static readonly Regex ObjectPattern = new("\\{[^{}]*\\}");

        [DllImport("__Internal")] private static extern int ntkh_abi();

        public static void Register()
        {
            IosUiHarness.Append("abi", "event", ntkh_abi() == 1 ? "2" : "1");
            M.NotificationActionReceived += a =>
                IosUiHarness.Append("tap", "event", $"id={a.NotificationId} action={a.ActionId} userInfo={UserInfo(a.UserInfo)}");
            M.NotificationTextInputActionReceived += a =>
                IosUiHarness.Append("reply", "event", $"id={a.NotificationId} action={a.ActionId} text={IosUiHarness.Value(a.UserText)}");

            // NU-1 to NU-3b: the permission, its status and HasPermission.
            IosUiHarness.Row("permission", RequestPermission);
            IosUiHarness.Row("status", () => M.GetAuthorizationStatus(s => IosUiHarness.Append("status", "ok", s.ToString())));
            IosUiHarness.Row("has", () => M.HasPermission(h => IosUiHarness.Append("has", "ok", h.ToString())));

            // NU-5, 6, 13: Open (foreground), Reply (text input) and the custom dismiss action.
            IosUiHarness.Row("category", () => M.RegisterCategory(
                "{\"identifier\":\"" + Category + "\",\"options\":[\"customDismissAction\"]," +
                "\"actions\":[{\"identifier\":\"open\",\"title\":\"Open\",\"options\":[\"foreground\"]}]," +
                "\"textInputActions\":[{\"identifier\":\"reply\",\"title\":\"Reply\",\"buttonTitle\":\"Send\",\"textInputPlaceholder\":\"" + ReplyPlaceholder + "\"}]}",
                r => Result("category", r)));

            // NU-4: userInfo with string values only, and one with a number (1.x reads no non-string value).
            IosUiHarness.Row("schedule-4", () => Schedule("ntkh-4", "ntk NU-4",
                ",\"userInfo\":{\"k\":\"v\",\"jp\":\"日本\",\"slash\":\"a/b\",\"q\":\"x\\\"y\"}"));
            IosUiHarness.Row("schedule-4n", () => Schedule("ntkh-4n", "ntk NU-4n", ",\"userInfo\":{\"n\":1}"));
            IosUiHarness.Row("schedule-5", () => Schedule("ntkh-5", "ntk NU-5", ",\"categoryIdentifier\":\"" + Category + "\""));
            IosUiHarness.Row("schedule-6", () => Schedule("ntkh-6", "ntk NU-6", ",\"categoryIdentifier\":\"" + Category + "\""));
            IosUiHarness.Row("schedule-8", () => Schedule("ntkh-8", "ntk NU-8"));
            IosUiHarness.Row("schedule-13", () => Schedule("ntkh-13", "ntk NU-13", ",\"categoryIdentifier\":\"" + Category + "\""));

            // NU-7: shown at once while the harness is in front, then the delivered list.
            IosUiHarness.Row("show-7", () => M.ShowNotification(Content("ntkh-7", "ntk NU-7"), null, r => Result("show", r)));
            IosUiHarness.Row("delivered", () => M.GetDeliveredNotifications(json =>
                IosUiHarness.Append("delivered", "ok", $"{Own(json)} date={Date(json, "ntkh-7")}")));
            IosUiHarness.Row("pending", () => M.GetScheduledNotifications(json => IosUiHarness.Append("pending", "ok", Own(json))));

            // NU-2b: the operations after the permission was denied, then both lists.
            IosUiHarness.Row("denied-ops", () =>
            {
                M.ShowNotification(Content("ntkh-2b-show", "ntk NU-2b"), null, r => Result("denied-show", r));
                M.ScheduleNotification(Content("ntkh-2b-schedule", "ntk NU-2b"), Interval(60), "ntkh-2b-schedule", r => Result("denied-schedule", r));
                M.UpdateNotification("ntkh-2b-update", Content("ntkh-2b-update", "ntk NU-2b"), Interval(60), r => Result("denied-update", r));
            });

            IosUiHarness.Row("open-settings", () =>
            {
                M.OpenNotificationSettings();
                IosUiHarness.Append("open-settings", "ok", "called");
            });

            // NU-10: RequestPermission 3 s later, natively timed, while Control Center is over the harness.
            IosUiHarness.Hidden("late-permission-fire", () =>
            {
                IosUiHarness.Append("late", "event", $"state={IosUiHarness.ActivationState()}");
                RequestPermission();
            });
            IosUiHarness.Row("late-permission", () => IosUiHarness.After(3.0, "late-permission-fire"));

            // NU-14, 14b, 15: called when the harness enters the background.
            IosUiHarness.Hidden("bg-permission-fire", () =>
            {
                IosUiHarness.Append("late", "event", $"state={IosUiHarness.ActivationState()}");
                RequestPermission();
            });
            IosUiHarness.Row("bg-permission", () => IosUiHarness.OnBackground("bg-permission-fire"));
            IosUiHarness.Hidden("bg-settings-fire", () =>
            {
                IosUiHarness.Append("late", "event", $"state={IosUiHarness.ActivationState()}");
                M.OpenNotificationSettings();
            });
            IosUiHarness.Row("bg-settings", () => IosUiHarness.OnBackground("bg-settings-fire"));

            // NU-11: the request lifecycle (section 2.3), one step per row, each followed by the lists.
            RegisterLifecycle();

            // NU-12: category operations, one per row; the test waits 1 s between them.
            IosUiHarness.Row("cat-x-a", () => M.RegisterCategory(CategoryJson("ntkh.x", "A"), r => Result("cat", r)));
            IosUiHarness.Row("cat-x-b", () => M.RegisterCategory(CategoryJson("ntkh.x", "B"), r => Result("cat", r)));
            IosUiHarness.Row("cat-y", () => M.RegisterCategory(CategoryJson("ntkh.y", "C"), r => Result("cat", r)));
            IosUiHarness.Row("cat-remove-x", () =>
            {
                M.RemoveCategory("ntkh.x");
                IosUiHarness.Append("cat", "ok", "removed ntkh.x");
            });
            IosUiHarness.Row("schedule-12x", () => Schedule("ntkh-12x", "ntk NU-12 x", ",\"categoryIdentifier\":\"ntkh.x\""));
            IosUiHarness.Row("schedule-12x2", () => Schedule("ntkh-12x2", "ntk NU-12 x2", ",\"categoryIdentifier\":\"ntkh.x\""));
            IosUiHarness.Row("schedule-12x3", () => Schedule("ntkh-12x3", "ntk NU-12 x3", ",\"categoryIdentifier\":\"ntkh.x\""));
            IosUiHarness.Row("schedule-12y", () => Schedule("ntkh-12y", "ntk NU-12 y", ",\"categoryIdentifier\":\"ntkh.y\""));

            // NU-16: values 1.x was never measured with, one launch each.
            IosUiHarness.Row("u-lat91", () => Unmeasured("lat91", Content("ntkh-16a", "ntk NU-16"),
                "{\"type\":\"location\",\"identifier\":\"ntkh-r\",\"latitude\":91,\"longitude\":139,\"radius\":100,\"notifyOnEntry\":true,\"notifyOnExit\":false}"));
            IosUiHarness.Row("u-no-entry-exit", () => Unmeasured("no-entry-exit", Content("ntkh-16b", "ntk NU-16"),
                "{\"type\":\"location\",\"identifier\":\"ntkh-r\",\"latitude\":35,\"longitude\":139,\"radius\":100,\"notifyOnEntry\":false,\"notifyOnExit\":false}"));
            IosUiHarness.Row("u-nested-null", () => Unmeasured("nested-null",
                Content("ntkh-16c", "ntk NU-16", ",\"userInfo\":{\"a\":{\"b\":null}}"), Interval(60)));

            IosUiHarness.Row("cleanup", () =>
            {
                M.CancelAllNotifications();
                M.RemoveAllDeliveredNotifications();
                foreach (string category in new[] { Category, "ntkh.x", "ntkh.y" })
                    M.RemoveCategory(category);
                IosUiHarness.Append("cleanup", "ok", "done");
            });
        }

        private static void RequestPermission() =>
            M.RequestPermission(r => IosUiHarness.Append("permission", r.IsSuccess ? "ok" : "fail",
                r.IsSuccess ? "granted" : $"error={IosUiHarness.Value(r.ErrorMessage)}"));

        private static string Content(string id, string body, string extra = "") =>
            "{\"id\":\"" + id + "\",\"title\":\"" + Title + "\",\"body\":\"" + body + "\"" + extra + "}";

        private static string Interval(double seconds) =>
            IosNotificationJsonBuilder.BuildTimeIntervalTriggerJson(new TimeIntervalTriggerPayload { interval = seconds });

        private static void Schedule(string id, string body, string extra = "") =>
            M.ScheduleNotification(Content(id, body, extra), Interval(10), id, r => Result("schedule", r, id));

        private static void Unmeasured(string label, string content, string trigger) =>
            M.ScheduleNotification(content, trigger, "ntkh-16", r => Result("unmeasured", r, label));

        private static void Result(string id, IosNotificationResult r, string? what = null) =>
            IosUiHarness.Append(id, r.IsSuccess ? "ok" : "fail",
                (what == null ? "" : what + " ") + (r.IsSuccess ? "done" : $"error={IosUiHarness.Value(r.ErrorMessage)}"));

        private static string CategoryJson(string id, string action) =>
            "{\"identifier\":\"" + id + "\",\"actions\":[{\"identifier\":\"" + action.ToLowerInvariant() + "\",\"title\":\"" + action + "\"}]}";

        /// <summary>The userInfo dictionary as key=value pairs sorted by key, or &lt;null&gt;.</summary>
        private static string UserInfo(IReadOnlyDictionary<string, string>? info) =>
            info == null ? "<null>" : string.Join(",", info.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => $"{p.Key}={p.Value}"));

        /// <summary>The harness's own entries of a list: each id with its title, category, subtitle and body presence.</summary>
        private static string Own(string? json)
        {
            if (json == null) return "<null>";
            var entries = new List<string>();
            foreach (Match o in ObjectPattern.Matches(json))
            {
                Match id = IdPattern.Match(o.Value);
                if (!id.Success) continue;
                string category = Regex.Match(o.Value, "\"categoryIdentifier\":\"([^\"]*)\"").Groups[1].Value;
                string title = Regex.Match(o.Value, "\"title\":\"([^\"]*)\"").Groups[1].Value;
                entries.Add($"{id.Groups[1].Value}(title={IosUiHarness.Value(title)}" +
                            $",category={IosUiHarness.Value(category)},subtitle={o.Value.Contains("\"subtitle\"")},body={o.Value.Contains("\"body\"")})");
            }
            entries.Sort(StringComparer.Ordinal);
            return "ids=" + (entries.Count == 0 ? "none" : string.Join(";", entries));
        }

        /// <summary>The date field of the entry with the id, with its digits replaced by 9 (the shape, not the time).</summary>
        private static string Date(string? json, string id)
        {
            if (json == null) return "<null>";
            foreach (Match o in ObjectPattern.Matches(json))
            {
                if (!o.Value.Contains("\"identifier\":\"" + id + "\"")) continue;
                Match date = Regex.Match(o.Value, "\"date\":\"([^\"]*)\"");
                return date.Success ? Regex.Replace(date.Groups[1].Value, "[0-9]", "9") : "<none>";
            }
            return "<absent>";
        }

        // NU-11 (section 2.3): each row is one step; the test presses them in order, 1 s apart, and reads the
        // [pending] and [delivered] lines each step writes.
        private static void RegisterLifecycle()
        {
            void Lists(string step) => M.GetScheduledNotifications(p => M.GetDeliveredNotifications(d =>
                IosUiHarness.Append("lists", "ok", $"step={step} pending-{Own(p)} delivered-{Own(d)}")));
            // An hour: a step of the test can take tens of seconds (scrolling to a row, reading the screen),
            // and a 60 s trigger fired between steps on the simulator (2026-11-15).
            string Hour() => Interval(3600);

            IosUiHarness.Row("l1", () =>
            {
                M.ScheduleNotification("{\"id\":\"ntkh-a\",\"title\":\"" + Title + "\",\"subtitle\":\"s\",\"body\":\"ntk NU-11 a\",\"categoryIdentifier\":\"" + Category + "\"}",
                    Hour(), "ntkh-a", r => Result("l", r, "a"));
                M.ScheduleNotification("{\"id\":\"ntkh-b\",\"title\":\"" + Title + "\",\"body\":\"ntk NU-11 b\"}", Hour(), "ntkh-b", r => Result("l", r, "b"));
            });
            IosUiHarness.Row("l2", () => M.UpdateNotification("ntkh-a",
                "{\"id\":\"ntkh-a\",\"title\":\"" + Title + " 2\",\"body\":\"ntk NU-11 a\"}", Hour(), r => Result("l", r, "update-a")));
            IosUiHarness.Row("l2b", () => M.UpdateNotification("ntkh-b",
                "{\"id\":\"ntkh-b\",\"title\":\"" + Title + "\",\"body\":\"ntk NU-11 b\"}", null, r => Result("l", r, "update-b-null")));
            IosUiHarness.Row("l2c", () =>
            {
                M.RemoveDeliveredNotification("ntkh-b");
                M.ScheduleNotification("{\"id\":\"ntkh-b\",\"title\":\"" + Title + "\",\"body\":\"ntk NU-11 b\"}", Hour(), "ntkh-b", r => Result("l", r, "b-again"));
            });
            IosUiHarness.Row("l3", () => { M.CancelScheduledNotification("ntkh-a"); IosUiHarness.Append("l", "ok", "cancel-a"); });
            IosUiHarness.Row("l4", () =>
            {
                M.ScheduleNotification(Content("ntkh-c0", "ntk NU-11 c0"), Hour(), "ntkh-c0", r => Result("l", r, "c0"));
            });
            IosUiHarness.Row("l4-race", () =>
            {
                M.CancelAllScheduledNotifications();
                M.ScheduleNotification(Content("ntkh-c", "ntk NU-11 c"), Hour(), "ntkh-c", r => Result("l", r, "c"));
            });
            IosUiHarness.Row("l4-clear", () => { M.CancelAllScheduledNotifications(); IosUiHarness.Append("l", "ok", "clear"); });
            IosUiHarness.Row("l4-c", () => M.ScheduleNotification(Content("ntkh-c", "ntk NU-11 c"), Hour(), "ntkh-c", r => Result("l", r, "c-again")));
            IosUiHarness.Row("l5", () => { M.CancelNotification("ntkh-c"); IosUiHarness.Append("l", "ok", "cancel-c"); });
            IosUiHarness.Row("l5-de", () =>
            {
                M.ScheduleNotification(Content("ntkh-d", "ntk NU-11 d"), Hour(), "ntkh-d", r => Result("l", r, "d"));
                M.ScheduleNotification(Content("ntkh-e", "ntk NU-11 e"), Hour(), "ntkh-e", r => Result("l", r, "e"));
            });
            IosUiHarness.Row("l5-all", () => { M.CancelAllNotifications(); IosUiHarness.Append("l", "ok", "cancel-all"); });
            IosUiHarness.Row("l6-fg", () =>
            {
                M.ShowNotification(Content("ntkh-f", "ntk NU-11 f"), null, r => Result("l", r, "f"));
                M.ShowNotification(Content("ntkh-g", "ntk NU-11 g"), null, r => Result("l", r, "g"));
            });
            IosUiHarness.Row("l6-f", () => { M.RemoveDeliveredNotification("ntkh-f"); IosUiHarness.Append("l", "ok", "remove-f"); });
            IosUiHarness.Row("l6-all", () => { M.RemoveAllDeliveredNotifications(); IosUiHarness.Append("l", "ok", "remove-all"); });
            IosUiHarness.Row("lists", () => Lists("now"));
        }
    }
}
