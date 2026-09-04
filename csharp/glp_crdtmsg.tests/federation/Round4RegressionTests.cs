// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT
//
// Regression tests for the twelve defects found by the round-4 adversarial review
// (reviews/102-quic-federation-transport/20260904T181959Z), plus the engineer ruling on board writes.
//
// FOUR ROUNDS ON ONE BRANCH: 1, 14, 17, 12. Round 4 again found ZERO recurrences — every earlier fix
// held — and twelve more, THREE of them in the round-3 remediation itself:
//
//   * the inbound dedup check bypassed the dot-conflict guard added in round 3;
//   * the board tail added in round 3 watched the lane logs and then discarded every line they
//     contain, because a scheduler-native record is not federation-shaped;
//   * the tail's start-at-current-length snapshot opened a race with startup replay.
//
// The lesson is not "review more". It is that a fix is a code change like any other and inherits the
// same defect rate — so the round that reviews the fixes is not optional.

using System.Text;
using System.Text.Json;
using GlpRuntime.CrdtMsg.Crdt;
using GlpRuntime.CrdtMsg.Federation;
using GlpRuntime.CrdtMsg.Route;
using Xunit;

namespace GlpRuntime.CrdtMsg.Tests.Federation;

public sealed class Round4RegressionTests
{
    private const string LiveEpoch = "ynet-epoch-7f3a91c2e04b5d68";
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

