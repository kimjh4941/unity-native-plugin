using UnityEditor;
using UnityEditor.Callbacks;
#if UNITY_IOS || UNITY_EDITOR_OSX
using UnityEditor.iOS.Xcode;
using UnityEditor.iOS.Xcode.Extensions;
#endif
using System;
using System.IO;
using System.Diagnostics;
using UnityEditor.Build;

/// <summary>
/// Post-build processor that handles platform-specific setup after Unity builds complete.
/// For macOS builds, copies XCFramework and modifies the Xcode project.
/// Windows builds need no post-processing: the DLLs are placed before the build by
/// PreBuildProcessor. (A development build used to copy a PDB from an absolute path in the
/// native-toolkit checkout; that path no longer resolved and dist publishes no PDB, so the copy
/// was removed.)
/// Android builds need none either. This used to apply the Kotlin Gradle plugin and kotlin-stdlib
/// 2.0.21 after the build, which was both too late for the dependency resolution of the build
/// itself and wrong for the 2.0.0 AARs (they need kotlin-stdlib 2.2 or later and no Kotlin
/// compilation). Their runtime dependencies are added before Gradle runs, by
/// Editor/Android/AndroidGradleDependencies.cs.
/// </summary>
public static class PostBuildProcessor
{
    /// <summary>
    /// Main entry point executed after the player build completes. Routes to platform-specific
    /// post-processing based on the build target.
    /// </summary>
    /// <param name="target">The build target platform.</param>
    /// <param name="pathToBuiltProject">Path to the built project.</param>
    [PostProcessBuild]
    public static void OnPostProcessBuild(BuildTarget target, string pathToBuiltProject)
    {
#if UNITY_IOS
        if (target == BuildTarget.iOS)
        {
            UnityEngine.Debug.Log("[Build][iOS] Post-build steps started.");

            bool isDevelopmentBuild = EditorUserBuildSettings.development;
            string xcfSuffix = isDevelopmentBuild ? "-debug" : "";
            // XCFramework source and destination paths
            string projectRoot = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."));
            string pluginsIosDir = Path.Combine(projectRoot, "Packages/com.jonghyunkim.nativetoolkit/Plugins/iOS");

            string unityXcframeworkName = FindXcframeworkName(pluginsIosDir, "unity-ios-native-toolkit-", xcfSuffix, "[Build][iOS][Unity]", isDevelopmentBuild);
            string iosNativeToolkitXcframeworkName = FindXcframeworkName(pluginsIosDir, "ios-native-toolkit-", xcfSuffix, "[Build][iOS][Native]", isDevelopmentBuild);

            if (string.IsNullOrEmpty(unityXcframeworkName) || string.IsNullOrEmpty(iosNativeToolkitXcframeworkName))
            {
                return;
            }

            string unityXcframeworkSrc = Path.Combine(pluginsIosDir, unityXcframeworkName);
            string iosNativeToolkitXcframeworkSrc = Path.Combine(pluginsIosDir, iosNativeToolkitXcframeworkName);
            string frameworksDir = Path.Combine(pathToBuiltProject, "Frameworks/com.jonghyunkim.nativetoolkit/Plugins/iOS");
            string unityXcframeworkDst = Path.Combine(frameworksDir, unityXcframeworkName);
            string iosNativeToolkitXcframeworkDst = Path.Combine(frameworksDir, iosNativeToolkitXcframeworkName);

            if (!Directory.Exists(unityXcframeworkSrc))
            {
                UnityEngine.Debug.LogError("[Build][iOS] Source xcframework not found: " + unityXcframeworkSrc);
                return;
            }

            if (!Directory.Exists(iosNativeToolkitXcframeworkSrc))
            {
                UnityEngine.Debug.LogError("[Build][iOS] Source xcframework not found: " + iosNativeToolkitXcframeworkSrc);
                return;
            }

            // Ensure clean destination
            if (Directory.Exists(unityXcframeworkDst))
            {
                Directory.Delete(unityXcframeworkDst, true);
            }

            if (Directory.Exists(iosNativeToolkitXcframeworkDst))
            {
                Directory.Delete(iosNativeToolkitXcframeworkDst, true);
            }
            Directory.CreateDirectory(frameworksDir);
            // Copy XCFrameworks into project
            DirectoryCopy(unityXcframeworkSrc, unityXcframeworkDst, true);
            DirectoryCopy(iosNativeToolkitXcframeworkSrc, iosNativeToolkitXcframeworkDst, true);

            // Edit Xcode project to link and embed the XCFramework
            string pbxprojPath = Path.Combine(pathToBuiltProject, "Unity-iPhone.xcodeproj", "project.pbxproj");
            if (!File.Exists(pbxprojPath))
            {
                UnityEngine.Debug.LogError("[Build][iOS] Xcode project file not found: " + pbxprojPath);
                return;
            }

            var proj = new PBXProject();
            proj.ReadFromFile(pbxprojPath);

            string mainTargetGuid = proj.GetUnityMainTargetGuid();
            string frameworkTargetGuid = proj.GetUnityFrameworkTargetGuid();

            // Add XCFrameworks to Frameworks
            string unityRelativePath = $"Frameworks/com.jonghyunkim.nativetoolkit/Plugins/iOS/{unityXcframeworkName}";
            string unityFileGuid = proj.AddFile(unityRelativePath, unityRelativePath, PBXSourceTree.Source);

            string iosNativeToolkitRelativePath = $"Frameworks/com.jonghyunkim.nativetoolkit/Plugins/iOS/{iosNativeToolkitXcframeworkName}";
            string iosNativeToolkitFileGuid = proj.AddFile(iosNativeToolkitRelativePath, iosNativeToolkitRelativePath, PBXSourceTree.Source);

            // Link and embed the framework
            proj.AddFileToBuild(frameworkTargetGuid, unityFileGuid);
            PBXProjectExtensions.AddFileToEmbedFrameworks(proj, mainTargetGuid, unityFileGuid);

            proj.AddFileToBuild(frameworkTargetGuid, iosNativeToolkitFileGuid);
            PBXProjectExtensions.AddFileToEmbedFrameworks(proj, mainTargetGuid, iosNativeToolkitFileGuid);

            // Search paths / Run paths (minimum necessary)
            proj.AddBuildProperty(frameworkTargetGuid, "FRAMEWORK_SEARCH_PATHS", "$(inherited)");
            proj.AddBuildProperty(frameworkTargetGuid, "FRAMEWORK_SEARCH_PATHS", "$(PROJECT_DIR)/Frameworks/**");
            proj.AddBuildProperty(frameworkTargetGuid, "LD_RUNPATH_SEARCH_PATHS", "$(inherited)");
            proj.AddBuildProperty(frameworkTargetGuid, "LD_RUNPATH_SEARCH_PATHS", "@executable_path/Frameworks");
            proj.AddBuildProperty(frameworkTargetGuid, "LD_RUNPATH_SEARCH_PATHS", "@loader_path/Frameworks");

            proj.AddBuildProperty(mainTargetGuid, "LD_RUNPATH_SEARCH_PATHS", "@loader_path/Frameworks");

            UnityEngine.Debug.Log($"[Build][iOS] Added {unityXcframeworkName} to Xcode project.");
            UnityEngine.Debug.Log($"[Build][iOS] Added {iosNativeToolkitXcframeworkName} to Xcode project.");

            proj.WriteToFile(pbxprojPath);
            UnityEngine.Debug.Log("[Build][iOS] Post-build steps completed.");
        }
#endif

#if UNITY_EDITOR_OSX
        if (target == BuildTarget.StandaloneOSX)
        {
            UnityEngine.Debug.Log("[Build][macOS] Post-build steps started.");

            bool isDevelopmentBuild = EditorUserBuildSettings.development;
            string xcfSuffix = isDevelopmentBuild ? "-debug" : "";
            string projectRoot = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."));
            string pluginsMacDir = Path.Combine(projectRoot, "Packages/com.jonghyunkim.nativetoolkit/Plugins/macOS");

            string unityXcframeworkName = FindXcframeworkName(pluginsMacDir, "unity-mac-native-toolkit-", xcfSuffix, "[Build][macOS]", isDevelopmentBuild);

            if (string.IsNullOrEmpty(unityXcframeworkName))
            {
                return;
            }

            // XCFramework source and destination paths
            string xcframeworkSrc = Path.Combine(pluginsMacDir, unityXcframeworkName);
            string frameworksDir = Path.Combine(pathToBuiltProject, "unity-native-plugin/Frameworks");
            string xcframeworkDst = Path.Combine(frameworksDir, unityXcframeworkName);

            if (!Directory.Exists(xcframeworkSrc))
            {
                UnityEngine.Debug.LogError("[Build][macOS] Source xcframework not found: " + xcframeworkSrc);
                return;
            }

            // Ensure clean destination
            if (Directory.Exists(xcframeworkDst))
            {
                Directory.Delete(xcframeworkDst, true);
            }
            Directory.CreateDirectory(frameworksDir);
            // Copy XCFramework into app Frameworks folder
            DirectoryCopy(xcframeworkSrc, xcframeworkDst, true);

            // Xcode project file path
            string pbxprojPath = Path.Combine(pathToBuiltProject, "Mac.xcodeproj", "project.pbxproj");
            if (!File.Exists(pbxprojPath))
            {
                UnityEngine.Debug.LogError("[Build][macOS] Xcode project file not found: " + pbxprojPath);
                return;
            }

            // Edit PBXProject to add XCFramework
            var proj = new PBXProject();
            proj.ReadFromFile(pbxprojPath);

            string targetGuid = proj.GetUnityMainTargetGuid();

            // Add XCFramework to Frameworks
            string relativePath = $"unity-native-plugin/Frameworks/{unityXcframeworkName}";
            string fileGuid = proj.AddFile(relativePath, relativePath, PBXSourceTree.Source);

            // Link and embed. Linking alone leaves the binary asking for
            // @rpath/UnityMacPlugin.framework with nothing at that path inside the bundle: Xcode
            // resolves it from its own build products directory, so Run works and the .app fails
            // to launch anywhere else with a dyld "Library not loaded" abort.
            proj.AddFileToBuild(targetGuid, fileGuid);
            PBXProjectExtensions.AddFileToEmbedFrameworks(proj, targetGuid, fileGuid);

            // Search paths / Run paths, matching the iOS branch above.
            proj.AddBuildProperty(targetGuid, "FRAMEWORK_SEARCH_PATHS", "$(inherited)");
            proj.AddBuildProperty(targetGuid, "FRAMEWORK_SEARCH_PATHS", "$(PROJECT_DIR)/unity-native-plugin/Frameworks");
            proj.AddBuildProperty(targetGuid, "LD_RUNPATH_SEARCH_PATHS", "$(inherited)");
            proj.AddBuildProperty(targetGuid, "LD_RUNPATH_SEARCH_PATHS", "@executable_path/../Frameworks");
            proj.AddBuildProperty(targetGuid, "LD_RUNPATH_SEARCH_PATHS", "@loader_path/../Frameworks");

            proj.WriteToFile(pbxprojPath);

            UnityEngine.Debug.Log($"[Build][macOS] Added {unityXcframeworkName} to Xcode project.");
            UnityEngine.Debug.Log("[Build][macOS] Post-build steps completed.");
        }
#endif
    }

