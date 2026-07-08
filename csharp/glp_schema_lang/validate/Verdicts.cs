// Verdict + structured error records (feature 043-xsd-schema-language, T005).
//
// data-model.md §4/§7, research R12: one structured error/verdict family for all five
// operation classes, each carrying {operation, construct, location, message}. Loud-fail
// everywhere (FR-014); nothing falls back silently. Exceptions are reserved for the cases the
// contracts explicitly specify as thrown (NoSchemaRegisteredError — validation-api.md; LiftError)
// and for programming errors, mirroring WireRegistryException style.

namespace GlpRuntime.SchemaLang;

// ---------------------------------------------------------------------------
// Neutral instance form (data-model §4, research R7)
// ---------------------------------------------------------------------------

/// <summary>
/// Neutral ground-term tree — the only instance-validation input type. Adapters map decoded
/// messages into this form (the reference Message → InstanceValue adapter lives in the tests
/// project); symbolic enum values map to <see cref="Str"/> (or <see cref="Int"/> for numeric
/// enums). No new wire formats or codecs (spec Assumptions).
/// </summary>
public abstract record InstanceValue
{
    public sealed record Int(long Value) : InstanceValue;

    public sealed record Str(string Value) : InstanceValue;

    public sealed record Bytes(IReadOnlyList<byte> Value) : InstanceValue;

    public sealed record Bool(bool Value) : InstanceValue;

    public sealed record List(IReadOnlyList<InstanceValue> Items) : InstanceValue;

    /// <summary>Named struct with ordered name→value fields.</summary>
    public sealed record Struct(string Name, IReadOnlyList<KeyValuePair<string, InstanceValue>> Fields) : InstanceValue;

    public static InstanceValue OfStruct(string name, params (string Name, InstanceValue Value)[] fields) =>
        new Struct(name, fields.Select(f => new KeyValuePair<string, InstanceValue>(f.Name, f.Value)).ToList());

    public static InstanceValue OfList(params InstanceValue[] items) => new List(items);
}

// ---------------------------------------------------------------------------
// Instance-validation verdict (data-model §4, FR-006)
// ---------------------------------------------------------------------------

public enum ConstructKind
{
    Element,
    Facet,
    Composition,
    Kind,
}

/// <summary>One localized instance-validation failure (FR-006 localization).</summary>
public sealed record Violation(
    ConstructKind ConstructKind,
    string ConstructName,
    string SchemaLocation,
    string InstancePath,
    string Message);

/// <summary>Pass, or Fail with every violation named and located — never silent.</summary>
public sealed record ValidationVerdict(IReadOnlyList<Violation> Violations)
{
    public static readonly ValidationVerdict Pass = new(Array.Empty<Violation>());

    public static ValidationVerdict Fail(IReadOnlyList<Violation> violations) =>
        violations.Count == 0
            ? throw new InvalidOperationException("Fail verdict requires at least one violation")
            : new ValidationVerdict(violations);

    public bool IsPass => Violations.Count == 0;
}

// ---------------------------------------------------------------------------
// Structured error records (data-model §7, research R12)
// ---------------------------------------------------------------------------

/// <summary>One schema-document validation error; document validation returns ALL of them in one pass (FR-002).</summary>
public sealed record SchemaValidationError(
    string Operation,
    string Construct,
    SourceLocation Location,
    string Message)
{
    public override string ToString() => $"[{Operation}] {Construct} at {Location}: {Message}";
}

/// <summary>Result of schema-document validation: a document, or the full error list.</summary>
public sealed record SchemaValidationResult(
    SchemaDocument? Document,
    IReadOnlyList<SchemaValidationError> Errors)
{
    public bool IsValid => Document is not null && Errors.Count == 0;

    public static SchemaValidationResult Ok(SchemaDocument document) =>
        new(document, Array.Empty<SchemaValidationError>());

    public static SchemaValidationResult Invalid(IReadOnlyList<SchemaValidationError> errors) =>
        errors.Count == 0
            ? throw new InvalidOperationException("Invalid result requires at least one error")
            : new SchemaValidationResult(null, errors);
}

public enum LoweringErrorKind
{
    /// <summary>Functor or payload-type already present in seed ∪ overlay (US1 AS-3).</summary>
    Collision,

    /// <summary>A construct combination the CDDL/functor layer cannot carry (spec edge case).</summary>
    Unlowerable,

    /// <summary>No free payload-type byte remains in the 8-bit space (research R3).</summary>
    ByteSpaceExhausted,
}

/// <summary>
/// Lowering/registration failure. All-or-nothing law: when this is produced, NOTHING was
/// written. <see cref="Constructs"/> lists every offending construct (unlowerable) or the
/// colliding functor/byte with the existing-entry identity (collision).
/// </summary>
public sealed record LoweringError(
    LoweringErrorKind Kind,
    IReadOnlyList<string> Constructs,
    string Message);

/// <summary>
/// FR-008: requesting validation for a message kind with no registered schema is an explicit
/// error, distinct from a Fail verdict — never a silent pass. Thrown per validation-api.md.
/// </summary>
public sealed class NoSchemaRegisteredError : Exception
{
    public NoSchemaRegisteredError(string functor)
        : base($"no schema registered for functor '{functor}'")
    {
        Functor = functor;
    }

    public string Functor { get; }
}

/// <summary>Lift failure for a functor that resolves to no registry entry (FR-009 loud-fail).</summary>
public sealed class LiftError : Exception
{
    public LiftError(string functor, string message)
        : base($"lift of functor '{functor}' failed: {message}")
    {
        Functor = functor;
    }

    public string Functor { get; }
}

/// <summary>
/// Refusal record (clarification 3, FR-011): evolution check/registration of a new version
/// refuses until a compatibility mode is declared for the type — never a silently assumed
/// default mode.
/// </summary>
public sealed record NoCompatModeDeclaredError(string Functor)
{
    public string Message =>
        $"no compatibility mode declared for '{Functor}' — declare a mode before checking or registering a new version";
}

/// <summary>
/// Refusal record (FR-014): a proposed version's number must strictly exceed the latest
/// stored version for every previously-registered kind in the document — re-registering the
/// same version or a lower one would make the chain ordering (and "latest" lookups)
/// ambiguous, never a silent append.
/// </summary>
public sealed record VersionNotMonotonicError(string Functor, int ProposedVersion, int LatestVersion)
{
    public string Message =>
        $"proposed version {ProposedVersion} for '{Functor}' does not exceed the latest registered version {LatestVersion} — version numbers must be strictly increasing";
}
