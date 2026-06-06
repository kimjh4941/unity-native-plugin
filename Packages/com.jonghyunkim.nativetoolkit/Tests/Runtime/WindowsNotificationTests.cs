#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using JonghyunKim.NativeToolkit.Runtime.Notification;

namespace JonghyunKim.NativeToolkit.Tests
{
    public sealed class WindowsNotificationResultTests
    {
        [Test]
        public void Success_SetsIsSuccessTrue_ErrorCodeZero_ErrorMessageNull()
        {
            var result = WindowsNotificationResult.Success("showNotification");
            Assert.AreEqual("showNotification", result.Operation);
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(0, result.ErrorCode);
            Assert.IsNull(result.ErrorMessage);
        }

        [Test]
        public void Failure_SetsIsSuccessFalse_ErrorMessageNotNull()
        {
            var result = WindowsNotificationResult.Failure("showNotification", 2);
            Assert.AreEqual("showNotification", result.Operation);
            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(2, result.ErrorCode);
            Assert.AreEqual("Notifications are disabled", result.ErrorMessage);
        }

        [Test]
        public void Failure_ErrorCode1_ReturnsNotInitialized()
        {
            var result = WindowsNotificationResult.Failure("initialize", 1);
            Assert.AreEqual("Not initialized", result.ErrorMessage);
        }

        [Test]
        public void Failure_ErrorCode3_ReturnsInvalidJsonPayload()
        {
            var result = WindowsNotificationResult.Failure("showNotification", 3);
            Assert.AreEqual("Invalid JSON payload", result.ErrorMessage);
        }

        [Test]
        public void Failure_ErrorCode4_ReturnsProgressNotFound()
        {
            var result = WindowsNotificationResult.Failure("updateNotificationProgress", 4);
            Assert.AreEqual("Progress notification not found", result.ErrorMessage);
        }

        [Test]
        public void Failure_ErrorCode5_ReturnsHresultFailure()
        {
            var result = WindowsNotificationResult.Failure("getAllNotifications", 5);
            Assert.AreEqual("WinRT HRESULT failure", result.ErrorMessage);
        }

        [Test]
        public void Failure_ErrorCode6_ReturnsBadgeOperationFailed()
        {
            var result = WindowsNotificationResult.Failure("setBadge", 6);
            Assert.AreEqual("Badge operation failed", result.ErrorMessage);
        }

        [Test]
        public void Failure_ErrorCode7_ReturnsInvalidParameter()
        {
            var result = WindowsNotificationResult.Failure("showNotification", 7);
            Assert.AreEqual("Invalid parameter", result.ErrorMessage);
        }

        [Test]
        public void Failure_UnknownErrorCode_ReturnsUnknownErrorString()
        {
            var result = WindowsNotificationResult.Failure("showNotification", 99);
            Assert.AreEqual("Unknown error (99)", result.ErrorMessage);
        }
    }

    public sealed class WindowsNotificationJsonBuilderValidateTests
    {
        [Test]
        public void Validate_ValidPayload_ReturnsNull()
        {
            var payload = new WindowsNotificationPayload { Title = "Hello", Body = "World" };
            Assert.IsNull(WindowsNotificationJsonBuilder.Validate(payload));
        }

        [Test]
        public void Validate_ButtonsExceed5_ReturnsError()
        {
            var payload = new WindowsNotificationPayload
            {
                Buttons = new List<WindowsNotificationButtonPayload>
                {
                    new() { Label = "A" },
                    new() { Label = "B" },
                    new() { Label = "C" },
                    new() { Label = "D" },
                    new() { Label = "E" },
                    new() { Label = "F" },
                }
            };
            Assert.AreEqual("buttons count exceeds 5", WindowsNotificationJsonBuilder.Validate(payload));
        }

        [Test]
        public void Validate_Exactly5Buttons_ReturnsNull()
        {
            var payload = new WindowsNotificationPayload
            {
                Buttons = new List<WindowsNotificationButtonPayload>
                {
                    new() { Label = "A" },
                    new() { Label = "B" },
                    new() { Label = "C" },
                    new() { Label = "D" },
                    new() { Label = "E" },
                }
            };
            Assert.IsNull(WindowsNotificationJsonBuilder.Validate(payload));
        }

