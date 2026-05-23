---
path: test/module/cssg_modules_test.dart
cycle_group_id: 134
scc_siblings: []
generated_at: 2026-05-21T16:34:45Z
source_sha256: fece36ea3f927a1077c5c1a176b2281d71cc9049947063c871d6dbc53d423a05
schema_version: 1
---

# Conversion Plan: test/module/cssg_modules_test.dart

## 1. Source Analysis

Inspection of `glp_runtime_net/test/module/cssg_modules_test.dart` (179
lines, sha256 `fece36ea3f927a1077c5c1a176b2281d71cc9049947063c871d6dbc53d423a05`).
The file is a `package:test` validation suite that drives the
cssg_modules project (`programs/cssg_modules/`) through the real
parse → partial-evaluation → type-check pipeline used by
`GlpEngine.loadSource`. It modifies no `.glp` files and no runtime code.

Concretely the file contains:

- Lines 9–18: TEN `import` directives:
  - `dart:io` (one core lib)
  - `package:test/test.dart` (test framework)
  - EIGHT internal `package:glp_runtime/...` imports — four under
    `lib/compiler/` (`lexer.dart`, `parser.dart`, `ast.dart as ast`,
    `partial_evaluator.dart`), three under
    `lib/analysis/type_checker/` (`type_ast.dart`,
    `type_environment_builder.dart`, `type_checker.dart`), one under
    `lib/runtime/` (`module_hierarchy.dart`). The third compiler import
    carries the prefix `as ast` (used only on `ast.Module`,
    `ast.Program`).
- Lines 20–177: `void main()` body containing:
  - Lines 22–27: pre-group block — `File('../programs/self.glp')` +
    `.existsSync()` guard; on hit, `.readAsStringSync()` into a `final
    source` local, then TWO setter calls
    (`setPreludeUnitClauseSource(source)` AND
    `setPreludeEnvironmentSource(source)`) — the same `source` string is
    fed to BOTH parallel setters.
  - Lines 29–35: `final cssgRoot = '../programs/cssg_modules';` plus a
    `Directory(cssgRoot).existsSync()` guard that, on miss, throws
    `StateError('cssg_modules directory not found at $cssgRoot')` (a
    string-interpolated message using bare-identifier `$cssgRoot`).
  - Lines 37–44: local function `ast.Module parseFile(String path)` —
    five-statement body: `File(path).readAsStringSync()` → `var source`;
    `Lexer(source)`; `lexer.tokenize()` → `var tokens`; `Parser(tokens)`;
    `return parser.parseModule();`.
  - Lines 46–82: local function `TypeEnvironment buildAncestorScope(
    String targetFile)` — calls `discoverSelfChain(targetFile:
    targetFile, rootDir: cssgRoot)` (NAMED args). Initialises `var env =
    buildPreludeEnvironment()`. `for (final selfGlpPath in chain)`: parse
    self.glp, build two typed empty maps `<String, TypeDef>{}` /
    `<String, ProcDecl>{}`, populate via two inner `for (final ... in
    selfModule.{typeDefs|procDeclarations})` loops, construct
    `TypeEnvironment(types, procedures)`, then reassign `env =
    env.merge(selfEnv)`. Returns `env`.
  - Lines 84–99: local function `TypeCheckResult typeCheckFile(String
    path)` — calls the two preceding helpers, builds `ast.Program(
    module.procedures, module.line, module.column)`, instantiates
    `PartialEvaluator()`, calls `pe.transformDefinedGuards(program)`,
    then `return checkModule(module, transformedProcedures:
    transformedAst.procedures, ancestorScope: ancestorScope);`.
  - Lines 101–177: ONE top-level
    `group('cssg_modules end-to-end', () { ... })` containing FIVE
    `test(...)` calls — all synchronous (no `async`):
    - `'self.glp parses and type-checks'`: `expect(File(selfPath
      ).existsSync(), isTrue, reason: 'self.glp must exist')`,
      `parseFile(selfPath)`, `expect(module.typeDefs, isNotEmpty,
      reason: 'self.glp should define shared types')`,
      `typeCheckFile(selfPath)`, `if (!result.isWellTyped) fail(
      'self.glp type errors:\n${result.errors.join('\n')}')`.
    - `'agent.glp type-checks with PE and ancestor scope'`: same
      shape (existence assert + typeCheckFile + guarded fail).
    - `'ui/mediator.glp type-checks with PE and ancestor scope'`: same
      shape.
    - `'ui/actors.glp type-checks with PE and ancestor scope'`: same
      shape.
    - `'boot.glp parses (untyped orchestration)'`: existence assert +
      `parseFile(bootPath)`, then `final importedDecls =
      module.procDeclarations.where((d) => d.imported).toList()`,
      followed by two `expect(..., isNotEmpty, reason: ...)` calls (for
      `importedDecls` and `module.procedures`).

