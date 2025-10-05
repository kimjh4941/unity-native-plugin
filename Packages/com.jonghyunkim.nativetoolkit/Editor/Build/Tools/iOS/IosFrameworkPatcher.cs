#if UNITY_IOS
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
    /// <summary>
    /// Apply XCFramework copy and link/embed edits to the specified Xcode project root.
    /// </summary>
    /// <param name="pathToBuiltProject">Xcode project root path (contains Unity-iPhone.xcodeproj)</param>
    public static void Apply(string pathToBuiltProject)
    {
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
            string xcframeworkSrc = Path.Combine(projectRoot, "Packages/com.jonghyunkim.nativetoolkit/Plugins/iOS/UnityIosNativeToolkit.xcframework");

            string frameworksDir = Path.Combine(pathToBuiltProject, "Frameworks/com.jonghyunkim.nativetoolkit/Plugins/iOS");
            string xcframeworkDst = Path.Combine(frameworksDir, "UnityIosNativeToolkit.xcframework");

            if (!Directory.Exists(xcframeworkSrc))
                throw new DirectoryNotFoundException("[NativeToolkit][iOS] Source xcframework not found: " + xcframeworkSrc);

            // Copy XCFramework into project
            if (Directory.Exists(xcframeworkDst))
                Directory.Delete(xcframeworkDst, true);
            Directory.CreateDirectory(frameworksDir);
            DirectoryCopy(xcframeworkSrc, xcframeworkDst, true);

            // Edit Xcode project to link and embed the XCFramework
            string pbxprojPath = Path.Combine(pathToBuiltProject, "Unity-iPhone.xcodeproj", "project.pbxproj");
            if (!File.Exists(pbxprojPath))
                throw new FileNotFoundException("[NativeToolkit][iOS] Xcode project file not found: " + pbxprojPath);

            var proj = new PBXProject();
            proj.ReadFromFile(pbxprojPath);

            string mainTargetGuid = proj.GetUnityMainTargetGuid();
            string frameworkTargetGuid = proj.GetUnityFrameworkTargetGuid();

            // Add XCFramework to Frameworks
            string relativePath = "Frameworks/com.jonghyunkim.nativetoolkit/Plugins/iOS/UnityIosNativeToolkit.xcframework";
            string fileGuid = proj.AddFile(relativePath, relativePath, PBXSourceTree.Source);

            // Link and embed the framework
            proj.AddFileToBuild(frameworkTargetGuid, fileGuid);
            PBXProjectExtensions.AddFileToEmbedFrameworks(proj, mainTargetGuid, fileGuid);

            // Search paths / Run paths (minimum necessary)
            proj.AddBuildProperty(frameworkTargetGuid, "FRAMEWORK_SEARCH_PATHS", "$(inherited)");
            proj.AddBuildProperty(frameworkTargetGuid, "FRAMEWORK_SEARCH_PATHS", "$(PROJECT_DIR)/Frameworks/**");
            proj.AddBuildProperty(mainTargetGuid, "LD_RUNPATH_SEARCH_PATHS", "$(inherited)");
            proj.AddBuildProperty(mainTargetGuid, "LD_RUNPATH_SEARCH_PATHS", "@executable_path/Frameworks");

            proj.WriteToFile(pbxprojPath);

            Debug.Log("[NativeToolkit][iOS] XCFramework added and embedded successfully.");
            EditorUtility.DisplayDialog("NativeToolkit (iOS)", "XCFramework was added and embedded successfully.", "OK");
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
