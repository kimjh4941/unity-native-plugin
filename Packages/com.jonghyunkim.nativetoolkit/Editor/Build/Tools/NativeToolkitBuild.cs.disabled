#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Jonghyunkim.NativeToolkit.Editor.Build.Tools
{
    public static class NativeToolkitBuild
    {
        // .env から読み込んでビルド（任意のパス指定可。未指定はプロジェクト直下の .env.nativetoolkit → .env）
        public static void BuildAndroidReleaseFromEnv(string? envPath = null)
        {
            var (defs, enable) = BuildEnvUtil.ReadEnv(envPath);
            BuildAndroid(false, defs, enable);
        }
        public static void BuildAndroidDevFromEnv(string? envPath = null)
        {
            var (defs, enable) = BuildEnvUtil.ReadEnv(envPath);
            BuildAndroid(true, defs, enable);
        }
        public static void BuildiOSReleaseFromEnv(string? envPath = null)
        {
            var (defs, enable) = BuildEnvUtil.ReadEnv(envPath);
            BuildiOS(false, defs, enable);
        }
        public static void BuildiOSDevFromEnv(string? envPath = null)
        {
            var (defs, enable) = BuildEnvUtil.ReadEnv(envPath);
            BuildiOS(true, defs, enable);
        }
        public static void BuildMacOSReleaseFromEnv(string? envPath = null)
        {
            var (defs, enable) = BuildEnvUtil.ReadEnv(envPath);
            BuildMacOS(false, defs, enable);
        }
        public static void BuildMacOSDevFromEnv(string? envPath = null)
        {
            var (defs, enable) = BuildEnvUtil.ReadEnv(envPath);
            BuildMacOS(true, defs, enable);
        }
        public static void BuildWindowsReleaseFromEnv(string? envPath = null)
        {
            var (defs, enable) = BuildEnvUtil.ReadEnv(envPath);
            BuildWindows(false, defs, enable);
        }
        public static void BuildWindowsDevFromEnv(string? envPath = null)
        {
            var (defs, enable) = BuildEnvUtil.ReadEnv(envPath);
            BuildWindows(true, defs, enable);
        }

        // 各プラットフォーム
        public static void BuildAndroid(bool development, string[]? extraDefines = null, bool enableBuildSteps = true)
        {
            string path = "Builds/Android/app.apk";
            BuildWithOptions(BuildTarget.Android, path, development, extraDefines, enableBuildSteps);
        }

        public static void BuildiOS(bool development, string[]? extraDefines = null, bool enableBuildSteps = true)
        {
            string path = "Builds/iOS";
            BuildWithOptions(BuildTarget.iOS, path, development, extraDefines, enableBuildSteps);
        }

        public static void BuildMacOS(bool development, string[]? extraDefines = null, bool enableBuildSteps = true)
        {
            string path = "Builds/macOS/NativeToolkit.app";
            BuildWithOptions(BuildTarget.StandaloneOSX, path, development, extraDefines, enableBuildSteps);
        }

        public static void BuildWindows(bool development, string[]? extraDefines = null, bool enableBuildSteps = true)
        {
            string path = "Builds/Windows/NativeToolkit.exe";
            BuildWithOptions(BuildTarget.StandaloneWindows64, path, development, extraDefines, enableBuildSteps);
        }

        // 共通ビルドロジック
        private static void BuildWithOptions(BuildTarget target, string locationPathName, bool development,
                                             string[]? extraDefines = null, bool enableBuildSteps = true)
        {
            var group = BuildPipeline.GetBuildTargetGroup(target);
            EditorUserBuildSettings.SwitchActiveBuildTarget(group, target);

            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
            {
                Debug.LogError("[Build] No enabled scenes in Build Settings.");
                return;
            }

            var options = development
                ? BuildOptions.Development | BuildOptions.AllowDebugging
                : BuildOptions.None;

            var defines = new List<string>();
            if (enableBuildSteps) defines.Add("NATIVETOOLKIT_ENABLE_BUILD_STEPS");
            if (extraDefines != null && extraDefines.Length > 0) defines.AddRange(extraDefines);

            var bpo = new BuildPlayerOptions
            {
                target = target,
                locationPathName = locationPathName,
                scenes = scenes,
                options = options,
                extraScriptingDefines = defines.Count > 0 ? defines.ToArray() : System.Array.Empty<string>()
            };

            EnsureOutputDir(locationPathName);

            Debug.Log($"[Build] Start {target} {(development ? "Development" : "Release")} → {locationPathName}");
            BuildReport report = BuildPipeline.BuildPlayer(bpo);

            if (report.summary.result == BuildResult.Succeeded)
                Debug.Log($"[Build] SUCCESS: {report.summary.outputPath}");
            else
                throw new System.Exception("Build failed: " + report.summary.result);
        }

        private static void EnsureOutputDir(string locationPathName)
        {
            var dir = Path.HasExtension(locationPathName)
                ? Path.GetDirectoryName(locationPathName)
                : locationPathName;
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir!);
        }
    }
}