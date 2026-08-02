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
/// Establishment role is irrelevant to which side arrives first (FR-004): whichever
/// side arrives parks until an opposite-role peer appears, so listener-first and
/// connector-first both succeed. One transport instance is the rendezvous hub for
/// all its links; share a single instance between the two ends in a test.
///
/// <para><b>Multi-accept (feature 062 US3, T019).</b> Rendezvous is <i>role-aware</i>:
/// a listener pairs only with a connector and vice versa, and each channel keeps a
/// FIFO queue per role. So a server may issue N concurrent <see cref="ListenAsync"/>
/// on ONE address and N connectors pair with them, yielding N independent bilateral
/// links (distinct <see cref="LinkId"/>s), none dropped. This preserves the D-9
/// point-to-point base — it is N separate links, never a fan-out hub.</para>
/// </remarks>
public sealed class LoopbackTransport : ILinkTransport
{
    private sealed class PendingSide
    {
        public required TaskCompletionSource<ILinkEndpoint> Tcs;
        public required LinkAddress Addr;
        public bool Cancelled;
    }

    // Per-channel FIFO queues of parked sides, kept apart by establishment role so a
    // listener only ever pairs with a connector (multi-accept correctness).
    private sealed class ChannelPending
    {
        public readonly List<PendingSide> Listeners = new();
        public readonly List<PendingSide> Connectors = new();
        public bool IsEmpty => Listeners.Count == 0 && Connectors.Count == 0;
    }

    private static readonly LinkScheme[] Schemes = { LinkScheme.Loopback };

    private readonly object _lock = new();
    private readonly Dictionary<string, ChannelPending> _pending = new();
    private long _nonce;

    public IReadOnlyCollection<LinkScheme> SupportedSchemes => Schemes;

    public Task<ILinkEndpoint> ListenAsync(LinkScheme scheme, LinkAddress local, LinkOptions opts, CancellationToken ct = default)
        => RendezvousAsync(scheme, local, LinkRole.Listener, ct);

    public Task<ILinkEndpoint> ConnectAsync(LinkScheme scheme, LinkAddress remote, LinkOptions opts, CancellationToken ct = default)
        => RendezvousAsync(scheme, remote, LinkRole.Connector, ct);

    private Task<ILinkEndpoint> RendezvousAsync(LinkScheme scheme, LinkAddress addr, LinkRole role, CancellationToken ct)
    {
        if (scheme != LinkScheme.Loopback)
            throw new ArgumentException($"LoopbackTransport does not serve scheme '{scheme}'", nameof(scheme));
        ct.ThrowIfCancellationRequested();

        string channel = addr.Host;

        lock (_lock)
        {
            if (!_pending.TryGetValue(channel, out var cp))
            {
                cp = new ChannelPending();
                _pending[channel] = cp;
            }

            // Pair with the head of the OPPOSITE-role queue, skipping cancelled sides.
            var peers = role == LinkRole.Listener ? cp.Connectors : cp.Listeners;
            PendingSide? peer = null;
            while (peers.Count > 0)
            {
                var head = peers[0];
                peers.RemoveAt(0);
                if (!head.Cancelled) { peer = head; break; }
            }

            if (peer is not null)
            {
                // Rendezvous: pair the two ends over two ordered channels (one fresh link).
                var a2b = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
                var b2a = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
                var id = new LinkId(LinkScheme.Loopback, addr, LinkNonce.Int(++_nonce));

                var peerEnd = new LoopbackEndpoint(id, a2b.Writer, b2a.Reader);
                var selfEnd = new LoopbackEndpoint(id, b2a.Writer, a2b.Reader);

                if (cp.IsEmpty) _pending.Remove(channel);

                peer.Tcs.SetResult(peerEnd);
                return Task.FromResult<ILinkEndpoint>(selfEnd);
            }

            // No opposite-role peer waiting: park in this role's queue until one arrives.
            var tcs = new TaskCompletionSource<ILinkEndpoint>(TaskCreationOptions.RunContinuationsAsynchronously);
            var side = new PendingSide { Tcs = tcs, Addr = addr };
            var ownQueue = role == LinkRole.Listener ? cp.Listeners : cp.Connectors;
            ownQueue.Add(side);

            if (ct.CanBeCanceled)
            {
                var reg = ct.Register(() =>
                {
                    lock (_lock)
                    {
                        side.Cancelled = true;
                        ownQueue.Remove(side);
                        if (cp.IsEmpty) _pending.Remove(channel);
                    }
                    tcs.TrySetCanceled(ct);
                });
                // Detach the registration once the rendezvous resolves.
                tcs.Task.ContinueWith(_ => reg.Dispose(), TaskScheduler.Default);
            }

            return tcs.Task;
        }
    }

    /// <summary>Sides currently awaiting a peer (no rendezvous yet), across all channels and roles.</summary>
    public int PendingCount
    {
        get
        {
            lock (_lock)
            {
                int n = 0;
                foreach (var cp in _pending.Values)
                {
                    foreach (var s in cp.Listeners) if (!s.Cancelled) n++;
                    foreach (var s in cp.Connectors) if (!s.Cancelled) n++;
                }
                return n;
            }
        }
    }
}
