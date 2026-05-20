# Conversion Spec — test/lint/linter_suspend_once_test.dart

> Conversion-spec artifact for test/lint/linter_suspend_once_test.dart
> (FR-011). Spec-only (FR-023): describes the Dart->C# conversion;
> contains NO compilable C#. A later codegen stage consumes the
> structured block.
>
> A tiny (30-line) xUnit-shaped test file that hand-assembles ONE
> deliberately-illegal `BytecodeProgram` via the `BC` static assembler
> facade (`asm.dart`) — TWO clause prologues (`C1`, `C2`) each followed
> by their own `SuspendEnd`, exercising the linter's
> `SUSPEND_ONCE_AT_END` rule (only one `SuspendEnd` is allowed and it
> must terminate the program; an additional `ClauseTry` after the
> terminal `SuspendEnd` is illegal). The test drives the program
> through `Linter().lint(prog)`, asserts `res.ok` is `false`, and
> asserts the issues collection contains an issue with
> `code == 'SUSPEND_ONCE_AT_END'`, surfacing the joined issue list as
> a diagnostic on failure. Every cross-file type reference (`BC`,
> `Linter`, `LintResult`, `LintIssue`) is REUSED from the corresponding
> sibling convspec (FR-024 cache hit), never re-derived. Every
> test-framework / assertion / top-level-`main` decision is REUSED
> from the prior batch of test convspecs (`test/smoke_test.dart.md`,
> `test/glp_runtime_test.dart.md`,
> `test/heap/varref_pointer_test.dart.md`,
> `test/conformance/fairness_26_test.dart.md`,
> `test/bytecode/utility_instructions_test.dart.md`,
> `test/bytecode/fairness_scheduler_loop_test.dart.md`,
> `test/lint/linter_ok_test.dart.md`,
> `test/lint/linter_body_precommit_test.dart.md`). The composed
> `expect(iter.any(p), isTrue, reason: iter.join('\n'))` finding is
> REUSED verbatim from `linter_body_precommit_test.dart.md`. No
> escalations.

