// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT
//
// Regression tests for the fourteen defects found by the round-2 adversarial review
// (reviews/102-quic-federation-transport/20260904T140111Z).
//
// WHY THIS FILE EXISTS AT ALL. Round 1 of the same review, on the same branch with the same scope
// and the same knobs, returned ONE finding. Round 2 returned fourteen — eleven of them P1 — in code
// that a 278-test suite was already passing. The suite was not merely incomplete; it was green
// ACROSS every one of those defects. So each fix below carries a test that FAILS against the
// pre-fix behaviour, and several carry a positive control proving the test can fail at all.
//
// The estate's standing rule applies here in full: a green self-written suite is not evidence. A
// test that would pass whether or not the fix is present measures nothing.

using System.Net;
using System.Text;
using System.Text.Json;
using GlpRuntime.CrdtMsg.Crdt;
using GlpRuntime.CrdtMsg.Federation;
using GlpRuntime.CrdtMsg.Route;
using Xunit;

namespace GlpRuntime.CrdtMsg.Tests.Federation;

public sealed class Round2RegressionTests
{
    private const string LiveEpoch = "ynet-epoch-7f3a91c2e04b5d68";   // no wall clock: FR-026 applies to fixtures too
    private static readonly JsonElement Body = JsonSerializer.SerializeToElement(new { });

