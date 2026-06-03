// lib/analysis/type_checker/type_environment_builder.cs
//
// Builds TypeEnvironment from a parsed Module.
// Loads prelude, merges with user definitions, validates.
// Resolves type aliases at preprocessing time.
//
// Converted from Dart source: lib/analysis/type_checker/type_environment_builder.dart
// source_sha256: dfd2a18574bdee84c8b2875529f6401ebd0a5cb60c16c619db3a842b519793fa

using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using GlpRuntime.Compiler;

namespace GlpRuntime.Analysis.TypeChecker;

// ---------------------------------------------------------------------------
// Domain exception types (four sealed classes, identical shape)
// ---------------------------------------------------------------------------

/// <summary>Error for illegal type redefinition.</summary>
/// <remarks>Specification: docs/modules/type-environment.md v0.8</remarks>
public sealed class RedefinitionError : Exception
{
    /// <summary>Line number in source.</summary>
    public long Line { get; }
    /// <summary>Column number in source.</summary>
    public long Column { get; }

    public RedefinitionError(string message, long line, long column) : base(message)
    {
        Line   = line;
        Column = column;
    }

    public override string ToString() => $"{Message} at line {Line}, column {Column}";
}

/// <summary>Error for circular alias chain.</summary>
public sealed class CircularAliasError : Exception
{
    public long Line { get; }
    public long Column { get; }

    public CircularAliasError(string message, long line, long column) : base(message)
    {
        Line   = line;
        Column = column;
    }

    public override string ToString() => $"{Message} at line {Line}, column {Column}";
}

/// <summary>Error for non-deterministic type (overlapping alternatives).</summary>
public sealed class NonDeterministicTypeError : Exception
{
    public long Line { get; }
    public long Column { get; }

    public NonDeterministicTypeError(string message, long line, long column) : base(message)
    {
        Line   = line;
        Column = column;
    }

    public override string ToString() => $"{Message} at line {Line}, column {Column}";
}

/// <summary>Error for union alias expansion failure.</summary>
public sealed class AliasExpansionError : Exception
{
    public long Line { get; }
    public long Column { get; }

    public AliasExpansionError(string message, long line, long column) : base(message)
    {
        Line   = line;
        Column = column;
    }

    public override string ToString() => $"{Message} at line {Line}, column {Column}";
}

// ---------------------------------------------------------------------------
// TypeEnvironmentBuilder — static host for all builder methods
// ---------------------------------------------------------------------------

/// <summary>
/// Builds TypeEnvironment from a parsed Module.
/// All methods are pure static; the only mutable state is the single-writer
/// engine-init hook <see cref="_preludeEnvironmentSource"/>.
/// </summary>
/// <remarks>Specification: docs/modules/type-environment.md v0.8</remarks>
public static class TypeEnvironmentBuilder
{
    // -----------------------------------------------------------------------
    // Engine-init hook (single-writer-many-readers; ECMA-335 atomic reference)
    // -----------------------------------------------------------------------

    /// <summary>Source for the prelude environment (set by engine from programs/self.glp).</summary>
    private static string? _preludeEnvironmentSource;

    /// <summary>
    /// Set the source from which the prelude environment is built.
    /// Call this once during engine initialization with the content of programs/self.glp.
    /// </summary>
    public static void SetPreludeEnvironmentSource(string source) =>
        _preludeEnvironmentSource = source;

    // -----------------------------------------------------------------------
    // Public entry points
    // -----------------------------------------------------------------------

    /// <summary>Build TypeEnvironment from prelude.</summary>
    public static TypeEnvironment BuildPreludeEnvironment()
    {
        var source = _preludeEnvironmentSource ?? Prelude.TypePrelude;
        if (source.Length == 0)
            return new TypeEnvironment(
                new Dictionary<string, TypeDef>(StringComparer.Ordinal),
                new Dictionary<string, ProcDecl>(StringComparer.Ordinal));

        var lexer  = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var module = parser.ParseModule();

        // Extract templates before expansion removes them.
        // These are passed to downstream modules so they can expand references
        // to prelude-defined parameterized types (e.g., Stream(X), Channel(X,Y)).
        var preludeTemplates = new Dictionary<string, TypeDef>(StringComparer.Ordinal);
        foreach (var td in module.TypeDefs)
            if (td.IsParameterized) preludeTemplates[td.Name] = td;

        // Expand parameterized types before building the environment.
        // Templates (e.g., Stream(X)) are removed; only concrete expansions remain.
        var expandedModule = ParamExpansion.ExpandParameterizedTypes(module);

        var env = BuildEnvironmentFromModule(
            expandedModule,
            checkRedefinitions: false,
            resolveAliasesNow: true);
        return new TypeEnvironment(
            env.Types,
            env.Procedures,
            paramProcDecls:  env.ParamProcDecls,
            typeTemplates:   preludeTemplates);
    }