No `async`/`await`, no `Future`, no `setUp`/`tearDown`, no isolates, no
streams, no `dynamic`, no nullable types, no exception-throw expectation
matchers. Pure synchronous test surface.

## 2. Dart → C#/.NET Conversion Plan

Each construct from the ratified convspec maps to its target shape as
follows. The `→` arrow below is U+2192.

- `dart.package_test.import_directive`
  (`import 'package:test/test.dart';`)
  → drop directive; add `using Xunit;` at file head (cached idiom
  `rf-dart-package-test-import-to-xunit-using`, REUSE verbatim, no
  re-research per FR-012/SC-007). Codegen ALSO adds `using System.IO;`,
  `using System.Collections.Generic;`, and `using System.Linq;` (the
  latter for the `.Where(...).ToList()` LINQ chain in the boot.glp
  test). Project namespace mirrors `test/module` directory →
  `<RootNs>.Test.Module`.

- `dart.dart_io.import_directive` (`import 'dart:io';`)
  → drop directive; replace with `using System.IO;` (cached idiom
  `rf-dart-import-dartio-to-csharp-using-systemio`, REUSE verbatim).
  Only `File` + `Directory` surface used → single `using System.IO;`
  suffices.

- `dart.package_under_test.import_directive` (eight internal
  `package:glp_runtime/...` imports, one carrying `as ast` prefix)
  → THREE `using` lines collapse the eight Dart imports
  (`using <RootNs>.Compiler;`,
  `using <RootNs>.Analysis.TypeChecker;`,
  `using <RootNs>.Runtime;`) plus ONE alias
  `using ast = <RootNs>.Compiler;` preserved for source-shape fidelity.
  Cached idiom `rf-dart-internal-package-import-to-csharp-using`, REUSE
  verbatim.

- `dart.package_test.main_entrypoint` (`void main() { ... }`)
  → eliminate Dart `void main` entirely; lift body onto class
  `CssgModulesEndToEndTests` (xUnit discovers `[Fact]` by reflection).
  The lift produces FOUR shapes on the class (per convspec):
  (1) pre-group file-IO block → `static` constructor;
  (2) `final cssgRoot` Dart local → `private const string CssgRoot =
  "../programs/cssg_modules";` field;
  (3) three local functions → `private` instance helper methods
  (`ParseFile`, `BuildAncestorScope`, `TypeCheckFile`);
  (4) the single `group(...)` → the class itself; each inner `test(...)`
  → one `[Fact(DisplayName = "<label>")]` method.
  Cached idiom `rf-dart-package-test-main-omit-in-xunit`, REUSE verbatim.

- `dart.platform.file_existsSync_readAsStringSync`
  (`File('<path>').existsSync()` / `.readAsStringSync()`)
  → `File.Exists("<path>")` / `File.ReadAllText("<path>")` (cached
  idiom `rf-dart-file-existssync-readasstringsync-to-system-io-file-exists-readalltext`,
  REUSE verbatim). Emitted inside the static constructor as a single
  `File.ReadAllText("../programs/self.glp")` into `var source` then two
  setter calls — the read MUST NOT be duplicated across the two
  setters (byte-identical-source invariant).

- `dart.platform.directory_existsSync_with_state_error_guard`
  (`if (!Directory(cssgRoot).existsSync()) throw StateError(...)`)
  → `if (!Directory.Exists(CssgRoot)) throw new
  InvalidOperationException($"cssg_modules directory not found at
  {CssgRoot}");` (cached idiom
  `rf-dart-directory-existssync-to-system-io-directory-exists` +
  `rf-dart-stateerror-throw-to-csharp-invalidoperationexception`, both
  REUSED verbatim). Static-ctor throw wraps in
  `TypeInitializationException` on subsequent type access — the
  observational equivalent of Dart `main` aborting before any `test()`
  registration.

