#nullable enable

using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using System.Collections.Generic;
using UnityEditor.SceneManagement;

public class TreeViewEditorWindow : EditorWindow
{
    [SerializeField] private VisualTreeAsset? mainDocument;
    [SerializeField] private VisualTreeAsset? itemTemplate;
    [SerializeField] private StyleSheet? styleSheet;

    private TreeView? treeView;

    private const string NativeToolkitExampleScenePath = "Assets/Scenes/NativeToolkitExampleScene.unity";

    private System.Action? _fileOpenHandler;

    [MenuItem("Tools/TreeView Editor")]
    public static void ShowWindow()
    {
        TreeViewEditorWindow window = GetWindow<TreeViewEditorWindow>();
        window.titleContent = new GUIContent("TreeView Editor");
        window.Show();
    }

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

        InitializeTreeView();
        PopulateTreeView();
        SetupTreeViewEvents();
        SetItemInspector();
    }

    private void LoadAssets()
    {
        // Load assets from Resources folder if not assigned
        if (mainDocument == null)
        {
            mainDocument = Resources.Load<VisualTreeAsset>("UI/TreeView");
        }

        if (itemTemplate == null)
        {
            itemTemplate = Resources.Load<VisualTreeAsset>("UI/TreeViewItem");
        }

        if (styleSheet == null)
        {
            styleSheet = Resources.Load<StyleSheet>("UI/TreeViewStyles");
        }
    }

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

        var header = new Label("TreeView Editor");
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

    private void InitializeTreeView()
    {
        treeView = rootVisualElement.Q<TreeView>("example-tree");

        if (treeView == null)
        {
            Debug.LogError("TreeView not found in UI Document!");
            return;
        }

        // TreeViewアイテムの高さを設定
        treeView.fixedItemHeight = 25f;

        // Editor環境での設定
        treeView.focusable = true;
        treeView.pickingMode = PickingMode.Position;
        treeView.selectionType = SelectionType.Single;
    }

    private void PopulateTreeView()
    {
        if (treeView == null) return;

        // サンプルデータの作成
        var rootData = CreateSampleData();

        // TreeViewItemDataに直接変換
        var treeViewItems = new List<TreeViewItemData<TreeItemData>>();
        foreach (var rootItem in rootData)
        {
            var treeViewItem = CreateTreeViewItemData(rootItem);
            treeViewItems.Add(treeViewItem);
        }

        // TreeViewにデータを設定
        treeView.SetRootItems(treeViewItems);

        // アイテム作成とバインド関数を設定
        treeView.makeItem = MakeTreeViewItem;
        treeView.bindItem = BindTreeViewItem;

        // TreeViewを再構築
        treeView.Rebuild();
    }

    private TreeViewItemData<TreeItemData> CreateTreeViewItemData(TreeItemData data)
    {
        var children = new List<TreeViewItemData<TreeItemData>>();

        // 子要素がある場合は再帰的に処理
        if (data.children != null && data.children.Count > 0)
        {
            foreach (var child in data.children)
            {
                children.Add(CreateTreeViewItemData(child));
            }
        }

        return new TreeViewItemData<TreeItemData>(data.id, data, children);
    }

    private VisualElement MakeTreeViewItem()
    {
        if (itemTemplate != null)
        {
            // UI Builderテンプレートを使用
            var itemElement = itemTemplate.Instantiate();
            var container = itemElement.Q<VisualElement>("tree-item-container");
            return container ?? itemElement;
        }
        else
        {
            // プログラムで作成
            return CreateTreeItemElement();
        }
    }

    private VisualElement CreateTreeItemElement()
    {
        var container = new VisualElement();
        container.name = "tree-item-container";
        container.style.flexDirection = FlexDirection.Row;
        container.style.alignItems = Align.Center;
        container.style.paddingLeft = 5;
        container.style.paddingRight = 5;
        container.style.paddingTop = 2;
        container.style.paddingBottom = 2;
        container.style.minHeight = 25;

        var icon = new VisualElement();
        icon.name = "item-icon";
        icon.style.width = 16;
        icon.style.height = 16;
        icon.style.marginRight = 5;
        // icon.style.borderRadius = 2;
        icon.style.backgroundColor = Color.gray;

        var label = new Label();
        label.name = "item-label";
        label.style.flexGrow = 1;
        label.style.color = Color.white;

        var description = new Label();
        description.name = "item-description";
        description.style.color = new Color(0.7f, 0.7f, 0.7f);
        description.style.fontSize = 10;
        description.style.marginLeft = 10;

        container.Add(icon);
        container.Add(label);
        container.Add(description);

        return container;
    }

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
        }

        if (description != null)
        {
            description.text = item.description;
            description.style.display = string.IsNullOrEmpty(item.description) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        if (icon != null)
        {
            // フォルダーかファイルかでアイコンの色を変更
            if (item.children != null && item.children.Count > 0)
            {
                icon.style.backgroundColor = new Color(1f, 0.8f, 0f); // フォルダー（黄色）
            }
            else
            {
                icon.style.backgroundColor = new Color(0f, 0.8f, 1f); // ファイル（シアン）
            }
        }

        // ホバー効果
        element.RegisterCallback<MouseEnterEvent>(evt =>
        {
            element.style.backgroundColor = new Color(1f, 1f, 1f, 0.1f);
        });

        element.RegisterCallback<MouseLeaveEvent>(evt =>
        {
            element.style.backgroundColor = Color.clear;
        });
    }

    private List<TreeItemData> CreateSampleData()
    {
        var data = new List<TreeItemData>();

        // ルートアイテム1
        var folder1 = new TreeItemData(1, 1, true, "Android");
        var subfolder1 = new TreeItemData(2, 2, true, "Dialog");
        subfolder1.children.Add(new TreeItemData(3, 3, false, "AndroidDialogManager.cs"));
        folder1.children.Add(subfolder1);

        // ルートアイテム2
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

    private void SetupTreeViewEvents()
    {
        if (treeView == null) return;

        // アイテム選択イベント
        treeView.selectionChanged += OnTreeViewSelectionChanged;

        // アイテムダブルクリックイベント
        treeView.itemsChosen += OnTreeViewItemsChosen;

        // 右クリックコンテキストメニュー
        treeView.RegisterCallback<ContextualMenuPopulateEvent>(OnContextualMenu);
    }

    private void SetItemInspector(TreeItemData? item = null)
    {
        var itemInspector = rootVisualElement.Q<VisualElement>("item-inspector");
        var infoInspector = rootVisualElement.Q<VisualElement>("info-inspector");

        // 要素が見つからない場合のエラーハンドリング
        if (itemInspector == null || infoInspector == null)
        {
            Debug.LogError("[Editor] Inspector elements not found in UI");
            return;
        }

        // 何も選択されていない状態
        if (item == null)
        {
            infoInspector.visible = true;
            itemInspector.visible = false;
            return;
        }

        // フォルダーの場合は情報インスペクターを表示
        if (item != null && item.isFolder)
        {
            Debug.Log($"[Editor] Displaying info for folder: {item.name}");
            infoInspector.visible = true;
            itemInspector.visible = false;

            infoInspector.Q<Label>("message").text = $"This folder is {item.name}";
            return;
        }

        // アイテムがフォルダーで、子要素がない場合
        if (item != null && item.isFolder && item.children.Count == 0)
        {
            Debug.Log($"[Editor] Displaying info for folder: {item.name}");
            infoInspector.visible = true;
            itemInspector.visible = false;

            infoInspector.Q<Label>("message").text = $"This folder is empty";
            return;
        }

        // アイテムがファイルを選択されている場合
        if (item != null && !item.isFolder)
        {
            Debug.Log($"[Editor] Displaying inspector for item: {item.name}");
            infoInspector.visible = false;
            itemInspector.visible = true;
        }

        // NativeToolkitExampleSceneが読み込まれている場合、GameObjectを探してInspectorに表示
        var fileName = itemInspector.Q<Label>("file-name");
        var fileOpen = itemInspector.Q<Button>("file-open");
        if (fileName != null)
        {
            fileName.text = item?.name;
        }

        if (fileOpen != null)
        {
            // ラムダ式は毎回新しいインスタンスになるため、解除・登録には変数で保持する必要があります
            if (_fileOpenHandler != null)
            {
                fileOpen.clicked -= _fileOpenHandler;
            }
            _fileOpenHandler = () => OnFileOpenClicked(item);
            fileOpen.clicked += _fileOpenHandler;
        }
    }

    private void OnFileOpenClicked(TreeItemData? item)
    {
        Debug.Log($"[Editor] Open file: {item?.name}");

        try
        {
            // NativeToolkitSampleSceneが読み込まれていない場合、NativeToolkitSampleSceneを読み込む
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

            // UXMLファイルのパスを動的に決定
            var uxmlPath = GetUXMLPathForItem(item);
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);

            if (uxml == null)
            {
                Debug.LogError($"[Editor] UXML not found at path: {uxmlPath}");
                return;
            }

            Debug.Log($"[Editor] UXML found at path: {uxmlPath}");

            // GameObjectを検索
            var gameObject = FindUIGameObject("NativeToolkitExample");
            if (gameObject == null)
            {
                Debug.LogError($"[Editor] Failed to find GameObject");
                return;
            }

            // UIDocumentコンポーネントを取得または追加
            var uiDocument = gameObject.GetComponent<UIDocument>();
            if (uiDocument == null)
            {
                uiDocument = gameObject.AddComponent<UIDocument>();
                Debug.Log($"[Editor] Added UIDocument component to {gameObject.name}");
            }

            // UXMLをUIDocumentに適用
            uiDocument.visualTreeAsset = uxml;

            // PanelSettingsを設定
            var panelSettings = GetPanelSettings(item);
            if (panelSettings != null)
            {
                uiDocument.panelSettings = panelSettings;
                Debug.Log($"[Editor] Applied PanelSettings: {panelSettings.name}");
            }

            // スタイルシートがある場合は適用
            ApplyStyleSheetIfExists(uiDocument, item);

            // プラットフォーム固有のコントローラーを自動追加
            AddControllerComponent(gameObject, item);

            Debug.Log($"[Editor] Successfully applied UXML to GameObject: {gameObject.name}");

            // 変更をシーンにマークして保存
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            SaveNativeToolkitExampleScene(prompt: false);

            // GameObjectを選択してInspectorで確認できるようにする
            Selection.activeGameObject = gameObject;

            // Scene Viewにフォーカスを当てる
            EditorApplication.ExecuteMenuItem("Window/General/Scene");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[Editor] Error applying UXML: {ex.Message}");
        }
    }

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

    private void SetControllerUIDocument<T>(T controller, UIDocument uiDocument) where T : MonoBehaviour
    {
        // publicプロパティを使用して設定を試行
        var propertyInfo = typeof(T).GetProperty("UIDocument");
        if (propertyInfo != null && propertyInfo.CanWrite)
        {
            propertyInfo.SetValue(controller, uiDocument);
            Debug.Log($"[Editor] Set UIDocument reference in {typeof(T).Name} via property");
            return;
        }

        // リフレクションを使用してprivateフィールドに値を設定
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

    private void AddAndroidDialogManagerExampleController(GameObject gameObject)
    {
        // 既存のコントローラーを削除（重複回避）
        RemoveExistingControllers(gameObject);

        // AndroidDialogManagerExampleControllerを追加
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

        // UIDocumentの参照を設定
        var uiDocument = gameObject.GetComponent<UIDocument>();
        if (uiDocument != null && controller != null)
        {
            SetControllerUIDocument(controller, uiDocument);
        }
    }

    private void AddIosDialogManagerExampleController(GameObject gameObject)
    {
        // 既存のコントローラーを削除
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

        // UIDocumentの参照を設定
        var uiDocument = gameObject.GetComponent<UIDocument>();
        if (uiDocument != null && controller != null)
        {
            SetControllerUIDocument(controller, uiDocument);
        }
    }

    private void AddMacDialogManagerExampleController(GameObject gameObject)
    {
        // 既存のコントローラーを削除
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

        // UIDocumentの参照を設定
        var uiDocument = gameObject.GetComponent<UIDocument>();
        if (uiDocument != null && controller != null)
        {
            SetControllerUIDocument(controller, uiDocument);
        }
    }

    private void AddWindowsDialogManagerExampleController(GameObject gameObject)
    {
        // 既存のコントローラーを削除
        RemoveExistingControllers(gameObject);

        Debug.Log($"[Editor] Windows Dialog Manager Example Controller not implemented yet for {gameObject.name}");

        // 将来的にWindowsDialogManagerExampleControllerを実装する場合
        /*
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

        // UIDocumentの参照を設定
        var uiDocument = gameObject.GetComponent<UIDocument>();
        if (uiDocument != null && controller != null)
        {
            SetControllerUIDocument(controller, uiDocument);
        }
        */
    }

    private void RemoveExistingControllers(GameObject gameObject)
    {
        // 既存のダイアログコントローラーを削除（プラットフォーム間の競合を防ぐ）
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

        // 将来的に他のプラットフォームコントローラーを追加する場合
        /*
        var windowsController = gameObject.GetComponent<WindowsDialogManagerExampleController>();
        if (windowsController != null)
        {
            DestroyImmediate(windowsController);
            Debug.Log($"[Editor] Removed existing WindowsDialogManagerExampleController from {gameObject.name}");
        }
        */
    }

    private PanelSettings? GetPanelSettings(TreeItemData? item)
    {
        // アイテムに応じた専用のPanelSettingsパスを取得
        var panelSettingsPath = GetPanelSettingsPathForItem(item);
        var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(panelSettingsPath);

        if (panelSettings != null)
        {
            Debug.Log($"[Editor] Found existing PanelSettings: {panelSettingsPath}");
            return panelSettings;
        }

        // 共通のPanelSettingsを試行
        var commonPanelSettingsPath = "Assets/Settings/UI/Common/CommonPanelSettings.asset";
        var commonPanelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(commonPanelSettingsPath);

        if (commonPanelSettings != null)
        {
            Debug.Log($"[Editor] Using common PanelSettings: {commonPanelSettingsPath}");
            return commonPanelSettings;
        }
        else
        {
            Debug.LogError($"[Editor] PanelSettings not found at path: {panelSettingsPath} or {commonPanelSettingsPath}");
            return null;
        }
    }

    private string GetPanelSettingsPathForItem(TreeItemData? item)
    {
        // アイテム名に基づいてPanelSettingsのパスを決定
        switch (item?.name)
        {
            case "AndroidDialogManager.cs":
                return "Assets/Settings/UI/Android/AndroidDialogPanelSettings.asset";
            case "IosDialogManager.cs":
                return "Assets/Settings/UI/iOS/IosDialogPanelSettings.asset";
            case "MacDialogManager.cs":
                return "Assets/Settings/UI/macOS/MacDialogPanelSettings.asset";
            case "WindowsDialogManager.cs":
                return "Assets/Settings/UI/Windows/WindowsDialogPanelSettings.asset";
            default:
                return "Assets/Settings/UI/Common/DefaultPanelSettings.asset";
        }
    }

    private string GetUXMLPathForItem(TreeItemData? item)
    {
        // アイテム名に基づいてUXMLファイルのパスを決定
        switch (item?.name)
        {
            case "AndroidDialogManager.cs":
                return "Assets/Scripts/Runtime/UI/Android/Dialog/AndroidDialogManagerExample.uxml";
            case "IosDialogManager.cs":
                return "Assets/Scripts/Runtime/UI/iOS/Dialog/IosDialogManagerExample.uxml";
            case "MacDialogManager.cs":
                return "Assets/Scripts/Runtime/UI/macOS/Dialog/MacDialogManagerExample.uxml";
            case "WindowsDialogManager.cs":
                return "Assets/Scripts/Runtime/UI/Windows/Dialog/WindowsDialogManagerExample.uxml";
            default:
                return "Assets/Scripts/Runtime/UI/Default/DefaultExample.uxml";
        }
    }

    private GameObject? FindUIGameObject(string objectName)
    {
        // 既存のGameObjectを検索
        var existingObject = GameObject.Find(objectName);
        if (existingObject != null)
        {
            Debug.Log($"[Editor] Found existing GameObject: {objectName}");
            return existingObject;
        }
        return null;
    }

    private void ApplyStyleSheetIfExists(UIDocument uiDocument, TreeItemData? item)
    {
        // 対応するスタイルシートのパスを取得
        var stylePath = GetStyleSheetPathForItem(item);
        var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(stylePath);

        if (styleSheet != null)
        {
            // 既存のスタイルシートをクリア
            uiDocument.rootVisualElement.styleSheets.Clear();
            uiDocument.rootVisualElement.styleSheets.Add(styleSheet);
            Debug.Log($"[Editor] Applied stylesheet: {stylePath}");
        }
        else
        {
            Debug.LogWarning($"[Editor] Stylesheet not found at path: {stylePath}");
        }
    }

    private string GetStyleSheetPathForItem(TreeItemData? item)
    {
        // アイテム名に基づいてスタイルシートのパスを決定
        switch (item?.name)
        {
            case "AndroidDialogManager.cs":
                return "Assets/Scripts/Runtime/UI/Android/Dialog/AndroidDialogManagerExampleStyle.uss";
            case "IosDialogManager.cs":
                return "Assets/Scripts/Runtime/UI/iOS/Dialog/IosDialogManagerExampleStyle.uss";
            case "MacDialogManager.cs":
                return "Assets/Scripts/Runtime/UI/macOS/Dialog/MacDialogManagerExampleStyle.uss";
            case "WindowsDialogManager.cs":
                return "Assets/Scripts/Runtime/UI/Windows/Dialog/WindowsDialogManagerExampleStyle.uss";
            default:
                return "Assets/Scripts/Runtime/UI/Common/CommonStyles.uss";
        }
    }

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

    private void OnTreeViewSelectionChanged(IEnumerable<object> selectedItems)
    {
        foreach (TreeItemData item in selectedItems)
        {
            Debug.Log($"[Editor] Selected: {item.name} - {item.description}");
            SetItemInspector(item);
        }
    }

    private void OnTreeViewItemsChosen(IEnumerable<object> chosenItems)
    {
        foreach (TreeItemData item in chosenItems)
        {
            Debug.Log($"[Editor] Double-clicked: {item.name}");
        }
    }

    private void OnContextualMenu(ContextualMenuPopulateEvent evt)
    {
        Debug.Log("Contextual menu opened");
        evt.menu.AppendAction("Add Item", (a) => Debug.Log("Add Item clicked"));
        evt.menu.AppendAction("Delete Item", (a) => Debug.Log("Delete Item clicked"));
        evt.menu.AppendSeparator();
        evt.menu.AppendAction("Refresh", (a) => PopulateTreeView());
    }

    private void OnDestroy()
    {
        if (treeView != null)
        {
            treeView.selectionChanged -= OnTreeViewSelectionChanged;
            treeView.itemsChosen -= OnTreeViewItemsChosen;
        }
    }
}
