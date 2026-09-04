// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT
//
// Admission acceptance (feature 102, T028-T030) plus config validation (contract G3).
// Covers SC-004 (unpinned dialer refused, NO data crossed), SC-006 (multi-address = one participant),
// SC-009 (recorded reversals), FR-008 (pin mismatch is its own condition).
//
// A reachable listener is not an open one. The default peer set admits NOBODY, which is the state
// the system fails INTO — so this property holds before any of it is configured.

using System.Net;
using System.Text.Json;
using GlpRuntime.CrdtMsg.Federation;
using Xunit;

namespace GlpRuntime.CrdtMsg.Tests.Federation;

public sealed class AdmissionTests
{
    private const string LiveEpoch = "ynet-epoch-7f3a91c2e04b5d68";   // no wall clock: FR-026 applies to fixtures too

    private static PeerEntry Entry(string name, string nodeId, params string[] eps) =>
        new(name, nodeId, eps.Select(IPEndPoint.Parse).ToList(), "pin-" + nodeId);

    // ---- SC-004: the negative control, and it asserts NO DATA CROSSED --------------------------

    /// <summary>
    /// An empty peer set admits nobody. Asserting only "connection refused" would not test FR-006 —
    /// the load-bearing half is that zero bytes of board data were transferred.
    /// </summary>
    [Fact]
    public async Task AnUnpinnedDialerIsRefusedAndNoBoardDataCrosses()
    {
        var link = new FakeLink("A");
        var cfg = new FederationConfig { Enabled = true, BoardRootPath = "D:/coop/buildkit/sched", BoardActor = "gavriella", BindAddress = "0.0.0.0", BindPort = 47890, SpaceId = LiveEpoch };
        var svc = new FederationService(cfg, link, new FederationFold(new TermSpaceRegistry(LiveEpoch)), new InMemoryBoardLog());
        await svc.BindAsync();

        Assert.True(svc.Peers.AdmitsNobody);
        Assert.Equal(AdmissionOutcome.NotInPeerSet, await svc.DialAsync("stranger"));

        Assert.Empty(link.Sent);                       // ZERO bytes of board data crossed
        Assert.Equal(Tri.No, svc.Status().PeerAdmitted);
    }

    /// <summary>An identity absent from the peer set is refused at the admission decision itself.</summary>
    [Fact]
    public void AnIdentityNotInThePeerSetIsRefused()
    {
        var set = new PeerSet(new[] { Entry("olamnit", "n-olamnit", "192.168.0.136:47890") });
        Assert.Equal(AdmissionOutcome.NotInPeerSet, set.Admit("n-stranger", "any-pin"));
    }

    /// <summary>The default set admits nobody — the safe state, present before configuration.</summary>
    [Fact]
    public void TheDefaultPeerSetAdmitsNobody()
    {
        var set = new PeerSet();
        Assert.True(set.AdmitsNobody);
        Assert.Equal(0, set.ParticipantCount);
        Assert.Equal(AdmissionOutcome.NotInPeerSet, set.Admit("anyone", "any"));
        Assert.Contains("peer set is empty", set.WhyNotAdmitted());
    }

    // ---- FR-008: a pin mismatch is a DISTINCT condition ----------------------------------------

    /// <summary>
    /// Pin mismatch and unreachable demand OPPOSITE operator responses — investigate an attack, vs.
    /// wait for a host. Collapsing them into one error costs the operator the distinction.
    /// </summary>
    [Fact]
    public void APinMismatchIsDistinctFromUnreachableAndFromNotInPeerSet()
    {
        var set = new PeerSet(new[] { Entry("olamnit", "n-olamnit", "192.168.0.136:47890") });

        var mismatch = set.Admit("n-olamnit", "WRONG-PIN");
        var absent = set.Admit("n-nobody", "pin-n-nobody");
        var ok = set.Admit("n-olamnit", "pin-n-olamnit");

        Assert.Equal(AdmissionOutcome.PinMismatch, mismatch);
        Assert.Equal(AdmissionOutcome.NotInPeerSet, absent);
        Assert.Equal(AdmissionOutcome.Admitted, ok);
        Assert.Equal(3, new[] { mismatch, absent, ok }.Distinct().Count());
    }

    /// <summary>Through the service path, a mismatching pin surfaces as PinMismatch, not Unreachable.</summary>
    [Fact]
    public async Task ThroughTheServiceAPinMismatchIsNotReportedAsUnreachable()
    {
        var cfg = new FederationConfig
        {
            Enabled = true, BoardRootPath = "D:/coop/buildkit/sched", BoardActor = "gavriella", BindAddress = "0.0.0.0", BindPort = 47890, SpaceId = LiveEpoch,
            Peers = { new PeerConfig { Name = "olamnit", NodeId = "1111111111111111111111111111111111111111111111111111111111111111", Endpoints = { "192.168.0.136:47890" } } },
        };
        var svc = new FederationService(cfg, new FakeLink("A") { PinMismatch = true },
            new FederationFold(new TermSpaceRegistry(LiveEpoch)), new InMemoryBoardLog());

        var outcome = await svc.DialAsync("olamnit");
        Assert.Equal(AdmissionOutcome.PinMismatch, outcome);
        Assert.NotEqual(AdmissionOutcome.Unreachable, outcome);
    }

