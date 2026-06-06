#nullable enable

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
using System;
using System.Collections.Generic;
using JonghyunKim.NativeToolkit.Runtime.Notification;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Example controller demonstrating Windows notification features via <see cref="WindowsNotificationManager"/>.
/// </summary>
public class WindowsNotificationManagerExampleController : MonoBehaviour
{
    private const string LogTag = "WindowsNotificationManagerExampleController";

    [SerializeField] private UIDocument? uiDocument;

    private const string SampleNotificationTag   = "win-sample-notification";
    private const string SampleNotificationGroup = "win-sample-group";

    private uint _sequenceNumber;

    private Label?     _resultLabel;
    private Button?    _homeButton;
    private Toggle?    _isPackagedToggle;
    private TextField? _clsidField;
    private TextField? _launchUriField;
    private Button?    _initializeButton;
    private Button?    _showNotificationButton;
    private Button?    _scheduleNotificationButton;
    private Button?    _updateProgressButton;
    private Button?    _cancelScheduledButton;
    private Button?    _removeByTagButton;
    private Button?    _removeByIdButton;
    private Button?    _removeAllButton;
    private Button?    _getAllButton;
    private Button?    _getSettingButton;
    private Button?    _openSettingsButton;
    private Button?    _setBadgeAlertButton;
    private Button?    _setBadgeNewMessageButton;
    private Button?    _setBadge1Button;
    private Button?    _clearBadgeButton;

    private void Awake()
    {
        Debug.Log($"[{LogTag}][{nameof(Awake)}]");
#if UNITY_EDITOR
        UnityEditor.EditorUtility.DisplayDialog(
            "WindowsNotificationManager Example",
            "This is a simulation of the Windows notification manager.\nAll events will not be triggered.\nRun in Windows player for full functionality.",
            "OK");
#elif UNITY_STANDALONE_WIN
        Debug.Log($"[{LogTag}][{nameof(Awake)}] Running on Windows player.");
#endif
    }

    private void Start()
    {
        Debug.Log($"[{LogTag}][{nameof(Start)}]");
        if (uiDocument == null)
        {
            uiDocument = GetComponent<UIDocument>();
        }

        if (uiDocument == null)
        {
            Debug.LogError($"[{LogTag}][{nameof(Start)}] UIDocument component not found.");
            return;
        }

        InitializeUI();
    }

    private void OnEnable()
    {
        Debug.Log($"[{LogTag}][{nameof(OnEnable)}]");
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        WindowsNotificationManager.Instance.NotificationOperationCompleted += OnNotificationOperationCompleted;
        WindowsNotificationManager.Instance.NotificationInvoked            += OnNotificationInvoked;
        WindowsNotificationManager.Instance.GetAllNotificationsCompleted   += OnGetAllNotificationsCompleted;
#endif
    }

    private void OnDisable()
    {
        Debug.Log($"[{LogTag}][{nameof(OnDisable)}]");
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        WindowsNotificationManager.Instance.NotificationOperationCompleted -= OnNotificationOperationCompleted;
        WindowsNotificationManager.Instance.NotificationInvoked            -= OnNotificationInvoked;
        WindowsNotificationManager.Instance.GetAllNotificationsCompleted   -= OnGetAllNotificationsCompleted;
#endif
    }

    private void OnDestroy()
    {
        Debug.Log($"[{LogTag}][{nameof(OnDestroy)}]");
        if (_homeButton != null)                _homeButton.clicked                -= OnHomeClicked;
        if (_initializeButton != null)          _initializeButton.clicked          -= OnInitializeClicked;
        if (_showNotificationButton != null)    _showNotificationButton.clicked    -= OnShowNotificationClicked;
        if (_scheduleNotificationButton != null) _scheduleNotificationButton.clicked -= OnScheduleNotificationClicked;
        if (_updateProgressButton != null)      _updateProgressButton.clicked      -= OnUpdateProgressClicked;
        if (_cancelScheduledButton != null)     _cancelScheduledButton.clicked     -= OnCancelScheduledClicked;
        if (_removeByTagButton != null)         _removeByTagButton.clicked         -= OnRemoveByTagClicked;
        if (_removeByIdButton != null)          _removeByIdButton.clicked          -= OnRemoveByIdClicked;
        if (_removeAllButton != null)           _removeAllButton.clicked           -= OnRemoveAllClicked;
        if (_getAllButton != null)              _getAllButton.clicked              -= OnGetAllClicked;
        if (_getSettingButton != null)          _getSettingButton.clicked          -= OnGetSettingClicked;
        if (_openSettingsButton != null)        _openSettingsButton.clicked        -= OnOpenSettingsClicked;
        if (_setBadgeAlertButton != null)       _setBadgeAlertButton.clicked       -= OnSetBadgeAlertClicked;
        if (_setBadgeNewMessageButton != null)  _setBadgeNewMessageButton.clicked  -= OnSetBadgeNewMessageClicked;
        if (_setBadge1Button != null)           _setBadge1Button.clicked           -= OnSetBadge1Clicked;
        if (_clearBadgeButton != null)          _clearBadgeButton.clicked          -= OnClearBadgeClicked;
    }

