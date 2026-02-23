#nullable enable

using UnityEngine;
using System.Collections.Generic;
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
using JonghyunKim.NativeToolkit.Runtime.Common;
using JonghyunKim.NativeToolkit.Runtime.Dialog;
#endif

public class MacDialogManagerTest : MonoBehaviour
{
    // Called when the script instance is being loaded
    private void Awake()
    {
        Debug.Log("[MacDialogManagerTest] Awake called.");
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Start()
    {
        Debug.Log("[MacDialogManagerTest] Start called.");
    }

    // Update is called once per frame
    private void Update()
    { }

    // Called when the MonoBehaviour will be destroyed
    private void OnDestroy()
    {
        Debug.Log("[MacDialogManagerTest] OnDestroy called.");
    }

    // Called when the button is clicked
    public void OnButtonClicked()
    {
        Debug.Log("[MacDialogManagerTest] OnButtonClicked called.");
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        string title = "Hello from Unity";
        string message = "This is a native macOS dialog!";
        DialogButton[] buttons = {
            new DialogButton(title: "OK", isDefault: true),
            new DialogButton(title: "Cancel", keyEquivalent: "\u001b"),
            new DialogButton(title: "Delete", keyEquivalent: "d")
        };
        DialogOptions options = new DialogOptions(
            alertStyle: DialogOptions.AlertStyle.Informational,
            showsHelp: true,
            showsSuppressionButton: true,
            suppressionButtonTitle: "Don't show this again",
            icon: new IconConfiguration(
                type: IconConfiguration.IconType.SystemSymbol,
                value: "info.square.fill",
                mode: IconConfiguration.RenderingMode.Palette,
                colors: new List<string> { "white", "systemblue", "systemblue" },
                size: 64f,
                weight: IconConfiguration.Weight.Regular,
                scale: IconConfiguration.Scale.Medium
            )
        );
        MacDialogManager.Instance.ShowDialog(title, message, buttons, options);
#endif
    }
}
