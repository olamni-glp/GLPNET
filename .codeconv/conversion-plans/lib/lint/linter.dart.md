---
path: lib/lint/linter.dart
cycle_group_id: 63
scc_siblings: []
generated_at: 2026-05-21T16:18:51Z
source_sha256: 257a66f29065ce82f55ec45df025e87aba1bcaeee0deaf93c42d93f27335bff7
schema_version: 1
---

# Conversion Plan: lib/lint/linter.dart

## 1. Source Analysis

Direct inspection of `glp_runtime_net/lib/lint/linter.dart` (147 lines, sha256 `257a66f2...5bff7`):

- **Imports** (lines 1–2): two intra-project package imports — `package:glp_runtime/bytecode/opcodes.dart` (pulls in the `Op` class hierarchy + all opcode subtypes: `ClauseTry`, `HeadBindWriter`, `GuardNeedReader`, `GuardFail`, `UnionSiAndGoto`, `ResetAndGoto`, `Label`, `Commit`, `SuspendEnd`, `BodySetConst`, `BodySetStructConstArgs`, `Proceed`) and `package:glp_runtime/bytecode/runner.dart` (pulls in `BytecodeProgram`).
- **`class LintIssue`** (lines 4–11): three `final` fields (`String code`, `String message`, `int index`); positional constructor `LintIssue(this.code, this.message, this.index)`; `@override String toString() => '[\$code] @op#\$index: \$message'`. Immutable data-class shape; instances are stored in a list and inspected by callers (tests).
- **`class LintResult`** (lines 13–17): one `final List<LintIssue> issues` (final REFERENCE, mutable contents); positional constructor; computed getter `bool get ok => issues.isEmpty`.
- **`class Linter`** (lines 19–147): single public method `LintResult lint(BytecodeProgram p)` that drives a four-state machine over `p.ops`:
  - state vars: `inClause` (bool), `inBody` (bool), `seenSuspendEnd` (bool), `suspendEndIndex` (int, init `-1`); local growable `issues = <LintIssue>[]`.
  - local function `bool isHeadGuardOp(Op op)` — single-expression `=>` disjunction of seven `op is X` runtime type-tests.
  - classical `for (var i = 0; i < p.ops.length; i++)` loop. Per-iteration cascade of `if (op is X) { ...; continue; }` arms driving the state machine. ARM ORDER is load-bearing: (1) `op is Label` → continue; (2) `seenSuspendEnd` branch — if `op is ClauseTry`, emit `SUSPEND_ONCE_AT_END`, then continue; (3) `!inClause` predicate-level branch — `ClauseTry` opens clause, `SuspendEnd` records final suspend, anything else emits `ILLEGAL_PRECOMMIT_OP`; (4) inside-clause head/guard branch (`!inBody`) — `Commit` flips to body, `UnionSiAndGoto|ResetAndGoto` closes clause, `SuspendEnd` records final suspend + closes clause, `BodySetConst|BodySetStructConstArgs|Proceed` emit `BODY_BEFORE_COMMIT`, anything else not matching `isHeadGuardOp` emits `ILLEGAL_PRECOMMIT_OP`; (5) inside-clause body branch — `UnionSiAndGoto|ResetAndGoto` emit `ILLEGAL_BODY_OP`, `SuspendEnd` emits `ILLEGAL_BODY_OP` + closes clause, `Commit` emits `REDUNDANT_COMMIT`.
  - Final post-loop check: `p.ops.whereType<SuspendEnd>().length` — if `>1`, emit `SUSPEND_ONCE_AT_END` at `suspendEndIndex >= 0 ? suspendEndIndex : 0`.
  - Returns `LintResult(issues)` — passes the same list reference (no defensive copy).
- **Lint codes emitted**: `SUSPEND_ONCE_AT_END`, `ILLEGAL_PRECOMMIT_OP`, `BODY_BEFORE_COMMIT`, `ILLEGAL_BODY_OP`, `REDUNDANT_COMMIT` (five string literals).
- **Diagnostic interpolation**: four messages interpolate `${op.runtimeType}` for the unqualified runtime-class name.
- **Absent constructs** (correctly not asserted): no Stream/Future/async/await, no isolates, no `late`/`sealed`/`mixin`, no nullable fields.

## 2. Dart → C#/.NET Conversion Plan

