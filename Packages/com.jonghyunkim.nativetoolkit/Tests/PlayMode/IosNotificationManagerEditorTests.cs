#nullable enable

#if UNITY_EDITOR && UNITY_IOS
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using JonghyunKim.NativeToolkit.Runtime.iOS.Common;
using JonghyunKim.NativeToolkit.Runtime.iOS.Notification;
using Api = JonghyunKim.NativeToolkit.Runtime.iOS.Notification.IosNotificationCApi;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// Editor PlayMode tests of the iOS Notification move to the C ABI 2.0.0 (artifact/ios/notification, C ABI design
    /// v3): the next-frame queue (D-3) and the completion core (D-2, D-12). The Editor makes no native call, so the
    /// core is driven directly, as the player's callbacks drive it after reading the handles.
    /// </summary>
    public sealed class IosNotificationManagerEditorTests
    {
        [TearDown]
        public void TearDown()
        {
            foreach (IosNotificationManager manager in
                     UnityEngine.Object.FindObjectsByType<IosNotificationManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                UnityEngine.Object.DestroyImmediate(manager.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator Unavailable_ResultComesOnALaterFrame_CallbackThenEvent()
        {
            IosNotificationManager manager = IosNotificationManager.Instance;
            var order = new List<string>();
            int callFrame = Time.frameCount, resultFrame = -1;
            void OnEvent(IosNotificationResult r) => order.Add("event:" + r.ErrorMessage);
            manager.NotificationOperationCompleted += OnEvent;

            manager.SetBadgeCount(1, r => { order.Add("callback:" + r.ErrorMessage); resultFrame = Time.frameCount; });
            Assert.AreEqual(0, order.Count, "nothing inside the call");
            for (int i = 0; i < 3; i++) yield return null;

            manager.NotificationOperationCompleted -= OnEvent;
            string message = IosNotificationManager.UnavailableMessage;
            CollectionAssert.AreEqual(new[] { "callback:" + message, "event:" + message }, order);
            Assert.Greater(resultFrame, callFrame);
        }

        [UnityTest]
        public IEnumerator CallingAgainFromTheCallback_WaitsForAnotherFrame()
        {
            IosNotificationManager manager = IosNotificationManager.Instance;
            var frames = new List<int>();
            void Call()
            {
                manager.SetBadgeCount(1, _ =>
                {
                    frames.Add(Time.frameCount);
                    if (frames.Count < 3) Call();
                });
            }

            Call();
            for (int i = 0; i < 10 && frames.Count < 3; i++) yield return null;

            Assert.AreEqual(3, frames.Count);
            Assert.Less(frames[0], frames[1]);
            Assert.Less(frames[1], frames[2]);
        }

        [UnityTest]
        public IEnumerator NativeResult_LinesUpBehindAWaitingDecidedOne()
        {
            IosNotificationManager manager = IosNotificationManager.Instance;
            var order = new List<string>();
            manager.SetBadgeCount(1, _ => order.Add("decided"));
            long id = IosNotificationManager.RecordCallForTests(IosNotificationManager.OperationShowNotification,
                (Action<IosNotificationResult>)(_ => order.Add("native")));
            IosNotificationManager.CompleteDone(id, Api.ErrorNone);

            for (int i = 0; i < 3; i++) yield return null;

            CollectionAssert.AreEqual(new[] { "decided", "native" }, order);
        }

        [UnityTest]
        public IEnumerator QueuedOffTheMainThread_IsDeliveredOnALaterFrame()
        {
            IosNotificationManager manager = IosNotificationManager.Instance;
            bool delivered = false;
            Task.Run(() => IosNextFrameQueue.Enqueue(() => delivered = true)).Wait();
            Assert.IsFalse(delivered);

            for (int i = 0; i < 4 && !delivered; i++) yield return null;

            Assert.IsTrue(delivered);
        }

        [UnityTest]
        public IEnumerator DispatcherDestroyedWhileAResultWaits_TheResultsStillCome()
        {
            // Design v4, D-3: a dispatcher made again gets a pump where a native result is queued.
            IosNotificationManager manager = IosNotificationManager.Instance;
            var order = new List<string>();
            manager.SetBadgeCount(1, _ => order.Add("decided"));
            GameObject? dispatcherObject = GameObject.Find("UnityMainThreadDispatcher");
            Assert.IsNotNull(dispatcherObject);
            UnityEngine.Object.DestroyImmediate(dispatcherObject);

            long id = IosNotificationManager.RecordCallForTests(IosNotificationManager.OperationShowNotification,
                (Action<IosNotificationResult>)(_ => order.Add("native")));
            IosNotificationManager.CompleteDone(id, Api.ErrorNone);
            for (int i = 0; i < 4; i++) yield return null;

            CollectionAssert.AreEqual(new[] { "decided", "native" }, order);
        }

        [UnityTest]
        public IEnumerator ACallWaitingWhenTheManagerIsDestroyed_StillGetsItsResult()
        {
            IosNotificationManager manager = IosNotificationManager.Instance;
            IosNotificationResult? result = null;
            string? list = null;
            long done = IosNotificationManager.RecordCallForTests(IosNotificationManager.OperationSetBadgeCount,
                (Action<IosNotificationResult>)(r => result = r));
            long scheduled = IosNotificationManager.RecordCallForTests(IosNotificationManager.OperationGetScheduled,
                (Action<string>)(json => list = json));
            UnityEngine.Object.DestroyImmediate(manager.gameObject);

            IosNotificationManager.CompleteDone(done, Api.ErrorSetBadgeCountFailed);
            IosNotificationManager.CompleteList(scheduled, Api.ErrorNone, new List<Api.ListItem> { new("n", "t", null, null, "", 0) });
            for (int i = 0; i < 3; i++) yield return null;

            Assert.AreEqual("Failed to set the badge count.", result?.ErrorMessage);
            Assert.AreEqual("[{\"identifier\":\"n\",\"title\":\"t\",\"categoryIdentifier\":\"\"}]", list);
        }

        [UnityTest]
        public IEnumerator Rejected_EachKindGetsItsFailureOnALaterFrame()
        {
            // Design v4, D-2: a rejection takes the call out of the table and answers it by its kind.
            IosNotificationManager manager = IosNotificationManager.Instance;
            var seen = new List<string>();
            manager.NotificationOperationCompleted += r => seen.Add("event:" + r.Operation);
            IosNotificationManager.Begin(IosNotificationManager.OperationSetBadgeCount,
                (Action<IosNotificationResult>)(r => seen.Add("badge:" + r.ErrorMessage)), _ => Api.ErrorInvalidParameter);
            IosNotificationManager.Begin(IosNotificationManager.OperationHasPermission, (Action<bool>)(h => seen.Add("has:" + h)), _ => Api.ErrorUnknown);
            IosNotificationManager.Begin(IosNotificationManager.OperationGetAuthorizationStatus,
                (Action<IosNotificationAuthorizationStatus>)(s => seen.Add("status:" + s)), _ => Api.ErrorUnknown);
            IosNotificationManager.Begin(IosNotificationManager.OperationGetDelivered, (Action<string>)(j => seen.Add("list:" + j)), _ => Api.ErrorUnknown);
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("native call failed"));
            IosNotificationManager.Begin(IosNotificationManager.OperationShowNotification,
                (Action<IosNotificationResult>)(r => seen.Add("show:" + r.ErrorMessage)), _ => throw new InvalidOperationException("native call failed"));
            Assert.AreEqual(0, seen.Count, "nothing inside the call");
            for (int i = 0; i < 3; i++) yield return null;

            CollectionAssert.AreEqual(new[]
            {
                "badge:The notification request is invalid.", "event:" + IosNotificationManager.OperationSetBadgeCount,
                "has:False", "status:Unknown", "list:[]",
                "show:An unknown error occurred.", "event:" + IosNotificationManager.OperationShowNotification,
            }, seen);
        }

        [UnityTest]
        public IEnumerator Accepted_ACompletionBeforeTheCallReturnsComesOnce()
        {
            _ = IosNotificationManager.Instance;
            int count = 0;
            IosNotificationManager.Begin(IosNotificationManager.OperationSetBadgeCount, (Action<IosNotificationResult>)(_ => count++), id =>
            {
                IosNotificationManager.CompleteDone(id, Api.ErrorNone); // as a call made off the main thread may
                return Api.ErrorNone;
            });
            for (int i = 0; i < 3; i++) yield return null;
            Assert.AreEqual(1, count);
        }

        [UnityTest]
        public IEnumerator ACallbackThatThrows_StillLetsTheEventCome()
        {
            IosNotificationManager manager = IosNotificationManager.Instance;
            bool eventCame = false;
            manager.NotificationOperationCompleted += _ => eventCame = true;
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("callback: boom"));
            manager.SetBadgeCount(1, _ => throw new InvalidOperationException("boom"));
            for (int i = 0; i < 3; i++) yield return null;
            Assert.IsTrue(eventCame, "design v4, D-3: separate try blocks");
        }

        [UnityTest]
        public IEnumerator PermissionAndStatus_AreMapped()
        {
            _ = IosNotificationManager.Instance;
            IosNotificationResult? denied = null;
            bool? has = null;
            IosNotificationAuthorizationStatus? status = null;
            long a = IosNotificationManager.RecordCallForTests(IosNotificationManager.OperationRequestPermission,
                (Action<IosNotificationResult>)(r => denied = r));
            long b = IosNotificationManager.RecordCallForTests(IosNotificationManager.OperationHasPermission, (Action<bool>)(h => has = h));
            long c = IosNotificationManager.RecordCallForTests(IosNotificationManager.OperationGetAuthorizationStatus,
                (Action<IosNotificationAuthorizationStatus>)(s => status = s));
            IosNotificationManager.CompletePermission(a, Api.ErrorNone, 0);
            IosNotificationManager.CompletePermission(b, Api.ErrorNone, 1);
            IosNotificationManager.CompleteStatus(c, Api.ErrorNone, 3);
            for (int i = 0; i < 3; i++) yield return null;

            Assert.AreEqual("Notification permission was denied.", denied?.ErrorMessage);
            Assert.AreEqual(true, has);
            Assert.AreEqual(IosNotificationAuthorizationStatus.Provisional, status);
        }

        [UnityTest]
        public IEnumerator Interaction_ReachesTheEvents_AndIsDroppedWithoutAManager()
        {
            IosNotificationManager manager = IosNotificationManager.Instance;
            var seen = new List<string>();
            manager.NotificationActionReceived += a => seen.Add($"action:{a.NotificationId}:{a.ActionId}:{a.UserInfo?["k"]}");
            manager.NotificationTextInputActionReceived += t => seen.Add($"text:{t.UserText}");

            IosNotificationManager.DeliverInteraction(Api.EventBodyTap, "n", "com.apple.UNNotificationDefaultActionIdentifier", null, "{\"k\":\"v\"}");
            IosNotificationManager.DeliverInteraction(Api.EventTextInput, "n", "reply", "hi", "{}");
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("unknown kind"));
            IosNotificationManager.DeliverInteraction(9, "n", "x", null, null);
            for (int i = 0; i < 3; i++) yield return null;

            CollectionAssert.AreEqual(new[] { "action:n:com.apple.UNNotificationDefaultActionIdentifier:v", "text:hi" }, seen);

            UnityEngine.Object.DestroyImmediate(manager.gameObject);
            IosNotificationManager.DeliverInteraction(Api.EventAction, "n", "open", null, null);
            for (int i = 0; i < 3; i++) yield return null;
            Assert.AreEqual(2, seen.Count, "no manager, no event");
        }
    }
}
#endif
