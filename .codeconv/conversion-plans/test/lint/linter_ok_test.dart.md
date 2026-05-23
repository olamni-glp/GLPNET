---
path: test/lint/linter_ok_test.dart
cycle_group_id: 132
scc_siblings: []
generated_at: 2026-05-21T16:34:18Z
source_sha256: 75029d51648451ea8ae4049fe8a1f3e64fc07635122432564fdc4c3c45ce99da
schema_version: 1
---

# Conversion Plan: test/lint/linter_ok_test.dart

## 1. Source Analysis

The Dart source file `test/lint/linter_ok_test.dart` (25 lines, sha256
`75029d51648451ea8ae4049fe8a1f3e64fc07635122432564fdc4c3c45ce99da`) is a
single-test "happy path" `package:test` file that exercises the linter on
a hand-assembled valid bytecode program. Inventory of constructs (in source
order):

- **Imports (3)**:
  - `import 'package:test/test.dart';` — the Dart test framework surface.
  - `import 'package:glp_runtime/bytecode/asm.dart';` — the BC static-helper
    namespace and the `BytecodeProgram` / `Op` type surface.
  - `import 'package:glp_runtime/lint/linter.dart';` — the `Linter`,
    `LintResult`, `LintIssue` surface.
- **Top-level `void main()`** containing exactly ONE `test(...)` call —
  no `group(...)`, no `setUp` / `tearDown`, no `skip:`, no shared state.
- **One `test(name, body)` call** with name string `'Valid shape:
  head/guards only pre-commit; single SuspendEnd after clauses'` and a
  synchronous `void`-returning closure.
- **Inside the test body**:
  - **`final prog = BC.prog([ ... ])`** — a single-assignment local
    holding a `BytecodeProgram` returned by the `BC.prog` factory. The
    list literal contains 10 `Op` instances built via the BC short-form
    factories: `BC.L('C1')`, `BC.TRY()`, `BC.R(1)`, `BC.U('C2')`,
    `BC.L('C2')`, `BC.TRY()`, `BC.R(2)`, `BC.U('END')`, `BC.L('END')`,
    `BC.SUSP()`.
  - **`final res = Linter().lint(prog)`** — a single-assignment local
    holding a `LintResult` returned by `Linter.lint(BytecodeProgram)`.
    The Dart-2-optional `new` is omitted in the source.
  - **`expect(res.ok, isTrue, reason: res.issues.join('\n'))`** — a
    boolean-shape `expect` with the `isTrue` matcher and a COMPUTED
    `reason:` argument (the joined list of issues as a fallback failure
    message).
- **No async / `Future` / `Stream` / isolate / `Completer` / `Timer`
  surface** anywhere in the file.
- **No `late`, `mixin`, `extension`, sealed/abstract, generics-at-callsite,
  bitwise/shift, or null-safety nuance** anywhere in the file.

The file's purpose is to assert the linter's "happy path" on a tiny but
structurally valid bytecode program (label / try / reader / unify_clause
pairs terminated by a single `SuspendEnd`).

## 2. Dart → C#/.NET Conversion Plan

Mirrors the convspec (RATIFIED) at
`.codeconv/conversion-specs/test/lint/linter_ok_test.dart.md` one-for-one.

- **`dart.package_test.import_directive`**
  `import 'package:test/test.dart';` → `using Xunit;` at file scope
  (reuse of the batch-wide `rf-dart-package-test-to-dotnet-xunit` finding;
  no re-research per FR-012 / SC-007). The `.csproj`-level NuGet wiring
  (xunit, xunit.runner.visualstudio, Microsoft.NET.Test.Sdk) is OUT OF
  SCOPE for this per-file artefact (langpair-level emission concern).

- **`dart.internal_package_import.same_package`** (two imports, do NOT
  collapse)
  `import 'package:glp_runtime/bytecode/asm.dart';` →
  `using <RootNs>.Bytecode;` (namespace owned by
  `.codeconv/conversion-specs/lib/bytecode/asm.dart.md`).
  `import 'package:glp_runtime/lint/linter.dart';` →
  `using <RootNs>.Lint;` (namespace owned by
  `.codeconv/conversion-specs/lib/lint/linter.dart.md`).
  The two imports target DIFFERENT converted namespaces (`Bytecode` vs
  `Lint`) — one `using` per namespace, no collapse. NO `using static
  <RootNs>.Bytecode.BC;` — the source explicitly writes `BC.L(...)` and
  the spec preserves source shape one-for-one. Visibility: `BC`,
  `Linter`, `LintResult` are all library-public Dart (no leading `_`)
  → `public` on the C# side per the SUT specs.

