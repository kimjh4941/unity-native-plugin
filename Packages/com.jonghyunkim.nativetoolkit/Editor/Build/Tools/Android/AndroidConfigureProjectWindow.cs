using UnityEditor;
using UnityEngine;
using System.IO;
using System.Reflection;

/// <summary>
/// Editor window to configure and run the Android Gradle patcher that adds
/// Kotlin configurations to an exported Unity Android project.
/// </summary>
public class AndroidConfigureProjectWindow : EditorWindow
{
    private const string PrefKey = "NativeToolkit.AndroidProjectPath";
    private const string InitSizeKey = "NativeToolkit.AndroidConfigureProjectWindow.InitSizeKey";
    private string androidProjectPath;

    [MenuItem("Tools/Native Toolkit/Android/Configure Gradle Project")]
    public static void ShowWindow()
    {
        var window = GetWindow<AndroidConfigureProjectWindow>(true, "Configure Gradle Project", true);
        window.minSize = new Vector2(600, 140);

        // initialize window size only once (and center on main editor window)
        if (!EditorPrefs.GetBool(InitSizeKey, false))
        {
            var pos = window.position;
            pos.width = 600f;  // initial size (arbitrary)
            pos.height = 140f; // initial size (arbitrary)

            // center in main editor window
            var main = GetEditorMainWindowPosition();
            pos.x = Mathf.Round(main.x + (main.width - pos.width) * 0.5f);
            pos.y = Mathf.Round(main.y + (main.height - pos.height) * 0.5f);

            window.position = pos;
            EditorPrefs.SetBool(InitSizeKey, true);
        }
        window.Show();
    }

    private void OnEnable()
    {
        androidProjectPath = EditorPrefs.GetString(PrefKey, string.Empty);
    }

    private void OnDisable()
    {
        EditorPrefs.SetString(PrefKey, androidProjectPath ?? string.Empty);
    }

    private void OnGUI()
    {
        GUILayout.Label("Configure exported Android (Gradle) project path", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        androidProjectPath = EditorGUILayout.TextField("Project Root", androidProjectPath);
        if (GUILayout.Button("Browse", GUILayout.Width(80)))
        {
            string picked = EditorUtility.OpenFolderPanel("Select Android Project Root", string.IsNullOrEmpty(androidProjectPath) ? Application.dataPath : androidProjectPath, "");
            if (!string.IsNullOrEmpty(picked))
            {
                androidProjectPath = picked;
            }
        }
        EditorGUILayout.EndHorizontal();

        if (!string.IsNullOrEmpty(androidProjectPath))
        {
            var isValid = File.Exists(Path.Combine(androidProjectPath, "build.gradle"))
                        && File.Exists(Path.Combine(androidProjectPath, "launcher", "build.gradle"))
                        && File.Exists(Path.Combine(androidProjectPath, "unityLibrary", "build.gradle"));

            EditorGUILayout.HelpBox(isValid ? "Looks like a valid Unity Gradle project."
                                            : "Expected build.gradle, launcher/build.gradle, unityLibrary/build.gradle.",
                                    isValid ? MessageType.Info : MessageType.Error);
        }

        GUILayout.Space(8);
        using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(androidProjectPath)))
        {
            if (GUILayout.Button("Run: Add Kotlin Dependencies"))
            {
                AndroidGradlePatcher.Apply(androidProjectPath);
            }
        }
    }

    // Helper: get Unity editor main window rect via reflection
    private static Rect GetEditorMainWindowPosition()
    {
        var containerWinType = typeof(Editor).Assembly.GetType("UnityEditor.ContainerWindow");
        if (containerWinType == null) return new Rect(0, 0, Screen.currentResolution.width, Screen.currentResolution.height);

        var showModeField = containerWinType.GetField("m_ShowMode", BindingFlags.NonPublic | BindingFlags.Instance);
        var positionProp = containerWinType.GetProperty("position", BindingFlags.Public | BindingFlags.Instance);
        var windows = Resources.FindObjectsOfTypeAll(containerWinType);

        foreach (var win in windows)
        {
            var showMode = (int)showModeField.GetValue(win);
            // 4 == main editor window
            if (showMode == 4)
            {
                return (Rect)positionProp.GetValue(win, null);
            }
        }
        return new Rect(0, 0, Screen.currentResolution.width, Screen.currentResolution.height);
    }
}
