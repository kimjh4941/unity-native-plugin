#nullable enable

using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEditor.Localization.Reporting;
using UnityEngine.Localization.Tables;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;

public static class GenerateLocalizationAssets
{
    private const string TableName = "NativeToolkit";

    // 要望どおり: Locales 配下に Tables/EditorUI/NativeToolkit
    private const string LOCALES_DIR = "Assets/Localization/Locales";
    private static string TABLES_DIR => $"{LOCALES_DIR}/Tables/EditorUI";
    private static string CollectionTablesDir => $"{TABLES_DIR}/{TableName}";

    [MenuItem("Tools/Native Toolkit/Localization/Generate Assets")]
    public static void Generate()
    {
        EnsureAssetDirectory(LOCALES_DIR);
        EnsureAssetDirectory(TABLES_DIR);
        EnsureAssetDirectory(CollectionTablesDir);

        var en = FindOrCreateLocale("en");
        var ja = FindOrCreateLocale("ja");

        var collection = LocalizationEditorSettings.GetStringTableCollection(TableName);
        if (collection == null)
        {
            Debug.Log($"[Localization] Creating collection '{TableName}' at {CollectionTablesDir}");
            collection = LocalizationEditorSettings.CreateStringTableCollection(
                TableName,
                CollectionTablesDir,
                new List<Locale> { en, ja }
            );
        }
        else
        {
            RelocateIfNeeded(collection);
            EnsureLocaleTables(collection, en, ja);
        }

        AddOrUpdateEntry(collection, "folder.empty", "This folder is empty", "このフォルダーは空です");
        AddOrUpdateEntry(collection, "folder.template", "This folder is {name}", "このフォルダーは{name}です");
        AddOrUpdateEntry(collection, "button.open", "Open", "開く");
        AddOrUpdateEntry(collection, "window.title", "TreeView Editor", "ツリービュー エディター");

        EditorUtility.SetDirty(collection.SharedData);
        AssetDatabase.SaveAssets();
        // Localization Tables ウィンドウへ通知
        // Notify editor that entries were modified (API no longer requires modification enum).
        LocalizationEditorSettings.EditorEvents.RaiseCollectionModified(null, collection);
        // 遅延で再通知（ウィンドウ未初期化ケース）
        EditorApplication.delayCall += () =>
        {
            LocalizationEditorSettings.EditorEvents.RaiseCollectionModified(null, collection);
        };

        // フォーカスを当てて UI 更新トリガ
        Selection.activeObject = collection.SharedData;
        AssetDatabase.Refresh();
        Debug.Log("[Localization] Generation complete.");
    }

    private static void EnsureAssetDirectory(string targetPath)
    {
        if (AssetDatabase.IsValidFolder(targetPath)) return;
        var segments = targetPath.Split('/');
        string current = "Assets";
        for (int i = 1; i < segments.Length; i++)
        {
            var next = $"{current}/{segments[i]}";
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, segments[i]);
            }
            current = next;
        }
    }

    private static Locale FindOrCreateLocale(string code)
    {
        var existing = LocalizationEditorSettings.GetLocale(new LocaleIdentifier(code));
        if (existing != null) return existing;

        var locale = Locale.CreateLocale(code);
        var fileName = GetLocaleFileName(code);
        var assetPath = $"{LOCALES_DIR}/{fileName}.asset";
        AssetDatabase.CreateAsset(locale, assetPath);
        LocalizationEditorSettings.AddLocale(locale);
        Debug.Log($"[Localization] Added locale: {code} ({assetPath})");
        return locale;
    }

    private static string GetLocaleFileName(string code)
    {
        try
        {
            var ci = new CultureInfo(code);
            // 例: English (en), Japanese (ja)
            return $"{ci.EnglishName} ({code})";
        }
        catch
        {
            return code;
        }
    }

    private static void EnsureLocaleTables(StringTableCollection collection, params Locale[] locales)
    {
        if (collection == null || collection.SharedData == null)
        {
            Debug.LogError("[Localization] Collection or SharedData is null.");
            return;
        }

        foreach (var locale in locales)
        {
            if (locale == null) continue;
            bool exists = collection.StringTables.Any(t => t != null && t.LocaleIdentifier == locale.Identifier);
            if (exists) continue;

            var newTable = collection.AddNewTable(locale.Identifier);
            if (newTable == null)
            {
                Debug.LogError($"[Localization] Failed to add table for locale {locale.Identifier.Code}");
                continue;
            }

            EnsureAssetDirectory(CollectionTablesDir);

            var createdPath = AssetDatabase.GetAssetPath(newTable);
            if (!string.IsNullOrEmpty(createdPath))
            {
                var fileName = Path.GetFileName(createdPath);
                var targetPath = $"{CollectionTablesDir}/{fileName}";
                if (createdPath != targetPath)
                {
                    var move = AssetDatabase.MoveAsset(createdPath, targetPath);
                    if (string.IsNullOrEmpty(move))
                        Debug.Log($"[Localization] Created & moved table: {fileName} -> {targetPath}");
                    else
                        Debug.LogWarning($"[Localization] Move failed for {fileName}: {move}");
                }
                else
                {
                    Debug.Log($"[Localization] Created table: {fileName}");
                }
            }
        }
    }

    private static void AddOrUpdateEntry(StringTableCollection collection, string key, string enValue, string jaValue)
    {
        var shared = collection.SharedData;
        var sharedEntry = shared.GetEntry(key) ?? shared.AddKey(key);

        foreach (var table in collection.StringTables)
        {
            if (table == null) continue;
            var code = table.LocaleIdentifier.Code;
            var entry = table.GetEntry(sharedEntry.Id) ?? table.AddEntry(sharedEntry.Id, string.Empty);
            string newValue = code.StartsWith("ja") ? jaValue : enValue;
            Debug.Log($"[Localization] Setting '{key}' for '{code}': '{newValue}'");
            entry.Value = newValue;
            EditorUtility.SetDirty(table);
        }
    }

    private static void RelocateIfNeeded(StringTableCollection collection)
    {
        bool moved = false;
        EnsureAssetDirectory(CollectionTablesDir);

        var shared = collection.SharedData;
        if (shared != null)
        {
            var sharedPath = AssetDatabase.GetAssetPath(shared);
            var targetSharedPath = $"{CollectionTablesDir}/{TableName}.SharedData.asset";
            if (!string.IsNullOrEmpty(sharedPath) && sharedPath != targetSharedPath)
            {
                var r = AssetDatabase.MoveAsset(sharedPath, targetSharedPath);
                if (string.IsNullOrEmpty(r))
                {
                    moved = true;
                    Debug.Log($"[Localization] Moved SharedData -> {targetSharedPath}");
                }
            }
        }

        foreach (var table in collection.StringTables)
        {
            if (table == null) continue;
            var oldPath = AssetDatabase.GetAssetPath(table);
            if (string.IsNullOrEmpty(oldPath)) continue;
            var fileName = Path.GetFileName(oldPath);
            var newPath = $"{CollectionTablesDir}/{fileName}";
            if (oldPath != newPath)
            {
                var r = AssetDatabase.MoveAsset(oldPath, newPath);
                if (string.IsNullOrEmpty(r))
                {
                    moved = true;
                    Debug.Log($"[Localization] Moved {fileName} -> {newPath}");
                }
            }
        }

        if (moved)
        {
            AssetDatabase.SaveAssets();
        }
    }
}