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

    /// <summary>
    /// Entry point executed before the player build starts. Routes to platform‑specific native
    /// build helpers after cleaning up unrelated plugin folders.
    /// </summary>
    public void OnPreprocessBuild(BuildReport report)
    {
        // Temporarily disable libraries for non-target platforms before building
        CleanupOtherPlatformLibraries(report.summary.platform);

        if (report.summary.platform == BuildTarget.Android)
        {
            BuildAndroidLibraries();
        }
        else if (report.summary.platform == BuildTarget.iOS)
        {
            BuildiOSLibraries();
        }
        else if (report.summary.platform == BuildTarget.StandaloneWindows64)
        {
            BuildWindowsLibraries();
        }
        else if (report.summary.platform == BuildTarget.StandaloneOSX)
        {
#if UNITY_2021_3_OR_NEWER
            // Unity 6: Disable player log for macOS if still available in this version
            PlayerSettings.usePlayerLog = false;
            UnityEngine.Debug.Log("[Build] Set PlayerSettings.usePlayerLog = false for macOS");
#endif
            BuildmacOSLibraries();
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
    /// Builds Android AAR libraries by executing Gradle commands in the native toolkit project
    /// and copies the resulting artifacts to the Unity Plugins folder.
    /// </summary>
    private void BuildAndroidLibraries()
    {
        UnityEngine.Debug.Log("[Build][Android] Pre-build steps started.");

        // 1. Gradle wrapper is located in android/AndroidLibraryExample directory
        string androidRootProjectPath = "/Users/jonghyunkim/Desktop/native-toolkit/android/AndroidLibraryExample";

        // 2. Build android_library-debug.aar
        string androidLibraryProjectPath = "/Users/jonghyunkim/Desktop/native-toolkit/android/android_library";
        RunShellCommand(
            $"cd \"{androidRootProjectPath}\" && ./gradlew :android_library:assembleDebug"
        );
        string aarSrc1 = Path.Combine(androidLibraryProjectPath, "build", "outputs", "aar", "android_library-debug.aar");

        // 3. Build unity_android_plugin-debug.aar
        string unityAndroidPluginProjectPath = "/Users/jonghyunkim/Desktop/native-toolkit/android/unity_android_plugin";
        RunShellCommand(
            $"cd \"{androidRootProjectPath}\" && ./gradlew :unity_android_plugin:assembleDebug"
        );
        string aarSrc2 = Path.Combine(unityAndroidPluginProjectPath, "build", "outputs", "aar", "unity_android_plugin-debug.aar");

        // 4. Copy to Plugins/Android/Library folder
        string destDir = Path.Combine(Application.dataPath, "Plugins/Android/Library");
        Directory.CreateDirectory(destDir);

        RunShellCommand($"cp -f \"{aarSrc1}\" \"{destDir}\"");
        RunShellCommand($"cp -f \"{aarSrc2}\" \"{destDir}\"");

        UnityEngine.Debug.Log("[Build][Android] Copied android_library-debug.aar & unity_android_plugin-debug.aar to Plugins/Android/Library");
        UnityEngine.Debug.Log("[Build][Android] Pre-build steps completed.");
    }

    /// <summary>
    /// Builds iOS XCFramework library by executing Xcode commands and copies the resulting
    /// framework to the Unity Plugins folder.
    /// </summary>
    private void BuildiOSLibraries()
    {
        UnityEngine.Debug.Log("[Build][iOS] Pre-build steps started.");

        // 1. Execute Xcode clean command
        string workspacePath = "/Users/jonghyunkim/Desktop/native-toolkit/ios/IosWorkspace.xcworkspace";
        string scheme = "UnityIosPlugin";
        RunShellCommand(
            $"xcodebuild clean -workspace \"{workspacePath}\" -scheme \"{scheme}\""
        );

        // 2. Create UnityIosPlugin library archive
        string archivePath = "/Users/jonghyunkim/Desktop/native-toolkit-outputs/ios/UnityIosPlugin.xcarchive";
        RunShellCommand(
            $"xcodebuild archive -workspace \"{workspacePath}\" -scheme \"{scheme}\" -archivePath \"{archivePath}\" -sdk iphoneos SKIP_INSTALL=NO BUILD_LIBRARY_FOR_DISTRIBUTION=YES"
        );

        // 3. Create XCFramework
        string xcframeworkPath = "/Users/jonghyunkim/Desktop/native-toolkit-outputs/ios/UnityIosPlugin.xcframework";

        // 4. Remove existing XCFramework before creating new one
        if (Directory.Exists(xcframeworkPath))
        {
            Directory.Delete(xcframeworkPath, true);
        }
        RunShellCommand(
            $"xcodebuild -create-xcframework -framework \"{archivePath}\"/Products/Library/Frameworks/UnityIosPlugin.framework -output \"{xcframeworkPath}\""
        );

        // 5. Copy XCFramework to Unity Plugins/iOS/Library folder
        string destDir = Path.Combine(Application.dataPath, "Plugins/iOS/Library");
        Directory.CreateDirectory(destDir);
        RunShellCommand($"cp -R \"{xcframeworkPath}\" \"{destDir}\"");

        UnityEngine.Debug.Log("[Build][iOS] Copied UnityIosPlugin.xcframework to Plugins/iOS/Library");
        UnityEngine.Debug.Log("[Build][iOS] Pre-build steps completed.");
    }

    /// <summary>
    /// Builds Windows DLL library and copies it to the Unity Plugins folder.
    /// </summary>
    private void BuildWindowsLibraries()
    {
        UnityEngine.Debug.Log("[Build][Windows] Pre-build steps started.");

        // Source and destination paths
        string dllSrc = @"C:\Users\User\Desktop\native-toolkit\windows\WindowsLibraryExample\x64\Debug\WindowsLibraryExample\AppX\WindowsLibrary.dll";
        string destDir = Path.Combine(Application.dataPath, "Plugins/Windows/Library");
        string dllDst = Path.Combine(destDir, "WindowsLibrary.dll");

        try
        {
            Directory.CreateDirectory(destDir);
            File.Copy(dllSrc, dllDst, true);
            UnityEngine.Debug.Log("[Build][Windows] Copied WindowsLibrary.dll to Plugins/Windows/Library");
        }
        catch (System.Exception ex)
        {
            UnityEngine.Debug.LogWarning("[Build][Windows] Failed to copy WindowsLibrary.dll: " + ex.Message);
        }

        UnityEngine.Debug.Log("[Build][Windows] Pre-build steps completed.");
    }

    /// <summary>
    /// Builds macOS XCFramework library by executing Xcode commands and copies the resulting
    /// framework to the Unity Plugins folder.
    /// </summary>
    private void BuildmacOSLibraries()
    {
        UnityEngine.Debug.Log("[Build][macOS] Pre-build steps started.");

        // 1. Execute Xcode clean command
        string workspacePath = "/Users/jonghyunkim/Desktop/native-toolkit/mac/MacWorkspace.xcworkspace";
        string scheme = "UnityMacPlugin";
        RunShellCommand(
            $"xcodebuild clean -workspace \"{workspacePath}\" -scheme \"{scheme}\""
        );

        // 2. Create UnityMacPlugin library archive
        string archivePath = "/Users/jonghyunkim/Desktop/native-toolkit-outputs/mac/UnityMacPlugin.xcarchive";
        RunShellCommand(
            $"xcodebuild archive -workspace \"{workspacePath}\" -scheme \"{scheme}\" -archivePath \"{archivePath}\" -sdk macosx SKIP_INSTALL=NO BUILD_LIBRARY_FOR_DISTRIBUTION=YES"
        );

        // 3. Create XCFramework
        string xcframeworkPath = "/Users/jonghyunkim/Desktop/native-toolkit-outputs/mac/UnityMacPlugin.xcframework";

        // 4. Remove existing XCFramework before creating new one
        if (Directory.Exists(xcframeworkPath))
        {
            Directory.Delete(xcframeworkPath, true);
        }
        RunShellCommand(
            $"xcodebuild -create-xcframework -framework \"{archivePath}\"/Products/Library/Frameworks/UnityMacPlugin.framework -output \"{xcframeworkPath}\""
        );

        // 5. Copy XCFramework to Unity Plugins/macOS/Library folder
        string destDir = Path.Combine(Application.dataPath, "Plugins/macOS/Library");
        Directory.CreateDirectory(destDir);
        RunShellCommand($"cp -R \"{xcframeworkPath}\" \"{destDir}\"");

        UnityEngine.Debug.Log("[Build][macOS] Copied UnityMacPlugin.xcframework to Plugins/macOS/Library");
        UnityEngine.Debug.Log("[Build][macOS] Pre-build steps completed.");
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
}
