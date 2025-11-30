#nullable enable

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using JonghyunKim.NativeToolkit.Runtime.Dialog;
#endif

/// <summary>
/// Controller for the Windows native dialog manager example UI.
/// Provides functionality to demonstrate various types of Windows native dialogs including
/// alert dialogs, file/folder selection dialogs, and save dialogs.
/// Handles Windows-specific file system interactions and native dialog integration.
/// </summary>
public class WindowsDialogManagerExampleController : MonoBehaviour
{
    [SerializeField] private UIDocument? uiDocument;

    // UI element references
    private Label? _resultLabel;
    private Button? _btnAlert;
    private Button? _btnFile;
    private Button? _btnMultiFile;
    private Button? _btnFolder;
    private Button? _btnMultiFolder;
    private Button? _btnSaveFile;

    /// <summary>
    /// Initialize component and platform-specific behavior on Awake.
    /// Shows a simulation dialog in Editor mode and validates platform compatibility.
    /// </summary>
    private void Awake()
    {
#if UNITY_EDITOR
        Debug.Log("Running in Unity Editor - Windows simulation mode");
        UnityEditor.EditorUtility.DisplayDialog(
            "WindowsDialogManager Example",
            "This is a simulation of the Windows dialog manager.\nAll events will not be triggered.\nRun in Windows player for full functionality.",
            "OK");
#elif UNITY_STANDALONE_WIN
        Debug.Log("Running on Windows player");
#else
        Debug.LogWarning("WindowsDialogManagerExampleController is only supported on Windows Standalone or Editor.");
        gameObject.SetActive(false);
        return;
#endif
        Debug.Log("[WindowsDialogManagerExampleController] initialized.");
    }

    /// <summary>
    /// Initialize UI elements and set up event handlers on Start.
    /// Configures button references and registers click events for Windows dialog operations.
    /// </summary>
    private void Start()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        if (uiDocument == null)
        {
            Debug.LogError("[WindowsDialogManagerExampleController] UIDocument component not found!");
            return;
        }

