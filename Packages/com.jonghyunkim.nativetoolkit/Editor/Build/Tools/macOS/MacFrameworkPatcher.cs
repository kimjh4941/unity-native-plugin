#if UNITY_EDITOR_OSX
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEditor.iOS.Xcode;
using UnityEditor.iOS.Xcode.Extensions;

/// <summary>
/// Utility to apply macOS XCFramework copy and Xcode project linking
/// to an exported Xcode project, mirroring the post-build macOS steps.
/// </summary>
public static class MacFrameworkPatcher
{
    private const string LogTag = "MacFrameworkPatcher";

    /// <summary>
    /// Apply XCFramework copy and link edits to the specified macOS Xcode project root.
    /// </summary>
    /// <param name="pathToBuiltProject">Xcode project root path (contains Mac.xcodeproj)</param>
    public static void Apply(string pathToBuiltProject)
    {
        Debug.Log($"[{LogTag}][{nameof(Apply)}] pathToBuiltProject: {pathToBuiltProject}");
        if (string.IsNullOrEmpty(pathToBuiltProject) || !Directory.Exists(pathToBuiltProject))
        {
            Debug.LogError("[NativeToolkit][macOS] Invalid Xcode project path.");
            EditorUtility.DisplayDialog("NativeToolkit (macOS) - Error", "Invalid Xcode project path.", "OK");
            return;
        }

        try
        {
            Debug.Log("[NativeToolkit][macOS] Applying XCFramework & PBXProject edits...");

            // XCFramework source and destination paths
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string pluginsMacDir = Path.Combine(projectRoot, "Packages/com.jonghyunkim.nativetoolkit/Plugins/macOS");

            string unityXcframeworkName = FindXcframeworkName(pluginsMacDir, "unity-mac-native-toolkit-", "[NativeToolkit][macOS]");

            if (string.IsNullOrEmpty(unityXcframeworkName))
            {
                throw new InvalidOperationException("Failed to resolve required macOS XCFramework name.");
            }

            string xcframeworkSrc = Path.Combine(pluginsMacDir, unityXcframeworkName);
            string frameworksDir = Path.Combine(pathToBuiltProject, "unity-native-plugin/Frameworks");
            string xcframeworkDst = Path.Combine(frameworksDir, unityXcframeworkName);

            if (!Directory.Exists(xcframeworkSrc))
                throw new DirectoryNotFoundException("[NativeToolkit][macOS] Source xcframework not found: " + xcframeworkSrc);

            // Copy XCFramework into app Frameworks folder
            if (Directory.Exists(xcframeworkDst))
                Directory.Delete(xcframeworkDst, true);
            Directory.CreateDirectory(frameworksDir);
            DirectoryCopy(xcframeworkSrc, xcframeworkDst, true);

            // Xcode project file path
            string pbxprojPath = Path.Combine(pathToBuiltProject, "Mac.xcodeproj", "project.pbxproj");
            if (!File.Exists(pbxprojPath))
                throw new FileNotFoundException("[NativeToolkit][macOS] Xcode project file not found: " + pbxprojPath);

            // Edit PBXProject to add XCFramework
            var proj = new PBXProject();
            proj.ReadFromFile(pbxprojPath);

            string targetGuid = proj.GetUnityMainTargetGuid();

            // Add XCFramework to Frameworks
            string relativePath = $"unity-native-plugin/Frameworks/{unityXcframeworkName}";
            proj.AddFileToBuild(targetGuid, proj.AddFile(relativePath, relativePath, PBXSourceTree.Source));

            proj.WriteToFile(pbxprojPath);

            Debug.Log($"[NativeToolkit][macOS] Added {unityXcframeworkName} to Xcode project.");
            Debug.Log("[NativeToolkit][macOS] XCFramework added successfully.");
            EditorUtility.DisplayDialog("NativeToolkit (macOS)", "XCFramework was added successfully.", "OK");
        }
        catch (Exception ex)
        {
            Debug.LogError("[NativeToolkit][macOS] Failed to apply XCFramework & PBX edits:\n" + ex);
            EditorUtility.DisplayDialog("NativeToolkit (macOS) - Error", "Failed to add XCFramework:\n" + ex.Message, "OK");
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
