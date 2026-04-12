#nullable enable

#if UNITY_ANDROID || UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

#if UNITY_ANDROID && !UNITY_EDITOR
using JonghyunKim.NativeToolkit.Runtime.Notification;
#endif

public class AndroidNotificationManagerExampleController : MonoBehaviour
{
    [SerializeField] private UIDocument? uiDocument;

    private const string SampleChannelId = "native_toolkit_gameplay";
    private const string SampleChannelName = "Gameplay Alerts";
    private const int ImmediateNotificationId = 1101;
    private const int ScheduledNotificationId = 1201;
    private const int ProgressNotificationId = 1301;

    private Label? _resultLabel;
    private Button? _homeButton;
    private Button? _hasPermissionButton;
    private Button? _notificationsEnabledButton;
    private Button? _notificationSettingsButton;
    private Button? _appDetailsSettingsButton;
    private Button? _exactAlarmSettingsButton;
    private Button? _createChannelButton;
    private Button? _deleteChannelButton;
    private Button? _showNotificationButton;
    private Button? _updateNotificationButton;
    private Button? _cancelNotificationButton;
    private Button? _cancelAllNotificationsButton;
    private Button? _scheduleNotificationButton;
    private Button? _cancelScheduledNotificationButton;
    private Button? _cancelAllScheduledNotificationsButton;
    private Button? _startProgressButton;
    private Button? _updateProgressButton;
    private Button? _completeProgressButton;
    private Button? _stopProgressButton;

    private int _currentProgressValue = 15;
    private readonly Dictionary<string, string> _pendingOperationDescriptions = new Dictionary<string, string>();

    private void Awake()
    {
#if UNITY_EDITOR
        Debug.Log("Running in Unity Editor - Android notification sample is disabled");
        UnityEditor.EditorUtility.DisplayDialog(
            "AndroidNotificationManager Example",
            "This sample is intended for Android devices only.\nRun on an Android player to verify notification features.",
            "OK");
#elif UNITY_ANDROID
        Debug.Log("Running on Android device");
#else
        Debug.LogWarning("AndroidNotificationManagerExampleController is only supported on Android platform or Editor.");
        gameObject.SetActive(false);
        return;
#endif

        Debug.Log("AndroidNotificationManagerExampleController initialized successfully.");
    }

    private void Start()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidNotificationManager.Instance.NotificationOperationCompleted += OnNotificationOperationCompleted;
#endif

        if (uiDocument == null)
        {
            uiDocument = GetComponent<UIDocument>();
        }

        if (uiDocument == null)
        {
            Debug.LogError("[AndroidNotificationManagerExampleController] UIDocument component not found.");
            return;
        }

        InitializeUI();
    }

    private void OnDestroy()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidNotificationManager.Instance.NotificationOperationCompleted -= OnNotificationOperationCompleted;
