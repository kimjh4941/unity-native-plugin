#nullable enable

using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using JonghyunKim.NativeToolkit.Runtime.Clipboard;

namespace JonghyunKim.NativeToolkit.Tests
{
    public sealed class AndroidClipboardJsonParserTests
    {
        // ---- ParseReadResult: HasContent ----

        [Test]
        public void ParseReadResult_ContentWithAllFields_MapsAllValues()
        {
            string json = "{\"label\":\"MyLabel\",\"mimeTypes\":[\"text/plain\",\"text/html\"],\"items\":[" +
                "{\"text\":\"hello\",\"htmlText\":\"<b>hi</b>\",\"uri\":\"content://x\",\"coercedText\":\"hello\"}," +
                "{\"text\":\"second\",\"htmlText\":\"\",\"uri\":\"\",\"coercedText\":\"second\"}]}";

            var result = AndroidClipboardJsonParser.ParseReadResult(json);

            Assert.AreEqual(ClipboardReadStatus.HasContent, result.Status);
            Assert.IsTrue(result.IsSuccess);
            Assert.IsNotNull(result.Contents);
            Assert.AreEqual("MyLabel", result.Contents!.Label);
            Assert.AreEqual(new[] { "text/plain", "text/html" }, result.Contents.MimeTypes);
            Assert.AreEqual(2, result.Contents.Items.Count);
            Assert.AreEqual("hello", result.Contents.Items[0].Text);
            Assert.AreEqual("<b>hi</b>", result.Contents.Items[0].HtmlText);
            Assert.AreEqual("content://x", result.Contents.Items[0].Uri);
            Assert.AreEqual("hello", result.Contents.Items[0].CoercedText);
            Assert.AreEqual("second", result.Contents.Items[1].Text);
        }

        [Test]
        public void ParseReadResult_MissingHtmlTextAndUri_NormalizedToNull()
        {
            string json = "{\"label\":null,\"mimeTypes\":[\"text/plain\"],\"items\":[{\"text\":\"hello\"}]}";

            var result = AndroidClipboardJsonParser.ParseReadResult(json);

            Assert.IsNull(result.Contents!.Items[0].HtmlText);
            Assert.IsNull(result.Contents.Items[0].Uri);
        }

        [Test]
        public void ParseReadResult_MissingItemsAndMimeTypes_NormalizedToEmptyList()
        {
            string json = "{\"label\":\"MyLabel\"}";

            var result = AndroidClipboardJsonParser.ParseReadResult(json);

            Assert.AreEqual(ClipboardReadStatus.HasContent, result.Status);
            Assert.AreEqual(0, result.Contents!.MimeTypes.Count);
            Assert.AreEqual(0, result.Contents.Items.Count);
        }

        // ---- ParseReadResult: Empty ----

        [Test]
        public void ParseReadResult_NullSentinel_ReturnsEmpty()
        {
            var result = AndroidClipboardJsonParser.ParseReadResult("null");

            Assert.AreEqual(ClipboardReadStatus.Empty, result.Status);
            Assert.IsTrue(result.IsSuccess);
        }

        // ---- ParseReadResult: Failed (error envelope, all 7 codes) ----

        [TestCase("CLIPBOARD_EMPTY_CONTENT", "Clipboard content is empty. Please provide text or HTML.")]
        [TestCase("CLIPBOARD_EMPTY_ITEMS", "No items provided for clipboard copy.")]
        [TestCase("CLIPBOARD_INVALID_URI", "Invalid URI: bad-uri")]
        [TestCase("CLIPBOARD_UNAVAILABLE", "Clipboard service is unavailable.")]
        [TestCase("CLIPBOARD_READ_NOT_ALLOWED", "Clipboard read is not allowed. The app must be in the foreground.")]
        [TestCase("CLIPBOARD_SECURITY", "Security restriction while accessing clipboard: denied")]
        [TestCase("CLIPBOARD_UNKNOWN", "Failed: unexpected")]
        public void ParseReadResult_ErrorEnvelope_MapsErrorCodeAndMessage(string code, string message)
        {
            string json = $"{{\"error\":\"{code}\",\"message\":\"{message}\"}}";

            var result = AndroidClipboardJsonParser.ParseReadResult(json);

            Assert.AreEqual(ClipboardReadStatus.Failed, result.Status);
            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(code, result.ErrorCode);
            Assert.AreEqual(message, result.ErrorMessage);
        }

