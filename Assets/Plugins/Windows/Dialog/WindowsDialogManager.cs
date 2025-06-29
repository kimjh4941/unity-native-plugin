#if UNITY_STANDALONE_WIN
using UnityEngine;
using System.Runtime.InteropServices;
using System;
using System.Text;
using System.Collections;

public class WindowsDialogManager : MonoBehaviour
{
    private static WindowsDialogManager _instance;

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

    public event Action<int, uint> AlertDialogResult;

    public event Action<string, uint> FileDialogResult;

    public event Action<ArrayList, uint> MultiFileDialogResult;

    public event Action<string, uint> SaveFileDialogResult;

    public event Action<string, uint> FolderDialogResult;

    public event Action<ArrayList, uint> MultiFolderDialogResult;

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

    [DllImport("WindowsLibrary.dll", CharSet = CharSet.Unicode)]
    private static extern int showAlertDialog(
        [MarshalAs(UnmanagedType.LPWStr)] string title,
        [MarshalAs(UnmanagedType.LPWStr)] string message,
        uint buttons,
        uint icon,
        uint defbutton,
        uint options,
        out uint pError
    );

    [DllImport("WindowsLibrary.dll", CharSet = CharSet.Unicode)]
    private static extern bool showFileDialog(
        [MarshalAs(UnmanagedType.LPWStr)] StringBuilder buffer,
        uint buffer_size,
        [MarshalAs(UnmanagedType.LPWStr)] string filter,
        out uint pError
    );

    [DllImport("WindowsLibrary.dll", CharSet = CharSet.Unicode)]
    private static extern int showMultiFileDialog(
        IntPtr buffer,
        uint buffer_size,
        [MarshalAs(UnmanagedType.LPWStr)] string filter,
        out uint pError
    );

    [DllImport("WindowsLibrary.dll", CharSet = CharSet.Unicode)]
    private static extern bool showSaveFileDialog(
        [MarshalAs(UnmanagedType.LPWStr)] StringBuilder buffer,
        uint buffer_size,
        [MarshalAs(UnmanagedType.LPWStr)] string filter,
        [MarshalAs(UnmanagedType.LPWStr)] string def_ext,
        out uint pError
    );

    [DllImport("WindowsLibrary.dll", CharSet = CharSet.Unicode)]
    private static extern bool showFolderDialog(
        [MarshalAs(UnmanagedType.LPWStr)] StringBuilder buffer,
        uint buffer_size,
        [MarshalAs(UnmanagedType.LPWStr)] string title,
        out uint pError
    );

    [DllImport("WindowsLibrary.dll", CharSet = CharSet.Unicode)]
    private static extern int showMultiFolderDialog(
        IntPtr buffer,
        uint buffer_size,
        [MarshalAs(UnmanagedType.LPWStr)] string title,
        out uint pError
    );

    public void ShowDialog(
        string title,
        string message,
        uint buttons,
        uint icon,
        uint defbutton,
        uint options
    )
    {
        Debug.Log("ShowDialog called with " +
            "title: " + title +
            ", message: " + message +
            ", buttons: " + buttons +
            ", icon: " + icon +
            ", defbutton: " + defbutton +
            ", options: " + options
        );
        int result = showAlertDialog(
            title,
            message,
            buttons,
            icon,
            defbutton,
            options,
            out uint errorCode
        );
        if (result == 0)
        {
            Debug.LogError($"ShowDialog failed with result: {result}, error code: 0x{errorCode:X8}");
        }
        else
        {
            Debug.Log($"ShowDialog succeeded with result: {result}, error code: 0x{errorCode:X8}");
        }
        AlertDialogResult?.Invoke(result, errorCode);
    }

    public void ShowFileDialog(
        StringBuilder buffer,
        uint buffer_size,
        string filter
    )
    {
        Debug.Log("ShowFileDialog called with " +
            "buffer size: " + buffer_size +
            ", filter: " + filter
        );
        // シングルファイル選択
        bool result = showFileDialog(
            buffer,
            buffer_size,
            filter,
            out uint errorCode);
        if (result)
        {
            Debug.Log($"ShowFileDialog 選択ファイル: {buffer}, error code: 0x{errorCode:X8}");
            FileDialogResult?.Invoke(buffer.ToString(), errorCode);
        }
        else
        {
            Debug.LogError($"ShowFileDialog ファイル選択でエラーが発生しました。error code: 0x{errorCode:X8}");
            FileDialogResult?.Invoke(null, errorCode);
        }
    }

