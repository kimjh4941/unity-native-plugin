#nullable enable

using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using JonghyunKim.NativeToolkit.Runtime.Dialog;
#endif

public class AndroidDialogManagerTest : MonoBehaviour
{
    // Called when the script instance is being loaded
    private void Awake()
    {
        Debug.Log("[AndroidDialogManagerTest] Awake called.");
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Start()
    {
        Debug.Log("[AndroidDialogManagerTest] Start called.");
    }

    // Update is called once per frame
    private void Update()
    { }

    // Called when the MonoBehaviour will be destroyed
    private void OnDestroy()
    {
        Debug.Log("[AndroidDialogManagerTest] OnDestroy called.");
    }

    // Called when the button is clicked
    public void OnButtonClicked()
    {
        Debug.Log("[AndroidDialogManagerTest] OnButtonClicked called.");
#if UNITY_ANDROID && !UNITY_EDITOR
        string title = "Hello from Unity";
        string message = "This is a native Android dialog!";
        string buttonText = "OK";
        bool cancelableOnTouchOutside = false;
        bool cancelable = false;
        AndroidDialogManager.Instance.ShowDialog(
            title,
            message,
            buttonText,
            cancelableOnTouchOutside,
            cancelable
        );
#endif
    }
}
