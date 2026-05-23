---
path: test/test_constant_compile.dart
cycle_group_id: 162
scc_siblings: []
generated_at: 2026-05-21T16:45:06Z
source_sha256: 4bcbea2e88df85d7e670dc297e7fa64a3241e200f07da360f84f36715d19aca6
schema_version: 1
---

# Conversion Plan: test/test_constant_compile.dart

## 1. Source Analysis

The source file `glp_runtime_net/test/test_constant_compile.dart` is a 12-line standalone Dart diagnostic harness (NOT a `package:test` test). Direct inspection of the `.dart` source confirms:

- **Line 1**: single internal-package import — `import 'package:glp_runtime/compiler/compiler.dart';`. NO `import 'package:test/test.dart';`, NO `import 'dart:async';`, NO `import 'dart:io';`. The sole imported symbol used in the file is `GlpCompiler` (constructor) plus the implicit `BytecodeProgram` return-type of `GlpCompiler.compile(String)`.
- **Line 3**: top-level `void main() { ... }` — entrypoint per Dart's `dart run` model.
- **Line 4**: `final compiler = GlpCompiler();` — write-once local, implicit-new constructor call with no type annotation.
- **Line 6**: `print('=== Testing: test_nil([]) ===');` — plain single-quoted string literal, no interpolation.
- **Line 7**: `final result = compiler.compile('test_nil([]).');` — write-once local, instance-method call `compile` (camelCase) on `GlpCompiler` with a single hardcoded Dart string literal `'test_nil([]).'`.
- **Line 8**: `print('Bytecode:');` — plain single-quoted string literal, no interpolation.
- **Line 9**: `for (int i = 0; i < result.ops.length; i++) {` — Dart C-style for-loop iterating `result.ops` by index. `result.ops.length` chains the public field `ops` (a `List<Op>`-shaped sequence per the SUT spec `lib/compiler/result.dart.md` / `lib/bytecode/runner.dart.md`) and the list's `.length` getter.
- **Line 10**: `print('  $i: ${result.ops[i]}');` — interpolated string with TWO slots: `$i` (bare-identifier shorthand) and `${result.ops[i]}` (full-brace expression with property access + list indexer). Leading two-space whitespace + `: ` separator preserve verbatim.
- **Lines 11, 12**: closing braces for the for-loop and `main`.

The file has NO assertions (`expect(...)` / matchers / `throwsA`), NO test registration (`test(...)` / `group(...)` / `setUp` / `tearDown`), NO async surface (no `Future`, no `Stream`, no `async`/`await`, no `Completer`), NO error handling (no `try`/`catch`/`on`), NO `late` field, NO mixin, NO null-safety nuance beyond default NNBD, NO collection literal beyond the embedded `[]` inside the GLP-source string literal (which is opaque to Dart — pure text passed to `GlpCompiler.compile`). Host shape on the Dart side: `dart run <file>` diagnostic script, invoked manually by a developer to print a bytecode trace for `test_nil([])` for human inspection. NOT discovered by `dart test`.

## 2. Dart → C#/.NET Conversion Plan

Each Dart construct → C#/.NET target, mirroring the ratified convspec.

### C-1. Internal-package import → C# `using` directive
- **Dart**: `import 'package:glp_runtime/compiler/compiler.dart';`
- **C#**: drop the Dart `import` directive; emit ONE file-level `using <RootNs>.Compiler;` directive. The converted `GlpCompiler` facade and the `BytecodeProgram` (returned by `GlpCompiler.Compile(string)`) both live in the `<RootNs>.Compiler` sub-namespace per the lib spec `lib/compiler/compiler.dart.md`. NO additional `using <RootNs>.Bytecode;` required at this file's scope — `BytecodeProgram` is reachable via the compiler-namespace re-export per the lib spec's decision. NO `as` alias / `show` filter (Dart side has none). NO `using static` (the only `GlpCompiler` reference is qualified at its constructor call site). Idiom: `rf-dart-internal-package-import-to-csharp-using`. KB cache hit per FR-012 / SC-007 (REUSE from `test/debug_negative.dart.md`, `test/bytecode/inspect_bytecode_test.dart.md`, `test/compiler/reserved_constant_test.dart.md`).

