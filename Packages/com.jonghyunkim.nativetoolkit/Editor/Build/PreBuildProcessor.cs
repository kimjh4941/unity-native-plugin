using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

/// <summary>
/// Pre-build processor that (1) temporarily disables libraries for non-target platforms to keep
/// the build artifact clean, and (2) copies native plugin binaries from native-toolkit dist folder
/// to Plugins for Android/iOS/macOS/Windows.
/// </summary>
public class PreBuildProcessor : IPreprocessBuildWithReport
{
    // Resolved at runtime: sibling repo "native-toolkit/dist" relative to the Unity project root,
    // or overridden by the NATIVE_TOOLKIT_DIST_ROOT environment variable.
    private static string NativeToolkitDistRoot
    {
        get
        {
            string envPath = System.Environment.GetEnvironmentVariable("NATIVE_TOOLKIT_DIST_ROOT");
            if (!string.IsNullOrEmpty(envPath))
            {
                return envPath;
            }

            // Project root is one level above Application.dataPath (the "Assets" folder).
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.GetFullPath(Path.Combine(projectRoot, "..", "native-toolkit", "dist"));
        }
    }

    /// <summary>
    /// The Windows entry in <c>Plugins/Windows/VERSION.txt</c>: which dist folder to copy from and
    /// which artifact to take.
    /// </summary>
    /// <remarks>
    /// Windows reads this instead of calling <see cref="FindLatestVersionInDist"/>. That method picks
    /// the highest semantic version it finds, and the DLL lookup it used to feed picked the
    /// highest-sorting file matching the prefix "windows-native-toolkit-" - two implicit choices whose
    /// outcome depends on what the sibling repository happens to contain, and a prefix that matches
    /// windows-native-toolkit-capi-2.0.0.dll as readily as the 1.x artifact. Both of those helpers are
    /// gone; this struct replaced them. When native-toolkit published dist/1.12.0, both choices
    /// moved on their own: the build deleted the committed 1.x DLL and copied the 2.0.0 C ABI DLL in
    /// its place, with the P/Invoke layer still on 1.x. Naming the version and the artifact makes that
    /// a reviewable line in a commit instead of a consequence of someone else's release.
    /// Android is pinned the same way (<see cref="AndroidPin"/>); iOS and macOS still use the scanning path.
    /// </remarks>
    private readonly struct WindowsPin
    {
        internal const string FileName = "VERSION.txt";

        internal string DistVersion { get; }
        internal string ReleaseDll { get; }

        /// <summary>Empty means "use <see cref="ReleaseDll"/>" - dist publishes no -debug.dll here.</summary>
        internal string DebugDll { get; }

        /// <summary>
        /// The file name to place in Plugins/Windows. Empty means "keep the dist file name". The 1.x
        /// P/Invoke layer loads "unity-windows-native-toolkit", so 1.x pins set this explicitly; the
        /// 2.0.0 C ABI is used under its own dist name, so its pin leaves it empty.
        /// </summary>
        internal string InstallAs { get; }

        /// <summary>
        /// The file name for development builds. Empty falls back to <see cref="InstallAs"/>, then to the
        /// dist file name.
        /// </summary>
        internal string InstallAsDebug { get; }

        private WindowsPin(string distVersion, string releaseDll, string debugDll, string installAs, string installAsDebug)
        {
            DistVersion = distVersion;
            ReleaseDll = releaseDll;
            DebugDll = debugDll;
            InstallAs = installAs;
            InstallAsDebug = installAsDebug;
        }

        /// <summary>The artifact to copy from dist for this build.</summary>
        internal string SourceFor(bool isDevelopmentBuild)
            => isDevelopmentBuild && !string.IsNullOrEmpty(DebugDll) ? DebugDll : ReleaseDll;

        /// <summary>The name to place it under in Plugins/Windows for this build.</summary>
        internal string DestinationFor(bool isDevelopmentBuild)
        {
            if (isDevelopmentBuild && !string.IsNullOrEmpty(InstallAsDebug))
                return InstallAsDebug;
            if (!string.IsNullOrEmpty(InstallAs))
                return InstallAs;
            return SourceFor(isDevelopmentBuild);
        }

        /// <summary>
        /// Reads the pin. Throws <see cref="BuildFailedException"/> when the file or a required key is
        /// missing, rather than falling back to a guess.
        /// </summary>
        /// <remarks>
        /// While the Windows features moved to the 2.0.0 C ABI one at a time, a second pin placed the
        /// 1.x DLL beside this one (artifact/windows/dialog/designs/2026-09-27-windows-dialog-design-v6.md,
        /// J-6). It was removed with the last feature's move; an extra_* key left in the file is ignored.
        /// </remarks>
        internal static WindowsPin Read(string path)
        {
            IDictionary<string, string> values = ReadPinValues(path, "[Build][Windows]", FileName);

            return new WindowsPin(
                Get(values, "dist_version", path, required: true),
                Get(values, "release_dll", path, required: true),
                Get(values, "debug_dll", path, required: false),
                Get(values, "install_as", path, required: false),
                Get(values, "install_as_debug", path, required: false));
        }

        private static string Get(IDictionary<string, string> values, string key, string path, bool required)
        {
            if (values.TryGetValue(key, out string value) && !string.IsNullOrEmpty(value))
                return value;

            if (!required)
                return null;

            throw new BuildFailedException(
                $"[Build][Windows] {FileName} has no value for '{key}' ({path}). " +
                "Add it; the build does not guess which version or artifact to use.");
        }
    }

