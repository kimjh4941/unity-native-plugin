#if UNITY_EDITOR_OSX
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
            bool isDevelopmentBuild = EditorUserBuildSettings.development;
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string unityXcframeworkSrc = Path.Combine(projectRoot, "Packages/com.jonghyunkim.nativetoolkit/Plugins/iOS/", isDevelopmentBuild ? "UnityIosNativeToolkit-Debug.xcframework" : "UnityIosNativeToolkit.xcframework");
            string iosNativeToolkitXcframeworkSrc = Path.Combine(projectRoot, "Packages/com.jonghyunkim.nativetoolkit/Plugins/iOS/", isDevelopmentBuild ? "IosNativeToolkit-Debug.xcframework" : "IosNativeToolkit.xcframework");

            string frameworksDir = Path.Combine(pathToBuiltProject, "Frameworks/com.jonghyunkim.nativetoolkit/Plugins/iOS");
            string unityXcframeworkDst = Path.Combine(frameworksDir, "UnityIosNativeToolkit.xcframework");
            string iosNativeToolkitXcframeworkDst = Path.Combine(frameworksDir, "IosNativeToolkit.xcframework");

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
            string unityRelativePath = "Frameworks/com.jonghyunkim.nativetoolkit/Plugins/iOS/UnityIosNativeToolkit.xcframework";
            string unityFileGuid = proj.AddFile(unityRelativePath, unityRelativePath, PBXSourceTree.Source);

            string iosNativeToolkitRelativePath = "Frameworks/com.jonghyunkim.nativetoolkit/Plugins/iOS/IosNativeToolkit.xcframework";
            string iosNativeToolkitFileGuid = proj.AddFile(iosNativeToolkitRelativePath, iosNativeToolkitRelativePath, PBXSourceTree.Source);

            // Link and embed the frameworks
            proj.AddFileToBuild(frameworkTargetGuid, unityFileGuid);
            PBXProjectExtensions.AddFileToEmbedFrameworks(proj, mainTargetGuid, unityFileGuid);

            proj.AddFileToBuild(frameworkTargetGuid, iosNativeToolkitFileGuid);
            PBXProjectExtensions.AddFileToEmbedFrameworks(proj, mainTargetGuid, iosNativeToolkitFileGuid);

            // Search paths / Run paths (minimum necessary)
            proj.AddBuildProperty(frameworkTargetGuid, "FRAMEWORK_SEARCH_PATHS", "$(inherited)");
            proj.AddBuildProperty(frameworkTargetGuid, "FRAMEWORK_SEARCH_PATHS", "$(PROJECT_DIR)/Frameworks/**");
            proj.AddBuildProperty(mainTargetGuid, "LD_RUNPATH_SEARCH_PATHS", "$(inherited)");
            proj.AddBuildProperty(mainTargetGuid, "LD_RUNPATH_SEARCH_PATHS", "@executable_path/Frameworks");

            proj.WriteToFile(pbxprojPath);

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
