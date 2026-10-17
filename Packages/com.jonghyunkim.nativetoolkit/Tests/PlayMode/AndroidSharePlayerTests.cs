#nullable enable

#if UNITY_ANDROID
#if !UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using UnityEngine;
using UnityEngine.TestTools;
using JonghyunKim.NativeToolkit.Runtime.Android.Share;
using static JonghyunKim.NativeToolkit.Tests.AndroidSampleScreenDriver;
using static JonghyunKim.NativeToolkit.Tests.AndroidShareTestSupport;
using Ui = JonghyunKim.NativeToolkit.Tests.AndroidUiDriver;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// Layer 2b tests of the Android Share on a device: the sample screen and the manager, with the
    /// share sheet worked by the host-side UI driver. Test IDs (AS-01 to AS-13, AS-41 to AS-53) and
    /// expectations are in artifact/android/share/designs/2026-10-11-android-share-ui-test-plan-v6.md.
    /// </summary>
    /// <remarks>
    /// The share sheet pauses Unity while it shows (V-11), so each test sends its UI driver request
    /// first, opens the sheet in the same frame, and then waits for the answer. The 1.x values of the
    /// rows whose 1.x behaviour differs were recorded on the 1.x bridge on 2026-10-12 (the plan's 1.x
    /// column); the 1.x bridge left with stage 4, so only the C ABI is checked.
    /// </remarks>
    [Category(AndroidUiDriver.Category)]
    public sealed class AndroidSharePlayerTests
    {
        private const string TopMenuShareButton = "ShareFeatureButton";
        private const string ResultLabel = "ResultTextBlock";
        private const string SheetPackage = "com.android.intentresolver";
        private const string SampleTargetId = "native_toolkit_sample_target";

        private AndroidShareEvents _events = new();
        private AndroidSharePauseRecorder? _pauses;

        // What the test expects the events to come to, its own calls and the cleanup's (plan 3).
        private int _ops;
        private int _selected;
        private int _actions;

        // What the teardown undoes. Kept here, not undone in a finally of the test: when a step the
        // test yields fails, the Test Framework never resumes the test's own code.
        private readonly List<string> _cleanupIds = new();
        private bool _sampleLoaded;
        private Thread? _background;
        private CancellationTokenSource? _abandonBackground;

        // The per-call callbacks, in the order they ran, with the events: for the order checks.
        private readonly List<string> _calls = new();

        [SetUp]
        public void SetUp()
        {
            if (Application.platform != RuntimePlatform.Android)
                Assert.Ignore("Device-only.");
            Assert.IsTrue(OnCApi, "AndroidShareManager has no PendingCallCountForTests: the test seam is gone or renamed");
            _events.Unsubscribe();
            _events = new AndroidShareEvents();
            _events.Subscribe();
            _pauses = AndroidSharePauseRecorder.Create();
            _ops = _selected = _actions = 0;
            _cleanupIds.Clear();
            _calls.Clear();
            _sampleLoaded = false;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            var errors = new List<Exception>();

            // 1. What needs no UI.
            try
            {
                StopBackgroundCall(errors);
            }
            catch (Exception e)
            {
                errors.Add(e);
            }

            // 2. Direct Share targets this test registered, always (their results are counted).
            if (_cleanupIds.Count > 0)
            {
                try
                {
                    Manager.RemoveDirectShareTargets(new AndroidRemoveDirectShareTargetsPayload { ids = _cleanupIds.ToArray() });
                }
                catch (Exception e)
                {
                    errors.Add(e);
                }
                _cleanupIds.Clear();
            }

            // 3. Requests not collected: a test that collects them all leaves none.
            string? leftRequests = Ui.PendingCount == 0 ? null : $"{Ui.PendingCount} UI driver request(s) were not collected by the test";

            // 4. The UI steps, through Safely: collect, close a sheet left open (normally none: the
            // sheet pauses Unity, so this runs only after the driver could not close one), unload the
            // sample, whose OnDisable cancels the pending callback (counted).
            yield return Safely(Ui.WaitForPending(), errors);
            yield return Safely(CloseLeftoverSheet(), errors);
            if (_sampleLoaded)
            {
                yield return Safely(Unload(), errors);
                _sampleLoaded = false;
            }

            // 5. The counts, once everything has come, and a little after.
            float deadline = Time.realtimeSinceStartup + TearDownTimeoutSeconds;
            while (!_events.Reached(_ops, _selected, _actions) && Time.realtimeSinceStartup < deadline)
                yield return null;
            float since = Math.Max(_pauses?.ResumedAt ?? 0f, _events.LastEventAt ?? 0f);
            while (Time.realtimeSinceStartup < since + 0.5f)
                yield return null;
            yield return new WaitForSecondsRealtime(0.5f);
            var failures = errors.Where(e => !(e is IgnoreException)).Select(e => e.Message).ToList();
            string? mismatch = _events.Mismatch(_ops, _selected, _actions);
            if (mismatch != null) failures.Add(mismatch);
            string? offThread = _events.OffMainThread();
            if (offThread != null) failures.Add(offThread);
            if (leftRequests != null) failures.Add(leftRequests);

            // 6. The C ABI's calls all completed.
            deadline = Time.realtimeSinceStartup + TearDownTimeoutSeconds;
            while (PendingCallCount() != 0 && Time.realtimeSinceStartup < deadline)
                yield return null;
            if (PendingCallCount() != 0)
                failures.Add($"{PendingCallCount()} call(s) still waiting for their completion");

            // 7. Always.
            _events.Unsubscribe();
            if (_pauses != null) UnityEngine.Object.Destroy(_pauses.gameObject);
            _pauses = null;
            if (_sampleLoaded) yield return Safely(Unload(), errors);

            if (failures.Count > 0) Assert.Fail(string.Join("\n", failures));
            Exception? ignored = errors.FirstOrDefault(e => e is IgnoreException);
            if (ignored != null && TestContext.CurrentContext.Result.Outcome.Status != TestStatus.Failed)
                ExceptionDispatchInfo.Capture(ignored).Throw();
        }

        private static IEnumerator CloseLeftoverSheet()
        {
            yield return Ui.Do("count", Ui.Selector("package", SheetPackage));
            if (Ui.LastValue != "0") yield return Ui.Do("close-sheet");
        }

        private void StopBackgroundCall(List<Exception> errors)
        {
            if (_background == null) return;
            Thread thread = _background;
            _background = null;
            _abandonBackground!.Cancel();
            if (!thread.Join(TimeSpan.FromSeconds(30)))
                errors.Add(new AssertionException("AS-52's thread did not return within 30 s"));
            _abandonBackground.Dispose();
            _abandonBackground = null;
        }

        // ── 1. The sample screen ─────────────────────────────────────────────────
        // Every row counts one more Op: unloading the sample cancels its pending callback.

        [UnityTest, Category(AndroidUiDriver.SampleScreenCategory)] public IEnumerator AS01_SampleShareText() => SampleCloses("ShareTextButton", "[event] Share plain text: Success");
        [UnityTest, Category(AndroidUiDriver.SampleScreenCategory)] public IEnumerator AS02_SampleShareUrl() => SampleCloses("ShareUrlButton", "[event] Share URL: Success");
        [UnityTest, Category(AndroidUiDriver.SampleScreenCategory)] public IEnumerator AS03_SampleShareWithSubjectTitle() => SampleCloses("ShareWithSubjectTitleButton", "[event] Share with subject and title: Success");
        [UnityTest, Category(AndroidUiDriver.SampleScreenCategory)] public IEnumerator AS04_SampleShareRichPreview() => SampleCloses("ShareRichPreviewButton", "[event] Share with rich preview: Success");
        [UnityTest, Category(AndroidUiDriver.SampleScreenCategory)] public IEnumerator AS05_SampleShareImage() => SampleCloses("ShareImageButton", "[event] Share image: Success");
        [UnityTest, Category(AndroidUiDriver.SampleScreenCategory)] public IEnumerator AS06_SampleShareImages() => SampleCloses("ShareImagesButton", "[event] Share multiple images: Success");
        [UnityTest, Category(AndroidUiDriver.SampleScreenCategory)] public IEnumerator AS07_SampleShareFile() => SampleCloses("ShareFileButton", "[event] Share file: Success");
        [UnityTest, Category(AndroidUiDriver.SampleScreenCategory)] public IEnumerator AS08_SampleShareFiles() => SampleCloses("ShareFilesButton", "[event] Share multiple files: Success");

        [UnityTest, Category(AndroidUiDriver.SampleScreenCategory)]
        public IEnumerator AS09_SampleChooserAction()
        {
            yield return OpenSample();
            Expect(ops: 2, actions: 1);
            yield return SendThen(() => Press(FindButton("ShareCustomActionButton")!), "tap-in-sheet", Ui.Base64("Save"));
            yield return ExpectLabel("[event] ChooserAction: com.jonghyunkim.nativetoolkit.SHARE_CUSTOM_ACTION_SAVE");
            Assert.That(_events.Ops, Does.Contain("Op|shareText|True|null"), _events.Describe());
        }

        [UnityTest, Category(AndroidUiDriver.SampleScreenCategory)]
        public IEnumerator AS10_SampleShareWithCallback()
        {
            yield return OpenSample();
            Expect(ops: 2, selected: 1);
            yield return SendThen(() => Press(FindButton("ShareWithCallbackButton")!), "tap-in-sheet", Ui.Base64(AppLabel()));
            yield return ExpectLabel($"[onSelected] Selected: {AppPackage()}");
        }

        [UnityTest, Category(AndroidUiDriver.SampleScreenCategory)]
        public IEnumerator AS11_SampleInvalidFile()
        {
            yield return OpenSample();
            Expect(ops: 2);
            yield return SendThen(() => Press(FindButton("ShareInvalidFileButton")!), "no-sheet", "5000");
            yield return ExpectLabel("[event] Share invalid file (error handling demo): Failed\nError: File not found: /invalid/path/that/does/not/exist/sample.txt");
        }

        [UnityTest, Category(AndroidUiDriver.SampleScreenCategory)]
        public IEnumerator AS12_SampleDirectShareTarget()
        {
            yield return OpenSample();
            _cleanupIds.Add(SampleTargetId);
            Expect(ops: 4);
            Press(FindButton("RegisterDirectShareTargetButton")!);
            yield return ExpectLabel("[event] Register Direct Share target: Success");
            Press(FindButton("RemoveDirectShareTargetButton")!);
            yield return ExpectLabel("[event] Remove Direct Share targets: Success");
        }

        [UnityTest, Category(AndroidUiDriver.SampleScreenCategory)]
        public IEnumerator AS13_SampleHome_AndOpenAgain()
        {
            yield return OpenSample();
            Expect(ops: 3);
            Press(FindButton("HomeButton")!);
            yield return WaitFor(() => FindButton(TopMenuShareButton) != null && FindButton("ShareTextButton") == null, "the top menu");
            yield return Open(TopMenuShareButton, "ShareTextButton");
            yield return SendThen(() => Press(FindButton("ShareTextButton")!), "close-sheet");
            yield return ExpectLabel("[event] Share plain text: Success");
        }

        // ── 2.2 The manager with the share sheet ─────────────────────────────────

        [UnityTest]
        public IEnumerator AS41a_PreviewText() => ShareTextAnd(
            new AndroidShareTextPayload { text = "ntk share preview 41", previewTitle = "ntk preview title 41" },
            "present-in-sheet", Ui.Base64("ntk share preview 41"));

        [UnityTest]
        public IEnumerator AS41b_PreviewTitle() => ShareTextAnd(
            new AndroidShareTextPayload { text = "ntk share preview 41", previewTitle = "ntk preview title 41" },
            "present-in-sheet", Ui.Base64("ntk preview title 41"));

        [UnityTest]
        public IEnumerator AS42_ChooserAction()
        {
            Expect(ops: 1, actions: 1);
            yield return SendThen(() => Manager.ShareText(
                    new AndroidShareTextPayload { text = "ntk share 42", chooserActions = new[] { Action("A42", "ntk.a42") } },
                    onChooserAction: r => _calls.Add($"onChooserAction|{r.ActionId}|after {_events.Actions.Count} event(s)")),
                "tap-in-sheet", Ui.Base64("A42"));
            yield return Eventually(() => _calls.Count > 0);
            Assert.AreEqual(new[] { "Action|ntk.a42" }, _events.Actions);
            Assert.AreEqual(new[] { "onChooserAction|ntk.a42|after 1 event(s)" }, _calls, "the event comes before the per-call callback");
        }

        [UnityTest]
        public IEnumerator AS43a_DroppedAction_NotBase64() => ShareTextAnd(
            AS43Payload(), "absent-in-sheet", Ui.Base64("B43"), Ui.Base64("A43"));

        [UnityTest]
        public IEnumerator AS43b_DroppedAction_NotAnImage() => ShareTextAnd(
            AS43Payload(), "absent-in-sheet", Ui.Base64("C43"), Ui.Base64("A43"));

        /// <summary>AS-44a: actions with the same intentAction. Known difference: 1.x shows both (5.3).</summary>
        [UnityTest]
        public IEnumerator AS44a_DuplicateAction()
        {
            return ShareTextAnd(AS44Payload(), "absent-in-sheet", Ui.Base64("A44b"), Ui.Base64("A44a"));
        }

        [UnityTest]
        public IEnumerator AS44b_DuplicateAction_FirstFires()
        {
            Expect(ops: 1, actions: 1);
            yield return SendThen(() => Manager.ShareText(AS44Payload()), "tap-in-sheet", Ui.Base64("A44a"));
            yield return ExpectAction("Action|ntk.a44");
        }

        [UnityTest]
        public IEnumerator AS45_SendAction_NoCallback()
        {
            Expect(ops: 1);
            yield return SendThen(() => Manager.ShareText(new AndroidShareTextPayload
            {
                text = "ntk share 45",
                chooserActions = new[] { Action("S45", "android.intent.action.SEND") },
            }), "tap-in-sheet", Ui.Base64("S45"));
            yield return NotComing();
        }

        /// <summary>AS-46: an icon with the PNG signature and nothing readable. Known difference: 1.x skips the action and opens the sheet (5.3).</summary>
        [UnityTest]
        public IEnumerator AS46_BrokenIcon()
        {
            Expect(ops: 1);
            var payload = new AndroidShareTextPayload
            {
                text = "ntk share 46",
                chooserActions = new[] { new AndroidChooserActionPayload { label = "X46", iconBase64 = BrokenPngBase64(), intentAction = "ntk.x46" } },
            };
            yield return SendThen(() => Manager.ShareText(payload), "no-sheet", "5000");
            yield return ExpectOp("Op|shareText|False|Failed to shareText: a chooser action icon could not be read as an image.");
        }

        [UnityTest]
        public IEnumerator AS47_ShareWithCallback_Selected()
        {
            Expect(ops: 1, selected: 1);
            yield return SendThen(() => Manager.ShareWithCallback(new AndroidShareTextPayload { text = "ntk share 47" },
                    onStarted: r => _calls.Add($"onStarted|{r.IsSuccess}"),
                    onSelected: r => _calls.Add($"onSelected|{r.SelectedPackageName}|after {_events.Selected.Count} event(s)")),
                "tap-in-sheet", Ui.Base64(AppLabel()));
            yield return Eventually(() => _calls.Count >= 2);
            Assert.AreEqual(new[] { $"Selected|{AppPackage()}" }, _events.Selected);
            Assert.AreEqual(new[] { "onStarted|True", $"onSelected|{AppPackage()}|after 1 event(s)" }, _calls);
        }

        [UnityTest]
        public IEnumerator AS48_ShareWithCallback_Closed()
        {
            Expect(ops: 1);
            yield return SendThen(() => Manager.ShareWithCallback(new AndroidShareTextPayload { text = "ntk share 48" },
                onStarted: r => _calls.Add($"onStarted|{r.IsSuccess}"),
                onSelected: r => _calls.Add("onSelected")), "close-sheet");
            yield return NotComing();
            Assert.AreEqual(new[] { "onStarted|True" }, _calls);
        }

        /// <summary>AS-49: a selection after the cancel does not come: the cancel took the right request.</summary>
        [UnityTest]
        public IEnumerator AS49_CancelledSelection()
        {
            Expect(ops: 2);
            yield return SendThen(() =>
            {
                Manager.ShareWithCallback(new AndroidShareTextPayload { text = "ntk share 49" }, onSelected: r => _calls.Add("onSelected"));
                Manager.CancelPendingShareCallback();
            }, "tap-in-sheet", Ui.Base64(AppLabel()));
            yield return NotComing();
            Assert.IsEmpty(_calls);
            Assert.That(_events.Ops, Is.EquivalentTo(new[] { "Op|shareWithCallback|True|null", "Op|cancelPendingShareCallback|True|null" }));
        }

        [UnityTest]
        public IEnumerator AS50_InitializeTwice_ListenersOnce()
        {
            Manager.Initialize();
            Manager.Initialize();
            Expect(ops: 2, selected: 1, actions: 1);
            yield return SendThen(() => Manager.ShareText(
                new AndroidShareTextPayload { text = "ntk share 50", chooserActions = new[] { Action("A50", "ntk.a50") } }),
                "tap-in-sheet", Ui.Base64("A50"));
            yield return ExpectAction("Action|ntk.a50");
            yield return SendThen(() => Manager.ShareWithCallback(new AndroidShareTextPayload { text = "ntk share 50b" }),
                "tap-in-sheet", Ui.Base64(AppLabel()));
            yield return Eventually(() => _events.Selected.Count > 0);
            yield return NotComing();
        }

        [UnityTest]
        public IEnumerator AS51_DirectShareTargetShows()
        {
            string label = $"NTK AS51 {Guid.NewGuid().ToString("N").Substring(0, 6)}";
            _cleanupIds.Add("ntk_as51");
            Expect(ops: 4);
            Manager.RemoveDirectShareTargets(new AndroidRemoveDirectShareTargetsPayload { ids = new[] { "ntk_as51" } });
            yield return ExpectOp("Op|removeDirectShareTargets|True|null");
            Manager.RegisterDirectShareTarget(new AndroidDirectShareTargetPayload { id = "ntk_as51", label = label, iconBase64 = IconBase64() });
            yield return ExpectOp("Op|registerDirectShareTarget|True|null");
            yield return SendThen(() => Manager.ShareText(new AndroidShareTextPayload { text = "ntk share 51" }),
                "present-in-sheet", Ui.Base64(label));
        }

        /// <summary>
        /// AS-52: ShareText from another thread while the app is in the background. On the C ABI it
        /// fails as not in the foreground. 1.x could not be called from another thread at all (AS-35).
        /// no-sheet watches for a sheet that shows after the app comes back.
        /// </summary>
        [UnityTest]
        public IEnumerator AS52_InTheBackground()
        {
            _ = Manager;
            Expect(ops: 1);
            // Makes the UI driver ready, so the thread can work out the answer file's path.
            yield return Ui.Do("count", Ui.Selector("package", SheetPackage));
            int background = Ui.NextNumber;
            string? threadError = null;
            var abandonSource = new CancellationTokenSource();
            CancellationToken abandon = abandonSource.Token;
            var thread = new Thread(() =>
            {
                try
                {
                    if (!Ui.WaitForAnswerFile(background, TimeSpan.FromSeconds(20), abandon))
                    {
                        threadError = "the UI driver did not report the app stopped";
                        return;
                    }
                    if (abandon.IsCancellationRequested) return;
                    Manager.ShareText(new AndroidShareTextPayload { text = "ntk share 52" });
                }
                catch (Exception e)
                {
                    threadError = e.ToString();
                }
            });
            thread.Start();
            _abandonBackground = abandonSource;
            _background = thread;
            yield return Ui.Send("background", "3000");
            yield return Ui.Send("no-sheet", "10000");
            int watch = Ui.LastSent;
            yield return Ui.Wait(background, Ui.SheetAnswerTimeoutSeconds);
            string answer = "";
            yield return Answer(watch, a => answer = a);
            yield return Eventually(() => _events.Ops.Count > 0);
            Assert.IsNull(threadError, threadError);
            Assert.AreEqual("ok", answer, "a share sheet showed after the app came back");
            Assert.AreEqual(new[]
            {
                "Op|shareText|False|Failed to shareText: the app is not in the foreground.",
            }, _events.Ops);
        }

        /// <summary>
        /// AS-53: ShareText from another thread. The C ABI opens the sheet. 1.x could not be called
        /// from another thread (AS-35): the call failed and no sheet showed.
        /// </summary>
        [UnityTest]
        public IEnumerator AS53_FromAnotherThread()
        {
            _ = Manager;
            Expect(ops: 1);
            Exception? thrown = null;
            yield return SendThen(() =>
            {
                var thread = new Thread(() =>
                {
                    try
                    {
                        Manager.ShareText(new AndroidShareTextPayload { text = "ntk share 53" });
                    }
                    catch (Exception e)
                    {
                        thrown = e;
                    }
                });
                thread.Start();
                thread.Join();
            }, "close-sheet");
            Assert.IsNull(thrown, thrown?.ToString());
            yield return ExpectOp("Op|shareText|True|null");
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private static AndroidShareManager Manager => AndroidShareManager.Instance;

        private void Expect(int ops = 0, int selected = 0, int actions = 0)
        {
            _ops = ops;
            _selected = selected;
            _actions = actions;
        }

        /// <summary>
        /// Sends the UI driver request, makes the call in the same frame (the share sheet pauses Unity
        /// as soon as it shows), and waits for the answer.
        /// </summary>
        private IEnumerator SendThen(Action call, string command, params string[] args)
        {
            yield return Ui.Send(command, args);
            int number = Ui.LastSent;
            call();
            yield return Ui.Wait(number, Ui.SheetAnswerTimeoutSeconds);
        }

        /// <summary>Waits for an answer without failing the test, and hands it over.</summary>
        private static IEnumerator Answer(int number, Action<string> onAnswer)
        {
            IEnumerator wait = Ui.Wait(number, Ui.SheetAnswerTimeoutSeconds);
            string answer;
            while (true)
            {
                try
                {
                    if (!wait.MoveNext()) { answer = Ui.LastAnswer; break; }
                }
                catch (AssertionException e) { answer = e.Message; break; }
                yield return wait.Current;
            }
            onAnswer(answer);
        }

        private IEnumerator ShareTextAnd(AndroidShareTextPayload payload, string command, params string[] args)
        {
            Expect(ops: 1);
            yield return SendThen(() => Manager.ShareText(payload), command, args);
            yield return ExpectOp("Op|shareText|True|null");
        }

        private IEnumerator OpenSample()
        {
            if (!SampleSceneIsInThePlayer())
                Assert.Ignore("The sample scene is not in this player (the Test Android™ Profile builds the test scene only).");
            _sampleLoaded = true;
            yield return LoadAtTopMenu(TopMenuShareButton);
            yield return Open(TopMenuShareButton, "ShareTextButton");
        }

        private IEnumerator SampleCloses(string button, string expected)
        {
            yield return OpenSample();
            Expect(ops: 2);
            yield return SendThen(() => Press(FindButton(button)!), "close-sheet");
            yield return ExpectLabel(expected);
        }

        private static IEnumerator ExpectLabel(string expected)
        {
            yield return Eventually(() => LabelText(ResultLabel) == expected);
            Assert.AreEqual(expected, LabelText(ResultLabel), "what the sample screen shows");
        }

        private IEnumerator ExpectOp(string expected)
        {
            yield return Eventually(() => _events.Ops.Contains(expected));
            Assert.That(_events.Ops, Does.Contain(expected), _events.Describe());
        }

        private IEnumerator ExpectAction(string expected)
        {
            yield return Eventually(() => _events.Actions.Contains(expected));
            Assert.That(_events.Actions, Does.Contain(expected), _events.Describe());
        }

        /// <summary>
        /// For a check that an event does not come: a selection or chooser action broadcast may arrive
        /// in the frames after the player resumes, so this waits 2 s from the resume (plan 3).
        /// </summary>
        private IEnumerator NotComing()
        {
            yield return Eventually(() => _pauses!.ResumedAt != null);
            float since = _pauses!.ResumedAt ?? Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup < since + 2f)
                yield return null;
        }

        private static IEnumerator Eventually(Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + EventTimeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline)
                yield return null;
        }

        private static AndroidChooserActionPayload Action(string label, string intentAction) =>
            new() { label = label, iconBase64 = IconBase64(), intentAction = intentAction };

        /// <summary>AS-43: a blank label, an icon that is not Base64 (B43), one that is not an image (C43), and a good one (A43).</summary>
        private static AndroidShareTextPayload AS43Payload() => new()
        {
            text = "ntk share 43",
            chooserActions = new[]
            {
                Action("  ", "ntk.blank43"),
                new AndroidChooserActionPayload { label = "B43", iconBase64 = "@@@ not base64 @@@", intentAction = "ntk.b43" },
                new AndroidChooserActionPayload { label = "C43", iconBase64 = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes("abc")), intentAction = "ntk.c43" },
                Action("A43", "ntk.a43"),
            },
        };

        private static AndroidShareTextPayload AS44Payload() => new()
        {
            text = "ntk share 44",
            chooserActions = new[] { Action("A44a", "ntk.a44"), Action("A44b", "ntk.a44") },
        };
    }
}
#endif
#endif
