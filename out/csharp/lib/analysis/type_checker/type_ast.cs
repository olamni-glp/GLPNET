// lib/analysis/type_checker/type_ast.cs
//
// AST nodes for GLP type declarations following Yardeni-Shapiro.
// Types are first-class syntactic elements parsed alongside clauses.
// Converted from Dart source: lib/analysis/type_checker/type_ast.dart
// source_sha256: f80349aefb8cc777764548f29d5c6bc663809f9dfffde921c141ae2f7028d38a

using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace GlpRuntime.Analysis.TypeChecker;

// ---------------------------------------------------------------------------
// T1 — Enum
// ---------------------------------------------------------------------------

/// <summary>
/// Classification of types by mode structure.
/// Per spec (type-environment.md): Types are classified based on internal complementation.
/// </summary>
public enum TypeClassification
{
    Output,      // No complementation in definition (pure output structure)
    Input,       // Complement of an output type (not directly defined)
    Interactive  // Contains internal complementation (_? or T? in alternatives)
}

// ---------------------------------------------------------------------------
// T2 + T3 — TypeExpr base class (including promoted extension members)
// ---------------------------------------------------------------------------

/// <summary>Base class for type expressions.</summary>
public abstract class TypeExpr
{
    /// <summary>Line number in source.</summary>
    public int Line { get; }

    /// <summary>Column number in source.</summary>
    public int Column { get; }

    protected TypeExpr(int line, int column)
    {
        Line   = line;
        Column = column;
    }

    // -----------------------------------------------------------------------
    // T3 — Promoted from Dart extension ProcArgTypeExpr on TypeExpr.
    // Each Dart `if (this is T) return (this as T).m;` collapses to a single
    // declaration-pattern arm `T t => t.m` in a switch expression.
    // -----------------------------------------------------------------------

    /// <summary>
    /// Whether this type expression represents an input (consume) mode.
    /// TypeRef with IsInput=true → true; PrimitiveModeAlt with IsInput=true → true; otherwise false.
    /// </summary>
    public bool IsInputMode => this switch
    {
        TypeRef r          => r.IsInput,
        PrimitiveModeAlt p => p.IsInput,
        _                  => false
    };

    /// <summary>
    /// The type name for named types, or null for primitives.
    /// TypeRef → the Name; anything else → null.
    /// </summary>
    public string? TypeName => this switch
    {
        TypeRef r => r.Name,
        _         => null
    };

    /// <summary>Whether this is a primitive type (_ or _?).</summary>
    public bool IsPrimitive => this is PrimitiveModeAlt;
}

// ---------------------------------------------------------------------------
// T4 + T5 + T6 — TypeRef (structural equality, static sets, Dual)
// ---------------------------------------------------------------------------

/// <summary>
/// Reference to a named type: Nat, List, Number, String, Any.
/// Optionally with input mode annotation (Type?).
/// Optionally with type arguments for parameterized types: Stream(Integer).
/// </summary>
public sealed class TypeRef : TypeExpr, IEquatable<TypeRef>
{
    // T5 — static const sets → static readonly FrozenSet with StringComparer.Ordinal.
    // C# has no const collection; FrozenSet<string> with ordinal comparer reproduces
    // Dart exact-string Set<String> semantics (no culture-sensitive drift).

    /// <summary>Primitive types (not defined via ::=, handled specially by compiler).</summary>
    public static readonly FrozenSet<string> Builtins =
        new[] { "Integer", "Real", "Number", "String" }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>System types (defined via ::= but not redefinable by user).</summary>
    public static readonly FrozenSet<string> SystemTypes =
        new[] { "Any", "List" }.ToFrozenSet(StringComparer.Ordinal);

    public string Name { get; }
    public bool IsInput { get; }                        // true if Type?, false if Type
    public IReadOnlyList<TypeExpr> TypeArgs { get; }    // e.g. [TypeRef("Integer")] for Stream(Integer)

