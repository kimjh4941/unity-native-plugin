#nullable enable

#if UNITY_IOS
namespace JonghyunKim.NativeToolkit.Runtime.iOS.Dialog
{
    using UnityEngine;
    using System.Runtime.InteropServices;
    using System;

    /// <summary>
    /// Singleton manager for iOS native dialog operations using Unity's native plugin interface.
    /// Provides a Unity-friendly API for showing various types of iOS native dialogs including
    /// basic alerts, confirmation dialogs, destructive alerts, action sheets, text input, and login dialogs.
    /// Results arrive through the events.
    /// <para>
    /// <b>Unavailable for now.</b> native-toolkit 2.0.0 removed the 1.x bridge this Manager called, and
    /// the Manager does not call the 2.0.0 C ABI yet (artifact/topics/ios-c-abi-2, stage 3-S). Until it
    /// moves (stage 3), each Show* that passes the argument checks raises its event once, inside the
    /// call, with (null…, false, <see cref="UnavailableMessage"/>); nothing follows.
    /// </para>
    /// </summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "JonghyunKim.NativeToolkit.Runtime.Dialog")]
    public class IosDialogManager : MonoBehaviour
    {
        private static IosDialogManager? _instance;

        /// <summary>
        /// Singleton instance property for IosDialogManager.
        /// Creates a new instance if none exists and ensures it persists across scene loads.
        /// </summary>
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

        // Event handlers for receiving dialog results (includes success status and error messages)
        public event Action<string?, bool, string?>? DialogResult; // buttonText, isSuccess, errorMessage
        public event Action<string?, bool, string?>? ConfirmDialogResult; // buttonText, isSuccess, errorMessage
        public event Action<string?, bool, string?>? DestructiveDialogResult; // buttonText, isSuccess, errorMessage
        public event Action<string?, bool, string?>? ActionSheetResult; // buttonText, isSuccess, errorMessage
        public event Action<string?, string?, bool, string?>? TextInputDialogResult; // buttonText, inputText, isSuccess, errorMessage
        public event Action<string?, string?, string?, bool, string?>? LoginDialogResult; // buttonText, username, password, isSuccess, errorMessage

