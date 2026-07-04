// SC-002 — loud-fail decode: 0% silent acceptance (feature 041-crdtmsg-mvp, T011).
//
// Contract C3 / FR-005: bad version, unknown payload_type, unknown must-understand tag, truncation,
// trailing bytes, out-of-range schema version all reject with a CrdtMsgException. Extends the 038
// LoudFailFuzz discipline to the messaging envelope.

using GlpRuntime.CrdtMsg.Envelope;
using GlpRuntime.CrdtMsg.Model;
using GlpRuntime.WireRegistry;

namespace GlpRuntime.CrdtMsg.Tests;

public sealed class LoudFailTests
{
    private static Message Valid() => SampleMessages.All().Single(t => t.name == "rich").msg;

    [Fact]
    public void Binary_bad_codec_version_rejects()
    {
        byte[] b = MessageCodec.Binary.Encode(Valid());
        b[0] = 0x02; // codec format version byte (TIER-2 hard gate)
        Assert.Throws<CrdtMsgException>(() => MessageCodec.Binary.Decode(b));
    }

    [Fact]
    public void Binary_trailing_bytes_reject()
    {
        byte[] b = MessageCodec.Binary.Encode(Valid());
        var withTrailer = b.Append((byte)0x00).ToArray();
        Assert.Throws<CrdtMsgException>(() => MessageCodec.Binary.Decode(withTrailer));
    }

    [Fact]
    public void Binary_truncation_rejects()
    {
        byte[] b = MessageCodec.Binary.Encode(Valid());
        byte[] truncated = b.Take(b.Length - 3).ToArray();
        Assert.Throws<CrdtMsgException>(() => MessageCodec.Binary.Decode(truncated));
    }

    [Fact]
    public void Json_trailing_content_rejects()
    {
        byte[] b = MessageCodec.Json.Encode(Valid());
        var withTrailer = b.Append((byte)0x7B).ToArray(); // '{'
        Assert.Throws<CrdtMsgException>(() => MessageCodec.Json.Decode(withTrailer));
    }

    [Fact]
    public void Cbor_trailing_content_rejects()
    {
        byte[] b = MessageCodec.Cbor.Encode(Valid());
        var withTrailer = b.Append((byte)0x00).ToArray();
        Assert.Throws<CrdtMsgException>(() => MessageCodec.Cbor.Decode(withTrailer));
    }

    [Theory]
    [InlineData("binary-term")]
    [InlineData("json")]
    [InlineData("yaml")]
    [InlineData("cbor")]
    public void Unknown_payload_type_rejects(string surfaceName)
    {
        var surface = MessageCodec.Surfaces.Single(s => s.Name == surfaceName);
        var m = Valid() with { PayloadType = 0x99 }; // not in the wire registry
        byte[] b = surface.Encode(m);
        Assert.Throws<CrdtMsgException>(() => surface.Decode(b));
    }

    [Theory]
    [InlineData("binary-term")]
    [InlineData("json")]
    [InlineData("cbor")]
    public void Out_of_range_schema_version_rejects(string surfaceName)
    {
        var surface = MessageCodec.Surfaces.Single(s => s.Name == surfaceName);
        var m = Valid() with { SchemaVersion = 99 }; // outside [1,2]
        byte[] b = surface.Encode(m);
        Assert.Throws<CrdtMsgException>(() => surface.Decode(b));
    }

    [Fact]
    public void Unknown_must_understand_section_rejects()
    {
        // 0x13 is odd ⇒ must-understand; not in the understood set ⇒ loud-fail via the facade.
        var m = new Message(
            VersionPolicy.EmitSchemaVersion, PayloadType.CrdtMessage,
            new Header("m", "a", "b", 0, RoutingPolicy.Empty),
            new[] { new Section(0x13, new byte[] { 1 }) },
            CrdtModel.OpBased);

        var understood = new HashSet<long>(); // understands nothing
        foreach (var surface in MessageCodec.Surfaces)
        {
            byte[] b = surface.Encode(m);
            Assert.Throws<CrdtMsgException>(() => MessageCodec.Decode(surface, b, understood));
        }
    }

    [Fact]
    public void Must_understand_section_that_is_understood_passes()
    {
        var m = new Message(
            VersionPolicy.EmitSchemaVersion, PayloadType.CrdtMessage,
            new Header("m", "a", "b", 0, RoutingPolicy.Empty),
            new[] { new Section(0x13, new byte[] { 1 }) },
            CrdtModel.OpBased);

        var understood = new HashSet<long> { 0x13 };
        foreach (var surface in MessageCodec.Surfaces)
        {
            byte[] b = surface.Encode(m);
            Message decoded = MessageCodec.Decode(surface, b, understood);
            Assert.Single(decoded.Sections);
        }
    }
}