        [Test]
        public void ParseReadResult_NullRaw_ReturnsFailedUnknown()
        {
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*ParseReadResult.*"));

            var result = AndroidClipboardJsonParser.ParseReadResult(null);

            Assert.AreEqual(ClipboardReadStatus.Failed, result.Status);
            Assert.AreEqual("CLIPBOARD_UNKNOWN", result.ErrorCode);
        }

        [Test]
        public void ParseReadResult_BlankRaw_ReturnsFailedUnknown()
        {
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*ParseReadResult.*"));

            var result = AndroidClipboardJsonParser.ParseReadResult("   ");

            Assert.AreEqual(ClipboardReadStatus.Failed, result.Status);
            Assert.AreEqual("CLIPBOARD_UNKNOWN", result.ErrorCode);
        }

        [Test]
        public void ParseReadResult_InvalidJson_ReturnsFailedUnknown()
        {
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*ParseReadResult.*"));

            var result = AndroidClipboardJsonParser.ParseReadResult("{not valid json");

            Assert.AreEqual(ClipboardReadStatus.Failed, result.Status);
            Assert.AreEqual("CLIPBOARD_UNKNOWN", result.ErrorCode);
        }

        // ---- ParseDescriptionResult: HasContent ----

        [Test]
        public void ParseDescriptionResult_AllFields_MapsAllValues()
        {
            string json = "{\"label\":\"MyLabel\",\"mimeTypes\":[\"text/plain\"],\"isStyledText\":true,\"classificationStatus\":3}";

            var result = AndroidClipboardJsonParser.ParseDescriptionResult(json);

            Assert.AreEqual(ClipboardReadStatus.HasContent, result.Status);
            Assert.IsNotNull(result.Description);
            Assert.AreEqual("MyLabel", result.Description!.Label);
            Assert.AreEqual(new[] { "text/plain" }, result.Description.MimeTypes);
            Assert.IsTrue(result.Description.IsStyledText);
            Assert.AreEqual(3, result.Description.ClassificationStatus);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ParseDescriptionResult_IsStyledText_MapsBothValues(bool isStyledText)
        {
            string json = $"{{\"isStyledText\":{(isStyledText ? "true" : "false")}}}";

            var result = AndroidClipboardJsonParser.ParseDescriptionResult(json);

            Assert.AreEqual(isStyledText, result.Description!.IsStyledText);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void ParseDescriptionResult_ClassificationStatus_MapsValue(int classificationStatus)
        {
            string json = $"{{\"classificationStatus\":{classificationStatus}}}";

            var result = AndroidClipboardJsonParser.ParseDescriptionResult(json);

            Assert.AreEqual(classificationStatus, result.Description!.ClassificationStatus);
        }

        [Test]
        public void ParseDescriptionResult_MissingClassificationStatus_NormalizedToNull()
        {
            string json = "{\"label\":\"MyLabel\"}";

            var result = AndroidClipboardJsonParser.ParseDescriptionResult(json);

            Assert.IsNull(result.Description!.ClassificationStatus);
        }

        [Test]
        public void ParseDescriptionResult_MissingMimeTypes_NormalizedToEmptyList()
        {
            string json = "{\"label\":\"MyLabel\"}";

            var result = AndroidClipboardJsonParser.ParseDescriptionResult(json);

            Assert.AreEqual(0, result.Description!.MimeTypes.Count);
        }

        [Test]
        public void ParseDescriptionResult_MissingLabel_NormalizedToNull()
        {
            string json = "{\"mimeTypes\":[\"text/plain\"]}";

            var result = AndroidClipboardJsonParser.ParseDescriptionResult(json);

            Assert.IsNull(result.Description!.Label);
        }

        // ---- ParseDescriptionResult: Empty ----

        [Test]
        public void ParseDescriptionResult_NullSentinel_ReturnsEmpty()
        {
            var result = AndroidClipboardJsonParser.ParseDescriptionResult("null");

            Assert.AreEqual(ClipboardReadStatus.Empty, result.Status);
            Assert.IsTrue(result.IsSuccess);
        }

        // ---- ParseDescriptionResult: Failed (error envelope, all 7 codes) ----

        [TestCase("CLIPBOARD_EMPTY_CONTENT", "Clipboard content is empty. Please provide text or HTML.")]
        [TestCase("CLIPBOARD_EMPTY_ITEMS", "No items provided for clipboard copy.")]
        [TestCase("CLIPBOARD_INVALID_URI", "Invalid URI: bad-uri")]
        [TestCase("CLIPBOARD_UNAVAILABLE", "Clipboard service is unavailable.")]
        [TestCase("CLIPBOARD_READ_NOT_ALLOWED", "Clipboard read is not allowed. The app must be in the foreground.")]
        [TestCase("CLIPBOARD_SECURITY", "Security restriction while accessing clipboard: denied")]
        [TestCase("CLIPBOARD_UNKNOWN", "Failed: unexpected")]
        public void ParseDescriptionResult_ErrorEnvelope_MapsErrorCodeAndMessage(string code, string message)
        {
            string json = $"{{\"error\":\"{code}\",\"message\":\"{message}\"}}";

            var result = AndroidClipboardJsonParser.ParseDescriptionResult(json);

            Assert.AreEqual(ClipboardReadStatus.Failed, result.Status);
            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(code, result.ErrorCode);
            Assert.AreEqual(message, result.ErrorMessage);
        }

        [Test]
        public void ParseDescriptionResult_NullRaw_ReturnsFailedUnknown()
        {
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*ParseDescriptionResult.*"));

            var result = AndroidClipboardJsonParser.ParseDescriptionResult(null);

            Assert.AreEqual(ClipboardReadStatus.Failed, result.Status);
            Assert.AreEqual("CLIPBOARD_UNKNOWN", result.ErrorCode);
        }

        [Test]
        public void ParseDescriptionResult_BlankRaw_ReturnsFailedUnknown()
        {
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*ParseDescriptionResult.*"));

            var result = AndroidClipboardJsonParser.ParseDescriptionResult("   ");

            Assert.AreEqual(ClipboardReadStatus.Failed, result.Status);
            Assert.AreEqual("CLIPBOARD_UNKNOWN", result.ErrorCode);
        }

        [Test]
        public void ParseDescriptionResult_InvalidJson_ReturnsFailedUnknown()
        {
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*ParseDescriptionResult.*"));

            var result = AndroidClipboardJsonParser.ParseDescriptionResult("{not valid json");

            Assert.AreEqual(ClipboardReadStatus.Failed, result.Status);
            Assert.AreEqual("CLIPBOARD_UNKNOWN", result.ErrorCode);
        }

        // ---- ParseHasClip ----

        [Test]
        public void ParseHasClip_True_ReturnsTrue()
        {
            Assert.IsTrue(AndroidClipboardJsonParser.ParseHasClip("true"));
        }

        [Test]
        public void ParseHasClip_False_ReturnsFalse()
        {
            Assert.IsFalse(AndroidClipboardJsonParser.ParseHasClip("false"));
        }

        [Test]
        public void ParseHasClip_InvalidValue_ReturnsFalse()
        {
            Assert.IsFalse(AndroidClipboardJsonParser.ParseHasClip("not-a-bool"));
        }

        [Test]
        public void ParseHasClip_Null_ReturnsFalse()
        {
            Assert.IsFalse(AndroidClipboardJsonParser.ParseHasClip(null));
        }

        [Test]
        public void ParseHasClip_Blank_ReturnsFalse()
        {
            Assert.IsFalse(AndroidClipboardJsonParser.ParseHasClip("   "));
        }
    }
}
