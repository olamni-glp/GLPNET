---
path: test/compiler/project_linker_test.dart
cycle_group_id: 117
scc_siblings: []
generated_at: 2026-05-21T16:39:52Z
source_sha256: f9c5c7d728fb53ad1b5d0bda9c918d9af1d7360ac7f3840334c05ee2906d12da
schema_version: 1
---

# Conversion Plan: test/compiler/project_linker_test.dart

## 1. Source Analysis

The file `glp_runtime_net/test/compiler/project_linker_test.dart` is a
`package:test`-based unit-test suite (319 lines, 13 `test()` cases
distributed across FOUR sibling top-level `group(...)` blocks inside a
single `void main()`):

- `group('Project discovery', ...)` — 4 tests:
  `discovers all modules in cssg_modules`,
  `excludes self.glp from modules`,
  `excludes boot_direct.glp from modules`,
  `modules have correct ancestor scopes`.
- `group('Type checking', ...)` — 1 test:
  `all modules type-check successfully` (uses `returnsNormally`).
- `group('Linking', ...)` — 8 tests, with a `setUp(...)` callback and
  three `late` fields (`late List<DiscoveredModule> modules; late
  LinkResult linkResult; late Program linked;`):
  `procedures are renamed with module prefix`,
  `bare procedure names do not exist (except aliases)`,
  `no name conflicts between modules`,
  `cross-module calls are resolved`,
  `local calls are resolved`,
  `prelude calls are preserved unprefixed`,
  `entry point aliases exist for top module`,
  `entry point alias calls renamed procedure`.
- `group('End-to-end compilation', ...)` — 2 tests:
  `linked program compiles to bytecode`,
  `fplay1 produces correct output` (drives the linked bytecode
  end-to-end through `GlpRuntime`, `Scheduler`, `BytecodeRunner`,
  `GoalRef`, `CallEnv`, with diagnostic `print(...)` calls).

