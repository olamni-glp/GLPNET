// Lift result records (feature 043-xsd-schema-language; data-model §5).
//
// FR-009: partial lifts carry a fidelity report marking exactly the unexpressible constructs
// — never a silent approximation. FR-013: drift between the stored registration-time hashes
// and the current registry forms is surfaced on every lift.

namespace GlpRuntime.SchemaLang;

public enum FidelityOutcome
{
    Full,
    Partial,
}

/// <summary>One construct the 043 language cannot faithfully express, captured verbatim.</summary>
public sealed record UnexpressibleConstruct(string CddlConstruct, string Location, string Reason);

public sealed record FidelityReport(
    FidelityOutcome Outcome,
    IReadOnlyList<UnexpressibleConstruct> Unexpressible)
{
    public static readonly FidelityReport Full = new(FidelityOutcome.Full, Array.Empty<UnexpressibleConstruct>());
}

public enum RegistryForm
{
    Cddl,
    Qmedit,
}

/// <summary>Present iff a stored hash no longer matches the current registry form (R9).</summary>
public sealed record DriftReport(RegistryForm Form, string StoredSha256, string CurrentSha256);

/// <summary>The outcome of lifting one registry entry into the XSD-level view.</summary>
public sealed record LiftResult(
    SchemaDocument? Rendering,
    FidelityReport Fidelity,
    DriftReport? Drift);
