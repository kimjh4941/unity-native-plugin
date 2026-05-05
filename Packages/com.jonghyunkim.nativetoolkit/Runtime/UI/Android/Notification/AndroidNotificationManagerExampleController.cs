#nullable enable

#if UNITY_ANDROID || UNITY_EDITOR
using System;
using System.Collections.Generic;
using JonghyunKim.NativeToolkit.Runtime.Notification;
using UnityEngine;
using UnityEngine.UIElements;

public class AndroidNotificationManagerExampleController : MonoBehaviour
{
    private const string LogTag = "AndroidNotificationManagerExampleController";

    [SerializeField] private UIDocument? uiDocument;

    private const string SampleChannelId = "native_toolkit_gameplay";
    private const string SampleChannelName = "Gameplay Alerts";
    private const int ImmediateNotificationId = 1101;
    private const int ScheduledNotificationId = 1201;
    private const int ProgressNotificationId = 1301;
    private const int ActionNotificationId = 1401;
    private const int FullScreenNotificationId = 1501;
    private const int DecoratedCustomViewNotificationId = 1601;
    private const string UnityAppIconResourceName = "app_icon";
    private const string UnityAppIconResourceType = "mipmap";
    private const string NotificationPermissionRequiredMessage = "Please allow notification permission first.";
    private const string ActionPlayNow = "com.jonghyunkim.nativetoolkit.ACTION_PLAY_NOW";
    private const string ActionDismiss = "com.jonghyunkim.nativetoolkit.ACTION_DISMISS";
    private const string ActionCustomViewDismiss = "com.jonghyunkim.nativetoolkit.ACTION_CUSTOM_VIEW_DISMISS";

    private Label? _resultLabel;
    private Button? _homeButton;
    private Button? _hasPermissionButton;
    private Button? _notificationsEnabledButton;
    private Button? _notificationSettingsButton;
    private Button? _appDetailsSettingsButton;
    private Button? _exactAlarmSettingsButton;
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
    private Button? _showActionNotificationButton;
    private Button? _showFullScreenNotificationButton;
    private Button? _requestPermissionButton;
    private Button? _canScheduleExactAlarmsButton;
    private Button? _isScheduledNotificationButton;
    private Button? _showDecoratedCustomViewButton;

    private int _currentProgressValue = 15;
    private readonly Dictionary<string, string> _pendingOperationDescriptions = new Dictionary<string, string>();

    private void Awake()
    {
        Debug.Log($"[{LogTag}][{nameof(Awake)}]");
    }

