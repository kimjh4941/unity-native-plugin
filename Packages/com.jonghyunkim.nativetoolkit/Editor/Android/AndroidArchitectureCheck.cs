#nullable enable

namespace JonghyunKim.NativeToolkit.Editor.Android
{
    using UnityEditor;
    using UnityEditor.Build;
    using UnityEditor.Build.Reporting;

    /// <summary>
    /// Stops an Android build that targets a 32-bit ABI, before Gradle runs, with a message that says
    /// what to change.
    /// </summary>
    /// <remarks>
    /// native-toolkit 2.0.0 ships libntk.so for arm64-v8a and x86_64 only, and its C ABI AAR carries a
    /// Prefab module for C/C++ consumers. Unity's generated unityLibrary enables Prefab (for
    /// GameActivity), so a build that also targets ARMv7 fails inside Gradle with
    /// "[CXX1210] No compatible library found [//ntk/ntk]", which names neither the cause nor the fix.
    /// 32-bit Android is out of scope (artifact/topics/android-c-abi-2, J-10): Google Play requires
    /// 64-bit, and only IL2CPP builds ARM64.
    /// <para>
    /// It runs in every Android build of a project that has the package, outside the
    /// NATIVETOOLKIT_ENABLE_BUILD_STEPS assembly, like <see cref="AndroidGradleDependencies"/>.
    /// </para>
    /// </remarks>
    internal sealed class AndroidArchitectureCheck : IPreprocessBuildWithReport
    {
        /// <summary>The ABIs libntk.so is built for.</summary>
        internal const AndroidArchitecture Supported = AndroidArchitecture.ARM64 | AndroidArchitecture.X86_64;

        /// <summary>Runs before the other build steps, so nothing is copied for a build that cannot succeed.</summary>
        public int callbackOrder => -100;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.Android) return;

            string? error = Check(PlayerSettings.Android.targetArchitectures);
            if (error != null) throw new BuildFailedException(error);
        }

        /// <summary>
        /// The error for target architectures libntk.so is not built for, or <see langword="null"/>
        /// when every target is supported.
        /// </summary>
        internal static string? Check(AndroidArchitecture targets)
        {
            AndroidArchitecture unsupported = targets & ~Supported;
            if (unsupported == 0 && (targets & Supported) != 0) return null;

            return "[NativeToolkit] Native Toolkit for Android supports ARM64 (and x86_64) only; its native " +
                $"library is not built for {unsupported}. Remove 32-bit architectures from Player Settings > " +
                "Other Settings > Target Architectures (or from the active build profile) and build again.";
        }
    }
}
