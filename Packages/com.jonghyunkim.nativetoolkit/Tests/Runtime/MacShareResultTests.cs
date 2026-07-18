#nullable enable

#if UNITY_STANDALONE_OSX || UNITY_EDITOR
using NUnit.Framework;
using JonghyunKim.NativeToolkit.Runtime.Share;

namespace JonghyunKim.NativeToolkit.Tests
{
    public sealed class MacShareResultTests
    {
        [Test]
        public void Success_SetsFieldsAndErrorMessageNull()
        {
            var result = MacShareResult.Success("share", completed: true, serviceName: "Mail");

            Assert.AreEqual("share", result.Operation);
            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.Completed);
            Assert.AreEqual("Mail", result.ServiceName);
            Assert.IsNull(result.ErrorMessage);
        }

        [Test]
        public void Success_CancelledByUser_CompletedFalseServiceNameNull()
        {
            var result = MacShareResult.Success("share", completed: false, serviceName: null);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsFalse(result.Completed);
            Assert.IsNull(result.ServiceName);
            Assert.IsNull(result.ErrorMessage);
        }

        [Test]
        public void Failure_SetsErrorMessageAndFalseFields()
        {
            var result = MacShareResult.Failure("shareViaService", "Sharing service unavailable: x.");

            Assert.AreEqual("shareViaService", result.Operation);
            Assert.IsFalse(result.IsSuccess);
            Assert.IsFalse(result.Completed);
            Assert.IsNull(result.ServiceName);
            Assert.AreEqual("Sharing service unavailable: x.", result.ErrorMessage);
        }

        [Test]
        public void Failure_NullError_NormalizedToUnknownError()
        {
            var result = MacShareResult.Failure("share", null);

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual("Unknown error.", result.ErrorMessage);
        }

        [Test]
        public void Failure_WhitespaceError_NormalizedToUnknownError()
        {
            var result = MacShareResult.Failure("share", "   ");

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual("Unknown error.", result.ErrorMessage);
        }
    }
}
#endif
