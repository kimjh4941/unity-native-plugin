#nullable enable

#if UNITY_STANDALONE_OSX || UNITY_EDITOR
using System;
using System.Text;
using NUnit.Framework;
using JonghyunKim.NativeToolkit.Runtime.Clipboard;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// EditMode tests for the macOS clipboard response parser. Covers the distinction between an
    /// explicit null and a missing key, the top-level array shapes, the 64-bit change count, and
    /// the failure paths that must not produce a half-built value.
    /// </summary>
    public sealed class MacClipboardJsonParserTests
    {
        // ── ownership and scope ──────────────────────────────────────────────

        [Test]
        public void TryParseOwnership_GeneralScope_AcceptsTheOmittedNameKey()
        {
            // Swift's synthesised encoder omits name for the general pasteboard rather than
            // writing null, so a missing key here is valid where elsewhere it is a mismatch.
            Assert.IsTrue(MacClipboardJsonParser.TryParseOwnership(
                "{\"scope\":{\"kind\":\"general\"},\"changeCount\":7}", out var ownership));
            Assert.IsNotNull(ownership);
            Assert.AreEqual(MacPasteboardScopeKind.General, ownership!.Scope.Kind);
            Assert.IsNull(ownership.Scope.Name);
            Assert.AreEqual(7L, ownership.ChangeCount);
        }

        [Test]
        public void TryParseOwnership_NamedAndUnique_RequireAName()
        {
            Assert.IsTrue(MacClipboardJsonParser.TryParseOwnership(
                "{\"scope\":{\"kind\":\"named\",\"name\":\"b\"},\"changeCount\":1}", out var named));
            Assert.AreEqual(MacPasteboardScopeKind.Named, named!.Scope.Kind);
            Assert.AreEqual("b", named.Scope.Name);

            Assert.IsTrue(MacClipboardJsonParser.TryParseOwnership(
                "{\"scope\":{\"kind\":\"unique\",\"name\":\"g\"},\"changeCount\":1}", out var unique));
            Assert.AreEqual(MacPasteboardScopeKind.Unique, unique!.Scope.Kind);

            // A named scope without a name is a mismatch, unlike the general case above.
            Assert.IsFalse(MacClipboardJsonParser.TryParseOwnership(
                "{\"scope\":{\"kind\":\"named\"},\"changeCount\":1}", out _));
        }

        [Test]
        public void TryParseOwnership_ChangeCountBeyondIntRange_IsAccepted()
        {
            // The native side declares Swift's Int. Reading it as int would turn a valid response
            // into a parse failure once the counter passes int.MaxValue.
            Assert.IsTrue(MacClipboardJsonParser.TryParseOwnership(
                "{\"scope\":{\"kind\":\"general\"},\"changeCount\":2147483648}", out var ownership));
            Assert.AreEqual(2147483648L, ownership!.ChangeCount);
        }

        [Test]
        public void TryParseOwnership_UnknownScopeKindOrMalformedJson_Fails()
        {
            Assert.IsFalse(MacClipboardJsonParser.TryParseOwnership(
                "{\"scope\":{\"kind\":\"nope\",\"name\":\"b\"},\"changeCount\":1}", out _));
            Assert.IsFalse(MacClipboardJsonParser.TryParseOwnership("{", out _));
            Assert.IsFalse(MacClipboardJsonParser.TryParseOwnership(null, out _));
        }

        [Test]
        public void TryParseScopeResult_ReadsTheNestedScope()
        {
            Assert.IsTrue(MacClipboardJsonParser.TryParseScopeResult(
                "{\"scope\":{\"kind\":\"unique\",\"name\":\"gen-1\"}}", out var scope));
            Assert.AreEqual("gen-1", scope!.Name);
        }

        // ── read ─────────────────────────────────────────────────────────────

        [Test]
        public void TryParseReadResult_DecodesEveryRepresentation()
        {
            // "hi" is aGk=, "ok" is b2s=.
            Assert.IsTrue(MacClipboardJsonParser.TryParseReadResult(
                "{\"changeCount\":3,\"items\":[{\"representations\":{\"a\":\"aGk=\",\"b\":\"b2s=\"}}]}",
                out var contents));
            Assert.AreEqual(3L, contents!.ChangeCount);
            Assert.AreEqual(1, contents.Items.Count);
            Assert.AreEqual("hi", Encoding.UTF8.GetString(contents.Items[0].Representations["a"]));
            Assert.AreEqual("ok", Encoding.UTF8.GetString(contents.Items[0].Representations["b"]));
        }

        [Test]
        public void TryParseReadResult_EmptyPasteboard_IsASuccessWithNoItems()
        {
            Assert.IsTrue(MacClipboardJsonParser.TryParseReadResult(
                "{\"changeCount\":0,\"items\":[]}", out var contents));
            Assert.AreEqual(0, contents!.Items.Count);
        }

        [Test]
        public void TryParseReadResult_MalformedBase64_Fails()
        {
            Assert.IsFalse(MacClipboardJsonParser.TryParseReadResult(
                "{\"changeCount\":1,\"items\":[{\"representations\":{\"a\":\"!!!\"}}]}", out _));
        }

        // ── readData ─────────────────────────────────────────────────────────

        [Test]
        public void TryParseReadData_ExplicitNull_IsASuccessWithNoBytes()
        {
            // The one place where success with a null payload is normal: the pasteboard has no
            // such type. An invalid uniform type identifier lands here too.
            Assert.IsTrue(MacClipboardJsonParser.TryParseReadData("{\"data\":null}", out byte[]? data));
            Assert.IsNull(data);
        }

        [Test]
        public void TryParseReadData_MissingKey_IsASchemaMismatch()
        {
            // The native encoder always writes the key, so its absence is not "no data".
            Assert.IsFalse(MacClipboardJsonParser.TryParseReadData("{}", out _));
        }

        [Test]
        public void TryParseReadData_Base64_IsDecoded()
        {
            Assert.IsTrue(MacClipboardJsonParser.TryParseReadData("{\"data\":\"aGk=\"}", out byte[]? data));
            Assert.AreEqual("hi", Encoding.UTF8.GetString(data!));
        }

        // ── snapshot ─────────────────────────────────────────────────────────

        [Test]
        public void TryParseSnapshot_ReadsTheArrayOfArrays()
        {
            Assert.IsTrue(MacClipboardJsonParser.TryParseSnapshot(
                "{\"changeCount\":2,\"itemTypes\":[[\"a\",\"b\"],[\"c\"]],\"matchingItemIndexes\":[1]}",
                out var snapshot));
            Assert.AreEqual(2L, snapshot!.ChangeCount);
            Assert.AreEqual(2, snapshot.ItemTypes.Count);
            Assert.AreEqual(2, snapshot.ItemTypes[0].Count);
            Assert.AreEqual("c", snapshot.ItemTypes[1][0]);
            Assert.AreEqual(new[] { 1 }, snapshot.MatchingItemIndexes);
        }

        [Test]
        public void TryParseSnapshot_MissingMatchingIndexes_Fails()
        {
            Assert.IsFalse(MacClipboardJsonParser.TryParseSnapshot(
                "{\"changeCount\":1,\"itemTypes\":[]}", out _));
        }

        // ── scalars ──────────────────────────────────────────────────────────

        [Test]
        public void TryParseChangeCount_AcceptsA64BitValue()
        {
            Assert.IsTrue(MacClipboardJsonParser.TryParseChangeCount(
                "{\"changeCount\":4294967296}", out long value));
            Assert.AreEqual(4294967296L, value);
        }

        [Test]
        public void TryParseBool_ReadsTheValueMember()
        {
            Assert.IsTrue(MacClipboardJsonParser.TryParseBool("{\"value\":true}", out bool value));
            Assert.IsTrue(value);
            Assert.IsTrue(MacClipboardJsonParser.TryParseBool("{\"value\":false}", out value));
            Assert.IsFalse(value);
            Assert.IsFalse(MacClipboardJsonParser.TryParseBool("{\"value\":1}", out _));
        }

        [Test]
        public void TryParseAccessBehavior_KnownValues_AreMapped()
        {
            Assert.IsTrue(MacClipboardJsonParser.TryParseAccessBehavior(
                "{\"value\":\"alwaysDeny\"}", out var behavior));
            Assert.AreEqual(MacClipboardAccessBehavior.AlwaysDeny, behavior);

            Assert.IsTrue(MacClipboardJsonParser.TryParseAccessBehavior(
                "{\"value\":\"unavailable\"}", out behavior));
            Assert.AreEqual(MacClipboardAccessBehavior.Unavailable, behavior);
        }

        [Test]
        public void TryParseAccessBehavior_UnknownValue_MapsToUnknownRatherThanFailing()
        {
            // Advisory information, and the native layer may gain a case before this package does.
            Assert.IsTrue(MacClipboardJsonParser.TryParseAccessBehavior(
                "{\"value\":\"somethingNew\"}", out var behavior));
            Assert.AreEqual(MacClipboardAccessBehavior.Unknown, behavior);
        }

        // ── patterns ─────────────────────────────────────────────────────────

        [Test]
        public void TryParsePatterns_ReadsABareTopLevelArray()
        {
            // Not an object: the native encoder writes the pattern list as the whole document.
            Assert.IsTrue(MacClipboardJsonParser.TryParsePatterns(
                "[\"links\",\"phoneNumbers\"]", out var patterns));
            Assert.AreEqual(2, patterns.Count);
            Assert.Contains(MacClipboardDetectionPattern.Links, (System.Collections.ICollection)patterns);
            Assert.Contains(MacClipboardDetectionPattern.PhoneNumbers, (System.Collections.ICollection)patterns);
        }

        [Test]
        public void TryParsePatterns_UnknownName_IsSkippedRatherThanFailing()
        {
            Assert.IsTrue(MacClipboardJsonParser.TryParsePatterns(
                "[\"links\",\"somethingNew\"]", out var patterns));
            Assert.AreEqual(1, patterns.Count);
        }

        [Test]
        public void TryParsePatterns_AnObject_Fails()
        {
            Assert.IsFalse(MacClipboardJsonParser.TryParsePatterns("{\"patterns\":[]}", out _));
        }

        // ── detected metadata ────────────────────────────────────────────────

        [Test]
        public void TryParseDetectedMetadata_ExplicitNullContentType_IsRead()
        {
            Assert.IsTrue(MacClipboardJsonParser.TryParseDetectedMetadata(
                "{\"metadataTypes\":[\"contentType\"],\"contentTypeIdentifier\":null}", out var metadata));
            Assert.AreEqual(1, metadata!.MetadataTypes.Count);
            Assert.IsNull(metadata.ContentTypeIdentifier);
        }

        [Test]
        public void TryParseDetectedMetadata_MissingContentTypeKey_Fails()
        {
            Assert.IsFalse(MacClipboardJsonParser.TryParseDetectedMetadata(
                "{\"metadataTypes\":[]}", out _));
        }

        // ── detected values ──────────────────────────────────────────────────

        private const string FullDetectedValues =
            "{\"patterns\":[\"links\"]," +
            "\"probableWebURL\":\"https://example.com\",\"probableWebSearch\":null,\"number\":12.5," +
            "\"links\":[{\"matchedString\":\"m\",\"url\":\"https://example.com\"}]," +
            "\"phoneNumbers\":[{\"matchedString\":\"p\",\"phoneNumber\":\"+81\",\"label\":\"home\"}]," +
            "\"emailAddresses\":[{\"matchedString\":\"e\",\"emailAddress\":\"a@b.c\",\"label\":null}]," +
            "\"postalAddresses\":[{\"matchedString\":\"a\",\"street\":\"s\",\"city\":null,\"state\":null," +
            "\"postalCode\":null,\"country\":\"JP\"}]," +
            "\"calendarEvents\":[{\"matchedString\":\"c\",\"isAllDay\":true," +
            "\"startDate\":\"2026-09-03T00:00:00Z\",\"startTimeZoneIdentifier\":\"Asia/Tokyo\"," +
            "\"endDate\":null,\"endTimeZoneIdentifier\":null}]," +
            "\"shipmentTrackingNumbers\":[{\"matchedString\":\"s\",\"carrier\":\"X\",\"trackingNumber\":\"1\"}]," +
            "\"flightNumbers\":[{\"matchedString\":\"f\",\"airline\":\"NH\",\"flightNumber\":\"1\"}]," +
            "\"moneyAmounts\":[{\"matchedString\":\"m\",\"currencyCode\":\"JPY\",\"amount\":100.0}]}";

        [Test]
        public void TryParseDetectedValues_ReadsEveryNestedShape()
        {
            Assert.IsTrue(MacClipboardJsonParser.TryParseDetectedValues(FullDetectedValues, out var values));
            Assert.AreEqual("https://example.com", values!.ProbableWebUrl);
            Assert.AreEqual(12.5, values.Number);
            Assert.AreEqual("https://example.com", values.Links[0].Url);
            Assert.AreEqual("+81", values.PhoneNumbers[0].Value);
            Assert.AreEqual("home", values.PhoneNumbers[0].Label);
            Assert.AreEqual("a@b.c", values.EmailAddresses[0].Value);
            Assert.AreEqual("s", values.PostalAddresses[0].Street);
            Assert.AreEqual("JP", values.PostalAddresses[0].Country);
            Assert.IsTrue(values.CalendarEvents[0].IsAllDay);
            Assert.AreEqual("Asia/Tokyo", values.CalendarEvents[0].StartTimeZoneIdentifier);
            Assert.AreEqual("X", values.ShipmentTrackingNumbers[0].Carrier);
            Assert.AreEqual("NH", values.FlightNumbers[0].Airline);
            Assert.AreEqual(100.0, values.MoneyAmounts[0].Amount);
        }

        [Test]
        public void TryParseDetectedValues_ExplicitNulls_BecomeNullRatherThanFailing()
        {
            Assert.IsTrue(MacClipboardJsonParser.TryParseDetectedValues(FullDetectedValues, out var values));
            Assert.IsNull(values!.ProbableWebSearch);
            Assert.IsNull(values.EmailAddresses[0].Label);
            Assert.IsNull(values.PostalAddresses[0].City);
            Assert.IsNull(values.CalendarEvents[0].EndDate);
        }

        [Test]
        public void TryParseDetectedValues_ParsesIso8601DatesAsUtc()
        {
            Assert.IsTrue(MacClipboardJsonParser.TryParseDetectedValues(FullDetectedValues, out var values));
            DateTimeOffset start = values!.CalendarEvents[0].StartDate!.Value;
            Assert.AreEqual(2026, start.Year);
            Assert.AreEqual(TimeSpan.Zero, start.Offset);
        }

        [Test]
        public void TryParseDetectedValues_UnparsableDate_LeavesTheFieldNullWithoutFailing()
        {
            // The rest of the match is still usable, and the format is the detector's to change.
            string json = FullDetectedValues.Replace("\"2026-09-03T00:00:00Z\"", "\"not-a-date\"");
            Assert.IsTrue(MacClipboardJsonParser.TryParseDetectedValues(json, out var values));
            Assert.IsNull(values!.CalendarEvents[0].StartDate);
            Assert.AreEqual("Asia/Tokyo", values.CalendarEvents[0].StartTimeZoneIdentifier);
        }

        [Test]
        public void TryParseDetectedValues_MissingKey_Fails()
        {
            // Distinct from an explicit null: absence means the response does not match the schema.
            string json = FullDetectedValues.Replace("\"probableWebSearch\":null,", string.Empty);
            Assert.IsFalse(MacClipboardJsonParser.TryParseDetectedValues(json, out _));
        }

        // ── change event ─────────────────────────────────────────────────────

        [Test]
        public void TryParseChangeEvent_ReadsScopeAndCount()
        {
            Assert.IsTrue(MacClipboardJsonParser.TryParseChangeEvent(
                "{\"scope\":{\"kind\":\"general\"},\"changeCount\":9}", out var changeEvent));
            Assert.AreEqual(MacPasteboardScopeKind.General, changeEvent!.Scope.Kind);
            Assert.AreEqual(9L, changeEvent.ChangeCount);
        }

        [Test]
        public void TryParseChangeEvent_MalformedJson_Fails()
        {
            Assert.IsFalse(MacClipboardJsonParser.TryParseChangeEvent("{\"scope\":{}}", out _));
        }
    }
}
#endif
