#nullable enable

#if UNITY_STANDALONE_OSX || UNITY_EDITOR
using System;
using JonghyunKim.NativeToolkit.Runtime.Notification;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Example controller demonstrating macOS notification features via <see cref="MacNotificationManager"/>.
/// </summary>
public class MacNotificationManagerExampleController : MonoBehaviour
{
    private const string LogTag = "MacNotificationManagerExampleController";

    [SerializeField] private UIDocument? uiDocument;

    private const string SampleNotificationId = "mac-sample-notification";
    private const string SampleCategoryId = "mac-sample-category";
    private const string NotificationPermissionRequiredMessage = "Please allow notification permission first.";

    private Label? _resultLabel;
    private Button? _homeButton;
    private Button? _requestPermissionButton;
    private Button? _hasPermissionButton;
    private Button? _authorizationStatusButton;
    private Button? _openSettingsButton;
    private Button? _showImmediateButton;
    private Button? _showTimeIntervalButton;
    private Button? _showCalendarButton;
    private Button? _updateByIdButton;
    private Button? _cancelByIdButton;
    private Button? _cancelAllButton;
    private Button? _removeDeliveredByIdButton;
    private Button? _removeAllDeliveredButton;
    private Button? _scheduleTimeIntervalButton;
    private Button? _scheduleCalendarButton;
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
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        MacNotificationManager.Instance.NotificationActionReceived += OnNotificationActionReceived;
        MacNotificationManager.Instance.NotificationTextInputActionReceived += OnNotificationTextInputActionReceived;
#endif
    }

    private void OnDisable()
    {
        Debug.Log($"[{LogTag}][{nameof(OnDisable)}]");
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        MacNotificationManager.Instance.NotificationActionReceived -= OnNotificationActionReceived;
        MacNotificationManager.Instance.NotificationTextInputActionReceived -= OnNotificationTextInputActionReceived;
#endif
    }

    private void OnDestroy()
    {
        Debug.Log($"[{LogTag}][{nameof(OnDestroy)}]");
        if (_homeButton != null) _homeButton.clicked -= OnHomeClicked;
        if (_requestPermissionButton != null) _requestPermissionButton.clicked -= OnRequestPermissionClicked;
        if (_hasPermissionButton != null) _hasPermissionButton.clicked -= OnHasPermissionClicked;
        if (_authorizationStatusButton != null) _authorizationStatusButton.clicked -= OnAuthorizationStatusClicked;
        if (_openSettingsButton != null) _openSettingsButton.clicked -= OnOpenSettingsClicked;
        if (_showImmediateButton != null) _showImmediateButton.clicked -= OnShowImmediateClicked;
        if (_showTimeIntervalButton != null) _showTimeIntervalButton.clicked -= OnShowTimeIntervalClicked;
        if (_showCalendarButton != null) _showCalendarButton.clicked -= OnShowCalendarClicked;
        if (_updateByIdButton != null) _updateByIdButton.clicked -= OnUpdateByIdClicked;
        if (_cancelByIdButton != null) _cancelByIdButton.clicked -= OnCancelByIdClicked;
        if (_cancelAllButton != null) _cancelAllButton.clicked -= OnCancelAllClicked;
        if (_removeDeliveredByIdButton != null) _removeDeliveredByIdButton.clicked -= OnRemoveDeliveredByIdClicked;
        if (_removeAllDeliveredButton != null) _removeAllDeliveredButton.clicked -= OnRemoveAllDeliveredClicked;
        if (_scheduleTimeIntervalButton != null) _scheduleTimeIntervalButton.clicked -= OnScheduleTimeIntervalClicked;
        if (_scheduleCalendarButton != null) _scheduleCalendarButton.clicked -= OnScheduleCalendarClicked;
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
        _openSettingsButton = root.Q<Button>("OpenSettingsButton");
        _showImmediateButton = root.Q<Button>("ShowImmediateButton");
        _showTimeIntervalButton = root.Q<Button>("ShowTimeIntervalButton");
        _showCalendarButton = root.Q<Button>("ShowCalendarButton");
        _updateByIdButton = root.Q<Button>("UpdateByIdButton");
        _cancelByIdButton = root.Q<Button>("CancelByIdButton");
        _cancelAllButton = root.Q<Button>("CancelAllButton");
        _removeDeliveredByIdButton = root.Q<Button>("RemoveDeliveredByIdButton");
        _removeAllDeliveredButton = root.Q<Button>("RemoveAllDeliveredButton");
        _scheduleTimeIntervalButton = root.Q<Button>("ScheduleTimeIntervalButton");
        _scheduleCalendarButton = root.Q<Button>("ScheduleCalendarButton");
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
        if (_openSettingsButton != null) _openSettingsButton.clicked += OnOpenSettingsClicked;
        if (_showImmediateButton != null) _showImmediateButton.clicked += OnShowImmediateClicked;
        if (_showTimeIntervalButton != null) _showTimeIntervalButton.clicked += OnShowTimeIntervalClicked;
        if (_showCalendarButton != null) _showCalendarButton.clicked += OnShowCalendarClicked;
        if (_updateByIdButton != null) _updateByIdButton.clicked += OnUpdateByIdClicked;
        if (_cancelByIdButton != null) _cancelByIdButton.clicked += OnCancelByIdClicked;
        if (_cancelAllButton != null) _cancelAllButton.clicked += OnCancelAllClicked;
        if (_removeDeliveredByIdButton != null) _removeDeliveredByIdButton.clicked += OnRemoveDeliveredByIdClicked;
        if (_removeAllDeliveredButton != null) _removeAllDeliveredButton.clicked += OnRemoveAllDeliveredClicked;
        if (_scheduleTimeIntervalButton != null) _scheduleTimeIntervalButton.clicked += OnScheduleTimeIntervalClicked;
        if (_scheduleCalendarButton != null) _scheduleCalendarButton.clicked += OnScheduleCalendarClicked;
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
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        MacNotificationManager.Instance.RequestPermission(result =>
        {
            SetResult(FormatResult("RequestPermission", result));
        });
#else
        SetResult("macOS Standalone only. Run this sample on macOS to verify.");
#endif
    }

    private void OnHasPermissionClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnHasPermissionClicked)}]");
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        MacNotificationManager.Instance.HasPermission(hasPermission =>
        {
            SetResult($"HasPermission: {hasPermission}");
        });