    public TypeRef(string name, int line, int column,
                   bool isInput = false,
                   IReadOnlyList<TypeExpr>? typeArgs = null)
        : base(line, column)
    {
        Name     = name;
        IsInput  = isInput;
        TypeArgs = typeArgs ?? Array.Empty<TypeExpr>();
    }

    /// <summary>Whether this is a parameterized type reference.</summary>
    public bool IsParameterized => TypeArgs.Count > 0;

    /// <summary>Whether this type name is a built-in primitive.</summary>
    public bool IsBuiltin => Builtins.Contains(Name);

    /// <summary>
    /// Mode dual operator per Definition 5.1.
    /// Returns a new TypeRef with inverted mode, SHARING the same TypeArgs reference
    /// (shallow alias — matches Dart `dual()` which passes the same list reference).
    /// </summary>
    public TypeRef Dual() =>
        new TypeRef(Name, Line, Column, isInput: !IsInput, typeArgs: TypeArgs);

    public override string ToString()
    {
        string argsStr = TypeArgs.Count > 0 ? $"({string.Join(", ", TypeArgs)})" : "";
        return IsInput ? $"{Name}{argsStr}?" : $"{Name}{argsStr}";
    }

    // T6 — Hand-written IEquatable<TypeRef> with SequenceEqual.
    // A positional `record` is REJECTED: record equality on List<> compares by reference,
    // silently breaking Stream(Integer) structural equality (see convspec nuance C4).

    /// <summary>Structural equality: Name, IsInput, and element-wise TypeArgs.</summary>
    public bool Equals(TypeRef? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Name == other.Name
            && IsInput == other.IsInput
            && TypeArgs.SequenceEqual(other.TypeArgs);
    }

    public override bool Equals(object? obj) => Equals(obj as TypeRef);

    public override int GetHashCode()
    {
        var hc = new HashCode();
        hc.Add(Name);
        hc.Add(IsInput);
        foreach (var arg in TypeArgs)
            hc.Add(arg);
        return hc.ToHashCode();
    }

    public static bool operator ==(TypeRef? left, TypeRef? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(TypeRef? left, TypeRef? right) =>
        !(left == right);
}

// ---------------------------------------------------------------------------
// T7 — Six non-equality leaf classes (reference identity, no Equals/GetHashCode override)
// ---------------------------------------------------------------------------

/// <summary>A constant alternative in a type: 0, [], foo.</summary>
public sealed class ConstantAlt : TypeExpr
{
    /// <summary>String (atom), int, or double.</summary>
    public object Value { get; }

    public ConstantAlt(object value, int line, int column) : base(line, column)
    {
        Value = value;
    }

    public override string ToString() => Value.ToString() ?? "";
}

/// <summary>A structure alternative: s(Nat), tree(Nat, Tree, Tree).</summary>
public sealed class StructAlt : TypeExpr
{
    public string Functor { get; }
    public IReadOnlyList<TypeExpr> Args { get; }

    public StructAlt(string functor, IReadOnlyList<TypeExpr> args, int line, int column)
        : base(line, column)
    {
        Functor = functor;
        Args    = args;
    }

    /// <summary>Arity of the structure.</summary>
    public int Arity => Args.Count;

    public override string ToString() => $"{Functor}({string.Join(", ", Args)})";
}

/// <summary>Empty list alternative: [].</summary>
public sealed class ListNilAlt : TypeExpr
{
    public ListNilAlt(int line, int column) : base(line, column) { }

    public override string ToString() => "[]";
}

/// <summary>List cons alternative: [Head | Tail].</summary>
public sealed class ListConsAlt : TypeExpr
{
    public TypeExpr Head { get; }
    public TypeExpr Tail { get; }

    public ListConsAlt(TypeExpr head, TypeExpr tail, int line, int column)
        : base(line, column)
    {
        Head = head;
        Tail = tail;
    }

    public override string ToString() => $"[{Head} | {Tail}]";
}

/// <summary>
/// Primitive mode type alternative: _ (output) or _? (input).
/// Used in type definitions like: Any ::= _ ; _?.
/// </summary>
public sealed class PrimitiveModeAlt : TypeExpr
{
    /// <summary>false = _ (output), true = _? (input).</summary>
    public bool IsInput { get; }

