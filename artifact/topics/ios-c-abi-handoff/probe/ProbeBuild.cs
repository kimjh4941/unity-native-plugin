// Batch-mode build for the probe, and the Xcode project changes that stand in for the manual steps of
// section 2 of the hand-off documents.
//   Unity -batchmode -quit -buildTarget iOS -executeMethod ProbeBuild.Build
//         -ntkVariant handoff1|handoff1-asan|handoff2 -ntkSdk simulator|device [-ntkOut <dir>]
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEditor.iOS.Xcode.Extensions;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ProbeBuild
{
    const string ScenePath = "Assets/Scenes/Probe.unity";
    const string NativeToolkitBuild = "/Users/jonghyunkim/Desktop/native-toolkit/ios/Build";
    const string InstallerSource = "/Users/jonghyunkim/Desktop/native-toolkit/artifact/topics/ios-c-abi/results/probes/stage1a/handoff2/unity/NtkHandoff2Installer.mm";

    static string variant = "handoff1";
    static string sdk = "simulator";

    static string Arg(string name, string fallback)
    {
        var args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
    }

    // Returns the folder holding the two xcframeworks and their names (IosLibrary, IosLibraryCApi).
    static (string dir, string library, string capi) Frameworks(string v)
    {
        switch (v)
        {
            case "handoff1": return ($"{NativeToolkitBuild}/stage1a-handoff1", "ios-native-toolkit-handoff1.xcframework", "ios-native-toolkit-capi-handoff1.xcframework");
            case "handoff1-asan": return ($"{NativeToolkitBuild}/stage1a-handoff1/asan", "ios-native-toolkit-handoff1.xcframework", "ios-native-toolkit-capi-handoff1.xcframework");
            case "handoff2": return ($"{NativeToolkitBuild}/stage1a-handoff2", "ios-native-toolkit-handoff2.xcframework", "ios-native-toolkit-capi-handoff2.xcframework");
            default: throw new ArgumentException($"unknown variant {v}");
        }
    }

    static void EnsureScene()
    {
        if (File.Exists(ScenePath)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        new GameObject("Probe").AddComponent<ProbeController>();
        EditorSceneManager.SaveScene(scene, ScenePath);
    }

    public static void Build()
    {
        variant = Arg("-ntkVariant", "handoff1");
        sdk = Arg("-ntkSdk", "simulator");
        var output = Arg("-ntkOut", $"Builds/{variant}-{sdk}");
        Frameworks(variant);   // validates the variant

        EnsureScene();
        var ios = NamedBuildTarget.iOS;
        PlayerSettings.companyName = "jonghyunkim";
        PlayerSettings.productName = "NtkCAbiProbe";
        PlayerSettings.SetApplicationIdentifier(ios, Arg("-ntkBundleId", "com.jonghyunkim.nativetoolkit.cabiprobe"));
        PlayerSettings.iOS.targetOSVersionString = "18.0";
        PlayerSettings.iOS.sdkVersion = sdk == "device" ? iOSSdkVersion.DeviceSDK : iOSSdkVersion.SimulatorSDK;
        EditorUserBuildSettings.development = false;

        var defines = variant == "handoff2" ? new[] { "NTK_HANDOFF2" } : new string[0];
        if (Arg("-ntkPackage", "no") == "yes") defines = defines.Append("NTK_UNITY_NOTIFICATIONS").ToArray();
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = output,
            target = BuildTarget.iOS,
            options = BuildOptions.None,
            extraScriptingDefines = defines,
        });
        Debug.Log($"[ProbeBuild] variant={variant} sdk={sdk} result={report.summary.result} errors={report.summary.totalErrors} output={output}");
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorApplication.Exit(1);
    }

    [PostProcessBuild(100)]
    static void OnPostProcessBuild(BuildTarget target, string path)
    {
        if (target != BuildTarget.iOS) return;
        var (dir, library, capi) = Frameworks(variant);
        var projectPath = PBXProject.GetPBXProjectPath(path);
        var project = new PBXProject();
        project.ReadFromFile(projectPath);
        string app = project.GetUnityMainTargetGuid();
        string framework = project.GetUnityFrameworkTargetGuid();

        // Copy the xcframeworks into the exported project, so a later rebuild in native-toolkit cannot change them.
        var destination = Path.Combine(path, "NtkFrameworks");
        Directory.CreateDirectory(destination);
        foreach (var name in new[] { library, capi })
        {
            CopyDirectory(Path.Combine(dir, name), Path.Combine(destination, name));
            string guid = project.AddFile($"NtkFrameworks/{name}", $"Frameworks/{name}", PBXSourceTree.Source);
            project.AddFileToBuild(framework, guid);   // UnityFramework: link only (Do Not Embed)
            project.AddFileToBuild(app, guid);         // app: Embed & Sign
            project.AddFileToEmbedFrameworks(app, guid);
        }
        File.Copy(Path.Combine(dir, "BUILD-INFO.txt"), Path.Combine(destination, "BUILD-INFO.txt"), true);
        File.WriteAllText(Path.Combine(destination, "VARIANT.txt"), variant + "\n");

        if (variant == "handoff2")
        {
            // The install timing is a build setting, so it can be switched with xcodebuild NTK_INSTALL_AT=n
            // without exporting again.
            // Its NSLog calls are redirected to NtkProbeNSLog, which also writes the probe log file.
            const string pluginDir = "Libraries/Plugins/iOS";
            var installer = $"{pluginDir}/NtkHandoff2Installer.mm";
            Directory.CreateDirectory(Path.Combine(path, pluginDir));
            // The copy gets two lines on top; the source in native-toolkit stays as it is.
            File.WriteAllText(Path.Combine(path, installer),
                "#import \"NtkProbeLog.h\"  // added by ProbeBuild\n#define NSLog NtkProbeNSLog  // added by ProbeBuild\n" + File.ReadAllText(InstallerSource));
            foreach (var name in new[] { "NtkProbeLog.h", "NtkProbeLog.mm" })
                File.Copy(Path.Combine("Native", name), Path.Combine(path, pluginDir, name), true);
            string guid = project.AddFile(installer, installer, PBXSourceTree.Source);
            project.AddFileToBuildWithFlags(framework, guid, "-DNTK_HANDOFF2_INSTALL_AT=$(NTK_INSTALL_AT)");
            project.AddFileToBuild(framework, project.AddFile($"{pluginDir}/NtkProbeLog.mm", $"{pluginDir}/NtkProbeLog.mm", PBXSourceTree.Source));
            project.SetBuildProperty(framework, "NTK_INSTALL_AT", "1");
            // The installer uses UNUserNotificationCenter, which UnityFramework does not link by default.
            project.AddFrameworkToProject(framework, "UserNotifications.framework", false);
        }

        if (sdk == "device")
        {
            // Sign with the team's existing wildcard profile, so no new App ID is registered. The team ID is
            // passed to xcodebuild (DEVELOPMENT_TEAM) and never stored here.
            project.SetBuildProperty(app, "CODE_SIGN_STYLE", "Manual");
            project.SetBuildProperty(app, "PROVISIONING_PROFILE_SPECIFIER", "iOS Team Provisioning Profile: *");
            project.SetBuildProperty(framework, "CODE_SIGN_STYLE", "Manual");
        }

        project.WriteToFile(projectPath);
        Debug.Log($"[ProbeBuild] added {library} and {capi} from {dir}");
    }

    // U-4: adds the Unity notification package (the version Unity picks for this editor).
    public static void AddNotificationPackage()
    {
        var request = UnityEditor.PackageManager.Client.Add("com.unity.mobile.notifications");
        while (!request.IsCompleted) System.Threading.Thread.Sleep(200);
        Debug.Log(request.Status == UnityEditor.PackageManager.StatusCode.Success
            ? $"[ProbeBuild] added {request.Result.packageId}"
            : $"[ProbeBuild] add failed: {request.Error?.message}");
    }

    static void CopyDirectory(string source, string destination)
    {
        if (Directory.Exists(destination)) Directory.Delete(destination, true);
        foreach (var d in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(d.Replace(source, destination));
        Directory.CreateDirectory(destination);
        foreach (var f in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            File.Copy(f, f.Replace(source, destination), true);
    }
}
