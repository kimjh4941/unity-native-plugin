#nullable enable

#if UNITY_ANDROID || UNITY_IOS || UNITY_EDITOR
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
#endif
#if UNITY_IOS || UNITY_EDITOR
        RemoveIfExists<IosDialogManagerExampleController>(gameObject);
        RemoveIfExists<IosNotificationManagerExampleController>(gameObject);
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