#nullable enable

#if UNITY_ANDROID || UNITY_EDITOR
namespace JonghyunKim.NativeToolkit.Runtime.Android.Notification
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using JonghyunKim.NativeToolkit.Runtime.Android.Common;

    /// <summary>
    /// The bridge to native-toolkit's C ABI for notifications (<c>Notification.h</c> in libntk.so): the
    /// two structs, the callbacks, the <c>extern</c> declarations, the pure conversions (error values to
    /// 1.x's messages, events to the manager's action IDs, the schedule options), and the calls that
    /// turn a spec read from the JSON into a builder.
    /// </summary>
    /// <remarks>
    /// The design is artifact/android/notification/designs/2026-10-12-android-notification-design-v4.md
    /// (4.1 to 4.5). The pure parts compile in the Editor too, so EditMode tests reach them; the
    /// <c>extern</c>s and the builder calls are compiled into an Android player only.
    /// </remarks>
    internal static class AndroidNotificationCApi
    {
        // ntk_notification_error (Notification.h).
        internal const int ErrorNone = 0;
        internal const int ErrorInvalidParameter = 1;
        internal const int ErrorNotInitialized = 2;
        internal const int ErrorNotSupported = 3;
        internal const int ErrorUnknown = 4;
        internal const int ErrorOutOfMemory = 5;
        internal const int ErrorCanceled = 6;
        internal const int ErrorCanceledBySystem = 7;
        internal const int ErrorNotForeground = 8;
        internal const int ErrorHostStartFailed = 9;
        internal const int ErrorPermissionDenied = 10;
        internal const int ErrorExactAlarmNotAllowed = 11;
        internal const int ErrorResourceNotFound = 12;
        internal const int ErrorStorageFailed = 13;
        internal const int ErrorServiceStartNotAllowed = 14;
        internal const int ErrorSettingsNotOpened = 15;

        // ntk_notification_settings_target / _result.
        internal const int TargetNotifications = 0;
        internal const int TargetAppDetails = 1;
        internal const int TargetExactAlarm = 2;
        internal const int ResultOpened = 0;
        internal const int ResultOpenedFallback = 1;

        // ntk_notification_tap / ntk_notification_event_kind.
        internal const int TapOpenApp = 0;
        internal const int TapEventOnly = 1;
        internal const int KindBodyTap = 0;
        internal const int KindAction = 1;
        internal const int KindDismiss = 2;

        /// <summary>The action ID 1.x gave a tap on the body (<c>AndroidNotificationManager.ActionBodyTap</c>).</summary>
        internal const string BodyTapActionId = "android.unity.notification.ACTION_BODY_TAP";

        /// <summary>The action ID 1.x gave a dismissal (<c>AndroidNotificationManager.ActionNotificationDismissed</c>).</summary>
        internal const string DismissedActionId = "android.unity.notification.ACTION_NOTIFICATION_DISMISSED";

        /// <summary>The settings result: <c>void (*)(void*, ntk_notification_error, uint32_t, ntk_notification_settings_result)</c>.</summary>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void SettingsCallback(IntPtr userData, int error, uint systemCode, int result);

        /// <summary>A tap, action or dismissal: <c>void (*)(void*, ntk_notification_interaction*)</c>. The event is the receiver's to free.</summary>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void InteractionCallback(IntPtr userData, IntPtr interaction);

        /// <summary>A scheduled notification shown: <c>void (*)(void*, ntk_notification_shown*)</c>. The event is the receiver's to free.</summary>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void ShownCallback(IntPtr userData, IntPtr shown);

        /// <summary><c>ntk_notification_schedule_options</c>: 32 bytes.</summary>
        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        internal struct ScheduleOptions
        {
            public uint StructSize;
            public uint Reserved0;
            public long TriggerAtMillis;
            public int Inexact;
            public int NotAllowWhileIdle;
            public int NotPersistAcrossBoot;
            public int AlarmType;
        }

        /// <summary><c>ntk_notification_action</c>: 56 bytes.</summary>
        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        internal struct ActionEntry
        {
            public uint StructSize;
            public uint Reserved0;
            public IntPtr Title;
            public IntPtr ActionId;
            public IntPtr IconName;
            public int LaunchApp;
            public int AllowGeneratedReplies;
            public int SemanticAction;
            public int Contextual;
            public int NoUserInterface;
            public uint Reserved1;
        }

        // ── Errors (4.5) ──────────────────────────────────────────────────────

        internal static string ErrorName(int error) => error switch
        {
            ErrorNone => "NONE",
            ErrorInvalidParameter => "INVALID_PARAMETER",
            ErrorNotInitialized => "NOT_INITIALIZED",
            ErrorNotSupported => "NOT_SUPPORTED",
            ErrorUnknown => "UNKNOWN",
            ErrorOutOfMemory => "OUT_OF_MEMORY",
            ErrorCanceled => "CANCELED",
            ErrorCanceledBySystem => "CANCELED_BY_SYSTEM",
            ErrorNotForeground => "NOT_FOREGROUND",
            ErrorHostStartFailed => "HOST_START_FAILED",
            ErrorPermissionDenied => "PERMISSION_DENIED",
            ErrorExactAlarmNotAllowed => "EXACT_ALARM_NOT_ALLOWED",
            ErrorResourceNotFound => "RESOURCE_NOT_FOUND",
            ErrorStorageFailed => "STORAGE_FAILED",
            ErrorServiceStartNotAllowed => "SERVICE_START_NOT_ALLOWED",
            ErrorSettingsNotOpened => "SETTINGS_NOT_OPENED",
            _ => $"ERROR_{error}",
        };

        /// <summary>
        /// The value that means C# built the request wrongly, logged as an error. INVALID_PARAMETER is
        /// not one: the builder's range checks return it for values the app passed (design 4.5).
        /// </summary>
        internal static bool IsRequestDefect(int error) => error == ErrorNotSupported;

        /// <summary>1.x's message when the call could not be made at all.</summary>
        internal static string NotStartedMessage(string operation) => $"{operation} could not be started.";

        /// <summary>1.x's message for a Progress call without <c>progress</c> (design 4.2).</summary>
        internal static string ProgressRequiredMessage(string operation) =>
            $"Invalid argument for {operation}: progress is required for Progress FGS operations.";

        /// <summary>A failed operation's message (design 4.5).</summary>
        internal static string FailureMessage(string operation, int error) => error switch
        {
            ErrorNotInitialized => NotStartedMessage(operation),
            ErrorInvalidParameter => $"Invalid argument for {operation}: Please verify the input parameters.",
            ErrorNotForeground => $"Failed to {operation}: the app is not in the foreground.",
            ErrorPermissionDenied => $"Failed to {operation}: notifications are not allowed for this app (permission or app notification setting).",
            ErrorResourceNotFound => $"Failed to {operation}: a resource named in the notification was not found.",
            ErrorStorageFailed => $"Failed to {operation}: the schedule store could not be read or written.",
            ErrorServiceStartNotAllowed => $"Failed to {operation}: the system did not allow the foreground service from the background.",
            ErrorSettingsNotOpened => $"No Activity found to handle {operation}. Please check device settings support.",
            _ => $"Failed to {operation}: {ErrorName(error)}",
        };

        // ── Events (4.4) ──────────────────────────────────────────────────────

        /// <summary>
        /// The action ID an interaction reaches the app with: 1.x's constants for a tap on the body and
        /// a dismissal, the button's ID for an action. Null for an unknown kind and for an action
        /// without an ID, which the manager logs and drops (design J-N13).
        /// </summary>
        internal static string? ActionIdOf(int kind, string? actionId) => kind switch
        {
            KindBodyTap => BodyTapActionId,
            KindDismiss => DismissedActionId,
            KindAction => string.IsNullOrEmpty(actionId) ? null : actionId,
            _ => null,
        };

        /// <summary>The tag of a shown event: an empty one is none, as 1.x reported it.</summary>
        internal static string? ShownTag(string? tag) => string.IsNullOrEmpty(tag) ? null : tag;

        // ── Arguments (4.2, 4.3) ──────────────────────────────────────────────

        /// <summary>The schedule options of a spec (design 4.2). <paramref name="inexact"/> overrides <c>exact</c>.</summary>
        internal static ScheduleOptions BuildScheduleOptions(AndroidNotificationScheduleSpec spec, int inexact) => new()
        {
            StructSize = (uint)Marshal.SizeOf<ScheduleOptions>(),
            Reserved0 = 0,
            TriggerAtMillis = spec.TriggerAtMillis,
            Inexact = inexact,
            NotAllowWhileIdle = spec.AllowWhileIdle ? 0 : 1,
            NotPersistAcrossBoot = spec.PersistAcrossBoot ? 0 : 1,
            AlarmType = spec.AlarmType,
        };

        /// <summary>The <c>inexact</c> value a spec asks for: 0 for an exact schedule.</summary>
        internal static int InexactOf(AndroidNotificationScheduleSpec spec) => spec.Exact ? 0 : 1;

        /// <summary>An action button, with its strings in <paramref name="allocated"/>.</summary>
        internal static ActionEntry BuildAction(AndroidNotificationActionSpec spec, List<IntPtr> allocated) => new()
        {
            StructSize = (uint)Marshal.SizeOf<ActionEntry>(),
            Reserved0 = 0,
            Title = AndroidNativeToolkitCApi.AllocUtf8(spec.Title, allocated),
            ActionId = AndroidNativeToolkitCApi.AllocUtf8(spec.ActionId, allocated),
            IconName = AndroidNativeToolkitCApi.AllocUtf8(spec.IconName, allocated),
            LaunchApp = spec.LaunchApp ? 1 : 0,
            AllowGeneratedReplies = spec.AllowGeneratedReplies ? 1 : 0,
            SemanticAction = spec.SemanticAction,
            Contextual = spec.Contextual ? 1 : 0,
            NoUserInterface = spec.ShowsUserInterface ? 0 : 1,
            Reserved1 = 0,
        };

        /// <summary>The tap mode: open the app, or only raise the event (1.x raised it in both cases).</summary>
        internal static int TapOf(AndroidNotificationSpec spec) => spec.LaunchAppOnTap ? TapOpenApp : TapEventOnly;

        /// <summary>A vibration pattern as an <c>int64_t</c> array in unmanaged memory, recorded in <paramref name="allocated"/>.</summary>
        internal static IntPtr AllocLongs(long[] values, List<IntPtr> allocated)
        {
            IntPtr array = AndroidNativeToolkitCApi.Alloc(sizeof(long) * Math.Max(values.Length, 1), allocated);
            Marshal.Copy(values, 0, array, values.Length);
            return array;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>
        /// Creates a content handle from a spec, calling each setter in turn. On success the handle is
        /// the caller's to free; on failure nothing is left to free, and <paramref name="step"/> names
        /// the call that refused (for the log, design 4.5). Strings stay in <paramref name="allocated"/>.
        /// </summary>
        internal static int BuildContent(AndroidNotificationSpec spec, List<IntPtr> allocated, out IntPtr content, out string step)
        {
            content = IntPtr.Zero;
            step = "content_create";
            int error = ntk_notification_content_create(spec.Id, Utf8(spec.Title), Utf8(spec.Message), out IntPtr created);
            if (error != ErrorNone) return error;
            try
            {
                error = SetAll(spec, created, allocated, out step);
            }
            catch
            {
                ntk_notification_content_free(created);
                throw;
            }
            if (error != ErrorNone)
            {
                ntk_notification_content_free(created);
                return error;
            }
            content = created;
            return ErrorNone;

            IntPtr Utf8(string? value) => AndroidNativeToolkitCApi.AllocUtf8(value, allocated);
        }

        private static int SetAll(AndroidNotificationSpec spec, IntPtr content, List<IntPtr> allocated, out string step)
        {
            IntPtr Utf8(string? value) => AndroidNativeToolkitCApi.AllocUtf8(value, allocated);
            int error;

            step = "set_channel";
            error = BuildChannel(spec.Channel, allocated, out IntPtr channel, out string channelStep);
            if (error != ErrorNone)
            {
                step = channelStep;
                return error;
            }
            try
            {
                // The content copies the channel (BLD:162-169), so it is freed right after.
                error = ntk_notification_content_set_channel(content, channel);
            }
            finally
            {
                ntk_notification_channel_free(channel);
            }
            if (error != ErrorNone) return error;

            if (spec.Tag != null && (error = Call("set_tag", ntk_notification_content_set_tag(content, Utf8(spec.Tag)), out step)) != 0) return error;
            if (spec.SmallIconName != null && (error = Call("set_small_icon", ntk_notification_content_set_small_icon(content, Utf8(spec.SmallIconName)), out step)) != 0) return error;
            if (spec.LargeIconName != null && (error = Call("set_large_icon", ntk_notification_content_set_large_icon(content, Utf8(spec.LargeIconName)), out step)) != 0) return error;
            if ((error = Call("set_priority", ntk_notification_content_set_priority(content, spec.Priority), out step)) != 0) return error;
            if ((error = Call("set_auto_cancel", ntk_notification_content_set_auto_cancel(content, Flag(spec.AutoCancel)), out step)) != 0) return error;
            if ((error = Call("set_ongoing", ntk_notification_content_set_ongoing(content, Flag(spec.Ongoing)), out step)) != 0) return error;
            if (spec.SubText != null && (error = Call("set_sub_text", ntk_notification_content_set_sub_text(content, Utf8(spec.SubText)), out step)) != 0) return error;
            if ((error = Call("set_show_timestamp", ntk_notification_content_set_show_timestamp(content, Flag(spec.ShowTimestamp)), out step)) != 0) return error;
            if (spec.TimestampMillis != null && (error = Call("set_timestamp", ntk_notification_content_set_timestamp(content, spec.TimestampMillis.Value), out step)) != 0) return error;
            if (spec.SoundUri != null && (error = Call("set_sound", ntk_notification_content_set_sound(content, Utf8(spec.SoundUri)), out step)) != 0) return error;
            if (spec.Category != null && (error = Call("set_category", ntk_notification_content_set_category(content, Utf8(spec.Category)), out step)) != 0) return error;
            if ((error = Call("set_visibility", ntk_notification_content_set_visibility(content, spec.Visibility), out step)) != 0) return error;
            if (spec.Color != null && (error = Call("set_color", ntk_notification_content_set_color(content, spec.Color.Value), out step)) != 0) return error;
            if (spec.Number != null && (error = Call("set_number", ntk_notification_content_set_number(content, spec.Number.Value), out step)) != 0) return error;
            if (spec.Ticker != null && (error = Call("set_ticker", ntk_notification_content_set_ticker(content, Utf8(spec.Ticker)), out step)) != 0) return error;
            if (spec.GroupKey != null && (error = Call("set_group", ntk_notification_content_set_group(content, Utf8(spec.GroupKey)), out step)) != 0) return error;
            if ((error = Call("set_group_summary", ntk_notification_content_set_group_summary(content, Flag(spec.IsGroupSummary)), out step)) != 0) return error;
            if ((error = Call("set_group_alert_behavior", ntk_notification_content_set_group_alert_behavior(content, spec.GroupAlertBehavior), out step)) != 0) return error;
            if (spec.SortKey != null && (error = Call("set_sort_key", ntk_notification_content_set_sort_key(content, Utf8(spec.SortKey)), out step)) != 0) return error;
            if ((error = Call("set_only_alert_once", ntk_notification_content_set_only_alert_once(content, Flag(spec.OnlyAlertOnce)), out step)) != 0) return error;
            if ((error = Call("set_local_only", ntk_notification_content_set_local_only(content, Flag(spec.LocalOnly)), out step)) != 0) return error;
            if ((error = Call("set_silent", ntk_notification_content_set_silent(content, Flag(spec.Silent)), out step)) != 0) return error;
            if ((error = Call("set_uses_chronometer", ntk_notification_content_set_uses_chronometer(content, Flag(spec.UsesChronometer)), out step)) != 0) return error;
            if (spec.TimeoutAfterMillis != null && (error = Call("set_timeout_after", ntk_notification_content_set_timeout_after(content, spec.TimeoutAfterMillis.Value), out step)) != 0) return error;
            if (spec.HasProgress && (error = Call("set_progress", ntk_notification_content_set_progress(content, spec.ProgressMax, spec.ProgressCurrent, Flag(spec.ProgressIndeterminate)), out step)) != 0) return error;
            if ((error = SetStyle(spec.Style, content, allocated, out step)) != 0) return error;
            if ((error = Call("set_tap", ntk_notification_content_set_tap(content, TapOf(spec)), out step)) != 0) return error;
            foreach (KeyValuePair<string, string> entry in spec.Data)
            {
                if ((error = Call("add_data", ntk_notification_content_add_data(content, Utf8(entry.Key), Utf8(entry.Value)), out step)) != 0) return error;
            }
            // 1.x always attached the dismissal (design 2.3).
            if ((error = Call("set_dismiss_event", ntk_notification_content_set_dismiss_event(content, 1), out step)) != 0) return error;
            if ((error = Call("set_full_screen", ntk_notification_content_set_full_screen(content, Flag(spec.FullScreenIntent)), out step)) != 0) return error;
            foreach (AndroidNotificationActionSpec action in spec.Actions)
            {
                ActionEntry entry = BuildAction(action, allocated);
                if ((error = Call("add_action", ntk_notification_content_add_action(content, ref entry), out step)) != 0) return error;
            }
            step = "";
            return ErrorNone;
        }

        private static int SetStyle(AndroidNotificationStyleSpec style, IntPtr content, List<IntPtr> allocated, out string step)
        {
            IntPtr Utf8(string? value) => AndroidNativeToolkitCApi.AllocUtf8(value, allocated);
            int error;
            step = "";
            switch (style.Kind)
            {
                case AndroidNotificationStyleKind.BigText:
                    return Call("set_style_big_text", ntk_notification_content_set_style_big_text(content, Utf8(style.BigText), Utf8(style.SummaryText), Utf8(style.BigContentTitle)), out step);
                case AndroidNotificationStyleKind.Inbox:
                    string[] lines = style.Lines.ToArray();
                    return Call("set_style_inbox", ntk_notification_content_set_style_inbox(content, AndroidNativeToolkitCApi.AllocStringArray(lines, allocated),
                        new UIntPtr((uint)lines.Length), Utf8(style.SummaryText), Utf8(style.BigContentTitle)), out step);
                case AndroidNotificationStyleKind.BigPicture:
                    return Call("set_style_big_picture", ntk_notification_content_set_style_big_picture(content, Utf8(style.PictureName), Utf8(style.PictureUri),
                        Utf8(style.SummaryText), Utf8(style.BigContentTitle), Utf8(style.LargeIconName), Flag(style.HideExpandedLargeIcon)), out step);
                case AndroidNotificationStyleKind.Messaging:
                    if ((error = Call("set_style_messaging", ntk_notification_content_set_style_messaging(content, Utf8(style.UserDisplayName),
                            Utf8(style.ConversationTitle), style.GroupConversation), out step)) != 0) return error;
                    foreach (AndroidNotificationMessageSpec message in style.Messages)
                    {
                        if ((error = Call("add_message", ntk_notification_content_add_message(content, Utf8(message.Text), message.TimestampMillis, Utf8(message.SenderName)), out step)) != 0) return error;
                    }
                    return ErrorNone;
                case AndroidNotificationStyleKind.CustomView:
                    if ((error = Call("set_style_custom_view", ntk_notification_content_set_style_custom_view(content, Utf8(style.Layout), Utf8(style.BigLayout)), out step)) != 0) return error;
                    foreach (KeyValuePair<string, string> click in style.ViewClicks)
                    {
                        if ((error = Call("add_view_click", ntk_notification_content_add_view_click(content, Utf8(click.Key), Utf8(click.Value)), out step)) != 0) return error;
                    }
                    return ErrorNone;
                default:
                    return ErrorNone;
            }
        }

        /// <summary>
        /// Creates a channel handle from a spec. On success the handle is the caller's to free; on
        /// failure nothing is left to free, and <paramref name="step"/> names the call that refused.
        /// </summary>
        internal static int BuildChannel(AndroidNotificationChannelSpec spec, List<IntPtr> allocated, out IntPtr channel, out string step)
        {
            channel = IntPtr.Zero;
            step = "channel_create";
            int error = ntk_notification_channel_create(AndroidNativeToolkitCApi.AllocUtf8(spec.Id, allocated),
                AndroidNativeToolkitCApi.AllocUtf8(spec.Name, allocated), spec.Importance, out IntPtr created);
            if (error != ErrorNone) return error;
            try
            {
                error = SetChannel(spec, created, allocated, out step);
            }
            catch
            {
                ntk_notification_channel_free(created);
                throw;
            }
            if (error != ErrorNone)
            {
                ntk_notification_channel_free(created);
                return error;
            }
            channel = created;
            return ErrorNone;
        }

        private static int SetChannel(AndroidNotificationChannelSpec spec, IntPtr channel, List<IntPtr> allocated, out string step)
        {
            IntPtr Utf8(string? value) => AndroidNativeToolkitCApi.AllocUtf8(value, allocated);
            int error;
            step = "";
            if (spec.Description != null && (error = Call("channel_set_description", ntk_notification_channel_set_description(channel, Utf8(spec.Description)), out step)) != 0) return error;
            if ((error = Call("channel_set_show_badge", ntk_notification_channel_set_show_badge(channel, Flag(spec.ShowBadge)), out step)) != 0) return error;
            if ((error = Call("channel_set_enable_lights", ntk_notification_channel_set_enable_lights(channel, Flag(spec.EnableLights)), out step)) != 0) return error;
            if (spec.LightColor != null && (error = Call("channel_set_light_color", ntk_notification_channel_set_light_color(channel, spec.LightColor.Value), out step)) != 0) return error;
            if ((error = Call("channel_set_enable_vibration", ntk_notification_channel_set_enable_vibration(channel, Flag(spec.EnableVibration)), out step)) != 0) return error;
            if (spec.VibrationPattern != null && (error = Call("channel_set_vibration_pattern", ntk_notification_channel_set_vibration_pattern(channel,
                    AllocLongs(spec.VibrationPattern, allocated), new UIntPtr((uint)spec.VibrationPattern.Length)), out step)) != 0) return error;
            if (spec.SoundUri != null && (error = Call("channel_set_sound", ntk_notification_channel_set_sound(channel, Utf8(spec.SoundUri)), out step)) != 0) return error;
            if ((error = Call("channel_set_lockscreen_visibility", ntk_notification_channel_set_lockscreen_visibility(channel, spec.LockscreenVisibility), out step)) != 0) return error;
            if (spec.GroupId != null && (error = Call("channel_set_group", ntk_notification_channel_set_group(channel, Utf8(spec.GroupId), Utf8(spec.GroupName)), out step)) != 0) return error;
            step = "";
            return ErrorNone;
        }

        private static int Call(string name, int error, out string step)
        {
            step = name;
            return error;
        }

        private static int Flag(bool value) => value ? 1 : 0;

        /// <summary>What an interaction event carries. Reads; does not free.</summary>
        internal static (int Kind, int NotificationId, string? ActionId, List<KeyValuePair<string, string>> Data) ReadInteraction(IntPtr interaction)
        {
            int kind = ntk_notification_interaction_kind(interaction);
            int id = ntk_notification_interaction_notification_id(interaction);
            string? actionId = AndroidNativeToolkitCApi.ReadUtf8OrNull(ntk_notification_interaction_action_id(interaction, out UIntPtr actionSize), actionSize);
            var data = new List<KeyValuePair<string, string>>();
            ulong count = ntk_notification_interaction_data_count(interaction).ToUInt64();
            for (ulong i = 0; i < count; i++)
            {
                var index = new UIntPtr(i);
                string key = AndroidNativeToolkitCApi.ReadUtf8OrNull(ntk_notification_interaction_data_key_at(interaction, index, out UIntPtr keySize), keySize) ?? "";
                string value = AndroidNativeToolkitCApi.ReadUtf8OrNull(ntk_notification_interaction_data_value_at(interaction, index, out UIntPtr valueSize), valueSize) ?? "";
                data.Add(new KeyValuePair<string, string>(key, value));
            }
            return (kind, id, actionId, data);
        }

        /// <summary>What a shown event carries. Reads; does not free.</summary>
        internal static (int NotificationId, string? Tag, string? ChannelId) ReadShown(IntPtr shown) => (
            ntk_notification_shown_notification_id(shown),
            AndroidNativeToolkitCApi.ReadUtf8OrNull(ntk_notification_shown_tag(shown, out UIntPtr tagSize), tagSize),
            AndroidNativeToolkitCApi.ReadUtf8OrNull(ntk_notification_shown_channel_id(shown, out UIntPtr channelSize), channelSize));

        // ── Operations ────────────────────────────────────────────────────────

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_show(IntPtr content);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_update(IntPtr content);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_remove(int id, IntPtr tag);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_remove_all();

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_create_channel(IntPtr channel);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_delete_channel(IntPtr channelId);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_schedule(IntPtr content, ref ScheduleOptions schedule);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_cancel_scheduled(int id, IntPtr tag);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_cancel_all_scheduled();

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_start_progress(IntPtr content);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_update_progress(IntPtr content);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_complete_progress(IntPtr content);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_stop_progress();

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_has_permission(out int value);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_are_enabled(out int value);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_is_scheduled(int id, IntPtr tag, out int value);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_can_schedule_exact_alarms(out int value);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_open_settings_async(int target, IntPtr callback, IntPtr userData, IntPtr release);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_add_interaction_listener(IntPtr callback, IntPtr userData, IntPtr release, out IntPtr listener);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_add_shown_listener(IntPtr callback, IntPtr userData, IntPtr release, out IntPtr listener);

        // ── Content builder ───────────────────────────────────────────────────

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_content_create(int id, IntPtr title, IntPtr message, out IntPtr content);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ntk_notification_content_free(IntPtr content);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_content_set_tag(IntPtr content, IntPtr value);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_content_set_channel(IntPtr content, IntPtr channel);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_content_set_small_icon(IntPtr content, IntPtr name);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_content_set_large_icon(IntPtr content, IntPtr name);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_content_set_priority(IntPtr content, int value);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_content_set_auto_cancel(IntPtr content, int value);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_content_set_ongoing(IntPtr content, int value);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_content_set_sub_text(IntPtr content, IntPtr value);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_content_set_show_timestamp(IntPtr content, int value);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_content_set_timestamp(IntPtr content, long unixMs);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_content_set_sound(IntPtr content, IntPtr uri);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_content_set_category(IntPtr content, IntPtr value);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_content_set_visibility(IntPtr content, int value);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_content_set_color(IntPtr content, uint argb);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_content_set_number(IntPtr content, int value);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_content_set_ticker(IntPtr content, IntPtr value);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_content_set_group(IntPtr content, IntPtr key);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_content_set_group_summary(IntPtr content, int value);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_content_set_group_alert_behavior(IntPtr content, int value);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_content_set_sort_key(IntPtr content, IntPtr value);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_content_set_only_alert_once(IntPtr content, int value);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_content_set_local_only(IntPtr content, int value);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_content_set_silent(IntPtr content, int value);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_content_set_uses_chronometer(IntPtr content, int value);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_content_set_timeout_after(IntPtr content, long millis);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_content_set_progress(IntPtr content, int max, int current, int indeterminate);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_content_set_style_big_text(IntPtr content, IntPtr bigText, IntPtr summaryText, IntPtr bigContentTitle);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_content_set_style_inbox(IntPtr content, IntPtr lines, UIntPtr lineCount, IntPtr summaryText, IntPtr bigContentTitle);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_content_set_style_big_picture(IntPtr content, IntPtr pictureName, IntPtr pictureUri, IntPtr summaryText,
            IntPtr bigContentTitle, IntPtr largeIconName, int hideExpandedLargeIcon);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_content_set_style_messaging(IntPtr content, IntPtr userDisplayName, IntPtr conversationTitle, int groupConversation);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_content_add_message(IntPtr content, IntPtr text, long timestampMs, IntPtr senderName);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_content_set_style_custom_view(IntPtr content, IntPtr layoutName, IntPtr bigLayoutName);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_content_add_view_click(IntPtr content, IntPtr viewIdName, IntPtr actionId);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_content_set_tap(IntPtr content, int mode);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_content_add_data(IntPtr content, IntPtr key, IntPtr value);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_content_set_dismiss_event(IntPtr content, int enabled);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_content_set_full_screen(IntPtr content, int enabled);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_content_add_action(IntPtr content, ref ActionEntry action);

        // ── Channel builder ───────────────────────────────────────────────────

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_channel_create(IntPtr id, IntPtr name, int importance, out IntPtr channel);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ntk_notification_channel_free(IntPtr channel);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_channel_set_description(IntPtr channel, IntPtr value);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_channel_set_show_badge(IntPtr channel, int value);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_channel_set_enable_lights(IntPtr channel, int value);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_channel_set_light_color(IntPtr channel, uint argb);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_channel_set_enable_vibration(IntPtr channel, int value);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_channel_set_vibration_pattern(IntPtr channel, IntPtr pattern, UIntPtr count);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_channel_set_sound(IntPtr channel, IntPtr uri);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_channel_set_lockscreen_visibility(IntPtr channel, int value);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_channel_set_group(IntPtr channel, IntPtr groupId, IntPtr groupName);

        // ── Event readers ─────────────────────────────────────────────────────

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_interaction_kind(IntPtr interaction);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_interaction_notification_id(IntPtr interaction);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr ntk_notification_interaction_action_id(IntPtr interaction, out UIntPtr size);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern UIntPtr ntk_notification_interaction_data_count(IntPtr interaction);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr ntk_notification_interaction_data_key_at(IntPtr interaction, UIntPtr index, out UIntPtr size);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr ntk_notification_interaction_data_value_at(IntPtr interaction, UIntPtr index, out UIntPtr size);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ntk_notification_interaction_free(IntPtr interaction);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_shown_notification_id(IntPtr shown);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr ntk_notification_shown_tag(IntPtr shown, out UIntPtr size);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr ntk_notification_shown_channel_id(IntPtr shown, out UIntPtr size);

        [DllImport(AndroidNativeToolkitCApi.LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ntk_notification_shown_free(IntPtr shown);
#endif
    }
}
#endif
