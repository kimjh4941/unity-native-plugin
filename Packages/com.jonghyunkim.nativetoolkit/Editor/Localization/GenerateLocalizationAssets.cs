#nullable enable

using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;

/// <summary>
/// Utility that (re)generates the Native Toolkit localization assets (Locales, StringTableCollection,
/// and per-locale tables) from a CSV source file. Ensures directory layout:
/// <c>Assets/Localization/Locales/Tables/EditorUI/NativeToolkit</c> as requested.
/// </summary>
/// <remarks>
/// CSV schema: "key","en","ja" (header line is skipped). Existing entries are updated, new ones added.
/// The generation process also relocates assets to the canonical directory if they were moved by the user.
/// </remarks>
public static class GenerateLocalizationAssets
{
    private static string CSV_FILE => "Packages/com.jonghyunkim.nativetoolkit/Localization/Tables/EditorUI/NativeToolkit/NativeToolkit.csv";
    private const string TableName = "NativeToolkit";

    // Layout requirement (as requested): Locales/Tables/EditorUI/NativeToolkit
    private const string LOCALES_DIR = "Assets/Localization/Locales";
    private static string TABLES_DIR => $"{LOCALES_DIR}/Tables/EditorUI";
    private static string CollectionTablesDir => $"{TABLES_DIR}/{TableName}";

    /// <summary>
    /// Menu command entry point. Creates / updates the localization collection and applies CSV entries.
    /// </summary>
    [MenuItem("Tools/Native Toolkit/Localization/Generate Assets")]
    public static void Generate()
    {
        EnsureAssetDirectory(LOCALES_DIR);
        EnsureAssetDirectory(TABLES_DIR);
        EnsureAssetDirectory(CollectionTablesDir);

        var en = FindOrCreateLocale("en");
        var ko = FindOrCreateLocale("ko");
        var ja = FindOrCreateLocale("ja");

        var collection = LocalizationEditorSettings.GetStringTableCollection(TableName);
        if (collection == null)
        {
            Debug.Log($"[Localization] Creating collection '{TableName}' at {CollectionTablesDir}");
            collection = LocalizationEditorSettings.CreateStringTableCollection(
                TableName,
                CollectionTablesDir,
                new List<Locale> { en, ko, ja }
            );
        }
        else
        {
            RelocateIfNeeded(collection);
            EnsureLocaleTables(collection, en, ko, ja);
        }

        // Load entries from CSV source (header skipped)
        var entries = LoadCsvEntries(CSV_FILE);

        foreach (var (key, enValue, koValue, jaValue) in entries)
        {
            AddOrUpdateEntry(collection, key, enValue, koValue, jaValue);
        }

        EditorUtility.SetDirty(collection.SharedData);
        AssetDatabase.SaveAssets();
        // Notify Localization Tables window (initial immediate notification)
        // Notify editor that entries were modified (API no longer requires modification enum).
        LocalizationEditorSettings.EditorEvents.RaiseCollectionModified(null, collection);
        // Re-notify on delay to cover case where window not yet initialized
        EditorApplication.delayCall += () =>
        {
            LocalizationEditorSettings.EditorEvents.RaiseCollectionModified(null, collection);
        };

        // Focus to shared data asset to trigger UI refresh
        Selection.activeObject = collection.SharedData;
        AssetDatabase.Refresh();
        Debug.Log("[Localization] Generation complete.");
    }

    /// <summary>
    /// Ensures that a nested asset folder path exists by creating any missing intermediate folders.
    /// </summary>
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
                Debug.Log($"[Localization] Creating folder: {next}");
                AssetDatabase.CreateFolder(current, segments[i]);
            }
            current = next;
        }
    }

    /// <summary>
    /// Retrieves an existing locale (by IETF code) or creates and registers a new one.
    /// </summary>
    private static Locale FindOrCreateLocale(string code)
    {
        var existing = LocalizationEditorSettings.GetLocale(new LocaleIdentifier(code));
        if (existing != null)
        {
            Debug.Log($"[Localization] Found existing locale: {code}");
            return existing;
        }
        var locale = Locale.CreateLocale(code);
        var fileName = GetLocaleFileName(code);
        var assetPath = $"{LOCALES_DIR}/{fileName}.asset";
        AssetDatabase.CreateAsset(locale, assetPath);
        LocalizationEditorSettings.AddLocale(locale);
        Debug.Log($"[Localization] Added locale: {code} ({assetPath})");
        return locale;
    }

    /// <summary>
    /// Builds a friendly asset filename using English display name and the locale code.
    /// </summary>
    private static string GetLocaleFileName(string code)
    {
        try
        {
            var ci = new CultureInfo(code);
            // Example: English (en), Korean (ko), Japanese (ja)
            return $"{ci.EnglishName} ({code})";
        }
        catch
        {
            return code;
        }
    }

    /// <summary>
    /// Verifies that a string table exists for each provided locale; creates any missing tables
    /// and relocates them into the canonical collection directory.
    /// </summary>
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

    /// <summary>
    /// Adds a key if missing and updates localized values for all tables in the collection.
    /// </summary>
    private static void AddOrUpdateEntry(StringTableCollection collection, string key, string enValue, string koValue, string jaValue)
    {
        var shared = collection.SharedData;
        var sharedEntry = shared.GetEntry(key) ?? shared.AddKey(key);

        foreach (var table in collection.StringTables)
        {
            if (table == null) continue;
            var code = table.LocaleIdentifier.Code;
            var entry = table.GetEntry(sharedEntry.Id) ?? table.AddEntry(sharedEntry.Id, string.Empty);
            string newValue = code.StartsWith("ko") ? koValue : (code.StartsWith("ja") ? jaValue : enValue);
            Debug.Log($"[Localization] Setting '{key}' for '{code}': '{newValue}'");
            entry.Value = newValue;
            EditorUtility.SetDirty(table);
        }
    }

    /// <summary>
    /// Moves shared data and per-locale tables into the expected directory hierarchy if they have drifted.
    /// </summary>
    private static void RelocateIfNeeded(StringTableCollection collection)
    {
        bool moved = false;
        EnsureAssetDirectory(CollectionTablesDir);

        var shared = collection.SharedData;
        if (shared != null)
        {
            var sharedPath = AssetDatabase.GetAssetPath(shared);
            var targetSharedPath = $"{CollectionTablesDir}/{TableName} Shared Data.asset";
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

    /// <summary>
    /// Loads CSV entries (skipping header) returning a tuple list of (key, en, ko, ja) values.
    /// </summary>
    private static List<(string key, string en, string ko, string ja)> LoadCsvEntries(string csvPath)
    {
        var entries = new List<(string, string, string, string)>();
        if (!File.Exists(csvPath))
        {
            Debug.LogWarning($"[Localization] CSV file not found: {csvPath}");
            return entries;
        }

        var lines = File.ReadAllLines(csvPath);
        foreach (var line in lines.Skip(1))  // Skip header line
        {
            // CSV schema: "key","en","ko","ja"
            var parts = line.Split(',');
            if (parts.Length >= 4)
            {
                var key = parts[0].Trim('"');
                var en = parts[1].Trim('"');
                var ko = parts[2].Trim('"');
                var ja = parts[3].Trim('"');
                entries.Add((key, en, ko, ja));
            }
        }
        Debug.Log($"[Localization] Loaded {entries.Count} entries from CSV.");
        return entries;
    }
}