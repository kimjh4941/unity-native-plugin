#nullable enable

#if UNITY_IOS
#if !UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using JonghyunKim.NativeToolkit.Runtime.iOS.Clipboard;
using static JonghyunKim.NativeToolkit.Tests.IosPlayerTestSupport;

namespace JonghyunKim.NativeToolkit.Tests
{
#if !NTK_IOS_CABI_SWITCHED || NTK_IOS_CABI_2_CLIPBOARD
    /// <summary>
    /// iOS player: the Clipboard plan v4, section 1 (artifact/ios/clipboard/designs/2026-11-14-ios-clipboard-ui-test-plan-v4.md).
    /// Every test uses its own named pasteboard and never the general one: the manager is only called
    /// through the helpers below, and the helpers that touch a pasteboard require its scope. The helpers
    /// keep every call, and the teardown fails a test whose calls did not each complete exactly once (an
    /// unfinished call would make the next test's call CLIPBOARD_BUSY). The values measured on 1.x are
    /// asserted; <see cref="Record"/> writes extra context to the log (the test's own values only).
    /// The error logs 1.x writes for failed read-like calls are not asserted: they are 1.x internals, not
    /// behaviour to keep (the tests that expect such a failure ignore failing log messages).
    /// </summary>
    /// <remarks>
    /// The whole fixture reaches the native side (its set-up and tear-down create and remove pasteboards,
    /// and the Test Framework runs them for [Test] too), so it is compiled out after the 3-S switch until
    /// Clipboard moves to the C ABI (NTK_IOS_CABI_2_CLIPBOARD; artifact/topics/ios-c-abi-2, 3-S design v3, 1.7).
    /// </remarks>
    [Category(IosPlayerTestSupport.Category)]
    public class IosClipboardPlayerTests
    {
        private const string Prefix = "com.jonghyunkim.nativetoolkit.playertests.";
        private const string CustomType = "com.jonghyunkim.ntk.custom";
        private const float Window = 3f;

        private sealed class Box<T> where T : struct
        {
            public T? Value;
            public int Count;
            public T V => Value ?? throw new AssertionException("no result within the timeout");
        }

        private IosClipboardManager M => IosClipboardManager.Instance;
        private IosPasteboardScope _scope = null!;
        private readonly List<(string what, Func<int> count)> _tracked = new();
        private bool _observing;
        private IosPasteboardScope? _extra;

        private Box<T> Track<T>(Action<Action<T>> start) where T : struct
        {
            var box = new Box<T>();
            _tracked.Add((typeof(T).Name, () => box.Count));
            start(r => { box.Value = r; box.Count++; });
            return box;
        }

        /// <summary>
        /// Waits for the result. It does not assert (an assertion inside a nested enumerator would skip the
        /// caller's finally blocks): a missing result fails at <see cref="Box{T}.V"/> in the test's body.
        /// </summary>
        private static IEnumerator Await<T>(Box<T> box, string what) where T : struct
        {
            yield return WaitFor(() => box.Count > 0);
        }

        private static void Record(string what) => Debug.Log($"[NTK-CLIPBOARD-RECORD] {TestContext.CurrentContext.Test.Name} {what}");

        /// <summary>
        /// 1.x logs an error when a read-like call fails (IosClipboardJsonParser.ReadErrorInfo); 2.0.0 need not.
        /// The tests that expect such a failure ignore failing log messages until the teardown.
        /// </summary>
        private static void IgnoreErrorLogs() => LogAssert.ignoreFailingMessages = true;

        private static void ExpectError(IosClipboardErrorInfo? error, string code, string message)
        {
            Assert.IsNotNull(error, "expected a failure");
            Record($"error code={error!.Value.Code} domain={error.Value.Domain ?? "<null>"} native-code={error.Value.NativeCode?.ToString() ?? "<null>"}");
            Assert.AreEqual(code, error.Value.Code);
            Assert.AreEqual(message, error.Value.Message);
        }

