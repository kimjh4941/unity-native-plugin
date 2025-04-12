using System.Runtime.InteropServices;
using UnityEngine;

public class NativePluginIos : MonoBehaviour
{
#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void ShowNativeAlert(string title, string message);
#endif

    public void ShowAlert()
    {
#if UNITY_IOS && !UNITY_EDITOR
        ShowNativeAlert("Hello from Unity", "This is a native iOS alert implemented in Swift!");
#else
        Debug.Log("ShowAlert() called. This will only work on an iOS device.");
#endif
    }
}