#if UNITY_IOS
using UnityEngine;
using System.Runtime.InteropServices;
using System;
using AOT;

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

    // ダイアログ結果を受け取るためのイベント（isSuccessとerrorMessageを追加）
    public event Action<string, bool, string> DialogResult; // result, isSuccess, errorMessage
    public event Action<string, bool, string> ConfirmDialogResult; // result, isSuccess, errorMessage
    public event Action<string, bool, string> DestructiveDialogResult; // result, isSuccess, errorMessage
    public event Action<string, bool, string> ActionSheetResult; // result, isSuccess, errorMessage
    public event Action<string, string, bool, string> TextInputDialogResult; // buttonPressed, inputText, isSuccess, errorMessage
    public event Action<string, string, string, bool, string> LoginDialogResult; // buttonPressed, username, password, isSuccess, errorMessage

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

    // ネイティブコードとのインターフェース用デリゲート定義（IL2CPP対応）
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void DialogCallback(string buttonPressed, bool isSuccess, string errorMessage);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void ConfirmDialogCallback(string buttonPressed, bool isSuccess, string errorMessage);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void DestructiveDialogCallback(string buttonPressed, bool isSuccess, string errorMessage);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void ActionSheetCallback(string buttonPressed, bool isSuccess, string errorMessage);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void TextInputDialogCallback(string buttonPressed, string inputText, bool isSuccess, string errorMessage);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void LoginDialogCallback(string buttonPressed, string username, string password, bool isSuccess, string errorMessage);

    // ネイティブ関数のインポート（enableパラメータ追加）
    [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
    private static extern void showDialog(string title, string message, string buttonText, DialogCallback callback);

    [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
    private static extern void showConfirmDialog(string title, string message, string confirmButtonText, string cancelButtonText, ConfirmDialogCallback callback);

    [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
    private static extern void showDestructiveDialog(string title, string message, string destructiveButtonText, string cancelButtonText, DestructiveDialogCallback callback);

    [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
    private static extern void showActionSheet(string title, string message, IntPtr options, int optionCount, string cancelButtonText, ActionSheetCallback callback);

    [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
    private static extern void showTextInputDialog(string title, string message, string placeholder, string confirmButtonText, string cancelButtonText, bool enableConfirmWhenEmpty, TextInputDialogCallback callback);

    [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
    private static extern void showLoginDialog(string title, string message, string usernamePlaceholder, string passwordPlaceholder, string loginButtonText, string cancelButtonText, bool enableLoginWhenEmpty, LoginDialogCallback callback);

    // ActionSheet用のメモリ管理（静的変数）
    private static IntPtr s_optionsPtr = IntPtr.Zero;
    private static IntPtr[] s_stringPointers = null;

    // 静的コールバックメソッド群（IL2CPP対応）
    [MonoPInvokeCallback(typeof(DialogCallback))]
    private static void OnDialogCallback(string buttonPressed, bool isSuccess, string errorMessage)
    {
        UnityMainThreadDispatcher.Instance.Enqueue(() =>
        {
            try
            {
                Instance.DialogResult?.Invoke(buttonPressed, isSuccess, errorMessage);
            }
            catch (Exception ex)
            {
                Debug.LogError($"DialogResult callback error: {ex.Message}");
            }
        });
    }

    [MonoPInvokeCallback(typeof(ConfirmDialogCallback))]
    private static void OnConfirmDialogCallback(string buttonPressed, bool isSuccess, string errorMessage)
    {
        UnityMainThreadDispatcher.Instance.Enqueue(() =>
        {
            try
            {
                Instance.ConfirmDialogResult?.Invoke(buttonPressed, isSuccess, errorMessage);
            }
            catch (Exception ex)
            {
                Debug.LogError($"ConfirmDialogResult callback error: {ex.Message}");
            }
        });
    }

    [MonoPInvokeCallback(typeof(DestructiveDialogCallback))]
    private static void OnDestructiveDialogCallback(string buttonPressed, bool isSuccess, string errorMessage)
    {
        UnityMainThreadDispatcher.Instance.Enqueue(() =>
        {
            try
            {
                Instance.DestructiveDialogResult?.Invoke(buttonPressed, isSuccess, errorMessage);
            }
            catch (Exception ex)
            {
                Debug.LogError($"DestructiveDialogResult callback error: {ex.Message}");
            }
        });
    }

    [MonoPInvokeCallback(typeof(ActionSheetCallback))]
    private static void OnActionSheetCallback(string buttonPressed, bool isSuccess, string errorMessage)
    {
        UnityMainThreadDispatcher.Instance.Enqueue(() =>
        {
            try
            {
                Instance.ActionSheetResult?.Invoke(buttonPressed, isSuccess, errorMessage);
            }
            catch (Exception ex)
            {
                Debug.LogError($"ActionSheetResult callback error: {ex.Message}");
            }
            finally
            {
                // ActionSheetのメモリ解放処理を直接記載
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
    private static void OnTextInputDialogCallback(string buttonPressed, string inputText, bool isSuccess, string errorMessage)
    {
        UnityMainThreadDispatcher.Instance.Enqueue(() =>
        {
            try
            {
                Instance.TextInputDialogResult?.Invoke(buttonPressed, inputText, isSuccess, errorMessage);
            }
            catch (Exception ex)
            {
                Debug.LogError($"TextInputDialogResult callback error: {ex.Message}");
            }
        });
    }

    [MonoPInvokeCallback(typeof(LoginDialogCallback))]
    private static void OnLoginDialogCallback(string buttonPressed, string username, string password, bool isSuccess, string errorMessage)
    {
        UnityMainThreadDispatcher.Instance.Enqueue(() =>
        {
            try
            {
                Instance.LoginDialogResult?.Invoke(buttonPressed, username, password, isSuccess, errorMessage);
            }
            catch (Exception ex)
            {
                Debug.LogError($"LoginDialogResult callback error: {ex.Message}");
            }
        });
    }

    // パブリックメソッド群（IL2CPP対応 - 静的コールバック使用）

    /// <summary>
    /// 基本的なアラートダイアログを表示
    /// </summary>
    public void ShowDialog(string title, string message, string buttonText = "OK")
    {
        Debug.Log($"ShowDialog called with title: {title}, message: {message}");
        if (string.IsNullOrEmpty(title))
        {
            Debug.LogError("Title cannot be null or empty.");
            DialogResult?.Invoke("Error: Title cannot be null or empty.", false, "Title validation failed");
            return;
        }

        try
        {
            showDialog(title, message, buttonText, OnDialogCallback);
        }
        catch (Exception ex)
        {
            Debug.LogError($"ShowDialog error: {ex.Message}");
            DialogResult?.Invoke("", false, $"Internal error: {ex.Message}");
        }
    }

    /// <summary>
    /// 確認ダイアログを表示（OK/Cancelボタン）
    /// </summary>
    public void ShowConfirmDialog(string title, string message, string confirmButtonText = "OK", string cancelButtonText = "Cancel")
    {
        Debug.Log($"ShowConfirmDialog called with title: {title}, message: {message}");
        if (string.IsNullOrEmpty(title))
        {
            Debug.LogError("Title cannot be null or empty.");
            ConfirmDialogResult?.Invoke("Error: Title cannot be null or empty.", false, "Title validation failed");
            return;
        }

        try
        {
            showConfirmDialog(title, message, confirmButtonText, cancelButtonText, OnConfirmDialogCallback);
        }
        catch (Exception ex)
        {
            Debug.LogError($"ShowConfirmDialog error: {ex.Message}");
            ConfirmDialogResult?.Invoke("", false, $"Internal error: {ex.Message}");
        }
    }

    /// <summary>
    /// 破壊的操作の確認ダイアログを表示（削除確認など）
    /// </summary>
    public void ShowDestructiveDialog(string title, string message, string destructiveButtonText = "Delete", string cancelButtonText = "Cancel")
    {
        Debug.Log($"ShowDestructiveDialog called with title: {title}, message: {message}");
        if (string.IsNullOrEmpty(title))
        {
            Debug.LogError("Title cannot be null or empty.");
            DestructiveDialogResult?.Invoke("Error: Title cannot be null or empty.", false, "Title validation failed");
            return;
        }

        try
        {
            showDestructiveDialog(title, message, destructiveButtonText, cancelButtonText, OnDestructiveDialogCallback);
        }
        catch (Exception ex)
        {
            Debug.LogError($"ShowDestructiveDialog error: {ex.Message}");
            DestructiveDialogResult?.Invoke("", false, $"Internal error: {ex.Message}");
        }
    }

    /// <summary>
    /// アクションシートを表示（複数選択肢）
    /// </summary>
    public void ShowActionSheet(string title, string message, string[] options, string cancelButtonText = "Cancel")
    {
        Debug.Log($"ShowActionSheet called with title: {title}, message: {message}, options: {options?.Length}, cancelButtonText: {cancelButtonText}");
        if (options == null || options.Length == 0)
        {
            Debug.LogError("Options cannot be null or empty.");
            ActionSheetResult?.Invoke("Error: Options cannot be null or empty.", false, "Options validation failed");
            return;
        }

        // 前回のメモリを解放
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
            // 文字列配列をアンマネージドメモリに変換
            s_stringPointers = new IntPtr[optionCount];
            for (int i = 0; i < optionCount; i++)
            {
                byte[] utf8Bytes = System.Text.Encoding.UTF8.GetBytes(options[i]);
                s_stringPointers[i] = Marshal.AllocHGlobal(utf8Bytes.Length + 1);
                Marshal.Copy(utf8Bytes, 0, s_stringPointers[i], utf8Bytes.Length);
                Marshal.WriteByte(s_stringPointers[i], utf8Bytes.Length, 0); // null終端文字
            }
            s_optionsPtr = Marshal.AllocHGlobal(IntPtr.Size * optionCount);
            Marshal.Copy(s_stringPointers, 0, s_optionsPtr, optionCount);

            showActionSheet(title, message, s_optionsPtr, optionCount, cancelButtonText, OnActionSheetCallback);
        }
        catch (Exception ex)
        {
            Debug.LogError($"ShowActionSheet error: {ex.Message}");
            // エラー時もメモリを解放
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
            ActionSheetResult?.Invoke("", false, $"Internal error: {ex.Message}");
        }
    }

    /// <summary>
    /// テキスト入力ダイアログを表示
    /// </summary>
    /// <param name="enableConfirmWhenEmpty">空の入力でもOKボタンを有効にするかどうか（デフォルト: false）</param>
    public void ShowTextInputDialog(string title, string message, string placeholder = "", string confirmButtonText = "OK", string cancelButtonText = "Cancel", bool enableConfirmWhenEmpty = false)
    {
        Debug.Log($"ShowTextInputDialog called with title: {title}, message: {message}, enableConfirmWhenEmpty: {enableConfirmWhenEmpty}");
        if (string.IsNullOrEmpty(title))
        {
            Debug.LogError("Title cannot be null or empty.");
            TextInputDialogResult?.Invoke("Error", "Title cannot be null or empty.", false, "Title validation failed");
            return;
        }

        try
        {
            showTextInputDialog(title, message, placeholder, confirmButtonText, cancelButtonText, enableConfirmWhenEmpty, OnTextInputDialogCallback);
        }
        catch (Exception ex)
        {
            Debug.LogError($"ShowTextInputDialog error: {ex.Message}");
            TextInputDialogResult?.Invoke("Error", "", false, $"Internal error: {ex.Message}");
        }
    }

    /// <summary>
    /// ログインダイアログを表示（ユーザー名・パスワード入力）
    /// </summary>
    /// <param name="enableLoginWhenEmpty">空の入力でもログインボタンを有効にするかどうか（デフォルト: false）</param>
    public void ShowLoginDialog(string title, string message, string usernamePlaceholder = "Username", string passwordPlaceholder = "Password", string loginButtonText = "Login", string cancelButtonText = "Cancel", bool enableLoginWhenEmpty = false)
    {
        Debug.Log($"ShowLoginDialog called with title: {title}, message: {message}, enableLoginWhenEmpty: {enableLoginWhenEmpty}");
        if (string.IsNullOrEmpty(title))
        {
            Debug.LogError("Title cannot be null or empty.");
            LoginDialogResult?.Invoke("Error", "Title cannot be null or empty.", "", false, "Title validation failed");
            return;
        }

        try
        {
            showLoginDialog(title, message, usernamePlaceholder, passwordPlaceholder, loginButtonText, cancelButtonText, enableLoginWhenEmpty, OnLoginDialogCallback);
        }
        catch (Exception ex)
        {
            Debug.LogError($"ShowLoginDialog error: {ex.Message}");
            LoginDialogResult?.Invoke("Error", "", "", false, $"Internal error: {ex.Message}");
        }
    }
}
#endif