- `dart.error.stateerror_throw`
  (`throw StateError('cssg_modules directory not found at $cssgRoot');`)
  → `throw new System.InvalidOperationException($"cssg_modules
  directory not found at {CssgRoot}");` (cached idiom
  `rf-dart-stateerror-throw-to-csharp-invalidoperationexception`, REUSE
  verbatim). Single-arg constructor; message via the interpolation
  idiom below.

- `dart.module.global_setter_function`
  (`setPreludeUnitClauseSource(source); setPreludeEnvironmentSource(
  source);`)
  → `PreludeUnitClauses.SetPreludeUnitClauseSource(source);
  PreludeEnvironment.SetPreludeEnvironmentSource(source);` (cached
  idiom `csharp-static-class-no-toplevel-members`, REUSE verbatim).
  Two parallel top-level setters host on DIFFERENT C# static classes
  per the respective lib specs — codegen MUST NOT merge them.

- `dart.package_test.group_block`
  (single `group('cssg_modules end-to-end', () { ... })`)
  → one xUnit class `CssgModulesEndToEndTests` containing the five
  `[Fact(DisplayName = "<original label>")]` methods plus the three
  private helper methods. Label-to-PascalCase rule: `cssg_modules
  end-to-end` → `CssgModulesEndToEnd` → suffix `Tests` →
  `CssgModulesEndToEndTests`. Cached idiom
  `rf-dart-package-test-group-to-xunit-class`, REUSE verbatim.

- `dart.local_function.named_inner_helper` (three local fns in main)
  → three `private` instance methods on `CssgModulesEndToEndTests`:
  `private ast.Module ParseFile(string path)`,
  `private TypeEnvironment BuildAncestorScope(string targetFile)`,
  `private TypeCheckResult TypeCheckFile(string path)`. Closure captures:
  `parseFile` captures nothing; `buildAncestorScope` reads `CssgRoot`
  (lifted const field); `typeCheckFile` calls the other two via `this`.
  Cached idiom `rf-dart-local-function-to-csharp-private-method`, REUSE
  verbatim.

- `dart.collections.typed_empty_map_literal_with_indexer_write`
  (`final types = <String, TypeDef>{};` etc. + indexer writes)
  → `var types = new Dictionary<string, TypeDef>();` plus identical
  `m[k] = v` indexer-write semantics (cached idiom
  `rf-dart-map-literal-typed-to-csharp-dictionary`, REUSE verbatim).
  Iteration order is not observable downstream (consumed by
  `TypeEnvironment` as keyed-lookup collections per
  type_environment_builder.dart.md).

- `dart.for_in_loop_over_list`
  (three `for (final x in xs) { ... }` loops)
  → three `foreach (var x in xs) { ... }` loops (cached idiom
  `rf-dart-for-in-to-csharp-foreach`, REUSE verbatim). All three Dart
  `List<T>`s satisfy `IEnumerable<T>` after conversion.

- `dart.local_var_with_reassignment`
  (`var env = buildPreludeEnvironment(); ... env = env.merge(selfEnv);
  return env;`)
  → `var env = PreludeEnvironment.BuildPreludeEnvironment(); ... env =
  env.Merge(selfEnv); return env;` (cached idiom
  `rf-dart-var-mutable-local-to-csharp-var-local`, REUSE verbatim).
  Free-fn `buildPreludeEnvironment` lifts onto the `PreludeEnvironment`
  static class per type_environment_builder.dart.md.