    /// <summary>
    /// Build TypeEnvironment from a parsed Module.
    /// Loads prelude first, then merges user definitions.
    /// Throws <see cref="RedefinitionError"/> if user redefines predefined types/procedures.
    ///
    /// If <paramref name="ancestorScope"/> is provided, it is used as the base environment
    /// instead of just the prelude. The ancestor scope should already include the prelude
    /// and all ancestor self.glp definitions (built by assembleTypeScope in module_hierarchy).
    /// The module's own definitions are then merged on top (shadowing ancestors).
    /// </summary>
    public static TypeEnvironment BuildTypeEnvironment(
        Module module, TypeEnvironment? ancestorScope = null)
    {
        // Base environment: ancestor scope if provided, otherwise just prelude.
        var baseEnv = ancestorScope ?? BuildPreludeEnvironment();

        // Build user environment WITHOUT resolving aliases yet.
        var userEnv = BuildEnvironmentFromModule(
            module,
            checkRedefinitions: ancestorScope == null,
            resolveAliasesNow: false);

        // Merge: base first, then user (user can shadow non-predefined).
        var merged = baseEnv.Merge(userEnv);

        // Defensive shallow copies — _resolveAliases mutates types/procedures in place;
        // without copies those mutations would leak into merged (silent footgun).
        var types      = new Dictionary<string, TypeDef>(merged.Types,      StringComparer.Ordinal);
        var procedures = new Dictionary<string, ProcDecl>(merged.Procedures, StringComparer.Ordinal);

        // Now resolve aliases on the merged environment
        // (so user aliases can reference prelude types).
        ResolveAliases(types, procedures);

        // paramProcDecls deliberately NOT defensively copied:
        // ResolveAliases does not touch it; if alias resolution is ever extended
        // to paramProcDecls, add a defensive copy at that point.
        return new TypeEnvironment(types, procedures, paramProcDecls: merged.ParamProcDecls);
    }

    /// <summary>Extract all clauses from a Module's procedures.</summary>
    public static List<Clause> ExtractClauses(Module module)
    {
        var clauses = new List<Clause>();
        foreach (var proc in module.Procedures) clauses.AddRange(proc.Clauses);
        return clauses;
    }

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

    /// <summary>Build TypeEnvironment from Module's type definitions and procedure declarations.</summary>
    private static TypeEnvironment BuildEnvironmentFromModule(
        Module module, bool checkRedefinitions, bool resolveAliasesNow)
    {
        var types         = new Dictionary<string, TypeDef>(StringComparer.Ordinal);
        var procedures    = new Dictionary<string, ProcDecl>(StringComparer.Ordinal);
        var paramProcDecls = new Dictionary<string, ProcDecl>(StringComparer.Ordinal);

        // Add type definitions (including aliases — will be resolved later).
        foreach (var typeDef in module.TypeDefs)
        {
            if (checkRedefinitions && Prelude.IsPredefinedType(typeDef.Name))
                throw new RedefinitionError(
                    $"Cannot redefine predefined type: {typeDef.Name}",
                    typeDef.Line, typeDef.Column);
            // Aliases are allowed (v0.7) — determinism check skipped for them.
            if (!IsTypeAlias(typeDef)) CheckDeterminism(typeDef);
            types[typeDef.Name] = typeDef;
        }

        // Add procedure declarations.
        foreach (var procDecl in module.ProcDeclarations)
        {
            if (checkRedefinitions && Prelude.IsPredefinedProcedure(procDecl.Name))
                throw new RedefinitionError(
                    $"Cannot redefine predefined procedure: {procDecl.Name}/{procDecl.Arity}",
                    procDecl.Line, procDecl.Column);
            // Mark procedure as builtin if it's a true builtin (implemented in the runtime).
            var isBuiltin = Prelude.IsBuiltinProcedure(procDecl.Key);
            procedures[procDecl.QualifiedKey] = (isBuiltin && !procDecl.IsBuiltin)
                ? new ProcDecl(
                    procDecl.Name, procDecl.ArgTypes, procDecl.Line, procDecl.Column,
                    isBuiltin:  true,
                    exported:   procDecl.Exported,
                    imported:   procDecl.Imported,
                    modulePath: procDecl.ModulePath)
                : procDecl;
        }

        // Add parameterized proc decl templates (for call-site inference).
        foreach (var paramDecl in module.ParamProcDecls)
            paramProcDecls[paramDecl.QualifiedKey] = paramDecl;

        // Resolve aliases (preprocessing step per v0.8 spec) — only if requested.
        if (resolveAliasesNow) ResolveAliases(types, procedures);

        return new TypeEnvironment(types, procedures, paramProcDecls: paramProcDecls);
    }

