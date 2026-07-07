// Lowering + payload-type allocation (feature 043-xsd-schema-language, T016; research R3/R5).
//
// Contract: specs/043-xsd-schema-language/contracts/lowering.md.
// `Lowering.Lower(doc[, registry])` → LoweringArtifactSet | LoweringError. One CDDL artifact
// per schema document; one FunctorRegistration per `message` declaration. Payload types
// allocate deterministically: a functor already registered in seed ∪ overlay REUSES its
// registered byte (a kind keeps its byte across versions, compat-evolution.md); genuinely new
// functors take the lowest free byte ≥ 0x13 (MessagingBase + 1) in seed ∪ overlay, in message
// declaration order. Unlowerable constructs produce a LoweringError listing EVERY
// offending construct; nothing is emitted partially (all-or-nothing, FR-003 edge case).
// Lowering is a pure function of (document, registry state) — no timestamps, no randomness,
// no unordered iteration (FR-005, research R11).

using GlpRuntime.WireRegistry;

namespace GlpRuntime.SchemaLang;

/// <summary>One functor registration produced by lowering (data-model §3). The declared
/// CompatMode is stamped at registration time (clarification 3 — mandatory there).</summary>
public sealed record FunctorRegistration(string Functor, byte PayloadType, CompatMode? CompatMode = null);

/// <summary>The CDDL artifact + functor registration(s) produced from one schema document.</summary>
public sealed record LoweringArtifactSet(string Cddl, IReadOnlyList<FunctorRegistration> Registrations);

/// <summary>Union result: artifacts on success, a LoweringError otherwise — never both.</summary>
public sealed record LoweringResult(LoweringArtifactSet? Artifacts, LoweringError? Error)
{
    public bool IsSuccess => Error is null;
}

public static class Lowering
{
    /// <summary>Lower against a fresh seeded registry view (seed only — first free byte 0x13).</summary>
    public static LoweringResult Lower(SchemaDocument doc) => Lower(doc, new SchemaLangRegistry());

    /// <summary>Lower against a live registry so allocation sees seed ∪ overlay (research R3).</summary>
    public static LoweringResult Lower(SchemaDocument doc, SchemaLangRegistry registry)
    {
        var unlowerable = new List<string>();
        var cddl = CddlEmitter.Emit(doc, unlowerable);
        if (unlowerable.Count > 0)
            return new LoweringResult(null, new LoweringError(
                LoweringErrorKind.Unlowerable,
                unlowerable,
                $"schema '{doc.Name}' contains {unlowerable.Count} construct(s) the CDDL/functor layer cannot carry — nothing was lowered"));

        var used = new HashSet<byte>(registry.UsedPayloadTypes);
        var registrations = new List<FunctorRegistration>();
        foreach (var message in doc.Messages)
        {
            // A functor already registered keeps its byte across versions (compat-evolution.md:
            // "A new version of a kind keeps the kind's payload-type byte") — reuse it instead
            // of consuming a fresh candidate, so (a) the genuinely NEW kinds of a version
            // document get the true lowest free bytes, and (b) a pure version bump needs no
            // free bytes at all (no spurious ByteSpaceExhausted near exhaustion).
            if (registry.HasFunctor(message.Functor))
            {
                registrations.Add(new FunctorRegistration(
                    message.Functor, registry.LookupByFunctor(message.Functor).PayloadType));
                continue;
            }
            byte? allocated = null;
            for (var candidate = PayloadType.MessagingBase + 1; candidate <= byte.MaxValue; candidate++)
            {
                if (used.Contains((byte)candidate)) continue;
                allocated = (byte)candidate;
                break;
            }
            if (allocated is null)
                return new LoweringResult(null, new LoweringError(
                    LoweringErrorKind.ByteSpaceExhausted,
                    new[] { message.Functor },
                    $"no free payload-type byte ≥ 0x{PayloadType.MessagingBase + 1:X2} remains for functor '{message.Functor}'"));
            used.Add(allocated.Value);
            registrations.Add(new FunctorRegistration(message.Functor, allocated.Value));
        }
        return new LoweringResult(new LoweringArtifactSet(cddl, registrations), null);
    }
}
