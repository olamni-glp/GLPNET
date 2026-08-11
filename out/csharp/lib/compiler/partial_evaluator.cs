// lib/compiler/partial_evaluator.cs
//
// GLP Partial Evaluator — source-to-source AST transformer.
// Converted from Dart source: lib/compiler/partial_evaluator.dart
// source_sha256: 8ac90433fa30c517b59e6f21f1860214b4493128880376e072467c37f92385ab
//
// Stage 1: Unfold defined guards (unit clauses in guard position).
// Stage 2: Unfold reduce/2 calls in clause bodies against reduce/2 facts.

using static GlpRuntime.Analysis.TypeChecker.Prelude;

namespace GlpRuntime.Compiler;

// ============================================================================
// PRELUDE UNIT CLAUSES
// ============================================================================

/// <summary>
/// Hosts the mutable prelude-unit-clause source and its memoised parsed form.
/// Wraps two Dart top-level globals plus a setter and a lazy getter
/// (C# forbids free top-level mutable fields and free functions).
/// </summary>
internal static class PreludeUnitClauses
{
    private static string? _preludeUnitClauseSource = null;
    private static Dictionary<string, IReadOnlyList<Term>>? _cachedPreludeUnitClauses = null;

    /// <summary>
    /// Set the source from which prelude unit clauses are extracted.
    /// Invalidates the cache so the next call to GetPreludeUnitClauses re-parses.
    /// </summary>
    public static void SetPreludeUnitClauseSource(string source)
    {
        _preludeUnitClauseSource = source;
        _cachedPreludeUnitClauses = null;
    }

    /// <summary>
    /// Parse the prelude source and return unit clauses, memoised.
    /// Returns a map from "name/arity" to the head arguments of each unit clause.
    /// NOT a Lazy&lt;T&gt; because the cache must be invalidatable by the setter.
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<Term>> GetPreludeUnitClauses()
    {
        if (_cachedPreludeUnitClauses is not null) return _cachedPreludeUnitClauses;

        var source = _preludeUnitClauseSource ?? string.Empty;
        if (source.Length == 0)
        {
            _cachedPreludeUnitClauses = new Dictionary<string, IReadOnlyList<Term>>(StringComparer.Ordinal);
            return _cachedPreludeUnitClauses;
        }

        var lexer  = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var module = parser.ParseModule();

        var unitClauses = new Dictionary<string, IReadOnlyList<Term>>(StringComparer.Ordinal);
        foreach (var proc in module.Procedures)
        {
            if (proc.Clauses.Count != 1) continue;
            var clause = proc.Clauses[0];
            if (!PartialEvaluator.IsUnitClauseShape(clause)) continue;
            unitClauses[$"{proc.Name}/{proc.Arity}"] = clause.Head.Args;
        }

        _cachedPreludeUnitClauses = unitClauses;
        return _cachedPreludeUnitClauses;
    }
}

// ============================================================================
// PARTIAL EVALUATOR
// ============================================================================

/// <summary>Partial evaluator for GLP programs.</summary>
public class PartialEvaluator
{
    private long _varCounter = 0;

    // ========================================================================
    // Public entry points
    // ========================================================================

    /// <summary>
    /// Stage 1: Transform all defined guards in a program.
    /// Call this before SRSW analysis.
    /// </summary>
    public Program TransformDefinedGuards(Program program)
    {
        // Merge prelude unit clauses with user unit clauses.
        // Second foreach overrides on key collision (user defs override prelude).
        var unitClauses = new Dictionary<string, IReadOnlyList<Term>>(StringComparer.Ordinal);
        foreach (var kv in PreludeUnitClauses.GetPreludeUnitClauses()) unitClauses[kv.Key] = kv.Value;
        foreach (var kv in CollectUnitClauses(program))                 unitClauses[kv.Key] = kv.Value;

        var allProcedures        = CollectAllProcedures(program);
        var transformedProcedures = new List<Procedure>(program.Procedures.Count);
        foreach (var procedure in program.Procedures)
        {
            var transformedClauses = new List<Clause>(procedure.Clauses.Count);
            foreach (var clause in procedure.Clauses)
                transformedClauses.Add(TransformClause(clause, unitClauses, allProcedures));
            transformedProcedures.Add(new Procedure(procedure.Name, procedure.Arity, transformedClauses, procedure.Line, procedure.Column));
        }
        return new Program(transformedProcedures, program.Line, program.Column);
    }

