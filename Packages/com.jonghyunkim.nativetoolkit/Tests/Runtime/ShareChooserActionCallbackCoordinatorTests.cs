#nullable enable

using System;
using System.Collections.Generic;
using NUnit.Framework;
using JonghyunKim.NativeToolkit.Runtime.Share;

namespace JonghyunKim.NativeToolkit.Tests
{
    public sealed class ShareChooserActionCallbackCoordinatorTests
    {
        private static ShareChooserActionCallbackCoordinator CreateSync()
            => new ShareChooserActionCallbackCoordinator(a => a());

        // (1) Fire dispatches global event before per-call callback
        [Test]
        public void Fire_GlobalEventFiredBeforePerCallCallback()
        {
            var coordinator = CreateSync();
            var order = new List<string>();

            coordinator.ChooserActionTapped += _ => order.Add("event");
            coordinator.Register(_ => order.Add("callback"));

            coordinator.Fire(new ShareChooserActionResult("action.test"));

            Assert.AreEqual(new[] { "event", "callback" }, order);
        }

        // (2) Fire dispatches global event even when no per-call callback is registered
        [Test]
        public void Fire_NoPerCallCallback_GlobalEventStillFires()
        {
            var coordinator = CreateSync();
            var fired = false;

            coordinator.ChooserActionTapped += _ => fired = true;

            coordinator.Fire(new ShareChooserActionResult("action.test"));

            Assert.IsTrue(fired);
        }

        // (3) Register enforces last-registered wins — only the latest per-call callback fires
        [Test]
        public void Register_LastRegisteredWins_OnlyLatestCallbackFires()
        {
            var coordinator = CreateSync();
            var firstFired = false;
            var secondFired = false;

            coordinator.Register(_ => firstFired = true);
            coordinator.Register(_ => secondFired = true);

            coordinator.Fire(new ShareChooserActionResult("action.test"));

            Assert.IsFalse(firstFired, "First callback must not fire after being replaced.");
            Assert.IsTrue(secondFired);
        }

        // (4) Register(null) clears the previous per-call callback
        [Test]
        public void Register_Null_ClearsPreviousCallback()
        {
            var coordinator = CreateSync();
            var fired = false;

            coordinator.Register(_ => fired = true);
            coordinator.Register(null); // clears

            coordinator.Fire(new ShareChooserActionResult("action.test"));

            Assert.IsFalse(fired);
        }

        // (5) Per-call callback is NOT cleared after firing — multiple taps invoke it each time
        [Test]
        public void Fire_MultipleTimes_PerCallCallbackInvokedEachTime()
        {
            var coordinator = CreateSync();
            var count = 0;

            coordinator.Register(_ => count++);

            coordinator.Fire(new ShareChooserActionResult("action.a"));
            coordinator.Fire(new ShareChooserActionResult("action.b"));
            coordinator.Fire(new ShareChooserActionResult("action.c"));

            Assert.AreEqual(3, count);
        }

        // (6) Exception in global event subscriber does not prevent per-call callback from firing
        [Test]
        public void Fire_GlobalEventThrows_PerCallCallbackStillInvoked()
        {
            var coordinator = CreateSync();
            var callbackFired = false;

            coordinator.ChooserActionTapped += _ => throw new InvalidOperationException("subscriber error");
            coordinator.Register(_ => callbackFired = true);

            Assert.DoesNotThrow(() => coordinator.Fire(new ShareChooserActionResult("action.test")));
            Assert.IsTrue(callbackFired);
        }

        // Clear removes per-call callback so subsequent Fire does not invoke it
        [Test]
        public void Clear_SubsequentFireDoesNotInvokeCallback()
        {
            var coordinator = CreateSync();
            var fired = false;

            coordinator.Register(_ => fired = true);
            coordinator.Clear();

            coordinator.Fire(new ShareChooserActionResult("action.test"));

            Assert.IsFalse(fired);
        }

        // Fire passes the result ActionId to subscribers unchanged
        [Test]
        public void Fire_PassesResultToSubscribers()
        {
            var coordinator = CreateSync();
            ShareChooserActionResult? received = null;

            coordinator.Register(r => received = r);

            coordinator.Fire(new ShareChooserActionResult("com.example.action"));

            Assert.IsNotNull(received);
            Assert.AreEqual("com.example.action", received!.Value.ActionId);
        }
    }
}
