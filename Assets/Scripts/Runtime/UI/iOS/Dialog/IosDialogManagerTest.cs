#nullable enable

using UnityEngine;
#if UNITY_IOS && !UNITY_EDITOR
using JonghyunKim.NativeToolkit.Runtime.Dialog;
#endif

public class IosDialogManagerTest : MonoBehaviour
{
    // Called when the script instance is being loaded
    private void Awake()
    {
        Debug.Log("[IosDialogManagerTest] Awake called.");
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Start()
    {
        Debug.Log("[IosDialogManagerTest] Start called.");
    }

    // Update is called once per frame
    private void Update()
    { }

    // Called when the MonoBehaviour will be destroyed
    private void OnDestroy()
    {
        Debug.Log("[IosDialogManagerTest] OnDestroy called.");
    }

    // Called when the button is clicked
    public void OnButtonClicked()
    {
        Debug.Log("[IosDialogManagerTest] OnButtonClicked called.");
#if UNITY_IOS && !UNITY_EDITOR
        string title = "Hello from Unity";
        string message = "This is a native iOS dialog!";
        string buttonText = "OK";
        IosDialogManager.Instance.ShowDialog(title, message, buttonText);
#endif
    }
}
