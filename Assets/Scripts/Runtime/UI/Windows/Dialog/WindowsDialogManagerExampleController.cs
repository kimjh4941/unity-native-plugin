#nullable enable

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;

public class WindowsDialogManagerExampleController : MonoBehaviour
{
    [SerializeField] private UIDocument? uiDocument;

    // UI refs
    private Label? _resultLabel;
    private Button? _btnAlert;
    private Button? _btnFile;
    private Button? _btnMultiFile;
    private Button? _btnFolder;
    private Button? _btnMultiFolder;
    private Button? _btnSaveFile;

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

    private void OnShowFileDialogClicked()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        uint bufferSize = 1024;
        string filter = "Text Files\0*.txt\0All Files\0*.*\0\0";
        WindowsDialogManager.Instance.ShowFileDialog(bufferSize, filter);
#endif
    }

    private void OnShowMultiFileDialogClicked()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        uint bufferSize = 4096;
        string filter = "Text Files\0*.txt\0All Files\0*.*\0\0";
        WindowsDialogManager.Instance.ShowMultiFileDialog(bufferSize, filter);
#endif
    }

    private void OnShowFolderDialogClicked()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        uint bufferSize = 1024;
        string title = "Select Folder";
        WindowsDialogManager.Instance.ShowFolderDialog(bufferSize, title);
#endif
    }

    private void OnShowMultiFolderDialogClicked()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        uint bufferSize = 4096;
        string title = "Select Folders";
        WindowsDialogManager.Instance.ShowMultiFolderDialog(bufferSize, title);
#endif
    }

    private void OnShowSaveFileDialogClicked()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        uint bufferSize = 1024;
        string filter = "Text Files\0*.txt\0All Files\0*.*\0\0";
        string defExt = "txt";
        WindowsDialogManager.Instance.ShowSaveFileDialog(bufferSize, filter, defExt);
#endif
    }

    // Event handlers (unsubscribe after one response to avoid duplicates)
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
