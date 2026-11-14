#nullable enable

using JonghyunKim.NativeToolkit.Runtime.Notification;

namespace JonghyunKim.NativeToolkit.IosUiHarness
{
    /// <summary>
    /// Notification: ask for permission, then a notification 10 seconds later the test taps (time enough
    /// for the test to send the harness to the background, also on a device).
    /// </summary>
    internal static class NotificationScenario
    {
        public const string Title = "NTK UI Harness";

        public static void Register()
        {
            IosNotificationManager.Instance.NotificationActionReceived += a =>
                IosUiHarness.Append("tap", "event", $"id={a.NotificationId} action={a.ActionId}");
            IosUiHarness.Row("permission", () => IosNotificationManager.Instance.RequestPermission(
                r => IosUiHarness.Append("permission", r.IsSuccess ? "ok" : "fail", r.IsSuccess ? "granted" : "not-granted")));
            IosUiHarness.Row("schedule", () => IosNotificationManager.Instance.ScheduleNotification(
                IosNotificationJsonBuilder.BuildContentJson(new NotificationContentPayload { id = "ntkh-1", title = Title, body = "harness banner" }),
                IosNotificationJsonBuilder.BuildTimeIntervalTriggerJson(new TimeIntervalTriggerPayload { interval = 10 }),
                "ntkh-1",
                r => IosUiHarness.Append("schedule", r.IsSuccess ? "ok" : "fail", r.IsSuccess ? "scheduled" : "not-scheduled")));
        }
    }
}
