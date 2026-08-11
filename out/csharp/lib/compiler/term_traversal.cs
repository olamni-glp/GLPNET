// lib/compiler/term_traversal.cs
//
// Feature 077 — guarded term-traversal utilities.
//
// ONE shared home for the compile-time unify / substitution / resolve / renaming
// primitives that were previously maintained as TWO divergent copies:
//   * analyzer.cs        DefinedGuardEvaluator  (the `_`-prefixed methods)
//   * partial_evaluator.cs PartialEvaluator     (the un-prefixed methods)
//
// The two copies were NOT byte-identical (5 material divergences, spec 077
// research Decision 3). This consolidation preserves every load-bearing
// behavioural difference by PARAMETERISING it, rather than picking a winner:
//   #1/#3 anonymous-var semantics  -> Func<Term,bool> isAnonymous  (per-caller)
//   #2    fresh-var prefix          -> string freshVarPrefix + ref long counter
//   #5    ResolveSubstitution type  -> IReadOnlyDictionary (the wider type)
// Divergence #4 (TransformClause orchestration) stays in each owning class —
// only the shared PRIMITIVES move here.
//
// The cycle guard (Phase 3/4) lands ON this module so it is defined exactly once
// for the substitution/resolve family and once for the structural family.

namespace GlpRuntime.Compiler;

/// <summary>
/// Shared, behaviour-preserving compile-time term-traversal primitives used by
/// both the analyzer's <c>DefinedGuardEvaluator</c> and the
/// <c>PartialEvaluator</c>. Static; per-caller behavioural knobs are parameters.
/// </summary>
internal static class TermTraversal
{
    // ========================================================================
    // GLP three-valued compile-time unification
    // ========================================================================

    internal static UnifyResult GlpUnifyForPE(
        IReadOnlyList<Term> callArgs, IReadOnlyList<Term> unitArgs, Func<Term, bool> isAnonymous)
    {
        if (callArgs.Count != unitArgs.Count)
            return new UnifyFail($"Arity mismatch: {callArgs.Count} vs {unitArgs.Count}");

        var substitution  = new Dictionary<string, Term>(StringComparer.Ordinal);
        var suspensionSet = new HashSet<string>(StringComparer.Ordinal);

        // Phase 1: Collection
        for (int i = 0; i < callArgs.Count; i++)
        {
            var result = UnifyTerms(callArgs[i], unitArgs[i], substitution, suspensionSet, isAnonymous);
            if (result is not null) return result;
        }

        // Phase 2: Resolution
        var unresolvedReaders = new HashSet<string>(StringComparer.Ordinal);
        foreach (var readerName in suspensionSet)
            if (!substitution.ContainsKey(readerName)) unresolvedReaders.Add(readerName);
        if (unresolvedReaders.Count > 0) return new UnifySuspend(unresolvedReaders);

        return new UnifySuccess(ResolveSubstitution(substitution));
    }

    private static void SubstSet(Dictionary<string, Term> subst, string key, Term value)
    {
        if (subst.TryGetValue(key, out var old)
            && old is VarTerm oldVar && !oldVar.IsReader
            && value is not VarTerm)
        {
            if (!subst.ContainsKey(oldVar.Name)) subst[oldVar.Name] = value;
        }
        subst[key] = value;
    }

