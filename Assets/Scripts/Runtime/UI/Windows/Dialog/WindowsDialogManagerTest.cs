#nullable enable

using UnityEngine;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using JonghyunKim.NativeToolkit.Runtime.Dialog;
#endif

public class WindowsDialogManagerTest : MonoBehaviour
{
    // Called when the script instance is being loaded
    private void Awake()
    {
        Debug.Log("[WindowsDialogManagerTest] Awake called.");
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Start()
    {
        Debug.Log("[WindowsDialogManagerTest] Start called.");
    }

    // Update is called once per frame
    private void Update()
    { }

    // Called when the MonoBehaviour will be destroyed
    private void OnDestroy()
    {
        Debug.Log("[WindowsDialogManagerTest] OnDestroy called.");
    }

    // Called when the button is clicked
    public void OnButtonClicked()
    {
        Debug.Log("[WindowsDialogManagerTest] OnButtonClicked called.");
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        string title = "Native Windows Dialog";
        string message = "This is a native Windows dialog!";
        uint buttons = Win32MessageBox.MB_OKCANCEL;
        uint icon = Win32MessageBox.MB_ICONINFORMATION;
        uint defbutton = Win32MessageBox.MB_DEFBUTTON2;
        uint options = Win32MessageBox.MB_APPLMODAL;
        WindowsDialogManager.Instance.ShowDialog(
            title,
            message,
            buttons,
            icon,
            defbutton,
            options
        );
#endif
    }
}
