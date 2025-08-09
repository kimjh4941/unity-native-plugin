using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

public class TreeViewController : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private VisualTreeAsset itemTemplate; // UI BuilderでアサインするTreeViewアイテムテンプレート
    private TreeView treeView;

    private void Start()
    {
        InitializeTreeView();
        PopulateTreeView();
        SetupTreeViewEvents();
    }

    private void InitializeTreeView()
    {
        var root = uiDocument.rootVisualElement;
        treeView = root.Q<TreeView>("example-tree");

        if (treeView == null)
        {
            Debug.LogError("TreeView not found in UI Document!");
            return;
        }

        // TreeViewアイテムの高さを設定
        treeView.fixedItemHeight = 30f; // UI Builderで作成したアイテムの高さに合わせて調整
    }

    private void PopulateTreeView()
    {
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

        // UI Builderのテンプレートを使用したアイテム作成関数を設定
        treeView.makeItem = MakeTreeViewItemFromTemplate;
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

    // UI Builderのテンプレートを使用してアイテムを作成
    private VisualElement MakeTreeViewItemFromTemplate()
    {
        if (itemTemplate == null)
        {
            Debug.LogError("Item template is not assigned!");
            return new Label("Error: No template");
        }

        // UI Builderで作成したテンプレートをインスタンス化
        var itemElement = itemTemplate.Instantiate();

        // ルート要素を取得（テンプレートの最上位要素）
        var container = itemElement.Q<VisualElement>("tree-item-container");
        if (container != null)
        {
            return container;
        }
        else
        {
            // フォールバック：テンプレート全体を返す
            return itemElement;
        }
    }

    private void BindTreeViewItem(VisualElement element, int index)
    {
        var item = treeView.GetItemDataForIndex<TreeItemData>(index);
        if (item == null) return;

        // UI Builderで定義された要素にデータをバインド
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
            // 説明がない場合は非表示
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

        // ホバー効果などの追加スタイリング
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
        var folder1 = new TreeItemData(1, "Project Assets", "Main project folder");
        folder1.children.Add(new TreeItemData(2, "Scripts", "C# script files"));
        folder1.children.Add(new TreeItemData(3, "Textures", "Image assets"));

        var subfolder1 = new TreeItemData(4, "Scenes", "Unity scene files");
        subfolder1.children.Add(new TreeItemData(5, "MainMenu.unity", "Main menu scene"));
        subfolder1.children.Add(new TreeItemData(6, "GamePlay.unity", "Gameplay scene"));
        folder1.children.Add(subfolder1);

        // ルートアイテム2
        var folder2 = new TreeItemData(7, "Resources", "Runtime resources");
        folder2.children.Add(new TreeItemData(8, "Audio", "Sound files"));
        folder2.children.Add(new TreeItemData(9, "Prefabs", "Prefab assets"));

        // ルートアイテム3
        var singleFile = new TreeItemData(10, "README.md", "Project documentation");

        data.Add(folder1);
        data.Add(folder2);
        data.Add(singleFile);

        return data;
    }

    private void SetupTreeViewEvents()
    {
        if (treeView == null) return;

        // アイテム選択イベント
        treeView.selectionChanged += OnTreeViewSelectionChanged;

        // アイテムダブルクリックイベント
        treeView.itemsChosen += OnTreeViewItemsChosen;
    }

    private void OnTreeViewSelectionChanged(IEnumerable<object> selectedItems)
    {
        foreach (TreeItemData item in selectedItems)
        {
            Debug.Log($"Selected: {item.name} - {item.description}");
        }
    }

    private void OnTreeViewItemsChosen(IEnumerable<object> chosenItems)
    {
        foreach (TreeItemData item in chosenItems)
        {
            Debug.Log($"Double-clicked: {item.name}");
        }
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