    private static string FindXcframeworkName(string pluginsDir, string prefix, string xcfSuffix, string logPrefix, bool isDevelopmentBuild)
    {
        if (!Directory.Exists(pluginsDir))
        {
            UnityEngine.Debug.LogError($"{logPrefix} Plugin directory not found: {pluginsDir}");
            return null;
        }

        string selectedName = null;
        bool hasMultipleMatches = false;
        string[] candidates = Directory.GetDirectories(pluginsDir, "*.xcframework");

        foreach (string candidatePath in candidates)
        {
            string candidateName = Path.GetFileName(candidatePath);
            if (!candidateName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            bool isDebugName = candidateName.EndsWith("-debug.xcframework", StringComparison.OrdinalIgnoreCase);
            bool suffixMatched;

            if (isDevelopmentBuild)
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

        if (selectedName == null)
        {
            string available = string.Join(", ", candidates);
            UnityEngine.Debug.LogError($"{logPrefix} XCFramework not found. prefix={prefix}, xcfSuffix={xcfSuffix}, isDevelopmentBuild={isDevelopmentBuild}, pluginsDir={pluginsDir}, available={available}");
            return null;
        }

        if (hasMultipleMatches)
        {
            UnityEngine.Debug.LogWarning($"{logPrefix} Multiple XCFramework matches found. Selected: {selectedName}");
        }

        UnityEngine.Debug.Log($"{logPrefix} Selected XCFramework: {selectedName}");
        return selectedName;
    }

    /// <summary>
    /// Helper method to recursively copy directories and their contents.
    /// Creates the destination directory if it doesn't exist and copies all files.
    /// Optionally copies subdirectories recursively.
    /// </summary>
    /// <param name="sourceDir">Source directory path to copy from</param>
    /// <param name="destDir">Destination directory path to copy to</param>
    /// <param name="copySubDirs">Whether to recursively copy subdirectories</param>
    private static void DirectoryCopy(string sourceDir, string destDir, bool copySubDirs)
    {
        DirectoryInfo dir = new DirectoryInfo(sourceDir);
        DirectoryInfo[] dirs = dir.GetDirectories();

        Directory.CreateDirectory(destDir);

        foreach (FileInfo file in dir.GetFiles())
        {
            string tempPath = Path.Combine(destDir, file.Name);
            file.CopyTo(tempPath, true);
        }

        if (copySubDirs)
        {
            foreach (DirectoryInfo subdir in dirs)
            {
                string tempPath = Path.Combine(destDir, subdir.Name);
                DirectoryCopy(subdir.FullName, tempPath, copySubDirs);
            }
        }
    }

    /// <summary>
    /// Executes a shell command and throws a <see cref="BuildFailedException"/> if the command fails.
    /// </summary>
    /// <param name="command">The shell command to execute.</param>
    private static void RunShellCommand(string command)
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