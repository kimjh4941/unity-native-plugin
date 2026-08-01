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
            string latestVersion = FindLatestVersionInDist();
            if (!string.IsNullOrEmpty(latestVersion))
            {
                CopyAndroidLibraries(config, latestVersion);
            }
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
            string latestVersion = FindLatestVersionInDist();
            if (!string.IsNullOrEmpty(latestVersion))
            {
                CopyWindowsLibraries(config, latestVersion);
            }
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
    /// Copies Android AAR libraries (Debug/Release) from dist folder to Plugins/Android.
    /// </summary>
    private void CopyAndroidLibraries(string config, string version)
    {
        UnityEngine.Debug.Log($"[Build][Android] Copying libraries from dist (config={config}, version={version})");

        string distAndroidDir = Path.Combine(NativeToolkitDistRoot, version, "android");

        if (!Directory.Exists(distAndroidDir))
        {
            UnityEngine.Debug.LogError($"[Build][Android] Android dist directory not found: {distAndroidDir}");
            return;
        }

        bool isDevelopmentBuild = string.Equals(config, "Debug", StringComparison.OrdinalIgnoreCase);

        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string destDir = Path.Combine(projectRoot, "Packages/com.jonghyunkim.nativetoolkit/Plugins/Android");

        // Resolve every source BEFORE deleting anything. Deleting first and then failing to find a
        // replacement would leave the project with no native libraries at all.
        string selectedAar1 = FindAarNameInDist(distAndroidDir, "android-native-toolkit-", "[Build][Android]", isDevelopmentBuild);
        string selectedAar2 = FindAarNameInDist(distAndroidDir, "unity-android-native-toolkit-", "[Build][Android]", isDevelopmentBuild);

        if (string.IsNullOrEmpty(selectedAar1) || string.IsNullOrEmpty(selectedAar2))
        {
            UnityEngine.Debug.LogError(
                $"[Build][Android] Aborting copy: could not resolve both AARs in {distAndroidDir}. " +
                "Existing libraries were left untouched.");
            return;
        }

        if (!Directory.Exists(destDir))
        {
            Directory.CreateDirectory(destDir);
        }
        else
        {
            // Clean existing AAR files only now that both replacements are known to exist.
            foreach (string aarFile in Directory.GetFiles(destDir, "*.aar"))
            {
                File.Delete(aarFile);
                UnityEngine.Debug.Log($"[Build][Android] Deleted old AAR: {aarFile}");
            }
        }

        // Copy AAR files located by prefix search (filename version may differ from directory version)
        File.Copy(Path.Combine(distAndroidDir, selectedAar1), Path.Combine(destDir, selectedAar1), true);
        UnityEngine.Debug.Log($"[Build][Android] Copied {selectedAar1}");

        File.Copy(Path.Combine(distAndroidDir, selectedAar2), Path.Combine(destDir, selectedAar2), true);
        UnityEngine.Debug.Log($"[Build][Android] Copied {selectedAar2}");

        UnityEngine.Debug.Log($"[Build][Android] Copy completed to {destDir}");
    }

    /// <summary>
    /// Finds an AAR file name in dist using prefix and build-mode filter.
    /// For development builds, falls back to the release AAR when no <c>-debug.aar</c> variant is
    /// published: dist does not always ship debug artifacts, and a test player build is always a
    /// development build (UnityEditor.TestRunner PlayerLauncher forces BuildOptions.Development),
    /// so without this fallback on-device test runs could never resolve a library.
    /// </summary>
    private string FindAarNameInDist(string distDir, string prefix, string logPrefix, bool isDevelopmentBuild)
    {
        if (!Directory.Exists(distDir))
        {
            UnityEngine.Debug.LogError($"{logPrefix} Dist directory not found: {distDir}");
            return null;
        }

        string[] candidates = Directory.GetFiles(distDir, "*.aar");

        string selectedName = SelectAarName(candidates, prefix, preferDebug: isDevelopmentBuild, logPrefix);

        if (selectedName == null && isDevelopmentBuild)
        {
            selectedName = SelectAarName(candidates, prefix, preferDebug: false, logPrefix);
            if (selectedName != null)
            {
                UnityEngine.Debug.LogWarning(
                    $"{logPrefix} No -debug.aar published for prefix={prefix}; falling back to the release AAR: {selectedName}");
            }
        }

        if (selectedName == null)
        {
            string available = string.Join(", ", candidates.Select(Path.GetFileName));
            UnityEngine.Debug.LogError($"{logPrefix} AAR not found. prefix={prefix}, isDevelopmentBuild={isDevelopmentBuild}, distDir={distDir}, available={available}");
            return null;
        }

        UnityEngine.Debug.Log($"{logPrefix} Selected AAR: {selectedName}");
        return selectedName;
    }

    /// <summary>
    /// Picks the highest-sorting AAR matching <paramref name="prefix"/> and the requested debug/release
    /// flavour. Returns null when no candidate matches.
    /// </summary>
    private static string SelectAarName(string[] candidates, string prefix, bool preferDebug, string logPrefix)
    {
        string selectedName = null;
        bool hasMultipleMatches = false;

        foreach (string candidatePath in candidates)
        {
            string candidateName = Path.GetFileName(candidatePath);
            if (!candidateName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;

            bool isDebugName = candidateName.EndsWith("-debug.aar", StringComparison.OrdinalIgnoreCase);
            if (isDebugName != preferDebug)
                continue;

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
            UnityEngine.Debug.LogWarning($"{logPrefix} Multiple AAR matches found. Selected: {selectedName}");

        return selectedName;
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

        // Resolve every source BEFORE deleting anything (see CopyAndroidLibraries for rationale).
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
    /// Copies Windows DLL (Debug/Release) from dist folder to Plugins/Windows.
    /// </summary>
    private void CopyWindowsLibraries(string config, string version)
    {
        UnityEngine.Debug.Log($"[Build][Windows] Copying libraries from dist (config={config}, version={version})");

        bool isDevelopmentBuild = string.Equals(config, "Debug", StringComparison.OrdinalIgnoreCase);
        string distWinDir = Path.Combine(NativeToolkitDistRoot, version, "windows");

        if (!Directory.Exists(distWinDir))
        {
            UnityEngine.Debug.LogError($"[Build][Windows] Windows dist directory not found: {distWinDir}");
            return;
        }

        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string destDir = Path.Combine(projectRoot, "Packages/com.jonghyunkim.nativetoolkit/Plugins/Windows");

        // Resolve the source BEFORE deleting anything (see CopyAndroidLibraries for rationale).
        string selectedDll = FindDllNameInDist(distWinDir, "windows-native-toolkit-", "[Build][Windows]", isDevelopmentBuild);
        if (string.IsNullOrEmpty(selectedDll))
        {
            UnityEngine.Debug.LogError(
                $"[Build][Windows] Aborting copy: could not resolve a DLL in {distWinDir}. " +
                "Existing libraries were left untouched.");
            return;
        }

        if (!Directory.Exists(destDir))
        {
            Directory.CreateDirectory(destDir);
        }
        else
        {
            // Clean existing native-toolkit DLL files only (preserve third-party DLLs like Bootstrap),
            // now that the replacement is known to exist.
            foreach (string dll in Directory.GetFiles(destDir, "unity-windows-native-toolkit*.dll"))
            {
                File.Delete(dll);
                UnityEngine.Debug.Log($"[Build][Windows] Deleted old DLL: {dll}");
            }
        }

        string destName = isDevelopmentBuild ? "unity-windows-native-toolkit-debug.dll" : "unity-windows-native-toolkit.dll";
        string srcPath = Path.Combine(distWinDir, selectedDll);
        string dstPath = Path.Combine(destDir, destName);
        File.Copy(srcPath, dstPath, true);
        UnityEngine.Debug.Log($"[Build][Windows] Copied {selectedDll} → {destName} to {destDir}");

        AssetDatabase.Refresh();

        string assetPath = $"Packages/com.jonghyunkim.nativetoolkit/Plugins/Windows/{destName}";
        ConfigureWindowsPluginImporter(assetPath);

        UnityEngine.Debug.Log($"[Build][Windows] Copy completed to {destDir}");
    }

    /// <summary>
    /// Finds a DLL file name in dist using prefix and build-mode filter.
    /// Falls back to the release DLL when no <c>-debug.dll</c> variant is published (see
    /// <see cref="FindAarNameInDist"/> for the rationale).
    /// </summary>
    private string FindDllNameInDist(string distDir, string prefix, string logPrefix, bool isDevelopmentBuild)
    {
        if (!Directory.Exists(distDir))
        {
            UnityEngine.Debug.LogError($"{logPrefix} Dist directory not found: {distDir}");
            return null;
        }

        string[] candidates = Directory.GetFiles(distDir, "*.dll");

        string selectedName = SelectDllName(candidates, prefix, preferDebug: isDevelopmentBuild, logPrefix);

        if (selectedName == null && isDevelopmentBuild)
        {
            selectedName = SelectDllName(candidates, prefix, preferDebug: false, logPrefix);
            if (selectedName != null)
            {
                UnityEngine.Debug.LogWarning(
                    $"{logPrefix} No -debug.dll published for prefix={prefix}; falling back to the release DLL: {selectedName}");
            }
        }

        if (selectedName == null)
        {
            string available = string.Join(", ", candidates.Select(Path.GetFileName));
            UnityEngine.Debug.LogError($"{logPrefix} DLL not found. prefix={prefix}, isDevelopmentBuild={isDevelopmentBuild}, distDir={distDir}, available={available}");
            return null;
        }

        UnityEngine.Debug.Log($"{logPrefix} Selected DLL: {selectedName}");
        return selectedName;
    }

    /// <summary>
    /// Picks the highest-sorting DLL matching <paramref name="prefix"/> and the requested debug/release
    /// flavour. Returns null when no candidate matches.
    /// </summary>
    private static string SelectDllName(string[] candidates, string prefix, bool preferDebug, string logPrefix)
    {
        string selectedName = null;
        bool hasMultipleMatches = false;

        foreach (string candidatePath in candidates)
        {
            string candidateName = Path.GetFileName(candidatePath);
            if (!candidateName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;

            bool isDebugName = candidateName.EndsWith("-debug.dll", StringComparison.OrdinalIgnoreCase);
            if (isDebugName != preferDebug)
                continue;

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
            UnityEngine.Debug.LogWarning($"{logPrefix} Multiple DLL matches found. Selected: {selectedName}");

        return selectedName;
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

        // Resolve the source BEFORE deleting anything (see CopyAndroidLibraries for rationale).
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
    /// (see <see cref="FindAarNameInDist"/> for the rationale).
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