    private void InitializeUI()
    {
        Debug.Log($"[{LogTag}][{nameof(InitializeUI)}]");
        var root = uiDocument?.rootVisualElement;
        if (root == null)
        {
            Debug.LogError($"[{LogTag}][{nameof(InitializeUI)}] rootVisualElement is null.");
            return;
        }

        _resultLabel               = root.Q<Label>("ResultTextBlock");
        _homeButton                = root.Q<Button>("HomeButton");
        _isPackagedToggle          = root.Q<Toggle>("IsPackagedToggle");
        _clsidField                = root.Q<TextField>("ClsidField");
        _launchUriField            = root.Q<TextField>("LaunchUriField");
        _initializeButton          = root.Q<Button>("InitializeButton");
        _showNotificationButton    = root.Q<Button>("ShowNotificationButton");
        _scheduleNotificationButton = root.Q<Button>("ScheduleNotificationButton");
        _updateProgressButton      = root.Q<Button>("UpdateProgressButton");
        _cancelScheduledButton     = root.Q<Button>("CancelScheduledButton");
        _removeByTagButton         = root.Q<Button>("RemoveByTagButton");
        _removeByIdButton          = root.Q<Button>("RemoveByIdButton");
        _removeAllButton           = root.Q<Button>("RemoveAllButton");
        _getAllButton               = root.Q<Button>("GetAllButton");
        _getSettingButton          = root.Q<Button>("GetSettingButton");
        _openSettingsButton        = root.Q<Button>("OpenSettingsButton");
        _setBadgeAlertButton       = root.Q<Button>("SetBadgeAlertButton");
        _setBadgeNewMessageButton  = root.Q<Button>("SetBadgeNewMessageButton");
        _setBadge1Button           = root.Q<Button>("SetBadge1Button");
        _clearBadgeButton          = root.Q<Button>("ClearBadgeButton");

        if (_clsidField != null)     _clsidField.value     = "{00000000-0000-0000-0000-000000000000}";
        if (_launchUriField != null) _launchUriField.value = "myapp://";

        if (_homeButton != null)                _homeButton.clicked                += OnHomeClicked;
        if (_initializeButton != null)          _initializeButton.clicked          += OnInitializeClicked;
        if (_showNotificationButton != null)    _showNotificationButton.clicked    += OnShowNotificationClicked;
        if (_scheduleNotificationButton != null) _scheduleNotificationButton.clicked += OnScheduleNotificationClicked;
        if (_updateProgressButton != null)      _updateProgressButton.clicked      += OnUpdateProgressClicked;
        if (_cancelScheduledButton != null)     _cancelScheduledButton.clicked     += OnCancelScheduledClicked;
        if (_removeByTagButton != null)         _removeByTagButton.clicked         += OnRemoveByTagClicked;
        if (_removeByIdButton != null)          _removeByIdButton.clicked          += OnRemoveByIdClicked;
        if (_removeAllButton != null)           _removeAllButton.clicked           += OnRemoveAllClicked;
        if (_getAllButton != null)              _getAllButton.clicked              += OnGetAllClicked;
        if (_getSettingButton != null)          _getSettingButton.clicked          += OnGetSettingClicked;
        if (_openSettingsButton != null)        _openSettingsButton.clicked        += OnOpenSettingsClicked;
        if (_setBadgeAlertButton != null)       _setBadgeAlertButton.clicked       += OnSetBadgeAlertClicked;
        if (_setBadgeNewMessageButton != null)  _setBadgeNewMessageButton.clicked  += OnSetBadgeNewMessageClicked;
        if (_setBadge1Button != null)           _setBadge1Button.clicked           += OnSetBadge1Clicked;
        if (_clearBadgeButton != null)          _clearBadgeButton.clicked          += OnClearBadgeClicked;
    }

