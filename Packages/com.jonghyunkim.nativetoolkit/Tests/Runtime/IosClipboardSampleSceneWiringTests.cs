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
    public sealed class IosClipboardSampleSceneWiringTests
    {
        private const string ClipboardUxmlPath = "Packages/com.jonghyunkim.nativetoolkit/Runtime/Resources/UI/iOS/Clipboard/IosClipboardManagerExample.uxml";
        private const string ClipboardUssPath = "Packages/com.jonghyunkim.nativetoolkit/Runtime/Resources/UI/iOS/Clipboard/IosClipboardManagerExampleStyle.uss";
        private const string TopMenuUxmlPath = "Packages/com.jonghyunkim.nativetoolkit/Runtime/Resources/UI/Top/TopMenuExample.uxml";

        private const string ClipboardResourcesUxmlPath = "UI/iOS/Clipboard/IosClipboardManagerExample";
        private const string ClipboardResourcesUssPath = "UI/iOS/Clipboard/IosClipboardManagerExampleStyle";
        private const string TopMenuResourcesUxmlPath = "UI/Top/TopMenuExample";

        private static readonly string[] RequiredClipboardButtonNames =
        {
            "HomeButton",
            // Scope
            "UseGeneralButton",
            "CreateNamedPasteboardButton",
            "UseFixedNamedScopeButton",
            "CreateUniquePasteboardButton",
            "RemoveActivePasteboardButton",
            "ProbeRemovedScopeButton",
            // Copy
            "CopyPlainTextButton",
            "CopyEmptyPlainTextButton",
            "CopyHtmlTextButton",
            "CopyUrlButton",
            "CopyImageFileButton",
            "CopyImageDataButton",
            "CopyColorButton",
            "CopyCustomDataButton",
            "CopyMultipleTextButton",
            "CopyMultiRepresentationButton",
            "CopyDetectionFixtureButton",
            // Copy options
            "CopyLocalOnlyTrueButton",
            "CopyLocalOnlyFalseButton",
            "CopyDeviceBaselineButton",
            "CopyExpiringButton",
            // Append
            "AppendPlainTextButton",
            "AppendUrlButton",
            // Read
            "ReadButton",
            "ReadDataPngButton",
            "SnapshotButton",
            "SnapshotMatchingButton",
            // Load
            "LoadTextButton",
            "LoadUrlButton",
            "LoadImageButton",
            "LoadFileButton",
            "LoadFileCustomButton",
            "CancelLoadsButton",
            // Detect
            "CopyNumberFixtureButton",
            "CopySearchFixtureButton",
            "DetectPatternsButton",
            "DetectValuesButton",
            // Observe
            "StartObservingButton",
            "RestartObservingButton",
            "StopObservingButton",
            "CheckForegroundChangeButton",
            // Clear
            "ClearActiveScopeButton",
            // Busy / memory
            "BusyLoadItemTwiceButton",
            "SeedAndCancelLoadButton",
            "BusyStartObservingTwiceButton",
            "CopyLargeImageDataButton",
            // Errors
            "ErrCopyMultipleEmptyButton",
            "ErrCopyMultiRepEmptyButton",
            "ErrCopyImageFileMissingButton",
            "ErrCopyInvalidUtiButton",
            "ErrCopyInvalidUrlButton",
            "ErrCopyColorOutOfRangeButton",
            "ErrReadDataInvalidUtiButton",
            "ErrRemoveGeneralButton",
            "ErrObserveMissingNamedButton",
            "ErrDetectEmptyPatternsButton"
        };

        private static readonly string[] RequiredClipboardLabelNames =
        {
            "ResultTextBlock",
            "StatusTextBlock",
            // Both notes carry the fixed wording that explains why C# argument errors have no
            // buttons; renaming or dropping them would silently remove that explanation.
            "SubtitleValidationLabel",
            "ErrorsSectionNote"
        };

        [Test]
        public void ClipboardUxml_ExistsAtResourcesPath()
        {
            var asset = Resources.Load<VisualTreeAsset>(ClipboardResourcesUxmlPath);
            Assert.IsNotNull(asset, $"VisualTreeAsset not found at Resources path: {ClipboardResourcesUxmlPath}");
        }

        [Test]
        public void ClipboardUss_ExistsAtResourcesPath()
        {
            var asset = Resources.Load<StyleSheet>(ClipboardResourcesUssPath);
            Assert.IsNotNull(asset, $"StyleSheet not found at Resources path: {ClipboardResourcesUssPath}");
        }

        [Test]
        public void ClipboardUss_ExistsAtAssetDatabasePath()
        {
            var asset = AssetDatabase.LoadAssetAtPath<StyleSheet>(ClipboardUssPath);
            if (asset == null)
            {
                Assert.Ignore($"USS not found at AssetDatabase path (run from project root): {ClipboardUssPath}");
                return;
            }

            Assert.IsNotNull(asset);
        }

        [Test]
        public void ClipboardUxml_ContainsAllRequiredButtons()
        {
            var asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(ClipboardUxmlPath);
            if (asset == null)
            {
                Assert.Ignore($"UXML not found at AssetDatabase path (run from project root): {ClipboardUxmlPath}");
                return;
            }

            var root = asset.Instantiate();
            foreach (string name in RequiredClipboardButtonNames)
            {
                var button = root.Q<Button>(name);
                Assert.IsNotNull(button, $"Button '{name}' not found in IosClipboardManagerExample.uxml");
            }
        }

        [Test]
        public void ClipboardUxml_ContainsAllRequiredLabels()
        {
            var asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(ClipboardUxmlPath);
            if (asset == null)
            {
                Assert.Ignore($"UXML not found at AssetDatabase path: {ClipboardUxmlPath}");
                return;
            }

            var root = asset.Instantiate();
            foreach (string name in RequiredClipboardLabelNames)
            {
                var label = root.Q<Label>(name);
                Assert.IsNotNull(label, $"Label '{name}' not found in IosClipboardManagerExample.uxml");
            }
        }

        /// <summary>
        /// The controller resets this ScrollView's offset on every result, so a rename would
        /// silently leave new results scrolled to the previous position.
        /// </summary>
        [Test]
        public void ClipboardUxml_ContainsResultScrollView()
        {
            var asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(ClipboardUxmlPath);
            if (asset == null)
            {
                Assert.Ignore($"UXML not found at AssetDatabase path: {ClipboardUxmlPath}");
                return;
            }

            var root = asset.Instantiate();
            var scrollView = root.Q<ScrollView>("ResultScrollView");
            Assert.IsNotNull(scrollView, "ScrollView 'ResultScrollView' not found in IosClipboardManagerExample.uxml");
            Assert.IsNotNull(
                scrollView.Q<Label>("ResultTextBlock"),
                "Label 'ResultTextBlock' must live inside 'ResultScrollView' for the fixed-height result area to scroll.");
        }

        [Test]
        public void TopMenuUxml_ContainsClipboardFeatureButton()
        {
            var asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(TopMenuUxmlPath);
            if (asset == null)
            {
                Assert.Ignore($"UXML not found at AssetDatabase path: {TopMenuUxmlPath}");
                return;
            }

            var root = asset.Instantiate();
            var button = root.Q<Button>("ClipboardFeatureButton");
            Assert.IsNotNull(button, "Button 'ClipboardFeatureButton' not found in TopMenuExample.uxml");
        }
    }
}
#endif
