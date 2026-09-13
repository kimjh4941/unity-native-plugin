#nullable enable

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
namespace JonghyunKim.NativeToolkit.Runtime.Clipboard
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// State of one entry in <see cref="WindowsClipboardRequestTable"/>.
    /// </summary>
    internal enum WindowsClipboardRequestState
    {
        /// <summary>Accepted by the native layer; its completion callback has not arrived yet.</summary>
        AwaitingNative,

        /// <summary>The outcome is known but has not been handed to a callback or event yet.</summary>
        Undelivered
    }

    /// <summary>
    /// Tracks asynchronous clipboard requests from acceptance until their result has actually been
    /// delivered.
    /// <para>
    /// Removing an entry when the native completion arrives is not enough: the delivery itself is
    /// queued on the main-thread dispatcher, and a teardown between the completion and the next
    /// Update would drop a result that no drain can find any more. Entries therefore stay in the
    /// table through an Undelivered state, and every delivery path has to win
    /// <see cref="TryClaim"/> first, which is what makes delivery exactly-once.
    /// </para>
    /// <para>
    /// Pre-acceptance rejections are tracked as well. They carry no native request id, so the
    /// ticket issued here is the only handle a teardown can drain them by.
    /// </para>
    /// <para>
    /// Not thread-safe by design: every caller runs on the Unity main thread (design 7.2).
    /// </para>
    /// </summary>
    internal sealed class WindowsClipboardRequestTable
    {
        private sealed class Entry
        {
            internal WindowsClipboardRequestState State;
            internal uint NativeRequestId;
        }

        private static readonly KeyValuePair<uint, WindowsClipboardRequestState>[] NothingClaimed =
            Array.Empty<KeyValuePair<uint, WindowsClipboardRequestState>>();

        private readonly Dictionary<uint, Entry> _entries = new();
        private readonly Dictionary<uint, uint> _byNativeId = new();
        private uint _nextTicket;

        /// <summary>Number of entries that have not been delivered yet.</summary>
        internal int Count => _entries.Count;

        /// <summary>
        /// Issues a ticket. Tickets are never zero, so zero can stand for "no ticket".
        /// </summary>
        /// <returns>A ticket that no live entry uses.</returns>
        internal uint IssueTicket()
        {
            // Wrapping is only reachable after four billion requests in one session, but a reused
            // ticket would deliver one request's result to another, so skip zero and any ticket
            // still in flight rather than trusting the counter.
            do
            {
                _nextTicket++;
            }
            while (_nextTicket == 0 || _entries.ContainsKey(_nextTicket));
            return _nextTicket;
        }

        /// <summary>
        /// Registers a request the native layer accepted.
        /// </summary>
        /// <param name="ticket">Ticket from <see cref="IssueTicket"/>.</param>
        /// <param name="nativeRequestId">Non-zero id returned by the native call.</param>
        internal void RegisterAwaitingNative(uint ticket, uint nativeRequestId)
        {
            // Re-registering a live ticket under a new native id would strand the old mapping,
            // which nothing would ever remove.
            if (_entries.TryGetValue(ticket, out Entry? previous) && previous.NativeRequestId != 0)
            {
                _byNativeId.Remove(previous.NativeRequestId);
            }
            _entries[ticket] = new Entry
            {
                State = WindowsClipboardRequestState.AwaitingNative,
                NativeRequestId = nativeRequestId
            };
            _byNativeId[nativeRequestId] = ticket;
        }

        /// <summary>
        /// Registers an outcome that is already known, such as a pre-acceptance rejection or a
        /// completion whose delivery has just been queued.
        /// </summary>
        /// <param name="ticket">Ticket from <see cref="IssueTicket"/>.</param>
        internal void RegisterUndelivered(uint ticket)
        {
            if (_entries.TryGetValue(ticket, out Entry? existing))
            {
                existing.State = WindowsClipboardRequestState.Undelivered;
                return;
            }
            _entries[ticket] = new Entry { State = WindowsClipboardRequestState.Undelivered };
        }

        /// <summary>
        /// Moves an accepted request to Undelivered once its native completion has arrived.
        /// </summary>
        /// <param name="ticket">The ticket to transition.</param>
        /// <returns>False when no entry is tracked for this ticket, delivered or never registered.</returns>
        internal bool MarkUndelivered(uint ticket)
        {
            if (!_entries.TryGetValue(ticket, out Entry? entry)) return false;
            entry.State = WindowsClipboardRequestState.Undelivered;
            return true;
        }

        /// <summary>
        /// Resolves a native request id to the ticket registered for it.
        /// </summary>
        /// <param name="nativeRequestId">Id carried by the native completion callback.</param>
        /// <param name="ticket">The ticket, or zero when the id is unknown.</param>
        /// <returns>False for an unknown id, which a caller must treat as harmless.</returns>
        internal bool TryResolveNativeId(uint nativeRequestId, out uint ticket) =>
            _byNativeId.TryGetValue(nativeRequestId, out ticket);

        /// <summary>
        /// Reads the state of an entry.
        /// </summary>
        /// <param name="ticket">The ticket to inspect.</param>
        /// <param name="state">The state when the entry is still present.</param>
        /// <returns>False when the entry has already been claimed.</returns>
        internal bool TryGetState(uint ticket, out WindowsClipboardRequestState state)
        {
            if (_entries.TryGetValue(ticket, out Entry? entry))
            {
                state = entry.State;
                return true;
            }
            state = default;
            return false;
        }

        /// <summary>
        /// Takes ownership of delivering one entry.
        /// </summary>
        /// <param name="ticket">The ticket to claim.</param>
        /// <returns>True exactly once per ticket; false for every later or racing attempt.</returns>
        internal bool TryClaim(uint ticket)
        {
            if (!_entries.TryGetValue(ticket, out Entry? entry)) return false;
            _entries.Remove(ticket);

            // Only if the mapping still points here. An entry keeps its native id after it is
            // marked undelivered, so the id can already have been handed to a later request; a
            // blind removal would drop that request's mapping and its completion would arrive as
            // an unknown id and be discarded.
            if (entry.NativeRequestId != 0 &&
                _byNativeId.TryGetValue(entry.NativeRequestId, out uint owner) &&
                owner == ticket)
            {
                _byNativeId.Remove(entry.NativeRequestId);
            }
            return true;
        }

        /// <summary>
        /// Claims every remaining entry at once, for a teardown that has to deliver synchronously.
        /// </summary>
        /// <returns>
        /// The claimed tickets with the state they held. A second call returns an empty list, so
        /// draining twice is harmless.
        /// </returns>
        internal IReadOnlyList<KeyValuePair<uint, WindowsClipboardRequestState>> ClaimAll()
        {
            // The teardown drain is idempotent and may run several times, so the empty case must
            // not allocate.
            if (_entries.Count == 0) return NothingClaimed;

            var claimed = new List<KeyValuePair<uint, WindowsClipboardRequestState>>(_entries.Count);
            foreach (KeyValuePair<uint, Entry> pair in _entries)
            {
                claimed.Add(new KeyValuePair<uint, WindowsClipboardRequestState>(pair.Key, pair.Value.State));
            }
            _entries.Clear();
            _byNativeId.Clear();
            return claimed;
        }

        /// <summary>
        /// Drops every entry without delivering, for a static reset between tests.
        /// </summary>
        /// <remarks>
        /// The ticket counter is deliberately not rewound. A delivery queued on the dispatcher
        /// before the reset still holds its ticket, and restarting the numbering would let that
        /// stale delivery claim an unrelated request registered after the reset.
        /// </remarks>
        internal void Reset()
        {
            _entries.Clear();
            _byNativeId.Clear();
        }
    }
}
#endif
