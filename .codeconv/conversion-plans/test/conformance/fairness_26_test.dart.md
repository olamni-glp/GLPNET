---
path: test/conformance/fairness_26_test.dart
cycle_group_id: 119
scc_siblings: []
generated_at: 2026-05-21T16:24:53Z
source_sha256: bb89ae3cfa3df92ffb3305f90fc80250bc658914cb53c211c49157ce5c469a6e
schema_version: 1
---

# Conversion Plan: test/conformance/fairness_26_test.dart

## 1. Source Analysis

Direct inspection of `glp_runtime_net/test/conformance/fairness_26_test.dart`
(26 lines, sha256 `bb89ae3cfa3df92ffb3305f90fc80250bc658914cb53c211c49157ce5c469a6e`)
yields the following surface:

- **Imports (3)**:
  - `package:test/test.dart` — Dart test framework.
  - `package:glp_runtime/runtime/runtime.dart` — SUT (`GlpRuntime` class).
  - `package:glp_runtime/runtime/machine_state.dart` — SUT (`GoalId` typedef).
- **Top-level**: a single `void main()` that registers exactly one `test(...)`
  call with the name `'26-step tail recursion budget yields and resets'`.
- **Test body locals (2)**:
  - `final rt = GlpRuntime();` — single-assignment local, RHS-typed via
    constructor invocation.
  - `const GoalId g = 123;` — compile-time constant local with explicit
    `GoalId` type and integer literal initialiser.
- **Control flow (1)**: a C-style `for (var i = 0; i < 25; i++)` loop, body
  contains one `tailReduce` call and one `expect(..., isFalse, reason: ...)`
  assertion with a `${i+1}` string-interpolation expression.
- **SUT calls (4 distinct sites)**: `rt.tailReduce(g)` (4 occurrences — one
  inside the loop, three outside), `rt.budgetOf(g)` (2 occurrences).
- **Assertions (6)**: in order — `expect(y, isFalse, reason: '...')` inside
  loop, `expect(y26, isTrue, reason: '...')`, `expect(rt.budgetOf(g), 26,
  reason: '...')`, `expect(y1, isFalse)` (bare, no `reason:`),
  `expect(rt.budgetOf(g), 25)` (bare, no `reason:`).
- **Async surface**: NONE. No `Future`, `async`, `await`, `Stream`,
  `Completer`, `Timer`, isolate, mixin, extension, generic, sealed,
  bitwise, null-safety operator. Synchronous `void main()` with a
  synchronous `test(...)` body.
- **Visibility**: all imported identifiers are library-public on the Dart
  side (no leading underscore).
- **Behavioural contract exercised**: tail-recursion budget — 25 calls
  return `false` (no yield), the 26th returns `true` (yield) and resets
  the budget to 26, the 27th call decrements to 25 and returns `false`.

## 2. Dart → C#/.NET Conversion Plan

Each construct lifts verbatim from the convspec at
`.codeconv/conversion-specs/test/conformance/fairness_26_test.dart.md`.

- **C1. `import 'package:test/test.dart';`** → `using Xunit;` at file
  scope. KB-reused finding `rf-dart-package-test-to-dotnet-xunit` per
  FR-012 / SC-007 (xUnit framework, settled batch-wide by
  smoke_test.dart). `.csproj` NuGet wiring (xunit,
  xunit.runner.visualstudio, Microsoft.NET.Test.Sdk) is OUT OF SCOPE
  for this per-file artifact.

- **C2. `import 'package:glp_runtime/runtime/runtime.dart';` +
  `import 'package:glp_runtime/runtime/machine_state.dart';`** →
  collapse into ONE `using <RootNs>.Runtime;` directive. C# `using` is
  per-namespace; both Dart libraries lift into the SUT-decided
  `Runtime` sub-namespace. Brings `GlpRuntime` and `GoalId` into scope.
  Exact `<RootNs>` identifier owned by the SUT specs at
  `.codeconv/conversion-specs/lib/runtime/runtime.dart.md` and
  `lib/runtime/machine_state.dart.md`.

