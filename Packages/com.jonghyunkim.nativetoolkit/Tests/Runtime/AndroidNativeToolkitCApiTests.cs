#nullable enable

#if UNITY_ANDROID || UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using NUnit.Framework;
using JonghyunKim.NativeToolkit.Runtime.Android.Common;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// Layer 1 tests for <see cref="AndroidNativeToolkitCApi"/>, the part of the C ABI every Android
    /// feature shares: the version check, which exceptions mean "unavailable", and UTF-8 in and out.
    /// </summary>
    public sealed class AndroidNativeToolkitCApiTests
    {
        [TestCase(0x020000u, true)]
        [TestCase(0x020103u, true)]
        [TestCase(0x010300u, false)]
        [TestCase(0x030000u, false)]
        public void IsSupportedVersion(uint packed, bool expected) =>
            Assert.AreEqual(expected, AndroidNativeToolkitCApi.IsSupportedVersion(packed));

        [Test]
        public void IsNativeUnavailable()
        {
            Assert.IsTrue(AndroidNativeToolkitCApi.IsNativeUnavailable(new DllNotFoundException()));
            Assert.IsTrue(AndroidNativeToolkitCApi.IsNativeUnavailable(new EntryPointNotFoundException()));
            Assert.IsTrue(AndroidNativeToolkitCApi.IsNativeUnavailable(new BadImageFormatException()));
            Assert.IsFalse(AndroidNativeToolkitCApi.IsNativeUnavailable(new InvalidOperationException()));
        }

        [Test]
        public void EnsureNativeAvailable_InTheEditor_IsPlatformUnavailable() =>
            Assert.AreEqual(AndroidNativeToolkitCApi.NativeState.PlatformUnavailable, AndroidNativeToolkitCApi.EnsureNativeAvailable());

        [TestCase("")]
        [TestCase("abc")]
        [TestCase("ダイアログ")]
        [TestCase("𠮷")]
        public void Utf8_RoundTrips(string value)
        {
            var allocated = new List<IntPtr>();
            try
            {
                IntPtr pointer = AndroidNativeToolkitCApi.AllocUtf8(value, allocated);
                int length = Encoding.UTF8.GetByteCount(value);
                Assert.AreEqual(0, Marshal.ReadByte(pointer, length), "NUL-terminated");
                Assert.AreEqual(value, AndroidNativeToolkitCApi.ReadUtf8(pointer, length));
            }
            finally
            {
                AndroidNativeToolkitCApi.FreeAll(allocated);
            }
        }

        [Test]
        public void ToUtf8_ReplacesALoneSurrogate()
        {
            byte[] bytes = AndroidNativeToolkitCApi.ToUtf8WithTerminator("a\uD800b");
            Assert.AreEqual("a�b\0", Encoding.UTF8.GetString(bytes));
        }

        [Test]
        public void AllocUtf8_OfNull_IsNullAndAllocatesNothing()
        {
            var allocated = new List<IntPtr>();
            Assert.AreEqual(IntPtr.Zero, AndroidNativeToolkitCApi.AllocUtf8(null, allocated));
            Assert.IsEmpty(allocated);
        }

        [Test]
        public void ReadUtf8_ReadsTheGivenByteCountOnly()
        {
            IntPtr pointer = Marshal.AllocHGlobal(4);
            try
            {
                Marshal.Copy(Encoding.ASCII.GetBytes("abcd"), 0, pointer, 4);
                Assert.AreEqual("ab", AndroidNativeToolkitCApi.ReadUtf8(pointer, 2));
            }
            finally
            {
                Marshal.FreeHGlobal(pointer);
            }
        }

        [Test]
        public void ReadUtf8OrNull_KeepsNullAndEmptyApart()
        {
            Assert.IsNull(AndroidNativeToolkitCApi.ReadUtf8OrNull(IntPtr.Zero, UIntPtr.Zero), "NULL is no value");
            IntPtr pointer = Marshal.AllocHGlobal(1);
            try
            {
                Assert.AreEqual("", AndroidNativeToolkitCApi.ReadUtf8OrNull(pointer, UIntPtr.Zero), "a pointer of size 0 is an empty string");
            }
            finally
            {
                Marshal.FreeHGlobal(pointer);
            }
        }
    }
}
#endif
