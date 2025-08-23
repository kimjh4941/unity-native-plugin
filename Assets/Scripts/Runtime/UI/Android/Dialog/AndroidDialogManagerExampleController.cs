#nullable enable

#if UNITY_ANDROID || UNITY_EDITOR
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;
using System.Linq;

public class AndroidDialogManagerExampleController : MonoBehaviour
{
    [SerializeField] private UIDocument? uiDocument;

    // UI refs
    private Label? _resultLabel;
    private Button? _btnDialog;
    private Button? _btnConfirm;
    private Button? _btnSingleChoice;
    private Button? _btnMultiChoice;
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
        Debug.Log("Running in Unity Editor - Android simulation mode");
        UnityEditor.EditorUtility.DisplayDialog(
            "AndroidDialogManager Example",
            "This is a simulation of the Android dialog manager.\nAll events will not be triggered.\nRun in Android player for full functionality.",
            "OK");
#elif UNITY_ANDROID
    Debug.Log("Running on Android device");
#else
    Debug.LogWarning("AndroidDialogManagerExampleController is only supported on Android platform or Editor.");
    gameObject.SetActive(false);
    return;
#endif
        Debug.Log("AndroidDialogManagerExampleController initialized successfully.");
    }

    private void Start()
    {
        if (uiDocument == null)
        {
            uiDocument = GetComponent<UIDocument>();
        }

        if (uiDocument == null)
        {
            Debug.LogError("[AndroidDialogManagerExampleController] UIDocument component not found!");
            return;
        }

        // 初期値を設定
        lastOrientation = Screen.orientation;
        lastScreenSize = new Vector2(Screen.width, Screen.height);

        InitializeUI();
        ApplyAndroidSafeArea();
        ApplyResponsiveClasses();

        Debug.Log("[AndroidDialogManagerExampleController] Initialized successfully");
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
            Debug.Log($"[AndroidDialogManagerExampleController] Screen change detected - " +
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
        Debug.Log("[AndroidDialogManagerExampleController] Layout updated after screen change");
    }

    private void InitializeUI()
    {
        var root = uiDocument?.rootVisualElement;
        if (root == null)
        {
            Debug.LogError("[AndroidDialogManagerExampleController] rootVisualElement is null!");
            return;
        }

        _resultLabel = root.Q<Label>("ResultTextBlock");
        _btnDialog = root.Q<Button>("ShowDialogButton");
        _btnConfirm = root.Q<Button>("ShowConfirmDialogButton");
        _btnSingleChoice = root.Q<Button>("ShowSingleChoiceItemDialogButton");
        _btnMultiChoice = root.Q<Button>("ShowMultiChoiceItemDialogButton");
        _btnTextInput = root.Q<Button>("ShowTextInputDialogButton");
        _btnLogin = root.Q<Button>("ShowLoginDialogButton");

        // Wire clicks
        if (_btnDialog != null) _btnDialog.clicked += OnShowDialogClicked;
        if (_btnConfirm != null) _btnConfirm.clicked += OnShowConfirmDialogClicked;
        if (_btnSingleChoice != null) _btnSingleChoice.clicked += OnShowSingleChoiceItemDialogClicked;
        if (_btnMultiChoice != null) _btnMultiChoice.clicked += OnShowMultiChoiceItemDialogClicked;
        if (_btnTextInput != null) _btnTextInput.clicked += OnShowTextInputDialogClicked;
        if (_btnLogin != null) _btnLogin.clicked += OnShowLoginDialogClicked;

#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidDialogManager.Instance.DialogResult += OnDialogResult;
        AndroidDialogManager.Instance.ConfirmDialogResult += OnConfirmDialogResult;
        AndroidDialogManager.Instance.SingleChoiceItemDialogResult += OnSingleChoiceItemDialogResult;
        AndroidDialogManager.Instance.MultiChoiceItemDialogResult += OnMultiChoiceItemDialogResult;
        AndroidDialogManager.Instance.TextInputDialogResult += OnTextInputDialogResult;
        AndroidDialogManager.Instance.LoginDialogResult += OnLoginDialogResult;
#endif
    }

    void OnDestroy()
    {
        if (_btnDialog != null) _btnDialog.clicked -= OnShowDialogClicked;
        if (_btnConfirm != null) _btnConfirm.clicked -= OnShowConfirmDialogClicked;
        if (_btnSingleChoice != null) _btnSingleChoice.clicked -= OnShowSingleChoiceItemDialogClicked;
        if (_btnMultiChoice != null) _btnMultiChoice.clicked -= OnShowMultiChoiceItemDialogClicked;
        if (_btnTextInput != null) _btnTextInput.clicked -= OnShowTextInputDialogClicked;
        if (_btnLogin != null) _btnLogin.clicked -= OnShowLoginDialogClicked;

#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidDialogManager.Instance.DialogResult -= OnDialogResult;
        AndroidDialogManager.Instance.ConfirmDialogResult -= OnConfirmDialogResult;
        AndroidDialogManager.Instance.SingleChoiceItemDialogResult -= OnSingleChoiceItemDialogResult;
        AndroidDialogManager.Instance.MultiChoiceItemDialogResult -= OnMultiChoiceItemDialogResult;
        AndroidDialogManager.Instance.TextInputDialogResult -= OnTextInputDialogResult;
        AndroidDialogManager.Instance.LoginDialogResult -= OnLoginDialogResult;
#endif
    }

    #region Dialog Event Handlers

    private void OnShowDialogClicked()
    {
        Debug.Log("[AndroidDialogManagerExampleController] OnShowDialogClicked triggered");
#if UNITY_ANDROID && !UNITY_EDITOR
        string title = "Hello from Unity";
        string message = "This is a native Android dialog!";
        string buttonText = "OK";
        bool cancelableOnTouchOutside = false;
        bool cancelable = false;
        AndroidDialogManager.Instance.ShowDialog(
            title,
            message,
            buttonText,
            cancelableOnTouchOutside,
            cancelable
        );
#endif
    }

    private void OnShowConfirmDialogClicked()
    {
        Debug.Log("[AndroidDialogManagerExampleController] OnShowConfirmDialogClicked triggered");
#if UNITY_ANDROID && !UNITY_EDITOR
        string title = "Confirmation";
        string message = "Do you want to proceed with this action?";
        string negativeButtonText = "No";
        string positiveButtonText = "Yes";
        bool cancelableOnTouchOutside = false;
        bool cancelable = false;
        AndroidDialogManager.Instance.ShowConfirmDialog(
            title,
            message,
            negativeButtonText,
            positiveButtonText,
            cancelableOnTouchOutside,
            cancelable
        );
#endif
    }

    private void OnShowSingleChoiceItemDialogClicked()
    {
        Debug.Log("[AndroidDialogManagerExampleController] OnShowSingleChoiceItemDialogClicked triggered");
#if UNITY_ANDROID && !UNITY_EDITOR
        string title = "Please select one";
        string[] singleChoiceItems = { "Option 1", "Option 2", "Option 3" };
        int checkedItem = 0;
        string negativeButtonText = "Cancel";
        string positiveButtonText = "OK";
        bool cancelableOnTouchOutside = false;
        bool cancelable = false;
        AndroidDialogManager.Instance.ShowSingleChoiceItemDialog(
            title,
            singleChoiceItems,
            checkedItem, // Default selection
            negativeButtonText,
            positiveButtonText,
            cancelableOnTouchOutside,
            cancelable
        );
#endif
    }

    private void OnShowMultiChoiceItemDialogClicked()
    {
        Debug.Log("[AndroidDialogManagerExampleController] OnShowMultiChoiceItemDialogClicked triggered");
#if UNITY_ANDROID && !UNITY_EDITOR
        string title = "Multiple Selection";
        string[] multiChoiceItems = { "Item 1", "Item 2", "Item 3", "Item 4" };
        bool[] checkedItems = { false, true, false, true }; // Default selection state
        string negativeButtonText = "Cancel";
        string positiveButtonText = "OK";
        bool cancelableOnTouchOutside = false;
        bool cancelable = false;
        AndroidDialogManager.Instance.ShowMultiChoiceItemDialog(
            title,
            multiChoiceItems,
            checkedItems,
            negativeButtonText,
            positiveButtonText,
            cancelableOnTouchOutside,
            cancelable
        );
#endif
    }

    private void OnShowTextInputDialogClicked()
    {
        Debug.Log("[AndroidDialogManagerExampleController] OnShowTextInputDialogClicked triggered");
#if UNITY_ANDROID && !UNITY_EDITOR
        string title = "Text Input";
        string message = "Please enter your name";
        string placeholder = "Enter here...";
        string negativeButtonText = "Cancel";
        string positiveButtonText = "OK";
        bool enablePositiveButtonWhenEmpty = false;
        bool cancelableOnTouchOutside = false;
        bool cancelable = false;
        AndroidDialogManager.Instance.ShowTextInputDialog(
            title,
            message,
            placeholder,
            negativeButtonText,
            positiveButtonText,
            enablePositiveButtonWhenEmpty,
            cancelableOnTouchOutside,
            cancelable
        );
#endif
    }

    private void OnShowLoginDialogClicked()
    {
        Debug.Log("[AndroidDialogManagerExampleController] OnShowLoginDialogClicked triggered");
#if UNITY_ANDROID && !UNITY_EDITOR
        string title = "Login";
        string message = "Please enter your credentials";
        string usernameHint = "Username";
        string passwordHint = "Password";
        string negativeButtonText = "Cancel";
        string positiveButtonText = "Login";
        bool enablePositiveButtonWhenEmpty = false;
        bool cancelableOnTouchOutside = false;
        bool cancelable = false;
        AndroidDialogManager.Instance.ShowLoginDialog(
            title,
            message,
            usernameHint,
            passwordHint,
            negativeButtonText,
            positiveButtonText,
            enablePositiveButtonWhenEmpty,
            cancelableOnTouchOutside,
            cancelable
        );
#endif
    }

    #endregion

    #region Safe Area and Responsive Layout

    private void ApplyAndroidSafeArea()
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
            Debug.LogError("[AndroidDialogManagerExampleController] rootVisualElement is null!");
            yield break;
        }

        float topPadding, bottomPadding, leftPadding, rightPadding;
        if (useManualSafeArea)
        {
            topPadding = manualTopPadding;
            bottomPadding = manualBottomPadding;
            leftPadding = manualLeftPadding;
            rightPadding = manualRightPadding;
            Debug.Log($"[AndroidDialogManagerExampleController] Using manual Safe Area values - Top: {topPadding}, Bottom: {bottomPadding}, Left: {leftPadding}, Right: {rightPadding}");
        }
        else
        {
            topPadding = 0;
            bottomPadding = 0;
            leftPadding = 0;
            rightPadding = 0;
            Debug.Log("[AndroidDialogManagerExampleController] Using default Safe Area values");
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
        if (root == null)
        {
            Debug.LogError("[AndroidDialogManagerExampleController] rootVisualElement is null!");
            return;
        }
        var screenWidth = Screen.width;
        var dpi = Screen.dpi;

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
            dpi = 160f; // Android標準DPI
        }

        // DPIに基づく調整係数を計算
        float dpiScale = dpi / 160f; // 160dpiを基準（1.0）とする
        float adjustedWidth = screenWidth / dpiScale; // DPIで正規化した幅

        // 画面サイズクラスを追加（DPI調整済み）
        if (adjustedWidth <= 480)
        {
            root.AddToClassList("small-screen");
        }
        else if (adjustedWidth <= 768)
        {
            root.AddToClassList("medium-screen");
        }
        else
        {
            root.AddToClassList("large-screen");
        }

        // DPIクラスを追加
        if (dpi <= 160)
        {
            root.AddToClassList("low-dpi");
        }
        else if (dpi <= 240)
        {
            root.AddToClassList("medium-dpi");
        }
        else
        {
            root.AddToClassList("high-dpi");
        }

        Debug.Log($"[AndroidDialogManagerExampleController] Screen: {screenWidth}px, DPI: {dpi}, Adjusted Width: {adjustedWidth:F0}px, Applied classes: " +
                 $"{(adjustedWidth <= 480 ? "small-screen" : adjustedWidth <= 768 ? "medium-screen" : "large-screen")}, " +
                 $"{(dpi <= 160 ? "low-dpi" : dpi <= 240 ? "medium-dpi" : "high-dpi")}");
    }

    #endregion

    #region AndroidDialogManager Event Handlers

    private void OnDialogResult(string? buttonText, bool isSuccess, string? errorMessage)
    {
        Debug.Log($"[AndroidDialogManagerExampleController] OnDialogResult buttonText: {buttonText ?? "null"}, isSuccess: {isSuccess}, errorMessage: {errorMessage ?? "null"}");
        if (_resultLabel != null)
        {
            _resultLabel.text = (isSuccess ? "OK" : "Error") +
                                $"\nShowDialog buttonText: {buttonText ?? "null"}, isSuccess: {isSuccess}, errorMessage: {errorMessage ?? "null"}";
        }
        else
        {
            Debug.LogError("[AndroidDialogManagerExampleController] Result label is null!");
        }
    }

    private void OnConfirmDialogResult(string? buttonText, bool isSuccess, string? errorMessage)
    {
        Debug.Log($"[AndroidDialogManagerExampleController] OnConfirmDialogResult buttonText: {buttonText ?? "null"}, isSuccess: {isSuccess}, errorMessage: {errorMessage ?? "null"}");
        if (_resultLabel != null)
        {
            _resultLabel.text = (isSuccess ? "OK" : "Error") +
                                $"\nShowConfirmDialog buttonText: {buttonText ?? "null"}, isSuccess: {isSuccess}, errorMessage: {errorMessage ?? "null"}";
        }
        else
        {
            Debug.LogError("[AndroidDialogManagerExampleController] Result label is null!");
        }
    }

    private void OnSingleChoiceItemDialogResult(string? buttonText, int? checkedItem, bool isSuccess, string? errorMessage)
    {
        Debug.Log($"[AndroidDialogManagerExampleController] OnSingleChoiceItemDialogResult buttonText: {buttonText ?? "null"}, checkedItem: {(checkedItem.HasValue ? checkedItem.Value.ToString() : "null")}, isSuccess: {isSuccess}, errorMessage: {errorMessage ?? "null"}");
        if (_resultLabel != null)
        {
            _resultLabel.text = (isSuccess ? "OK" : "Error") +
                                $"\nShowSingleChoiceItemDialog buttonText: {buttonText ?? "null"}, " +
                                $"checkedItem: {(checkedItem.HasValue ? checkedItem.Value.ToString() : "null")}, " +
                                $"isSuccess: {isSuccess}, errorMessage: {errorMessage ?? "null"}";
        }
        else
        {
            Debug.LogError("[AndroidDialogManagerExampleController] Result label is null!");
        }
    }

    private void OnMultiChoiceItemDialogResult(string? buttonText, bool[]? checkedItems, bool isSuccess, string? errorMessage)
    {
        Debug.Log($"[AndroidDialogManagerExampleController] OnMultiChoiceItemDialogResult buttonText: {buttonText ?? "null"}, " +
                  $"checkedItems: {(checkedItems != null ? string.Join(",", checkedItems.Select(b => b ? "True" : "False")) : "null")}, " +
                  $"isSuccess: {isSuccess}, errorMessage: {errorMessage ?? "null"}");
        if (_resultLabel != null)
        {
            var state = checkedItems != null ? string.Join(",", checkedItems.Select(b => b ? "True" : "False")) : null;
            _resultLabel.text = (isSuccess ? "OK" : "Error") +
                                $"\nShowMultiChoiceItemDialog buttonText: {buttonText ?? "null"}, " +
                                $"checkedItems: {(state != null ? $"[{state}]" : "null")}, isSuccess: {isSuccess}, errorMessage: {errorMessage ?? "null"}";
        }
        else
        {
            Debug.LogError("[AndroidDialogManagerExampleController] Result label is null!");
        }
    }

    private void OnTextInputDialogResult(string? buttonText, string? inputText, bool isSuccess, string? errorMessage)
    {
        Debug.Log($"[AndroidDialogManagerExampleController] OnTextInputDialogResult buttonText: {buttonText ?? "null"}, inputText: {inputText ?? "null"}, isSuccess: {isSuccess}, errorMessage: {errorMessage ?? "null"}");
        if (_resultLabel != null)
        {
            _resultLabel.text = (isSuccess ? "OK" : "Error") +
                                $"\nShowTextInputDialog buttonText: {buttonText ?? "null"}, " +
                                $"inputText: {inputText ?? "null"}, isSuccess: {isSuccess}, errorMessage: {errorMessage ?? "null"}";
        }
        else
        {
            Debug.LogError("[AndroidDialogManagerExampleController] Result label is null!");
        }
    }

    private void OnLoginDialogResult(string? buttonText, string? username, string? password, bool isSuccess, string? errorMessage)
    {
        Debug.Log($"[AndroidDialogManagerExampleController] OnLoginDialogResult buttonText: {buttonText ?? "null"}, " +
                  $"username: {username ?? "null"}, password: {password ?? "null"}, isSuccess: {isSuccess}, errorMessage: {errorMessage ?? "null"}");
        if (_resultLabel != null)
        {
            _resultLabel.text = (isSuccess ? "OK" : "Error") +
                                $"\nShowLoginDialog buttonText: {buttonText ?? "null"}, " +
                                $"username: {username ?? "null"}, password: {password ?? "null"}, isSuccess: {isSuccess}, errorMessage: {errorMessage ?? "null"}";
        }
        else
        {
            Debug.LogError("[AndroidDialogManagerExampleController] Result label is null!");
        }
    }

    #endregion
}
#endif