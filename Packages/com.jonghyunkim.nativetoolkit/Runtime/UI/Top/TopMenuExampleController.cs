#nullable enable

#if UNITY_ANDROID || UNITY_IOS || UNITY_STANDALONE_OSX || UNITY_STANDALONE_WIN || UNITY_EDITOR
using UnityEngine;
using UnityEngine.UIElements;

public class TopMenuExampleController : MonoBehaviour
{
    private const string LogTag = "TopMenuExampleController";

    [SerializeField] private UIDocument? uiDocument;

    private Button? _dialogButton;
    private Button? _notificationButton;
    private Button? _shareButton;
    private Button? _clipboardButton;

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

        if (_shareButton != null)
        {
            _shareButton.clicked -= OnShareClicked;
        }

        if (_clipboardButton != null)
        {
            _clipboardButton.clicked -= OnClipboardClicked;
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
        _shareButton = root.Q<Button>("ShareFeatureButton");
        _clipboardButton = root.Q<Button>("ClipboardFeatureButton");

        if (_dialogButton != null)
        {
            _dialogButton.clicked += OnDialogClicked;
        }

#if UNITY_ANDROID || UNITY_IOS || UNITY_STANDALONE_OSX || UNITY_STANDALONE_WIN || UNITY_EDITOR
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

#if UNITY_ANDROID || UNITY_IOS || UNITY_STANDALONE_OSX || UNITY_EDITOR
        if (_shareButton != null)
        {
            _shareButton.clicked += OnShareClicked;
        }
#else
        if (_shareButton != null)
        {
            Debug.Log($"[{LogTag}][{nameof(InitializeUI)}] Share feature is only supported on Android, iOS, and macOS. Hiding button.");
            _shareButton.style.display = DisplayStyle.None;
        }
#endif

#if UNITY_ANDROID || UNITY_IOS || UNITY_EDITOR
        if (_clipboardButton != null)
        {
            _clipboardButton.clicked += OnClipboardClicked;
        }
#else
        if (_clipboardButton != null)
        {
            Debug.Log($"[{LogTag}][{nameof(InitializeUI)}] Clipboard feature is only supported on Android and iOS. Hiding button.");
            _clipboardButton.style.display = DisplayStyle.None;
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
            "This feature runs natively on Android, iOS, macOS, or Windows.\nRun on an Android, iOS, macOS, or Windows player for full functionality.",
            "OK");
#elif UNITY_ANDROID
        NativeToolkitSampleNavigator.ShowAndroidDialog(uiDocument);
#elif UNITY_IOS
        NativeToolkitSampleNavigator.ShowIosDialog(uiDocument);
#elif UNITY_STANDALONE_OSX
        NativeToolkitSampleNavigator.ShowMacDialog(uiDocument);
#elif UNITY_STANDALONE_WIN
        NativeToolkitSampleNavigator.ShowWindowsDialog(uiDocument);
#endif
    }

    private void OnNotificationClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnNotificationClicked)}]");
        if (uiDocument == null) return;
#if UNITY_EDITOR
        UnityEditor.EditorUtility.DisplayDialog(
            "Notification Feature",
            "This feature runs natively on Android, iOS, macOS, or Windows.\nRun on an Android, iOS, macOS, or Windows player for full functionality.",
            "OK");
#elif UNITY_ANDROID
        NativeToolkitSampleNavigator.ShowAndroidNotification(uiDocument);
#elif UNITY_IOS
        NativeToolkitSampleNavigator.ShowIosNotification(uiDocument);
#elif UNITY_STANDALONE_OSX
        NativeToolkitSampleNavigator.ShowMacNotification(uiDocument);
#elif UNITY_STANDALONE_WIN
        NativeToolkitSampleNavigator.ShowWindowsNotification(uiDocument);
#endif
    }

    private void OnShareClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnShareClicked)}]");
        if (uiDocument == null) return;
#if UNITY_EDITOR
        UnityEditor.EditorUtility.DisplayDialog(
            "Share Feature",
            "This feature runs natively on Android, iOS, or macOS.\nRun on an Android, iOS, or macOS player for full functionality.",
            "OK");
#elif UNITY_ANDROID
        NativeToolkitSampleNavigator.ShowAndroidShare(uiDocument);
#elif UNITY_IOS
        NativeToolkitSampleNavigator.ShowIosShare(uiDocument);
#elif UNITY_STANDALONE_OSX
        NativeToolkitSampleNavigator.ShowMacShare(uiDocument);
#endif
    }

    private void OnClipboardClicked()
    {
        Debug.Log($"[{LogTag}][{nameof(OnClipboardClicked)}]");
        if (uiDocument == null) return;
#if UNITY_EDITOR
        // Unlike the other three features, the clipboard sample screen is reachable in the Editor so
        // its wiring and the bridge-unavailable path can be exercised without a device. This
        // asymmetry is deliberate: the Android clipboard screen is already verified on device, and
        // routing by active build target here would add a branch this sample does not need.
        bool open = UnityEditor.EditorUtility.DisplayDialog(
            "Clipboard Feature",
            "This feature runs natively on Android or iOS.\n" +
            "Opening the sample screen in the Editor lets you check the layout; every operation will " +
            "report CLIPBOARD_BRIDGE_UNAVAILABLE.",
            "Open Sample Screen",
            "Close");
        if (!open) return;
        NativeToolkitSampleNavigator.ShowIosClipboard(uiDocument);
#elif UNITY_ANDROID
        NativeToolkitSampleNavigator.ShowAndroidClipboard(uiDocument);
#elif UNITY_IOS
        NativeToolkitSampleNavigator.ShowIosClipboard(uiDocument);
#endif
    }
}
#endif