#nullable enable

#if UNITY_IOS || UNITY_EDITOR
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;

public class IosDialogManagerExampleController : MonoBehaviour
{
    [SerializeField] private UIDocument? uiDocument;

    // UI refs
    private Label? _resultLabel;
    private Button? _btnDialog;
    private Button? _btnConfirm;
    private Button? _btnDestructive;
    private Button? _btnActionSheet;
    private Button? _btnTextInput;
    private Button? _btnLogin;

    [Header("Manual Safe Area Override (for testing)")]
    [SerializeField] private bool useManualSafeArea = false;
    [SerializeField] private int manualTopPadding = 0;
    [SerializeField] private int manualBottomPadding = 0;
    [SerializeField] private int manualLeftPadding = 0;
    [SerializeField] private int manualRightPadding = 0;

    // 画面回転監視用
    private ScreenOrientation lastOrientation;
    private Vector2 lastScreenSize;
    private float orientationCheckInterval = 0.3f;
    private float lastOrientationCheckTime;

    void Awake()
    {
#if UNITY_EDITOR
        Debug.Log("Running in Unity Editor - iOS simulation mode");
        UnityEditor.EditorUtility.DisplayDialog(
            "IosDialogManager Example",
            "This is a simulation of the iOS dialog manager.\nAll events will not be triggered.\nRun in iOS player for full functionality.",
            "OK");
#elif UNITY_IOS
    Debug.Log("Running on iOS device");
#else
    Debug.LogWarning("IosDialogManagerExampleController is only supported on iOS platform or Editor.");
    gameObject.SetActive(false);
    return;
#endif
        Debug.Log("[IosDialogManagerExampleController] initialized.");
    }

    private void Start()
    {
        if (uiDocument == null)
        {
            uiDocument = GetComponent<UIDocument>();
        }

        if (uiDocument == null)
        {
            Debug.LogError("[IosDialogManagerExampleController] UIDocument component not found!");
            return;
        }

        // 初期値を設定
        lastOrientation = Screen.orientation;
        lastScreenSize = new Vector2(Screen.width, Screen.height);

        InitializeUI();
        ApplyIosSafeArea();
        ApplyResponsiveClasses();

        Debug.Log("[IosDialogManagerExampleController] Initialized successfully");
    }

    private void Update()
    {
        // 効率的な画面サイズ変更チェック
        if (Time.time - lastOrientationCheckTime > orientationCheckInterval)
        {
            lastOrientationCheckTime = Time.time;
            CheckForOrientationChange();
        }
    }

    private void CheckForOrientationChange()
    {
        var currentOrientation = Screen.orientation;
        var currentScreenSize = new Vector2(Screen.width, Screen.height);

        if (currentOrientation != lastOrientation ||
            Vector2.Distance(lastScreenSize, currentScreenSize) > 1f)
        {
            Debug.Log($"[IosDialogManagerExampleController] Screen change detected - " +
                     $"Orientation: {lastOrientation} -> {currentOrientation}, " +
                     $"Size: {lastScreenSize} -> {currentScreenSize}");

            lastOrientation = currentOrientation;
            lastScreenSize = currentScreenSize;

            StartCoroutine(UpdateLayoutAfterChange());
        }
    }

    private IEnumerator UpdateLayoutAfterChange()
    {
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();

        ApplyResponsiveClasses();
        Debug.Log("[IosDialogManagerExampleController] Layout updated after screen change");
    }

    private void InitializeUI()
    {
        var root = uiDocument?.rootVisualElement;
        if (root == null)
        {
            Debug.LogError("[IosDialogManagerExampleController] rootVisualElement is null!");
            return;
        }
        _resultLabel = root.Q<Label>("ResultTextBlock");
        _btnDialog = root.Q<Button>("ShowDialogButton");
        _btnConfirm = root.Q<Button>("ShowConfirmDialogButton");
        _btnDestructive = root.Q<Button>("ShowDestructiveDialogButton");
        _btnActionSheet = root.Q<Button>("ShowActionSheetButton");
        _btnTextInput = root.Q<Button>("ShowTextInputDialogButton");
        _btnLogin = root.Q<Button>("ShowLoginDialogButton");

        if (_resultLabel == null || _btnDialog == null || _btnConfirm == null || _btnDestructive == null || _btnActionSheet == null || _btnTextInput == null || _btnLogin == null)
        {
            Debug.LogError("[IosDialogManagerExampleController] One or more UI elements are missing in UXML.");
            return;
        }

        if (_btnDialog != null) _btnDialog.clicked += OnShowDialogClicked;
        if (_btnConfirm != null) _btnConfirm.clicked += OnShowConfirmDialogClicked;
        if (_btnDestructive != null) _btnDestructive.clicked += OnShowDestructiveDialogClicked;
        if (_btnActionSheet != null) _btnActionSheet.clicked += OnShowActionSheetClicked;
        if (_btnTextInput != null) _btnTextInput.clicked += OnShowTextInputDialogClicked;
        if (_btnLogin != null) _btnLogin.clicked += OnShowLoginDialogClicked;

#if UNITY_IOS && !UNITY_EDITOR
        IosDialogManager.Instance.DialogResult += OnDialogResult;
        IosDialogManager.Instance.ConfirmDialogResult += OnConfirmDialogResult;
        IosDialogManager.Instance.DestructiveDialogResult += OnDestructiveDialogResult;
        IosDialogManager.Instance.ActionSheetResult += OnActionSheetResult;
        IosDialogManager.Instance.TextInputDialogResult += OnTextInputDialogResult;
        IosDialogManager.Instance.LoginDialogResult += OnLoginDialogResult;
#endif
    }

