using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System.Diagnostics;
using System.IO;

public class BuildPreprocessor : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform == BuildTarget.iOS)
        {
            UnityEngine.Debug.Log("iOSビルドの前処理を実行します。");

            // 0. xcode cleanコマンドを実行
            string workspacePath = "/Users/jonghyunkim/Desktop/native-toolkit/ios/IosWorkspace.xcworkspace"; // ワークスペースのパスに変更
            string scheme = "UnityIosPlugin"; // スキーム名に合わせてください
            RunShellCommand(
                $"xcodebuild clean -workspace \"{workspacePath}\" -scheme \"{scheme}\""
            );

            // 1. UnityIosPluginライブラリのarchive作成
            string archivePath = "/Users/jonghyunkim/Desktop/native-toolkit-outputs/ios/UnityIosPlugin.xcarchive";
            RunShellCommand(
                $"xcodebuild archive -workspace \"{workspacePath}\" -scheme \"{scheme}\" -archivePath \"{archivePath}\" -sdk iphoneos SKIP_INSTALL=NO BUILD_LIBRARY_FOR_DISTRIBUTION=YES"
            );

            // 2. xcframework作成
            string xcframeworkPath = "/Users/jonghyunkim/Desktop/native-toolkit-outputs/ios/UnityIosPlugin.xcframework";
            // 2. xcframework作成前に既存のxcframeworkを削除
            if (Directory.Exists(xcframeworkPath))
            {
                Directory.Delete(xcframeworkPath, true);
            }
            RunShellCommand(
                $"xcodebuild -create-xcframework -framework \"{archivePath}\"/Products/Library/Frameworks/UnityIosPlugin.framework -output \"{xcframeworkPath}\""
            );

            // 3. xcframeworkをUnity6の/iOS/Dialogフォルダにコピー
            string destDir = Path.Combine(Application.dataPath, "Plugins/iOS/Dialog");
            Directory.CreateDirectory(destDir);
            RunShellCommand($"cp -R \"{xcframeworkPath}\" \"{destDir}\"");

            UnityEngine.Debug.Log("UnityIosPlugin.xcframeworkを「Plugins/iOS/Dialog」にコピーしました。");
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
