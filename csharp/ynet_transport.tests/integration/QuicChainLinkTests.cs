using System.Net;
using System.Text;
using Ynet.Transport.Link;

namespace Ynet.Transport.Tests.Integration;

/// <summary>
/// End-to-end over the fallback chain: whichever tier this host can serve must carry a real
/// bidirectional QUIC link, and a host that can serve none must say so naming every tier.
/// </summary>
/// <remarks>
/// There is no vacuous branch here. The usual shape — <c>if (!IsSupported) return;</c> — is exactly
/// the "green check that certifies something other than what will run" this chain exists to prevent,
/// so the unsupported path asserts the REFUSAL instead of skipping.
/// </remarks>
public class QuicChainLinkTests(Xunit.Abstractions.ITestOutputHelper output)
{
    [Fact]
    public async Task Chain_either_carries_a_real_link_or_refuses_naming_every_tier()
    {
        // A hang is not a result. Bound the whole exchange so a regression FAILS instead of stalling
        // the suite — this test deadlocked once already, for exactly the reason documented below.
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var chain = QuicProviderChain.Default;

        // Recorded in the test output so a run is evidence about THIS host, not about a fake.
        output.WriteLine(chain.Describe());

        if (!chain.TrySelect(out var provider, out var diagnoses))
        {
            var ex = Assert.Throws<QuicUnavailableException>(() => chain.Select("listen"));
            Assert.Equal(diagnoses.Count, ex.Diagnoses.Count);
            Assert.All(diagnoses, d => Assert.Contains(d.Provider, ex.Message));
            Assert.All(diagnoses, d => Assert.False(string.IsNullOrWhiteSpace(d.Availability.Detail)));
            return;
        }

        output.WriteLine($"selected tier {(int)provider!.Tier} {provider.Name}");

        await using var listener = await provider.BindListenerAsync(new IPEndPoint(IPAddress.Loopback, 0), cts.Token);
        Assert.Equal(provider.Name, listener.ProviderName);
        Assert.NotEqual(0, listener.LocalEndPoint.Port);

        var accept = listener.AcceptAsync(cts.Token);
        using var client = await provider.ConnectAsync(listener.LocalEndPoint, cts.Token);

        // ORDER IS LOAD-BEARING, and getting it wrong DEADLOCKS rather than fails: MsQuic does not
        // realize an outbound stream on the peer until the client writes, so awaiting the accept
        // before the first write hangs both ends forever. Write first, then accept.
        var payload = Encoding.UTF8.GetBytes("ultimate-fallback-probe");
        client.WriteFrame(payload);

        using var server = await accept;
        // ReadFrame() blocks on BlockingCollection.Take() with no token, so the 30 s bound must be
        // applied to the AWAIT, or a stalled receive loop hangs the suite despite the timeout.
        Assert.Equal(payload, await Task.Run(server.ReadFrame).WaitAsync(cts.Token));

        // and the other direction — a bilateral link, not a one-way pipe (FR-004/FR-005)
        var reply = Encoding.UTF8.GetBytes("pong");
        server.WriteFrame(reply);
        Assert.Equal(reply, await Task.Run(client.ReadFrame).WaitAsync(cts.Token));
    }
}