```yaml
schema_version: 1
source_path: test/lint/linter_suspend_once_test.dart
source_sha256: ff52d31f145f441b25675a8c7ab295757e470f4981b48748f51126645e61cfda
target_code_unit: test/lint/LinterSuspendOnceTest.cs
constructs:
  - construct_key: dart.package_test.import_directive
    source_form: "import 'package:test/test.dart';"
    target_decision: >-
      Drop `import 'package:test/test.dart';` and emit `using Xunit;` at
      file scope. REUSE the batch-wide test-framework idiom from
      `test/smoke_test.dart.md` (and every subsequent test convspec in
      the batch — `glp_runtime_test`, `test/heap/*`, `test/conformance/*`,
      `test/multiagent/*`, `test/analysis/*`, `test/module/*`,
      `test/bytecode/utility_instructions_test`,
      `test/bytecode/fairness_scheduler_loop_test`,
      `test/lint/linter_ok_test`, `test/lint/linter_body_precommit_test`).
      Per FR-012 / SC-007 this construct is NOT re-researched here — the
      `rf-dart-package-test-to-dotnet-xunit` finding carries forward
      verbatim. NO `ITestOutputHelper` injection needed here (this file
      has zero `print(...)` calls). The .NET test project's `.csproj`
      (referencing `xunit`, `xunit.runner.visualstudio`,
      `Microsoft.NET.Test.Sdk`) is OUT OF SCOPE for this per-file
      artifact — langpair-level emission concern.
    idiom_id: rf-dart-package-test-to-dotnet-xunit
    research_finding_id: rf-dart-package-test-to-dotnet-xunit
    nuance: >-
      Framework-choice reuse (explicitly addressed, no re-derivation):
      xUnit was settled in `smoke_test.dart`; every subsequent test
      file in the batch reuses it via the KB (FR-012). Top-level
      `test()` => `[Fact]` instance method, fresh test-class instance
      per `[Fact]` invocation (xunit.net "Shared Context between
      Tests"), no top-level function surface in xUnit. No async /
      `Future` / `Stream` / isolate surface in this file => `[Fact]`
      method is synchronous `void` (NOT `async Task`). Strict-bool /
      strict-equality semantics unaffected by the import itself. No
      setUp / tearDown / group / `IDisposable.Dispose` /
      `IAsyncLifetime` needed — single one-shot test with method-local
      state only.
  - construct_key: dart.internal_package_import.same_package_multi
    source_form: >-
      "import 'package:glp_runtime/bytecode/asm.dart';
       import 'package:glp_runtime/lint/linter.dart';"
    target_decision: >-
      Drop both Dart `import 'package:glp_runtime/...';` directives and
      emit TWO file-level C# `using` directives: `using
      <RootNs>.Bytecode;` (covers the `BC` static assembler facade per
      the SUT spec `.codeconv/conversion-specs/lib/bytecode/asm.dart.md`
      and the opcode types parameterised by `BC` builders per
      `.codeconv/conversion-specs/lib/bytecode/opcodes.dart.md`) and
      `using <RootNs>.Lint;` (covers `Linter`, `LintResult`, `LintIssue`
      per the SUT spec
      `.codeconv/conversion-specs/lib/lint/linter.dart.md`). REUSE the
      `rf-dart-internal-package-import-to-csharp-using` finding recorded
      in `test/heap/*`, `test/conformance/fairness_26_test`,
      `test/bytecode/utility_instructions_test`,
      `test/bytecode/fairness_scheduler_loop_test`,
      `test/lint/linter_ok_test`, and
      `test/lint/linter_body_precommit_test`. No external NuGet
      reference (both targets are sibling code units inside the same
      converted assembly). The test assembly's `.csproj` must reference
      the converted-SUT assembly — langpair-level concern, OUT OF SCOPE.
    idiom_id: rf-dart-internal-package-import-to-csharp-using
    research_finding_id: rf-dart-internal-package-import-to-csharp-using
    nuance: >-
      Cross-file dependency nuance (explicitly addressed, KB reuse):
      Dart `package:` imports are per-file path-based; C# `using` is
      per-namespace. Two Dart files into two converted sub-namespaces
      (`Bytecode` and `Lint`) => two C# `using` directives — direct
      1:1 here (no collapse, because each import lifts into a distinct
      sub-namespace per the SUT specs). Sub-namespace boundary from
      the SUT specs: `bytecode/*.dart` => `<RootNs>.Bytecode`,
      `lint/*.dart` => `<RootNs>.Lint`. No `using static` is needed —
      the `BC` class IS a namespace-of-statics (per
      `lib/bytecode/asm.dart.md` rf-dart-namespace-class-of-statics-to-
      csharp-static-class) and is called as `BC.Prog(...)`, `BC.L(...)`,
      `BC.TRY()`, `BC.R(...)`, `BC.U(...)`, `BC.SUSP()` at the test
      call sites — qualified, so no `using static BC;` is needed.
      Linter / LintResult / LintIssue are ordinary types reachable
      through the namespace-level `using`. No cross-package, cross-
      isolate, or transitive-export semantics apply. Visibility: every
      imported identifier is library-public on the Dart side (no
      leading underscore) => `public` on the C# side per the SUT specs.
  - construct_key: dart.test_file.void_main_single_test_call
    source_form: >-
      "void main() {
         test('Multiple SuspendEnd or ClauseTry after SuspendEnd is flagged',
              () { ... });
       }"
    target_decision: >-
      Eliminate the Dart `void main()` function entirely and lift the
      single registered `test(...)` call into one `[Fact]`-attributed
      public instance method on a `public class
      LinterSuspendOnceTest` (mirroring the file name
      `linter_suspend_once_test.dart` =>
      `LinterSuspendOnceTest.cs` per the file-name-to-class-name idiom
      recorded in every prior sibling test convspec). The Dart test
      name `'Multiple SuspendEnd or ClauseTry after SuspendEnd is
      flagged'` becomes the method identifier
      `MultipleSuspendEndOrClauseTryAfterSuspendEndIsFlagged`
      (PascalCased, spaces stripped — no leading-digit / colon /
      slash / semicolon footgun fires here; the name is a pure
      identifier-clean phrase). Emit `[Fact(DisplayName = "Multiple
      SuspendEnd or ClauseTry after SuspendEnd is flagged")]` to
      preserve the original human-readable reporting name. REUSE the
      idiom recorded in the sibling smoke_test.dart,
      glp_runtime_test.dart, fairness_26_test.dart,
      utility_instructions_test.dart, fairness_scheduler_loop_test.dart,
      linter_ok_test.dart, and linter_body_precommit_test.dart specs —
      same structural lift; no re-research (FR-012).
    idiom_id: rf-dart-test-main-to-xunit-class-with-facts
    research_finding_id: rf-dart-test-main-to-xunit-class-with-facts
    nuance: >-
      Discovery-model nuance (explicitly addressed, carry-forward from
      siblings): xUnit discovers tests by reflection over `[Fact]`
      attributes with a FRESH instance of the test class per `[Fact]`
      invocation (xunit.net "Shared Context between Tests"). The Dart
      `main()` registration pass has no xUnit equivalent and is
      dropped entirely. No setUp / tearDown / group / async — the
      method is synchronous `void`, no constructor / `IDisposable.
      Dispose` / `IAsyncLifetime` surface (no per-test fixture state).
      The `BytecodeProgram prog`, `LintResult res` locals are method-
      scoped (Dart `final`, not class-scoped) so no field promotion
      occurs. Identifier nuance: the Dart test-name leading token
      `Multiple` is a letter — no `Step` / `TwentySix` prefix needed
      (contrast fairness_26 where the leading digit `26` forced a
      `StepTwentySix` rewrite). Length nuance: the resulting C#
      method identifier (52 characters) is well within the .NET
      identifier length limit (no truncation required); the C#
      language specification places no practical bound on identifier
      length (Microsoft Learn lexical-structure reference).
  - construct_key: dart.local_var.final_typed_bc_prog_list_literal_of_static_calls
    source_form: >-
      "final prog = BC.prog([
         BC.L('C1'),
         BC.TRY(),
         BC.R(1),
         BC.U('END'),

         BC.L('END'),
         BC.SUSP(),

         // Illegal extra ClauseTry after final suspend:
         BC.L('C2'),
         BC.TRY(),
         BC.R(2),
         BC.U('END2'),
         BC.L('END2'),
         BC.SUSP(),
       ]);"
    target_decision: >-
      Emit `var prog = BC.Prog(new[] { BC.L("C1"), BC.TRY(), BC.R(1),
      BC.U("END"), BC.L("END"), BC.SUSP(), /* Illegal extra ClauseTry
      after final suspend: */ BC.L("C2"), BC.TRY(), BC.R(2),
      BC.U("END2"), BC.L("END2"), BC.SUSP() });` (or equivalently
      `BC.Prog(new List<Op> { ... })` if the SUT
      `BC.Prog`-equivalent's parameter type is `List<Op>` per
      `.codeconv/conversion-specs/lib/bytecode/asm.dart.md`). REUSE
      three composed idioms here, all KB-cached (FR-012 / SC-007):
      (a) Dart `final` local => C# `var` per
      `rf-dart-final-local-to-csharp-var` (sibling
      fairness_scheduler_loop_test.dart, linter_ok_test.dart,
      linter_body_precommit_test.dart); (b) Dart list-literal of
      constructor/factory calls => C# `new[] { ... }` array initializer
      per `rf-dart-list-literal-of-constructors-to-csharp-array-init`
      (sibling fairness_scheduler_loop_test.dart,
      linter_body_precommit_test.dart) — applied here to static-method
      calls on `BC` rather than `new` expressions, with the same shape;
      (c) `BC.<name>` static-method names PascalCased verbatim per the
      asm.dart SUT spec
      (`rf-dart-namespace-class-of-statics-to-csharp-static-class`):
      `BC.prog` => `BC.Prog`, `BC.L` already PascalCase, `BC.TRY` is an
      acronym preserved as-is per the SUT spec, `BC.R` already
      PascalCase, `BC.U` already PascalCase, `BC.SUSP` acronym
      preserved as-is per the SUT spec. Dart single-quoted string
      literals (`'C1'`, `'END'`, `'C2'`, `'END2'`) => C# double-quoted
      string literals (`"C1"`, `"END"`, `"C2"`, `"END2"`). The Dart
      line comment `// Illegal extra ClauseTry after final suspend:`
      is preserved verbatim above the `BC.L("C2")` element to retain
      reviewer-intent (load-bearing — documents the test's DELIBERATE
      illegal-bytecode construction).
    idiom_id: rf-dart-list-literal-of-constructors-to-csharp-array-init
    research_finding_id: rf-dart-list-literal-of-constructors-to-csharp-array-init
    nuance: >-
      Three-idiom composition nuance (explicitly addressed): the same
      array-initializer shape applies whether the elements are
      `new Ctor(...)` expressions (siblings) or static-factory calls
      `BC.X(...)` (this file). Both lift into the same C# `new[] {
      ... }` literal whose element type is inferred as the LUB of the
      factory return-types — per the asm.dart SUT spec the BC
      factories all return `Op` (or an `Op`-derived sub-type that the
      array infers up to `Op`), so the result type is `Op[]` (or
      `List<Op>` if the SUT parameter is List). Line-comment
      preservation is load-bearing here because the comment documents
      the test's DELIBERATE invalid-bytecode construction (an extra
      `ClauseTry` block after the first `SuspendEnd`, including a
      SECOND `SuspendEnd` — both forbidden by the
      `SUSPEND_ONCE_AT_END` rule) — without it, a future maintainer
      might "fix" the apparent bug. Blank-line preservation nuance:
      the Dart source uses blank lines between the three logical
      groups (clause C1 prologue, terminal END+SUSP, illegal extra
      C2+END2+SUSP); C# permits identical blank lines inside an
      array initializer, so the conversion preserves the visual
      grouping verbatim. Single-assignment-INTENT loss (Dart `final`
      => C# `var`) is recorded in the var-idiom but does not fire
      here (the local is never reassigned). The two static calls
      with NO arguments — `BC.TRY()` and `BC.SUSP()` — preserve
      their empty argument lists verbatim in C# (`BC.TRY()`,
      `BC.SUSP()`); both appear TWICE in the element list (once per
      clause prologue/end pair). Integer-width nuance: the `1` and
      `2` arguments to `BC.R(1)` and `BC.R(2)` are Dart `int`
      literals. Per the asm.dart SUT spec they map to C# `int`
      (unless the SUT records `long` per
      `rf-dart-int-to-csharp-long-width` for the `BC.R` parameter —
      codegen honours the asm.dart SUT spec's chosen width). The
      labels-and-jumps structural nuance (two `BC.L` per clause:
      header `C1`/`C2` and end-target `END`/`END2`; two `BC.U` calls
      jumping to those end-targets) is preserved verbatim — the
      array-initializer is order-sensitive in BOTH languages, so the
      relative ordering that makes this program illegal
      (`BC.SUSP()` then a SECOND `BC.L("C2")` etc.) is faithfully
      preserved.
  - construct_key: dart.local_var.final_method_call_chain_constructor_dot_method
    source_form: "final res = Linter().lint(prog);"
    target_decision: >-
      Emit `var res = new Linter().Lint(prog);` in the C# `[Fact]`
      method body. REUSE three composed idioms (FR-012 / SC-007):
      (a) Dart `final` local => C# `var` per `rf-dart-final-local-to-
      csharp-var`; (b) Dart constructor invocation without `new` => C#
      mandatory `new Linter()` (C# requires the `new` operator at
      object-creation sites — Microsoft Learn `new` operator
      reference); (c) Dart method-call chain `obj.method(arg)` => C#
      `obj.Method(arg)` per `rf-dart-property-chain-method-call-to-
      csharp` (sibling fairness_scheduler_loop_test.dart,
      linter_body_precommit_test.dart) — method name PascalCased per
      the linter SUT spec
      `.codeconv/conversion-specs/lib/lint/linter.dart.md` (the public
      method `lint` is rendered as `Lint`, and the public class
      `Linter` keeps its PascalCase identity verbatim). The `prog`
      positional argument flows through unchanged (single-segment
      identifier reference, no transformation).
    idiom_id: rf-dart-property-chain-method-call-to-csharp
    research_finding_id: rf-dart-property-chain-method-call-to-csharp
    nuance: >-
      Constructor-on-stateless-service nuance (explicitly addressed):
      Dart allows constructor calls without `new` (deprecated-but-
      tolerated since Dart 2); C# requires `new` at every object-
      creation site (Microsoft Learn `new` operator reference). The
      `Linter` class is a stateless service-shaped class per the SUT
      spec — the test creates a fresh instance per call rather than
      using a static or singleton pattern, preserving the Dart shape;
      a `static class Linter` with a `static Lint(...)` would be a
      faithful alternative but is REJECTED by the SUT spec which
      records `Linter` as an instance class (so `new
      Linter().Lint(...)` is the correct C# rendering). Reference-vs-
      value: `Linter` is a reference type (C# `class`) per the SUT
      spec — the instance is discarded immediately after
      `.Lint(prog)` returns; .NET GC reclaims it. The `LintResult`
      return type is also a reference type per the SUT spec (NOT a
      record-struct — held in `var res` and read by reference).
  - construct_key: dart.package_test.expect_bare_value_isFalse_matcher
    source_form: "expect(res.ok, isFalse);"
    target_decision: >-
      Translate to xUnit `Assert.False(res.Ok);`. REUSE the
      `isTrue`/`isFalse` matcher mapping recorded in the sibling
      `test/heap/binding_pointer_test.dart.md` spec
      (`rf-dart-expect-isFalse-isTrue-to-xunit-assert-true-false`),
      itself derived from the canonical `smoke_test.dart` matcher
      finding `rf-dart-expect-isTrue-to-xunit-assert-true` and reused
      verbatim in `linter_body_precommit_test.dart.md`. Member access
      on `res.ok` => `res.Ok` (PascalCased per the LintResult SUT
      spec, which records `ok` as a computed get-only property
      `public bool Ok => Issues.Count == 0`). No `reason:` argument
      at this call site, so no inline-comment routing is needed.
    idiom_id: rf-dart-expect-isFalse-isTrue-to-xunit-assert-true-false
    research_finding_id: rf-dart-expect-isFalse-isTrue-to-xunit-assert-true-false
    nuance: >-
      Matcher-mapping nuance (explicitly addressed, KB reuse): Dart's
      `isFalse` matcher from `package:matcher` (re-exported by
      `package:test`) asserts strict boolean falsity (not falsity-
      coercion — Dart booleans are strict). xUnit's
      `Assert.False(bool)` mirrors that exactly: `bool` argument only,
      no truthy/coercion surface (xunit.net Assert API reference).
      Property-access nuance: `res.ok` is a computed get-only Dart
      getter => C# computed get-only property `res.Ok`; per the
      LintResult SUT spec the property body is `Issues.Count == 0`
      (Dart `Iterable.isEmpty` => .NET `List<T>.Count == 0` — O(1)
      on both sides). No null-safety surface fires: `res` is non-
      nullable (the `Linter.Lint` return type is `LintResult`, not
      `LintResult?`, per the SUT spec).
  - construct_key: dart.package_test.expect_iterable_any_predicate_isTrue_with_reason_and_lambda_diagnostic
    source_form: >-
      "expect(res.issues.any((e) => e.code == 'SUSPEND_ONCE_AT_END'),
         isTrue,
         reason: res.issues.join('\\n'));"
    target_decision: >-
      Translate to a C# LINQ `Any(predicate)` call wrapped in
      `Assert.True(<bool-expr>, <userMessage>)`: `Assert.True(res.Issues
      .Any(e => e.Code == "SUSPEND_ONCE_AT_END"), string.Join("\n",
      res.Issues));`. REUSE the composed finding recorded in
      `linter_body_precommit_test.dart.md`
      (`rf-dart-iterable-any-with-expect-reason-join-to-xunit-assert-
      true-linq`) which itself composes: (a) Dart `Iterable.any(p)`
      => LINQ `IEnumerable<T>.Any(p)`; (b) Dart `Iterable.join(sep)`
      => `string.Join(sep, source)`; (c) Dart `isTrue` matcher with
      `reason:` => xUnit `Assert.True(actual, userMessage)`. Member-
      access PascalCasing per the LintResult / LintIssue SUT specs:
      `res.issues` => `res.Issues`, `e.code` => `e.Code`. Dart lambda
      `(e) => e.code == 'SUSPEND_ONCE_AT_END'` => C# lambda `e =>
      e.Code == "SUSPEND_ONCE_AT_END"` (parens around single param
      dropped per C# convention; single-quoted Dart string literal
      => double-quoted C# string literal). The Dart `'\n'` separator
      => C# `"\n"` (Dart and C# both interpret `\n` identically as
      LF). The diagnostic-code string `'SUSPEND_ONCE_AT_END'` is the
      linter-side constant emitted by the linter SUT spec's
      SUSPEND_ONCE_AT_END rule — codegen MUST keep the diagnostic-
      code string byte-identical between linter.cs (emitter) and
      this test (consumer), or the test will break.
    idiom_id: rf-dart-iterable-any-with-expect-reason-join-to-xunit-assert-true-linq
    research_finding_id: rf-dart-iterable-any-with-expect-reason-join-to-xunit-assert-true-linq
    nuance: >-
      Lambda-diagnostic composition nuance (LOAD-BEARING, explicitly
      addressed, KB reuse from linter_body_precommit_test.dart.md):
      this single Dart `expect` call composes FOUR sub-constructs at
      once — (1) iterable predicate filter via `.any(p)`, (2) string-
      typed `reason:` diagnostic, (3) lazy string construction via
      `.join('\\n')` on the issue list, and (4) the `isTrue`
      matcher. The conversion preserves all four: LINQ `Any` keeps
      the predicate's lazy short-circuit semantics (returns as soon
      as the first matching element is found — Microsoft Learn
      `Enumerable.Any<T>(IEnumerable<T>, Func<T,bool>)` => identical
      short-circuit semantics to Dart's `Iterable.any`), and
      `string.Join("\n", res.Issues)` eagerly materialises the
      diagnostic string (same as Dart's `Iterable.join`). Eager-vs-
      lazy diagnostic nuance: BOTH languages eagerly evaluate the
      `reason:`/`userMessage` argument BEFORE the assertion runs
      (Dart `expect`'s `reason:` is a positional `String?`, not a
      thunk; C# `Assert.True`'s `userMessage` is `string`, not
      `Func<string>`). This means the `string.Join` is computed even
      on PASSING assertions — semantic match with Dart, NOT a
      regression. `string.Join("\n", res.Issues)` calls
      `LintIssue.ToString()` internally (Microsoft Learn
      `String.Join<T>(String, IEnumerable<T>)`); the LintIssue SUT
      spec pins `ToString()` as a polymorphic override emitting
      `[<code>] @op#<index>: <message>` verbatim — matching Dart
      `Iterable<LintIssue>.join('\\n')`'s reliance on each element's
      `toString()`. Mutation-side-effect nuance: NO side effect —
      both `.any(p)` and `.Any(p)` are pure reads of the issues
      collection. Order-sensitivity: `.any(p)` is order-sensitive in
      its short-circuit (iterates in collection order, returns on
      first match); the LintResult SUT spec records `issues` as a
      `List<LintIssue>` (insertion-ordered) so the test is
      deterministic on both sides. Diagnostic-code coupling: the
      string literal `'SUSPEND_ONCE_AT_END'` is a cross-file
      constant — the linter-side emitter (linter.dart.md's
      SUSPEND_ONCE_AT_END check) must emit the byte-identical
      string `"SUSPEND_ONCE_AT_END"` in C# or this test will
      silently degrade to `Any` returning `false` and the assertion
      failing. Recorded as a load-bearing cross-file string-constant
      coupling (same shape as the `BODY_BEFORE_COMMIT` coupling in
      linter_body_precommit_test.dart.md).
