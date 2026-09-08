#nullable enable

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Fixture bodies and byte layouts the Windows clipboard sample writes, plus the temporary files
/// the file operations need.
/// </summary>
/// <remarks>
/// <para>
/// Kept out of the controller so the shapes that a manual check judges against can be pinned by
/// <c>WindowsClipboardSampleFixtureTests</c> without a UIDocument. The DIB in particular passes
/// straight through the managed layer: <c>CopyImage</c> only rejects null and empty, so a broken
/// header reaches the native validator and reads on screen as "the image feature is broken"
/// rather than "the sample built a bad bitmap".
/// </para>
/// <para>
/// Nothing here logs a fixture body. The bodies are sample text rather than secrets, but the
/// screen's rule is that clipboard content never reaches the log, and a fixture becomes clipboard
/// content the moment it is written.
/// </para>
/// </remarks>
internal static class WindowsClipboardSampleFixtures
{
    private const string LogTag = "WindowsClipboardSampleFixtures";

    internal const string PlainTextPrefix = "NativeToolkit clipboard sample ";
    internal const string HtmlFragment = "<b>Hello</b> from NativeToolkit";
    internal const string HtmlPlainFallback = "Hello from NativeToolkit";
    internal const string CustomFormatDefaultName = "NativeToolkitSample";
    internal const string CustomBody = "native-toolkit-sample-payload";

    /// <summary>
    /// The body for the CF_TEXT loss check (M-8).
    /// </summary>
    /// <remarks>
    /// Two independent reasons it cannot survive an ANSI code page. Hangul is absent from 932 and
    /// from 1252, and the rocket is outside the BMP, which no ANSI code page can encode at all.
    /// A body of ASCII plus Japanese would round-trip intact on a 932 machine and the check would
    /// report a loss that never happened.
    /// </remarks>
    internal const string AnsiLossyText = "한글 clipboard \U0001F680";

    internal const string DuplicateFormatName = "CF_UNICODETEXT";

    /// <summary>Roughly a megabyte, for the large round trip (M-21).</summary>
    /// <remarks>
    /// Size is not what puts a read on the retry path: that happens only when the content grows
    /// between the two native calls. This body is here to round-trip a large payload, nothing more.
    /// </remarks>
    internal const int LargeTextLength = 1024 * 1024;

    private const char LargeTextFillChar = 'A';

    // BITMAPINFOHEADER 40 + 8 * 8 * 4 pixel bytes. Fixed here rather than computed so the test can
    // assert the number the sample plan names.
    internal const int DibByteCount = 296;
    internal const int DibHeaderBytes = 40;
    internal const int DibWidth = 8;
    internal const int DibHeight = 8;
    internal const ushort DibPlanes = 1;
    internal const ushort DibBitCount = 32;
    internal const uint DibCompressionBiRgb = 0;
    internal const int DibPixelBytes = DibWidth * DibHeight * 4;

    // Same colour the native toolkit's own sample writes, so a paste into Paint looks the same on
    // both sides and a difference points at this layer rather than at the eye.
    private const byte DibBlue = 215;
    private const byte DibGreen = 120;
    private const byte DibRed = 0;

    private const string TempFilePrefix = "nativetoolkit-clipboard-sample-";
    private const int TempFileCount = 2;

    /// <summary>Plain text body carrying a call number, so two pastes can be told apart.</summary>
    /// <param name="sequence">Monotonic sample sequence number.</param>
    /// <returns>The body to write.</returns>
    internal static string PlainText(int sequence) =>
        PlainTextPrefix + sequence.ToString(CultureInfo.InvariantCulture);

    /// <summary>About a megabyte of one repeated character.</summary>
    /// <returns>The body to write.</returns>
    internal static string LargeText() => new(LargeTextFillChar, LargeTextLength);

    /// <summary>UTF-8 bytes for the custom format operations.</summary>
    /// <returns>The bytes to write.</returns>
    internal static byte[] CustomBytes() => Encoding.UTF8.GetBytes(CustomBody);

    /// <summary>
    /// Builds the 8x8 32bpp BI_RGB device-independent bitmap the image operations write.
    /// </summary>
    /// <returns>A 296 byte DIB: BITMAPINFOHEADER followed by bottom-up pixel rows.</returns>
    /// <remarks>
    /// No BITMAPFILEHEADER: the clipboard carries the info header onwards, and a file header in
    /// front shifts every field the native validator reads. biHeight stays positive, which is
    /// bottom-up; the validator rejects zero and this sample has no reason to test top-down.
    /// </remarks>
    internal static byte[] BuildDib()
    {
        var dib = new byte[DibByteCount];
        int at = 0;

        WriteUInt32(dib, ref at, DibHeaderBytes);       // biSize
        WriteInt32(dib, ref at, DibWidth);              // biWidth
        WriteInt32(dib, ref at, DibHeight);             // biHeight, positive so rows run bottom-up
        WriteUInt16(dib, ref at, DibPlanes);            // biPlanes
        WriteUInt16(dib, ref at, DibBitCount);          // biBitCount
        WriteUInt32(dib, ref at, DibCompressionBiRgb);  // biCompression
        WriteUInt32(dib, ref at, DibPixelBytes);        // biSizeImage
        WriteInt32(dib, ref at, 0);                     // biXPelsPerMeter
        WriteInt32(dib, ref at, 0);                     // biYPelsPerMeter
        WriteUInt32(dib, ref at, 0);                    // biClrUsed
        WriteUInt32(dib, ref at, 0);                    // biClrImportant

        for (int pixel = 0; pixel < DibWidth * DibHeight; pixel++)
        {
            dib[at++] = DibBlue;
            dib[at++] = DibGreen;
            dib[at++] = DibRed;
            dib[at++] = 0; // Unused for BI_RGB; kept zero rather than 255 to match the native sample.
        }

        return dib;
    }

