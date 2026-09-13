#nullable enable

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
using JonghyunKim.NativeToolkit.Runtime.Clipboard;
using NUnit.Framework;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// EditMode tests for the Windows clipboard JSON builder. The native side parses these strings
    /// with the WinRT JSON parser, so escaping and ordering are part of the contract.
    /// </summary>
    public sealed class WindowsClipboardJsonBuilderTests
    {
        [Test]
        public void BuildPathsJson_EscapesBackslashes()
        {
            string json = WindowsClipboardJsonBuilder.BuildPathsJson(
                new[] { @"C:\a.txt", @"D:\dir\b.png" });

            Assert.AreEqual("[\"C:\\\\a.txt\",\"D:\\\\dir\\\\b.png\"]", json);
        }

        [Test]
        public void BuildPathsJson_EmptyListProducesAnEmptyArray()
        {
            Assert.AreEqual("[]", WindowsClipboardJsonBuilder.BuildPathsJson(new string[0]));
        }

        [Test]
        public void BuildPathsJson_NullListProducesAnEmptyArray()
        {
            Assert.AreEqual("[]", WindowsClipboardJsonBuilder.BuildPathsJson(null!));
        }

        [Test]
        public void BuildStringArray_EscapesQuotesAndControlCharacters()
        {
            string json = WindowsClipboardJsonBuilder.BuildFormatNamesJson(
                new[] { "say \"hi\"", "line\nbreak", "tab\there" });

            Assert.AreEqual("[\"say \\\"hi\\\"\",\"line\\nbreak\",\"tab\\there\"]", json);
        }

        [Test]
        public void BuildStringArray_EscapesOtherControlCharactersAsUnicode()
        {
            string json = WindowsClipboardJsonBuilder.BuildFormatNamesJson(new[] { "a\u0001b" });

            Assert.AreEqual("[\"a\\u0001b\"]", json);
        }

        [Test]
        public void BuildStringArray_PassesNonAsciiThrough()
        {
            string json = WindowsClipboardJsonBuilder.BuildFormatNamesJson(new[] { "日本語" });

            Assert.AreEqual("[\"日本語\"]", json);
        }

        [Test]
        public void BuildStringArray_EscapesTheRemainingControlCharacters()
        {
            string json = WindowsClipboardJsonBuilder.BuildFormatNamesJson(
                new[] { "a\bb", "c\fd", "e\rf" });

            Assert.AreEqual("[\"a\\bb\",\"c\\fd\",\"e\\rf\"]", json);
        }

        [Test]
        public void BuildStringArray_KeepsSurrogatePairsIntact()
        {
            // An emoji is two UTF-16 units; splitting or escaping one half would corrupt it.
            string json = WindowsClipboardJsonBuilder.BuildFormatNamesJson(new[] { "a\U0001F600b" });

            Assert.AreEqual("[\"a\U0001F600b\"]", json);
        }

        [Test]
        public void BuildStringArray_ANullElementBecomesAnEmptyString()
        {
            string json = WindowsClipboardJsonBuilder.BuildFormatNamesJson(new string[] { null! });

            Assert.AreEqual("[\"\"]", json);
        }

        [Test]
        public void BuildMultiFormatItemsJson_KeepsOrderAndUsesOnePayloadKeyPerItem()
        {
            string json = WindowsClipboardJsonBuilder.BuildMultiFormatItemsJson(new[]
            {
                WindowsClipboardFormatPayload.Html("HTML Format", "<b>a</b>"),
                WindowsClipboardFormatPayload.Text("CF_UNICODETEXT", "a"),
                WindowsClipboardFormatPayload.Base64("CF_DIB", "AAA=")
            });

            Assert.AreEqual(
                "[{\"format\":\"HTML Format\",\"html\":\"<b>a</b>\"}," +
                "{\"format\":\"CF_UNICODETEXT\",\"text\":\"a\"}," +
                "{\"format\":\"CF_DIB\",\"base64\":\"AAA=\"}]",
                json);
        }

        [Test]
        public void BuildMultiFormatItemsJson_EmptyListProducesAnEmptyArray()
        {
            Assert.AreEqual("[]",
                WindowsClipboardJsonBuilder.BuildMultiFormatItemsJson(new WindowsClipboardFormatPayload[0]));
        }

        [Test]
        public void BuildMultiFormatItemsJson_EscapesThePayload()
        {
            string json = WindowsClipboardJsonBuilder.BuildMultiFormatItemsJson(new[]
            {
                WindowsClipboardFormatPayload.Text("CF_UNICODETEXT", "a\"b\\c")
            });

            Assert.AreEqual("[{\"format\":\"CF_UNICODETEXT\",\"text\":\"a\\\"b\\\\c\"}]", json);
        }

        [Test]
        public void ALoneSurrogateIsEscapedRatherThanWrittenAsText()
        {
            // On its own a surrogate is not valid UTF-16, and writing it raw hands the native
            // parser a payload it rejects - with nothing to say which entry was at fault.
            string json = WindowsClipboardJsonBuilder.BuildPathsJson(new[] { "a\ud83db" });

            Assert.AreEqual("[\"a\\ud83db\"]", json);
        }

        [Test]
        public void ASurrogatePairIsLeftAlone()
        {
            // A pair is valid UTF-16 and the boundary is UTF-16 on both sides, so escaping it here
            // would only make the payload larger.
            string json = WindowsClipboardJsonBuilder.BuildPathsJson(new[] { "a\ud83d\ude00b" });

            Assert.AreEqual("[\"a\ud83d\ude00b\"]", json);
        }

        [Test]
        public void ALoneLowSurrogateIsEscapedToo()
        {
            string json = WindowsClipboardJsonBuilder.BuildPathsJson(new[] { "\ude00" });

            Assert.AreEqual("[\"\\ude00\"]", json);
        }

    }
}
#endif
