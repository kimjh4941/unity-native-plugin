#nullable enable

#if UNITY_ANDROID || UNITY_EDITOR
using UnityEngine;
using UnityEngine.UIElements;

public class TopMenuExampleController : MonoBehaviour
{
    [SerializeField] private UIDocument? uiDocument;

    private Button? _dialogButton;
    private Button? _notificationButton;

    private void Start()
    {
        if (uiDocument == null)
        {
            uiDocument = GetComponent<UIDocument>();
        }

        if (uiDocument == null)
        {
            Debug.LogError("[TopMenuExampleController] UIDocument component not found.");
            return;
        }

        InitializeUI();
    }

    private void OnDestroy()
    {
        if (_dialogButton != null)
        {
            _dialogButton.clicked -= OnDialogClicked;
        }

        if (_notificationButton != null)
        {
            _notificationButton.clicked -= OnNotificationClicked;
        }
    }

    private void InitializeUI()
    {
        var root = uiDocument?.rootVisualElement;
        if (root == null)
        {
            Debug.LogError("[TopMenuExampleController] rootVisualElement is null.");
            return;
        }

        _dialogButton = root.Q<Button>("DialogFeatureButton");
        _notificationButton = root.Q<Button>("NotificationFeatureButton");

        if (_dialogButton != null)
        {
            _dialogButton.clicked += OnDialogClicked;
        }

        if (_notificationButton != null)
        {
            _notificationButton.clicked += OnNotificationClicked;
        }
    }

    private void OnDialogClicked()
    {
        if (uiDocument != null)
        {
            NativeToolkitSampleNavigator.ShowAndroidDialog(uiDocument);
        }
    }

    private void OnNotificationClicked()
    {
        if (uiDocument != null)
        {
            NativeToolkitSampleNavigator.ShowAndroidNotification(uiDocument);
        }
    }
}
#endif