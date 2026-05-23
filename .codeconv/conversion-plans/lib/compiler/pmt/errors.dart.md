---
path: lib/compiler/pmt/errors.dart
cycle_group_id: 54
scc_siblings: []
generated_at: 2026-05-21T15:00:00Z
source_sha256: 37c3d4a451199f6d875bcfbadc4d5b9b5bd80ed139d6d833de4e479dac9e3339
schema_version: 1
---

# Conversion Plan: lib/compiler/pmt/errors.dart

## 1. Source Analysis

The file `lib/compiler/pmt/errors.dart` is a small, purely synchronous Dart
source unit (22 LOC of code, 35 LOC total) containing exactly two top-level
declarations and one doc comment. Inspection of the actual `.dart` bytes
(sha256 `37c3d4a4…3339`, matches convspec) reveals:

- **Doc comment** (line 1): `/// PMT Error: Represents a mode/SRSW violation
  detected during PMT checking`. Single-line `///` triple-slash documentation
  associated with the next top-level declaration (`PmtError`).

- **Class `PmtError`** (lines 3–22): a plain reference-type Dart class that
  hand-codes value-equality semantics on top of reference identity.
  - Three `final` instance fields: `String message`, `int line`,
    `int column`. None nullable (no `?`), so under Dart null-safety they are
    non-null by contract.
  - One positional generative constructor `PmtError(this.message, this.line,
    this.column)` — three positional initialising-formal parameters; no
    optional/named/default parameters; no `const` modifier.
  - `@override String toString() => 'PMT Error at $line:$column: $message';`
    expression-bodied method using Dart string interpolation. The interpolated
    members are `int`, `int`, `String` (no `${expr}` expressions).
  - `@override bool operator ==(Object other) => other is PmtError &&
    message == other.message && line == other.line && column == other.column;`
    — type-test-and-narrow (`is PmtError` then `other.message` reads as
    `PmtError.message` via Dart's flow-typing/promotion), then short-circuit
    member-wise equality across all three fields.
  - `@override int get hashCode => Object.hash(message, line, column);` —
    structural hash via the Dart core `Object.hash(...)` static helper, in
    field-declaration order (matches the `==` field order).
  - No factory constructor, no static members, no mixins, no `extends`.

- **Class `PmtErrors`** (lines 25–35): an aggregate exception bundling a list
  of `PmtError`.
  - Doc comment line 24: `/// Exception thrown when PMT checking fails`.
  - `class PmtErrors implements Exception` — uses the `implements Exception`
    Dart idiom (Dart `Exception` is a marker interface; no fields, no
    methods, no `message` contract).
  - One `final` field `List<PmtError> errors` (the field is `final`; the
    referenced list is mutable in Dart unless wrapped — not wrapped here).
  - One positional generative constructor `PmtErrors(this.errors)` —
    initialising-formal aliasing the caller's list (no copy).
  - `@override String toString()` body: early-return
    `if (errors.isEmpty) return 'PmtErrors: (none)';`, otherwise
    `return 'PmtErrors:\n${errors.map((e) => '  $e').join('\n')}';` —
    iterates `errors` via `Iterable.map`, formats each element with two-
    space indent using `'  $e'` (which invokes `PmtError.toString()` via
    Dart's string-interpolation `toString` call), and joins with `\n`.

- **Dependencies**: none. No `import`/`export` declarations. Tombstone
  confirms `dependencies: []`. Callers are
  `lib/compiler/pmt/checker.dart` and `lib/compiler/pmt/validator.dart`
  (per tombstone).

- **Concurrency / async surface**: none. No `Future`, `Stream`, `async`,
  `await`, `Isolate`, `Completer`, `late`, `?` operators in this file.
  Purely synchronous data + exception.

- **Sealed/mixin/extension**: none.

The convspec correctly identifies two constructs: a hand-rolled
value-equality class (`PmtError`) and an aggregate exception with marker-
interface origin (`PmtErrors`).

## 2. Dart → C#/.NET Conversion Plan

This section mirrors the ratified convspec at
`.codeconv/conversion-specs/lib/compiler/pmt/errors.dart.md` verbatim in
substance; idiom IDs and research-finding IDs are preserved.

**Target code unit**: `lib/compiler/pmt/errors.cs`

### Construct 1 — `class PmtError` → C# sealed value-equality class

- `dart.value_class.error_record_final_fields_message_line_column_tostring_override`
  → `sealed class PmtError : IEquatable<PmtError>`
- Three get-only auto-properties:
  - `string Message` (non-nullable; NRT enabled)
  - `int Line`
  - `int Column`
  - All initialised in the constructor (no init-only setters needed; get-
    only suffices because the constructor assigns once).
- Single positional constructor `PmtError(string message, int line,
  int column)` — assigns the three properties.
- `bool Equals(PmtError? other)` — type-specific equality required by
  `IEquatable<PmtError>`; null-check then member-wise compare on
  `Message`/`Line`/`Column` (`string` uses `string.Equals` ordinal default;
  `int` uses `==`).
- `override bool Equals(object? obj)` — delegates to `Equals(obj as
  PmtError)`.
- `override int GetHashCode()` — returns `HashCode.Combine(Message, Line,
  Column)`. Mirrors Dart `Object.hash(message, line, column)` (order-
  sensitive structural hash of three values).
- `override string ToString()` — returns `$"PMT Error at {Line}:{Column}:
  {Message}"`. C# string interpolation is the direct translation of Dart
  `'PMT Error at $line:$column: $message'`; `int` formats invariantly in
  composite formatting for these small values, matching Dart.
- `==` and `!=` operator overloads with the documented null-safe pattern
  (`ReferenceEquals(a, b)` short-circuit, both-null → true, one-null →
  false, otherwise delegate to `Equals`). Required by Microsoft Learn rule 3
  for consistency with `Equals`.
- `sealed` modifier prevents subclassing — moots the polymorphic-equality
  `GetType()` symmetry pitfall the Microsoft doc warns about.
- **REJECTED**: emitting a `record class PmtError(string Message, int Line,
  int Column)`. Not because the three primitive members would mis-compare
  under record equality (they wouldn't), but because: (a) the Dart source
  explicitly hand-wrote `==`/`hashCode` (deliberate value-equality intent
  to preserve verbatim), and (b) project precedent
  (`lib/analysis/type_checker/type_ast.dart` construct
  `dart.value_class.manual_eq_hashcode_with_list_element_equality`) is
  hand-rolled `IEquatable<T>` for any Dart class with hand-rolled
  `==`/`hashCode`.
- **Nuance preserved**: Dart non-nullable `String` → C# non-nullable `string`
  (NRT enabled); Dart `Object` parameter → C# `object?` override parameter;
  interpolation is a 1:1 syntactic translation; `HashCode.Combine` is the
  .NET-idiomatic counterpart to `Object.hash` and shares the order-sensitive
  contract.

### Construct 2 — `class PmtErrors implements Exception` → C# exception subclass

- `dart.exception_aggregate_class.implements_Exception_list_of_errors_tostring_with_empty_branch`
  → `class PmtErrors : Exception`
- **Inheritance, NOT interface implementation**: `System.Exception` is the
  universal throwable base in .NET; there is no marker-interface idiom for
  exceptions in .NET. The Dart `implements Exception` marker → C# `:
  Exception` direct inheritance.
- One get-only property `IReadOnlyList<PmtError> Errors` — exposes the
  caller's list as a read-only view without copying (mirrors Dart
  `PmtErrors(this.errors)` aliasing semantics).
- Single primary constructor
  `PmtErrors(IReadOnlyList<PmtError> errors)` that:
  - Calls `: base(FormatSummary(errors))` to seed `Exception.Message` with
    the same human-readable summary `ToString` produces. `FormatSummary` is
    a `private static string` helper used by both the constructor and
    `ToString` (computed once at construction and stored implicitly via
    `base.Message`; `ToString` re-derives from `Errors` so the empty-vs-
    non-empty branches stay live if a caller ever mutates the underlying
    `List<PmtError>` they passed in — same aliasing semantics as Dart).
  - Stores `errors` to `Errors`.
- **Critical**: the standard three .NET exception constructors (`()`,
  `(string)`, `(string, Exception)`) recommended by Microsoft Learn are
  intentionally NOT added. The Dart source has exactly one logical
  constructor (taking the list); the .NET recommendation is guidance, not
  contract — adding constructors with no Dart counterpart would enlarge the
  conversion surface beyond the source.
- `override string ToString()` with the empty-branch logic:
  - If `Errors.Count == 0` return `"PmtErrors: (none)"`.
  - Otherwise return `"PmtErrors:\n"` followed by each error joined with
    `"\n"`, each element prefixed by two spaces (`"  " + e.ToString()` per
    element). Mirrors Dart
    `'PmtErrors:\n${errors.map((e) => '  $e').join('\n')}'`. The interior
    `$"{e}"` / `'$e'` resolves to a virtual `ToString` call in both
    languages — preserved verbatim.
- **List aliasing preserved**: the C# property holds the same `IReadOnlyList<
  PmtError>` reference the caller supplied (typically a `List<PmtError>`
  upcast); no defensive copy. Matches Dart `final List<PmtError> errors`
  semantics where the reference is final but the contents are not.
- **Nuance preserved**: Dart `Exception` is a marker interface (no fields,
  no message); .NET `Exception` has a `Message` property used by debuggers,
  loggers, and `AggregateException` printers, so the seeded `base(message)`
  is what makes the C# exception observably equivalent under standard .NET
  tooling. `ToString` override is still emitted for parity with the Dart
  source.

### File-level conventions

- Target file: `lib/compiler/pmt/errors.cs` (subtree-rel path, matches
  convspec `target_code_unit`).
- Namespace: per project scaffolding convention (deferred to scaffold/
  codegen stage; not asserted here because the convspec does not specify
  one and the project's namespace convention is centralised elsewhere).
- NRT enabled: required for the non-nullable `string Message` decision and
  the `object?` override signature.
- `using System;` (for `Exception`, `HashCode`); `using System.Collections.
  Generic;` (for `IReadOnlyList<T>`).

## 3. Decomposed Task Units

- **T1 — Emit `PmtError` skeleton + properties**
  - Definition of done: `sealed class PmtError : IEquatable<PmtError>` exists
    with three get-only auto-properties (`Message: string`, `Line: int`,
    `Column: int`) and a single positional constructor assigning them.

- **T2 — Implement `PmtError` value-equality surface**
  - Definition of done: `Equals(PmtError?)`, `override Equals(object?)`,
    `override GetHashCode()` (via `HashCode.Combine(Message, Line, Column)`),
    and `==` / `!=` operator overloads with the null-safe pattern (both-null
    → true, one-null → false, otherwise delegate to `Equals`) are present
    and consistent.

- **T3 — Implement `PmtError.ToString` override**
  - Definition of done: `override string ToString()` returns
    `$"PMT Error at {Line}:{Column}: {Message}"`.

- **T4 — Emit `PmtErrors` skeleton inheriting `System.Exception`**
  - Definition of done: `class PmtErrors : Exception` exists with a get-only
    `IReadOnlyList<PmtError> Errors` property and a single primary
    constructor `PmtErrors(IReadOnlyList<PmtError> errors)` that assigns
    `Errors = errors` (no defensive copy).

- **T5 — Seed `Exception.Message` via constructor chaining**
  - Definition of done: the constructor in T4 calls `: base(FormatSummary(
    errors))`, where `FormatSummary` is a `private static string` helper
    that produces the same string as the non-empty / empty branches of
    `ToString`.

- **T6 — Implement `PmtErrors.ToString` override with empty-branch logic**
  - Definition of done: `override string ToString()` returns
    `"PmtErrors: (none)"` when `Errors.Count == 0`; otherwise returns
    `"PmtErrors:\n"` + the errors joined by `"\n"`, each prefixed with two
    spaces (`"  " + e.ToString()`).

- **T7 — File-level boilerplate**
  - Definition of done: `errors.cs` contains the required `using` directives
    (`System`, `System.Collections.Generic`), is NRT-aware (file or
    project-level `#nullable enable`), and is placed at
    `lib/compiler/pmt/errors.cs`.

## 4. Research Findings

None required at the plan stage. The convspec already cites two
authoritative research findings ratified during the convspec phase, and the
plan inherits them verbatim:

- `rf-csharp-class-value-equality-iequatable` — Microsoft Learn "How to
  define value equality for a type" (the four-step recipe + null-safe `==`
  template + sealed-class symmetry note) and Dart core API
  `Object.hashCode` contract.
- `rf-dart-exception-marker-to-csharp-exception-subclass` — Dart core API
  `Exception` class (marker-interface specification) and Microsoft Learn
  "How to: Create user-defined exceptions" (`derive from Exception`,
  three-constructor recommendation as guidance).

No new external research is performed at the plan stage (web research is
forbidden in this stage). All §2 decisions are verbatim-derivable from the
convspec, the file contents, and the cited research above.

## 5. Consistency Pass

- §2 Construct 1 vs convspec construct
  `dart.value_class.error_record_final_fields_message_line_column_tostring_override`:
  IDENTICAL — sealed class with `IEquatable<T>`, three get-only properties,
  manual `Equals`/`GetHashCode`/`==`/`!=`, `HashCode.Combine`, `ToString`
  override. No gap.
- §2 Construct 2 vs convspec construct
  `dart.exception_aggregate_class.implements_Exception_list_of_errors_tostring_with_empty_branch`:
  IDENTICAL — derives from `System.Exception`, `IReadOnlyList<PmtError>`
  property, single primary constructor seeding `base(message)` via a
  private static formatter, `ToString` override with empty-branch and
  joined-with-two-space-indent branches; the three "standard" .NET
  exception constructors intentionally NOT added. No gap.
- §3 task units vs §2 decisions: T1+T2+T3 cover the `PmtError` decisions
  one-to-one (skeleton, equality surface, `ToString`); T4+T5+T6 cover the
  `PmtErrors` decisions one-to-one (skeleton + property, message seeding,
  `ToString` empty-branch); T7 covers file-level boilerplate. Every §2
  decision element is reachable from a §3 task. No gap.
- §4 research vs §2 decisions: both research findings cited in §4 are
  invoked in §2 (rf-csharp-class-value-equality-iequatable underpins the
  Construct 1 four-step recipe + `==`/`!=` pattern; rf-dart-exception-
  marker-to-csharp-exception-subclass underpins the Construct 2 inheritance
  decision and the `base(message)` seeding). No gap.
- §2 / §3 vs source file: every Dart member observed in §1 has a §2
  mapping and a §3 task (three `PmtError` fields → properties via T1; both
  `PmtError` operator/method overrides → T2/T3; `PmtErrors` field + ctor +
  `toString` → T4/T5/T6). No orphan members. No gap.
- Convspec `escalations: []` and convspec status RATIFIED: no inherited
  open question. No gap.

All consistency checks pass with zero deltas; no escalations raised in this
pass.

## 6. Escalations

None.