#endif

        if (_homeButton != null) _homeButton.clicked -= OnHomeClicked;
        if (_hasPermissionButton != null) _hasPermissionButton.clicked -= OnHasPermissionClicked;
        if (_notificationsEnabledButton != null) _notificationsEnabledButton.clicked -= OnNotificationsEnabledClicked;
        if (_notificationSettingsButton != null) _notificationSettingsButton.clicked -= OnNotificationSettingsClicked;
        if (_appDetailsSettingsButton != null) _appDetailsSettingsButton.clicked -= OnAppDetailsSettingsClicked;
        if (_exactAlarmSettingsButton != null) _exactAlarmSettingsButton.clicked -= OnExactAlarmSettingsClicked;
        if (_createChannelButton != null) _createChannelButton.clicked -= OnCreateChannelClicked;
        if (_deleteChannelButton != null) _deleteChannelButton.clicked -= OnDeleteChannelClicked;
        if (_showNotificationButton != null) _showNotificationButton.clicked -= OnShowNotificationClicked;
        if (_updateNotificationButton != null) _updateNotificationButton.clicked -= OnUpdateNotificationClicked;
        if (_cancelNotificationButton != null) _cancelNotificationButton.clicked -= OnCancelNotificationClicked;
        if (_cancelAllNotificationsButton != null) _cancelAllNotificationsButton.clicked -= OnCancelAllNotificationsClicked;
        if (_scheduleNotificationButton != null) _scheduleNotificationButton.clicked -= OnScheduleNotificationClicked;
        if (_cancelScheduledNotificationButton != null) _cancelScheduledNotificationButton.clicked -= OnCancelScheduledNotificationClicked;
        if (_cancelAllScheduledNotificationsButton != null) _cancelAllScheduledNotificationsButton.clicked -= OnCancelAllScheduledNotificationsClicked;
        if (_startProgressButton != null) _startProgressButton.clicked -= OnStartProgressClicked;
        if (_updateProgressButton != null) _updateProgressButton.clicked -= OnUpdateProgressClicked;
        if (_completeProgressButton != null) _completeProgressButton.clicked -= OnCompleteProgressClicked;
        if (_stopProgressButton != null) _stopProgressButton.clicked -= OnStopProgressClicked;
    }

    private void InitializeUI()
    {
        var root = uiDocument?.rootVisualElement;
        if (root == null)
        {
            Debug.LogError("[AndroidNotificationManagerExampleController] rootVisualElement is null.");
            return;
        }

        _resultLabel = root.Q<Label>("ResultTextBlock");
        _homeButton = root.Q<Button>("HomeButton");
        _hasPermissionButton = root.Q<Button>("HasPermissionButton");
        _notificationsEnabledButton = root.Q<Button>("NotificationsEnabledButton");
        _notificationSettingsButton = root.Q<Button>("NotificationSettingsButton");
        _appDetailsSettingsButton = root.Q<Button>("AppDetailsSettingsButton");
        _exactAlarmSettingsButton = root.Q<Button>("ExactAlarmSettingsButton");
        _createChannelButton = root.Q<Button>("CreateChannelButton");
        _deleteChannelButton = root.Q<Button>("DeleteChannelButton");
        _showNotificationButton = root.Q<Button>("ShowNotificationButton");
        _updateNotificationButton = root.Q<Button>("UpdateNotificationButton");
        _cancelNotificationButton = root.Q<Button>("CancelNotificationButton");
        _cancelAllNotificationsButton = root.Q<Button>("CancelAllNotificationsButton");
        _scheduleNotificationButton = root.Q<Button>("ScheduleNotificationButton");
        _cancelScheduledNotificationButton = root.Q<Button>("CancelScheduledNotificationButton");
        _cancelAllScheduledNotificationsButton = root.Q<Button>("CancelAllScheduledNotificationsButton");
        _startProgressButton = root.Q<Button>("StartProgressButton");
        _updateProgressButton = root.Q<Button>("UpdateProgressButton");
        _completeProgressButton = root.Q<Button>("CompleteProgressButton");
        _stopProgressButton = root.Q<Button>("StopProgressButton");

        if (_homeButton != null) _homeButton.clicked += OnHomeClicked;
        if (_hasPermissionButton != null) _hasPermissionButton.clicked += OnHasPermissionClicked;
        if (_notificationsEnabledButton != null) _notificationsEnabledButton.clicked += OnNotificationsEnabledClicked;
        if (_notificationSettingsButton != null) _notificationSettingsButton.clicked += OnNotificationSettingsClicked;
        if (_appDetailsSettingsButton != null) _appDetailsSettingsButton.clicked += OnAppDetailsSettingsClicked;
        if (_exactAlarmSettingsButton != null) _exactAlarmSettingsButton.clicked += OnExactAlarmSettingsClicked;
        if (_createChannelButton != null) _createChannelButton.clicked += OnCreateChannelClicked;
        if (_deleteChannelButton != null) _deleteChannelButton.clicked += OnDeleteChannelClicked;
        if (_showNotificationButton != null) _showNotificationButton.clicked += OnShowNotificationClicked;
        if (_updateNotificationButton != null) _updateNotificationButton.clicked += OnUpdateNotificationClicked;
        if (_cancelNotificationButton != null) _cancelNotificationButton.clicked += OnCancelNotificationClicked;
        if (_cancelAllNotificationsButton != null) _cancelAllNotificationsButton.clicked += OnCancelAllNotificationsClicked;
        if (_scheduleNotificationButton != null) _scheduleNotificationButton.clicked += OnScheduleNotificationClicked;
        if (_cancelScheduledNotificationButton != null) _cancelScheduledNotificationButton.clicked += OnCancelScheduledNotificationClicked;
        if (_cancelAllScheduledNotificationsButton != null) _cancelAllScheduledNotificationsButton.clicked += OnCancelAllScheduledNotificationsClicked;
        if (_startProgressButton != null) _startProgressButton.clicked += OnStartProgressClicked;
        if (_updateProgressButton != null) _updateProgressButton.clicked += OnUpdateProgressClicked;
        if (_completeProgressButton != null) _completeProgressButton.clicked += OnCompleteProgressClicked;
        if (_stopProgressButton != null) _stopProgressButton.clicked += OnStopProgressClicked;

        SetResult("Android notification sample ready. Create a gameplay channel first, then test immediate, scheduled, and progress notifications on an Android device.");
    }

    private void OnHomeClicked()
    {
        if (uiDocument != null)
        {
            NativeToolkitSampleNavigator.ShowTopMenu(uiDocument);
        }
    }

    private void OnHasPermissionClicked()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        SetResult($"HasPermission: {AndroidNotificationManager.Instance.HasPermission()}");
