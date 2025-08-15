#nullable enable

using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

[System.Serializable]
public class MacDialogButtonData
{
    public string? text;
    public string? action;
    public string? buttonId;
}

public class MacDialogManagerExampleController : MonoBehaviour
{
    [SerializeField] private UIDocument? uiDocument;

    private ListView? dialogListView;
    private readonly List<MacDialogButtonData> dialogButtons = new()
    {
        new MacDialogButtonData { text = "ShowAlertDialog", action = "ShowAlertDialog", buttonId = "ShowAlertDialog" },
        new MacDialogButtonData { text = "ShowFileDialog", action = "ShowFileDialog", buttonId = "ShowFileDialog" },
        new MacDialogButtonData { text = "ShowMultiFileDialog", action = "ShowMultiFileDialog", buttonId = "ShowMultiFileDialog" },
        new MacDialogButtonData { text = "ShowFolderDialog", action = "ShowFolderDialog", buttonId = "ShowFolderDialog" },
        new MacDialogButtonData { text = "ShowMultiFolderDialog", action = "ShowMultiFolderDialog", buttonId = "ShowMultiFolderDialog" },
        new MacDialogButtonData { text = "ShowSaveFileDialog", action = "ShowSaveFileDialog", buttonId = "ShowSaveFileDialog" },
    };

    void Awake()
    {
#if UNITY_EDITOR
        Debug.Log("Running in Unity Editor - macOS simulation mode");
#elif UNITY_STANDALONE_OSX
        Debug.Log("Running on macOS player");
#else
        Debug.LogWarning("MacDialogManagerExampleController is only supported on macOS Standalone or Editor.");
        gameObject.SetActive(false);
        return;
#endif
        Debug.Log("MacDialogManagerExampleController initialized.");
    }

    private void Start()
    {
        if (uiDocument == null)
        {
            uiDocument = GetComponent<UIDocument>();
        }

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

        dialogListView = root.Q<ListView>("DialogListView");
        if (dialogListView == null)
        {
            Debug.LogError("[MacDialogManagerExampleController] DialogListView not found!");
            return;
        }

        SetupListView();
        Debug.Log($"[MacDialogManagerExampleController] ListView initialized with {dialogButtons.Count} items");
    }

    private void SetupListView()
    {
        if (dialogListView == null) return;

        dialogListView.itemsSource = dialogButtons;

        dialogListView.makeItem = () =>
        {
            var label = new Label();
            label.AddToClassList("mac-dialog-list-item");
            return label;
        };

        dialogListView.bindItem = (element, index) =>
        {
            var buttonData = dialogButtons[index];
            if (element is not Label label || buttonData == null)
            {
                Debug.LogError($"[MacDialogManagerExampleController] Invalid item at index {index}");
                return;
            }

            label.text = buttonData.text;
            label.name = buttonData.buttonId;

            label.RemoveFromClassList("show-alert-dialog-item");
            label.RemoveFromClassList("show-file-dialog-item");
            label.RemoveFromClassList("show-multi-file-dialog-item");
            label.RemoveFromClassList("show-folder-dialog-item");
            label.RemoveFromClassList("show-multi-folder-dialog-item");
            label.RemoveFromClassList("show-save-file-dialog-item");

            label.AddToClassList(GetItemClassFromButtonId(buttonData.buttonId ?? string.Empty));

            // Clickable を追加（単一クリックで実行）
            var clickable = new Clickable(() =>
            {
                if (!string.IsNullOrEmpty(buttonData.action))
                {
                    OnDialogButtonClicked(buttonData.action);
                }
                else
                {
                    Debug.LogError("[MacDialogManagerExampleController] Button action is null or empty.");
                }
            });
            label.AddManipulator(clickable);
            // 後で解除するために保持
            label.userData = clickable;
        };

        dialogListView.unbindItem = (element, index) =>
        {
            if (element is VisualElement ve && ve.userData is Clickable c)
            {
                ve.RemoveManipulator(c);
                ve.userData = null;
            }
        };
        dialogListView.Rebuild();
    }

