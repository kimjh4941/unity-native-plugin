#if UNITY_STANDALONE_WIN
using System.Runtime.InteropServices;
using UnityEngine;
using System.Text;

public class WindowsDialogPlugin : MonoBehaviour
{
    void Start()
    {
        // WindowsDialogManager.Instance.AlertDialogResult += (result, errorCode) => Debug.Log("AlertDialogResult result: " + result + ", errorCode: " + errorCode);
        // WindowsDialogManager.Instance.FileDialogResult += (result, errorCode) => Debug.Log("FileDialogResult: " + result + ", errorCode: " + errorCode);
        // WindowsDialogManager.Instance.MultiFileDialogResult += (result, errorCode) => Debug.Log("MultiFileDialogResult: " + result + ", errorCode: " + errorCode);
        // WindowsDialogManager.Instance.SaveFileDialogResult += (result, errorCode) => Debug.Log("SaveFileDialogResult: " + result + ", errorCode: " + errorCode);
        // WindowsDialogManager.Instance.FolderDialogResult += (result, errorCode) => Debug.Log("FolderDialogResult: " + result + ", errorCode: " + errorCode);
        // WindowsDialogManager.Instance.MultiFolderDialogResult += (result, errorCode) => Debug.Log("MultiFolderDialogResult: " + result + ", errorCode: " + errorCode);
    }

    void OnDestroy()
    {
        // WindowsDialogManager.Instance.AlertDialogResult -= (result, errorCode) => Debug.Log("AlertDialogResult result: " + result + ", errorCode: " + errorCode);
        // WindowsDialogManager.Instance.FileDialogResult -= (result, errorCode) => Debug.Log("FileDialogResult: " + result + ", errorCode: " + errorCode);
        // WindowsDialogManager.Instance.MultiFileDialogResult -= (result, errorCode) => Debug.Log("MultiFileDialogResult: " + result + ", errorCode: " + errorCode);
        // WindowsDialogManager.Instance.SaveFileDialogResult -= (result, errorCode) => Debug.Log("SaveFileDialogResult: " + result + ", errorCode: " + errorCode);
        // WindowsDialogManager.Instance.FolderDialogResult -= (result, errorCode) => Debug.Log("FolderDialogResult: " + result + ", errorCode: " + errorCode);
        // WindowsDialogManager.Instance.MultiFolderDialogResult -= (result, errorCode) => Debug.Log("MultiFolderDialogResult: " + result + ", errorCode: " + errorCode);
    }
    
    public void ShowDialog()
    {
        WindowsDialogManager.Instance.ShowDialog(
            "Native Windows Dialog",
            "This is a native Windows dialog!",
            Win32MessageBox.MB_OKCANCEL,
            Win32MessageBox.MB_ICONINFORMATION,
            Win32MessageBox.MB_DEFBUTTON2,
            Win32MessageBox.MB_APPLMODAL
        );
    }

    // public void ShowFileDialog()
    // {
    //     // シングルファイル選択
    //     var buffer = new StringBuilder(260);
    //     string filter = "テキストファイル\0*.txt\0すべてのファイル\0*.*\0";
    //     WindowsDialogManager.Instance.ShowFileDialog(buffer, (uint)buffer.Capacity, filter);
    // }

    // public void ShowMultiFileDialog()
    // {
    //     string filter = "テキストファイル\0*.txt\0すべてのファイル\0*.*\0";
    //     WindowsDialogManager.Instance.ShowMultiFileDialog(4096, filter);
    // }

    // public void ShowSaveFileDialog()
    // {
    //     // ファイル保存ダイアログ
    //     var buffer = new StringBuilder(260);
    //     string filter = "テキストファイル\0*.txt\0すべてのファイル\0*.*\0";
    //     string def_ext = "txt"; // デフォルト拡張子
    //     WindowsDialogManager.Instance.ShowSaveFileDialog(buffer, (uint)buffer.Capacity, filter, def_ext);
    // }

    // public void ShowFolderDialog()
    // {
    //     // シングルフォルダ選択
    //     var buffer = new StringBuilder(260);
    //     string title = "フォルダを選択してください";
    //     WindowsDialogManager.Instance.ShowFolderDialog(buffer, (uint)buffer.Capacity, title);
    // }
    
    // public void ShowMultiFolderDialog()
    // {
    //     // 複数フォルダ選択
    //     string title = "複数のフォルダを選択してください";
    //     WindowsDialogManager.Instance.ShowMultiFolderDialog(4096, title);
    // }
}
#endif