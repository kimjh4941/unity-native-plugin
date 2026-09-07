#nullable enable

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
using System.Collections.Generic;
using JonghyunKim.NativeToolkit.Runtime.Clipboard;
using NUnit.Framework;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// EditMode tests for the completion registry that backs the asynchronous clipboard history
    /// APIs. These cover the exactly-once delivery contract of design 7.6.1 and 8.4, including the
    /// case a plain "remove on completion" table cannot express: a completion that is known but
    /// has not been delivered when a teardown drains the registry.
    /// </summary>
    public sealed class WindowsClipboardRequestTableTests
    {
        private WindowsClipboardRequestTable _table = null!;

        [SetUp]
        public void SetUp() => _table = new WindowsClipboardRequestTable();

        [Test]
        public void IssueTicket_NeverReturnsZeroAndNeverRepeatsALiveTicket()
        {
            var seen = new HashSet<uint>();
            for (int i = 0; i < 100; i++)
            {
                uint ticket = _table.IssueTicket();
                Assert.AreNotEqual(0u, ticket);
                Assert.IsTrue(seen.Add(ticket), "a live ticket was handed out twice");
                _table.RegisterAwaitingNative(ticket, (uint)(i + 1));
            }
        }

        [Test]
        public void RegisterAwaitingNative_IsResolvableByNativeId()
        {
            uint ticket = _table.IssueTicket();
            _table.RegisterAwaitingNative(ticket, 42);

            Assert.IsTrue(_table.TryResolveNativeId(42, out uint resolved));
            Assert.AreEqual(ticket, resolved);
            Assert.IsTrue(_table.TryGetState(ticket, out WindowsClipboardRequestState state));
            Assert.AreEqual(WindowsClipboardRequestState.AwaitingNative, state);
        }

        [Test]
        public void TryResolveNativeId_UnknownIdIsHarmless()
        {
            Assert.IsFalse(_table.TryResolveNativeId(999, out uint ticket));
            Assert.AreEqual(0u, ticket);
        }

        [Test]
        public void MarkUndelivered_MovesTheEntryWithoutRemovingIt()
        {
            uint ticket = _table.IssueTicket();
            _table.RegisterAwaitingNative(ticket, 7);

            Assert.IsTrue(_table.MarkUndelivered(ticket));
            Assert.IsTrue(_table.TryGetState(ticket, out WindowsClipboardRequestState state));
            Assert.AreEqual(WindowsClipboardRequestState.Undelivered, state);
            Assert.AreEqual(1, _table.Count, "the entry must stay until it is actually delivered");
        }

        [Test]
        public void TryClaim_SucceedsExactlyOnce()
        {
            uint ticket = _table.IssueTicket();
            _table.RegisterAwaitingNative(ticket, 7);
            _table.MarkUndelivered(ticket);

            Assert.IsTrue(_table.TryClaim(ticket), "the first delivery path must win");
            Assert.IsFalse(_table.TryClaim(ticket), "a second delivery must be a no-op");
            Assert.AreEqual(0, _table.Count);
        }

        [Test]
        public void TryClaim_AlsoReleasesTheNativeIdMapping()
        {
            uint ticket = _table.IssueTicket();
            _table.RegisterAwaitingNative(ticket, 7);

            Assert.IsTrue(_table.TryClaim(ticket));
            Assert.IsFalse(_table.TryResolveNativeId(7, out _));
        }

        [Test]
        public void RegisterUndelivered_TracksAPreAcceptanceRejectionThatHasNoNativeId()
        {
            uint ticket = _table.IssueTicket();
            _table.RegisterUndelivered(ticket);

            Assert.IsTrue(_table.TryGetState(ticket, out WindowsClipboardRequestState state));
            Assert.AreEqual(WindowsClipboardRequestState.Undelivered, state);
            Assert.IsTrue(_table.TryClaim(ticket));
        }

        [Test]
        public void ClaimAll_DrainsAwaitingAndUndeliveredAlike()
        {
            uint awaiting = _table.IssueTicket();
            _table.RegisterAwaitingNative(awaiting, 1);

            uint completed = _table.IssueTicket();
            _table.RegisterAwaitingNative(completed, 2);
            _table.MarkUndelivered(completed);

            uint rejected = _table.IssueTicket();
            _table.RegisterUndelivered(rejected);

            IReadOnlyList<KeyValuePair<uint, WindowsClipboardRequestState>> claimed = _table.ClaimAll();

            Assert.AreEqual(3, claimed.Count);
            Assert.AreEqual(0, _table.Count);
            var states = new Dictionary<uint, WindowsClipboardRequestState>();
            foreach (KeyValuePair<uint, WindowsClipboardRequestState> pair in claimed) states[pair.Key] = pair.Value;
            Assert.AreEqual(WindowsClipboardRequestState.AwaitingNative, states[awaiting]);
            Assert.AreEqual(WindowsClipboardRequestState.Undelivered, states[completed]);
            Assert.AreEqual(WindowsClipboardRequestState.Undelivered, states[rejected]);
        }

        [Test]
        public void ClaimAll_IsIdempotent()
        {
            uint ticket = _table.IssueTicket();
            _table.RegisterAwaitingNative(ticket, 1);

            Assert.AreEqual(1, _table.ClaimAll().Count);
            Assert.AreEqual(0, _table.ClaimAll().Count, "a second drain must be harmless");
        }

        [Test]
        public void ADeliveryQueuedBeforeADrain_BecomesANoOpAfterIt()
        {
            // The dispatcher has the delivery queued, then a teardown drains the registry, then the
            // queued action finally runs. It must not deliver a second time.
            uint ticket = _table.IssueTicket();
            _table.RegisterAwaitingNative(ticket, 1);
            _table.MarkUndelivered(ticket);

            _table.ClaimAll();

            Assert.IsFalse(_table.TryClaim(ticket));
        }

        [Test]
        public void MarkUndelivered_AfterAClaimReportsFailureInsteadOfResurrecting()
        {
            uint ticket = _table.IssueTicket();
            _table.RegisterAwaitingNative(ticket, 1);
            _table.TryClaim(ticket);

            Assert.IsFalse(_table.MarkUndelivered(ticket));
            Assert.AreEqual(0, _table.Count);
        }

        [Test]
        public void Reset_DropsEverythingWithoutDelivering()
        {
            uint ticket = _table.IssueTicket();
            _table.RegisterAwaitingNative(ticket, 1);

            _table.Reset();

            Assert.AreEqual(0, _table.Count);
            Assert.IsFalse(_table.TryClaim(ticket));
        }
    }
}
#endif