    private void Start()
    {
        Debug.Log($"[{LogTag}][{nameof(Start)}]");
#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidNotificationManager.Instance.NotificationOperationCompleted += OnNotificationOperationCompleted;
        AndroidNotificationManager.Instance.NotificationActionTapped += OnNotificationActionTapped;
        AndroidNotificationManager.Instance.NotificationReceived += OnNotificationReceived;
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
        Debug.Log($"[{LogTag}][{nameof(OnDestroy)}]");
#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidNotificationManager.Instance.NotificationOperationCompleted -= OnNotificationOperationCompleted;
        AndroidNotificationManager.Instance.NotificationActionTapped -= OnNotificationActionTapped;
        AndroidNotificationManager.Instance.NotificationReceived -= OnNotificationReceived;
#endif

        if (_homeButton != null) _homeButton.clicked -= OnHomeClicked;
        if (_hasPermissionButton != null) _hasPermissionButton.clicked -= OnHasPermissionClicked;
        if (_notificationsEnabledButton != null) _notificationsEnabledButton.clicked -= OnNotificationsEnabledClicked;
        if (_notificationSettingsButton != null) _notificationSettingsButton.clicked -= OnNotificationSettingsClicked;
        if (_appDetailsSettingsButton != null) _appDetailsSettingsButton.clicked -= OnAppDetailsSettingsClicked;
        if (_exactAlarmSettingsButton != null) _exactAlarmSettingsButton.clicked -= OnExactAlarmSettingsClicked;
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
        if (_showActionNotificationButton != null) _showActionNotificationButton.clicked -= OnShowActionNotificationClicked;
        if (_showFullScreenNotificationButton != null) _showFullScreenNotificationButton.clicked -= OnShowFullScreenNotificationClicked;
        if (_requestPermissionButton != null) _requestPermissionButton.clicked -= OnRequestPermissionClicked;
        if (_canScheduleExactAlarmsButton != null) _canScheduleExactAlarmsButton.clicked -= OnCanScheduleExactAlarmsClicked;
        if (_isScheduledNotificationButton != null) _isScheduledNotificationButton.clicked -= OnIsScheduledNotificationClicked;
        if (_showDecoratedCustomViewButton != null) _showDecoratedCustomViewButton.clicked -= OnShowDecoratedCustomViewClicked;
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
        _showActionNotificationButton = root.Q<Button>("ShowActionNotificationButton");
        _showFullScreenNotificationButton = root.Q<Button>("ShowFullScreenNotificationButton");
        _requestPermissionButton = root.Q<Button>("RequestPermissionButton");
        _canScheduleExactAlarmsButton = root.Q<Button>("CanScheduleExactAlarmsButton");
        _isScheduledNotificationButton = root.Q<Button>("IsScheduledNotificationButton");
        _showDecoratedCustomViewButton = root.Q<Button>("ShowDecoratedCustomViewButton");

        if (_homeButton != null) _homeButton.clicked += OnHomeClicked;
        if (_hasPermissionButton != null) _hasPermissionButton.clicked += OnHasPermissionClicked;
        if (_notificationsEnabledButton != null) _notificationsEnabledButton.clicked += OnNotificationsEnabledClicked;
        if (_notificationSettingsButton != null) _notificationSettingsButton.clicked += OnNotificationSettingsClicked;
        if (_appDetailsSettingsButton != null) _appDetailsSettingsButton.clicked += OnAppDetailsSettingsClicked;
        if (_exactAlarmSettingsButton != null) _exactAlarmSettingsButton.clicked += OnExactAlarmSettingsClicked;
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
        if (_showActionNotificationButton != null) _showActionNotificationButton.clicked += OnShowActionNotificationClicked;
        if (_showFullScreenNotificationButton != null) _showFullScreenNotificationButton.clicked += OnShowFullScreenNotificationClicked;
        if (_requestPermissionButton != null) _requestPermissionButton.clicked += OnRequestPermissionClicked;
        if (_canScheduleExactAlarmsButton != null) _canScheduleExactAlarmsButton.clicked += OnCanScheduleExactAlarmsClicked;
        if (_isScheduledNotificationButton != null) _isScheduledNotificationButton.clicked += OnIsScheduledNotificationClicked;
        if (_showDecoratedCustomViewButton != null) _showDecoratedCustomViewButton.clicked += OnShowDecoratedCustomViewClicked;

        SetResult("Android notification sample ready. Test immediate, scheduled, and progress notifications on an Android device.");
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

    private void OnShowNotificationClicked()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!EnsureNotificationPermission())
        {
            return;
        }

        RegisterRequestedOperation(AndroidNotificationManager.OperationShowNotification, $"Showing notification {ImmediateNotificationId}: Energy Refilled");
        AndroidNotificationManager.Instance.ShowNotification(BuildImmediateNotificationJson());
#else
        SetResult("Android device only. Run this sample on Android to show a gameplay notification.");
#endif
    }

    private void OnUpdateNotificationClicked()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!EnsureNotificationPermission())
        {
            return;
        }

        RegisterRequestedOperation(AndroidNotificationManager.OperationUpdateNotification, $"Updating notification {ImmediateNotificationId}: Daily Reward Ready");
        AndroidNotificationManager.Instance.UpdateNotification(BuildUpdatedNotificationJson());
#else
        SetResult("Android device only. Run this sample on Android to update the gameplay notification.");
#endif
    }

    private void OnCancelNotificationClicked()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!EnsureNotificationPermission())
        {
            return;
        }

        RegisterRequestedOperation(AndroidNotificationManager.OperationCancelNotification, $"Cancelling notification {ImmediateNotificationId} with tag energy");
        AndroidNotificationManager.Instance.CancelNotification(ImmediateNotificationId, "energy");
#else
        SetResult("Android device only. Run this sample on Android to cancel the gameplay notification.");
#endif
    }

    private void OnCancelAllNotificationsClicked()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!EnsureNotificationPermission())
        {
            return;
        }

        RegisterRequestedOperation(AndroidNotificationManager.OperationCancelAllNotifications, "Cancelling all visible gameplay notifications.");
        AndroidNotificationManager.Instance.CancelAllNotifications();
#else
        SetResult("Android device only. Run this sample on Android to clear gameplay notifications.");
