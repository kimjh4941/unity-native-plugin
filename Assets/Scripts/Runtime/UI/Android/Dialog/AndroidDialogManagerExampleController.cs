#nullable enable

using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

[System.Serializable]
public class AndroidDialogButtonData
{
    public string? text;
    public string? action;
    public string? buttonId;
}

public class AndroidDialogManagerExampleController : MonoBehaviour
{
    [SerializeField] private UIDocument? uiDocument;

    // ListView用のデータ
    private ListView? dialogListView;
    private readonly List<AndroidDialogButtonData> dialogButtons = new()
    {
        new AndroidDialogButtonData { text = "ShowDialog", action = "ShowDialog", buttonId = "ShowDialog" },
        new AndroidDialogButtonData { text = "ShowConfirmDialog", action = "ShowConfirmDialog", buttonId = "ShowConfirmDialog" },
        new AndroidDialogButtonData { text = "ShowSingleChoiceItemDialog", action = "ShowSingleChoiceItemDialog", buttonId = "ShowSingleChoiceItemDialog" },
        new AndroidDialogButtonData { text = "ShowMultiChoiceItemDialog", action = "ShowMultiChoiceItemDialog", buttonId = "ShowMultiChoiceItemDialog" },
        new AndroidDialogButtonData { text = "ShowTextInputDialog", action = "ShowTextInputDialog", buttonId = "ShowTextInputDialog" },
        new AndroidDialogButtonData { text = "ShowLoginDialog", action = "ShowLoginDialog", buttonId = "ShowLoginDialog" }
    };

    [Header("Manual Safe Area Override (for testing)")]
    [SerializeField] private bool useManualSafeArea = false;
    [SerializeField] private int manualTopPadding = 40;
    [SerializeField] private int manualBottomPadding = 20;
    [SerializeField] private int manualLeftPadding = 20;
    [SerializeField] private int manualRightPadding = 20;

    // 画面回転監視用
    private ScreenOrientation lastOrientation;
    private Vector2 lastScreenSize;
    private float orientationCheckInterval = 0.3f;
    private float lastOrientationCheckTime;

    void Awake()
    {
#if UNITY_EDITOR
        Debug.Log("Running in Unity Editor - Android simulation mode");
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
        dialogListView = root.Q<ListView>("DialogListView");

        if (dialogListView == null)
        {
            Debug.LogError("[AndroidDialogManagerExampleController] DialogListView not found!");
            return;
        }

        SetupListView();
        Debug.Log("[AndroidDialogManagerExampleController] ListView initialized with " + dialogButtons.Count + " items");
    }

    private void SetupListView()
    {
        if (dialogListView == null)
        {
            Debug.LogError("[AndroidDialogManagerExampleController] dialogListView is null!");
            return;
        }
        // ListViewのデータソースを設定
        dialogListView.itemsSource = dialogButtons;

        // アイテム作成関数（Labelを作成）
        dialogListView.makeItem = () =>
        {
            var label = new Label();
            label.AddToClassList("android-dialog-list-item");
            return label;
        };

        // アイテムバインド関数
        dialogListView.bindItem = (element, index) =>
        {
            var buttonData = dialogButtons[index];

            if (element is not Label label || buttonData == null)
            {
                Debug.LogError($"[ListView] Invalid element or button data at index {index}");
                return;
            }
            label.text = buttonData.text;
            label.name = buttonData.buttonId;

            // 既存のアイテム別クラスを削除
            label.RemoveFromClassList("show-dialog-item");
            label.RemoveFromClassList("show-confirm-dialog-item");
            label.RemoveFromClassList("show-single-choice-item-dialog-item");
            label.RemoveFromClassList("show-multi-choice-item-dialog-item");
            label.RemoveFromClassList("show-text-input-dialog-item");
            label.RemoveFromClassList("show-login-dialog-item");

            // アイテム別のクラスを追加（色分け用）
            string itemClass = GetItemClassFromButtonId(buttonData.buttonId ?? string.Empty);
            label.AddToClassList(itemClass);

            // デバッグ用：テキストが設定されているか確認
            Debug.Log($"[ListView] Binding item {index}: {buttonData.text}");
        };

        // アイテム解放関数
        dialogListView.unbindItem = (element, index) =>
        {
            var label = element as Label;
            // クリーンアップ
        };

        // 選択変更イベントを登録
        dialogListView.selectionChanged += OnListViewSelectionChanged;
        dialogListView.Rebuild();

        // デバッグ用：ListViewの状態を確認
        Debug.Log($"[ListView] Setup complete. Item count: {dialogButtons.Count}, FixedItemHeight: {dialogListView.fixedItemHeight}");
    }

