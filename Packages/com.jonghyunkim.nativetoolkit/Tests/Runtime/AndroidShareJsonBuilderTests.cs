#nullable enable

using System;
using NUnit.Framework;
using JonghyunKim.NativeToolkit.Runtime.Share;

namespace JonghyunKim.NativeToolkit.Tests
{
    public sealed class AndroidShareJsonBuilderTests
    {
        // ---- ShareText ----

        [Test]
        public void BuildShareTextJson_RequiredOnly_ExactJson()
        {
            string json = AndroidShareJsonBuilder.BuildShareTextJson(new ShareTextPayload { text = "hello" });

            Assert.AreEqual("{\"text\":\"hello\"}", json);
        }

        [Test]
        public void BuildShareTextJson_AllOptionals_ProducesFullJson()
        {
            var payload = new ShareTextPayload
            {
                text = "Share me",
                title = "My Title",
                subject = "My Subject",
                mimeType = "text/plain",
                previewTitle = "Preview",
                previewThumbnailPath = "/path/thumb.png",
                chooserActions = new[]
                {
                    new ChooserActionPayload
                    {
                        label = "Copy",
                        iconBase64 = "abc123",
                        intentAction = "android.intent.action.SEND"
                    }
                }
            };

            string json = AndroidShareJsonBuilder.BuildShareTextJson(payload);

            StringAssert.Contains("\"text\":\"Share me\"", json);
            StringAssert.Contains("\"title\":\"My Title\"", json);
            StringAssert.Contains("\"subject\":\"My Subject\"", json);
            StringAssert.Contains("\"mimeType\":\"text/plain\"", json);
            StringAssert.Contains("\"previewTitle\":\"Preview\"", json);
            StringAssert.Contains("\"previewThumbnailPath\":\"/path/thumb.png\"", json);
            StringAssert.Contains("\"chooserActions\":", json);
            StringAssert.Contains("\"label\":\"Copy\"", json);
            StringAssert.Contains("\"iconBase64\":\"abc123\"", json);
            StringAssert.Contains("\"intentAction\":\"android.intent.action.SEND\"", json);
        }

        [Test]
        public void BuildShareTextJson_ChooserActionWithoutIntentAction_OmitsIntentAction()
        {
            var payload = new ShareTextPayload
            {
                text = "hello",
                chooserActions = new[] { new ChooserActionPayload { label = "L", iconBase64 = "B" } }
            };

            string json = AndroidShareJsonBuilder.BuildShareTextJson(payload);

            StringAssert.Contains("\"chooserActions\":", json);
            StringAssert.DoesNotContain("\"intentAction\"", json);
        }

        [Test]
        public void BuildShareTextJson_NullChooserActions_OmitsChooserActions()
        {
            var payload = new ShareTextPayload { text = "hello", chooserActions = null };

            string json = AndroidShareJsonBuilder.BuildShareTextJson(payload);

            StringAssert.DoesNotContain("\"chooserActions\"", json);
        }

        [Test]
        public void BuildShareTextJson_EmptyChooserActions_OmitsChooserActions()
        {
            var payload = new ShareTextPayload { text = "hello", chooserActions = new ChooserActionPayload[0] };

            string json = AndroidShareJsonBuilder.BuildShareTextJson(payload);

            StringAssert.DoesNotContain("\"chooserActions\"", json);
        }

        // (9) intentAction present → included in JSON (callback contract root)
        [Test]
        public void BuildShareTextJson_ChooserActionWithIntentAction_IncludesIntentAction()
        {
            var payload = new ShareTextPayload
            {
                text = "hello",
                chooserActions = new[]
                {
                    new ChooserActionPayload
                    {
                        label = "Save",
                        iconBase64 = "B64",
                        intentAction = "com.example.action.SAVE"
                    }
                }
            };

            string json = AndroidShareJsonBuilder.BuildShareTextJson(payload);

            StringAssert.Contains("\"intentAction\":\"com.example.action.SAVE\"", json);
        }

        // (11) Multiple chooserActions preserve array order
        [Test]
        public void BuildShareTextJson_MultipleChooserActions_PreservesOrder()
        {
            var payload = new ShareTextPayload
            {
                text = "hello",
                chooserActions = new[]
                {
                    new ChooserActionPayload { label = "First",  iconBase64 = "B1", intentAction = "com.example.action.FIRST"  },
                    new ChooserActionPayload { label = "Second", iconBase64 = "B2", intentAction = "com.example.action.SECOND" },
                    new ChooserActionPayload { label = "Third",  iconBase64 = "B3", intentAction = "com.example.action.THIRD"  }
                }
            };

            string json = AndroidShareJsonBuilder.BuildShareTextJson(payload);

            int firstPos  = json.IndexOf("FIRST",  StringComparison.Ordinal);
            int secondPos = json.IndexOf("SECOND", StringComparison.Ordinal);
            int thirdPos  = json.IndexOf("THIRD",  StringComparison.Ordinal);
            Assert.Less(firstPos,  secondPos, "FIRST must appear before SECOND");
            Assert.Less(secondPos, thirdPos,  "SECOND must appear before THIRD");
        }

