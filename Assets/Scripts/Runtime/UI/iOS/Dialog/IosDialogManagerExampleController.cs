#nullable enable

using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

[System.Serializable]
public class IosDialogButtonData
{
    public string? text;
    public string? action;
    public string? buttonId;
}

public class IosDialogManagerExampleController : MonoBehaviour
{
    [SerializeField] private UIDocument? uiDocument;

    // ListView用のデータ - iOS用のダイアログタイプ
    private ListView? dialogListView;
    private readonly List<IosDialogButtonData> dialogButtons = new()
    {
        new IosDialogButtonData { text = "ShowDialog", action = "ShowDialog", buttonId = "ShowDialog" },
        new IosDialogButtonData { text = "ShowConfirmDialog", action = "ShowConfirmDialog", buttonId = "ShowConfirmDialog" },
        new IosDialogButtonData { text = "ShowDestructiveDialog", action = "ShowDestructiveDialog", buttonId = "ShowDestructiveDialog" },
        new IosDialogButtonData { text = "ShowActionSheet", action = "ShowActionSheet", buttonId = "ShowActionSheet" },
        new IosDialogButtonData { text = "ShowTextInputDialog", action = "ShowTextInputDialog", buttonId = "ShowTextInputDialog" },
        new IosDialogButtonData { text = "ShowLoginDialog", action = "ShowLoginDialog", buttonId = "ShowLoginDialog" }
    };

    [Header("Manual Safe Area Override (for testing)")]
    [SerializeField] private bool useManualSafeArea = false;
    [SerializeField] private int manualTopPadding = 44;  // iOS Status Bar
    [SerializeField] private int manualBottomPadding = 34; // iOS Home Indicator
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
        Debug.Log("Running in Unity Editor - iOS simulation mode");
#elif UNITY_IOS
    Debug.Log("Running on iOS device");
#else
    Debug.LogWarning("IosDialogManagerExampleController is only supported on iOS platform or Editor.");
    gameObject.SetActive(false);
    return;
#endif
        Debug.Log("IosDialogManagerExampleController initialized successfully.");
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
        dialogListView = root.Q<ListView>("DialogListView");

        if (dialogListView == null)
        {
            Debug.LogError("[IosDialogManagerExampleController] DialogListView not found!");
            return;
        }

