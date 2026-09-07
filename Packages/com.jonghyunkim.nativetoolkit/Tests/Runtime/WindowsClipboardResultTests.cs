#nullable enable

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
using System;
using JonghyunKim.NativeToolkit.Runtime.Clipboard;
using NUnit.Framework;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// EditMode tests for the Windows clipboard result types and their error codes.
    /// Covers the invariants of design 8.4: a success carries no message, a failure always carries
    /// one, an empty read is still a success, and presence flags stay independent of success.
    /// </summary>
    public sealed class WindowsClipboardResultTests
    {
        private const string Op = "copyPlainText";

        [Test]
        public void Success_HasNoErrorCodeAndNoMessage()
        {
            WindowsClipboardResult result = WindowsClipboardResult.Success(Op);

            Assert.AreEqual(Op, result.Operation);
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(WindowsClipboardErrorCode.None, result.ErrorCode);
            Assert.IsNull(result.ErrorMessage);
        }

        [Test]
        public void Failure_AlwaysCarriesAMessage()
        {
            WindowsClipboardResult result =
                WindowsClipboardResult.Failure(Op, WindowsClipboardErrorCode.Busy);

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(WindowsClipboardErrorCode.Busy, result.ErrorCode);
            Assert.AreEqual("Clipboard is held by another process", result.ErrorMessage);
        }

        [Test]
        public void Failure_WithNoneCode_IsPromotedToUnknownSoTheMessageIsNeverNull()
        {
            WindowsClipboardResult result =
                WindowsClipboardResult.Failure(Op, WindowsClipboardErrorCode.None);

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(WindowsClipboardErrorCode.Unknown, result.ErrorCode);
            Assert.IsNotNull(result.ErrorMessage);
        }

        [Test]
        public void FromNative_ZeroIsSuccess_NonZeroCarriesThatCode()
        {
            Assert.IsTrue(WindowsClipboardResult.FromNative(Op, 0).IsSuccess);

            WindowsClipboardResult failed = WindowsClipboardResult.FromNative(Op, 14);
            Assert.IsFalse(failed.IsSuccess);
            Assert.AreEqual(WindowsClipboardErrorCode.WrongThread, failed.ErrorCode);
            Assert.AreEqual("API must be called from the owner UI thread", failed.ErrorMessage);
        }

        [Test]
        public void EveryNativeErrorCode_HasAMessageOfItsOwn()
        {
            for (int code = 1; code <= 19; code++)
            {
                var value = (WindowsClipboardErrorCode)code;
                string? message = value.ToMessage(Op);
                Assert.IsNotNull(message, $"code {code} has no message");
                // Asserting non-null alone would keep passing if the arm were deleted, because the
                // switch has a catch-all that formats the raw number.
                StringAssert.DoesNotStartWith("Unknown error (", message!,
                    $"code {code} fell through to the default arm instead of having its own message");
                Assert.IsTrue(value.IsNative(), $"code {code} should be classified as native");
            }
        }

        [Test]
        public void AnUndefinedCode_FallsThroughToTheCatchAll()
        {
            // Guards the guard: the check above only means something while the catch-all exists.
            string? message = ((WindowsClipboardErrorCode)9999).ToMessage(Op);

            Assert.IsNotNull(message);
            StringAssert.StartsWith("Unknown error (", message!);
        }

        [Test]
        public void FromNative_UnknownCodeIsNotClassifiedAsNative()
        {
            WindowsClipboardResult result = WindowsClipboardResult.FromNative(Op, 99);

            Assert.IsFalse(result.IsSuccess);
            Assert.IsFalse(result.ErrorCode.IsNative(), "99 is outside the native 0-19 range");
            Assert.IsNotNull(result.ErrorMessage);
        }

        [Test]
        public void EveryManagedErrorCode_HasAMessageOfItsOwnAndIsNotNative()
        {
            foreach (WindowsClipboardErrorCode value in Enum.GetValues(typeof(WindowsClipboardErrorCode)))
            {
                if ((int)value < 1000) continue;
                string? message = value.ToMessage(Op);
                Assert.IsNotNull(message, $"code {(int)value} has no message");
                StringAssert.DoesNotStartWith("Unknown error (", message!,
                    $"code {(int)value} fell through to the default arm instead of having its own message");
                Assert.IsFalse(value.IsNative(), $"code {(int)value} should not be classified as native");
            }
        }

        [Test]
        public void ManagedMessages_IncludeTheOperationName()
        {
            string? message = WindowsClipboardErrorCode.PlatformUnavailable.ToMessage("pastePlainText");

            Assert.IsNotNull(message);
            StringAssert.Contains("pastePlainText", message);
        }

        [Test]
        public void InvalidArgument_IncludesTheDetail()
        {
            string? message =
                WindowsClipboardErrorCode.InvalidArgument.ToMessage(Op, "paths was empty");

            Assert.IsNotNull(message);
            StringAssert.Contains("paths was empty", message);
        }

        [Test]
        public void NoneCode_HasNoMessage()
        {
            Assert.IsNull(WindowsClipboardErrorCode.None.ToMessage(Op));
        }

        [Test]
        public void TextResult_EmptyIsASuccessWithoutText()
        {
            WindowsClipboardTextResult result = WindowsClipboardTextResult.Empty("pastePlainText");

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.IsEmpty);
            Assert.IsNull(result.Text);
            Assert.IsNull(result.ErrorMessage);
        }

        [Test]
        public void TextResult_SuccessCarriesTheTextAndIsNotEmpty()
        {
            WindowsClipboardTextResult result =
                WindowsClipboardTextResult.Success("pastePlainText", "hello");

            Assert.IsTrue(result.IsSuccess);
            Assert.IsFalse(result.IsEmpty);
            Assert.AreEqual("hello", result.Text);
        }

        [Test]
        public void TextResult_FailureIsNotEmptyAndCarriesAMessage()
        {
            WindowsClipboardTextResult result = WindowsClipboardTextResult.Failure(
                "pastePlainText", WindowsClipboardErrorCode.NotInitialized);

            Assert.IsFalse(result.IsSuccess);
            Assert.IsFalse(result.IsEmpty, "a failure must not be reported as an empty clipboard");
            Assert.IsNotNull(result.ErrorMessage);
        }

        [Test]
        public void StringListResult_AnEmptyListIsAnEmptySuccess()
        {
            WindowsClipboardStringListResult result =
                WindowsClipboardStringListResult.Success("getClipboardFormats", new string[0]);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.IsEmpty);
            Assert.AreEqual(0, result.Values.Count);
        }

        [Test]
        public void StringListResult_FailureStillExposesAnEmptyList()
        {
            WindowsClipboardStringListResult result = WindowsClipboardStringListResult.Failure(
                "pasteFiles", WindowsClipboardErrorCode.Busy);

            Assert.IsFalse(result.IsSuccess);
            Assert.IsNotNull(result.Values);
            Assert.AreEqual(0, result.Values.Count);
        }

        [Test]
        public void BytesResult_ZeroLengthIsAnEmptySuccess()
        {
            WindowsClipboardBytesResult result =
                WindowsClipboardBytesResult.Success("pasteImage", new byte[0]);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.IsEmpty);
            Assert.AreEqual(0, result.Data.Length);
        }

        [Test]
        public void BytesResult_SuccessCarriesTheBytes()
        {
            WindowsClipboardBytesResult result =
                WindowsClipboardBytesResult.Success("pasteImage", new byte[] { 1, 2, 3 });

            Assert.IsFalse(result.IsEmpty);
            Assert.AreEqual(3, result.Data.Length);
        }

        [Test]
        public void FormatPresence_SuccessAndPresenceAreIndependent()
        {
            WindowsClipboardFormatPresenceResult absent =
                WindowsClipboardFormatPresenceResult.Success("hasClipboardFormat", false);
            WindowsClipboardFormatPresenceResult failed = WindowsClipboardFormatPresenceResult.Failure(
                "hasClipboardFormat", WindowsClipboardErrorCode.NotInitialized);

            Assert.IsTrue(absent.IsSuccess, "a format that is absent is still a successful query");
            Assert.IsFalse(absent.HasFormat);
            Assert.IsFalse(failed.IsSuccess);
            Assert.IsFalse(failed.HasFormat);
            Assert.IsNotNull(failed.ErrorMessage);
        }

        [Test]
        public void FlagResult_SuccessAndValueAreIndependent()
        {
            WindowsClipboardFlagResult value =
                WindowsClipboardFlagResult.Success("canDestroyClipboardManager", true);
            WindowsClipboardFlagResult failed = WindowsClipboardFlagResult.Failure(
                "canDestroyClipboardManager", WindowsClipboardErrorCode.NotInitialized);

            Assert.IsTrue(value.IsSuccess);
            Assert.IsTrue(value.Value);
            Assert.IsFalse(failed.IsSuccess);
            Assert.IsFalse(failed.Value);
        }

        [Test]
        public void CollectionResults_AreNeverNullEvenWhenDefaultConstructed()
        {
            // A readonly struct cannot run a field initializer, so default(T) would expose null
            // unless the accessors substitute the empty instance.
            Assert.IsNotNull(default(WindowsClipboardStringListResult).Values);
            Assert.IsNotNull(default(WindowsClipboardBytesResult).Data);
            Assert.IsNotNull(default(WindowsClipboardHistoryResult).Items);
        }

        [Test]
        public void HistoryResult_NonEmptyListIsNotEmpty()
        {
            WindowsClipboardHistoryResult parsed = ParseSingleHistoryItem();

            Assert.IsTrue(parsed.IsSuccess);
            Assert.IsFalse(parsed.IsEmpty);
            Assert.AreEqual(1, parsed.Items.Count);
        }

        private static WindowsClipboardHistoryResult ParseSingleHistoryItem()
        {
            WindowsClipboardJsonParser.TryParseHistoryItems(
                "[{\"id\":\"a\",\"timestamp\":\"1\"}]",
                out System.Collections.Generic.IReadOnlyList<WindowsClipboardHistoryItem> items);
            return WindowsClipboardHistoryResult.Success("getClipboardHistory", items);
        }

        [Test]
        public void HistoryResult_EmptyListIsAnEmptySuccess()
        {
            WindowsClipboardHistoryResult result = WindowsClipboardHistoryResult.Success(
                "getClipboardHistory", new WindowsClipboardHistoryItem[0]);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.IsEmpty);
            Assert.AreEqual(0, result.Items.Count);
        }

        [Test]
        public void AvailabilityResult_FailureReportsBothFlagsFalse()
        {
            WindowsClipboardAvailabilityResult result = WindowsClipboardAvailabilityResult.Failure(
                "getClipboardHistoryAvailability", WindowsClipboardErrorCode.NotForeground);

            Assert.IsFalse(result.IsSuccess);
            Assert.IsFalse(result.HistoryEnabled);
            Assert.IsFalse(result.RoamingEnabled);
            Assert.AreEqual("App is not in the foreground", result.ErrorMessage);
        }
    }
}
#endif
