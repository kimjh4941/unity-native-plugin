#nullable enable

#if UNITY_IOS
using UnityEngine;
using System.Runtime.InteropServices;
using System;
using AOT;

/// <summary>
/// Singleton manager for iOS native dialog operations using Unity's native plugin interface.
/// Provides a Unity-friendly API for showing various types of iOS native dialogs including
/// basic alerts, confirmation dialogs, destructive alerts, action sheets, text input, and login dialogs.
/// Uses P/Invoke to communicate with Objective-C native code and event-driven callbacks for results.
/// </summary>
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

    // Native function imports (Objective-C side) – all use Cdecl for IL2CPP compatibility
    /// <summary>
    /// Shows a basic iOS alert dialog with a single button.
    /// </summary>
    [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
    private static extern void showDialog(string title, string message, string buttonText, DialogCallback callback);

    /// <summary>
    /// Shows a confirmation dialog (OK / Cancel style).
    /// </summary>
    [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
    private static extern void showConfirmDialog(string title, string message, string confirmButtonText, string cancelButtonText, ConfirmDialogCallback callback);

    /// <summary>
    /// Shows a destructive confirmation dialog (e.g. Delete / Cancel).
    /// </summary>
    [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
    private static extern void showDestructiveDialog(string title, string message, string destructiveButtonText, string cancelButtonText, DestructiveDialogCallback callback);

    /// <summary>
    /// Shows an action sheet with selectable options.
    /// </summary>
    [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
    private static extern void showActionSheet(string title, string message, IntPtr options, int optionCount, string cancelButtonText, ActionSheetCallback callback);

    /// <summary>
    /// Shows a text input dialog with a single editable field.
    /// </summary>
    [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
    private static extern void showTextInputDialog(string title, string message, string placeholder, string confirmButtonText, string cancelButtonText, bool enableConfirmWhenEmpty, TextInputDialogCallback callback);

    /// <summary>
    /// Shows a login dialog with username & password fields.
    /// </summary>
    [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
    private static extern void showLoginDialog(string title, string message, string usernamePlaceholder, string passwordPlaceholder, string loginButtonText, string cancelButtonText, bool enableLoginWhenEmpty, LoginDialogCallback callback);

    // Memory management for ActionSheet options (static to allow cleanup in callback)
    private static IntPtr s_optionsPtr = IntPtr.Zero;
    private static IntPtr[]? s_stringPointers = null;

    // Static callback methods (IL2CPP compatible) – marshalled from native side
    [MonoPInvokeCallback(typeof(DialogCallback))]
    private static void OnDialogCallback(string? buttonText, bool isSuccess, string? errorMessage)
    {
        UnityMainThreadDispatcher.Instance.Enqueue(() =>
        {
            try
            {
                Instance.DialogResult?.Invoke(buttonText, isSuccess, errorMessage);
            }
            catch (Exception ex)
            {
                Debug.LogError($"DialogResult callback error: {ex.Message}");
            }
        });
    }

    [MonoPInvokeCallback(typeof(ConfirmDialogCallback))]
    private static void OnConfirmDialogCallback(string? buttonText, bool isSuccess, string? errorMessage)
    {
        UnityMainThreadDispatcher.Instance.Enqueue(() =>
        {
            try
            {
                Instance.ConfirmDialogResult?.Invoke(buttonText, isSuccess, errorMessage);
            }
            catch (Exception ex)
            {
                Debug.LogError($"ConfirmDialogResult callback error: {ex.Message}");
            }
        });
    }

    [MonoPInvokeCallback(typeof(DestructiveDialogCallback))]
    private static void OnDestructiveDialogCallback(string? buttonText, bool isSuccess, string? errorMessage)
    {
        UnityMainThreadDispatcher.Instance.Enqueue(() =>
        {
            try
            {
                Instance.DestructiveDialogResult?.Invoke(buttonText, isSuccess, errorMessage);
            }
            catch (Exception ex)
            {
                Debug.LogError($"DestructiveDialogResult callback error: {ex.Message}");
            }
        });
    }

    [MonoPInvokeCallback(typeof(ActionSheetCallback))]
    private static void OnActionSheetCallback(string? buttonText, bool isSuccess, string? errorMessage)
    {
        UnityMainThreadDispatcher.Instance.Enqueue(() =>
        {
            try
            {
                Instance.ActionSheetResult?.Invoke(buttonText, isSuccess, errorMessage);
            }
            catch (Exception ex)
            {
                Debug.LogError($"ActionSheetResult callback error: {ex.Message}");
            }
            finally
            {
                // Release unmanaged memory allocated for ActionSheet options
                if (s_optionsPtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(s_optionsPtr);
                    s_optionsPtr = IntPtr.Zero;
                }
                if (s_stringPointers != null)
                {
                    foreach (var ptr in s_stringPointers)
                    {
                        if (ptr != IntPtr.Zero) Marshal.FreeHGlobal(ptr);
                    }
                    s_stringPointers = null;
                }
                Debug.Log("Memory for ActionSheet options freed in callback.");
            }
        });
    }

    [MonoPInvokeCallback(typeof(TextInputDialogCallback))]
    private static void OnTextInputDialogCallback(string? buttonText, string? inputText, bool isSuccess, string? errorMessage)
    {
        UnityMainThreadDispatcher.Instance.Enqueue(() =>
        {
            try
            {
                Instance.TextInputDialogResult?.Invoke(buttonText, inputText, isSuccess, errorMessage);
            }
            catch (Exception ex)
            {
                Debug.LogError($"TextInputDialogResult callback error: {ex.Message}");
            }
        });
    }

    [MonoPInvokeCallback(typeof(LoginDialogCallback))]
    private static void OnLoginDialogCallback(string? buttonText, string? username, string? password, bool isSuccess, string? errorMessage)
    {
        UnityMainThreadDispatcher.Instance.Enqueue(() =>
        {
            try
            {
                Instance.LoginDialogResult?.Invoke(buttonText, username, password, isSuccess, errorMessage);
            }
            catch (Exception ex)
            {
                Debug.LogError($"LoginDialogResult callback error: {ex.Message}");
            }
        });
    }

    // Public dialog API (IL2CPP friendly – uses static callbacks)

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

        try
        {
            showDialog(title, message, buttonText, OnDialogCallback);
        }
        catch (Exception ex)
        {
            Debug.LogError($"ShowDialog error: {ex.Message}");
            DialogResult?.Invoke(null, false, $"Internal error: {ex.Message}");
        }
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

        try
        {
            showConfirmDialog(title, message, confirmButtonText, cancelButtonText, OnConfirmDialogCallback);
        }
        catch (Exception ex)
        {
            Debug.LogError($"ShowConfirmDialog error: {ex.Message}");
            ConfirmDialogResult?.Invoke(null, false, $"Internal error: {ex.Message}");
        }
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

        try
        {
            showDestructiveDialog(title, message, destructiveButtonText, cancelButtonText, OnDestructiveDialogCallback);
        }
        catch (Exception ex)
        {
            Debug.LogError($"ShowDestructiveDialog error: {ex.Message}");
            DestructiveDialogResult?.Invoke(null, false, $"Internal error: {ex.Message}");
        }
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

        // Release previously allocated unmanaged memory (if any)
        if (s_optionsPtr != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(s_optionsPtr);
            s_optionsPtr = IntPtr.Zero;
        }
        if (s_stringPointers != null)
        {
            foreach (var ptr in s_stringPointers)
            {
                if (ptr != IntPtr.Zero) Marshal.FreeHGlobal(ptr);
            }
            s_stringPointers = null;
        }

        int optionCount = options.Length;

        try
        {
            // Convert managed string array to unmanaged UTF-8 buffer array
            s_stringPointers = new IntPtr[optionCount];
            for (int i = 0; i < optionCount; i++)
            {
                byte[] utf8Bytes = System.Text.Encoding.UTF8.GetBytes(options[i]);
                s_stringPointers[i] = Marshal.AllocHGlobal(utf8Bytes.Length + 1);
                Marshal.Copy(utf8Bytes, 0, s_stringPointers[i], utf8Bytes.Length);
                Marshal.WriteByte(s_stringPointers[i], utf8Bytes.Length, 0); // null terminator
            }
            s_optionsPtr = Marshal.AllocHGlobal(IntPtr.Size * optionCount);
            Marshal.Copy(s_stringPointers, 0, s_optionsPtr, optionCount);

            showActionSheet(title, message, s_optionsPtr, optionCount, cancelButtonText, OnActionSheetCallback);
        }
        catch (Exception ex)
        {
            Debug.LogError($"ShowActionSheet error: {ex.Message}");
            // Ensure memory is released on error
            if (s_optionsPtr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(s_optionsPtr);
                s_optionsPtr = IntPtr.Zero;
            }
            if (s_stringPointers != null)
            {
                foreach (var ptr in s_stringPointers)
                {
                    if (ptr != IntPtr.Zero) Marshal.FreeHGlobal(ptr);
                }
                s_stringPointers = null;
            }
            ActionSheetResult?.Invoke(null, false, $"Internal error: {ex.Message}");
        }
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

        try
        {
            showTextInputDialog(title, message, placeholder, confirmButtonText, cancelButtonText, enableConfirmWhenEmpty, OnTextInputDialogCallback);
        }
        catch (Exception ex)
        {
            Debug.LogError($"ShowTextInputDialog error: {ex.Message}");
            TextInputDialogResult?.Invoke(null, null, false, $"Internal error: {ex.Message}");
        }
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

        try
        {
            showLoginDialog(title, message, usernamePlaceholder, passwordPlaceholder, loginButtonText, cancelButtonText, enableLoginWhenEmpty, OnLoginDialogCallback);
        }
        catch (Exception ex)
        {
            Debug.LogError($"ShowLoginDialog error: {ex.Message}");
            LoginDialogResult?.Invoke(null, null, null, false, $"Internal error: {ex.Message}");
        }
    }
}
#endif
