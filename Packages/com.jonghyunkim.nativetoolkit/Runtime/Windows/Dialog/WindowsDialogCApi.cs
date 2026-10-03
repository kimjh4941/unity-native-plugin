#nullable enable

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
namespace JonghyunKim.NativeToolkit.Runtime.Windows.Dialog
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using JonghyunKim.NativeToolkit.Runtime.Windows.Common;

    /// <summary>
    /// The bridge between <see cref="WindowsDialogManager"/> and native-toolkit's C ABI
    /// (<c>NativeToolkitC/Dialog.h</c>, version 2.0.0).
    /// </summary>
    /// <remarks>
    /// Two halves, as agent-rules/coding-rules/common.md ("Unity Bridge パターン > Windows") lays
    /// out. The conversions between the manager's public arguments and the C ABI's values are pure
    /// functions compiled everywhere, so EditMode tests reach them. The <c>DllImport</c>s and the
    /// calls that marshal through them are compiled only into a Windows player; in the Editor the
    /// calls report <see cref="WindowsDialogErrorCodes.PlatformUnavailable"/> without touching the
    /// DLL. Design: artifact/windows/dialog/designs/2026-09-27-windows-dialog-design-v6.md.
    /// </remarks>
    internal static class WindowsDialogCApi
    {
        // ntk_dialog_error
        internal const int ErrorNone = 0;
        internal const int ErrorInvalidParameter = 1;
        internal const int ErrorCanceled = 2;
        internal const int ErrorSystemError = 4;
        internal const int ErrorUnknown = 5;

        // ntk_dialog_alert_buttons
        internal const int ButtonsOk = 0;
        internal const int ButtonsOkCancel = 1;
        internal const int ButtonsYesNo = 2;
        internal const int ButtonsYesNoCancel = 3;
        internal const int ButtonsRetryCancel = 4;
        internal const int ButtonsAbortRetryIgnore = 5;
        internal const int ButtonsCancelTryContinue = 6;

        // ntk_dialog_alert_icon
        internal const int IconNone = 0;
        internal const int IconInformation = 1;
        internal const int IconWarning = 2;
        internal const int IconError = 3;
        internal const int IconQuestion = 4;

        // ntk_dialog_alert_result
        internal const int ResultOk = 0;
        internal const int ResultCancel = 1;
        internal const int ResultYes = 2;
        internal const int ResultNo = 3;
        internal const int ResultRetry = 4;
        internal const int ResultAbort = 5;
        internal const int ResultIgnore = 6;
        internal const int ResultTryAgain = 7;
        internal const int ResultContinue = 8;
        internal const int ResultClose = 9;
        internal const int ResultHelp = 10;

        // What the public methods fall back to for a null argument, as 1.x did.
        internal const string DefaultFilter = "All Files\0*.*\0\0";
        internal const string DefaultFolderTitle = "Select Folder";
        internal const string DefaultMultiFolderTitle = "Select Folders";
        internal const string DefaultSaveExtension = "txt";

        // The groups of a Win32 MessageBox uType, and the flags the C ABI has fields for.
        private const uint ButtonsMask = 0x0000000F;
        private const uint IconMask = 0x000000F0;
        private const uint DefaultButtonMask = 0x00000F00;
        private const uint MbHelp = 0x00004000;

        /// <summary><c>ntk_dialog_filter</c>, 16 bytes.</summary>
        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        internal struct Filter
        {
            public IntPtr Name;
            public IntPtr Patterns;
        }

        /// <summary><c>ntk_dialog_alert_request</c>, 56 bytes.</summary>
        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        internal struct AlertRequest
        {
            public uint StructSize;
            public uint Reserved0;
            public IntPtr Title;
            public IntPtr Message;
            public int Buttons;
            public int Icon;
            public int DefaultButton;
            public int TopMost;
            public int ShowHelpButton;
            public uint Reserved1;
            public IntPtr Owner;
        }

        /// <summary><c>ntk_dialog_file_request</c>, 32 bytes.</summary>
        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        internal struct FileRequest
        {
            public uint StructSize;
            public uint Reserved0;
            public IntPtr Title;
            public int AllowMissingFile;
            public uint Reserved1;
            public IntPtr Owner;
        }

        /// <summary><c>ntk_dialog_save_file_request</c>, 40 bytes.</summary>
        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        internal struct SaveFileRequest
        {
            public uint StructSize;
            public uint Reserved0;
            public IntPtr Title;
            public IntPtr DefaultExtension;
            public int SkipOverwritePrompt;
            public uint Reserved1;
            public IntPtr Owner;
        }

        /// <summary><c>ntk_dialog_folder_request</c>, 24 bytes.</summary>
        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        internal struct FolderRequest
        {
            public uint StructSize;
            public uint Reserved0;
            public IntPtr Title;
            public IntPtr Owner;
        }

        /// <summary>A message box's buttons, icon and options, in the C ABI's terms.</summary>
        internal readonly struct AlertFlags
        {
            internal AlertFlags(int buttons, int icon, int defaultButton, bool topMost, bool showHelpButton)
            {
                Buttons = buttons;
                Icon = icon;
                DefaultButton = defaultButton;
                TopMost = topMost;
                ShowHelpButton = showHelpButton;
            }

            internal int Buttons { get; }
            internal int Icon { get; }
            internal int DefaultButton { get; }
            internal bool TopMost { get; }
            internal bool ShowHelpButton { get; }
        }

        // ── Conversions (pure) ───────────────────────────────────────────────────

        /// <summary>
        /// The four <see cref="Win32MessageBox"/> arguments as one uType, with each null replaced by
        /// the default 1.x used. 1.x OR-ed the four together before calling MessageBoxW, so a flag
        /// works whichever argument carries it.
        /// </summary>
        internal static uint CombineAlertStyle(uint? buttons, uint? icon, uint? defaultButton, uint? options) =>
            (buttons ?? Win32MessageBox.MB_OK)
            | (icon ?? Win32MessageBox.MB_ICONINFORMATION)
            | (defaultButton ?? Win32MessageBox.MB_DEFBUTTON1)
            | (options ?? Win32MessageBox.MB_APPLMODAL);

        /// <summary>
        /// Splits a MessageBox uType into what the C ABI has fields for. Returns false when it holds
        /// something the C ABI cannot express (system or task modal, right-aligned or right-to-left
        /// text, an undefined value, any other bit); the caller shows nothing and reports
        /// <see cref="WindowsDialogErrorCodes.InvalidArgument"/>.
        /// </summary>
        internal static bool TryMapAlertStyle(uint style, out AlertFlags flags)
        {
            flags = default;

            int buttons;
            switch (style & ButtonsMask)
            {
                case Win32MessageBox.MB_OK: buttons = ButtonsOk; break;
                case Win32MessageBox.MB_OKCANCEL: buttons = ButtonsOkCancel; break;
                case Win32MessageBox.MB_ABORTRETRYIGNORE: buttons = ButtonsAbortRetryIgnore; break;
                case Win32MessageBox.MB_YESNOCANCEL: buttons = ButtonsYesNoCancel; break;
                case Win32MessageBox.MB_YESNO: buttons = ButtonsYesNo; break;
                case Win32MessageBox.MB_RETRYCANCEL: buttons = ButtonsRetryCancel; break;
                case Win32MessageBox.MB_CANCELTRYCONTINUE: buttons = ButtonsCancelTryContinue; break;
                default: return false;
            }

            int icon;
            switch (style & IconMask)
            {
                case 0: icon = IconNone; break;
                case Win32MessageBox.MB_ICONHAND: icon = IconError; break;
                case Win32MessageBox.MB_ICONQUESTION: icon = IconQuestion; break;
                case Win32MessageBox.MB_ICONEXCLAMATION: icon = IconWarning; break;
                case Win32MessageBox.MB_ICONASTERISK: icon = IconInformation; break;
                default: return false;
            }

            uint defaultButtonBits = style & DefaultButtonMask;
            if (defaultButtonBits > Win32MessageBox.MB_DEFBUTTON4) return false;
            int defaultButton = (int)(defaultButtonBits >> 8);

            bool topMost = (style & Win32MessageBox.MB_TOPMOST) != 0;
            bool help = (style & MbHelp) != 0;

            uint rest = style & ~(ButtonsMask | IconMask | DefaultButtonMask | Win32MessageBox.MB_TOPMOST | MbHelp);
            if (rest != 0) return false;

            flags = new AlertFlags(buttons, icon, defaultButton, topMost, help);
            return true;
        }

        /// <summary>
        /// The Win32 button ID (IDOK, IDCANCEL, ...) for a C ABI alert result, matched by name: the
        /// two numberings differ. Returns false for a value the C ABI does not define.
        /// </summary>
        internal static bool TryToWin32Result(int alertResult, out int win32Id)
        {
            win32Id = alertResult switch
            {
                ResultOk => 1,
                ResultCancel => 2,
                ResultAbort => 3,
                ResultRetry => 4,
                ResultIgnore => 5,
                ResultYes => 6,
                ResultNo => 7,
                ResultClose => 8,
                ResultHelp => 9,
                ResultTryAgain => 10,
                ResultContinue => 11,
                _ => 0,
            };
            return win32Id != 0;
        }

        /// <summary>
        /// Reads a Win32 filter string (<c>"name\0patterns\0...\0\0"</c>) the way the file dialogs
        /// do: pairs from the start, until an empty name or the end of the string; anything after is
        /// ignored. Returns false when a name has no pattern, an empty one, or one made of ';' only
        /// (the native library would drop the empty entries and quietly show every file). A string
        /// with no pairs at all gives the default: every file.
        /// </summary>
        internal static bool TryParseFilter(string filter, out List<KeyValuePair<string, string>> pairs)
        {
            pairs = new List<KeyValuePair<string, string>>();
            int index = 0;
            while (index < filter.Length)
            {
                string name = ReadToNul(filter, ref index, out _);
                if (name.Length == 0) break;

                string patterns = ReadToNul(filter, ref index, out bool terminated);
                if (patterns.Length == 0 || !HasPattern(patterns)) return false;

                pairs.Add(new KeyValuePair<string, string>(name, patterns));
                if (!terminated) break;
            }

            if (pairs.Count == 0)
                pairs.Add(new KeyValuePair<string, string>("All Files", "*.*"));
            return true;
        }

        private static string ReadToNul(string text, ref int index, out bool terminated)
        {
            int end = text.IndexOf('\0', index);
            terminated = end >= 0;
            if (end < 0) end = text.Length;
            string part = text.Substring(index, end - index);
            index = terminated ? end + 1 : text.Length;
            return part;
        }

        private static bool HasPattern(string patterns)
        {
            foreach (string part in patterns.Split(';'))
                if (part.Length > 0) return true;
            return false;
        }

        /// <summary>
        /// The <c>errorCode</c> for a C ABI failure other than a file dialog's cancel: the system
        /// code as the OS reported it for <c>SYSTEM_ERROR</c> (an HRESULT reads as negative),
        /// <see cref="WindowsDialogErrorCodes.InvalidArgument"/> for a request the native library
        /// rejected, and <see cref="WindowsDialogErrorCodes.Unknown"/> for anything else.
        /// </summary>
        internal static int FailureCode(int error, uint systemCode) => error switch
        {
            ErrorSystemError => unchecked((int)systemCode),
            ErrorInvalidParameter => WindowsDialogErrorCodes.InvalidArgument,
            _ => WindowsDialogErrorCodes.Unknown,
        };

        /// <summary>The <c>errorCode</c> for an exception thrown while calling the native library.</summary>
        internal static int FromException(Exception exception) =>
            WindowsNativeToolkitCApi.IsNativeUnavailable(exception)
                ? WindowsDialogErrorCodes.NativeUnavailable
                : WindowsDialogErrorCodes.Unknown;

        /// <summary>The <c>errorCode</c> when the native library is not usable, or 0 when it is.</summary>
        internal static int StatusFor(WindowsNativeToolkitCApi.NativeState state) => state switch
        {
            WindowsNativeToolkitCApi.NativeState.Available => 0,
            WindowsNativeToolkitCApi.NativeState.PlatformUnavailable => WindowsDialogErrorCodes.PlatformUnavailable,
            _ => WindowsDialogErrorCodes.NativeUnavailable,
        };

        internal static AlertRequest BuildAlertRequest(IntPtr title, IntPtr message, AlertFlags flags) => new AlertRequest
        {
            StructSize = (uint)Marshal.SizeOf<AlertRequest>(),
            Title = title,
            Message = message,
            Buttons = flags.Buttons,
            Icon = flags.Icon,
            DefaultButton = flags.DefaultButton,
            TopMost = flags.TopMost ? 1 : 0,
            ShowHelpButton = flags.ShowHelpButton ? 1 : 0,
        };

        /// <summary>The open-file request 1.x's behaviour asks for: no title, no owner, existing files only.</summary>
        internal static FileRequest BuildFileRequest() => new FileRequest
        {
            StructSize = (uint)Marshal.SizeOf<FileRequest>(),
        };

        /// <summary>The save-file request 1.x's behaviour asks for: no title, no owner, always ask before overwriting.</summary>
        internal static SaveFileRequest BuildSaveFileRequest(IntPtr defaultExtension) => new SaveFileRequest
        {
            StructSize = (uint)Marshal.SizeOf<SaveFileRequest>(),
            DefaultExtension = defaultExtension,
        };

        internal static FolderRequest BuildFolderRequest(IntPtr title) => new FolderRequest
        {
            StructSize = (uint)Marshal.SizeOf<FolderRequest>(),
            Title = title,
        };

        // ── Native calls ─────────────────────────────────────────────────────────
        // Each returns the C ABI's ntk_dialog_error, or WindowsDialogErrorCodes.NativeUnavailable /
        // PlatformUnavailable when the library is not usable (no dialog is shown). systemCode is read
        // right after the call, before any other native call, whenever the error is neither NONE nor
        // CANCELED. Exceptions are left to the caller, which turns them into an errorCode.

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        internal static int ShowAlert(string title, string message, AlertFlags flags, out int alertResult, out uint systemCode)
        {
            alertResult = 0;
            systemCode = 0;
            int status = StatusFor(WindowsNativeToolkitCApi.EnsureNativeAvailable());
            if (status != 0) return status;

            var allocated = new List<IntPtr>();
            try
            {
                AlertRequest request = BuildAlertRequest(
                    WindowsNativeToolkitCApi.AllocUtf8(title, allocated),
                    WindowsNativeToolkitCApi.AllocUtf8(message, allocated),
                    flags);
                int error = ntk_dialog_show_alert(ref request, out alertResult);
                if (error != ErrorNone && error != ErrorCanceled) systemCode = WindowsNativeToolkitCApi.ntk_last_system_code();
                return error;
            }
            finally
            {
                WindowsNativeToolkitCApi.FreeAll(allocated);
            }
        }

        internal static int OpenFile(IReadOnlyList<KeyValuePair<string, string>> filters, out string? path, out uint systemCode) =>
            PathCall(filters, (IntPtr array, UIntPtr count, out IntPtr result) =>
            {
                FileRequest request = BuildFileRequest();
                return ntk_dialog_show_open_file(ref request, array, count, out result);
            }, out path, out systemCode);

        internal static int SaveFile(IReadOnlyList<KeyValuePair<string, string>> filters, string defaultExtension, out string? path, out uint systemCode)
        {
            var extension = new List<IntPtr>();
            try
            {
                IntPtr extensionPointer = WindowsNativeToolkitCApi.AllocUtf8(defaultExtension, extension);
                return PathCall(filters, (IntPtr array, UIntPtr count, out IntPtr result) =>
                {
                    SaveFileRequest request = BuildSaveFileRequest(extensionPointer);
                    return ntk_dialog_show_save_file(ref request, array, count, out result);
                }, out path, out systemCode);
            }
            finally
            {
                WindowsNativeToolkitCApi.FreeAll(extension);
            }
        }

        internal static int OpenFiles(IReadOnlyList<KeyValuePair<string, string>> filters, out List<string>? paths, out uint systemCode) =>
            ListCall(filters, (IntPtr array, UIntPtr count, out IntPtr result) =>
            {
                FileRequest request = BuildFileRequest();
                return ntk_dialog_show_open_files(ref request, array, count, out result);
            }, out paths, out systemCode);

        internal static int PickFolder(string title, out string? path, out uint systemCode)
        {
            var allocated = new List<IntPtr>();
            try
            {
                IntPtr titlePointer = WindowsNativeToolkitCApi.AllocUtf8(title, allocated);
                return PathCall(Array.Empty<KeyValuePair<string, string>>(), (IntPtr _, UIntPtr __, out IntPtr result) =>
                {
                    FolderRequest request = BuildFolderRequest(titlePointer);
                    return ntk_dialog_show_pick_folder(ref request, out result);
                }, out path, out systemCode);
            }
            finally
            {
                WindowsNativeToolkitCApi.FreeAll(allocated);
            }
        }

        internal static int PickFolders(string title, out List<string>? paths, out uint systemCode)
        {
            var allocated = new List<IntPtr>();
            try
            {
                IntPtr titlePointer = WindowsNativeToolkitCApi.AllocUtf8(title, allocated);
                return ListCall(Array.Empty<KeyValuePair<string, string>>(), (IntPtr _, UIntPtr __, out IntPtr result) =>
                {
                    FolderRequest request = BuildFolderRequest(titlePointer);
                    return ntk_dialog_show_pick_folders(ref request, out result);
                }, out paths, out systemCode);
            }
            finally
            {
                WindowsNativeToolkitCApi.FreeAll(allocated);
            }
        }

        private delegate int NativeDialog(IntPtr filters, UIntPtr filterCount, out IntPtr result);

        /// <summary>A dialog that answers with one <c>ntk_string</c>.</summary>
        private static int PathCall(IReadOnlyList<KeyValuePair<string, string>> filters, NativeDialog call, out string? path, out uint systemCode)
        {
            path = null;
            systemCode = 0;
            int status = StatusFor(WindowsNativeToolkitCApi.EnsureNativeAvailable());
            if (status != 0) return status;

            var allocated = new List<IntPtr>();
            IntPtr result = IntPtr.Zero;
            try
            {
                IntPtr array = AllocFilters(filters, allocated, out UIntPtr count);
                int error = call(array, count, out result);
                if (error != ErrorNone && error != ErrorCanceled) systemCode = WindowsNativeToolkitCApi.ntk_last_system_code();
                if (error == ErrorNone) path = WindowsNativeToolkitCApi.ReadString(result);
                return error;
            }
            finally
            {
                // The inputs are freed even when freeing the result throws.
                try
                {
                    if (result != IntPtr.Zero) WindowsNativeToolkitCApi.ntk_string_free(result);
                }
                finally
                {
                    WindowsNativeToolkitCApi.FreeAll(allocated);
                }
            }
        }

        /// <summary>A dialog that answers with an <c>ntk_string_list</c>.</summary>
        private static int ListCall(IReadOnlyList<KeyValuePair<string, string>> filters, NativeDialog call, out List<string>? paths, out uint systemCode)
        {
            paths = null;
            systemCode = 0;
            int status = StatusFor(WindowsNativeToolkitCApi.EnsureNativeAvailable());
            if (status != 0) return status;

            var allocated = new List<IntPtr>();
            IntPtr result = IntPtr.Zero;
            try
            {
                IntPtr array = AllocFilters(filters, allocated, out UIntPtr count);
                int error = call(array, count, out result);
                if (error != ErrorNone && error != ErrorCanceled) systemCode = WindowsNativeToolkitCApi.ntk_last_system_code();
                if (error == ErrorNone) paths = WindowsNativeToolkitCApi.ReadStringList(result);
                return error;
            }
            finally
            {
                try
                {
                    if (result != IntPtr.Zero) WindowsNativeToolkitCApi.ntk_string_list_free(result);
                }
                finally
                {
                    WindowsNativeToolkitCApi.FreeAll(allocated);
                }
            }
        }

        /// <summary>Lays the filters out as an <c>ntk_dialog_filter</c> array; <see cref="IntPtr.Zero"/> for none.</summary>
        private static IntPtr AllocFilters(IReadOnlyList<KeyValuePair<string, string>> filters, List<IntPtr> allocated, out UIntPtr count)
        {
            count = new UIntPtr((uint)filters.Count);
            if (filters.Count == 0) return IntPtr.Zero;

            int size = Marshal.SizeOf<Filter>();
            IntPtr array = Marshal.AllocHGlobal(size * filters.Count);
            allocated.Add(array);
            for (int i = 0; i < filters.Count; i++)
            {
                var filter = new Filter
                {
                    Name = WindowsNativeToolkitCApi.AllocUtf8(filters[i].Key, allocated),
                    Patterns = WindowsNativeToolkitCApi.AllocUtf8(filters[i].Value, allocated),
                };
                Marshal.StructureToPtr(filter, array + i * size, false);
            }
            return array;
        }

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_dialog_show_alert(ref AlertRequest request, out int outResult);

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_dialog_show_open_file(ref FileRequest request, IntPtr filters, UIntPtr filterCount, out IntPtr outPath);

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_dialog_show_open_files(ref FileRequest request, IntPtr filters, UIntPtr filterCount, out IntPtr outPaths);

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_dialog_show_save_file(ref SaveFileRequest request, IntPtr filters, UIntPtr filterCount, out IntPtr outPath);

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_dialog_show_pick_folder(ref FolderRequest request, out IntPtr outPath);

        [DllImport(WindowsNativeToolkitCApi.DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ntk_dialog_show_pick_folders(ref FolderRequest request, out IntPtr outPaths);
#else
        internal static int ShowAlert(string title, string message, AlertFlags flags, out int alertResult, out uint systemCode)
        {
            alertResult = 0;
            systemCode = 0;
            return WindowsDialogErrorCodes.PlatformUnavailable;
        }

        internal static int OpenFile(IReadOnlyList<KeyValuePair<string, string>> filters, out string? path, out uint systemCode) =>
            Unavailable(out path, out systemCode);

        internal static int SaveFile(IReadOnlyList<KeyValuePair<string, string>> filters, string defaultExtension, out string? path, out uint systemCode) =>
            Unavailable(out path, out systemCode);

        internal static int OpenFiles(IReadOnlyList<KeyValuePair<string, string>> filters, out List<string>? paths, out uint systemCode) =>
            Unavailable(out paths, out systemCode);

        internal static int PickFolder(string title, out string? path, out uint systemCode) =>
            Unavailable(out path, out systemCode);

        internal static int PickFolders(string title, out List<string>? paths, out uint systemCode) =>
            Unavailable(out paths, out systemCode);

        private static int Unavailable<T>(out T? value, out uint systemCode) where T : class
        {
            value = null;
            systemCode = 0;
            return WindowsDialogErrorCodes.PlatformUnavailable;
        }
#endif
    }
}
#endif
