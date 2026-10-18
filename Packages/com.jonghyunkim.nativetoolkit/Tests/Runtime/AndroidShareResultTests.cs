#nullable enable

using NUnit.Framework;
using JonghyunKim.NativeToolkit.Runtime.Android.Share;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// EditMode tests for the Android share result types.
    /// </summary>
    public sealed class AndroidShareResultTests
    {
        [Test]
        public void AndroidShareOperationResult_Success_IsSuccessTrueAndErrorMessageNull()
        {
            var result = AndroidShareOperationResult.Success("shareText");

            Assert.AreEqual("shareText", result.Operation);
            Assert.IsTrue(result.IsSuccess);
            Assert.IsNull(result.ErrorMessage);
        }

        [Test]
        public void AndroidShareOperationResult_Failure_IsSuccessFalseAndErrorMessageSet()
        {
            var result = AndroidShareOperationResult.Failure("shareImage", "File not found: /path.png");

            Assert.AreEqual("shareImage", result.Operation);
            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual("File not found: /path.png", result.ErrorMessage);
        }

        [Test]
        public void AndroidShareOperationResult_Success_ErrorMessageIsNull_Invariant()
        {
            var result = AndroidShareOperationResult.Success("shareFile");

            Assert.IsTrue(result.IsSuccess);
            Assert.IsNull(result.ErrorMessage, "ErrorMessage must be null when IsSuccess is true.");
        }

        [Test]
        public void AndroidShareCallbackResult_WithPackageName_StoresValues()
        {
            var result = new AndroidShareCallbackResult("shareWithCallback", "com.example.app");

            Assert.AreEqual("shareWithCallback", result.Operation);
            Assert.AreEqual("com.example.app", result.SelectedPackageName);
        }

        [Test]
        public void AndroidShareCallbackResult_NullPackageName_IsAllowed()
        {
            var result = new AndroidShareCallbackResult("shareWithCallback", null);

            Assert.AreEqual("shareWithCallback", result.Operation);
            Assert.IsNull(result.SelectedPackageName);
        }

        // (7) AndroidShareChooserActionResult stores ActionId
        [Test]
        public void AndroidShareChooserActionResult_WithActionId_StoresValue()
        {
            var result = new AndroidShareChooserActionResult("com.example.action");

            Assert.AreEqual("com.example.action", result.ActionId);
        }

        // (8) AndroidShareChooserActionResult normalizes null to string.Empty
        [Test]
        public void AndroidShareChooserActionResult_NullActionId_NormalizedToEmpty()
        {
            var result = new AndroidShareChooserActionResult(null!);

            Assert.AreEqual(string.Empty, result.ActionId);
        }

        [Test]
        public void AndroidShareChooserActionResult_EmptyActionId_StoredAsEmpty()
        {
            var result = new AndroidShareChooserActionResult(string.Empty);

            Assert.AreEqual(string.Empty, result.ActionId);
        }
    }
}
