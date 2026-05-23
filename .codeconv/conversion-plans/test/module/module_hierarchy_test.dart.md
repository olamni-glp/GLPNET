---
path: test/module/module_hierarchy_test.dart
cycle_group_id: 136
scc_siblings: []
generated_at: 2026-05-21T16:34:44Z
source_sha256: 21a38c8225f5824cc125308c58c06dd808d7db35dfcb085086d0a265fba780aa
schema_version: 1
---

# Conversion Plan: test/module/module_hierarchy_test.dart

## 1. Source Analysis

`glp_runtime_net/test/module/module_hierarchy_test.dart` is an integration test
for `lib/runtime/module_hierarchy.dart` (the SUT exposes the file-level free
functions `discoverSelfChain`, `assembleTypeScope`, and
`setPreludeEnvironmentSource`). It exercises the filesystem walk over `self.glp`
ancestor chains using temporary directory trees built per-test.

Inventory of constructs found by direct inspection of the .dart source:

- **Imports (6)**: `dart:io` (sync), `package:test/test.dart` (test framework),
  and four `package:glp_runtime/...` SUT imports — `compiler/lexer.dart`,
  `compiler/parser.dart`, `compiler/ast.dart`,
  `analysis/type_checker/type_ast.dart`,
  `analysis/type_checker/type_environment_builder.dart`,
  `runtime/module_hierarchy.dart`.
- **Top-of-`main()` once-per-process setup** (lines 12–15): reads
  `'../programs/self.glp'` (relative path) sync and conditionally calls
  `setPreludeEnvironmentSource(...)` — a side-effecting registration. Guarded by
  `rootSelfGlp.existsSync()`.
- **Local helper closures (2)** declared inside `main()`:
  - `Module parseModule(String source)` — synchronous Lexer→Parser→Module
    pipeline.
  - `Future<Directory> createTempHierarchy(Map<String, String> files) async` —
    builds a temp directory tree via `Directory.systemTemp.createTemp(...)`,
    iterates `files.entries` with `file.parent.create(recursive: true)` +
    `file.writeAsString(...)`.
- **Seven sibling top-level `group(...)` blocks** (NOT nested):
  - `Phase 2 - 2a: self.glp chain discovery` — 4 async tests.
  - `Phase 2 - 2b: type scope assembly from ancestor chain` — 2 async tests.
  - `Phase 2 - 2c: shadowing` — 2 async tests.
  - `Phase 2 - 2d: sibling isolation` — 1 async test.
  - `Phase 2 - 2e: type-only self.glp` — 1 async test.
  - `Phase 2 - 2f: prelude as root ancestor` — 1 async test.
  - `Phase 2 - 2g: procedure declarations from ancestor self.glp` — 2 async
    tests.
- **13 `async` test callbacks** with create→try→assert→finally→cleanup
  discipline. Every test follows the shape:
  `final tempDir = await createTempHierarchy({...}); try { ... } finally { await
  tempDir.delete(recursive: true); }`.
- **`final` locals** (single-assignment): `tempDir`, `chain`, `moduleSource`,
  `module`, `env`, `responseDef`, `fooDef`, `proc`, plus the helper-internal
  `lexer`, `tokens`, `parser`, `file`.
- **Map literals** (Dart `Map<String, String>` inferred): 13 occurrences as the
  argument to `createTempHierarchy({'path/key.glp': 'GLP source', ...})`. Keys
  are POSIX-style relative paths (some with embedded `/` separators); values are
  embedded GLP source strings using `\n` for line breaks.
- **Named-argument call sites**: `discoverSelfChain(targetFile: ..., rootDir:
  ...)` and `assembleTypeScope(chain: ..., module: ...)`.
- **String interpolation in path-equality assertions**:
  `expect(chain[0], '${tempDir.path}/self.glp');` etc. — 5 occurrences.
- **Matchers used**: `expect(<int>, <int-literal>)` (5×), `expect(<string>,
  '<interp>')` (5×), `expect(<list>, isEmpty)` (1×), `expect(<bool>, isTrue)`
  (9×), `expect(<bool>, isFalse)` (1×), `expect(<obj>, isNotNull)` (3×).
- **Null-bang member access (3×)**: `responseDef!.alternatives.length`,
  `fooDef!.alternatives.length`, `proc!.exported` — each preceded by a matching
  `isNotNull` assertion.
- **List indexer access (5×)**: `chain[0]`, `chain[1]` for chain element
  verification.
