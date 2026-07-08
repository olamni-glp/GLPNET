// Real-QUIC adapter behind the link seam (048 bk-colab-yngenios-transport, T013).
//
// A genuine System.Net.Quic/MsQuic handshake between two in-process endpoints on 127.0.0.1 — real
// UDP datagrams, real TLS 1.3, per-peer SPKI pinning with self-signed dev certs. Proves the
// peer-link.md loopback surface: connect (hello + mutual pin), per-box bidirectional stream
// exchange (one QUIC stream per box — no cross-box interleaving of lanes), advisory presence pings,
// pin-refusal of an un-pinned peer, and orderly close. Skipped automatically where the platform
// lacks QUIC (the glp_link QuicTransportTests convention). The 5-point real-LAN acceptance run
// (loss / partition / migration / back-fill on two hosts) is the GATED multi-host tier (D3) — see
// the env-gated stub at the bottom.

using System.Net;

using GlpRuntime.CrdtMsg.Route;

namespace GlpRuntime.CrdtMsg.Tests;

public sealed class QuicLinkTransportTests
{
    private static bool QuicAvailable => QuicLinkTransport.IsSupported;

    private static CancellationToken Timeout(int seconds = 20) =>
        new CancellationTokenSource(TimeSpan.FromSeconds(seconds)).Token;