#else
        SetResult("macOS Standalone only. Run this sample on macOS to verify.");
#endif
    }

    private void OnAuthorizationStatusClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnAuthorizationStatusClicked)}]");
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        MacNotificationManager.Instance.GetAuthorizationStatus(result =>
        {
            var status = MacNotificationAuthorizationStatusParser.ParseJson(result.Json);
            SetResult($"AuthorizationStatus: {status}");
        });
#else
        SetResult("macOS Standalone only. Run this sample on macOS to verify.");
#endif
    }

    private void OnOpenSettingsClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnOpenSettingsClicked)}]");
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        MacNotificationManager.Instance.OpenSettings(result =>
        {
            SetResult(FormatResult("OpenSettings", result));
        });
#else
        SetResult("macOS Standalone only. Run this sample on macOS to verify.");
#endif
    }

    private void OnShowImmediateClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnShowImmediateClicked)}]");
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        ExecuteIfNotificationPermissionGranted("ShowImmediate", () =>
        {
            var content = new NotificationContentPayload
            {
                id = SampleNotificationId,
                title = "Energy Refilled",
                body = "Your squad is fully rested. Jump back in and clear the next raid.",
                categoryIdentifier = SampleCategoryId
            };
            var contentJson = MacNotificationJsonBuilder.BuildContentJson(content);
            MacNotificationManager.Instance.ShowNotification(contentJson, null, result =>
            {
                SetResult(result.IsSuccess
                    ? "✓ ShowImmediate\nLong-press the foreground banner or delivered notification to open Open / Delete / Reply."
                    : FormatResult("ShowImmediate", result));
            });
        });
#else
        SetResult("macOS Standalone only. Run this sample on macOS to verify.");