    /// <summary>
    /// Creates the temporary files the file operations copy.
    /// </summary>
    /// <returns>Absolute paths with backslash separators.</returns>
    /// <remarks>
    /// <c>Application.temporaryCachePath</c> hands back forward slashes on Windows, and the native
    /// layer writes whatever it is given into DROPFILES without normalising it. Left alone, the
    /// copy succeeds and only the paste into Explorer fails, which points the blame at the file
    /// operations rather than at the path. <c>Path.GetFullPath</c> is what turns them round.
    /// </remarks>
    internal static IReadOnlyList<string> CreateTempFiles()
    {
        Debug.Log($"[{LogTag}][{nameof(CreateTempFiles)}] count: {TempFileCount}");
        var paths = new List<string>(TempFileCount);
        for (int i = 0; i < TempFileCount; i++)
        {
            string path = Path.GetFullPath(Path.Combine(
                Application.temporaryCachePath,
                TempFilePrefix + i.ToString(CultureInfo.InvariantCulture) + ".txt"));
            File.WriteAllText(path, "NativeToolkit clipboard sample file " + i, Encoding.UTF8);
            paths.Add(path);
        }
        return paths;
    }

    /// <summary>Removes the files <see cref="CreateTempFiles"/> made.</summary>
    /// <returns>How many existed and were removed.</returns>
    internal static int DeleteTempFiles()
    {
        Debug.Log($"[{LogTag}][{nameof(DeleteTempFiles)}]");
        int removed = 0;
        for (int i = 0; i < TempFileCount; i++)
        {
            string path = Path.GetFullPath(Path.Combine(
                Application.temporaryCachePath,
                TempFilePrefix + i.ToString(CultureInfo.InvariantCulture) + ".txt"));
            if (!File.Exists(path)) continue;
            File.Delete(path);
            removed++;
        }
        return removed;
    }

    /// <summary>
    /// The ANSI code page the current culture reports.
    /// </summary>
    /// <returns>The code page number, or 0 when the culture has none.</returns>
    /// <remarks>
    /// A stand-in for GetACP, which would need a P/Invoke this sample has no other reason to add.
    /// The two normally agree, but they are not the same thing: the native layer encodes CF_TEXT
    /// with the system ANSI code page. Shown so the operator can record which one was in force,
    /// because a UTF-8 code page makes M-8 unobservable rather than failing.
    /// </remarks>
    internal static int CultureAnsiCodePage() => CultureInfo.CurrentCulture.TextInfo.ANSICodePage;

    /// <summary>FNV-1a over bytes, used only to compare a round trip against what was written.</summary>
    /// <param name="bytes">Bytes to digest.</param>
    /// <returns>The digest. Never displayed, only compared.</returns>
    internal static ulong HashOf(byte[] bytes)
    {
        ulong hash = 14695981039346656037UL;
        foreach (byte b in bytes)
        {
            hash ^= b;
            hash *= 1099511628211UL;
        }
        return hash;
    }

    /// <summary>FNV-1a over a string's UTF-8 bytes.</summary>
    /// <param name="text">Text to digest, or null.</param>
    /// <returns>The digest, or 0 for null.</returns>
    internal static ulong HashOf(string? text) =>
        text == null ? 0UL : HashOf(Encoding.UTF8.GetBytes(text));

    private static void WriteUInt32(byte[] buffer, ref int at, uint value)
    {
        buffer[at++] = (byte)(value & 0xFF);
        buffer[at++] = (byte)((value >> 8) & 0xFF);
        buffer[at++] = (byte)((value >> 16) & 0xFF);
        buffer[at++] = (byte)((value >> 24) & 0xFF);
    }

    private static void WriteInt32(byte[] buffer, ref int at, int value) =>
        WriteUInt32(buffer, ref at, unchecked((uint)value));

    private static void WriteUInt16(byte[] buffer, ref int at, ushort value)
    {
        buffer[at++] = (byte)(value & 0xFF);
        buffer[at++] = (byte)((value >> 8) & 0xFF);
    }
}
#endif
