#nullable enable

#if UNITY_IOS || UNITY_EDITOR
using JonghyunKim.NativeToolkit.Runtime.Notification;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Example controller demonstrating iOS notification features via <see cref="IosNotificationManager"/>.
/// </summary>
public class IosNotificationManagerExampleController : MonoBehaviour
{
    private const string LogTag = "IosNotificationManagerExampleController";

    [SerializeField] private UIDocument? uiDocument;

    private const string SampleNotificationId = "sample-notification";
    private const string SampleScheduledId = "scheduled-notification";
    private const string SampleCategoryId = "sample-category";

    private Label? _resultLabel;
    private Button? _homeButton;
    private Button? _requestPermissionButton;
    private Button? _hasPermissionButton;
    private Button? _authorizationStatusButton;
    private Button? _openNotificationSettingsButton;
    private Button? _showImmediateButton;
    private Button? _showTimeIntervalButton;
    private Button? _showCalendarButton;
    private Button? _showLocationButton;
    private Button? _updateByIdButton;
    private Button? _cancelByIdButton;
    private Button? _cancelAllButton;
    private Button? _removeDeliveredByIdButton;
    private Button? _removeAllDeliveredButton;
    private Button? _scheduleTimeIntervalButton;
    private Button? _scheduleCalendarButton;
    private Button? _scheduleLocationButton;
    private Button? _cancelScheduledByIdButton;
    private Button? _cancelAllScheduledButton;
    private Button? _getScheduledButton;
    private Button? _getDeliveredButton;
    private Button? _setBadgeCount1Button;
    private Button? _setBadgeCount0Button;
    private Button? _registerCategoryButton;
    private Button? _removeCategoryButton;

    private void Awake()
    {
        Debug.Log($"[{LogTag}][{nameof(Awake)}]");
    }

