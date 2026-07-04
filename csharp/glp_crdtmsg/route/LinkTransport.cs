// Link-transport seam (feature 041-crdtmsg-mvp, T008).
//
// Contract C20 (routing-policy-guard.md) / C16 (capability-signature.md) / FR-016/019:
//   Delivery rides the shipped glp_quick_host (MsQuic QUIC + RFC 6455 WS over one bidi stream,
//   SPKI pin) behind THIS seam. The concrete QUIC adapter is wired at T052; the SC-009 demonstrator
//   runs single-host multi-client, for which the in-memory switch below is the transport.
//
//   🔴 FR-019 (membership vs identity): the SPKI-pin shared cert is LAYER-0 MEMBERSHIP ONLY
//   (possession == membership). It is NEVER a per-peer identity. Per-peer identity comes from the
//   enrolled Ed25519 key (sig/PeerKeys, T039), bound to the peer-name — never from the cert.
//
// Reuse points this seam sits in front of (wired incrementally): glp_result_codec.TermCodec (+the
// CycleGuard on cyclic payloads), glp_link FrameCodec (BE 22-byte frame header, CRC-32, 64 MiB
// guard), glp_quick_host QUIC/WS/SPKI. Router forwards header+payload bytes verbatim (payload-opacity).

using System.Threading.Channels;

namespace GlpRuntime.CrdtMsg.Route;

/// <summary>One inbound link delivery: the authenticated sender's peer-name + the raw frame bytes.</summary>
public readonly record struct LinkInbound(string FromPeer, byte[] Bytes);

/// <summary>
/// The transport seam every routing path talks to. Sends opaque frame bytes to a named peer and
/// surfaces inbound frames. <c>Members</c> is LAYER-0 membership (SPKI-pin) — a coarse "who is on the
/// mesh" set, explicitly NOT identity (FR-019). Identity is asserted cryptographically per message by
/// the signature layer against enrolled Ed25519 keys.
/// </summary>
public interface ILinkTransport
{
    /// <summary>This endpoint's authenticated peer-name.</summary>
    string LocalPeer { get; }

    /// <summary>Layer-0 membership (SPKI-pin possession). NOT per-peer identity (FR-019).</summary>
    IReadOnlyCollection<string> Members { get; }

    /// <summary>Send opaque frame bytes to <paramref name="toPeer"/>. Bytes are forwarded verbatim.</summary>
    ValueTask SendAsync(string toPeer, ReadOnlyMemory<byte> frameBytes, CancellationToken ct = default);

    /// <summary>Inbound frames addressed to this endpoint, in arrival order.</summary>
    ChannelReader<LinkInbound> Inbound { get; }
}

/// <summary>
/// In-process switch backing the single-host multi-client demonstrator (SC-009) and the store/route
/// unit tests: registered endpoints exchange frame bytes verbatim through a shared, thread-safe fabric.
/// This is a genuine transport implementation of the seam — the QUIC/WS adapter (T052) is a drop-in
/// replacement with identical semantics from the router's point of view.
/// </summary>
public sealed class InMemoryLinkFabric
{
    private readonly object _gate = new();
    private readonly Dictionary<string, Channel<LinkInbound>> _mailboxes = new();

    /// <summary>Join an endpoint; returns its transport view over the shared fabric.</summary>
    public ILinkTransport Connect(string peerName)
    {
        lock (_gate)
        {
            if (_mailboxes.ContainsKey(peerName))
                throw new InvalidOperationException($"peer '{peerName}' already connected to the fabric");
            var ch = Channel.CreateUnbounded<LinkInbound>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false,
            });
            _mailboxes[peerName] = ch;
            return new Endpoint(this, peerName);
        }
    }

    private IReadOnlyCollection<string> MembersSnapshot()
    {
        lock (_gate) return _mailboxes.Keys.ToArray();
    }

    private ValueTask DeliverAsync(string toPeer, string fromPeer, ReadOnlyMemory<byte> bytes)
    {
        Channel<LinkInbound>? ch;
        lock (_gate)
        {
            if (!_mailboxes.TryGetValue(toPeer, out ch))
                // @name loud-fail is enforced at the addressing layer (T049); at the raw fabric an
                // unknown destination is a hard error, never a silent drop (consistent with C21).
                throw new InvalidOperationException($"no such peer on fabric: '{toPeer}'");
        }
        // Frame bytes are copied so the caller's buffer can be reused; forwarded verbatim.
        return ch.Writer.WriteAsync(new LinkInbound(fromPeer, bytes.ToArray()));
    }

    private sealed class Endpoint : ILinkTransport
    {
        private readonly InMemoryLinkFabric _fabric;
        public string LocalPeer { get; }

        public Endpoint(InMemoryLinkFabric fabric, string localPeer)
        {
            _fabric = fabric;
            LocalPeer = localPeer;
        }

        public IReadOnlyCollection<string> Members => _fabric.MembersSnapshot();

        public ChannelReader<LinkInbound> Inbound
        {
            get
            {
                lock (_fabric._gate) return _fabric._mailboxes[LocalPeer].Reader;
            }
        }

        public ValueTask SendAsync(string toPeer, ReadOnlyMemory<byte> frameBytes, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return _fabric.DeliverAsync(toPeer, LocalPeer, frameBytes);
        }
    }
}
