#nullable enable

#if UNITY_IOS || UNITY_EDITOR
namespace JonghyunKim.NativeToolkit.Runtime.iOS.Common
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using System.Text;
    using UnityEngine;

    /// <summary>
    /// The part of native-toolkit's C ABI that every iOS feature uses: the version check
    /// (artifact/topics/ios-c-abi-2, decision I-14; 3-S design v3, 1.3).
    /// </summary>
    /// <remarks>
    /// On iOS the C ABI is linked into the app (IosLibraryCApi.framework, embedded by Unity), so the
    /// functions are found with <c>__Internal</c>. The declarations are compiled in the Editor too (outside
    /// <c>!UNITY_EDITOR</c>), so that the EditMode check compares them with the headers; they are called on
    /// an iOS player only. Each feature keeps its own <c>ntk_&lt;feature&gt;_*</c> declarations in a file
    /// with the same <c>#if</c> (3-S design v3, 1.3: the rule for every feature).
    /// </remarks>
    internal static class IosNativeToolkitCApi
    {
        /// <summary>Functions linked into the app itself.</summary>
        internal const string LibraryName = "__Internal";

        /// <summary>The C ABI major version these bindings are written against.</summary>
        internal const uint SupportedMajor = 2;

        private static bool? s_isAvailable;

        /// <summary>
        /// Whether a packed <c>NTK_VERSION</c> (<c>(major &lt;&lt; 16) | (minor &lt;&lt; 8) | patch</c>)
        /// has the major version these bindings speak.
        /// </summary>
        internal static bool IsSupportedVersion(uint packedVersion) => (packedVersion >> 16) == SupportedMajor;

        /// <summary>
        /// Whether the C ABI can be called: an iOS player whose linked native-toolkit reports major version
        /// 2. Read once; a mismatch is logged once.
        /// </summary>
        internal static bool IsAvailable
        {
            get
            {
                if (s_isAvailable is bool known) return known;
                bool available = false;
#if UNITY_IOS && !UNITY_EDITOR
                uint version = ntk_version();
                available = IsSupportedVersion(version);
                if (!available)
                    Debug.LogWarning($"[{nameof(IosNativeToolkitCApi)}] native-toolkit C ABI version 0x{version:X6} is not major {SupportedMajor}; the iOS features are unavailable.");
#endif
                s_isAvailable = available;
                return available;
            }
        }

        /// <summary>
        /// A string as NUL-terminated UTF-8. A lone surrogate becomes U+FFFD (the encoder's
        /// replacement), so the C ABI never sees invalid UTF-8 from C#.
        /// </summary>
        internal static byte[] ToUtf8WithTerminator(string value)
        {
            int length = Encoding.UTF8.GetByteCount(value);
            var bytes = new byte[length + 1];
            Encoding.UTF8.GetBytes(value, 0, value.Length, bytes, 0);
            return bytes;
        }

        /// <summary>
        /// Copies a string into unmanaged memory as NUL-terminated UTF-8 and records the pointer in
        /// <paramref name="allocated"/>, which the caller frees with <see cref="FreeAll"/>.
        /// <see langword="null"/> becomes <see cref="IntPtr.Zero"/> and allocates nothing.
        /// </summary>
        /// <remarks>
        /// Placed here, not shared with the Android helpers: those compile for Android and the Editor
        /// only, not into an iOS player (Dialog design v2, D-11).
        /// </remarks>
        internal static IntPtr AllocUtf8(string? value, List<IntPtr> allocated)
        {
            if (value == null) return IntPtr.Zero;

            byte[] bytes = ToUtf8WithTerminator(value);
            IntPtr pointer = Alloc(bytes.Length, allocated);
            Marshal.Copy(bytes, 0, pointer, bytes.Length);
            return pointer;
        }

        /// <summary>
        /// Allocates unmanaged memory and records it in <paramref name="allocated"/> for
        /// <see cref="FreeAll"/>. If recording it fails, the memory is freed before the exception leaves.
        /// </summary>
        internal static IntPtr Alloc(int byteCount, List<IntPtr> allocated)
        {
            IntPtr pointer = Marshal.AllocHGlobal(byteCount);
            try
            {
                allocated.Add(pointer);
            }
            catch
            {
                Marshal.FreeHGlobal(pointer);
                throw;
            }
            return pointer;
        }

        /// <summary>
        /// A <c>const char* const*</c>: each item as NUL-terminated UTF-8, then the array of pointers,
        /// all recorded in <paramref name="allocated"/>. An empty array still gets one slot, so the
        /// pointer is never NULL. The caller makes sure no item is <see langword="null"/>.
        /// </summary>
        internal static IntPtr AllocStringArray(string[] items, List<IntPtr> allocated)
        {
            IntPtr array = Alloc(IntPtr.Size * Math.Max(items.Length, 1), allocated);
            for (int i = 0; i < items.Length; i++)
                Marshal.WriteIntPtr(array, i * IntPtr.Size, AllocUtf8(items[i], allocated));
            return array;
        }

        /// <summary>Frees every pointer in the list and empties it.</summary>
        internal static void FreeAll(List<IntPtr> allocated)
        {
            foreach (IntPtr pointer in allocated)
                Marshal.FreeHGlobal(pointer);
            allocated.Clear();
        }

        /// <summary>
        /// Reads a string the C ABI returns as a pointer and a size, keeping its two kinds of nothing
        /// apart: NULL becomes <see langword="null"/>, a non-NULL pointer of size 0 becomes "". The
        /// data is not assumed to be NUL-terminated.
        /// </summary>
        internal static string? ReadUtf8OrNull(IntPtr data, UIntPtr size)
        {
            if (data == IntPtr.Zero) return null;
            ulong count = size.ToUInt64();
            if (count == 0) return "";
            if (count > int.MaxValue)
                throw new OutOfMemoryException($"a native string of {count} bytes does not fit a managed array");

            var bytes = new byte[(int)count];
            Marshal.Copy(data, bytes, 0, (int)count);
            return Encoding.UTF8.GetString(bytes);
        }

        /// <summary><c>uint32_t ntk_version(void)</c> (Common.h): the packed version of the linked C ABI.</summary>
        [DllImport(LibraryName)]
        internal static extern uint ntk_version();
    }
}
#endif
