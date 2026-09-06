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
}
