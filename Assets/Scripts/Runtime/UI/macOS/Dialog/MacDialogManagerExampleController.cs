#nullable enable

#if UNITY_STANDALONE_OSX || UNITY_EDITOR
using UnityEngine;
using UnityEngine.UIElements;

public class MacDialogManagerExampleController : MonoBehaviour
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
        Debug.Log("Running in Unity Editor - macOS simulation mode");
        UnityEditor.EditorUtility.DisplayDialog(
            "MacDialogManager Example",
            "This is a simulation of the macOS dialog manager.\nAll events will not be triggered.\nRun in macOS player for full functionality.",
            "OK");
#elif UNITY_STANDALONE_OSX
        Debug.Log("Running on macOS player");
#else
        Debug.LogWarning("MacDialogManagerExampleController is only supported on macOS Standalone or Editor.");
        gameObject.SetActive(false);
        return;
#endif
        Debug.Log("[MacDialogManagerExampleController] initialized.");
    }

    private void Start()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        if (uiDocument == null)
        {
            Debug.LogError("[MacDialogManagerExampleController] UIDocument component not found!");
            return;
        }

        InitializeUI();
        Debug.Log("[MacDialogManagerExampleController] Initialized successfully");
    }

    private void InitializeUI()
    {
        var root = uiDocument?.rootVisualElement;
        if (root == null)
        {
            Debug.LogError("[MacDialogManagerExampleController] rootVisualElement is null!");
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
            Debug.LogError("[MacDialogManagerExampleController] One or more UI elements are missing in UXML.");
            return;
        }

        // Wire up clicks
        if (_btnAlert != null) _btnAlert.clicked += OnShowDialogClicked;
        if (_btnFile != null) _btnFile.clicked += OnShowFileDialogClicked;
        if (_btnMultiFile != null) _btnMultiFile.clicked += OnShowMultiFileDialogClicked;
        if (_btnFolder != null) _btnFolder.clicked += OnShowFolderDialogClicked;
        if (_btnMultiFolder != null) _btnMultiFolder.clicked += OnShowMultiFolderDialogClicked;
        if (_btnSaveFile != null) _btnSaveFile.clicked += OnShowSaveFileDialogClicked;

#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        MacDialogManager.Instance.AlertDialogResult += OnAlertDialogResult;
        MacDialogManager.Instance.FileDialogResult += OnFileDialogResult;
        MacDialogManager.Instance.MultiFileDialogResult += OnMultiFileDialogResult;
        MacDialogManager.Instance.FolderDialogResult += OnFolderDialogResult;
        MacDialogManager.Instance.MultiFolderDialogResult += OnMultiFolderDialogResult;
        MacDialogManager.Instance.SaveFileDialogResult += OnSaveFileDialogResult;
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

#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        if (MacDialogManager.Instance != null)
        {
            MacDialogManager.Instance.AlertDialogResult -= OnAlertDialogResult;
            MacDialogManager.Instance.FileDialogResult -= OnFileDialogResult;
            MacDialogManager.Instance.MultiFileDialogResult -= OnMultiFileDialogResult;
            MacDialogManager.Instance.FolderDialogResult -= OnFolderDialogResult;
            MacDialogManager.Instance.MultiFolderDialogResult -= OnMultiFolderDialogResult;
            MacDialogManager.Instance.SaveFileDialogResult -= OnSaveFileDialogResult;
        }
#endif
        Debug.Log("[MacDialogManagerExampleController] Destroyed and unsubscribed from all events.");
    }

    // Button actions
    private void OnShowDialogClicked()
    {
        Debug.Log("[MacDialogManagerExampleController] OnShowDialogClicked triggered");
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        string title = "Hello from Unity";
        string message = "This is a native macOS dialog!";
        DialogButton[] buttons = {
            new DialogButton { title = "OK", isDefault = true, keyEquivalent = "return" },
            new DialogButton { title = "Cancel", keyEquivalent = "return" },
            new DialogButton { title = "Delete", keyEquivalent = "d" }
        };
        DialogOptions options = new DialogOptions
        {
            alertStyle = "warning",
            showsSuppressionButton = true,
            suppressionButtonTitle = "Don't show this again",
        };
        MacDialogManager.Instance.ShowDialog(title, message, buttons, options);
#endif
    }

    private void OnShowFileDialogClicked()
    {
        Debug.Log("[MacDialogManagerExampleController] OnShowFileDialogClicked triggered");
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        string title = "Select a file";
        string message = "Please select a file to open.";
        string[] allowedContentTypes = { "public.text" };
        string? directoryPath = null;
        MacDialogManager.Instance.ShowFileDialog(
            title,
            message,
            allowedContentTypes,
            directoryPath
        );
#endif
    }

    private void OnShowMultiFileDialogClicked()
    {
        Debug.Log("[MacDialogManagerExampleController] OnShowMultiFileDialogClicked triggered");
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        string title = "Select files";
        string message = "Please select files to open.";
        string[] allowedContentTypes = { "public.text" };
        string? directoryPath = null;
        MacDialogManager.Instance.ShowMultiFileDialog(
            title,
            message,
            allowedContentTypes,
            directoryPath
        );
#endif
    }

    private void OnShowFolderDialogClicked()
    {
        Debug.Log("[MacDialogManagerExampleController] OnShowFolderDialogClicked triggered");
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        string title = "Select a folder";
        string message = "Please select a folder to open.";
        string? directoryPath = null;
        MacDialogManager.Instance.ShowFolderDialog(
            title,
            message,
            directoryPath
        );
#endif
    }

    private void OnShowMultiFolderDialogClicked()
    {
        Debug.Log("[MacDialogManagerExampleController] OnShowMultiFolderDialogClicked triggered");
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        string title = "Select folders";
        string message = "Please select folders to open.";
        string? directoryPath = null;
        MacDialogManager.Instance.ShowMultiFolderDialog(
            title,
            message,
            directoryPath
        );
#endif
    }

    private void OnShowSaveFileDialogClicked()
    {
        Debug.Log("[MacDialogManagerExampleController] OnShowSaveFileDialogClicked triggered");
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        string title = "Save File";
        string message = "Choose a destination";
        string defaultFileName = "default.txt";
        string[] allowedContentTypes = { "public.text" };
        string? directoryPath = null;
        MacDialogManager.Instance.ShowSaveFileDialog(
            title,
            message,
            defaultFileName,
            allowedContentTypes,
            directoryPath
        );
#endif
    }

    // Event handlers
    private void OnAlertDialogResult(string? buttonTitle, int buttonIndex, bool suppressionState, bool isSuccess, string? errorMessage)
    {
        Debug.Log($"[MacDialogManagerExampleController] OnAlertDialogResult -> buttonTitle: {buttonTitle ?? "null"}, buttonIndex: {buttonIndex}, suppressionState: {suppressionState}, isSuccess: {isSuccess}, errorMessage: {errorMessage ?? "null"}");
        if (_resultLabel != null)
        {
            _resultLabel.text = (isSuccess ? "OK" : "Error") +
                                $"\nShowDialog buttonTitle: {buttonTitle ?? "null"}, buttonIndex: {buttonIndex}, suppressionState: {suppressionState}, isSuccess: {isSuccess}, errorMessage: {errorMessage ?? "null"}";
        }
        else
        {
            Debug.LogError("[MacDialogManagerExampleController] Result label is null!");
        }
    }

    private void OnFileDialogResult(string[]? filePaths, int fileCount, string? directoryURL, bool isCancelled, bool isSuccess, string? errorMessage)
    {
        var list = filePaths == null ? "null" : string.Join(", ", filePaths);
        Debug.Log($"[MacDialogManagerExampleController] OnFileDialogResult -> filePaths: {list}, fileCount: {fileCount}, directoryURL: {directoryURL ?? "null"}, isCancelled: {isCancelled}, isSuccess: {isSuccess}, errorMessage: {errorMessage ?? "null"}");
        if (_resultLabel != null)
        {
            _resultLabel.text = (isSuccess ? "OK" : "Error") +
                                $"\nShowFileDialog filePaths: {list}, fileCount: {fileCount}, directoryURL: {directoryURL ?? "null"}, isCancelled: {isCancelled}, isSuccess: {isSuccess}, errorMessage: {errorMessage ?? "null"}";
        }
        else
        {
            Debug.LogError("[MacDialogManagerExampleController] Result label is null!");
        }
    }

    private void OnMultiFileDialogResult(string[]? filePaths, int fileCount, string? directoryURL, bool isCancelled, bool isSuccess, string? errorMessage)
    {
        var list = filePaths == null ? "null" : string.Join(", ", filePaths);
        Debug.Log($"[MacDialogManagerExampleController] OnMultiFileDialogResult -> filePaths: {list}, fileCount: {fileCount}, directoryURL: {directoryURL ?? "null"}, isCancelled: {isCancelled}, isSuccess: {isSuccess}, errorMessage: {errorMessage ?? "null"}");
        if (_resultLabel != null)
        {
            _resultLabel.text = (isSuccess ? "OK" : "Error") +
                                $"\nShowMultiFileDialog filePaths: {list}, fileCount: {fileCount}, directoryURL: {directoryURL ?? "null"}, isCancelled: {isCancelled}, isSuccess: {isSuccess}, errorMessage: {errorMessage ?? "null"}";
        }
        else
        {
            Debug.LogError("[MacDialogManagerExampleController] Result label is null!");
        }
    }

    private void OnFolderDialogResult(string[]? folderPaths, int folderCount, string? directoryURL, bool isCancelled, bool isSuccess, string? errorMessage)
    {
        var list = folderPaths == null ? "null" : string.Join(", ", folderPaths);
        Debug.Log($"[MacDialogManagerExampleController] OnFolderDialogResult -> folderPaths: {list}, folderCount: {folderCount}, directoryURL: {directoryURL ?? "null"}, isCancelled: {isCancelled}, isSuccess: {isSuccess}, errorMessage: {errorMessage ?? "null"}");
        if (_resultLabel != null)
        {
            _resultLabel.text = (isSuccess ? "OK" : "Error") +
                                $"\nShowFolderDialog folderPaths: {list}, folderCount: {folderCount}, directoryURL: {directoryURL ?? "null"}, isCancelled: {isCancelled}, isSuccess: {isSuccess}, errorMessage: {errorMessage ?? "null"}";
        }
        else
        {
            Debug.LogError("[MacDialogManagerExampleController] Result label is null!");
        }
    }

    private void OnMultiFolderDialogResult(string[]? folderPaths, int folderCount, string? directoryURL, bool isCancelled, bool isSuccess, string? errorMessage)
    {
        var list = folderPaths == null ? "null" : string.Join(", ", folderPaths);
        Debug.Log($"[MacDialogManagerExampleController] OnMultiFolderDialogResult -> folderPaths: {list}, folderCount: {folderCount}, directoryURL: {directoryURL ?? "null"}, isCancelled: {isCancelled}, isSuccess: {isSuccess}, errorMessage: {errorMessage ?? "null"}");
        if (_resultLabel != null)
        {
            _resultLabel.text = (isSuccess ? "OK" : "Error") +
                                $"\nShowMultiFolderDialog folderPaths: {list}, folderCount: {folderCount}, directoryURL: {directoryURL ?? "null"}, isCancelled: {isCancelled}, isSuccess: {isSuccess}, errorMessage: {errorMessage ?? "null"}";
        }
        else
        {
            Debug.LogError("[MacDialogManagerExampleController] Result label is null!");
        }
    }

    private void OnSaveFileDialogResult(string? filePath, int fileCount, string? directoryURL, bool isCancelled, bool isSuccess, string? errorMessage)
    {
        Debug.Log($"[MacDialogManagerExampleController] OnSaveFileDialogResult -> filePath: {filePath ?? "null"}, fileCount: {fileCount}, directoryURL: {directoryURL ?? "null"}, isCancelled: {isCancelled}, isSuccess: {isSuccess}, errorMessage: {errorMessage ?? "null"}");
        if (_resultLabel != null)
        {
            _resultLabel.text = (isSuccess ? "OK" : "Error") +
                                $"\nShowSaveFileDialog filePath: {filePath ?? "null"}, fileCount: {fileCount}, directoryURL: {directoryURL ?? "null"}, isCancelled: {isCancelled}, isSuccess: {isSuccess}, errorMessage: {errorMessage ?? "null"}";
        }
        else
        {
            Debug.LogError("[MacDialogManagerExampleController] Result label is null!");
        }
    }
}
#endif