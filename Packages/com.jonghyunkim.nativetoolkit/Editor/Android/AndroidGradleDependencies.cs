#nullable enable

namespace JonghyunKim.NativeToolkit.Editor.Android
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using UnityEngine;

    /// <summary>
    /// Adds the runtime dependencies of native-toolkit's two Android AARs to the Gradle project Unity
    /// generates, before Gradle runs.
    /// </summary>
    /// <remarks>
    /// An AAR copied into Plugins/Android carries no dependency information, and the Gradle project
    /// Unity writes resolves AARs through flatDir with <c>PREFER_SETTINGS</c>, so a Maven repository
    /// added to a template is not used. The dependencies are therefore named here and added to
    /// <c>unityLibrary/build.gradle</c>; <c>launcher</c> already depends on <c>:unityLibrary</c>.
    /// <para>
    /// This runs in every Android build of a project that has the package, not only in this
    /// repository: it sits outside the <c>NATIVETOOLKIT_ENABLE_BUILD_STEPS</c> assembly. It also runs
    /// when a project is exported, so an exported project needs no manual patching.
    /// </para>
    /// <para>
    /// The versions are those of the runtime variant in the Gradle Module Metadata (<c>.module</c>)
    /// that native-toolkit publishes next to the AARs, for the release pinned in
    /// Plugins/Android/VERSION.txt. An EditMode test compares this list with that file, so a new
    /// release that changes a version fails the test instead of shipping a stale list.
    /// </para>
    /// </remarks>
    internal static class AndroidGradleDependencies
    {
        private const string LogTag = nameof(AndroidGradleDependencies);

        internal const string BeginMarker = "    // BEGIN native-toolkit runtime dependencies (added by the NativeToolkit package; do not edit)";
        internal const string EndMarker = "    // END native-toolkit runtime dependencies";

        /// <summary>
        /// group:module:version, in the order the runtime variants of android-native-toolkit 2.0.0
        /// and android-native-toolkit-capi 2.0.0 list them, without the capi's dependency on
        /// android-native-toolkit itself (that AAR is placed in Plugins/Android).
        /// </summary>
        internal static readonly IReadOnlyList<string> Dependencies = new[]
        {
            "org.jetbrains.kotlin:kotlin-parcelize-runtime:2.2.21",
            "androidx.core:core-ktx:1.18.0",
            "androidx.media:media:1.8.0",
            "org.jetbrains.kotlinx:kotlinx-coroutines-core:1.9.0",
            "androidx.fragment:fragment:1.9.1",
            "androidx.startup:startup-runtime:1.2.0",
            "org.jetbrains.kotlin:kotlin-stdlib:2.2.21",
        };

        /// <param name="unityLibraryPath">The root of the unityLibrary module, not of the Gradle project.</param>
        internal static void Apply(string unityLibraryPath)
        {
            string buildGradle = Path.Combine(unityLibraryPath, "build.gradle");
            if (!File.Exists(buildGradle))
            {
                // An unexpected layout: fail the build rather than produce an APK that crashes on the
                // first call into the library.
                throw new InvalidOperationException($"[{LogTag}] unityLibrary build.gradle not found at {buildGradle}");
            }

            string original = File.ReadAllText(buildGradle);
            string patched = Patch(original);
            if (patched != original)
            {
                File.WriteAllText(buildGradle, patched, new UTF8Encoding(false));
            }
            Debug.Log($"[{LogTag}] {Dependencies.Count} native-toolkit runtime dependencies in {buildGradle}");
        }

        /// <summary>
        /// Puts the dependency block at the top of the first top-level <c>dependencies {</c> block,
        /// replacing a block an earlier run left there, so running it again changes nothing.
        /// </summary>
        /// <exception cref="InvalidOperationException">No top-level <c>dependencies {</c> line.</exception>
        internal static string Patch(string buildGradle)
        {
            string newline = buildGradle.Contains("\r\n") ? "\r\n" : "\n";
            string withoutOld = RemoveBlock(buildGradle);

            var block = new StringBuilder();
            block.Append(BeginMarker).Append(newline);
            foreach (string dependency in Dependencies)
                block.Append("    implementation '").Append(dependency).Append('\'').Append(newline);
            block.Append(EndMarker).Append(newline);

            int anchor = FindDependenciesLineEnd(withoutOld);
            if (anchor < 0)
                throw new InvalidOperationException($"[{LogTag}] no top-level 'dependencies {{' block in unityLibrary build.gradle");

            return withoutOld.Insert(anchor, block.ToString());
        }

        /// <summary>The index just past the line break of the first line that is exactly <c>dependencies {</c>.</summary>
        private static int FindDependenciesLineEnd(string text)
        {
            int index = 0;
            while (index < text.Length)
            {
                int lineEnd = text.IndexOf('\n', index);
                int next = lineEnd < 0 ? text.Length : lineEnd + 1;
                string line = text.Substring(index, (lineEnd < 0 ? text.Length : lineEnd) - index).TrimEnd('\r');
                if (line == "dependencies {")
                    return next;
                index = next;
            }
            return -1;
        }

        private static string RemoveBlock(string text)
        {
            int begin = text.IndexOf(BeginMarker, StringComparison.Ordinal);
            if (begin < 0) return text;

            int end = text.IndexOf(EndMarker, begin, StringComparison.Ordinal);
            if (end < 0)
                throw new InvalidOperationException($"[{LogTag}] unityLibrary build.gradle has the begin marker but not the end marker");

            int endLine = text.IndexOf('\n', end);
            int removeTo = endLine < 0 ? text.Length : endLine + 1;
            return text.Remove(begin, removeTo - begin);
        }
    }

#if UNITY_ANDROID
    /// <summary>
    /// The build callback. Compiled only while Android is the target, because the interface lives in
    /// the Android build support module, which an Editor without it does not have.
    /// </summary>
    internal sealed class AndroidGradleDependenciesProcessor : UnityEditor.Android.IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 0;

        /// <param name="path">The root of the unityLibrary module, not of the Gradle project.</param>
        public void OnPostGenerateGradleAndroidProject(string path) => AndroidGradleDependencies.Apply(path);
    }
#endif
}
