#nullable enable

#if UNITY_ANDROID || UNITY_IOS || UNITY_EDITOR
using UnityEngine;
using UnityEngine.UIElements;

public class TopMenuExampleController : MonoBehaviour
{
    private const string LogTag = "TopMenuExampleController";

    [SerializeField] private UIDocument? uiDocument;

    private Button? _dialogButton;
    private Button? _notificationButton;

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

    private void OnDestroy()
    {
        Debug.Log($"[{LogTag}][{nameof(OnDestroy)}]");
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
        Debug.Log($"[{LogTag}][{nameof(InitializeUI)}]");
        var root = uiDocument?.rootVisualElement;
        if (root == null)
        {
            Debug.LogError($"[{LogTag}][{nameof(InitializeUI)}] rootVisualElement is null.");
            return;
        }

        _dialogButton = root.Q<Button>("DialogFeatureButton");
        _notificationButton = root.Q<Button>("NotificationFeatureButton");

        if (_dialogButton != null)
        {
            _dialogButton.clicked += OnDialogClicked;
        }

#if UNITY_ANDROID || UNITY_IOS || UNITY_EDITOR
        if (_notificationButton != null)
        {
            _notificationButton.clicked += OnNotificationClicked;
        }
#else
        // Hide Notification button on platforms where the feature is not available.
        if (_notificationButton != null)
        {
            Debug.Log($"[{LogTag}][{nameof(InitializeUI)}] Notification feature is not supported on this platform. Hiding button.");
            _notificationButton.style.display = DisplayStyle.None;
        }
#endif
    }

    private void OnDialogClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnDialogClicked)}]");
        if (uiDocument == null) return;
#if UNITY_EDITOR
        UnityEditor.EditorUtility.DisplayDialog(
            "Dialog Feature",
            "This feature runs natively on Android or iOS.\nRun on an Android or iOS player for full functionality.",
            "OK");
#elif UNITY_ANDROID
        NativeToolkitSampleNavigator.ShowAndroidDialog(uiDocument);
#elif UNITY_IOS
        NativeToolkitSampleNavigator.ShowIosDialog(uiDocument);
#endif
    }

    private void OnNotificationClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnNotificationClicked)}]");
        if (uiDocument == null) return;
#if UNITY_EDITOR
        UnityEditor.EditorUtility.DisplayDialog(
            "Notification Feature",
            "This feature runs natively on Android or iOS.\nRun on an Android or iOS player for full functionality.",
            "OK");
#elif UNITY_ANDROID
        NativeToolkitSampleNavigator.ShowAndroidNotification(uiDocument);
#elif UNITY_IOS
        NativeToolkitSampleNavigator.ShowIosNotification(uiDocument);
#endif
    }
}
#endif