    private string GetItemClassFromButtonId(string buttonId)
    {
        switch (buttonId)
        {
            case "ShowAlertDialog": return "show-alert-dialog-item";
            case "ShowFileDialog": return "show-file-dialog-item";
            case "ShowMultiFileDialog": return "show-multi-file-dialog-item";
            case "ShowFolderDialog": return "show-folder-dialog-item";
            case "ShowMultiFolderDialog": return "show-multi-folder-dialog-item";
            case "ShowSaveFileDialog": return "show-save-file-dialog-item";
            default: return "mac-dialog-list-item";
        }
    }

    private void OnDialogButtonClicked(string action)
    {
        Debug.Log($"[MacDialogManagerExampleController] Action: {action}");
        switch (action)
        {
            case "ShowAlertDialog": OnShowAlertDialogClicked(); break;
            case "ShowFileDialog": OnShowFileDialogClicked(); break;
            case "ShowMultiFileDialog": OnShowMultiFileDialogClicked(); break;
            case "ShowFolderDialog": OnShowFolderDialogClicked(); break;
            case "ShowMultiFolderDialog": OnShowMultiFolderDialogClicked(); break;
            case "ShowSaveFileDialog": OnShowSaveFileDialogClicked(); break;
            default: Debug.LogWarning($"Unknown action: {action}"); break;
        }
    }

    // Dialog actions
    private void OnShowAlertDialogClicked()
    {
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        var buttons = new DialogButton[]
        {
            new DialogButton{ title = "OK", isDefault = true, keyEquivalent = "return" },
            new DialogButton{ title = "Cancel", isDefault = false, keyEquivalent = "escape" },
        };
        var options = new DialogOptions
        {
            alertStyle = "informational",
            showsSuppressionButton = false,
            suppressionButtonTitle = null
        };
        MacDialogManager.Instance.ShowDialog("Hello macOS", "This is a native macOS alert.", buttons, options);
#else
        Debug.Log("[MacDialogManagerExampleController] ShowAlertDialog - Editor simulation");
        UnityEditor.EditorUtility.DisplayDialog("Hello macOS", "This is a native macOS alert.", "OK");
#endif
    }

    private void OnShowFileDialogClicked()
    {
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        string[] types = { "public.data", "public.image" }; // UTType identifiers
        MacDialogManager.Instance.ShowFileDialog("Open File", "Choose a file to open", types, null);
#else
        Debug.Log("[MacDialogManagerExampleController] ShowFileDialog - Editor simulation");
#endif
    }

    private void OnShowMultiFileDialogClicked()
    {
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        string[] types = { "public.data", "public.image" };
        MacDialogManager.Instance.ShowMultiFileDialog("Open Multiple Files", "Choose files to open", types, null);
#else
        Debug.Log("[MacDialogManagerExampleController] ShowMultiFileDialog - Editor simulation");
#endif
    }

    private void OnShowFolderDialogClicked()
    {
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        MacDialogManager.Instance.ShowFolderDialog("Open Folder", "Choose a folder", null);
#else
        Debug.Log("[MacDialogManagerExampleController] ShowFolderDialog - Editor simulation");
#endif
    }

    private void OnShowMultiFolderDialogClicked()
    {
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        MacDialogManager.Instance.ShowMultiFolderDialog("Open Folders", "Choose folders", null);
#else
        Debug.Log("[MacDialogManagerExampleController] ShowMultiFolderDialog - Editor simulation");
#endif
    }

    private void OnShowSaveFileDialogClicked()
    {
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        string[] types = { "public.text" };
        MacDialogManager.Instance.ShowSaveFileDialog("Save File", "Choose a destination", "untitled.txt", types, null);
#else
        Debug.Log("[MacDialogManagerExampleController] ShowSaveFileDialog - Editor simulation");
#endif
    }