    /// <summary>
    /// Reads a VERSION.txt pin as "key: value" lines. '#' starts a comment line. Throws
    /// <see cref="BuildFailedException"/> when the file is missing.
    /// </summary>
    private static IDictionary<string, string> ReadPinValues(string path, string logPrefix, string fileName)
    {
        if (!File.Exists(path))
        {
            throw new BuildFailedException(
                $"{logPrefix} {fileName} not found at {path}. It declares which native-toolkit " +
                "artifact this folder holds; without it the build has nothing to copy from.");
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

            string key = line.Substring(0, separator).Trim();
            // Values may contain ':' (the source URL does), so split on the first one only.
            values[key] = line.Substring(separator + 1).Trim();
        }
        return values;
    }

    /// <summary>The value of a required pin key; throws <see cref="BuildFailedException"/> when it is missing or empty.</summary>
    private static string RequirePinValue(IDictionary<string, string> values, string key, string path, string logPrefix, string fileName)
    {
        if (values.TryGetValue(key, out string value) && !string.IsNullOrEmpty(value))
            return value;

        throw new BuildFailedException(
            $"{logPrefix} {fileName} has no value for '{key}' ({path}). " +
            "Add it; the build does not guess which version or artifact to use.");
    }

    /// <summary>
    /// The Android entry in <c>Plugins/Android/VERSION.txt</c>: which dist folder to copy from and
    /// which AARs to take, by their full file names.
    /// </summary>
    /// <remarks>
    /// Android used to scan dist for the highest version and pick AARs by name prefix. When
    /// native-toolkit published dist/1.13.0, the prefix "android-native-toolkit-" matched
    /// android-native-toolkit-capi-2.0.0.aar, the bridge AAR was no longer there, and the build
    /// failed (artifact/topics/android-c-abi-2). The same happened on Windows with 1.12.0, which is
    /// why <see cref="WindowsPin"/> exists; this is the same idea with a list of AARs, because
    /// Android needs two (the Kotlin API and the C ABI that runs on it).
    /// <para>
    /// The extra_* keys place a second set from another dist folder beside the first. They exist
    /// for the migration to the 2.0.0 C ABI, while features not yet moved still call the 1.x bridge.
    /// </para>
    /// </remarks>
    private readonly struct AndroidPin
    {
        internal const string FileName = "VERSION.txt";

        internal string DistVersion { get; }
        internal IReadOnlyList<string> Aars { get; }

        /// <summary>Null when there is no extra set.</summary>
        internal string ExtraDistVersion { get; }
        internal IReadOnlyList<string> ExtraAars { get; }

        private AndroidPin(string distVersion, IReadOnlyList<string> aars, string extraDistVersion, IReadOnlyList<string> extraAars)
        {
            DistVersion = distVersion;
            Aars = aars;
            ExtraDistVersion = extraDistVersion;
            ExtraAars = extraAars;
        }

        /// <summary>
        /// Reads the pin. Throws <see cref="BuildFailedException"/> when the file or a required key is
        /// missing, or when only one of the two extra_* keys is given.
        /// </summary>
        internal static AndroidPin Read(string path)
        {
            IDictionary<string, string> values = ReadPinValues(path, "[Build][Android]", FileName);

            string distVersion = RequirePinValue(values, "dist_version", path, "[Build][Android]", FileName);
            IReadOnlyList<string> aars = SplitList(RequirePinValue(values, "aars", path, "[Build][Android]", FileName));

            values.TryGetValue("extra_dist_version", out string extraDistVersion);
            values.TryGetValue("extra_aars", out string extraAarsValue);
            bool hasExtraVersion = !string.IsNullOrEmpty(extraDistVersion);
            bool hasExtraAars = !string.IsNullOrEmpty(extraAarsValue);
            if (hasExtraVersion != hasExtraAars)
            {
                throw new BuildFailedException(
                    $"[Build][Android] {FileName} gives only one of extra_dist_version and extra_aars ({path}). " +
                    "Give both, or neither.");
            }

            return new AndroidPin(
                distVersion,
                aars,
                hasExtraVersion ? extraDistVersion : null,
                hasExtraAars ? SplitList(extraAarsValue) : Array.Empty<string>());
        }

        private static IReadOnlyList<string> SplitList(string value) =>
            value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(item => item.Trim())
                .Where(item => item.Length > 0)
                .ToArray();
    }

