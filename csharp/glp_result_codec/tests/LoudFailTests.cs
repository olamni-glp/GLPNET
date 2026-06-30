// SC-004 / FR-005 — loud-fail rules (contract §5). Every malformed input MUST be
// rejected with ResultCodecException; zero silent acceptances. Covers: trailing
// bytes, truncation, bad version, bad payloadType (incl. the 029 IL value 0x10),
// bad status, bad errorPresent, unknown term tag, 029-reserved null/bool tags, and
// an over-64-bit varint.

using GlpRuntime.ResultCodec;

namespace GlpRuntime.ResultCodec.Tests;

public class LoudFailTests
{
    // A minimal, valid empty-success envelope:
    //   01 version | 11 payloadType | 00 status
    //   00 bindings | 00 varToWriter | 00 suspended | 00 capturedLen | 00 errorPresent
    private static byte[] MinimalValid() => new byte[]
        { 0x01, 0x11, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };

    [Fact]
    public void Minimal_valid_envelope_decodes()
    {
        var env = ResultEnvelopeCodec.Decode(MinimalValid());
        Assert.Equal(ExecutionStatus.Success, env.Status);
        Assert.Empty(env.ResolvedBindings);
    }

    [Fact]
    public void Trailing_bytes_are_rejected()
    {
        var bytes = ResultEnvelopeCodec.Encode(Corpus.ByName("success_int"));
        var withTrailer = new byte[bytes.Length + 1];
        bytes.CopyTo(withTrailer, 0);
        withTrailer[^1] = 0x00; // one extra byte after a complete envelope
        Assert.Throws<ResultCodecException>(() => ResultEnvelopeCodec.Decode(withTrailer));
    }

    [Fact]
    public void Truncated_input_is_rejected()
    {
        var bytes = ResultEnvelopeCodec.Encode(Corpus.ByName("success_nested_struct"));
        var truncated = bytes[..^1]; // drop the last byte
        Assert.Throws<ResultCodecException>(() => ResultEnvelopeCodec.Decode(truncated));
    }

    [Fact]
    public void Empty_input_is_rejected()
    {
        Assert.Throws<ResultCodecException>(() => ResultEnvelopeCodec.Decode(System.Array.Empty<byte>()));
    }

    [Fact]
    public void Bad_version_is_rejected()
    {
        var bytes = MinimalValid();
        bytes[0] = 0x02; // not 0x01
        Assert.Throws<ResultCodecException>(() => ResultEnvelopeCodec.Decode(bytes));
    }

    [Fact]
    public void Bad_payload_type_is_rejected()
    {
        var bytes = MinimalValid();
        bytes[1] = 0x99; // not 0x11
        Assert.Throws<ResultCodecException>(() => ResultEnvelopeCodec.Decode(bytes));
    }

    [Fact]
    public void IL_program_payload_type_is_rejected()
    {
        // 0x10 is the 029 IL_PROGRAM payload type — must NOT be mistaken for an envelope.
        var bytes = MinimalValid();
        bytes[1] = 0x10;
        Assert.Throws<ResultCodecException>(() => ResultEnvelopeCodec.Decode(bytes));
    }

    [Fact]
    public void Bad_status_is_rejected()
    {
        var bytes = MinimalValid();
        bytes[2] = 0x03; // status not in {0,1,2}
        Assert.Throws<ResultCodecException>(() => ResultEnvelopeCodec.Decode(bytes));
    }

    [Fact]
    public void Bad_error_present_flag_is_rejected()
    {
        var bytes = MinimalValid();
        bytes[^1] = 0x02; // errorPresent not in {0,1}
        Assert.Throws<ResultCodecException>(() => ResultEnvelopeCodec.Decode(bytes));
    }

    [Fact]
    public void Unknown_term_tag_is_rejected()
    {
        // version, payloadType, status, bindingsCount=1, name "X" (len 1), term tag 0x09 (unknown)
        var bytes = new byte[] { 0x01, 0x11, 0x00, 0x01, 0x01, (byte)'X', 0x09 };
        Assert.Throws<ResultCodecException>(() => ResultEnvelopeCodec.Decode(bytes));
    }

    [Theory]
    [InlineData((byte)0x00)] // 029-reserved null
    [InlineData((byte)0x01)] // 029-reserved bool
    public void Reserved_null_or_bool_term_tag_is_rejected(byte reservedTag)
    {
        var bytes = new byte[] { 0x01, 0x11, 0x00, 0x01, 0x01, (byte)'X', reservedTag };
        Assert.Throws<ResultCodecException>(() => ResultEnvelopeCodec.Decode(bytes));
    }

    [Fact]
    public void Varint_over_64_bits_is_rejected()
    {
        // bindingsCount varint = ten 0x80 continuation bytes -> shift passes 64 bits.
        var bytes = new byte[]
        {
            0x01, 0x11, 0x00,
            0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80,
        };
        Assert.Throws<ResultCodecException>(() => ResultEnvelopeCodec.Decode(bytes));
    }
}