    public void ShowMultiFileDialog(
        uint buffer_size,
        string filter
    )
    {
        Debug.Log("ShowMultiFileDialog called with " +
            "buffer size: " + buffer_size +
            ", filter: " + filter
        );

        // バッファサイズ分のアンマネージドメモリを確保
        IntPtr unmanagedBuffer = Marshal.AllocHGlobal((int)buffer_size * 2);
        try
        {
            // 複数ファイル選択
            int count = showMultiFileDialog(
                unmanagedBuffer,
                buffer_size,
                filter,
                out uint errorCode
            );
            Debug.Log("ShowMultiFileDialog returned count: " + count);
            if (count > 0)
            {
                // バッファ全体をバイト配列として取得
                byte[] raw = new byte[buffer_size * 2];
                Marshal.Copy(unmanagedBuffer, raw, 0, raw.Length);

                // UTF-16としてデコード
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
                    Debug.Log("ShowMultiFileDialog 選択ファイル: " + parts[0]);
                    ArrayList selectedFiles = new ArrayList { parts[0] };
                    MultiFileDialogResult?.Invoke(selectedFiles, errorCode);
                }
                else
                {
                    string folder = parts[0];
                    Debug.Log("ShowMultiFileDialog 選択ファイルのフォルダ(0): " + parts[0]);
                    ArrayList selectedFiles = new ArrayList();
                    for (int i = 1; i < count; i++)
                    {
                        Debug.Log($"ShowMultiFileDialog 選択ファイル({i}): {folder}\\{parts[i]}");
                        selectedFiles.Add(folder + "\\" + parts[i]);
                    }
                    MultiFileDialogResult?.Invoke(selectedFiles, errorCode);
                }
            }
            else
            {
                if (count < 0)
                {
                    Debug.LogError($"ShowMultiFileDialog 複数ファイル選択でエラーが発生しました。error code: 0x{errorCode:X8}");
                    MultiFileDialogResult?.Invoke(null, errorCode);
                }
                else
                {
                    Debug.Log($"ShowMultiFileDialog 複数ファイル選択がキャンセルされました。error code: 0x{errorCode:X8}");
                    MultiFileDialogResult?.Invoke(new ArrayList(), errorCode);
                }
            }
        }
        finally
        {
            Marshal.FreeHGlobal(unmanagedBuffer);
        }
    }

    public void ShowSaveFileDialog(
        StringBuilder buffer,
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
        // 名前を付けて保存
        bool result = showSaveFileDialog(
            buffer,
            buffer_size,
            filter,
            def_ext,
            out uint errorCode
        );
        if (result)
        {
            Debug.Log($"ShowSaveFileDialog 保存ファイル: {buffer}, error code: 0x{errorCode:X8}");
            SaveFileDialogResult?.Invoke(buffer.ToString(), errorCode);
        }
        else
        {
            Debug.LogError($"ShowSaveFileDialog 保存ダイアログでエラーが発生しました。error code: 0x{errorCode:X8}");
            SaveFileDialogResult?.Invoke(null, errorCode);
        }
    }

    public void ShowFolderDialog(
        StringBuilder buffer,
        uint buffer_size,
        string title
    )
    {
        Debug.Log("ShowFolderDialog called with " +
            "buffer size: " + buffer_size +
            ", title: " + title
        );

        // フォルダ選択
        bool result = showFolderDialog(
            buffer,
            buffer_size,
            title,
            out uint errorCode
        );
        if (result)
        {
            Debug.Log($"ShowFolderDialog 選択フォルダ: {buffer}, error code: 0x{errorCode:X8}");
            FolderDialogResult?.Invoke(buffer.ToString(), errorCode);
        }
        else
        {
            Debug.LogError($"ShowFolderDialog フォルダ選択でエラーが発生しました。error code: 0x{errorCode:X8}");
            FolderDialogResult?.Invoke(null, errorCode);
        }
    }
    
    public void ShowMultiFolderDialog(
        uint buffer_size,
        string title
    )
    {
        Debug.Log("ShowMultiFolderDialog called with " +
            "buffer size: " + buffer_size +
            ", title: " + title
        );

        // バッファサイズ分のアンマネージドメモリを確保
        IntPtr unmanagedBuffer = Marshal.AllocHGlobal((int)buffer_size * 2);
        try
        {
            // 複数フォルダ選択
            int count = showMultiFolderDialog(
                unmanagedBuffer,
                buffer_size,
                title,
                out uint errorCode
            );
            Debug.Log("ShowMultiFolderDialog returned count: " + count);
            if (count > 0)
            {
                // バッファ全体をバイト配列として取得
                byte[] raw = new byte[buffer_size * 2];
                Marshal.Copy(unmanagedBuffer, raw, 0, raw.Length);

                // UTF-16としてデコード
                string all = System.Text.Encoding.Unicode.GetString(raw);
                string[] parts = all.Split('\0');
                Debug.Log("ShowMultiFolderDialog Raw buffer bytes: " + BitConverter.ToString(raw));
                Debug.Log($"ShowMultiFolderDialog buffer string: {all}");

                Debug.Log("ShowMultiFolderDialog parts.Length: " + parts.Length);
                for (int i = 0; i < parts.Length; i++)
                {
                    Debug.Log($"ShowMultiFolderDialog parts[{i}]: {parts[i]}");
                }

                // フォルダパスのリストを作成
                ArrayList selectedFolders = new ArrayList();
                for (int i = 0; i < count; i++)
                {
                    if (!string.IsNullOrEmpty(parts[i]))
                    {
                        Debug.Log($"ShowMultiFolderDialog 選択フォルダ({i}): {parts[i]}");
                        selectedFolders.Add(parts[i]);
                    }
                }
                MultiFolderDialogResult?.Invoke(selectedFolders, errorCode);
            }
            else
            {
                if (count < 0)
                {
                    Debug.LogError($"ShowMultiFolderDialog 複数フォルダ選択でエラーが発生しました。error code: 0x{errorCode:X8}");
                    MultiFolderDialogResult?.Invoke(null, errorCode);
                }
                else
                {
                    Debug.Log($"ShowMultiFolderDialog 複数フォルダ選択がキャンセルされました。error code: 0x{errorCode:X8}");
                    MultiFolderDialogResult?.Invoke(new ArrayList(), errorCode);
                }
            }
        }
        finally
        {
            Marshal.FreeHGlobal(unmanagedBuffer);
        }
    }
}
#endif
