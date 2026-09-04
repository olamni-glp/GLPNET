// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT
//
// Status surface acceptance (feature 102, T024-T026).
// Covers SC-007 (positive AND negative control for each of the four states) and SC-010 (unknown != no).
//
// THE BAR, restated because it is unusual: for each state a positive control and a negative control
// must produce DIFFERENT reported results. Identical output in both directions is a FAILING test
// even when both individually "pass" — that is the 1-of-206 shape that let a green aggregate through
// on this estate before, and the reason six false greens landed in one week.

using System.Net;
using System.Text;
using System.Text.Json;
using GlpRuntime.CrdtMsg.Crdt;
using GlpRuntime.CrdtMsg.Federation;
using GlpRuntime.CrdtMsg.Route;
using Xunit;

namespace GlpRuntime.CrdtMsg.Tests.Federation;

public sealed class StatusSurfaceTests
{
    private const string LiveEpoch = "ynet-epoch-2026-09";
    private static readonly JsonElement Body = JsonSerializer.SerializeToElement(new { });

    private static FederationConfig Config(bool enabled = true, params PeerConfig[] peers) => new()
    {
        Enabled = enabled,
        BindAddress = "0.0.0.0",
        BindPort = 47890,
        SpaceId = LiveEpoch,
        Peers = peers.ToList(),
    };

    private static PeerConfig Peer(string name, string nodeId, params string[] endpoints) => new()
    {
        Name = name, NodeId = nodeId, Endpoints = endpoints.ToList(), Pin = "pin-" + nodeId,
    };

    private static FederationService Service(FederationConfig cfg, FakeLink link) =>
        new(cfg, link, new FederationFold(new TermSpaceRegistry(LiveEpoch)), new InMemoryBoardLog());

    // ---- SC-007 state 1: stack supported -------------------------------------------------------

    /// <summary>
    /// The gate reports the BCL's own answer rather than a cached or assumed one. Without this,
    /// "QUIC is unavailable" and "all tests pass" would be indistinguishable.
    /// </summary>
    [Fact]
    public void StackSupportedIsMeasuredNotAssumed()
    {
        var measured = FederationStatusProbe.MeasureStackSupported();
        var expected = System.Net.Quic.QuicListener.IsSupported && System.Net.Quic.QuicConnection.IsSupported
            ? Tri.Yes : Tri.No;
        Assert.Equal(expected, measured);
        Assert.NotEqual(Tri.Unknown, measured);   // it WAS measurable here, so it must not say unknown
    }

    // ---- SC-007 state 2: listener bound --------------------------------------------------------

    /// <summary>POSITIVE control: bound ⇒ yes, with the endpoint.</summary>
    [Fact]
    public async Task ListenerBoundPositiveControl()
    {
        var link = new FakeLink("A");
        var svc = Service(Config(), link);
        await svc.BindAsync();

        var s = svc.Status();
        Assert.Equal(Tri.Yes, s.ListenerBound);
        Assert.NotNull(s.BoundEndpoint);
    }

    /// <summary>NEGATIVE control: never bound ⇒ no. Must DIFFER from the positive control.</summary>
    [Fact]
    public void ListenerBoundNegativeControl()
    {
        var svc = Service(Config(), new FakeLink("A"));
        var s = svc.Status();                     // BindAsync deliberately not called

        Assert.Equal(Tri.No, s.ListenerBound);
        Assert.Null(s.BoundEndpoint);
    }

    /// <summary>The two controls produce DIFFERENT output — the actual SC-007 assertion.</summary>
    [Fact]
    public async Task ListenerBoundControlsDiffer()
    {
        var boundSvc = Service(Config(), new FakeLink("A"));
        await boundSvc.BindAsync();
        var unboundSvc = Service(Config(), new FakeLink("A"));

        Assert.NotEqual(boundSvc.Status().ListenerBound, unboundSvc.Status().ListenerBound);
        Assert.NotEqual(boundSvc.Status().Render(), unboundSvc.Status().Render());
    }

    // ---- SC-007 state 3: peer admitted ---------------------------------------------------------

    /// <summary>POSITIVE control: a pinned peer dialled successfully ⇒ yes, one participant.</summary>
    [Fact]
    public async Task PeerAdmittedPositiveControl()
    {
        var cfg = Config(true, Peer("olamnit", "nodeid-olamnit", "192.168.0.136:47890"));
        var svc = Service(cfg, new FakeLink("A"));
        await svc.BindAsync();

        Assert.Equal(AdmissionOutcome.Admitted, await svc.DialAsync("olamnit"));

        var s = svc.Status();
        Assert.Equal(Tri.Yes, s.PeerAdmitted);
        Assert.Equal(1, s.AdmittedParticipants);
    }

