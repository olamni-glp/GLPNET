---
path: lib/compiler/partial_evaluator.dart
cycle_group_id: 38
scc_siblings: []
generated_at: 2026-05-21T16:07:13Z
source_sha256: 8ac90433fa30c517b59e6f21f1860214b4493128880376e072467c37f92385ab
schema_version: 1
---

# Conversion Plan: lib/compiler/partial_evaluator.dart

## 1. Source Analysis

The file is the GLP **partial evaluator** — a 1037-line source-to-source AST
transformer. It runs in two stages over a `Program` (from `ast.dart`):

- **Stage 1 — `transformDefinedGuards(Program)`**: unfolds *defined guards*
  (calls to single-unit-clause procedures appearing in guard position) by
  fixpoint-iterating each clause's guard list; on each guard whose
  `name/arity` signature matches a known unit clause, it renames the unit's
  variables to fresh `PE<n>` names, runs the file-local
  three-valued `_glpUnifyForPE`, then either throws `CompileError` (Fail /
  Suspend / negated / non-unit-clause-in-guard) or applies the resulting
  substitution to head, remaining guards, and body and restarts.
- **Stage 2 — `unfoldReduceCalls(Program)`**: collects `reduce/2` unit-clause
  facts; for each clause with a `reduce/2` call in its body, tries to unfold
  the FIRST such call against every fact, producing zero, one, or many
  expansion clauses. Suspend is treated identically to Fail (skip this fact).

The file additionally hosts:

- two top-level globals plus a public setter and a lazy memoised getter for
  prelude unit clauses (parsed once from `programs/self.glp`);
- an instance `_varCounter` (monotonic `PE<n>` fresh-name supplier);
- a recursive AST walk family — `_unifyTerms` (six-arm
  writer/reader/const/struct/list/underscore matrix), `_substSet`
  (alias-chain propagation), `_checkCompatible` (loose structural compat),
  `_isUnderscore`, `_resolveSubstitution` + `_resolveTerm` (chain flattening
  with cycle protection and reader-status preservation), `_applySubstitution`
  + `_applySubstitutionToAtom` / `Guard` / `Goal` (Goal variant preserves
  `RemoteGoal`/`SpawnGoal` wrappers), `_simplifyGuards` + `_isRedundantGuard`
  + `_getConcreteArg` + `_isGround` (post-specialisation guard-redundancy
  table).

