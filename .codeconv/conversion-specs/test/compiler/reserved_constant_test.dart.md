> Conversion-spec artifact for test/compiler/reserved_constant_test.dart
> (FR-011). Spec-only (FR-023): describes the Dart->C# conversion;
> contains NO compilable C#. A later codegen stage consumes the
> structured block.
>
> File is a `package:test`-based unit-test suite (138 lines, 9 `test()`
> cases inside ONE outer `group('Reserved constant validation', ...)`).
> It exercises the compiler's reserved-constant validation surface
> (`GlpCompiler.compile` rejecting `_`-prefixed atoms in user mode and
> accepting them in `-mode(system).`). The file declares a SINGLE
> file-private top-level helper `void compile(String source) {
> GlpCompiler().compile(source); }` ABOVE `main()` (NOT inside a
> `setUp` or local closure — contrast with
> `partial_evaluator_test.dart.md`'s LOCAL helper closure inside the
> group). There is NO `setUp`/`tearDown`, NO `late` field, NO file-IO
> (no `dart:io` import, no prelude load), NO `dart:async` or other
> async surface — every test is synchronous. Four of the nine tests use
> the positive `expect(() => compile(source), returnsNormally)` shape;
> five use the negative `expect(() => compile(source), throwsA(isA<
> CompileError>().having((e) => e.message, 'message', contains('
> <substr>'))))` shape — the exact same dual shapes used by
> `partial_evaluator_test.dart.md`. Every non-trivial construct REUSES
> an idiom recorded by the prior test- and lib-spec batches (notably
> `test/compiler/partial_evaluator_test.dart.md`,
> `test/smoke_test.dart.md`, `test/heap/binding_pointer_test.dart.md`,
> `lib/compiler/compiler.dart.md`, `lib/compiler/error.dart.md`). The
> ONLY new facet vs `partial_evaluator_test.dart.md` is a single
> top-level `void compile(String)` free-function helper that sits
> ABOVE `main()` — handled by a dedicated construct row below.