    /// <summary>
    /// NEGATIVE control: an EMPTY pin set ⇒ no, and the reason NAMES the missing pins rather than
    /// leaving the operator to guess (FR-019).
    /// </summary>
    [Fact]
    public async Task PeerAdmittedNegativeControlNamesTheMissingPins()
    {
        var svc = Service(Config(), new FakeLink("A"));    // no peers configured
        await svc.BindAsync();

        var s = svc.Status();
        Assert.Equal(Tri.No, s.PeerAdmitted);
        Assert.Equal(0, s.AdmittedParticipants);
        Assert.Contains("peer set is empty", s.Reasons["peer admitted"]);
        Assert.Contains("peer set is empty", s.Render());
    }

    /// <summary>The two controls differ.</summary>
    [Fact]
    public async Task PeerAdmittedControlsDiffer()
    {
        var withPeer = Service(Config(true, Peer("olamnit", "n1", "192.168.0.136:47890")), new FakeLink("A"));
        await withPeer.BindAsync();
        await withPeer.DialAsync("olamnit");

        var without = Service(Config(), new FakeLink("A"));
        await without.BindAsync();

        Assert.NotEqual(withPeer.Status().PeerAdmitted, without.Status().PeerAdmitted);
    }

    // ---- SC-007 state 4: op received from peer -------------------------------------------------

    /// <summary>POSITIVE control: an op actually crossed ⇒ yes.</summary>
    [Fact]
    public async Task OpReceivedPositiveControl()
    {
        var link = new FakeLink("B");
        var svc = Service(Config(), link);
        await svc.BindAsync();

        var op = FederationOp.Create(new Dot("gavriella", 1), "gavriella", "board_post", Body);
        link.PushInbound(new LinkInbound("A", Encoding.UTF8.GetBytes(op.ToCanonicalJson()), FederationService.BoardBox));
        await svc.ReceiveOneAsync();

        Assert.Equal(Tri.Yes, svc.Status().OpReceivedFromPeer);
    }

    /// <summary>
    /// NEGATIVE control: a peer is ADMITTED but sent nothing ⇒ no. This is the specific inference
    /// FR-020 forbids — a link being up is not an op having crossed.
    /// </summary>
    [Fact]
    public async Task OpReceivedNegativeControlAdmittedButNothingSent()
    {
        var cfg = Config(true, Peer("olamnit", "n1", "192.168.0.136:47890"));
        var svc = Service(cfg, new FakeLink("A"));
        await svc.BindAsync();
        await svc.DialAsync("olamnit");

        var s = svc.Status();
        Assert.Equal(Tri.Yes, s.PeerAdmitted);          // the link IS up
        Assert.Equal(Tri.No, s.OpReceivedFromPeer);     // and nothing has crossed. Both true at once.
    }

    /// <summary>Reachability implies NOTHING about crossing — stated as its own assertion (FR-020).</summary>
    [Fact]
    public async Task ReachabilityDoesNotImplyCrossing()
    {
        var cfg = Config(true, Peer("olamnit", "n1", "192.168.0.136:47890"));
        var svc = Service(cfg, new FakeLink("A"));
        await svc.BindAsync();
        await svc.DialAsync("olamnit");

        Assert.NotEqual(svc.Status().PeerAdmitted, svc.Status().OpReceivedFromPeer);
    }

    // ---- SC-010: unknown is not no -------------------------------------------------------------

    /// <summary>
    /// A state that could not be measured reports UNKNOWN, and unknown renders differently from no.
    /// Reporting a clean negative for an unmeasured condition is the failure this prevents.
    /// </summary>
    [Fact]
    public void AnUnmeasurableStateIsUnknownAndRendersDifferentlyFromNo()
    {
        var unknown = new FederationStatus { ListenerBound = Tri.Unknown };
        var no = new FederationStatus { ListenerBound = Tri.No };

        Assert.NotEqual(unknown.ListenerBound, no.ListenerBound);
        Assert.NotEqual(unknown.Render(), no.Render());
        Assert.Contains("listener bound         : unknown", unknown.Render());
        Assert.Contains("listener bound         : no", no.Render());
    }

