#nullable enable

using NUnit.Framework;
using JonghyunKim.NativeToolkit.Runtime.Android.Notification;

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
            string json = AndroidNotificationJsonBuilder.BuildNotificationJson(new AndroidNotificationPayload
            {
                id = 101,
                title = "Boss Alert",
                message = "The world boss has spawned.",
                tag = "boss",
                channel = new AndroidChannelPayload
                {
                    id = "gameplay",
                    name = "Gameplay",
                    importance = 4,
                    groupId = "events",
                    groupName = "Events"
                },
                smallIcon = new AndroidNotificationResourcePayload { name = "app_icon", type = "mipmap" },
                largeIcon = new AndroidNotificationResourcePayload { name = "app_icon", type = "mipmap" },
                subText = "Limited Event",
                category = "alarm",
                ticker = "Boss alert",
                number = 7,
                color = unchecked((int)0xFF4CAF50),
                groupKey = "live-events",
                sortKey = "boss-001",
                timeoutAfterMillis = 30000,
                launchAction = "open_boss_screen",
                style = new AndroidNotificationStylePayload
                {
                    type = "bigPicture",
                    picture = new AndroidNotificationResourcePayload { name = "app_icon", type = "mipmap" },
                    largeIcon = new AndroidNotificationResourcePayload { name = "app_icon", type = "mipmap" },
                    bigContentTitle = "Boss Alert",
                    summaryText = "Limited Event"
                },
                actions = new[]
                {
                    new AndroidNotificationActionPayload
                    {
                        title = "Join",
                        actionId = "action.join",
                        icon = new AndroidNotificationResourcePayload { name = "app_icon", type = "mipmap" },
                        launchApp = true,
                        allowGeneratedReplies = false,
                        semanticAction = 10,
                        contextual = true,
                        showsUserInterface = true
                    }
                },
                data = new[]
                {
                    new AndroidNotificationDataEntryPayload { key = "screen", value = "boss" },
                    new AndroidNotificationDataEntryPayload { key = "bossId", value = "boss_001" }
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
            string json = AndroidNotificationJsonBuilder.BuildScheduledNotificationJson(new AndroidScheduledNotificationEnvelopePayload
            {
                notification = new AndroidNotificationPayload
                {
                    id = 202,
                    title = "Guild Battle",
                    message = "Starts soon.",
                    channel = new AndroidChannelPayload
                    {
                        id = "events",
                        name = "Events"
                    }
                },
                schedule = new AndroidNotificationSchedulePayload
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
            string json = AndroidNotificationJsonBuilder.BuildNotificationJson(new AndroidNotificationPayload
            {
                id = 303,
                title = "Simple",
                message = "Minimal payload.",
                channel = new AndroidChannelPayload
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