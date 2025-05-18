#if UNITY_ANDROID
using UnityEngine;

public class AndroidDialogManager : MonoBehaviour
{
    private static AndroidDialogManager _instance;

    public static AndroidDialogManager Instance
    {
        get
        {
            if (_instance == null)
            {
                Debug.Log("Creating new instance of AndroidDialogManager");
                // シーン内に存在しない場合、新しいGameObjectを作成してアタッチ
                GameObject singletonObject = new GameObject("AndroidDialogManager");
                _instance = singletonObject.AddComponent<AndroidDialogManager>();
                DontDestroyOnLoad(singletonObject); // シーンをまたいで保持
            }
            return _instance;
        }
    }

    private AndroidJavaObject pluginInstance;

    private void Awake()
    {
        Debug.Log("Awake");
        // シングルトンのインスタンスを設定
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject); // シーンをまたいで保持
        }
        else if (_instance != this)
        {
            Destroy(gameObject); // 重複するインスタンスを破棄
        }
        Initialize();
    }

    public void Initialize()
    {
        Debug.Log("Initialize");
        using (AndroidJavaClass pluginClass = new AndroidJavaClass("android.unity.dialog.UnityAndroidDialogManager"))
        {
            if (pluginClass == null)
            {
                Debug.LogError("Failed to find the Android plugin class.");
                return;
            }

            // プラグインのインスタンスを取得
            pluginInstance = pluginClass.CallStatic<AndroidJavaObject>("getInstance");

            if (pluginInstance == null)
            {
                Debug.LogError("Failed to initialize pluginInstance.");
            }
            else
            {
                Debug.Log("pluginInstance initialized successfully.");
            }
        }

        // コールバックを登録
        pluginInstance?.Call("registerCallback", new UnityCallback());
    }

    private void Start()
    {
        Debug.Log("Start");
    }

    // Unityのコールバッククラス
    private class UnityCallback : AndroidJavaProxy
    {
        public UnityCallback() : base("android.unity.dialog.UnityAndroidDialogManager$AndroidDialogManagerCallback") { }

        public void onClickDialogNeutralButton(string message)
        {
            Debug.Log("onClickDialogNeutralButton: " + message);
        }

        public void onClickDialogNegativeButton(string message)
        {
            Debug.Log("onClickDialogNegativeButton: " + message);
        }

        public void onClickDialogPositiveButton(string message)
        {
            Debug.Log("onClickDialogPositiveButton: " + message);
        }
    }

    public void ShowDialog(string title, string message)
    {
        Debug.Log("ShowDialog called with title: " + title + ", message: " + message);
        if (Application.platform == RuntimePlatform.Android)
        {
            if (pluginInstance == null)
            {
                Debug.LogError("pluginInstance is null. Ensure it is initialized correctly.");
                return;
            }

            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

                // Android側のアラートダイアログを表示
                pluginInstance.Call("showDialog", currentActivity, title, message);
            }
        }
        else
        {
            Debug.LogWarning("ShowDialog can only be called on an Android device.");
        }
    }
}
#endif