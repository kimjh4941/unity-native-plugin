#nullable enable

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
namespace JonghyunKim.NativeToolkit.Runtime.Clipboard
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using UnityEngine;

    /// <summary>
    /// Parses the JSON payloads returned by the native Windows clipboard APIs: the file list of
    /// PasteFiles, the format list of GetFormats, the clipboard history array, and the history
    /// availability object.
    /// <para>
    /// Uses <see cref="JsonUtility"/> to avoid an external dependency, matching
    /// AndroidClipboardJsonParser. JsonUtility cannot parse a top-level array, so an array payload
    /// is wrapped in an object before it is handed over. The iOS reader is deliberately not shared:
    /// common.md requires each platform to carry its own copy rather than sharing one.
    /// </para>
    /// <para>
    /// Intentional deviation from csharp.md's "log every internal method" rule: these methods
    /// receive raw clipboard content, which may hold passwords or tokens, so no entry log is
    /// emitted at all. Failures log the shape of the problem, never the payload.
    /// </para>
    /// </summary>
    internal static class WindowsClipboardJsonParser
    {
        private const string LogTag = "WindowsClipboardJsonParser";

        private static readonly IReadOnlyList<string> NoValues = Array.Empty<string>();
        private static readonly IReadOnlyList<WindowsClipboardHistoryItem> NoItems =
            Array.Empty<WindowsClipboardHistoryItem>();

        [Serializable]
        private sealed class StringArrayDto
        {
            public string[]? values;
        }

        [Serializable]
        private sealed class HistoryItemDto
        {
            public string? id;
            public string? text;
            public string[]? contentTypes;
            public string? timestamp;
        }

        [Serializable]
        private sealed class HistoryArrayDto
        {
            public HistoryItemDto[]? values;
        }

        [Serializable]
        private sealed class AvailabilityDto
        {
            public bool historyEnabled;
            public bool roamingEnabled;
        }

        /// <summary>
        /// Parses a JSON array of strings, as returned by PasteFiles and GetFormats.
        /// </summary>
        /// <param name="json">The raw payload. An empty array is valid input.</param>
        /// <param name="values">The parsed values. Never null; empty on failure.</param>
        /// <returns>True when the payload was a JSON array of strings.</returns>
        internal static bool TryParseStringArray(string? json, out IReadOnlyList<string> values)
        {
            values = NoValues;
            if (string.IsNullOrEmpty(json)) return false;

            if (!TryWrapArray(json!, out string wrapped)) return false;

            try
            {
                StringArrayDto? dto = JsonUtility.FromJson<StringArrayDto>(wrapped);
                // A payload that is not an array of strings leaves values null. Reporting that as
                // an empty success would let a malformed payload masquerade as an empty clipboard,
                // which the caller cannot tell apart from the real thing.
                if (dto?.values == null) return false;
                values = dto.values;
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{LogTag}][{nameof(TryParseStringArray)}] {ex.GetType().Name}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Parses the clipboard history array.
        /// </summary>
        /// <param name="json">The raw payload. An empty array is valid input.</param>
        /// <param name="items">The parsed items. Never null; empty on failure.</param>
        /// <returns>True when the payload was a JSON array of history entries.</returns>
        internal static bool TryParseHistoryItems(
            string? json, out IReadOnlyList<WindowsClipboardHistoryItem> items)
        {
            items = NoItems;
            if (string.IsNullOrEmpty(json)) return false;

            if (!TryWrapArray(json!, out string wrapped)) return false;

            try
            {
                HistoryArrayDto? dto = JsonUtility.FromJson<HistoryArrayDto>(wrapped);
                // Same reasoning as TryParseStringArray: a malformed payload must not read as an
                // empty history.
                if (dto?.values == null) return false;

                var parsed = new List<WindowsClipboardHistoryItem>(dto.values.Length);
                foreach (HistoryItemDto entry in dto.values)
                {
                    if (entry == null || string.IsNullOrEmpty(entry.id))
                    {
                        // An entry without an id cannot be restored or deleted, so it is useless
                        // to a caller; dropping it beats handing back an unusable handle.
                        Debug.LogWarning($"[{LogTag}][{nameof(TryParseHistoryItems)}] dropped an entry without an id");
                        continue;
                    }

                    long timestamp = 0;
                    if (!string.IsNullOrEmpty(entry.timestamp) &&
                        !long.TryParse(entry.timestamp, NumberStyles.Integer, CultureInfo.InvariantCulture, out timestamp))
                    {
                        Debug.LogWarning($"[{LogTag}][{nameof(TryParseHistoryItems)}] a timestamp was not an int64");
                        timestamp = 0;
                    }

                    // The native layer omits keys whose value is null, so an absent text arrives
                    // as null here and an empty string is normalized to null as well.
                    string? text = string.IsNullOrEmpty(entry.text) ? null : entry.text;
                    parsed.Add(new WindowsClipboardHistoryItem(entry.id!, text, entry.contentTypes, timestamp));
                }

                items = parsed;
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{LogTag}][{nameof(TryParseHistoryItems)}] {ex.GetType().Name}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Parses the history availability object.
        /// </summary>
        /// <param name="json">The raw payload, an object with two boolean keys.</param>
        /// <param name="historyEnabled">Whether clipboard history is on. False on failure.</param>
        /// <param name="roamingEnabled">Whether cloud sync is on. False on failure.</param>
        /// <returns>True when the payload was a JSON object.</returns>
        internal static bool TryParseAvailability(string? json, out bool historyEnabled, out bool roamingEnabled)
        {
            historyEnabled = false;
            roamingEnabled = false;
            if (string.IsNullOrEmpty(json)) return false;

            // JsonUtility fills a missing bool with false instead of failing, so an object that
            // carries neither key would be indistinguishable from "history is off". Both keys are
            // mandatory in the native schema (design 2.4), so their absence is a parse failure.
            if (!LooksLikeObjectWithKeys(json!, "historyEnabled", "roamingEnabled")) return false;

            try
            {
                AvailabilityDto? dto = JsonUtility.FromJson<AvailabilityDto>(json!);
                if (dto == null) return false;
                historyEnabled = dto.historyEnabled;
                roamingEnabled = dto.roamingEnabled;
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{LogTag}][{nameof(TryParseAvailability)}] {ex.GetType().Name}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Wraps a top-level JSON array so JsonUtility, which only accepts an object, can read it.
        /// </summary>
        /// <param name="json">The raw payload.</param>
        /// <param name="wrapped">The wrapped text. Meaningful only when this returns true.</param>
        /// <returns>False when the payload is not shaped like a JSON array.</returns>
        private static bool TryWrapArray(string json, out string wrapped)
        {
            string trimmed = json.Trim();
            wrapped = "{\"values\":" + trimmed + "}";
            return trimmed.Length >= 2 && trimmed[0] == '[' && trimmed[trimmed.Length - 1] == ']';
        }

        /// <summary>
        /// Checks that the payload is a JSON object that names every required key.
        /// </summary>
        /// <param name="json">The raw payload.</param>
        /// <param name="keys">Key names the native schema requires.</param>
        /// <returns>False when the payload is not an object or a key is missing.</returns>
        private static bool LooksLikeObjectWithKeys(string json, params string[] keys)
        {
            string trimmed = json.Trim();
            if (trimmed.Length < 2 || trimmed[0] != '{' || trimmed[trimmed.Length - 1] != '}') return false;
            foreach (string key in keys)
            {
                if (trimmed.IndexOf("\"" + key + "\"", StringComparison.Ordinal) < 0) return false;
            }
            return true;
        }
    }
}
#endif
