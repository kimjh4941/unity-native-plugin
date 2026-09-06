#nullable enable

#if UNITY_IOS || UNITY_EDITOR
using NUnit.Framework;
using JonghyunKim.NativeToolkit.Runtime.Share;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// EditMode tests for the iOS share result type.
    /// </summary>
    public sealed class IosShareResultTests
    {
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
#endif