#else
        SetResult("Android device only. Run this sample on Android to verify notification permission.");
#endif
    }

    private void OnNotificationsEnabledClicked()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        SetResult($"AreNotificationsEnabled: {AndroidNotificationManager.Instance.AreNotificationsEnabled()}");
#else
        SetResult("Android device only. Run this sample on Android to verify notification settings.");
#endif
    }

    private void OnNotificationSettingsClicked()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        RegisterRequestedOperation(AndroidNotificationManager.OperationOpenNotificationSettings, "Opening notification settings.");
        AndroidNotificationManager.Instance.OpenNotificationSettings();
#else
        SetResult("Android device only. Run this sample on Android to open notification settings.");
#endif
    }

    private void OnAppDetailsSettingsClicked()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        RegisterRequestedOperation(AndroidNotificationManager.OperationOpenAppDetailsSettings, "Opening app details settings.");
        AndroidNotificationManager.Instance.OpenAppDetailsSettings();
#else
        SetResult("Android device only. Run this sample on Android to verify app details settings.");
#endif
    }

    private void OnExactAlarmSettingsClicked()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        RegisterRequestedOperation(AndroidNotificationManager.OperationOpenExactAlarmSettings, "Opening exact alarm settings.");
        AndroidNotificationManager.Instance.OpenExactAlarmSettings();
#else
        SetResult("Android device only. Run this sample on Android to verify exact alarm settings.");
#endif
    }

    private void OnCreateChannelClicked()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        RegisterRequestedOperation(AndroidNotificationManager.OperationCreateChannel, $"Creating channel: {SampleChannelName} ({SampleChannelId})");
        AndroidNotificationManager.Instance.CreateChannel(BuildSampleChannelJson());
#else
        SetResult("Android device only. Run this sample on Android to create the gameplay notification channel.");
#endif
    }

    private void OnDeleteChannelClicked()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        RegisterRequestedOperation(AndroidNotificationManager.OperationDeleteChannel, $"Deleting channel: {SampleChannelId}");
        AndroidNotificationManager.Instance.DeleteChannel(SampleChannelId);
#else
        SetResult("Android device only. Run this sample on Android to delete the gameplay notification channel.");
#endif
    }

    private void OnShowNotificationClicked()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        RegisterRequestedOperation(AndroidNotificationManager.OperationShowNotification, $"Showing notification {ImmediateNotificationId}: Energy Refilled");
        AndroidNotificationManager.Instance.ShowNotification(BuildImmediateNotificationJson());
#else
        SetResult("Android device only. Run this sample on Android to show a gameplay notification.");
