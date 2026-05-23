---
path: test/srsw_test.dart
cycle_group_id: 159
scc_siblings: []
generated_at: 2026-05-21T16:44:51Z
source_sha256: 651ad3d1b41dabc4cf7d9d2bff2c273d81d7020400a6429734be6bd1b08f240d
schema_version: 1
---

# Conversion Plan: test/srsw_test.dart

## 1. Source Analysis

Direct inspection of `glp_runtime_net/test/srsw_test.dart` (75 lines, sha256 `651ad3d1…f240d`) confirms the file shape recorded by the convspec:

- **Line 1**: `import 'package:test/test.dart';` — test framework import (no file-header banner comment; file starts directly with import).
- **Line 2**: `import 'package:glp_runtime/compiler/compiler.dart';` — single internal-package import for `GlpCompiler`.
- **Line 4**: `void main() {` — entrypoint, no library directive, no `setUp`/`tearDown`, no `late` field.
- **Lines 5–12**: `test('SRSW violation: repeated variable should be rejected', () { ... });` — instantiates `final compiler = GlpCompiler();`, calls `print('\nTesting SRSW violation: same(f(X, X))');`, asserts `expect(() => compiler.compile('same(f(X, X)).'), throwsException);`, then `print('✅ Correctly rejected repeated variable');`.
- **Lines 14–30**: `test('Anonymous variable _ in head argument compiles without SRSW error', () { ... });` — `print` banner, `final compiler = GlpCompiler();`, `final source = '''…''';` (triple-quoted GLP fixture with `procedure foo(_?, _).` and a clause), `final program = compiler.compile(source);`, `expect(program, isNotNull);`, `expect(program.ops.length, greaterThan(0));`, two success-marker `print` lines (the second uses interpolation `${program.ops.length}`).
- **Lines 32–56**: `test('Anonymous variable _ passes SRSW where named variable would fail', () { ... });` — `print` banner, `final compiler = GlpCompiler();`, two triple-quoted GLP fixtures (`final badSource = '''…''';` and `final goodSource = '''…''';`), `expect(() => compiler.compile(badSource), throwsException, reason: 'Result with no reader should fail SRSW');` (the ONLY `reason:` argument in the file), `print('✅ Named variable correctly rejected (no reader)');`, then `final program = compiler.compile(goodSource);` + `expect(program, isNotNull);` + `print('✅ _ correctly accepted (anonymous)');`.
- **Lines 58–74**: `test('SRSW rejects guard-only readers without groundness', () { ... });` — `print` banner, `final compiler = GlpCompiler();`, `final badSource = '''foo(X) :- otherwise | bar.\n''';`, `expect(() => compiler.compile(badSource), throwsException, reason: 'otherwise does not ground X, so X has no reader');`, success-marker `print`. (Re-reading the source: the `reason:` is in fact present here at line 72 as `'otherwise does not ground X, so X has no reader'` — a SECOND reason argument; the convspec mentioned only the test-#3 reason explicitly but the cu-8 enumeration "tests #1, #3-negative-branch, #4" + the spec-default "reason dropped" rule covers both reason sites uniformly.)
- **Line 75**: closing `}` of `main`.

**Construct tally** (matches convspec):
- 1 file-header banner: NONE.
- 2 imports (Dart) → 2 `using` directives.
- 1 `void main()` → eliminated (xUnit attribute-driven).
- 4 sibling `test('…', () { … });` calls inside `main`, NO outer `group(...)` → 4 `[Fact]` methods on a single test class `SrswTests`.
- 4 `final compiler = GlpCompiler();` local instantiations (one per test).
- 3 `expect(() => compiler.compile(...), throwsException [, reason: ...]);` sites (test #1 line 10, test #3 lines 43–44, test #4 lines 71–72).
- 2 `expect(program, isNotNull);` sites (test #2 line 26, test #3 line 54).
- 1 `expect(program.ops.length, greaterThan(0));` site (test #2 line 27).
- 4 triple-quoted GLP source fixtures (`final source` in test #2; `final badSource` + `final goodSource` in test #3; `final badSource` in test #4).
- 1 inline single-quoted GLP-source string (`'same(f(X, X)).'`, line 10).
- 13 `print(...)` diagnostic statements (banner + success markers + the interpolation line), including UTF-8 `'✅'` glyphs (U+2705) and one `${program.ops.length}` interpolation.

**Async/IO surface**: NONE. Every test body is synchronous; no `Future`/`Stream`/isolate/`Completer`/`Timer`/`dart:io`/`dart:async`.
**State surface**: NONE. No `late` field, no `setUp`/`tearDown`, no shared per-test state, no helper hoist.
**Dependencies**: ONE internal — `lib/compiler/compiler.dart` (`GlpCompiler`).

## 2. Dart → C#/.NET Conversion Plan

The plan mirrors the convspec exactly; each row below corresponds to the matching `constructs:` row in `.codeconv/conversion-specs/test/srsw_test.dart.md`.

### 2.1 `dart.package_test.import_directive` (REUSED idiom `rf-dart-package-test-import-to-xunit-using`)

- **Source**: `import 'package:test/test.dart';`
- **Target**: drop the Dart import; emit `using Xunit;` at file scope.
- **Rationale**: framework choice (xUnit) pinned batch-wide since `test/smoke_test.dart.md` (per SC-007); no re-research per FR-012 / SC-007.
- **Out of scope**: the test-project `.csproj` referencing `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk` — owned at the langpair / project-file level, not per-file.

### 2.2 `dart.internal_package_import.glp_runtime_compiler_single_file` (REUSED idiom `rf-dart-internal-package-import-to-csharp-using`)

- **Source**: `import 'package:glp_runtime/compiler/compiler.dart';`
- **Target**: `using Glp.Runtime.Compiler;` (single using; no `as`/`show`).
- **Rationale**: per `lib/compiler/compiler.dart.md`, all `lib/compiler/*` Dart files collapse into one C# namespace `Glp.Runtime.Compiler`. This file imports ONE Dart file → ONE `using` (no folding-from-multiple).

### 2.3 `dart.package_test.main_entrypoint` (REUSED idiom `rf-dart-package-test-main-omit-in-xunit`)

- **Source**: `void main() { test(...); test(...); test(...); test(...); }`
- **Target**: eliminate `main` entirely; xUnit discovery is attribute-driven.
- **Rationale**: `main` body has ZERO pre-`test` statements (empty-main branch — same as `reserved_constant_test.dart.md`'s empty main, contrast with `partial_evaluator_test.dart.md`'s file-IO main). No `static` constructor needed.

### 2.4 `dart.package_test.test_calls_no_outer_group` (REUSED idiom `rf-dart-package-test-group-to-xunit-class`, no-outer-group facet)

- **Source**: four sibling `test('<label>', () { ... });` calls directly inside `main`, NO outer `group(...)`.
- **Target**: a single public test class `SrswTests` (file stem PascalCased + `Tests` suffix because no outer group exists; class-name source is the file stem per the convspec's stem-derivation convention) inside `namespace <RootNs>.Test;`, containing four `[Fact(DisplayName = "<original Dart label verbatim>")] public void <MethodName>()` methods.
- **Method names** (illustrative; codegen sanitises by removing punctuation and PascalCasing tokens):
  - `SrswViolationRepeatedVariableShouldBeRejected` ← `'SRSW violation: repeated variable should be rejected'`
  - `AnonymousVariableInHeadArgumentCompilesWithoutSrswError` ← `'Anonymous variable _ in head argument compiles without SRSW error'`
  - `AnonymousVariablePassesSrswWhereNamedVariableWouldFail` ← `'Anonymous variable _ passes SRSW where named variable would fail'`
  - `SrswRejectsGuardOnlyReadersWithoutGroundness` ← `'SRSW rejects guard-only readers without groundness'`
- **DisplayName preservation**: each `[Fact(DisplayName = "…")]` carries the Dart label byte-identically (colon-and-space, underscore characters, etc.).
- **No constructor** (instance or static). **No `private static` helper** — each `[Fact]` opens with its own `var compiler = new GlpCompiler();` local.

### 2.5 `dart.constructor_call.implicit_new_local_var` (REUSED idioms `rf-dart-final-local-to-csharp-var-local` + `rf-dart-implicit-new-and-camelcase-to-csharp-explicit-new-pascalcase`, composed)

- **Source**: `final compiler = GlpCompiler();` (FOUR occurrences, one per test).
- **Target**: `var compiler = new GlpCompiler();` (FOUR occurrences, one per `[Fact]`).
- **Rationale**: Dart `final` local → C# `var` local (mutability nuance: tests never reassign `compiler`, so the difference is observably irrelevant); Dart implicit-`new` → C# explicit `new` (REQUIRED in C# per Microsoft Learn "new operator"). `GlpCompiler` already PascalCase on both sides (class names); local `compiler` stays lowerCamelCase per C# identifier-names guide.

### 2.6 `dart.package_test.expect_call_throwsException` (NEW IDIOM `rf-dart-expect-throws-exception-to-xunit-assert-throws-exception`)

- **Source** (3 sites):
  - Line 10: `expect(() => compiler.compile('same(f(X, X)).'), throwsException);`
  - Lines 43–44: `expect(() => compiler.compile(badSource), throwsException, reason: 'Result with no reader should fail SRSW');`
  - Lines 71–72: `expect(() => compiler.compile(badSource), throwsException, reason: 'otherwise does not ground X, so X has no reader');`
- **Target**:
  - `Assert.Throws<Exception>(() => compiler.Compile("same(f(X, X)).") );`
  - `Assert.Throws<Exception>(() => compiler.Compile(badSource));` (reason dropped per spec default)
  - `Assert.Throws<Exception>(() => compiler.Compile(badSource));` (reason dropped per spec default)
- **Rationale**: Dart `throwsException` constant (`package:matcher`) asserts the closure throws something implementing Dart's `Exception` marker interface; the C# faithful mapping is `Assert.Throws<Exception>` rooted at `System.Exception` (subtype-tolerance carve-out: `Assert.Throws<T>` requires exact-T but with `T = Exception` (root), exact-match and any-match converge — the strict-faithful `Assert.ThrowsAny<Exception>` is documented but unused, matching the cached batch default).
- **Reason argument** (lines 43–44, 71–72): `Assert.Throws<T>(Action)` has no message-bearing overload. **Spec default = drop the reason** (same as the cached batch default; the alternative — wrap with `Assert.Fail(...)` in a try/catch — is documented but unused).
- **Lambda shape**: Dart arrow lambda `() => compiler.compile(...)` maps 1-to-1 to C# `() => compiler.Compile(...)`; method name PascalCases (`compile` → `Compile`) via the cached camelCase→PascalCase idiom.
- **String literal in lambda** (line 10): `'same(f(X, X)).'` → `"same(f(X, X))."` per §2.10 below.

### 2.7 `dart.package_test.expect_isNotNull` (REUSED idiom `rf-dart-expect-isNotNull-to-xunit-assert-notnull`)

- **Source** (2 sites):
  - Line 26: `expect(program, isNotNull);`
  - Line 54: `expect(program, isNotNull);`
- **Target**: `Assert.NotNull(program);` (twice).
- **Rationale**: cached idiom; neither call site carries a `reason:`, so the single-arg overload suffices. `program` is `Program` (per `lib/compiler/compiler.dart.md`); under nullable-reference-types in net8+ it's non-nullable, but `Assert.NotNull` is worth emitting because the SUT may evolve.

### 2.8 `dart.package_test.expect_length_greaterThan` (NEW IDIOM `rf-dart-expect-length-greaterthan-to-xunit-assert-true`, composed with cached `rf-dart-list-length-to-csharp-list-count`)

- **Source** (1 site): line 27: `expect(program.ops.length, greaterThan(0));`
- **Target**: `Assert.True(program.Ops.Count > 0);`
- **Rationale**: xUnit deliberately omits `Assert.Greater` per the xunit.net "Comparisons" page (positive-assertions-only stance); the canonical workaround is `Assert.True(bool)`. Composed with `rf-dart-list-length-to-csharp-list-count`: Dart `.length` on `List<T>` → C# `.Count`. `program.ops` field/getter → `program.Ops` property (cached `rf-dart-camelcase-to-csharp-pascalcase`). Strict `>` (excludes equality) preserved.

### 2.9 `dart.const_string.triple_quoted_multiline_glp_source_fixture` (REUSED idiom `rf-dart-triple-quoted-to-csharp-raw-string`, with `final`-not-`const` facet)

- **Source** (4 sites): `final source = '''…''';` (test #2), `final badSource = '''…''';` + `final goodSource = '''…''';` (test #3), `final badSource = '''…''';` (test #4). All four embed multi-line GLP source.
- **Target**: `var source = """…""";` / `var badSource = """…""";` / `var goodSource = """…""";` / `var badSource = """…""";` — C# 11+ raw-string literals with the closing `"""` delimiter at column 0 to preserve fixture indentation byte-identically.
- **Rationale**: cached idiom; new facet — the Dart side here uses `final` (NOT `const`, contrast with `reserved_constant_test.dart.md`), so C# target is `var` (NOT `const string`), composed with `rf-dart-final-local-to-csharp-var-local`. No fixture contains a `"""` triple-double-quote sequence, so no delimiter-bumping (to `""""…""""`) is needed.
- **Indentation rule (load-bearing)**: closing `"""` MUST be at column 0 — C# 11 raw strings strip the common-prefix shared by all lines based on the closing delimiter's column; misplacing the delimiter silently changes the GLP source seen by the lexer.

### 2.10 `dart.string_literal.single_quoted_glp_source_inline` (NEW IDIOM `rf-dart-string-literal-to-csharp-string-literal-quote-swap`)

- **Source** (1 site): line 10, `'same(f(X, X)).'`.
- **Target**: `"same(f(X, X))."` (mechanical single-quote → double-quote swap; content contains no `"` characters).
- **Rationale**: C# reserves `'…'` for `char` literals; strings use `"…"` only. The literal at this site is unambiguously a string (>1 character).

### 2.11 `dart.print_statement.diagnostic_log_to_stdout` (REUSED idiom `rf-dart-print-and-terminate-to-csharp-equivalent`)

- **Source** (13 sites): `print('\nTesting …');`, `print('✅ Correctly rejected repeated variable');`, `print('   Generated ${program.ops.length} instructions');`, etc. — diagnostic banner-and-marker statements interleaved through each test body.
- **Target**: `System.Console.WriteLine("…");` for each, preserving:
  - The leading `\n` escape (Dart `'\nTesting …'` → C# `"\nTesting …"`; both languages interpret `\n` in regular string literals).
  - The `'✅'` U+2705 glyph (UTF-8 source-file encoding required; no `\uXXXX` escape).
  - The string interpolation: Dart `'   Generated ${program.ops.length} instructions'` → C# `$"   Generated {program.Ops.Count} instructions"` (cached `rf-dart-string-interpolation-to-csharp-interpolated-string`, composed with `.length` → `.Count`).
- **Rationale**: spec default = `Console.WriteLine` (matches the batch convention of stateless test classes with no constructor-injected `ITestOutputHelper`). Trade-off documented: xUnit v2+ on .NET Core/5+ does NOT auto-capture `Console.WriteLine` (capture mechanism is `ITestOutputHelper`). Loss of per-test capture is acceptable here because the `print`s are debugging aids, not assertion-load-bearing.
- **Recorded alternative**: switch all four `[Fact]` bodies to use `ITestOutputHelper.WriteLine` with a constructor `public SrswTests(ITestOutputHelper output) { _output = output; }`. Unused per the batch convention.
- **File encoding**: target `.cs` MUST be emitted in UTF-8 (with or without BOM) so the `✅` glyph survives byte-identical embedding.

### 2.12 Method bodies (lifted verbatim from Dart closures)

The four `[Fact]` bodies are each a self-contained Arrange / Act / Assert block, lifting the Dart closure body 1-to-1 via the rows above. Pseudo-shape (the codegen owns the exact text):

- **Test #1** (`SrswViolationRepeatedVariableShouldBeRejected`): `Console.WriteLine` banner → `var compiler = new GlpCompiler();` → `Assert.Throws<Exception>(() => compiler.Compile("same(f(X, X)).") );` → success-marker `Console.WriteLine`.
- **Test #2** (`AnonymousVariableInHeadArgumentCompilesWithoutSrswError`): banner → `var compiler = …` → `var source = """…""";` → `var program = compiler.Compile(source);` → `Assert.NotNull(program);` → `Assert.True(program.Ops.Count > 0);` → two success-marker `Console.WriteLine` (the second uses interpolation).
- **Test #3** (`AnonymousVariablePassesSrswWhereNamedVariableWouldFail`): banner → `var compiler = …` → `var badSource = """…""";` → `Assert.Throws<Exception>(() => compiler.Compile(badSource));` (reason dropped) → success-marker `Console.WriteLine` → `var goodSource = """…""";` → `var program = compiler.Compile(goodSource);` → `Assert.NotNull(program);` → success-marker `Console.WriteLine`.
- **Test #4** (`SrswRejectsGuardOnlyReadersWithoutGroundness`): banner → `var compiler = …` → `var badSource = """foo(X) :- otherwise | bar.\n""";` → `Assert.Throws<Exception>(() => compiler.Compile(badSource));` (reason dropped) → success-marker `Console.WriteLine`.

## 3. Decomposed Task Units

- **T1**: emit `using Xunit;` directive at file scope. *done*
- **T2**: emit `using Glp.Runtime.Compiler;` directive at file scope. *done*
- **T3**: omit Dart `void main()` (no per-file entrypoint in xUnit; no pre-`test` statements to hoist). *done*
- **T4**: declare `namespace <RootNs>.Test;` (file-scoped) mirroring `test/` (no sub-directory because file sits directly in `test/`). *done*
- **T5**: declare public test class `SrswTests` (file stem PascalCased + `Tests` suffix). *done*
- **T6**: emit NO constructor (neither static nor instance) and NO `private static` helper on `SrswTests`. *done*
- **T7**: emit four `[Fact(DisplayName = "<original Dart label verbatim>")] public void <MethodName>()` methods; method names per §2.4 above. *done*
- **T8**: in EACH `[Fact]` body, emit `var compiler = new GlpCompiler();` as the first non-diagnostic statement (Arrange). *done*
- **T9**: emit three `Assert.Throws<Exception>(() => compiler.Compile(<arg>));` sites (test #1 with inline string literal, tests #3-negative-branch and #4 with `badSource` locals); drop both `reason:` arguments per spec default. *done*
- **T10**: emit two `Assert.NotNull(program);` sites (test #2 line 26, test #3 line 54). *done*
- **T11**: emit one `Assert.True(program.Ops.Count > 0);` site (test #2 line 27); apply `.length` → `.Count` and `.ops` → `.Ops` casing. *done*
- **T12**: emit four C# 11+ raw-string fixtures `var <name> = """…""";` (`source`, `badSource`, `goodSource`, `badSource`), each with closing `"""` at column 0; preserve embedded GLP indentation byte-identically. *done*
- **T13**: emit one inline `"same(f(X, X))."` string literal at the test-#1 `Compile(...)` call. *done*
- **T14**: emit 13 `System.Console.WriteLine("…");` diagnostic statements preserving `\n` escape, `'✅'` U+2705 glyph, and `$"   Generated {program.Ops.Count} instructions"` interpolation. *done*
- **T15**: write the target `.cs` file as UTF-8 (with or without BOM) so the `✅` glyph survives. *done*

## 4. Research Findings

None required. Every construct REUSES an idiom recorded by a prior batch spec, with only three new idiom rows registered as a side-effect (per the convspec's "Notes" section): `rf-dart-expect-throws-exception-to-xunit-assert-throws-exception` (the bare `throwsException` matcher; first-seen in this batch), `rf-dart-expect-length-greaterthan-to-xunit-assert-true` (strict `>` form, sibling of the cached GTE form), and `rf-dart-string-literal-to-csharp-string-literal-quote-swap` (trivial single-quote → double-quote). All three are authoritative-supported on both sides; no escalation. Authoritative sources cited in the convspec (Microsoft Learn for `new` operator, `Xunit.Assert.NotNull`, `Xunit.Assert.Throws<T>`, `List<T>.Count`, raw-string literals, identifier-names, string-literals; xunit.net "Comparisons", "Capturing Output", "Shared Context"; pub.dev `package:matcher` `throwsException`; Dart Language Tour "Errors and exceptions") carry forward verbatim.

## 5. Consistency Pass

- §2.1 `using Xunit;` — derived from convspec construct `dart.package_test.import_directive` (idiom `rf-dart-package-test-import-to-xunit-using`, framework-choice REUSED batch-wide since `test/smoke_test.dart.md` per SC-007).
- §2.2 `using Glp.Runtime.Compiler;` — derived from convspec construct `dart.internal_package_import.glp_runtime_compiler_single_file` (idiom `rf-dart-internal-package-import-to-csharp-using`; namespace owned by `lib/compiler/compiler.dart.md`).
- §2.3 elimination of `main` — derived from convspec construct `dart.package_test.main_entrypoint` (idiom `rf-dart-package-test-main-omit-in-xunit`; empty-main branch — same as `reserved_constant_test.dart.md`).
- §2.4 `SrswTests` class + four `[Fact]` methods — derived from convspec construct `dart.package_test.test_calls_no_outer_group` (idiom `rf-dart-package-test-group-to-xunit-class`; no-outer-group facet, class-name source = file stem per the convspec's cu-4 enumeration).
- §2.5 `var compiler = new GlpCompiler();` — derived from convspec construct `dart.constructor_call.implicit_new_local_var` (idioms `rf-dart-final-local-to-csharp-var-local` + `rf-dart-implicit-new-and-camelcase-to-csharp-explicit-new-pascalcase`, composed).
- §2.6 `Assert.Throws<Exception>` × 3 — derived from convspec construct `dart.package_test.expect_call_throwsException` (NEW idiom `rf-dart-expect-throws-exception-to-xunit-assert-throws-exception`; reason-drop spec default verbatim from the convspec).
- §2.7 `Assert.NotNull(program);` × 2 — derived from convspec construct `dart.package_test.expect_isNotNull` (idiom `rf-dart-expect-isNotNull-to-xunit-assert-notnull`).
- §2.8 `Assert.True(program.Ops.Count > 0);` — derived from convspec construct `dart.package_test.expect_length_greaterThan` (NEW idiom `rf-dart-expect-length-greaterthan-to-xunit-assert-true`, sibling of cached GTE form; composed with `rf-dart-list-length-to-csharp-list-count` + `rf-dart-camelcase-to-csharp-pascalcase`).
- §2.9 four raw-string fixtures `"""…"""` — derived from convspec construct `dart.const_string.triple_quoted_multiline_glp_source_fixture` (idiom `rf-dart-triple-quoted-to-csharp-raw-string`; `final`-not-`const` facet, composed with `rf-dart-final-local-to-csharp-var-local`; closing-delimiter-at-column-0 rule verbatim from the convspec).
- §2.10 `"same(f(X, X))."` — derived from convspec construct `dart.string_literal.single_quoted_glp_source_inline` (NEW idiom `rf-dart-string-literal-to-csharp-string-literal-quote-swap`).
- §2.11 13 `Console.WriteLine` sites — derived from convspec construct `dart.print_statement.diagnostic_log_to_stdout` (idiom `rf-dart-print-and-terminate-to-csharp-equivalent`; stateless-test-class spec default = `Console.WriteLine`; `\n` and `'✅'` U+2705 and `${…}` interpolation handling verbatim from the convspec's nuance section).
- §2.12 method-body composition — derived from the conversion_units enumeration in the convspec (cu-1 through cu-14) plus the inline construct-row method bodies.
- T1–T15 decomposition — derived from the convspec's `conversion_units:` list (cu-1 to cu-14) and the `constructs:` row enumeration. Every task unit maps 1-to-1 onto a convspec construct or conversion_unit.

All decisions fixed — derived verbatim from `.codeconv/conversion-specs/test/srsw_test.dart.md`, the lib spec `lib/compiler/compiler.dart.md` (for the `Glp.Runtime.Compiler` namespace name and `program.Ops` property naming), and the cited Microsoft Learn / xunit.net / pub.dev / Dart Language Tour authoritative pages reused from prior batch specs.

## 6. Escalations

None.
