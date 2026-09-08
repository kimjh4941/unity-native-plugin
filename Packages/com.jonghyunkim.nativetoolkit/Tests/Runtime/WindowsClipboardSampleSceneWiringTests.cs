#nullable enable

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
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

        /// <summary>The screen's own elements, none of which the controller can do without.</summary>
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
        /// The Awaitable forms complete with ErrorCode Canceled rather than throwing, so a
        /// try/catch on OperationCanceledException never runs and the handler carries on past the
        /// await into a screen a destroy may already have taken apart. It compiles, it passes, and
        /// it fails later at a VisualElement, where it looks like a UI defect.
        /// </remarks>
        [Test]
        public void NoAwaitSiteCatchesOperationCanceledException()
        {
            string source = File.ReadAllText(Path.GetFullPath(ControllerSourcePath));

            int at = source.IndexOf("catch (OperationCanceledException", StringComparison.Ordinal);
            Assert.AreEqual(
                -1, at,
                "cancellation arrives as a result with ErrorCode.Canceled, never as an exception. " +
                "Check the code and return instead of catching.");
        }

        /// <remarks>
        /// Every awaited handler must consult the Canceled guard. Counting is enough: each of the
        /// six await sites calls it exactly once, so a new one added without the check moves the
        /// two numbers apart.
        /// </remarks>
        [Test]
        public void EveryAwaitedHandlerChecksForCancellation()
        {
            string source = File.ReadAllText(Path.GetFullPath(ControllerSourcePath));

            int awaits = CountOccurrences(source, "await WindowsClipboardManager.Instance");
            int guards = CountOccurrences(source, "if (Cancelled(call,");

            Assert.Greater(awaits, 0, "the sample must exercise the Awaitable forms");
            Assert.AreEqual(
                awaits, guards,
                "every await site must test the result for Canceled and return; continuing past a " +
                "cancellation touches elements the destroy has already removed");
        }

        /// <remarks>
        /// The provider runs on the native window's thread and keeps running while the application
        /// shuts down, which is exactly when the deferred check is looking. A Unity call from
        /// inside one is undefined at that moment, and the reserve is the only place this sample
        /// hands out a callback the engine does not own.
        /// </remarks>
        [Test]
        public void TheDeferredProvidersTouchNoUnityApi()
        {
            string source = File.ReadAllText(Path.GetFullPath(ControllerSourcePath));

            const string marker = "var providers = new Dictionary<string, Func<byte[]>>";
            int start = source.IndexOf(marker, StringComparison.Ordinal);
            Assert.Greater(start, -1, "the reserve handler must build its providers here");

            int end = source.IndexOf("};", start, StringComparison.Ordinal);
            Assert.Greater(end, start, "the provider table must be closed");

            string table = source.Substring(start, end - start);
            foreach (string forbidden in new[] { "Debug.", "AppendResult", "_result", "RefreshStatus", "Instance" })
            {
                Assert.IsFalse(
                    table.Contains(forbidden),
                    $"a deferred provider must not reach {forbidden}: it runs off the main thread " +
                    "and during shutdown. Count with Interlocked and display it from Update.");
            }
        }

        /// <remarks>
        /// Without the registration the controller survives a trip back to the TopMenu, keeps its
        /// event subscriptions, and a second visit binds a second copy. Every counter then reads
        /// double and no button responds to the screen the operator is actually looking at.
        /// </remarks>
        [Test]
        public void TheNavigatorRemovesThisControllerBeforeSwitchingScreens()
        {
            string source = File.ReadAllText(Path.GetFullPath(NavigatorSourcePath));

            StringAssert.Contains(
                "RemoveIfExists<WindowsClipboardManagerExampleController>",
                source,
                "RemoveExistingControllers must drop this controller, or a second visit subscribes twice");
            StringAssert.Contains("ShowWindowsClipboard", source, "the navigator needs an entry point");
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

        [Test]
        public void TopMenu_StillExposesTheClipboardEntryPoint()
        {
            VisualElement root = Instantiate(TopMenuResourcesUxmlPath);

            Assert.IsNotNull(
                root.Q<Button>("ClipboardFeatureButton"),
                "the Windows clipboard sample is reached through this button");
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