    private void OnDestroy()
    {
        if (_btnDialog != null) _btnDialog.clicked -= OnShowDialogClicked;
        if (_btnConfirm != null) _btnConfirm.clicked -= OnShowConfirmDialogClicked;
        if (_btnDestructive != null) _btnDestructive.clicked -= OnShowDestructiveDialogClicked;
        if (_btnActionSheet != null) _btnActionSheet.clicked -= OnShowActionSheetClicked;
        if (_btnTextInput != null) _btnTextInput.clicked -= OnShowTextInputDialogClicked;
        if (_btnLogin != null) _btnLogin.clicked -= OnShowLoginDialogClicked;

#if UNITY_IOS && !UNITY_EDITOR
        IosDialogManager.Instance.DialogResult -= OnDialogResult;
        IosDialogManager.Instance.ConfirmDialogResult -= OnConfirmDialogResult;
        IosDialogManager.Instance.DestructiveDialogResult -= OnDestructiveDialogResult;
        IosDialogManager.Instance.ActionSheetResult -= OnActionSheetResult;
        IosDialogManager.Instance.TextInputDialogResult -= OnTextInputDialogResult;
        IosDialogManager.Instance.LoginDialogResult -= OnLoginDialogResult;
#endif
    }

    #region Dialog Event Handlers

    private void OnShowDialogClicked()
    {
        Debug.Log("[IosDialogManagerExampleController] OnShowDialogClicked triggered");
#if UNITY_IOS && !UNITY_EDITOR
        string title = "Hello from Unity";
        string message = "This is a native iOS dialog!";
        string buttonText = "OK";
        IosDialogManager.Instance.ShowDialog(title, message, buttonText);
#endif
    }

    private void OnShowConfirmDialogClicked()
    {
        Debug.Log("[IosDialogManagerExampleController] OnShowConfirmDialogClicked triggered");
#if UNITY_IOS && !UNITY_EDITOR
        string title = "Confirm Action";
        string message = "Are you sure you want to proceed?";
        string confirmButtonText = "Yes";
        string cancelButtonText = "No";
        IosDialogManager.Instance.ShowConfirmDialog(title, message, confirmButtonText, cancelButtonText);
#endif
    }

    private void OnShowDestructiveDialogClicked()
    {
        Debug.Log("[IosDialogManagerExampleController] OnShowDestructiveDialogClicked triggered");
#if UNITY_IOS && !UNITY_EDITOR
        string title = "Delete File";
        string message = "This action cannot be undone. Are you sure?";
        string destructiveButtonText = "Delete";
        string cancelButtonText = "Cancel";
        IosDialogManager.Instance.ShowDestructiveDialog(title, message, destructiveButtonText, cancelButtonText);
#endif
    }

    private void OnShowActionSheetClicked()
    {
        Debug.Log("[IosDialogManagerExampleController] OnShowActionSheetClicked triggered");
#if UNITY_IOS && !UNITY_EDITOR
        string title = "Select Source";
        string message = "Choose where to get the file from";
        string[] options = { "Camera", "Photo Library", "Documents" };
        string cancelButtonText = "Cancel";
        IosDialogManager.Instance.ShowActionSheet(title, message, options, cancelButtonText);
#endif
    }

    private void OnShowTextInputDialogClicked()
    {
        Debug.Log("[IosDialogManagerExampleController] OnShowTextInputDialogClicked triggered");
#if UNITY_IOS && !UNITY_EDITOR
        string title = "Enter Name";
        string message = "Please enter your name";
        string placeholder = "Your name here";
        string confirmButtonText = "OK";
        string cancelButtonText = "Cancel";
        bool enableConfirmWhenEmpty = false;
        IosDialogManager.Instance.ShowTextInputDialog(title, message, placeholder, confirmButtonText, cancelButtonText, enableConfirmWhenEmpty);
#endif
    }

