#nullable enable

using NUnit.Framework;
using JonghyunKim.NativeToolkit.Runtime.Notification;

namespace JonghyunKim.NativeToolkit.Tests
{
    public sealed class MacNotificationResultTests
    {
        [Test]
        public void Success_SetsCorrectFields()
        {
            var result = MacNotificationResult.Success("requestPermission");
            Assert.AreEqual("requestPermission", result.Operation);
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(0, result.ErrorCode);
            Assert.IsNull(result.ErrorMessage);
        }

        [Test]
        public void Failure_SetsCorrectFields()
        {
            var result = MacNotificationResult.Failure("showNotification", 42, "something went wrong");
            Assert.AreEqual("showNotification", result.Operation);
            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(42, result.ErrorCode);
            Assert.AreEqual("something went wrong", result.ErrorMessage);
        }
    }

    public sealed class MacNotificationJsonResultTests
    {
        [Test]
        public void Success_SetsCorrectFields()
        {
            var result = MacNotificationJsonResult.Success("getAuthorizationStatus", "{\"status\":\"authorized\"}");
            Assert.AreEqual("getAuthorizationStatus", result.Operation);
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("{\"status\":\"authorized\"}", result.Json);
            Assert.AreEqual(0, result.ErrorCode);
            Assert.IsNull(result.ErrorMessage);
        }

        [Test]
        public void Failure_SetsNullJson()
        {
            var result = MacNotificationJsonResult.Failure("getScheduledNotifications", 5, "fetch failed");
            Assert.AreEqual("getScheduledNotifications", result.Operation);
            Assert.IsFalse(result.IsSuccess);
            Assert.IsNull(result.Json);
            Assert.AreEqual(5, result.ErrorCode);
            Assert.AreEqual("fetch failed", result.ErrorMessage);
        }
    }

    public sealed class MacNotificationAuthorizationStatusTests
    {
        [Test]
        public void Parse_AllKnownStatuses()
        {
            Assert.AreEqual(MacNotificationAuthorizationStatus.Authorized,    MacNotificationAuthorizationStatusParser.Parse("authorized"));
            Assert.AreEqual(MacNotificationAuthorizationStatus.Denied,        MacNotificationAuthorizationStatusParser.Parse("denied"));
            Assert.AreEqual(MacNotificationAuthorizationStatus.NotDetermined,  MacNotificationAuthorizationStatusParser.Parse("notDetermined"));
            Assert.AreEqual(MacNotificationAuthorizationStatus.Provisional,   MacNotificationAuthorizationStatusParser.Parse("provisional"));
            Assert.AreEqual(MacNotificationAuthorizationStatus.Unsupported,   MacNotificationAuthorizationStatusParser.Parse("unsupported"));
        }

        [Test]
        public void Parse_UnknownString_ReturnsUnsupported()
        {
            Assert.AreEqual(MacNotificationAuthorizationStatus.Unsupported, MacNotificationAuthorizationStatusParser.Parse("ephemeral"));
            Assert.AreEqual(MacNotificationAuthorizationStatus.Unsupported, MacNotificationAuthorizationStatusParser.Parse(null));
            Assert.AreEqual(MacNotificationAuthorizationStatus.Unsupported, MacNotificationAuthorizationStatusParser.Parse(""));
        }

        [Test]
        public void ParseJson_ExtractsStatusFromJson()
        {
            Assert.AreEqual(MacNotificationAuthorizationStatus.Authorized,
                MacNotificationAuthorizationStatusParser.ParseJson("{\"status\":\"authorized\"}"));
            Assert.AreEqual(MacNotificationAuthorizationStatus.Denied,
                MacNotificationAuthorizationStatusParser.ParseJson("{\"status\":\"denied\"}"));
            Assert.AreEqual(MacNotificationAuthorizationStatus.Unsupported,
                MacNotificationAuthorizationStatusParser.ParseJson(null));
            Assert.AreEqual(MacNotificationAuthorizationStatus.Unsupported,
                MacNotificationAuthorizationStatusParser.ParseJson("{}"));
        }
    }

