#nullable enable

using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using System.IO;

public static class GenerateLocalizationAssets
{
    private const string TableName = "NativeToolkit";
    private const string LOCALES_DIR = "Assets/Localization/Locales";
    private static string TABLES_DIR => $"{LOCALES_DIR}/Tables/EditorUI";
    private static string CollectionTablesDir => $"{TABLES_DIR}/{TableName}";

    [MenuItem("Tools/Native Toolkit/Localization/Generate Assets")]
    public static void Generate()
    {
        bool dirty = false;

        EnsureDir(LOCALES_DIR);
        EnsureDir(TABLES_DIR);
        EnsureDir(CollectionTablesDir);

        var en = FindOrCreateLocale("en");
        var ja = FindOrCreateLocale("ja");

        var collection = LocalizationEditorSettings.GetStringTableCollection(TableName);
        if (collection == null)
        {
            Debug.Log($"[Localization] Creating String Table Collection: {TableName}");
            collection = LocalizationEditorSettings.CreateStringTableCollection(TableName, TableName);
            dirty = true;
        }

        // 先にテーブル資産を所定ディレクトリへ移動
        MoveStringTablesIntoCollectionDir(collection);

        AddOrUpdateEntry(collection, "folder.empty", "This folder is empty", "このフォルダーは空です");
        AddOrUpdateEntry(collection, "folder.template", "This folder is {name}", "このフォルダーは{name}です");
        AddOrUpdateEntry(collection, "button.open", "Open", "開く");
        AddOrUpdateEntry(collection, "window.title", "TreeView Editor", "ツリービュー エディター");

        // 移動後に再度パス整合性確認（新規ロケール追加などで生成された場合）
        MoveStringTablesIntoCollectionDir(collection);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Localization] Generation complete.");
    }

    private static void EnsureDir(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
            AssetDatabase.Refresh();
        }
    }

    private static Locale FindOrCreateLocale(string code)
    {
        var existing = LocalizationEditorSettings.GetLocale(new LocaleIdentifier(code));
        if (existing != null)
        {
            var expectedPath = $"{LOCALES_DIR}/{code}.locale";
            var actualPath = AssetDatabase.GetAssetPath(existing);
            if (!string.IsNullOrEmpty(actualPath) && actualPath != expectedPath)
            {
                // AssetDatabase.MoveAsset(actualPath, expectedPath); // 必要ならコメント解除
            }
            return existing;
        }

        var locale = Locale.CreateLocale(code);
        var assetPath = $"{LOCALES_DIR}/{code}.locale";
        AssetDatabase.CreateAsset(locale, assetPath);
        LocalizationEditorSettings.AddLocale(locale);
        Debug.Log($"[Localization] Added locale: {code} at {assetPath}");
        return locale;
    }

    private static void AddOrUpdateEntry(StringTableCollection collection, string key,
        string enValue, string jaValue)
    {
        var shared = collection.SharedData;
        var sharedEntry = shared.GetEntry(key) ?? shared.AddKey(key);

        foreach (var table in collection.StringTables)
        {
            if (table == null) continue;
            var localeCode = table.LocaleIdentifier.Code;
            var entry = table.GetEntry(sharedEntry.Id) ?? table.AddEntry(sharedEntry.Id, string.Empty);
            string newValue = localeCode switch
            {
                "ja" => jaValue,
                _ => enValue
            };
            if (entry.Value != newValue)
            {
                entry.Value = newValue;
                EditorUtility.SetDirty(table);
                Debug.Log($"[Localization] Updated '{key}' for {localeCode}: {newValue}");
            }
        }
    }

    private static void MoveStringTablesIntoCollectionDir(StringTableCollection collection)
    {
        EnsureDir(CollectionTablesDir);

        foreach (var table in collection.StringTables)
        {
            if (table == null) continue;
            var oldPath = AssetDatabase.GetAssetPath(table);
            if (string.IsNullOrEmpty(oldPath)) continue;

            var fileName = Path.GetFileName(oldPath);
            var newPath = $"{CollectionTablesDir}/{fileName}";

            if (oldPath != newPath)
            {
                var result = AssetDatabase.MoveAsset(oldPath, newPath);
                if (string.IsNullOrEmpty(result))
                {
                    Debug.Log($"[Localization] Moved StringTable '{table.TableCollectionName}' ({table.LocaleIdentifier.Code}) -> {newPath}");
                }
                else
                {
                    Debug.LogWarning($"[Localization] Move failed for {oldPath} : {result}");
                }
            }
        }

        // SharedData のアセットも整理
        var sharedData = collection.SharedData;
        if (sharedData != null)
        {
            var sharedPath = AssetDatabase.GetAssetPath(sharedData);
            if (!string.IsNullOrEmpty(sharedPath))
            {
                var targetSharedPath = $"{CollectionTablesDir}/{TableName}.SharedData.asset";
                if (sharedPath != targetSharedPath)
                {
                    var moveResult = AssetDatabase.MoveAsset(sharedPath, targetSharedPath);
                    if (string.IsNullOrEmpty(moveResult))
                        Debug.Log($"[Localization] Moved SharedData -> {targetSharedPath}");
                    else
                        Debug.LogWarning($"[Localization] SharedData move failed: {moveResult}");
                }
            }
        }
    }
}