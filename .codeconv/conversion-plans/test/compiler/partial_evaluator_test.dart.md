---
path: test/compiler/partial_evaluator_test.dart
cycle_group_id: 116
scc_siblings: []
generated_at: 2026-05-21T17:25:00Z
source_sha256: b6a416de5607acf814c73228a7dd938d0b2de5ce07856b7bf42931c4628d5c2a
schema_version: 1
---

# Conversion Plan: test/compiler/partial_evaluator_test.dart

## 1. Source Analysis

The file is a `package:test`-based unit-test suite of 285 lines exercising
`PartialEvaluator.transformDefinedGuards` (guard validation surface). It
has the following Dart-side anatomy verified by direct inspection of
`glp_runtime_net/test/compiler/partial_evaluator_test.dart`:

- **File-header import directives (lines 12-18)**: `dart:io`,
  `package:test/test.dart`, and five `package:glp_runtime/compiler/*`
  imports — `partial_evaluator.dart`, `parser.dart`, `lexer.dart`,
  `ast.dart`, `error.dart`.
- **`void main()` entrypoint (lines 20-285)** containing:
  - **Pre-`group` initialisation block (lines 22-25)**: constructs
    `File('../programs/self.glp')`, checks `existsSync()`, and on hit
    calls top-level `setPreludeUnitClauseSource(rootSelfGlp.readAsStringSync())`.
  - **One outer `group('PartialEvaluator guard validation', () { ... })`
    (lines 26-284)** containing:
    - `late PartialEvaluator pe;` field (line 27).
    - `setUp(() { pe = PartialEvaluator(); });` (lines 29-31).
    - Local helper closure `void runPE(String source) { ... }` (lines
      35-42) that builds Lexer→Parser→Program→`pe.transformDefinedGuards`.
    - **12 `test()` cases** (verified by hand): 7 positive
      (`returnsNormally`) and 5 negative
      (`throwsA(isA<CompileError>().having((e) => e.message, 'message',
      contains('<substr>')))`). The positive set is: `accepts
      single-unit-clause procedure in guard position` (line 44), `accepts
      builtin guard (integer/1)` (line 61), `accepts builtin guard
      (ground/1)` (line 74), `accepts builtin guard (number/1)` (line 86),
      `accepts builtin comparison guards` (line 98), `mixed guards:
      builtin and single-unit-clause both accepted` (line 222), `unit
      clause (new_channel pattern) accepted in guard` (line 240). The
      negative set is: `rejects procedure with multiple clauses in guard
      position` (line 118, substr `'Cannot call "multi/1" in guard
      position'`), `rejects procedure with body in guard position` (line
      144, substr `'Cannot call "has_body/1" in guard position'`),
      `rejects procedure with guards in guard position` (line 172, substr
      `'Cannot call "has_guard/1" in guard position'`), `error message
      mentions multiple clauses or non-unit clauses` (line 197, substr
      `'multiple clauses or non-unit clauses'`), `rejects negated defined
      guard` (line 260, substr `'cannot be negated'`).
- **All test bodies are synchronous** (no `async` / `await` / `Future` /
  `Stream` / `Completer` / `Timer` / isolate surface).
- **Each test body declares `const source = '''...''';`** — a Dart
  triple-quoted multi-line string containing GLP source text — then
  invokes `expect(() => runPE(source), <matcher>)`.

Confirmed: 12 `test()` calls (count from spec — 7 positive + 5 negative
= 12; the convspec notes "twelve `test()` cases" and individually
enumerates 12 test labels).

## 2. Dart → C#/.NET Conversion Plan

Each construct in this section mirrors a row from the ratified convspec
`constructs:` block at `.codeconv/conversion-specs/test/compiler/partial_evaluator_test.dart.md`.
The convspec rows are the source of truth for the conversion decisions;
this section reproduces them in narrative form for the codegen stage.

- **`dart.package_test.import_directive` → `using Xunit;`**: Drop the
  Dart `import 'package:test/test.dart';` directive; emit `using
  Xunit;` at the file head. Framework choice reuses the batch-wide
  xUnit pin recorded by `test/smoke_test.dart.md`. Idiom
  `rf-dart-package-test-import-to-xunit-using`.

- **`dart.dart_io.import_directive` → `using System.IO;`**: Drop the
  Dart `import 'dart:io';` directive; emit `using System.IO;`. The only
  `dart:io` symbol used in THIS file is `File`, so the single-namespace
  `using System.IO;` covers the entire surface (no `Directory`,
  `Platform`, `Process`, `Socket`, `Stdin`, `Stdout` references). Idiom
  `rf-dart-import-dartio-to-csharp-using-systemio`.

