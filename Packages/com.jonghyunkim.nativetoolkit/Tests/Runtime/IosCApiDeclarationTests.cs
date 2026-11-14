#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using NUnit.Framework;
using JonghyunKim.NativeToolkit.Editor.iOS;
using JonghyunKim.NativeToolkit.Runtime.iOS.Common;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// The iOS C ABI declarations against the headers of the C ABI the package links
    /// (artifact/topics/ios-c-abi-2, decision I-15; 3-S design v3, 1.4). The headers are read from the
    /// package's own IosLibraryCApi.framework, so the check runs wherever the package is (also from
    /// Library/PackageCache) and always against the version that is linked.
    /// </summary>
    public sealed class IosCApiDeclarationTests
    {
        private const string PluginsIos = "Packages/com.jonghyunkim.nativetoolkit/Plugins/iOS";

        /// <summary>
        /// The <c>__Internal</c> functions declared in the package's iOS namespaces. Extended as each feature
        /// moves to the C ABI (stage 3).
        /// </summary>
        private static readonly string[] Expected = { "ntk_version" };

        private static IEnumerable<string> DeclaredNames()
        {
            Assembly runtime = typeof(IosNativeToolkitCApi).Assembly;
            const string ns = "JonghyunKim.NativeToolkit.Runtime.iOS";
            foreach (Type type in runtime.GetTypes().Where(t => t.Namespace == ns || (t.Namespace?.StartsWith(ns + ".") ?? false)))
            {
                foreach (MethodInfo method in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    var import = method.GetCustomAttribute<DllImportAttribute>();
                    if (import == null) continue;
                    Assert.That(import.Value, Is.EqualTo(IosNativeToolkitCApi.LibraryName), $"{type.Name}.{method.Name} is not __Internal");
                    yield return import.EntryPoint ?? method.Name;
                }
            }
        }

        private static string HeadersDirectory()
        {
            string pluginsIos = Path.GetFullPath(PluginsIos);
            IosXcframeworkPin pin = IosXcframeworkPin.Read(Path.Combine(pluginsIos, IosXcframeworkPin.FileName));
            string capi = pin.ResolvePresentIn(pluginsIos).Single(name => name.Contains("-capi-"));
            return Path.Combine(pluginsIos, capi, "ios-arm64", "IosLibraryCApi.framework", "Headers", "NativeToolkitC");
        }

        [Test]
        public void TheDeclarationsAreExactlyTheExpectedOnes()
        {
            // Exact: a declaration left inside !UNITY_EDITOR would drop out of this set and fail here,
            // and so would a leftover 1.x bridge declaration (stage 3-S removes all 43).
            CollectionAssert.AreEquivalent(Expected, DeclaredNames().ToArray());
        }

        [Test]
        public void EveryDeclarationIsInTheLinkedHeaders()
        {
            string directory = HeadersDirectory();
            Assert.That(Directory.Exists(directory), Is.True, $"no C ABI headers at {directory}");
            string headers = string.Join("\n", Directory.GetFiles(directory, "*.h").Select(File.ReadAllText));
            foreach (string name in DeclaredNames())
                Assert.That(Regex.IsMatch(headers, @"NTK_CALL\s+" + Regex.Escape(name) + @"\s*\("), Is.True, $"{name} is not in {directory}");
        }

        [TestCase(0x020000u, true)]
        [TestCase(0x020103u, true)]
        [TestCase(0x010300u, false)]
        [TestCase(0x030000u, false)]
        public void IsSupportedVersion_ChecksTheMajorVersion(uint packed, bool supported)
        {
            Assert.That(IosNativeToolkitCApi.IsSupportedVersion(packed), Is.EqualTo(supported));
        }
    }
}