        [Test]
        public void BuildShareTextJson_WhitespaceOptionals_OmitsOptionals()
        {
            var payload = new ShareTextPayload
            {
                text = "hello",
                title = "   ",
                subject = "",
                mimeType = "\t"
            };

            string json = AndroidShareJsonBuilder.BuildShareTextJson(payload);

            Assert.AreEqual("{\"text\":\"hello\"}", json);
        }

        [Test]
        public void BuildShareTextJson_EscapesSpecialCharacters()
        {
            string json = AndroidShareJsonBuilder.BuildShareTextJson(
                new ShareTextPayload { text = "line1\nline2\ttab\"quote\\back" });

            StringAssert.Contains("\\n", json);
            StringAssert.Contains("\\t", json);
            StringAssert.Contains("\\\"", json);
            StringAssert.Contains("\\\\", json);
        }

        [Test]
        public void BuildShareTextJson_EscapesControlCharacters()
        {
            string json = AndroidShareJsonBuilder.BuildShareTextJson(
                new ShareTextPayload { text = "" });

            StringAssert.Contains("\\u0001", json);
            StringAssert.Contains("\\u001f", json);
        }

        // ---- ShareImage ----

        [Test]
        public void BuildShareImageJson_RequiredOnly_ExactJson()
        {
            string json = AndroidShareJsonBuilder.BuildShareImageJson(
                new ShareImagePayload { filePath = "/sdcard/img.png" });

            Assert.AreEqual("{\"filePath\":\"/sdcard/img.png\"}", json);
        }

        [Test]
        public void BuildShareImageJson_WithMimeType_IncludesMimeType()
        {
            string json = AndroidShareJsonBuilder.BuildShareImageJson(
                new ShareImagePayload { filePath = "/sdcard/img.png", mimeType = "image/png" });

            StringAssert.Contains("\"mimeType\":\"image/png\"", json);
        }

        // ---- ShareImages ----

        [Test]
        public void BuildShareImagesJson_MultipleFiles_ExactJson()
        {
            string json = AndroidShareJsonBuilder.BuildShareImagesJson(
                new ShareImagesPayload { filePaths = new[] { "/a.png", "/b.png" } });

            Assert.AreEqual("{\"filePaths\":[\"/a.png\",\"/b.png\"]}", json);
        }

        [Test]
        public void BuildShareImagesJson_EmptyArray_ProducesEmptyFilePaths()
        {
            string json = AndroidShareJsonBuilder.BuildShareImagesJson(
                new ShareImagesPayload { filePaths = new string[0] });

            Assert.AreEqual("{\"filePaths\":[]}", json);
        }

        // ---- ShareFile ----

        [Test]
        public void BuildShareFileJson_ExactJson()
        {
            string json = AndroidShareJsonBuilder.BuildShareFileJson(
                new ShareFilePayload { filePath = "/sdcard/doc.pdf" });

            Assert.AreEqual("{\"filePath\":\"/sdcard/doc.pdf\"}", json);
        }

        // ---- ShareFiles ----

        [Test]
        public void BuildShareFilesJson_MultipleFiles_ExactJson()
        {
            string json = AndroidShareJsonBuilder.BuildShareFilesJson(
                new ShareFilesPayload { filePaths = new[] { "/a.pdf", "/b.pdf" } });

            Assert.AreEqual("{\"filePaths\":[\"/a.pdf\",\"/b.pdf\"]}", json);
        }

        [Test]
        public void BuildShareFilesJson_EmptyArray_ProducesEmptyFilePaths()
        {
            string json = AndroidShareJsonBuilder.BuildShareFilesJson(
                new ShareFilesPayload { filePaths = new string[0] });

            Assert.AreEqual("{\"filePaths\":[]}", json);
        }

        // ---- DirectShareTarget ----

        [Test]
        public void BuildDirectShareTargetJson_RequiredOnly_ExactJson()
        {
            string json = AndroidShareJsonBuilder.BuildDirectShareTargetJson(new DirectShareTargetPayload
            {
                id = "t1",
                label = "Friend",
                iconBase64 = "B64"
            });

            Assert.AreEqual("{\"id\":\"t1\",\"label\":\"Friend\",\"iconBase64\":\"B64\"}", json);
        }

        [Test]
        public void BuildDirectShareTargetJson_WithCategory_IncludesCategory()
        {
            string json = AndroidShareJsonBuilder.BuildDirectShareTargetJson(new DirectShareTargetPayload
            {
                id = "t",
                label = "L",
                iconBase64 = "B",
                category = "android.shortcut.conversation"
            });

            StringAssert.Contains("\"category\":\"android.shortcut.conversation\"", json);
        }

        // ---- RemoveDirectShareTargets ----

        [Test]
        public void BuildRemoveDirectShareTargetsJson_MultipleIds_ExactJson()
        {
            string json = AndroidShareJsonBuilder.BuildRemoveDirectShareTargetsJson(
                new RemoveDirectShareTargetsPayload { ids = new[] { "id1", "id2" } });

            Assert.AreEqual("{\"ids\":[\"id1\",\"id2\"]}", json);
        }

        [Test]
        public void BuildRemoveDirectShareTargetsJson_EmptyArray_ProducesEmptyIds()
        {
            string json = AndroidShareJsonBuilder.BuildRemoveDirectShareTargetsJson(
                new RemoveDirectShareTargetsPayload { ids = new string[0] });

            Assert.AreEqual("{\"ids\":[]}", json);
        }
    }
}
