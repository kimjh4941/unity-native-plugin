using UnityEditor;
using UnityEditor.Callbacks;
#if UNITY_IOS || UNITY_EDITOR_OSX
using UnityEditor.iOS.Xcode;
using UnityEditor.iOS.Xcode.Extensions;
#endif
using System.IO;

/// <summary>
/// Post-build processor that handles platform-specific setup after Unity builds complete.
/// For macOS builds, copies XCFramework and modifies the Xcode project.
/// For Android builds, adds Kotlin dependencies to generated Gradle files.
/// For Windows builds, copies PDB files for debugging support.
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
#if UNITY_ANDROID
        if (target == BuildTarget.Android)
        {
            UnityEngine.Debug.Log("[Build][Android] Post-build steps started.");
            AddKotlinDependenciesToAndroidProject(pathToBuiltProject);
            UnityEngine.Debug.Log("[Build][Android] Post-build steps completed.");
        }
#endif

#if UNITY_IOS
        if (target == BuildTarget.iOS)
        {
            UnityEngine.Debug.Log("[Build][iOS] Post-build steps started.");

            // XCFramework source and destination paths
            string projectRoot = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."));
            string xcframeworkSrc = Path.Combine(projectRoot, "Packages/com.jonghyunkim.nativetoolkit/Plugins/iOS/UnityIosNativeToolkit.xcframework");

            string frameworksDir = Path.Combine(pathToBuiltProject, "Frameworks/com.jonghyunkim.nativetoolkit/Plugins/iOS");
            string xcframeworkDst = Path.Combine(frameworksDir, "UnityIosNativeToolkit.xcframework");

            if (!Directory.Exists(xcframeworkSrc))
            {
                UnityEngine.Debug.LogError("[Build][iOS] Source xcframework not found: " + xcframeworkSrc);
                return;
            }

            // Copy XCFramework to Xcode Frameworks folder
            if (Directory.Exists(xcframeworkDst))
                Directory.Delete(xcframeworkDst, true);
            Directory.CreateDirectory(frameworksDir);
            DirectoryCopy(xcframeworkSrc, xcframeworkDst, true);

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

            UnityEngine.Debug.Log("[Build][iOS] Added UnityIosNativeToolkit.xcframework to Xcode project.");
            UnityEngine.Debug.Log("[Build][iOS] Post-build steps completed.");
        }
#endif

#if UNITY_STANDALONE_WIN
        if (target == BuildTarget.StandaloneWindows64)
        {
            UnityEngine.Debug.Log("[Build][Windows] Post-build steps started.");

            bool isDevelopmentBuild = EditorUserBuildSettings.development;
            if (!isDevelopmentBuild)
            {
                UnityEngine.Debug.Log("[Build][Windows] Skipping PDB copy for non-development build.");
                UnityEngine.Debug.Log("[Build][Windows] Post-build steps completed.");
                return;
            }
            // Source and destination paths
            string pdbSrc = @"C:\Users\User\Desktop\native-toolkit\windows\WindowsLibraryExample\x64\Debug\WindowsLibraryExample\AppX\WindowsLibrary-Debug.pdb";
            string pdbDst = Path.Combine(@"D:\Build\Windows\unity-native-plugin_Data", @"Plugins\x86_64\WindowsLibrary-Debug.pdb");
            
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(pdbDst));
                File.Copy(pdbSrc, pdbDst, true);
                UnityEngine.Debug.Log("[Build][Windows] Copied WindowsLibrary-Debug.pdb to: " + pdbDst);
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError("[Build][Windows] Failed to copy WindowsLibrary-Debug.pdb: " + ex.Message);
            }

            UnityEngine.Debug.Log("[Build][Windows] Post-build steps completed.");
        }
#endif

#if UNITY_EDITOR_OSX
        if (target == BuildTarget.StandaloneOSX)
        {
            UnityEngine.Debug.Log("[Build][macOS] Post-build steps started.");

            // XCFramework source and destination paths
            string projectRoot = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."));
            string xcframeworkSrc = Path.Combine(projectRoot, "Packages/com.jonghyunkim.nativetoolkit/Plugins/macOS/UnityMacNativeToolkit.xcframework");
            string frameworksDir = Path.Combine(pathToBuiltProject, "unity-native-plugin/Frameworks");
            string xcframeworkDst = Path.Combine(frameworksDir, "UnityMacNativeToolkit.xcframework");

            // Copy XCFramework to .app Frameworks folder
            if (Directory.Exists(xcframeworkDst))
                Directory.Delete(xcframeworkDst, true);
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
            string relativePath = "unity-native-plugin/Frameworks/UnityMacNativeToolkit.xcframework";
            proj.AddFileToBuild(targetGuid, proj.AddFile(relativePath, relativePath, PBXSourceTree.Source));

            proj.WriteToFile(pbxprojPath);

            UnityEngine.Debug.Log("[Build][macOS] Added UnityMacNativeToolkit.xcframework to Xcode project.");
            UnityEngine.Debug.Log("[Build][macOS] Post-build steps completed.");
        }