- **`try/finally` cleanup with `await tempDir.delete(recursive: true)`** in the
  finally block of every test (13×).
- **Embedded GLP source strings**: 13 short GLP source fragments inside the map
  literals — these are OPAQUE content, NOT translated.

Source sha256: `21a38c8225f5824cc125308c58c06dd808d7db35dfcb085086d0a265fba780aa`
(verified). Convspec source_sha256 matches.

## 2. Dart → C#/.NET Conversion Plan

Each construct → its target shape, mirroring the ratified convspec verbatim. The
direction arrow `→` is U+2192.

- **`import 'dart:io';` → `using System.IO;`** (cached idiom
  `rf-dart-dart-io-to-csharp-system-io`). Covers `File`, `Directory`,
  `DirectoryInfo`, `Path`. Sub-mappings used: `File.Exists`, `File.ReadAllText`
  (sync, top-of-main bootstrap); `Directory.CreateTempSubdirectory` (.NET 8+),
  `Directory.CreateDirectory`, `File.WriteAllTextAsync`,
  `DirectoryInfo.Delete(bool)` (per-test temp-tree primitives).
- **`import 'package:test/test.dart';` → `using Xunit;`** (cached idiom
  `rf-dart-package-test-import-to-xunit-using`). Also requires `using
  System.Threading.Tasks;` (file-scope) for the `Task` return type of `async`
  test methods.
- **Six `import 'package:glp_runtime/...';` SUT imports → THREE `using`
  directives** (granularity collapse — cached idiom
  `rf-dart-package-import-to-csharp-using-namespace`):
  - `using Glp.Compiler;` (covers lexer.dart + parser.dart + ast.dart →
    `Lexer`, `Parser`, `Module`).
  - `using Glp.Analysis.TypeChecker;` (covers type_ast.dart +
    type_environment_builder.dart → `TypeDef`, `TypeEnvironment`).
  - `using Glp.Runtime;` (covers module_hierarchy.dart → static class
    `ModuleHierarchy`).
- **`void main() { ... }` → ELIMINATED** (cached idiom
  `rf-dart-package-test-main-omit-in-xunit`). The two load-bearing elements
  inside `main()` lift as follows:
  - Top-of-`main` conditional `setPreludeEnvironmentSource(...)` block → lifted
    into a `public sealed class PreludeFixture` constructor (xUnit
    `IClassFixture<PreludeFixture>`). One construction per test-run; ordering
    before any `[Fact]` invocation guaranteed by xUnit.
  - Two helper closures (`parseModule`, `createTempHierarchy`) → lifted into
    `internal static class ModuleHierarchyTestHelpers` with `private static
    Module ParseModule(string source)` and `private static async
    Task<DirectoryInfo> CreateTempHierarchyAsync(Dictionary<string, string>
    files)` (spec-default: shared helpers, NOT per-class duplication).
- **Seven top-level `group(...)` siblings → seven xUnit test classes**, each
  declaring `: IClassFixture<PreludeFixture>` (cached idiom
  `rf-dart-package-test-group-to-xunit-class`). Names PascalCased from the
  labels, `Phase 2 - <letter>:` prefix preserved as `Phase2<letter>`:
  - `Phase2aSelfGlpChainDiscoveryTests` (4 `[Fact]`)
  - `Phase2bTypeScopeAssemblyFromAncestorChainTests` (2 `[Fact]`)
  - `Phase2cShadowingTests` (2 `[Fact]`)
  - `Phase2dSiblingIsolationTests` (1 `[Fact]`)
  - `Phase2eTypeOnlySelfGlpTests` (1 `[Fact]`)
  - `Phase2fPreludeAsRootAncestorTests` (1 `[Fact]`)
  - `Phase2gProcedureDeclarationsFromAncestorSelfGlpTests` (2 `[Fact]`)
  Each class has a constructor `public <ClassName>(PreludeFixture fixture)`
  taking the fixture parameter (required by xUnit fixture-discovery, even if
  unused in the test body).
- **`test('<label>', () async { ... })` (13×) → `[Fact(DisplayName = "<original
  label>")] public async Task <PascalName>() { ... }`** (FIRST-SEEN idiom
  `rf-dart-async-test-callback-to-xunit-async-task-method`). xUnit natively
  discovers and awaits async-Task-returning test methods. `DisplayName`
  preserves the original Dart label verbatim for reporter parity.
