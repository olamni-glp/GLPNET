// lib/analysis/type_checker/moded_head.cs
//
// Moded head construction for GLP type checking.
// Specification: docs/type system/moded-head.md v0.8
// Paper Reference: Definition 5.5 (moded head)
//
// Constructs a moded head H' from a clause head H and a procedure declaration.
// The moded head is used for well-typing checks.

using System.Collections.Generic;
using System.Linq;
using GlpRuntime.Compiler;

namespace GlpRuntime.Analysis.TypeChecker;

// =============================================================================
// ModedHead — host static class for all top-level members
// =============================================================================

/// <summary>
/// Moded head construction per Definition 5.5.
/// <para>
/// Turns a Dart-AST clause head + procedure declaration into a <see cref="ModedTerm"/>
/// tree with all variables flipped (reader↔writer), and builds the analogous
/// produced term for body atoms (no flip).
/// </para>
/// </summary>
public static class ModedHead
{
    // =========================================================================
    // Counter for anonymous variable names
    // Bug 4 fix: Each occurrence of _ must be treated as a fresh, independent variable
    // Paper Remark 3.1: "Each occurrence [of _] denotes a fresh writer"
    // Single-threaded contract preserved verbatim from Dart isolate model — NO volatile/Interlocked.
    // =========================================================================

    private static int _anonVarCounter = 0;

    /// <summary>
    /// Reset the anonymous variable counter.
    /// Call once at the start of each clause (before processing head and body atoms).
    /// </summary>
    public static void ResetAnonVarCounter() { _anonVarCounter = 0; }

    /// <summary>Generate a unique name for an anonymous variable: "_#1", "_#2", etc.</summary>
    private static string FreshAnonVarName() { _anonVarCounter++; return $"_#{_anonVarCounter}"; }

    // =========================================================================
    // Public entry points
    // =========================================================================

    /// <summary>
    /// Constructs a moded head H' from clause head H per Definition 5.5.
    /// <para>
    /// Given a head H, a moded head H' is obtained by:
    /// 1. Constructing an I/O-moded term corresponding to H, then
    /// 2. Replacing each variable by its paired variable (X ↔ X?)
    /// </para>
    /// <para>
    /// The variable flip captures inverted roles at the call boundary:
    /// — Head writer X becomes reader X? (bound BY the goal)
    /// — Head reader X? becomes writer X (will be bound BY the body)
    /// </para>
    /// </summary>
    /// <exception cref="ArityMismatchError">If head arity does not match declaration arity.</exception>
    public static ModedTerm Build(Goal head, ProcDecl decl, TypeEnvironment? typeEnv = null)
    {
        // Reset counter for each clause to ensure unique names within the clause
        ResetAnonVarCounter();
        // Validate arity
        if (head.Arity != decl.Arity)
            throw new ArityMismatchError(
                $"Head arity {head.Arity} does not match declaration arity {decl.Arity}");

        // Step 1: Build I/O moded term (root mode = consume)
        var ioTerm = BuildIOModedTerm(head, decl, ModeAliases.Consume, typeEnv);

        // Step 2: Ensure each variable's form matches its position's mode
        return EnsureVariablesMatchModes(ioTerm);
    }

    /// <summary>
    /// Constructs a produced moded term from a body atom.
    /// <para>
    /// Body atoms are goals being called, not definitions being matched.
    /// Variables stay as-is (no flip) because the body atom represents the caller's perspective.
    /// </para>
    /// <para>
    /// Postconditions: root mode is ↑ (produce); variables are NOT flipped.
    /// </para>
    /// </summary>
    /// <exception cref="ArityMismatchError">If atom arity does not match declaration arity.</exception>
    public static ModedTerm ProducedTerm(Goal atom, ProcDecl decl, TypeEnvironment? typeEnv = null)
    {
        // NOTE: do NOT reset the counter here — body atoms share the clause's anon-var namespace with the head
        // Validate arity
        if (atom.Arity != decl.Arity)
            throw new ArityMismatchError(
                $"Atom arity {atom.Arity} does not match declaration arity {decl.Arity}");

        // Build I/O moded term with root mode = produce; NO variable flip for body atoms
        return BuildIOModedTerm(atom, decl, ModeAliases.Produce, typeEnv);
    }

