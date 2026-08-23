#nullable enable

#if UNITY_ANDROID || UNITY_IOS || UNITY_STANDALONE_OSX || UNITY_STANDALONE_WIN || UNITY_EDITOR
using UnityEngine;
using UnityEngine.UIElements;

public static class NativeToolkitSampleNavigator
{
    public static void ShowTopMenu(UIDocument uiDocument)
    {
        ApplyScreen<TopMenuExampleController>(
            uiDocument,
            "UI/Top/TopMenuExample",
            "UI/Top/TopMenuExampleStyle");
    }

    public static void ShowAndroidDialog(UIDocument uiDocument)
    {
#if UNITY_ANDROID || UNITY_EDITOR
        ApplyScreen<AndroidDialogManagerExampleController>(
            uiDocument,
            "UI/Android/Dialog/AndroidDialogManagerExample",
            "UI/Android/Dialog/AndroidDialogManagerExampleStyle");
#endif
    }

    public static void ShowAndroidNotification(UIDocument uiDocument)
    {
#if UNITY_ANDROID || UNITY_EDITOR
        ApplyScreen<AndroidNotificationManagerExampleController>(
            uiDocument,
            "UI/Android/Notification/AndroidNotificationManagerExample",
            "UI/Android/Notification/AndroidNotificationManagerExampleStyle");
#endif
    }

    public static void ShowAndroidShare(UIDocument uiDocument)
    {
#if UNITY_ANDROID || UNITY_EDITOR
        ApplyScreen<AndroidShareManagerExampleController>(
            uiDocument,
            "UI/Android/Share/AndroidShareManagerExample",
            "UI/Android/Share/AndroidShareManagerExampleStyle");
#endif
    }

    public static void ShowAndroidClipboard(UIDocument uiDocument)
    {
#if UNITY_ANDROID || UNITY_EDITOR
        ApplyScreen<AndroidClipboardManagerExampleController>(
            uiDocument,
            "UI/Android/Clipboard/AndroidClipboardManagerExample",
            "UI/Android/Clipboard/AndroidClipboardManagerExampleStyle");
#endif
    }

    public static void ShowIosDialog(UIDocument uiDocument)
    {
#if UNITY_IOS || UNITY_EDITOR
        ApplyScreen<IosDialogManagerExampleController>(
            uiDocument,
            "UI/iOS/Dialog/IosDialogManagerExample",
            "UI/iOS/Dialog/IosDialogManagerExampleStyle");
#endif
    }

    public static void ShowIosNotification(UIDocument uiDocument)
    {
#if UNITY_IOS || UNITY_EDITOR
        ApplyScreen<IosNotificationManagerExampleController>(
            uiDocument,
            "UI/iOS/Notification/IosNotificationManagerExample",
            "UI/iOS/Notification/IosNotificationManagerExampleStyle");
#endif
    }

    public static void ShowIosShare(UIDocument uiDocument)
    {
#if UNITY_IOS || UNITY_EDITOR
        ApplyScreen<IosShareManagerExampleController>(
            uiDocument,
            "UI/iOS/Share/IosShareManagerExample",
            "UI/iOS/Share/IosShareManagerExampleStyle");
#endif
    }

    /// <summary>
    /// Replaces the current screen with the iOS clipboard sample.
    /// </summary>
    /// <param name="uiDocument">UIDocument that hosts the sample screens.</param>
    public static void ShowIosClipboard(UIDocument uiDocument)
    {
#if UNITY_IOS || UNITY_EDITOR
        ApplyScreen<IosClipboardManagerExampleController>(
            uiDocument,
            "UI/iOS/Clipboard/IosClipboardManagerExample",
            "UI/iOS/Clipboard/IosClipboardManagerExampleStyle");
#endif
    }

    public static void ShowWindowsDialog(UIDocument uiDocument)
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR
        ApplyScreen<WindowsDialogManagerExampleController>(
            uiDocument,
            "UI/Windows/Dialog/WindowsDialogManagerExample",
            "UI/Windows/Dialog/WindowsDialogManagerExampleStyle");
#endif
    }

    public static void ShowWindowsNotification(UIDocument uiDocument)
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR
        ApplyScreen<WindowsNotificationManagerExampleController>(
            uiDocument,
            "UI/Windows/Notification/WindowsNotificationManagerExample",
            "UI/Windows/Notification/WindowsNotificationManagerExampleStyle");
