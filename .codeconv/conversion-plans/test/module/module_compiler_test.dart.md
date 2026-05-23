---
path: test/module/module_compiler_test.dart
cycle_group_id: 135
scc_siblings: []
generated_at: 2026-05-21T16:39:23Z
source_sha256: 112ccd7b1688a462b205b63e4ad4082a0088432a2921539cee0597b8c8f7c2dd
schema_version: 1
---

# Conversion Plan: test/module/module_compiler_test.dart

## 1. Source Analysis

The source file `glp_runtime_net/test/module/module_compiler_test.dart` (186 lines, sha256 `112ccd7b1688a462b205b63e4ad4082a0088432a2921539cee0597b8c8f7c2dd`) is a `package:test` xUnit-style test file covering the GLP compiler's module/import support and the `Distribute`/`Transmit` opcodes. Inspected structure:

- **Imports (lines 1–7)**: one `package:test/test.dart` test-framework import; six `package:glp_runtime/...` SUT imports — five `compiler/{lexer, parser, analyzer, codegen, ast}.dart` and one `bytecode/opcodes.dart`.
- **File-scope helper (lines 9–20)**: `List<dynamic> compile(String source)` — wires Lexer → Parser → Analyzer → CodeGenerator and returns `bytecode.ops`. Carries a single `///` doc comment.
- **`void main()` entrypoint (lines 22–185)**: contains FIVE SIBLING top-level `group(...)` calls (NOT nested). No file-level `setUp`, no `late` field, no shared closure state.
  - `group('ImportTable', ...)` — 6 tests on `ImportTable.addImport`/`getIndex`/`size`/`orderedImports`/`contains`.
  - `group('RPC Transformation - Static Module', ...)` — 2 tests calling `compile(...)` with embedded GLP triple-quoted fixtures, filtering `ops.whereType<Distribute>().toList()`, asserting `length`/`importIndex`/`functor`/`arity`.
  - `group('RPC Transformation - Dynamic Module', ...)` — 2 tests, same shape, filtering `ops.whereType<Transmit>().toList()`.
  - `group('RPC Transformation - Mixed', ...)` — 1 test using BOTH `whereType<Distribute>` AND `whereType<Transmit>`.
  - `group('Distribute and Transmit opcodes', ...)` — 2 tests asserting `op.toString()` payloads (`'Distribute([1] factorial/2)'`, `'Transmit(X5, foo/3)'`).
- **13 total `test(label, body)` calls**, all with SYNCHRONOUS closure bodies (no `async`/`Future`).
- **Assertion vocabulary**: ~25 `expect(actual, literal-int|String|bool)` implicit-equals calls; one `expect(x, isNull)` (line 47); one `expect(<List>, <list-literal>)` (line 67 — `['io', 'math', 'utils']`).
- **String literals**: 5 triple-single-quoted (`'''...'''`) GLP-source fixtures; many single-quoted single-line labels and `toString()`-payload literals.
- **Constructor calls**: Dart-2-no-`new` style throughout (`ImportTable()`, `Distribute(1, 'factorial', 2)`, `Transmit(5, 'foo', 3)`, `Lexer(source)`, `Parser(tokens)`, `Analyzer()`, `CodeGenerator()`).
- **Property/getter access**: `table.size`, `distributeOps.length`, `distributeOps[i].importIndex`/`.functor`/`.arity`, `transmitOps[0].functor`/`.arity`, `op.toString()`.
- **Iterable filtering**: 6 invocations of `ops.whereType<T>().toList()` (across the Static / Dynamic / Mixed groups).
- **Comments**: 18 `//` single-line comments inside test bodies documenting SRSW invariants of the embedded GLP fixtures.

## 2. Dart → C#/.NET Conversion Plan

