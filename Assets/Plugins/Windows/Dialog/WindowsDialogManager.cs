#if UNITY_STANDALONE_WIN
using UnityEngine;
using System.Runtime.InteropServices;
using System;

public class WindowsDialogManager : MonoBehaviour
{
    private static WindowsDialogManager _instance;

    public static WindowsDialogManager Instance
    {
        get
        {
            if (_instance == null)
            {
                Debug.Log("Creating new instance of WindowsDialogManager");
                GameObject singletonObject = new GameObject("WindowsDialogManager");
                _instance = singletonObject.AddComponent<WindowsDialogManager>();
                DontDestroyOnLoad(singletonObject);
            }
            return _instance;
        }
    }

    public event Action<int> AlertDialogResult;

    private void Awake()
    {
        Debug.Log("Awake");
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    [DllImport("WindowsLibrary.dll", CharSet = CharSet.Unicode)]
    private static extern int ShowAlertDialog(
        [MarshalAs(UnmanagedType.LPWStr)] string title,
        [MarshalAs(UnmanagedType.LPWStr)] string message,
        uint buttons,
        uint icon,
        uint defbutton,
        uint options
    );

    public void ShowDialog(
        string title,
        string message,
        uint buttons,
        uint icon,
        uint defbutton,
        uint options
    )
    {
        Debug.Log("ShowDialog called with " +
            "title: " + title +
            ", message: " + message +
            ", buttons: " + buttons +
            ", icon: " + icon +
            ", defbutton: " + defbutton +
            ", options: " + options
        );
        int result = ShowAlertDialog(
            title,
            message,
            buttons,
            icon,
            defbutton,
            options
        );
        AlertDialogResult?.Invoke(result);
    }
}
#endif