    private static async Task WaitUntilAsync(Func<bool> condition, CancellationToken ct)
    {
        while (!condition())
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(25, ct);
        }
    }

    /// <summary>Two linked endpoints on loopback: A listens (OS-assigned port), B dials. Mutual pins.</summary>
    private static async Task<(QuicLinkTransport a, QuicLinkTransport b)> PairAsync(CancellationToken ct)
    {
        var certA = QuicLinkTransport.CreateDevCert("colab-A");
        var certB = QuicLinkTransport.CreateDevCert("colab-B");
        var a = new QuicLinkTransport("A", certA,
            new Dictionary<string, string> { ["B"] = QuicLinkTransport.SpkiPin(certB) });
        var b = new QuicLinkTransport("B", certB,
            new Dictionary<string, string> { ["A"] = QuicLinkTransport.SpkiPin(certA) });

        await a.ListenAsync(new IPEndPoint(IPAddress.Loopback, 0), ct);
        await b.ConnectPeerAsync("A", a.ListenEndPoint!, ct);
        await WaitUntilAsync(() => a.Members.Contains("B"), ct); // accept-side registration is async
        return (a, b);
    }

    [Fact]
    public async Task Connect_links_both_ends_with_mutual_pins()
    {
        if (!QuicAvailable) return; // platform lacks QUIC — nothing to verify here
        var ct = Timeout();
        var (a, b) = await PairAsync(ct);
        try
        {
            Assert.Contains("B", a.Members);
            Assert.Contains("A", b.Members);
            Assert.NotNull(a.LastSeen("B")); // link-up counts as first sighting
        }
        finally
        {
            await a.DisposeAsync();
            await b.DisposeAsync();
        }
    }

    [Fact]
    public async Task PerBox_streams_exchange_frames_both_directions()
    {
        if (!QuicAvailable) return; // platform lacks QUIC — nothing to verify here
        var ct = Timeout();
        var (a, b) = await PairAsync(ct);
        try
        {
            // dialer → listener on box "inbox"
            await b.SendAsync("A", "inbox", new byte[] { 1, 2, 3 }, ct);
            var got = await a.Inbound.ReadAsync(ct);
            Assert.Equal(("B", "inbox"), (got.FromPeer, got.Box));
            Assert.Equal(new byte[] { 1, 2, 3 }, got.Bytes);

            // listener → dialer on box "wip" (streams are role-independent)
            await a.SendAsync("B", "wip", new byte[] { 9, 8 }, ct);
            got = await b.Inbound.ReadAsync(ct);
            Assert.Equal(("A", "wip"), (got.FromPeer, got.Box));
            Assert.Equal(new byte[] { 9, 8 }, got.Bytes);

            // the plain (box-less) seam send rides the default-box lane (back-compat)
            await b.SendAsync("A", new byte[] { 7 }, ct);
            got = await a.Inbound.ReadAsync(ct);
            Assert.Equal("default", got.Box);
        }
        finally
        {
            await a.DisposeAsync();
            await b.DisposeAsync();
        }
    }

    [Fact]
    public async Task Boxes_multiplex_on_independent_lanes()
    {
        if (!QuicAvailable) return; // platform lacks QUIC — nothing to verify here
        var ct = Timeout();
        var (a, b) = await PairAsync(ct);
        try
        {
            // interleave sends across two boxes; each box's lane stays FIFO, boxes never cross-tag
            for (byte i = 0; i < 4; i++)
            {
                await b.SendAsync("A", "inbox", new byte[] { 0x10, i }, ct);
                await b.SendAsync("A", "wip", new byte[] { 0x20, i }, ct);
            }
            var byBox = new Dictionary<string, List<byte>>() { ["inbox"] = new(), ["wip"] = new() };
            for (int n = 0; n < 8; n++)
            {
                var got = await a.Inbound.ReadAsync(ct);
                byBox[got.Box].Add(got.Bytes[1]);
                Assert.Equal(got.Box == "inbox" ? (byte)0x10 : (byte)0x20, got.Bytes[0]); // tag matches lane
            }
            Assert.Equal(new byte[] { 0, 1, 2, 3 }, byBox["inbox"]); // per-lane FIFO preserved
            Assert.Equal(new byte[] { 0, 1, 2, 3 }, byBox["wip"]);
        }
        finally
        {
            await a.DisposeAsync();
            await b.DisposeAsync();
        }
    }

    [Fact]
    public async Task Presence_ping_advises_liveness()
    {
        if (!QuicAvailable) return; // platform lacks QUIC — nothing to verify here
        var ct = Timeout();
        var (a, b) = await PairAsync(ct);
        try
        {
            DateTimeOffset? before = a.LastSeen("B");
            await Task.Delay(50, ct); // let the clock move past the link-up sighting
            await b.SendPresencePingAsync("A", ct);
            await WaitUntilAsync(() => a.LastSeen("B") > before, ct);
            Assert.True(a.LastSeen("B") > before);
        }
        finally
        {
            await a.DisposeAsync();
            await b.DisposeAsync();
        }
    }

    [Fact]
    public async Task Unpinned_peer_is_refused()
    {
        if (!QuicAvailable) return; // platform lacks QUIC — nothing to verify here
        var ct = Timeout();
        var certA = QuicLinkTransport.CreateDevCert("colab-A");
        var certM = QuicLinkTransport.CreateDevCert("colab-mallory");
        var a = new QuicLinkTransport("A", certA, new Dictionary<string, string>()); // pins NOBODY
        var m = new QuicLinkTransport("M", certM,
            new Dictionary<string, string> { ["A"] = QuicLinkTransport.SpkiPin(certA) });
        try
        {
            await a.ListenAsync(new IPEndPoint(IPAddress.Loopback, 0), ct);
            // mere reachability MUST NOT admit: the TLS pin gate rejects mallory's cert (US4 AS-2)
            await Assert.ThrowsAnyAsync<Exception>(() => m.ConnectPeerAsync("A", a.ListenEndPoint!, ct));
            Assert.DoesNotContain("M", a.Members);
        }
        finally
        {
            await a.DisposeAsync();
            await m.DisposeAsync();
        }
    }

    [Fact]
    public async Task Dialing_an_unpinned_name_refuses_locally()
    {
        if (!QuicAvailable) return; // platform lacks QUIC — nothing to verify here
        var cert = QuicLinkTransport.CreateDevCert("colab-A");
        var a = new QuicLinkTransport("A", cert, new Dictionary<string, string>());
        await using (a)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => a.ConnectPeerAsync("nobody", new IPEndPoint(IPAddress.Loopback, 1), Timeout(2)));
        }
    }

    [Fact]
    public async Task Orderly_close_completes_the_inbound_channel()
    {
        if (!QuicAvailable) return; // platform lacks QUIC — nothing to verify here
        var ct = Timeout();
        var (a, b) = await PairAsync(ct);

        await b.SendAsync("A", "inbox", new byte[] { 1 }, ct);
        var got = await a.Inbound.ReadAsync(ct);
        Assert.Equal(new byte[] { 1 }, got.Bytes);

        await b.DisposeAsync();
        await a.DisposeAsync();
        await a.Inbound.Completion.WaitAsync(ct); // close is orderly: the channel COMPLETES, no hang
        await b.Inbound.Completion.WaitAsync(ct);
    }

    /// <summary>
    /// The 5-point real-LAN acceptance surface (peer-link.md: no-preshared-address handshake,
    /// convergence under loss, under partition, connection migration, Bloom back-fill) REQUIRES two
    /// physical hosts — set COLAB_QUIC_LAN_PEER=host:port to run it; skipped otherwise. The loopback
    /// tier above covers seam wiring only and is NOT acceptance-sufficient alone (D3, K7).
    /// </summary>
    [Fact]
    public async Task MultiHost_lan_acceptance_when_available()
    {
        if (!QuicAvailable) return; // platform lacks QUIC — nothing to verify here
        string? peer = Environment.GetEnvironmentVariable("COLAB_QUIC_LAN_PEER");
        if (string.IsNullOrWhiteSpace(peer)) return; // multi-host rig absent — gated tier (D3)

        string[] parts = peer.Split(':');
        var remote = new IPEndPoint(IPAddress.Parse(parts[0]), int.Parse(parts[1]));
        string? pin = Environment.GetEnvironmentVariable("COLAB_QUIC_LAN_PIN");
        Assert.False(string.IsNullOrWhiteSpace(pin), "COLAB_QUIC_LAN_PIN must carry the remote SPKI pin");

        var cert = QuicLinkTransport.CreateDevCert("colab-lan-probe");
        var t = new QuicLinkTransport("probe", cert, new Dictionary<string, string> { ["lan-peer"] = pin! });
        await using (t)
        {
            await t.ConnectPeerAsync("lan-peer", remote, Timeout(10));
            await t.SendAsync("lan-peer", "inbox", new byte[] { 42 }, Timeout(10));
        }
    }
}
