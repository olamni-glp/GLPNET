// lib/analysis/type_checker/moded_term.cs
//
// Moded terms for GLP type checking.
// Specification: docs/modules/moded-term.md v0.5
// Paper Reference: Definition 4.2 (Moded Term, Complement), lines 179-211
//
// A moded term is a GLP term with mode annotations (↓ consume, ↑ produce)
// on non-variable subterms. Variables have implicit mode based on
// reader/writer status.

using System.Collections.Generic;
using System.Linq;

namespace GlpRuntime.Analysis.TypeChecker;

// =============================================================================
// Moded Term Classes
// =============================================================================

/// <summary>
/// A moded term: a GLP term with mode annotations.
/// <para>
/// Per Definition 4.2: A moded term T' corresponding to a GLP term T is the
/// result of adding a mode annotation (either ↓ consume or ↑ produce) to T
/// and to every non-variable subterm of T.
/// </para>
/// </summary>
public abstract class ModedTerm
{
    /// <summary>The mode annotation of this term.</summary>
    public abstract Mode Mode { get; }

    /// <summary>Accept a visitor.</summary>
    public abstract T Accept<T>(IModedTermVisitor<T> visitor);
}

/// <summary>
/// Visitor interface for moded terms.
/// <para>Pure structural contract (no state, no default implementations) — maps to C# interface.</para>
/// <para>The <c>out T</c> covariance marker is safe because T only appears in return position.</para>
/// </summary>
public interface IModedTermVisitor<out T>
{
    T VisitCompound(ModedCompound term);
    T VisitConstant(ModedConstant term);
    T VisitVariable(ModedVariable term);
}

/// <summary>
/// A compound term with mode annotation.
/// <para>Example: ↓merge(↓[↓X?|Xs?], Ys?, ↑[↑X|Zs])</para>
/// </summary>
public sealed class ModedCompound : ModedTerm, IEquatable<ModedCompound>
{
    public override Mode Mode { get; }
    public string Functor { get; }
    public int Arity { get; }
    /// <summary>Arguments; stored as-passed (no defensive copy), exposed read-only.</summary>
    public IReadOnlyList<ModedTerm> Args { get; }

    public ModedCompound(Mode mode, string functor, int arity, List<ModedTerm> args)
    {
        Mode = mode;
        Functor = functor;
        Arity = arity;
        Args = args;
    }

    /// <summary>Convenience factory for list cons [H|T].</summary>
    public static ModedCompound ListCons(Mode mode, ModedTerm head, ModedTerm tail)
        => new ModedCompound(mode, "[|]", 2, new List<ModedTerm> { head, tail });

    /// <summary>Check if this is a list cons term.</summary>
    public bool IsListCons => Functor == "[|]" && Arity == 2;

    public override T Accept<T>(IModedTermVisitor<T> visitor) => visitor.VisitCompound(this);

    public override string ToString()
    {
        var modeStr = Mode == ModeAliases.Consume ? "↓" : "↑";
        if (IsListCons)
            return $"{modeStr}[{Args[0]}|{Args[1]}]";
        if (Arity == 0)
            return $"{modeStr}{Functor}";
        return $"{modeStr}{Functor}({string.Join(", ", Args)})";
    }

    public bool Equals(ModedCompound? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Mode == other.Mode &&
               Functor == other.Functor &&
               Arity == other.Arity &&
               Args.SequenceEqual(other.Args);
    }

    public override bool Equals(object? obj) => obj is ModedCompound c && Equals(c);

    public override int GetHashCode()
    {
        var hc = new HashCode();
        hc.Add(Mode);
        hc.Add(Functor);
        hc.Add(Arity);
        foreach (var a in Args)
            hc.Add(a);
        return hc.ToHashCode();
    }

    public static bool operator ==(ModedCompound? a, ModedCompound? b)
        => EqualityComparer<ModedCompound>.Default.Equals(a, b);
    public static bool operator !=(ModedCompound? a, ModedCompound? b) => !(a == b);
}

