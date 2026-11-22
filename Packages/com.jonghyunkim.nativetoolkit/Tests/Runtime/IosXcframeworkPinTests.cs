#nullable enable

using System;
using System.IO;
using NUnit.Framework;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.TestTools;
using JonghyunKim.NativeToolkit.Editor.iOS;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// Layer 1 tests for <see cref="IosXcframeworkPin"/>: how Plugins/iOS/VERSION.txt is read, and
    /// that the build steps pick XCFrameworks by exact name.
    /// </summary>
    public sealed class IosXcframeworkPinTests
    {
        private string tempDir = string.Empty;

        [SetUp]
        public void SetUp()
        {
            tempDir = Path.Combine(Path.GetTempPath(), "IosXcframeworkPinTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }

        [Test]
        public void Read_TakesTheVersionAndTheNamesInOrder()
        {
            IosXcframeworkPin pin = IosXcframeworkPin.Read(WritePin(
                "# comment",
                "dist_version: 1.14.0",
                "xcframeworks: ios-native-toolkit-2.0.0.xcframework , ios-native-toolkit-capi-2.0.0.xcframework"));

            Assert.That(pin.DistVersion, Is.EqualTo("1.14.0"));
            Assert.That(pin.Xcframeworks, Is.EqualTo(new[] { "ios-native-toolkit-2.0.0.xcframework", "ios-native-toolkit-capi-2.0.0.xcframework" }));
        }

        [Test]
        public void Read_MissingFile_Throws()
        {
            Assert.Throws<BuildFailedException>(() => IosXcframeworkPin.Read(Path.Combine(tempDir, "VERSION.txt")));
        }

        [TestCase("xcframeworks: ios-native-toolkit-2.0.0.xcframework")]
        [TestCase("dist_version: 1.14.0")]
        [TestCase("dist_version: 1.14.0", "xcframeworks:")]
        public void Read_MissingKey_Throws(params string[] lines)
        {
            Assert.Throws<BuildFailedException>(() => IosXcframeworkPin.Read(WritePin(lines)));
        }

        // Passes the required-value check but names nothing; the build would delete every XCFramework.
        [TestCase(",")]
        [TestCase(" , , ")]
        [TestCase("ios-native-toolkit-2.0.0")]
        [TestCase("ios-native-toolkit-2.0.0-debug.xcframework")]
        [TestCase("ios/ios-native-toolkit-2.0.0.xcframework")]
        [TestCase("ios-native-toolkit-2.0.0.xcframework, ios-native-toolkit-2.0.0.xcframework")]
        public void Read_NameThatIsNotAReleaseXcframework_Throws(string xcframeworks)
        {
            Assert.Throws<BuildFailedException>(() =>
                IosXcframeworkPin.Read(WritePin("dist_version: 1.14.0", "xcframeworks: " + xcframeworks)));
        }

        // The reason for the pin: "ios-native-toolkit-capi-2.0.0" sorts after "ios-native-toolkit-2.0.0",
        // so the old prefix lookup put the C ABI in the Swift library's slot.
        [Test]
        public void ResolveIn_MatchesExactNames_NotTheHighestSortingPrefixMatch()
        {
            MakeXcframeworks("ios-native-toolkit-2.0.0", "ios-native-toolkit-capi-2.0.0", "ios-native-toolkit-1.3.0", "unity-ios-native-toolkit-1.3.0");
            IosXcframeworkPin pin = Pin("ios-native-toolkit-2.0.0.xcframework");

            Assert.That(pin.ResolveIn(tempDir, isDevelopmentBuild: false), Is.EqualTo(new[] { "ios-native-toolkit-2.0.0.xcframework" }));
        }

        [Test]
        public void ResolveIn_ReleaseBuild_IgnoresTheDebugVariant()
        {
            MakeXcframeworks("ios-native-toolkit-2.0.0", "ios-native-toolkit-2.0.0-debug");
            IosXcframeworkPin pin = Pin("ios-native-toolkit-2.0.0.xcframework");

            Assert.That(pin.ResolveIn(tempDir, isDevelopmentBuild: false), Is.EqualTo(new[] { "ios-native-toolkit-2.0.0.xcframework" }));
        }

        [Test]
        public void ResolveIn_DevelopmentBuild_PrefersTheDebugVariant_AndFallsBackToRelease()
        {
            // The Swift library has a -debug variant; the C ABI has none.
            MakeXcframeworks("ios-native-toolkit-2.0.0", "ios-native-toolkit-2.0.0-debug", "ios-native-toolkit-capi-2.0.0");
            IosXcframeworkPin pin = Pin("ios-native-toolkit-2.0.0.xcframework", "ios-native-toolkit-capi-2.0.0.xcframework");

            // One warning, for the C ABI only: the Swift library has its -debug variant.
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("ios-native-toolkit-capi-2.0.0-debug.xcframework"));
            Assert.That(pin.ResolveIn(tempDir, isDevelopmentBuild: true),
                Is.EqualTo(new[] { "ios-native-toolkit-2.0.0-debug.xcframework", "ios-native-toolkit-capi-2.0.0.xcframework" }));
            LogAssert.NoUnexpectedReceived();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ResolveIn_PinnedXcframeworkMissing_Throws(bool isDevelopmentBuild)
        {
            MakeXcframeworks("ios-native-toolkit-capi-2.0.0");
            IosXcframeworkPin pin = Pin("ios-native-toolkit-2.0.0.xcframework", "ios-native-toolkit-capi-2.0.0.xcframework");

            var exception = Assert.Throws<BuildFailedException>(() => pin.ResolveIn(tempDir, isDevelopmentBuild));
            Assert.That(exception.Message, Does.Contain("ios-native-toolkit-2.0.0.xcframework"));
        }

        // PreBuildProcessor resolves in dist/<dist_version>/ios, which may not exist.
        [Test]
        public void ResolveIn_DirectoryMissing_Throws()
        {
            IosXcframeworkPin pin = Pin("ios-native-toolkit-2.0.0.xcframework");

            Assert.Throws<BuildFailedException>(() => pin.ResolveIn(Path.Combine(tempDir, "missing"), isDevelopmentBuild: false));
        }

        [Test]
        public void ResolvePresentIn_TakesTheReleaseForm_ThenTheDebugForm()
        {
            // What a development build leaves in Plugins/iOS: the -debug Swift library and the release C ABI.
            MakeXcframeworks("ios-native-toolkit-2.0.0-debug", "ios-native-toolkit-capi-2.0.0", "ios-native-toolkit-capi-2.0.1");
            IosXcframeworkPin pin = Pin("ios-native-toolkit-2.0.0.xcframework", "ios-native-toolkit-capi-2.0.0.xcframework");

            Assert.That(pin.ResolvePresentIn(tempDir),
                Is.EqualTo(new[] { "ios-native-toolkit-2.0.0-debug.xcframework", "ios-native-toolkit-capi-2.0.0.xcframework" }));
        }

        [Test]
        public void ResolvePresentIn_BothForms_TakesTheRelease()
        {
            MakeXcframeworks("ios-native-toolkit-2.0.0", "ios-native-toolkit-2.0.0-debug");
            IosXcframeworkPin pin = Pin("ios-native-toolkit-2.0.0.xcframework");

            Assert.That(pin.ResolvePresentIn(tempDir), Is.EqualTo(new[] { "ios-native-toolkit-2.0.0.xcframework" }));
        }

        [Test]
        public void ResolvePresentIn_NeitherForm_Throws()
        {
            MakeXcframeworks("ios-native-toolkit-capi-2.0.0");
            IosXcframeworkPin pin = Pin("ios-native-toolkit-2.0.0.xcframework");

            Assert.Throws<BuildFailedException>(() => pin.ResolvePresentIn(tempDir));
        }

        [Test]
        public void DebugNameOf_InsertsTheSuffixBeforeTheExtension()
        {
            Assert.That(IosXcframeworkPin.DebugNameOf("ios-native-toolkit-2.0.0.xcframework"), Is.EqualTo("ios-native-toolkit-2.0.0-debug.xcframework"));
        }

        // The committed pin and the committed XCFrameworks move together (Plugins/iOS/VERSION.txt). A
        // development build may have left the -debug form of one in the working tree, so either form counts.
        [Test]
        public void CommittedPin_ResolvesInPluginsIos()
        {
            string pluginsIos = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Packages", "com.jonghyunkim.nativetoolkit", "Plugins", "iOS"));
            IosXcframeworkPin pin = IosXcframeworkPin.Read(Path.Combine(pluginsIos, IosXcframeworkPin.FileName));

            Assert.That(pin.ResolvePresentIn(pluginsIos).Count, Is.EqualTo(pin.Xcframeworks.Count));
        }

        private string WritePin(params string[] lines)
        {
            string path = Path.Combine(tempDir, "VERSION.txt");
            File.WriteAllLines(path, lines);
            return path;
        }

        private IosXcframeworkPin Pin(params string[] xcframeworks) =>
            IosXcframeworkPin.Read(WritePin("dist_version: 1.14.0", "xcframeworks: " + string.Join(", ", xcframeworks)));

        private void MakeXcframeworks(params string[] baseNames)
        {
            foreach (string baseName in baseNames)
                Directory.CreateDirectory(Path.Combine(tempDir, baseName + ".xcframework"));
        }
    }
}
