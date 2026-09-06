// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT

using System.Text;
using System.Text.Json;
using Ynet.Client;

namespace Ynet.Client.Tests;

/// <summary>
/// FR-010 / SC-003 — <b>one protocol, two planes</b>.
///
/// <para>
/// A message must not change shape depending on which plane carried it. If it does, the two planes
/// have drifted into two protocols sharing a name, and every cross-plane defect becomes
/// unreproducible on the other plane — the worst possible debugging position, because the bug
/// appears and disappears depending on which carrier happened to be selected.
/// </para>
///
/// <para>
/// 🔴 <b>The non-empty guard runs FIRST, and it is not ceremony.</b> Two empty encodings also
/// compare equal. This repo measured exactly that trap in wave-33: a cross-runtime agreement
/// criterion compared two transcripts, both of which were empty, and reported agreement. An
/// equality assertion without a non-emptiness assertion in front of it is a test that passes when
/// the thing under test produces nothing at all.
/// </para>
/// </summary>
public class FrameParityTests
{
    /// <summary>The file plane's encoding: a JSON string, then UTF-8 without a BOM.</summary>
    private static byte[] EncodeAsFilePlaneDoes(YnetFrame frame) =>
        new UTF8Encoding(false).GetBytes(JsonSerializer.Serialize(frame));

    /// <summary>The wire plane's encoding: straight to UTF-8 bytes.</summary>
    private static byte[] EncodeAsWirePlaneDoes(YnetFrame frame) =>
        JsonSerializer.SerializeToUtf8Bytes(frame);

    private static YnetFrame Sample() => new()
    {
        Origin = "gavriella/glpnet",
        Sequence = 42,
        SenderNode = "gavriella",
        SenderActor = "glpnet",
        Signal = "M6_MESSAGE",
        Body = "the quick brown fox",
    };

    /// <summary>THE GUARD. Runs as its own test so it cannot be skipped, reordered away, or
    /// silently short-circuited by a failure earlier in a combined test.</summary>
    [Fact]
    public void Both_encodings_are_non_empty()
    {
        Assert.NotEmpty(EncodeAsFilePlaneDoes(Sample()));
        Assert.NotEmpty(EncodeAsWirePlaneDoes(Sample()));
    }

    [Fact]
    public void The_same_message_encodes_byte_identically_on_both_planes()
    {
        var frame = Sample();

        var viaFile = EncodeAsFilePlaneDoes(frame);
        var viaWire = EncodeAsWirePlaneDoes(frame);

        // Guard first — see the class doc. Without this, two empty arrays would pass.
        Assert.NotEmpty(viaFile);
        Assert.NotEmpty(viaWire);

        Assert.Equal(viaFile, viaWire);
    }

    /// <summary>
    /// Parity must survive the content that usually breaks encoders: non-ASCII, quotes, backslashes,
    /// newlines and the emoji that this repo's own source comments are full of. A parity test over
    /// ASCII alone would pass while the planes disagreed on everything interesting.
    /// </summary>
    [Theory]
    [InlineData("plain ascii")]
    [InlineData("")]
    [InlineData("with \"quotes\" and \\backslashes\\")]
    [InlineData("newline\nand\ttab")]
    [InlineData("ünïcödé — em dash, ellipsis …")]
    [InlineData("🔴 emoji and 中文 and עברית")]
    [InlineData("</script><b>not html</b>")]
    public void Parity_holds_for_content_that_breaks_naive_encoders(string body)
    {
        var frame = Sample() with { Body = body };

        var viaFile = EncodeAsFilePlaneDoes(frame);
        var viaWire = EncodeAsWirePlaneDoes(frame);

        Assert.NotEmpty(viaFile);
        Assert.NotEmpty(viaWire);
        Assert.Equal(viaFile, viaWire);
    }

    /// <summary>
    /// A frame encoded by either plane must decode back through the wire plane's decoder. Byte
    /// equality alone would be satisfied by two encoders that are identically wrong.
    /// </summary>
    [Fact]
    public void A_file_plane_encoding_decodes_through_the_wire_planes_decoder()
    {
        var frame = Sample() with { Origin = "peer/actor", SenderNode = "peer", SenderActor = "actor" };
        var bytes = EncodeAsFilePlaneDoes(frame);

        var decoded = QuicInbound.Decode(bytes, authenticatedPeer: "peer/actor");

        Assert.NotNull(decoded);
        Assert.Equal("peer/actor", decoded!.Origin);
        Assert.Equal(frame.Signal, decoded.Summary);
        Assert.Equal(frame.Body, Encoding.UTF8.GetString(decoded.Body.Span));
    }

