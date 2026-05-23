---
path: test/debug_negative.dart
cycle_group_id: 122
scc_siblings: []
generated_at: 2026-05-21T16:30:04Z
source_sha256: fda6e94ebaad1f79d40ea453ff0b6a856c963a9eef87b1122c1c238126874594
schema_version: 1
---

# Conversion Plan: test/debug_negative.dart

## 1. Source Analysis

Direct inspection of `glp_runtime_net/test/debug_negative.dart` (73 lines, sha256 above) confirms the following:

- Four `package:glp_runtime/...` imports:
  - `package:glp_runtime/analysis/type_checker/type_checker.dart`
  - `package:glp_runtime/analysis/type_checker/type_parser.dart` (SUT file MISSING from converted inventory — recorded as downstream gate, not an escalation)
  - `package:glp_runtime/compiler/lexer.dart`
  - `package:glp_runtime/compiler/parser.dart`
- NO `import 'package:test/test.dart';`, NO `test(...)`, NO `expect(...)`, NO `group(...)`, NO matchers. This file is a `dart run`-invoked diagnostic harness, NOT a `package:test` fixture.
- One top-level helper: `TypeCheckResult checkTypes(String source)` — splits the source on `\n`, filters out lines containing `::=` or starting with `procedure ` or `%`, joins the remainder, lexes, parses, parses the type env, and runs `TypeChecker.check`.
- One top-level entrypoint: `void main()` — prints a banner, calls `checkTypes` with a triple-quoted negative-case GLP source, prints `isWellTyped`, branches on the boolean to print a BUG/CORRECT message, prints any errors, then repeats for the positive case.
- Local declarations used: `final lines, clauseLines, trimmed, clauseSource, lexer, tokens, parser, program, clauses, typeEnv, checker` (all `final`), `var result` (re-assigned once between negative and positive cases).
- Two Dart triple-quoted `'''...'''` string literals containing GLP grammar fragments with `\\` escapes (e.g. `A?\\B`, `B?\\C`).
- One `for (final err in result.errors) { print('  - $err'); }` loop (used twice).
- One string-interpolation shorthand: `'  - $err'`. One expression-form interpolation: `'isWellTyped: ${result.isWellTyped}'`.
- Member calls observed: `String.split`, `String.trim`, `String.contains`, `String.startsWith`, `String.isNotEmpty`, `List<String>.add`, `Iterable<String>.join`, `Iterable<Procedure>.expand`, `Iterable<Clause>.toList`, `List<Error>.isNotEmpty`, plus default-constructor calls `Lexer(...)`, `Parser(...)`, `TypeChecker(...)`.

This inspection matches the convspec constructs verbatim.

## 2. Dart → C#/.NET Conversion Plan

Each Dart construct mirrored from the ratified convspec at `.codeconv/conversion-specs/test/debug_negative.dart.md`.

