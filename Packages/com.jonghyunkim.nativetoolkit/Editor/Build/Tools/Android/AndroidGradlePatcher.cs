using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Utility to apply the same Android Gradle Kotlin dependency/config edits
/// as performed during post-build, but on-demand from an Editor command.
/// This class does not modify existing build processors.
/// </summary>
internal static class AndroidGradlePatcher
{
    /// <summary>
    /// Apply Kotlin plugin/dependency and Gradle tweaks to the specified
    /// exported Android project directory (Unity Gradle project root).
    /// </summary>
    /// <param name="androidProjectRoot">Path to exported Gradle project (folder containing build.gradle, launcher/, unityLibrary/)</param>
    public static void Apply(string androidProjectRoot)
    {
        if (string.IsNullOrEmpty(androidProjectRoot))
        {
            Debug.LogError("[NativeToolkit][Android] Android project path is empty.");
            return;
        }

        if (!Directory.Exists(androidProjectRoot))
        {
            Debug.LogError($"[NativeToolkit][Android] Directory not found: {androidProjectRoot}");
            return;
        }

        // Validate expected Gradle files
        string launcherBuildGradlePath = Path.Combine(androidProjectRoot, "launcher", "build.gradle");
        string unityLibraryBuildGradlePath = Path.Combine(androidProjectRoot, "unityLibrary", "build.gradle");
        string projectBuildGradlePath = Path.Combine(androidProjectRoot, "build.gradle");

        if (!File.Exists(projectBuildGradlePath) || !File.Exists(launcherBuildGradlePath) || !File.Exists(unityLibraryBuildGradlePath))
        {
            Debug.LogError("[NativeToolkit][Android] Not a valid Unity Gradle project. Expected build.gradle, launcher/build.gradle, unityLibrary/build.gradle.");
            return;
        }

        try
        {
            Debug.Log("[NativeToolkit][Android] Applying Kotlin dependency patches to Gradle project...");
            // Reuse the same logic as PostBuildProcessor by reproducing minimal invocations
            ModifyProjectBuildGradle(projectBuildGradlePath);
            ModifyUnityLibraryBuildGradle(unityLibraryBuildGradlePath);
            ModifyLauncherBuildGradle(launcherBuildGradlePath);
            Debug.Log("[NativeToolkit][Android] Kotlin dependency patches applied successfully.");
            EditorUtility.DisplayDialog(
                "NativeToolkit (Android)",
                "Kotlin dependency patches were applied successfully.",
                "OK"
            );
        }
        catch (Exception ex)
        {
            Debug.LogError($"[NativeToolkit][Android] Failed to apply patches: {ex.Message}\n{ex}");
            EditorUtility.DisplayDialog(
                "NativeToolkit (Android) - Error",
                $"Failed to apply Kotlin dependency patches:\n{ex.Message}",
                "OK"
            );
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
            Debug.LogError($"[NativeToolkit][Android] Project build.gradle not found: {filePath}");
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
                Debug.Log("[NativeToolkit][Android] Added Kotlin plugin to project build.gradle.");
            }
        }
        else
        {
            Debug.Log("[NativeToolkit][Android] Project build.gradle already contains Kotlin plugin.");
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
            Debug.LogError($"[NativeToolkit][Android] unityLibrary build.gradle not found: {filePath}");
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
            Debug.Log("[NativeToolkit][Android] Added Kotlin plugin to unityLibrary build.gradle.");
        }
        else
        {
            Debug.Log("[NativeToolkit][Android] unityLibrary build.gradle already contains Kotlin plugin.");
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
            Debug.Log("[NativeToolkit][Android] Added duplicate exclusion configuration to unityLibrary build.gradle.");
        }
        else
        {
            Debug.Log("[NativeToolkit][Android] unityLibrary build.gradle already contains duplicate exclusion configuration.");
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
            Debug.Log("[NativeToolkit][Android] Added Kotlin 2.0.21 dependencies to unityLibrary build.gradle.");
        }
        else
        {
            Debug.Log("[NativeToolkit][Android] unityLibrary build.gradle already contains Kotlin dependencies.");
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
            Debug.Log("[NativeToolkit][Android] Added kotlinOptions to unityLibrary build.gradle.");
        }
        else
        {
            Debug.Log("[NativeToolkit][Android] unityLibrary build.gradle already contains kotlinOptions.");
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
            Debug.LogError($"[NativeToolkit][Android] launcher build.gradle not found: {filePath}");
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
            Debug.Log("[NativeToolkit][Android] Added Kotlin plugin to launcher build.gradle.");
        }
        else
        {
            Debug.Log("[NativeToolkit][Android] launcher build.gradle already contains Kotlin plugin.");
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
            Debug.Log("[NativeToolkit][Android] Added duplicate exclusion configuration to launcher build.gradle.");
        }
        else
        {
            Debug.Log("[NativeToolkit][Android] launcher build.gradle already contains duplicate exclusion configuration.");
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
            Debug.Log("[NativeToolkit][Android] Added Kotlin 2.0.21 dependencies to launcher build.gradle.");
        }
        else
        {
            Debug.Log("[NativeToolkit][Android] launcher build.gradle already contains Kotlin dependencies.");
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
            Debug.Log("[NativeToolkit][Android] Added kotlinOptions to launcher build.gradle.");
        }
        else
        {
            Debug.Log("[NativeToolkit][Android] launcher build.gradle already contains kotlinOptions.");
        }

        File.WriteAllText(filePath, content);
        Debug.Log("[NativeToolkit][Android] Modified launcher build.gradle to include Kotlin dependencies.");
    }
}
