#nullable enable

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
namespace JonghyunKim.NativeToolkit.Runtime.Windows.Dialog
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using UnityEngine;

    /// <summary>
    /// Singleton manager for Windows native dialogs: alerts, file open and save, and folder selection.
    /// Calls native-toolkit's C ABI 2.0.0 and reports each result through an event.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every call is synchronous: it blocks the calling thread (normally the main thread) until the
    /// dialog closes, then raises its event exactly once on that thread before returning. An exception
    /// thrown by a subscriber reaches the caller.
    /// </para>
    /// <para>
    /// Failures never throw; they arrive as the event's <c>errorCode</c>. The values
    /// <see cref="WindowsDialogManager"/> reserves are in <see cref="WindowsDialogErrorCodes"/>; every
    /// other value is the operating system's own code, and an <c>HRESULT</c> reads as negative.
    /// </para>
    /// <para>
    /// The class also compiles in the Unity Editor, where no dialog is shown: every call reports
    /// <see cref="WindowsDialogErrorCodes.PlatformUnavailable"/>.
    /// </para>
    /// <para>
    /// Changes from native-toolkit 1.x: <c>buffer_size</c> is ignored; an empty title or message,
    /// a <see cref="Win32MessageBox"/> flag the C ABI cannot express, and a filter name without a
    /// pattern are refused with <see cref="WindowsDialogErrorCodes.InvalidArgument"/> before any
    /// dialog is shown; a filter string with no pairs shows every file; empty entries inside a
    /// pattern (<c>"*.txt;;*.log"</c>) are dropped; an unpaired surrogate becomes U+FFFD.
    /// </para>
    /// </remarks>
    public class WindowsDialogManager : MonoBehaviour
    {
        private const string LogTag = nameof(WindowsDialogManager);

        private static WindowsDialogManager? _instance;

        /// <summary>
        /// Singleton instance property for WindowsDialogManager.
        /// Creates a new instance if none exists and ensures it persists across scene loads.
        /// </summary>
        public static WindowsDialogManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    Debug.Log($"[{LogTag}] Creating new instance of WindowsDialogManager");
                    GameObject singletonObject = new GameObject("WindowsDialogManager");
                    _instance = singletonObject.AddComponent<WindowsDialogManager>();
                    DontDestroyOnLoad(singletonObject);
                }
                return _instance;
            }
        }

        /// <summary>
        /// Raised after an alert dialog completes.
        /// </summary>
        /// <remarks>
        /// Parameters: result = the pressed button as a Win32 MessageBox return value (IDOK = 1 ...
        /// IDCONTINUE = 11), isSuccess = the dialog was shown and closed, errorCode = null on success.
        /// On failure, result is 0 when the native library failed, and null when no dialog was
        /// attempted (an argument was refused, the library is unavailable, or in the Editor).
        /// </remarks>
        public event Action<int?, bool, int?>? AlertDialogResult;

        /// <summary>
        /// Raised after a single-file open dialog completes.
        /// </summary>
        /// <remarks>
        /// filePath = selected path (null if cancelled or error), isCancelled = user cancelled, isSuccess = the dialog ran,
        /// errorCode = error code (null if success). When isCancelled is true, isSuccess remains true and errorCode is
        /// <see cref="WindowsDialogErrorCodes.Cancelled"/> to distinguish user intent from failure.
        /// </remarks>
        public event Action<string?, bool, bool, int?>? FileDialogResult;

        /// <summary>
        /// Raised after a multi-file open dialog completes.
        /// </summary>
        /// <remarks>
        /// filePaths = collection of fully qualified file paths, isCancelled = user cancelled selection,
        /// isSuccess = the dialog ran, errorCode = error code (null if success). A success with no selection
        /// reports an empty list. ArrayList is used for compatibility with existing code; consider migrating to List&lt;string&gt;.
        /// </remarks>
        public event Action<ArrayList?, bool, bool, int?>? MultiFileDialogResult;

        /// <summary>
        /// Raised after a save file dialog completes.
        /// </summary>
        /// <remarks>filePath = saved target path (null on cancel/error), isCancelled = user cancelled, isSuccess = the dialog ran, errorCode = error code.</remarks>
        public event Action<string?, bool, bool, int?>? SaveFileDialogResult;

        /// <summary>
        /// Raised after a single-folder selection dialog completes.
        /// </summary>
        public event Action<string?, bool, bool, int?>? FolderDialogResult;

        /// <summary>
        /// Raised after a multi-folder selection dialog completes.
        /// </summary>
        /// <remarks>folderPaths = selected folder paths; semantics mirror <see cref="MultiFileDialogResult"/>.</remarks>
        public event Action<ArrayList?, bool, bool, int?>? MultiFolderDialogResult;

        /// <summary>
        /// Initialize the singleton instance and ensure persistence across scene changes.
        /// </summary>
        private void Awake()
        {
            Debug.Log($"[{LogTag}][{nameof(Awake)}]");
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// Shows a Windows native message box style alert dialog.
        /// </summary>
        /// <param name="title">Dialog caption text. Must not be empty.</param>
        /// <param name="message">Message body. Must not be empty.</param>
        /// <param name="buttons">Flags determining which buttons to show.</param>
        /// <param name="icon">Icon style flags.</param>
        /// <param name="defbutton">Default button flag.</param>
        /// <param name="options">Additional option flags (<see cref="Win32MessageBox.MB_TOPMOST"/>, MB_HELP).</param>
        /// <remarks>
        /// The four flag arguments are ORed together, so a flag works whichever argument carries it; a
        /// null argument takes its default. Result is raised via <see cref="AlertDialogResult"/>. An empty
        /// title or message, or a flag the C ABI cannot express (see <see cref="Win32MessageBox"/>),
        /// shows nothing and reports <see cref="WindowsDialogErrorCodes.InvalidArgument"/>.
        /// </remarks>
        public void ShowDialog(
            string title,
            string message,
            uint? buttons = Win32MessageBox.MB_OK,
            uint? icon = Win32MessageBox.MB_ICONINFORMATION,
            uint? defbutton = Win32MessageBox.MB_DEFBUTTON1,
            uint? options = Win32MessageBox.MB_APPLMODAL
        )
        {
            Debug.Log($"[{LogTag}][{nameof(ShowDialog)}] title: {title}, message: {message}, buttons: {buttons}, icon: {icon}, defbutton: {defbutton}, options: {options}");

            if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(message))
            {
                Debug.LogWarning($"[{LogTag}][{nameof(ShowDialog)}] the title and the message must not be empty; no dialog is shown");
                AlertDialogResult?.Invoke(null, false, WindowsDialogErrorCodes.InvalidArgument);
                return;
            }

            uint style = WindowsDialogCApi.CombineAlertStyle(buttons, icon, defbutton, options);
            if (!WindowsDialogCApi.TryMapAlertStyle(style, out WindowsDialogCApi.AlertFlags flags))
            {
                Debug.LogWarning($"[{LogTag}][{nameof(ShowDialog)}] style 0x{style:X8} has a flag native-toolkit 2.0.0 cannot express " +
                    "(system or task modal, right-aligned or right-to-left text, or an undefined value); no dialog is shown");
                AlertDialogResult?.Invoke(null, false, WindowsDialogErrorCodes.InvalidArgument);
                return;
            }

            int? result;
            int? errorCode;
            try
            {
                int code = WindowsDialogCApi.ShowAlert(title, message, flags, out int alertResult, out uint systemCode);
                (result, errorCode) = AlertOutcome(code, alertResult, systemCode);
            }
            catch (Exception e)
            {
                result = null;
                errorCode = FromException(nameof(ShowDialog), e);
            }

            Debug.Log($"[{LogTag}][{nameof(ShowDialog)}] result: {result}, errorCode: {errorCode}");
            AlertDialogResult?.Invoke(result, errorCode == null, errorCode);
        }

        /// <summary>
        /// Shows a Windows native single file open dialog.
        /// </summary>
        /// <param name="buffer_size">Ignored since native-toolkit 2.0.0; kept for compatibility. A path is limited to 1023 characters.</param>
        /// <param name="filter">Filter specification string ("Description\0Pattern\0...\0\0"), read up to the first empty description.</param>
        /// <remarks>
        /// Emits <see cref="FileDialogResult"/>. On cancel errorCode is <see cref="WindowsDialogErrorCodes.Cancelled"/> but isSuccess remains true to indicate no failure.
        /// </remarks>
        public void ShowFileDialog(
            uint? buffer_size = 1024,
            string? filter = "All Files\0*.*\0\0"
        )
        {
            Debug.Log($"[{LogTag}][{nameof(ShowFileDialog)}] buffer_size: {buffer_size}, filter: {Printable(filter)}");

            string? path = null;
            if (!TryFilters(nameof(ShowFileDialog), filter, out List<KeyValuePair<string, string>> filters))
            {
                FileDialogResult?.Invoke(null, false, false, WindowsDialogErrorCodes.InvalidArgument);
                return;
            }

            bool isCancelled;
            int? errorCode;
            try
            {
                int code = WindowsDialogCApi.OpenFile(filters, out path, out uint systemCode);
                (isCancelled, errorCode) = FileOutcome(nameof(ShowFileDialog), "ntk_dialog_show_open_file", code, systemCode, Marshal.SizeOf<WindowsDialogCApi.FileRequest>());
            }
            catch (Exception e)
            {
                path = null;
                isCancelled = false;
                errorCode = FromException(nameof(ShowFileDialog), e);
            }

            Debug.Log($"[{LogTag}][{nameof(ShowFileDialog)}] filePath: {path}, isCancelled: {isCancelled}, errorCode: {errorCode}");
            FileDialogResult?.Invoke(path, isCancelled, IsSuccess(isCancelled, errorCode), errorCode);
        }

        /// <summary>
        /// Shows a Windows native multi-file open dialog.
        /// </summary>
        /// <param name="buffer_size">Ignored since native-toolkit 2.0.0; kept for compatibility. The list is limited to 32768 characters in total.</param>
        /// <param name="filter">Filter specification string (see <see cref="ShowFileDialog"/>).</param>
        /// <remarks>
        /// Emits <see cref="MultiFileDialogResult"/> with each selected file as a fully qualified path.
        /// </remarks>
        public void ShowMultiFileDialog(
            uint? buffer_size = 4096,
            string? filter = "All Files\0*.*\0\0"
        )
        {
            Debug.Log($"[{LogTag}][{nameof(ShowMultiFileDialog)}] buffer_size: {buffer_size}, filter: {Printable(filter)}");

            if (!TryFilters(nameof(ShowMultiFileDialog), filter, out List<KeyValuePair<string, string>> filters))
            {
                MultiFileDialogResult?.Invoke(null, false, false, WindowsDialogErrorCodes.InvalidArgument);
                return;
            }

            ArrayList? selectedFiles;
            bool isCancelled;
            int? errorCode;
            try
            {
                int code = WindowsDialogCApi.OpenFiles(filters, out List<string>? paths, out uint systemCode);
                (isCancelled, errorCode) = FileOutcome(nameof(ShowMultiFileDialog), "ntk_dialog_show_open_files", code, systemCode, Marshal.SizeOf<WindowsDialogCApi.FileRequest>());
                selectedFiles = paths == null ? null : new ArrayList(paths);
            }
            catch (Exception e)
            {
                selectedFiles = null;
                isCancelled = false;
                errorCode = FromException(nameof(ShowMultiFileDialog), e);
            }

            Debug.Log($"[{LogTag}][{nameof(ShowMultiFileDialog)}] count: {selectedFiles?.Count}, isCancelled: {isCancelled}, errorCode: {errorCode}");
            MultiFileDialogResult?.Invoke(selectedFiles, isCancelled, IsSuccess(isCancelled, errorCode), errorCode);
        }

        /// <summary>
        /// Shows a Windows native folder selection dialog.
        /// </summary>
        /// <param name="buffer_size">Ignored since native-toolkit 2.0.0; kept for compatibility. A path is limited to 1023 characters.</param>
        /// <param name="title">Dialog title text. An empty string shows the system's own title.</param>
        /// <remarks>Emits <see cref="FolderDialogResult"/>.</remarks>
        public void ShowFolderDialog(
            uint? buffer_size = 1024,
            string? title = "Select Folder"
        )
        {
            Debug.Log($"[{LogTag}][{nameof(ShowFolderDialog)}] buffer_size: {buffer_size}, title: {title}");

            string? path;
            bool isCancelled;
            int? errorCode;
            try
            {
                int code = WindowsDialogCApi.PickFolder(title ?? WindowsDialogCApi.DefaultFolderTitle, out path, out uint systemCode);
                (isCancelled, errorCode) = FileOutcome(nameof(ShowFolderDialog), "ntk_dialog_show_pick_folder", code, systemCode, Marshal.SizeOf<WindowsDialogCApi.FolderRequest>());
            }
            catch (Exception e)
            {
                path = null;
                isCancelled = false;
                errorCode = FromException(nameof(ShowFolderDialog), e);
            }

            Debug.Log($"[{LogTag}][{nameof(ShowFolderDialog)}] folderPath: {path}, isCancelled: {isCancelled}, errorCode: {errorCode}");
            FolderDialogResult?.Invoke(path, isCancelled, IsSuccess(isCancelled, errorCode), errorCode);
        }

        /// <summary>
        /// Shows a Windows native multi-folder selection dialog.
        /// </summary>
        /// <param name="buffer_size">Ignored since native-toolkit 2.0.0; kept for compatibility. The list is limited to 32768 characters in total.</param>
        /// <param name="title">Dialog title text. An empty string shows the system's own title.</param>
        /// <remarks>Emits <see cref="MultiFolderDialogResult"/>.</remarks>
        public void ShowMultiFolderDialog(
            uint? buffer_size = 4096,
            string? title = "Select Folders"
        )
        {
            Debug.Log($"[{LogTag}][{nameof(ShowMultiFolderDialog)}] buffer_size: {buffer_size}, title: {title}");

            ArrayList? selectedFolders;
            bool isCancelled;
            int? errorCode;
            try
            {
                int code = WindowsDialogCApi.PickFolders(title ?? WindowsDialogCApi.DefaultMultiFolderTitle, out List<string>? paths, out uint systemCode);
                (isCancelled, errorCode) = FileOutcome(nameof(ShowMultiFolderDialog), "ntk_dialog_show_pick_folders", code, systemCode, Marshal.SizeOf<WindowsDialogCApi.FolderRequest>());
                selectedFolders = paths == null ? null : new ArrayList(paths);
            }
            catch (Exception e)
            {
                selectedFolders = null;
                isCancelled = false;
                errorCode = FromException(nameof(ShowMultiFolderDialog), e);
            }

            Debug.Log($"[{LogTag}][{nameof(ShowMultiFolderDialog)}] count: {selectedFolders?.Count}, isCancelled: {isCancelled}, errorCode: {errorCode}");
            MultiFolderDialogResult?.Invoke(selectedFolders, isCancelled, IsSuccess(isCancelled, errorCode), errorCode);
        }

        /// <summary>
        /// Shows a Windows native save file dialog.
        /// </summary>
        /// <param name="buffer_size">Ignored since native-toolkit 2.0.0; kept for compatibility. A path is limited to 1023 characters.</param>
        /// <param name="filter">Filter specification string (see <see cref="ShowFileDialog"/>).</param>
        /// <param name="def_ext">Default extension (without leading dot).</param>
        /// <remarks>Emits <see cref="SaveFileDialogResult"/>. The dialog always asks before overwriting an existing file.</remarks>
        public void ShowSaveFileDialog(
            uint? buffer_size = 1024,
            string? filter = "All Files\0*.*\0\0",
            string? def_ext = "txt"
        )
        {
            Debug.Log($"[{LogTag}][{nameof(ShowSaveFileDialog)}] buffer_size: {buffer_size}, filter: {Printable(filter)}, def_ext: {def_ext}");

            if (!TryFilters(nameof(ShowSaveFileDialog), filter, out List<KeyValuePair<string, string>> filters))
            {
                SaveFileDialogResult?.Invoke(null, false, false, WindowsDialogErrorCodes.InvalidArgument);
                return;
            }

            string? path;
            bool isCancelled;
            int? errorCode;
            try
            {
                int code = WindowsDialogCApi.SaveFile(filters, def_ext ?? WindowsDialogCApi.DefaultSaveExtension, out path, out uint systemCode);
                (isCancelled, errorCode) = FileOutcome(nameof(ShowSaveFileDialog), "ntk_dialog_show_save_file", code, systemCode, Marshal.SizeOf<WindowsDialogCApi.SaveFileRequest>());
            }
            catch (Exception e)
            {
                path = null;
                isCancelled = false;
                errorCode = FromException(nameof(ShowSaveFileDialog), e);
            }

            Debug.Log($"[{LogTag}][{nameof(ShowSaveFileDialog)}] filePath: {path}, isCancelled: {isCancelled}, errorCode: {errorCode}");
            SaveFileDialogResult?.Invoke(path, isCancelled, IsSuccess(isCancelled, errorCode), errorCode);
        }

        // ── Results ─────────────────────────────────────────────────────────────

        private static bool IsSuccess(bool isCancelled, int? errorCode) => isCancelled || errorCode == null;

        private static bool TryFilters(string method, string? filter, out List<KeyValuePair<string, string>> filters)
        {
            if (WindowsDialogCApi.TryParseFilter(filter ?? WindowsDialogCApi.DefaultFilter, out filters)) return true;

            Debug.LogWarning($"[{LogTag}][{method}] filter \"{Printable(filter)}\" has a description with no pattern; no dialog is shown");
            return false;
        }

        /// <summary>The alert's (result, errorCode) for what <see cref="WindowsDialogCApi.ShowAlert"/> returned.</summary>
        private static (int? result, int? errorCode) AlertOutcome(int code, int alertResult, uint systemCode)
        {
            if (code < 0)
                return (null, Unavailable(nameof(ShowDialog), code));

            if (code != WindowsDialogCApi.ErrorNone)
                return (0, NativeFailure(nameof(ShowDialog), "ntk_dialog_show_alert", code, systemCode, Marshal.SizeOf<WindowsDialogCApi.AlertRequest>()));

            if (WindowsDialogCApi.TryToWin32Result(alertResult, out int win32Id))
                return (win32Id, null);

            Debug.LogWarning($"[{LogTag}][{nameof(ShowDialog)}] ntk_dialog_show_alert reported an undefined result {alertResult}");
            return (0, WindowsDialogErrorCodes.Unknown);
        }

        /// <summary>A file or folder dialog's (isCancelled, errorCode) for what the bridge returned.</summary>
        private static (bool isCancelled, int? errorCode) FileOutcome(string method, string function, int code, uint systemCode, int requestSize)
        {
            if (code == WindowsDialogCApi.ErrorNone)
                return (false, null);

            if (code == WindowsDialogCApi.ErrorCanceled)
            {
                Debug.Log($"[{LogTag}][{method}] cancelled");
                return (true, WindowsDialogErrorCodes.Cancelled);
            }

            if (code < 0)
                return (false, Unavailable(method, code));

            return (false, NativeFailure(method, function, code, systemCode, requestSize));
        }

        /// <summary>Logs a code the bridge returned without calling a dialog (-4 or -5) and passes it on.</summary>
        private static int Unavailable(string method, int code)
        {
            if (code == WindowsDialogErrorCodes.PlatformUnavailable)
                Debug.LogWarning($"[{LogTag}][{method}] not running in a Windows player; no dialog is shown");
            else
                Debug.LogWarning($"[{LogTag}][{method}] the native library is not available (errorCode {code}); no dialog is shown");
            return code;
        }

        /// <summary>Logs a failure the native library reported and returns its errorCode.</summary>
        private static int NativeFailure(string method, string function, int error, uint systemCode, int requestSize)
        {
            switch (error)
            {
                case WindowsDialogCApi.ErrorInvalidParameter:
                    Debug.LogWarning($"[{LogTag}][{method}] {function} rejected the request (struct_size {requestSize}); the C# structure may not match the C ABI");
                    break;
                case WindowsDialogCApi.ErrorCanceled:
                    Debug.LogWarning($"[{LogTag}][{method}] {function} reported an unexpected CANCELED");
                    break;
                default:
                    Debug.LogWarning($"[{LogTag}][{method}] {function} failed with error {error}, system code 0x{systemCode:X8}");
                    break;
            }
            return WindowsDialogCApi.FailureCode(error, systemCode);
        }

        /// <summary>Logs an exception thrown while calling the native library and returns its errorCode.</summary>
        private static int FromException(string method, Exception exception)
        {
            int code = WindowsDialogCApi.FromException(exception);
            if (code == WindowsDialogErrorCodes.NativeUnavailable)
                Debug.LogWarning($"[{LogTag}][{method}] the native library could not be used: {exception.GetType().Name}: {exception.Message}");
            else
                Debug.LogError($"[{LogTag}][{method}] {exception.GetType().Name}: {exception.Message}");
            return code;
        }

        /// <summary>A filter string with its NULs shown as \0, for the log.</summary>
        private static string? Printable(string? filter) => filter?.Replace("\0", "\\0");
    }
}
#endif
