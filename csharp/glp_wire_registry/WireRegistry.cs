// The ONE wire payload-type / functor registry (feature 041-crdtmsg-mvp, T005).
//
// Contract C6 (envelope-header-registry.md) / data-model §5 / research R10:
//   Exactly one constant table. 0x10 IL, 0x11 RESULT_ENVELOPE, 0x12+ messaging kinds.
//   glp_il_codec (PayloadHeader) and glp_result_codec (ResultEnvelopeCodec) reference THIS
//   table instead of each re-declaring the byte — zero duplicated constants (SC-010).
//   Each entry carries {payload_type, functor, compat_mode, qmedit_dsl, cddl}; both DSL forms
//   are stored, CDDL is the registered artifact (E9, Claude-agentic translation — T054 fills
//   the CDDL/qmedit text for later kinds; the byte discriminant + functor are the MVP core).
//
// The byte VALUES here are exactly the shipped 029/038 values (0x10, 0x11), so re-homing the
// constant changes no wire byte — 038 byte-parity is a property of the bytes, not the source.

namespace GlpRuntime.WireRegistry;

/// <summary>Loud-fail exception for any unknown/duplicate wire-registry lookup (FR-005 discipline).</summary>
public sealed class WireRegistryException : Exception
{
    public WireRegistryException(string message) : base(message) { }
}

/// <summary>
/// Backward-/forward-compatibility posture a wire kind promises to peers (data-model §5).
/// MVP records the declared mode; enforcement of transitive chains is future work.
/// </summary>
public enum CompatMode : byte
{
    /// <summary>New readers understand old writers.</summary>
    Backward = 0,
    /// <summary>Old readers tolerate new writers (additive-optional).</summary>
    Forward = 1,
    /// <summary>Both directions (additive-only evolution).</summary>
    Full = 2,
    /// <summary>Full compatibility that composes across a version chain.</summary>
    Transitive = 3,
}

/// <summary>
/// The canonical payload-type discriminant bytes. These are the SINGLE source of truth —
/// <c>PayloadHeader</c> and <c>ResultEnvelopeCodec</c> alias these (T006), never re-declare them.
/// </summary>
public static class PayloadType
{
    /// <summary>An IL/bytecode program (shipped 029). Was <c>PayloadHeader.IlProgram</c>.</summary>
    public const byte IlProgram = 0x10;

    /// <summary>A result envelope (shipped 038). Was <c>ResultEnvelopeCodec.PayloadTypeResultEnvelope</c>.</summary>
    public const byte ResultEnvelope = 0x11;

    /// <summary>An op-based CRDT / multi-format message document (feature 041). First messaging kind.</summary>
    public const byte CrdtMessage = 0x12;

    /// <summary>Lowest byte reserved for 041+ messaging kinds; kinds below this are pre-041 wire.</summary>
    public const byte MessagingBase = 0x12;
}

/// <summary>One registry row (data-model §5). Immutable value.</summary>
public sealed record WireRegistryEntry(
    byte PayloadType,
    string Functor,
    CompatMode CompatMode,
    string? QmeditDsl = null,
    string? Cddl = null);

/// <summary>
/// The single wire registry. Static, closed table for the shipped + 041-MVP kinds. Lookups
/// loud-fail on an unknown byte or functor; the table is validated once for uniqueness of both
/// <c>payload_type</c> and <c>functor</c> (SC-010 — a duplicate is a build-detectable defect).
/// </summary>
public static class WireRegistry
{
    private static readonly IReadOnlyList<WireRegistryEntry> Entries = Validate(new[]
    {
        new WireRegistryEntry(
            GlpRuntime.WireRegistry.PayloadType.IlProgram,
            "il_program",
            CompatMode.Backward),
        new WireRegistryEntry(
            GlpRuntime.WireRegistry.PayloadType.ResultEnvelope,
            "result_envelope",
            CompatMode.Backward),
        new WireRegistryEntry(
            GlpRuntime.WireRegistry.PayloadType.CrdtMessage,
            "crdt_message",
            CompatMode.Full),
    });

    private static readonly IReadOnlyDictionary<byte, WireRegistryEntry> ByType =
        Entries.ToDictionary(e => e.PayloadType);

    private static readonly IReadOnlyDictionary<string, WireRegistryEntry> ByFunctor =
        Entries.ToDictionary(e => e.Functor);

    /// <summary>All registered rows, in declaration order.</summary>
    public static IReadOnlyList<WireRegistryEntry> All => Entries;

    /// <summary>True if <paramref name="payloadType"/> is a registered kind.</summary>
    public static bool IsKnown(byte payloadType) => ByType.ContainsKey(payloadType);

    /// <summary>Lookup by payload-type byte; loud-fails on an unknown kind (FR-005).</summary>
    public static WireRegistryEntry Lookup(byte payloadType) =>
        ByType.TryGetValue(payloadType, out var e)
            ? e
            : throw new WireRegistryException($"Unknown wire payload type 0x{payloadType:X2}");

    /// <summary>Lookup by registered functor; loud-fails on an unknown functor.</summary>
    public static WireRegistryEntry LookupByFunctor(string functor) =>
        ByFunctor.TryGetValue(functor, out var e)
            ? e
            : throw new WireRegistryException($"Unknown wire functor '{functor}'");

    private static IReadOnlyList<WireRegistryEntry> Validate(WireRegistryEntry[] entries)
    {
        var seenType = new HashSet<byte>();
        var seenFunctor = new HashSet<string>();
        foreach (var e in entries)
        {
            if (!seenType.Add(e.PayloadType))
                throw new WireRegistryException(
                    $"Duplicate wire payload type 0x{e.PayloadType:X2} — the registry must be single-source (SC-010)");
            if (!seenFunctor.Add(e.Functor))
                throw new WireRegistryException(
                    $"Duplicate wire functor '{e.Functor}' — the registry must be single-source (SC-010)");
        }
        return entries;
    }
}
