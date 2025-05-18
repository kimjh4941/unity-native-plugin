#if UNITY_ANDROID
using System.Runtime.InteropServices;
using UnityEngine;

public class AndroidDialogPlugin : MonoBehaviour
{
    public void ShowDialog()
    {
        // NativeDialogManagerを使用してダイアログを表示
        AndroidDialogManager.Instance.ShowDialog("Hello from Unity", "This is a native Android dialog!");
    }
}
#endif