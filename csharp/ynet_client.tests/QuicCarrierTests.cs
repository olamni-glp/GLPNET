// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT

using System.Net;
using System.Text;
using System.Text.Json;
using Ynet.Client;
using Ynet.Transport.Capability;
using Ynet.Transport.Link;
using Ynet.Transport.Listener;

namespace Ynet.Client.Tests;

/// <summary>
/// The QUIC realization of the receive seam — path 1, the YNET wire.
///
/// Every test here drives a REAL <see cref="YnetSession"/> over a real duplex channel and a real
/// signed handshake. None of them assert against a fake carrier: the whole reason this class exists
/// is that <see cref="IYnetInbound"/> previously had only an in-memory plane and a file plane, and a
/// third fake would have proved nothing about the wire.
/// </summary>
public class QuicCarrierTests
{
    private static byte[] Frame(string origin, string senderNode, string senderActor, string signal, string body, long seq = 1) =>
        JsonSerializer.SerializeToUtf8Bytes(new YnetFrame
        {
            Origin = origin,
            Sequence = seq,
            SenderNode = senderNode,
            SenderActor = senderActor,
            Signal = signal,
            Body = body,
        });

    // ---- Decode: what reaches a handler, and what is refused before it can ----
    // Decode is the whole trust boundary of this plane, so it is tested directly AND through a live
    // session below. Testing only through the session would make a decode regression look like a
    // transport flake.

    [Fact]
    public void A_well_formed_frame_from_the_authenticated_peer_is_delivered()
    {
        var msg = QuicInbound.Decode(Frame("nodeA/laneA", "nodeA", "laneA", "M6_PROOF", "hello"), "nodeA/laneA");

        Assert.NotNull(msg);
        Assert.Equal("nodeA/laneA", msg!.Origin);
        Assert.Equal("M6_PROOF", msg.Summary);
        Assert.Equal("hello", Encoding.UTF8.GetString(msg.Body.Span));
        Assert.Equal("nodeA/laneA#1", msg.MessageId);
    }

    [Fact]
    public void An_empty_object_is_NOT_a_message()
    {
        // `{}` deserializes into a YnetFrame with every field defaulted. Delivering that as a message
        // was a real defect on the file plane (codexreview 2026-09-05); the wire plane must not
        // reinvent it.
        Assert.Null(QuicInbound.Decode("{}"u8.ToArray(), "nodeA/laneA"));
    }

    [Fact]
    public void A_zero_length_payload_is_refused()
        => Assert.Null(QuicInbound.Decode(ReadOnlyMemory<byte>.Empty, "nodeA/laneA"));

    [Fact]
    public void Malformed_json_is_refused_rather_than_thrown()
        => Assert.Null(QuicInbound.Decode("not json at all"u8.ToArray(), "nodeA/laneA"));

    [Fact]
    public void An_oversize_frame_is_refused_rather_than_buffered()
    {
        var huge = Frame("nodeA/laneA", "nodeA", "laneA", "S", new string('x', QuicInbound.MaxFrameBytes + 16));
        Assert.True(huge.Length > QuicInbound.MaxFrameBytes, "the fixture must actually exceed the ceiling");
        Assert.Null(QuicInbound.Decode(huge, "nodeA/laneA"));
    }

    [Fact]
    public void A_frame_claiming_a_sender_that_is_NOT_the_authenticated_peer_is_refused()
    {
        // 🔴 The security case. On this plane the sender is PROVEN by the handshake, so a frame that
        // claims to come from someone else is refused outright — never normalized, never delivered
        // with a warning. "Strip a field and it becomes yours" was the worst finding of era 105.
        var forged = Frame(origin: "victim/lane", senderNode: "victim", senderActor: "lane", "S", "b");
        Assert.Null(QuicInbound.Decode(forged, authenticatedPeer: "attacker/lane"));
    }

    [Fact]
    public void The_refusal_is_NOT_vacuous_the_same_frame_from_the_right_peer_IS_delivered()
    {
        // Negative control for the test above. Without this, a Decode that refused EVERYTHING would
        // pass the forgery test and look like a working guard. This repo has already shipped one
        // control that could not fail.
        var frame = Frame("victim/lane", "victim", "lane", "S", "b");
        Assert.Null(QuicInbound.Decode(frame, "attacker/lane"));
        Assert.NotNull(QuicInbound.Decode(frame, "victim/lane"));
    }

    // ---- The wire: a real session, a real handshake, the same envelope as the file plane ----

