---
path: lib/compiler/pmt/occurrence.dart
cycle_group_id: 56
scc_siblings: []
generated_at: 2026-05-21T15:25:17Z
source_sha256: cb56e5b79b12f401309ef978dd33b1fdb7ccafd1cd7a202e52f2f797905df6d1
schema_version: 1
---

# Conversion Plan: lib/compiler/pmt/occurrence.dart

## 1. Source Analysis

Direct inspection of `glp_runtime_net/lib/compiler/pmt/occurrence.dart` (158 lines, sha256 `cb56e5b79b12f401309ef978dd33b1fdb7ccafd1cd7a202e52f2f797905df6d1`):

- Imports: `../ast.dart` (Atom, Clause, Goal, Guard, Term, VarTerm, StructTerm, ListTerm, RemoteGoal, SpawnGoal — referenced; ConstTerm, UnderscoreTerm — referenced by comment as no-op), `mode_table.dart` (Mode, ModeTable).
- `enum OccurrenceType { writer, reader }` (line 14) — plain two-member enum; declaration order writer FIRST, reader SECOND; consumed via boolean ternary on `term.isReader` (line 118) and via `${type.name}` interpolation in `toString` (line 26). No `switch` over the enum.
- `class Occurrence` (lines 17–38) — four final fields `variable: String`, `type: OccurrenceType`, `line: int`, `column: int`; positional constructor; hand-written `toString` returning `'$variable:${type.name}@$line:$column'`; hand-written `operator ==` comparing all four; hand-written `hashCode` via `Object.hash(variable, type, line, column)`. Reference type with value-equality intent.
- `class OccurrenceClassifier` (lines 41–134) — single final dep `modeTable: ModeTable` injected via constructor; one public method `classifyClause(Clause, List<Mode>) -> List<Occurrence>`; four private helpers `_classifyHead`, `_classifyGoal`, `_classifyGuard`, `_collectVariables`.
  - `classifyClause` (lines 52–73): allocates `<Occurrence>[]`, calls `_classifyHead` then iterates `clause.body!` (gated by `clause.body != null`), then iterates `clause.guards!` (gated by `clause.guards != null`). Order: head → body → guards.
  - `_classifyHead` (lines 76–81): paired for-loop over `head.args` zipped with `headModes` by min-length; calls `_collectVariables`; `headModes` parameter currently unused per doc comment "Collect variables using syntactic annotations (headModes unused for now)".
  - `_classifyGoal` (lines 84–100): early-return on `goal is RemoteGoal`; recursive descent on `goal is SpawnGoal` via `goal.innerGoal`; fall-through `foreach goal.args` calling `_collectVariables`.
  - `_classifyGuard` (lines 103–107): `foreach guard.args` calling `_collectVariables`.
  - `_collectVariables` (lines 110–133): is-chain over `VarTerm` (skip if `term.name.startsWith('_')`, else build `Occurrence` with `term.isReader ? reader : writer` ternary), `StructTerm` (foreach `term.args` recurse), `ListTerm` (recurse on non-null `term.head!` and `term.tail!`). Trailing comment "ConstTerm, UnderscoreTerm — no variables to collect".
- Top-level function `groupByVariable(List<Occurrence>) -> Map<String, List<Occurrence>>` (lines 137–143) — `putIfAbsent(occ.variable, () => []).add(occ)` bucket pattern.
- Top-level function `countOccurrences(List<Occurrence>) -> ({int writers, int readers})` (lines 146–157) — Dart-3 record return type with named fields; writers tested FIRST against `OccurrenceType.writer`, else readers; returns `(writers: writers, readers: readers)` record literal.

No async/Future/Stream, no isolates, no `late`, no inheritance among occurrence helpers, no `switch` over OccurrenceType, no exceptions thrown, no IO.

## 2. Dart → C#/.NET Conversion Plan

