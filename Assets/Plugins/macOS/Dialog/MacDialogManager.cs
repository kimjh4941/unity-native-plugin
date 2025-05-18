#if UNITY_STANDALONE_OSX
using UnityEngine;
using System.Runtime.InteropServices;

public class MacDialogManager : MonoBehaviour
{
    private static MacDialogManager _instance;

    public static MacDialogManager Instance
    {
        get
        {
            if (_instance == null)
            {
                Debug.Log("Creating new instance of MacDialogManager");
                GameObject singletonObject = new GameObject("MacDialogManager");
                _instance = singletonObject.AddComponent<MacDialogManager>();
                DontDestroyOnLoad(singletonObject);
            }
            return _instance;
        }
    }

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

    public delegate void DialogManagerCallback(string result);

    [AOT.MonoPInvokeCallback(typeof(DialogManagerCallback))]
    public static void OnAlertCallback(string result)
    {
        Debug.Log("Alert callback received: " + result);
        // 必要ならイベント発火など
    }

    [DllImport("__Internal")]
    private static extern void showDialog(string title, string message, DialogManagerCallback callback);

    public void ShowDialog(string title, string message)
    {
        Debug.Log("ShowDialog called with title: " + title + ", message: " + message);
        showDialog(title, message, OnAlertCallback);
    }
}
#endif