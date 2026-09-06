#nullable enable

#if UNITY_STANDALONE_OSX || UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using JonghyunKim.NativeToolkit.Runtime.Clipboard;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// EditMode tests for the macOS clipboard result types, the error info, the payload factories
    /// and the raw-value tables. Covers the three invariant shapes a payload can take and the
    /// boundary where <c>default</c> is not a failure.
    /// </summary>
    public sealed class MacClipboardResultTests
    {
        private const long SomeCode = MacClipboardErrorCodes.WriteRejected;
        private const string SomeMessage = "The pasteboard rejected the write operation.";

        // ── invariant 1: reference-typed payloads are null on failure ────────

        [Test]
        public void ReferencePayloads_AreNullOnFailureAndNonNullOnSuccess()
        {
            var ownership = new MacPasteboardOwnership(MacPasteboardScope.General, 1);
            var success = MacClipboardOwnershipResult.Success(MacClipboardOperations.Copy, ownership);
            Assert.IsTrue(success.IsSuccess);
            Assert.IsNull(success.Error);
            Assert.IsNotNull(success.Ownership);

            var failure = MacClipboardOwnershipResult.Failure(MacClipboardOperations.Copy, SomeCode, SomeMessage);
            Assert.IsFalse(failure.IsSuccess);
            Assert.IsNotNull(failure.Error);
            Assert.IsNull(failure.Ownership);
        }

        [Test]
        public void ReferencePayloads_HoldForEveryResultTypeThatCarriesOne()
        {
            Assert.IsNull(MacClipboardReadResult.Failure(SomeCode, SomeMessage).Contents);
            Assert.IsNull(MacClipboardSnapshotResult.Failure(SomeCode, SomeMessage).Snapshot);
            Assert.IsNull(MacPasteboardScopeResult.Failure(SomeCode, SomeMessage).Scope);
            Assert.IsNull(MacClipboardDetectedValuesResult.Failure(SomeCode, SomeMessage).Values);
            Assert.IsNull(MacClipboardDetectedMetadataResult.Failure(SomeCode, SomeMessage).Metadata);
        }

        // ── invariant 2: value-typed payloads take their default on failure ──

        [Test]
        public void ValuePayloads_TakeTheirDefaultOnFailure()
        {
            // These cannot be null, so a caller must check IsSuccess before reading them.
            Assert.AreEqual(0L, MacClipboardChangeCountResult.Failure(SomeCode, SomeMessage).ChangeCount);
            Assert.IsFalse(MacClipboardForegroundChangeResult.Failure(SomeCode, SomeMessage).Changed);
            Assert.AreEqual(MacClipboardAccessBehavior.Unknown,
                MacClipboardAccessBehaviorResult.Failure(SomeCode, SomeMessage).Behavior);
        }

        [Test]
        public void ValuePayloads_CarryTheirValueOnSuccess()
        {
            Assert.AreEqual(42L, MacClipboardChangeCountResult.Success(42).ChangeCount);
            Assert.IsTrue(MacClipboardForegroundChangeResult.Success(true).Changed);
            Assert.AreEqual(MacClipboardAccessBehavior.Ask,
                MacClipboardAccessBehaviorResult.Success(MacClipboardAccessBehavior.Ask).Behavior);
        }

        // ── invariant 3: a directly held collection is never null ────────────

        [Test]
        public void DetectedPatternsResult_PatternsIsNeverNullEvenOnFailure()
        {
            // The one collection a result type holds directly rather than through a nested object.
            var failure = MacClipboardDetectedPatternsResult.Failure(SomeCode, SomeMessage);
            Assert.IsNotNull(failure.Patterns);
            Assert.AreEqual(0, failure.Patterns.Count);

            var success = MacClipboardDetectedPatternsResult.Success(
                new[] { MacClipboardDetectionPattern.Links });
            Assert.AreEqual(1, success.Patterns.Count);
        }

        [Test]
        public void NestedCollections_AreNeverNullWhenTheNestedObjectExists()
        {
            // These belong to the nested object, not the result, so "non-null on failure" does not
            // apply: on failure the nested object itself is absent.
            var contents = new MacClipboardReadContents(1, Array.Empty<MacClipboardItem>());
            Assert.IsNotNull(contents.Items);

            var snapshot = new MacClipboardSnapshot(
                1, Array.Empty<IReadOnlyList<string>>(), Array.Empty<int>());
            Assert.IsNotNull(snapshot.ItemTypes);
            Assert.IsNotNull(snapshot.MatchingItemIndexes);
        }

        // ── the readData exception ───────────────────────────────────────────

        [Test]
        public void ReadDataResult_IsTheOnlyTypeWhereSuccessWithNullIsNormal()
        {
            var absent = MacClipboardReadDataResult.Success(null);
            Assert.IsTrue(absent.IsSuccess);
            Assert.IsNull(absent.Error);
            Assert.IsNull(absent.Data);

            var present = MacClipboardReadDataResult.Success(new byte[] { 1 });
            Assert.IsTrue(present.IsSuccess);
            Assert.IsNotNull(present.Data);
        }

        // ── default is not a failure ─────────────────────────────────────────

        [Test]
        public void Default_IsAnUninitialisedValueRatherThanAFailureResult()
        {
            // A readonly struct can always be default-constructed, and that value satisfies
            // neither "failure carries an error" nor "operation is never null". The Manager only
            // ever returns factory-produced values; default must not be treated as a result.
            var uninitialised = default(MacClipboardOperationResult);
            Assert.IsFalse(uninitialised.IsSuccess);
            Assert.IsNull(uninitialised.Error, "default carries no error, so it is not a failure result");
            Assert.IsNull(uninitialised.Operation, "default carries no operation");
        }

        [Test]
        public void Failure_AlwaysCarriesAnErrorAndAnOperation()
        {
            var failure = MacClipboardOperationResult.Failure(
                MacClipboardOperations.RemovePasteboard, SomeCode, SomeMessage);
            Assert.IsNotNull(failure.Error);
            Assert.IsNotNull(failure.Operation);
            Assert.AreEqual(MacClipboardOperations.RemovePasteboard, failure.Operation);
        }

        [Test]
        public void OwnershipResult_DistinguishesCopyFromAppend()
        {
            // Both operations share this type, so Operation is the only way a subscriber can tell
            // which write completed, and that OwnershipLost belongs to append.
            var copy = MacClipboardOwnershipResult.Failure(
                MacClipboardOperations.Copy, MacClipboardErrorCodes.WriteRejected, "w");
            var append = MacClipboardOwnershipResult.Failure(
                MacClipboardOperations.Append, MacClipboardErrorCodes.OwnershipLost, "o");

            Assert.AreEqual(MacClipboardOperations.Copy, copy.Operation);
            Assert.AreEqual(MacClipboardOperations.Append, append.Operation);
            Assert.AreNotEqual(copy.Operation, append.Operation);
        }

        // ── error info ───────────────────────────────────────────────────────

        [Test]
        public void ErrorInfo_NormalisesABlankMessage()
        {
            foreach (string? message in new[] { null, string.Empty, "   " })
            {
                MacClipboardErrorInfo info = MacClipboardErrorInfo.Create(SomeCode, message);
                Assert.AreEqual(MacClipboardErrorInfo.UnknownErrorMessage, info.Message);
            }
        }

        [Test]
        public void ErrorInfo_NarrowsTheNative64BitCode()
        {
            Assert.AreEqual(1511, MacClipboardErrorInfo.Create(1511L, "m").Code);
        }

        [Test]
        public void ErrorInfo_CodeOutsideIntRange_BecomesUnknownRatherThanWrapping()
        {
            // An unchecked cast would turn this into 1, which looks like a plausible code.
            long outOfRange = (long)int.MaxValue + 1;
            Assert.AreEqual(MacClipboardErrorCodes.Unknown,
                MacClipboardErrorInfo.Create(outOfRange, "m").Code);
            Assert.AreEqual(MacClipboardErrorCodes.Unknown,
                MacClipboardErrorInfo.Create((long)int.MinValue - 1, "m").Code);
        }

        [Test]
        public void ErrorInfo_IsManagedCode_SeparatesBridgeCodesFromNativeOnes()
        {
            // 1301 and 1302 are raised inside the native bridge, so they are native codes.
            Assert.IsFalse(MacClipboardErrorInfo.Create(MacClipboardErrorCodes.ContractViolation, "m").IsManagedCode);
            Assert.IsFalse(MacClipboardErrorInfo.Create(MacClipboardErrorCodes.Unknown, "m").IsManagedCode);
            Assert.IsTrue(MacClipboardErrorInfo.Create(MacClipboardErrorCodes.Busy, "m").IsManagedCode);
            Assert.IsTrue(MacClipboardErrorInfo.Create(MacClipboardErrorCodes.RequestTooLarge, "m").IsManagedCode);
        }

        // ── payload factories ────────────────────────────────────────────────

        [Test]
        public void PasteboardScope_RejectsABlankName()
        {
            // The native parser only rejects an empty name, so " " would reach NSPasteboard and a
            // pasteboard would actually be created. This check is the only thing that stops it.
            foreach (string blank in new[] { "", " ", "\t" })
            {
                Assert.Throws<ArgumentException>(() => MacPasteboardScope.Named(blank), blank);
                Assert.Throws<ArgumentException>(() => MacPasteboardScope.Unique(blank), blank);
                Assert.Throws<ArgumentException>(() => MacPasteboardCreationRequest.Named(blank), blank);
            }
        }

        [Test]
        public void ContentItem_PlainText_IsUtf8UnderThePlainTextIdentifier()
        {
            MacClipboardContentItem item = MacClipboardContentItem.PlainText("日本語");
            Assert.AreEqual(1, item.Representations.Count);
            Assert.AreEqual("日本語",
                Encoding.UTF8.GetString(item.Representations[MacClipboardTypes.PlainText]));
        }

        [Test]
        public void ContentItem_HtmlWithFallback_CarriesBothRepresentations()
        {
            MacClipboardContentItem item = MacClipboardContentItem.Html("<b>x</b>", "x");
            Assert.AreEqual(2, item.Representations.Count);
            Assert.IsTrue(item.Representations.ContainsKey(MacClipboardTypes.Html));
            Assert.IsTrue(item.Representations.ContainsKey(MacClipboardTypes.PlainText));
        }

        [Test]
        public void ContentItem_NullArguments_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => MacClipboardContentItem.PlainText(null!));
            Assert.Throws<ArgumentNullException>(() => MacClipboardContentItem.Url(null!));
            Assert.Throws<ArgumentNullException>(() => MacClipboardContentItem.Data("a", null!));
            Assert.Throws<ArgumentNullException>(
                () => MacClipboardContentItem.FromRepresentations(null!));
        }

        [Test]
        public void Ownership_NullScope_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new MacPasteboardOwnership(null!, 0));
        }

        // ── operation names ──────────────────────────────────────────────────

        [Test]
        public void OperationNames_AreAllDistinct()
        {
            string[] names =
            {
                MacClipboardOperations.Copy, MacClipboardOperations.Append,
                MacClipboardOperations.Read, MacClipboardOperations.ReadData,
                MacClipboardOperations.Snapshot, MacClipboardOperations.Clear,
                MacClipboardOperations.CreatePasteboard, MacClipboardOperations.RemovePasteboard,
                MacClipboardOperations.DetectPatterns, MacClipboardOperations.DetectValues,
                MacClipboardOperations.DetectMetadata, MacClipboardOperations.AccessBehavior,
                MacClipboardOperations.StartObserving, MacClipboardOperations.StopObserving,
                MacClipboardOperations.CheckForegroundChange,
            };
            Assert.AreEqual(15, names.Length);
            CollectionAssert.AllItemsAreUnique(names);
        }

        [Test]
        public void ObservationControlKey_MatchesNoOperationName()
        {
            // Start and Stop share this key while keeping their own Operation values, so the key
            // must not collide with any operation in the in-flight set.
            string[] names =
            {
                MacClipboardOperations.Copy, MacClipboardOperations.Append,
                MacClipboardOperations.Read, MacClipboardOperations.ReadData,
                MacClipboardOperations.Snapshot, MacClipboardOperations.Clear,
                MacClipboardOperations.CreatePasteboard, MacClipboardOperations.RemovePasteboard,
                MacClipboardOperations.DetectPatterns, MacClipboardOperations.DetectValues,
                MacClipboardOperations.DetectMetadata, MacClipboardOperations.AccessBehavior,
                MacClipboardOperations.StartObserving, MacClipboardOperations.StopObserving,
                MacClipboardOperations.CheckForegroundChange,
            };
            CollectionAssert.DoesNotContain(names, MacClipboardOperations.ObservationControlKey);
        }

        // ── limits ───────────────────────────────────────────────────────────

        [Test]
        public void Limits_MatchTheNativeObservationDefault()
        {
            Assert.AreEqual(0.5, MacClipboardLimits.DefaultObservationInterval);
            Assert.Greater(MacClipboardLimits.MaxRequestBytes, 0);
            Assert.Greater(MacClipboardLimits.MaxResponseBytesPerRepresentation, 0);
        }
    }
}
#endif