    private static string Board()
    {
        string root = Path.Combine(Path.GetTempPath(), "ynet_r4", Guid.NewGuid().ToString("n")[..8]);
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, BoardRoot.RootMarker), "{\"root_id\":\"t\"}");
        return root;
    }

    // ---- ENGINEER RULING 2026-09-04: federated ops land where the lanes read -------------------

    /// <summary>
    /// The default is now the lane's own <c>ops/</c> segment. Two consecutive review rounds ranked
    /// this first: a host could ACK a claim it had folded while the lane ON THAT HOST could not see
    /// it. Federation that converges a board nobody reads is the second oracle in a new costume.
    /// </summary>
    [Fact]
    public void FederatedOpsAreWrittenWhereTheLanesRead()
    {
        Assert.True(new FederationConfig().WriteIntoLaneSegment);

        string root = Board();
        try
        {
            var log = new SchedulerBoardLog(root, "gavriella", BoardWriteMode.LaneSegment);
            Assert.Contains($"{Path.DirectorySeparatorChar}ops{Path.DirectorySeparatorChar}", log.WritePath);

            // POSITIVE CONTROL: the federation-owned kind is still reachable for a lane whose
            // readers turn out to be strict, so the ruling is a default and not a one-way door.
            var fed = new SchedulerBoardLog(root, "gavriella", BoardWriteMode.FederationKind);
            Assert.DoesNotContain($"{Path.DirectorySeparatorChar}ops{Path.DirectorySeparatorChar}", fed.WritePath);
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    // ---- R4-01: the inbound dedup check bypassed the dot-conflict guard ------------------------

    /// <summary>
    /// The early <c>Contains()</c> return short-circuited <c>FederationFold.Apply</c> and its
    /// canonical-content comparison, so a peer sending a DIFFERENT operation on an already-folded
    /// dot was treated as a redelivery — and then ACKED. Replicas kept different operations
    /// permanently, because reconciliation compares dots too.
    /// </summary>
    [Fact]
    public async Task AConflictingDotFromAPeerIsRefusedRatherThanDeduplicated()
    {
        var fold = NewFold();
        var log = new InMemoryBoardLog();
        var svc = new FederationService(Cfg(), new RecordingLink("A"), fold, log);

        var first = FederationOp.Create(new Dot("g", 1), "g", "board_post",
            JsonSerializer.SerializeToElement(new { v = "original" }));
        var conflicting = FederationOp.Create(new Dot("g", 1), "g", "board_post",
            JsonSerializer.SerializeToElement(new { v = "different" }));

        Assert.Equal(1, await svc.ReconcileAsync(new[] { first }, new PeerCapabilities(true, LiveEpoch)));

        await Assert.ThrowsAsync<DotConflictException>(() =>
            svc.ReconcileAsync(new[] { conflicting }, new PeerCapabilities(true, LiveEpoch)));

        Assert.Contains("original", fold.ToCanonicalJson());
        Assert.DoesNotContain("different", fold.ToCanonicalJson());

        // POSITIVE CONTROL: a genuine redelivery of the SAME op is still a quiet no-op.
        Assert.Equal(0, await svc.ReconcileAsync(new[] { first }, new PeerCapabilities(true, LiveEpoch)));
    }

    // ---- R4-02: the tail watched the lane logs and discarded everything in them ----------------

    /// <summary>
    /// A real lane appends a SCHEDULER-NATIVE record whose <c>op_id</c> is a string, so
    /// <c>FederationOp.FromJson</c> throws and the tail silently dropped it. Round 3 pointed the
    /// tail at the live `ops` directories and then pushed nothing from them — which is the entire
    /// point of watching them.
    /// </summary>
    [Fact]
    public async Task ALaneClaimAppendedAfterStartupIsFoldedAndPushed()
    {
        string root = Board();
        try
        {
            string id = NodeId("olamnit");
            var link = new RecordingLink("A");
            var clock = new DrivableClock();
            var svc = new FederationService(Cfg(Peer("olamnit", "olamnit", "192.168.0.136:47890")),
                                            link, NewFold(), new InMemoryBoardLog(), clock);
            await svc.DialAsync(id);

            var own = new SchedulerBoardLog(root, "gavriella", BoardWriteMode.LaneSegment);
            Directory.CreateDirectory(Path.GetDirectoryName(own.WritePath)!);
            File.WriteAllText(own.WritePath, "");

            using var cts = new CancellationTokenSource();
            var tail = svc.RunBoardTailAsync(root, own.WritePath, cts.Token);

            // A REAL LANE appends, in the shape the scheduler actually writes.
            string laneDir = BoardRoot.ActorDirectory(root, "olamnit");
            Directory.CreateDirectory(laneDir);
            File.WriteAllText(Path.Combine(laneDir, "olamnit-ops-000001.jsonl"),
                "{\"actor\":\"olamnit\",\"op_id\":\"olamnit:000042\",\"op_type\":\"claim\",\"seq\":42,\"wp_id\":\"wp-x\"}\n");

            await clock.AdvanceAsync(TimeSpan.FromSeconds(1));
            await clock.AdvanceAsync(TimeSpan.FromSeconds(1));
            cts.Cancel();
            try { await tail; } catch (OperationCanceledException) { }

            var pushed = link.Sent.Where(s => s.Box == FederationService.BoardBox).ToList();
            Assert.NotEmpty(pushed);
            var op = FederationOp.FromJson(Encoding.UTF8.GetString(pushed[0].Bytes));
            Assert.Equal(new Dot("sched:olamnit", 42), op.OpId);
            Assert.Equal("claim", op.Kind);
            Assert.Equal("wp-x", op.Body.GetProperty("wp_id").GetString());   // carried VERBATIM
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    // ---- R4-03: the replay-to-tail handoff gap -------------------------------------------------

    /// <summary>
    /// An operation appended between startup replay finishing and the tail snapshotting the file
    /// length was neither replayed NOR tailed, and could not enter the fold until a restart.
    /// Starting from zero is free of the race: the fold deduplicates, so nothing is pushed twice.
    /// </summary>
    [Fact]
    public async Task AnOpAppendedDuringTheReplayHandoffIsNotLost()
    {
        string root = Board();
        try
        {
            string id = NodeId("olamnit");
            var link = new RecordingLink("A");
            var clock = new DrivableClock();
            var fold = NewFold();
            var svc = new FederationService(Cfg(Peer("olamnit", "olamnit", "192.168.0.136:47890")),
                                            link, fold, new InMemoryBoardLog(), clock);
            await svc.DialAsync(id);

            var own = new SchedulerBoardLog(root, "gavriella", BoardWriteMode.LaneSegment);
            Directory.CreateDirectory(Path.GetDirectoryName(own.WritePath)!);

            // Written BEFORE the tail starts and NOT replayed — the handoff window.
            await own.AppendAsync(Op(NodeId("gavriella"), 1));

            using var cts = new CancellationTokenSource();
            var tail = svc.RunBoardTailAsync(root, own.WritePath, cts.Token);
            await clock.AdvanceAsync(TimeSpan.FromSeconds(1));
            cts.Cancel();
            try { await tail; } catch (OperationCanceledException) { }

            Assert.True(fold.Contains(new Dot(NodeId("gavriella"), 1)));
            Assert.Contains(link.Sent, s => s.Box == FederationService.BoardBox);
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    // ---- R4-08: strict attribution must survive a restart --------------------------------------

    /// <summary>
    /// Startup replay inserted operations straight into the fold, so
    /// <c>require_verified_attribution</c> was bypassed after every restart: an unsigned or tampered
    /// operation already on the board became visible and eligible for propagation. A gate a restart
    /// turns off is not a gate.
    /// </summary>
    [Fact]
    public void StartupReplayEnforcesAttribution()
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
        var svc = new FederationService(cfg, new RecordingLink("A"), fold, new InMemoryBoardLog())
        {
            RequireVerifiedAttribution = true,
        };

        var forged = FederationOp.Create(new Dot(aliceId, 1), aliceId, "claim", Body).SignedBy(mallory);
        var contradictory = FederationOp.Create(new Dot("alice", 2), "bob", "claim", Body);
        var genuine = FederationOp.Create(new Dot(aliceId, 3), aliceId, "claim", Body).SignedBy(alice);

        // Everything on disk goes through the same gate the live paths use.
        Assert.Equal(1, svc.ReplayIntoFold(new[] { forged, contradictory, genuine }));

        Assert.False(fold.Contains(forged.OpId));
        Assert.False(fold.Contains(contradictory.OpId));
        Assert.True(fold.Contains(genuine.OpId));      // POSITIVE CONTROL: the valid one is folded
        Assert.Equal(2, svc.RefusedOps);
    }

    // ---- R4-10: board_actor is a path segment and must be constrained --------------------------

    /// <summary>
    /// It names a directory under the board root, so a rooted value or one containing traversal
    /// resolves the write path OUTSIDE the validated root — creating, by typo or by hostile config,
    /// the exact second-board condition this feature exists to prevent.
    /// </summary>
    [Theory]
    [InlineData("../../elsewhere")]
    [InlineData("C:/somewhere")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    public void AnUnsafeBoardActorIsRefused(string actor) =>
        Assert.Contains((Cfg() with { BoardActor = actor }).Validate(), p => p.StartsWith("board_actor"));

    /// <summary>POSITIVE CONTROL: an ordinary lane name is accepted, so this is not "refuse all".</summary>
    [Fact]
    public void AnOrdinaryBoardActorIsAccepted() =>
        Assert.True((Cfg() with { BoardActor = "gavriella-buildkit" }).IsValid);

    // ---- R4-11: config show must name the settings that decide where ops go --------------------

    /// <summary>
    /// The readback contract exists so an operator can VERIFY configuration. Omitting board_root,
    /// board_actor and the write mode meant `config show` could not answer the one question that
    /// matters most: which board actually receives operations.
    /// </summary>
    [Fact]
    public void TheEffectiveConfigurationNamesTheBoardAndTheWriteMode()
    {
        string rendered = Cfg().RenderEffective();

        Assert.Contains("board_root", rendered);
        Assert.Contains("board_actor", rendered);
        Assert.Contains("writes into", rendered);
        Assert.Contains("attribution", rendered);
        Assert.Contains("identity_path", rendered);

        // And it says which one in words an operator can act on, not just a boolean.
        Assert.Contains("lanes see federated ops", rendered);
        Assert.Contains("lanes do NOT see federated ops",
            (Cfg() with { WriteIntoLaneSegment = false }).RenderEffective());
    }

    // ---- R4-07: an ack is a crossing and must be attributed ------------------------------------

    /// <summary>
    /// The ACK branch recorded the dot but never attributed the crossing, so <c>_crossedFrom</c>
    /// stayed empty, SameMachine stayed null, and the acceptance run's required assertion could not
    /// pass even after a successful remote fold.
    /// </summary>
    [Fact]
    public async Task AnAckAttributesItsCrossingToThePeer()
    {
        string id = NodeId("olamnit");
        var link = new RecordingLink("A");
        var svc = new FederationService(Cfg(Peer("olamnit", "olamnit", "192.168.0.136:47890")),
                                        link, NewFold(), new InMemoryBoardLog());
        await svc.BindAsync();
        await svc.DialAsync(id);

        var op = Op(NodeId("gavriella"), 1);
        await svc.AppendAndPushAsync(op);

        link.PushInbound(new LinkInbound(id, AckProtocol.Encode(op.OpId), AckProtocol.Box));
        await svc.ReceiveOneAsync();

        // The peer is at a routable address, so this is measurable and REMOTE — which is exactly
        // what SC-001 needs to be able to assert.
        Assert.Equal(Tri.No, svc.Status().SameMachine);
    }

    // ---- R4-05: a malformed frame is one peer's problem ----------------------------------------

    /// <summary>
    /// Any admitted peer sending bad JSON in a hello, ack, pull or board frame threw out of the
    /// decoder and terminated federation for EVERY peer — a denial of service any admitted party
    /// could trigger by accident. The decode failure must be catchable per frame.
    /// </summary>
    [Theory]
    [InlineData(HelloProtocol.Box)]
    [InlineData(AckProtocol.Box)]
    [InlineData(PullProtocol.RequestBox)]
    [InlineData(FederationService.BoardBox)]
    public async Task AMalformedFrameThrowsATypeTheServeLoopCanCatch(string box)
    {
        string id = NodeId("olamnit");
        var link = new RecordingLink("A");
        var svc = new FederationService(Cfg(Peer("olamnit", "olamnit", "192.168.0.136:47890")),
                                        link, NewFold(), new InMemoryBoardLog());

        link.PushInbound(new LinkInbound(id, Encoding.UTF8.GetBytes("{ not json at all"), box));

        // The serve loop catches exactly these, so the daemon survives. The point of the test is
        // that the exception is of a CATCHABLE type — an unexpected type escapes to Main and ends
        // federation for every peer.
        var ex = await Record.ExceptionAsync(() => svc.ReceiveOneAsync());
        if (ex is not null)
            Assert.True(ex is JsonException or KeyNotFoundException or FormatException
                              or ArgumentException or InvalidOperationException,
                $"a malformed frame on '{box}' threw {ex.GetType().Name}, which the serve loop does not catch");
    }
}