conversion_units:
  - "using Xunit; (file-level using directive replacing `import 'package:test/test.dart';`)"
  - "using <RootNs>.Bytecode; (single file-level using directive replacing `import 'package:glp_runtime/bytecode/asm.dart';` — namespace identifier owned by the bytecode SUT specs)"
  - "using <RootNs>.Lint; (single file-level using directive replacing `import 'package:glp_runtime/lint/linter.dart';` — namespace identifier owned by the lint SUT spec)"
  - "public class LinterSuspendOnceTest { ... } (single public test class, name mirrors the .dart file name linter_suspend_once_test.dart => LinterSuspendOnceTest, no base class needed)"
  - "[Fact(DisplayName = \"Multiple SuspendEnd or ClauseTry after SuspendEnd is flagged\")] public void MultipleSuspendEndOrClauseTryAfterSuspendEndIsFlagged() { ... } (one Fact-attributed method per Dart test() call; DisplayName preserves the original human-readable test name verbatim)"
  - "method body line 1-15: var prog = BC.Prog(new[] { BC.L(\"C1\"), BC.TRY(), BC.R(1), BC.U(\"END\"), BC.L(\"END\"), BC.SUSP(), /* Illegal extra ClauseTry after final suspend: */ BC.L(\"C2\"), BC.TRY(), BC.R(2), BC.U(\"END2\"), BC.L(\"END2\"), BC.SUSP() }); (Dart list-of-static-factory-calls literal => C# array initializer; line comment preserved verbatim above the BC.L(\"C2\") element; blank-line grouping preserved; element type inferred to Op[] per the asm.dart SUT spec)"
  - "method body line 16: var res = new Linter().Lint(prog); (Dart final => var; Dart implicit-new => C# mandatory new; method-name PascalCased per the linter SUT spec)"
  - "method body line 17: Assert.False(res.Ok); (Dart expect(actual, isFalse) => xUnit Assert.False; property-access PascalCased per the LintResult SUT spec)"
  - "method body line 18: Assert.True(res.Issues.Any(e => e.Code == \"SUSPEND_ONCE_AT_END\"), string.Join(\"\\n\", res.Issues)); (Dart expect(iterable.any(pred), isTrue, reason: iterable.join(\\n)) => xUnit Assert.True(<linq-any>, <user-message>); LINQ Any short-circuits like Dart any; string.Join calls T.ToString() so LintIssue.ToString override is exercised on diagnostic path; diagnostic-code string SUSPEND_ONCE_AT_END byte-identical to the linter SUT-side emitter)"
  - "NO equivalent of Dart's void main() — xUnit discovery is attribute-driven, registration-via-main is dropped entirely (same as every other test-file conversion in this batch)"
