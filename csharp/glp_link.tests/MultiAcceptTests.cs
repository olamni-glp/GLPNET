using GlpRuntime.Link.Seam;
using GlpRuntime.Link.Transports;
using Xunit;

namespace GlpRuntime.Link.Tests;

/// <summary>
/// T019/T021 (feature 062 US3) — multi-accept: one server accepts ≥2 concurrent
/// clients on ONE address, each an independent bilateral link (distinct
/// <see cref="LinkId"/>), none dropped. This is N separate point-to-point links
/// (D-9), not a fan-out hub. The role-aware <see cref="LoopbackTransport"/> pairs
/// each parked listener with exactly one connector.
/// </summary>
public class MultiAcceptTests
{
    [Fact]
    public async Task Server_accepts_two_concurrent_clients_none_dropped()
    {
        var t = new LoopbackTransport();
        var addr = LinkAddress.Path("srv2");

        var accept = MultiAcceptListener.AcceptManyAsync(
            t, LinkScheme.Loopback, addr, LinkOptions.Default, count: 2);

        var c1 = await t.ConnectAsync(LinkScheme.Loopback, addr, LinkOptions.Default);
        var c2 = await t.ConnectAsync(LinkScheme.Loopback, addr, LinkOptions.Default);
        var servers = await accept;

        Assert.Equal(2, servers.Length);
        Assert.Equal(0, t.PendingCount);                              // none dangling
        Assert.NotEqual(servers[0].Id, servers[1].Id);               // two independent links

        // Each server end round-trips with its paired client (paired by shared LinkId).
        var clients = new[] { c1, c2 };
        foreach (var s in servers)
        {
            var client = clients.First(c => c.Id.Equals(s.Id));
            await client.SendBytesAsync(new byte[] { 7 });
            Assert.Equal(new byte[] { 7 }, await s.RecvBytesAsync());
        }
    }

    [Fact]
    public async Task Five_clients_all_served_with_distinct_links()
    {
        var t = new LoopbackTransport();
        var addr = LinkAddress.Path("srv5");

        var accept = MultiAcceptListener.AcceptManyAsync(
            t, LinkScheme.Loopback, addr, LinkOptions.Default, count: 5);

        for (int i = 0; i < 5; i++)
            await t.ConnectAsync(LinkScheme.Loopback, addr, LinkOptions.Default);

        var servers = await accept;

        Assert.Equal(5, servers.Length);
        Assert.Equal(5, servers.Select(s => s.Id).Distinct().Count()); // 5 distinct links
        Assert.Equal(0, t.PendingCount);
    }
}
