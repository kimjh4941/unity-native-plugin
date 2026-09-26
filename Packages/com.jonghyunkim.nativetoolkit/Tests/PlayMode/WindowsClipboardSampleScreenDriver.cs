#nullable enable

// Windows player only, like the tests that use it.
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.Collections;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// Works the Windows clipboard sample the way a person does, for the player tests that drive
    /// its screens: loads the sample scene, finds and presses its buttons, and finds the screen
    /// controller.
    /// <para>
    /// The sample scene is loaded additively and unloaded afterwards, so the test runner's own scene
    /// is never replaced. A test player can load it because the Test Framework adds every scene in
    /// the build settings to the player it builds (PlayerLauncher).
    /// </para>
    /// <para>
    /// Buttons are pressed by sending a NavigationSubmitEvent. Button answers it with
    /// clickable.SimulateSingleClick, which invokes clicked synchronously; only the pressed-state
    /// styling is delayed. This is the same handler a mouse click reaches, without depending on
    /// pointer coordinates or layout.
    /// </para>
    /// </summary>
    internal static class WindowsClipboardSampleScreenDriver
    {
        internal const string SampleScene = "NativeToolkitExampleScene";
        internal const float TimeoutSeconds = 5f;

        /// <summary>
        /// The first load of the sample scene in a run is far slower than the later ones, and slower
        /// still when the player is not in the foreground. Five seconds was not enough for it
        /// (2026-09-26, both run tests timed out while the later screen tests loaded it at once).
        /// </summary>
        internal const float SceneLoadTimeoutSeconds = 30f;

        internal const string TopMenuClipboardButton = "ClipboardFeatureButton";
        internal const string ClipboardHomeButton = "HomeButton";

        /// <summary>
        /// Whether the player window has the foreground right now. The clipboard history API answers
        /// only a foreground window, so this goes into every failure that could come from losing it.
        /// <para>
        /// Application.isFocused alone is not the question the native layer asks: it compares the
        /// process owning GetForegroundWindow() with its own (IsSelfForeground in
        /// WindowsClipboardHistoryWinRt.cpp). A run reported NotForeground at every history test
        /// while isFocused was True at each (2026-09-26), so the note names the window that does
        /// hold the foreground, asked the same way.
        /// </para>
        /// </summary>
        internal static string FocusNote()
        {
            IntPtr foreground = GetForegroundWindow();
            string holder = "none";
            if (foreground != IntPtr.Zero)
            {
                GetWindowThreadProcessId(foreground, out uint pid);
                holder = pid == (uint)Process.GetCurrentProcess().Id
                    ? "this player"
                    : $"pid {pid} ({ProcessName(pid)}, \"{WindowTitle(foreground)}\")";
            }
            string active = GetActiveWindow() != IntPtr.Zero ? "yes" : "no";
            return $"Application.isFocused={Application.isFocused}, foreground window: {holder}, active window on this thread: {active}";
        }

        /// <summary>
        /// Brings the player window to the foreground, as a person clicking it would, then records
        /// the state at the start of a test.
        /// <para>
        /// Windows does not give the foreground to a process nobody touched: the player is started
        /// by the editor, which the script started in the background, so its window opens behind
        /// whatever the developer was using. Unity still reports isFocused, since the window is
        /// active on its own thread, but every history call answered NotForeground (2026-09-26,
        /// VS Code held the foreground through the whole run). Attaching to the foreground window's
        /// input for the call is what lets SetForegroundWindow through.
        /// </para>
        /// </summary>
        internal static IEnumerator TakeForegroundAndLog(string testName)
        {
            if (!IsForeground())
            {
                TakeForeground();
                yield return Eventually(IsForeground, _ => { });
            }
            LogFocus(testName);
        }

        internal static bool IsForeground()
        {
            IntPtr foreground = GetForegroundWindow();
            if (foreground == IntPtr.Zero) return false;
            GetWindowThreadProcessId(foreground, out uint pid);
            return pid == (uint)Process.GetCurrentProcess().Id;
        }

        /// <summary>
        /// Minimizes the player window, so that another window has the foreground, as a person
        /// switching to another app does for block B (M-13). Returns the window for
        /// <see cref="ComeBack"/>: once minimized it is no longer the active one.
        /// </summary>
        internal static IEnumerator LeaveForeground(Action<IntPtr> window)
        {
            IntPtr own = GetActiveWindow();
            window(own);
            if (own == IntPtr.Zero) yield break;
            ShowWindow(own, ShowMinimized);
            yield return Eventually(() => !IsForeground(), _ => { });
        }

        /// <summary>Restores the window <see cref="LeaveForeground"/> minimized and takes the foreground back.</summary>
        internal static IEnumerator ComeBack(IntPtr own)
        {
            if (own == IntPtr.Zero) yield break;
            ShowWindow(own, ShowRestored);
            TakeForeground(own);
            yield return Eventually(IsForeground, _ => { });
        }

        private const int ShowMinimized = 6;
        private const int ShowRestored = 9;

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr window, int command);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr window);

        private static void TakeForeground() => TakeForeground(GetActiveWindow());

        private static void TakeForeground(IntPtr own)
        {
            if (own == IntPtr.Zero) return;
            // A test that failed while stepped back (LeaveForeground) leaves the window minimized.
            if (IsIconic(own)) ShowWindow(own, ShowRestored);

            IntPtr foreground = GetForegroundWindow();
            uint foregroundThread = foreground != IntPtr.Zero ? GetWindowThreadProcessId(foreground, out _) : 0;
            uint thisThread = GetCurrentThreadId();
            bool attached = foregroundThread != 0 && foregroundThread != thisThread
                && AttachThreadInput(thisThread, foregroundThread, true);
            try
            {
                BringWindowToTop(own);
                SetForegroundWindow(own);
            }
            finally
            {
                if (attached) AttachThreadInput(thisThread, foregroundThread, false);
            }
        }

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr window);

        [DllImport("user32.dll")]
        private static extern bool BringWindowToTop(IntPtr window);

        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint attach, uint attachTo, bool doAttach);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetActiveWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr window, StringBuilder text, int capacity);

        private static string ProcessName(uint pid)
        {
            try
            {
                using Process process = Process.GetProcessById((int)pid);
                return process.ProcessName;
            }
            catch (Exception e) when (e is ArgumentException or InvalidOperationException or NotSupportedException)
            {
                return "unknown";
            }
        }

        private static string WindowTitle(IntPtr window)
        {
            var title = new StringBuilder(256);
            GetWindowText(window, title, title.Capacity);
            return title.ToString();
        }

        /// <summary>
        /// Records the foreground state at the start of a test, in the result file and in Player.log,
        /// so a run that lost the foreground shows at which test it happened.
        /// </summary>
        internal static void LogFocus(string testName)
        {
            string line = $"[PlayerTests][focus] {testName}: {FocusNote()}";
            TestContext.WriteLine(line);
            UnityEngine.Debug.Log(line);
        }

        internal static IEnumerator LoadAtTopMenu()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync(SampleScene, LoadSceneMode.Additive);
            yield return WaitFor(() => load.isDone, "the sample scene to load", SceneLoadTimeoutSeconds);
            yield return WaitFor(() => FindButton(TopMenuClipboardButton) != null, "the top menu");
        }

        internal static IEnumerator Unload()
        {
            Scene scene = SceneManager.GetSceneByName(SampleScene);
            if (!scene.isLoaded) yield break;
            AsyncOperation unload = SceneManager.UnloadSceneAsync(scene);
            yield return WaitFor(() => unload.isDone, "the sample scene to unload");
        }

        /// <summary>The sample's UIDocument: the one that lives in the sample scene.</summary>
        internal static UIDocument? SampleDocument()
        {
            Scene scene = SceneManager.GetSceneByName(SampleScene);
            if (!scene.isLoaded) return null;
            return UnityEngine.Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None)
                .FirstOrDefault(document => document.gameObject.scene == scene);
        }

        internal static Button? FindButton(string name) => SampleDocument()?.rootVisualElement?.Q<Button>(name);

        internal static void Press(Button button)
        {
            using NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled();
            submit.target = button;
            button.SendEvent(submit);
        }

        internal static WindowsClipboardManagerExampleController? ClipboardController()
        {
            WindowsClipboardManagerExampleController? controller =
                SampleDocument()?.GetComponent<WindowsClipboardManagerExampleController>();
            // Unity's null: a component destroyed this frame still exists as a C# object.
            return controller != null ? controller : null;
        }

        /// <summary>
        /// Polls every frame until <paramref name="condition"/> holds or the timeout passes, and
        /// reports which. Callers assert on the answer themselves: an assertion thrown inside a
        /// nested coroutine is logged as a coroutine exception, not reported as the test's failure.
        /// </summary>
        internal static IEnumerator Eventually(Func<bool> condition, Action<bool> met, float timeoutSeconds = TimeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > deadline)
                {
                    met(false);
                    yield break;
                }
                yield return null;
            }
            met(true);
        }

        internal static IEnumerator WaitFor(Func<bool> condition, string what, float timeoutSeconds = TimeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > deadline)
                {
                    Assert.Fail($"Timed out after {timeoutSeconds}s waiting for {what} ({FocusNote()}).");
                }
                yield return null;
            }
        }
    }
}
#endif