        // Helpers that take a scope (never null) and helpers for the calls without one.
        private Box<IosClipboardOperationResult> Copy(IosPasteboardScope scope, IosClipboardContent content, IosClipboardCopyOptions? options = null) =>
            Track<IosClipboardOperationResult>(cb => M.Copy(content, scope, options, cb));
        private Box<IosClipboardOperationResult> Append(IosPasteboardScope scope, IosClipboardContent content) =>
            Track<IosClipboardOperationResult>(cb => M.Append(content, scope, cb));
        private Box<IosClipboardOperationResult> Clear(IosPasteboardScope scope) => Track<IosClipboardOperationResult>(cb => M.Clear(scope, cb));
        private Box<IosClipboardOperationResult> Remove(IosPasteboardScope scope) => Track<IosClipboardOperationResult>(cb => M.RemovePasteboard(scope, cb));
        private Box<IosClipboardReadResult> Read(IosPasteboardScope scope) => Track<IosClipboardReadResult>(cb => M.Read(scope, cb));
        private Box<IosClipboardReadDataResult> ReadData(IosPasteboardScope scope, string type) => Track<IosClipboardReadDataResult>(cb => M.ReadData(type, scope, cb));
        private Box<IosClipboardSnapshotResult> Snapshot(IosPasteboardScope scope, string[]? types) => Track<IosClipboardSnapshotResult>(cb => M.GetSnapshot(scope, types, cb));
        private Box<IosClipboardDetectedPatternsResult> DetectPatterns(IosPasteboardScope scope, params IosClipboardDetectionPattern[] p) =>
            Track<IosClipboardDetectedPatternsResult>(cb => M.DetectPatterns(p, scope, cb));
        private Box<IosClipboardDetectedValuesResult> DetectValues(IosPasteboardScope scope, params IosClipboardDetectionPattern[] p) =>
            Track<IosClipboardDetectedValuesResult>(cb => M.DetectValues(p, scope, cb));
        private Box<IosClipboardLoadedItemResult> Load(IosPasteboardScope scope, IosClipboardLoadRequest request) =>
            Track<IosClipboardLoadedItemResult>(cb => M.LoadItem(request, scope, cb));
        private Box<IosClipboardForegroundChangeResult> CheckForeground(IosPasteboardScope scope) =>
            Track<IosClipboardForegroundChangeResult>(cb => M.CheckForegroundChange(scope, cb));
        private Box<IosClipboardOperationResult> StartObserving(IosPasteboardScope scope, Action<IosClipboardChangeEvent>? onChanged = null)
        {
            _observing = true;
            return Track<IosClipboardOperationResult>(cb => M.StartObserving(scope, onChanged, cb));
        }
        private Box<IosClipboardOperationResult> StopObserving() => Track<IosClipboardOperationResult>(cb => M.StopObserving(cb));
        private Box<IosClipboardOperationResult> CancelLoads() => Track<IosClipboardOperationResult>(cb => M.CancelLoads(cb));
        private Box<IosPasteboardScopeResult> Create(IosPasteboardCreationRequest request) => Track<IosPasteboardScopeResult>(cb => M.CreatePasteboard(request, cb));

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _tracked.Clear();
            _observing = false;
            _extra = null;
            string name = Prefix + TestContext.CurrentContext.Test.Name;
            _scope = IosPasteboardScope.Named(name);
            var created = Create(IosPasteboardCreationRequest.Named(name));
            yield return Await(created, "CreatePasteboard");
            Assert.IsTrue(created.V.IsSuccess, $"CreatePasteboard failed: {created.V.Error?.Code}");
            // C ABI design v2, table 2.1: the kind comes from the request, the name from the library.
            Assert.AreEqual(IosPasteboardScopeKind.Named, created.V.Scope!.Kind);
            Assert.AreEqual(name, created.V.Scope.Name);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            try
            {
                if (_observing)
                {
                    var stopped = StopObserving();
                    yield return Await(stopped, "StopObserving");
                    Assert.IsTrue(stopped.V.IsSuccess, $"StopObserving failed: {stopped.V.Error?.Code}");
                }
                if (_extra != null)
                {
                    var removedExtra = Remove(_extra);
                    yield return Await(removedExtra, "RemovePasteboard");
                }
                var removed = Remove(_scope);
                yield return Await(removed, "RemovePasteboard");
                // A test may have removed its pasteboard already (CP-19, CP-25).
                Assert.IsTrue(removed.V.IsSuccess || removed.V.Error?.Code == "CLIPBOARD_UNAVAILABLE",
                    $"RemovePasteboard failed: {removed.V.Error?.Code}");
                yield return WaitFor(() => _tracked.TrueForAll(c => c.count() >= 1), 5f);
                yield return WaitFor(() => false, 0.5f);
                foreach (var (what, count) in _tracked)
                    Assert.AreEqual(1, count(), $"a {what} call of this test did not complete exactly once");
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
            }
        }

