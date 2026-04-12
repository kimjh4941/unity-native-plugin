#nullable enable

#if UNITY_ANDROID || UNITY_EDITOR
using UnityEngine;
using UnityEngine.UIElements;

#if UNITY_ANDROID && !UNITY_EDITOR
using JonghyunKim.NativeToolkit.Runtime.Notification;
#endif

public class AndroidNotificationManagerExampleController : MonoBehaviour
{
    [SerializeField] private UIDocument? uiDocument;

    private Label? _resultLabel;
    private Button? _homeButton;
    private Button? _hasPermissionButton;
    private Button? _notificationsEnabledButton;
    private Button? _notificationSettingsButton;
    private Button? _appDetailsSettingsButton;

    private void Start()
    {
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
        if (_homeButton != null) _homeButton.clicked -= OnHomeClicked;
        if (_hasPermissionButton != null) _hasPermissionButton.clicked -= OnHasPermissionClicked;
        if (_notificationsEnabledButton != null) _notificationsEnabledButton.clicked -= OnNotificationsEnabledClicked;
        if (_notificationSettingsButton != null) _notificationSettingsButton.clicked -= OnNotificationSettingsClicked;
        if (_appDetailsSettingsButton != null) _appDetailsSettingsButton.clicked -= OnAppDetailsSettingsClicked;
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

        if (_homeButton != null) _homeButton.clicked += OnHomeClicked;
        if (_hasPermissionButton != null) _hasPermissionButton.clicked += OnHasPermissionClicked;
        if (_notificationsEnabledButton != null) _notificationsEnabledButton.clicked += OnNotificationsEnabledClicked;
        if (_notificationSettingsButton != null) _notificationSettingsButton.clicked += OnNotificationSettingsClicked;
        if (_appDetailsSettingsButton != null) _appDetailsSettingsButton.clicked += OnAppDetailsSettingsClicked;
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
        SetResult("HasPermission: simulated in Editor");
#endif
    }

    private void OnNotificationsEnabledClicked()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        SetResult($"AreNotificationsEnabled: {AndroidNotificationManager.Instance.AreNotificationsEnabled()}");
#else
        SetResult("AreNotificationsEnabled: simulated in Editor");
#endif
    }

    private void OnNotificationSettingsClicked()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        SetResult($"OpenNotificationSettings: {AndroidNotificationManager.Instance.OpenNotificationSettings()}");
#else
        SetResult("OpenNotificationSettings: simulated in Editor");
#endif
    }

    private void OnAppDetailsSettingsClicked()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        SetResult($"OpenAppDetailsSettings: {AndroidNotificationManager.Instance.OpenAppDetailsSettings()}");
#else
        SetResult("OpenAppDetailsSettings: simulated in Editor");
#endif
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