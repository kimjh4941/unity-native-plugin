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
        if (report.summary.platform == BuildTarget.Android)
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

            // 4. Plugins/Androidフォルダにコピー
            string destDir = Path.Combine(Application.dataPath, "Plugins/Android/Dialog");
            Directory.CreateDirectory(destDir);

            RunShellCommand($"cp -f \"{aarSrc1}\" \"{destDir}\"");
            RunShellCommand($"cp -f \"{aarSrc2}\" \"{destDir}\"");

            UnityEngine.Debug.Log("android_library-debug.aarとunity_android_plugin-debug.aarを「Plugins/Android/Dialog」にコピーしました。");
            UnityEngine.Debug.Log("Androidビルドの前処理を終了しました。");
        }
        else if (report.summary.platform == BuildTarget.iOS)
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
        else if (report.summary.platform == BuildTarget.StandaloneWindows64)
        {
            UnityEngine.Debug.Log("Windowsビルドの前処理を開始します。");

            // コピー元とコピー先
            string dllSrc = @"C:\Users\User\Desktop\native-toolkit\windows\WindowsLibraryExample\x64\Debug\WindowsLibraryExample\AppX\WindowsLibrary.dll";
            string destDir = Path.Combine(Application.dataPath, "Plugins/Windows/Dialog");
            string dllDst = Path.Combine(destDir, "WindowsLibrary.dll");

            try
            {
                Directory.CreateDirectory(destDir);
                File.Copy(dllSrc, dllDst, true);
                UnityEngine.Debug.Log("WindowsLibrary.dll を「Plugins/Windows/Dialog」にコピーしました。");
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogWarning("WindowsLibrary.dll のコピーに失敗: " + ex.Message);
            }

            UnityEngine.Debug.Log("Windowsビルドの前処理を終了しました。");
        }
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
