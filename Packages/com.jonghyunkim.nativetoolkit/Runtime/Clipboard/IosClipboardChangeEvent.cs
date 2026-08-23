#nullable enable

#if UNITY_IOS || UNITY_EDITOR
namespace JonghyunKim.NativeToolkit.Runtime.Clipboard
{
    using System.Collections.Generic;

    /// <summary>Kind of clipboard change reported while observing.</summary>
    public enum IosClipboardChangeEventKind
    {
        /// <summary>The system posted a pasteboard change notification.</summary>
        Changed,

        /// <summary>A change was detected on foreground return by comparing the change count.</summary>
        ChangedDetectedOnForeground,

        /// <summary>The observed named pasteboard was removed.</summary>
        Removed,

        /// <summary>
        /// A kind this version does not recognize. The native layer emitted it deliberately, so it
        /// is delivered rather than dropped. Events that fail to parse are dropped instead and
        /// never surface as <c>Unknown</c>.
        /// </summary>
        Unknown
    }

    /// <summary>
    /// A clipboard change event delivered through <see cref="IosClipboardManager.ClipboardChanged"/>
    /// while observation is active.
    /// </summary>
    public sealed class IosClipboardChangeEvent
    {
        /// <summary>What happened. Always present: an event without a kind is dropped, not delivered.</summary>
        public IosClipboardChangeEventKind Kind { get; }

        /// <summary>
        /// Scope the event refers to, or <c>null</c> when the native layer omitted it or reported
        /// it in an unrecognized shape. The kind remains actionable in that case.
        /// </summary>
        public IosPasteboardScope? Scope { get; }

        /// <summary>Representation types added. Never null; empty unless <see cref="Kind"/> is <see cref="IosClipboardChangeEventKind.Changed"/>.</summary>
        public IReadOnlyList<string> TypesAdded { get; }

        /// <summary>Representation types removed. Never null; empty unless <see cref="Kind"/> is <see cref="IosClipboardChangeEventKind.Changed"/>.</summary>
        public IReadOnlyList<string> TypesRemoved { get; }

        internal IosClipboardChangeEvent(
            IosClipboardChangeEventKind kind,
            IosPasteboardScope? scope,
            IReadOnlyList<string> typesAdded,
            IReadOnlyList<string> typesRemoved)
        {
            Kind = kind;
            Scope = scope;
            TypesAdded = typesAdded;
            TypesRemoved = typesRemoved;
        }
    }
}
#endif