    private void Start()
    {
        Debug.Log($"[{LogTag}][{nameof(Start)}]");
#if UNITY_IOS && !UNITY_EDITOR
        IosNotificationManager.Instance.NotificationOperationCompleted += OnNotificationOperationCompleted;
        IosNotificationManager.Instance.NotificationActionReceived += OnNotificationActionReceived;
        IosNotificationManager.Instance.NotificationTextInputActionReceived += OnNotificationTextInputActionReceived;
#endif

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

    private void OnDestroy()
    {
        Debug.Log($"[{LogTag}][{nameof(OnDestroy)}]");
#if UNITY_IOS && !UNITY_EDITOR
        IosNotificationManager.Instance.NotificationOperationCompleted -= OnNotificationOperationCompleted;
        IosNotificationManager.Instance.NotificationActionReceived -= OnNotificationActionReceived;
        IosNotificationManager.Instance.NotificationTextInputActionReceived -= OnNotificationTextInputActionReceived;
#endif

        if (_homeButton != null) _homeButton.clicked -= OnHomeClicked;
        if (_requestPermissionButton != null) _requestPermissionButton.clicked -= OnRequestPermissionClicked;
        if (_hasPermissionButton != null) _hasPermissionButton.clicked -= OnHasPermissionClicked;
        if (_authorizationStatusButton != null) _authorizationStatusButton.clicked -= OnAuthorizationStatusClicked;
        if (_openNotificationSettingsButton != null) _openNotificationSettingsButton.clicked -= OnOpenNotificationSettingsClicked;
        if (_showImmediateButton != null) _showImmediateButton.clicked -= OnShowImmediateClicked;
        if (_showTimeIntervalButton != null) _showTimeIntervalButton.clicked -= OnShowTimeIntervalClicked;
        if (_showCalendarButton != null) _showCalendarButton.clicked -= OnShowCalendarClicked;
        if (_showLocationButton != null) _showLocationButton.clicked -= OnShowLocationClicked;
        if (_updateByIdButton != null) _updateByIdButton.clicked -= OnUpdateByIdClicked;
        if (_cancelByIdButton != null) _cancelByIdButton.clicked -= OnCancelByIdClicked;
        if (_cancelAllButton != null) _cancelAllButton.clicked -= OnCancelAllClicked;
        if (_removeDeliveredByIdButton != null) _removeDeliveredByIdButton.clicked -= OnRemoveDeliveredByIdClicked;
        if (_removeAllDeliveredButton != null) _removeAllDeliveredButton.clicked -= OnRemoveAllDeliveredClicked;
        if (_scheduleTimeIntervalButton != null) _scheduleTimeIntervalButton.clicked -= OnScheduleTimeIntervalClicked;
        if (_scheduleCalendarButton != null) _scheduleCalendarButton.clicked -= OnScheduleCalendarClicked;
        if (_scheduleLocationButton != null) _scheduleLocationButton.clicked -= OnScheduleLocationClicked;
        if (_cancelScheduledByIdButton != null) _cancelScheduledByIdButton.clicked -= OnCancelScheduledByIdClicked;
        if (_cancelAllScheduledButton != null) _cancelAllScheduledButton.clicked -= OnCancelAllScheduledClicked;
        if (_getScheduledButton != null) _getScheduledButton.clicked -= OnGetScheduledClicked;
        if (_getDeliveredButton != null) _getDeliveredButton.clicked -= OnGetDeliveredClicked;
        if (_setBadgeCount1Button != null) _setBadgeCount1Button.clicked -= OnSetBadgeCount1Clicked;
        if (_setBadgeCount0Button != null) _setBadgeCount0Button.clicked -= OnSetBadgeCount0Clicked;
        if (_registerCategoryButton != null) _registerCategoryButton.clicked -= OnRegisterCategoryClicked;
        if (_removeCategoryButton != null) _removeCategoryButton.clicked -= OnRemoveCategoryClicked;
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

        _resultLabel = root.Q<Label>("ResultTextBlock");
        _homeButton = root.Q<Button>("HomeButton");
        _requestPermissionButton = root.Q<Button>("RequestPermissionButton");
        _hasPermissionButton = root.Q<Button>("HasPermissionButton");
        _authorizationStatusButton = root.Q<Button>("AuthorizationStatusButton");
        _openNotificationSettingsButton = root.Q<Button>("OpenNotificationSettingsButton");
        _showImmediateButton = root.Q<Button>("ShowImmediateButton");
        _showTimeIntervalButton = root.Q<Button>("ShowTimeIntervalButton");
        _showCalendarButton = root.Q<Button>("ShowCalendarButton");
        _showLocationButton = root.Q<Button>("ShowLocationButton");
        _updateByIdButton = root.Q<Button>("UpdateByIdButton");
        _cancelByIdButton = root.Q<Button>("CancelByIdButton");
        _cancelAllButton = root.Q<Button>("CancelAllButton");
        _removeDeliveredByIdButton = root.Q<Button>("RemoveDeliveredByIdButton");
        _removeAllDeliveredButton = root.Q<Button>("RemoveAllDeliveredButton");
        _scheduleTimeIntervalButton = root.Q<Button>("ScheduleTimeIntervalButton");
        _scheduleCalendarButton = root.Q<Button>("ScheduleCalendarButton");
        _scheduleLocationButton = root.Q<Button>("ScheduleLocationButton");
        _cancelScheduledByIdButton = root.Q<Button>("CancelScheduledByIdButton");
        _cancelAllScheduledButton = root.Q<Button>("CancelAllScheduledButton");
        _getScheduledButton = root.Q<Button>("GetScheduledButton");
        _getDeliveredButton = root.Q<Button>("GetDeliveredButton");
        _setBadgeCount1Button = root.Q<Button>("SetBadgeCount1Button");
        _setBadgeCount0Button = root.Q<Button>("SetBadgeCount0Button");
        _registerCategoryButton = root.Q<Button>("RegisterCategoryButton");
        _removeCategoryButton = root.Q<Button>("RemoveCategoryButton");

        if (_homeButton != null) _homeButton.clicked += OnHomeClicked;
        if (_requestPermissionButton != null) _requestPermissionButton.clicked += OnRequestPermissionClicked;
        if (_hasPermissionButton != null) _hasPermissionButton.clicked += OnHasPermissionClicked;
        if (_authorizationStatusButton != null) _authorizationStatusButton.clicked += OnAuthorizationStatusClicked;
        if (_openNotificationSettingsButton != null) _openNotificationSettingsButton.clicked += OnOpenNotificationSettingsClicked;
        if (_showImmediateButton != null) _showImmediateButton.clicked += OnShowImmediateClicked;
        if (_showTimeIntervalButton != null) _showTimeIntervalButton.clicked += OnShowTimeIntervalClicked;
        if (_showCalendarButton != null) _showCalendarButton.clicked += OnShowCalendarClicked;
        if (_showLocationButton != null) _showLocationButton.clicked += OnShowLocationClicked;
        if (_updateByIdButton != null) _updateByIdButton.clicked += OnUpdateByIdClicked;
        if (_cancelByIdButton != null) _cancelByIdButton.clicked += OnCancelByIdClicked;
        if (_cancelAllButton != null) _cancelAllButton.clicked += OnCancelAllClicked;
        if (_removeDeliveredByIdButton != null) _removeDeliveredByIdButton.clicked += OnRemoveDeliveredByIdClicked;
        if (_removeAllDeliveredButton != null) _removeAllDeliveredButton.clicked += OnRemoveAllDeliveredClicked;
        if (_scheduleTimeIntervalButton != null) _scheduleTimeIntervalButton.clicked += OnScheduleTimeIntervalClicked;
        if (_scheduleCalendarButton != null) _scheduleCalendarButton.clicked += OnScheduleCalendarClicked;
        if (_scheduleLocationButton != null) _scheduleLocationButton.clicked += OnScheduleLocationClicked;
        if (_cancelScheduledByIdButton != null) _cancelScheduledByIdButton.clicked += OnCancelScheduledByIdClicked;
        if (_cancelAllScheduledButton != null) _cancelAllScheduledButton.clicked += OnCancelAllScheduledClicked;
        if (_getScheduledButton != null) _getScheduledButton.clicked += OnGetScheduledClicked;
        if (_getDeliveredButton != null) _getDeliveredButton.clicked += OnGetDeliveredClicked;
        if (_setBadgeCount1Button != null) _setBadgeCount1Button.clicked += OnSetBadgeCount1Clicked;
        if (_setBadgeCount0Button != null) _setBadgeCount0Button.clicked += OnSetBadgeCount0Clicked;
        if (_registerCategoryButton != null) _registerCategoryButton.clicked += OnRegisterCategoryClicked;
        if (_removeCategoryButton != null) _removeCategoryButton.clicked += OnRemoveCategoryClicked;
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

    private void OnRequestPermissionClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnRequestPermissionClicked)}]");