- **C3. `void main() { test('...', () { ... }); }`** → eliminate `main()`
  entirely. Lift the one `test(...)` into a single `[Fact]`-attributed
  public instance method on `public class Fairness26Test` (file-name
  mirror `fairness_26_test.dart` ⇒ `Fairness26Test.cs`). Method
  identifier: `Step26TailRecursionBudgetYieldsAndResets` (PascalCased
  with mandatory `Step` prefix to satisfy C# identifier-cannot-start-
  with-digit rule per C# language specification lexical-structure §
  Identifiers). Display name preserved via `[Fact(DisplayName =
  "26-step tail recursion budget yields and resets")]`. Synchronous
  `void` return (no `async Task` — no async surface in source).

- **C4. `final rt = GlpRuntime();`** → `var rt = new GlpRuntime();`. Dart
  `final` single-assignment local maps to C# `var` (C# has no method-
  local `readonly`; `const` requires a compile-time constant which a
  constructor call is not). C# requires the `new` operator on
  constructor invocation. `GlpRuntime` reachable via the file-level
  `using` of C2.

- **C5. `const GoalId g = 123;`** → `const GoalId g = 123;`. C# `const`
  on a method local is supported and semantically equivalent to Dart
  `const` on a local with a literal initialiser. `GoalId` type-alias
  shape owned by the machine_state SUT spec; if SUT spec converts to
  `long`, literal becomes `123L`.

- **C6. `for (var i = 0; i < 25; i++) { ... }`** → verbatim transcription
  `for (var i = 0; i < 25; i++) { ... }`. Dart and C# share C-style for-
  loop syntax with identical semantics; `var i = 0` infers `int` in
  both languages.

- **C7. `'should not yield on step ${i+1}'`** → `$"should not yield on
  step {i + 1}"`. Dart `${expr}` ⇒ C# `{expr}` inside a `$"..."`
  literal. Single quotes ⇒ double quotes (C# string literal
  requirement). No brace escaping needed (no literal `{`/`}` in
  source).

- **C8. `expect(y, isFalse, reason: '...')`** (inside loop) →
  `Assert.False(y, $"should not yield on step {i + 1}");`. Strict-
  boolean match; `reason:` ⇒ `userMessage` overload of `Assert.False`.

- **C9. `final y26 = rt.tailReduce(g);`** → `var y26 = rt.TailReduce(g);`.
  Same `final` ⇒ `var` rule as C4; Dart `tailReduce` ⇒ C# `TailReduce`
  via SUT-spec PascalCase rule.

- **C10. `expect(y26, isTrue, reason: 'should yield on step 26')`** →
  `Assert.True(y26, "should yield on step 26");`. `isTrue` ⇒
  `Assert.True`; `reason:` ⇒ `userMessage` overload.

