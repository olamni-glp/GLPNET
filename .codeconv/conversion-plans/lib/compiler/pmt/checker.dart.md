---
path: lib/compiler/pmt/checker.dart
cycle_group_id: 57
scc_siblings: []
generated_at: 2026-05-21T16:13:44Z
source_sha256: 2cdf947748a1e9b0f92210357cda90b7f453ebb6b9111c75db0445a7ade131ef
schema_version: 1
---

# Conversion Plan: lib/compiler/pmt/checker.dart

## 1. Source Analysis

Direct inspection of `glp_runtime_net/lib/compiler/pmt/checker.dart` (199 LOC, sha256 `2cdf94…131ef`):

- **File header doc-comment** (lines 1-9): describes the PMT SRSW checker — verifies Single-Reader/Single-Writer constraint with rules: (a) each variable has exactly 1 writer occurrence, (b) at least 1 reader occurrence, (c) multiple readers allowed only if variable is grounded by a guard. Supports multiple mode alternatives (union of mode declarations) — clause is valid if it satisfies SRSW for at least one declared mode.
- **Imports** (lines 11-14): four relative imports — `../ast.dart`, `errors.dart`, `mode_table.dart`, `occurrence.dart`.
- **Class `PmtChecker`** (lines 16-199): single public class, non-sealed, no `==` / `hashCode` overrides, no inheritance. Two `final` fields:
  - `final ModeTable modeTable` (line 17) — injected dependency, no underscore prefix → library-public access surface.
  - `final OccurrenceClassifier classifier` (line 18) — owned helper, no underscore prefix.
- **Constructor** (line 20): `PmtChecker(this.modeTable) : classifier = OccurrenceClassifier(modeTable);` — positional, initialising-formal for `modeTable`, initialiser-list constructs the owned `classifier` from the parameter.
- **Method `checkProcedure(Procedure proc) -> List<PmtError>`** (lines 23-37): allocates fresh `<PmtError>[]`; calls `modeTable.getAllModes(proc.name, proc.arity)`; null-or-empty short-circuit `allModes == null || allModes.isEmpty` returns the (empty) accumulator silently; `for (final clause in proc.clauses)` calls `checkClauseAgainstModes(clause, allModes)` and `errors.addAll(...)` per iteration.
- **Method `checkClauseAgainstModes(Clause, List<List<Mode>>) -> List<PmtError>`** (lines 42-80): single-mode fast-path (`allModes.length == 1` → `return checkClause(clause, allModes.first)`); multi-mode loop tracks `List<PmtError>? bestErrors` via "fewest errors" comparison (`bestErrors == null || errors.length < bestErrors.length`); early-success `return []` on first mode that yields empty errors; composite diagnostic on no-match builds `modeStrings` via `allModes.map((m) => '(${m.map((mode) => mode == Mode.reader ? '?' : '').join(', ')})').join(' | ')` and returns `[PmtError('Clause does not match any declared mode. Available modes: $modeStrings', clause.line, clause.column), ...bestErrors]` (spread-in-list-literal); `return bestErrors ?? []` fallthrough.
- **Method `checkClause(Clause, List<Mode>) -> List<PmtError>`** (lines 83-143): allocates fresh accumulator; calls `classifier.classifyClause(clause, modes)`; groups via `groupByVariable(occurrences)`; extracts `_extractGroundedVars(clause)`; iterates `byVar.entries`; for each `(varName, occs)`: gets `counts = countOccurrences(occs)`; writer-count branches (==0 → "no writer occurrence" at `occs.first.line/column`, >1 → "N writer occurrences (expected 1)" at the SECOND writer via `occs.where((o) => o.type == OccurrenceType.writer).skip(1).first`); reader-count branches (==0 → "no reader occurrence" at `occs.first`, >1 AND NOT grounded → "N reader occurrences; add ground(varName) guard" at the SECOND reader via `.where(...).skip(1).first`).
- **Private method `_extractGroundedVars(Clause) -> Set<String>`** (lines 154-183): fresh `<String>{}`; early `return grounded` if `clause.guards == null`; declares two method-local `const Set<String>` literals — `typeCheckOps` (13 keys: `ground, number, integer, float, atom, string, list, tuple, compound, var, nonvar, is_mutual_ref, unknown`) and `comparisonOps` (7 keys: `<, >, =<, >=, =:=, r'=\='`, `=?=`) — note `r'=\='` is a Dart raw string (the literal three chars `=\=`); two SEQUENTIAL (not `else-if`) arity-checked membership branches: arity-1 type-check → recurse on `guard.args[0]`; arity-2 comparison → recurse on `guard.args[0]` AND `guard.args[1]`; returns accumulator.
- **Private method `_collectVarNames(Term, Set<String>) -> void`** (lines 186-198): `is`-test chain over `VarTerm` (adds `term.name`), `StructTerm` (foreach `args` recurses), `ListTerm` (null-check `term.head` and `term.tail`, recurse on each with bang); trailing comment `// ConstTerm, UnderscoreTerm — no variables` documents the chain exhaustiveness. NO underscore-prefix skip on VarTerm (intentional divergence from `occurrence.dart._collectVariables`).
- **Concurrency / async surface**: NONE — entirely synchronous, no `async` / `Future` / `Stream` / `Isolate` / `late`.

