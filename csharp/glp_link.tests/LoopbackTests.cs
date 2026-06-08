using GlpRuntime.Link.Reliability;
using GlpRuntime.Link.Seam;
using GlpRuntime.Link.Transports;
using Xunit;

namespace GlpRuntime.Link.Tests;

/// <summary>T026 deterministic loopback transport tests (FR-002/004/018/020).</summary>
public class LoopbackTests
{
    private static async Task<(ILinkEndpoint a, ILinkEndpoint b)> PairAsync(LoopbackTransport t, string chan)
    {
        var listen = t.ListenAsync(LinkScheme.Loopback, LinkAddress.Path(chan), LinkOptions.Default);
        var b = await t.ConnectAsync(LinkScheme.Loopback, LinkAddress.Path(chan), LinkOptions.Default);
        var a = await listen;
        return (a, b);
    }

    [Fact]
    public async Task Pairing_YieldsEquivalentLinkId()
    {
        var t = new LoopbackTransport();
        var (a, b) = await PairAsync(t, "c1");
        Assert.Equal(a.Id, b.Id);             // both ends share the link identity (FR-002)
        Assert.Equal(LinkScheme.Loopback, a.Id.Scheme);
        Assert.Equal(0, t.PendingCount);
    }

    [Fact]
    public async Task ConnectorFirst_AlsoPairs()
    {
        // Role is independent of arrival order (FR-004): connector parks first.
        var t = new LoopbackTransport();
        var connect = t.ConnectAsync(LinkScheme.Loopback, LinkAddress.Path("c2"), LinkOptions.Default);
        var a = await t.ListenAsync(LinkScheme.Loopback, LinkAddress.Path("c2"), LinkOptions.Default);
        var b = await connect;
        Assert.Equal(a.Id, b.Id);
    }

    [Fact]
    public async Task RoundTrip_FifoExactlyOnce()
    {
        var t = new LoopbackTransport();
        var (a, b) = await PairAsync(t, "c3");

        await a.SendBytesAsync(new byte[] { 1, 2, 3 });
        await a.SendBytesAsync(new byte[] { 4, 5 });
        await a.SendBytesAsync(Array.Empty<byte>());

        Assert.Equal(new byte[] { 1, 2, 3 }, await b.RecvBytesAsync());
        Assert.Equal(new byte[] { 4, 5 }, await b.RecvBytesAsync());
        Assert.Equal(Array.Empty<byte>(), await b.RecvBytesAsync());
    }

    [Fact]
    public async Task Bidirectional()
    {
        var t = new LoopbackTransport();
        var (a, b) = await PairAsync(t, "c4");
        await a.SendBytesAsync(new byte[] { 10 });
        await b.SendBytesAsync(new byte[] { 20 });
        Assert.Equal(new byte[] { 10 }, await b.RecvBytesAsync());
        Assert.Equal(new byte[] { 20 }, await a.RecvBytesAsync());
    }

    [Fact]
    public async Task GracefulClose_DrainsThenReturnsNull()
    {
        var t = new LoopbackTransport();
        var (a, b) = await PairAsync(t, "c5");
        await a.SendBytesAsync(new byte[] { 9 });
        await a.CloseAsync();

        Assert.Equal(new byte[] { 9 }, await b.RecvBytesAsync()); // buffered frame still drains
        Assert.Null(await b.RecvBytesAsync());                    // then graceful eos
    }

    [Fact]
    public async Task PendingRendezvous_Cancellable()
    {
        var t = new LoopbackTransport();
        using var cts = new CancellationTokenSource();
        var pending = t.ListenAsync(LinkScheme.Loopback, LinkAddress.Path("c6"), LinkOptions.Default, cts.Token);
        Assert.Equal(1, t.PendingCount);
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Equal(0, t.PendingCount);
    }

    [Fact]
    public void WrongScheme_Rejected()
    {
        var t = new LoopbackTransport();
        // Scheme is validated synchronously (argument check), so this throws before
        // a Task is produced; discard the result to bind the Action overload.
        Assert.Throws<ArgumentException>(() =>
            { _ = t.ConnectAsync(LinkScheme.Ws, LinkAddress.Path("c7"), LinkOptions.Default); });
    }

    [Fact]
    public async Task FullStack_OverLoopback_ExactlyOnceInOrder()
    {
        // Window + sequencer + frame codec on the send side; reassembler + ordering
        // on the receive side; over a real ILinkTransport. Proves the whole Phase-2
        // reliability stack composes.
        var t = new LoopbackTransport();
        var (sender, receiver) = await PairAsync(t, "stack");

        var payloads = new[] { "one"u8.ToArray(), "two"u8.ToArray(), "three"u8.ToArray(), "four"u8.ToArray() };
        var seq = new LinkSequencer();
        using var window = new SendWindow(2); // small window forces backpressure accounting

        foreach (var p in payloads)
        {
            await window.AcquireAsync();
            uint id = seq.Next();
            foreach (var frame in FrameCodec.Encode(p, id, maxFrameBytes: FrameCodec.HeaderSize + 2)) // small MTU → fragmentation
                await sender.SendBytesAsync(frame);
            window.Release(); // loopback is in-order reliable: a frame is "acked" once enqueued
        }
        await sender.CloseAsync();

        var reasm = new FrameReassembler();
        var ord = new InboundOrdering();
        var got = new List<byte[]>();
        byte[]? frameBytes;
        while ((frameBytes = await receiver.RecvBytesAsync()) is not null)
        {
            var parsed = FrameCodec.ParseFrame(frameBytes);
            var payload = reasm.Accept(parsed);
            if (payload is not null)
                got.AddRange(ord.Accept(parsed.MessageId, payload));
        }

        Assert.Equal(payloads, got);
    }
}
