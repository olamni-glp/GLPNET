// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT
//
// Fold convergence acceptance (feature 102, T036-T039).
// Covers SC-002 (exactly-once), SC-003 (order-independence), SC-011 (pull backstop), SC-014 (append-then-ship).
//
// The property that makes a federated fold safe is UNION-BY-ID WITH IDEMPOTENT REDELIVERY.
// Redelivery is certain on any link that can drop and retry, so a fold that has not been tested
// against DELIBERATE redelivery is untested, not convergent.

using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using GlpRuntime.CrdtMsg.Crdt;
using GlpRuntime.CrdtMsg.Federation;
using GlpRuntime.CrdtMsg.Route;
using Xunit;

namespace GlpRuntime.CrdtMsg.Tests.Federation;

public sealed class FoldConvergenceTests
{
    private const string LiveEpoch = "ynet-epoch-2026-09";
    private static readonly JsonElement Body = JsonSerializer.SerializeToElement(new { note = "board state" });

    private static FederationOp Op(string peer, long ctr, Term? term = null) =>
        FederationOp.Create(new Dot(peer, ctr), peer, "board_post", Body, term);

    private static FederationFold NewFold() => new(new TermSpaceRegistry(LiveEpoch));

    // ---- SC-002: exactly once, under deliberate redelivery -------------------------------------

    /// <summary>The same op shipped twice folds ONCE. This is the whole safety property.</summary>
    [Fact]
    public void ADeliberatelyRedeliveredOpFoldsExactlyOnce()
    {
        var fold = NewFold();
        var op = Op("gavriella", 1);

        Assert.True(fold.Apply(op));    // first delivery is new
        Assert.False(fold.Apply(op));   // redelivery is a no-op, NOT an error
        Assert.False(fold.Apply(op));   // and again

        Assert.Equal(1, fold.Count);
        Assert.Single(fold.Operations);
    }

    /// <summary>Redelivery through the whole service path, not just the fold, still counts once.</summary>
    [Fact]
    public async Task RedeliveryThroughTheServicePathStillCountsOnce()
    {
        var log = new InMemoryBoardLog();
        var link = new FakeLink("B");
        var svc = new FederationService(EnabledConfig(), link, NewFold(), log);

        var op = Op("gavriella", 1);
        byte[] bytes = Encoding.UTF8.GetBytes(op.ToCanonicalJson());

        link.PushInbound(new LinkInbound("A", bytes, FederationService.BoardBox));
        link.PushInbound(new LinkInbound("A", bytes, FederationService.BoardBox));

        Assert.True(await svc.ReceiveOneAsync());    // new
        Assert.False(await svc.ReceiveOneAsync());   // redelivered

        Assert.Equal(1, svc.Fold.Count);
        Assert.Equal(1, log.Count);                  // and it was durably written exactly once
    }

    // ---- SC-003: order-independence, asserted BYTE-FOR-BYTE ------------------------------------

    /// <summary>
    /// Two hosts holding the same op set produce byte-identical folds regardless of arrival order.
    /// Compared as BYTES on purpose: a bespoke "equivalent" comparer would hide exactly the ordering
    /// bug this assertion exists to catch.
    /// </summary>
    [Fact]
    public void TwoHostsWithTheSameOpSetProduceByteIdenticalFolds()
    {
        var ops = new[]
        {
            Op("gavriella", 1, new Term(LiveEpoch, 1, "gavriella")),
            Op("olamnit",   1, new Term(LiveEpoch, 2, "olamnit")),
            Op("ariellas",  1),
            Op("shiras",    7),
            Op("olamnit",   2),
        };

        var hostA = NewFold();
        hostA.ApplyAll(ops);

        var hostB = NewFold();
        hostB.ApplyAll(ops.Reverse());               // arrived in the opposite order

        Assert.Equal(hostA.ToCanonicalJson(), hostB.ToCanonicalJson());
        Assert.Equal(hostA.Count, hostB.Count);
    }

    /// <summary>Order-independence must survive duplicates mixed into the stream as well.</summary>
    [Fact]
    public void OrderIndependenceHoldsWithDuplicatesInterleaved()
    {
        var ops = new[] { Op("a", 1), Op("b", 1), Op("c", 1) };

        var one = NewFold();
        one.ApplyAll(new[] { ops[0], ops[1], ops[0], ops[2], ops[1] });

        var two = NewFold();
        two.ApplyAll(new[] { ops[2], ops[2], ops[1], ops[0] });

        Assert.Equal(one.ToCanonicalJson(), two.ToCanonicalJson());
        Assert.Equal(3, one.Count);
    }

