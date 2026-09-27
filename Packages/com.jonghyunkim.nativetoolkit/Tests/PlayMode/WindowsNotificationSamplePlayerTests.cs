#nullable enable

// Windows player only: the notification manager reaches the native library only there, and in the
// Editor the Notification screen stops on an editor dialog in Awake.
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using JonghyunKim.NativeToolkit.Runtime.Windows.Notification;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static JonghyunKim.NativeToolkit.Tests.WindowsClipboardSampleScreenDriver;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// Drives the Windows Notification sample on a player and checks its toasts in the notification
    /// center: W-01 to W-11 of artifact/features/notification/designs/2026-09-27-windows-notification-ui-test-plan-v1.md.
    /// <para>
    /// The notification center is worked from a PowerShell process through UI Automation. It is
    /// ShellExperienceHost's CoreWindow, which neither EnumWindows nor UI Automation's top-level
    /// windows list; right after 'ms-actioncenter:' opens it, it is the foreground window, and is
    /// read from its handle. Esc closes it only while it is in front, so the process closes it
    /// before it exits whatever happened.
    /// </para>
    /// <para>
    /// Destructive: the toasts reach the developer's notification center, and every Initialize
    /// writes the app's COM server and AUMID under HKCU (plan, chapter 3). Run only with
    /// verify_unity_windows.sh --include-destructive.
    /// </para>
    /// </summary>
    [Category(WindowsClipboardPlayerTests.DestructiveCategory)]
    public sealed class WindowsNotificationSamplePlayerTests
    {
        private const string TopMenuNotificationButton = "NotificationFeatureButton";
        private const string ResultLabel = "ResultTextBlock";

        /// <summary>The toast's sender: the product name, which the build profiles set.</summary>
        private static string App => Application.productName;

        private const string ShownTitle = "Energy Refilled";
        private const string ShownBody = "Your squad is fully rested. Jump back in and clear the next raid.";

        private const string ScheduledTitle = "Guild Battle Starts Soon";
        private const string ProgressTitle = "Downloading...";

        /// <summary>
        /// The sample schedules its toast one minute ahead (DateTimeOffset.UtcNow.AddMinutes(1)).
        /// The tests wait past that before looking, so a toast that did not come has had its chance.
        /// </summary>
        private const float ScheduledSeconds = 60f;

        // The Test Framework stops a test after 180 s; the scheduled cases take about 90.
        private const int ScheduledTimeoutMilliseconds = 5 * 60 * 1000;
        private const float InvokedSeconds = 15f;

        private readonly WindowsNotificationCenter _center = new();

        [UnitySetUp]
        public IEnumerator LoadTheSample()
        {
            yield return TakeForegroundAndLog(TestContext.CurrentContext.Test.Name);
            yield return LoadAtTopMenu();
            yield return OpenNotificationScreen();

            // The buttons are on the screen a frame or two before Start wires their clicks, and a
            // press made then does nothing (2026-09-27). Press until the screen answers.
            string? initialized = null;
            yield return Eventually(() =>
            {
                Press(FindButton("InitializeButton")!);
                initialized = ResultText();
                return initialized?.StartsWith("✓ Initialize") == true;
            }, _ => { });
            StringAssert.StartsWith("✓ Initialize", initialized, "Initialize before the test");

            // Not before Initialize: without a manager the setting reads Unknown (design v8, 8.2).
            Assert.AreEqual(WindowsNotificationSetting.Enabled, WindowsNotificationManager.Instance.GetNotificationSetting(),
                "notifications are off for this PC or this app; turn them on in Settings > System > Notifications " +
                "(Notifications, and Unity NativeToolkit), or every toast test fails with error 2");

            string? removed = null;
            yield return PressAndRead("RemoveAllButton", t => removed = t);
            StringAssert.StartsWith("✓ RemoveAllNotifications", removed, "RemoveAll before the test");
        }

        [UnityTearDown]
        public IEnumerator UnloadTheSample()
        {
            _center.Stop();
            WindowsNotificationManager.Instance.RemoveAllNotifications();
            yield return Unload();
        }

        // ── W-01 ─────────────────────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator Initialize_Succeeds()
        {
            string? text = null;
            yield return PressAndRead("InitializeButton", t => text = t);
            StringAssert.StartsWith("✓ Initialize", text);
        }

        // ── W-03 ─────────────────────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator ShowNotification_ReachesTheNotificationCenter()
        {
            string? text = null;
            yield return PressAndRead("ShowNotificationButton", t => text = t);
            StringAssert.StartsWith("✓ ShowNotification", text);

            yield return _center.Work($"find {ShownTitle}");
            StringAssert.Contains($"found [{ShownTitle}] content=[{ShownBody}]", _center.Log);
        }

        // ── W-04 ─────────────────────────────────────────────────────────────────

        [UnityTest]
        [Ignore("W-04 on hold: the 1.x library does not write AppUserModelId\\<AUMID>\\CustomActivator, without which Windows " +
                "routes no click to a running unpackaged app. Fixed in native-toolkit c9f4071b (dist 1.12.0); comes back with the C ABI 2.0.0 migration. " +
                "artifact/features/notification/designs/2026-09-27-windows-notification-ui-test-plan-v1.md, chapter 2")]
        public IEnumerator ShowNotification_OpenInTheCenter_ComesBackAsInvoked()
        {
            // Windows 11 routes a click on an unpackaged app's toast to the COM class named by
            // AppUserModelId\<AUMID>\CustomActivator; the shortcut's ToastActivatorCLSID alone does not
            // get it there (native-toolkit measured it, 2026-09-27). 1.x registers its class but never
            // writes that value, and one left by a Windows App SDK build points elsewhere. Either
            // way no click comes, so say which, rather than time out.
            string? activator = ReadCurrentUserString($@"Software\Classes\AppUserModelId\{App}", "CustomActivator");
            Assert.IsNotNull(activator,
                $"HKCU\\Software\\Classes\\AppUserModelId\\{App} has no CustomActivator, so Windows routes no click to this player. " +
                "The 1.x library does not write it (plan, chapter 2).");
            string? server = ReadCurrentUserString($@"Software\Classes\CLSID\{activator}\LocalServer32", "");
            string exe = Process.GetCurrentProcess().MainModule!.FileName;
            Assert.IsTrue(server != null && server.Trim('"').StartsWith(exe, StringComparison.OrdinalIgnoreCase),
                $"CustomActivator {activator} is served by [{server}], not by this player ({exe}), so the click goes there (plan, chapter 2).");

            // Diagnostics while the click does not arrive (2026-09-27). The dispatcher is created on
            // first use; not created here, since doing so would hide a first use off the main thread.
            TestContext.WriteLine($"dispatcher present before the click: {GameObject.Find("UnityMainThreadDispatcher") != null}");
            string? managerArgs = null;
            void OnInvoked(string args) => managerArgs = args;
            WindowsNotificationManager.Instance.NotificationInvoked += OnInvoked;

            yield return PressAndRead("ShowNotificationButton", _ => { });
            yield return _center.Work($"open {ShownTitle}|Open");

            // The click comes back through the COM activator and the main thread dispatcher.
            bool invoked = false;
            string players = "";
            float waitedFrom = Time.realtimeSinceStartup;
            yield return Eventually(() =>
            {
                int count = Process.GetProcessesByName(Process.GetCurrentProcess().ProcessName).Length;
                string mark = $"{Time.realtimeSinceStartup - waitedFrom:0}s:{count}";
                if (!players.EndsWith(":" + count)) players += (players.Length > 0 ? " " : "") + mark;
                return ResultText()?.StartsWith("NotificationInvoked: ") == true;
            }, ok => invoked = ok, InvokedSeconds);
            WindowsNotificationManager.Instance.NotificationInvoked -= OnInvoked;
            TestContext.WriteLine($"manager NotificationInvoked: {managerArgs ?? "(none)"}; test players over time: {players}; " +
                $"dispatcher present after: {GameObject.Find("UnityMainThreadDispatcher") != null}");
            Assert.IsTrue(invoked, $"no NotificationInvoked on the screen within {InvokedSeconds}s: [{ResultText()}]");
            StringAssert.Contains("action", ResultText());
            StringAssert.Contains("open", ResultText());

            // Had this player not taken the activation, Windows would have started the registered
            // exe - this one - again, which would begin running the tests too.
            Assert.AreEqual(1, Process.GetProcessesByName(Process.GetCurrentProcess().ProcessName).Length,
                "another test player was started to take the click");
        }

        // ── W-02 ─────────────────────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator GetSetting_ReportsEnabled()
        {
            string? text = null;
            yield return PressAndRead("GetSettingButton", t => text = t);
            Assert.AreEqual("✓ NotificationSetting: Enabled", text);
        }

        // ── W-05 / W-06 ──────────────────────────────────────────────────────────

        [UnityTest, Timeout(ScheduledTimeoutMilliseconds)]
        public IEnumerator ScheduleNotification_ArrivesAboutAMinuteLater()
        {
            string? text = null;
            yield return PressAndRead("ScheduleNotificationButton", t => text = t);
            StringAssert.StartsWith("✓ ScheduleNotification (+1m)", text);

            // Not there yet, then there once the minute is up.
            yield return _center.Work($"absent {ScheduledTitle}");
            yield return new WaitForSecondsRealtime(ScheduledSeconds - 10f);
            yield return _center.Work($"find {ScheduledTitle}", 40000);
        }

        [UnityTest, Timeout(ScheduledTimeoutMilliseconds)]
        public IEnumerator ScheduleThenCancel_NeverArrives()
        {
            string? text = null;
            yield return PressAndRead("ScheduleNotificationButton", t => text = t);
            StringAssert.StartsWith("✓ ScheduleNotification (+1m)", text);
            yield return PressAndRead("CancelScheduledButton", t => text = t);
            StringAssert.StartsWith("✓ CancelScheduledNotification", text);

            yield return new WaitForSecondsRealtime(ScheduledSeconds + 15f);
            yield return _center.Work($"absent {ScheduledTitle}");
        }

        // ── W-07 / W-08 ──────────────────────────────────────────────────────────

        /// <remarks>The center reads the bar on a 0-100 scale.</remarks>
        [UnityTest]
        public IEnumerator UpdateProgress_MovesTheBarFrom30To50()
        {
            string? text = null;
            yield return PressAndRead("ShowProgressNotificationButton", t => text = t);
            StringAssert.StartsWith("✓ ShowProgressNotification", text);
            yield return _center.Work($"progress {ProgressTitle}");
            StringAssert.Contains($"progress [{ProgressTitle}] value=30", _center.Log);

            yield return PressAndRead("UpdateProgressButton", t => text = t);
            StringAssert.StartsWith("✓ UpdateProgress (seq=1)", text);
            yield return _center.Work($"progress {ProgressTitle}");
            StringAssert.Contains($"progress [{ProgressTitle}] value=50", _center.Log);
        }

        /// <remarks>The set-up removes every toast of the app, so there is no progress toast to update.</remarks>
        [UnityTest]
        public IEnumerator UpdateProgress_WithoutAProgressToast_SaysNotFound()
        {
            string? text = null;
            yield return PressAndRead("UpdateProgressButton", t => text = t);
            StringAssert.StartsWith("✗ UpdateProgress\nProgress notification not found.", text);
        }

        // ── W-09 / W-10 ──────────────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator RemoveByTag_TakesTheToastOutOfTheCenter()
        {
            yield return PressAndRead("ShowNotificationButton", _ => { });
            yield return _center.Work($"find {ShownTitle}");

            string? text = null;
            yield return PressAndRead("RemoveByTagButton", t => text = t);
            StringAssert.StartsWith("✓ RemoveByTag (win-sample-notification)", text);
            yield return _center.Work($"absent {ShownTitle}");
        }

        [UnityTest]
        public IEnumerator RemoveAll_TakesTheToastOutOfTheCenter()
        {
            yield return PressAndRead("ShowNotificationButton", _ => { });
            yield return _center.Work($"find {ShownTitle}");

            string? text = null;
            yield return PressAndRead("RemoveAllButton", t => text = t);
            StringAssert.StartsWith("✓ RemoveAllNotifications", text);
            yield return _center.Work($"absent {ShownTitle}");
        }

        // ── Recorded on 1.x before the move to the 2.0.0 C ABI ──────────────────
        // artifact/features/notification/designs/2026-09-27-windows-notification-design-v8.md, 5.6
        // step 1 and 7.3: these call the manager directly and pin down what 1.x does, so the
        // migration can compare. The payloads are the fixtures of the UI test plan v2.

        /// <summary>
        /// Every key the payload takes, over four toasts: one JSON cannot hold every audio type, and
        /// expiresOnReboot is kept apart as a packaged-app setting. Each is shown and looked for in
        /// the notification center by its title.
        /// </summary>
        [UnityTest]
        public IEnumerator ShowNotification_FixtureA_EveryKey_ReachesTheCenter() => ShowFixture(FixtureA, FixtureATitle);

        [UnityTest]
        public IEnumerator ShowNotification_FixtureB_MuteAudioAndProgress_ReachesTheCenter() => ShowFixture(FixtureB, FixtureBTitle);

        [UnityTest]
        public IEnumerator ShowNotification_FixtureC_AudioFromAUri_ReachesTheCenter() => ShowFixture(FixtureC, FixtureCTitle);

        [UnityTest]
        public IEnumerator ShowNotification_FixtureD_ExpiresOnReboot_ReachesTheCenter() => ShowFixture(FixtureD, FixtureDTitle);

        [UnityTest]
        public IEnumerator ShowNotification_BrokenJson_Is3()
        {
            AssertOneResult(r => WindowsNotificationManager.Instance.ShowNotification("{\"title\":", r), 3);
            yield break;
        }

        [UnityTest]
        public IEnumerator ShowNotification_SixButtons_Is7()
        {
            string buttons = string.Join(",", Enumerable.Range(1, 6).Select(i => $"{{\"label\":\"B{i}\"}}"));
            AssertOneResult(r => WindowsNotificationManager.Instance.ShowNotification($"{{\"title\":\"Six\",\"buttons\":[{buttons}]}}", r), 7);
            yield break;
        }

        /// <summary>
        /// The three APIs the sample no longer has buttons for report 8, not supported, for an
        /// unpackaged app (issue unreachable-notification-apis.md). GetAll reports through its own
        /// event and not through NotificationOperationCompleted.
        /// </summary>
        [UnityTest]
        public IEnumerator BadgeRemoveByIdAndGetAll_AreNotSupportedUnpackaged()
        {
            WindowsNotificationManager manager = WindowsNotificationManager.Instance;
            AssertOneResult(r => manager.SetBadge(1, r), 8);
            AssertOneResult(r => manager.RemoveNotificationById(1, r), 8);

            var perCall = new List<(string?, WindowsNotificationResult)>();
            var completed = new List<(string?, WindowsNotificationResult)>();
            var operations = new List<WindowsNotificationResult>();
            void OnCompleted(string? json, WindowsNotificationResult result) => completed.Add((json, result));
            void OnOperation(WindowsNotificationResult result) => operations.Add(result);
            manager.GetAllNotificationsCompleted += OnCompleted;
            manager.NotificationOperationCompleted += OnOperation;
            try
            {
                manager.GetAllNotifications((json, result) => perCall.Add((json, result)));
            }
            finally
            {
                manager.GetAllNotificationsCompleted -= OnCompleted;
                manager.NotificationOperationCompleted -= OnOperation;
            }

            Assert.AreEqual(1, perCall.Count, "per-call results");
            Assert.AreEqual(1, completed.Count, "GetAllNotificationsCompleted");
            CollectionAssert.IsEmpty(operations, "NotificationOperationCompleted");
            Assert.IsNull(perCall[0].Item1);
            Assert.AreEqual((false, 8, WindowsNotificationManager.OperationGetAll),
                (perCall[0].Item2.IsSuccess, perCall[0].Item2.ErrorCode, perCall[0].Item2.Operation));
            Assert.AreEqual(perCall[0].Item2.ErrorCode, completed[0].Item2.ErrorCode);
            yield break;
        }

        /// <remarks>1.x refuses a value below the lowest glyph before it looks at anything else.</remarks>
        [UnityTest]
        public IEnumerator SetBadge_BelowTheLowestGlyph_Is7()
        {
            AssertOneResult(r => WindowsNotificationManager.Instance.SetBadge(-7, r), 7);
            yield break;
        }

        /// <remarks>
        /// The per-call callback and the event are called in one try: when the callback throws, the
        /// exception is logged as an error and the event does not come (1.x's FireResult).
        /// </remarks>
        [UnityTest]
        public IEnumerator APerCallCallbackThatThrows_IsLogged_AndTheEventDoesNotCome()
        {
            WindowsNotificationManager manager = WindowsNotificationManager.Instance;
            var operations = new List<WindowsNotificationResult>();
            void OnOperation(WindowsNotificationResult result) => operations.Add(result);
            manager.NotificationOperationCompleted += OnOperation;
            try
            {
                LogAssert.Expect(LogType.Error, new Regex("per-call callback failed"));
                manager.RemoveAllNotifications(_ => throw new InvalidOperationException("per-call callback failed"));
            }
            finally
            {
                manager.NotificationOperationCompleted -= OnOperation;
            }
            CollectionAssert.IsEmpty(operations, "NotificationOperationCompleted after the callback threw");
            yield break;
        }

        [UnityTest]
        public IEnumerator APerCallCallbackThatThrows_OnGetAll_IsLogged_AndItsEventDoesNotCome()
        {
            WindowsNotificationManager manager = WindowsNotificationManager.Instance;
            int completed = 0;
            void OnCompleted(string? json, WindowsNotificationResult result) => completed++;
            manager.GetAllNotificationsCompleted += OnCompleted;
            try
            {
                LogAssert.Expect(LogType.Error, new Regex("per-call callback failed"));
                manager.GetAllNotifications((_, __) => throw new InvalidOperationException("per-call callback failed"));
            }
            finally
            {
                manager.GetAllNotificationsCompleted -= OnCompleted;
            }
            Assert.AreEqual(0, completed, "GetAllNotificationsCompleted after the callback threw");
            yield break;
        }

        // ── Fixtures (UI test plan v2) ───────────────────────────────────────────

        internal static string IconUri => new Uri(Path.Combine(Application.streamingAssetsPath, "app-icon.png")).AbsoluteUri;

        private const string FixtureATitle = "Fixture A: every key";
        private const string FixtureBTitle = "Fixture B: mute and progress";
        private const string FixtureCTitle = "Fixture C: audio from a uri";
        private const string FixtureDTitle = "Fixture D: expires on reboot";

        /// <summary>
        /// Every key but the other audio types, progress and expiresOnReboot. Only the reminder
        /// scenario: alarm, urgent and incoming call keep their banner up past do-not-disturb and
        /// could take the foreground window the center helper relies on.
        /// </summary>
        private static string FixtureA =>
            "{\"title\":\"" + FixtureATitle + "\",\"body\":\"Every key the payload takes, but the ones fixtures B to D carry.\"," +
            "\"tag\":\"ntk-fixture\",\"group\":\"ntk-fixture-group\",\"scenario\":\"reminder\",\"duration\":\"long\"," +
            "\"buttons\":[{\"label\":\"Open\",\"args\":{\"action\":\"open\",\"item\":\"7\"}},{\"label\":\"Docs\",\"invokeUri\":\"https://example.com/\"}]," +
            "\"textBoxes\":[{\"id\":\"reply\",\"placeholder\":\"Type a reply\",\"title\":\"Reply\"},{\"id\":\"note\"}]," +
            "\"comboBoxes\":[{\"id\":\"choice\",\"title\":\"Pick\",\"defaultSelection\":\"b\",\"items\":[{\"id\":\"a\",\"label\":\"A\"},{\"id\":\"b\",\"label\":\"B\"}]}]," +
            "\"appLogo\":{\"uri\":\"" + IconUri + "\",\"crop\":\"circle\"},\"heroImage\":\"" + IconUri + "\",\"inlineImage\":\"" + IconUri + "\"," +
            "\"audio\":{\"type\":\"event\",\"event\":\"reminder\",\"loop\":true}," +
            "\"attribution\":\"via NativeToolkit tests\",\"timestamp\":" + DateTimeOffset.UtcNow.ToUnixTimeSeconds() + ",\"expiration\":3600}";

        private static string FixtureB =>
            "{\"title\":\"" + FixtureBTitle + "\",\"body\":\"Muted, with a progress bar.\",\"tag\":\"ntk-fixture-b\",\"group\":\"ntk-fixture-group\"," +
            "\"audio\":{\"type\":\"mute\"},\"progress\":{\"title\":\"Progress\",\"value\":0.4,\"valueStr\":\"40%\",\"status\":\"Working\"}}";

        private static string FixtureC =>
            "{\"title\":\"" + FixtureCTitle + "\",\"body\":\"A sound from a file.\",\"tag\":\"ntk-fixture-c\",\"group\":\"ntk-fixture-group\"," +
            "\"audio\":{\"type\":\"uri\",\"uri\":\"file:///C:/Windows/Media/Windows%20Notify%20Email.wav\"}}";

        private static string FixtureD =>
            "{\"title\":\"" + FixtureDTitle + "\",\"body\":\"Dropped at the next reboot.\",\"tag\":\"ntk-fixture-d\",\"group\":\"ntk-fixture-group\"," +
            "\"expiresOnReboot\":true}";

        private IEnumerator ShowFixture(string json, string title)
        {
            AssertOneResult(r => WindowsNotificationManager.Instance.ShowNotification(json, r), 0);
            yield return _center.Work($"find {title}");
        }

        /// <summary>
        /// Makes a call with a per-call callback and checks it reported once with the code, and that
        /// NotificationOperationCompleted reported the same once.
        /// </summary>
        internal static void AssertOneResult(Action<Action<WindowsNotificationResult>> call, int errorCode)
        {
            var perCall = new List<WindowsNotificationResult>();
            var operations = new List<WindowsNotificationResult>();
            void OnOperation(WindowsNotificationResult result) => operations.Add(result);
            WindowsNotificationManager.Instance.NotificationOperationCompleted += OnOperation;
            try
            {
                call(perCall.Add);
            }
            finally
            {
                WindowsNotificationManager.Instance.NotificationOperationCompleted -= OnOperation;
            }

            Assert.AreEqual(1, perCall.Count, "per-call results");
            Assert.AreEqual(1, operations.Count, "NotificationOperationCompleted");
            Assert.AreEqual((errorCode == 0, errorCode), (perCall[0].IsSuccess, perCall[0].ErrorCode),
                $"result: {perCall[0].ErrorMessage}");
            Assert.AreEqual(perCall[0].ErrorCode, operations[0].ErrorCode);
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        internal static IEnumerator OpenNotificationScreen()
        {
            Press(FindButton(TopMenuNotificationButton)!);
            bool opened = false;
            yield return Eventually(() => FindButton("InitializeButton") != null, ok => opened = ok);
            Assert.IsTrue(opened, "the Windows Notification screen did not open");
        }

        internal static string? ResultText() =>
            SampleDocument()?.rootVisualElement?.Q<Label>(ResultLabel)?.text;

        /// <summary>Presses a sample button and hands over what the screen then shows (results arrive in the same call).</summary>
        internal static IEnumerator PressAndRead(string button, Action<string?> text)
        {
            Button? found = FindButton(button);
            Assert.IsNotNull(found, $"no {button} on the screen");
            Press(found!);
            yield return null;
            text(ResultText());
        }

        private static string? ReadCurrentUserString(string subKey, string value)
        {
            var currentUser = new IntPtr(unchecked((int)0x80000001));
            var data = new StringBuilder(512);
            uint size = (uint)data.Capacity * 2;
            int status = RegGetValueW(currentUser, subKey, value, RrfRtRegSz, IntPtr.Zero, data, ref size);
            return status == 0 ? data.ToString() : null;
        }

        private const uint RrfRtRegSz = 0x2;

        [System.Runtime.InteropServices.DllImport("advapi32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern int RegGetValueW(IntPtr key, string subKey, string value, uint flags, IntPtr type,
            StringBuilder data, ref uint size);
    }

    /// <summary>
    /// Opens the notification center from a PowerShell process, works it through UI Automation, and
    /// closes it. The notification center is ShellExperienceHost's CoreWindow, which neither
    /// EnumWindows nor UI Automation's top-level windows list; right after 'ms-actioncenter:' opens
    /// it, it is the foreground window, and is read from its handle. Esc closes it only while it is
    /// in front, so the process closes it before it exits whatever happened. Toasts are looked for
    /// under the product name, the sender the build profiles set.
    /// </summary>
    internal sealed class WindowsNotificationCenter
    {
        /// <summary>How long the helper waits for a toast to appear, by default.</summary>
        internal const int DefaultWaitMilliseconds = 15000;

        private Process? _center;
        private readonly ConcurrentQueue<string> _output = new();

        /// <summary>Runs the center helper with the steps and waits for it; fails if a step failed.</summary>
        internal IEnumerator Work(string steps, int waitMilliseconds = DefaultWaitMilliseconds)
        {
            _center?.Dispose();
            while (_output.TryDequeue(out _)) { }

            string script = Path.Combine(Application.temporaryCachePath, "ntk-notification-center.ps1");
            File.WriteAllText(script, CenterScript);
            string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(steps));
            var info = new ProcessStartInfo(
                "powershell.exe",
                $"-NoProfile -ExecutionPolicy Bypass -File \"{script}\" -App \"{Application.productName}\" -StepsBase64 {encoded} -TimeoutMs {waitMilliseconds}")
            {
                UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true,
            };
            _center = Process.Start(info);
            Assert.IsNotNull(_center, "powershell.exe did not start");
            _center!.OutputDataReceived += (_, e) => { if (e.Data != null) _output.Enqueue(e.Data); };
            _center.ErrorDataReceived += (_, e) => { if (e.Data != null) _output.Enqueue("stderr: " + e.Data); };
            _center.BeginOutputReadLine();
            _center.BeginErrorReadLine();

            // Opening and closing the center takes a few seconds on top of the wait for a toast.
            float exitSeconds = waitMilliseconds / 1000f + 45f;
            yield return Eventually(() => _center.HasExited, _ => { }, exitSeconds);
            if (!_center.HasExited) Assert.Fail($"the center helper was still running {exitSeconds}s later: {Log}");
            _center.WaitForExit();
            TestContext.WriteLine($"center: {Log}");
            Assert.AreEqual(0, _center.ExitCode, $"the center helper failed: {Log}");
        }

        /// <summary>What the last run of the helper printed, one line per step.</summary>
        internal string Log => string.Join(" | ", _output.ToArray());

        /// <summary>Stops a helper still running (a test that failed while waiting) and lets it go.</summary>
        internal void Stop()
        {
            if (_center != null && !_center.HasExited) _center.Kill();
            _center?.Dispose();
            _center = null;
        }

        /// <summary>
        /// Opens the notification center, works it by the steps, and closes it. Windows PowerShell 5.1,
        /// nothing installed. No line of it may start with '#' (see WindowsDialogSamplePlayerTests).
        /// </summary>
        private const string CenterScript = @"param([string]$App, [string]$StepsBase64, [int]$TimeoutMs = 15000)
$ErrorActionPreference = 'Stop'
<# Steps arrive as base64 UTF-8, separated by ';':
   find <title> | absent <title> | open <title>|<button> | progress <title> #>
$Steps = [System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($StepsBase64))
function Say([string]$line) { [Console]::Out.WriteLine($line); [Console]::Out.Flush() }
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class NtkShell {
    [DllImport(""user32.dll"")] static extern IntPtr GetForegroundWindow();
    [DllImport(""user32.dll"")] static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport(""user32.dll"", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr window, StringBuilder name, int capacity);
    [DllImport(""user32.dll"")] static extern void keybd_event(byte key, byte scan, uint flags, IntPtr extra);
    public static void Escape() { keybd_event(0x1B, 0, 0, IntPtr.Zero); keybd_event(0x1B, 0, 2, IntPtr.Zero); }
    public static IntPtr ForegroundCoreWindowOf(uint processId) {
        IntPtr window = GetForegroundWindow();
        uint owner;
        GetWindowThreadProcessId(window, out owner);
        var name = new StringBuilder(64);
        GetClassName(window, name, name.Capacity);
        return owner == processId && name.ToString() == ""Windows.UI.Core.CoreWindow"" ? window : IntPtr.Zero;
    }
}
'@
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
$A = [System.Windows.Automation.AutomationElement]
$Scope = [System.Windows.Automation.TreeScope]
function Cond($property, $value) { New-Object System.Windows.Automation.PropertyCondition($property, $value) }
$ById = { param($root, $id) $root.FindFirst($Scope::Descendants, (Cond $A::AutomationIdProperty $id)) }

<# The notification center: ShellExperienceHost's CoreWindow holding DoNotDisturbButton. It is not
   listed by EnumWindows or among UI Automation's top-level windows; right after it opens it is the
   foreground window. #>
$shell = [uint32](Get-Process ShellExperienceHost | Select-Object -First 1).Id
function Center {
    $window = [NtkShell]::ForegroundCoreWindowOf($shell)
    if ($window -eq [IntPtr]::Zero) { return $null }
    $root = $A::FromHandle($window)
    if (& $ById $root 'DoNotDisturbButton') { return $root }
    return $null
}
function Item($center, [string]$title) {
    foreach ($group in $center.FindAll($Scope::Descendants, (Cond $A::ControlTypeProperty ([System.Windows.Automation.ControlType]::Group)))) {
        $name = $group.FindFirst($Scope::Children, (Cond $A::AutomationIdProperty 'Title'))
        if (-not $name -or $name.Current.Name -ne $App) { continue }
        foreach ($item in $group.FindAll($Scope::Descendants, (Cond $A::ControlTypeProperty ([System.Windows.Automation.ControlType]::ListItem)))) {
            $itemTitle = & $ById $item 'Title'
            if ($itemTitle -and $itemTitle.Current.Name -eq $title) { return $item }
        }
    }
    return $null
}
function WaitItem($center, [string]$title) {
    $deadline = (Get-Date).AddMilliseconds($TimeoutMs)
    do {
        $item = Item $center $title
        if ($item) { return $item }
        Start-Sleep -Milliseconds 300
    } while ((Get-Date) -lt $deadline)
    return $null
}

$exit = 0
$center = $null
try {
    Start-Process 'ms-actioncenter:'
    $deadline = (Get-Date).AddSeconds(10)
    while (-not $center -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 200; $center = Center }
    if (-not $center) { Say 'failed: the notification center did not come up'; exit 2 }
    Say 'center open'
    foreach ($step in $Steps.Split(';')) {
        $verb, $argument = $step.Trim().Split(' ', 2)
        switch ($verb) {
            'find' {
                $item = WaitItem $center $argument
                if ($item) { Say ('found [' + $argument + '] content=[' + (& $ById $item 'Content').Current.Name + ']') }
                else { Say ('failed: no [' + $argument + '] from ' + $App); $exit = 3 }
            }
            'absent' {
                if (Item $center $argument) { Say ('failed: [' + $argument + '] is there'); $exit = 3 }
                else { Say ('absent [' + $argument + ']') }
            }
            'open' {
                $title, $button = $argument.Split('|', 2)
                $item = WaitItem $center $title
                if (-not $item) { Say ('failed: no [' + $title + '] from ' + $App); $exit = 3; break }
                $expand = & $ById $item 'ExpandButton'
                if ($expand) { $expand.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); Start-Sleep -Milliseconds 800 }
                $verbButton = $null
                foreach ($v in $item.FindAll($Scope::Descendants, (Cond $A::AutomationIdProperty 'VerbButton'))) { if ($v.Current.Name -eq $button) { $verbButton = $v } }
                if (-not $verbButton) { Say ('failed: no [' + $button + '] button'); $exit = 3; break }
                $verbButton.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
                Say ('pressed [' + $button + '] on [' + $title + ']')
            }
            'progress' {
                $item = WaitItem $center $argument
                $bar = if ($item) { $item.FindFirst($Scope::Descendants, (Cond $A::ControlTypeProperty ([System.Windows.Automation.ControlType]::ProgressBar))) } else { $null }
                if (-not $bar) { Say ('failed: no progress bar on [' + $argument + ']'); $exit = 3 }
                else { Say ('progress [' + $argument + '] value=' + $bar.GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern).Current.Value) }
            }
            default { Say ('failed: unknown step ' + $step); $exit = 3 }
        }
        if ($exit -ne 0) { break }
    }
}
finally {
    <# Close it while it is still in front; Esc does nothing otherwise. Pressing a toast's button
       may already have closed it. #>
    Start-Sleep -Milliseconds 300
    if (Center) { [NtkShell]::Escape(); Start-Sleep -Milliseconds 500 }
    Say ('center closed: ' + (-not (Center)))
}
exit $exit
";
    }
}
#endif
