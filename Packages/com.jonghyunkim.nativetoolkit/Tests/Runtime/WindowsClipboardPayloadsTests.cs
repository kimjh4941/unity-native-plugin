#nullable enable

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
using System;
using JonghyunKim.NativeToolkit.Runtime.Clipboard;
using NUnit.Framework;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// EditMode tests for the Windows clipboard payload types: the write-option flags and the
    /// per-format payload that CopyMultipleFormats takes.
    /// </summary>
    public sealed class WindowsClipboardPayloadsTests
    {
        [Test]
        public void WriteOptions_MatchTheNativeBitValues()
        {
            Assert.AreEqual(0x0u, (uint)WindowsClipboardWriteOptions.None);
            Assert.AreEqual(0x1u, (uint)WindowsClipboardWriteOptions.ExcludeHistory);
            Assert.AreEqual(0x2u, (uint)WindowsClipboardWriteOptions.ExcludeRoaming);
            Assert.AreEqual(0x3u, (uint)WindowsClipboardWriteOptions.Sensitive);
        }

        [Test]
        public void WriteOptions_SensitiveIsBothExclusions()
        {
            WindowsClipboardWriteOptions combined =
                WindowsClipboardWriteOptions.ExcludeHistory | WindowsClipboardWriteOptions.ExcludeRoaming;

            Assert.AreEqual(WindowsClipboardWriteOptions.Sensitive, combined);
            Assert.IsTrue(WindowsClipboardWriteOptions.Sensitive.HasFlag(WindowsClipboardWriteOptions.ExcludeHistory));
            Assert.IsTrue(WindowsClipboardWriteOptions.Sensitive.HasFlag(WindowsClipboardWriteOptions.ExcludeRoaming));
        }

        [Test]
        public void FormatPayload_FactoriesSetExactlyOneKind()
        {
            WindowsClipboardFormatPayload text = WindowsClipboardFormatPayload.Text("CF_UNICODETEXT", "a");
            WindowsClipboardFormatPayload html = WindowsClipboardFormatPayload.Html("HTML Format", "<b>a</b>");
            WindowsClipboardFormatPayload raw = WindowsClipboardFormatPayload.Base64("CF_DIB", "AAA=");

            Assert.AreEqual(WindowsClipboardPayloadKind.Text, text.Kind);
            Assert.AreEqual(WindowsClipboardPayloadKind.Html, html.Kind);
            Assert.AreEqual(WindowsClipboardPayloadKind.Base64, raw.Kind);
            Assert.AreEqual("CF_UNICODETEXT", text.Format);
            Assert.AreEqual("<b>a</b>", html.Value);
        }

        [Test]
        public void FormatPayload_BytesFactoryEncodesBase64()
        {
            WindowsClipboardFormatPayload payload =
                WindowsClipboardFormatPayload.Bytes("CF_DIB", new byte[] { 1, 2, 3 });

            Assert.AreEqual(WindowsClipboardPayloadKind.Base64, payload.Kind);
            Assert.AreEqual(Convert.ToBase64String(new byte[] { 1, 2, 3 }), payload.Value);
        }

        [Test]
        public void FormatPayload_BlankFormatIsRejected()
        {
            WindowsClipboardFormatPayload payload = WindowsClipboardFormatPayload.Text("   ", "a");

            Assert.IsFalse(payload.TryValidate(out string? detail));
            Assert.IsNotNull(detail);
        }

        [Test]
        public void FormatPayload_NullPayloadIsRejected()
        {
            WindowsClipboardFormatPayload payload =
                WindowsClipboardFormatPayload.Text("CF_UNICODETEXT", null!);

            Assert.IsFalse(payload.TryValidate(out string? detail));
            StringAssert.Contains("CF_UNICODETEXT", detail!);
        }

        [Test]
        public void FormatPayload_NullByteArrayIsRejectedRatherThanEncodedAsEmpty()
        {
            // Encoding null as an empty payload would place a zero-length format on the clipboard
            // instead of reporting the caller's mistake.
            WindowsClipboardFormatPayload payload =
                WindowsClipboardFormatPayload.Bytes("CF_DIB", null!);

            Assert.IsFalse(payload.TryValidate(out string? detail));
            Assert.IsNotNull(detail);
        }

        [Test]
        public void FormatPayload_EmptyByteArrayIsAValidEmptyPayload()
        {
            WindowsClipboardFormatPayload payload =
                WindowsClipboardFormatPayload.Bytes("CF_DIB", new byte[0]);

            Assert.IsTrue(payload.TryValidate(out _));
            Assert.AreEqual(string.Empty, payload.Value);
        }

        [Test]
        public void FormatPayload_ValidEntryPasses()
        {
            WindowsClipboardFormatPayload payload =
                WindowsClipboardFormatPayload.Text("CF_UNICODETEXT", string.Empty);

            Assert.IsTrue(payload.TryValidate(out string? detail));
            Assert.IsNull(detail);
        }
    }
}
#endif
