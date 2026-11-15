#nullable enable

using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.TestTools;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

[assembly: TestPlayerBuildModifier(typeof(JonghyunKim.NativeToolkit.IosPlayerTests.Editor.IosPlayerBuildSetup))]
[assembly: PostBuildCleanup(typeof(JonghyunKim.NativeToolkit.IosPlayerTests.Editor.IosPlayerBuildSetup))]

namespace JonghyunKim.NativeToolkit.IosPlayerTests.Editor
{
    /// <summary>
    /// Turns an iOS player test run into "export only" for scripts/verify_unity_ios.sh
    /// (artifact/topics/ios-c-abi-2, UI automation design v4, 3.1-3.2; stage 2 spike, README 6.4).
    /// </summary>
    /// <remarks>
    /// <para>
    /// It acts only when the Editor was started with <c>-ntkIosPlayerSdk simulator|device</c> and the
    /// target is iOS; otherwise (the Test Runner window, Android and Windows player runs) it does nothing.
    /// It lives in this repository's Assets, not in the package, so package users never get it.
    /// </para>
    /// <para>
    /// The attributes are assembly-level: the Test Framework only collects class- and method-level
    /// ones from tests compiled in the Editor, and the iOS player tests are not.
    /// </para>
    /// <para>
    /// The build options lose AutoRunPlayer (the script launches the player with simctl, so Xcode and
    /// Simulator.app stay closed), ConnectToHost and WaitForPlayerConnection (no Editor to report to;
    /// with -doNotReportTestResultsBackToEditor on the Editor's command line, the test scene has no
    /// RemoteTestResultSender, which the cleanup counts and logs). Development (and the options that
    /// depend on it) goes too: a development player triggers the local network prompt on a device, which
    /// pauses Unity until someone answers it. The player gets its own bundle id, and
    /// for the simulator the Simulator SDK and arm64. Those Player Settings are restored in the cleanup;
    /// they take effect even when the active build profile overrides Player Settings (spike V-2, V-3).
    /// </para>
    /// </remarks>
    public sealed class IosPlayerBuildSetup : ITestPlayerBuildModifier, IPostBuildCleanup
    {
        public const string LogTag = "[NTK-IOS-PLAYER]";
        public const string BundleId = "com.jonghyunkim.nativetoolkit.playertests";

        /// <summary>
        /// Defines added to a run's player scripts (3-S design v3, 1.7). NTK_IOS_CABI_SWITCHED: the package
        /// is on native-toolkit 2.0.0, so the tests of a feature not moved yet expect its unavailable path
        /// (NTK_IOS_CABI_2_&lt;FEATURE&gt; joins it when the feature moves). NTK_IOS_PLAYER_PROBE: the
        /// exported project gets NativeSource~/NtkPlayerTestsProbe.mm (<see cref="IosPlayerProbeInstaller"/>),
        /// so the tests that call it link. Build profiles do not override extra defines.
        /// </summary>
        public static readonly string[] Defines = { "NTK_IOS_CABI_SWITCHED", "NTK_IOS_CABI_2_DIALOG", "NTK_IOS_PLAYER_PROBE" };

        /// <summary>Whether the current build is a player test run of verify_unity_ios.sh.</summary>
        internal static bool IsActive => s_Active;

        // Static: the cleanup runs on a new instance (TestPlayerBuildModifierAttribute's own example).
        private static bool s_Active;
        private static iOSSdkVersion s_Sdk;
        private static AppleMobileArchitectureSimulator s_Arch;
        private static string s_BundleId = string.Empty;

        public BuildPlayerOptions ModifyOptions(BuildPlayerOptions options)
        {
            string? sdk = Argument("-ntkIosPlayerSdk");
            if (sdk == null || options.target != BuildTarget.iOS)
                return options;
            if (sdk != "simulator" && sdk != "device")
                throw new ArgumentException($"{LogTag} -ntkIosPlayerSdk must be simulator or device, not '{sdk}'");
            string output = Argument("-ntkIosPlayerOut")
                ?? throw new ArgumentException($"{LogTag} -ntkIosPlayerOut is required with -ntkIosPlayerSdk");

            s_Active = true;
            s_Sdk = PlayerSettings.iOS.sdkVersion;
            s_Arch = PlayerSettings.iOS.simulatorSdkArchitecture;
            s_BundleId = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.iOS);

            // Not a development player either: a development player looks for the Unity Editor on the local
            // network, and iOS then asks for local network access after every install. The prompt makes the
            // app inactive, Unity pauses, and the run never finishes while nobody answers it (the device run
            // of 2026-11-14 23:14 stopped with no result; the UI harness had the same prompt, 5026db0).
            options.options &= ~(BuildOptions.AutoRunPlayer | BuildOptions.ConnectToHost | BuildOptions.WaitForPlayerConnection
                | BuildOptions.Development | BuildOptions.ConnectWithProfiler
                | BuildOptions.AllowDebugging | BuildOptions.EnableDeepProfilingSupport);
            options.locationPathName = output;
            options.extraScriptingDefines = (options.extraScriptingDefines ?? Array.Empty<string>()).Concat(Defines).Distinct().ToArray();
            if (sdk == "simulator")
            {
                PlayerSettings.iOS.sdkVersion = iOSSdkVersion.SimulatorSDK;
                PlayerSettings.iOS.simulatorSdkArchitecture = AppleMobileArchitectureSimulator.ARM64;
            }
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, BundleId);
            Debug.Log($"{LogTag} export sdk={sdk} options={options.options} defines={string.Join(",", options.extraScriptingDefines)}");
            return options;
        }

        public void Cleanup()
        {
            if (!s_Active)
                return;
            s_Active = false;
            Debug.Log($"{LogTag} remote-sender-count={CountInLoadedScenes("RemoteTestResultSender")} runner-callback-count={CountInLoadedScenes("PlayModeRunnerCallback")}");
            PlayerSettings.iOS.sdkVersion = s_Sdk;
            PlayerSettings.iOS.simulatorSdkArchitecture = s_Arch;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, s_BundleId);
            Debug.Log($"{LogTag} player settings restored");
            // From the command line nothing else ends the Editor after an export-only run. Exiting here
            // leaves the test scene (Assets/InitTestScene*) behind; the script removes it.
            if (Environment.GetCommandLineArgs().Contains("-runTests"))
                EditorApplication.update += () => EditorApplication.Exit(0);
        }

        // The component types are internal to the Test Framework, so they are matched by name. The
        // runner callback is always there and shows the scene was actually read.
        private static int CountInLoadedScenes(string typeName)
        {
            int count = 0;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                    continue;
                foreach (GameObject root in scene.GetRootGameObjects())
                    count += root.GetComponentsInChildren<MonoBehaviour>(true).Count(c => c != null && c.GetType().Name == typeName);
            }
            return count;
        }

        private static string? Argument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }
}