    /// <summary>The fold never removes or rewrites what it already holds (FR-011).</summary>
    [Fact]
    public void FoldingIsAdditiveOnly()
    {
        var fold = NewFold();
        var first = FederationOp.Create(new Dot("g", 1), "g", "board_post",
            JsonSerializer.SerializeToElement(new { v = "original" }));
        fold.Apply(first);

        // An op with the SAME id but different content must not overwrite the original.
        var impostor = FederationOp.Create(new Dot("g", 1), "g", "board_post",
            JsonSerializer.SerializeToElement(new { v = "rewritten" }));
        Assert.False(fold.Apply(impostor));

        Assert.Contains("original", fold.Operations[0].ToCanonicalJson());
        Assert.DoesNotContain("rewritten", fold.ToCanonicalJson());
    }

    // ---- SC-011: the pull backstop is load-bearing ---------------------------------------------

    /// <summary>
    /// An op appended while the link is down is present after the link is restored — via the
    /// reconciliation pull, NOT the push. Deleting the backstop must make this fail.
    /// </summary>
    [Fact]
    public async Task AnOpAppendedWhileTheLinkIsDownArrivesViaTheReconciliationPull()
    {
        var linkA = new FakeLink("A") { Broken = true };     // the link is DOWN
        var logA = new InMemoryBoardLog();
        var svcA = new FederationService(EnabledConfig(), linkA, NewFold(), logA);

        var op = Op("gavriella", 1);
        await svcA.AppendAndPushAsync(op);                   // push silently fails; append does not

        Assert.Equal(1, logA.Count);                         // A kept it locally regardless
        Assert.Equal(0, linkA.Sent.Count);                   // and nothing crossed

        // Link restored. B pulls: it exchanges its frontier and receives only what it lacks.
        var svcB = new FederationService(EnabledConfig(), new FakeLink("B"), NewFold(), new InMemoryBoardLog());
        var missing = svcA.OpsMissingFrom(svcB.Fold.Frontier);

        Assert.Single(missing);
        Assert.Equal(1, await svcB.ReconcileAsync(missing));
        Assert.True(svcB.Fold.Contains(op.OpId));
    }

    /// <summary>The pull transfers ONLY what the peer lacks — shipping the whole log is a storm, not a backstop.</summary>
    [Fact]
    public async Task TheReconciliationPullTransfersOnlyWhatThePeerLacks()
    {
        var svcA = new FederationService(EnabledConfig(), new FakeLink("A"), NewFold(), new InMemoryBoardLog());
        var svcB = new FederationService(EnabledConfig(), new FakeLink("B"), NewFold(), new InMemoryBoardLog());

        var shared = Op("gavriella", 1);
        var onlyOnA = Op("gavriella", 2);

        await svcA.AppendAndPushAsync(shared);
        await svcA.AppendAndPushAsync(onlyOnA);
        await svcB.AppendAndPushAsync(shared);               // B already has the shared one

        var missing = svcA.OpsMissingFrom(svcB.Fold.Frontier);

        Assert.Single(missing);                               // NOT two
        Assert.Equal(onlyOnA.OpId, missing[0].OpId);
    }

    /// <summary>Reconciling something already held is idempotent.</summary>
    [Fact]
    public async Task ReconcilingAlreadyHeldOpsAddsNothing()
    {
        var svc = new FederationService(EnabledConfig(), new FakeLink("B"), NewFold(), new InMemoryBoardLog());
        var op = Op("gavriella", 1);

        Assert.Equal(1, await svc.ReconcileAsync(new[] { op }));
        Assert.Equal(0, await svc.ReconcileAsync(new[] { op }));
        Assert.Equal(1, svc.Fold.Count);
    }

    // ---- SC-014 / FR-030: append locally, THEN ship --------------------------------------------

