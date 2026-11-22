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
            // Clipboard (C ABI design v2, D-10).
            "ntk_clipboard_copy_async",
            "ntk_clipboard_append_async",
            "ntk_clipboard_read_async",
            "ntk_clipboard_read_data_async",
            "ntk_clipboard_get_snapshot_async",
            "ntk_clipboard_clear_async",
            "ntk_clipboard_create_pasteboard_async",
            "ntk_clipboard_remove_pasteboard_async",
            "ntk_clipboard_detect_patterns_async",
            "ntk_clipboard_detect_values_async",
            "ntk_clipboard_load_item_async",
            "ntk_clipboard_cancel_load",
            "ntk_clipboard_start_observing_async",
            "ntk_clipboard_stop_observing_async",
            "ntk_clipboard_check_foreground_change_async",
            "ntk_clipboard_add_change_listener",
            "ntk_clipboard_listener_remove",
            "ntk_clipboard_read_result_item_count",
            "ntk_clipboard_read_result_item_type_count",
            "ntk_clipboard_read_result_item_type_at",
            "ntk_clipboard_read_result_item_text_at",
            "ntk_clipboard_read_result_item_url_at",
            "ntk_clipboard_read_result_item_image_type_at",
            "ntk_clipboard_read_result_free",
            "ntk_clipboard_snapshot_has_strings",
            "ntk_clipboard_snapshot_has_urls",
            "ntk_clipboard_snapshot_has_images",
            "ntk_clipboard_snapshot_has_colors",
            "ntk_clipboard_snapshot_number_of_items",
            "ntk_clipboard_snapshot_type_count",
            "ntk_clipboard_snapshot_type_at",
            "ntk_clipboard_snapshot_item_type_count",
            "ntk_clipboard_snapshot_item_type_at",
            "ntk_clipboard_snapshot_has_matching",
            "ntk_clipboard_snapshot_matching_count",
            "ntk_clipboard_snapshot_matching_at",
            "ntk_clipboard_snapshot_free",
            "ntk_clipboard_loaded_item_kind",
            "ntk_clipboard_loaded_item_text",
            "ntk_clipboard_loaded_item_url",
            "ntk_clipboard_loaded_item_image_data",
            "ntk_clipboard_loaded_item_image_type",
            "ntk_clipboard_loaded_item_file_path",
            "ntk_clipboard_loaded_item_free",
            "ntk_clipboard_change_kind",
            "ntk_clipboard_change_scope_kind",
            "ntk_clipboard_change_scope_name",
            "ntk_clipboard_change_types_added_count",
            "ntk_clipboard_change_types_added_at",
            "ntk_clipboard_change_types_removed_count",
            "ntk_clipboard_change_types_removed_at",
            "ntk_clipboard_change_free",
            "ntk_string_data",
            "ntk_string_size",
            "ntk_string_free",
            "ntk_bytes_data",
            "ntk_bytes_size",
            "ntk_bytes_free",
            // Notification (C ABI design v4, D-1).
            "ntk_notification_install",
            "ntk_notification_show_async",
            "ntk_notification_update_async",
            "ntk_notification_schedule_async",
            "ntk_notification_remove_async",
            "ntk_notification_remove_all_async",
            "ntk_notification_cancel_scheduled_async",
            "ntk_notification_cancel_all_scheduled_async",
            "ntk_notification_get_scheduled_async",
            "ntk_notification_get_delivered_async",
            "ntk_notification_request_permission_async",
            "ntk_notification_get_authorization_status_async",
            "ntk_notification_has_permission_async",
            "ntk_notification_open_settings_async",
            "ntk_notification_set_badge_count_async",
            "ntk_notification_register_category_async",
            "ntk_notification_remove_category_async",
            "ntk_notification_add_interaction_listener",
            "ntk_notification_content_create",
            "ntk_notification_content_free",
            "ntk_notification_content_set_subtitle",
            "ntk_notification_content_set_body",
            "ntk_notification_content_set_badge",
            "ntk_notification_content_set_sound",
            "ntk_notification_content_set_category",
            "ntk_notification_content_set_interruption_level",
            "ntk_notification_content_set_thread",
            "ntk_notification_content_set_target_content",
            "ntk_notification_content_set_relevance_score",
            "ntk_notification_content_set_filter_criteria",
            "ntk_notification_content_set_user_info_json",
            "ntk_notification_content_add_attachment",
            "ntk_notification_interaction_kind",
            "ntk_notification_interaction_notification_id",
            "ntk_notification_interaction_action_id",
            "ntk_notification_interaction_user_text",
            "ntk_notification_interaction_user_info_json",
            "ntk_notification_interaction_free",
            "ntk_notification_list_count",
            "ntk_notification_list_identifier_at",
            "ntk_notification_list_title_at",
            "ntk_notification_list_subtitle_at",
            "ntk_notification_list_body_at",
            "ntk_notification_list_category_at",
            "ntk_notification_list_date_millis_at",
            "ntk_notification_list_free",
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
            ["ntk_clipboard_copy_async"] = "Int32(IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, IntPtr)",
            ["ntk_clipboard_append_async"] = "Int32(IntPtr, IntPtr, IntPtr, IntPtr, IntPtr)",
            ["ntk_clipboard_read_async"] = "Int32(IntPtr, IntPtr, IntPtr, IntPtr)",
            ["ntk_clipboard_read_data_async"] = "Int32(IntPtr, IntPtr, IntPtr, IntPtr, IntPtr)",
            ["ntk_clipboard_get_snapshot_async"] = "Int32(IntPtr, UIntPtr, IntPtr, IntPtr, IntPtr, IntPtr)",
            ["ntk_clipboard_clear_async"] = "Int32(IntPtr, IntPtr, IntPtr, IntPtr)",
            ["ntk_clipboard_create_pasteboard_async"] = "Int32(Int32, IntPtr, IntPtr, IntPtr, IntPtr)",
            ["ntk_clipboard_remove_pasteboard_async"] = "Int32(IntPtr, IntPtr, IntPtr, IntPtr)",
            ["ntk_clipboard_detect_patterns_async"] = "Int32(UInt32, IntPtr, IntPtr, IntPtr, IntPtr)",
            ["ntk_clipboard_detect_values_async"] = "Int32(UInt32, IntPtr, IntPtr, IntPtr, IntPtr)",
            ["ntk_clipboard_load_item_async"] = "Int32(Int32, IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, out UInt64)",
            ["ntk_clipboard_cancel_load"] = "Int32(UInt64)",
            ["ntk_clipboard_start_observing_async"] = "Int32(IntPtr, IntPtr, IntPtr, IntPtr)",
            ["ntk_clipboard_stop_observing_async"] = "Int32(IntPtr, IntPtr, IntPtr)",
            ["ntk_clipboard_check_foreground_change_async"] = "Int32(IntPtr, IntPtr, IntPtr, IntPtr)",
            ["ntk_clipboard_add_change_listener"] = "Int32(IntPtr, IntPtr, IntPtr, out IntPtr)",
            ["ntk_clipboard_listener_remove"] = "Void(IntPtr)",
            ["ntk_clipboard_read_result_item_count"] = "UIntPtr(IntPtr)",
            ["ntk_clipboard_read_result_item_type_count"] = "UIntPtr(IntPtr, UIntPtr)",
            ["ntk_clipboard_read_result_item_type_at"] = "IntPtr(IntPtr, UIntPtr, UIntPtr, out UIntPtr)",
            ["ntk_clipboard_read_result_item_text_at"] = "IntPtr(IntPtr, UIntPtr, out UIntPtr)",
            ["ntk_clipboard_read_result_item_url_at"] = "IntPtr(IntPtr, UIntPtr, out UIntPtr)",
            ["ntk_clipboard_read_result_item_image_type_at"] = "IntPtr(IntPtr, UIntPtr, out UIntPtr)",
            ["ntk_clipboard_read_result_free"] = "Void(IntPtr)",
            ["ntk_clipboard_snapshot_has_strings"] = "Int32(IntPtr)",
            ["ntk_clipboard_snapshot_has_urls"] = "Int32(IntPtr)",
            ["ntk_clipboard_snapshot_has_images"] = "Int32(IntPtr)",
            ["ntk_clipboard_snapshot_has_colors"] = "Int32(IntPtr)",
            ["ntk_clipboard_snapshot_number_of_items"] = "UIntPtr(IntPtr)",
            ["ntk_clipboard_snapshot_type_count"] = "UIntPtr(IntPtr)",
            ["ntk_clipboard_snapshot_type_at"] = "IntPtr(IntPtr, UIntPtr, out UIntPtr)",
            ["ntk_clipboard_snapshot_item_type_count"] = "UIntPtr(IntPtr, UIntPtr)",
            ["ntk_clipboard_snapshot_item_type_at"] = "IntPtr(IntPtr, UIntPtr, UIntPtr, out UIntPtr)",
            ["ntk_clipboard_snapshot_has_matching"] = "Int32(IntPtr)",
            ["ntk_clipboard_snapshot_matching_count"] = "UIntPtr(IntPtr)",
            ["ntk_clipboard_snapshot_matching_at"] = "Int64(IntPtr, UIntPtr)",
            ["ntk_clipboard_snapshot_free"] = "Void(IntPtr)",
            ["ntk_clipboard_loaded_item_kind"] = "Int32(IntPtr)",
            ["ntk_clipboard_loaded_item_text"] = "IntPtr(IntPtr, out UIntPtr)",
            ["ntk_clipboard_loaded_item_url"] = "IntPtr(IntPtr, out UIntPtr)",
            ["ntk_clipboard_loaded_item_image_data"] = "IntPtr(IntPtr, out UIntPtr)",
            ["ntk_clipboard_loaded_item_image_type"] = "IntPtr(IntPtr, out UIntPtr)",
            ["ntk_clipboard_loaded_item_file_path"] = "IntPtr(IntPtr, out UIntPtr)",
            ["ntk_clipboard_loaded_item_free"] = "Void(IntPtr)",
            ["ntk_clipboard_change_kind"] = "Int32(IntPtr)",
            ["ntk_clipboard_change_scope_kind"] = "Int32(IntPtr)",
            ["ntk_clipboard_change_scope_name"] = "IntPtr(IntPtr, out UIntPtr)",
            ["ntk_clipboard_change_types_added_count"] = "UIntPtr(IntPtr)",
            ["ntk_clipboard_change_types_added_at"] = "IntPtr(IntPtr, UIntPtr, out UIntPtr)",
            ["ntk_clipboard_change_types_removed_count"] = "UIntPtr(IntPtr)",
            ["ntk_clipboard_change_types_removed_at"] = "IntPtr(IntPtr, UIntPtr, out UIntPtr)",
            ["ntk_clipboard_change_free"] = "Void(IntPtr)",
            ["ntk_string_data"] = "IntPtr(IntPtr)",
            ["ntk_string_size"] = "UIntPtr(IntPtr)",
            ["ntk_string_free"] = "Void(IntPtr)",
            ["ntk_bytes_data"] = "IntPtr(IntPtr)",
            ["ntk_bytes_size"] = "UIntPtr(IntPtr)",
            ["ntk_bytes_free"] = "Void(IntPtr)",
            ["ntk_notification_install"] = "Int32()",
            ["ntk_notification_show_async"] = "Int32(IntPtr, IntPtr, IntPtr, IntPtr, IntPtr)",
            ["ntk_notification_update_async"] = "Int32(IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, IntPtr)",
            ["ntk_notification_schedule_async"] = "Int32(IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, IntPtr)",
            ["ntk_notification_remove_async"] = "Int32(IntPtr, IntPtr, IntPtr, IntPtr)",
            ["ntk_notification_remove_all_async"] = "Int32(IntPtr, IntPtr, IntPtr)",
            ["ntk_notification_cancel_scheduled_async"] = "Int32(IntPtr, IntPtr, IntPtr, IntPtr)",
            ["ntk_notification_cancel_all_scheduled_async"] = "Int32(IntPtr, IntPtr, IntPtr)",
            ["ntk_notification_get_scheduled_async"] = "Int32(IntPtr, IntPtr, IntPtr)",
            ["ntk_notification_get_delivered_async"] = "Int32(IntPtr, IntPtr, IntPtr)",
            ["ntk_notification_request_permission_async"] = "Int32(UInt32, IntPtr, IntPtr, IntPtr)",
            ["ntk_notification_get_authorization_status_async"] = "Int32(IntPtr, IntPtr, IntPtr)",
            ["ntk_notification_has_permission_async"] = "Int32(IntPtr, IntPtr, IntPtr)",
            ["ntk_notification_open_settings_async"] = "Int32(IntPtr, IntPtr, IntPtr)",
            ["ntk_notification_set_badge_count_async"] = "Int32(Int32, IntPtr, IntPtr, IntPtr)",
            ["ntk_notification_register_category_async"] = "Int32(IntPtr, IntPtr, IntPtr, IntPtr)",
            ["ntk_notification_remove_category_async"] = "Int32(IntPtr, IntPtr, IntPtr, IntPtr)",
            ["ntk_notification_add_interaction_listener"] = "Int32(IntPtr, IntPtr, IntPtr, out IntPtr)",
            ["ntk_notification_content_create"] = "Int32(IntPtr, IntPtr, out IntPtr)",
            ["ntk_notification_content_free"] = "Void(IntPtr)",
            ["ntk_notification_content_set_subtitle"] = "Int32(IntPtr, IntPtr)",
            ["ntk_notification_content_set_body"] = "Int32(IntPtr, IntPtr)",
            ["ntk_notification_content_set_badge"] = "Int32(IntPtr, Int32)",
            ["ntk_notification_content_set_sound"] = "Int32(IntPtr, Int32, IntPtr)",
            ["ntk_notification_content_set_category"] = "Int32(IntPtr, IntPtr)",
            ["ntk_notification_content_set_interruption_level"] = "Int32(IntPtr, Int32)",
            ["ntk_notification_content_set_thread"] = "Int32(IntPtr, IntPtr)",
            ["ntk_notification_content_set_target_content"] = "Int32(IntPtr, IntPtr)",
            ["ntk_notification_content_set_relevance_score"] = "Int32(IntPtr, Double)",
            ["ntk_notification_content_set_filter_criteria"] = "Int32(IntPtr, IntPtr)",
            ["ntk_notification_content_set_user_info_json"] = "Int32(IntPtr, IntPtr)",
            ["ntk_notification_content_add_attachment"] = "Int32(IntPtr, IntPtr, IntPtr)",
            ["ntk_notification_interaction_kind"] = "Int32(IntPtr)",
            ["ntk_notification_interaction_notification_id"] = "IntPtr(IntPtr, out UIntPtr)",
            ["ntk_notification_interaction_action_id"] = "IntPtr(IntPtr, out UIntPtr)",
            ["ntk_notification_interaction_user_text"] = "IntPtr(IntPtr, out UIntPtr)",
            ["ntk_notification_interaction_user_info_json"] = "IntPtr(IntPtr, out UIntPtr)",
            ["ntk_notification_interaction_free"] = "Void(IntPtr)",
            ["ntk_notification_list_count"] = "UIntPtr(IntPtr)",
            ["ntk_notification_list_identifier_at"] = "IntPtr(IntPtr, UIntPtr, out UIntPtr)",
            ["ntk_notification_list_title_at"] = "IntPtr(IntPtr, UIntPtr, out UIntPtr)",
            ["ntk_notification_list_subtitle_at"] = "IntPtr(IntPtr, UIntPtr, out UIntPtr)",
            ["ntk_notification_list_body_at"] = "IntPtr(IntPtr, UIntPtr, out UIntPtr)",
            ["ntk_notification_list_category_at"] = "IntPtr(IntPtr, UIntPtr, out UIntPtr)",
            ["ntk_notification_list_date_millis_at"] = "Int64(IntPtr, UIntPtr)",
            ["ntk_notification_list_free"] = "Void(IntPtr)",
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

        /// <summary>The callback types of Clipboard.h (C ABI design v2, D-2) and ntk_release_fn of Common.h.</summary>
        [TestCase(typeof(JonghyunKim.NativeToolkit.Runtime.iOS.Clipboard.IosClipboardCApi.DoneCallback), "Void(IntPtr, Int32, UInt32)")]
        [TestCase(typeof(JonghyunKim.NativeToolkit.Runtime.iOS.Clipboard.IosClipboardCApi.HandleCallback), "Void(IntPtr, Int32, UInt32, IntPtr)")]
        [TestCase(typeof(JonghyunKim.NativeToolkit.Runtime.iOS.Clipboard.IosClipboardCApi.PatternsCallback), "Void(IntPtr, Int32, UInt32, UInt32)")]
        [TestCase(typeof(JonghyunKim.NativeToolkit.Runtime.iOS.Clipboard.IosClipboardCApi.LoadCallback), "Void(IntPtr, UInt64, Int32, UInt32, IntPtr)")]
        [TestCase(typeof(JonghyunKim.NativeToolkit.Runtime.iOS.Clipboard.IosClipboardCApi.ForegroundChangeCallback), "Void(IntPtr, Int32, UInt32, Int32)")]
        [TestCase(typeof(JonghyunKim.NativeToolkit.Runtime.iOS.Clipboard.IosClipboardCApi.ChangeCallback), "Void(IntPtr, IntPtr)")]
        [TestCase(typeof(JonghyunKim.NativeToolkit.Runtime.iOS.Clipboard.IosClipboardCApi.ReleaseCallback), "Void(IntPtr)")]
        public void TheClipboardCallbacksHaveTheHeadersSignatures(Type callback, string signature)
        {
            Assert.AreEqual(signature, SignatureOf(callback.GetMethod("Invoke")!));
        }

        /// <summary>The callback types of Notification.h (C ABI design v4, D-2).</summary>
        [TestCase(typeof(JonghyunKim.NativeToolkit.Runtime.iOS.Notification.IosNotificationCApi.DoneCallback), "Void(IntPtr, Int32, UInt32)")]
        [TestCase(typeof(JonghyunKim.NativeToolkit.Runtime.iOS.Notification.IosNotificationCApi.PermissionCallback), "Void(IntPtr, Int32, UInt32, Int32)")]
        [TestCase(typeof(JonghyunKim.NativeToolkit.Runtime.iOS.Notification.IosNotificationCApi.StatusCallback), "Void(IntPtr, Int32, UInt32, Int32)")]
        [TestCase(typeof(JonghyunKim.NativeToolkit.Runtime.iOS.Notification.IosNotificationCApi.ListCallback), "Void(IntPtr, Int32, UInt32, IntPtr)")]
        [TestCase(typeof(JonghyunKim.NativeToolkit.Runtime.iOS.Notification.IosNotificationCApi.InteractionCallback), "Void(IntPtr, IntPtr)")]
        public void TheNotificationCallbacksHaveTheHeadersSignatures(Type callback, string signature)
        {
            Assert.AreEqual(signature, SignatureOf(callback.GetMethod("Invoke")!));
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
