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
using JonghyunKim.NativeToolkit.Runtime.Dialog;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static JonghyunKim.NativeToolkit.Tests.WindowsClipboardSampleScreenDriver;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// Drives the Windows Dialog sample on a player, with the native dialogs answered from outside.
    /// <para>
    /// Every dialog call blocks the Unity main thread until the dialog closes (WindowsDialogManager
    /// calls the native library synchronously from the button's click). A press made here therefore
    /// does not come back until something else closes the dialog, so that something is started
    /// first: a PowerShell process that finds the player's dialog (a visible top-level #32770 window
    /// of this process, found with EnumWindows as native-toolkit's UI tests do) and presses one of
    /// its controls by control ID, which does not depend on the display language. It sends what a
    /// click sends, WM_COMMAND with BN_CLICKED to the button's parent. UI Automation from Windows
    /// PowerShell was tried first and does not do: its managed client showed the buttons of a
    /// message box as panes with no Invoke pattern (2026-09-26). The press is made only once that
    /// process reports it is ready. If it cannot press the control it closes the dialog with
    /// WM_CLOSE and fails, so a run is never left waiting.
    /// </para>
    /// <para>
    /// There is no manual verification record for the Dialog sample; the checks follow
    /// native-toolkit's D-01 to D-14 (WindowsLibraryExampleUITest/Tests/Dialog/DialogTests.cs).
    /// </para>
    /// </summary>
    public sealed class WindowsDialogSamplePlayerTests
    {
        private const string TopMenuDialogButton = "DialogFeatureButton";
        private const string ShowDialogButton = "ShowDialogButton";
        private const string ResultLabel = "ResultTextBlock";

        // MessageBox button IDs, which are also the control IDs of its buttons.
        private const int IdOk = 1;
        private const int IdCancel = 2;

        private const float CloserReadySeconds = 30f;
        private const float CloserExitSeconds = 15f;

        private Process? _closer;
        private readonly ConcurrentQueue<string> _closerOutput = new();

        [UnitySetUp]
        public IEnumerator LoadTheSample()
        {
            yield return TakeForegroundAndLog(TestContext.CurrentContext.Test.Name);
            yield return LoadAtTopMenu();
        }

        [UnityTearDown]
        public IEnumerator UnloadTheSample()
        {
            if (_closer != null && !_closer.HasExited) _closer.Kill();
            _closer?.Dispose();
            _closer = null;
            yield return Unload();
        }

        // ── D-01 / D-02 ──────────────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator ShowDialog_PressOk_ReportsIdOk() => ShowDialogAndPress(IdOk);

        [UnityTest]
        public IEnumerator ShowDialog_PressCancel_ReportsIdCancel() => ShowDialogAndPress(IdCancel);

        private IEnumerator ShowDialogAndPress(int button)
        {
            yield return OpenDialogScreen();

            int? result = null;
            bool? isSuccess = null;
            int? errorCode = null;
            int reports = 0;
            void OnResult(int? r, bool ok, int? error)
            {
                reports++;
                result = r;
                isSuccess = ok;
                errorCode = error;
            }

            WindowsDialogManager.Instance.AlertDialogResult += OnResult;
            try
            {
                bool ready = false;
                yield return StartCloser(button.ToString(), ok => ready = ok);
                Assert.IsTrue(ready, $"the dialog closer did not start within {CloserReadySeconds}s: {CloserLog()}");

                // Blocks until the closer has answered the dialog.
                Press(FindButton(ShowDialogButton)!);
                yield return null;

                yield return WaitForCloser();
            }
            finally
            {
                WindowsDialogManager.Instance.AlertDialogResult -= OnResult;
            }

            TestContext.WriteLine($"closer: {CloserLog()}");
            Assert.AreEqual(0, _closer!.ExitCode, $"the dialog closer failed: {CloserLog()}");
            Assert.AreEqual(1, reports, "AlertDialogResult reports");
            Assert.AreEqual(button, result, "the pressed button");
            Assert.AreEqual(true, isSuccess, "isSuccess");
            Assert.IsNull(errorCode, "errorCode");
            StringAssert.StartsWith($"OK\nShowDialog result: {button},", ResultText(), "what the screen shows");
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

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
        private IEnumerator StartCloser(string control, Action<bool> ready)
        {
            string script = Path.Combine(Application.temporaryCachePath, "ntk-dialog-closer.ps1");
            File.WriteAllText(script, CloserScript);

            var info = new ProcessStartInfo(
                "powershell.exe",
                $"-NoProfile -ExecutionPolicy Bypass -File \"{script}\" -ProcessId {Process.GetCurrentProcess().Id} -Press {control}")
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

        private IEnumerator WaitForCloser()
        {
            yield return Eventually(() => _closer!.HasExited, _ => { }, CloserExitSeconds);
            if (!_closer!.HasExited) Assert.Fail($"the dialog closer was still running {CloserExitSeconds}s later: {CloserLog()}");
            _closer.WaitForExit(); // lets the asynchronous readers finish
        }

        private string CloserLog() => string.Join(" | ", _closerOutput.ToArray());

        /// <summary>
        /// Finds this player's dialog and presses the control whose ID is -Press. Written for Windows
        /// PowerShell 5.1, which compiles the Win32 calls with Add-Type without installing anything.
        /// Each line is flushed as written, so the test sees "ready" at once.
        /// </summary>
        private const string CloserScript = @"param([int]$ProcessId, [string]$Press, [int]$TimeoutMs = 10000)
$ErrorActionPreference = 'Stop'
function Say([string]$line) { [Console]::Out.WriteLine($line); [Console]::Out.Flush() }
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class NtkDialogWindows {
    delegate bool EnumProc(IntPtr window, IntPtr parameter);
    [DllImport(""user32.dll"")] static extern bool EnumWindows(EnumProc proc, IntPtr parameter);
    [DllImport(""user32.dll"")] static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport(""user32.dll"")] static extern bool IsWindowVisible(IntPtr window);
    [DllImport(""user32.dll"", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr window, StringBuilder name, int capacity);
    [DllImport(""user32.dll"")] public static extern bool PostMessage(IntPtr window, uint message, IntPtr w, IntPtr l);
    [DllImport(""user32.dll"")] static extern bool EnumChildWindows(IntPtr parent, EnumProc proc, IntPtr parameter);
    [DllImport(""user32.dll"")] static extern int GetDlgCtrlID(IntPtr window);
    [DllImport(""user32.dll"")] static extern IntPtr GetParent(IntPtr window);
    [DllImport(""user32.dll"")] static extern bool IsWindowEnabled(IntPtr window);
    const uint WM_COMMAND = 0x0111;
    public static IntPtr FindControl(IntPtr dialog, int id) {
        IntPtr found = IntPtr.Zero;
        EnumChildWindows(dialog, (window, parameter) => {
            if (GetDlgCtrlID(window) != id || !IsWindowVisible(window)) return true;
            found = window;
            return false;
        }, IntPtr.Zero);
        return found;
    }
    // What a click on the button sends its parent: WM_COMMAND with BN_CLICKED (0) and the button.
    public static string Press(IntPtr dialog, int id) {
        IntPtr button = FindControl(dialog, id);
        if (button == IntPtr.Zero) return ""no control with ID "" + id;
        if (!IsWindowEnabled(button)) return ""control "" + id + "" is disabled"";
        return PostMessage(GetParent(button), WM_COMMAND, new IntPtr(id), button) ? null : ""PostMessage failed"";
    }
    public static IntPtr Find(uint processId) {
        IntPtr found = IntPtr.Zero;
        EnumWindows((window, parameter) => {
            uint owner;
            GetWindowThreadProcessId(window, out owner);
            if (owner != processId || !IsWindowVisible(window)) return true;
            var name = new StringBuilder(64);
            GetClassName(window, name, name.Capacity);
            if (name.ToString() != ""#32770"") return true;
            found = window;
            return false;
        }, IntPtr.Zero);
        return found;
    }
}
'@
Say 'ready'
$deadline = (Get-Date).AddMilliseconds($TimeoutMs)
$window = [IntPtr]::Zero
while ($window -eq [IntPtr]::Zero -and (Get-Date) -lt $deadline) {
    $window = [NtkDialogWindows]::Find($ProcessId)
    if ($window -eq [IntPtr]::Zero) { Start-Sleep -Milliseconds 100 }
}
if ($window -eq [IntPtr]::Zero) { Say 'no dialog'; exit 2 }
$problem = [NtkDialogWindows]::Press($window, [int]$Press)
if ($problem) {
    Say ('failed: ' + $problem + '; closing the dialog')
    [void][NtkDialogWindows]::PostMessage($window, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero)
    exit 3
}
Say ('pressed ' + $Press)
exit 0
";
    }
}
#endif