        [Test]
        public void Validate_AudioLoopWithoutLongDuration_ReturnsError()
        {
            var payload = new WindowsNotificationPayload
            {
                Audio = new WindowsNotificationAudioPayload { Loop = true }
            };
            Assert.AreEqual("audio.loop requires duration=long", WindowsNotificationJsonBuilder.Validate(payload));
        }

        [Test]
        public void Validate_AudioLoopWithLongDuration_ReturnsNull()
        {
            var payload = new WindowsNotificationPayload
            {
                Duration = "long",
                Audio = new WindowsNotificationAudioPayload { Loop = true }
            };
            Assert.IsNull(WindowsNotificationJsonBuilder.Validate(payload));
        }

        [Test]
        public void Validate_ButtonWithBothArgsAndInvokeUri_ReturnsError()
        {
            var payload = new WindowsNotificationPayload
            {
                Buttons = new List<WindowsNotificationButtonPayload>
                {
                    new()
                    {
                        Label = "Open",
                        Args = new Dictionary<string, string> { ["action"] = "open" },
                        InvokeUri = "https://example.com"
                    }
                }
            };
            Assert.AreEqual("button cannot have both args and invokeUri", WindowsNotificationJsonBuilder.Validate(payload));
        }
    }

    public sealed class WindowsNotificationJsonBuilderBuildTests
    {
        [Test]
        public void Build_TitleAndBody_ProducesExpectedJson()
        {
            var payload = new WindowsNotificationPayload { Title = "Hello", Body = "World" };
            var json = WindowsNotificationJsonBuilder.BuildNotificationPayload(payload);
            Assert.That(json, Does.Contain("\"title\":\"Hello\""));
            Assert.That(json, Does.Contain("\"body\":\"World\""));
        }

        [Test]
        public void Build_WithButton_ArgsOnly_ProducesArgsInJson()
        {
            var payload = new WindowsNotificationPayload
            {
                Buttons = new List<WindowsNotificationButtonPayload>
                {
                    new() { Label = "OK", Args = new Dictionary<string, string> { ["action"] = "ok" } }
                }
            };
            var json = WindowsNotificationJsonBuilder.BuildNotificationPayload(payload);
            Assert.That(json, Does.Contain("\"buttons\""));
            Assert.That(json, Does.Contain("\"label\":\"OK\""));
            Assert.That(json, Does.Contain("\"args\""));
        }

        [Test]
        public void Build_WithButton_InvokeUri_ProducesInvokeUriInJson()
        {
            var payload = new WindowsNotificationPayload
            {
                Buttons = new List<WindowsNotificationButtonPayload>
                {
                    new() { Label = "Open", InvokeUri = "https://example.com" }
                }
            };
            var json = WindowsNotificationJsonBuilder.BuildNotificationPayload(payload);
            Assert.That(json, Does.Contain("\"invokeUri\""));
        }

        [Test]
        public void Build_InvalidPayload_ThrowsArgumentException()
        {
            var payload = new WindowsNotificationPayload
            {
                Audio = new WindowsNotificationAudioPayload { Loop = true }
            };
            Assert.Throws<System.ArgumentException>(() =>
                WindowsNotificationJsonBuilder.BuildNotificationPayload(payload));
        }

        [Test]
        public void Build_WithProgress_ProducesProgressInJson()
        {
            var payload = new WindowsNotificationPayload
            {
                Progress = new WindowsNotificationProgressPayload { Value = 0.5, ValueStr = "50%", Status = "Downloading" }
            };
            var json = WindowsNotificationJsonBuilder.BuildNotificationPayload(payload);
            Assert.That(json, Does.Contain("\"progress\""));
            Assert.That(json, Does.Contain("\"value\":0.5"));
        }

        [Test]
        public void Build_NullOptionalFields_NotIncludedInJson()
        {
            var payload = new WindowsNotificationPayload { Title = "T" };
            var json = WindowsNotificationJsonBuilder.BuildNotificationPayload(payload);
            Assert.That(json, Does.Not.Contain("\"body\""));
            Assert.That(json, Does.Not.Contain("\"tag\""));
            Assert.That(json, Does.Not.Contain("\"audio\""));
        }
    }
}