#endif
    }

    private void OnShowTimeIntervalClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnShowTimeIntervalClicked)}]");
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        ExecuteIfNotificationPermissionGranted("ShowTimeInterval", () =>
        {
            var content = new NotificationContentPayload
            {
                id = SampleNotificationId,
                title = "Guild Battle Countdown",
                body = "Your team queue opens in 5 seconds. Rally your party and get ready."
            };
            var trigger = new TimeIntervalTriggerPayload { interval = 5.0, repeats = false };
            var contentJson = MacNotificationJsonBuilder.BuildContentJson(content);
            var triggerJson = MacNotificationJsonBuilder.BuildTimeIntervalTriggerJson(trigger);
            MacNotificationManager.Instance.ShowNotification(contentJson, triggerJson, result =>
            {
                SetResult(FormatResult("ShowTimeInterval", result));
            });
        });
#else
        SetResult("macOS Standalone only. Run this sample on macOS to verify.");
#endif
    }

    private void OnShowCalendarClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnShowCalendarClicked)}]");
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        ExecuteIfNotificationPermissionGranted("ShowCalendar", () =>
        {
            var now = DateTime.Now.AddMinutes(1);
            var content = new NotificationContentPayload
            {
                id = SampleNotificationId,
                title = "Daily Reward Ready",
                body = "Your login streak chest is ready in town. Claim it before reset."
            };
            var trigger = new CalendarTriggerPayload
            {
                year = now.Year,
                month = now.Month,
                day = now.Day,
                hour = now.Hour,
                minute = now.Minute,
                second = now.Second,
                repeats = false
            };
            var contentJson = MacNotificationJsonBuilder.BuildContentJson(content);
            var triggerJson = MacNotificationJsonBuilder.BuildCalendarTriggerJson(trigger);
            MacNotificationManager.Instance.ShowNotification(contentJson, triggerJson, result =>
            {
                SetResult(FormatResult("ShowCalendar", result));
            });
        });
#else
        SetResult("macOS Standalone only. Run this sample on macOS to verify.");
#endif
    }

    private void OnUpdateByIdClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnUpdateByIdClicked)}]");
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        ExecuteIfNotificationPermissionGranted("UpdateById", () =>
        {
            var content = new NotificationContentPayload
            {
                id = SampleNotificationId,
                title = "Town Entry Bonus",
                body = "Welcome back to town. Your blacksmith bonus is now available."
            };
            var contentJson = MacNotificationJsonBuilder.BuildContentJson(content);
            MacNotificationManager.Instance.UpdateNotification(SampleNotificationId, contentJson, null, result =>
            {
                SetResult(FormatResult($"UpdateById ({SampleNotificationId})", result));
            });
        });
#else
        SetResult("macOS Standalone only. Run this sample on macOS to verify.");
#endif
    }

    private void OnCancelByIdClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnCancelByIdClicked)}]");
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        ExecuteIfNotificationPermissionGranted("CancelById", () =>
        {
            MacNotificationManager.Instance.CancelNotification(SampleNotificationId);
            SetResult($"CancelById ({SampleNotificationId}): requested");
        });
#else
        SetResult("macOS Standalone only. Run this sample on macOS to verify.");
#endif
    }

    private void OnCancelAllClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnCancelAllClicked)}]");
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        ExecuteIfNotificationPermissionGranted("CancelAll", () =>
        {
            MacNotificationManager.Instance.CancelAllNotifications();
            SetResult("CancelAll: requested");
        });
#else
        SetResult("macOS Standalone only. Run this sample on macOS to verify.");
#endif
    }

    private void OnRemoveDeliveredByIdClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnRemoveDeliveredByIdClicked)}]");
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        ExecuteIfNotificationPermissionGranted("RemoveDeliveredById", () =>
        {
            MacNotificationManager.Instance.RemoveDeliveredNotification(SampleNotificationId);
            SetResult($"RemoveDeliveredById ({SampleNotificationId}): requested");
        });
#else
        SetResult("macOS Standalone only. Run this sample on macOS to verify.");