Six non-trivial constructs identified, mirroring the convspec construct list exactly.

## 2. Dart → C#/.NET Conversion Plan

Each construct below mirrors the ratified convspec verbatim. Cross-file invariants relied upon: `Mode` (PascalCased enum with `Mode.Reader`), `ModeTable.GetAllModes(string, int) -> IReadOnlyList<IReadOnlyList<Mode>>?` / `List<List<Mode>>?`, `OccurrenceClassifier.ClassifyClause(Clause, IReadOnlyList<Mode>) -> List<Occurrence>` with public get-only `ModeTable`, `OccurrenceExtras.GroupByVariable(IReadOnlyList<Occurrence>) -> Dictionary<string, List<Occurrence>>` (Ordinal) and `OccurrenceExtras.CountOccurrences(IReadOnlyList<Occurrence>) -> (int Writers, int Readers)`, `PmtError` sealed value class with positional ctor, AST types (`Procedure`, `Clause`, `Goal`, `Term`, `VarTerm`, `StructTerm`, `ListTerm`, `ConstTerm`, `UnderscoreTerm`).

### C1 — Coordinator class with final injected dep + owned helper initialised in initialiser-list

**Source form** (Dart):
```dart
class PmtChecker {
  final ModeTable modeTable;
  final OccurrenceClassifier classifier;
  PmtChecker(this.modeTable) : classifier = OccurrenceClassifier(modeTable);
}
```

