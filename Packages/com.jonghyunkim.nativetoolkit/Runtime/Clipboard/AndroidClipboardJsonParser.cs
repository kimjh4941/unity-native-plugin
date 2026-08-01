#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace JonghyunKim.NativeToolkit.Runtime.Clipboard
{
    /// <summary>
    /// Parses JSON strings returned by the synchronous Android clipboard native APIs
    /// (<c>read</c>, <c>hasClip</c>, <c>getDescription</c>).
    ///
    /// Uses <see cref="JsonUtility"/> to avoid an external JSON dependency. JsonUtility maps public
    /// fields only and ignores properties silently, so all DTOs below are defined with public
    /// fields whose names match the native JSON keys exactly.
    ///
    /// The native layer omits JSON keys whose value is null (see design 1.6), so this parser cannot
    /// distinguish an absent value from an empty string; both are normalized to null. This is a
    /// documented public API characteristic, not an implementation detail (see ClipItem, ClipContents).
    ///
    /// Intentional deviation from csharp.md's "log every public/internal method" rule: the internal
    /// methods below receive raw clipboard content on entry (may hold passwords or tokens), so no
    /// entry log is emitted at all, matching the same rationale as <see cref="AndroidClipboardManager"/>
    /// (5.7.2 of the clipboard design). Failure paths still log, but never the raw content.
    /// </summary>
    internal static class AndroidClipboardJsonParser
    {
        private const string LogTag = "AndroidClipboardJsonParser";
        private const string UnknownErrorCode = "CLIPBOARD_UNKNOWN";
        private const string NullSentinel = "null";

        [Serializable]
        private sealed class ErrorEnvelopeDto
        {
            public string? error;
            public string? message;
        }

        [Serializable]
        private sealed class ClipItemDto
        {
            public string? text;
            public string? htmlText;
            public string? uri;
            public string? coercedText;
        }

        [Serializable]
        private sealed class ReadResultDto
        {
            public string? label;
            public string[]? mimeTypes;
            public ClipItemDto[]? items;
        }

        [Serializable]
        private sealed class DescriptionDto
        {
            public string? label;
            public string[]? mimeTypes;
            public bool isStyledText;
            // Absent key deserializes to 0; 0 is not a valid CLASSIFICATION_* value, so it means "unavailable".
            public int classificationStatus;
        }

        internal static ClipboardReadResult ParseReadResult(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                Debug.LogError($"[{LogTag}][{nameof(ParseReadResult)}] raw is null or blank.");
                return ClipboardReadResult.Failed(UnknownErrorCode, "Clipboard bridge returned no data.");
            }

            if (raw!.Trim() == NullSentinel)
            {
                return ClipboardReadResult.Empty();
            }

            try
            {
                var envelope = JsonUtility.FromJson<ErrorEnvelopeDto>(raw);
                if (!string.IsNullOrEmpty(envelope.error))
                {
                    return ClipboardReadResult.Failed(envelope.error!, envelope.message ?? string.Empty);
                }

                var dto = JsonUtility.FromJson<ReadResultDto>(raw);
                return ClipboardReadResult.FromContents(ToClipContents(dto));
            }
            catch (ArgumentException ex)
            {
                Debug.LogError($"[{LogTag}][{nameof(ParseReadResult)}] invalid JSON: {ex.Message}");
                return ClipboardReadResult.Failed(UnknownErrorCode, $"Failed: {ex.Message}");
            }
        }

        internal static ClipboardDescriptionResult ParseDescriptionResult(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                Debug.LogError($"[{LogTag}][{nameof(ParseDescriptionResult)}] raw is null or blank.");
                return ClipboardDescriptionResult.Failed(UnknownErrorCode, "Clipboard bridge returned no data.");
            }

            if (raw!.Trim() == NullSentinel)
            {
                return ClipboardDescriptionResult.Empty();
            }

            try
            {
                var envelope = JsonUtility.FromJson<ErrorEnvelopeDto>(raw);
                if (!string.IsNullOrEmpty(envelope.error))
                {
                    return ClipboardDescriptionResult.Failed(envelope.error!, envelope.message ?? string.Empty);
                }

                var dto = JsonUtility.FromJson<DescriptionDto>(raw);
                return ClipboardDescriptionResult.FromDescription(ToClipDescriptionInfo(dto));
            }
            catch (ArgumentException ex)
            {
                Debug.LogError($"[{LogTag}][{nameof(ParseDescriptionResult)}] invalid JSON: {ex.Message}");
                return ClipboardDescriptionResult.Failed(UnknownErrorCode, $"Failed: {ex.Message}");
            }
        }

        internal static bool ParseHasClip(string? raw)
        {
            if (bool.TryParse(raw, out bool result))
            {
                return result;
            }

            Debug.LogWarning($"[{LogTag}][{nameof(ParseHasClip)}] unexpected value, defaulting to false.");
            return false;
        }

        private static ClipContents ToClipContents(ReadResultDto dto)
        {
            var items = new List<ClipItem>();
            if (dto.items != null)
            {
                foreach (var itemDto in dto.items)
                {
                    items.Add(new ClipItem(
                        NormalizeString(itemDto.text),
                        NormalizeString(itemDto.htmlText),
                        NormalizeString(itemDto.uri),
                        NormalizeString(itemDto.coercedText)));
                }
            }

            return new ClipContents(NormalizeString(dto.label), NormalizeStringArray(dto.mimeTypes), items);
        }

        private static ClipDescriptionInfo ToClipDescriptionInfo(DescriptionDto dto)
        {
            int? classificationStatus = dto.classificationStatus == 0 ? null : dto.classificationStatus;
            return new ClipDescriptionInfo(NormalizeString(dto.label), NormalizeStringArray(dto.mimeTypes), dto.isStyledText, classificationStatus);
        }

        private static string? NormalizeString(string? value) => string.IsNullOrEmpty(value) ? null : value;

        private static IReadOnlyList<string> NormalizeStringArray(string[]? values) => values ?? Array.Empty<string>();
    }
}