        SetupListView();
        Debug.Log("[IosDialogManagerExampleController] ListView initialized with " + dialogButtons.Count + " items");
    }

    private void SetupListView()
    {
        if (dialogListView == null)
        {
            Debug.LogError("[IosDialogManagerExampleController] dialogListView is null!");
            return;
        }
        // ListViewのデータソースを設定
        dialogListView.itemsSource = dialogButtons;

        // アイテム作成関数（Labelを作成）
        dialogListView.makeItem = () =>
        {
            var label = new Label();
            label.AddToClassList("ios-dialog-list-item");
            return label;
        };

        // アイテムバインド関数
        dialogListView.bindItem = (element, index) =>
        {
            var buttonData = dialogButtons[index];

            if (element is not Label label || buttonData == null)
            {
                Debug.LogError($"[IosDialogManagerExampleController] Invalid item at index {index}");
                return;
            }
            label.text = buttonData.text;
            label.name = buttonData.buttonId;

            // 既存のアイテム別クラスを削除
            label.RemoveFromClassList("show-dialog-item");
            label.RemoveFromClassList("show-confirm-dialog-item");
            label.RemoveFromClassList("show-destructive-dialog-item");
            label.RemoveFromClassList("show-action-sheet-item");
            label.RemoveFromClassList("show-text-input-dialog-item");
            label.RemoveFromClassList("show-login-dialog-item");

            // アイテム別のクラスを追加（色分け用）
            string itemClass = GetItemClassFromButtonId(buttonData.buttonId ?? string.Empty);
            label.AddToClassList(itemClass);
        };

        // アイテム解放関数
        dialogListView.unbindItem = (element, index) =>
        {
            var label = element as Label;
            // 必要に応じてクリーンアップ
        };

        // 選択変更イベントを登録
        dialogListView.selectionChanged += OnListViewSelectionChanged;
        dialogListView.Rebuild();
    }

    private string GetItemClassFromButtonId(string buttonId)
    {
        switch (buttonId)
        {
            case "ShowDialog":
                return "show-dialog-item";
            case "ShowConfirmDialog":
                return "show-confirm-dialog-item";
            case "ShowDestructiveDialog":
                return "show-destructive-dialog-item";
            case "ShowActionSheet":
                return "show-action-sheet-item";
            case "ShowTextInputDialog":
                return "show-text-input-dialog-item";
            case "ShowLoginDialog":
                return "show-login-dialog-item";
            default:
                return "ios-dialog-list-item";
        }
    }

    private void OnListViewSelectionChanged(IEnumerable<object> selectedItems)
    {
        foreach (IosDialogButtonData item in selectedItems.Cast<IosDialogButtonData>())
        {
            Debug.Log($"[IosDialogManagerExampleController] ListView item selected: {item.text}");
            if (!string.IsNullOrEmpty(item.action))
            {
                OnDialogButtonClicked(item.action);
            }
            else
            {
                Debug.LogError("[IosDialogManagerExampleController] Button action is null or empty.");
            }

            // 選択をクリア（連続クリックを可能にする）
            dialogListView?.ClearSelection();
            break;
        }
    }

    private void OnDialogButtonClicked(string action)
    {
        Debug.Log($"[IosDialogManagerExampleController] Dialog action triggered: {action}");

        switch (action)
        {
            case "ShowDialog":
                OnShowDialogClicked();
                break;
            case "ShowConfirmDialog":
                OnShowConfirmDialogClicked();
                break;
            case "ShowDestructiveDialog":
                OnShowDestructiveDialogClicked();
                break;
            case "ShowActionSheet":
                OnShowActionSheetClicked();
                break;
            case "ShowTextInputDialog":
                OnShowTextInputDialogClicked();
                break;
            case "ShowLoginDialog":
                OnShowLoginDialogClicked();
                break;
            default:
                Debug.LogWarning($"[IosDialogManagerExampleController] Unknown action: {action}");
                break;
        }
    }

    #region Dialog Event Handlers

    private void OnShowDialogClicked()
    {
        Debug.Log("[IosDialogManagerExampleController] Show Dialog triggered");

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
        Debug.Log("[IosDialogManagerExampleController] ShowDialog - Running in Editor mode");
        ShowEditorDialog("Hello from Unity", "This is a native iOS dialog!");
#endif
    }

    private void OnShowConfirmDialogClicked()
    {
        Debug.Log("[IosDialogManagerExampleController] Show Confirm Dialog triggered");

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
        Debug.Log("[IosDialogManagerExampleController] ShowConfirmDialog - Running in Editor mode");
        ShowEditorConfirmDialog("Confirm Action", "Are you sure you want to proceed?");
#endif
    }

    private void OnShowDestructiveDialogClicked()
    {
        Debug.Log("[IosDialogManagerExampleController] Show Destructive Dialog triggered");

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
        Debug.Log("[IosDialogManagerExampleController] ShowDestructiveDialog - Running in Editor mode");
        ShowEditorDestructiveDialog("Delete File", "This action cannot be undone. Are you sure?");
#endif
    }

    private void OnShowActionSheetClicked()
    {
        Debug.Log("[IosDialogManagerExampleController] Show Action Sheet triggered");

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
        Debug.Log("[IosDialogManagerExampleController] ShowActionSheet - Running in Editor mode");
        string[] options = { "Camera", "Photo Library", "Documents" };
        ShowEditorActionSheet("Select Source", "Choose where to get the file from", options);
#endif
    }

    private void OnShowTextInputDialogClicked()
    {
        Debug.Log("[IosDialogManagerExampleController] Show Text Input Dialog triggered");

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
        Debug.Log("[IosDialogManagerExampleController] ShowTextInputDialog - Running in Editor mode");
        ShowEditorTextInputDialog("Enter Name", "Please enter your name");
#endif
    }

    private void OnShowLoginDialogClicked()
    {
        Debug.Log("[IosDialogManagerExampleController] Show Login Dialog triggered");

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
        Debug.Log("[IosDialogManagerExampleController] ShowLoginDialog - Running in Editor mode");
        ShowEditorLoginDialog("Login Required", "Please enter your credentials");
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

#if UNITY_IOS && !UNITY_EDITOR
        
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
                
                topPadding = Mathf.Clamp(topInset, 44, 88);  // iOS Status Bar
                bottomPadding = Mathf.Clamp(bottomInset, 34, 60); // iOS Home Indicator
                leftPadding = Mathf.Clamp(leftInset, 20, 30);
                rightPadding = Mathf.Clamp(rightInset, 20, 30);
            }
            else
            {
                topPadding = 44;  // デフォルトStatus Bar高さ
                bottomPadding = 34; // デフォルトHome Indicator高さ
                leftPadding = 20;
                rightPadding = 20;
            }
            
            Debug.Log($"[IosDialogManagerExampleController] Auto-calculated Safe Area - Top: {topPadding}, Bottom: {bottomPadding}, Left: {leftPadding}, Right: {rightPadding}");
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
        root.style.paddingTop = 44;  // iOS Status Bar
        root.style.paddingBottom = 34; // iOS Home Indicator
        root.style.paddingLeft = 20;
        root.style.paddingRight = 20;
        Debug.Log("[IosDialogManagerExampleController] Applied default safe area values for editor");
