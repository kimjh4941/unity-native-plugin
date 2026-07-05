#nullable enable

using NUnit.Framework;
using JonghyunKim.NativeToolkit.Runtime.Share;

namespace JonghyunKim.NativeToolkit.Tests
{
    public sealed class IosShareJsonBuilderTests
    {
        [Test]
        public void BuildShareContentJson_TextItem_ExactJson()
        {
            var payload = new IosShareContentPayload { items = new[] { IosShareItem.Text("hello") } };

            string json = IosShareJsonBuilder.BuildShareContentJson(payload);

            Assert.AreEqual("{\"items\":[{\"type\":\"text\",\"value\":\"hello\"}]}", json);
        }

        [Test]
        public void BuildShareContentJson_UrlItem_ExactJson()
        {
            var payload = new IosShareContentPayload { items = new[] { IosShareItem.Url("https://example.com") } };

            string json = IosShareJsonBuilder.BuildShareContentJson(payload);

            Assert.AreEqual("{\"items\":[{\"type\":\"url\",\"value\":\"https://example.com\"}]}", json);
        }

        [Test]
        public void BuildShareContentJson_ImageItem_ExactJson()
        {
            var payload = new IosShareContentPayload { items = new[] { IosShareItem.Image("/tmp/a.png") } };

            string json = IosShareJsonBuilder.BuildShareContentJson(payload);

            Assert.AreEqual("{\"items\":[{\"type\":\"image\",\"value\":\"/tmp/a.png\"}]}", json);
        }

        [Test]
        public void BuildShareContentJson_FileItem_ExactJson()
        {
            var payload = new IosShareContentPayload { items = new[] { IosShareItem.File("/tmp/a.pdf") } };

            string json = IosShareJsonBuilder.BuildShareContentJson(payload);

            Assert.AreEqual("{\"items\":[{\"type\":\"file\",\"value\":\"/tmp/a.pdf\"}]}", json);
        }

        [Test]
        public void BuildShareContentJson_MultipleItems_PreservesOrder()
        {
            var payload = new IosShareContentPayload
            {
                items = new[] { IosShareItem.Text("first"), IosShareItem.Text("second") }
            };

            string json = IosShareJsonBuilder.BuildShareContentJson(payload);

            int firstPos = json.IndexOf("first", System.StringComparison.Ordinal);
            int secondPos = json.IndexOf("second", System.StringComparison.Ordinal);
            Assert.Less(firstPos, secondPos);
        }

        [Test]
        public void BuildShareContentJson_EmptyItems_ProducesEmptyArray()
        {
            var payload = new IosShareContentPayload { items = new IosShareItem[0] };

            string json = IosShareJsonBuilder.BuildShareContentJson(payload);

            Assert.AreEqual("{\"items\":[]}", json);
        }

        [Test]
        public void BuildShareContentJson_NullItemInArray_IsExcluded()
        {
            var payload = new IosShareContentPayload
            {
                items = new[] { IosShareItem.Text("kept"), null!, IosShareItem.Text("also-kept") }
            };

            string json = IosShareJsonBuilder.BuildShareContentJson(payload);

            Assert.AreEqual(
                "{\"items\":[{\"type\":\"text\",\"value\":\"kept\"},{\"type\":\"text\",\"value\":\"also-kept\"}]}",
                json);
        }

        [Test]
        public void BuildShareContentJson_EmptyValue_PassesThroughAsIs()
        {
            var payload = new IosShareContentPayload { items = new[] { IosShareItem.Text(string.Empty) } };

            string json = IosShareJsonBuilder.BuildShareContentJson(payload);

            Assert.AreEqual("{\"items\":[{\"type\":\"text\",\"value\":\"\"}]}", json);
        }

        [Test]
        public void BuildShareContentJson_UnknownType_PassesThroughAsIs()
        {
            var payload = new IosShareContentPayload
            {
                items = new[] { new IosShareItem { type = "unknown", value = "v" } }
            };

            string json = IosShareJsonBuilder.BuildShareContentJson(payload);

            StringAssert.Contains("\"type\":\"unknown\"", json);
        }

        [Test]
        public void BuildShareContentJson_WithSubjectAndPreviewTitle_IncludesBoth()
        {
            var payload = new IosShareContentPayload
            {
                items = new[] { IosShareItem.Text("hello") },
                subject = "My Subject",
                previewTitle = "Preview"
            };

            string json = IosShareJsonBuilder.BuildShareContentJson(payload);

            StringAssert.Contains("\"subject\":\"My Subject\"", json);
            StringAssert.Contains("\"previewTitle\":\"Preview\"", json);
        }

        [Test]
        public void BuildShareContentJson_NullOrWhitespaceSubjectAndPreviewTitle_OmitsBoth()
        {
            var payload = new IosShareContentPayload
            {
                items = new[] { IosShareItem.Text("hello") },
                subject = "   ",
                previewTitle = null
            };

            string json = IosShareJsonBuilder.BuildShareContentJson(payload);

            StringAssert.DoesNotContain("\"subject\"", json);
            StringAssert.DoesNotContain("\"previewTitle\"", json);
        }

        [Test]
        public void BuildShareContentJson_WithExcludedActivityTypes_IncludesArray()
        {
            var payload = new IosShareContentPayload
            {
                items = new[] { IosShareItem.Text("hello") },
                excludedActivityTypes = new[] { "com.apple.UIKit.activity.PostToFacebook" }
            };

            string json = IosShareJsonBuilder.BuildShareContentJson(payload);

            Assert.AreEqual(
                "{\"items\":[{\"type\":\"text\",\"value\":\"hello\"}],\"excludedActivityTypes\":[\"com.apple.UIKit.activity.PostToFacebook\"]}",
                json);
        }

        [Test]
        public void BuildShareContentJson_NullExcludedActivityTypes_OmitsField()
        {
            var payload = new IosShareContentPayload
            {
                items = new[] { IosShareItem.Text("hello") },
                excludedActivityTypes = null
            };

            string json = IosShareJsonBuilder.BuildShareContentJson(payload);

            StringAssert.DoesNotContain("\"excludedActivityTypes\"", json);
        }

        [Test]
        public void BuildShareContentJson_EmptyExcludedActivityTypes_OmitsField()
        {
            var payload = new IosShareContentPayload
            {
                items = new[] { IosShareItem.Text("hello") },
                excludedActivityTypes = new string[0]
            };

            string json = IosShareJsonBuilder.BuildShareContentJson(payload);

            StringAssert.DoesNotContain("\"excludedActivityTypes\"", json);
        }

        [Test]
        public void BuildShareContentJson_EscapesSpecialCharacters()
        {
            var payload = new IosShareContentPayload
            {
                items = new[] { IosShareItem.Text("line1\nline2\ttab\"quote\\back") }
            };

            string json = IosShareJsonBuilder.BuildShareContentJson(payload);

            StringAssert.Contains("\\n", json);
            StringAssert.Contains("\\t", json);
            StringAssert.Contains("\\\"", json);
            StringAssert.Contains("\\\\", json);
        }

        [Test]
        public void BuildShareContentJson_EscapesControlCharacters()
        {
            var payload = new IosShareContentPayload
            {
                items = new[] { IosShareItem.Text("") }
            };

            string json = IosShareJsonBuilder.BuildShareContentJson(payload);

            StringAssert.Contains("\\u0001", json);
            StringAssert.Contains("\\u001f", json);
        }
    }
}
