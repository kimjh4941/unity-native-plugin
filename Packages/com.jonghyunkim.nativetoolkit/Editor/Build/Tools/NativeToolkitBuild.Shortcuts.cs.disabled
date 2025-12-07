#nullable enable

using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace Jonghyunkim.NativeToolkit.Editor.Build.Tools
{
    internal static class NativeToolkitBuildShortcuts
    {
        // .env を読み、現在の設定をログに表示
        [Shortcut("NativeToolkit/Build/Show .env Settings")]
        private static void ShowEnvSettings()
        {
            var env = BuildEnvUtil.ReadEnv(null);
            Debug.Log($"[NativeToolkitBuild] enableBuildSteps={env.enableBuildSteps}, extraDefines=[{string.Join(", ", env.extraDefines)}]");
        }

        // 実ビルド呼び出し（.env からパラメータを読み渡す）
        [Shortcut("NativeToolkit/Build/Android Dev")]
        private static void BuildAndroidDev()
        {
            var env = BuildEnvUtil.ReadEnv(null);
            NativeToolkitBuild.BuildAndroid(true, env.extraDefines, env.enableBuildSteps);
        }

        [Shortcut("NativeToolkit/Build/Android Release")]
        private static void BuildAndroidRelease()
        {
            var env = BuildEnvUtil.ReadEnv(null);
            NativeToolkitBuild.BuildAndroid(false, env.extraDefines, env.enableBuildSteps);
        }

        [Shortcut("NativeToolkit/Build/iOS Dev")]
        private static void BuildiOSDev()
        {
            var env = BuildEnvUtil.ReadEnv(null);
            NativeToolkitBuild.BuildiOS(true, env.extraDefines, env.enableBuildSteps);
        }

        [Shortcut("NativeToolkit/Build/iOS Release")]
        private static void BuildiOSRelease()
        {
            var env = BuildEnvUtil.ReadEnv(null);
            NativeToolkitBuild.BuildiOS(false, env.extraDefines, env.enableBuildSteps);
        }

        [Shortcut("NativeToolkit/Build/macOS Dev")]
        private static void BuildMacOSDev()
        {
            var env = BuildEnvUtil.ReadEnv(null);
            NativeToolkitBuild.BuildMacOS(true, env.extraDefines, env.enableBuildSteps);
        }

        [Shortcut("NativeToolkit/Build/macOS Release")]
        private static void BuildMacOSRelease()
        {
            var env = BuildEnvUtil.ReadEnv(null);
            NativeToolkitBuild.BuildMacOS(false, env.extraDefines, env.enableBuildSteps);
        }

        [Shortcut("NativeToolkit/Build/Windows Dev")]
        private static void BuildWindowsDev()
        {
            var env = BuildEnvUtil.ReadEnv(null);
            NativeToolkitBuild.BuildWindows(true, env.extraDefines, env.enableBuildSteps);
        }

        [Shortcut("NativeToolkit/Build/Windows Release")]
        private static void BuildWindowsRelease()
        {
            var env = BuildEnvUtil.ReadEnv(null);
            NativeToolkitBuild.BuildWindows(false, env.extraDefines, env.enableBuildSteps);
        }
    }
}