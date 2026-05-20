> Conversion-spec artifact for test/srsw_test.dart (FR-011). Spec-only
> (FR-023): describes the Dart->C# conversion; contains NO compilable
> C#. A later codegen stage consumes the structured block.
>
> File is a `package:test`-based unit-test suite (75 lines, 4 `test()`
> cases at the TOP level of `main()` with NO outer `group(...)`
> wrapper). It exercises the GLP compiler's SRSW
> (Single-Reader / Single-Writer) validation surface
> (`GlpCompiler.compile` accepting the anonymous `_` writer + rejecting
> a repeated variable / a named writer-with-no-reader / a guard-only
> reader without groundness). No file-header banner comment — file
> starts directly with the two `import` directives (contrast with
> `reserved_constant_test.dart.md`'s `//`-block header). NO library
> directive. NO `setUp`/`tearDown`, NO `late` field, NO file-IO (no
> `dart:io` import, no prelude load), NO `dart:async` or other async
> surface — every test is synchronous. NO top-level helper function
> (each test instantiates `final compiler = GlpCompiler();` locally —
> contrast with `reserved_constant_test.dart.md`'s file-level
> `void compile(String)` helper). The matcher vocabulary is the
> SIMPLEST in the batch: every assertion uses `expect(closure,
> throwsException)` (the bare-Exception variant — distinct from the
> `throwsA(isA<CompileError>().having(...))` shape used by
> `reserved_constant_test` and `partial_evaluator_test`, and distinct
> from the `throwsA(anything)` shape used by `module_parser_test`),
> `expect(value, isNotNull)`, or `expect(value, greaterThan(0))`. The
> file's only NEW facet vs the batch is the bare `throwsException`
> matcher — registered as a new active idiom row
> `rf-dart-expect-throws-exception-to-xunit-assert-throws-exception`
> (sibling of, but distinct from, the cached
> `rf-dart-throwsa-anything-to-xunit-assert-throws-exception` recorded
> by `module_parser_test.dart.md`). The file also uses Dart `print(...)`
> diagnostic statements with embedded check-mark glyphs (`'✅ ...'`) —
> reuses the cached
> `rf-dart-print-and-terminate-to-csharp-equivalent` mapping to
> `System.Console.WriteLine` (sibling of the
> `rf-dart-print-to-xunit-itestoutputhelper-writeline` precedent;
> EXPLICITLY chooses `Console.WriteLine` here per the cached
> diagnostic-output decision and the absence of constructor-injected
> `ITestOutputHelper` in the rest of the batch's stateless test
> classes — see nuance on the construct row below). Every non-trivial
> construct REUSES an idiom recorded by the prior test- and lib-spec
> batches (notably `test/compiler/reserved_constant_test.dart.md`,
> `test/compiler/partial_evaluator_test.dart.md`,
> `test/module/module_parser_test.dart.md`,
> `test/smoke_test.dart.md`, `lib/compiler/compiler.dart.md`).

```yaml
schema_version: 1
source_path: test/srsw_test.dart
source_sha256: 651ad3d1b41dabc4cf7d9d2bff2c273d81d7020400a6429734be6bd1b08f240d
target_code_unit: test/SrswTest.cs
constructs:
  - construct_key: dart.package_test.import_directive
    source_form: "import 'package:test/test.dart';"
    target_decision: >-
      Drop the Dart `import 'package:test/test.dart';` directive and
      replace it at the file level with `using Xunit;`. REUSE the
      batch-wide test-framework idiom — xUnit was pinned by
      `test/smoke_test.dart.md` and reused by every subsequent
      `package:test` spec (smoke_test, multiagent/*, heap/*, module/*,
      analysis/type_checker/*, compiler/partial_evaluator_test,
      compiler/project_linker_test, compiler/reserved_constant_test).
      Per FR-012 / SC-007 this row REUSES the cached finding; no
      re-research. The .NET test project's `.csproj` (referencing
      `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`)
      remains OUT OF SCOPE for this per-file artifact (langpair-level
      project-file emission, same as the sibling specs).
    idiom_id: rf-dart-package-test-import-to-xunit-using
    research_finding_id: rf-dart-package-test-import-to-xunit-using
    nuance: >-
      Framework-choice reuse (cached idiom, no re-derivation): every
      `package:test` file in this batch maps to the SAME .NET
      framework (xUnit) so test discovery / runner config / attribute
      vocabulary stay consistent (per SC-007). Project to a namespace
      mirroring the Dart `test/` directory (e.g. `<RootNs>.Test`).
      Lifecycle nuance (carry-forward, xUnit creates a FRESH instance
      of the test class per `[Fact]` per xunit.net "Shared Context
      between Tests" `https://xunit.net/docs/shared-context`) is
      recorded but NOT exercised in this file — there is no
      `setUp`/`late` field so the constructor body is empty (or
      omitted). Each `[Fact]` allocates its own local `var compiler =
      new GlpCompiler();` so per-Fact freshness has no observable
      consequence here.

  - construct_key: dart.internal_package_import.glp_runtime_compiler_single_file
    source_form: "import 'package:glp_runtime/compiler/compiler.dart';"
    target_decision: >-
      Replace the single Dart `package:glp_runtime/compiler/compiler.dart`
      import with a C# `using` directive that names the converted
      compiler package's namespace. Per the lib spec
      `lib/compiler/compiler.dart.md`, all `lib/compiler/*` Dart files
      collapse into a SINGLE C# namespace (e.g. `Glp.Runtime.Compiler`).
      This file's SOLE internal import (`compiler.dart` for
      `GlpCompiler`) becomes ONE `using Glp.Runtime.Compiler;`
      directive. Contrast with `reserved_constant_test.dart.md` which
      imports BOTH `compiler.dart` and `error.dart` (still collapsing
      to ONE using because same-directory same-namespace) — this file
      imports only `compiler.dart` because it uses the bare
      `throwsException` matcher (no `CompileError` type narrowing in
      the assertions). No `as` alias or `show` narrowing is used on
      the Dart side, so no C# alias or filter is needed. REUSE the
      `rf-dart-internal-package-import-to-csharp-using` idiom from
      `partial_evaluator_test.dart.md`, `reserved_constant_test.dart.md`,
      `boot_loader_test.dart.md`, `moded_head_test.dart.md`, etc.
    idiom_id: rf-dart-internal-package-import-to-csharp-using
    research_finding_id: rf-dart-internal-package-import-to-csharp-using
    nuance: >-
      Single-import-vs-multi-import nuance (EXPLICITLY addressed):
      `reserved_constant_test.dart.md` collapsed TWO imports into ONE
      `using` via same-namespace folding; THIS file imports ONE
      symbol so trivially yields ONE `using`. Symbol visibility:
      every imported symbol used in this file (`GlpCompiler`) is
      library-public on the Dart side (no leading underscore),
      mapping to `public` C# types — no accessibility relaxation
      required. The thrown-exception type (`Exception` on the Dart
      side via the `throwsException` matcher) is `dart:core` —
      maps to C# `System.Exception` (`using System;` is implicit
      in `global using`s on net6+ but codegen may emit it
      explicitly). Cross-file dependency nuance: the test assembly
      must reference the SUT assembly via the project file
      (project-system idiom, OUT OF SCOPE for this per-file
      artifact, same as every sibling test spec).

  - construct_key: dart.package_test.main_entrypoint
    source_form: >-
      "void main() {
        test('SRSW violation: repeated variable should be rejected', () { ... });
        test('Anonymous variable _ in head argument compiles without SRSW error', () { ... });
        test('Anonymous variable _ passes SRSW where named variable would fail', () { ... });
        test('SRSW rejects guard-only readers without groundness', () { ... });
      }"
    target_decision: >-
      Dart `void main()` is the per-file `package:test` entrypoint.
      xUnit discovers `[Fact]` methods by reflection — there is NO
      per-file entrypoint to emit. Eliminate `main` entirely; lift
      the four sibling top-level `test(...)` calls inside `main`'s
      body directly to `[Fact]` methods on a single converted test
      class (see `dart.package_test.test_calls_no_outer_group` below).
      Unlike `partial_evaluator_test.dart.md` and
      `project_linker_test.dart.md`, the `main` body here has NO
      pre-`test` statements — no file-IO, no prelude load — so NO
      `static` constructor is needed. Unlike
      `reserved_constant_test.dart.md`, there is NO outer `group(...)`
      wrapper either — the four `test()` calls sit directly inside
      `main`'s body. The lifted test class has neither a static ctor
      nor an instance ctor (each `[Fact]` allocates its own
      `var compiler = new GlpCompiler();` local, the only test-method
      state).
    idiom_id: rf-dart-package-test-main-omit-in-xunit
    research_finding_id: rf-dart-package-test-main-omit-in-xunit
    nuance: >-
      Cached idiom (precedents: every batch test spec). Empty-main
      nuance EXPLICITLY addressed (same as
      `reserved_constant_test.dart.md`'s empty main, contrast with
      `partial_evaluator_test.dart.md`'s NON-empty main): the `main()`
      body contains ONLY four `test(...)` calls and no pre-`test`
      side effects, so NO static-constructor is required on the C#
      side — the omission is lossless. Lifecycle nuance: Dart `main`
      runs ONCE per test-file process; xUnit has no per-file hook —
      but here there is nothing to run once anyway.

  - construct_key: dart.package_test.test_calls_no_outer_group
    source_form: >-
      "test('SRSW violation: repeated variable should be rejected', () { ... });
       test('Anonymous variable _ in head argument compiles without SRSW error', () { ... });
       test('Anonymous variable _ passes SRSW where named variable would fail', () { ... });
       test('SRSW rejects guard-only readers without groundness', () { ... });"
    target_decision: >-
      Dart `test(label, body)` calls sitting directly at the top
      level of `main()` (NO enclosing `group(...)`) map to a SINGLE
      xUnit test class containing one `[Fact]` per `test()` call.
      The class name is derived from the file's stem
      (`srsw_test.dart` ⇒ `SrswTests`, PascalCased + `Tests` suffix
      per the recorded class-naming convention — same fallback used
      by other group-less test files in the batch — when no outer
      `group` label exists, the file stem is the canonical source
      of the class name). The four inner `test(label, () { ... })`
      calls lift to four `[Fact(DisplayName = "<original label>")]`-
      attributed `public void` methods on the class, body = the
      Dart closure body. No constructor (xUnit fresh-instance
      semantics is trivially satisfied — no shared state). No
      `late` field. NO `private static` helper either (contrast
      with `reserved_constant_test.dart.md`'s `Compile` helper)
      because every `test()` body opens with its own
      `final compiler = GlpCompiler();` local.
    idiom_id: rf-dart-package-test-group-to-xunit-class
    research_finding_id: rf-dart-package-test-group-to-xunit-class
    nuance: >-
      Cached idiom partial-reuse (the canonical
      `rf-dart-package-test-group-to-xunit-class` precedents:
      smoke_test → boot_loader_test → heap/* → module/* →
      analysis/type_checker/* → partial_evaluator_test →
      project_linker_test → reserved_constant_test). No-outer-group
      nuance EXPLICITLY addressed (NEW facet vs
      `reserved_constant_test.dart.md` which had ONE outer group,
      contrast with `project_linker_test.dart.md`'s FOUR sibling
      groups): when `main()` contains `test()` calls without any
      enclosing `group(...)`, the class-name source SHIFTS from the
      group label to the file stem. The mapping is therefore
      `srsw_test.dart` ⇒ `SrswTests` (file stem PascalCased + `Tests`
      suffix, per the same stem-derivation convention used implicitly
      by every previous test spec for the file-naming of the target
      `.cs` file). The `DisplayName` MUST preserve the original Dart
      label verbatim (including the colon-and-space in "SRSW
      violation: ...", the underscore characters in "Anonymous
      variable _ ...", and the multi-clause structure in the third
      label). Suggested method names (illustrative; codegen may apply
      equivalent sanitisation):
      `SrswViolationRepeatedVariableShouldBeRejected`,
      `AnonymousVariableInHeadArgumentCompilesWithoutSrswError`,
      `AnonymousVariablePassesSrswWhereNamedVariableWouldFail`,
      `SrswRejectsGuardOnlyReadersWithoutGroundness`. Method-naming
      PascalCase collision check: no `System.Object` overrides
      collide; no helper named `Compile` exists on this class (every
      `compile` call is on a fresh `var compiler` instance).

  - construct_key: dart.constructor_call.implicit_new_local_var
    source_form: "final compiler = GlpCompiler();"
    target_decision: >-
      Map Dart's implicit-`new` constructor invocation bound to a
      `final` local to C# `var <local> = new <Type>();` per the
      cached `rf-dart-final-local-to-csharp-var-local` idiom (the
      `final ⇒ var` half) composed with
      `rf-dart-implicit-new-and-camelcase-to-csharp-explicit-new-pascalcase`
      (the implicit-new ⇒ explicit-new half). Concretely:
      `final compiler = GlpCompiler();` ⇒ `var compiler = new
      GlpCompiler();`. The local variable name `compiler` stays
      lowerCamelCase (C# local-variable naming convention per
      Microsoft's C# Identifier Names guide at
      `https://learn.microsoft.com/dotnet/csharp/fundamentals/coding-style/identifier-names`
      — locals are lowerCamelCase, same as Dart). `GlpCompiler` is
      the converted SUT class per the lib spec
      `lib/compiler/compiler.dart.md` — same name on both sides
      (PascalCase already by Dart convention for class names; no
      casing change needed). Used FOUR times in this file (one per
      `[Fact]`), each at the top of the test body — Arrange step.
    idiom_id: rf-dart-final-local-to-csharp-var-local
    research_finding_id: rf-dart-final-local-to-csharp-var-local
    nuance: >-
      Two cached idioms composed (precedents:
      `test/test_channel_construction.dart.md` cu-2,
      `test/runtime/module_activation_test.dart.md`,
      `test/conformance/restart_clause1_test.dart.md`,
      `test/debug_negative.dart.md`,
      `reserved_constant_test.dart.md` and the implicit-new precedents
      across the lib specs). Mutability nuance EXPLICITLY addressed:
      Dart `final` forbids reassignment (deep-immutable for the
      binding, shallow for the referenced object); C# `var` does NOT
      forbid reassignment — but the test bodies never reassign the
      `compiler` local, so the mutability difference is observably
      irrelevant here. An alternative `readonly`-equivalent would be
      a `private readonly` field (with constructor init), but THAT
      requires hoisting state out of the per-method scope (different
      xUnit lifecycle — fresh instance per `[Fact]` means the field
      and the local are observably equivalent EXCEPT that the local
      is allocated lazily). Spec default: `var <local>` because
      readability matches the Dart source's `final <local>` shape
      best. Implicit-new nuance (cached, carry-forward): Dart 2+
      dropped the requirement for `new` (Dart language tour: "The
      `new` keyword is optional"); C# REQUIRES `new` for constructor
      invocations per Microsoft Learn "new operator" at
      `https://learn.microsoft.com/dotnet/csharp/language-reference/operators/new-operator`
      — codegen MUST emit `new`.

  - construct_key: dart.package_test.expect_call_throwsException
    source_form: "expect(() => compiler.compile('same(f(X, X)).'), throwsException);"
    target_decision: >-
      Dart `package:matcher`'s `throwsException` constant
      (`https://pub.dev/documentation/matcher/latest/matcher/throwsException-constant.html`)
      asserts that the closure throws ANY object that satisfies
      `isInstanceOf<Exception>` (i.e. anything implementing Dart's
      `Exception` marker interface — DISTINCT from Dart `Error`,
      which is the unrelated class for programmer errors). xUnit has
      no direct `Assert.ThrowsException`-style root matcher — the
      faithful translation is `Assert.Throws<Exception>(() =>
      compiler.Compile(...));`, mapping Dart's `Exception` marker
      to .NET's `System.Exception` root class. This row REGISTERS A
      NEW ACTIVE IDIOM ROW
      `rf-dart-expect-throws-exception-to-xunit-assert-throws-exception`
      because the construct is FIRST-SEEN in this batch (sibling of,
      but DISTINCT from,
      `rf-dart-throwsa-anything-to-xunit-assert-throws-exception`
      from `module_parser_test.dart.md` which covers
      `throwsA(anything)` — the latter is type-unconstrained at the
      matcher level whereas `throwsException` is constrained to
      `Exception` subtypes). xUnit `Assert.Throws<Exception>`
      (Microsoft Learn `Xunit.Assert.Throws<T>(Action)` at
      `https://learn.microsoft.com/dotnet/api/xunit.assert.throws`)
      passes for any thrown `Exception` or subtype — observably
      equivalent to `throwsException` in practice because in BOTH
      languages the marker-root catches the entirety of the
      user-throwable hierarchy. The `reason:` named argument in the
      third test's negative case is REASON-MOVE: xUnit
      `Assert.Throws<T>(Action)` has no message overload, so the
      reason cannot be preserved as a method argument; codegen
      options: (a) drop the reason (xUnit's diagnostic on
      `Assert.Throws<T>` failure is "Assert.Throws() Failure:
      Expected ... actual ..." — adequate); (b) wrap with
      `Assert.True(...)` over a try/catch — verbose, NOT idiomatic.
      Spec default: drop the reason (same default as
      `rf-dart-expect-isNotEmpty-to-xunit-assert-notempty`'s
      no-message-overload sibling). Used THREE times in this file
      (lines 10, 43-44 with reason, 71-72).
    idiom_id: rf-dart-expect-throws-exception-to-xunit-assert-throws-exception
    research_finding_id: rf-dart-expect-throws-exception-to-xunit-assert-throws-exception
    nuance: >-
      FIRST-SEEN idiom (defines a new active row in the KB). Marker
      hierarchy nuance EXPLICITLY addressed: Dart's `Exception` is
      a marker INTERFACE (any class can `implements Exception` —
      Dart Language Tour "Errors and exceptions" at
      `https://dart.dev/language/error-handling`); Dart's `Error` is
      a SEPARATE class for programmer-error bugs. The compiler's
      validation throws (per `lib/compiler/compiler.dart.md` and
      `lib/compiler/error.dart.md`) `CompileError extends
      Exception` (NOT `extends Error`), so `throwsException` matches
      the compiler's validation throws. On the C# side `System.Exception`
      is the SINGLE base class (no marker-interface distinction —
      `System.SystemException` is a sibling but everything
      user-throwable derives from `Exception`). Therefore
      `Assert.Throws<Exception>` is the closest mapping — it tests
      "any user-throwable". Subtype-tolerance carve-out (CACHED,
      carry-forward): `Assert.Throws<T>` requires EXACT type and
      FAILS if a subclass of `T` is thrown — but with `T =
      Exception` (the root), exact-match and any-match converge in
      practice because the runtime cannot throw an UNqualified
      `System.Exception` (the closest exception subclass is always
      thrown). The strict-faithful alternative
      `Assert.ThrowsAny<Exception>(...)` is documented but unused
      here (same default as `rf-dart-throwsa-anything-to-xunit-
      assert-throws-exception` in `module_parser_test.dart.md`).
      Distinct-from-cached-precedent nuance EXPLICITLY addressed:
      `throwsException` ≠ `throwsA(anything)` on the Dart side
      (the former is type-bounded to `Exception`-implementers, the
      latter is unbounded — accepts ANY thrown object including
      `Error` subclasses); on the C# side BOTH map to
      `Assert.Throws<Exception>` because the .NET hierarchy collapses
      the two Dart hierarchies into one root. The two idioms are
      registered SEPARATELY (one per Dart-side matcher) for KB
      faithfulness — codegen consults the Dart matcher form, not
      the C# emit form. Lambda-shape nuance: Dart `() =>
      compiler.compile('same(f(X, X)).')` arrow lambda maps 1-to-1
      to C# `() => compiler.Compile("same(f(X, X)).")` (identical
      arrow syntax; the method name PascalCases via
      `rf-dart-implicit-new-and-camelcase-to-csharp-explicit-new-pascalcase`).
      String-literal nuance: Dart single-quoted `'same(f(X, X)).'`
      maps to C# `"same(f(X, X))."` (C# string literals use only
      double quotes; the single-quote → double-quote replacement
      is mechanical because the content contains no double quotes).
      Reason-preservation alternative recorded for the third test
      (`reason: 'Result with no reader should fail SRSW'`): codegen
      may optionally emit `try { compiler.Compile(badSource);
      Assert.Fail("Result with no reader should fail SRSW"); }
      catch (Exception) { /* expected */ }` — but spec default is
      the bare `Assert.Throws<Exception>(...)` (reason dropped).

  - construct_key: dart.package_test.expect_isNotNull
    source_form: "expect(program, isNotNull);"
    target_decision: >-
      Map Dart `expect(<nullable>, isNotNull)` to xUnit
      `Assert.NotNull(<value>)` (REUSE
      `rf-dart-expect-isNotNull-to-xunit-assert-notnull`, precedents:
      mad_scenarios_test.dart.md, project_linker_test.dart.md;
      Microsoft Learn `Xunit.Assert.NotNull(Object)` at
      `https://learn.microsoft.com/dotnet/api/xunit.assert.notnull`).
      Concretely: `expect(program, isNotNull);` ⇒
      `Assert.NotNull(program);`. Used TWICE in this file (lines 26
      and 54). Neither call carries a `reason:` argument so the
      no-message-overload constraint is not exercised.
    idiom_id: rf-dart-expect-isNotNull-to-xunit-assert-notnull
    research_finding_id: rf-dart-expect-isNotNull-to-xunit-assert-notnull
    nuance: >-
      Cached idiom (precedents listed above). Reason-absence trivial
      branch: both call sites in this file pass no `reason:`
      argument, so codegen emits the canonical
      `Assert.NotNull(program);` form (no `Assert.True(<val> is not
      null, msg)` fallback needed — contrast with
      `project_linker_test.dart.md`'s reason-bearing site). Type
      nuance EXPLICITLY addressed: the local `program` is bound by
      `final program = compiler.compile(source);` whose return type
      is `Program` per `lib/compiler/compiler.dart.md` — under .NET
      nullable-reference-types this is `Program` (non-nullable by
      default in net8+ projects). `Assert.NotNull(<Program>)` over
      a non-nullable parameter is observably equivalent to a runtime
      truth check; it remains worth emitting because the SUT's
      `Compile` may evolve to return a nullable.

  - construct_key: dart.package_test.expect_length_greaterThan
    source_form: "expect(program.ops.length, greaterThan(0));"
    target_decision: >-
      Dart `package:matcher` `greaterThan(N)` maps to xUnit
      `Assert.True(<value> > N)` — xUnit has no built-in
      `Assert.Greater` per the xunit.net FAQ "What's missing" at
      `https://xunit.net/docs/comparisons`; the canonical workaround
      is `Assert.True(expression, message)` (or the message-less
      single-arg overload). REUSE the sibling
      `rf-dart-expect-length-greaterthanorequalto-to-xunit-assert-true`
      idiom recorded by `project_linker_test.dart.md` for the
      strict-`greaterThan` variant — it is the SAME pattern with `>`
      instead of `>=` (registers
      `rf-dart-expect-length-greaterthan-to-xunit-assert-true` as a
      new active idiom row, sibling of the GTE form). Composed with
      `rf-dart-list-length-to-csharp-list-count`: Dart `.length` on
      `List<T>` ⇒ C# `.Count` (Microsoft Learn `List<T>.Count`
      property at
      `https://learn.microsoft.com/dotnet/api/system.collections.generic.list-1.count`).
      Concretely: `expect(program.ops.length, greaterThan(0));` ⇒
      `Assert.True(program.Ops.Count > 0);`. Used ONCE in this file
      (line 27). No `reason:` argument is passed, so the single-arg
      `Assert.True(bool)` overload is sufficient.
    idiom_id: rf-dart-expect-length-greaterthan-to-xunit-assert-true
    research_finding_id: rf-dart-expect-length-greaterthan-to-xunit-assert-true
    nuance: >-
      FIRST-SEEN idiom in this file for the strict-`greaterThan`
      shape (sibling of the cached
      `rf-dart-expect-length-greaterthanorequalto-to-xunit-assert-true`
      from `project_linker_test.dart.md`). xUnit-omission nuance
      EXPLICITLY addressed: xUnit deliberately omits
      `Assert.Greater` / `Assert.Less` / `Assert.GreaterOrEqual` /
      `Assert.LessOrEqual` (xUnit team's "positive assertions only"
      stance — see also the omission of `Assert.DoesNotThrow`). The
      faithful translation uses `Assert.True(comparison)` with no
      message (no `reason:` in the source). PascalCase nuance: Dart
      `program.ops` (instance field/getter, camelCase) ⇒ C#
      `program.Ops` (property, PascalCase) per
      `rf-dart-camelcase-to-csharp-pascalcase` (cached; the
      property-name decision is owned by the lib spec
      `lib/compiler/compiler.dart.md` / `lib/compiler/program.dart.md`).
      Length-vs-Count nuance: Dart `.length` on a `List<T>` is a
      getter; C# `.Count` is a property — both O(1). The choice
      between `Count` (`List<T>`, `Dictionary<K,V>`) and `Length`
      (`Array`, `string`) depends on the SUT type; per the lib
      spec, `program.ops` is a `List<Op>` so the property is
      `.Count`. Strict-vs-non-strict nuance: Dart `greaterThan(0)`
      excludes equality (a zero-length op list would fail);
      `Assert.True(count > 0)` likewise excludes equality —
      observably identical.

  - construct_key: dart.const_string.triple_quoted_multiline_glp_source_fixture
    source_form: >-
      "const source = '''
       procedure foo(_?, _).
       foo(X, _) :- ground(X?) | true.
       ''';
       // and three other multi-line triple-quoted GLP-source fixtures in this file"
    target_decision: >-
      Dart triple-quoted string literals (`'''...'''`) translate to
      C# raw-string literals — preferred shape is the C# 11+ raw
      string `\"\"\"...\"\"\"` (Microsoft Learn "Raw string literals"
      at
      `https://learn.microsoft.com/dotnet/csharp/language-reference/tokens/raw-string`),
      emitted with the closing `\"\"\"` delimiter at column 0 to
      preserve the indentation of the embedded GLP source
      byte-identically. The Dart `final source = '''...'''` (note:
      this file uses `final`, NOT `const`, contrast with
      `reserved_constant_test.dart.md`'s `const source`) maps to
      C# `var source = \"\"\"...\"\"\";` because the local is not
      a compile-time constant on the Dart side — `var` is the
      faithful C# equivalent of `final` per the cached
      `rf-dart-final-local-to-csharp-var-local` idiom. REUSE the
      `rf-dart-triple-quoted-to-csharp-raw-string` idiom recorded
      by `boot_loader_test.dart.md` cu-9 and reused by
      `partial_evaluator_test.dart.md` cu-10 and
      `reserved_constant_test.dart.md`. Used FOUR times in this
      file (the `source` in test #2, the `badSource` + `goodSource`
      pair in test #3, the `badSource` in test #4).
    idiom_id: rf-dart-triple-quoted-to-csharp-raw-string
    research_finding_id: rf-dart-triple-quoted-to-csharp-raw-string
    nuance: >-
      Cached idiom (precedents listed above). final-vs-const nuance
      EXPLICITLY addressed (NEW facet vs
      `reserved_constant_test.dart.md` which used `const source`):
      this file's fixtures are declared `final source = '''...''';`
      not `const source = '''...''';`. Dart `final` defers
      initialization to runtime (still single-assignment); Dart
      `const` requires compile-time-constant. C# `const string`
      requires a compile-time constant; C# `var` does not. Because
      the Dart source uses `final` here (not `const`), the C# target
      is `var source = \"\"\"...\"\"\";` (NOT `const string source`).
      Both `final`/`var` and `const`/`const string` accept the
      raw-string literal as the initializer (raw strings ARE
      compile-time constants in C# 11+ per Microsoft Learn — but
      using `var` keeps the binding shape closer to the Dart
      `final` source). Indentation-preservation nuance
      (carry-forward, load-bearing): Dart `'''...'''` preserves
      newlines verbatim and preserves the literal's INTERNAL
      indentation as-is (Dart Language Tour at
      `https://dart.dev/language/built-in-types#strings`). C# 11
      raw-string literals (`\"\"\"...\"\"\"`) preserve newlines but
      have one TRAP: the closing `\"\"\"` delimiter's column
      determines the COMMON-PREFIX strip (Microsoft Learn
      raw-string: "leading whitespace shared by all lines is
      removed"). To preserve the GLP fixture's indentation
      byte-identically, the C# emitter MUST place the closing
      `\"\"\"` at column 0 (or at the same column as the
      lowest-indented content line) — failing to do so would
      silently change the GLP source seen by the lexer. Codegen
      MUST emit the closing delimiter at column 0 for these
      fixtures. Embedded-quote nuance EXPLICITLY addressed: each
      fixture contains the GLP `?` modeflag and `|` guard operator
      but no triple-double-quote sequences — no bumping to
      `\"\"\"\"...\"\"\"\"` needed.

  - construct_key: dart.print_statement.diagnostic_log_to_stdout
    source_form: >-
      "print('\\nTesting SRSW violation: same(f(X, X))');
       print('✅ Correctly rejected repeated variable');
       print('   Generated ${program.ops.length} instructions');
       // plus several more diagnostic `print(...)` lines per test"
    target_decision: >-
      Map each `print(...)` to `System.Console.WriteLine(...)` —
      simplest 1:1, REUSE the cached
      `rf-dart-print-and-terminate-to-csharp-equivalent` idiom
      recorded in `lib/bytecode/runner.dart.md` for the SUT side
      and used by `test/multiagent/mad_cold_call_isolate_test.dart.md`
      on the test side. The strict-faithful alternative is xUnit's
      `ITestOutputHelper.WriteLine` (the test-isolated diagnostic
      capture, cached idiom
      `rf-dart-print-to-xunit-itestoutputhelper-writeline` from
      `test/test_channel_construction.dart.md`) but THAT requires
      constructor injection on the test class
      (`public SrswTests(ITestOutputHelper output) { _output =
      output; }`) — and this file's batch convention has been
      "stateless test class, no constructor" since
      `reserved_constant_test.dart.md` (zero instance fields, all
      tests synchronous on the test thread). For THIS file the
      prints are debugging aids (banner-lines and check-mark
      success markers, not assertion-load-bearing), so emitting
      `Console.WriteLine` is the simplest faithful translation —
      same observable behaviour as Dart `print` (which writes to
      the process stdout). Recorded alternative: switch all four
      `[Fact]` bodies to use `ITestOutputHelper.WriteLine` plus a
      constructor — preserves test-isolated capture under xUnit
      v2+/net6+ where `Console` output is NOT auto-captured per
      `https://xunit.net/docs/capturing-output`. Spec default
      (matching the cached choice in
      `mad_cold_call_isolate_test.dart.md`'s same dilemma): emit
      `Console.WriteLine`.
    idiom_id: rf-dart-print-and-terminate-to-csharp-equivalent
    research_finding_id: rf-dart-print-and-terminate-to-csharp-equivalent
    nuance: >-
      Cached idiom (precedents: `lib/bytecode/runner.dart.md`,
      `test/multiagent/mad_cold_call_isolate_test.dart.md`).
      Test-runner-capture nuance EXPLICITLY addressed: xUnit
      captures `Console.WriteLine` output per test ONLY from .NET
      Framework runners; xUnit v2+ on .NET Core/5+ does NOT capture
      Console output — `ITestOutputHelper` is the recommended
      capture mechanism
      (`https://xunit.net/docs/capturing-output`). For THIS test
      the prints are debugging aids, not assertion-load-bearing;
      loss of capture under .NET Core is acceptable. Newline-escape
      nuance EXPLICITLY addressed: Dart `'\\nTesting ...'` uses the
      `\\n` escape inside a single-quoted string literal — Dart
      single-quoted strings interpret backslash escapes. The C#
      faithful target is `"\\nTesting ..."` (C# regular-string
      literals likewise interpret `\\n`) or `Console.WriteLine();
      Console.WriteLine("Testing ...");` (split into two calls
      because `Console.WriteLine` appends its own newline — the
      `\\n` at the START of the Dart string adds a BLANK LINE
      before the text). Spec default: preserve the `\\n` escape
      verbatim inside the C# regular-string literal (faithful
      output: blank line + text on one `Console.WriteLine` call;
      `WriteLine` appends a SECOND newline after the text, but that
      matches Dart `print`'s newline-appending behaviour
      automatically). Glyph-preservation nuance EXPLICITLY
      addressed: the source contains `'✅'` (U+2705 WHITE HEAVY
      CHECK MARK) inside several print statements. The C# source
      file MUST be UTF-8 encoded (Microsoft Learn "C# source code
      file encoding" at
      `https://learn.microsoft.com/dotnet/csharp/language-reference/proposals/csharp-9.0/source-generators#source-file-encoding`
      — UTF-8 with BOM is conventional for .NET) so the glyph
      survives byte-identical embedding in the string literal —
      no `\\uXXXX` escape needed. Codegen MUST emit the target
      `.cs` file with a UTF-8 encoding (with or without BOM). String
      interpolation nuance: Dart `'   Generated ${program.ops.length}
      instructions'` maps to C# `$"   Generated {program.Ops.Count}
      instructions"` per the cached
      `rf-dart-string-interpolation-to-csharp-interpolated-string`
      idiom (`.length` ⇒ `.Count` carry-forward via the
      length-to-count row above).

  - construct_key: dart.string_literal.single_quoted_glp_source_inline
    source_form: "compiler.compile('same(f(X, X)).');"
    target_decision: >-
      Map Dart single-quoted string literal `'same(f(X, X)).'` to
      a C# regular-string literal `"same(f(X, X))."`. C# string
      literals use ONLY double quotes (single quotes are reserved
      for `char` literals per Microsoft Learn "String literals" at
      `https://learn.microsoft.com/dotnet/csharp/language-reference/tokens/string`).
      The content contains no double quotes so the conversion is
      a mechanical single-quote ⇒ double-quote replacement. The
      method-call `compiler.compile(...)` ⇒ `compiler.Compile(...)`
      via the cached
      `rf-dart-implicit-new-and-camelcase-to-csharp-explicit-new-pascalcase`
      idiom (the camelCase-to-PascalCase half — no `new` here
      because this is an instance-method call, not a constructor
      invocation). Used ONCE inline in this file (line 10);
      the other compile-call sites use the `source` /
      `badSource` / `goodSource` triple-quoted-string locals.
    idiom_id: rf-dart-string-literal-to-csharp-string-literal-quote-swap
    research_finding_id: rf-dart-string-literal-to-csharp-string-literal-quote-swap
    nuance: >-
      Trivial idiom — registers a NEW first-seen KB row
      `rf-dart-string-literal-to-csharp-string-literal-quote-swap`
      for the bare quote-swap case (Dart single-quoted ⇒ C#
      double-quoted, no escapes needed). Distinguished from the
      raw-string idiom (`rf-dart-triple-quoted-to-csharp-raw-string`)
      which handles MULTI-LINE Dart strings. Distinguished from the
      string-interpolation idiom
      (`rf-dart-string-interpolation-to-csharp-interpolated-string`)
      which handles `'$expr'` ⇒ `$"{expr}"`. Char-vs-string nuance
      EXPLICITLY addressed: in Dart `'a'` is a one-character `String`;
      in C# `'a'` is a `char` (System.Char) — the literal at this
      site (`'same(f(X, X)).'`) is unambiguously a string (more than
      one character) so the mapping is to a C# `string` literal
      (`"..."`), NEVER a `char` literal.

conversion_units:
  - cu-1: "NO file-header banner comment (contrast with reserved_constant_test.dart.md cu-1) — first line of file is `import 'package:test/test.dart';`; the C# target file starts directly with `using` directives"
  - cu-2: "file-scope using directives — `using Xunit;`, `using Glp.Runtime.Compiler;` (the Dart `package:glp_runtime/compiler/compiler.dart` import; one Dart import ⇒ one C# using, no folding-from-multiple)"
  - cu-3: "namespace declaration mirroring the test/ path — `namespace <RootNs>.Test;` (no subdirectory because srsw_test.dart sits directly in test/, not test/compiler/)"
  - cu-4: "top-level test class `SrswTests` (file stem PascalCased + `Tests` suffix because NO outer `group(...)` exists; class-name source is the file stem, not a group label — same convention as any group-less test file in the batch)"
  - cu-5: "NO constructor — neither static nor instance (no `late` field, no `setUp`, no pre-`test` state to seed; `main()`'s body was four sibling `test(...)` calls so nothing to lift to a static ctor)"
  - cu-6: "NO file-private static helper method (contrast with reserved_constant_test.dart.md cu-5) — each `[Fact]` body opens with its own `var compiler = new GlpCompiler();` local"
  - cu-7: "4 `[Fact(DisplayName = \"<original label>\")]` methods — one per Dart `test()` call, each `public void`, body lifted verbatim from the Dart test closure"
  - cu-8: "3 `Assert.Throws<Exception>(() => compiler.Compile(...));` sites (tests #1, #3-negative-branch, #4) — Dart `throwsException` ⇒ `Assert.Throws<Exception>` (NEW idiom `rf-dart-expect-throws-exception-to-xunit-assert-throws-exception`); reason on test #3 is DROPPED per spec default; the strict-faithful `Assert.ThrowsAny<Exception>` alternative is documented but unused"
  - cu-9: "2 `Assert.NotNull(program);` sites (tests #2 and #3-positive-branch) — Dart `expect(program, isNotNull)` ⇒ `Assert.NotNull(program)`; no `reason:` so the single-arg overload is sufficient"
  - cu-10: "1 `Assert.True(program.Ops.Count > 0);` site (test #2) — Dart `expect(program.ops.length, greaterThan(0))` ⇒ `Assert.True(<count> > 0)` (NEW idiom `rf-dart-expect-length-greaterthan-to-xunit-assert-true`, sibling of the cached GTE form from project_linker_test.dart.md)"
  - cu-11: "4 raw-string-literal payloads (`\"\"\"...\"\"\"`) for the four embedded `.glp` source fixtures (the `source` in test #2, the `badSource` + `goodSource` pair in test #3, the `badSource` in test #4), closing delimiter at column 0 to preserve indentation byte-identically; each declared as `var source = \"\"\"...\"\"\";` matching the Dart `final source = '''...''';` form (NOT `const string` — the Dart side uses `final`, not `const`)"
  - cu-12: "13 `Console.WriteLine(...)` sites — diagnostic stdout traces (one banner-style `print` at each test's start, one success-marker `print` at each test's end, plus the `print('   Generated …')` line in test #2). UTF-8 file encoding required for the `'✅'` (U+2705) glyph. String interpolation `${program.ops.length}` ⇒ `$\"{program.Ops.Count}\"`."
  - cu-13: "1 inline single-quoted Dart string literal `'same(f(X, X)).'` ⇒ C# `\"same(f(X, X)).\"` (quote-swap; NEW idiom `rf-dart-string-literal-to-csharp-string-literal-quote-swap`)"
  - cu-14: "NO equivalent of Dart's `void main()` — xUnit discovery is attribute-driven, registration-via-main is dropped entirely; there are no pre-`test` statements to hoist"
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
  compiler/project_linker_test, compiler/reserved_constant_test).
  Authoritative sources (Microsoft Learn unit-testing-csharp-with-xunit,
  xunit.net v3 getting-started, pub.dev/package:test) carry forward
  verbatim.
- **Conclusion**: drop `import 'package:test/test.dart';`, emit
  `using Xunit;`. Zero escalation — same as every sibling test spec.

### rf-dart-internal-package-import-to-csharp-using — internal `package:` import ⇒ `using` (REUSED)

- **KB reuse**: same idiom as `reserved_constant_test.dart.md`,
  `partial_evaluator_test.dart.md`, and the rest of the batch. The
  single Dart `package:glp_runtime/compiler/compiler.dart` import
  becomes ONE `using Glp.Runtime.Compiler;` directive (the
  namespace string is owned by `lib/compiler/compiler.dart.md`).
  Authoritative sources cited in prior batch specs carry forward.

### rf-dart-package-test-main-omit-in-xunit — Dart `void main()` ⇒ no-op (REUSED, empty-main branch)

- **KB reuse**: same as every batch test spec — xUnit has no
  per-file entrypoint, so Dart's `void main()` wrapper is
  dropped. This file's `main` body contains ONLY four `test(...)`
  calls and no pre-`test` side-effects, so NO static-constructor
  hoist is needed (same trivial branch as
  `reserved_constant_test.dart.md`'s empty main).

### rf-dart-package-test-group-to-xunit-class — `test()` calls (no outer group) ⇒ test class (REUSED with no-group facet)

- **KB reuse**: same canonical idiom as the entire test-spec batch
  for the group ⇒ class mapping. NEW facet (not requiring new
  research): when `main()` contains `test()` calls without ANY
  enclosing `group(...)`, the class-name source shifts from the
  group label to the file stem (`srsw_test.dart` ⇒ `SrswTests`).
  This is the natural extension of the cached idiom — same
  identifier-sanitisation rules apply, only the input string
  source changes (label ⇒ file stem). Authoritative sources cited
  in sibling specs carry forward.

### rf-dart-final-local-to-csharp-var-local + rf-dart-implicit-new-and-camelcase-to-csharp-explicit-new-pascalcase — `final compiler = GlpCompiler();` ⇒ `var compiler = new GlpCompiler();` (REUSED, composed)

- **KB reuse**: TWO cached idioms composed (precedents:
  `test/test_channel_construction.dart.md`,
  `test/runtime/module_activation_test.dart.md`,
  `test/conformance/restart_clause1_test.dart.md`,
  `test/debug_negative.dart.md`,
  `reserved_constant_test.dart.md` and the implicit-new precedents
  across the lib specs). Two facets: (1) Dart `final` local ⇒ C#
  `var` local (mutability nuance documented above — observably
  irrelevant here because no reassignment); (2) Dart's implicit-new
  (`GlpCompiler()`) requires `new` in C# (`new GlpCompiler()`), per
  Microsoft Learn `new` operator
  (`https://learn.microsoft.com/dotnet/csharp/language-reference/operators/new-operator`).

### rf-dart-expect-throws-exception-to-xunit-assert-throws-exception — `throwsException` ⇒ `Assert.Throws<Exception>` (NEW IDIOM, registered active)

- **First-seen in this batch**: defines a new active idiom row.
  The Dart side is `package:matcher` `throwsException` constant
  (`https://pub.dev/documentation/matcher/latest/matcher/throwsException-constant.html`),
  which asserts that the closure throws an object satisfying
  `isInstanceOf<Exception>` (Dart's `Exception` marker interface
  — distinct from `Error`; Dart Language Tour "Errors and
  exceptions" at `https://dart.dev/language/error-handling`).
  The C# side is `Assert.Throws<Exception>` mapping to
  `System.Exception` (Microsoft Learn `System.Exception` at
  `https://learn.microsoft.com/dotnet/api/system.exception`,
  xUnit `Assert.Throws<T>(Action)` at
  `https://learn.microsoft.com/dotnet/api/xunit.assert.throws`).
- **Distinguished from cached siblings**: `throwsException` is
  TYPE-BOUNDED to `Exception`-implementers (excludes
  `Error` subclasses on the Dart side); `throwsA(anything)` from
  `module_parser_test.dart.md` is UNBOUNDED (accepts any thrown
  object including `Error`); `throwsA(isA<T>().having(...))` from
  `partial_evaluator_test.dart.md` is type-AND-message-bounded.
  All three map under DIFFERENT idiom rows because Dart's
  matcher form is preserved at the KB row level (the C# side
  collapses Dart's `Exception`/`Error` hierarchy to a single
  `System.Exception` root, so the C# emit form converges in
  practice — but KB row stays Dart-form-keyed).
- **Authoritative both sides**; reason-preservation alternative
  documented; spec default = drop the `reason:` argument.

### rf-dart-expect-isNotNull-to-xunit-assert-notnull — `isNotNull` ⇒ `Assert.NotNull` (REUSED)

- **KB reuse**: precedents include `mad_scenarios_test.dart.md`,
  `project_linker_test.dart.md`, `well_typed_clause_test.dart.md`,
  `suspension_pointer_test.dart.md`, `module_parser_test.dart.md`,
  multiple multiagent/* test specs. Microsoft Learn
  `Xunit.Assert.NotNull(Object)` at
  `https://learn.microsoft.com/dotnet/api/xunit.assert.notnull`
  carries forward.

### rf-dart-expect-length-greaterthan-to-xunit-assert-true — `greaterThan` ⇒ `Assert.True(<expr>)` (NEW IDIOM, sibling of cached GTE form)

- **First-seen for the strict `greaterThan` form**: the cached
  `rf-dart-expect-length-greaterthanorequalto-to-xunit-assert-true`
  from `project_linker_test.dart.md` handled the `>=` variant;
  THIS row registers the `>` variant. The pattern is identical
  (`Assert.True(<expr> > N)`), only the operator differs. No
  re-research required — the xunit.net "Comparisons" page at
  `https://xunit.net/docs/comparisons` documents the same
  positive-assertions-only stance for both forms.
- Composed with cached `rf-dart-list-length-to-csharp-list-count`
  (Dart `.length` on `List<T>` ⇒ C# `.Count` per Microsoft Learn
  `List<T>.Count` property).

### rf-dart-triple-quoted-to-csharp-raw-string — `'''...'''` ⇒ `"""..."""` (REUSED, with final-not-const facet)

- **KB reuse**: recorded in `boot_loader_test.dart.md` cu-9 and
  reused by `partial_evaluator_test.dart.md` cu-10 and
  `reserved_constant_test.dart.md`. Dart triple-quoted strings ⇒
  C# 11+ raw string literals
  (`https://learn.microsoft.com/dotnet/csharp/language-reference/tokens/raw-string`).
  Closing-delimiter-at-column-0 rule for indentation preservation
  carries forward.
- **NEW facet (final-vs-const, not requiring new research)**: this
  file uses `final source = '''...''';` (NOT `const source`),
  contrast with `reserved_constant_test.dart.md`'s `const`. The
  C# target is `var source = """...""";` (NOT `const string`) —
  composed with the cached `rf-dart-final-local-to-csharp-var-local`
  idiom. None of the four fixtures contains `"""` triple-double-quotes
  so no delimiter-bumping is needed.

### rf-dart-print-and-terminate-to-csharp-equivalent — `print()` ⇒ `Console.WriteLine` (REUSED, with stateless-test-class choice)

- **KB reuse**: same idiom as `lib/bytecode/runner.dart.md` (SUT
  side) and `test/multiagent/mad_cold_call_isolate_test.dart.md`
  (test side, with the same Console-vs-ITestOutputHelper choice).
  The cached precedent EXPLICITLY documents the trade-off (xUnit
  per-test stdout capture is .NET-Framework-only;
  `ITestOutputHelper` is the recommended capture on .NET Core/5+
  per `https://xunit.net/docs/capturing-output`); the spec
  default for the batch is `Console.WriteLine` when no
  constructor-injected `ITestOutputHelper` field exists. THIS
  file follows the batch convention of a stateless test class
  (no constructor, no instance fields), so `Console.WriteLine`
  is the natural emission. Recorded alternative: switch the four
  `[Fact]` bodies to `ITestOutputHelper.WriteLine` and add a
  constructor.

### rf-dart-string-literal-to-csharp-string-literal-quote-swap — `'x'` ⇒ `"x"` (NEW IDIOM, trivial)

- **First-seen trivial idiom**: Dart single-quoted strings ⇒ C#
  double-quoted strings (C# reserves single quotes for `char`
  literals per Microsoft Learn "String literals" at
  `https://learn.microsoft.com/dotnet/csharp/language-reference/tokens/string`).
  The mapping is mechanical when no double-quote characters
  appear in the content (the source contains `'same(f(X, X)).'`
  — no `"` characters). For strings containing `"` the codegen
  must escape; for strings containing both `"` and `\` the
  codegen may prefer a verbatim `@"..."` or raw `"""..."""`
  literal — those decisions are owned by separate idiom rows.
  Registered for future reuse; authoritative-supported on both
  sides.

## Notes

- Four `test()` cases total, all synchronous, NO outer `group(...)`
  wrapper. Three exercise the negative SRSW path (`throwsException`);
  one exercises BOTH a positive path (`isNotNull` +
  `greaterThan(0)`) and a negative path (`throwsException` with
  `reason:`); one exercises only the positive
  (`isNotNull`) path on a `goodSource`.
- The four-test layout is the SIMPLEST in the batch — no
  cross-test setup, no shared state, no test-class constructor,
  no helper hoist. Every `[Fact]` body is a self-contained
  Arrange / Act / Assert sequence.
- No `dart:io` import — NO file-IO, NO `static` constructor, NO
  prelude load. The conversion is strictly simpler than
  `partial_evaluator_test.dart.md` and
  `project_linker_test.dart.md` on that axis.
- No `late` field, no `setUp`, no `tearDown`, no shared per-test
  state. The lifted test class has ONLY the four `[Fact]` methods
  — no constructor (instance or static) needed, no helper method.
- No async / `Future` / `Stream` / isolate / `Completer` / `Timer`
  surface anywhere in this file — all `[Fact]` methods return
  `void` (not `async Task`). The well-known async-Dart-vs-.NET-
  async nuance is correctly NOT asserted here (does not apply).
- No `mixin`, `extension`, generics (the SUT
  `GlpCompiler.compile` is non-generic), sealed/abstract test
  types, bitwise/shift, isolate, null-safety surface on the test
  side beyond the default nullable-reference-types context.
- No value-vs-reference nuance applies in this file: every local
  is either `var compiler` (a reference holding a fresh instance)
  or `var source/badSource/goodSource` (a reference holding a
  raw-string literal). No `final` local of a reference type is
  reassigned anywhere; no `record class` / `record struct`
  decisions to make on the test side.
- TWO NEW idiom rows are registered as a side-effect of this spec:
  `rf-dart-expect-throws-exception-to-xunit-assert-throws-exception`
  (the bare `throwsException` matcher),
  `rf-dart-expect-length-greaterthan-to-xunit-assert-true` (the
  strict `greaterThan` matcher), plus the trivial
  `rf-dart-string-literal-to-csharp-string-literal-quote-swap`
  (single-quoted ⇒ double-quoted) row. All three are
  authoritative-supported on both sides; no escalation.
- Zero escalations: every construct is authoritative-supported on
  both sides; every construct row REUSES an idiom recorded by the
  prior batch (smoke_test, glp_runtime_test, multiagent/*, heap/*,
  module/*, analysis/type_checker/*, compiler/partial_evaluator_test,
  compiler/project_linker_test, compiler/reserved_constant_test)
  and the lib spec `lib/compiler/compiler.dart.md`, EXCEPT for the
  three first-seen idiom rows listed above which are registered as
  new active rows in the KB for future reuse.
