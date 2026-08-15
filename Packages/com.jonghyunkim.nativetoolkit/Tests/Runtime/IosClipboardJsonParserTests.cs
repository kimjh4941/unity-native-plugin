#nullable enable

#if UNITY_IOS || UNITY_EDITOR
using System;
using NUnit.Framework;
using UnityEngine.TestTools;
using JonghyunKim.NativeToolkit.Runtime.Clipboard;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// EditMode tests for the iOS clipboard response parser.
    /// <para>
    /// The parser logs an error for every malformed input, and most cases here are deliberately
    /// malformed, so failing log messages are ignored per test rather than expected
    /// one by one (see IgnoreExpectedParserErrorLogs).
    /// </para>
    /// </summary>
    public sealed class IosClipboardJsonParserTests
    {
        [TearDown]
        public void TearDown() => LogAssert.ignoreFailingMessages = false;

        // The parser logs an error for every malformed input, and most cases here are deliberately
        // malformed. The flag must be set inside the test body: assigning it from [SetUp] is reset
        // when the framework opens the per-test log scope.
        private static void IgnoreExpectedParserErrorLogs() => LogAssert.ignoreFailingMessages = true;

        // ── envelope (E-1 .. E-7) ───────────────────────────────────────────────

        [Test]
        public void E1_NullOrBlankResponse_ReportsNoData()
        {
            IgnoreExpectedParserErrorLogs();
            foreach (string? json in new[] { null, "", "   " })
            {
                IosClipboardReadResult result = IosClipboardJsonParser.ParseReadResult(json);

                Assert.IsFalse(result.IsSuccess);
                Assert.AreEqual(IosClipboardJsonParser.UnknownErrorCode, result.Error!.Value.Code);
                Assert.AreEqual(IosClipboardJsonParser.NoDataMessage, result.Error.Value.Message);
            }
        }

        [Test]
        public void E2_UnparsableOrNonObjectRoot_ReportsParseFailure()
        {
            IgnoreExpectedParserErrorLogs();
            foreach (string json in new[] { "{", "[1,2]", "\"text\"", "12" })
            {
                IosClipboardReadResult result = IosClipboardJsonParser.ParseReadResult(json);

                Assert.IsFalse(result.IsSuccess, json);
                Assert.AreEqual(IosClipboardJsonParser.ParseFailedMessage, result.Error!.Value.Message, json);
            }
        }

        [Test]
        public void E3_MissingOrNonBooleanOk_IsNeverTreatedAsSuccess()
        {
            IgnoreExpectedParserErrorLogs();
            foreach (string json in new[] { "{\"data\":{}}", "{\"ok\":\"true\",\"data\":{}}", "{\"ok\":1}" })
            {
                IosClipboardReadResult result = IosClipboardJsonParser.ParseReadResult(json);

                Assert.IsFalse(result.IsSuccess, json);
                Assert.AreEqual(IosClipboardJsonParser.ParseFailedMessage, result.Error!.Value.Message, json);
            }
        }

        [Test]
        public void E4_FailureWithoutErrorObject_FallsBackToUnknown()
        {
            IgnoreExpectedParserErrorLogs();
            foreach (string json in new[] { "{\"ok\":false}", "{\"ok\":false,\"error\":\"boom\"}" })
            {
                IosClipboardReadResult result = IosClipboardJsonParser.ParseReadResult(json);

                Assert.IsFalse(result.IsSuccess, json);
                Assert.AreEqual(IosClipboardErrorInfo.UnknownErrorCode, result.Error!.Value.Code, json);
                Assert.AreEqual(IosClipboardErrorInfo.UnknownErrorMessage, result.Error.Value.Message, json);
            }
        }

        [Test]
        public void E5AndE6_MissingCodeOrMessage_FallBackIndependently()
        {
            IgnoreExpectedParserErrorLogs();
            IosClipboardReadResult noCode = IosClipboardJsonParser.ParseReadResult(
                "{\"ok\":false,\"error\":{\"message\":\"Something specific.\"}}");
            Assert.AreEqual(IosClipboardErrorInfo.UnknownErrorCode, noCode.Error!.Value.Code);
            Assert.AreEqual("Something specific.", noCode.Error.Value.Message);

            IosClipboardReadResult noMessage = IosClipboardJsonParser.ParseReadResult(
                "{\"ok\":false,\"error\":{\"code\":\"CLIPBOARD_UNAVAILABLE\"}}");
            Assert.AreEqual("CLIPBOARD_UNAVAILABLE", noMessage.Error!.Value.Code);
            Assert.AreEqual(IosClipboardErrorInfo.UnknownErrorMessage, noMessage.Error.Value.Message);
        }

        [Test]
        public void ErrorEnvelope_WithDetails_ExposesDomainAndNativeCode()
        {
            IgnoreExpectedParserErrorLogs();
            IosClipboardReadResult result = IosClipboardJsonParser.ParseReadResult(
                "{\"ok\":false,\"error\":{\"code\":\"CLIPBOARD_LOAD_FAILED\",\"message\":\"Failed to load the clipboard item.\"," +
                "\"details\":{\"domain\":\"NSItemProviderErrorDomain\",\"code\":-1000}}}");

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual("CLIPBOARD_LOAD_FAILED", result.Error!.Value.Code);
            Assert.AreEqual("NSItemProviderErrorDomain", result.Error.Value.Domain);
            Assert.AreEqual(-1000, result.Error.Value.NativeCode);
        }

        [Test]
        public void E7_PartialDetails_LeaveDomainAndNativeCodeNull()
        {
            IgnoreExpectedParserErrorLogs();
            foreach (string details in new[] { "\"details\":{\"domain\":\"D\"}", "\"details\":{\"code\":5}", "\"details\":[]" })
            {
                IosClipboardReadResult result = IosClipboardJsonParser.ParseReadResult(
                    "{\"ok\":false,\"error\":{\"code\":\"C\",\"message\":\"M\"," + details + "}}");

                Assert.IsFalse(result.IsSuccess, details);
                Assert.AreEqual("C", result.Error!.Value.Code, details);
                Assert.IsNull(result.Error.Value.Domain, details);
                Assert.IsNull(result.Error.Value.NativeCode, details);
            }
        }

        [Test]
        public void E8AndE9_MissingOrNullData_FailsForEveryOperationExceptReadData()
        {
            IgnoreExpectedParserErrorLogs();
            foreach (string json in new[] { "{\"ok\":true}", "{\"ok\":true,\"data\":null}" })
            {
                Assert.IsFalse(IosClipboardJsonParser.ParseReadResult(json).IsSuccess, json);
                Assert.IsFalse(IosClipboardJsonParser.ParseSnapshotResult(json).IsSuccess, json);
                Assert.IsFalse(IosClipboardJsonParser.ParsePasteboardScopeResult(json).IsSuccess, json);
                Assert.IsFalse(IosClipboardJsonParser.ParseDetectedPatternsResult(json).IsSuccess, json);
                Assert.IsFalse(IosClipboardJsonParser.ParseForegroundChangeResult(json).IsSuccess, json);
            }
        }

        [Test]
        public void E10_DataOfUnexpectedShape_Fails()
        {
            IgnoreExpectedParserErrorLogs();
            IosClipboardReadResult result = IosClipboardJsonParser.ParseReadResult("{\"ok\":true,\"data\":[1,2]}");

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(IosClipboardJsonParser.ParseFailedMessage, result.Error!.Value.Message);
        }

        // ── read ────────────────────────────────────────────────────────────────

        [Test]
        public void ParseReadResult_FullPayload_IsMapped()
        {
            IgnoreExpectedParserErrorLogs();
            IosClipboardReadResult result = IosClipboardJsonParser.ParseReadResult(
                "{\"ok\":true,\"data\":{\"numberOfItems\":2,\"items\":[" +
                "{\"typeIdentifiers\":[\"public.utf8-plain-text\"],\"text\":\"hello\",\"urlString\":null,\"imageDataUTType\":null}," +
                "{\"typeIdentifiers\":[],\"text\":null,\"urlString\":\"https://a.example\",\"imageDataUTType\":\"public.png\"}]}}");

            Assert.IsTrue(result.IsSuccess);
            Assert.IsNull(result.Error);
            Assert.AreEqual(2, result.NumberOfItems);
            Assert.AreEqual(2, result.Items.Count);

            Assert.AreEqual("hello", result.Items[0].Text);
            Assert.IsNull(result.Items[0].UrlString);
            Assert.AreEqual("public.utf8-plain-text", result.Items[0].TypeIdentifiers[0]);

            Assert.AreEqual("https://a.example", result.Items[1].UrlString);
            Assert.AreEqual("public.png", result.Items[1].ImageDataUtType);
            Assert.AreEqual(0, result.Items[1].TypeIdentifiers.Count);
        }

        [Test]
        public void ParseReadResult_EmptyClipboard_IsSuccessNotFailure()
        {
            IgnoreExpectedParserErrorLogs();
            IosClipboardReadResult result = IosClipboardJsonParser.ParseReadResult(
                "{\"ok\":true,\"data\":{\"numberOfItems\":0,\"items\":[]}}");

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(0, result.NumberOfItems);
            Assert.AreEqual(0, result.Items.Count);
        }

        [Test]
        public void E11_ReadResult_MissingRequiredField_Fails()
        {
            IgnoreExpectedParserErrorLogs();
            Assert.IsFalse(IosClipboardJsonParser.ParseReadResult(
                "{\"ok\":true,\"data\":{\"items\":[]}}").IsSuccess);
            Assert.IsFalse(IosClipboardJsonParser.ParseReadResult(
                "{\"ok\":true,\"data\":{\"numberOfItems\":0}}").IsSuccess);
            Assert.IsFalse(IosClipboardJsonParser.ParseReadResult(
                "{\"ok\":true,\"data\":{\"numberOfItems\":0,\"items\":{}}}").IsSuccess);
            Assert.IsFalse(IosClipboardJsonParser.ParseReadResult(
                "{\"ok\":true,\"data\":{\"numberOfItems\":1,\"items\":[\"nope\"]}}").IsSuccess);
        }

        [Test]
        public void E12_ReadItem_OptionalKeysMayBeAbsent()
        {
            IgnoreExpectedParserErrorLogs();
            IosClipboardReadResult result = IosClipboardJsonParser.ParseReadResult(
                "{\"ok\":true,\"data\":{\"numberOfItems\":1,\"items\":[{}]}}");

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(0, result.Items[0].TypeIdentifiers.Count);
            Assert.IsNull(result.Items[0].Text);
        }

        // ── readData ────────────────────────────────────────────────────────────

        [Test]
        public void ParseReadDataResult_Payload_IsDecoded()
        {
            IgnoreExpectedParserErrorLogs();
            IosClipboardReadDataResult result = IosClipboardJsonParser.ParseReadDataResult(
                "{\"ok\":true,\"data\":{\"utType\":\"public.png\",\"base64\":\"AQID\",\"byteCount\":3}}");

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.HasData);
            Assert.AreEqual("public.png", result.UtType);
            Assert.AreEqual(new byte[] { 1, 2, 3 }, result.Data);
            Assert.AreEqual(3, result.ByteCount);
        }

        [Test]
        public void ParseReadDataResult_NullOrMissingData_IsSuccessWithoutData()
        {
            IgnoreExpectedParserErrorLogs();
            foreach (string json in new[] { "{\"ok\":true,\"data\":null}", "{\"ok\":true}" })
            {
                IosClipboardReadDataResult result = IosClipboardJsonParser.ParseReadDataResult(json);

                Assert.IsTrue(result.IsSuccess, json);
                Assert.IsFalse(result.HasData, json);
                Assert.IsNull(result.Data, json);
                Assert.IsNull(result.UtType, json);
                Assert.AreEqual(0, result.ByteCount, json);
            }
        }

        [Test]
        public void E15_ByteCountMismatch_Fails()
        {
            IgnoreExpectedParserErrorLogs();
            IosClipboardReadDataResult result = IosClipboardJsonParser.ParseReadDataResult(
                "{\"ok\":true,\"data\":{\"utType\":\"t\",\"base64\":\"AQID\",\"byteCount\":99}}");

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(IosClipboardJsonParser.ParseFailedMessage, result.Error!.Value.Message);
        }

        [Test]
        public void E16_MalformedBase64_ReportsDecodeFailure()
        {
            IgnoreExpectedParserErrorLogs();
            IosClipboardReadDataResult result = IosClipboardJsonParser.ParseReadDataResult(
                "{\"ok\":true,\"data\":{\"utType\":\"t\",\"base64\":\"AAA\",\"byteCount\":2}}");

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(IosClipboardJsonParser.UnknownErrorCode, result.Error!.Value.Code);
            Assert.AreEqual(IosClipboardJsonParser.DecodeFailedMessage, result.Error.Value.Message);
        }

        [Test]
        public void ParseReadDataResult_MissingRequiredField_Fails()
        {
            IgnoreExpectedParserErrorLogs();
            Assert.IsFalse(IosClipboardJsonParser.ParseReadDataResult(
                "{\"ok\":true,\"data\":{\"base64\":\"AQID\",\"byteCount\":3}}").IsSuccess);
            Assert.IsFalse(IosClipboardJsonParser.ParseReadDataResult(
                "{\"ok\":true,\"data\":{\"utType\":\"t\",\"byteCount\":3}}").IsSuccess);
            Assert.IsFalse(IosClipboardJsonParser.ParseReadDataResult(
                "{\"ok\":true,\"data\":{\"utType\":\"t\",\"base64\":\"AQID\"}}").IsSuccess);
        }

        // ── snapshot ────────────────────────────────────────────────────────────

        private const string SnapshotBody =
            "\"hasStrings\":true,\"hasURLs\":false,\"hasImages\":true,\"hasColors\":false," +
            "\"numberOfItems\":2,\"typeIdentifiers\":[\"a\"],\"allTypeIdentifiers\":[[\"a\"],[\"b\",\"c\"]]";

        [Test]
        public void ParseSnapshotResult_NestedTypeIdentifiers_ArePreserved()
        {
            IgnoreExpectedParserErrorLogs();
            IosClipboardSnapshotResult result = IosClipboardJsonParser.ParseSnapshotResult(
                "{\"ok\":true,\"data\":{" + SnapshotBody + "}}");

            Assert.IsTrue(result.IsSuccess);
            IosClipboardSnapshot snapshot = result.Snapshot!;
            Assert.IsTrue(snapshot.HasStrings);
            Assert.IsFalse(snapshot.HasUrls);
            Assert.IsTrue(snapshot.HasImages);
            Assert.IsFalse(snapshot.HasColors);
            Assert.AreEqual(2, snapshot.NumberOfItems);
            Assert.AreEqual(1, snapshot.TypeIdentifiers.Count);
            Assert.AreEqual(2, snapshot.AllTypeIdentifiers.Count);
            Assert.AreEqual(2, snapshot.AllTypeIdentifiers[1].Count);
            Assert.AreEqual("c", snapshot.AllTypeIdentifiers[1][1]);
        }

        [Test]
        public void ParseSnapshotResult_MatchingItemIndexes_DistinguishesNullFromEmpty()
        {
            IgnoreExpectedParserErrorLogs();
            IosClipboardSnapshotResult notRequested = IosClipboardJsonParser.ParseSnapshotResult(
                "{\"ok\":true,\"data\":{" + SnapshotBody + ",\"matchingItemIndexes\":null}}");
            Assert.IsNull(notRequested.Snapshot!.MatchingItemIndexes, "null means matchingTypes were not requested");

            IosClipboardSnapshotResult absent = IosClipboardJsonParser.ParseSnapshotResult(
                "{\"ok\":true,\"data\":{" + SnapshotBody + "}}");
            Assert.IsNull(absent.Snapshot!.MatchingItemIndexes);

            IosClipboardSnapshotResult noMatches = IosClipboardJsonParser.ParseSnapshotResult(
                "{\"ok\":true,\"data\":{" + SnapshotBody + ",\"matchingItemIndexes\":[]}}");
            Assert.IsNotNull(noMatches.Snapshot!.MatchingItemIndexes, "empty means requested but nothing matched");
            Assert.AreEqual(0, noMatches.Snapshot.MatchingItemIndexes!.Count);

            IosClipboardSnapshotResult matched = IosClipboardJsonParser.ParseSnapshotResult(
                "{\"ok\":true,\"data\":{" + SnapshotBody + ",\"matchingItemIndexes\":[0,2]}}");
            Assert.AreEqual(new[] { 0, 2 }, matched.Snapshot!.MatchingItemIndexes);
        }

        [Test]
        public void ParseSnapshotResult_NestedRowThatIsNotAnArray_Fails()
        {
            IgnoreExpectedParserErrorLogs();
            // allTypeIdentifiers is required, so a non-array row is a structural break, not an
            // item that happens to have no types.
            string body = SnapshotBody.Replace("\"allTypeIdentifiers\":[[\"a\"],[\"b\",\"c\"]]",
                "\"allTypeIdentifiers\":[[\"a\"],\"oops\"]");

            IosClipboardSnapshotResult result = IosClipboardJsonParser.ParseSnapshotResult(
                "{\"ok\":true,\"data\":{" + body + "}}");

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(IosClipboardJsonParser.ParseFailedMessage, result.Error!.Value.Message);
        }

        [Test]
        public void ParseSnapshotResult_NonStringElementInAnyTypeArray_Fails()
        {
            IgnoreExpectedParserErrorLogs();
            // The snapshot arrays are required fields, so a wrong element type is a structural
            // break. Dropping it would report a shorter list the bridge never sent.
            var cases = new[]
            {
                SnapshotBody.Replace("\"typeIdentifiers\":[\"a\"]", "\"typeIdentifiers\":[\"a\",7]"),
                SnapshotBody.Replace("\"typeIdentifiers\":[\"a\"]", "\"typeIdentifiers\":[null]"),
                SnapshotBody.Replace("\"allTypeIdentifiers\":[[\"a\"],[\"b\",\"c\"]]", "\"allTypeIdentifiers\":[[7]]"),
                SnapshotBody.Replace("\"allTypeIdentifiers\":[[\"a\"],[\"b\",\"c\"]]",
                    "\"allTypeIdentifiers\":[[\"a\"],[\"b\",{}]]")
            };

            foreach (string body in cases)
            {
                IosClipboardSnapshotResult result = IosClipboardJsonParser.ParseSnapshotResult(
                    "{\"ok\":true,\"data\":{" + body + "}}");

                Assert.IsFalse(result.IsSuccess, body);
                Assert.AreEqual(IosClipboardJsonParser.ParseFailedMessage, result.Error!.Value.Message, body);
            }
        }

        [Test]
        public void ParseSnapshotResult_NonIntegerElementInMatchingItemIndexes_Fails()
        {
            IgnoreExpectedParserErrorLogs();
            foreach (string value in new[] { "[0,\"x\"]", "[null]", "[1.5]", "[[0]]" })
            {
                IosClipboardSnapshotResult result = IosClipboardJsonParser.ParseSnapshotResult(
                    "{\"ok\":true,\"data\":{" + SnapshotBody + ",\"matchingItemIndexes\":" + value + "}}");

                Assert.IsFalse(result.IsSuccess, value);
                Assert.AreEqual(IosClipboardJsonParser.ParseFailedMessage, result.Error!.Value.Message, value);
            }
        }

        [Test]
        public void ParseSnapshotResult_WellFormedArrays_StillSucceed()
        {
            IgnoreExpectedParserErrorLogs();
            IosClipboardSnapshotResult result = IosClipboardJsonParser.ParseSnapshotResult(
                "{\"ok\":true,\"data\":{" + SnapshotBody + ",\"matchingItemIndexes\":[0,2]}}");

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(new[] { 0, 2 }, result.Snapshot!.MatchingItemIndexes);
            Assert.AreEqual("a", result.Snapshot.TypeIdentifiers[0]);
        }

        [Test]
        public void ParseReadResult_NonStringTypeIdentifier_IsStillSkipped()
        {
            IgnoreExpectedParserErrorLogs();
            // read items' typeIdentifiers is an optional per-item field, so it keeps the lenient
            // behaviour; only the snapshot payload is strict.
            IosClipboardReadResult result = IosClipboardJsonParser.ParseReadResult(
                "{\"ok\":true,\"data\":{\"numberOfItems\":1,\"items\":[{\"typeIdentifiers\":[\"a\",7]}]}}");

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(1, result.Items[0].TypeIdentifiers.Count);
        }

        [Test]
        public void ParseSnapshotResult_MatchingItemIndexesOfWrongType_Fails()
        {
            IgnoreExpectedParserErrorLogs();
            // Folding a broken shape into null ("not requested") or empty ("requested, no match")
            // would misreport what the pasteboard was asked, so it fails instead.
            foreach (string value in new[] { "5", "\"a\"", "{}" })
            {
                IosClipboardSnapshotResult result = IosClipboardJsonParser.ParseSnapshotResult(
                    "{\"ok\":true,\"data\":{" + SnapshotBody + ",\"matchingItemIndexes\":" + value + "}}");

                Assert.IsFalse(result.IsSuccess, value);
                Assert.AreEqual(IosClipboardJsonParser.ParseFailedMessage, result.Error!.Value.Message, value);
            }
        }

        [Test]
        public void ParseSnapshotResult_MissingRequiredField_Fails()
        {
            IgnoreExpectedParserErrorLogs();
            Assert.IsFalse(IosClipboardJsonParser.ParseSnapshotResult(
                "{\"ok\":true,\"data\":{\"hasStrings\":true}}").IsSuccess);
            Assert.IsFalse(IosClipboardJsonParser.ParseSnapshotResult(
                "{\"ok\":true,\"data\":{" + SnapshotBody.Replace("\"hasURLs\":false,", string.Empty) + "}}").IsSuccess);
        }

        // ── createPasteboard ────────────────────────────────────────────────────

        [Test]
        public void ParsePasteboardScopeResult_AllKinds()
        {
            IgnoreExpectedParserErrorLogs();
            IosPasteboardScopeResult general = IosClipboardJsonParser.ParsePasteboardScopeResult(
                "{\"ok\":true,\"data\":{\"scope\":{\"kind\":\"general\"}}}");
            Assert.IsTrue(general.IsSuccess);
            Assert.AreEqual(IosPasteboardScopeKind.General, general.Scope!.Kind);

            IosPasteboardScopeResult named = IosClipboardJsonParser.ParsePasteboardScopeResult(
                "{\"ok\":true,\"data\":{\"scope\":{\"kind\":\"named\",\"name\":\"group.a\"}}}");
            Assert.AreEqual(IosPasteboardScopeKind.Named, named.Scope!.Kind);
            Assert.AreEqual("group.a", named.Scope.Name);

            IosPasteboardScopeResult unique = IosClipboardJsonParser.ParsePasteboardScopeResult(
                "{\"ok\":true,\"data\":{\"scope\":{\"kind\":\"unique\",\"name\":\"generated\"}}}");
            Assert.AreEqual(IosPasteboardScopeKind.Unique, unique.Scope!.Kind);
            Assert.AreEqual("generated", unique.Scope.Name);
        }

        [Test]
        public void ParsePasteboardScopeResult_MissingNameOrUnknownKind_Fails()
        {
            IgnoreExpectedParserErrorLogs();
            Assert.IsFalse(IosClipboardJsonParser.ParsePasteboardScopeResult(
                "{\"ok\":true,\"data\":{\"scope\":{\"kind\":\"named\"}}}").IsSuccess);
            Assert.IsFalse(IosClipboardJsonParser.ParsePasteboardScopeResult(
                "{\"ok\":true,\"data\":{\"scope\":{\"kind\":\"future\"}}}").IsSuccess);
            Assert.IsFalse(IosClipboardJsonParser.ParsePasteboardScopeResult(
                "{\"ok\":true,\"data\":{}}").IsSuccess);
        }

        // ── detection ───────────────────────────────────────────────────────────

        [Test]
        public void ParseDetectedPatternsResult_MapsRawValues()
        {
            IgnoreExpectedParserErrorLogs();
            IosClipboardDetectedPatternsResult result = IosClipboardJsonParser.ParseDetectedPatternsResult(
                "{\"ok\":true,\"data\":{\"patterns\":[\"probableWebURL\",\"emailAddress\"]}}");

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(2, result.Patterns.Count);
            Assert.Contains(IosClipboardDetectionPattern.ProbableWebUrl, (System.Collections.ICollection)result.Patterns);
            Assert.Contains(IosClipboardDetectionPattern.EmailAddress, (System.Collections.ICollection)result.Patterns);
        }

        [Test]
        public void ParseDetectedPatternsResult_UnknownRawValue_IsSkippedNotFailed()
        {
            IgnoreExpectedParserErrorLogs();
            IosClipboardDetectedPatternsResult result = IosClipboardJsonParser.ParseDetectedPatternsResult(
                "{\"ok\":true,\"data\":{\"patterns\":[\"link\",\"somethingNewInTheFuture\",42]}}");

            Assert.IsTrue(result.IsSuccess, "version skew must not fail the whole result");
            Assert.AreEqual(1, result.Patterns.Count);
            Assert.AreEqual(IosClipboardDetectionPattern.Link, result.Patterns[0]);
        }

        [Test]
        public void ParseDetectedValuesResult_FullPayload_IsMapped()
        {
            IgnoreExpectedParserErrorLogs();
            IosClipboardDetectedValuesResult result = IosClipboardJsonParser.ParseDetectedValuesResult(
                "{\"ok\":true,\"data\":{" +
                "\"detectedPatterns\":[\"number\",\"moneyAmount\"]," +
                "\"probableWebURL\":\"https://a.example\",\"probableWebSearch\":null,\"number\":42.5," +
                "\"links\":[\"https://b.example\"]," +
                "\"emailAddresses\":[{\"value\":\"a@b.example\",\"label\":\"home\"},{\"value\":\"c@d.example\",\"label\":null}]," +
                "\"phoneNumbers\":[{\"value\":\"+81-3-0000-0000\",\"label\":null}]," +
                "\"postalAddresses\":[{\"street\":\"S\",\"city\":\"C\",\"state\":null,\"postalCode\":\"100-0001\",\"country\":\"JP\"}]," +
                "\"calendarEvents\":[{\"startDate\":\"2026-08-15T09:00:00.000Z\",\"endDate\":null,\"startTimeZone\":\"Asia/Tokyo\",\"endTimeZone\":null,\"isAllDay\":true}]," +
                "\"flightNumbers\":[{\"airline\":\"NH\",\"flightNumber\":\"105\"}]," +
                "\"moneyAmounts\":[{\"amount\":1234.5,\"currency\":\"JPY\"}]," +
                "\"shipmentTrackingNumbers\":[{\"carrier\":\"YAMATO\",\"trackingNumber\":\"1234\"}]}}");

            Assert.IsTrue(result.IsSuccess);
            IosClipboardDetectedValues values = result.Values!;

            Assert.AreEqual(2, values.DetectedPatterns.Count);
            Assert.AreEqual("https://a.example", values.ProbableWebUrl);
            Assert.IsNull(values.ProbableWebSearch);
            Assert.AreEqual(42.5d, values.Number!.Value, 1e-9);
            Assert.AreEqual(1, values.Links.Count);

            Assert.AreEqual(2, values.EmailAddresses.Count);
            Assert.AreEqual("home", values.EmailAddresses[0].Label);
            Assert.IsNull(values.EmailAddresses[1].Label);

            Assert.AreEqual(1, values.PhoneNumbers.Count);
            Assert.AreEqual("100-0001", values.PostalAddresses[0].PostalCode);
            Assert.IsNull(values.PostalAddresses[0].State);

            Assert.IsTrue(values.CalendarEvents[0].IsAllDay);
            Assert.IsNotNull(values.CalendarEvents[0].StartDate);
            Assert.IsNull(values.CalendarEvents[0].EndDate);
            Assert.AreEqual("Asia/Tokyo", values.CalendarEvents[0].StartTimeZone);

            Assert.AreEqual("NH", values.FlightNumbers[0].Airline);
            Assert.AreEqual(1234.5d, values.MoneyAmounts[0].Amount, 1e-9);
            Assert.AreEqual("JPY", values.MoneyAmounts[0].Currency);
            Assert.AreEqual("YAMATO", values.ShipmentTrackingNumbers[0].Carrier);
        }

        [Test]
        public void ParseDetectedValuesResult_OnlyRequiredField_YieldsEmptyCollections()
        {
            IgnoreExpectedParserErrorLogs();
            IosClipboardDetectedValuesResult result = IosClipboardJsonParser.ParseDetectedValuesResult(
                "{\"ok\":true,\"data\":{\"detectedPatterns\":[]}}");

            Assert.IsTrue(result.IsSuccess);
            IosClipboardDetectedValues values = result.Values!;
            Assert.AreEqual(0, values.DetectedPatterns.Count);
            Assert.IsNull(values.Number);
            Assert.AreEqual(0, values.Links.Count);
            Assert.AreEqual(0, values.EmailAddresses.Count);
            Assert.AreEqual(0, values.PostalAddresses.Count);
            Assert.AreEqual(0, values.CalendarEvents.Count);
            Assert.AreEqual(0, values.FlightNumbers.Count);
            Assert.AreEqual(0, values.MoneyAmounts.Count);
            Assert.AreEqual(0, values.ShipmentTrackingNumbers.Count);
        }

        [Test]
        public void ParseDetectedValuesResult_MalformedEntry_IsSkippedNotFailed()
        {
            IgnoreExpectedParserErrorLogs();
            IosClipboardDetectedValuesResult result = IosClipboardJsonParser.ParseDetectedValuesResult(
                "{\"ok\":true,\"data\":{\"detectedPatterns\":[]," +
                "\"emailAddresses\":[{\"label\":\"no value\"},{\"value\":\"ok@example.com\"}]," +
                "\"moneyAmounts\":[{\"currency\":\"JPY\"},{\"amount\":1,\"currency\":\"USD\"}]," +
                "\"flightNumbers\":[{\"airline\":\"NH\"}]}}");

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(1, result.Values!.EmailAddresses.Count);
            Assert.AreEqual("ok@example.com", result.Values.EmailAddresses[0].Value);
            Assert.AreEqual(1, result.Values.MoneyAmounts.Count);
            Assert.AreEqual("USD", result.Values.MoneyAmounts[0].Currency);
            Assert.AreEqual(0, result.Values.FlightNumbers.Count);
        }

        [Test]
        public void ParseDetectedValuesResult_MissingDetectedPatterns_Fails()
        {
            IgnoreExpectedParserErrorLogs();
            Assert.IsFalse(IosClipboardJsonParser.ParseDetectedValuesResult(
                "{\"ok\":true,\"data\":{\"links\":[]}}").IsSuccess);
        }

        // ── loadItem ────────────────────────────────────────────────────────────

        [Test]
        public void ParseLoadedItemResult_AllKinds()
        {
            IgnoreExpectedParserErrorLogs();
            IosClipboardLoadedItemResult text = IosClipboardJsonParser.ParseLoadedItemResult(
                "{\"ok\":true,\"data\":{\"kind\":\"text\",\"text\":\"hello\"}}");
            Assert.AreEqual(IosClipboardLoadedItemKind.Text, text.Item!.Kind);
            Assert.AreEqual("hello", text.Item.Text);

            IosClipboardLoadedItemResult url = IosClipboardJsonParser.ParseLoadedItemResult(
                "{\"ok\":true,\"data\":{\"kind\":\"url\",\"urlString\":\"https://a.example\"}}");
            Assert.AreEqual(IosClipboardLoadedItemKind.Url, url.Item!.Kind);
            Assert.AreEqual("https://a.example", url.Item.UrlString);

            IosClipboardLoadedItemResult image = IosClipboardJsonParser.ParseLoadedItemResult(
                "{\"ok\":true,\"data\":{\"kind\":\"imageData\",\"base64\":\"AQID\",\"utType\":\"public.png\"}}");
            Assert.AreEqual(IosClipboardLoadedItemKind.ImageData, image.Item!.Kind);
            Assert.AreEqual(new byte[] { 1, 2, 3 }, image.Item.Data);
            Assert.AreEqual("public.png", image.Item.UtType);

            IosClipboardLoadedItemResult file = IosClipboardJsonParser.ParseLoadedItemResult(
                "{\"ok\":true,\"data\":{\"kind\":\"file\",\"path\":\"/tmp/a.png\"}}");
            Assert.AreEqual(IosClipboardLoadedItemKind.File, file.Item!.Kind);
            Assert.AreEqual("/tmp/a.png", file.Item.Path);
        }

        [Test]
        public void ParseLoadedItemResult_UnknownOrFutureKind_IsSuccess()
        {
            IgnoreExpectedParserErrorLogs();
            foreach (string kind in new[] { "unknown", "somethingNew" })
            {
                IosClipboardLoadedItemResult result = IosClipboardJsonParser.ParseLoadedItemResult(
                    "{\"ok\":true,\"data\":{\"kind\":\"" + kind + "\"}}");

                Assert.IsTrue(result.IsSuccess, kind);
                Assert.AreEqual(IosClipboardLoadedItemKind.Unknown, result.Item!.Kind, kind);
            }
        }

        [Test]
        public void ParseLoadedItemResult_KindWithoutItsValue_Fails()
        {
            IgnoreExpectedParserErrorLogs();
            Assert.IsFalse(IosClipboardJsonParser.ParseLoadedItemResult(
                "{\"ok\":true,\"data\":{\"kind\":\"text\"}}").IsSuccess);
            Assert.IsFalse(IosClipboardJsonParser.ParseLoadedItemResult(
                "{\"ok\":true,\"data\":{\"kind\":\"url\"}}").IsSuccess);
            Assert.IsFalse(IosClipboardJsonParser.ParseLoadedItemResult(
                "{\"ok\":true,\"data\":{\"kind\":\"file\"}}").IsSuccess);
            Assert.IsFalse(IosClipboardJsonParser.ParseLoadedItemResult(
                "{\"ok\":true,\"data\":{\"kind\":\"imageData\",\"utType\":\"t\"}}").IsSuccess);
        }

        [Test]
        public void ParseLoadedItemResult_CancelledEnvelope_IsAFailureWithTheCancelledCode()
        {
            IgnoreExpectedParserErrorLogs();
            IosClipboardLoadedItemResult result = IosClipboardJsonParser.ParseLoadedItemResult(
                "{\"ok\":false,\"error\":{\"code\":\"CLIPBOARD_CANCELLED\",\"message\":\"The clipboard load was cancelled.\"}}");

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual("CLIPBOARD_CANCELLED", result.Error!.Value.Code);
        }

        // ── checkForegroundChange ───────────────────────────────────────────────

        [Test]
        public void ParseForegroundChangeResult_MapsChangedFlag()
        {
            IgnoreExpectedParserErrorLogs();
            Assert.IsTrue(IosClipboardJsonParser
                .ParseForegroundChangeResult("{\"ok\":true,\"data\":{\"changed\":true}}").Changed);
            Assert.IsFalse(IosClipboardJsonParser
                .ParseForegroundChangeResult("{\"ok\":true,\"data\":{\"changed\":false}}").Changed);
            Assert.IsFalse(IosClipboardJsonParser
                .ParseForegroundChangeResult("{\"ok\":true,\"data\":{}}").IsSuccess);
        }

        // ── change events (no envelope) ─────────────────────────────────────────

        [Test]
        public void ParseChangeEvent_KnownKinds_AreDelivered()
        {
            IgnoreExpectedParserErrorLogs();
            IosClipboardChangeEvent? changed = IosClipboardJsonParser.ParseChangeEvent(
                "{\"scope\":{\"kind\":\"general\"},\"kind\":\"changed\",\"typesAdded\":[\"a\"],\"typesRemoved\":[\"b\",\"c\"]}");
            Assert.IsNotNull(changed);
            Assert.AreEqual(IosClipboardChangeEventKind.Changed, changed!.Kind);
            Assert.AreEqual(IosPasteboardScopeKind.General, changed.Scope!.Kind);
            Assert.AreEqual(1, changed.TypesAdded.Count);
            Assert.AreEqual(2, changed.TypesRemoved.Count);

            Assert.AreEqual(
                IosClipboardChangeEventKind.ChangedDetectedOnForeground,
                IosClipboardJsonParser.ParseChangeEvent(
                    "{\"scope\":{\"kind\":\"general\"},\"kind\":\"changedDetectedOnForeground\"}")!.Kind);

            Assert.AreEqual(
                IosClipboardChangeEventKind.Removed,
                IosClipboardJsonParser.ParseChangeEvent(
                    "{\"scope\":{\"kind\":\"named\",\"name\":\"n\"},\"kind\":\"removed\"}")!.Kind);
        }

        [Test]
        public void ParseChangeEvent_NativeUnknownKind_IsDeliveredAsUnknown()
        {
            IgnoreExpectedParserErrorLogs();
            // The native layer emits "unknown" deliberately, so it must reach the subscriber.
            IosClipboardChangeEvent? result = IosClipboardJsonParser.ParseChangeEvent(
                "{\"scope\":{\"kind\":\"general\"},\"kind\":\"unknown\"}");

            Assert.IsNotNull(result);
            Assert.AreEqual(IosClipboardChangeEventKind.Unknown, result!.Kind);
        }

        [Test]
        public void ParseChangeEvent_UnparsableOrKindless_IsDropped()
        {
            IgnoreExpectedParserErrorLogs();
            foreach (string? json in new[] { null, "", "not json", "[]", "{}", "{\"scope\":{\"kind\":\"general\"}}", "{\"kind\":5}" })
            {
                Assert.IsNull(IosClipboardJsonParser.ParseChangeEvent(json), $"expected drop for: {json}");
            }
        }

        [Test]
        public void ParseChangeEvent_MissingOrMalformedScope_StillDelivers()
        {
            IgnoreExpectedParserErrorLogs();
            // The kind is the actionable part; losing the notification would be worse.
            IosClipboardChangeEvent? noScope = IosClipboardJsonParser.ParseChangeEvent("{\"kind\":\"changed\"}");
            Assert.IsNotNull(noScope);
            Assert.IsNull(noScope!.Scope);
            Assert.AreEqual(0, noScope.TypesAdded.Count);

            IosClipboardChangeEvent? badScope = IosClipboardJsonParser.ParseChangeEvent(
                "{\"kind\":\"changed\",\"scope\":{\"kind\":\"nope\"}}");
            Assert.IsNotNull(badScope);
            Assert.IsNull(badScope!.Scope);
        }
    }
}
#endif
