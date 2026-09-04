using System.Net;
using Ynet.Transport.Link;

namespace Ynet.Transport.Tests.Unit;

/// <summary>
/// The QUIC fallback chain (iroh -> msquic -> ngtcp2) and the ngtcp2 Linux tier.
/// </summary>
/// <remarks>
/// These tests are written to fail rather than skip. The failure this repo keeps paying for is a
/// capability gap that renders as health, so every assertion here is about what the chain SAYS when
/// something is missing, not only about what it does when everything is present.
/// </remarks>
public class QuicProviderChainTests
{
    private sealed class FakeProvider(string name, QuicProviderTier tier, bool supported) : IQuicProvider
    {
        public string Name => name;
        public QuicProviderTier Tier => tier;
        public int ProbeCount { get; private set; }

        public QuicAvailability Probe()
        {
            ProbeCount++;
            return supported ? QuicAvailability.Yes($"{name} fake up") : QuicAvailability.No($"{name} fake down");
        }

        public Task<IQuicListenerHandle> BindListenerAsync(IPEndPoint local, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<IWireChannel> ConnectAsync(IPEndPoint remote, CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    [Fact]
    public void Chain_orders_by_tier_regardless_of_registration_order()
    {
        var chain = new QuicProviderChain(new IQuicProvider[]
        {
            new FakeProvider("ngtcp2", QuicProviderTier.Ngtcp2, false),
            new FakeProvider("iroh", QuicProviderTier.Iroh, false),
            new FakeProvider("msquic", QuicProviderTier.MsQuic, false),
        });

        Assert.Equal(new[] { "iroh", "msquic", "ngtcp2" }, chain.Providers.Select(p => p.Name));
    }

    [Fact]
    public void Select_takes_the_lowest_available_tier()
    {
        var chain = new QuicProviderChain(new IQuicProvider[]
        {
            new FakeProvider("iroh", QuicProviderTier.Iroh, supported: false),
            new FakeProvider("msquic", QuicProviderTier.MsQuic, supported: true),
            new FakeProvider("ngtcp2", QuicProviderTier.Ngtcp2, supported: true),
        });

        Assert.Equal("msquic", chain.Select().Name);
    }

    [Fact]
    public void Ngtcp2_carries_the_link_when_iroh_and_msquic_are_both_down()
    {
        // The whole point of the tier: iroh failing completely must not take the host off the network.
        var chain = new QuicProviderChain(new IQuicProvider[]
        {
            new FakeProvider("iroh", QuicProviderTier.Iroh, supported: false),
            new FakeProvider("msquic", QuicProviderTier.MsQuic, supported: false),
            new FakeProvider("ngtcp2", QuicProviderTier.Ngtcp2, supported: true),
        });

        Assert.Equal("ngtcp2", chain.Select().Name);
    }

    [Fact]
    public void Select_does_not_probe_tiers_below_the_one_it_takes()
    {
        var fallback = new FakeProvider("ngtcp2", QuicProviderTier.Ngtcp2, supported: true);
        var chain = new QuicProviderChain(new IQuicProvider[]
        {
            new FakeProvider("msquic", QuicProviderTier.MsQuic, supported: true),
            fallback,
        });

        chain.Select();
        Assert.Equal(0, fallback.ProbeCount);
    }

    [Fact]
    public void When_no_tier_is_available_the_refusal_names_every_tier_and_its_reason()
    {
        var chain = new QuicProviderChain(new IQuicProvider[]
        {
            new FakeProvider("iroh", QuicProviderTier.Iroh, supported: false),
            new FakeProvider("msquic", QuicProviderTier.MsQuic, supported: false),
            new FakeProvider("ngtcp2", QuicProviderTier.Ngtcp2, supported: false),
        });

        var ex = Assert.Throws<QuicUnavailableException>(() => chain.Select("listen"));

        Assert.Equal(3, ex.Diagnoses.Count);
        foreach (var name in new[] { "iroh", "msquic", "ngtcp2" })
        {
            Assert.Contains(name, ex.Message);            // the tier is named
            Assert.Contains($"{name} fake down", ex.Message); // and so is WHY
        }
        Assert.Contains("listen", ex.Message);
    }

    [Fact]
    public void ProbeAll_reports_every_tier_even_after_one_succeeds()
    {
        // An operator needs to know what the fallbacks would do BEFORE the primary fails.
        var chain = new QuicProviderChain(new IQuicProvider[]
        {
            new FakeProvider("msquic", QuicProviderTier.MsQuic, supported: true),
            new FakeProvider("ngtcp2", QuicProviderTier.Ngtcp2, supported: false),
        });

        Assert.Equal(2, chain.ProbeAll().Count);
        Assert.Contains("ngtcp2", chain.Describe());
    }

    [Fact]
    public void Default_chain_is_msquic_then_ngtcp2()
    {
        Assert.Equal(
            new[] { "msquic", "ngtcp2" },
            QuicProviderChain.Default.Providers.Select(p => p.Name));
    }
}

/// <summary>The ngtcp2 tier itself — the distro-native Linux fallback.</summary>
public class Ngtcp2ProviderTests
{
    [Fact]
    public void Probe_never_reports_available_while_the_managed_interop_is_unbuilt()
    {
        // The anti-"green check, deaf service" assertion. A present native engine is necessary and
        // not sufficient; if this ever starts failing because the interop landed, the assertion is
        // what tells you to re-read the tier's contract rather than a service failing at first link.
        var availability = Ngtcp2Provider.Instance.Probe();

        Assert.False(availability.Supported);
        Assert.False(string.IsNullOrWhiteSpace(availability.Detail));
    }

    [Fact]
    public async Task Bind_and_connect_refuse_loudly_rather_than_simulating_a_link()
    {
        var ep = new IPEndPoint(IPAddress.Loopback, 0);

        var bind = await Assert.ThrowsAsync<QuicUnavailableException>(
            () => Ngtcp2Provider.Instance.BindListenerAsync(ep));
        var connect = await Assert.ThrowsAsync<QuicUnavailableException>(
            () => Ngtcp2Provider.Instance.ConnectAsync(ep));

        Assert.Contains("ngtcp2", bind.Message);
        Assert.Contains("ngtcp2", connect.Message);
    }

    [Fact]
    public void Native_probe_is_honest_in_both_directions()
    {
        var native = Ngtcp2Provider.ProbeNative();

        Assert.False(string.IsNullOrWhiteSpace(native.Detail));

        if (native.Present)
        {
            // Provisioned: the version must have been read from the library, not assumed.
            Assert.False(string.IsNullOrWhiteSpace(native.Version));
            Assert.Contains("libngtcp2", native.Detail);
        }
        else if (OperatingSystem.IsLinux())
        {
            // Absent: the refusal must say what to install, not merely that something is missing.
            Assert.Contains("apt install", native.Detail);
            Assert.All(Ngtcp2Provider.AptPackages, p => Assert.False(string.IsNullOrWhiteSpace(p)));
        }
    }

    [Fact]
    public void Apt_packages_are_declared_so_provisioning_reads_them_from_one_place()
    {
        Assert.Contains("libngtcp2-16", Ngtcp2Provider.AptPackages);
        Assert.Contains("libngtcp2-crypto-ossl0", Ngtcp2Provider.AptPackages);
    }
}
