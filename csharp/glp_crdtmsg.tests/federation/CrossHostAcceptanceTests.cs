// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT
//
// SC-001 — the cross-host criterion (feature 102, T040/T047).
//
// FR-022 disqualifies a two-processes-on-one-machine proof as cross-host evidence. The mechanism
// proof in YnetFederationTests is REAL and it is NOT this. SC-001 therefore CANNOT be satisfied on
// one host, and the honest behaviour without a peer is to report the criterion UNMEASURED.
//
// WHY AN EVIDENCE FILE AND NOT A SKIP. xunit 2.9.3 has no dynamic skip, and a skip is invisible in
// a summary line anyway — "72 passed" and "72 passed, 1 skipped" read the same to a tired reader at
// the end of an era. So the criterion's state is written to a machine-readable evidence record that
// the close gate reads. An UNMEASURED record is a REFUSAL to claim SC-001, not a quiet omission.
//
// TO MEASURE IT: set YNET_FED_PEER_ENDPOINT=<ip:port> and YNET_FED_PEER_NODEID=<64-hex>, with a peer
// serving on a PHYSICALLY SEPARATE host. See docs/runbooks/ynet-federation.md.

using System.Net;
using System.Text.Json;
using GlpRuntime.CrdtMsg.Crdt;
using GlpRuntime.CrdtMsg.Federation;
using GlpRuntime.CrdtMsg.Route;
using Xunit;
using Xunit.Abstractions;

namespace GlpRuntime.CrdtMsg.Tests.Federation;

public sealed class CrossHostAcceptanceTests
{
    private const string PeerEndpointVar = "YNET_FED_PEER_ENDPOINT";
    private const string PeerNodeIdVar = "YNET_FED_PEER_NODEID";
    private const string LiveEpoch = "ynet-epoch-7f3a91c2e04b5d68";   // no wall clock: FR-026 applies to fixtures too

    private readonly ITestOutputHelper _out;
    public CrossHostAcceptanceTests(ITestOutputHelper o) => _out = o;

    /// <summary>
    /// Measures SC-001 when a peer is configured, and records the outcome either way. The test
    /// passes when the criterion is MEASURED-AND-MET, and also when it is honestly recorded as
    /// UNMEASURED — because "this lane cannot schedule another host's listener" is not a defect in
    /// this lane's code. What it must NEVER do is record MET without having measured it.
    /// </summary>
    [Fact]
    public async Task Sc001IsMeasuredAgainstARealPeerOrRecordedUnmeasured()
    {
        string? endpoint = Environment.GetEnvironmentVariable(PeerEndpointVar);
        string? peerNodeId = Environment.GetEnvironmentVariable(PeerNodeIdVar);

        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(peerNodeId))
        {
            Record(Sc001Evidence.Unmeasured(
                $"no peer listener configured ({PeerEndpointVar}/{PeerNodeIdVar} unset). " +
                "FR-022 disqualifies the one-machine mechanism proof as evidence for this criterion, " +
                "so this run does NOT satisfy SC-001. Ruling Q-GLPNETG28-02: implement here, " +
                "broadcast an ACK-required peer ask, measure when a peer answers."));
            return;
        }

        if (!QuicLinkTransport.IsSupported)
        {
            Record(Sc001Evidence.Unmeasured("the QUIC stack is not supported in this process"));
            return;
        }

        var remote = IPEndPoint.Parse(endpoint);

