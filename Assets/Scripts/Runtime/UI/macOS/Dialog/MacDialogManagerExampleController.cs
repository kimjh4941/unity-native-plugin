#nullable enable

#if UNITY_STANDALONE_OSX || UNITY_EDITOR
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Controller for the macOS native dialog manager example UI.
/// Provides functionality to demonstrate various types of macOS native dialogs including
/// alert dialogs, file/folder selection dialogs, and save dialogs.
/// Handles macOS-specific file system interactions and native dialog integration.
/// </summary>
public class MacDialogManagerExampleController : MonoBehaviour
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

    /// <summary>
    /// Initialize UI elements and set up event handlers on Start.
    /// Configures button references and registers click events for macOS dialog operations.
    /// </summary>
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

    /// <summary>
    /// Resolves and caches UI Toolkit element references, validates presence of required elements,
    /// wires button click callbacks, and (in a macOS standalone player) subscribes to native dialog result events.
    /// </summary>
    /// <remarks>
    /// In the Unity Editor we only simulate user interaction (native events are not fired). In a macOS standalone
    /// build the <see cref="MacDialogManager"/> singleton dispatches results back to these handlers on the Unity main thread.
    /// </remarks>
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

    /// <summary>
    /// Cleans up button click handlers and unsubscribes from native dialog result events to prevent leaks or
    /// duplicate callbacks after domain reload or scene unload.
    /// </summary>
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
    /// <summary>
    /// Shows a native macOS alert dialog with multiple buttons (OK / Cancel / Delete) and a suppression checkbox.
    /// </summary>
    /// <remarks>
    /// The suppression state and selected button are reported through <see cref="OnAlertDialogResult"/>.
    /// In the editor this logic is simulated with an editor dialog and no native events are raised.
    /// </remarks>
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

    /// <summary>
    /// Opens a single file selection dialog restricted to the specified content types (UTI based filtering).
    /// </summary>
    /// <remarks>The result is delivered via <see cref="OnFileDialogResult"/>.</remarks>
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

    /// <summary>
    /// Opens a multi-file selection dialog allowing the user to choose multiple files.
    /// </summary>
    /// <remarks>The result is delivered via <see cref="OnMultiFileDialogResult"/>.</remarks>
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

    /// <summary>
    /// Opens a single folder selection dialog.
    /// </summary>
    /// <remarks>The result is delivered via <see cref="OnFolderDialogResult"/>.</remarks>
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

    /// <summary>
    /// Opens a multi-folder selection dialog allowing the user to pick several directories.
    /// </summary>
    /// <remarks>The result is delivered via <see cref="OnMultiFolderDialogResult"/>.</remarks>
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

    /// <summary>
    /// Opens a save file dialog pre-populated with a default filename and optional content type filter.
    /// </summary>
    /// <remarks>The result is delivered via <see cref="OnSaveFileDialogResult"/>.</remarks>
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
    /// <summary>
    /// Callback for results of <see cref="MacDialogManager.ShowDialog"/>.
    /// </summary>
    /// <param name="buttonTitle">The title text of the button pressed (null if unavailable on failure).</param>
    /// <param name="buttonIndex">Zero-based index of the pressed button in the provided array.</param>
    /// <param name="suppressionState">True if the suppression checkbox was checked.</param>
    /// <param name="isSuccess">True if the native API call succeeded.</param>
    /// <param name="errorMessage">Error description when <paramref name="isSuccess"/> is false; otherwise null.</param>
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

    /// <summary>
    /// Callback for results of <see cref="MacDialogManager.ShowFileDialog"/>.
    /// </summary>
    /// <param name="filePaths">Array of selected file paths or null on failure.</param>
    /// <param name="fileCount">Number of file paths returned (may be 0 if cancelled).</param>
    /// <param name="directoryURL">Directory URL where selection occurred (may be null).</param>
    /// <param name="isCancelled">True if the user cancelled the dialog.</param>
    /// <param name="isSuccess">True if the native operation succeeded.</param>
    /// <param name="errorMessage">Error description when <paramref name="isSuccess"/> is false.</param>
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

    /// <summary>
    /// Callback for results of <see cref="MacDialogManager.ShowMultiFileDialog"/>.
    /// </summary>
    /// <param name="filePaths">Array of selected file paths or null on failure.</param>
    /// <param name="fileCount">Number of file paths returned (may be 0 if cancelled).</param>
    /// <param name="directoryURL">Directory URL where selection occurred (may be null).</param>
    /// <param name="isCancelled">True if the user cancelled the dialog.</param>
    /// <param name="isSuccess">True if the native operation succeeded.</param>
    /// <param name="errorMessage">Error description when <paramref name="isSuccess"/> is false.</param>
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

    /// <summary>
    /// Callback for results of <see cref="MacDialogManager.ShowFolderDialog"/>.
    /// </summary>
    /// <param name="folderPaths">Array with a single selected folder path or null on failure.</param>
    /// <param name="folderCount">Number of folder paths returned (0 if cancelled).</param>
    /// <param name="directoryURL">Directory URL where selection occurred (may be null).</param>
    /// <param name="isCancelled">True if the user cancelled the dialog.</param>
    /// <param name="isSuccess">True if the native operation succeeded.</param>
    /// <param name="errorMessage">Error description when <paramref name="isSuccess"/> is false.</param>
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

    /// <summary>
    /// Callback for results of <see cref="MacDialogManager.ShowMultiFolderDialog"/>.
    /// </summary>
    /// <param name="folderPaths">Array of selected folder paths or null on failure.</param>
    /// <param name="folderCount">Number of folder paths returned (0 if cancelled).</param>
    /// <param name="directoryURL">Directory URL where selection occurred (may be null).</param>
    /// <param name="isCancelled">True if the user cancelled the dialog.</param>
    /// <param name="isSuccess">True if the native operation succeeded.</param>
    /// <param name="errorMessage">Error description when <paramref name="isSuccess"/> is false.</param>
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

    /// <summary>
    /// Callback for results of <see cref="MacDialogManager.ShowSaveFileDialog"/>.
    /// </summary>
    /// <param name="filePath">The chosen file path (null if cancelled or failed).</param>
    /// <param name="fileCount">The number of returned paths (1 for success, 0 if cancelled).</param>
    /// <param name="directoryURL">Directory URL where save occurred (may be null).</param>
    /// <param name="isCancelled">True if the user cancelled the dialog.</param>
    /// <param name="isSuccess">True if the native operation succeeded.</param>
    /// <param name="errorMessage">Error description when <paramref name="isSuccess"/> is false.</param>
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