**Target decision** (C#): emit a non-sealed reference-type `class PmtChecker` with TWO get-only auto-properties initialised from the primary positional constructor — `public ModeTable ModeTable { get; }` (the injected dependency) and `public OccurrenceClassifier Classifier { get; }` (the owned helper). Constructor signature: `public PmtChecker(ModeTable modeTable) { ModeTable = modeTable; Classifier = new OccurrenceClassifier(modeTable); }`. The Dart initialiser-list `: classifier = OccurrenceClassifier(modeTable)` is the "field B computed from constructor parameter A" pattern; the documented C# equivalent is two assignments inside the constructor body in source order. NO `IEquatable<PmtChecker>` (Dart source has no `==` / `hashCode` overrides → reference equality preserved). NOT `sealed` (no subclassing precedent locked; downstream test fakes may subtype). Both helper-data fields are exposed as `public` get-only properties (NOT `private readonly`) because the Dart source declares them without an underscore prefix — signalling library-public access surface; this aligns with `occurrence.dart.md`'s `OccurrenceClassifier.ModeTable` get-only public property precedent.

**Research finding**: `rf-dart-initialiser-list-owned-helper-to-csharp-ctor-body-assignment` (NEW per convspec).

**Nuance**: (a) owned-vs-injected distinction preserved — `new OccurrenceClassifier(modeTable)` MUST live in the constructor body (a C# field initialiser cannot see constructor parameters per Microsoft Learn "Constructors"); (b) reference-aliasing preserved — `modeTable` parameter is passed into `new OccurrenceClassifier(modeTable)` so both `ModeTable` and `Classifier.ModeTable` alias the caller's instance, no defensive copy; (c) initialiser-list source-order preserved — body assigns `ModeTable` FIRST, then `Classifier` SECOND, mirroring Dart's left-to-right initialiser-list evaluation.

### C2 — List accumulator with null-or-empty short-circuit then AddAll-per-clause

**Source form** (Dart):
```dart
List<PmtError> checkProcedure(Procedure proc) {
  final errors = <PmtError>[];
  final allModes = modeTable.getAllModes(proc.name, proc.arity);
  if (allModes == null || allModes.isEmpty) {
    return errors;
  }
  for (final clause in proc.clauses) {
    errors.addAll(checkClauseAgainstModes(clause, allModes));
  }
  return errors;
}
```

**Target decision** (C#):
```
public List<PmtError> CheckProcedure(Procedure proc) {
    var errors = new List<PmtError>();
    var allModes = ModeTable.GetAllModes(proc.Name, proc.Arity);
    if (allModes is null || allModes.Count == 0) return errors;
    foreach (var clause in proc.Clauses) {
        errors.AddRange(CheckClauseAgainstModes(clause, allModes));
    }
    return errors;
}
```

Dart `<PmtError>[]` → C# `new List<PmtError>()`; Dart `errors.addAll(other)` → C# `errors.AddRange(other)`; Dart `for (final x in xs)` → C# `foreach (var x in xs)`. The compound short-circuit `allModes == null || allModes.isEmpty` becomes `allModes is null || allModes.Count == 0` — `is null` FIRST for short-circuit safety, `.Count == 0` mirrors Dart `isEmpty` per cached idiom.

**Research finding**: `rf-dart-list-typed-literal-and-addall-to-csharp-list-and-addrange` (CACHED — type_checker.dart) composed with `rf-dart-length-isempty-to-csharp-count` (CACHED — mode_table.dart).

**Nuance**: the silent-skip semantics on `allModes is null || allModes.Count == 0` is mandated by the source doc-comment "No mode declaration — skip checking" — codegen MUST NOT promote to an error. Short-circuit evaluation order matters: `is null` MUST be tested FIRST (Microsoft Learn "Boolean logical operators" — `||` evaluates RHS only if necessary); reordering would throw `NullReferenceException` on a null `allModes`. Fresh-empty-list allocator inside the early-return arm preserves Dart's "each invocation returns a fresh list" semantics — codegen MUST NOT hoist to a shared static empty.

### C3 — Try each alternative until success with best-error tracking and composite diagnostic

**Source form** (Dart): `checkClauseAgainstModes` (lines 42-80, see §1).

**Target decision** (C#):
```
public List<PmtError> CheckClauseAgainstModes(Clause clause, IReadOnlyList<IReadOnlyList<Mode>> allModes) {
    if (allModes.Count == 1) return CheckClause(clause, allModes[0]);
    List<PmtError>? bestErrors = null;
    foreach (var modes in allModes) {
        var errors = CheckClause(clause, modes);
        if (errors.Count == 0) return new List<PmtError>();
        if (bestErrors is null || errors.Count < bestErrors.Count) bestErrors = errors;
    }
    if (bestErrors is not null && bestErrors.Count > 0) {
        var modeStrings = string.Join(" | ",
            allModes.Select(m => $"({string.Join(", ", m.Select(mode => mode == Mode.Reader ? "?" : ""))})"));
        var result = new List<PmtError> {
            new PmtError($"Clause does not match any declared mode. Available modes: {modeStrings}", clause.Line, clause.Column)
        };
        result.AddRange(bestErrors);
        return result;
    }
    return bestErrors ?? new List<PmtError>();
}
```

Dart `allModes.first` → C# `allModes[0]`; Dart `.length` / `.isEmpty` / `.isNotEmpty` → C# `.Count == 0` / `.Count > 0` / `.Count < x`; Dart `Iterable.map(...).join(sep)` → C# LINQ `xs.Select(...)` composed with `string.Join(sep, xs)` (separator FIRST); Dart spread-in-list-literal `[head, ...bestErrors]` decomposed to `new List<PmtError> { head }` initialiser followed by `AddRange(bestErrors)`. ALTERNATIVE C# 12 collection expression `[head, .. bestErrors]` REJECTED — target-typed result depends on context; explicit `new List + AddRange` guarantees `List<PmtError>` regardless. Dart `??` → C# `??` (verbatim). `Mode.reader` → `Mode.Reader` (cross-file invariant from mode_table.dart.md).

**Research finding**: `rf-dart-spread-in-list-literal-to-csharp-list-initializer-plus-addrange` (NEW per convspec).

**Nuance**: (a) single-mode fast-path preserved verbatim — promoting it to "always use the loop" would change error messages (the composite "Clause does not match any declared mode" prefix would never apply for single-mode case but is observable in goldens); (b) tracking-minimum idiom uses strict `<` (NOT `<=`) so ties go to the FIRST encountered mode — preserves Dart's left-to-right scan order; (c) spread-in-list-literal `[head, ...tail]` allocates fresh and copies; the explicit `new List + AddRange` shape preserves head-FIRST insertion order verbatim; (d) `return []` early-success → `new List<PmtError>()` (NOT `Array.Empty<>`) — source-faithful rendering, consistency with `CheckProcedure`'s fresh-empty.

### C4 — SRSW check loop: classify → group → extract grounded → writer/reader count with secondary lookup

**Source form** (Dart): `checkClause` (lines 83-143, see §1).

**Target decision** (C#):
```
public List<PmtError> CheckClause(Clause clause, IReadOnlyList<Mode> modes) {
    var errors = new List<PmtError>();
    var occurrences = Classifier.ClassifyClause(clause, modes);
    var byVar = OccurrenceExtras.GroupByVariable(occurrences);
    var groundedVars = _ExtractGroundedVars(clause);
    foreach (var entry in byVar) {
        var varName = entry.Key;
        var occs = entry.Value;
        var counts = OccurrenceExtras.CountOccurrences(occs);
        if (counts.Writers == 0) {
            errors.Add(new PmtError($"Variable {varName} has no writer occurrence", occs[0].Line, occs[0].Column));
        } else if (counts.Writers > 1) {
            var secondWriter = occs.Where(o => o.Type == OccurrenceType.Writer).Skip(1).First();
            errors.Add(new PmtError($"Variable {varName} has {counts.Writers} writer occurrences (expected 1)", secondWriter.Line, secondWriter.Column));
        }
        if (counts.Readers == 0) {
            errors.Add(new PmtError($"Variable {varName} has no reader occurrence", occs[0].Line, occs[0].Column));
        } else if (counts.Readers > 1 && !groundedVars.Contains(varName)) {
            var secondReader = occs.Where(o => o.Type == OccurrenceType.Reader).Skip(1).First();
            errors.Add(new PmtError($"Variable {varName} has {counts.Readers} reader occurrences; add ground({varName}) guard", secondReader.Line, secondReader.Column));
        }
    }
    return errors;
}
```

`Classifier.ClassifyClause` is the owned helper from C1; `OccurrenceExtras.GroupByVariable` / `OccurrenceExtras.CountOccurrences` host top-level Dart functions per occurrence.dart.md (cross-file invariant). Dart `.where(p).skip(1).first` → C# LINQ `xs.Where(p).Skip(1).First()`. Dart `Map.entries` → C# `foreach (var entry in byVar)` directly iterates `Dictionary<K,V>` yielding `KeyValuePair<K,V>` with `.Key` / `.Value`. Counts come from `OccurrenceExtras.CountOccurrences` named tuple `(int Writers, int Readers)` — accessed PascalCased. `occs.first.line` → `occs[0].Line` because `occs` is the `List<Occurrence>` bucket.

**Research finding**: `rf-dart-iterable-where-skip-first-to-csharp-linq-where-skip-first` (NEW per convspec).

**Nuance**: (a) iterable-chain laziness preserved exactly — Dart `where`/`skip` are lazy, C# LINQ `Where`/`Skip` are lazy (deferred execution per Microsoft Learn); terminal `.First()` triggers evaluation; (b) "second-of-kind" pattern retrieves the SECOND matching element (skipping the FIRST) — codegen MUST NOT rewrite to `.First()` or `.Last()`; (c) exception parity — both Dart `Iterable.first` and C# `Enumerable.First()` throw on empty; source guarantees non-emptiness via surrounding `counts.Writers > 1` / `counts.Readers > 1` check, so codegen MUST NOT introduce defensive `FirstOrDefault` (would mask bugs in `OccurrenceClassifier`). The `ground({varName})` literal call-syntax inside the reader-too-many message is preserved verbatim — it's observable in user-facing error messages and downstream test goldens.

### C5 — Guard scan with const-string-set membership and arity check then var-name recursive collect

**Source form** (Dart): `_extractGroundedVars` (lines 154-183, see §1).

**Target decision** (C#):
```
private static readonly FrozenSet<string> TypeCheckOps = new[] {
    "ground", "number", "integer", "float", "atom", "string",
    "list", "tuple", "compound", "var", "nonvar",
    "is_mutual_ref", "unknown",
}.ToFrozenSet(StringComparer.Ordinal);

private static readonly FrozenSet<string> ComparisonOps = new[] {
    "<", ">", "=<", ">=", "=:=", "=\\=", "=?=",
}.ToFrozenSet(StringComparer.Ordinal);

private HashSet<string> _ExtractGroundedVars(Clause clause) {
    var grounded = new HashSet<string>(StringComparer.Ordinal);
    if (clause.Guards is null) return grounded;
    foreach (var guard in clause.Guards) {
        if (TypeCheckOps.Contains(guard.Predicate) && guard.Args.Count == 1) {
            _CollectVarNames(guard.Args[0], grounded);
        }
        if (ComparisonOps.Contains(guard.Predicate) && guard.Args.Count == 2) {
            _CollectVarNames(guard.Args[0], grounded);
            _CollectVarNames(guard.Args[1], grounded);
        }
    }
    return grounded;
}
```

Two `const Set<String>` Dart compile-time constants become two `private static readonly FrozenSet<string>` fields (per cached idiom). Each constructed once via `.ToFrozenSet(StringComparer.Ordinal)`. Dart raw string `r'=\='` (literal three chars `=\=`) → C# escaped-backslash literal `"=\\="`. Empty `<String>{}` accumulator → `new HashSet<string>(StringComparer.Ordinal)` (MUTABLE — not `FrozenSet`, mutated via `.Add`).

**Research finding**: `rf-dart-const-set-to-csharp-frozenset-ordinal` (CACHED — glp_printer.dart / parser.dart / type_ast.dart / program_dfa.dart); `rf-dart-set-literal-to-csharp-hashset` (CACHED — boot_loader.dart / param_expansion.dart).

**Nuance**: (a) `const` Dart set → `static readonly` C# field initialised lazily at type initialisation — no compile-time-constant set type in .NET, the FrozenSet pattern is the canonical rendering; (b) `StringComparer.Ordinal` is MANDATORY by project-wide discipline (mode_table.dart.md, well_typed_term.dart.md) — explicit for reviewer-parity even though `HashSet<string>` default is ordinal; (c) raw-string nuance — Dart `r'=\='` and C# `"=\\="` both produce the in-memory three-character string `=\=`; codegen MUST NOT substitute `"=\="` (would either fail to compile or encode a different sequence); (d) `clause.guards == null` followed by `clause.guards!` dereference → C# flow-narrowed `clause.Guards is null` then bare `clause.Guards` (no `!`); (e) two `if` branches NOT `else-if`-chained — sets are disjoint by manual verification, sequential-if shape preserved verbatim for minimal source-diff. Fields are `private` (implementation detail of `_ExtractGroundedVars` only). Naming: Dart local-`const` `typeCheckOps` → C# static field `TypeCheckOps` PascalCased; `comparisonOps` → `ComparisonOps`. Field ordering: `TypeCheckOps` FIRST, `ComparisonOps` SECOND.

### C6 — Recursive visitor: collect var names into string-set accumulator with Term subtype dispatch and ListTerm head/tail null check

**Source form** (Dart): `_collectVarNames` (lines 186-198, see §1).

**Target decision** (C#):
```
private void _CollectVarNames(Term term, HashSet<string> @out) {
    if (term is VarTerm varTerm) {
        @out.Add(varTerm.Name);
    } else if (term is StructTerm structTerm) {
        foreach (var arg in structTerm.Args) {
            _CollectVarNames(arg, @out);
        }
    } else if (term is ListTerm listTerm) {
        if (listTerm.Head is not null) _CollectVarNames(listTerm.Head, @out);
        if (listTerm.Tail is not null) _CollectVarNames(listTerm.Tail, @out);
    }
    // ConstTerm, UnderscoreTerm — no variables
}
```

Structurally identical to `occurrence.dart`'s `_collectVariables`. Dart `is` type-test chain → C# declaration-pattern matching with typed locals (`varTerm`, `structTerm`, `listTerm`). Dart `term.head!` / `term.tail!` bang force-unwraps drop because C# flow-narrowing applies inside the null-check branch. Parameter identifier `out` → `@out` (C# reserved keyword; verbatim-identifier prefix preserves source surface). The trailing comment `// ConstTerm, UnderscoreTerm — no variables` MUST be preserved verbatim in the C# port — documents exhaustiveness for reviewer comprehension per FR-023.

**Research finding**: `rf-dart-is-type-test-chain-to-csharp-pattern-switch` (CACHED — occurrence.dart); `rf-dart-nullable-bang-inside-null-check-to-csharp-flow-narrowed-access` (CACHED — type_checker.dart / occurrence.dart).

**Nuance**: (a) `is`-test chain with smart-cast — Dart narrows the static type of `term` inside each branch; C# requires the declaration-pattern-with-binding (`term is VarTerm varTerm`) for the narrowed access to `.Name`, `.Args`, `.Head`, `.Tail`; (b) null-bang elision via flow-narrowing — C# nullable-flow analysis recognises `if (listTerm.Head is not null) ...` and narrows `listTerm.Head` to non-null, the bang disappears; (c) accumulator-by-reference — `Set<String>` / `HashSet<string>` are reference types; mutations to set state visible to caller, no `ref` keyword, no defensive copy; terminal `.Add` is idempotent on duplicates, return value ignored same as Dart `Set.add`; (d) INTENTIONAL divergence from `occurrence.dart._collectVariables` — that one skips `_`-prefixed VarTerm names (anonymous-variable exemption per typed-glp-manual §9 for SRSW counting); THIS `_collectVarNames` does NOT skip — collects EVERY variable name including underscore-prefixed, because guards bind every named variable they reference. Codegen MUST preserve the difference verbatim.

## 3. Decomposed Task Units

- T1: emit class declaration `class PmtChecker` (non-sealed, no IEquatable) with two public get-only auto-properties `ModeTable` and `Classifier` and positional constructor with source-ordered body assignments (C1) — done.
- T2: emit `CheckProcedure(Procedure proc) -> List<PmtError>` with fresh-list allocator, null-or-empty short-circuit (`is null` FIRST), and foreach-AddRange dispatch (C2) — done.
- T3: emit `CheckClauseAgainstModes(Clause, IReadOnlyList<IReadOnlyList<Mode>>) -> List<PmtError>` with single-mode fast-path via `[0]`, multi-mode loop with strict `<` minimum-tracking, composite-diagnostic LINQ Select + string.Join (separator FIRST), and explicit `new List + AddRange` rendering of the spread-in-list-literal (C3) — done.
- T4: emit `CheckClause(Clause, IReadOnlyList<Mode>) -> List<PmtError>` with classify+group+grounded extraction, foreach over KeyValuePair, named-tuple counts access, writer/reader 0/>1 branches with `Where().Skip(1).First()` second-of-kind chain, and verbatim error messages including `ground({varName})` call-syntax (C4) — done.
- T5: emit two `private static readonly FrozenSet<string>` fields `TypeCheckOps` (13 keys) and `ComparisonOps` (7 keys, including escaped-backslash `"=\\="`) via `.ToFrozenSet(StringComparer.Ordinal)` (C5 field emission) — done.
- T6: emit `private HashSet<string> _ExtractGroundedVars(Clause clause)` with fresh `HashSet<string>(StringComparer.Ordinal)`, flow-narrowed null-guards, and two sequential-if (NOT else-if) arity-checked membership branches dispatching to `_CollectVarNames` (C5 method emission) — done.
- T7: emit `private void _CollectVarNames(Term term, HashSet<string> @out)` with declaration-pattern `is`-chain over VarTerm/StructTerm/ListTerm, NO underscore-prefix skip on VarTerm, flow-narrowed Head/Tail null-checks (bang elided), and preserved trailing comment `// ConstTerm, UnderscoreTerm — no variables` (C6) — done.
- T8: emit `using` directives required by the port (`System.Collections.Frozen`, `System.Collections.Generic`, `System.Linq`) and project-relative type references for `ModeTable`, `OccurrenceClassifier`, `OccurrenceExtras`, `Occurrence`, `OccurrenceType`, `Mode`, `PmtError`, AST types from `lib/compiler/ast.dart`'s ported namespace — done.
- T9: verify no `async` / `Stream` / `Future` / `Isolate` / `late` surfaces remain (none in source — pure synchronous single-class) — done.
- T10: preserve file-level doc-comment (lines 1-9) as XML doc-comment block atop the class declaration per FR-023 source-surface preservation — done.

## 4. Research Findings

All non-trivial constructs are resolved against rf-* findings already recorded in the ratified convspec (FR-024: never re-research; cached findings reused verbatim). Three findings are NEW (recorded by the convspec author for this file) and several are CACHED from prior corpus files. Full provenance:

- **rf-dart-initialiser-list-owned-helper-to-csharp-ctor-body-assignment** (NEW): grounded in dart.dev "Constructors — Initializer list" (initialiser-list entries execute BEFORE the constructor body in source order; RHS doesn't access `this`) and Microsoft Learn "Constructors" (C# field initialisers cannot reference constructor parameters; body assignment is the canonical rendering). Composes with cached `rf-csharp-class-with-readonly-injected-dep-and-private-helpers` (single-dep variant from occurrence.dart) for the "two fields, one injected, one constructed FROM the injected parameter" shape.
- **rf-dart-list-typed-literal-and-addall-to-csharp-list-and-addrange** (CACHED — type_checker.dart): Dart `<T>[]` → C# `new List<T>()`; Dart `addAll` → C# `AddRange`. Microsoft Learn `List<T>.AddRange`.
- **rf-dart-length-isempty-to-csharp-count** (CACHED — mode_table.dart): Dart `.isEmpty` → C# `.Count == 0`. Microsoft Learn Framework Design Guidelines: `.Count` on `IReadOnlyCollection<T>`, `.Length` reserved for arrays/strings.
- **rf-dart-spread-in-list-literal-to-csharp-list-initializer-plus-addrange** (NEW): grounded in dart.dev "Collections — spread operators" (spread allocates fresh and copies head + tail in source order) and Microsoft Learn "Collection expressions" (C# 12 spread is target-typed). The spec REJECTS the literal one-to-one C# 12 mapping `[head, .. tail]` in favour of the explicit `new List<T> { head }; result.AddRange(tail);` shape for guaranteed `List<T>` return type and reviewer-clarity.
- **rf-dart-iterable-where-skip-first-to-csharp-linq-where-skip-first** (NEW): grounded in Dart core API (`Iterable.where` returns a lazy Iterable; `Iterable.skip` returns an Iterable that provides all but the first count elements; `Iterable.first` throws StateError on empty) and Microsoft Learn `System.Linq.Enumerable` (`Where`, `Skip`, `First` — all deferred-execution; `First()` throws `InvalidOperationException` on empty). Method-name and semantic mapping verbatim.
- **rf-dart-const-set-to-csharp-frozenset-ordinal** (CACHED — glp_printer.dart / parser.dart / type_ast.dart / program_dfa.dart): Dart `const Set<String> = { ... }` → C# `private static readonly FrozenSet<string>` initialised once via `.ToFrozenSet(StringComparer.Ordinal)`. Microsoft Learn `System.Collections.Frozen.FrozenSet`.
- **rf-dart-set-literal-to-csharp-hashset** (CACHED — boot_loader.dart / param_expansion.dart): Dart `<String>{}` → C# `new HashSet<string>(StringComparer.Ordinal)` for the mutable accumulator case.
- **rf-dart-is-type-test-chain-to-csharp-pattern-switch** (CACHED — occurrence.dart): Dart `is`-test chain with smart-cast → C# declaration-pattern matching with bound typed locals. Microsoft Learn "Pattern matching".
- **rf-dart-nullable-bang-inside-null-check-to-csharp-flow-narrowed-access** (CACHED — type_checker.dart / occurrence.dart): Dart `field != null` then `field!` dereference → C# flow-narrowed bare access (no `!`). Microsoft Learn "Nullable reference types — flow analysis".

No additional research required beyond what the convspec already records.

## 5. Consistency Pass

All six constructs and all task units T1–T10 are fixed — derived verbatim from the ratified convspec at `.codeconv/conversion-specs/lib/compiler/pmt/checker.dart.md` (source_sha256 `2cdf94…131ef`, matching the deep-analysis sha256 above), composed with cross-file invariants from `mode_table.dart.md` (`Mode.Reader` PascalCase, `GetAllModes` return type), `occurrence.dart.md` (`OccurrenceClassifier` surface, `OccurrenceExtras.GroupByVariable` / `CountOccurrences` static-host class, `Occurrence` / `OccurrenceType.Reader` / `.Writer` PascalCase), `errors.dart.md` (`PmtError` sealed positional ctor), and `ast.dart`'s ported AST surface. Three NEW rf-* findings and four CACHED rf-* findings (per convspec §"Rationale and research provenance") provide authoritative Microsoft Learn + dart.dev grounding for each non-trivial construct; CLAUDE.md project-wide invariants enforced (StringComparer.Ordinal mandatory, FR-023 source-surface preservation, FR-024 no-re-research). Zero divergence between this plan and the ratified convspec.

## 6. Escalations

None.
