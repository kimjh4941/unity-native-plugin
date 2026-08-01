#nullable enable

#if UNITY_ANDROID
using NUnit.Framework;
using UnityEngine.TestTools;
using JonghyunKim.NativeToolkit.Runtime.Clipboard;

namespace JonghyunKim.NativeToolkit.Tests
{
    public sealed class AndroidClipboardManagerDispatchTests
    {
        [Test]
        public void InvokeInOrder_CommonThenPerCall_InvokedOnceEachInOrder()
        {
            var order = new System.Collections.Generic.List<string>();
            var result = ClipboardOperationResult.Success("copyPlainText");

            AndroidClipboardManager.InvokeInOrder(
                result,
                common: r => order.Add("common"),
                perCall: r => order.Add("perCall"));

            Assert.AreEqual(new[] { "common", "perCall" }, order.ToArray());
        }

        [Test]
        public void InvokeInOrder_PassesResultToBothDelegates()
        {
            var result = ClipboardOperationResult.Failure("clear", "Clipboard service is unavailable.");
            ClipboardOperationResult? receivedByCommon = null;
            ClipboardOperationResult? receivedByPerCall = null;

            AndroidClipboardManager.InvokeInOrder(
                result,
                common: r => receivedByCommon = r,
                perCall: r => receivedByPerCall = r);

            Assert.AreEqual(result.ErrorMessage, receivedByCommon?.ErrorMessage);
            Assert.AreEqual(result.ErrorMessage, receivedByPerCall?.ErrorMessage);
        }

        [Test]
        public void InvokeInOrder_OnlyPerCall_DoesNotThrow()
        {
            var result = ClipboardOperationResult.Success("copyPlainText");
            bool invoked = false;

            Assert.DoesNotThrow(() => AndroidClipboardManager.InvokeInOrder(result, common: null, perCall: r => invoked = true));
            Assert.IsTrue(invoked);
        }

        [Test]
        public void InvokeInOrder_OnlyCommon_DoesNotThrow()
        {
            var result = ClipboardOperationResult.Success("copyPlainText");
            bool invoked = false;

            Assert.DoesNotThrow(() => AndroidClipboardManager.InvokeInOrder(result, common: r => invoked = true, perCall: null));
            Assert.IsTrue(invoked);
        }

        [Test]
        public void InvokeInOrder_BothNull_DoesNotThrow()
        {
            var result = ClipboardOperationResult.Success("copyPlainText");

            Assert.DoesNotThrow(() => AndroidClipboardManager.InvokeInOrder(result, common: null, perCall: null));
        }

        [Test]
        public void InvokeInOrder_PerCallThrows_CommonAlreadyInvokedAndExceptionSwallowed()
        {
            var result = ClipboardOperationResult.Success("copyPlainText");
            bool commonInvoked = false;

            LogAssert.Expect(UnityEngine.LogType.Error, new System.Text.RegularExpressions.Regex(".*InvokeInOrder.*"));

            Assert.DoesNotThrow(() => AndroidClipboardManager.InvokeInOrder(
                result,
                common: r => commonInvoked = true,
                perCall: r => throw new System.InvalidOperationException("boom")));

            Assert.IsTrue(commonInvoked, "Common event must have already fired before the per-call callback threw.");
        }

        [Test]
        public void InvokeInOrder_CommonThrows_PerCallStillInvokedAndExceptionSwallowed()
        {
            var result = ClipboardOperationResult.Success("copyPlainText");
            bool perCallInvoked = false;

            LogAssert.Expect(UnityEngine.LogType.Error, new System.Text.RegularExpressions.Regex(".*InvokeInOrder.*"));

            Assert.DoesNotThrow(() => AndroidClipboardManager.InvokeInOrder(
                result,
                common: r => throw new System.InvalidOperationException("boom"),
                perCall: r => perCallInvoked = true));

            Assert.IsTrue(perCallInvoked, "Per-call callback must still be invoked even if the common event threw.");
        }

        [Test]
        public void ClipboardOperationResult_Success_ErrorMessageIsNull_Invariant()
        {
            var result = ClipboardOperationResult.Success("clear");

            Assert.IsTrue(result.IsSuccess);
            Assert.IsNull(result.ErrorMessage, "ErrorMessage must be null when IsSuccess is true.");
        }
    }
}
#endif
