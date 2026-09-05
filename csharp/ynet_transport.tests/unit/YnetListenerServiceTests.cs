using System.Net;
using System.Net.Sockets;
// Ynet.Transport.Path shadows System.IO.Path inside this namespace — alias it (restart-brief rule 5).
using SysPath = System.IO.Path;
using Ynet.Transport.Link;
using Ynet.Transport.Listener;

namespace Ynet.Transport.Tests.Unit;

// ---- 104 / WP-02: configurable QUIC listener service for broker, guardian, oracle, admin ----
public class YnetListenerServiceTests
{
    private static ListenerConfig Loopback(string name) => new(name, IPAddress.Loopback, 0);

    /// <summary>A provider that is never available, with a named reason (stands in for a missing tier).</summary>
    private sealed class UnavailableProvider(string name, QuicProviderTier tier, string why) : IQuicProvider
    {
        public string Name => name;
        public QuicProviderTier Tier => tier;
        public QuicAvailability Probe() => QuicAvailability.No(why);
        public Task<IQuicListenerHandle> BindListenerAsync(IPEndPoint local, CancellationToken ct = default)
            => throw new QuicUnavailableException("listen", new[] { (name, tier, Probe()) });
        public Task<IWireChannel> ConnectAsync(IPEndPoint remote, CancellationToken ct = default)
            => throw new QuicUnavailableException("connect", new[] { (name, tier, Probe()) });
    }

    // ---- SC-001: the bound provider is REPORTED, and it is read off the handle ----
    [Fact]
    public async Task SC001_a_named_service_binds_and_the_report_names_the_provider_that_bound_it()
    {
        // Loud, not skipped: a host with no QUIC provider must FAIL this, not quietly pass it.
        Assert.True(MsQuicProvider.Instance.Probe().Supported,
            "msquic unavailable on this host: " + MsQuicProvider.Instance.Probe().Detail);

        var svc = new YnetListenerService();
        var (report, handle) = await svc.BindAsync(Loopback("yng-broker"));
        await using var _ = handle;

        Assert.NotNull(handle);
        Assert.Equal("yng-broker", report.ServiceName);
        Assert.NotNull(report.Provider);
        // FR-003: reported provider must equal the HANDLE's, not the configuration's.
        Assert.Equal(handle!.ProviderName, report.Provider);
        Assert.Equal(handle.LocalEndPoint, report.BoundEndPoint);
    }

    // ---- SC-001b: a bind alone is NOT Ok. This is the whole point of the outcome enum. ----
    [Fact]
    public async Task SC001b_bind_alone_never_reports_Ok_because_a_bind_is_not_a_link()
    {
        // Loud, not skipped: a host with no QUIC provider must FAIL this, not quietly pass it.
        Assert.True(MsQuicProvider.Instance.Probe().Supported,
            "msquic unavailable on this host: " + MsQuicProvider.Instance.Probe().Detail);

        var svc = new YnetListenerService();
        var (report, handle) = await svc.BindAsync(Loopback("oracle"));
        await using var _ = handle;

        Assert.Equal(ListenerOutcome.BoundUnreachable, report.Outcome);
        Assert.False(report.IsHealthy);
    }

    // ---- SC-002: no sidecar -> msquic wins AND the tier-0 skip is reported with its reason ----
    [Fact]
    public async Task SC002_without_a_sidecar_the_chain_falls_back_and_SAYS_SO()
    {
        // Loud, not skipped: a host with no QUIC provider must FAIL this, not quietly pass it.
        Assert.True(MsQuicProvider.Instance.Probe().Supported,
            "msquic unavailable on this host: " + MsQuicProvider.Instance.Probe().Detail);

        // A sidecar endpoint nothing is listening on — the real condition on a host with no iroh.
        var noSidecar = new IrohSidecarProvider(
            new IPEndPoint(IPAddress.Loopback, 1), TimeSpan.FromMilliseconds(120));
        var chain = new QuicProviderChain(new IQuicProvider[] { noSidecar, MsQuicProvider.Instance });

        var svc = new YnetListenerService(chain);
        var (report, handle) = await svc.BindAsync(Loopback("yng-guardian"));
        await using var _ = handle;

        Assert.Equal("msquic", report.Provider);
        Assert.True(report.FellBack);                                  // FR-008
        var skipped = Assert.Single(report.SkippedTiers);
        Assert.Equal(QuicProviderTier.Iroh, skipped.Tier);
        Assert.Contains("iroh sidecar", skipped.Availability.Detail);
        // the reason must reach the operator-facing text, not just the object
        Assert.Contains("SKIPPED tier 0", report.Describe());
    }