#if UNITY_IOS && !UNITY_EDITOR
        IosNotificationManager.Instance.RequestPermission(result =>
        {
            SetResult($"RequestPermission: {result}");
        });
#else
        SetResult("iOS device only. Run this sample on iOS to verify.");
#endif
    }

    private void OnHasPermissionClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnHasPermissionClicked)}]");
#if UNITY_IOS && !UNITY_EDITOR
        IosNotificationManager.Instance.HasPermission(hasPermission =>
        {
            SetResult($"HasPermission: {hasPermission}");
        });
#else
        SetResult("iOS device only. Run this sample on iOS to verify.");
#endif
    }

    private void OnAuthorizationStatusClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnAuthorizationStatusClicked)}]");
#if UNITY_IOS && !UNITY_EDITOR
        IosNotificationManager.Instance.GetAuthorizationStatus(status =>
        {
            SetResult($"AuthorizationStatus: {status}");
        });
#else
        SetResult("iOS device only. Run this sample on iOS to verify.");
#endif
    }

    private void OnOpenNotificationSettingsClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnOpenNotificationSettingsClicked)}]");
#if UNITY_IOS && !UNITY_EDITOR
        IosNotificationManager.Instance.OpenNotificationSettings();
        SetResult("Opening notification settings...");
#else
        SetResult("iOS device only. Run this sample on iOS to verify.");
#endif
    }

    private void OnShowImmediateClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnShowImmediateClicked)}]");
#if UNITY_IOS && !UNITY_EDITOR
        var content = new NotificationContentPayload
        {
            id = SampleNotificationId,
            title = "Immediate Notification",
            body = "This notification was triggered immediately.",
            sound = "default"
        };
        var contentJson = IosNotificationJsonBuilder.BuildContentJson(content);
        IosNotificationManager.Instance.ShowNotification(contentJson, null, result =>
        {
            SetResult($"ShowImmediate: {result}");
        });
#else
        SetResult("iOS device only. Run this sample on iOS to verify.");