Mirrors convspec §constructs (8 constructs) verbatim. The U+2192 arrow is used for source → target mappings.

### Construct 1: `enum OccurrenceType { writer, reader }` → C# `enum OccurrenceType { Writer, Reader }`

- Emit plain C# `enum OccurrenceType { Writer, Reader }` (PascalCase per .NET enumeration naming guidelines; declaration order preserved so `(OccurrenceType)0 == Writer` and `(OccurrenceType)1 == Reader`).
- Place in namespace `<root>.Compiler.Pmt` (mirrors directory).
- No methods, no static aliases, no `name` override — `Enum.ToString()` returns the member-name string (PascalCase: `"Writer"` / `"Reader"`), matching Dart `Enum.name` behaviour with documented case-drift (lowercase → PascalCase) — diagnostic-only `toString` surface; not a serialisation contract.
- Boolean dispatch via ternary in `_CollectVariables` — no `switch`, so enum-exhaustiveness default-arm nuance does NOT arise.

### Construct 2: `class Occurrence` → `sealed class Occurrence : IEquatable<Occurrence>`

- Emit `sealed` reference-type C# class (NOT record, NOT struct).
- Four get-only auto-properties initialised from a single positional constructor:
  - `string Variable` (non-nullable under enabled NRT)
  - `OccurrenceType Type`
  - `int Line`
  - `int Column`
- Implement `IEquatable<Occurrence>` with:
  - `Equals(Occurrence? other)` — comparing the four components
  - `override Equals(object? obj)` — delegating to `Equals(Occurrence?)`
  - `override GetHashCode()` — `HashCode.Combine(Variable, Type, Line, Column)` (order-sensitive structural hash of four values mirroring Dart `Object.hash`)
  - `override ToString()` — `$"{Variable}:{Type}@{Line}:{Column}"` (composite-format `{Type}` invokes `Enum.ToString()` returning member name)
  - `==` and `!=` operator overloads with the documented null-safe pattern (both-null → true, one-null → false, otherwise delegate to `Equals`).
- `sealed` modifier avoids the polymorphic-equality pitfall — no subclasses exist or are planned.
- Record REJECTED to preserve explicit hand-written value-equality intent — uniform with errors.dart's `PmtError` and type_ast.dart precedents.

### Construct 3: `class OccurrenceClassifier` → non-sealed reference-type C# class

