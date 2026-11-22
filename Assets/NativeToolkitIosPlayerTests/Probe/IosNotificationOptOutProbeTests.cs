#nullable enable

#if UNITY_IOS && !UNITY_EDITOR && NTK_IOS_PLAYER_PROBE && NATIVETOOLKIT_IOS_SKIP_NOTIFICATION_INSTALL
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using AOT;
using JonghyunKim.NativeToolkit.Runtime.iOS.Notification;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace JonghyunKim.NativeToolkit.IosPlayerTests
{
    /// <summary>
    /// iOS player built with NATIVETOOLKIT_IOS_SKIP_NOTIFICATION_INSTALL (the "Test iOS SkipInstall Profile",
    /// scripts/verify_unity_ios.sh --skip-install): no delegate at launch, and Initialize() installs it
    /// (artifact/ios/notification/designs/2026-11-21-ios-notification-optout-check-design-v2.md, OO-2 to OO-4).
    /// The install cannot be undone in the process, so the steps run in order in one test.
    /// </summary>
    [Category("IosNotificationOptOut")]
    public class IosNotificationOptOutProbeTests
    {
        private const string Proxy = "IosLibrary.NotificationDelegateProxy";
        // Notification.h: NTK_NOTIFICATION_ERROR_NONE = 0, NTK_NOTIFICATION_ERROR_NOT_INITIALIZED = 2.
        private const int ErrorNone = 0;
        private const int ErrorNotInitialized = 2;
        private const string NotRegistered = "the interaction listener was not registered";

        private delegate void InteractionCallback(IntPtr userData, IntPtr interaction);

        // Kept in a static field: the C ABI holds the pointer for as long as the listener is registered.
        private static readonly InteractionCallback s_callback = OnInteraction;
        private static readonly IntPtr s_callbackPointer = Marshal.GetFunctionPointerForDelegate(s_callback);

        [DllImport("__Internal")] private static extern string? ntkpt_notification_delegate_class();
        // Notification.h: ntk_notification_add_interaction_listener(callback, user_data, release, out_listener);
        // a NULL callback is INVALID_PARAMETER, checked before the install.
        [DllImport("__Internal")] private static extern int ntk_notification_add_interaction_listener(IntPtr callback, IntPtr userData, IntPtr release, out IntPtr listener);
        // Notification.h: void ntk_notification_listener_remove(ntk_notification_listener* listener).
        [DllImport("__Internal")] private static extern void ntk_notification_listener_remove(IntPtr listener);

        [MonoPInvokeCallback(typeof(InteractionCallback))]
        private static void OnInteraction(IntPtr userData, IntPtr interaction) =>
            Debug.LogWarning("[NTK-IOS-PLAYER] optout: an interaction reached the test's listener");

        [Test]
        public void OO_InitializeInstallsTheDelegate()
        {
            // OO-2: nothing installed the delegate at launch. Creating the manager tries its listener and warns.
            Assert.AreNotEqual(Proxy, Report("launch"), "the delegate was installed at launch although the define is set");
            LogAssert.Expect(LogType.Warning, new Regex(Regex.Escape(NotRegistered)));
            IosNotificationManager manager = IosNotificationManager.Instance;
            Assert.AreEqual(ErrorNotInitialized, AddAndRemove(), "a listener was accepted before the delegate was installed");

            // OO-3: Initialize() installs the delegate and registers the manager's own listener (no warning).
            List<string> warnings = WarningsDuring(manager.Initialize);
            Assert.IsFalse(warnings.Exists(w => w.Contains(NotRegistered)), $"Initialize() did not register the manager's listener: {string.Join(" | ", warnings)}");
            Assert.AreEqual(Proxy, Report("initialize"));
            Assert.AreEqual(ErrorNone, AddAndRemove(), "a listener was refused after Initialize()");

            // OO-4: a second Initialize() keeps the proxy (Initialize() logs an error, which fails the test, if
            // the install fails).
            manager.Initialize();
            Assert.AreEqual(Proxy, Report("initialize again"));
        }

        private static string? Report(string step)
        {
            string? name = ntkpt_notification_delegate_class();
            Debug.Log($"[NTK-IOS-PLAYER] optout {step} delegate={name ?? "<none>"}");
            return name;
        }

        /// <summary>Registers a listener from the test and removes it at once; returns the add's error.</summary>
        private static int AddAndRemove()
        {
            int error = ntk_notification_add_interaction_listener(s_callbackPointer, IntPtr.Zero, IntPtr.Zero, out IntPtr listener);
            Debug.Log($"[NTK-IOS-PLAYER] optout listener={error}");
            if (listener != IntPtr.Zero) ntk_notification_listener_remove(listener);
            return error;
        }

        private static List<string> WarningsDuring(Action action)
        {
            var warnings = new List<string>();
            void Collect(string message, string stackTrace, LogType type)
            {
                if (type == LogType.Warning) warnings.Add(message);
            }
            Application.logMessageReceived += Collect;
            try
            {
                action();
            }
            finally
            {
                Application.logMessageReceived -= Collect;
            }
            return warnings;
        }
    }
}
#endif
