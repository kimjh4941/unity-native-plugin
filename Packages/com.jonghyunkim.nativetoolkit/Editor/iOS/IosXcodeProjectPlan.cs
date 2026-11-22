#nullable enable

namespace JonghyunKim.NativeToolkit.Editor.iOS
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;

    /// <summary>
    /// The decisions of <see cref="IosXcodeProjectSetup"/>, apart from the Xcode project API so that EditMode
    /// tests reach them on every platform (artifact/topics/ios-c-abi-2, 3-S design v3, 1.2).
    /// </summary>
    internal static class IosXcodeProjectPlan
    {
        /// <summary>What to do with the notification installer source in the exported project.</summary>
        internal enum InstallerAction
        {
            /// <summary>Leave it as it is.</summary>
            None,
            /// <summary>Copy it and add it to UnityFramework.</summary>
            Add,
            /// <summary>Remove its reference and its file (the opt-out was set after an export that had it).</summary>
            Remove,
        }

        /// <summary>The 1.x XCFrameworks, which must not stay in a project the 2.0.0 ones go into (F-1).</summary>
        private static readonly Regex Legacy = new(@"(?<![\w-])(?:ios-native-toolkit-1\.[0-9.]+|unity-ios-native-toolkit-[0-9.]+)(?:-debug)?\.xcframework");

        /// <summary>
        /// The error that stops the build, or <see langword="null"/>: every pinned XCFramework must be
        /// linked into UnityFramework once and embedded into the app once (Unity does both from Plugins/iOS
        /// and the .meta's AddToEmbeddedBinaries; the hook only checks), and no 1.x XCFramework may be
        /// referenced (a project exported with 1.x and appended to).
        /// </summary>
        internal static string? Problem(IReadOnlyList<string> pinned, string projectText)
        {
            var problems = new List<string>();
            foreach (string name in pinned)
            {
                // Entries of the build phases' file lists end with "*/," (the build file's own
                // definition line does not), so each phase entry counts once.
                int link = Count(projectText, $"{name} in Frameworks */,");
                int embed = Count(projectText, $"{name} in Embed Frameworks */,");
                if (link != 1 || embed != 1)
                    problems.Add($"{name} is linked {link} and embedded {embed} time(s), not once each");
            }
            string[] legacy = Legacy.Matches(projectText).Select(m => m.Value).Distinct().OrderBy(n => n).ToArray();
            if (legacy.Length > 0)
                problems.Add($"the project still references the 1.x XCFramework(s) {string.Join(", ", legacy)}");
            if (problems.Count == 0) return null;

            return "[NativeToolkit] The exported Xcode project is not set up for native-toolkit 2.0.0: " +
                string.Join("; ", problems) + ". Unity links the XCFrameworks in Packages/com.jonghyunkim.nativetoolkit/" +
                "Plugins/iOS and embeds them when their .meta has AddToEmbeddedBinaries; a project exported with 1.x " +
                "cannot be appended to - build into a new folder (Replace).";
        }

        /// <summary>Add the installer when it should be there and is not; remove it when it should not be and is.</summary>
        internal static InstallerAction Installer(bool skipInstall, bool present) =>
            (skipInstall, present) switch
            {
                (false, false) => InstallerAction.Add,
                (true, true) => InstallerAction.Remove,
                _ => InstallerAction.None,
            };

        internal static int Count(string text, string needle) => Regex.Matches(text, Regex.Escape(needle)).Count;
    }
}