#endif
    }

    private void OnScheduleNotificationClicked()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!EnsureNotificationPermission())
        {
            return;
        }

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
        if (!EnsureNotificationPermission())
        {
            return;
        }

        RegisterRequestedOperation(AndroidNotificationManager.OperationCancelScheduledNotification, $"Cancelling scheduled notification {ScheduledNotificationId} with tag guild-battle");
        AndroidNotificationManager.Instance.CancelScheduledNotification(ScheduledNotificationId, "guild-battle");
#else
        SetResult("Android device only. Run this sample on Android to cancel the scheduled gameplay reminder.");
#endif
    }

    private void OnCancelAllScheduledNotificationsClicked()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!EnsureNotificationPermission())
        {
            return;
        }

        RegisterRequestedOperation(AndroidNotificationManager.OperationCancelAllScheduledNotifications, "Cancelling all scheduled gameplay reminders.");
        AndroidNotificationManager.Instance.CancelAllScheduledNotifications();
#else
        SetResult("Android device only. Run this sample on Android to clear scheduled gameplay reminders.");
#endif
    }

    private void OnStartProgressClicked()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!EnsureNotificationPermission())
        {
            return;
        }

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
        if (!EnsureNotificationPermission())
        {
            return;
        }

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
        if (!EnsureNotificationPermission())
        {
            return;
        }

        RegisterRequestedOperation(AndroidNotificationManager.OperationCompleteProgressForegroundService, $"Completing foreground progress notification {ProgressNotificationId}.");
        AndroidNotificationManager.Instance.CompleteProgressForegroundService(BuildForegroundCompleteJson());
#else
        SetResult("Android device only. Run this sample on Android to complete the foreground progress notification.");
#endif
    }

    private void OnStopProgressClicked()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!EnsureNotificationPermission())
        {
            return;
        }

        RegisterRequestedOperation(AndroidNotificationManager.OperationStopProgressForegroundService, $"Stopping foreground progress notification {ProgressNotificationId}.");
        AndroidNotificationManager.Instance.StopProgressForegroundService();
#else
        SetResult("Android device only. Run this sample on Android to stop the foreground progress notification.");
#endif
    }

    private void OnShowActionNotificationClicked()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!EnsureNotificationPermission())
        {
            return;
        }

        RegisterRequestedOperation(AndroidNotificationManager.OperationShowNotification, $"Showing match alert {ActionNotificationId} with action buttons.");
        AndroidNotificationManager.Instance.ShowNotification(BuildActionNotificationJson());
#else
        SetResult("Android device only. Run this sample on Android to show a notification with action buttons.");
#endif
    }

    private void OnShowFullScreenNotificationClicked()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!EnsureNotificationPermission())
        {
            return;
        }

        RegisterRequestedOperation(AndroidNotificationManager.OperationShowNotification, $"Showing full-screen match start notification {FullScreenNotificationId}.");
        AndroidNotificationManager.Instance.ShowNotification(BuildFullScreenNotificationJson());
#else
        SetResult("Android device only. Run this sample on Android to show a full-screen notification.");
#endif
    }

    private void OnCanScheduleExactAlarmsClicked()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        SetResult($"CanScheduleExactAlarms: {AndroidNotificationManager.Instance.CanScheduleExactAlarms()}");
#else
        SetResult("Android device only. Run this sample on Android 12+ to check exact alarm permission.");
#endif
    }

    private void OnRequestPermissionClicked()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        SetResult("Requesting POST_NOTIFICATIONS permission...");
        AndroidNotificationManager.Instance.RequestPermission(granted =>
        {
            SetResult($"Permission {(granted ? "Granted" : "Denied")}");
        });
#else
        SetResult("Android device only. Run this sample on Android 13+ to request notification permission.");
#endif
    }

    private void OnIsScheduledNotificationClicked()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        bool scheduled = AndroidNotificationManager.Instance.IsNotificationScheduled(ScheduledNotificationId, "guild-battle");
        SetResult($"IsNotificationScheduled({ScheduledNotificationId}, guild-battle): {scheduled}");
#else
        SetResult("Android device only. Run this sample on Android to check scheduled notification status.");
#endif
    }

    private void OnShowDecoratedCustomViewClicked()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!EnsureNotificationPermission())
        {
            return;
        }

        RegisterRequestedOperation(AndroidNotificationManager.OperationShowNotification, $"Showing custom view notification {DecoratedCustomViewNotificationId}.");
        AndroidNotificationManager.Instance.ShowNotification(BuildDecoratedCustomViewNotificationJson());