- **dart.package_under_test.import_directive** → file-scope C# `using` directives. The two `analysis/type_checker/<file>.dart` imports collapse to ONE `using <RootNs>.Analysis.TypeChecker;`; the two `compiler/<file>.dart` imports collapse to ONE `using <RootNs>.Compiler;`. Idiom: `rf-dart-internal-package-import-to-csharp-using`. Missing-SUT-file (`type_parser.dart`) is a downstream gate (see cu-8), not a per-construct escalation; the call-site shape `TypeParser.ParseTypes(source)` is fully determined by the existing top-level-function idiom.
- **dart.top_level_function_helper_with_return_type** (`TypeCheckResult checkTypes(String source)`) → `private static TypeCheckResult CheckTypes(string source)` member on the file's single host class `DebugNegative`. Body translated statement-for-statement using cached idioms: `final` → `var`, `<String>[]` → `new List<string>()`, `String.split('\n')` → `source.Split('\n')`, `String.trim()` → `Trim()`, `String.contains(s)` → `Contains(s)`, `String.startsWith(s)` → `StartsWith(s)`, `String.isNotEmpty` → `Length > 0`, `List<T>.add(x)` → `Add(x)`, `Iterable<String>.join('\n')` → `string.Join("\n", clauseLines)` (STATIC method, argument-order swap), `Lexer(...)`/`Parser(...)`/`TypeChecker(...)` → `new Lexer(...)`/`new Parser(...)`/`new TypeChecker(...)`, `lexer.tokenize()` → `lexer.Tokenize()`, `parser.parse()` → `parser.Parse()`, `program.procedures.expand((p) => p.clauses).toList()` → `program.Procedures.SelectMany(p => p.Clauses).ToList()`, `parseTypes(source)` → `TypeParser.ParseTypes(source)`, `checker.check(clauses)` → `checker.Check(clauses)`. Idiom: `rf-dart-top-level-function-to-csharp-static-class-method`.
- **dart.string_and_iterable_member_calls** → per-member mapping per convspec (see cached idiom rows enumerated above). The argument-order swap on `Iterable.join` is preserved as `string.Join(separator, collection)`. `using System.Linq;` is required at file scope for `SelectMany`/`ToList`. Idiom: `rf-dart-string-and-iterable-members-to-dotnet`.
- **dart.constructor_call_implicit_new** → `new Lexer(clauseSource)`, `new Parser(tokens)`, `new TypeChecker(typeEnv)`. Positional-argument shape preserved 1:1; SUT-side constructor signatures are decided by the peer convspecs. Idiom: `rf-dart-implicit-new-to-csharp-explicit-or-targettyped-new`.
- **dart.test_file.void_main_as_dart_run_entrypoint** → `public static int Main(string[] args)` on the host class `public static class DebugNegative`. NO `[Fact]`, NO xUnit attributes, NO test-class instance. Method returns `0` at end to make the exit code explicit. The convspec records that .csproj orchestration (test-exe vs separate diagnostic-exe vs `[Fact(Skip = "manual diagnostic")]`) is a langpair-level concern, not asserted here. Idiom: `rf-dart-debug-script-main-to-csharp-static-main`.
- **dart.core.print** → `Console.WriteLine(...)` (with `using System;`). `print('')` → `Console.WriteLine()` (the empty-args overload). NO `ITestOutputHelper` injection — this file is a console-exe host, not a `[Fact]` host. Idiom: `rf-dart-print-in-console-exe-to-console-writeline`.
- **dart.string_interpolation** → C# interpolated-string literal `$"..."`. `'isWellTyped: ${result.isWellTyped}'` → `$"isWellTyped: {result.IsWellTyped}"`. `'  - $err'` → `$"  - {err}"` (Dart bare-identifier shorthand `$name` expands to C# `{name}` — C# has no shorthand form). PascalCasing of `IsWellTyped`/`Errors` is decided by the peer SUT convspec at `.codeconv/conversion-specs/lib/analysis/type_checker/type_checker.dart.md`. Idiom: `rf-dart-string-interpolation-to-csharp-interpolated-string`.
- **dart.if_else_statement** → C# `if (cond) { ... } else { ... }` 1:1. Boolean condition surface: `result.isWellTyped` → `result.IsWellTyped`; `result.errors.isNotEmpty` → `result.Errors.Count > 0`; `!result.isWellTyped` → `!result.IsWellTyped`. Idiom: `rf-dart-if-else-to-csharp-if-else`.
- **dart.for_in_loop_over_list** → `foreach (var err in result.Errors) { Console.WriteLine($"  - {err}"); }`. C# `foreach` loop variable is a fresh per-iteration binding (matching Dart `final` semantics observably). Idiom: `rf-dart-for-in-to-csharp-foreach`.
- **dart.local_var_with_reassignment** → `var result = CheckTypes(...);` followed by `result = CheckTypes(...);`. C# `var` is mutable by default, matching Dart `var`. Idiom: `rf-dart-var-mutable-local-to-csharp-var-local`.
- **dart.triple_quoted_raw_clause_source_literal** → C# VERBATIM string literal `@"..."` (default, broad-compat) OR C# 11+ raw string `"""..."""`. The Dart `\\B` (one-backslash runtime value) becomes C# `\B` (one backslash, since `@"..."` does not process escapes) — the escape count is REDUCED BY ONE across the boundary while the runtime string value is byte-identical. Embedded newlines preserved verbatim. Idiom: `rf-dart-triple-quoted-string-to-csharp-verbatim-or-raw-string`.