    // =========================================================================
    // Internal helpers
    // =========================================================================

    /// <summary>
    /// Build an I/O moded term from a goal/head.
    /// Root mode is <paramref name="parentMode"/> (consume for heads, produce for body atoms).
    /// Input arguments (Type?) have mode ↓; output arguments (Type) have mode ↑.
    /// </summary>
    private static ModedTerm BuildIOModedTerm(Goal term, ProcDecl decl, Mode parentMode, TypeEnvironment? typeEnv)
    {
        // Pre-sized list — three parallel index-aligned reads; NOT LINQ (rf-dart-private-toplevel-fn spec)
        var modedArgs = new List<ModedTerm>(term.Args.Count);
        for (int i = 0; i < term.Args.Count; i++)
        {
            var argType = decl.ArgTypes[i];
            // Input (Type? or _?) → consume, Output (Type or _) → produce
            var argMode = decl.IsInputArg(i) ? ModeAliases.Consume : ModeAliases.Produce;
            modedArgs.Add(BuildModedSubterm(term.Args[i], argMode, argType, typeEnv));
        }
        return new ModedCompound(parentMode, term.Functor, term.Arity, modedArgs);
    }

    /// <summary>
    /// Build a moded subterm from an AST term.
    /// <para>
    /// FIX: When expectedType is a wildcard (_ or _?) OR unknown (null), don't use
    /// type-driven mode computation. All subterms inherit the same mode uniformly.
    /// This implements Definition 5.5 (wildcard positions).
    /// </para>
    /// CRITICAL: the wildcard guard runs BEFORE the term-subtype dispatch — orthogonal concerns.
    /// </summary>
    private static ModedTerm BuildModedSubterm(Term term, Mode mode, TypeExpr? expectedType, TypeEnvironment? typeEnv)
    {
        // Wildcard fast-path guard FIRST (orthogonal to term-subtype dispatch)
        if (expectedType is null or PrimitiveModeAlt)
            return BuildOpaqueModedTerm(term, mode);

        return term switch
        {
            VarTerm v =>
                new ModedVariable(v.Name, isReader: v.IsReader, structuralMode: mode),

            StructTerm s =>
                BuildModedStructTerm(s, mode, expectedType, typeEnv),

            ListTerm l when l.IsNil =>
                ModedConstant.Nil(mode),

            ListTerm l =>
                BuildModedListTerm(l, mode, expectedType, typeEnv),

            ConstTerm c =>
                new ModedConstant(mode, c.Value?.ToString() ?? "null"),

            UnderscoreTerm _ =>
                // Bug 4 fix: Each anonymous variable must be treated as a FRESH writer
                // Paper Remark 3.1: "Each occurrence denotes a fresh writer"
                new ModedVariable(FreshAnonVarName(), isReader: false, structuralMode: mode),

            _ => throw new InvalidHeadError($"Unknown term type: {term.GetType().Name}")
        };
    }

    /// <summary>Helper: build moded StructTerm with type-driven sub-mode computation.</summary>
    private static ModedTerm BuildModedStructTerm(StructTerm s, Mode mode, TypeExpr expectedType, TypeEnvironment? typeEnv)
    {
        var subtermModes = GetSubtermModes(s.Functor, s.Arity, mode, expectedType, typeEnv);
        var modedArgs = new List<ModedTerm>(s.Args.Count);
        for (int i = 0; i < s.Args.Count; i++)
        {
            var (subtermMode, subtermType) = subtermModes[i];
            modedArgs.Add(BuildModedSubterm(s.Args[i], subtermMode, subtermType, typeEnv));
        }
        return new ModedCompound(mode, s.Functor, s.Arity, modedArgs);
    }

    /// <summary>Helper: build moded non-nil ListTerm with type-driven sub-mode computation.</summary>
    private static ModedTerm BuildModedListTerm(ListTerm l, Mode mode, TypeExpr expectedType, TypeEnvironment? typeEnv)
    {
        var ((headMode, headType), (tailMode, tailType)) = GetListSubtermModes(mode, expectedType, typeEnv);
        var head = BuildModedSubterm(l.Head!, headMode, headType, typeEnv);
        var tail = BuildModedSubterm(l.Tail!, tailMode, tailType, typeEnv);
        return ModedCompound.ListCons(mode, head, tail);
    }