#else
        SetResult("Android device only. Run this sample on Android to show a decorated custom view notification.");
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private void RegisterRequestedOperation(string operation, string description)
    {
        _pendingOperationDescriptions[operation] = description;
        SetResult($"Requested: {description}");
    }

    private bool EnsureNotificationPermission()
    {
        if (AndroidNotificationManager.Instance.HasPermission())
        {
            return true;
        }

        SetResult(NotificationPermissionRequiredMessage);
        return false;
    }

    private void OnNotificationOperationCompleted(NotificationResult result)
    {
        string description = _pendingOperationDescriptions.TryGetValue(result.Operation, out string? value)
            ? value
            : GetOperationDescription(result.Operation);

        _pendingOperationDescriptions.Remove(result.Operation);

        string status = result.IsSuccess ? "Success" : "Failed";
        string message = $"{GetOperationTitle(result.Operation)}: {status}\n{description}";

        if (!string.IsNullOrEmpty(result.ErrorMessage))
        {
            message += $"\nError: {result.ErrorMessage}";
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

    private void OnNotificationReceived(NotificationReceivedResult result)
    {
        string message = $"Notification Received (Foreground)\nID: {result.NotificationId}\nChannel: {result.ChannelId}";
        if (result.Tag != null) message += $"\nTag: {result.Tag}";
        SetResult(message);
    }

    private void OnNotificationActionTapped(NotificationActionResult result)
    {
        bool isBodyTap = result.ActionId == AndroidNotificationManager.ActionBodyTap;
        bool isDismiss = result.ActionId == AndroidNotificationManager.ActionNotificationDismissed;
        string tapType = isBodyTap ? "Body Tap" : isDismiss ? "Dismissed" : "Action Button";
        string message = $"Notification {tapType}\nNotification ID: {result.NotificationId}";
        if (!isBodyTap && !isDismiss)
        {
            message += $"\nAction: {result.ActionId}";
        }

        if (result.Data != null && result.Data.Count > 0)
        {
            message += "\nData:";
            foreach (var kvp in result.Data)
            {
                message += $"\n  {kvp.Key}={kvp.Value}";
            }
        }

        SetResult(message);
    }
#endif

    private string BuildImmediateNotificationJson()
    {
        return AndroidNotificationJsonBuilder.BuildNotificationJson(new NotificationPayload
        {
            id = ImmediateNotificationId,
            title = "Energy Refilled",
            message = "Your squad is fully rested. Jump back in and clear the next raid.",
            tag = "energy",
            channel = CreateGameplayChannelReference(),
            smallIcon = CreateUnityAppIconResource(),
            largeIcon = CreateUnityAppIconResource(),
            subText = "Raid Ready",
            autoCancel = true,
            priority = 1,
            category = "recommendation",
            ticker = "Energy refilled",
            number = 3,
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
        return AndroidNotificationJsonBuilder.BuildNotificationJson(new NotificationPayload
        {
            id = ImmediateNotificationId,
            title = "Daily Reward Ready",
            message = "Your login streak chest is waiting in town.",
            tag = "energy",
            channel = CreateGameplayChannelReference(),
            smallIcon = CreateUnityAppIconResource(),
            largeIcon = CreateUnityAppIconResource(),
            subText = "Town Reward",
            autoCancel = true,
            priority = 1,
            style = new NotificationStylePayload
            {
                type = "bigPicture",
                picture = CreateUnityAppIconResource(),
                largeIcon = CreateUnityAppIconResource(),
                hideExpandedLargeIcon = false,
                bigText = "Your login streak chest is waiting in town. Claim it now to keep your reward multiplier active.",
                bigContentTitle = "Daily Reward Ready",
                summaryText = "Town Reward"
            }
        });
    }

    private string BuildScheduledNotificationJson(long triggerTime)
    {
        return AndroidNotificationJsonBuilder.BuildScheduledNotificationJson(new ScheduledNotificationEnvelopePayload
        {
            notification = new NotificationPayload
            {
                id = ScheduledNotificationId,
                title = "Guild Battle Starts Soon",
                message = "Your team queue opens in 15 seconds. Rally your guild and prepare to deploy.",
                tag = "guild-battle",
                channel = CreateGameplayChannelReference(),
                smallIcon = CreateUnityAppIconResource(),
                autoCancel = true,
                priority = 1,
                groupKey = "guild-events",
                sortKey = "001",
                style = new NotificationStylePayload
                {
                    type = "bigText",
                    bigText = "Your team queue opens in 15 seconds. Rally your guild, finalize your loadout, and prepare to deploy.",
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
        return AndroidNotificationJsonBuilder.BuildNotificationJson(new NotificationPayload
        {
            id = ProgressNotificationId,
            title = "Downloading Guild Battle Assets",
            message = "Preparing the arena for your next match.",
            channel = CreateGameplayChannelReference(),
            smallIcon = CreateUnityAppIconResource(),
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
        return AndroidNotificationJsonBuilder.BuildNotificationJson(new NotificationPayload
        {
            id = ProgressNotificationId,
            title = "Crafting Legendary Gear",
            message = "The forge is running at full power.",
            channel = CreateGameplayChannelReference(),
            smallIcon = CreateUnityAppIconResource(),
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
        return AndroidNotificationJsonBuilder.BuildNotificationJson(new NotificationPayload
        {
            id = ProgressNotificationId,
            title = "Crafting Complete",
            message = "Your legendary sword is ready to claim.",
            channel = CreateGameplayChannelReference(),
            smallIcon = CreateUnityAppIconResource(),
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

    private string BuildActionNotificationJson()
    {
        return AndroidNotificationJsonBuilder.BuildNotificationJson(new NotificationPayload
        {
            id = ActionNotificationId,
            title = "Match Found",
            message = "A ranked match is ready. Accept within 30 seconds.",
            channel = CreateGameplayChannelReference(),
            smallIcon = CreateUnityAppIconResource(),
            autoCancel = true,
            priority = 1,
            launchAction = "open_battle_screen",
            actions = new[]
            {
                new NotificationActionPayload
                {
                    title = "Play Now",
                    actionId = ActionPlayNow,
                    icon = CreateUnityAppIconResource(),
                    launchApp = true,
                    showsUserInterface = true
                },
                new NotificationActionPayload
                {
                    title = "Dismiss",
                    actionId = ActionDismiss,
                    launchApp = false,
                    showsUserInterface = false
                }
            },
            data = new[]
            {
                new NotificationDataEntryPayload { key = "screen", value = "battle" },
                new NotificationDataEntryPayload { key = "matchId", value = "match_5678" }
            }
        });
    }

    private string BuildFullScreenNotificationJson()
    {
        return AndroidNotificationJsonBuilder.BuildNotificationJson(new NotificationPayload
        {
            id = FullScreenNotificationId,
            title = "Match Starting",
            message = "Your match begins now. Launching game screen.",
            channel = CreateGameplayChannelReference(),
            smallIcon = CreateUnityAppIconResource(),
            autoCancel = true,
            priority = 2,
            category = "call",
            fullScreenIntent = true
        });
    }

    private string BuildDecoratedCustomViewNotificationJson()
    {
        return AndroidNotificationJsonBuilder.BuildNotificationJson(new NotificationPayload
        {
            id = DecoratedCustomViewNotificationId,
            title = "Custom Layout Notification",
            message = "Expand to see the custom view and tap Dismiss.",
            channel = CreateGameplayChannelReference(),
            smallIcon = CreateUnityAppIconResource(),
            autoCancel = true,
            style = new NotificationStylePayload
            {
                type = "decoratedCustomView",
                customViewLayout = "nt_notification_custom_view_sample",
                bigCustomViewLayout = "nt_notification_custom_view_sample_expanded",
                viewActions = new[]
                {
                    new NotificationViewActionPayload
                    {
                        type = "setClickIntent",
                        viewId = "nt_notification_btn_dismiss",
                        actionId = ActionCustomViewDismiss
                    }
                }
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

    private NotificationResourcePayload CreateUnityAppIconResource()
    {
        return new NotificationResourcePayload
        {
            name = UnityAppIconResourceName,
            type = UnityAppIconResourceType
        };
    }

    private long GetScheduledTriggerTime()
    {
        return DateTimeOffset.UtcNow.AddSeconds(15).ToUnixTimeMilliseconds();
    }

    private void SetResult(string message)
    {
        if (_resultLabel != null)
        {
            _resultLabel.text = message;
        }
    }
}
#endif