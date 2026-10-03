#nullable enable

// Editor only. These tests rest on the native library never being called in the Editor: every
// call that gets past the argument checks reports PlatformUnavailable without showing a dialog.
// The PlayMode assembly is also built into the player, where the same calls would show real
// dialogs and block; the player's tests are in WindowsDialogSamplePlayerTests.
#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using JonghyunKim.NativeToolkit.Runtime.Windows.Dialog;
using NUnit.Framework;
using UnityEngine;
using static JonghyunKim.NativeToolkit.Runtime.Windows.Dialog.Win32MessageBox;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// PlayMode tests for <c>WindowsDialogManager</c> in the Editor
    /// (artifact/windows/dialog/designs/2026-09-27-windows-dialog-design-v6.md, 7.2): the paths
    /// that end before the native library, and the one-event contract.
    /// </summary>
    public sealed class WindowsDialogManagerEditorTests
    {
        private const string BadFilter = "Text\0\0";

        private WindowsDialogManager _manager = null!;

        [SetUp]
        public void SetUp()
        {
            _manager = WindowsDialogManager.Instance;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (WindowsDialogManager manager in
                     UnityEngine.Object.FindObjectsByType<WindowsDialogManager>(FindObjectsInactive.Include))
            {
                UnityEngine.Object.DestroyImmediate(manager.gameObject);
            }
        }

        // ── Alert ───────────────────────────────────────────────────────────────

        private List<(int? result, bool isSuccess, int? errorCode)> RecordAlerts()
        {
            var events = new List<(int?, bool, int?)>();
            _manager.AlertDialogResult += (result, isSuccess, errorCode) => events.Add((result, isSuccess, errorCode));
            return events;
        }

        [TestCase("", "message")]
        [TestCase("title", "")]
        [TestCase("", "")]
        [TestCase(null, "message")]
        [TestCase("title", null)]
        [TestCase(null, null)]
        public void ShowDialog_EmptyTitleOrMessage_IsRefusedOnce(string? title, string? message)
        {
            var events = RecordAlerts();
            _manager.ShowDialog(title!, message!);
            CollectionAssert.AreEqual(new[] { ((int?)null, false, (int?)WindowsDialogErrorCodes.InvalidArgument) }, events);
        }

        [TestCase(MB_OK, MB_ICONINFORMATION, MB_DEFBUTTON1, MB_SYSTEMMODAL)]
        [TestCase(MB_OK, MB_ICONINFORMATION, MB_DEFBUTTON1, MB_TASKMODAL)]
        [TestCase(MB_OK | MB_RIGHT, MB_ICONINFORMATION, MB_DEFBUTTON1, MB_APPLMODAL)]
        [TestCase(MB_OK, MB_ICONINFORMATION | MB_RTLREADING, MB_DEFBUTTON1, MB_APPLMODAL)]
        [TestCase(0x7u, MB_ICONINFORMATION, MB_DEFBUTTON1, MB_APPLMODAL)]
        public void ShowDialog_AFlagTheCApiCannotExpress_IsRefused(uint buttons, uint icon, uint defbutton, uint options)
        {
            var events = RecordAlerts();
            _manager.ShowDialog("title", "message", buttons, icon, defbutton, options);
            CollectionAssert.AreEqual(new[] { ((int?)null, false, (int?)WindowsDialogErrorCodes.InvalidArgument) }, events);
        }

        [Test]
        public void ShowDialog_InTheEditor_IsPlatformUnavailable_WithNoResult()
        {
            var events = RecordAlerts();
            _manager.ShowDialog("title", "message", MB_YESNO, MB_ICONWARNING, MB_DEFBUTTON2, MB_TOPMOST | 0x4000u);
            CollectionAssert.AreEqual(new[] { ((int?)null, false, (int?)WindowsDialogErrorCodes.PlatformUnavailable) }, events);
        }

        [Test]
        public void ShowDialog_NullFlags_TakeTheDefaults()
        {
            // The defaults are accepted, so the call gets as far as the native library.
            var events = RecordAlerts();
            _manager.ShowDialog("title", "message", null, null, null, null);
            CollectionAssert.AreEqual(new[] { ((int?)null, false, (int?)WindowsDialogErrorCodes.PlatformUnavailable) }, events);
        }

        // ── Files and folders ───────────────────────────────────────────────────

        [Test]
        public void ShowFileDialog_AFilterWithoutAPattern_IsRefused()
        {
            var events = new List<(string?, bool, bool, int?)>();
            _manager.FileDialogResult += (path, isCancelled, isSuccess, errorCode) => events.Add((path, isCancelled, isSuccess, errorCode));
            _manager.ShowFileDialog(1024, BadFilter);
            CollectionAssert.AreEqual(new[] { ((string?)null, false, false, (int?)WindowsDialogErrorCodes.InvalidArgument) }, events);
        }

        [Test]
        public void ShowMultiFileDialog_AFilterOfSemicolonsOnly_IsRefused()
        {
            var events = new List<(ArrayList?, bool, bool, int?)>();
            _manager.MultiFileDialogResult += (paths, isCancelled, isSuccess, errorCode) => events.Add((paths, isCancelled, isSuccess, errorCode));
            _manager.ShowMultiFileDialog(4096, "Text\0;\0\0");
            CollectionAssert.AreEqual(new[] { ((ArrayList?)null, false, false, (int?)WindowsDialogErrorCodes.InvalidArgument) }, events);
        }

        [Test]
        public void ShowSaveFileDialog_AFilterWhosePatternIsMissing_IsRefused()
        {
            var events = new List<(string?, bool, bool, int?)>();
            _manager.SaveFileDialogResult += (path, isCancelled, isSuccess, errorCode) => events.Add((path, isCancelled, isSuccess, errorCode));
            _manager.ShowSaveFileDialog(1024, "Text", "txt");
            CollectionAssert.AreEqual(new[] { ((string?)null, false, false, (int?)WindowsDialogErrorCodes.InvalidArgument) }, events);
        }

        private static readonly (string? path, bool isCancelled, bool isSuccess, int? errorCode) PathUnavailable =
            (null, false, false, WindowsDialogErrorCodes.PlatformUnavailable);

        private static readonly (ArrayList? paths, bool isCancelled, bool isSuccess, int? errorCode) ListUnavailable =
            (null, false, false, WindowsDialogErrorCodes.PlatformUnavailable);

        [Test]
        public void ShowFileDialog_InTheEditor_IsPlatformUnavailable()
        {
            var events = new List<(string?, bool, bool, int?)>();
            _manager.FileDialogResult += (path, isCancelled, isSuccess, errorCode) => events.Add((path, isCancelled, isSuccess, errorCode));
            _manager.ShowFileDialog();
            _manager.ShowFileDialog(null, null);
            _manager.ShowFileDialog(1024, "");
            CollectionAssert.AreEqual(new[] { PathUnavailable, PathUnavailable, PathUnavailable }, events);
        }

        [Test]
        public void ShowMultiFileDialog_InTheEditor_IsPlatformUnavailable()
        {
            var events = new List<(ArrayList?, bool, bool, int?)>();
            _manager.MultiFileDialogResult += (paths, isCancelled, isSuccess, errorCode) => events.Add((paths, isCancelled, isSuccess, errorCode));
            _manager.ShowMultiFileDialog();
            _manager.ShowMultiFileDialog(null, null);
            CollectionAssert.AreEqual(new[] { ListUnavailable, ListUnavailable }, events);
        }

        [Test]
        public void ShowSaveFileDialog_InTheEditor_IsPlatformUnavailable()
        {
            var events = new List<(string?, bool, bool, int?)>();
            _manager.SaveFileDialogResult += (path, isCancelled, isSuccess, errorCode) => events.Add((path, isCancelled, isSuccess, errorCode));
            _manager.ShowSaveFileDialog();
            _manager.ShowSaveFileDialog(null, null, null);
            _manager.ShowSaveFileDialog(1024, "Text\0*.txt\0\0", "");
            CollectionAssert.AreEqual(new[] { PathUnavailable, PathUnavailable, PathUnavailable }, events);
        }

        [Test]
        public void ShowFolderDialog_InTheEditor_IsPlatformUnavailable()
        {
            var events = new List<(string?, bool, bool, int?)>();
            _manager.FolderDialogResult += (path, isCancelled, isSuccess, errorCode) => events.Add((path, isCancelled, isSuccess, errorCode));
            _manager.ShowFolderDialog();
            _manager.ShowFolderDialog(null, null);
            _manager.ShowFolderDialog(1024, "");
            CollectionAssert.AreEqual(new[] { PathUnavailable, PathUnavailable, PathUnavailable }, events);
        }

        [Test]
        public void ShowMultiFolderDialog_InTheEditor_IsPlatformUnavailable()
        {
            var events = new List<(ArrayList?, bool, bool, int?)>();
            _manager.MultiFolderDialogResult += (paths, isCancelled, isSuccess, errorCode) => events.Add((paths, isCancelled, isSuccess, errorCode));
            _manager.ShowMultiFolderDialog();
            _manager.ShowMultiFolderDialog(null, null);
            CollectionAssert.AreEqual(new[] { ListUnavailable, ListUnavailable }, events);
        }

        // ── One event per call; a subscriber's exception reaches the caller ─────

        private sealed class SubscriberException : Exception
        {
        }

        [Test]
        public void ASubscribersException_ReachesTheCaller_AfterOneEvent()
        {
            int alerts = 0;
            _manager.AlertDialogResult += (_, __, ___) => { alerts++; throw new SubscriberException(); };
            Assert.Throws<SubscriberException>(() => _manager.ShowDialog("title", "message"));
            Assert.AreEqual(1, alerts);

            int files = 0;
            _manager.FileDialogResult += (_, __, ___, ____) => { files++; throw new SubscriberException(); };
            Assert.Throws<SubscriberException>(() => _manager.ShowFileDialog());
            Assert.AreEqual(1, files);

            int folders = 0;
            _manager.MultiFolderDialogResult += (_, __, ___, ____) => { folders++; throw new SubscriberException(); };
            Assert.Throws<SubscriberException>(() => _manager.ShowMultiFolderDialog());
            Assert.AreEqual(1, folders);
        }

        [Test]
        public void ASubscribersException_OnARefusal_ReachesTheCaller_AfterOneEvent()
        {
            int alerts = 0;
            _manager.AlertDialogResult += (_, __, ___) => { alerts++; throw new SubscriberException(); };
            Assert.Throws<SubscriberException>(() => _manager.ShowDialog("", ""));
            Assert.AreEqual(1, alerts);
        }
    }
}
#endif
