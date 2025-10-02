#nullable enable

using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEngine.Localization.Settings;
using System.Collections.Generic;
using UnityEditor.SceneManagement;
using System.Linq;
using UnityEditor.Localization;

/// <summary>
/// Editor window that showcases Native Toolkit sample UI documents and allows applying
/// platform‑specific UXML / USS plus controller components into the sample scene.
/// Provides a localized tree view (driven by the <c>NativeToolkit</c> string table) and
/// contextual actions for previewing runtime layouts inside the editor.
/// </summary>
/// <remarks>
/// Responsibilities:
/// 1. Ensure localization system is initialized and select the Project locale.
/// 2. Build (or clone) the root UI, including a manually constructed TwoPaneSplitView fallback.
/// 3. Populate and bind a TreeView of sample items (folders / files) without relying on asset GUIDs.
/// 4. On selection, show either an informational inspector or file inspector with actions.
/// 5. On file open, load/ensure the sample scene, apply UXML, PanelSettings, stylesheet, and controller.
/// 6. Maintain separation between editor logic and runtime sample controllers by using reflection when needed.
/// </remarks>
public class NativeToolkitEditorWindow : EditorWindow
{
    // Package-rooted paths only
    private const string PackageRoot = "Packages/com.jonghyunkim.nativetoolkit";
    private const string RuntimeUIRoot = PackageRoot + "/Runtime/Resources/UI";
    private const string NativeToolkitExampleScenePath = "Assets/Samples/Native Toolkit/1.0.0/Native Toolkit Example/NativeToolkitExampleScene.unity";

    [SerializeField] private VisualTreeAsset? mainDocument;
    [SerializeField] private VisualTreeAsset? itemTemplate;
    [SerializeField] private StyleSheet? styleSheet;

    private TreeView? treeView;

    private System.Action? _fileOpenHandler;

    [MenuItem("Tools/Native Toolkit/Example")]
    public static void ShowWindow()
    {
        var window = GetWindow<NativeToolkitEditorWindow>();
        window.titleContent = new GUIContent("Native Toolkit");
        void ApplyTitle()
        {
            var title = LocalizationUtil.L("NativeToolkit", "window.title");
            window.titleContent = new GUIContent(string.IsNullOrEmpty(title) ? "Native Toolkit" : title);
        }

        if (!LocalizationSettings.InitializationOperation.IsDone)
        {
            Debug.Log("[Localization] Waiting for Initialization...");
            LocalizationSettings.InitializationOperation.Completed += _ =>
            {
                window.EnsureSelectedLocale();
                ApplyTitle();
            };
        }
        else
        {
            Debug.Log("[Localization] Initialization completed");
            window.EnsureSelectedLocale();
            ApplyTitle();
        }

        window.Show();
    }