File-header construction. Lines 1–6 carry a `///` triple-slash doc
comment ("Project linker tests: static linking of multi-module GLP
projects. Tests discovery, type checking, renaming, call resolution,
and end-to-end compilation of the cssg_modules project.") immediately
followed by `library;` (the unnamed Dart-2.19+ library directive that
anchors file-level doc comments).

Imports (lines 7–17). Nine import directives:

- `import 'dart:io';` — `File`/`Directory` constructors,
  `.existsSync()`, `.readAsStringSync()`, `.absolute.path`.
- `import 'package:test/test.dart';` — `group`, `test`, `setUp`,
  `expect`, matchers (`contains`, `isNot`, `equals`,
  `greaterThanOrEqualTo`, `isFalse`, `isTrue`, `isNotNull`,
  `isNotEmpty`, `returnsNormally`).
- Seven internal `package:glp_runtime/*` imports (with two `show`
  clauses): `compiler/project_linker.dart`,
  `compiler/compiler.dart`,
  `compiler/partial_evaluator.dart show setPreludeUnitClauseSource`,
  `analysis/type_checker/type_environment_builder.dart show
  setPreludeEnvironmentSource`, `compiler/ast.dart`,
  `runtime/runtime.dart`, `runtime/machine_state.dart`,
  `runtime/scheduler.dart`, `bytecode/runner.dart`.

Pre-`group` block (lines 19–33). `void main()` opens with:

1. Conditional prelude load from `'../programs/self.glp'`:
   constructs a `File`, calls `.existsSync()`, on success reads via
   `.readAsStringSync()` and feeds the string to BOTH
   `setPreludeUnitClauseSource(source)` and
   `setPreludeEnvironmentSource(source)`.
2. `final cssgRoot = '../programs/cssg_modules';` and the
   ternary-derived `final rootSelfPath = rootSelfGlp.existsSync()
   ? rootSelfGlp.absolute.path : null;`.
3. Soft-skip guard: `if (!Directory(cssgRoot).existsSync()) {
   print('cssg_modules directory not found at $cssgRoot, skipping
   tests'); return; }` — exits `main` before any `group(...)` runs.

`setUp` block in `Linking` (lines 86–90). Rebuilds the three `late`
fields before each test:
`modules = discoverProject(cssgRoot, rootSelfGlpPath: rootSelfPath);
linkResult = linkProject(modules, 'boot'); linked = linkResult.
program;`.

Inside the test bodies the file uses LINQ-equivalents extensively:
`.map((p) => p.name).toSet()`, `.where((p) => p.name.contains(':'))
.toList()`, `.firstWhere((p) => p.name == 'boot:play1')`, `.any((f)
=> f.contains('boot_direct'))`. Assertion matchers used include
`contains`, `isNot(contains(...))`, `equals(N)`,
`greaterThanOrEqualTo(2)`, `isFalse`, `isTrue`, `isNotNull`,
`isNotEmpty`, `returnsNormally`. The `End-to-end compilation` group
also uses `print(...)` with `${output.length}`/`$line` interpolation
for diagnostic output; calls `GlpCompiler()`,
`compiler.compileProgram(...)` with `procDeclarations:` named arg;
`bytecode.merge(stdlibProg)`; `GlpRuntime()`; the `outputCallback`
field assignment with an arrow lambda; the `runners[program] =
BytecodeRunner(program)` map indexer assignment; `Scheduler(rt: rt)`
with the named REQUIRED `rt:` arg; `rt.nextGoalId++` post-increment;
`CallEnv(args: {})` with empty map; `rt.setGoalEnv` / `rt.
setGoalProgram`; `program.labels['fplay1/0']!` (Dart Map indexer +
non-null assertion); `rt.gq.enqueue(GoalRef(...))`;
`scheduler.drainWithStatus(maxCycles: 50000)`; finally `output.
join('\n')` for the verification string.

## 2. Dart → C#/.NET Conversion Plan

Mirrors the convspec construct-by-construct (verbatim derivation from
spec — every row keyed by its convspec `construct_key`).

- `dart.docblock_triple_slash_file_header_with_library_directive`
  → `///`-XML-doc `<summary>` block on the lifted base class;
  `library;` directive dropped (C# has no analogue — namespace
  declaration carries library scope). Idiom
  `rf-dart-library-directive-to-csharp-namespace-elision`.

- `dart.package_test.import_directive` → drop the Dart import; emit
  `using Xunit;` at file head. Codegen ALSO emits `using System.IO;`,
  `using System.Collections.Generic;`, `using System.Linq;`.
  Namespace `<RootNs>.Test.Compiler`. Idiom
  `rf-dart-package-test-import-to-xunit-using`.

- `dart.dart_io.import_directive` → drop the Dart import; emit
  `using System.IO;`. Surface used: `FileInfo` (`.Exists`,
  `.FullName`), `File.ReadAllText`, `Directory.Exists`. Idiom
  `rf-dart-import-dartio-to-csharp-using-systemio`.

- `dart.internal_package_import.glp_runtime_compiler_analysis_runtime_bytecode_set`
  → nine Dart imports collapse to FOUR `using` directives via the
  langpair file→namespace fold: `using <RootNs>.Compiler;` (five
  Dart imports — `project_linker`, `compiler`, `partial_evaluator`,
  `ast` — collapse), `using <RootNs>.Analysis.TypeChecker;`,
  `using <RootNs>.Runtime;` (three: `runtime`, `machine_state`,
  `scheduler`), `using <RootNs>.Bytecode;` (`runner`). Both `show
  <symbol>` narrowing clauses (on `partial_evaluator` and
  `type_environment_builder`) are dropped — the narrowed symbols
  are already encapsulated on distinct host static classes
  (`PreludeUnitClauses`, `PreludeEnvironment`) per the lib specs, so
  the test site references them via qualified names. Idiom
  `rf-dart-internal-package-import-to-csharp-using`.

- `dart.package_test.main_entrypoint` → Dart `void main()` is
  ELIMINATED. xUnit discovers `[Fact]` methods by reflection. The
  body decomposes into five target shapes (see §3 T-units): (1)
  pre-group file-IO block lifts to a `static` constructor on a
  shared `public abstract class ProjectLinkerTestsBase`; (2)
  `cssgRoot` lifts as `private const string CssgRoot =
  "../programs/cssg_modules";` and `rootSelfPath` lifts as
  `private static readonly string? RootSelfPath =
  RootSelfGlp.Exists ? RootSelfGlp.FullName : null;`; (3) early
  return skip guard lifts to `static readonly bool CssgRootExists
  = Directory.Exists(CssgRoot);` set in the static ctor + per-Fact
  `Assert.SkipWhen(!CssgRootExists, "...");`; (4) the four
  `group(...)` calls lift to four sealed derived classes; (5) the
  `setUp(...)` callback lifts to the `LinkingTests` instance
  constructor. Idiom `rf-dart-package-test-main-omit-in-xunit`.

- `dart.platform.file_existsSync_readAsStringSync` → `var
  rootSelfGlp = new FileInfo("../programs/self.glp"); if
  (rootSelfGlp.Exists) { var source = File.ReadAllText(rootSelfGlp.
  FullName); PreludeUnitClauses.SetPreludeUnitClauseSource(source);
  PreludeEnvironment.SetPreludeEnvironmentSource(source); }`. Uses
  `FileInfo` (instance class) NOT static `File` because the variable
  is referenced TWICE (existence+read, then `.absolute.path`). Idiom
  `rf-dart-file-existssync-readasstringsync-to-system-io-file-exists-readalltext`.

- `dart.platform.directory_existsSync_skip_with_print` → soft-skip
  semantics: emit `CssgRootExists = Directory.Exists(CssgRoot);` in
  the static ctor; each `[Fact]` opens with `Assert.SkipWhen(
  !CssgRootExists, "cssg_modules directory not found at
  ../programs/cssg_modules, skipping tests");` (xUnit v3). NOT a
  throw — that would convert a silent skip into a failing init.
  Idiom
  `rf-dart-directory-existssync-to-system-io-directory-exists`.

- `dart.module.global_setter_function` → both Dart top-level
  setters become qualified static calls on distinct host classes:
  `PreludeUnitClauses.SetPreludeUnitClauseSource(source);
  PreludeEnvironment.SetPreludeEnvironmentSource(source);`. No
  aliasing/merging — the two setters live on different host classes.
  Idiom `csharp-static-class-no-toplevel-members`.

- `dart.local_var.final_conditional_nullable_path_from_file_absolute`
  → `private static readonly string? RootSelfPath = RootSelfGlp.
  Exists ? RootSelfGlp.FullName : null;`. `final` single-assignment
  → `static readonly`; Dart `.absolute.path` two-step chain →
  C# `FileInfo.FullName`; ternary is identical. Idiom
  `rf-dart-final-local-to-csharp-var`.

- `dart.package_test.four_sibling_top_level_groups_in_one_main` →
  four sealed classes deriving from the shared abstract base:
  `ProjectDiscoveryTests` (4 `[Fact]`), `TypeCheckingTests` (1
  `[Fact]`), `LinkingTests` (8 `[Fact]` + 3 null-forgiving fields +
  instance ctor), `EndToEndCompilationTests` (2 `[Fact]` +
  `ITestOutputHelper`-injecting ctor). Each `test('<label>', ...)`
  becomes one `[Fact(DisplayName = "<label>")]` method. Idiom
  `rf-dart-package-test-group-to-xunit-class`.

- `dart.package_test.setUp_inside_group_with_three_late_fields` →
  three `private` null-forgiving fields: `private
  List<DiscoveredModule> _modules = null!;`, `private LinkResult
  _linkResult = null!;`, `private Program _linked = null!;`. The
  `setUp` callback body lifts to `LinkingTests`' instance
  constructor (xUnit's fresh-instance-per-Fact gives Dart-`setUp`
  semantics): `public LinkingTests() { if (!CssgRootExists) return;
  _modules = ProjectLinker.DiscoverProject(CssgRoot,
  rootSelfGlpPath: RootSelfPath); _linkResult = ProjectLinker.
  LinkProject(_modules, "boot"); _linked = _linkResult.Program; }`.
  Assignment order matters (`_modules` → `_linkResult` → `_linked`).
  Idiom `rf-dart-late-field-to-csharp-nullforgiving-field`.

- `dart.iterable.map_toset_member_access` → LINQ chains:
  `_modules.Select(m => m.ModuleName).ToHashSet()`,
  `_modules.Select(m => m.FilePath).ToList()`,
  `_linked.Procedures.Select(p => p.Name).ToHashSet()`,
  `_linked.Procedures.Where(p => p.Name.Contains(':')).Select(p =>
  p.Name).ToHashSet()`,
  `_linked.Procedures.Where(p => p.Name.EndsWith(":merge")).
  ToList()`, `mergeProcs.Select(p => p.Name).ToHashSet()`,
  `bootModule.Ast.Procedures.Select(p => p.Name).ToHashSet()`. Idiom
  `rf-dart-iterable-map-toset-to-csharp-linq-select-tohashset`.

- `dart.iterable.firstWhere_with_lambda_predicate` → LINQ `First`
  (NOT `FirstOrDefault` — Dart `firstWhere` throws `StateError` on
  no-match, C# `First(predicate)` throws `InvalidOperationException`
  on no-match; semantically equivalent). Emissions:
  `_linked.Procedures.First(p => p.Name == "boot:play1");`,
  `_linked.Procedures.First(p => p.Name == "boot:send_to_user_tagged");`,
  `_modules.First(m => m.ModuleName == "boot");`,
  `_linked.Procedures.First(p => p.Name == "play1");`. Idiom
  `rf-dart-iterable-firstwhere-to-csharp-linq-first` (NEW in
  convspec).

- `dart.iterable.any_with_lambda_predicate` →
  `Assert.False(filenames.Any(f => f.Contains("boot_direct")),
  "boot_direct.glp should be excluded");`. Idiom
  `rf-dart-iterable-any-with-expect-reason-join-to-xunit-assert-true-linq`.

- `dart.package_test.expect_collection_contains_string` →
  `Assert.Contains("agent", names);`, `Assert.DoesNotContain("self",
  names);`, `Assert.Contains("agent:agent", procNames);`, etc. Uses
  the IEnumerable overload `Assert.Contains<T>(T, IEnumerable<T>)`
  (disambiguates from the string overload by operand types). Idiom
  `rf-dart-expect-collection-contains-to-xunit-assert-contains`
  (NEW in convspec).

- `dart.package_test.expect_length_equals` → `Assert.Equal(5,
  names.Count);` (expected-first); `Assert.Equal(1, play1Alias.
  Clauses.Count);`; `Assert.Equal(1, body!.Count);`. For the
  `greaterThanOrEqualTo(2)` form: `Assert.True(mergeProcs.Count >= 2,
  "agent:merge and boot:merge should both exist");` (xUnit has no
  built-in `Assert.GreaterOrEqual`). Bang-operator nuance: `body!.
  length` → `body!.Count`. Idioms
  `rf-dart-expect-equals-to-xunit-assert-equal-argorder` +
  `rf-dart-list-length-to-csharp-list-count` +
  `rf-dart-expect-length-greaterthanorequalto-to-xunit-assert-true`
  (the last being new in convspec).

- `dart.package_test.expect_isNotEmpty_with_reason` → reason-
  preserving form: `Assert.True(aliases.Any(), $"Entry point alias
  should exist for {name}");` and `Assert.True(output.Any(),
  "fplay1 should produce tagged output");` (xUnit `Assert.NotEmpty`
  has no message overload). Idiom
  `rf-dart-expect-isNotEmpty-to-xunit-assert-notempty`.

- `dart.package_test.expect_isNotNull` → reason-bearing site
  switches to `Assert.True(mod.AncestorScope is not null, $"{mod.
  ModuleName} should have an ancestor scope");`; the unreasoned
  site emits canonical `Assert.NotNull(body);`. Idiom
  `rf-dart-expect-isNotNull-to-xunit-assert-notnull`.

- `dart.package_test.expect_isTrue_isFalse` → `Assert.False(
  prefixedProcs.Contains("merge"));`,
  `Assert.True(bytecode.Labels.ContainsKey("boot:play1/0"));`,
  `Assert.True(bytecode.Labels.ContainsKey("play1/0"), "Entry point
  alias should be in bytecode");`. The
  `expect(prefixedProcs, contains('agent:merge'));` line resolves
  to `Assert.Contains("agent:merge", prefixedProcs);` per the
  collection-contains row. Both `Assert.True(bool, string)` and
  `Assert.False(bool, string)` exist (have message overloads).
  Idiom `rf-dart-expect-isTrue-to-xunit-assert-true`.

- `dart.package_test.expect_call_returnsNormally` → BARE call:
  `ProjectLinker.TypeCheckProject(_modules);` (no assertion
  wrapper; xUnit treats a no-throw body as pass). Idiom
  `rf-dart-expect-returns-normally-to-xunit-bare-call`.

- `dart.control_flow.for_in_clauses_collect_goal_functors` →
  `var bodyFunctors = new HashSet<string>(); foreach (var clause in
  bootPlay1.Clauses) { if (clause.Body is not null) { foreach (var
  goal in clause.Body) { bodyFunctors.Add(goal.Functor); } } }`.
  Bang elided per C# flow-analysis. Idioms
  `rf-dart-for-in-to-csharp-foreach` +
  `rf-dart-set-literal-typed-to-csharp-hashset-initializer`.

- `dart.compiler.compile_program_with_named_optional_arg` →
  `var compiler = new GlpCompiler(); var bytecode = compiler.
  CompileProgram(result.Program, procDeclarations: result.
  ProcDeclarations);`. Idiom
  `rf-dart-implicit-new-and-camelcase-to-csharp-explicit-new-pascalcase`.

- `dart.compiler.compile_program_merge_to_var_reassign` →
  `var stdlibProg = compiler.Compile(File.ReadAllText("../programs/
  self.glp")); var program = bytecode.Merge(stdlibProg);`. No
  defensive `File.Exists` check inserted (FR-024 forbids adding
  checks not in source). Idiom
  `rf-dart-implicit-new-and-camelcase-to-csharp-explicit-new-pascalcase`.

- `dart.runtime.glpruntime_construction_outputcallback_assign` →
  `var rt = new GlpRuntime(); var output = new List<string>(); rt.
  OutputCallback = s => output.Add(s);`. `outputCallback` typed as
  `Action<string>?` per the lib spec. Idiom
  `rf-dart-arrow-lambda-to-csharp-lambda`.

- `dart.runtime.runners_map_indexer_assignment` → `rt.Runners[
  program] = new BytecodeRunner(program);`. Idiom
  `rf-dart-list-indexer-to-csharp-list-indexer`.

- `dart.runtime.scheduler_constructor_named_arg` → `var scheduler
  = new Scheduler(rt: rt);` (preserves named-arg call form; the
  parameter on the C# constructor is positional-or-named without
  default, since `required` keyword doesn't apply to constructor
  parameters). Idiom `rf-dart-named-argument-to-csharp-named-argument`.

- `dart.runtime.member_access_assignment_dotted_property_chains` →
  six rows of access/call: `var goalId = rt.NextGoalId++;`,
  `var env = new CallEnv(args: new Dictionary<string, object>());`,
  `rt.SetGoalEnv(goalId, env);`, `rt.SetGoalProgram(goalId,
  program);`, `var fplayPc = program.Labels["fplay1/0"];` (NO `!` —
  C# `Dictionary` indexer already throws on miss, subsuming Dart's
  non-null assertion; idiom
  `rf-dart-map-bracket-bang-to-csharp-dictionary-bracket` registered
  in convspec), `rt.Gq.Enqueue(new GoalRef(goalId, fplayPc));`, `var
  execResult = scheduler.DrainWithStatus(maxCycles: 50000);`. Idiom
  `rf-dart-instance-method-call-camelcase-to-csharp-pascalcase`.

- `dart.io.print_in_test_with_string_interpolation` →
  `_output.WriteLine($"=== Static link fplay1 output ({output.
  Count} lines) ===");`, `foreach (var line in output) { _output.
  WriteLine($"  {line}"); }`, `_output.WriteLine("=== Static link
  fplay1 produced no output ===");`, `_output.WriteLine($"Status:
  {execResult.Status}");`. Constructor-injection of
  `ITestOutputHelper` required on `EndToEndCompilationTests`. Idiom
  `rf-dart-print-to-xunit-itestoutputhelper-writeline`.

- `dart.package_test.expect_string_contains_substring_with_reason`
  → string-overload sites preserve reason via `Assert.True`:
  `Assert.True(outputStr.Contains("tagged(alice"), "Output should
  contain tagged messages for alice");`, etc. Set-overload sites
  similarly: `Assert.True(bodyFunctors.Contains("actors:alice1"),
  "actors # alice1 should become actors:alice1");`, etc. The
  `isNot(contains('#'))` form becomes `Assert.False(bodyFunctors.
  Contains("#"), "No RemoteGoal # dispatch should remain");`. Idiom
  `rf-dart-expect-collection-contains-to-xunit-assert-contains`.

- `dart.string.join_with_newline` → `var outputStr = string.Join(
  "\n", output);` (static `string.Join(sep, coll)` flips
  argument-order vs Dart's instance `coll.join(sep)`). Idiom
  `rf-dart-iterable-join-to-csharp-string-join`.

## 3. Decomposed Task Units

- T1: emit file-scope `using` directives — `using Xunit;`, `using
  System.IO;`, `using System.Collections.Generic;`, `using
  System.Linq;`, `using <RootNs>.Compiler;`, `using <RootNs>.
  Analysis.TypeChecker;`, `using <RootNs>.Runtime;`, `using
  <RootNs>.Bytecode;`. (cu-1)
- T2: emit `namespace <RootNs>.Test.Compiler;`. (cu-2)
- T3: emit `public abstract class ProjectLinkerTestsBase` with
  `private static readonly FileInfo RootSelfGlp = new FileInfo(
  "../programs/self.glp");`, `private const string CssgRoot =
  "../programs/cssg_modules";`, `private static readonly string?
  RootSelfPath = RootSelfGlp.Exists ? RootSelfGlp.FullName :
  null;`, `private static readonly bool CssgRootExists;`, and the
  static constructor running the prelude-load block + the
  CssgRootExists assignment. (cu-3)
- T4: lift the Dart `///` library doc-comment to a `<summary>`
  XML-doc block on `ProjectLinkerTestsBase`; drop `library;`. (cu-4)
- T5: emit `public sealed class ProjectDiscoveryTests :
  ProjectLinkerTestsBase` with 4 `[Fact(DisplayName = "...")]`
  methods; each body opens with the `Assert.SkipWhen(!
  CssgRootExists, "...")` skip-gate, then performs the
  `ProjectLinker.DiscoverProject` call and the assertions per the
  convspec assertion-shape routing. (cu-5)
- T6: emit `public sealed class TypeCheckingTests :
  ProjectLinkerTestsBase` with 1 `[Fact]` performing the skip-gate
  + `var modules = ProjectLinker.DiscoverProject(...);
  ProjectLinker.TypeCheckProject(modules);` (returnsNormally → bare
  call). (cu-6)
- T7: emit `public sealed class LinkingTests :
  ProjectLinkerTestsBase` with three null-forgiving fields
  (`_modules`, `_linkResult`, `_linked`), an instance constructor
  running the per-Fact rebuild (skip-aware), and 8 `[Fact]`
  methods. (cu-7)
- T8: emit `public sealed class EndToEndCompilationTests :
  ProjectLinkerTestsBase` with `private readonly ITestOutputHelper
  _output;` and an `ITestOutputHelper`-injecting constructor; 2
  `[Fact]` methods including the `Fplay1ProducesCorrectOutput` test
  that drives `GlpRuntime`, `Scheduler`, `BytecodeRunner`,
  `GoalRef`, `CallEnv`. (cu-8)
- T9: emit all `expect(...)` → `Assert.*` translations per the
  assertion-shape routing (Assert.Equal / Assert.True / Assert.False
  / Assert.NotNull / Assert.NotEmpty / Assert.Contains /
  Assert.DoesNotContain); reason-preserving forms switch to
  `Assert.True(expr, msg)` where the underlying overload lacks a
  message parameter. (cu-9)
- T10: emit all LINQ chains (`Select`/`Where`/`ToHashSet`/`ToList`/
  `First`/`Any`/`Contains`/`ContainsKey`) at each iterable-
  projection site per the cached LINQ idioms. (cu-10)
- T11: emit `_output.WriteLine(...)` calls in place of `print(...)`
  inside `EndToEndCompilationTests.Fplay1ProducesCorrectOutput`. (cu-11)
- T12: ensure NO top-level Dart `void main()` equivalent exists —
  xUnit discovery is attribute-driven; init lifted to the base
  class's static constructor; four sibling sealed classes
  produced. (cu-12)

## 4. Research Findings

none required — every construct row REUSES a cached idiom from a
prior batch precedent (lib + test spec batch) or registers a new
idiom that is authoritatively supported on both sides via the
api.dart.dev + Microsoft Learn / xunit.net references already cited
in the convspec. Three NEW idioms registered in convspec
(`rf-dart-iterable-firstwhere-to-csharp-linq-first`,
`rf-dart-expect-collection-contains-to-xunit-assert-contains`,
`rf-dart-map-bracket-bang-to-csharp-dictionary-bracket`) — each
with authoritative citations on both sides. No WebSearch / WebFetch
/ Agent invocations required.

## 5. Consistency Pass

fixed — derived from convspec
`.codeconv/conversion-specs/test/compiler/project_linker_test.dart.md`
(schema_version 1, source_sha256
`f9c5c7d728fb53ad1b5d0bda9c918d9af1d7360ac7f3840334c05ee2906d12da`,
matches the source sha256 computed for THIS plan). All construct
rows in §2 mirror convspec construct rows verbatim. All task units
in §3 mirror convspec `conversion_units` (cu-1 through cu-12)
one-for-one. All idiom references (`rf-*` and
`csharp-static-class-no-toplevel-members`) cited above are present
in the convspec. Convspec `escalations: []` — zero open issues —
carries forward to this plan.

## 6. Escalations

None.
