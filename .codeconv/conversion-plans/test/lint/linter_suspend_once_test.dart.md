---
path: test/lint/linter_suspend_once_test.dart
cycle_group_id: 133
scc_siblings: []
generated_at: 2026-05-21T16:34:28Z
source_sha256: ff52d31f145f441b25675a8c7ab295757e470f4981b48748f51126645e61cfda
schema_version: 1
---

# Conversion Plan: test/lint/linter_suspend_once_test.dart

## 1. Source Analysis

Actual .dart inspection (30 lines, sha256 `ff52d31f145f441b25675a8c7ab295757e470f4981b48748f51126645e61cfda`):

- **Line 1**: `import 'package:test/test.dart';` — `package:test` framework import.
- **Line 2**: `import 'package:glp_runtime/bytecode/asm.dart';` — internal package import, brings in the `BC` static assembler facade.
- **Line 3**: `import 'package:glp_runtime/lint/linter.dart';` — internal package import, brings in `Linter`, `LintResult`, `LintIssue`.
- **Line 5**: `void main() { ... }` — Dart top-level entry function that registers exactly ONE `test(...)` call.
- **Line 6**: `test('Multiple SuspendEnd or ClauseTry after SuspendEnd is flagged', () { ... });` — single registered xUnit-shaped test case with a free-form descriptive name (no leading digit, no colon/slash/semicolon characters — only spaces).
- **Lines 7–23**: `final prog = BC.prog([...]);` — Dart `final` local bound to the result of `BC.prog(...)` applied to a 12-element list literal of static-factory calls on `BC`. Elements (in source order):
  - `BC.L('C1')`, `BC.TRY()`, `BC.R(1)`, `BC.U('END')` (blank line)
  - `BC.L('END')`, `BC.SUSP()` (blank line)
  - Line comment `// Illegal extra ClauseTry after final suspend:`
  - `BC.L('C2')`, `BC.TRY()`, `BC.R(2)`, `BC.U('END2')`, `BC.L('END2')`, `BC.SUSP()`
  - Two blank lines and one load-bearing line comment preserve the visual grouping: clause C1 prologue, terminal END+SUSP, illegal extra C2+END2+SUSP.
- **Line 25**: `final res = Linter().lint(prog);` — Dart `final` local; constructor invocation without `new` (Dart 2+); single method call `.lint(prog)` on the freshly-constructed `Linter` instance.
- **Line 26**: `expect(res.ok, isFalse);` — `package:test` `expect` with `isFalse` matcher on a get-only computed property.
- **Lines 27–28**: `expect(res.issues.any((e) => e.code == 'SUSPEND_ONCE_AT_END'), isTrue, reason: res.issues.join('\n'));` — composed expect call: `Iterable.any` predicate filter + `isTrue` matcher + `reason:` named argument carrying a lazily-built but eagerly-evaluated diagnostic string via `Iterable.join('\n')`. The diagnostic-code string `'SUSPEND_ONCE_AT_END'` is a cross-file constant owned by the linter SUT.
- No `async` / `Future` / `Stream` / isolate / `Completer` / `Timer` / `late` / `mixin` / `extension` / generics / sealed / abstract / nullable surface anywhere in the file. The `[Fact]` method is fully synchronous.
- File-name convention: `linter_suspend_once_test.dart` → C# file `LinterSuspendOnceTest.cs` / class `LinterSuspendOnceTest`.

## 2. Dart → C#/.NET Conversion Plan

Each construct mirrors the RATIFIED convspec verbatim. The `→` separator below is U+2192.

