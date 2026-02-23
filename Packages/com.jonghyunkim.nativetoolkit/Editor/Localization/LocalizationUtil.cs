using System.Linq;
using UnityEngine;
using UnityEditor.Localization;
using UnityEngine.Localization.Settings;

/// <summary>
/// Editor-only diagnostics for localization issues.
/// </summary>
public static partial class LocalizationUtil
{
    /// <summary>
    /// Fetch a localized string from the specified table. Returns the key on failure
    /// (graceful degradation). When running in the Editor, will emit detailed diagnostics
    /// via <c>DebugLogEntryValues</c> if the entry is not resolved.
    /// </summary>
    /// <param name="table">String table collection name (e.g. "NativeToolkit").</param>
    /// <param name="key">Entry key within the table.</param>
    /// <returns>Localized string value or the key if not resolved.</returns>
    public static string L(string table, string key)
    {
        try
        {
            var value = LocalizationSettings.StringDatabase.GetLocalizedString(table, key);
            if (string.IsNullOrEmpty(value))
            {
                return key;
            }
            return value;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[Localization] L() failed table={table} key={key} : {ex.Message}");
            return key;
        }
    }

    /// <summary>
    /// Outputs per-locale values for a key to aid in diagnosing missing localization entries.
    /// </summary>
    /// <param name="table">String table collection name.</param>
    /// <param name="key">Entry key.</param>
    public static void DebugLogEntryValues(string table, string key)
    {
        var collection = LocalizationEditorSettings.GetStringTableCollection(table);
        if (collection == null)
        {
            Debug.LogError($"[Localization] Collection not found: {table}");
            return;
        }

        var shared = collection.SharedData;
        var sharedEntry = shared.GetEntry(key);
        if (sharedEntry == null)
        {
            var keys = string.Join(", ", shared.Entries.Select(e => e.Key));
            Debug.LogError($"[Localization] Key '{key}' not in SharedData. Existing keys: {keys}");
            return;
        }

        foreach (var t in collection.StringTables)
        {
            if (t == null) continue;
            var entry = t.GetEntry(sharedEntry.Id);
            var val = entry?.Value ?? "<null>";
            Debug.Log($"[Localization] TableLocale={t.LocaleIdentifier.Code} key={key} value='{val}'");
        }
    }
}