        private static string Hash(byte[] data)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "").Substring(0, 16);
        }

        private static byte[] Png()
        {
            var texture = new Texture2D(2, 2);
            texture.SetPixels(new[] { Color.red, Color.green, Color.blue, Color.white });
            texture.Apply();
            return texture.EncodeToPNG();
        }

        // 1.1 Reading and writing

        [UnityTest]
        public IEnumerator CP1_PlainTextRoundTrips()
        {
            var copied = Copy(_scope, IosClipboardContent.PlainText("ntk player round trip"));
            yield return Await(copied, "Copy");
            Assert.IsTrue(copied.V.IsSuccess);
            var read = Read(_scope);
            yield return Await(read, "Read");
            Assert.IsTrue(read.V.IsSuccess);
            Assert.AreEqual(1, read.V.Items.Count);
            Assert.AreEqual("ntk player round trip", read.V.Items[0].Text);
        }

        [UnityTest]
        public IEnumerator CP2_HtmlTextHasPlainAndHtml()
        {
            const string html = "<b>ntk</b>";
            var copied = Copy(_scope, IosClipboardContent.HtmlText("ntk", html));
            yield return Await(copied, "Copy");
            Assert.IsTrue(copied.V.IsSuccess);
            var read = Read(_scope);
            yield return Await(read, "Read");
            Assert.AreEqual(1, read.V.Items.Count);
            Record($"types={string.Join(",", read.V.Items[0].TypeIdentifiers)}");
            CollectionAssert.Contains(read.V.Items[0].TypeIdentifiers, "public.html");
            CollectionAssert.Contains(read.V.Items[0].TypeIdentifiers, "public.plain-text");
            Assert.AreEqual("ntk", read.V.Items[0].Text);
            var data = ReadData(_scope, "public.html");
            yield return Await(data, "ReadData");
            Assert.IsTrue(data.V.HasData);
            Assert.AreEqual("public.html", data.V.UtType);
            CollectionAssert.AreEqual(Encoding.UTF8.GetBytes(html), data.V.Data);
        }

        [UnityTest]
        public IEnumerator CP3_UrlRoundTrips()
        {
            var copied = Copy(_scope, IosClipboardContent.Url("https://example.com/ntk"));
            yield return Await(copied, "Copy");
            Assert.IsTrue(copied.V.IsSuccess);
            var read = Read(_scope);
            yield return Await(read, "Read");
            Assert.AreEqual("https://example.com/ntk", read.V.Items[0].UrlString);
        }

        [UnityTest]
        public IEnumerator CP4_ImageDataRoundTrips()
        {
            byte[] png = Png();
            var copied = Copy(_scope, IosClipboardContent.ImageData(png, "public.png"));
            yield return Await(copied, "Copy");
            Assert.IsTrue(copied.V.IsSuccess);
            var data = ReadData(_scope, "public.png");
            yield return Await(data, "ReadData");
            Assert.IsTrue(data.V.HasData);
            Record($"sent={png.Length}/{Hash(png)} read={data.V.ByteCount}/{Hash(data.V.Data!)}");
            CollectionAssert.AreEqual(png, data.V.Data, "1.x keeps the PNG bytes as they were sent");
        }

        [UnityTest]
        public IEnumerator CP5_ColorIsCopied()
        {
            var copied = Copy(_scope, IosClipboardContent.Color(1, 0, 0, 1));
            yield return Await(copied, "Copy");
            Assert.IsTrue(copied.V.IsSuccess);
            var read = Read(_scope);
            yield return Await(read, "Read");
            Assert.AreEqual(1, read.V.Items.Count);
            Record($"types={string.Join(",", read.V.Items[0].TypeIdentifiers)}");
            CollectionAssert.Contains(read.V.Items[0].TypeIdentifiers, "com.apple.uikit.color");
        }

        [UnityTest]
        public IEnumerator CP6_CustomDataRoundTrips()
        {
            byte[] bytes = { 1, 2, 3, 250 };
            var copied = Copy(_scope, IosClipboardContent.CustomData(bytes, CustomType));
            yield return Await(copied, "Copy");
            Assert.IsTrue(copied.V.IsSuccess);
            var data = ReadData(_scope, CustomType);
            yield return Await(data, "ReadData");
            Assert.AreEqual(CustomType, data.V.UtType);
            CollectionAssert.AreEqual(bytes, data.V.Data);
        }

        [UnityTest]
        public IEnumerator CP7_ReadDataOfAMissingTypeHasNoData()
        {
            var copied = Copy(_scope, IosClipboardContent.PlainText("ntk"));
            yield return Await(copied, "Copy");
            var data = ReadData(_scope, "com.jonghyunkim.ntk.absent");
            yield return Await(data, "ReadData");
            Assert.IsTrue(data.V.IsSuccess);
            Assert.IsFalse(data.V.HasData);
            Assert.IsNull(data.V.Data);
        }

        [UnityTest]
        public IEnumerator CP8_MultipleTextIsTwoItems()
        {
            var copied = Copy(_scope, IosClipboardContent.MultipleText(new[] { "a", "b" }));
            yield return Await(copied, "Copy");
            var read = Read(_scope);
            yield return Await(read, "Read");
            Assert.AreEqual(2, read.V.Items.Count);
        }

        [UnityTest]
        public IEnumerator CP9_MultiRepresentationIsOneItemOfTwoTypes()
        {
            var copied = Copy(_scope, IosClipboardContent.MultiRepresentation(new Dictionary<string, byte[]>
            {
                ["public.plain-text"] = Encoding.UTF8.GetBytes("ntk"),
                [CustomType] = new byte[] { 1 },
            }));
            yield return Await(copied, "Copy");
            Assert.IsTrue(copied.V.IsSuccess);
            var read = Read(_scope);
            yield return Await(read, "Read");
            Assert.AreEqual(1, read.V.Items.Count);
            Record($"types={string.Join(",", read.V.Items[0].TypeIdentifiers)}");
            CollectionAssert.Contains(read.V.Items[0].TypeIdentifiers, CustomType);
            CollectionAssert.Contains(read.V.Items[0].TypeIdentifiers, "public.plain-text");
            var custom = ReadData(_scope, CustomType);
            yield return Await(custom, "ReadData");
            CollectionAssert.AreEqual(new byte[] { 1 }, custom.V.Data);
            var plain = ReadData(_scope, "public.plain-text");
            yield return Await(plain, "ReadData");
            CollectionAssert.AreEqual(Encoding.UTF8.GetBytes("ntk"), plain.V.Data);
        }

        [UnityTest]
        public IEnumerator CP10_AppendAddsAnItem()
        {
            var copied = Copy(_scope, IosClipboardContent.PlainText("ntk"));
            yield return Await(copied, "Copy");
            var appended = Append(_scope, IosClipboardContent.Url("https://example.com/ntk"));
            yield return Await(appended, "Append");
            Assert.IsTrue(appended.V.IsSuccess);
            var read = Read(_scope);
            yield return Await(read, "Read");
            Assert.AreEqual(2, read.V.Items.Count);
        }

        [UnityTest]
        public IEnumerator CP11_ClearLeavesNoItems()
        {
            var copied = Copy(_scope, IosClipboardContent.PlainText("ntk"));
            yield return Await(copied, "Copy");
            var cleared = Clear(_scope);
            yield return Await(cleared, "Clear");
            Assert.IsTrue(cleared.V.IsSuccess);
            var read = Read(_scope);
            yield return Await(read, "Read");
            Assert.IsTrue(read.V.IsSuccess);
            Assert.AreEqual(0, read.V.Items.Count);
        }

        [UnityTest]
        public IEnumerator CP12_SnapshotMatchesOnlyTheUrlItem()
        {
            var copied = Copy(_scope, IosClipboardContent.MultipleText(new[] { "ntk" }));
            yield return Await(copied, "Copy");
            var appended = Append(_scope, IosClipboardContent.Url("https://example.com/ntk"));
            yield return Await(appended, "Append");
            var snap = Snapshot(_scope, new[] { "public.url" });
            yield return Await(snap, "GetSnapshot");
            Assert.IsTrue(snap.V.IsSuccess);
            var s = snap.V.Snapshot!;
            Record($"types={string.Join(",", s.TypeIdentifiers)} all={string.Join("|", s.AllTypeIdentifiers.Select(t => string.Join(",", t)))}");
            Record($"has strings={s.HasStrings} urls={s.HasUrls} images={s.HasImages} colors={s.HasColors}");
            Assert.IsTrue(s.HasStrings);
            Assert.IsTrue(s.HasUrls);
            Assert.IsFalse(s.HasImages);
            Assert.IsFalse(s.HasColors);
            Assert.AreEqual(2, s.NumberOfItems);
            CollectionAssert.AreEqual(new[] { "public.plain-text" }, s.TypeIdentifiers);
            Assert.AreEqual(2, s.AllTypeIdentifiers.Count);
            CollectionAssert.Contains(s.AllTypeIdentifiers[1], "public.url");
            CollectionAssert.AreEqual(new[] { 1 }, s.MatchingItemIndexes);
        }

        /// <summary>
        /// 1.x, measured on the simulator (2026-11-14): on a named pasteboard, detection finds nothing in a
        /// URL item, and finds it in the same URL put as plain text.
        /// </summary>
        [UnityTest]
        public IEnumerator CP13_DetectPatternsOnANamedPasteboard()
        {
            var copied = Copy(_scope, IosClipboardContent.Url("https://example.com/ntk"));
            yield return Await(copied, "Copy");
            var found = DetectPatterns(_scope, IosClipboardDetectionPattern.ProbableWebUrl);
            yield return Await(found, "DetectPatterns");
            Assert.IsTrue(found.V.IsSuccess);
            CollectionAssert.IsEmpty(found.V.Patterns);
            var text = Copy(_scope, IosClipboardContent.PlainText("https://example.com/ntk"));
            yield return Await(text, "Copy");
            var fromText = DetectPatterns(_scope, IosClipboardDetectionPattern.ProbableWebUrl);
            yield return Await(fromText, "DetectPatterns");
            Assert.IsTrue(fromText.V.IsSuccess);
            CollectionAssert.AreEqual(new[] { IosClipboardDetectionPattern.ProbableWebUrl }, fromText.V.Patterns);
        }

        [UnityTest]
        public IEnumerator CP14_DetectValuesOnANamedPasteboard()
        {
            var copied = Copy(_scope, IosClipboardContent.Url("https://example.com/ntk"));
            yield return Await(copied, "Copy");
            var found = DetectValues(_scope, IosClipboardDetectionPattern.ProbableWebUrl);
            yield return Await(found, "DetectValues");
            Assert.IsTrue(found.V.IsSuccess);
            Assert.IsNull(found.V.Values!.ProbableWebUrl);
            var text = Copy(_scope, IosClipboardContent.PlainText("https://example.com/ntk"));
            yield return Await(text, "Copy");
            var fromText = DetectValues(_scope, IosClipboardDetectionPattern.ProbableWebUrl);
            yield return Await(fromText, "DetectValues");
            Assert.IsTrue(fromText.V.IsSuccess);
            Assert.AreEqual("https://example.com/ntk", fromText.V.Values!.ProbableWebUrl);
        }

        [UnityTest]
        public IEnumerator CP15_LoadItemTextAndUrl()
        {
            var copied = Copy(_scope, IosClipboardContent.PlainText("ntk load"));
            yield return Await(copied, "Copy");
            var text = Load(_scope, IosClipboardLoadRequest.Text);
            yield return Await(text, "LoadItem(Text)");
            Assert.IsTrue(text.V.IsSuccess);
            Assert.AreEqual("ntk load", text.V.Item!.Text);
            // 1.x, measured: a plain-text item gives no URL.
            IgnoreErrorLogs();
            var url = Load(_scope, IosClipboardLoadRequest.Url);
            yield return Await(url, "LoadItem(Url)");
            ExpectError(url.V.Error, "CLIPBOARD_NO_MATCHING_ITEM", "No clipboard item matches the requested type.");
        }

        [UnityTest]
        public IEnumerator CP18_ForegroundChangeIsFalseFirstThenTrue()
        {
            var first = CheckForeground(_scope);
            yield return Await(first, "CheckForegroundChange");
            Assert.IsTrue(first.V.IsSuccess);
            Assert.IsFalse(first.V.Changed);
            var copied = Copy(_scope, IosClipboardContent.PlainText("ntk"));
            yield return Await(copied, "Copy");
            var second = CheckForeground(_scope);
            yield return Await(second, "CheckForegroundChange");
            Record($"second changed={second.V.Changed}");
            Assert.IsTrue(second.V.Changed);
        }

        [UnityTest]
        public IEnumerator CP19_UniquePasteboardIsCreatedAndRemoved()
        {
            var created = Create(IosPasteboardCreationRequest.Unique);
            yield return Await(created, "CreatePasteboard(Unique)");
            Assert.IsTrue(created.V.IsSuccess);
            var unique = created.V.Scope!;
            _extra = unique;
            Assert.AreEqual(IosPasteboardScopeKind.Unique, unique.Kind);
            Assert.IsFalse(string.IsNullOrEmpty(unique.Name));
            var removed = Remove(unique);
            yield return Await(removed, "RemovePasteboard");
            Assert.IsTrue(removed.V.IsSuccess);
            IgnoreErrorLogs();
            var read = Read(unique);
            yield return Await(read, "Read");
            ExpectError(read.V.Error, "CLIPBOARD_UNAVAILABLE", "The requested pasteboard is unavailable.");
        }

        [UnityTest]
        public IEnumerator CP22a_ExpirationRemovesTheContent()
        {
            var copied = Copy(_scope, IosClipboardContent.PlainText("ntk"), IosClipboardCopyOptions.Create(true, DateTime.UtcNow.AddSeconds(3)));
            yield return Await(copied, "Copy");
            Assert.IsTrue(copied.V.IsSuccess);
            var now = Read(_scope);
            yield return Await(now, "Read");
            Assert.AreEqual(1, now.V.Items.Count);
            yield return WaitFor(() => false, 6f);
            var later = Read(_scope);
            yield return Await(later, "Read");
            Record($"after-expiry items={later.V.Items?.Count}");
            Assert.AreEqual(0, later.V.Items!.Count);
        }

        // 1.2 Errors

        // CP-20 is in IosClipboardCSharpPlayerTests below: it reaches no native code and runs after 3-S too.

        [UnityTest]
        public IEnumerator CP21_RemovingTheGeneralPasteboardFails()
        {
            // The one direct use of General: RemovePasteboard neither reads nor writes it.
            var removed = Remove(IosPasteboardScope.General);
            yield return Await(removed, "RemovePasteboard(General)");
            ExpectError(removed.V.Error, "CLIPBOARD_CANNOT_REMOVE_GENERAL", "The general pasteboard cannot be removed.");
        }

        [UnityTest]
        public IEnumerator CP22_PastExpirationFailsAndKeepsTheContent()
        {
            var first = Copy(_scope, IosClipboardContent.PlainText("ntk kept"));
            yield return Await(first, "Copy");
            var second = Copy(_scope, IosClipboardContent.PlainText("ntk expired"), IosClipboardCopyOptions.Create(true, DateTime.UtcNow.AddMinutes(-1)));
            yield return Await(second, "Copy");
            ExpectError(second.V.Error, "CLIPBOARD_INVALID_EXPIRATION", "expirationDate must be in the future.");
            var read = Read(_scope);
            yield return Await(read, "Read");
            Assert.AreEqual(1, read.V.Items.Count);
            Assert.AreEqual("ntk kept", read.V.Items[0].Text);
        }

        [UnityTest]
        public IEnumerator CP25_ReadingARemovedPasteboardFails()
        {
            var removed = Remove(_scope);
            yield return Await(removed, "RemovePasteboard");
            Assert.IsTrue(removed.V.IsSuccess);
            IgnoreErrorLogs();
            var read = Read(_scope);
            yield return Await(read, "Read");
            ExpectError(read.V.Error, "CLIPBOARD_UNAVAILABLE", "The requested pasteboard is unavailable.");
        }

        [UnityTest]
        public IEnumerator CP26_LoadingAnImageFromTextFails()
        {
            var copied = Copy(_scope, IosClipboardContent.PlainText("ntk"));
            yield return Await(copied, "Copy");
            IgnoreErrorLogs();
            var loaded = Load(_scope, IosClipboardLoadRequest.Image);
            yield return Await(loaded, "LoadItem(Image)");
            ExpectError(loaded.V.Error, "CLIPBOARD_NO_MATCHING_ITEM", "No clipboard item matches the requested type.");
        }

        [UnityTest]
        public IEnumerator CP27_ColorOutOfRangeFails()
        {
            var copied = Copy(_scope, IosClipboardContent.Color(1.5, 0, 0, 1));
            yield return Await(copied, "Copy");
            ExpectError(copied.V.Error, "CLIPBOARD_INVALID_COLOR", "Color components must be finite and within 0.0...1.0.");
        }

        [UnityTest]
        public IEnumerator CP28_TypeWithoutADotFails()
        {
            var copied = Copy(_scope, IosClipboardContent.CustomData(new byte[] { 1 }, "ntkcustom"));
            yield return Await(copied, "Copy");
            ExpectError(copied.V.Error, "CLIPBOARD_INVALID_TYPE", "The uniform type identifier is invalid.");
        }

        [UnityTest]
        public IEnumerator CP29_UndecodableImageFails()
        {
            var copied = Copy(_scope, IosClipboardContent.ImageData(Encoding.ASCII.GetBytes("not an image"), "public.png"));
            yield return Await(copied, "Copy");
            ExpectError(copied.V.Error, "CLIPBOARD_INVALID_IMAGE_DATA", "The provided image data could not be decoded.");
        }

        [UnityTest]
        public IEnumerator CP30_InvalidUrlFails()
        {
            var copied = Copy(_scope, IosClipboardContent.Url("not a url"));
            yield return Await(copied, "Copy");
            ExpectError(copied.V.Error, "CLIPBOARD_INVALID_URL", "The URL is invalid.");
        }

        [UnityTest]
        public IEnumerator CP23_SecondCallOfTheSameKindIsBusy()
        {
            int events = 0;
            Action<IosClipboardReadResult> onEvent = _ => events++;
            M.ReadCompleted += onEvent;
            try
            {
                var first = Read(_scope);
                var second = Read(_scope);
                yield return Await(first, "Read");
                yield return Await(second, "Read");
                Assert.IsTrue(first.V.IsSuccess);
                Assert.AreEqual(IosClipboardManager.BusyErrorCode, second.V.Error?.Code);
                yield return WaitFor(() => false, 1f);
                Assert.AreEqual(2, events);
            }
            finally
            {
                M.ReadCompleted -= onEvent;
            }
        }

        // 1.3 Cancelling and observing

        [UnityTest]
        public IEnumerator CP16_CancelLoadsWithNothingLoading()
        {
            var cancelled = CancelLoads();
            yield return Await(cancelled, "CancelLoads");
            Assert.IsTrue(cancelled.V.IsSuccess);
        }

        [UnityTest]
        public IEnumerator CP31_CancelLoadsCancelsALoadInFlight()
        {
            var copied = Copy(_scope, IosClipboardContent.PlainText("ntk"));
            yield return Await(copied, "Copy");
            var order = new List<string>();
            var loaded = Track<IosClipboardLoadedItemResult>(cb => M.LoadItem(IosClipboardLoadRequest.Text, _scope, r => { order.Add("load"); cb(r); }));
            var cancelled = Track<IosClipboardOperationResult>(cb => M.CancelLoads(r => { order.Add("cancel"); cb(r); }));
            yield return Await(loaded, "LoadItem");
            yield return Await(cancelled, "CancelLoads");
            yield return WaitFor(() => false, 1f);
            Record($"order={string.Join(",", order)}");
            Assert.AreEqual(1, loaded.Count);
            ExpectError(loaded.V.Error, "CLIPBOARD_CANCELLED", "The clipboard load was cancelled.");
            Assert.AreEqual(1, cancelled.Count);
            Assert.IsTrue(cancelled.V.IsSuccess);
            CollectionAssert.AreEqual(new[] { "cancel", "load" }, order, "1.x: the cancel result first");
        }

        private IEnumerator CountChanges(Func<IEnumerator> body, List<IosClipboardChangeEvent> events)
        {
            Action<IosClipboardChangeEvent> onEvent = e => events.Add(e);
            M.ClipboardChanged += onEvent;
            try
            {
                yield return body();
            }
            finally
            {
                M.ClipboardChanged -= onEvent;
            }
        }

        private static string Describe(IEnumerable<IosClipboardChangeEvent> events) =>
            string.Join(";", events.Select(e => $"{e.Kind}+{string.Join(",", e.TypesAdded)}"));

        [UnityTest]
        public IEnumerator CP17_ObservingGivesOneChange()
        {
            var events = new List<IosClipboardChangeEvent>();
            int perCall = 0;
            yield return CountChanges(() => Body(), events);
            IEnumerator Body()
            {
                var started = StartObserving(_scope, _ => perCall++);
                yield return Await(started, "StartObserving");
                Assert.IsTrue(started.V.IsSuccess);
                var copied = Copy(_scope, IosClipboardContent.PlainText("ntk"));
                yield return Await(copied, "Copy");
                yield return WaitFor(() => false, Window);
            }
            // 1.x, measured on the simulator (2026-11-14): one Copy gives two changes, the first without
            // types and the second with the plain-text type.
            Record($"events={events.Count} on-changed={perCall} kinds={Describe(events)}");
            Assert.AreEqual("Changed+;Changed+public.plain-text", Describe(events));
            Assert.AreEqual(2, perCall);
            foreach (var e in events)
            {
                Assert.AreEqual(IosPasteboardScopeKind.Named, e.Scope?.Kind, "the change's scope");
                Assert.AreEqual(_scope.Name, e.Scope!.Name);
            }
        }

        [UnityTest]
        public IEnumerator CP32_SecondStartReplacesTheFirst()
        {
            var events = new List<IosClipboardChangeEvent>();
            int a = 0, b = 0;
            yield return CountChanges(() => Body(), events);
            IEnumerator Body()
            {
                var first = StartObserving(_scope, _ => a++);
                yield return Await(first, "StartObserving");
                var second = StartObserving(_scope, _ => b++);
                yield return Await(second, "StartObserving");
                var copied = Copy(_scope, IosClipboardContent.PlainText("ntk"));
                yield return Await(copied, "Copy");
                yield return WaitFor(() => false, Window);
            }
            Record($"a={a} b={b} events={events.Count}");
            Assert.AreEqual(0, a);
            Assert.AreEqual(2, b); // two changes per Copy (CP17)
            Assert.AreEqual(2, events.Count);
        }

        [UnityTest]
        public IEnumerator CP33_NoChangeAfterStop()
        {
            var events = new List<IosClipboardChangeEvent>();
            int beforeStop = -1;
            yield return CountChanges(() => Body(), events);
            IEnumerator Body()
            {
                var started = StartObserving(_scope);
                yield return Await(started, "StartObserving");
                Assert.IsTrue(started.V.IsSuccess);
                var before = Copy(_scope, IosClipboardContent.PlainText("ntk before"));
                yield return Await(before, "Copy");
                Assert.IsTrue(before.V.IsSuccess);
                yield return WaitFor(() => false, Window);
                beforeStop = events.Count;
                var stopped = StopObserving();
                yield return Await(stopped, "StopObserving");
                Assert.IsTrue(stopped.V.IsSuccess);
                var copied = Copy(_scope, IosClipboardContent.PlainText("ntk after"));
                yield return Await(copied, "Copy");
                Assert.IsTrue(copied.V.IsSuccess);
                yield return WaitFor(() => false, Window);
            }
            // The same observation delivered the Copy before the stop (two changes per Copy, CP17).
            Assert.AreEqual(2, beforeStop, Describe(events));
            Assert.AreEqual(2, events.Count, Describe(events));
        }

        [UnityTest]
        public IEnumerator CP34_StopBeforeStartCompletesIsBusy()
        {
            var started = StartObserving(_scope);
            var stopped = StopObserving();
            yield return Await(started, "StartObserving");
            yield return Await(stopped, "StopObserving");
            Assert.IsTrue(started.V.IsSuccess);
            Assert.AreEqual(IosClipboardManager.BusyErrorCode, stopped.V.Error?.Code);
        }

        [UnityTest]
        public IEnumerator CP35_RemovingAnObservedPasteboard()
        {
            var events = new List<IosClipboardChangeEvent>();
            yield return CountChanges(() => Body(), events);
            IEnumerator Body()
            {
                var started = StartObserving(_scope);
                yield return Await(started, "StartObserving");
                var removed = Remove(_scope);
                yield return Await(removed, "RemovePasteboard");
                yield return WaitFor(() => false, Window);
            }
            Record($"events={events.Count} kinds={Describe(events)}");
            Assert.AreEqual("Removed+", Describe(events));
        }