    /// <summary>
    /// Pairwise term unification.
    /// Returns null on success (null-as-success convention); non-null on failure.
    /// Divergence #1: the anonymous-var predicate is supplied by the caller so
    /// the analyzer (Name.StartsWith('_')) and PE (Name == "_") keep their
    /// distinct, behaviour-affecting semantics.
    /// </summary>
    internal static UnifyResult? UnifyTerms(
        Term callArg, Term unitArg,
        Dictionary<string, Term> subst, HashSet<string> suspSet,
        Func<Term, bool> isAnonymous)
    {
        // (a) Anonymous on either side: success, no binding
        if (isAnonymous(callArg) || isAnonymous(unitArg)) return null;

        // (b) Call arg is writer
        if (callArg is VarTerm callVar && !callVar.IsReader)
        {
            if      (unitArg is VarTerm unitW && !unitW.IsReader) subst[unitW.Name] = callVar;
            else if (unitArg is VarTerm unitR &&  unitR.IsReader) subst[unitR.Name] = callVar;
            else                                                   subst[callVar.Name] = unitArg;
            return null;
        }

        // (c) Call arg is reader
        if (callArg is VarTerm callReader && callReader.IsReader)
        {
            var writerName = callReader.Name;
            if (unitArg is VarTerm uW && !uW.IsReader)
            {
                subst[uW.Name] = new VarTerm(writerName, false, callReader.Line, callReader.Column);
            }
            else if (unitArg is VarTerm uR && uR.IsReader)
            {
                subst[uR.Name] = new VarTerm(writerName, false, callReader.Line, callReader.Column);
                suspSet.Add(writerName);
            }
            else
            {
                suspSet.Add(writerName);
                if (subst.TryGetValue(writerName, out var existing))
                {
                    var compat = CheckCompatible(existing, unitArg, subst, suspSet);
                    if (compat is not null) return compat;
                }
                else
                {
                    subst[writerName] = unitArg;
                }
            }
            return null;
        }

        // (d) Call arg is constant
        if (callArg is ConstTerm callConst)
        {
            if (unitArg is ConstTerm unitConst)
                return object.Equals(callConst.Value, unitConst.Value)
                    ? null
                    : new UnifyFail($"Constant mismatch: {callConst.Value} vs {unitConst.Value}");
            if (unitArg is VarTerm uW2 && !uW2.IsReader) { SubstSet(subst, uW2.Name, callArg); return null; }
            if (unitArg is VarTerm uR2 &&  uR2.IsReader) { SubstSet(subst, uR2.Name, callArg); return null; }
            return new UnifyFail($"Constant {callConst.Value} cannot match structure {unitArg}");
        }

        // (e) Call arg is structure
        if (callArg is StructTerm callStruct)
        {
            if (unitArg is StructTerm unitStruct)
            {
                if (callStruct.Functor != unitStruct.Functor || callStruct.Args.Count != unitStruct.Args.Count)
                    return new UnifyFail($"Functor mismatch: {callStruct.Functor}/{callStruct.Args.Count} vs {unitStruct.Functor}/{unitStruct.Args.Count}");
                for (int i = 0; i < callStruct.Args.Count; i++)
                {
                    var r = UnifyTerms(callStruct.Args[i], unitStruct.Args[i], subst, suspSet, isAnonymous);
                    if (r is not null) return r;
                }
                return null;
            }
            if (unitArg is VarTerm uW3 && !uW3.IsReader) { SubstSet(subst, uW3.Name, callArg); return null; }
            if (unitArg is VarTerm uR3 &&  uR3.IsReader) { SubstSet(subst, uR3.Name, callArg); return null; }
            return new UnifyFail($"Structure {callStruct.Functor} cannot match {unitArg}");
        }

        // (f) Call arg is list
        if (callArg is ListTerm callList)
        {
            if (unitArg is ListTerm unitList)
            {
                if (callList.IsNil && unitList.IsNil) return null;
                if (callList.IsNil != unitList.IsNil) return new UnifyFail("List structure mismatch: nil vs non-nil");
                if (callList.Head is not null && unitList.Head is not null)
                {
                    var r = UnifyTerms(callList.Head, unitList.Head, subst, suspSet, isAnonymous);
                    if (r is not null) return r;
                }
                if (callList.Tail is not null && unitList.Tail is not null)
                {
                    var r = UnifyTerms(callList.Tail, unitList.Tail, subst, suspSet, isAnonymous);
                    if (r is not null) return r;
                }
                return null;
            }
            if (unitArg is VarTerm uW4 && !uW4.IsReader) { SubstSet(subst, uW4.Name, callArg); return null; }
            if (unitArg is VarTerm uR4 &&  uR4.IsReader) { SubstSet(subst, uR4.Name, callArg); return null; }
            return new UnifyFail($"List cannot match {unitArg}");
        }

        return new UnifyFail($"Unhandled case: {callArg.GetType().Name} vs {unitArg.GetType().Name}");
    }

    private static UnifyResult? CheckCompatible(
        Term existing, Term newTerm,
        Dictionary<string, Term> subst, HashSet<string> suspSet)
    {
        if (existing is ConstTerm e && newTerm is ConstTerm n)
        {
            if (!object.Equals(e.Value, n.Value))
                return new UnifyFail($"Incompatible bindings: {e.Value} vs {n.Value}");
            return null;
        }
        if (existing is StructTerm es && newTerm is StructTerm ns)
        {
            if (es.Functor != ns.Functor || es.Args.Count != ns.Args.Count)
                return new UnifyFail($"Incompatible structures: {es.Functor} vs {ns.Functor}");
            return null;
        }
        return null;   // loose-accept for variable cases (deeper check deferred)
    }

    // ========================================================================
    // Substitution resolution (chain flattening + cycle protection)
    // Divergence #5: returns the wider IReadOnlyDictionary; mutating callers copy.
    // ========================================================================

    internal static IReadOnlyDictionary<string, Term> ResolveSubstitution(Dictionary<string, Term> subst)
    {
        var resolved = new Dictionary<string, Term>(StringComparer.Ordinal);
        foreach (var entry in subst)
            resolved[entry.Key] = ResolveTerm(entry.Value, subst, new HashSet<string>(StringComparer.Ordinal));
        return resolved;
    }

