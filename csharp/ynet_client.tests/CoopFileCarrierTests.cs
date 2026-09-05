// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT
//
// These tests pin the two INTEROP rules that let this lane's client exchange frames with the
// canonical M6 client (qhstate's YngeniOS.Ynet.Client, engineer ruling R-B 2026-09-05T15:10Z).
//
// Both rules were derived by MEASUREMENT against the live COOP root, not read out of the canonical
// source — which lives in another repo and was not readable from this host. That makes them
// falsifiable rather than assumed: the golden vectors below are real peer directory names and a
// real frame body copied from /d/coop, so if the canonical client ever changes either rule, these
// tests fail HERE, loudly, instead of this lane silently addressing nobody for a day.
//
// The negative controls matter as much as the positive ones. A carrier that "finds no strays" is
// only meaningful if it can be shown to find one; a send that "succeeds" is only meaningful if it
// can be shown to refuse an unregistered peer.

using System.Text;
using System.Text.Json;

namespace Ynet.Client.Tests;

public sealed class CoopFileCarrierTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "ynet_coop_tests", Guid.NewGuid().ToString("N"));

    public CoopFileCarrierTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
        catch (IOException) { /* a leftover temp dir is not a test failure */ }
    }

    private static readonly PeerIdentity Self = new("GAVRIELLA", "gavriella.glpnet");
    private static readonly PeerIdentity Peer = new("GAVRIELLA", "gavriella.peer");

    private string Register(PeerIdentity who)
    {
        var inbox = CoopLayout.InboxOf(_root, who);
        Directory.CreateDirectory(inbox);
        return inbox;
    }

    // ---- rule 1: the peer directory name ------------------------------------------------------

    // Golden vectors: every one of these is a REAL directory observed under /d/coop on 2026-09-05.
    // The rule reproduced all 21 live directories with zero mismatches; these four span the shapes
    // that differ — a dotted actor, a hyphenated actor, and a hex node id.
    [Theory]
    [InlineData("GAVRIELLA/gavriella.buildkit", "GAVRIELLA%2Fgavriella%2Ebuildkit~90f7084bfaa5")]
    [InlineData("shiras/shiras.yngcor", "shiras%2Fshiras%2Eyngcor~cd0f5eada92a")]
    [InlineData("shiras/shiras-glpnet", "shiras%2Fshiras-glpnet~83f24280ce41")]
    [InlineData("1b23876b/shiras.qhstate", "1b23876b%2Fshiras%2Eqhstate~45238f32916b")]
    public void The_peer_directory_name_matches_the_live_fleet(string identity, string expected)
    {
        Assert.Equal(expected, PeerIdentity.Parse(identity).DirectoryName);
    }

    [Fact]
    public void An_identity_without_a_node_and_actor_is_refused_not_half_accepted()
    {
        // A silently-accepted malformed identity produces a mailbox no peer will ever address,
        // and the client then reports "running" forever. Refuse it at the boundary.
        Assert.Throws<FormatException>(() => PeerIdentity.Parse("no-slash-here"));
        Assert.Throws<FormatException>(() => PeerIdentity.Parse("/leading"));
        Assert.Throws<FormatException>(() => PeerIdentity.Parse("trailing/"));
    }

    // ---- rule 2: the frame wire format --------------------------------------------------------

    [Fact]
    public void A_frame_written_by_the_canonical_client_is_read_by_this_one()
    {
        // Copied byte-for-byte from a live frame written by qhstate's carrier:
        //   /d/coop/shiras%2Fshiras%2Eyngcor~cd0f5eada92a/processed/1788619622605.0.04ce...frame
        const string golden =
            """{"Origin":"shiras/shiras.yngcor.probe","Sequence":2,"SenderNode":"shiras","SenderActor":"shiras.yngcor.probe","Signal":"M6_PROOF_YNGCOR","Body":"probe"}""";

        var inbox = Register(Self);
        File.WriteAllText(Path.Combine(inbox, "1788619622605.0.deadbeef.frame"), golden);

        var received = new List<YnetMessage>();
        using var carrier = CoopFileInbound.Manual(Self, _root);
        carrier.Received += received.Add;
        carrier.Open();

        Assert.Equal(1, carrier.PollOnce());
        var msg = Assert.Single(received);
        Assert.Equal("shiras/shiras.yngcor.probe", msg.Origin);
        Assert.Contains("M6_PROOF_YNGCOR", msg.Summary);
        Assert.Equal("probe", Encoding.UTF8.GetString(msg.Body.Span));
        Assert.Empty(carrier.StrayFiles);
    }

    [Fact]
    public void A_frame_written_by_this_client_is_readable_as_the_canonical_shape()
    {
        Register(Peer);
        var outbound = new CoopFileOutbound(Self, Peer, _root);
        Assert.True(outbound.Send("M6_PROOF_GLPNET", "hello"));

        var written = Directory.EnumerateFiles(CoopLayout.InboxOf(_root, Peer)).Single();
        Assert.EndsWith(".frame", written, StringComparison.Ordinal);

        // Deserialize with the wire property names only — if the serializer ever emitted camelCase
        // or omitted a field, the canonical client would read blanks and this would fail.
        using var doc = JsonDocument.Parse(File.ReadAllText(written));
        var root = doc.RootElement;
        Assert.Equal("GAVRIELLA/gavriella.glpnet", root.GetProperty("Origin").GetString());
        Assert.Equal("GAVRIELLA", root.GetProperty("SenderNode").GetString());
        Assert.Equal("gavriella.glpnet", root.GetProperty("SenderActor").GetString());
        Assert.Equal("M6_PROOF_GLPNET", root.GetProperty("Signal").GetString());
        Assert.Equal("hello", root.GetProperty("Body").GetString());
        Assert.Equal(0, root.GetProperty("Sequence").GetInt64());
    }

    // ---- the round trip: send here, receive there, no agent anywhere --------------------------

    [Fact]
    public void A_frame_sent_to_a_peer_is_received_by_that_peers_carrier()
    {
        Register(Peer);
        Assert.True(new CoopFileOutbound(Self, Peer, _root).Send("M6_ROUNDTRIP", "payload"));

        var received = new List<YnetMessage>();
        using var carrier = CoopFileInbound.Manual(Peer, _root);
        carrier.Received += received.Add;
        carrier.Open();
        carrier.PollOnce();

        var msg = Assert.Single(received);
        Assert.Equal(Self.Identity, msg.Origin);
        Assert.Equal("payload", Encoding.UTF8.GetString(msg.Body.Span));
    }

    [Fact]
    public void A_delivered_frame_is_moved_to_processed_so_a_restart_does_not_redeliver_it()
    {
        Register(Peer);
        new CoopFileOutbound(Self, Peer, _root).Send("M6_ONCE", "x");

        var first = new List<YnetMessage>();
        using (var carrier = CoopFileInbound.Manual(Peer, _root))
        {
            carrier.Received += first.Add;
            carrier.Open();
            carrier.PollOnce();
        }

        // A SECOND carrier — a restart — must not see it again.
        var second = new List<YnetMessage>();
        using var restarted = CoopFileInbound.Manual(Peer, _root);
        restarted.Received += second.Add;
        restarted.Open();
        restarted.PollOnce();

        Assert.Single(first);
        Assert.Empty(second);
        Assert.Single(Directory.EnumerateFiles(CoopLayout.ProcessedOf(_root, Peer)));
    }

    // ---- A3: a stray file is LOUD, not silent -------------------------------------------------

    [Fact]
    public void A_non_frame_file_in_the_inbox_is_counted_and_NAMED()
    {
        // The defect this pins, measured by shiras-glpnet on 2026-09-05: enumerating "*.frame" only
        // means a mis-addressed document is not refused, it is NOT SEEN — frames_refused stayed 0
        // while two ACK-MANDATORY broadcasts sat unread in a lane's mailbox.
        var inbox = Register(Self);
        File.WriteAllText(Path.Combine(inbox, "BROADCAST-ACK-MANDATORY.md"), "# a document, not a frame");
        new CoopFileOutbound(Peer, Self, _root).Send("M6_REAL", "real");

        var received = new List<YnetMessage>();
        using var carrier = CoopFileInbound.Manual(Self, _root);
        carrier.Received += received.Add;
        carrier.Open();

        Assert.Equal(1, carrier.PollOnce());                       // the frame still gets through
        Assert.Single(received);
        Assert.Equal(new[] { "BROADCAST-ACK-MANDATORY.md" }, carrier.StrayFiles);
        // and it is left in place rather than consumed: it belongs to whoever mis-addressed it.
        Assert.True(File.Exists(Path.Combine(inbox, "BROADCAST-ACK-MANDATORY.md")));
    }

    [Fact]
    public void A_frame_file_that_is_not_valid_json_is_a_stray_not_a_crash()
    {
        var inbox = Register(Self);
        File.WriteAllText(Path.Combine(inbox, "1788600000000.0.corrupt.frame"), "{not json");

        using var carrier = CoopFileInbound.Manual(Self, _root);
        carrier.Open();

        Assert.Equal(0, carrier.PollOnce());
        Assert.Equal(new[] { "1788600000000.0.corrupt.frame" }, carrier.StrayFiles);
    }

    // ---- negative controls: the refusals must actually refuse ---------------------------------

    [Fact]
    public void Sending_to_a_peer_with_no_registered_mailbox_returns_false_and_writes_nothing()
    {
        // NOT an exception, because IYnetOutbound's contract says a dead peer returns false. And
        // NOT a success: creating the peer's inbox on its behalf would invent a peer that never
        // announced itself, and every later send would report success into a directory nobody reads.
        var outbound = new CoopFileOutbound(Self, Peer, _root);
        Assert.False(outbound.PeerIsReachable);
        Assert.False(outbound.Send("M6_LOST", "x"));
        Assert.Null(outbound.LastFrameName);
        Assert.False(Directory.Exists(CoopLayout.InboxOf(_root, Peer)));
    }

    [Fact]
    public void A_missing_coop_root_is_refused_rather_than_guessed()
    {
        var absent = Path.Combine(_root, "does-not-exist");
        Assert.Throws<DirectoryNotFoundException>(() => new CoopFileInbound(Self, absent));
    }

    [Fact]
    public void Open_and_Close_are_idempotent_as_the_interface_promises()
    {
        Register(Self);
        using var carrier = CoopFileInbound.Manual(Self, _root);
        carrier.Open();
        carrier.Open();     // must not start a second pump thread
        carrier.Close();
        carrier.Close();    // must not throw on an already-closed carrier
    }
}
