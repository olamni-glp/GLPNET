using GlpRuntime.Link.Reliability;
using Xunit;

namespace GlpRuntime.Link.Tests;

/// <summary>T021 wire-format tests (FR-022): version byte, length/CRC, fragmentation, reassembly.</summary>
public class FrameCodecTests
{
    private static byte[] Seq(int n)
    {
        var b = new byte[n];
        for (int i = 0; i < n; i++) b[i] = (byte)(i * 31 + 7);
        return b;
    }

    [Fact]
    public void Crc32_KnownVector()
    {
        // The canonical CRC-32 check value for "123456789".
        var data = "123456789"u8.ToArray();
        Assert.Equal(0xCBF43926u, Crc32.Compute(data));
    }

    [Fact]
    public void Whole_RoundTrip()
    {
        var payload = Seq(200);
        var frames = FrameCodec.Encode(payload, messageId: 42);
        Assert.Single(frames);

        var parsed = FrameCodec.ParseFrame(frames[0]);
        Assert.Equal(FrameKind.Whole, parsed.Kind);
        Assert.Equal(42u, parsed.MessageId);

        var reasm = new FrameReassembler();
        var outPayload = reasm.Accept(parsed);
        Assert.Equal(payload, outPayload);
    }

    [Fact]
    public void Empty_RoundTrip()
    {
        var frames = FrameCodec.Encode(Array.Empty<byte>(), messageId: 1);
        Assert.Single(frames);
        var outPayload = new FrameReassembler().Accept(FrameCodec.ParseFrame(frames[0]));
        Assert.Equal(Array.Empty<byte>(), outPayload);
    }

    [Fact]
    public void Fragmentation_RoundTrip_AndUnderMtu()
    {
        var payload = Seq(1000);
        int mtu = 128; // each frame <= 128 bytes incl. 22-byte header
        var frames = FrameCodec.Encode(payload, messageId: 7, maxFrameBytes: mtu);

        Assert.True(frames.Count > 1);
        Assert.All(frames, f => Assert.True(f.Length <= mtu, $"frame {f.Length} > mtu {mtu}"));

        var reasm = new FrameReassembler();
        byte[]? result = null;
        foreach (var f in frames)
            result = reasm.Accept(FrameCodec.ParseFrame(f)) ?? result;
        Assert.Equal(payload, result);
    }

    [Fact]
    public void Fragmentation_OutOfOrder_Reassembles()
    {
        var payload = Seq(500);
        var frames = FrameCodec.Encode(payload, messageId: 9, maxFrameBytes: 80).ToList();
        var reasm = new FrameReassembler();

        byte[]? result = null;
        // Reverse delivery order — reorder buffer must still reconstruct in-order.
        for (int i = frames.Count - 1; i >= 0; i--)
            result = reasm.Accept(FrameCodec.ParseFrame(frames[i])) ?? result;
        Assert.Equal(payload, result);
    }

    [Fact]
    public void Fragmentation_DuplicateFragment_Ignored()
    {
        var payload = Seq(300);
        var frames = FrameCodec.Encode(payload, messageId: 3, maxFrameBytes: 64).ToList();
        var reasm = new FrameReassembler();

        byte[]? result = null;
        result = reasm.Accept(FrameCodec.ParseFrame(frames[0])) ?? result;
        result = reasm.Accept(FrameCodec.ParseFrame(frames[0])) ?? result; // duplicate
        for (int i = 1; i < frames.Count; i++)
            result = reasm.Accept(FrameCodec.ParseFrame(frames[i])) ?? result;
        Assert.Equal(payload, result);
    }

    [Fact]
    public void BadVersion_Rejected()
    {
        var frame = FrameCodec.Encode(Seq(10), 1)[0];
        frame[0] = 0x99;
        var ex = Assert.Throws<FrameException>(() => FrameCodec.ParseFrame(frame));
        Assert.Contains("version", ex.Message);
    }

    [Fact]
    public void BadCrc_Rejected()
    {
        var frame = FrameCodec.Encode(Seq(10), 1)[0];
        frame[^1] ^= 0xFF; // corrupt last payload byte
        Assert.Throws<FrameException>(() => FrameCodec.ParseFrame(frame));
    }

    [Fact]
    public void TruncatedHeader_Rejected()
    {
        Assert.Throws<FrameException>(() => FrameCodec.ParseFrame(new byte[5]));
    }

    [Fact]
    public void MtuSmallerThanHeader_Rejected()
    {
        Assert.Throws<FrameException>(() => FrameCodec.Encode(Seq(100), 1, maxFrameBytes: FrameCodec.HeaderSize - 1));
    }

    [Fact]
    public void Reassembler_MetadataMismatch_Rejected()
    {
        var a = FrameCodec.Encode(Seq(300), messageId: 5, maxFrameBytes: 64).ToList();
        var reasm = new FrameReassembler();
        reasm.Accept(FrameCodec.ParseFrame(a[0]));

        // A forged fragment for the same message id whose (total,count) differs.
        var forged = FrameCodec.Encode(Seq(100), messageId: 5, maxFrameBytes: 64)[0];
        Assert.Throws<FrameException>(() => reasm.Accept(FrameCodec.ParseFrame(forged)));
    }

    [Fact]
    public void Reassembler_TooManyInFlight_Rejected()
    {
        var reasm = new FrameReassembler(maxInFlightMessages: 2);
        // open 2 partials (one fragment each, leave incomplete)
        for (uint id = 0; id < 2; id++)
            reasm.Accept(FrameCodec.ParseFrame(FrameCodec.Encode(Seq(200), id, maxFrameBytes: 64)[0]));
        Assert.Equal(2, reasm.InFlightCount);
        var third = FrameCodec.Encode(Seq(200), 99, maxFrameBytes: 64)[0];
        Assert.Throws<FrameException>(() => reasm.Accept(FrameCodec.ParseFrame(third)));
    }
}