    /// <summary>
    /// If the durable append fails, NOTHING is shipped. A federation that ships an op it has not
    /// stored loses that op whenever the link succeeds and the disk does not.
    /// </summary>
    [Fact]
    public async Task NothingIsShippedWhenTheDurableAppendFails()
    {
        var link = new FakeLink("A");
        var log = new InMemoryBoardLog { FailNextAppend = true };
        var svc = new FederationService(EnabledConfig(), link, NewFold(), log);

        await Assert.ThrowsAsync<IOException>(() => svc.AppendAndPushAsync(Op("gavriella", 1)));

        Assert.Equal(0, log.Count);
        Assert.Empty(link.Sent);          // the push never happened — order is append-then-ship
    }

    /// <summary>An op that was appended before a crash is recoverable from the local log afterwards.</summary>
    [Fact]
    public async Task AnAppendedOpSurvivesACrashBeforeThePush()
    {
        string dir = Path.Combine(Path.GetTempPath(), "ynet_fed_t", Guid.NewGuid().ToString("n")[..8]);
        string path = Path.Combine(dir, "gavriella-board-000001.jsonl");
        try
        {
            var log = new JsonlBoardLog(path);
            var link = new FakeLink("A") { Broken = true };            // "crash": the push cannot happen
            var svc = new FederationService(EnabledConfig(), link, NewFold(), log);

            var op = Op("gavriella", 1);
            await svc.AppendAndPushAsync(op);

            // A fresh process reads the log back and finds the op waiting for the backstop.
            var recovered = await new JsonlBoardLog(path).ReadAllAsync();
            Assert.Single(recovered);
            Assert.Equal(op.OpId, recovered[0].OpId);
            Assert.Equal("gavriella", recovered[0].Origin);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    // ---- FR-009: attribution survives the crossing ---------------------------------------------

    /// <summary>Round-tripping through the canonical wire form preserves identity, origin and term.</summary>
    [Fact]
    public void CanonicalRoundTripPreservesIdentityOriginAndTerm()
    {
        var op = Op("gavriella", 42, new Term(LiveEpoch, 3, "gavriella"));
        var back = FederationOp.FromJson(op.ToCanonicalJson());

        Assert.Equal(op.OpId, back.OpId);
        Assert.Equal(op.Origin, back.Origin);
        Assert.Equal(op.Kind, back.Kind);
        Assert.Equal(op.Term, back.Term);
        Assert.Equal(op.ToCanonicalJson(), back.ToCanonicalJson());
    }

    /// <summary>An op with no term round-trips as HAVING no term — absent is not term zero.</summary>
    [Fact]
    public void AnOpWithNoTermRoundTripsWithNoTermNotZero()
    {
        var back = FederationOp.FromJson(Op("gavriella", 1).ToCanonicalJson());
        Assert.Null(back.Term);
    }

    private static FederationConfig EnabledConfig() => new()
    {
        Enabled = true,
        BindAddress = "0.0.0.0",
        BindPort = 47890,
        SpaceId = LiveEpoch,
    };
}

/// <summary>An in-process link, so the fold and the two convergence legs are testable without QUIC.</summary>
internal sealed class FakeLink : IFederationLink
{
    private readonly Channel<LinkInbound> _in = Channel.CreateUnbounded<LinkInbound>();

    public FakeLink(string localPeer) => LocalPeer = localPeer;

    public string LocalPeer { get; }
    public IPEndPoint? ListenEndPoint { get; private set; }

    /// <summary>When true, every send throws — a dropped link.</summary>
    public bool Broken { get; set; }

    /// <summary>When true, connecting raises the pin-mismatch condition.</summary>
    public bool PinMismatch { get; set; }

    public List<(string Peer, string Box, byte[] Bytes)> Sent { get; } = new();

    public Task ListenAsync(IPEndPoint bind, CancellationToken ct = default)
    {
        ListenEndPoint = bind;
        return Task.CompletedTask;
    }

    public Task ConnectPeerAsync(string peerName, IPEndPoint remote, CancellationToken ct = default)
    {
        if (PinMismatch) throw new System.Security.Authentication.AuthenticationException("pin mismatch");
        if (Broken) throw new IOException("unreachable");
        return Task.CompletedTask;
    }

    public ValueTask SendAsync(string toPeer, string box, ReadOnlyMemory<byte> bytes, CancellationToken ct = default)
    {
        if (Broken) throw new IOException("link is down");
        Sent.Add((toPeer, box, bytes.ToArray()));
        return ValueTask.CompletedTask;
    }

    public ChannelReader<LinkInbound> Inbound => _in.Reader;

    public void PushInbound(LinkInbound m) => _in.Writer.TryWrite(m);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
