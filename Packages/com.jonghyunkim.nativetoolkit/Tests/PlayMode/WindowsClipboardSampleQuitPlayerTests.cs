#nullable enable

// Windows player only, like the other tests that drive the sample.
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static JonghyunKim.NativeToolkit.Tests.WindowsClipboardSampleScreenDriver;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// M-19: a deferred reservation still pastes after the player has quit. Presses 64 and 65 of
    /// the manual session 1 (Reserve Deferred Formats, then Quit), with clipboard history off as
    /// in block C: with history on, the history service asks for every format as soon as it is
    /// reserved (D-8), and the check would pass whether or not the quit rendered them.
    /// <para>
    /// This test ends the player, and the test run with it: nothing reports its result. It is in
    /// its own category, excluded from every other run, and verify_unity_windows.sh runs it alone
    /// with --include-destructive. The script judges it after the player has gone: Player.log shows
    /// the sample reached Quit, and another process reads the reserved text from the clipboard.
    /// The script also puts the history setting back, since no teardown runs after a quit.
    /// </para>
    /// </summary>
    [Category(QuitCategory)]
    public sealed class WindowsClipboardSampleQuitPlayerTests
    {
        /// <summary>Read by verify_unity_windows.sh, which excludes it from the main run.</summary>
        public const string QuitCategory = "QuitsThePlayer";

        private static readonly string[] Presses = { "Clipboard", "Initialize", "ReserveDeferredFormats", "Quit" };

        /// <summary>Long enough for the quit drain; the player is gone well before.</summary>
        private const float QuitSeconds = 30f;

        [UnitySetUp]
        public IEnumerator LoadTheSample()
        {
            yield return TakeForegroundAndLog(TestContext.CurrentContext.Test.Name);
            yield return LoadAtTopMenu();
        }

        [UnityTest]
        public IEnumerator ReserveThenQuit_TheReservationStillPastes()
        {
            WriteHistorySetting(0);
            Debug.Log("[SampleRun] #0 [local] test.historyOff for M-19");

            foreach (string press in Presses)
            {
                string buttonName = press == "Clipboard" ? TopMenuClipboardButton : press + "Button";
                bool found = false;
                yield return Eventually(() => FindButton(buttonName) != null, ok => found = ok);
                Assert.IsTrue(found, $"{press}: no {buttonName} on the screen");
                Press(FindButton(buttonName)!);
                yield return null;
            }

            yield return new WaitForSecondsRealtime(QuitSeconds);
            Assert.Fail($"the player was still running {QuitSeconds}s after Quit");
        }
    }
}
#endif