    public int callbackOrder => 0;

    // Determine config from build options
    private static string GetConfigName(BuildReport report)
        => (report.summary.options & BuildOptions.Development) != 0 ? "Debug" : "Release";

    /// <summary>
    /// Entry point executed before the player build starts. Routes to platform‑specific handlers
    /// after cleaning up unrelated plugin folders.
    /// </summary>
    public void OnPreprocessBuild(BuildReport report)
    {
        // Temporarily disable libraries for non-target platforms before building
        CleanupOtherPlatformLibraries(report.summary.platform);

        var config = GetConfigName(report);
        UnityEngine.Debug.Log($"[Build] Configuration: {config}");

        if (report.summary.platform == BuildTarget.Android)
        {
            // Android does not call FindLatestVersionInDist: it reads the pin in
            // Plugins/Android/VERSION.txt instead. See AndroidPin for why.
            CopyAndroidLibraries();
        }
        else if (report.summary.platform == BuildTarget.iOS)
        {
            string latestVersion = FindLatestVersionInDist();
            if (!string.IsNullOrEmpty(latestVersion))
            {
                CopyiOSLibraries(config, latestVersion);
            }
        }
        else if (report.summary.platform == BuildTarget.StandaloneWindows64)
        {
            // Windows does not call FindLatestVersionInDist: it reads the pin in
            // Plugins/Windows/VERSION.txt instead. See WindowsPin for why.
            CopyWindowsLibraries(config);
        }
        else if (report.summary.platform == BuildTarget.StandaloneOSX)
        {
#if UNITY_2021_3_OR_NEWER
            PlayerSettings.usePlayerLog = false;
            UnityEngine.Debug.Log("[Build] Set PlayerSettings.usePlayerLog = false for macOS");
#endif
            string latestVersion = FindLatestVersionInDist();
            if (!string.IsNullOrEmpty(latestVersion))
            {
                CopymacOSLibraries(config, latestVersion);
            }
        }
    }

    /// <summary>
    /// Temporarily disables (renames) plugin library folders for platforms other than the active build
    /// target by appending <c>.disabled</c>. Restores the required platform folder if previously disabled.
    /// </summary>
    private void CleanupOtherPlatformLibraries(BuildTarget targetPlatform)
    {
        UnityEngine.Debug.Log($"[Build] Starting plugin library cleanup. Target = {targetPlatform}");

        string[] allLibraryDirs = {
            "Assets/Plugins/iOS/Library",
            "Assets/Plugins/macOS/Library",
            "Assets/Plugins/Android/Library",
            "Assets/Plugins/Windows/Library"
        };

        foreach (string dir in allLibraryDirs)
        {
            bool shouldKeep = ShouldKeepLibrary(dir, targetPlatform);

            if (!shouldKeep && Directory.Exists(dir))
            {
                // Temporarily disable by renaming to .disabled folder
                string disabledDir = dir + ".disabled";

                // Remove existing .disabled folder if present
                if (Directory.Exists(disabledDir))
                {
                    Directory.Delete(disabledDir, true);
                    UnityEngine.Debug.Log($"[Build] Removing stale disabled folder: {disabledDir}");
                }

                // Rename folder to disable it
                Directory.Move(dir, disabledDir);
                UnityEngine.Debug.Log($"[Build] Disabled library folder: {dir} → {disabledDir}");

                // Make Unity recognize the meta file changes
                AssetDatabase.Refresh();
            }
            else if (shouldKeep)
            {
                UnityEngine.Debug.Log($"[Build] Keeping library folder for active target: {dir}");

                // Restore previously disabled library
                string disabledDir = dir + ".disabled";
                if (!Directory.Exists(dir) && Directory.Exists(disabledDir))
                {
                    Directory.Move(disabledDir, dir);
                    UnityEngine.Debug.Log($"[Build] Restored disabled library: {disabledDir} → {dir}");
                    AssetDatabase.Refresh();
                }
            }
        }

        UnityEngine.Debug.Log("[Build] Plugin library cleanup complete");
    }