Each construct from the ratified convspec, mirrored verbatim:

### C1. `dart.import_directive.package_internal_to_using_namespace`
Two intra-project `package:glp_runtime/...` imports → C# `using` directives over the namespaces produced by converted `opcodes.cs` / `runner.cs` (e.g. `using GlpRuntime.Bytecode;`). NO third-party packages; both targets are sibling code units in the same converted assembly. Type symbols pulled in: `BytecodeProgram`, `Op`, and every opcode subtype `is`-checked in §1 (`ClauseTry`, `HeadBindWriter`, `GuardNeedReader`, `GuardFail`, `UnionSiAndGoto`, `ResetAndGoto`, `Label`, `Commit`, `SuspendEnd`, `BodySetConst`, `BodySetStructConstArgs`, `Proceed`).

Nuance: FR-024 cache hit (identical decision as `bytecode/runner.dart.md` lines 77–115). Namespace fidelity nuance: the `Op`-subtype `is` checks below assume the converted `opcodes.cs` hierarchy preserves the same nominal type names (no flattening to an enum tag) — required by `rf-dart-pattern-match-cascade-dispatch-to-csharp-is-pattern-cascade` at the call site.

Research provenance: `rf-dart-import-relative-to-csharp-using-namespace`.

### C2. `dart.data_class.immutable_final_fields_positional_ctor_with_optional_positional` — `class LintIssue`
Reference-type C# class (Dart class instances are heap reference objects with identity; `LintResult` holds a `List<LintIssue>` by reference, and `List.Add` elsewhere relies on that). Three `final` instance fields → get-only auto-properties initialised from a single positional constructor (immutability preserved, no setters exposed). `@override String toString()` → `override string ToString()`. `index` is a Dart `int` source-position index → mapped to `long` for width fidelity (`rf-dart-int-to-csharp-long-width` — opcodes.dart.md cache hit). String interpolation `'[\$code] @op#\$index: \$message'` → C# interpolated-string `$"[{Code}] @op#{Index}: {Message}"`.

Nuance: Reuse — identical shape to `opcodes.dart.md` LineToken/SourceSpan (`rf-dart-final-field-class-to-csharp-getonly-class`). LintIssue MUST be a CLASS, NOT a record-struct, because (a) reference identity must survive being stored in a list and inspected by test code, and (b) the `ToString` override is polymorphic. Every field is non-nullable (no `?`); the constructor enforces presence. `index` widens to `long`.

Research provenance: `rf-dart-final-field-class-to-csharp-getonly-class`.

### C3. `dart.data_class.list_field_with_isempty_getter_idiomatic` — `class LintResult`
Reference-type C# class with a get-only `List<LintIssue>` property (kept as the concrete `List<T>` to preserve eager-snapshot, index-and-iterate semantics callers rely on; tests assert `.issues.length` / indexing). `bool get ok => issues.isEmpty` → computed get-only property `public bool Ok => Issues.Count == 0` (Dart `Iterable.isEmpty` → .NET `List<T>.Count == 0`; direct counterpart to `isEmpty`).

Nuance: Reuse of `rf-dart-final-field-class-to-csharp-getonly-class` (cache hit). Final-field-of-mutable-collection nuance: the `final List` is a final REFERENCE to a mutable list — the reference cannot be reassigned, but contents are mutated by the linter (`issues.add(...)`); this matches a C# get-only property of type `List<T>` (the property is read-only, the list contents are not). Choosing `IReadOnlyList<T>` would over-constrain because the converted Linter.Lint method needs to Add during construction. Eager-vs-lazy: Dart `.isEmpty` on a populated List is O(1); `Count == 0` is also O(1) on .NET `List<T>` — direct counterpart, no LINQ deferral concerns.

Research provenance: `rf-dart-final-field-class-to-csharp-getonly-class`.

### C4. `dart.local_function_typed_predicate_closure_over_is_pattern_cascade` — `isHeadGuardOp`
Dart local-function with single-expression `=>` body → C# LOCAL FUNCTION `static bool IsHeadGuardOp(Op op) => ...` inside the `Lint` method (preferred over a lambda assigned to a `Func<Op,bool>` because the local function avoids delegate allocation and matches Dart local-function semantics: a named callable scoped to the enclosing method). The chained `op is X || op is Y || ...` → directly equivalent C# `op is X || op is Y || ...` using the C# `is` type-pattern. Marked `static` because it captures no locals.

