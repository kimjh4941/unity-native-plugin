using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using System.IO;

public static class PostBuildProcessor
{
    [PostProcessBuild]
    public static void OnPostProcessBuild(BuildTarget target, string pathToBuiltProject)
    {
        if (target == BuildTarget.StandaloneOSX)
        {
            UnityEngine.Debug.Log("macOSビルドの後処理を開始します。");

            // xcframeworkのコピー元とコピー先
            string xcframeworkSrc = Path.Combine(UnityEngine.Application.dataPath, "Plugins/macOS/Dialog/UnityMacPlugin.xcframework");
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
                UnityEngine.Debug.LogWarning("Xcodeプロジェクトファイルが見つかりません: " + pbxprojPath);
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