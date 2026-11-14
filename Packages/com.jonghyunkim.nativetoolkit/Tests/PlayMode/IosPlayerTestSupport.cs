#nullable enable

#if UNITY_IOS
#if !UNITY_EDITOR
using System;
using System.Collections;
using UnityEngine;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// Shared pieces of the iOS player tests (category <see cref="Category"/>). They run only on an iOS
    /// player built by scripts/verify_unity_ios.sh (artifact/topics/ios-c-abi-2, UI automation design v4,
    /// 3.3) and never show a system screen.
    /// </summary>
    internal static class IosPlayerTestSupport
    {
        public const string Category = "IosPlayer";

        /// <summary>
        /// Yields until <paramref name="done"/> is true or <paramref name="seconds"/> have passed. It does
        /// not fail by itself: the caller asserts on what it waited for, so a timeout fails in the test's
        /// own body and its finally blocks still run (an exception inside a nested enumerator would
        /// skip them).
        /// </summary>
        public static IEnumerator WaitFor(Func<bool> done, float seconds = 10f)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!done() && Time.realtimeSinceStartup <= deadline)
                yield return null;
        }
    }
}
#endif
#endif
