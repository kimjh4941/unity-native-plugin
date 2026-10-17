#nullable enable

// Editor only: in a player the manager reaches the native library, and the valid calls here would
// open the share sheet.
#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using JonghyunKim.NativeToolkit.Runtime.Android.Common;
using JonghyunKim.NativeToolkit.Runtime.Android.Share;
using Api = JonghyunKim.NativeToolkit.Runtime.Android.Share.AndroidShareCApi;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// Layer 2a tests of <see cref="AndroidShareManager"/> in the Editor
    /// (artifact/android/share/designs/2026-10-11-android-share-design-v6.md, 7.2): the Editor's
    /// "not on Android" path, the per-call slots, the completion path through its seams
    /// (<c>CallNative</c>, <c>OnDoneCore</c>, <c>OnReleaseCore</c>, <c>OnSelectionCore</c>,
    /// <c>OnChooserActionCore</c>), and delivery through <see cref="AndroidNativeCallbackQueue"/>.
    /// </summary>
    public sealed class AndroidShareManagerEditorTests
    {
        private readonly List<string> _events = new();
        private readonly List<string> _calls = new();

        [SetUp]
        public void SetUp()
        {
            _events.Clear();
            _calls.Clear();
            DestroyManagers();
            AndroidNativeCallbackQueue.ResetForTests();
            AndroidShareManager.ResetStaticStateForTests();
        }

        [TearDown]
        public void TearDown()
        {
            DestroyManagers();
            AndroidNativeCallbackQueue.ResetForTests();
            AndroidShareManager.ResetStaticStateForTests();
        }

        private static void DestroyManagers()
        {
            foreach (var manager in UnityEngine.Object.FindObjectsByType<AndroidShareManager>(FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(manager.gameObject);
        }

        private AndroidShareManager Manager()
        {
            var manager = AndroidShareManager.Instance;
            Record(manager, _events);
            return manager;
        }

        private static void Record(AndroidShareManager manager, List<string> events)
        {
            manager.ShareOperationCompleted += r => events.Add($"Op|{r.Operation}|{r.IsSuccess}|{r.ErrorMessage ?? "null"}");
            manager.ShareCallbackReceived += r => events.Add($"Selected|{r.Operation}|{r.SelectedPackageName ?? "null"}");
            manager.ShareChooserActionTapped += r => events.Add($"Action|{r.ActionId}");
        }

        private Action<AndroidShareOperationResult> Call(string name) =>
            r => _calls.Add($"{name}|{r.Operation}|{r.IsSuccess}|{r.ErrorMessage ?? "null"}");

        /// <summary>Lets the callback queue run (it runs on the frames after an action is posted).</summary>
        private static IEnumerator Frames(int count = 3)
        {
            for (int i = 0; i < count; i++) yield return null;
        }

        private static string NotStarted(string op) => $"Op|{op}|False|{op} could not be started.";

        // ── The Editor (J-S3) ─────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator Editor_EveryOperationFailsAfterTheCall()
        {
            var manager = Manager();
            var actions = new (string op, Action call)[]
            {
                (AndroidShareManager.OperationShareText, () => manager.ShareText(new AndroidShareTextPayload { text = "a" }, Call("text"))),
                (AndroidShareManager.OperationShareImage, () => manager.ShareImage(new AndroidShareImagePayload { filePath = "/a.png" }, Call("image"))),
                (AndroidShareManager.OperationShareImages, () => manager.ShareImages(new AndroidShareImagesPayload { filePaths = new[] { "/a.png" } }, Call("images"))),
                (AndroidShareManager.OperationShareFile, () => manager.ShareFile(new AndroidShareFilePayload { filePath = "/a" }, Call("file"))),
                (AndroidShareManager.OperationShareFiles, () => manager.ShareFiles(new AndroidShareFilesPayload { filePaths = new[] { "/a" } }, Call("files"))),
                (AndroidShareManager.OperationRegisterDirectShareTarget, () => manager.RegisterDirectShareTarget(new AndroidDirectShareTargetPayload { id = "i", label = "l", iconBase64 = "AA==" }, Call("register"))),
                (AndroidShareManager.OperationRemoveDirectShareTargets, () => manager.RemoveDirectShareTargets(new AndroidRemoveDirectShareTargetsPayload { ids = new[] { "i" } }, Call("remove"))),
                (AndroidShareManager.OperationShareWithCallback, () => manager.ShareWithCallback(new AndroidShareTextPayload { text = "a" }, Call("withCallback"))),
                (AndroidShareManager.OperationCancelPendingShareCallback, () => manager.CancelPendingShareCallback(Call("cancel"))),
            };
            foreach (var (op, call) in actions)
            {
                _events.Clear();
                _calls.Clear();
                call();
                Assert.IsEmpty(_events, $"{op}: an event came inside the call");
                yield return Frames();
                Assert.AreEqual(new[] { NotStarted(op) }, _events, op);
                Assert.AreEqual(1, _calls.Count, $"{op}: the per-call callback, once");
                StringAssert.EndsWith($"|{op}|False|{op} could not be started.", _calls[0]);
            }
        }

        [UnityTest]
        public IEnumerator Editor_InvalidInput_FailsTheSameWay()
        {
            var manager = Manager();
            manager.ShareText(new AndroidShareTextPayload { text = "" });
            manager.ShareFiles(new AndroidShareFilesPayload { filePaths = new string[0] });
            yield return Frames();
            Assert.AreEqual(new[] { NotStarted("shareText"), NotStarted("shareFiles") }, _events);
        }

        [UnityTest]
        public IEnumerator Editor_Cancel_ClearsOnSelected()
        {
            var manager = Manager();
            AndroidShareManager.SetPendingSelectedForTests(r => _calls.Add("onSelected"));
            manager.CancelPendingShareCallback();
            AndroidShareManager.OnSelectionCore(1, "pkg");
            yield return Frames();
            Assert.AreEqual(new[] { NotStarted("cancelPendingShareCallback"), "Selected|shareWithCallback|pkg" }, _events);
            Assert.IsEmpty(_calls);
        }

        // ── Per-call slots (J-S5, J-S14) ──────────────────────────────────────

        [UnityTest]
        public IEnumerator SharedEvent_ComesBeforeThePerCallCallback()
        {
            var manager = Manager();
            manager.ShareOperationCompleted += r => _calls.Add($"event|{r.Operation}|{r.IsSuccess}");
            manager.ShareFile(new AndroidShareFilePayload { filePath = "/a" }, r => _calls.Add($"perCall|{r.Operation}|{r.IsSuccess}"));
            AndroidShareManager.SetPendingOperationForTests("shareImage", r => _calls.Add($"perCall|{r.Operation}|{r.IsSuccess}"));
            long key = AndroidShareManager.AddCall(new Api.ShareCall("shareImage", new[] { "/a.png" }));
            AndroidShareManager.OnDoneCore(new IntPtr(key), Api.ErrorNone);
            yield return Frames();
            Assert.AreEqual(new[]
            {
                "event|shareFile|False", "perCall|shareFile|False",
                "event|shareImage|True", "perCall|shareImage|True",
            }, _calls, "an immediate result and a completion, each event first");
        }

        [UnityTest]
        public IEnumerator SameOperationTwiceInAFrame_EachGetsItsOwnResult()
        {
            var manager = Manager();
            manager.ShareFile(new AndroidShareFilePayload { filePath = "/a" }, Call("first"));
            manager.ShareFile(new AndroidShareFilePayload { filePath = "/b" }, Call("second"));
            manager.ShareFile(new AndroidShareFilePayload { filePath = "/c" }, null);
            yield return Frames();
            Assert.AreEqual(new[] { "first|shareFile|False|shareFile could not be started.", "second|shareFile|False|shareFile could not be started." }, _calls);
            Assert.AreEqual(3, _events.Count);
        }

        [UnityTest]
        public IEnumerator Completion_TakesTheSlotWhenItArrives()
        {
            Manager();
            AndroidShareManager.SetPendingOperationForTests("shareFile", Call("before"));
            long key = AndroidShareManager.AddCall(new Api.ShareCall("shareFile", new[] { "/a" }));
            AndroidShareManager.OnDoneCore(new IntPtr(key), Api.ErrorNone);
            AndroidShareManager.SetPendingOperationForTests("shareFile", Call("after"));
            yield return Frames();
            Assert.AreEqual(new[] { "before|shareFile|True|null" }, _calls);
            Assert.AreEqual(new[] { "Op|shareFile|True|null" }, _events);
        }

        [UnityTest]
        public IEnumerator Editor_ShareWithCallbackFailure_ClearsOnSelected()
        {
            var manager = Manager();
            manager.ShareWithCallback(new AndroidShareTextPayload { text = "a" }, onSelected: r => _calls.Add("onSelected"));
            AndroidShareManager.OnSelectionCore(1, "pkg");
            yield return Frames();
            Assert.AreEqual(new[] { NotStarted("shareWithCallback"), "Selected|shareWithCallback|pkg" }, _events);
            Assert.IsEmpty(_calls);
        }

        [UnityTest]
        public IEnumerator CompletionFailure_ClearsOnSelected_SuccessKeepsIt()
        {
            Manager();
            AndroidShareManager.SetPendingSelectedForTests(r => _calls.Add("onSelected|failed"));
            long failed = AndroidShareManager.AddCall(new Api.ShareCall("shareWithCallback"));
            AndroidShareManager.OnDoneCore(new IntPtr(failed), Api.ErrorNotForeground);
            yield return Frames();
            AndroidShareManager.OnSelectionCore(1, "pkg");
            yield return Frames();
            Assert.IsEmpty(_calls, "a failed shareWithCallback clears onSelected");

            AndroidShareManager.SetPendingSelectedForTests(r => _calls.Add("onSelected|ok"));
            long ok = AndroidShareManager.AddCall(new Api.ShareCall("shareWithCallback"));
            AndroidShareManager.OnDoneCore(new IntPtr(ok), Api.ErrorNone);
            yield return Frames();
            AndroidShareManager.OnSelectionCore(2, "pkg");
            yield return Frames();
            Assert.AreEqual(new[] { "onSelected|ok" }, _calls);
            Assert.AreEqual(new[]
            {
                "Op|shareWithCallback|False|Failed to shareWithCallback: the app is not in the foreground.",
                "Selected|shareWithCallback|pkg",
                "Op|shareWithCallback|True|null",
                "Selected|shareWithCallback|pkg",
            }, _events);
        }

        // ── OnDestroy (J-S15, K-2) ────────────────────────────────────────────

        [UnityTest]
        public IEnumerator ASecondManager_DoesNotClearTheLiveOnesState()
        {
            Manager();
            AndroidShareManager.SetPendingOperationForTests("shareFile", Call("kept"));
            AndroidShareManager.SetPendingSelectedForTests(r => _calls.Add("onSelected"));
            AndroidShareManager.AddSelectionIdForTests(7);
            new GameObject("SecondShareManager").AddComponent<AndroidShareManager>();
            yield return null;
            Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType<AndroidShareManager>(FindObjectsSortMode.None).Length, "the second destroyed itself");
            Assert.AreEqual(1, AndroidShareManager.SelectionIdCountForTests);

            long key = AndroidShareManager.AddCall(new Api.ShareCall("shareFile", new[] { "/a" }));
            AndroidShareManager.OnDoneCore(new IntPtr(key), Api.ErrorNone);
            AndroidShareManager.OnSelectionCore(1, "pkg");
            AndroidShareManager.OnChooserActionCore("ntk.a");
            yield return Frames();
            Assert.AreEqual(new[] { "kept|shareFile|True|null", "onSelected" }, _calls);
            Assert.AreEqual(new[] { "Op|shareFile|True|null", "Selected|shareWithCallback|pkg", "Action|ntk.a" }, _events, "the live manager hears each once");
        }

        [UnityTest]
        public IEnumerator Destroying_TheManager_ClearsItsState()
        {
            var manager = Manager();
            AndroidShareManager.SetPendingOperationForTests("shareFile", Call("old"));
            AndroidShareManager.SetPendingSelectedForTests(r => _calls.Add("old onSelected"));
            AndroidShareManager.AddSelectionIdForTests(7);
            UnityEngine.Object.DestroyImmediate(manager.gameObject);
            Assert.AreEqual(0, AndroidShareManager.SelectionIdCountForTests);

            // A new manager hears the results, but none of the old manager's callbacks.
            Manager();
            long key = AndroidShareManager.AddCall(new Api.ShareCall("shareFile", new[] { "/a" }));
            AndroidShareManager.OnDoneCore(new IntPtr(key), Api.ErrorNone);
            AndroidShareManager.OnSelectionCore(1, "pkg");
            yield return Frames();
            Assert.IsEmpty(_calls, "the old per-call and onSelected were cleared");
            Assert.AreEqual(new[] { "Op|shareFile|True|null", "Selected|shareWithCallback|pkg" }, _events);
        }

        // ── Null arguments (J-S7) ─────────────────────────────────────────────

        [UnityTest]
        public IEnumerator NullArguments_ThrowAndRegisterNothing()
        {
            var manager = Manager();
            Assert.Throws<ArgumentNullException>(() => manager.ShareText(null!, Call("text")));
            Assert.Throws<ArgumentNullException>(() => manager.ShareText(
                new AndroidShareTextPayload { text = "a", chooserActions = new AndroidChooserActionPayload[] { null! } },
                Call("text"), r => _calls.Add("onChooserAction")));
            Assert.Throws<ArgumentNullException>(() => manager.ShareWithCallback(
                new AndroidShareTextPayload { text = "a", chooserActions = new AndroidChooserActionPayload[] { null! } },
                Call("withCallback"), r => _calls.Add("onSelected")));
            Assert.Throws<ArgumentNullException>(() => manager.ShareFiles(new AndroidShareFilesPayload { filePaths = null! }, Call("files")));
            Assert.Throws<ArgumentNullException>(() => manager.ShareImages(new AndroidShareImagesPayload { filePaths = null! }, Call("images")));
            Assert.Throws<ArgumentNullException>(() => manager.ShareImages(null!, Call("images")));
            Assert.Throws<ArgumentNullException>(() => manager.ShareFile(null!, Call("file")));
            Assert.Throws<ArgumentNullException>(() => manager.ShareImage(null!, Call("image")));
            Assert.Throws<ArgumentNullException>(() => manager.RemoveDirectShareTargets(new AndroidRemoveDirectShareTargetsPayload { ids = null! }, Call("remove")));
            Assert.Throws<ArgumentNullException>(() => manager.RegisterDirectShareTarget(null!, Call("register")));
            Assert.IsEmpty(_events, "nothing was reported");

            // A completion for each operation finds no per-call callback, and no onSelected waits.
            foreach (string op in new[] { "shareText", "shareWithCallback", "shareFiles", "shareImages", "shareFile", "shareImage", "removeDirectShareTargets", "registerDirectShareTarget" })
            {
                long key = AndroidShareManager.AddCall(new Api.ShareCall(op, new[] { "/a" }));
                AndroidShareManager.OnDoneCore(new IntPtr(key), Api.ErrorNone);
            }
            AndroidShareManager.OnSelectionCore(1, "pkg");
            AndroidShareManager.OnChooserActionCore("ntk.a");
            yield return Frames();
            Assert.IsEmpty(_calls, "no per-call callback was registered");
            Assert.AreEqual(10, _events.Count, string.Join(" | ", _events));
            Assert.AreEqual("Action|ntk.a", _events[_events.Count - 1]);
        }

        // ── user_data and release (J-S1, E-20, E-23) ──────────────────────────

        [UnityTest]
        public IEnumerator CallNative_CompletionAndReleaseBeforeItReturns()
        {
            Manager();
            AndroidShareManager.SetPendingOperationForTests("shareFile", Call("cb"));
            bool accepted = AndroidShareManager.CallNative(new Api.ShareCall("shareFile", new[] { "/a" }), key =>
            {
                AndroidShareManager.OnDoneCore(key, Api.ErrorNone);
                AndroidShareManager.OnReleaseCore(key);
                return Api.ErrorNone;
            });
            Assert.IsTrue(accepted);
            Assert.AreEqual(0, AndroidShareManager.PendingCallCountForTests);
            yield return Frames();
            Assert.AreEqual(new[] { "Op|shareFile|True|null" }, _events);
            Assert.AreEqual(new[] { "cb|shareFile|True|null" }, _calls);
        }

        [UnityTest]
        public IEnumerator CallNative_RejectedAtTheEntry()
        {
            Manager();
            LogAssert.Expect(LogType.Error, new Regex(@"the request was rejected \(INVALID_PARAMETER\)"));
            bool accepted = AndroidShareManager.CallNative(new Api.ShareCall("shareText"), key =>
            {
                AndroidShareManager.OnReleaseCore(key);
                return Api.ErrorInvalidParameter;
            });
            Assert.IsFalse(accepted);
            Assert.AreEqual(0, AndroidShareManager.PendingCallCountForTests);
            yield return Frames();
            Assert.AreEqual(new[] { "Op|shareText|False|Failed to shareText: the request was rejected (INVALID_PARAMETER)." }, _events);
        }

        [UnityTest]
        public IEnumerator CallNative_Throws()
        {
            Manager();
            LogAssert.Expect(LogType.Error, new Regex("DllNotFoundException"));
            bool accepted = AndroidShareManager.CallNative(new Api.ShareCall("shareFiles", new[] { "/a" }),
                key => throw new DllNotFoundException("libntk.so"));
            Assert.IsFalse(accepted);
            Assert.AreEqual(0, AndroidShareManager.PendingCallCountForTests, "no release will come, so the call is removed");
            yield return Frames();
            Assert.AreEqual(new[] { "Op|shareFiles|False|libntk.so" }, _events);
        }

        // ── Delivery (J-S4) ───────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator Delivery_ReachesADisabledManager_NotADestroyedOne()
        {
            var manager = Manager();
            manager.enabled = false;
            long key = AndroidShareManager.AddCall(new Api.ShareCall("shareFile", new[] { "/a" }));
            AndroidShareManager.OnDoneCore(new IntPtr(key), Api.ErrorNone);
            yield return Frames();
            Assert.AreEqual(new[] { "Op|shareFile|True|null" }, _events, "a disabled manager still hears it");

            key = AndroidShareManager.AddCall(new Api.ShareCall("shareFile", new[] { "/a" }));
            AndroidShareManager.OnDoneCore(new IntPtr(key), Api.ErrorNone);
            UnityEngine.Object.DestroyImmediate(manager.gameObject);
            yield return Frames();
            Assert.AreEqual(1, _events.Count, "dropped once the manager is gone");
            Assert.AreEqual(0, UnityEngine.Object.FindObjectsByType<AndroidShareManager>(FindObjectsSortMode.None).Length, "no manager was made for it");
        }

        // ── Selected apps (J-S12) ─────────────────────────────────────────────

        [UnityTest]
        public IEnumerator Selection_EventThenOnSelected_Once()
        {
            var manager = Manager();
            manager.ShareCallbackReceived += r => _calls.Add($"event|{r.SelectedPackageName ?? "null"}");
            AndroidShareManager.SetPendingSelectedForTests(r => _calls.Add($"onSelected|{r.SelectedPackageName ?? "null"}"));
            AndroidShareManager.OnSelectionCore(1, "pkg");
            yield return Frames();
            AndroidShareManager.OnSelectionCore(2, null);
            yield return Frames();
            Assert.AreEqual(new[] { "event|pkg", "onSelected|pkg", "event|null" }, _calls);
        }

        // ── Chooser actions (4.3, K-1) ────────────────────────────────────────

        [UnityTest]
        public IEnumerator ChooserAction_EventThenPerCall_KeptForTheNextTap()
        {
            var manager = Manager();
            manager.ShareChooserActionTapped += r => _calls.Add($"event|{r.ActionId}");
            manager.ShareText(new AndroidShareTextPayload { text = "a" }, onChooserAction: r => _calls.Add($"perCall|{r.ActionId}"));
            yield return Frames();
            AndroidShareManager.OnChooserActionCore("ntk.a");
            AndroidShareManager.OnChooserActionCore("android.intent.action.SEND");
            yield return Frames();
            AndroidShareManager.OnChooserActionCore("ntk.b");
            yield return Frames();
            Assert.AreEqual(new[] { "event|ntk.a", "perCall|ntk.a", "event|ntk.b", "perCall|ntk.b" }, _calls);
        }

        [UnityTest]
        public IEnumerator ChooserAction_KeepsThePerCallOfTheTap()
        {
            var manager = Manager();
            manager.ShareText(new AndroidShareTextPayload { text = "a" }, onChooserAction: r => _calls.Add($"old|{r.ActionId}"));
            yield return Frames();
            AndroidShareManager.OnChooserActionCore("ntk.a");
            manager.ShareText(new AndroidShareTextPayload { text = "b" }, onChooserAction: r => _calls.Add($"new|{r.ActionId}"));
            yield return Frames();
            Assert.AreEqual(new[] { "old|ntk.a" }, _calls);
        }

        [UnityTest]
        public IEnumerator ChooserAction_TakenThenManagerDestroyed_StillReachesItsSubscribers()
        {
            var manager = Manager();
            manager.ShareText(new AndroidShareTextPayload { text = "a" }, onChooserAction: r => _calls.Add($"perCall|{r.ActionId}"));
            yield return Frames();
            _events.Clear();
            AndroidShareManager.OnChooserActionCore("ntk.a");
            UnityEngine.Object.DestroyImmediate(manager.gameObject);
            yield return Frames();
            Assert.AreEqual(new[] { "Action|ntk.a" }, _events, "once, to the manager it was taken for (as 1.x)");
            Assert.AreEqual(new[] { "perCall|ntk.a" }, _calls, "with the per-call callback of the tap (K-1)");
        }

        [UnityTest]
        public IEnumerator ChooserAction_WithoutAManager_IsDropped()
        {
            var manager = Manager();
            UnityEngine.Object.DestroyImmediate(manager.gameObject);
            AndroidShareManager.OnChooserActionCore("ntk.a");
            yield return Frames();
            Assert.IsEmpty(_events);
        }
    }
}
#endif
