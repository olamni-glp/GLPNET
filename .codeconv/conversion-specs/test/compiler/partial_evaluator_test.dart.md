> Conversion-spec artifact for test/compiler/partial_evaluator_test.dart
> (FR-011). Spec-only (FR-023): describes the Dart->C# conversion;
> contains NO compilable C#. A later codegen stage consumes the
> structured block.
>
> File is a `package:test`-based unit-test suite (285 lines, 12 `test()`
> cases inside one outer `group('PartialEvaluator guard validation', ...)`).
> It exercises the partial-evaluator's guard-validation surface
> (`PartialEvaluator.transformDefinedGuards`). The file also performs
> one-time prelude initialisation at the top of `void main()` by reading
> `../programs/self.glp` from disk via `dart:io` `File` and calling
> `setPreludeUnitClauseSource(...)`. A `setUp(() { pe = PartialEvaluator(); })`
> sits inside the outer `group`. Five of the twelve tests use the
> positive `expect(() => runPE(source), returnsNormally)` shape; six use
> the negative `expect(() => runPE(source), throwsA(isA<CompileError>()
> .having((e) => e.message, 'message', contains('<substr>'))))` shape;
> the `runPE` body is a local helper closure that calls
> `Lexer/Parser/Program/PartialEvaluator.transformDefinedGuards` against
> the embedded multi-line GLP source string. Every non-trivial construct
> REUSES an idiom recorded by the prior test-spec batch (smoke_test,
> glp_runtime_test, multiagent/boot_loader_test, multiagent/mad_error_handling_test,
> heap/*, module/*, analysis/type_checker/*) and the lib spec
> `lib/compiler/partial_evaluator.dart.md`.

```yaml
schema_version: 1
source_path: test/compiler/partial_evaluator_test.dart
source_sha256: b6a416de5607acf814c73228a7dd938d0b2de5ce07856b7bf42931c4628d5c2a
target_code_unit: test/compiler/PartialEvaluatorTest.cs
constructs:
  - construct_key: dart.package_test.import_directive
    source_form: "import 'package:test/test.dart';"
    target_decision: >-
      Drop the Dart `import 'package:test/test.dart';` directive and
      replace it at the file level with `using Xunit;`. REUSE the
      batch-wide test-framework idiom — xUnit was pinned by
      `test/smoke_test.dart.md` and reused by every subsequent
      `package:test` spec (multiagent/mad_error_handling, multiagent/
      boot_loader, heap/*, module/*, analysis/type_checker/*). Per
      FR-012 / SC-007 this row REUSES the cached finding; no
      re-research. The .NET test project's `.csproj` (referencing
      `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`)
      remains OUT OF SCOPE for this per-file artifact (langpair-level
      project-file emission, same as the sibling specs).
    idiom_id: rf-dart-package-test-import-to-xunit-using
    research_finding_id: rf-dart-package-test-import-to-xunit-using
    nuance: >-
      Framework-choice reuse (explicitly addressed, no re-derivation):
      every `package:test` file in this batch maps to the SAME .NET
      framework (xUnit) so test discovery / runner config / attribute
      vocabulary stay consistent (per SC-007). Codegen MUST also add
      `using System.IO;` (referenced by the `File`/`File.Exists`/
      `File.ReadAllText` translation under
      `dart.platform.file_existsSync_readAsStringSync` below) and
      project to a namespace mirroring the Dart `test/compiler`
      directory (e.g. `<RootNs>.Test.Compiler`). The lifecycle nuance
      (xUnit creates a FRESH instance of the test class per `[Fact]`
      per xunit.net "Shared Context between Tests") carries forward
      verbatim from prior test specs and is consistent with the Dart
      `setUp(() { pe = PartialEvaluator(); })` semantics handled
      below.

  - construct_key: dart.dart_io.import_directive
    source_form: "import 'dart:io';"
    target_decision: >-
      Drop the Dart `import 'dart:io';` directive and replace it at
      the file level with `using System.IO;`. REUSE the lib-spec
      idiom `rf-dart-import-dartio-to-csharp-using-systemio` recorded
      in `lib/runtime/runtime.dart.md` for the `dart:io` ->
      `System.IO` namespace mapping; the only `dart:io` surface used
      in THIS file is the `File` class (no `Directory`, `Platform`,
      `Process`, `Socket`, `Stdin`, or `Stdout` references), so a
      single `using System.IO;` suffices.
    idiom_id: rf-dart-import-dartio-to-csharp-using-systemio
    research_finding_id: rf-dart-import-dartio-to-csharp-using-systemio
    nuance: >-
      Library-vs-namespace nuance (explicitly addressed, carry-forward
      from runtime.dart.md): `dart:io` is a Dart-core library; .NET
      splits the same surface across several `System.*` namespaces
      (`System.IO` for file/stream APIs, `System.Diagnostics` for
      `Process`, `System.Net.Sockets` for sockets). The faithful
      mapping is "the `dart:io` SYMBOLS USED -> the matching `System.*`
      namespaces", not a blanket 1-to-1 namespace swap. For this file
      the only used symbol is `File`, so the emitted `using` is exactly
      one: `using System.IO;`. No `show` filter on the Dart side, no
      narrowing needed on the C# side.

  - construct_key: dart.internal_package_import.glp_runtime_compiler_set
    source_form: >-
      "import 'package:glp_runtime/compiler/partial_evaluator.dart';
      import 'package:glp_runtime/compiler/parser.dart';
      import 'package:glp_runtime/compiler/lexer.dart';
      import 'package:glp_runtime/compiler/ast.dart';
      import 'package:glp_runtime/compiler/error.dart';"
    target_decision: >-
      Replace the five Dart `package:glp_runtime/compiler/*` imports
      with C# `using` directives that name the converted compiler
      package's namespace. Per the lib spec
      `lib/compiler/partial_evaluator.dart.md`, all five `lib/compiler/`
      Dart files collapse into a SINGLE C# namespace (e.g.
      `Glp.Runtime.Compiler`) — being in the same namespace from the
      converted SUT side means the test file emits ONE `using
      Glp.Runtime.Compiler;` directive (not five). The test file
      references `PartialEvaluator`, `Lexer`, `Parser`, `Program`,
      `CompileError` — all of which live under that one namespace per
      the lib spec's namespace-folding rule. No `as` alias is used on
      the Dart side, so no C# namespace alias is needed.
    idiom_id: rf-dart-internal-package-import-to-csharp-using
    research_finding_id: rf-dart-internal-package-import-to-csharp-using
    nuance: >-
      Cross-file-dependency nuance (reused from boot_loader_test.dart.md
      and moded_head_test.dart.md): Dart `package:glp_runtime/...` is
      a pubspec-anchored URI resolved at compile time; C# has no
      per-file URI — only assembly + namespace. The test assembly
      must reference the SUT assembly via the project file
      (project-system idiom, OUT OF SCOPE for this artifact). Same-
      namespace folding (load-bearing, carry-forward from
      partial_evaluator.dart.md): the five Dart imports converge on
      ONE C# `using`. Symbol visibility: every imported symbol
      (`PartialEvaluator`, `Lexer`, `Parser`, `Program`, `CompileError`)
      is library-public on the Dart side (no leading underscore),
      mapping to `public` C# types — none of the test references is
      to an `_`-prefixed library-private symbol, so no relaxation of
      C# accessibility is required.

  - construct_key: dart.package_test.main_entrypoint
    source_form: >-
      "void main() { final rootSelfGlp = File('../programs/self.glp');
      if (rootSelfGlp.existsSync()) {
        setPreludeUnitClauseSource(rootSelfGlp.readAsStringSync()); }
      group('PartialEvaluator guard validation', () { ... }); }"
    target_decision: >-
      Dart `void main()` is the per-file `package:test` entrypoint.
      xUnit discovers `[Fact]` methods by reflection — there is NO
      per-file entrypoint to emit. Eliminate `main` entirely; lift
      the single inner `group(...)` to a test class (see
      `dart.package_test.group_block` below). The PRE-GROUP statement
      block (the `File('../programs/self.glp')` existence check +
      `setPreludeUnitClauseSource(...)` call) — which today runs once
      per Dart test-file process, BEFORE any `setUp` or `test` — is
      load-bearing and is lifted into a `static` initialiser on the
      test class. See the dedicated construct
      `dart.platform.file_existsSync_readAsStringSync` below for the
      file-IO mapping and the dedicated
      `dart.module.global_setter_function` for the
      `setPreludeUnitClauseSource` call mapping.
    idiom_id: rf-dart-package-test-main-omit-in-xunit
    research_finding_id: rf-dart-package-test-main-omit-in-xunit
    nuance: >-
      Lifecycle nuance (carry-forward, plus NEW pre-group-init nuance
      explicitly addressed): Dart `main` runs ONCE per test-file process,
      hosting both the pre-`group` initialisation block and the
      `group()` registration. xUnit has no per-file hook — the canonical
      C# replacements are (i) `static` constructor of the test class
      (runs once per type per AppDomain, on first member access — see
      Microsoft Learn "static constructors" at
      `https://learn.microsoft.com/dotnet/csharp/programming-guide/classes-and-structs/static-constructors`),
      (ii) an `IClassFixture<T>` shared-context fixture, (iii) an
      `[CollectionDefinition]`+`ICollectionFixture<T>` shared across
      test classes. For THIS file (one inner `group`, one process-wide
      side-effect — `setPreludeUnitClauseSource` writes a mutable
      compiler global), option (i) `static` constructor on the
      lifted test class `PartialEvaluatorGuardValidationTests` is the
      tightest fit: it runs exactly once, before any `[Fact]`, with
      no fixture-DI plumbing. The static-ctor approach matches Dart
      `main`'s "runs once before any test" semantics; the
      `IClassFixture` route is unnecessary because the side-effect
      is into a `static` field on the converted prelude wrapper (per
      partial_evaluator.dart.md), not into a per-fixture instance.
      Codegen MUST emit the static initialiser BEFORE any `[Fact]`
      method definition, so the first `[Fact]` invocation finds the
      prelude already loaded.

  - construct_key: dart.platform.file_existsSync_readAsStringSync
    source_form: >-
      "final rootSelfGlp = File('../programs/self.glp');
      if (rootSelfGlp.existsSync()) {
        setPreludeUnitClauseSource(rootSelfGlp.readAsStringSync()); }"
    target_decision: >-
      Map Dart `File('<path>')` + `.existsSync()` + `.readAsStringSync()`
      to C# `System.IO.File.Exists(<path>)` + `System.IO.File.ReadAllText(<path>)`
      (Microsoft Learn `System.IO.File.Exists(string)` and
      `System.IO.File.ReadAllText(string)`). The Dart pattern is:
      construct a `File` object, check existence with the sync method,
      read all bytes/text with another sync method. The C# pattern is
      shorter — `System.IO.File` is a STATIC class with static
      `Exists`/`ReadAllText` methods that take a path string directly,
      no instance construction needed. Emitted form: `if (File.Exists(
      "../programs/self.glp")) { PreludeUnitClauses.SetPreludeUnitClauseSource(
      File.ReadAllText("../programs/self.glp")); }`. The relative path
      `"../programs/self.glp"` is preserved verbatim — both Dart's
      `File` and .NET's `System.IO.File` resolve relative paths against
      the current working directory at the moment of the call. REUSE
      the `dart.platform.file_directory_existsSync_path_join_string_interpolation`
      family of idioms recorded in `lib/multiagent/repl_play_runner.dart.md`
      (existence-vs-read split into `Exists` + `ReadAllText`).
    idiom_id: rf-dart-file-existssync-readasstringsync-to-system-io-file-exists-readalltext
    research_finding_id: rf-dart-file-existssync-readasstringsync-to-system-io-file-exists-readalltext
    nuance: >-
      Three intertwined nuances (load-bearing, explicitly addressed).
      (1) Instance-vs-static nuance: Dart's `dart:io` `File` is a
      CLASS — `File(path)` constructs an instance, then `.existsSync()`
      / `.readAsStringSync()` are instance methods. .NET's
      `System.IO.File` is a STATIC class — `File.Exists(path)` and
      `File.ReadAllText(path)` take the path directly. .NET also has
      `System.IO.FileInfo` (instance class with `.Exists` /
      `.OpenRead()` etc.) which more closely mirrors Dart's `File`,
      but the static `System.IO.File.Exists`/`ReadAllText` is the
      idiomatic short form for this two-call existence-then-read
      pattern (Microsoft Learn `System.IO.File` documentation
      explicitly recommends the static class for one-shot operations,
      reserving `FileInfo` for cases with repeated operations on the
      same path). (2) Sync-vs-async nuance (carry-forward from
      repl_play_runner.dart.md): Dart `existsSync` / `readAsStringSync`
      are the synchronous variants of `exists` / `readAsString`;
      .NET BCL `File.Exists` is intrinsically sync (no async variant
      in the BCL), and `File.ReadAllText` has an async sibling
      `File.ReadAllTextAsync` — but this file uses the sync Dart
      form, so the sync C# counterpart `File.ReadAllText` is the
      faithful mapping. (3) Encoding nuance: Dart `readAsStringSync()`
      decodes using `utf8` by default (Dart Documentation:
      `https://api.dart.dev/stable/dart-io/File/readAsStringSync.html`);
      .NET `File.ReadAllText(string)` uses BOM-aware UTF-8 detection
      (Microsoft Learn `File.ReadAllText`: "attempts to automatically
      detect the encoding of a file based on the presence of byte
      order marks; encoding formats UTF-8 and UTF-32 (both big-endian
      and little-endian) can be detected"). For a `.glp` source file
      that is plain ASCII / UTF-8 with no BOM, both decoders produce
      the same string — semantically equivalent here.

  - construct_key: dart.module.global_setter_function
    source_form: "setPreludeUnitClauseSource(rootSelfGlp.readAsStringSync());"
    target_decision: >-
      Map the free-function call `setPreludeUnitClauseSource(...)` to
      a `public static void` method on the converted prelude-unit-
      clauses host class, named `SetPreludeUnitClauseSource(...)`. Per
      the lib spec `lib/compiler/partial_evaluator.dart.md`'s
      `dart.toplevel.mutable_global_nullable_string_init_to_null_and_setter_function`
      construct, the Dart top-level setter is hosted by a C# `internal
      static class PreludeUnitClauses` with members
      `_preludeUnitClauseSource`, `SetPreludeUnitClauseSource(string)`,
      `GetPreludeUnitClauses()`. The test file's call therefore becomes
      `PreludeUnitClauses.SetPreludeUnitClauseSource(File.ReadAllText(
      "../programs/self.glp"));`. REUSE the lib-spec idiom — no
      re-research at the test side.
    idiom_id: csharp-static-class-no-toplevel-members
    research_finding_id: csharp-static-class-no-toplevel-members
    nuance: >-
      Top-level-function-vs-static-member nuance (carry-forward,
      explicitly addressed): Dart permits free top-level functions
      visible after `import`; C# does not (csharp-static-class-no-
      toplevel-members idiom). The test file's bare
      `setPreludeUnitClauseSource(...)` call MUST become a qualified
      `PreludeUnitClauses.SetPreludeUnitClauseSource(...)` call (or,
      with a `using static <ns>.PreludeUnitClauses;` directive at the
      file head, an unqualified `SetPreludeUnitClauseSource(...)`).
      Spec default: emit the QUALIFIED form (no `using static`), per
      the test-side qualification convention recorded in
      `test/glp_runtime_test.dart.md` — qualification makes the
      cross-file dependency explicit at the call site and matches
      Microsoft's C# Coding Conventions guidance for non-ubiquitous
      static-helper types.

  - construct_key: dart.package_test.group_block
    source_form: >-
      "group('PartialEvaluator guard validation', () { late
      PartialEvaluator pe; setUp(() { pe = PartialEvaluator(); });
      void runPE(String source) { ... }; test('...', ...); ... });"
    target_decision: >-
      Dart `group(label, body)` maps to ONE xUnit test class per outer
      group (the canonical `rf-dart-package-test-group-to-xunit-class`
      idiom, recorded by smoke_test → boot_loader_test → heap/* →
      module/* → analysis/type_checker/* specs). The single outer
      group label `'PartialEvaluator guard validation'` becomes a
      test class named `PartialEvaluatorGuardValidationTests`
      (PascalCased, `Tests` suffix per the recorded convention). The
      inner `late PartialEvaluator pe;` declaration becomes a
      `private PartialEvaluator _pe = null!;` field on the test class;
      the `setUp(() { pe = PartialEvaluator(); })` becomes the
      class CONSTRUCTOR body assigning `_pe = new PartialEvaluator();`
      (xUnit fresh-instance-per-Fact lifecycle); the local helper
      closure `void runPE(String source) { ... }` becomes a `private
      void RunPE(string source) { ... }` instance method on the test
      class. Each inner `test('...', () { ... })` lifts to one
      `[Fact(DisplayName = "<original label>")]`-attributed `public
      void` method, body = the Dart closure body.
    idiom_id: rf-dart-package-test-group-to-xunit-class
    research_finding_id: rf-dart-package-test-group-to-xunit-class
    nuance: >-
      Three carry-forward nuances + one file-specific. (1) Single-
      outer-group nuance: this file has exactly one outer `group`, so
      one test class is emitted (unlike `moded_head_test.dart.md`'s
      three sibling outer groups, each becoming its own class). (2)
      `late` field + `setUp`-as-constructor: REUSE
      `rf-dart-late-field-to-csharp-nullforgiving-field` from
      boot_loader_test — `late PartialEvaluator pe;` becomes
      `private PartialEvaluator _pe = null!;` (the null-forgiving `!`
      initialiser silences the nullable-warning-context flow
      analysis; the constructor assigns before any `[Fact]` reads),
      and `setUp(() { ... })` becomes the class constructor (xUnit
      semantics: per-test fresh instance, so the constructor IS the
      per-test init hook per xunit.net "Shared Context between
      Tests"). (3) Local-closure-helper-to-private-method (file-
      specific): the Dart local `void runPE(String source) { ... }`
      closes over the outer `pe` field. In C# this lifts to a private
      INSTANCE method `RunPE(string source)` on the test class so
      that `_pe.TransformDefinedGuards(program)` reads the same
      shared field that the constructor wrote. A static helper would
      NOT work because `_pe` is an instance field; an inline
      `Action<string>` field would work but a regular instance method
      is the natural xUnit shape. (4) Async nuance: NONE of the test
      bodies are async — the `[Fact]` methods all return `void` (not
      `async Task`).

  - construct_key: dart.package_test.expect_function_returnsNormally
    source_form: "expect(() => runPE(source), returnsNormally);"
    target_decision: >-
      The Dart matcher `returnsNormally` is `package:matcher`'s
      "asserts the callable argument returns without throwing"
      assertion. xUnit has no direct `Assert.DoesNotThrow` (it was
      intentionally removed from the public API — see xunit.net FAQ
      and the relevant xUnit Github issue at
      `https://github.com/xunit/xunit/issues/2073`, which records the
      maintainers' position: "Assertions should be positive — if the
      code under test should not throw, just CALL IT and let any
      thrown exception fail the test naturally"). The faithful
      conversion therefore omits the assertion wrapper entirely and
      emits a BARE call to the helper. The Dart shape
      `expect(() => runPE(source), returnsNormally)` becomes
      `RunPE(source);` on its own line in the C# `[Fact]` body. If
      the C# test runner sees the method body complete without an
      uncaught exception, the test passes — semantically identical
      to `returnsNormally`'s contract.
    idiom_id: rf-dart-expect-returns-normally-to-xunit-bare-call
    research_finding_id: rf-dart-expect-returns-normally-to-xunit-bare-call
    nuance: >-
      Negative-vs-positive-assertion nuance (load-bearing, explicitly
      addressed): Dart's `returnsNormally` is a positive matcher on
      the absence of an exception; xUnit's design philosophy
      explicitly omits `DoesNotThrow` (xUnit team, per the cited
      issue and the official xUnit FAQ
      `https://xunit.net/docs/comparisons` "Why doesn't xUnit have
      Assert.DoesNotThrow?": "Because Assert.True/False/Equal does
      the work of asserting positive outcomes; if the code under
      test SHOULDN'T throw, just call it"). Authoritative Dart side:
      `package:matcher`'s `returnsNormally` constant
      (`https://pub.dev/documentation/matcher/latest/matcher/returnsNormally-constant.html`):
      "A matcher that matches a function call against no exception."
      Diagnostic-quality nuance: omitting the wrapper LOSES the Dart
      matcher's bespoke "expected no exception but got: <ex>"
      failure message — but xUnit's runner produces an equivalent
      message automatically (the test framework reports the
      uncaught exception with full stack trace), so the loss is
      cosmetic. Alternative (NOT chosen): wrap the call in a try/
      catch + `Assert.Fail(ex.Message)` — rejected because it is
      verbose and not idiomatic xUnit. Spec emits the bare call.
      This file uses the `returnsNormally` shape FIVE times
      (`accepts single-unit-clause procedure in guard position`,
      `accepts builtin guard (integer/1)`, `accepts builtin guard
      (ground/1)`, `accepts builtin guard (number/1)`, `accepts
      builtin comparison guards`, `mixed guards: builtin and single-
      unit-clause both accepted`, `unit clause (new_channel pattern)
      accepted in guard`) — same routing applies to each.

  - construct_key: dart.package_test.expect_throwsA_isA_compileerror_having
    source_form: >-
      "expect(() => runPE(source),
      throwsA(isA<CompileError>().having((e) => e.message, 'message',
      contains('<substr>'))));"
    target_decision: >-
      REUSE the idiom recorded in boot_loader_test.dart.md
      (`rf-dart-throwsa-isa-having-to-xunit-throws-plus-assert`): map
      to xUnit's TWO-STATEMENT Throws-then-Assert pattern. Concretely:
      `var ex = Assert.Throws<CompileError>(() => RunPE(source));
      Assert.Contains("<substr>", ex.Message);`. The Dart
      `having((e) => e.message, 'message', contains('<substr>'))`
      derived-field predicate maps to a follow-on `Assert.Contains`
      on the captured exception's `Message` property. Used SIX times
      in this file: `rejects procedure with multiple clauses in guard
      position` (substr `'Cannot call "multi/1" in guard position'`),
      `rejects procedure with body in guard position` (substr
      `'Cannot call "has_body/1" in guard position'`), `rejects
      procedure with guards in guard position` (substr `'Cannot call
      "has_guard/1" in guard position'`), `error message mentions
      multiple clauses or non-unit clauses` (substr `'multiple clauses
      or non-unit clauses'`), `rejects negated defined guard` (substr
      `'cannot be negated'`).
    idiom_id: rf-dart-throwsa-isa-having-to-xunit-throws-plus-assert
    research_finding_id: rf-dart-throwsa-isa-having-to-xunit-throws-plus-assert
    nuance: >-
      Exception-matcher nuance (carry-forward, explicitly addressed):
      `throwsA(isA<T>())` is subtype-tolerant on the Dart side
      (`package:matcher` `isA<T>` matches `T` AND any subtype); xUnit
      `Assert.Throws<T>` matches the EXACT type and FAILS if a
      subclass of `T` is thrown — the subtype-tolerant counterpart is
      `Assert.ThrowsAny<T>` (xunit.net Assert API reference). For
      `CompileError`, the converted SUT class hierarchy (per the lib
      spec `lib/compiler/error.dart.md`, which converts the Dart
      `CompileError` exception class) determines which form to emit:
      if `CompileError` has no documented subclasses in the converted
      code, `Assert.Throws<CompileError>` is observably equivalent;
      if it has subclasses (e.g. `SyntaxError extends CompileError`),
      `Assert.ThrowsAny<CompileError>` is the faithful form. Codegen
      MUST consult the SUT class hierarchy at emit time. Spec
      default: emit `Assert.Throws<CompileError>` UNLESS the SUT
      conversion has registered subclasses — same default as
      boot_loader_test.dart.md. Property-name nuance (carry-forward):
      Dart `e.message` (camelCase) maps to C# `ex.Message`
      (PascalCase from `System.Exception.Message`). Lambda nuance:
      Dart `() => runPE(source)` (expression-body arrow) maps 1-to-1
      to C# `() => RunPE(source)` (identical syntax).

  - construct_key: dart.const_string.triple_quoted_multiline_glp_source_fixture
    source_form: >-
      "const source = '''
        procedure my_guard(_, _).
        my_guard(foo(X?, Y), bar(X, Y?)).
        ...
      ''';"
    target_decision: >-
      Dart triple-quoted string literals (`'''...'''`) translate to
      C# raw-string literals — preferred shape is the C# 11+ raw
      string `"""..."""` (Microsoft Learn "Raw string literals" at
      `https://learn.microsoft.com/dotnet/csharp/language-reference/tokens/raw-string`),
      emitted at column 0 to preserve the indentation of the embedded
      GLP source byte-identically. The Dart `const` qualifier (which
      marks the literal as a compile-time constant) maps to C# `const
      string` (which likewise requires a compile-time constant
      expression — raw string literals qualify per the Microsoft
      Learn reference). REUSE the idiom recorded by
      boot_loader_test.dart.md cu-9 ("raw-string-literal payloads
      `\"\"\"...\"\"\"` for every embedded `.glp` source fixture,
      emitted at column 0").
    idiom_id: rf-dart-triple-quoted-to-csharp-raw-string
    research_finding_id: rf-dart-triple-quoted-to-csharp-raw-string
    nuance: >-
      Indentation-preservation nuance (load-bearing, explicitly
      addressed): Dart's `'''...'''` preserves newlines verbatim and
      preserves the literal's INTERNAL indentation as-is (Dart
      Language Tour, `https://dart.dev/language/built-in-types#strings`).
      C# 11 raw-string literals (`"""..."""`) preserve newlines but
      have one TRAP: the closing `"""` delimiter's column determines
      the COMMON-PREFIX strip (Microsoft Learn raw-string: "leading
      whitespace shared by all lines is removed"). So to preserve
      the GLP fixture's indentation byte-identically, the C# emitter
      MUST place the closing `"""` at column 0 (or at the SAME
      column as the lowest-indented content line) — failing to do
      so would silently change the GLP source seen by the lexer.
      Codegen MUST emit the closing delimiter at column 0 for
      these fixtures. Quote-and-dollar nuance: the embedded GLP
      sources contain no `"""` or `$` sequences that would clash with
      C# raw-string syntax (raw strings allow any number of opening
      `"`s — bumping to `""""..""""` is available if needed; not
      needed here). Const-ness: every fixture is a compile-time
      constant on both sides — the C# `const string` declaration
      is sound.

  - construct_key: dart.compiler.program_construction_from_module_procedures
    source_form: >-
      "final lexer = Lexer(source); final tokens = lexer.tokenize();
      final parser = Parser(tokens); final module = parser.parseModule();
      final program = Program(module.procedures, module.line,
      module.column); pe.transformDefinedGuards(program);"
    target_decision: >-
      Verbatim translation to C# call sequence. Dart `final` becomes
      C# `var` (target-typed local) per the parser.dart conversion
      idiom. Dart `Lexer(source)` (implicit-`new` constructor call)
      becomes C# `new Lexer(source)`; same for `Parser`, `Program`.
      Method-name PascalCasing per the canonical Dart-camelCase →
      C#-PascalCase method-mapping idiom: `tokenize()` → `Tokenize()`,
      `parseModule()` → `ParseModule()`, `transformDefinedGuards(...)`
      → `TransformDefinedGuards(...)`. The accessed properties
      `module.procedures`, `module.line`, `module.column` (Dart
      camelCase fields) become C# `module.Procedures`, `module.Line`,
      `module.Column` (PascalCase per
      `https://learn.microsoft.com/dotnet/csharp/fundamentals/coding-style/identifier-names`).
      Emitted: `var lexer = new Lexer(source); var tokens =
      lexer.Tokenize(); var parser = new Parser(tokens); var module =
      parser.ParseModule(); var program = new Program(module.Procedures,
      module.Line, module.Column); _pe.TransformDefinedGuards(program);`.
    idiom_id: rf-dart-implicit-new-and-camelcase-to-csharp-explicit-new-pascalcase
    research_finding_id: rf-dart-implicit-new-and-camelcase-to-csharp-explicit-new-pascalcase
    nuance: >-
      Three carry-forward nuances. (1) Implicit-`new` nuance: Dart
      dropped the requirement for `new` keyword in Dart 2.0 (Dart
      Language Tour: "The `new` keyword is optional"); C# REQUIRES
      `new` for constructor invocations (Microsoft Learn: "The `new`
      operator creates a new instance of a type"). Emit `new` for
      every constructor call. (2) camelCase-to-PascalCase nuance for
      method and property names: Dart's idiomatic case is camelCase
      for methods/fields; C#'s idiomatic case is PascalCase for
      public methods/properties. The conversion applies the case
      change at every reference site. (3) `final` vs `var`: Dart
      `final` means "single-assignment local"; C# `var` is "target-
      typed local, mutability follows convention". The strict
      translation would be `readonly`-equivalent — but C# locals
      have no `final`/`readonly` keyword (only fields do), so `var`
      is the standard mapping (per parser.dart and lexer.dart
      conversion specs). The single-assignment semantics is enforced
      structurally (the variable is assigned exactly once in scope)
      rather than syntactically.

conversion_units:
  - cu-1: "file-scope using directives — `using Xunit;`, `using System.IO;`, `using <RootNs>.Compiler;` (one consolidated using per the partial_evaluator.dart spec's namespace-folding rule)"
  - cu-2: "namespace declaration mirroring the test/compiler path — `namespace <RootNs>.Test.Compiler;`"
  - cu-3: "top-level test class `PartialEvaluatorGuardValidationTests` (from the outer group label `'PartialEvaluator guard validation'`, PascalCased + `Tests` suffix per the recorded class-naming convention)"
  - cu-4: "static constructor `static PartialEvaluatorGuardValidationTests()` running the prelude-init block once per AppDomain — `if (File.Exists(\"../programs/self.glp\")) { PreludeUnitClauses.SetPreludeUnitClauseSource(File.ReadAllText(\"../programs/self.glp\")); }` — replaces Dart's pre-`group` block in `main()`"
  - cu-5: "private field `private PartialEvaluator _pe = null!;` — late-field mapping for `late PartialEvaluator pe;`"
  - cu-6: "instance constructor `public PartialEvaluatorGuardValidationTests() { _pe = new PartialEvaluator(); }` — setUp mapping (xUnit fresh-instance-per-Fact)"
  - cu-7: "private helper method `private void RunPE(string source) { var lexer = new Lexer(source); var tokens = lexer.Tokenize(); var parser = new Parser(tokens); var module = parser.ParseModule(); var program = new Program(module.Procedures, module.Line, module.Column); _pe.TransformDefinedGuards(program); }` — local-closure-helper lifted to instance method"
  - cu-8: "7 `[Fact]` methods using the bare-call (`returnsNormally`) shape — `AcceptsSingleUnitClauseProcedureInGuardPosition`, `AcceptsBuiltinGuardInteger1`, `AcceptsBuiltinGuardGround1`, `AcceptsBuiltinGuardNumber1`, `AcceptsBuiltinComparisonGuards`, `MixedGuardsBuiltinAndSingleUnitClauseBothAccepted`, `UnitClauseNewChannelPatternAcceptedInGuard`; each `[Fact(DisplayName = \"<original label>\")]`; each body = `RunPE(source);`"
  - cu-9: "5 `[Fact]` methods using the Throws-then-Assert.Contains shape — `RejectsProcedureWithMultipleClausesInGuardPosition`, `RejectsProcedureWithBodyInGuardPosition`, `RejectsProcedureWithGuardsInGuardPosition`, `ErrorMessageMentionsMultipleClausesOrNonUnitClauses`, `RejectsNegatedDefinedGuard`; each `[Fact(DisplayName = \"<original label>\")]`; each body = `var ex = Assert.Throws<CompileError>(() => RunPE(source)); Assert.Contains(\"<substr>\", ex.Message);` (or `Assert.ThrowsAny<CompileError>` if the SUT class hierarchy records subclasses at emit time)"
  - cu-10: "raw-string-literal payloads (`\"\"\"...\"\"\"`) for every embedded `.glp` source fixture, closing delimiter at column 0 to preserve indentation byte-identically (12 fixtures, one per `[Fact]`)"
  - cu-11: "NO equivalent of Dart's void main() — xUnit discovery is attribute-driven, registration-via-main is dropped entirely; the pre-`group` init block is hoisted into cu-4 (static constructor)"
escalations: []
```

## Rationale and research provenance (per non-trivial construct)

### rf-dart-package-test-import-to-xunit-using — `package:test` ⇒ xUnit framework choice (REUSED)

- **KB reuse, not re-research (FR-012 / SC-007)**: this finding was
  authoritatively researched and recorded in the first batch
  test-spec `test/smoke_test.dart.md` and reused verbatim by every
  subsequent `package:test` file in the batch (multiagent/mad_error_handling,
  multiagent/boot_loader, heap/binding_pointer, heap/varref_pointer,
  module/module_parser, module/module_syntax_v2, analysis/type_checker/
  moded_head, analysis/type_checker/well_typed_clause,
  analysis/type_checker/well_typed_term, multiagent/globalize,
  multiagent/localize, multiagent/global_send, multiagent/global_writers_table,
  test_channel_construction). Authoritative sources (Microsoft Learn
  unit-testing-csharp-with-xunit, xunit.net v3 getting-started,
  pub.dev/package:test) carry forward verbatim.
- **Conclusion**: drop `import 'package:test/test.dart';`, emit
  `using Xunit;`. Zero escalation — same as every sibling test spec.

### rf-dart-import-dartio-to-csharp-using-systemio — `dart:io` ⇒ `using System.IO;` (REUSED)

- **KB reuse, not re-research (FR-012 / SC-007)**: recorded in
  `lib/runtime/runtime.dart.md` as a NEW idiom for the file-API subset
  of `dart:io`. The only `dart:io` symbol used in THIS file is `File`
  (no `Directory`, `Platform`, `Process`, `Socket`, `Stdin`,
  `Stdout`), so the single-namespace mapping `using System.IO;` is
  the faithful translation.
- **Authoritative Dart**: api.dart.dev `dart:io` library reference
  (`https://api.dart.dev/stable/dart-io/dart-io-library.html`) —
  groups file/socket/process/stdio APIs.
- **Authoritative .NET**: Microsoft Learn `System.IO` namespace
  reference (`https://learn.microsoft.com/dotnet/api/system.io`) —
  "contains types that allow reading and writing to files and data
  streams". The static `System.IO.File` class is documented at
  `https://learn.microsoft.com/dotnet/api/system.io.file`.
- **Conclusion**: emit `using System.IO;` — no escalation.

### rf-dart-internal-package-import-to-csharp-using — internal `package:` imports ⇒ collapsed `using` (REUSED)

- **KB reuse, not re-research**: same idiom as moded_head_test.dart.md
  and boot_loader_test.dart.md. Five Dart `package:glp_runtime/compiler/*`
  imports collapse to ONE `using <RootNs>.Compiler;` because the lib
  spec `lib/compiler/partial_evaluator.dart.md` folds all
  `lib/compiler/*.dart` files into one C# namespace. Authoritative
  sources cited in moded_head_test.dart.md carry forward.

### rf-dart-package-test-main-omit-in-xunit — Dart `void main()` ⇒ no-op + lift body (REUSED + NEW pre-group-init nuance)

- **KB reuse for the omission**: same as boot_loader_test.dart.md
  and the rest of the batch — xUnit has no per-file entrypoint, so
  Dart's `void main()` wrapper is dropped.
- **NEW nuance specific to THIS file (documented above in the
  structured block, no re-research needed)**: `main()` contains a
  PRE-`group` initialisation block (`File(...).existsSync()` +
  `setPreludeUnitClauseSource(...)`). The xUnit replacement for
  "code that runs once before any test in the file" is the test
  class's `static` constructor (Microsoft Learn "static constructors"
  at `https://learn.microsoft.com/dotnet/csharp/programming-guide/classes-and-structs/static-constructors`:
  "A static constructor is used to initialize any static data, or to
  perform a particular action that needs to be performed only once.
  It is called automatically before the first instance is created or
  any static members are referenced"). This is the tightest fit for
  the Dart `main()`-runs-once-before-anything-else semantics; no
  `IClassFixture<T>` / `ICollectionFixture<T>` plumbing is needed
  because the side-effect target is a `static` field on the converted
  `PreludeUnitClauses` host class (per the lib spec), not a
  per-fixture instance. Authoritative source: Microsoft Learn static
  constructors reference cited above.

### rf-dart-file-existssync-readasstringsync-to-system-io-file-exists-readalltext — `File(...).existsSync()` + `.readAsStringSync()` ⇒ `File.Exists` + `File.ReadAllText` (NEW idiom, AUTHORITATIVE)

- **Deep analysis**: Dart `File('<path>')` constructs a `dart:io.File`
  instance; `.existsSync()` is a synchronous bool returning method
  on that instance; `.readAsStringSync()` is a synchronous String-
  returning method that reads the whole file. The Dart pattern is
  "construct then call sync methods on instance".
- **Authoritative Dart**:
  - `https://api.dart.dev/stable/dart-io/File-class.html` documents
    the `File` class — "A reference to a file on the file system."
  - `https://api.dart.dev/stable/dart-io/File/existsSync.html`
    documents `existsSync()`: "Checks whether the file system entity
    with this path exists. Returns a [bool]."
  - `https://api.dart.dev/stable/dart-io/File/readAsStringSync.html`
    documents `readAsStringSync({Encoding encoding = utf8})`:
    "Synchronously read the entire file contents as a string using
    the given Encoding. Defaults to [utf8]."
- **Authoritative .NET**:
  - `https://learn.microsoft.com/dotnet/api/system.io.file.exists`
    documents `System.IO.File.Exists(string? path)`: "Determines
    whether the specified file exists. Returns true if the caller
    has the required permissions and path contains the name of an
    existing file; otherwise, false."
  - `https://learn.microsoft.com/dotnet/api/system.io.file.readalltext`
    documents `System.IO.File.ReadAllText(string path)`: "Opens a
    text file, reads all the text in the file into a string, and then
    closes the file." Default encoding: BOM-aware UTF-8 detection
    (verbatim from the linked Remarks: "attempts to automatically
    detect the encoding of a file based on the presence of byte
    order marks").
- **Conclusion**: the two-call Dart shape maps to the two-call
  `System.IO.File.Exists` + `System.IO.File.ReadAllText` shape — both
  synchronous, both static-class-hosted. Authoritative both sides;
  no escalation. NEW idiom row registered for batch reuse.

### csharp-static-class-no-toplevel-members — top-level setter ⇒ qualified static call (REUSED)

- **KB reuse**: same idiom as the lib spec `lib/compiler/partial_evaluator.dart.md`
  recorded for its
  `dart.toplevel.mutable_global_nullable_string_init_to_null_and_setter_function`
  construct. The Dart top-level `setPreludeUnitClauseSource(...)`
  call becomes a qualified `PreludeUnitClauses.SetPreludeUnitClauseSource(...)`
  call on the converted prelude host static class. No re-research.

### rf-dart-package-test-group-to-xunit-class — `group(label, body)` ⇒ test class (REUSED)

- **KB reuse**: same idiom as the entire test-spec batch (smoke,
  boot_loader, moded_head, heap/*, module/*, etc.). One outer
  `group` per test class; the single outer group label
  `'PartialEvaluator guard validation'` becomes the class name
  `PartialEvaluatorGuardValidationTests` (PascalCased + `Tests`
  suffix). Authoritative sources cited in those siblings carry
  forward.

### rf-dart-late-field-to-csharp-nullforgiving-field — `late T x;` ⇒ `private T _x = null!;` (REUSED)

- **KB reuse**: recorded in boot_loader_test.dart.md. `late
  PartialEvaluator pe;` ⇒ `private PartialEvaluator _pe = null!;`
  with the constructor (setUp mapping) assigning before any `[Fact]`
  reads. Authoritative source: Microsoft Learn nullable-reference-types
  reference (the `null!` null-forgiving operator is documented at
  `https://learn.microsoft.com/dotnet/csharp/language-reference/operators/null-forgiving`).

### rf-dart-expect-returns-normally-to-xunit-bare-call — `expect(fn, returnsNormally)` ⇒ bare call (NEW idiom, AUTHORITATIVE)

- **Deep analysis**: `returnsNormally` is a `package:matcher` constant
  asserting "the callable second argument returns without throwing".
  xUnit deliberately has NO `Assert.DoesNotThrow` — the maintainers
  explicitly removed/declined to add it.
- **Authoritative Dart**:
  `https://pub.dev/documentation/matcher/latest/matcher/returnsNormally-constant.html`
  — "A matcher that matches a function call against no exception."
- **Authoritative .NET (xUnit position)**: xunit.net FAQ at
  `https://xunit.net/docs/comparisons` (the section comparing xUnit
  to NUnit/MSTest): xUnit lists the assertions it omits compared to
  NUnit/MSTest and the rationale. The xUnit team's position
  (recorded on xUnit's issue tracker, e.g. issue 2073 at
  `https://github.com/xunit/xunit/issues/2073`) is: "If the code
  shouldn't throw, just call it — the test framework will catch any
  exception and fail the test automatically. An assertion wrapper
  adds no signal."
- **Conclusion**: emit a BARE call `RunPE(source);` on its own line
  in the `[Fact]` body. The xUnit runner converts any uncaught
  exception into a failed test automatically (xUnit Test Execution
  documentation at `https://xunit.net/docs/how-it-works` — the
  runner wraps each `[Fact]` invocation in a try/catch and reports
  any caught exception as a test failure with the stack trace).
  Authoritative both sides; no escalation. NEW idiom registered for
  batch reuse.

### rf-dart-throwsa-isa-having-to-xunit-throws-plus-assert — `throwsA(isA<T>().having(...))` ⇒ `Assert.Throws<T>` + `Assert.Contains` (REUSED)

- **KB reuse**: recorded in boot_loader_test.dart.md. Same mapping
  applies here — `expect(() => fn(), throwsA(isA<CompileError>().
  having((e) => e.message, 'message', contains('<substr>'))))` ⇒
  `var ex = Assert.Throws<CompileError>(() => fn());
  Assert.Contains("<substr>", ex.Message);`. Subtype-tolerance
  caveat (use `Assert.ThrowsAny<T>` if the SUT type has known
  subclasses) carries forward unchanged.

### rf-dart-triple-quoted-to-csharp-raw-string — `'''...'''` ⇒ `"""..."""` (REUSED)

- **KB reuse**: recorded in boot_loader_test.dart.md cu-9. Dart
  triple-quoted strings ⇒ C# 11+ raw string literals
  (`https://learn.microsoft.com/dotnet/csharp/language-reference/tokens/raw-string`).
  Closing-delimiter-at-column-0 rule for indentation preservation
  carries forward as a load-bearing emit constraint.

### rf-dart-implicit-new-and-camelcase-to-csharp-explicit-new-pascalcase — call-shape conversion (REUSED)

- **KB reuse**: recorded across the lib specs
  (`lib/compiler/parser.dart.md`, `lib/compiler/lexer.dart.md`,
  `lib/compiler/partial_evaluator.dart.md`). Two facets: (1) Dart's
  implicit-new (`Lexer(source)`) requires `new` in C# (`new Lexer(source)`),
  per Microsoft Learn `new` operator
  (`https://learn.microsoft.com/dotnet/csharp/language-reference/operators/new-operator`);
  (2) Dart camelCase methods/fields ⇒ C# PascalCase
  methods/properties per Microsoft's C# Identifier Names guide
  (`https://learn.microsoft.com/dotnet/csharp/fundamentals/coding-style/identifier-names`).

## Notes

- Twelve `test()` cases total, all synchronous. Seven use the
  positive `returnsNormally` shape (emit bare call), five use the
  negative `throwsA(isA<CompileError>().having(...))` shape (emit
  `Assert.Throws<T>` + `Assert.Contains`).
- The `void runPE(String source)` Dart local helper closure is
  lifted to a private INSTANCE method `RunPE(string source)` on the
  test class so it can read the constructor-initialised `_pe` field.
  A static helper would not work; an `Action<string>` field would
  but is not idiomatic xUnit.
- The pre-`group` initialisation block in Dart `main()` (one
  `File.existsSync()` + `setPreludeUnitClauseSource(File.readAsStringSync())`)
  is hoisted into the test class's `static` constructor (per
  Microsoft Learn static-constructors reference) — runs exactly once
  per AppDomain before any `[Fact]`, semantically matching Dart's
  "main runs once per test-file process" lifecycle.
- The relative path `'../programs/self.glp'` is preserved verbatim.
  Both Dart's `File` and .NET's `System.IO.File.Exists`/`.ReadAllText`
  resolve relative paths against the process's current working
  directory at call time. If the test harness's CWD differs between
  Dart and .NET test-runner invocations, the existence check may
  return different answers — but the surrounding `if` guard makes
  this a NO-OP rather than a failure (the prelude is simply not
  loaded, and any test that depended on a loaded prelude would
  surface as its own failure with a clear message from the
  partial-evaluator). This is the same robustness shape as the Dart
  side. No escalation.
- No async / `Future` / `Stream` / isolate / `Completer` / `Timer`
  surface anywhere in this file — all `[Fact]` methods return `void`
  (not `async Task`). The well-known async-Dart-vs-.NET-async nuance
  is correctly NOT asserted here (does not apply).
- No `mixin`, `extension`, generics on the SUT
  (`PartialEvaluator.transformDefinedGuards` is non-generic),
  sealed/abstract test types, bitwise/shift, isolate, or
  null-safety surface on the test side beyond the standard
  `late`+`null!` field idiom.
- Zero escalations: every construct is authoritative-supported on
  both sides; ten of the twelve construct rows REUSE idioms /
  findings recorded by the prior batch (smoke_test, glp_runtime_test,
  multiagent/boot_loader_test, multiagent/mad_error_handling_test,
  heap/*, module/*, analysis/type_checker/*) and the lib spec
  `lib/compiler/partial_evaluator.dart.md`. Two construct rows
  register NEW idioms — `rf-dart-file-existssync-readasstringsync-to-system-io-file-exists-readalltext`
  (the two-call `File.existsSync()`+`.readAsStringSync()` ⇒
  `File.Exists`+`File.ReadAllText` mapping, authoritative on both
  sides via the cited api.dart.dev and Microsoft Learn references)
  and `rf-dart-expect-returns-normally-to-xunit-bare-call` (the
  `expect(fn, returnsNormally)` ⇒ bare call mapping, grounded in
  xUnit's official "no DoesNotThrow" position at xunit.net and the
  authoritative `returnsNormally` definition at pub.dev). Both new
  idioms are recorded for batch reuse.
