#nullable enable

using System;
using NUnit.Framework;
using JonghyunKim.NativeToolkit.Runtime.iOS.Notification;
// The payload types under test. The move to the iOS types (artifact/ios/notification/designs/
// 2026-11-21-ios-notification-payloads-design-v2.md, D-6) changes these five lines only: the expected JSON
// below was first made green with the old types, so the same strings show that the copy kept every field,
// default and type.
using Content = JonghyunKim.NativeToolkit.Runtime.iOS.Notification.IosNotificationContentPayload;
using Attachment = JonghyunKim.NativeToolkit.Runtime.iOS.Notification.IosNotificationAttachmentPayload;
using TimeInterval = JonghyunKim.NativeToolkit.Runtime.iOS.Notification.IosTimeIntervalTriggerPayload;
using Calendar = JonghyunKim.NativeToolkit.Runtime.iOS.Notification.IosCalendarTriggerPayload;
using Location = JonghyunKim.NativeToolkit.Runtime.iOS.Notification.IosLocationTriggerPayload;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// The JSON <see cref="IosNotificationJsonBuilder"/> writes for the content and trigger payloads: with each
    /// type's defaults, and with every field set to something else. The manager reads these keys
    /// (<c>IosNotificationRequests</c>).
    /// </summary>
    public sealed class IosNotificationJsonBuilderTests
    {
        [Test]
        public void Content_Defaults()
        {
            Assert.AreEqual("{\"id\":\"\",\"title\":\"\"}", IosNotificationJsonBuilder.BuildContentJson(new Content()));
        }

        [Test]
        public void Content_EveryField()
        {
            var content = new Content
            {
                id = "n1",
                title = "T",
                subtitle = "S",
                body = "B",
                badge = 3,
                sound = "default",
                categoryIdentifier = "c",
                interruptionLevel = "timeSensitive",
                threadIdentifier = "t",
                targetContentIdentifier = "x",
                relevanceScore = 0.75,
                filterCriteria = "f",
                attachments = new[]
                {
                    new Attachment { identifier = "a1", fileURL = "file:///a.png" },
                    new Attachment { identifier = "a2", fileURL = "file:///b.png" },
                },
            };
            Assert.AreEqual(
                "{\"id\":\"n1\",\"title\":\"T\",\"subtitle\":\"S\",\"body\":\"B\",\"badge\":3,\"sound\":\"default\"," +
                "\"categoryIdentifier\":\"c\",\"interruptionLevel\":\"timeSensitive\",\"threadIdentifier\":\"t\"," +
                "\"targetContentIdentifier\":\"x\",\"relevanceScore\":0.75,\"filterCriteria\":\"f\"," +
                "\"attachments\":[{\"identifier\":\"a1\",\"fileURL\":\"file:///a.png\"},{\"identifier\":\"a2\",\"fileURL\":\"file:///b.png\"}]}",
                IosNotificationJsonBuilder.BuildContentJson(content));
        }

        [Test]
        public void Content_AnAttachmentWithItsDefaultsIsLeftOut()
        {
            var content = new Content { id = "n1", title = "T", attachments = new[] { new Attachment() } };
            Assert.AreEqual("{\"id\":\"n1\",\"title\":\"T\"}", IosNotificationJsonBuilder.BuildContentJson(content));
        }

        [Test]
        public void TimeInterval_Defaults()
        {
            Assert.AreEqual("{\"type\":\"timeInterval\",\"interval\":1,\"repeats\":false}",
                IosNotificationJsonBuilder.BuildTimeIntervalTriggerJson(new TimeInterval()));
        }

        [Test]
        public void TimeInterval_EveryField()
        {
            Assert.AreEqual("{\"type\":\"timeInterval\",\"interval\":60.5,\"repeats\":true}",
                IosNotificationJsonBuilder.BuildTimeIntervalTriggerJson(new TimeInterval { interval = 60.5, repeats = true }));
        }

        [Test]
        public void Calendar_Defaults()
        {
            Assert.AreEqual("{\"type\":\"calendar\",\"repeats\":false}",
                IosNotificationJsonBuilder.BuildCalendarTriggerJson(new Calendar()));
        }

        [Test]
        public void Calendar_EveryField()
        {
            var trigger = new Calendar { year = 2027, month = 1, day = 2, hour = 3, minute = 4, second = 5, repeats = true };
            Assert.AreEqual(
                "{\"type\":\"calendar\",\"repeats\":true,\"year\":2027,\"month\":1,\"day\":2,\"hour\":3,\"minute\":4,\"second\":5}",
                IosNotificationJsonBuilder.BuildCalendarTriggerJson(trigger));
        }

        [Test]
        public void Location_Defaults()
        {
            Assert.AreEqual(
                "{\"type\":\"location\",\"identifier\":\"\",\"latitude\":0,\"longitude\":0,\"radius\":100,\"notifyOnEntry\":true,\"notifyOnExit\":false}",
                IosNotificationJsonBuilder.BuildLocationTriggerJson(new Location()));
        }

        [Test]
        public void Location_EveryField()
        {
            var trigger = new Location
            {
                identifier = "r", latitude = 35.5, longitude = 139.25, radius = 250.5, notifyOnEntry = false, notifyOnExit = true,
            };
            Assert.AreEqual(
                "{\"type\":\"location\",\"identifier\":\"r\",\"latitude\":35.5,\"longitude\":139.25,\"radius\":250.5,\"notifyOnEntry\":false,\"notifyOnExit\":true}",
                IosNotificationJsonBuilder.BuildLocationTriggerJson(trigger));
        }

        [TestCase(typeof(Content))]
        [TestCase(typeof(Attachment))]
        [TestCase(typeof(TimeInterval))]
        [TestCase(typeof(Calendar))]
        [TestCase(typeof(Location))]
        public void PayloadTypes_AreSerializable(Type type)
        {
            Assert.IsTrue(type.IsSerializable, type.Name);
        }
    }
}
