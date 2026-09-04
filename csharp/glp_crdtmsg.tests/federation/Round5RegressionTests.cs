// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT
//
// Regression tests for the round-5 adversarial review
// (reviews/102-quic-federation-transport/20260904T185600Z).
//
// FIVE ROUNDS ON ONE BRANCH: 1, 14, 17, 12, 14. **THE REVIEW IS NOT CONVERGING.**
//
// That is the finding, and it outranks any individual defect below. Under the engineer's ruling —
// an era is reviewed only after two consecutive LOW-YIELD rounds — era 102 is NOT reviewed and must
// NOT ship on this evidence.
//
// TWO OF ROUND 5'S FINDINGS ARE REGRESSIONS INTRODUCED BY MY OWN FIXES EARLIER THE SAME SESSION:
//
//   * R3-04 made the passive side answer a hello, which fixed one-way federation — and created an
//     INFINITE control-frame ping-pong between two daemons that both answer.
//   * R4-02/08 routed tailed records through the admitting path, which enforced attribution on them
//     — and DOUBLE-APPENDED every operation `post` created, because in `serve` the log being tailed
//     IS the log being written.
//
// Both fixes were correct about the problem and wrong about the blast radius. A fix is a code change
// like any other and carries the same defect rate; the round that reviews the fixes is not optional.

using System.Net;
using System.Text;
using System.Text.Json;
using GlpRuntime.CrdtMsg.Crdt;
using GlpRuntime.CrdtMsg.Federation;
using GlpRuntime.CrdtMsg.Route;
using Xunit;

namespace GlpRuntime.CrdtMsg.Tests.Federation;

