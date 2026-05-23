---
path: test/lint/linter_body_precommit_test.dart
cycle_group_id: 131
scc_siblings: []
generated_at: 2026-05-21T16:34:12Z
source_sha256: ac6c501dfe96836ca19e73d1aab4d30b935aee1700c2328d4769354d872d5bc2
schema_version: 1
---

# Conversion Plan: test/lint/linter_body_precommit_test.dart

## 1. Source Analysis

Direct inspection of `glp_runtime_net/test/lint/linter_body_precommit_test.dart` (22 lines, sha256 `ac6c501d…d872d5bc2`):

- **Imports (3)**:
  1. `import 'package:test/test.dart';` — external Dart test framework.
  2. `import 'package:glp_runtime/bytecode/asm.dart';` — internal package import; brings in the `BC` static assembler facade.
  3. `import 'package:glp_runtime/lint/linter.dart';` — internal package import; brings in `Linter`, `LintResult`, `LintIssue`.
- **Top-level entry point**: `void main()` containing exactly one `test(...)` registration.
- **Single test**: `test('Body op before commit is flagged', () { ... })`.
- **Test body (lines 7–20)**:
  - `final prog = BC.prog([...]);` — list literal of seven static-factory calls on `BC` (`BC.L('C1')`, `BC.TRY()`, `BC.W(10)`, `BC.BCONST(10, 42)`, `BC.U('END')`, `BC.L('END')`, `BC.SUSP()`).
  - `// body op before COMMIT (illegal)` — load-bearing line comment above `BC.BCONST(10, 42)` documenting deliberate-invalid construction.
  - `final res = Linter().lint(prog);` — implicit-new constructor invocation chained with method call.
  - `expect(res.ok, isFalse);` — bare-value matcher assertion.
  - `expect(res.issues.any((e) => e.code == 'BODY_BEFORE_COMMIT'), isTrue, reason: res.issues.join('\n'));` — composed assertion: `Iterable.any` predicate + `isTrue` matcher + string `reason:` built lazily via `Iterable.join('\n')`.
- **No** async / `Future` / `Stream` / isolate / `Completer` / `Timer` / `late` / `mixin` / `extension` / generics / nullable surface.
- **Cross-file dependencies** (per convspec): two sub-namespaces (`Bytecode`, `Lint`) of the converted assembly; SUT specs at `lib/bytecode/asm.dart.md`, `lib/bytecode/opcodes.dart.md`, `lib/lint/linter.dart.md` own opcode/linter shape.

## 2. Dart → C#/.NET Conversion Plan

Each construct mirrors the convspec verbatim (FR-012 / SC-007 KB reuse — six REUSED idioms + one NEW composed finding). The `→` glyph is U+2192.

- **`import 'package:test/test.dart';` → `using Xunit;`** (file-level using directive). Reuses `rf-dart-package-test-to-dotnet-xunit` from `smoke_test.dart` (authoritative: Microsoft Learn `unit-testing-csharp-with-xunit`, xunit.net, pub.dev/package:test). No `ITestOutputHelper` injection (file has zero `print` calls). `.csproj` test-framework references out of scope (langpair-level emission).

- **`import 'package:glp_runtime/bytecode/asm.dart';` + `import 'package:glp_runtime/lint/linter.dart';` → `using <RootNs>.Bytecode;` + `using <RootNs>.Lint;`** (two file-level using directives, no collapse — each resolves to a distinct sub-namespace per SUT specs). Reuses `rf-dart-internal-package-import-to-csharp-using`. `BC` is qualified at call sites so no `using static BC;` is needed. Test-assembly `.csproj` reference to converted-SUT assembly out of scope.

- **`void main() { test('Body op before commit is flagged', () { ... }); }` → `public class LinterBodyPrecommitTest { [Fact(DisplayName = "Body op before commit is flagged")] public void BodyOpBeforeCommitIsFlagged() { ... } }`** (lift single `test(...)` into one `[Fact]`-attributed public instance method; class name mirrors `.dart` filename PascalCased — `linter_body_precommit_test.dart` → `LinterBodyPrecommitTest.cs`; test name PascalCased to identifier `BodyOpBeforeCommitIsFlagged`, preserved verbatim via `DisplayName`). Reuses `rf-dart-test-main-to-xunit-class-with-facts`. No `IDisposable`/`IAsyncLifetime` (no setUp/tearDown). Synchronous `void` (no async surface).

