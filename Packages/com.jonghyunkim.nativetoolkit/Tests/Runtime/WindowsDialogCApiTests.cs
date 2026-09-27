#nullable enable

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using JonghyunKim.NativeToolkit.Runtime.Windows.Common;
using JonghyunKim.NativeToolkit.Runtime.Windows.Dialog;
using NUnit.Framework;
using static JonghyunKim.NativeToolkit.Runtime.Windows.Dialog.Win32MessageBox;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// EditMode tests for the Windows Dialog bridge's conversions to and from native-toolkit's C ABI
    /// (artifact/features/dialog/designs/2026-09-27-windows-dialog-design-v6.md, 4 and 7.1).
    /// </summary>
    public sealed class WindowsDialogCApiTests
    {
        // ── Structure layout (native-toolkit WindowsLibraryCApiTest/Common/CApiLayoutTest.cpp) ──

        [Test]
        public void Structures_HaveTheCApiSizes()
        {
            Assert.AreEqual(16, Marshal.SizeOf<WindowsDialogCApi.Filter>());
            Assert.AreEqual(56, Marshal.SizeOf<WindowsDialogCApi.AlertRequest>());
            Assert.AreEqual(32, Marshal.SizeOf<WindowsDialogCApi.FileRequest>());
            Assert.AreEqual(40, Marshal.SizeOf<WindowsDialogCApi.SaveFileRequest>());
            Assert.AreEqual(24, Marshal.SizeOf<WindowsDialogCApi.FolderRequest>());
        }

        [TestCase(nameof(WindowsDialogCApi.Filter.Name), 0)]
        [TestCase(nameof(WindowsDialogCApi.Filter.Patterns), 8)]
        public void Filter_FieldOffsets(string field, int offset) =>
            Assert.AreEqual(offset, Marshal.OffsetOf<WindowsDialogCApi.Filter>(field).ToInt32());

        [TestCase(nameof(WindowsDialogCApi.AlertRequest.StructSize), 0)]
        [TestCase(nameof(WindowsDialogCApi.AlertRequest.Reserved0), 4)]
        [TestCase(nameof(WindowsDialogCApi.AlertRequest.Title), 8)]
        [TestCase(nameof(WindowsDialogCApi.AlertRequest.Message), 16)]
        [TestCase(nameof(WindowsDialogCApi.AlertRequest.Buttons), 24)]
        [TestCase(nameof(WindowsDialogCApi.AlertRequest.Icon), 28)]
        [TestCase(nameof(WindowsDialogCApi.AlertRequest.DefaultButton), 32)]
        [TestCase(nameof(WindowsDialogCApi.AlertRequest.TopMost), 36)]
        [TestCase(nameof(WindowsDialogCApi.AlertRequest.ShowHelpButton), 40)]
        [TestCase(nameof(WindowsDialogCApi.AlertRequest.Reserved1), 44)]
        [TestCase(nameof(WindowsDialogCApi.AlertRequest.Owner), 48)]
        public void AlertRequest_FieldOffsets(string field, int offset) =>
            Assert.AreEqual(offset, Marshal.OffsetOf<WindowsDialogCApi.AlertRequest>(field).ToInt32());

        [TestCase(nameof(WindowsDialogCApi.FileRequest.StructSize), 0)]
        [TestCase(nameof(WindowsDialogCApi.FileRequest.Reserved0), 4)]
        [TestCase(nameof(WindowsDialogCApi.FileRequest.Title), 8)]
        [TestCase(nameof(WindowsDialogCApi.FileRequest.AllowMissingFile), 16)]
        [TestCase(nameof(WindowsDialogCApi.FileRequest.Reserved1), 20)]
        [TestCase(nameof(WindowsDialogCApi.FileRequest.Owner), 24)]
        public void FileRequest_FieldOffsets(string field, int offset) =>
            Assert.AreEqual(offset, Marshal.OffsetOf<WindowsDialogCApi.FileRequest>(field).ToInt32());

        [TestCase(nameof(WindowsDialogCApi.SaveFileRequest.StructSize), 0)]
        [TestCase(nameof(WindowsDialogCApi.SaveFileRequest.Reserved0), 4)]
        [TestCase(nameof(WindowsDialogCApi.SaveFileRequest.Title), 8)]
        [TestCase(nameof(WindowsDialogCApi.SaveFileRequest.DefaultExtension), 16)]
        [TestCase(nameof(WindowsDialogCApi.SaveFileRequest.SkipOverwritePrompt), 24)]
        [TestCase(nameof(WindowsDialogCApi.SaveFileRequest.Reserved1), 28)]
        [TestCase(nameof(WindowsDialogCApi.SaveFileRequest.Owner), 32)]
        public void SaveFileRequest_FieldOffsets(string field, int offset) =>
            Assert.AreEqual(offset, Marshal.OffsetOf<WindowsDialogCApi.SaveFileRequest>(field).ToInt32());

        [TestCase(nameof(WindowsDialogCApi.FolderRequest.StructSize), 0)]
        [TestCase(nameof(WindowsDialogCApi.FolderRequest.Reserved0), 4)]
        [TestCase(nameof(WindowsDialogCApi.FolderRequest.Title), 8)]
        [TestCase(nameof(WindowsDialogCApi.FolderRequest.Owner), 16)]
        public void FolderRequest_FieldOffsets(string field, int offset) =>
            Assert.AreEqual(offset, Marshal.OffsetOf<WindowsDialogCApi.FolderRequest>(field).ToInt32());

        // ── Requests: the values 1.x's behaviour fixes ──────────────────────────

        [Test]
        public void BuildAlertRequest_CarriesTheFlagsAndZeroesTheRest()
        {
            var flags = new WindowsDialogCApi.AlertFlags(WindowsDialogCApi.ButtonsYesNo, WindowsDialogCApi.IconWarning, 1, true, true);
            WindowsDialogCApi.AlertRequest request = WindowsDialogCApi.BuildAlertRequest(new IntPtr(10), new IntPtr(20), flags);

            Assert.AreEqual(56u, request.StructSize);
            Assert.AreEqual(0u, request.Reserved0);
            Assert.AreEqual(new IntPtr(10), request.Title);
            Assert.AreEqual(new IntPtr(20), request.Message);
            Assert.AreEqual(WindowsDialogCApi.ButtonsYesNo, request.Buttons);
            Assert.AreEqual(WindowsDialogCApi.IconWarning, request.Icon);
            Assert.AreEqual(1, request.DefaultButton);
            Assert.AreEqual(1, request.TopMost);
            Assert.AreEqual(1, request.ShowHelpButton);
            Assert.AreEqual(0u, request.Reserved1);
            Assert.AreEqual(IntPtr.Zero, request.Owner);
        }

        [Test]
        public void BuildFileRequest_HasNoTitleNoOwner_AndOnlyExistingFiles()
        {
            WindowsDialogCApi.FileRequest request = WindowsDialogCApi.BuildFileRequest();
            Assert.AreEqual(32u, request.StructSize);
            Assert.AreEqual(0u, request.Reserved0);
            Assert.AreEqual(IntPtr.Zero, request.Title);
            Assert.AreEqual(0, request.AllowMissingFile);
            Assert.AreEqual(0u, request.Reserved1);
            Assert.AreEqual(IntPtr.Zero, request.Owner);
        }

        [Test]
        public void BuildSaveFileRequest_AlwaysAsksBeforeOverwriting()
        {
            WindowsDialogCApi.SaveFileRequest request = WindowsDialogCApi.BuildSaveFileRequest(new IntPtr(30));
            Assert.AreEqual(40u, request.StructSize);
            Assert.AreEqual(0u, request.Reserved0);
            Assert.AreEqual(IntPtr.Zero, request.Title);
            Assert.AreEqual(new IntPtr(30), request.DefaultExtension);
            Assert.AreEqual(0, request.SkipOverwritePrompt);
            Assert.AreEqual(0u, request.Reserved1);
            Assert.AreEqual(IntPtr.Zero, request.Owner);
        }

        [Test]
        public void BuildFolderRequest_CarriesTheTitle()
        {
            WindowsDialogCApi.FolderRequest request = WindowsDialogCApi.BuildFolderRequest(new IntPtr(40));
            Assert.AreEqual(24u, request.StructSize);
            Assert.AreEqual(0u, request.Reserved0);
            Assert.AreEqual(new IntPtr(40), request.Title);
            Assert.AreEqual(IntPtr.Zero, request.Owner);
        }

        [Test]
        public void Defaults_AreThe1xOnes()
        {
            Assert.AreEqual("All Files\0*.*\0\0", WindowsDialogCApi.DefaultFilter);
            Assert.AreEqual("Select Folder", WindowsDialogCApi.DefaultFolderTitle);
            Assert.AreEqual("Select Folders", WindowsDialogCApi.DefaultMultiFolderTitle);
            Assert.AreEqual("txt", WindowsDialogCApi.DefaultSaveExtension);
        }

        // ── Alert style ──────────────────────────────────────────────────────────

        [Test]
        public void CombineAlertStyle_FillsEachNullWithThe1xDefault()
        {
            Assert.AreEqual(MB_OK | MB_ICONINFORMATION | MB_DEFBUTTON1 | MB_APPLMODAL,
                WindowsDialogCApi.CombineAlertStyle(null, null, null, null));
            Assert.AreEqual(MB_YESNO | MB_ICONINFORMATION, WindowsDialogCApi.CombineAlertStyle(MB_YESNO, null, null, null));
        }

        [TestCase(MB_OK, WindowsDialogCApi.ButtonsOk)]
        [TestCase(MB_OKCANCEL, WindowsDialogCApi.ButtonsOkCancel)]
        [TestCase(MB_ABORTRETRYIGNORE, WindowsDialogCApi.ButtonsAbortRetryIgnore)]
        [TestCase(MB_YESNOCANCEL, WindowsDialogCApi.ButtonsYesNoCancel)]
        [TestCase(MB_YESNO, WindowsDialogCApi.ButtonsYesNo)]
        [TestCase(MB_RETRYCANCEL, WindowsDialogCApi.ButtonsRetryCancel)]
        [TestCase(MB_CANCELTRYCONTINUE, WindowsDialogCApi.ButtonsCancelTryContinue)]
        public void TryMapAlertStyle_MapsTheButtonsByName(uint style, int expected)
        {
            Assert.IsTrue(WindowsDialogCApi.TryMapAlertStyle(style, out WindowsDialogCApi.AlertFlags flags));
            Assert.AreEqual(expected, flags.Buttons);
        }

        [TestCase(0u, WindowsDialogCApi.IconNone)]
        [TestCase(MB_ICONHAND, WindowsDialogCApi.IconError)]
        [TestCase(MB_ICONQUESTION, WindowsDialogCApi.IconQuestion)]
        [TestCase(MB_ICONWARNING, WindowsDialogCApi.IconWarning)]
        [TestCase(MB_ICONINFORMATION, WindowsDialogCApi.IconInformation)]
        public void TryMapAlertStyle_MapsTheIconByName(uint style, int expected)
        {
            Assert.IsTrue(WindowsDialogCApi.TryMapAlertStyle(style, out WindowsDialogCApi.AlertFlags flags));
            Assert.AreEqual(expected, flags.Icon);
        }

        [TestCase(MB_DEFBUTTON1, 0)]
        [TestCase(MB_DEFBUTTON2, 1)]
        [TestCase(MB_DEFBUTTON3, 2)]
        [TestCase(MB_DEFBUTTON4, 3)]
        public void TryMapAlertStyle_MapsTheDefaultButton(uint style, int expected)
        {
            Assert.IsTrue(WindowsDialogCApi.TryMapAlertStyle(style, out WindowsDialogCApi.AlertFlags flags));
            Assert.AreEqual(expected, flags.DefaultButton);
        }

        [Test]
        public void TryMapAlertStyle_AFlagWorksWhicheverArgumentCarriesIt()
        {
            // The sample's own style, and a warning icon put in the buttons argument.
            uint sample = WindowsDialogCApi.CombineAlertStyle(MB_OKCANCEL, MB_ICONINFORMATION, MB_DEFBUTTON2, MB_APPLMODAL);
            Assert.IsTrue(WindowsDialogCApi.TryMapAlertStyle(sample, out WindowsDialogCApi.AlertFlags flags));
            Assert.AreEqual((WindowsDialogCApi.ButtonsOkCancel, WindowsDialogCApi.IconInformation, 1), (flags.Buttons, flags.Icon, flags.DefaultButton));

            uint mixed = WindowsDialogCApi.CombineAlertStyle(MB_YESNO | MB_ICONWARNING, 0u, null, null);
            Assert.IsTrue(WindowsDialogCApi.TryMapAlertStyle(mixed, out flags));
            Assert.AreEqual((WindowsDialogCApi.ButtonsYesNo, WindowsDialogCApi.IconWarning), (flags.Buttons, flags.Icon));
        }

        [Test]
        public void TryMapAlertStyle_TopMostAndHelp()
        {
            Assert.IsTrue(WindowsDialogCApi.TryMapAlertStyle(MB_TOPMOST | 0x4000u, out WindowsDialogCApi.AlertFlags flags));
            Assert.IsTrue(flags.TopMost);
            Assert.IsTrue(flags.ShowHelpButton);

            Assert.IsTrue(WindowsDialogCApi.TryMapAlertStyle(MB_OK, out flags));
            Assert.IsFalse(flags.TopMost);
            Assert.IsFalse(flags.ShowHelpButton);
        }

        [TestCase(MB_SYSTEMMODAL)]
        [TestCase(MB_TASKMODAL)]
        [TestCase(MB_RIGHT)]
        [TestCase(MB_RTLREADING)]
        [TestCase(0x7u)]        // an undefined button set
        [TestCase(0x50u)]       // an undefined icon
        [TestCase(0x400u)]      // an undefined default button
        [TestCase(0x80000000u)] // any other bit
        public void TryMapAlertStyle_RefusesWhatTheCApiCannotExpress(uint style)
        {
            Assert.IsFalse(WindowsDialogCApi.TryMapAlertStyle(style, out _));
        }

        // ── Alert result ─────────────────────────────────────────────────────────

        [TestCase(WindowsDialogCApi.ResultOk, 1)]
        [TestCase(WindowsDialogCApi.ResultCancel, 2)]
        [TestCase(WindowsDialogCApi.ResultAbort, 3)]
        [TestCase(WindowsDialogCApi.ResultRetry, 4)]
        [TestCase(WindowsDialogCApi.ResultIgnore, 5)]
        [TestCase(WindowsDialogCApi.ResultYes, 6)]
        [TestCase(WindowsDialogCApi.ResultNo, 7)]
        [TestCase(WindowsDialogCApi.ResultClose, 8)]
        [TestCase(WindowsDialogCApi.ResultHelp, 9)]
        [TestCase(WindowsDialogCApi.ResultTryAgain, 10)]
        [TestCase(WindowsDialogCApi.ResultContinue, 11)]
        public void TryToWin32Result_MapsByName(int result, int win32Id)
        {
            Assert.IsTrue(WindowsDialogCApi.TryToWin32Result(result, out int id));
            Assert.AreEqual(win32Id, id);
        }

        [TestCase(-1)]
        [TestCase(11)]
        public void TryToWin32Result_RefusesAnUndefinedValue(int result)
        {
            Assert.IsFalse(WindowsDialogCApi.TryToWin32Result(result, out _));
        }

        // ── Errors ───────────────────────────────────────────────────────────────

        [Test]
        public void FailureCode_PassesTheSystemCodeOn_EvenWhenItReadsNegative()
        {
            Assert.AreEqual(0x3002, WindowsDialogCApi.FailureCode(WindowsDialogCApi.ErrorSystemError, 0x3002u)); // FNERR_BUFFERTOOSMALL
            Assert.AreEqual(unchecked((int)0x80070005u), WindowsDialogCApi.FailureCode(WindowsDialogCApi.ErrorSystemError, 0x80070005u));
            Assert.Less(WindowsDialogCApi.FailureCode(WindowsDialogCApi.ErrorSystemError, 0x80004005u), 0);
        }

        [Test]
        public void FailureCode_ReservedValuesForTheRest()
        {
            Assert.AreEqual(WindowsDialogErrorCodes.InvalidArgument, WindowsDialogCApi.FailureCode(WindowsDialogCApi.ErrorInvalidParameter, 0));
            Assert.AreEqual(WindowsDialogErrorCodes.Unknown, WindowsDialogCApi.FailureCode(WindowsDialogCApi.ErrorUnknown, 0x8007000Eu));
            Assert.AreEqual(WindowsDialogErrorCodes.Unknown, WindowsDialogCApi.FailureCode(WindowsDialogCApi.ErrorCanceled, 0)); // an alert's unexpected cancel
            Assert.AreEqual(WindowsDialogErrorCodes.Unknown, WindowsDialogCApi.FailureCode(99, 0));
        }

        [Test]
        public void FromException_LoadFailuresAreNativeUnavailable()
        {
            Assert.AreEqual(WindowsDialogErrorCodes.NativeUnavailable, WindowsDialogCApi.FromException(new DllNotFoundException()));
            Assert.AreEqual(WindowsDialogErrorCodes.NativeUnavailable, WindowsDialogCApi.FromException(new EntryPointNotFoundException()));
            Assert.AreEqual(WindowsDialogErrorCodes.NativeUnavailable, WindowsDialogCApi.FromException(new BadImageFormatException()));
            Assert.AreEqual(WindowsDialogErrorCodes.Unknown, WindowsDialogCApi.FromException(new OutOfMemoryException()));
            Assert.AreEqual(WindowsDialogErrorCodes.Unknown, WindowsDialogCApi.FromException(new InvalidOperationException()));
        }

        [Test]
        public void StatusFor_TheNativeStates()
        {
            Assert.AreEqual(0, WindowsDialogCApi.StatusFor(WindowsNativeToolkitCApi.NativeState.Available));
            Assert.AreEqual(WindowsDialogErrorCodes.NativeUnavailable, WindowsDialogCApi.StatusFor(WindowsNativeToolkitCApi.NativeState.NativeUnavailable));
            Assert.AreEqual(WindowsDialogErrorCodes.PlatformUnavailable, WindowsDialogCApi.StatusFor(WindowsNativeToolkitCApi.NativeState.PlatformUnavailable));
        }

        [Test]
        public void ErrorCodes_AreTheReservedValues()
        {
            Assert.AreEqual(-1, WindowsDialogErrorCodes.Cancelled);
            Assert.AreEqual(-2, WindowsDialogErrorCodes.InvalidArgument);
            Assert.AreEqual(-3, WindowsDialogErrorCodes.Unknown);
            Assert.AreEqual(-4, WindowsDialogErrorCodes.NativeUnavailable);
            Assert.AreEqual(-5, WindowsDialogErrorCodes.PlatformUnavailable);
        }

        [Test]
        public void Calls_InTheEditor_ArePlatformUnavailable_AndShowNothing()
        {
            var filters = new List<KeyValuePair<string, string>> { new("All Files", "*.*") };
            Assert.AreEqual(WindowsDialogErrorCodes.PlatformUnavailable,
                WindowsDialogCApi.ShowAlert("t", "m", default, out int alertResult, out uint systemCode));
            Assert.AreEqual((0, 0u), (alertResult, systemCode));
            Assert.AreEqual(WindowsDialogErrorCodes.PlatformUnavailable, WindowsDialogCApi.OpenFile(filters, out string? path, out _));
            Assert.IsNull(path);
            Assert.AreEqual(WindowsDialogErrorCodes.PlatformUnavailable, WindowsDialogCApi.OpenFiles(filters, out List<string>? paths, out _));
            Assert.IsNull(paths);
            Assert.AreEqual(WindowsDialogErrorCodes.PlatformUnavailable, WindowsDialogCApi.SaveFile(filters, "txt", out path, out _));
            Assert.AreEqual(WindowsDialogErrorCodes.PlatformUnavailable, WindowsDialogCApi.PickFolder("t", out path, out _));
            Assert.AreEqual(WindowsDialogErrorCodes.PlatformUnavailable, WindowsDialogCApi.PickFolders("t", out paths, out _));
        }

        // ── Filter strings (read the way the file dialogs read them) ────────────

        private static string[] Parse(string filter)
        {
            Assert.IsTrue(WindowsDialogCApi.TryParseFilter(filter, out List<KeyValuePair<string, string>> pairs), $"refused {Escape(filter)}");
            return pairs.Select(p => p.Key + "=" + p.Value).ToArray();
        }

        private static string Escape(string text) => text.Replace("\0", "\\0");

        [Test]
        public void TryParseFilter_TheDefault_IsOnePair() =>
            CollectionAssert.AreEqual(new[] { "All Files=*.*" }, Parse(WindowsDialogCApi.DefaultFilter));

        [Test]
        public void TryParseFilter_SeveralPairs() =>
            CollectionAssert.AreEqual(new[] { "Text=*.txt;*.log", "Images=*.png" }, Parse("Text\0*.txt;*.log\0Images\0*.png\0\0"));

        [Test]
        public void TryParseFilter_EndsAtTheFirstEmptyName() =>
            CollectionAssert.AreEqual(new[] { "A=*.a" }, Parse("A\0*.a\0\0B\0*.b\0\0"));

        [Test]
        public void TryParseFilter_EndsAtTheEndOfTheString() =>
            CollectionAssert.AreEqual(new[] { "Text=*.txt" }, Parse("Text\0*.txt"));

        [TestCase("")]
        [TestCase("\0*.txt\0\0")]
        [TestCase("\0")]
        public void TryParseFilter_NoPairs_IsEveryFile(string filter) =>
            CollectionAssert.AreEqual(new[] { "All Files=*.*" }, Parse(filter));

        [TestCase("A\0\0")]      // a name with an empty pattern
        [TestCase("A\0")]        // a name whose pattern is missing
        [TestCase("A")]          // a name and nothing else
        [TestCase("A\0;\0\0")]   // a pattern of ';' only
        [TestCase("A\0;;\0\0")]
        [TestCase("A\0*.a\0B\0\0")]
        public void TryParseFilter_RefusesANameWithoutAPattern(string filter)
        {
            Assert.IsFalse(WindowsDialogCApi.TryParseFilter(filter, out _), Escape(filter));
        }

        [Test]
        public void TryParseFilter_KeepsEmptyEntriesInsideAPattern() =>
            CollectionAssert.AreEqual(new[] { "A=*.txt;;*.log" }, Parse("A\0*.txt;;*.log\0\0"));
    }
}
#endif
