// Feature 050 P1 fix (2026-07-13, codexreview 20260713T070618Z): the lazy QUIC-composition adapters
// defer the fail-closed glpquick-cert/ trust-material load (cert + macaroon root key) from
// composition-root/REPL-startup to FIRST actual QUIC use. A host WITHOUT that (gitignored) material
// must still start the REPL for non-QUIC (loopback/tcp) links — while a QUIC link still fails closed
// LOUDLY the moment it is opened. These tests pin both halves.

using GlpRuntime.CrdtMsg.Bridge;
using GlpRuntime.Link.Seam;
using GlpRuntime.Link.Transports;

namespace GlpRuntime.CrdtMsg.Tests;

public sealed class LazyQuicCompositionTests
{
    /// <summary>Sentinel standing in for the real fail-closed load throw (FileNotFoundException on
    /// a missing glpquick-cert/ artifact) so the test never depends on the repo cert dir.</summary>
    private sealed class TrustMaterialAbsent : Exception { }

    [Fact]
    public void Composition_root_construction_loads_no_trust_material()
    {
        var loads = 0;

        // The exact shape the REPL composition root wires up (out/csharp/glp_repl/Program.cs).
        var transport = new LazyQuicTransport(() => { loads++; throw new TrustMaterialAbsent(); });
        var gate = new LazyMacaroonLinkGate(() => { loads++; throw new TrustMaterialAbsent(); });
        var codec = new LazyCrdtMsgPayloadCodec(() => { loads++; return new CrdtMsgPayloadCodec(gate.Value); });

        // Constructing the composition — the fresh-checkout REPL-startup path — invokes NO factory,
        // so a host with no glpquick-cert/ still boots (this is the P1 regression guard).
        Assert.Equal(0, loads);

        // And the transport registry can still SELECT the QUIC leaf by scheme without any load.
        Assert.Equal(new[] { LinkScheme.Quic }, transport.SupportedSchemes.ToArray());
        Assert.Equal(0, loads);

        GC.KeepAlive(codec);
    }

    [Fact]
    public void Lazy_codec_fails_closed_loudly_on_first_wire_use()
    {
        var codec = new LazyCrdtMsgPayloadCodec(() => throw new TrustMaterialAbsent());

        // First actual wire use materializes the gated codec — and surfaces the fail-closed throw.
        Assert.Throws<TrustMaterialAbsent>(() => codec.Decode(new byte[] { 0 }));
    }

    [Fact]
    public void Lazy_transport_does_not_materialize_when_only_scheme_is_queried()
    {
        var loaded = false;
        var transport = new LazyQuicTransport(() => { loaded = true; throw new TrustMaterialAbsent(); });

        _ = transport.SupportedSchemes; // registry selection path — must not touch the factory
        Assert.False(loaded);
    }
}