escalations: []
```

## Rationale and research provenance (per non-trivial construct)

### rf-dart-package-test-to-dotnet-xunit — `package:test` => xUnit framework choice (REUSED)

- **KB reuse, not re-research (FR-012 / SC-007)**: authoritatively
  researched and recorded in the first test-file spec of this batch
  (`smoke_test.dart`); reused verbatim by every subsequent test
  convspec in the batch including the prior `BC`-using and lint test
  convspecs (`test/bytecode/utility_instructions_test.dart.md`,
  `test/bytecode/fairness_scheduler_loop_test.dart.md`,
  `test/lint/linter_ok_test.dart.md`,
  `test/lint/linter_body_precommit_test.dart.md`). Authoritative
  sources cited verbatim in the originating spec: Microsoft Learn
  `unit-testing-csharp-with-xunit`, xunit.net, pub.dev/package:test.
- **Conclusion**: drop `import 'package:test/test.dart';`, emit
  `using Xunit;`. No `ITestOutputHelper` injection needed (no
  `print` calls). Zero escalation.

### rf-dart-internal-package-import-to-csharp-using — `package:glp_runtime/...` => two `using` directives (REUSED)

- **KB reuse (FR-012 / SC-007)**: recorded in `test/heap/*`,
  `test/conformance/fairness_26_test`,
  `test/bytecode/utility_instructions_test`,
  `test/bytecode/fairness_scheduler_loop_test`,
  `test/lint/linter_ok_test`, and
  `test/lint/linter_body_precommit_test`. Authoritative Dart:
  `dart.dev/tools/pub/dependencies` (per-file `package:` import).
  Authoritative .NET: Microsoft Learn `using directive` reference
  (per-namespace `using`).
- **File-specific application**: two Dart imports map directly to
  two C# `using` directives (one for `Bytecode`, one for `Lint`) —
  no collapse here because each import resolves to a distinct
  sub-namespace per the SUT specs. Zero escalation.

### rf-dart-test-main-to-xunit-class-with-facts — `void main() { test(...) }` => `class { [Fact] }` (REUSED)

- **KB reuse (FR-012 / SC-007)**: recorded in `smoke_test.dart`,
  `glp_runtime_test.dart`, `fairness_26_test.dart`,
  `utility_instructions_test.dart`,
  `fairness_scheduler_loop_test.dart`, `linter_ok_test.dart`,
  `linter_body_precommit_test.dart`, and every other test sibling.
- **File-specific application**: `linter_suspend_once_test.dart` =>
  `LinterSuspendOnceTest.cs` => `public class LinterSuspendOnceTest`;
  the test name `'Multiple SuspendEnd or ClauseTry after SuspendEnd
  is flagged'` => method identifier
  `MultipleSuspendEndOrClauseTryAfterSuspendEndIsFlagged`
  (PascalCased; no leading-digit footgun; no colon/slash/semicolon
  characters to strip — only spaces). `[Fact(DisplayName =
  "Multiple SuspendEnd or ClauseTry after SuspendEnd is flagged")]`
  preserves the original human-readable reporting name. Zero
  escalation.

### rf-dart-final-local-to-csharp-var — `final <local> = <expr>;` => `var <local> = <expr>;` (REUSED)

- **KB reuse (FR-012 / SC-007)**: recorded in the
  `fairness_scheduler_loop_test.dart` sibling, `linter_ok_test.dart`,
  `linter_body_precommit_test.dart`, and many earlier test-file
  siblings. Authoritative Dart: language tour
  `dart.dev/language/variables#final-and-const`. Authoritative .NET:
  Microsoft Learn C# reference
  `learn.microsoft.com/en-us/dotnet/csharp/language-reference/statements/declarations`.
- **File-specific application**: applies to both `final` locals in
  this method body (`prog`, `res`) — both become `var`. Neither is
  reassigned in the source. Zero escalation.

### rf-dart-list-literal-of-constructors-to-csharp-array-init — `[BC.X(...), BC.Y(...)]` => `new[] { BC.X(...), BC.Y(...) }` (REUSED)

- **KB reuse (FR-012 / SC-007)**: recorded in
  `fairness_scheduler_loop_test.dart.md` for `[Ctor1(...),
  Ctor2(...)]` => `new[] { new Ctor1(...), new Ctor2(...) }`, and
  extended in `linter_body_precommit_test.dart.md` for static-
  factory calls `[BC.X(...), BC.Y(...)]` => `new[] { BC.X(...),
  BC.Y(...) }` — the array-initializer mechanic is agnostic to
  whether elements are `new Ctor(...)` or static-factory calls;
  both are element expressions that share a common element type.
  Authoritative Dart: language tour
  `dart.dev/language/collections#lists`. Authoritative .NET
  (pre-12): Microsoft Learn array-initializer reference.
  Authoritative .NET (C# 12+): Microsoft Learn collection-
  expressions reference.
- **File-specific application**: the twelve `BC.*` factory calls
  (two `BC.L('C1')` / `BC.L('END')` / `BC.L('C2')` / `BC.L('END2')`
  labels — four total; two `BC.TRY()`; two `BC.R(n)`; two
  `BC.U(...)`; two `BC.SUSP()`) all return `Op` (per the asm.dart
  SUT spec) so element-type inference produces `Op[]`. Static-method
  PascalCasing for `BC.prog` => `BC.Prog` per the asm.dart SUT spec;
  the other five factories used here (`L`, `TRY`, `R`, `U`, `SUSP`)
  already use uppercase-acronym identifiers preserved verbatim per
  the asm.dart SUT spec
  (`rf-dart-namespace-class-of-statics-to-csharp-static-class`
  records the acronym-preservation rule for `BC` methods). Line-
  comment preservation: the Dart `// Illegal extra ClauseTry after
  final suspend:` is load-bearing and emitted verbatim above the
  `BC.L("C2")` element in the C# array initializer. Blank-line
  grouping: preserved verbatim (legal inside both Dart list
  literals and C# array initializers). Zero escalation.

### rf-dart-property-chain-method-call-to-csharp — `Ctor().method(arg)` => `new Ctor().Method(arg)` (REUSED)

- **KB reuse (FR-012 / SC-007)**: recorded in
  `fairness_scheduler_loop_test.dart.md` for `obj.field.method(arg)`
  => `obj.Field.Method(arg)`, and reused verbatim in
  `linter_body_precommit_test.dart.md` for the same
  `Linter().lint(prog)` shape. The shape extends to constructor-on-
  the-fly-then-method-call (`Linter().lint(prog)` => `new
  Linter().Lint(prog)`) — same chaining semantics, with the addition
  that C# requires `new` at the constructor site (Microsoft Learn
  `new` operator reference). Authoritative Dart:
  `dart.dev/language/operators` (`.` member access; Dart 2+ allows
  optional `new`). Authoritative .NET: Microsoft Learn `member-
  access-operators` and `new` operator.
- **File-specific application**: `Linter()` => `new Linter()` (C#
  mandatory `new`); `.lint(prog)` => `.Lint(prog)` (PascalCased per
  the linter SUT spec). The stateless-service shape is preserved
  (no static/singleton substitution) because the linter SUT spec
  records `Linter` as a per-instance class. Zero escalation.

### rf-dart-expect-isFalse-isTrue-to-xunit-assert-true-false — `expect(x, isFalse)` => `Assert.False(x)` (REUSED)

- **KB reuse (FR-012 / SC-007)**: derived from the canonical
  `smoke_test.dart` finding `rf-dart-expect-isTrue-to-xunit-assert-
  true` and the symmetric `isFalse` extension recorded in
  `test/heap/binding_pointer_test.dart.md` and
  `linter_body_precommit_test.dart.md`. Authoritative Dart: pub.dev
  `package:matcher` `isFalse` ("matches the boolean value false").
  Authoritative .NET: xunit.net Assert API reference
  `Assert.False(bool)` ("verifies that the condition is false").
- **File-specific application**: `expect(res.ok, isFalse)` =>
  `Assert.False(res.Ok);` — direct mapping; `res.ok` is a computed
  get-only Dart getter => C# computed get-only property `res.Ok`
  per the LintResult SUT spec (`public bool Ok => Issues.Count ==
  0`). No `reason:` argument at this call site. Zero escalation.

### rf-dart-iterable-any-with-expect-reason-join-to-xunit-assert-true-linq — `expect(iter.any(p), isTrue, reason: iter.join('\n'))` => `Assert.True(iter.Any(p), string.Join("\n", iter))` (REUSED)

- **KB reuse (FR-012 / SC-007)**: this composed finding was
  authoritatively analysed and recorded in
  `linter_body_precommit_test.dart.md` for the byte-identical Dart
  shape `expect(res.issues.any((e) => e.code == '<CODE>'), isTrue,
  reason: res.issues.join('\\n'))`. This file exercises the SAME
  composed idiom with a different diagnostic-code constant
  (`'SUSPEND_ONCE_AT_END'` instead of `'BODY_BEFORE_COMMIT'`); per
  FR-012 the finding is REUSED verbatim, NOT re-derived.
- **Authoritative sources (carried forward from
  linter_body_precommit_test.dart.md)**: dart.dev `Iterable.any` (
  "Whether any element satisfies test. Iterates and stops as soon
  as test returns true."); Microsoft Learn `System.Linq.Enumerable.
  Any<T>(IEnumerable<T>, Func<T,bool>)` ("Determines whether any
  element of a sequence satisfies a condition. Stops iterating as
  soon as the result can be determined."); dart.dev `Iterable.
  join`; Microsoft Learn `String.Join<T>(string separator,
  IEnumerable<T> values)`; pub.dev `expect(actual, matcher,
  {reason})`; xunit.net `Assert.True(bool condition, string
  userMessage)`.
- **Composition rationale (reused verbatim)**: UNLIKE
  `Assert.Equal<T>` (which has no `userMessage` overload, forcing
  Dart `reason:` to be routed to an inline `// ...` comment in
  the sibling fairness specs), `Assert.True(bool, string)` DOES
  have a `userMessage` overload — so the Dart `reason:` text
  survives DIRECTLY as the second argument (no inline-comment
  routing needed).
- **Eager-vs-lazy diagnostic nuance**: BOTH languages eagerly
  evaluate the `reason:`/`userMessage` argument BEFORE the
  assertion runs (Dart `expect`'s `reason:` is a positional
  `String?`, NOT a thunk; xUnit `Assert.True`'s `userMessage` is
  `string`, NOT `Func<string>`). The `.join('\\n')` /
  `string.Join("\n", ...)` is computed even on PASSING assertions
  — semantic match.
- **Diagnostic-code coupling (this-file-specific)**: the string
  literal `'SUSPEND_ONCE_AT_END'` is a cross-file constant — the
  linter SUT-side emitter (linter.dart.md's SUSPEND_ONCE_AT_END
  rule) must emit the byte-identical string
  `"SUSPEND_ONCE_AT_END"` in C# or this test will silently degrade
  to `Any` returning `false` and the assertion failing. Same
  cross-file string-constant coupling shape as the
  `BODY_BEFORE_COMMIT` coupling in
  `linter_body_precommit_test.dart.md`; recorded here as a load-
  bearing fact.
- **Conclusion**: `Assert.True(res.Issues.Any(e => e.Code ==
  "SUSPEND_ONCE_AT_END"), string.Join("\n", res.Issues));`. Zero
  escalation.

## Notes

- No async / `Future` / `Stream` / isolate / `Completer` / `Timer`
  surface in this file — the `[Fact]` method is `void` (NOT `async
  Task`). The well-known async-Dart-vs-.NET-async nuance is
  deliberately not asserted here (does not apply to this file's
  source surface).
- No `late`, `mixin`, `extension`, generics, sealed/abstract,
  bitwise/shift, isolate — all absent. No nullable surface fires
  (the `Linter.Lint` return type is non-nullable `LintResult` per
  the SUT spec; the `res.issues` collection is non-nullable
  `List<LintIssue>` per the SUT spec).
- The file exercises the linter's diagnostic surface (`Linter.Lint`,
  `LintResult.Ok`, `LintResult.Issues`, `LintIssue.Code`,
  `LintIssue.ToString`) and the `BC` assembler facade (`BC.Prog`,
  `BC.L`, `BC.TRY`, `BC.R`, `BC.U`, `BC.SUSP`). The SUT-side
  conversion shape (class names, method names, return types,
  opcode hierarchy, BC factory return type, LintIssue.ToString
  format) is owned by the SUT specs at
  `.codeconv/conversion-specs/lib/lint/linter.dart.md`,
  `.codeconv/conversion-specs/lib/bytecode/asm.dart.md`,
  `.codeconv/conversion-specs/lib/bytecode/opcodes.dart.md`; this
  test convspec references their decisions but does not duplicate
  them.
- The intentionally-illegal bytecode-program construction is
  load-bearing for this test's purpose — the line comment `//
  Illegal extra ClauseTry after final suspend:` documents WHY the
  program is invalid (an extra `ClauseTry` block, INCLUDING a
  second `SuspendEnd`, after the terminal `SuspendEnd` — both
  forbidden by the `SUSPEND_ONCE_AT_END` rule), and the conversion
  preserves it verbatim above the `BC.L("C2")` element. Without
  that comment, a future maintainer might "fix" the apparent bug.
  The blank-line visual grouping (clause C1 prologue, terminal
  END+SUSP, illegal extra C2+END2+SUSP) is also preserved verbatim.
- The Dart `'SUSPEND_ONCE_AT_END'` string-literal code is the
  Linter-side diagnostic constant emitted by the linter SUT's
  SUSPEND_ONCE_AT_END check — codegen MUST keep the diagnostic-
  code string byte-identical between linter.cs (emitter) and this
  test (consumer), or the test will break. This is a cross-file
  string-constant coupling, not a per-file conversion concern; the
  spec records it as a load-bearing fact (same shape as the
  `BODY_BEFORE_COMMIT` coupling in
  `linter_body_precommit_test.dart.md`).
- Zero escalations: every construct is authoritative-supported on
  both sides, all seven REUSE idioms/findings from sibling specs
  (smoke_test, binding_pointer_test, fairness_scheduler_loop_test,
  linter_ok_test, linter_body_precommit_test, asm.dart.md,
  linter.dart.md, opcodes.dart.md) per FR-012 / SC-007 KB-reuse
  decision order. No NEW idioms or findings introduced — this
  file is fully covered by the existing batch KB.
