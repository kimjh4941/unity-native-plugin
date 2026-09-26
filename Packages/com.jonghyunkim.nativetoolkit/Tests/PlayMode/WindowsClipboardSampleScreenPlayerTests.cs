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
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static JonghyunKim.NativeToolkit.Tests.WindowsClipboardSampleScreenDriver;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// Layer 2b tests that drive the Windows clipboard sample's own screens on a player: the manual
    /// checks S-1 (the top menu opens the clipboard screen) and S-5 / S-6 (leaving and re-entering
    /// the screen tears each one down and leaves exactly one subscription behind).
    /// <para>
    /// How the sample is loaded and its buttons pressed is in
    /// <see cref="WindowsClipboardSampleScreenDriver"/>.
    /// </para>
    /// </summary>
    public sealed class WindowsClipboardSampleScreenPlayerTests
    {
        private const string ControllerLogTag = "[WindowsClipboardManagerExampleController]";

        private readonly List<string> _log = new();

        [UnitySetUp]
        public IEnumerator LoadTheSample()
        {
            _log.Clear();
            yield return TakeForegroundAndLog(TestContext.CurrentContext.Test.Name);
            Application.logMessageReceived += Record;
            yield return LoadAtTopMenu();
        }

        [UnityTearDown]
        public IEnumerator UnloadTheSample()
        {
            Application.logMessageReceived -= Record;
            yield return Unload();

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
    }
}
#endif