    /// <summary>An unreachable peer is Unreachable — not a security condition.</summary>
    [Fact]
    public async Task AnUnreachablePeerIsReportedAsUnreachable()
    {
        var cfg = new FederationConfig
        {
            Enabled = true, BoardRootPath = "D:/coop/buildkit/sched", BoardActor = "gavriella", BindAddress = "0.0.0.0", BindPort = 47890, SpaceId = LiveEpoch,
            Peers = { new PeerConfig { Name = "olamnit", NodeId = "1111111111111111111111111111111111111111111111111111111111111111", Endpoints = { "192.168.0.136:47890" } } },
        };
        var svc = new FederationService(cfg, new FakeLink("A") { Broken = true },
            new FederationFold(new TermSpaceRegistry(LiveEpoch)), new InMemoryBoardLog());

        Assert.Equal(AdmissionOutcome.Unreachable, await svc.DialAsync("olamnit"));
    }

    /// <summary>A peer with no usable endpoint is a NAME-RESOLUTION failure, never a transport one.</summary>
    [Fact]
    public async Task APeerWithNoResolvedEndpointIsNameResolutionNotTransportFailure()
    {
        var cfg = new FederationConfig
        {
            Enabled = true, BoardRootPath = "D:/coop/buildkit/sched", BoardActor = "gavriella", BindAddress = "0.0.0.0", BindPort = 47890, SpaceId = LiveEpoch,
            Peers = { new PeerConfig { Name = "olamnit", NodeId = "1111111111111111111111111111111111111111111111111111111111111111", Endpoints = { } } },
        };
        var svc = new FederationService(cfg, new FakeLink("A"),
            new FederationFold(new TermSpaceRegistry(LiveEpoch)), new InMemoryBoardLog());

        var outcome = await svc.DialAsync("olamnit");
        Assert.Equal(AdmissionOutcome.NameResolutionFailed, outcome);
        Assert.NotEqual(AdmissionOutcome.Unreachable, outcome);
    }

    // ---- SC-006: a multi-address participant counts ONCE ---------------------------------------

    /// <summary>
    /// Olamnit answers on 192.168.0.136 AND 192.168.0.129. It is ONE participant. Adding an address
    /// does not add a participant — a count keyed on address would read 2 and over-count the fleet.
    /// </summary>
    [Fact]
    public void AHostAnsweringOnTwoAddressesCountsAsOneParticipant()
    {
        var set = new PeerSet(new[]
        {
            Entry("olamnit", "n-olamnit", "192.168.0.136:47890", "192.168.0.129:47890"),
        });

        Assert.Equal(1, set.ParticipantCount);
        Assert.Equal(2, set.Find("n-olamnit")!.Endpoints.Count);
    }

    /// <summary>Re-adding the same node id replaces rather than duplicates — one participant, one entry.</summary>
    [Fact]
    public void ReAddingTheSameNodeIdDoesNotAddAParticipant()
    {
        var set = new PeerSet();
        set.Add(Entry("olamnit", "n-olamnit", "192.168.0.136:47890"));
        set.Add(Entry("olamnit", "n-olamnit", "192.168.0.129:47890"));

        Assert.Equal(1, set.ParticipantCount);
    }

    /// <summary>
    /// Two DIFFERENT node ids reachable at the same address are two participants. Addresses are not
    /// identity — the converse of the rule above, and it must hold in both directions.
    /// </summary>
    [Fact]
    public void TwoNodeIdsAtOneAddressAreTwoParticipants()
    {
        var set = new PeerSet();
        set.Add(Entry("a", "n-a", "192.168.0.108:47890"));
        set.Add(Entry("b", "n-b", "192.168.0.108:47890"));

        Assert.Equal(2, set.ParticipantCount);
    }

    /// <summary>A peer entry without a node id is refused — identity is not derived from address.</summary>
    [Fact]
    public void APeerEntryWithoutANodeIdIsRefused()
    {
        var set = new PeerSet();
        Assert.Throws<ArgumentException>(() =>
            set.Add(new PeerEntry("nameless", "", Array.Empty<IPEndPoint>(), "pin")));
    }

    // ---- contract G3: configuration refuses loudly and names the field -------------------------

