#nullable enable

// Windows player only, like WindowsClipboardPlayerTests. In the Editor the top menu answers the
// clipboard button with an editor dialog instead of opening the screen (TopMenuExampleController),
// and the manager behind the screen never reaches the native layer.
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JonghyunKim.NativeToolkit.Runtime.Clipboard;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// Layer 2b tests that drive the Windows clipboard sample's own screens on a player: the manual
    /// checks S-1 (the top menu opens the clipboard screen) and S-5 / S-6 (leaving and re-entering
    /// the screen tears each one down and leaves exactly one subscription behind).
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
    public sealed class WindowsClipboardSampleScreenPlayerTests
    {
        private const string SampleScene = "NativeToolkitExampleScene";
        private const float TimeoutSeconds = 5f;

        private const string TopMenuClipboardButton = "ClipboardFeatureButton";
        private const string ClipboardHomeButton = "HomeButton";
        private const string ControllerLogTag = "[WindowsClipboardManagerExampleController]";

        private readonly List<string> _log = new();

        [UnitySetUp]
        public IEnumerator LoadTheSample()
        {
            _log.Clear();
            Application.logMessageReceived += Record;

            AsyncOperation load = SceneManager.LoadSceneAsync(SampleScene, LoadSceneMode.Additive);
            yield return WaitFor(() => load.isDone, "the sample scene to load");
            yield return WaitFor(() => FindButton(TopMenuClipboardButton) != null, "the top menu");
        }

        [UnityTearDown]
        public IEnumerator UnloadTheSample()
        {
            Application.logMessageReceived -= Record;

            Scene scene = SceneManager.GetSceneByName(SampleScene);
            if (scene.isLoaded)
            {
                AsyncOperation unload = SceneManager.UnloadSceneAsync(scene);
                yield return WaitFor(() => unload.isDone, "the sample scene to unload");
            }

            // This class may be the last to run; the layer 3 check after the run expects the sample.
            WindowsClipboardPlayerTests.LeaveSampleOnClipboard();
        }

        // ── S-1 ──────────────────────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator TopMenu_ClipboardButton_OpensTheClipboardScreen()
        {
            bool opened = false;
            yield return OpenClipboardScreen(done => opened = done);

            Assert.IsTrue(opened, $"the clipboard screen did not open within {TimeoutSeconds}s");
            Assert.IsNotNull(ClipboardController(), "no WindowsClipboardManagerExampleController on the sample's UIDocument");
            Assert.IsNotNull(FindButton(ClipboardHomeButton), $"the clipboard screen has no {ClipboardHomeButton}");
        }

        // ── S-5 / S-6 ────────────────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator LeavingAndReenteringTwice_TearsEachScreenDown_AndLeavesOneSubscription()
        {
            for (int round = 1; round <= 2; round++)
            {
                bool opened = false;
                yield return OpenClipboardScreen(done => opened = done);
                Assert.IsTrue(opened, $"round {round}: the clipboard screen did not open");

                bool left = false;
                yield return GoHome(done => left = done);
                Assert.IsTrue(left, $"round {round}: the top menu did not come back");
            }

            bool reopened = false;
            yield return OpenClipboardScreen(done => reopened = done);
            Assert.IsTrue(reopened, "the clipboard screen did not open a third time");

            // S-5: every exit disables the screen's controller and then destroys it, in that order.
            string[] lifecycle = _log
                .Where(line => line.StartsWith(ControllerLogTag, StringComparison.Ordinal))
                .Select(line => line.Substring(ControllerLogTag.Length))
                .Where(rest => rest.StartsWith("[OnEnable]") || rest.StartsWith("[OnDisable]") || rest.StartsWith("[OnDestroy]"))
                .Select(rest => rest.Substring(1, rest.IndexOf(']') - 1))
                .ToArray();
            string[] expected = { "OnEnable", "OnDisable", "OnDestroy", "OnEnable", "OnDisable", "OnDestroy", "OnEnable" };
            Assert.AreEqual(expected, lifecycle, "controller lifecycle across two exits and three entries");

            // S-6: after all that, every manager event carries exactly one handler from a screen
            // controller, and it belongs to the screen now showing. A controller that failed to
            // unsubscribe would leave a destroyed target behind; one that subscribed twice, two.
            WindowsClipboardManagerExampleController? live = ClipboardController();
            Assert.IsNotNull(live, "no live clipboard screen controller");

            foreach (EventInfo managerEvent in typeof(WindowsClipboardManager).GetEvents(BindingFlags.Instance | BindingFlags.Public))
            {
                object[] targets = ControllerHandlers(managerEvent);
                Assert.AreEqual(1, targets.Length, $"{managerEvent.Name}: screen controller handlers");
                Assert.AreSame(live, targets[0], $"{managerEvent.Name}: the handler belongs to a screen no longer showing");
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private void Record(string message, string stackTrace, LogType type) => _log.Add(message);

        private static IEnumerator OpenClipboardScreen(Action<bool> done)
        {
            Button? button = FindButton(TopMenuClipboardButton);
            Assert.IsNotNull(button, $"the top menu has no {TopMenuClipboardButton}");
            Press(button!);
            yield return Eventually(() => ClipboardController() != null && FindButton(ClipboardHomeButton) != null, done);
        }

        private static IEnumerator GoHome(Action<bool> done)
        {
            Button? button = FindButton(ClipboardHomeButton);
            Assert.IsNotNull(button, $"the clipboard screen has no {ClipboardHomeButton}");
            Press(button!);
            // Destroy runs at the end of the frame, so the controller lingers for one.
            yield return Eventually(() => ClipboardController() == null && FindButton(TopMenuClipboardButton) != null, done);
        }

        private static void Press(Button button)
        {
            using NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled();
            submit.target = button;
            button.SendEvent(submit);
        }

        /// <summary>The sample's UIDocument: the one that lives in the sample scene.</summary>
        private static UIDocument? SampleDocument()
        {
            Scene scene = SceneManager.GetSceneByName(SampleScene);
            if (!scene.isLoaded) return null;
            return UnityEngine.Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None)
                .FirstOrDefault(document => document.gameObject.scene == scene);
        }

        private static Button? FindButton(string name) => SampleDocument()?.rootVisualElement?.Q<Button>(name);

        private static WindowsClipboardManagerExampleController? ClipboardController()
        {
            WindowsClipboardManagerExampleController? controller =
                SampleDocument()?.GetComponent<WindowsClipboardManagerExampleController>();
            // Unity's null: a component destroyed this frame still exists as a C# object.
            return controller != null ? controller : null;
        }

        /// <summary>
        /// The targets of a manager event's handlers that are screen controllers. A field-like event is
        /// backed by a private field of the same name; reading it is the only way to count
        /// subscriptions from outside, and it needs no test hook in the manager.
        /// </summary>
        private static object[] ControllerHandlers(EventInfo managerEvent)
        {
            FieldInfo? field = typeof(WindowsClipboardManager).GetField(
                managerEvent.Name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"{managerEvent.Name} is not a field-like event any more; this check needs rethinking");

            var handlers = field!.GetValue(WindowsClipboardManager.Instance) as Delegate;
            return (handlers?.GetInvocationList() ?? Array.Empty<Delegate>())
                .Select(handler => handler.Target)
                .Where(target => target is WindowsClipboardManagerExampleController)
                .ToArray()!;
        }

        private static IEnumerator Eventually(Func<bool> condition, Action<bool> met)
        {
            float deadline = Time.realtimeSinceStartup + TimeoutSeconds;
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

        private static IEnumerator WaitFor(Func<bool> condition, string what)
        {
            float deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > deadline)
                {
                    Assert.Fail($"Timed out after {TimeoutSeconds}s waiting for {what}.");
                }
                yield return null;
            }
        }
    }
}
#endif