    /// <summary>
    /// Determines whether a given plugin folder should be kept for the active build target.
    /// </summary>
    private bool ShouldKeepLibrary(string libraryPath, BuildTarget targetPlatform)
    {
        return targetPlatform switch
        {
            BuildTarget.iOS => libraryPath.Contains("iOS"),
            BuildTarget.StandaloneOSX => libraryPath.Contains("macOS"),
            BuildTarget.Android => libraryPath.Contains("Android"),
            BuildTarget.StandaloneWindows64 => libraryPath.Contains("Windows"),
            _ => false,
        };
    }

    /// <summary>
    /// Finds the highest semantic version in the native-toolkit dist directory.
    /// </summary>
    private string FindLatestVersionInDist()
    {
        if (!Directory.Exists(NativeToolkitDistRoot))
        {
            UnityEngine.Debug.LogError($"[Build] Native-toolkit dist root not found: {NativeToolkitDistRoot}");
            return null;
        }

        var versions = new List<(string dir, Version semanticVersion)>();
        foreach (string dir in Directory.GetDirectories(NativeToolkitDistRoot))
        {
            string dirName = Path.GetFileName(dir);
            if (Version.TryParse(dirName, out var version))
            {
                versions.Add((dirName, version));
            }
        }

        if (versions.Count == 0)
        {
            UnityEngine.Debug.LogError($"[Build] No valid semantic versions found in: {NativeToolkitDistRoot}");
            return null;
        }

        // Sort by semantic version (descending) and return the highest
        var highest = versions.OrderByDescending(v => v.semanticVersion).First();
        UnityEngine.Debug.Log($"[Build] Using native-toolkit version: {highest.dir}");
        return highest.dir;
    }

    /// <summary>
    /// Copies the AARs named by Plugins/Android/VERSION.txt from the dist folder to Plugins/Android,
    /// and deletes every other AAR there.
    /// </summary>
    /// <remarks>
    /// The same AARs serve development and release builds: dist publishes no -debug.aar for Android.
    /// </remarks>
    private void CopyAndroidLibraries()
    {
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string destDir = Path.Combine(projectRoot, "Packages/com.jonghyunkim.nativetoolkit/Plugins/Android");

        AndroidPin pin = AndroidPin.Read(Path.Combine(destDir, AndroidPin.FileName));
        UnityEngine.Debug.Log(
            $"[Build][Android] Pinned by {AndroidPin.FileName}: dist_version={pin.DistVersion}, aars={string.Join(", ", pin.Aars)}" +
            (pin.ExtraDistVersion != null ? $"; extra_dist_version={pin.ExtraDistVersion}, extra_aars={string.Join(", ", pin.ExtraAars)}" : ""));

        // Resolve every source BEFORE deleting anything. Deleting first and then failing to find a
        // replacement would leave the project with no native libraries at all. A pin that does not
        // resolve is a configuration error, not something to work around by copying whatever else
        // is in dist.
        var sources = new List<string>();
        AddAndroidSources(sources, pin.DistVersion, pin.Aars);
        if (pin.ExtraDistVersion != null)
            AddAndroidSources(sources, pin.ExtraDistVersion, pin.ExtraAars);

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string source in sources)
        {
            if (!names.Add(Path.GetFileName(source)))
            {
                throw new BuildFailedException(
                    $"[Build][Android] {AndroidPin.FileName} names {Path.GetFileName(source)} twice.");
            }
        }

        Directory.CreateDirectory(destDir);
        foreach (string aarFile in Directory.GetFiles(destDir, "*.aar"))
        {
            if (names.Contains(Path.GetFileName(aarFile)))
                continue;

            File.Delete(aarFile);
            string meta = aarFile + ".meta";
            if (File.Exists(meta))
                File.Delete(meta);
            UnityEngine.Debug.Log($"[Build][Android] Deleted AAR not in the pin: {aarFile}");
        }

        foreach (string source in sources)
        {
            File.Copy(source, Path.Combine(destDir, Path.GetFileName(source)), true);
            UnityEngine.Debug.Log($"[Build][Android] Copied {source}");
        }