    /// <summary>
    /// Stage 2: Unfold reduce/2 calls in clause bodies.
    /// Short-circuits (identity-return) when there are no reduce facts.
    /// </summary>
    public Program UnfoldReduceCalls(Program program)
    {
        var reduceFacts = CollectReduceFacts(program);
        if (reduceFacts.Count == 0) return program;   // identity-return — preserve reference identity

        var transformedProcedures = new List<Procedure>(program.Procedures.Count);
        foreach (var procedure in program.Procedures)
        {
            var transformedClauses = new List<Clause>();
            foreach (var clause in procedure.Clauses)
                transformedClauses.AddRange(UnfoldReduceInClause(clause, reduceFacts));
            transformedProcedures.Add(new Procedure(procedure.Name, procedure.Arity, transformedClauses, procedure.Line, procedure.Column));
        }
        return new Program(transformedProcedures, program.Line, program.Column);
    }

    // ========================================================================
    // Private helpers — collection
    // ========================================================================

    /// <summary>
    /// Lifted helper: true iff the clause is a unit-clause shape
    /// (no guards AND body is null, empty, or the singleton "true/0").
    /// The "exactly one clause" predicate stays at each call site.
    /// </summary>
    internal static bool IsUnitClauseShape(Clause clause) =>
        (clause.Guards is null || clause.Guards.Count == 0) &&
        (clause.Body is null
         || clause.Body.Count == 0
         || (clause.Body.Count == 1 && clause.Body[0].Functor == "true" && clause.Body[0].Args.Count == 0));

    private static Dictionary<string, IReadOnlyList<Term>> CollectUnitClauses(Program program)
    {
        var result = new Dictionary<string, IReadOnlyList<Term>>(StringComparer.Ordinal);
        foreach (var proc in program.Procedures)
        {
            if (proc.Clauses.Count != 1) continue;
            var clause = proc.Clauses[0];
            if (!IsUnitClauseShape(clause)) continue;
            result[$"{proc.Name}/{proc.Arity}"] = clause.Head.Args;
        }
        return result;
    }

    private static HashSet<string> CollectAllProcedures(Program program)
    {
        var procedures = new HashSet<string>(StringComparer.Ordinal);
        foreach (var proc in program.Procedures) procedures.Add($"{proc.Name}/{proc.Arity}");
        return procedures;
    }

    private static List<Clause> CollectReduceFacts(Program program)
    {
        var facts = new List<Clause>();
        foreach (var proc in program.Procedures)
        {
            if (proc.Name != "reduce" || proc.Arity != 2) continue;
            foreach (var clause in proc.Clauses)
            {
                if (!IsUnitClauseShape(clause)) continue;
                facts.Add(clause);
            }
        }
        return facts;
    }

    // ========================================================================
    // Stage-1 fixpoint transformer
    // ========================================================================