    private void OnShowLoginDialogClicked()
    {
        Debug.Log("[IosDialogManagerExampleController] OnShowLoginDialogClicked triggered");
#if UNITY_IOS && !UNITY_EDITOR
        string title = "Login Required";
        string message = "Please enter your credentials";
        string usernamePlaceholder = "Username";
        string passwordPlaceholder = "Password";
        string loginButtonText = "Login";
        string cancelButtonText = "Cancel";
        bool enableLoginWhenEmpty = false;
        IosDialogManager.Instance.ShowLoginDialog(title, message, usernamePlaceholder, passwordPlaceholder, loginButtonText, cancelButtonText, enableLoginWhenEmpty);
#endif
    }

    #endregion

    #region Safe Area and Responsive Layout

    private void ApplyIosSafeArea()
    {
        StartCoroutine(ApplySafeAreaCoroutine());
    }

    private IEnumerator ApplySafeAreaCoroutine()
    {
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();

        var root = uiDocument?.rootVisualElement;
        if (root == null)
        {
            Debug.LogError("[IosDialogManagerExampleController] rootVisualElement is null!");
            yield break;
        }

        float topPadding, bottomPadding, leftPadding, rightPadding;
        if (useManualSafeArea)
        {
            topPadding = manualTopPadding;
            bottomPadding = manualBottomPadding;
            leftPadding = manualLeftPadding;
            rightPadding = manualRightPadding;
            Debug.Log($"[IosDialogManagerExampleController] Using manual Safe Area values - Top: {topPadding}, Bottom: {bottomPadding}, Left: {leftPadding}, Right: {rightPadding}");
        }
        else
        {
            topPadding = 0;
            bottomPadding = 0;
            leftPadding = 0;
            rightPadding = 0;
            Debug.Log("[IosDialogManagerExampleController] Using default Safe Area values for iOS");
        }

        root.style.paddingTop = StyleKeyword.Null;
        root.style.paddingBottom = StyleKeyword.Null;
        root.style.paddingLeft = StyleKeyword.Null;
        root.style.paddingRight = StyleKeyword.Null;

        yield return null;

        root.style.paddingTop = topPadding;
        root.style.paddingBottom = bottomPadding;
        root.style.paddingLeft = leftPadding;
        root.style.paddingRight = rightPadding;
    }

    private void ApplyResponsiveClasses()
    {
        var root = uiDocument?.rootVisualElement;
        var screenWidth = Screen.width;
        var dpi = Screen.dpi;

        if (root == null)
        {
            Debug.LogError("[IosDialogManagerExampleController] rootVisualElement is null!");
            return;
        }

        // 既存のクラスを削除
        root.RemoveFromClassList("small-screen");
        root.RemoveFromClassList("medium-screen");
        root.RemoveFromClassList("large-screen");
        root.RemoveFromClassList("low-dpi");
        root.RemoveFromClassList("medium-dpi");
        root.RemoveFromClassList("high-dpi");

        // DPIが無効な場合のデフォルト値
        if (dpi <= 0 || float.IsNaN(dpi))
        {
            dpi = 326f; // iOS Retina標準DPI
        }

        // DPIに基づく調整係数を計算
        float dpiScale = dpi / 326f; // 326dpiを基準（1.0）とする
        float adjustedWidth = screenWidth / dpiScale; // DPIで正規化した幅

        // 画面サイズクラスを追加（DPI調整済み）
        if (adjustedWidth <= 390) // iPhone 12 Mini以下
        {
            root.AddToClassList("small-screen");
        }
        else if (adjustedWidth <= 428) // iPhone 12 Pro Max以下
        {
            root.AddToClassList("medium-screen");
        }
        else
        {
            root.AddToClassList("large-screen"); // iPad等
        }

        // DPIクラスを追加
        if (dpi <= 264) // iPad等
        {
            root.AddToClassList("low-dpi");
        }
        else if (dpi <= 326) // iPhone標準
        {
            root.AddToClassList("medium-dpi");
        }
        else
        {
            root.AddToClassList("high-dpi"); // iPhone Plus/Pro Max等
        }

        Debug.Log($"[IosDialogManagerExampleController] Screen: {screenWidth}px, DPI: {dpi}, Adjusted Width: {adjustedWidth:F0}px, Applied classes: " +
                 $"{(adjustedWidth <= 390 ? "small-screen" : adjustedWidth <= 428 ? "medium-screen" : "large-screen")}, " +
                 $"{(dpi <= 264 ? "low-dpi" : dpi <= 326 ? "medium-dpi" : "high-dpi")}");
    }

