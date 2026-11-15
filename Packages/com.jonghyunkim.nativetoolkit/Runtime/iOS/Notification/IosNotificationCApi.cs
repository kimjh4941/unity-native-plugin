#nullable enable

#if UNITY_IOS || UNITY_EDITOR
namespace JonghyunKim.NativeToolkit.Runtime.iOS.Notification
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Runtime.InteropServices;
    using System.Text;
    using JonghyunKim.NativeToolkit.Runtime.iOS.Clipboard;
    using JonghyunKim.NativeToolkit.Runtime.iOS.Common;

    /// <summary>
    /// The bridge to native-toolkit's C ABI for iOS notifications (<c>Notification.h</c> in IosLibraryCApi): the
    /// structs, the <c>extern</c> declarations, and the pure conversions between <see cref="IosNotificationManager"/>'s
    /// JSON-based public API and the C ABI (artifact/ios/notification, C ABI design).
    /// </summary>
    /// <remarks>
    /// The declarations compile in the Editor too, so the EditMode check compares them with the headers; they are
    /// called on an iOS player only. The JSON is read as native-toolkit 1.13.0's Unity bridge read it (design D-4),
    /// and values 1.x accepted but the C ABI rejects are adjusted where 1.x gave the same result (D-5 (a)).
    /// </remarks>
    internal static class IosNotificationCApi
    {
        // ntk_notification_error (Notification.h).
        internal const int ErrorNone = 0;
        internal const int ErrorInvalidParameter = 1;
        internal const int ErrorNotInitialized = 2;
        internal const int ErrorNotSupported = 3;
        internal const int ErrorUnknown = 4;
        internal const int ErrorOutOfMemory = 5;
        internal const int ErrorNotForeground = 8;
        internal const int ErrorPermissionDenied = 10;
        internal const int ErrorSettingsNotOpened = 15;
        internal const int ErrorWrongThread = 16;
        internal const int ErrorAttachmentLoadFailed = 17;
        internal const int ErrorAddRequestFailed = 18;
        internal const int ErrorSetBadgeCountFailed = 19;
        internal const int ErrorAuthorizationFailed = 20;

        // ntk_notification_sound_kind, interruption level, trigger kind, event kind.
        internal const int SoundDefault = 0;
        internal const int SoundDefaultCritical = 1;
        internal const int SoundNamed = 2;
        internal const int InterruptionPassive = 1;
        internal const int InterruptionActive = 2;
        internal const int InterruptionTimeSensitive = 3;
        internal const int InterruptionCritical = 4;
        internal const int TriggerTimeInterval = 0;
        internal const int TriggerCalendar = 1;
        internal const int TriggerLocation = 2;
        internal const uint CalendarYear = 1u << 0;
        internal const uint CalendarMonth = 1u << 1;
        internal const uint CalendarDay = 1u << 2;
        internal const uint CalendarHour = 1u << 3;
        internal const uint CalendarMinute = 1u << 4;
        internal const uint CalendarSecond = 1u << 5;
        internal const uint ActionAuthenticationRequired = 1u << 0;
        internal const uint ActionDestructive = 1u << 1;
        internal const uint ActionForeground = 1u << 2;
        internal const uint CategoryCustomDismissAction = 1u << 0;
        internal const uint CategoryAllowInCarPlay = 1u << 1;
        internal const uint CategoryHiddenPreviewsShowTitle = 1u << 2;
        internal const uint CategoryAllowAnnouncement = 1u << 3;
        internal const int EventBodyTap = 0;
        internal const int EventAction = 1;
        internal const int EventDismiss = 2;
        internal const int EventTextInput = 3;

        // The callbacks of Notification.h (all on the main thread).
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void DoneCallback(IntPtr userData, int error, uint systemCode);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void PermissionCallback(IntPtr userData, int error, uint systemCode, int granted);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void StatusCallback(IntPtr userData, int error, uint systemCode, int status);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void ListCallback(IntPtr userData, int error, uint systemCode, IntPtr list);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void InteractionCallback(IntPtr userData, IntPtr interaction);

        /// <summary><c>ntk_notification_trigger</c> (pack 8, 96 bytes).</summary>
        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        internal struct Trigger
        {
            public uint StructSize;
            public uint Reserved0;
            public int Kind;
            public int Repeats;
            public double IntervalSeconds;
            public uint CalendarFields;
            public int Year;
            public int Month;
            public int Day;
            public int Hour;
            public int Minute;
            public int Second;
            public uint Reserved1;
            public IntPtr LocationIdentifier;
            public double Latitude;
            public double Longitude;
            public double Radius;
            public int NotNotifyOnEntry;
            public int NotifyOnExit;
        }

        /// <summary><c>ntk_notification_action</c> (pack 8, 40 bytes).</summary>
        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        internal struct NotificationAction
        {
            public uint StructSize;
            public uint Reserved0;
            public IntPtr Identifier;
            public IntPtr Title;
            public IntPtr SfSymbolName;
            public uint Options;
            public uint Reserved1;
        }

        /// <summary><c>ntk_notification_text_input_action</c> (pack 8, 40 bytes).</summary>
        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        internal struct TextInputAction
        {
            public uint StructSize;
            public uint Reserved0;
            public IntPtr Identifier;
            public IntPtr Title;
            public IntPtr ButtonTitle;
            public IntPtr Placeholder;
        }

        /// <summary><c>ntk_notification_category</c> (pack 8, 56 bytes).</summary>
        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        internal struct Category
        {
            public uint StructSize;
            public uint Reserved0;
            public uint Options;
            public uint Reserved1;
            public IntPtr Identifier;
            public IntPtr Actions;
            public UIntPtr ActionCount;
            public IntPtr TextInputActions;
            public UIntPtr TextInputActionCount;
        }

        // ── The C ABI (only the functions the manager uses). Struct pointers and handles are IntPtr. ──

        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern int ntk_notification_install();
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern int ntk_notification_show_async(IntPtr content, IntPtr trigger, IntPtr callback, IntPtr userData, IntPtr release);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern int ntk_notification_update_async(IntPtr identifier, IntPtr content, IntPtr trigger, IntPtr callback, IntPtr userData, IntPtr release);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern int ntk_notification_schedule_async(IntPtr identifier, IntPtr content, IntPtr trigger, IntPtr callback, IntPtr userData, IntPtr release);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern int ntk_notification_remove_async(IntPtr identifier, IntPtr callback, IntPtr userData, IntPtr release);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern int ntk_notification_remove_all_async(IntPtr callback, IntPtr userData, IntPtr release);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern int ntk_notification_cancel_scheduled_async(IntPtr identifier, IntPtr callback, IntPtr userData, IntPtr release);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern int ntk_notification_cancel_all_scheduled_async(IntPtr callback, IntPtr userData, IntPtr release);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern int ntk_notification_get_scheduled_async(IntPtr callback, IntPtr userData, IntPtr release);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern int ntk_notification_get_delivered_async(IntPtr callback, IntPtr userData, IntPtr release);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern int ntk_notification_request_permission_async(uint options, IntPtr callback, IntPtr userData, IntPtr release);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern int ntk_notification_get_authorization_status_async(IntPtr callback, IntPtr userData, IntPtr release);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern int ntk_notification_has_permission_async(IntPtr callback, IntPtr userData, IntPtr release);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern int ntk_notification_open_settings_async(IntPtr callback, IntPtr userData, IntPtr release);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern int ntk_notification_set_badge_count_async(int count, IntPtr callback, IntPtr userData, IntPtr release);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern int ntk_notification_register_category_async(IntPtr category, IntPtr callback, IntPtr userData, IntPtr release);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern int ntk_notification_remove_category_async(IntPtr identifier, IntPtr callback, IntPtr userData, IntPtr release);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern int ntk_notification_add_interaction_listener(IntPtr callback, IntPtr userData, IntPtr release, out IntPtr listener);

        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern int ntk_notification_content_create(IntPtr id, IntPtr title, out IntPtr content);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern void ntk_notification_content_free(IntPtr content);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern int ntk_notification_content_set_subtitle(IntPtr content, IntPtr subtitle);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern int ntk_notification_content_set_body(IntPtr content, IntPtr body);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern int ntk_notification_content_set_badge(IntPtr content, int badge);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern int ntk_notification_content_set_sound(IntPtr content, int kind, IntPtr name);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern int ntk_notification_content_set_category(IntPtr content, IntPtr identifier);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern int ntk_notification_content_set_interruption_level(IntPtr content, int level);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern int ntk_notification_content_set_thread(IntPtr content, IntPtr identifier);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern int ntk_notification_content_set_target_content(IntPtr content, IntPtr identifier);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern int ntk_notification_content_set_relevance_score(IntPtr content, double score);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern int ntk_notification_content_set_filter_criteria(IntPtr content, IntPtr criteria);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern int ntk_notification_content_set_user_info_json(IntPtr content, IntPtr json);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern int ntk_notification_content_add_attachment(IntPtr content, IntPtr identifier, IntPtr fileUrl);

        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern int ntk_notification_interaction_kind(IntPtr interaction);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern IntPtr ntk_notification_interaction_notification_id(IntPtr interaction, out UIntPtr size);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern IntPtr ntk_notification_interaction_action_id(IntPtr interaction, out UIntPtr size);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern IntPtr ntk_notification_interaction_user_text(IntPtr interaction, out UIntPtr size);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern IntPtr ntk_notification_interaction_user_info_json(IntPtr interaction, out UIntPtr size);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern void ntk_notification_interaction_free(IntPtr interaction);

        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern UIntPtr ntk_notification_list_count(IntPtr list);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern IntPtr ntk_notification_list_identifier_at(IntPtr list, UIntPtr index, out UIntPtr size);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern IntPtr ntk_notification_list_title_at(IntPtr list, UIntPtr index, out UIntPtr size);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern IntPtr ntk_notification_list_subtitle_at(IntPtr list, UIntPtr index, out UIntPtr size);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern IntPtr ntk_notification_list_body_at(IntPtr list, UIntPtr index, out UIntPtr size);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern IntPtr ntk_notification_list_category_at(IntPtr list, UIntPtr index, out UIntPtr size);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern long ntk_notification_list_date_millis_at(IntPtr list, UIntPtr index);
        [DllImport(IosNativeToolkitCApi.LibraryName)] internal static extern void ntk_notification_list_free(IntPtr list);

        // ── Messages (design D-4, D-6) ─────────────────────────────────────

        internal const string ContentParseFailed = "Failed to parse notification content JSON";
        internal const string ContentOrTriggerParseFailed = "Failed to parse notification content or trigger JSON";
        internal const string CategoryParseFailed = "Failed to parse category JSON";
        internal const string PermissionDenied = "Notification permission was denied.";

        /// <summary>
        /// The message of a C ABI error: the 2.0.0 library's fixed English text (IosLibrary's
        /// <c>NotificationError.errorDescription</c>), as 1.x's generic, localized text cannot be rebuilt (D-6).
        /// </summary>
        internal static string ErrorMessage(int error) => error switch
        {
            ErrorPermissionDenied => PermissionDenied,
            ErrorAttachmentLoadFailed => "Failed to create a notification attachment.",
            ErrorAddRequestFailed => "Failed to add the notification request.",
            ErrorSetBadgeCountFailed => "Failed to set the badge count.",
            ErrorAuthorizationFailed => "Failed to request notification permission.",
            ErrorNotForeground => "The app is not in the foreground.",
            ErrorSettingsNotOpened => "Failed to open the notification settings.",
            ErrorNotInitialized => "The library's notification delegate is not installed.",
            ErrorInvalidParameter => "The notification request is invalid.",
            _ => "An unknown error occurred.",
        };

        /// <summary>The warning for a displayed string cut at U+0000 (design D-16). It names no value.</summary>
        internal const string NulValueWarning =
            "A notification string contains U+0000; the native library receives it up to the U+0000.";

        /// <summary>One element of a pending or delivered list, as read from the handle.</summary>
        internal readonly struct ListItem
        {
            internal ListItem(string identifier, string title, string? subtitle, string? body, string category, long dateMillis)
            {
                Identifier = identifier;
                Title = title;
                Subtitle = subtitle;
                Body = body;
                Category = category;
                DateMillis = dateMillis;
            }

            internal string Identifier { get; }
            internal string Title { get; }
            internal string? Subtitle { get; }
            internal string? Body { get; }
            internal string Category { get; }
            internal long DateMillis { get; }
        }

        /// <summary>
        /// The list JSON of 1.x (design D-9): <c>identifier</c>, <c>title</c>, <c>subtitle</c> and <c>body</c> when
        /// not empty, <c>categoryIdentifier</c>, and for delivered ones <c>date</c> as UTC <c>yyyy-MM-ddTHH:mm:ssZ</c>
        /// (the milliseconds dropped).
        /// </summary>
        internal static string ListJson(IReadOnlyList<ListItem> items, bool delivered)
        {
            var builder = new StringBuilder("[");
            for (int i = 0; i < items.Count; i++)
            {
                ListItem item = items[i];
                if (i > 0) builder.Append(',');
                builder.Append("{\"identifier\":");
                IosNotificationRequests.AppendString(builder, item.Identifier);
                builder.Append(",\"title\":");
                IosNotificationRequests.AppendString(builder, item.Title);
                if (!string.IsNullOrEmpty(item.Subtitle))
                {
                    builder.Append(",\"subtitle\":");
                    IosNotificationRequests.AppendString(builder, item.Subtitle!);
                }
                if (!string.IsNullOrEmpty(item.Body))
                {
                    builder.Append(",\"body\":");
                    IosNotificationRequests.AppendString(builder, item.Body!);
                }
                builder.Append(",\"categoryIdentifier\":");
                IosNotificationRequests.AppendString(builder, item.Category);
                if (delivered)
                {
                    builder.Append(",\"date\":");
                    IosNotificationRequests.AppendString(builder, IsoSeconds(item.DateMillis));
                }
                builder.Append('}');
            }
            return builder.Append(']').ToString();
        }

        /// <summary>Unix milliseconds as UTC <c>yyyy-MM-ddTHH:mm:ssZ</c>, toward the past to the second.</summary>
        internal static string IsoSeconds(long millis)
        {
            long seconds = millis / 1000;
            if (millis < 0 && millis % 1000 != 0) seconds--;
            return DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        }

        /// <summary>The 1.x status name of an <c>ntk_notification_authorization_status</c> (unknown values: "unknown").</summary>
        internal static string StatusName(int status) => status switch
        {
            0 => "notDetermined",
            1 => "denied",
            2 => "authorized",
            3 => "provisional",
            4 => "ephemeral",
            _ => "unknown",
        };
    }
}
#endif
