#nullable enable

#if UNITY_STANDALONE_WIN
using UnityEngine;
using System.Runtime.InteropServices;
using System;
using System.Text;
using System.Collections;

/// <summary>
/// Singleton manager for Windows native dialog operations using Unity's native plugin interface.
/// Provides a Unity-friendly API for showing various types of Windows native dialogs including
/// alert dialogs, file selection dialogs, folder selection dialogs, and save dialogs.
/// Uses P/Invoke to communicate with Win32 native code and event-driven callbacks for results.
/// </summary>
public class WindowsDialogManager : MonoBehaviour
{
    private static WindowsDialogManager _instance;

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
                Debug.Log("Creating new instance of WindowsDialogManager");
                GameObject singletonObject = new GameObject("WindowsDialogManager");
                _instance = singletonObject.AddComponent<WindowsDialogManager>();
                DontDestroyOnLoad(singletonObject);
            }
            return _instance;
        }
    }

    // Event handlers (all invoke on the Unity main thread because they're called synchronously here, but
    // keep usage consistent across platforms in case of future async refactors).
    /// <summary>
    /// Raised after an alert dialog completes.
    /// </summary>
    /// <remarks>
    /// Parameters: result = pressed button identifier (Win32 MessageBox return), isSuccess = native call succeeded,
    /// errorCode = Win32/GetLastError style error code (null if success).
    /// </remarks>
    public event Action<int?, bool, int?>? AlertDialogResult;                    

    /// <summary>
    /// Raised after a single-file open dialog completes.
    /// </summary>
    /// <remarks>
    /// filePath = selected path (null if cancelled or error), isCancelled = user cancelled, isSuccess = native call executed,
    /// errorCode = error code (null if success). When isCancelled is true, isSuccess remains true to distinguish user intent from failure.
    /// </remarks>
    public event Action<string?, bool, bool, int?>? FileDialogResult;            

    /// <summary>
    /// Raised after a multi-file open dialog completes.
    /// </summary>
    /// <remarks>
    /// filePaths = collection of fully qualified file paths, isCancelled = user cancelled selection,
    /// isSuccess = native call executed, errorCode = error code (null if success). ArrayList is used for compatibility with existing code; consider migrating to List&lt;string&gt;.
    /// </remarks>
    public event Action<ArrayList?, bool, bool, int?>? MultiFileDialogResult;    

    /// <summary>
    /// Raised after a save file dialog completes.
    /// </summary>
    /// <remarks>filePath = saved target path (null on cancel/error), isCancelled = user cancelled, isSuccess = native call executed, errorCode = error code.</remarks>
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
        Debug.Log("Awake");
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
    /// Native P/Invoke for displaying a Windows message box (alert dialog).
    /// </summary>
    /// <param name="title">Caption text of the dialog window.</param>
    /// <param name="message">Body text displayed in the dialog.</param>
    /// <param name="buttons">Bitmask / flags indicating which buttons to show (maps to Win32 style flags).</param>
    /// <param name="icon">Icon style flags (e.g., information, warning).</param>
    /// <param name="defbutton">Default button flag determining initial focus.</param>
    /// <param name="options">Additional option flags (top-most, etc.).</param>
    /// <param name="pError">Outputs 0 on success, -1 on cancel (if defined by implementation), or another error code.</param>
    /// <returns>Win32 MessageBox style result indicating which button was pressed.</returns>
    [DllImport("WindowsLibrary.dll", CharSet = CharSet.Unicode)]
    private static extern int showAlertDialog(
        [MarshalAs(UnmanagedType.LPWStr)] string title,
        [MarshalAs(UnmanagedType.LPWStr)] string message,
        uint buttons,
        uint icon,
        uint defbutton,
        uint options,
        out int pError
    );

    /// <summary>
    /// Native P/Invoke for single file selection using Windows common dialog (OPENFILENAME or similar implementation).
    /// </summary>
    /// <param name="buffer">Receives the selected file path (UTF-16).</param>
    /// <param name="buffer_size">Buffer size in WCHAR units (NOT bytes).</param>
    /// <param name="filter">Filter string in Win32 format: "Description\0Pattern\0...\0\0".</param>
    /// <param name="pError">0 success, -1 cancelled, other value = failure.</param>
    /// <returns>true if the native dialog executed (even if cancelled), false on internal failure.</returns>
    [DllImport("WindowsLibrary.dll", CharSet = CharSet.Unicode)]
    private static extern bool showFileDialog(
        [MarshalAs(UnmanagedType.LPWStr)] StringBuilder buffer,
        uint buffer_size,
        [MarshalAs(UnmanagedType.LPWStr)] string filter,
        out int pError
    );

    /// <summary>
    /// Native P/Invoke for multi-file selection. Caller provides unmanaged buffer for performance and manual parsing.
    /// </summary>
    /// <param name="buffer">Unmanaged memory receiving a double-null terminated list. First segment may be directory path.</param>
    /// <param name="buffer_size">Size of the buffer in WCHAR units.</param>
    /// <param name="filter">Filter string (see <see cref="showFileDialog"/>).</param>
    /// <param name="pError">0 success, -1 cancelled, other value = failure.</param>
    /// <returns>Number of selected items; interpretation depends on implementation (first element may be folder).</returns>
    [DllImport("WindowsLibrary.dll", CharSet = CharSet.Unicode)]
    private static extern int showMultiFileDialog(
        IntPtr buffer,
        uint buffer_size,
        [MarshalAs(UnmanagedType.LPWStr)] string filter,
        out int pError
    );

    /// <summary>
    /// Native P/Invoke for save file dialog.
    /// </summary>
    [DllImport("WindowsLibrary.dll", CharSet = CharSet.Unicode)]
    private static extern bool showSaveFileDialog(
        [MarshalAs(UnmanagedType.LPWStr)] StringBuilder buffer,
        uint buffer_size,
        [MarshalAs(UnmanagedType.LPWStr)] string filter,
        [MarshalAs(UnmanagedType.LPWStr)] string def_ext,
        out int pError
    );

    /// <summary>
    /// Native P/Invoke for single folder selection dialog.
    /// </summary>
    [DllImport("WindowsLibrary.dll", CharSet = CharSet.Unicode)]
    private static extern bool showFolderDialog(
        [MarshalAs(UnmanagedType.LPWStr)] StringBuilder buffer,
        uint buffer_size,
        [MarshalAs(UnmanagedType.LPWStr)] string title,
        out int pError
    );

    /// <summary>
    /// Native P/Invoke for multi-folder selection dialog.
    /// </summary>
    [DllImport("WindowsLibrary.dll", CharSet = CharSet.Unicode)]
    private static extern int showMultiFolderDialog(
        IntPtr buffer,
        uint buffer_size,
        [MarshalAs(UnmanagedType.LPWStr)] string title,
        out int pError
    );

    /// <summary>
    /// Shows a Windows native message box style alert dialog.
    /// </summary>
    /// <param name="title">Dialog caption text.</param>
    /// <param name="message">Message body.</param>
    /// <param name="buttons">Flags determining which buttons to show.</param>
    /// <param name="icon">Icon style flags.</param>
    /// <param name="defbutton">Default button flag.</param>
    /// <param name="options">Additional option flags (e.g., modality, top-most).</param>
    /// <remarks>Result is raised via <see cref="AlertDialogResult"/>; errors provide non-null error codes.</remarks>
    public void ShowDialog(
        string title,
        string message,
        uint buttons,
        uint icon,
        uint defbutton,
        uint options
    )
    {
    Debug.Log("ShowDialog called with title: " + title + ", message: " + message + ", buttons: " + buttons + ", icon: " + icon + ", defbutton: " + defbutton + ", options: " + options);
        int result = showAlertDialog(
            title,
            message,
            buttons,
            icon,
            defbutton,
            options,
            out int errorCode
        );
        Debug.Log($"ShowDialog returned result: {result}, error code: 0x{errorCode:X8}");
        if (errorCode == 0)
        {
            Debug.Log($"ShowDialog succeeded with result: {result}, error code: 0x{errorCode:X8}");
            AlertDialogResult?.Invoke(result, true, null);
        }
        else
        {
            Debug.LogError($"ShowDialog failed with result: {result}, error code: 0x{errorCode:X8}");
            AlertDialogResult?.Invoke(result, false, errorCode);
        }
    }

    /// <summary>
    /// Shows a Windows native single file open dialog.
    /// </summary>
    /// <param name="buffer_size">Size of the internal selection buffer in WCHAR units (recommend >= 260 for typical paths).</param>
    /// <param name="filter">Filter specification string ("Description\0Pattern\0...\0\0").</param>
    /// <remarks>
    /// Emits <see cref="FileDialogResult"/>. On cancel errorCode is -1 but isSuccess remains true to indicate no failure.
    /// </remarks>
    public void ShowFileDialog(
        uint buffer_size,
        string filter
    )
    {
        Debug.Log("ShowFileDialog called with " +
            "buffer size: " + buffer_size +
            ", filter: " + filter
        );

        var buffer = new StringBuilder((int)buffer_size * 2);
        // Single file selection
        bool result = showFileDialog(
            buffer,
            buffer_size,
            filter,
            out int errorCode);
        Debug.Log($"ShowFileDialog returned result: {result}, error code: 0x{errorCode:X8}");
        if (errorCode == 0)
        {
            Debug.Log($"ShowFileDialog selected file: {buffer}, error code: 0x{errorCode:X8}");
            FileDialogResult?.Invoke(buffer.ToString(), false, true, null);
        }
        else if (errorCode == -1)
        {
            Debug.Log($"ShowFileDialog selection cancelled. error code: 0x{errorCode:X8}");
            FileDialogResult?.Invoke(null, true, true, errorCode);
        }
        else
        {
            Debug.LogError($"ShowFileDialog error occurred. error code: 0x{errorCode:X8}");
            FileDialogResult?.Invoke(null, false, false, errorCode);
        }
    }

    /// <summary>
    /// Shows a Windows native multi-file open dialog.
    /// </summary>
    /// <param name="buffer_size">Size of unmanaged buffer in WCHAR units used to receive selection list (double-null terminated).</param>
    /// <param name="filter">Filter specification string.</param>
    /// <remarks>
    /// The buffer is manually parsed: when multiple files are chosen, first entry is the directory; subsequent entries are file names.
    /// Emits <see cref="MultiFileDialogResult"/>. Memory is always freed via try/finally.
    /// </remarks>
    public void ShowMultiFileDialog(
        uint buffer_size,
        string filter
    )
    {
        Debug.Log("ShowMultiFileDialog called with " +
            "buffer size: " + buffer_size +
            ", filter: " + filter
        );

        // Allocate unmanaged buffer sized in WCHAR units * 2 bytes per char
        IntPtr unmanagedBuffer = Marshal.AllocHGlobal((int)buffer_size * 2);
        try
        {
            // Perform multi-file selection
            int count = showMultiFileDialog(
                unmanagedBuffer,
                buffer_size,
                filter,
                out int errorCode
            );
            Debug.Log($"ShowMultiFileDialog returned count: {count}, error code: 0x{errorCode:X8}");
            if (errorCode == 0)
            {
                // Copy unmanaged buffer into managed byte[]
                byte[] raw = new byte[buffer_size * 2];
                Marshal.Copy(unmanagedBuffer, raw, 0, raw.Length);

                // Decode as UTF-16 (Unicode) string containing null separators
                string all = System.Text.Encoding.Unicode.GetString(raw);
                string[] parts = all.Split('\0');
                Debug.Log("ShowMultiFileDialog Raw buffer bytes: " + BitConverter.ToString(raw));
                Debug.Log($"ShowMultiFileDialog buffer string: {all}");

                Debug.Log("ShowMultiFileDialog parts.Length: " + parts.Length);
                for (int i = 0; i < parts.Length; i++)
                {
                    Debug.Log($"ShowMultiFileDialog parts[{i}]: {parts[i]}");
                }

                if (count == 1)
                {
                    Debug.Log("ShowMultiFileDialog selected file: " + parts[0]);
                    ArrayList selectedFiles = new ArrayList { parts[0] };
                    MultiFileDialogResult?.Invoke(selectedFiles, false, true, null);
                }
                else
                {
                    string folder = parts[0];
                    Debug.Log("ShowMultiFileDialog selected files folder (0): " + parts[0]);
                    ArrayList selectedFiles = new ArrayList();
                    for (int i = 1; i < count; i++)
                    {
                        Debug.Log($"ShowMultiFileDialog selected file({i}): {folder}\\{parts[i]}");
                        selectedFiles.Add(folder + "\\" + parts[i]);
                    }
                    MultiFileDialogResult?.Invoke(selectedFiles, false, true, null);
                }
            }
            else if (errorCode == -1)
            {
                Debug.Log($"ShowMultiFileDialog selection cancelled. error code: 0x{errorCode:X8}");
                MultiFileDialogResult?.Invoke(null, true, true, errorCode);
            }
            else
            {
                Debug.LogError($"ShowMultiFileDialog error occurred. error code: 0x{errorCode:X8}");
                MultiFileDialogResult?.Invoke(null, false, false, errorCode);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(unmanagedBuffer);
        }
    }

    /// <summary>
    /// Shows a Windows native folder selection dialog.
    /// </summary>
    /// <param name="buffer_size">Buffer size in WCHAR units for the resulting path.</param>
    /// <param name="title">Dialog title text.</param>
    /// <remarks>Emits <see cref="FolderDialogResult"/>.</remarks>
    public void ShowFolderDialog(
        uint buffer_size,
        string title
    )
    {
        Debug.Log("ShowFolderDialog called with " +
            "buffer size: " + buffer_size +
            ", title: " + title
        );

        var buffer = new StringBuilder((int)buffer_size * 2);
        // Folder selection
        bool result = showFolderDialog(
            buffer,
            buffer_size,
            title,
            out int errorCode
        );
        Debug.Log($"ShowFolderDialog returned result: {result}, error code: 0x{errorCode:X8}");
        if (errorCode == 0)
        {
            Debug.Log($"ShowFolderDialog selected folder: {buffer}, error code: 0x{errorCode:X8}");
            FolderDialogResult?.Invoke(buffer.ToString(), false, true, null);
        }
        else if (errorCode == -1)
        {
            Debug.Log($"ShowFolderDialog selection cancelled. error code: 0x{errorCode:X8}");
            FolderDialogResult?.Invoke(null, true, true, errorCode);
        }
        else
        {
            Debug.LogError($"ShowFolderDialog error occurred. error code: 0x{errorCode:X8}");
            FolderDialogResult?.Invoke(null, false, false, errorCode);
        }
    }

    /// <summary>
    /// Shows a Windows native multi-folder selection dialog.
    /// </summary>
    /// <param name="buffer_size">Unmanaged buffer size in WCHAR units.</param>
    /// <param name="title">Dialog title text.</param>
    /// <remarks>
    /// Parses a double-null terminated UTF-16 list. Each element is a folder path. Emits <see cref="MultiFolderDialogResult"/>.
    /// </remarks>
    public void ShowMultiFolderDialog(
        uint buffer_size,
        string title
    )
    {
        Debug.Log("ShowMultiFolderDialog called with " +
            "buffer size: " + buffer_size +
            ", title: " + title
        );

        // Allocate unmanaged buffer sized in WCHAR units * 2 bytes per char
        IntPtr unmanagedBuffer = Marshal.AllocHGlobal((int)buffer_size * 2);
        try
        {
            // Perform multi-folder selection
            int count = showMultiFolderDialog(
                unmanagedBuffer,
                buffer_size,
                title,
                out int errorCode
            );
            Debug.Log($"ShowMultiFolderDialog returned count: {count}, error code: 0x{errorCode:X8}");
            if (errorCode == 0)
            {
                // Copy unmanaged buffer into managed byte[]
                byte[] raw = new byte[buffer_size * 2];
                Marshal.Copy(unmanagedBuffer, raw, 0, raw.Length);

                // Decode as UTF-16 (Unicode) string containing null separators
                string all = System.Text.Encoding.Unicode.GetString(raw);
                string[] parts = all.Split('\0');
                Debug.Log("ShowMultiFolderDialog Raw buffer bytes: " + BitConverter.ToString(raw));
                Debug.Log($"ShowMultiFolderDialog buffer string: {all}");

                Debug.Log("ShowMultiFolderDialog parts.Length: " + parts.Length);
                for (int i = 0; i < parts.Length; i++)
                {
                    Debug.Log($"ShowMultiFolderDialog parts[{i}]: {parts[i]}");
                }

                // Build folder path list
                ArrayList selectedFolders = new ArrayList();
                for (int i = 0; i < count; i++)
                {
                    if (!string.IsNullOrEmpty(parts[i]))
                    {
                        Debug.Log($"ShowMultiFolderDialog selected folder({i}): {parts[i]}");
                        selectedFolders.Add(parts[i]);
                    }
                }
                MultiFolderDialogResult?.Invoke(selectedFolders, false, true, null);
            }
            else if (errorCode == -1)
            {
                Debug.Log($"ShowMultiFolderDialog selection cancelled. error code: 0x{errorCode:X8}");
                MultiFolderDialogResult?.Invoke(null, true, true, errorCode);
            }
            else
            {
                Debug.LogError($"ShowMultiFolderDialog error occurred. error code: 0x{errorCode:X8}");
                MultiFolderDialogResult?.Invoke(null, false, false, errorCode);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(unmanagedBuffer);
        }
    }
    
    /// <summary>
    /// Shows a Windows native save file dialog.
    /// </summary>
    /// <param name="buffer_size">Buffer size in WCHAR units for the resulting path.</param>
    /// <param name="filter">Filter specification string.</param>
    /// <param name="def_ext">Default extension (without leading dot).</param>
    /// <remarks>Emits <see cref="SaveFileDialogResult"/>.</remarks>
    public void ShowSaveFileDialog(
        uint buffer_size,
        string filter,
        string def_ext
    )
    {
        Debug.Log("ShowSaveFileDialog called with " +
            "buffer size: " + buffer_size +
            ", filter: " + filter +
            ", default extension: " + def_ext
        );

        var buffer = new StringBuilder((int)buffer_size * 2);
        // Save file selection
        bool result = showSaveFileDialog(
            buffer,
            buffer_size,
            filter,
            def_ext,
            out int errorCode
        );
        Debug.Log($"ShowSaveFileDialog returned result: {result}, error code: 0x{errorCode:X8}");
        if (errorCode == 0)
        {
            Debug.Log($"ShowSaveFileDialog saved file: {buffer}, error code: 0x{errorCode:X8}");
            SaveFileDialogResult?.Invoke(buffer.ToString(), false, true, null);
        }
        else if (errorCode == -1)
        {
            Debug.Log($"ShowSaveFileDialog selection cancelled. error code: 0x{errorCode:X8}");
            SaveFileDialogResult?.Invoke(null, true, true, errorCode);
        }
        else
        {
            Debug.LogError($"ShowSaveFileDialog error occurred. error code: 0x{errorCode:X8}");
            SaveFileDialogResult?.Invoke(null, false, false, errorCode);
        }
    }
}
#endif