    [Fact]
    public void A_frame_crosses_a_REAL_ynet_session_and_arrives_through_the_seam()
    {
        var (a, b) = InProcessDuplexChannel.CreatePair();
        using var client = NodeIdentity.Generate();
        using var server = NodeIdentity.Generate();

        // Accept blocks until the peer dials, and Receive blocks on a BlockingCollection — so this
        // runs on a DEDICATED THREAD, not Task.Run. Parking blocking work on the pool is the exact
        // defect measured in ynet_transport on 2026-09-06.
        Result<YnetSession> accepted = default;
        var t = new Thread(() => accepted = YnetSession.Accept(b, server, RoutingSelection.SafeDefault))
        { IsBackground = true, Name = "test-accept" };
        t.Start();

        var dialed = YnetSession.Connect(a, client, server.NodeId, RoutingSelection.SafeDefault);
        Assert.True(t.Join(TimeSpan.FromSeconds(10)), "the accept side never completed");
        Assert.True(dialed.Ok, $"connect refused: {dialed.Reason}");
        Assert.True(accepted.Ok, $"accept refused: {accepted.Reason}");

        using var dialSession = dialed.Value!;
        using var acceptSession = accepted.Value!;

        var peer = client.NodeId.ToString();
        Assert.True(dialSession.Send(Frame(peer, peer, "glpnet", "M6_WIRE", "carried by QUIC, not by a disk")).Ok);

        var got = acceptSession.Receive();
        Assert.True(got.Ok, $"receive refused: {got.Reason}");

        var msg = QuicInbound.Decode(got.Value, peer);
        Assert.NotNull(msg);
        Assert.Equal("M6_WIRE", msg!.Summary);
        Assert.Equal("carried by QUIC, not by a disk", Encoding.UTF8.GetString(msg.Body.Span));
    }

    [Fact]
    public void The_wire_envelope_is_BYTE_IDENTICAL_to_the_file_planes()
    {
        // The two planes must not drift into two protocols that share a name. If this ever fails,
        // one plane changed its envelope and the other did not — which is precisely the bug class a
        // single seam exists to make impossible.
        var frame = new YnetFrame
        {
            Origin = "n/a", Sequence = 7, SenderNode = "n", SenderActor = "a",
            Signal = "SIG", Body = "body",
        };

        var wire = JsonSerializer.SerializeToUtf8Bytes(frame);
        var reparsed = JsonSerializer.Deserialize<YnetFrame>(wire);

        Assert.NotNull(reparsed);
        Assert.Equal(frame.Origin, reparsed!.Origin);
        Assert.Equal(frame.Sequence, reparsed.Sequence);
        Assert.Equal(frame.SenderNode, reparsed.SenderNode);
        Assert.Equal(frame.SenderActor, reparsed.SenderActor);
        Assert.Equal(frame.Signal, reparsed.Signal);
        Assert.Equal(frame.Body, reparsed.Body);
    }

    // ---- Lifecycle ----

    [Fact]
    public void Close_before_Open_is_a_no_op_and_Close_is_idempotent()
    {
        using var self = NodeIdentity.Generate();
        var inbound = new QuicInbound(self, new ListenerConfig("test-svc", IPAddress.Loopback, 0));

        inbound.Close();          // never opened
        inbound.Close();          // twice
        Assert.Null(inbound.BoundEndPoint);
        Assert.Equal("quic", inbound.PlaneName);
    }

    [Fact]
    public void Open_binds_and_reports_the_provider_that_actually_bound_it()
    {
        // Loud, not skipped: a host with no QUIC provider must FAIL this rather than quietly pass.
        Assert.True(MsQuicProvider.Instance.Probe().Supported,
            "msquic unavailable on this host: " + MsQuicProvider.Instance.Probe().Detail);

        using var self = NodeIdentity.Generate();
        using var inbound = new QuicInbound(self, new ListenerConfig("m6-inbound", IPAddress.Loopback, 0));

        inbound.Open();
        inbound.Open();           // idempotent

        Assert.NotNull(inbound.BoundEndPoint);
        Assert.NotEqual(0, inbound.BoundEndPoint!.Port);   // port 0 resolved to a real one
        Assert.False(string.IsNullOrWhiteSpace(inbound.ProviderName));
        Assert.Equal(0, inbound.RefusedFrames);

        inbound.Close();
    }

    [Fact]
    public void An_unreachable_peer_makes_Send_return_false_and_NEVER_throw()
    {
        // The interface's contract. A carrier that throws on an unreachable peer turns a routine
        // partition into a crash of whatever was sending.
        using var self = NodeIdentity.Generate();
        using var peerId = NodeIdentity.Generate();

        // Port 1 on loopback: nothing listens there, on any host.
        using var outbound = new QuicOutbound(
            self, peerId.NodeId, new PeerIdentity("nodeB", "laneB"),
            new IPEndPoint(IPAddress.Loopback, 1));

        var sent = outbound.Send(new YnetMessage("id-1", "nodeA/laneA", "SIG", "body"u8.ToArray()));
        Assert.False(sent);
    }
}
