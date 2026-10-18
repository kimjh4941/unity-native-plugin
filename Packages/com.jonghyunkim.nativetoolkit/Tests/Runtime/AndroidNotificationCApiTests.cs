#nullable enable

#if UNITY_ANDROID || UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using NUnit.Framework;
using JonghyunKim.NativeToolkit.Runtime.Android.Common;
using JonghyunKim.NativeToolkit.Runtime.Android.Notification;
using Api = JonghyunKim.NativeToolkit.Runtime.Android.Notification.AndroidNotificationCApi;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// Layer 1 tests for <see cref="AndroidNotificationCApi"/>: the two structs against the C ABI's
    /// layout, the callbacks' shapes, every error value's message, the events' action IDs, and the
    /// schedule options and action entries built from a spec
    /// (artifact/android/notification/designs/2026-10-12-android-notification-design-v4.md, 7.1).
    /// </summary>
    public sealed class AndroidNotificationCApiTests
    {
        private static readonly string[] Operations =
        {
            AndroidNotificationManager.OperationOpenNotificationSettings, AndroidNotificationManager.OperationOpenAppDetailsSettings,
            AndroidNotificationManager.OperationOpenExactAlarmSettings, AndroidNotificationManager.OperationCreateChannel,
            AndroidNotificationManager.OperationDeleteChannel, AndroidNotificationManager.OperationShowNotification,
            AndroidNotificationManager.OperationUpdateNotification, AndroidNotificationManager.OperationCancelNotification,
            AndroidNotificationManager.OperationCancelAllNotifications, AndroidNotificationManager.OperationScheduleNotification,
            AndroidNotificationManager.OperationCancelScheduledNotification, AndroidNotificationManager.OperationCancelAllScheduledNotifications,
            AndroidNotificationManager.OperationStartProgressForegroundService, AndroidNotificationManager.OperationUpdateProgressForegroundService,
            AndroidNotificationManager.OperationCompleteProgressForegroundService, AndroidNotificationManager.OperationStopProgressForegroundService,
        };

        private readonly List<IntPtr> _allocated = new();

        [TearDown]
        public void FreeAllocated() => AndroidNativeToolkitCApi.FreeAll(_allocated);

        private static string? Utf8At(IntPtr pointer)
        {
            if (pointer == IntPtr.Zero) return null;
            var bytes = new List<byte>();
            for (int i = 0; ; i++)
            {
                byte b = Marshal.ReadByte(pointer, i);
                if (b == 0) break;
                bytes.Add(b);
            }
            return Encoding.UTF8.GetString(bytes.ToArray());
        }

        // ── Layout (native-toolkit StructLayoutTest.cpp:96-116) ──────────────────

        [Test]
        public void ScheduleOptions_Layout()
        {
            Assert.AreEqual(32, Marshal.SizeOf<Api.ScheduleOptions>());
            Assert.AreEqual(0, Marshal.OffsetOf<Api.ScheduleOptions>("StructSize").ToInt32());
            Assert.AreEqual(4, Marshal.OffsetOf<Api.ScheduleOptions>("Reserved0").ToInt32());
            Assert.AreEqual(8, Marshal.OffsetOf<Api.ScheduleOptions>("TriggerAtMillis").ToInt32());
            Assert.AreEqual(16, Marshal.OffsetOf<Api.ScheduleOptions>("Inexact").ToInt32());
            Assert.AreEqual(20, Marshal.OffsetOf<Api.ScheduleOptions>("NotAllowWhileIdle").ToInt32());
            Assert.AreEqual(24, Marshal.OffsetOf<Api.ScheduleOptions>("NotPersistAcrossBoot").ToInt32());
            Assert.AreEqual(28, Marshal.OffsetOf<Api.ScheduleOptions>("AlarmType").ToInt32());
        }

        [Test]
        public void ActionEntry_Layout()
        {
            Assert.AreEqual(56, Marshal.SizeOf<Api.ActionEntry>());
            Assert.AreEqual(0, Marshal.OffsetOf<Api.ActionEntry>("StructSize").ToInt32());
            Assert.AreEqual(4, Marshal.OffsetOf<Api.ActionEntry>("Reserved0").ToInt32());
            Assert.AreEqual(8, Marshal.OffsetOf<Api.ActionEntry>("Title").ToInt32());
            Assert.AreEqual(16, Marshal.OffsetOf<Api.ActionEntry>("ActionId").ToInt32());
            Assert.AreEqual(24, Marshal.OffsetOf<Api.ActionEntry>("IconName").ToInt32());
            Assert.AreEqual(32, Marshal.OffsetOf<Api.ActionEntry>("LaunchApp").ToInt32());
            Assert.AreEqual(36, Marshal.OffsetOf<Api.ActionEntry>("AllowGeneratedReplies").ToInt32());
            Assert.AreEqual(40, Marshal.OffsetOf<Api.ActionEntry>("SemanticAction").ToInt32());
            Assert.AreEqual(44, Marshal.OffsetOf<Api.ActionEntry>("Contextual").ToInt32());
            Assert.AreEqual(48, Marshal.OffsetOf<Api.ActionEntry>("NoUserInterface").ToInt32());
            Assert.AreEqual(52, Marshal.OffsetOf<Api.ActionEntry>("Reserved1").ToInt32());
        }

        [Test]
        public void Callbacks_HaveTheCAbiShapes()
        {
            AssertShape(typeof(Api.SettingsCallback), typeof(IntPtr), typeof(int), typeof(uint), typeof(int));
            AssertShape(typeof(Api.InteractionCallback), typeof(IntPtr), typeof(IntPtr));
            AssertShape(typeof(Api.ShownCallback), typeof(IntPtr), typeof(IntPtr));

            static void AssertShape(Type callback, params Type[] parameters)
            {
                MethodInfo invoke = callback.GetMethod("Invoke")!;
                CollectionAssert.AreEqual(parameters, invoke.GetParameters().Select(p => p.ParameterType).ToArray(), callback.Name);
                Assert.AreEqual(typeof(void), invoke.ReturnType, callback.Name);
                Assert.AreEqual(CallingConvention.Cdecl, callback.GetCustomAttribute<UnmanagedFunctionPointerAttribute>()!.CallingConvention, callback.Name);
            }
        }

        [Test]
        public void Enumerations_HaveTheHeadersValues()
        {
            // Notification.h:41-72. The tests elsewhere use the constants, so a swap would pass there.
            Assert.AreEqual((0, 1, 2), (Api.TargetNotifications, Api.TargetAppDetails, Api.TargetExactAlarm));
            Assert.AreEqual((0, 1), (Api.ResultOpened, Api.ResultOpenedFallback));
            Assert.AreEqual((0, 1), (Api.TapOpenApp, Api.TapEventOnly));
            Assert.AreEqual((0, 1, 2), (Api.KindBodyTap, Api.KindAction, Api.KindDismiss));
        }

        [Test]
        public void ActionIds_MatchTheManagersConstants()
        {
            Assert.AreEqual(AndroidNotificationManager.ActionBodyTap, Api.BodyTapActionId);
            Assert.AreEqual(AndroidNotificationManager.ActionNotificationDismissed, Api.DismissedActionId);
        }

        // ── Errors (4.5) ─────────────────────────────────────────────────────────

        [Test]
        public void ErrorName_EveryValue()
        {
            string[] names =
            {
                "NONE", "INVALID_PARAMETER", "NOT_INITIALIZED", "NOT_SUPPORTED", "UNKNOWN", "OUT_OF_MEMORY", "CANCELED", "CANCELED_BY_SYSTEM",
                "NOT_FOREGROUND", "HOST_START_FAILED", "PERMISSION_DENIED", "EXACT_ALARM_NOT_ALLOWED", "RESOURCE_NOT_FOUND", "STORAGE_FAILED",
                "SERVICE_START_NOT_ALLOWED", "SETTINGS_NOT_OPENED",
            };
            for (int error = 0; error < names.Length; error++)
                Assert.AreEqual(names[error], Api.ErrorName(error));
            Assert.AreEqual("ERROR_16", Api.ErrorName(16));
            Assert.AreEqual("ERROR_-1", Api.ErrorName(-1));
        }

        [TestCaseSource(nameof(Operations))]
        public void FailureMessage_EveryValue(string op)
        {
            Assert.AreEqual($"{op} could not be started.", Api.FailureMessage(op, Api.ErrorNotInitialized));
            Assert.AreEqual($"Invalid argument for {op}: Please verify the input parameters.", Api.FailureMessage(op, Api.ErrorInvalidParameter));
            Assert.AreEqual($"Failed to {op}: NOT_SUPPORTED", Api.FailureMessage(op, Api.ErrorNotSupported));
            Assert.AreEqual($"Failed to {op}: UNKNOWN", Api.FailureMessage(op, Api.ErrorUnknown));
            Assert.AreEqual($"Failed to {op}: OUT_OF_MEMORY", Api.FailureMessage(op, Api.ErrorOutOfMemory));
            Assert.AreEqual($"Failed to {op}: CANCELED", Api.FailureMessage(op, Api.ErrorCanceled));
            Assert.AreEqual($"Failed to {op}: CANCELED_BY_SYSTEM", Api.FailureMessage(op, Api.ErrorCanceledBySystem));
            Assert.AreEqual($"Failed to {op}: the app is not in the foreground.", Api.FailureMessage(op, Api.ErrorNotForeground));
            Assert.AreEqual($"Failed to {op}: HOST_START_FAILED", Api.FailureMessage(op, Api.ErrorHostStartFailed));
            Assert.AreEqual($"Failed to {op}: notifications are not allowed for this app (permission or app notification setting).", Api.FailureMessage(op, Api.ErrorPermissionDenied));
            Assert.AreEqual($"Failed to {op}: EXACT_ALARM_NOT_ALLOWED", Api.FailureMessage(op, Api.ErrorExactAlarmNotAllowed));
            Assert.AreEqual($"Failed to {op}: a resource named in the notification was not found.", Api.FailureMessage(op, Api.ErrorResourceNotFound));
            Assert.AreEqual($"Failed to {op}: the schedule store could not be read or written.", Api.FailureMessage(op, Api.ErrorStorageFailed));
            Assert.AreEqual($"Failed to {op}: the system did not allow the foreground service from the background.", Api.FailureMessage(op, Api.ErrorServiceStartNotAllowed));
            Assert.AreEqual($"No Activity found to handle {op}. Please check device settings support.", Api.FailureMessage(op, Api.ErrorSettingsNotOpened));
            Assert.AreEqual($"Failed to {op}: ERROR_99", Api.FailureMessage(op, 99));
            Assert.AreEqual($"{op} could not be started.", Api.NotStartedMessage(op));
        }

        [Test]
        public void ProgressRequiredMessage_As1x() =>
            Assert.AreEqual("Invalid argument for startProgressForegroundService: progress is required for Progress FGS operations.",
                Api.ProgressRequiredMessage(AndroidNotificationManager.OperationStartProgressForegroundService));

        [Test]
        public void IsRequestDefect_OnlyNotSupported()
        {
            for (int error = 0; error <= 16; error++)
                Assert.AreEqual(error == Api.ErrorNotSupported, Api.IsRequestDefect(error), Api.ErrorName(error));
        }

        // ── Events (4.4) ─────────────────────────────────────────────────────────

        [Test]
        public void ActionIdOf_EachKind()
        {
            Assert.AreEqual(AndroidNotificationManager.ActionBodyTap, Api.ActionIdOf(Api.KindBodyTap, "ignored"));
            Assert.AreEqual(AndroidNotificationManager.ActionNotificationDismissed, Api.ActionIdOf(Api.KindDismiss, null));
            Assert.AreEqual("play", Api.ActionIdOf(Api.KindAction, "play"));
            Assert.IsNull(Api.ActionIdOf(Api.KindAction, null), "an action without an ID");
            Assert.IsNull(Api.ActionIdOf(Api.KindAction, ""), "an action with an empty ID");
            Assert.IsNull(Api.ActionIdOf(3, "x"), "an unknown kind");
            Assert.IsNull(Api.ActionIdOf(-1, "x"), "an unknown kind");
        }

        [Test]
        public void ShownTag_EmptyIsNone()
        {
            Assert.IsNull(Api.ShownTag(null));
            Assert.IsNull(Api.ShownTag(""));
            Assert.AreEqual(" ", Api.ShownTag(" "));
            Assert.AreEqual("t", Api.ShownTag("t"));
        }

        // ── Arguments (4.2) ──────────────────────────────────────────────────────

        [Test]
        public void BuildScheduleOptions_Defaults()
        {
            var spec = new AndroidNotificationScheduleSpec { TriggerAtMillis = 1714377600000 };
            Api.ScheduleOptions options = Api.BuildScheduleOptions(spec, Api.InexactOf(spec));
            Assert.AreEqual((32u, 0u, 1714377600000L, 0, 0, 0, 0),
                (options.StructSize, options.Reserved0, options.TriggerAtMillis, options.Inexact, options.NotAllowWhileIdle, options.NotPersistAcrossBoot, options.AlarmType));
        }

        [Test]
        public void BuildScheduleOptions_InvertsEachFlag()
        {
            var spec = new AndroidNotificationScheduleSpec { TriggerAtMillis = 5, Exact = false, AllowWhileIdle = false, PersistAcrossBoot = false, AlarmType = 1 };
            Assert.AreEqual(1, Api.InexactOf(spec));
            Api.ScheduleOptions options = Api.BuildScheduleOptions(spec, Api.InexactOf(spec));
            Assert.AreEqual((1, 1, 1, 1), (options.Inexact, options.NotAllowWhileIdle, options.NotPersistAcrossBoot, options.AlarmType));
            Assert.AreEqual(1, Api.BuildScheduleOptions(new AndroidNotificationScheduleSpec(), 1).Inexact, "the retry's inexact overrides exact");
        }

        [Test]
        public void BuildAction_CopiesEveryValue()
        {
            Api.ActionEntry entry = Api.BuildAction(new AndroidNotificationActionSpec
            {
                Title = "再生", ActionId = "play", IconName = "ic", LaunchApp = true, AllowGeneratedReplies = true, SemanticAction = 10,
                Contextual = true, ShowsUserInterface = false,
            }, _allocated);
            Assert.AreEqual((56u, 0u, 0u), (entry.StructSize, entry.Reserved0, entry.Reserved1));
            Assert.AreEqual(("再生", "play", "ic"), (Utf8At(entry.Title), Utf8At(entry.ActionId), Utf8At(entry.IconName)));
            Assert.AreEqual((1, 1, 10, 1, 1), (entry.LaunchApp, entry.AllowGeneratedReplies, entry.SemanticAction, entry.Contextual, entry.NoUserInterface));

            Api.ActionEntry plain = Api.BuildAction(new AndroidNotificationActionSpec { Title = "t", ActionId = "a" }, _allocated);
            Assert.AreEqual(IntPtr.Zero, plain.IconName, "no icon");
            Assert.AreEqual((0, 0, 0, 0, 0), (plain.LaunchApp, plain.AllowGeneratedReplies, plain.SemanticAction, plain.Contextual, plain.NoUserInterface));
        }

        [Test]
        public void TapOf_OpenAppOrEventOnly()
        {
            Assert.AreEqual(Api.TapOpenApp, Api.TapOf(new AndroidNotificationSpec { LaunchAppOnTap = true }));
            Assert.AreEqual(Api.TapEventOnly, Api.TapOf(new AndroidNotificationSpec { LaunchAppOnTap = false }));
        }

        [Test]
        public void AllocLongs_CopiesThePattern()
        {
            IntPtr array = Api.AllocLongs(new long[] { 0, 250, -1, long.MaxValue }, _allocated);
            Assert.AreEqual(new long[] { 0, 250, -1, long.MaxValue }, Enumerable.Range(0, 4).Select(i => Marshal.ReadInt64(array, i * 8)).ToArray());
            Assert.AreNotEqual(IntPtr.Zero, Api.AllocLongs(new long[0], _allocated), "never NULL");
        }
    }
}
#endif