Conversion units (mirrored from convspec):
- **cu-1**: file-scope using directives (`System`; `System.Collections.Generic`; `System.Linq`; `<RootNs>.Analysis.TypeChecker`; `<RootNs>.Compiler`) — NO `using Xunit`.
- **cu-2**: single top-level namespace mirroring `test/` (e.g. `<RootNs>.Test`).
- **cu-3**: host class `public static class DebugNegative` — NOT a test class.
- **cu-4**: `private static TypeCheckResult CheckTypes(string source)` helper.
- **cu-5**: `public static int Main(string[] args)` entrypoint with `return 0` at end.
- **cu-6**: two C# verbatim (or raw) string literals carrying the negative/positive GLP grammar payloads.
- **cu-7**: NO `[Fact]`, `[Trait]`, `DisplayName`, or other xUnit metadata.
- **cu-8**: DOWNSTREAM GATE — codegen MUST NOT emit `DebugNegative.cs` until the SUT-side `type_parser.dart` convspec exists.

## 3. Decomposed Task Units

- T1: Emit cu-1 file-scope using directives (System, System.Collections.Generic, System.Linq, <RootNs>.Analysis.TypeChecker, <RootNs>.Compiler) — done.
- T2: Emit cu-2 namespace declaration `<RootNs>.Test` — done.
- T3: Emit cu-3 host class `public static class DebugNegative` — done.
- T4: Emit cu-4 `private static TypeCheckResult CheckTypes(string source)` body, statement-for-statement per §2 mappings — done.
- T5: Emit cu-5 `public static int Main(string[] args)` body translating each Dart `print` to `Console.WriteLine`, each `var result = checkTypes(...)` to `var result = CheckTypes(@"...")`, and preserving if/else and foreach blocks 1:1 — done.
- T6: Emit cu-6 two verbatim/raw string literals with `\\` reduced to `\` while preserving newlines — done.
- T7: Confirm cu-7 — no xUnit attributes anywhere in the emitted file — done.
- T8: Record cu-8 downstream gate against `lib/analysis/type_checker/type_parser.dart` — done.

## 4. Research Findings

None required. Every per-construct mapping is grounded in either a cached idiom row reused per FR-012 / SC-007 (`rf-dart-internal-package-import-to-csharp-using`, `rf-dart-final-local-to-csharp-var-local`, `rf-dart-var-mutable-local-to-csharp-var-local`, `rf-dart-implicit-new-to-csharp-explicit-or-targettyped-new`, `rf-dart-instance-method-camelcase-to-csharp-pascalcase`, `rf-dart-top-level-function-to-csharp-static-class-method`, `rf-dart-string-and-iterable-members-to-dotnet`, `rf-dart-string-interpolation-to-csharp-interpolated-string`, `rf-dart-if-else-to-csharp-if-else`, `rf-dart-for-in-to-csharp-foreach`, `rf-dart-triple-quoted-string-to-csharp-verbatim-or-raw-string`) or in one of the two file-specific idiom rows authoritatively established in the convspec (`rf-dart-debug-script-main-to-csharp-static-main`, `rf-dart-print-in-console-exe-to-console-writeline`). No new research is needed; the convspec already cites Microsoft Learn URLs for `string.Split`, `string.Trim`, `string.Contains`, `string.StartsWith`, `string.Join`, `List<T>.Add`, `Enumerable.SelectMany`, `Enumerable.ToList`, `Console.WriteLine`, the C# `Main` entrypoint shape, interpolated strings, verbatim strings, and raw strings.

## 5. Consistency Pass

- §1 (Source Analysis) accurately reflects the actual `.dart` source (verified by direct read of `glp_runtime_net/test/debug_negative.dart`; sha256 matches the YAML front-matter).
- §2 (Conversion Plan) is verbatim-derived from the ratified convspec at `.codeconv/conversion-specs/test/debug_negative.dart.md` — every construct, every target_decision, every idiom_id, and every conversion-unit (cu-1 through cu-8) is mirrored without alteration.
- §3 (Decomposed Task Units) covers all 8 conversion units one-line each; ordering follows the natural file-emission sequence (usings → namespace → class → helper → entrypoint → literals → attribute-omission audit → downstream gate).
- §4 (Research Findings) records "none required" because every idiom is either a KB cache hit or authoritatively grounded in the convspec's existing citations.
- §6 (Escalations) is empty — fixed, derived from the convspec's `escalations: []`.

Fixed — derived from `.codeconv/conversion-specs/test/debug_negative.dart.md`.

## 6. Escalations

None.
