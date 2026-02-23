#nullable enable

#if UNITY_ANDROID
namespace JonghyunKim.NativeToolkit.Runtime.Dialog
{
    using UnityEngine;
    using System;
    using JonghyunKim.NativeToolkit.Runtime.Common;

    /// <summary>
    /// Singleton manager for Android native dialog operations using Unity's AndroidJavaObject interface.
    /// Provides a Unity-friendly API for showing various types of Android native dialogs including
    /// basic alerts, confirmation dialogs, single/multi-choice dialogs, text input, and login dialogs.
    /// Uses event-driven callbacks to handle dialog results asynchronously.
    /// </summary>
    public class AndroidDialogManager : MonoBehaviour
    {
        private static AndroidDialogManager? _instance;

        /// <summary>
        /// Singleton instance property for AndroidDialogManager.
        /// Creates a new instance if none exists and ensures it persists across scene loads.
        /// </summary>
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

        // Event handlers for receiving dialog results
        public event Action<string?, bool, string?>? DialogResult; // buttonText, isSuccessful, errorMessage
        public event Action<string?, bool, string?>? ConfirmDialogResult; // buttonText, isSuccessful, errorMessage
        public event Action<string?, int?, bool, string?>? SingleChoiceItemDialogResult; // buttonText, checkedItem, isSuccessful, errorMessage
        public event Action<string?, bool[]?, bool, string?>? MultiChoiceItemDialogResult; // buttonText, checkedItems, isSuccessful, errorMessage
        public event Action<string?, string?, bool, string?>? TextInputDialogResult; // buttonText, inputText, isSuccessful, errorMessage
        public event Action<string?, string?, string?, bool, string?>? LoginDialogResult; // buttonText, username, password, isSuccessful, errorMessage

        private AndroidJavaObject? pluginInstance;

        /// <summary>
        /// Initialize the singleton instance and set up the main thread dispatcher.
        /// Ensures only one instance exists and persists across scene changes.
        /// </summary>
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
            // Ensure Dispatcher is created on main thread for safe cross-thread enqueueing
            _ = UnityMainThreadDispatcher.Instance;

            Initialize();
        }

