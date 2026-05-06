#if UNITY_EDITOR_OSX
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEditor.iOS.Xcode;
using UnityEditor.iOS.Xcode.Extensions;

/// <summary>
/// Utility to apply iOS XCFramework copy and Xcode project linking/embedding
/// to an exported Xcode project, mirroring the post-build iOS steps.
/// </summary>
public static class IosFrameworkPatcher
{
    private const string LogTag = "IosFrameworkPatcher";

    /// <summary>
    /// Apply XCFramework copy and link/embed edits to the specified Xcode project root.
    /// </summary>
    /// <param name="pathToBuiltProject">Xcode project root path (contains Unity-iPhone.xcodeproj)</param>
    public static void Apply(string pathToBuiltProject)
    {
        Debug.Log($"[{LogTag}][{nameof(Apply)}] pathToBuiltProject: {pathToBuiltProject}");
        if (string.IsNullOrEmpty(pathToBuiltProject) || !Directory.Exists(pathToBuiltProject))
        {
            Debug.LogError("[NativeToolkit][iOS] Invalid Xcode project path.");
            EditorUtility.DisplayDialog("NativeToolkit (iOS) - Error", "Invalid Xcode project path.", "OK");
            return;
        }

        try
        {
            Debug.Log("[NativeToolkit][iOS] Applying XCFramework & PBXProject edits...");

            // XCFramework source and destination paths
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string pluginsIosDir = Path.Combine(projectRoot, "Packages/com.jonghyunkim.nativetoolkit/Plugins/iOS");

            string unityXcframeworkName = FindXcframeworkName(pluginsIosDir, "unity-ios-native-toolkit-", "[NativeToolkit][iOS][Unity]");
            string iosNativeToolkitXcframeworkName = FindXcframeworkName(pluginsIosDir, "ios-native-toolkit-", "[NativeToolkit][iOS][Native]");

            if (string.IsNullOrEmpty(unityXcframeworkName) || string.IsNullOrEmpty(iosNativeToolkitXcframeworkName))
            {
                throw new InvalidOperationException("Failed to resolve required iOS XCFramework names.");
            }

            string unityXcframeworkSrc = Path.Combine(pluginsIosDir, unityXcframeworkName);
            string iosNativeToolkitXcframeworkSrc = Path.Combine(pluginsIosDir, iosNativeToolkitXcframeworkName);

            string frameworksDir = Path.Combine(pathToBuiltProject, "Frameworks/com.jonghyunkim.nativetoolkit/Plugins/iOS");
            string unityXcframeworkDst = Path.Combine(frameworksDir, unityXcframeworkName);
            string iosNativeToolkitXcframeworkDst = Path.Combine(frameworksDir, iosNativeToolkitXcframeworkName);

            if (!Directory.Exists(unityXcframeworkSrc))
                throw new DirectoryNotFoundException("[NativeToolkit][iOS] Source xcframework not found: " + unityXcframeworkSrc);
            if (!Directory.Exists(iosNativeToolkitXcframeworkSrc))
                throw new DirectoryNotFoundException("[NativeToolkit][iOS] Source xcframework not found: " + iosNativeToolkitXcframeworkSrc);

            // Copy XCFrameworks into project
            if (Directory.Exists(unityXcframeworkDst))
                Directory.Delete(unityXcframeworkDst, true);
            if (Directory.Exists(iosNativeToolkitXcframeworkDst))
                Directory.Delete(iosNativeToolkitXcframeworkDst, true);
            Directory.CreateDirectory(frameworksDir);
            DirectoryCopy(unityXcframeworkSrc, unityXcframeworkDst, true);
            DirectoryCopy(iosNativeToolkitXcframeworkSrc, iosNativeToolkitXcframeworkDst, true);

            // Edit Xcode project to link and embed the XCFramework
            string pbxprojPath = Path.Combine(pathToBuiltProject, "Unity-iPhone.xcodeproj", "project.pbxproj");
            if (!File.Exists(pbxprojPath))
                throw new FileNotFoundException("[NativeToolkit][iOS] Xcode project file not found: " + pbxprojPath);

            var proj = new PBXProject();
            proj.ReadFromFile(pbxprojPath);

            string mainTargetGuid = proj.GetUnityMainTargetGuid();
            string frameworkTargetGuid = proj.GetUnityFrameworkTargetGuid();

            // Add XCFrameworks to Frameworks
            string unityRelativePath = $"Frameworks/com.jonghyunkim.nativetoolkit/Plugins/iOS/{unityXcframeworkName}";
            string unityFileGuid = proj.AddFile(unityRelativePath, unityRelativePath, PBXSourceTree.Source);

            string iosNativeToolkitRelativePath = $"Frameworks/com.jonghyunkim.nativetoolkit/Plugins/iOS/{iosNativeToolkitXcframeworkName}";
            string iosNativeToolkitFileGuid = proj.AddFile(iosNativeToolkitRelativePath, iosNativeToolkitRelativePath, PBXSourceTree.Source);

            // Link and embed the frameworks
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

            proj.WriteToFile(pbxprojPath);

            Debug.Log($"[NativeToolkit][iOS] Added {unityXcframeworkName} to Xcode project.");
            Debug.Log($"[NativeToolkit][iOS] Added {iosNativeToolkitXcframeworkName} to Xcode project.");
            Debug.Log("[NativeToolkit][iOS] XCFrameworks added and embedded successfully.");
            EditorUtility.DisplayDialog("NativeToolkit (iOS)", "XCFrameworks were added and embedded successfully.", "OK");
        }
        catch (System.Exception ex)
        {
            Debug.LogError("[NativeToolkit][iOS] Failed to apply XCFramework & PBX edits:\n" + ex);
            EditorUtility.DisplayDialog("NativeToolkit (iOS) - Error", "Failed to add/embed XCFramework:\n" + ex.Message, "OK");
        }
    }

    private static string FindXcframeworkName(string pluginsDir, string prefix, string logPrefix)
    {
        if (!Directory.Exists(pluginsDir))
        {
            Debug.LogError($"{logPrefix} Plugin directory not found: {pluginsDir}");
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
            if (isDebugName)
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
            Debug.LogError($"{logPrefix} XCFramework not found. prefix={prefix}, pluginsDir={pluginsDir}, available={available}");
            return null;
        }

        if (hasMultipleMatches)
        {
            Debug.LogWarning($"{logPrefix} Multiple XCFramework matches found. Selected: {selectedName}");
        }

        Debug.Log($"{logPrefix} Selected XCFramework: {selectedName}");
        return selectedName;
    }

    private static void DirectoryCopy(string sourceDir, string destDir, bool copySubDirs)
    {
        var dir = new DirectoryInfo(sourceDir);
        var dirs = dir.GetDirectories();
        Directory.CreateDirectory(destDir);
        foreach (var file in dir.GetFiles())
        {
            string tempPath = Path.Combine(destDir, file.Name);
            file.CopyTo(tempPath, true);
        }
        if (copySubDirs)
        {
            foreach (var subdir in dirs)
            {
                string tempPath = Path.Combine(destDir, subdir.Name);
                DirectoryCopy(subdir.FullName, tempPath, copySubDirs);
            }
        }
    }
}
#endif
