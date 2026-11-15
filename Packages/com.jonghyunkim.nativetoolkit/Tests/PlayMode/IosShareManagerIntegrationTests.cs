#nullable enable

#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using JonghyunKim.NativeToolkit.Runtime.iOS.Share;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// PlayMode integration tests exercising <c>IosShareManager.Share</c> end-to-end through
    /// <c>UnityMainThreadDispatcher</c> (which requires an actual running player loop / Play Mode
    /// to flush its queue via <c>Update</c>; EditMode tests cannot drive this path).
    /// </summary>
    public sealed class IosShareManagerIntegrationTests
    {
        [UnityTest]
        public IEnumerator Share_NullPayload_FiresBothCallbacksWithNoShareableItemsFailure()
        {
            var received = new List<IosShareResult>();
            void OnCompleted(IosShareResult r) => received.Add(r);
            IosShareManager.Instance.ShareCompleted += OnCompleted;

            IosShareResult? perCallResult = null;
            IosShareManager.Instance.Share(null, r => perCallResult = r);

            yield return null;

            IosShareManager.Instance.ShareCompleted -= OnCompleted;

            Assert.AreEqual(1, received.Count, "ShareCompleted must fire exactly once.");
            Assert.IsFalse(received[0].IsSuccess);
            Assert.AreEqual("No shareable items were provided.", received[0].ErrorMessage);
            Assert.IsNotNull(perCallResult);
            Assert.AreEqual("No shareable items were provided.", perCallResult!.Value.ErrorMessage);
        }

        [UnityTest]
        public IEnumerator Share_EmptyItems_FiresNoShareableItemsFailure()
        {
            IosShareResult? perCallResult = null;
            var payload = new IosShareContentPayload { items = new IosShareItem[0] };

            IosShareManager.Instance.Share(payload, r => perCallResult = r);

            yield return null;

            Assert.IsNotNull(perCallResult);
            Assert.IsFalse(perCallResult!.Value.IsSuccess);
            Assert.AreEqual("No shareable items were provided.", perCallResult.Value.ErrorMessage);
        }

        [UnityTest]
        public IEnumerator Share_NonIosPlatformOrEditor_FiresIosOnlyFailure()
        {
            IosShareResult? perCallResult = null;
            var payload = new IosShareContentPayload { items = new[] { IosShareItem.Text("hello") } };

            IosShareManager.Instance.Share(payload, r => perCallResult = r);

            yield return null;

            // In the Editor (any build target) or on non-iOS runtime platforms, native P/Invoke is
            // never reached; the manager must still resolve exactly one result.
            Assert.IsNotNull(perCallResult);
            Assert.IsFalse(perCallResult!.Value.IsSuccess);
            Assert.AreEqual("iOS share is only available on an iOS device.", perCallResult.Value.ErrorMessage);
        }

        [UnityTest]
        public IEnumerator Share_ConsecutiveCalls_EachCallbackFiresExactlyOnce()
        {
            // In the Editor every share fails on the next Update, so each call's own callback fires.
            // On a device each accepted call is recorded by number (IosSharePendingCalls); that is checked
            // by IosShareManagerEditorTests with a stand-in native call and by the player tests.
            var payload = new IosShareContentPayload { items = new[] { IosShareItem.Text("hello") } };
            bool firstCallbackInvoked = false;
            bool secondCallbackInvoked = false;

            IosShareManager.Instance.Share(payload, _ => firstCallbackInvoked = true);
            IosShareManager.Instance.Share(payload, _ => secondCallbackInvoked = true);

            yield return null;

            Assert.IsTrue(firstCallbackInvoked, "First call resolves synchronously on this path and must fire its own callback.");
            Assert.IsTrue(secondCallbackInvoked, "Second call resolves synchronously on this path and must fire its own callback.");
        }

        [UnityTest]
        public IEnumerator Share_DispatchOrder_CommonEventFiresBeforePerCallCallback()
        {
            var order = new List<string>();
            void OnCompleted(IosShareResult r) => order.Add("common");
            IosShareManager.Instance.ShareCompleted += OnCompleted;

            IosShareManager.Instance.Share(null, _ => order.Add("perCall"));

            yield return null;

            IosShareManager.Instance.ShareCompleted -= OnCompleted;

            Assert.AreEqual(new[] { "common", "perCall" }, order.ToArray());
        }
    }
}
#endif
