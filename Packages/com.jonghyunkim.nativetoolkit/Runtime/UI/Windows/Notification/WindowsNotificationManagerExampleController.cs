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
    private Button?    _initializeButton;
    private Button?    _showNotificationButton;
    private Button?    _scheduleNotificationButton;
    private Button?    _showProgressNotificationButton;
    private Button?    _updateProgressButton;
    private Button?    _cancelScheduledButton;
    private Button?    _removeByTagButton;
    private Button?    _removeAllButton;
    private Button?    _getSettingButton;
    private Button?    _openSettingsButton;
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
#endif
    }

    private void OnDisable()
    {
        Debug.Log($"[{LogTag}][{nameof(OnDisable)}]");
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        WindowsNotificationManager.Instance.NotificationOperationCompleted -= OnNotificationOperationCompleted;
        WindowsNotificationManager.Instance.NotificationInvoked            -= OnNotificationInvoked;
#endif
    }

    private void OnDestroy()
    {
        Debug.Log($"[{LogTag}][{nameof(OnDestroy)}]");
        if (_homeButton != null)                _homeButton.clicked                -= OnHomeClicked;
        if (_initializeButton != null)          _initializeButton.clicked          -= OnInitializeClicked;
        if (_showNotificationButton != null)    _showNotificationButton.clicked    -= OnShowNotificationClicked;
        if (_scheduleNotificationButton != null) _scheduleNotificationButton.clicked -= OnScheduleNotificationClicked;
        if (_showProgressNotificationButton != null) _showProgressNotificationButton.clicked -= OnShowProgressNotificationClicked;
        if (_updateProgressButton != null)      _updateProgressButton.clicked      -= OnUpdateProgressClicked;
        if (_cancelScheduledButton != null)     _cancelScheduledButton.clicked     -= OnCancelScheduledClicked;
        if (_removeByTagButton != null)         _removeByTagButton.clicked         -= OnRemoveByTagClicked;
        if (_removeAllButton != null)           _removeAllButton.clicked           -= OnRemoveAllClicked;
        if (_getSettingButton != null)          _getSettingButton.clicked          -= OnGetSettingClicked;
        if (_openSettingsButton != null)        _openSettingsButton.clicked        -= OnOpenSettingsClicked;
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
        _initializeButton          = root.Q<Button>("InitializeButton");
        _showNotificationButton    = root.Q<Button>("ShowNotificationButton");
        _scheduleNotificationButton = root.Q<Button>("ScheduleNotificationButton");
        _showProgressNotificationButton = root.Q<Button>("ShowProgressNotificationButton");
        _updateProgressButton      = root.Q<Button>("UpdateProgressButton");
        _cancelScheduledButton     = root.Q<Button>("CancelScheduledButton");
        _removeByTagButton         = root.Q<Button>("RemoveByTagButton");
        _removeAllButton           = root.Q<Button>("RemoveAllButton");
        _getSettingButton          = root.Q<Button>("GetSettingButton");
        _openSettingsButton        = root.Q<Button>("OpenSettingsButton");

        if (_homeButton != null)                _homeButton.clicked                += OnHomeClicked;
        if (_initializeButton != null)          _initializeButton.clicked          += OnInitializeClicked;
        if (_showNotificationButton != null)    _showNotificationButton.clicked    += OnShowNotificationClicked;
        if (_scheduleNotificationButton != null) _scheduleNotificationButton.clicked += OnScheduleNotificationClicked;
        if (_showProgressNotificationButton != null) _showProgressNotificationButton.clicked += OnShowProgressNotificationClicked;
        if (_updateProgressButton != null)      _updateProgressButton.clicked      += OnUpdateProgressClicked;
        if (_cancelScheduledButton != null)     _cancelScheduledButton.clicked     += OnCancelScheduledClicked;
        if (_removeByTagButton != null)         _removeByTagButton.clicked         += OnRemoveByTagClicked;
        if (_removeAllButton != null)           _removeAllButton.clicked           += OnRemoveAllClicked;
        if (_getSettingButton != null)          _getSettingButton.clicked          += OnGetSettingClicked;
        if (_openSettingsButton != null)        _openSettingsButton.clicked        += OnOpenSettingsClicked;
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
        string iconPath = System.IO.Path.Combine(Application.streamingAssetsPath, "app-icon.png");
        string iconUri = new Uri(iconPath).AbsoluteUri;
        WindowsNotificationManager.Instance.Initialize(false, Application.productName, iconUri, result =>
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
        var payload = new WindowsNotificationPayload
        {
            Title   = "Guild Battle Starts Soon",
            Body    = "Battle queue opens in 1 minute. Finalize your loadout and deploy.",
            Tag     = SampleNotificationTag,
            Group   = SampleNotificationGroup,
            Buttons = new List<WindowsNotificationButtonPayload>
            {
                new() { Label = "Open", Args = new Dictionary<string, string> { ["action"] = "open" } }
            }
        };
        var json = WindowsNotificationJsonBuilder.BuildNotificationPayload(payload);
        long scheduledTimeUnixMs = DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeMilliseconds();
        WindowsNotificationManager.Instance.ScheduleNotification(json, scheduledTimeUnixMs, result =>
        {
            SetResult(FormatResult("ScheduleNotification (+1m)", result));
        });
#else
        SetResult("Windows Standalone only. Run this sample on Windows to verify.");
#endif
    }

    private void OnShowProgressNotificationClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnShowProgressNotificationClicked)}]");
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        _sequenceNumber = 0;
        var payload = new WindowsNotificationPayload
        {
            Title    = "Downloading...",
            Tag      = SampleNotificationTag,
            Group    = SampleNotificationGroup,
            Progress = new WindowsNotificationProgressPayload { Value = 0.3, ValueStr = "30%", Status = "Downloading" }
        };
        var json = WindowsNotificationJsonBuilder.BuildNotificationPayload(payload);
        WindowsNotificationManager.Instance.ShowNotification(json, result =>
        {
            SetResult(FormatResult("ShowProgressNotification", result));
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
