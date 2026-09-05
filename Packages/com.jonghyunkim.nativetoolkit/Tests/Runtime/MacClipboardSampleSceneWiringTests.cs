#nullable enable

#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// Lightweight EditMode tests to catch UXML/Controller wiring mismatches without running on a
    /// device. Verifies Resources paths, required element names, and the TopMenu entry point.
    /// </summary>
    /// <remarks>
    /// A missing button name only surfaces as a silent no-op at runtime: the controller logs and
    /// carries on. On a screen whose whole purpose is driving a manual pass, that would look like
    /// a passing check that was never actually run.
    /// </remarks>
    public sealed class MacClipboardSampleSceneWiringTests
    {
        private const string ClipboardUxmlPath =
            "Packages/com.jonghyunkim.nativetoolkit/Runtime/Resources/UI/macOS/Clipboard/MacClipboardManagerExample.uxml";
        private const string ClipboardUssPath =
            "Packages/com.jonghyunkim.nativetoolkit/Runtime/Resources/UI/macOS/Clipboard/MacClipboardManagerExampleStyle.uss";

        private const string ClipboardResourcesUxmlPath = "UI/macOS/Clipboard/MacClipboardManagerExample";
        private const string ClipboardResourcesUssPath = "UI/macOS/Clipboard/MacClipboardManagerExampleStyle";
        private const string TopMenuResourcesUxmlPath = "UI/Top/TopMenuExample";

        /// <summary>
        /// The button names the controller actually binds, read from the controller itself.
        /// </summary>
        /// <remarks>
        /// A second, hand-written list here would defeat the point: a wrong name in the binding
        /// table would still match this copy and every test would stay green while the button
        /// quietly stopped working.
        /// <para>
        /// The component is added to an <b>inactive</b> GameObject, so Unity runs neither Awake
        /// nor OnEnable. Nothing subscribes, and no MacClipboardManager is created, which keeps
        /// this within the EditMode rule against instantiating Managers.
        /// </para>
        /// </remarks>
        private static string[] ReadBoundButtonNames()
        {
            var host = new GameObject("MacClipboardWiringProbe") { hideFlags = HideFlags.HideAndDontSave };
            host.SetActive(false);
            try
            {
                var controller = host.AddComponent<MacClipboardManagerExampleController>();
                var names = new List<string>();
                foreach ((string name, System.Action _) in controller.Bindings) names.Add(name);
                return names.ToArray();
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        private static readonly string[] RequiredLabelNames =
        {
            "ResultTextBlock",
            "StatusTextBlock",
            "SubtitleValidationLabel"
        };

        [Test]
        public void ClipboardUxml_ExistsAtResourcesPath()
        {
            Assert.IsNotNull(
                Resources.Load<VisualTreeAsset>(ClipboardResourcesUxmlPath),
                $"VisualTreeAsset not found at Resources path: {ClipboardResourcesUxmlPath}");
        }

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
        public void ClipboardUxml_ContainsEveryRequiredLabel()
        {
            VisualElement root = Instantiate(ClipboardResourcesUxmlPath);

            foreach (string name in RequiredLabelNames)
            {
                Assert.IsNotNull(root.Q<Label>(name), $"Label not found in UXML: {name}");
            }
            Assert.IsNotNull(root.Q<ScrollView>("ResultScrollView"));
        }

        [Test]
        public void ClipboardUxml_ButtonCountMatchesThePlan()
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
            // BoundButtonNames is now the only list, so its length is what pins the screen to the
            // 43 buttons the sample plan enumerates.
            string[] names = ReadBoundButtonNames();
            Assert.AreEqual(43, names.Length, "the sample plan enumerates 43 buttons");
            CollectionAssert.AllItemsAreUnique(names);
        }

        [Test]
        public void TopMenu_StillExposesTheClipboardEntryPoint()
        {
            VisualElement root = Instantiate(TopMenuResourcesUxmlPath);

            Assert.IsNotNull(
                root.Q<Button>("ClipboardFeatureButton"),
                "the macOS clipboard sample is reached through this button");
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