#endif
    }

    private void OnUpdateNotificationClicked()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        RegisterRequestedOperation(AndroidNotificationManager.OperationUpdateNotification, $"Updating notification {ImmediateNotificationId}: Daily Reward Ready");
        AndroidNotificationManager.Instance.UpdateNotification(BuildUpdatedNotificationJson());
#else
        SetResult("Android device only. Run this sample on Android to update the gameplay notification.");
#endif
    }

    private void OnCancelNotificationClicked()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        RegisterRequestedOperation(AndroidNotificationManager.OperationCancelNotification, $"Cancelling notification {ImmediateNotificationId} with tag energy");
        AndroidNotificationManager.Instance.CancelNotification(ImmediateNotificationId, "energy");
#else
        SetResult("Android device only. Run this sample on Android to cancel the gameplay notification.");
#endif
    }

    private void OnCancelAllNotificationsClicked()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        RegisterRequestedOperation(AndroidNotificationManager.OperationCancelAllNotifications, "Cancelling all visible gameplay notifications.");
        AndroidNotificationManager.Instance.CancelAllNotifications();
#else
        SetResult("Android device only. Run this sample on Android to clear gameplay notifications.");
#endif
    }

    private void OnScheduleNotificationClicked()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        long triggerTime = GetScheduledTriggerTime();
        RegisterRequestedOperation(
            AndroidNotificationManager.OperationScheduleNotification,
            $"Scheduling Guild Battle reminder for {DateTimeOffset.FromUnixTimeMilliseconds(triggerTime).LocalDateTime:HH:mm:ss}.");
        AndroidNotificationManager.Instance.ScheduleNotification(BuildScheduledNotificationJson(triggerTime));
#else
        SetResult("Android device only. Run this sample on Android to schedule the gameplay reminder.");
#endif
    }

    private void OnCancelScheduledNotificationClicked()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        RegisterRequestedOperation(AndroidNotificationManager.OperationCancelScheduledNotification, $"Cancelling scheduled notification {ScheduledNotificationId} with tag guild-battle");
        AndroidNotificationManager.Instance.CancelScheduledNotification(ScheduledNotificationId, "guild-battle");
#else
        SetResult("Android device only. Run this sample on Android to cancel the scheduled gameplay reminder.");
#endif
    }

    private void OnCancelAllScheduledNotificationsClicked()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        RegisterRequestedOperation(AndroidNotificationManager.OperationCancelAllScheduledNotifications, "Cancelling all scheduled gameplay reminders.");
        AndroidNotificationManager.Instance.CancelAllScheduledNotifications();
#else
        SetResult("Android device only. Run this sample on Android to clear scheduled gameplay reminders.");
#endif
    }

    private void OnStartProgressClicked()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        _currentProgressValue = 15;
        RegisterRequestedOperation(
            AndroidNotificationManager.OperationStartProgressForegroundService,
            $"Starting foreground progress notification {ProgressNotificationId} at {_currentProgressValue}%.");
        AndroidNotificationManager.Instance.StartProgressForegroundService(BuildForegroundStartJson());
#else
        SetResult("Android device only. Run this sample on Android to start the foreground progress notification.");
#endif
    }

    private void OnUpdateProgressClicked()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        _currentProgressValue = Mathf.Min(_currentProgressValue + 35, 90);
        RegisterRequestedOperation(
            AndroidNotificationManager.OperationUpdateProgressForegroundService,
            $"Updating foreground progress notification {ProgressNotificationId} to {_currentProgressValue}%.");
        AndroidNotificationManager.Instance.UpdateProgressForegroundService(BuildForegroundUpdateJson(_currentProgressValue));
#else
        SetResult("Android device only. Run this sample on Android to update the foreground progress notification.");
#endif
    }

    private void OnCompleteProgressClicked()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        RegisterRequestedOperation(AndroidNotificationManager.OperationCompleteProgressForegroundService, $"Completing foreground progress notification {ProgressNotificationId}.");
        AndroidNotificationManager.Instance.CompleteProgressForegroundService(BuildForegroundCompleteJson());
#else
        SetResult("Android device only. Run this sample on Android to complete the foreground progress notification.");