    public PrimitiveModeAlt(bool isInput, int line, int column) : base(line, column)
    {
        IsInput = isInput;
    }

    public override string ToString() => IsInput ? "_?" : "_";
}

/// <summary>
/// Difference list alternative: List \ List?.
/// Used for DiffList type: DiffList ::= List \ List?.
/// </summary>
public sealed class DiffListAlt : TypeExpr
{
    /// <summary>The content list.</summary>
    public TypeExpr Content { get; }
    /// <summary>The hole/tail.</summary>
    public TypeExpr Hole { get; }

    public DiffListAlt(TypeExpr content, TypeExpr hole, int line, int column)
        : base(line, column)
    {
        Content = content;
        Hole    = hole;
    }

    public override string ToString() => $"{Content} \\ {Hole}";
}

// ---------------------------------------------------------------------------
// T8 — TypeDef
// ---------------------------------------------------------------------------

/// <summary>
/// A type definition: TypeName ::= alt1 ; alt2 ; ... .
/// Parameterized types have non-empty TypeParams: Stream(X) ::= [] ; [X | Stream(X)].
/// </summary>
public sealed class TypeDef
{
    public string Name { get; }
    /// <summary>e.g. ["X"] for Stream(X), empty for monomorphic.</summary>
    public IReadOnlyList<string> TypeParams { get; }
    public IReadOnlyList<TypeExpr> Alternatives { get; }
    public int Line { get; }
    public int Column { get; }

    public TypeDef(string name, IReadOnlyList<TypeExpr> alternatives, int line, int column,
                   IReadOnlyList<string>? typeParams = null)
    {
        Name         = name;
        Alternatives = alternatives;
        Line         = line;
        Column       = column;
        TypeParams   = typeParams ?? Array.Empty<string>();
    }

    /// <summary>Whether this is a parameterized type definition.</summary>
    public bool IsParameterized => TypeParams.Count > 0;

    /// <summary>
    /// Classify this type based on mode structure.
    /// Per spec (type-environment.md v0.5):
    ///   output: no complementation in any alternative
    ///   interactive: contains internal complementation (_? or T?)
    /// </summary>
    public TypeClassification Classification =>
        Alternatives.Any(ContainsComplement)
            ? TypeClassification.Interactive
            : TypeClassification.Output;

    /// <summary>Check if a type expression contains any complementation.</summary>
    private static bool ContainsComplement(TypeExpr expr) => expr switch
    {
        TypeRef { IsInput: true }          => true,
        PrimitiveModeAlt { IsInput: true } => true,
        ListConsAlt c                      => ContainsComplement(c.Head) || ContainsComplement(c.Tail),
        StructAlt s                        => s.Args.Any(ContainsComplement),
        DiffListAlt d                      => ContainsComplement(d.Content) || ContainsComplement(d.Hole),
        _                                  => false
    };

    public override string ToString() =>
        $"{Name} ::= {string.Join(" ; ", Alternatives)}.";
}

// ---------------------------------------------------------------------------
// T9 — ProcDecl
// ---------------------------------------------------------------------------

/// <summary>
/// A procedure declaration: procedure name(Type1, Type2, ...).
///
/// Argument types can be TypeRef (a named type reference, e.g. Nat, Stream?)
/// or PrimitiveModeAlt (a primitive type directly, e.g. _, _?).
///
/// Parameterized procedure declarations have non-empty TypeParams:
///   procedure gethead(Stream(X)?, X).  → TypeParams: ["X"]
/// These are templates instantiated per call site by the type checker.
/// </summary>
public sealed class ProcDecl
{
    public string Name { get; }
    /// <summary>TypeRef or PrimitiveModeAlt elements.</summary>
    public IReadOnlyList<TypeExpr> ArgTypes { get; }
    /// <summary>e.g. ["X"] for parameterized proc decls, empty for monomorphic.</summary>
    public IReadOnlyList<string> TypeParams { get; }
    public int Line { get; }
    public int Column { get; }
    /// <summary>True if implemented in the runtime (no GLP clauses).</summary>
    public bool IsBuiltin { get; }
    /// <summary>True if declared with 'exported procedure'.</summary>
    public bool Exported { get; }
    /// <summary>True if declared with 'imported procedure'.</summary>
    public bool Imported { get; }
    /// <summary>For imported procedures: module path (e.g. 'social' or 'ui#actors'), null for ancestor scope.</summary>
    public string? ModulePath { get; }

