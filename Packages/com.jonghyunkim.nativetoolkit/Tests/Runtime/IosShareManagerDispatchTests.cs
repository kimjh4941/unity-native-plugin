#nullable enable

#if UNITY_IOS || UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.TestTools;
using JonghyunKim.NativeToolkit.Runtime.Share;

namespace JonghyunKim.NativeToolkit.Tests
{
    public sealed class IosShareManagerDispatchTests
    {
        [Test]
        public void InvokeInOrder_CommonThenPerCall_InvokedOnceEachInOrder()
        {
            var order = new List<string>();
            var result = IosShareResult.Success(completed: true, activityType: "com.apple.UIKit.activity.Mail");

            IosShareManager.InvokeInOrder(
                result,
                common: r => order.Add("common"),
                perCall: r => order.Add("perCall"));

            Assert.AreEqual(new[] { "common", "perCall" }, order.ToArray());
        }

        [Test]
        public void InvokeInOrder_PassesResultToBothDelegates()
        {
            var result = IosShareResult.Failure("No shareable items were provided.");
            IosShareResult? receivedByCommon = null;
            IosShareResult? receivedByPerCall = null;

            IosShareManager.InvokeInOrder(
                result,
                common: r => receivedByCommon = r,
                perCall: r => receivedByPerCall = r);

            Assert.AreEqual(result.ErrorMessage, receivedByCommon?.ErrorMessage);
            Assert.AreEqual(result.ErrorMessage, receivedByPerCall?.ErrorMessage);
        }

        [Test]
        public void InvokeInOrder_OnlyPerCall_DoesNotThrow()
        {
            var result = IosShareResult.Success(completed: false, activityType: null);
            bool invoked = false;

            Assert.DoesNotThrow(() => IosShareManager.InvokeInOrder(result, common: null, perCall: r => invoked = true));
            Assert.IsTrue(invoked);
        }

        [Test]
        public void InvokeInOrder_OnlyCommon_DoesNotThrow()
        {
            var result = IosShareResult.Success(completed: false, activityType: null);
            bool invoked = false;

            Assert.DoesNotThrow(() => IosShareManager.InvokeInOrder(result, common: r => invoked = true, perCall: null));
            Assert.IsTrue(invoked);
        }

        [Test]
        public void InvokeInOrder_BothNull_DoesNotThrow()
        {
            var result = IosShareResult.Success(completed: false, activityType: null);

            Assert.DoesNotThrow(() => IosShareManager.InvokeInOrder(result, common: null, perCall: null));
        }

        [Test]
        public void InvokeInOrder_PerCallThrows_CommonAlreadyInvokedAndExceptionSwallowed()
        {
            var result = IosShareResult.Success(completed: true, activityType: null);
            bool commonInvoked = false;

            LogAssert.Expect(UnityEngine.LogType.Error, new System.Text.RegularExpressions.Regex(".*InvokeInOrder.*"));

            Assert.DoesNotThrow(() => IosShareManager.InvokeInOrder(
                result,
                common: r => commonInvoked = true,
                perCall: r => throw new System.InvalidOperationException("boom")));

            Assert.IsTrue(commonInvoked, "Common event must have already fired before the per-call callback threw.");
        }
    }
}
#endif