- **`dart.internal_package_import.glp_runtime_compiler_set` → ONE
  `using <RootNs>.Compiler;`**: All five Dart `package:glp_runtime/compiler/*`
  imports collapse to ONE `using <RootNs>.Compiler;` because the lib
  spec `lib/compiler/partial_evaluator.dart.md` folds all
  `lib/compiler/*.dart` files into one C# namespace. The five referenced
  symbols (`PartialEvaluator`, `Lexer`, `Parser`, `Program`,
  `CompileError`) all live under that single namespace. Idiom
  `rf-dart-internal-package-import-to-csharp-using`.

- **`dart.package_test.main_entrypoint` → no entrypoint; lift body**:
  Eliminate `void main()` entirely (xUnit is attribute-driven, no
  per-file entrypoint). The pre-`group` init block lifts to a `static`
  constructor on the lifted test class (per the next constructs); the
  inner `group(...)` lifts to the test class declaration. Idiom
  `rf-dart-package-test-main-omit-in-xunit`.

- **`dart.platform.file_existsSync_readAsStringSync` → `File.Exists` +
  `File.ReadAllText`**: Map Dart `File('<path>').existsSync()` /
  `File('<path>').readAsStringSync()` (instance class + sync methods)
  to .NET `System.IO.File.Exists(<path>)` / `System.IO.File.ReadAllText(<path>)`
  (static class + static methods that take the path directly). Emitted
  shape: `if (File.Exists("../programs/self.glp")) {
  PreludeUnitClauses.SetPreludeUnitClauseSource(File.ReadAllText(
  "../programs/self.glp")); }`. Relative path preserved verbatim. Idiom
  `rf-dart-file-existssync-readasstringsync-to-system-io-file-exists-readalltext`.

- **`dart.module.global_setter_function` → qualified static call**: Map
  the bare-function call `setPreludeUnitClauseSource(...)` to
  `PreludeUnitClauses.SetPreludeUnitClauseSource(...)` — the converted
  prelude-unit-clauses host is a `internal static class
  PreludeUnitClauses` per `lib/compiler/partial_evaluator.dart.md`.
  Spec default: qualified form (no `using static`). Idiom
  `csharp-static-class-no-toplevel-members`.

- **`dart.package_test.group_block` → one xUnit test class**: Map the
  single outer `group('PartialEvaluator guard validation', () { ... })`
  to ONE xUnit test class named `PartialEvaluatorGuardValidationTests`
  (PascalCased outer-label + `Tests` suffix per the batch-wide
  convention). The inner pieces map as follows:
  - `late PartialEvaluator pe;` → `private PartialEvaluator _pe = null!;`
    instance field (null-forgiving initialiser; constructor writes before
    any `[Fact]` reads).
  - `setUp(() { pe = PartialEvaluator(); });` → class
    INSTANCE-CONSTRUCTOR body `_pe = new PartialEvaluator();` (xUnit
    fresh-instance-per-Fact semantics).
  - `void runPE(String source) { ... }` local closure → private
    instance method `private void RunPE(string source) { ... }` on the
    test class (so it can read the `_pe` field).
  - Each `test('label', () { ... })` → one `[Fact(DisplayName =
    "<original label>")]` public void method with the closure body.
  Idiom `rf-dart-package-test-group-to-xunit-class`.

- **`dart.package_test.expect_function_returnsNormally` → bare call**:
  Map `expect(() => runPE(source), returnsNormally)` to a BARE call
  `RunPE(source);` on its own line in the `[Fact]` body. xUnit has no
  `Assert.DoesNotThrow` (intentional design — `https://github.com/xunit/xunit/issues/2073`);
  if the `[Fact]` body completes without an uncaught exception, the
  test passes. Applies to 7 positive cases. Idiom
  `rf-dart-expect-returns-normally-to-xunit-bare-call`.

- **`dart.package_test.expect_throwsA_isA_compileerror_having` →
  `Assert.Throws<T>` + `Assert.Contains`**: Map
  `expect(() => runPE(source), throwsA(isA<CompileError>().having((e)
  => e.message, 'message', contains('<substr>'))))` to the two-statement
  xUnit shape: `var ex = Assert.Throws<CompileError>(() => RunPE(source));
  Assert.Contains("<substr>", ex.Message);`. Subtype-tolerance caveat:
  if the SUT's `CompileError` has known subclasses at codegen time,
  emit `Assert.ThrowsAny<CompileError>` instead (per
  boot_loader_test.dart.md). Applies to 5 negative cases. Idiom
  `rf-dart-throwsa-isa-having-to-xunit-throws-plus-assert`.

