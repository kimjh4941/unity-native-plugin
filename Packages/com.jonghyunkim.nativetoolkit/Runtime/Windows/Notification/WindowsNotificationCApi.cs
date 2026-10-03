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
    /// into a Windows player. Design: artifact/windows/notification/designs/2026-09-27-windows-notification-design-v8.md.
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

        // ── Native calls ─────────────────────────────────────────────────────────
        // Compiled only into a Windows player. The manager keeps its calls inside the same #if and
        // does nothing in the Editor (design v8 J-4), so there is nothing to stub here. Each wrapper
        // returns ntk_notification_error; where a failure is worth logging, the system code is read
        // right after the failing call, before any other native call.

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        /// <summary><c>ntk_notification_invoked_fn</c>. The activation is valid only during the call.</summary>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void InvokedCallback(IntPtr userData, IntPtr activation);

        /// <summary>
        /// <c>GetCurrentPackageFullName</c> with a zero length and no buffer: 15700 without package
        /// identity, 122 with it (<see cref="ClassifyPackage"/>).
        /// </summary>
        internal static int QueryPackageIdentity()
        {
            uint length = 0;
            return GetCurrentPackageFullName(ref length, IntPtr.Zero);
        }

        internal static int InitializeRuntime(out IntPtr runtime, out uint systemCode)
        {
            runtime = IntPtr.Zero;
            systemCode = 0;
            int error = ntk_notification_runtime_initialize(RuntimeVersion, out runtime);
            if (error != ErrorNone) systemCode = WindowsNativeToolkitCApi.ntk_last_system_code();
            return error;
        }

        internal static int CreateManager(IntPtr onInvoked, bool isPackaged, string? displayName, string? iconUri,
            out IntPtr manager, out uint systemCode)
        {
            manager = IntPtr.Zero;
            systemCode = 0;
            var allocated = new List<IntPtr>();
            try
            {
                ManagerOptions options = BuildManagerOptions(onInvoked, isPackaged,
                    WindowsNativeToolkitCApi.AllocUtf8(displayName, allocated),
                    WindowsNativeToolkitCApi.AllocUtf8(iconUri, allocated));
                int error = ntk_notification_manager_create(ref options, out manager);
                if (error != ErrorNone) systemCode = WindowsNativeToolkitCApi.ntk_last_system_code();
                return error;
            }
            finally
            {
                WindowsNativeToolkitCApi.FreeAll(allocated);
            }
        }

        /// <summary>Closes and frees the manager. Does nothing for <see cref="IntPtr.Zero"/>.</summary>
        internal static void FreeManager(IntPtr manager)
        {
            if (manager != IntPtr.Zero) ntk_notification_manager_free(manager);
        }

        /// <summary>Releases the runtime; it unloads once no manager is open. Does nothing for <see cref="IntPtr.Zero"/>.</summary>
        internal static void FreeRuntime(IntPtr runtime)
        {
            if (runtime != IntPtr.Zero) ntk_notification_runtime_free(runtime);
        }

        internal static int GetSetting(IntPtr manager, out int setting)
        {
            setting = -1;
            return ntk_notification_get_setting(manager, out setting);
        }

        /// <summary>Builds the content from the steps and shows it.</summary>
        internal static int Show(IntPtr manager, IReadOnlyList<ContentStep> steps, out uint systemCode) =>
            WithContent(steps, content => ntk_notification_show(manager, content), out systemCode);

        /// <summary>Builds the content from the steps and schedules it at <paramref name="unixMs"/>.</summary>
        internal static int Schedule(IntPtr manager, IReadOnlyList<ContentStep> steps, long unixMs, out uint systemCode) =>
            WithContent(steps, content => ntk_notification_schedule(manager, content, unixMs), out systemCode);

        internal static int CancelScheduled(IntPtr manager, string? tag, string? group) =>
            WithTagAndGroup(tag, group, (t, g) => ntk_notification_cancel_scheduled(manager, t, g));

        internal static int RemoveByTag(IntPtr manager, string? tag, string? group) =>
            WithTagAndGroup(tag, group, (t, g) => ntk_notification_remove_by_tag(manager, t, g));

        internal static int UpdateProgress(IntPtr manager, string? tag, string? group, double value, string? valueString,
            string? status, uint sequenceNumber)
        {
            var allocated = new List<IntPtr>();
            try
            {
                ProgressUpdate update = BuildProgressUpdate(
                    WindowsNativeToolkitCApi.AllocUtf8(tag, allocated),
                    WindowsNativeToolkitCApi.AllocUtf8(group, allocated),
                    value,
                    WindowsNativeToolkitCApi.AllocUtf8(valueString, allocated),
                    WindowsNativeToolkitCApi.AllocUtf8(status, allocated),
                    sequenceNumber);
                return ntk_notification_update_progress(manager, ref update);
            }
            finally
            {
                WindowsNativeToolkitCApi.FreeAll(allocated);
            }
        }

        internal static int SetBadge(IntPtr manager, int value) => ntk_notification_set_badge(manager, value);

        internal static int RemoveById(IntPtr manager, uint id) => ntk_notification_remove_by_id(manager, id);

        internal static int RemoveAll(IntPtr manager) => ntk_notification_remove_all(manager);

        internal static int OpenSettings(IntPtr manager) => ntk_notification_open_settings(manager);

        /// <summary>Lists the app's notifications; the list handle is read and freed here.</summary>
        internal static int GetAll(IntPtr manager, out List<ListedNotification>? notifications, out uint systemCode)
        {
            notifications = null;
            systemCode = 0;
            IntPtr list = IntPtr.Zero;
            try
            {
                int error = ntk_notification_get_all(manager, out list);
                if (error != ErrorNone)
                {
                    systemCode = WindowsNativeToolkitCApi.ntk_last_system_code();
                    return error;
                }

                ulong count = ntk_notification_list_count(list).ToUInt64();
                var read = new List<ListedNotification>();
                for (ulong i = 0; i < count; i++)
                {
                    var index = new UIntPtr(i);
                    IntPtr tag = ntk_notification_list_tag_at(list, index, out UIntPtr tagSize);
                    IntPtr group = ntk_notification_list_group_at(list, index, out UIntPtr groupSize);
                    read.Add(new ListedNotification(
                        ntk_notification_list_id_at(list, index),
                        WindowsNativeToolkitCApi.ReadUtf8(tag, WindowsNativeToolkitCApi.ToByteCount(tagSize)),
                        WindowsNativeToolkitCApi.ReadUtf8(group, WindowsNativeToolkitCApi.ToByteCount(groupSize))));
                }
                notifications = read;
                return ErrorNone;
            }
            finally
            {
                if (list != IntPtr.Zero) ntk_notification_list_free(list);
            }
        }

        /// <summary>
        /// The activation's arguments as the JSON text 1.x passed (<c>raw_arguments</c>), copied out
        /// at once: the activation lives only during the callback.
        /// </summary>
        internal static string ReadActivationArguments(IntPtr activation)
        {
            IntPtr data = ntk_notification_activation_raw_arguments(activation, out UIntPtr size);
            return WindowsNativeToolkitCApi.ReadUtf8(data, WindowsNativeToolkitCApi.ToByteCount(size));
        }

        private static int WithTagAndGroup(string? tag, string? group, Func<IntPtr, IntPtr, int> call)
        {
            var allocated = new List<IntPtr>();
            try
            {
                return call(WindowsNativeToolkitCApi.AllocUtf8(tag, allocated), WindowsNativeToolkitCApi.AllocUtf8(group, allocated));
            }
            finally
            {
                WindowsNativeToolkitCApi.FreeAll(allocated);
            }
        }

        /// <summary>
        /// Creates the content, applies the steps in order, and hands it to <paramref name="deliver"/>.
        /// The first step that fails stops it with its error. The content and every UTF-8 copy are
        /// freed whatever happens, the copies even when freeing the content throws.
        /// </summary>
        private static int WithContent(IReadOnlyList<ContentStep> steps, Func<IntPtr, int> deliver, out uint systemCode)
        {
            systemCode = 0;
            var allocated = new List<IntPtr>();
            IntPtr content = IntPtr.Zero;
            try
            {
                int error = ntk_notification_content_create(out content);
                for (int i = 0; error == ErrorNone && i < steps.Count; i++)
                {
                    error = Apply(content, steps[i], allocated);
                    if (error != ErrorNone)
                        Debug.LogWarning($"[{LogTag}][{nameof(WithContent)}] {steps[i].Call} failed with error {error}");
                }
                if (error == ErrorNone) error = deliver(content);
                if (error != ErrorNone) systemCode = WindowsNativeToolkitCApi.ntk_last_system_code();
                return error;
            }
            finally
            {
                try
                {
                    if (content != IntPtr.Zero) ntk_notification_content_free(content);
                }
                finally
                {
                    WindowsNativeToolkitCApi.FreeAll(allocated);
                }
            }
        }

        /// <summary>One builder call. The strings are copied as UTF-8 into <paramref name="allocated"/>.</summary>
        private static int Apply(IntPtr content, ContentStep step, List<IntPtr> allocated)
        {
            IntPtr Text(int at) => WindowsNativeToolkitCApi.AllocUtf8(step.Text(at), allocated);
            UIntPtr Index(int at) => new UIntPtr((uint)step.Int(at));

            switch (step.Call)
            {
                case ContentCall.SetTitle: return ntk_notification_content_set_title(content, Text(0));
                case ContentCall.SetBody: return ntk_notification_content_set_body(content, Text(0));
                case ContentCall.SetTag: return ntk_notification_content_set_tag(content, Text(0));
                case ContentCall.SetGroup: return ntk_notification_content_set_group(content, Text(0));
                case ContentCall.SetScenario: return ntk_notification_content_set_scenario(content, step.Int(0));
                case ContentCall.SetDuration: return ntk_notification_content_set_duration(content, step.Int(0));
                case ContentCall.AddButton:
                {
                    int error = ntk_notification_content_add_button(content, Text(0), Text(1), step.Int(2), out UIntPtr index);
                    return error == ErrorNone ? ExpectIndex(step, index, 3) : error;
                }
                case ContentCall.AddButtonArgument:
                    return ntk_notification_content_add_button_argument(content, Index(0), Text(1), Text(2));
                case ContentCall.AddTextInput: return ntk_notification_content_add_text_input(content, Text(0), Text(1), Text(2));
                case ContentCall.AddCombo:
                {
                    int error = ntk_notification_content_add_combo(content, Text(0), Text(1), Text(2), out UIntPtr index);
                    return error == ErrorNone ? ExpectIndex(step, index, 3) : error;
                }
                case ContentCall.AddComboItem: return ntk_notification_content_add_combo_item(content, Index(0), Text(1), Text(2));
                case ContentCall.SetAppLogo: return ntk_notification_content_set_app_logo(content, Text(0), step.Int(1));
                case ContentCall.SetHeroImage: return ntk_notification_content_set_hero_image(content, Text(0));
                case ContentCall.SetInlineImage: return ntk_notification_content_set_inline_image(content, Text(0));
                case ContentCall.SetAudio: return ntk_notification_content_set_audio(content, step.Int(0), Text(1), Text(2), step.Int(3));
                case ContentCall.SetProgress: return ntk_notification_content_set_progress(content, Text(0), step.Double(1), Text(2), Text(3));
                case ContentCall.SetAttribution: return ntk_notification_content_set_attribution(content, Text(0));
                case ContentCall.SetTimestamp: return ntk_notification_content_set_timestamp(content, step.Long(0));
                case ContentCall.SetExpiration: return ntk_notification_content_set_expiration(content, step.Long(0));
                case ContentCall.SetExpiresOnReboot: return ntk_notification_content_set_expires_on_reboot(content, step.Int(0));
                default: return ErrorHresultFailure;
            }
        }

        /// <summary>
        /// The index a button or combo got must be the one the plan gave its arguments and items;
        /// otherwise they would land on another element. It cannot differ while every add succeeds.
        /// </summary>
        private static int ExpectIndex(ContentStep step, UIntPtr actual, int at)
        {
            if (actual.ToUInt64() == (ulong)step.Int(at)) return ErrorNone;
            Debug.LogWarning($"[{LogTag}][{nameof(ExpectIndex)}] {step.Call} got index {actual.ToUInt64()}, not {step.Int(at)}");
            return ErrorHresultFailure;
        }

        [DllImport("kernel32.dll")]
        private static extern int GetCurrentPackageFullName(ref uint packageFullNameLength, IntPtr packageFullName);

        // Runtime and manager
        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_runtime_initialize(uint majorMinor, out IntPtr runtime);

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ntk_notification_runtime_free(IntPtr runtime);

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_manager_create(ref ManagerOptions options, out IntPtr manager);

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ntk_notification_manager_close(IntPtr manager);

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ntk_notification_manager_free(IntPtr manager);

        // Content builder
        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_content_create(out IntPtr content);

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ntk_notification_content_free(IntPtr content);

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_content_set_title(IntPtr content, IntPtr value);

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_content_set_body(IntPtr content, IntPtr value);

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_content_set_tag(IntPtr content, IntPtr value);

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_content_set_group(IntPtr content, IntPtr value);

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_content_set_scenario(IntPtr content, int value);

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_content_set_hero_image(IntPtr content, IntPtr value);

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_content_set_inline_image(IntPtr content, IntPtr value);

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_content_set_app_logo(IntPtr content, IntPtr uri, int crop);

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_content_set_attribution(IntPtr content, IntPtr value);

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_content_set_duration(IntPtr content, int value);

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_content_set_audio(IntPtr content, int kind, IntPtr eventName, IntPtr uri, int loop);

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_content_add_button(IntPtr content, IntPtr label, IntPtr invokeUri, int withArguments, out UIntPtr index);

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_content_add_button_argument(IntPtr content, UIntPtr buttonIndex, IntPtr key, IntPtr value);

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_content_add_text_input(IntPtr content, IntPtr id, IntPtr placeholder, IntPtr title);

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_content_add_combo(IntPtr content, IntPtr id, IntPtr title, IntPtr defaultSelection, out UIntPtr index);

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_content_add_combo_item(IntPtr content, UIntPtr comboIndex, IntPtr id, IntPtr label);

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_content_set_progress(IntPtr content, IntPtr title, double value, IntPtr valueString, IntPtr status);

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_content_set_timestamp(IntPtr content, long unixMs);

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_content_set_expiration(IntPtr content, long seconds);

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_content_set_expires_on_reboot(IntPtr content, int value);

        // Operations
        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_show(IntPtr manager, IntPtr content);

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_schedule(IntPtr manager, IntPtr content, long unixMs);

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_cancel_scheduled(IntPtr manager, IntPtr tag, IntPtr group);

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_update_progress(IntPtr manager, ref ProgressUpdate update);

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_set_badge(IntPtr manager, int value);

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_remove_by_id(IntPtr manager, uint id);

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_remove_by_tag(IntPtr manager, IntPtr tag, IntPtr group);

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_remove_all(IntPtr manager);

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_get_all(IntPtr manager, out IntPtr list);

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_get_setting(IntPtr manager, out int setting);

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_notification_open_settings(IntPtr manager);

        // List
        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern UIntPtr ntk_notification_list_count(IntPtr list);

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint ntk_notification_list_id_at(IntPtr list, UIntPtr index);

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr ntk_notification_list_tag_at(IntPtr list, UIntPtr index, out UIntPtr size);

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr ntk_notification_list_group_at(IntPtr list, UIntPtr index, out UIntPtr size);

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ntk_notification_list_free(IntPtr list);

        // Activation
        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr ntk_notification_activation_raw_arguments(IntPtr activation, out UIntPtr size);
#endif
    }
}
#endif
