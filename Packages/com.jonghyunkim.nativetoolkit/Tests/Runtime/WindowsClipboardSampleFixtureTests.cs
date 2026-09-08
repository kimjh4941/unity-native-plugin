#nullable enable

#if UNITY_EDITOR
using System.IO;
using System.Text;
using NUnit.Framework;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// Pins the shapes of the fixtures the Windows clipboard sample writes.
    /// </summary>
    /// <remarks>
    /// The managed layer does not look at any of this. <c>CopyImage</c> rejects only null and
    /// empty, so a malformed bitmap header travels all the way to the native validator, and the
    /// screen then shows a failed image operation. Read on a device that says "the image feature is
    /// broken" when what is broken is the sample's own fixture, and nothing in the C# build objects.
    /// </remarks>
    public sealed class WindowsClipboardSampleFixtureTests
    {
        private const int BiSizeOffset = 0;
        private const int BiWidthOffset = 4;
        private const int BiHeightOffset = 8;
        private const int BiPlanesOffset = 12;
        private const int BiBitCountOffset = 14;
        private const int BiCompressionOffset = 16;
        private const int BiSizeImageOffset = 20;

        [Test]
        public void TheDibIsExactlyTheHeaderPlusItsPixels()
        {
            byte[] dib = WindowsClipboardSampleFixtures.BuildDib();

            Assert.AreEqual(296, dib.Length, "40 byte BITMAPINFOHEADER plus 8 x 8 x 4 pixel bytes");
            Assert.AreEqual(
                WindowsClipboardSampleFixtures.DibHeaderBytes + WindowsClipboardSampleFixtures.DibPixelBytes,
                dib.Length);
        }

        /// <remarks>
        /// Every field the native validator reads. It refuses a plane count other than one, a
        /// width at or below zero, a height of exactly zero, and bit depths or compressions it does
        /// not handle.
        /// </remarks>
        [Test]
        public void TheDibHeaderCarriesWhatTheNativeValidatorChecks()
        {
            byte[] dib = WindowsClipboardSampleFixtures.BuildDib();

            Assert.AreEqual(40u, ReadUInt32(dib, BiSizeOffset), "biSize");
            Assert.AreEqual(8, ReadInt32(dib, BiWidthOffset), "biWidth");
            Assert.AreEqual(8, ReadInt32(dib, BiHeightOffset), "biHeight");
            Assert.AreEqual(1, ReadUInt16(dib, BiPlanesOffset), "biPlanes");
            Assert.AreEqual(32, ReadUInt16(dib, BiBitCountOffset), "biBitCount");
            Assert.AreEqual(0u, ReadUInt32(dib, BiCompressionOffset), "biCompression must be BI_RGB");
            Assert.AreEqual(256u, ReadUInt32(dib, BiSizeImageOffset), "biSizeImage");
        }

        /// <remarks>
        /// Positive is bottom-up, which is what the clipboard expects here. Zero is rejected
        /// outright, and a negative value would be a top-down bitmap this sample has no reason to
        /// produce.
        /// </remarks>
        [Test]
        public void TheDibRunsBottomUp()
        {
            Assert.Greater(ReadInt32(WindowsClipboardSampleFixtures.BuildDib(), BiHeightOffset), 0);
        }

        /// <remarks>
        /// A BITMAPFILEHEADER starts with "BM" and would shift every field above it by fourteen
        /// bytes. The clipboard carries the info header alone.
        /// </remarks>
        [Test]
        public void TheDibHasNoFileHeaderInFrontOfIt()
        {
            byte[] dib = WindowsClipboardSampleFixtures.BuildDib();

            Assert.IsFalse(
                dib[0] == (byte)'B' && dib[1] == (byte)'M',
                "a BITMAPFILEHEADER must not be prepended");
        }

        /// <remarks>
        /// Two independent reasons the body cannot survive an ANSI code page: Hangul is outside
        /// 932 and 1252, and a non-BMP character is outside all of them. A body of ASCII plus
        /// Japanese would come back intact on a 932 machine, and the check would then report a loss
        /// that never happened.
        /// </remarks>
        [Test]
        public void TheAnsiFixtureCannotSurviveAnAnsiCodePage()
        {
            string body = WindowsClipboardSampleFixtures.AnsiLossyText;

            Assert.IsTrue(HasNonBmpCharacter(body), "needs a character no ANSI code page can encode");
            Assert.IsTrue(HasHangul(body), "needs a character outside the Japanese and Western code pages");
            Assert.IsFalse(IsAscii(body), "an ASCII body would round-trip through CF_TEXT intact");
        }

        [Test]
        public void TheLargeFixtureIsAboutAMegabyte()
        {
            Assert.AreEqual(1024 * 1024, WindowsClipboardSampleFixtures.LargeText().Length);
        }

        /// <remarks>
        /// Two calls must not produce the same body, or a paste cannot tell this copy from the
        /// previous one and a stale clipboard reads as a successful round trip.
        /// </remarks>
        [Test]
        public void TwoPlainTextFixturesDiffer()
        {
            Assert.AreNotEqual(
                WindowsClipboardSampleFixtures.PlainText(1),
                WindowsClipboardSampleFixtures.PlainText(2));
        }

        [Test]
        public void TheCustomFixtureIsItsBodyInUtf8()
        {
            CollectionAssert.AreEqual(
                Encoding.UTF8.GetBytes(WindowsClipboardSampleFixtures.CustomBody),
                WindowsClipboardSampleFixtures.CustomBytes());
        }

        /// <remarks>
        /// Unity hands back forward slashes here, and the native layer writes what it is given into
        /// DROPFILES without normalising it. Left alone the copy still succeeds and only the paste
        /// into Explorer fails, which points the blame at the file operations rather than the path.
        /// </remarks>
        [Test]
        public void TemporaryFilePathsAreAbsoluteAndUseBackslashes()
        {
            var paths = WindowsClipboardSampleFixtures.CreateTempFiles();
            try
            {
                Assert.AreEqual(2, paths.Count);
                foreach (string path in paths)
                {
                    Assert.IsTrue(Path.IsPathRooted(path), path);
                    Assert.IsTrue(File.Exists(path), path);
                    Assert.IsFalse(path.Contains("/"), $"forward slashes reach DROPFILES unchanged: {path}");
                }
            }
            finally
            {
                WindowsClipboardSampleFixtures.DeleteTempFiles();
            }
        }

        [Test]
        public void DeletingTheTemporaryFilesTwiceIsSafeAndReportsZeroTheSecondTime()
        {
            WindowsClipboardSampleFixtures.CreateTempFiles();

            Assert.AreEqual(2, WindowsClipboardSampleFixtures.DeleteTempFiles());
            Assert.AreEqual(0, WindowsClipboardSampleFixtures.DeleteTempFiles());
        }

        [Test]
        public void TheDigestSeparatesContentThatDiffersByOneByte()
        {
            Assert.AreNotEqual(
                WindowsClipboardSampleFixtures.HashOf(new byte[] { 1, 2, 3 }),
                WindowsClipboardSampleFixtures.HashOf(new byte[] { 1, 2, 4 }));
        }

        /// <remarks>
        /// Zero is the sample's "no anchor" value, so a real body must never digest to it or a
        /// genuine comparison would silently be reported as not applicable.
        /// </remarks>
        [Test]
        public void NoFixtureDigestsToTheNoAnchorValue()
        {
            Assert.AreNotEqual(0UL, WindowsClipboardSampleFixtures.HashOf(WindowsClipboardSampleFixtures.CustomBytes()));
            Assert.AreNotEqual(0UL, WindowsClipboardSampleFixtures.HashOf(WindowsClipboardSampleFixtures.BuildDib()));
            Assert.AreNotEqual(0UL, WindowsClipboardSampleFixtures.HashOf(WindowsClipboardSampleFixtures.PlainText(1)));
        }

        private static bool IsAscii(string value)
        {
            foreach (char c in value)
            {
                if (c > 0x7F) return false;
            }
            return true;
        }

        private static bool HasNonBmpCharacter(string value)
        {
            foreach (char c in value)
            {
                if (char.IsHighSurrogate(c)) return true;
            }
            return false;
        }

        private static bool HasHangul(string value)
        {
            foreach (char c in value)
            {
                if (c >= 0xAC00 && c <= 0xD7A3) return true;
            }
            return false;
        }

        private static uint ReadUInt32(byte[] buffer, int at) =>
            (uint)(buffer[at] | (buffer[at + 1] << 8) | (buffer[at + 2] << 16) | (buffer[at + 3] << 24));

        private static int ReadInt32(byte[] buffer, int at) => unchecked((int)ReadUInt32(buffer, at));

        private static int ReadUInt16(byte[] buffer, int at) => buffer[at] | (buffer[at + 1] << 8);
    }
}
#endif
