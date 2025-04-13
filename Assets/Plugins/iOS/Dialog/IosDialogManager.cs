using UnityEngine;
using System.Runtime.InteropServices;

public class IosDialogManager : MonoBehaviour
{
    private static IosDialogManager _instance;

    public static IosDialogManager Instance
    {
        get
        {
            if (_instance == null)
            {
                Debug.Log("Creating new instance of IosDialogManager");
                GameObject singletonObject = new GameObject("IosDialogManager");
                _instance = singletonObject.AddComponent<IosDialogManager>();
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

    [DllImport("__Internal")]
    private static extern void showDialog(string title, string message, string callbackObjectName, string callbackMethodName);

    public void ShowDialog(string title, string message)
    {
        Debug.Log("ShowDialog called with title: " + title + ", message: " + message);
        showDialog(title, message, gameObject.name, "OnAlertCallback");
    }

    private void OnAlertCallback(string result)
    {
        Debug.Log("Alert callback received: " + result);
    }

    // iOS側のセレクタ名に対応するメソッド
    public void sendMessageToGameObject(string callbackObjectName, string callbackMethodName)
    {
        Debug.Log($"sendMessageToGameObject called with callbackObjectName: {callbackObjectName}, callbackMethodName: {callbackMethodName}");

        // 指定されたGameObjectを名前で検索
        GameObject callbackObject = GameObject.Find(callbackObjectName);
        if (callbackObject != null)
        {
            // 指定されたメソッドをGameObject上で呼び出す
            callbackObject.SendMessage(callbackMethodName, "OK", SendMessageOptions.DontRequireReceiver);
        }
        else
        {
            Debug.LogError($"GameObject with name {callbackObjectName} not found.");
        }
    }
}