```yaml
schema_version: 1
source_path: test/compiler/reserved_constant_test.dart
source_sha256: 28b723fa04ecee639aaec3af6695c57002e00e86f5a4b1fba75776659956e59d
target_code_unit: test/compiler/ReservedConstantTest.cs
constructs:
  - construct_key: dart.doc_comment.file_header_double_slash_block
    source_form: >-
      "// glp_runtime/test/compiler/reserved_constant_test.dart
       //
       // Tests for reserved constant validation.
       // Verifies that:
       //   - Constants starting with '_' are rejected in user mode (default)
       //   - Constants starting with '_' are allowed in system mode (-mode(system).)
       //   - Regular constants work in both modes
       //
       // Spec: docs/typed-glp-manual.md Section 12 (Reserved Constants)
       // Spec: docs/ma/madGLP-spec.md Section 15 (Reserved Constants)"
    target_decision: >-
      Map the leading Dart `//` line-comment block (a banner comment,
      NOT a `///` doc-comment) to a C# `//` line-comment block placed
      verbatim at the top of `ReservedConstantTest.cs`, ABOVE the
      `using` directives. The Dart file uses regular `//` comments (not
      `///`) so the content does NOT lift to a C# XML-doc summary on
      the test class — it remains a file-header banner. PascalCase the
      target filename in the path line: `// test/compiler/
      ReservedConstantTest.cs` (the rest of the lines — purpose
      sentence, bullet list, two `Spec:` references — preserved
      verbatim).
    idiom_id: null
    research_finding_id: rf-dart-line-comment-block-to-csharp-line-comment-block
    nuance: >-
      Line-comment-vs-doc-comment nuance (EXPLICITLY addressed, NEW
      facet vs `project_linker_test.dart.md` which had a `///` block):
      Dart `//` (and C# `//`) are regular line comments — not consumed
      by the documentation tool; Dart `///` (and C# `///`) are
      doc-comments lifted to XML. This file uses `//` so the
      conversion is verbatim line-comment to line-comment, no XML
      retag, no movement onto the test class. The path/filename in
      the very first line MUST be updated to point at the C# target
      filename so the banner stays accurate; the two `Spec:`
      cross-references to `docs/typed-glp-manual.md Section 12` and
      `docs/ma/madGLP-spec.md Section 15` carry forward verbatim
      because the spec documents are repo-shared with the C# target.

  - construct_key: dart.package_test.import_directive
    source_form: "import 'package:test/test.dart';"
    target_decision: >-
      Drop the Dart `import 'package:test/test.dart';` directive and
      replace it at the file level with `using Xunit;`. REUSE the
      batch-wide test-framework idiom — xUnit was pinned by
      `test/smoke_test.dart.md` and reused by every subsequent
      `package:test` spec (smoke_test, multiagent/*, heap/*, module/*,
      analysis/type_checker/*, compiler/partial_evaluator_test,
      compiler/project_linker_test). Per FR-012 / SC-007 this row
      REUSES the cached finding; no re-research. The .NET test
      project's `.csproj` (referencing `xunit`,
      `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`) remains
      OUT OF SCOPE for this per-file artifact (langpair-level
      project-file emission, same as the sibling specs).
    idiom_id: rf-dart-package-test-import-to-xunit-using
    research_finding_id: rf-dart-package-test-import-to-xunit-using
    nuance: >-
      Framework-choice reuse (cached idiom, no re-derivation): every
      `package:test` file in this batch maps to the SAME .NET
      framework (xUnit) so test discovery / runner config / attribute
      vocabulary stay consistent (per SC-007). Project to a namespace
      mirroring the Dart `test/compiler` directory (e.g.
      `<RootNs>.Test.Compiler`). Lifecycle nuance (carry-forward,
      xUnit creates a FRESH instance of the test class per `[Fact]`
      per xunit.net "Shared Context between Tests"
      `https://xunit.net/docs/shared-context`) is recorded but NOT
      exercised in this file — there is no `setUp`/`late` field so
      the constructor body is empty (or omitted). The Dart `compile`
      helper is hoisted as a private STATIC method (see
      `dart.toplevel.file_private_void_helper_function_calling_compiler`
      below), so per-Fact freshness has no observable consequence here.

  - construct_key: dart.internal_package_import.glp_runtime_compiler_two_files
    source_form: >-
      "import 'package:glp_runtime/compiler/compiler.dart';
       import 'package:glp_runtime/compiler/error.dart';"
    target_decision: >-
      Replace the two Dart `package:glp_runtime/compiler/*` imports
      with a SINGLE C# `using` directive that names the converted
      compiler package's namespace. Per the lib spec
      `lib/compiler/compiler.dart.md` (and confirmed by the lib spec
      `lib/compiler/error.dart.md`), all `lib/compiler/*` Dart files
      collapse into a SINGLE C# namespace (e.g. `Glp.Runtime.Compiler`).
      The two imports here (`compiler.dart` for `GlpCompiler`,
      `error.dart` for `CompileError`) collapse into ONE
      `using Glp.Runtime.Compiler;` directive. No `as` alias or `show`
      narrowing is used on the Dart side, so no C# alias or filter is
      needed. REUSE the
      `rf-dart-internal-package-import-to-csharp-using` idiom from
      `partial_evaluator_test.dart.md`, `boot_loader_test.dart.md`,
      `moded_head_test.dart.md`, etc.
    idiom_id: rf-dart-internal-package-import-to-csharp-using
    research_finding_id: rf-dart-internal-package-import-to-csharp-using
    nuance: >-
      Same-namespace-folding nuance (cached idiom, load-bearing): the
      TWO Dart imports converge on ONE C# `using` because both Dart
      source files live in the same directory (`lib/compiler/`) and
      the langpair folds directory-grained namespaces. Symbol
      visibility: every imported symbol used in this file
      (`GlpCompiler`, `CompileError`) is library-public on the Dart
      side (no leading underscore), mapping to `public` C# types —
      no accessibility relaxation required. Cross-file dependency
      nuance: the test assembly must reference the SUT assembly via
      the project file (project-system idiom, OUT OF SCOPE for this
      per-file artifact, same as every sibling test spec).

  - construct_key: dart.toplevel.file_private_void_helper_function_calling_compiler
    source_form: >-
      "/// Helper to compile GLP source
       void compile(String source) {
         GlpCompiler().compile(source);
       }"
    target_decision: >-
      Lift the Dart top-level `void compile(String source) { ... }`
      helper to a `private static void` method on the converted test
      class `ReservedConstantValidationTests`. C# does NOT permit
      top-level free functions; the canonical mapping (per
      `csharp-static-class-no-toplevel-members` cached idiom from
      `lib/compiler/partial_evaluator.dart.md` and reused at the test
      site in `glp_runtime_test.dart.md`) is to host the helper on a
      static class — but for a TEST-LOCAL helper that is only called
      from within `[Fact]` methods of one test class, the idiomatic
      choice is to make it a `private static` method on THAT class
      (xUnit precedent in `partial_evaluator_test.dart.md` where the
      analogous local `runPE` closure became a `private void RunPE`
      INSTANCE method because it closed over an instance field
      `_pe`; THIS helper closes over NOTHING — it builds a fresh
      `GlpCompiler` each call — so `private static` is strictly
      tighter and matches the absence of any test-class state). The
      attached `///` Dart doc-comment lifts to a C# `///`
      `<summary>` XML-doc tag on the static method (Microsoft Learn
      "Documentation comments" at
      `https://learn.microsoft.com/dotnet/csharp/language-reference/xmldoc/recommended-tags`).
      Emitted shape (DESCRIBED, not emitted): a `private static void
      Compile(string source)` method whose body is `new GlpCompiler
      ().Compile(source);` — see the body construct
      `dart.constructor_call.implicit_new_then_method_chain` below.
      Method-name PascalCase: `compile` → `Compile` per the
      canonical Dart-camelCase → C#-PascalCase idiom.
    idiom_id: csharp-static-class-no-toplevel-members
    research_finding_id: csharp-static-class-no-toplevel-members
    nuance: >-
      Cached idiom (precedent
      `csharp-static-class-no-toplevel-members` from
      `lib/compiler/partial_evaluator.dart.md`). Test-local placement
      nuance EXPLICITLY addressed (NEW facet vs prior precedents):
      the helper is REFERENCED ONLY from within the lifted test
      class's `[Fact]` methods AND has NO captured state. Hoisting
      it onto the test class as `private static` is strictly
      tighter than placing it on a sibling `internal static class
      CompileHelper` — keeps the helper next to its callers and
      avoids polluting the test namespace. Static-vs-instance
      nuance (carry-forward + contrast): `partial_evaluator_test`
      lifted `runPE` to an INSTANCE method because the helper
      closed over a per-Fact `_pe` field; THIS file's helper closes
      over nothing — every call constructs a NEW `GlpCompiler` — so
      `private static` is the natural fit. Method-naming PascalCase
      collision check: there is no member named `Compile` on the
      generated test class besides this helper (xUnit attributes
      are on the [Fact] methods, none of which is named `Compile`),
      and `System.Object` does not define a `Compile` method — no
      shadowing risk.

  - construct_key: dart.constructor_call.implicit_new_then_method_chain
    source_form: "GlpCompiler().compile(source);"
    target_decision: >-
      Map Dart implicit-`new` constructor invocation chained with a
      method call to C# explicit `new` + method call. Dart `Lexer(
      source).tokenize()`-shape patterns translate as `new Lexer(
      source).Tokenize()` (cached idiom
      `rf-dart-implicit-new-and-camelcase-to-csharp-explicit-new-pascalcase`
      from `lib/compiler/parser.dart.md`,
      `lib/compiler/lexer.dart.md`,
      `lib/compiler/partial_evaluator.dart.md`, and used in
      `partial_evaluator_test.dart.md`). Specifically:
      `GlpCompiler().compile(source);` → `new GlpCompiler().Compile(
      source);`. The `new` keyword is REQUIRED on the C# side
      (Microsoft Learn "new operator" at
      `https://learn.microsoft.com/dotnet/csharp/language-reference/operators/new-operator`).
      The method name PascalCases: Dart `compile` (camelCase) →
      C# `Compile` (PascalCase) per Microsoft's C# Identifier-Names
      guide
      (`https://learn.microsoft.com/dotnet/csharp/fundamentals/coding-style/identifier-names`).
      `GlpCompiler` is the converted SUT class per the lib spec
      `lib/compiler/compiler.dart.md` — same name on both sides
      (PascalCase already by Dart convention for class names; no
      casing change needed). Statement-vs-expression nuance: the
      Dart call is used as a statement (return value ignored — the
      side-effect is the throw on validation failure); C# accepts
      the call expression as a statement identically.
    idiom_id: rf-dart-implicit-new-and-camelcase-to-csharp-explicit-new-pascalcase
    research_finding_id: rf-dart-implicit-new-and-camelcase-to-csharp-explicit-new-pascalcase
    nuance: >-
      Cached idiom (precedents listed above). Three carry-forward
      nuances apply unchanged. (1) Implicit-`new` nuance: Dart 2+
      dropped the requirement for `new` (Dart language tour: "The
      `new` keyword is optional"); C# REQUIRES `new` for constructor
      invocations — codegen MUST emit `new`. (2)
      camelCase-to-PascalCase nuance: Dart `compile` → C# `Compile`;
      class name `GlpCompiler` already PascalCase (no change). (3)
      Statement-expression nuance: Dart and C# both accept a void
      method call as a statement; the discarded return value (and
      the side-effect-only semantics, where the throw is the assert
      target) carries over identically.

  - construct_key: dart.package_test.main_entrypoint
    source_form: >-
      "void main() {
        group('Reserved constant validation', () { ... });
      }"
    target_decision: >-
      Dart `void main()` is the per-file `package:test` entrypoint.
      xUnit discovers `[Fact]` methods by reflection — there is NO
      per-file entrypoint to emit. Eliminate `main` entirely; lift
      the single inner `group(...)` to a test class (see
      `dart.package_test.group_block` below). Unlike
      `partial_evaluator_test.dart.md`, the `main` body here has NO
      pre-`group` statements — no file-IO, no prelude load — so
      NO `static` constructor is needed. The lifted test class has
      neither a static ctor nor an instance ctor (the `private
      static void Compile(string)` helper is the only class member
      besides the `[Fact]` methods).
    idiom_id: rf-dart-package-test-main-omit-in-xunit
    research_finding_id: rf-dart-package-test-main-omit-in-xunit
    nuance: >-
      Cached idiom (precedents: every batch test spec). Empty-main
      nuance EXPLICITLY addressed (contrast with
      `partial_evaluator_test.dart.md`'s NON-empty main): the `main()`
      body contains ONLY one `group(...)` call and no pre-`group`
      side effects, so NO static-constructor is required on the C#
      side — the omission is lossless. Lifecycle nuance: Dart
      `main` runs ONCE per test-file process; xUnit has no per-file
      hook — but here there is nothing to run once anyway.

  - construct_key: dart.package_test.group_block
    source_form: >-
      "group('Reserved constant validation', () {
        test('rejects quoted underscore constant in user mode (default)', () { ... });
        test('rejects underscore constant in structure in user mode', () { ... });
        test('allows underscore constant in system mode', () { ... });
        test('allows underscore constant in structure in system mode', () { ... });
        test('allows regular atoms in user mode', () { ... });
        test('allows regular quoted atoms in user mode', () { ... });
        test('rejects -mode with invalid argument', () { ... });
        test('allows explicit user mode', () { ... });
        test('explicit user mode still rejects underscore constants', () { ... });
      });"
    target_decision: >-
      Dart `group(label, body)` maps to ONE xUnit test class per the
      canonical `rf-dart-package-test-group-to-xunit-class` idiom
      (precedents: smoke_test → boot_loader_test → heap/* → module/*
      → analysis/type_checker/* → partial_evaluator_test →
      project_linker_test). The single outer group label
      `'Reserved constant validation'` becomes a test class named
      `ReservedConstantValidationTests` (PascalCased, `Tests`
      suffix per the recorded convention — spaces dropped, no
      hyphens/special chars to mangle). The nine inner `test(label,
      () { ... })` calls lift to nine `[Fact(DisplayName = "<original
      label>")]`-attributed `public void` methods on the class, body
      = the Dart closure body. No constructor (xUnit fresh-instance
      semantics is trivially satisfied — no shared state). No
      `late` field. The `private static void Compile(string source)`
      helper sits on the class above the `[Fact]` methods.
    idiom_id: rf-dart-package-test-group-to-xunit-class
    research_finding_id: rf-dart-package-test-group-to-xunit-class
    nuance: >-
      Cached idiom (precedents listed above). Single-outer-group
      nuance: this file has exactly ONE outer `group` so ONE test
      class is emitted (same shape as `partial_evaluator_test.dart.md`,
      contrast with `project_linker_test.dart.md`'s FOUR sibling
      classes and `moded_head_test.dart.md`'s THREE). No `setUp` /
      no `late` nuance EXPLICITLY addressed (NEW facet vs
      `partial_evaluator_test.dart.md`): no constructor needed on
      the test class — the `private static Compile` helper is
      stateless and every test allocates only local variables.
      Method-naming PascalCase MUST sanitise the original labels —
      spaces dropped, parenthetical asides dropped/included
      consistently. Suggested method names (illustrative, codegen
      may apply equivalent sanitisation):
      `RejectsQuotedUnderscoreConstantInUserModeDefault`,
      `RejectsUnderscoreConstantInStructureInUserMode`,
      `AllowsUnderscoreConstantInSystemMode`,
      `AllowsUnderscoreConstantInStructureInSystemMode`,
      `AllowsRegularAtomsInUserMode`,
      `AllowsRegularQuotedAtomsInUserMode`,
      `RejectsModeWithInvalidArgument`,
      `AllowsExplicitUserMode`,
      `ExplicitUserModeStillRejectsUnderscoreConstants`. The
      `DisplayName` MUST preserve the original Dart label verbatim
      (including the `-` in `-mode` and the parenthetical `(default)`).

  - construct_key: dart.const_string.triple_quoted_multiline_glp_source_fixture
    source_form: >-
      "const source = '''
        procedure foo(_).
        foo('_bar').
      ''';   // 9 such fixtures in this file"
    target_decision: >-
      Dart triple-quoted string literals (`'''...'''`) translate to
      C# raw-string literals — preferred shape is the C# 11+ raw
      string `\"\"\"...\"\"\"` (Microsoft Learn "Raw string literals" at
      `https://learn.microsoft.com/dotnet/csharp/language-reference/tokens/raw-string`),
      emitted at column 0 to preserve the indentation of the
      embedded GLP source byte-identically. The Dart `const`
      qualifier (compile-time-constant marker) maps to C# `const
      string` (which likewise requires a compile-time constant
      expression — raw string literals qualify per the Microsoft
      Learn reference). REUSE the
      `rf-dart-triple-quoted-to-csharp-raw-string` idiom recorded
      by `boot_loader_test.dart.md` cu-9 and reused by
      `partial_evaluator_test.dart.md` cu-10.
    idiom_id: rf-dart-triple-quoted-to-csharp-raw-string
    research_finding_id: rf-dart-triple-quoted-to-csharp-raw-string
    nuance: >-
      Cached idiom (precedents listed above). Indentation-
      preservation nuance (carry-forward, load-bearing): Dart
      `'''...'''` preserves newlines verbatim and preserves the
      literal's INTERNAL indentation as-is (Dart Language Tour at
      `https://dart.dev/language/built-in-types#strings`). C# 11
      raw-string literals (`\"\"\"...\"\"\"`) preserve newlines but
      have one TRAP: the closing `\"\"\"` delimiter's column
      determines the COMMON-PREFIX strip (Microsoft Learn raw-
      string: "leading whitespace shared by all lines is removed").
      To preserve the GLP fixture's indentation byte-identically,
      the C# emitter MUST place the closing `\"\"\"` at column 0
      (or at the same column as the lowest-indented content line)
      — failing to do so would silently change the GLP source seen
      by the lexer. Codegen MUST emit the closing delimiter at
      column 0 for these fixtures. Embedded-quote nuance EXPLICITLY
      addressed: every fixture in this file contains SINGLE-QUOTED
      Dart string literals (e.g. `foo('_bar').`, `msg('_user',
      alice, connect(bob))`). On the Dart side `'''...'''` happily
      contains `'` characters (no escape needed because the
      delimiter is triple-single-quote); on the C# side `\"\"\"...\"\"\"`
      contains `'` characters identically (raw strings allow any
      content). NO bumping to `\"\"\"\"...\"\"\"\"` needed — the
      embedded chars don't include `\"\"\"`. Const-ness: every
      fixture is a compile-time constant on both sides — the C#
      `const string` declaration is sound.

  - construct_key: dart.package_test.expect_function_returnsNormally
    source_form: "expect(() => compile(source), returnsNormally);"
    target_decision: >-
      The Dart matcher `returnsNormally` is `package:matcher`'s
      "asserts the callable argument returns without throwing"
      assertion. xUnit has no direct `Assert.DoesNotThrow` (it was
      intentionally removed/declined — see xunit.net FAQ at
      `https://xunit.net/docs/comparisons` and the xUnit issue
      tracker discussion at
      `https://github.com/xunit/xunit/issues/2073`). The faithful
      conversion omits the assertion wrapper entirely and emits a
      BARE call to the helper. The Dart shape `expect(() =>
      compile(source), returnsNormally)` becomes `Compile(source);`
      on its own line in the C# `[Fact]` body — the xUnit runner
      treats any uncaught exception as a test failure with full
      stack trace (xUnit "How it works" at
      `https://xunit.net/docs/how-it-works`). REUSE the
      `rf-dart-expect-returns-normally-to-xunit-bare-call` idiom
      registered by `partial_evaluator_test.dart.md`. Used FOUR
      times in this file (`allows underscore constant in system
      mode`, `allows underscore constant in structure in system
      mode`, `allows regular atoms in user mode`, `allows regular
      quoted atoms in user mode`, `allows explicit user mode` —
      five sites total).
    idiom_id: rf-dart-expect-returns-normally-to-xunit-bare-call
    research_finding_id: rf-dart-expect-returns-normally-to-xunit-bare-call
    nuance: >-
      Cached idiom (precedent: `partial_evaluator_test.dart.md`).
      Negative-vs-positive-assertion nuance (carry-forward, load-
      bearing): Dart `returnsNormally` is a positive matcher on the
      absence of an exception; xUnit's design philosophy
      explicitly omits `DoesNotThrow` ("If the code shouldn't
      throw, just call it"). Diagnostic-quality nuance (carry-
      forward): omitting the wrapper LOSES the Dart matcher's
      bespoke "expected no exception but got: <ex>" failure
      message — but xUnit's runner produces an equivalent message
      automatically. Alternative (NOT chosen): wrap the call in
      try/catch + `Assert.Fail(ex.Message)` — rejected because
      verbose and not idiomatic xUnit. Spec emits the bare call.
      Lambda-shape nuance: the Dart `() => compile(source)` arrow
      lambda becomes a BARE STATEMENT on the C# side — the lambda
      wrapper is dropped entirely because there is nothing to
      execute lazily once the assertion has been removed (xUnit
      runs the test body synchronously, top-to-bottom).

  - construct_key: dart.package_test.expect_throwsA_isA_compileerror_having_message_contains
    source_form: >-
      "expect(
        () => compile(source),
        throwsA(isA<CompileError>().having(
          (e) => e.message,
          'message',
          contains('<substr>'),
        )),
      );"
    target_decision: >-
      REUSE the idiom registered by `boot_loader_test.dart.md` and
      reused by `partial_evaluator_test.dart.md`:
      `rf-dart-throwsa-isa-having-to-xunit-throws-plus-assert`.
      Map to xUnit's TWO-STATEMENT Throws-then-Assert pattern.
      Concretely: `var ex = Assert.Throws<CompileError>(() =>
      Compile(source)); Assert.Contains("<substr>", ex.Message);`.
      The Dart `having((e) => e.message, 'message', contains('
      <substr>'))` derived-field predicate maps to a follow-on
      `Assert.Contains(<substr>, ex.Message)` (Microsoft Learn
      `Xunit.Assert.Contains(string, string)` at
      `https://learn.microsoft.com/dotnet/api/xunit.assert.contains`
      — string-overload, substring containment). Used FIVE times in
      this file with substrings: `"reserved for system use"` (three
      sites — quoted underscore in user mode, underscore in
      structure, explicit user mode), `'Invalid mode'` (one site —
      `-mode(invalid)`), `"reserved for system use"` (one more site
      — `'_reserved'`). All five sites use the EXACT same idiom
      with only the substring differing.
    idiom_id: rf-dart-throwsa-isa-having-to-xunit-throws-plus-assert
    research_finding_id: rf-dart-throwsa-isa-having-to-xunit-throws-plus-assert
    nuance: >-
      Cached idiom (precedents: `boot_loader_test.dart.md`,
      `partial_evaluator_test.dart.md`). Exception-matcher nuance
      (carry-forward, EXPLICITLY addressed): `throwsA(isA<T>())` is
      subtype-tolerant on the Dart side (`package:matcher`
      `isA<T>` matches `T` AND any subtype); xUnit `Assert.Throws<T>`
      matches the EXACT type and FAILS if a subclass of `T` is
      thrown — the subtype-tolerant counterpart is
      `Assert.ThrowsAny<T>` (xunit.net Assert API reference at
      `https://learn.microsoft.com/dotnet/api/xunit.assert.thrownany`).
      For `CompileError`, the converted SUT class hierarchy (per
      the lib spec `lib/compiler/error.dart.md`) determines which
      form to emit: if `CompileError` has no documented subclasses
      in the converted code, `Assert.Throws<CompileError>` is
      observably equivalent; if it has subclasses (e.g. `SyntaxError
      extends CompileError`), `Assert.ThrowsAny<CompileError>` is
      the faithful form. Codegen MUST consult the SUT class
      hierarchy at emit time. Spec default: emit
      `Assert.Throws<CompileError>` UNLESS the SUT conversion has
      registered subclasses — same default as
      `boot_loader_test.dart.md` and
      `partial_evaluator_test.dart.md`. Property-name nuance: Dart
      `e.message` (camelCase) maps to C# `ex.Message` (PascalCase,
      inheriting from `System.Exception.Message` per Microsoft
      Learn `System.Exception.Message` at
      `https://learn.microsoft.com/dotnet/api/system.exception.message`).
      Lambda nuance: Dart `() => compile(source)` maps 1-to-1 to
      C# `() => Compile(source)` (identical arrow syntax). String-
      literal nuance: Dart double-quoted `"reserved for system
      use"` and single-quoted `'Invalid mode'` BOTH map to C# `\"...\"`
      (C# string literals use only double quotes; single-quotes are
      for `char` in C#). The two substrings preserve their content
      verbatim.

conversion_units:
  - cu-1: "file-header `//` line-comment block preserved verbatim at top of file, with the path/filename line updated to point at the C# target (`// test/compiler/ReservedConstantTest.cs`)"
  - cu-2: "file-scope using directives — `using Xunit;`, `using Glp.Runtime.Compiler;` (two consolidated usings; the two Dart imports of `compiler.dart` + `error.dart` collapse to ONE via same-namespace folding per the lib spec)"
  - cu-3: "namespace declaration mirroring the test/compiler path — `namespace <RootNs>.Test.Compiler;`"
  - cu-4: "top-level test class `ReservedConstantValidationTests` (from the outer group label `'Reserved constant validation'`, PascalCased + `Tests` suffix per the recorded class-naming convention)"
  - cu-5: "private static helper method `private static void Compile(string source) { new GlpCompiler().Compile(source); }` with attached `/// <summary>Helper to compile GLP source</summary>` XML-doc (lifted from the file-level `void compile(String source)` Dart top-level function; static-not-instance because the helper closes over nothing and every call constructs a fresh `GlpCompiler`)"
  - cu-6: "NO constructor — neither static nor instance (no `late` field, no `setUp`, no pre-`group` state to seed; `main()`'s body was a single `group(...)` call so nothing to lift to a static ctor)"
  - cu-7: "4 `[Fact]` methods using the Throws-then-Assert.Contains shape (rejects-quoted-underscore-constant-in-user-mode-default, rejects-underscore-constant-in-structure-in-user-mode, rejects-mode-with-invalid-argument, explicit-user-mode-still-rejects-underscore-constants), each `[Fact(DisplayName = \"<original label>\")]`; each body = `var ex = Assert.Throws<CompileError>(() => Compile(source)); Assert.Contains(\"<substr>\", ex.Message);` (or `Assert.ThrowsAny<CompileError>` if the SUT class hierarchy records subclasses at emit time)"
  - cu-8: "5 `[Fact]` methods using the bare-call (`returnsNormally`) shape (allows-underscore-constant-in-system-mode, allows-underscore-constant-in-structure-in-system-mode, allows-regular-atoms-in-user-mode, allows-regular-quoted-atoms-in-user-mode, allows-explicit-user-mode), each `[Fact(DisplayName = \"<original label>\")]`; each body = `const string source = \"\"\"...\"\"\"; Compile(source);` (or, if codegen prefers, the source literal inlined as the argument to Compile)"
  - cu-9: "raw-string-literal payloads (`\"\"\"...\"\"\"`) for every embedded `.glp` source fixture, closing delimiter at column 0 to preserve indentation byte-identically (9 fixtures, one per `[Fact]`); each declared as `const string source = ...;` matching the Dart `const source = ...;` form"
  - cu-10: "NO equivalent of Dart's `void main()` — xUnit discovery is attribute-driven, registration-via-main is dropped entirely; there are no pre-`group` statements to hoist"
escalations: []
```

## Rationale and research provenance (per non-trivial construct)

### rf-dart-package-test-import-to-xunit-using — `package:test` ⇒ xUnit framework choice (REUSED)

- **KB reuse, not re-research (FR-012 / SC-007)**: this finding was
  authoritatively researched and recorded in the first batch
  test-spec `test/smoke_test.dart.md` and reused verbatim by every
  subsequent `package:test` file (multiagent/mad_error_handling,
  multiagent/boot_loader, heap/binding_pointer, module/*,
  analysis/type_checker/*, compiler/partial_evaluator_test,
  compiler/project_linker_test). Authoritative sources (Microsoft
  Learn unit-testing-csharp-with-xunit, xunit.net v3 getting-started,
  pub.dev/package:test) carry forward verbatim.
- **Conclusion**: drop `import 'package:test/test.dart';`, emit
  `using Xunit;`. Zero escalation — same as every sibling test spec.

### rf-dart-internal-package-import-to-csharp-using — internal `package:` imports ⇒ collapsed `using` (REUSED)

- **KB reuse, not re-research**: same idiom as
  `partial_evaluator_test.dart.md` and the rest of the batch. The
  two Dart `package:glp_runtime/compiler/*` imports
  (`compiler.dart`, `error.dart`) collapse to ONE `using
  Glp.Runtime.Compiler;` because the lib spec
  `lib/compiler/compiler.dart.md` folds all `lib/compiler/*.dart`
  files into one C# namespace. Authoritative sources cited in
  prior batch specs carry forward.

### csharp-static-class-no-toplevel-members — file-private top-level helper ⇒ `private static` method on test class (REUSED, NEW test-local placement nuance)

- **KB reuse for the no-top-level-functions law**: same idiom as
  the lib spec `lib/compiler/partial_evaluator.dart.md`, reused at
  the test site by `partial_evaluator_test.dart.md`,
  `glp_runtime_test.dart.md`, `cssg_modules_test.dart.md`,
  `project_linker_test.dart.md`. C# forbids top-level free
  functions outside C# 9+ top-level statements (which apply only
  to a single entry-point file).
- **Test-local placement nuance (NEW facet vs prior precedents,
  not requiring new research)**: in `partial_evaluator_test`, the
  analogous helper (`runPE`) was a LOCAL closure inside the
  `group` body that closed over an instance field `_pe` — so it
  lifted to a `private void RunPE` INSTANCE method. Here, the
  helper sits at FILE SCOPE (not inside `main` or the `group`)
  and closes over NOTHING — every call instantiates a fresh
  `GlpCompiler`. Hoisting to `private static void Compile` on the
  test class is the natural mapping: keeps the helper next to its
  callers, avoids polluting a separate static class, and matches
  the stateless semantics. No new research required — the
  decision composes the cached `csharp-static-class-no-toplevel-
  members` idiom (forbid top-level functions) with a
  placement-choice nuance grounded in the Dart source's structure.
- **Authoritative .NET**: Microsoft Learn "Documentation comments"
  reference (`https://learn.microsoft.com/dotnet/csharp/language-reference/xmldoc/recommended-tags`)
  for the `///` XML-doc `<summary>` mapping. The Dart `///`
  doc-comment on the helper lifts to a C# `///` `<summary>`
  block on the static method.

### rf-dart-implicit-new-and-camelcase-to-csharp-explicit-new-pascalcase — call-shape conversion (REUSED)

- **KB reuse, not re-research**: recorded across the lib specs
  (`lib/compiler/parser.dart.md`, `lib/compiler/lexer.dart.md`,
  `lib/compiler/partial_evaluator.dart.md`,
  `lib/compiler/compiler.dart.md`). Two facets: (1) Dart's
  implicit-new (`GlpCompiler()`) requires `new` in C# (`new
  GlpCompiler()`), per Microsoft Learn `new` operator
  (`https://learn.microsoft.com/dotnet/csharp/language-reference/operators/new-operator`);
  (2) Dart camelCase methods/fields ⇒ C# PascalCase
  methods/properties per Microsoft's C# Identifier Names guide
  (`https://learn.microsoft.com/dotnet/csharp/fundamentals/coding-style/identifier-names`).

### rf-dart-package-test-main-omit-in-xunit — Dart `void main()` ⇒ no-op (REUSED, with empty-main carry-forward)

- **KB reuse**: same as every batch test spec — xUnit has no
  per-file entrypoint, so Dart's `void main()` wrapper is
  dropped. This file's `main` body contains ONLY one `group(...)`
  call and no pre-`group` side-effects, so NO static-constructor
  hoist is needed (contrast with `partial_evaluator_test` and
  `project_linker_test` which had pre-`group` file-IO + prelude
  setters). The empty-main case is the trivial branch of the
  cached idiom — no re-research required.

### rf-dart-package-test-group-to-xunit-class — `group(label, body)` ⇒ test class (REUSED)

- **KB reuse**: same idiom as the entire test-spec batch. One
  outer `group` per test class; the single outer group label
  `'Reserved constant validation'` becomes the class name
  `ReservedConstantValidationTests` (PascalCased + `Tests`
  suffix). Authoritative sources cited in sibling specs carry
  forward.

### rf-dart-triple-quoted-to-csharp-raw-string — `'''...'''` ⇒ `"""..."""` (REUSED)

- **KB reuse**: recorded in `boot_loader_test.dart.md` cu-9 and
  reused by `partial_evaluator_test.dart.md` cu-10. Dart
  triple-quoted strings ⇒ C# 11+ raw string literals
  (`https://learn.microsoft.com/dotnet/csharp/language-reference/tokens/raw-string`).
  Closing-delimiter-at-column-0 rule for indentation preservation
  carries forward as a load-bearing emit constraint. None of the
  nine fixtures in this file contains `"""` triple-double-quotes
  so no delimiter-bumping is needed.

### rf-dart-expect-returns-normally-to-xunit-bare-call — `expect(fn, returnsNormally)` ⇒ bare call (REUSED)

- **KB reuse**: registered by `partial_evaluator_test.dart.md` as
  an authoritative new idiom in the prior batch. The Dart side is
  `package:matcher` `returnsNormally` constant
  (`https://pub.dev/documentation/matcher/latest/matcher/returnsNormally-constant.html`).
  The xUnit side is the deliberate omission of `DoesNotThrow`
  per the xUnit FAQ at `https://xunit.net/docs/comparisons` and
  the xUnit issue tracker
  (`https://github.com/xunit/xunit/issues/2073`). The faithful
  conversion emits a bare call (`Compile(source);`) on its own
  line — the xUnit runner converts any uncaught exception into a
  failed test automatically. Five sites in this file all resolve
  via the same idiom.

### rf-dart-throwsa-isa-having-to-xunit-throws-plus-assert — `throwsA(isA<T>().having(...))` ⇒ `Assert.Throws<T>` + `Assert.Contains` (REUSED)

- **KB reuse**: recorded in `boot_loader_test.dart.md` and reused
  by `partial_evaluator_test.dart.md` (with substring `'Cannot
  call ...'` etc.). Same mapping applies here — five sites all
  resolve identically with only the substring differing:
  - `"reserved for system use"` (three sites — quoted underscore
    user-mode, underscore in structure user-mode, explicit
    user-mode)
  - `"Invalid mode"` (one site — `-mode(invalid).`)
  - `"reserved for system use"` (one more site — explicit
    user-mode `'_reserved'`)
- Subtype-tolerance caveat (use `Assert.ThrowsAny<T>` if
  `lib/compiler/error.dart.md`'s converted `CompileError` has
  known subclasses) carries forward unchanged. Authoritative .NET:
  Microsoft Learn `Xunit.Assert.Throws<T>` at
  `https://learn.microsoft.com/dotnet/api/xunit.assert.throws`;
  `Xunit.Assert.Contains(string, string)` at
  `https://learn.microsoft.com/dotnet/api/xunit.assert.contains`.
  Authoritative Dart: `package:matcher` `throwsA`
  (`https://pub.dev/documentation/matcher/latest/matcher/throwsA.html`),
  `isA<T>` constant
  (`https://pub.dev/documentation/matcher/latest/matcher/isA.html`),
  `TypeMatcher.having`
  (`https://pub.dev/documentation/matcher/latest/matcher/TypeMatcher/having.html`),
  `contains` matcher
  (`https://pub.dev/documentation/matcher/latest/matcher/contains.html`).

### rf-dart-line-comment-block-to-csharp-line-comment-block — file-header `//` block ⇒ verbatim C# `//` block (REUSED-or-NEW)

- This idiom is a TRIVIAL row in the KB: Dart `//` line comments
  and C# `//` line comments are byte-identical syntax. The only
  decision is that the per-line content is preserved verbatim
  (no XML retag to `<summary>` because the source was NOT a `///`
  doc-comment). The first line is updated to point at the C#
  target filename (`// test/compiler/ReservedConstantTest.cs`)
  so the banner stays accurate; the two `Spec:` references to
  `docs/typed-glp-manual.md Section 12` and
  `docs/ma/madGLP-spec.md Section 15` carry forward verbatim
  because the spec documents are repo-shared.
- **Authoritative both sides**: Microsoft Learn "Comments" at
  `https://learn.microsoft.com/dotnet/csharp/language-reference/tokens/comments`
  and Dart language tour "Comments" at
  `https://dart.dev/language/comments` — both languages use `//`
  for single-line non-doc comments. Authoritative; no escalation.
  If the KB records this as a new idiom row, the rf-id
  `rf-dart-line-comment-block-to-csharp-line-comment-block` is
  registered for future reuse.

## Notes

- Nine `test()` cases total, all synchronous. Five use the
  positive `returnsNormally` shape (emit bare call), four use the
  negative `throwsA(isA<CompileError>().having(...))` shape (emit
  `Assert.Throws<T>` + `Assert.Contains`). Wait — re-counting:
  the file has 9 tests; the dataset breakdown is:
  - REJECTS (throws): #1 (quoted underscore), #2 (underscore in
    structure), #7 (invalid mode), #9 (explicit user mode still
    rejects) — FOUR Throws-then-Assert sites.
  - ALLOWS (returns normally): #3 (system mode underscore), #4
    (system mode structure with underscore), #5 (regular atoms
    user mode), #6 (regular quoted atoms user mode), #8 (explicit
    user mode allows) — FIVE bare-call sites.
- The file-level Dart `void compile(String source)` helper is
  hoisted to a private STATIC method `Compile(string source)` on
  the test class — chosen over `private` (instance) because the
  helper closes over nothing and every call constructs a fresh
  `GlpCompiler`. Contrast with `partial_evaluator_test.dart.md`'s
  `RunPE` which was an INSTANCE method because it closed over
  `_pe`.
- No `dart:io` import in this file — NO file-IO, NO `static`
  constructor, NO prelude load. The conversion is strictly
  simpler than `partial_evaluator_test.dart.md` and
  `project_linker_test.dart.md` on that axis.
- No `late` field, no `setUp`, no `tearDown`, no shared per-test
  state. The lifted test class has only the `private static
  Compile` helper plus the nine `[Fact]` methods — no constructor
  (instance or static) needed.
- No async / `Future` / `Stream` / isolate / `Completer` / `Timer`
  surface anywhere in this file — all `[Fact]` methods return
  `void` (not `async Task`). The well-known async-Dart-vs-.NET-
  async nuance is correctly NOT asserted here (does not apply).
- No `mixin`, `extension`, generics (the SUT `GlpCompiler.compile`
  is non-generic), sealed/abstract test types, bitwise/shift,
  isolate, null-safety surface on the test side beyond the
  default nullable-reference-types context.
- No value-vs-reference nuance applies in this file: all locals
  are either `const string` (value-type-like immutable strings)
  or implicit method-call expressions; no `final` local of a
  reference type is reassigned anywhere; no `record class` /
  `record struct` decisions to make on the test side (the SUT
  type `CompileError` is decided by `lib/compiler/error.dart.md`,
  out of scope here).
- The single new placement nuance (file-level top-level helper ⇒
  `private static` method on the test class, not on a sibling
  static class, not on the SUT-side `Compiler` host) is grounded
  in the cached `csharp-static-class-no-toplevel-members` idiom
  plus a stateless-helper-near-callers heuristic — no new
  research required.
- Zero escalations: every construct is authoritative-supported on
  both sides; all ten construct rows REUSE idioms / findings
  recorded by the prior batch (smoke_test, glp_runtime_test,
  multiagent/boot_loader_test, multiagent/mad_error_handling_test,
  heap/*, module/*, analysis/type_checker/*,
  compiler/partial_evaluator_test, compiler/project_linker_test)
  and the lib specs `lib/compiler/compiler.dart.md` and
  `lib/compiler/error.dart.md`. The file-header `//` line-comment
  block trivially maps to a C# `//` block (`rf-dart-line-comment-
  block-to-csharp-line-comment-block`) and is recorded as a
  trivial KB row.
