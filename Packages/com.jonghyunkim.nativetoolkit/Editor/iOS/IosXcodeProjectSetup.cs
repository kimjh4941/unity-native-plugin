#nullable enable

#if UNITY_IOS
namespace JonghyunKim.NativeToolkit.Editor.iOS
{
    using System.Collections.Generic;
    using System.IO;
    using UnityEditor;
    using UnityEditor.Build;
    using UnityEditor.Build.Reporting;
    using UnityEditor.iOS.Xcode;
    using UnityEngine;

    /// <summary>
    /// After an iOS export: checks that Unity linked and embedded native-toolkit 2.0.0's XCFrameworks, and
    /// adds the notification installer to UnityFramework (artifact/topics/ios-c-abi-2, decisions I-1 and
    /// I-2; 3-S design v3, 1.2).
    /// </summary>
    /// <remarks>
    /// Unity links the XCFrameworks in Plugins/iOS and embeds them through AddToEmbeddedBinaries in their
    /// .meta (stage 1, S-1 and S-2), so this hook adds neither: it stops the build when one is missing, and
    /// when a 1.x XCFramework is still referenced (a project exported with 1.x and appended to).
    /// <para>
    /// The installer (<c>Editor/iOS/NativeSource~/NtkNotificationInstaller.mm</c>) is copied into the
    /// exported project's <c>Libraries/com.jonghyunkim.nativetoolkit/</c> and built into UnityFramework, once
    /// however often the project is appended to. With NATIVETOOLKIT_IOS_SKIP_NOTIFICATION_INSTALL it is not
    /// added, and an installer an earlier export added is removed. The paths go through
    /// <see cref="Path.GetFullPath(string)"/>, so the package also works from Library/PackageCache (git URL).
    /// </para>
    /// <para>
    /// It runs in every iOS build of a project that has the package, outside the
    /// NATIVETOOLKIT_ENABLE_BUILD_STEPS assembly.
    /// </para>
    /// </remarks>
    internal sealed class IosXcodeProjectSetup : IPostprocessBuildWithReport
    {
        private const string LogTag = "[NativeToolkit][iOS][Setup]";
        private const string PackageRoot = "Packages/com.jonghyunkim.nativetoolkit";
        internal const string InstallerName = "NtkNotificationInstaller.mm";
        internal const string InstallerDir = "Libraries/com.jonghyunkim.nativetoolkit";

        public int callbackOrder => 100;

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.iOS) return;
            Apply(report.summary.outputPath);
        }

        internal static void Apply(string xcodeProjectDir)
        {
            string pluginsIos = Path.GetFullPath($"{PackageRoot}/Plugins/iOS");
            IosXcframeworkPin pin = IosXcframeworkPin.Read(Path.Combine(pluginsIos, IosXcframeworkPin.FileName));
            IReadOnlyList<string> names = pin.ResolvePresentIn(pluginsIos);

            string pbxPath = PBXProject.GetPBXProjectPath(xcodeProjectDir);
            var project = new PBXProject();
            project.ReadFromFile(pbxPath);

            string? problem = IosXcodeProjectPlan.Problem(names, project.WriteToString());
            if (problem != null) throw new BuildFailedException(problem);

#if NATIVETOOLKIT_IOS_SKIP_NOTIFICATION_INSTALL
            const bool skipInstall = true;
#else
            const bool skipInstall = false;
#endif
            string installerPath = $"{InstallerDir}/{InstallerName}";
            string installerFile = Path.Combine(xcodeProjectDir, installerPath);
            string? installerGuid = project.FindFileGuidByProjectPath(installerPath);
            switch (IosXcodeProjectPlan.Installer(skipInstall, installerGuid != null))
            {
                case IosXcodeProjectPlan.InstallerAction.Add:
                    Directory.CreateDirectory(Path.GetDirectoryName(installerFile)!);
                    File.Copy(Path.GetFullPath($"{PackageRoot}/Editor/iOS/NativeSource~/{InstallerName}"), installerFile, true);
                    project.AddFileToBuild(project.GetUnityFrameworkTargetGuid(), project.AddFile(installerPath, installerPath, PBXSourceTree.Source));
                    break;
                case IosXcodeProjectPlan.InstallerAction.Remove:
                    project.RemoveFile(installerGuid!);
                    if (File.Exists(installerFile)) File.Delete(installerFile);
                    break;
                default:
                    if (!skipInstall)
                    {
                        // Appended to: refresh the copy, so a newer package's installer is built.
                        File.Copy(Path.GetFullPath($"{PackageRoot}/Editor/iOS/NativeSource~/{InstallerName}"), installerFile, true);
                    }
                    break;
            }
            project.WriteToFile(pbxPath);

            string after = project.WriteToString();
            Debug.Log($"{LogTag} linked and embedded: {string.Join(", ", names)}; " +
                      $"installer entries: {IosXcodeProjectPlan.Count(after, $"{InstallerName} in Sources */,")} (skip: {skipInstall})");
        }
    }
}
#endif