Nuance: FR-024 cache hit (same idiom as `bytecode/runner.dart.md` dispatch loop). Dart `op is X` and C# `op is X` are SEMANTICALLY EQUIVALENT runtime type-tests (both null-safe — return `false` if `op` is null, no NullReferenceException). Codegen MUST preserve ARM ORDER (Label / UnionSiAndGoto / ResetAndGoto are reached frequently); reordering would change neither correctness nor observable behaviour but is unnecessary. Reject conversion to `switch (op) { case X _: ... }` because the predicate returns a single bool and the disjunction reads cleanly as a chained `is`.

Research provenance: `rf-dart-pattern-match-cascade-dispatch-to-csharp-is-pattern-cascade`.

### C5. `dart.classical_for_loop_index_into_list_dynamic_with_is_dispatch_and_state_machine` — main loop
Classical Dart `for (var i = 0; i < p.ops.length; i++)` → classical C# `for (int i = 0; i < p.Ops.Count; i++)` (index preserved because the body emits `LintIssue` with `index: i`; `foreach` would lose the running index; `Enumerable.Range` + LINQ would obscure the imperative state machine). `final op = p.ops[i]` → `Op op = p.Ops[i]` (or `var op = p.Ops[i]`). Each `if (op is X) { ... continue; }` arm preserved verbatim as a C# `is`-pattern cascade. The four local bool flags `inClause`, `inBody`, `seenSuspendEnd` plus `suspendEndIndex` → local C# variables of the same types (`bool` and `int` for the running index — kept as `int` here because it is a list index, not a width-sensitive source position; if `BytecodeProgram.Ops` is converted with a long-indexed accessor the type widens accordingly).

Nuance: Reuse of `rf-dart-pattern-match-cascade-dispatch-to-csharp-is-pattern-cascade` (FR-024 cache hit). State-machine fidelity nuance: ARM ORDER is load-bearing — the Label early-continue MUST come first, the post-SuspendEnd branch second, the `!inClause` predicate-level branch third, and the body/head split last; reordering would change which LintIssue codes fire on edge cases (e.g. a Label appearing after SuspendEnd is silently skipped by design). `continue` semantics are identical in both languages. Reject conversion to `foreach (var (i, op) in p.Ops.Select((o, idx) => (idx, o)))` because (a) it adds a LINQ projection allocation per call, and (b) the imperative shape mirrors the Dart source for review traceability.

Research provenance: `rf-dart-pattern-match-cascade-dispatch-to-csharp-is-pattern-cascade`.

