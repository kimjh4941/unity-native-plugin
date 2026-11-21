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
    /// The layout of the iOS sample screens (artifact/topics/ios-c-abi-2, UI driver design v3, chapter 9;
    /// artifact/ios/sample-layout, sample plan v3): every button inside the safe area and reachable by a tap at
    /// its centre, the root padded by the safe area, and the result readable, in portrait and in landscape.
    /// </summary>
    /// <remarks>
    /// In the gate's first player run (IosPlayer) since the samples follow the safe area (IosSampleSafeArea).
    /// IosSampleScreen: the Test iOS Profile has no sample scene, so they are ignored there. The orientation is
    /// put back to portrait after each test, and the test waits for it, so the next test starts upright.
    /// </remarks>
    [Category(IosPlayerTestSupport.Category)]
    [Category(IosUiDriver.SampleScreenCategory)]
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
            // Upright first, and wait for it: the value before may be AutoRotation, which would leave the
            // screen as it is (sample plan v3, S-4).
            Screen.orientation = ScreenOrientation.Portrait;
            float end = Time.realtimeSinceStartup + 10f;
            while (Screen.height <= Screen.width && Time.realtimeSinceStartup < end) yield return null;
            if (Screen.height <= Screen.width)
                Debug.LogWarning("[IosSampleLayoutPlayerTests] the screen did not turn back to portrait within 10 s; the next test may start in landscape");
            Screen.orientation = _orientationBefore;
            yield return WaitSeconds(1f);
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

        /// <summary>
        /// Back on the top menu after a screen, then turned: the menu the navigator puts back must be padded too,
        /// not only the one the scene starts with (sample plan v3, S-1).
        /// </summary>
        [UnityTest]
        public IEnumerator SL11_Dialog_Home_TopMenu_Landscape()
        {
            yield return LoadAtTopMenu("DialogFeatureButton");
            yield return Open(TopMenuDialog, "ShowDialogButton");
            yield return Open("HomeButton", "DialogFeatureButton");
            yield return Turn(ScreenOrientation.LandscapeLeft, expectResult: false);
        }

        /// <summary>Opens a screen (null: stays on the top menu), turns the screen, and checks every button.</summary>
        private static IEnumerator Check(string? topMenuButton, string screenButton, ScreenOrientation orientation)
        {
            yield return LoadAtTopMenu("DialogFeatureButton");
            if (topMenuButton != null)
                yield return Open(topMenuButton, screenButton);
            yield return Turn(orientation, expectResult: topMenuButton != null);
        }

        /// <summary>Turns the screen and checks the layout of the screen shown.</summary>
        private static IEnumerator Turn(ScreenOrientation orientation, bool expectResult)
        {
            Screen.orientation = orientation;
            // The rotation and the layout after it take a moment (the spike waited 4 s).
            yield return WaitSeconds(4f);
            Assert.AreEqual(orientation == ScreenOrientation.Portrait, Screen.height > Screen.width, "the screen did not turn");
            var problems = new List<string>();
            yield return CheckLayout(problems, expectResult);
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