    private Clause TransformClause(
        Clause clause,
        IReadOnlyDictionary<string, IReadOnlyList<Term>> unitClauses,
        IReadOnlySet<string> allProcedures)
    {
        if (clause.Guards is null || clause.Guards.Count == 0) return clause;

        var currentHead   = clause.Head;
        var currentGuards = new List<Guard>(clause.Guards);
        var currentBody   = clause.Body is not null ? new List<Goal>(clause.Body) : null;
        bool changed      = true;

        while (changed)
        {
            changed = false;
            var remainingGuards = new List<Guard>();

            for (int i = 0; i < currentGuards.Count; i++)
            {
                var guard = currentGuards[i];
                var key   = $"{guard.Predicate}/{guard.Args.Count}";

                if (unitClauses.TryGetValue(key, out var unitArgs))
                {
                    if (guard.Negated)
                        throw new CompileError(
                            $"Defined guard \"{guard.Predicate}\" cannot be negated",
                            guard.Line, guard.Column, phase: "analyzer");

                    var renamedArgs = TermTraversal.RenameUnitClauseVars(
                        unitArgs, "PE", ref _varCounter, TermTraversal.PeIsUnderscore);
                    var result      = TermTraversal.GlpUnifyForPE(
                        guard.Args, renamedArgs, TermTraversal.PeIsUnderscore);

                    switch (result)
                    {
                        case UnifyFail fail:
                            throw new CompileError(
                                $"Defined guard \"{guard.Predicate}({string.Join(", ", guard.Args)})\" can never succeed.\n" +
                                $"  Unit clause: {guard.Predicate}({string.Join(", ", unitArgs)})\n" +
                                $"  Reason: {fail.Reason}\n" +
                                "  This clause is unreachable.",
                                guard.Line, guard.Column, phase: "analyzer");

                        case UnifySuspend suspend:
                            throw new CompileError(
                                $"Cannot reduce defined guard \"{guard.Predicate}({string.Join(", ", guard.Args)})\" at compile time.\n" +
                                $"  Unit clause: {guard.Predicate}({string.Join(", ", unitArgs)})\n" +
                                $"  Unbound readers: {string.Join(", ", suspend.UnboundReaders.Select(r => r + "?"))}\n" +
                                "  Defined guards must be fully reducible at compile time.",
                                guard.Line, guard.Column, phase: "analyzer");

                        case UnifySuccess success:
                            currentHead = TermTraversal.ApplySubstitutionToAtom(currentHead, success.Substitution);
                            var restGuards = currentGuards.GetRange(i + 1, currentGuards.Count - i - 1)
                                .Select(g => TermTraversal.ApplySubstitutionToGuard(g, success.Substitution)).ToList();
                            remainingGuards = remainingGuards
                                .Select(g => TermTraversal.ApplySubstitutionToGuard(g, success.Substitution)).ToList();
                            if (currentBody is not null)
                                currentBody = currentBody
                                    .Select(g => TermTraversal.ApplySubstitutionToGoal(g, success.Substitution)).ToList();
                            currentGuards = new List<Guard>(remainingGuards.Count + restGuards.Count);
                            currentGuards.AddRange(remainingGuards);
                            currentGuards.AddRange(restGuards);
                            changed = true;
                            break;

                        default:
                            throw new InvalidOperationException("UnifyResult: unreachable subtype.");
                    }
                    if (changed) break;
                }
                else if (BuiltinProcedures.Contains(key))
                {
                    remainingGuards.Add(guard);
                }
                else if (allProcedures.Contains(key))
                {
                    throw new CompileError(
                        $"Cannot call \"{guard.Predicate}/{guard.Args.Count}\" in guard position.\n" +
                        "  Only builtin guards and single-unit-clause procedures can appear in guards.\n" +
                        $"  The procedure \"{guard.Predicate}\" has multiple clauses or non-unit clauses.",
                        guard.Line, guard.Column, phase: "partial_evaluator");
                }
                else
                {
                    remainingGuards.Add(guard);
                }
            }
            if (!changed) currentGuards = remainingGuards;
        }

        return new Clause(
            currentHead,
            guards: currentGuards.Count == 0 ? null : currentGuards,
            body:   currentBody,
            line:   clause.Line,
            column: clause.Column);
    }

    // ========================================================================
    // Stage-2 single-clause unfolder
    // ========================================================================

    private List<Clause> UnfoldReduceInClause(Clause clause, IReadOnlyList<Clause> reduceFacts)
    {
        if (clause.Body is null || clause.Body.Count == 0) return new List<Clause> { clause };

        int    reduceIndex = -1;
        Goal?  reduceCall  = null;
        for (int i = 0; i < clause.Body.Count; i++)
        {
            var goal = clause.Body[i];
            if (goal.Functor == "reduce" && goal.Args.Count == 2)
            {
                reduceIndex = i;
                reduceCall  = goal;
                break;
            }
        }
        if (reduceCall is null) return new List<Clause> { clause };

        var expanded = new List<Clause>();
        foreach (var fact in reduceFacts)
        {
            var renamedFact      = RenameClauseVars(fact);
            var factPattern      = renamedFact.Head.Args[0];
            var factReplacement  = renamedFact.Head.Args[1];
            var callPattern      = reduceCall.Args[0];
            var callResult       = reduceCall.Args[1];

            var result = TermTraversal.GlpUnifyForPE(new[] { callPattern }, new[] { factPattern }, TermTraversal.PeIsUnderscore);
            switch (result)
            {
                case UnifyFail:
                case UnifySuspend:
                    continue;

                case UnifySuccess success:
                {
                    var resultUnify = TermTraversal.GlpUnifyForPE(new[] { callResult }, new[] { factReplacement }, TermTraversal.PeIsUnderscore);
                    var fullSubst   = new Dictionary<string, Term>(success.Substitution, StringComparer.Ordinal);
                    if (resultUnify is UnifySuccess rs)
                        foreach (var kv in rs.Substitution) fullSubst[kv.Key] = kv.Value;

                    var newHead = TermTraversal.ApplySubstitutionToAtom(clause.Head, fullSubst);

                    List<Guard>? newGuards = null;
                    if (clause.Guards is not null && clause.Guards.Count > 0)
                        newGuards = clause.Guards.Select(g => TermTraversal.ApplySubstitutionToGuard(g, fullSubst)).ToList();

                    var newBody = new List<Goal>();
                    for (int i = 0; i < clause.Body.Count; i++)
                    {
                        if (i == reduceIndex) continue;
                        newBody.Add(TermTraversal.ApplySubstitutionToGoal(clause.Body[i], fullSubst));
                    }
                    if (newBody.Count == 0)
                        newBody = new List<Goal> { new Goal("true", new List<Term>(), clause.Line, clause.Column) };

                    var simplifiedGuards = SimplifyGuards(newGuards, newHead);
                    expanded.Add(new Clause(newHead, guards: simplifiedGuards, body: newBody, line: clause.Line, column: clause.Column));
                    break;
                }

                default:
                    throw new InvalidOperationException("UnifyResult: unreachable subtype.");
            }
        }
        if (expanded.Count == 0) return new List<Clause> { clause };
        return expanded;
    }

