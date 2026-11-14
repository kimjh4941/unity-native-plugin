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
    /// <summary>
    /// iOS player: the Clipboard plan v4, section 1 (artifact/ios/clipboard/designs/2026-11-14-ios-clipboard-ui-test-plan-v4.md).
    /// Every test uses its own named pasteboard and never the general one: the manager is only called
    /// through the helpers below, and the helpers that touch a pasteboard require its scope. The helpers
    /// count calls and completions, and the teardown fails a test that left a call unfinished (it would
    /// make the next test's call CLIPBOARD_BUSY). Values that the plan says to record are written with
    /// <see cref="Record"/> (the test's own values only).
    /// </summary>
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
            public T V => Value ?? throw new AssertionException("no result");
        }

        private IosClipboardManager M => IosClipboardManager.Instance;
        private IosPasteboardScope _scope = null!;
        private int _calls;
        private int _done;
        private bool _observing;

        private Box<T> Track<T>(Action<Action<T>> start) where T : struct
        {
            var box = new Box<T>();
            _calls++;
            start(r => { box.Value = r; box.Count++; _done++; });
            return box;
        }

        private static IEnumerator Await<T>(Box<T> box, string what) where T : struct
        {
            yield return WaitFor(() => box.Count > 0);
            Assert.IsNotNull(box.Value, $"no {what} result within the timeout");
        }

        private static void Record(string what) => Debug.Log($"[NTK-CLIPBOARD-RECORD] {TestContext.CurrentContext.Test.Name} {what}");

        /// <summary>
        /// 1.x logs an error when a read-like call fails (IosClipboardJsonParser.ReadErrorInfo); the test
        /// framework would fail the test on that log, so the tests that expect such a failure declare it.
        /// </summary>
        private static void ExpectErrorLog(string code) =>
            LogAssert.Expect(LogType.Error, new Regex(@"\[IosClipboardJsonParser\]\[ReadErrorInfo\] errorCode: " + code + ","));

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
            _calls = 0;
            _done = 0;
            _observing = false;
            string name = Prefix + TestContext.CurrentContext.Test.Name;
            _scope = IosPasteboardScope.Named(name);
            var created = Create(IosPasteboardCreationRequest.Named(name));
            yield return Await(created, "CreatePasteboard");
            Assert.IsTrue(created.V.IsSuccess, $"CreatePasteboard failed: {created.V.Error?.Code}");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_observing)
            {
                var stopped = StopObserving();
                yield return WaitFor(() => stopped.Count > 0);
            }
            var removed = Remove(_scope);
            yield return WaitFor(() => removed.Count > 0);
            yield return WaitFor(() => _done >= _calls);
            Assert.AreEqual(_calls, _done, "a call of this test did not complete");
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
            var data = ReadData(_scope, "public.html");
            yield return Await(data, "ReadData");
            Assert.IsTrue(data.V.HasData);
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
            Assert.AreEqual(2, s.NumberOfItems);
            CollectionAssert.DoesNotContain(s.TypeIdentifiers, "public.url");
            Assert.AreEqual(2, s.AllTypeIdentifiers.Count);
            CollectionAssert.Contains(s.AllTypeIdentifiers[1], "public.url");
            CollectionAssert.AreEqual(new[] { 1 }, s.MatchingItemIndexes);
        }

        /// <summary>
        /// 1.x, measured on the simulator (2026-11-14): on a named pasteboard, detection finds nothing in a
        /// URL item. The same URL put as plain text is recorded next to it.
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
            Record($"from-text success={fromText.V.IsSuccess} patterns={string.Join(",", fromText.V.Patterns ?? Array.Empty<IosClipboardDetectionPattern>())}");
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
            Record($"from-text success={fromText.V.IsSuccess} url={fromText.V.Values?.ProbableWebUrl ?? "<null>"}");
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
            ExpectErrorLog("CLIPBOARD_NO_MATCHING_ITEM");
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
            Assert.IsFalse(string.IsNullOrEmpty(unique.Name));
            var removed = Remove(unique);
            yield return Await(removed, "RemovePasteboard");
            Assert.IsTrue(removed.V.IsSuccess);
            ExpectErrorLog("CLIPBOARD_UNAVAILABLE");
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

        [Test]
        public void CP20_BlankNameThrowsInCSharp()
        {
            Assert.Throws<ArgumentException>(() => IosPasteboardCreationRequest.Named("   "));
        }

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
            ExpectErrorLog("CLIPBOARD_UNAVAILABLE");
            var read = Read(_scope);
            yield return Await(read, "Read");
            ExpectError(read.V.Error, "CLIPBOARD_UNAVAILABLE", "The requested pasteboard is unavailable.");
        }

        [UnityTest]
        public IEnumerator CP26_LoadingAnImageFromTextFails()
        {
            var copied = Copy(_scope, IosClipboardContent.PlainText("ntk"));
            yield return Await(copied, "Copy");
            ExpectErrorLog("CLIPBOARD_NO_MATCHING_ITEM");
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
            yield return CountChanges(() => Body(), events);
            IEnumerator Body()
            {
                var started = StartObserving(_scope);
                yield return Await(started, "StartObserving");
                var stopped = StopObserving();
                yield return Await(stopped, "StopObserving");
                Assert.IsTrue(stopped.V.IsSuccess);
                var copied = Copy(_scope, IosClipboardContent.PlainText("ntk"));
                yield return Await(copied, "Copy");
                yield return WaitFor(() => false, Window);
            }
            Assert.AreEqual(0, events.Count, Describe(events));
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
        }
    }
}
#endif
#endif
