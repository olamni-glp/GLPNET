---
path: lib/analysis/type_checker/prelude.dart
cycle_group_id: 7
scc_siblings: []
generated_at: 2026-05-21T14:28:27Z
source_sha256: a2cc710565ab37de28ec936b315c082d6c9b766c0fc3f59861b98f5724281bde
schema_version: 1
---

# Conversion Plan: lib/analysis/type_checker/prelude.dart

## 1. Source Analysis

The file is a leaf-level (topo_level 0) prelude module that holds compile-time
constants and pure membership predicates used by the type checker, parser, and
partial evaluator. From direct inspection of the 131-line source:

- **File header (lines 1-8)**: A documentation comment that names the
  specification (`docs/modules/type-environment.md`) and the paper reference
  (Section 8 — Prelude). No code.
- **`const String typePrelude` (line 13)**: A top-level empty-string constant.
  The doc comment explicitly states the prelude source is now empty because
  type/procedure definitions live in `programs/self.glp` and are loaded via the
  scope chain. Importing modules that reference this symbol will still resolve;
  the resolver simply prepends an empty string.
- **`const Set<String> predefinedTypeNames` (lines 18-28)**: A compile-time
  immutable set with 8 string literals — fundamental builtin primitives
  (`Number`, `Integer`, `Real`, `String`, `Constant`, `Exp`, `Stream`,
  `OpenStream`). Comment explicitly notes that library-level types
  (`DiffList`, `Channel`) are intentionally NOT in this set so user programs
  may redefine them.
- **`const Set<String> predefinedProcedureNames` (lines 33-64)**: A
  compile-time immutable set of procedure names protected from user
  redefinition. Grouped by category in the source: type guards, groundness
  guards, time guards, comparison guards, equality, and univ operations.
  Names include operator-like atoms such as `<`, `>`, `=<`, `>=`, `=:=`,
  `=\\=`, `=?=`, `=..`, `..=` (note the escaped backslash in `=\\=` is the
  Dart string literal for the GLP operator `=\=`).
- **`const Set<String> builtinGoals` (lines 70-74)**: Three 0-arity / special
  control goals (`true`, `otherwise`, `:=`) that bypass type checking.
- **`const Set<String> builtinProcedures` (lines 80-118)**: Set of `name/arity`
  keys for true builtins (those implemented in Dart with no GLP clauses).
  Includes the same operator-like atoms with arity suffixes (e.g. `</2`,
  `=\\=/2`, `=../2`, `..=/2`) plus MWM runtime primitives
  (`_allocate_mutual_reference/2`, `is_mutual_ref/1`, `_stream_append/3`,
  `_close_mutual_reference/1`), madGLP network primitive `_send/3`, and the
  system output predicate `_output/1`. Keying by `"name/arity"` is required to
  distinguish overloads.
- **Four predicate functions (lines 121-130)**: Each is a pure top-level
  `String -> bool` expression-bodied function delegating to `.contains` on
  exactly one of the four sets above. No state, no exceptions, no side effects.
  - `isPredefinedType(String name)` → reads `predefinedTypeNames`
  - `isBuiltinGoal(String name)` → reads `builtinGoals`
  - `isPredefinedProcedure(String name)` → reads `predefinedProcedureNames`
  - `isBuiltinProcedure(String nameArity)` → reads `builtinProcedures`

No async, no `Stream`, no isolates, no IO, no generics beyond `Set<String>`,
no mutable state, no nullable types. Callers per tombstone:
`type_environment_builder.dart`, `well_typed_clause.dart`, `parser.dart`,
`partial_evaluator.dart`. Cycle group is a singleton (no SCC peers).

## 2. Dart → C#/.NET Conversion Plan

This section mirrors the ratified per-construct decisions from the convspec
(`construct_key` references in parentheses). No deviation from the convspec.

### 2.1 Container shell

- **C# target**: a single `public static class Prelude` in
  `lib/analysis/type_checker/Prelude.cs` (per `target_code_unit`).
- **Rationale**: C# has no top-level members; a static class is the idiomatic
  container for sets of pure helper methods and shared constants
  (research_finding_id: `csharp-static-class-no-toplevel-members`).
- **Namespace**: follows the project convention for this subtree (to be
  emitted matching whatever sibling files in `lib/analysis/type_checker/`
  use — established at scaffold time, not a planning-stage decision).

### 2.2 `const String typePrelude` → `public const string TypePrelude = "";`
(construct_key `dart.toplevel.const-string-field`,
idiom_id `dart-toplevel-const-string-to-csharp-const-string`)