- **`final prog = BC.prog([BC.L('C1'), BC.TRY(), BC.W(10), BC.BCONST(10, 42) /* body op before COMMIT (illegal) */, BC.U('END'), BC.L('END'), BC.SUSP()]);` → `var prog = BC.Prog(new[] { BC.L("C1"), BC.TRY(), BC.W(10), /* body op before COMMIT (illegal) */ BC.BCONST(10, 42), BC.U("END"), BC.L("END"), BC.SUSP() });`** (or `new List<Op> { ... }` if asm.dart SUT spec records `List<Op>` parameter). Composes three KB-cached idioms: (a) `rf-dart-final-local-to-csharp-var`, (b) `rf-dart-list-literal-of-constructors-to-csharp-array-init` (extended to static-factory calls — same shape, `Op[]` element type inferred per asm.dart SUT spec), (c) `rf-dart-namespace-class-of-statics-to-csharp-static-class` (PascalCasing rules per asm.dart SUT spec: `BC.prog` → `BC.Prog`; the acronym factories `L`, `TRY`, `W`, `BCONST`, `U`, `SUSP` preserved verbatim). Single-quoted Dart string literals → double-quoted C#. Line comment preserved verbatim above `BC.BCONST(10, 42)` — load-bearing for reviewer intent. Integer literals (`10`, `42`) → C# `int` (or `long` if asm.dart SUT spec records `rf-dart-int-to-csharp-long-width`).

- **`final res = Linter().lint(prog);` → `var res = new Linter().Lint(prog);`** Composes: (a) `rf-dart-final-local-to-csharp-var`, (b) C# mandatory `new` at object-creation site (Microsoft Learn `new` operator reference), (c) `rf-dart-property-chain-method-call-to-csharp` — `.lint` → `.Lint` per linter SUT spec (`Linter` retains class identity, method `lint` PascalCased). Linter is reference-type per SUT spec; instance discarded after call.

- **`expect(res.ok, isFalse);` → `Assert.False(res.Ok);`** Reuses `rf-dart-expect-isFalse-isTrue-to-xunit-assert-true-false` (authoritative: pub.dev `package:matcher` `isFalse`, xunit.net `Assert.False(bool)`). Member-access PascalCased: `res.ok` → `res.Ok` (LintResult SUT spec records `public bool Ok => Issues.Count == 0`). No `reason:` argument at this site.

- **`expect(res.issues.any((e) => e.code == 'BODY_BEFORE_COMMIT'), isTrue, reason: res.issues.join('\n'));` → `Assert.True(res.Issues.Any(e => e.Code == "BODY_BEFORE_COMMIT"), string.Join("\n", res.Issues));`** NEW composed finding `rf-dart-iterable-any-with-expect-reason-join-to-xunit-assert-true-linq` composes four sub-constructs: (1) Dart `Iterable.any(p)` → LINQ `IEnumerable<T>.Any(p)` (both short-circuit — Microsoft Learn `Enumerable.Any<T>`); (2) Dart `isTrue` matcher → xUnit `Assert.True(bool, string)` (which DOES have a `userMessage` overload — load-bearing divergence from `Assert.Equal<T>`); (3) Dart `reason:` keyword argument → xUnit `userMessage` positional second argument (direct routing, no inline-comment fallback); (4) Dart `Iterable.join('\n')` → `string.Join("\n", iter)` (calls `T.ToString()` internally — Microsoft Learn `String.Join<T>`). LintIssue SUT spec records polymorphic `ToString()` producing `[code] @op#index: message` — matches Dart `toString()`. Eager evaluation of `reason:`/`userMessage` is semantic-match on both sides. Lambda `(e) => e.code == 'BODY_BEFORE_COMMIT'` → `e => e.Code == "BODY_BEFORE_COMMIT"` (parens dropped on single param; single-quoted → double-quoted; `e.code` → `e.Code` per LintIssue SUT spec). `\n` byte-identical on both sides.

- **`void main()` registration pass → DROPPED entirely** (xUnit discovers via reflection over `[Fact]` attributes; no `main` equivalent — same as every prior test-file conversion in the batch).

## 3. Decomposed Task Units

