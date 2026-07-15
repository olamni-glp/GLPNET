namespace Ynet.Transport.Capability;

/// <summary>
/// The single first-class capability the transport tier presents to qhstate 056 (FR-004).
/// 056 owns the embed, macaroon gate, admission decision, and leaf policy; this surface is what
/// those decisions bind to. Contains NO service-embed, macaroon-minting, admission-deciding, or
/// durable-mailbox operation (FR-024 / SC-011) — see contracts/transport-capability.md.
/// </summary>
public interface IYnetTransport
{
    // --- core link (US1/US2) ---
    Result<LinkHandle> Connect(NodeId peer, RoutingSelection selection);
    Result<Ack> Send(LinkHandle link, ReadOnlyMemory<byte> frame);
    Result<ReadOnlyMemory<byte>> Receive(LinkHandle link);
    void Close(LinkHandle link);

    // --- discovery (US3) ---
    Result<Unit> DhtStore(Dht.SignedRecord record);
    Result<Dht.SignedRecord> DhtLookup(ReadOnlyMemory<byte> key);

    // --- relay mechanism, admission ENFORCED from 056 (US4) ---
    Result<Unit> OfferRelay(NodeId relay, AdmissionProof proof);

    // --- leaf mode (US8) ---
    void SetMode(NodeMode mode);

    // --- introspection (FR-023 auditability) ---
    PathInfo PathInfo(LinkHandle link);
}

/// <summary>Distinct, observable refusal reasons (FR-018/FR-023). No silent drops.</summary>
public enum RefusalReason
{
    None = 0,
    IdentityMismatch,          // peer key != handshake identity (FR-002)
    Unreachable,               // no direct punch and no admitted relay (FR-018)
    AuthorizedButUnreachable,  // auth ok, path could not establish / was torn down (research R3)
    SealUnavailable,           // sealed requested, only clear paths available — fail closed (FR-011)
    RelayNotAdmitted,          // 056 has not admitted this relay (FR-007)
    LeafTransitRefused,        // leaf asked to forward third-party transit (FR-016)
    EgressDenied,              // exit-abuse policy / default-deny (FR-012/FR-013)
    FurtherResolverRequired,   // name beyond self-certified key->record (FR-017)
    RecordNotFound,            // DHT lookup: no self-certified record for a valid key (contract NotFound)
    RecordRejected,            // DHT store: record does not self-certify — tamper/spoof/expiry (FR-006)
    RoutingCapacityExhausted,  // DSDV node index full — the overlay cannot admit another node (FR-021, co #221)
    TransportUnsupported,      // native QUIC unavailable — refuse, never downgrade (050 gate)
}

public enum NodeMode { Full, Leaf }

public enum RoutingMode { Normal, Sealed }

public enum ExitKind { Internal, ClearnetGate }

/// <summary>Per-invocation selection surfaced by 056 (US5/US6). Unspecified -> declared safe default.</summary>
public readonly record struct RoutingSelection(RoutingMode Mode, int AnonymityLevel, ExitKind Exit)
{
    /// <summary>Declared safe default (FR-011): sealed + anonymity, internal reach.</summary>
    public static RoutingSelection SafeDefault => new(RoutingMode.Sealed, AnonymityLevel: 1, ExitKind.Internal);
}

public readonly record struct NodeId(string Value)
{
    public override string ToString() => Value;
}

public readonly record struct LinkHandle(Guid Id, NodeId Peer, PathType PathType);

public enum PathType { Direct, Relayed }

public readonly record struct PathInfo(PathType PathType, RoutingMode Mode, int AnonymityLevel, int RelayHops);

public readonly record struct Ack(long Sequence);

public readonly record struct Unit
{
    public static readonly Unit Value = default;
}

/// <summary>The 056 admission decision (a macaroon caveat verdict) this tier ENFORCES, never decides.</summary>
public readonly record struct AdmissionProof(NodeId Relay, bool Admitted, string TrafficClass, bool Revoked);

/// <summary>
/// Uniform outcome carrier: a success value OR a distinct refusal reason with zero side-effects
/// guaranteed by the caller on the refusal path (contract invariant 2).
/// </summary>
public readonly struct Result<T>
{
    public bool Ok { get; }
    public T? Value { get; }
    public RefusalReason Reason { get; }

    private Result(bool ok, T? value, RefusalReason reason) { Ok = ok; Value = value; Reason = reason; }

    public static Result<T> Success(T value) => new(true, value, RefusalReason.None);
    public static Result<T> Refuse(RefusalReason reason) => new(false, default, reason);
}