    /// <summary>
    /// Ensures <see cref="LocalizationSettings.SelectedLocale"/> matches the Project Locale Identifier
    /// configured in Project Settings. Falls back to first available (preferring English) when the
    /// project locale is not explicitly set.
    /// </summary>
    private void EnsureSelectedLocale()
    {
        try
        {
            // Locale corresponding to the Project Settings' Project Locale Identifier
            var projectLocale = LocalizationSettings.ProjectLocale; // Default locale defined in Project Settings
            if (projectLocale != null)
            {
                if (LocalizationSettings.SelectedLocale != projectLocale)
                {
                    LocalizationSettings.SelectedLocale = projectLocale;
                    Debug.Log($"[Localization] SelectedLocale set from ProjectLocale: {projectLocale.Identifier.Code}");
                }
            }
            else
            {
                Debug.LogWarning("[Localization] ProjectLocale is not set.");
                // Fallback: choose first available locale, preferring English variants
                var avail = LocalizationSettings.AvailableLocales?.Locales;
                if (avail != null && avail.Count > 0)
                {
                    var en = avail.FirstOrDefault(l => l.Identifier.Code == "en" || l.Identifier.Code.StartsWith("en-"));
                    LocalizationSettings.SelectedLocale = en ?? avail[0];
                    Debug.Log($"[Localization] SelectedLocale fallback: {LocalizationSettings.SelectedLocale.Identifier.Code}");
                }
                else
                {
                    Debug.LogWarning("[Localization] No available locales.");
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[Localization] EnsureSelectedLocale failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Unity callback invoked when the window's visual tree should be created / rebuilt.
    /// Loads assets, constructs split view (code fallback), then initializes and populates the TreeView.
    /// </summary>
    public void CreateGUI()
    {
        // Load assets from Resources or assign directly in inspector
        LoadAssets();

        // Create the root visual element
        if (mainDocument != null)
        {
            mainDocument.CloneTree(rootVisualElement);
        }
        else
        {
            CreateDefaultUI();
        }

        // Apply styles
        if (styleSheet != null)
        {
            rootVisualElement.styleSheets.Add(styleSheet);
        }

        // Build split view in code (fallback when UXML TwoPaneSplitView not available)
        var host = rootVisualElement.Q<VisualElement>("split-host");
        var left = rootVisualElement.Q<VisualElement>("left-pane");
        var right = rootVisualElement.Q<VisualElement>("right-pane");
        if (host != null && left != null && right != null)
        {
            const string PrefKey = "NativeToolkitEditorWindow.Split.LeftWidth";
            float initial = EditorPrefs.GetFloat(PrefKey, 280f);

            var split = new TwoPaneSplitView(0, initial, TwoPaneSplitViewOrientation.Horizontal)
            {
                name = "main-split"
            };

            // Re-parent existing children under the TwoPaneSplitView
            host.Clear();
            host.Add(split);
            split.Add(left);
            split.Add(right);

            // Persist left pane width after drag
            left.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                EditorPrefs.SetFloat(PrefKey, left.resolvedStyle.width);
            });
        }

        InitializeTreeView();
        PopulateTreeView();
        SetupTreeViewEvents();
        SetItemInspector();
    }

    /// <summary>
    /// Loads UXML / template / stylesheet assets from Resources if they are not assigned via inspector.
    /// </summary>
    private void LoadAssets()
    {
        // Load assets from Resources folder if not assigned
        if (mainDocument == null)
        {
            mainDocument = Resources.Load<VisualTreeAsset>("UI/NativeToolkitTreeView");
        }

        if (itemTemplate == null)
        {
            itemTemplate = Resources.Load<VisualTreeAsset>("UI/NativeToolkitTreeViewItem");
        }

        if (styleSheet == null)
        {
            styleSheet = Resources.Load<StyleSheet>("UI/NativeToolkitTreeViewStyles");
        }
    }

    /// <summary>
    /// Creates a minimal programmatic UI when the authored UXML document is not available.
    /// </summary>
    private void CreateDefaultUI()
    {
        // Create UI programmatically if UXML is not available
        var container = new VisualElement();
        container.name = "main-container";
        container.style.flexGrow = 1;
        container.style.paddingLeft = 10;
        container.style.paddingRight = 10;
        container.style.paddingTop = 10;
        container.style.paddingBottom = 10;

        var header = new Label(LocalizationUtil.L("NativeToolkit", "header.title"));
        header.name = "header-label";
        header.style.fontSize = 18;
        header.style.marginBottom = 10;
        header.style.color = Color.white;

        var treeViewElement = new TreeView();
        treeViewElement.name = "example-tree";
        treeViewElement.style.flexGrow = 1;
        treeViewElement.style.borderLeftWidth = 1;
        treeViewElement.style.borderRightWidth = 1;
        treeViewElement.style.borderTopWidth = 1;
        treeViewElement.style.borderBottomWidth = 1;
        treeViewElement.style.borderLeftColor = Color.gray;
        treeViewElement.style.borderRightColor = Color.gray;
        treeViewElement.style.borderTopColor = Color.gray;
        treeViewElement.style.borderBottomColor = Color.gray;

        container.Add(header);
        container.Add(treeViewElement);
        rootVisualElement.Add(container);
    }

    /// <summary>
    /// Locates and configures the TreeView element: sizing, focus, selection style classes.
    /// </summary>
    private void InitializeTreeView()
    {
        treeView = rootVisualElement.Q<TreeView>("example-tree");

        if (treeView == null)
        {
            Debug.LogError("TreeView not found in UI Document!");
            return;
        }

        // Row height similar to Project window styling
        treeView.fixedItemHeight = EditorGUIUtility.singleLineHeight + 4f;

        treeView.focusable = true;
        treeView.pickingMode = PickingMode.Position;
        treeView.selectionType = SelectionType.Single;

        // Apply default theme classes (selection / hover etc.)
        treeView.AddToClassList("unity-tree-view");
    }

    /// <summary>
    /// Populates the TreeView with sample hierarchical data and assigns make/bind handlers.
    /// </summary>
    private void PopulateTreeView()
    {
        if (treeView == null) return;

        // Build sample data model
        var rootData = CreateSampleData();

        // Convert to TreeViewItemData directly
        var treeViewItems = new List<TreeViewItemData<TreeItemData>>();
        foreach (var rootItem in rootData)
        {
            var treeViewItem = CreateTreeViewItemData(rootItem);
            treeViewItems.Add(treeViewItem);
        }

        // Assign data to TreeView
        treeView.SetRootItems(treeViewItems);

        // Configure item factory & bind handlers
        treeView.makeItem = MakeTreeViewItem;
        treeView.bindItem = BindTreeViewItem;

        // Rebuild TreeView
        treeView.Rebuild();
    }

    /// <summary>
    /// Recursively converts a <see cref="TreeItemData"/> hierarchy into <see cref="TreeViewItemData{T}"/>.
    /// </summary>
    private TreeViewItemData<TreeItemData> CreateTreeViewItemData(TreeItemData data)
    {
        var children = new List<TreeViewItemData<TreeItemData>>();

        // Recursively process children if present
        if (data.children != null && data.children.Count > 0)
        {
            foreach (var child in data.children)
            {
                children.Add(CreateTreeViewItemData(child));
            }
        }

        return new TreeViewItemData<TreeItemData>(data.id, data, children);
    }

    /// <summary>
    /// Factory method for TreeView rows. Instantiates a template (if provided) or builds a fallback element.
    /// </summary>
    private VisualElement MakeTreeViewItem()
    {
        if (itemTemplate != null)
        {
            // Use UI Builder template
            var itemElement = itemTemplate.Instantiate();
            var container = itemElement.Q<VisualElement>("tree-item-container");
            return container ?? itemElement;
        }
        else
        {
            // Create programmatically
            return CreateTreeItemElement();
        }
    }

    /// <summary>
    /// Creates a fallback TreeView item visual element (icon + labels) mimicking Project window styling.
    /// </summary>
    private VisualElement CreateTreeItemElement()
    {
        var container = new VisualElement();
        container.name = "tree-item-container";
        container.style.flexDirection = FlexDirection.Row;
        container.style.alignItems = Align.Center;
        container.style.paddingLeft = 4;
        container.style.paddingRight = 4;
        container.style.minHeight = EditorGUIUtility.singleLineHeight + 4f;

        // Apply Project window-like selection styling
        container.AddToClassList("unity-collection-view__item");

        var icon = new VisualElement();
        icon.name = "item-icon";
        icon.style.width = 16;
        icon.style.height = 16;
        icon.style.marginRight = 4;
        icon.style.backgroundColor = StyleKeyword.Null;
        icon.AddToClassList("unity-image");

        var label = new Label();
        label.name = "item-label";
        label.style.flexGrow = 1;
        label.style.color = StyleKeyword.Null;
        label.AddToClassList("unity-label");

        var description = new Label();
        description.name = "item-description";
        description.style.color = StyleKeyword.Null;
        description.style.fontSize = StyleKeyword.Null;
        description.style.marginLeft = 8;

        container.Add(icon);
        container.Add(label);
        container.Add(description);

        return container;
    }

    /// <summary>
    /// Binds data (name / description / icon) to an instantiated TreeView row element.
    /// </summary>
    /// <param name="element">Row root element.</param>
    /// <param name="index">Data index provided by TreeView.</param>
    private void BindTreeViewItem(VisualElement element, int index)
    {
        var item = treeView?.GetItemDataForIndex<TreeItemData>(index);
        if (item == null) return;

        var label = element.Q<Label>("item-label");
        var description = element.Q<Label>("item-description");
        var icon = element.Q<VisualElement>("item-icon");

        if (label != null)
        {
            label.text = item.name;
            // Apply default editor font
            var f = EditorStyles.label?.font;
            if (f != null) label.style.unityFontDefinition = FontDefinition.FromFont(f);
        }

        if (description != null)
        {
            description.text = item.description;
            description.style.display = string.IsNullOrEmpty(item.description) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        if (icon != null)
        {
            // Switch folder icon based on expansion state
            bool isOpenFolder = item.isFolder && treeView != null &&
                                (treeView.GetType().GetMethod("IsExpanded") != null
                                    ? (bool)treeView.GetType().GetMethod("IsExpanded")!.Invoke(treeView, new object[] { item.id })!
                                    : (treeView.GetType().GetMethod("IsExpanded") != null
                                        ? (bool)treeView.GetType().GetMethod("IsExpanded")!.Invoke(treeView, new object[] { item.id })!
                                        : false));

            Texture2D? tex = item.isFolder ? GetFolderIcon(isOpenFolder) : GetDefaultFileIcon();
            if (tex != null)
            {
                icon.style.backgroundImage = new StyleBackground(tex);
                // ScaleMode.ScaleToFit is not available for icon.style.backgroundImage; ensure icon size is set appropriately
                icon.style.backgroundColor = StyleKeyword.Null;
            }
            else
            {
                icon.style.backgroundImage = default;
                icon.style.backgroundColor = StyleKeyword.Null;
            }
        }
    }

    // Built-in icon helpers
    /// <summary>
    /// Returns built-in folder icon (open/closed) honoring editor skin.
    /// </summary>
    private static Texture2D? GetFolderIcon(bool opened = false)
    {
        string name = EditorGUIUtility.isProSkin
            ? (opened ? "d_FolderOpened Icon" : "d_Folder Icon")
            : (opened ? "FolderOpened Icon" : "Folder Icon");
        return EditorGUIUtility.IconContent(name).image as Texture2D;
    }

    /// <summary>
    /// Returns built-in default asset icon honoring editor skin.
    /// </summary>
    private static Texture2D? GetDefaultFileIcon()
    {
        var name = EditorGUIUtility.isProSkin ? "d_DefaultAsset Icon" : "DefaultAsset Icon";
        return EditorGUIUtility.IconContent(name).image as Texture2D;
    }

    /// <summary>
    /// Constructs in-memory sample data representing platform categories and dialog manager files.
    /// </summary>
    private List<TreeItemData> CreateSampleData()
    {
        var data = new List<TreeItemData>();

        // Root item 1 (Android)
        var folder1 = new TreeItemData(1, 1, true, "Android");
        var subfolder1 = new TreeItemData(2, 2, true, "Dialog");
        subfolder1.children.Add(new TreeItemData(3, 3, false, "AndroidDialogManager.cs"));
        folder1.children.Add(subfolder1);

        // Root item 2 (iOS)
        var folder2 = new TreeItemData(4, 1, true, "iOS");
        var subfolder2 = new TreeItemData(5, 2, true, "Dialog");
        subfolder2.children.Add(new TreeItemData(6, 3, false, "IosDialogManager.cs"));
        folder2.children.Add(subfolder2);

        var folder3 = new TreeItemData(7, 1, true, "macOS");
        var subfolder3 = new TreeItemData(8, 2, true, "Dialog");
        subfolder3.children.Add(new TreeItemData(9, 3, false, "MacDialogManager.cs"));
        folder3.children.Add(subfolder3);

        var folder4 = new TreeItemData(10, 1, true, "Windows");
        var subfolder4 = new TreeItemData(11, 2, true, "Dialog");
        subfolder4.children.Add(new TreeItemData(12, 3, false, "WindowsDialogManager.cs"));
        folder4.children.Add(subfolder4);

        data.Add(folder1);
        data.Add(folder2);
        data.Add(folder3);
        data.Add(folder4);
        return data;
    }

    /// <summary>
    /// Wires selection, double-click, and context menu events for the TreeView.
    /// </summary>
    private void SetupTreeViewEvents()
    {
        if (treeView == null) return;

        // Selection changed event
        treeView.selectionChanged += OnTreeViewSelectionChanged;

        // Double-click (items chosen) event
        treeView.itemsChosen += OnTreeViewItemsChosen;

        // Right-click contextual menu
        treeView.RegisterCallback<ContextualMenuPopulateEvent>(OnContextualMenu);
    }

    /// <summary>
    /// Updates inspector panels depending on the current selection (folder info vs file inspector).
    /// </summary>
    private void SetItemInspector(TreeItemData? item = null)
    {
        var itemInspector = rootVisualElement.Q<VisualElement>("item-inspector");
        var infoInspector = rootVisualElement.Q<VisualElement>("info-inspector");

        // Error handling if required inspector elements are missing
        if (itemInspector == null || infoInspector == null)
        {
            Debug.LogError("[Editor] Inspector elements not found in UI");
            return;
        }

        // Nothing selected: show info panel
        if (item == null)
        {
            infoInspector.visible = true;
            itemInspector.visible = false;
            return;
        }

        // Folder selected: show info inspector
        if (item != null && item.isFolder)
        {
            Debug.Log($"[Editor] Displaying info for folder: {item.name}");
            infoInspector.visible = true;
            itemInspector.visible = false;

            infoInspector.Q<Label>("message").text = LocalizationUtil.L("NativeToolkit", "folder.template").Replace("{name}", item.name);
            return;
        }

        // Folder selected with no children
        if (item != null && item.isFolder && item.children.Count == 0)
        {
            Debug.Log($"[Editor] Displaying info for folder: {item.name}");
            infoInspector.visible = true;
            itemInspector.visible = false;

            infoInspector.Q<Label>("message").text = LocalizationUtil.L("NativeToolkit", "folder.empty");
            return;
        }

        // File item selected
        if (item != null && !item.isFolder)
        {
            Debug.Log($"[Editor] Displaying inspector for item: {item.name}");
            infoInspector.visible = false;
            itemInspector.visible = true;
        }

        // When sample scene loaded, expose file metadata & open actions
        var fileName = itemInspector.Q<Label>("file-name");
        var fileOpen = itemInspector.Q<Button>("file-open");
        if (fileName != null)
        {
            fileName.text = item?.name;
        }

        if (fileOpen != null)
        {
            // Lambda creates a new delegate instance each time; store reference to properly unsubscribe before re-subscribing
            if (_fileOpenHandler != null)
            {
                fileOpen.clicked -= _fileOpenHandler;
            }
            _fileOpenHandler = () => OnFileOpenClicked(item);
            fileOpen.clicked += _fileOpenHandler;
        }
    }

    /// <summary>
    /// Handles the file open action: ensures scene is loaded, applies UXML, PanelSettings, stylesheet,
    /// attaches the appropriate platform controller, and marks the scene dirty for saving.
    /// </summary>
    private void OnFileOpenClicked(TreeItemData? item)
    {
        Debug.Log($"[Editor] Open file: {item?.name}");

        try
        {
            // Load sample scene if it's not already the active scene
            if (!EditorSceneManager.GetActiveScene().path.Equals(NativeToolkitExampleScenePath))
            {
                if (System.IO.File.Exists(NativeToolkitExampleScenePath))
                {
                    EditorSceneManager.OpenScene(NativeToolkitExampleScenePath);
                    Debug.Log($"[Editor] Loaded scene: {NativeToolkitExampleScenePath}");
                }
                else
                {
                    Debug.LogError($"[Editor] Scene not found at path: {NativeToolkitExampleScenePath}");
                    return;
                }
            }

            // Resolve UXML file path dynamically based on selected item
            var uxmlPath = GetUXMLPathForItem(item);
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);

            if (uxml == null)
            {
                Debug.LogError($"[Editor] UXML not found at path: {uxmlPath}");
                return;
            }

            Debug.Log($"[Editor] UXML found at path: {uxmlPath}");

            // Find root UI GameObject
            var gameObject = FindUIGameObject("NativeToolkitExample");
            if (gameObject == null)
            {
                Debug.LogError($"[Editor] Failed to find GameObject");
                return;
            }

            // Get or add UIDocument component
            var uiDocument = gameObject.GetComponent<UIDocument>();
            if (uiDocument == null)
            {
                uiDocument = gameObject.AddComponent<UIDocument>();
                Debug.Log($"[Editor] Added UIDocument component to {gameObject.name}");
            }

            // Apply UXML to UIDocument
            uiDocument.visualTreeAsset = uxml;

            // Configure PanelSettings if available
            var panelSettings = GetPanelSettings(item);
            if (panelSettings != null)
            {
                uiDocument.panelSettings = panelSettings;
                Debug.Log($"[Editor] Applied PanelSettings: {panelSettings.name}");
            }

            // Apply stylesheet if present
            ApplyStyleSheetIfExists(uiDocument, item);

            // Add platform-specific controller automatically
            AddControllerComponent(gameObject, item);

            Debug.Log($"[Editor] Successfully applied UXML to GameObject: {gameObject.name}");

            // Mark scene dirty and save
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            SaveNativeToolkitExampleScene(prompt: false);

            // Select GameObject to expose in Inspector
            Selection.activeGameObject = gameObject;

            // Focus Scene View
            EditorApplication.ExecuteMenuItem("Window/General/Scene");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[Editor] Error applying UXML: {ex.Message}");
        }
    }

    /// <summary>
    /// Adds a platform‑specific example controller component based on the selected file name.
    /// </summary>
    private void AddControllerComponent(GameObject gameObject, TreeItemData? item)
    {
        switch (item?.name)
        {
            case "AndroidDialogManager.cs":
                AddAndroidDialogManagerExampleController(gameObject);
                break;

            case "IosDialogManager.cs":
                AddIosDialogManagerExampleController(gameObject);
                break;

            case "MacDialogManager.cs":
                AddMacDialogManagerExampleController(gameObject);
                break;

            case "WindowsDialogManager.cs":
                AddWindowsDialogManagerExampleController(gameObject);
                break;

            default:
                Debug.LogWarning($"[Editor] No controller available for: {item?.name}");
                break;
        }
    }

    /// <summary>
    /// Sets a UIDocument reference on a controller component by attempting public property first,
    /// then falling back to a private field via reflection (editor convenience, not for runtime builds).
    /// </summary>
    private void SetControllerUIDocument<T>(T controller, UIDocument uiDocument) where T : MonoBehaviour
    {
        // Attempt to set via public property first
        var propertyInfo = typeof(T).GetProperty("UIDocument");
        if (propertyInfo != null && propertyInfo.CanWrite)
        {
            propertyInfo.SetValue(controller, uiDocument);
            Debug.Log($"[Editor] Set UIDocument reference in {typeof(T).Name} via property");
            return;
        }

        // Fallback: set private field via reflection
        var fieldInfo = typeof(T).GetField("uiDocument",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (fieldInfo != null)
        {
            fieldInfo.SetValue(controller, uiDocument);
            Debug.Log($"[Editor] Set UIDocument reference in {typeof(T).Name} via field");
        }
        else
        {
            Debug.LogWarning($"[Editor] Could not find uiDocument field or UIDocument property in {typeof(T).Name}");
        }
    }

    /// <summary>
    /// Ensures only the Android example controller exists and wires its UIDocument.
    /// </summary>
    private void AddAndroidDialogManagerExampleController(GameObject gameObject)
    {
        // Remove existing controllers (avoid duplicates)
        RemoveExistingControllers(gameObject);

        // Add AndroidDialogManagerExampleController
        var controller = gameObject.GetComponent<AndroidDialogManagerExampleController>();
        if (controller == null)
        {
            controller = gameObject.AddComponent<AndroidDialogManagerExampleController>();
            Debug.Log($"[Editor] Added AndroidDialogManagerExampleController to {gameObject.name}");
        }
        else
        {
            Debug.Log($"[Editor] AndroidDialogManagerExampleController already exists on {gameObject.name}");
        }

        // Wire UIDocument reference
        var uiDocument = gameObject.GetComponent<UIDocument>();
        if (uiDocument != null && controller != null)
        {
            SetControllerUIDocument(controller, uiDocument);
        }
    }

    /// <summary>
    /// Ensures only the iOS example controller exists and wires its UIDocument.
    /// </summary>
    private void AddIosDialogManagerExampleController(GameObject gameObject)
    {
        // Remove existing controllers
        RemoveExistingControllers(gameObject);

        var controller = gameObject.GetComponent<IosDialogManagerExampleController>();
        if (controller == null)
        {
            controller = gameObject.AddComponent<IosDialogManagerExampleController>();
            Debug.Log($"[Editor] Added IosDialogManagerExampleController to {gameObject.name}");
        }
        else
        {
            Debug.Log($"[Editor] IosDialogManagerExampleController already exists on {gameObject.name}");
        }

        // Wire UIDocument reference
        var uiDocument = gameObject.GetComponent<UIDocument>();
        if (uiDocument != null && controller != null)
        {
            SetControllerUIDocument(controller, uiDocument);
        }
    }

    /// <summary>
    /// Ensures only the macOS example controller exists and wires its UIDocument.
    /// </summary>
    private void AddMacDialogManagerExampleController(GameObject gameObject)
    {
        // Remove existing controllers
        RemoveExistingControllers(gameObject);

        Debug.Log($"[Editor] macOS Dialog Manager Example Controller not implemented yet for {gameObject.name}");

        var controller = gameObject.GetComponent<MacDialogManagerExampleController>();
        if (controller == null)
        {
            controller = gameObject.AddComponent<MacDialogManagerExampleController>();
            Debug.Log($"[Editor] Added MacDialogManagerExampleController to {gameObject.name}");
        }
        else
        {
            Debug.Log($"[Editor] MacDialogManagerExampleController already exists on {gameObject.name}");
        }

        // Wire UIDocument reference
        var uiDocument = gameObject.GetComponent<UIDocument>();
        if (uiDocument != null && controller != null)
        {
            SetControllerUIDocument(controller, uiDocument);
        }
    }

    /// <summary>
    /// Ensures only the Windows example controller exists and wires its UIDocument.
    /// </summary>
    private void AddWindowsDialogManagerExampleController(GameObject gameObject)
    {
        // Remove existing controllers
        RemoveExistingControllers(gameObject);

        Debug.Log($"[Editor] Windows Dialog Manager Example Controller not implemented yet for {gameObject.name}");

        var controller = gameObject.GetComponent<WindowsDialogManagerExampleController>();
        if (controller == null)
        {
            controller = gameObject.AddComponent<WindowsDialogManagerExampleController>();
            Debug.Log($"[Editor] Added WindowsDialogManagerExampleController to {gameObject.name}");
        }
        else
        {
            Debug.Log($"[Editor] WindowsDialogManagerExampleController already exists on {gameObject.name}");
        }

        // Wire UIDocument reference
        var uiDocument = gameObject.GetComponent<UIDocument>();
        if (uiDocument != null && controller != null)
        {
            SetControllerUIDocument(controller, uiDocument);
        }
    }

    /// <summary>
    /// Removes any existing platform dialog controllers to avoid conflicting components.
    /// </summary>
    private void RemoveExistingControllers(GameObject gameObject)
    {
        // Remove any existing dialog controllers (prevent cross-platform conflicts)
        var androidController = gameObject.GetComponent<AndroidDialogManagerExampleController>();
        if (androidController != null)
        {
            DestroyImmediate(androidController);
            Debug.Log($"[Editor] Removed existing AndroidDialogManagerExampleController from {gameObject.name}");
        }

        var iosController = gameObject.GetComponent<IosDialogManagerExampleController>();
        if (iosController != null)
        {
            DestroyImmediate(iosController);
            Debug.Log($"[Editor] Removed existing IosDialogManagerExampleController from {gameObject.name}");
        }

        var macController = gameObject.GetComponent<MacDialogManagerExampleController>();
        if (macController != null)
        {
            DestroyImmediate(macController);
            Debug.Log($"[Editor] Removed existing MacDialogManagerExampleController from {gameObject.name}");
        }

        var windowsController = gameObject.GetComponent<WindowsDialogManagerExampleController>();
        if (windowsController != null)
        {
            DestroyImmediate(windowsController);
            Debug.Log($"[Editor] Removed existing WindowsDialogManagerExampleController from {gameObject.name}");
        }
    }

    /// <summary>
    /// Retrieves a dedicated PanelSettings asset for a file or falls back to a common/shared asset.
    /// </summary>
    private PanelSettings? GetPanelSettings(TreeItemData? item)
    {
        // Package-only dedicated PanelSettings
        var panelSettingsPath = GetPanelSettingsPathForItem(item);
        var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(panelSettingsPath);
        if (panelSettings != null)
        {
            Debug.Log($"[Editor] Found PanelSettings: {panelSettingsPath}");
            return panelSettings;
        }

        // Package-only common fallback inside package
        var commonPanelSettingsPath = $"{RuntimeUIRoot}/Common/CommonPanelSettings.asset";
        var commonPanelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(commonPanelSettingsPath);
        if (commonPanelSettings != null)
        {
            Debug.Log($"[Editor] Using common PanelSettings: {commonPanelSettingsPath}");
            return commonPanelSettings;
        }

        Debug.LogError($"[Editor] PanelSettings not found at path: {panelSettingsPath} or {commonPanelSettingsPath}");
        return null;
    }

    /// <summary>
    /// Resolves a PanelSettings asset path based on the selected file name.
    /// </summary>
    private string GetPanelSettingsPathForItem(TreeItemData? item)
    {
        // Package-only PanelSettings paths
        switch (item?.name)
        {
            case "AndroidDialogManager.cs":
                return $"{RuntimeUIRoot}/Android/AndroidDialogPanelSettings.asset";
            case "IosDialogManager.cs":
                return $"{RuntimeUIRoot}/iOS/IosDialogPanelSettings.asset";
            case "MacDialogManager.cs":
                return $"{RuntimeUIRoot}/macOS/MacDialogPanelSettings.asset";
            case "WindowsDialogManager.cs":
                return $"{RuntimeUIRoot}/Windows/WindowsDialogPanelSettings.asset";
            default:
                return $"{RuntimeUIRoot}/Common/DefaultPanelSettings.asset";
        }
    }

    /// <summary>
    /// Resolves a UXML path for a selected item; returns a default path if not matched.
    /// </summary>
    private string GetUXMLPathForItem(TreeItemData? item)
    {
        // Package-only UXML paths
        switch (item?.name)
        {
            case "AndroidDialogManager.cs":
                return $"{RuntimeUIRoot}/Android/Dialog/AndroidDialogManagerExample.uxml";
            case "IosDialogManager.cs":
                return $"{RuntimeUIRoot}/iOS/Dialog/IosDialogManagerExample.uxml";
            case "MacDialogManager.cs":
                return $"{RuntimeUIRoot}/macOS/Dialog/MacDialogManagerExample.uxml";
            case "WindowsDialogManager.cs":
                return $"{RuntimeUIRoot}/Windows/Dialog/WindowsDialogManagerExample.uxml";
            default:
                return $"{RuntimeUIRoot}/Default/DefaultExample.uxml";
        }
    }

    /// <summary>
    /// Finds an existing GameObject in the active scene by name (does not create a new one).
    /// </summary>
    private GameObject? FindUIGameObject(string objectName)
    {
        // Search for existing GameObject
        var existingObject = GameObject.Find(objectName);
        if (existingObject != null)
        {
            Debug.Log($"[Editor] Found existing GameObject: {objectName}");
            return existingObject;
        }
        return null;
    }

    /// <summary>
    /// Clears existing style sheets from the UIDocument root and applies a platform‑specific one if present.
    /// </summary>
    private void ApplyStyleSheetIfExists(UIDocument uiDocument, TreeItemData? item)
    {
        // Get corresponding stylesheet path
        var stylePath = GetStyleSheetPathForItem(item);
        var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(stylePath);

        if (styleSheet != null)
        {
            // Clear existing stylesheets before applying platform-specific one
            uiDocument.rootVisualElement.styleSheets.Clear();
            uiDocument.rootVisualElement.styleSheets.Add(styleSheet);
            Debug.Log($"[Editor] Applied stylesheet: {stylePath}");
        }
        else
        {
            Debug.LogWarning($"[Editor] Stylesheet not found at path: {stylePath}");
        }
    }

    /// <summary>
    /// Resolves a USS stylesheet path for a selected item; returns a common stylesheet by default.
    /// </summary>
    private string GetStyleSheetPathForItem(TreeItemData? item)
    {
        // Package-only USS paths
        switch (item?.name)
        {
            case "AndroidDialogManager.cs":
                return $"{RuntimeUIRoot}/Android/Dialog/AndroidDialogManagerExampleStyle.uss";
            case "IosDialogManager.cs":
                return $"{RuntimeUIRoot}/iOS/Dialog/IosDialogManagerExampleStyle.uss";
            case "MacDialogManager.cs":
                return $"{RuntimeUIRoot}/macOS/Dialog/MacDialogManagerExampleStyle.uss";
            case "WindowsDialogManager.cs":
                return $"{RuntimeUIRoot}/Windows/Dialog/WindowsDialogManagerExampleStyle.uss";
            default:
                return $"{RuntimeUIRoot}/Common/CommonStyles.uss";
        }
    }

    /// <summary>
    /// Saves the active scene if it matches the example scene path and is dirty.
    /// </summary>
    private static bool SaveNativeToolkitExampleScene(bool prompt = true)
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.path.Equals(NativeToolkitExampleScenePath))
            return false;

        if (!scene.isDirty)
            return true;

        if (prompt)
        {
            var ok = EditorUtility.DisplayDialog("Save NativeToolkitExampleScene",
                "Save changes to NativeToolkitExampleScene.unity?", "Save", "Don't Save");
            if (!ok) return false;
        }

        var saved = EditorSceneManager.SaveScene(scene);
        if (saved)
        {
            AssetDatabase.SaveAssets();
            Debug.Log($"[Editor] Saved scene: {NativeToolkitExampleScenePath}");
        }
        else
        {
            Debug.LogError($"[Editor] Failed to save scene: {NativeToolkitExampleScenePath}");
        }
        return saved;
    }

    /// <summary>
    /// Selection change callback – updates inspector content based on selected items.
    /// </summary>
    private void OnTreeViewSelectionChanged(IEnumerable<object> selectedItems)
    {
        foreach (TreeItemData item in selectedItems)
        {
            Debug.Log($"[Editor] Selected: {item.name} - {item.description}");
            SetItemInspector(item);
        }
    }

    /// <summary>
    /// Double‑click (itemsChosen) callback – currently logs chosen items (expansion point for future actions).
    /// </summary>
    private void OnTreeViewItemsChosen(IEnumerable<object> chosenItems)
    {
        foreach (TreeItemData item in chosenItems)
        {
            Debug.Log($"[Editor] Double-clicked: {item.name}");
        }
    }

    /// <summary>
    /// Populates context menu with localized actions (Add / Delete / Refresh).
    /// </summary>
    private void OnContextualMenu(ContextualMenuPopulateEvent evt)
    {
        Debug.Log("Contextual menu opened");
        evt.menu.AppendAction(LocalizationUtil.L("NativeToolkit", "context.add"), (a) => Debug.Log("Add Item clicked"));
        evt.menu.AppendAction(LocalizationUtil.L("NativeToolkit", "context.delete"), (a) => Debug.Log("Delete Item clicked"));
        evt.menu.AppendSeparator();
        evt.menu.AppendAction(LocalizationUtil.L("NativeToolkit", "context.refresh"), (a) => PopulateTreeView());
    }

    /// <summary>
    /// Cleans up event subscriptions when the window is destroyed.
    /// </summary>
    private void OnDestroy()
    {
        if (treeView != null)
        {
            treeView.selectionChanged -= OnTreeViewSelectionChanged;
            treeView.itemsChosen -= OnTreeViewItemsChosen;
        }
    }
}
