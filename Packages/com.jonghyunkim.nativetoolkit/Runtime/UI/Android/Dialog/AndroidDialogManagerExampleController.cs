#nullable enable

#if UNITY_ANDROID || UNITY_EDITOR
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;
using System.Linq;

/// <summary>
/// Controller for the Android native dialog manager example UI.
/// Provides functionality to demonstrate various types of Android native dialogs including
/// basic dialogs, confirmation dialogs, single/multi-choice dialogs, text input, and login dialogs.
/// Also handles responsive UI layout and safe area adaptation for Android devices.
/// </summary>
public class AndroidDialogManagerExampleController : MonoBehaviour
{
    [SerializeField] private UIDocument? uiDocument;

    // UI element references
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

    // Screen orientation monitoring variables
    private ScreenOrientation lastOrientation;
    private Vector2 lastScreenSize;
    private float orientationCheckInterval = 0.3f;
    private float lastOrientationCheckTime;

    /// <summary>
    /// Initialize component and platform-specific behavior on Awake.
    /// Shows a simulation dialog in Editor mode and validates platform compatibility.
    /// </summary>
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

    /// <summary>
    /// Initialize UI elements, safe area configuration, and responsive layout on Start.
    /// Sets up event handlers and initial screen state monitoring.
    /// </summary>
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

        // Set initial values
        lastOrientation = Screen.orientation;
        lastScreenSize = new Vector2(Screen.width, Screen.height);

        InitializeUI();
        ApplyAndroidSafeArea();
        ApplyResponsiveClasses();

