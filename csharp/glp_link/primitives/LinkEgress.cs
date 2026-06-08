using GlpRuntime.Link.Reliability;
using GlpRuntime.Multiagent;
using GlpRuntime.Runtime;

namespace GlpRuntime.Link.Primitives;

/// <summary>
/// The one ground-relay egress routine shared by BOTH sender faces (feature 025,
/// T031): the channel face (<c>link_send/3</c> → the <c>Out</c>-stream egress
/// drainer armed by <c>'_link_setup'</c>) and the LinkId face (<c>out_relay/3</c> →
/// the <c>'_link_send'/3</c> kernel). Keeping a single ship path closes risk R-5
/// (two egress paths diverging): both serialize a GROUND copy (FR-010), frame +
/// sequence it, and write every fragment to the link endpoint in send order.
/// </summary>
internal static class LinkEgress
{
    /// <summary>
    /// Resolve <paramref name="msg"/> to a VarRef-free ground tree, serialize it,
    /// frame + sequence it, and ship every fragment over <paramref name="handle"/>'s
    /// endpoint (FR-010 ground-relay, per-link FIFO FR-018/053). Throws
    /// <see cref="InvalidOperationException"/> if the term is not ground (an unbound
    /// reader/writer reached the wire) — the ground gate. Callers decide what a gate
    /// violation means: a dropped frame for the stream drainer, a caller-bug Abort
    /// for the kernel (the GLP <c>ground(Msg?)</c> guard should have excluded it).
    /// </summary>
    public static void ShipGround(HeapFCP heap, LinkHandle handle, Term msg)
    {
        // Deep-resolve against the heap so a ground STRUCT (whose args may be VarRefs
        // into bound cells) serializes — SerializeAgentMessage throws on ANY VarRef,
        // so the term handed to it must be VarRef-free. Resolution doubles as the
        // ground gate: an unbound cell at any depth throws here, never on the wire.
        Term ground = ResolveGround(heap, msg);

        // Empty origin: the per-link endpoint identity, not a global-name stamp, keys
        // delivery on the base relay (consistent with the T030 Out-stream drainer).
        byte[] payload = new PayloadSerializer(string.Empty).SerializeAgentMessage(ground);

        uint seq = handle.Sequencer.Next();
        // NOTE: SendWindow backpressure (FR-025) is intentionally NOT gated here yet —
        // credit release rides the inbound ack path, which lands with the monitor
        // primitive (T034). Acquiring a credit with no Release would freeze the runner
        // thread at the window bound. Framing + monotone sequencing are real today.
        foreach (var frame in FrameCodec.Encode(payload, seq, handle.Options.MaxFrameBytes))
            handle.Endpoint.SendBytesAsync(frame).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Dereference <paramref name="t"/> recursively into a VarRef-free tree of
    /// <see cref="ConstTerm"/>/<see cref="StructTerm"/>. An unbound cell at any depth
    /// is the ground-relay gate violation: throw rather than place a placeholder on
    /// the wire (FR-010 — no <c>_w</c>/<c>_r</c>/embedded reader ever crosses).
    /// </summary>
    private static Term ResolveGround(HeapFCP heap, Term t)
    {
        Term d = heap.Dereference(t);
        switch (d)
        {
            case ConstTerm c:
                return c;
            case StructTerm s:
            {
                var args = new Term[s.Args.Count];
                for (int i = 0; i < s.Args.Count; i++)
                    args[i] = ResolveGround(heap, s.Args[i]);
                return new StructTerm(s.Functor, args);
            }
            case VarRef v:
                throw new InvalidOperationException(
                    $"non-ground term reached egress: unbound cell {v.Addr} (ground-relay gate)");
            default:
                throw new InvalidOperationException($"cannot ground-resolve term {d.GetType().Name}");
        }
    }
}
