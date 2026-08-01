#nullable enable

using NUnit.Framework;
using JonghyunKim.NativeToolkit.Runtime.Clipboard;

namespace JonghyunKim.NativeToolkit.Tests
{
    public sealed class AndroidClipboardJsonBuilderTests
    {
        // ---- copyPlainText ----

        [Test]
        public void BuildCopyPlainTextJson_RequiredOnly_ExactJson()
        {
            string json = AndroidClipboardJsonBuilder.BuildCopyPlainTextJson(new CopyPlainTextPayload { text = "hello" });

            Assert.AreEqual("{\"text\":\"hello\",\"isSensitive\":false}", json);
        }

        [Test]
        public void BuildCopyPlainTextJson_BlankText_StillEmitsTextKey()
        {
            string json = AndroidClipboardJsonBuilder.BuildCopyPlainTextJson(new CopyPlainTextPayload { text = "" });

            StringAssert.Contains("\"text\":\"\"", json);
        }

        [Test]
        public void BuildCopyPlainTextJson_WithLabelAndSensitive_IncludesBoth()
        {
            var payload = new CopyPlainTextPayload { text = "hello", label = "My Label", isSensitive = true };

            string json = AndroidClipboardJsonBuilder.BuildCopyPlainTextJson(payload);

            StringAssert.Contains("\"text\":\"hello\"", json);
            StringAssert.Contains("\"label\":\"My Label\"", json);
            StringAssert.Contains("\"isSensitive\":true", json);
        }

        [Test]
        public void BuildCopyPlainTextJson_NullLabel_OmitsLabelKey()
        {
            string json = AndroidClipboardJsonBuilder.BuildCopyPlainTextJson(new CopyPlainTextPayload { text = "hello", label = null });

            StringAssert.DoesNotContain("\"label\"", json);
        }

        [Test]
        public void BuildCopyPlainTextJson_WhitespaceLabel_OmitsLabelKey()
        {
            string json = AndroidClipboardJsonBuilder.BuildCopyPlainTextJson(new CopyPlainTextPayload { text = "hello", label = "   " });

            StringAssert.DoesNotContain("\"label\"", json);
        }

        // ---- copyHtmlText ----

        [Test]
        public void BuildCopyHtmlTextJson_RequiredOnly_ExactJson()
        {
            var payload = new CopyHtmlTextPayload { plainText = "hi", htmlText = "<b>hi</b>" };

            string json = AndroidClipboardJsonBuilder.BuildCopyHtmlTextJson(payload);

            Assert.AreEqual("{\"plainText\":\"hi\",\"htmlText\":\"<b>hi</b>\",\"isSensitive\":false}", json);
        }

        [Test]
        public void BuildCopyHtmlTextJson_BlankPlainText_StillEmitsPlainTextKey()
        {
            var payload = new CopyHtmlTextPayload { plainText = "", htmlText = "<b>hi</b>" };

            string json = AndroidClipboardJsonBuilder.BuildCopyHtmlTextJson(payload);

            StringAssert.Contains("\"plainText\":\"\"", json);
        }

        [Test]
        public void BuildCopyHtmlTextJson_WithLabelAndSensitive_IncludesBoth()
        {
            var payload = new CopyHtmlTextPayload
            {
                plainText = "hi",
                htmlText = "<b>hi</b>",
                label = "My Label",
                isSensitive = true
            };

            string json = AndroidClipboardJsonBuilder.BuildCopyHtmlTextJson(payload);

            StringAssert.Contains("\"label\":\"My Label\"", json);
            StringAssert.Contains("\"isSensitive\":true", json);
        }

        // ---- copyUri ----

        [Test]
        public void BuildCopyUriJson_RequiredOnly_ExactJson()
        {
            string json = AndroidClipboardJsonBuilder.BuildCopyUriJson(new CopyUriPayload { uri = "content://media/1" });

            Assert.AreEqual("{\"uri\":\"content://media/1\",\"isSensitive\":false}", json);
        }

        [Test]
        public void BuildCopyUriJson_WithLabelAndSensitive_IncludesBoth()
        {
            var payload = new CopyUriPayload { uri = "content://media/1", label = "My Label", isSensitive = true };

            string json = AndroidClipboardJsonBuilder.BuildCopyUriJson(payload);

            StringAssert.Contains("\"label\":\"My Label\"", json);
            StringAssert.Contains("\"isSensitive\":true", json);
        }

        // ---- copyMultipleText ----

        [Test]
        public void BuildCopyMultipleTextJson_EmptyArray_ExactJson()
        {
            string json = AndroidClipboardJsonBuilder.BuildCopyMultipleTextJson(new CopyMultipleTextPayload { texts = new string[0] });

            Assert.AreEqual("{\"texts\":[],\"isSensitive\":false}", json);
        }

        [Test]
        public void BuildCopyMultipleTextJson_SingleItem_ExactJson()
        {
            string json = AndroidClipboardJsonBuilder.BuildCopyMultipleTextJson(new CopyMultipleTextPayload { texts = new[] { "a" } });

            Assert.AreEqual("{\"texts\":[\"a\"],\"isSensitive\":false}", json);
        }

        [Test]
        public void BuildCopyMultipleTextJson_MultipleItems_PreservesOrder()
        {
            string json = AndroidClipboardJsonBuilder.BuildCopyMultipleTextJson(new CopyMultipleTextPayload { texts = new[] { "a", "", "b" } });

            Assert.AreEqual("{\"texts\":[\"a\",\"\",\"b\"],\"isSensitive\":false}", json);
        }

        [Test]
        public void BuildCopyMultipleTextJson_WithLabelAndSensitive_IncludesBoth()
        {
            var payload = new CopyMultipleTextPayload { texts = new[] { "a" }, label = "My Label", isSensitive = true };

            string json = AndroidClipboardJsonBuilder.BuildCopyMultipleTextJson(payload);

            StringAssert.Contains("\"label\":\"My Label\"", json);
            StringAssert.Contains("\"isSensitive\":true", json);
        }

        // ---- escaping ----

        [Test]
        public void BuildCopyPlainTextJson_SpecialCharacters_EscapesCorrectly()
        {
            string json = AndroidClipboardJsonBuilder.BuildCopyPlainTextJson(new CopyPlainTextPayload { text = "quote\"back\\slash\nnewline\ttab" });

            StringAssert.Contains("\\\"", json);
            StringAssert.Contains("\\\\", json);
            StringAssert.Contains("\\n", json);
            StringAssert.Contains("\\t", json);
        }

        [Test]
        public void BuildCopyPlainTextJson_UnicodeCharacters_PreservedAsIs()
        {
            string json = AndroidClipboardJsonBuilder.BuildCopyPlainTextJson(new CopyPlainTextPayload { text = "こんにちは" });

            StringAssert.Contains("こんにちは", json);
        }
    }
}