    /// <summary>
    /// Check if a type definition is a simple alias (single type reference).
    ///
    /// Per spec (type-environment.md v0.8):
    /// Simple aliases have a single alternative that is a TypeRef or PrimitiveModeAlt:
    ///   Output ::= _.         (alias for primitive wildcard)
    ///   Input  ::= _?.        (alias for primitive wildcard complement)
    ///   MyList ::= List.      (alias for defined type)
    ///   MyStream ::= Stream?. (alias for complement of defined type)
    /// </summary>
    private static bool IsSimpleAlias(TypeDef def)
    {
        if (def.Alternatives.Count != 1) return false;
        var alt = def.Alternatives[0];
        return alt is PrimitiveModeAlt || alt is TypeRef;
    }

    /// <summary>
    /// Check if a type definition is a union alias (multiple type references to defined types).
    ///
    /// Per spec (type-environment.md v0.8):
    /// Union aliases have multiple alternatives, all of which are TypeRefs to
    /// user-defined types (not predefined primitives):
    ///   Msg ::= NetMsg ; UserMsg.  (union of two user-defined message types)
    ///
    /// NOT a union alias:
    ///   Constant ::= Number ; String.  (references predefined primitives)
    /// </summary>
    private static bool IsUnionAlias(TypeDef def)
    {
        if (def.Alternatives.Count < 2) return false;
        foreach (var alt in def.Alternatives)
        {
            if (alt is not TypeRef r) return false;
            // If it references a predefined type, it's not a union alias.
            if (Prelude.IsPredefinedType(r.Name)) return false;
        }
        return true;
    }

    /// <summary>Check if a type definition is any kind of alias.</summary>
    private static bool IsTypeAlias(TypeDef def) => IsSimpleAlias(def) || IsUnionAlias(def);

