#nullable enable

// Editor only: in a player the manager reaches the native library, and the valid calls here would
// show real dialogs.
#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using JonghyunKim.NativeToolkit.Runtime.Android.Common;
using JonghyunKim.NativeToolkit.Runtime.Android.Dialog;
using Api = JonghyunKim.NativeToolkit.Runtime.Android.Dialog.AndroidDialogCApi;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// Layer 2a tests of <see cref="AndroidDialogManager"/> in the Editor
    /// (artifact/android/dialog/designs/2026-10-11-android-dialog-design-v4.md, 7.2): the checks that
    /// fail before any native call, the Editor's "not on a device" path, and the delivery of
    /// completions through <see cref="AndroidNativeCallbackQueue"/> (J-D2).
    /// </summary>
    public sealed class AndroidDialogManagerEditorTests
    {
        private readonly List<string> _events = new();

        [SetUp]
        public void SetUp()
        {
            _events.Clear();
            AndroidNativeCallbackQueue.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            AndroidNativeCallbackQueue.ResetForTests();
            var manager = UnityEngine.Object.FindFirstObjectByType<AndroidDialogManager>();
            if (manager != null) UnityEngine.Object.DestroyImmediate(manager.gameObject);
        }

        private void Record(AndroidDialogManager manager) => Record(manager, _events);

        private static void Record(AndroidDialogManager manager, List<string> events)
        {
            manager.DialogResult += (b, ok, e) => events.Add($"Dialog|{b ?? "null"}|{ok}|{e ?? "null"}");
            manager.ConfirmDialogResult += (b, ok, e) => events.Add($"Confirm|{b ?? "null"}|{ok}|{e ?? "null"}");
            manager.SingleChoiceItemDialogResult += (b, i, ok, e) => events.Add($"SingleChoice|{b ?? "null"}|{i?.ToString() ?? "null"}|{ok}|{e ?? "null"}");
            manager.MultiChoiceItemDialogResult += (b, s, ok, e) => events.Add($"MultiChoice|{b ?? "null"}|{(s == null ? "null" : string.Join(",", s))}|{ok}|{e ?? "null"}");
            manager.TextInputDialogResult += (b, t, ok, e) => events.Add($"TextInput|{b ?? "null"}|{t ?? "null"}|{ok}|{e ?? "null"}");
            manager.LoginDialogResult += (b, u, p, ok, e) => events.Add($"Login|{b ?? "null"}|{u ?? "null"}|{p ?? "null"}|{ok}|{e ?? "null"}");
        }

        // ── Checks before the native call: an event inside the call, once ────────────────────────

        [Test]
        public void EmptyTitle_FailsOnceInsideTheCall()
        {
            var manager = AndroidDialogManager.Instance;
            Record(manager);
            manager.ShowDialog("", "");
            Assert.AreEqual(new[] { "Dialog|null|False|Title cannot be null or empty." }, _events);
        }

        [Test]
        public void EmptyMessage_Fails()
        {
            var manager = AndroidDialogManager.Instance;
            Record(manager);
            manager.ShowLoginDialog("title", "");
            Assert.AreEqual(new[] { "Login|null|null|null|False|Message cannot be null or empty." }, _events);
        }

        [Test]
        public void EmptyItems_Fail()
        {
            var manager = AndroidDialogManager.Instance;
            Record(manager);
            manager.ShowSingleChoiceItemDialog("title", new string[0]);
            manager.ShowMultiChoiceItemDialog("title", null!);
            Assert.AreEqual(new[]
            {
                "SingleChoice|null|null|False|singleChoiceItems cannot be null or empty.",
                "MultiChoice|null|null|False|multiChoiceItems cannot be null or empty.",
            }, _events);
        }

        [Test]
        public void NullItem_Fails()
        {
            var manager = AndroidDialogManager.Instance;
            Record(manager);
            manager.ShowSingleChoiceItemDialog("title", new[] { "a", null! });
            manager.ShowMultiChoiceItemDialog("title", new[] { null!, "b" });
            Assert.AreEqual(new[]
            {
                "SingleChoice|null|null|False|singleChoiceItems cannot contain null.",
                "MultiChoice|null|null|False|multiChoiceItems cannot contain null.",
            }, _events);
        }

        [Test]
        public void CheckedItemOutOfRange_Fails()
        {
            var manager = AndroidDialogManager.Instance;
            Record(manager);
            manager.ShowSingleChoiceItemDialog("title", new[] { "a", "b" }, checkedItem: 2);
            manager.ShowSingleChoiceItemDialog("title", new[] { "a", "b" }, checkedItem: -2);
            Assert.AreEqual(new[]
            {
                "SingleChoice|null|null|False|checkedItem is out of range.",
                "SingleChoice|null|null|False|checkedItem is out of range.",
            }, _events);
        }

        // ── The Editor: a valid call shows nothing and raises nothing (J-D6) ─────────────────────

        [Test]
        public void ValidCalls_InTheEditor_WarnAndRaiseNothing()
        {
            var manager = AndroidDialogManager.Instance;
            Record(manager);
            foreach (string method in new[] { "ShowDialog", "ShowConfirmDialog", "ShowSingleChoiceItemDialog", "ShowMultiChoiceItemDialog", "ShowTextInputDialog", "ShowLoginDialog" })
                LogAssert.Expect(LogType.Warning, $"{method} can only be called on an Android device.");
            manager.ShowDialog("title", "message");
            manager.ShowConfirmDialog("title", "message");
            manager.ShowSingleChoiceItemDialog("title", new[] { "a" });
            manager.ShowMultiChoiceItemDialog("title", new[] { "a" });
            manager.ShowTextInputDialog("title", "message");
            manager.ShowLoginDialog("title", "message");
            Assert.IsEmpty(_events);
        }

        [Test]
        public void SubscriberException_ReachesTheCaller_AndTheHandlerRunsOnce()
        {
            var manager = AndroidDialogManager.Instance;
            int calls = 0;
            manager.DialogResult += (_, _, _) => { calls++; throw new InvalidOperationException("subscriber"); };
            Assert.Throws<InvalidOperationException>(() => manager.ShowDialog("", "m"));
            Assert.AreEqual(1, calls);
        }

        // ── Delivery through the callback queue (J-D2) ───────────────────────────────────────────

        // AndroidDialogManager.PostAnswer is what the completion callback calls on a device.

        [UnityTest]
        public IEnumerator Completion_IsRaisedOnALaterFrame_NotInsideThePost()
        {
            var manager = AndroidDialogManager.Instance;
            Record(manager);
            AndroidDialogManager.PostAnswer(new Api.Answer(Api.Kind.Confirm, Api.ErrorNone, buttonText: "Yes"));
            Assert.IsEmpty(_events, "the post ran the completion at once (J-D14)");
            yield return null;
            yield return null;
            Assert.AreEqual(new[] { "Confirm|Yes|True|null" }, _events);
        }

        [UnityTest]
        public IEnumerator Completion_ReachesADisabledManager()
        {
            var manager = AndroidDialogManager.Instance;
            Record(manager);
            manager.enabled = false;
            AndroidDialogManager.PostAnswer(new Api.Answer(Api.Kind.Confirm, Api.ErrorNone, buttonText: "Yes"));
            yield return null;
            yield return null;
            Assert.AreEqual(new[] { "Confirm|Yes|True|null" }, _events);
        }

        [UnityTest]
        public IEnumerator Completion_AfterTheManagerIsDestroyed_GoesToTheNewInstance()
        {
            var old = AndroidDialogManager.Instance;
            Record(old);
            UnityEngine.Object.DestroyImmediate(old.gameObject);
            AndroidDialogManager.PostAnswer(new Api.Answer(Api.Kind.Alert, Api.ErrorNone, buttonText: "OK"));
            // The instance that exists when the queue runs; made here so the test can listen to it.
            var current = AndroidDialogManager.Instance;
            var heard = new List<string>();
            Record(current, heard);
            yield return null;
            yield return null;
            Assert.IsEmpty(_events, "the destroyed manager's subscribers must not hear it");
            Assert.AreEqual(new[] { "Dialog|OK|True|null" }, heard, "the current instance hears it, as with 1.x's dispatcher");
        }

        private static readonly Regex Stranded = new Regex("missing or disabled");

        [UnityTest]
        public IEnumerator Completion_WhenTheQueuesObjectWasDestroyed_IsReported_AndRunsOnceAManagerMakesItAgain()
        {
            var manager = AndroidDialogManager.Instance;
            Record(manager);
            var pump = UnityEngine.Object.FindFirstObjectByType<AndroidNativeCallbackQueue>();
            Assert.IsNotNull(pump, "the manager's Awake made the queue's object");
            UnityEngine.Object.DestroyImmediate(pump.gameObject);

            LogAssert.Expect(LogType.Error, Stranded);
            AndroidDialogManager.PostAnswer(new Api.Answer(Api.Kind.Confirm, Api.ErrorNone, buttonText: "Yes"));
            yield return null;
            yield return null;
            Assert.IsEmpty(_events, "nothing runs the queue");

            // A new manager's Awake makes the queue's object again (on a device, so does every Show*).
            UnityEngine.Object.DestroyImmediate(manager.gameObject);
            var current = AndroidDialogManager.Instance;
            var heard = new List<string>();
            Record(current, heard);
            yield return null;
            yield return null;
            Assert.AreEqual(new[] { "Confirm|Yes|True|null" }, heard);
        }

        [UnityTest]
        public IEnumerator Completion_WhenTheQueuesObjectWasDisabled_IsReported_AndRunsOnceEnabledAgain()
        {
            var manager = AndroidDialogManager.Instance;
            Record(manager);
            var pump = UnityEngine.Object.FindFirstObjectByType<AndroidNativeCallbackQueue>();
            Assert.IsNotNull(pump);
            pump!.enabled = false;

            LogAssert.Expect(LogType.Error, Stranded);
            AndroidDialogManager.PostAnswer(new Api.Answer(Api.Kind.Confirm, Api.ErrorNone, buttonText: "Yes"));
            yield return null;
            yield return null;
            Assert.IsEmpty(_events, "a disabled object does not run the queue");

            AndroidNativeCallbackQueue.EnsureCreated(); // what each Show* does on a device
            yield return null;
            yield return null;
            Assert.AreEqual(new[] { "Confirm|Yes|True|null" }, _events);
        }

        [UnityTest]
        public IEnumerator Completion_WhenTheQueuesGameObjectWasDeactivated_RunsOnceActivatedAgain()
        {
            var manager = AndroidDialogManager.Instance;
            Record(manager);
            var pump = UnityEngine.Object.FindFirstObjectByType<AndroidNativeCallbackQueue>();
            Assert.IsNotNull(pump);
            GameObject pumpObject = pump!.gameObject;
            pumpObject.SetActive(false);

            LogAssert.Expect(LogType.Error, Stranded);
            AndroidDialogManager.PostAnswer(new Api.Answer(Api.Kind.Confirm, Api.ErrorNone, buttonText: "Yes"));
            yield return null;
            yield return null;
            Assert.IsEmpty(_events, "an inactive object does not run the queue");

            AndroidNativeCallbackQueue.EnsureCreated();
            Assert.IsTrue(pumpObject.activeSelf, "EnsureCreated activates the object again");
            yield return null;
            yield return null;
            Assert.AreEqual(new[] { "Confirm|Yes|True|null" }, _events);
        }

        [UnityTest]
        public IEnumerator Completion_QueuedBeforeTheQueuesObjectIsDisabled_IsReportedByTheDisable()
        {
            var manager = AndroidDialogManager.Instance;
            Record(manager);
            var pump = UnityEngine.Object.FindFirstObjectByType<AndroidNativeCallbackQueue>();
            Assert.IsNotNull(pump);

            // Queued while the object runs (no report), then the object is disabled before the next
            // frame: the disable finds the stranded completion (the other half of the Post / OnDisable
            // ordering).
            AndroidDialogManager.PostAnswer(new Api.Answer(Api.Kind.Confirm, Api.ErrorNone, buttonText: "Yes"));
            LogAssert.Expect(LogType.Error, Stranded);
            pump!.enabled = false;
            yield return null;
            yield return null;
            Assert.IsEmpty(_events);

            AndroidNativeCallbackQueue.EnsureCreated();
            yield return null;
            yield return null;
            Assert.AreEqual(new[] { "Confirm|Yes|True|null" }, _events);
        }

        [Test]
        public void EnsureCreated_OffTheMainThread_DoesNothingAndDoesNotThrow()
        {
            _ = AndroidDialogManager.Instance;
            var pump = UnityEngine.Object.FindFirstObjectByType<AndroidNativeCallbackQueue>();
            Assert.IsNotNull(pump);
            pump!.enabled = false;

            Exception? thrown = null;
            var thread = new Thread(() =>
            {
                try
                {
                    AndroidNativeCallbackQueue.EnsureCreated();
                }
                catch (Exception e)
                {
                    thrown = e;
                }
            });
            thread.Start();
            thread.Join();
            Assert.IsNull(thrown, "a Show* from another thread must not throw (1.x worked from other threads)");
            Assert.IsFalse(pump.enabled, "Unity objects are touched only from the main thread");

            AndroidNativeCallbackQueue.EnsureCreated();
            Assert.IsTrue(pump.enabled);
        }

        [UnityTest]
        public IEnumerator Queue_RunsWhatWasQueuedWhenTheFrameBegan()
        {
            AndroidDialogManager.Instance.ToString(); // creates the queue's object
            var order = new List<string>();
            AndroidNativeCallbackQueue.Post(() =>
            {
                order.Add($"first@{Time.frameCount}");
                AndroidNativeCallbackQueue.Post(() => order.Add($"second@{Time.frameCount}"));
            });
            yield return null;
            yield return null;
            yield return null;
            Assert.AreEqual(2, order.Count, string.Join(", ", order));
            int first = int.Parse(order[0].Split('@')[1]);
            int second = int.Parse(order[1].Split('@')[1]);
            Assert.Greater(second, first, "an action queued while the queue runs waits for the next frame");
        }
    }
}
#endif