| # | Construct (Dart) | C#/.NET target | Source |
|---|---|---|---|
| 1 | `import 'package:test/test.dart';` | `using Xunit;` (project-wide xUnit policy). Also add `using System.Linq;` (for `OfType<T>` / `ToList`) and `using System.Collections.Generic;` (for `IReadOnlyList<object>`). | convspec `dart.package_test.import_directive` (idiom `rf-dart-package-test-import-to-xunit-using`) |
| 2 | Six `import 'package:glp_runtime/...';` SUT imports | One `using <RootNs>.Compiler;` (the five `compiler/*.dart` collapse into one namespace) + one `using <RootNs>.Bytecode;` (for `Distribute`, `Transmit`). Exact namespace strings decided by SUT-file convspecs; this plan records the SHAPE. | convspec `dart.package_under_test.import_directive` (idiom `rf-dart-internal-package-import-to-csharp-using`) |
| 3 | `void main() { group(...); ×5 }` entrypoint | OMIT entirely. xUnit discovers `[Fact]` methods by reflection — no per-file entrypoint. Body's five sibling `group(...)` calls become five enclosing test classes. | convspec `dart.package_test.main_entrypoint` (idiom `rf-dart-package-test-main-omit-in-xunit`) |
| 4 | Five sibling top-level `group(<label>, () { ... })` calls | Five INDEPENDENT PascalCase test classes within the same `.cs` file: `ImportTableTests`, `RpcTransformationStaticModuleTests`, `RpcTransformationDynamicModuleTests`, `RpcTransformationMixedTests`, `DistributeAndTransmitOpcodesTests`. No shared base class, no `IClassFixture<>` (no shared state). Acronym `RPC` PascalCased as `Rpc` per Microsoft naming guidelines. | convspec `dart.package_test.group_block` (idiom `rf-dart-package-test-group-to-xunit-class`) |
| 5 | `List<dynamic> compile(String source) { ... }` file-scope helper | `internal static class ModuleCompilerTestHelpers` (sibling to the five test classes, same namespace) containing `public static IReadOnlyList<object> Compile(string source) { ... }`. Body translates statement-for-statement (`final x = Ctor(...);` → `var x = new Ctor(...);`). Terminal `return bytecode.ops;` → `return bytecode.Ops;`. | convspec `dart.toplevel.helper_function` (research `rf-dart-toplevel-function-to-csharp-static-helper-class`) |
| 6 | `List<dynamic>` return type of the helper | `IReadOnlyList<object>` — NOT `IReadOnlyList<dynamic>`. Dart `dynamic` ≠ C# `dynamic` (C# `dynamic` activates the DLR with per-access overhead). Consumers use only `OfType<T>` + indexed read — `object` is the correct heterogeneous-list root. | convspec `dart.return_type.list_of_dynamic` (research `rf-dart-list-of-dynamic-to-csharp-ireadonlylist-of-object`) |
| 7 | 13× `test('<label>', () { ... })` synchronous test calls | 13× `public void <PascalName>()` instance methods, each decorated `[Fact(DisplayName = "<original label>")]`. Method-name PascalCase mangling per the convspec's per-test list (e.g. `'assigns 1-based indices to imports'` → `Assigns1BasedIndicesToImports`; `'getIndex returns null for unknown modules'` → `GetIndexReturnsNullForUnknownModules`; etc.). NO `async Task` (all callbacks synchronous). | convspec `dart.package_test.test_call_simple` (idiom `rf-dart-test-callback-to-xunit-method-body`) |
| 8 | `final <name> = <expr>;` local declarations | `var <name> = <expr>;`. None of this file's locals are reassigned (so the `var`-vs-`final` single-assignment relaxation is observably invisible). For `final <n> = <Ctor>(...);` Dart-2-no-`new` constructor calls — add the `new` keyword: `var n = new <Ctor>(...);` per cached `rf-dart-constructor-call-no-new-to-csharp-new-keyword`. | convspec `dart.local.final_var_declaration` (idiom `rf-dart-final-local-to-csharp-var-local`) |
| 9 | `ops.whereType<T>().toList()` (6 invocations) | `ops.OfType<T>().ToList()` (from `System.Linq`). Same runtime-type-filter semantics; same lazy/deferred sequence; same terminal materialisation. NOT `Cast<T>()` (which throws on non-T elements) and NOT `Where(o => o is T).Cast<T>()` (verbose). | convspec `dart.iterable.whereType_filter` (idiom `rf-dart-iterable-where-to-linq`) |
| 10 | `expect(<actual>, <int\|String\|bool literal>)` implicit-equals (~25 calls) | `Assert.Equal(<expected-literal>, <actual>)` — ARGUMENT ORDER FLIPS (Dart actual-first, xUnit expected-first). For the two `expect(table.contains('math'), true)` / `expect(table.contains('io'), false)` calls on line 74/75: literal `Assert.Equal(true, table.Contains("math"))` / `Assert.Equal(false, table.Contains("io"))` (precedent boot_loader_test.dart.md uses this literal shape; `Assert.True`/`Assert.False` are observably equivalent). | convspec `dart.package_test.expect_equals_implicit` (idiom `rf-dart-expect-equals-to-xunit-assertequal`) |
| 11 | `expect(table.getIndex('unknown'), isNull);` (line 47) | `Assert.Null(table.GetIndex("unknown"));` — unary form (no argument flip). | convspec `dart.package_test.expect_isNull_matcher` (idiom `rf-dart-expect-isNull-to-xunit-assert-null`) |
| 12 | `expect(table.orderedImports, ['io', 'math', 'utils']);` (line 67) | `Assert.Equal(new[] { "io", "math", "utils" }, table.OrderedImports);` — `Assert.Equal(IEnumerable<T>, IEnumerable<T>)` with expected-first argument flip. ORDERED element-wise equality on both sides — lossless. | convspec `dart.package_test.expect_list_equals_implicit` (research `rf-dart-expect-list-equals-to-xunit-assertequal-enumerable`) |
| 13 | Triple-single-quoted `'''...'''` GLP-source fixtures (5 occurrences) | C# 11 raw string literals (`""" ... """`). Closing `"""` at column-0 to strip common indent; LF line endings inside the literal payload (codegen normalises any CRLF source artefacts to LF). Fixture payload is byte-identical. | convspec `dart.string.triple_quoted_raw_literal` (idiom `rf-dart-triple-quoted-string-to-csharp-raw-string`) |
| 14 | Single-quoted single-line `'...'` literals (labels, identifiers, `toString` payloads) | C# `"..."` double-quoted literals. All content is ASCII-printable — no escapes needed. The two `toString()` payloads `'Distribute([1] factorial/2)'` and `'Transmit(X5, foo/3)'` contain only legal-inside-`"..."` characters. | convspec `dart.string.single_quoted_literal` (idiom `rf-dart-single-quoted-string-to-csharp-double-quoted-string`) |
| 15 | `distributeOps[i]` / `transmitOps[0]` zero-indexed `[i]` access | `distributeOps[i]` / `transmitOps[0]` — identical syntax. | convspec `dart.list.indexer_access` (idiom `rf-dart-list-indexer-to-csharp-list-indexer`) |
| 16 | Property/getter access `<expr>.<lcc>` | `<expr>.<PascalName>` — consult SUT convspecs for target casing (`table.size` → `table.Size`; `distributeOps.length` → `distributeOps.Count` (Dart `List.length` → C# `List<T>.Count`, the well-known rename); `Distribute.importIndex`/`functor`/`arity` → `Distribute.ImportIndex`/`Functor`/`Arity`; `op.toString()` → `op.ToString()` from `System.Object`). | convspec `dart.property_access.identity` (research `rf-dart-property-access-to-csharp-property-access`) |
| 17 | `///` doc comment above `compile` helper | C# `/// <summary>Helper to compile GLP source to bytecode and return the ops list</summary>` triple-slash XML doc comment. | convspec `dart.doc_comment.triple_slash` (research `rf-dart-doc-comment-to-csharp-xml-doc-comment`) |
| 18 | 18× `//` single-line comments inside test bodies | Preserved VERBATIM as C# `//` single-line comments (identical syntax). They document SRSW invariants of the EMBEDDED GLP-source fixtures (not the Dart→C# transformation); no translation of `M?`/`R?` reader-syntax tokens is required (they are GLP, not Dart). | convspec `dart.comment.line` (research `rf-dart-line-comment-to-csharp-line-comment`) |

## 3. Decomposed Task Units

- T1: Emit file-scope `using` directives — `using Xunit;`, `using System.Linq;`, `using System.Collections.Generic;`, `using <RootNs>.Compiler;`, `using <RootNs>.Bytecode;` (rows 1, 2). done.
- T2: Emit `namespace <RootNs>.Test.Module;` declaration mirroring the `test/module/` path (convspec cu-2). done.
- T3: Emit `internal static class ModuleCompilerTestHelpers` containing `/// <summary>Helper to compile GLP source to bytecode and return the ops list</summary>` + `public static IReadOnlyList<object> Compile(string source) { ... }` with statement-for-statement-translated body (rows 5, 6, 8, 17). done.
- T4: Emit `public class ImportTableTests` with 6 `[Fact(DisplayName = "...")]` methods — `Assigns1BasedIndicesToImports`, `ReturnsSameIndexForDuplicateImports`, `GetIndexReturnsNullForUnknownModules`, `SizeReturnsNumberOfUniqueImports`, `OrderedImportsReturnsImportsInIndexOrder`, `ContainsChecksForModulePresence` — using `Assert.Equal`, `Assert.Null`, and `Assert.Equal(IEnumerable<T>, IEnumerable<T>)` (rows 4, 7, 8, 10, 11, 12, 14, 16). done.
- T5: Emit `public class RpcTransformationStaticModuleTests` with 2 `[Fact]` methods — `CompilesStaticRpcToDistributeOpcode`, `AssignsCorrectIndicesToMultipleStaticRpcs` — calling `ModuleCompilerTestHelpers.Compile(...)` with raw-string GLP fixtures and `OfType<Distribute>().ToList()` (rows 4, 7, 8, 9, 10, 13, 14, 15, 16, 18). done.
- T6: Emit `public class RpcTransformationDynamicModuleTests` with 2 `[Fact]` methods — `CompilesDynamicRpcToTransmitOpcode`, `CompilesDynamicRpcWithMultipleArgs` — same shape as T5 but `OfType<Transmit>().ToList()` (rows 4, 7, 8, 9, 10, 13, 14, 15, 16, 18). done.
- T7: Emit `public class RpcTransformationMixedTests` with 1 `[Fact]` method — `HandlesMixOfStaticAndDynamicRpcs` — using BOTH `OfType<Distribute>` and `OfType<Transmit>` (rows 4, 7, 8, 9, 10, 13, 14, 15, 16, 18). done.
- T8: Emit `public class DistributeAndTransmitOpcodesTests` with 2 `[Fact]` methods — `DistributeToStringFormatsCorrectly`, `TransmitToStringFormatsCorrectly` — `var op = new Distribute(1, "factorial", 2); Assert.Equal("Distribute([1] factorial/2)", op.ToString());` and the analogous `Transmit(5, "foo", 3)` assertion (rows 4, 7, 8, 10, 14, 16). done.
- T9: Verify all five embedded GLP fixtures (5 occurrences of `'''...'''`) emit as column-0-closing C# 11 raw strings (`""" ... """`) with LF line endings — payload byte-identical to the Dart fixture (row 13). done.
- T10: Verify all 18 `//` single-line comments are preserved verbatim inside method bodies (row 18). done.

## 4. Research Findings

none required — every construct resolves via a cached idiom (12 constructs) or via a research finding fully justified in the ratified convspec with authoritative Microsoft Learn / dart.dev / xUnit / pub.dev / api.dart.dev citations (6 first-seen rows: `rf-dart-toplevel-function-to-csharp-static-helper-class`, `rf-dart-list-of-dynamic-to-csharp-ireadonlylist-of-object`, `rf-dart-expect-list-equals-to-xunit-assertequal-enumerable`, `rf-dart-property-access-to-csharp-property-access`, `rf-dart-doc-comment-to-csharp-xml-doc-comment`, `rf-dart-line-comment-to-csharp-line-comment`). All research provenance is verbatim-derived from the convspec's `## Rationale + research provenance` section; no additional WebSearch / WebFetch / Agent lookup performed (none available to sub-agent; none needed).

## 5. Consistency Pass

fixed — derived from the ratified convspec `.codeconv/conversion-specs/test/module/module_compiler_test.dart.md` (source_sha256 `112ccd7b...c2dd` matches the source-file sha256 byte-for-byte; convspec `escalations: []` declared intentional in the rationale). Every row in §2 cites the convspec construct_key + idiom_id / research_finding_id that ratified it. The five conversion units cu-1…cu-9 listed in the convspec's `conversion_units` block are reproduced as T1–T10 in §3 (T4–T8 = cu-4…cu-8; T3 = cu-3; T1 = cu-1; T2 = cu-2; T9 = cu-9; T10 covers the `//` line-comment preservation rule from convspec row `dart.comment.line`).

## 6. Escalations

None.
