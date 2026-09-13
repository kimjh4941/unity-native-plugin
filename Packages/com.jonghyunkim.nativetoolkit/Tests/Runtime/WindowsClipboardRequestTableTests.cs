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

        [Test]
        public void TryClaim_LeavesANativeIdThatAlreadyBelongsToAnotherRequest()
        {
            // An entry keeps its native id after it is marked undelivered, so the native side can
            // hand the same id to a later request before the first one's queued delivery runs.
            // Removing the mapping blindly would drop the second request's completion.
            var table = new WindowsClipboardRequestTable();
            uint first = table.IssueTicket();
            uint second = table.IssueTicket();
            table.RegisterAwaitingNative(first, 500);
            table.MarkUndelivered(first);
            table.RegisterAwaitingNative(second, 500);

            Assert.IsTrue(table.TryClaim(first));

            Assert.IsTrue(table.TryResolveNativeId(500, out uint owner),
                "the later request still needs its mapping");
            Assert.AreEqual(second, owner);
        }

        [Test]
        public void TryClaim_StillReleasesANativeIdThatIsStillItsOwn()
        {
            var table = new WindowsClipboardRequestTable();
            uint ticket = table.IssueTicket();
            table.RegisterAwaitingNative(ticket, 501);

            Assert.IsTrue(table.TryClaim(ticket));

            Assert.IsFalse(table.TryResolveNativeId(501, out _),
                "nothing owns this id any more");
        }


        [Test]
        public void Reset_DoesNotRewindTheTicketCounter()
        {
            // A delivery queued before the reset still carries its ticket. Restarting the numbering
            // would let that stale delivery claim a request registered after the reset and hand one
            // caller another caller's result.
            var table = new WindowsClipboardRequestTable();
            uint before = table.IssueTicket();
            table.RegisterUndelivered(before);

            table.Reset();
            uint after = table.IssueTicket();

            Assert.Greater(after, before, "a ticket may never be handed out twice in a session");
        }

        [Test]
        public void IssueTicket_NeverHandsOutZero()
        {
            // Zero is what the public API returns for "not accepted", so a ticket of zero would be
            // indistinguishable from a request that never started.
            var table = new WindowsClipboardRequestTable();

            for (int i = 0; i < 200; i++)
            {
                Assert.AreNotEqual(0u, table.IssueTicket());
            }
        }

        [Test]
        public void RegisterAwaitingNative_Twice_LeavesNoMappingBehindForTheOldId()
        {
            // Nothing would ever remove the first mapping, and a late completion arriving under the
            // old id would then be handed to a request that has moved on.
            var table = new WindowsClipboardRequestTable();
            uint ticket = table.IssueTicket();
            table.RegisterAwaitingNative(ticket, 600);

            table.RegisterAwaitingNative(ticket, 601);

            Assert.IsFalse(table.TryResolveNativeId(600, out _), "the old id is stranded");
            Assert.IsTrue(table.TryResolveNativeId(601, out uint owner));
            Assert.AreEqual(ticket, owner);
        }

    }
}
#endif