    public ProcDecl(
        string name,
        IReadOnlyList<TypeExpr> argTypes,
        int line,
        int column,
        IReadOnlyList<string>? typeParams = null,
        bool isBuiltin  = false,
        bool exported   = false,
        bool imported   = false,
        string? modulePath = null)
    {
        Name       = name;
        ArgTypes   = argTypes;
        Line       = line;
        Column     = column;
        TypeParams = typeParams ?? Array.Empty<string>();
        IsBuiltin  = isBuiltin;
        Exported   = exported;
        Imported   = imported;
        ModulePath = modulePath;
    }

    /// <summary>Whether this is a parameterized procedure declaration.</summary>
    public bool IsParameterized => TypeParams.Count > 0;

    /// <summary>Arity (number of argument types).</summary>
    public int Arity => ArgTypes.Count;

    /// <summary>Local key: "name/arity".</summary>
    public string Key => $"{Name}/{Arity}";

    /// <summary>
    /// Key for TypeEnvironment lookup, including module path for imported procedures.
    ///   Local/exported: 'factorial/2'
    ///   Imported with path: 'math#factorial/2'
    ///   Imported from ancestor (no path): 'factorial/2'
    /// </summary>
    public string QualifiedKey => $"{QualifiedName}/{Arity}";

    /// <summary>Full qualified name (with module path for imported procedures).</summary>
    public string QualifiedName =>
        ModulePath is not null ? $"{ModulePath}#{Name}" : Name;

    /// <summary>The visibility prefix for this declaration.</summary>
    private string VisibilityPrefix =>
        Exported ? "exported " :
        Imported ? "imported " :
        "";

    /// <summary>Get the mode for argument at index i (true = input mode).</summary>
    public bool IsInputArg(int i) => ArgTypes[i] switch
    {
        TypeRef r          => r.IsInput,
        PrimitiveModeAlt p => p.IsInput,
        _                  => false
    };

    /// <summary>
    /// Get the base type name for argument at index i.
    /// Returns null for primitive types (_ or _?).
    /// </summary>
    public string? GetTypeName(int i) =>
        ArgTypes[i] is TypeRef r ? r.Name : null;

    public override string ToString() =>
        $"{VisibilityPrefix}procedure {QualifiedName}({string.Join(", ", ArgTypes)}).";
}

// ---------------------------------------------------------------------------
// T10 — TypeEnvironment
// ---------------------------------------------------------------------------

/// <summary>The type environment: all type definitions and procedure declarations in a module.</summary>
public sealed class TypeEnvironment
{
    /// <summary>Type definitions, keyed by type name.</summary>
    public Dictionary<string, TypeDef> Types { get; }

    /// <summary>Procedure declarations, keyed by "name/arity".</summary>
    public Dictionary<string, ProcDecl> Procedures { get; }

    /// <summary>
    /// Parameterized procedure declaration templates, keyed by "name/arity".
    /// Used for call-site type parameter inference (Case B).
    /// </summary>
    public Dictionary<string, ProcDecl> ParamProcDecls { get; }

    /// <summary>
    /// Parameterized type templates from prelude/ancestors.
    /// Read-only: the Dart source defaults this to const {} (immutable empty map).
    /// </summary>
    public IReadOnlyDictionary<string, TypeDef> TypeTemplates { get; }