    // ── Button Handlers ──────────────────────────────────────────────────────

    private void OnHomeClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnHomeClicked)}]");
        if (uiDocument != null)
        {
            NativeToolkitSampleNavigator.ShowTopMenu(uiDocument);
        }
    }

    private void OnInitializeClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnInitializeClicked)}]");
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        bool isPackaged = _isPackagedToggle?.value ?? false;
        string? clsid     = string.IsNullOrEmpty(_clsidField?.value)    ? null : _clsidField.value;
        string? launchUri = string.IsNullOrEmpty(_launchUriField?.value) ? null : _launchUriField.value;

        if (!isPackaged && clsid == null)
        {
            SetResult("clsid is required when not packaged.");
            return;
        }

        WindowsNotificationManager.Instance.Initialize(isPackaged, clsid, launchUri, result =>
        {
            SetResult(FormatResult("Initialize", result));
        });
#else
        SetResult("Windows Standalone only. Run this sample on Windows to verify.");
#endif
    }

    private void OnShowNotificationClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnShowNotificationClicked)}]");
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        var json = WindowsNotificationJsonBuilder.BuildNotificationPayload(BuildSamplePayload());
        WindowsNotificationManager.Instance.ShowNotification(json, result =>
        {
            SetResult(FormatResult("ShowNotification", result));
        });
#else
        SetResult("Windows Standalone only. Run this sample on Windows to verify.");
#endif
    }

    private void OnScheduleNotificationClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnScheduleNotificationClicked)}]");
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        var json = WindowsNotificationJsonBuilder.BuildNotificationPayload(BuildSamplePayload());
        long scheduledTimeUnixMs = DateTimeOffset.UtcNow.AddSeconds(30).ToUnixTimeMilliseconds();
        WindowsNotificationManager.Instance.ScheduleNotification(json, scheduledTimeUnixMs, result =>
        {
            SetResult(FormatResult("ScheduleNotification (+30s)", result));
        });
#else
        SetResult("Windows Standalone only. Run this sample on Windows to verify.");
#endif
    }

    private void OnUpdateProgressClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnUpdateProgressClicked)}]");
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        _sequenceNumber++;
        WindowsNotificationManager.Instance.UpdateNotificationProgress(
            SampleNotificationTag, SampleNotificationGroup,
            0.5, "50%", "Downloading...", _sequenceNumber, result =>
            {
                SetResult(FormatResult($"UpdateProgress (seq={_sequenceNumber})", result));
            });
#else
        SetResult("Windows Standalone only. Run this sample on Windows to verify.");
#endif
    }

    private void OnCancelScheduledClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnCancelScheduledClicked)}]");
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        WindowsNotificationManager.Instance.CancelScheduledNotification(
            SampleNotificationTag, SampleNotificationGroup, result =>
            {
                SetResult(FormatResult("CancelScheduledNotification", result));
            });
#else
        SetResult("Windows Standalone only. Run this sample on Windows to verify.");
#endif
    }

    private void OnRemoveByTagClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnRemoveByTagClicked)}]");
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        WindowsNotificationManager.Instance.RemoveNotificationsByTag(
            SampleNotificationTag, SampleNotificationGroup, result =>
            {
                SetResult(FormatResult($"RemoveByTag ({SampleNotificationTag})", result));
            });
#else
        SetResult("Windows Standalone only. Run this sample on Windows to verify.");
#endif
    }

    private void OnRemoveByIdClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnRemoveByIdClicked)}]");
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        WindowsNotificationManager.Instance.RemoveNotificationById(1u, result =>
        {
            SetResult(FormatResult("RemoveById (id=1)", result));
        });
#else
        SetResult("Windows Standalone only. Run this sample on Windows to verify.");
#endif
    }

    private void OnRemoveAllClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnRemoveAllClicked)}]");
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        WindowsNotificationManager.Instance.RemoveAllNotifications(result =>
        {
            SetResult(FormatResult("RemoveAllNotifications", result));
        });
#else
        SetResult("Windows Standalone only. Run this sample on Windows to verify.");
