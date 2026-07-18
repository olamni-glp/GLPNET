using System.IO;

using GlpRuntime.Link.Primitives;
using GlpRuntime.Link.Seam;
using GlpRuntime.Runtime;

namespace GlpRuntime.Link.Tests;

/// <summary>
/// Feature 050 P1 fix (2026-07-13, codexreview 20260713T072512Z): a payload-codec loud-fail on egress
/// (e.g. a non-crdtmsg/7 term handed to the "quic" link's codec, which throws a codec-specific
/// exception) must surface as the seam's <see cref="PayloadCodecException"/> — an
/// <see cref="InvalidOperationException"/> the '_link_send' / Out-drainer controlled-failure path
/// already handles (drop/abort) — never an uncaught exception that crashes the runner thread.
/// </summary>
public class PayloadCodecEgressTests
{
    /// <summary>Stands in for CrdtMsgPayloadCodec loud-failing an unencodable term. Throws a
    /// NON-InvalidOperationException (as CrdtMsgException does) to prove the egress boundary converts
    /// ANY codec throw — glp_link cannot see the concrete codec exception type below the seam.</summary>
    private sealed class ThrowingCodec : IPayloadCodec
    {
        public byte[] Encode(Term ground) => throw new InvalidDataException("not crdtmsg/7");
        public Term Decode(byte[] payload) => throw new InvalidDataException("malformed");
    }

    /// <summary>An endpoint whose data methods are never reached (the codec throws before the wire).</summary>
    private sealed class UnusedEndpoint : ILinkEndpoint
    {
        public UnusedEndpoint(LinkId id) => Id = id;
        public LinkId Id { get; }
        public Task SendBytesAsync(ReadOnlyMemory<byte> frame, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<byte[]?> RecvBytesAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task CloseAsync() => throw new NotSupportedException();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public event Action<LinkFaultSignal> OnFault { add { } remove { } }
    }

    private static LinkId QuicId() =>
        new(LinkScheme.Quic, LinkAddress.Endpoint("127.0.0.1", 9999), LinkNonce.Int(1));

    [Fact]
    public void ShipGround_CodecLoudFail_SurfacesControlledPayloadCodecException()
    {
        var engine = new GlpRuntimeEngine();
        var id = QuicId();
        var handle = new LinkHandle(id, new UnusedEndpoint(id), LinkOptions.Default, new ThrowingCodec());

        var ex = Assert.Throws<PayloadCodecException>(
            () => LinkEgress.ShipGround(engine.Heap, handle, new ConstTerm("x")));

        // The raw codec throw is preserved as the inner cause (diagnostics), and the seam type is an
        // InvalidOperationException so the existing controlled-failure catches handle it.
        Assert.IsType<InvalidDataException>(ex.InnerException);
        Assert.IsAssignableFrom<InvalidOperationException>(ex);
    }

    [Fact]
    public void PayloadCodecException_FlowsThroughInvalidOperationHandlers()
    {
        // The '_link_send' kernel and the Out-stream drainer catch InvalidOperationException; the seam
        // type extends it, so a codec rejection routes through the SAME drop/abort channel.
        Assert.IsAssignableFrom<InvalidOperationException>(new PayloadCodecException("codec rejected"));
    }
}
