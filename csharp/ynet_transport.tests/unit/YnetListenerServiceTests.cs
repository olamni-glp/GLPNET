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

    // ---- SC-003: with a stub sidecar reachable, iroh WINS at tier 0 ----
    // This is what proves FR-004 rather than restating it: without it, "iroh is registered at tier 0"
    // is a claim about a list, not a measurement of selection.
    [Fact]
    public async Task SC003_a_reachable_sidecar_makes_iroh_probe_available_and_rank_first()
    {
        using var stub = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        stub.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        stub.Listen(64);
        var stubEp = (IPEndPoint)stub.LocalEndPoint!;

        // A real sidecar ACCEPTS. Without this loop the first Probe fills the accept backlog and
        // every later Probe times out — which is how this test failed the first time it ran, and is
        // worth keeping: an unaccepting listener is indistinguishable from an absent one after the
        // backlog fills, which is itself a "bound but not serving" case.
        using var stopAccepting = new CancellationTokenSource();
        var accepting = Task.Run(async () =>
        {
            try
            {
                while (!stopAccepting.IsCancellationRequested)
                    (await stub.AcceptAsync(stopAccepting.Token)).Dispose();
            }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
            catch (SocketException) { }
        });

        var iroh = new IrohSidecarProvider(stubEp, TimeSpan.FromSeconds(2));

        var availability = iroh.Probe();
        Assert.True(availability.Supported);                            // measured, not assumed
        Assert.Contains("reachable", availability.Detail);

        var chain = new QuicProviderChain(new IQuicProvider[] { MsQuicProvider.Instance, iroh });
        Assert.Equal(iroh.Name, chain.Providers[0].Name);               // tier 0 sorts first
        Assert.True(chain.TrySelect(out var selected, out _));
        Assert.Equal("iroh-sidecar", selected!.Name);                   // and tier 0 is SELECTED

        stopAccepting.Cancel();
        await accepting;
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