        AssetDatabase.Refresh();
        UnityEngine.Debug.Log($"[Build][Android] Copy completed to {destDir}");
    }

    /// <summary>
    /// Adds the full path of each pinned AAR in dist/&lt;version&gt;/android, or throws
    /// <see cref="BuildFailedException"/> when one is not there.
    /// </summary>
    private static void AddAndroidSources(List<string> sources, string distVersion, IReadOnlyList<string> aars)
    {
        string distAndroidDir = Path.Combine(NativeToolkitDistRoot, distVersion, "android");
        foreach (string aar in aars)
        {
            string source = Path.Combine(distAndroidDir, aar);
            if (!File.Exists(source))
            {
                throw new BuildFailedException(
                    $"[Build][Android] {AndroidPin.FileName} names {aar} in dist {distVersion}, but it is not at {source}.");
            }
            sources.Add(source);
        }
    }

    /// <summary>
    /// Copies iOS XCFramework (Debug/Release) from dist folder to Plugins/iOS.
    /// </summary>
    private void CopyiOSLibraries(string config, string version)
    {
        UnityEngine.Debug.Log($"[Build][iOS] Copying libraries from dist (config={config}, version={version})");

        bool isDevelopmentBuild = string.Equals(config, "Debug", StringComparison.OrdinalIgnoreCase);
        string xcfSuffix = config == "Debug" ? "-debug" : "";
        string distIosDir = Path.Combine(NativeToolkitDistRoot, version, "ios");

        if (!Directory.Exists(distIosDir))
        {
            UnityEngine.Debug.LogError($"[Build][iOS] iOS dist directory not found: {distIosDir}");
            return;
        }

        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string destDir = Path.Combine(projectRoot, "Packages/com.jonghyunkim.nativetoolkit/Plugins/iOS");

        // Resolve every source BEFORE deleting anything (deleting first and then failing to find a replacement would leave the project with no native libraries).
        string selectedXcf1 = FindXcframeworkNameInDist(distIosDir, "ios-native-toolkit-", xcfSuffix, "[Build][iOS]", isDevelopmentBuild);
        string selectedXcf2 = FindXcframeworkNameInDist(distIosDir, "unity-ios-native-toolkit-", xcfSuffix, "[Build][iOS]", isDevelopmentBuild);

        if (string.IsNullOrEmpty(selectedXcf1) || string.IsNullOrEmpty(selectedXcf2))
        {
            UnityEngine.Debug.LogError(
                $"[Build][iOS] Aborting copy: could not resolve both XCFrameworks in {distIosDir}. " +
                "Existing libraries were left untouched.");
            return;
        }

        if (!Directory.Exists(destDir))
        {
            Directory.CreateDirectory(destDir);
        }
        else
        {
            // Clean existing xcframeworks only now that both replacements are known to exist.
            foreach (string xcfDir in Directory.GetDirectories(destDir, "*.xcframework"))
            {
                Directory.Delete(xcfDir, true);
                UnityEngine.Debug.Log($"[Build][iOS] Deleted old XCFramework: {xcfDir}");
            }
        }

        CopyDirectory(Path.Combine(distIosDir, selectedXcf1), Path.Combine(destDir, selectedXcf1));
        UnityEngine.Debug.Log($"[Build][iOS] Copied {selectedXcf1}");

        CopyDirectory(Path.Combine(distIosDir, selectedXcf2), Path.Combine(destDir, selectedXcf2));
        UnityEngine.Debug.Log($"[Build][iOS] Copied {selectedXcf2}");

        AssetDatabase.Refresh();

        // Apply import settings
        foreach (string xcfDir in Directory.GetDirectories(destDir, "*.xcframework"))
        {
            string assetPath = xcfDir.Replace(Path.GetFullPath(Path.Combine(Application.dataPath, "..")), "").TrimStart(Path.DirectorySeparatorChar);
            ConfigureIosXcframeworkImporter(assetPath);
        }

        UnityEngine.Debug.Log($"[Build][iOS] Copy completed to {destDir}");
    }

    /// <summary>
    /// Copies the Windows DLL named by Plugins/Windows/VERSION.txt from the dist folder to
    /// Plugins/Windows.
    /// </summary>
    private void CopyWindowsLibraries(string config)
    {
        bool isDevelopmentBuild = string.Equals(config, "Debug", StringComparison.OrdinalIgnoreCase);

        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string destDir = Path.Combine(projectRoot, "Packages/com.jonghyunkim.nativetoolkit/Plugins/Windows");

        WindowsPin pin = WindowsPin.Read(Path.Combine(destDir, WindowsPin.FileName));
        string selectedDll = pin.SourceFor(isDevelopmentBuild);
        string destName = pin.DestinationFor(isDevelopmentBuild);

        UnityEngine.Debug.Log(
            $"[Build][Windows] Pinned by {WindowsPin.FileName}: dist_version={pin.DistVersion}, " +
            $"dll={selectedDll}, install_as={destName} (config={config})");

        // Resolve the source BEFORE deleting anything (deleting first and then failing to find a replacement would leave the project with no native libraries).
        // A pin that does not resolve is a configuration error, not something to work around by
        // copying whatever else is in dist: that is exactly how the 2.0.0 C ABI DLL got in here
        // while the P/Invoke layer was still 1.x.
        string distWinDir = Path.Combine(NativeToolkitDistRoot, pin.DistVersion, "windows");
        string source = Path.Combine(distWinDir, selectedDll);
        if (!File.Exists(source))
        {
            string available = Directory.Exists(distWinDir)
                ? string.Join(", ", Directory.GetFiles(distWinDir, "*.dll").Select(Path.GetFileName))
                : "(dist directory does not exist)";
            throw new BuildFailedException(
                $"[Build][Windows] {WindowsPin.FileName} names {selectedDll} under dist/{pin.DistVersion}/windows, " +
                $"which is not there. Existing libraries were left untouched. Looked in: {distWinDir}. " +
                $"Available: {available}. Fix the pin or the dist folder; do not let the build pick something else.");
        }

        if (!Directory.Exists(destDir))
        {
            Directory.CreateDirectory(destDir);
        }
        else
        {
            // Remove every other native-toolkit DLL (preserve third-party DLLs like Bootstrap), now that
            // the replacement is known to exist. Both naming schemes are matched: the 1.x DLL was installed
            // as unity-windows-native-toolkit*.dll, the 2.0.0 C ABI keeps its dist name
            // windows-native-toolkit-capi-*.dll. Moving the pin must not leave the old library beside the
            // new one, or the player ships both.
            // The file being replaced under the same name is overwritten below instead, so its .meta (and
            // GUID) survives; a file under a different name goes together with its .meta.
            IEnumerable<string> previous = Directory.GetFiles(destDir, "unity-windows-native-toolkit*.dll")
                .Concat(Directory.GetFiles(destDir, "windows-native-toolkit*.dll"));
            foreach (string dll in previous)
            {
                if (string.Equals(Path.GetFileName(dll), destName, StringComparison.OrdinalIgnoreCase))
                    continue;

                File.Delete(dll);
                string meta = dll + ".meta";
                if (File.Exists(meta))
                    File.Delete(meta);
                UnityEngine.Debug.Log($"[Build][Windows] Deleted old DLL: {dll}");
            }
        }

        File.Copy(source, Path.Combine(destDir, destName), true);
        UnityEngine.Debug.Log($"[Build][Windows] Copied {selectedDll} → {destName} to {destDir}");

        AssetDatabase.Refresh();

        ConfigureWindowsPluginImporter($"Packages/com.jonghyunkim.nativetoolkit/Plugins/Windows/{destName}");

        UnityEngine.Debug.Log($"[Build][Windows] Copy completed to {destDir}");
    }

    /// <summary>
    /// Copies macOS XCFramework (Debug/Release) from dist folder to Plugins/macOS.
    /// </summary>
    private void CopymacOSLibraries(string config, string version)
    {
        UnityEngine.Debug.Log($"[Build][macOS] Copying libraries from dist (config={config}, version={version})");

        bool isDevelopmentBuild = string.Equals(config, "Debug", StringComparison.OrdinalIgnoreCase);
        string xcfSuffix = config == "Debug" ? "-debug" : "";
        string distMacDir = Path.Combine(NativeToolkitDistRoot, version, "mac");

        if (!Directory.Exists(distMacDir))
        {
            UnityEngine.Debug.LogError($"[Build][macOS] macOS dist directory not found: {distMacDir}");
            return;
        }

        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string destDir = Path.Combine(projectRoot, "Packages/com.jonghyunkim.nativetoolkit/Plugins/macOS");

        // Resolve the source BEFORE deleting anything (deleting first and then failing to find a replacement would leave the project with no native libraries).
        string selectedXcf2 = FindXcframeworkNameInDist(distMacDir, "unity-mac-native-toolkit-", xcfSuffix, "[Build][macOS]", isDevelopmentBuild);
        if (string.IsNullOrEmpty(selectedXcf2))
        {
            UnityEngine.Debug.LogError(
                $"[Build][macOS] Aborting copy: could not resolve an XCFramework in {distMacDir}. " +
                "Existing libraries were left untouched.");
            return;
        }

        if (!Directory.Exists(destDir))
        {
            Directory.CreateDirectory(destDir);
        }
        else
        {
            // Clean existing xcframeworks only now that the replacement is known to exist.
            foreach (string xcfDir in Directory.GetDirectories(destDir, "*.xcframework"))
            {
                Directory.Delete(xcfDir, true);
                UnityEngine.Debug.Log($"[Build][macOS] Deleted old XCFramework: {xcfDir}");
            }
        }

        CopyDirectory(Path.Combine(distMacDir, selectedXcf2), Path.Combine(destDir, selectedXcf2));
        UnityEngine.Debug.Log($"[Build][macOS] Copied {selectedXcf2}");

        AssetDatabase.Refresh();

        // Apply import settings
        foreach (string xcfDir in Directory.GetDirectories(destDir, "*.xcframework"))
        {
            string assetPath = xcfDir.Replace(Path.GetFullPath(Path.Combine(Application.dataPath, "..")), "").TrimStart(Path.DirectorySeparatorChar);
            ConfigureMacXcframeworkImporter(assetPath);
        }

        UnityEngine.Debug.Log($"[Build][macOS] Copy completed to {destDir}");
    }

    /// <summary>
    /// Finds an XCFramework folder name in dist using prefix and build-mode filter.
    /// Falls back to the release XCFramework when no <c>-debug.xcframework</c> variant is published
    /// (dist does not always ship debug artifacts, and a test player build is always a development
    /// build: UnityEditor.TestRunner PlayerLauncher forces BuildOptions.Development).
    /// </summary>
    private string FindXcframeworkNameInDist(string distDir, string prefix, string xcfSuffix, string logPrefix, bool isDevelopmentBuild)
    {
        if (!Directory.Exists(distDir))
        {
            UnityEngine.Debug.LogError($"{logPrefix} Dist directory not found: {distDir}");
            return null;
        }

        string[] candidates = Directory.GetDirectories(distDir, "*.xcframework");

        string selectedName = SelectXcframeworkName(candidates, prefix, xcfSuffix, preferDebug: isDevelopmentBuild, logPrefix);

        if (selectedName == null && isDevelopmentBuild)
        {
            selectedName = SelectXcframeworkName(candidates, prefix, xcfSuffix: string.Empty, preferDebug: false, logPrefix);
            if (selectedName != null)
            {
                UnityEngine.Debug.LogWarning(
                    $"{logPrefix} No -debug.xcframework published for prefix={prefix}; falling back to the release XCFramework: {selectedName}");
            }
        }

        if (selectedName == null)
        {
            string available = string.Join(", ", candidates.Select(Path.GetFileName));
            UnityEngine.Debug.LogError($"{logPrefix} XCFramework not found. prefix={prefix}, xcfSuffix={xcfSuffix}, isDevelopmentBuild={isDevelopmentBuild}, distDir={distDir}, available={available}");
            return null;
        }

        UnityEngine.Debug.Log($"{logPrefix} Selected XCFramework: {selectedName}");
        return selectedName;
    }

    /// <summary>
    /// Picks the highest-sorting XCFramework matching <paramref name="prefix"/> and the requested
    /// debug/release flavour. Returns null when no candidate matches.
    /// </summary>
    private static string SelectXcframeworkName(string[] candidates, string prefix, string xcfSuffix, bool preferDebug, string logPrefix)
    {
        string selectedName = null;
        bool hasMultipleMatches = false;

        foreach (string candidatePath in candidates)
        {
            string candidateName = Path.GetFileName(candidatePath);
            if (!candidateName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            bool isDebugName = candidateName.EndsWith("-debug.xcframework", StringComparison.OrdinalIgnoreCase);
            bool suffixMatched;

            if (preferDebug)
            {
                suffixMatched = candidateName.EndsWith(xcfSuffix + ".xcframework", StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                suffixMatched = candidateName.EndsWith(".xcframework", StringComparison.OrdinalIgnoreCase) && !isDebugName;
            }

            if (!suffixMatched)
            {
                continue;
            }

            if (selectedName == null || string.Compare(candidateName, selectedName, StringComparison.OrdinalIgnoreCase) > 0)
            {
                hasMultipleMatches = selectedName != null || hasMultipleMatches;
                selectedName = candidateName;
            }
            else
            {
                hasMultipleMatches = true;
            }
        }

        if (selectedName != null && hasMultipleMatches)
        {
            UnityEngine.Debug.LogWarning($"{logPrefix} Multiple XCFramework matches found. Selected: {selectedName}");
        }

        return selectedName;
    }

    /// <summary>
    /// Recursively copies a directory and its contents.
    /// </summary>
    private void CopyDirectory(string sourceDir, string destDir)
    {
        if (Directory.Exists(destDir))
        {
            Directory.Delete(destDir, true);
        }

        Directory.CreateDirectory(destDir);

        foreach (string file in Directory.GetFiles(sourceDir))
        {
            File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), true);
        }

        foreach (string dir in Directory.GetDirectories(sourceDir))
        {
            CopyDirectory(dir, Path.Combine(destDir, Path.GetFileName(dir)));
        }
    }



    // Enable only iOS for the given .xcframework; disable others
    private static void ConfigureIosXcframeworkImporter(string assetPath)
    {
        AssetDatabase.Refresh();
        var importer = AssetImporter.GetAtPath(assetPath) as PluginImporter;
        if (importer == null)
        {
            UnityEngine.Debug.LogWarning($"[Build][iOS] PluginImporter not found for: {assetPath}");
            return;
        }

        importer.SetCompatibleWithAnyPlatform(false);
        importer.SetCompatibleWithEditor(false);
        importer.SetCompatibleWithPlatform(BuildTarget.iOS, true);
        importer.SetCompatibleWithPlatform(BuildTarget.StandaloneOSX, false);
        importer.SetCompatibleWithPlatform(BuildTarget.Android, false);
#if UNITY_2021_3_OR_NEWER
        importer.SetCompatibleWithPlatform(BuildTarget.StandaloneWindows64, false);
#endif
        importer.SaveAndReimport();
        UnityEngine.Debug.Log($"[Build][iOS] Import settings updated (iOS only): {assetPath}");
    }

    // Enable only macOS for the given .xcframework; disable others
    private static void ConfigureMacXcframeworkImporter(string assetPath)
    {
        AssetDatabase.Refresh();
        var importer = AssetImporter.GetAtPath(assetPath) as PluginImporter;
        if (importer == null)
        {
            UnityEngine.Debug.LogWarning($"[Build][macOS] PluginImporter not found for: {assetPath}");
            return;
        }

        importer.SetCompatibleWithAnyPlatform(false);
        importer.SetCompatibleWithEditor(false);
        importer.SetCompatibleWithPlatform(BuildTarget.StandaloneOSX, true);
        importer.SetCompatibleWithPlatform(BuildTarget.iOS, false);
        importer.SetCompatibleWithPlatform(BuildTarget.Android, false);
#if UNITY_2021_3_OR_NEWER
        importer.SetCompatibleWithPlatform(BuildTarget.StandaloneWindows64, false);
#endif
        importer.SaveAndReimport();
        UnityEngine.Debug.Log($"[Build][macOS] Import settings updated (macOS only): {assetPath}");
    }

    // Enable only Windows for the given plugin DLL; disable others
    private static void ConfigureWindowsPluginImporter(string assetPath)
    {
        AssetDatabase.Refresh();
        var importer = AssetImporter.GetAtPath(assetPath) as PluginImporter;
        if (importer == null)
        {
            UnityEngine.Debug.LogWarning($"[Build][Windows] PluginImporter not found for: {assetPath}");
            return;
        }

        importer.SetCompatibleWithAnyPlatform(false);
        importer.SetCompatibleWithEditor(false);
        importer.SetCompatibleWithPlatform(BuildTarget.StandaloneWindows64, true);
        importer.SetCompatibleWithPlatform(BuildTarget.StandaloneOSX, false);
        importer.SetCompatibleWithPlatform(BuildTarget.iOS, false);
        importer.SetCompatibleWithPlatform(BuildTarget.Android, false);
#if UNITY_2021_3_OR_NEWER
        importer.SetCompatibleWithPlatform(BuildTarget.StandaloneLinux64, false);
#endif
        importer.SaveAndReimport();
        UnityEngine.Debug.Log($"[Build][Windows] Import settings updated (Windows only): {assetPath}");
    }

}
