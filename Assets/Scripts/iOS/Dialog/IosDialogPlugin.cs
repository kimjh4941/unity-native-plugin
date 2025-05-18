#if UNITY_IOS
using System.Runtime.InteropServices;
using UnityEngine;

public class IosDialogPlugin : MonoBehaviour
{
    public void ShowDialog()
    {
        // NativeDialogManagerを使用してダイアログを表示
        IosDialogManager.Instance.ShowDialog("Hello from Unity", "This is a native iOS dialog!");
    }
}
#endif