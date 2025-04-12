using System.Runtime.InteropServices;
using UnityEngine;

public class NativePluginMac : MonoBehaviour
{
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void ShowNativeAlert(string title, string message);
#endif

    public void ShowAlert()
    {
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        // macOSネイティブアラートを表示
        ShowNativeAlert("Hello from Unity kim", "This is a native macOS alert implemented in Swift!");
#else
        Debug.Log("ShowAlert() is only supported on macOS standalone builds.");
#endif
    }
}