using System.Threading.Channels;
using GlpRuntime.Link.Seam;

namespace GlpRuntime.Link.Transports;

/// <summary>
/// One end of an in-process loopback link (T026). Writes go to the peer's inbound
/// channel; reads drain this end's inbound channel. FIFO, in-order, exactly-once by
/// construction (a <see cref="Channel{T}"/> preserves order and never drops or
/// duplicates), so it is the deterministic substrate for hermetic tests. Fault
/// behaviour (drop/reorder/duplicate/delay) is added by a T052 decorator wrapping
/// this <see cref="ILinkEndpoint"/> — the loopback itself never deviates.
/// </summary>
internal sealed class LoopbackEndpoint : ILinkEndpoint
{
    private readonly ChannelWriter<byte[]> _toPeer;
    private readonly ChannelReader<byte[]> _fromPeer;

    public LinkId Id { get; }

    public event Action<LinkFaultSignal>? OnFault;

    internal LoopbackEndpoint(LinkId id, ChannelWriter<byte[]> toPeer, ChannelReader<byte[]> fromPeer)
    {
        Id = id;
        _toPeer = toPeer;
        _fromPeer = fromPeer;
    }

    public async Task SendBytesAsync(ReadOnlyMemory<byte> frame, CancellationToken ct = default)
    {
        // Copy: the caller may reuse its buffer once the await returns.
        var copy = frame.ToArray();
        try
        {
            await _toPeer.WriteAsync(copy, ct).ConfigureAwait(false);
        }
        catch (ChannelClosedException)
        {
            // Peer closed its receive side. Report as a clean close, not a Fail.
            OnFault?.Invoke(new LinkFaultSignal(Id, LinkFaultKind.Closed, "peer closed"));
            throw;
        }
    }

    public async Task<byte[]?> RecvBytesAsync(CancellationToken ct = default)
    {
        try
        {
            return await _fromPeer.ReadAsync(ct).ConfigureAwait(false);
        }
        catch (ChannelClosedException)
        {
            // Peer completed the stream: graceful end-of-stream → null (closed/eos upstream).
            return null;
        }
    }

    public Task CloseAsync()
    {
        // Complete the channel the peer reads from: it drains buffered frames, then
        // its RecvBytesAsync returns null (graceful close).
        _toPeer.TryComplete();
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await CloseAsync().ConfigureAwait(false);
}