### C6. `dart.list_literal_typed_growable_with_add`
`<LintIssue>[]` → `new List<LintIssue>()` (empty, growable .NET `List<T>` — same semantics as Dart's growable list literal; both amortise Add to O(1)). `issues.add(...)` → `issues.Add(...)`. The list is then passed to `new LintResult(issues)`, which stores the same reference (no defensive copy in either language).

Nuance: Cache hit on `rf-dart-iterable-where-to-linq` (analysis_phase.dart.md) for the collection-literal-init shape. Growable-list nuance: Dart `<T>[]` is growable by default; .NET `new List<T>()` is also growable by default — equivalent. The list reference IS shared between Linter and LintResult (no defensive copy on either side), so post-return mutations would be visible — acceptable because Linter no longer holds the reference after `return` (locked-in fact, not a hidden mutation contract).

Research provenance: `rf-dart-iterable-where-to-linq`.

### C7. `dart.iterable_wheretype_count_to_csharp_linq_oftype_count` — final post-loop check
Dart `Iterable.whereType<T>()` filters by runtime type and skips elements that are not `T`, returning an `Iterable<T>`. The .NET counterpart is LINQ `Enumerable.OfType<T>()`. `.length` on the filtered Iterable → `.Count()` (LINQ terminal). The conditional `suspendEndIndex >= 0 ? suspendEndIndex : 0` → direct C# ternary `suspendEndIndex >= 0 ? suspendEndIndex : 0`. The interpolated message → C# interpolated-string with `{suspendCount}`.

Nuance: Refinement of `rf-dart-iterable-where-to-linq` for the whereType-specific shape: Dart `whereType<T>()` is the direct counterpart of .NET `OfType<T>()` (both skip non-T elements; both deferred until a terminal). Both are O(n) here because the `.length` / `.Count()` terminal materialises the count by walking the list. Could also be expressed as `p.Ops.Count(o => o is SuspendEnd)` — single-pass with no projection allocation; codegen MAY prefer `Count(predicate)` for marginal efficiency, but `OfType<SuspendEnd>().Count()` is the more LITERAL translation and is preferred for review traceability.

Research provenance: `rf-dart-iterable-where-to-linq`.

### C8. `dart.private_helper.runtimetype_in_interpolation_to_csharp_gettype_name` — four diagnostic messages
Dart `op.runtimeType` returns the dynamic Type token (`Object.runtimeType` — dart.dev language spec) whose `toString()` yields the unqualified class name (e.g. `ClauseTry`). The .NET counterpart is `op.GetType().Name` (`System.Type.Name` — Microsoft Learn — returns the simple name of the type without namespace) inside a C# interpolated-string. The interpolation becomes `$"...: {op.GetType().Name}"`.

Nuance: Equivalence basis: Dart `runtimeType.toString()` returns the unqualified class name by convention; .NET `Type.Name` likewise returns the simple unqualified name (versus `Type.FullName` which would include the namespace). The simple name is what the diagnostic messages here intend (lint message readability), so `.GetType().Name` is the correct counterpart — `FullName` would change the user-visible diagnostic. Mapping is over messages emitted to a list of LintIssue consumed by test code — test assertions check the `code` field, not the message body, so even `FullName` would not break tests; but the message is documentation, and `Name` preserves Dart-side readability.

Research provenance: `rf-dart-runtimetype-in-interpolation-to-csharp-gettype-name`.

### Conversion units (target file layout)

Per convspec, one target code unit `lib/lint/linter.cs` containing:
- `class LintIssue` — get-only `Code` (string), `Message` (string), `Index` (long) properties; positional constructor; `ToString` override with interpolated `$"[{Code}] @op#{Index}: {Message}"`.
- `class LintResult` — get-only `Issues` property of type `List<LintIssue>`; computed `Ok => Issues.Count == 0`.
- `class Linter` — single public method `LintResult Lint(BytecodeProgram p)` containing the four-state imperative state machine over `p.Ops` with `is`-pattern cascade and local `static bool IsHeadGuardOp(Op op)` predicate; final `whereType`-equivalent count check via `OfType<SuspendEnd>().Count()`.

## 3. Decomposed Task Units

- **T1** — Done: write `using` directives for converted `opcodes.cs` / `runner.cs` namespaces (no NuGet refs) — C1.
- **T2** — Done: generate `class LintIssue` (reference type, three get-only auto-properties `Code:string`, `Message:string`, `Index:long`, positional ctor, `override string ToString()` with interpolation) — C2.
- **T3** — Done: generate `class LintResult` (reference type, get-only `Issues: List<LintIssue>` property, positional ctor, computed `public bool Ok => Issues.Count == 0`) — C3.
- **T4** — Done: emit `class Linter` shell with single public method signature `public LintResult Lint(BytecodeProgram p)` — C5 scaffold.
- **T5** — Done: emit local `static bool IsHeadGuardOp(Op op) => op is ClauseTry || op is HeadBindWriter || op is GuardNeedReader || op is GuardFail || op is UnionSiAndGoto || op is ResetAndGoto || op is Label;` inside `Lint` — C4.
- **T6** — Done: emit local variables `var issues = new List<LintIssue>(); var inClause = false; var inBody = false; var seenSuspendEnd = false; var suspendEndIndex = -1;` — C5 + C6.
- **T7** — Done: emit classical `for (int i = 0; i < p.Ops.Count; i++) { var op = p.Ops[i]; ... }` body containing the five-arm cascade (Label early-continue; seenSuspendEnd branch; !inClause branch; !inBody head/guard branch; body branch) — C5, ARM ORDER preserved verbatim.
- **T8** — Done: emit each `issues.Add(new LintIssue("CODE", $"... {op.GetType().Name} ...", i));` call site with the exact five lint-code string literals (`SUSPEND_ONCE_AT_END`, `ILLEGAL_PRECOMMIT_OP`, `BODY_BEFORE_COMMIT`, `ILLEGAL_BODY_OP`, `REDUNDANT_COMMIT`) and C# interpolated messages — C8.
- **T9** — Done: emit post-loop `var suspendCount = p.Ops.OfType<SuspendEnd>().Count(); if (suspendCount > 1) { issues.Add(new LintIssue("SUSPEND_ONCE_AT_END", $"Multiple SuspendEnd opcodes found ({suspendCount}). Expect a single final suspend per predicate.", suspendEndIndex >= 0 ? suspendEndIndex : 0)); }` — C7.
- **T10** — Done: emit `return new LintResult(issues);` (shared list reference, no defensive copy) — C3 + C6.

## 4. Research Findings

None required — all eight constructs resolved via FR-024 cache hits against prior ratified convspecs:

- C1 → `rf-dart-import-relative-to-csharp-using-namespace` (cached in `bytecode/runner.dart.md`).
- C2, C3 → `rf-dart-final-field-class-to-csharp-getonly-class` (cached in `compiler/token.dart.md`, `bytecode/opcodes.dart.md`); `Index:long` via `rf-dart-int-to-csharp-long-width` (cached in `opcodes.dart.md`).
- C4, C5 → `rf-dart-pattern-match-cascade-dispatch-to-csharp-is-pattern-cascade` (cached in `bytecode/runner.dart.md`).
- C6, C7 → `rf-dart-iterable-where-to-linq` (cached in `analysis/analysis_phase.dart.md`).
- C8 → `rf-dart-runtimetype-in-interpolation-to-csharp-gettype-name` (first occurrence in feature 018 corpus; resolved from authoritative dart.dev `Object.runtimeType` + Microsoft Learn `System.Type.Name`; cached for future reuse).

All eight constructs authoritatively grounded on both sides (Dart spec + Microsoft Learn) per the convspec's "Rationale and research provenance" section; no escalation required.

## 5. Consistency Pass

- C1 → fixed — derived from convspec construct `dart.import_directive.package_internal_to_using_namespace` + `rf-dart-import-relative-to-csharp-using-namespace` cache (`bytecode/runner.dart.md` lines 77–115).
- C2 → fixed — derived from convspec construct `dart.data_class.immutable_final_fields_positional_ctor_with_optional_positional` + `rf-dart-final-field-class-to-csharp-getonly-class` cache + `rf-dart-int-to-csharp-long-width` cache (`opcodes.dart.md`).
- C3 → fixed — derived from convspec construct `dart.data_class.list_field_with_isempty_getter_idiomatic` + `rf-dart-final-field-class-to-csharp-getonly-class` cache.
- C4 → fixed — derived from convspec construct `dart.local_function_typed_predicate_closure_over_is_pattern_cascade` + `rf-dart-pattern-match-cascade-dispatch-to-csharp-is-pattern-cascade` cache (`bytecode/runner.dart.md`).
- C5 → fixed — derived from convspec construct `dart.classical_for_loop_index_into_list_dynamic_with_is_dispatch_and_state_machine` + `rf-dart-pattern-match-cascade-dispatch-to-csharp-is-pattern-cascade` cache; ARM ORDER verified against Dart source lines 43–135 (Label early-continue first, post-SuspendEnd second, !inClause third, head/guard fourth, body fifth).
- C6 → fixed — derived from convspec construct `dart.list_literal_typed_growable_with_add` + `rf-dart-iterable-where-to-linq` cache (`analysis_phase.dart.md`).
- C7 → fixed — derived from convspec construct `dart.iterable_wheretype_count_to_csharp_linq_oftype_count` + `rf-dart-iterable-where-to-linq` cache; Microsoft Learn `Enumerable.OfType<TResult>` cited in convspec rationale.
- C8 → fixed — derived from convspec construct `dart.private_helper.runtimetype_in_interpolation_to_csharp_gettype_name` + new `rf-dart-runtimetype-in-interpolation-to-csharp-gettype-name` (dart.dev `Object.runtimeType` + Microsoft Learn `System.Type.Name`).

All decisions cross-checked against `CLAUDE.md` policy (CompileError-style verbatim error-class names not applicable here — no Error subclasses; no Stream/Future/async; no isolate primitives) and convspec `escalations: []`. No tensions surfaced.

## 6. Escalations

None.
