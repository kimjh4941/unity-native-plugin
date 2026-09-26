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
using JonghyunKim.NativeToolkit.Runtime.Dialog;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static JonghyunKim.NativeToolkit.Tests.WindowsClipboardSampleScreenDriver;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// Drives the Windows Dialog sample on a player, with the native dialogs answered from outside:
    /// D-01 to D-14 of artifact/features/dialog/designs/2026-09-26-windows-dialog-ui-test-plan-v1.md,
    /// after native-toolkit's UI tests, expected as the 1.x DLL behaves.
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

        /// <summary>What 1.x reports for a cancelled file or folder dialog.</summary>
        private const int Cancelled = -1;

        private const float CloserReadySeconds = 30f;
        private const float CloserExitSeconds = 45f;

        private Process? _closer;
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
        /// 1.x hands back the folder and then the names; the Manager joins them into full paths.
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

        /// <remarks>1.x always asks before an existing file is named; 2.0.0 can skip it.</remarks>
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

        // ── Helpers ──────────────────────────────────────────────────────────────

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
        private IEnumerator Answer(string button, string steps)
        {
            bool ready = false;
            yield return StartCloser(steps, ok => ready = ok);
            Assert.IsTrue(ready, $"the dialog closer did not start within {CloserReadySeconds}s: {CloserLog()}");

            // Blocks until the closer has answered the dialog.
            Press(FindButton(button)!);
            yield return null;

            yield return Eventually(() => _closer!.HasExited, _ => { }, CloserExitSeconds);
            if (!_closer!.HasExited) Assert.Fail($"the dialog closer was still running {CloserExitSeconds}s later: {CloserLog()}");
            _closer.WaitForExit(); // lets the asynchronous readers finish
            TestContext.WriteLine($"closer: {CloserLog()}");
            Assert.AreEqual(0, _closer.ExitCode, $"the dialog closer failed: {CloserLog()}");
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
            var info = new ProcessStartInfo(
                "powershell.exe",
                $"-NoProfile -ExecutionPolicy Bypass -File \"{script}\" -ProcessId {Process.GetCurrentProcess().Id} -StepsBase64 {encoded}")
            {
                UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true,
            };
            _closer = Process.Start(info);
            Assert.IsNotNull(_closer, "powershell.exe did not start");
            _closer!.OutputDataReceived += (_, e) => { if (e.Data != null) _closerOutput.Enqueue(e.Data); };
            _closer.ErrorDataReceived += (_, e) => { if (e.Data != null) _closerOutput.Enqueue("stderr: " + e.Data); };
            _closer.BeginOutputReadLine();
            _closer.BeginErrorReadLine();

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
<# Steps, separated by ';':  text <id> <value> | press <id> | select <item name> | confirm <id> #>
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