        /// <summary>
        /// Initialize the singleton instance and ensure persistence across scene changes.
        /// </summary>
        private void Awake()
        {
            Debug.Log("IosDialogManager Awake");
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

        // Native code interface delegate definitions (IL2CPP compatible)
        /// <summary>
        /// Callback signature for basic alert dialog results.
        /// </summary>
        /// <param name="buttonText">Pressed button text (may be null on failure)</param>
        /// <param name="isSuccess">True if native dialog completed successfully</param>
        /// <param name="errorMessage">Error details if <paramref name="isSuccess"/> is false</param>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void DialogCallback(string? buttonText, bool isSuccess, string? errorMessage);

        /// <summary>
        /// Callback signature for confirmation dialog (OK / Cancel) results.
        /// </summary>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void ConfirmDialogCallback(string? buttonText, bool isSuccess, string? errorMessage);

        /// <summary>
        /// Callback signature for destructive confirmation dialog results.
        /// </summary>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void DestructiveDialogCallback(string? buttonText, bool isSuccess, string? errorMessage);

        /// <summary>
        /// Callback signature for action sheet selection results.
        /// </summary>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void ActionSheetCallback(string? buttonText, bool isSuccess, string? errorMessage);

        /// <summary>
        /// Callback signature for text input dialog results.
        /// </summary>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void TextInputDialogCallback(string? buttonText, string? inputText, bool isSuccess, string? errorMessage);

        /// <summary>
        /// Callback signature for login dialog results (username + password).
        /// </summary>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void LoginDialogCallback(string? buttonText, string? username, string? password, bool isSuccess, string? errorMessage);

        /// <summary>The error each Show* reports until Dialog moves to the C ABI (stage 3).</summary>
        internal const string UnavailableMessage = "Dialog is not available yet on this build.";

        // Public dialog API

        /// <summary>
        /// Shows a basic alert dialog with a single button.
        /// </summary>
        public void ShowDialog(string title, string message, string? buttonText = "OK")
        {
            Debug.Log($"ShowDialog called with title: {title}, message: {message}, buttonText: {buttonText}");
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

            Debug.LogWarning(UnavailableMessage);
            DialogResult?.Invoke(null, false, UnavailableMessage);
        }

        /// <summary>
        /// Shows a confirmation dialog with confirm / cancel buttons.
        /// </summary>
        public void ShowConfirmDialog(string title, string message, string? confirmButtonText = "OK", string? cancelButtonText = "Cancel")
        {
            Debug.Log($"ShowConfirmDialog called with title: {title}, message: {message}, confirmButtonText: {confirmButtonText}, cancelButtonText: {cancelButtonText}");
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

            confirmButtonText ??= "OK";
            cancelButtonText ??= "Cancel";

            Debug.LogWarning(UnavailableMessage);
            ConfirmDialogResult?.Invoke(null, false, UnavailableMessage);
        }

        /// <summary>
        /// Shows a destructive confirmation dialog (e.g. Delete confirmation).
        /// </summary>
        public void ShowDestructiveDialog(string title, string message, string? destructiveButtonText = "Delete", string? cancelButtonText = "Cancel")
        {
            Debug.Log($"ShowDestructiveDialog called with title: {title}, message: {message}, destructiveButtonText: {destructiveButtonText}, cancelButtonText: {cancelButtonText}");
            if (string.IsNullOrEmpty(title))
            {
                Debug.LogError("Title cannot be null or empty.");
                DestructiveDialogResult?.Invoke(null, false, "Title cannot be null or empty.");
                return;
            }

            if (string.IsNullOrEmpty(message))
            {
                Debug.LogError("Message cannot be null or empty.");
                DestructiveDialogResult?.Invoke(null, false, "Message cannot be null or empty.");
                return;
            }

            destructiveButtonText ??= "Delete";
            cancelButtonText ??= "Cancel";

            Debug.LogWarning(UnavailableMessage);
            DestructiveDialogResult?.Invoke(null, false, UnavailableMessage);
        }

        /// <summary>
        /// Shows an action sheet with multiple selectable options.
        /// </summary>
        public void ShowActionSheet(string title, string message, string[] options, string? cancelButtonText = "Cancel")
        {
            var optionsList = options == null ? "[]" : $"[{string.Join(", ", options)}]";
            Debug.Log($"ShowActionSheet called with title: {title}, message: {message}, options: {optionsList}, cancelButtonText: {cancelButtonText}");
            if (string.IsNullOrEmpty(title))
            {
                Debug.LogError("Title cannot be null or empty.");
                ActionSheetResult?.Invoke(null, false, "Title cannot be null or empty.");
                return;
            }

            if (string.IsNullOrEmpty(message))
            {
                Debug.LogError("Message cannot be null or empty.");
                ActionSheetResult?.Invoke(null, false, "Message cannot be null or empty.");
                return;
            }

            if (options == null || options.Length == 0)
            {
                Debug.LogError("Options cannot be null or empty.");
                ActionSheetResult?.Invoke(null, false, "Options cannot be null or empty.");
                return;
            }

            cancelButtonText ??= "Cancel";

            Debug.LogWarning(UnavailableMessage);
            ActionSheetResult?.Invoke(null, false, UnavailableMessage);
        }

        /// <summary>
        /// Shows a text input dialog with a single text field.
        /// </summary>
        /// <param name="enableConfirmWhenEmpty">If true the confirm button remains enabled when input is empty (default: false)</param>
        public void ShowTextInputDialog(string title, string message, string? placeholder = "", string? confirmButtonText = "OK", string? cancelButtonText = "Cancel", bool enableConfirmWhenEmpty = false)
        {
            Debug.Log($"ShowTextInputDialog called with title: {title}, message: {message}, placeholder: {placeholder}, confirmButtonText: {confirmButtonText}, cancelButtonText: {cancelButtonText}, enableConfirmWhenEmpty: {enableConfirmWhenEmpty}");
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

            placeholder ??= "";
            confirmButtonText ??= "OK";
            cancelButtonText ??= "Cancel";

            Debug.LogWarning(UnavailableMessage);
            TextInputDialogResult?.Invoke(null, null, false, UnavailableMessage);
        }

        /// <summary>
        /// Shows a login dialog requesting username and password.
        /// </summary>
        /// <param name="enableLoginWhenEmpty">If true the login button remains enabled when fields are empty (default: false)</param>
        public void ShowLoginDialog(string title, string message, string? usernamePlaceholder = "Username", string? passwordPlaceholder = "Password", string? loginButtonText = "Login", string? cancelButtonText = "Cancel", bool enableLoginWhenEmpty = false)
        {
            Debug.Log($"ShowLoginDialog called with title: {title}, message: {message}, usernamePlaceholder: {usernamePlaceholder}, passwordPlaceholder: {passwordPlaceholder}, loginButtonText: {loginButtonText}, cancelButtonText: {cancelButtonText}, enableLoginWhenEmpty: {enableLoginWhenEmpty}");
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

            usernamePlaceholder ??= "Username";
            passwordPlaceholder ??= "Password";
            loginButtonText ??= "Login";
            cancelButtonText ??= "Cancel";

            Debug.LogWarning(UnavailableMessage);
            LoginDialogResult?.Invoke(null, null, null, false, UnavailableMessage);
        }
    }
}
#endif