        Debug.Log("[AndroidDialogManagerExampleController] Initialized successfully");
    }

    /// <summary>
    /// Monitor screen orientation and size changes for responsive layout updates.
    /// Checks periodically to avoid excessive Update calls.
    /// </summary>
    /// <summary>
    /// Monitor screen orientation and size changes for responsive layout updates.
    /// Checks periodically to avoid excessive Update calls.
    /// </summary>
    private void Update()
    {
        // Efficient screen change detection
        if (Time.time - lastOrientationCheckTime > orientationCheckInterval)
        {
            lastOrientationCheckTime = Time.time;
            CheckForOrientationChange();
        }
    }

    /// <summary>
    /// Check for screen orientation or size changes and trigger layout updates if needed.
    /// Compares current screen state with cached values to detect changes.
    /// </summary>
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

    /// <summary>
    /// Coroutine to update UI layout after screen orientation or size change.
    /// Waits for frames to complete before applying responsive classes.
    /// </summary>
    /// <returns>Coroutine enumerator</returns>
    private IEnumerator UpdateLayoutAfterChange()
    {
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();

        ApplyResponsiveClasses();
        Debug.Log("[AndroidDialogManagerExampleController] Layout updated after screen change");
    }

    /// <summary>
    /// Initialize UI elements and wire up button event handlers.
    /// Sets up references to UI components and registers event listeners for native dialog actions.
    /// Also registers AndroidDialogManager event handlers on Android platform.
    /// </summary>
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

    /// <summary>
    /// Clean up event handlers when the component is destroyed.
    /// Unregisters all button click events and AndroidDialogManager event handlers to prevent memory leaks.
    /// </summary>
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

    /// <summary>
    /// Handles the basic dialog button click event.
    /// Shows a simple Android native dialog with a single OK button.
    /// </summary>
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

    /// <summary>
    /// Handles the confirmation dialog button click event.
    /// Shows an Android native dialog with Yes/No buttons for user confirmation.
    /// </summary>
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

    /// <summary>
    /// Handles the single choice dialog button click event.
    /// Shows an Android native dialog with radio button options for single selection.
    /// </summary>
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

    /// <summary>
    /// Handles the multi-choice dialog button click event.
    /// Shows an Android native dialog with checkboxes for multiple item selection.
    /// </summary>
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

    /// <summary>
    /// Handles the text input dialog button click event.
    /// Shows an Android native dialog with a text input field for user text entry.
    /// </summary>
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

    /// <summary>
    /// Handles the login dialog button click event.
    /// Shows an Android native dialog with username and password input fields for user authentication.
    /// </summary>
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

    /// <summary>
    /// Applies Android-specific safe area configuration to the UI.
    /// Initiates a coroutine to handle safe area padding after UI is fully initialized.
    /// </summary>
    private void ApplyAndroidSafeArea()
    {
        StartCoroutine(ApplySafeAreaCoroutine());
    }

    /// <summary>
    /// Coroutine to apply safe area padding to the root UI element.
    /// Waits for UI layout completion before applying manual or automatic safe area values.
    /// </summary>
    /// <returns>Coroutine enumerator</returns>
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

    /// <summary>
    /// Applies responsive CSS classes to the UI based on screen size and DPI.
    /// Calculates DPI-adjusted screen dimensions and applies appropriate size and density classes.
    /// </summary>
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

        // Remove existing classes
        root.RemoveFromClassList("small-screen");
        root.RemoveFromClassList("medium-screen");
        root.RemoveFromClassList("large-screen");
        root.RemoveFromClassList("low-dpi");
        root.RemoveFromClassList("medium-dpi");
        root.RemoveFromClassList("high-dpi");

        // Default value for invalid DPI
        if (dpi <= 0 || float.IsNaN(dpi))
        {
            dpi = 160f; // Android standard DPI
        }

        // Calculate DPI adjustment factor
        float dpiScale = dpi / 160f; // Use 160dpi as reference (1.0)
        float adjustedWidth = screenWidth / dpiScale; // DPI-normalized width

        // Add screen size classes (DPI-adjusted)
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

        // Add DPI classes
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

    /// <summary>
    /// Handles the result from a basic Android dialog.
    /// Updates the result label with button text, success status, and any error message.
    /// </summary>
    /// <param name="buttonText">Text of the button that was pressed</param>
    /// <param name="isSuccess">Whether the dialog operation succeeded</param>
    /// <param name="errorMessage">Error message if the operation failed</param>
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

    /// <summary>
    /// Handles the result from an Android confirmation dialog.
    /// Updates the result label with button selection, success status, and any error message.
    /// </summary>
    /// <param name="buttonText">Text of the button that was pressed (positive/negative)</param>
    /// <param name="isSuccess">Whether the dialog operation succeeded</param>
    /// <param name="errorMessage">Error message if the operation failed</param>
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

    /// <summary>
    /// Handles the result from an Android single choice dialog.
    /// Updates the result label with the selected item index, button text, and operation status.
    /// </summary>
    /// <param name="buttonText">Text of the button that was pressed</param>
    /// <param name="checkedItem">Index of the selected item (null if cancelled)</param>
    /// <param name="isSuccess">Whether the dialog operation succeeded</param>
    /// <param name="errorMessage">Error message if the operation failed</param>
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

    /// <summary>
    /// Handles the result from an Android multi-choice dialog.
    /// Updates the result label with the selection state of all items, button text, and operation status.
    /// </summary>
    /// <param name="buttonText">Text of the button that was pressed</param>
    /// <param name="checkedItems">Array of boolean values indicating which items were selected</param>
    /// <param name="isSuccess">Whether the dialog operation succeeded</param>
    /// <param name="errorMessage">Error message if the operation failed</param>
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

    /// <summary>
    /// Handles the result from an Android text input dialog.
    /// Updates the result label with the entered text, button selection, and operation status.
    /// </summary>
    /// <param name="buttonText">Text of the button that was pressed</param>
    /// <param name="inputText">Text entered by the user (null if cancelled)</param>
    /// <param name="isSuccess">Whether the dialog operation succeeded</param>
    /// <param name="errorMessage">Error message if the operation failed</param>
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

    /// <summary>
    /// Handles the result from an Android login dialog.
    /// Updates the result label with the entered credentials, button selection, and operation status.
    /// </summary>
    /// <param name="buttonText">Text of the button that was pressed</param>
    /// <param name="username">Username entered by the user (null if cancelled)</param>
    /// <param name="password">Password entered by the user (null if cancelled)</param>
    /// <param name="isSuccess">Whether the dialog operation succeeded</param>
    /// <param name="errorMessage">Error message if the operation failed</param>
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