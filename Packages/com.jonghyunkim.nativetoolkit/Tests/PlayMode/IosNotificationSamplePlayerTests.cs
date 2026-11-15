#nullable enable

// iOS player only: the sample scene and the UI driver.
#if UNITY_IOS && !UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using JonghyunKim.NativeToolkit.Runtime.iOS.Notification;
using NUnit.Framework;
using UnityEngine.TestTools;
using static JonghyunKim.NativeToolkit.Tests.IosSampleScreenDriver;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// The iOS Notification sample screen (artifact/topics/ios-c-abi-2, UI driver design v3, 4.5). The permission
    /// can be asked once per install, so these tests run in their own player run on a fresh install
    /// (scripts/verify_unity_ios.sh: category <see cref="IosUiDriver.NotificationSampleCategory"/> only, not on
    /// the Test iOS Profile), in order: before the permission, the one "Allow", then what needs it. "Don't Allow"
    /// is the harness's (NU-2). The location buttons are not pressed (V-7: they may ask for the location).
    /// An operation with a result is checked by the Manager's <see cref="IosNotificationManager.NotificationOperationCompleted"/>
    /// (one success of that operation after the press): the sample's callback writes its own text and the event,
    /// in the same frame, overwrites it with "✓ &lt;operation&gt;", which is what the screen then shows.
    /// </summary>
    [Category(IosUiDriver.NotificationSampleCategory)]
    [Category(IosUiDriver.Category)]
    [Category(IosUiDriver.SampleScreenCategory)]
    public sealed class IosNotificationSamplePlayerTests
    {
        private const string ResultLabel = "ResultTextBlock";
        private const string PermissionRequired = "Please allow notification permission first.";

        private readonly List<IosNotificationResult> _completed = new();

        [SetUp]
        public void SetUp()
        {
            _completed.Clear();
            IosNotificationManager.Instance.NotificationOperationCompleted += OnCompleted;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            IosNotificationManager.Instance.NotificationOperationCompleted -= OnCompleted;
            yield return IosUiDriver.WaitForPending();
            yield return Unload();
        }

        private void OnCompleted(IosNotificationResult result) => _completed.Add(result);

        [UnityTest, Order(1)]
        public IEnumerator NS01_BeforeThePermission()
        {
            yield return OpenScreen();
            yield return PressAndExpect("AuthorizationStatusButton", "AuthorizationStatus: NotDetermined");
            yield return PressAndExpect("HasPermissionButton", "HasPermission: False");
            yield return PressAndExpect("ShowImmediateButton", $"ShowImmediate: {PermissionRequired}");
        }

        [UnityTest, Order(2), Timeout(IosUiDriverProtocol.ScreenTestTimeoutMilliseconds)]
        public IEnumerator NS02_RequestPermission_Allow()
        {
            yield return OpenScreen();
            yield return IosUiDriver.Send("permission-answer", "allow");
            int request = IosUiDriver.LastSent;
            int before = _completed.Count;
            Press("RequestPermissionButton");
            yield return IosUiDriver.Wait(request, IosUiDriverProtocol.ScreenAnswerTimeoutSeconds);
            yield return WaitForSuccess(IosNotificationManager.OperationRequestPermission, before);
        }

        [UnityTest, Order(3)]
        public IEnumerator NS03_StatusAfterAllow()
        {
            yield return OpenScreen();
            yield return PressAndExpect("AuthorizationStatusButton", "AuthorizationStatus: Authorized");
            yield return PressAndExpect("HasPermissionButton", "HasPermission: True");
        }

        [UnityTest, Order(4)]
        public IEnumerator NS04_ShowAndRemoveDelivered()
        {
            yield return OpenScreen();
            yield return PressAndExpectSuccess("ShowImmediateButton", IosNotificationManager.OperationShowNotification);
            yield return PressAndExpectPrefix("GetDeliveredButton", "GetDelivered:\n");
            yield return PressAndExpect("RemoveAllDeliveredButton", "RemoveAllDelivered: requested");
        }

        [UnityTest, Order(5)]
        public IEnumerator NS05_ScheduleAndCancel()
        {
            yield return OpenScreen();
            yield return PressAndExpectSuccess("ScheduleTimeIntervalButton", IosNotificationManager.OperationScheduleNotification);
            yield return PressAndExpectPrefix("GetScheduledButton", "GetScheduled:\n");
            yield return PressAndExpect("CancelAllScheduledButton", "CancelAllScheduled: requested");
        }

        [UnityTest, Order(6)]
        public IEnumerator NS06_BadgeAndCategory()
        {
            yield return OpenScreen();
            yield return PressAndExpectSuccess("SetBadgeCount1Button", IosNotificationManager.OperationSetBadgeCount);
            yield return PressAndExpectSuccess("SetBadgeCount0Button", IosNotificationManager.OperationSetBadgeCount);
            yield return PressAndExpectPrefix("RegisterCategoryButton", "✓ RegisterCategory\n");
            yield return PressAndExpectPrefix("RemoveCategoryButton", "RemoveCategory (");
        }

        [UnityTest, Order(7), Timeout(IosUiDriverProtocol.ScreenTestTimeoutMilliseconds)]
        public IEnumerator NS07_OpenNotificationSettings_ComesBack()
        {
            yield return OpenScreen();
            yield return IosUiDriver.Send("settings-close");
            int request = IosUiDriver.LastSent;
            Press("OpenNotificationSettingsButton");
            yield return IosUiDriver.Wait(request, IosUiDriverProtocol.ScreenAnswerTimeoutSeconds);
            Assert.AreEqual("opened", IosUiDriver.LastValue, "the Settings app did not come to the front");
            yield return PressAndExpect("CancelAllButton", "CancelAll: requested");
        }

        private static IEnumerator OpenScreen()
        {
            yield return LoadAtTopMenu(TopMenuNotification);
            yield return Open(TopMenuNotification, "RequestPermissionButton");
        }

        /// <summary>Presses a button and waits for one more success of the operation, then the screen's "✓ op".</summary>
        private IEnumerator PressAndExpectSuccess(string button, string operation)
        {
            int before = _completed.Count;
            Press(button);
            yield return WaitForSuccess(operation, before);
        }

        private IEnumerator WaitForSuccess(string operation, int before)
        {
            yield return WaitFor(() => _completed.Count > before, $"the result of {operation}");
            IosNotificationResult result = _completed[before];
            Assert.AreEqual(operation, result.Operation);
            Assert.IsTrue(result.IsSuccess, $"{operation} failed: {result.ErrorMessage}");
            yield return WaitForResult($"✓ {operation}");
        }

        private static IEnumerator PressAndExpect(string button, string expected)
        {
            Press(button);
            yield return WaitForResult(expected);
        }

        private static IEnumerator PressAndExpectPrefix(string button, string prefix)
        {
            Press(button);
            yield return WaitFor(() => LabelText(ResultLabel)?.StartsWith(prefix, StringComparison.Ordinal) == true,
                $"a result starting with \"{prefix.Replace("\n", " / ")}\" after {button}");
        }

        private static IEnumerator WaitForResult(string expected) =>
            WaitFor(() => LabelText(ResultLabel) == expected, $"the result \"{expected.Replace("\n", " / ")}\"");
    }
}
#endif