- **Mapping**: `const string TypePrelude = "";` declared on the static class.
- **Why `const` (not `static readonly`)**: C# permits `const` for `string`
  (string is the one reference type accepted by `const` because of CLR
  interning). The Dart `const` semantic — compile-time constant, baked into
  call sites — maps faithfully.
- **Naming**: Dart `lowerCamelCase` → C# `PascalCase` for public members.
- **No nullable annotation**: `string` (NRT enabled across the project).

### 2.3 Four `const Set<String>` → `static readonly FrozenSet<string>`
(construct_key `dart.toplevel.const-set-string`,
idiom_id `dart-const-set-string-to-csharp-frozenset`)

Applies identically to all four sets:

| Dart symbol                   | C# field                       |
| ----------------------------- | ------------------------------ |
| `predefinedTypeNames`         | `PredefinedTypeNames`          |
| `predefinedProcedureNames`    | `PredefinedProcedureNames`     |
| `builtinGoals`                | `BuiltinGoals`                 |
| `builtinProcedures`           | `BuiltinProcedures`            |

- **Type**: `static readonly System.Collections.Frozen.FrozenSet<string>`.
- **Initialisation**: collection-expression converted via `ToFrozenSet`, e.g.
  `public static readonly FrozenSet<string> PredefinedTypeNames =
   new[] { "Number", "Integer", … }.ToFrozenSet();`
  Initialisation runs at type load, matching Dart's one-time `const`
  materialisation (research_finding_id: `dotnet-frozenset-immutable-readheavy`).
- **String contents preserved verbatim**. Note operator atoms:
  - Dart string literal `"=\\="` decodes to the three-character GLP operator
    `=\=` — the C# verbatim equivalent is `"=\\="` (identical escaping in
    standard C# string literals; OR `@"=\="` as a verbatim literal). Use the
    same escape form as Dart for byte-identical output.
- **Ordering**: Dart `Set` literals preserve insertion order; `FrozenSet`
  does not. The convspec explicitly verifies that all four collections are
  consumed only through `.Contains` (membership) — never enumerated for an
  order-dependent purpose — so the divergence has no observable effect. This
  is documented (not glossed) per the convspec rationale.
- **Element type**: `string` is an immutable reference type — no value-copy
  concern.

### 2.4 Four predicate functions → `public static bool` expression-bodied methods
(construct_key `dart.toplevel.bool-fn-expression-body-contains`,
idiom_id `dart-toplevel-fn-to-csharp-static-method`)

All four 1-arg `String -> bool` predicates collapse to the same shape:

```csharp
public static bool IsPredefinedType(string name) =>
    PredefinedTypeNames.Contains(name);

public static bool IsBuiltinGoal(string name) =>
    BuiltinGoals.Contains(name);

public static bool IsPredefinedProcedure(string name) =>
    PredefinedProcedureNames.Contains(name);

public static bool IsBuiltinProcedure(string nameArity) =>
    BuiltinProcedures.Contains(nameArity);
```

- Dart `=>` expression bodies map 1:1 to C# expression-bodied members.
- `FrozenSet<T>.Contains` is O(1), matching Dart `Set.contains`.
- Parameter naming preserved (`name`, `nameArity`) to maintain caller-site
  readability; the fourth predicate's distinct parameter name documents that
  the key format is `"name/arity"`, not a bare name.

### 2.5 Doc comments

All `///` doc comments transcribe to `///` XML doc comments on the C#
members. The header file comment becomes a file-top `//` comment. Inline
comments inside the set literals (e.g. `// Primitive builtin`, the category
banners in `predefinedProcedureNames` and `builtinProcedures`) carry over as
`//` line comments alongside the elements in the collection expression.

### 2.6 Callers (no change needed, informational)

The four callers (`type_environment_builder.dart`, `well_typed_clause.dart`,
`parser.dart`, `partial_evaluator.dart`) will after their own conversion
reference `Prelude.IsPredefinedType(...)`, `Prelude.IsBuiltinGoal(...)`, etc.
— no API surface drift relative to the Dart source.

## 3. Decomposed Task Units

Each unit corresponds 1:1 to a `conversion_units` entry in the convspec.

- **T1 — Create static container class**
  Done when `lib/analysis/type_checker/Prelude.cs` exists with
  `public static class Prelude { }` in the project's chosen namespace and
  compiles standalone.