- Emit non-sealed C# class `OccurrenceClassifier`.
- One get-only auto-property `public ModeTable ModeTable { get; }` initialised from positional constructor `OccurrenceClassifier(ModeTable modeTable)`. Field shadowing of `ModeTable` (property name shadows type name) is permitted in C# per project precedent (errors.dart `PmtErrors.Errors`, type_table.dart `TypeTable.Definitions`).
- One public method `ClassifyClause(Clause clause, IReadOnlyList<Mode> headModes) -> List<Occurrence>`.
- Four private helpers retaining underscore prefix per source-surface preservation convention (mode_table.dart, type_table.dart): `_ClassifyHead`, `_ClassifyGoal`, `_ClassifyGuard`, `_CollectVariables`.
- `headModes` parameter typed `IReadOnlyList<Mode>` (read-only-view widening; method only iterates).
- `clause.body!` / `clause.guards!` Dart bangs DROP in C# — `if (clause.Body != null) { foreach (var goal in clause.Body) ... }` flow-narrows the C# nullable-reference type for the duration of the branch (no `!` operator needed).
- Result list: `var occurrences = new List<Occurrence>();` (Dart `<Occurrence>[]` → C# empty list).

### Construct 4: `_classifyGoal` → C# pattern-matching dispatch

- Emit `private void _ClassifyGoal(Goal goal, List<Occurrence> @out)`.
- `if (goal is RemoteGoal) { return; }` — type test only (no captured payload; `goal` discarded after test).
- `if (goal is SpawnGoal spawn) { _ClassifyGoal(spawn.InnerGoal, @out); return; }` — type-pattern with identifier; strongly-typed local; no separate cast.
- Fall-through: `foreach (var arg in goal.Args) _CollectVariables(arg, @out);` (PascalCase `Args` per ast.dart cross-file invariant).
- `switch` expression/statement REJECTED — universal fall-through over `goal.args` for every other `Goal` subtype; imperative chain preserves "default-is-the-base-behaviour" verbatim.
- Parameter `out` is a C# reserved keyword (out modifier); rename to `@out` (verbatim identifier prefix per Microsoft Learn "Identifier names") to preserve source surface under FR-023.

### Construct 5: `_collectVariables` → C# pattern-matching visitor

- Emit `private void _CollectVariables(Term term, List<Occurrence> @out)`.
- `if (term is VarTerm varTerm) { ... } else if (term is StructTerm structTerm) { ... } else if (term is ListTerm listTerm) { ... }` — three explicit-cast-free branches preserving Dart's runtime-type dispatch. Pattern-typed locals renamed `varTerm` / `structTerm` / `listTerm` (reviewer-friendly; spec-prescribed).
- VarTerm branch:
  - `if (varTerm.Name.StartsWith("_", StringComparison.Ordinal)) return;` — explicit `StringComparison.Ordinal` matches project-wide discipline (well_typed_term.dart, mode_table.dart) and restores Dart's implicit code-unit ordinal semantics visibly.
  - `var occType = varTerm.IsReader ? OccurrenceType.Reader : OccurrenceType.Writer;`
  - `@out.Add(new Occurrence(varTerm.Name, occType, varTerm.Line, varTerm.Column));`
- StructTerm branch: `foreach (var arg in structTerm.Args) _CollectVariables(arg, @out);`
- ListTerm branch: two nullable-flow-narrowed branches — `if (listTerm.Head != null) _CollectVariables(listTerm.Head, @out);` and `if (listTerm.Tail != null) _CollectVariables(listTerm.Tail, @out);` — no `!` operator needed; C# flow analysis narrows `Head`/`Tail` to non-null inside each branch.
- Trailing comment preserved: `// ConstTerm, UnderscoreTerm — no variables to collect`.
- `switch` REJECTED — meaningful no-op default for two specific Term subtypes documented by source comment; imperative chain preserves textual shape.

### Construct 6: `classifyClause` → public orchestrator method

- Emit `public List<Occurrence> ClassifyClause(Clause clause, IReadOnlyList<Mode> headModes)`.
- Body:
  - `var occurrences = new List<Occurrence>();`
  - `_ClassifyHead(clause.Head, headModes, occurrences);`
  - `if (clause.Body != null) { foreach (var goal in clause.Body) _ClassifyGoal(goal, occurrences); }`
  - `if (clause.Guards != null) { foreach (var guard in clause.Guards) _ClassifyGuard(guard, occurrences); }`
  - `return occurrences;`
- Order preserved: head FIRST, body SECOND, guards THIRD.
- `clause.body!` / `clause.guards!` bangs DROP under C# flow narrowing inside the `!= null` branch.

### Construct 7: `groupByVariable` → C# static method on `OccurrenceExtras`

- Move top-level Dart function onto a `public static class OccurrenceExtras` (host chosen to not collide with `Occurrence` value class nor `OccurrenceClassifier`; mirrors mode.dart / moded_term.dart precedent).
- Signature: `public static Dictionary<string, List<Occurrence>> GroupByVariable(IReadOnlyList<Occurrence> occurrences)`.
- Body:
  - `var result = new Dictionary<string, List<Occurrence>>(StringComparer.Ordinal);`
  - `foreach (var occ in occurrences) { if (!result.TryGetValue(occ.Variable, out var bucket)) { bucket = new List<Occurrence>(); result[occ.Variable] = bucket; } bucket.Add(occ); }`
  - `return result;`
- `StringComparer.Ordinal` matches project-wide string-keyed-Dictionary discipline (well_typed_term.dart, mode_table.dart, message_queue.dart).
- Lazy default-construction preserved — empty bucket built ONLY on cache-miss arm.

### Construct 8: `countOccurrences` → C# static method returning named value tuple

- Emit `public static (int Writers, int Readers) CountOccurrences(IReadOnlyList<Occurrence> occurrences)` on the same `OccurrenceExtras` host class.
- Body:
  - `int writers = 0; int readers = 0;`
  - `foreach (var occ in occurrences) { if (occ.Type == OccurrenceType.Writer) writers++; else readers++; }`
  - `return (Writers: writers, Readers: readers);`
- Dart record `({int writers, int readers})` ↔ C# value tuple `(int Writers, int Readers)` — both value-type structural-equality aggregates (Microsoft Learn "Tuple types"; dart.dev "Records").
- Branch order preserved: writer-test FIRST, reader fallback SECOND — observable under debugger stepping.
- Naming: Dart camelCase `writers`/`readers` → C# PascalCase `Writers`/`Readers` per .NET tuple-element naming conventions.

### Conversion units (faithful to convspec §conversion_units)

1. `enum OccurrenceType { Writer, Reader }` (pmt-namespace; value type; two members in source order — Writer FIRST, Reader SECOND; PascalCase).
2. `sealed class Occurrence : IEquatable<Occurrence>` (reference type with value equality; four get-only auto-properties Variable/Type/Line/Column; positional ctor; `Equals(Occurrence?)`, `Equals(object?)`, `GetHashCode` via `HashCode.Combine(Variable,Type,Line,Column)`, `ToString` `$"{Variable}:{Type}@{Line}:{Column}"`, `==` / `!=` null-safe operator overloads).
3. `class OccurrenceClassifier` (reference type; public readonly `ModeTable ModeTable` get-only property initialised in constructor; positional ctor `OccurrenceClassifier(ModeTable modeTable)`).
4. `ClassifyClause(Clause, IReadOnlyList<Mode>) -> List<Occurrence>` (allocates new `List<Occurrence>`; dispatches `_ClassifyHead` → body foreach if `clause.Body != null` → guards foreach if `clause.Guards != null`; flow-narrowed nulls; no `!`).
5. private `_ClassifyHead(Atom, IReadOnlyList<Mode>, List<Occurrence> @out)` (paired for-loop over `head.Args` zipped with `headModes` by min-length; calls `_CollectVariables` on each arg; `headModes` currently unused — `_ = headModes;` discard plus TODO comment, parameter kept).
6. private `_ClassifyGoal(Goal, List<Occurrence> @out)` (early-return on `goal is RemoteGoal`; recursive descent on `goal is SpawnGoal spawn` via `spawn.InnerGoal`; fall-through foreach `goal.Args` calling `_CollectVariables`).
7. private `_ClassifyGuard(Guard, List<Occurrence> @out)` (foreach `guard.Args` calling `_CollectVariables`).
8. private `_CollectVariables(Term, List<Occurrence> @out)` (pattern-matching is-chain: VarTerm `varTerm` — skip if `varTerm.Name.StartsWith("_", StringComparison.Ordinal)`, else build `Occurrence` with isReader-ternary `OccurrenceType`; StructTerm `structTerm` — foreach `structTerm.Args` recurse; ListTerm `listTerm` — recurse on `Head` if non-null, recurse on `Tail` if non-null; ConstTerm / UnderscoreTerm — no-op per source comment).
9. `static class OccurrenceExtras` (host for top-level helpers — separates instance-state-free helpers from value/classifier types; equivalent of Dart top-level scope).
10. `OccurrenceExtras.GroupByVariable(IReadOnlyList<Occurrence>) -> Dictionary<string, List<Occurrence>>` (TryGetValue-or-Add bucket pattern; `new Dictionary<string,List<Occurrence>>(StringComparer.Ordinal)`; foreach occurrences).
11. `OccurrenceExtras.CountOccurrences(IReadOnlyList<Occurrence>) -> (int Writers, int Readers)` (named C# value tuple; writers tested FIRST against `OccurrenceType.Writer`; else readers; returns `(Writers: writers, Readers: readers)`).

## 3. Decomposed Task Units

- T1: Emit namespace `<root>.Compiler.Pmt` skeleton in `lib/compiler/pmt/occurrence.cs` — done.
- T2: Emit `enum OccurrenceType { Writer, Reader }` preserving member order — done.
- T3: Emit `sealed class Occurrence : IEquatable<Occurrence>` with four get-only auto-properties + positional constructor — done.
- T4: Implement `Occurrence.Equals(Occurrence?)`, `Equals(object?)`, `GetHashCode()` via `HashCode.Combine`, `ToString()` via composite format, `==` / `!=` operator overloads — done.
- T5: Emit `class OccurrenceClassifier` with `public ModeTable ModeTable { get; }` and positional constructor — done.
- T6: Emit `public List<Occurrence> ClassifyClause(Clause, IReadOnlyList<Mode>)` with head → body → guards order, flow-narrowed null checks — done.
- T7: Emit `private void _ClassifyHead(Atom, IReadOnlyList<Mode>, List<Occurrence> @out)` with min-length paired iteration and `_ = headModes;` discard + TODO — done.
- T8: Emit `private void _ClassifyGoal(Goal goal, List<Occurrence> @out)` with `is RemoteGoal` early-return, `is SpawnGoal spawn` recursive descent, fall-through `foreach goal.Args` — done.
- T9: Emit `private void _ClassifyGuard(Guard, List<Occurrence> @out)` with `foreach guard.Args` calling `_CollectVariables` — done.
- T10: Emit `private void _CollectVariables(Term term, List<Occurrence> @out)` with three pattern-matched branches (VarTerm/StructTerm/ListTerm) — done.
- T11: Inside VarTerm branch: ordinal underscore skip via `StringComparison.Ordinal`, ternary on `IsReader`, `@out.Add(new Occurrence(...))` — done.
- T12: Inside StructTerm branch: foreach `structTerm.Args` recurse — done.
- T13: Inside ListTerm branch: two flow-narrowed `Head != null` / `Tail != null` recursive calls — done.
- T14: Preserve trailing `// ConstTerm, UnderscoreTerm — no variables to collect` comment — done.
- T15: Emit `public static class OccurrenceExtras` host — done.
- T16: Emit `static GroupByVariable(IReadOnlyList<Occurrence>) -> Dictionary<string, List<Occurrence>>` with `StringComparer.Ordinal` and TryGetValue-or-Add bucket pattern — done.
- T17: Emit `static CountOccurrences(IReadOnlyList<Occurrence>) -> (int Writers, int Readers)` with writers-FIRST branch order and named-tuple literal return — done.
- T18: Verify cross-file invariants: ast.dart property names (`Args`, `Head`, `Body`, `Guards`, `Name`, `Line`, `Column`, `IsReader`, `InnerGoal`) and mode_table.dart `Mode`/`ModeTable` surface — done (cross-file invariant per convspec Notes).

## 4. Research Findings

All seven non-trivial constructs grounded in cached + fresh rf-* findings recorded in convspec §"Rationale and research provenance":

- **rf-dart-enum-plain-to-csharp-enum** (CACHED — mode_table.dart / message_queue.dart). Authoritative Dart: dart.dev/language/enums. Authoritative .NET: learn.microsoft.com C# enum reference + enumeration naming guidelines.
- **rf-csharp-class-value-equality-iequatable** (CACHED — errors.dart). Authoritative .NET: learn.microsoft.com "How to define value equality for a class or struct". Authoritative Dart: api.dart.dev `Object` `hashCode`/`==` contract.
- **rf-csharp-class-with-readonly-injected-dep-and-private-helpers** (NEW). Authoritative .NET: learn.microsoft.com auto-implemented properties (get-only auto-property initialised in constructor). Authoritative Dart: dart.dev/language/classes#instance-variables (`final` instance variables initialised by constructor parameter).
- **rf-dart-is-type-test-chain-to-csharp-pattern-switch** (NEW). Authoritative .NET: learn.microsoft.com "Patterns" — `is T name` type-pattern with identifier introduces typed local. Authoritative Dart: dart.dev/language/operators — Dart `is` narrows static type inside branch.
- **rf-dart-nullable-field-bang-after-null-check-to-csharp-flow-analysis** (NEW). Authoritative .NET: learn.microsoft.com "Nullable reference types" — flow analysis narrows inside `!= null` branch. Authoritative Dart: dart.dev/null-safety/understanding-null-safety#type-promotion-on-fields — Dart type promotion does NOT apply to fields (why bang is needed).
- **rf-dart-map-putifabsent-to-csharp-trygetvalue-or-add** (CACHED — mode_table.dart / well_typed_term.dart). Four-statement `TryGetValue` / new-bucket / indexer-assign / Add idiom.
- **rf-dart-record-named-fields-to-csharp-value-tuple-named-fields** (NEW). Authoritative .NET: learn.microsoft.com "Tuple types" — value tuples with named members, structural equality. Authoritative Dart: dart.dev/language/records — anonymous immutable aggregate with structural equality.
- **rf-dart-top-level-function-to-csharp-static-method** (CACHED — mode.dart / moded_term.dart). Static-class host for Dart top-level functions.

No fresh research required at plan time — every finding is either cached or already documented in the ratified convspec.

## 5. Consistency Pass

- Construct 1 (`OccurrenceType`): fixed — derived from convspec §constructs[0] (`dart.enum.plain_two_members_writer_reader`).
- Construct 2 (`Occurrence` value class): fixed — derived from convspec §constructs[1] (`dart.value_class.occurrence_record_final_fields_var_type_line_col_tostring_eq_hash`).
- Construct 3 (`OccurrenceClassifier` shape): fixed — derived from convspec §constructs[2] (`dart.class.classifier_with_final_dep_field_one_public_method_dispatching_to_three_privates`).
- Construct 4 (`_classifyGoal` dispatch): fixed — derived from convspec §constructs[3] (`dart.dispatch_method.runtime_type_test_chain_with_early_return`).
- Construct 5 (`_collectVariables` visitor): fixed — derived from convspec §constructs[4] (`dart.recursive_visitor.runtime_type_test_chain_over_term_subclasses_collecting_into_list_param`).
- Construct 6 (`classifyClause` orchestrator): fixed — derived from convspec §constructs[5] (`dart.method.classify_clause_orchestrator_with_three_subdispatches_and_nullable_collections`).
- Construct 7 (`groupByVariable`): fixed — derived from convspec §constructs[6] (`dart.top_level_function.group_by_variable_with_putifabsent_then_add`).
- Construct 8 (`countOccurrences`): fixed — derived from convspec §constructs[7] (`dart.top_level_function.count_writers_readers_returning_dart_record_named_two_ints`).
- Cross-file invariants (ast.dart PascalCase property surface, mode_table.dart `Mode`/`ModeTable` surface): fixed — derived from convspec §Notes.
- Parameter-keyword shadowing (`out` → `@out`): fixed — derived from convspec construct[3].nuance + construct[4].nuance.
- String comparison discipline (`StringComparison.Ordinal`, `StringComparer.Ordinal`): fixed — derived from convspec construct[4].target_decision + construct[6].target_decision (project-wide invariant).
- Convspec escalations: `[]` (zero). Plan inherits zero escalations.

## 6. Escalations

None.