    public TypeEnvironment(
        Dictionary<string, TypeDef> types,
        Dictionary<string, ProcDecl> procedures,
        Dictionary<string, ProcDecl>? paramProcDecls = null,
        IReadOnlyDictionary<string, TypeDef>? typeTemplates = null)
    {
        Types          = types;
        Procedures     = procedures;
        ParamProcDecls = paramProcDecls ?? new Dictionary<string, ProcDecl>();
        // Dart `const {}` default is immutable; use ImmutableDictionary.Empty
        // so we never share a mutable static default across instances.
        TypeTemplates  = typeTemplates ?? ImmutableDictionary<string, TypeDef>.Empty;
    }

    /// <summary>
    /// Static factory — equivalent to Dart `factory TypeEnvironment.empty()`.
    /// C# has no `factory` keyword; a static method is the canonical equivalent.
    /// </summary>
    public static TypeEnvironment Empty() =>
        new TypeEnvironment(new Dictionary<string, TypeDef>(), new Dictionary<string, ProcDecl>());

    /// <summary>
    /// Merge another environment into this one, returning a new environment.
    /// Dart map-spread `{...a, ...b}` has last-wins / right-bias on duplicate keys;
    /// reproduced via C# indexer upsert (dict[k] = v).
    /// Dictionary.Add would throw on duplicates — rejected.
    /// </summary>
    public TypeEnvironment Merge(TypeEnvironment other)
    {
        var mergedTypes = new Dictionary<string, TypeDef>(Types);
        foreach (var kv in other.Types)
            mergedTypes[kv.Key] = kv.Value;

        var mergedProcs = new Dictionary<string, ProcDecl>(Procedures);
        foreach (var kv in other.Procedures)
            mergedProcs[kv.Key] = kv.Value;

        var mergedParam = new Dictionary<string, ProcDecl>(ParamProcDecls);
        foreach (var kv in other.ParamProcDecls)
            mergedParam[kv.Key] = kv.Value;

        var mergedTemplates = new Dictionary<string, TypeDef>(TypeTemplates);
        foreach (var kv in other.TypeTemplates)
            mergedTemplates[kv.Key] = kv.Value;

        return new TypeEnvironment(mergedTypes, mergedProcs, mergedParam, mergedTemplates);
    }

    /// <summary>
    /// Look up a type definition.
    /// RENAMED from Dart getType → LookupType to avoid shadowing object.GetType()
    /// (project-wide getX → LookupX idiom; convspec rf-dart-getx-rename-avoiding-object-member-shadow).
    /// </summary>
    public TypeDef? LookupType(string name) =>
        Types.TryGetValue(name, out var t) ? t : null;

    /// <summary>Look up a procedure declaration by name and arity.</summary>
    public ProcDecl? GetProcedure(string name, int arity) =>
        Procedures.TryGetValue($"{name}/{arity}", out var p) ? p : null;

    /// <summary>Check if a type name is defined (including built-ins).</summary>
    public bool HasType(string name) =>
        Types.ContainsKey(name) || TypeRef.Builtins.Contains(name);

    /// <summary>Check if a procedure is defined.</summary>
    public bool HasProcedure(string name, int arity) =>
        Procedures.ContainsKey($"{name}/{arity}");

    /// <summary>Add a type definition to the environment (upsert by Name).</summary>
    public void AddType(TypeDef typeDef)
    {
        Types[typeDef.Name] = typeDef;
    }

    /// <summary>
    /// Add a procedure declaration to the environment.
    /// Keys by QualifiedKey (may include module path) — asymmetric vs GetProcedure
    /// which keys by "name/arity". Asymmetry preserved verbatim from Dart source.
    /// </summary>
    public void AddProcedure(ProcDecl procDecl)
    {
        Procedures[procDecl.QualifiedKey] = procDecl;
    }

    public override string ToString()
    {
        var sb = new StringBuilder();
        foreach (var t in Types.Values)
            sb.AppendLine(t.ToString());
        foreach (var p in Procedures.Values)
            sb.AppendLine(p.ToString());
        return sb.ToString();
    }
}
