---
path: test/compiler/reserved_constant_test.dart
cycle_group_id: 118
scc_siblings: []
generated_at: 2026-05-21T17:05:00Z
source_sha256: 28b723fa04ecee639aaec3af6695c57002e00e86f5a4b1fba75776659956e59d
schema_version: 1
---

# Conversion Plan: test/compiler/reserved_constant_test.dart

## 1. Source Analysis

Direct inspection of `glp_runtime_net/test/compiler/reserved_constant_test.dart` (138 lines, sha256 `28b723fa…59d`):

- **File-header banner**: Ten `//` line-comment lines (NOT `///` doc-comments) — path line, blank, three-sentence purpose, three-bullet behaviour list, blank, two `Spec:` cross-references (`docs/typed-glp-manual.md Section 12`, `docs/ma/madGLP-spec.md Section 15`).
- **Imports** (3): `package:test/test.dart`; `package:glp_runtime/compiler/compiler.dart` (for `GlpCompiler`); `package:glp_runtime/compiler/error.dart` (for `CompileError`).
- **File-private top-level helper** (lines 16-19): `/// Helper to compile GLP source` doc-comment + `void compile(String source) { GlpCompiler().compile(source); }` — single statement body, no captured state, fresh `GlpCompiler` per call.
- **`void main()`** (lines 21-138): body contains EXACTLY one `group('Reserved constant validation', () { ... })` call; no pre-`group` statements, no file-IO, no prelude load.
- **Nine `test(...)` cases** inside the single group, all synchronous (no `async`/`await`/`Future`):
  - #1 (lines 23-36) "rejects quoted underscore constant in user mode (default)" → throws, substring `"reserved for system use"`
  - #2 (lines 38-51) "rejects underscore constant in structure in user mode" → throws, substring `"reserved for system use"`
  - #3 (lines 53-62) "allows underscore constant in system mode" → `returnsNormally`
  - #4 (lines 64-73) "allows underscore constant in structure in system mode" → `returnsNormally`
  - #5 (lines 75-82) "allows regular atoms in user mode" → `returnsNormally`
  - #6 (lines 84-91) "allows regular quoted atoms in user mode" → `returnsNormally`
  - #7 (lines 93-108) "rejects -mode with invalid argument" → throws, substring `'Invalid mode'`
  - #8 (lines 110-119) "allows explicit user mode" → `returnsNormally`
  - #9 (lines 121-136) "explicit user mode still rejects underscore constants" → throws, substring `"reserved for system use"`
