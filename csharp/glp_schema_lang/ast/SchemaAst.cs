// AST for the 043 schema DSL (feature 043-xsd-schema-language, T004).
//
// data-model.md §1: immutable records for one authored schema document — named simple types
// (primitive + facets), named complex types (sequence/choice compositions), message-kind
// declarations, per-element occurs bounds, and source locations for construct-level error
// localization (FR-002/FR-014).

namespace GlpRuntime.SchemaLang;

/// <summary>A line:col position in the authored schema text (1-based).</summary>
public readonly record struct SourceLocation(int Line, int Col)
{
    public override string ToString() => $"{Line}:{Col}";
}

/// <summary>
/// Primitive value types (data-model §1). No standalone symbol primitive — symbolic constants
/// are enum members on a str base (analyze I2: str and symbol would both lower to tstr,
/// breaking deterministic lift).
/// </summary>
public enum PrimitiveKind
{
    Int,
    Str,
    Bytes,
    Bool,
}

/// <summary>One authored unit; <see cref="Source"/> is retained verbatim for FR-004.</summary>
public sealed record SchemaDocument(
    string Name,
    int Version,
    IReadOnlyList<NamedType> Types,
    IReadOnlyList<MessageDecl> Messages,
    string Source);

/// <summary>A named type definition; name unique per document (FR-002).</summary>
public abstract record NamedType(string Name, SourceLocation Location);

/// <summary>A user-named simple type: primitive base constrained by facets.</summary>
public sealed record SimpleType(
    string Name,
    SourceLocation Location,
    PrimitiveKind Base,
    IReadOnlyList<Facet> Facets) : NamedType(Name, Location);

/// <summary>A user-named complex type: one sequence or choice composition.</summary>
public sealed record ComplexType(
    string Name,
    SourceLocation Location,
    Composition Composition) : NamedType(Name, Location);

public enum CompositionKind
{
    Sequence,
    Choice,
}

/// <summary>Element names unique within one composition (FR-002).</summary>
public sealed record Composition(CompositionKind Kind, IReadOnlyList<ElementDecl> Elements);

/// <summary>A named slot inside a composition: type reference, optionality, repetition.</summary>
public sealed record ElementDecl(
    string Name,
    TypeRef Type,
    Occurs Occurs,
    SourceLocation Location);

/// <summary>A reference to a named type, a primitive, or a list <c>[T]</c> (schema-dsl.md grammar).</summary>
public abstract record TypeRef(SourceLocation Location);

/// <summary>Reference to a named type in the same document; must resolve (FR-002).</summary>
public sealed record NamedRef(string Name, SourceLocation Location) : TypeRef(Location);

/// <summary>A primitive type used directly at an element position.</summary>
public sealed record PrimitiveRef(PrimitiveKind Kind, SourceLocation Location) : TypeRef(Location);

/// <summary><c>[T]</c> — unbounded list of T (matches the stored qmedit <c>targets: [str]</c>).</summary>
public sealed record ListRef(TypeRef Element, SourceLocation Location) : TypeRef(Location);

/// <summary>Occurs bounds; <c>Max == null</c> means unbounded (<c>*</c>). Default 1..1; 0..1 = optional.</summary>
public sealed record Occurs(int Min, int? Max)
{
    public static readonly Occurs One = new(1, 1);
    public static readonly Occurs Optional = new(0, 1);

    public bool IsDefault => Min == 1 && Max == 1;
    public bool IsOptional => Min == 0 && Max == 1;

    public override string ToString() => Max is null ? $"{Min}..*" : $"{Min}..{Max}";
}

/// <summary>One message kind; lowers to one functor registration (data-model §1).</summary>
public sealed record MessageDecl(string Functor, Composition Body, SourceLocation Location);

// ---------------------------------------------------------------------------
// Facets (on SimpleType only) — data-model §1 facet table.
// ---------------------------------------------------------------------------

public abstract record Facet(SourceLocation Location)
{
    /// <summary>The DSL keyword of this facet, for error messages naming the construct.</summary>
    public abstract string Keyword { get; }
}

/// <summary><c>min</c> — Int only; requires min ≤ max.</summary>
public sealed record MinValueFacet(long Value, SourceLocation Location) : Facet(Location)
{
    public override string Keyword => "min";
}

/// <summary><c>max</c> — Int only.</summary>
public sealed record MaxValueFacet(long Value, SourceLocation Location) : Facet(Location)
{
    public override string Keyword => "max";
}

/// <summary><c>minLength</c> — Str, Bytes; requires 0 ≤ minLength ≤ maxLength.</summary>
public sealed record MinLengthFacet(int Value, SourceLocation Location) : Facet(Location)
{
    public override string Keyword => "minLength";
}

/// <summary><c>maxLength</c> — Str, Bytes.</summary>
public sealed record MaxLengthFacet(int Value, SourceLocation Location) : Facet(Location)
{
    public override string Keyword => "maxLength";
}

/// <summary><c>pattern</c> — Str only; restricted regex subset (research R6), non-empty language.</summary>
public sealed record PatternFacet(string Pattern, SourceLocation Location) : Facet(Location)
{
    public override string Keyword => "pattern";
}

/// <summary>
/// <c>enum(...)</c> — Str, Int. Members stored as their source text; for an Int base every
/// member must be an integer literal; each member must satisfy the co-facets (data-model §1).
/// </summary>
public sealed record EnumerationFacet(IReadOnlyList<string> Members, SourceLocation Location) : Facet(Location)
{
    public override string Keyword => "enum";
}
