#nullable enable

#if UNITY_ANDROID || UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using JonghyunKim.NativeToolkit.Runtime.Android.Notification;
using Mapping = JonghyunKim.NativeToolkit.Runtime.Android.Notification.AndroidNotificationJsonMapping;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// Layer 1 tests for <see cref="AndroidNotificationJsonMapping"/>: the rules of 1.x's Kotlin parser
    /// (required keys and their order, defaults, blank strings, dropped entries), the corrections 1.x
    /// applied (design J-N5), the type rules, and the JSON that <see cref="AndroidNotificationJsonBuilder"/>
    /// writes read back to the payload's values
    /// (artifact/android/notification/designs/2026-10-12-android-notification-design-v4.md, 4.2, 7.1).
    /// </summary>
    public sealed class AndroidNotificationJsonMappingTests
    {
        private const string Op = "showNotification";
        private const long Now = 1_700_000_000_000;
        private const string Channel = "\"channel\":{\"id\":\"c\",\"name\":\"C\"}";

        private static AndroidNotificationSpec Read(string json)
        {
            string? error = Mapping.ReadNotification(json, Op, Now, out AndroidNotificationSpec? spec);
            Assert.IsNull(error, json);
            return spec!;
        }

        private static string Error(string json) => Mapping.ReadNotification(json, Op, Now, out _)!;

        private static AndroidNotificationSpec With(string members) => Read($"{{\"id\":1,\"title\":\"t\",\"message\":\"m\",{Channel}{(members.Length > 0 ? "," + members : "")}}}");

        // ── Syntax ───────────────────────────────────────────────────────────────

        [TestCase(null)]
        [TestCase("")]
        [TestCase("{\"id\":")]
        [TestCase("[]")]
        [TestCase("\"text\"")]
        [TestCase("{id:1}")]
        public void Unreadable_IsOneMessage(string? json)
        {
            Assert.AreEqual("Failed to showNotification: the JSON could not be read.", Mapping.ReadNotification(json, Op, Now, out var spec));
            Assert.IsNull(spec);
            Assert.AreEqual("Failed to scheduleNotification: the JSON could not be read.", Mapping.ReadSchedule(json, "scheduleNotification", Now, out _));
            Assert.AreEqual("Failed to createChannel: the JSON could not be read.", Mapping.ReadChannel(json, "createChannel", out _));
        }

        // ── Required keys, in 1.x's order ────────────────────────────────────────

        [TestCase("{}", "channel is required.")]
        [TestCase("{\"channel\":\"c\",\"id\":1,\"title\":\"t\",\"message\":\"m\"}", "channel is required.")]
        [TestCase("{" + Channel + "}", "id is required.")]
        [TestCase("{\"id\":-2147483648," + Channel + "}", "id is required.")]
        [TestCase("{\"id\":\"1\"," + Channel + "}", "id is required.")]
        [TestCase("{\"id\":2147483648," + Channel + "}", "id is required.")]
        [TestCase("{\"id\":1," + Channel + "}", "title is required.")]
        [TestCase("{\"id\":1,\"title\":\"  \"," + Channel + "}", "title is required.")]
        [TestCase("{\"id\":1,\"title\":null," + Channel + "}", "title is required.")]
        [TestCase("{\"id\":1,\"title\":\"t\"," + Channel + "}", "message is required.")]
        [TestCase("{\"id\":1,\"title\":\"t\",\"message\":\"m\",\"channel\":{\"name\":\"C\"}}", "id is required.")]
        [TestCase("{\"id\":1,\"title\":\"t\",\"message\":\"m\",\"channel\":{\"id\":\"c\",\"name\":\" \"}}", "name is required.")]
        [TestCase("{\"id\":1,\"title\":\"t\",\"message\":\"m\"," + Channel + ",\"style\":{\"type\":\"bigText\"}}", "bigText is required.")]
        [TestCase("{\"id\":1,\"title\":\"t\",\"message\":\"m\",\"channel\":{\"name\":\"C\"},\"style\":{\"type\":\"bigText\"}}", "id is required.")]
        public void Required_AsWordedAndOrderedIn1x(string json, string detail) =>
            Assert.AreEqual($"Invalid argument for showNotification: {detail}", Error(json));

        [Test]
        public void Id_CutsAFractionTowardZero()
        {
            Assert.AreEqual(7, Read("{\"id\":7.9,\"title\":\"t\",\"message\":\"m\"," + Channel + "}").Id);
            Assert.AreEqual(-7, Read("{\"id\":-7.9,\"title\":\"t\",\"message\":\"m\"," + Channel + "}").Id);
        }

        // ── Defaults and blanks ──────────────────────────────────────────────────

        [Test]
        public void Defaults_As1x()
        {
            AndroidNotificationSpec spec = With("");
            Assert.AreEqual((1, "t", "m"), (spec.Id, spec.Title, spec.Message));
            Assert.IsNull(spec.Tag);
            Assert.IsNull(spec.SmallIconName);
            Assert.AreEqual(0, spec.Priority);
            Assert.IsTrue(spec.AutoCancel);
            Assert.IsTrue(spec.ShowTimestamp);
            Assert.IsTrue(spec.LaunchAppOnTap);
            Assert.IsFalse(spec.Ongoing || spec.IsGroupSummary || spec.OnlyAlertOnce || spec.LocalOnly || spec.Silent || spec.UsesChronometer || spec.FullScreenIntent);
            Assert.AreEqual(1, spec.Visibility);
            Assert.AreEqual(0, spec.GroupAlertBehavior);
            Assert.IsNull(spec.Color);
            Assert.IsNull(spec.Number);
            Assert.IsNull(spec.TimestampMillis);
            Assert.IsNull(spec.TimeoutAfterMillis);
            Assert.IsFalse(spec.HasProgress);
            Assert.AreEqual(AndroidNotificationStyleKind.None, spec.Style.Kind);
            Assert.IsEmpty(spec.Actions);
            Assert.IsEmpty(spec.Data);

            AndroidNotificationChannelSpec channel = spec.Channel;
            Assert.AreEqual(("c", "C", 3), (channel.Id, channel.Name, channel.Importance));
            Assert.IsTrue(channel.ShowBadge && channel.EnableLights && channel.EnableVibration);
            Assert.AreEqual(1, channel.LockscreenVisibility);
            Assert.IsNull(channel.LightColor);
            Assert.IsNull(channel.VibrationPattern);
            Assert.IsNull(channel.GroupId);
            Assert.IsNull(channel.GroupName);
        }

        [Test]
        public void BlankOrNullOrOtherTypedStrings_AreAbsent()
        {
            AndroidNotificationSpec spec = With("\"tag\":\" \",\"subText\":null,\"category\":7,\"ticker\":\"\",\"smallIcon\":{\"name\":\" \"},\"largeIcon\":\"app_icon\"");
            Assert.IsNull(spec.Tag);
            Assert.IsNull(spec.SubText);
            Assert.IsNull(spec.Category);
            Assert.IsNull(spec.Ticker);
            Assert.IsNull(spec.SmallIconName);
            Assert.IsNull(spec.LargeIconName);
        }

        [TestCase("\u0020\t\n\u000B\f\r", true)]
        [TestCase("\u001C\u001D\u001E\u001F", true)]
        [TestCase("\u00A0\u2007\u202F\u3000\u2028\u2029", true)]
        [TestCase("\u0085", false)]
        [TestCase("\u200B", false)]
        [TestCase(" a ", false)]
        [TestCase("", true)]
        public void IsBlank_AsKotlin(string text, bool blank)
        {
            // Kotlin's isBlank: Java's isWhitespace (with U+001C..U+001F) or isSpaceChar (with the
            // non-breaking spaces); not U+0085, which .NET's IsWhiteSpace has.
            Assert.AreEqual(blank, Mapping.IsBlank(text));
            Assert.AreEqual(blank ? null : text, With($"\"tag\":\"{EscapeJson(text)}\"").Tag);
        }

        private static string EscapeJson(string text) =>
            string.Concat(text.Select(c => c < 0x20 || c > 0x7E ? $"\\u{(int)c:X4}" : c.ToString()));

        [Test]
        public void OtherTypedValues_AreAbsent()
        {
            AndroidNotificationSpec spec = With("\"autoCancel\":\"false\",\"priority\":\"2\",\"number\":true,\"visibility\":null");
            Assert.IsTrue(spec.AutoCancel, "a bool string is not a bool");
            Assert.AreEqual(0, spec.Priority, "a number string is not a number");
            Assert.IsNull(spec.Number);
            Assert.AreEqual(1, spec.Visibility);
        }

        [Test]
        public void ResourceTypes_AreNotPassedOn()
        {
            AndroidNotificationSpec spec = With("\"smallIcon\":{\"name\":\"ic\",\"type\":\"drawable\"}");
            Assert.AreEqual("ic", spec.SmallIconName);
        }

        // ── Corrections 1.x applied (design J-N5) ────────────────────────────────

        [TestCase(-3, -2)]
        [TestCase(-2, -2)]
        [TestCase(2, 2)]
        [TestCase(3, 2)]
        [TestCase(100, 2)]
        public void Priority_Clamped(int given, int read) => Assert.AreEqual(read, With($"\"priority\":{given}").Priority);

        [TestCase(-2, 1)]
        [TestCase(-1, -1)]
        [TestCase(0, 0)]
        [TestCase(1, 1)]
        [TestCase(2, 1)]
        public void Visibility_OutOfRangeIsOne(int given, int read)
        {
            Assert.AreEqual(read, With($"\"visibility\":{given}").Visibility);
            Assert.AreEqual(read, With($"\"channel\":{{\"id\":\"c\",\"name\":\"C\",\"lockscreenVisibility\":{given}}}").Channel.LockscreenVisibility);
        }

        [TestCase(-1000, 3)]
        [TestCase(-1, 3)]
        [TestCase(0, 0)]
        [TestCase(4, 4)]
        [TestCase(5, 3)]
        public void Importance_OutOfRangeIsThree(int given, int read) =>
            Assert.AreEqual(read, With($"\"channel\":{{\"id\":\"c\",\"name\":\"C\",\"importance\":{given}}}").Channel.Importance);

        [TestCase("{\"max\":100,\"current\":150}", 100, 100)]
        [TestCase("{\"max\":100,\"current\":-5}", 100, 0)]
        [TestCase("{\"max\":-10,\"current\":5}", 0, 0)]
        [TestCase("{\"max\":100,\"current\":40}", 100, 40)]
        [TestCase("{}", 0, 0)]
        public void Progress_Clamped(string progress, int max, int current)
        {
            AndroidNotificationSpec spec = With($"\"progress\":{progress}");
            Assert.IsTrue(spec.HasProgress);
            Assert.AreEqual((max, current), (spec.ProgressMax, spec.ProgressCurrent));
        }

        [Test]
        public void Progress_Indeterminate_IsNotClamped()
        {
            AndroidNotificationSpec spec = With("\"progress\":{\"max\":-10,\"current\":150,\"indeterminate\":true}");
            Assert.AreEqual((-10, 150, true), (spec.ProgressMax, spec.ProgressCurrent, spec.ProgressIndeterminate));
        }

        [Test]
        public void ChannelGroupName_DefaultsToTheGroupId()
        {
            Assert.AreEqual(("g", "g"), Group("\"groupId\":\"g\""));
            Assert.AreEqual(("g", "G"), Group("\"groupId\":\"g\",\"groupName\":\"G\""));
            Assert.AreEqual(((string?)null, (string?)null), Group("\"groupName\":\"G\""));
            (string?, string?) Group(string members)
            {
                AndroidNotificationChannelSpec channel = With($"\"channel\":{{\"id\":\"c\",\"name\":\"C\",{members}}}").Channel;
                return (channel.GroupId, channel.GroupName);
            }
        }

        // ── Colors (design 4.2) ──────────────────────────────────────────────────

        [TestCase("-1", 0xFFFFFFFFu)]
        [TestCase("-2147483648", 0x80000000u)]
        [TestCase("2147483648", 0x80000000u)]
        [TestCase("4294967295", 0xFFFFFFFFu)]
        [TestCase("4283215696", 0xFF4CAF50u)]
        [TestCase("0", 0u)]
        public void Color_IntOrUintRange(string value, uint read)
        {
            Assert.AreEqual(read, With($"\"color\":{value}").Color);
            Assert.AreEqual(read, With($"\"channel\":{{\"id\":\"c\",\"name\":\"C\",\"lightColor\":{value}}}").Channel.LightColor);
        }

        [TestCase("-2147483649")]
        [TestCase("4294967296")]
        [TestCase("\"#FF0000\"")]
        [TestCase("null")]
        public void Color_OutOfRange_IsAbsent(string value)
        {
            Assert.IsNull(With($"\"color\":{value}").Color);
            Assert.IsNull(With($"\"channel\":{{\"id\":\"c\",\"name\":\"C\",\"lightColor\":{value}}}").Channel.LightColor);
        }

        // ── Dropped entries ──────────────────────────────────────────────────────

        [Test]
        public void Actions_WithoutTitleOrActionId_AreDropped()
        {
            AndroidNotificationSpec spec = With("\"actions\":[{\"title\":\"A\",\"actionId\":\"a\"},{\"title\":\"B\"},{\"actionId\":\"c\"},{\"title\":\" \",\"actionId\":\"d\"},\"e\",null,"
                + "{\"title\":\"F\",\"actionId\":\"f\",\"icon\":{\"name\":\"ic\"},\"launchApp\":true,\"allowGeneratedReplies\":true,\"semanticAction\":10,\"contextual\":true,\"showsUserInterface\":false}]");
            Assert.AreEqual(new[] { "a", "f" }, spec.Actions.Select(a => a.ActionId).ToArray());
            AndroidNotificationActionSpec first = spec.Actions[0], last = spec.Actions[1];
            Assert.IsNull(first.IconName);
            Assert.IsFalse(first.LaunchApp || first.AllowGeneratedReplies || first.Contextual);
            Assert.AreEqual(0, first.SemanticAction);
            Assert.IsTrue(first.ShowsUserInterface);
            Assert.AreEqual(("F", "ic", true, true, 10, true, false),
                (last.Title, last.IconName, last.LaunchApp, last.AllowGeneratedReplies, last.SemanticAction, last.Contextual, last.ShowsUserInterface));
        }

        [Test]
        public void Data_KeepsStringValuesOnly()
        {
            AndroidNotificationSpec spec = With("\"data\":{\"a\":\"1\",\"b\":2,\"c\":null,\"d\":true,\"e\":\"q\\\"/\"}");
            CollectionAssert.AreEqual(new[] { new KeyValuePair<string, string>("a", "1"), new KeyValuePair<string, string>("e", "q\"/") }, spec.Data);
            Assert.IsEmpty(With("\"data\":{}").Data);
        }

        [Test]
        public void VibrationPattern_DropsWhatIsNotANumber()
        {
            CollectionAssert.AreEqual(new long[] { 0, 100, 50 },
                With("\"channel\":{\"id\":\"c\",\"name\":\"C\",\"vibrationPattern\":[0,\"x\",100,null,50.7]}").Channel.VibrationPattern);
            Assert.IsNull(With("\"channel\":{\"id\":\"c\",\"name\":\"C\",\"vibrationPattern\":[\"x\"]}").Channel.VibrationPattern);
        }

        // ── Styles ───────────────────────────────────────────────────────────────

        [TestCase("{}")]
        [TestCase("{\"type\":\" \"}")]
        [TestCase("{\"type\":\"default\"}")]
        [TestCase("{\"type\":\"unknown\"}")]
        [TestCase("{\"type\":\"bigPicture\"}")]
        [TestCase("{\"type\":\"bigPicture\",\"picture\":{\"name\":\" \"},\"pictureUri\":\"\"}")]
        [TestCase("{\"type\":\"decoratedCustomView\"}")]
        [TestCase("{\"type\":\"decoratedCustomView\",\"customViewLayout\":\" \"}")]
        [TestCase("{\"type\":\"decoratedCustomView\",\"customViewLayout\":null}")]
        public void Style_DefaultsToNone(string style) => Assert.AreEqual(AndroidNotificationStyleKind.None, With($"\"style\":{style}").Style.Kind);

        [Test]
        public void Style_BigText()
        {
            AndroidNotificationStyleSpec style = With("\"style\":{\"type\":\"bigText\",\"bigText\":\"B\",\"summaryText\":\"S\",\"bigContentTitle\":\" \"}").Style;
            Assert.AreEqual((AndroidNotificationStyleKind.BigText, "B", "S", (string?)null), (style.Kind, style.BigText, style.SummaryText, style.BigContentTitle));
        }

        [Test]
        public void Style_Inbox_DropsBlankLines()
        {
            AndroidNotificationStyleSpec style = With("\"style\":{\"type\":\"inbox\",\"lines\":[\"a\",\" \",null,3,\"b\"]}").Style;
            Assert.AreEqual(AndroidNotificationStyleKind.Inbox, style.Kind);
            CollectionAssert.AreEqual(new[] { "a", "b" }, style.Lines);
        }

        [Test]
        public void Style_BigPicture()
        {
            AndroidNotificationStyleSpec style = With("\"style\":{\"type\":\"bigPicture\",\"pictureUri\":\"content://p\",\"largeIcon\":{\"name\":\"li\"},\"hideExpandedLargeIcon\":true}").Style;
            Assert.AreEqual((AndroidNotificationStyleKind.BigPicture, (string?)null, "content://p", "li", true),
                (style.Kind, style.PictureName, style.PictureUri, style.LargeIconName, style.HideExpandedLargeIcon));
        }

        [TestCase("", -1)]
        [TestCase(",\"isGroupConversation\":null", 0)]
        [TestCase(",\"isGroupConversation\":\"true\"", 0)]
        [TestCase(",\"isGroupConversation\":false", 0)]
        [TestCase(",\"isGroupConversation\":true", 1)]
        public void Style_Messaging_GroupConversation(string member, int read) =>
            Assert.AreEqual(read, With($"\"style\":{{\"type\":\"messaging\"{member}}}").Style.GroupConversation);

        [Test]
        public void Style_Messaging_NamesAndTimes()
        {
            AndroidNotificationStyleSpec style = With("\"style\":{\"type\":\"messaging\",\"conversationTitle\":\"T\",\"messages\":["
                + "{\"text\":\"a\",\"timestampMillis\":5,\"senderName\":\"S\"},{\"text\":\" \"},{\"senderName\":\"X\"},{\"text\":\"b\"}]}").Style;
            Assert.AreEqual(("You", "T"), (style.UserDisplayName, style.ConversationTitle));
            Assert.AreEqual(2, style.Messages.Count);
            Assert.AreEqual(("a", 5L, "S"), (style.Messages[0].Text, style.Messages[0].TimestampMillis, style.Messages[0].SenderName));
            Assert.AreEqual(("b", Now, (string?)null), (style.Messages[1].Text, style.Messages[1].TimestampMillis, style.Messages[1].SenderName));
        }

        [Test]
        public void Style_CustomView_KeepsClickIntentsWithBothIds()
        {
            AndroidNotificationStyleSpec style = With("\"style\":{\"type\":\"decoratedCustomView\",\"customViewLayout\":\"l\",\"bigCustomViewLayout\":\" \",\"viewActions\":["
                + "{\"type\":\"setClickIntent\",\"viewId\":\"v\",\"actionId\":\"a\"},{\"type\":\"other\",\"viewId\":\"v\",\"actionId\":\"a\"},"
                + "{\"type\":\"setClickIntent\",\"viewId\":\"v\"},{\"type\":\"setClickIntent\",\"actionId\":\"a\"}]}").Style;
            Assert.AreEqual((AndroidNotificationStyleKind.CustomView, "l", (string?)null), (style.Kind, style.Layout, style.BigLayout));
            CollectionAssert.AreEqual(new[] { new KeyValuePair<string, string>("v", "a") }, style.ViewClicks);
        }

        // ── Schedules and channels ───────────────────────────────────────────────

        [TestCase("{}", "notification is required.")]
        [TestCase("{\"notification\":{}}", "schedule is required.")]
        [TestCase("{\"notification\":{},\"schedule\":{}}", "channel is required.")]
        [TestCase("{\"notification\":{\"id\":1,\"title\":\"t\",\"message\":\"m\"," + Channel + "},\"schedule\":{}}", "schedule.triggerAtMillis must be greater than 0.")]
        [TestCase("{\"notification\":{\"id\":1,\"title\":\"t\",\"message\":\"m\"," + Channel + "},\"schedule\":{\"triggerAtMillis\":0}}", "schedule.triggerAtMillis must be greater than 0.")]
        [TestCase("{\"notification\":{\"id\":1,\"title\":\"t\",\"message\":\"m\"," + Channel + "},\"schedule\":{\"triggerAtMillis\":\"5\"}}", "schedule.triggerAtMillis must be greater than 0.")]
        public void Schedule_Required(string json, string detail) =>
            Assert.AreEqual($"Invalid argument for scheduleNotification: {detail}", Mapping.ReadSchedule(json, "scheduleNotification", Now, out _));

        [Test]
        public void Schedule_Defaults()
        {
            Assert.IsNull(Mapping.ReadSchedule("{\"notification\":{\"id\":1,\"title\":\"t\",\"message\":\"m\"," + Channel + "},\"schedule\":{\"triggerAtMillis\":9}}",
                "scheduleNotification", Now, out AndroidNotificationScheduleSpec? spec));
            Assert.AreEqual((9L, true, true, true, 0), (spec!.TriggerAtMillis, spec.Exact, spec.AllowWhileIdle, spec.PersistAcrossBoot, spec.AlarmType));
            Assert.AreEqual(1, spec.Notification.Id);
        }

        [Test]
        public void Channel_Required()
        {
            Assert.AreEqual("Invalid argument for createChannel: id is required.", Mapping.ReadChannel("{\"name\":\"N\"}", "createChannel", out _));
            Assert.AreEqual("Invalid argument for createChannel: name is required.", Mapping.ReadChannel("{\"id\":\"i\"}", "createChannel", out _));
            Assert.IsNull(Mapping.ReadChannel("{\"id\":\"i\",\"name\":\"N\",\"description\":\"D\",\"soundUri\":\"s\",\"showBadge\":false}", "createChannel", out var spec));
            Assert.AreEqual(("i", "N", "D", "s", false), (spec!.Id, spec.Name, spec.Description, spec.SoundUri, spec.ShowBadge));
        }

        // ── What the builder writes, read back (design 7.1) ──────────────────────

        private static IEnumerable<AndroidNotificationPayload> Payloads()
        {
            AndroidNotificationResourcePayload Icon() => new() { name = "app_icon", type = "mipmap" };
            // The three payloads of AndroidNotificationJsonBuilderTests.
            yield return new AndroidNotificationPayload
            {
                id = 101, title = "Boss Alert", message = "The world boss has spawned.", tag = "boss",
                channel = new AndroidChannelPayload { id = "gameplay", name = "Gameplay", importance = 4, groupId = "events", groupName = "Events" },
                smallIcon = Icon(), largeIcon = Icon(), subText = "Limited Event", category = "alarm", ticker = "Boss alert", number = 7,
                color = unchecked((int)0xFF4CAF50), groupKey = "live-events", sortKey = "boss-001", timeoutAfterMillis = 30000,
                launchAction = "open_boss_screen",
                style = new AndroidNotificationStylePayload { type = "bigPicture", picture = Icon(), largeIcon = Icon(), bigContentTitle = "Boss Alert", summaryText = "Limited Event" },
                actions = new[] { new AndroidNotificationActionPayload { title = "Join", actionId = "action.join", icon = Icon(), launchApp = true, semanticAction = 10, contextual = true } },
                data = new[] { new AndroidNotificationDataEntryPayload { key = "screen", value = "boss" }, new AndroidNotificationDataEntryPayload { key = "bossId", value = "boss_001" } },
            };
            yield return new AndroidNotificationPayload { id = 202, title = "Guild Battle", message = "Starts soon.", channel = new AndroidChannelPayload { id = "events", name = "Events" } };
            yield return new AndroidNotificationPayload { id = 303, title = "Simple", message = "Minimal payload.", channel = new AndroidChannelPayload { id = "default", name = "Default" } };
            // The sample's kinds: bigText with a priority, a progress, messaging, inbox, a custom view, a full screen.
            yield return new AndroidNotificationPayload
            {
                id = 1101, title = "Energy Refilled", message = "Rested.", tag = "energy", channel = new AndroidChannelPayload { id = "native_toolkit_gameplay", name = "Gameplay Alerts" },
                smallIcon = Icon(), priority = 1, category = "recommendation", number = 3, timestampMillis = 1_700_000_000_123,
                style = new AndroidNotificationStylePayload { type = "bigText", bigText = "Big", bigContentTitle = "Energy Refilled", summaryText = "Raid Ready" },
            };
            yield return new AndroidNotificationPayload
            {
                id = 1301, title = "Downloading", message = "Arena.", channel = new AndroidChannelPayload { id = "native_toolkit_gameplay", name = "Gameplay Alerts", lightColor = -16711936, vibrationPattern = new long[] { 0, 250 }, lockscreenVisibility = 0 },
                ongoing = true, autoCancel = false, onlyAlertOnce = true, progress = new AndroidNotificationProgressPayload { max = 100, current = 15 },
            };
            yield return new AndroidNotificationPayload
            {
                id = 1401, title = "Chat", message = "Hi", channel = new AndroidChannelPayload { id = "c", name = "C" }, launchAppOnTap = false, fullScreenIntent = true, visibility = -1,
                style = new AndroidNotificationStylePayload { type = "messaging", userDisplayName = "Me", conversationTitle = "Team", isGroupConversation = true,
                    messages = new[] { new AndroidNotificationMessagePayload { text = "one", timestampMillis = 10, senderName = "A" } } },
            };
            yield return new AndroidNotificationPayload
            {
                id = 1501, title = "Inbox", message = "3 new", channel = new AndroidChannelPayload { id = "c", name = "C" },
                style = new AndroidNotificationStylePayload { type = "inbox", lines = new[] { "a", "b" }, summaryText = "S" },
            };
            yield return new AndroidNotificationPayload
            {
                id = 1601, title = "Custom", message = "View", channel = new AndroidChannelPayload { id = "c", name = "C" },
                style = new AndroidNotificationStylePayload { type = "decoratedCustomView", customViewLayout = "notification_custom", bigCustomViewLayout = "notification_custom_big",
                    viewActions = new[] { new AndroidNotificationViewActionPayload { type = "setClickIntent", viewId = "dismiss_button", actionId = "dismiss" } } },
            };
        }

        [TestCaseSource(nameof(Payloads))]
        public void BuilderJson_ReadsBackToThePayload(AndroidNotificationPayload payload)
        {
            AndroidNotificationSpec spec = Read(AndroidNotificationJsonBuilder.BuildNotificationJson(payload));
            Assert.AreEqual((payload.id, payload.title, payload.message, payload.tag), (spec.Id, spec.Title, spec.Message, spec.Tag));
            Assert.AreEqual((payload.smallIcon?.name, payload.largeIcon?.name), (spec.SmallIconName, spec.LargeIconName));
            Assert.AreEqual((payload.subText, payload.soundUri, payload.category, payload.ticker, payload.groupKey, payload.sortKey),
                (spec.SubText, spec.SoundUri, spec.Category, spec.Ticker, spec.GroupKey, spec.SortKey));
            Assert.AreEqual((payload.priority, payload.visibility, payload.groupAlertBehavior, payload.number),
                (spec.Priority, spec.Visibility, spec.GroupAlertBehavior, spec.Number));
            Assert.AreEqual(payload.color == null ? (uint?)null : unchecked((uint)payload.color.Value), spec.Color);
            Assert.AreEqual((payload.timestampMillis, payload.timeoutAfterMillis), (spec.TimestampMillis, spec.TimeoutAfterMillis));
            Assert.AreEqual((payload.autoCancel, payload.showTimestamp, payload.ongoing, payload.isGroupSummary, payload.onlyAlertOnce, payload.localOnly,
                    payload.silent, payload.usesChronometer, payload.launchAppOnTap, payload.fullScreenIntent),
                (spec.AutoCancel, spec.ShowTimestamp, spec.Ongoing, spec.IsGroupSummary, spec.OnlyAlertOnce, spec.LocalOnly,
                    spec.Silent, spec.UsesChronometer, spec.LaunchAppOnTap, spec.FullScreenIntent));
            Assert.AreEqual(payload.progress != null, spec.HasProgress);
            if (payload.progress != null)
                Assert.AreEqual((payload.progress.max, payload.progress.current, payload.progress.indeterminate), (spec.ProgressMax, spec.ProgressCurrent, spec.ProgressIndeterminate));
            CollectionAssert.AreEqual(payload.data.Select(d => new KeyValuePair<string, string>(d.key, d.value)), spec.Data);
            CollectionAssert.AreEqual(payload.actions.Select(a => (a.title, a.actionId, a.icon?.name, a.launchApp, a.allowGeneratedReplies, a.semanticAction, a.contextual, a.showsUserInterface)),
                spec.Actions.Select(a => (a.Title, a.ActionId, a.IconName, a.LaunchApp, a.AllowGeneratedReplies, a.SemanticAction, a.Contextual, a.ShowsUserInterface)));

            AndroidChannelPayload c = payload.channel;
            AndroidNotificationChannelSpec channel = spec.Channel;
            Assert.AreEqual((c.id, c.name, c.importance, c.description, c.showBadge, c.enableLights, c.enableVibration, c.soundUri, c.lockscreenVisibility, c.groupId),
                (channel.Id, channel.Name, channel.Importance, channel.Description, channel.ShowBadge, channel.EnableLights, channel.EnableVibration, channel.SoundUri, channel.LockscreenVisibility, channel.GroupId));
            Assert.AreEqual(c.groupId == null ? null : c.groupName ?? c.groupId, channel.GroupName);
            Assert.AreEqual(c.lightColor == null ? (uint?)null : unchecked((uint)c.lightColor.Value), channel.LightColor);
            Assert.AreEqual(c.vibrationPattern, channel.VibrationPattern);

            AndroidNotificationStylePayload? s = payload.style;
            AndroidNotificationStyleSpec style = spec.Style;
            switch (s?.type)
            {
                case "bigText":
                    Assert.AreEqual((AndroidNotificationStyleKind.BigText, s.bigText, s.summaryText, s.bigContentTitle), (style.Kind, style.BigText, style.SummaryText, style.BigContentTitle));
                    break;
                case "bigPicture":
                    Assert.AreEqual((AndroidNotificationStyleKind.BigPicture, s.picture?.name, s.pictureUri, s.largeIcon?.name, s.hideExpandedLargeIcon, s.summaryText, s.bigContentTitle),
                        (style.Kind, style.PictureName, style.PictureUri, style.LargeIconName, style.HideExpandedLargeIcon, style.SummaryText, style.BigContentTitle));
                    break;
                case "messaging":
                    Assert.AreEqual((AndroidNotificationStyleKind.Messaging, s.userDisplayName, s.conversationTitle, s.isGroupConversation == true ? 1 : 0),
                        (style.Kind, style.UserDisplayName, style.ConversationTitle, style.GroupConversation));
                    CollectionAssert.AreEqual(s.messages.Select(m => (m.text, m.timestampMillis ?? Now, m.senderName)), style.Messages.Select(m => (m.Text, m.TimestampMillis, m.SenderName)));
                    break;
                case "inbox":
                    Assert.AreEqual((AndroidNotificationStyleKind.Inbox, s.summaryText), (style.Kind, style.SummaryText));
                    CollectionAssert.AreEqual(s.lines, style.Lines);
                    break;
                case "decoratedCustomView":
                    Assert.AreEqual((AndroidNotificationStyleKind.CustomView, s.customViewLayout, s.bigCustomViewLayout), (style.Kind, style.Layout, style.BigLayout));
                    CollectionAssert.AreEqual(s.viewActions.Select(v => new KeyValuePair<string, string>(v.viewId, v.actionId!)), style.ViewClicks);
                    break;
                default:
                    Assert.AreEqual(AndroidNotificationStyleKind.None, style.Kind);
                    break;
            }
        }

        [Test]
        public void BuilderScheduleJson_ReadsBackToThePayload()
        {
            string json = AndroidNotificationJsonBuilder.BuildScheduledNotificationJson(new AndroidScheduledNotificationEnvelopePayload
            {
                notification = new AndroidNotificationPayload { id = 202, title = "Guild Battle", message = "Starts soon.", channel = new AndroidChannelPayload { id = "events", name = "Events" } },
                schedule = new AndroidNotificationSchedulePayload { triggerAtMillis = 1714377600000, exact = false, allowWhileIdle = false, persistAcrossBoot = false, alarmType = 1 },
            });
            Assert.IsNull(Mapping.ReadSchedule(json, "scheduleNotification", Now, out AndroidNotificationScheduleSpec? spec));
            Assert.AreEqual((1714377600000L, false, false, false, 1), (spec!.TriggerAtMillis, spec.Exact, spec.AllowWhileIdle, spec.PersistAcrossBoot, spec.AlarmType));
            Assert.AreEqual(202, spec.Notification.Id);
        }

        [Test]
        public void BuilderChannelJson_ReadsBackToThePayload()
        {
            string json = AndroidNotificationJsonBuilder.BuildChannelJson(new AndroidChannelPayload
            {
                id = "ch", name = "Channel", importance = 2, description = "D", showBadge = false, enableLights = false, lightColor = 255,
                enableVibration = false, vibrationPattern = new long[] { 1, 2 }, soundUri = "s", lockscreenVisibility = -1, groupId = "g", groupName = "G",
            });
            Assert.IsNull(Mapping.ReadChannel(json, "createChannel", out AndroidNotificationChannelSpec? c));
            Assert.AreEqual(("ch", "Channel", 2, "D", false, false, (uint?)255u, false, "s", -1, "g", "G"),
                (c!.Id, c.Name, c.Importance, c.Description, c.ShowBadge, c.EnableLights, c.LightColor, c.EnableVibration, c.SoundUri, c.LockscreenVisibility, c.GroupId, c.GroupName));
            CollectionAssert.AreEqual(new long[] { 1, 2 }, c.VibrationPattern);
        }
    }
}
#endif
