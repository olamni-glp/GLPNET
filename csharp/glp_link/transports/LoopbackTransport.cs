using System.Threading.Channels;
using GlpRuntime.Link.Seam;

namespace GlpRuntime.Link.Transports;

/// <summary>
/// Deterministic in-process loopback transport (T026), a concrete
/// <see cref="ILinkTransport"/> for <see cref="LinkScheme.Loopback"/>. A
/// <see cref="ListenAsync"/> and a <see cref="ConnectAsync"/> on the same channel
/// name (the <see cref="LinkAddress.Host"/>) rendezvous into one bilateral link;
/// the two ends share an equivalent <see cref="LinkId"/> (FR-002) and exchange
/// frames over two ordered in-memory channels — FIFO/in-order/exactly-once by
/// default (FR-018/020). It is the substrate for the headline-split and
/// integration tests (Phase 4/5); a T052 fault decorator wraps the produced
/// endpoints to inject drop/reorder/duplicate/delay deterministically (seeded).
/// </summary>
/// <remarks>
/// Establishment role is irrelevant to loopback pairing (FR-004): whichever side
/// arrives first parks until the other appears, so listener-first and
/// connector-first both succeed. One transport instance is the rendezvous hub for
/// all its links; share a single instance between the two ends in a test.
/// </remarks>
public sealed class LoopbackTransport : ILinkTransport
{
    private sealed class PendingSide
    {
        public required TaskCompletionSource<ILinkEndpoint> Tcs;
        public required LinkAddress Addr;
    }

    private static readonly LinkScheme[] Schemes = { LinkScheme.Loopback };

    private readonly object _lock = new();
    private readonly Dictionary<string, PendingSide> _pending = new();
    private long _nonce;

    public IReadOnlyCollection<LinkScheme> SupportedSchemes => Schemes;

    public Task<ILinkEndpoint> ListenAsync(LinkScheme scheme, LinkAddress local, LinkOptions opts, CancellationToken ct = default)
        => RendezvousAsync(scheme, local, ct);

    public Task<ILinkEndpoint> ConnectAsync(LinkScheme scheme, LinkAddress remote, LinkOptions opts, CancellationToken ct = default)
        => RendezvousAsync(scheme, remote, ct);

    private Task<ILinkEndpoint> RendezvousAsync(LinkScheme scheme, LinkAddress addr, CancellationToken ct)
    {
        if (scheme != LinkScheme.Loopback)
            throw new ArgumentException($"LoopbackTransport does not serve scheme '{scheme}'", nameof(scheme));
        ct.ThrowIfCancellationRequested();

        string channel = addr.Host;

        lock (_lock)
        {
            if (_pending.Remove(channel, out var other))
            {
                // Second arrival: pair the two ends over two ordered channels.
                var a2b = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
                var b2a = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
                var id = new LinkId(LinkScheme.Loopback, addr, LinkNonce.Int(++_nonce));

                var firstEnd = new LoopbackEndpoint(id, a2b.Writer, b2a.Reader);
                var secondEnd = new LoopbackEndpoint(id, b2a.Writer, a2b.Reader);

                other.Tcs.SetResult(firstEnd);
                return Task.FromResult<ILinkEndpoint>(secondEnd);
            }

            // First arrival: park until the peer shows up.
            var tcs = new TaskCompletionSource<ILinkEndpoint>(TaskCreationOptions.RunContinuationsAsynchronously);
            var side = new PendingSide { Tcs = tcs, Addr = addr };
            _pending[channel] = side;

            if (ct.CanBeCanceled)
            {
                var reg = ct.Register(() =>
                {
                    lock (_lock)
                    {
                        if (_pending.TryGetValue(channel, out var p) && ReferenceEquals(p, side))
                            _pending.Remove(channel);
                    }
                    tcs.TrySetCanceled(ct);
                });
                // Detach the registration once the rendezvous resolves.
                tcs.Task.ContinueWith(_ => reg.Dispose(), TaskScheduler.Default);
            }

            return tcs.Task;
        }
    }

    /// <summary>Channels currently awaiting a peer (no rendezvous yet).</summary>
    public int PendingCount
    {
        get { lock (_lock) return _pending.Count; }
    }
}
