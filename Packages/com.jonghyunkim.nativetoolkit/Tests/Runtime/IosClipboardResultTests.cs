#nullable enable

#if UNITY_IOS || UNITY_EDITOR
using System;
using NUnit.Framework;
using JonghyunKim.NativeToolkit.Runtime.Clipboard;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// EditMode tests for the invariants shared by every iOS clipboard result type:
    /// success implies no error, failure implies a usable error and no payload, and collections
    /// are never null.
    /// </summary>
    public sealed class IosClipboardResultTests
    {
        // ── error info normalization ────────────────────────────────────────────

        [Test]
        public void ErrorInfo_NullOrBlankCodeAndMessage_AreNormalized()
        {
            foreach (string? code in new[] { null, "", "   " })
            {
                foreach (string? message in new[] { null, "", "   " })
                {
                    IosClipboardErrorInfo info = IosClipboardErrorInfo.Create(code, message);

                    Assert.AreEqual(IosClipboardErrorInfo.UnknownErrorCode, info.Code);
                    Assert.AreEqual(IosClipboardErrorInfo.UnknownErrorMessage, info.Message);
                }
            }
        }

        [Test]
        public void ErrorInfo_ProvidedValues_ArePreserved()
        {
            IosClipboardErrorInfo info = IosClipboardErrorInfo.Create(
                "CLIPBOARD_DETECTION_FAILED", "Pattern detection failed.", "NSCocoaErrorDomain", 42);

            Assert.AreEqual("CLIPBOARD_DETECTION_FAILED", info.Code);
            Assert.AreEqual("Pattern detection failed.", info.Message);
            Assert.AreEqual("NSCocoaErrorDomain", info.Domain);
            Assert.AreEqual(42, info.NativeCode);
        }

        [Test]
        public void ErrorInfo_WithoutDetails_HasNullDomainAndNativeCode()
        {
            IosClipboardErrorInfo info = IosClipboardErrorInfo.Create("C", "M");

            Assert.IsNull(info.Domain);
            Assert.IsNull(info.NativeCode);
        }

        // ── operation result ────────────────────────────────────────────────────

        [Test]
        public void OperationResult_Success_HasNoError()
        {
            IosClipboardOperationResult result = IosClipboardOperationResult.Success(IosClipboardManager.OperationCopy);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsNull(result.Error);
            Assert.AreEqual(IosClipboardManager.OperationCopy, result.Operation);
        }

        [Test]
        public void OperationResult_Failure_AlwaysCarriesAUsableError()
        {
            IosClipboardOperationResult result =
                IosClipboardOperationResult.Failure(IosClipboardManager.OperationClear, null, null);

            Assert.IsFalse(result.IsSuccess);
            Assert.IsNotNull(result.Error);
            Assert.AreEqual(IosClipboardErrorInfo.UnknownErrorCode, result.Error!.Value.Code);
            Assert.AreEqual(IosClipboardErrorInfo.UnknownErrorMessage, result.Error.Value.Message);
            Assert.AreEqual(IosClipboardManager.OperationClear, result.Operation);
        }

        [Test]
        public void OperationResult_FailureFromErrorInfo_PreservesIt()
        {
            IosClipboardErrorInfo error = IosClipboardErrorInfo.Create("CLIPBOARD_BUSY", "busy", "D", 1);
            IosClipboardOperationResult result =
                IosClipboardOperationResult.Failure(IosClipboardManager.OperationAppend, error);

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual("CLIPBOARD_BUSY", result.Error!.Value.Code);
            Assert.AreEqual("D", result.Error.Value.Domain);
        }

        // ── payload results ─────────────────────────────────────────────────────

        [Test]
        public void ReadResult_Failure_HasEmptyItemsAndZeroCount()
        {
            IosClipboardReadResult result = IosClipboardReadResult.Failure("C", "M");

            Assert.IsFalse(result.IsSuccess);
            Assert.IsNotNull(result.Error);
            Assert.IsNotNull(result.Items, "collections are never null");
            Assert.AreEqual(0, result.Items.Count);
            Assert.AreEqual(0, result.NumberOfItems);
        }

        [Test]
        public void ReadDataResult_NoData_IsSuccessWithEmptyPayload()
        {
            IosClipboardReadDataResult result = IosClipboardReadDataResult.NoData();

            Assert.IsTrue(result.IsSuccess);
            Assert.IsNull(result.Error);
            Assert.IsFalse(result.HasData);
            Assert.IsNull(result.Data);
            Assert.IsNull(result.UtType);
            Assert.AreEqual(0, result.ByteCount);
        }

        [Test]
        public void ReadDataResult_Success_KeepsByteCountAndDataLengthInSync()
        {
            var payload = new byte[] { 1, 2, 3, 4 };
            IosClipboardReadDataResult result = IosClipboardReadDataResult.Success("public.png", payload);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.HasData);
            Assert.AreEqual(payload.Length, result.ByteCount);
            Assert.AreEqual(payload.Length, result.Data!.Length);
        }

        [Test]
        public void ReadDataResult_Failure_HasNoData()
        {
            IosClipboardReadDataResult result = IosClipboardReadDataResult.Failure("C", "M");

            Assert.IsFalse(result.IsSuccess);
            Assert.IsFalse(result.HasData);
            Assert.IsNull(result.Data);
            Assert.AreEqual(0, result.ByteCount);
        }

        [Test]
        public void SnapshotResult_Failure_HasNullSnapshot()
        {
            IosClipboardSnapshotResult result = IosClipboardSnapshotResult.Failure("C", "M");

            Assert.IsFalse(result.IsSuccess);
            Assert.IsNull(result.Snapshot);
            Assert.IsNotNull(result.Error);
        }

        [Test]
        public void PasteboardScopeResult_Failure_HasNullScope()
        {
            IosPasteboardScopeResult result = IosPasteboardScopeResult.Failure(null, null);

            Assert.IsFalse(result.IsSuccess);
            Assert.IsNull(result.Scope);
            Assert.AreEqual(IosClipboardErrorInfo.UnknownErrorCode, result.Error!.Value.Code);
        }

        [Test]
        public void DetectedPatternsResult_Failure_HasEmptyPatterns()
        {
            IosClipboardDetectedPatternsResult result = IosClipboardDetectedPatternsResult.Failure("C", "M");

            Assert.IsFalse(result.IsSuccess);
            Assert.IsNotNull(result.Patterns);
            Assert.AreEqual(0, result.Patterns.Count);
        }

        [Test]
        public void DetectedValuesResult_Failure_HasNullValues()
        {
            IosClipboardDetectedValuesResult result = IosClipboardDetectedValuesResult.Failure("C", "M");

            Assert.IsFalse(result.IsSuccess);
            Assert.IsNull(result.Values);
        }

        [Test]
        public void LoadedItemResult_Failure_HasNullItem()
        {
            IosClipboardLoadedItemResult result = IosClipboardLoadedItemResult.Failure("C", "M");

            Assert.IsFalse(result.IsSuccess);
            Assert.IsNull(result.Item);
        }

        [Test]
        public void ForegroundChangeResult_Failure_ReportsNotChanged()
        {
            IosClipboardForegroundChangeResult result = IosClipboardForegroundChangeResult.Failure("C", "M");

            Assert.IsFalse(result.IsSuccess);
            Assert.IsFalse(result.Changed);
        }

        [Test]
        public void EveryPayloadResult_Success_HasNoError()
        {
            Assert.IsNull(IosClipboardReadResult.Success(0, Array.Empty<IosClipboardItem>()).Error);
            Assert.IsNull(IosClipboardReadDataResult.NoData().Error);
            Assert.IsNull(IosClipboardDetectedPatternsResult
                .Success(Array.Empty<IosClipboardDetectionPattern>()).Error);
            Assert.IsNull(IosClipboardForegroundChangeResult.Success(true).Error);
            Assert.IsNull(IosPasteboardScopeResult.Success(IosPasteboardScope.General).Error);
        }

        // ── loaded item shape ───────────────────────────────────────────────────

        [Test]
        public void LoadedItem_OnlyTheMatchingPropertyIsPopulated()
        {
            IosClipboardLoadedItem text = IosClipboardLoadedItem.FromText("t");
            Assert.AreEqual("t", text.Text);
            Assert.IsNull(text.UrlString);
            Assert.IsNull(text.Data);
            Assert.IsNull(text.Path);

            IosClipboardLoadedItem file = IosClipboardLoadedItem.FromFile("/tmp/a");
            Assert.AreEqual("/tmp/a", file.Path);
            Assert.IsNull(file.Text);

            IosClipboardLoadedItem unknown = IosClipboardLoadedItem.UnknownKind();
            Assert.AreEqual(IosClipboardLoadedItemKind.Unknown, unknown.Kind);
            Assert.IsNull(unknown.Text);
            Assert.IsNull(unknown.UrlString);
            Assert.IsNull(unknown.Data);
            Assert.IsNull(unknown.Path);
        }

        // ── payload types ───────────────────────────────────────────────────────

        [Test]
        public void PasteboardScope_General_HasNoName()
        {
            Assert.AreEqual(IosPasteboardScopeKind.General, IosPasteboardScope.General.Kind);
            Assert.IsNull(IosPasteboardScope.General.Name);
        }

        [Test]
        public void CreationRequest_Unique_HasNoName()
        {
            Assert.AreEqual(IosPasteboardCreationRequestKind.Unique, IosPasteboardCreationRequest.Unique.Kind);
            Assert.IsNull(IosPasteboardCreationRequest.Unique.Name);
        }

        [Test]
        public void LoadRequest_OnlyFileCarriesAUtType()
        {
            Assert.IsNull(IosClipboardLoadRequest.Text.UtType);
            Assert.IsNull(IosClipboardLoadRequest.Url.UtType);
            Assert.IsNull(IosClipboardLoadRequest.Image.UtType);
            Assert.AreEqual("public.png", IosClipboardLoadRequest.File("public.png").UtType);
            Assert.Throws<ArgumentNullException>(() => IosClipboardLoadRequest.File(null!));
        }
    }
}
#endif
