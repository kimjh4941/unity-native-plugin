using UnityEngine;
using UnityEngine.UIElements;

public class IosDialogManagerExampleController : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;

    // エディタから設定できるようにpublic setterを追加
    public UIDocument UIDocument
    {
        get => uiDocument;
        set => uiDocument = value;
    }

    // UI要素の参照
    private Button showDialogButton;
    private Button showConfirmDialogButton;
    private Button showDestructiveDialogButton;
    private Button showActionSheetButton;
    private Button showTextInputDialogButton;
    private Button showLoginDialogButton;

    private void Start()
    {
        // UIDocumentが設定されていない場合は取得
        if (uiDocument == null)
        {
            uiDocument = GetComponent<UIDocument>();
        }

        if (uiDocument == null)
        {
            Debug.LogError("[IosDialogExample] UIDocument component not found!");
            return;
        }

        InitializeUI();
        SetupButtonEvents();
    }

    private void InitializeUI()
    {
        var root = uiDocument.rootVisualElement;

        Debug.Log($"[IosDialogExample] Root element children count: {root.childCount}");
        DebugUIHierarchy(root, 0);

        // ボタン要素を取得（iOS特有のボタン名に対応）
        showDialogButton = root.Q<Button>("ShowDialog");
        showConfirmDialogButton = root.Q<Button>("ShowConfirmDialog");
        showDestructiveDialogButton = root.Q<Button>("ShowDestructiveDialog");
        showActionSheetButton = root.Q<Button>("ShowActionSheet");
        showTextInputDialogButton = root.Q<Button>("ShowTextInputDialog");
        showLoginDialogButton = root.Q<Button>("ShowLoginDialog");

        // ボタンが見つからない場合の詳細ログ
        LogButtonStatus("ShowDialog", showDialogButton);
        LogButtonStatus("ShowConfirmDialog", showConfirmDialogButton);
        LogButtonStatus("ShowDestructiveDialog", showDestructiveDialogButton);
        LogButtonStatus("ShowActionSheet", showActionSheetButton);
        LogButtonStatus("ShowTextInputDialog", showTextInputDialogButton);
        LogButtonStatus("ShowLoginDialog", showLoginDialogButton);
    }

    private void DebugUIHierarchy(VisualElement element, int depth)
    {
        var indent = new string(' ', depth * 2);
        Debug.Log($"[IosDialogExample] {indent}{element.GetType().Name} - name: '{element.name}', text: '{GetElementText(element)}'");

        foreach (var child in element.Children())
        {
            DebugUIHierarchy(child, depth + 1);
        }
    }

    private string GetElementText(VisualElement element)
    {
        if (element is Label label)
            return label.text;
        if (element is Button button)
            return button.text;
        return "";
    }

    private void LogButtonStatus(string buttonName, Button button)
    {
        if (button != null)
        {
            Debug.Log($"[IosDialogExample] ✓ {buttonName} button found successfully");
        }
        else
        {
            Debug.LogWarning($"[IosDialogExample] ✗ {buttonName} button not found");
        }
    }

    private void SetupButtonEvents()
    {
        // 各ボタンにイベントハンドラーを設定
        showDialogButton?.RegisterCallback<ClickEvent>(OnShowDialogClicked);
        showConfirmDialogButton?.RegisterCallback<ClickEvent>(OnShowConfirmDialogClicked);
        showDestructiveDialogButton?.RegisterCallback<ClickEvent>(OnShowDestructiveDialogClicked);
        showActionSheetButton?.RegisterCallback<ClickEvent>(OnShowActionSheetClicked);
        showTextInputDialogButton?.RegisterCallback<ClickEvent>(OnShowTextInputDialogClicked);
        showLoginDialogButton?.RegisterCallback<ClickEvent>(OnShowLoginDialogClicked);

        Debug.Log("[IosDialogExample] Button events initialized successfully");
    }

    #region Button Event Handlers

    private void OnShowDialogClicked(ClickEvent evt)
    {
        Debug.Log("[IosDialogExample] ShowDialog button clicked");

#if UNITY_IOS && !UNITY_EDITOR
        if (IosDialogManager.Instance != null)
        {
            IosDialogManager.Instance.ShowDialog(
                "Hello from Unity",
                "This is a native iOS dialog!",
                "OK"
            );
        }
#else
        Debug.Log("[IosDialogExample] ShowDialog - Running in Editor mode");
        ShowEditorDialog("Hello from Unity", "This is a native iOS dialog!");
#endif
    }

    private void OnShowConfirmDialogClicked(ClickEvent evt)
    {
        Debug.Log("[IosDialogExample] ShowConfirmDialog button clicked");

#if UNITY_IOS && !UNITY_EDITOR
        if (IosDialogManager.Instance != null)
        {
            IosDialogManager.Instance.ShowConfirmDialog(
                "Confirm Action",
                "Are you sure you want to proceed?",
                "Yes",
                "No"
            );
        }
#else
        Debug.Log("[IosDialogExample] ShowConfirmDialog - Running in Editor mode");
        ShowEditorConfirmDialog("Confirm Action", "Are you sure you want to proceed?");
#endif
    }

    private void OnShowDestructiveDialogClicked(ClickEvent evt)
    {
        Debug.Log("[IosDialogExample] ShowDestructiveDialog button clicked");

#if UNITY_IOS && !UNITY_EDITOR
        if (IosDialogManager.Instance != null)
        {
            IosDialogManager.Instance.ShowDestructiveDialog(
                "Delete File",
                "This action cannot be undone. Are you sure?",
                "Delete",
                "Cancel"
            );
        }
#else
        Debug.Log("[IosDialogExample] ShowDestructiveDialog - Running in Editor mode");
        ShowEditorDestructiveDialog("Delete File", "This action cannot be undone. Are you sure?");
#endif
    }

    private void OnShowActionSheetClicked(ClickEvent evt)
    {
        Debug.Log("[IosDialogExample] ShowActionSheet button clicked");

#if UNITY_IOS && !UNITY_EDITOR
        if (IosDialogManager.Instance != null)
        {
            string[] options = { "Camera", "Photo Library", "Documents" };
            IosDialogManager.Instance.ShowActionSheet(
                "Select Source",
                "Choose where to get the file from",
                options,
                "Cancel"
            );
        }
#else
        Debug.Log("[IosDialogExample] ShowActionSheet - Running in Editor mode");
        string[] options = { "Camera", "Photo Library", "Documents" };
        ShowEditorActionSheet("Select Source", "Choose where to get the file from", options);
#endif
    }

    private void OnShowTextInputDialogClicked(ClickEvent evt)
    {
        Debug.Log("[IosDialogExample] ShowTextInputDialog button clicked");

#if UNITY_IOS && !UNITY_EDITOR
        if (IosDialogManager.Instance != null)
        {
            IosDialogManager.Instance.ShowTextInputDialog(
                "Enter Name",
                "Please enter your name",
                "Your name here",
                "OK",
                "Cancel",
                false // cancelable
            );
        }
#else
        Debug.Log("[IosDialogExample] ShowTextInputDialog - Running in Editor mode");
        ShowEditorTextInputDialog("Enter Name", "Please enter your name");
#endif
    }

    private void OnShowLoginDialogClicked(ClickEvent evt)
    {
        Debug.Log("[IosDialogExample] ShowLoginDialog button clicked");

#if UNITY_IOS && !UNITY_EDITOR
        if (IosDialogManager.Instance != null)
        {
            IosDialogManager.Instance.ShowLoginDialog(
                "Login Required",
                "Please enter your credentials",
                "Username",
                "Password",
                "Login",
                "Cancel",
                false // cancelable
            );
        }
#else
        Debug.Log("[IosDialogExample] ShowLoginDialog - Running in Editor mode");
        ShowEditorLoginDialog("Login Required", "Please enter your credentials");
#endif
    }

    #endregion

    #region Editor Mode Fallback Methods

    private void ShowEditorDialog(string title, string message)
    {
#if UNITY_EDITOR
        bool result = UnityEditor.EditorUtility.DisplayDialog(title, message, "OK");
        Debug.Log($"[IosDialogExample] Editor Dialog - Result: {result}");

        // 実際のiOSダイアログの結果をシミュレート
        OnDialogResult("OK", true, "");
#else
    Debug.LogWarning("[IosDialogExample] ShowEditorDialog called but not in editor mode");
#endif
    }

    private void ShowEditorConfirmDialog(string title, string message)
    {
#if UNITY_EDITOR
        bool result = UnityEditor.EditorUtility.DisplayDialog(title, message, "Yes", "No");

        if (result)
        {
            Debug.Log("[IosDialogExample] Editor Confirm Dialog - Yes clicked");
            OnConfirmDialogResult("Yes", true, "");
        }
        else
        {
            Debug.Log("[IosDialogExample] Editor Confirm Dialog - No clicked");
            OnConfirmDialogResult("No", true, "");
        }
#else
    Debug.LogWarning("[IosDialogExample] ShowEditorConfirmDialog called but not in editor mode");
#endif
    }

    private void ShowEditorDestructiveDialog(string title, string message)
    {
#if UNITY_EDITOR
        bool result = UnityEditor.EditorUtility.DisplayDialog(title, message, "Delete", "Cancel");

        if (result)
        {
            Debug.Log("[IosDialogExample] Editor Destructive Dialog - Delete clicked");
            OnDestructiveDialogResult("Delete", true, "");
        }
        else
        {
            Debug.Log("[IosDialogExample] Editor Destructive Dialog - Cancel clicked");
            OnDestructiveDialogResult("Cancel", true, "");
        }
#else
    Debug.LogWarning("[IosDialogExample] ShowEditorDestructiveDialog called but not in editor mode");
#endif
    }

    private void ShowEditorActionSheet(string title, string message, string[] options)
    {
#if UNITY_EDITOR
        string optionsText = string.Join("\n", options);

        int choice = UnityEditor.EditorUtility.DisplayDialogComplex(
            title,
            $"{message}\n\n{optionsText}",
            options.Length > 0 ? options[0] : "Option 1",
            options.Length > 1 ? options[1] : "Option 2",
            "Cancel"
        );

        Debug.Log($"[IosDialogExample] Editor Action Sheet - Selected: {choice}");

        if (choice < 2 && choice < options.Length)
        {
            OnActionSheetResult(options[choice], true, "");
        }
        else
        {
            OnActionSheetResult("Cancel", true, "");
        }
#else
    Debug.LogWarning("[IosDialogExample] ShowEditorActionSheet called but not in editor mode");
#endif
    }

    private void ShowEditorTextInputDialog(string title, string message)
    {
#if UNITY_EDITOR
        bool result = UnityEditor.EditorUtility.DisplayDialog(
            title,
            $"{message}\n\n(エディタモードでは 'Sample Text' が入力されたとして処理されます)",
            "OK",
            "Cancel"
        );

        if (result)
        {
            OnTextInputDialogResult("OK", "Sample Text", true, "");
            Debug.Log("[IosDialogExample] Editor Text Input - Sample text entered");
        }
        else
        {
            OnTextInputDialogResult("Cancel", "", true, "");
            Debug.Log("[IosDialogExample] Editor Text Input - Cancelled");
        }
#else
    Debug.LogWarning("[IosDialogExample] ShowEditorTextInputDialog called but not in editor mode");
#endif
    }

    private void ShowEditorLoginDialog(string title, string message)
    {
#if UNITY_EDITOR
        bool result = UnityEditor.EditorUtility.DisplayDialog(
            title,
            $"{message}\n\n(エディタモードでは 'admin'/'password' でログインしたとして処理されます)",
            "Login",
            "Cancel"
        );

        if (result)
        {
            OnLoginDialogResult("Login", "admin", "password", true, "");
            Debug.Log("[IosDialogExample] Editor Login - Sample credentials used");
        }
        else
        {
            OnLoginDialogResult("Cancel", "", "", true, "");
            Debug.Log("[IosDialogExample] Editor Login - Cancelled");
        }
#else
    Debug.LogWarning("[IosDialogExample] ShowEditorLoginDialog called but not in editor mode");
#endif
    }

    #endregion

    #region Event Subscription for IosDialogManager

    private void OnEnable()
    {
        // IosDialogManagerのイベントを購読（IosDialogPluginと同じシグネチャ）
#if UNITY_IOS && !UNITY_EDITOR
        if (IosDialogManager.Instance != null)
        {
            IosDialogManager.Instance.DialogResult += OnDialogResult;
            IosDialogManager.Instance.ConfirmDialogResult += OnConfirmDialogResult;
            IosDialogManager.Instance.DestructiveDialogResult += OnDestructiveDialogResult;
            IosDialogManager.Instance.ActionSheetResult += OnActionSheetResult;
            IosDialogManager.Instance.TextInputDialogResult += OnTextInputDialogResult;
            IosDialogManager.Instance.LoginDialogResult += OnLoginDialogResult;
        }
#endif
    }

    private void OnDisable()
    {
        // イベントの購読を解除
#if UNITY_IOS && !UNITY_EDITOR
        if (IosDialogManager.Instance != null)
        {
            IosDialogManager.Instance.DialogResult -= OnDialogResult;
            IosDialogManager.Instance.ConfirmDialogResult -= OnConfirmDialogResult;
            IosDialogManager.Instance.DestructiveDialogResult -= OnDestructiveDialogResult;
            IosDialogManager.Instance.ActionSheetResult -= OnActionSheetResult;
            IosDialogManager.Instance.TextInputDialogResult -= OnTextInputDialogResult;
            IosDialogManager.Instance.LoginDialogResult -= OnLoginDialogResult;
        }
#endif
    }

    #endregion

    #region IosDialogManager Event Handlers (IosDialogPluginと同じシグネチャ)

    private void OnDialogResult(string result, bool isSuccess, string errorMessage)
    {
        Debug.Log($"[IosDialogExample] DialogResult result: {result}, isSuccess: {isSuccess}, errorMessage: {errorMessage}");
    }

    private void OnConfirmDialogResult(string result, bool isSuccess, string errorMessage)
    {
        Debug.Log($"[IosDialogExample] ConfirmDialogResult result: {result}, isSuccess: {isSuccess}, errorMessage: {errorMessage}");
    }

    private void OnDestructiveDialogResult(string result, bool isSuccess, string errorMessage)
    {
        Debug.Log($"[IosDialogExample] DestructiveDialogResult result: {result}, isSuccess: {isSuccess}, errorMessage: {errorMessage}");
    }

    private void OnActionSheetResult(string result, bool isSuccess, string errorMessage)
    {
        Debug.Log($"[IosDialogExample] ActionSheetResult result: {result}, isSuccess: {isSuccess}, errorMessage: {errorMessage}");
    }

    private void OnTextInputDialogResult(string buttonPressed, string inputText, bool isSuccess, string errorMessage)
    {
        Debug.Log($"[IosDialogExample] TextInputDialogResult buttonPressed: {buttonPressed}, inputText: {inputText}, isSuccess: {isSuccess}, errorMessage: {errorMessage}");
    }

    private void OnLoginDialogResult(string buttonPressed, string username, string password, bool isSuccess, string errorMessage)
    {
        Debug.Log($"[IosDialogExample] LoginDialogResult buttonPressed: {buttonPressed}, username: {username}, password: [HIDDEN], isSuccess: {isSuccess}, errorMessage: {errorMessage}");
    }

    #endregion
}