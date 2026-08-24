using Ynet.Transport.Capability;

namespace Ynet.Transport.Relay;

/// <summary>
/// Enforces the qhstate-056 relay ADMISSION decision at the forwarding hop (FR-007). This tier
/// owns the relay mechanism; it never DECIDES admission — it consumes the 056 AdmissionProof.
/// Sybil/DoS resistance is by curated-node gating (FR-008), i.e. only proofs the curated authority
/// issued are ever admitted. REAL + TESTED (T031).
/// </summary>
public static class AdmissionEnforcer
{
    /// <summary>
    /// A relay is selectable only if 056 admitted it, it is not revoked, AND the proof was issued
    /// for THIS relay (FR-007/FR-008). Binding proof.Relay to the target defeats a confused-deputy
    /// where a valid proof for admitted relay X is replayed to authorize a different relay M
    /// (codexreview finding, SC-004).
    /// </summary>
    public static bool IsSelectable(NodeId targetRelay, AdmissionProof proof)
        => proof.Admitted && !proof.Revoked && proof.Relay == targetRelay;

    /// <summary>
    /// Map a traffic class to the relay mechanism (clarify §5.2): circuit-relay-v2 (voucher-gated)
    /// for mesh; Tor-style cell relay default for internet + critical flows.
    /// </summary>
    public static RelayMechanism MechanismFor(string trafficClass) => trafficClass switch
    {
        "mesh" => RelayMechanism.CircuitRelayV2,
        "internet" or "critical" => RelayMechanism.TorCell,
        _ => RelayMechanism.CircuitRelayV2, // safe default: voucher-gated
    };
}

public enum RelayMechanism { CircuitRelayV2, TorCell }

/// <summary>
/// Leaf-never-relays enforcement HOOK (FR-016). The leaf-never-relays POLICY is declared and owned
/// by qhstate 056; this hook is the concrete enforcement surface it binds to. A leaf may use relays
/// for its OWN egress but never forwards third-party transit. REAL + TESTED (T051).
/// </summary>
public sealed class LeafMode
{
    public NodeMode Mode { get; private set; } = NodeMode.Full;

    public void SetMode(NodeMode mode) => Mode = mode;

    /// <summary>Refuse third-party transit in leaf mode; the node's own traffic is unaffected.</summary>
    public RefusalReason? EvaluateTransit(bool isThirdPartyTransit)
    {
        if (Mode == NodeMode.Leaf && isThirdPartyTransit)
            return RefusalReason.LeafTransitRefused;
        return null; // own originated/terminated traffic, or a full node — allowed
    }

    /// <summary>The use-relays-for-self / never-relay-for-others asymmetry, made inspectable (US8 AS3).</summary>
    public (bool usesRelaysForSelf, bool relaysForOthers) Asymmetry()
        => Mode == NodeMode.Leaf ? (true, false) : (true, true);
}