#endif
    }

    private void OnStopProgressClicked()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        RegisterRequestedOperation(AndroidNotificationManager.OperationStopProgressForegroundService, $"Stopping foreground progress notification {ProgressNotificationId}.");
        AndroidNotificationManager.Instance.StopProgressForegroundService();
#else
        SetResult("Android device only. Run this sample on Android to stop the foreground progress notification.");
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private void RegisterRequestedOperation(string operation, string description)
    {
        _pendingOperationDescriptions[operation] = description;
        SetResult($"Requested: {description}");
    }

    private void OnNotificationOperationCompleted(string operation, bool isSuccessful, string? errorMessage)
    {
        string description = _pendingOperationDescriptions.TryGetValue(operation, out string? value)
            ? value
            : GetOperationDescription(operation);

        _pendingOperationDescriptions.Remove(operation);

        string status = isSuccessful ? "Success" : "Failed";
        string message = $"{GetOperationTitle(operation)}: {status}\n{description}";

        if (!string.IsNullOrEmpty(errorMessage))
        {
            message += $"\nError: {errorMessage}";
        }

        SetResult(message);
    }

    private static string GetOperationTitle(string operation)
    {
        return operation switch
        {
            AndroidNotificationManager.OperationOpenNotificationSettings => "OpenNotificationSettings",
            AndroidNotificationManager.OperationOpenAppDetailsSettings => "OpenAppDetailsSettings",
            AndroidNotificationManager.OperationOpenExactAlarmSettings => "OpenExactAlarmSettings",
            AndroidNotificationManager.OperationCreateChannel => "CreateChannel",
            AndroidNotificationManager.OperationDeleteChannel => "DeleteChannel",
            AndroidNotificationManager.OperationShowNotification => "ShowNotification",
            AndroidNotificationManager.OperationUpdateNotification => "UpdateNotification",
            AndroidNotificationManager.OperationCancelNotification => "CancelNotification",
            AndroidNotificationManager.OperationCancelAllNotifications => "CancelAllNotifications",
            AndroidNotificationManager.OperationScheduleNotification => "ScheduleNotification",
            AndroidNotificationManager.OperationCancelScheduledNotification => "CancelScheduledNotification",
            AndroidNotificationManager.OperationCancelAllScheduledNotifications => "CancelAllScheduledNotifications",
            AndroidNotificationManager.OperationStartProgressForegroundService => "StartProgressForegroundService",
            AndroidNotificationManager.OperationUpdateProgressForegroundService => "UpdateProgressForegroundService",
            AndroidNotificationManager.OperationCompleteProgressForegroundService => "CompleteProgressForegroundService",
            AndroidNotificationManager.OperationStopProgressForegroundService => "StopProgressForegroundService",
            _ => operation
        };
    }

    private static string GetOperationDescription(string operation)
    {
        return operation switch
        {
            AndroidNotificationManager.OperationOpenNotificationSettings => "Opening notification settings.",
            AndroidNotificationManager.OperationOpenAppDetailsSettings => "Opening app details settings.",
            AndroidNotificationManager.OperationOpenExactAlarmSettings => "Opening exact alarm settings.",
            AndroidNotificationManager.OperationCreateChannel => "Creating gameplay notification channel.",
            AndroidNotificationManager.OperationDeleteChannel => "Deleting gameplay notification channel.",
            AndroidNotificationManager.OperationShowNotification => "Showing gameplay notification.",
            AndroidNotificationManager.OperationUpdateNotification => "Updating gameplay notification.",
            AndroidNotificationManager.OperationCancelNotification => "Cancelling gameplay notification.",
            AndroidNotificationManager.OperationCancelAllNotifications => "Cancelling all visible gameplay notifications.",
            AndroidNotificationManager.OperationScheduleNotification => "Scheduling gameplay reminder.",
            AndroidNotificationManager.OperationCancelScheduledNotification => "Cancelling scheduled gameplay reminder.",
            AndroidNotificationManager.OperationCancelAllScheduledNotifications => "Cancelling all scheduled gameplay reminders.",
            AndroidNotificationManager.OperationStartProgressForegroundService => "Starting foreground progress notification.",
            AndroidNotificationManager.OperationUpdateProgressForegroundService => "Updating foreground progress notification.",
            AndroidNotificationManager.OperationCompleteProgressForegroundService => "Completing foreground progress notification.",
            AndroidNotificationManager.OperationStopProgressForegroundService => "Stopping foreground progress notification.",
            _ => operation
        };
    }
