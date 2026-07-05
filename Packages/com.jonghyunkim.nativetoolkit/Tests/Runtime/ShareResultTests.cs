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

        // (7) ShareChooserActionResult stores ActionId
        [Test]
        public void ShareChooserActionResult_WithActionId_StoresValue()
        {
            var result = new ShareChooserActionResult("com.example.action");

            Assert.AreEqual("com.example.action", result.ActionId);
        }

        // (8) ShareChooserActionResult normalizes null to string.Empty
        [Test]
        public void ShareChooserActionResult_NullActionId_NormalizedToEmpty()
        {
            var result = new ShareChooserActionResult(null!);

            Assert.AreEqual(string.Empty, result.ActionId);
        }

        [Test]
        public void ShareChooserActionResult_EmptyActionId_StoredAsEmpty()
        {
            var result = new ShareChooserActionResult(string.Empty);

            Assert.AreEqual(string.Empty, result.ActionId);
        }

        [Test]
        public void IosShareResult_Success_Completed_StoresValues()
        {
            var result = IosShareResult.Success(completed: true, activityType: "com.apple.UIKit.activity.Mail");

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.Completed);
            Assert.AreEqual("com.apple.UIKit.activity.Mail", result.ActivityType);
            Assert.IsNull(result.ErrorMessage);
        }

        [Test]
        public void IosShareResult_Success_Cancelled_IsNotAnError()
        {
            var result = IosShareResult.Success(completed: false, activityType: null);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsFalse(result.Completed);
            Assert.IsNull(result.ActivityType);
            Assert.IsNull(result.ErrorMessage);
        }

        [Test]
        public void IosShareResult_Failure_SetsErrorMessage()
        {
            var result = IosShareResult.Failure("No shareable items were provided.");

            Assert.IsFalse(result.IsSuccess);
            Assert.IsFalse(result.Completed);
            Assert.IsNull(result.ActivityType);
            Assert.AreEqual("No shareable items were provided.", result.ErrorMessage);
        }

        [Test]
        public void IosShareResult_Success_ErrorMessageIsNull_Invariant()
        {
            var result = IosShareResult.Success(completed: true, activityType: null);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsNull(result.ErrorMessage, "ErrorMessage must be null when IsSuccess is true.");
        }

        [Test]
        public void IosShareResult_Failure_NullError_NormalizesMessage()
        {
            var result = IosShareResult.Failure(null);

            Assert.IsFalse(result.IsSuccess);
            Assert.IsNotNull(result.ErrorMessage, "ErrorMessage must be non-null when IsSuccess is false.");
            Assert.AreEqual("Unknown error.", result.ErrorMessage);
        }

        [Test]
        public void IosShareResult_Failure_WhitespaceError_NormalizesMessage()
        {
            var result = IosShareResult.Failure("   ");

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual("Unknown error.", result.ErrorMessage);
        }
    }
}