- `import 'package:test/test.dart';` → `using Xunit;` (file-level using directive). REUSE `rf-dart-package-test-to-dotnet-xunit` from `smoke_test.dart.md`. No `ITestOutputHelper` injection (no `print` calls).
- `import 'package:glp_runtime/bytecode/asm.dart';` → `using <RootNs>.Bytecode;` (file-level using). REUSE `rf-dart-internal-package-import-to-csharp-using`. Namespace owned by the bytecode SUT specs (`asm.dart.md`, `opcodes.dart.md`).
- `import 'package:glp_runtime/lint/linter.dart';` → `using <RootNs>.Lint;` (file-level using). REUSE `rf-dart-internal-package-import-to-csharp-using`. Namespace owned by the lint SUT spec (`linter.dart.md`).
- `void main() { test('...', () { ... }); }` → `public class LinterSuspendOnceTest { [Fact(DisplayName = "Multiple SuspendEnd or ClauseTry after SuspendEnd is flagged")] public void MultipleSuspendEndOrClauseTryAfterSuspendEndIsFlagged() { ... } }` — drop `main()` entirely; single `[Fact]`-attributed instance method; `DisplayName` preserves the human-readable test name verbatim. REUSE `rf-dart-test-main-to-xunit-class-with-facts`. Method identifier is a pure PascalCased phrase (no leading-digit / colon / slash / semicolon footgun — only spaces stripped). Method is synchronous `void` (no async surface). Fresh instance per `[Fact]` per xUnit "Shared Context between Tests".
- `final prog = BC.prog([ BC.L('C1'), BC.TRY(), BC.R(1), BC.U('END'), BC.L('END'), BC.SUSP(), /* line comment */ BC.L('C2'), BC.TRY(), BC.R(2), BC.U('END2'), BC.L('END2'), BC.SUSP() ]);` → `var prog = BC.Prog(new[] { BC.L("C1"), BC.TRY(), BC.R(1), BC.U("END"), BC.L("END"), BC.SUSP(), /* Illegal extra ClauseTry after final suspend: */ BC.L("C2"), BC.TRY(), BC.R(2), BC.U("END2"), BC.L("END2"), BC.SUSP() });`. Composes three KB-cached idioms:
  - (a) `final` → `var` via `rf-dart-final-local-to-csharp-var`.
  - (b) Dart list-literal of factory calls → C# `new[] { ... }` array initializer via `rf-dart-list-literal-of-constructors-to-csharp-array-init` (extended to static-factory call elements; element type inferred to `Op[]` per the asm.dart SUT spec).
  - (c) `BC.<name>` static-method names PascalCased per `rf-dart-namespace-class-of-statics-to-csharp-static-class`: `BC.prog` → `BC.Prog`; `BC.L`, `BC.TRY`, `BC.R`, `BC.U`, `BC.SUSP` already PascalCase/acronym, preserved verbatim per the asm.dart SUT spec's acronym-preservation rule.
  - Dart single-quoted string literals (`'C1'`, `'END'`, `'C2'`, `'END2'`) → C# double-quoted (`"C1"`, `"END"`, `"C2"`, `"END2"`).
  - The line comment `// Illegal extra ClauseTry after final suspend:` is load-bearing and is emitted verbatim above the `BC.L("C2")` element to document the test's DELIBERATE invalid-bytecode construction.
  - Blank-line visual grouping (three logical groups) preserved verbatim (legal in both Dart list literals and C# array initializers).
  - Element-order preserved verbatim (the relative ordering is what makes this bytecode illegal under `SUSPEND_ONCE_AT_END`).
  - Integer-literal width nuance: `1` and `2` flow through as C# `int` literals (asm.dart SUT spec authoritative).
- `final res = Linter().lint(prog);` → `var res = new Linter().Lint(prog);`. Composes:
  - (a) `final` → `var` via `rf-dart-final-local-to-csharp-var`.
  - (b) Dart implicit-new constructor call → C# mandatory `new` per Microsoft Learn `new` operator reference.
  - (c) `.lint(prog)` → `.Lint(prog)` (PascalCased) via `rf-dart-property-chain-method-call-to-csharp` and the linter SUT spec.
  - `Linter` is recorded as an instance class per the SUT spec (NOT static/singleton), so `new Linter().Lint(...)` is the correct shape; the instance is discarded immediately and reclaimed by GC.
- `expect(res.ok, isFalse);` → `Assert.False(res.Ok);` via `rf-dart-expect-isFalse-isTrue-to-xunit-assert-true-false`. `res.ok` → `res.Ok` (computed get-only property per LintResult SUT spec; `public bool Ok => Issues.Count == 0`). No `reason:` argument at this call site.
- `expect(res.issues.any((e) => e.code == 'SUSPEND_ONCE_AT_END'), isTrue, reason: res.issues.join('\n'));` → `Assert.True(res.Issues.Any(e => e.Code == "SUSPEND_ONCE_AT_END"), string.Join("\n", res.Issues));` via `rf-dart-iterable-any-with-expect-reason-join-to-xunit-assert-true-linq`. Composes:
  - `res.issues` → `res.Issues` (PascalCased per LintResult SUT spec; insertion-ordered `List<LintIssue>`).
  - `.any((e) => e.code == 'SUSPEND_ONCE_AT_END')` → `.Any(e => e.Code == "SUSPEND_ONCE_AT_END")` (LINQ `Any` with identical short-circuit semantics; lambda param parens dropped per C# convention; `e.code` → `e.Code` per LintIssue SUT spec; single-quoted → double-quoted string literal).
  - `reason: res.issues.join('\n')` → second argument `string.Join("\n", res.Issues)` of `Assert.True(bool, string)`. Both languages eagerly evaluate the diagnostic argument even on PASSING assertions — semantic match.
  - `string.Join("\n", res.Issues)` calls `LintIssue.ToString()` internally; the SUT spec pins `LintIssue.ToString()` as a polymorphic override emitting `[<code>] @op#<index>: <message>`.
  - Diagnostic-code string `"SUSPEND_ONCE_AT_END"` is a load-bearing cross-file constant — must remain byte-identical between linter.cs (emitter) and this test (consumer), or the test silently degrades to `Any` returning `false`.

## 3. Decomposed Task Units

- T1: Emit file-level `using Xunit;` directive (drop `import 'package:test/test.dart';`).
- T2: Emit file-level `using <RootNs>.Bytecode;` directive (drop `import 'package:glp_runtime/bytecode/asm.dart';`).
- T3: Emit file-level `using <RootNs>.Lint;` directive (drop `import 'package:glp_runtime/lint/linter.dart';`).
- T4: Emit `public class LinterSuspendOnceTest { ... }` test class (file-name → class-name).
- T5: Emit `[Fact(DisplayName = "Multiple SuspendEnd or ClauseTry after SuspendEnd is flagged")] public void MultipleSuspendEndOrClauseTryAfterSuspendEndIsFlagged() { ... }` method shell.
- T6: Emit `var prog = BC.Prog(new[] { BC.L("C1"), BC.TRY(), BC.R(1), BC.U("END"), BC.L("END"), BC.SUSP(), /* Illegal extra ClauseTry after final suspend: */ BC.L("C2"), BC.TRY(), BC.R(2), BC.U("END2"), BC.L("END2"), BC.SUSP() });` with preserved blank-line grouping and the load-bearing line comment.
- T7: Emit `var res = new Linter().Lint(prog);`.
- T8: Emit `Assert.False(res.Ok);`.
- T9: Emit `Assert.True(res.Issues.Any(e => e.Code == "SUSPEND_ONCE_AT_END"), string.Join("\n", res.Issues));`.
- T10: Drop Dart `void main()` entirely (xUnit discovery is attribute-driven).

## 4. Research Findings

none required — all seven idioms/findings are REUSED verbatim from the RATIFIED convspec's KB-cached entries (FR-012 / SC-007): `rf-dart-package-test-to-dotnet-xunit`, `rf-dart-internal-package-import-to-csharp-using`, `rf-dart-test-main-to-xunit-class-with-facts`, `rf-dart-final-local-to-csharp-var`, `rf-dart-list-literal-of-constructors-to-csharp-array-init`, `rf-dart-property-chain-method-call-to-csharp`, `rf-dart-expect-isFalse-isTrue-to-xunit-assert-true-false`, `rf-dart-iterable-any-with-expect-reason-join-to-xunit-assert-true-linq`. No new findings introduced; no NEW idioms required.

## 5. Consistency Pass

fixed — derived from `.codeconv/conversion-specs/test/lint/linter_suspend_once_test.dart.md` (RATIFIED convspec). All construct mappings (§2), task units (§3), and idiom citations align verbatim with the convspec's `constructs:` block, `conversion_units:` block, and "Rationale and research provenance" section. Cross-file SUT references resolved against `.codeconv/conversion-specs/lib/bytecode/asm.dart.md`, `.codeconv/conversion-specs/lib/bytecode/opcodes.dart.md`, and `.codeconv/conversion-specs/lib/lint/linter.dart.md`. Source sha256 verified against tombstone: `ff52d31f145f441b25675a8c7ab295757e470f4981b48748f51126645e61cfda` (matches both tombstone and convspec). The convspec records zero escalations; this plan inherits zero escalations.

## 6. Escalations

None.