#endif

        root.AddToClassList("ios-safe-area-container");
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

    #region Editor Mode Fallback Methods

    private void ShowEditorDialog(string title, string message)
    {
#if UNITY_EDITOR
        if (UnityEditor.EditorUtility.DisplayDialog(title, message, "OK"))
        {
            Debug.Log("[IosDialogManagerExampleController] Editor Dialog - OK clicked");
        }
#else
        Debug.Log($"[IosDialogManagerExampleController] ShowEditorDialog called: {title} - {message}");
#endif
    }

    private void ShowEditorConfirmDialog(string title, string message)
    {
#if UNITY_EDITOR
        if (UnityEditor.EditorUtility.DisplayDialog(title, message, "Yes", "No"))
        {
            Debug.Log("[IosDialogManagerExampleController] Editor Confirm Dialog - Yes clicked");
        }
        else
        {
            Debug.Log("[IosDialogManagerExampleController] Editor Confirm Dialog - No clicked");
        }
#else
        Debug.Log($"[IosDialogManagerExampleController] ShowEditorConfirmDialog called: {title} - {message}");
#endif
    }

    private void ShowEditorDestructiveDialog(string title, string message)
    {
#if UNITY_EDITOR
        if (UnityEditor.EditorUtility.DisplayDialog(title, message, "Delete", "Cancel"))
        {
            Debug.Log("[IosDialogManagerExampleController] Editor Destructive Dialog - Delete clicked");
        }
        else
        {
            Debug.Log("[IosDialogManagerExampleController] Editor Destructive Dialog - Cancel clicked");
        }
#else
        Debug.Log($"[IosDialogManagerExampleController] ShowEditorDestructiveDialog called: {title} - {message}");
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
        Debug.Log($"[IosDialogManagerExampleController] Editor Action Sheet - Selected: {choice}");
#else
        Debug.Log($"[IosDialogManagerExampleController] ShowEditorActionSheet called: {title} - {message} - Options: {string.Join(", ", options)}");
#endif
    }

    private void ShowEditorTextInputDialog(string title, string message)
    {
#if UNITY_EDITOR
        if (UnityEditor.EditorUtility.DisplayDialog(title, $"{message}\n\n(Sample Text will be entered in editor mode)", "OK", "Cancel"))
        {
            Debug.Log("[IosDialogManagerExampleController] Editor Text Input - Sample text entered");
        }
        else
        {
            Debug.Log("[IosDialogManagerExampleController] Editor Text Input - Cancelled");
        }
#else
        Debug.Log($"[IosDialogManagerExampleController] ShowEditorTextInputDialog called: {title} - {message}");
#endif
    }

    private void ShowEditorLoginDialog(string title, string message)
    {
#if UNITY_EDITOR
        if (UnityEditor.EditorUtility.DisplayDialog(title, $"{message}\n\n(admin/password will be used in editor mode)", "Login", "Cancel"))
        {
            Debug.Log("[IosDialogManagerExampleController] Editor Login - Sample credentials used");
        }
        else
        {
            Debug.Log("[IosDialogManagerExampleController] Editor Login - Cancelled");
        }
#else
        Debug.Log($"[IosDialogManagerExampleController] ShowEditorLoginDialog called: {title} - {message}");
#endif
    }

    #endregion

    #region Event Subscription for IosDialogManager

    private void OnEnable()
    {
        // IosDialogManagerのイベントを購読
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

    #region IosDialogManager Event Handlers

    private void OnDialogResult(string? result, bool isSuccess, string? errorMessage)
    {
        Debug.Log($"[IosDialogManagerExampleController] DialogResult result: {result ?? "null"}, isSuccess: {isSuccess}, errorMessage: {errorMessage ?? "null"}");
    }

    private void OnConfirmDialogResult(string? result, bool isSuccess, string? errorMessage)
    {
        Debug.Log($"[IosDialogManagerExampleController] ConfirmDialogResult result: {result ?? "null"}, isSuccess: {isSuccess}, errorMessage: {errorMessage ?? "null"}");
    }

    private void OnDestructiveDialogResult(string? result, bool isSuccess, string? errorMessage)
    {
        Debug.Log($"[IosDialogManagerExampleController] DestructiveDialogResult result: {result ?? "null"}, isSuccess: {isSuccess}, errorMessage: {errorMessage ?? "null"}");
    }

    private void OnActionSheetResult(string? result, bool isSuccess, string? errorMessage)
    {
        Debug.Log($"[IosDialogManagerExampleController] ActionSheetResult result: {result ?? "null"}, isSuccess: {isSuccess}, errorMessage: {errorMessage ?? "null"}");
    }

    private void OnTextInputDialogResult(string? buttonPressed, string? inputText, bool isSuccess, string? errorMessage)
    {
        Debug.Log($"[IosDialogManagerExampleController] TextInputDialogResult buttonPressed: {buttonPressed ?? "null"}, inputText: {inputText ?? "null"}, isSuccess: {isSuccess}, errorMessage: {errorMessage ?? "null"}");
    }

    private void OnLoginDialogResult(string? buttonPressed, string? username, string? password, bool isSuccess, string? errorMessage)
    {
        Debug.Log($"[IosDialogManagerExampleController] LoginDialogResult buttonPressed: {buttonPressed ?? "null"}, username: {username ?? "null"}, password: {password ?? "null"}, isSuccess: {isSuccess}, errorMessage: {errorMessage ?? "null"}");
    }

    #endregion
}
