#nullable enable

// Windows player only: the dialog manager reaches the native library only there, and in the
// Editor the top menu answers the Dialog button with an editor dialog instead.
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using JonghyunKim.NativeToolkit.Runtime.Windows.Common;
using JonghyunKim.NativeToolkit.Runtime.Windows.Dialog;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static JonghyunKim.NativeToolkit.Tests.WindowsClipboardSampleScreenDriver;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// Drives the Windows Dialog sample on a player, with the native dialogs answered from outside:
    /// D-01 to D-14 of artifact/windows/dialog/designs/2026-09-26-windows-dialog-ui-test-plan-v2.md,
    /// after native-toolkit's UI tests. The expectations were taken from the 1.x DLL and did not
    /// change with the move to the 2.0.0 C ABI; that they still pass is what checks the move. The
    /// tests after them call the manager directly (design v6, 7.3).
    /// <para>
    /// Every dialog call blocks the Unity main thread until the dialog closes (WindowsDialogManager
    /// calls the native library synchronously from the button's click). A press made here therefore
    /// does not come back until something else closes the dialog, so that something is started
    /// first: a PowerShell process that finds the player's dialog (a visible top-level #32770 window
    /// of this process, found with EnumWindows as native-toolkit's UI tests do) and works it by
    /// control ID, which does not depend on the display language. The press is made only once that
    /// process reports it is ready. If a step fails it closes the dialog with WM_CLOSE, which every
    /// one of these dialogs takes as a cancel, and fails, so a run is never left waiting.
    /// </para>
    /// <para>
    /// How each dialog is worked (all found on 2026-09-26 against the same Win32 calls the 1.x DLL
    /// makes): buttons get what a click sends, WM_COMMAND with BN_CLICKED; file name boxes get
    /// WM_SETTEXT, looked up by ID and class, since the save dialog's address bar shares its box's
    /// ID; the overwrite prompt, a task dialog, gets TDM_CLICK_BUTTON. A folder picker navigates
    /// into a folder whose path is typed, so one folder is picked by typing it, pressing, and
    /// pressing again with the box empty. It does not take several typed folders ("f1 is not a
    /// valid folder name"), so two are picked the way a person does, by selecting them in the view:
    /// the view's items answer UI Automation themselves. UI Automation from Windows PowerShell is
    /// used for nothing else, as it shows Win32 buttons as panes that cannot be pressed.
    /// </para>
    /// </summary>
    public sealed class WindowsDialogSamplePlayerTests
    {
        private const string TopMenuDialogButton = "DialogFeatureButton";
        private const string ResultLabel = "ResultTextBlock";

        private const string ShowDialogButton = "ShowDialogButton";
        private const string ShowFileDialogButton = "ShowFileDialogButton";
        private const string ShowMultiFileDialogButton = "ShowMultiFileDialogButton";
        private const string ShowFolderDialogButton = "ShowFolderDialogButton";
        private const string ShowMultiFolderDialogButton = "ShowMultiFolderDialogButton";
        private const string ShowSaveFileDialogButton = "ShowSaveFileDialogButton";

        // Control IDs. MessageBox buttons carry their result as their ID.
        private const int IdOk = 1;
        private const int IdCancel = 2;
        private const int IdYes = 6;
        private const int OpenFileNameBox = 1148;
        private const int SaveFileNameBox = 1001;
        private const int FolderNameBox = 1152;
        /// <summary>The file dialogs' file type list (cmb1).</summary>
        private const int FileTypeBox = 0x470;

        /// <summary>The MessageBox's message text (a Static control).</summary>
        private const int MessageText = 0xFFFF;

        /// <summary>What a cancelled file or folder dialog reports.</summary>
        private const int Cancelled = WindowsDialogErrorCodes.Cancelled;

        private const float CloserReadySeconds = 30f;
        private const float CloserExitSeconds = 45f;

        private WindowsTestProcess? _closer;
        private readonly ConcurrentQueue<string> _closerOutput = new();
        private string _folder = "";
        private string _currentDirectory = "";

        [UnitySetUp]
        public IEnumerator LoadTheSample()
        {
            // The file dialogs can move the process's current directory; each test puts it back.
            _currentDirectory = Environment.CurrentDirectory;
            _folder = Path.Combine(Path.GetTempPath(), "ntk-unity-dialog-test");
            DeleteFolder();
            Directory.CreateDirectory(Path.Combine(_folder, "f1"));
            Directory.CreateDirectory(Path.Combine(_folder, "f2"));
            File.WriteAllText(Path.Combine(_folder, "a.txt"), "a");
            File.WriteAllText(Path.Combine(_folder, "b.txt"), "b");

            yield return TakeForegroundAndLog(TestContext.CurrentContext.Test.Name);
            yield return LoadAtTopMenu();
            yield return OpenDialogScreen();
        }

        [UnityTearDown]
        public IEnumerator UnloadTheSample()
        {
            if (_closer != null && !_closer.HasExited) _closer.Kill();
            _closer?.Dispose();
            _closer = null;
            while (_closerOutput.TryDequeue(out _)) { }
            Environment.CurrentDirectory = _currentDirectory;
            yield return Unload();
            DeleteFolder();
        }

        // ── D-01 / D-02: alert ───────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator ShowDialog_PressOk_ReportsIdOk() => ShowDialogAndPress(IdOk);

        [UnityTest]
        public IEnumerator ShowDialog_PressCancel_ReportsIdCancel() => ShowDialogAndPress(IdCancel);

        private IEnumerator ShowDialogAndPress(int button)
        {
            var reports = new Reports<int?>();
            WindowsDialogManager.Instance.AlertDialogResult += OnResult;
            try
            {
                yield return Answer(ShowDialogButton, $"press {button}");
            }
            finally
            {
                WindowsDialogManager.Instance.AlertDialogResult -= OnResult;
            }

            reports.AssertOne(button, false, true, null);
            StringAssert.StartsWith($"OK\nShowDialog result: {button},", ResultText(), "what the screen shows");

            // An alert has no cancelled flag; the button pressed is the result.
            void OnResult(int? result, bool isSuccess, int? errorCode) => reports.Add(result, false, isSuccess, errorCode);
        }

        // ── D-03 / D-04: open one file ───────────────────────────────────────────

        [UnityTest]
        public IEnumerator ShowFileDialog_Cancel_ReportsCancelled() =>
            PathDialog(ShowFileDialogButton, $"press {IdCancel}", null, true, Cancelled);

        [UnityTest]
        public IEnumerator ShowFileDialog_PickAFile_ReportsItsPath() =>
            PathDialog(ShowFileDialogButton, $"text {OpenFileNameBox} {In("a.txt")};press {IdOk}", In("a.txt"), false, null);

        // ── D-05 / D-06: open several files ──────────────────────────────────────

        [UnityTest]
        public IEnumerator ShowMultiFileDialog_Cancel_ReportsCancelled() =>
            ListDialog(ShowMultiFileDialogButton, $"press {IdCancel}", null, true, Cancelled);

        /// <remarks>
        /// The event carries full paths: 1.x handed back the folder and then the names, which the
        /// Manager joined; 2.0.0 hands back the full paths itself.
        /// </remarks>
        [UnityTest]
        public IEnumerator ShowMultiFileDialog_PickTwo_ReportsBothPaths() =>
            ListDialog(ShowMultiFileDialogButton,
                $"text {OpenFileNameBox} \"{In("a.txt")}\" \"{In("b.txt")}\";press {IdOk}",
                new[] { In("a.txt"), In("b.txt") }, false, null);

        // ── D-07 / D-08 / D-14: save ─────────────────────────────────────────────

        [UnityTest]
        public IEnumerator ShowSaveFileDialog_Cancel_ReportsCancelled_AndWritesNothing()
        {
            yield return PathDialog(ShowSaveFileDialogButton, $"press {IdCancel}", null, true, Cancelled);
            AssertFolderUnchanged();
        }

        /// <remarks>The dialog only names the file; nothing is written.</remarks>
        [UnityTest]
        public IEnumerator ShowSaveFileDialog_NewName_ReportsThePath_AndWritesNothing()
        {
            yield return PathDialog(ShowSaveFileDialogButton, $"text {SaveFileNameBox} {In("new.txt")};press {IdOk}",
                In("new.txt"), false, null);
            AssertFolderUnchanged();
        }

        /// <remarks>
        /// The Manager always asks before an existing file is named, as 1.x did (it leaves the C
        /// ABI's skip_overwrite_prompt at 0).
        /// </remarks>
        [UnityTest]
        public IEnumerator ShowSaveFileDialog_ExistingName_AsksAndReportsThePathOnYes()
        {
            yield return PathDialog(ShowSaveFileDialogButton,
                $"text {SaveFileNameBox} {In("a.txt")};press {IdOk};confirm {IdYes}", In("a.txt"), false, null);
            AssertFolderUnchanged();
        }

        // ── D-09 / D-10: one folder ──────────────────────────────────────────────

        [UnityTest]
        public IEnumerator ShowFolderDialog_Cancel_ReportsCancelled() =>
            PathDialog(ShowFolderDialogButton, $"press {IdCancel}", null, true, Cancelled);

        [UnityTest]
        public IEnumerator ShowFolderDialog_PickAFolder_ReportsItsPath() =>
            PathDialog(ShowFolderDialogButton, $"text {FolderNameBox} {In("f1")};press {IdOk};text {FolderNameBox} ;press {IdOk}",
                In("f1"), false, null);

        // ── D-11 / D-12: several folders ─────────────────────────────────────────

        [UnityTest]
        public IEnumerator ShowMultiFolderDialog_Cancel_ReportsCancelled() =>
            ListDialog(ShowMultiFolderDialogButton, $"press {IdCancel}", null, true, Cancelled);

        [UnityTest]
        public IEnumerator ShowMultiFolderDialog_PickTwo_ReportsBothPaths() =>
            ListDialog(ShowMultiFolderDialogButton,
                $"text {FolderNameBox} {_folder};press {IdOk};select f1;select f2;press {IdOk}",
                new[] { In("f1"), In("f2") }, false, null);

        // ── D-13: home ───────────────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator Home_ReturnsToTheTopMenu_AndTheScreenOpensAgain()
        {
            Press(FindButton("HomeButton")!);
            bool left = false;
            yield return Eventually(() => FindButton(TopMenuDialogButton) != null && FindButton(ShowDialogButton) == null, ok => left = ok);
            Assert.IsTrue(left, "the top menu did not come back");

            yield return OpenDialogScreen();
            yield return ShowDialogAndPress(IdOk);
        }

        // ── Recorded on 1.x before the move to the 2.0.0 C ABI ──────────────────
        // artifact/windows/dialog/designs/2026-09-27-windows-dialog-design-v6.md, 5.6 step 1: these
        // call the manager directly and pinned down what 1.x did, so the migration could compare.

        /// <remarks>
        /// An empty default extension with a *.txt filter. 1.x handed the empty string on as
        /// lpstrDefExt, and Windows appended the chosen filter's extension ("new.txt"). 2.0.0 hands
        /// on NULL, and the name comes back as typed. An accepted difference (design v6, 5.3); the
        /// implementation result records it for the manual.
        /// </remarks>
        [UnityTest]
        public IEnumerator ShowSaveFileDialog_EmptyDefaultExtension_ReportsTheTypedNameAsIs()
        {
            var reports = new Reports<string?>();
            void OnResult(string? p, bool isCancelled, bool isSuccess, int? error) => reports.Add(p, isCancelled, isSuccess, error);

            WindowsDialogManager manager = WindowsDialogManager.Instance;
            manager.SaveFileDialogResult += OnResult;
            try
            {
                yield return AnswerCall(() => manager.ShowSaveFileDialog(1024, "Text\0*.txt\0\0", ""),
                    $"text {SaveFileNameBox} {In("new")};press {IdOk}");
            }
            finally
            {
                manager.SaveFileDialogResult -= OnResult;
            }
            reports.AssertOne(In("new"), false, true, null);
            AssertFolderUnchanged();
        }

        /// <remarks>
        /// A filter string with a second list after the first double NUL. 1.x hands the string to
        /// the OS as is, which reads pairs up to the first empty name; the design (v6, 4.2) reads it
        /// the same way. The dialog is asked how many file types it lists, then cancelled.
        /// </remarks>
        [UnityTest]
        public IEnumerator ShowFileDialog_FilterEndsAtTheFirstEmptyName()
        {
            var reports = new Reports<string?>();
            void OnResult(string? p, bool isCancelled, bool isSuccess, int? error) => reports.Add(p, isCancelled, isSuccess, error);

            WindowsDialogManager manager = WindowsDialogManager.Instance;
            manager.FileDialogResult += OnResult;
            try
            {
                yield return AnswerCall(() => manager.ShowFileDialog(1024, "A\0*.a\0\0B\0*.b\0\0"),
                    $"expect-count {FileTypeBox} 1;press {IdCancel}");
            }
            finally
            {
                manager.FileDialogResult -= OnResult;
            }
            reports.AssertOne(null, true, true, Cancelled);
        }

        // ── Added with the 2.0.0 C ABI (design v6, 7.3) ─────────────────────────

        /// <remarks>
        /// The version check passes on the player, and ntk_last_system_code, which the dialogs read
        /// only after a failure they cannot be made to have, binds. Every other entry point is called
        /// by D-01 to D-14.
        /// </remarks>
        [UnityTest]
        public IEnumerator TheCApi_IsAvailable_AndLastSystemCodeBinds()
        {
            Assert.AreEqual(WindowsNativeToolkitCApi.NativeState.Available, WindowsNativeToolkitCApi.EnsureNativeAvailable());
            Assert.DoesNotThrow(() => WindowsNativeToolkitCApi.ntk_last_system_code());
            yield break;
        }

        [UnityTest]
        public IEnumerator ShowDialog_EmptyTitle_ShowsNothing_AndReportsInvalidArgument()
        {
            var reports = new Reports<int?>();
            void OnResult(int? result, bool isSuccess, int? errorCode) => reports.Add(result, false, isSuccess, errorCode);

            WindowsDialogManager manager = WindowsDialogManager.Instance;
            manager.AlertDialogResult += OnResult;
            try
            {
                yield return AnswerNoDialog(() => manager.ShowDialog("", "message"));
            }
            finally
            {
                manager.AlertDialogResult -= OnResult;
            }
            reports.AssertOne(null, false, false, WindowsDialogErrorCodes.InvalidArgument);
        }

        [UnityTest]
        public IEnumerator ShowDialog_SystemModal_ShowsNothing_AndReportsInvalidArgument()
        {
            var reports = new Reports<int?>();
            void OnResult(int? result, bool isSuccess, int? errorCode) => reports.Add(result, false, isSuccess, errorCode);

            WindowsDialogManager manager = WindowsDialogManager.Instance;
            manager.AlertDialogResult += OnResult;
            try
            {
                yield return AnswerNoDialog(() => manager.ShowDialog("title", "message", options: Win32MessageBox.MB_SYSTEMMODAL));
            }
            finally
            {
                manager.AlertDialogResult -= OnResult;
            }
            reports.AssertOne(null, false, false, WindowsDialogErrorCodes.InvalidArgument);
        }

        [UnityTest]
        public IEnumerator ShowFileDialog_AFilterWithoutAPattern_ShowsNothing_AndReportsInvalidArgument()
        {
            var reports = new Reports<string?>();
            void OnResult(string? p, bool isCancelled, bool isSuccess, int? error) => reports.Add(p, isCancelled, isSuccess, error);

            WindowsDialogManager manager = WindowsDialogManager.Instance;
            manager.FileDialogResult += OnResult;
            try
            {
                yield return AnswerNoDialog(() => manager.ShowFileDialog(1024, "Text\0\0"));
            }
            finally
            {
                manager.FileDialogResult -= OnResult;
            }
            reports.AssertOne(null, false, false, WindowsDialogErrorCodes.InvalidArgument);
        }

        /// <remarks>
        /// The title and the message reach the MessageBox as UTF-8, surrogate pairs included. OK and
        /// Cancel, because a MessageBox with OK alone gives its button the ID IDCANCEL.
        /// </remarks>
        [UnityTest]
        public IEnumerator ShowDialog_NonAsciiTitleAndMessage_AreShown()
        {
            var reports = new Reports<int?>();
            void OnResult(int? result, bool isSuccess, int? errorCode) => reports.Add(result, false, isSuccess, errorCode);

            WindowsDialogManager manager = WindowsDialogManager.Instance;
            manager.AlertDialogResult += OnResult;
            try
            {
                yield return AnswerCall(() => manager.ShowDialog(NonAsciiTitle, NonAsciiMessage, Win32MessageBox.MB_OKCANCEL),
                    $"expect-title {NonAsciiTitle};expect-text {MessageText} {NonAsciiMessage};press {IdOk}");
            }
            finally
            {
                manager.AlertDialogResult -= OnResult;
            }
            reports.AssertOne(IdOk, false, true, null);
        }

        [UnityTest]
        public IEnumerator ShowFolderDialog_NonAsciiTitle_IsShown()
        {
            var reports = new Reports<string?>();
            void OnResult(string? p, bool isCancelled, bool isSuccess, int? error) => reports.Add(p, isCancelled, isSuccess, error);

            WindowsDialogManager manager = WindowsDialogManager.Instance;
            manager.FolderDialogResult += OnResult;
            try
            {
                yield return AnswerCall(() => manager.ShowFolderDialog(1024, NonAsciiTitle),
                    $"expect-title {NonAsciiTitle};press {IdCancel}");
            }
            finally
            {
                manager.FolderDialogResult -= OnResult;
            }
            reports.AssertOne(null, true, true, Cancelled);
        }

        /// <remarks>
        /// Paths with Japanese and a surrogate pair make the round trip both ways (D-04, D-10 and
        /// D-12 with such names). They are made here, not in the set-up, where D-07, D-08 and D-14
        /// would see them in the folder.
        /// </remarks>
        [UnityTest]
        public IEnumerator ShowFileDialog_NonAsciiPath_RoundTrips()
        {
            string file = In(NonAsciiName + ".txt");
            File.WriteAllText(file, "x");

            var reports = new Reports<string?>();
            void OnResult(string? p, bool isCancelled, bool isSuccess, int? error) => reports.Add(p, isCancelled, isSuccess, error);

            WindowsDialogManager manager = WindowsDialogManager.Instance;
            manager.FileDialogResult += OnResult;
            try
            {
                yield return AnswerCall(() => manager.ShowFileDialog(), $"text {OpenFileNameBox} {file};press {IdOk}");
            }
            finally
            {
                manager.FileDialogResult -= OnResult;
            }
            reports.AssertOne(file, false, true, null);
            Assert.IsTrue(File.Exists(file));
        }

        [UnityTest]
        public IEnumerator ShowFolderDialog_NonAsciiPath_RoundTrips()
        {
            string folder = In(NonAsciiName);
            Directory.CreateDirectory(folder);

            var reports = new Reports<string?>();
            void OnResult(string? p, bool isCancelled, bool isSuccess, int? error) => reports.Add(p, isCancelled, isSuccess, error);

            WindowsDialogManager manager = WindowsDialogManager.Instance;
            manager.FolderDialogResult += OnResult;
            try
            {
                yield return AnswerCall(() => manager.ShowFolderDialog(),
                    $"text {FolderNameBox} {folder};press {IdOk};text {FolderNameBox} ;press {IdOk}");
            }
            finally
            {
                manager.FolderDialogResult -= OnResult;
            }
            reports.AssertOne(folder, false, true, null);
            Assert.IsTrue(Directory.Exists(folder));
        }

        [UnityTest]
        public IEnumerator ShowMultiFolderDialog_NonAsciiPaths_RoundTrip()
        {
            string first = In(NonAsciiName);
            string second = In(NonAsciiName + "-2");
            Directory.CreateDirectory(first);
            Directory.CreateDirectory(second);

            var reports = new Reports<string[]?>();
            void OnResult(ArrayList? list, bool isCancelled, bool isSuccess, int? error) =>
                reports.Add(list?.Cast<string>().ToArray(), isCancelled, isSuccess, error);

            WindowsDialogManager manager = WindowsDialogManager.Instance;
            manager.MultiFolderDialogResult += OnResult;
            try
            {
                yield return AnswerCall(() => manager.ShowMultiFolderDialog(),
                    $"text {FolderNameBox} {_folder};press {IdOk};select {NonAsciiName};select {NonAsciiName}-2;press {IdOk}");
            }
            finally
            {
                manager.MultiFolderDialogResult -= OnResult;
            }
            reports.AssertOne(new[] { first, second }, false, true, null);
            Assert.IsTrue(Directory.Exists(first) && Directory.Exists(second));
        }

        /// <remarks>The default extension, "txt", is added to a name typed without one.</remarks>
        [UnityTest]
        public IEnumerator ShowSaveFileDialog_DefaultExtension_IsAddedToTheTypedName()
        {
            var reports = new Reports<string?>();
            void OnResult(string? p, bool isCancelled, bool isSuccess, int? error) => reports.Add(p, isCancelled, isSuccess, error);

            WindowsDialogManager manager = WindowsDialogManager.Instance;
            manager.SaveFileDialogResult += OnResult;
            try
            {
                yield return AnswerCall(() => manager.ShowSaveFileDialog(), $"text {SaveFileNameBox} {In("new")};press {IdOk}");
            }
            finally
            {
                manager.SaveFileDialogResult -= OnResult;
            }
            reports.AssertOne(In("new.txt"), false, true, null);
            AssertFolderUnchanged();
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private const string NonAsciiName = "ダイアログ-𠮷";
        private const string NonAsciiTitle = "タイトル 𠮷";
        private const string NonAsciiMessage = "本文です 𠮷";

        private string In(string name) => Path.Combine(_folder, name);

        private void AssertFolderUnchanged()
        {
            string[] names = Directory.GetFileSystemEntries(_folder).Select(Path.GetFileName).OrderBy(n => n).ToArray()!;
            CollectionAssert.AreEqual(new[] { "a.txt", "b.txt", "f1", "f2" }, names, "what the folder holds");
            Assert.AreEqual("a", File.ReadAllText(In("a.txt")), "a.txt was rewritten");
        }

        /// <summary>A dialog reporting one path: open file, save file, folder.</summary>
        private IEnumerator PathDialog(string button, string steps, string? path, bool cancelled, int? errorCode)
        {
            var reports = new Reports<string?>();
            void OnResult(string? p, bool isCancelled, bool isSuccess, int? error) => reports.Add(p, isCancelled, isSuccess, error);

            WindowsDialogManager manager = WindowsDialogManager.Instance;
            Action subscribe, unsubscribe;
            switch (button)
            {
                case ShowFileDialogButton: subscribe = () => manager.FileDialogResult += OnResult; unsubscribe = () => manager.FileDialogResult -= OnResult; break;
                case ShowSaveFileDialogButton: subscribe = () => manager.SaveFileDialogResult += OnResult; unsubscribe = () => manager.SaveFileDialogResult -= OnResult; break;
                case ShowFolderDialogButton: subscribe = () => manager.FolderDialogResult += OnResult; unsubscribe = () => manager.FolderDialogResult -= OnResult; break;
                default: throw new ArgumentException(button);
            }

            subscribe();
            try
            {
                yield return Answer(button, steps);
            }
            finally
            {
                unsubscribe();
            }
            reports.AssertOne(path, cancelled, true, errorCode);
        }

        /// <summary>A dialog reporting a list: several files, several folders.</summary>
        private IEnumerator ListDialog(string button, string steps, string[]? paths, bool cancelled, int? errorCode)
        {
            var reports = new Reports<string[]?>();
            void OnResult(ArrayList? list, bool isCancelled, bool isSuccess, int? error) =>
                reports.Add(list?.Cast<string>().ToArray(), isCancelled, isSuccess, error);

            WindowsDialogManager manager = WindowsDialogManager.Instance;
            bool files = button == ShowMultiFileDialogButton;
            if (files) manager.MultiFileDialogResult += OnResult; else manager.MultiFolderDialogResult += OnResult;
            try
            {
                yield return Answer(button, steps);
            }
            finally
            {
                if (files) manager.MultiFileDialogResult -= OnResult; else manager.MultiFolderDialogResult -= OnResult;
            }
            reports.AssertOne(paths, cancelled, true, errorCode);
        }

        /// <summary>Starts the closer with the steps, presses the button, and waits for the closer to finish.</summary>
        private IEnumerator Answer(string button, string steps) => AnswerCall(() => Press(FindButton(button)!), steps);

        /// <summary>
        /// Starts the closer with the steps, makes the call that opens the dialog, and waits for the
        /// closer to finish.
        /// </summary>
        private IEnumerator AnswerCall(Action openDialog, string steps)
        {
            bool ready = false;
            yield return StartCloser(steps, ok => ready = ok);
            Assert.IsTrue(ready, $"the dialog closer did not start within {CloserReadySeconds}s: {CloserLog()}");

            // Blocks until the closer has answered the dialog.
            openDialog();
            yield return null;

            yield return Eventually(() => _closer!.HasExited, _ => { }, CloserExitSeconds);
            if (!_closer!.HasExited) Assert.Fail($"the dialog closer was still running {CloserExitSeconds}s later: {CloserLog()}");
            _closer.WaitForExit(); // lets the readers finish
            TestContext.WriteLine($"closer: {CloserLog()}");
            Assert.AreEqual(0, _closer.ExitCode, $"the dialog closer failed: {CloserLog()}");
        }

        /// <summary>
        /// Starts the closer, makes a call that must not show a dialog, and checks that the closer
        /// found none. Should one appear, the closer's press of Cancel (or its WM_CLOSE) closes it.
        /// </summary>
        private IEnumerator AnswerNoDialog(Action call)
        {
            bool ready = false;
            yield return StartCloser($"press {IdCancel}", ok => ready = ok);
            Assert.IsTrue(ready, $"the dialog closer did not start within {CloserReadySeconds}s: {CloserLog()}");

            call();
            yield return null;

            yield return Eventually(() => _closer!.HasExited, _ => { }, CloserExitSeconds);
            if (!_closer!.HasExited) Assert.Fail($"the dialog closer was still running {CloserExitSeconds}s later: {CloserLog()}");
            _closer.WaitForExit();
            TestContext.WriteLine($"closer: {CloserLog()}");
            Assert.AreEqual(2, _closer.ExitCode, $"a dialog was shown: {CloserLog()}");
        }

        private static IEnumerator OpenDialogScreen()
        {
            Press(FindButton(TopMenuDialogButton)!);
            bool opened = false;
            yield return Eventually(() => FindButton(ShowDialogButton) != null, ok => opened = ok);
            Assert.IsTrue(opened, "the Windows Dialog screen did not open");
        }

        private static string? ResultText() =>
            SampleDocument()?.rootVisualElement?.Q<Label>(ResultLabel)?.text;

        /// <summary>Starts the closer and waits until it says it is ready to find the dialog.</summary>
        private IEnumerator StartCloser(string steps, Action<bool> ready)
        {
            _closer?.Dispose();
            while (_closerOutput.TryDequeue(out _)) { }

            string script = Path.Combine(Application.temporaryCachePath, "ntk-dialog-closer.ps1");
            File.WriteAllText(script, CloserScript);

            // Base64: Windows PowerShell drops the quotes inside a native argument, and a multiple
            // selection is written as quoted names.
            string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(steps));
            _closer = WindowsTestProcess.Start(
                "powershell.exe",
                $"-NoProfile -ExecutionPolicy Bypass -File \"{script}\" -ProcessId {Process.GetCurrentProcess().Id} -StepsBase64 {encoded}",
                line => _closerOutput.Enqueue(line),
                line => _closerOutput.Enqueue("stderr: " + line));

            yield return Eventually(() => _closerOutput.Contains("ready") || _closer.HasExited, _ => { }, CloserReadySeconds);
            ready(_closerOutput.Contains("ready") && !_closer.HasExited);
        }

        private string CloserLog() => string.Join(" | ", _closerOutput.ToArray());

        private void DeleteFolder()
        {
            // A file dialog may still hold the folder as the current directory for a moment.
            for (int attempt = 0; attempt < 10 && Directory.Exists(_folder); attempt++)
            {
                try { Directory.Delete(_folder, true); }
                catch (IOException) { System.Threading.Thread.Sleep(200); }
                catch (UnauthorizedAccessException) { System.Threading.Thread.Sleep(200); }
            }
        }

        /// <summary>What a dialog event reported, and how many times.</summary>
        private sealed class Reports<T>
        {
            private int _count;
            private T? _value;
            private bool _cancelled;
            private bool _success;
            private int? _error;

            public void Add(T? value, bool cancelled, bool success, int? error)
            {
                _count++;
                _value = value;
                _cancelled = cancelled;
                _success = success;
                _error = error;
            }

            public void AssertOne(T? value, bool cancelled, bool success, int? error)
            {
                Assert.AreEqual(1, _count, "reports");
                if (value is not string && value is IEnumerable expected && _value is IEnumerable actual)
                    CollectionAssert.AreEqual(expected, actual, "value");
                else
                    Assert.AreEqual(value, _value, "value");
                Assert.AreEqual(cancelled, _cancelled, "isCancelled");
                Assert.AreEqual(success, _success, "isSuccess");
                Assert.AreEqual(error, _error, "errorCode");
            }
        }

        /// <summary>
        /// Finds this player's dialog and works it by the steps. Written for Windows PowerShell 5.1,
        /// which compiles the Win32 calls with Add-Type and has UI Automation, without installing
        /// anything. Each line is flushed as written, so the test sees "ready" at once.
        /// <para>
        /// No line of it may start with '#'. In the Editor this whole file sits in a skipped #if
        /// region, where the compiler reads such a line as a preprocessor directive even inside
        /// this string (CS1024); comments are written &lt;# ... #&gt; instead.
        /// </para>
        /// </summary>
        private const string CloserScript = @"param([int]$ProcessId, [string]$StepsBase64, [int]$TimeoutMs = 10000)
<# Steps arrive as base64 UTF-8: Windows PowerShell drops the quotes inside a native argument, and #>
<# a multiple selection is written as quoted names. #>
$Steps = [System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($StepsBase64))
$ErrorActionPreference = 'Stop'
<# UTF-8, which the tests read it as; titles may be non-ASCII. #>
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
function Say([string]$line) { [Console]::Out.WriteLine($line); [Console]::Out.Flush() }
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class NtkDialogWindows {
    delegate bool EnumProc(IntPtr window, IntPtr parameter);
    [DllImport(""user32.dll"")] static extern bool EnumWindows(EnumProc proc, IntPtr parameter);
    [DllImport(""user32.dll"")] static extern bool EnumChildWindows(IntPtr parent, EnumProc proc, IntPtr parameter);
    [DllImport(""user32.dll"")] static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport(""user32.dll"")] static extern bool IsWindowVisible(IntPtr window);
    [DllImport(""user32.dll"")] static extern bool IsWindowEnabled(IntPtr window);
    [DllImport(""user32.dll"", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr window, StringBuilder name, int capacity);
    [DllImport(""user32.dll"")] static extern int GetDlgCtrlID(IntPtr window);
    [DllImport(""user32.dll"")] static extern IntPtr GetParent(IntPtr window);
    [DllImport(""user32.dll"")] public static extern bool PostMessage(IntPtr window, uint message, IntPtr w, IntPtr l);
    [DllImport(""user32.dll"", CharSet = CharSet.Unicode)] static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr w, string l);
    [DllImport(""user32.dll"")] static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr w, IntPtr l);
    const uint WM_SETTEXT = 0x000C, WM_COMMAND = 0x0111, TDM_CLICK_BUTTON = 0x0400 + 102;
    static string ClassOf(IntPtr window) { var name = new StringBuilder(64); GetClassName(window, name, name.Capacity); return name.ToString(); }
    // A visible top-level dialog of the process, other than <except>.
    public static IntPtr Find(uint processId, IntPtr except) {
        IntPtr found = IntPtr.Zero;
        EnumWindows((window, parameter) => {
            uint owner;
            GetWindowThreadProcessId(window, out owner);
            if (owner != processId || window == except || !IsWindowVisible(window) || ClassOf(window) != ""#32770"") return true;
            found = window;
            return false;
        }, IntPtr.Zero);
        return found;
    }
    // A visible control with the ID, of the class when one is given (the save dialog's address bar
    // shares its file name box's ID).
    static IntPtr FindControl(IntPtr dialog, int id, string className) {
        IntPtr found = IntPtr.Zero;
        EnumChildWindows(dialog, (window, parameter) => {
            if (GetDlgCtrlID(window) != id || !IsWindowVisible(window)) return true;
            if (className != null && ClassOf(window) != className) return true;
            found = window;
            return false;
        }, IntPtr.Zero);
        return found;
    }
    // What a click on the button sends its parent: WM_COMMAND with BN_CLICKED (0) and the button.
    public static string Press(IntPtr dialog, int id) {
        IntPtr button = FindControl(dialog, id, ""Button"");
        if (button == IntPtr.Zero) return ""no button with ID "" + id;
        if (!IsWindowEnabled(button)) return ""button "" + id + "" is disabled"";
        return PostMessage(GetParent(button), WM_COMMAND, new IntPtr(id), button) ? null : ""PostMessage failed"";
    }
    public static string SetText(IntPtr dialog, int id, string text) {
        IntPtr edit = FindControl(dialog, id, ""Edit"");
        if (edit == IntPtr.Zero) return ""no edit box with ID "" + id;
        SendMessage(edit, WM_SETTEXT, IntPtr.Zero, text);
        return null;
    }
    // A task dialog (the save dialog's overwrite prompt) takes a button press as TDM_CLICK_BUTTON.
    public static void ClickTaskButton(IntPtr dialog, int id) { SendMessage(dialog, TDM_CLICK_BUTTON, new IntPtr(id), IntPtr.Zero); }
    const uint CB_GETCOUNT = 0x0146;
    // How many items a combo box lists (the file dialogs' type list); -1 when there is none with the ID.
    public static int Count(IntPtr dialog, int id) {
        IntPtr combo = FindControl(dialog, id, ""ComboBox"");
        if (combo == IntPtr.Zero) return -1;
        return SendMessage(combo, CB_GETCOUNT, IntPtr.Zero, IntPtr.Zero).ToInt32();
    }
    [DllImport(""user32.dll"", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr window, StringBuilder text, int capacity);
    [DllImport(""user32.dll"")] static extern int GetWindowTextLength(IntPtr window);
    [DllImport(""user32.dll"", CharSet = CharSet.Unicode)] static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr w, StringBuilder l);
    const uint WM_GETTEXT = 0x000D, WM_GETTEXTLENGTH = 0x000E;
    // The dialog's own title.
    public static string Title(IntPtr dialog) {
        var text = new StringBuilder(GetWindowTextLength(dialog) + 1);
        GetWindowText(dialog, text, text.Capacity);
        return text.ToString();
    }
    // A control's text, asked with WM_GETTEXT: GetWindowText does not read another process's controls.
    // Null when there is no visible control with the ID.
    public static string Text(IntPtr dialog, int id) {
        IntPtr control = FindControl(dialog, id, null);
        if (control == IntPtr.Zero) return null;
        var text = new StringBuilder(SendMessage(control, WM_GETTEXTLENGTH, IntPtr.Zero, IntPtr.Zero).ToInt32() + 1);
        SendMessage(control, WM_GETTEXT, new IntPtr(text.Capacity), text);
        return text.ToString();
    }
}
'@
<# Selecting items in a file dialog's view: its items are DirectUI elements, which answer UI #>
<# Automation themselves, so the managed client can select them (it cannot press Win32 buttons). #>
function SelectItem([IntPtr]$dialog, [string]$name) {
    Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
    $A = [System.Windows.Automation.AutomationElement]
    $condition = New-Object System.Windows.Automation.AndCondition(
        (New-Object System.Windows.Automation.PropertyCondition($A::ControlTypeProperty, [System.Windows.Automation.ControlType]::ListItem)),
        (New-Object System.Windows.Automation.PropertyCondition($A::NameProperty, $name)))
    $deadline = (Get-Date).AddMilliseconds($TimeoutMs)
    do {
        $item = $A::FromHandle($dialog).FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
        if ($item) { break }
        Start-Sleep -Milliseconds 200
    } while ((Get-Date) -lt $deadline)
    if (-not $item) { return ('no item named ' + $name) }
    $item.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).AddToSelection()
    return $null
}
function WaitFor([IntPtr]$except) {
    $deadline = (Get-Date).AddMilliseconds($TimeoutMs)
    do {
        $found = [NtkDialogWindows]::Find($ProcessId, $except)
        if ($found -ne [IntPtr]::Zero) { return $found }
        Start-Sleep -Milliseconds 100
    } while ((Get-Date) -lt $deadline)
    return [IntPtr]::Zero
}
Say 'ready'
$dialog = WaitFor ([IntPtr]::Zero)
if ($dialog -eq [IntPtr]::Zero) { Say 'no dialog'; exit 2 }
<# Steps, separated by ';':  text <id> <value> | press <id> | select <item name> | confirm <id> | expect-count <id> <n> #>
<#   | expect-title <text> | expect-text <id> <text>. An expected text is the rest of the step, spaces included. #>
foreach ($step in $Steps.Split(';')) {
    $parts = $step.Trim().Split(' ', 3)
    $problem = $null
    switch ($parts[0]) {
        'text'    { $problem = [NtkDialogWindows]::SetText($dialog, [int]$parts[1], $(if ($parts.Length -gt 2) { $parts[2] } else { '' })) }
        'press'   { $problem = [NtkDialogWindows]::Press($dialog, [int]$parts[1]) }
        'select'  { $problem = SelectItem $dialog $step.Trim().Substring(7) }
        'confirm' {
            $prompt = WaitFor $dialog
            if ($prompt -eq [IntPtr]::Zero) { $problem = 'no confirmation dialog' }
            else { [NtkDialogWindows]::ClickTaskButton($prompt, [int]$parts[1]) }
        }
        'expect-count' {
            <# The list may still be filling when the dialog first shows; wait for it to settle. #>
            $expected = [int]$parts[2]
            $deadline = (Get-Date).AddMilliseconds($TimeoutMs)
            do {
                $count = [NtkDialogWindows]::Count($dialog, [int]$parts[1])
                if ($count -eq $expected) { break }
                Start-Sleep -Milliseconds 200
            } while ((Get-Date) -lt $deadline)
            Say ('count ' + $parts[1] + ' = ' + $count)
            if ($count -ne $expected) { $problem = 'combo ' + $parts[1] + ' lists ' + $count + ' items, expected ' + $expected }
        }
        'expect-title' {
            $expected = $step.Trim().Substring(13)
            $actual = [NtkDialogWindows]::Title($dialog)
            if ($actual -cne $expected) { $problem = 'the title is [' + $actual + ']' }
        }
        'expect-text' {
            $rest = $step.Trim().Substring(12)
            $space = $rest.IndexOf(' ')
            $expected = $rest.Substring($space + 1)
            $actual = [NtkDialogWindows]::Text($dialog, [int]$rest.Substring(0, $space))
            if ($actual -eq $null) { $problem = 'no control with ID ' + $rest.Substring(0, $space) }
            elseif ($actual -cne $expected) { $problem = 'the text is [' + $actual + ']' }
        }
        default   { $problem = 'unknown step ' + $step }
    }
    if ($problem) {
        Say ('failed at [' + $step + ']: ' + $problem + '; closing the dialog')
        [void][NtkDialogWindows]::PostMessage($dialog, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero)
        exit 3
    }
    Say ('did ' + $step)
    <# A press can navigate into a folder rather than close the dialog; give it time to. #>
    Start-Sleep -Milliseconds 700
}
exit 0

";
    }
}
#endif