    private string GetItemClassFromButtonId(string buttonId)
    {
        switch (buttonId)
        {
            case "ShowDialog":
                return "show-dialog-item";
            case "ShowConfirmDialog":
                return "show-confirm-dialog-item";
            case "ShowSingleChoiceItemDialog":
                return "show-single-choice-item-dialog-item";
            case "ShowMultiChoiceItemDialog":
                return "show-multi-choice-item-dialog-item";
            case "ShowTextInputDialog":
                return "show-text-input-dialog-item";
            case "ShowLoginDialog":
                return "show-login-dialog-item";
            default:
                return "android-dialog-list-item";
        }
    }

    private void OnListViewSelectionChanged(IEnumerable<object> selectedItems)
    {
        foreach (AndroidDialogButtonData item in selectedItems.Cast<AndroidDialogButtonData>())
        {
            Debug.Log($"[AndroidDialogManagerExampleController] ListView item selected: {item.text}");
            if (!string.IsNullOrEmpty(item.action))
            {
                OnDialogButtonClicked(item.action);
            }
            else
            {
                Debug.LogError("[AndroidDialogManagerExampleController] Button action is null or empty.");
            }

            // 選択をクリア（連続クリックを可能にする）
            dialogListView?.ClearSelection();
            break;
        }
    }

    private void OnDialogButtonClicked(string action)
    {
        Debug.Log($"[AndroidDialogManagerExampleController] Dialog action triggered: {action}");

        switch (action)
        {
            case "ShowDialog":
                OnShowDialogClicked();
                break;
            case "ShowConfirmDialog":
                OnShowConfirmDialogClicked();
                break;
            case "ShowSingleChoiceItemDialog":
                OnShowSingleChoiceItemDialogClicked();
                break;
            case "ShowMultiChoiceItemDialog":
                OnShowMultiChoiceItemDialogClicked();
                break;
            case "ShowTextInputDialog":
                OnShowTextInputDialogClicked();
                break;
            case "ShowLoginDialog":
                OnShowLoginDialogClicked();
                break;
            default:
                Debug.LogWarning($"[AndroidDialogManagerExampleController] Unknown action: {action}");
                break;
        }
    }

    #region Dialog Event Handlers

    private void OnShowDialogClicked()
    {
        Debug.Log("[AndroidDialogManagerExampleController] Show Dialog triggered");

#if UNITY_ANDROID && !UNITY_EDITOR
        if (AndroidDialogManager.Instance != null)
        {
            AndroidDialogManager.Instance.ShowDialog(
                "Hello from Unity",
                "This is a native Android dialog!",
                "OK",
                false, // cancelableOnTouchOutside
                false  // cancelable
            );
        }
#else
        Debug.Log("[AndroidDialogManagerExampleController] ShowDialog - Running in Editor mode");
        ShowEditorDialog("Hello from Unity", "This is a native Android dialog!");
#endif
    }

    private void OnShowConfirmDialogClicked()
    {
        Debug.Log("[AndroidDialogManagerExampleController] Show Confirm Dialog triggered");

#if UNITY_ANDROID && !UNITY_EDITOR
        if (AndroidDialogManager.Instance != null)
        {
            AndroidDialogManager.Instance.ShowConfirmDialog(
                "Confirmation",
                "Do you want to proceed with this action?",
                "No",
                "Yes",
                false, // cancelableOnTouchOutside
                false  // cancelable
            );
        }
#else
        Debug.Log("[AndroidDialogManagerExampleController] ShowConfirmDialog - Running in Editor mode");
        ShowEditorConfirmDialog("Confirmation", "Do you want to proceed with this action?");
#endif
    }

