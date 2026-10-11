#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using JonghyunKim.NativeToolkit.Editor.Android;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// Layer 1 tests for <see cref="AndroidGradleDependencies"/>: what it writes into
    /// unityLibrary/build.gradle, and that its list matches what native-toolkit publishes.
    /// </summary>
    public sealed class AndroidGradleDependenciesTests
    {
        // The shape Unity 6000.4 generates (Library/Bee/artifacts/Android/Gradle/unityLibrary/build.gradle).
        private const string GeneratedBuildGradle =
            "apply plugin: 'com.android.library'\n" +
            "apply from: '../shared/keepUnitySymbols.gradle'\n" +
            "\n" +
            "dependencies {\n" +
            "    implementation fileTree(dir: 'libs', include: ['*.jar'])\n" +
            "    implementation(name: 'android-native-toolkit-2.0.0', ext:'aar')\n" +
            "    implementation 'androidx.games:games-activity:4.4.0'\n" +
            "}\n" +
            "\n" +
            "android {\n" +
            "    namespace \"com.unity3d.player\"\n" +
            "}\n";

        [Test]
        public void Patch_AddsEveryDependencyInsideTheDependenciesBlock()
        {
            string patched = AndroidGradleDependencies.Patch(GeneratedBuildGradle);

            int open = patched.IndexOf("dependencies {\n", StringComparison.Ordinal);
            int close = patched.IndexOf("\n}\n", open, StringComparison.Ordinal);
            string block = patched.Substring(open, close - open);
            foreach (string dependency in AndroidGradleDependencies.Dependencies)
                Assert.That(block, Does.Contain($"    implementation '{dependency}'\n"));
            Assert.That(block, Does.Contain("implementation(name: 'android-native-toolkit-2.0.0', ext:'aar')"), "the existing lines are kept");
        }

        [Test]
        public void Patch_RunTwice_ChangesNothingTheSecondTime()
        {
            string once = AndroidGradleDependencies.Patch(GeneratedBuildGradle);
            string twice = AndroidGradleDependencies.Patch(once);

            Assert.That(twice, Is.EqualTo(once));
        }

        [Test]
        public void Patch_ReplacesABlockFromAnOlderList()
        {
            string older = GeneratedBuildGradle.Replace(
                "dependencies {\n",
                "dependencies {\n" +
                AndroidGradleDependencies.BeginMarker + "\n" +
                "    implementation 'org.jetbrains.kotlin:kotlin-stdlib:2.0.21'\n" +
                AndroidGradleDependencies.EndMarker + "\n");

            string patched = AndroidGradleDependencies.Patch(older);

            Assert.That(patched, Does.Not.Contain("2.0.21"));
            Assert.That(patched, Is.EqualTo(AndroidGradleDependencies.Patch(GeneratedBuildGradle)));
        }

        [Test]
        public void Patch_KeepsCrLfLineEndings()
        {
            string crlf = GeneratedBuildGradle.Replace("\n", "\r\n");

            string patched = AndroidGradleDependencies.Patch(crlf);

            Assert.That(patched.Replace("\r\n", ""), Does.Not.Contain("\n"));
        }

        [Test]
        public void Patch_WithoutADependenciesBlock_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => AndroidGradleDependencies.Patch("android {\n}\n"));
        }

        [Test]
        public void Patch_DoesNotTakeANestedDependenciesBlock()
        {
            // Only a top-level block counts; buildscript { dependencies { ... } } is the classpath.
            string nested = "buildscript {\n    dependencies {\n    }\n}\n" + GeneratedBuildGradle;

            string patched = AndroidGradleDependencies.Patch(nested);

            Assert.That(patched.IndexOf(AndroidGradleDependencies.BeginMarker, StringComparison.Ordinal),
                Is.GreaterThan(patched.IndexOf("\ndependencies {", StringComparison.Ordinal)));
        }

        /// <summary>
        /// J-2 in artifact/topics/android-c-abi-2: the list is written in C#, so it is compared with
        /// the Gradle Module Metadata native-toolkit publishes for the pinned release. Needs the
        /// sibling native-toolkit checkout (or NATIVE_TOOLKIT_DIST_ROOT); skipped without it.
        /// </summary>
        [Test]
        public void Dependencies_MatchTheRuntimeVariantsOfThePinnedRelease()
        {
            string distVersion = ReadPinnedDistVersion();
            string m2 = Path.Combine(DistRoot(), distVersion, "android", "m2", "io", "github", "kimjh4941");
            if (!Directory.Exists(m2))
                Assert.Ignore($"native-toolkit dist not found at {m2}");

            var published = new HashSet<string>();
            foreach (string module in Directory.GetFiles(m2, "*.module", SearchOption.AllDirectories))
            {
                foreach (string dependency in RuntimeDependencies(File.ReadAllText(module)))
                {
                    // The capi AAR depends on the Kotlin API AAR, which is placed in Plugins/Android.
                    if (dependency.StartsWith("io.github.kimjh4941:", StringComparison.Ordinal))
                        continue;
                    published.Add(dependency);
                }
            }

            Assert.That(published, Is.Not.Empty, "no runtime dependency was read; the .module format may have changed");
            Assert.That(AndroidGradleDependencies.Dependencies, Is.EquivalentTo(published));
        }

        /// <summary>group:module:version of each dependency of the runtime variant in a .module file.</summary>
        private static IEnumerable<string> RuntimeDependencies(string moduleJson)
        {
            Match usage = Regex.Match(moduleJson, "\"org\\.gradle\\.usage\"\\s*:\\s*\"java-runtime\"");
            Assert.That(usage.Success, Is.True, "no java-runtime variant in the .module file");

            // The variant's dependencies come after its attributes and before its files.
            int files = moduleJson.IndexOf("\"files\"", usage.Index, StringComparison.Ordinal);
            string variant = moduleJson.Substring(usage.Index, (files < 0 ? moduleJson.Length : files) - usage.Index);

            foreach (Match match in Regex.Matches(variant,
                "\"group\"\\s*:\\s*\"([^\"]+)\"\\s*,\\s*\"module\"\\s*:\\s*\"([^\"]+)\"\\s*,\\s*\"version\"\\s*:\\s*\\{\\s*\"requires\"\\s*:\\s*\"([^\"]+)\""))
            {
                yield return $"{match.Groups[1].Value}:{match.Groups[2].Value}:{match.Groups[3].Value}";
            }
        }

        private static string ProjectRoot() => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        private static string DistRoot()
        {
            string? fromEnvironment = Environment.GetEnvironmentVariable("NATIVE_TOOLKIT_DIST_ROOT");
            return !string.IsNullOrEmpty(fromEnvironment)
                ? fromEnvironment!
                : Path.GetFullPath(Path.Combine(ProjectRoot(), "..", "native-toolkit", "dist"));
        }

        private static string ReadPinnedDistVersion()
        {
            string pin = Path.Combine(ProjectRoot(), "Packages", "com.jonghyunkim.nativetoolkit", "Plugins", "Android", "VERSION.txt");
            string? line = File.ReadAllLines(pin)
                .Select(l => l.Trim())
                .FirstOrDefault(l => l.StartsWith("dist_version:", StringComparison.Ordinal));
            Assert.That(line, Is.Not.Null, "Plugins/Android/VERSION.txt has no dist_version");
            return line!.Substring("dist_version:".Length).Trim();
        }
    }
}
