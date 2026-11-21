#nullable enable

namespace JonghyunKim.NativeToolkit.Editor.iOS
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using UnityEditor.Build;
    using UnityEngine;

    /// <summary>
    /// The iOS entry in <c>Plugins/iOS/VERSION.txt</c>: which native-toolkit dist folder the
    /// XCFrameworks come from, and their full folder names.
    /// </summary>
    /// <remarks>
    /// The three places that pick the iOS XCFrameworks - <c>PreBuildProcessor</c> (dist to Plugins/iOS),
    /// <c>PostBuildProcessor</c> (Plugins/iOS to the exported Xcode project) and
    /// <c>IosFrameworkPatcher</c> (the same, from the menu; removed in stage 4 of the C ABI 2.0.0 move, since
    /// Unity links and embeds the XCFrameworks by their .meta) - used to take the highest-sorting folder
    /// whose name starts with <c>ios-native-toolkit-</c> or <c>unity-ios-native-toolkit-</c>. The first
    /// prefix also matches <c>ios-native-toolkit-capi-2.0.0.xcframework</c>, which sorts after
    /// <c>ios-native-toolkit-2.0.0.xcframework</c> ('c' &gt; '2'), so with both present the Swift
    /// library's slot would get the C ABI. Android and Windows broke the same way when native-toolkit
    /// published a new dist, and are pinned the same way (<c>AndroidPin</c>, <c>WindowsPin</c>).
    /// They read this pin and match names exactly (the menu tool, while it existed, too).
    /// <para>
    /// The pin lives in <c>NativeToolkit.Editor</c> because the build steps sit in two assemblies:
    /// <c>NativeToolkit.BuildProcessors.Editor</c>, which only compiles with
    /// <c>NATIVETOOLKIT_ENABLE_BUILD_STEPS</c>, and <c>NativeToolkit.BuildTools.Editor</c>, which always
    /// compiles. Both reach it through <c>InternalsVisibleTo</c>, and so do the EditMode tests.
    /// </para>
    /// </remarks>
    internal sealed class IosXcframeworkPin
    {
        internal const string FileName = "VERSION.txt";

        private const string Extension = ".xcframework";
        private const string DebugSuffix = "-debug";

        internal string DistVersion { get; }

        /// <summary>The release XCFramework folder names, in the order the pin lists them.</summary>
        internal IReadOnlyList<string> Xcframeworks { get; }

        private IosXcframeworkPin(string distVersion, IReadOnlyList<string> xcframeworks)
        {
            DistVersion = distVersion;
            Xcframeworks = xcframeworks;
        }

        /// <summary>
        /// Reads the pin. Throws <see cref="BuildFailedException"/> when the file or a required key is
        /// missing, or a name is not a release XCFramework folder name, rather than falling back to a guess.
        /// </summary>
        internal static IosXcframeworkPin Read(string path)
        {
            if (!File.Exists(path))
            {
                throw new BuildFailedException(
                    $"[Build][iOS] {FileName} not found at {path}. It declares which native-toolkit " +
                    "XCFrameworks this folder holds; without it the build has nothing to pick.");
            }

            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string rawLine in File.ReadAllLines(path))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                    continue;

                int separator = line.IndexOf(':');
                if (separator <= 0)
                    continue;

                values[line.Substring(0, separator).Trim()] = line.Substring(separator + 1).Trim();
            }

            string distVersion = Require(values, "dist_version", path);
            string[] xcframeworks = Require(values, "xcframeworks", path)
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(item => item.Trim())
                .Where(item => item.Length > 0)
                .ToArray();

            // "xcframeworks: ," passes Require but names nothing; resolving it would delete every
            // XCFramework in Plugins/iOS and copy none.
            if (xcframeworks.Length == 0)
                throw new BuildFailedException($"[Build][iOS] {FileName} lists no XCFrameworks under 'xcframeworks' ({path}).");

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string name in xcframeworks)
            {
                if (!name.EndsWith(Extension, StringComparison.OrdinalIgnoreCase)
                    || name.EndsWith(DebugSuffix + Extension, StringComparison.OrdinalIgnoreCase)
                    || name.IndexOfAny(new[] { '/', '\\' }) >= 0)
                {
                    throw new BuildFailedException(
                        $"[Build][iOS] {FileName} lists '{name}' ({path}). List the full folder names of " +
                        $"the release XCFrameworks (ending in {Extension}, without {DebugSuffix}); the " +
                        $"{DebugSuffix} variant is picked from the release name.");
                }

                if (!seen.Add(name))
                    throw new BuildFailedException($"[Build][iOS] {FileName} lists {name} twice ({path}).");
            }

            return new IosXcframeworkPin(distVersion, xcframeworks);
        }

        /// <summary>
        /// The folder names to use from <paramref name="directory"/>, by exact name. A development build
        /// takes the <c>-debug</c> variant of a pinned name when the directory has it and the release
        /// one otherwise (native-toolkit publishes no <c>-debug</c> variant of every XCFramework; the
        /// C ABI has none, and a test player build is always a development build). Throws
        /// <see cref="BuildFailedException"/> when a pinned XCFramework is in neither form.
        /// </summary>
        internal IReadOnlyList<string> ResolveIn(string directory, bool isDevelopmentBuild)
        {
            var resolved = new List<string>();
            var missing = new List<string>();
            foreach (string name in Xcframeworks)
            {
                if (isDevelopmentBuild)
                {
                    string debugName = DebugNameOf(name);
                    if (Directory.Exists(Path.Combine(directory, debugName)))
                    {
                        resolved.Add(debugName);
                        continue;
                    }
                }

                if (Directory.Exists(Path.Combine(directory, name)))
                {
                    if (isDevelopmentBuild)
                        Debug.LogWarning($"[Build][iOS] No {DebugNameOf(name)} in {directory}; using the release XCFramework {name}.");
                    resolved.Add(name);
                }
                else
                {
                    missing.Add(name);
                }
            }

            ThrowIfMissing(directory, missing);
            return resolved;
        }

        /// <summary>
        /// The folder names in <paramref name="directory"/> for the pinned XCFrameworks, in whichever
        /// form is there: the release one, or the <c>-debug</c> one when only that is. For a folder that
        /// the last build filled - a development build replaces the release folder in Plugins/iOS with the
        /// <c>-debug</c> one when dist publishes it - rather than for a build that picks the form. Throws
        /// <see cref="BuildFailedException"/> when a pinned XCFramework is in neither form.
        /// </summary>
        internal IReadOnlyList<string> ResolvePresentIn(string directory)
        {
            var resolved = new List<string>();
            var missing = new List<string>();
            foreach (string name in Xcframeworks)
            {
                if (Directory.Exists(Path.Combine(directory, name)))
                    resolved.Add(name);
                else if (Directory.Exists(Path.Combine(directory, DebugNameOf(name))))
                    resolved.Add(DebugNameOf(name));
                else
                    missing.Add(name);
            }

            ThrowIfMissing(directory, missing);
            return resolved;
        }

        private void ThrowIfMissing(string directory, List<string> missing)
        {
            if (missing.Count > 0)
            {
                string available = Directory.Exists(directory)
                    ? string.Join(", ", Directory.GetDirectories(directory, "*" + Extension).Select(Path.GetFileName))
                    : "(the directory does not exist)";
                throw new BuildFailedException(
                    $"[Build][iOS] {FileName} names {string.Join(", ", missing)} (dist {DistVersion}), which is not in " +
                    $"{directory}. Available: {available}. Fix the pin or the folder; the build does not pick another XCFramework.");
            }
        }

        /// <summary><c>name.xcframework</c> to <c>name-debug.xcframework</c>.</summary>
        internal static string DebugNameOf(string releaseName) =>
            releaseName.Substring(0, releaseName.Length - Extension.Length) + DebugSuffix + Extension;

        private static string Require(IDictionary<string, string> values, string key, string path)
        {
            if (values.TryGetValue(key, out string? value) && !string.IsNullOrEmpty(value))
                return value;

            throw new BuildFailedException(
                $"[Build][iOS] {FileName} has no value for '{key}' ({path}). " +
                "Add it; the build does not guess which version or XCFramework to use.");
        }
    }
}