/// <summary>
/// A constant (integer, string, atom) with mode annotation.
/// <para>Example: ↓42, ↑"hello", ↓[]</para>
/// </summary>
public sealed class ModedConstant : ModedTerm, IEquatable<ModedConstant>
{
    public override Mode Mode { get; }

    /// <summary>The constant value: int, double, string, or atom name. Never null.</summary>
    public object Value { get; }

    public ModedConstant(Mode mode, object value)
    {
        Mode = mode;
        Value = value;
    }

    /// <summary>Convenience factory for nil [].</summary>
    public static ModedConstant Nil(Mode mode) => new ModedConstant(mode, "[]");

    /// <summary>Check if this is nil.</summary>
    public bool IsNil => object.Equals(Value, "[]");

    /// <summary>True if value is an integer.</summary>
    public bool IsInteger => Value is int;

    /// <summary>True if value is a real (floating-point) number.</summary>
    public bool IsReal => Value is double;

    /// <summary>True if value is numeric (integer or real). Dart <c>num</c> is closed over int|double only.</summary>
    public bool IsNumeric => Value is int or double;

    /// <summary>True if value is a quoted string.</summary>
    public bool IsString
    {
        get
        {
            if (Value is not string s) return false;
            return (s.StartsWith("\"") && s.EndsWith("\"")) ||
                   (s.StartsWith("'") && s.EndsWith("'"));
        }
    }

    /// <summary>True if value is an unquoted atom (includes nil).</summary>
    public bool IsAtom => Value is string && !IsString;

    public override T Accept<T>(IModedTermVisitor<T> visitor) => visitor.VisitConstant(this);

    public override string ToString()
    {
        var modeStr = Mode == ModeAliases.Consume ? "↓" : "↑";
        return $"{modeStr}{Value}";
    }

    public bool Equals(ModedConstant? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        // Use static object.Equals so boxed int/double/string dispatch to their own Equals.
        return Mode == other.Mode && object.Equals(Value, other.Value);
    }

    public override bool Equals(object? obj) => obj is ModedConstant c && Equals(c);

    public override int GetHashCode() => HashCode.Combine(Mode, Value);

    public static bool operator ==(ModedConstant? a, ModedConstant? b)
        => EqualityComparer<ModedConstant>.Default.Equals(a, b);
    public static bool operator !=(ModedConstant? a, ModedConstant? b) => !(a == b);
}

/// <summary>
/// A variable (reader or writer) with structural mode annotation.
/// <para>
/// Per Remark 4.1 (Structural Mode vs. Variable Mode):
/// <list type="bullet">
///   <item><description>Structural mode: inherited from enclosing type context (stored).</description></item>
///   <item><description>Implicit mode: determined by reader/writer form (computed).</description></item>
/// </list>
/// </para>
/// <para>For well-typing, implicit mode must equal structural mode at variable positions.</para>
/// <para>Example: X? (reader), X (writer)</para>
/// </summary>
public sealed class ModedVariable : ModedTerm, IEquatable<ModedVariable>
{
    public string Name { get; }

    /// <summary>true for reader X?, false for writer X.</summary>
    public bool IsReader { get; }

    /// <summary>Structural mode from parent context. Backing field for overridden Mode property.</summary>
    private readonly Mode structuralModeField;

    public ModedVariable(string name, bool isReader, Mode structuralMode)
    {
        Name = name;
        IsReader = isReader;
        structuralModeField = structuralMode;
    }

    /// <summary>Convenience factory for reader.</summary>
    public static ModedVariable Reader(string name, Mode structuralMode)
        => new ModedVariable(name, isReader: true, structuralMode: structuralMode);

    /// <summary>Convenience factory for writer.</summary>
    public static ModedVariable Writer(string name, Mode structuralMode)
        => new ModedVariable(name, isReader: false, structuralMode: structuralMode);

    /// <summary>Structural mode (used in path traversal for automaton lookup).</summary>
    public override Mode Mode => structuralModeField;

    /// <summary>Implicit mode based on reader/writer form.</summary>
    public Mode ImplicitMode => IsReader ? ModeAliases.Consume : ModeAliases.Produce;