- **`final <name> = <expr>;` locals → `var <name> = <expr>;`** (cached idiom
  `rf-dart-final-local-to-csharp-var-local`). `tempDir` typed
  `Task<DirectoryInfo>` → resolved `DirectoryInfo`; `chain` typed
  `IReadOnlyList<string>` per `lib/runtime/module_hierarchy.dart`'s C# render.
- **Map literal `{'k': 'v', ...}` → `new Dictionary<string, string> { ["k"] =
  "v", ... }`** (FIRST-SEEN idiom
  `rf-dart-map-literal-string-string-to-csharp-dictionary-string-string`). Path
  keys with embedded `/` separators preserved verbatim; GLP-source values
  preserved verbatim with `\n` escape (identical syntax in both languages).
- **`Future<Directory> createTempHierarchy(Map<String, String> files) async` →
  `private static async Task<DirectoryInfo> CreateTempHierarchyAsync(Dictionary
  <string, string> files)`** (FIRST-SEEN idiom
  `rf-dart-systemTemp-createTemp-to-csharp-path-getTempPath-plus-directory-createDirectory`).
  Body:
  - `await Directory.systemTemp.createTemp('glp_hierarchy_test_')` →
    `Directory.CreateTempSubdirectory("glp_hierarchy_test_")` (.NET 8+, sync —
    `await` DROPPED).
  - `for (final entry in files.entries)` → `foreach (var entry in files)` (C#
    `Dictionary<K,V>` yields `KeyValuePair<K,V>` with `.Key`/`.Value`).
  - `File('${tempDir.path}/${entry.key}')` (path wrapper) → `Path.Combine(
    tempDir.FullName, entry.Key)` (string path, NOT a FileInfo).
  - `await file.parent.create(recursive: true)` →
    `Directory.CreateDirectory(Path.GetDirectoryName(file)!)` (idempotent +
    recursive by default in .NET — sync, `await` DROPPED).
  - `await file.writeAsString(entry.value)` → `await
    File.WriteAllTextAsync(file, entry.Value)` (genuinely async on both sides).
  - `return tempDir;` → `return tempDir;`.
- **`Module parseModule(String source)` → `private static Module ParseModule(
  string source)`** with body `var lexer = new Lexer(source); var tokens =
  lexer.Tokenize(); var parser = new Parser(tokens); return parser.ParseModule
  ();`. Closure-lift composition under cached idiom
  `rf-dart-package-test-main-omit-in-xunit`.
- **`await tempDir.delete(recursive: true);` → `tempDir.Delete(true);`**
  (FIRST-SEEN idiom
  `rf-dart-directory-delete-recursive-to-csharp-directory-delete-recursive`).
  Sync on .NET (no `DirectoryInfo.DeleteAsync`); `await` DROPPED. Stays in
  `finally` block.
- **`try { ... } finally { await tempDir.delete(recursive: true); }` → `try {
  ... } finally { tempDir.Delete(true); }`** (FIRST-SEEN idiom
  `rf-dart-try-finally-cleanup-to-csharp-try-finally-or-await-using`).
  Byte-faithful preservation of the Dart cleanup shape; `await using` +
  `IAsyncDisposable` flagged as optional polish (not adopted in spec default).
- **`expect(<int>, <int-literal>)` (5×) → `Assert.Equal(<int-literal>, <int>);`
  with ARG-ORDER FLIP** (cached idiom
  `rf-dart-expect-equals-to-xunit-assertequal`). Dart `.length` → C# `.Count`
  (PascalCase, IReadOnlyList<T>.Count). `alternatives` → `Alternatives`.