        InitializeUI();
        Debug.Log("[WindowsDialogManagerExampleController] Initialized successfully");
    }

    /// <summary>
    /// Resolves UI Toolkit element references, validates required elements, wires button click callbacks,
    /// and (in a Windows standalone player) subscribes to native dialog result events from <see cref="WindowsDialogManager"/>.
    /// </summary>
    /// <remarks>
    /// In the Unity Editor native dialogs are simulated and result events do not fire; clicking buttons only logs intent.
    /// </remarks>
    private void InitializeUI()
    {
        var root = uiDocument?.rootVisualElement;
        if (root == null)
        {
            Debug.LogError("[WindowsDialogManagerExampleController] rootVisualElement is null!");
            return;
        }

        _resultLabel = root.Q<Label>("ResultTextBlock");
        _btnAlert = root.Q<Button>("ShowDialogButton");
        _btnFile = root.Q<Button>("ShowFileDialogButton");
        _btnMultiFile = root.Q<Button>("ShowMultiFileDialogButton");
        _btnFolder = root.Q<Button>("ShowFolderDialogButton");
        _btnMultiFolder = root.Q<Button>("ShowMultiFolderDialogButton");
        _btnSaveFile = root.Q<Button>("ShowSaveFileDialogButton");

        if (_btnAlert == null || _btnFile == null || _btnMultiFile == null || _btnFolder == null || _btnMultiFolder == null || _resultLabel == null || _btnSaveFile == null)
        {
            Debug.LogError("[WindowsDialogManagerExampleController] One or more UI elements are missing in UXML.");
            return;
        }

        // Wire up clicks
        if (_btnAlert != null) _btnAlert.clicked += OnShowDialogClicked;
        if (_btnFile != null) _btnFile.clicked += OnShowFileDialogClicked;
        if (_btnMultiFile != null) _btnMultiFile.clicked += OnShowMultiFileDialogClicked;
        if (_btnFolder != null) _btnFolder.clicked += OnShowFolderDialogClicked;
        if (_btnMultiFolder != null) _btnMultiFolder.clicked += OnShowMultiFolderDialogClicked;
        if (_btnSaveFile != null) _btnSaveFile.clicked += OnShowSaveFileDialogClicked;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        WindowsDialogManager.Instance.AlertDialogResult += OnAlertDialogResult;
        WindowsDialogManager.Instance.FileDialogResult += OnFileDialogResult;
        WindowsDialogManager.Instance.MultiFileDialogResult += OnMultiFileDialogResult;
        WindowsDialogManager.Instance.FolderDialogResult += OnFolderDialogResult;
        WindowsDialogManager.Instance.MultiFolderDialogResult += OnMultiFolderDialogResult;
        WindowsDialogManager.Instance.SaveFileDialogResult += OnSaveFileDialogResult;
#endif
    }

    /// <summary>
    /// Unsubscribes all button callbacks and native dialog result events to prevent memory leaks or duplicate
    /// callbacks after scene unload or domain reload.
    /// </summary>
    private void OnDestroy()
    {
        if (_btnAlert != null) _btnAlert.clicked -= OnShowDialogClicked;
        if (_btnFile != null) _btnFile.clicked -= OnShowFileDialogClicked;
        if (_btnMultiFile != null) _btnMultiFile.clicked -= OnShowMultiFileDialogClicked;
        if (_btnFolder != null) _btnFolder.clicked -= OnShowFolderDialogClicked;
        if (_btnMultiFolder != null) _btnMultiFolder.clicked -= OnShowMultiFolderDialogClicked;
        if (_btnSaveFile != null) _btnSaveFile.clicked -= OnShowSaveFileDialogClicked;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        WindowsDialogManager.Instance.AlertDialogResult -= OnAlertDialogResult;
        WindowsDialogManager.Instance.FileDialogResult -= OnFileDialogResult;
        WindowsDialogManager.Instance.MultiFileDialogResult -= OnMultiFileDialogResult;
        WindowsDialogManager.Instance.FolderDialogResult -= OnFolderDialogResult;
        WindowsDialogManager.Instance.MultiFolderDialogResult -= OnMultiFolderDialogResult;
        WindowsDialogManager.Instance.SaveFileDialogResult -= OnSaveFileDialogResult;
#endif
        Debug.Log("[WindowsDialogManagerExampleController] Destroyed and unsubscribed from all events.");
    }

    // Button actions
    /// <summary>
    /// Displays a native Windows MessageBox with OK / Cancel buttons, information icon, defaulting to the second button.
    /// </summary>
    /// <remarks>
    /// The result (pressed button) and any error code are returned via <see cref="OnAlertDialogResult"/>.
    /// </remarks>
    private void OnShowDialogClicked()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        string title = "Native Windows Dialog";
        string message = "This is a native Windows dialog!";
        uint buttons = Win32MessageBox.MB_OKCANCEL;
        uint icon = Win32MessageBox.MB_ICONINFORMATION;
        uint defbutton = Win32MessageBox.MB_DEFBUTTON2;
        uint options = Win32MessageBox.MB_APPLMODAL;
        WindowsDialogManager.Instance.ShowDialog(
            title,
            message,
            buttons,
            icon,
            defbutton,
            options
        );
#endif
    }

    /// <summary>
    /// Opens an Open File dialog allowing the user to pick a single file with optional filter.
    /// </summary>
    /// <remarks>
    /// A double-null terminated filter string is used (e.g. "Text Files\0*.txt\0All Files\0*.*\0\0").
    /// Result delivered via <see cref="OnFileDialogResult"/>.
    /// </remarks>
    private void OnShowFileDialogClicked()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        uint bufferSize = 1024;
        string filter = "All Files\0*.*\0\0";
        WindowsDialogManager.Instance.ShowFileDialog(bufferSize, filter);
#endif
    }

    /// <summary>
    /// Opens an Open File dialog permitting multiple file selection.
    /// </summary>
    /// <remarks>Result delivered via <see cref="OnMultiFileDialogResult"/> as an ArrayList of paths.</remarks>
    private void OnShowMultiFileDialogClicked()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        uint bufferSize = 4096;
        string filter = "All Files\0*.*\0\0";
        WindowsDialogManager.Instance.ShowMultiFileDialog(bufferSize, filter);
#endif
    }

    /// <summary>
    /// Opens a single folder selection dialog using the Windows shell API.
    /// </summary>
    /// <remarks>Result delivered via <see cref="OnFolderDialogResult"/>.</remarks>
    private void OnShowFolderDialogClicked()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        uint bufferSize = 1024;
        string title = "Select Folder";
        WindowsDialogManager.Instance.ShowFolderDialog(bufferSize, title);