    /// <summary>
    /// Whether this variable is mode-consistent (for well-typing).
    /// Note: convenience property; authoritative check is in Definition 4.3.
    /// </summary>
    public bool IsModeConsistent => ImplicitMode == structuralModeField;

    /// <summary>Whether this is a writer.</summary>
    public bool IsWriter => !IsReader;

    public override T Accept<T>(IModedTermVisitor<T> visitor) => visitor.VisitVariable(this);

    public override string ToString() => IsReader ? $"{Name}?" : Name;

    public bool Equals(ModedVariable? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Name == other.Name &&
               IsReader == other.IsReader &&
               structuralModeField == other.structuralModeField;
    }

    public override bool Equals(object? obj) => obj is ModedVariable v && Equals(v);

    public override int GetHashCode() => HashCode.Combine(Name, IsReader, structuralModeField);

    public static bool operator ==(ModedVariable? a, ModedVariable? b)
        => EqualityComparer<ModedVariable>.Default.Equals(a, b);
    public static bool operator !=(ModedVariable? a, ModedVariable? b) => !(a == b);
}

// =============================================================================
// Moded Path Classes
// =============================================================================

/// <summary>
/// A path through a moded term.
/// <para>
/// A moded path is a sequence from root to leaf:
/// (0, m₀) --> f/n --(i₁, m₁)--&gt; ... --(iₖ, mₖ)--&gt; leaf
/// </para>
/// <para>The leaf is either a constant or a variable.</para>
/// </summary>
public sealed class ModedPath : IEquatable<ModedPath>
{
    /// <summary>Steps; stored as-passed, exposed read-only.</summary>
    public IReadOnlyList<PathStep> Steps { get; }

    public ModedPath(List<PathStep> steps)
    {
        Steps = steps;
    }

    /// <summary>The root step (first step in path).</summary>
    public PathStep Root => Steps[0];

    /// <summary>The leaf step (last step in path).</summary>
    public PathStep Leaf => Steps[Steps.Count - 1];

    /// <summary>Whether this path starts with consume mode (input path).</summary>
    public bool IsInputPath => Root.Mode == ModeAliases.Consume;

    /// <summary>Whether this path starts with produce mode (output path).</summary>
    public bool IsOutputPath => Root.Mode == ModeAliases.Produce;

    /// <summary>Path length (number of steps).</summary>
    public int Length => Steps.Count;

    public override string ToString()
        // Mode rendered via AsModeString() (lowercase "input"/"output") to mirror the
        // Dart Mode.toString() override; the default C# enum ToString() is PascalCase
        // ("Input"/"Output") which diverged from the goldens (e.g. ch05/ex07 mode-error).
        => string.Join(" → ", Steps.Select(s => $"({s.Symbol}, {s.ArgIndex}, {s.Mode.AsModeString()})"));

    public bool Equals(ModedPath? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Steps.SequenceEqual(other.Steps);
    }

    public override bool Equals(object? obj) => obj is ModedPath p && Equals(p);

    public override int GetHashCode()
    {
        var hc = new HashCode();
        foreach (var s in Steps)
            hc.Add(s);
        return hc.ToHashCode();
    }

    public static bool operator ==(ModedPath? a, ModedPath? b)
        => EqualityComparer<ModedPath>.Default.Equals(a, b);
    public static bool operator !=(ModedPath? a, ModedPath? b) => !(a == b);
}

/// <summary>
/// A single step in a moded path.
/// </summary>
public sealed class PathStep : IEquatable<PathStep>
{
    /// <summary>
    /// Symbol at this position:
    /// <list type="bullet">
    ///   <item><description>functor/arity for compound (e.g., "merge/3", "[|]/2")</description></item>
    ///   <item><description>value for constant (e.g., "42", "[]")</description></item>
    ///   <item><description>variable name for variable (e.g., "X", "X?")</description></item>
    /// </list>
    /// </summary>
    public string Symbol { get; }

    /// <summary>Argument index: 0 for root, 1-based for arguments.</summary>
    public int ArgIndex { get; }

