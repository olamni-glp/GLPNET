// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT
//
// ADDRESSING, THE SEND HALF, AND THE WIRE FORMAT.
//
// 🔴 THIS FILE DELIBERATELY DOES NOT RE-TEST THE INBOUND PLANE. CoopFileInboundTests.cs owns that.
//     Two lanes in this one repo independently built a cross-lane receive plane on 2026-09-05 and
//     collided on the merge; the incumbent won on merit (it refuses a partially-written frame, it
//     confines the lane directory under the coop root, it claims by move, it keeps .taken/ for
//     forensics) and the rival was withdrawn rather than kept as a fork. What survives here is only
//     what the incumbent did NOT have, contributed onto it: how a peer's directory NAME is derived,
//     how a frame BODY is written and read, the durability gate, and the send half.
//
// THE TWO INTEROP RULES BELOW WERE MEASURED, NOT READ.
//     The canonical M6 client (qhstate's YngeniOS.Ynet.Client, ruling R-B) lives in a repo this host
//     cannot read, so both rules were derived from the live COOP root and are pinned here by GOLDEN
//     VECTORS taken from it: four real peer directory names, and a real frame written by qhstate's
//     own carrier. They are falsifiable by construction - if the canonical client ever changes
//     either rule, these fail HERE rather than this lane silently addressing nobody for a day.
//
//     They have since been confirmed the only way that really counts: frames written by this lane
//     were consumed by three peer daemons on SHIRAS within seconds (2026-09-05T16:02Z).

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

    private CoopFileInbound CarrierFor(PeerIdentity who)
    {
        // No Open(): the test drives PollOnce itself, so no background pump can race it.
        var c = new CoopFileInbound(_root, who.DirectoryName);
        c.EnsureMailbox();
        return c;
    }

    // ---- rule 1: the peer directory name ------------------------------------------------------

    // Golden vectors: every one is a REAL directory observed under /d/coop on 2026-09-05. The rule
    // reproduced all 21 live directories with zero mismatches; these four span the shapes that
    // differ - a dotted actor, a hyphenated actor, and a hex node id.
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
        // A silently-accepted malformed identity produces a mailbox no peer will ever address, and
        // the client then reports "running" forever. Refuse it at the boundary.
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
        using var carrier = CarrierFor(Self);
        carrier.Received += received.Add;

        carrier.PollOnce();                       // first sighting: length not yet stable
        Assert.Equal(1, carrier.PollOnce());      // second: stable, delivered

        var msg = Assert.Single(received);
        Assert.Equal("shiras/shiras.yngcor.probe", msg.Origin);
        Assert.Contains("M6_PROOF_YNGCOR", msg.Summary);
    }

    [Fact]
    public void The_origin_comes_from_the_frame_BODY_not_from_the_file_NAME()
    {
        // Measured 2026-09-05 on the live coop root: 173 of 173 real frames carry {"Origin":...} in
        // the body and ZERO are named "[origin]--[id].frame". Reading the name returned "unknown"
        // for every frame the fleet actually sends. This golden name carries a MISLEADING "--"
        // prefix precisely so that a regression to name-parsing fails here instead of shipping.
        var inbox = Register(Self);
        File.WriteAllText(Path.Combine(inbox, "impostor--1788619622605.frame"),
            """{"Origin":"shiras/shiras.yngcor","Sequence":0,"SenderNode":"shiras","SenderActor":"shiras.yngcor","Signal":"S","Body":"b"}""");

        var received = new List<YnetMessage>();
        using var carrier = CarrierFor(Self);
        carrier.Received += received.Add;
        carrier.PollOnce();
        carrier.PollOnce();

        Assert.Equal("shiras/shiras.yngcor", Assert.Single(received).Origin);
    }

    [Fact]
    public void A_frame_written_by_this_client_is_readable_as_the_canonical_shape()
    {
        Register(Peer);
        var outbound = new CoopFileOutbound(Self, Peer, _root);
        Assert.True(outbound.Send("M6_PROOF_GLPNET", "hello"));

        var written = Directory.EnumerateFiles(CoopLayout.InboxOf(_root, Peer)).Single();
        Assert.EndsWith(".frame", written, StringComparison.Ordinal);

        // Deserialize by the wire property names only - if the serializer ever emitted camelCase or
        // omitted a field, the canonical client would read blanks and this would fail.
        using var doc = JsonDocument.Parse(File.ReadAllText(written));
        var root = doc.RootElement;
        Assert.Equal("GAVRIELLA/gavriella.glpnet", root.GetProperty("Origin").GetString());
        Assert.Equal("GAVRIELLA", root.GetProperty("SenderNode").GetString());
        Assert.Equal("gavriella.glpnet", root.GetProperty("SenderActor").GetString());
        Assert.Equal("M6_PROOF_GLPNET", root.GetProperty("Signal").GetString());
        Assert.Equal("hello", root.GetProperty("Body").GetString());
        Assert.Equal(0, root.GetProperty("Sequence").GetInt64());
    }

    [Fact]
    public void A_frame_sent_to_a_peer_is_received_by_that_peers_carrier()
    {
        Register(Peer);
        Assert.True(new CoopFileOutbound(Self, Peer, _root).Send("M6_ROUNDTRIP", "payload"));

        var received = new List<YnetMessage>();
        using var carrier = CarrierFor(Peer);
        carrier.Received += received.Add;
        carrier.PollOnce();
        carrier.PollOnce();

        Assert.Equal(Self.Identity, Assert.Single(received).Origin);
    }

    // ---- attribution cannot be spoofed --------------------------------------------------------

    [Fact]
    public void A_frame_whose_Origin_disagrees_with_its_sender_fields_is_REFUSED()
    {
        // Origin is what every consumer attributes the message to; SenderNode/SenderActor are what a
        // verifier keys on. Two fields that can disagree silently are one field too many: this exact
        // frame was delivered, and displayed, as coming from the victim.
        //
        // This is NOT authentication - see CoopFileInbound.OriginIsAuthenticated, which still
        // reports false. Anyone who can write the file chooses the body. It removes an INTERNAL
        // inconsistency, it does not establish a sender.
        var inbox = Register(Self);
        File.WriteAllText(Path.Combine(inbox, "1788600000003.0.spoof.frame"),
            """{"Origin":"victim/victim.actor","Sequence":0,"SenderNode":"attacker","SenderActor":"attacker.actor","Signal":"S","Body":"b"}""");

        var received = new List<YnetMessage>();
        using var carrier = CarrierFor(Self);
        carrier.Received += received.Add;
        carrier.PollOnce();
        carrier.PollOnce();

        Assert.Empty(received);
        Assert.True(carrier.StrayCount > 0, "an inconsistent frame must be counted as a stray");
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"Signal":"S","Body":"b"}""")]
    [InlineData("not json at all")]
    public void A_frame_that_names_no_sender_is_a_stray_not_a_message(string body)
    {
        // `{}` deserializes cleanly into a record of empty defaults. Delivering that as
        // "unknown-origin" manufactured a message out of an empty file: a WRONG ANSWER, not an
        // error, which is the failure class this codebase keeps paying for.
        var inbox = Register(Self);
        File.WriteAllText(Path.Combine(inbox, "1788600000001.0.empty.frame"), body);

        var received = new List<YnetMessage>();
        using var carrier = CarrierFor(Self);
        carrier.Received += received.Add;
        carrier.PollOnce();
        carrier.PollOnce();

        Assert.Empty(received);
    }

    [Fact]
    public void A_consistent_frame_IS_accepted()
    {
        // NEGATIVE CONTROL for the two refusals above: the checks must not simply refuse everything.
        Register(Self);
        Assert.True(new CoopFileOutbound(Peer, Self, _root).Send("M6_CONSISTENT", "x"));

        var received = new List<YnetMessage>();
        using var carrier = CarrierFor(Self);
        carrier.Received += received.Add;
        carrier.PollOnce();
        carrier.PollOnce();

        Assert.Equal(Peer.Identity, Assert.Single(received).Origin);
    }

    // ---- the durability gate: a frame is the ONLY copy until its alert is durable -------------

    [Fact]
    public void A_frame_is_RETURNED_TO_THE_INBOX_when_durability_is_not_confirmed()
    {
        // Received only ENQUEUES onto the receiver machine; the spool write happens later on another
        // thread. Consuming regardless meant a failed spool write - or a process that died in
        // between - lost the message from BOTH places, and on the bounded mailbox's OVERFLOW path
        // the loss was CERTAIN rather than racy: Post refuses, nothing is recorded under that id.
        Register(Self);
        new CoopFileOutbound(Peer, Self, _root).Send("M6_DURABLE", "payload");

        var received = new List<YnetMessage>();
        using var carrier = CarrierFor(Self);
        carrier.Received += received.Add;
        carrier.ConfirmDurable = _ => false;              // the durability owner says "not recorded"

        carrier.PollOnce();
        Assert.Equal(0, carrier.PollOnce());              // not counted as delivered
        Assert.Single(received);                          // but it WAS handed over
        Assert.Equal(1, carrier.UndurableReturned);
        Assert.Single(Directory.EnumerateFiles(carrier.InboxDirectory));   // and it is back

        // ... and the retry succeeds once durability is confirmed.
        carrier.ConfirmDurable = _ => true;
        carrier.PollOnce();
        Assert.Equal(1, carrier.PollOnce());
        Assert.Empty(Directory.EnumerateFiles(carrier.InboxDirectory));
    }

    [Fact]
    public void With_no_durability_owner_the_carrier_consumes_as_before()
    {
        // NEGATIVE CONTROL for the gate: a null ConfirmDurable must not silently stop delivery.
        // Without this, "never consume anything" would satisfy the test above.
        Register(Self);
        new CoopFileOutbound(Peer, Self, _root).Send("M6_NOGATE", "x");

        using var carrier = CarrierFor(Self);
        Assert.Null(carrier.ConfirmDurable);
        carrier.PollOnce();
        Assert.Equal(1, carrier.PollOnce());
        Assert.Equal(0, carrier.UndurableReturned);
    }

    // ---- the send half ------------------------------------------------------------------------

    [Fact]
    public void Sending_to_a_peer_with_no_registered_mailbox_returns_false_and_writes_nothing()
    {
        // NOT an exception, because IYnetOutbound's contract says a dead peer returns false. And NOT
        // a success: creating the peer's inbox on its behalf would invent a peer that never
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
        Assert.Throws<DirectoryNotFoundException>(() => new CoopFileOutbound(Self, Peer, absent));
    }
}