#if NTK_IOS_CABI_2_CLIPBOARD
        // C ABI 2.0.0 only (artifact/ios/clipboard, C ABI design v2).

        /// <summary>D-4, D-12: a call the library rejects at once fails on a later frame; until then the kind is busy.</summary>
        [UnityTest]
        public IEnumerator CP36_RejectedAtEntryKeepsTheKindBusyUntilDelivered()
        {
            var first = Copy(_scope, IosClipboardContent.MultipleText(new string[0]));
            var second = Copy(_scope, IosClipboardContent.PlainText("ntk"));
            Assert.AreEqual(0, first.Count + second.Count, "a result came inside the call");
            yield return Await(first, "Copy");
            yield return Await(second, "Copy");
            ExpectError(first.V.Error, "CLIPBOARD_EMPTY_ITEMS", "No items provided for clipboard copy.");
            Assert.AreEqual(IosClipboardManager.BusyErrorCode, second.V.Error?.Code);
        }

        /// <summary>D-12: a call made again from the callback of a rejected call is not delivered in the same frame.</summary>
        [UnityTest]
        public IEnumerator CP37_RecallingFromTheCallbackWaitsForAnotherFrame()
        {
            int frameOfFirst = -1, frameOfSecond = -1;
            var second = new Box<IosClipboardOperationResult>();
            var first = Track<IosClipboardOperationResult>(cb => M.Copy(IosClipboardContent.MultipleText(new string[0]), _scope, null, r =>
            {
                frameOfFirst = Time.frameCount;
                cb(r);
                _tracked.Add(("second", () => second.Count));
                M.Copy(IosClipboardContent.MultipleText(new string[0]), _scope, null, r2 =>
                {
                    frameOfSecond = Time.frameCount;
                    second.Value = r2;
                    second.Count++;
                });
            }));
            yield return Await(first, "Copy");
            yield return Await(second, "Copy");
            Record($"frames first={frameOfFirst} second={frameOfSecond}");
            Assert.Greater(frameOfSecond, frameOfFirst);
            Assert.AreEqual("CLIPBOARD_EMPTY_ITEMS", second.V.Error?.Code);
        }

        /// <summary>D-8 (5): a name or a type holding U+0000 fails in C#; a value holding it is cut, with a warning.</summary>
        [UnityTest]
        public IEnumerator CP38_NulInANameFailsAndInAValueIsCut()
        {
            var named = Read(IosPasteboardScope.Named(_scope.Name + "\0other"));
            yield return Await(named, "Read");
            ExpectError(named.V.Error, "CLIPBOARD_INVALID_REQUEST", "The request is invalid.");
            var typed = ReadData(_scope, "public.plain-text\0x");
            yield return Await(typed, "ReadData");
            ExpectError(typed.V.Error, "CLIPBOARD_INVALID_REQUEST", "The request is invalid.");

            LogAssert.Expect(LogType.Warning, new Regex("U\\+0000"));
            var copied = Copy(_scope, IosClipboardContent.PlainText("ntk\0secret"));
            yield return Await(copied, "Copy");
            Assert.IsTrue(copied.V.IsSuccess);
            var read = Read(_scope);
            yield return Await(read, "Read");
            Assert.AreEqual("ntk", read.V.Items[0].Text);
        }

        /// <summary>D-8 (4): a pattern outside the enum fails in C# instead of being taken for another pattern.</summary>
        [UnityTest]
        public IEnumerator CP39_PatternOutsideTheEnumFails()
        {
            var found = DetectPatterns(_scope, (IosClipboardDetectionPattern)35);
            yield return Await(found, "DetectPatterns");
            ExpectError(found.V.Error, "CLIPBOARD_INVALID_REQUEST", "The request is invalid.");
        }

        /// <summary>D-6 (design v3): a stop rejected as busy leaves the observation on; changes still come.</summary>
        [UnityTest]
        public IEnumerator CP40_BusyStopKeepsObserving()
        {
            var events = new List<IosClipboardChangeEvent>();
            int perCall = 0;
            yield return CountChanges(() => Body(), events);
            IEnumerator Body()
            {
                var started = StartObserving(_scope, _ => perCall++);
                var stopped = StopObserving();
                yield return Await(started, "StartObserving");
                yield return Await(stopped, "StopObserving");
                Assert.IsTrue(started.V.IsSuccess);
                Assert.AreEqual(IosClipboardManager.BusyErrorCode, stopped.V.Error?.Code);
                var copied = Copy(_scope, IosClipboardContent.PlainText("ntk"));
                yield return Await(copied, "Copy");
                yield return WaitFor(() => false, Window);
            }
            Record($"events={events.Count} on-changed={perCall}");
            Assert.AreEqual(2, events.Count, Describe(events));
            Assert.AreEqual(2, perCall);
        }
