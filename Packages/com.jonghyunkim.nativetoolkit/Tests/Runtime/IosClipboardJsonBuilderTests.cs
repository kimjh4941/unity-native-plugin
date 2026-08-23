#nullable enable

#if UNITY_IOS || UNITY_EDITOR
using System;
using System.Collections.Generic;
using NUnit.Framework;
using JonghyunKim.NativeToolkit.Runtime.Clipboard;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// EditMode tests for the iOS clipboard request builders.
    /// One builder exists per bridge function that takes a requestJson argument (13 of 15);
    /// cancelLoads and stopObserving take no request and therefore have no builder.
    /// </summary>
    public sealed class IosClipboardJsonBuilderTests
    {
        // ── scope key omission ──────────────────────────────────────────────────

        [Test]
        public void Scope_Null_OmitsTheKeyEntirely()
        {
            // Only an omitted scope resolves to the general pasteboard natively; a present but
            // malformed one is a hard error, so null must never be emitted.
            string json = IosClipboardJsonBuilder.BuildReadJson(null);

            Assert.AreEqual("{}", json);
            StringAssert.DoesNotContain("scope", json);
        }

        [Test]
        public void Scope_General_EmitsKindWithoutName()
        {
            string json = IosClipboardJsonBuilder.BuildReadJson(IosPasteboardScope.General);

            StringAssert.Contains("\"kind\":\"general\"", json);
            StringAssert.DoesNotContain("\"name\"", json);
        }

        [Test]
        public void Scope_NamedAndUnique_EmitKindAndName()
        {
            string named = IosClipboardJsonBuilder.BuildClearJson(IosPasteboardScope.Named("group.example"));
            StringAssert.Contains("\"kind\":\"named\"", named);
            StringAssert.Contains("\"name\":\"group.example\"", named);

            string unique = IosClipboardJsonBuilder.BuildClearJson(IosPasteboardScope.Unique("generated-1"));
            StringAssert.Contains("\"kind\":\"unique\"", unique);
            StringAssert.Contains("\"name\":\"generated-1\"", unique);
        }

        [Test]
        public void Scope_BlankName_ThrowsAtTheCallSite()
        {
            Assert.Throws<ArgumentException>(() => IosPasteboardScope.Named(string.Empty));
            Assert.Throws<ArgumentException>(() => IosPasteboardScope.Named("   "));
            Assert.Throws<ArgumentException>(() => IosPasteboardScope.Unique(string.Empty));
            Assert.Throws<ArgumentException>(() => IosPasteboardCreationRequest.Named("  "));
        }

        // ── copy / append ───────────────────────────────────────────────────────

        [Test]
        public void BuildCopyJson_PlainText_EmitsKindAndText()
        {
            string json = IosClipboardJsonBuilder.BuildCopyJson(
                IosClipboardContent.PlainText("hello"), null, null);

            StringAssert.Contains("\"kind\":\"plainText\"", json);
            StringAssert.Contains("\"text\":\"hello\"", json);
        }

        [Test]
        public void BuildCopyJson_EmptyText_IsEmittedRatherThanRejected()
        {
            // Blank plain text is accepted natively; validation stays on one side only.
            string json = IosClipboardJsonBuilder.BuildCopyJson(
                IosClipboardContent.PlainText(string.Empty), null, null);

            StringAssert.Contains("\"text\":\"\"", json);
        }

        [Test]
        public void BuildCopyJson_AllContentKinds_EmitTheirRequiredKeys()
        {
            var cases = new Dictionary<IosClipboardContent, string[]>
            {
                { IosClipboardContent.HtmlText("p", "<b>p</b>"), new[] { "\"kind\":\"htmlText\"", "\"plain\":\"p\"", "\"html\":\"<b>p</b>\"" } },
                { IosClipboardContent.Url("https://example.com"), new[] { "\"kind\":\"url\"", "\"urlString\":\"https://example.com\"" } },
                { IosClipboardContent.ImageFile("/tmp/a.png"), new[] { "\"kind\":\"imageFile\"", "\"path\":\"/tmp/a.png\"" } },
                { IosClipboardContent.ImageData(new byte[] { 1, 2, 3 }, "public.png"), new[] { "\"kind\":\"imageData\"", "\"base64\":\"AQID\"", "\"utType\":\"public.png\"" } },
                { IosClipboardContent.CustomData(new byte[] { 1, 2, 3 }, "com.example.x"), new[] { "\"kind\":\"customData\"", "\"base64\":\"AQID\"", "\"utType\":\"com.example.x\"" } },
                { IosClipboardContent.MultipleText(new[] { "a", "b" }), new[] { "\"kind\":\"multipleText\"", "\"texts\":[\"a\",\"b\"]" } }
            };

            foreach (var pair in cases)
            {
                string json = IosClipboardJsonBuilder.BuildCopyJson(pair.Key, null, null);
                foreach (string fragment in pair.Value)
                {
                    StringAssert.Contains(fragment, json);
                }
            }
        }

        [Test]
        public void BuildCopyJson_Color_EmitsFiniteDoubleComponents()
        {
            string json = IosClipboardJsonBuilder.BuildCopyJson(
                IosClipboardContent.Color(0.5, 0, 1, 0.25), null, null);

            StringAssert.Contains("\"kind\":\"color\"", json);
            StringAssert.Contains("\"red\":0.5", json);
            StringAssert.Contains("\"green\":0", json);
            StringAssert.Contains("\"blue\":1", json);
            StringAssert.Contains("\"alpha\":0.25", json);
        }

        [Test]
        public void Color_NonFiniteComponent_ThrowsAtTheCallSite()
        {
            // Emitting NaN or Infinity would produce invalid JSON, which the native parser would
            // report as CLIPBOARD_INVALID_REQUEST instead of CLIPBOARD_INVALID_COLOR.
            Assert.Throws<ArgumentException>(() => IosClipboardContent.Color(double.NaN, 0, 0, 1));
            Assert.Throws<ArgumentException>(() => IosClipboardContent.Color(0, double.PositiveInfinity, 0, 1));
            Assert.Throws<ArgumentException>(() => IosClipboardContent.Color(0, 0, double.NegativeInfinity, 1));
            Assert.Throws<ArgumentException>(() => IosClipboardContent.Color(0, 0, 0, double.NaN));
        }

        [Test]
        public void BuildCopyJson_MultiRepresentation_EncodesEachValue()
        {
            var representations = new Dictionary<string, byte[]>
            {
                { "public.utf8-plain-text", new byte[] { 65 } }
            };

            string json = IosClipboardJsonBuilder.BuildCopyJson(
                IosClipboardContent.MultiRepresentation(representations), null, null);

            StringAssert.Contains("\"kind\":\"multiRepresentation\"", json);
            StringAssert.Contains("\"public.utf8-plain-text\":\"QQ==\"", json);
        }

        [Test]
        public void ContentFactories_NullArguments_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => IosClipboardContent.PlainText(null!));
            Assert.Throws<ArgumentNullException>(() => IosClipboardContent.HtmlText(null!, "x"));
            Assert.Throws<ArgumentNullException>(() => IosClipboardContent.Url(null!));
            Assert.Throws<ArgumentNullException>(() => IosClipboardContent.ImageFile(null!));
            Assert.Throws<ArgumentNullException>(() => IosClipboardContent.ImageData(null!, "t"));
            Assert.Throws<ArgumentNullException>(() => IosClipboardContent.MultipleText(null!));
            Assert.Throws<ArgumentNullException>(() => IosClipboardContent.MultiRepresentation(null!));
        }

        // ── copy options ────────────────────────────────────────────────────────

        [Test]
        public void BuildCopyJson_NullOptions_OmitsTheKey()
        {
            string json = IosClipboardJsonBuilder.BuildCopyJson(
                IosClipboardContent.PlainText("x"), null, null);

            StringAssert.DoesNotContain("options", json);
        }

        [Test]
        public void BuildCopyJson_PrivacyPreservingDefault_EmitsLocalOnlyTrueAndNoExpiration()
        {
            string json = IosClipboardJsonBuilder.BuildCopyJson(
                IosClipboardContent.PlainText("x"), null, IosClipboardCopyOptions.PrivacyPreservingDefault);

            StringAssert.Contains("\"localOnly\":true", json);
            StringAssert.DoesNotContain("expirationDate", json);
        }

        [Test]
        public void BuildCopyJson_ExplicitLocalOnlyValues_AreEmitted()
        {
            string on = IosClipboardJsonBuilder.BuildCopyJson(
                IosClipboardContent.PlainText("x"), null, IosClipboardCopyOptions.Create(true));
            StringAssert.Contains("\"localOnly\":true", on);

            string off = IosClipboardJsonBuilder.BuildCopyJson(
                IosClipboardContent.PlainText("x"), null, IosClipboardCopyOptions.Create(false));
            StringAssert.Contains("\"localOnly\":false", off);
        }

        [Test]
        public void CopyOptions_PrivacyPreservingDefault_KeepsContentOffUniversalClipboard()
        {
            // The type is a class specifically so no default(T) can invert this.
            Assert.IsTrue(IosClipboardCopyOptions.PrivacyPreservingDefault.LocalOnly);
            Assert.IsNull(IosClipboardCopyOptions.PrivacyPreservingDefault.ExpirationDate);
        }

        [Test]
        public void BuildCopyJson_ExpirationDate_IsUtcIso8601WithoutFractionalSeconds()
        {
            var expiry = new DateTime(2026, 8, 15, 12, 34, 56, DateTimeKind.Utc);

            string json = IosClipboardJsonBuilder.BuildCopyJson(
                IosClipboardContent.PlainText("x"),
                null,
                IosClipboardCopyOptions.Create(true, expiry));

            StringAssert.Contains("\"expirationDate\":\"2026-08-15T12:34:56Z\"", json);
        }

        [Test]
        public void BuildCopyJson_LocalExpirationDate_IsConvertedToUtc()
        {
            var local = new DateTime(2026, 8, 15, 12, 0, 0, DateTimeKind.Utc).ToLocalTime();

            string json = IosClipboardJsonBuilder.BuildCopyJson(
                IosClipboardContent.PlainText("x"), null, IosClipboardCopyOptions.Create(true, local));

            StringAssert.Contains("\"expirationDate\":\"2026-08-15T12:00:00Z\"", json);
        }

        [Test]
        public void BuildAppendJson_NeverEmitsOptions()
        {
            // The native layer rejects an append request carrying an options key, whatever its
            // value, so Append exposes no options parameter at all.
            string json = IosClipboardJsonBuilder.BuildAppendJson(
                IosClipboardContent.PlainText("x"), IosPasteboardScope.General);

            StringAssert.DoesNotContain("options", json);
            StringAssert.Contains("\"kind\":\"plainText\"", json);
        }

        // ── remaining requests ──────────────────────────────────────────────────

        [Test]
        public void BuildReadDataJson_EmitsUtType()
        {
            string json = IosClipboardJsonBuilder.BuildReadDataJson("public.png", null);
            Assert.AreEqual("{\"utType\":\"public.png\"}", json);
        }

        [Test]
        public void BuildGetSnapshotJson_NullOrEmptyMatchingTypes_OmitsTheKey()
        {
            Assert.AreEqual("{}", IosClipboardJsonBuilder.BuildGetSnapshotJson(null, null));
            Assert.AreEqual("{}", IosClipboardJsonBuilder.BuildGetSnapshotJson(null, Array.Empty<string>()));
        }

        [Test]
        public void BuildGetSnapshotJson_MatchingTypes_AreEmitted()
        {
            string json = IosClipboardJsonBuilder.BuildGetSnapshotJson(null, new[] { "public.png", "public.url" });
            StringAssert.Contains("\"matchingTypes\":[\"public.png\",\"public.url\"]", json);
        }

        [Test]
        public void BuildCreatePasteboardJson_NamedAndUnique()
        {
            Assert.AreEqual(
                "{\"request\":{\"kind\":\"named\",\"name\":\"group.a\"}}",
                IosClipboardJsonBuilder.BuildCreatePasteboardJson(IosPasteboardCreationRequest.Named("group.a")));

            Assert.AreEqual(
                "{\"request\":{\"kind\":\"unique\"}}",
                IosClipboardJsonBuilder.BuildCreatePasteboardJson(IosPasteboardCreationRequest.Unique));
        }

        [Test]
        public void BuildRemovePasteboardJson_AlwaysEmitsScope()
        {
            // Omitting the scope would resolve to the general pasteboard, which the native layer
            // refuses with CLIPBOARD_CANNOT_REMOVE_GENERAL.
            string json = IosClipboardJsonBuilder.BuildRemovePasteboardJson(IosPasteboardScope.Named("group.a"));
            StringAssert.Contains("\"scope\":{\"kind\":\"named\",\"name\":\"group.a\"}", json);
        }

        [Test]
        public void BuildDetectPatternsJson_UsesTheNativeRawValues()
        {
            string json = IosClipboardJsonBuilder.BuildDetectPatternsJson(
                new[]
                {
                    IosClipboardDetectionPattern.ProbableWebUrl,
                    IosClipboardDetectionPattern.EmailAddress,
                    IosClipboardDetectionPattern.ShipmentTrackingNumber
                },
                null);

            // probableWebURL keeps its native capitalization: an unknown raw value fails the
            // whole request natively.
            StringAssert.Contains("\"patterns\":[\"probableWebURL\",\"emailAddress\",\"shipmentTrackingNumber\"]", json);
        }

        [Test]
        public void BuildDetectValuesJson_MatchesDetectPatternsShape()
        {
            var patterns = new[] { IosClipboardDetectionPattern.MoneyAmount };
            Assert.AreEqual(
                IosClipboardJsonBuilder.BuildDetectPatternsJson(patterns, null),
                IosClipboardJsonBuilder.BuildDetectValuesJson(patterns, null));
        }

        [Test]
        public void BuildLoadItemJson_AllKinds()
        {
            StringAssert.Contains(
                "\"request\":{\"kind\":\"text\"}",
                IosClipboardJsonBuilder.BuildLoadItemJson(IosClipboardLoadRequest.Text, null));
            StringAssert.Contains(
                "\"request\":{\"kind\":\"url\"}",
                IosClipboardJsonBuilder.BuildLoadItemJson(IosClipboardLoadRequest.Url, null));
            StringAssert.Contains(
                "\"request\":{\"kind\":\"image\"}",
                IosClipboardJsonBuilder.BuildLoadItemJson(IosClipboardLoadRequest.Image, null));
            StringAssert.Contains(
                "\"request\":{\"kind\":\"file\",\"utType\":\"public.png\"}",
                IosClipboardJsonBuilder.BuildLoadItemJson(IosClipboardLoadRequest.File("public.png"), null));
        }

        [Test]
        public void BuildStartObservingAndCheckForegroundChangeJson_AreScopeOnly()
        {
            Assert.AreEqual("{}", IosClipboardJsonBuilder.BuildStartObservingJson(null));
            Assert.AreEqual("{}", IosClipboardJsonBuilder.BuildCheckForegroundChangeJson(null));

            StringAssert.Contains(
                "\"kind\":\"general\"",
                IosClipboardJsonBuilder.BuildStartObservingJson(IosPasteboardScope.General));
        }

        // ── escaping ────────────────────────────────────────────────────────────

        [Test]
        public void Strings_ControlCharactersAndQuotes_AreEscaped()
        {
            string json = IosClipboardJsonBuilder.BuildCopyJson(
                IosClipboardContent.PlainText("a\"b\\c\nd\te\u0001f"), null, null);

            StringAssert.Contains("\\\"", json);
            StringAssert.Contains("\\\\", json);
            StringAssert.Contains("\\n", json);
            StringAssert.Contains("\\t", json);
            StringAssert.Contains("\\u0001", json);
        }

        [Test]
        public void Strings_JapaneseAndEmoji_SurviveARoundTripThroughTheReader()
        {
            const string value = "日本語 \U0001F680 テスト";

            string json = IosClipboardJsonBuilder.BuildCopyJson(
                IosClipboardContent.PlainText(value), null, null);

            JsonValue root = IosClipboardJsonReader.Parse(json)!;
            Assert.IsTrue(root.TryGetMember("content", out JsonValue content));
            Assert.IsTrue(content.GetMemberOrNull("text")!.TryGetString(out string decoded));
            Assert.AreEqual(value, decoded);
        }

        [Test]
        public void EveryBuilderOutput_IsParseableJson()
        {
            string[] outputs =
            {
                IosClipboardJsonBuilder.BuildCopyJson(IosClipboardContent.PlainText("x"), IosPasteboardScope.General, IosClipboardCopyOptions.Create(false, DateTime.UtcNow)),
                IosClipboardJsonBuilder.BuildAppendJson(IosClipboardContent.Url("https://a.example"), null),
                IosClipboardJsonBuilder.BuildReadJson(IosPasteboardScope.Named("n")),
                IosClipboardJsonBuilder.BuildReadDataJson("public.png", null),
                IosClipboardJsonBuilder.BuildGetSnapshotJson(null, new[] { "public.png" }),
                IosClipboardJsonBuilder.BuildClearJson(null),
                IosClipboardJsonBuilder.BuildCreatePasteboardJson(IosPasteboardCreationRequest.Unique),
                IosClipboardJsonBuilder.BuildRemovePasteboardJson(IosPasteboardScope.Unique("u")),
                IosClipboardJsonBuilder.BuildDetectPatternsJson(new[] { IosClipboardDetectionPattern.Link }, null),
                IosClipboardJsonBuilder.BuildDetectValuesJson(new[] { IosClipboardDetectionPattern.Number }, null),
                IosClipboardJsonBuilder.BuildLoadItemJson(IosClipboardLoadRequest.File("public.png"), null),
                IosClipboardJsonBuilder.BuildStartObservingJson(null),
                IosClipboardJsonBuilder.BuildCheckForegroundChangeJson(null)
            };

            Assert.AreEqual(13, outputs.Length, "one builder per bridge function that takes a requestJson");

            foreach (string json in outputs)
            {
                Assert.IsNotNull(IosClipboardJsonReader.Parse(json), $"not parseable: {json}");
            }
        }
    }
}
#endif