### C-2. Diagnostic-script `void main()` → C# `static int Main(string[] args)` console-exe entrypoint
- **Dart**: top-level `void main() { ... }` with no `package:test` import and no `test(...)`/`expect(...)`/`group(...)` calls.
- **C#**: `public static class TestConstantCompile { public static int Main(string[] args) { ... return 0; } }` — host class name PascalCased from the filename (`test_constant_compile.dart` → `TestConstantCompile`), identical-shape to the `DebugNegative` host from `test/debug_negative.dart.md`. NOT a `[Fact]`, NOT a test class, NO xUnit attributes, NO test-method visibility. The `string[] args` parameter is required by the C# `Main` signature but is unused. `return 0;` at the end so the diagnostic exit code is explicit. The top-level-statements alternative (C# 9+, Microsoft Learn "Top-level statements" `https://learn.microsoft.com/dotnet/csharp/fundamentals/program-structure/top-level-statements`) is recorded as a langpair-preference alternative; default is the classic `static class` + `static int Main` shape for symmetry with `debug_negative.dart.md`. Idiom: `rf-dart-debug-script-main-to-csharp-static-main`. KB cache hit (REUSE from `test/debug_negative.dart.md`).

### C-3. `final` local with implicit-new constructor call → C# `var` with explicit `new`
- **Dart**: `final compiler = GlpCompiler();`
- **C#**: `var compiler = new GlpCompiler();`
- Mutability annotation is LOST in conversion (C# has no first-class `let`/`readonly`-local keyword; `readonly` is field-only, `in` is parameter-only). Observably equivalent because the local is not reassigned. The `new` keyword is REQUIRED in C# (Microsoft Learn `https://learn.microsoft.com/dotnet/csharp/language-reference/operators/new-operator`). Target-typed `new()` is an authoritative alternative when the target type is known from context; the spec records the explicit `new GlpCompiler()` form as the default. Inferred local type: `GlpCompiler` (reference type per `lib/compiler/compiler.dart.md`). Idioms: `rf-dart-final-local-to-csharp-var-local`, `rf-dart-implicit-new-to-csharp-explicit-or-targettyped-new`. KB cache hits (REUSE from `test/analysis/type_checker/well_typed_clause_test.dart.md`, `test/debug_negative.dart.md`).

### C-4. `final` local with instance-method call → C# `var` with PascalCased method
- **Dart**: `final result = compiler.compile('test_nil([]).');`
- **C#**: `var result = compiler.Compile("test_nil([]).");`
- Dart camelCased instance method `compile` → C# PascalCased `Compile` per Microsoft's C# Identifier-Names guide (`https://learn.microsoft.com/dotnet/csharp/fundamentals/coding-style/identifier-names`). Dart single-quoted string `'test_nil([]).'` → C# double-quoted string `"test_nil([])."` (C# strings use ONLY double quotes; single-quotes are `char` literals). Embedded parentheses/brackets/period require no escaping in either language. Inferred local type: `BytecodeProgram` (reference type per `lib/compiler/result.dart.md`). Idioms: `rf-dart-final-local-to-csharp-var-local`, `rf-dart-instance-method-camelcase-to-csharp-pascalcase`, `rf-dart-single-quoted-string-to-csharp-double-quoted-string`. KB cache hits (REUSE across the batch — `lib/compiler/compiler.dart.md`, `test/compiler/reserved_constant_test.dart.md`, `test/debug_negative.dart.md`).

### C-5. Plain `print(...)` → `Console.WriteLine(string)`
- **Dart**: `print('=== Testing: test_nil([]) ===');` and `print('Bytecode:');`
- **C#**: `Console.WriteLine("=== Testing: test_nil([]) ===");` and `Console.WriteLine("Bytecode:");`
- Host shape is `static Main` (NOT `[Fact]`), so route to `Console.WriteLine` (Microsoft Learn `https://learn.microsoft.com/dotnet/api/system.console.writeline`) — NOT `ITestOutputHelper.WriteLine`. NO `using Xunit.Abstractions;`, NO constructor injection. The `using System;` directive supplies `Console`. Trailing-newline semantics identical between Dart `print` and C# `Console.WriteLine` (both append a newline). Idiom: `rf-dart-print-in-console-exe-to-console-writeline`. KB cache hit (REUSE from `test/debug_negative.dart.md`).

### C-6. Interpolated `print(...)` → `Console.WriteLine($"...")` with PascalCased property + indexer
- **Dart**: `print('  $i: ${result.ops[i]}');`
- **C#**: `Console.WriteLine($"  {i}: {result.Ops[i]}");`
- C# REQUIRES the `$` prefix BEFORE the literal opener (Microsoft Learn "$ — string interpolation" `https://learn.microsoft.com/dotnet/csharp/language-reference/tokens/interpolated`). Dart `$i` (bare-identifier shorthand) → C# `{i}` (curly braces mandatory; C# has no shorthand). Dart `${result.ops[i]}` (full-brace expression) → C# `{result.Ops[i]}` — `Ops` PascalCased per `rf-dart-instance-field-camelcase-to-csharp-property-pascalcase`; list indexer `[i]` byte-identical (Microsoft Learn `https://learn.microsoft.com/dotnet/api/system.collections.generic.list-1.item`). Leading two-space whitespace and `: ` separator preserve verbatim. No `"` or `\` characters and no embedded newlines, so NO verbatim (`$@"..."`) or raw (`$"""..."""`) interpolated form needed. Format-provider nuance: C# `$"..."` uses CURRENT CULTURE's `IFormatProvider` for `IFormattable` arguments by default — for `int i`, culture-invariant; for `Op` element, depends on SUT-side `Op.ToString()` (downstream consistency gate — see §5). No format specifier (Dart interpolation has none). Idioms: `rf-dart-string-interpolation-to-csharp-interpolated-string`, `rf-dart-list-indexer-to-csharp-list-indexer`, `rf-dart-instance-field-camelcase-to-csharp-property-pascalcase`. KB cache hits (REUSE from `test/debug_negative.dart.md`, `test/bytecode/inspect_bytecode_test.dart.md`).

### C-7. C-style for-loop over `<list>.length` → C# `for` over `<list>.Count` / `.Length`
- **Dart**: `for (int i = 0; i < result.ops.length; i++) { ... }`
- **C#**: `for (int i = 0; i < result.Ops.Count; i++) { ... }` (or `.Length` if the SUT property is array-backed — owned by `lib/compiler/result.dart.md` / `lib/bytecode/runner.dart.md`).
- Loop header byte-identical between Dart and C# (Microsoft Learn `https://learn.microsoft.com/dotnet/csharp/language-reference/statements/iteration-statements#the-for-statement`). PascalCases BOTH `.ops` → `.Ops` (field) AND `.length` → `.Count`/`.Length` (getter). Bounds-evaluation: both languages re-evaluate the condition each iteration; loop body does not mutate `result.Ops` so the JIT may hoist invariant property-loads. `foreach` alternative rejected: would require LINQ `Select((op, i) => (op, i))` to preserve `int i` shape, adding idiom drift. Iteration-variable scope identical (loop-statement scope on both sides). Idiom: `rf-dart-c-style-for-loop-to-csharp-verbatim`. KB cache hit (REUSE from `test/bytecode/inspect_bytecode_test.dart.md`).

### Final C# file shape (target: `test/TestConstantCompile.cs`)

- file-scope `using` directives: `using System;`, `using <RootNs>.Compiler;`. NO `using Xunit;`, NO `using Xunit.Abstractions;`, NO `using System.Linq;`.
- namespace declaration: `namespace <RootNs>.Test;` (single top-level namespace; mirrors `test/test_constant_compile.dart`'s position in the test-root).
- host class: `public static class TestConstantCompile` — NOT a test class, NO xUnit attributes, NO public test-method visibility, NO constructor.
- entrypoint: `public static int Main(string[] args)` — body translated statement-for-statement:
  1. `var compiler = new GlpCompiler();`
  2. `Console.WriteLine("=== Testing: test_nil([]) ===");`
  3. `var result = compiler.Compile("test_nil([]).");`
  4. `Console.WriteLine("Bytecode:");`
  5. `for (int i = 0; i < result.Ops.Count; i++) { Console.WriteLine($"  {i}: {result.Ops[i]}"); }`
  6. `return 0;`
- NO `[Fact]`, `[Trait]`, `[DisplayName]`, `ITestOutputHelper`, `setUp`, `tearDown`, no async/`Task`, no `try`/`catch`, no `late`-equivalent. .csproj orchestration (compile this as a separate diagnostic exe vs. auxiliary entrypoint vs. `[Fact(Skip = "manual diagnostic")]` no-op) is a LANGPAIR-level concern recorded in cu-9 (alternative-host-shape note) but not asserted here.

## 3. Decomposed Task Units

- T1: emit file-scope `using System;` directive. — done
- T2: emit file-scope `using <RootNs>.Compiler;` directive (replaces Dart `import 'package:glp_runtime/compiler/compiler.dart';`). — done
- T3: emit `namespace <RootNs>.Test;` declaration mirroring `test/` position. — done
- T4: emit `public static class TestConstantCompile` host class (PascalCased from filename), NO xUnit attributes. — done
- T5: emit `public static int Main(string[] args)` entrypoint hoisted from Dart top-level `void main`. — done
- T6: emit `var compiler = new GlpCompiler();` (Dart `final` → C# `var`, implicit-new → explicit `new`). — done
- T7: emit `Console.WriteLine("=== Testing: test_nil([]) ===");` (plain `print` → `Console.WriteLine`, single-quote → double-quote). — done
- T8: emit `var result = compiler.Compile("test_nil([]).");` (`final` → `var`, camelCased `compile` → PascalCased `Compile`, single-quote → double-quote). — done
- T9: emit `Console.WriteLine("Bytecode:");` (plain `print` → `Console.WriteLine`). — done
- T10: emit C-style `for (int i = 0; i < result.Ops.Count; i++) { ... }` loop header (`.ops.length` → `.Ops.Count`, PascalCased on both segments). — done
- T11: emit interpolated `Console.WriteLine($"  {i}: {result.Ops[i]}");` loop body (Dart `$i` → C# `{i}`; Dart `${result.ops[i]}` → C# `{result.Ops[i]}` with PascalCased `Ops`; `$` prefix on literal mandatory). — done
- T12: emit `return 0;` before the closing brace of `Main` (explicit exit code; consistent with `debug_negative.dart.md`). — done

## 4. Research Findings

none required (every construct is grounded in a CACHED idiom recorded by prior-batch convspecs; the convspec for this file is RATIFIED with zero escalations and exhaustively cites cached idiom IDs — `rf-dart-internal-package-import-to-csharp-using`, `rf-dart-debug-script-main-to-csharp-static-main`, `rf-dart-print-in-console-exe-to-console-writeline`, `rf-dart-c-style-for-loop-to-csharp-verbatim`, `rf-dart-list-indexer-to-csharp-list-indexer`, `rf-dart-final-local-to-csharp-var-local`, `rf-dart-implicit-new-to-csharp-explicit-or-targettyped-new`, `rf-dart-instance-method-camelcase-to-csharp-pascalcase`, `rf-dart-instance-field-camelcase-to-csharp-property-pascalcase`, `rf-dart-string-interpolation-to-csharp-interpolated-string`, `rf-dart-single-quoted-string-to-csharp-double-quoted-string` — all REUSED verbatim from `test/debug_negative.dart.md`, `test/bytecode/inspect_bytecode_test.dart.md`, `test/compiler/reserved_constant_test.dart.md`, `test/analysis/type_checker/well_typed_clause_test.dart.md`, and the lib `lib/compiler/*.dart.md` / `lib/bytecode/runner.dart.md` specs).

## 5. Consistency Pass

fixed — derived from `.codeconv/conversion-specs/test/test_constant_compile.dart.md` (RATIFIED convspec). Every per-construct decision in §2 mirrors the convspec's `constructs:` block verbatim and inherits the convspec's downstream-only gates:

- C-1 mirrors convspec `dart.internal_package_import.same_package_single` — single `using <RootNs>.Compiler;` directive.
- C-2 mirrors convspec `dart.diag_script.void_main_no_package_test_no_assertions` — `public static class TestConstantCompile` + `public static int Main(string[] args)` console-exe entrypoint (NOT xUnit `[Fact]`).
- C-3 mirrors convspec `dart.final_local_immutable_with_implicit_new` for the `compiler` local — `var compiler = new GlpCompiler();`.
- C-4 mirrors convspec `dart.final_local_immutable_with_implicit_new` for the `result` local — `var result = compiler.Compile("test_nil([]).");`.
- C-5 mirrors convspec `dart.core.print` for the two plain `print` calls — `Console.WriteLine` (NOT `ITestOutputHelper.WriteLine`).
- C-6 mirrors convspec `dart.core.print` and `dart.string_interpolation_with_list_indexer` for the interpolated `print` — `Console.WriteLine($"  {i}: {result.Ops[i]}");`.
- C-7 mirrors convspec `dart.c_style_for_loop_over_list_length_with_indexer` — `for (int i = 0; i < result.Ops.Count; i++) { ... }`.

Downstream consistency gate (recorded by convspec cu-8 — NOT a new escalation): the diagnostic output's faithfulness depends on the SUT-side `Op.ToString()` override being consistent with the Dart-side `Op.toString()` override. The SUT spec `lib/bytecode/runner.dart.md` owns that decision; codegen MUST consult it at emit time, but the call-site shape `{result.Ops[i]}` is fully determined here. The PascalCasing of `result.ops.length` → `result.Ops.Count` (vs `.Length` for arrays) is owned by the SUT lib specs `lib/compiler/result.dart.md` / `lib/bytecode/runner.dart.md` — this plan records only the call-site shape. The alternative-host-shape decision (classic `static class` + `static int Main` vs C# 9+ top-level statements) is owned by langpair preference (convspec cu-9); the plan emits the classic shape as the default, matching `debug_negative.dart.md`.

## 6. Escalations

None.
