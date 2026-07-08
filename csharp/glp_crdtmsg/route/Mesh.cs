// Mesh node — deliver over the link-transport seam (feature 041-crdtmsg-mvp, T052).
//
// Contract C20/C13 / SC-009: a node applies an op locally, wraps it in a router-opaque message, and sends
// the encoded bytes over the ILinkTransport (the in-memory fabric for the single-host multi-client
// demonstrator; the glp_quick_host QUIC/WS adapter is the drop-in replacement). A receiving node decodes
// the header, dedups (msg_id + per-link seq), extracts the op section VERBATIM, and commits it to its
// op-WAL. The live rich-text document is the store projection (US2/US3), so two nodes that exchange the
// same ops converge — even with reordering — and each op is durable in each store's op-WAL.

using GlpRuntime.CrdtMsg.Crdt;
using GlpRuntime.CrdtMsg.Envelope;
using GlpRuntime.CrdtMsg.Headers;
using GlpRuntime.CrdtMsg.Model;
using GlpRuntime.CrdtMsg.Store;
using GlpRuntime.WireRegistry;

namespace GlpRuntime.CrdtMsg.Route;

public sealed class MeshNode
{
    private static readonly IReadOnlySet<long> Understood =
        new HashSet<long> { UnifiedHeader.OpSectionType, CapabilitySlot.SectionType };

    private readonly ILinkTransport _link;
    private readonly Dedup _dedup = new();
    private long _seq;
    private int _msgCounter;

    public string Peer => _link.LocalPeer;
    public IOpWal Store { get; }

    public MeshNode(ILinkTransport link, IOpWal store)
    {
        _link = link;
        Store = store;
    }

    /// <summary>The live rich-text document = the store projection (converges under the op set).</summary>
    public RichTextDoc Doc => RichTextDoc.Rebuild(Store.Ops);

    /// <summary>Apply an op locally and send it (router-opaque) to <paramref name="to"/>.</summary>
    public async Task SendOpAsync(Op op, string to)
    {
        Store.Append(op); // local commit (idempotent)
        // deterministic, unique-per-node msg_id (no wall clock — peer + monotonic counter)
        string msgId = $"{Peer}#{_msgCounter++}";
        var msg = new Message(
            SchemaVersion: 1,
            PayloadType: PayloadType.CrdtMessage,
            Header: new Model.Header(msgId, Peer, to, _seq++, RoutingPolicy.Empty),
            Sections: new[] { new Section(UnifiedHeader.OpSectionType, OpCodec.Encode(op)) },
            CrdtModel: CrdtModel.OpBased);
        await _link.SendAsync(to, MessageCodec.Binary.Encode(msg));
    }

    /// <summary>Receive and process exactly <paramref name="count"/> inbound messages.</summary>
    public async Task PumpAsync(int count)
    {
        for (int i = 0; i < count; i++)
        {
            var inbound = await _link.Inbound.ReadAsync();
            Receive(inbound.Bytes, inbound.FromPeer);
        }
    }

    private void Receive(byte[] bytes, string fromLink)
    {
        Message msg = MessageCodec.Binary.Decode(bytes);
        DecodeGuard.CheckMustUnderstand(msg, Understood);
        RouterView view = UnifiedHeader.Route(msg);

        if (!_dedup.Accept(view.MsgId, fromLink, view.Seq)) return; // duplicate suppressed (C22)

        byte[] opBytes = UnifiedHeader.OpBytes(msg)
            ?? throw new CrdtMsgException("message carries no op section");
        Store.Append(OpCodec.Decode(opBytes)); // idempotent at the store boundary
    }
}
