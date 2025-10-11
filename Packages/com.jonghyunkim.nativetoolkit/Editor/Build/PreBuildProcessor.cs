using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System.Diagnostics;
using System.IO;

/// <summary>
/// Pre-build processor that (1) temporarily disables libraries for non-target platforms to keep
/// the build artifact clean, and (2) triggers native plugin build pipelines per platform (Android,
/// iOS, macOS, Windows) so that the latest native binaries are copied under <c>Assets/Plugins</c>.
/// </summary>
public class PreBuildProcessor : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    // Determine config from build options
    private static string GetConfigName(BuildReport report)
        => (report.summary.options & BuildOptions.Development) != 0 ? "Debug" : "Release";

    /// <summary>
    /// Entry point executed before the player build starts. Routes to platform‑specific native
    /// build helpers after cleaning up unrelated plugin folders.
    /// </summary>
    public void OnPreprocessBuild(BuildReport report)
    {
        // Temporarily disable libraries for non-target platforms before building
        CleanupOtherPlatformLibraries(report.summary.platform);

        var config = GetConfigName(report);
        UnityEngine.Debug.Log($"[Build] Configuration: {config}");

        if (report.summary.platform == BuildTarget.Android)
        {
            BuildAndroidLibraries(config);
        }
        else if (report.summary.platform == BuildTarget.iOS)
        {
            BuildiOSLibraries(config);
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
            BuildmacOSLibraries(config);
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
        switch (targetPlatform)
        {
            case BuildTarget.iOS:
                return libraryPath.Contains("iOS");
            case BuildTarget.StandaloneOSX:
                return libraryPath.Contains("macOS");
            case BuildTarget.Android:
                return libraryPath.Contains("Android");
            case BuildTarget.StandaloneWindows64:
                return libraryPath.Contains("Windows");
            default:
                return false; // Disable all libraries for unsupported platforms
        }
    }

    /// <summary>
    /// Builds Android AAR libraries (Debug/Release) and copies them to Plugins/Android/Library.
    /// </summary>
    private void BuildAndroidLibraries(string config)
    {
        UnityEngine.Debug.Log($"[Build][Android] Pre-build steps started. Config={config}");

        string androidRootProjectPath = "/Users/jonghyunkim/Desktop/native-toolkit/android/AndroidLibraryExample";
        string androidLibraryProjectPath = "/Users/jonghyunkim/Desktop/native-toolkit/android/android_library";
        string unityAndroidPluginProjectPath = "/Users/jonghyunkim/Desktop/native-toolkit/android/unity_android_plugin";

        // assembleDebug or assembleRelease
        RunShellCommand($"cd \"{androidRootProjectPath}\" && ./gradlew :android_library:assemble{config}");
        RunShellCommand($"cd \"{androidRootProjectPath}\" && ./gradlew :unity_android_plugin:assemble{config}");

        // suffix = debug / release
        string suffix = config.ToLowerInvariant();

        // original artifact paths produced by Gradle
        string builtAar1 = Path.Combine(androidLibraryProjectPath, "build", "outputs", "aar", $"android_library-{suffix}.aar");
        string builtAar2 = Path.Combine(unityAndroidPluginProjectPath, "build", "outputs", "aar", $"unity_android_plugin-{suffix}.aar");

        // desired names
        string desiredAar1 = Path.Combine(Path.GetDirectoryName(builtAar1)!, config.Equals("Debug", System.StringComparison.OrdinalIgnoreCase) ? $"android_nativetoolkit-{suffix}.aar" : $"android_nativetoolkit.aar");
        string desiredAar2 = Path.Combine(Path.GetDirectoryName(builtAar2)!, config.Equals("Debug", System.StringComparison.OrdinalIgnoreCase) ? $"unity_android_nativetoolkit-{suffix}.aar" : $"unity_android_nativetoolkit.aar");

        // destination directory in Unity project
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string destDir = Path.Combine(projectRoot, "Packages/com.jonghyunkim.nativetoolkit/Plugins/Android");
        // ensure clean destination
        if (Directory.Exists(destDir))
        {
            Directory.Delete(destDir, true);
        }
        Directory.CreateDirectory(destDir);

        // rename if exists
        RunShellCommand($"if [ -f \"{builtAar1}\" ]; then mv \"{builtAar1}\" \"{desiredAar1}\"; fi");
        RunShellCommand($"if [ -f \"{builtAar2}\" ]; then mv \"{builtAar2}\" \"{desiredAar2}\"; fi");

        // copy to package plugin folder (example)
        RunShellCommand($"cp -f \"{desiredAar1}\" \"{destDir}\"");
        RunShellCommand($"cp -f \"{desiredAar2}\" \"{destDir}\"");

        UnityEngine.Debug.Log($"[Build][Android] Copied AARs (config={config}) to {destDir}");
        UnityEngine.Debug.Log("[Build][Android] Pre-build steps completed.");
    }

    /// <summary>
    /// Builds iOS XCFramework (Debug/Release) and copies it to Plugins/iOS/Library.
    /// </summary>
    private void BuildiOSLibraries(string config)
    {
        UnityEngine.Debug.Log($"[Build][iOS] Pre-build steps started. Config={config}");

        string workspacePath = "/Users/jonghyunkim/Desktop/native-toolkit/ios/IosWorkspace.xcworkspace";
        string scheme = "UnityIosPlugin";

        RunShellCommand($"xcodebuild clean -workspace \"{workspacePath}\" -scheme \"{scheme}\" -configuration {config}");

        string archivePath = "/Users/jonghyunkim/Desktop/native-toolkit-outputs/ios/UnityIosPlugin.xcarchive";
        RunShellCommand($"xcodebuild archive -workspace \"{workspacePath}\" -scheme \"{scheme}\" -archivePath \"{archivePath}\" -sdk iphoneos -configuration {config} SKIP_INSTALL=NO BUILD_LIBRARY_FOR_DISTRIBUTION=YES");

        string xcframeworkPath = Path.Combine("/Users/jonghyunkim/Desktop/native-toolkit-outputs/ios", config.Equals("Debug", System.StringComparison.OrdinalIgnoreCase) ? $"UnityIosNativeToolkit-{config}.xcframework" : "UnityIosNativeToolkit.xcframework");
        if (Directory.Exists(xcframeworkPath))
        {
            Directory.Delete(xcframeworkPath, true);
        }

        RunShellCommand($"xcodebuild -create-xcframework -framework \"{archivePath}\"/Products/Library/Frameworks/UnityIosPlugin.framework -output \"{xcframeworkPath}\"");

        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string destDir = Path.Combine(projectRoot, "Packages/com.jonghyunkim.nativetoolkit/Plugins/iOS");

        RunShellCommand($"rm -rf \"{destDir}\"/*");
        RunShellCommand($"cp -R \"{xcframeworkPath}\" \"{destDir}\"");

        UnityEngine.Debug.Log($"[Build][iOS] Copied UnityIosNativeToolkit.xcframework (config={config}) to {destDir}");
        UnityEngine.Debug.Log("[Build][iOS] Pre-build steps completed.");

        string assetPath = "Packages/com.jonghyunkim.nativetoolkit/Plugins/iOS/" + (config.Equals("Debug", System.StringComparison.OrdinalIgnoreCase) ? "UnityIosNativeToolkit-Debug.xcframework" : "UnityIosNativeToolkit.xcframework");
        // Apply plugin import settings (enable only for iOS)
        ConfigureIosXcframeworkImporter(assetPath);
    }

    /// <summary>
    /// Copies Windows DLL built for Debug/Release to Plugins/Windows/Library.
    /// </summary>
    private void BuildWindowsLibraries(string config)
    {
        UnityEngine.Debug.Log($"[Build][Windows] Pre-build steps started. Config={config}");

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
    /// Builds macOS XCFramework (Debug/Release) and copies it to Plugins/macOS/Library.
    /// </summary>
    private void BuildmacOSLibraries(string config)
    {
        UnityEngine.Debug.Log($"[Build][macOS] Pre-build steps started. Config={config}");

        string workspacePath = "/Users/jonghyunkim/Desktop/native-toolkit/mac/MacWorkspace.xcworkspace";
        string scheme = "UnityMacPlugin";

        RunShellCommand($"xcodebuild clean -workspace \"{workspacePath}\" -scheme \"{scheme}\" -configuration {config}");

        string archivePath = "/Users/jonghyunkim/Desktop/native-toolkit-outputs/mac/UnityMacPlugin.xcarchive";
        RunShellCommand($"xcodebuild archive -workspace \"{workspacePath}\" -scheme \"{scheme}\" -archivePath \"{archivePath}\" -sdk macosx -configuration {config} SKIP_INSTALL=NO BUILD_LIBRARY_FOR_DISTRIBUTION=YES");

        string xcframeworkPath = Path.Combine("/Users/jonghyunkim/Desktop/native-toolkit-outputs/mac", config.Equals("Debug", System.StringComparison.OrdinalIgnoreCase) ? $"UnityMacNativeToolkit-{config}.xcframework" : "UnityMacNativeToolkit.xcframework");
        if (Directory.Exists(xcframeworkPath))
        {
            Directory.Delete(xcframeworkPath, true);
        }

        RunShellCommand($"xcodebuild -create-xcframework -framework \"{archivePath}\"/Products/Library/Frameworks/UnityMacPlugin.framework -output \"{xcframeworkPath}\"");

        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string destDir = Path.Combine(projectRoot, "Packages/com.jonghyunkim.nativetoolkit/Plugins/macOS");
        RunShellCommand($"rm -rf \"{destDir}\"/*");
        RunShellCommand($"cp -R \"{xcframeworkPath}\" \"{destDir}\"");

        UnityEngine.Debug.Log($"[Build][macOS] Copied UnityMacNativeToolkit.xcframework (config={config}) to {destDir}");
        UnityEngine.Debug.Log("[Build][macOS] Pre-build steps completed.");

        string assetPath = "Packages/com.jonghyunkim.nativetoolkit/Plugins/macOS/" + (config.Equals("Debug", System.StringComparison.OrdinalIgnoreCase) ? "UnityMacNativeToolkit-Debug.xcframework" : "UnityMacNativeToolkit.xcframework");
        // Apply plugin import settings (enable only for macOS)
        ConfigureMacXcframeworkImporter(assetPath);
    }

    /// <summary>
    /// Executes a shell command and throws a <see cref="BuildFailedException"/> if the command fails.
    /// </summary>
    /// <param name="command">The shell command to execute.</param>
    private void RunShellCommand(string command)
    {
        var process = new Process();
        process.StartInfo.FileName = "/bin/bash";
        process.StartInfo.Arguments = $"-c \"{command}\"";
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.CreateNoWindow = true;
        process.Start();
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new BuildFailedException($"Command failed: {command}\n{error}");
        }
        else
        {
            UnityEngine.Debug.Log(output);
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