    // ---- SC-003: tier-0 selection is MEASURED, and presence is not mistaken for capability ----
    // codexreview F1: the first version of this test stood up a bare TCP listener and asserted iroh
    // "wins". That proved only that a port accepts. Probe's contract is "can this provider CARRY A
    // LINK here, right now", so the stub now has to speak the capability handshake, and the adapter
    // has to admit whether it implements carriage. Three cases, three different refusals.

    /// <summary>A stub sidecar that speaks (or deliberately mis-speaks) the capability handshake.</summary>
    private static async Task<(IPEndPoint Ep, CancellationTokenSource Stop, Task Loop)> StubSidecar(string? capsLine)
    {
        var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        listener.Listen(64);
        var ep = (IPEndPoint)listener.LocalEndPoint!;
        var stop = new CancellationTokenSource();

        var loop = Task.Run(async () =>
        {
            try
            {
                while (!stop.IsCancellationRequested)
                {
                    using var conn = await listener.AcceptAsync(stop.Token);
                    var buf = new byte[256];
                    var n = await conn.ReceiveAsync(buf, SocketFlags.None, stop.Token);
                    if (n > 0 && capsLine is not null)
                        await conn.SendAsync(System.Text.Encoding.ASCII.GetBytes(capsLine), SocketFlags.None, stop.Token);
                }
            }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
            catch (SocketException) { }
            finally { listener.Dispose(); }
        });

        await Task.Yield();
        return (ep, stop, loop);
    }

    [Fact]
    public async Task SC003a_a_socket_that_accepts_but_says_nothing_is_NOT_available()
    {
        var (ep, stop, loop) = await StubSidecar(capsLine: null);   // accepts, never answers
        try
        {
            var iroh = new IrohSidecarProvider(ep, TimeSpan.FromMilliseconds(400));
            var a = iroh.Probe();
            Assert.False(a.Supported);                              // presence is NOT capability
            Assert.Contains("handshake", a.Detail);
        }
        finally { stop.Cancel(); await loop; }
    }

    [Fact]
    public async Task SC003b_a_sidecar_that_speaks_but_this_build_cannot_carry_links_refuses_precisely()
    {
        var (ep, stop, loop) = await StubSidecar($"{IrohSidecarProvider.Protocol} CAPS quic-link\n");
        try
        {
            // carriesLinks:false — the honest state of this build today.
            var iroh = new IrohSidecarProvider(ep, TimeSpan.FromSeconds(2), carriesLinks: false);
            var a = iroh.Probe();
            Assert.False(a.Supported);
            Assert.Contains("does not implement link carriage", a.Detail);
        }
        finally { stop.Cancel(); await loop; }
    }

    [Fact]
    public async Task SC003c_when_the_sidecar_speaks_AND_the_build_carries_links_iroh_is_SELECTED_at_tier_0()
    {
        var (ep, stop, loop) = await StubSidecar($"{IrohSidecarProvider.Protocol} CAPS quic-link,dht\n");
        try
        {
            // carriesLinks:true — proves FR-004's SELECTION mechanism is real today, so that when
            // carriage lands the tier-0 preference is already measured rather than assumed.
            var iroh = new IrohSidecarProvider(ep, TimeSpan.FromSeconds(2), carriesLinks: true);
            Assert.True(iroh.Probe().Supported);

            var chain = new QuicProviderChain(new IQuicProvider[] { MsQuicProvider.Instance, iroh });
            Assert.Equal(iroh.Name, chain.Providers[0].Name);       // tier 0 sorts first
            Assert.True(chain.TrySelect(out var selected, out _));
            Assert.Equal("iroh-sidecar", selected!.Name);           // and tier 0 is SELECTED
        }
        finally { stop.Cancel(); await loop; }
    }

    [Fact]
    public void SC003d_the_production_instance_is_unavailable_here_and_says_exactly_why()
    {
        // The honest state on OLAMNIT: no sidecar process, no Rust toolchain. The refusal must be
        // actionable, not merely negative.
        var a = IrohSidecarProvider.Instance.Probe();
        Assert.False(a.Supported);
        Assert.Contains("iroh sidecar not usable", a.Detail);
        Assert.Contains("Rust", a.Detail);
    }

