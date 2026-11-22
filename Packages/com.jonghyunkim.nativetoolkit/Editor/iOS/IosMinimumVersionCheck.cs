#nullable enable

namespace JonghyunKim.NativeToolkit.Editor.iOS
{
    using System;
    using UnityEditor;
    using UnityEditor.Build;
    using UnityEditor.Build.Reporting;

    /// <summary>
    /// Stops an iOS build whose minimum iOS version is below 18.0, before anything else runs, with a
    /// message that says what to change.
    /// </summary>
    /// <remarks>
    /// native-toolkit 2.0.0's frameworks are built for iOS 18.0 (minos 18.0). Unity exports and Xcode
    /// links a lower target with two warnings only, and the app then quits about a second after launch
    /// on iOS 17.x without a crash or a dyld error (artifact/topics/ios-c-abi-2 README, 6.3, S-8;
    /// decision I-16). The value is read at build time, so an active build profile's override counts.
    /// <para>
    /// It runs in every iOS build of a project that has the package, outside the
    /// NATIVETOOLKIT_ENABLE_BUILD_STEPS assembly, like the Android architecture check.
    /// </para>
    /// </remarks>
    internal sealed class IosMinimumVersionCheck : IPreprocessBuildWithReport
    {
        /// <summary>The minimum iOS version of native-toolkit 2.0.0's frameworks.</summary>
        internal static readonly Version Required = new(18, 0);

        /// <summary>Runs before the other build steps, so nothing is copied for a build that cannot succeed.</summary>
        public int callbackOrder => -100;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.iOS) return;

            string? error = Check(PlayerSettings.iOS.targetOSVersionString);
            if (error != null) throw new BuildFailedException(error);
        }

        /// <summary>
        /// The error for a minimum iOS version below <see cref="Required"/> or one that cannot be read,
        /// or <see langword="null"/> when it is high enough. A major version alone ("18") counts as ".0".
        /// </summary>
        internal static string? Check(string? minimum)
        {
            string text = (minimum ?? "").Trim();
            if (!text.Contains(".")) text += ".0";
            if (!Version.TryParse(text, out Version? version))
            {
                return $"[NativeToolkit] The minimum iOS version '{minimum}' in Player Settings > Other Settings > " +
                    "Target minimum iOS Version (or in the active build profile) cannot be read. Set it to 18.0 or later.";
            }
            if (version >= Required) return null;

            return $"[NativeToolkit] Native Toolkit for iOS needs iOS {Required.Major}.0 or later; the minimum iOS version " +
                $"is {minimum}. On iOS 17 and earlier the app quits right after launch. Set Player Settings > Other " +
                "Settings > Target minimum iOS Version (or the active build profile's) to 18.0 or later and build again.";
        }
    }
}