    // Event subscription
    private void OnEnable()
    {
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        if (MacDialogManager.Instance != null)
        {
            MacDialogManager.Instance.AlertDialogResult += OnAlertDialogResult;
            MacDialogManager.Instance.FileDialogResult += OnFileDialogResult;
            MacDialogManager.Instance.MultiFileDialogResult += OnMultiFileDialogResult;
            MacDialogManager.Instance.FolderDialogResult += OnFolderDialogResult;
            MacDialogManager.Instance.MultiFolderDialogResult += OnMultiFolderDialogResult;
            MacDialogManager.Instance.SaveFileDialogResult += OnSaveFileDialogResult;
        }
#endif
    }

    private void OnDisable()
    {
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
    }

    // Event handlers
    private void OnAlertDialogResult(string? buttonTitle, int buttonIndex, bool suppressionState, bool isSuccess, string? errorMessage)
    {
        Debug.Log($"[MacDialogManagerExampleController] AlertDialogResult -> buttonTitle: {buttonTitle ?? "null"}, buttonIndex: {buttonIndex}, suppressionState: {suppressionState}, isSuccess: {isSuccess}, errorMessage: {errorMessage ?? "null"}");
    }

    private void OnFileDialogResult(string[]? filePaths, int fileCount, string? directoryURL, bool isCancelled, bool isSuccess, string? errorMessage)
    {
        Debug.Log($"[MacDialogManagerExampleController] FileDialogResult -> filePaths: {(filePaths == null ? "null" : string.Join(", ", filePaths))}, fileCount: {fileCount}, directoryURL: {directoryURL ?? "null"}, isCancelled: {isCancelled}, isSuccess: {isSuccess}, errorMessage: {errorMessage ?? "null"}");
    }

    private void OnMultiFileDialogResult(string[]? filePaths, int fileCount, string? directoryURL, bool isCancelled, bool isSuccess, string? errorMessage)
    {
        Debug.Log($"[MacDialogManagerExampleController] MultiFileDialogResult -> filePaths: {(filePaths == null ? "null" : string.Join(", ", filePaths))}, fileCount: {fileCount}, directoryURL: {directoryURL ?? "null"}, isCancelled: {isCancelled}, isSuccess: {isSuccess}, errorMessage: {errorMessage ?? "null"}");
    }

    private void OnFolderDialogResult(string[]? folderPaths, int folderCount, string? directoryURL, bool isCancelled, bool isSuccess, string? errorMessage)
    {
        Debug.Log($"[MacDialogManagerExampleController] FolderDialogResult -> folderPaths: {(folderPaths == null ? "null" : string.Join(", ", folderPaths))}, folderCount: {folderCount}, directoryURL: {directoryURL ?? "null"}, isCancelled: {isCancelled}, isSuccess: {isSuccess}, errorMessage: {errorMessage ?? "null"}");
    }

    private void OnMultiFolderDialogResult(string[]? folderPaths, int folderCount, string? directoryURL, bool isCancelled, bool isSuccess, string? errorMessage)
    {
        Debug.Log($"[MacDialogManagerExampleController] MultiFolderDialogResult -> folderPaths: {(folderPaths == null ? "null" : string.Join(", ", folderPaths))}, folderCount: {folderCount}, directoryURL: {directoryURL ?? "null"}, isCancelled: {isCancelled}, isSuccess: {isSuccess}, errorMessage: {errorMessage ?? "null"}");
    }

    private void OnSaveFileDialogResult(string? filePath, int fileCount, string? directoryURL, bool isCancelled, bool isSuccess, string? errorMessage)
    {
        Debug.Log($"[MacDialogManagerExampleController] SaveFileDialogResult -> filePath: {filePath ?? "null"}, fileCount: {fileCount}, directoryURL: {directoryURL ?? "null"}, isCancelled: {isCancelled}, isSuccess: {isSuccess}, errorMessage: {errorMessage ?? "null"}");
    }
}