- **T2 — Add `const string TypePrelude`**
  Done when `public const string TypePrelude = "";` is on the class and the
  Dart `///` doc-comment text is reproduced as an XML `<summary>`.

- **T3 — Add `PredefinedTypeNames` FrozenSet**
  Done when the field is declared `static readonly FrozenSet<string>`,
  initialised with the eight literals in source order, and the inline
  `// Primitive builtin` / `// Note: …` comments are preserved.

- **T4 — Add `PredefinedProcedureNames` FrozenSet**
  Done when the field is declared as above, all category banner comments are
  preserved, and operator-atom string literals (`<`, `>`, `=<`, `>=`, `=:=`,
  `=\=`, `=?=`, `=..`, `..=`) are present with byte-identical content
  (Dart's `"=\\="` rendered as C# `"=\\="`).

- **T5 — Add `BuiltinGoals` FrozenSet**
  Done when the field carries the three control atoms (`true`, `otherwise`,
  `:=`) and the explanatory comment is preserved.

- **T6 — Add `BuiltinProcedures` FrozenSet**
  Done when all 24 `name/arity` keys are present in source order, MWM /
  madGLP / output sub-banners are preserved, and operator+arity keys
  (`</2`, `=\=/2`, `=../2`, `..=/2`, etc.) are byte-identical.

- **T7 — Implement `IsPredefinedType(string)`**
  Done when the expression-bodied static method delegates to
  `PredefinedTypeNames.Contains(name)` and the Dart doc-comment is
  reproduced as XML `<summary>`.

- **T8 — Implement `IsBuiltinGoal(string)`**
  Done when the method delegates to `BuiltinGoals.Contains(name)` with
  preserved doc-comment.

- **T9 — Implement `IsPredefinedProcedure(string)`**
  Done when the method delegates to `PredefinedProcedureNames.Contains(name)`
  with preserved doc-comment.

- **T10 — Implement `IsBuiltinProcedure(string nameArity)`**
  Done when the method delegates to `BuiltinProcedures.Contains(nameArity)`,
  the parameter name `nameArity` is preserved, and the doc-comment that
  explains the `name/arity` key contract is reproduced.

- **T11 — File-level documentation comment**
  Done when the header (lines 1-8 of the Dart source — purpose, specification
  reference, paper reference) is reproduced as a file-top C# comment block.

## 4. Research Findings

None required — the convspec already records two authoritative research
findings (`csharp-static-class-no-toplevel-members`,
`dotnet-frozenset-immutable-readheavy`), both with verbatim citations to
official `learn.microsoft.com` documentation. Every construct in this file
resolves against those findings. No new research is needed at the planning
stage.

## 5. Consistency Pass

- **§2 vs convspec construct table**: every `construct_key` in the convspec
  appears in §2 with a 1:1 target decision and the same `idiom_id` and
  `research_finding_id` references. No additions, no contradictions.
- **§2 vs convspec `conversion_units`**: the 10 conversion_units
  (`static-class-shell`, `const-field` ×1, `frozenset-field` ×4,
  `static-method` ×4) all appear as targets in §2.1–§2.4 and as task units
  T1–T10 in §3; T11 covers the file-header comment (a structural carry-over,
  not a numbered conversion_unit but required for faithful conversion). No
  silent drops.
- **§3 vs §2**: each task unit references the §2 construct it implements;
  acceptance criteria are byte-faithful ("preserved", "byte-identical",
  "in source order") matching the convspec's nuance about ordering being
  unused-but-preserved.
- **Operator atom escaping**: convspec's source_form quotes `=\\=` as written
  in Dart; §2.3 explicitly documents that the C# port reproduces this
  identically — derived verbatim from inspection of source lines 56, 103.
- **Cycle group**: instruction states `cycle_group_id: 7, scc_siblings: []`
  (singleton). Tombstone still shows `cycle_group_id: 9` — the orchestrator's
  cycle-group reassignment is authoritative for plan front-matter; no §7
  needed (singleton). Recorded here as derivation from orchestrator
  instruction.
- **Contracts (012/015)**: this file is a pure-leaf constants/predicates
  module — it touches none of the bridge, depgraph, scaffold, or langpair
  contracts at runtime. No contract gaps.
- **Callers (tombstone)**: four callers listed; none of them are converted
  yet, but their future call sites become `Prelude.<Method>(...)` with the
  same arity/signature shape — no API drift.

No unresolved inconsistencies. No items escalated.

## 6. Escalations

None.
