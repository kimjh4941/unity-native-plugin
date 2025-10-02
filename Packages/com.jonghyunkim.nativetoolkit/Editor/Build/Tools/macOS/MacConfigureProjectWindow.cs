using UnityEditor;
using UnityEngine;
using System.IO;
using System.Reflection;

/// <summary>
/// Editor window to configure and run the macOS framework patcher on an exported Xcode project.
/// </summary>
public class MacConfigureProjectWindow : EditorWindow
{
    private const string PrefKey = "NativeToolkit.macOSProjectPath";
    private const string InitSizeKey = "NativeToolkit.MacConfigureProjectWindow.InitSizeKey";
    private string xcodeProjectPath;

    [MenuItem("Tools/Native Toolkit/macOS/Configure Xcode Project")]
    public static void ShowWindow()
    {
        var window = GetWindow<MacConfigureProjectWindow>(true, "Configure Xcode Project (macOS)", true);
        window.minSize = new Vector2(600, 140);

        if (!EditorPrefs.GetBool(InitSizeKey, false))
        {
            var pos = window.position;
            pos.width = 600f;
            pos.height = 140f;
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
        xcodeProjectPath = EditorPrefs.GetString(PrefKey, string.Empty);
    }

    private void OnDisable()
    {
        EditorPrefs.SetString(PrefKey, xcodeProjectPath ?? string.Empty);
    }

    private void OnGUI()
    {
        GUILayout.Label("Configure exported macOS (Xcode) project path", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        xcodeProjectPath = EditorGUILayout.TextField("Xcode Root", xcodeProjectPath);
        if (GUILayout.Button("Browse", GUILayout.Width(80)))
        {
            string picked = EditorUtility.OpenFolderPanel("Select Xcode Project Root", string.IsNullOrEmpty(xcodeProjectPath) ? Application.dataPath : xcodeProjectPath, "");
            if (!string.IsNullOrEmpty(picked))
            {
                xcodeProjectPath = picked;
            }
        }
        EditorGUILayout.EndHorizontal();

        if (!string.IsNullOrEmpty(xcodeProjectPath))
        {
            var looksValid = File.Exists(Path.Combine(xcodeProjectPath, "Mac.xcodeproj", "project.pbxproj"));
            EditorGUILayout.HelpBox(looksValid ? "Looks like a valid exported Unity macOS project."
                                               : "Expected Mac.xcodeproj/project.pbxproj inside the selected folder.",
                                    looksValid ? MessageType.Info : MessageType.Error);
        }

        GUILayout.Space(8);
        using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(xcodeProjectPath)))
        {
            if (GUILayout.Button("Run: Add UnityMacNativeToolkit.xcframework"))
            {
                MacFrameworkPatcher.Apply(xcodeProjectPath);
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
