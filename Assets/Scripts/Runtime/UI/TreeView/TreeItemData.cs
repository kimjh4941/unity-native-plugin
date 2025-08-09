using System.Collections.Generic;

[System.Serializable]
public class TreeItemData
{
    public int id;
    public int depth;
    public bool isFolder;
    public string name;
    public string description;
    public List<TreeItemData> children;

    public TreeItemData(int id, int depth, bool isFolder, string name, string description = "")
    {
        this.id = id;
        this.depth = depth;
        this.isFolder = isFolder;
        this.name = name;
        this.description = description;
        this.children = new List<TreeItemData>();
    }

    // 従来の3つのパラメータのコンストラクタ（後方互換性のため）
    public TreeItemData(int id, string name, string description = "")
    {
        this.id = id;
        this.depth = 0;
        this.isFolder = false;
        this.name = name;
        this.description = description;
        this.children = new List<TreeItemData>();
    }
}
