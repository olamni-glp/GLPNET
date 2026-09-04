// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT
//
// Regression tests for the seventeen defects found by the round-3 adversarial review
// (reviews/102-quic-federation-transport/20260904T174339Z).
//
// THE NUMBER THAT MATTERS: three review rounds on the SAME branch with the SAME scope and the SAME
// settings returned 1, then 14, then 17 findings. Round 3 found ZERO recurrences of round 2's
// fourteen — every fix held — and seventeen defects nobody had seen, several of them in code
// written that same day to fix round 2. A single clean review round is not evidence of anything.
//
// Several of these are the fix-introduced-a-new-fault shape, which is the most dangerous kind: the
// per-peer same-machine field, the SC-001 assertion that could never pass, and the batch abort that
// turned one refusal into permanent non-convergence were all introduced BY the round-2 remediation.

using System.Net;
using System.Text;
using System.Text.Json;
using GlpRuntime.CrdtMsg.Crdt;
using GlpRuntime.CrdtMsg.Federation;
using GlpRuntime.CrdtMsg.Route;
using Xunit;

namespace GlpRuntime.CrdtMsg.Tests.Federation;

public sealed class Round3RegressionTests
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

    // ---- R3-04: complete the capability exchange, or federation is permanently one-way ---------

    /// <summary>
    /// The gate is fail-closed BOTH ways, so a side that never declares its own capabilities has
    /// every push and pull response refused by the other. A listener that can only be dialled (a
    /// peer entry with no endpoints, which the config deliberately allows) never announced, and
    /// federation was permanently one-way while both surfaces showed an admitted peer.
    /// </summary>
    [Fact]
    public async Task ThePassiveSideAnswersAHelloWithItsOwn()
    {
        string id = NodeId("olamnit");
        var link = new RecordingLink("A");
        var svc = new FederationService(Cfg(Peer("olamnit", "olamnit", "192.168.0.136:47890")),
                                        link, NewFold(), new InMemoryBoardLog());

        // Never dialled — this host is the PASSIVE side.
        link.PushInbound(new LinkInbound(id,
            HelloProtocol.Encode(new PeerCapabilities(true, LiveEpoch)), HelloProtocol.Box));
        await svc.ReceiveOneAsync();

        var hello = link.Sent.Single(s => s.Box == HelloProtocol.Box);
        Assert.Equal(id, hello.To);
        Assert.True(HelloProtocol.Decode(hello.Bytes).TermSpaceAware);
    }

    // ---- R3-05: same-machine evidence must belong to the peer that crossed --------------------

    /// <summary>
    /// A single overwritten field let an operation from a same-machine peer inherit the last remote
    /// peer's "No" and be reported as cross-host evidence — defeating FR-022 on the exact path
    /// SC-001 depends on.
    /// </summary>
    [Fact]
    public async Task SameMachineIsKeyedByPeerNotOverwrittenByTheLastDial()
    {
        string remoteId = NodeId("olamnit"), localId = NodeId("loopback-peer");
        var link = new RecordingLink("A");
        var svc = new FederationService(
            Cfg(Peer("olamnit", "olamnit", "192.168.0.136:47890"),
                Peer("loopback", "loopback-peer", "127.0.0.1:47890")),
            link, NewFold(), new InMemoryBoardLog());

        await svc.BindAsync();
        await svc.DialAsync(localId);    // same machine
        await svc.DialAsync(remoteId);   // then a remote one — this used to overwrite the verdict

        // An op crosses FROM THE LOOPBACK PEER. That is same-machine evidence and must say so.
        link.PushInbound(new LinkInbound(localId,
            HelloProtocol.Encode(new PeerCapabilities(true, LiveEpoch)), HelloProtocol.Box));
        await svc.ReceiveOneAsync();
        var op = Op(localId, 1);
        link.PushInbound(new LinkInbound(localId,
            Encoding.UTF8.GetBytes(op.ToCanonicalJson()), FederationService.BoardBox));
        await svc.ReceiveOneAsync();

        Assert.Equal(Tri.Yes, svc.Status().SameMachine);
    }

    /// <summary>An unmeasurable same-machine probe is UNKNOWN, never a measured "different host".</summary>
    [Fact]
    public void AnUnmeasurableSameMachineProbeIsUnknown()
    {
        // Measurable and TRUE.
        Assert.Equal(Tri.Yes, FederationStatusProbe.SameMachineTri(IPAddress.Any, IPAddress.Loopback));
        // Measurable and FALSE.
        Assert.Equal(Tri.No, FederationStatusProbe.SameMachineTri(IPAddress.Any, IPAddress.Parse("203.0.113.7")));

        // THE BRANCH THAT MATTERS, and the reason the enumeration is injectable: on a healthy host
        // the real probe never throws, so a test written against it cannot tell "returns Unknown on
        // failure" from "never fails here". A mutation reverting this to Tri.No SURVIVED the first
        // version of this test, which is what a decorative test looks like.
        Assert.Equal(Tri.Unknown, FederationStatusProbe.SameMachineTri(
            IPAddress.Any, IPAddress.Parse("203.0.113.7"),
            () => throw new InvalidOperationException("interface enumeration unavailable")));

        // An EMPTY enumeration is equally unmeasured: every host has at least a loopback address.
        Assert.Equal(Tri.Unknown, FederationStatusProbe.SameMachineTri(
            IPAddress.Any, IPAddress.Parse("203.0.113.7"), Array.Empty<IPAddress>));

        // POSITIVE CONTROL: a WORKING enumeration still answers No for a genuinely remote address,
        // so Unknown is the failure signal and not the only answer this can give.
        Assert.Equal(Tri.No, FederationStatusProbe.SameMachineTri(
            IPAddress.Any, IPAddress.Parse("203.0.113.7"), () => new[] { IPAddress.Parse("192.168.0.108") }));

        // The boolean form collapses "could not measure" into false, i.e. into positive cross-host
        // evidence. That is why callers feeding the status surface must use the Tri form.
        Assert.False(FederationStatusProbe.IsSameMachine(IPAddress.Any, IPAddress.Parse("203.0.113.7")));
    }

    // ---- R3-07: a reused dot is a conflict, not a redelivery ----------------------------------

    /// <summary>
    /// Silently ignoring a second, DIFFERENT operation on the same dot made the fold arrival-order
    /// dependent: replicas receiving them in opposite orders keep different values forever while
    /// both report converged, breaking FR-012.
    /// </summary>
    [Fact]
    public void TwoDifferentOpsOnOneDotAreRefusedNotSilentlyDropped()
    {
        var fold = NewFold();
        var a = FederationOp.Create(new Dot("g", 1), "g", "board_post",
            JsonSerializer.SerializeToElement(new { v = "a" }));
        var b = FederationOp.Create(new Dot("g", 1), "g", "board_post",
            JsonSerializer.SerializeToElement(new { v = "b" }));

        Assert.True(fold.Apply(a));
        Assert.Throws<DotConflictException>(() => fold.Apply(b));

        // POSITIVE CONTROL: a true redelivery is still a quiet no-op, so the throw is discriminating.
        Assert.False(fold.Apply(a));
        Assert.Equal(1, fold.Count);
    }

    /// <summary>
    /// The property this protects: two folds given the same ops in OPPOSITE orders render the same
    /// bytes (FR-012 / SC-003).
    /// </summary>
    [Fact]
    public void OppositeArrivalOrdersStillRenderIdenticalBytes()
    {
        var ops = new[] { Op("g", 1), Op("g", 2), Op("o", 5) };
        var forward = NewFold();
        var backward = NewFold();
        forward.ApplyAll(ops);
        backward.ApplyAll(ops.Reverse());
        Assert.Equal(forward.ToCanonicalJson(), backward.ToCanonicalJson());
    }

    // ---- R3-14: a retired op with no term is still retired ------------------------------------

    /// <summary>
    /// Consulting the term first meant a retired ordinary post — or a retired RETIREMENT — reported
    /// NotLeadershipBearing, contradicting SC-012 and making FR-029's "a retirement is itself an
    /// ordinary op" unobservable.
    /// </summary>
    [Fact]
    public void ARetiredOpWithNoTermStillReportsAsRetired()
    {
        var fold = NewFold();
        var post = Op("g", 1);                                   // no term at all
        fold.Apply(post);
        Assert.Equal(OrderingDisposition.NotLeadershipBearing, fold.DispositionOf(post));

        fold.Apply(RetirementOp.Create(new Dot("g", 2), "g", post.OpId, "wrong body"));
        Assert.Equal(OrderingDisposition.UnorderedLegacy, fold.DispositionOf(post));
        Assert.True(fold.Contains(post.OpId));                   // retained, never deleted (SC-012)
    }

    /// <summary>A retirement is itself retirable — the FR-029 property, now observable.</summary>
    [Fact]
    public void ARetirementIsItselfRetirable()
    {
        var fold = NewFold();
        var post = Op("g", 1);
        var retire = RetirementOp.Create(new Dot("g", 2), "g", post.OpId, "first");
        fold.Apply(post);
        fold.Apply(retire);

        fold.Apply(RetirementOp.Create(new Dot("g", 3), "g", retire.OpId, "the retirement was wrong"));
        Assert.Equal(OrderingDisposition.UnorderedLegacy, fold.DispositionOf(retire));
    }

    // ---- R3-08: one refusal must not strand everything behind it ------------------------------
    // (covered by Round2RegressionTests.ARefusedOpDoesNotStrandTheValidOpsBehindIt)

    // ---- R3-09: the policy refusal must reach the separate status process ---------------------

    /// <summary>
    /// `serve` EXITS on a Smart App Control bind failure telling the operator to run `status`, which
    /// is a different process. Without publishing the refusal, the one failure FR-023 exists to name
    /// was the one status could not name.
    /// </summary>
    [Fact]
    public void APolicyRefusalSurvivesTheProcessBoundary()
    {
        string dir = Path.Combine(Path.GetTempPath(), "ynet_pr", Guid.NewGuid().ToString("n")[..8]);
        string path = Path.Combine(dir, "serving-status.json");
        try
        {
            var refusal = new PolicyRefusal("Smart App Control",
                PolicyRefusal.SmartAppControlHResult, "blocked");
            var status = new FederationStatus { ListenerBound = Tri.No, PolicyRefused = refusal };

            StatusHeartbeat.From(status, 0, DateTimeOffset.UtcNow).Publish(path);
            var read = StatusHeartbeat.ReadFresh(DateTimeOffset.UtcNow, path);

            Assert.NotNull(read);
            Assert.NotNull(read!.PolicyRefused);
            Assert.Equal("Smart App Control", read.ToStatus(Tri.Yes).PolicyRefused!.Policy);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    // ---- R3-12: an identity mismatch is not unreachability ------------------------------------

    /// <summary>
    /// The real transport throws CrdtMsgException when the far end's hello claims a different node
    /// id. Recognising only AuthenticationException sent that down the generic path and reported
    /// Unreachable — pointing the operator at the network for an identity fault (FR-008).
    /// </summary>
    [Fact]
    public async Task AHelloIdentityMismatchIsReportedAsPinMismatchNotUnreachable()
    {
        string id = NodeId("olamnit");
        var link = new RecordingLink("A")
        {
            ThrowOnConnect = new GlpRuntime.CrdtMsg.Envelope.CrdtMsgException(
                "dialed 'x' but the far end claims 'y' — link refused"),
        };
        var svc = new FederationService(Cfg(Peer("olamnit", "olamnit", "192.168.0.136:47890")),
                                        link, NewFold(), new InMemoryBoardLog());

        Assert.Equal(AdmissionOutcome.PinMismatch, await svc.DialAsync(id));

        // POSITIVE CONTROL: a genuine reachability failure still reports Unreachable, so the two
        // conditions remain distinguishable — which is the whole of FR-008.
        var down = new RecordingLink("A") { Broken = true };
        var svc2 = new FederationService(Cfg(Peer("olamnit", "olamnit", "192.168.0.136:47890")),
                                         down, NewFold(), new InMemoryBoardLog());
        Assert.Equal(AdmissionOutcome.Unreachable, await svc2.DialAsync(id));
    }

    // ---- R3-13: the epoch id must carry no wall clock -----------------------------------------

    /// <summary>
    /// FR-026 forbids deriving the term-space id from time in ANY encoding. The guard rejected only
    /// all-digit strings, so the mint command's own "ynet-epoch-2026-09-8240c4" sailed through it.
    /// A guard that cannot catch its own caller's output is decorative.
    /// </summary>
    [Theory]
    [InlineData("5961694")]                      // the live fossil: floor(unix_ts/300)
    [InlineData("ynet-epoch-2026-09-8240c4")]    // what this codebase itself minted
    [InlineData("ynet-epoch-202609-abc")]
    [InlineData("epoch-20260904")]
    public void AClockDerivedEpochIdIsRefusedInEveryEncoding(string id) =>
        Assert.True(TermSpaceRegistry.LooksClockDerived(id));

    /// <summary>POSITIVE CONTROL: a genuinely random id passes, so the guard is not just "reject all".</summary>
    [Theory]
    [InlineData("ynet-epoch-7f3a91c2e04b5d68")]
    [InlineData("ynet-epoch-c0ffee")]
    public void ARandomEpochIdIsAccepted(string id) =>
        Assert.False(TermSpaceRegistry.LooksClockDerived(id));

    // ---- R3-17: node ids are canonicalised before the ordinal tables are built ----------------

    /// <summary>
    /// The peer map compares case-insensitively but the pin and SPKI tables are keyed ordinally, as
    /// they must be — the transport compares its dial key ordinally. An uppercase configuration
    /// therefore validated, sat in the map, and then missed every ordinal lookup.
    /// </summary>
    [Fact]
    public void AnUppercaseNodeIdStillMatchesTheOrdinalTransportTables()
    {
        string id = NodeId("olamnit");
        var cfg = Cfg(new PeerConfig
        {
            Name = "olamnit",
            NodeId = id.ToUpperInvariant(),          // an operator pasting from a different tool
            Endpoints = { "192.168.0.136:47890" },
            Spki = "",
        });

        var set = cfg.ToPeerSet();
        Assert.True(set.ToPinTable().ContainsKey(id));   // the LOWERCASE form the transport presents
        Assert.NotNull(set.Find(id));
        Assert.NotNull(set.Find(id.ToUpperInvariant()));
    }

    // ---- R3-06: a partial line must not be consumed -------------------------------------------

    /// <summary>
    /// Reading mid-append is the NORMAL case for a file another process writes. Consuming the
    /// unterminated fragment and advancing past it lost the operation entirely: the fragment failed
    /// to parse, and the next poll saw only the remaining suffix, which also failed.
    /// </summary>
    [Fact]
    public async Task AnOpWrittenInTwoHalvesIsStillFoldedAndPushed()
    {
        string dir = Path.Combine(Path.GetTempPath(), "ynet_tail", Guid.NewGuid().ToString("n")[..8]);
        try
        {
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "log.jsonl");
            File.WriteAllText(path, "");

            string id = NodeId("olamnit");
            var link = new RecordingLink("A");
            var clock = new DrivableClock();
            var svc = new FederationService(Cfg(Peer("olamnit", "olamnit", "192.168.0.136:47890")),
                                            link, NewFold(), new InMemoryBoardLog(), clock);
            await svc.DialAsync(id);

            using var cts = new CancellationTokenSource();
            var tail = svc.RunLogTailAsync(new[] { path }, cts.Token);

            // HALF a line — exactly what a reader sees mid-append.
            string json = Op(NodeId("gavriella"), 1).ToCanonicalJson();
            File.AppendAllText(path, json[..(json.Length / 2)]);
            await clock.AdvanceAsync(TimeSpan.FromSeconds(1));
            Assert.DoesNotContain(link.Sent, s => s.Box == FederationService.BoardBox);

            // ...and now the rest of it.
            File.AppendAllText(path, json[(json.Length / 2)..] + "\n");
            await clock.AdvanceAsync(TimeSpan.FromSeconds(1));

            cts.Cancel();
            try { await tail; } catch (OperationCanceledException) { }

            Assert.Contains(link.Sent, s => s.Box == FederationService.BoardBox);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    // ---- R3-02: the tail must watch the LIVE lane logs, not only this host's file --------------

    /// <summary>
    /// `serve` replayed every actor's log at startup and then watched only its own file, so a claim
    /// a real lane appended a minute later never entered the running fold and was never pushed until
    /// a restart. The board is the thing that changes.
    /// </summary>
    [Fact]
    public async Task ClaimsAppendedByALaneAfterStartupAreFoldedAndPushed()
    {
        string root = Path.Combine(Path.GetTempPath(), "ynet_board", Guid.NewGuid().ToString("n")[..8]);
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

            var own = new SchedulerBoardLog(root, "gavriella");
            Directory.CreateDirectory(Path.GetDirectoryName(own.WritePath)!);
            File.WriteAllText(own.WritePath, "");

            using var cts = new CancellationTokenSource();
            var tail = svc.RunBoardTailAsync(root, own.WritePath, cts.Token);

            // ANOTHER actor's federation log appears AFTER the daemon started.
            string peerDir = Path.Combine(root, SchedulerBoardLog.FederationKindName, "olamnit");
            Directory.CreateDirectory(peerDir);
            File.WriteAllText(Path.Combine(peerDir, "olamnit-fedops-000001.jsonl"),
                Op(NodeId("olamnit-actor"), 1).ToCanonicalJson() + "\n");

            await clock.AdvanceAsync(TimeSpan.FromSeconds(1));
            await clock.AdvanceAsync(TimeSpan.FromSeconds(1));
            cts.Cancel();
            try { await tail; } catch (OperationCanceledException) { }

            Assert.Contains(link.Sent, s => s.Box == FederationService.BoardBox);
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    // ---- R3-11: first-run identity minting is a cross-process race ----------------------------

    /// <summary>
    /// `serve`, `post` and `identity` can start together on a fresh host — the runbook has the
    /// operator do exactly that. An unlocked exists-then-write let two processes mint DIFFERENT
    /// certificates, each return its own, and only one survive: the loser signed operations no peer
    /// could verify, and the host's identity differed between two commands seconds apart.
    /// </summary>
    [Fact]
    public void ConcurrentFirstRunMintingYieldsExactlyOneIdentity()
    {
        string dir = Path.Combine(Path.GetTempPath(), "ynet_id", Guid.NewGuid().ToString("n")[..8]);
        string path = Path.Combine(dir, "node.key");
        try
        {
            var ids = new System.Collections.Concurrent.ConcurrentBag<string>();
            Parallel.For(0, 16, _ =>
                ids.Add(NodeIdentityStore.DeriveNodeId(new NodeIdentityStore(path).LoadOrMint("host"))));

            // EVERY caller must get the SAME identity — including the ones that raced.
            Assert.Single(ids.Distinct());

            // And it must be the one actually persisted, or the winner signs with a key on disk that
            // is not its own.
            Assert.Equal(ids.First(),
                NodeIdentityStore.DeriveNodeId(new NodeIdentityStore(path).LoadOrMint("host")));
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    // ---- R3-15: post and serve append to one file from two processes --------------------------

    /// <summary>
    /// The instance semaphore serialises one object; the runbook has two PROCESSES appending to the
    /// same file. On Windows that collides with a sharing violation, so a legitimate post fails.
    /// </summary>
    [Fact]
    public async Task ConcurrentAppendsFromSeparateLogInstancesAllSucceed()
    {
        string root = Path.Combine(Path.GetTempPath(), "ynet_board", Guid.NewGuid().ToString("n")[..8]);
        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, BoardRoot.RootMarker), "{\"root_id\":\"t\"}");

            string id = NodeId("gavriella");
            // SEPARATE instances, standing in for separate processes — no shared semaphore.
            var writers = Enumerable.Range(0, 8).Select(_ => new SchedulerBoardLog(root, "gavriella")).ToList();

            await Task.WhenAll(writers.Select((w, i) => w.AppendAsync(Op(id, i + 1))));

            var back = await new SchedulerBoardLog(root, "gavriella").ReadAllAsync();
            Assert.Equal(8, back.Count);                    // none lost to a sharing violation
            Assert.Equal(8, back.Select(o => o.OpId).Distinct().Count());
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    // ---- R3-03: the shared state survives concurrent loops ------------------------------------

    /// <summary>
    /// The receive, pull, board-tail and heartbeat loops run at once over the same fold and peer
    /// tables. A Dictionary enumerated during a mutation throws, and a faulted background loop stops
    /// converging SILENTLY — the exact failure class this feature exists to remove.
    /// </summary>
    [Fact]
    public async Task TheFoldSurvivesConcurrentApplyAndEnumeration()
    {
        var fold = NewFold();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

        var writer = Task.Run(() =>
        {
            for (long i = 1; i <= 4000 && !cts.IsCancellationRequested; i++) fold.Apply(Op("g", i));
        });

        var reader = Task.Run(() =>
        {
            while (!writer.IsCompleted && !cts.IsCancellationRequested)
            {
                _ = fold.ToCanonicalJson();
                _ = fold.Operations.Count;
                _ = fold.Frontier;
                _ = fold.WinningTerm();
                _ = fold.Unordered();
            }
        });

        await Task.WhenAll(writer, reader);   // an unsynchronised fold throws here
        Assert.Equal(4000, fold.Count);
    }
}