        // Refuse to measure against a peer on THIS machine — that would manufacture a green SC-001
        // from precisely the evidence FR-022 excludes. (I: is an SMB loopback of this host's own
        // D:\, so a "peer" reached that way is this host wearing a different name.)
        if (FederationStatusProbe.IsSameMachine(IPAddress.Any, remote.Address))
        {
            Record(Sc001Evidence.Unmeasured(
                $"{endpoint} resolves to THIS machine — FR-022: a same-machine crossing is not " +
                "cross-host federation"));
            return;
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var ct = cts.Token;
        string dir = Path.Combine(Path.GetTempPath(), "ynet_sc001", Guid.NewGuid().ToString("n")[..8]);

        try
        {
            var identity = new NodeIdentityStore(Path.Combine(dir, "node.key"));
            var cert = identity.LoadOrMint("gavriella");
            string localNodeId = NodeIdentityStore.DeriveNodeId(cert);

            await using var transport = new QuicLinkTransport(localNodeId, cert,
                new Dictionary<string, string> { [peerNodeId] = NodeIdentityStore.PinFromNodeId(peerNodeId) });

            var cfg = new FederationConfig
            {
                Enabled = true,
                BoardRootPath = "D:/coop/buildkit/sched",
                BoardActor = "gavriella",
                BindAddress = "0.0.0.0",
                BindPort = FederationConfig.DefaultPort,
                SpaceId = LiveEpoch,
                Peers = { new PeerConfig { Name = "peer", NodeId = peerNodeId, Endpoints = { endpoint }, Pin = NodeIdentityStore.PinFromNodeId(peerNodeId) } },
            };

            await using var svc = new FederationService(cfg, new QuicFederationLink(transport),
                new FederationFold(new TermSpaceRegistry(LiveEpoch)),
                new JsonlBoardLog(Path.Combine(dir, "gavriella-board-000001.jsonl")));

            Assert.True(await svc.BindAsync(ct));
            Assert.Equal(AdmissionOutcome.Admitted, await svc.DialAsync(peerNodeId, ct));

            var claim = FederationOp.Create(
                new Dot(localNodeId, 1), localNodeId, "claim",
                JsonSerializer.SerializeToElement(new { wp = "sc-001-probe", lane = "gavriella-GLPNET" }));

            // MEASURE THE REMOTE FOLD, NOT THE LOCAL WRITE.
            //
            // Round 2 found that this timed an append plus a socket write. PushAsync swallows a send
            // failure by design (the pull is its repair path), so that figure was achievable with
            // the peer switched off, and nothing read the peer's fold. SC-001 asserts a claim is
            // VISIBLE ON A SECOND HOST; only the peer can attest to that, so we wait for its ack.
            var started = DateTimeOffset.UtcNow;
            await svc.AppendAndPushAsync(claim, ct);

            // The receive loop has to run for an inbound ack to be read at all.
            var pump = Task.Run(async () =>
            {
                try { while (!ct.IsCancellationRequested) await svc.ReceiveOneAsync(ct); }
                catch { /* cancelled, or a refusal the acceptance run does not adjudicate */ }
            }, ct);

            bool acked = await svc.WaitForPeerAckAsync(claim.OpId, TimeSpan.FromSeconds(10), ct);
            double seconds = (DateTimeOffset.UtcNow - started).TotalSeconds;

            if (!acked)
            {
                // NOT a failure and emphatically NOT a pass: no peer attested to folding the claim,
                // so remote visibility is UNMEASURED. Recording a green here on a local timing is
                // the precise false-green this era exists to remove.
                Record(Sc001Evidence.Unmeasured(
                    $"pushed {claim.OpId} to {endpoint} but no peer acknowledged folding it within 10s — "
                    + "remote visibility is UNPROVEN; a local append returning is not evidence"));
                return;
            }

            var status = svc.Status();
            Assert.Equal(Tri.Yes, status.ListenerBound);
            Assert.Equal(Tri.Yes, status.PeerAdmitted);

            // THE assertion that separates SC-001 from the mechanism proof. It must be MEASURED as
            // No — an Unknown here (a crossing whose peer address was not captured) is not evidence
            // of cross-host federation either, and must not satisfy SC-001.
            Assert.Equal(Tri.No, status.SameMachine);

            // NOT asserted here: `op received from peer`. An ACK is the peer attesting that it
            // folded OUR operation; it is not an operation received FROM the peer, and _opCrossed
            // means the latter. Asserting Yes here was UNSATISFIABLE and would have made SC-001
            // permanently unmeasurable — a guard so strict it can never pass is not a guard, it is
            // an outage. What SC-001 needs is the ack, which is asserted above.
            Assert.True(svc.WasAckedByPeer(claim.OpId));

            // And the clarified window from ruling Q-GLPNETG28-03 — now measured to the REMOTE fold.
            Assert.True(seconds <= 5.0,
                $"claim took {seconds:F2}s to become visible on the peer, over the 5s bound");

            Record(Sc001Evidence.Measured(endpoint, claim.OpId.ToString(), seconds));
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    /// <summary>
    /// Guards the guard. The same-machine detector must actually DETECT, or the refusal above could
    /// not tell a real peer from a loopback and SC-001 could be satisfied by the excluded evidence.
    /// </summary>
    [Fact]
    public void TheSameMachineDetectorActuallyDistinguishesLocalFromRemote()
    {
        Assert.True(FederationStatusProbe.IsSameMachine(IPAddress.Any, IPAddress.Loopback));
        Assert.False(FederationStatusProbe.IsSameMachine(IPAddress.Any, IPAddress.Parse("203.0.113.7")));
    }

    private void Record(Sc001Evidence e)
    {
        _out.WriteLine(e.Render());
        e.WriteTo(Sc001Evidence.DefaultPath());
    }
}

/// <summary>
/// The durable record of SC-001's state on a given run. Read by the era-close gate: an UNMEASURED
/// record means the criterion may NOT be reported as met, whatever the suite's green count says.
/// </summary>
public sealed record Sc001Evidence
{
    public required string State { get; init; }          // "MEASURED" | "UNMEASURED"
    public required string Detail { get; init; }
    public string? PeerEndpoint { get; init; }
    public string? OpId { get; init; }
    public double? Seconds { get; init; }
    public string Utc { get; init; } = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
    public string Host { get; init; } = Environment.MachineName;

    public static Sc001Evidence Unmeasured(string why) =>
        new() { State = "UNMEASURED", Detail = why };

    public static Sc001Evidence Measured(string endpoint, string opId, double seconds) =>
        new()
        {
            State = "MEASURED",
            Detail = "a claim appended here was pushed to a physically separate host",
            PeerEndpoint = endpoint,
            OpId = opId,
            Seconds = seconds,
        };

    /// <summary>True only when the criterion was actually measured AND met.</summary>
    public bool IsMet => State == "MEASURED";

    public static string DefaultPath() =>
        Path.Combine(Path.GetTempPath(), "ynet_federation", "sc001.evidence.json");

    public string Render() =>
        $"SC-001 {State}: {Detail}" +
        (PeerEndpoint is null ? "" : $" [peer={PeerEndpoint} op={OpId} {Seconds:F2}s]");

    public void WriteTo(string path)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // The console record above already carries the fact; failing to persist it must not
            // fail the run, but it also never turns UNMEASURED into MEASURED.
        }
    }
}