- **`dart.test_file.void_main_as_test_registration_root`**
  `void main() { test('Valid shape: ...', () { ... }); }` → drop
  `main()` entirely; emit `namespace <RootNs>.Test.Lint { public class
  LinterOkTest { [Fact(DisplayName = "Valid shape: head/guards only
  pre-commit; single SuspendEnd after clauses")] public void
  ValidShape_HeadGuardsOnlyPreCommit_SingleSuspendEndAfterClauses() {
  ... } } }`. xUnit discovery is attribute-driven (reflection over
  `[Fact]`, fresh instance per `[Fact]` per xunit.net "Shared Context
  between Tests") — registration-via-`main` has no equivalent and is
  dropped. The Dart test name's colons, slash, semicolon, and spaces are
  stripped or replaced with `_` to form an identifier-safe method name;
  `[Fact(DisplayName = ...)]` preserves the original human-readable
  reporting name verbatim. Body is synchronous `void` (no `async Task`)
  — no Future / Stream / isolate surface in the file.

- **`dart.local_var.final_typed_static_factory_invocation`**
  `final prog = BC.prog([ BC.L('C1'), BC.TRY(), BC.R(1), BC.U('C2'),
  BC.L('C2'), BC.TRY(), BC.R(2), BC.U('END'), BC.L('END'), BC.SUSP(),
  ]);` → `var prog = BC.prog(new List<Op> { BC.L("C1"), BC.TRY(),
  BC.R(1), BC.U("C2"), BC.L("C2"), BC.TRY(), BC.R(2), BC.U("END"),
  BC.L("END"), BC.SUSP(), });`. Dart `final` on a never-reassigned local
  → C# `var` (NOT `readonly` — fields only; NOT `const` — the RHS is a
  runtime construction). Dart list literal `[ ... ]` of `Op` → C# `new
  List<Op> { ... }` (collection-initialiser, assignable to the
  `IReadOnlyList<Op>`/`List<Op>` parameter of `BC.Prog` per
  asm.dart.md). The BC short-form factories (`L`, `TRY`, `R`, `U`,
  `SUSP`, `prog`) are preserved verbatim per asm.dart.md's recorded
  UPPERCASE-alias decision. Integer literals `1` and `2` bind to the
  converted `BC.R` parameter type recorded in asm.dart.md (`long` per
  `rf-dart-int-to-csharp-long-width` if pinned, else `int`); emit as
  `1L`/`2L` for unambiguous binding to a `long` parameter, else as
  `1`/`2`. String literals `'C1'`/`'C2'`/`'END'` (Dart single quotes)
  → `"C1"`/`"C2"`/`"END"` (C# double quotes; identical content).
  Trailing comma in the list literal is preserved (legal in both
  languages).

- **`dart.local_var.final_method_call_result`**
  `final res = Linter().lint(prog);` → `var res = new Linter().Lint(prog);`.
  Dart-2-optional `new` is omitted in the source; C# requires `new`.
  Dart instance method `lint` → C# `Lint` per linter.dart.md's recorded
  camelCase→PascalCase mapping (`Linter.lint(BytecodeProgram) →
  LintResult` → `Linter.Lint(BytecodeProgram) → LintResult`). The
  `prog` argument flows in unchanged (type `BytecodeProgram` per
  asm.dart.md's `BC.prog` return type). The two-step
  construct-then-call shape is preserved faithful to the source (a
  single-line `new Linter().Lint(prog)` chain is an alternative; the
  spec keeps two steps).

- **`dart.package_test.expect_value_boolean_matcher_with_reason_computed`**
  `expect(res.ok, isTrue, reason: res.issues.join('\n'));` →
  `Assert.True(res.Ok, string.Join("\n", res.Issues));`. xUnit's
  `Assert.True(bool? condition, string? userMessage)` overload mirrors
  Dart's `expect(actual, isTrue, reason: msg)` exactly — the
  `userMessage` surfaces in the failure-message header the same way
  Dart's `reason:` does. Matcher routing: `isTrue` → `Assert.True` (per
  smoke_test.dart's recorded matcher table). The `reason:` text is a
  COMPUTED string (not a literal) and is evaluated EAGERLY in both
  languages — positional argument, not a deferred lambda — so the join
  runs whether or not the assertion fails (identical Dart behaviour).
  Strict-boolean semantics match (both `isTrue` and `Assert.True(bool,
  ...)` assert strict `true`). Exception on failure: Dart
  `TestFailure` ↔ xUnit `Xunit.Sdk.TrueException` (subclass of
  `XunitException`) — runner-caught, equivalent.

- **`dart.iterable_join.list_of_string_to_string_with_separator`**
  `res.issues.join('\n')` → `string.Join("\n", res.Issues)`. Dart
  `Iterable<E>.join([String separator = ''])` calls `Object.toString()`
  per element and concatenates with `separator`; .NET `String.Join<T>
  (string? separator, IEnumerable<T> values)` calls `Object.ToString()`
  per element. The argument-order is FLIPPED — Dart is receiver-method
  (`xs.join(sep)`), C# is static-method (`string.Join(sep, xs)`) — a
  load-bearing footgun recorded for any future test that joins a
  collection. Newline separator `'\n'` → `"\n"` (same escape sequence in
  both languages; no `\r\n` introduced). Element type is `LintIssue` per
  linter.dart.md; `LintIssue.ToString()` is pinned by linter.dart.md as
  an override emitting `[<code>] @op#<index>: <message>` — observably
  identical joined string. Null-element divergence (Dart emits `"null"`,
  .NET emits `""`) does NOT fire here (linter.dart.md never adds null
  to `issues`). LINQ alternative `Aggregate("", (a, b) => a + "\n" + b)`
  REJECTED (O(n²) string concatenation, semantically equivalent but
  degenerate).

