#nullable enable

#if UNITY_STANDALONE_OSX || UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.TestTools;
using JonghyunKim.NativeToolkit.Runtime.Share;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// PlayMode integration tests exercising <c>MacShareManager.Share</c> /
    /// <c>MacShareManager.ShareViaService</c> end-to-end through
    /// <c>UnityMainThreadDispatcher</c> (which requires an actual running player loop / Play Mode
    /// to flush its queue via <c>Update</c>; EditMode tests cannot drive this path).
    /// </summary>
    public sealed class MacShareManagerIntegrationTests
    {
        [UnityTest]
        public IEnumerator Share_NullPayload_FiresBothCallbacksWithNoShareableItemsFailure()
        {
            var received = new List<MacShareResult>();
            void OnCompleted(MacShareResult r) => received.Add(r);
            MacShareManager.Instance.ShareCompleted += OnCompleted;

            MacShareResult? perCallResult = null;
            MacShareManager.Instance.Share(null, r => perCallResult = r);

            yield return null;

            MacShareManager.Instance.ShareCompleted -= OnCompleted;

            Assert.AreEqual(1, received.Count, "ShareCompleted must fire exactly once.");
            Assert.IsFalse(received[0].IsSuccess);
            Assert.AreEqual("No shareable items were provided.", received[0].ErrorMessage);
            Assert.IsNotNull(perCallResult);
            Assert.AreEqual("No shareable items were provided.", perCallResult!.Value.ErrorMessage);
        }

        [UnityTest]
        public IEnumerator Share_EmptyItems_FiresNoShareableItemsFailure()
        {
            MacShareResult? perCallResult = null;
            var payload = new MacShareContentPayload { items = new MacShareItem[0] };

            MacShareManager.Instance.Share(payload, r => perCallResult = r);

            yield return null;

            Assert.IsNotNull(perCallResult);
            Assert.IsFalse(perCallResult!.Value.IsSuccess);
            Assert.AreEqual("No shareable items were provided.", perCallResult.Value.ErrorMessage);
        }

        [UnityTest]
        public IEnumerator Share_NonMacPlatformOrEditor_FiresMacOnlyFailure()
        {
            MacShareResult? perCallResult = null;
            var payload = new MacShareContentPayload { items = new[] { MacShareItem.Text("hello") } };

            MacShareManager.Instance.Share(payload, r => perCallResult = r);

            yield return null;

            // In the Editor (any build target) or on non-macOS runtime platforms, native P/Invoke
            // is never reached; the manager must still resolve exactly one result.
            Assert.IsNotNull(perCallResult);
            Assert.IsFalse(perCallResult!.Value.IsSuccess);
            Assert.AreEqual("macOS share is only available on a macOS Standalone player.", perCallResult.Value.ErrorMessage);
        }

        [UnityTest]
        public IEnumerator ShareViaService_EmptyServiceName_FiresServiceNameFailure()
        {
            MacShareResult? perCallResult = null;
            var payload = new MacShareContentPayload { items = new[] { MacShareItem.Text("hello") } };

            MacShareManager.Instance.ShareViaService(string.Empty, payload, r => perCallResult = r);

            yield return null;

            Assert.IsNotNull(perCallResult);
            Assert.IsFalse(perCallResult!.Value.IsSuccess);
            Assert.AreEqual("Sharing service name must not be empty.", perCallResult.Value.ErrorMessage);
        }

        [UnityTest]
        public IEnumerator ShareViaService_NullPayload_FiresNoShareableItemsFailure()
        {
            MacShareResult? perCallResult = null;

            MacShareManager.Instance.ShareViaService(MacShareServiceNames.MailCompose, null, r => perCallResult = r);

            yield return null;

            Assert.IsNotNull(perCallResult);
            Assert.IsFalse(perCallResult!.Value.IsSuccess);
            Assert.AreEqual("No shareable items were provided.", perCallResult.Value.ErrorMessage);
        }

        [UnityTest]
        public IEnumerator ShareViaService_NonMacPlatformOrEditor_FiresMacOnlyFailure()
        {
            MacShareResult? perCallResult = null;
            var payload = new MacShareContentPayload { items = new[] { MacShareItem.Text("hello") } };

            MacShareManager.Instance.ShareViaService(MacShareServiceNames.MailCompose, payload, r => perCallResult = r);

            yield return null;

            Assert.IsNotNull(perCallResult);
            Assert.IsFalse(perCallResult!.Value.IsSuccess);
            Assert.AreEqual("macOS share is only available on a macOS Standalone player.", perCallResult.Value.ErrorMessage);
        }

        [UnityTest]
        public IEnumerator Share_DispatchOrder_CommonEventFiresBeforePerCallCallback()
        {
            var order = new List<string>();
            void OnCompleted(MacShareResult r) => order.Add("common");
            MacShareManager.Instance.ShareCompleted += OnCompleted;

            MacShareManager.Instance.Share(null, _ => order.Add("perCall"));

            yield return null;

            MacShareManager.Instance.ShareCompleted -= OnCompleted;

            Assert.AreEqual(new[] { "common", "perCall" }, order.ToArray());
        }

        [UnityTest]
        public IEnumerator ShareAndShareViaService_Consecutive_EachCallbackFiresIndependently()
        {
            // Share and ShareViaService use independent per-call fields (s_onShare /
            // s_onShareViaService), so calling both concurrently must not cross-overwrite either
            // callback (unlike same-operation last-registered-wins semantics).
            bool shareCallbackInvoked = false;
            bool shareViaServiceCallbackInvoked = false;
            var payload = new MacShareContentPayload { items = new[] { MacShareItem.Text("hello") } };

            MacShareManager.Instance.Share(payload, _ => shareCallbackInvoked = true);
            MacShareManager.Instance.ShareViaService(MacShareServiceNames.MailCompose, payload, _ => shareViaServiceCallbackInvoked = true);

            yield return null;

            Assert.IsTrue(shareCallbackInvoked);
            Assert.IsTrue(shareViaServiceCallbackInvoked);
        }
    }
}
#endif
