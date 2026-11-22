#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using NUnit.Framework;
using JonghyunKim.NativeToolkit.Runtime.iOS.Notification;
using Api = JonghyunKim.NativeToolkit.Runtime.iOS.Notification.IosNotificationCApi;
using Req = JonghyunKim.NativeToolkit.Runtime.iOS.Notification.IosNotificationRequests;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// Layer 1 tests for the iOS Notification C ABI bridge (artifact/ios/notification, C ABI design v4): the structs
    /// against native-toolkit's StructLayoutTests.m, the messages (D-6), the list JSON (D-9), the JSON of the
    /// public API read as 1.13.0 read it (D-4, D-5, D-16), and the userInfo of the event results (D-12).
    /// </summary>
    public sealed class IosNotificationCApiTests
    {
        private static void AssertLayout<T>(int size, params (string field, int offset)[] fields) where T : struct
        {
            Assert.AreEqual(size, Marshal.SizeOf<T>(), $"sizeof {typeof(T).Name}");
            Assert.AreEqual(fields.Length, typeof(T).GetFields().Length, $"{typeof(T).Name} field count");
            foreach (var (field, offset) in fields)
                Assert.AreEqual(offset, Marshal.OffsetOf<T>(field).ToInt32(), $"{typeof(T).Name}.{field}");
        }

        // ── Structs ──

        [Test]
        public void Trigger_Layout() => AssertLayout<Api.Trigger>(96,
            ("StructSize", 0), ("Reserved0", 4), ("Kind", 8), ("Repeats", 12), ("IntervalSeconds", 16),
            ("CalendarFields", 24), ("Year", 28), ("Month", 32), ("Day", 36), ("Hour", 40), ("Minute", 44),
            ("Second", 48), ("Reserved1", 52), ("LocationIdentifier", 56), ("Latitude", 64), ("Longitude", 72),
            ("Radius", 80), ("NotNotifyOnEntry", 88), ("NotifyOnExit", 92));

        [Test]
        public void Action_Layout() => AssertLayout<Api.NotificationAction>(40,
            ("StructSize", 0), ("Reserved0", 4), ("Identifier", 8), ("Title", 16), ("SfSymbolName", 24),
            ("Options", 32), ("Reserved1", 36));

        [Test]
        public void TextInputAction_Layout() => AssertLayout<Api.TextInputAction>(40,
            ("StructSize", 0), ("Reserved0", 4), ("Identifier", 8), ("Title", 16), ("ButtonTitle", 24), ("Placeholder", 32));

        [Test]
        public void Category_Layout() => AssertLayout<Api.Category>(56,
            ("StructSize", 0), ("Reserved0", 4), ("Options", 8), ("Reserved1", 12), ("Identifier", 16),
            ("Actions", 24), ("ActionCount", 32), ("TextInputActions", 40), ("TextInputActionCount", 48));

        // ── Messages and status (D-6, D-8) ──

        [TestCase(Api.ErrorPermissionDenied, "Notification permission was denied.")]
        [TestCase(Api.ErrorInvalidParameter, "The notification request is invalid.")]
        [TestCase(Api.ErrorNotForeground, "The app is not in the foreground.")]
        [TestCase(Api.ErrorAddRequestFailed, "Failed to add the notification request.")]
        [TestCase(Api.ErrorSetBadgeCountFailed, "Failed to set the badge count.")]
        [TestCase(Api.ErrorAttachmentLoadFailed, "Failed to create a notification attachment.")]
        [TestCase(Api.ErrorAuthorizationFailed, "Failed to request notification permission.")]
        [TestCase(Api.ErrorSettingsNotOpened, "Failed to open the notification settings.")]
        [TestCase(Api.ErrorNotInitialized, "The library's notification delegate is not installed.")]
        [TestCase(Api.ErrorUnknown, "An unknown error occurred.")]
        [TestCase(99, "An unknown error occurred.")]
        public void ErrorMessage_IsTheLibrarysText(int error, string message) => Assert.AreEqual(message, Api.ErrorMessage(error));

        [Test]
        public void StatusNames_Are1xs()
        {
            CollectionAssert.AreEqual(
                new[] { "notDetermined", "denied", "authorized", "provisional", "ephemeral", "unknown", "unknown" },
                new[] { 0, 1, 2, 3, 4, 5, 9 }.Select(Api.StatusName));
        }

        // ── List JSON (D-9) ──

        [Test]
        public void ListJson_KeysAndDateAs1x()
        {
            var items = new List<Api.ListItem>
            {
                new("a", "T", null, "B", "", 1_700_000_000_999),
                new("b/\"c\"", "", "S", "", "cat", 0),
            };
            Assert.AreEqual(
                "[{\"identifier\":\"a\",\"title\":\"T\",\"body\":\"B\",\"categoryIdentifier\":\"\",\"date\":\"2023-11-14T22:13:20Z\"}," +
                "{\"identifier\":\"b/\\\"c\\\"\",\"title\":\"\",\"subtitle\":\"S\",\"categoryIdentifier\":\"cat\",\"date\":\"1970-01-01T00:00:00Z\"}]",
                Api.ListJson(items, delivered: true));
            Assert.AreEqual("[{\"identifier\":\"a\",\"title\":\"T\",\"body\":\"B\",\"categoryIdentifier\":\"\"}]",
                Api.ListJson(items.GetRange(0, 1), delivered: false));
            Assert.AreEqual("[]", Api.ListJson(new List<Api.ListItem>(), delivered: true));
        }

        [Test]
        public void StringEscapes_ControlCharactersLowerHex_SlashKept()
        {
            var builder = new System.Text.StringBuilder();
            Req.AppendString(builder, "a/b\n\t\u0001\u2028\\");
            Assert.AreEqual("\"a/b\\n\\t\\u0001\u2028\\\\\"", builder.ToString());
        }

        // ── Content (D-4, D-5, D-16) ──

        private static Req.Content Read(string json, string? fallback = null)
        {
            Assert.AreEqual(Req.Problem.None, Req.ReadContent(json, fallback, out Req.Content content, out _), json);
            return content;
        }

        [TestCase("not json")]
        [TestCase("[1]")]
        [TestCase("{\"title\":\"t\"}")]
        [TestCase("{\"id\":1,\"title\":\"t\"}")]
        [TestCase("{\"id\":\"a\"}")]
        public void Content_WithoutIdAndTitleIsUnreadable(string json) =>
            Assert.AreEqual(Req.Problem.Unreadable, Req.ReadContent(json, null, out _, out _));

        [Test]
        public void Content_AdjustsWhat1xAccepted()
        {
            Req.Content c = Read("{\"id\":\"\",\"title\":\"t\",\"badge\":-2,\"relevanceScore\":1.5,\"interruptionLevel\":\"loud\"," +
                                 "\"sound\":\"\",\"userInfo\":[1]}", "fallback");
            Assert.AreEqual("fallback", c.Id, "Schedule and Update use the identifier");
            Assert.IsNull(c.Badge, "a negative badge is not set");
            Assert.AreEqual(1.0, c.RelevanceScore);
            Assert.AreEqual(Api.InterruptionActive, c.InterruptionLevel);
            Assert.AreEqual(Api.SoundDefault, c.SoundKind);
            Assert.AreEqual("{}", c.UserInfoJson);
        }

        [Test]
        public void Content_SoundsAndLevels()
        {
            Assert.AreEqual(Api.SoundDefaultCritical, Read("{\"id\":\"a\",\"title\":\"t\",\"sound\":\"defaultCritical\"}").SoundKind);
            Req.Content named = Read("{\"id\":\"a\",\"title\":\"t\",\"sound\":\"bell.caf\"}");
            Assert.AreEqual((Api.SoundNamed, "bell.caf"), (named.SoundKind, named.SoundName));
            Assert.AreEqual(Api.InterruptionTimeSensitive, Read("{\"id\":\"a\",\"title\":\"t\",\"interruptionLevel\":\"timeSensitive\"}").InterruptionLevel);
            Assert.IsNull(Read("{\"id\":\"a\",\"title\":\"t\"}").InterruptionLevel);
            Assert.IsNull(Read("{\"id\":\"a\",\"title\":\"t\",\"sound\":1}").SoundName, "a non-string sound is ignored");
        }

        [Test]
        public void Content_UserInfoLosesNullsAndKeepsNumbersAsWritten()
        {
            Req.Content c = Read("{\"id\":\"a\",\"title\":\"t\",\"userInfo\":{\"n\":1.50,\"z\":null,\"o\":{\"x\":null,\"y\":[1,null,\"s\"]},\"b\":true}}");
            var reparsed = JonghyunKim.NativeToolkit.Runtime.iOS.Common.IosJsonReader.Parse(c.UserInfoJson)!;
            Assert.AreEqual("1.50", reparsed.Members["n"].RawNumber);
            Assert.IsFalse(reparsed.Members.ContainsKey("z"));
            Assert.IsFalse(reparsed.Members["o"].Members.ContainsKey("x"));
            Assert.AreEqual(2, reparsed.Members["o"].Members["y"].AsArray().Count);
            Assert.IsTrue(reparsed.Members["b"].TryGetBool(out bool b) && b);
            StringAssert.Contains("\\u0000", Read("{\"id\":\"a\",\"title\":\"t\",\"userInfo\":{\"k\":\"a\\u0000b\"}}").UserInfoJson,
                "U+0000 stays escaped");
        }

        [Test]
        public void Content_ArraysAreAllOrNothing()
        {
            Assert.AreEqual(0, Read("{\"id\":\"a\",\"title\":\"t\",\"attachments\":[{\"identifier\":\"i\",\"fileURL\":\"file:///a.png\"},1]}").Attachments.Count);
            Req.Content kept = Read("{\"id\":\"a\",\"title\":\"t\",\"attachments\":[{\"identifier\":\"\",\"fileURL\":\"file:///a.png\"}," +
                                    "{\"identifier\":\"x\",\"fileURL\":\"\"},{\"identifier\":\"y\"}]}");
            Assert.AreEqual(1, kept.Attachments.Count, "an empty identifier stays; an empty or missing fileURL is dropped");
            Assert.AreEqual("", kept.Attachments[0].Identifier);
        }

        [TestCase("{\"id\":\"a\\u0000b\",\"title\":\"t\"}")]
        [TestCase("{\"id\":\"a\",\"title\":\"t\",\"categoryIdentifier\":\"c\\u0000\"}")]
        [TestCase("{\"id\":\"a\",\"title\":\"t\",\"sound\":\"bell\\u0000.caf\"}")]
        [TestCase("{\"id\":\"a\",\"title\":\"t\",\"attachments\":[{\"identifier\":\"i\",\"fileURL\":\"file:///a.png\\u0000x\"}]}")]
        [TestCase("{\"id\":\"a\",\"title\":\"t\",\"attachments\":[{\"identifier\":\"i\\u0000\",\"fileURL\":\"file:///a.png\"}]}")]
        [TestCase("{\"id\":\"a\",\"title\":\"t\",\"threadIdentifier\":\"x\\u0000\"}")]
        [TestCase("{\"id\":\"a\",\"title\":\"t\",\"targetContentIdentifier\":\"x\\u0000\"}")]
        public void Content_NulInAnIdentifierFileOrSoundIsInvalid(string json) =>
            Assert.AreEqual(Req.Problem.Invalid, Req.ReadContent(json, null, out _, out _));

        [Test]
        public void Content_EmptyIdTakesTheArgumentUpToU0000()
        {
            Assert.AreEqual("a", Read("{\"id\":\"\",\"title\":\"t\"}", "a\0suffix").Id, "the argument is a C string, cut as in 1.x");
        }

        [Test]
        public void Content_ScheduleAndUpdateDoNotRejectTheUnusedIdForU0000()
        {
            Assert.AreEqual("a", Read("{\"id\":\"a\\u0000b\",\"title\":\"t\"}", "x").Id, "design v4, D-16: not used by the C ABI");
            Assert.AreEqual(Req.Problem.Invalid, Req.ReadContent("{\"id\":\"a\\u0000b\",\"title\":\"t\"}", null, out _, out _),
                "Show uses the id");
        }

        [Test]
        public void Content_NulInADisplayedStringWarns()
        {
            Assert.AreEqual(Req.Problem.None, Req.ReadContent("{\"id\":\"a\",\"title\":\"t\\u0000x\"}", null, out _, out bool warn));
            Assert.IsTrue(warn);
            Assert.AreEqual("A notification string contains U+0000; the native library receives it up to the U+0000.",
                Api.NulValueWarning, "a fixed text, without the value");
        }

        // ── Trigger (D-4) ──

        [TestCase(null)]
        [TestCase("{\"type\":\"unknown\"}")]
        [TestCase("{\"type\":\"timeInterval\"}")]
        [TestCase("{\"type\":\"location\",\"identifier\":\"l\",\"latitude\":1,\"longitude\":2}")]
        [TestCase("{\"interval\":60}")]
        public void Trigger_UnreadableAs1x(string? json)
        {
            Assert.AreEqual(Req.Problem.Unreadable, Req.ReadTrigger(json, out Req.Trigger? trigger));
            Assert.IsNull(trigger);
        }

        [Test]
        public void Trigger_SwiftCasts()
        {
            Req.ReadTrigger("{\"type\":\"timeInterval\",\"interval\":3600,\"repeats\":2}", out Req.Trigger? two);
            Assert.IsFalse(two!.Repeats, "2 is not a Bool for as? Bool");
            Req.ReadTrigger("{\"type\":\"timeInterval\",\"interval\":true,\"repeats\":1.0}", out Req.Trigger? one);
            Assert.AreEqual(1.0, one!.IntervalSeconds);
            Assert.IsTrue(one.Repeats);
            Req.ReadTrigger("{\"type\":\"calendar\",\"hour\":2.0,\"minute\":1e1,\"second\":1.5,\"day\":true}", out Req.Trigger? calendar);
            Assert.AreEqual(Api.CalendarHour | Api.CalendarMinute | Api.CalendarDay, calendar!.CalendarFields);
            Assert.AreEqual((2, 10, 1), (calendar.Hour, calendar.Minute, calendar.Day));
            Req.ReadTrigger("{\"type\":\"calendar\",\"year\":3000000000}", out Req.Trigger? large);
            Assert.AreEqual(0u, large!.CalendarFields, "outside int32 is not read");
        }

        [Test]
        public void Json_ReadAs1xRead_ByteOrderMarkAndDeepNesting()
        {
            Assert.AreEqual(Req.Problem.None, Req.ReadTrigger("\uFEFF{\"type\":\"timeInterval\",\"interval\":3600}", out Req.Trigger? bom));
            Assert.AreEqual(3600, bom!.IntervalSeconds);
            string deep = new string('[', 70) + new string(']', 70);
            Assert.AreEqual(Req.Problem.None, Req.ReadTrigger("{\"type\":\"timeInterval\",\"interval\":60,\"x\":" + deep + "}", out _),
                "deeper than 64 levels, as JSONSerialization read it");
        }

        [Test]
        public void Numbers_ThatSwiftCouldNotReadAsDoubleAreIgnored()
        {
            Assert.AreEqual(Req.Problem.Unreadable, Req.ReadTrigger("{\"type\":\"timeInterval\",\"interval\":1e400}", out _));
            Assert.AreEqual(Req.Problem.Unreadable, Req.ReadTrigger("{\"type\":\"timeInterval\",\"interval\":-1e400}", out _));
            Assert.AreEqual(Req.Problem.Unreadable, Req.ReadTrigger("{\"type\":\"timeInterval\",\"interval\":9007199254740993}", out _));
            Assert.AreEqual(Req.Problem.None, Req.ReadTrigger("{\"type\":\"timeInterval\",\"interval\":9007199254740992}", out _));
        }

        [Test]
        public void Trigger_Location()
        {
            Assert.AreEqual(Req.Problem.None, Req.ReadTrigger(
                "{\"type\":\"location\",\"identifier\":\"l\",\"latitude\":35,\"longitude\":139,\"radius\":100,\"notifyOnExit\":true,\"repeats\":true}",
                out Req.Trigger? t));
            Assert.AreEqual(Api.TriggerLocation, t!.Kind);
            Assert.IsTrue(t.NotifyOnEntry);
            Assert.IsTrue(t.NotifyOnExit);
            Assert.IsFalse(t.Repeats, "1.x never repeated a location trigger");
            Assert.AreEqual(Req.Problem.Invalid, Req.ReadTrigger(
                "{\"type\":\"location\",\"identifier\":\"l\\u0000\",\"latitude\":35,\"longitude\":139,\"radius\":100}", out _));
        }

        // ── Category (D-4) ──

        [Test]
        public void Category_DropsIncompleteActionsAndIgnoresUnknownOptions()
        {
            Assert.AreEqual(Req.Problem.None, Req.ReadCategory(
                "{\"identifier\":\"c\",\"options\":[\"customDismissAction\",\"loud\"]," +
                "\"actions\":[{\"identifier\":\"\",\"title\":\"A\"},{\"identifier\":\"b\"},{\"identifier\":\"ok\",\"title\":\"OK\",\"options\":[\"foreground\",\"x\"]}]," +
                "\"textInputActions\":[{\"identifier\":\"r\",\"title\":\"R\",\"buttonTitle\":\"Send\",\"textInputPlaceholder\":\"p\"},{\"identifier\":\"\",\"title\":\"R\",\"buttonTitle\":\"S\",\"textInputPlaceholder\":\"p\"}]}",
                out Req.Category c, out _));
            Assert.AreEqual(Api.CategoryCustomDismissAction, c.Options);
            Assert.AreEqual(1, c.Actions.Count);
            Assert.AreEqual(("ok", Api.ActionForeground), (c.Actions[0].Identifier, c.Actions[0].Options));
            Assert.AreEqual(1, c.TextInputActions.Count);
        }

        [Test]
        public void Category_MixedArraysAreIgnored()
        {
            Req.ReadCategory("{\"identifier\":\"c\",\"actions\":[{\"identifier\":\"a\",\"title\":\"A\"},42],\"options\":[\"allowInCarPlay\",1]}",
                out Req.Category c, out _);
            Assert.AreEqual(0, c.Actions.Count);
            Assert.AreEqual(0u, c.Options);
        }

        [TestCase("{\"identifier\":\"c\\u0000\"}")]
        [TestCase("{\"identifier\":\"c\",\"actions\":[{\"identifier\":\"a\\u0000\",\"title\":\"A\"}]}")]
        [TestCase("{\"identifier\":\"c\",\"textInputActions\":[{\"identifier\":\"r\\u0000\",\"title\":\"R\",\"buttonTitle\":\"S\",\"textInputPlaceholder\":\"p\"}]}")]
        public void Category_NulInAnIdentifierIsInvalid(string json) =>
            Assert.AreEqual(Req.Problem.Invalid, Req.ReadCategory(json, out _, out _));

        [Test]
        public void Category_Unreadable() =>
            Assert.AreEqual(Req.Problem.Unreadable, Req.ReadCategory("{\"actions\":[]}", out _, out _));

        // ── userInfo of the event results (D-12) ──

        [Test]
        public void UserInfo_SkipsNonStringValuesAndGoesOn()
        {
            var info = IosNotificationUserInfo.ParseFlat("{\"a\":{\"x\":\"1\",\"y\":\"2\"},\"b\":\"c\",\"d\":[\"p\",\"q\"],\"e\":1,\"f\":\"g,}\",\"q\":\"x\\\"y\"}");
            CollectionAssert.AreEquivalent(new Dictionary<string, string> { ["b"] = "c", ["f"] = "g,}", ["q"] = "x\\\"y" }, info);
            Assert.IsNull(IosNotificationUserInfo.ParseFlat("{\"n\":1}"));
            Assert.IsNull(IosNotificationUserInfo.ParseFlat("{}"));
            Assert.AreEqual("a/b", IosNotificationUserInfo.ParseFlat("{\"slash\":\"a/b\"}")!["slash"]);
            CollectionAssert.AreEquivalent(new Dictionary<string, string> { ["b"] = "c" },
                IosNotificationUserInfo.ParseFlat("{\"a\":{\"x\":\"}\",\"y\":[\"]\"]},\"b\":\"c\"}"), "brackets inside nested strings");
        }
    }
}