    /// <summary>
    /// Resolve type aliases at preprocessing time.
    ///
    /// Per spec (type-environment.md v0.8):
    /// — Simple aliases are resolved transitively and replaced everywhere.
    /// — Union aliases are expanded by collecting alternatives from referenced types.
    /// — Circular alias chains are detected and rejected.
    ///
    /// Mutates <paramref name="types"/> and <paramref name="procedures"/> in place.
    /// </summary>
    private static void ResolveAliases(
        IDictionary<string, TypeDef>  types,
        IDictionary<string, ProcDecl> procedures)
    {
        // Step 1: Identify simple and union aliases.
        var simpleAliases = new Dictionary<string, TypeDef>(StringComparer.Ordinal);
        var unionAliases  = new Dictionary<string, TypeDef>(StringComparer.Ordinal);

        foreach (var entry in types)
        {
            if      (IsSimpleAlias(entry.Value)) simpleAliases[entry.Key] = entry.Value;
            else if (IsUnionAlias(entry.Value))  unionAliases[entry.Key]  = entry.Value;
        }

        if (simpleAliases.Count == 0 && unionAliases.Count == 0) return;

        // Step 2: Resolve simple aliases transitively, detecting cycles.
        // Non-static local function captures resolved/visiting/simpleAliases
        // from the enclosing scope — mirrors the Dart nested-recursive-closure shape.
        var resolved = new Dictionary<string, TypeExpr>(StringComparer.Ordinal);
        var visiting = new HashSet<string>(StringComparer.Ordinal);

        TypeExpr ResolveSimpleAliasLocal(string name)
        {
            if (resolved.TryGetValue(name, out var prior)) return prior;

            if (!simpleAliases.TryGetValue(name, out var aliasDef))
                // Not a simple alias — return a TypeRef to it.
                return new TypeRef(name, 0, 0);

            if (visiting.Contains(name))
                throw new CircularAliasError(
                    $"Circular alias chain detected: {name}",
                    aliasDef.Line, aliasDef.Column);

            visiting.Add(name);

            var target = aliasDef.Alternatives[0];
            TypeExpr result;

            if (target is TypeRef tr)
            {
                // Target is another type reference — check if it's also an alias.
                if (simpleAliases.ContainsKey(tr.Name))
                {
                    // Resolve transitively.
                    var resolvedTarget = ResolveSimpleAliasLocal(tr.Name);
                    // Apply complement if needed: (T?)? = T.
                    result = ApplyComplement(resolvedTarget, tr.IsInput, tr.Line, tr.Column);
                }
                else
                {
                    // Target is a real type, keep as TypeRef.
                    result = tr;
                }
            }
            else if (target is PrimitiveModeAlt)
            {
                // Target is _ or _?.
                result = target;
            }
            else
            {
                // Shouldn't happen if IsSimpleAlias is correct.
                result = target;
            }

            visiting.Remove(name);
            resolved[name] = result;
            return result;
        }

        // Resolve all simple aliases.
        foreach (var name in simpleAliases.Keys) ResolveSimpleAliasLocal(name);

        // Step 3: Expand union aliases (per-alt foreach + complement + determinism check).
        foreach (var entry in unionAliases)
        {
            var name        = entry.Key;
            var def         = entry.Value;
            var expandedAlts = new List<TypeExpr>();

            foreach (var alt in def.Alternatives)
            {
                var r = (TypeRef)alt;  // verified by IsUnionAlias

                // Check: union alias cannot reference another alias.
                if (simpleAliases.ContainsKey(r.Name) || unionAliases.ContainsKey(r.Name))
                    throw new AliasExpansionError(
                        $"Union alias cannot reference another alias: {r.Name}",
                        def.Line, def.Column);

                // If this is a predefined type, keep the TypeRef as an alternative.
                if (Prelude.IsPredefinedType(r.Name)) { expandedAlts.Add(r); continue; }

                // Get the target type definition.
                if (!types.TryGetValue(r.Name, out var targetDef))
                    throw new AliasExpansionError(
                        $"Union alias references undefined type: {r.Name}",
                        def.Line, def.Column);

                // Collect alternatives from the target type, applying complement if needed.
                foreach (var targetAlt in targetDef.Alternatives)
                    expandedAlts.Add(ApplyComplementToAlt(targetAlt, r.IsInput, def.Line, def.Column));
            }

            // Replace union alias definition with expanded definition.
            var expandedDef = new TypeDef(name, expandedAlts, def.Line, def.Column);
            CheckDeterminism(expandedDef);
            types[name] = expandedDef;
        }

        // Step 4: Replace simple alias references in all non-simple-alias type defs.
        // .Where(...).ToList() snapshot is CORRECTNESS-CRITICAL: the loop body mutates
        // types (types[k] = new TypeDef(...)), and iterating a live Dictionary while
        // mutating throws InvalidOperationException.
        var nonSimpleAliasTypes = types
            .Where(e => !simpleAliases.ContainsKey(e.Key))
            .ToList();

        foreach (var entry in nonSimpleAliasTypes)
        {
            var newAlternatives = new List<TypeExpr>();
            foreach (var alt in entry.Value.Alternatives)
                newAlternatives.Add(ReplaceAliasReferences(alt, resolved));
            types[entry.Key] = new TypeDef(
                entry.Value.Name, newAlternatives,
                entry.Value.Line, entry.Value.Column);
        }

        // Step 5: Replace alias references in procedure declarations.
        // .ToList() snapshot for the same mutation-safety reason.
        foreach (var entry in procedures.ToList())
        {
            var newArgTypes = new List<TypeExpr>();
            foreach (var argType in entry.Value.ArgTypes)
                newArgTypes.Add(ReplaceAliasReferences(argType, resolved));
            procedures[entry.Key] = new ProcDecl(
                entry.Value.Name, newArgTypes,
                entry.Value.Line, entry.Value.Column,
                isBuiltin:  entry.Value.IsBuiltin,
                exported:   entry.Value.Exported,
                imported:   entry.Value.Imported,
                modulePath: entry.Value.ModulePath);
        }

        // Step 6: Remove simple alias definitions from types map.
        // (Union aliases are already expanded in place, so keep them.)
        // .Keys.ToList() snapshot: conservative iteration discipline.
        foreach (var name in simpleAliases.Keys.ToList()) types.Remove(name);
    }

