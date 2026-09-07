#nullable enable

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
namespace JonghyunKim.NativeToolkit.Runtime.Clipboard
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// One entry of the Windows clipboard history.
    /// </summary>
    public sealed class WindowsClipboardHistoryItem
    {
        private static readonly IReadOnlyList<string> NoContentTypes = Array.Empty<string>();

        /// <summary>Opaque id used by RestoreHistoryItem and DeleteHistoryItem.</summary>
        public string Id { get; }

        /// <summary>Plain text of the item, or null when the item holds no text.</summary>
        public string? Text { get; }

        /// <summary>
        /// Formats the item offers, taken verbatim from the native payload.
        /// Never null; empty when the payload omitted the key.
        /// </summary>
        public IReadOnlyList<string> ContentTypes { get; }

        /// <summary>
        /// Capture time in 100ns FILETIME ticks since 1601, as sent by the native layer.
        /// The payload carries it as a decimal string because a JSON number cannot hold the
        /// full int64 range without loss.
        /// </summary>
        public long Timestamp { get; }

        /// <summary>
        /// Converts the capture time to UTC.
        /// </summary>
        /// <returns>The converted time, or null when the raw value is outside the valid range.</returns>
        public DateTimeOffset? ToUtcTime()
        {
            try
            {
                return DateTimeOffset.FromFileTime(Timestamp).ToUniversalTime();
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
        }

        internal WindowsClipboardHistoryItem(
            string id, string? text, IReadOnlyList<string>? contentTypes, long timestamp)
        {
            Id = id;
            Text = text;
            ContentTypes = contentTypes ?? NoContentTypes;
            Timestamp = timestamp;
        }
    }
}
#endif
