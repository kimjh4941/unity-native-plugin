#if UNITY_ANDROID
using UnityEngine;
using System;

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
                GameObject singletonObject = new GameObject("AndroidDialogManager");
                _instance = singletonObject.AddComponent<AndroidDialogManager>();
                DontDestroyOnLoad(singletonObject);
            }
            return _instance;
        }
    }

    // ダイアログ結果を受け取るためのイベント
    public event Action<string, bool, string> DialogResult; // buttonText, isSuccessful, errorMessage
    public event Action<string, bool, string> ConfirmDialogResult; // buttonText, isSuccessful, errorMessage
    public event Action<string, int, bool, string> SingleChoiceItemDialogResult; // buttonText, checkedItem, isSuccessful, errorMessage
    public event Action<string, bool[], bool, string> MultiChoiceItemDialogResult; // buttonText, checkedItems, isSuccessful, errorMessage
    public event Action<string, string, bool, string> TextInputDialogResult; // buttonText, inputText, isSuccessful, errorMessage
    public event Action<string, string, string, bool, string> LoginDialogResult; // buttonText, username, password, isSuccessful, errorMessage

    private AndroidJavaObject pluginInstance;

    private void Awake()
    {
        Debug.Log("AndroidDialogManager Awake");
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
        Initialize();
    }

    public void Initialize()
    {
        // Androidプラットフォームで実行されている場合のみ、ネイティブプラグインの初期化を行う
        if (Application.platform != RuntimePlatform.Android)
        {
            Debug.Log("Not running on an Android device. Skipping native plugin initialization.");
            return;
        }

        Debug.Log("AndroidDialogManager Initialize");
        using (AndroidJavaClass pluginClass = new AndroidJavaClass("android.unity.dialog.UnityAndroidDialogManager"))
        {
            if (pluginClass == null)
            {
                Debug.LogError("Failed to find the Android plugin class.");
                return;
            }

            pluginInstance = pluginClass.CallStatic<AndroidJavaObject>("getInstance");

            if (pluginInstance == null)
            {
                Debug.LogError("Failed to initialize pluginInstance.");
            }
            else
            {
                Debug.Log("pluginInstance initialized successfully.");

                // 各種リスナーを登録
                pluginInstance.Call("setDialogListener", new DialogListener());
                pluginInstance.Call("setConfirmDialogListener", new ConfirmDialogListener());
                pluginInstance.Call("setSingleChoiceItemDialogListener", new SingleChoiceItemDialogListener());
                pluginInstance.Call("setMultiChoiceItemDialogListener", new MultiChoiceItemDialogListener());
                pluginInstance.Call("setTextInputDialogListener", new TextInputDialogListener());
                pluginInstance.Call("setLoginDialogListener", new LoginDialogListener());
            }
        }
    }

    // Unity callback classes
    private class DialogListener : AndroidJavaProxy
    {
        public DialogListener() : base("android.unity.dialog.UnityAndroidDialogManager$DialogListener") { }

        public void onDialog(string buttonText, bool isSuccessful, string errorMessage)
        {
            Debug.Log($"onDialog: buttonText={buttonText}, isSuccessful={isSuccessful}, errorMessage={errorMessage}");
            Instance.DialogResult?.Invoke(buttonText, isSuccessful, errorMessage);
        }
    }

    private class ConfirmDialogListener : AndroidJavaProxy
    {
        public ConfirmDialogListener() : base("android.unity.dialog.UnityAndroidDialogManager$ConfirmDialogListener") { }

        public void onConfirmDialog(string buttonText, bool isSuccessful, string errorMessage)
        {
            Debug.Log($"onConfirmDialog: buttonText={buttonText}, isSuccessful={isSuccessful}, errorMessage={errorMessage}");
            Instance.ConfirmDialogResult?.Invoke(buttonText, isSuccessful, errorMessage);
        }
    }

    private class SingleChoiceItemDialogListener : AndroidJavaProxy
    {
        public SingleChoiceItemDialogListener() : base("android.unity.dialog.UnityAndroidDialogManager$SingleChoiceItemDialogListener") { }

        public void onSingleChoiceItemDialog(string buttonText, int checkedItem, bool isSuccessful, string errorMessage)
        {
            Debug.Log($"onSingleChoiceItemDialog: buttonText={buttonText}, checkedItem={checkedItem}, isSuccessful={isSuccessful}, errorMessage={errorMessage}");
            Instance.SingleChoiceItemDialogResult?.Invoke(buttonText, checkedItem, isSuccessful, errorMessage);
        }
    }

    private class MultiChoiceItemDialogListener : AndroidJavaProxy
    {
        public MultiChoiceItemDialogListener() : base("android.unity.dialog.UnityAndroidDialogManager$MultiChoiceItemDialogListener") { }

        public void onMultiChoiceItemDialog(string buttonText, bool[] checkedItems, bool isSuccessful, string errorMessage)
        {
            Debug.Log($"onMultiChoiceItemDialog: buttonText={buttonText}, checkedItems={string.Join(", ", checkedItems)}, isSuccessful={isSuccessful}, errorMessage={errorMessage}");
            Instance.MultiChoiceItemDialogResult?.Invoke(buttonText, checkedItems, isSuccessful, errorMessage);
        }
    }

    private class TextInputDialogListener : AndroidJavaProxy
    {
        public TextInputDialogListener() : base("android.unity.dialog.UnityAndroidDialogManager$TextInputDialogListener") { }

        public void onTextInputDialog(string buttonText, string inputText, bool isSuccessful, string errorMessage)
        {
            Debug.Log($"onTextInputDialog: buttonText={buttonText}, inputText={inputText}, isSuccessful={isSuccessful}, errorMessage={errorMessage}");
            Instance.TextInputDialogResult?.Invoke(buttonText, inputText, isSuccessful, errorMessage);
        }
    }

    private class LoginDialogListener : AndroidJavaProxy
    {
        public LoginDialogListener() : base("android.unity.dialog.UnityAndroidDialogManager$LoginDialogListener") { }

        public void onLoginDialog(string buttonText, string username, string password, bool isSuccessful, string errorMessage)
        {
            Debug.Log($"onLoginDialog: buttonText={buttonText}, username={username}, password={password}, isSuccessful={isSuccessful}, errorMessage={errorMessage}");
            Instance.LoginDialogResult?.Invoke(buttonText, username, password, isSuccessful, errorMessage);
        }
    }

    // Public methods to show dialogs

    /// <summary>
    /// 基本的なアラートダイアログを表示
    /// </summary>
    public void ShowDialog(string title, string message, string buttonText = "OK", bool cancelableOnTouchOutside = true, bool cancelable = true)
    {
        Debug.Log($"ShowDialog called with title: {title}, message: {message}");
        if (Application.platform == RuntimePlatform.Android)
        {
            if (pluginInstance == null)
            {
                Debug.LogError("pluginInstance is null. Ensure it is initialized correctly.");
                DialogResult?.Invoke("", false, "pluginInstance is null");
                return;
            }

            try
            {
                using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                {
                    AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                    pluginInstance.Call("showDialog", currentActivity, title, message, buttonText, cancelableOnTouchOutside, cancelable);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"ShowDialog error: {ex.Message}");
                DialogResult?.Invoke("", false, $"Internal error: {ex.Message}");
            }
        }
        else
        {
            Debug.LogWarning("ShowDialog can only be called on an Android device.");
        }
    }

    /// <summary>
    /// 確認ダイアログを表示（Yes/Noボタン）
    /// </summary>
    public void ShowConfirmDialog(string title, string message, string negativeButtonText = "No", string positiveButtonText = "Yes", bool cancelableOnTouchOutside = true, bool cancelable = true)
    {
        Debug.Log($"ShowConfirmDialog called with title: {title}, message: {message}");
        if (Application.platform == RuntimePlatform.Android)
        {
            if (pluginInstance == null)
            {
                Debug.LogError("pluginInstance is null. Ensure it is initialized correctly.");
                ConfirmDialogResult?.Invoke("", false, "pluginInstance is null");
                return;
            }

            try
            {
                using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                {
                    AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                    pluginInstance.Call("showConfirmDialog", currentActivity, title, message, negativeButtonText, positiveButtonText, cancelableOnTouchOutside, cancelable);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"ShowConfirmDialog error: {ex.Message}");
                ConfirmDialogResult?.Invoke("", false, $"Internal error: {ex.Message}");
            }
        }
        else
        {
            Debug.LogWarning("ShowConfirmDialog can only be called on an Android device.");
        }
    }

    /// <summary>
    /// 単一選択ダイアログを表示
    /// </summary>
    public void ShowSingleChoiceItemDialog(string title, string[] singleChoiceItems, int checkedItem = 0, string negativeButtonText = "Cancel", string positiveButtonText = "OK", bool cancelableOnTouchOutside = true, bool cancelable = true)
    {
        Debug.Log($"ShowSingleChoiceItemDialog called with title: {title}, items: {singleChoiceItems?.Length}");
        if (Application.platform == RuntimePlatform.Android)
        {
            if (pluginInstance == null)
            {
                Debug.LogError("pluginInstance is null. Ensure it is initialized correctly.");
                SingleChoiceItemDialogResult?.Invoke("", checkedItem, false, "pluginInstance is null");
                return;
            }

            try
            {
                using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                {
                    AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                    pluginInstance.Call("showSingleChoiceItemDialog", currentActivity, title, singleChoiceItems, checkedItem, negativeButtonText, positiveButtonText, cancelableOnTouchOutside, cancelable);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"ShowSingleChoiceItemDialog error: {ex.Message}");
                SingleChoiceItemDialogResult?.Invoke("", checkedItem, false, $"Internal error: {ex.Message}");
            }
        }
        else
        {
            Debug.LogWarning("ShowSingleChoiceItemDialog can only be called on an Android device.");
        }
    }

    /// <summary>
    /// 複数選択ダイアログを表示
    /// </summary>
    public void ShowMultiChoiceItemDialog(string title, string[] multiChoiceItems, bool[] checkedItems, string negativeButtonText = "Cancel", string positiveButtonText = "OK", bool cancelableOnTouchOutside = true, bool cancelable = true)
    {
        Debug.Log($"ShowMultiChoiceItemDialog called with title: {title}, items: {multiChoiceItems?.Length}");
        if (Application.platform == RuntimePlatform.Android)
        {
            if (pluginInstance == null)
            {
                Debug.LogError("pluginInstance is null. Ensure it is initialized correctly.");
                MultiChoiceItemDialogResult?.Invoke("", checkedItems, false, "pluginInstance is null");
                return;
            }

            try
            {
                using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                {
                    AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                    pluginInstance.Call("showMultiChoiceItemDialog", currentActivity, title, multiChoiceItems, checkedItems, negativeButtonText, positiveButtonText, cancelableOnTouchOutside, cancelable);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"ShowMultiChoiceItemDialog error: {ex.Message}");
                MultiChoiceItemDialogResult?.Invoke("", checkedItems, false, $"Internal error: {ex.Message}");
            }
        }
        else
        {
            Debug.LogWarning("ShowMultiChoiceItemDialog can only be called on an Android device.");
        }
    }

    /// <summary>
    /// テキスト入力ダイアログを表示
    /// </summary>
    public void ShowTextInputDialog(string title, string message, string hint = "", string negativeButtonText = "Cancel", string positiveButtonText = "OK", bool enablePositiveButtonWhenEmpty = false, bool cancelableOnTouchOutside = true, bool cancelable = true)
    {
        Debug.Log($"ShowTextInputDialog called with title: {title}, message: {message}, enablePositiveButtonWhenEmpty: {enablePositiveButtonWhenEmpty}");
        if (Application.platform == RuntimePlatform.Android)
        {
            if (pluginInstance == null)
            {
                Debug.LogError("pluginInstance is null. Ensure it is initialized correctly.");
                TextInputDialogResult?.Invoke("", "", false, "pluginInstance is null");
                return;
            }

            try
            {
                using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                {
                    AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                    pluginInstance.Call("showTextInputDialog", currentActivity, title, message, hint, negativeButtonText, positiveButtonText, enablePositiveButtonWhenEmpty, cancelableOnTouchOutside, cancelable);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"ShowTextInputDialog error: {ex.Message}");
                TextInputDialogResult?.Invoke("", "", false, $"Internal error: {ex.Message}");
            }
        }
        else
        {
            Debug.LogWarning("ShowTextInputDialog can only be called on an Android device.");
        }
    }

    /// <summary>
    /// ログインダイアログを表示（ユーザー名・パスワード入力）
    /// </summary>
    public void ShowLoginDialog(string title, string message, string usernameHint = "Username", string passwordHint = "Password", string negativeButtonText = "Cancel", string positiveButtonText = "OK", bool enablePositiveButtonWhenEmpty = false, bool cancelableOnTouchOutside = true, bool cancelable = true)
    {
        Debug.Log($"ShowLoginDialog called with title: {title}, message: {message}, enablePositiveButtonWhenEmpty: {enablePositiveButtonWhenEmpty}");
        if (Application.platform == RuntimePlatform.Android)
        {
            if (pluginInstance == null)
            {
                Debug.LogError("pluginInstance is null. Ensure it is initialized correctly.");
                LoginDialogResult?.Invoke("", "", "", false, "pluginInstance is null");
                return;
            }

            try
            {
                using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                {
                    AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                    pluginInstance.Call("showLoginDialog", currentActivity, title, message, usernameHint, passwordHint, negativeButtonText, positiveButtonText, enablePositiveButtonWhenEmpty, cancelableOnTouchOutside, cancelable);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"ShowLoginDialog error: {ex.Message}");
                LoginDialogResult?.Invoke("", "", "", false, $"Internal error: {ex.Message}");
            }
        }
        else
        {
            Debug.LogWarning("ShowLoginDialog can only be called on an Android device.");
        }
    }
}
#endif
