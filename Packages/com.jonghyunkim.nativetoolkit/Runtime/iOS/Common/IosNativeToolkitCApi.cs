#nullable enable

#if UNITY_IOS || UNITY_EDITOR
namespace JonghyunKim.NativeToolkit.Runtime.iOS.Common
{
    using System.Runtime.InteropServices;
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

        /// <summary><c>uint32_t ntk_version(void)</c> (Common.h): the packed version of the linked C ABI.</summary>
        [DllImport(LibraryName)]
        internal static extern uint ntk_version();
    }
}
#endif
