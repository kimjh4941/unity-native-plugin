#if UNITY_STANDALONE_OSX
using System.Runtime.InteropServices;
using UnityEngine;

public class MacDialogPlugin : MonoBehaviour
{
    void Start()
    {
        // MacDialogManagerのインスタンスを取得し、イベントを登録
        MacDialogManager.Instance.AlertDialogResult += (buttonTitle, buttonIndex, suppressionButtonState, success, errorMessage) =>
        {
            Debug.Log($"AlertDialogResult buttonTitle: {buttonTitle}, buttonIndex: {buttonIndex}, suppressionButtonState: {suppressionButtonState}, success: {success}, errorMessage: {errorMessage}");
        };
        MacDialogManager.Instance.FileDialogResult += (filePaths, fileCount, directoryURL, isCancelled, isSuccess, errorMessage) =>
        {
            Debug.Log($"FileDialogResult: {string.Join(", ", filePaths)}, fileCount: {fileCount}, directoryURL: {directoryURL}, isCancelled: {isCancelled}, isSuccess: {isSuccess}, errorMessage: {errorMessage}");
        };
        MacDialogManager.Instance.MultiFileDialogResult += (filePaths, fileCount, directoryURL, isCancelled, isSuccess, errorMessage) =>
        {
            Debug.Log($"MultiFileDialogResult: {string.Join(", ", filePaths)}, fileCount: {fileCount}, directoryURL: {directoryURL}, isCancelled: {isCancelled}, isSuccess: {isSuccess}, errorMessage: {errorMessage}");
        };
        MacDialogManager.Instance.FolderDialogResult += (folderPaths, folderCount, directoryURL, isCancelled, isSuccess, errorMessage) =>
        {
            Debug.Log($"FolderDialogResult: {string.Join(", ", folderPaths)}, folderCount: {folderCount}, directoryURL: {directoryURL}, isCancelled: {isCancelled}, isSuccess: {isSuccess}, errorMessage: {errorMessage}");
        };
        MacDialogManager.Instance.MultiFolderDialogResult += (folderPaths, folderCount, directoryURL, isCancelled, isSuccess, errorMessage) =>
        {
            Debug.Log($"MultiFolderDialogResult: {string.Join(", ", folderPaths)}, folderCount: {folderCount}, directoryURL: {directoryURL}, isCancelled: {isCancelled}, isSuccess: {isSuccess}, errorMessage: {errorMessage}");
        };
        MacDialogManager.Instance.SaveFileDialogResult += (filePath, fileCount, directoryURL, isCancelled, isSuccess, errorMessage) =>
        {
            Debug.Log($"SaveFileDialogResult: filePath: {filePath}, fileCount: {fileCount}, directoryURL: {directoryURL}, isCancelled: {isCancelled}, isSuccess: {isSuccess}, errorMessage: {errorMessage}");
        };
    }

    void OnDestroy()
    {
        // イベントの登録を解除
        MacDialogManager.Instance.AlertDialogResult -= (buttonTitle, buttonIndex, suppressionButtonState, success, errorMessage) =>
        {
            Debug.Log($"AlertDialogResult buttonTitle: {buttonTitle}, buttonIndex: {buttonIndex}, suppressionButtonState: {suppressionButtonState}, success: {success}, errorMessage: {errorMessage}");
        };
        MacDialogManager.Instance.FileDialogResult -= (filePaths, fileCount, directoryURL, isCancelled, isSuccess, errorMessage) =>
        {
            Debug.Log($"FileDialogResult: {string.Join(", ", filePaths)}, fileCount: {fileCount}, directoryURL: {directoryURL}, isCancelled: {isCancelled}, isSuccess: {isSuccess}, errorMessage: {errorMessage}");
        };
        MacDialogManager.Instance.MultiFileDialogResult -= (filePaths, fileCount, directoryURL, isCancelled, isSuccess, errorMessage) =>
        {
            Debug.Log($"MultiFileDialogResult: {string.Join(", ", filePaths)}, fileCount: {fileCount}, directoryURL: {directoryURL}, isCancelled: {isCancelled}, isSuccess: {isSuccess}, errorMessage: {errorMessage}");
        };
        MacDialogManager.Instance.FolderDialogResult -= (folderPaths, folderCount, directoryURL, isCancelled, isSuccess, errorMessage) =>
        {
            Debug.Log($"FolderDialogResult: {string.Join(", ", folderPaths)}, folderCount: {folderCount}, directoryURL: {directoryURL}, isCancelled: {isCancelled}, isSuccess: {isSuccess}, errorMessage: {errorMessage}");
        };
        MacDialogManager.Instance.MultiFolderDialogResult -= (folderPaths, folderCount, directoryURL, isCancelled, isSuccess, errorMessage) =>
        {
            Debug.Log($"MultiFolderDialogResult: {string.Join(", ", folderPaths)}, folderCount: {folderCount}, directoryURL: {directoryURL}, isCancelled: {isCancelled}, isSuccess: {isSuccess}, errorMessage: {errorMessage}");
        };
        MacDialogManager.Instance.SaveFileDialogResult -= (filePath, fileCount, directoryURL, isCancelled, isSuccess, errorMessage) =>
        {
            Debug.Log($"SaveFileDialogResult: filePath: {filePath}, fileCount: {fileCount}, directoryURL: {directoryURL}, isCancelled: {isCancelled}, isSuccess: {isSuccess}, errorMessage: {errorMessage}");
        };
    }

    public void ShowDialog()
    {
        // ダイアログのタイトルとメッセージ
        DialogButton[] buttons = {
            new DialogButton { title = "OK", isDefault = true, keyEquivalent = "\r" },
            new DialogButton { title = "Cancel", keyEquivalent = "\u001b" },
            new DialogButton { title = "Delete", keyEquivalent = "d" }
        };

        // ダイアログのオプション
        DialogOptions options = new DialogOptions
        {
            alertStyle = "warning",
            showsSuppressionButton = true,
            suppressionButtonTitle = "今後表示しない"
        };

        // MacDialogManagerを使用してダイアログを表示
        MacDialogManager.Instance.ShowDialog("Hello from Unity", "This is a native macOS dialog!", buttons, options);
    }

    public void ShowFileDialog()
    {
        // シングルファイル選択ダイアログ
        string[] allowedContentTypes = { "public.text" }; // テキストファイルのみ
        MacDialogManager.Instance.ShowFileDialog("Select a file", "Please select a file to open.", allowedContentTypes);
    }

    public void ShowMultiFileDialog()
    {
        // 複数ファイル選択ダイアログ
        string[] allowedContentTypes = { "public.text" }; // テキストファイルのみ
        MacDialogManager.Instance.ShowMultiFileDialog("Select files", "Please select files to open.", allowedContentTypes);
    }

    public void ShowFolderDialog()
    {
        // フォルダ選択ダイアログ
        MacDialogManager.Instance.ShowFolderDialog("Select a folder", "Please select a folder to open.");
    }

    public void ShowMultiFolderDialog()
    {
        // 複数フォルダ選択ダイアログ
        MacDialogManager.Instance.ShowMultiFolderDialog("Select folders", "Please select folders to open.");
    }

    public void ShowSaveFileDialog()
    {
        // ファイル保存ダイアログ
        string[] allowedContentTypes = { "public.text" }; // テキストファイルのみ
        MacDialogManager.Instance.ShowSaveFileDialog("Save file", "Please select a location to save the file.", "default.txt", allowedContentTypes);
    }
}
#endif