    private static Term ResolveTerm(Term term, IReadOnlyDictionary<string, Term> subst, HashSet<string> visited)
    {
        if (term is VarTerm varTerm)
        {
            if (visited.Contains(varTerm.Name)) return term;   // cycle — return as-is
            if (subst.TryGetValue(varTerm.Name, out var bound))
            {
                visited.Add(varTerm.Name);
                var resolved = ResolveTerm(bound, subst, visited);
                // Reader-status preservation: reader var resolving to writer var returns reader var
                if (varTerm.IsReader && resolved is VarTerm rv && !rv.IsReader)
                    return new VarTerm(rv.Name, true, rv.Line, rv.Column);
                return resolved;
            }
            return term;
        }
        if (term is StructTerm s)
            return new StructTerm(s.Functor,
                s.Args.Select(a => ResolveTerm(a, subst, new HashSet<string>(visited, StringComparer.Ordinal))).ToList(),
                s.Line, s.Column);
        if (term is ListTerm l)
        {
            if (l.IsNil) return l;
            return new ListTerm(
                l.Head is not null ? ResolveTerm(l.Head, subst, new HashSet<string>(visited, StringComparer.Ordinal)) : null,
                l.Tail is not null ? ResolveTerm(l.Tail, subst, new HashSet<string>(visited, StringComparer.Ordinal)) : null,
                l.Line, l.Column);
        }
        return term;   // ConstTerm / UnderscoreTerm unchanged
    }

    // ========================================================================
    // Substitution application family
    // ========================================================================

    internal static Term ApplySubstitution(Term term, IReadOnlyDictionary<string, Term> subst) =>
        ApplySubstitution(term, subst, new HashSet<string>(StringComparer.Ordinal));

    // Feature 077 (US1/US2, FR-004) — cycle guard on the substitution-closure walker.
    // ApplySubstitution follows subst[name] transitively; a self-referential binding
    // (X -> ...X..., the F-069-1 shape) recursed forever -> uncatchable StackOverflow.
    // `active` holds the var NAMES on the CURRENT resolution path (var-name keyed,
    // generalising the ResolveTerm visited-set into one guard). Re-following a name
    // already on the active path is an unbreakable cycle -> hard-fail with a catchable
    // CompileError. Add/remove is balanced (finally), so a DAG (same name resolved on
    // sibling paths) and a deep acyclic chain are NOT cycles (FR-005/FR-006).
    private static Term ApplySubstitution(
        Term term, IReadOnlyDictionary<string, Term> subst, HashSet<string> active)
    {
        if (term is VarTerm varTerm)
        {
            if (varTerm.Name == "_") return term;   // underscore unchanged
            if (subst.TryGetValue(varTerm.Name, out var replacement))
            {
                if (varTerm.IsReader && replacement is VarTerm rv && !rv.IsReader)
                    return new VarTerm(rv.Name, true, rv.Line, rv.Column);
                if (!active.Add(varTerm.Name))
                    throw CyclicTermError(varTerm, "term_traversal");
                try { return ApplySubstitution(replacement, subst, active); }   // transitive-closure
                finally { active.Remove(varTerm.Name); }
            }
            return term;
        }
        if (term is StructTerm s)
            return new StructTerm(s.Functor, s.Args.Select(a => ApplySubstitution(a, subst, active)).ToList(), s.Line, s.Column);
        if (term is ListTerm l)
        {
            if (l.IsNil) return l;
            return new ListTerm(
                l.Head is not null ? ApplySubstitution(l.Head, subst, active) : null,
                l.Tail is not null ? ApplySubstitution(l.Tail, subst, active) : null,
                l.Line, l.Column);
        }
        if (term is UnderscoreTerm) return term;
        return term;   // ConstTerm passthrough
    }

    // ========================================================================
    // Cyclic-term diagnostic (feature 077, FR-004 / contract C3)
    // ========================================================================

    /// <summary>
    /// The catchable diagnostic raised when a term traversal detects an unbreakable
    /// cycle — replaces the former uncatchable StackOverflowException (SC-001).
    /// Names the offending node where possible; caught by the existing compile driver.
    /// </summary>
    internal static CompileError CyclicTermError(Term at, string phase, string? detail = null)
    {
        var what = at switch
        {
            VarTerm v    => $" (variable '{v.Name}')",
            StructTerm s => $" (functor '{s.Functor}/{s.Args.Count}')",
            _            => "",
        };
        var suffix = detail is null ? "" : $" [{detail}]";
        return new CompileError(
            $"Cyclic term detected during compile-time traversal{what}{suffix}: the term refers to " +
            "itself (occurs-check violation) and cannot be finitely resolved.",
            at.Line, at.Column, phase: phase);
    }

    internal static Atom ApplySubstitutionToAtom(Atom atom, IReadOnlyDictionary<string, Term> subst) =>
        new Atom(atom.Functor, atom.Args.Select(a => ApplySubstitution(a, subst)).ToList(), atom.Line, atom.Column);

