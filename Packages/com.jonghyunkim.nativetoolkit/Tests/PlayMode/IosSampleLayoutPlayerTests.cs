#nullable enable

// iOS player only: the sample scene.
#if UNITY_IOS && !UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static JonghyunKim.NativeToolkit.Tests.IosSampleScreenDriver;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// The layout of the iOS sample screens (artifact/topics/ios-c-abi-2, UI driver design v3, chapter 9): every
    /// button inside the safe area and reachable by a tap at its centre, in portrait and in landscape.
    /// </summary>
    /// <remarks>
    /// Not in the gate yet: the iOS sample screens do not follow the safe area (the design's spike), and fixing
    /// them waits for the user's decision. Only the category <see cref="Category"/>, without IosPlayer, so the
    /// gate's player leaves it out; scripts/verify_unity_ios.sh --category IosSampleLayout runs it on its own
    /// on the simulator and lists what is wrong. Once the samples are fixed, add IosPlayer.
    /// </remarks>
    [Category(Category)]
    public sealed class IosSampleLayoutPlayerTests
    {
        internal const string Category = "IosSampleLayout";

        private ScreenOrientation _orientationBefore;

        [SetUp]
        public void SetUp() => _orientationBefore = Screen.orientation;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Screen.orientation = _orientationBefore;
            yield return WaitSeconds(2f);
            yield return Unload();
        }

        [UnityTest] public IEnumerator SL01_TopMenu_Portrait() => Check(null, "DialogFeatureButton", ScreenOrientation.Portrait);
        [UnityTest] public IEnumerator SL02_TopMenu_Landscape() => Check(null, "DialogFeatureButton", ScreenOrientation.LandscapeLeft);
        [UnityTest] public IEnumerator SL03_Dialog_Portrait() => Check(TopMenuDialog, "ShowDialogButton", ScreenOrientation.Portrait);
        [UnityTest] public IEnumerator SL04_Dialog_Landscape() => Check(TopMenuDialog, "ShowDialogButton", ScreenOrientation.LandscapeLeft);
        [UnityTest] public IEnumerator SL05_Share_Portrait() => Check(TopMenuShare, "ShareTextButton", ScreenOrientation.Portrait);
        [UnityTest] public IEnumerator SL06_Share_Landscape() => Check(TopMenuShare, "ShareTextButton", ScreenOrientation.LandscapeLeft);
        [UnityTest] public IEnumerator SL07_Clipboard_Portrait() => Check(TopMenuClipboard, "CopyPlainTextButton", ScreenOrientation.Portrait);
        [UnityTest] public IEnumerator SL08_Clipboard_Landscape() => Check(TopMenuClipboard, "CopyPlainTextButton", ScreenOrientation.LandscapeLeft);
        [UnityTest] public IEnumerator SL09_Notification_Portrait() => Check(TopMenuNotification, "RequestPermissionButton", ScreenOrientation.Portrait);
        [UnityTest] public IEnumerator SL10_Notification_Landscape() => Check(TopMenuNotification, "RequestPermissionButton", ScreenOrientation.LandscapeLeft);

        /// <summary>Opens a screen (null: stays on the top menu), turns the screen, and checks every button.</summary>
        private static IEnumerator Check(string? topMenuButton, string screenButton, ScreenOrientation orientation)
        {
            yield return LoadAtTopMenu("DialogFeatureButton");
            if (topMenuButton != null)
                yield return Open(topMenuButton, screenButton);
            Screen.orientation = orientation;
            // The rotation and the layout after it take a moment (the spike waited 4 s).
            yield return WaitSeconds(4f);
            Assert.AreEqual(orientation == ScreenOrientation.Portrait, Screen.height > Screen.width, "the screen did not turn");
            var problems = new List<string>();
            yield return CheckLayout(problems);
            Assert.IsEmpty(problems, $"{problems.Count} layout problem(s) in {orientation}:\n" + string.Join("\n", problems));
        }

        private static IEnumerator WaitSeconds(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) yield return null;
        }
    }
}
#endif
