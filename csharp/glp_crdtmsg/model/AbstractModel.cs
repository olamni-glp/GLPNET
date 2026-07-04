// Abstract message model (feature 041-crdtmsg-mvp, T007).
//
// Contract C1/C4 (envelope-header-registry.md) / data-model §1/§2/§3/§6:
//   ONE encoding-neutral message definition. Every surface (binary-term / JSON / YAML / CBOR) is a
//   codec AGAINST this model, never a parallel schema (avoids drift — R1). All wire values are
//   ground terms (BB-CRDT-9). "opaque" = carried verbatim, never re-encoded.
//
// crdt_model discriminator (T034 / C9) lives here: op_based | state_based (default when absent) | none.

namespace GlpRuntime.CrdtMsg.Model;

/// <summary>
/// CRDT discriminator (data-model §1, C9). <c>op_based</c> for the message document; <c>StateBased</c>
/// is the default when absent; <c>None</c> = an ordinary (non-CRDT) request/response, which travels
/// the pipe unimpeded.
/// </summary>
public enum CrdtModel : byte
{
    /// <summary>Ordinary request/response — not a CRDT document.</summary>
    None = 0,
    /// <summary>Default when the tag is absent — a state-based CRDT payload.</summary>
    StateBased = 1,
    /// <summary>Op-based JSON-CRDT document (the messaging demonstrator).</summary>
    OpBased = 2,
}

/// <summary>
/// A TLV section (data-model §3, C2). <c>TypeNumber</c> is LEB128-encoded; its numeric range decides
/// criticality (ignorable vs must-understand — see <c>envelope/TlvSection</c>, T013). <c>Value</c> is
/// opaque bytes (term-codec-inner for term payloads). Unknown+ignorable sections are carried verbatim.
/// </summary>
public sealed record Section(long TypeNumber, byte[] Value);

/// <summary>
/// Fixed routing policy (data-model §6, C23) — the shipped MVP evaluator (the experimental GLP guard
/// is propose-only, T053). All three lists hold authenticated peer-names / @names. An unsatisfiable
/// policy fails loud (evaluated by <c>route/PolicyMatcher</c>, T051).
/// </summary>
public sealed record RoutingPolicy(
    IReadOnlyList<string> Targets,
    IReadOnlyList<string> Waypoints,
    IReadOnlyList<string> Excludes)
{
    public static readonly RoutingPolicy Empty =
        new(Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());
}

/// <summary>
/// The unified, router-opaque header (data-model §2, C4). A relay forwards <c>msg_id..seq</c> +
/// payload bytes VERBATIM (payload-opacity, FR-010). <c>CapabilitySlot</c> is envelope-v2
/// additive-optional: a v1 reader skips it by length (T048) — <c>null</c> here means absent.
/// </summary>
public sealed record Header(
    string MsgId,
    string From,
    string To,
    long Seq,
    RoutingPolicy Policy,
    byte[]? CapabilitySlot = null);

/// <summary>
/// The single abstract message (data-model §1). <c>PayloadType</c> is a <c>glp_wire_registry</c> id;
/// an unknown id is rejected by the decoder. Decoding consumes all bytes or throws (FR-005, enforced
/// per surface by <c>envelope/DecodeGuard</c>, T018). <c>Sections</c> are ordered; unknown ones are
/// preserved verbatim across every surface conversion (C1 invariant).
/// </summary>
public sealed record Message(
    int SchemaVersion,
    byte PayloadType,
    Header Header,
    IReadOnlyList<Section> Sections,
    CrdtModel CrdtModel);
