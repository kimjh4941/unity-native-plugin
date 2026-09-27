#nullable enable

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
namespace JonghyunKim.NativeToolkit.Runtime.Windows.Notification
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Runtime.InteropServices;
    using System.Text;
    using JonghyunKim.NativeToolkit.Runtime.Windows.Common;
    using UnityEngine;

    /// <summary>
    /// The bridge between <see cref="WindowsNotificationManager"/> and native-toolkit's C ABI
    /// (<c>NativeToolkitC/Notification.h</c>, version 2.0.0).
    /// </summary>
    /// <remarks>
    /// Two halves, as agent-rules/coding-rules/common.md ("Unity Bridge パターン > Windows") lays
    /// out. The conversions are pure functions compiled everywhere, so EditMode tests reach them:
    /// the JSON payload to the builder calls, the list to the JSON <c>GetAllNotifications</c>
    /// reports, the order of the checks, and the codes. The <c>DllImport</c>s are compiled only
    /// into a Windows player. Design: artifact/features/notification/designs/2026-09-27-windows-notification-design-v8.md.
    /// </remarks>
    internal static class WindowsNotificationCApi
    {
        private const string LogTag = nameof(WindowsNotificationCApi);

        // ntk_notification_error. The values and meanings are 1.x's errorCode 1 to 8.
        internal const int ErrorNone = 0;
        internal const int ErrorNotInitialized = 1;
        internal const int ErrorDisabled = 2;
        internal const int ErrorInvalidPayload = 3;
        internal const int ErrorProgressNotFound = 4;
        internal const int ErrorHresultFailure = 5;
        internal const int ErrorBadgeFailed = 6;
        internal const int ErrorInvalidParameter = 7;
        internal const int ErrorNotSupported = 8;

        /// <summary>
        /// The native library could not be used: the DLL is missing, lacks a function, is not a
        /// loadable image, or reports another major version. Only <c>Initialize</c> reports it.
        /// </summary>
        internal const int NativeUnavailable = -4;

        /// <summary>The Windows App SDK runtime <c>Initialize</c> loads, as 1.x did (1.7).</summary>
        internal const uint RuntimeVersion = 0x00010007;

        /// <summary>The lowest glyph value <c>SetBadge</c> takes (<see cref="WindowsBadgeValue.Away"/>).</summary>
        internal const int LowestBadgeValue = -6;

        /// <summary>
        /// The widest <c>timestamp</c> the native library takes, in seconds: its limit is
        /// 922,337,203,685,477 ms either side.
        /// </summary>
        internal const long MaxTimestampSeconds = 922_337_203_685;

        // GetCurrentPackageFullName with a zero length and no buffer.
        internal const int AppModelErrorNoPackage = 15700;
        internal const int ErrorInsufficientBuffer = 122;

        /// <summary><c>ntk_notification_manager_options</c>, 56 bytes.</summary>
        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        internal struct ManagerOptions
        {
            public uint StructSize;
            public uint Reserved0;
            public IntPtr OnInvoked;
            public IntPtr UserData;
            public IntPtr Release;
            public int IsUnpackaged;
            public uint Reserved1;
            public IntPtr DisplayName;
            public IntPtr IconUri;
        }

        /// <summary><c>ntk_notification_progress_update</c>, 56 bytes.</summary>
        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        internal struct ProgressUpdate
        {
            public uint StructSize;
            public uint Reserved0;
            public IntPtr Tag;
            public IntPtr Group;
            public double Value;
            public IntPtr ValueString;
            public IntPtr Status;
            public uint SequenceNumber;
            public uint Reserved1;
        }

        /// <summary>
        /// The manager options: the activation handler, no <c>user_data</c> and no <c>release</c>
        /// (the handler is static, design v8 J-7), and the unpackaged registration.
        /// </summary>
        internal static ManagerOptions BuildManagerOptions(IntPtr onInvoked, bool isPackaged, IntPtr displayName, IntPtr iconUri) => new ManagerOptions
        {
            StructSize = (uint)Marshal.SizeOf<ManagerOptions>(),
            OnInvoked = onInvoked,
            IsUnpackaged = isPackaged ? 0 : 1,
            DisplayName = displayName,
            IconUri = iconUri,
        };

        internal static ProgressUpdate BuildProgressUpdate(IntPtr tag, IntPtr group, double value, IntPtr valueString, IntPtr status, uint sequenceNumber) => new ProgressUpdate
        {
            StructSize = (uint)Marshal.SizeOf<ProgressUpdate>(),
            Tag = tag,
            Group = group,
            Value = value,
            ValueString = valueString,
            Status = status,
            SequenceNumber = sequenceNumber,
        };

        // ── Checks made before the native call (pure) ────────────────────────────

        /// <summary>
        /// Whether a show or schedule goes on to read its JSON, in 1.x's order (design v8 J-11):
        /// 1 without a manager, 2 when the setting could not be read or is not enabled, else 0.
        /// </summary>
        internal static int ShowGate(bool hasManager, int settingError, int setting)
        {
            if (!hasManager) return ErrorNotInitialized;
            if (settingError != ErrorNone || setting != (int)WindowsNotificationSetting.Enabled) return ErrorDisabled;
            return ErrorNone;
        }

        /// <summary>7 for a badge value below the lowest glyph, which 1.x refused before looking for a manager; else 0.</summary>
        internal static int CheckBadgeValue(int value) => value < LowestBadgeValue ? ErrorInvalidParameter : ErrorNone;

        /// <summary>Whether this process has package identity, from what <c>GetCurrentPackageFullName</c> returned.</summary>
        internal enum PackageIdentity
        {
            /// <summary>No package: <c>Initialize</c> loads the Windows App SDK runtime.</summary>
            Unpackaged,

            /// <summary>Packaged: the runtime comes with the package and is not loaded.</summary>
            Packaged,

            /// <summary>Anything else: <c>Initialize</c> reports 5.</summary>
            Unknown,
        }

        /// <summary>
        /// Classifies the result of <c>GetCurrentPackageFullName(&amp;length = 0, NULL)</c>:
        /// <c>APPMODEL_ERROR_NO_PACKAGE</c> (15700) is unpackaged, <c>ERROR_INSUFFICIENT_BUFFER</c>
        /// (122) is packaged (design v8 J-8).
        /// </summary>
        internal static PackageIdentity ClassifyPackage(int getCurrentPackageFullNameResult) => getCurrentPackageFullNameResult switch
        {
            AppModelErrorNoPackage => PackageIdentity.Unpackaged,
            ErrorInsufficientBuffer => PackageIdentity.Packaged,
            _ => PackageIdentity.Unknown,
        };

        // ── Results (pure) ───────────────────────────────────────────────────────

        internal static WindowsNotificationResult ToResult(string operation, int code) =>
            code == ErrorNone ? WindowsNotificationResult.Success(operation) : WindowsNotificationResult.Failure(operation, code);

        /// <summary>The code for an exception thrown while calling the native library: -4 when it could not be loaded or bound, else 5.</summary>
        internal static int FromException(Exception exception) =>
            WindowsNativeToolkitCApi.IsNativeUnavailable(exception) ? NativeUnavailable : ErrorHresultFailure;

        /// <summary>The setting for what <c>ntk_notification_get_setting</c> returned; <see cref="WindowsNotificationSetting.Unknown"/> on failure or an undefined value.</summary>
        internal static WindowsNotificationSetting SettingFrom(int error, int raw) =>
            error == ErrorNone && raw >= 0 && Enum.IsDefined(typeof(WindowsNotificationSetting), raw)
                ? (WindowsNotificationSetting)raw
                : WindowsNotificationSetting.Unknown;

        /// <summary>
        /// Raises <c>NotificationInvoked</c> on the main thread, as the dispatcher runs it. A
        /// subscriber's exception is logged and goes no further, as in 1.x: let out, it would leave
        /// the dispatcher's <c>Update</c> and hold up the rest of the frame.
        /// </summary>
        internal static void DeliverInvoked(Action<string>? handler, string arguments)
        {
            try
            {
                handler?.Invoke(arguments);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{LogTag}][{nameof(DeliverInvoked)}] {ex.Message}");
            }
        }

        // ── GetAllNotifications (pure) ───────────────────────────────────────────

        /// <summary>One entry of <c>ntk_notification_list</c>.</summary>
        internal readonly struct ListedNotification
        {
            internal ListedNotification(uint id, string tag, string group)
            {
                Id = id;
                Tag = tag;
                Group = group;
            }

            internal uint Id { get; }
            internal string Tag { get; }
            internal string Group { get; }
        }

        /// <summary>
        /// The JSON 1.x reported: <c>JsonArray.Stringify()</c> of <c>[{"id":N,"tag":"…","group":"…"}]</c>,
        /// no spaces, <c>[]</c> for none. Escapes follow what <c>Windows.Data.Json</c> wrote (design v8,
        /// 4.4): <c>\"</c>, <c>\\</c>, the short forms of <c>\b \f \n \r \t</c>, other control characters as
        /// upper-case <c>\u001F</c>; <c>/</c>, DEL and non-ASCII unescaped.
        /// </summary>
        internal static string BuildGetAllJson(IReadOnlyList<ListedNotification> notifications)
        {
            var builder = new StringBuilder("[");
            for (int i = 0; i < notifications.Count; i++)
            {
                if (i > 0) builder.Append(',');
                builder.Append("{\"id\":").Append(notifications[i].Id.ToString(CultureInfo.InvariantCulture));
                builder.Append(",\"tag\":");
                AppendJsonString(builder, notifications[i].Tag);
                builder.Append(",\"group\":");
                AppendJsonString(builder, notifications[i].Group);
                builder.Append('}');
            }
            return builder.Append(']').ToString();
        }

        private static void AppendJsonString(StringBuilder builder, string value)
        {
            builder.Append('"');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\b': builder.Append("\\b"); break;
                    case '\f': builder.Append("\\f"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (c < 0x20) builder.Append("\\u").Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
                        else builder.Append(c);
                        break;
                }
            }
            builder.Append('"');
        }

        // ── The JSON payload as builder calls (pure) ─────────────────────────────

        /// <summary>A content builder function (<c>ntk_notification_content_*</c>).</summary>
        internal enum ContentCall
        {
            SetTitle,
            SetBody,
            SetTag,
            SetGroup,
            SetScenario,
            SetDuration,
            AddButton,
            AddButtonArgument,
            AddTextInput,
            AddCombo,
            AddComboItem,
            SetAppLogo,
            SetHeroImage,
            SetInlineImage,
            SetAudio,
            SetProgress,
            SetAttribution,
            SetTimestamp,
            SetExpiration,
            SetExpiresOnReboot,
        }

        /// <summary>
        /// One builder call and its arguments after the content handle, in the C declaration's
        /// order: strings (null for NULL), <see cref="int"/> for the enums, flags and indexes,
        /// <see cref="long"/> for times, <see cref="double"/> for the progress value.
        /// </summary>
        internal sealed class ContentStep
        {
            internal ContentStep(ContentCall call, params object?[] arguments)
            {
                Call = call;
                Arguments = arguments;
            }

            internal ContentCall Call { get; }
            internal IReadOnlyList<object?> Arguments { get; }

            internal string? Text(int at) => (string?)Arguments[at];
            internal int Int(int at) => (int)Arguments[at]!;
            internal long Long(int at) => (long)Arguments[at]!;
            internal double Double(int at) => (double)Arguments[at]!;

            /// <summary><c>Call(arg, ...)</c>, strings quoted and NULL for null. For tests and logs.</summary>
            public override string ToString()
            {
                var builder = new StringBuilder(Call.ToString()).Append('(');
                for (int i = 0; i < Arguments.Count; i++)
                {
                    if (i > 0) builder.Append(", ");
                    builder.Append(Arguments[i] switch
                    {
                        null => "NULL",
                        string text => "\"" + text + "\"",
                        double number => number.ToString("R", CultureInfo.InvariantCulture),
                        IFormattable other => other.ToString(null, CultureInfo.InvariantCulture),
                        var other => other.ToString(),
                    });
                }
                return builder.Append(')').ToString();
            }
        }

        /// <summary>
        /// Reads a <c>ShowNotification</c> / <c>ScheduleNotification</c> payload the way the 1.x
        /// native library did (design v8, 4.2) and turns it into the builder calls, in 1.x's order.
        /// </summary>
        /// <returns>
        /// 0, or 3 when the text is not a JSON object (null included), or 7 when a key 1.x reads is
        /// missing where it was required, has another type, or is <c>null</c>. Nothing else is
        /// checked here: the button count, a looping sound without <c>duration: "long"</c>, and a
        /// button with both <c>args</c> and <c>invokeUri</c> are left to the native show, which
        /// refuses them with 7 as 1.x did.
        /// </returns>
        internal static int PlanContent(string? json, out List<ContentStep> steps)
        {
            steps = new List<ContentStep>();
            WindowsJsonValue? root = WindowsNotificationJsonReader.Parse(json);
            if (root == null || root.Kind != WindowsJsonValueKind.Object) return ErrorInvalidPayload;

            var plan = new ContentPlanner(root, steps);
            return plan.Run() ? ErrorNone : ErrorInvalidParameter;
        }

        /// <summary>Walks the payload once; any false stops it with 7.</summary>
        private sealed class ContentPlanner
        {
            private readonly WindowsJsonValue _root;
            private readonly List<ContentStep> _steps;
            private int _buttons;
            private int _combos;

            internal ContentPlanner(WindowsJsonValue root, List<ContentStep> steps)
            {
                _root = root;
                _steps = steps;
            }

            private void Add(ContentCall call, params object?[] arguments) => _steps.Add(new ContentStep(call, arguments));

            internal bool Run()
            {
                if (!OptionalString(_root, "title", out string? title)) return false;
                if (title != null) Add(ContentCall.SetTitle, title);
                if (!OptionalString(_root, "body", out string? body)) return false;
                if (body != null) Add(ContentCall.SetBody, body);
                if (!OptionalString(_root, "tag", out string? tag)) return false;
                if (tag != null) Add(ContentCall.SetTag, tag);
                if (!OptionalString(_root, "group", out string? group)) return false;
                if (group != null) Add(ContentCall.SetGroup, group);

                if (!OptionalString(_root, "scenario", out string? scenario)) return false;
                int scenarioValue = scenario switch
                {
                    "reminder" => 1,
                    "alarm" => 2,
                    "urgent" => 3,
                    "incomingCall" => 4,
                    _ => 0,
                };
                // 1.x set nothing for another string, which leaves the default.
                if (scenarioValue != 0) Add(ContentCall.SetScenario, scenarioValue);

                if (!OptionalString(_root, "duration", out string? duration)) return false;
                if (duration == "long") Add(ContentCall.SetDuration, 1);

                return Buttons()
                    && TextBoxes()
                    && ComboBoxes()
                    && Images()
                    && Audio()
                    && Progress()
                    && Tail();
            }

            private bool Buttons()
            {
                if (!OptionalArray(_root, "buttons", out IReadOnlyList<WindowsJsonValue>? buttons)) return false;
                if (buttons == null) return true;

                foreach (WindowsJsonValue button in buttons)
                {
                    if (button.Kind != WindowsJsonValueKind.Object) return false;
                    if (!RequiredString(button, "label", out string label)) return false;
                    if (!OptionalObject(button, "args", out WindowsJsonValue? args)) return false;
                    if (!OptionalString(button, "invokeUri", out string? invokeUri)) return false;

                    // Both args and invokeUri go through: the native show refuses the pair with 7.
                    int index = _buttons++;
                    Add(ContentCall.AddButton, label, invokeUri, args != null ? 1 : 0, index);
                    if (args == null) continue;
                    foreach (KeyValuePair<string, WindowsJsonValue> argument in args.Members)
                    {
                        if (!argument.Value.TryGetString(out string value)) return false;
                        Add(ContentCall.AddButtonArgument, index, argument.Key, value);
                    }
                }
                return true;
            }

            private bool TextBoxes()
            {
                if (!OptionalArray(_root, "textBoxes", out IReadOnlyList<WindowsJsonValue>? boxes)) return false;
                if (boxes == null) return true;

                foreach (WindowsJsonValue box in boxes)
                {
                    if (box.Kind != WindowsJsonValueKind.Object) return false;
                    if (!RequiredString(box, "id", out string id)) return false;
                    if (!OptionalString(box, "placeholder", out string? placeholder)) return false;
                    if (!OptionalString(box, "title", out string? title)) return false;

                    // 1.x called AddTextBox(id) with neither, and AddTextBox(id, placeholder, title)
                    // with empty strings for the one missing.
                    if (placeholder == null && title == null)
                        Add(ContentCall.AddTextInput, id, null, null);
                    else
                        Add(ContentCall.AddTextInput, id, placeholder ?? "", title ?? "");
                }
                return true;
            }

            private bool ComboBoxes()
            {
                if (!OptionalArray(_root, "comboBoxes", out IReadOnlyList<WindowsJsonValue>? combos)) return false;
                if (combos == null) return true;

                foreach (WindowsJsonValue combo in combos)
                {
                    if (combo.Kind != WindowsJsonValueKind.Object) return false;
                    if (!RequiredString(combo, "id", out string id)) return false;
                    if (!OptionalString(combo, "title", out string? title)) return false;
                    if (!OptionalArray(combo, "items", out IReadOnlyList<WindowsJsonValue>? items)) return false;
                    if (!OptionalString(combo, "defaultSelection", out string? defaultSelection)) return false;

                    int index = _combos++;
                    Add(ContentCall.AddCombo, id, title, defaultSelection, index);
                    if (items == null) continue;
                    foreach (WindowsJsonValue item in items)
                    {
                        if (item.Kind != WindowsJsonValueKind.Object) return false;
                        if (!RequiredString(item, "id", out string itemId)) return false;
                        if (!RequiredString(item, "label", out string itemLabel)) return false;
                        Add(ContentCall.AddComboItem, index, itemId, itemLabel);
                    }
                }
                return true;
            }

            private bool Images()
            {
                if (!OptionalObject(_root, "appLogo", out WindowsJsonValue? logo)) return false;
                if (logo != null)
                {
                    if (!OptionalString(logo, "crop", out string? crop)) return false;
                    if (!RequiredString(logo, "uri", out string uri)) return false;
                    Add(ContentCall.SetAppLogo, uri, crop == "circle" ? 1 : 0);
                }

                if (!OptionalString(_root, "heroImage", out string? hero)) return false;
                if (hero != null) Add(ContentCall.SetHeroImage, hero);
                if (!OptionalString(_root, "inlineImage", out string? inline)) return false;
                if (inline != null) Add(ContentCall.SetInlineImage, inline);
                return true;
            }

            /// <summary>
            /// 1.x read <c>loop</c> whatever the type (its validation did), <c>uri</c> only for
            /// <c>"uri"</c>, and <c>event</c> only when the type was neither <c>"mute"</c> nor
            /// <c>"uri"</c>. <c>src</c> it never read (design v8 J-5).
            /// </summary>
            private bool Audio()
            {
                if (!OptionalObject(_root, "audio", out WindowsJsonValue? audio)) return false;
                if (audio == null) return true;

                if (!OptionalBool(audio, "loop", out bool? loop)) return false;
                if (!OptionalString(audio, "type", out string? type)) return false;
                int loopFlag = loop == true ? 1 : 0;

                switch (type)
                {
                    case "mute":
                        Add(ContentCall.SetAudio, 1, null, null, loopFlag);
                        return true;
                    case "uri":
                        if (!RequiredString(audio, "uri", out string uri)) return false;
                        Add(ContentCall.SetAudio, 2, null, uri, loopFlag);
                        return true;
                    default:
                        if (!OptionalString(audio, "event", out string? eventName)) return false;
                        Add(ContentCall.SetAudio, 0, eventName, null, loopFlag);
                        return true;
                }
            }

            private bool Progress()
            {
                if (!OptionalObject(_root, "progress", out WindowsJsonValue? progress)) return false;
                if (progress == null) return true;

                if (!OptionalString(progress, "title", out string? title)) return false;
                if (!OptionalNumber(progress, "value", out double? value)) return false;
                if (!OptionalString(progress, "valueStr", out string? valueString)) return false;
                if (!OptionalString(progress, "status", out string? status)) return false;
                Add(ContentCall.SetProgress, title, value ?? 0.0, valueString, status);
                return true;
            }

            private bool Tail()
            {
                if (!OptionalString(_root, "attribution", out string? attribution)) return false;
                if (attribution != null) Add(ContentCall.SetAttribution, attribution);

                if (!OptionalNumber(_root, "timestamp", out double? timestamp)) return false;
                if (timestamp != null)
                {
                    if (!TryTimestampMilliseconds(timestamp.Value, out long milliseconds)) return false;
                    Add(ContentCall.SetTimestamp, milliseconds);
                }

                if (!OptionalNumber(_root, "expiration", out double? expiration)) return false;
                if (expiration != null)
                {
                    if (!TryTruncateToLong(expiration.Value, out long seconds)) return false;
                    Add(ContentCall.SetExpiration, seconds);
                }

                if (!OptionalBool(_root, "expiresOnReboot", out bool? expiresOnReboot)) return false;
                if (expiresOnReboot == true) Add(ContentCall.SetExpiresOnReboot, 1);
                return true;
            }
        }

        /// <summary>
        /// A <c>timestamp</c> in Unix seconds, truncated toward zero as 1.x's <c>static_cast&lt;time_t&gt;</c>
        /// did, as the milliseconds the C ABI takes. False beyond <see cref="MaxTimestampSeconds"/>.
        /// </summary>
        internal static bool TryTimestampMilliseconds(double seconds, out long milliseconds)
        {
            milliseconds = 0;
            double whole = Math.Truncate(seconds);
            if (whole < -MaxTimestampSeconds || whole > MaxTimestampSeconds) return false;
            milliseconds = (long)whole * 1000;
            return true;
        }

        /// <summary>A number truncated toward zero, when it fits a <see cref="long"/>.</summary>
        internal static bool TryTruncateToLong(double value, out long result)
        {
            result = 0;
            double whole = Math.Truncate(value);
            // 2^63 is exactly representable; long.MaxValue is not, so compare against 2^63.
            if (whole < -9223372036854775808.0 || whole >= 9223372036854775808.0) return false;
            result = (long)whole;
            return true;
        }

        // Member readers. Each is false for a present member of another type or JSON null, and
        // true with null for an absent one; the Required forms are false for an absent one too.

        private static bool OptionalString(WindowsJsonValue owner, string key, out string? value)
        {
            value = null;
            if (!owner.TryGetMember(key, out WindowsJsonValue member)) return true;
            if (!member.TryGetString(out string text)) return false;
            value = text;
            return true;
        }

        private static bool RequiredString(WindowsJsonValue owner, string key, out string value)
        {
            value = string.Empty;
            return owner.TryGetMember(key, out WindowsJsonValue member) && member.TryGetString(out value);
        }

        private static bool OptionalBool(WindowsJsonValue owner, string key, out bool? value)
        {
            value = null;
            if (!owner.TryGetMember(key, out WindowsJsonValue member)) return true;
            if (!member.TryGetBool(out bool flag)) return false;
            value = flag;
            return true;
        }

        private static bool OptionalNumber(WindowsJsonValue owner, string key, out double? value)
        {
            value = null;
            if (!owner.TryGetMember(key, out WindowsJsonValue member)) return true;
            if (!member.TryGetNumber(out double number)) return false;
            value = number;
            return true;
        }

        private static bool OptionalObject(WindowsJsonValue owner, string key, out WindowsJsonValue? value)
        {
            value = null;
            if (!owner.TryGetMember(key, out WindowsJsonValue member)) return true;
            if (member.Kind != WindowsJsonValueKind.Object) return false;
            value = member;
            return true;
        }

        private static bool OptionalArray(WindowsJsonValue owner, string key, out IReadOnlyList<WindowsJsonValue>? value)
        {
            value = null;
            if (!owner.TryGetMember(key, out WindowsJsonValue member)) return true;
            if (member.Kind != WindowsJsonValueKind.Array) return false;
            value = member.Elements;
            return true;
        }
    }
}
#endif