- **`dart.member_access.property_chain_through_result`**
  `res.ok` → `res.Ok` (computed get-only `bool` property; `bool get ok
  => issues.isEmpty` → `public bool Ok => Issues.Count == 0` per
  linter.dart.md's `list_field_with_isempty_getter_idiomatic` row).
  `res.issues` → `res.Issues` (get-only `List<LintIssue>` property,
  no defensive copy on either side, mutable list contents preserved —
  but this test only READS through it). Dart `lowerCamelCase` field /
  getter → C# `PascalCase` property per Microsoft C# Coding
  Conventions and linter.dart.md's recorded mapping.

## 3. Decomposed Task Units

- T1 — Emit `using Xunit;` (drop `import 'package:test/test.dart';`).
- T2 — Emit `using <RootNs>.Bytecode;` (drop `import
  'package:glp_runtime/bytecode/asm.dart';`).
- T3 — Emit `using <RootNs>.Lint;` (drop `import
  'package:glp_runtime/lint/linter.dart';`).
- T4 — Emit `namespace <RootNs>.Test.Lint { ... }` (bracket form, mirrors
  test/lint path).
- T5 — Emit `public class LinterOkTest { ... }` (single test class, no
  base, no constructor / Dispose — no shared state).
- T6 — Emit `[Fact(DisplayName = "Valid shape: head/guards only
  pre-commit; single SuspendEnd after clauses")] public void
  ValidShape_HeadGuardsOnlyPreCommit_SingleSuspendEndAfterClauses() {
  ... }` (lift the one `test(...)` call, drop `main()`).
- T7 — Emit `var prog = BC.prog(new List<Op> { BC.L("C1"), BC.TRY(),
  BC.R(1), BC.U("C2"), BC.L("C2"), BC.TRY(), BC.R(2), BC.U("END"),
  BC.L("END"), BC.SUSP(), });` (Dart `final` + list literal of `Op`
  → C# `var` + collection-initialiser; BC short-form factories
  preserved per asm.dart.md; integer-literal width per asm.dart.md's
  `BC.R` parameter type).
- T8 — Emit `var res = new Linter().Lint(prog);` (Dart `final` + omitted
  `new` + camelCase method → C# `var` + explicit `new` + PascalCase
  method per linter.dart.md).
- T9 — Emit `Assert.True(res.Ok, string.Join("\n", res.Issues));`
  (`isTrue` matcher → `Assert.True`; `reason:` named → `userMessage`
  positional; `string.Join` static + flipped argument order; PascalCased
  `res.Ok` / `res.Issues` per linter.dart.md).
- T10 — Confirm NO equivalent of `void main()` is emitted (xUnit
  discovery is attribute-driven; registration-via-main is dropped).

## 4. Research Findings

none required — every construct in this file REUSES an authoritative
finding already recorded by sibling specs in this batch
(smoke_test.dart, fairness_26_test.dart, glp_runtime_test.dart,
test/heap/* siblings) or by the SUT specs at
`.codeconv/conversion-specs/lib/bytecode/asm.dart.md` and
`.codeconv/conversion-specs/lib/lint/linter.dart.md` per FR-012 /
SC-007 KB-reuse policy. The findings carried forward verbatim are:

- `rf-dart-package-test-to-dotnet-xunit` — framework choice (xUnit).
- `rf-dart-internal-package-import-to-csharp-using` — one `using` per
  converted namespace (no collapse across `Bytecode` and `Lint`).
- `rf-dart-test-main-to-xunit-class-with-facts` — drop `main()`, lift
  `test()` calls into `[Fact]` methods on a class mirroring the file
  name; `[Fact(DisplayName = ...)]` preserves the original test name.
- `rf-dart-final-local-to-csharp-var` — `final <local> = <expr>` →
  `var <local> = <expr>` (twice in this file).
- `rf-dart-expect-isTrue-with-reason-to-xunit-assert-true` — `expect(x,
  isTrue, reason: msg)` → `Assert.True(x, msg)` via the
  `(bool?, string?)` overload; eager evaluation of the positional
  message argument identical on both sides.
- `rf-dart-iterable-join-to-csharp-string-join` — `xs.join(sep)` →
  `string.Join(sep, xs)`; receiver-vs-static + separator-vs-collection
  argument-order flip recorded; null-element divergence noted but not
  exercised here.
- `rf-dart-final-field-class-to-csharp-getonly-class` — `lowerCamelCase`
  field / getter → `PascalCase` property; the
  `list_field_with_isempty_getter_idiomatic` row maps `bool get ok =>
  issues.isEmpty` → `public bool Ok => Issues.Count == 0` and
  `final List<LintIssue> issues` → `public List<LintIssue> Issues
  { get; }`.

## 5. Consistency Pass

- T1: fixed — derived from convspec `dart.package_test.import_directive`
  + `rf-dart-package-test-to-dotnet-xunit` (REUSED from
  smoke_test.dart.md per FR-012 / SC-007).
- T2: fixed — derived from convspec
  `dart.internal_package_import.same_package` +
  `rf-dart-internal-package-import-to-csharp-using` + SUT spec
  `.codeconv/conversion-specs/lib/bytecode/asm.dart.md`.
- T3: fixed — derived from convspec
  `dart.internal_package_import.same_package` +
  `rf-dart-internal-package-import-to-csharp-using` + SUT spec
  `.codeconv/conversion-specs/lib/lint/linter.dart.md`.
- T4: fixed — derived from convspec
  `dart.test_file.void_main_as_test_registration_root` +
  `rf-dart-test-main-to-xunit-class-with-facts` (REUSED from
  smoke_test.dart.md per FR-012 / SC-007).
- T5: fixed — derived from convspec
  `dart.test_file.void_main_as_test_registration_root` +
  `rf-dart-test-main-to-xunit-class-with-facts` (REUSED; no
  constructor / Dispose because no shared state per convspec nuance).
- T6: fixed — derived from convspec
  `dart.test_file.void_main_as_test_registration_root` +
  `rf-dart-test-main-to-xunit-class-with-facts`; identifier mangling
  rule and `[Fact(DisplayName = ...)]` preservation taken verbatim from
  the convspec target_decision.
- T7: fixed — derived from convspec
  `dart.local_var.final_typed_static_factory_invocation` +
  `rf-dart-final-local-to-csharp-var` + SUT spec
  `.codeconv/conversion-specs/lib/bytecode/asm.dart.md` (BC factory
  surface, `Op` element type, `BC.R` integer-parameter width).
- T8: fixed — derived from convspec
  `dart.local_var.final_method_call_result` +
  `rf-dart-final-local-to-csharp-var` + SUT spec
  `.codeconv/conversion-specs/lib/lint/linter.dart.md` (PascalCase
  `Lint` method name).
- T9: fixed — derived from convspec
  `dart.package_test.expect_value_boolean_matcher_with_reason_computed`
  + `rf-dart-expect-isTrue-with-reason-to-xunit-assert-true` +
  `dart.iterable_join.list_of_string_to_string_with_separator` +
  `rf-dart-iterable-join-to-csharp-string-join` +
  `dart.member_access.property_chain_through_result` +
  `rf-dart-final-field-class-to-csharp-getonly-class`.
- T10: fixed — derived from convspec
  `dart.test_file.void_main_as_test_registration_root` nuance ("The
  Dart `main()` registration pass has no xUnit equivalent and is
  dropped").

## 6. Escalations

None.
