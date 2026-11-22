#nullable enable

#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace JonghyunKim.NativeToolkit.IosPlayerTests.Editor
{
    /// <summary>
    /// Adds NativeSource~/NtkPlayerTestsProbe.mm to the exported project of an iOS player test run
    /// (artifact/topics/ios-c-abi-2, 3-S design v3, 1.7), the way the UI harness adds its overlay.
    /// </summary>
    /// <remarks>
    /// Only in a run of scripts/verify_unity_ios.sh (<see cref="IosPlayerBuildSetup.IsActive"/>), whose
    /// player scripts have NTK_IOS_PLAYER_PROBE; any other iOS build neither calls nor gets the probe. Not
    /// in Plugins/iOS, where it would go into every iOS build of this repository.
    /// </remarks>
    public sealed class IosPlayerProbeInstaller : IPostprocessBuildWithReport
    {
        private const string Source = "Assets/NativeToolkitIosPlayerTests/NativeSource~/NtkPlayerTestsProbe.mm";
        private const string Dir = "Libraries/NativeToolkitIosPlayerTests";
        private const string Probe = Dir + "/NtkPlayerTestsProbe.mm";

        // After the package's own step (IosXcodeProjectSetup, 100), which reads and writes the same project.
        public int callbackOrder => 200;

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.iOS || !IosPlayerBuildSetup.IsActive)
                return;

            string output = report.summary.outputPath;
            string pbxPath = PBXProject.GetPBXProjectPath(output);
            var project = new PBXProject();
            project.ReadFromFile(pbxPath);
            Directory.CreateDirectory(Path.Combine(output, Dir));
            File.Copy(Source, Path.Combine(output, Probe), true);
            if (project.FindFileGuidByProjectPath(Probe) == null)
                project.AddFileToBuild(project.GetUnityFrameworkTargetGuid(), project.AddFile(Probe, Probe, PBXSourceTree.Source));
            project.WriteToFile(pbxPath);
            Debug.Log($"{IosPlayerBuildSetup.LogTag} probe added: {Probe}");
        }
    }
}
#endif
