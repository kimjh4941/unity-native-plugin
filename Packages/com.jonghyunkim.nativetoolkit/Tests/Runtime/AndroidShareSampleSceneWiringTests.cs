#nullable enable

#if UNITY_EDITOR
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// Lightweight EditMode tests to catch UXML/Controller wiring mismatches without running on device.
    /// Verifies Resources paths, required element names, and TopMenu button existence.
    /// </summary>
    public sealed class AndroidShareSampleSceneWiringTests
    {
        private const string ShareUxmlPath = "Packages/com.jonghyunkim.nativetoolkit/Runtime/Resources/UI/Android/Share/AndroidShareManagerExample.uxml";
        private const string ShareUssPath = "Packages/com.jonghyunkim.nativetoolkit/Runtime/Resources/UI/Android/Share/AndroidShareManagerExampleStyle.uss";
        private const string TopMenuUxmlPath = "Packages/com.jonghyunkim.nativetoolkit/Runtime/Resources/UI/Top/TopMenuExample.uxml";

        private const string ShareResourcesUxmlPath = "UI/Android/Share/AndroidShareManagerExample";
        private const string ShareResourcesUssPath = "UI/Android/Share/AndroidShareManagerExampleStyle";
        private const string TopMenuResourcesUxmlPath = "UI/Top/TopMenuExample";

        private static readonly string[] RequiredShareButtonNames =
        {
            "HomeButton",
            "ShareTextButton",
            "ShareUrlButton",
            "ShareCustomActionButton",
            "ShareWithSubjectTitleButton",
            "ShareRichPreviewButton",
            "ShareImageButton",
            "ShareImagesButton",
            "ShareFileButton",
            "ShareFilesButton",
            "RegisterDirectShareTargetButton",
            "RemoveDirectShareTargetButton",
            "ShareWithCallbackButton",
            "CancelPendingCallbackButton",
            "ShareInvalidFileButton"
        };

        private static readonly string[] RequiredShareLabelNames =
        {
            "ResultTextBlock"
        };

        [Test]
        public void ShareUxml_ExistsAtResourcesPath()
        {
            var asset = Resources.Load<VisualTreeAsset>(ShareResourcesUxmlPath);
            Assert.IsNotNull(asset, $"VisualTreeAsset not found at Resources path: {ShareResourcesUxmlPath}");
        }

        [Test]
        public void ShareUss_ExistsAtResourcesPath()
        {
            var asset = Resources.Load<StyleSheet>(ShareResourcesUssPath);
            Assert.IsNotNull(asset, $"StyleSheet not found at Resources path: {ShareResourcesUssPath}");
        }

        [Test]
        public void TopMenuUxml_ExistsAtResourcesPath()
        {
            var asset = Resources.Load<VisualTreeAsset>(TopMenuResourcesUxmlPath);
            Assert.IsNotNull(asset, $"VisualTreeAsset not found at Resources path: {TopMenuResourcesUxmlPath}");
        }

        [Test]
        public void ShareUxml_ContainsAllRequiredButtons()
        {
            var asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(ShareUxmlPath);
            if (asset == null)
            {
                Assert.Ignore($"UXML not found at AssetDatabase path (run from project root): {ShareUxmlPath}");
                return;
            }

            var root = asset.Instantiate();
            foreach (string name in RequiredShareButtonNames)
            {
                var button = root.Q<Button>(name);
                Assert.IsNotNull(button, $"Button '{name}' not found in AndroidShareManagerExample.uxml");
            }
        }

        [Test]
        public void ShareUxml_ContainsAllRequiredLabels()
        {
            var asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(ShareUxmlPath);
            if (asset == null)
            {
                Assert.Ignore($"UXML not found at AssetDatabase path: {ShareUxmlPath}");
                return;
            }

            var root = asset.Instantiate();
            foreach (string name in RequiredShareLabelNames)
            {
                var label = root.Q<Label>(name);
                Assert.IsNotNull(label, $"Label '{name}' not found in AndroidShareManagerExample.uxml");
            }
        }

        [Test]
        public void TopMenuUxml_ContainsShareFeatureButton()
        {
            var asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(TopMenuUxmlPath);
            if (asset == null)
            {
                Assert.Ignore($"UXML not found at AssetDatabase path: {TopMenuUxmlPath}");
                return;
            }

            var root = asset.Instantiate();
            var button = root.Q<Button>("ShareFeatureButton");
            Assert.IsNotNull(button, "Button 'ShareFeatureButton' not found in TopMenuExample.uxml");
        }
    }
}
#endif