        /// <summary>
        /// Initialize the native Android plugin interface and register event listeners.
        /// Only initializes on Android platform, logs warning on other platforms.
        /// </summary>
        public void Initialize()
        {
            // Only initialize native plugin when running on Android platform
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

                    // Register various event listeners
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

        /// <summary>
        /// Proxy class for handling basic dialog callbacks from Android native code.
        /// Receives dialog result and forwards to Unity event system via main thread dispatcher.
        /// </summary>
        private class DialogListener : AndroidJavaProxy
        {
            public DialogListener() : base("android.unity.dialog.UnityAndroidDialogManager$DialogListener") { }

            public void onDialog(string? buttonText, bool isSuccessful, string? errorMessage)
            {
                Debug.Log($"onDialog: buttonText={buttonText ?? "null"}, isSuccessful={isSuccessful}, errorMessage={errorMessage ?? "null"}");
                PostToMainThread(() =>
                {
                    Instance.DialogResult?.Invoke(buttonText, isSuccessful, errorMessage);
                });
            }
        }

        /// <summary>
        /// Proxy class for handling confirmation dialog callbacks from Android native code.
        /// Receives dialog result and forwards to Unity event system via main thread dispatcher.
        /// </summary>
        private class ConfirmDialogListener : AndroidJavaProxy
        {
            public ConfirmDialogListener() : base("android.unity.dialog.UnityAndroidDialogManager$ConfirmDialogListener") { }

            public void onConfirmDialog(string? buttonText, bool isSuccessful, string? errorMessage)
            {
                Debug.Log($"onConfirmDialog: buttonText={buttonText ?? "null"}, isSuccessful={isSuccessful}, errorMessage={errorMessage ?? "null"}");
                PostToMainThread(() =>
                {
                    Instance.ConfirmDialogResult?.Invoke(buttonText, isSuccessful, errorMessage);
                });
            }
        }

        /// <summary>
        /// Proxy class for handling single choice dialog callbacks from Android native code.
        /// Receives dialog result with selected item index and forwards to Unity event system.
        /// </summary>
        private class SingleChoiceItemDialogListener : AndroidJavaProxy
        {
            public SingleChoiceItemDialogListener() : base("android.unity.dialog.UnityAndroidDialogManager$SingleChoiceItemDialogListener") { }

            public void onSingleChoiceItemDialog(string? buttonText, int checkedItem, bool isSuccessful, string? errorMessage)
            {
                Debug.Log($"onSingleChoiceItemDialog: buttonText={buttonText ?? "null"}, checkedItem={checkedItem}, isSuccessful={isSuccessful}, errorMessage={errorMessage ?? "null"}");
                PostToMainThread(() =>
                {
                    Instance.SingleChoiceItemDialogResult?.Invoke(buttonText, checkedItem == -1 ? null : checkedItem, isSuccessful, errorMessage);
                });
            }
        }

        /// <summary>
        /// Proxy class for handling multi-choice dialog callbacks from Android native code.
        /// Receives dialog result with array of selected items and forwards to Unity event system.
        /// </summary>
        private class MultiChoiceItemDialogListener : AndroidJavaProxy
        {
            public MultiChoiceItemDialogListener() : base("android.unity.dialog.UnityAndroidDialogManager$MultiChoiceItemDialogListener") { }

            public void onMultiChoiceItemDialog(string? buttonText, bool[]? checkedItems, bool isSuccessful, string? errorMessage)
            {
                Debug.Log($"onMultiChoiceItemDialog: buttonText={buttonText ?? "null"}, checkedItems={(checkedItems != null ? string.Join(", ", checkedItems) : "null")}, isSuccessful={isSuccessful}, errorMessage={errorMessage ?? "null"}");
                PostToMainThread(() =>
                {
                    Instance.MultiChoiceItemDialogResult?.Invoke(buttonText, checkedItems, isSuccessful, errorMessage);
                });
            }
        }

        /// <summary>
        /// Proxy class for handling text input dialog callbacks from Android native code.
        /// Receives dialog result with entered text and forwards to Unity event system.
        /// </summary>
        private class TextInputDialogListener : AndroidJavaProxy
        {
            public TextInputDialogListener() : base("android.unity.dialog.UnityAndroidDialogManager$TextInputDialogListener") { }

            public void onTextInputDialog(string? buttonText, string? inputText, bool isSuccessful, string? errorMessage)
            {
                Debug.Log($"onTextInputDialog: buttonText={buttonText ?? "null"}, inputText={inputText ?? "null"}, isSuccessful={isSuccessful}, errorMessage={errorMessage ?? "null"}");
                PostToMainThread(() =>
                {
                    Instance.TextInputDialogResult?.Invoke(buttonText, inputText, isSuccessful, errorMessage);
                });
            }
        }

        /// <summary>
        /// Proxy class for handling login dialog callbacks from Android native code.
        /// Receives dialog result with entered credentials and forwards to Unity event system.
        /// </summary>
        private class LoginDialogListener : AndroidJavaProxy
        {
            public LoginDialogListener() : base("android.unity.dialog.UnityAndroidDialogManager$LoginDialogListener") { }

            public void onLoginDialog(string? buttonText, string? username, string? password, bool isSuccessful, string? errorMessage)
            {
                Debug.Log($"onLoginDialog: buttonText={buttonText ?? "null"}, username={username ?? "null"}, password={password ?? "null"}, isSuccessful={isSuccessful}, errorMessage={errorMessage ?? "null"}");
                PostToMainThread(() =>
                {
                    Instance.LoginDialogResult?.Invoke(buttonText, username, password, isSuccessful, errorMessage);
                });
            }
        }

        // Public methods to show dialogs

        /// <summary>
        /// Shows a basic Android alert dialog with a single button.
        /// </summary>
        /// <param name="title">Dialog title</param>
        /// <param name="message">Dialog message content</param>
        /// <param name="buttonText">Text for the single button (default: "OK")</param>
        /// <param name="cancelableOnTouchOutside">Whether dialog can be cancelled by touching outside (default: true)</param>
        /// <param name="cancelable">Whether dialog can be cancelled by back button (default: true)</param>
        public void ShowDialog(string title, string message, string? buttonText = "OK", bool cancelableOnTouchOutside = true, bool cancelable = true)
        {
            Debug.Log($"ShowDialog called with title: {title}, message: {message}, buttonText: {buttonText}, cancelableOnTouchOutside: {cancelableOnTouchOutside}, cancelable: {cancelable}");
            if (string.IsNullOrEmpty(title))
            {
                Debug.LogError("Title cannot be null or empty.");
                DialogResult?.Invoke(null, false, "Title cannot be null or empty.");
                return;
            }

            if (string.IsNullOrEmpty(message))
            {
                Debug.LogError("Message cannot be null or empty.");
                DialogResult?.Invoke(null, false, "Message cannot be null or empty.");
                return;
            }

            buttonText ??= "OK";

            if (Application.platform == RuntimePlatform.Android)
            {
                if (pluginInstance == null)
                {
                    Debug.LogError("pluginInstance is null. Ensure it is initialized correctly.");
                    DialogResult?.Invoke(null, false, "pluginInstance is null");
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
                    DialogResult?.Invoke(null, false, $"Internal error: {ex.Message}");
                }
            }
            else
            {
                Debug.LogWarning("ShowDialog can only be called on an Android device.");
            }
        }

        /// <summary>
        /// Shows a confirmation dialog with Yes/No buttons.
        /// </summary>
        /// <param name="title">Dialog title</param>
        /// <param name="message">Dialog message content</param>
        /// <param name="negativeButtonText">Text for the negative button (default: "No")</param>
        /// <param name="positiveButtonText">Text for the positive button (default: "Yes")</param>
        /// <param name="cancelableOnTouchOutside">Whether dialog can be cancelled by touching outside (default: true)</param>
        /// <param name="cancelable">Whether dialog can be cancelled by back button (default: true)</param>
        public void ShowConfirmDialog(string title, string message, string? negativeButtonText = "No", string? positiveButtonText = "Yes", bool cancelableOnTouchOutside = true, bool cancelable = true)
        {
            Debug.Log($"ShowConfirmDialog called with title: {title}, message: {message}, negativeButtonText: {negativeButtonText}, positiveButtonText: {positiveButtonText}, cancelableOnTouchOutside: {cancelableOnTouchOutside}, cancelable: {cancelable}");
            if (string.IsNullOrEmpty(title))
            {
                Debug.LogError("Title cannot be null or empty.");
                ConfirmDialogResult?.Invoke(null, false, "Title cannot be null or empty.");
                return;
            }

            if (string.IsNullOrEmpty(message))
            {
                Debug.LogError("Message cannot be null or empty.");
                ConfirmDialogResult?.Invoke(null, false, "Message cannot be null or empty.");
                return;
            }

            negativeButtonText ??= "No";
            positiveButtonText ??= "Yes";

            if (Application.platform == RuntimePlatform.Android)
            {
                if (pluginInstance == null)
                {
                    Debug.LogError("pluginInstance is null. Ensure it is initialized correctly.");
                    ConfirmDialogResult?.Invoke(null, false, "pluginInstance is null");
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
                    ConfirmDialogResult?.Invoke(null, false, $"Internal error: {ex.Message}");
                }
            }
            else
            {
                Debug.LogWarning("ShowConfirmDialog can only be called on an Android device.");
            }
        }

        /// <summary>
        /// Shows a single selection dialog with radio button options.
        /// </summary>
        /// <param name="title">Dialog title</param>
        /// <param name="singleChoiceItems">Array of items to choose from</param>
        /// <param name="checkedItem">Index of initially selected item (default: 0)</param>
        /// <param name="negativeButtonText">Text for the negative button (default: "Cancel")</param>
        /// <param name="positiveButtonText">Text for the positive button (default: "OK")</param>
        /// <param name="cancelableOnTouchOutside">Whether dialog can be cancelled by touching outside (default: true)</param>
        /// <param name="cancelable">Whether dialog can be cancelled by back button (default: true)</param>
        public void ShowSingleChoiceItemDialog(string title, string[] singleChoiceItems, int checkedItem = 0, string? negativeButtonText = "Cancel", string? positiveButtonText = "OK", bool cancelableOnTouchOutside = true, bool cancelable = true)
        {
            var singleChoiceItemsList = singleChoiceItems != null ? string.Join(", ", singleChoiceItems) : "null";
            Debug.Log($"ShowSingleChoiceItemDialog called with title: {title}, singleChoiceItems: {singleChoiceItemsList}, checkedItem: {checkedItem}, negativeButtonText: {negativeButtonText}, positiveButtonText: {positiveButtonText}, cancelableOnTouchOutside: {cancelableOnTouchOutside}, cancelable: {cancelable}");
            if (string.IsNullOrEmpty(title))
            {
                Debug.LogError("Title cannot be null or empty.");
                SingleChoiceItemDialogResult?.Invoke(null, null, false, "Title cannot be null or empty.");
                return;
            }

            if (singleChoiceItems == null || singleChoiceItems.Length == 0)
            {
                Debug.LogError("singleChoiceItems cannot be null or empty.");
                SingleChoiceItemDialogResult?.Invoke(null, null, false, "singleChoiceItems cannot be null or empty.");
                return;
            }

            negativeButtonText ??= "Cancel";
            positiveButtonText ??= "OK";

            if (Application.platform == RuntimePlatform.Android)
            {
                if (pluginInstance == null)
                {
                    Debug.LogError("pluginInstance is null. Ensure it is initialized correctly.");
                    SingleChoiceItemDialogResult?.Invoke(null, null, false, "pluginInstance is null");
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
                    SingleChoiceItemDialogResult?.Invoke(null, null, false, $"Internal error: {ex.Message}");
                }
            }
            else
            {
                Debug.LogWarning("ShowSingleChoiceItemDialog can only be called on an Android device.");
            }
        }

        /// <summary>
        /// Shows a multi-selection dialog with checkbox options.
        /// </summary>
        /// <param name="title">Dialog title</param>
        /// <param name="multiChoiceItems">Array of items to choose from</param>
        /// <param name="checkedItems">Array indicating initial selection state for each item</param>
        /// <param name="negativeButtonText">Text for the negative button (default: "Cancel")</param>
        /// <param name="positiveButtonText">Text for the positive button (default: "OK")</param>
        /// <param name="cancelableOnTouchOutside">Whether dialog can be cancelled by touching outside (default: true)</param>
        /// <param name="cancelable">Whether dialog can be cancelled by back button (default: true)</param>
        public void ShowMultiChoiceItemDialog(string title, string[] multiChoiceItems, bool[]? checkedItems = null, string? negativeButtonText = "Cancel", string? positiveButtonText = "OK", bool cancelableOnTouchOutside = true, bool cancelable = true)
        {
            var multiChoiceItemsList = multiChoiceItems != null ? string.Join(", ", multiChoiceItems) : "null";
            var checkedItemsList = checkedItems != null ? string.Join(", ", checkedItems) : "null";
            Debug.Log($"ShowMultiChoiceItemDialog called with title: {title}, multiChoiceItems: {multiChoiceItemsList}, checkedItems: {checkedItemsList}, negativeButtonText: {negativeButtonText}, positiveButtonText: {positiveButtonText}, cancelableOnTouchOutside: {cancelableOnTouchOutside}, cancelable: {cancelable}");
            if (string.IsNullOrEmpty(title))
            {
                Debug.LogError("Title cannot be null or empty.");
                MultiChoiceItemDialogResult?.Invoke(null, null, false, "Title cannot be null or empty.");
                return;
            }

            if (multiChoiceItems == null || multiChoiceItems.Length == 0)
            {
                Debug.LogError("multiChoiceItems cannot be null or empty.");
                MultiChoiceItemDialogResult?.Invoke(null, null, false, "multiChoiceItems cannot be null or empty.");
                return;
            }

            if (checkedItems == null || checkedItems.Length == 0)
            {
                checkedItems = new bool[multiChoiceItems.Length];
                for (int i = 0; i < checkedItems.Length; i++)
                {
                    checkedItems[i] = false;
                }
            }

            negativeButtonText ??= "Cancel";
            positiveButtonText ??= "OK";

            if (Application.platform == RuntimePlatform.Android)
            {
                if (pluginInstance == null)
                {
                    Debug.LogError("pluginInstance is null. Ensure it is initialized correctly.");
                    MultiChoiceItemDialogResult?.Invoke(null, null, false, "pluginInstance is null");
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
                    MultiChoiceItemDialogResult?.Invoke(null, null, false, $"Internal error: {ex.Message}");
                }
            }
            else
            {
                Debug.LogWarning("ShowMultiChoiceItemDialog can only be called on an Android device.");
            }
        }

        /// <summary>
        /// Shows a text input dialog with a single text field.
        /// </summary>
        /// <param name="title">Dialog title</param>
        /// <param name="message">Dialog message content</param>
        /// <param name="hint">Placeholder text for the input field (default: "")</param>
        /// <param name="negativeButtonText">Text for the negative button (default: "Cancel")</param>
        /// <param name="positiveButtonText">Text for the positive button (default: "OK")</param>
        /// <param name="enablePositiveButtonWhenEmpty">Whether positive button is enabled when input is empty (default: false)</param>
        /// <param name="cancelableOnTouchOutside">Whether dialog can be cancelled by touching outside (default: true)</param>
        /// <param name="cancelable">Whether dialog can be cancelled by back button (default: true)</param>
        public void ShowTextInputDialog(string title, string message, string? hint = "", string? negativeButtonText = "Cancel", string? positiveButtonText = "OK", bool enablePositiveButtonWhenEmpty = false, bool cancelableOnTouchOutside = true, bool cancelable = true)
        {
            Debug.Log($"ShowTextInputDialog called with title: {title}, message: {message}, hint: {hint}, negativeButtonText: {negativeButtonText}, positiveButtonText: {positiveButtonText}, enablePositiveButtonWhenEmpty: {enablePositiveButtonWhenEmpty}, cancelableOnTouchOutside: {cancelableOnTouchOutside}, cancelable: {cancelable}");
            if (string.IsNullOrEmpty(title))
            {
                Debug.LogError("Title cannot be null or empty.");
                TextInputDialogResult?.Invoke(null, null, false, "Title cannot be null or empty.");
                return;
            }

            if (string.IsNullOrEmpty(message))
            {
                Debug.LogError("Message cannot be null or empty.");
                TextInputDialogResult?.Invoke(null, null, false, "Message cannot be null or empty.");
                return;
            }

            hint ??= "";
            negativeButtonText ??= "Cancel";
            positiveButtonText ??= "OK";

            if (Application.platform == RuntimePlatform.Android)
            {
                if (pluginInstance == null)
                {
                    Debug.LogError("pluginInstance is null. Ensure it is initialized correctly.");
                    TextInputDialogResult?.Invoke(null, null, false, "pluginInstance is null");
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
                    TextInputDialogResult?.Invoke(null, null, false, $"Internal error: {ex.Message}");
                }
            }
            else
            {
                Debug.LogWarning("ShowTextInputDialog can only be called on an Android device.");
            }
        }

        /// <summary>
        /// Shows a login dialog with username and password input fields.
        /// </summary>
        /// <param name="title">Dialog title</param>
        /// <param name="message">Dialog message content</param>
        /// <param name="usernameHint">Placeholder text for the username field (default: "Username")</param>
        /// <param name="passwordHint">Placeholder text for the password field (default: "Password")</param>
        /// <param name="negativeButtonText">Text for the negative button (default: "Cancel")</param>
        /// <param name="positiveButtonText">Text for the positive button (default: "OK")</param>
        /// <param name="enablePositiveButtonWhenEmpty">Whether positive button is enabled when inputs are empty (default: false)</param>
        /// <param name="cancelableOnTouchOutside">Whether dialog can be cancelled by touching outside (default: true)</param>
        /// <param name="cancelable">Whether dialog can be cancelled by back button (default: true)</param>
        public void ShowLoginDialog(string title, string message, string? usernameHint = "Username", string? passwordHint = "Password", string? negativeButtonText = "Cancel", string? positiveButtonText = "Login", bool enablePositiveButtonWhenEmpty = false, bool cancelableOnTouchOutside = true, bool cancelable = true)
        {
            Debug.Log($"ShowLoginDialog called with title: {title}, message: {message}, usernameHint: {usernameHint}, passwordHint: {passwordHint}, negativeButtonText: {negativeButtonText}, positiveButtonText: {positiveButtonText}, enablePositiveButtonWhenEmpty: {enablePositiveButtonWhenEmpty}, cancelableOnTouchOutside: {cancelableOnTouchOutside}, cancelable: {cancelable}");
            if (string.IsNullOrEmpty(title))
            {
                Debug.LogError("Title cannot be null or empty.");
                LoginDialogResult?.Invoke(null, null, null, false, "Title cannot be null or empty.");
                return;
            }

            if (string.IsNullOrEmpty(message))
            {
                Debug.LogError("Message cannot be null or empty.");
                LoginDialogResult?.Invoke(null, null, null, false, "Message cannot be null or empty.");
                return;
            }

            usernameHint ??= "Username";
            passwordHint ??= "Password";
            negativeButtonText ??= "Cancel";
            positiveButtonText ??= "Login";

            if (Application.platform == RuntimePlatform.Android)
            {
                if (pluginInstance == null)
                {
                    Debug.LogError("pluginInstance is null. Ensure it is initialized correctly.");
                    LoginDialogResult?.Invoke(null, null, null, false, "pluginInstance is null");
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
                    LoginDialogResult?.Invoke(null, null, null, false, $"Internal error: {ex.Message}");
                }
            }
            else
            {
                Debug.LogWarning("ShowLoginDialog can only be called on an Android device.");
            }
        }

        /// <summary>
        /// Helper method to post actions to the main Unity thread safely.
        /// Uses UnityMainThreadDispatcher to ensure UI operations happen on the correct thread.
        /// </summary>
        /// <param name="action">Action to execute on the main thread</param>
        private static void PostToMainThread(Action action)
        {
            try
            {
                // UnityMainThreadDispatcher は既にメインスレッドで生成しておく（Awake 参照）
                UnityMainThreadDispatcher.Instance.Enqueue(action);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AndroidDialogManager] PostToMainThread failed: {ex}");
            }
        }
    }
}
#endif