    /// <summary>Unknown renders as the literal word — never blank, never a dash, never degraded to no.</summary>
    [Fact]
    public void UnknownRendersAsTheLiteralWord()
    {
        var text = new FederationStatus().Render();     // every field defaults to Unknown
        Assert.Contains("stack supported        : unknown", text);
        Assert.Contains("peer admitted          : unknown", text);
        Assert.DoesNotContain(": -", text);
        Assert.DoesNotContain(": \r", text);
    }

    /// <summary>Default construction is Unknown, not No. An unasked question has not been answered.</summary>
    [Fact]
    public void DefaultStateIsUnknownNotNo()
    {
        var s = new FederationStatus();
        Assert.Equal(Tri.Unknown, s.StackSupported);
        Assert.Equal(Tri.Unknown, s.ListenerBound);
        Assert.Equal(Tri.Unknown, s.PeerAdmitted);
        Assert.Equal(Tri.Unknown, s.OpReceivedFromPeer);
    }

    // ---- FR-022: a same-machine crossing is not federation -------------------------------------

    /// <summary>
    /// A loopback crossing sets OpReceivedFromPeer AND SameMachine. The mechanism worked; the
    /// federation claim is not thereby earned. The rendered output must say so.
    /// </summary>
    [Fact]
    public void ASameMachineCrossingIsReportedAsSuchAndNotAsFederation()
    {
        var s = new FederationStatus
        {
            StackSupported = Tri.Yes,
            ListenerBound = Tri.Yes,
            PeerAdmitted = Tri.Yes,
            OpReceivedFromPeer = Tri.Yes,
            SameMachine = true,
        };

        Assert.True(s.SameMachine);
        Assert.Contains("same machine           : yes", s.Render());

        var crossHost = s with { SameMachine = false };
        Assert.NotEqual(s.Render(), crossHost.Render());   // the two are DISTINGUISHABLE
    }

    /// <summary>Loopback and this host's own addresses are detected as same-machine.</summary>
    [Fact]
    public void LoopbackAndOwnAddressesAreDetectedAsSameMachine()
    {
        Assert.True(FederationStatusProbe.IsSameMachine(IPAddress.Any, IPAddress.Loopback));
        Assert.True(FederationStatusProbe.IsSameMachine(IPAddress.Parse("192.168.0.108"), IPAddress.Parse("192.168.0.108")));
    }

    /// <summary>With no crossing observed, SameMachine is n/a — not false. Nothing was measured.</summary>
    [Fact]
    public void NoCrossingObservedMeansSameMachineIsNotApplicable()
    {
        var svc = Service(Config(), new FakeLink("A"));
        Assert.Null(svc.Status().SameMachine);
        Assert.Contains("n/a", svc.Status().Render());
    }

    // ---- FR-023: a policy refusal has its own name ---------------------------------------------

    /// <summary>Smart App Control's 0x800711C7 is recognised and NAMED, not generalised.</summary>
    [Fact]
    public void SmartAppControlRefusalIsNamed()
    {
        var ex = new System.IO.FileLoadException("An Application Control policy has blocked this file.")
        {
            HResult = PolicyRefusal.SmartAppControlHResult,
        };

        var refusal = PolicyRefusal.Detect(ex);
        Assert.NotNull(refusal);
        Assert.Equal("Smart App Control", refusal!.Policy);
        Assert.Contains("Smart App Control", new FederationStatus { PolicyRefused = refusal }.Render());
    }

    /// <summary>An unrelated exception is NOT a policy refusal — the detector must not over-claim.</summary>
    [Fact]
    public void AnUnrelatedFailureIsNotReportedAsAPolicyRefusal()
    {
        Assert.Null(PolicyRefusal.Detect(new IOException("disk full")));
        Assert.Contains("policy refusal         : none", new FederationStatus().Render());
    }

    // ---- S1: there is no aggregate verdict -----------------------------------------------------

    /// <summary>
    /// The type must expose no aggregate boolean. An aggregate is exactly how four honest states
    /// become one dishonest one — this asserts the API shape, which is the only place it can be
    /// prevented.
    /// </summary>
    [Fact]
    public void TheStatusTypeExposesNoAggregateVerdict()
    {
        var names = typeof(FederationStatus).GetProperties().Select(p => p.Name).ToArray();
        Assert.DoesNotContain("IsFederated", names);
        Assert.DoesNotContain("IsHealthy", names);
        Assert.DoesNotContain("Ok", names);

        // ...and the rendering carries no summary line either.
        var text = new FederationStatus().Render();
        Assert.DoesNotContain("federated:", text.ToLowerInvariant());
        Assert.DoesNotContain("overall", text.ToLowerInvariant());
    }
}
