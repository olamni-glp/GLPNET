using Ynet.Transport.Capability;

namespace Ynet.Transport.Link;

/// <summary>
/// SEAM (not yet wired to the wire): the consolidated YNET-owned QUIC link that will SUPERSEDE
/// GLPNET `QuicTransport` (FR-001). The production implementation HARVESTS csharp/glp_link's MsQuic
/// setup (per-scheme registries, reliability sublayer, IPayloadCodec/ICapabilityGate seams, the 050
/// codexreview robustness fixes) and replaces the SPKI-pin identity with the node key AS the TLS
/// identity (FR-002, absorbing iroh/`noq`), keeping the 050 IsSupported-gate-and-refuse posture.
///
/// This class captures the REAL, TESTED pre-frame identity-verification gate (FR-002) and the seal
/// wiring; the MsQuic dial/accept itself is a compiling seam pending the glp_link harvest (T011/T012).
/// Marked [~] in tasks.md — it compiles and its verifiable logic is tested, but it does not open a
/// real QUIC connection in this pass. No fake "robustness" masks this (Constitution II): the wire
/// operations throw NotSupportedException with a clear reason rather than pretending to connect.
/// </summary>
public sealed class YnetLink
{
    /// <summary>Whether the native QUIC transport is available. 050 gate: refuse, never downgrade.</summary>
    public static bool IsSupported => System.Net.Quic.QuicConnection.IsSupported;

    /// <summary>
    /// The REAL pre-frame gate (FR-002): a dialer whose presented node id does not match its
    /// handshake public key is refused BEFORE any application frame. Tested (T010).
    /// </summary>
    public static Result<Unit> VerifyPeerIdentity(NodeId claimedPeer, ReadOnlySpan<byte> handshakePublicKeySpki)
        => NodeIdentity.PeerIdentityMatches(claimedPeer, handshakePublicKeySpki)
            ? Result<Unit>.Success(Unit.Value)
            : Result<Unit>.Refuse(RefusalReason.IdentityMismatch);

    /// <summary>
    /// SEAM: dial a peer over the consolidated QUIC socket. The real link handshake + sealed
    /// send/receive/close is implemented and tested in <see cref="YnetSession"/> over an
    /// <see cref="IWireChannel"/> (US1/T014/T016 — see the in-process channel). What remains here is
    /// the MsQuic <see cref="IWireChannel"/> impl harvested from csharp/glp_link (T011) so `Dial`
    /// resolves a real socket to <paramref name="peer"/> and hands it to <see cref="YnetSession"/>.
    /// Until then this refuses honestly (050 gate) rather than pretending to connect (Constitution II).
    /// </summary>
    public Result<LinkHandle> Dial(NodeId peer, RoutingSelection selection)
    {
        if (!IsSupported) return Result<LinkHandle>.Refuse(RefusalReason.TransportUnsupported);
        throw new NotSupportedException(
            "YnetLink.Dial is a compiling seam pending the csharp/glp_link MsQuic IWireChannel harvest " +
            "(051 T011). The link session itself is real + tested in YnetSession over an IWireChannel " +
            "(in-process today); only the QUIC socket channel is outstanding — see FR-001/FR-002.");
    }
}
