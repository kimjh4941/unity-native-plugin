#nullable enable

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using JonghyunKim.NativeToolkit.Runtime.Clipboard;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// EditMode tests for the Windows clipboard JSON parser. The payload shapes come from the
    /// native design: a string array for files and formats, an array of history entries whose
    /// timestamp is a decimal string, and a two-flag availability object.
    /// </summary>
    public sealed class WindowsClipboardJsonParserTests
    {
        [Test]
        public void TryParseStringArray_ReadsEveryElement()
        {
            bool ok = WindowsClipboardJsonParser.TryParseStringArray(
                "[\"CF_UNICODETEXT\",\"HTML Format\"]", out IReadOnlyList<string> values);

            Assert.IsTrue(ok);
            Assert.AreEqual(2, values.Count);
            Assert.AreEqual("CF_UNICODETEXT", values[0]);
            Assert.AreEqual("HTML Format", values[1]);
        }

        [Test]
        public void TryParseStringArray_EmptyArrayIsASuccessWithNoValues()
        {
            bool ok = WindowsClipboardJsonParser.TryParseStringArray("[]", out IReadOnlyList<string> values);

            Assert.IsTrue(ok);
            Assert.AreEqual(0, values.Count);
        }

        [Test]
        public void TryParseStringArray_UnescapesEscapedPaths()
        {
            bool ok = WindowsClipboardJsonParser.TryParseStringArray(
                "[\"C:\\\\a.txt\"]", out IReadOnlyList<string> values);

            Assert.IsTrue(ok);
            Assert.AreEqual(@"C:\a.txt", values[0]);
        }

        [Test]
        public void TryParseStringArray_NullOrEmptyInputFails()
        {
            Assert.IsFalse(WindowsClipboardJsonParser.TryParseStringArray(null, out IReadOnlyList<string> a));
            Assert.AreEqual(0, a.Count);
            Assert.IsFalse(WindowsClipboardJsonParser.TryParseStringArray(string.Empty, out IReadOnlyList<string> b));
            Assert.AreEqual(0, b.Count);
        }

        [Test]
        public void TryParseHistoryItems_ReadsEveryField()
        {
            const string json =
                "[{\"id\":\"abc\",\"text\":\"hello\",\"contentTypes\":[\"Text\",\"Bitmap\"],\"timestamp\":\"133000000000000000\"}]";

            bool ok = WindowsClipboardJsonParser.TryParseHistoryItems(
                json, out IReadOnlyList<WindowsClipboardHistoryItem> items);

            Assert.IsTrue(ok);
            Assert.AreEqual(1, items.Count);
            Assert.AreEqual("abc", items[0].Id);
            Assert.AreEqual("hello", items[0].Text);
            Assert.AreEqual(2, items[0].ContentTypes.Count);
            Assert.AreEqual(133000000000000000L, items[0].Timestamp);
        }

        [Test]
        public void TryParseHistoryItems_MissingTextBecomesNull()
        {
            const string json = "[{\"id\":\"abc\",\"contentTypes\":[\"Bitmap\"],\"timestamp\":\"1\"}]";

            bool ok = WindowsClipboardJsonParser.TryParseHistoryItems(
                json, out IReadOnlyList<WindowsClipboardHistoryItem> items);

            Assert.IsTrue(ok);
            Assert.IsNull(items[0].Text);
        }

        [Test]
        public void TryParseHistoryItems_MissingContentTypesBecomesEmptyNotNull()
        {
            const string json = "[{\"id\":\"abc\",\"timestamp\":\"1\"}]";

            bool ok = WindowsClipboardJsonParser.TryParseHistoryItems(
                json, out IReadOnlyList<WindowsClipboardHistoryItem> items);

            Assert.IsTrue(ok);
            Assert.IsNotNull(items[0].ContentTypes);
            Assert.AreEqual(0, items[0].ContentTypes.Count);
        }

        [Test]
        public void TryParseHistoryItems_KeepsTheFullInt64Range()
        {
            string json = "[{\"id\":\"a\",\"timestamp\":\"" + long.MaxValue + "\"}]";

            bool ok = WindowsClipboardJsonParser.TryParseHistoryItems(
                json, out IReadOnlyList<WindowsClipboardHistoryItem> items);

            Assert.IsTrue(ok);
            Assert.AreEqual(long.MaxValue, items[0].Timestamp);
        }

        [Test]
        public void TryParseHistoryItems_EmptyArrayIsASuccessWithNoItems()
        {
            bool ok = WindowsClipboardJsonParser.TryParseHistoryItems(
                "[]", out IReadOnlyList<WindowsClipboardHistoryItem> items);

            Assert.IsTrue(ok);
            Assert.AreEqual(0, items.Count);
        }

        [Test]
        public void TryParseHistoryItems_NullInputFails()
        {
            Assert.IsFalse(WindowsClipboardJsonParser.TryParseHistoryItems(
                null, out IReadOnlyList<WindowsClipboardHistoryItem> items));
            Assert.AreEqual(0, items.Count);
        }

        [Test]
        public void TryParseStringArray_MalformedPayloadFailsInsteadOfLookingEmpty()
        {
            // The dangerous outcome is not an exception but a silent "true plus empty list": the
            // caller cannot tell that apart from an empty clipboard (design 2.4 / 7.5).
            Assert.IsFalse(WindowsClipboardJsonParser.TryParseStringArray("[\"a\",", out IReadOnlyList<string> a));
            Assert.AreEqual(0, a.Count);
            Assert.IsFalse(WindowsClipboardJsonParser.TryParseStringArray("{\"a\":1}", out IReadOnlyList<string> b));
            Assert.AreEqual(0, b.Count);
            Assert.IsFalse(WindowsClipboardJsonParser.TryParseStringArray("not json", out IReadOnlyList<string> c));
            Assert.AreEqual(0, c.Count);
            Assert.IsFalse(WindowsClipboardJsonParser.TryParseStringArray("42", out IReadOnlyList<string> d));
            Assert.AreEqual(0, d.Count);
        }

        [Test]
        public void TryParseHistoryItems_MalformedPayloadFailsInsteadOfLookingEmpty()
        {
            Assert.IsFalse(WindowsClipboardJsonParser.TryParseHistoryItems(
                "[{\"id\":\"a\",", out IReadOnlyList<WindowsClipboardHistoryItem> a));
            Assert.AreEqual(0, a.Count);
            Assert.IsFalse(WindowsClipboardJsonParser.TryParseHistoryItems(
                "{\"id\":\"a\"}", out IReadOnlyList<WindowsClipboardHistoryItem> b));
            Assert.AreEqual(0, b.Count);
        }

        [Test]
        public void TryParseHistoryItems_AnEntryWithoutAnIdIsDropped()
        {
            bool ok = WindowsClipboardJsonParser.TryParseHistoryItems(
                "[{\"text\":\"x\",\"timestamp\":\"1\"},{\"id\":\"keep\",\"timestamp\":\"2\"}]",
                out IReadOnlyList<WindowsClipboardHistoryItem> items);

            LogAssert.Expect(LogType.Warning, new Regex("dropped an entry without an id"));
            Assert.IsTrue(ok);
            Assert.AreEqual(1, items.Count);
            Assert.AreEqual("keep", items[0].Id);
        }

        [Test]
        public void TryParseHistoryItems_ANonNumericTimestampBecomesZero()
        {
            bool ok = WindowsClipboardJsonParser.TryParseHistoryItems(
                "[{\"id\":\"a\",\"timestamp\":\"not-a-number\"}]",
                out IReadOnlyList<WindowsClipboardHistoryItem> items);

            LogAssert.Expect(LogType.Warning, new Regex("timestamp was not an int64"));
            Assert.IsTrue(ok);
            Assert.AreEqual(0L, items[0].Timestamp);
        }

        [Test]
        public void ToUtcTime_ConvertsAValidFileTime()
        {
            long ticks = new DateTimeOffset(2026, 9, 7, 12, 0, 0, TimeSpan.Zero).ToFileTime();
            bool ok = WindowsClipboardJsonParser.TryParseHistoryItems(
                "[{\"id\":\"a\",\"timestamp\":\"" + ticks + "\"}]",
                out IReadOnlyList<WindowsClipboardHistoryItem> items);

            Assert.IsTrue(ok);
            DateTimeOffset? converted = items[0].ToUtcTime();
            Assert.IsNotNull(converted);
            Assert.AreEqual(2026, converted!.Value.Year);
            Assert.AreEqual(9, converted.Value.Month);
        }

        [Test]
        public void ToUtcTime_OutOfRangeValueReturnsNullInsteadOfThrowing()
        {
            bool ok = WindowsClipboardJsonParser.TryParseHistoryItems(
                "[{\"id\":\"a\",\"timestamp\":\"" + long.MaxValue + "\"}]",
                out IReadOnlyList<WindowsClipboardHistoryItem> items);

            Assert.IsTrue(ok);
            Assert.IsNull(items[0].ToUtcTime());
        }

        [Test]
        public void TryParseAvailability_AnObjectMissingTheKeysFailsInsteadOfReportingHistoryOff()
        {
            // JsonUtility fills a missing bool with false, so without a shape check this payload
            // would be indistinguishable from a genuine "history is off".
            Assert.IsFalse(WindowsClipboardJsonParser.TryParseAvailability(
                "{}", out bool history, out bool roaming));
            Assert.IsFalse(history);
            Assert.IsFalse(roaming);

            Assert.IsFalse(WindowsClipboardJsonParser.TryParseAvailability(
                "{\"historyEnabled\":true}", out _, out _), "roamingEnabled is mandatory too");
            Assert.IsFalse(WindowsClipboardJsonParser.TryParseAvailability("[]", out _, out _));
            Assert.IsFalse(WindowsClipboardJsonParser.TryParseAvailability("garbage", out _, out _));
        }

        [Test]
        public void TryParseAvailability_ReadsBothFlags()
        {
            bool ok = WindowsClipboardJsonParser.TryParseAvailability(
                "{\"historyEnabled\":true,\"roamingEnabled\":false}",
                out bool historyEnabled, out bool roamingEnabled);

            Assert.IsTrue(ok);
            Assert.IsTrue(historyEnabled);
            Assert.IsFalse(roamingEnabled);
        }

        [Test]
        public void TryParseAvailability_NullInputFailsAndReportsBothFalse()
        {
            bool ok = WindowsClipboardJsonParser.TryParseAvailability(
                null, out bool historyEnabled, out bool roamingEnabled);

            Assert.IsFalse(ok);
            Assert.IsFalse(historyEnabled);
            Assert.IsFalse(roamingEnabled);
        }

        [Test]
        public void Availability_AKeyNameInsideAValueDoesNotCountAsAKey()
        {
            // Both names appear, quoted, as values rather than keys. JsonUtility fills the
            // missing fields with false, so accepting this would report "history is off" for a
            // payload that never said anything of the sort.
            bool parsed = WindowsClipboardJsonParser.TryParseAvailability(
                "{\"a\":\"historyEnabled\",\"b\":\"roamingEnabled\"}", out _, out _);

            Assert.IsFalse(parsed);
        }

        [Test]
        public void Availability_AcceptsTheKeysWhitespaceAndAll()
        {
            bool parsed = WindowsClipboardJsonParser.TryParseAvailability(
                "{ \"historyEnabled\" : true , \"roamingEnabled\" : true }",
                out bool history, out bool roaming);

            Assert.IsTrue(parsed);
            Assert.IsTrue(history);
            Assert.IsTrue(roaming);
        }

        [Test]
        public void Availability_AValueThatMentionsOneKeyStillNeedsTheRealOne()
        {
            bool parsed = WindowsClipboardJsonParser.TryParseAvailability(
                "{\"historyEnabled\":true,\"note\":\"roamingEnabled\"}", out _, out _);

            Assert.IsFalse(parsed);
        }


        [Test]
        public void Availability_AKeyNestedInsideAnotherObjectIsNotATopLevelKey()
        {
            // JsonUtility reads only the top level, so it would not find historyEnabled here and
            // would fill it with false - reporting "history is off" for a payload that never said
            // so. The shape check has to know the difference.
            bool parsed = WindowsClipboardJsonParser.TryParseAvailability(
                "{\"meta\":{\"historyEnabled\":true},\"roamingEnabled\":false}", out _, out _);

            Assert.IsFalse(parsed);
        }

        [Test]
        public void Availability_AcceptsTopLevelKeysAlongsideANestedObject()
        {
            bool parsed = WindowsClipboardJsonParser.TryParseAvailability(
                "{\"meta\":{\"note\":\"x\"},\"historyEnabled\":true,\"roamingEnabled\":false}",
                out bool history, out bool roaming);

            Assert.IsTrue(parsed);
            Assert.IsTrue(history);
            Assert.IsFalse(roaming);
        }

        [Test]
        public void Availability_AnEscapedQuoteInsideAValueDoesNotDerailTheScan()
        {
            bool parsed = WindowsClipboardJsonParser.TryParseAvailability(
                "{\"note\":\"a\\\"b\",\"historyEnabled\":true,\"roamingEnabled\":true}",
                out bool history, out bool roaming);

            Assert.IsTrue(parsed);
            Assert.IsTrue(history);
            Assert.IsTrue(roaming);
        }

    }
}
#endif
