#nullable enable

#if UNITY_ANDROID || UNITY_EDITOR
namespace JonghyunKim.NativeToolkit.Runtime.Android.Common
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using System.Text;
    using UnityEngine;

    /// <summary>
    /// The part of native-toolkit's C ABI that every Android feature uses: the library name, the
    /// version and initialization checks, the <c>Common.h</c> functions, and UTF-8 in and out.
    /// </summary>
    /// <remarks>
    /// One place for these, so that moving to a new native-toolkit version changes one name
    /// (agent-rules/coding-rules/common.md, "Unity Bridge パターン"). Each feature's bridge keeps
    /// its own <c>ntk_&lt;feature&gt;_*</c> declarations: only <c>Common.h</c> is shared with
    /// Windows in shape, and even that is declared again here rather than shared, because the two
    /// platforms load different libraries.
    /// <para>
    /// <c>libntk.so</c> ships for arm64-v8a and x86_64 only. On armeabi-v7a the first call throws
    /// <see cref="DllNotFoundException"/>, which <see cref="EnsureNativeAvailable"/> turns into
    /// <see cref="NativeState.NativeUnavailable"/>.
    /// </para>
    /// <para>
    /// The library is initialized at app start by androidx.startup (<c>NtkInitializer</c>), not by
    /// Unity. Until that has finished, every operation answers <c>NOT_INITIALIZED</c>; callers check
    /// <see cref="IsInitialized"/> first.
    /// </para>
    /// </remarks>
    internal static class AndroidNativeToolkitCApi
    {
        private const string LogTag = nameof(AndroidNativeToolkitCApi);

        /// <summary>
        /// <c>libntk.so</c> from android-native-toolkit-capi. The loader adds the "lib" prefix and
        /// the ".so" extension.
        /// </summary>
        internal const string LibraryName = "ntk";

        /// <summary>The C ABI major version these bindings are written against.</summary>
        internal const uint SupportedMajor = 2;

        /// <summary>Whether the native library can be called, as found on the first check.</summary>
        internal enum NativeState
        {
            /// <summary>The library loaded and its major version matches.</summary>
            Available,

            /// <summary>The library is missing (armeabi-v7a), lacks an entry point, or reports another major version.</summary>
            NativeUnavailable,

            /// <summary>Not an Android player: the Editor, where the library is never loaded.</summary>
            PlatformUnavailable,
        }

        /// <summary>
        /// Whether a packed <c>NTK_VERSION</c> (<c>(major &lt;&lt; 16) | (minor &lt;&lt; 8) | patch</c>)
        /// has the major version these bindings speak.
        /// </summary>
        internal static bool IsSupportedVersion(uint packedVersion) => (packedVersion >> 16) == SupportedMajor;

        /// <summary>Whether an exception says the native library could not be loaded or bound.</summary>
        internal static bool IsNativeUnavailable(Exception exception) =>
            exception is DllNotFoundException
            || exception is EntryPointNotFoundException
            || exception is BadImageFormatException;

        /// <summary>A string as NUL-terminated UTF-8. Unpaired surrogates become U+FFFD.</summary>
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
        internal static IntPtr AllocUtf8(string? value, List<IntPtr> allocated)
        {
            if (value == null) return IntPtr.Zero;

            byte[] bytes = ToUtf8WithTerminator(value);
            IntPtr pointer = Marshal.AllocHGlobal(bytes.Length);
            allocated.Add(pointer);
            Marshal.Copy(bytes, 0, pointer, bytes.Length);
            return pointer;
        }

        /// <summary>Frees every pointer in the list and empties it.</summary>
        internal static void FreeAll(List<IntPtr> allocated)
        {
            foreach (IntPtr pointer in allocated)
                Marshal.FreeHGlobal(pointer);
            allocated.Clear();
        }

        /// <summary>
        /// Reads <paramref name="byteCount"/> bytes of UTF-8. The length comes from the native side;
        /// the data is not assumed to be NUL-terminated.
        /// </summary>
        internal static string ReadUtf8(IntPtr data, int byteCount)
        {
            if (data == IntPtr.Zero || byteCount <= 0) return "";

            var bytes = new byte[byteCount];
            Marshal.Copy(data, bytes, 0, byteCount);
            return Encoding.UTF8.GetString(bytes);
        }

        /// <summary>
        /// Converts a native byte count to <see cref="int"/>, refusing counts a managed array cannot hold.
        /// </summary>
        internal static int ToByteCount(UIntPtr size)
        {
            ulong value = size.ToUInt64();
            if (value > int.MaxValue)
                throw new OutOfMemoryException($"a native string of {value} bytes does not fit a managed array");
            return (int)value;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static NativeState? s_state;

        /// <summary>
        /// Checks once whether the native library loads and speaks <see cref="SupportedMajor"/>,
        /// and remembers the answer: a library that is missing or of another version does not
        /// become usable later in the same process. Initialization is a separate question that can
        /// change, so it is not part of this answer (<see cref="IsInitialized"/>).
        /// </summary>
        internal static NativeState EnsureNativeAvailable()
        {
            if (s_state is NativeState known) return known;

            NativeState state;
            try
            {
                uint version = ntk_version();
                state = IsSupportedVersion(version) ? NativeState.Available : NativeState.NativeUnavailable;
                if (state != NativeState.Available)
                    Debug.LogWarning($"[{LogTag}][{nameof(EnsureNativeAvailable)}] lib{LibraryName}.so reports version 0x{version:X6}; these bindings speak major {SupportedMajor}");
            }
            catch (Exception e) when (IsNativeUnavailable(e))
            {
                Debug.LogWarning($"[{LogTag}][{nameof(EnsureNativeAvailable)}] lib{LibraryName}.so could not be used: {e.GetType().Name}: {e.Message}");
                state = NativeState.NativeUnavailable;
            }

            s_state = state;
            return state;
        }

        /// <summary>
        /// Whether androidx.startup has finished initializing the C ABI. Asked on every call, not
        /// cached: it turns from false to true once, after the app has started.
        /// </summary>
        internal static bool IsInitialized() =>
            EnsureNativeAvailable() == NativeState.Available && ntk_android_is_initialized() != 0;

        /// <summary>Copies an <c>ntk_string</c> into a managed string. Does not free it.</summary>
        internal static string ReadString(IntPtr handle) =>
            ReadUtf8(ntk_string_data(handle), ToByteCount(ntk_string_size(handle)));

        /// <summary>
        /// Copies every entry of an <c>ntk_string_list</c> into managed strings, while the list is
        /// still alive (the entries are borrowed from it). Does not free it.
        /// </summary>
        internal static List<string> ReadStringList(IntPtr list)
        {
            ulong count = ntk_string_list_count(list).ToUInt64();
            var values = new List<string>();
            for (ulong index = 0; index < count; index++)
            {
                IntPtr data = ntk_string_list_at(list, new UIntPtr(index), out UIntPtr size);
                values.Add(ReadUtf8(data, ToByteCount(size)));
            }
            return values;
        }

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint ntk_version();

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint ntk_last_system_code();

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_android_is_initialized();

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr ntk_string_data(IntPtr s);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern UIntPtr ntk_string_size(IntPtr s);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ntk_string_free(IntPtr s);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr ntk_bytes_data(IntPtr b);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern UIntPtr ntk_bytes_size(IntPtr b);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ntk_bytes_free(IntPtr b);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern UIntPtr ntk_string_list_count(IntPtr list);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr ntk_string_list_at(IntPtr list, UIntPtr index, out UIntPtr outSize);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ntk_string_list_free(IntPtr list);
#else
        /// <summary>Outside an Android player the native library is never loaded.</summary>
        internal static NativeState EnsureNativeAvailable() => NativeState.PlatformUnavailable;

        /// <summary>Outside an Android player the C ABI is never initialized.</summary>
        internal static bool IsInitialized() => false;
#endif
    }
}
#endif
