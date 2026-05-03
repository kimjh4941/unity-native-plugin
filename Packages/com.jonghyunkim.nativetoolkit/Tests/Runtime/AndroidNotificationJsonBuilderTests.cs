#nullable enable

using NUnit.Framework;
using JonghyunKim.NativeToolkit.Runtime.Notification;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// Validates JSON output generated for Android notification APIs.
    /// </summary>
    public sealed class AndroidNotificationJsonBuilderTests
    {
        [Test]
        public void BuildNotificationJson_SerializesOptionalFieldsAndData()
        {
            string json = AndroidNotificationJsonBuilder.BuildNotificationJson(new NotificationPayload
            {
                id = 101,
                title = "Boss Alert",
                message = "The world boss has spawned.",
                tag = "boss",
                channel = new ChannelPayload
                {
                    id = "gameplay",
                    name = "Gameplay",
                    importance = 4,
                    groupId = "events",
                    groupName = "Events"
                },
                smallIcon = new NotificationResourcePayload { name = "app_icon", type = "mipmap" },
                largeIcon = new NotificationResourcePayload { name = "app_icon", type = "mipmap" },
                subText = "Limited Event",
                category = "alarm",
                ticker = "Boss alert",
                number = 7,
                color = unchecked((int)0xFF4CAF50),
                groupKey = "live-events",
                sortKey = "boss-001",
                timeoutAfterMillis = 30000,
                launchAction = "open_boss_screen",
                style = new NotificationStylePayload
                {
                    type = "bigPicture",
                    picture = new NotificationResourcePayload { name = "app_icon", type = "mipmap" },
                    largeIcon = new NotificationResourcePayload { name = "app_icon", type = "mipmap" },
                    bigContentTitle = "Boss Alert",
                    summaryText = "Limited Event"
                },
                actions = new[]
                {
                    new NotificationActionPayload
                    {
                        title = "Join",
                        actionId = "action.join",
                        icon = new NotificationResourcePayload { name = "app_icon", type = "mipmap" },
                        launchApp = true,
                        allowGeneratedReplies = false,
                        semanticAction = 10,
                        contextual = true,
                        showsUserInterface = true
                    }
                },
                data = new[]
                {
                    new NotificationDataEntryPayload { key = "screen", value = "boss" },
                    new NotificationDataEntryPayload { key = "bossId", value = "boss_001" }
                }
            });

            StringAssert.Contains("\"smallIcon\":{\"name\":\"app_icon\",\"type\":\"mipmap\"}", json);
            StringAssert.Contains("\"picture\":{\"name\":\"app_icon\",\"type\":\"mipmap\"}", json);
            StringAssert.Contains("\"launchAction\":\"open_boss_screen\"", json);
            StringAssert.Contains("\"data\":{\"screen\":\"boss\",\"bossId\":\"boss_001\"}", json);
            StringAssert.Contains("\"groupKey\":\"live-events\"", json);
            StringAssert.Contains("\"timeoutAfterMillis\":30000", json);
        }

        [Test]
        public void BuildScheduledNotificationJson_SerializesNestedScheduleObject()
        {
            string json = AndroidNotificationJsonBuilder.BuildScheduledNotificationJson(new ScheduledNotificationEnvelopePayload
            {
                notification = new NotificationPayload
                {
                    id = 202,
                    title = "Guild Battle",
                    message = "Starts soon.",
                    channel = new ChannelPayload
                    {
                        id = "events",
                        name = "Events"
                    }
                },
                schedule = new NotificationSchedulePayload
                {
                    triggerAtMillis = 1714377600000,
                    exact = true,
                    allowWhileIdle = true,
                    persistAcrossBoot = true,
                    alarmType = 0
                }
            });

            StringAssert.Contains("\"notification\":{", json);
            StringAssert.Contains("\"schedule\":{\"triggerAtMillis\":1714377600000,\"exact\":true,\"allowWhileIdle\":true,\"persistAcrossBoot\":true,\"alarmType\":0}", json);
        }

        [Test]
        public void BuildNotificationJson_OmitsUnsetOptionalFields()
        {
            string json = AndroidNotificationJsonBuilder.BuildNotificationJson(new NotificationPayload
            {
                id = 303,
                title = "Simple",
                message = "Minimal payload.",
                channel = new ChannelPayload
                {
                    id = "default",
                    name = "Default"
                }
            });

            StringAssert.DoesNotContain("\"smallIcon\"", json);
            StringAssert.DoesNotContain("\"launchAction\"", json);
            StringAssert.DoesNotContain("\"data\"", json);
            StringAssert.DoesNotContain("\"timeoutAfterMillis\"", json);
        }
    }
}