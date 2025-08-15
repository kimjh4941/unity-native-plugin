using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System.Diagnostics;
using System.IO;

public class PreBuildProcessor : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        // ビルド前に不要なライブラリを一時的に削除
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
            // Unity 6 でも残っていれば有効。無い場合はコンパイル条件で外してください。
            PlayerSettings.usePlayerLog = false;
            UnityEngine.Debug.Log("[Build] Set PlayerSettings.usePlayerLog = false for macOS");
#endif
            BuildmacOSLibraries();
        }
    }

    /// <summary>
    /// ビルド対象以外のプラットフォームのライブラリを一時的に無効化
    /// </summary>
    private void CleanupOtherPlatformLibraries(BuildTarget targetPlatform)
    {
        UnityEngine.Debug.Log($"不要なライブラリのクリーンアップを開始: ターゲット = {targetPlatform}");

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
                // 一時的に無効化（.disabledフォルダにリネーム）
                string disabledDir = dir + ".disabled";

                // 既存の.disabledフォルダがある場合は削除
                if (Directory.Exists(disabledDir))
                {
                    Directory.Delete(disabledDir, true);
                    UnityEngine.Debug.Log($"既存の無効化フォルダを削除: {disabledDir}");
                }

                // フォルダをリネームして無効化
                Directory.Move(dir, disabledDir);
                UnityEngine.Debug.Log($"ライブラリを一時無効化: {dir} → {disabledDir}");

                // Unityにメタファイルの変更を認識させる
                AssetDatabase.Refresh();
            }
            else if (shouldKeep)
            {
                UnityEngine.Debug.Log($"ライブラリを保持: {dir} (ターゲットプラットフォーム用)");

                // 無効化されているライブラリを復元
                string disabledDir = dir + ".disabled";
                if (!Directory.Exists(dir) && Directory.Exists(disabledDir))
                {
                    Directory.Move(disabledDir, dir);
                    UnityEngine.Debug.Log($"ライブラリを復元: {disabledDir} → {dir}");
                    AssetDatabase.Refresh();
                }
            }
        }

        UnityEngine.Debug.Log("ライブラリのクリーンアップ完了");
    }

    /// <summary>
    /// 指定されたライブラリがターゲットプラットフォームで必要かどうかを判定
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
                return false; // 未対応プラットフォームの場合はすべて無効化
        }
    }

    private void BuildAndroidLibraries()
    {
        UnityEngine.Debug.Log("Androidビルドの前処理を開始します。");

        // 1. gradlewがandroid/AndroidLibraryExample直下にある
        string androidRootProjectPath = "/Users/jonghyunkim/Desktop/native-toolkit/android/AndroidLibraryExample";

        // 2. android_library-debug.aarのビルド
        string androidLibraryProjectPath = "/Users/jonghyunkim/Desktop/native-toolkit/android/android_library";
        RunShellCommand(
            $"cd \"{androidRootProjectPath}\" && ./gradlew :android_library:assembleDebug"
        );
        string aarSrc1 = Path.Combine(androidLibraryProjectPath, "build", "outputs", "aar", "android_library-debug.aar");

        // 3. unity_android_plugin-debug.aarのビルド
        string unityAndroidPluginProjectPath = "/Users/jonghyunkim/Desktop/native-toolkit/android/unity_android_plugin";
        RunShellCommand(
            $"cd \"{androidRootProjectPath}\" && ./gradlew :unity_android_plugin:assembleDebug"
        );
        string aarSrc2 = Path.Combine(unityAndroidPluginProjectPath, "build", "outputs", "aar", "unity_android_plugin-debug.aar");

        // 4. Plugins/Android/Libraryフォルダにコピー
        string destDir = Path.Combine(Application.dataPath, "Plugins/Android/Library");
        Directory.CreateDirectory(destDir);

        RunShellCommand($"cp -f \"{aarSrc1}\" \"{destDir}\"");
        RunShellCommand($"cp -f \"{aarSrc2}\" \"{destDir}\"");

        UnityEngine.Debug.Log("android_library-debug.aarとunity_android_plugin-debug.aarを「Plugins/Android/Library」にコピーしました。");
        UnityEngine.Debug.Log("Androidビルドの前処理を終了しました。");
    }

    private void BuildiOSLibraries()
    {
        UnityEngine.Debug.Log("iOSビルドの前処理を開始します。");

        // 1. xcode cleanコマンドを実行
        string workspacePath = "/Users/jonghyunkim/Desktop/native-toolkit/ios/IosWorkspace.xcworkspace";
        string scheme = "UnityIosPlugin";
        RunShellCommand(
            $"xcodebuild clean -workspace \"{workspacePath}\" -scheme \"{scheme}\""
        );

        // 2. UnityIosPluginライブラリのarchive作成
        string archivePath = "/Users/jonghyunkim/Desktop/native-toolkit-outputs/ios/UnityIosPlugin.xcarchive";
        RunShellCommand(
            $"xcodebuild archive -workspace \"{workspacePath}\" -scheme \"{scheme}\" -archivePath \"{archivePath}\" -sdk iphoneos SKIP_INSTALL=NO BUILD_LIBRARY_FOR_DISTRIBUTION=YES"
        );

        // 3. xcframework作成
        string xcframeworkPath = "/Users/jonghyunkim/Desktop/native-toolkit-outputs/ios/UnityIosPlugin.xcframework";

        // 4. xcframework作成前に既存のxcframeworkを削除
        if (Directory.Exists(xcframeworkPath))
        {
            Directory.Delete(xcframeworkPath, true);
        }
        RunShellCommand(
            $"xcodebuild -create-xcframework -framework \"{archivePath}\"/Products/Library/Frameworks/UnityIosPlugin.framework -output \"{xcframeworkPath}\""
        );

        // 5. xcframeworkをUnity6のPlugins/iOS/Libraryフォルダにコピー
        string destDir = Path.Combine(Application.dataPath, "Plugins/iOS/Library");
        Directory.CreateDirectory(destDir);
        RunShellCommand($"cp -R \"{xcframeworkPath}\" \"{destDir}\"");

        UnityEngine.Debug.Log("UnityIosPlugin.xcframeworkを「Plugins/iOS/Library」にコピーしました。");
        UnityEngine.Debug.Log("iOSビルドの前処理を終了しました。");
    }

    private void BuildWindowsLibraries()
    {
        UnityEngine.Debug.Log("Windowsビルドの前処理を開始します。");

        // コピー元とコピー先
        string dllSrc = @"C:\Users\User\Desktop\native-toolkit\windows\WindowsLibraryExample\x64\Debug\WindowsLibraryExample\AppX\WindowsLibrary.dll";
        string destDir = Path.Combine(Application.dataPath, "Plugins/Windows/Library");
        string dllDst = Path.Combine(destDir, "WindowsLibrary.dll");

        try
        {
            Directory.CreateDirectory(destDir);
            File.Copy(dllSrc, dllDst, true);
            UnityEngine.Debug.Log("WindowsLibrary.dll を「Plugins/Windows/Library」にコピーしました。");
        }
        catch (System.Exception ex)
        {
            UnityEngine.Debug.LogWarning("WindowsLibrary.dll のコピーに失敗: " + ex.Message);
        }

        UnityEngine.Debug.Log("Windowsビルドの前処理を終了しました。");
    }

    private void BuildmacOSLibraries()
    {
        UnityEngine.Debug.Log("macOSビルドの前処理を開始します。");

        // 1. xcode cleanコマンドを実行
        string workspacePath = "/Users/jonghyunkim/Desktop/native-toolkit/mac/MacWorkspace.xcworkspace";
        string scheme = "UnityMacPlugin";
        RunShellCommand(
            $"xcodebuild clean -workspace \"{workspacePath}\" -scheme \"{scheme}\""
        );

        // 2. UnityMacPluginライブラリのarchive作成
        string archivePath = "/Users/jonghyunkim/Desktop/native-toolkit-outputs/mac/UnityMacPlugin.xcarchive";
        RunShellCommand(
            $"xcodebuild archive -workspace \"{workspacePath}\" -scheme \"{scheme}\" -archivePath \"{archivePath}\" -sdk macosx SKIP_INSTALL=NO BUILD_LIBRARY_FOR_DISTRIBUTION=YES"
        );

        // 3. xcframework作成
        string xcframeworkPath = "/Users/jonghyunkim/Desktop/native-toolkit-outputs/mac/UnityMacPlugin.xcframework";

        // 4. xcframework作成前に既存のxcframeworkを削除
        if (Directory.Exists(xcframeworkPath))
        {
            Directory.Delete(xcframeworkPath, true);
        }
        RunShellCommand(
            $"xcodebuild -create-xcframework -framework \"{archivePath}\"/Products/Library/Frameworks/UnityMacPlugin.framework -output \"{xcframeworkPath}\""
        );

        // 5. xcframeworkをUnity6のPlugins/macOS/Libraryフォルダにコピー
        string destDir = Path.Combine(Application.dataPath, "Plugins/macOS/Library");
        Directory.CreateDirectory(destDir);
        RunShellCommand($"cp -R \"{xcframeworkPath}\" \"{destDir}\"");

        UnityEngine.Debug.Log("UnityMacPlugin.xcframeworkを「Plugins/macOS/Library」にコピーしました。");
        UnityEngine.Debug.Log("macOSビルドの前処理を終了しました。");
    }

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
            throw new BuildFailedException($"コマンド失敗: {command}\n{error}");
        }
        else
        {
            UnityEngine.Debug.Log(output);
        }
    }
}
