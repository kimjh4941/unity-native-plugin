using System.Runtime.InteropServices;
using UnityEngine;

public class NativePluginAndroid : MonoBehaviour
{
    public void ShowDialog()
    {
        // NativeDialogManagerを使用してダイアログを表示
        NativeDialogManager.Instance.ShowDialog("Hello from Unity", "This is a native Android dialog!");
    }
}