- `dart.string.interpolation_dollar_local`
  (bare `$cssgRoot` + braced `${result.errors.join('\n')}` forms)
  → C# `$"..."` interpolated string with `{...}` braces for ALL
  interpolations (no Dart bare-`$x` equivalent in C#). Concretely:
  `'$cssgRoot/self.glp'` → `$"{CssgRoot}/self.glp"` (and analogously
  for the other four path strings); `'cssg_modules directory not found
  at $cssgRoot'` → `$"cssg_modules directory not found at {CssgRoot}"`;
  `'self.glp type errors:\n${result.errors.join('\n')}'` →
  `$"self.glp type errors:\n{string.Join(\"\\n\", result.Errors)}"`.
  Cached idiom `rf-dart-string-interpolation-to-csharp-interpolated-string`,
  REUSE verbatim.

- `dart.iterable.join_string_separator` (`result.errors.join('\n')`)
  → `string.Join("\n", result.Errors)` (argument-order INVERSION:
  separator first, iterable second). Cached idiom
  `rf-dart-iterable-join-to-csharp-string-join`, REUSE verbatim.

- `dart.package_test.test_call_simple` (five `test(...)` calls,
  synchronous bodies)
  → five `public void` instance methods, each decorated with
  `[Fact(DisplayName = "<label>")]`. Name mappings (PascalCase, non-
  identifier chars stripped, per convspec):
  - `'self.glp parses and type-checks'` →
    `SelfGlpParsesAndTypeChecks`
  - `'agent.glp type-checks with PE and ancestor scope'` →
    `AgentGlpTypeChecksWithPeAndAncestorScope`
  - `'ui/mediator.glp type-checks with PE and ancestor scope'` →
    `UiMediatorGlpTypeChecksWithPeAndAncestorScope`
  - `'ui/actors.glp type-checks with PE and ancestor scope'` →
    `UiActorsGlpTypeChecksWithPeAndAncestorScope`
  - `'boot.glp parses (untyped orchestration)'` →
    `BootGlpParsesUntypedOrchestration`
  Cached idiom `rf-dart-test-callback-to-xunit-method-body`, REUSE
  verbatim. No method is `async Task` (all callbacks synchronous).

- `dart.package_test.expect_isTrue_with_reason` (five existence
  asserts)
  → `Assert.True(File.Exists(<path>), "<reason>");` (cached idiom
  `rf-dart-expect-isTrue-with-reason-to-xunit-assert-true`, REUSE
  verbatim). Named `reason:` → positional second arg.

- `dart.package_test.fail_call_with_message` (four `if (!result
  .isWellTyped) fail(...)` calls — convspec note: `self.glp`, `agent
  .glp`, `mediator.glp`, `actors.glp`, NOT the boot.glp test which has
  no `fail` call)
  → `if (!result.IsWellTyped) { Assert.Fail($"<file>.glp type
  errors:\n{string.Join(\"\\n\", result.Errors)}"); }` (cached idiom
  `rf-dart-fail-call-to-xunit-assert-fail`, REUSE verbatim).
  Spec default PRESERVES the explicit `if + Assert.Fail` shape (the
  collapsed `Assert.True(result.IsWellTyped, ...)` alternative
  documented in convspec but NOT chosen for this plan).

- `dart.package_test.expect_isNotEmpty_matcher` (three
  `expect(x, isNotEmpty, reason: ...)` calls in the boot.glp test +
  one in the self.glp test)
  → `Assert.NotEmpty(<actual>);` — single-arg form; the Dart
  `reason:` text is DROPPED per the cached idiom's "dedicated-
  assertion rule" (cached idiom
  `rf-dart-expect-isNotEmpty-to-xunit-assert-notempty`, REUSE
  verbatim). `using System.Linq;` is NOT required for this default
  form.

- `dart.iterable.where_to_list` (`module.procDeclarations.where((d) =>
  d.imported).toList()`)
  → `module.ProcDeclarations.Where(d => d.Imported).ToList()` (LINQ
  extension methods on `IEnumerable<T>`; requires `using System.Linq;`).
  Cached idiom
  `rf-dart-iterable-where-tolist-to-csharp-linq-where-tolist`, REUSE
  verbatim. Predicate is single-expression lambda; spec default emits
  bare `d => d.Imported` (no parens around single parameter).

- `dart.string.single_quoted_literal` (~25 single-line single-quoted
  Dart string literals)
  → C# double-quoted string literals; `\n` (LF) preserved verbatim;
  no embedded apostrophes or double-quotes to escape. Cached idiom
  `rf-dart-single-quoted-string-to-csharp-double-quoted-string`, REUSE
  verbatim.

- `dart.local.final_var_declaration` (every `final <name> = <expr>;`
  in main + helper bodies — `rootSelfGlp`, `source`, `cssgRoot`,
  `lexer`, `tokens`, `parser`, `chain`, `types`, `procedures`,
  `selfEnv`, `module`, `ancestorScope`, `program`, `pe`,
  `transformedAst`, `selfPath`, `agentPath`, `mediatorPath`,
  `actorsPath`, `bootPath`, `result`, `importedDecls`, loop binders)
  → `var <name> = <expr>;` (cached idiom
  `rf-dart-final-local-to-csharp-var-local`, REUSE verbatim).
  Constructor calls without `new` (`Lexer(...)`, `Parser(...)`,
  `PartialEvaluator()`, `TypeEnvironment(...)`, `ast.Program(...)`)
  gain `new`: `new Lexer(...)`, `new Parser(...)`, etc. (cached
  `rf-dart-constructor-call-without-new-to-csharp-new`). Dart NAMED
  args at the `discoverSelfChain(targetFile: targetFile, rootDir:
  cssgRoot)` call become C# NAMED args
  `DiscoverSelfChain(targetFile: targetFile, rootDir: CssgRoot)` (C#
  4+ syntax) — preserve named-arg form for readability fidelity.