    /// <summary>A loopback bind while enabled is REFUSED — it is the failure that looks like success.</summary>
    [Fact]
    public void ALoopbackBindWhileEnabledIsRefused()
    {
        var cfg = new FederationConfig { Enabled = true, BoardRootPath = "D:/coop/buildkit/sched", BoardActor = "gavriella", BindAddress = "127.0.0.1", SpaceId = LiveEpoch };
        var problems = cfg.Validate();
        Assert.Contains(problems, p => p.StartsWith("bind_address:"));
        Assert.Contains(problems, p => p.Contains("not peer-reachable"));
    }

    /// <summary>An empty space id while enabled is refused — an unminted space orders nothing.</summary>
    [Fact]
    public void AnEmptySpaceIdWhileEnabledIsRefused()
    {
        var cfg = new FederationConfig { Enabled = true, BoardRootPath = "D:/coop/buildkit/sched", BoardActor = "gavriella", BindAddress = "0.0.0.0", SpaceId = "" };
        Assert.Contains(cfg.Validate(), p => p.StartsWith("space_id:"));
    }

    /// <summary>A clock-derived space id is refused — that is precisely how the fossil was born.</summary>
    [Fact]
    public void AClockDerivedSpaceIdIsRefused()
    {
        var cfg = new FederationConfig { Enabled = true, BoardRootPath = "D:/coop/buildkit/sched", BoardActor = "gavriella", BindAddress = "0.0.0.0", SpaceId = "5961694" };
        Assert.Contains(cfg.Validate(), p => p.Contains("clock-derived"));
    }

    /// <summary>A duplicate node id across peers is refused — one participant, one entry.</summary>
    [Fact]
    public void ADuplicateNodeIdAcrossPeersIsRefused()
    {
        var cfg = new FederationConfig
        {
            Enabled = true, BoardRootPath = "D:/coop/buildkit/sched", BoardActor = "gavriella", BindAddress = "0.0.0.0", SpaceId = LiveEpoch,
            Peers =
            {
                new PeerConfig { Name = "a", NodeId = "5a5a5a5a5a5a5a5a5a5a5a5a5a5a5a5a5a5a5a5a5a5a5a5a5a5a5a5a5a5a5a5a", Endpoints = { "192.168.0.1:47890" } },
                new PeerConfig { Name = "b", NodeId = "5a5a5a5a5a5a5a5a5a5a5a5a5a5a5a5a5a5a5a5a5a5a5a5a5a5a5a5a5a5a5a5a", Endpoints = { "192.168.0.2:47890" } },
            },
        };
        Assert.Contains(cfg.Validate(), p => p.Contains("duplicate"));
    }

    /// <summary>A hostname endpoint is refused with the estate-specific reason (names ⇒ link-local only).</summary>
    [Fact]
    public void AHostnameEndpointIsRefusedWithTheLinkLocalReason()
    {
        var cfg = new FederationConfig
        {
            Enabled = true, BoardRootPath = "D:/coop/buildkit/sched", BoardActor = "gavriella", BindAddress = "0.0.0.0", SpaceId = LiveEpoch,
            Peers = { new PeerConfig { Name = "o", NodeId = "2222222222222222222222222222222222222222222222222222222222222222", Endpoints = { "olamnit:47890" } } },
        };
        Assert.Contains(cfg.Validate(), p => p.Contains("literal address"));
    }

    /// <summary>The safe default validates clean, and disables nothing local (FR-004).</summary>
    [Fact]
    public void TheDefaultConfigIsValidAndDisabled()
    {
        var cfg = new FederationConfig();
        Assert.False(cfg.Enabled);
        Assert.Empty(cfg.Peers);
        Assert.True(cfg.IsValid);
        Assert.Equal(FederationConfig.DefaultPort, cfg.BindPort);
    }