#endif
    }
#endif

    /// <summary>
    /// iOS player: the Clipboard tests that reach no native code, without the fixture's set-up (3-S design
    /// v3, 1.7): CP-20, and until Clipboard moves to the C ABI, the unavailable path of every operation.
    /// </summary>
    [Category(IosPlayerTestSupport.Category)]
    public class IosClipboardCSharpPlayerTests
    {
        [Test]
        public void CP20_BlankNameThrowsInCSharp()
        {
            Assert.Throws<ArgumentException>(() => IosPasteboardCreationRequest.Named("   "));
        }

#if NTK_IOS_CABI_SWITCHED && !NTK_IOS_CABI_2_CLIPBOARD
        private const string Unavailable = "CLIPBOARD_BRIDGE_UNAVAILABLE";

        /// <summary>
        /// 3-S: every operation fails with CLIPBOARD_BRIDGE_UNAVAILABLE and "&lt;operation&gt; is only
        /// available on an iOS device." Nothing comes inside the call; after the next Update each
        /// operation has its event and then its callback, once each; nothing more within a second.
        /// </summary>
        [UnityTest]
        public IEnumerator UnavailableUntilMoved()
        {
            LogAssert.ignoreFailingMessages = true;
            var m = IosClipboardManager.Instance;
            var seen = new List<(string op, string source, bool ok, string? code, string? message)>();
            void Add(string op, string source, bool ok, IosClipboardErrorInfo? e) => seen.Add((op, source, ok, e?.Code, e?.Message));

            Action<IosClipboardOperationResult> onOperation = r => Add(r.Operation, "event", r.IsSuccess, r.Error);
            Action<IosClipboardReadResult> onRead = r => Add("read", "event", r.IsSuccess, r.Error);
            Action<IosClipboardReadDataResult> onReadData = r => Add("readData", "event", r.IsSuccess, r.Error);
            Action<IosClipboardSnapshotResult> onSnapshot = r => Add("getSnapshot", "event", r.IsSuccess, r.Error);
            Action<IosPasteboardScopeResult> onCreated = r => Add("createPasteboard", "event", r.IsSuccess, r.Error);
            Action<IosClipboardDetectedPatternsResult> onPatterns = r => Add("detectPatterns", "event", r.IsSuccess, r.Error);
            Action<IosClipboardDetectedValuesResult> onValues = r => Add("detectValues", "event", r.IsSuccess, r.Error);
            Action<IosClipboardLoadedItemResult> onLoaded = r => Add("loadItem", "event", r.IsSuccess, r.Error);
            Action<IosClipboardForegroundChangeResult> onForeground = r => Add("checkForegroundChange", "event", r.IsSuccess, r.Error);
            m.ClipboardOperationCompleted += onOperation;
            m.ReadCompleted += onRead;
            m.ReadDataCompleted += onReadData;
            m.SnapshotCompleted += onSnapshot;
            m.PasteboardCreated += onCreated;
            m.PatternsDetected += onPatterns;
            m.ValuesDetected += onValues;
            m.ItemLoaded += onLoaded;
            m.ForegroundChangeChecked += onForeground;
            try
            {
                var scope = IosPasteboardScope.Named("com.jonghyunkim.nativetoolkit.playertests.unavailable");
                var link = new[] { IosClipboardDetectionPattern.Link };
                m.Copy(IosClipboardContent.PlainText("x"), scope, null, r => Add(r.Operation, "callback", r.IsSuccess, r.Error));
                m.Append(IosClipboardContent.PlainText("x"), scope, r => Add(r.Operation, "callback", r.IsSuccess, r.Error));
                m.Clear(scope, r => Add(r.Operation, "callback", r.IsSuccess, r.Error));
                m.RemovePasteboard(scope, r => Add(r.Operation, "callback", r.IsSuccess, r.Error));
                m.CancelLoads(r => Add(r.Operation, "callback", r.IsSuccess, r.Error));
                m.StartObserving(scope, null, r => Add(r.Operation, "callback", r.IsSuccess, r.Error));
                m.StopObserving(r => Add(r.Operation, "callback", r.IsSuccess, r.Error));
                m.Read(scope, r => Add("read", "callback", r.IsSuccess, r.Error));
                m.ReadData("public.png", scope, r => Add("readData", "callback", r.IsSuccess, r.Error));
                m.GetSnapshot(scope, null, r => Add("getSnapshot", "callback", r.IsSuccess, r.Error));
                m.CreatePasteboard(IosPasteboardCreationRequest.Unique, r => Add("createPasteboard", "callback", r.IsSuccess, r.Error));
                m.DetectPatterns(link, scope, r => Add("detectPatterns", "callback", r.IsSuccess, r.Error));
                m.DetectValues(link, scope, r => Add("detectValues", "callback", r.IsSuccess, r.Error));
                m.LoadItem(IosClipboardLoadRequest.Text, scope, r => Add("loadItem", "callback", r.IsSuccess, r.Error));
                m.CheckForegroundChange(scope, r => Add("checkForegroundChange", "callback", r.IsSuccess, r.Error));
                Assert.AreEqual(0, seen.Count, "a result came inside the call");

                yield return null;
                var operations = new[]
                {
                    "copy", "append", "clear", "removePasteboard", "cancelLoads", "startObserving", "stopObserving",
                    "read", "readData", "getSnapshot", "createPasteboard", "detectPatterns", "detectValues", "loadItem",
                    "checkForegroundChange",
                };
                AssertEachOnce(seen, operations, "after the next Update");

                yield return WaitFor(() => false, 1f);
                AssertEachOnce(seen, operations, "a second later");
            }
            finally
            {
                m.ClipboardOperationCompleted -= onOperation;
                m.ReadCompleted -= onRead;
                m.ReadDataCompleted -= onReadData;
                m.SnapshotCompleted -= onSnapshot;
                m.PasteboardCreated -= onCreated;
                m.PatternsDetected -= onPatterns;
                m.ValuesDetected -= onValues;
                m.ItemLoaded -= onLoaded;
                m.ForegroundChangeChecked -= onForeground;
                LogAssert.ignoreFailingMessages = false;
            }
        }

        private static void AssertEachOnce(
            List<(string op, string source, bool ok, string? code, string? message)> seen, string[] operations, string when)
        {
            Assert.AreEqual(operations.Length * 2, seen.Count, $"{when}: not one event and one callback per operation");
            foreach (string op in operations)
            {
                var mine = seen.Where(s => s.op == op).ToList();
                CollectionAssert.AreEqual(new[] { "event", "callback" }, mine.Select(s => s.source).ToArray(), $"{when}: {op}");
                foreach (var s in mine)
                {
                    Assert.IsFalse(s.ok, $"{op} {s.source}");
                    Assert.AreEqual(Unavailable, s.code, $"{op} {s.source}");
                    Assert.AreEqual($"{op} is only available on an iOS device.", s.message, $"{op} {s.source}");
                }
            }
        }
#endif
    }
}
#endif
#endif