    #endregion

    #region IosDialogManager Event Handlers

    private void OnDialogResult(string? buttonText, bool isSuccess, string? errorMessage)
    {
        Debug.Log($"[IosDialogManagerExampleController] OnDialogResult buttonText: {buttonText ?? "null"}, isSuccess: {isSuccess}, errorMessage: {errorMessage ?? "null"}");
        if (_resultLabel != null)
        {
            _resultLabel.text = (isSuccess ? "OK" : "Error") +
                                $"\nShowDialog buttonText: {buttonText ?? "null"}, isSuccess: {isSuccess}, errorMessage: {errorMessage ?? "null"}";
        }
        else
        {
            Debug.LogError("[IosDialogManagerExampleController] Result label is null!");
        }
    }

    private void OnConfirmDialogResult(string? buttonText, bool isSuccess, string? errorMessage)
    {
        Debug.Log($"[IosDialogManagerExampleController] OnConfirmDialogResult buttonText: {buttonText ?? "null"}, isSuccess: {isSuccess}, errorMessage: {errorMessage ?? "null"}");
        if (_resultLabel != null)
        {
            _resultLabel.text = (isSuccess ? "OK" : "Error") +
                                $"\nShowConfirmDialog buttonText: {buttonText ?? "null"}, isSuccess: {isSuccess}, errorMessage: {errorMessage ?? "null"}";
        }
        else
        {
            Debug.LogError("[IosDialogManagerExampleController] Result label is null!");
        }
    }

    private void OnDestructiveDialogResult(string? buttonText, bool isSuccess, string? errorMessage)
    {
        Debug.Log($"[IosDialogManagerExampleController] OnDestructiveDialogResult buttonText: {buttonText ?? "null"}, isSuccess: {isSuccess}, errorMessage: {errorMessage ?? "null"}");
        if (_resultLabel != null)
        {
            _resultLabel.text = (isSuccess ? "OK" : "Error") +
                                $"\nShowDestructiveDialog buttonText: {buttonText ?? "null"}, isSuccess: {isSuccess}, errorMessage: {errorMessage ?? "null"}";
        }
        else
        {
            Debug.LogError("[IosDialogManagerExampleController] Result label is null!");
        }
    }

    private void OnActionSheetResult(string? buttonText, bool isSuccess, string? errorMessage)
    {
        Debug.Log($"[IosDialogManagerExampleController] OnActionSheetResult buttonText: {buttonText ?? "null"}, isSuccess: {isSuccess}, errorMessage: {errorMessage ?? "null"}");
        if (_resultLabel != null)
        {
            _resultLabel.text = (isSuccess ? "OK" : "Error") +
                                $"\nShowActionSheet buttonText: {buttonText ?? "null"}, isSuccess: {isSuccess}, errorMessage: {errorMessage ?? "null"}";
        }
        else
        {
            Debug.LogError("[IosDialogManagerExampleController] Result label is null!");
        }
    }

    private void OnTextInputDialogResult(string? buttonText, string? inputText, bool isSuccess, string? errorMessage)
    {
        Debug.Log($"[IosDialogManagerExampleController] OnTextInputDialogResult buttonText: {buttonText ?? "null"}, inputText: {inputText ?? "null"}, isSuccess: {isSuccess}, errorMessage: {errorMessage ?? "null"}");
        if (_resultLabel != null)
        {
            _resultLabel.text = (isSuccess ? "OK" : "Error") +
                                $"\nShowTextInputDialog buttonText: {buttonText ?? "null"}, inputText: {inputText ?? "null"}, isSuccess: {isSuccess}, errorMessage: {errorMessage ?? "null"}";
        }
        else
        {
            Debug.LogError("[IosDialogManagerExampleController] Result label is null!");
        }
    }

    private void OnLoginDialogResult(string? buttonText, string? username, string? password, bool isSuccess, string? errorMessage)
    {
        Debug.Log($"[IosDialogManagerExampleController] OnLoginDialogResult buttonText: {buttonText ?? "null"}, username: {username ?? "null"}, password: {password ?? "null"}, isSuccess: {isSuccess}, errorMessage: {errorMessage ?? "null"}");
        if (_resultLabel != null)
        {
            _resultLabel.text = (isSuccess ? "OK" : "Error") +
                                $"\nShowLoginDialog buttonText: {buttonText ?? "null"}, username: {username ?? "null"}, password: {password ?? "null"}, isSuccess: {isSuccess}, errorMessage: {errorMessage ?? "null"}";
        }
        else
        {
            Debug.LogError("[IosDialogManagerExampleController] Result label is null!");
        }
    }

    #endregion
}
#endif