    /// <summary>Mode at this position.</summary>
    public Mode Mode { get; }

    /// <summary>True if this step is a variable leaf.</summary>
    public bool IsVariable { get; }

    /// <summary>If IsVariable, whether it's a reader (X?).</summary>
    public bool IsReader { get; }

    public PathStep(string symbol, int argIndex, Mode mode, bool isVariable = false, bool isReader = false)
    {
        Symbol = symbol;
        ArgIndex = argIndex;
        Mode = mode;
        IsVariable = isVariable;
        IsReader = isReader;
    }

    /// <summary>Whether this is a writer variable.</summary>
    public bool IsWriter => IsVariable && !IsReader;

    public override string ToString()
    {
        var modeStr = Mode == ModeAliases.Consume ? "↓" : "↑";
        return $"({Symbol}, {ArgIndex}, {modeStr})";
    }

    public bool Equals(PathStep? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Symbol == other.Symbol &&
               ArgIndex == other.ArgIndex &&
               Mode == other.Mode &&
               IsVariable == other.IsVariable &&
               IsReader == other.IsReader;
    }

    public override bool Equals(object? obj) => obj is PathStep s && Equals(s);

    public override int GetHashCode() => HashCode.Combine(Symbol, ArgIndex, Mode, IsVariable, IsReader);

    public static bool operator ==(PathStep? a, PathStep? b)
        => EqualityComparer<PathStep>.Default.Equals(a, b);
    public static bool operator !=(PathStep? a, PathStep? b) => !(a == b);
}

// =============================================================================
// File-scoped visitor implementations (library-private in Dart → file in C# 11)
// =============================================================================

/// <summary>Visitor: returns true iff all mode annotations are consume (↓).</summary>
file sealed class IsConsumedVisitor : IModedTermVisitor<bool>
{
    internal static readonly IsConsumedVisitor Instance = new();

    public bool VisitCompound(ModedCompound term)
    {
        if (term.Mode != ModeAliases.Consume) return false;
        return term.Args.All(arg => arg.Accept(this));
    }

    public bool VisitConstant(ModedConstant term) => term.Mode == ModeAliases.Consume;

    public bool VisitVariable(ModedVariable term) => term.Mode == ModeAliases.Consume;
}

/// <summary>Visitor: returns true iff all mode annotations are produce (↑).</summary>
file sealed class IsProducedVisitor : IModedTermVisitor<bool>
{
    internal static readonly IsProducedVisitor Instance = new();

    public bool VisitCompound(ModedCompound term)
    {
        if (term.Mode != ModeAliases.Produce) return false;
        return term.Args.All(arg => arg.Accept(this));
    }

    public bool VisitConstant(ModedConstant term) => term.Mode == ModeAliases.Produce;

    public bool VisitVariable(ModedVariable term) => term.Mode == ModeAliases.Produce;
}

/// <summary>Visitor: constructs the dual of a moded term (Definition 5.1). Builds a fresh sub-tree.</summary>
file sealed class DualVisitor : IModedTermVisitor<ModedTerm>
{
    internal static readonly DualVisitor Instance = new();

    public ModedTerm VisitCompound(ModedCompound term)
        => new ModedCompound(
            term.Mode.Flip(),
            term.Functor,
            term.Arity,
            // Fresh list, fresh nodes throughout — no aliasing between input and output.
            term.Args.Select(arg => ModedTermOps.Dual(arg)).ToList());

    public ModedTerm VisitConstant(ModedConstant term)
        => new ModedConstant(term.Mode.Flip(), term.Value);

    public ModedTerm VisitVariable(ModedVariable term)
        // Flip both reader/writer and structural mode.
        => new ModedVariable(term.Name, isReader: !term.IsReader, structuralMode: term.Mode.Flip());
}

// =============================================================================
// Classification / dual / path operations (Dart top-level functions → static class)
// =============================================================================