#endif
    }

    private void OnShowTimeIntervalClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnShowTimeIntervalClicked)}]");
#if UNITY_IOS && !UNITY_EDITOR
        var content = new NotificationContentPayload
        {
            id = SampleNotificationId,
            title = "Time Interval Notification",
            body = "Delivered after a 5-second interval.",
            sound = "default"
        };
        var trigger = new TimeIntervalTriggerPayload { interval = 5.0, repeats = false };
        var contentJson = IosNotificationJsonBuilder.BuildContentJson(content);
        var triggerJson = IosNotificationJsonBuilder.BuildTimeIntervalTriggerJson(trigger);
        IosNotificationManager.Instance.ShowNotification(contentJson, triggerJson, result =>
        {
            SetResult($"ShowTimeInterval: {result}");
        });
#else
        SetResult("iOS device only. Run this sample on iOS to verify.");
#endif
    }

    private void OnShowCalendarClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnShowCalendarClicked)}]");
#if UNITY_IOS && !UNITY_EDITOR
        var now = System.DateTime.Now.AddSeconds(10);
        var content = new NotificationContentPayload
        {
            id = SampleNotificationId,
            title = "Calendar Notification",
            body = "Delivered at a specific calendar time.",
            sound = "default"
        };
        var trigger = new CalendarTriggerPayload
        {
            hour = now.Hour,
            minute = now.Minute,
            second = now.Second,
            repeats = false
        };
        var contentJson = IosNotificationJsonBuilder.BuildContentJson(content);
        var triggerJson = IosNotificationJsonBuilder.BuildCalendarTriggerJson(trigger);
        IosNotificationManager.Instance.ShowNotification(contentJson, triggerJson, result =>
        {
            SetResult($"ShowCalendar: {result}");
        });
#else
        SetResult("iOS device only. Run this sample on iOS to verify.");
#endif
    }

    private void OnShowLocationClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnShowLocationClicked)}]");
#if UNITY_IOS && !UNITY_EDITOR
        var content = new NotificationContentPayload
        {
            id = SampleNotificationId,
            title = "Location Notification",
            body = "Delivered when entering Tokyo Station area.",
            sound = "default"
        };
        var trigger = new LocationTriggerPayload
        {
            identifier = "tokyo-station",
            latitude = 35.6812,
            longitude = 139.7671,
            radius = 100.0,
            notifyOnEntry = true,
            notifyOnExit = false
        };
        var contentJson = IosNotificationJsonBuilder.BuildContentJson(content);
        var triggerJson = IosNotificationJsonBuilder.BuildLocationTriggerJson(trigger);
        IosNotificationManager.Instance.ShowNotification(contentJson, triggerJson, result =>
        {
            SetResult($"ShowLocation: {result}");
        });
#else
        SetResult("iOS device only. Run this sample on iOS to verify.");
#endif
    }

    private void OnUpdateByIdClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnUpdateByIdClicked)}]");
#if UNITY_IOS && !UNITY_EDITOR
        var content = new NotificationContentPayload
        {
            id = SampleNotificationId,
            title = "Updated Notification",
            body = "This notification has been updated.",
            sound = "default"
        };
        var contentJson = IosNotificationJsonBuilder.BuildContentJson(content);
        IosNotificationManager.Instance.UpdateNotification(SampleNotificationId, contentJson, null, result =>
        {
            SetResult($"UpdateById ({SampleNotificationId}): {result}");
        });
#else
        SetResult("iOS device only. Run this sample on iOS to verify.");
#endif
    }

    private void OnCancelByIdClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnCancelByIdClicked)}]");
#if UNITY_IOS && !UNITY_EDITOR
        IosNotificationManager.Instance.CancelNotification(SampleNotificationId);
        SetResult($"CancelById ({SampleNotificationId}): requested");
#else
        SetResult("iOS device only. Run this sample on iOS to verify.");
#endif
    }

    private void OnCancelAllClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnCancelAllClicked)}]");
#if UNITY_IOS && !UNITY_EDITOR
        IosNotificationManager.Instance.CancelAllNotifications();
        SetResult("CancelAll: requested");
#else
        SetResult("iOS device only. Run this sample on iOS to verify.");
