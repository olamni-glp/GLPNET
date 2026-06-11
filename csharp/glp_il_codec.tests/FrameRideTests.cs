using GlpRuntime.Link.Reliability;

namespace GlpRuntime.IlCodec.Tests;

/// <summary>
/// A4 — the IL payload rides the existing <c>GlpLink</c> <see cref="FrameCodec"/> as
/// an opaque blob, both as a single <see cref="FrameKind.Whole"/> frame and split
/// into <see cref="FrameKind.Fragment"/> frames, surviving reassembly byte-for-byte
/// and decoding back to the same program. The transport <see cref="FrameKind"/> enum
/// is NOT extended — the payload-type discriminant lives inside the IL payload
/// header, not in a new frame kind (seed T2 option 3).
/// </summary>
[Trait("Category", "FrameRide")]
public class FrameRideTests
{
    private static byte[] SamplePayload()
    {
        var c = Corpus.ByName("all_opcodes");
        return IlCodec.Encode(c.Program);
    }

    [Fact]
    public void Payload_rides_as_a_single_whole_frame()
    {
        var payload = SamplePayload();

        var frames = FrameCodec.Encode(payload, messageId: 0x2929);
        Assert.Single(frames);
        var parsed = FrameCodec.ParseFrame(frames[0]);
        Assert.Equal(FrameKind.Whole, parsed.Kind);

        var reassembled = new FrameReassembler().Accept(FrameCodec.ParseFrame(frames[0]));
        Assert.NotNull(reassembled);
        Assert.Equal(payload, reassembled);

        // And the IL payload still decodes to the same program.
        var original = Corpus.ByName("all_opcodes").Program;
        ProgramEquality.AssertStructurallyEqual(original, IlCodec.Decode(reassembled!).Program);
    }

    [Fact]
    public void Payload_rides_split_into_fragments_and_reassembles()
    {
        var payload = SamplePayload();

        var frames = FrameCodec.Encode(payload, messageId: 0x2930, maxFrameBytes: FrameCodec.HeaderSize + 16);
        Assert.True(frames.Count > 1, "payload should fragment under a small MTU");

        var reasm = new FrameReassembler();
        byte[]? reassembled = null;
        foreach (var f in frames)
        {
            var parsed = FrameCodec.ParseFrame(f);
            Assert.Equal(FrameKind.Fragment, parsed.Kind);
            reassembled = reasm.Accept(parsed) ?? reassembled;
        }

        Assert.NotNull(reassembled);
        Assert.Equal(payload, reassembled);

        var original = Corpus.ByName("all_opcodes").Program;
        ProgramEquality.AssertStructurallyEqual(original, IlCodec.Decode(reassembled!).Program);
    }

    [Fact]
    public void FrameKind_enum_is_unchanged()
    {
        // A4: exactly two kinds, with their established byte values — no new FrameKind.
        var values = Enum.GetValues<FrameKind>();
        Assert.Equal(2, values.Length);
        Assert.Equal((byte)0, (byte)FrameKind.Whole);
        Assert.Equal((byte)1, (byte)FrameKind.Fragment);
    }
}
