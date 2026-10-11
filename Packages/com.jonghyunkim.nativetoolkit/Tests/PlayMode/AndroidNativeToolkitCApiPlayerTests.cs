#nullable enable

#if UNITY_ANDROID
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using JonghyunKim.NativeToolkit.Runtime.Android.Common;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// Layer 2b tests (agent-rules/coding-rules/testing.md) for the part of native-toolkit's C ABI
    /// every Android feature shares: that Unity's <c>DllImport</c> reaches <c>libntk.so</c>, that the
    /// library speaks major 2, and that the copy Unity loads is the one androidx.startup initialized.
    /// </summary>
    /// <remarks>
    /// The last point is the first thing the stage 2c handoff asks to confirm (handoff 4.1): native-
    /// toolkit only proved that a second .so loaded with System.loadLibrary shares the instance, not
    /// that Unity's DllImport does. If Unity loaded a separate copy, every call would answer
    /// NOT_INITIALIZED for ever.
    /// </remarks>
    public sealed class AndroidNativeToolkitCApiPlayerTests
    {
        /// <summary>androidx.startup runs before the first activity; allow for a slow first launch anyway.</summary>
        private const float InitializationTimeoutSeconds = 5f;

        [SetUp]
        public void SetUp()
        {
            if (Application.platform != RuntimePlatform.Android)
                Assert.Ignore("Device-only: the C ABI is loaded only in an Android player.");
        }

#if !UNITY_EDITOR
        [Test]
        public void Version_HasTheSupportedMajor()
        {
            uint version = AndroidNativeToolkitCApi.ntk_version();

            Assert.That(version >> 16, Is.EqualTo(AndroidNativeToolkitCApi.SupportedMajor), $"ntk_version() = 0x{version:X6}");
            Assert.That(AndroidNativeToolkitCApi.EnsureNativeAvailable(), Is.EqualTo(AndroidNativeToolkitCApi.NativeState.Available));
        }

        [UnityTest]
        public IEnumerator IsInitialized_TheLibraryUnityLoadsIsTheOneAndroidxStartupInitialized()
        {
            float deadline = Time.realtimeSinceStartup + InitializationTimeoutSeconds;
            while (AndroidNativeToolkitCApi.ntk_android_is_initialized() == 0 && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.That(AndroidNativeToolkitCApi.ntk_android_is_initialized(), Is.Not.EqualTo(0),
                "libntk.so loaded through DllImport reports that it is not initialized. Either androidx.startup " +
                "did not run NtkInitializer, or Unity loaded a separate copy of the library.");
            Assert.That(AndroidNativeToolkitCApi.IsInitialized(), Is.True);
        }
#endif
    }
}
#endif
