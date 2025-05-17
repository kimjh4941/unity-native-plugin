using System.Runtime.InteropServices;
using UnityEngine;

public class MacDialogPlugin : MonoBehaviour
{
    public void ShowDialog()
    {
        // MacDialogManagerを使用してダイアログを表示
        MacDialogManager.Instance.ShowDialog("Hello from Unity", "This is a native macOS dialog!");
    }
}
