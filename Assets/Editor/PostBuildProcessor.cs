using UnityEditor;
using UnityEditor.Callbacks;
#if UNITY_EDITOR_OSX
using UnityEditor.iOS.Xcode;
#endif
using System.IO;

public static class PostBuildProcessor
{
    [PostProcessBuild]
    public static void OnPostProcessBuild(BuildTarget target, string pathToBuiltProject)
    {
#if UNITY_EDITOR_OSX
        if (target == BuildTarget.StandaloneOSX)
        {
            UnityEngine.Debug.Log("macOSビルドの後処理を開始します。");

            // xcframeworkのコピー元とコピー先
            string xcframeworkSrc = Path.Combine(UnityEngine.Application.dataPath, "Plugins/macOS/Library/UnityMacPlugin.xcframework");
            string frameworksDir = Path.Combine(pathToBuiltProject, "unity-native-plugin/Frameworks");
            string xcframeworkDst = Path.Combine(frameworksDir, "UnityMacPlugin.xcframework");

            // xcframeworkを.app内Frameworksにコピー
            if (Directory.Exists(xcframeworkDst))
                Directory.Delete(xcframeworkDst, true);
            DirectoryCopy(xcframeworkSrc, xcframeworkDst, true);

            // Xcodeプロジェクトファイルのパス
            string pbxprojPath = Path.Combine(pathToBuiltProject, "Mac.xcodeproj", "project.pbxproj");
            if (!File.Exists(pbxprojPath))
            {
                UnityEngine.Debug.LogError("Xcodeプロジェクトファイルが見つかりません: " + pbxprojPath);
                return;
            }

            // PBXProjectを編集してxcframeworkを追加
            var proj = new PBXProject();
            proj.ReadFromFile(pbxprojPath);

            string targetGuid = proj.GetUnityMainTargetGuid();

            // xcframeworkをFrameworksに追加
            string relativePath = "unity-native-plugin/Frameworks/UnityMacPlugin.xcframework";
            proj.AddFileToBuild(targetGuid, proj.AddFile(relativePath, relativePath, PBXSourceTree.Source));

            proj.WriteToFile(pbxprojPath);

            UnityEngine.Debug.Log("UnityMacPlugin.xcframeworkをXcodeプロジェクトに追加しました。");
            UnityEngine.Debug.Log("macOSビルドの後処理を終了しました。");
        }
#endif

#if UNITY_ANDROID
        if (target == BuildTarget.Android)
        {
            UnityEngine.Debug.Log("Androidビルドの後処理を開始します。");
            AddKotlinDependenciesToAndroidProject(pathToBuiltProject);
            UnityEngine.Debug.Log("Androidビルドの後処理を終了しました。");
        }
#endif

#if UNITY_STANDALONE_WIN
        if (target == BuildTarget.StandaloneWindows64)
        {
            UnityEngine.Debug.Log("Windowsビルドの後処理を開始します。");

            // コピー元とコピー先
            string pdbSrc = @"C:\Users\User\Desktop\native-toolkit\windows\WindowsLibraryExample\x64\Debug\WindowsLibraryExample\AppX\WindowsLibrary.pdb";
            string pdbDst = Path.Combine(@"D:\Build\Windows\unity-native-plugin_Data", @"Plugins\x86_64\WindowsLibrary.pdb");
            
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(pdbDst));
                File.Copy(pdbSrc, pdbDst, true);
                UnityEngine.Debug.Log("WindowsLibrary.pdb をコピーしました: " + pdbDst);
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError("WindowsLibrary.pdb のコピーに失敗: " + ex.Message);
            }

            UnityEngine.Debug.Log("Windowsビルドの後処理を終了しました。");
        }
