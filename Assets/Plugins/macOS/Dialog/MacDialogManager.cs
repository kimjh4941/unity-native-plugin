#if UNITY_STANDALONE_OSX
using UnityEngine;
using System.Runtime.InteropServices;
using System;

[Serializable]
public class DialogButtonsWrapper
{
    public DialogButton[] buttons;
}

[Serializable]
public class DialogButton
{
    public string title;
    public bool isDefault;
    public string keyEquivalent;
}

[Serializable]
public class DialogOptions
{
    public string alertStyle;
    public bool showsSuppressionButton;
    public string suppressionButtonTitle;
}

public class MacDialogManager : MonoBehaviour
{
    private static MacDialogManager _instance;

    public static MacDialogManager Instance
    {
        get
        {
            if (_instance == null)
            {
                Debug.Log("Creating new instance of MacDialogManager");
                GameObject singletonObject = new GameObject("MacDialogManager");
                _instance = singletonObject.AddComponent<MacDialogManager>();
                DontDestroyOnLoad(singletonObject);
            }
            return _instance;
        }
    }

    public event Action<string, int, bool, bool, string> AlertDialogResult;
    public event Action<string[], int, string, bool, bool, string> FileDialogResult;
    public event Action<string[], int, string, bool, bool, string> MultiFileDialogResult;
    public event Action<string[], int, string, bool, bool, string> FolderDialogResult;
    public event Action<string[], int, string, bool, bool, string> MultiFolderDialogResult;
    public event Action<string, int, string, bool, bool, string> SaveFileDialogResult;

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

    // Objective-Cのコールバック型定義
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void DialogCallback(string buttonTitle, int buttonIndex, bool suppressionButtonState, bool success, string errorMessage);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void FileDialogCallback(IntPtr filePaths, int fileCount, string directoryURL, bool isCancelled, bool isSuccess, string errorMessage);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void MultiFileDialogCallback(IntPtr filePaths, int fileCount, string directoryURL, bool isCancelled, bool isSuccess, string errorMessage);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void FolderDialogCallback(IntPtr folderPaths, int folderCount, string directoryURL, bool isCancelled, bool isSuccess, string errorMessage);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void MultiFolderDialogCallback(IntPtr folderPaths, int folderCount, string directoryURL, bool isCancelled, bool isSuccess, string errorMessage);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void SaveFileDialogCallback(string filePath, int fileCount, string directoryURL, bool isCancelled, bool isSuccess, string errorMessage);