    private static string NodeId(string seed) =>
        Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(seed)));

    private static FederationConfig Cfg(params PeerConfig[] peers) => new()
    {
        Enabled = true,
        BoardRootPath = "D:/coop/buildkit/sched",
        BoardActor = "gavriella",
        BindAddress = "0.0.0.0",
        BindPort = 47890,
        SpaceId = LiveEpoch,
        Peers = peers.ToList(),
    };

    private static PeerConfig Peer(string name, string seed, params string[] endpoints)
    {
        string id = NodeId(seed);
        return new PeerConfig
        {
            Name = name, NodeId = id, Endpoints = endpoints.ToList(),
            Pin = NodeIdentityStore.PinFromNodeId(id),
        };
    }

    private static FederationFold NewFold() => new(new TermSpaceRegistry(LiveEpoch));

    private static FederationOp Op(string peer, long counter) =>
        FederationOp.Create(new Dot(peer, counter), peer, "board_post", Body);

    // ==========================================================================================
    // F1 — encode-configured-spki-pins-as-base64
    // ==========================================================================================

    /// <summary>
    /// The node id and the transport pin are the SAME 32 bytes in two encodings. Assigning one to
    /// the other — which `config add-peer` did — refuses every correctly-configured peer at the TLS
    /// callback, presenting a configuration fault as a security event.
    /// </summary>
    [Fact]
    public void ThePinIsBase64OfTheSameBytesTheNodeIdIsHexOf()
    {
        using var cert = QuicLinkTransport.CreateDevCert("gavriella");
        string nodeId = NodeIdentityStore.DeriveNodeId(cert);

        // What the TLS callback ACTUALLY compares against — the shipped glp_link discipline.
        string transportPin = QuicLinkTransport.SpkiPin(cert);

        Assert.Equal(transportPin, NodeIdentityStore.PinFromNodeId(nodeId));
        Assert.Equal(nodeId, NodeIdentityStore.NodeIdFromPin(transportPin));

        // The positive control: the two ENCODINGS genuinely differ, so this test could fail.
        Assert.NotEqual(nodeId, transportPin);
    }

    /// <summary>Configuring the node id as the pin is REFUSED, not silently accepted.</summary>
    [Fact]
    public void APinThatIsActuallyANodeIdIsRefused()
    {
        string id = NodeId("olamnit");
        var bad = Cfg(new PeerConfig
        {
            Name = "olamnit", NodeId = id, Endpoints = { "192.168.0.136:47890" },
            Pin = id,   // the exact defect: hex where base64 is required
        });

        Assert.Contains(bad.Validate(), p => p.Contains("pin"));
        Assert.False(bad.IsValid);
    }

    /// <summary>An omitted pin is DERIVED, which removes the operator's chance to get it wrong.</summary>
    [Fact]
    public void AnOmittedPinIsDerivedFromTheNodeId()
    {
        string id = NodeId("olamnit");
        var cfg = Cfg(new PeerConfig { Name = "olamnit", NodeId = id, Endpoints = { "192.168.0.136:47890" } });

        Assert.True(cfg.IsValid);
        Assert.Equal(NodeIdentityStore.PinFromNodeId(id), cfg.ToPeerSet().Find(id)!.Pin);
    }

    // ==========================================================================================
    // F2 — key-transport-peers-by-node-id
    // ==========================================================================================

    /// <summary>
    /// The transport's pin-table key, its dial key and its hello value must be ONE string. Keying
    /// the table by the human name while the service dialled by node id made the accept-side lookup
    /// miss and the dial-side remote-name check fail for otherwise valid peers.
    /// </summary>
    [Fact]
    public void ThePinTableIsKeyedByNodeIdNotByName()
    {
        string id = NodeId("olamnit");
        var table = Cfg(Peer("olamnit", "olamnit", "192.168.0.136:47890")).ToPeerSet().ToPinTable();

        Assert.True(table.ContainsKey(id));
        Assert.False(table.ContainsKey("olamnit"));   // the name is a LABEL, never a key
    }

    /// <summary>The service dials using the same string the pin table is keyed by.</summary>
    [Fact]
    public async Task TheServiceDialsByNodeId()
    {
        string id = NodeId("olamnit");
        var link = new RecordingLink("A");
        var svc = new FederationService(Cfg(Peer("olamnit", "olamnit", "192.168.0.136:47890")),
                                        link, NewFold(), new InMemoryBoardLog());

        Assert.Equal(AdmissionOutcome.Admitted, await svc.DialAsync(id));
        Assert.Equal(id, link.Dialled.Single().Peer);
    }

    /// <summary>And it PUSHES to that same key — a send keyed by name reaches no connection.</summary>
    [Fact]
    public async Task TheServicePushesToTheNodeIdKey()
    {
        string id = NodeId("olamnit");
        var link = new RecordingLink("A");
        var svc = new FederationService(Cfg(Peer("olamnit", "olamnit", "192.168.0.136:47890")),
                                        link, NewFold(), new InMemoryBoardLog());
        await svc.DialAsync(id);
        // R13-02: outbound board data is gated on a current declaration, so the peer has
        // to declare — as a real one does — before a push is expected.
        link.PushInbound(new LinkInbound(id,
            HelloProtocol.Encode(new PeerCapabilities(true, LiveEpoch), isReply: true),
            HelloProtocol.Box));
        await svc.ReceiveOneAsync();
        await svc.AppendAndPushAsync(Op(id, 1));

        Assert.Equal(id, link.Sent.Single(s => s.Box == FederationService.BoardBox).To);
    }

    // ==========================================================================================
    // F5 — reconnect-peers-before-issuing-reconciliation-pulls
    // ==========================================================================================

    /// <summary>
    /// A peer whose FIRST dial failed must not be skipped forever. Before the fix the pull loop only
    /// pulled from `_admitted`, which a failed dial never entered — so restoring the network
    /// repaired nothing until the process was restarted, in the loop whose whole purpose is repair.
    /// </summary>
    [Fact]
    public async Task ThePullLoopRedialsAPeerWhoseFirstDialFailed()
    {
        string id = NodeId("olamnit");
        var link = new RecordingLink("A") { Broken = true };
        var cfg = Cfg(Peer("olamnit", "olamnit", "192.168.0.136:47890")) with { PullIntervalSeconds = 1 };
        var clock = new DrivableClock();
        var svc = new FederationService(cfg, link, NewFold(), new InMemoryBoardLog(), clock);

        Assert.Equal(AdmissionOutcome.Unreachable, await svc.DialAsync(id));   // never admitted

        using var cts = new CancellationTokenSource();
        var loop = svc.RunPullLoopAsync(cts.Token);

        link.Broken = false;                       // the network comes back
        await clock.AdvanceAsync(TimeSpan.FromSeconds(1));
        await clock.AdvanceAsync(TimeSpan.FromSeconds(1));

        cts.Cancel();
        try { await loop; } catch (OperationCanceledException) { }

        Assert.Contains(link.Dialled, d => d.Peer == id);                    // it re-dialled
        Assert.Contains(link.Sent, s => s.Box == PullProtocol.RequestBox);   // and then pulled
    }

    /// <summary>
    /// POSITIVE CONTROL for the test above: while the link stays broken, no pull is ever sent. If
    /// this passed as well, the test above would be proving nothing about reconnection.
    /// </summary>
    [Fact]
    public async Task AStillBrokenPeerYieldsNoPull()
    {
        string id = NodeId("olamnit");
        var link = new RecordingLink("A") { Broken = true };
        var cfg = Cfg(Peer("olamnit", "olamnit", "192.168.0.136:47890")) with { PullIntervalSeconds = 1 };
        var clock = new DrivableClock();
        var svc = new FederationService(cfg, link, NewFold(), new InMemoryBoardLog(), clock);

        await svc.DialAsync(id);

        using var cts = new CancellationTokenSource();
        var loop = svc.RunPullLoopAsync(cts.Token);
        await clock.AdvanceAsync(TimeSpan.FromSeconds(1));
        await clock.AdvanceAsync(TimeSpan.FromSeconds(1));
        cts.Cancel();
        try { await loop; } catch (OperationCanceledException) { }

        Assert.DoesNotContain(link.Sent, s => s.Box == PullProtocol.RequestBox);
    }

    /// <summary>
    /// A link that closes AFTER being established must not stay "connected" forever. Before the fix
    /// the peer remained admitted and every later send went into a connection that no longer
    /// existed, failing silently at each interval.
    /// </summary>
    [Fact]
    public async Task ASendFailureMarksTheLinkDownSoTheNextIntervalRedials()
    {
        string id = NodeId("olamnit");
        var link = new RecordingLink("A");
        var cfg = Cfg(Peer("olamnit", "olamnit", "192.168.0.136:47890")) with { PullIntervalSeconds = 1 };
        var clock = new DrivableClock();
        var svc = new FederationService(cfg, link, NewFold(), new InMemoryBoardLog(), clock);

        Assert.Equal(AdmissionOutcome.Admitted, await svc.DialAsync(id));

        // The peer DECLARES, as a real one does. Without it R13-02 skips the push entirely, so the
        // send never fails, the link is never marked down, and this test would be measuring the
        // outbound gate instead of the re-dial it is named for.
        link.PushInbound(new LinkInbound(id,
            HelloProtocol.Encode(new PeerCapabilities(true, LiveEpoch), isReply: true), HelloProtocol.Box));
        await svc.ReceiveOneAsync();

        int dialsAfterFirst = link.Dialled.Count;

        link.FailSends = true;                                  // the established connection drops
        await svc.AppendAndPushAsync(Op(NodeId("gavriella"), 1));

        link.FailSends = false;
        using var cts = new CancellationTokenSource();
        var loop = svc.RunPullLoopAsync(cts.Token);
        await clock.AdvanceAsync(TimeSpan.FromSeconds(1));
        cts.Cancel();
        try { await loop; } catch (OperationCanceledException) { }

        Assert.True(link.Dialled.Count > dialsAfterFirst, "the closed link was never re-dialled");
    }

    // ==========================================================================================
    // F6 — preserve-holes-in-the-reconciliation-frontier
    // ==========================================================================================

    /// <summary>
    /// THE ABSORBING DEFECT. With a max-merge vector, receiving op 7 while op 6 was lost makes
    /// Contains(6) answer true, the frontier advertises 6 as held, the peer computes "nothing
    /// missing" and op 6 is suppressed for the life of the board.
    /// </summary>
    [Fact]
    public void AFrontierWithAHoleDoesNotClaimTheMissingOp()
    {
        var f = new FederationFrontier().With(new Dot("g", 5)).With(new Dot("g", 7));

        Assert.False(f.Contains(new Dot("g", 6)));   // the hole is VISIBLE
        Assert.True(f.Contains(new Dot("g", 5)));
        Assert.True(f.Contains(new Dot("g", 7)));

        // POSITIVE CONTROL: the shared VersionVector — reused unchanged elsewhere — is exactly the
        // shape that made this a defect. If it ever stopped claiming the hole, this test would be
        // asserting against nothing.
        var vv = new VersionVector().With(new Dot("g", 5)).With(new Dot("g", 7));
        Assert.True(vv.Contains(new Dot("g", 6)));
    }

    /// <summary>Filling the hole absorbs the run above it, so the frontier stays bounded.</summary>
    [Fact]
    public void FillingAHoleCompactsTheFrontier()
    {
        var f = new FederationFrontier();
        for (long i = 1; i <= 5; i++) f = f.With(new Dot("g", i));
        f = f.With(new Dot("g", 7));

        Assert.Equal(5, f.ContiguousUpTo("g"));
        Assert.Equal(new long[] { 7 }, f.Above("g").ToArray());

        f = f.With(new Dot("g", 6));
        Assert.Equal(7, f.ContiguousUpTo("g"));
        Assert.Empty(f.Above("g"));
    }

    /// <summary>The end-to-end consequence: a peer with a hole is SENT the op it lacks.</summary>
    [Fact]
    public async Task APeerWithAHoleIsSentTheMissingOp()
    {
        var link = new RecordingLink("A");
        var svc = new FederationService(Cfg(), link, NewFold(), new InMemoryBoardLog());
        for (long i = 1; i <= 3; i++) await svc.AppendAndPushAsync(Op("g", i));


        // R14-02: pull responses are gated on the peer's declaration too, so the peer must declare
        // — as a real one does — before board operations are handed to it.
        link.PushInbound(new LinkInbound("peer",
            HelloProtocol.Encode(new PeerCapabilities(true, LiveEpoch), isReply: true), HelloProtocol.Box));
        await svc.ReceiveOneAsync();

        // The requester has 1 and 3 but never received 2.
        var theirs = new FederationFrontier().With(new Dot("g", 1)).With(new Dot("g", 3));
        await svc.AnswerPullAsync("peer", theirs);

        var ops = PullProtocol.DecodeResponse(
            link.Sent.Single(s => s.Box == PullProtocol.ResponseBox).Bytes);
        Assert.Equal(new Dot("g", 2), Assert.Single(ops).OpId);
    }

    // ==========================================================================================
    // F7 — allocate-durable-unique-dot-counters
    // ==========================================================================================

    /// <summary>
    /// A dot counter is an identity allocator, not a timestamp. Two millisecond-clock allocations in
    /// the same millisecond produced the SAME dot for different operations, and the fold — whose job
    /// is to never lose an operation — discarded one of them as a duplicate.
    /// </summary>
    [Fact]
    public void ConcurrentAllocationsNeverCollide()
    {
        string dir = Path.Combine(Path.GetTempPath(), "ynet_seq", Guid.NewGuid().ToString("n")[..8]);
        string path = Path.Combine(dir, "dot.seq");
        try
        {
            var seen = new System.Collections.Concurrent.ConcurrentBag<long>();
            Parallel.For(0, 200, _ =>
                seen.Add(new DotSequencer(path, "g").Next().Counter));

            Assert.Equal(200, seen.Distinct().Count());          // no collisions
            Assert.Equal(Enumerable.Range(1, 200).Select(i => (long)i).OrderBy(x => x),
                         seen.OrderBy(x => x));                  // and CONTIGUOUS, so no false holes
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    /// <summary>The log is the truth: a lost sequence file cannot re-issue a counter already used.</summary>
    [Fact]
    public void ALostSequenceFileCannotReissueACounterAlreadyOnTheBoard()
    {
        string dir = Path.Combine(Path.GetTempPath(), "ynet_seq", Guid.NewGuid().ToString("n")[..8]);
        string path = Path.Combine(dir, "dot.seq");
        try
        {
            var onBoard = new[] { Op("g", 1), Op("g", 2), Op("g", 9) };
            long floor = DotSequencer.HighestFor("g", onBoard);
            Assert.Equal(9, floor);

            // The sequence file has never existed — the exact post-crash / post-reprovision state.
            Assert.Equal(10, new DotSequencer(path, "g", floor).Next().Counter);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    // ==========================================================================================
    // F8 — persist-received-ops-before-exposing-them-in-the-fold
    // ==========================================================================================

    /// <summary>
    /// If the durable append fails AFTER the fold already holds the dot, redelivery is classified as
    /// a duplicate, the append is never retried, and the host serves an operation it never stored.
    /// The fix orders the two: durable first, visible second.
    /// </summary>
    [Fact]
    public async Task AFailedAppendLeavesNothingVisibleInTheFold()
    {
        var fold = NewFold();
        var log = new InMemoryBoardLog { FailNextAppend = true };
        var svc = new FederationService(Cfg(), new RecordingLink("A"), fold, log);

        var op = Op(NodeId("gavriella"), 1);
        await Assert.ThrowsAsync<IOException>(() => svc.AppendAndPushAsync(op));

        Assert.False(fold.Contains(op.OpId));   // NOT visible — so the retry is not a "duplicate"
        Assert.Equal(0, log.Count);

        // And the retry genuinely succeeds, which is the point of not marking it seen.
        await svc.AppendAndPushAsync(op);
        Assert.True(fold.Contains(op.OpId));
        Assert.Equal(1, log.Count);
    }

    /// <summary>The same ordering on the INBOUND path, which had the identical defect.</summary>
    [Fact]
    public async Task AFailedAppendOfAReceivedOpLeavesNothingVisible()
    {
        var fold = NewFold();
        var log = new InMemoryBoardLog { FailNextAppend = true };
        var svc = new FederationService(Cfg(), new RecordingLink("A"), fold, log);

        var op = Op(NodeId("olamnit"), 1);
        await Assert.ThrowsAsync<IOException>(() =>
            svc.ReconcileAsync(new[] { op }, new PeerCapabilities(true, LiveEpoch)));

        Assert.False(fold.Contains(op.OpId));
    }

    // ==========================================================================================
    // F9 — reject-unverifiable-operation-attribution
    // ==========================================================================================

    /// <summary>An op whose three identity fields disagree is a fault, not a merge (FR-009).</summary>
    [Fact]
    public void ContradictoryAttributionIsRefused()
    {
        var op = FederationOp.Create(new Dot("alice", 1), "bob", "board_post", Body);
        var r = OpAttribution.Check(op, null);

        Assert.Equal(AttributionVerdict.Inconsistent, r.Verdict);
        Assert.Contains("disagrees", r.Reason);
    }

    /// <summary>A forged leadership identity in the term is refused — it is monotone and unfixable.</summary>
    [Fact]
    public void AForgedTermHostIsRefused()
    {
        var op = FederationOp.Create(new Dot("alice", 1), "alice", "claim", Body,
            term: new Term(LiveEpoch, 3, "bob"));   // claims alice's dot, bob's leadership identity

        Assert.Equal(AttributionVerdict.Inconsistent, OpAttribution.Check(op, null).Verdict);
    }

    /// <summary>Blank attribution is refused too — absent is not "unspecified but fine".</summary>
    [Fact]
    public void BlankAttributionIsRefused()
    {
        Assert.Equal(AttributionVerdict.Inconsistent,
            OpAttribution.Check(FederationOp.Create(new Dot("a", 1), "", "board_post", Body), null).Verdict);
    }

    /// <summary>
    /// A signature by the wrong key is a FORGERY, distinct from an unproven origin. This is the one
    /// the consistency gate alone cannot catch: an admitted peer forging an op in ANOTHER admitted
    /// peer's name has perfectly self-consistent fields.
    /// </summary>
    [Fact]
    public void AnOpSignedByTheWrongKeyIsRejectedAsAForgery()
    {
        using var alice = QuicLinkTransport.CreateDevCert("alice");
        using var mallory = QuicLinkTransport.CreateDevCert("mallory");
        string aliceId = NodeIdentityStore.DeriveNodeId(alice);

        // Mallory writes an op in Alice's name — self-consistent, and signed with her own key.
        var forged = FederationOp.Create(new Dot(aliceId, 1), aliceId, "claim", Body).SignedBy(mallory);
        Assert.Equal(AttributionVerdict.SignatureInvalid,
            OpAttribution.Check(forged, NodeIdentityStore.ExportSpki(alice)).Verdict);

        // POSITIVE CONTROL: Alice's own op over the same fields verifies, so the check can pass.
        var genuine = FederationOp.Create(new Dot(aliceId, 1), aliceId, "claim", Body).SignedBy(alice);
        Assert.Equal(AttributionVerdict.Verified,
            OpAttribution.Check(genuine, NodeIdentityStore.ExportSpki(alice)).Verdict);
    }

    /// <summary>An unpublished key yields UNVERIFIED — a third state, never silently "valid".</summary>
    [Fact]
    public void AnUnpublishedKeyYieldsUnverifiedNotValid()
    {
        string id = NodeId("alice");
        var op = FederationOp.Create(new Dot(id, 1), id, "board_post", Body);

        Assert.Equal(AttributionVerdict.UnverifiedOrigin, OpAttribution.Check(op, null).Verdict);
    }

    /// <summary>A signature covers the bytes that cross the wire, minus the signature itself.</summary>
    [Fact]
    public void TamperingWithAnySignedFieldInvalidatesTheSignature()
    {
        using var alice = QuicLinkTransport.CreateDevCert("alice");
        string id = NodeIdentityStore.DeriveNodeId(alice);
        string spki = NodeIdentityStore.ExportSpki(alice);

        var op = FederationOp.Create(new Dot(id, 1), id, "claim", Body,
            term: new Term(LiveEpoch, 3, id)).SignedBy(alice);
        Assert.Equal(AttributionVerdict.Verified, OpAttribution.Check(op, spki).Verdict);

        // Raise the term — the exact edit that would steal leadership — keeping the signature.
        var tampered = op with { Term = new Term(LiveEpoch, 999, id) };
        Assert.Equal(AttributionVerdict.SignatureInvalid, OpAttribution.Check(tampered, spki).Verdict);
    }

    /// <summary>A forged op never reaches the fold OR the log, on either inbound path.</summary>
    [Fact]
    public async Task AForgedOpIsRefusedBeforeItIsFoldedOrStored()
    {
        using var alice = QuicLinkTransport.CreateDevCert("alice");
        using var mallory = QuicLinkTransport.CreateDevCert("mallory");
        string aliceId = NodeIdentityStore.DeriveNodeId(alice);

        var cfg = Cfg(new PeerConfig
        {
            Name = "alice", NodeId = aliceId, Endpoints = { "192.168.0.142:47890" },
            Pin = NodeIdentityStore.PinFromNodeId(aliceId),
            Spki = NodeIdentityStore.ExportSpki(alice),
        });

        var fold = NewFold();
        var log = new InMemoryBoardLog();
        var svc = new FederationService(cfg, new RecordingLink("A"), fold, log);

        AttributionRefusedException? reported = null;
        svc.OnRefusal += ex => reported = ex;

        var forged = FederationOp.Create(new Dot(aliceId, 1), aliceId, "claim", Body).SignedBy(mallory);
        Assert.Equal(0, await svc.ReconcileAsync(new[] { forged }, new PeerCapabilities(true, LiveEpoch)));

        // Refused, reported and COUNTED — but the batch is not aborted (see the stranding test).
        Assert.False(fold.Contains(forged.OpId));
        Assert.Equal(0, log.Count);
        Assert.Equal(1, svc.RefusedOps);
        Assert.NotNull(reported);

        // POSITIVE CONTROL: the genuine article over the same path is folded and stored.
        var genuine = FederationOp.Create(new Dot(aliceId, 2), aliceId, "claim", Body).SignedBy(alice);
        Assert.Equal(1, await svc.ReconcileAsync(new[] { genuine }, new PeerCapabilities(true, LiveEpoch)));
        Assert.Equal(1, log.Count);
    }

    /// <summary>
    /// One refused operation must not strand every valid operation behind it.
    /// <para>
    /// Aborting the batch on a refusal was permanent: the refused op never enters the frontier, so
    /// the peer resends it FIRST at every interval and nothing after it ever converges. A single
    /// malformed entry could stop reconciliation for good.
    /// </para>
    /// </summary>
    [Fact]
    public async Task ARefusedOpDoesNotStrandTheValidOpsBehindIt()
    {
        using var alice = QuicLinkTransport.CreateDevCert("alice");
        using var mallory = QuicLinkTransport.CreateDevCert("mallory");
        string aliceId = NodeIdentityStore.DeriveNodeId(alice);

        var cfg = Cfg(new PeerConfig
        {
            Name = "alice", NodeId = aliceId, Endpoints = { "192.168.0.142:47890" },
            Pin = NodeIdentityStore.PinFromNodeId(aliceId),
            Spki = NodeIdentityStore.ExportSpki(alice),
        });

        var fold = NewFold();
        var log = new InMemoryBoardLog();
        var svc = new FederationService(cfg, new RecordingLink("A"), fold, log);

        var forged = FederationOp.Create(new Dot(aliceId, 1), aliceId, "claim", Body).SignedBy(mallory);
        var good1 = FederationOp.Create(new Dot(aliceId, 2), aliceId, "claim", Body).SignedBy(alice);
        var good2 = FederationOp.Create(new Dot(aliceId, 3), aliceId, "claim", Body).SignedBy(alice);

        // The forged op is FIRST in the batch — the position that used to abort everything after it.
        int added = await svc.ReconcileAsync(new[] { forged, good1, good2 },
                                             new PeerCapabilities(true, LiveEpoch));

        Assert.Equal(2, added);
        Assert.False(fold.Contains(forged.OpId));
        Assert.True(fold.Contains(good1.OpId));
        Assert.True(fold.Contains(good2.OpId));
        Assert.Equal(1, svc.RefusedOps);
    }

    /// <summary>A published key that does not belong to the node id it is filed under is refused.</summary>
    [Fact]
    public void AnSpkiThatDoesNotHashToItsNodeIdIsRefused()
    {
        using var alice = QuicLinkTransport.CreateDevCert("alice");
        var cfg = Cfg(new PeerConfig
        {
            Name = "olamnit",
            NodeId = NodeId("olamnit"),                        // a different participant
            Endpoints = { "192.168.0.136:47890" },
            Spki = NodeIdentityStore.ExportSpki(alice),         // ...carrying Alice's key
        });

        Assert.Contains(cfg.Validate(), p => p.Contains("does not belong to this participant"));
    }

    // ==========================================================================================
    // F10 — read-status-from-the-serving-process
    // ==========================================================================================

    /// <summary>
    /// A record from a process that has since been killed is a measurement that no longer holds.
    /// Reading it as current is how a dead daemon reports itself healthy.
    /// </summary>
    [Fact]
    public void AStaleHeartbeatIsNoMeasurementAtAll()
    {
        string dir = Path.Combine(Path.GetTempPath(), "ynet_hb", Guid.NewGuid().ToString("n")[..8]);
        string path = Path.Combine(dir, "serving-status.json");
        try
        {
            var now = DateTimeOffset.UtcNow;
            new StatusHeartbeat { PublishedUtc = now, ListenerBound = "yes" }.Publish(path);

            Assert.NotNull(StatusHeartbeat.ReadFresh(now, path));                     // fresh: read
            Assert.Null(StatusHeartbeat.ReadFresh(                                    // stale: not
                now + StatusHeartbeat.Freshness + TimeSpan.FromSeconds(1), path));
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    /// <summary>Absent and corrupt both resolve to "no measurement" — never to a negative (SC-010).</summary>
    [Fact]
    public void ACorruptOrAbsentHeartbeatIsUnknownNotNo()
    {
        string dir = Path.Combine(Path.GetTempPath(), "ynet_hb", Guid.NewGuid().ToString("n")[..8]);
        string path = Path.Combine(dir, "serving-status.json");
        try
        {
            Assert.Null(StatusHeartbeat.ReadFresh(DateTimeOffset.UtcNow, path));   // absent

            Directory.CreateDirectory(dir);
            File.WriteAllText(path, "{ this is not json");
            Assert.Null(StatusHeartbeat.ReadFresh(DateTimeOffset.UtcNow, path));   // corrupt

            // And the reader turns that into UNKNOWN, which is the whole FR-021 point.
            var reconstructed = new StatusHeartbeat().ToStatus(Tri.Yes);
            Assert.Equal(Tri.Unknown, reconstructed.ListenerBound);
            Assert.Equal(Tri.Unknown, reconstructed.OpReceivedFromPeer);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    /// <summary>The serving process's live measurement survives the round trip to a reader.</summary>
    [Fact]
    public async Task TheServingProcessPublishesAReadableMeasurement()
    {
        string dir = Path.Combine(Path.GetTempPath(), "ynet_hb", Guid.NewGuid().ToString("n")[..8]);
        string path = Path.Combine(dir, "serving-status.json");
        try
        {
            string id = NodeId("olamnit");
            var svc = new FederationService(Cfg(Peer("olamnit", "olamnit", "192.168.0.136:47890")),
                                            new RecordingLink("A"), NewFold(), new InMemoryBoardLog())
            {
                StatusHeartbeatPath = path,
            };
            await svc.BindAsync();
            await svc.DialAsync(id);

            var read = StatusHeartbeat.ReadFresh(DateTimeOffset.UtcNow, path);
            Assert.NotNull(read);

            // The separate reader sees BOUND and ADMITTED — the states it previously reported as No
            // because it could not see the serving process at all.
            var status = read!.ToStatus(Tri.Yes);
            Assert.Equal(Tri.Yes, status.ListenerBound);
            Assert.Equal(Tri.Yes, status.PeerAdmitted);
            Assert.Equal(1, status.AdmittedParticipants);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    /// <summary>
    /// The heartbeat must refresh well INSIDE its own freshness window.
    /// <para>
    /// Found by running the daemon rather than by reading it: publishing only on pull ticks tied the
    /// refresh rate to `pull_interval_seconds` (60s default) against a 30s window, so a healthy
    /// daemon reported "measured 23s ago" and climbing, and would have gone UNKNOWN for half of
    /// every minute. A fix for a false-negative that reintroduces a false-negative is not a fix.
    /// </para>
    /// </summary>
    [Fact]
    public async Task TheHeartbeatRefreshesWellInsideItsFreshnessWindow()
    {
        string dir = Path.Combine(Path.GetTempPath(), "ynet_hb", Guid.NewGuid().ToString("n")[..8]);
        string path = Path.Combine(dir, "serving-status.json");
        try
        {
            var clock = new DrivableClock();
            // A pull interval far LONGER than the freshness window — the exact live configuration.
            var cfg = Cfg() with { PullIntervalSeconds = 600 };
            var svc = new FederationService(cfg, new RecordingLink("A"), NewFold(),
                                            new InMemoryBoardLog(), clock) { StatusHeartbeatPath = path };
            await svc.BindAsync();

            using var cts = new CancellationTokenSource();
            var beat = svc.RunStatusHeartbeatAsync(cts.Token);

            // Advance most of the way through the window, repeatedly. The record must never expire.
            for (int i = 0; i < 5; i++)
            {
                await clock.AdvanceAsync(StatusHeartbeat.Freshness - TimeSpan.FromSeconds(5));
                Assert.NotNull(StatusHeartbeat.ReadFresh(clock.GetUtcNow(), path));
            }

            cts.Cancel();
            try { await beat; } catch (OperationCanceledException) { }

            // POSITIVE CONTROL: once the loop STOPS, the record does expire — so the assertions
            // above are the refresh working, not ReadFresh failing to check.
            await clock.AdvanceAsync(StatusHeartbeat.Freshness + TimeSpan.FromSeconds(5));
            Assert.Null(StatusHeartbeat.ReadFresh(clock.GetUtcNow(), path));
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    // ==========================================================================================
    // F12 — batch-pull-responses-below-the-transport-frame-limit
    // ==========================================================================================

    /// <summary>
    /// A peer far enough behind produced one oversized frame, which the transport REJECTS — and the
    /// identical frame was rebuilt and rejected at every interval, so the peer never made any
    /// progress at all. Batches are a prefix sequence, so partial delivery still converges.
    /// </summary>
    [Fact]
    public void AFarBehindPeerIsAnsweredInFrameSizedBatches()
    {
        string filler = new('x', 64 * 1024);
        var ops = Enumerable.Range(1, 400)
            .Select(i => FederationOp.Create(new Dot("g", i), "g", "board_post",
                JsonSerializer.SerializeToElement(new { body = filler })))
            .ToList();

        // POSITIVE CONTROL: unbatched, this genuinely exceeds the batch limit — otherwise the
        // assertion below would hold trivially and prove nothing.
        Assert.True(PullProtocol.EncodeResponse(ops).Length > PullProtocol.MaxResponseBytes);

        var batches = PullProtocol.BatchResponses(ops);
        Assert.True(batches.Count > 1);
        Assert.All(batches, b => Assert.True(PullProtocol.EncodeResponse(b).Length <= PullProtocol.MaxResponseBytes));

        // Nothing is lost or reordered by the split.
        Assert.Equal(ops.Select(o => o.OpId), batches.SelectMany(b => b).Select(o => o.OpId));
    }

    /// <summary>
    /// An operation that cannot cross this transport is SKIPPED AND NAMED, not batched.
    /// <para>
    /// THIS TEST ASSERTED THE OPPOSITE UNTIL ROUND 9, on my reasoning that "the transport's own
    /// guard is the backstop". It is not a backstop: the transport REJECTS the oversized frame, the
    /// same frame is rebuilt at the next interval, and every operation queued behind it is stranded
    /// too. Skipping costs one operation; the batch-it-anyway contract cost all of them.
    /// </para>
    /// </summary>
    [Fact]
    public void AnOpAboveTheBatchThresholdButBelowTheWireLimitIsStillSent()
    {
        // ROUND 9 SAID "skip the unsendable"; MY FIX MEASURED AGAINST THE WRONG LIMIT and round 10
        // caught it. MaxResponseBytes (8 MiB) is a BATCHING preference; MaxWireBytes (64 MiB) is
        // what the transport will actually carry. Skipping everything above the former stranded
        // every operation in between — permanently, and silently, via the frontier.
        var big = FederationOp.Create(new Dot("g", 1), "g", "board_post",
            JsonSerializer.SerializeToElement(new { body = new string('x', PullProtocol.MaxResponseBytes + 1024) }));
        var ordinary = Op("g", 2);

        var batches = PullProtocol.BatchResponses(new[] { big, ordinary });

        // BOTH cross. The big one alone, the ordinary one after it.
        Assert.Equal(new[] { big.OpId, ordinary.OpId }, batches.SelectMany(b => b).Select(o => o.OpId));
        Assert.DoesNotContain(big.OpId, PullProtocol.Oversized);

        // Each batch still fits the wire.
        Assert.All(batches, b => Assert.True(PullProtocol.EncodeResponse(b).Length <= PullProtocol.MaxWireBytes));

        // AND THE TWO LIMITS ARE STILL DISTINCT — the confusion that caused this.
        Assert.True(PullProtocol.MaxWireBytes > PullProtocol.MaxResponseBytes);
    }

    // ==========================================================================================
    // F13 — keep-malformed-retirement-bodies-from-crashing-the-fold
    // ==========================================================================================

    /// <summary>
    /// TargetOf promises null on a malformed retirement. GetInt64 THREW on a fractional or
    /// out-of-range counter — and because the caller inserted the op first, the exception left the
    /// fold partially mutated on its way out.
    /// </summary>
    [Theory]
    [InlineData("1.5")]
    [InlineData("99999999999999999999999999")]
    [InlineData("-99999999999999999999999999")]
    public void AMalformedRetirementCounterReturnsNullRatherThanThrowing(string counter)
    {
        // A CONFORMANT pred_hash, because the decoder now requires one. The fixture has to obey the
        // wire contract it is testing a DIFFERENT part of — a fixture that violates one rule while
        // probing another cannot tell you which rule failed.
        string pred = Convert.ToHexStringLower(HashChain.PredHash(new Dot("g", 1), Array.Empty<Dot>()));
        string json = "{\"op_id\":{\"peer\":\"g\",\"counter\":1},\"origin\":\"g\",\"kind\":\"retire\","
                    + "\"deps\":[],\"pred_hash\":\"" + pred + "\",\"body\":{\"target_op_id\":{\"peer\":\"g\",\"counter\":"
                    + counter + "},\"into_space\":\"legacy\",\"reason\":\"r\"}}";
        var op = FederationOp.FromJson(json);

        Assert.Null(RetirementOp.TargetOf(op));

        // And the fold takes it WHOLE: the op is retained (FR-011), with no ordering consequence.
        var fold = NewFold();
        Assert.True(fold.Apply(op));
        Assert.Equal(1, fold.Count);
        Assert.True(fold.Contains(op.OpId));
    }

    /// <summary>POSITIVE CONTROL: a well-formed retirement still takes effect.</summary>
    [Fact]
    public void AWellFormedRetirementStillRetiresItsTarget()
    {
        var fold = NewFold();
        var target = FederationOp.Create(new Dot("g", 1), "g", "claim", Body,
            term: new Term(LiveEpoch, 5, "g"));
        fold.Apply(target);
        Assert.Equal(OrderingDisposition.Orderable, fold.DispositionOf(target));

        fold.Apply(RetirementOp.Create(new Dot("g", 2), "g", target.OpId, "fossil term"));

        Assert.Equal(OrderingDisposition.UnorderedLegacy, fold.DispositionOf(target));
        Assert.True(fold.Contains(target.OpId));   // still PRESENT — never deleted (SC-012)
    }

    // ==========================================================================================
    // F14 — honor-the-configured-identity-path
    // ==========================================================================================

    /// <summary>
    /// A deployment that pre-provisions a key and is silently given a freshly-minted one instead
    /// federates under an identity no peer has pinned. The configured setting was inert while
    /// appearing effective.
    /// </summary>
    [Fact]
    public void TheConfiguredIdentityPathIsUsed()
    {
        var configured = new FederationConfig { IdentityPath = "D:/somewhere/preprovisioned.pfx" };
        Assert.Equal("D:/somewhere/preprovisioned.pfx", configured.EffectiveIdentityPath);

        // POSITIVE CONTROL: unset still falls back to the default, so this is not just echoing.
        Assert.Equal(NodeIdentityStore.DefaultPath(), new FederationConfig().EffectiveIdentityPath);
    }

    /// <summary>A pre-provisioned key is LOADED, not re-minted — the node id is stable.</summary>
    [Fact]
    public void APreProvisionedKeyIsLoadedRatherThanReminted()
    {
        string dir = Path.Combine(Path.GetTempPath(), "ynet_id", Guid.NewGuid().ToString("n")[..8]);
        string path = Path.Combine(dir, "preprovisioned.pfx");
        try
        {
            string first = NodeIdentityStore.DeriveNodeId(new NodeIdentityStore(path).LoadOrMint("host"));
            string again = NodeIdentityStore.DeriveNodeId(new NodeIdentityStore(path).LoadOrMint("host"));

            Assert.Equal(first, again);

            // POSITIVE CONTROL: a DIFFERENT path mints a different identity, so the equality above
            // is the persistence working rather than the derivation being constant.
            string other = Path.Combine(dir, "other.pfx");
            Assert.NotEqual(first, NodeIdentityStore.DeriveNodeId(new NodeIdentityStore(other).LoadOrMint("host")));
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    // ==========================================================================================
    // F4 — attach-federation-to-the-existing-board-log
    // ==========================================================================================

    /// <summary>
    /// A path that is not a board root is REFUSED. Silently creating root.json under a mistyped path
    /// produces a brand-new empty board that looks healthy and shares nothing — the second-oracle
    /// failure in a new costume.
    /// </summary>
    [Fact]
    public void ANonBoardDirectoryIsRefusedRatherThanInitialised()
    {
        string dir = Path.Combine(Path.GetTempPath(), "ynet_root", Guid.NewGuid().ToString("n")[..8]);
        Directory.CreateDirectory(dir);
        try
        {
            var ex = Assert.Throws<BoardRootException>(() => BoardRoot.Resolve(null, dir));
            Assert.Contains("not a board root", ex.Message);
            Assert.False(File.Exists(Path.Combine(dir, BoardRoot.RootMarker)));   // nothing created

            // POSITIVE CONTROL: with the marker present the same path resolves.
            File.WriteAllText(Path.Combine(dir, BoardRoot.RootMarker), "{\"root_id\":\"t\"}");
            Assert.Equal(dir, BoardRoot.Resolve(null, dir));
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    /// <summary>An unconfigured board root is refused, not defaulted to a private file.</summary>
    [Fact]
    public void AnUnconfiguredBoardRootIsRefused()
    {
        Assert.Throws<BoardRootException>(() => BoardRoot.Resolve(null, null));
        Assert.Contains(new FederationConfig { Enabled = true, SpaceId = LiveEpoch }.Validate(),
                        p => p.StartsWith("board_root"));
    }

    /// <summary>
    /// THE SECOND-ORACLE TEST. Real scheduler-native lines — the shape the lanes actually write —
    /// are folded, so a lane's claim reaches the federated board. Before the fix nothing read them.
    /// </summary>
    [Fact]
    public async Task RealLaneClaimsAreFoldedFromTheExistingBoard()
    {
        string root = Path.Combine(Path.GetTempPath(), "ynet_board", Guid.NewGuid().ToString("n")[..8]);
        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, BoardRoot.RootMarker), "{\"root_id\":\"t\",\"schema_version\":\"1\"}");

            string laneDir = BoardRoot.ActorDirectory(root, "gavriella");
            Directory.CreateDirectory(laneDir);
            // Verbatim shape from D:\coop\buildkit\sched\ops\gavriella\gavriella-ops-000001.jsonl.
            File.WriteAllLines(Path.Combine(laneDir, "gavriella-ops-000001.jsonl"), new[]
            {
                "{\"actor\":\"gavriella\",\"op_id\":\"gavriella:000001\",\"op_type\":\"claim\",\"seq\":1,\"wp_id\":\"wp-a\",\"timestamp\":\"2026-08-18T13:14:13Z\"}",
                "{\"actor\":\"gavriella\",\"op_id\":\"gavriella:000002\",\"op_type\":\"claim\",\"seq\":2,\"wp_id\":\"wp-b\",\"timestamp\":\"2026-08-18T14:21:05Z\"}",
            });

            var log = new SchedulerBoardLog(root, "gavriella");
            var ops = await log.ReadAllAsync();

            Assert.Equal(2, ops.Count);
            Assert.Equal(2, log.AdaptedLines);
            Assert.Equal(0, log.UnreadableLines);
            // NAMESPACED. A scheduler record's identity is (actor, seq) — a per-board identity, NOT
            // the NodeId the federation contract names. The prefix says what this identity actually
            // is instead of claiming a NodeId the record never carried, and stops a scheduler dot
            // colliding with a federation one.
            Assert.Equal(new Dot("sched:gavriella", 1), ops[0].OpId);
            Assert.Equal("sched:gavriella", ops[0].Origin);
            Assert.Equal("claim", ops[0].Kind);
            // The original record is carried VERBATIM — nothing is rewritten (FR-011).
            Assert.Equal("wp-a", ops[0].Body.GetProperty("wp_id").GetString());
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    /// <summary>Every actor's log is read — a fold of one host's own ops is not the board.</summary>
    [Fact]
    public async Task TheFoldIsBuiltFromEveryActorsLogNotJustThisHosts()
    {
        string root = Path.Combine(Path.GetTempPath(), "ynet_board", Guid.NewGuid().ToString("n")[..8]);
        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, BoardRoot.RootMarker), "{\"root_id\":\"t\"}");

            foreach (var actor in new[] { "gavriella", "olamnit", "ariellas" })
            {
                string d = BoardRoot.ActorDirectory(root, actor);
                Directory.CreateDirectory(d);
                File.WriteAllText(Path.Combine(d, $"{actor}-ops-000001.jsonl"),
                    $"{{\"actor\":\"{actor}\",\"op_id\":\"{actor}:000001\",\"op_type\":\"claim\",\"seq\":1}}\n");
            }

            var ops = await new SchedulerBoardLog(root, "gavriella").ReadAllAsync();
            Assert.Equal(3, ops.Count);
            Assert.Equal(new[] { "sched:ariellas", "sched:gavriella", "sched:olamnit" },
                         ops.Select(o => o.Origin).OrderBy(x => x));
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    /// <summary>
    /// Federation appends UNDER THE SAME ROOT, and by default does not put a foreign-schema line in
    /// a lane's live segment (an interop decision that is the engineer's, not a default).
    /// </summary>
    [Fact]
    public async Task FederationWritesUnderTheSameRootAndRoundTrips()
    {
        string root = Path.Combine(Path.GetTempPath(), "ynet_board", Guid.NewGuid().ToString("n")[..8]);
        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, BoardRoot.RootMarker), "{\"root_id\":\"t\"}");

            var log = new SchedulerBoardLog(root, "gavriella");
            Assert.StartsWith(root, log.WritePath);
            Assert.DoesNotContain($"{Path.DirectorySeparatorChar}ops{Path.DirectorySeparatorChar}", log.WritePath);

            string id = NodeId("gavriella");
            await log.AppendAsync(Op(id, 1));

            var back = await log.ReadAllAsync();
            Assert.Equal(new Dot(id, 1), Assert.Single(back).OpId);
            Assert.Equal(0, log.UnreadableLines);

            // The symmetric mode writes into the lane's own segment, for when that is ruled on.
            Assert.Contains($"{Path.DirectorySeparatorChar}ops{Path.DirectorySeparatorChar}",
                            new SchedulerBoardLog(root, "gavriella", BoardWriteMode.LaneSegment).WritePath);
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    /// <summary>A line that is neither schema is COUNTED, never silently skipped.</summary>
    [Fact]
    public async Task AnUnreadableLineIsCountedNotSwallowed()
    {
        string root = Path.Combine(Path.GetTempPath(), "ynet_board", Guid.NewGuid().ToString("n")[..8]);
        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, BoardRoot.RootMarker), "{\"root_id\":\"t\"}");
            string d = BoardRoot.ActorDirectory(root, "gavriella");
            Directory.CreateDirectory(d);
            File.WriteAllLines(Path.Combine(d, "gavriella-ops-000001.jsonl"), new[]
            {
                "{\"actor\":\"gavriella\",\"op_id\":\"gavriella:000001\",\"op_type\":\"claim\",\"seq\":1}",
                "{ not json at all",
                "{\"no_actor\":true}",
            });

            var log = new SchedulerBoardLog(root, "gavriella");
            Assert.Single(await log.ReadAllAsync());
            Assert.Equal(2, log.UnreadableLines);
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    // ==========================================================================================
    // F11 — confirm-the-remote-fold-before-recording-sc-001
    // ==========================================================================================

    /// <summary>
    /// A local append returning proves nothing about a peer. `PushAsync` swallows a send failure by
    /// design, so the SC-001 elapsed figure was achievable with the peer switched off. Only the peer
    /// can attest that an op reached its fold.
    /// </summary>
    [Fact]
    public async Task APushedOpIsNotConsideredRemotelyVisibleUntilThePeerAcksIt()
    {
        string id = NodeId("olamnit");
        var link = new RecordingLink("A");
        var clock = new DrivableClock();
        var svc = new FederationService(Cfg(Peer("olamnit", "olamnit", "192.168.0.136:47890")),
                                        link, NewFold(), new InMemoryBoardLog(), clock);
        await svc.DialAsync(id);
        // R13-02: outbound board data is gated on a current declaration, so the peer has
        // to declare — as a real one does — before a push is expected.
        link.PushInbound(new LinkInbound(id,
            HelloProtocol.Encode(new PeerCapabilities(true, LiveEpoch), isReply: true),
            HelloProtocol.Box));
        await svc.ReceiveOneAsync();

        var op = Op(NodeId("gavriella"), 1);
        await svc.AppendAndPushAsync(op);

        // The local write returned and the frame was handed to the link — and STILL not acked.
        Assert.Contains(link.Sent, s => s.Box == FederationService.BoardBox);
        Assert.False(svc.WasAckedByPeer(op.OpId));

        var wait = svc.WaitForPeerAckAsync(op.OpId, TimeSpan.FromSeconds(5));
        link.PushInbound(new LinkInbound(id, AckProtocol.Encode(op.OpId), AckProtocol.Box));
        await svc.ReceiveOneAsync();

        Assert.True(await wait);
        Assert.True(svc.WasAckedByPeer(op.OpId));
    }

    /// <summary>
    /// POSITIVE CONTROL: with no ack the wait TIMES OUT rather than passing. Without this the test
    /// above could be satisfied by a WaitForPeerAckAsync that always returned true.
    /// </summary>
    [Fact]
    public async Task WaitingForAnAckThatNeverComesTimesOut()
    {
        string id = NodeId("olamnit");
        var link = new RecordingLink("A");
        var clock = new DrivableClock();
        var svc = new FederationService(Cfg(Peer("olamnit", "olamnit", "192.168.0.136:47890")),
                                        link, NewFold(), new InMemoryBoardLog(), clock);
        await svc.DialAsync(id);
        // R13-02: outbound board data is gated on a current declaration, so the peer has
        // to declare — as a real one does — before a push is expected.
        link.PushInbound(new LinkInbound(id,
            HelloProtocol.Encode(new PeerCapabilities(true, LiveEpoch), isReply: true),
            HelloProtocol.Box));
        await svc.ReceiveOneAsync();

        var op = Op(NodeId("gavriella"), 1);
        await svc.AppendAndPushAsync(op);

        var wait = svc.WaitForPeerAckAsync(op.OpId, TimeSpan.FromSeconds(5));
        await clock.AdvanceAsync(TimeSpan.FromSeconds(6));

        Assert.False(await wait);
    }

    /// <summary>A receiver ACKS what it folded, naming the dot — that is the attestation.</summary>
    [Fact]
    public async Task AReceiverAcksTheDotItFolded()
    {
        string id = NodeId("olamnit");
        var link = new RecordingLink("A");
        var svc = new FederationService(Cfg(Peer("olamnit", "olamnit", "192.168.0.136:47890")),
                                        link, NewFold(), new InMemoryBoardLog());

        link.PushInbound(new LinkInbound(id,
            HelloProtocol.Encode(new PeerCapabilities(true, LiveEpoch)), HelloProtocol.Box));
        await svc.ReceiveOneAsync();

        var op = Op(id, 7);
        link.PushInbound(new LinkInbound(id,
            Encoding.UTF8.GetBytes(op.ToCanonicalJson()), FederationService.BoardBox));
        await svc.ReceiveOneAsync();

        var ack = link.Sent.Single(s => s.Box == AckProtocol.Box);
        Assert.Equal(id, ack.To);
        Assert.Equal(op.OpId, AckProtocol.Decode(ack.Bytes));
    }

    // ==========================================================================================
    // F3 — send-posts-through-the-running-service
    // ==========================================================================================

    /// <summary>
    /// `post` and `serve` are separate processes. The daemon tails the durable log, so an op another
    /// process appended is pushed — previously it was written locally and reached nobody.
    /// </summary>
    [Fact]
    public async Task TheDaemonPushesAnOpAppendedByAnotherProcess()
    {
        string root = Path.Combine(Path.GetTempPath(), "ynet_tail", Guid.NewGuid().ToString("n")[..8]);
        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, BoardRoot.RootMarker), "{\"root_id\":\"t\"}");

            string id = NodeId("olamnit");
            var link = new RecordingLink("A");
            var clock = new DrivableClock();
            var svc = new FederationService(Cfg(Peer("olamnit", "olamnit", "192.168.0.136:47890")),
                                            link, NewFold(), new InMemoryBoardLog(), clock);
            await svc.DialAsync(id);
            // R13-02: outbound board data is gated on a current declaration, so the peer has
            // to declare — as a real one does — before a push is expected.
            link.PushInbound(new LinkInbound(id,
                HelloProtocol.Encode(new PeerCapabilities(true, LiveEpoch), isReply: true),
                HelloProtocol.Box));
            await svc.ReceiveOneAsync();

            // The daemon's tail. The file starts empty, exactly as at daemon start.
            var writer = new SchedulerBoardLog(root, "gavriella");
            string path = writer.WritePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "");

            using var cts = new CancellationTokenSource();
            var tail = svc.RunLogTailAsync(path, cts.Token);

            // ANOTHER process appends — this is `post`.
            await writer.AppendAsync(Op(NodeId("gavriella"), 1));

            await clock.AdvanceAsync(TimeSpan.FromSeconds(1));
            await clock.AdvanceAsync(TimeSpan.FromSeconds(1));
            cts.Cancel();
            try { await tail; } catch (OperationCanceledException) { }

            Assert.Contains(link.Sent, s => s.Box == FederationService.BoardBox && s.To == id);
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    /// <summary>
    /// POSITIVE CONTROL: with nothing appended, the tail pushes nothing. Otherwise the test above
    /// could pass on any stray send.
    /// </summary>
    [Fact]
    public async Task TheTailPushesNothingWhenNothingIsAppended()
    {
        string root = Path.Combine(Path.GetTempPath(), "ynet_tail", Guid.NewGuid().ToString("n")[..8]);
        try
        {
            Directory.CreateDirectory(root);
            string path = Path.Combine(root, "log.jsonl");
            File.WriteAllText(path, "");

            string id = NodeId("olamnit");
            var link = new RecordingLink("A");
            var clock = new DrivableClock();
            var svc = new FederationService(Cfg(Peer("olamnit", "olamnit", "192.168.0.136:47890")),
                                            link, NewFold(), new InMemoryBoardLog(), clock);
            await svc.DialAsync(id);
            // R13-02: outbound board data is gated on a current declaration, so the peer has
            // to declare — as a real one does — before a push is expected.
            link.PushInbound(new LinkInbound(id,
                HelloProtocol.Encode(new PeerCapabilities(true, LiveEpoch), isReply: true),
                HelloProtocol.Box));
            await svc.ReceiveOneAsync();

            using var cts = new CancellationTokenSource();
            var tail = svc.RunLogTailAsync(path, cts.Token);
            await clock.AdvanceAsync(TimeSpan.FromSeconds(1));
            cts.Cancel();
            try { await tail; } catch (OperationCanceledException) { }

            Assert.DoesNotContain(link.Sent, s => s.Box == FederationService.BoardBox);
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    /// <summary>An op the daemon already holds is not re-pushed when the tail re-reads the file.</summary>
    [Fact]
    public async Task TheTailDoesNotRepushAnOpTheFoldAlreadyHolds()
    {
        string root = Path.Combine(Path.GetTempPath(), "ynet_tail", Guid.NewGuid().ToString("n")[..8]);
        try
        {
            Directory.CreateDirectory(root);
            string path = Path.Combine(root, "log.jsonl");

            string id = NodeId("olamnit");
            var link = new RecordingLink("A");
            var clock = new DrivableClock();
            var fold = NewFold();
            var svc = new FederationService(Cfg(Peer("olamnit", "olamnit", "192.168.0.136:47890")),
                                            link, fold, new InMemoryBoardLog(), clock);
            await svc.DialAsync(id);
            // R13-02: outbound board data is gated on a current declaration, so the peer has
            // to declare — as a real one does — before a push is expected.
            link.PushInbound(new LinkInbound(id,
                HelloProtocol.Encode(new PeerCapabilities(true, LiveEpoch), isReply: true),
                HelloProtocol.Box));
            await svc.ReceiveOneAsync();

            var op = Op(NodeId("gavriella"), 1);
            fold.Apply(op);                                   // the daemon already knows it
            File.WriteAllText(path, "");
            using var cts = new CancellationTokenSource();
            var tail = svc.RunLogTailAsync(path, cts.Token);
            File.AppendAllText(path, op.ToCanonicalJson() + Environment.NewLine);

            await clock.AdvanceAsync(TimeSpan.FromSeconds(1));
            await clock.AdvanceAsync(TimeSpan.FromSeconds(1));
            cts.Cancel();
            try { await tail; } catch (OperationCanceledException) { }

            Assert.DoesNotContain(link.Sent, s => s.Box == FederationService.BoardBox);
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }
}

/// <summary>
/// A link that records what it was asked to do, so the tests can assert on the KEY a send or dial
/// used — which is the whole of findings F2, F3 and F5.
/// </summary>
public sealed class RecordingLink : IFederationLink
{
    private readonly System.Threading.Channels.Channel<LinkInbound> _in =
        System.Threading.Channels.Channel.CreateUnbounded<LinkInbound>();

    public RecordingLink(string local) => LocalPeer = local;

    public string LocalPeer { get; }
    public IPEndPoint? ListenEndPoint { get; private set; }

    /// <summary>Every connection attempt is refused — the "peer unreachable" condition.</summary>
    public bool Broken { get; set; }

    /// <summary>A specific exception to throw from a dial, for classifying identity vs reachability.</summary>
    public Exception? ThrowOnConnect { get; set; }

    /// <summary>Connections succeed but sends fail — an established link that has since closed.</summary>
    public bool FailSends { get; set; }

    public List<(string Peer, IPEndPoint Remote)> Dialled { get; } = new();
    public List<(string To, string Box, byte[] Bytes)> Sent { get; } = new();

    public Task ListenAsync(IPEndPoint bind, CancellationToken ct = default)
    {
        ListenEndPoint = bind;
        return Task.CompletedTask;
    }

    public Task ConnectPeerAsync(string peer, IPEndPoint remote, CancellationToken ct = default)
    {
        if (ThrowOnConnect is not null) throw ThrowOnConnect;
        if (Broken) throw new IOException("unreachable");
        Dialled.Add((peer, remote));
        return Task.CompletedTask;
    }

    public ValueTask SendAsync(string to, string box, ReadOnlyMemory<byte> bytes, CancellationToken ct = default)
    {
        if (FailSends) throw new IOException("connection closed");
        Sent.Add((to, box, bytes.ToArray()));
        return ValueTask.CompletedTask;
    }

    public void PushInbound(LinkInbound inbound) => _in.Writer.TryWrite(inbound);

    public System.Threading.Channels.ChannelReader<LinkInbound> Inbound => _in.Reader;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>
/// A clock the test drives. The pull loop's period is real time otherwise, and a test that sleeps
/// for a 60-second interval is a test nobody runs.
/// <para>
/// <c>Task.Delay(period, TimeProvider, ct)</c> routes through <see cref="CreateTimer"/>, so the
/// timer itself has to be driven — returning a no-op timer would hang the loop forever and the
/// reconnection tests would pass by never running their assertions at all.
/// </para>
/// </summary>
public sealed class DrivableClock : TimeProvider
{
    private readonly object _gate = new();
    private readonly List<FakeTimer> _timers = new();
    private DateTimeOffset _now = DateTimeOffset.UtcNow;

    public override DateTimeOffset GetUtcNow()
    {
        lock (_gate) return _now;
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var t = new FakeTimer(this, callback, state);
        lock (_gate)
        {
            t.Due = dueTime == Timeout.InfiniteTimeSpan ? null : _now + dueTime;
            t.Period = period;
            _timers.Add(t);
        }
        return t;
    }

    /// <summary>Move time forward and let everything due at that point run to completion.</summary>
    public async Task AdvanceAsync(TimeSpan by)
    {
        List<FakeTimer> fired;
        lock (_gate)
        {
            _now += by;
            fired = _timers.Where(t => t.Due is { } d && d <= _now).ToList();
            foreach (var t in fired)
                t.Due = t.Period == Timeout.InfiniteTimeSpan || t.Period <= TimeSpan.Zero
                    ? null : _now + t.Period;
        }
        foreach (var t in fired) t.Fire();

        // Yield generously so the woken loop body finishes its awaits before the test asserts.
        for (int i = 0; i < 50; i++) await Task.Yield();
        await Task.Delay(50);
    }

    private void Remove(FakeTimer t)
    {
        lock (_gate) _timers.Remove(t);
    }

    private sealed class FakeTimer : ITimer
    {
        private readonly DrivableClock _owner;
        private readonly TimerCallback _callback;
        private readonly object? _state;

        public FakeTimer(DrivableClock owner, TimerCallback callback, object? state)
        {
            _owner = owner;
            _callback = callback;
            _state = state;
        }

        public DateTimeOffset? Due { get; set; }
        public TimeSpan Period { get; set; }

        public void Fire() => _callback(_state);

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            Due = dueTime == Timeout.InfiniteTimeSpan ? null : _owner.GetUtcNow() + dueTime;
            Period = period;
            return true;
        }

        public void Dispose() => _owner.Remove(this);
        public ValueTask DisposeAsync() { _owner.Remove(this); return ValueTask.CompletedTask; }
    }
}
