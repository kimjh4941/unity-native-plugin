#nullable enable

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using JonghyunKim.NativeToolkit.Runtime.Windows.Notification;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static JonghyunKim.NativeToolkit.Runtime.Windows.Notification.WindowsNotificationCApi;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// EditMode tests for the JSON reader the Windows notification payloads go through
    /// (artifact/features/notification/designs/2026-09-27-windows-notification-design-v8.md, 4.2 and 7.1).
    /// </summary>
    public sealed class WindowsNotificationJsonReaderTests
    {
        private static WindowsJsonValue Parse(string json)
        {
            WindowsJsonValue? value = WindowsNotificationJsonReader.Parse(json);
            Assert.IsNotNull(value, $"refused: {json}");
            return value!;
        }

        [TestCase("")]
        [TestCase("{")]
        [TestCase("{\"a\":}")]
        [TestCase("{\"a\":1,}")]
        [TestCase("{'a':1}")]
        [TestCase("{\"a\":01}")]
        [TestCase("{\"a\":1.}")]
        [TestCase("{\"a\":NaN}")]
        [TestCase("{\"a\":\"\\x\"}")]
        [TestCase("{\"a\":\"\\u12\"}")]
        [TestCase("{\"a\":\"line\nbreak\"}")]
        [TestCase("{\"a\":1} x")]
        public void Parse_RefusesWhatIsNotJson(string json)
        {
            Assert.IsNull(WindowsNotificationJsonReader.Parse(json));
        }

        [Test]
        public void Parse_OfNull_IsNull()
        {
            Assert.IsNull(WindowsNotificationJsonReader.Parse(null));
        }

        [Test]
        public void Parse_ReadsEscapes()
        {
            Parse("{\"a\":\"q\\\" b\\\\ s\\/ \\b\\f\\n\\r\\t \\u0041\"}").TryGetMember("a", out WindowsJsonValue a);
            Assert.IsTrue(a.TryGetString(out string text));
            Assert.AreEqual("q\" b\\ s/ \b\f\n\r\t A", text);
        }

        [Test]
        public void Parse_KeepsSurrogatePairs_AndUnpairedOnes()
        {
            Parse("{\"a\":\"\\uD842\\uDFB7𠮷\",\"b\":\"\\uD800\"}").TryGetMember("a", out WindowsJsonValue a);
            a.TryGetString(out string pair);
            Assert.AreEqual("𠮷𠮷", pair);
            Parse("{\"b\":\"\\uD800\"}").TryGetMember("b", out WindowsJsonValue b);
            b.TryGetString(out string unpaired);
            Assert.AreEqual("\uD800", unpaired);
        }

        [TestCase("1e400")]
        [TestCase("-1e400")]
        [TestCase("1e-400")]
        public void Parse_ANumberADoubleCannotHold_FailsTheDocument_EvenUnderAnUnreadKey(string number)
        {
            Assert.IsNull(WindowsNotificationJsonReader.Parse($"{{\"unread\":{number}}}"));
        }

        [Test]
        public void Parse_ADecimalThatRoundsToZero_FailsTheDocument()
        {
            string tiny = "0." + new string('0', 323) + "1";
            Assert.IsNull(WindowsNotificationJsonReader.Parse($"{{\"a\":{tiny}}}"));
        }

        [TestCase("0", 0.0)]
        [TestCase("-0", 0.0)]
        [TestCase("0e5", 0.0)]
        [TestCase("0.000", 0.0)]
        [TestCase("-12.5e1", -125.0)]
        [TestCase("1E+2", 100.0)]
        public void Parse_ReadsNumbers(string number, double expected)
        {
            Parse($"{{\"a\":{number}}}").TryGetMember("a", out WindowsJsonValue a);
            Assert.IsTrue(a.TryGetNumber(out double value));
            Assert.AreEqual(expected, value);
        }

        [Test]
        public void Parse_Nests512Containers_ButNot513()
        {
            Assert.IsNotNull(WindowsNotificationJsonReader.Parse(new string('[', 512) + new string(']', 512)));
            Assert.IsNull(WindowsNotificationJsonReader.Parse(new string('[', 513) + new string(']', 513)));
        }

        [Test]
        public void Parse_Nests512Objects()
        {
            string objects = string.Concat(Enumerable.Repeat("{\"a\":", 511)) + "{}" + new string('}', 511);
            Assert.IsNotNull(WindowsNotificationJsonReader.Parse(objects));
            Assert.IsNull(WindowsNotificationJsonReader.Parse("{\"a\":" + objects + "}"));
        }

        [Test]
        public void Parse_ADuplicateKey_KeepsTheLaterValue_InTheFirstPlace()
        {
            WindowsJsonValue root = Parse("{\"a\":1,\"b\":2,\"a\":3}");
            CollectionAssert.AreEqual(new[] { "a", "b" }, root.Members.Select(m => m.Key));
            root.TryGetMember("a", out WindowsJsonValue a);
            a.TryGetNumber(out double value);
            Assert.AreEqual(3.0, value);
        }

        [Test]
        public void TryGetMember_TellsAnAbsentKeyFromANullValue()
        {
            WindowsJsonValue root = Parse("{\"present\":null}");
            Assert.IsTrue(root.TryGetMember("present", out WindowsJsonValue present));
            Assert.IsTrue(present.IsNull);
            Assert.IsFalse(root.TryGetMember("absent", out _));
        }

        [Test]
        public void Members_AreInDocumentOrder()
        {
            CollectionAssert.AreEqual(new[] { "z", "a", "m" }, Parse("{\"z\":1,\"a\":2,\"m\":3}").Members.Select(m => m.Key));
        }

        [Test]
        public void Parse_TakesAnyTopLevelValue()
        {
            Assert.AreEqual(WindowsJsonValueKind.Array, Parse(" [1, true, null, \"s\"] ").Kind);
            Assert.AreEqual(WindowsJsonValueKind.Null, Parse("null").Kind);
        }
    }

    /// <summary>EditMode tests for the notification payload read as builder calls (design v8, 4.2 and 7.1).</summary>
    public sealed class WindowsNotificationContentPlanTests
    {
        private static string[] Plan(string json)
        {
            int error = PlanContent(json, out List<ContentStep> steps);
            Assert.AreEqual(ErrorNone, error, $"refused: {json}");
            return steps.Select(s => s.ToString()).ToArray();
        }

        private static int Error(string? json) => PlanContent(json, out _);

        [Test]
        public void AnEmptyObject_CallsNothing() => CollectionAssert.IsEmpty(Plan("{}"));

        [TestCase(null)]
        [TestCase("")]
        [TestCase("not json")]
        [TestCase("[]")]
        [TestCase("\"text\"")]
        [TestCase("null")]
        [TestCase("{\"unread\":1e400}")]
        public void NotAJsonObject_Is3(string? json) => Assert.AreEqual(ErrorInvalidPayload, Error(json));

        [Test]
        public void EveryTopLevelKey_InTheOrder1xCalledThem()
        {
            string json = @"{
                ""expiresOnReboot"": true, ""expiration"": 60.9, ""timestamp"": 1700000000.7, ""attribution"": ""via test"",
                ""progress"": {""title"": ""Download"", ""value"": 0.25, ""valueStr"": ""25%"", ""status"": ""Working""},
                ""audio"": {""type"": ""uri"", ""uri"": ""ms-appx:///a.wav"", ""loop"": true},
                ""inlineImage"": ""file:///i.png"", ""heroImage"": ""file:///h.png"", ""appLogo"": {""uri"": ""file:///l.png"", ""crop"": ""circle""},
                ""comboBoxes"": [{""id"": ""c"", ""title"": ""Pick"", ""defaultSelection"": ""x"", ""items"": [{""id"": ""x"", ""label"": ""X""}, {""id"": ""y"", ""label"": ""Y""}]}],
                ""textBoxes"": [{""id"": ""t"", ""placeholder"": ""Type"", ""title"": ""Reply""}],
                ""buttons"": [{""label"": ""Open"", ""args"": {""action"": ""open"", ""id"": ""7""}}, {""label"": ""Docs"", ""invokeUri"": ""https://example.com""}],
                ""duration"": ""long"", ""scenario"": ""reminder"", ""group"": ""g"", ""tag"": ""t"", ""body"": ""B"", ""title"": ""T""
            }";
            CollectionAssert.AreEqual(new[]
            {
                "SetTitle(\"T\")", "SetBody(\"B\")", "SetTag(\"t\")", "SetGroup(\"g\")", "SetScenario(1)", "SetDuration(1)",
                "AddButton(\"Open\", NULL, 1, 0)", "AddButtonArgument(0, \"action\", \"open\")", "AddButtonArgument(0, \"id\", \"7\")",
                "AddButton(\"Docs\", \"https://example.com\", 0, 1)",
                "AddTextInput(\"t\", \"Type\", \"Reply\")",
                "AddCombo(\"c\", \"Pick\", \"x\", 0)", "AddComboItem(0, \"x\", \"X\")", "AddComboItem(0, \"y\", \"Y\")",
                "SetAppLogo(\"file:///l.png\", 1)", "SetHeroImage(\"file:///h.png\")", "SetInlineImage(\"file:///i.png\")",
                "SetAudio(2, NULL, \"ms-appx:///a.wav\", 1)",
                "SetProgress(\"Download\", 0.25, \"25%\", \"Working\")",
                "SetAttribution(\"via test\")", "SetTimestamp(1700000000000)", "SetExpiration(60)", "SetExpiresOnReboot(1)",
            }, Plan(json));
        }

        [TestCase("reminder", "SetScenario(1)")]
        [TestCase("alarm", "SetScenario(2)")]
        [TestCase("urgent", "SetScenario(3)")]
        [TestCase("incomingCall", "SetScenario(4)")]
        public void Scenario_ByName(string scenario, string call) =>
            CollectionAssert.AreEqual(new[] { call }, Plan($"{{\"scenario\":\"{scenario}\"}}"));

        [TestCase("{\"scenario\":\"default\"}")]
        [TestCase("{\"scenario\":\"Reminder\"}")]
        [TestCase("{\"duration\":\"short\"}")]
        [TestCase("{\"duration\":\"LONG\"}")]
        [TestCase("{\"expiresOnReboot\":false}")]
        public void UnknownValues_CallNothing(string json) => CollectionAssert.IsEmpty(Plan(json));

        [TestCase("{\"appLogo\":{\"uri\":\"u\"}}", "SetAppLogo(\"u\", 0)")]
        [TestCase("{\"appLogo\":{\"uri\":\"u\",\"crop\":\"square\"}}", "SetAppLogo(\"u\", 0)")]
        [TestCase("{\"audio\":{}}", "SetAudio(0, NULL, NULL, 0)")]
        [TestCase("{\"audio\":{\"type\":\"event\",\"event\":\"alarm\"}}", "SetAudio(0, \"alarm\", NULL, 0)")]
        [TestCase("{\"audio\":{\"type\":\"other\",\"event\":\"loopingCall\"}}", "SetAudio(0, \"loopingCall\", NULL, 0)")]
        [TestCase("{\"audio\":{\"type\":\"mute\"}}", "SetAudio(1, NULL, NULL, 0)")]
        [TestCase("{\"progress\":{}}", "SetProgress(NULL, 0, NULL, NULL)")]
        public void Defaults(string json, string call) => CollectionAssert.AreEqual(new[] { call }, Plan(json));

        [TestCase("{\"textBoxes\":[{\"id\":\"a\"}]}", "AddTextInput(\"a\", NULL, NULL)")]
        [TestCase("{\"textBoxes\":[{\"id\":\"a\",\"placeholder\":\"p\"}]}", "AddTextInput(\"a\", \"p\", \"\")")]
        [TestCase("{\"textBoxes\":[{\"id\":\"a\",\"title\":\"t\"}]}", "AddTextInput(\"a\", \"\", \"t\")")]
        public void TextBoxes_AreNullWithNeither_AndEmptyForTheMissingOne(string json, string call) =>
            CollectionAssert.AreEqual(new[] { call }, Plan(json));

        [Test]
        public void ArgsAndInvokeUri_BothGoThrough_ForTheNativeShowToRefuse() =>
            CollectionAssert.AreEqual(new[] { "AddButton(\"B\", \"u\", 1, 0)", "AddButtonArgument(0, \"k\", \"v\")" },
                Plan("{\"buttons\":[{\"label\":\"B\",\"args\":{\"k\":\"v\"},\"invokeUri\":\"u\"}]}"));

        [Test]
        public void SixButtons_GoThrough_ForTheNativeShowToRefuse() =>
            Assert.AreEqual(6, Plan("{\"buttons\":[" + string.Join(",", Enumerable.Repeat("{\"label\":\"b\"}", 6)) + "]}").Length);

        [Test]
        public void ButtonAndComboIndexes_Count() =>
            CollectionAssert.AreEqual(new[]
            {
                "AddButton(\"a\", NULL, 0, 0)", "AddButton(\"b\", NULL, 1, 1)", "AddButtonArgument(1, \"k\", \"v\")",
                "AddCombo(\"c1\", NULL, NULL, 0)", "AddCombo(\"c2\", NULL, NULL, 1)", "AddComboItem(1, \"i\", \"I\")",
            }, Plan("{\"buttons\":[{\"label\":\"a\"},{\"label\":\"b\",\"args\":{\"k\":\"v\"}}],\"comboBoxes\":[{\"id\":\"c1\"},{\"id\":\"c2\",\"items\":[{\"id\":\"i\",\"label\":\"I\"}]}]}"));

        [Test]
        public void ButtonArguments_KeepTheDocumentOrder() =>
            CollectionAssert.AreEqual(new[] { "AddButton(\"b\", NULL, 1, 0)", "AddButtonArgument(0, \"z\", \"1\")", "AddButtonArgument(0, \"a\", \"2\")" },
                Plan("{\"buttons\":[{\"label\":\"b\",\"args\":{\"z\":\"1\",\"a\":\"2\"}}]}"));

        [TestCase("{\"buttons\":[{}]}")]
        [TestCase("{\"buttons\":[{\"label\":1}]}")]
        [TestCase("{\"buttons\":[{\"label\":\"b\",\"args\":{\"k\":1}}]}")]
        [TestCase("{\"buttons\":[{\"label\":\"b\",\"args\":[]}]}")]
        [TestCase("{\"textBoxes\":[{\"placeholder\":\"p\"}]}")]
        [TestCase("{\"comboBoxes\":[{\"title\":\"t\"}]}")]
        [TestCase("{\"comboBoxes\":[{\"id\":\"c\",\"items\":[{\"id\":\"i\"}]}]}")]
        [TestCase("{\"comboBoxes\":[{\"id\":\"c\",\"items\":[{\"label\":\"L\"}]}]}")]
        [TestCase("{\"appLogo\":{}}")]
        [TestCase("{\"appLogo\":{\"uri\":\"u\",\"crop\":1}}")]
        [TestCase("{\"audio\":{\"type\":\"uri\"}}")]
        public void AMissingRequiredKey_OrAWrongType_Is7(string json) => Assert.AreEqual(ErrorInvalidParameter, Error(json));

        [TestCase("title")]
        [TestCase("body")]
        [TestCase("tag")]
        [TestCase("group")]
        [TestCase("scenario")]
        [TestCase("duration")]
        [TestCase("attribution")]
        [TestCase("heroImage")]
        [TestCase("inlineImage")]
        public void AStringKeyOfAnotherTypeOrNull_Is7(string key)
        {
            Assert.AreEqual(ErrorInvalidParameter, Error($"{{\"{key}\":1}}"));
            Assert.AreEqual(ErrorInvalidParameter, Error($"{{\"{key}\":null}}"));
        }

        [TestCase("{\"buttons\":{}}")]
        [TestCase("{\"buttons\":null}")]
        [TestCase("{\"buttons\":[null]}")]
        [TestCase("{\"buttons\":[1]}")]
        [TestCase("{\"textBoxes\":[null]}")]
        [TestCase("{\"comboBoxes\":[\"c\"]}")]
        [TestCase("{\"comboBoxes\":[{\"id\":\"c\",\"items\":[null]}]}")]
        [TestCase("{\"appLogo\":\"u\"}")]
        [TestCase("{\"audio\":null}")]
        [TestCase("{\"progress\":[]}")]
        [TestCase("{\"progress\":{\"value\":\"0.5\"}}")]
        [TestCase("{\"progress\":{\"valueStr\":null}}")]
        [TestCase("{\"timestamp\":\"now\"}")]
        [TestCase("{\"expiration\":null}")]
        [TestCase("{\"expiresOnReboot\":\"true\"}")]
        [TestCase("{\"expiresOnReboot\":1}")]
        public void AContainerOrValueOfAnotherTypeOrNull_Is7(string json) => Assert.AreEqual(ErrorInvalidParameter, Error(json));

        [TestCase("{\"audio\":{\"type\":\"mute\",\"event\":5}}", "SetAudio(1, NULL, NULL, 0)")]
        [TestCase("{\"audio\":{\"type\":\"mute\",\"event\":null}}", "SetAudio(1, NULL, NULL, 0)")]
        [TestCase("{\"audio\":{\"type\":\"mute\",\"uri\":5}}", "SetAudio(1, NULL, NULL, 0)")]
        [TestCase("{\"audio\":{\"type\":\"uri\",\"uri\":\"u\",\"event\":5}}", "SetAudio(2, NULL, \"u\", 0)")]
        [TestCase("{\"audio\":{\"uri\":5}}", "SetAudio(0, NULL, NULL, 0)")]
        [TestCase("{\"audio\":{\"src\":5}}", "SetAudio(0, NULL, NULL, 0)")]
        [TestCase("{\"audio\":{\"src\":\"ms-winsoundevent:Notification.Mail\"}}", "SetAudio(0, NULL, NULL, 0)")]
        public void KeysThat1xDidNotRead_AreNotChecked(string json, string call) => CollectionAssert.AreEqual(new[] { call }, Plan(json));

        [Test]
        public void UnknownKeys_AreIgnored() => CollectionAssert.IsEmpty(Plan("{\"unknown\":{\"a\":[1,null]},\"another\":null}"));

        [Test]
        public void AudioLoop_IsReadWhateverTheType()
        {
            // Without duration "long" the native show refuses the looping sound with 7, as 1.x did.
            CollectionAssert.AreEqual(new[] { "SetAudio(1, NULL, NULL, 1)" }, Plan("{\"audio\":{\"type\":\"mute\",\"loop\":true}}"));
            Assert.AreEqual(ErrorInvalidParameter, Error("{\"audio\":{\"type\":\"mute\",\"loop\":\"x\"}}"));
            Assert.AreEqual(ErrorInvalidParameter, Error("{\"audio\":{\"type\":\"mute\",\"loop\":null}}"));
            CollectionAssert.AreEqual(new[] { "SetDuration(1)", "SetAudio(1, NULL, NULL, 1)" },
                Plan("{\"duration\":\"long\",\"audio\":{\"type\":\"mute\",\"loop\":true}}"));
        }

        [Test]
        public void ProgressValueString_IsReadAsValueStr() =>
            CollectionAssert.AreEqual(new[] { "SetProgress(NULL, 0.5, \"50%\", NULL)" },
                Plan("{\"progress\":{\"value\":0.5,\"valueStr\":\"50%\",\"value_string\":\"x\"}}"));

        [TestCase("1700000000", 1700000000000L)]
        [TestCase("1700000000.999", 1700000000000L)]
        [TestCase("-1.9", -1000L)]
        [TestCase("0", 0L)]
        [TestCase("922337203685", 922337203685000L)]
        [TestCase("-922337203685", -922337203685000L)]
        public void Timestamp_SecondsTruncatedTowardZero_AsMilliseconds(string seconds, long milliseconds) =>
            CollectionAssert.AreEqual(new[] { $"SetTimestamp({milliseconds})" }, Plan($"{{\"timestamp\":{seconds}}}"));

        [TestCase("922337203686")]
        [TestCase("-922337203686")]
        [TestCase("1e300")]
        public void Timestamp_OutOfRange_Is7(string seconds) => Assert.AreEqual(ErrorInvalidParameter, Error($"{{\"timestamp\":{seconds}}}"));

        [TestCase("60", 60L)]
        [TestCase("-5.5", -5L)]
        [TestCase("9223372036854774784", 9223372036854774784L)]
        public void Expiration_TruncatedTowardZero(string seconds, long expected) =>
            CollectionAssert.AreEqual(new[] { $"SetExpiration({expected})" }, Plan($"{{\"expiration\":{seconds}}}"));

        [TestCase("9223372036854775808")]
        [TestCase("-9223372036854777856")]
        [TestCase("1e19")]
        public void Expiration_BeyondALong_Is7(string seconds) => Assert.AreEqual(ErrorInvalidParameter, Error($"{{\"expiration\":{seconds}}}"));

        [Test]
        public void StringsKeepEmbeddedNuls_ForTheBridgeToCut() =>
            CollectionAssert.AreEqual(new[] { "SetTitle(\"a\0b\")" }, Plan("{\"title\":\"a\\u0000b\"}"));

        [Test]
        public void ThePublicBuildersJson_ReadsBackAsTheSameCalls()
        {
            var payload = new WindowsNotificationPayload
            {
                Title = "T", Body = "B", Tag = "t", Group = "g", Scenario = "reminder", Duration = "long",
                Expiration = 60, ExpiresOnReboot = true, Timestamp = 1700000000, Attribution = "A",
                Buttons = new List<WindowsNotificationButtonPayload>
                {
                    new() { Label = "Open", Args = new Dictionary<string, string> { ["action"] = "open" } },
                    new() { Label = "Docs", InvokeUri = "https://example.com" },
                },
                TextBoxes = new List<WindowsNotificationTextBoxPayload> { new() { Id = "reply", Placeholder = "Type" } },
                Audio = new WindowsNotificationAudioPayload { Loop = true, Src = "ms-winsoundevent:Notification.Mail" },
                Progress = new WindowsNotificationProgressPayload { Value = 0.5, ValueStr = "50%", Status = "s" },
            };

            CollectionAssert.AreEqual(new[]
            {
                "SetTitle(\"T\")", "SetBody(\"B\")", "SetTag(\"t\")", "SetGroup(\"g\")", "SetScenario(1)", "SetDuration(1)",
                "AddButton(\"Open\", NULL, 1, 0)", "AddButtonArgument(0, \"action\", \"open\")",
                "AddButton(\"Docs\", \"https://example.com\", 0, 1)",
                "AddTextInput(\"reply\", \"Type\", \"\")",
                "SetAudio(0, NULL, NULL, 1)",
                "SetProgress(NULL, 0.5, \"50%\", \"s\")",
                "SetAttribution(\"A\")", "SetTimestamp(1700000000000)", "SetExpiration(60)", "SetExpiresOnReboot(1)",
            }, Plan(WindowsNotificationJsonBuilder.BuildNotificationPayload(payload)));
        }
    }

    /// <summary>EditMode tests for the rest of the notification bridge's pure functions (design v8, 7.1).</summary>
    public sealed class WindowsNotificationCApiTests
    {
        [Test]
        public void Structures_HaveTheCApiSizes()
        {
            Assert.AreEqual(56, Marshal.SizeOf<ManagerOptions>());
            Assert.AreEqual(56, Marshal.SizeOf<ProgressUpdate>());
        }

        [TestCase(nameof(ManagerOptions.StructSize), 0)]
        [TestCase(nameof(ManagerOptions.Reserved0), 4)]
        [TestCase(nameof(ManagerOptions.OnInvoked), 8)]
        [TestCase(nameof(ManagerOptions.UserData), 16)]
        [TestCase(nameof(ManagerOptions.Release), 24)]
        [TestCase(nameof(ManagerOptions.IsUnpackaged), 32)]
        [TestCase(nameof(ManagerOptions.Reserved1), 36)]
        [TestCase(nameof(ManagerOptions.DisplayName), 40)]
        [TestCase(nameof(ManagerOptions.IconUri), 48)]
        public void ManagerOptions_FieldOffsets(string field, int offset) =>
            Assert.AreEqual(offset, Marshal.OffsetOf<ManagerOptions>(field).ToInt32());

        [TestCase(nameof(ProgressUpdate.StructSize), 0)]
        [TestCase(nameof(ProgressUpdate.Reserved0), 4)]
        [TestCase(nameof(ProgressUpdate.Tag), 8)]
        [TestCase(nameof(ProgressUpdate.Group), 16)]
        [TestCase(nameof(ProgressUpdate.Value), 24)]
        [TestCase(nameof(ProgressUpdate.ValueString), 32)]
        [TestCase(nameof(ProgressUpdate.Status), 40)]
        [TestCase(nameof(ProgressUpdate.SequenceNumber), 48)]
        [TestCase(nameof(ProgressUpdate.Reserved1), 52)]
        public void ProgressUpdate_FieldOffsets(string field, int offset) =>
            Assert.AreEqual(offset, Marshal.OffsetOf<ProgressUpdate>(field).ToInt32());

        [Test]
        public void BuildManagerOptions_HasNoUserDataOrRelease()
        {
            ManagerOptions unpackaged = BuildManagerOptions(new IntPtr(1), false, new IntPtr(2), new IntPtr(3));
            Assert.AreEqual(56u, unpackaged.StructSize);
            Assert.AreEqual((0u, 0u), (unpackaged.Reserved0, unpackaged.Reserved1));
            Assert.AreEqual(new IntPtr(1), unpackaged.OnInvoked);
            Assert.AreEqual((IntPtr.Zero, IntPtr.Zero), (unpackaged.UserData, unpackaged.Release));
            Assert.AreEqual(1, unpackaged.IsUnpackaged);
            Assert.AreEqual((new IntPtr(2), new IntPtr(3)), (unpackaged.DisplayName, unpackaged.IconUri));

            Assert.AreEqual(0, BuildManagerOptions(IntPtr.Zero, true, IntPtr.Zero, IntPtr.Zero).IsUnpackaged);
        }

        [Test]
        public void BuildProgressUpdate_CarriesTheSixValues()
        {
            ProgressUpdate update = BuildProgressUpdate(new IntPtr(1), new IntPtr(2), 0.5, new IntPtr(3), new IntPtr(4), 9);
            Assert.AreEqual(56u, update.StructSize);
            Assert.AreEqual((0u, 0u), (update.Reserved0, update.Reserved1));
            Assert.AreEqual((new IntPtr(1), new IntPtr(2), new IntPtr(3), new IntPtr(4)), (update.Tag, update.Group, update.ValueString, update.Status));
            Assert.AreEqual(0.5, update.Value);
            Assert.AreEqual(9u, update.SequenceNumber);
        }

        [TestCase(false, 0, 0, ErrorNotInitialized)]
        [TestCase(false, 5, 1, ErrorNotInitialized)]
        [TestCase(true, 5, 0, ErrorDisabled)]
        [TestCase(true, 1, 0, ErrorDisabled)]
        [TestCase(true, 0, 1, ErrorDisabled)]
        [TestCase(true, 0, 4, ErrorDisabled)]
        [TestCase(true, 0, -1, ErrorDisabled)]
        [TestCase(true, 0, 0, ErrorNone)]
        public void ShowGate_ChecksTheManagerThenTheSetting(bool hasManager, int settingError, int setting, int expected) =>
            Assert.AreEqual(expected, ShowGate(hasManager, settingError, setting));

        [TestCase(-7, ErrorInvalidParameter)]
        [TestCase(int.MinValue, ErrorInvalidParameter)]
        [TestCase(-6, ErrorNone)]
        [TestCase(0, ErrorNone)]
        [TestCase(99, ErrorNone)]
        public void CheckBadgeValue_RefusesBelowTheLowestGlyph(int value, int expected) =>
            Assert.AreEqual(expected, CheckBadgeValue(value));

        // The expectation is passed as int: a public test cannot take the internal enum.
        [TestCase(15700, (int)PackageIdentity.Unpackaged)]
        [TestCase(122, (int)PackageIdentity.Packaged)]
        [TestCase(0, (int)PackageIdentity.Unknown)]
        [TestCase(87, (int)PackageIdentity.Unknown)]
        public void ClassifyPackage_ByTheResultOfGetCurrentPackageFullName(int result, int expected) =>
            Assert.AreEqual((PackageIdentity)expected, ClassifyPackage(result));

        [Test]
        public void FromException_LoadFailuresAre4_OthersAre5()
        {
            Assert.AreEqual(NativeUnavailable, FromException(new DllNotFoundException()));
            Assert.AreEqual(NativeUnavailable, FromException(new EntryPointNotFoundException()));
            Assert.AreEqual(NativeUnavailable, FromException(new BadImageFormatException()));
            Assert.AreEqual(ErrorHresultFailure, FromException(new OutOfMemoryException()));
            Assert.AreEqual(ErrorHresultFailure, FromException(new InvalidOperationException()));
        }

        [TestCase(0, 0, WindowsNotificationSetting.Enabled)]
        [TestCase(0, 4, WindowsNotificationSetting.DisabledByManifest)]
        [TestCase(0, 5, WindowsNotificationSetting.Unknown)]
        [TestCase(0, -1, WindowsNotificationSetting.Unknown)]
        [TestCase(5, 0, WindowsNotificationSetting.Unknown)]
        [TestCase(1, 0, WindowsNotificationSetting.Unknown)]
        public void SettingFrom_IsUnknownOnFailureOrAnUndefinedValue(int error, int raw, WindowsNotificationSetting expected) =>
            Assert.AreEqual(expected, SettingFrom(error, raw));

        [Test]
        public void ToResult_ZeroIsSuccess()
        {
            Assert.IsTrue(ToResult("op", 0).IsSuccess);
            WindowsNotificationResult failure = ToResult("op", 7);
            Assert.IsFalse(failure.IsSuccess);
            Assert.AreEqual(7, failure.ErrorCode);
            Assert.AreEqual("op", failure.Operation);
        }

        [Test]
        public void DeliverInvoked_PassesTheArguments()
        {
            string? received = null;
            DeliverInvoked(args => received = args, "{\"action\":\"open\"}");
            Assert.AreEqual("{\"action\":\"open\"}", received);
        }

        [Test]
        public void DeliverInvoked_ASubscribersException_IsLoggedAndStops()
        {
            LogAssert.Expect(LogType.Error, new Regex("subscriber failed"));
            Assert.DoesNotThrow(() => DeliverInvoked(_ => throw new InvalidOperationException("subscriber failed"), "{}"));
        }

        [Test]
        public void DeliverInvoked_WithoutASubscriber_DoesNothing() => Assert.DoesNotThrow(() => DeliverInvoked(null, "{}"));

        [Test]
        public void BuildGetAllJson_OfNone_IsAnEmptyArray() =>
            Assert.AreEqual("[]", BuildGetAllJson(Array.Empty<ListedNotification>()));

        [Test]
        public void BuildGetAllJson_IdTagGroup_InThatOrder_WithoutSpaces() =>
            Assert.AreEqual("[{\"id\":1,\"tag\":\"a\",\"group\":\"b\"},{\"id\":4294967295,\"tag\":\"\",\"group\":\"\"}]",
                BuildGetAllJson(new[] { new ListedNotification(1, "a", "b"), new ListedNotification(uint.MaxValue, "", "") }));

        [TestCase("\"", "\\\"")]
        [TestCase("\\", "\\\\")]
        [TestCase("\b", "\\b")]
        [TestCase("\f", "\\f")]
        [TestCase("\n", "\\n")]
        [TestCase("\r", "\\r")]
        [TestCase("\t", "\\t")]
        [TestCase("\u0000", "\\u0000")]
        [TestCase("\u001f", "\\u001F")]
        [TestCase("\u000b", "\\u000B")]
        [TestCase("/", "/")]
        [TestCase("\u007f", "\u007f")]
        [TestCase("ダ𠮷", "ダ𠮷")]
        [TestCase("\u2028", "\u2028")]
        public void BuildGetAllJson_EscapesAsWindowsDataJsonDid(string raw, string written) =>
            Assert.AreEqual($"[{{\"id\":0,\"tag\":\"{written}\",\"group\":\"\"}}]",
                BuildGetAllJson(new[] { new ListedNotification(0, raw, "") }));
    }
}
#endif