    /// <summary>Config round-trips through disk and reads back EFFECTIVE values (FR-002).</summary>
    [Fact]
    public void ConfigRoundTripsAndReadsBack()
    {
        string dir = Path.Combine(Path.GetTempPath(), "ynet_cfg", Guid.NewGuid().ToString("n")[..8]);
        string path = Path.Combine(dir, "config.json");
        try
        {
            var cfg = new FederationConfig
            {
                Enabled = true, BoardRootPath = "D:/coop/buildkit/sched", BoardActor = "gavriella", BindAddress = "0.0.0.0", BindPort = 47890, SpaceId = LiveEpoch,
                Peers = { new PeerConfig { Name = "olamnit", NodeId = "1111111111111111111111111111111111111111111111111111111111111111", Endpoints = { "192.168.0.136:47890" } } },
            };
            cfg.Save(path);

            var back = FederationConfig.Load(path);
            Assert.Equal(cfg.SpaceId, back.SpaceId);
            Assert.Equal(cfg.BindPort, back.BindPort);
            Assert.Single(back.Peers);
            Assert.Contains("olamnit", back.RenderEffective());
            Assert.Contains("validation            : OK", back.RenderEffective());
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    /// <summary>An absent config is not an error — it is an unconfigured host, serving lanes normally.</summary>
    [Fact]
    public void AnAbsentConfigLoadsAsTheSafeDefault()
    {
        var cfg = FederationConfig.Load(Path.Combine(Path.GetTempPath(), "definitely", "absent.json"));
        Assert.False(cfg.Enabled);
        Assert.True(cfg.IsValid);
    }

    // ---- SC-009: every enabling change carries its recorded reversal ---------------------------

    /// <summary>
    /// The reversal is DATA. A runbook that says "and to undo it, remove the rule" is a reversal
    /// nobody can execute six weeks later on a host they did not configure.
    /// </summary>
    [Fact]
    public void EveryRecordedChangeCarriesItsReversalAndReplaysNewestFirst()
    {
        string dir = Path.Combine(Path.GetTempPath(), "ynet_led", Guid.NewGuid().ToString("n")[..8]);
        try
        {
            var led = new ChangeLedger(Path.Combine(dir, "changes.jsonl"));
            led.Record("wrote config.json (enabled=true)", "restore the recorded prior file", "enable federation", prior: "{\"enabled\":false}");
            led.Record("minted node.key", "delete the key file", "stable node id");
            led.Record("added firewall rule ynet-federation-quic-udp-47890",
                       "Remove-NetFirewallRule -DisplayName 'ynet-federation-quic-udp-47890'",
                       "inbound peer dial, ruling Q-GLPNETG27-04");

            var all = led.All();
            Assert.Equal(3, all.Count);
            Assert.All(all, c => Assert.False(string.IsNullOrWhiteSpace(c.Reversal)));
            Assert.All(all, c => Assert.False(string.IsNullOrWhiteSpace(c.Rationale)));

            // Reverse order: undo the firewall rule before the config that motivated it.
            var plan = led.ReversalPlan();
            Assert.Contains("Remove-NetFirewallRule", plan[0].Reversal);
            Assert.Equal("{\"enabled\":false}", plan[2].Prior);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    /// <summary>An empty ledger yields an empty plan rather than throwing.</summary>
    [Fact]
    public void AnEmptyLedgerYieldsAnEmptyReversalPlan()
    {
        var led = new ChangeLedger(Path.Combine(Path.GetTempPath(), "nope", "changes.jsonl"));
        Assert.Empty(led.All());
        Assert.Empty(led.ReversalPlan());
    }

    // ---- FR-004: federation is never on the local critical path --------------------------------

    /// <summary>With federation disabled, appending still works and nothing is shipped.</summary>
    [Fact]
    public async Task WithFederationDisabledTheLocalBoardStillWorks()
    {
        var link = new FakeLink("A");
        var log = new InMemoryBoardLog();
        var svc = new FederationService(new FederationConfig { Enabled = false }, link,
            new FederationFold(new TermSpaceRegistry(LiveEpoch)), log);

        await svc.AppendAndPushAsync(FederationOp.Create(
            new GlpRuntime.CrdtMsg.Crdt.Dot("g", 1), "g", "board_post", JsonSerializer.SerializeToElement(new { })));

        Assert.Equal(1, log.Count);                                  // local append happened
        Assert.Empty(link.Sent);                                     // nothing shipped
        Assert.Equal(FederationHealth.Disabled, svc.Health);
    }

    /// <summary>Enabled but with no peer reachable degrades EXPLICITLY — and never reports success.</summary>
    [Fact]
    public async Task AnEnabledServiceWithNoReachablePeerDegradesExplicitly()
    {
        var cfg = new FederationConfig
        {
            Enabled = true, BoardRootPath = "D:/coop/buildkit/sched", BoardActor = "gavriella", BindAddress = "0.0.0.0", BindPort = 47890, SpaceId = LiveEpoch,
            Peers = { new PeerConfig { Name = "olamnit", NodeId = "1111111111111111111111111111111111111111111111111111111111111111", Endpoints = { "192.168.0.136:47890" } } },
        };
        var log = new InMemoryBoardLog();
        var svc = new FederationService(cfg, new FakeLink("A") { Broken = true },
            new FederationFold(new TermSpaceRegistry(LiveEpoch)), log);

        Assert.Equal(AdmissionOutcome.Unreachable, await svc.DialAsync("olamnit"));
        Assert.Equal(FederationHealth.DegradedLocalOnly, svc.Health);

        await svc.AppendAndPushAsync(FederationOp.Create(
            new GlpRuntime.CrdtMsg.Crdt.Dot("g", 1), "g", "board_post", JsonSerializer.SerializeToElement(new { })));
        Assert.Equal(1, log.Count);   // local lanes served UNCHANGED
    }
}
