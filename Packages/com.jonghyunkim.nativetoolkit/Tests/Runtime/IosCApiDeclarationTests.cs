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
        private static readonly string[] Expected =
        {
            "ntk_version",
            // Dialog (C ABI design v2, D-12).
            "ntk_dialog_show_alert_async",
            "ntk_dialog_show_confirm_async",
            "ntk_dialog_show_destructive_async",
            "ntk_dialog_show_action_sheet_async",
            "ntk_dialog_show_text_input_async",
            "ntk_dialog_show_login_async",
            "ntk_dialog_result_button_text",
            "ntk_dialog_result_text",
            "ntk_dialog_result_username",
            "ntk_dialog_result_password",
            "ntk_dialog_result_free",
            // Share (C ABI design v2, D-9).
            "ntk_share_content_async",
            "ntk_share_result_completed",
            "ntk_share_result_activity_type",
            "ntk_share_result_free",
        };

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

        /// <summary>
        /// The C# signature of each declaration, written from the header: a value struct passed instead of
        /// a pointer, a missing <c>out</c>, or a wrong width would keep the name and still crash on the
        /// device (implementation review v1 of the Dialog move, R-8 and R2-1). "ref"/"out" mark pointer
        /// parameters; IntPtr stands for a pointer or a function pointer.
        /// </summary>
        private static readonly Dictionary<string, string> Signatures = new()
        {
            ["ntk_version"] = "UInt32()",
            ["ntk_dialog_show_alert_async"] = "Int32(ref AlertRequest, IntPtr, IntPtr, IntPtr, IntPtr)",
            ["ntk_dialog_show_confirm_async"] = "Int32(ref ConfirmRequest, IntPtr, IntPtr, IntPtr, IntPtr)",
            ["ntk_dialog_show_destructive_async"] = "Int32(ref DestructiveRequest, IntPtr, IntPtr, IntPtr, IntPtr)",
            ["ntk_dialog_show_action_sheet_async"] = "Int32(ref ActionSheetRequest, IntPtr, IntPtr, IntPtr, IntPtr)",
            ["ntk_dialog_show_text_input_async"] = "Int32(ref TextInputRequest, IntPtr, IntPtr, IntPtr, IntPtr)",
            ["ntk_dialog_show_login_async"] = "Int32(ref LoginRequest, IntPtr, IntPtr, IntPtr, IntPtr)",
            ["ntk_dialog_result_button_text"] = "IntPtr(IntPtr, out UIntPtr)",
            ["ntk_dialog_result_text"] = "IntPtr(IntPtr, out UIntPtr)",
            ["ntk_dialog_result_username"] = "IntPtr(IntPtr, out UIntPtr)",
            ["ntk_dialog_result_password"] = "IntPtr(IntPtr, out UIntPtr)",
            ["ntk_dialog_result_free"] = "Void(IntPtr)",
            ["ntk_share_content_async"] = "Int32(ref ShareRequest, IntPtr, IntPtr, IntPtr)",
            ["ntk_share_result_completed"] = "Int32(IntPtr)",
            ["ntk_share_result_activity_type"] = "IntPtr(IntPtr, out UIntPtr)",
            ["ntk_share_result_free"] = "Void(IntPtr)",
        };

        private static string SignatureOf(MethodInfo method)
        {
            IEnumerable<string> parameters = method.GetParameters().Select(p =>
                p.ParameterType.IsByRef
                    ? (p.IsOut ? "out " : "ref ") + p.ParameterType.GetElementType()!.Name
                    : p.ParameterType.Name);
            return $"{method.ReturnType.Name}({string.Join(", ", parameters)})";
        }

        [Test]
        public void EveryDeclarationHasTheHeadersSignature()
        {
            Assembly runtime = typeof(IosNativeToolkitCApi).Assembly;
            const string ns = "JonghyunKim.NativeToolkit.Runtime.iOS";
            var found = new Dictionary<string, string>();
            foreach (Type type in runtime.GetTypes().Where(t => t.Namespace == ns || (t.Namespace?.StartsWith(ns + ".") ?? false)))
            {
                foreach (MethodInfo method in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    var import = method.GetCustomAttribute<DllImportAttribute>();
                    if (import != null) found[import.EntryPoint ?? method.Name] = SignatureOf(method);
                }
            }
            CollectionAssert.AreEquivalent(Signatures.Keys, found.Keys, "every declaration needs its signature here");
            foreach (var pair in Signatures)
                Assert.AreEqual(pair.Value, found[pair.Key], pair.Key);
        }

        [Test]
        public void TheDialogCallbackHasTheHeadersSignature()
        {
            // ntk_dialog_result_fn: void (void*, uint64_t, ntk_dialog_error, uint32_t, ntk_dialog_result*).
            MethodInfo invoke = typeof(JonghyunKim.NativeToolkit.Runtime.iOS.Dialog.IosDialogCApi.ResultCallback).GetMethod("Invoke")!;
            Assert.AreEqual("Void(IntPtr, UInt64, Int32, UInt32, IntPtr)", SignatureOf(invoke));
        }

        [Test]
        public void TheShareCallbackHasTheHeadersSignature()
        {
            // ntk_share_result_fn: void (void*, ntk_share_error, uint32_t, ntk_share_result*). No request ID.
            MethodInfo invoke = typeof(JonghyunKim.NativeToolkit.Runtime.iOS.Share.IosShareCApi.ResultCallback).GetMethod("Invoke")!;
            Assert.AreEqual("Void(IntPtr, Int32, UInt32, IntPtr)", SignatureOf(invoke));
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