    // ---- SC-004: bound but unreachable is its own outcome, never Ok ----
    [Fact]
    public async Task SC004_a_listener_nobody_can_reach_reports_BoundUnreachable_not_Ok()
    {
        // Loud, not skipped: a host with no QUIC provider must FAIL this, not quietly pass it.
        Assert.True(MsQuicProvider.Instance.Probe().Supported,
            "msquic unavailable on this host: " + MsQuicProvider.Instance.Probe().Detail);

        // Bind on a real provider, but give the chain NO way to dial back: the connect side has only
        // an unavailable provider, so the handshake can never complete. That is exactly the shape of
        // a per-binary inbound Block — the socket is open and nothing arrives.
        var bindOnly = new QuicProviderChain(new IQuicProvider[] { MsQuicProvider.Instance });
        var (report, handle) = await new YnetListenerService(bindOnly).BindAsync(Loopback("admin"));
        Assert.NotNull(handle);

        var deaf = new QuicProviderChain(new IQuicProvider[]
        {
            new UnavailableProvider("no-dial", QuicProviderTier.MsQuic, "dial path removed for this test"),
        });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var reachable = await new YnetListenerService(deaf).ProbeReachabilityAsync(handle!, cts.Token);
        await handle!.DisposeAsync();

        Assert.False(reachable);
        Assert.NotEqual(ListenerOutcome.Ok, report.Outcome);
    }

    // ---- SC-005: no provider -> refuse to start, naming every tier ----
    [Fact]
    public async Task SC005_when_no_provider_can_serve_the_service_refuses_and_names_every_tier()
    {
        var chain = new QuicProviderChain(new IQuicProvider[]
        {
            new UnavailableProvider("iroh-sidecar", QuicProviderTier.Iroh, "no sidecar process"),
            new UnavailableProvider("msquic", QuicProviderTier.MsQuic, "libmsquic absent"),
            new UnavailableProvider("ngtcp2", QuicProviderTier.Ngtcp2, "libngtcp2 absent"),
        });

        var (report, handle) = await new YnetListenerService(chain).BindAsync(Loopback("yng-broker"));

        Assert.Null(handle);
        Assert.Equal(ListenerOutcome.Refused, report.Outcome);
        Assert.False(report.IsHealthy);
        Assert.Equal(3, report.Diagnoses.Count);

        var text = report.Describe();
        Assert.Contains("no sidecar process", text);
        Assert.Contains("libmsquic absent", text);
        Assert.Contains("libngtcp2 absent", text);
    }

    // ---- FR-001: the operator-facing one-line config form ----
    [Fact]
    public void ListenerConfig_parses_the_operator_one_liner_and_refuses_malformed_input()
    {
        var c = ListenerConfig.Parse("yng-broker@0.0.0.0:47890");
        Assert.Equal("yng-broker", c.ServiceName);
        Assert.Equal(IPAddress.Any, c.BindAddress);
        Assert.Equal(ListenerConfig.FederationPort, c.Port);

        Assert.Throws<FormatException>(() => ListenerConfig.Parse("no-at-sign:47890"));
        Assert.Throws<FormatException>(() => ListenerConfig.Parse("svc@not-an-ip:47890"));
        Assert.Throws<FormatException>(() => ListenerConfig.Parse("svc@127.0.0.1:99999"));
    }

    // ---- FR-004 / Q-olg15-03: the FLEET DEFAULT must carry iroh at tier 0 and RETAIN the fallbacks ----
    [Fact]
    public void The_default_chain_puts_iroh_first_and_keeps_msquic_and_ngtcp2_beneath_it()
    {
        var names = QuicProviderChain.Default.Providers.Select(p => p.Name).ToArray();
        Assert.Equal(new[] { "iroh-sidecar", "msquic", "ngtcp2" }, names);
    }

    // ---- FR-012: this feature must contain no election. Enforced, not promised. ----
    [Fact]
    public void The_listener_service_contains_no_election_campaign_vote_or_leader()
    {
        var root = FindRepoRoot();
        var files = new[]
        {
            SysPath.Combine(root, "csharp", "ynet_transport", "Listener", "YnetListenerService.cs"),
            SysPath.Combine(root, "csharp", "ynet_transport", "Listener", "ListenerConfig.cs"),
            SysPath.Combine(root, "csharp", "ynet_transport", "Listener", "ListenerReport.cs"),
            SysPath.Combine(root, "csharp", "ynet_transport", "Link", "IrohSidecarProvider.cs"),
            // codexreview F6: QuicProviderChain.cs is a CHANGED file of this feature and was
            // omitted, so election logic added there would have passed the feature's own
            // no-election test. The list must cover every file the feature touches.
            SysPath.Combine(root, "csharp", "ynet_transport", "Link", "QuicProviderChain.cs"),
        };

        // "leader" appears in no forbidden form; the words below would each indicate a lane-local
        // election mechanism, which Q-gsbk14-01 forbids in any probe, tool or pipeline command.
        string[] forbidden = ["Election", "Campaign", "CastVote", "SeatLeader", "ElectLeader"];

        foreach (var f in files)
        {
            Assert.True(File.Exists(f), $"expected source file missing: {f}");
            var text = File.ReadAllText(f);
            foreach (var word in forbidden)
                Assert.DoesNotContain(word, text, StringComparison.Ordinal);
        }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(SysPath.Combine(dir.FullName, "CLAUDE.md")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