public sealed class Round5RegressionTests
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
        string root = Path.Combine(Path.GetTempPath(), "ynet_r5", Guid.NewGuid().ToString("n")[..8]);
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, BoardRoot.RootMarker), "{\"root_id\":\"t\"}");
        return root;
    }

    // ---- R5-01: MY OWN FIX MADE AN INFINITE LOOP -----------------------------------------------

    /// <summary>
    /// Answering every hello made two daemons volley control frames forever: A's reply is a hello,
    /// which B answers with a hello, which A answers... The fix for a one-way link became an
    /// infinite loop, and both status surfaces still looked healthy throughout.
    /// </summary>
    [Fact]
    public async Task AReplyHelloIsNeverAnsweredAgainSoTheExchangeTerminates()
    {
        string id = NodeId("olamnit");
        var link = new RecordingLink("A");
        var svc = new FederationService(Cfg(Peer("olamnit", "olamnit", "192.168.0.136:47890")),
                                        link, NewFold(), new InMemoryBoardLog());

        // A peer's DECLARATION is answered — that is what fixed one-way federation.
        link.PushInbound(new LinkInbound(id,
            HelloProtocol.Encode(new PeerCapabilities(true, LiveEpoch)), HelloProtocol.Box));
        await svc.ReceiveOneAsync();

        var reply = Assert.Single(link.Sent.Where(s => s.Box == HelloProtocol.Box));
        Assert.True(HelloProtocol.IsReply(reply.Bytes));      // marked, so the peer will not answer it
        Assert.True(HelloProtocol.Decode(reply.Bytes).TermSpaceAware);

        // AND THE PEER'S ANSWER IS NOT ANSWERED. This is what terminates the exchange: replying to
        // every hello made two daemons volley control frames forever.
        link.PushInbound(new LinkInbound(id,
            HelloProtocol.Encode(new PeerCapabilities(true, LiveEpoch), isReply: true), HelloProtocol.Box));
        await svc.ReceiveOneAsync();

        Assert.Single(link.Sent.Where(s => s.Box == HelloProtocol.Box));   // still ONE, not two
    }

    /// <summary>
    /// A peer that RESTARTS must be answered again.
    /// <para>
    /// Suppressing on "have we seen this peer before" was scoped to the PROCESS: when one peer
    /// restarted and the other did not, the survivor's cache still held it, so the fresh declaration
    /// went unanswered and the restarted peer's fail-closed gate refused everything the survivor
    /// sent — permanently, and with both surfaces reporting an admitted peer.
    /// </para>
    /// </summary>
    [Fact]
    public async Task ARestartedPeersFreshDeclarationIsAnswered()
    {
        string id = NodeId("olamnit");
        var link = new RecordingLink("A");
        var svc = new FederationService(Cfg(Peer("olamnit", "olamnit", "192.168.0.136:47890")),
                                        link, NewFold(), new InMemoryBoardLog());

        link.PushInbound(new LinkInbound(id,
            HelloProtocol.Encode(new PeerCapabilities(true, LiveEpoch)), HelloProtocol.Box));
        await svc.ReceiveOneAsync();
        Assert.Single(link.Sent.Where(s => s.Box == HelloProtocol.Box));

        // The peer restarts and declares again — a NEW declaration, not a reply.
        link.PushInbound(new LinkInbound(id,
            HelloProtocol.Encode(new PeerCapabilities(true, LiveEpoch)), HelloProtocol.Box));
        await svc.ReceiveOneAsync();

        Assert.Equal(2, link.Sent.Count(s => s.Box == HelloProtocol.Box));
    }

    // ---- R5-03: MY OWN FIX DOUBLE-APPENDED EVERY POST ------------------------------------------

    /// <summary>
    /// A tailed record is BY DEFINITION already durable — it is being read from the log. In `serve`
    /// the log being tailed IS the log being written, so routing it through the appending path wrote
    /// a second copy of every operation `post` created, into an append-only journal nothing can
    /// compact.
    /// </summary>
    [Fact]
    public async Task ATailedRecordIsNotAppendedASecondTime()
    {
        string root = Board();
        try
        {
            string id = NodeId("olamnit");
            var link = new RecordingLink("A");
            var clock = new DrivableClock();

            // The SAME log object is both the service's store and the tail's source — exactly as
            // `serve` wires it.
            var log = new SchedulerBoardLog(root, "gavriella", BoardWriteMode.LaneSegment);
            var svc = new FederationService(Cfg(Peer("olamnit", "olamnit", "192.168.0.136:47890")),
                                            link, NewFold(), log, clock);
            await svc.DialAsync(id);

            Directory.CreateDirectory(Path.GetDirectoryName(log.WritePath)!);
            File.WriteAllText(log.WritePath, "");

            using var cts = new CancellationTokenSource();
            var tail = svc.RunBoardTailAsync(root, log.WritePath, cts.Token);

            // ANOTHER process appends — this is `post`.
            var writer = new SchedulerBoardLog(root, "gavriella", BoardWriteMode.LaneSegment);
            await writer.AppendAsync(Op(NodeId("gavriella"), 1));

            await clock.AdvanceAsync(TimeSpan.FromSeconds(1));
            await clock.AdvanceAsync(TimeSpan.FromSeconds(1));
            cts.Cancel();
            try { await tail; } catch (OperationCanceledException) { }

            // EXACTLY ONE line on disk. Two means the tail re-appended what it read.
            int lines = File.ReadAllLines(log.WritePath).Count(l => !string.IsNullOrWhiteSpace(l));
            Assert.Equal(1, lines);

            // POSITIVE CONTROL: it was still folded and pushed, so the gate did run.
            Assert.Contains(link.Sent, s => s.Box == FederationService.BoardBox);
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    // ---- R6-04: a permission refusal must not be retried as lock contention --------------------

    /// <summary>
    /// The refusal was raised as a plain <c>IOException</c>, which the minting retry loop caught —
    /// and its next iteration took the existing-file fast path and returned the UNPROTECTED key as
    /// a success. A security refusal and lock contention must not look alike to a catch clause.
    /// <para>
    /// The real check is Unix-only, so on this host it never runs and a test written against it
    /// would assert nothing. The verdict is injected so the refusal path is actually exercised.
    /// </para>
    /// </summary>
    [Fact]
    public void AnInsecureKeyIsRefusedAndNotRetriedIntoSuccess()
    {
        string dir = Path.Combine(Path.GetTempPath(), "ynet_perm", Guid.NewGuid().ToString("n")[..8]);
        string path = Path.Combine(dir, "node.key");
        try
        {
            // THE MINT PATH, which is where the retry loop lives and where the defect was.
            // Permissions are insecure from the outset, exactly as on a filesystem that will not
            // honour them.
            int mintProbes = 0;
            NodeIdentityStore.PermissionsAreInsecureOverride = _ => { mintProbes++; return true; };
            Assert.Throws<InsecureKeyPermissionsException>(
                () => new NodeIdentityStore(path).LoadOrMint("host"));

            // EXACTLY ONE ATTEMPT. Caught by the lock-contention retry the refusal was re-raised
            // anyway — a hundred attempts later, having RE-MINTED AND RE-EXPOSED the key each time.
            // The attempt count is the only thing separating "refused" from "retried into refusal".
            Assert.Equal(1, mintProbes);
            Assert.False(File.Exists(path));

            // Now mint properly, with permissions considered fine.
            NodeIdentityStore.PermissionsAreInsecureOverride = _ => false;
            var store = new NodeIdentityStore(path);
            Assert.NotNull(store.LoadOrMint("host"));
            Assert.True(File.Exists(path));

            // Now the key is insecure. Loading it must REFUSE — not retry into the fast path and
            // hand back the very key the refusal exists to withhold.
            int probes = 0;
            NodeIdentityStore.PermissionsAreInsecureOverride = _ => { probes++; return true; };
            Assert.Throws<InsecureKeyPermissionsException>(
                () => new NodeIdentityStore(path).LoadOrMint("host"));

            // EXACTLY ONE ATTEMPT. Asserting only the exception type does not discriminate: when the
            // refusal is caught by the lock-contention retry it is re-raised anyway, just a hundred
            // attempts later — and on each of those the key is re-minted and re-exposed. The
            // probe COUNT is the only thing that separates "refused" from "retried into refusal".
            Assert.Equal(1, probes);

            // And the exposed key is GONE, not left on disk for the next process to pick up.
            Assert.False(File.Exists(path));
        }
        finally
        {
            NodeIdentityStore.PermissionsAreInsecureOverride = null;
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    // ---- R6-05: the counter invariant belongs at CONSTRUCTION, not only at the decoder ---------

    /// <summary>
    /// Enforcing it in <c>FromJson</c> alone left three ways in: the factory, the scheduler-native
    /// adapter that calls it, and any local caller. A nonpositive counter is reported as ALREADY
    /// HELD by every frontier, so such an operation can never be recovered after a lost push.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void ANonpositiveCounterIsRefusedAtConstruction(long counter) =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            FederationOp.Create(new Dot("g", counter), "g", "board_post", Body));

    /// <summary>The scheduler-native adapter goes through the factory, so it inherits the guard.</summary>
    [Fact]
    public void ASchedulerRowWithANonpositiveSeqIsNotAdapted()
    {
        using var doc = JsonDocument.Parse(
            "{\"actor\":\"gavriella\",\"op_id\":\"gavriella:000000\",\"op_type\":\"claim\",\"seq\":0}");

        // The adapter must not hand back an operation the frontier would call already-held.
        Assert.ThrowsAny<Exception>(() => SchedulerBoardLog.AdaptSchedulerLine(doc.RootElement));

        // POSITIVE CONTROL: a well-formed row still adapts.
        using var ok = JsonDocument.Parse(
            "{\"actor\":\"gavriella\",\"op_id\":\"gavriella:000007\",\"op_type\":\"claim\",\"seq\":7}");
        Assert.Equal(new Dot("gavriella", 7),
            SchedulerBoardLog.AdaptSchedulerLine(ok.RootElement)!.OpId);
    }

    // ---- R5-02: strict attribution refused this host's OWN operations --------------------------

    /// <summary>
    /// The verifier table held only configured PEERS, so with
    /// <c>require_verified_attribution=true</c> the tail and startup replay classified THIS host's
    /// own signed operations as UnverifiedOrigin and refused them. Turning the security setting on
    /// disabled the host's ability to publish at all — a gate that locks the door from the inside.
    /// </summary>
    [Fact]
    public void StrictAttributionDoesNotRefuseThisHostsOwnOperations()
    {
        using var mine = QuicLinkTransport.CreateDevCert("gavriella");
        string myId = NodeIdentityStore.DeriveNodeId(mine);

        var fold = NewFold();
        var svc = new FederationService(Cfg(), new RecordingLink("A"), fold, new InMemoryBoardLog())
        {
            RequireVerifiedAttribution = true,
        };

        var own = FederationOp.Create(new Dot(myId, 1), myId, "board_post", Body).SignedBy(mine);

        // BEFORE enrolment the host's own op is refused — this is the defect, asserted directly.
        Assert.Equal(0, svc.ReplayIntoFold(new[] { own }));
        Assert.Equal(1, svc.RefusedOps);

        // AFTER enrolment it verifies.
        svc.EnrolLocalIdentity(myId, NodeIdentityStore.ExportSpki(mine));
        Assert.Equal(1, svc.ReplayIntoFold(new[] { own }));
        Assert.True(fold.Contains(own.OpId));
    }

    // ---- R5-07: a nonpositive counter is reported as already-held by every frontier -------------

    /// <summary>
    /// <c>FederationFrontier</c>'s contiguous run defaults to 0, so <c>Contains</c> answers TRUE for
    /// every nonpositive dot. If such an operation's push were lost, reconciliation would suppress
    /// it forever and the replicas diverge silently.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ANonpositiveCounterIsRefusedAtTheDecoder(long counter)
    {
        // The hazard, asserted so the reason for the refusal is visible and not just asserted.
        Assert.True(new FederationFrontier().Contains(new Dot("g", counter)));

        string json = "{\"op_id\":{\"peer\":\"g\",\"counter\":" + counter + "},\"origin\":\"g\","
                    + "\"kind\":\"board_post\",\"deps\":[],\"pred_hash\":\"\",\"body\":null}";
        Assert.Throws<FormatException>(() => FederationOp.FromJson(json));
    }

    /// <summary>POSITIVE CONTROL: counter 1 decodes, so this is not "refuse everything".</summary>
    [Fact]
    public void CounterOneIsAccepted()
    {
        // A CONFORMANT pred_hash — the decoder now requires one, and this test is about the
        // COUNTER. A fixture that breaks a different rule cannot show you which rule failed.
        string pred = Convert.ToHexStringLower(HashChain.PredHash(new Dot("g", 1), Array.Empty<Dot>()));
        string json = "{\"op_id\":{\"peer\":\"g\",\"counter\":1},\"origin\":\"g\","
                    + "\"kind\":\"board_post\",\"deps\":[],\"pred_hash\":\"" + pred + "\",\"body\":null}";
        Assert.Equal(1, FederationOp.FromJson(json).OpId.Counter);
        Assert.False(new FederationFrontier().Contains(new Dot("g", 1)));
    }

    // ---- R5-14: health must reflect LIVE connectivity ------------------------------------------

    /// <summary>
    /// <c>_admitted</c> records that a peer has EVER completed verification and is deliberately
    /// retained across a send failure — so reading health from it meant a service whose last link
    /// had dropped went on reporting Federating. FR-004 says a degraded deployment is reported
    /// explicitly, never as success.
    /// </summary>
    [Fact]
    public async Task HealthDegradesWhenTheLastLinkDrops()
    {
        string id = NodeId("olamnit");
        var link = new RecordingLink("A");
        var svc = new FederationService(Cfg(Peer("olamnit", "olamnit", "192.168.0.136:47890")),
                                        link, NewFold(), new InMemoryBoardLog());

        await svc.DialAsync(id);
        Assert.Equal(FederationHealth.Federating, svc.Health);

        // The established connection closes.
        link.FailSends = true;
        await svc.AppendAndPushAsync(Op(NodeId("gavriella"), 1));

        Assert.Equal(FederationHealth.DegradedLocalOnly, svc.Health);

        // And the peer is still recorded as HAVING been admitted — a different fact, still true.
        Assert.Equal(Tri.Yes, svc.Status().PeerAdmitted);
    }

    // ---- R5-12: the whole endpoint must validate, port included --------------------------------

    /// <summary>
    /// Checking only the host half let "192.0.2.1:notaport" pass; ToPeerSet then silently dropped
    /// it, and the configuration error resurfaced much later as a name-resolution or reachability
    /// failure — pointing the operator at the network for a typo.
    /// </summary>
    [Theory]
    [InlineData("192.0.2.1:notaport")]
    [InlineData("192.0.2.1:0")]
    [InlineData("192.0.2.1:70000")]
    [InlineData("192.0.2.1")]
    public void AnEndpointWithoutAValidPortIsRefused(string endpoint)
    {
        var cfg = Cfg(new PeerConfig
        {
            Name = "olamnit", NodeId = NodeId("olamnit"), Endpoints = { endpoint },
        });
        Assert.Contains(cfg.Validate(), p => p.Contains("endpoints"));
    }

    /// <summary>POSITIVE CONTROL: a complete endpoint is accepted and survives into the peer set.</summary>
    [Fact]
    public void ACompleteEndpointIsAccepted()
    {
        var cfg = Cfg(Peer("olamnit", "olamnit", "192.168.0.136:47890"));
        Assert.True(cfg.IsValid);
        Assert.Single(cfg.ToPeerSet().Find(NodeId("olamnit"))!.Endpoints);
    }

    // ---- R5-10: a pull-recovered crossing must be attributed ------------------------------------

    /// <summary>
    /// A pull response arrives with an authenticated sender. Discarding it recorded a null crossing,
    /// so an operation recovered only through the pull reported "an op crossed" while leaving
    /// same_machine unknown even for a peer that HAD been measured — withholding exactly the
    /// cross-host evidence SC-001 needs.
    /// </summary>
    [Fact]
    public async Task AnOpRecoveredThroughThePullIsAttributedToItsSender()
    {
        string id = NodeId("olamnit");
        var link = new RecordingLink("A");
        var svc = new FederationService(Cfg(Peer("olamnit", "olamnit", "192.168.0.136:47890")),
                                        link, NewFold(), new InMemoryBoardLog());
        await svc.BindAsync();
        await svc.DialAsync(id);            // measures this peer as REMOTE

        link.PushInbound(new LinkInbound(id,
            HelloProtocol.Encode(new PeerCapabilities(true, LiveEpoch)), HelloProtocol.Box));
        await svc.ReceiveOneAsync();

        link.PushInbound(new LinkInbound(id,
            PullProtocol.EncodeResponse(new[] { Op(id, 9) }), PullProtocol.ResponseBox));
        await svc.ReceiveOneAsync();

        var status = svc.Status();
        Assert.Equal(Tri.Yes, status.OpReceivedFromPeer);
        Assert.Equal(Tri.No, status.SameMachine);   // was Unknown while the sender was discarded
    }

    // ---- R5-09: the tail must not allocate the whole board ------------------------------------

    /// <summary>
    /// Starting every log at offset 0 — which is what closes the replay-to-tail race — means the
    /// remaining suffix is the WHOLE FILE on the first pass. Allocating that at once OOMs on a large
    /// board and faults the tail task, which nothing awaits, so push-on-append stops silently.
    /// </summary>
    [Fact]
    public async Task ALargeBacklogIsTailedInBoundedChunksAndStillConverges()
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

            // A backlog comfortably larger than one chunk.
            string path = Path.Combine(root, "big.jsonl");
            string filler = new('x', 32 * 1024);
            using (var w = new StreamWriter(path))
                for (long i = 1; i <= 200; i++)
                    w.WriteLine(FederationOp.Create(new Dot("g", i), "g", "board_post",
                        JsonSerializer.SerializeToElement(new { body = filler })).ToCanonicalJson());

            Assert.True(new FileInfo(path).Length > FederationService.MaxTailChunkBytes);

            using var cts = new CancellationTokenSource();
            var tail = svc.RunLogTailAsync(new[] { path }, cts.Token);

            // ONE tick must NOT consume the whole backlog — that is what "bounded" means, and
            // asserting only the end state passes whether the read is chunked or not.
            await clock.AdvanceAsync(TimeSpan.FromSeconds(1));
            int afterOneTick = fold.Count;
            Assert.InRange(afterOneTick, 1, 199);

            // And successive ticks converge on all of it.
            for (int i = 0; i < 12; i++) await clock.AdvanceAsync(TimeSpan.FromSeconds(1));
            cts.Cancel();
            try { await tail; } catch (OperationCanceledException) { }

            Assert.Equal(200, fold.Count);   // ALL of it, just not all at once
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    // ---- R5-04: the check-append-apply sequence must be atomic ---------------------------------

    /// <summary>
    /// The receive, pull and board-tail paths call the admission sequence concurrently. Without
    /// serialisation two deliveries can both observe <c>Contains == false</c>, BOTH append to the
    /// append-only log, and only then deduplicate in the fold — poisoning every future replay on a
    /// journal that has no delete.
    /// </summary>
    [Fact]
    public async Task ConcurrentDeliveriesOfOneOpAppendItExactlyOnce()
    {
        var fold = NewFold();

        // A DETERMINISTIC barrier, not a hopeful Task.Yield.
        //
        // The first attempt used a yielding in-memory log and the mutation that REMOVES the
        // serialisation still passed: the interleaving simply did not happen to occur. A
        // concurrency test that depends on luck is decorative. This log blocks inside the append
        // until the test releases it, so the second caller is GUARANTEED to observe the
        // pre-append state — which is exactly the window the lock exists to close.
        var barrier = new BarrierBoardLog();
        var svc = new FederationService(Cfg(), new RecordingLink("A"), fold, barrier);

        var op = Op("g", 1);
        var caps = new PeerCapabilities(true, LiveEpoch);

        var first = svc.ReconcileAsync(new[] { op }, caps);
        await barrier.EnteredAppend.Task;          // first caller is INSIDE the append

        var second = svc.ReconcileAsync(new[] { op }, caps);
        await Task.Delay(50);                      // give the second caller every chance to race

        barrier.Release();
        await Task.WhenAll(first, second);

        Assert.Equal(1, barrier.Count);            // ONE durable record, not two
        Assert.Equal(1, fold.Count);
    }
}

/// <summary>
/// A board log whose first append BLOCKS until the test releases it, so a concurrency window is
/// created on purpose rather than waited for.
/// </summary>
public sealed class BarrierBoardLog : IBoardLog
{
    private readonly List<FederationOp> _ops = new();
    private readonly TaskCompletionSource _release =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _entries;

    /// <summary>Completes as soon as a caller is inside the append and blocked.</summary>
    public TaskCompletionSource EnteredAppend { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Release() => _release.TrySetResult();

    public int Count { get { lock (_ops) return _ops.Count; } }

    public async Task AppendAsync(FederationOp op, CancellationToken ct = default)
    {
        if (Interlocked.Increment(ref _entries) == 1)
        {
            EnteredAppend.TrySetResult();
            await _release.Task.ConfigureAwait(false);
        }
        lock (_ops) _ops.Add(op);
    }

    public Task<IReadOnlyList<FederationOp>> ReadAllAsync(CancellationToken ct = default)
    {
        lock (_ops) return Task.FromResult<IReadOnlyList<FederationOp>>(_ops.ToList());
    }
}