#endif
    }

    public static void ShowMacDialog(UIDocument uiDocument)
    {
#if UNITY_STANDALONE_OSX || UNITY_EDITOR
        ApplyScreen<MacDialogManagerExampleController>(
            uiDocument,
            "UI/macOS/Dialog/MacDialogManagerExample",
            "UI/macOS/Dialog/MacDialogManagerExampleStyle");
#endif
    }

    public static void ShowMacNotification(UIDocument uiDocument)
    {
#if UNITY_STANDALONE_OSX || UNITY_EDITOR
        ApplyScreen<MacNotificationManagerExampleController>(
            uiDocument,
            "UI/macOS/Notification/MacNotificationManagerExample",
            "UI/macOS/Notification/MacNotificationManagerExampleStyle");
#endif
    }

    public static void ShowMacShare(UIDocument uiDocument)
    {
#if UNITY_STANDALONE_OSX || UNITY_EDITOR
        ApplyScreen<MacShareManagerExampleController>(
            uiDocument,
            "UI/macOS/Share/MacShareManagerExample",
            "UI/macOS/Share/MacShareManagerExampleStyle");
#endif
    }

    private static void ApplyScreen<TController>(UIDocument uiDocument, string visualTreeResourcePath, string styleResourcePath)
        where TController : MonoBehaviour
    {
        if (uiDocument == null)
        {
            Debug.LogError("[NativeToolkitSampleNavigator] UIDocument is null.");
            return;
        }

        var visualTreeAsset = Resources.Load<VisualTreeAsset>(visualTreeResourcePath);
        if (visualTreeAsset == null)
        {
            Debug.LogError($"[NativeToolkitSampleNavigator] VisualTreeAsset not found: {visualTreeResourcePath}");
            return;
        }

        RemoveExistingControllers(uiDocument.gameObject);
        uiDocument.visualTreeAsset = visualTreeAsset;

        var styleSheet = Resources.Load<StyleSheet>(styleResourcePath);
        if (styleSheet != null)
        {
            uiDocument.rootVisualElement.styleSheets.Clear();
            uiDocument.rootVisualElement.styleSheets.Add(styleSheet);
        }

        if (uiDocument.gameObject.GetComponent<TController>() == null)
        {
            uiDocument.gameObject.AddComponent<TController>();
        }
    }

    private static void RemoveExistingControllers(GameObject gameObject)
    {
        RemoveIfExists<TopMenuExampleController>(gameObject);
#if UNITY_ANDROID || UNITY_EDITOR
        RemoveIfExists<AndroidDialogManagerExampleController>(gameObject);
        RemoveIfExists<AndroidNotificationManagerExampleController>(gameObject);
        RemoveIfExists<AndroidShareManagerExampleController>(gameObject);
        RemoveIfExists<AndroidClipboardManagerExampleController>(gameObject);
#endif
#if UNITY_IOS || UNITY_EDITOR
        RemoveIfExists<IosDialogManagerExampleController>(gameObject);
        RemoveIfExists<IosNotificationManagerExampleController>(gameObject);
        RemoveIfExists<IosShareManagerExampleController>(gameObject);
        RemoveIfExists<IosClipboardManagerExampleController>(gameObject);
#endif
#if UNITY_STANDALONE_WIN || UNITY_EDITOR
        RemoveIfExists<WindowsDialogManagerExampleController>(gameObject);
        RemoveIfExists<WindowsNotificationManagerExampleController>(gameObject);
#endif
#if UNITY_STANDALONE_OSX || UNITY_EDITOR
        RemoveIfExists<MacDialogManagerExampleController>(gameObject);
        RemoveIfExists<MacNotificationManagerExampleController>(gameObject);
        RemoveIfExists<MacShareManagerExampleController>(gameObject);
#endif
    }

    private static void RemoveIfExists<T>(GameObject gameObject) where T : Component
    {
        var component = gameObject.GetComponent<T>();
        if (component != null)
        {
            Object.Destroy(component);
        }
    }
}
#endif