#nullable enable

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using JonghyunKim.NativeToolkit.Runtime.Windows.Common;
using NUnit.Framework;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// EditMode tests for the part of native-toolkit's C ABI every Windows feature shares: the
    /// version check and UTF-8 in and out. The native calls themselves run only on a player.
    /// </summary>
    public sealed class WindowsNativeToolkitCApiTests
    {
        [TestCase(0x020000u, true)]
        [TestCase(0x020103u, true)]
        [TestCase(0x02FFFFu, true)]
        [TestCase(0x010200u, false)]
        [TestCase(0x030000u, false)]
        [TestCase(0x000000u, false)]
        public void IsSupportedVersion_ComparesTheMajorOnly(uint packed, bool expected)
        {
            Assert.AreEqual(expected, WindowsNativeToolkitCApi.IsSupportedVersion(packed));
        }

        [Test]
        public void IsNativeUnavailable_CoversTheLoadAndBindFailures()
        {
            Assert.IsTrue(WindowsNativeToolkitCApi.IsNativeUnavailable(new DllNotFoundException()));
            Assert.IsTrue(WindowsNativeToolkitCApi.IsNativeUnavailable(new EntryPointNotFoundException()));
            Assert.IsTrue(WindowsNativeToolkitCApi.IsNativeUnavailable(new BadImageFormatException()));
            Assert.IsFalse(WindowsNativeToolkitCApi.IsNativeUnavailable(new OutOfMemoryException()));
            Assert.IsFalse(WindowsNativeToolkitCApi.IsNativeUnavailable(new InvalidOperationException()));
        }

        [Test]
        public void EnsureNativeAvailable_InTheEditor_IsPlatformUnavailable()
        {
            Assert.AreEqual(WindowsNativeToolkitCApi.NativeState.PlatformUnavailable, WindowsNativeToolkitCApi.EnsureNativeAvailable());
        }

        [Test]
        public void ToUtf8WithTerminator_EndsWithOneNul()
        {
            CollectionAssert.AreEqual(new byte[] { 0x61, 0x62, 0x00 }, WindowsNativeToolkitCApi.ToUtf8WithTerminator("ab"));
            CollectionAssert.AreEqual(new byte[] { 0x00 }, WindowsNativeToolkitCApi.ToUtf8WithTerminator(""));
        }

        [Test]
        public void ToUtf8WithTerminator_EncodesNonAsciiAndSurrogatePairs()
        {
            // "ダ" is three bytes, "𠮷" (a surrogate pair in UTF-16) is four.
            CollectionAssert.AreEqual(new byte[] { 0xE3, 0x83, 0x80, 0xF0, 0xA0, 0xAE, 0xB7, 0x00 },
                WindowsNativeToolkitCApi.ToUtf8WithTerminator("ダ𠮷"));
        }

        [Test]
        public void ToUtf8WithTerminator_ReplacesAnUnpairedSurrogate()
        {
            CollectionAssert.AreEqual(new byte[] { 0x61, 0xEF, 0xBF, 0xBD, 0x00 },
                WindowsNativeToolkitCApi.ToUtf8WithTerminator("a\uD800"));
        }

        [Test]
        public void AllocUtf8_RoundTripsThroughReadUtf8_AndIsFreed()
        {
            var allocated = new List<IntPtr>();
            try
            {
                IntPtr pointer = WindowsNativeToolkitCApi.AllocUtf8("C:\\ダイアログ-𠮷\\a.txt", allocated);
                Assert.AreEqual(1, allocated.Count);

                int byteCount = WindowsNativeToolkitCApi.ToUtf8WithTerminator("C:\\ダイアログ-𠮷\\a.txt").Length - 1;
                Assert.AreEqual("C:\\ダイアログ-𠮷\\a.txt", WindowsNativeToolkitCApi.ReadUtf8(pointer, byteCount));
            }
            finally
            {
                WindowsNativeToolkitCApi.FreeAll(allocated);
            }
            Assert.AreEqual(0, allocated.Count);
        }

        [Test]
        public void AllocUtf8_OfNull_IsZeroAndAllocatesNothing()
        {
            var allocated = new List<IntPtr>();
            Assert.AreEqual(IntPtr.Zero, WindowsNativeToolkitCApi.AllocUtf8(null, allocated));
            Assert.AreEqual(0, allocated.Count);
        }

        [Test]
        public void ReadUtf8_ReadsByLength_NotToANul()
        {
            IntPtr pointer = Marshal.AllocHGlobal(4);
            try
            {
                Marshal.Copy(new byte[] { 0x61, 0x62, 0x63, 0x64 }, 0, pointer, 4);
                Assert.AreEqual("ab", WindowsNativeToolkitCApi.ReadUtf8(pointer, 2));
            }
            finally
            {
                Marshal.FreeHGlobal(pointer);
            }
        }

        [Test]
        public void ReadUtf8_OfNothing_IsEmpty()
        {
            Assert.AreEqual("", WindowsNativeToolkitCApi.ReadUtf8(IntPtr.Zero, 3));
            Assert.AreEqual("", WindowsNativeToolkitCApi.ReadUtf8(new IntPtr(1), 0));
        }

        [Test]
        public void ToByteCount_RefusesWhatAnArrayCannotHold()
        {
            Assert.AreEqual(12, WindowsNativeToolkitCApi.ToByteCount(new UIntPtr(12u)));
            Assert.Throws<OutOfMemoryException>(() => WindowsNativeToolkitCApi.ToByteCount(new UIntPtr((ulong)int.MaxValue + 1)));
        }
    }
}
#endif