Imports: five — four sibling-folder (`ast.dart`, `error.dart`, `lexer.dart`,
`parser.dart`, `unify_result.dart`) plus one cross-folder selective import
`'../analysis/type_checker/prelude.dart' show builtinProcedures`. The
`UnifyResult` sealed ADT is **lifted** to `lib/compiler/unify_result.dart`
(commit `213e5601`, escalation #3 close); this file's `PartialEvaluator` is
the *lenient* variant (returns failure on suspend / non-reducing guards),
distinct from the analyzer's strict `DefinedGuardEvaluator`.

Concurrency: PURELY synchronous; single-isolate; no `async`/`await`,
`Future`, `Stream`, `Completer`, `Isolate`; no `dart:io`/`dart:ffi`/
`dart:isolate`. No mixins, extensions, records, operator overloading,
extension types, or FFI.

## 2. Dart → C#/.NET Conversion Plan

Each construct below mirrors the convspec one-for-one (FR-024 cache
discipline — no re-derivation).

### 2.1 Module / imports

- Place every type emitted from this file in namespace
  `Glp.Runtime.Compiler` (same namespace as `ast.dart`, `error.dart`,
  `lexer.dart`, `parser.dart`, `unify_result.dart` siblings) → the four
  sibling Dart imports COLLAPSE TO ZERO C# `using`s.
- The one cross-folder selective import `'../analysis/type_checker/prelude.dart'
  show builtinProcedures` → `using static
  Glp.Runtime.Analysis.TypeChecker.Prelude;` (Microsoft Learn `using static`
  imports the accessible static members and nested types — the closest
  C# equivalent to Dart `show` filter, since the only consumed symbol IS
  the static `BuiltinProcedures` set).
- `UnifyResult` consumed from `unify_result.dart` (lifted ADT) — present in
  the same `Glp.Runtime.Compiler` namespace, no `using` needed.

### 2.2 Top-level globals → `static class PreludeUnitClauses`

C# forbids free top-level mutable fields and free functions
(`csharp-static-class-no-toplevel-members` idiom, cached from
prelude.dart). Wrap both globals + the setter + the lazy getter in a
single hosting class:

```
internal static class PreludeUnitClauses
{
    private static string? _preludeUnitClauseSource = null;
    private static Dictionary<string, IReadOnlyList<Term>>? _cachedPreludeUnitClauses = null;

    public static void SetPreludeUnitClauseSource(string source) { ... }
    public static IReadOnlyDictionary<string, IReadOnlyList<Term>> GetPreludeUnitClauses() { ... }
}
```

- Field initialisers `= null;` are EXPLICIT (Microsoft Learn nullable
  reference types — reviewer clarity even when the language permits
  default-null).
- Setter: assigns `_preludeUnitClauseSource = source;` THEN clears
  `_cachedPreludeUnitClauses = null;` — ordering is load-bearing (see
  convspec nuance: a concurrent reader between the two assignments
  must not observe a stale cache against a new source).
- Getter: early-return on cache hit (`if (_cachedPreludeUnitClauses is
  not null) return _cachedPreludeUnitClauses;`); coalesce
  `_preludeUnitClauseSource ?? string.Empty`; length-0 short-circuit
  caches `new Dictionary<string, IReadOnlyList<Term>>(StringComparer.
  Ordinal)` and returns it; else `new Lexer(source).Tokenize()` →
  `new Parser(tokens).ParseModule()` → iterate `module.Procedures`,
  filter via the lifted `IsUnitClauseShape` helper (see §2.4) PLUS
  the "exactly-one-clause" predicate, key by `$"{proc.Name}/
  {proc.Arity}"`, value the head args (`IReadOnlyList<Term>`).
- NOT a `Lazy<T>` — `Lazy<T>` cannot be RESET, and the setter MUST
  invalidate the cache (Microsoft Learn: "After a Lazy<T>'s Value is
  initialised, further calls return the same instance" — exact
  opposite of required).

### 2.3 `class PartialEvaluator`

```
public class PartialEvaluator
{
    private long _varCounter = 0;

    public Program TransformDefinedGuards(Program program) { ... }
    public Program UnfoldReduceCalls(Program program) { ... }

    // helpers — all private (instance or static as noted below)
}
```

- `int _varCounter` → `long` per the recurring 64-bit-width idiom
  (token.dart / lexer.dart / parser.dart family; Dart `int` is 64-bit
  on native).
- `'PE${_varCounter++}'` → `$"PE{_varCounter++}"` — post-increment then
  interpolate is IDENTICAL in both languages (Microsoft Learn `++`: "The
  result of x++ is the value of x before the operation").
- Counter is INSTANCE-scoped, monotonically grows over the lifetime of
  one `PartialEvaluator`; never reset.

### 2.4 `IsUnitClauseShape(Clause)` — lifted helper

The Dart file contains THREE byte-identical body-shape filter copies
(`getPreludeUnitClauses`, `_collectUnitClauses`, `_collectReduceFacts`).
Lift to a SINGLE private static helper:

```
private static bool IsUnitClauseShape(Clause clause) =>
    (clause.Guards is null || clause.Guards.Count == 0) &&
    (clause.Body is null
     || clause.Body.Count == 0
     || (clause.Body.Count == 1 && clause.Body[0].Functor == "true" && clause.Body[0].Args.Count == 0));
```

The "exactly one clause" predicate stays at each call site (callers pair
it differently — prelude/extract requires one-clause; reduce-facts
accepts any number).

Load-bearing GLP invariant preserved: "no body" and "body is singleton
`true/0`" are BOTH unit-clause shapes (parser may emit either).

### 2.5 `TransformDefinedGuards(Program)` — Stage 1 entry

```
public Program TransformDefinedGuards(Program program)
{
    var unitClauses = new Dictionary<string, IReadOnlyList<Term>>(StringComparer.Ordinal);
    foreach (var kv in PreludeUnitClauses.GetPreludeUnitClauses()) unitClauses[kv.Key] = kv.Value;
    foreach (var kv in CollectUnitClauses(program))             unitClauses[kv.Key] = kv.Value;

    var allProcedures = CollectAllProcedures(program);
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
```

- Dart spread-merge `{...A, ...B}` (Dart official: B's keys last → B
  overrides) → C# two-`foreach` with indexer-assign (Microsoft Learn
  `Dictionary<TKey,TValue>` indexer: "If the key already exists in the
  dictionary, the value is overwritten."). User definitions override
  prelude (second foreach is user).
- Immutability-by-construction preserved — NO input mutation; every
  per-procedure/per-clause loop allocates a NEW list and constructs
  NEW `Procedure` / `Clause` instances.
- Pre-size capacity hints (`new List<Procedure>(program.Procedures.
  Count)`) are an idiomatic performance improvement; observably
  equivalent to `new List<Procedure>()`.

### 2.6 `UnfoldReduceCalls(Program)` — Stage 2 entry

```
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
```

- Short-circuit identity-return on `reduceFacts.Count == 0` is load-
  bearing (downstream callers may compare by reference for caching).
- 1:N per clause — use `AddRange` (Microsoft Learn `List<T>.AddRange`:
  "Adds the elements of the specified collection to the end of the
  `List<T>`") rather than `Add`.

### 2.7 `CollectAllProcedures(Program)` — signature set

```
private static HashSet<string> CollectAllProcedures(Program program)
{
    var procedures = new HashSet<string>(StringComparer.Ordinal);
    foreach (var proc in program.Procedures) procedures.Add($"{proc.Name}/{proc.Arity}");
    return procedures;
}
```

- `Set<String>` → `HashSet<string>` (cached `rf-dart-set-to-csharp-
  hashset` from prelude.dart family — mutable here, NOT `FrozenSet`).
- `StringComparer.Ordinal` mandatory — signatures ASCII-only, exact
  match contract (Microsoft Learn: "Ordinal string comparison is the
  fastest kind of comparison" + Turkish-I locale safety).

### 2.8 `CollectUnitClauses(Program)` / `CollectReduceFacts(Program)`

```
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
```

- `CollectUnitClauses` requires BOTH proc-has-exactly-one-clause AND
  body-shape; `CollectReduceFacts` requires ONLY body-shape (each
  reduce/2 clause is an independent rewrite rule).
- `List<Clause>` (not `HashSet`) preserves declaration order — all
  matching facts are tried as separate expansions.

### 2.9 `TransformClause(Clause, …)` — Stage-1 fixpoint loop with three throw-arms

```
private Clause TransformClause(
    Clause clause,
    IReadOnlyDictionary<string, IReadOnlyList<Term>> unitClauses,
    IReadOnlySet<string> allProcedures)
{
    if (clause.Guards is null || clause.Guards.Count == 0) return clause;

    var currentHead = clause.Head;
    var currentGuards = new List<Guard>(clause.Guards);
    var currentBody = clause.Body is not null ? new List<Goal>(clause.Body) : null;
    bool changed = true;

    while (changed)
    {
        changed = false;
        var remainingGuards = new List<Guard>();
        for (int i = 0; i < currentGuards.Count; i++)
        {
            var guard = currentGuards[i];
            var key = $"{guard.Predicate}/{guard.Args.Count}";
            if (unitClauses.TryGetValue(key, out var unitArgs))
            {
                if (guard.Negated)
                    throw new CompileError(
                        $"Defined guard \"{guard.Predicate}\" cannot be negated",
                        guard.Line, guard.Column, phase: "analyzer");
                var renamedArgs = RenameUnitClauseVars(unitArgs);
                var result = GlpUnifyForPE(guard.Args, renamedArgs);
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
                        currentHead = ApplySubstitutionToAtom(currentHead, success.Substitution);
                        var restGuards = currentGuards.GetRange(i + 1, currentGuards.Count - i - 1)
                            .Select(g => ApplySubstitutionToGuard(g, success.Substitution)).ToList();
                        remainingGuards = remainingGuards
                            .Select(g => ApplySubstitutionToGuard(g, success.Substitution)).ToList();
                        if (currentBody is not null)
                            currentBody = currentBody
                                .Select(g => ApplySubstitutionToGoal(g, success.Substitution)).ToList();
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
        body: currentBody,
        line: clause.Line, column: clause.Column);
}
```

- **Fixpoint**: outer `while (changed)` restarts on every reduction
  because the substitution from one guard may cascade into others.
- **Three throw-arms**: Fail (`never succeed`, phase `analyzer`),
  Suspend (`must be fully reducible`, phase `analyzer`), multi-
  clause-procedure-in-guard (`cannot call in guard position`, phase
  `partial_evaluator`); plus the negated-defined-guard guard.
- **Sealed-leaf switch with throwing default**: per the cached
  `rf-dart-abstract-marker-base-to-csharp-abstract-sealed-leaves`
  idiom (ast.dart), C# sealed leaves do not provide compile-time
  exhaustiveness over user hierarchies — runtime `default: throw`
  preserves the Dart-3 `sealed` static guarantee.
- **Substitution propagation to THREE lists**: `remainingGuards`,
  `restGuards`, `currentBody`. Ordering: substitute first, then
  concatenate. Preserved verbatim.

### 2.10 `UnfoldReduceInClause(Clause, IReadOnlyList<Clause>)` — Stage-2 single-clause unfolder

```
private List<Clause> UnfoldReduceInClause(Clause clause, IReadOnlyList<Clause> reduceFacts)
{
    if (clause.Body is null || clause.Body.Count == 0) return new List<Clause> { clause };

    int reduceIndex = -1;
    Goal? reduceCall = null;
    for (int i = 0; i < clause.Body.Count; i++)
    {
        var goal = clause.Body[i];
        if (goal.Functor == "reduce" && goal.Args.Count == 2) { reduceIndex = i; reduceCall = goal; break; }
    }
    if (reduceCall is null) return new List<Clause> { clause };

    var expanded = new List<Clause>();
    foreach (var fact in reduceFacts)
    {
        var renamedFact = RenameClauseVars(fact);
        var factPattern = renamedFact.Head.Args[0];
        var factReplacement = renamedFact.Head.Args[1];
        var callPattern = reduceCall.Args[0];
        var callResult = reduceCall.Args[1];

        var result = GlpUnifyForPE(new[] { callPattern }, new[] { factPattern });
        switch (result)
        {
            case UnifyFail:
            case UnifySuspend:
                continue;   // skip this fact, try next
            case UnifySuccess success:
            {
                var resultUnify = GlpUnifyForPE(new[] { callResult }, new[] { factReplacement });
                var fullSubst = new Dictionary<string, Term>(success.Substitution, StringComparer.Ordinal);
                if (resultUnify is UnifySuccess rs)
                    foreach (var kv in rs.Substitution) fullSubst[kv.Key] = kv.Value;

                var newHead = ApplySubstitutionToAtom(clause.Head, fullSubst);

                List<Guard>? newGuards = null;
                if (clause.Guards is not null && clause.Guards.Count > 0)
                    newGuards = clause.Guards.Select(g => ApplySubstitutionToGuard(g, fullSubst)).ToList();

                var newBody = new List<Goal>();
                for (int i = 0; i < clause.Body.Count; i++)
                {
                    if (i == reduceIndex) continue;   // drop the reduce call
                    newBody.Add(ApplySubstitutionToGoal(clause.Body[i], fullSubst));
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
```

- FIRST-reduce-only per pass (Dart `break` preserved verbatim).
- Suspend ≡ Fail at this site — both skip-this-fact.
- Two-step unify (call-pattern vs fact-pattern, then call-result vs
  fact-replacement); substitutions MERGED with second-wins (Microsoft
  Learn `Dictionary<TKey,TValue>(IDictionary<TKey,TValue>)` copy-
  constructor + indexer-overwrite).
- Empty-body sentinel: replace `[]` with singleton `[Goal("true", [],
  …)]` — the inverse of `IsUnitClauseShape`'s "body is just true"
  case. Round-trip-preserves the GLP invariant "every clause body is
  non-empty or normalised to true".

### 2.11 `RenameClauseVars(Clause)` / `RenameUnitClauseVars(IReadOnlyList<Term>)`

```
private Clause RenameClauseVars(Clause clause)
{
    var varNames = new HashSet<string>(StringComparer.Ordinal);
    CollectVarNamesFromAtom(clause.Head, varNames);
    if (clause.Guards is not null)
        foreach (var guard in clause.Guards)
            foreach (var arg in guard.Args) CollectVarNames(arg, varNames);
    if (clause.Body is not null)
        foreach (var goal in clause.Body)
            foreach (var arg in goal.Args) CollectVarNames(arg, varNames);

    var renaming = new Dictionary<string, string>(StringComparer.Ordinal);
    foreach (var name in varNames)
        if (name != "_") renaming[name] = $"PE{_varCounter++}";

    var newHead = ApplyRenamingToAtom(clause.Head, renaming);
    List<Guard>? newGuards = clause.Guards?.Select(g =>
        new Guard(g.Predicate, g.Args.Select(a => ApplyRenaming(a, renaming)).ToList(), g.Line, g.Column, negated: g.Negated)).ToList();
    List<Goal>? newBody = clause.Body?.Select(g =>
        new Goal(g.Functor, g.Args.Select(a => ApplyRenaming(a, renaming)).ToList(), g.Line, g.Column)).ToList();

    return new Clause(newHead, guards: newGuards, body: newBody, line: clause.Line, column: clause.Column);
}

private List<Term> RenameUnitClauseVars(IReadOnlyList<Term> args)
{
    var varNames = new HashSet<string>(StringComparer.Ordinal);
    foreach (var arg in args) CollectVarNames(arg, varNames);
    var renaming = new Dictionary<string, string>(StringComparer.Ordinal);
    foreach (var name in varNames)
        if (name != "_") renaming[name] = $"PE{_varCounter++}";
    return args.Select(arg => ApplyRenaming(arg, renaming)).ToList();
}
```

- **Underscore-preservation (load-bearing)**: `_` is anonymous; each
  occurrence is a distinct writer. Skipping `_` in the renaming map
  preserves SRSW; `ApplyRenaming` then NORMALISES `VarTerm` with
  `Name == "_"` to a fresh `UnderscoreTerm` (see §2.13).
- **Walk-order determinism cosmetic flag**: Dart `Set<String>` is
  `LinkedHashSet` (insertion order); C# `HashSet<string>` is
  unordered — fresh-counter assignment to a given source name may
  differ across ports, but observable output is identical (each name
  gets a unique fresh name regardless of iteration order).

### 2.12 `CollectVarNames(Term, HashSet<string>)` / `CollectVarNamesFromAtom(Atom, HashSet<string>)`

```
private static void CollectVarNames(Term term, HashSet<string> names)
{
    switch (term)
    {
        case VarTerm varTerm:    names.Add(varTerm.Name); break;
        case StructTerm s:       foreach (var arg in s.Args) CollectVarNames(arg, names); break;
        case ListTerm l:
            if (l.Head is not null) CollectVarNames(l.Head, names);
            if (l.Tail is not null) CollectVarNames(l.Tail, names);
            break;
        // ConstTerm / UnderscoreTerm: silent fallthrough (no default arm — matches Dart no-else)
    }
}

private static void CollectVarNamesFromAtom(Atom atom, HashSet<string> names)
{
    foreach (var arg in atom.Args) CollectVarNames(arg, names);
}
```

- Silent-fallthrough for `ConstTerm`/`UnderscoreTerm` matches the Dart
  `is`-chain-without-else shape exactly. Microsoft Learn pattern
  matching switch statement: cases without `default` simply fall
  through.
- Accumulator passed by reference (collections are reference types in
  both languages); no `ref`/`out`.

### 2.13 `ApplyRenaming(Term, IReadOnlyDictionary<string,string>)` / `ApplyRenamingToAtom`

```
private static Term ApplyRenaming(Term term, IReadOnlyDictionary<string, string> renaming)
{
    switch (term)
    {
        case VarTerm varTerm when varTerm.Name == "_":
            return new UnderscoreTerm(varTerm.Line, varTerm.Column);
        case VarTerm varTerm when renaming.TryGetValue(varTerm.Name, out var newName):
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

private static Atom ApplyRenamingToAtom(Atom atom, IReadOnlyDictionary<string, string> renaming) =>
    new Atom(atom.Functor, atom.Args.Select(a => ApplyRenaming(a, renaming)).ToList(), atom.Line, atom.Column);
```

- **Underscore demotion (load-bearing)**: `VarTerm` with `Name == "_"`
  is REPLACED by a fresh `UnderscoreTerm`. The parser may produce
  either shape; this rebuild normalises.
- **`TryGetValue` with out-pattern** in the `when` guard avoids the
  Dart `renaming[term.name]!` non-null-assert double-lookup
  (`rf-dart-map-lookup-to-csharp-trygetvalue`, parser.dart cached).
- **Reader-status preservation**: `varTerm.IsReader` flows through —
  renaming does NOT flip the reader/writer marker.

### 2.14 `GlpUnifyForPE(IReadOnlyList<Term>, IReadOnlyList<Term>)` — three-valued

```
private UnifyResult GlpUnifyForPE(IReadOnlyList<Term> callArgs, IReadOnlyList<Term> unitArgs)
{
    if (callArgs.Count != unitArgs.Count)
        return new UnifyFail($"Arity mismatch: {callArgs.Count} vs {unitArgs.Count}");

    var substitution = new Dictionary<string, Term>(StringComparer.Ordinal);
    var suspensionSet = new HashSet<string>(StringComparer.Ordinal);

    // Phase 1: Collection
    for (int i = 0; i < callArgs.Count; i++)
    {
        var result = UnifyTerms(callArgs[i], unitArgs[i], substitution, suspensionSet);
        if (result is not null) return result;   // failure
    }

    // Phase 2: Resolution
    var unresolvedReaders = new HashSet<string>(StringComparer.Ordinal);
    foreach (var readerName in suspensionSet)
        if (!substitution.ContainsKey(readerName)) unresolvedReaders.Add(readerName);
    if (unresolvedReaders.Count > 0) return new UnifySuspend(unresolvedReaders);

    return new UnifySuccess(ResolveSubstitution(substitution));
}
```

- Sealed-three-valued contract: every caller assumes EXACTLY one of
  `UnifySuccess` / `UnifyFail` / `UnifySuspend`.
- **Phase-1-collect / Phase-2-resolve separation (load-bearing GLP
  semantics)**: a reader X? may be seen BEFORE its writer X is bound;
  Phase 1 records `suspensionSet.Add(X)` and tentatively records
  `substitution[X] = factArg`; Phase 2 checks whether any reader's
  WRITER (`!substitution.ContainsKey(readerName)`) is unresolved.
  Subtle, preserved verbatim.

### 2.15 `UnifyTerms(Term, Term, Dictionary<string,Term>, HashSet<string>)` — six-arm matrix

Preserve the Dart `if`/`else if` cascade verbatim (not a pattern-
switch — too many cross-cutting conditions to flatten cleanly).
Returns `UnifyResult?` — `null` is SUCCESS, non-null is FAIL.

```
private UnifyResult? UnifyTerms(Term callArg, Term unitArg, Dictionary<string, Term> subst, HashSet<string> suspSet)
{
    // (a) Underscore on either side: success, no binding
    if (IsUnderscore(callArg) || IsUnderscore(unitArg)) return null;

    // (b) Call arg is writer
    if (callArg is VarTerm callVar && !callVar.IsReader)
    {
        if (unitArg is VarTerm unitVar && !unitVar.IsReader)       subst[unitVar.Name] = callVar;
        else if (unitArg is VarTerm unitR && unitR.IsReader)       subst[unitR.Name]   = callVar;
        else                                                       subst[callVar.Name] = unitArg;
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
        if (unitArg is VarTerm unitW && !unitW.IsReader) { SubstSet(subst, unitW.Name, callArg); return null; }
        if (unitArg is VarTerm unitR && unitR.IsReader)  { SubstSet(subst, unitR.Name, callArg); return null; }
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
                var r = UnifyTerms(callStruct.Args[i], unitStruct.Args[i], subst, suspSet);
                if (r is not null) return r;
            }
            return null;
        }
        if (unitArg is VarTerm uW && !uW.IsReader) { SubstSet(subst, uW.Name, callArg); return null; }
        if (unitArg is VarTerm uR && uR.IsReader)  { SubstSet(subst, uR.Name, callArg); return null; }
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
                var r = UnifyTerms(callList.Head, unitList.Head, subst, suspSet);
                if (r is not null) return r;
            }
            if (callList.Tail is not null && unitList.Tail is not null)
            {
                var r = UnifyTerms(callList.Tail, unitList.Tail, subst, suspSet);
                if (r is not null) return r;
            }
            return null;
        }
        if (unitArg is VarTerm uW && !uW.IsReader) { SubstSet(subst, uW.Name, callArg); return null; }
        if (unitArg is VarTerm uR && uR.IsReader)  { SubstSet(subst, uR.Name, callArg); return null; }
        return new UnifyFail($"List cannot match {unitArg}");
    }

    return new UnifyFail($"Unhandled case: {callArg.GetType().Name} vs {unitArg.GetType().Name}");
}
```

- **Null-as-success convention preserved verbatim** — every caller
  uses this contract; converting would diverge.
- **`object.Equals(callConst.Value, unitConst.Value)`** — `ConstTerm.
  Value` is `object?` polymorphic (int/long/double/string per ast.dart
  spec); Microsoft Learn `Object.Equals(Object, Object)` handles
  null-safe polymorphic value-equality.
- **`callArg.runtimeType`** → `callArg.GetType().Name` (Microsoft
  Learn `Type.Name`).
- **Direction-asymmetric aliasing** (writer/writer ⇒ alias unit-side
  writer to call-side writer) preserved — load-bearing because the
  substitution is later applied to the call's clause, not the unit
  clause.

### 2.16 `SubstSet(Dictionary<string,Term>, string, Term)` — alias-chain propagation

```
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
```

- **Propagation invariant (load-bearing)**: when X→Y is in subst (X
  aliased to writer Y) and we set X→f(a), MUST ALSO set Y→f(a) so
  `ResolveSubstitution` can find Y's binding. The
  `!subst.ContainsKey(old.Name)` guard prevents overwriting an
  existing Y binding.
- `value is not VarTerm` guard: only propagate when new value is
  CONCRETE (const/struct/list); for VarTerm-to-VarTerm aliasing we
  leave the chain in place and let `ResolveSubstitution` flatten it.

### 2.17 `CheckCompatible(Term, Term, Dictionary<string,Term>, HashSet<string>)`

```
private UnifyResult? CheckCompatible(Term existing, Term newTerm, Dictionary<string, Term> subst, HashSet<string> suspSet)
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
```

- Deliberately under-specified per Dart comment ("For now, accept
  other combinations"); preserved verbatim — not a full unification
  (would risk infinite recursion).
- Unused parameters `subst`/`suspSet` kept in signature — reviewer-
  faithful and pre-positioned for a possible future deep-recurse
  extension.

### 2.18 `IsUnderscore(Term)`

```
private static bool IsUnderscore(Term term) =>
    term is UnderscoreTerm
    || (term is VarTerm varTerm && varTerm.Name == "_");
```

- Two-shape detection (load-bearing): parser may emit `_` as EITHER
  `UnderscoreTerm` OR `VarTerm` with `Name == "_"`. Both recognised
  uniformly via short-circuit-OR.
- Expression-bodied member; C# 7+ declaration pattern (`is VarTerm
  varTerm`).

### 2.19 `ResolveSubstitution` / `ResolveTerm` — chain flattening with cycle protection

```
private static Dictionary<string, Term> ResolveSubstitution(Dictionary<string, Term> subst)
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
    return term;   // Const / Underscore unchanged
}
```

- **Cycle detection**: `visited.Contains(varTerm.Name) ⇒ return
  term` — defensive against pathological self-references.
- **Visited-set per-branch copy**: Dart `{...visited}` →
  C# `new HashSet<string>(visited, StringComparer.Ordinal)` (Microsoft
  Learn `HashSet<T>(IEnumerable<T>)` copy-constructor). Necessary at
  struct/list fanout points so sibling branches do not falsely report
  cycles (e.g. `f(X, X)`).
- **In-place mutation along VarTerm chain**: linear chain, no fanout,
  so `visited.Add` in-place is correct.
- **Reader-status preservation**: `varTerm.IsReader` flowing into a
  writer-var resolution returns a NEW reader-var (`new VarTerm(rv.
  Name, true, …)`) — load-bearing for SRSW downstream.

### 2.20 `ApplySubstitution` family — Term / Atom / Guard / Goal (with RemoteGoal/SpawnGoal preservation)

```
private static Term ApplySubstitution(Term term, IReadOnlyDictionary<string, Term> subst)
{
    if (term is VarTerm varTerm)
    {
        if (varTerm.Name == "_") return term;
        if (subst.TryGetValue(varTerm.Name, out var replacement))
        {
            if (varTerm.IsReader && replacement is VarTerm rv && !rv.IsReader)
                return new VarTerm(rv.Name, true, rv.Line, rv.Column);
            return ApplySubstitution(replacement, subst);   // transitive-closure
        }
        return term;
    }
    if (term is StructTerm s)
        return new StructTerm(s.Functor, s.Args.Select(a => ApplySubstitution(a, subst)).ToList(), s.Line, s.Column);
    if (term is ListTerm l)
    {
        if (l.IsNil) return l;
        return new ListTerm(
            l.Head is not null ? ApplySubstitution(l.Head, subst) : null,
            l.Tail is not null ? ApplySubstitution(l.Tail, subst) : null,
            l.Line, l.Column);
    }
    if (term is UnderscoreTerm) return term;
    return term;   // ConstTerm passthrough
}

private static Atom ApplySubstitutionToAtom(Atom atom, IReadOnlyDictionary<string, Term> subst) =>
    new Atom(atom.Functor, atom.Args.Select(a => ApplySubstitution(a, subst)).ToList(), atom.Line, atom.Column);

private static Guard ApplySubstitutionToGuard(Guard guard, IReadOnlyDictionary<string, Term> subst) =>
    new Guard(guard.Predicate, guard.Args.Select(a => ApplySubstitution(a, subst)).ToList(), guard.Line, guard.Column, negated: guard.Negated);

private static Goal ApplySubstitutionToGoal(Goal goal, IReadOnlyDictionary<string, Term> subst)
{
    if (goal is RemoteGoal rg)
    {
        var newModule = ApplySubstitution(rg.Module, subst);
        var newInner = ApplySubstitutionToGoal(rg.Goal, subst);
        return new RemoteGoal(newModule, newInner, rg.Line, rg.Column);
    }
    if (goal is SpawnGoal sg)
    {
        var newInner = ApplySubstitutionToGoal(sg.InnerGoal, subst);
        return new SpawnGoal(newInner, sg.AgentId, sg.Line, sg.Column);
    }
    return new Goal(goal.Functor, goal.Args.Select(a => ApplySubstitution(a, subst)).ToList(), goal.Line, goal.Column);
}
```

- **Underscore preservation**: `_` is NEVER substituted (each `_` is
  unique).
- **Reader-status preservation** on VarTerm-to-writer substitution.
- **RemoteGoal / SpawnGoal wrapper preservation (load-bearing for
  distributed-GLP semantics)**: a plain `Goal` constructor would lose
  the wrapper — dispatch on subtype reconstructs with substituted
  inner components.
- **Transitive-closure recursion**: `ApplySubstitution(replacement,
  subst)` re-substitutes in case `replacement` is itself a VarTerm.
  Safe because `ResolveSubstitution` runs FIRST and flattens chains /
  aborts cycles — by the time `ApplySubstitution` is called, the
  substitution is acyclic.

### 2.21 `SimplifyGuards` / `IsRedundantGuard` / `GetConcreteArg` / `IsGround` — post-spec redundancy table

```
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
        "tuple" or "compound" => concreteArg is StructTerm,
        "list"  or "is_list"  => concreteArg is ListTerm,
        "integer"             => concreteArg is ConstTerm ic && ic.Value is int or long,
        "number"              => concreteArg is ConstTerm nc && (nc.Value is int or long or double or float),
        "atom"                => concreteArg is ConstTerm ac && ac.Value is string,
        "ground" or "no_readers" => IsGround(concreteArg),
        _ => false
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
    if (term is VarTerm) return false;
    if (term is UnderscoreTerm) return true;
    if (term is ConstTerm) return true;
    if (term is StructTerm s) return s.Args.All(IsGround);
    if (term is ListTerm l)
    {
        if (l.IsNil) return true;
        var headG = l.Head is null || IsGround(l.Head);
        var tailG = l.Tail is null || IsGround(l.Tail);
        return headG && tailG;
    }
    return false;
}
```

- C# switch-expression with `or` patterns (C# 9+).
- **Width mapping**: Dart `int` → C# `long` per the recurring 64-bit
  idiom; `integer`-guard must accept BOTH `int` AND `long`; `number`
  accepts `int`/`long`/`double`/`float`.
- `atom` → `string` (GLP atom IS a lower-case identifier string).
- `ground` ≡ `no_readers` — same implementation; preserved per the
  Dart-side comment "a concrete term has no variables (hence no
  readers)".
- Single-arg restriction preserved verbatim.

### 2.22 Cross-cutting policies

- **Naming**: Dart `_camelCase` private members → C# `PascalCase`
  private members (drop leading underscore, capitalise) per the
  cached `rf-dart-leading-underscore-privacy-to-csharp-private`
  idiom (error.dart). Public Dart members already PascalCase or
  camelCase → C# PascalCase.
- **Nullability**: `?` suffix throughout under
  `<Nullable>enable</Nullable>` (`rf-dart-nullsafety-to-csharp-nrt`).
- **Exception hierarchy**: `CompileError` derives from
  `System.Exception` (cached `rf-dart-implements-exception-to-csharp-
  derive-system-exception`, error.dart); the `phase:` argument is a
  named optional parameter (cached `rf-dart-named-default-param-to-
  csharp-optional-arg`).
- **Multi-line error messages**: Dart `\n` literal → C# `\n` literal
  (Microsoft Learn string-literal escape sequences); explicit string
  concatenation across `+` to keep the interpolated portions readable
  (alternative: verbatim `@"..."` is wrong here because we want `\n`
  interpretation).

## 3. Decomposed Task Units

- T1 — emit namespace `Glp.Runtime.Compiler` declaration + `using static
  Glp.Runtime.Analysis.TypeChecker.Prelude;`.
- T2 — emit `internal static class PreludeUnitClauses` with the two
  static fields, `SetPreludeUnitClauseSource`, and
  `GetPreludeUnitClauses`.
- T3 — emit `public class PartialEvaluator` skeleton with `private
  long _varCounter = 0;`.
- T4 — emit lifted `private static bool IsUnitClauseShape(Clause)`.
- T5 — emit `public Program TransformDefinedGuards(Program)`.
- T6 — emit `public Program UnfoldReduceCalls(Program)`.
- T7 — emit `private static HashSet<string> CollectAllProcedures(Program)`.
- T8 — emit `private static Dictionary<string, IReadOnlyList<Term>>
  CollectUnitClauses(Program)` and `private static List<Clause>
  CollectReduceFacts(Program)`.
- T9 — emit `private Clause TransformClause(Clause,
  IReadOnlyDictionary<string, IReadOnlyList<Term>>,
  IReadOnlySet<string>)` with the fixpoint loop and three throw-arms.
- T10 — emit `private List<Clause> UnfoldReduceInClause(Clause,
  IReadOnlyList<Clause>)` with FIRST-reduce-only + two-step unify.
- T11 — emit `private Clause RenameClauseVars(Clause)` and
  `private List<Term> RenameUnitClauseVars(IReadOnlyList<Term>)`.
- T12 — emit `private static void CollectVarNames(Term, HashSet<string>)`
  and `private static void CollectVarNamesFromAtom(Atom, HashSet<string>)`.
- T13 — emit `private static Term ApplyRenaming(Term,
  IReadOnlyDictionary<string,string>)` and
  `private static Atom ApplyRenamingToAtom(Atom,
  IReadOnlyDictionary<string,string>)` with underscore demotion.
- T14 — emit `private UnifyResult GlpUnifyForPE(IReadOnlyList<Term>,
  IReadOnlyList<Term>)` with Phase-1-collect / Phase-2-resolve.
- T15 — emit `private UnifyResult? UnifyTerms(Term, Term,
  Dictionary<string,Term>, HashSet<string>)` six-arm cascade.
- T16 — emit `private static void SubstSet(Dictionary<string,Term>,
  string, Term)` alias-chain-propagation helper.
- T17 — emit `private UnifyResult? CheckCompatible(Term, Term,
  Dictionary<string,Term>, HashSet<string>)` loose-compat check.
- T18 — emit `private static bool IsUnderscore(Term)` two-shape
  detector.
- T19 — emit `private static Dictionary<string,Term>
  ResolveSubstitution(Dictionary<string,Term>)` and `private static
  Term ResolveTerm(Term, IReadOnlyDictionary<string,Term>,
  HashSet<string>)` chain-flattening pair with cycle protection +
  reader preservation.
- T20 — emit `private static Term ApplySubstitution(Term,
  IReadOnlyDictionary<string,Term>)` + `…ToAtom` + `…ToGuard` +
  `…ToGoal` (Goal variant dispatches on RemoteGoal/SpawnGoal).
- T21 — emit `private static List<Guard>? SimplifyGuards(List<Guard>?,
  Atom)` + `IsRedundantGuard` + `GetConcreteArg` + `IsGround`
  post-specialisation redundancy table.
- T22 — wire `BuiltinProcedures` reference (via `using static`) at T9
  call site.

## 4. Research Findings

None required — every construct maps to a research finding ALREADY
cached from sibling specs (per FR-024 cache discipline). The convspec
section "Cross-spec consistency" enumerates all sixteen cached
findings reused here:

- rf-dart-relative-import-to-csharp-using-or-same-namespace
- rf-dart-leading-underscore-privacy-to-csharp-private
- rf-dart-nullsafety-to-csharp-nrt
- rf-dart-map-to-csharp-dictionary
- rf-dart-abstract-marker-base-to-csharp-abstract-sealed-leaves
- rf-dart-implements-exception-to-csharp-derive-system-exception
- rf-dart-named-default-param-to-csharp-optional-arg
- rf-dart-iterable-where-to-linq
- rf-dart-runtime-type-test-polymorphic-value-to-csharp-switch-expression-when-and-invariant-culture
- rf-dart-tostring-interp-to-csharp-tostring-interp
- rf-dart-is-chain-to-csharp-switch-expression-type-pattern
- rf-dart-string-interpolation-join-to-csharp-interpolation-string-join
- rf-dart-map-lookup-to-csharp-trygetvalue
- rf-dart-list-to-csharp-list-of-T
- csharp-static-class-no-toplevel-members
- rf-dart-set-to-csharp-hashset

All sixteen are authoritative (Dart official docs / Microsoft Learn)
per the FR-024 contract. No WebSearch/WebFetch/Agent was invoked; no
new research was needed for this file.

## 5. Consistency Pass

All target decisions in §2 are derivable VERBATIM from the convspec
(file `.codeconv/conversion-specs/lib/compiler/partial_evaluator.dart.md`,
sha matches source). Every construct decision is grounded in:

- the convspec construct block's `target_decision` + `nuance` (mirrored
  one-for-one in §2.1 through §2.21);
- the convspec `conversion_units` enumeration (T1–T22 in §3 cover every
  listed unit);
- the cached cross-spec research findings enumerated in §4 (FR-024).

Per-section provenance:

- §2.1 imports — convspec construct `dart.module.relative_imports_plus_show_filter_for_prelude_builtins`.
- §2.2 PreludeUnitClauses static class — convspec constructs
  `dart.toplevel.mutable_global_nullable_string_init_to_null_and_setter_function`
  + `dart.toplevel.mutable_nullable_cached_map_with_lazy_parse_getter`.
- §2.3 PartialEvaluator skeleton — convspec construct
  `dart.classfield.int_counter_for_fresh_variable_names_with_prefix_PE`.
- §2.4 IsUnitClauseShape — convspec construct
  `dart.procedure.unit_clause_extractor_filter_predicate_with_nested_body_shape_test`.
- §2.5 TransformDefinedGuards — convspec construct
  `dart.method.transform_program_via_per_procedure_per_clause_loop_returning_new_immutable_program`.
- §2.6 UnfoldReduceCalls — convspec construct
  `dart.method.stage2_unfold_reduce_facts_with_short_circuit_early_return_when_no_facts`.
- §2.7 CollectAllProcedures — convspec construct
  `dart.method.collect_signature_set_from_procedures`.
- §2.8 CollectUnitClauses / CollectReduceFacts — convspec construct
  `dart.method.collect_reduce_facts_unit_clauses_filter` plus the
  unit-clause filter construct.
- §2.9 TransformClause — convspec construct
  `dart.method.transform_clause_fixpoint_loop_with_three_unify_arms_throwing_on_fail_or_suspend`.
- §2.10 UnfoldReduceInClause — convspec construct
  `dart.method.unfold_single_reduce_call_per_clause_three_valued_unify_dispatch`.
- §2.11 RenameClauseVars / RenameUnitClauseVars — convspec construct
  `dart.method.rename_clause_variables_fresh_with_underscore_preservation`.
- §2.12 CollectVarNames / CollectVarNamesFromAtom — convspec construct
  `dart.method.var_name_collector_recursive_descent_term_walk`.
- §2.13 ApplyRenaming / ApplyRenamingToAtom — convspec construct
  `dart.method.apply_renaming_recursive_term_rebuild_with_underscore_demotion`.
- §2.14 GlpUnifyForPE — convspec construct
  `dart.method.glp_compile_time_three_valued_unification_phase1_collection_phase2_resolution`.
- §2.15 UnifyTerms — convspec construct
  `dart.method.unify_terms_recursive_six_arm_branching_writer_reader_const_struct_list_underscore`.
- §2.16 SubstSet — convspec construct
  `dart.method.substset_helper_propagating_through_alias_chain`.
- §2.17 CheckCompatible — convspec construct
  `dart.method.check_compatible_structural_compatibility_const_struct_loose_default_accept`.
- §2.18 IsUnderscore — convspec construct
  `dart.method.is_underscore_test_unioning_two_dart_runtime_types`.
- §2.19 ResolveSubstitution / ResolveTerm — convspec construct
  `dart.method.resolve_substitution_flatten_chains_with_cycle_protection`.
- §2.20 ApplySubstitution family — convspec construct
  `dart.method.apply_substitution_to_term_atom_guard_goal_with_remoteGoal_spawnGoal_preservation`.
- §2.21 SimplifyGuards family — convspec construct
  `dart.method.simplify_guards_remove_redundant_with_concrete_arg_type_table_dispatch`.

Fixed — derived from `.codeconv/conversion-specs/lib/compiler/partial_evaluator.dart.md`
(escalation-cleared convspec, 0 escalations) and source file at the
matching sha `8ac90433fa30c517b59e6f21f1860214b4493128880376e072467c37f92385ab`.

## 6. Escalations

None.
