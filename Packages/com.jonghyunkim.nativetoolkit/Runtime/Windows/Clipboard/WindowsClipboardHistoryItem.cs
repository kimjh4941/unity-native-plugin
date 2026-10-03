#nullable enable

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
namespace JonghyunKim.NativeToolkit.Runtime.Windows.Clipboard
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
        /// Formats the item offers, as the native side reports them.
        /// Never null; empty when the item reports none.
        /// </summary>
        public IReadOnlyList<string> ContentTypes { get; }

        /// <summary>
        /// Capture time in 100ns FILETIME ticks since 1601, 0 when unknown. Since native-toolkit
        /// 2.0.0 the native side reports milliseconds, so the last four digits are always 0.
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
            // Copied rather than stored directly: whatever was handed in may still be an array
            // its caller can reach through a cast, and this is meant to be the payload as it
            // arrived rather than a view of something that can still change.
            ContentTypes = contentTypes == null || contentTypes.Count == 0
                ? NoContentTypes
                : Array.AsReadOnly(System.Linq.Enumerable.ToArray(contentTypes));
            Timestamp = timestamp;
        }
    }
}
#endif