The single target code unit is `test/module/CssgModulesTest.cs`
(per convspec `target_code_unit`).

## 3. Decomposed Task Units

- T1: emit file-scope `using` directives (`using Xunit;`,
  `using System.IO;`, `using System.Collections.Generic;`,
  `using System.Linq;`, `using <RootNs>.Compiler;`,
  `using <RootNs>.Analysis.TypeChecker;`, `using <RootNs>.Runtime;`,
  `using ast = <RootNs>.Compiler;`) — one-line done.
- T2: emit namespace `<RootNs>.Test.Module` block — one-line done.
- T3: emit class declaration `public class CssgModulesEndToEndTests` —
  one-line done.
- T4: emit `private const string CssgRoot = "../programs/cssg_modules";`
  field — one-line done.
- T5: emit static constructor performing
  `File.Exists`/`File.ReadAllText` of `../programs/self.glp`, twin
  setter calls (`PreludeUnitClauses.SetPreludeUnitClauseSource(source)`
  + `PreludeEnvironment.SetPreludeEnvironmentSource(source)`),
  `Directory.Exists(CssgRoot)` guard with
  `InvalidOperationException` throw — one-line done.
- T6: emit `private ast.Module ParseFile(string path)` — one-line done.
- T7: emit `private TypeEnvironment BuildAncestorScope(string
  targetFile)` (calls `DiscoverSelfChain` with named args; constructs
  two `Dictionary<...>` locals; two `foreach` loops; `env =
  env.Merge(selfEnv)` reassignment loop) — one-line done.
- T8: emit `private TypeCheckResult TypeCheckFile(string path)` (builds
  `ast.Program`, `new PartialEvaluator()`, calls
  `TransformDefinedGuards`, calls `CheckModule` with named args) —
  one-line done.
- T9: emit `[Fact(DisplayName="self.glp parses and type-checks")]
  public void SelfGlpParsesAndTypeChecks()` body — one-line done.
- T10: emit `[Fact(DisplayName="agent.glp type-checks with PE and
  ancestor scope")] public void AgentGlpTypeChecksWithPeAndAncestorScope()`
  body — one-line done.
- T11: emit `[Fact(DisplayName="ui/mediator.glp type-checks with PE and
  ancestor scope")] public void UiMediatorGlpTypeChecksWithPeAndAncestorScope()`
  body — one-line done.
- T12: emit `[Fact(DisplayName="ui/actors.glp type-checks with PE and
  ancestor scope")] public void UiActorsGlpTypeChecksWithPeAndAncestorScope()`
  body — one-line done.
- T13: emit `[Fact(DisplayName="boot.glp parses (untyped orchestration)"
  )] public void BootGlpParsesUntypedOrchestration()` body (existence
  assert + `ParseFile` + `Where(...).ToList()` LINQ chain + two
  `Assert.NotEmpty` asserts) — one-line done.

## 4. Research Findings

none required. Every construct resolves to a cached active idiom from
the convspec KB with multiple prior precedents (see convspec §
"Cached-idiom reuse profile (SC-007 / FR-012)" — 22 cached idioms
covering all 20 constructs). Per the convspec decision-order, KB-hit ⇒
REUSE verbatim, no re-research and no re-derivation. The convspec
records `escalations: []` and explicitly notes "no NEW idiom rows" —
the plan inherits this finding.

## 5. Consistency Pass

fixed — derived from the ratified convspec
`.codeconv/conversion-specs/test/module/cssg_modules_test.dart.md`
(every construct → C#/.NET mapping in §2 mirrors the convspec
`target_decision` field verbatim; the §3 task list mirrors the
convspec `conversion_units` list — `cu-1` covers using-directives
(T1), `cu-2` covers namespace (T2), `cu-3` covers the single
`CssgModulesEndToEndTests` class with its const field, static ctor,
three private helpers, and five `[Fact]` methods (T3–T13), `cu-4`
confirms no sibling test classes are produced). All cached idiom IDs
referenced in §2 are present in the convspec construct list.

## 6. Escalations

None.