    /// <summary>
    /// Get subterm modes for a structure by looking up type definition.
    /// Uses mode involution: combinedMode = parentMode ⊕ embeddedMode.
    /// For dual types (T?), uses algorithmic complementation.
    /// </summary>
    private static List<(Mode Mode, TypeExpr? Type)> GetSubtermModes(
        string functor, int arity, Mode parentMode, TypeExpr? expectedType, TypeEnvironment? typeEnv)
    {
        // Default: propagate parent mode if no type info available
        var defaultModes = new List<(Mode, TypeExpr?)>(arity);
        for (int i = 0; i < arity; i++)
            defaultModes.Add((parentMode, null));

        if (typeEnv is null || expectedType is null)
            return defaultModes;

        // Resolve type reference to get the type definition
        string? typeName = null;
        bool isDual = false;
        if (expectedType is TypeRef tr)
        {
            typeName = tr.Name;
            isDual = tr.IsInput;
        }

        if (typeName is null)
            return defaultModes;

        var typeDef = typeEnv.LookupType(typeName);
        if (typeDef is null)
            return defaultModes;

        // Find matching structure alternative in type definition
        foreach (var alt in typeDef.Alternatives)
        {
            if (alt is StructAlt sa && sa.Functor == functor && sa.Arity == arity)
            {
                // Found matching constructor — compute combined modes for each arg
                var result = new List<(Mode, TypeExpr?)>(sa.Args.Count);
                foreach (var argType in sa.Args)
                {
                    var embeddedMode = GetEmbeddedMode(argType);
                    var combinedMode = ModeOps.CombineMode(parentMode, embeddedMode);
                    var subtermType = isDual ? DualType(argType) : argType;
                    result.Add((combinedMode, subtermType));
                }
                return result;
            }

            // Handle DiffList: \ operator
            if (alt is DiffListAlt da && functor == @"\" && arity == 2)
            {
                var contentMode = GetEmbeddedMode(da.Content);
                var holeMode = GetEmbeddedMode(da.Hole);
                var contentCombined = ModeOps.CombineMode(parentMode, contentMode);
                var holeCombined = ModeOps.CombineMode(parentMode, holeMode);
                var contentType = isDual ? DualType(da.Content) : da.Content;
                var holeType = isDual ? DualType(da.Hole) : da.Hole;
                return new List<(Mode, TypeExpr?)>
                {
                    (contentCombined, contentType),
                    (holeCombined, holeType),
                };
            }
        }

        return defaultModes;
    }

    /// <summary>
    /// Get subterm modes for list [H|T] by looking up type definition.
    /// Returns a nested tuple: ((headMode, headType), (tailMode, tailType)).
    /// For dual types (T?), uses algorithmic complementation.
    /// </summary>
    private static ((Mode Mode, TypeExpr? Type) Head, (Mode Mode, TypeExpr? Type) Tail) GetListSubtermModes(
        Mode parentMode, TypeExpr? expectedType, TypeEnvironment? typeEnv)
    {
        // Default: propagate parent mode
        var defaultResult = ((parentMode, (TypeExpr?)null), (parentMode, (TypeExpr?)null));

        if (typeEnv is null || expectedType is null)
            return defaultResult;

        string? typeName = null;
        bool isDual = false;
        if (expectedType is TypeRef tr)
        {
            typeName = tr.Name;
            isDual = tr.IsInput;
        }

        if (typeName is null)
            return defaultResult;

        var typeDef = typeEnv.LookupType(typeName);
        if (typeDef is null)
            return defaultResult;

        // Find ListConsAlt in type definition
        foreach (var alt in typeDef.Alternatives)
        {
            if (alt is ListConsAlt lca)
            {
                var headEmbeddedMode = GetEmbeddedMode(lca.Head);
                var tailEmbeddedMode = GetEmbeddedMode(lca.Tail);
                var headMode = ModeOps.CombineMode(parentMode, headEmbeddedMode);
                var tailMode = ModeOps.CombineMode(parentMode, tailEmbeddedMode);
                var headType = isDual ? DualType(lca.Head) : lca.Head;
                var tailType = isDual ? DualType(lca.Tail) : lca.Tail;
                return ((headMode, headType), (tailMode, tailType));
            }
        }

        return defaultResult;
    }

    /// <summary>
    /// Get the embedded mode from a type expression.
    /// TypeRef/PrimitiveModeAlt with IsInput=true → consume (↓); false → produce (↑).
    /// All other expressions default to produce (↑) — NOT a throwing arm (preserves Dart design intent).
    /// </summary>
    private static Mode GetEmbeddedMode(TypeExpr expr) => expr switch
    {
        TypeRef tr => tr.IsInput ? ModeAliases.Consume : ModeAliases.Produce,
        PrimitiveModeAlt pma => pma.IsInput ? ModeAliases.Consume : ModeAliases.Produce,
        _ => ModeAliases.Produce
    };

    /// <summary>
    /// Get the dual of a type expression.
    /// TypeRef → TypeRef.Dual(); PrimitiveModeAlt → flip IsInput via ctor; others → identity.
    /// </summary>
    private static TypeExpr DualType(TypeExpr expr) => expr switch
    {
        TypeRef tr => tr.Dual(),
        PrimitiveModeAlt pma => new PrimitiveModeAlt(!pma.IsInput, pma.Line, pma.Column),
        _ => expr
    };

    /// <summary>
    /// Build a moded term without type-driven mode computation.
    /// Used when the expected type is a wildcard (_ or _?).
    /// All sub-children inherit <paramref name="mode"/> unchanged — NO type-driven dispatch.
    /// </summary>
    private static ModedTerm BuildOpaqueModedTerm(Term term, Mode mode) => term switch
    {
        VarTerm v =>
            new ModedVariable(v.Name, isReader: v.IsReader, structuralMode: mode),

        ConstTerm c =>
            new ModedConstant(mode, c.Value?.ToString() ?? "null"),

        UnderscoreTerm _ =>
            // Each anonymous variable is a fresh writer (paper Remark 3.1)
            new ModedVariable(FreshAnonVarName(), isReader: false, structuralMode: mode),

        ListTerm l when l.IsNil =>
            ModedConstant.Nil(mode),

        ListTerm l =>
            // Build list structure — all children inherit the same mode
            ModedCompound.ListCons(mode,
                BuildOpaqueModedTerm(l.Head!, mode),
                BuildOpaqueModedTerm(l.Tail!, mode)),

        StructTerm s =>
            // Build structure — all children inherit the same mode (no type-driven computation)
            new ModedCompound(mode, s.Functor, s.Arity,
                s.Args.Select(arg => BuildOpaqueModedTerm(arg, mode)).ToList()),

        _ => throw new InvalidHeadError($"Unknown term type in opaque context: {term.GetType().Name}")
    };

    /// <summary>
    /// Complement all variables in the moded term.
    /// Per Definition 5.5 step 2: Replace each variable with its paired variable (unconditional).
    /// ModedConstant is returned unchanged (structural sharing — no variable to flip).
    /// </summary>
    private static ModedTerm EnsureVariablesMatchModes(ModedTerm term) => term switch
    {
        ModedCompound c =>
            new ModedCompound(c.Mode, c.Functor, c.Arity,
                c.Args.Select(EnsureVariablesMatchModes).ToList()),

        ModedConstant k =>
            k, // structural sharing — no-op

        ModedVariable v =>
            // Unconditional complementation: always flip the variable
            new ModedVariable(v.Name, isReader: !v.IsReader, structuralMode: v.Mode),

        _ => throw new InvalidHeadError($"Unknown moded term type: {term.GetType().Name}")
    };
}

// =============================================================================
// Errors
// =============================================================================

/// <summary>Error thrown when head/atom arity does not match declaration.</summary>
public sealed class ArityMismatchError : Exception
{
    public ArityMismatchError(string message) : base(message) { }

    public override string ToString() => $"{nameof(ArityMismatchError)}: {Message}";
}

/// <summary>Error thrown when head is not a valid compound term.</summary>
public sealed class InvalidHeadError : Exception
{
    public InvalidHeadError(string message) : base(message) { }

    public override string ToString() => $"{nameof(InvalidHeadError)}: {Message}";
}
