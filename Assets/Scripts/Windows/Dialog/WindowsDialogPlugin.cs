using System.Runtime.InteropServices;
using UnityEngine;

public class WindowsDialogPlugin : MonoBehaviour
{
    public void ShowDialog()
    {
        // WindowsDialogManagerを使用してダイアログを表示
        WindowsDialogManager.Instance.ShowDialog(
            "Native Windows Dialog",
            "This is a native Windows dialog!",
            Win32MessageBox.MB_OKCANCEL,
            Win32MessageBox.MB_ICONINFORMATION,
            Win32MessageBox.MB_DEFBUTTON2,
            Win32MessageBox.MB_APPLMODAL
        );
        WindowsDialogManager.Instance.AlertDialogResult += result => Debug.Log("AlertDialogResult: " + result);
    }
}
