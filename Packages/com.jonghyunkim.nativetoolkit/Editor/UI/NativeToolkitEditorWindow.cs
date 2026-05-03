#nullable enable

using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

public static class NativeToolkitEditorWindow
{
    private const string ImportedSampleScenePath = "Assets/Samples/Native Toolkit/1.0.0/Native Toolkit Example/NativeToolkitExampleScene.unity";
    private const string SampleRootObjectName = "NativeToolkitExample";

    private const string TopMenuUxmlPath = "Packages/com.jonghyunkim.nativetoolkit/Runtime/Resources/UI/Top/TopMenuExample.uxml";
    private const string TopMenuStylePath = "Packages/com.jonghyunkim.nativetoolkit/Runtime/Resources/UI/Top/TopMenuExampleStyle.uss";
    private const string CommonPanelSettingsPath = "Packages/com.jonghyunkim.nativetoolkit/Runtime/Resources/UI/Common/CommonPanelSettings.asset";

    [MenuItem("Tools/Native Toolkit/Sample")]
    public static void OpenSampleScene()
    {
        if (!File.Exists(ImportedSampleScenePath))
        {
            Debug.LogError($"[NativeToolkit] Sample scene not found: {ImportedSampleScenePath}");
            return;
        }

        var scene = EditorSceneManager.OpenScene(ImportedSampleScenePath);
        var sampleRoot = GameObject.Find(SampleRootObjectName);
        if (sampleRoot == null)
        {
            Debug.LogError($"[NativeToolkit] Sample root GameObject not found: {SampleRootObjectName}");
            return;
        }

        var visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(TopMenuUxmlPath);
        var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(TopMenuStylePath);
        var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(CommonPanelSettingsPath);

        if (visualTreeAsset == null || styleSheet == null || panelSettings == null)
        {
            Debug.LogError("[NativeToolkit] Failed to load one or more Top sample assets.");
            return;
        }

        var uiDocument = sampleRoot.GetComponent<UIDocument>();
        if (uiDocument == null)
        {
            uiDocument = sampleRoot.AddComponent<UIDocument>();
        }

        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(sampleRoot);
        RemoveExistingSampleControllers(sampleRoot);

        uiDocument.panelSettings = panelSettings;
        uiDocument.visualTreeAsset = visualTreeAsset;

        if (uiDocument.rootVisualElement != null)
        {
            uiDocument.rootVisualElement.styleSheets.Clear();
            uiDocument.rootVisualElement.styleSheets.Add(styleSheet);
        }

        var topController = sampleRoot.GetComponent<TopMenuExampleController>();
        if (topController == null)
        {
            topController = sampleRoot.AddComponent<TopMenuExampleController>();
        }

        var controllerSerializedObject = new SerializedObject(topController);
        controllerSerializedObject.FindProperty("uiDocument")!.objectReferenceValue = uiDocument;
        controllerSerializedObject.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(uiDocument);
        EditorUtility.SetDirty(topController);
        EditorUtility.SetDirty(sampleRoot);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Selection.activeGameObject = sampleRoot;
        EditorWindow.FocusWindowIfItsOpen<SceneView>();
    }

    private static void RemoveExistingSampleControllers(GameObject gameObject)
    {
        RemoveIfExists<AndroidDialogManagerExampleController>(gameObject);
        RemoveIfExists<IosDialogManagerExampleController>(gameObject);
        RemoveIfExists<MacDialogManagerExampleController>(gameObject);
        RemoveIfExists<WindowsDialogManagerExampleController>(gameObject);
        RemoveIfExists<AndroidNotificationManagerExampleController>(gameObject);
        RemoveIfExists<TopMenuExampleController>(gameObject);
    }

    private static void RemoveIfExists<T>(GameObject gameObject) where T : Component
    {
        var component = gameObject.GetComponent<T>();
        if (component != null)
        {
            Object.DestroyImmediate(component);
        }
    }
}