    // Objective-Cの関数をインポート
    [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
    private static extern void showDialog(string title, string message, string buttonsJson, string optionsJson, DialogCallback callback);

    [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
    private static extern void showFileDialog(string title, string message, IntPtr allowedContentTypes, int contentTypesCount, string directoryPath, FileDialogCallback callback);

    [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
    private static extern void showMultiFileDialog(string title, string message, IntPtr allowedContentTypes, int contentTypesCount, string directoryPath, MultiFileDialogCallback callback);

    [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
    private static extern void showFolderDialog(string title, string message, string directoryPath, FolderDialogCallback callback);

    [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
    private static extern void showMultiFolderDialog(string title, string message, string directoryPath, MultiFolderDialogCallback callback);

    [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
    private static extern void showSaveFileDialog(string title, string message, string defaultFileName, IntPtr allowedContentTypes, int contentTypesCount, string directoryPath, SaveFileDialogCallback callback);

    public void ShowDialog(string title, string message, DialogButton[] buttons, DialogOptions options)
    {
        Debug.Log($"ShowDialog called with title: {title}, message: {message} buttons: {buttons.Length}, options: {options}");
        if (string.IsNullOrEmpty(title))
        {
            Debug.LogError("Title cannot be null or empty.");
            AlertDialogResult?.Invoke("", -1, false, false, "Title cannot be null or empty.");
            return;
        }

        if (buttons == null || buttons.Length == 0)
        {
            Debug.LogError("No buttons provided for the dialog.");
            AlertDialogResult?.Invoke("", -1, false, false, "No buttons provided for the dialog.");
            return;
        }

        if (options == null)
        {
            Debug.LogError("No options provided for the dialog.");
            AlertDialogResult?.Invoke("", -1, false, false, "No options provided for the dialog.");
            return;
        }

        // Convert buttons and options to JSON
        DialogButtonsWrapper wrapper = new DialogButtonsWrapper { buttons = buttons };
        string buttonsJson = JsonUtility.ToJson(wrapper);
        string optionsJson = JsonUtility.ToJson(options);
        Debug.Log($"ShowDialog title: {title}, message: {message}, buttonsJson: {buttonsJson}, optionsJson: {optionsJson}");

        UnityMainThreadDispatcher.Instance.Enqueue(() =>
        {
            DialogCallback dialogCallback = (buttonTitle, buttonIndex, suppressionButtonState, success, errorMessage) =>
            {
                UnityMainThreadDispatcher.Instance.Enqueue(() =>
                {
                    try
                    {
                        AlertDialogResult?.Invoke(buttonTitle, buttonIndex, suppressionButtonState, success, errorMessage);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"AlertDialogResult callback error: {ex.Message}");
                    }
                });
            };
            showDialog(title, message, buttonsJson, optionsJson, dialogCallback);
        });
    }

    public void ShowFileDialog(string title, string message, string[] allowedContentTypes = null, string directoryPath = "")
    {
        Debug.Log($"ShowFileDialog called with title: {title}, message: {message}, allowedContentTypes: {allowedContentTypes?.Length}, directoryPath: {directoryPath}");
        if (string.IsNullOrEmpty(title))
        {
            Debug.LogError("Title cannot be null or empty.");
            FileDialogResult?.Invoke(null, 0, "", false, false, "Title cannot be null or empty.");
            return;
        }

        IntPtr contentTypesPtr = IntPtr.Zero;
        IntPtr[] stringPointers = null;
        int contentTypesCount = allowedContentTypes?.Length ?? 0;

        try
        {
            if (contentTypesCount > 0)
            {
                stringPointers = new IntPtr[contentTypesCount];
                for (int i = 0; i < contentTypesCount; i++)
                {
                    byte[] utf8Bytes = System.Text.Encoding.UTF8.GetBytes(allowedContentTypes[i]);
                    stringPointers[i] = Marshal.AllocHGlobal(utf8Bytes.Length + 1);
                    Marshal.Copy(utf8Bytes, 0, stringPointers[i], utf8Bytes.Length);
                    Marshal.WriteByte(stringPointers[i], utf8Bytes.Length, 0); // null終端文字
                }
                contentTypesPtr = Marshal.AllocHGlobal(IntPtr.Size * contentTypesCount);
                Marshal.Copy(stringPointers, 0, contentTypesPtr, contentTypesCount);
            }

            FileDialogCallback fileDialogCallback = (filePathsPtr, fileCount, directoryURL, isCancelled, isSuccess, errorMessage) =>
            {
                // ポインタからC#のstring[]に即時変換
                string[] filePaths = new string[fileCount];
                if (filePathsPtr != IntPtr.Zero && fileCount > 0)
                {
                    IntPtr[] ptrArray = new IntPtr[fileCount];
                    Marshal.Copy(filePathsPtr, ptrArray, 0, fileCount);
                    for (int i = 0; i < fileCount; i++)
                    {
                        filePaths[i] = Marshal.PtrToStringUTF8(ptrArray[i]);
                    }
                }

                // マネージドオブジェクト（C#のstring[]）をキューに入れる
                UnityMainThreadDispatcher.Instance.Enqueue(() =>
                {
                    try
                    {
                        FileDialogResult?.Invoke(filePaths, fileCount, directoryURL, isCancelled, isSuccess, errorMessage);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"FileDialogResult callback error: {ex.Message}");
                    }
                    finally
                    {
                        // ★重要：ここで引数用のメモリを解放する
                        if (contentTypesPtr != IntPtr.Zero) Marshal.FreeHGlobal(contentTypesPtr);
                        if (stringPointers != null)
                        {
                            foreach (var ptr in stringPointers)
                            {
                                if (ptr != IntPtr.Zero) Marshal.FreeHGlobal(ptr);
                            }
                        }
                        Debug.Log("Memory for allowedContentTypes freed in FileDialog callback.");
                    }
                });
            };

            showFileDialog(title, message, contentTypesPtr, contentTypesCount, directoryPath, fileDialogCallback);
        }
        catch (Exception ex)
        {
            Debug.LogError($"ShowFileDialog error: {ex.Message}");
            // エラー発生時にも確保したメモリを解放する
            if (contentTypesPtr != IntPtr.Zero) Marshal.FreeHGlobal(contentTypesPtr);
            if (stringPointers != null)
            {
                foreach (var ptr in stringPointers)
                {
                    if (ptr != IntPtr.Zero) Marshal.FreeHGlobal(ptr);
                }
            }
            FileDialogResult?.Invoke(null, 0, "", false, false, $"Internal error: {ex.Message}");
        }
    }

    public void ShowMultiFileDialog(string title, string message, string[] allowedContentTypes = null, string directoryPath = "")
    {
        Debug.Log($"ShowMultiFileDialog called with title: {title}, message: {message}, allowedContentTypes: {allowedContentTypes?.Length}, directoryPath: {directoryPath}");
        if (string.IsNullOrEmpty(title))
        {
            Debug.LogError("Title cannot be null or empty.");
            MultiFileDialogResult?.Invoke(null, 0, "", false, false, "Title cannot be null or empty.");
            return;
        }

        IntPtr contentTypesPtr = IntPtr.Zero;
        IntPtr[] stringPointers = null;
        int contentTypesCount = allowedContentTypes?.Length ?? 0;

        try
        {
            if (contentTypesCount > 0)
            {
                stringPointers = new IntPtr[contentTypesCount];
                for (int i = 0; i < contentTypesCount; i++)
                {
                    byte[] utf8Bytes = System.Text.Encoding.UTF8.GetBytes(allowedContentTypes[i]);
                    stringPointers[i] = Marshal.AllocHGlobal(utf8Bytes.Length + 1);
                    Marshal.Copy(utf8Bytes, 0, stringPointers[i], utf8Bytes.Length);
                    Marshal.WriteByte(stringPointers[i], utf8Bytes.Length, 0); // null終端文字
                }
                contentTypesPtr = Marshal.AllocHGlobal(IntPtr.Size * contentTypesCount);
                Marshal.Copy(stringPointers, 0, contentTypesPtr, contentTypesCount);
            }

            MultiFileDialogCallback multiFileDialogCallback = (filePathsPtr, fileCount, directoryURL, isCancelled, isSuccess, errorMessage) =>
            {
                // ポインタからC#のstring[]に即時変換
                string[] filePaths = new string[fileCount];
                if (filePathsPtr != IntPtr.Zero && fileCount > 0)
                {
                    IntPtr[] ptrArray = new IntPtr[fileCount];
                    Marshal.Copy(filePathsPtr, ptrArray, 0, fileCount);
                    for (int i = 0; i < fileCount; i++)
                    {
                        filePaths[i] = Marshal.PtrToStringUTF8(ptrArray[i]);
                    }
                }

                // マネージドオブジェクト（C#のstring[]）をキューに入れる
                UnityMainThreadDispatcher.Instance.Enqueue(() =>
                {
                    try
                    {
                        MultiFileDialogResult?.Invoke(filePaths, fileCount, directoryURL, isCancelled, isSuccess, errorMessage);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"MultiFileDialogResult callback error: {ex.Message}");
                    }
                    finally
                    {
                        // ★重要：ここで引数用のメモリを解放する
                        if (contentTypesPtr != IntPtr.Zero) Marshal.FreeHGlobal(contentTypesPtr);
                        if (stringPointers != null)
                        {
                            foreach (var ptr in stringPointers)
                            {
                                if (ptr != IntPtr.Zero) Marshal.FreeHGlobal(ptr);
                            }
                        }
                        Debug.Log("Memory for allowedContentTypes freed in MultiFileDialog callback.");
                    }
                });
            };

            showMultiFileDialog(title, message, contentTypesPtr, contentTypesCount, directoryPath, multiFileDialogCallback);
        }
        catch (Exception ex)
        {
            Debug.LogError($"ShowMultiFileDialog error: {ex.Message}");
            // エラー発生時にも確保したメモリを解放する
            if (contentTypesPtr != IntPtr.Zero) Marshal.FreeHGlobal(contentTypesPtr);
            if (stringPointers != null)
            {
                foreach (var ptr in stringPointers)
                {
                    if (ptr != IntPtr.Zero) Marshal.FreeHGlobal(ptr);
                }
            }
            MultiFileDialogResult?.Invoke(null, 0, "", false, false, $"Internal error: {ex.Message}");
        }
    }

    public void ShowFolderDialog(string title, string message, string directoryPath = "")
    {
        Debug.Log($"ShowFolderDialog called with title: {title}, message: {message}, directoryPath: {directoryPath}");
        if (string.IsNullOrEmpty(title))
        {
            Debug.LogError("Title cannot be null or empty.");
            FolderDialogResult?.Invoke(null, 0, "", false, false, "Title cannot be null or empty.");
            return;
        }

        try
        {
            FolderDialogCallback folderDialogCallback = (folderPathsPtr, folderCount, directoryURL, isCancelled, isSuccess, errorMessage) =>
            {
                // ポインタからC#のstring[]に即時変換
                string[] folderPaths = new string[folderCount];
                if (folderPathsPtr != IntPtr.Zero && folderCount > 0)
                {
                    IntPtr[] ptrArray = new IntPtr[folderCount];
                    Marshal.Copy(folderPathsPtr, ptrArray, 0, folderCount);

                    for (int i = 0; i < folderCount; i++)
                    {
                        folderPaths[i] = Marshal.PtrToStringUTF8(ptrArray[i]);
                    }
                }

                // マネージドオブジェクト（C#のstring[]）をキューに入れる
                UnityMainThreadDispatcher.Instance.Enqueue(() =>
                {
                    try
                    {
                        FolderDialogResult?.Invoke(folderPaths, folderCount, directoryURL, isCancelled, isSuccess, errorMessage);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"FolderDialogResult callback error: {ex.Message}");
                    }
                });
            };

            showFolderDialog(title, message, directoryPath, folderDialogCallback);
        }
        catch (Exception ex)
        {
            Debug.LogError($"ShowFolderDialog error: {ex.Message}");
            FolderDialogResult?.Invoke(null, 0, "", false, false, $"Internal error: {ex.Message}");
        }
    }

    public void ShowMultiFolderDialog(string title, string message, string directoryPath = "")
    {
        Debug.Log($"ShowMultiFolderDialog called with title: {title}, message: {message}, directoryPath: {directoryPath}");
        if (string.IsNullOrEmpty(title))
        {
            Debug.LogError("Title cannot be null or empty.");
            MultiFolderDialogResult?.Invoke(null, 0, "", false, false, "Title cannot be null or empty.");
            return;
        }

        try
        {
            MultiFolderDialogCallback multiFolderDialogCallback = (folderPathsPtr, folderCount, directoryURL, isCancelled, isSuccess, errorMessage) =>
            {
                // ポインタからC#のstring[]に即時変換
                string[] folderPaths = new string[folderCount];
                if (folderPathsPtr != IntPtr.Zero && folderCount > 0)
                {
                    IntPtr[] ptrArray = new IntPtr[folderCount];
                    Marshal.Copy(folderPathsPtr, ptrArray, 0, folderCount);

                    for (int i = 0; i < folderCount; i++)
                    {
                        folderPaths[i] = Marshal.PtrToStringUTF8(ptrArray[i]);
                    }
                }

                // マネージドオブジェクト（C#のstring[]）をキューに入れる
                UnityMainThreadDispatcher.Instance.Enqueue(() =>
                {
                    try
                    {
                        MultiFolderDialogResult?.Invoke(folderPaths, folderCount, directoryURL, isCancelled, isSuccess, errorMessage);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"MultiFolderDialogResult callback error: {ex.Message}");
                    }
                });
            };

            showMultiFolderDialog(title, message, directoryPath, multiFolderDialogCallback);
        }
        catch (Exception ex)
        {
            Debug.LogError($"ShowMultiFolderDialog error: {ex.Message}");
            MultiFolderDialogResult?.Invoke(null, 0, "", false, false, $"Internal error: {ex.Message}");
        }
    }

    public void ShowSaveFileDialog(string title, string message, string defaultFileName = "", string[] allowedContentTypes = null, string directoryPath = "")
    {
        Debug.Log($"ShowSaveFileDialog called with title: {title}, message: {message}, defaultFileName: {defaultFileName}, allowedContentTypes: {allowedContentTypes?.Length}, directoryPath: {directoryPath}");
        if (string.IsNullOrEmpty(title))
        {
            Debug.LogError("Title cannot be null or empty.");
            SaveFileDialogResult?.Invoke("", 0, "", false, false, "Title cannot be null or empty.");
            return;
        }

        IntPtr contentTypesPtr = IntPtr.Zero;
        IntPtr[] stringPointers = null;
        int contentTypesCount = allowedContentTypes?.Length ?? 0;

        try
        {
            if (contentTypesCount > 0)
            {
                stringPointers = new IntPtr[contentTypesCount];
                for (int i = 0; i < contentTypesCount; i++)
                {
                    byte[] utf8Bytes = System.Text.Encoding.UTF8.GetBytes(allowedContentTypes[i]);
                    stringPointers[i] = Marshal.AllocHGlobal(utf8Bytes.Length + 1);
                    Marshal.Copy(utf8Bytes, 0, stringPointers[i], utf8Bytes.Length);
                    Marshal.WriteByte(stringPointers[i], utf8Bytes.Length, 0); // null終端文字
                }
                contentTypesPtr = Marshal.AllocHGlobal(IntPtr.Size * contentTypesCount);
                Marshal.Copy(stringPointers, 0, contentTypesPtr, contentTypesCount);
            }

            SaveFileDialogCallback saveFileDialogCallback = (filePath, fileCount, directoryURL, isCancelled, isSuccess, errorMessage) =>
            {
                // ネイティブからのコールバックはメインスレッドではない可能性があるため、
                // Unityのメインスレッドでイベント発行とメモリ解放を行う
                UnityMainThreadDispatcher.Instance.Enqueue(() =>
                {
                    try
                    {
                        SaveFileDialogResult?.Invoke(filePath, fileCount, directoryURL, isCancelled, isSuccess, errorMessage);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"SaveFileDialogResult callback error: {ex.Message}");
                    }
                    finally
                    {
                        // ★重要：ここで引数用のメモリを解放する
                        if (contentTypesPtr != IntPtr.Zero) Marshal.FreeHGlobal(contentTypesPtr);
                        if (stringPointers != null)
                        {
                            foreach (var ptr in stringPointers)
                            {
                                if (ptr != IntPtr.Zero) Marshal.FreeHGlobal(ptr);
                            }
                        }
                        Debug.Log("Memory for allowedContentTypes freed in SaveFileDialog callback.");
                    }
                });
            };

            showSaveFileDialog(title, message, defaultFileName, contentTypesPtr, contentTypesCount, directoryPath, saveFileDialogCallback);
        }
        catch (Exception ex)
        {
            Debug.LogError($"ShowSaveFileDialog error: {ex.Message}");
            // エラー発生時にも確保したメモリを解放する
            if (contentTypesPtr != IntPtr.Zero) Marshal.FreeHGlobal(contentTypesPtr);
            if (stringPointers != null)
            {
                foreach (var ptr in stringPointers)
                {
                    if (ptr != IntPtr.Zero) Marshal.FreeHGlobal(ptr);
                }
            }
            SaveFileDialogResult?.Invoke("", 0, "", false, false, $"Internal error: {ex.Message}");
        }
    }
}
#endif