#endif
    }

    /// <summary>
    /// Adds Kotlin dependencies and configuration to the generated Android Gradle project.
    /// </summary>
    /// <param name="pathToBuiltProject">Path to the built Android project.</param>
    private static void AddKotlinDependenciesToAndroidProject(string pathToBuiltProject)
    {
        UnityEngine.Debug.Log("[Build][Android] Adding Kotlin dependencies started.");

        // Generated Android project Gradle file paths
        string launcherBuildGradlePath = Path.Combine(pathToBuiltProject, "launcher", "build.gradle");
        string unityLibraryBuildGradlePath = Path.Combine(pathToBuiltProject, "unityLibrary", "build.gradle");
        string projectBuildGradlePath = Path.Combine(pathToBuiltProject, "build.gradle");

        try
        {
            // 1. Add Kotlin plugin at project level
            ModifyProjectBuildGradle(projectBuildGradlePath);
            // 2. Add Kotlin configuration to unityLibrary module
            ModifyUnityLibraryBuildGradle(unityLibraryBuildGradlePath);
            // 3. Add Kotlin configuration to launcher module
            ModifyLauncherBuildGradle(launcherBuildGradlePath);

            UnityEngine.Debug.Log("[Build][Android] Kotlin dependencies addition completed.");
        }
        catch (System.Exception ex)
        {
            UnityEngine.Debug.LogError($"[Build][Android] Failed to add Kotlin dependencies: {ex.Message}");
        }
    }

    /// <summary>
    /// Modifies the project-level build.gradle file to add Kotlin plugin.
    /// </summary>
    /// <param name="filePath">Path to the project build.gradle file.</param>
    private static void ModifyProjectBuildGradle(string filePath)
    {
        if (!File.Exists(filePath))
        {
            UnityEngine.Debug.LogError($"[Build][Android] Project build.gradle not found: {filePath}");
            return;
        }

        string content = File.ReadAllText(filePath);

        // Add Kotlin plugin - don't add if already exists
        if (!content.Contains("org.jetbrains.kotlin.android"))
        {
            if (content.Contains("id 'com.android.library'"))
            {
                content = content.Replace(
                    "    id 'com.android.library' version '8.3.0' apply false",
                    @"    id 'com.android.library' version '8.3.0' apply false
    id 'org.jetbrains.kotlin.android' version '2.0.21' apply false"
                );
                UnityEngine.Debug.Log("[Build][Android] Added Kotlin plugin to project build.gradle.");
            }
        }
        else
        {
            UnityEngine.Debug.Log("[Build][Android] Project build.gradle already contains Kotlin plugin.");
        }

        File.WriteAllText(filePath, content);
    }

    /// <summary>
    /// Modifies the unityLibrary module's build.gradle file to add Kotlin configuration.
    /// </summary>
    /// <param name="filePath">Path to the unityLibrary build.gradle file.</param>
    private static void ModifyUnityLibraryBuildGradle(string filePath)
    {
        if (!File.Exists(filePath))
        {
            UnityEngine.Debug.LogError($"[Build][Android] unityLibrary build.gradle not found: {filePath}");
            return;
        }

        string content = File.ReadAllText(filePath);

        // Add Kotlin plugin
        if (!content.Contains("apply plugin: 'org.jetbrains.kotlin.android'"))
        {
            content = content.Replace(
                "apply plugin: 'com.android.library'",
                "apply plugin: 'com.android.library'\napply plugin: 'org.jetbrains.kotlin.android'"
            );
            UnityEngine.Debug.Log("[Build][Android] Added Kotlin plugin to unityLibrary build.gradle.");
        }
        else
        {
            UnityEngine.Debug.Log("[Build][Android] unityLibrary build.gradle already contains Kotlin plugin.");
        }

        // Add configuration to exclude duplicate Kotlin dependencies
        if (!content.Contains("configurations.all"))
        {
            content = content.Replace(
                "dependencies {",
                @"configurations.all {
    exclude group: 'org.jetbrains.kotlin', module: 'kotlin-stdlib-jdk7'
    exclude group: 'org.jetbrains.kotlin', module: 'kotlin-stdlib-jdk8'
}

dependencies {"
            );
            UnityEngine.Debug.Log("[Build][Android] Added duplicate exclusion configuration to unityLibrary build.gradle.");
        }
        else
        {
            UnityEngine.Debug.Log("[Build][Android] unityLibrary build.gradle already contains duplicate exclusion configuration.");
        }

        // Add Kotlin dependencies (to existing dependencies block)
        if (!content.Contains("kotlin-stdlib"))
        {
            content = content.Replace(
                "implementation 'androidx.constraintlayout:constraintlayout:2.1.4'",
                @"implementation 'androidx.constraintlayout:constraintlayout:2.1.4'
    implementation 'org.jetbrains.kotlin:kotlin-stdlib:2.0.21'
    implementation 'org.jetbrains.kotlin:kotlin-reflect:2.0.21'"
            );
            UnityEngine.Debug.Log("[Build][Android] Added Kotlin 2.0.21 dependencies to unityLibrary build.gradle.");
        }
        else
        {
            UnityEngine.Debug.Log("[Build][Android] unityLibrary build.gradle already contains Kotlin dependencies.");
        }

        // Add kotlinOptions
        if (!content.Contains("tasks.withType(org.jetbrains.kotlin.gradle.tasks.KotlinCompile)"))
        {
            content += @"

tasks.withType(org.jetbrains.kotlin.gradle.tasks.KotlinCompile).configureEach {
    kotlinOptions {
        jvmTarget = '17'
        freeCompilerArgs += ['-Xjvm-default=all']
    }
}";
            UnityEngine.Debug.Log("[Build][Android] Added kotlinOptions to unityLibrary build.gradle.");
        }
        else
        {
            UnityEngine.Debug.Log("[Build][Android] unityLibrary build.gradle already contains kotlinOptions.");
        }

        File.WriteAllText(filePath, content);
    }

    /// <summary>
    /// Modifies the launcher module's build.gradle file to add Kotlin configuration.
    /// </summary>
    /// <param name="filePath">Path to the launcher build.gradle file.</param>
    private static void ModifyLauncherBuildGradle(string filePath)
    {
        if (!File.Exists(filePath))
        {
            UnityEngine.Debug.LogError($"[Build][Android] launcher build.gradle not found: {filePath}");
            return;
        }

        string content = File.ReadAllText(filePath);

        // Apply Kotlin plugin
        if (!content.Contains("apply plugin: 'org.jetbrains.kotlin.android'"))
        {
            content = content.Replace(
                "apply plugin: 'com.android.application'",
                "apply plugin: 'com.android.application'\napply plugin: 'org.jetbrains.kotlin.android'"
            );
            UnityEngine.Debug.Log("[Build][Android] Added Kotlin plugin to launcher build.gradle.");
        }
        else
        {
            UnityEngine.Debug.Log("[Build][Android] launcher build.gradle already contains Kotlin plugin.");
        }

        // Add configuration to exclude duplicate Kotlin dependencies
        if (!content.Contains("configurations.all"))
        {
            content = content.Replace(
                "dependencies {",
                @"configurations.all {
    exclude group: 'org.jetbrains.kotlin', module: 'kotlin-stdlib-jdk7'
    exclude group: 'org.jetbrains.kotlin', module: 'kotlin-stdlib-jdk8'
}

dependencies {"
            );
            UnityEngine.Debug.Log("[Build][Android] Added duplicate exclusion configuration to launcher build.gradle.");
        }
        else
        {
            UnityEngine.Debug.Log("[Build][Android] launcher build.gradle already contains duplicate exclusion configuration.");
        }

        // Add Kotlin dependencies (to existing dependencies block)
        if (!content.Contains("kotlin-stdlib"))
        {
            content = content.Replace(
                "implementation 'androidx.constraintlayout:constraintlayout:2.1.4'",
                @"implementation 'androidx.constraintlayout:constraintlayout:2.1.4'
    implementation 'org.jetbrains.kotlin:kotlin-stdlib:2.0.21'
    implementation 'org.jetbrains.kotlin:kotlin-reflect:2.0.21'"
            );
            UnityEngine.Debug.Log("[Build][Android] Added Kotlin 2.0.21 dependencies to launcher build.gradle.");
        }
        else
        {
            UnityEngine.Debug.Log("[Build][Android] launcher build.gradle already contains Kotlin dependencies.");
        }

        // Add kotlinOptions at the end of the file
        if (!content.Contains("tasks.withType(org.jetbrains.kotlin.gradle.tasks.KotlinCompile)"))
        {
            content += @"

tasks.withType(org.jetbrains.kotlin.gradle.tasks.KotlinCompile).configureEach {
    kotlinOptions {
        jvmTarget = '17'
        freeCompilerArgs += ['-Xjvm-default=all']
    }
}";
            UnityEngine.Debug.Log("[Build][Android] Added kotlinOptions to launcher build.gradle.");
        }
        else
        {
            UnityEngine.Debug.Log("[Build][Android] launcher build.gradle already contains kotlinOptions.");
        }

        File.WriteAllText(filePath, content);
        UnityEngine.Debug.Log("[Build][Android] Modified launcher build.gradle to include Kotlin dependencies.");
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
}