- T1 — emit `using Xunit;` file-level directive (replaces `package:test/test.dart` import). done.
- T2 — emit `using <RootNs>.Bytecode;` file-level directive (replaces `package:glp_runtime/bytecode/asm.dart` import). done.
- T3 — emit `using <RootNs>.Lint;` file-level directive (replaces `package:glp_runtime/lint/linter.dart` import). done.
- T4 — emit `public class LinterBodyPrecommitTest { ... }` (single public test class, no base class). done.
- T5 — emit `[Fact(DisplayName = "Body op before commit is flagged")] public void BodyOpBeforeCommitIsFlagged() { ... }` (one `[Fact]` method, synchronous `void`, DisplayName preserves Dart test name verbatim). done.
- T6 — emit method-body line 1: `var prog = BC.Prog(new[] { BC.L("C1"), BC.TRY(), BC.W(10), /* body op before COMMIT (illegal) */ BC.BCONST(10, 42), BC.U("END"), BC.L("END"), BC.SUSP() });` (array initializer of seven `BC.*` static-factory calls; line comment preserved verbatim above `BC.BCONST(10, 42)`; element type inferred to `Op[]` per asm.dart SUT spec). done.
- T7 — emit method-body line 2: `var res = new Linter().Lint(prog);` (Dart implicit-new → C# mandatory `new`; `.lint` PascalCased to `.Lint` per linter SUT spec). done.
- T8 — emit method-body line 3: `Assert.False(res.Ok);` (Dart `expect(actual, isFalse)` → xUnit `Assert.False`; `res.ok` → `res.Ok` per LintResult SUT spec). done.
- T9 — emit method-body line 4: `Assert.True(res.Issues.Any(e => e.Code == "BODY_BEFORE_COMMIT"), string.Join("\n", res.Issues));` (LINQ `Any` predicate + xUnit `Assert.True(bool, string)` userMessage overload + `string.Join` lazy diagnostic; PascalCasing per LintResult/LintIssue SUT specs). done.
- T10 — drop Dart `void main()` entirely (xUnit discovery is attribute-driven). done.

## 4. Research Findings

none required — every construct REUSES a KB-cached idiom from a sibling convspec (FR-012 / SC-007) or applies a single NEW composed finding (`rf-dart-iterable-any-with-expect-reason-join-to-xunit-assert-true-linq`) that the convspec records with full authoritative provenance on both Dart (dart.dev `Iterable.any`, `Iterable.join`, pub.dev `expect`/`reason:`) and .NET (Microsoft Learn `Enumerable.Any<T>`, `String.Join<T>`, xunit.net `Assert.True(bool, string)`) sides. No WebSearch/WebFetch/Agent needed.

## 5. Consistency Pass

fixed — derived from `.codeconv/conversion-specs/test/lint/linter_body_precommit_test.dart.md` (construct keys `dart.package_test.import_directive`, `dart.internal_package_import.same_package_multi`, `dart.test_file.void_main_single_test_call`, `dart.local_var.final_typed_bc_prog_list_literal_of_static_calls`, `dart.local_var.final_method_call_chain_constructor_dot_method`, `dart.package_test.expect_bare_value_isFalse_matcher`, `dart.package_test.expect_iterable_any_predicate_isTrue_with_reason_and_lambda_diagnostic`); the seven `conversion_units` items in the convspec match T1–T10 one-to-one (T2+T3 are the two `using` directives split from the spec's two-import unit; T6 absorbs the spec's one method-body-line-1 unit; T10 mirrors the spec's "NO equivalent of Dart's void main()" line). All SUT-side decisions (class identity `Linter`, method PascalCasing `lint` → `Lint`, property `ok` → `Ok` with body `Issues.Count == 0`, property `issues` → `Issues` of type `List<LintIssue>`, property `code` → `Code`, `LintIssue.ToString` polymorphic override producing `[code] @op#index: message`, `BC.*` factory return type `Op`, PascalCasing rule for `BC.prog` → `BC.Prog` with acronym factories `L`/`TRY`/`W`/`BCONST`/`U`/`SUSP` preserved verbatim) are owned by the cited SUT specs (`.codeconv/conversion-specs/lib/lint/linter.dart.md`, `.codeconv/conversion-specs/lib/bytecode/asm.dart.md`, `.codeconv/conversion-specs/lib/bytecode/opcodes.dart.md`) and referenced (not duplicated) here. Convspec `escalations: []` honoured.

## 6. Escalations

None.
