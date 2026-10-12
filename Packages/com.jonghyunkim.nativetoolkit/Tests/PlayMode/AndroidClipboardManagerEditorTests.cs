#nullable enable

// Editor only: in a player the manager reaches the native library and the real clipboard.
#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using JonghyunKim.NativeToolkit.Runtime.Android.Clipboard;
using JonghyunKim.NativeToolkit.Runtime.Android.Common;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// Layer 2a tests of <see cref="AndroidClipboardManager"/> in the Editor
    /// (artifact/android/clipboard/designs/2026-10-12-android-clipboard-design-v4.md, 7.2): the
    /// Editor's "not on Android" results, the per-call callbacks, delivery through
    /// <see cref="AndroidNativeCallbackQueue"/>, the change callback behind the observing flag, the
    /// listener registration, and the stop on destroy, through the manager's test seams.
    /// </summary>
    public sealed class AndroidClipboardManagerEditorTests
    {
        private readonly List<string> _events = new();
        private readonly List<string> _calls = new();
        private int _changes;

        [SetUp]
        public void SetUp()
        {
            _events.Clear();
            _calls.Clear();
            _changes = 0;
            DestroyManagers();
            AndroidNativeCallbackQueue.ResetForTests();
            AndroidClipboardManager.ResetStaticStateForTests();
        }

        [TearDown]
        public void TearDown()
        {
            DestroyManagers();
            AndroidNativeCallbackQueue.ResetForTests();
            AndroidClipboardManager.ResetStaticStateForTests();
        }

        private static void DestroyManagers()
        {
            foreach (var manager in UnityEngine.Object.FindObjectsByType<AndroidClipboardManager>(FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(manager.gameObject);
        }

        private AndroidClipboardManager Manager()
        {
            var manager = AndroidClipboardManager.Instance;
            manager.ClipboardOperationCompleted += r => _events.Add($"Op|{r.Operation}|{r.IsSuccess}|{r.ErrorMessage ?? "null"}");
            manager.ClipboardChanged += () => _changes++;
            return manager;
        }

        private Action<ClipboardOperationResult> Call(string name) =>
            r => _calls.Add($"{name}|{r.Operation}|{r.IsSuccess}|{r.ErrorMessage ?? "null"}");

        /// <summary>Lets the callback queue run (it runs on the frames after an action is posted).</summary>
        private static IEnumerator Frames(int count = 3)
        {
            for (int i = 0; i < count; i++) yield return null;
        }

        private static string NotStarted(string op) => $"Op|{op}|False|{op} could not be started.";

        private static int AnotherManagerCount() =>
            UnityEngine.Object.FindObjectsByType<AndroidClipboardManager>(FindObjectsSortMode.None).Length;

        // ── The Editor (J-C6) ─────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator Editor_EveryAsyncOperationFailsAfterTheCall()
        {
            var manager = Manager();
            var actions = new (string op, Action call)[]
            {
                (AndroidClipboardManager.OperationCopyPlainText, () => manager.CopyPlainText(new CopyPlainTextPayload { text = "a" }, Call("plain"))),
                (AndroidClipboardManager.OperationCopyHtmlText, () => manager.CopyHtmlText(new CopyHtmlTextPayload { htmlText = "" }, Call("html"))),
                (AndroidClipboardManager.OperationCopyUri, () => manager.CopyUri(new CopyUriPayload { uri = "http://x" }, Call("uri"))),
                (AndroidClipboardManager.OperationCopyMultipleText, () => manager.CopyMultipleText(new CopyMultipleTextPayload { texts = Array.Empty<string>() }, Call("multi"))),
                (AndroidClipboardManager.OperationClear, () => manager.Clear(Call("clear"))),
                (AndroidClipboardManager.OperationStopObserving, () => manager.StopObserving(Call("stop"))),
            };
            foreach (var (op, call) in actions)
            {
                _events.Clear();
                _calls.Clear();
                call();
                Assert.IsEmpty(_events, $"{op}: an event came inside the call");
                Assert.IsEmpty(_calls, $"{op}: the per-call callback came inside the call");
                yield return Frames();
                Assert.AreEqual(new[] { NotStarted(op) }, _events, op);
                Assert.AreEqual(1, _calls.Count, $"{op}: the per-call callback, once");
                StringAssert.EndsWith($"|{op}|False|{op} could not be started.", _calls[0]);
            }
        }

        [UnityTest]
        public IEnumerator Editor_CommonEventComesBeforeThePerCallCallback()
        {
            var manager = Manager();
            var order = new List<string>();
            manager.ClipboardOperationCompleted += _ => order.Add("common");
            manager.Clear(_ => order.Add("perCall"));
            yield return Frames();
            Assert.AreEqual(new[] { "common", "perCall" }, order);
        }

        [UnityTest]
        public IEnumerator Editor_SyncOperationsFailWithoutEvents()
        {
            var manager = Manager();

            ClipboardReadResult read = manager.Read();
            Assert.AreEqual(ClipboardReadStatus.Failed, read.Status);
            Assert.AreEqual("CLIPBOARD_BRIDGE_UNAVAILABLE", read.ErrorCode);
            Assert.AreEqual("read could not be started.", read.ErrorMessage);

            ClipboardDescriptionResult description = manager.GetDescription();
            Assert.AreEqual(ClipboardReadStatus.Failed, description.Status);
            Assert.AreEqual("CLIPBOARD_BRIDGE_UNAVAILABLE", description.ErrorCode);
            Assert.AreEqual("getDescription could not be started.", description.ErrorMessage);

            Assert.IsFalse(manager.HasClip());

            yield return Frames();
            Assert.IsEmpty(_events);
        }

        [UnityTest]
        public IEnumerator Editor_StartObservingRaisesNothingAndDoesNotObserve()
        {
            var manager = Manager();
            manager.StartObserving();
            yield return Frames();
            Assert.IsEmpty(_events);
            Assert.AreEqual(0, _changes);
            Assert.IsFalse(AndroidClipboardManager.IsObservingForTests);
        }

        // ── Per-call callbacks (J-C2) ─────────────────────────────────────────

        [UnityTest]
        public IEnumerator PerCall_TwoCallsInOneFrame_EachGetsItsOwnResult()
        {
            var manager = Manager();
            manager.CopyPlainText(new CopyPlainTextPayload { text = "a" }, Call("first"));
            manager.CopyPlainText(new CopyPlainTextPayload { text = "b" }, Call("second"));
            manager.CopyPlainText(new CopyPlainTextPayload { text = "c" });
            yield return Frames();
            Assert.AreEqual(3, _events.Count);
            Assert.AreEqual(2, _calls.Count);
            StringAssert.StartsWith("first|", _calls[0]);
            StringAssert.StartsWith("second|", _calls[1]);
        }

        // ── Null arguments (J-C7, E-1) ────────────────────────────────────────

        [UnityTest]
        public IEnumerator NullPayload_ThrowsAndRaisesNothing()
        {
            var manager = Manager();
            Assert.Throws<ArgumentNullException>(() => manager.CopyPlainText(null!, Call("x")));
            Assert.Throws<ArgumentNullException>(() => manager.CopyHtmlText(null!, Call("x")));
            Assert.Throws<ArgumentNullException>(() => manager.CopyUri(null!, Call("x")));
            Assert.Throws<ArgumentNullException>(() => manager.CopyMultipleText(null!, Call("x")));
            Assert.Throws<ArgumentNullException>(() => manager.CopyMultipleText(new CopyMultipleTextPayload { texts = null! }, Call("x")));
            yield return Frames();
            Assert.IsEmpty(_events);
            Assert.IsEmpty(_calls);
        }

        // ── Delivery (J-C3) ───────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator Delivery_AfterTheManagerIsDestroyed_IsDroppedWithoutANewManager()
        {
            var manager = Manager();
            manager.CopyPlainText(new CopyPlainTextPayload { text = "a" }, Call("x"));
            UnityEngine.Object.DestroyImmediate(manager.gameObject);
            yield return Frames();
            Assert.IsEmpty(_events);
            Assert.IsEmpty(_calls);
            Assert.AreEqual(0, AnotherManagerCount(), "no manager is created to deliver a result");
        }

        [UnityTest]
        public IEnumerator Delivery_ToADisabledManager_StillArrives()
        {
            var manager = Manager();
            manager.enabled = false;
            manager.Clear(Call("clear"));
            yield return Frames();
            Assert.AreEqual(new[] { NotStarted(AndroidClipboardManager.OperationClear) }, _events);
            Assert.AreEqual(1, _calls.Count);
        }

        // ── Changes and the observing flag (J-C10) ────────────────────────────

        [UnityTest]
        public IEnumerator Change_WhileNotObserving_IsDropped()
        {
            Manager();
            AndroidClipboardManager.OnChangedCore();
            yield return Frames();
            Assert.AreEqual(0, _changes);
        }

        [UnityTest]
        public IEnumerator Change_WhileObserving_IsRaisedOnceAfterTheCall()
        {
            Manager();
            AndroidClipboardManager.SetObservingForTests(true);
            AndroidClipboardManager.OnChangedCore();
            Assert.AreEqual(0, _changes, "not inside the callback");
            yield return Frames();
            Assert.AreEqual(1, _changes);
        }

        [UnityTest]
        public IEnumerator Change_ASubscriberThatThrows_IsLoggedAsAnError()
        {
            var manager = Manager();
            manager.ClipboardChanged += () => throw new InvalidOperationException("subscriber");
            AndroidClipboardManager.SetObservingForTests(true);
            LogAssert.Expect(LogType.Error, new Regex("subscriber"));
            AndroidClipboardManager.OnChangedCore();
            yield return Frames();
            Assert.AreEqual(1, _changes);
        }

        [UnityTest]
        public IEnumerator Change_AfterTheManagerIsDestroyed_IsDroppedWithoutANewManager()
        {
            var manager = Manager();
            AndroidClipboardManager.SetObservingForTests(true);
            UnityEngine.Object.DestroyImmediate(manager.gameObject);
            AndroidClipboardManager.SetObservingForTests(true);
            AndroidClipboardManager.OnChangedCore();
            yield return Frames();
            Assert.AreEqual(0, _changes);
            Assert.AreEqual(0, AnotherManagerCount());
        }

        [UnityTest]
        public IEnumerator Change_QueuedBeforeStopObserving_IsNotRaisedAfterIt()
        {
            var manager = Manager();
            AndroidClipboardManager.SetObservingForTests(true);
            AndroidClipboardManager.OnChangedCore();
            manager.StopObserving();
            yield return Frames();
            Assert.AreEqual(0, _changes);
        }

        [UnityTest]
        public IEnumerator Change_QueuedBeforeAStopAndARestart_IsNotRaised()
        {
            Manager();
            AndroidClipboardManager.SetNativeForTests(() => AndroidClipboardCApi.ErrorNone, () => AndroidClipboardCApi.ErrorNone, () => AndroidClipboardCApi.ErrorNone);
            AndroidClipboardManager.SetObservingForTests(true);
            AndroidClipboardManager.OnChangedCore();
            AndroidClipboardManager.Instance.StopObserving();
            AndroidClipboardManager.StartObservingCore();
            Assert.IsTrue(AndroidClipboardManager.IsObservingForTests);
            yield return Frames();
            Assert.AreEqual(0, _changes, "the change came before the stop");

            AndroidClipboardManager.OnChangedCore();
            yield return Frames();
            Assert.AreEqual(1, _changes, "a change after the restart is raised");
        }

        [UnityTest]
        public IEnumerator StopObserving_LowersTheFlagEvenInTheEditor()
        {
            var manager = Manager();
            AndroidClipboardManager.SetObservingForTests(true);
            manager.StopObserving();
            Assert.IsFalse(AndroidClipboardManager.IsObservingForTests);
            yield return Frames();
        }

        // ── Destroying the manager (J-C12) ────────────────────────────────────

        private static IEnumerator DestroyCase(bool live, bool observing, int expectedStops)
        {
            int stops = 0;
            AndroidClipboardManager.SetNativeForTests(null, () => { stops++; return AndroidClipboardCApi.ErrorNone; });
            var manager = AndroidClipboardManager.Instance;
            AndroidClipboardManager.SetObservingForTests(observing);

            if (live)
            {
                UnityEngine.Object.DestroyImmediate(manager.gameObject);
                Assert.IsFalse(AndroidClipboardManager.IsObservingForTests, $"live, observing {observing}: the flag is lowered");
            }
            else
            {
                // A second manager destroys itself in Awake; the live one's flag must stay.
                new GameObject("SecondClipboardManager").AddComponent<AndroidClipboardManager>();
                yield return null;
                Assert.AreEqual(observing, AndroidClipboardManager.IsObservingForTests, $"second, observing {observing}: the flag stays");
            }
            Assert.AreEqual(expectedStops, stops, $"live {live}, observing {observing}: native stops");
            AndroidClipboardManager.SetNativeForTests(null, null);
            DestroyManagers();
        }

        [UnityTest]
        public IEnumerator Destroy_StopsTheNativeObservingOnlyForTheLiveObservingManager()
        {
            yield return DestroyCase(live: true, observing: true, expectedStops: 1);
            yield return DestroyCase(live: true, observing: false, expectedStops: 0);
            yield return DestroyCase(live: false, observing: true, expectedStops: 0);
            yield return DestroyCase(live: false, observing: false, expectedStops: 0);
        }

        [UnityTest]
        public IEnumerator Destroy_ANativeStopThatThrows_IsAWarningAndTheManagerGoes()
        {
            AndroidClipboardManager.SetNativeForTests(null, () => throw new InvalidOperationException("stop"));
            var manager = AndroidClipboardManager.Instance;
            AndroidClipboardManager.SetObservingForTests(true);
            UnityEngine.Object.DestroyImmediate(manager.gameObject);
            yield return null;
            Assert.AreEqual(0, AnotherManagerCount());
            Assert.IsFalse(AndroidClipboardManager.IsObservingForTests);
        }

        // ── Starting to observe (J-C10, J-C11) ────────────────────────────────

        [Test]
        public void Start_Accepted_RaisesTheFlagBeforeTheNativeStart()
        {
            int starts = 0;
            bool flagAtTheCall = false;
            AndroidClipboardManager.SetNativeForTests(() => AndroidClipboardCApi.ErrorNone, null, () =>
            {
                starts++;
                flagAtTheCall = AndroidClipboardManager.IsObservingForTests;
                return AndroidClipboardCApi.ErrorNone;
            });
            AndroidClipboardManager.StartObservingCore();
            Assert.AreEqual(1, starts);
            Assert.IsTrue(flagAtTheCall, "a change right after the start must not be dropped (J-C10)");
            Assert.IsTrue(AndroidClipboardManager.IsObservingForTests);
        }

        [Test]
        public void Start_Refused_LeavesTheFlagDown()
        {
            int starts = 0;
            AndroidClipboardManager.SetNativeForTests(() => AndroidClipboardCApi.ErrorNone, null, () => { starts++; return AndroidClipboardCApi.ErrorUnknown; });
            AndroidClipboardManager.StartObservingCore();
            Assert.AreEqual(1, starts);
            Assert.IsFalse(AndroidClipboardManager.IsObservingForTests);
        }

        [Test]
        public void Start_Throwing_LeavesTheFlagDownAndLogsAnError()
        {
            int starts = 0;
            AndroidClipboardManager.SetNativeForTests(() => AndroidClipboardCApi.ErrorNone, null, () => { starts++; throw new InvalidOperationException("start"); });
            LogAssert.Expect(LogType.Error, new Regex("start"));
            AndroidClipboardManager.StartObservingCore();
            Assert.AreEqual(1, starts);
            Assert.IsFalse(AndroidClipboardManager.IsObservingForTests);
        }

        [Test]
        public void Start_ASecondStartThatFails_KeepsTheFirstObserving()
        {
            int result = AndroidClipboardCApi.ErrorNone;
            AndroidClipboardManager.SetNativeForTests(() => AndroidClipboardCApi.ErrorNone, null, () => result);
            AndroidClipboardManager.StartObservingCore();
            result = AndroidClipboardCApi.ErrorUnknown;
            AndroidClipboardManager.StartObservingCore();
            Assert.IsTrue(AndroidClipboardManager.IsObservingForTests);
        }

        [Test]
        public void Start_ASecondStartThatThrows_KeepsTheFirstObserving()
        {
            bool fail = false;
            AndroidClipboardManager.SetNativeForTests(() => AndroidClipboardCApi.ErrorNone, null, () =>
            {
                if (fail) throw new InvalidOperationException("start");
                return AndroidClipboardCApi.ErrorNone;
            });
            AndroidClipboardManager.StartObservingCore();
            fail = true;
            LogAssert.Expect(LogType.Error, new Regex("start"));
            AndroidClipboardManager.StartObservingCore();
            Assert.IsTrue(AndroidClipboardManager.IsObservingForTests);
        }

        [Test]
        public void Start_AStopFromAnotherThread_WaitsAndWins()
        {
            // While a second start is inside the native call, another thread stops. The stop waits for
            // the start, so the start's rollback cannot undo it: the flag ends down.
            var manager = Manager();
            bool stopFinishedDuringTheStart = false;
            Thread? stopper = null;
            int result = AndroidClipboardCApi.ErrorNone;
            AndroidClipboardManager.SetNativeForTests(() => AndroidClipboardCApi.ErrorNone, () => AndroidClipboardCApi.ErrorNone, () =>
            {
                if (result == AndroidClipboardCApi.ErrorNone) return result;
                stopper = new Thread(() => manager.StopObserving());
                stopper.Start();
                stopFinishedDuringTheStart = stopper.Join(200);
                return result;
            });
            AndroidClipboardManager.StartObservingCore();
            result = AndroidClipboardCApi.ErrorUnknown;
            AndroidClipboardManager.StartObservingCore();
            stopper!.Join();

            Assert.IsFalse(stopFinishedDuringTheStart, "the stop must wait for the start");
            Assert.IsFalse(AndroidClipboardManager.IsObservingForTests);
        }

        [Test]
        public void Start_WithoutAListener_DoesNotStart()
        {
            int starts = 0;
            AndroidClipboardManager.SetNativeForTests(() => AndroidClipboardCApi.ErrorUnknown, null, () => { starts++; return AndroidClipboardCApi.ErrorNone; });
            AndroidClipboardManager.StartObservingCore();
            Assert.AreEqual(0, starts);
            Assert.IsFalse(AndroidClipboardManager.IsObservingForTests);
        }

        [Test]
        public void Start_RegistersAgainAfterAFailedRegistration()
        {
            int adds = 0;
            int result = AndroidClipboardCApi.ErrorUnknown;
            AndroidClipboardManager.SetNativeForTests(() => { adds++; return result; }, null, () => AndroidClipboardCApi.ErrorNone);
            Assert.IsFalse(AndroidClipboardManager.EnsureChangeListener(), "as Initialize would");
            result = AndroidClipboardCApi.ErrorNone;
            AndroidClipboardManager.StartObservingCore();
            Assert.AreEqual(2, adds);
            Assert.IsTrue(AndroidClipboardManager.ListenerRegisteredForTests);
            Assert.IsTrue(AndroidClipboardManager.IsObservingForTests);
        }

        // ── Reading frees the handle (Clipboard.h: the caller owns it) ────────

        [Test]
        public void ReadAndFree_FreesOnceAfterARead()
        {
            var freed = new List<IntPtr>();
            int value = AndroidClipboardManager.ReadAndFree(new IntPtr(42), h => h.ToInt32() + 1, freed.Add);
            Assert.AreEqual(43, value);
            CollectionAssert.AreEqual(new[] { new IntPtr(42) }, freed);
        }

        [Test]
        public void ReadAndFree_FreesOnceWhenTheReadThrows()
        {
            var freed = new List<IntPtr>();
            Assert.Throws<InvalidOperationException>(() =>
                AndroidClipboardManager.ReadAndFree<int>(new IntPtr(7), _ => throw new InvalidOperationException("read"), freed.Add));
            CollectionAssert.AreEqual(new[] { new IntPtr(7) }, freed);
        }

        // ── The change listener (J-C9) ────────────────────────────────────────

        [Test]
        public void Listener_IsRegisteredOnce()
        {
            int adds = 0;
            AndroidClipboardManager.SetNativeForTests(() => { adds++; return AndroidClipboardCApi.ErrorNone; }, null);
            Assert.IsTrue(AndroidClipboardManager.EnsureChangeListener());
            Assert.IsTrue(AndroidClipboardManager.EnsureChangeListener());
            Assert.AreEqual(1, adds);
            Assert.IsTrue(AndroidClipboardManager.ListenerRegisteredForTests);
        }

        [Test]
        public void Listener_AFailedRegistrationIsTriedAgain()
        {
            int adds = 0;
            int result = AndroidClipboardCApi.ErrorUnknown;
            AndroidClipboardManager.SetNativeForTests(() => { adds++; return result; }, null);
            Assert.IsFalse(AndroidClipboardManager.EnsureChangeListener());
            Assert.IsFalse(AndroidClipboardManager.ListenerRegisteredForTests);

            result = AndroidClipboardCApi.ErrorNone;
            Assert.IsTrue(AndroidClipboardManager.EnsureChangeListener());
            Assert.IsTrue(AndroidClipboardManager.EnsureChangeListener());
            Assert.AreEqual(2, adds);
        }

        [Test]
        public void Listener_ARegistrationThatThrows_IsAnErrorAndTriedAgain()
        {
            int adds = 0;
            AndroidClipboardManager.SetNativeForTests(() => { adds++; if (adds == 1) throw new InvalidOperationException("add"); return AndroidClipboardCApi.ErrorNone; }, null);
            LogAssert.Expect(LogType.Error, new Regex("add"));
            Assert.IsFalse(AndroidClipboardManager.EnsureChangeListener());
            Assert.IsTrue(AndroidClipboardManager.EnsureChangeListener());
            Assert.AreEqual(2, adds);
        }
    }
}
#endif