- **`expect(<string>, '${tempDir.path}/<suffix>')` (5×) → `Assert.Equal(
  $"{tempDir.FullName}/<suffix>", <string>);` with ARG-ORDER FLIP** (cached
  idiom `rf-dart-expect-equals-to-xunit-assertequal`). String interpolation:
  Dart `'${expr}'` ↔ C# `$"{expr}"`. `tempDir.path` (Dart) → `tempDir.FullName`
  (C# DirectoryInfo).
- **`expect(<list>, isEmpty)` (1×) → `Assert.Empty(<list>);`** (cached idiom
  `rf-dart-expect-isEmpty-to-xunit-assert-empty`).
- **`expect(<bool>, isTrue)` (9×) → `Assert.True(<bool>);`** and
  **`expect(<bool>, isFalse)` (1×) → `Assert.False(<bool>);`** (cached idiom
  `rf-dart-expect-isTrue-to-xunit-assert-true` — routing table covers both).
  Targets: `env.HasType(...)`, `env.HasProcedure(...)`, `proc!.Exported`.
- **`expect(<obj>, isNotNull)` (3×) → `Assert.NotNull(<obj>);`** (cached idiom
  `rf-dart-expect-isNotNull-to-xunit-assert-notnull`). xUnit's
  `Assert.NotNull(object?)` triggers null-flow narrowing on the asserted
  variable; subsequent `!` is preserved for byte-faithful migration (optional
  drop as polish pass).
- **`<nullable-expr>!.<member>` (3×) → `<nullable-expr>!.<Member>`** (cached
  idiom `rf-dart-bang-to-csharp-null-forgiving`). Runtime-vs-static nuance:
  preceding `Assert.NotNull` guarantees observable parity. Member
  PascalCasing: `alternatives` → `Alternatives`, `exported` → `Exported`.
- **Named-argument call sites** (cached idiom
  `rf-dart-named-required-params-to-csharp-positional-params`):
  - `discoverSelfChain(targetFile: X, rootDir: Y)` →
    `ModuleHierarchy.DiscoverSelfChain(targetFile: X, rootDir: Y)`.
  - `assembleTypeScope(chain: X, module: Y)` →
    `ModuleHierarchy.AssembleTypeScope(chain: X, module: Y)`.
  Parameter names stay camelCase (NOT PascalCase) per Microsoft naming
  guidelines for parameter names.
- **Single-quoted string literals → double-quoted string literals** (cached
  idiom `rf-dart-single-quoted-string-to-csharp-double-quoted-string`). `\n`
  escape identical in both languages.
- **`chain[i]` (5×) → `chain[i]`** (cached idiom
  `rf-dart-list-indexer-to-csharp-list-indexer`). Identical syntax;
  `IReadOnlyList<string>` indexer.
- **Top-of-`main` `File('../programs/self.glp')` + `existsSync()` +
  `readAsStringSync()` + `setPreludeEnvironmentSource(...)` → lifted into
  `PreludeFixture` constructor** (FIRST-SEEN composition
  `rf-dart-file-existsSync-conditional-prelude-bootstrap-to-csharp-class-static-ctor-or-fixture`).
  Body: `var rootSelfGlpPath = Path.Combine(AppContext.BaseDirectory, "..",
  "..", "..", "..", "programs", "self.glp"); if (File.Exists(rootSelfGlpPath))
  { ModuleHierarchy.SetPreludeEnvironmentSource(File.ReadAllText(
  rootSelfGlpPath)); }`. CWD-divergence nuance (Dart `dart test` CWD = package
  root vs xUnit CWD = test-assembly output directory) resolved via
  `AppContext.BaseDirectory`-relative `Path.Combine` (spec default); MSBuild
  `CopyToOutputDirectory` flagged as cleaner alternative.

Final emitted file structure (conversion_units cu-1 … cu-12 from convspec):

- **cu-1**: file-scope `using` directives — `using System;`, `using
  System.Collections.Generic;`, `using System.IO;`, `using
  System.Threading.Tasks;`, `using Xunit;`, `using Glp.Compiler;`, `using
  Glp.Analysis.TypeChecker;`, `using Glp.Runtime;`.
- **cu-2**: `namespace Glp.Test.Module { ... }` (mirrors test/module path).
- **cu-3**: `public sealed class PreludeFixture` — `IClassFixture<T>` host;
  constructor performs the once-per-run prelude bootstrap.
- **cu-4**: `internal static class ModuleHierarchyTestHelpers` — hosts
  `ParseModule` + `CreateTempHierarchyAsync` (shared by all 7 test classes).
- **cu-5**: `public class Phase2aSelfGlpChainDiscoveryTests :
  IClassFixture<PreludeFixture>` with 4 `[Fact]` async Task methods.
- **cu-6**: `public class Phase2bTypeScopeAssemblyFromAncestorChainTests :
  IClassFixture<PreludeFixture>` with 2 `[Fact]` async Task methods.
- **cu-7**: `public class Phase2cShadowingTests :
  IClassFixture<PreludeFixture>` with 2 `[Fact]` async Task methods.
- **cu-8**: `public class Phase2dSiblingIsolationTests :
  IClassFixture<PreludeFixture>` with 1 `[Fact]` async Task method.
- **cu-9**: `public class Phase2eTypeOnlySelfGlpTests :
  IClassFixture<PreludeFixture>` with 1 `[Fact]` async Task method.
- **cu-10**: `public class Phase2fPreludeAsRootAncestorTests :
  IClassFixture<PreludeFixture>` with 1 `[Fact]` async Task method (the only
  test that semantically depends on PreludeFixture's bootstrap call running
  first).
- **cu-11**: `public class Phase2gProcedureDeclarationsFromAncestorSelfGlpTests
  : IClassFixture<PreludeFixture>` with 2 `[Fact]` async Task methods.
- **cu-12**: `try { ... } finally { tempDir.Delete(true); }` cleanup inside
  every `[Fact]` body — `await` on `tempDir.delete` is DROPPED.

## 3. Decomposed Task Units

- T1 — Emit cu-1 file-scope `using` directives (8 namespaces). DONE.
- T2 — Emit cu-2 `namespace Glp.Test.Module` declaration wrapping the file
  body. DONE.
- T3 — Emit cu-3 `PreludeFixture` sealed class with the
  `AppContext.BaseDirectory`-relative bootstrap constructor (File.Exists +
  File.ReadAllText + ModuleHierarchy.SetPreludeEnvironmentSource). DONE.
- T4 — Emit cu-4 `internal static class ModuleHierarchyTestHelpers` with
  `ParseModule(string)` and `async Task<DirectoryInfo>
  CreateTempHierarchyAsync(Dictionary<string, string>)`. DONE.
- T5 — Emit cu-5 `Phase2aSelfGlpChainDiscoveryTests` class with 4 `[Fact(
  DisplayName=...)]` async Task methods + fixture-ctor parameter. DONE.
- T6 — Emit cu-6 `Phase2bTypeScopeAssemblyFromAncestorChainTests` class with
  2 `[Fact]` methods. DONE.
- T7 — Emit cu-7 `Phase2cShadowingTests` class with 2 `[Fact]` methods. DONE.
- T8 — Emit cu-8 `Phase2dSiblingIsolationTests` class with 1 `[Fact]` method.
  DONE.
- T9 — Emit cu-9 `Phase2eTypeOnlySelfGlpTests` class with 1 `[Fact]` method.
  DONE.
- T10 — Emit cu-10 `Phase2fPreludeAsRootAncestorTests` class with 1 `[Fact]`
  method (depends on PreludeFixture bootstrap). DONE.
- T11 — Emit cu-11 `Phase2gProcedureDeclarationsFromAncestorSelfGlpTests`
  class with 2 `[Fact]` methods. DONE.
- T12 — Translate the 13 test bodies statement-for-statement: map-literal
  fixture → `new Dictionary<string, string> { [...] = ... }`; await
  `CreateTempHierarchyAsync(...)`; named-arg
  `ModuleHierarchy.DiscoverSelfChain(targetFile: ..., rootDir: ...)`; for the
  type-scope tests, `await File.ReadAllTextAsync(...)` + `ParseModule(...)` +
  `ModuleHierarchy.AssembleTypeScope(chain: ..., module: ...)`; assert via the
  matcher routing table; null-bang member access preserved. DONE.
- T13 — Wrap every `[Fact]` body in cu-12 `try { ... } finally {
  tempDir.Delete(true); }` cleanup discipline; verify `await` is dropped on
  the delete. DONE.
- T14 — Verify embedded GLP source strings (13 fixtures) are preserved
  verbatim — opaque content, no translation. DONE.
- T15 — Verify ARG-ORDER FLIP applied at every `Assert.Equal(...)` call site
  (5 int-equality + 5 string-equality). DONE.
- T16 — Verify member-name PascalCasing at `Alternatives`, `Count`, `Exported`,
  `HasType`, `HasProcedure`, `LookupType`, `GetProcedure`, `FullName`. DONE.

## 4. Research Findings

None required. Every construct is resolved either by a cached idiom from prior
module/* convspecs (14 of 21 constructs) or by a FIRST-SEEN idiom row already
documented in the convspec with verbatim authoritative-doc citations (Microsoft
Learn for `Directory.CreateTempSubdirectory`, `Directory.CreateDirectory`,
`File.WriteAllTextAsync`, `DirectoryInfo.Delete(Boolean)`, try-finally,
`IAsyncDisposable`, "Object and Collection Initializers", xunit.net
"Async tests" + "Shared Context between Tests"; api.dart.dev for
`Directory.systemTemp.createTemp`, `Directory.create`, `Directory.delete`,
`File.writeAsString`; dart.dev/language for collections, async, and
error-handling/finally). No additional research is needed at the plan stage.

## 5. Consistency Pass

- File-scope using directives consistency — fixed — derived from convspec
  cu-1 verbatim (8 namespaces in fixed order).
- Async/sync `await`-drop discipline (Dart async → .NET sync where the BCL
  offers no async variant) — fixed — derived from convspec construct
  `dart.async_helper.future_directory_async_function_with_for_in_writeAsString`
  nuance row + `dart.directory.delete_recursive_await` nuance row. The
  `await` is dropped ONLY on `CreateTempSubdirectory`, `CreateDirectory`, and
  `DirectoryInfo.Delete` calls; preserved on `File.WriteAllTextAsync` and
  `File.ReadAllTextAsync`.
- Namespace mapping (Glp.Compiler / Glp.Analysis.TypeChecker / Glp.Runtime) —
  fixed — derived from convspec construct
  `dart.package_under_test.import_directive_sut_imports` and from cited cross-
  convspecs `lib/runtime/module_hierarchy.dart.md`,
  `lib/compiler/ast.dart.md`, `lib/analysis/type_checker/type_ast.dart.md`,
  `lib/analysis/type_checker/type_environment_builder.dart.md`.
- Seven-class topology with PreludeFixture sharing — fixed — derived from
  convspec construct
  `dart.package_test.main_entrypoint_with_top_of_file_setup` decision
  (per-class IClassFixture<T> declarations sharing one fixture instance) and
  construct `dart.package_test.group_block_seven_siblings` (per-group-class
  lift, not flatten, matching module_parser_test.dart.md precedent).
- ARG-ORDER FLIP at `Assert.Equal` — fixed — derived from cached idiom
  `rf-dart-expect-equals-to-xunit-assertequal` per convspec constructs
  `dart.package_test.expect_equals_implicit_int` and
  `dart.package_test.expect_equals_implicit_string`.
- Member PascalCasing (`Alternatives`, `Count`, `Exported`, `HasType`,
  `HasProcedure`, `LookupType`, `GetProcedure`, `FullName`) — fixed — derived from
  cited cross-convspec `type_ast.dart.md` (TypeEnvironment + TypeDef shape) +
  the Microsoft `DirectoryInfo` BCL property names.
- Named-argument call-site form preserved — fixed — derived from convspec
  construct `dart.named_arguments_call_site` (C# 4+ supports named arguments
  identically to Dart at the call site).
- Try/finally cleanup discipline preserved per-test — fixed — derived from
  convspec construct `dart.try_finally_cleanup_discipline`; `await using` +
  `IAsyncDisposable` flagged as optional polish, NOT adopted in spec default.
- `Path.Combine` vs literal `/` separator — fixed — derived from convspec
  construct
  `dart.async_helper.future_directory_async_function_with_for_in_writeAsString`
  nuance (path-composition row): `Path.Combine` used inside the helper for
  the temp-tree path composition; the literal `/` in the
  `'${tempDir.path}/self.glp'` ASSERTION expressions is preserved verbatim
  because the SUT (`ModuleHierarchy`) normalises to forward slashes per its own
  C# render — cross-file consistency with module_hierarchy.dart.md.
- AppContext.BaseDirectory-relative path resolution in PreludeFixture — fixed
  — derived from convspec construct
  `dart.file_constructor_existsSync_readAsStringSync_top_of_main` nuance row
  (CWD-divergence between `dart test` and xUnit; spec-default
  `AppContext.BaseDirectory` chosen to avoid forced MSBuild
  `CopyToOutputDirectory` discipline).
- DisplayName preservation — fixed — derived from convspec construct
  `dart.package_test.group_block_seven_siblings` decision row (`[Fact(
  DisplayName = "<original test label>")]` on every method).
- Shared helper class (cu-4) chosen over per-class duplication — fixed —
  derived from convspec construct
  `dart.package_test.main_entrypoint_with_top_of_file_setup` decision (spec
  default = DRY-shared helper class to avoid seven-fold duplication).
- GLP-source string preservation (opaque content, no translation) — fixed —
  derived from convspec Notes section (final bullet) + construct
  `dart.string.single_quoted_literal_pervasive` nuance.

## 6. Escalations

None.