#endif
    }

    /// <summary>
    /// AndroidプロジェクトにKotlin依存関係を追加
    /// </summary>
    private static void AddKotlinDependenciesToAndroidProject(string pathToBuiltProject)
    {
        UnityEngine.Debug.Log("Kotlin依存関係の追加を開始します。");

        // ビルドされたAndroidプロジェクトのGradleファイルパス
        string launcherBuildGradlePath = Path.Combine(pathToBuiltProject, "launcher", "build.gradle");
        string unityLibraryBuildGradlePath = Path.Combine(pathToBuiltProject, "unityLibrary", "build.gradle");
        string projectBuildGradlePath = Path.Combine(pathToBuiltProject, "build.gradle");

        try
        {
            // 1. プロジェクトレベルでKotlinプラグインを追加
            ModifyProjectBuildGradle(projectBuildGradlePath);
            // 2. unityLibraryモジュールにKotlin設定を追加
            ModifyUnityLibraryBuildGradle(unityLibraryBuildGradlePath);
            // 3. launcherモジュールにKotlin設定を追加
            ModifyLauncherBuildGradle(launcherBuildGradlePath);

            UnityEngine.Debug.Log("Kotlin依存関係の追加が完了しました。");
        }
        catch (System.Exception ex)
        {
            UnityEngine.Debug.LogError($"Kotlin依存関係の追加に失敗: {ex.Message}");
        }
    }

    /// <summary>
    /// プロジェクトレベルのbuild.gradleを修正
    /// </summary>
    private static void ModifyProjectBuildGradle(string filePath)
    {
        if (!File.Exists(filePath))
        {
            UnityEngine.Debug.LogError($"プロジェクトbuild.gradleが見つかりません: {filePath}");
            return;
        }

        string content = File.ReadAllText(filePath);

        // Kotlinプラグインを追加
        // 既に存在する場合は追加しない
        if (!content.Contains("org.jetbrains.kotlin.android"))
        {
            if (content.Contains("id 'com.android.library'"))
            {
                content = content.Replace(
                    "    id 'com.android.library' version '8.3.0' apply false",
                    @"    id 'com.android.library' version '8.3.0' apply false
    id 'org.jetbrains.kotlin.android' version '2.0.21' apply false"
                );
                UnityEngine.Debug.Log("プロジェクトbuild.gradleにKotlinプラグインを追加しました。");
            }
        }
        else
        {
            UnityEngine.Debug.Log("プロジェクトbuild.gradleには既にKotlinプラグインが含まれています。");
        }

        File.WriteAllText(filePath, content);
    }

    /// <summary>
    /// unityLibraryモジュールのbuild.gradleを修正
    /// </summary>
    private static void ModifyUnityLibraryBuildGradle(string filePath)
    {
        if (!File.Exists(filePath))
        {
            UnityEngine.Debug.LogError($"unityLibrary build.gradleが見つかりません: {filePath}");
            return;
        }

        string content = File.ReadAllText(filePath);

        // Kotlinプラグインを追加
        if (!content.Contains("apply plugin: 'org.jetbrains.kotlin.android'"))
        {
            content = content.Replace(
                "apply plugin: 'com.android.library'",
                "apply plugin: 'com.android.library'\napply plugin: 'org.jetbrains.kotlin.android'"
            );
            UnityEngine.Debug.Log("unityLibrary build.gradleにKotlinプラグインを追加しました。");
        }
        else
        {
            UnityEngine.Debug.Log("unityLibrary build.gradleには既にKotlinプラグインが含まれています。");
        }

        // 重複するKotlin依存関係を除外するconfigurationを追加
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
            UnityEngine.Debug.Log("unityLibrary build.gradleに重複除外の設定を追加しました。");
        }
        else
        {
            UnityEngine.Debug.Log("unityLibrary build.gradleには既に重複除外の設定が含まれています。");
        }

        // Kotlin依存関係を追加（既存のdependenciesブロックに追加）
        if (!content.Contains("kotlin-stdlib"))
        {
            content = content.Replace(
                "implementation 'androidx.constraintlayout:constraintlayout:2.1.4'",
                @"implementation 'androidx.constraintlayout:constraintlayout:2.1.4'
    implementation 'org.jetbrains.kotlin:kotlin-stdlib:2.0.21'
    implementation 'org.jetbrains.kotlin:kotlin-reflect:2.0.21'"
            );
            UnityEngine.Debug.Log("unityLibrary build.gradleにKotlin 2.0.21依存関係を追加しました。");
        }
        else
        {
            UnityEngine.Debug.Log("unityLibrary build.gradleには既にKotlin依存関係が含まれています。");
        }

        // kotlinOptionsを追加
        if (!content.Contains("tasks.withType(org.jetbrains.kotlin.gradle.tasks.KotlinCompile)"))
        {
            content += @"

tasks.withType(org.jetbrains.kotlin.gradle.tasks.KotlinCompile).configureEach {
    kotlinOptions {
        jvmTarget = '17'
        freeCompilerArgs += ['-Xjvm-default=all']
    }
}";
            UnityEngine.Debug.Log("unityLibrary build.gradleにkotlinOptionsを追加しました。");
        }
        else
        {
            UnityEngine.Debug.Log("unityLibrary build.gradleには既にkotlinOptionsが含まれています。");
        }

        File.WriteAllText(filePath, content);
    }

    /// <summary>
    /// launcherモジュールのbuild.gradleを修正
    /// </summary>
    private static void ModifyLauncherBuildGradle(string filePath)
    {
        if (!File.Exists(filePath))
        {
            UnityEngine.Debug.LogError($"launcher build.gradleが見つかりません: {filePath}");
            return;
        }

        string content = File.ReadAllText(filePath);

        // Kotlinプラグインを適用
        if (!content.Contains("apply plugin: 'org.jetbrains.kotlin.android'"))
        {
            content = content.Replace(
                "apply plugin: 'com.android.application'",
                "apply plugin: 'com.android.application'\napply plugin: 'org.jetbrains.kotlin.android'"
            );
            UnityEngine.Debug.Log("launcher build.gradleにKotlinプラグインを追加しました。");
        }
        else
        {
            UnityEngine.Debug.Log("launcher build.gradleには既にKotlinプラグインが含まれています。");
        }

        // 重複するKotlin依存関係を除外するconfigurationを追加
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
            UnityEngine.Debug.Log("launcher build.gradleに重複除外の設定を追加しました。");
        }
        else
        {
            UnityEngine.Debug.Log("launcher build.gradleには既に重複除外の設定が含まれています。");
        }

        // Kotlin依存関係を追加（既存のdependenciesブロックに追加）
        if (!content.Contains("kotlin-stdlib"))
        {
            content = content.Replace(
                "implementation 'androidx.constraintlayout:constraintlayout:2.1.4'",
                @"implementation 'androidx.constraintlayout:constraintlayout:2.1.4'
    implementation 'org.jetbrains.kotlin:kotlin-stdlib:2.0.21'
    implementation 'org.jetbrains.kotlin:kotlin-reflect:2.0.21'"
            );
            UnityEngine.Debug.Log("launcher build.gradleにKotlin 2.0.21依存関係を追加しました。");
        }
        else
        {
            UnityEngine.Debug.Log("launcher build.gradleには既にKotlin依存関係が含まれています。");
        }

        // ファイルの最後にkotlinOptionsを追加
        if (!content.Contains("tasks.withType(org.jetbrains.kotlin.gradle.tasks.KotlinCompile)"))
        {
            content += @"

tasks.withType(org.jetbrains.kotlin.gradle.tasks.KotlinCompile).configureEach {
    kotlinOptions {
        jvmTarget = '17'
        freeCompilerArgs += ['-Xjvm-default=all']
    }
}";
            UnityEngine.Debug.Log("launcher build.gradleにkotlinOptionsを追加しました。");
        }
        else
        {
            UnityEngine.Debug.Log("launcher build.gradleには既にkotlinOptionsが含まれています。");
        }

        File.WriteAllText(filePath, content);
    }

    // ディレクトリコピーのヘルパー
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