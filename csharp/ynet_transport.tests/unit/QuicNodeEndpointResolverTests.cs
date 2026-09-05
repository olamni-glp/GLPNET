using System.Net;
using System.Net.Sockets;
using Ynet.Transport.Capability;
using Ynet.Transport.Link;

namespace Ynet.Transport.Tests;

/// <summary>
/// Feature 102, second half: <c>Connect</c> over a REAL QUIC wire, composed from
/// <see cref="INodeAddressResolver"/>. The refusal-passthrough tests matter most — a caller that
/// cannot dial needs to know WHICH of five things went wrong, because they have different owners.
/// </summary>
public sealed class QuicNodeEndpointResolverTests
{
    private static NodeId WellFormedId() => NodeIdentity.Generate().NodeId;

    [Fact]
    public void Unknown_id_is_refused_record_not_found_and_never_dials()
    {
        var resolver = new QuicNodeEndpointResolver(new StaticNodeAddressResolver());
        var result = resolver.OpenChannel(WellFormedId());

        Assert.False(result.Ok);
        Assert.Equal(RefusalReason.RecordNotFound, result.Reason);
    }

    [Fact]
    public void Non_key_name_is_refused_further_resolver_required()
    {
        var resolver = new QuicNodeEndpointResolver(new StaticNodeAddressResolver());
        Assert.Equal(
            RefusalReason.FurtherResolverRequired,
            resolver.OpenChannel(new NodeId("shiras.glpnet")).Reason);
    }

    // The distinction the oracle's "never counted and never refused" defect turns on: a lapsed
    // lease must not arrive at the caller looking like an id nobody ever heard of.
    [Fact]
    public void Lapsed_lease_is_refused_unreachable_not_record_not_found()
    {
        var now = DateTimeOffset.UnixEpoch;
        var id = WellFormedId();
        var addresses = new StaticNodeAddressResolver(() => now);
        addresses.Bind(id, NodeAddress.Quic("127.0.0.1", 47899), expiresAt: now.AddMinutes(1));
        now = now.AddMinutes(2);

        var result = new QuicNodeEndpointResolver(addresses).OpenChannel(id);
        Assert.Equal(RefusalReason.Unreachable, result.Reason);
    }

    // A DNS name is not resolved here, on purpose (FR-017): trusting the host resolver would put a
    // peer's identity→address binding in DNS, outside the self-certified overlay.
    [Fact]
    public void Dns_name_is_refused_rather_than_looked_up()
    {
        var id = WellFormedId();
        var addresses = new StaticNodeAddressResolver();
        addresses.Bind(id, NodeAddress.Quic("shiras.local", 47899));

        var result = new QuicNodeEndpointResolver(addresses).OpenChannel(id);
        Assert.Equal(RefusalReason.FurtherResolverRequired, result.Reason);
    }

    [Fact]
    public void Foreign_scheme_is_not_dialed()
    {
        var id = WellFormedId();
        var addresses = new StaticNodeAddressResolver();
        addresses.Bind(id, new NodeAddress("http", "127.0.0.1", 8080));

        Assert.Equal(
            RefusalReason.FurtherResolverRequired,
            new QuicNodeEndpointResolver(addresses).OpenChannel(id).Reason);
    }

    [Theory]
    [InlineData("127.0.0.1", 47899, true)]
    [InlineData("[::1]", 47899, true)]
    [InlineData("::1", 47899, true)]
    [InlineData("shiras.local", 47899, false)]
    [InlineData("", 47899, false)]
    public void Only_ip_literals_are_dialable(string host, int port, bool dialable)
    {
        Assert.Equal(
            dialable,
            QuicNodeEndpointResolver.TryDialableEndpoint(NodeAddress.Quic(host, port), out var ep));
        if (dialable) Assert.Equal(port, ep!.Port);
    }

    // A well-formed address whose peer is not there: authorized-but-unreachable, never a hang and
    // never an exception escaping to the caller.
    [Fact]
    public void Dead_endpoint_is_refused_not_thrown_and_does_not_hang()
    {
        if (!QuicNodeEndpointResolver.IsSupported) return; // no QUIC here; covered by the gate test

        var id = WellFormedId();
        var addresses = new StaticNodeAddressResolver();
        addresses.Bind(id, NodeAddress.Quic("127.0.0.1", FreeUdpPort()));

        var resolver = new QuicNodeEndpointResolver(addresses, TimeSpan.FromSeconds(3));
        var started = DateTimeOffset.UtcNow;
        var result = resolver.OpenChannel(id);
        var elapsed = DateTimeOffset.UtcNow - started;

        Assert.False(result.Ok);
        Assert.Equal(RefusalReason.AuthorizedButUnreachable, result.Reason);
        Assert.True(elapsed < TimeSpan.FromSeconds(20), $"dial was not bounded: {elapsed}");
    }

    // The end of the era's arc: two nodes, one resolved by ID over a real QUIC wire, a sealed
    // YnetSession on top, a frame across and back. Nothing in this test names an address twice.
    [Fact]
    public async Task Two_nodes_connect_by_id_over_a_real_quic_wire_and_exchange_a_sealed_frame()
    {
        if (!QuicNodeEndpointResolver.IsSupported) return;

        using var quicCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var listener = await QuicWireChannel.BindListenerAsync(
            new IPEndPoint(IPAddress.Loopback, 0), quicCts.Token);
        var port = listener.LocalEndPoint.Port;

        using var dialerIdentity = NodeIdentity.Generate();
        using var listenerIdentity = NodeIdentity.Generate();

        var addresses = new StaticNodeAddressResolver();
        addresses.Bind(listenerIdentity.NodeId, NodeAddress.Quic("127.0.0.1", port));

        var accept = Task.Run(async () =>
        {
            var channel = await QuicWireChannel.AcceptAsync(listener, quicCts.Token);
            return YnetSession.Accept(channel, listenerIdentity, RoutingSelection.SafeDefault);
        });

        using var dialer = new YnetTransportCapability(
            dialerIdentity,
            new QuicNodeEndpointResolver(addresses, TimeSpan.FromSeconds(20)),
            addresses: addresses);

        // resolution and dialing are separate, and both work off the SAME id
        var resolved = dialer.Resolve(listenerIdentity.NodeId);
        Assert.True(resolved.Ok);
        Assert.Equal(port, resolved.Value.Port);

        var link = dialer.Connect(listenerIdentity.NodeId, RoutingSelection.SafeDefault);
        Assert.True(link.Ok, $"connect refused: {link.Reason}");

        var accepted = await accept;
        Assert.True(accepted.Ok, $"accept refused: {accepted.Reason}");
        using var serverSession = accepted.Value!;

        var payload = System.Text.Encoding.UTF8.GetBytes("era-102: resolved by id, sealed on the wire");
        Assert.True(dialer.Send(link.Value, payload).Ok);

        var received = serverSession.Receive();
        Assert.True(received.Ok, $"receive refused: {received.Reason}");
        Assert.Equal(payload, received.Value.ToArray());

        dialer.Close(link.Value);
    }

    private static int FreeUdpPort()
    {
        using var probe = new Socket(
            AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        probe.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)probe.LocalEndPoint!).Port;
    }
}