#endif
    }

    private void OnRemoveAllDeliveredClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnRemoveAllDeliveredClicked)}]");
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        ExecuteIfNotificationPermissionGranted("RemoveAllDelivered", () =>
        {
            MacNotificationManager.Instance.RemoveAllDeliveredNotifications();
            SetResult("RemoveAllDelivered: requested");
        });
#else
        SetResult("macOS Standalone only. Run this sample on macOS to verify.");
#endif
    }

    private void OnScheduleTimeIntervalClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnScheduleTimeIntervalClicked)}]");
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        ExecuteIfNotificationPermissionGranted("ScheduleTimeInterval", () =>
        {
            var content = new NotificationContentPayload
            {
                id = SampleNotificationId,
                title = "Guild Battle Starts Soon",
                body = "Battle queue opens in 10 seconds. Finalize your loadout and deploy."
            };
            var trigger = new TimeIntervalTriggerPayload { interval = 10.0, repeats = false };
            var contentJson = MacNotificationJsonBuilder.BuildContentJson(content);
            var triggerJson = MacNotificationJsonBuilder.BuildTimeIntervalTriggerJson(trigger);
            MacNotificationManager.Instance.ScheduleNotification(contentJson, triggerJson, result =>
            {
                SetResult(FormatResult("ScheduleTimeInterval", result));
            });
        });
#else
        SetResult("macOS Standalone only. Run this sample on macOS to verify.");
#endif
    }

    private void OnScheduleCalendarClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnScheduleCalendarClicked)}]");
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        ExecuteIfNotificationPermissionGranted("ScheduleCalendar", () =>
        {
            var future = DateTime.Now.AddMinutes(1);
            var content = new NotificationContentPayload
            {
                id = SampleNotificationId,
                title = "Daily Reward Window",
                body = "Your daily reward window is open. Check in now to keep your streak."
            };
            var trigger = new CalendarTriggerPayload
            {
                year = future.Year,
                month = future.Month,
                day = future.Day,
                hour = future.Hour,
                minute = future.Minute,
                second = future.Second,
                repeats = false
            };
            var contentJson = MacNotificationJsonBuilder.BuildContentJson(content);
            var triggerJson = MacNotificationJsonBuilder.BuildCalendarTriggerJson(trigger);
            MacNotificationManager.Instance.ScheduleNotification(contentJson, triggerJson, result =>
            {
                SetResult(FormatResult("ScheduleCalendar", result));
            });
        });
#else
        SetResult("macOS Standalone only. Run this sample on macOS to verify.");
#endif
    }

    private void OnCancelScheduledByIdClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnCancelScheduledByIdClicked)}]");
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        ExecuteIfNotificationPermissionGranted("CancelScheduledById", () =>
        {
            MacNotificationManager.Instance.CancelScheduledNotification(SampleNotificationId);
            SetResult($"CancelScheduledById ({SampleNotificationId}): requested");
        });
#else
        SetResult("macOS Standalone only. Run this sample on macOS to verify.");
#endif
    }

    private void OnCancelAllScheduledClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnCancelAllScheduledClicked)}]");
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        ExecuteIfNotificationPermissionGranted("CancelAllScheduled", () =>
        {
            MacNotificationManager.Instance.CancelAllScheduledNotifications();
            SetResult("CancelAllScheduled: requested");
        });
#else
        SetResult("macOS Standalone only. Run this sample on macOS to verify.");
#endif
    }

    private void OnGetScheduledClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnGetScheduledClicked)}]");
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        ExecuteIfNotificationPermissionGranted("GetScheduled", () =>
        {
            MacNotificationManager.Instance.GetScheduledNotifications(result =>
            {
                SetResult($"GetScheduled:\n{result.Json}");
            });
        });
#else
        SetResult("macOS Standalone only. Run this sample on macOS to verify.");
#endif
    }

    private void OnGetDeliveredClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnGetDeliveredClicked)}]");
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        ExecuteIfNotificationPermissionGranted("GetDelivered", () =>
        {
            MacNotificationManager.Instance.GetDeliveredNotifications(result =>
            {
                SetResult($"GetDelivered:\n{result.Json}");
            });
        });
#else
        SetResult("macOS Standalone only. Run this sample on macOS to verify.");
