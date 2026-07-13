// Lazy QUIC-composition adapters — defer the fail-closed glpquick-cert/ trust-material load from
// composition-root/REPL-startup to FIRST ACTUAL QUIC USE (feature 050, P1 fix 2026-07-13).
//
// Why: the composition root (out/csharp/glp_repl/Program.cs, AfterEngineCreated) runs ONCE at REPL
// startup and eagerly called SharedCertMaterial.LoadFromRepo() + StaticMacaroonMaterial.LoadFromRepo(),
// both of which FAIL CLOSED by THROWING when glpquick-cert/ is absent (the material is gitignored —
// private keys). That made a fresh checkout unable to start the REPL AT ALL, even for non-QUIC
// (loopback/tcp) links (codexreview 20260713T070618Z, P1).
//
// These adapters keep the fail-closed guarantee EXACTLY — the throwing *Material.LoadFromRepo() still
// runs, and still throws loudly — but only the first time a QUIC link is actually established/used,
// never at startup. Non-QUIC use no longer depends on QUIC trust material. FR-010/FR-011 (cert is a
// permanent, mutually-pinned credential) are untouched: they govern WHAT the credential is, not WHEN
// it loads. The Lazy<T> is thread-safe (ExecutionAndPublication) — concurrent first establishments
// load the material exactly once.

using GlpRuntime.Link.Seam;
using GlpRuntime.Link.Transports;
using GlpRuntime.Runtime;

namespace GlpRuntime.CrdtMsg.Bridge;

/// <summary>An <see cref="ILinkTransport"/> that defers constructing the real <see cref="QuicTransport"/>
/// (and thus the <see cref="SharedCertMaterial.LoadFromRepo"/> trust-material load) until the first
/// <see cref="ListenAsync"/>/<see cref="ConnectAsync"/>. <see cref="SupportedSchemes"/> answers WITHOUT
/// loading, so the transport registry can select the QUIC leaf on a host with no cert material — the
/// load (and its loud fail-closed throw when absent) happens only if a QUIC link is actually opened.</summary>
public sealed class LazyQuicTransport : ILinkTransport
{
    private static readonly IReadOnlyCollection<LinkScheme> QuicOnly = new[] { LinkScheme.Quic };
    private readonly Lazy<QuicTransport> _inner;

    /// <param name="factory">Builds the real leaf (loading cert+pin); invoked at most once, on first use.</param>
    public LazyQuicTransport(Func<QuicTransport> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _inner = new Lazy<QuicTransport>(factory, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>The materialized leaf (forces the deferred trust-material load).</summary>
    public QuicTransport Value => _inner.Value;

    public IReadOnlyCollection<LinkScheme> SupportedSchemes => QuicOnly;

    public Task<ILinkEndpoint> ListenAsync(LinkScheme scheme, LinkAddress local, LinkOptions opts, CancellationToken ct = default)
        => _inner.Value.ListenAsync(scheme, local, opts, ct);

    public Task<ILinkEndpoint> ConnectAsync(LinkScheme scheme, LinkAddress remote, LinkOptions opts, CancellationToken ct = default)
        => _inner.Value.ConnectAsync(scheme, remote, opts, ct);
}

/// <summary>An <see cref="ICapabilityGate"/> that defers constructing the real <see cref="MacaroonLinkGate"/>
/// (and thus the <see cref="StaticMacaroonMaterial.LoadFromRepo"/> root-key load) until the first
/// <see cref="GateEstablish"/>. Establishing a QUIC link on a host with no macaroon material fails
/// closed loudly at that point — never at startup.</summary>
public sealed class LazyMacaroonLinkGate : ICapabilityGate
{
    private readonly Lazy<MacaroonLinkGate> _inner;

    /// <param name="factory">Builds the real gate (loading the root key + minting the macaroon);
    /// invoked at most once, on first gated action.</param>
    public LazyMacaroonLinkGate(Func<MacaroonLinkGate> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _inner = new Lazy<MacaroonLinkGate>(factory, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>The materialized gate (forces the deferred root-key load). The codec's capability slot
    /// carriage reads the presented macaroon through this.</summary>
    public MacaroonLinkGate Value => _inner.Value;

    public bool GateEstablish(LinkId id) => _inner.Value.GateEstablish(id);
}

/// <summary>An <see cref="IPayloadCodec"/> that defers constructing the real gated
/// <see cref="CrdtMsgPayloadCodec"/> until the first <see cref="Encode"/>/<see cref="Decode"/> — so the
/// gate it wraps (and its macaroon load) is materialized only when a QUIC envelope is actually
/// encoded/decoded.</summary>
public sealed class LazyCrdtMsgPayloadCodec : IPayloadCodec
{
    private readonly Lazy<CrdtMsgPayloadCodec> _inner;

    /// <param name="factory">Builds the real codec (typically <c>new CrdtMsgPayloadCodec(gate.Value)</c>);
    /// invoked at most once, on first wire use.</param>
    public LazyCrdtMsgPayloadCodec(Func<CrdtMsgPayloadCodec> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _inner = new Lazy<CrdtMsgPayloadCodec>(factory, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public byte[] Encode(Term ground) => _inner.Value.Encode(ground);

    public Term Decode(byte[] payload) => _inner.Value.Decode(payload);
}