- **C11. `expect(rt.budgetOf(g), 26, reason: 'budget resets after
  yielding')`** → `Assert.Equal(26, rt.BudgetOf(g)); // budget resets
  after yielding`. `Assert.Equal<T>` is EXPECTED-FIRST (opposite of
  Dart's ACTUAL-FIRST `expect`). xUnit `Assert.Equal<T>` has NO
  `userMessage` overload — `reason:` text routed to an inline `// ...`
  comment so author rationale survives review. Integer width
  (`26`/`26L`) tracks the SUT spec's `BudgetOf` return-type decision.

- **C12. `final y1 = rt.tailReduce(g);`** → `var y1 = rt.TailReduce(g);`.
  Same `final` ⇒ `var` + PascalCase rules.

- **C13. `expect(y1, isFalse);`** (no `reason:`) → `Assert.False(y1);`.
  Bare-form `isFalse` ⇒ bare `Assert.False`; no `userMessage`
  argument.

- **C14. `expect(rt.budgetOf(g), 25);`** (no `reason:`) →
  `Assert.Equal(25, rt.BudgetOf(g));`. Bare-form equals ⇒ bare
  EXPECTED-FIRST `Assert.Equal`; no inline-comment emission (source
  supplied no `reason:`).

Conversion-units (mirror convspec `conversion_units`):

1. `using Xunit;`
2. `using <RootNs>.Runtime;` (single collapsed using)
3. `public class Fairness26Test { ... }` (file-name-mirroring test
   class, no base class)
4. `[Fact(DisplayName = "26-step tail recursion budget yields and
   resets")] public void Step26TailRecursionBudgetYieldsAndResets() {
   ... }` (one Fact per Dart `test()`; DisplayName preserves
   human-readable name; `Step` prefix resolves leading-digit
   constraint)
5. method body line 1: `var rt = new GlpRuntime();`
6. method body line 2: `const GoalId g = 123;` (integer width per SUT
   spec)
7. method body lines 3–6: `for (var i = 0; i < 25; i++) { var y =
   rt.TailReduce(g); Assert.False(y, $"should not yield on step {i +
   1}"); }`
8. method body line 7: `var y26 = rt.TailReduce(g);`
9. method body line 8: `Assert.True(y26, "should yield on step 26");`
10. method body line 9: `Assert.Equal(26, rt.BudgetOf(g)); // budget
    resets after yielding`
11. method body line 10: `var y1 = rt.TailReduce(g);`
12. method body line 11: `Assert.False(y1);`
13. method body line 12: `Assert.Equal(25, rt.BudgetOf(g));`
14. NO equivalent of Dart's `void main()` — xUnit discovery is
    attribute-driven; registration-via-main dropped entirely.

## 3. Decomposed Task Units

- T1. Emit file-level `using Xunit;` directive — done.
- T2. Emit collapsed `using <RootNs>.Runtime;` directive (SUT-spec-owned
  namespace identifier) — done.
- T3. Emit `public class Fairness26Test` declaration mirroring file
  name — done.
- T4. Emit `[Fact(DisplayName = "26-step tail recursion budget yields
  and resets")] public void Step26TailRecursionBudgetYieldsAndResets()`
  method signature with `Step` prefix resolving leading-digit
  constraint — done.
- T5. Emit `var rt = new GlpRuntime();` for Dart `final rt =
  GlpRuntime();` — done.
- T6. Emit `const GoalId g = 123;` (integer width per machine_state SUT
  spec) for Dart `const GoalId g = 123;` — done.
- T7. Emit C-style `for (var i = 0; i < 25; i++) { ... }` verbatim —
  done.
- T8. Emit loop-body `var y = rt.TailReduce(g);` (PascalCase per SUT
  spec) — done.
- T9. Emit loop-body `Assert.False(y, $"should not yield on step {i +
  1}");` with C# `$"..."` interpolation — done.
- T10. Emit `var y26 = rt.TailReduce(g);` — done.
- T11. Emit `Assert.True(y26, "should yield on step 26");` with
  `userMessage` overload — done.
- T12. Emit `Assert.Equal(26, rt.BudgetOf(g));` EXPECTED-FIRST plus
  inline `// budget resets after yielding` comment routing the lossy
  `reason:` — done.
- T13. Emit `var y1 = rt.TailReduce(g);` — done.
- T14. Emit bare `Assert.False(y1);` (no `userMessage`) — done.
- T15. Emit bare `Assert.Equal(25, rt.BudgetOf(g));` EXPECTED-FIRST
  (no inline comment, no `reason:` in source) — done.
- T16. Drop Dart `void main()` entirely (xUnit attribute-driven
  discovery) — done.

## 4. Research Findings

None required — every construct's target is verbatim-derived from the
ratified convspec (which records its own research findings:
`rf-dart-package-test-to-dotnet-xunit`,
`rf-dart-internal-package-import-to-csharp-using`,
`rf-dart-test-main-to-xunit-class-with-facts`,
`rf-dart-final-local-to-csharp-var`,
`rf-dart-const-local-typed-int-to-csharp-const`,
`rf-dart-c-style-for-loop-to-csharp-verbatim`,
`rf-dart-string-interpolation-to-csharp-dollar-string`,
`rf-dart-expect-isFalse-with-reason-to-xunit-assert-false`,
`rf-dart-expect-isTrue-isFalse-bare-to-xunit-assert`,
`rf-dart-expect-bare-value-int-to-xunit-assert-equal`) and KB-reuses
the framework-choice and matcher-routing-table rows recorded
batch-wide by `smoke_test.dart`, `glp_runtime_test.dart`, and the
`test/heap/` siblings per FR-012 / SC-007.

## 5. Consistency Pass

- C1 (`using Xunit;`) — fixed; derived from convspec construct
  `dart.package_test.import_directive` (RF
  `rf-dart-package-test-to-dotnet-xunit`).
- C2 (collapsed `using <RootNs>.Runtime;`) — fixed; derived from
  convspec construct `dart.internal_package_import.same_package` (RF
  `rf-dart-internal-package-import-to-csharp-using`).
- C3 (`public class Fairness26Test` + `[Fact(DisplayName=...)]
  Step26TailRecursionBudgetYieldsAndResets`) — fixed; derived from
  convspec construct
  `dart.test_file.void_main_as_test_registration_root` (RF
  `rf-dart-test-main-to-xunit-class-with-facts`); leading-digit
  prefix `Step` derived from convspec nuance + C# language
  specification lexical-structure cite.
- C4 (`var rt = new GlpRuntime();`) — fixed; derived from convspec
  construct `dart.local_var.final_typed_constructor_invocation` (RF
  `rf-dart-final-local-to-csharp-var`).
- C5 (`const GoalId g = 123;`) — fixed; derived from convspec
  construct `dart.const_local.typed_int_literal` (RF
  `rf-dart-const-local-typed-int-to-csharp-const`); integer-width
  defers to SUT machine_state spec.
- C6 (verbatim C-style for-loop) — fixed; derived from convspec
  construct `dart.for_loop.c_style_int_index` (RF
  `rf-dart-c-style-for-loop-to-csharp-verbatim`).
- C7 (`$"...{i + 1}"`) — fixed; derived from convspec construct
  `dart.string.interpolation.simple_expression` (RF
  `rf-dart-string-interpolation-to-csharp-dollar-string`).
- C8 (`Assert.False(y, $"...");`) — fixed; derived from convspec
  construct `dart.package_test.expect_value_boolean_matcher` (RF
  `rf-dart-expect-isFalse-with-reason-to-xunit-assert-false`).
- C9, C12 (`var y26/y1 = rt.TailReduce(g);`) — fixed; derived from
  convspec construct
  `dart.local_var.final_typed_constructor_invocation` (RF
  `rf-dart-final-local-to-csharp-var`) plus SUT-spec PascalCase
  carry-forward.
- C10 (`Assert.True(y26, "should yield on step 26");`) — fixed;
  derived from convspec construct
  `dart.package_test.expect_value_boolean_matcher_no_reason` (RF
  `rf-dart-expect-isTrue-isFalse-bare-to-xunit-assert`).
- C11 (`Assert.Equal(26, rt.BudgetOf(g));` + inline `// budget resets
  after yielding`) — fixed; derived from convspec construct
  `dart.package_test.expect_value_equals_matcher_with_reason` (RF
  `rf-dart-expect-bare-value-int-to-xunit-assert-equal`);
  `reason:`-to-inline-comment lossiness routing explicitly recorded in
  the convspec nuance.
- C13 (`Assert.False(y1);` bare) — fixed; derived from convspec
  construct `dart.package_test.expect_value_boolean_matcher_no_reason`
  (RF `rf-dart-expect-isTrue-isFalse-bare-to-xunit-assert`).
- C14 (`Assert.Equal(25, rt.BudgetOf(g));` bare) — fixed; derived
  from convspec construct
  `dart.package_test.expect_value_equals_matcher_bare_with_reason`
  (RF `rf-dart-expect-bare-value-int-to-xunit-assert-equal`).
- Tasks T1–T16 — fixed; each task is a 1-to-1 emission of a convspec
  conversion-unit row.
- Out-of-scope items (`.csproj` NuGet wiring; exact `<RootNs>`
  identifier; integer-width choice; method-name PascalCase) — fixed;
  derived from explicit convspec deference to langpair-level / SUT
  specs.

## 6. Escalations

None.