- **Fixture form**: every test declares `const source = '''…''';` (triple-single-quoted multiline GLP source) with `procedure foo(_).` plus one `foo(...)` clause; fixtures contain embedded single quotes (`'_bar'`, `'_user'`, `'_reserved'`, `'hello world'`) but never triple quotes.
- **Assertion shapes**: FIVE positive sites use `expect(() => compile(source), returnsNormally);` (cases #3, #4, #5, #6, #8); FOUR negative sites use `expect(() => compile(source), throwsA(isA<CompileError>().having((e) => e.message, 'message', contains('<substr>'))));` (cases #1, #2, #7, #9).
- **Absent surface**: no `setUp`/`tearDown`, no `late` field, no `dart:io`, no `dart:async`, no `Future`/`Stream`/`Completer`/`Timer`/isolate, no `mixin`/`extension`, no generics, no sealed/abstract test types, no bitwise/shift, no null-safety surface beyond default.

## 2. Dart → C#/.NET Conversion Plan

Each Dart construct → its C#/.NET target, mirroring the ratified convspec construct rows.

### 2.1 File-header `//` line-comment block (convspec construct `dart.doc_comment.file_header_double_slash_block`)

Map the ten-line `//` banner verbatim to a C# `//` block at the top of `ReservedConstantTest.cs`, ABOVE the `using` directives. NOT `///` — stays a banner, does NOT retag to XML `<summary>`. First line updated: `// test/compiler/ReservedConstantTest.cs` (PascalCased target filename). The two `Spec:` cross-references carry verbatim — the docs are repo-shared.

### 2.2 `import 'package:test/test.dart';` (convspec construct `dart.package_test.import_directive`)

Drop the import; emit `using Xunit;` at file scope. REUSE batch-wide framework choice `rf-dart-package-test-import-to-xunit-using` (pinned by `test/smoke_test.dart.md`). xUnit creates a fresh test-class instance per `[Fact]` — irrelevant here (no shared state). `.csproj` emission is langpair-level, OUT OF SCOPE per-file.

### 2.3 `import 'package:glp_runtime/compiler/compiler.dart';` + `import 'package:glp_runtime/compiler/error.dart';` (convspec construct `dart.internal_package_import.glp_runtime_compiler_two_files`)

Collapse to ONE C# `using Glp.Runtime.Compiler;` directive — both files live in `lib/compiler/` and the langpair folds directory-grained namespaces (per `lib/compiler/compiler.dart.md` + `lib/compiler/error.dart.md`). No `as` alias, no `show` filter on the Dart side → no C# alias/filter needed. Symbols `GlpCompiler` and `CompileError` are library-public → C# `public`. REUSE `rf-dart-internal-package-import-to-csharp-using`.

### 2.4 Top-level `void compile(String source)` helper (convspec construct `dart.toplevel.file_private_void_helper_function_calling_compiler`)

Lift to a `private static void Compile(string source)` method on the test class `ReservedConstantValidationTests`. Static (not instance) because the helper closes over NOTHING — every call constructs a fresh `GlpCompiler`; this is strictly tighter than instance scope and matches the absence of any test-class state (contrast `partial_evaluator_test.dart.md`'s `RunPE` which was instance because it closed over `_pe`). Method name PascalCases: `compile` → `Compile`. The attached `/// Helper to compile GLP source` Dart doc-comment lifts to a C# `/// <summary>Helper to compile GLP source</summary>` XML-doc on the static method (Microsoft Learn "Documentation comments"). REUSE `csharp-static-class-no-toplevel-members`. No `Compile` collision: `System.Object` has no `Compile`; no `[Fact]` method is named `Compile`.

### 2.5 `GlpCompiler().compile(source)` (convspec construct `dart.constructor_call.implicit_new_then_method_chain`)

Map to `new GlpCompiler().Compile(source);` — Dart implicit-new → C# explicit `new` (REQUIRED, Microsoft Learn `new` operator); Dart `compile` (camelCase) → C# `Compile` (PascalCase, Microsoft C# Identifier Names guide); class name `GlpCompiler` already PascalCase by Dart class-name convention. Used as a statement (return value ignored — the side-effect is the throw on validation failure); C# accepts identically. REUSE `rf-dart-implicit-new-and-camelcase-to-csharp-explicit-new-pascalcase`.

### 2.6 `void main() { group(...); }` (convspec construct `dart.package_test.main_entrypoint`)

Eliminate entirely. xUnit discovers `[Fact]` methods by reflection — no per-file entrypoint. Since `main`'s body contains ONLY one `group(...)` call and NO pre-`group` side-effects, NO `static` constructor on the test class is needed — lossless omission. REUSE `rf-dart-package-test-main-omit-in-xunit`.

### 2.7 `group('Reserved constant validation', () { … nine tests … })` (convspec construct `dart.package_test.group_block`)

Lift to ONE xUnit test class `ReservedConstantValidationTests` (PascalCased + `Tests` suffix, spaces dropped). Nine inner `test(label, () { ... })` calls lift to nine `[Fact(DisplayName = "<original label>")]`-attributed `public void` methods. NO constructor, NO `late` field — `private static Compile` is the only non-`[Fact]` member. `DisplayName` preserves the original Dart label verbatim (including `-mode` hyphen and `(default)` parenthetical). REUSE `rf-dart-package-test-group-to-xunit-class`. Suggested method-name PascalCase sanitisation (codegen may apply equivalent):

| Dart label | C# method name |
| --- | --- |
| `rejects quoted underscore constant in user mode (default)` | `RejectsQuotedUnderscoreConstantInUserModeDefault` |
| `rejects underscore constant in structure in user mode` | `RejectsUnderscoreConstantInStructureInUserMode` |
| `allows underscore constant in system mode` | `AllowsUnderscoreConstantInSystemMode` |
| `allows underscore constant in structure in system mode` | `AllowsUnderscoreConstantInStructureInSystemMode` |
| `allows regular atoms in user mode` | `AllowsRegularAtomsInUserMode` |
| `allows regular quoted atoms in user mode` | `AllowsRegularQuotedAtomsInUserMode` |
| `rejects -mode with invalid argument` | `RejectsModeWithInvalidArgument` |
| `allows explicit user mode` | `AllowsExplicitUserMode` |
| `explicit user mode still rejects underscore constants` | `ExplicitUserModeStillRejectsUnderscoreConstants` |

### 2.8 `const source = '''…''';` (convspec construct `dart.const_string.triple_quoted_multiline_glp_source_fixture`)

Each of the nine triple-single-quoted Dart fixtures → C# 11+ raw-string literal `const string source = """ … """;` (Microsoft Learn "Raw string literals"). Closing `"""` delimiter MUST be emitted at column 0 to preserve fixture indentation byte-identically (raw-string common-prefix-strip rule — load-bearing for the GLP lexer). None of the fixtures contains `"""`, so no delimiter-bumping (to `""""`) needed; embedded single quotes (`'_bar'`, `'_user'`, `'_reserved'`, etc.) need no escape on the C# raw-string side. Dart `const` → C# `const string` (raw-string literals are compile-time constants per Microsoft Learn). REUSE `rf-dart-triple-quoted-to-csharp-raw-string`.

### 2.9 `expect(() => compile(source), returnsNormally);` — FIVE sites (convspec construct `dart.package_test.expect_function_returnsNormally`)

Emit a BARE call `Compile(source);` on its own line in the C# `[Fact]` body. xUnit deliberately omits `Assert.DoesNotThrow` (xUnit FAQ + issue tracker — "if the code shouldn't throw, just call it"); the runner converts any uncaught exception into a failed test with full stack trace. Lambda wrapper `() => …` is DROPPED entirely (nothing to execute lazily once the assertion is gone). Five sites: cases #3, #4, #5, #6, #8. REUSE `rf-dart-expect-returns-normally-to-xunit-bare-call`.

### 2.10 `expect(() => compile(source), throwsA(isA<CompileError>().having((e) => e.message, 'message', contains('<substr>'))));` — FOUR sites (convspec construct `dart.package_test.expect_throwsA_isA_compileerror_having_message_contains`)

Two-statement Throws-then-Assert pattern:

```
var ex = Assert.Throws<CompileError>(() => Compile(source));
Assert.Contains("<substr>", ex.Message);
```

Microsoft Learn `Xunit.Assert.Throws<T>` + `Xunit.Assert.Contains(string, string)`. Dart `e.message` (camelCase) → C# `ex.Message` (PascalCase, inherited from `System.Exception.Message`). Lambda maps 1-to-1: `() => compile(source)` → `() => Compile(source)`.

Subtype-tolerance caveat: Dart `isA<T>` matches T and subtypes; C# `Assert.Throws<T>` is EXACT-type — if `lib/compiler/error.dart.md`'s converted `CompileError` has registered subclasses at emit time, codegen MUST switch to `Assert.ThrowsAny<CompileError>` (subtype-tolerant). Spec default per the convspec: emit `Assert.Throws<CompileError>` UNLESS subclasses are registered. Four sites with substrings:

| Case | Dart substring | C# `Assert.Contains` arg |
| --- | --- | --- |
| #1 | `"reserved for system use"` | `"reserved for system use"` |
| #2 | `"reserved for system use"` | `"reserved for system use"` |
| #7 | `'Invalid mode'` (single-quoted) | `"Invalid mode"` (double-quoted — C# strings always double-quoted) |
| #9 | `"reserved for system use"` | `"reserved for system use"` |

REUSE `rf-dart-throwsa-isa-having-to-xunit-throws-plus-assert`.

### 2.11 Namespace declaration (convspec conversion_units cu-3)

Emit `namespace <RootNs>.Test.Compiler;` (file-scoped namespace) mirroring the Dart `test/compiler/` path. `<RootNs>` resolved by the langpair at emit time.

## 3. Decomposed Task Units

- **T1** — Emit file-header `//` line-comment block verbatim at top of `ReservedConstantTest.cs` with first line updated to `// test/compiler/ReservedConstantTest.cs`; preserve all `Spec:` references. (§2.1)
- **T2** — Emit consolidated using directives `using Xunit;` and `using Glp.Runtime.Compiler;` (two Dart imports `compiler.dart`+`error.dart` collapse to one via same-namespace folding). (§2.2, §2.3)
- **T3** — Emit file-scoped namespace declaration `namespace <RootNs>.Test.Compiler;`. (§2.11)
- **T4** — Emit test class declaration `public class ReservedConstantValidationTests` (from outer group label `'Reserved constant validation'`); NO constructor, NO `late` field. (§2.7)
- **T5** — Emit `private static void Compile(string source)` helper method with attached `/// <summary>Helper to compile GLP source</summary>` XML-doc; body `new GlpCompiler().Compile(source);`. (§2.4, §2.5)
- **T6** — Emit four `[Fact(DisplayName = "<verbatim Dart label>")]` Throws-then-Assert.Contains methods (cases #1, #2, #7, #9) with raw-string `const string source = """…""";` fixture (closing delimiter at column 0) + `var ex = Assert.Throws<CompileError>(() => Compile(source)); Assert.Contains("<substr>", ex.Message);` body. (§2.7, §2.8, §2.10)
- **T7** — Emit five `[Fact(DisplayName = "<verbatim Dart label>")]` bare-call methods (cases #3, #4, #5, #6, #8) with raw-string `const string source = """…""";` fixture + bare `Compile(source);` body. (§2.7, §2.8, §2.9)
- **T8** — Codegen guard at emit time: if `lib/compiler/error.dart.md`'s converted `CompileError` has registered subclasses, switch the four Throws methods (T6) to `Assert.ThrowsAny<CompileError>` (subtype-tolerant); default `Assert.Throws<CompileError>` (exact-type). (§2.10 caveat)
- **T9** — Codegen guard at emit time: place every raw-string closing `"""` delimiter at column 0 (common-prefix-strip rule — load-bearing for byte-identical GLP fixtures). (§2.8 nuance)
- **T10** — Drop Dart `void main()` entirely (no static-constructor hoist needed — empty `main` body apart from one `group(...)`). (§2.6)

## 4. Research Findings

None required — every construct is verbatim-derivable from the ratified convspec, which records cached idioms registered by prior batch specs (smoke_test, partial_evaluator_test, boot_loader_test, project_linker_test, moded_head_test, glp_runtime_test, cssg_modules_test, heap/*, module/*, analysis/type_checker/*) and the lib specs `lib/compiler/compiler.dart.md` + `lib/compiler/error.dart.md`. All authoritative sources (Microsoft Learn `new` operator, Identifier Names, Documentation comments, raw-string literals, `Xunit.Assert.Throws<T>`, `Xunit.Assert.Contains`, `System.Exception.Message`, "Comments"; xUnit FAQ / shared-context / how-it-works / issue #2073; Dart Language Tour strings/comments; `package:matcher` `throwsA`/`isA`/`having`/`contains`/`returnsNormally`; pub.dev/test) carry forward verbatim per FR-012 / SC-007.

## 5. Consistency Pass

Fixed — derived from `.codeconv/conversion-specs/test/compiler/reserved_constant_test.dart.md` (RATIFIED) and its cited cached idioms. Every construct row in §2 mirrors a convspec `constructs:` entry (10 rows total: file-header banner, package:test import, two-internal-package-import collapse, top-level helper lift, implicit-new + camelCase method chain, main entrypoint omission, group→test-class, triple-quoted→raw-string, `returnsNormally`→bare call, `throwsA(isA<T>.having(...))`→Throws+Contains). Conversion-unit set §3 (T1-T10) covers the convspec `conversion_units:` cu-1 through cu-10 with the same partitioning (4-throws / 5-bare-call split matches convspec cu-7 / cu-8 exactly). Subtype-tolerance guard (T8) and column-0 raw-string-delimiter guard (T9) preserve the load-bearing nuances flagged in the convspec rows. Method-name PascalCase sanitisation table in §2.7 matches the suggested-names list in the convspec `dart.package_test.group_block` nuance.

## 6. Escalations

None.
