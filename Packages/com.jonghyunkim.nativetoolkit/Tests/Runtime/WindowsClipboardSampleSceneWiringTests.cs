#nullable enable

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using JonghyunKim.NativeToolkit.Runtime.Clipboard;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// EditMode checks that the Windows clipboard sample's screen and controller agree, without
    /// running on a device.
    /// </summary>
    /// <remarks>
    /// A button name that does not resolve is not loud at runtime: the controller logs once and
    /// carries on, leaving a button that does nothing. On a screen whose only purpose is executing
    /// operations that have never run against the native layer, that reads as a check that passed
    /// when it never happened.
    /// </remarks>
    public sealed class WindowsClipboardSampleSceneWiringTests
    {
        private const string ClipboardUxmlPath =
            "Packages/com.jonghyunkim.nativetoolkit/Runtime/Resources/UI/Windows/Clipboard/WindowsClipboardManagerExample.uxml";
        private const string ClipboardUssPath =
            "Packages/com.jonghyunkim.nativetoolkit/Runtime/Resources/UI/Windows/Clipboard/WindowsClipboardManagerExampleStyle.uss";

        private const string ClipboardResourcesUxmlPath = "UI/Windows/Clipboard/WindowsClipboardManagerExample";
        private const string ClipboardResourcesUssPath = "UI/Windows/Clipboard/WindowsClipboardManagerExampleStyle";
        private const string TopMenuResourcesUxmlPath = "UI/Top/TopMenuExample";

        private const string ControllerSourcePath =
            "Packages/com.jonghyunkim.nativetoolkit/Runtime/UI/Windows/Clipboard/WindowsClipboardManagerExampleController.cs";
        private const string NavigatorSourcePath =
            "Packages/com.jonghyunkim.nativetoolkit/Runtime/UI/Common/NativeToolkitSampleNavigator.cs";
        private const string TopMenuSourcePath =
            "Packages/com.jonghyunkim.nativetoolkit/Runtime/UI/Top/TopMenuExampleController.cs";

        /// <summary>
        /// Elements the screen cannot lose: the three the controller resolves by name, plus the one
        /// note whose absence would leave a reader of the Await section with no warning that a
        /// cancellation there arrives as a result rather than an exception.
        /// </summary>
        private static readonly string[] RequiredLabelNames =
        {
            "ResultTextBlock",
            "StatusTextBlock",
            "StateTextBlock",
            "AwaitCancelLabel",
        };

        private static readonly string[] RequiredFieldNames =
        {
            "CustomFormatNameField",
            "HistoryItemIdField",
        };

        /// <summary>
        /// The button names the controller actually binds, read from the controller itself.
        /// </summary>
        /// <remarks>
        /// A second hand-written list here would defeat the point: a wrong name in the binding
        /// table would match this copy too and every test would stay green.
        /// <para>
        /// The component goes on an <b>inactive</b> GameObject, so Unity runs neither Awake nor
        /// OnEnable. Nothing subscribes and no WindowsClipboardManager is created, which keeps this
        /// inside the EditMode rule against instantiating Managers.
        /// </para>
        /// </remarks>
        private static string[] ReadBoundButtonNames()
        {
            var host = new GameObject("WindowsClipboardWiringProbe") { hideFlags = HideFlags.HideAndDontSave };
            host.SetActive(false);
            try
            {
                var controller = host.AddComponent<WindowsClipboardManagerExampleController>();
                var names = new List<string>();
                foreach ((string name, Action _) in controller.Bindings) names.Add(name);
                return names.ToArray();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ClipboardUxml_ExistsAtResourcesPath()
        {
            Assert.IsNotNull(
                Resources.Load<VisualTreeAsset>(ClipboardResourcesUxmlPath),
                $"VisualTreeAsset not found at Resources path: {ClipboardResourcesUxmlPath}");
        }

        /// <remarks>
        /// The navigator only clears and reapplies style sheets when it finds one. A missing style
        /// sheet leaves the screen wearing the TopMenu's styles, which looks like a layout bug
        /// rather than a missing file.
        /// </remarks>
        [Test]
        public void ClipboardUss_ExistsAtResourcesPath()
        {
            Assert.IsNotNull(
                Resources.Load<StyleSheet>(ClipboardResourcesUssPath),
                $"StyleSheet not found at Resources path: {ClipboardResourcesUssPath}");
        }

        [Test]
        public void ClipboardUxml_ExistsOnDisk()
        {
            Assert.IsNotNull(
                UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(ClipboardUxmlPath),
                ClipboardUxmlPath);
            Assert.IsNotNull(
                UnityEditor.AssetDatabase.LoadAssetAtPath<StyleSheet>(ClipboardUssPath),
                ClipboardUssPath);
        }

        [Test]
        public void ClipboardUxml_ContainsEveryButtonTheControllerBinds()
        {
            VisualElement root = Instantiate(ClipboardResourcesUxmlPath);

            foreach (string name in ReadBoundButtonNames())
            {
                Assert.IsNotNull(root.Q<Button>(name), $"Button not found in UXML: {name}");
            }
        }

        [Test]
        public void ClipboardUxml_ContainsEveryRequiredElement()
        {
            VisualElement root = Instantiate(ClipboardResourcesUxmlPath);

            foreach (string name in RequiredLabelNames)
            {
                Assert.IsNotNull(root.Q<Label>(name), $"Label not found in UXML: {name}");
            }
            foreach (string name in RequiredFieldNames)
            {
                Assert.IsNotNull(root.Q<TextField>(name), $"TextField not found in UXML: {name}");
            }
            Assert.IsNotNull(root.Q<ScrollView>("ResultScrollView"));
        }

        /// <remarks>
        /// An unbound button is worse than a missing one. It is visible, it is pressable, and the
        /// coverage pass records it as exercised.
        /// </remarks>
        [Test]
        public void ClipboardUxml_HasNoButtonTheControllerDoesNotBind()
        {
            VisualElement root = Instantiate(ClipboardResourcesUxmlPath);

            int actual = 0;
            root.Query<Button>().ForEach(_ => actual++);

            Assert.AreEqual(
                ReadBoundButtonNames().Length, actual,
                "an extra button means the plan and the screen have drifted apart");
        }

        [Test]
        public void Controller_BindsExactlyThePlannedNumberOfButtons()
        {
            string[] names = ReadBoundButtonNames();
            Assert.AreEqual(66, names.Length, "the sample plan enumerates 66 buttons");
            CollectionAssert.AllItemsAreUnique(names);
        }

        [Test]
        public void EveryButtonIsBoundToItsOwnHandler()
        {
            // Names alone do not pin the wiring: pointing DeleteLastButton at OnRestoreLastClicked
            // leaves every name check green while the button runs the wrong operation, which on a
            // verification harness reads as a passing check.
            var host = new GameObject("WindowsClipboardHandlerProbe") { hideFlags = HideFlags.HideAndDontSave };
            host.SetActive(false);
            try
            {
                var controller = host.AddComponent<WindowsClipboardManagerExampleController>();
                foreach ((string name, Action handler) in controller.Bindings)
                {
                    string expected = "On" + name.Substring(0, name.Length - "Button".Length) + "Clicked";
                    Assert.AreEqual(
                        expected, handler.Method.Name,
                        $"{name} is bound to the wrong handler");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        /// <remarks>
        /// <para>
        /// The Awaitable forms complete with ErrorCode Canceled rather than throwing, so a
        /// try/catch on OperationCanceledException never runs and the handler carries on past the
        /// await into a screen a destroy may already have taken apart. It compiles, it passes, and
        /// it fails later at a VisualElement, where it looks like a UI defect.
        /// </para>
        /// <para>
        /// Matching the one spelling would have let through catch without the space, the
        /// namespace-qualified form, TaskCanceledException, an exception filter, and a bare catch.
        /// The screen has exactly one catch - the worker-thread guard, whose whole job is to keep a
        /// swallowed exception from stranding the pending count - so the rule is stated as a count.
        /// </para>
        /// </remarks>
        [Test]
        public void TheOnlyCatchIsTheWorkerThreadGuard()
        {
            string source = CodeOnly(File.ReadAllText(Path.GetFullPath(ControllerSourcePath)));

            Assert.AreEqual(
                1, CountOccurrences(source, "catch"),
                "cancellation arrives as a result with ErrorCode.Canceled, never as an exception. " +
                "Check the code and return instead of catching. The one permitted catch is the " +
                "Task.Run guard in RunOnWorker.");
            StringAssert.Contains(
                "catch (Exception exception)", source,
                "the permitted catch is the one in RunOnWorker");
            Assert.AreEqual(
                0, CountOccurrences(source, "OperationCanceledException"),
                "no code here may be written around a cancellation exception");
        }

        /// <remarks>
        /// <para>
        /// Per handler, not by counting. Two totals agreeing says nothing about where either sits:
        /// a seventh await reached through a local variable leaves the await total unchanged, an
        /// added guard elsewhere restores the balance, and deleting one of these handlers keeps
        /// both totals equal while a public method quietly stops being exercised at all.
        /// </para>
        /// <para>
        /// Windows Clipboard is the only feature in the repository with Awaitable methods, so a
        /// handler that disappears here takes the only execution path that method has with it. The
        /// expected count is therefore fixed as well.
        /// </para>
        /// </remarks>
        [Test]
        public void EveryAwaitedHandlerChecksForCancellationAfterItsAwait()
        {
            string source = CodeOnly(File.ReadAllText(Path.GetFullPath(ControllerSourcePath)));
            var checkedHandlers = new List<string>();

            foreach (string handler in AsyncHandlerBodies(source))
            {
                int name = handler.IndexOf("On", StringComparison.Ordinal);
                string label = handler.Substring(name, handler.IndexOf('(', name) - name);
                checkedHandlers.Add(label);

                int await = handler.IndexOf("await ", StringComparison.Ordinal);
                Assert.Greater(await, -1, label + " is declared async but never awaits");

                int guard = handler.IndexOf("if (Cancelled(", await, StringComparison.Ordinal);
                Assert.Greater(
                    guard, -1,
                    label + " must test its result for Canceled after the await. Cancellation " +
                    "arrives as a result, so without this the handler runs on past it and touches " +
                    "elements a destroy may already have removed.");
                StringAssert.Contains(
                    "return;", handler.Substring(guard),
                    label + " must return when the guard reports a cancellation");
            }

            Assert.AreEqual(
                6, checkedHandlers.Count,
                "the six Awaitable buttons are the only place these methods ever run: " +
                string.Join(", ", checkedHandlers));
        }

        /// <summary>
        /// Splits the controller source into the body of every async handler.
        /// </summary>
        /// <param name="source">Controller source.</param>
        /// <returns>One brace-matched body per <c>private async void</c> method.</returns>
        private static List<string> AsyncHandlerBodies(string source)
        {
            var bodies = new List<string>();
            const string marker = "private async void ";
            int at = 0;
            while ((at = source.IndexOf(marker, at, StringComparison.Ordinal)) >= 0)
            {
                int open = source.IndexOf('{', at);
                bodies.Add(source.Substring(at, MatchingBrace(source, open) - at));
                at = open;
            }
            return bodies;
        }

        /// <summary>Finds the brace closing the one at <paramref name="open"/>.</summary>
        /// <param name="source">Source to scan.</param>
        /// <param name="open">Index of an opening brace.</param>
        /// <returns>Index just past the matching closing brace.</returns>
        private static int MatchingBrace(string source, int open)
        {
            int depth = 0;
            for (int i = open; i < source.Length; i++)
            {
                if (source[i] == '{') depth++;
                else if (source[i] == '}' && --depth == 0) return i + 1;
            }
            Assert.Fail("unbalanced braces from index " + open);
            return source.Length;
        }

        /// <remarks>
        /// <para>
        /// A provider runs while the application is shutting down, when this MonoBehaviour and its
        /// elements may already be gone, so it may reach no Unity API at all. The reserve is the
        /// only place this sample hands the engine a callback it does not own.
        /// </para>
        /// <para>
        /// The table is delimited by matching braces rather than by the first <c>};</c>, or a
        /// nested initializer inside a lambda would end the scan early and leave everything after
        /// it unexamined. Requiring both entries to be lambdas closes the other way round the
        /// check: a method group, or a Func built above the table, would put the body somewhere the
        /// scan never looks.
        /// </para>
        /// </remarks>
        [Test]
        public void TheDeferredProvidersTouchNoUnityApi()
        {
            string source = CodeOnly(File.ReadAllText(Path.GetFullPath(ControllerSourcePath)));

            const string marker = "var providers = new Dictionary<string, Func<byte[]>>";
            int start = source.IndexOf(marker, StringComparison.Ordinal);
            Assert.Greater(start, -1, "the reserve handler must build its providers here");

            int open = source.IndexOf('{', start);
            string table = source.Substring(open, MatchingBrace(source, open) - open);

            Assert.AreEqual(
                2, CountOccurrences(table, "() =>"),
                "both providers must be written inline. A method group or a Func built above the " +
                "table moves the body out of everything this test can see.");
            Assert.AreEqual(
                2, CountOccurrences(table, "Interlocked.Increment"),
                "each provider counts its own call, and Interlocked is the only bookkeeping it may do");

            string[] forbidden =
            {
                "Debug.", "AppendResult", "RefreshStatus", "RefreshState", "Instance",
                "Application.", "Time.", "gameObject", "uiDocument", "_result", "_status",
                "_state", "_last", "Local(", "Call(", "Done(",
            };
            foreach (string name in forbidden)
            {
                Assert.IsFalse(
                    table.Contains(name),
                    $"a deferred provider must not reach {name}: it runs during shutdown, when the " +
                    "screen it would touch may already be gone. Count with Interlocked and publish " +
                    "from Update.");
            }
        }

        /// <remarks>
        /// <para>
        /// Without the registration the controller survives a trip back to the TopMenu, keeps its
        /// event subscriptions, and a second visit binds a second copy. Every counter then reads
        /// double and no button responds to the screen the operator is actually looking at.
        /// </para>
        /// <para>
        /// Scoped to the two method bodies. Asserting that the file contains the text
        /// "ShowWindowsClipboard" was satisfied by the method's own declaration, so that check
        /// could not fail; and the removal could be moved to a method nobody calls while the same
        /// assertion stayed green.
        /// </para>
        /// </remarks>
        [Test]
        public void TheNavigatorRemovesThisControllerBeforeSwitchingScreens()
        {
            string source = File.ReadAllText(Path.GetFullPath(NavigatorSourcePath));

            StringAssert.Contains(
                "RemoveIfExists<WindowsClipboardManagerExampleController>",
                MethodBody(source, "private static void RemoveExistingControllers"),
                "the removal must be inside RemoveExistingControllers, which is what ApplyScreen " +
                "calls, or a second visit subscribes twice");
        }

        /// <remarks>
        /// The screen is loaded from Resources by path. A typo there is invisible to every other
        /// test here, which loads its own copy of the string, and shows up only as a TopMenu button
        /// that opens nothing. This is the same hole the missing style sheet went through.
        /// </remarks>
        [Test]
        public void TheNavigatorLoadsTheScreenThisTestSuiteChecks()
        {
            string body = MethodBody(
                File.ReadAllText(Path.GetFullPath(NavigatorSourcePath)),
                "public static void ShowWindowsClipboard");

            StringAssert.Contains(
                "ApplyScreen<WindowsClipboardManagerExampleController>", body,
                "the entry point must add this controller");
            StringAssert.Contains(
                "\"" + ClipboardResourcesUxmlPath + "\"", body,
                "the navigator must load the UXML this suite verifies, not another path");
            StringAssert.Contains(
                "\"" + ClipboardResourcesUssPath + "\"", body,
                "the navigator must load the USS this suite verifies, not another path");
        }

        /// <summary>Returns the brace-matched body of a method, found by its declaration.</summary>
        /// <param name="source">Source to search.</param>
        /// <param name="declaration">Text that begins the declaration.</param>
        /// <returns>The declaration and its body.</returns>
        private static string MethodBody(string source, string declaration)
        {
            int at = source.IndexOf(declaration, StringComparison.Ordinal);
            Assert.Greater(at, -1, declaration + " must exist");
            int open = source.IndexOf('{', at);
            return source.Substring(at, MatchingBrace(source, open) - at);
        }

        /// <remarks>
        /// The TopMenu reaches each platform through its own elif. Subscribing the button without
        /// adding the branch leaves it pressable on a Windows player and silent when pressed.
        /// </remarks>
        [Test]
        public void TheTopMenuRoutesWindowsToThisScreen()
        {
            string source = File.ReadAllText(Path.GetFullPath(TopMenuSourcePath));

            StringAssert.Contains(
                "NativeToolkitSampleNavigator.ShowWindowsClipboard(uiDocument);",
                source,
                "OnClipboardClicked needs a UNITY_STANDALONE_WIN branch");

            int subscribe = source.IndexOf("_clipboardButton.clicked += OnClipboardClicked;", StringComparison.Ordinal);
            Assert.Greater(subscribe, -1, "the clipboard button must be subscribed");
            string guard = source.Substring(0, subscribe);
            int lastGuard = guard.LastIndexOf("#if ", StringComparison.Ordinal);
            Assert.Greater(lastGuard, -1);
            StringAssert.Contains(
                "UNITY_STANDALONE_WIN",
                guard.Substring(lastGuard, guard.Length - lastGuard),
                "the subscription guard must let Windows through, or the button is hidden there");
        }

        /// <remarks>
        /// Deleting one line from OnDisable left all 43 tests green. The symptom is that the
        /// screen's counters read double after a trip to the TopMenu and back - which is exactly
        /// what the manual checks about re-entry are looking for, so the harness would be
        /// producing the failure it is meant to detect.
        /// <para>
        /// Compared by event name, not by count, so swapping one name for another cannot balance.
        /// The total is held against the Manager's public events, so a new event that nobody
        /// subscribes to is reported here rather than discovered on a device.
        /// </para>
        /// </remarks>
        [Test]
        public void EverySubscribedEventIsUnsubscribed()
        {
            string source = CodeOnly(File.ReadAllText(Path.GetFullPath(ControllerSourcePath)));

            List<string> subscribed = EventNames(MethodBody(source, "private void OnEnable"), "+=");
            List<string> unsubscribed = EventNames(MethodBody(source, "private void OnDisable"), "-=");

            CollectionAssert.AreEquivalent(
                subscribed, unsubscribed,
                "OnEnable and OnDisable must name the same events. A subscription left behind " +
                "doubles that event's counter on the next visit.");

            int declared = typeof(WindowsClipboardManager).GetEvents(
                BindingFlags.Public | BindingFlags.Instance).Length;
            Assert.AreEqual(
                declared, subscribed.Count,
                "the screen observes every event the Manager publishes; a new one is not optional " +
                "here, because an event nobody watches cannot be checked on a device");
        }

        /// <summary>Collects the event names bound with a given operator in a method body.</summary>
        /// <param name="body">Method body.</param>
        /// <param name="op">Either "+=" or "-=".</param>
        /// <returns>The event names, in source order.</returns>
        private static List<string> EventNames(string body, string op)
        {
            var names = new List<string>();
            foreach (string raw in body.Split('\n'))
            {
                string line = raw.Trim();
                if (!line.StartsWith("manager.", StringComparison.Ordinal)) continue;
                int at = line.IndexOf(" " + op + " ", StringComparison.Ordinal);
                if (at < 0) continue;
                names.Add(line.Substring("manager.".Length, at - "manager.".Length));
            }
            return names;
        }

        [Test]
        public void TopMenu_StillExposesTheClipboardEntryPoint()
        {
            VisualElement root = Instantiate(TopMenuResourcesUxmlPath);

            Assert.IsNotNull(
                root.Q<Button>("ClipboardFeatureButton"),
                "the Windows clipboard sample is reached through this button");
        }

        /// <summary>
        /// Drops comments, so a rule about what the code does is not tripped by prose describing it.
        /// </summary>
        /// <param name="source">C# source.</param>
        /// <returns>The same source with line and documentation comments removed.</returns>
        /// <remarks>
        /// These checks are worth having only if the explanation can sit beside the thing being
        /// checked. Banning the word "OperationCanceledException" outright banned the comment that
        /// tells the next reader why nothing catches it.
        /// <para>
        /// A "//" inside a string literal is left alone by counting the quotes before it. No block
        /// comments are used in this file's subjects; one would survive this and is not swept.
        /// </para>
        /// </remarks>
        private static string CodeOnly(string source)
        {
            var kept = new List<string>();
            foreach (string line in source.Split('\n'))
            {
                int at = line.IndexOf("//", StringComparison.Ordinal);
                if (at < 0)
                {
                    kept.Add(line);
                    continue;
                }

                int quotes = 0;
                for (int i = 0; i < at; i++)
                {
                    if (line[i] == '"') quotes++;
                }
                kept.Add(quotes % 2 == 0 ? line.Substring(0, at) : line);
            }
            return string.Join("\n", kept);
        }

        private static int CountOccurrences(string source, string needle)
        {
            int count = 0;
            int at = 0;
            while ((at = source.IndexOf(needle, at, StringComparison.Ordinal)) >= 0)
            {
                count++;
                at += needle.Length;
            }
            return count;
        }

        private static VisualElement Instantiate(string resourcesPath)
        {
            var asset = Resources.Load<VisualTreeAsset>(resourcesPath);
            Assert.IsNotNull(asset, resourcesPath);
            return asset.CloneTree();
        }
    }
}
#endif