    private void OnShowSingleChoiceItemDialogClicked()
    {
        Debug.Log("[AndroidDialogManagerExampleController] Show Single Choice Item Dialog triggered");

#if UNITY_ANDROID && !UNITY_EDITOR
        if (AndroidDialogManager.Instance != null)
        {
            string[] options = { "Option 1", "Option 2", "Option 3", "Option 4" };
            AndroidDialogManager.Instance.ShowSingleChoiceItemDialog(
                "Please select one",
                options,
                0,      // defaultSelection
                "Cancel",
                "OK",
                false,  // cancelableOnTouchOutside
                false   // cancelable
            );
        }
#else
        Debug.Log("[AndroidDialogManagerExampleController] ShowSingleChoiceItemDialog - Running in Editor mode");
        string[] options = { "Option 1", "Option 2", "Option 3", "Option 4" };
        ShowEditorSingleChoiceDialog("Please select one", options);
#endif
    }

    private void OnShowMultiChoiceItemDialogClicked()
    {
        Debug.Log("[AndroidDialogManagerExampleController] Show Multi Choice Item Dialog triggered");

#if UNITY_ANDROID && !UNITY_EDITOR
        if (AndroidDialogManager.Instance != null)
        {
            string[] items = { "Item 1", "Item 2", "Item 3", "Item 4" };
            bool[] checkedItems = { false, true, false, true };
            AndroidDialogManager.Instance.ShowMultiChoiceItemDialog(
                "Multiple Selection",
                items,
                checkedItems,
                "Cancel",
                "OK",
                false, // cancelableOnTouchOutside
                false  // cancelable
            );
        }
#else
        Debug.Log("[AndroidDialogManagerExampleController] ShowMultiChoiceItemDialog - Running in Editor mode");
        string[] items = { "Item 1", "Item 2", "Item 3", "Item 4" };
        ShowEditorMultiChoiceDialog("Multiple Selection", items);
#endif
    }

    private void OnShowTextInputDialogClicked()
    {
        Debug.Log("[AndroidDialogManagerExampleController] Show Text Input Dialog triggered");

#if UNITY_ANDROID && !UNITY_EDITOR
        if (AndroidDialogManager.Instance != null)
        {
            AndroidDialogManager.Instance.ShowTextInputDialog(
                "Text Input",
                "Please enter your name",
                "Enter here...",
                "Cancel",
                "OK",
                false, // enablePositiveButtonWhenEmpty
                false, // cancelableOnTouchOutside
                false  // cancelable
            );
        }
#else
        Debug.Log("[AndroidDialogManagerExampleController] ShowTextInputDialog - Running in Editor mode");
        ShowEditorTextInputDialog("Text Input", "Please enter your name");
#endif
    }

