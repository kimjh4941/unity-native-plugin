#nullable enable

#if UNITY_STANDALONE_OSX || UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using NUnit.Framework;
using JonghyunKim.NativeToolkit.Runtime.Clipboard;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// EditMode tests for the macOS clipboard request builder. Covers the key-omission rules the
    /// native parser depends on, the base64 and escaping paths, and culture independence.
    /// </summary>
    public sealed class MacClipboardJsonBuilderTests
    {
        // ── scope ────────────────────────────────────────────────────────────

        [Test]
        public void BuildScopeJson_General_OmitsTheNameKey()
        {
            Assert.AreEqual("{\"kind\":\"general\"}",
                MacClipboardJsonBuilder.BuildScopeJson(MacPasteboardScope.General));
        }

        [Test]
        public void BuildScopeJson_NamedAndUnique_IncludeTheName()
        {
            Assert.AreEqual("{\"kind\":\"named\",\"name\":\"board\"}",
                MacClipboardJsonBuilder.BuildScopeJson(MacPasteboardScope.Named("board")));
            Assert.AreEqual("{\"kind\":\"unique\",\"name\":\"gen-1\"}",
                MacClipboardJsonBuilder.BuildScopeJson(MacPasteboardScope.Unique("gen-1")));
        }

        [Test]
        public void BuildScopeJson_NullScope_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => MacClipboardJsonBuilder.BuildScopeJson(null!));
        }

        // ── content ──────────────────────────────────────────────────────────

        [Test]
        public void BuildContentJson_SingleTextItem_IsBase64Encoded()
        {
            string json = MacClipboardJsonBuilder.BuildContentJson(MacClipboardContent.PlainText("hi"));
            // "hi" as UTF-8 is 0x68 0x69, which is "aGk=" in base64.
            Assert.AreEqual(
                "{\"items\":[{\"representations\":{\"public.utf8-plain-text\":\"aGk=\"}}]}", json);
        }

        [Test]
        public void BuildContentJson_MultipleRepresentations_AreSortedByOrdinal()
        {
            // A dictionary does not promise an enumeration order, so the builder sorts. Without it
            // this test would pass or fail depending on hashing.
            var item = MacClipboardContentItem.FromRepresentations(new Dictionary<string, byte[]>
            {
                ["public.utf8-plain-text"] = new byte[] { 0x61 },
                ["public.html"] = new byte[] { 0x62 },
            });
            string json = MacClipboardJsonBuilder.BuildContentJson(MacClipboardContent.Single(item));
            Assert.AreEqual(
                "{\"items\":[{\"representations\":{\"public.html\":\"Yg==\",\"public.utf8-plain-text\":\"YQ==\"}}]}",
                json);
        }

        [Test]
        public void BuildContentJson_MultipleItems_KeepTheirOrder()
        {
            var content = MacClipboardContent.Multiple(new[]
            {
                MacClipboardContentItem.Data("a", new byte[] { 0x61 }),
                MacClipboardContentItem.Data("b", new byte[] { 0x62 }),
            });
            Assert.AreEqual(
                "{\"items\":[{\"representations\":{\"a\":\"YQ==\"}},{\"representations\":{\"b\":\"Yg==\"}}]}",
                MacClipboardJsonBuilder.BuildContentJson(content));
        }

        [Test]
        public void BuildContentJson_EmptyRepresentations_ProducesAnEmptyObject()
        {
            // Left for the native layer to reject with EmptyRepresentations rather than guessing here.
            var item = MacClipboardContentItem.FromRepresentations(new Dictionary<string, byte[]>());
            Assert.AreEqual("{\"items\":[{\"representations\":{}}]}",
                MacClipboardJsonBuilder.BuildContentJson(MacClipboardContent.Single(item)));
        }

        [Test]
        public void BuildContentJson_NullContent_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => MacClipboardJsonBuilder.BuildContentJson(null!));
        }

        // ── options ──────────────────────────────────────────────────────────

        [Test]
        public void BuildOptionsJson_Null_ReturnsNullSoThePInvokePassesNoPointer()
        {
            Assert.IsNull(MacClipboardJsonBuilder.BuildOptionsJson(null));
        }

        [Test]
        public void BuildOptionsJson_BothValues_AreEmitted()
        {
            Assert.AreEqual("{\"localOnly\":true}",
                MacClipboardJsonBuilder.BuildOptionsJson(MacClipboardCopyOptions.PrivacyPreservingDefault));
            Assert.AreEqual("{\"localOnly\":false}",
                MacClipboardJsonBuilder.BuildOptionsJson(MacClipboardCopyOptions.Create(false)));
        }

        // ── ownership ────────────────────────────────────────────────────────

        [Test]
        public void BuildOwnershipJson_NestsTheScopeAndKeepsA64BitCount()
        {
            var ownership = new MacPasteboardOwnership(MacPasteboardScope.General, 4294967296L);
            Assert.AreEqual("{\"scope\":{\"kind\":\"general\"},\"changeCount\":4294967296}",
                MacClipboardJsonBuilder.BuildOwnershipJson(ownership));
        }

        [Test]
        public void BuildOwnershipJson_NullOwnership_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => MacClipboardJsonBuilder.BuildOwnershipJson(null!));
        }

        // ── create request ───────────────────────────────────────────────────

        [Test]
        public void BuildCreateRequestJson_Unique_OmitsTheNameKey()
        {
            Assert.AreEqual("{\"kind\":\"unique\"}",
                MacClipboardJsonBuilder.BuildCreateRequestJson(MacPasteboardCreationRequest.Unique));
        }

        [Test]
        public void BuildCreateRequestJson_Named_IncludesTheName()
        {
            Assert.AreEqual("{\"kind\":\"named\",\"name\":\"board\"}",
                MacClipboardJsonBuilder.BuildCreateRequestJson(MacPasteboardCreationRequest.Named("board")));
        }

        // ── matching types ───────────────────────────────────────────────────

        [Test]
        public void BuildMatchingTypesJson_Null_ReturnsNullMeaningNoFilter()
        {
            Assert.IsNull(MacClipboardJsonBuilder.BuildMatchingTypesJson(null));
        }

        [Test]
        public void BuildMatchingTypesJson_EmptyList_ProducesAnEmptyArray()
        {
            // Distinct from null: the native layer rejects [] with EmptyTypeFilter, which is the
            // behaviour a caller passing an empty filter should see.
            Assert.AreEqual("[]", MacClipboardJsonBuilder.BuildMatchingTypesJson(Array.Empty<string>()));
        }

        [Test]
        public void BuildMatchingTypesJson_KeepsTheGivenOrder()
        {
            Assert.AreEqual("[\"b\",\"a\"]",
                MacClipboardJsonBuilder.BuildMatchingTypesJson(new[] { "b", "a" }));
        }

        // ── patterns ─────────────────────────────────────────────────────────

        [Test]
        public void BuildPatternsJson_UsesTheMacOsRawValuesWhichArePlural()
        {
            // The iOS raw values are singular. Copying them would produce a request the native
            // layer rejects outright, so this is asserted literally.
            string json = MacClipboardJsonBuilder.BuildPatternsJson(new[]
            {
                MacClipboardDetectionPattern.PhoneNumbers,
                MacClipboardDetectionPattern.EmailAddresses,
                MacClipboardDetectionPattern.ProbableWebUrl,
            });
            Assert.AreEqual("[\"emailAddresses\",\"phoneNumbers\",\"probableWebURL\"]", json);
        }

        [Test]
        public void BuildPatternsJson_IsSortedSoTheSameSetGivesTheSameString()
        {
            string a = MacClipboardJsonBuilder.BuildPatternsJson(new[]
            {
                MacClipboardDetectionPattern.Number, MacClipboardDetectionPattern.Links,
            });
            string b = MacClipboardJsonBuilder.BuildPatternsJson(new[]
            {
                MacClipboardDetectionPattern.Links, MacClipboardDetectionPattern.Number,
            });
            Assert.AreEqual(a, b);
            Assert.AreEqual("[\"links\",\"number\"]", a);
        }

        [Test]
        public void BuildPatternsJson_EveryPatternHasARawValue()
        {
            foreach (MacClipboardDetectionPattern pattern in
                     (MacClipboardDetectionPattern[])Enum.GetValues(typeof(MacClipboardDetectionPattern)))
            {
                string json = MacClipboardJsonBuilder.BuildPatternsJson(new[] { pattern });
                Assert.IsTrue(json.StartsWith("[\"", StringComparison.Ordinal), pattern.ToString());
                Assert.AreNotEqual("[\"\"]", json, pattern.ToString());
            }
        }

        [Test]
        public void BuildPatternsJson_Empty_ProducesAnEmptyArray()
        {
            Assert.AreEqual("[]",
                MacClipboardJsonBuilder.BuildPatternsJson(Array.Empty<MacClipboardDetectionPattern>()));
        }

        // ── escaping ─────────────────────────────────────────────────────────

        [Test]
        public void AppendString_EscapesQuotesBackslashesAndControlCharacters()
        {
            string utType = "a\"b\\c\nd\te\u0001f";
            var item = MacClipboardContentItem.Data(utType, new byte[] { 0x61 });
            string json = MacClipboardJsonBuilder.BuildContentJson(MacClipboardContent.Single(item));
            StringAssert.Contains("\"a\\\"b\\\\c\\nd\\te\\u0001f\"", json);
        }

        [Test]
        public void AppendString_NonAsciiIsWrittenThroughRatherThanEscaped()
        {
            var item = MacClipboardContentItem.Data("日本語🙂", new byte[] { 0x61 });
            string json = MacClipboardJsonBuilder.BuildContentJson(MacClipboardContent.Single(item));
            StringAssert.Contains("\"日本語🙂\"", json);
            Assert.IsFalse(json.Contains("\\u65e5"), "non-ASCII must not be escaped");
        }

        // ── culture ──────────────────────────────────────────────────────────

        [Test]
        public void BuildOwnershipJson_IsCultureIndependent()
        {
            // A culture that uses "," as the decimal separator would break a naive ToString().
            CultureInfo original = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
                var ownership = new MacPasteboardOwnership(MacPasteboardScope.General, 1234567);
                StringAssert.Contains("\"changeCount\":1234567",
                    MacClipboardJsonBuilder.BuildOwnershipJson(ownership));
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = original;
            }
        }
    }
}
#endif
