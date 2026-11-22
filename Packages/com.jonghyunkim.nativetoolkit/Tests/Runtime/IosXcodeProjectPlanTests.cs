#nullable enable

using NUnit.Framework;
using JonghyunKim.NativeToolkit.Editor.iOS;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// Layer 1 tests for <see cref="IosXcodeProjectPlan"/>: when the iOS post-build hook stops a build and
    /// what it does with the notification installer (3-S design v3, 1.2).
    /// </summary>
    public sealed class IosXcodeProjectPlanTests
    {
        private static readonly string[] Pinned = { "ios-native-toolkit-2.0.0.xcframework", "ios-native-toolkit-capi-2.0.0.xcframework" };

        private static string Entry(string name, string phase) => $"\t\t\t\tAB /* {name} in {phase} */,\n";

        private static string Linked(params string[] names)
        {
            string text = "";
            foreach (string name in names)
            {
                // The build file's definition line (no trailing comma) and its phase entry.
                text += $"\t\tAB /* {name} in Frameworks */ = {{isa = PBXBuildFile; }};\n" + Entry(name, "Frameworks");
                text += $"\t\tCD /* {name} in Embed Frameworks */ = {{isa = PBXBuildFile; }};\n" + Entry(name, "Embed Frameworks");
            }
            return text;
        }

        [Test]
        public void Problem_LinkedAndEmbeddedOnce_None()
        {
            Assert.That(IosXcodeProjectPlan.Problem(Pinned, Linked(Pinned)), Is.Null);
        }

        [Test]
        public void Problem_NotEmbedded_StopsAndNamesIt()
        {
            string text = Linked(Pinned).Replace(Entry(Pinned[1], "Embed Frameworks"), "");
            string? problem = IosXcodeProjectPlan.Problem(Pinned, text);
            Assert.That(problem, Does.Contain("ios-native-toolkit-capi-2.0.0.xcframework is linked 1 and embedded 0"));
            Assert.That(problem, Does.Contain("AddToEmbeddedBinaries"));
        }

        [Test]
        public void Problem_LinkedTwice_Stops()
        {
            string text = Linked(Pinned) + Entry(Pinned[0], "Frameworks");
            Assert.That(IosXcodeProjectPlan.Problem(Pinned, text), Does.Contain("linked 2"));
        }

        [TestCase("ios-native-toolkit-1.3.0.xcframework")]
        [TestCase("unity-ios-native-toolkit-1.3.0.xcframework")]
        [TestCase("unity-ios-native-toolkit-1.3.0-debug.xcframework")]
        public void Problem_A1xFrameworkLeft_StopsAndSaysReplace(string legacy)
        {
            string? problem = IosXcodeProjectPlan.Problem(Pinned, Linked(Pinned) + Entry(legacy, "Frameworks"));
            Assert.That(problem, Does.Contain(legacy));
            Assert.That(problem, Does.Contain("Replace"));
        }

        [Test]
        public void Problem_TheCApiNameIsNotTakenForA1xName()
        {
            // "ios-native-toolkit-capi-2.0.0" must not match the 1.x pattern.
            Assert.That(IosXcodeProjectPlan.Problem(Pinned, Linked(Pinned)), Is.Null);
        }

        // The expected action is passed by name: the enum is internal and a public test method cannot take it.
        [TestCase(false, false, "Add")]
        [TestCase(false, true, "None")]
        [TestCase(true, true, "Remove")]
        [TestCase(true, false, "None")]
        public void Installer_AddsOnceAndRemovesWhenOptedOut(bool skip, bool present, string expected)
        {
            Assert.That(IosXcodeProjectPlan.Installer(skip, present).ToString(), Is.EqualTo(expected));
        }
    }
}