    private void OnShowLoginDialogClicked()
    {
        Debug.Log("[AndroidDialogManagerExampleController] Show Login Dialog triggered");

#if UNITY_ANDROID && !UNITY_EDITOR
        if (AndroidDialogManager.Instance != null)
        {
            AndroidDialogManager.Instance.ShowLoginDialog(
                "Login",
                "Please enter your credentials",
                "Username",
                "Password",
                "Cancel",
                "Login",
                false, // enablePositiveButtonWhenEmpty
                false, // cancelableOnTouchOutside
                false  // cancelable
            );
        }
#else
        Debug.Log("[AndroidDialogManagerExampleController] ShowLoginDialog - Running in Editor mode");
        ShowEditorLoginDialog("Login", "Please enter your credentials");
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

#if UNITY_ANDROID && !UNITY_EDITOR
        
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
            var safeArea = Screen.safeArea;
            var screenWidth = Screen.width;
            var screenHeight = Screen.height;
            
            bool hasSafeArea = (safeArea.width < screenWidth) || (safeArea.height < screenHeight);
            
            if (hasSafeArea)
            {
                var topInset = screenHeight - safeArea.height - safeArea.y;
                var bottomInset = safeArea.y;
                var leftInset = safeArea.x;
                var rightInset = screenWidth - safeArea.width - safeArea.x;
                
                topPadding = Mathf.Clamp(topInset, 30, 60);
                bottomPadding = Mathf.Clamp(bottomInset, 20, 40);
                leftPadding = Mathf.Clamp(leftInset, 20, 30);
                rightPadding = Mathf.Clamp(rightInset, 20, 30);
            }
            else
            {
                topPadding = 40;
                bottomPadding = 20;
                leftPadding = 20;
                rightPadding = 20;
            }
            
            Debug.Log($"[AndroidDialogManagerExampleController] Auto-calculated Safe Area - Top: {topPadding}, Bottom: {bottomPadding}, Left: {leftPadding}, Right: {rightPadding}");
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
        
#else
        root.style.paddingTop = 50;
        root.style.paddingBottom = 20;
        root.style.paddingLeft = 20;
        root.style.paddingRight = 20;
        Debug.Log("[AndroidDialogManagerExampleController] Applied default safe area values for editor");
#endif

        root.AddToClassList("android-safe-area-container");
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

    #region Editor Mode Fallback Methods

    private void ShowEditorDialog(string title, string message)
    {
#if UNITY_EDITOR
        if (UnityEditor.EditorUtility.DisplayDialog(title, message, "OK"))
        {
            Debug.Log("[AndroidDialogManagerExampleController] Editor Dialog - OK clicked");
        }
#else
        Debug.Log($"[AndroidDialogManagerExampleController] ShowEditorDialog called: {title} - {message}");
#endif
    }

    private void ShowEditorConfirmDialog(string title, string message)
    {
#if UNITY_EDITOR
        if (UnityEditor.EditorUtility.DisplayDialog(title, message, "Yes", "No"))
        {
            Debug.Log("[AndroidDialogManagerExampleController] Editor Confirm Dialog - Yes clicked");
        }
        else
        {
            Debug.Log("[AndroidDialogManagerExampleController] Editor Confirm Dialog - No clicked");
        }
#else
        Debug.Log($"[AndroidDialogManagerExampleController] ShowEditorConfirmDialog called: {title} - {message}");
#endif
    }

    private void ShowEditorSingleChoiceDialog(string title, string[] items)
    {
#if UNITY_EDITOR
        int selectedIndex = UnityEditor.EditorUtility.DisplayDialogComplex(title, $"Available options:\n{string.Join("\n", items)}", items[0], items[1], "Cancel");
        Debug.Log($"[AndroidDialogManagerExampleController] Editor Single Choice - Selected index: {selectedIndex}");
#else
        Debug.Log($"[AndroidDialogManagerExampleController] ShowEditorSingleChoiceDialog called: {title} - {string.Join(", ", items)}");
#endif
    }

    private void ShowEditorMultiChoiceDialog(string title, string[] items)
    {
#if UNITY_EDITOR
        Debug.Log($"[AndroidDialogManagerExampleController] Editor Multi Choice - Available items: {string.Join(", ", items)}");
        UnityEditor.EditorUtility.DisplayDialog(title, $"Items: {string.Join(", ", items)}", "OK");
#else
        Debug.Log($"[AndroidDialogManagerExampleController] ShowEditorMultiChoiceDialog called: {title} - {string.Join(", ", items)}");
#endif
    }

    private void ShowEditorTextInputDialog(string title, string message)
    {
#if UNITY_EDITOR
        Debug.Log($"[AndroidDialogManagerExampleController] Editor Text Input - {title}: {message}");
        UnityEditor.EditorUtility.DisplayDialog(title, message, "OK");
#else
        Debug.Log($"[AndroidDialogManagerExampleController] ShowEditorTextInputDialog called: {title} - {message}");
#endif
    }

    private void ShowEditorLoginDialog(string title, string message)
    {
#if UNITY_EDITOR
        Debug.Log($"[AndroidDialogManagerExampleController] Editor Login Dialog - {title}: {message}");
        UnityEditor.EditorUtility.DisplayDialog(title, message, "OK");
#else
        Debug.Log($"[AndroidDialogManagerExampleController] ShowEditorLoginDialog called: {title} - {message}");
#endif
    }

    #endregion

    #region Event Subscription for AndroidDialogManager

    private void OnEnable()
    {
        // AndroidDialogManagerのイベントを購読
#if UNITY_ANDROID && !UNITY_EDITOR
        if (AndroidDialogManager.Instance != null)
        {
            AndroidDialogManager.Instance.DialogResult += OnDialogResult;
            AndroidDialogManager.Instance.ConfirmDialogResult += OnConfirmDialogResult;
            AndroidDialogManager.Instance.SingleChoiceItemDialogResult += OnSingleChoiceItemDialogResult;
            AndroidDialogManager.Instance.MultiChoiceItemDialogResult += OnMultiChoiceItemDialogResult;
            AndroidDialogManager.Instance.TextInputDialogResult += OnTextInputDialogResult;
            AndroidDialogManager.Instance.LoginDialogResult += OnLoginDialogResult;
        }
#endif
    }

    private void OnDisable()
    {
        // イベントの購読を解除
#if UNITY_ANDROID && !UNITY_EDITOR
        if (AndroidDialogManager.Instance != null)
        {
            AndroidDialogManager.Instance.DialogResult -= OnDialogResult;
            AndroidDialogManager.Instance.ConfirmDialogResult -= OnConfirmDialogResult;
            AndroidDialogManager.Instance.SingleChoiceItemDialogResult -= OnSingleChoiceItemDialogResult;
            AndroidDialogManager.Instance.MultiChoiceItemDialogResult -= OnMultiChoiceItemDialogResult;
            AndroidDialogManager.Instance.TextInputDialogResult -= OnTextInputDialogResult;
            AndroidDialogManager.Instance.LoginDialogResult -= OnLoginDialogResult;
        }
#endif
    }

    #endregion

    #region AndroidDialogManager Event Handlers

    private void OnDialogResult(string? buttonText, bool isSuccess, string? errorMessage)
    {
        Debug.Log($"[AndroidDialogManagerExampleController] DialogResult buttonText: {buttonText ?? "null"}, isSuccess: {isSuccess}, errorMessage: {errorMessage ?? "null"}");
    }

    private void OnConfirmDialogResult(string? buttonText, bool isSuccess, string? errorMessage)
    {
        Debug.Log($"[AndroidDialogManagerExampleController] ConfirmDialogResult buttonText: {buttonText ?? "null"}, isSuccess: {isSuccess}, errorMessage: {errorMessage ?? "null"}");
    }

    private void OnSingleChoiceItemDialogResult(string? buttonText, int? checkedItem, bool isSuccess, string? errorMessage)
    {
        Debug.Log($"[AndroidDialogManagerExampleController] SingleChoiceItemDialogResult buttonText: {buttonText ?? "null"}, checkedItem: {(checkedItem.HasValue ? checkedItem.Value.ToString() : "null")}, isSuccess: {isSuccess}, errorMessage: {errorMessage ?? "null"}");
    }

    private void OnMultiChoiceItemDialogResult(string? buttonText, bool[]? checkedItems, bool isSuccess, string? errorMessage)
    {
        Debug.Log($"[AndroidDialogManagerExampleController] MultiChoiceItemDialogResult buttonText: {buttonText ?? "null"}, checkedItems: {(checkedItems != null ? string.Join(", ", checkedItems) : "null")}, isSuccess: {isSuccess}, errorMessage: {errorMessage ?? "null"}");
    }

    private void OnTextInputDialogResult(string? buttonText, string? inputText, bool isSuccess, string? errorMessage)
    {
        Debug.Log($"[AndroidDialogManagerExampleController] TextInputDialogResult buttonText: {buttonText ?? "null"}, inputText: {inputText ?? "null"}, isSuccess: {isSuccess}, errorMessage: {errorMessage ?? "null"}");
    }

    private void OnLoginDialogResult(string? buttonText, string? username, string? password, bool isSuccess, string? errorMessage)
    {
        Debug.Log($"[AndroidDialogManagerExampleController] LoginDialogResult buttonText: {buttonText ?? "null"}, username: {username ?? "null"}, password: {password ?? "null"}, isSuccess: {isSuccess}, errorMessage: {errorMessage ?? "null"}");
    }

    #endregion
}