/// <summary>
/// Host for the library-level top-level functions from the Dart source.
/// Named <c>ModedTermOps</c> to avoid the C# type-name clash with <c>ModedTerm</c>
/// (same precedent as <c>Mode</c>/<c>ModeOps</c> in <c>mode.cs</c>).
/// </summary>
public static class ModedTermOps
{
    /// <summary>
    /// Returns true if all mode annotations in t are consume (↓).
    /// Per spec: returns true iff every non-variable subterm has mode = consume.
    /// </summary>
    public static bool IsConsumed(ModedTerm t) => t.Accept(IsConsumedVisitor.Instance);

    /// <summary>
    /// Returns true if all mode annotations in t are produce (↑).
    /// Per spec: returns true iff every non-variable subterm has mode = produce.
    /// </summary>
    public static bool IsProduced(ModedTerm t) => t.Accept(IsProducedVisitor.Instance);

    /// <summary>
    /// Returns true if t is an I/O moded term.
    /// <para>
    /// An I/O moded term has root mode consume (↓) and on every path from root
    /// to leaf, mode transitions only in the direction ↓ → ↑ (never ↑ → ↓).
    /// </para>
    /// </summary>
    public static bool IsIO(ModedTerm t)
    {
        if (t.Mode != ModeAliases.Consume) return false;
        return AllPathsValidIO(t, ModeAliases.Consume);
    }

    /// <summary>
    /// Constructs the dual of a moded term per Definition 5.1.
    /// The dual is an involution: Dual(Dual(t)) == t.
    /// </summary>
    public static ModedTerm Dual(ModedTerm t) => t.Accept(DualVisitor.Instance);

    /// <summary>
    /// Extracts all moded paths from a moded term.
    /// Returns the set of all paths from root to leaves.
    /// </summary>
    public static HashSet<ModedPath> Paths(ModedTerm t)
    {
        var result = new HashSet<ModedPath>();
        var rootStep = new PathStep(
            symbol: SymbolOf(t),
            argIndex: 0,
            mode: t.Mode,
            isVariable: t is ModedVariable,
            isReader: t is ModedVariable v ? v.IsReader : false);
        ExtractPaths(t, new List<PathStep> { rootStep }, result);
        return result;
    }

    /// <summary>
    /// Helper: check all paths have valid I/O mode transitions.
    /// Valid: ↓→↓, ↓→↑, ↑→↑. Invalid: ↑→↓ (flip back from produce to consume).
    /// </summary>
    private static bool AllPathsValidIO(ModedTerm t, Mode parentMode)
    {
        var currentMode = t.Mode;
        // Invalid transition: ↑→↓
        if (parentMode == ModeAliases.Produce && currentMode == ModeAliases.Consume)
            return false;
        if (t is ModedCompound c)
            return c.Args.All(arg => AllPathsValidIO(arg, currentMode));
        // Leaf reached (constant or variable)
        return true;
    }

    /// <summary>Recursively extract paths from a moded term.</summary>
    private static void ExtractPaths(ModedTerm t, List<PathStep> prefix, HashSet<ModedPath> result)
    {
        if (t is ModedCompound c)
        {
            for (int i = 0; i < c.Arity; i++)
            {
                var child = c.Args[i];
                var childStep = new PathStep(
                    symbol: SymbolOf(child),
                    argIndex: i + 1, // 1-based
                    mode: child.Mode,
                    isVariable: child is ModedVariable,
                    isReader: child is ModedVariable cv ? cv.IsReader : false);
                ExtractPaths(child, new List<PathStep>(prefix) { childStep }, result);
            }
        }
        else
        {
            // Leaf (constant or variable): add path to result
            result.Add(new ModedPath(new List<PathStep>(prefix)));
        }
    }

    /// <summary>Get the symbol representation of a moded term.</summary>
    private static string SymbolOf(ModedTerm t) => t switch
    {
        ModedCompound c => $"{c.Functor}/{c.Arity}",
        ModedConstant c => c.Value.ToString()!,
        ModedVariable v => v.IsReader ? $"{v.Name}?" : v.Name,
        _ => throw new ArgumentException($"Unknown moded term type: {t}"),
    };
}
