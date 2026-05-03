using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

/// <summary>
/// Pre-build processor that (1) temporarily disables libraries for non-target platforms to keep
/// the build artifact clean, and (2) copies native plugin binaries from native-toolkit dist folder
/// to Assets/Plugins for Android/iOS/macOS, or triggers Windows native build pipeline.
/// </summary>
public class PreBuildProcessor : IPreprocessBuildWithReport
{
    private const string NativeToolkitDistRoot = "/Users/jonghyunkim/Desktop/native-toolkit/dist";

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
            BuildWindowsLibraries(config);
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

        // Expected file names in dist
        string aarSuffix = config == "Debug" ? "-debug" : "";
        string sourceAar1 = Path.Combine(distAndroidDir, $"android-native-toolkit-{version}{aarSuffix}.aar");
        string sourceAar2 = Path.Combine(distAndroidDir, $"unity-android-native-toolkit-{version}{aarSuffix}.aar");

        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string destDir = Path.Combine(projectRoot, "Packages/com.jonghyunkim.nativetoolkit/Plugins/Android");

        // Clean existing AAR files
        if (Directory.Exists(destDir))
        {
            foreach (string aarFile in Directory.GetFiles(destDir, "*.aar"))
            {
                File.Delete(aarFile);
                UnityEngine.Debug.Log($"[Build][Android] Deleted old AAR: {aarFile}");
            }
        }
        else
        {
            Directory.CreateDirectory(destDir);
        }

        // Copy AAR files
        if (File.Exists(sourceAar1))
        {
            File.Copy(sourceAar1, Path.Combine(destDir, Path.GetFileName(sourceAar1)), true);
            UnityEngine.Debug.Log($"[Build][Android] Copied {Path.GetFileName(sourceAar1)}");
        }
        else
        {
            UnityEngine.Debug.LogWarning($"[Build][Android] AAR not found: {sourceAar1}");
        }

        if (File.Exists(sourceAar2))
        {
            File.Copy(sourceAar2, Path.Combine(destDir, Path.GetFileName(sourceAar2)), true);
            UnityEngine.Debug.Log($"[Build][Android] Copied {Path.GetFileName(sourceAar2)}");
        }
        else
        {
            UnityEngine.Debug.LogWarning($"[Build][Android] AAR not found: {sourceAar2}");
        }

