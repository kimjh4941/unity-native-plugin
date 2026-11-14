#nullable enable

#if UNITY_IOS
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.iOS.Xcode;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace JonghyunKim.NativeToolkit.IosUiHarness.Editor
{
    /// <summary>
    /// Exports the UI harness (artifact/topics/ios-c-abi-2, UI automation design v4, 4.1), called by
    /// scripts/test_ios_ui.sh: <c>-executeMethod JonghyunKim.NativeToolkit.IosUiHarness.Editor.IosUiHarnessBuild.Export
    /// -ntkHarnessSdk simulator|device -ntkHarnessOut &lt;dir&gt;</c>.
    /// </summary>
    /// <remarks>
    /// The harness assembly is compiled into this build only (NTK_UI_HARNESS as an extra define). The
    /// player gets its own bundle id and display name, so it never shares notification permissions with
    /// the sample app or is mistaken for it in the share sheet; for the simulator, the Simulator SDK and
    /// arm64. Those Player Settings are restored before the method returns (they take effect even when
    /// the active build profile overrides Player Settings; stage 2 spike, V-2 and V-3). The overlay
    /// (NativeSource~/NtkUiHarnessOverlay.mm) is not in Plugins/iOS, where it would go into every iOS
    /// build of this repository; it is added to the exported project here. For a device, signing is
    /// automatic without -allowProvisioningUpdates (the team's existing wildcard profile; stage 1, S-9).
    /// </remarks>
    public static class IosUiHarnessBuild
    {
        public const string BundleId = "com.jonghyunkim.nativetoolkit.uiharness";
        private const string Root = "Assets/NativeToolkitIosUiHarness";
        private const string Scene = Root + "/IosUiHarness.unity";
        private const string LogTag = "[NTK-UI-HARNESS-BUILD]";

        public static void Export()
        {
            int code = 1;
            try
            {
                code = Build() ? 0 : 1;
            }
            catch (Exception e)
            {
                Debug.LogError($"{LogTag} {e}");
            }
            EditorApplication.Exit(code);
        }

        private static bool Build()
        {
            string sdk = Argument("-ntkHarnessSdk") ?? throw new ArgumentException("-ntkHarnessSdk simulator|device is required");
            if (sdk != "simulator" && sdk != "device")
                throw new ArgumentException($"-ntkHarnessSdk must be simulator or device, not '{sdk}'");
            string output = Argument("-ntkHarnessOut") ?? throw new ArgumentException("-ntkHarnessOut is required");
            if (!File.Exists(Scene))
                EditorSceneManager.SaveScene(EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects), Scene);

            iOSSdkVersion oldSdk = PlayerSettings.iOS.sdkVersion;
            AppleMobileArchitectureSimulator oldArch = PlayerSettings.iOS.simulatorSdkArchitecture;
            string oldId = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.iOS);
            BuildReport report;
            try
            {
                PlayerSettings.iOS.sdkVersion = sdk == "simulator" ? iOSSdkVersion.SimulatorSDK : iOSSdkVersion.DeviceSDK;
                if (sdk == "simulator")
                    PlayerSettings.iOS.simulatorSdkArchitecture = AppleMobileArchitectureSimulator.ARM64;
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, BundleId);
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { Scene },
                    locationPathName = output,
                    target = BuildTarget.iOS,
                    // Not a development build: a development player looks for the Unity Editor on the local
                    // network, and iOS then asks the user for local network access after every install
                    // (the first device run, 2026-11-14). The harness needs neither the profiler nor the
                    // Editor connection; Debug.Log still reaches the device log.
                    options = BuildOptions.None,
                    extraScriptingDefines = new[] { "NTK_UI_HARNESS" },
                });
            }
            finally
            {
                PlayerSettings.iOS.sdkVersion = oldSdk;
                PlayerSettings.iOS.simulatorSdkArchitecture = oldArch;
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, oldId);
            }
            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"{LogTag} result={report.summary.result}");
                return false;
            }

            string pbxPath = PBXProject.GetPBXProjectPath(output);
            var project = new PBXProject();
            project.ReadFromFile(pbxPath);
            string framework = project.GetUnityFrameworkTargetGuid();
            const string dir = "Libraries/NativeToolkitIosUiHarness";
            const string overlay = dir + "/NtkUiHarnessOverlay.mm";
            Directory.CreateDirectory(Path.Combine(output, dir));
            File.Copy($"{Root}/NativeSource~/NtkUiHarnessOverlay.mm", Path.Combine(output, overlay), true);
            if (project.FindFileGuidByProjectPath(overlay) == null)
                project.AddFileToBuild(framework, project.AddFile(overlay, overlay, PBXSourceTree.Source));
            if (sdk == "device")
            {
                foreach (string target in new[] { project.GetUnityMainTargetGuid(), framework })
                {
                    project.SetBuildProperty(target, "CODE_SIGN_STYLE", "Automatic");
                    project.SetBuildProperty(target, "PROVISIONING_PROFILE_SPECIFIER", "");
                }
            }
            project.WriteToFile(pbxPath);

            var plist = new PlistDocument();
            string plistPath = Path.Combine(output, "Info.plist");
            plist.ReadFromFile(plistPath);
            plist.root.SetString("CFBundleDisplayName", "NTK UI Harness");
            plist.WriteToFile(plistPath);

            Debug.Log($"{LogTag} result=Succeeded sdk={sdk}");
            return true;
        }

        private static string? Argument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }
}
#endif