#endif
    }

    /// <summary>
    /// Opens a multi-folder selection dialog (if supported) allowing selection of multiple directories.
    /// </summary>
    /// <remarks>Result delivered via <see cref="OnMultiFolderDialogResult"/>.</remarks>
    private void OnShowMultiFolderDialogClicked()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        uint bufferSize = 4096;
        string title = "Select Folders";
        WindowsDialogManager.Instance.ShowMultiFolderDialog(bufferSize, title);
#endif
    }

    /// <summary>
    /// Opens a Save File dialog pre-populated with a default extension and filter list.
    /// </summary>
    /// <remarks>Result delivered via <see cref="OnSaveFileDialogResult"/>.</remarks>
    private void OnShowSaveFileDialogClicked()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        uint bufferSize = 1024;
        string filter = "All Files\0*.*\0\0";
        string defExt = "txt";
        WindowsDialogManager.Instance.ShowSaveFileDialog(bufferSize, filter, defExt);
#endif
    }

    // Event handlers (unsubscribe after one response to avoid duplicates)
    /// <summary>
    /// Callback for a completed Windows MessageBox dialog.
    /// </summary>
    /// <param name="result">The raw Win32 MessageBox result code (e.g. IDOK / IDCANCEL) or null on failure.</param>
    /// <param name="isSuccess">True if the dialog call returned successfully.</param>
    /// <param name="errorCode">Win32 error code when <paramref name="isSuccess"/> is false.</param>
    private void OnAlertDialogResult(int? result, bool isSuccess, int? errorCode)
    {
        Debug.Log($"[WindowsDialogManagerExampleController] OnAlertDialogResult -> result: {(result.HasValue ? result.Value.ToString() : "null")}, isSuccess: {isSuccess}, errorCode: {(errorCode.HasValue ? errorCode.Value.ToString() : "null")}");
        if (_resultLabel != null)
        {
            _resultLabel.text = (isSuccess ? "OK" : "Error") +
                                $"\nShowDialog result: {(result.HasValue ? result.Value.ToString() : "null")}, isSuccess: {isSuccess}, errorCode: {(errorCode.HasValue ? errorCode.Value.ToString() : "null")}";
        }
        else
        {
            Debug.LogError("[WindowsDialogManagerExampleController] Result label is null!");
        }
    }

    /// <summary>
    /// Callback for a single file selection dialog completion.
    /// </summary>
    /// <param name="filePath">Selected file path or null if cancelled/failure.</param>
    /// <param name="isCancelled">True if the user cancelled the dialog.</param>
    /// <param name="isSuccess">True if the native API succeeded.</param>
    /// <param name="errorCode">Win32 error code when <paramref name=\"isSuccess\"/> is false.</param>
    private void OnFileDialogResult(string? filePath, bool isCancelled, bool isSuccess, int? errorCode)
    {
        Debug.Log($"[WindowsDialogManagerExampleController] OnFileDialogResult -> filePath: {filePath ?? "null"}, isCancelled: {isCancelled}, isSuccess: {isSuccess}, errorCode: {(errorCode.HasValue ? errorCode.Value.ToString() : "null")}");
        if (_resultLabel != null)
        {
            _resultLabel.text = (isSuccess ? "OK" : "Error") +
                                $"\nShowFileDialog filePath: {filePath ?? "null"}, isCancelled: {isCancelled}, isSuccess: {isSuccess}, errorCode: {(errorCode.HasValue ? errorCode.Value.ToString() : "null")}";
        }
        else
        {
            Debug.LogError("[WindowsDialogManagerExampleController] Result label is null!");
        }
    }

    /// <summary>
    /// Callback for a multi-file selection dialog completion.
    /// </summary>
    /// <param name="filePaths">Collection of selected file paths (ArrayList for heterogeneity with P/Invoke marshalling) or null.</param>
    /// <param name="isCancelled">True if the user cancelled.</param>
    /// <param name="isSuccess">True if native call succeeded.</param>
    /// <param name="errorCode">Win32 error code when <paramref name=\"isSuccess\"/> is false.</param>
    private void OnMultiFileDialogResult(ArrayList? filePaths, bool isCancelled, bool isSuccess, int? errorCode)
    {
        var list = filePaths == null ? "null" : string.Join(", ", filePaths.ToArray());
        Debug.Log($"[WindowsDialogManagerExampleController] OnMultiFileDialogResult -> filePaths: {list}, isCancelled: {isCancelled}, isSuccess: {isSuccess}, errorCode: {(errorCode.HasValue ? errorCode.Value.ToString() : "null")}");
        if (_resultLabel != null)
        {
            _resultLabel.text = (isSuccess ? "OK" : "Error") +
                                $"\nShowMultiFileDialog filePaths: {list}, isCancelled: {isCancelled}, isSuccess: {isSuccess}, errorCode: {(errorCode.HasValue ? errorCode.Value.ToString() : "null")}";
        }
        else
        {
            Debug.LogError("[WindowsDialogManagerExampleController] Result label is null!");
        }
    }

    /// <summary>
    /// Callback for a single folder selection dialog completion.
    /// </summary>
    /// <param name="folderPath">Selected folder path or null if cancelled/failure.</param>
    /// <param name="isCancelled">True if the user cancelled.</param>
    /// <param name="isSuccess">True if native call succeeded.</param>
    /// <param name="errorCode">Win32 error code when <paramref name=\"isSuccess\"/> is false.</param>
    private void OnFolderDialogResult(string? folderPath, bool isCancelled, bool isSuccess, int? errorCode)
    {
        Debug.Log($"[WindowsDialogManagerExampleController] OnFolderDialogResult -> folderPath: {folderPath ?? "null"}, isCancelled: {isCancelled}, isSuccess: {isSuccess}, errorCode: {(errorCode.HasValue ? errorCode.Value.ToString() : "null")}");
        if (_resultLabel != null)
        {
            _resultLabel.text = (isSuccess ? "OK" : "Error") +
                                $"\nShowFolderDialog folderPath: {folderPath ?? "null"}, isCancelled: {isCancelled}, isSuccess: {isSuccess}, errorCode: {(errorCode.HasValue ? errorCode.Value.ToString() : "null")}";
        }
        else
        {
            Debug.LogError("[WindowsDialogManagerExampleController] Result label is null!");
        }
    }

    /// <summary>
    /// Callback for a multi-folder selection dialog completion.
    /// </summary>
    /// <param name="folderPaths">Collection of selected folder paths or null on failure.</param>
    /// <param name="isCancelled">True if the operation was cancelled.</param>
    /// <param name="isSuccess">True if native call succeeded.</param>
    /// <param name="errorCode">Win32 error code when <paramref name=\"isSuccess\"/> is false.</param>
    private void OnMultiFolderDialogResult(ArrayList? folderPaths, bool isCancelled, bool isSuccess, int? errorCode)
    {
        var list = folderPaths == null ? "null" : string.Join(", ", folderPaths.ToArray());
        Debug.Log($"[WindowsDialogManagerExampleController] OnMultiFolderDialogResult -> folderPaths: {list}, isCancelled: {isCancelled}, isSuccess: {isSuccess}, errorCode: {(errorCode.HasValue ? errorCode.Value.ToString() : "null")}");
        if (_resultLabel != null)
        {
            _resultLabel.text = (isSuccess ? "OK" : "Error") +
                                $"\nShowMultiFolderDialog folderPaths: {list}, isCancelled: {isCancelled}, isSuccess: {isSuccess}, errorCode: {(errorCode.HasValue ? errorCode.Value.ToString() : "null")}";
        }
        else
        {
            Debug.LogError("[WindowsDialogManagerExampleController] Result label is null!");
        }
    }

    /// <summary>
    /// Callback for a save file dialog completion.
    /// </summary>
    /// <param name="filePath">The chosen save path or null if cancelled/failure.</param>
    /// <param name="isCancelled">True if the user cancelled.</param>
    /// <param name="isSuccess">True if native call succeeded.</param>
    /// <param name="errorCode">Win32 error code when <paramref name=\"isSuccess\"/> is false.</param>
    private void OnSaveFileDialogResult(string? filePath, bool isCancelled, bool isSuccess, int? errorCode)
    {
        Debug.Log($"[WindowsDialogManagerExampleController] OnSaveFileDialogResult -> filePath: {filePath ?? "null"}, isCancelled: {isCancelled}, isSuccess: {isSuccess}, errorCode: {(errorCode.HasValue ? errorCode.Value.ToString() : "null")}");
        if (_resultLabel != null)
        {
            _resultLabel.text = (isSuccess ? "OK" : "Error") +
                                $"\nShowSaveFileDialog filePath: {filePath ?? "null"}, isCancelled: {isCancelled}, isSuccess: {isSuccess}, errorCode: {(errorCode.HasValue ? errorCode.Value.ToString() : "null")}";
        }
        else
        {
            Debug.LogError("[WindowsDialogManagerExampleController] Result label is null!");
        }
    }
}
#endif