#endif
    }

    private void OnGetAllClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnGetAllClicked)}]");
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        WindowsNotificationManager.Instance.GetAllNotifications((json, result) =>
        {
            SetResult(result.IsSuccess
                ? $"✓ GetAllNotifications:\n{json}"
                : $"✗ GetAllNotifications\nError: {result.ErrorMessage}");
        });
#else
        SetResult("Windows Standalone only. Run this sample on Windows to verify.");
#endif
    }

    private void OnGetSettingClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnGetSettingClicked)}]");
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        WindowsNotificationSetting setting = WindowsNotificationManager.Instance.GetNotificationSetting();
        SetResult($"✓ NotificationSetting: {setting}");
#else
        SetResult("Windows Standalone only. Run this sample on Windows to verify.");
#endif
    }

    private void OnOpenSettingsClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnOpenSettingsClicked)}]");
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        WindowsNotificationManager.Instance.OpenNotificationSettings(result =>
        {
            SetResult(FormatResult("OpenNotificationSettings", result));
        });
#else
        SetResult("Windows Standalone only. Run this sample on Windows to verify.");
#endif
    }

    private void OnSetBadgeAlertClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnSetBadgeAlertClicked)}]");
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        WindowsNotificationManager.Instance.SetBadge((int)WindowsBadgeValue.Alert, result =>
        {
            SetResult(FormatResult("SetBadge(Alert)", result));
        });
#else
        SetResult("Windows Standalone only. Run this sample on Windows to verify.");
#endif
    }

    private void OnSetBadgeNewMessageClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnSetBadgeNewMessageClicked)}]");
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        WindowsNotificationManager.Instance.SetBadge((int)WindowsBadgeValue.NewMessage, result =>
        {
            SetResult(FormatResult("SetBadge(NewMessage)", result));
        });
#else
        SetResult("Windows Standalone only. Run this sample on Windows to verify.");
#endif
    }

    private void OnSetBadge1Clicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnSetBadge1Clicked)}]");
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        WindowsNotificationManager.Instance.SetBadge(1, result =>
        {
            SetResult(FormatResult("SetBadge(1)", result));
        });
#else
        SetResult("Windows Standalone only. Run this sample on Windows to verify.");
#endif
    }

    private void OnClearBadgeClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnClearBadgeClicked)}]");
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        WindowsNotificationManager.Instance.SetBadge((int)WindowsBadgeValue.Clear, result =>
        {
            SetResult(FormatResult("ClearBadge", result));
        });
#else
        SetResult("Windows Standalone only. Run this sample on Windows to verify.");
#endif
    }

    // ── Event Handlers ───────────────────────────────────────────────────────

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    private void OnNotificationOperationCompleted(WindowsNotificationResult result)
    {
        Debug.Log($"[{LogTag}][{nameof(OnNotificationOperationCompleted)}] operation: {result.Operation}, isSuccess: {result.IsSuccess}");
    }

    private void OnNotificationInvoked(string argsJson)
    {
        Debug.Log($"[{LogTag}][{nameof(OnNotificationInvoked)}] argsJson: {argsJson}");
        SetResult($"NotificationInvoked: {argsJson}");
    }

    private void OnGetAllNotificationsCompleted(string? json, WindowsNotificationResult result)
    {
        Debug.Log($"[{LogTag}][{nameof(OnGetAllNotificationsCompleted)}] isSuccess: {result.IsSuccess}");
        // Handled by per-call callback in OnGetAllClicked; this global event is supplemental
    }
#endif

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static WindowsNotificationPayload BuildSamplePayload()
    {
        return new WindowsNotificationPayload
        {
            Title   = "Energy Refilled",
            Body    = "Your squad is fully rested. Jump back in and clear the next raid.",
            Tag     = SampleNotificationTag,
            Group   = SampleNotificationGroup,
            Buttons = new List<WindowsNotificationButtonPayload>
            {
                new() { Label = "Open", Args = new Dictionary<string, string> { ["action"] = "open" } }
            }
        };
    }

    private static string FormatResult(string label, WindowsNotificationResult result)
    {
        return result.IsSuccess
            ? $"✓ {label}"
            : $"✗ {label}\nError: {result.ErrorMessage ?? "unknown"}";
    }

    private void SetResult(string message)
    {
        Debug.Log($"[{LogTag}][{nameof(SetResult)}] {message}");
        if (_resultLabel != null)
        {
            _resultLabel.text = message;
        }
    }
}
#endif
