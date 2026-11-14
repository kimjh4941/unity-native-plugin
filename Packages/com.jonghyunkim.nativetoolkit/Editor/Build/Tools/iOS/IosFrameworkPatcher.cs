#if UNITY_EDITOR_OSX
using System;
using System.Collections.Generic;
using System.IO;
using JonghyunKim.NativeToolkit.Editor.iOS;
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

            // The XCFrameworks named in Plugins/iOS/VERSION.txt, matched exactly, in the form the last
            // build left there (a development build leaves the -debug one when dist publishes it); throws
            // when the pin or a pinned XCFramework is missing.
            IosXcframeworkPin pin = IosXcframeworkPin.Read(Path.Combine(pluginsIosDir, IosXcframeworkPin.FileName));
            IReadOnlyList<string> xcframeworkNames = pin.ResolvePresentIn(pluginsIosDir);

            const string frameworksRelativeDir = "Frameworks/com.jonghyunkim.nativetoolkit/Plugins/iOS";
            string frameworksDir = Path.Combine(pathToBuiltProject, frameworksRelativeDir);

            // Copy XCFrameworks into project
            Directory.CreateDirectory(frameworksDir);
            foreach (string name in xcframeworkNames)
            {
                string destination = Path.Combine(frameworksDir, name);
                if (Directory.Exists(destination))
                    Directory.Delete(destination, true);
                DirectoryCopy(Path.Combine(pluginsIosDir, name), destination, true);
            }

            // Edit Xcode project to link and embed the XCFramework
            string pbxprojPath = Path.Combine(pathToBuiltProject, "Unity-iPhone.xcodeproj", "project.pbxproj");
            if (!File.Exists(pbxprojPath))
                throw new FileNotFoundException("[NativeToolkit][iOS] Xcode project file not found: " + pbxprojPath);

            var proj = new PBXProject();
            proj.ReadFromFile(pbxprojPath);

            string mainTargetGuid = proj.GetUnityMainTargetGuid();
            string frameworkTargetGuid = proj.GetUnityFrameworkTargetGuid();

            // Add each XCFramework to Frameworks, link it to UnityFramework and embed it in the app
            foreach (string name in xcframeworkNames)
            {
                string relativePath = $"{frameworksRelativeDir}/{name}";
                string fileGuid = proj.AddFile(relativePath, relativePath, PBXSourceTree.Source);
                proj.AddFileToBuild(frameworkTargetGuid, fileGuid);
                PBXProjectExtensions.AddFileToEmbedFrameworks(proj, mainTargetGuid, fileGuid);
            }

            // Search paths / Run paths (minimum necessary)
            proj.AddBuildProperty(frameworkTargetGuid, "FRAMEWORK_SEARCH_PATHS", "$(inherited)");
            proj.AddBuildProperty(frameworkTargetGuid, "FRAMEWORK_SEARCH_PATHS", "$(PROJECT_DIR)/Frameworks/**");
            proj.AddBuildProperty(frameworkTargetGuid, "LD_RUNPATH_SEARCH_PATHS", "$(inherited)");
            proj.AddBuildProperty(frameworkTargetGuid, "LD_RUNPATH_SEARCH_PATHS", "@executable_path/Frameworks");
            proj.AddBuildProperty(frameworkTargetGuid, "LD_RUNPATH_SEARCH_PATHS", "@loader_path/Frameworks");
            proj.AddBuildProperty(mainTargetGuid, "LD_RUNPATH_SEARCH_PATHS", "@loader_path/Frameworks");

            proj.WriteToFile(pbxprojPath);

            foreach (string name in xcframeworkNames)
                Debug.Log($"[NativeToolkit][iOS] Added {name} to Xcode project.");
            Debug.Log("[NativeToolkit][iOS] XCFrameworks added and embedded successfully.");
            EditorUtility.DisplayDialog("NativeToolkit (iOS)", "XCFrameworks were added and embedded successfully.", "OK");
        }
        catch (System.Exception ex)
        {
            Debug.LogError("[NativeToolkit][iOS] Failed to apply XCFramework & PBX edits:\n" + ex);
            EditorUtility.DisplayDialog("NativeToolkit (iOS) - Error", "Failed to add/embed XCFramework:\n" + ex.Message, "OK");
        }
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