    /// <summary>
    /// Apply complement to a TypeExpr if needed.
    /// Implements the involution (T?)? = T.
    /// </summary>
    private static TypeExpr ApplyComplement(
        TypeExpr expr, bool applyComplement, long line, long column)
    {
        if (!applyComplement) return expr;
        return expr switch
        {
            TypeRef r          => new TypeRef(r.Name, (int)line, (int)column, isInput: !r.IsInput),
            PrimitiveModeAlt p => new PrimitiveModeAlt(!p.IsInput, (int)line, (int)column),
            _                  => expr,
        };
    }

    /// <summary>
    /// Apply complement to all type references within a type alternative.
    /// Used for union alias expansion when the reference is complemented (e.g., Msg ::= NetMsg?).
    /// </summary>
    private static TypeExpr ApplyComplementToAlt(
        TypeExpr alt, bool applyComplement, long line, long column)
    {
        if (!applyComplement) return alt;
        switch (alt)
        {
            case TypeRef r:
                return new TypeRef(r.Name, (int)line, (int)column, isInput: !r.IsInput);
            case PrimitiveModeAlt p:
                return new PrimitiveModeAlt(!p.IsInput, (int)line, (int)column);
            case ConstantAlt:
                return alt;  // Constants don't have modes.
            case ListNilAlt:
                return alt;  // [] doesn't have modes.
            case ListConsAlt lc:
                return new ListConsAlt(
                    ApplyComplementToAlt(lc.Head, true, line, column),
                    ApplyComplementToAlt(lc.Tail, true, line, column),
                    (int)line, (int)column);
            case StructAlt s:
                return new StructAlt(
                    s.Functor,
                    s.Args.Select(a => ApplyComplementToAlt(a, true, line, column)).ToList(),
                    (int)line, (int)column);
            case DiffListAlt d:
                return new DiffListAlt(
                    ApplyComplementToAlt(d.Content, true, line, column),
                    ApplyComplementToAlt(d.Hole,    true, line, column),
                    (int)line, (int)column);
            default:
                return alt;
        }
    }

    /// <summary>Replace alias references in a TypeExpr recursively.</summary>
    private static TypeExpr ReplaceAliasReferences(
        TypeExpr expr, IDictionary<string, TypeExpr> resolved)
    {
        switch (expr)
        {
            case TypeRef r:
                // Dart Map[k] returns null on miss; C# indexer throws KeyNotFoundException —
                // must use TryGetValue (rf-csharp-dictionary-trygetvalue-vs-indexer-null-vs-keynotfound).
                if (resolved.TryGetValue(r.Name, out var resolvedTarget))
                    return ApplyComplement(resolvedTarget, r.IsInput, r.Line, r.Column);
                return r;  // Not an alias, keep as-is.
            case PrimitiveModeAlt:
                return expr;  // Primitives don't reference other types.
            case ConstantAlt:
                return expr;  // Constants don't reference other types.
            case ListNilAlt:
                return expr;  // Empty list doesn't reference other types.
            case ListConsAlt lc:
                return new ListConsAlt(
                    ReplaceAliasReferences(lc.Head, resolved),
                    ReplaceAliasReferences(lc.Tail, resolved),
                    lc.Line, lc.Column);
            case StructAlt s:
                return new StructAlt(
                    s.Functor,
                    s.Args.Select(a => ReplaceAliasReferences(a, resolved)).ToList(),
                    s.Line, s.Column);
            case DiffListAlt d:
                return new DiffListAlt(
                    ReplaceAliasReferences(d.Content, resolved),
                    ReplaceAliasReferences(d.Hole,    resolved),
                    d.Line, d.Column);
            default:
                return expr;  // Unknown type, return as-is.
        }
    }

    // -----------------------------------------------------------------------
    // Determinism validation helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Primitive type names that trigger overlap checking.
    /// Static readonly FrozenSet — hoisted from the per-call inline array
    /// (cached idiom from prelude.dart for set-membership predicates).
    /// </summary>
    private static readonly FrozenSet<string> PrimitiveTypeNames =
        new[] { "Integer", "Real", "Number", "String" }
            .ToFrozenSet(StringComparer.Ordinal);

