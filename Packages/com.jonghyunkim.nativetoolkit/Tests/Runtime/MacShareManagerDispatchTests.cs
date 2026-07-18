#nullable enable

#if UNITY_STANDALONE_OSX || UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.TestTools;
using JonghyunKim.NativeToolkit.Runtime.Share;

namespace JonghyunKim.NativeToolkit.Tests
{
    public sealed class MacShareManagerDispatchTests
    {
        [Test]
        public void InvokeInOrder_CommonThenPerCall_InvokedOnceEachInOrder()
        {
            var order = new List<string>();
            var result = MacShareResult.Success("share", completed: true, serviceName: "Mail");

            MacShareManager.InvokeInOrder(
                result,
                common: r => order.Add("common"),
                perCall: r => order.Add("perCall"));

            Assert.AreEqual(new[] { "common", "perCall" }, order.ToArray());
        }

        [Test]
        public void InvokeInOrder_PassesResultToBothDelegates()
        {
            var result = MacShareResult.Failure("shareViaService", "No shareable items were provided.");
            MacShareResult? receivedByCommon = null;
            MacShareResult? receivedByPerCall = null;

            MacShareManager.InvokeInOrder(
                result,
                common: r => receivedByCommon = r,
                perCall: r => receivedByPerCall = r);

            Assert.AreEqual(result.ErrorMessage, receivedByCommon?.ErrorMessage);
            Assert.AreEqual(result.ErrorMessage, receivedByPerCall?.ErrorMessage);
        }

        [Test]
        public void InvokeInOrder_OnlyPerCall_DoesNotThrow()
        {
            var result = MacShareResult.Success("share", completed: false, serviceName: null);
            bool invoked = false;

            Assert.DoesNotThrow(() => MacShareManager.InvokeInOrder(result, common: null, perCall: r => invoked = true));
            Assert.IsTrue(invoked);
        }

        [Test]
        public void InvokeInOrder_OnlyCommon_DoesNotThrow()
        {
            var result = MacShareResult.Success("share", completed: false, serviceName: null);
            bool invoked = false;

            Assert.DoesNotThrow(() => MacShareManager.InvokeInOrder(result, common: r => invoked = true, perCall: null));
            Assert.IsTrue(invoked);
        }

        [Test]
        public void InvokeInOrder_BothNull_DoesNotThrow()
        {
            var result = MacShareResult.Success("share", completed: false, serviceName: null);

            Assert.DoesNotThrow(() => MacShareManager.InvokeInOrder(result, common: null, perCall: null));
        }

        [Test]
        public void InvokeInOrder_PerCallThrows_CommonAlreadyInvokedAndExceptionSwallowed()
        {
            var result = MacShareResult.Success("share", completed: true, serviceName: null);
            bool commonInvoked = false;

            LogAssert.Expect(UnityEngine.LogType.Error, new System.Text.RegularExpressions.Regex(".*InvokeInOrder.*"));

            Assert.DoesNotThrow(() => MacShareManager.InvokeInOrder(
                result,
                common: r => commonInvoked = true,
                perCall: r => throw new System.InvalidOperationException("boom")));

            Assert.IsTrue(commonInvoked, "Common event must have already fired before the per-call callback threw.");
        }
    }
}
#endif