    // ========================================================================
    // Variable renaming
    // ========================================================================

    private Clause RenameClauseVars(Clause clause)
    {
        var varNames = new HashSet<string>(StringComparer.Ordinal);
        CollectVarNamesFromAtom(clause.Head, varNames);
        if (clause.Guards is not null)
            foreach (var guard in clause.Guards)
                foreach (var arg in guard.Args) TermTraversal.CollectVarNames(arg, varNames);
        if (clause.Body is not null)
            foreach (var goal in clause.Body)
                foreach (var arg in goal.Args) TermTraversal.CollectVarNames(arg, varNames);

        var renaming = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in varNames)
            if (name != "_") renaming[name] = $"PE{_varCounter++}";

        var newHead   = ApplyRenamingToAtom(clause.Head, renaming);
        List<Guard>? newGuards = clause.Guards?.Select(g =>
            new Guard(g.Predicate, g.Args.Select(a => TermTraversal.ApplyRenaming(a, renaming)).ToList(), g.Line, g.Column, negated: g.Negated)).ToList();
        List<Goal>? newBody = clause.Body?.Select(g =>
            new Goal(g.Functor, g.Args.Select(a => TermTraversal.ApplyRenaming(a, renaming)).ToList(), g.Line, g.Column)).ToList();

        return new Clause(newHead, guards: newGuards, body: newBody, line: clause.Line, column: clause.Column);
    }

    // Stage-2-only helpers. The shared primitives (CollectVarNames, ApplyRenaming,
    // RenameUnitClauseVars, the unify/subst/resolve family) live in TermTraversal
    // (feature 077 consolidation — SC-004, no second copy).

    private static void CollectVarNamesFromAtom(Atom atom, HashSet<string> names)
    {
        foreach (var arg in atom.Args) TermTraversal.CollectVarNames(arg, names);
    }

    private static Atom ApplyRenamingToAtom(Atom atom, IReadOnlyDictionary<string, string> renaming) =>
        new Atom(atom.Functor, atom.Args.Select(a => TermTraversal.ApplyRenaming(a, renaming)).ToList(), atom.Line, atom.Column);

    // ========================================================================
    // Guard simplification (post-specialisation redundancy table)
    // ========================================================================

    private static List<Guard>? SimplifyGuards(List<Guard>? guards, Atom head)
    {
        if (guards is null || guards.Count == 0) return null;
        var simplified = new List<Guard>();
        foreach (var guard in guards)
        {
            if (IsRedundantGuard(guard, head)) continue;
            simplified.Add(guard);
        }
        return simplified.Count == 0 ? null : simplified;
    }

    private static bool IsRedundantGuard(Guard guard, Atom head)
    {
        if (guard.Args.Count != 1) return false;
        var concreteArg = GetConcreteArg(guard.Args[0]);
        if (concreteArg is null) return false;
        return guard.Predicate switch
        {
            "tuple"    or "compound"   => concreteArg is StructTerm,
            "list"     or "is_list"    => concreteArg is ListTerm,
            "integer"                  => concreteArg is ConstTerm ic && ic.Value is int or long,
            "number"                   => concreteArg is ConstTerm nc && (nc.Value is int or long or double or float),
            "atom"                     => concreteArg is ConstTerm ac && ac.Value is string,
            "ground"   or "no_readers" => IsGround(concreteArg),
            _                          => false,
        };
    }

    private static Term? GetConcreteArg(Term term)
    {
        if (term is VarTerm) return null;
        if (term is ConstTerm or StructTerm or ListTerm) return term;
        return null;
    }

    private static bool IsGround(Term term)
    {
        if (term is VarTerm)        return false;
        if (term is UnderscoreTerm) return true;
        if (term is ConstTerm)      return true;
        if (term is StructTerm s)   return s.Args.All(IsGround);
        if (term is ListTerm l)
        {
            if (l.IsNil) return true;
            var headG = l.Head is null || IsGround(l.Head);
            var tailG = l.Tail is null || IsGround(l.Tail);
            return headG && tailG;
        }
        return false;
    }
}