    internal static Guard ApplySubstitutionToGuard(Guard guard, IReadOnlyDictionary<string, Term> subst) =>
        new Guard(guard.Predicate, guard.Args.Select(a => ApplySubstitution(a, subst)).ToList(), guard.Line, guard.Column, negated: guard.Negated);

    internal static Goal ApplySubstitutionToGoal(Goal goal, IReadOnlyDictionary<string, Term> subst)
    {
        // Preserve RemoteGoal wrapper (M # proc(...))
        if (goal is RemoteGoal rg)
        {
            var newModule = ApplySubstitution(rg.Module, subst);
            var newInner  = ApplySubstitutionToGoal(rg.Goal, subst);
            return new RemoteGoal(newModule, newInner, rg.Line, rg.Column);
        }
        // Preserve SpawnGoal wrapper (Goal@Agent)
        if (goal is SpawnGoal sg)
        {
            var newInner = ApplySubstitutionToGoal(sg.InnerGoal, subst);
            return new SpawnGoal(newInner, sg.AgentId, sg.Line, sg.Column);
        }
        return new Goal(goal.Functor, goal.Args.Select(a => ApplySubstitution(a, subst)).ToList(), goal.Line, goal.Column);
    }

    // ========================================================================
    // Variable name collection + renaming
    // ========================================================================

    internal static void CollectVarNames(Term term, HashSet<string> names)
    {
        switch (term)
        {
            case VarTerm varTerm:
                names.Add(varTerm.Name);
                break;
            case StructTerm s:
                foreach (var arg in s.Args) CollectVarNames(arg, names);
                break;
            case ListTerm l:
                if (l.Head is not null) CollectVarNames(l.Head, names);
                if (l.Tail is not null) CollectVarNames(l.Tail, names);
                break;
            // ConstTerm / UnderscoreTerm: silent no-op — no default arm (matches Dart no-else)
        }
    }

    internal static Term ApplyRenaming(Term term, IReadOnlyDictionary<string, string> renaming)
    {
        switch (term)
        {
            case VarTerm varTerm when varTerm.Name == "_":
                // Underscore demotion (load-bearing): VarTerm("_") → fresh UnderscoreTerm
                return new UnderscoreTerm(varTerm.Line, varTerm.Column);
            case VarTerm varTerm when renaming.TryGetValue(varTerm.Name, out var newName):
                // Reader-status preservation: renamed VarTerm keeps its IsReader flag
                return new VarTerm(newName, varTerm.IsReader, varTerm.Line, varTerm.Column);
            case VarTerm:
                return term;
            case StructTerm s:
                return new StructTerm(s.Functor, s.Args.Select(a => ApplyRenaming(a, renaming)).ToList(), s.Line, s.Column);
            case ListTerm l:
                return new ListTerm(
                    l.Head is not null ? ApplyRenaming(l.Head, renaming) : null,
                    l.Tail is not null ? ApplyRenaming(l.Tail, renaming) : null,
                    l.Line, l.Column);
            case UnderscoreTerm:
                return term;
            default:
                return term;   // ConstTerm passthrough
        }
    }

    /// <summary>
    /// Rename every non-anonymous variable in <paramref name="args"/> to a fresh
    /// name. Divergence #2: caller supplies the prefix ("PE_" for the analyzer,
    /// "PE" for the PE) and the shared long counter (by ref). Divergence #3: the
    /// "which names are anonymous" filter is the SAME isAnonymous knob as
    /// divergence #1, so a name is renamed iff it is not anonymous.
    /// </summary>
    internal static List<Term> RenameUnitClauseVars(
        IReadOnlyList<Term> args, string freshVarPrefix, ref long counter, Func<Term, bool> isAnonymous)
    {
        var varNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var arg in args) CollectVarNames(arg, varNames);

        var renaming = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in varNames)
            if (!isAnonymous(new VarTerm(name, false, 0, 0)))
                renaming[name] = $"{freshVarPrefix}{counter++}";

        return args.Select(arg => ApplyRenaming(arg, renaming)).ToList();
    }

    // ========================================================================
    // Per-caller anonymous-var predicates (divergence #1/#3 — preserved, not flattened)
    // ========================================================================

    /// <summary>Analyzer semantics: UnderscoreTerm OR any VarTerm whose name starts with '_'.</summary>
    internal static readonly Func<Term, bool> AnalyzerIsAnonymous =
        term => term is UnderscoreTerm || (term is VarTerm v && v.Name.StartsWith('_'));

    /// <summary>PE semantics: UnderscoreTerm OR a VarTerm named exactly "_".</summary>
    internal static readonly Func<Term, bool> PeIsUnderscore =
        term => term is UnderscoreTerm || (term is VarTerm v && v.Name == "_");
}
