#nullable enable

using NUnit.Framework;
using JonghyunKim.NativeToolkit.Runtime.Share;

namespace JonghyunKim.NativeToolkit.Tests
{
    public sealed class MacShareJsonBuilderTests
    {
        [Test]
        public void BuildShareContentJson_TextItem_ExactJson()
        {
            var payload = new MacShareContentPayload { items = new[] { MacShareItem.Text("hello") } };

            string json = MacShareJsonBuilder.BuildShareContentJson(payload);

            Assert.AreEqual("{\"items\":[{\"type\":\"text\",\"value\":\"hello\"}]}", json);
        }

        [Test]
        public void BuildShareContentJson_UrlItem_ExactJson()
        {
            var payload = new MacShareContentPayload { items = new[] { MacShareItem.Url("https://example.com") } };

            string json = MacShareJsonBuilder.BuildShareContentJson(payload);

            Assert.AreEqual("{\"items\":[{\"type\":\"url\",\"value\":\"https://example.com\"}]}", json);
        }

        [Test]
        public void BuildShareContentJson_ImageItem_ExactJson()
        {
            var payload = new MacShareContentPayload { items = new[] { MacShareItem.Image("/tmp/a.png") } };

            string json = MacShareJsonBuilder.BuildShareContentJson(payload);

            Assert.AreEqual("{\"items\":[{\"type\":\"image\",\"value\":\"/tmp/a.png\"}]}", json);
        }

        [Test]
        public void BuildShareContentJson_FileItem_ExactJson()
        {
            var payload = new MacShareContentPayload { items = new[] { MacShareItem.File("/tmp/a.pdf") } };

            string json = MacShareJsonBuilder.BuildShareContentJson(payload);

            Assert.AreEqual("{\"items\":[{\"type\":\"file\",\"value\":\"/tmp/a.pdf\"}]}", json);
        }

        [Test]
        public void BuildShareContentJson_MultipleItems_PreservesOrder()
        {
            var payload = new MacShareContentPayload
            {
                items = new[] { MacShareItem.Text("first"), MacShareItem.Text("second") }
            };

            string json = MacShareJsonBuilder.BuildShareContentJson(payload);

            int firstPos = json.IndexOf("first", System.StringComparison.Ordinal);
            int secondPos = json.IndexOf("second", System.StringComparison.Ordinal);
            Assert.Greater(secondPos, firstPos);
        }

        [Test]
        public void BuildShareContentJson_NullItemEntry_IsExcluded()
        {
            var payload = new MacShareContentPayload
            {
                items = new[] { MacShareItem.Text("kept"), null! }
            };

            string json = MacShareJsonBuilder.BuildShareContentJson(payload);

            Assert.AreEqual("{\"items\":[{\"type\":\"text\",\"value\":\"kept\"}]}", json);
        }

        [Test]
        public void BuildShareContentJson_Recipients_Included()
        {
            var payload = new MacShareContentPayload
            {
                items = new[] { MacShareItem.Text("hello") },
                recipients = new[] { "a@example.com", "b@example.com" }
            };

            string json = MacShareJsonBuilder.BuildShareContentJson(payload);

            Assert.AreEqual(
                "{\"items\":[{\"type\":\"text\",\"value\":\"hello\"}],\"recipients\":[\"a@example.com\",\"b@example.com\"]}",
                json);
        }

        [Test]
        public void BuildShareContentJson_RecipientsNull_Omitted()
        {
            var payload = new MacShareContentPayload
            {
                items = new[] { MacShareItem.Text("hello") },
                recipients = null
            };

            string json = MacShareJsonBuilder.BuildShareContentJson(payload);

            Assert.IsFalse(json.Contains("recipients"));
        }

        [Test]
        public void BuildShareContentJson_RecipientsEmpty_Omitted()
        {
            var payload = new MacShareContentPayload
            {
                items = new[] { MacShareItem.Text("hello") },
                recipients = new string[0]
            };

            string json = MacShareJsonBuilder.BuildShareContentJson(payload);

            Assert.IsFalse(json.Contains("recipients"));
        }

        [Test]
        public void BuildShareContentJson_Subject_Included()
        {
            var payload = new MacShareContentPayload
            {
                items = new[] { MacShareItem.Text("hello") },
                subject = "My Subject"
            };

            string json = MacShareJsonBuilder.BuildShareContentJson(payload);

            Assert.AreEqual(
                "{\"items\":[{\"type\":\"text\",\"value\":\"hello\"}],\"subject\":\"My Subject\"}",
                json);
        }

        [Test]
        public void BuildShareContentJson_SubjectNullOrWhitespace_Omitted()
        {
            var payload = new MacShareContentPayload
            {
                items = new[] { MacShareItem.Text("hello") },
                subject = "   "
            };

            string json = MacShareJsonBuilder.BuildShareContentJson(payload);

            Assert.IsFalse(json.Contains("subject"));
        }

        [Test]
        public void BuildShareContentJson_ExcludedServiceTitles_Included()
        {
            var payload = new MacShareContentPayload
            {
                items = new[] { MacShareItem.Text("hello") },
                excludedServiceTitles = new[] { "Twitter", "Facebook" }
            };

            string json = MacShareJsonBuilder.BuildShareContentJson(payload);

            Assert.AreEqual(
                "{\"items\":[{\"type\":\"text\",\"value\":\"hello\"}],\"excludedServiceTitles\":[\"Twitter\",\"Facebook\"]}",
                json);
        }

        [Test]
        public void BuildShareContentJson_ExcludedServiceTitlesEmpty_Omitted()
        {
            var payload = new MacShareContentPayload
            {
                items = new[] { MacShareItem.Text("hello") },
                excludedServiceTitles = new string[0]
            };

            string json = MacShareJsonBuilder.BuildShareContentJson(payload);

            Assert.IsFalse(json.Contains("excludedServiceTitles"));
        }

        [Test]
        public void BuildShareContentJson_AllOptionalFields_Included()
        {
            var payload = new MacShareContentPayload
            {
                items = new[] { MacShareItem.Url("https://example.com") },
                recipients = new[] { "a@example.com" },
                subject = "Subject",
                excludedServiceTitles = new[] { "Twitter" }
            };

            string json = MacShareJsonBuilder.BuildShareContentJson(payload);

            Assert.AreEqual(
                "{\"items\":[{\"type\":\"url\",\"value\":\"https://example.com\"}]," +
                "\"recipients\":[\"a@example.com\"]," +
                "\"subject\":\"Subject\"," +
                "\"excludedServiceTitles\":[\"Twitter\"]}",
                json);
        }

        [Test]
        public void BuildShareContentJson_SpecialCharacters_Escaped()
        {
            var payload = new MacShareContentPayload
            {
                items = new[] { MacShareItem.Text("line1\nline2\t\"quoted\"") }
            };

            string json = MacShareJsonBuilder.BuildShareContentJson(payload);

            Assert.AreEqual("{\"items\":[{\"type\":\"text\",\"value\":\"line1\\nline2\\t\\\"quoted\\\"\"}]}", json);
        }
    }
}
