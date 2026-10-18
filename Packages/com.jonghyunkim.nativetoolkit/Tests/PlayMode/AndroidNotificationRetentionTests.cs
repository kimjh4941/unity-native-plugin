#nullable enable

#if UNITY_ANDROID
#if !UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using JonghyunKim.NativeToolkit.Runtime.Android.Common;
using JonghyunKim.NativeToolkit.Runtime.Android.Notification;
using static JonghyunKim.NativeToolkit.Tests.AndroidNotificationTestSupport;
using Api = JonghyunKim.NativeToolkit.Runtime.Android.Notification.AndroidNotificationCApi;
using Ui = JonghyunKim.NativeToolkit.Tests.AndroidUiDriver;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// AN-01: a tap made while no manager exists is kept by the library and delivered once the manager
    /// is first created (artifact/android/notification/designs/2026-10-12-android-notification-ui-test-plan-v5.md,
    /// chapter 2). It holds only in a process where nothing has created the manager, so
    /// scripts/verify_unity_android.sh runs it alone, from a fresh install (category
    /// <see cref="Category"/>), and leaves it out of every other run.
    /// </summary>
    [Category(Category)]
    [Category(AndroidUiDriver.Category)]
    public sealed class AndroidNotificationRetentionTests
    {
        /// <summary>The category of the run that holds this test alone.</summary>
        public const string Category = "AndroidNotificationRetention";

        private const int Id = 7001;
        private const float WaitAfterTheTapSeconds = 3f;

        private AndroidNotificationEvents? _events;

        [SetUp]
        public void SetUp()
        {
            // Nothing here may touch the manager (plan R1-4).
            if (Application.platform != RuntimePlatform.Android)
                Assert.Ignore("Device-only.");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            var errors = new List<Exception>();
            yield return AndroidShareTestSupport.Safely(Ui.WaitForPending(), errors);
            _events?.Unsubscribe();
            _events = null;
            yield return null;
            yield return null;
            if (errors.Count > 0) Assert.Fail(string.Join("\n", errors.Select(e => e.Message)));
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator AN01_TapWithoutAManager_IsDeliveredWhenTheManagerIsCreated()
        {
            yield return Ui.Do("grant", "android.permission.POST_NOTIFICATIONS");
            Assert.AreEqual((false, false), AndroidNotificationManager.ListenersRegisteredForTests,
                "the manager was created earlier in this process: the test cannot tell a kept tap from a live one");

            // Shown through the C ABI directly, so that no listener exists yet: a body tap that only
            // raises the event (EVENT_ONLY), with data, cancelled by the tap.
            string title = Title("01");
            var spec = new AndroidNotificationSpec
            {
                Id = Id, Title = title, Message = "NTK retention test", Channel = new AndroidNotificationChannelSpec { Id = ChannelId, Name = ChannelName },
                SmallIconName = "app_icon", AutoCancel = true, LaunchAppOnTap = false,
            };
            spec.Data.Add(new KeyValuePair<string, string>("k", "v"));
            Assert.AreEqual(Api.ErrorNone, Show(spec), "shown through the C ABI");
            yield return Shows(Id);

            yield return Ui.Do("notification-tap", Ui.Base64(title), "stay");
            // Gone from the shade: the system took the tap (autoCancel). The receiver may run a little
            // later; the wait after it is what lets it run before the manager exists (plan R2-9, V-30).
            yield return Goes(Id, null, 10f);
            yield return new WaitForSecondsRealtime(WaitAfterTheTapSeconds);
            Assert.AreEqual((false, false), AndroidNotificationManager.ListenersRegisteredForTests, "still no manager");

            // The manager's first creation registers the listener, which delivers the kept tap in a later
            // frame: subscribe in the same frame as Instance (the manager's documentation).
            _events = new AndroidNotificationEvents();
            _events.Subscribe();
            float deadline = Time.realtimeSinceStartup + EventTimeoutSeconds;
            while (_events.Taps.Count == 0 && Time.realtimeSinceStartup < deadline)
                yield return null;
            yield return new WaitForSecondsRealtime(1f);

            Assert.AreEqual(new[] { $"Tap|{AndroidNotificationManager.ActionBodyTap}|{Id}|k=v" }, _events.Taps, _events.Describe());
            Assert.IsEmpty(_events.Ops, _events.Describe());
            Assert.IsNull(_events.OffMainThread());
        }

        private static int Show(AndroidNotificationSpec spec)
        {
            var allocated = new List<IntPtr>();
            try
            {
                int code = Api.BuildContent(spec, allocated, out IntPtr content, out string step);
                if (code != Api.ErrorNone) return code;
                try
                {
                    return Api.ntk_notification_show(content);
                }
                finally
                {
                    Api.ntk_notification_content_free(content);
                }
            }
            finally
            {
                AndroidNativeToolkitCApi.FreeAll(allocated);
            }
        }
    }
}
#endif
#endif
