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
        if (report.summary.platform == BuildTarget.iOS)
        {
            UnityEngine.Debug.Log("iOSビルドの前処理を開始します。");

            // 1. xcode cleanコマンドを実行
            string workspacePath = "/Users/jonghyunkim/Desktop/native-toolkit/ios/IosWorkspace.xcworkspace"; // ワークスペースのパスに変更
            string scheme = "UnityIosPlugin"; // スキーム名に合わせてください
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

            // 5. xcframeworkをUnity6のPlugins/iOS/Dialogフォルダにコピー
            string destDir = Path.Combine(Application.dataPath, "Plugins/iOS/Dialog");
            Directory.CreateDirectory(destDir);
            RunShellCommand($"cp -R \"{xcframeworkPath}\" \"{destDir}\"");

            UnityEngine.Debug.Log("UnityIosPlugin.xcframeworkを「Plugins/iOS/Dialog」にコピーしました。");
            UnityEngine.Debug.Log("iOSビルドの前処理を終了しました。");
        }
        else if (report.summary.platform == BuildTarget.StandaloneOSX)
        {
            UnityEngine.Debug.Log("macOSビルドの前処理を開始します。");

            // 1. xcode cleanコマンドを実行
            string workspacePath = "/Users/jonghyunkim/Desktop/native-toolkit/mac/MacWorkspace.xcworkspace"; // ワークスペースのパスに変更
            string scheme = "UnityMacPlugin"; // スキーム名に合わせてください
            RunShellCommand(
                $"xcodebuild clean -workspace \"{workspacePath}\" -scheme \"{scheme}\""
            );

            // 2. UnityIosPluginライブラリのarchive作成
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

            // 5. xcframeworkをUnity6のPlugins/macOS/Dialogフォルダにコピー
            string destDir = Path.Combine(Application.dataPath, "Plugins/macOS/Dialog");
            Directory.CreateDirectory(destDir);
            RunShellCommand($"cp -R \"{xcframeworkPath}\" \"{destDir}\"");

            UnityEngine.Debug.Log("UnityMacPlugin.xcframeworkを「Plugins/macOS/Dialog」にコピーしました。");
            UnityEngine.Debug.Log("macOSビルドの前処理を終了しました。");
        }

        // else if (report.summary.platform == BuildTarget.Android)
        // {
        //     Debug.Log("Androidビルドの前処理を実行します。");

        //     // 例: Android固有の設定を確認
        //     if (PlayerSettings.Android.minSdkVersion < AndroidSdkVersions.AndroidApiLevel21)
        //     {
        //         throw new BuildFailedException("AndroidのminSdkVersionが21未満です。");
        //     }
        // }
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
