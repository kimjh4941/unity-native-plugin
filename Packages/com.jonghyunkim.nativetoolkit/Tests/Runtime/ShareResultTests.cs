#nullable enable

using NUnit.Framework;
using JonghyunKim.NativeToolkit.Runtime.Share;

namespace JonghyunKim.NativeToolkit.Tests
{
    public sealed class ShareResultTests
    {
        [Test]
        public void ShareOperationResult_Success_IsSuccessTrueAndErrorMessageNull()
        {
            var result = ShareOperationResult.Success("shareText");

            Assert.AreEqual("shareText", result.Operation);
            Assert.IsTrue(result.IsSuccess);
            Assert.IsNull(result.ErrorMessage);
        }

        [Test]
        public void ShareOperationResult_Failure_IsSuccessFalseAndErrorMessageSet()
        {
            var result = ShareOperationResult.Failure("shareImage", "File not found: /path.png");

            Assert.AreEqual("shareImage", result.Operation);
            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual("File not found: /path.png", result.ErrorMessage);
        }

        [Test]
        public void ShareOperationResult_Success_ErrorMessageIsNull_Invariant()
        {
            var result = ShareOperationResult.Success("shareFile");

            Assert.IsTrue(result.IsSuccess);
            Assert.IsNull(result.ErrorMessage, "ErrorMessage must be null when IsSuccess is true.");
        }

        [Test]
        public void ShareCallbackResult_WithPackageName_StoresValues()
        {
            var result = new ShareCallbackResult("shareWithCallback", "com.example.app");

            Assert.AreEqual("shareWithCallback", result.Operation);
            Assert.AreEqual("com.example.app", result.SelectedPackageName);
        }

        [Test]
        public void ShareCallbackResult_NullPackageName_IsAllowed()
        {
            var result = new ShareCallbackResult("shareWithCallback", null);

            Assert.AreEqual("shareWithCallback", result.Operation);
            Assert.IsNull(result.SelectedPackageName);
        }
    }
}