- **`dart.const_string.triple_quoted_multiline_glp_source_fixture` →
  C# 11+ raw-string literal**: Map Dart `const source = '''...''';` to
  C# `const string source = """..."""` (raw-string literal). Emit the
  closing `"""` at column 0 (or at the same column as the
  lowest-indented content line) to preserve the GLP fixture's
  indentation byte-identically (Microsoft Learn raw-string: "leading
  whitespace shared by all lines is removed"). Idiom
  `rf-dart-triple-quoted-to-csharp-raw-string`.

- **`dart.compiler.program_construction_from_module_procedures` →
  verbatim C# call sequence with PascalCase**: Map the Dart helper body
  (Lexer → tokenize → Parser → parseModule → Program → transformDefinedGuards)
  to the verbatim C# sequence with `new` keyword on every constructor
  and PascalCased method/property names: `var lexer = new Lexer(source);
  var tokens = lexer.Tokenize(); var parser = new Parser(tokens); var
  module = parser.ParseModule(); var program = new Program(module.Procedures,
  module.Line, module.Column); _pe.TransformDefinedGuards(program);`.
  Dart `final` → C# `var`. Idiom
  `rf-dart-implicit-new-and-camelcase-to-csharp-explicit-new-pascalcase`.

## 3. Decomposed Task Units

- **T1**: Emit file-header `using` directives (`using Xunit;`, `using
  System.IO;`, `using <RootNs>.Compiler;`). done
- **T2**: Emit namespace declaration `namespace <RootNs>.Test.Compiler;`.
  done
- **T3**: Emit test class declaration `public class
  PartialEvaluatorGuardValidationTests`. done
- **T4**: Emit `static PartialEvaluatorGuardValidationTests()` static
  constructor with the prelude-init block (`File.Exists` +
  `PreludeUnitClauses.SetPreludeUnitClauseSource(File.ReadAllText(...))`).
  done
- **T5**: Emit private field `private PartialEvaluator _pe = null!;`.
  done
- **T6**: Emit public instance constructor `public
  PartialEvaluatorGuardValidationTests() { _pe = new PartialEvaluator(); }`.
  done
- **T7**: Emit private helper `private void RunPE(string source) { ... }`
  with the verbatim Lexer→Parser→Program→TransformDefinedGuards sequence.
  done
- **T8**: Emit 7 positive `[Fact]` methods using the bare-call shape
  (`RunPE(source);`), one per positive Dart `test()` case, each with a
  `[Fact(DisplayName = "<original label>")]` attribute and a `const
  string source = """..."""` raw-string fixture. done
- **T9**: Emit 5 negative `[Fact]` methods using the
  `Assert.Throws<CompileError>` + `Assert.Contains` shape, one per
  negative Dart `test()` case, each with the corresponding substring
  literal. done
- **T10**: For each of the 12 fixtures, emit the GLP source as a C# 11+
  raw-string literal (`"""..."""`) with the closing delimiter at column
  0 to preserve indentation byte-identically. done
- **T11**: Omit any equivalent of Dart's `void main()`; xUnit discovery
  is reflection-driven. done

## 4. Research Findings

none required — every construct row in §2 reuses an idiom recorded in
the ratified convspec at `.codeconv/conversion-specs/test/compiler/partial_evaluator_test.dart.md`
(which carries cross-references to the lib spec
`lib/compiler/partial_evaluator.dart.md` and to the prior test-spec
batch — smoke_test, boot_loader_test, moded_head_test, glp_runtime_test,
heap/*, module/*, analysis/type_checker/*). Two of those idioms are NEW
(`rf-dart-file-existssync-readasstringsync-to-system-io-file-exists-readalltext`
and `rf-dart-expect-returns-normally-to-xunit-bare-call`) and were
authoritatively grounded in the convspec itself (api.dart.dev,
Microsoft Learn `System.IO.File`, xunit.net, pub.dev `package:matcher`,
GitHub xunit/xunit#2073) — they require no additional research at the
planning stage.

## 5. Consistency Pass

fixed — derived from `.codeconv/conversion-specs/test/compiler/partial_evaluator_test.dart.md`
(the ratified convspec). Each construct in §2 cites its convspec
`construct_key` and reuses the convspec's `idiom_id` /
`research_finding_id` verbatim; the 11 task units in §3 mirror the
convspec's `conversion_units` cu-1…cu-11 one-for-one. No spec
amendments needed.

## 6. Escalations

None.
