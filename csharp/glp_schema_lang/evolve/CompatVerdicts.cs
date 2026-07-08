// Compatibility verdict + override records (feature 043-xsd-schema-language; data-model §6).
//
// Modes are the registry's existing GlpRuntime.WireRegistry.CompatMode — consumed, never
// redefined (spec Assumptions). The CompatChecker rule table itself is evolve/CompatChecker.cs
// (T030); these are the data shapes it produces, defined early because RegistryRecord stores
// the OverrideRecord (US4 AS-3).

using GlpRuntime.WireRegistry;

namespace GlpRuntime.SchemaLang;

public enum CompatOutcome
{
    Compatible,
    Incompatible,
}

public enum CompatDirection
{
    Backward,
    Forward,
}

/// <summary>One breaking construct, naming the rule-table row violated (contracts/compat-evolution.md).</summary>
public sealed record BreakingConstruct(
    string Construct,
    string Location,
    CompatDirection Direction,
    string Rule);

/// <summary>The outcome of checking a proposed version against a declared compatibility mode.</summary>
public sealed record CompatVerdict(
    CompatMode Mode,
    CompatOutcome Outcome,
    IReadOnlyList<BreakingConstruct> Breaks)
{
    public bool IsCompatible => Outcome == CompatOutcome.Compatible;
}

/// <summary>
/// Explicit recorded override required to register an incompatible version (US4 AS-3);
/// stored on the resulting RegistryRecord and retrievable with it.
/// </summary>
public sealed record OverrideRecord(
    CompatVerdict Verdict,
    string Acknowledger,
    string Reason);
