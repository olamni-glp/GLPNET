using GlpRuntime.Link.Reliability;
using Xunit;

namespace GlpRuntime.Link.Tests;

/// <summary>T022 sequence/dedup + FIFO + reorder tests (FR-020/021/023/053).</summary>
public class OrderingTests
{
    private static byte[] P(int tag) => new[] { (byte)tag };

    [Fact]
    public void Sequencer_IsMonotone()
    {
        var s = new LinkSequencer();
        Assert.Equal(0u, s.Next());
        Assert.Equal(1u, s.Next());
        Assert.Equal(2u, s.Next());
        Assert.Equal(3u, s.Peek);
    }

    [Fact]
    public void InOrder_ReleasedImmediately()
    {
        var ord = new InboundOrdering();
        Assert.Equal(new[] { P(0) }, ord.Accept(0, P(0)));
        Assert.Equal(new[] { P(1) }, ord.Accept(1, P(1)));
        Assert.Equal(new[] { P(2) }, ord.Accept(2, P(2)));
    }

    [Fact]
    public void OutOfOrder_BufferedThenDrainedInOrder()
    {
        var ord = new InboundOrdering();
        Assert.Empty(ord.Accept(2, P(2)));  // future → buffered
        Assert.Empty(ord.Accept(1, P(1)));  // future → buffered
        Assert.Equal(2, ord.BufferedCount);

        var released = ord.Accept(0, P(0)); // gap fills → drain 0,1,2 in order
        Assert.Equal(new[] { P(0), P(1), P(2) }, released);
        Assert.Equal(0, ord.BufferedCount);
        Assert.Equal(3u, ord.NextExpected);
    }

    [Fact]
    public void Duplicate_OldFrame_IsNoOp()
    {
        var ord = new InboundOrdering();
        ord.Accept(0, P(0));
        ord.Accept(1, P(1));
        Assert.Empty(ord.Accept(0, P(0))); // redelivered old → idempotent no-op
        Assert.Empty(ord.Accept(1, P(1)));
        Assert.Equal(2u, ord.NextExpected);
    }

    [Fact]
    public void Duplicate_BufferedFuture_IsNoOp()
    {
        var ord = new InboundOrdering();
        Assert.Empty(ord.Accept(5, P(5)));
        Assert.Empty(ord.Accept(5, P(5))); // duplicate future → no-op, not double-buffered
        Assert.Equal(1, ord.BufferedCount);
    }

    [Fact]
    public void ReorderBuffer_Bounded()
    {
        var ord = new InboundOrdering(start: 0, maxBufferedFrames: 3);
        ord.Accept(1, P(1));
        ord.Accept(2, P(2));
        ord.Accept(3, P(3));
        Assert.Throws<FrameException>(() => ord.Accept(4, P(4))); // exceeds bound while seq 0 missing
    }

    [Fact]
    public void EndToEnd_FrameCodec_Reorder_Dedup()
    {
        // Simulate an at-least-once, reordering transport over the full T021+T022 stack.
        var seq = new LinkSequencer();
        var payloads = new[] { "alpha"u8.ToArray(), "beta"u8.ToArray(), "gamma"u8.ToArray() };
        var wire = new List<(uint seq, byte[] frame)>();
        foreach (var p in payloads)
        {
            uint id = seq.Next();
            wire.Add((id, FrameCodec.Encode(p, id)[0]));
        }

        var reasm = new FrameReassembler();
        var ord = new InboundOrdering();
        var delivered = new List<byte[]>();

        // Deliver reordered (2,0,1) and duplicate frame 0.
        foreach (var i in new[] { 2, 0, 0, 1 })
        {
            var parsed = FrameCodec.ParseFrame(wire[i].frame);
            var payload = reasm.Accept(parsed);
            if (payload is null) continue;
            delivered.AddRange(ord.Accept(parsed.MessageId, payload));
        }

        Assert.Equal(payloads, delivered); // exactly-once, in original order
    }
}
