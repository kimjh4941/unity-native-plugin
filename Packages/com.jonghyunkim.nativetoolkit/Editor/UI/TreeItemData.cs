#nullable enable

using System.Collections.Generic;

/// <summary>
/// Data model representing a node displayed in the editor TreeView. Supports folder/file semantics
/// with arbitrary depth and a simple description field used for UI display. Children are stored as a
/// concrete list to simplify recursive conversion to <c>TreeViewItemData&lt;T&gt;</c>.
/// </summary>
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

    /// <summary>
    /// Backwards‑compatible constructor formerly used (id + name + optional description) when
    /// depth / folder distinction was implicit. Initializes as a non-folder, depth 0 node.
    /// </summary>
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
