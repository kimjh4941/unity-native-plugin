#nullable enable

#if UNITY_STANDALONE_OSX || UNITY_EDITOR
// Intentional deviation from the "log every public method" rule in csharp.md: these result types
// carry clipboard content and native error detail. The factories are pure value constructors, so
// they emit no logs at all rather than a shape-only line. The Manager already logs operation,
// success and error code at the dispatch boundary.

namespace JonghyunKim.NativeToolkit.Runtime.Clipboard
{
    /// <summary>
    /// One pasteboard change reported while observation is running.
    /// <para>
    /// An event, not a result: it carries no success flag. Polling is suspended while the app is
    /// inactive and catches up when it becomes active again, so a change made by another app
    /// arrives on return to the foreground.
    /// </para>
    /// </summary>
    public sealed class MacClipboardChangeEvent
    {
        /// <summary>Pasteboard that changed.</summary>
        public MacPasteboardScope Scope { get; }

        /// <summary>Change count observed. 64-bit; see <see cref="MacPasteboardOwnership.ChangeCount"/>.</summary>
        public long ChangeCount { get; }

        internal MacClipboardChangeEvent(MacPasteboardScope scope, long changeCount)
        {
            Scope = scope;
            ChangeCount = changeCount;
        }
    }
}
#endif
