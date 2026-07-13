namespace GlpRuntime.Link.Seam;

/// <summary>
/// Host-side, below-GLP capability gate (feature 050 US3, FR-008/FR-009 — contract
/// <c>capability-gating.md</c>): verify-before-act for link establishment and for gated actions on an
/// established link. Mirrors the <see cref="IPayloadCodec"/> seam shape (research D-1): the interface
/// lives in <c>glp_link</c>, the concrete macaroon-backed gate lives in <c>glp_crdtmsg</c> (which
/// already references <c>glp_link</c>) and is injected per scheme at the composition root, so
/// <c>glp_link</c> stays capability-agnostic and no reference cycle arises. Not a GLP kernel, guard,
/// or primitive — the gate is entirely below the seam (FR-019 / Constitution IV-a).
/// </summary>
public interface ICapabilityGate
{
    /// <summary>
    /// Gate link establishment (FR-008): called by the canonical establish core BEFORE the transport
    /// endpoint is opened or the link wired. Returns <c>true</c> to proceed; <c>false</c> is a
    /// fail-closed refusal the gate has already RECORDED as a distinct outcome (never a silent drop).
    /// The caller surfaces the refusal via its graceful abort path — never a crash (FR-009).
    /// </summary>
    bool GateEstablish(LinkId id);
}

/// <summary>
/// Thrown by a capability-verifying payload codec when an inbound gated action's capability fails
/// verify-before-act (FR-009: absent / tampered / expired / unsatisfiable / un-understood — all
/// fail closed). The thrower records the distinct refusal BEFORE throwing; the inbound pump refuses
/// just that action (nothing is delivered) and the link, pump, and run stay graceful.
/// </summary>
public sealed class CapabilityRefusedException : Exception
{
    public CapabilityRefusedException(string message) : base(message) { }
}

/// <summary>Allow-all default: a scheme with no registered gate keeps its pre-050 behaviour
/// (loopback/tcp links are not capability-gated).</summary>
public sealed class DefaultCapabilityGate : ICapabilityGate
{
    public static readonly DefaultCapabilityGate Instance = new();

    private DefaultCapabilityGate() { }

    public bool GateEstablish(LinkId id) => true;
}