    /// <summary>
    /// Check if type alternatives are deterministic (distinguishable).
    ///
    /// Per spec (type-environment.md v0.5):
    /// Type definitions must be deterministic: alternatives must be distinguishable
    /// by their top-level functor or, for primitive types, by disjoint type membership.
    ///
    /// Throws <see cref="NonDeterministicTypeError"/> for:
    /// — Two alternatives with same functor/arity.
    /// — Two constant alternatives with same value.
    /// — Primitive type alternatives that overlap (e.g., _ with anything, Number with Integer/Real).
    /// </summary>
    private static void CheckDeterminism(TypeDef def)
    {
        var functors    = new HashSet<string>(StringComparer.Ordinal);  // "functor/arity" keys
        var constants   = new HashSet<string>(StringComparer.Ordinal);  // constant values
        var primitives  = new HashSet<string>(StringComparer.Ordinal);  // primitive type names
        var hasWildcard = false;

        foreach (var alt in def.Alternatives)
        {
            switch (alt)
            {
                case ConstantAlt c:
                {
                    var key = c.Value.ToString() ?? "null";
                    if (!constants.Add(key))
                        throw new NonDeterministicTypeError(
                            $"Duplicate constant alternative: {key} in {def.Name}",
                            def.Line, def.Column);
                    break;
                }
                case ListNilAlt:
                    if (!functors.Add("[]/0"))
                        throw new NonDeterministicTypeError(
                            $"Duplicate [] alternative in {def.Name}",
                            def.Line, def.Column);
                    break;
                case ListConsAlt:
                    if (!functors.Add("[|]/2"))
                        throw new NonDeterministicTypeError(
                            $"Duplicate [|] alternative in {def.Name}",
                            def.Line, def.Column);
                    break;
                case StructAlt s:
                {
                    var key = $"{s.Functor}/{s.Args.Count}";
                    if (!functors.Add(key))
                        throw new NonDeterministicTypeError(
                            $"Duplicate functor alternative: {key} in {def.Name}",
                            def.Line, def.Column);
                    break;
                }
                case DiffListAlt:
                    // "\\/2" in C# is the two-char string \/2 — byte-exact match to Dart '\\/2'.
                    if (!functors.Add("\\/2"))
                        throw new NonDeterministicTypeError(
                            $"Duplicate \\ alternative in {def.Name}",
                            def.Line, def.Column);
                    break;
                case PrimitiveModeAlt:
                    // _ or _? — wildcards overlap with everything.
                    if (hasWildcard || primitives.Count > 0)
                        throw new NonDeterministicTypeError(
                            $"Wildcard _ overlaps with other alternatives in {def.Name}",
                            def.Line, def.Column);
                    hasWildcard = true;
                    break;
                case TypeRef r:
                    // TypeRef in alternative position = primitive type reference.
                    if (PrimitiveTypeNames.Contains(r.Name))
                    {
                        CheckPrimitiveOverlap(r.Name, primitives, hasWildcard, def);
                        primitives.Add(r.Name);
                    }
                    break;
            }
        }
    }

    /// <summary>
    /// Four-way primitive overlap check.
    /// — Wildcard overlaps with everything.
    /// — Number overlaps with Integer and Real (both directions).
    /// — Direct duplicate.
    /// Domain invariant from type-environment.md v0.5; preserved verbatim.
    /// </summary>
    private static void CheckPrimitiveOverlap(
        string newPrimitive, ISet<string> existing, bool hasWildcard, TypeDef def)
    {
        if (hasWildcard)
            throw new NonDeterministicTypeError(
                $"Wildcard _ overlaps with {newPrimitive} in {def.Name}",
                def.Line, def.Column);

        if (newPrimitive == "Number"
            && (existing.Contains("Integer") || existing.Contains("Real")))
            throw new NonDeterministicTypeError(
                $"Number overlaps with Integer/Real in {def.Name}",
                def.Line, def.Column);

        if ((newPrimitive == "Integer" || newPrimitive == "Real")
            && existing.Contains("Number"))
            throw new NonDeterministicTypeError(
                $"{newPrimitive} overlaps with Number in {def.Name}",
                def.Line, def.Column);

        if (existing.Contains(newPrimitive))
            throw new NonDeterministicTypeError(
                $"Duplicate primitive type {newPrimitive} in {def.Name}",
                def.Line, def.Column);
    }
}