#endif
    }

    private void OnRemoveDeliveredByIdClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnRemoveDeliveredByIdClicked)}]");
#if UNITY_IOS && !UNITY_EDITOR
        IosNotificationManager.Instance.RemoveDeliveredNotification(SampleNotificationId);
        SetResult($"RemoveDeliveredById ({SampleNotificationId}): requested");
#else
        SetResult("iOS device only. Run this sample on iOS to verify.");
#endif
    }

    private void OnRemoveAllDeliveredClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnRemoveAllDeliveredClicked)}]");
#if UNITY_IOS && !UNITY_EDITOR
        IosNotificationManager.Instance.RemoveAllDeliveredNotifications();
        SetResult("RemoveAllDelivered: requested");
#else
        SetResult("iOS device only. Run this sample on iOS to verify.");
#endif
    }

    private void OnScheduleTimeIntervalClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnScheduleTimeIntervalClicked)}]");
#if UNITY_IOS && !UNITY_EDITOR
        var content = new NotificationContentPayload
        {
            id = SampleScheduledId,
            title = "Scheduled (Time Interval)",
            body = "Scheduled notification delivered after 30 seconds.",
            sound = "default"
        };
        var trigger = new TimeIntervalTriggerPayload { interval = 30.0, repeats = false };
        var contentJson = IosNotificationJsonBuilder.BuildContentJson(content);
        var triggerJson = IosNotificationJsonBuilder.BuildTimeIntervalTriggerJson(trigger);
        IosNotificationManager.Instance.ScheduleNotification(contentJson, triggerJson, SampleScheduledId, result =>
        {
            SetResult($"ScheduleTimeInterval: {result}");
        });
#else
        SetResult("iOS device only. Run this sample on iOS to verify.");
#endif
    }

    private void OnScheduleCalendarClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnScheduleCalendarClicked)}]");
#if UNITY_IOS && !UNITY_EDITOR
        var future = System.DateTime.Now.AddMinutes(1);
        var content = new NotificationContentPayload
        {
            id = SampleScheduledId,
            title = "Scheduled (Calendar)",
            body = "Scheduled notification delivered at a calendar time.",
            sound = "default"
        };
        var trigger = new CalendarTriggerPayload
        {
            hour = future.Hour,
            minute = future.Minute,
            second = future.Second,
            repeats = false
        };
        var contentJson = IosNotificationJsonBuilder.BuildContentJson(content);
        var triggerJson = IosNotificationJsonBuilder.BuildCalendarTriggerJson(trigger);
        IosNotificationManager.Instance.ScheduleNotification(contentJson, triggerJson, SampleScheduledId, result =>
        {
            SetResult($"ScheduleCalendar: {result}");
        });
#else
        SetResult("iOS device only. Run this sample on iOS to verify.");
#endif
    }

    private void OnScheduleLocationClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnScheduleLocationClicked)}]");
#if UNITY_IOS && !UNITY_EDITOR
        var content = new NotificationContentPayload
        {
            id = SampleScheduledId,
            title = "Scheduled (Location)",
            body = "Scheduled notification delivered near Tokyo Station.",
            sound = "default"
        };
        var trigger = new LocationTriggerPayload
        {
            identifier = "tokyo-station-scheduled",
            latitude = 35.6812,
            longitude = 139.7671,
            radius = 100.0,
            notifyOnEntry = true,
            notifyOnExit = false
        };
        var contentJson = IosNotificationJsonBuilder.BuildContentJson(content);
        var triggerJson = IosNotificationJsonBuilder.BuildLocationTriggerJson(trigger);
        IosNotificationManager.Instance.ScheduleNotification(contentJson, triggerJson, SampleScheduledId, result =>
        {
            SetResult($"ScheduleLocation: {result}");
        });
#else
        SetResult("iOS device only. Run this sample on iOS to verify.");
#endif
    }

    private void OnCancelScheduledByIdClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnCancelScheduledByIdClicked)}]");
#if UNITY_IOS && !UNITY_EDITOR
        IosNotificationManager.Instance.CancelScheduledNotification(SampleScheduledId);
        SetResult($"CancelScheduledById ({SampleScheduledId}): requested");
#else
        SetResult("iOS device only. Run this sample on iOS to verify.");
#endif
    }

    private void OnCancelAllScheduledClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnCancelAllScheduledClicked)}]");
#if UNITY_IOS && !UNITY_EDITOR
        IosNotificationManager.Instance.CancelAllScheduledNotifications();
        SetResult("CancelAllScheduled: requested");
#else
        SetResult("iOS device only. Run this sample on iOS to verify.");
#endif
    }

    private void OnGetScheduledClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnGetScheduledClicked)}]");
#if UNITY_IOS && !UNITY_EDITOR
        IosNotificationManager.Instance.GetScheduledNotifications(json =>
        {
            SetResult($"GetScheduled:\n{json}");
        });
#else
        SetResult("iOS device only. Run this sample on iOS to verify.");
#endif
    }

    private void OnGetDeliveredClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnGetDeliveredClicked)}]");
#if UNITY_IOS && !UNITY_EDITOR
        IosNotificationManager.Instance.GetDeliveredNotifications(json =>
        {
            SetResult($"GetDelivered:\n{json}");
        });
#else
        SetResult("iOS device only. Run this sample on iOS to verify.");
#endif
    }

    private void OnSetBadgeCount1Clicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnSetBadgeCount1Clicked)}]");
#if UNITY_IOS && !UNITY_EDITOR
        IosNotificationManager.Instance.SetBadgeCount(1, result =>
        {
            SetResult($"SetBadgeCount(1): {result}");
        });
#else
        SetResult("iOS device only. Run this sample on iOS to verify.");
#endif
    }

    private void OnSetBadgeCount0Clicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnSetBadgeCount0Clicked)}]");
#if UNITY_IOS && !UNITY_EDITOR
        IosNotificationManager.Instance.SetBadgeCount(0, result =>
        {
            SetResult($"SetBadgeCount(0): {result}");
        });
#else
        SetResult("iOS device only. Run this sample on iOS to verify.");
#endif
    }

    private void OnRegisterCategoryClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnRegisterCategoryClicked)}]");
#if UNITY_IOS && !UNITY_EDITOR
        var category = new IosNotificationCategoryPayload
        {
            identifier = SampleCategoryId,
            actions = new[]
            {
                new IosNotificationActionPayload
                {
                    identifier = "open",
                    title = "Open",
                    options = new[] { "foreground" }
                },
                new IosNotificationActionPayload
                {
                    identifier = "delete",
                    title = "Delete",
                    options = new[] { "destructive" }
                }
            },
            textInputActions = new[]
            {
                new IosNotificationTextInputActionPayload
                {
                    identifier = "reply",
                    title = "Reply",
                    buttonTitle = "Send",
                    textInputPlaceholder = "Type a message"
                }
            }
        };
        var categoryJson = IosNotificationJsonBuilder.BuildCategoryJson(category);
        IosNotificationManager.Instance.RegisterCategory(categoryJson, result =>
        {
            SetResult($"RegisterCategory: {result}");
        });
#else
        SetResult("iOS device only. Run this sample on iOS to verify.");
#endif
    }

    private void OnRemoveCategoryClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnRemoveCategoryClicked)}]");
#if UNITY_IOS && !UNITY_EDITOR
        IosNotificationManager.Instance.RemoveCategory(SampleCategoryId);
        SetResult($"RemoveCategory ({SampleCategoryId}): requested");
#else
        SetResult("iOS device only. Run this sample on iOS to verify.");
#endif
    }

    // ── Event Handlers ───────────────────────────────────────────────────────

    private void OnNotificationOperationCompleted(IosNotificationResult result)
    {
        Debug.Log($"[{LogTag}][{nameof(OnNotificationOperationCompleted)}] result: {result}");
    }

    private void OnNotificationActionReceived(IosNotificationActionResult result)
    {
        Debug.Log($"[{LogTag}][{nameof(OnNotificationActionReceived)}] result: {result}");
        SetResult($"Action received: notificationId={result.NotificationId}, actionId={result.ActionId}");
    }

    private void OnNotificationTextInputActionReceived(IosNotificationTextInputActionResult result)
    {
        Debug.Log($"[{LogTag}][{nameof(OnNotificationTextInputActionReceived)}] result: {result}");
        SetResult($"TextInput action received: notificationId={result.NotificationId}, actionId={result.ActionId}, userText={result.UserText}");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

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