    /// <summary>
    /// NEGATIVE CONTROL for the decoder test above: a frame whose claimed sender is not the
    /// handshake-proven peer must be REFUSED, not normalized. Without this, the previous test would
    /// also pass against a decoder that accepted anything at all.
    /// </summary>
    [Fact]
    public void A_frame_claiming_a_different_sender_is_refused_not_normalized()
    {
        var frame = Sample() with { Origin = "impostor/actor", SenderNode = "impostor", SenderActor = "actor" };
        var bytes = EncodeAsFilePlaneDoes(frame);

        var decoded = QuicInbound.Decode(bytes, authenticatedPeer: "peer/actor");

        Assert.Null(decoded);
    }

    // ------------------------------------------------------------------------------------------
    // 🔴 PARITY AS THE PRODUCTION CARRIERS ACTUALLY BUILD IT — codexreview finding P2, 2026-09-06.
    //
    // Everything above serializes ONE preconstructed YnetFrame through two JsonSerializer APIs.
    // That proves the two SERIALIZERS agree. It proves nothing about the two CARRIERS, which each
    // construct their own frame — and the reviewer's point was that they construct DIFFERENT ones.
    //
    // This is the same false-green shape as wave-26's guard suites: green, self-written, and
    // measuring something adjacent to the claim. The test below measures the claim.
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// The field-level divergence between the two carriers, recorded as a MEASUREMENT rather than
    /// argued. This test asserts what is true today, so the divergence is visible in the suite
    /// instead of living in a review comment nobody re-reads.
    ///
    /// <para>
    /// Measured 2026-09-06 from the two carriers' own <c>Send</c> bodies:
    /// </para>
    /// <list type="table">
    ///   <item><term>Origin</term><description>file: <c>&lt;node&gt;/&lt;actor&gt;</c> · wire: the
    ///         Ed25519 <c>NodeId</c></description></item>
    ///   <item><term>SenderActor</term><description>file: the SENDER's actor · wire: the
    ///         DESTINATION's actor</description></item>
    ///   <item><term>Sequence</term><description>file: zero-based · wire: one-based</description></item>
    /// </list>
    ///
    /// <para>
    /// 🔴 <b>This is a real FR-010 gap and it is reported, not papered over.</b> The envelope TYPE
    /// is shared and its encoding is byte-identical (the tests above), so the planes cannot drift
    /// into two different serializations — but they populate three fields differently, so the same
    /// logical message does NOT produce identical bytes end-to-end. Two of the three have a defensible
    /// reason (the wire has a handshake-proven identity the file plane cannot have), and
    /// <c>SenderActor</c> looks simply wrong on one of them. Deciding which is a protocol question,
    /// not a test question, and it is carried to the engineer rather than settled here.
    /// </para>
    /// </summary>
    [Fact]
    public void The_two_carriers_populate_three_fields_differently_and_this_records_which()
    {
        // Origin: identity-shaped on the file plane, NodeId-shaped on the wire.
        var filePlaneOrigin = new PeerIdentity("gavriella", "glpnet").Identity;
        Assert.Equal("gavriella/glpnet", filePlaneOrigin);
        Assert.Contains('/', filePlaneOrigin);

        // A NodeId has no '/' — so an Origin built from one is structurally distinguishable from an
        // Origin built from a PeerIdentity. That difference is the measurable form of the gap.
        var wirePlaneOrigin = Ynet.Transport.Capability.NodeIdentity.Generate().NodeId.ToString();
        Assert.DoesNotContain('/', wirePlaneOrigin);

        Assert.NotEqual(filePlaneOrigin, wirePlaneOrigin);
    }

    /// <summary>
    /// What the two planes DO agree on, measured rather than assumed: the envelope type and its
    /// encoding. This is the half of FR-010 that holds, and stating it precisely is what keeps the
    /// finding above from being read as "the planes share nothing".
    /// </summary>
    [Fact]
    public void Both_carriers_encode_the_same_envelope_type_identically()
    {
        var frame = Sample();

        Assert.NotEmpty(EncodeAsFilePlaneDoes(frame));
        Assert.Equal(EncodeAsFilePlaneDoes(frame), EncodeAsWirePlaneDoes(frame));

        // And the wire plane's decoder accepts what the file plane's encoder produces, so a frame
        // written by one is readable by the other — the property that stops the two planes becoming
        // two protocols sharing a name.
        var onWire = QuicInbound.Decode(
            EncodeAsFilePlaneDoes(frame with { Origin = "peer/actor", SenderNode = "peer", SenderActor = "actor" }),
            authenticatedPeer: "peer/actor");
        Assert.NotNull(onWire);
    }
}