        UnityEngine.Debug.Log($"[Build][Android] Copy completed to {destDir}");
    }

    /// <summary>
    /// Copies iOS XCFramework (Debug/Release) from dist folder to Plugins/iOS.
    /// </summary>
    private void CopyiOSLibraries(string config, string version)
    {
        UnityEngine.Debug.Log($"[Build][iOS] Copying libraries from dist (config={config}, version={version})");

        string xcfSuffix = config == "Debug" ? "-debug" : "";
        string distIosDir = Path.Combine(NativeToolkitDistRoot, version, "ios");

        if (!Directory.Exists(distIosDir))
        {
            UnityEngine.Debug.LogError($"[Build][iOS] iOS dist directory not found: {distIosDir}");
            return;
        }

        // Expected XCFramework names in dist
        string sourceXcf1 = Path.Combine(distIosDir, $"ios-native-toolkit-{version}{xcfSuffix}.xcframework");
        string sourceXcf2 = Path.Combine(distIosDir, $"unity-ios-native-toolkit-{version}{xcfSuffix}.xcframework");

        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string destDir = Path.Combine(projectRoot, "Packages/com.jonghyunkim.nativetoolkit/Plugins/iOS");

        // Clean existing xcframeworks
        if (Directory.Exists(destDir))
        {
            foreach (string xcfDir in Directory.GetDirectories(destDir, "*.xcframework"))
            {
                Directory.Delete(xcfDir, true);
                UnityEngine.Debug.Log($"[Build][iOS] Deleted old XCFramework: {xcfDir}");
            }
        }
        else
        {
            Directory.CreateDirectory(destDir);
        }

        // Copy XCFrameworks
        if (Directory.Exists(sourceXcf1))
        {
            CopyDirectory(sourceXcf1, Path.Combine(destDir, Path.GetFileName(sourceXcf1)));
            UnityEngine.Debug.Log($"[Build][iOS] Copied {Path.GetFileName(sourceXcf1)}");
        }
        else
        {
            UnityEngine.Debug.LogWarning($"[Build][iOS] XCFramework not found: {sourceXcf1}");
        }

        if (Directory.Exists(sourceXcf2))
        {
            CopyDirectory(sourceXcf2, Path.Combine(destDir, Path.GetFileName(sourceXcf2)));
            UnityEngine.Debug.Log($"[Build][iOS] Copied {Path.GetFileName(sourceXcf2)}");
        }
        else
        {
            UnityEngine.Debug.LogWarning($"[Build][iOS] XCFramework not found: {sourceXcf2}");
        }

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
    /// Copies Windows DLL built for Debug/Release to Plugins/Windows/Library.
    /// </summary>
    private void BuildWindowsLibraries(string config)
    {
        UnityEngine.Debug.Log($"[Build][Windows] Pre-build steps started. Config={config}");

        // Step 1: Build WindowsLibrary.dll using MSBuild
        if (!BuildWindowsLibraryDll(config))
        {
            UnityEngine.Debug.LogError("[Build][Windows] Aborting pre-build due to build failure.");
            return;
        }

        // Step 2: Copy the built DLL to Unity Plugins folder
        const string baseDir = @"C:\Users\User\Desktop\native-toolkit\windows\WindowsLibraryExample\x64";
        var dllFileName = config.Equals("Debug", System.StringComparison.OrdinalIgnoreCase)
            ? "WindowsLibrary-Debug.dll"
            : "WindowsLibrary.dll";
        string dllSrc = Path.Combine(baseDir, config, "WindowsLibraryExample", "AppX", dllFileName);
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string destDir = Path.Combine(projectRoot, "Packages/com.jonghyunkim.nativetoolkit/Plugins/Windows");
        string dllDst = Path.Combine(
            destDir,
            config.Equals("Debug", System.StringComparison.OrdinalIgnoreCase)
                ? "UnityWindowsNativeToolkit-Debug.dll"
                : "UnityWindowsNativeToolkit.dll");

        try
        {
            if (Directory.Exists(destDir))
            {
                Directory.Delete(destDir, true);
            }
            Directory.CreateDirectory(destDir);
            File.Copy(dllSrc, dllDst, true);
            UnityEngine.Debug.Log($"[Build][Windows] Copied {Path.GetFileName(dllDst)} (config={config}) to {destDir}");

            // Apply plugin import settings (enable only for Windows)
            string assetPath = $"Packages/com.jonghyunkim.nativetoolkit/Plugins/Windows/{Path.GetFileName(dllDst)}";
            ConfigureWindowsPluginImporter(assetPath);
        }
        catch (System.Exception ex)
        {
            UnityEngine.Debug.LogError($"[Build][Windows] Failed to copy {Path.GetFileName(dllDst)}: " + ex.Message);
        }

        UnityEngine.Debug.Log("[Build][Windows] Pre-build steps completed.");
    }

    /// <summary>
    /// Builds WindowsLibrary.dll using MSBuild.
    /// </summary>
    private bool BuildWindowsLibraryDll(string config)
    {
        UnityEngine.Debug.Log($"[Build][Windows] Building WindowsLibrary.dll (config={config})...");

        // MSBuild path (Visual Studio 2022)
        string msbuildPath = FindMSBuildPath();
        if (string.IsNullOrEmpty(msbuildPath))
        {
            UnityEngine.Debug.LogError("[Build][Windows] MSBuild.exe not found. Please install Visual Studio 2022.");
            return false;
        }

        // Solution file path
        string solutionPath = @"C:\Users\User\Desktop\native-toolkit\windows\WindowsLibraryExample\WindowsLibraryExample.sln";
        if (!File.Exists(solutionPath))
        {
            UnityEngine.Debug.LogError($"[Build][Windows] Solution file not found: {solutionPath}");
            return false;
        }

        // MSBuild arguments
        string configuration = config.Equals("Debug", System.StringComparison.OrdinalIgnoreCase) ? "Debug" : "Release";
        string arguments = $"\"{solutionPath}\" /p:Configuration={configuration} /p:Platform=x64 /t:WindowsLibrary /m";

        try
        {
            // Start MSBuild process
            var processStartInfo = new ProcessStartInfo
            {
                FileName = msbuildPath,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            TryApplyShiftJisEncoding(processStartInfo);

            using (var process = Process.Start(processStartInfo))
            {
                if (process == null)
                {
                    UnityEngine.Debug.LogError("[Build][Windows] Failed to start MSBuild process.");
                    return false;
                }

                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                bool buildSucceeded = process.ExitCode == 0;
                if (!buildSucceeded && ContainsRegsvrFailure(output, error))
                {
                    UnityEngine.Debug.LogWarning("[Build][Windows] MSBuild post-build regsvr32 step failed; skipping COM registration.");
                    buildSucceeded = true;
                }

                if (buildSucceeded)
                {
                    UnityEngine.Debug.Log($"[Build][Windows] WindowsLibrary.dll built successfully (config={configuration}).");
                    UnityEngine.Debug.Log($"[Build][Windows] MSBuild output:\n{output}");
                    return true;
                }
                else
                {
                    UnityEngine.Debug.LogError($"[Build][Windows] MSBuild failed with exit code {process.ExitCode}.");
                    UnityEngine.Debug.LogError($"[Build][Windows] MSBuild output:\n{output}");
                    UnityEngine.Debug.LogError($"[Build][Windows] MSBuild error:\n{error}");
                    return false;
                }
            }
        }
        catch (System.Exception ex)
        {
            UnityEngine.Debug.LogError($"[Build][Windows] Exception during MSBuild: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Finds MSBuild.exe path (Visual Studio 2022).
    /// </summary>
    private string FindMSBuildPath()
    {
        // Common MSBuild paths for Visual Studio 2022
        string[] possiblePaths = new string[]
        {
            @"D:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"
        };

        foreach (string path in possiblePaths)
        {
            if (File.Exists(path))
            {
                UnityEngine.Debug.Log($"[Build][Windows] Found MSBuild.exe: {path}");
                return path;
            }
        }

        // Try to find MSBuild using vswhere.exe
        string vswherePath = @"C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe";
        if (File.Exists(vswherePath))
        {
            try
            {
                var processStartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = vswherePath,
                    Arguments = "-latest -requires Microsoft.Component.MSBuild -find MSBuild\\**\\Bin\\MSBuild.exe",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = System.Diagnostics.Process.Start(processStartInfo))
                {
                    if (process != null)
                    {
                        string output = process.StandardOutput.ReadToEnd().Trim();
                        process.WaitForExit();

                        if (!string.IsNullOrEmpty(output) && File.Exists(output))
                        {
                            UnityEngine.Debug.Log($"[Build][Windows] Found MSBuild.exe via vswhere: {output}");
                            return output;
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError($"[Build][Windows] Failed to run vswhere.exe: {ex.Message}");
            }
        }

        UnityEngine.Debug.LogError("[Build][Windows] MSBuild.exe not found. Please install Visual Studio 2022.");
        return null;
    }

    /// <summary>
    /// Copies macOS XCFramework (Debug/Release) from dist folder to Plugins/macOS.
    /// </summary>
    private void CopymacOSLibraries(string config, string version)
    {
        UnityEngine.Debug.Log($"[Build][macOS] Copying libraries from dist (config={config}, version={version})");

        string xcfSuffix = config == "Debug" ? "-debug" : "";
        string distMacDir = Path.Combine(NativeToolkitDistRoot, version, "mac");

        if (!Directory.Exists(distMacDir))
        {
            UnityEngine.Debug.LogError($"[Build][macOS] macOS dist directory not found: {distMacDir}");
            return;
        }

        // Expected XCFramework names in dist
        string sourceXcf1 = Path.Combine(distMacDir, $"mac-native-toolkit-{version}{xcfSuffix}.xcframework");
        string sourceXcf2 = Path.Combine(distMacDir, $"unity-mac-native-toolkit-{version}{xcfSuffix}.xcframework");

        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string destDir = Path.Combine(projectRoot, "Packages/com.jonghyunkim.nativetoolkit/Plugins/macOS");

        // Clean existing xcframeworks
        if (Directory.Exists(destDir))
        {
            foreach (string xcfDir in Directory.GetDirectories(destDir, "*.xcframework"))
            {
                Directory.Delete(xcfDir, true);
                UnityEngine.Debug.Log($"[Build][macOS] Deleted old XCFramework: {xcfDir}");
            }
        }
        else
        {
            Directory.CreateDirectory(destDir);
        }

        // Copy XCFrameworks
        if (Directory.Exists(sourceXcf1))
        {
            CopyDirectory(sourceXcf1, Path.Combine(destDir, Path.GetFileName(sourceXcf1)));
            UnityEngine.Debug.Log($"[Build][macOS] Copied {Path.GetFileName(sourceXcf1)}");
        }
        else
        {
            UnityEngine.Debug.LogWarning($"[Build][macOS] XCFramework not found: {sourceXcf1}");
        }

        if (Directory.Exists(sourceXcf2))
        {
            CopyDirectory(sourceXcf2, Path.Combine(destDir, Path.GetFileName(sourceXcf2)));
            UnityEngine.Debug.Log($"[Build][macOS] Copied {Path.GetFileName(sourceXcf2)}");
        }
        else
        {
            UnityEngine.Debug.LogWarning($"[Build][macOS] XCFramework not found: {sourceXcf2}");
        }

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

    // Sets Shift_JIS encoding for MSBuild process output to handle Japanese characters.
    private void TryApplyShiftJisEncoding(ProcessStartInfo startInfo)
    {
        try
        {
            startInfo.StandardOutputEncoding = Encoding.GetEncoding("shift_jis");
            startInfo.StandardErrorEncoding = Encoding.GetEncoding("shift_jis");
        }
        catch
        {
            // Encoding not available; fall back to default
        }
    }

    // Checks if build failure was caused by a post-build COM registration step (regsvr32) which is not critical.
    private bool ContainsRegsvrFailure(string output, string error)
    {
        return (output + error).Contains("regsvr32") || (output + error).Contains("REGSVR32");
    }
}