#endif
    }

    private void OnSetBadgeCount1Clicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnSetBadgeCount1Clicked)}]");
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        ExecuteIfNotificationPermissionGranted("SetBadgeCount(1)", () =>
        {
            MacNotificationManager.Instance.SetBadgeCount(1, result =>
            {
                SetResult(FormatResult("SetBadgeCount(1)", result));
            });
        });
#else
        SetResult("macOS Standalone only. Run this sample on macOS to verify.");
#endif
    }

    private void OnSetBadgeCount0Clicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnSetBadgeCount0Clicked)}]");
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        ExecuteIfNotificationPermissionGranted("SetBadgeCount(0)", () =>
        {
            MacNotificationManager.Instance.SetBadgeCount(0, result =>
            {
                SetResult(FormatResult("SetBadgeCount(0)", result));
            });
        });
#else
        SetResult("macOS Standalone only. Run this sample on macOS to verify.");
#endif
    }

    private void OnRegisterCategoryClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnRegisterCategoryClicked)}]");
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        ExecuteIfNotificationPermissionGranted("RegisterCategory", () =>
        {
            var category = new MacNotificationCategoryPayload
            {
                id = SampleCategoryId,
                actions = new[]
                {
                    new MacNotificationActionPayload
                    {
                        id = "open",
                        title = "Open",
                        isForeground = true,
                        isTextInput = false
                    },
                    new MacNotificationActionPayload
                    {
                        id = "delete",
                        title = "Delete",
                        isForeground = false,
                        isTextInput = false
                    },
                    new MacNotificationActionPayload
                    {
                        id = "reply",
                        title = "Reply",
                        isForeground = false,
                        isTextInput = true,
                        textInputPlaceholder = "Type a message"
                    }
                }
            };
            var categoryJson = MacNotificationJsonBuilder.BuildCategoryJson(category);
            MacNotificationManager.Instance.RegisterCategory(categoryJson, result =>
            {
                SetResult(result.IsSuccess
                    ? "✓ RegisterCategory\nNext, tap ShowImmediate and long-press the foreground banner to open Open / Delete / Reply."
                    : FormatResult("RegisterCategory", result));
            });
        });
#else
        SetResult("macOS Standalone only. Run this sample on macOS to verify.");
#endif
    }

    private void OnRemoveCategoryClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnRemoveCategoryClicked)}]");
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        ExecuteIfNotificationPermissionGranted("RemoveCategory", () =>
        {
            MacNotificationManager.Instance.RemoveCategory(SampleCategoryId, result =>
            {
                SetResult(FormatResult($"RemoveCategory ({SampleCategoryId})", result));
            });
        });
#else
        SetResult("macOS Standalone only. Run this sample on macOS to verify.");
#endif
    }

    // ── Event Handlers ───────────────────────────────────────────────────────

    private void OnNotificationActionReceived(MacNotificationActionResult result)
    {
        Debug.Log($"[{LogTag}][{nameof(OnNotificationActionReceived)}] result: {result}");
        SetResult($"Action received: notificationId={result.NotificationId}, actionId={result.ActionId}");
    }

    private void OnNotificationTextInputActionReceived(MacNotificationTextInputActionResult result)
    {
        Debug.Log($"[{LogTag}][{nameof(OnNotificationTextInputActionReceived)}] result: {result}");
        SetResult($"TextInput action received: notificationId={result.NotificationId}, actionId={result.ActionId}, userText={result.UserText}");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
    private void ExecuteIfNotificationPermissionGranted(string operationName, Action onGranted)
    {
        MacNotificationManager.Instance.HasPermission(hasPermission =>
        {
            if (!hasPermission)
            {
                SetResult($"{operationName}: {NotificationPermissionRequiredMessage}");
                return;
            }

            onGranted();
        });
    }

    private static string FormatResult(string label, MacNotificationResult result)
    {
        var icon = result.IsSuccess ? "✓" : "✗";
        return result.IsSuccess
            ? $"{icon} {label}"
            : $"{icon} {label}\nError: {result.ErrorMessage ?? "nil"}";
    }
#endif

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