    public sealed class MacNotificationJsonBuilderTests
    {
        [Test]
        public void BuildContentJson_RequiredFields()
        {
            string json = MacNotificationJsonBuilder.BuildContentJson(new NotificationContentPayload
            {
                id = "notif-001",
                title = "Hello"
            });

            StringAssert.Contains("\"id\":\"notif-001\"", json);
            StringAssert.Contains("\"title\":\"Hello\"", json);
            StringAssert.DoesNotContain("\"subtitle\"", json);
            StringAssert.DoesNotContain("\"badge\"", json);
            StringAssert.DoesNotContain("\"categoryIdentifier\"", json);
        }

        [Test]
        public void BuildContentJson_OptionalFields()
        {
            string json = MacNotificationJsonBuilder.BuildContentJson(new NotificationContentPayload
            {
                id = "notif-002",
                title = "Title",
                subtitle = "Sub",
                body = "Body",
                badge = 3,
                categoryIdentifier = "cat-001"
            });

            StringAssert.Contains("\"subtitle\":\"Sub\"", json);
            StringAssert.Contains("\"body\":\"Body\"", json);
            StringAssert.Contains("\"badge\":3", json);
            StringAssert.Contains("\"categoryIdentifier\":\"cat-001\"", json);
        }

        [Test]
        public void BuildTimeIntervalTriggerJson_UsesSecondsKey()
        {
            string json = MacNotificationJsonBuilder.BuildTimeIntervalTriggerJson(new TimeIntervalTriggerPayload
            {
                interval = 60.0,
                repeats = false
            });

            StringAssert.Contains("\"type\":\"timeInterval\"", json);
            StringAssert.Contains("\"seconds\":60", json);
            StringAssert.Contains("\"repeats\":false", json);
            StringAssert.DoesNotContain("\"interval\"", json);
        }

        [Test]
        public void BuildCalendarTriggerJson_IncludesDateComponents()
        {
            string json = MacNotificationJsonBuilder.BuildCalendarTriggerJson(new CalendarTriggerPayload
            {
                year = 2026,
                month = 5,
                day = 16,
                hour = 9,
                minute = 0,
                repeats = false
            });

            StringAssert.Contains("\"type\":\"calendar\"", json);
            StringAssert.Contains("\"year\":2026", json);
            StringAssert.Contains("\"month\":5", json);
            StringAssert.Contains("\"day\":16", json);
            StringAssert.Contains("\"hour\":9", json);
            StringAssert.Contains("\"repeats\":false", json);
        }

        [Test]
        public void BuildCategoryJson_UsesIdKey_NotIdentifier()
        {
            string json = MacNotificationJsonBuilder.BuildCategoryJson(new MacNotificationCategoryPayload
            {
                id = "cat-001",
                actions = new[]
                {
                    new MacNotificationActionPayload
                    {
                        id = "act-ok",
                        title = "OK",
                        isForeground = false,
                        isTextInput = false
                    }
                }
            });

            StringAssert.Contains("\"id\":\"cat-001\"", json);
            StringAssert.Contains("\"id\":\"act-ok\"", json);
            StringAssert.Contains("\"isForeground\":false", json);
            StringAssert.Contains("\"isTextInput\":false", json);
            StringAssert.DoesNotContain("\"identifier\"", json);
            StringAssert.DoesNotContain("\"sfSymbolName\"", json);
            StringAssert.DoesNotContain("\"options\"", json);
        }

        [Test]
        public void BuildCategoryJson_TextInputAction_IncludesPlaceholder()
        {
            string json = MacNotificationJsonBuilder.BuildCategoryJson(new MacNotificationCategoryPayload
            {
                id = "cat-input",
                actions = new[]
                {
                    new MacNotificationActionPayload
                    {
                        id = "act-reply",
                        title = "Reply",
                        isTextInput = true,
                        textInputPlaceholder = "Type here..."
                    }
                }
            });

            StringAssert.Contains("\"textInputPlaceholder\":\"Type here...\"", json);
        }

        [Test]
        public void BuildCategoryJson_EmptyActions_OmitsActionsField()
        {
            string json = MacNotificationJsonBuilder.BuildCategoryJson(new MacNotificationCategoryPayload
            {
                id = "cat-empty"
            });

            StringAssert.Contains("\"id\":\"cat-empty\"", json);
            StringAssert.DoesNotContain("\"actions\"", json);
        }
    }
}