#endif

    private string BuildSampleChannelJson()
    {
        return JsonUtility.ToJson(new ChannelPayload
        {
            id = SampleChannelId,
            name = SampleChannelName,
            importance = 4,
            description = "Gameplay alerts for energy, rewards, battles, and crafting updates.",
            showBadge = true,
            enableLights = true,
            lightColor = unchecked((int)0xFF4CAF50),
            enableVibration = true,
            vibrationPattern = new long[] { 0, 250, 200, 250 },
            soundUri = null,
            lockscreenVisibility = 1,
            groupId = "gameplay",
            groupName = "Gameplay"
        });
    }

    private string BuildImmediateNotificationJson()
    {
        return JsonUtility.ToJson(new NotificationPayload
        {
            id = ImmediateNotificationId,
            title = "Energy Refilled",
            message = "Your squad is fully rested. Jump back in and clear the next raid.",
            tag = "energy",
            channel = CreateGameplayChannelReference(),
            subText = "Raid Ready",
            autoCancel = true,
            priority = 1,
            style = new NotificationStylePayload
            {
                type = "bigText",
                bigText = "Your squad is fully rested. Jump back in and clear the next raid before the stamina cap overflows.",
                bigContentTitle = "Energy Refilled",
                summaryText = "Raid Ready"
            }
        });
    }

    private string BuildUpdatedNotificationJson()
    {
        return JsonUtility.ToJson(new NotificationPayload
        {
            id = ImmediateNotificationId,
            title = "Daily Reward Ready",
            message = "Your login streak chest is waiting in town.",
            tag = "energy",
            channel = CreateGameplayChannelReference(),
            subText = "Town Reward",
            autoCancel = true,
            priority = 1,
            style = new NotificationStylePayload
            {
                type = "bigText",
                bigText = "Your login streak chest is waiting in town. Claim it now to keep your reward multiplier active.",
                bigContentTitle = "Daily Reward Ready",
                summaryText = "Town Reward"
            }
        });
    }

    private string BuildScheduledNotificationJson(long triggerTime)
    {
        return JsonUtility.ToJson(new ScheduledNotificationEnvelopePayload
        {
            notification = new NotificationPayload
            {
                id = ScheduledNotificationId,
                title = "Guild Battle Starts Soon",
                message = "Your team queue opens in one minute. Rally your guild and prepare to deploy.",
                tag = "guild-battle",
                channel = CreateGameplayChannelReference(),
                autoCancel = true,
                priority = 1,
                style = new NotificationStylePayload
                {
                    type = "bigText",
                    bigText = "Your team queue opens in one minute. Rally your guild, finalize your loadout, and prepare to deploy.",
                    bigContentTitle = "Guild Battle Starts Soon",
                    summaryText = "Guild Event"
                }
            },
            schedule = new NotificationSchedulePayload
            {
                triggerAtMillis = triggerTime,
                exact = true,
                allowWhileIdle = true,
                persistAcrossBoot = true,
                alarmType = 0
            }
        });
    }

    private string BuildForegroundStartJson()
    {
        return JsonUtility.ToJson(new NotificationPayload
        {
            id = ProgressNotificationId,
            title = "Downloading Guild Battle Assets",
            message = "Preparing the arena for your next match.",
            channel = CreateGameplayChannelReference(),
            ongoing = true,
            autoCancel = false,
            progress = new NotificationProgressPayload
            {
                max = 100,
                current = _currentProgressValue,
                indeterminate = false
            },
            style = new NotificationStylePayload
            {
                type = "bigText",
                bigText = "Preparing the arena for your next match. Keep the app alive while assets finish downloading.",
                bigContentTitle = "Downloading Guild Battle Assets",
                summaryText = "Background Download"
            }
        });
    }

    private string BuildForegroundUpdateJson(int progressValue)
    {
        return JsonUtility.ToJson(new NotificationPayload
        {
            id = ProgressNotificationId,
            title = "Crafting Legendary Gear",
            message = "The forge is running at full power.",
            channel = CreateGameplayChannelReference(),
            ongoing = true,
            autoCancel = false,
            onlyAlertOnce = true,
            progress = new NotificationProgressPayload
            {
                max = 100,
                current = progressValue,
                indeterminate = false
            },
            style = new NotificationStylePayload
            {
                type = "bigText",
                bigText = "The forge is running at full power. Stay ready to equip the reward when crafting finishes.",
                bigContentTitle = "Crafting Legendary Gear",
                summaryText = "Forge Update"
            }
        });
    }

    private string BuildForegroundCompleteJson()
    {
        return JsonUtility.ToJson(new NotificationPayload
        {
            id = ProgressNotificationId,
            title = "Crafting Complete",
            message = "Your legendary sword is ready to claim.",
            channel = CreateGameplayChannelReference(),
            ongoing = false,
            autoCancel = true,
            progress = new NotificationProgressPayload
            {
                max = 100,
                current = 100,
                indeterminate = false
            },
            style = new NotificationStylePayload
            {
                type = "bigText",
                bigText = "Your legendary sword is ready to claim. Return to the forge and equip it before the next battle.",
                bigContentTitle = "Crafting Complete",
                summaryText = "Forge Complete"
            }
        });
    }

    private ChannelPayload CreateGameplayChannelReference()
    {
        return new ChannelPayload
        {
            id = SampleChannelId,
            name = SampleChannelName,
            importance = 4,
            description = "Gameplay alerts for energy, rewards, battles, and crafting updates.",
            showBadge = true,
            enableLights = true,
            lightColor = unchecked((int)0xFF4CAF50),
            enableVibration = true,
            vibrationPattern = new long[] { 0, 250, 200, 250 },
            lockscreenVisibility = 1,
            groupId = "gameplay",
            groupName = "Gameplay"
        };
    }

    private long GetScheduledTriggerTime()
    {
        return DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeMilliseconds();
    }

    private void SetResult(string message)
    {
        if (_resultLabel != null)
        {
            _resultLabel.text = message;
        }
    }

    [Serializable]
    private sealed class ChannelPayload
    {
        public string id = string.Empty;
        public string name = string.Empty;
        public int importance;
        public string description = string.Empty;
        public bool showBadge;
        public bool enableLights;
        public int lightColor;
        public bool enableVibration;
        public long[]? vibrationPattern;
        public string? soundUri;
        public int lockscreenVisibility;
        public string groupId = string.Empty;
        public string groupName = string.Empty;
    }

    [Serializable]
    private class NotificationPayload
    {
        public int id;
        public string title = string.Empty;
        public string message = string.Empty;
        public string tag = string.Empty;
        public ChannelPayload channel = new ChannelPayload();
        public string subText = string.Empty;
        public bool autoCancel;
        public int priority;
        public bool ongoing;
        public bool onlyAlertOnce;
        public NotificationProgressPayload? progress;
        public NotificationStylePayload? style;
    }

    [Serializable]
    private sealed class ScheduledNotificationEnvelopePayload
    {
        public NotificationPayload notification = new NotificationPayload();
        public NotificationSchedulePayload schedule = new NotificationSchedulePayload();
    }

    [Serializable]
    private sealed class NotificationSchedulePayload
    {
        public long triggerAtMillis;
        public bool exact;
        public bool allowWhileIdle;
        public bool persistAcrossBoot;
        public int alarmType;
    }

    [Serializable]
    private sealed class NotificationProgressPayload
    {
        public int max;
        public int current;
        public bool indeterminate;
    }

    [Serializable]
    private sealed class NotificationStylePayload
    {
        public string type = string.Empty;
        public string bigText = string.Empty;
        public string summaryText = string.Empty;
        public string bigContentTitle = string.Empty;
    }
}
#endif