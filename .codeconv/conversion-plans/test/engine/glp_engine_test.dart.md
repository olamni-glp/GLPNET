---
path: test/engine/glp_engine_test.dart
cycle_group_id: 124
scc_siblings: []
generated_at: 2026-05-21T16:50:12Z
source_sha256: ba6d7b38ff34bd811a6ead5ef440929fb6c02eff1295f1906b68a37b7b4ac2eb
schema_version: 1
---

# Conversion Plan: test/engine/glp_engine_test.dart

## 1. Source Analysis

Direct inspection of `glp_runtime_net/test/engine/glp_engine_test.dart`
(97 lines, sha256 `ba6d7b38ff34bd811a6ead5ef440929fb6c02eff1295f1906b68a37b7b4ac2eb`):

- **Top-level doc-comment** (line 1): `/// Tests for GlpEngine - the unified GLP execution core`.
- **Imports** (lines 2–5): `dart:io`, `package:test/test.dart`,
  `package:glp_runtime/engine/glp_engine.dart`,
  `package:glp_runtime/runtime/scheduler.dart`.
- **`void main()`** (line 7) wraps a single outer
  `group('GlpEngine', () { ... })` (lines 8–95).
- **Late field + setUp** (lines 9–13):
  - `late GlpEngine engine;`
  - `setUp(() { engine = GlpEngine(rootSelfGlpPath: File('../programs/self.glp').absolute.path); });`
- **Five `test(...)` cases**, each an `async` closure:
  1. `'runs simple goal with binding'` (lines 15–28): `engine.loadSource('''procedure test(_?, _). test(a, b). test(b, c).''');` → `await engine.runGoal('test(a, X)')` →
     `print('Status: ${result.status}, error: ${result.error}')` →
     `expect(result.succeeded, isTrue, reason: 'Error: ${result.error}')` →
     `expect(result.bindings['X'], isNotNull)` →
     `print('X = ${result.bindings['X']}')`.
  2. `'clause selection by constant matching'` (lines 30–54):
     loadSource (pick/2 three clauses) → `var result = await engine.runGoal('pick(alice, X)')` →
     `expect(result.succeeded, isTrue)` → print → constructs SECOND engine
     `final engine2 = GlpEngine(rootSelfGlpPath: File('../programs/self.glp').absolute.path);` → second LoadSource (same source) →
     `result = await engine2.runGoal('pick(bob, X)')` → assert/print.
  3. `'loads and runs actor-style clauses'` (lines 56–76): loadSource (actor/2 + three predicates with `ground(Ch?)` guards) → `await engine.runGoal('actor(alice, some_channel)')` →
     `expect(result.succeeded, isTrue)` → print.
  4. `'fails on unknown predicate'` (lines 78–82): NO loadSource (uses
     just root-self loaded in ctor) → `await engine.runGoal('unknown_predicate(x)')` →
     `expect(result.failed, isTrue)` → `expect(result.error, contains('not found'))`.
  5. `'runs conjunction'` (lines 84–94): loadSource (set/2 two clauses) →
     `await engine.runGoal('set(a, X), set(b, Y)')` →
     `expect(result.status, isNot(ExecutionStatus.failed))` → print.

No exception handling in test bodies, no parallelism, no `Future` outside
the documented `await engine.runGoal(...)` calls, no nested groups, no
tearDown / setUpAll / tearDownAll callbacks. All six `print(...)` calls
are diagnostic-only.

## 2. Dart → C#/.NET Conversion Plan

Each Dart construct maps to its C#/.NET counterpart as ratified in the
convspec; cross-file dependency shapes inherited from
`lib/engine/glp_engine.dart.md` (SUT), `lib/runtime/scheduler.dart.md`
(ExecutionStatus enum), and the test-exemplar set
(`boot_loader_test.dart.md`, `binding_pointer_test.dart.md`,
`module_activation_test.dart.md`, `rpc_routing_test.dart.md`).

- **Doc-comment** (`dart.doc_comment.toplevel_triple_slash`) → XML-doc
  `<summary>` block on the test class `GlpEngineTests`.
- **`import 'dart:io';`** (`dart.import.dart_io`) → `using System.IO;`
  (provides `Path`/`FileInfo`).
- **`import 'package:test/test.dart';`** (`dart.package_test.import_directive`)
  → `using Xunit;` (project-pinned framework; cached idiom
  `rf-dart-package-test-import-to-xunit-using`).
- **`import 'package:glp_runtime/engine/glp_engine.dart';`** and
  **`import 'package:glp_runtime/runtime/scheduler.dart';`**
  (`dart.package_under_test.import_directive_engine_and_scheduler`) →
  two `using` directives: `using <RootNs>.Engine;` (GlpEngine,
  ExecutionResult) and `using <RootNs>.Runtime;` (ExecutionStatus).
  Plus `using System.Threading.Tasks;` for `Task` and (recommended)
  `using Xunit.Abstractions;` if `ITestOutputHelper` is adopted.
- **`void main()`** (`dart.package_test.main_entrypoint`) → eliminated
  entirely (xUnit reflection-driven discovery).
- **`group('GlpEngine', () { ... })`** (`dart.package_test.group_block_single_outer`)
  → single test class `GlpEngineTests` in `namespace <RootNs>.Test.Engine`.
  No `[Trait]` partition (single group). Five `[Fact]` methods, each
  decorated with `[Fact(DisplayName = "<original label>")]`.
  - `'runs simple goal with binding'` → `RunsSimpleGoalWithBinding`
  - `'clause selection by constant matching'` → `ClauseSelectionByConstantMatching`
  - `'loads and runs actor-style clauses'` → `LoadsAndRunsActorStyleClauses`
  - `'fails on unknown predicate'` → `FailsOnUnknownPredicate`
  - `'runs conjunction'` → `RunsConjunction`
- **`late GlpEngine engine; setUp(() => engine = ...)`**
  (`dart.package_test.setup_callback_with_late_field`) →
  `private readonly GlpEngine _engine;` field assigned in xUnit ctor
  `public GlpEngineTests() { _engine = new GlpEngine(rootSelfGlpPath: Path.GetFullPath("../programs/self.glp")); }`.
  Per-test class instantiation provides per-test isolation (no leakage
  of `_loadedPrograms` / `_loadedModules` between tests).
- **`File('../programs/self.glp').absolute.path`**
  (`dart.constructor_call.file_path_absolute_path_property`) →
  `Path.GetFullPath("../programs/self.glp")` (preferred — pure static,
  no FileInfo allocation; equivalent `new FileInfo("../programs/self.glp").FullName`
  also acceptable). Applied at both construction sites (ctor + `engine2` local).
- **Test-callback bodies** (`dart.package_test.test_call_async_with_load_source_then_run_goal`)
  → `public async Task <PascalLabel>()` methods. No `-Async` suffix on
  test methods (xUnit ecosystem norm). Statement-by-statement conversion.
- **`final result = await engine.runGoal(...)` / `var result = ...; result = ...`**
  (`dart.local_var.final_executionresult_from_await`) →
  `var result = await _engine.RunGoalAsync(...);`. The `var result` /
  reassignment pattern in 'clause selection' maps verbatim:
  `var result = await _engine.RunGoalAsync("pick(alice, X)"); ...; result = await engine2.RunGoalAsync("pick(bob, X)");`.
- **`engine.loadSource('''<GLP source>''')`**
  (`dart.method_call.engine_load_source_with_triple_quoted_glp`) →
  `_engine.LoadSource("""<verbatim multi-line GLP source>""");`
  using C# 11+ raw-string-literal `"""..."""` (preferred — no escaping
  needed; the five GLP source strings contain NO `"` characters)
  OR `@"..."` verbatim form (universal since C# 2.0). Codegen target
  language version determines the form. PascalCased method name
  `LoadSource` per SUT spec.
- **`await engine.runGoal('<goal>')`** (and `await engine2.runGoal(...)`)
  (`dart.method_call.engine_run_goal_async_returning_execution_result`)
  → `await _engine.RunGoalAsync("<goal>")` (and `await engine2.RunGoalAsync(...)`)
  per SUT spec (`-Async` suffix on the SUT method).
- **`result.succeeded` / `result.failed` / `result.status` / `result.error`**
  (`dart.member_access.executionresult_succeeded_failed_status_error`) →
  `result.Succeeded` / `result.Failed` / `result.Status` / `result.Error`
  PascalCase get-only properties on the SUT's `sealed class ExecutionResult`.
- **`result.bindings['X']` / `result.bindings['Y']`**
  (`dart.member_access.executionresult_bindings_indexer_string_key`) →
  `result.Bindings["X"]` / `result.Bindings["Y"]` (indexer on
  `IReadOnlyDictionary<string, RtTerm?>`; success-path lookup, key
  expected to be present). NOT `TryGetValue` — preserve throw-on-missing
  semantics as a regression signal.
- **`print('<lit>${expr}<lit>')`** (`dart.string_interpolation.in_print_call`)
  → `Console.WriteLine($"<lit>{expr}<lit>");`
  (LITERAL mapping — cached idiom). Recommended alternative
  `_output.WriteLine($"...")` via injected `ITestOutputHelper` for
  proper xUnit-runner output capture; both options preserve pass/fail
  outcomes identically (taste decision at codegen time, no escalation).
- **`expect(result.succeeded, isTrue)` / `expect(..., isTrue, reason: '...')`**
  (`dart.package_test.expect_isTrue`) →
  `Assert.True(result.Succeeded)` for plain form;
  `Assert.True(result.Succeeded, $"Error: {result.Error}")` for the
  reason-form (the first test's `reason: 'Error: ${result.error}'`).
- **`expect(result.bindings['X'], isNotNull)`** (`dart.package_test.expect_isNotNull`)
  → `Assert.NotNull(result.Bindings["X"]);`.
- **`expect(result.error, contains('not found'))`** (`dart.package_test.expect_contains_substring`)
  → `Assert.Contains("not found", result.Error);`
  (ARG-FLIP: substring first, actual second per Microsoft Learn
  `xunit.Assert.Contains(string, string)` overload).
- **`expect(result.status, isNot(ExecutionStatus.failed))`** (`dart.package_test.expect_isNot_with_enum_value`)
  → `Assert.NotEqual(ExecutionStatus.Failed, result.Status);`
  (enum member PascalCased per scheduler.dart.md; ARG-FLIP: expected
  first, actual second).
- **`GlpEngine(rootSelfGlpPath: File('../programs/self.glp').absolute.path)`**
  (`dart.constructor_call.engine_named_required_root_self_glp_path`) →
  `new GlpEngine(rootSelfGlpPath: Path.GetFullPath("../programs/self.glp"))`
  with named-argument call-site preserved for self-documentation. Both
  construction sites (ctor body + `engine2` local in 'clause selection').

### Conversion units (file structure)

- **cu-1**: file-scope using directives —
  `using Xunit; using System; using System.IO; using System.Threading.Tasks; using <RootNs>.Engine; using <RootNs>.Runtime;`
  (plus optionally `using Xunit.Abstractions;` if `ITestOutputHelper` chosen).
- **cu-2**: `namespace <RootNs>.Test.Engine` declaration.
- **cu-3**: XML-doc `<summary>` on the class preserving
  "Tests for GlpEngine - the unified GLP execution core".
- **cu-4**: `public class GlpEngineTests` (single class; no nested
  classes; no `[Trait]` attribute).
- **cu-5**: `private readonly GlpEngine _engine;` field.
- **cu-6**: `public GlpEngineTests() { _engine = new GlpEngine(rootSelfGlpPath: Path.GetFullPath("../programs/self.glp")); }`
  constructor (mirrors Dart `setUp`).
- **cu-7** (OPTIONAL): inject `ITestOutputHelper output` into ctor +
  `private readonly ITestOutputHelper _output;` if the
  ITestOutputHelper-route is chosen for `print` mapping.
- **cu-8**: `[Fact(DisplayName = "runs simple goal with binding")]
  public async Task RunsSimpleGoalWithBinding()` — body:
  `_engine.LoadSource(""" procedure test(_?, _). test(a, b). test(b, c). """);`
  `var result = await _engine.RunGoalAsync("test(a, X)");`
  `Console.WriteLine($"Status: {result.Status}, error: {result.Error}");`
  `Assert.True(result.Succeeded, $"Error: {result.Error}");`
  `Assert.NotNull(result.Bindings["X"]);`
  `Console.WriteLine($"X = {result.Bindings[\"X\"]}");`.
- **cu-9**: `[Fact(DisplayName = "clause selection by constant matching")]
  public async Task ClauseSelectionByConstantMatching()` — body:
  LoadSource(pick/2 triple) + `var result = await _engine.RunGoalAsync("pick(alice, X)")` +
  `Assert.True(result.Succeeded)` + Console.WriteLine + method-local
  `var engine2 = new GlpEngine(rootSelfGlpPath: Path.GetFullPath("../programs/self.glp"));` +
  `engine2.LoadSource(""" ... """);` +
  `result = await engine2.RunGoalAsync("pick(bob, X)");` +
  `Assert.True(result.Succeeded)` + Console.WriteLine.
- **cu-10**: `[Fact(DisplayName = "loads and runs actor-style clauses")]
  public async Task LoadsAndRunsActorStyleClauses()` — body:
  LoadSource(actor/2 + three done predicates) +
  `var result = await _engine.RunGoalAsync("actor(alice, some_channel)");` +
  `Assert.True(result.Succeeded)` + Console.WriteLine.
- **cu-11**: `[Fact(DisplayName = "fails on unknown predicate")]
  public async Task FailsOnUnknownPredicate()` — body: NO LoadSource
  (engine has only root-self) +
  `var result = await _engine.RunGoalAsync("unknown_predicate(x)");` +
  `Assert.True(result.Failed)` +
  `Assert.Contains("not found", result.Error);`.
- **cu-12**: `[Fact(DisplayName = "runs conjunction")]
  public async Task RunsConjunction()` — body: LoadSource(set/2 two
  clauses) + `var result = await _engine.RunGoalAsync("set(a, X), set(b, Y)");` +
  `Assert.NotEqual(ExecutionStatus.Failed, result.Status);` +
  Console.WriteLine.
- **cu-13**: NO `IDisposable` / `Dispose` method (SUT does not advertise
  `IDisposable`; xUnit GC-driven cleanup suffices).

## 3. Decomposed Task Units

- **T1**: emit file-scope `using` directives (cu-1) — single source-of-truth done.
- **T2**: emit `namespace <RootNs>.Test.Engine` (cu-2) — done.
- **T3**: emit XML-doc `<summary>` block (cu-3) — done.
- **T4**: emit `public class GlpEngineTests` (cu-4) — done.
- **T5**: emit `private readonly GlpEngine _engine;` field (cu-5) — done.
- **T6**: emit `public GlpEngineTests()` ctor with `_engine =
  new GlpEngine(rootSelfGlpPath: Path.GetFullPath("../programs/self.glp"));` (cu-6) — done.
- **T7** (OPTIONAL): emit `ITestOutputHelper` injection (cu-7) — taste, done if chosen.
- **T8**: emit method cu-8 (`RunsSimpleGoalWithBinding`) — done.
- **T9**: emit method cu-9 (`ClauseSelectionByConstantMatching`) — done.
- **T10**: emit method cu-10 (`LoadsAndRunsActorStyleClauses`) — done.
- **T11**: emit method cu-11 (`FailsOnUnknownPredicate`) — done.
- **T12**: emit method cu-12 (`RunsConjunction`) — done.
- **T13**: omit `IDisposable` (cu-13) — done by absence.
- **T14**: pick raw-string `"""..."""` (C# 11+) vs verbatim `@"..."`
  for the four LoadSource literals based on codegen target language
  version — done at codegen taste-decision time.
- **T15**: pick `Console.WriteLine` vs `_output.WriteLine` for six
  `print(...)` mappings — done at codegen taste-decision time.

## 4. Research Findings

none required — every construct's target shape is verbatim-derivable
from the ratified convspec at
`.codeconv/conversion-specs/test/engine/glp_engine_test.dart.md`
(source_sha256 `ba6d7b38ff34bd811a6ead5ef440929fb6c02eff1295f1906b68a37b7b4ac2eb`,
escalations: []), which in turn cites:
- SUT spec `.codeconv/conversion-specs/lib/engine/glp_engine.dart.md`
  (the GlpEngine ctor / LoadSource / RunGoalAsync shapes,
  ExecutionResult sealed-class shape, RtTerm alias resolution).
- `.codeconv/conversion-specs/lib/runtime/scheduler.dart.md`
  (ExecutionStatus PascalCase enum members).
- Test-exemplar carry-forward set: `boot_loader_test.dart.md`
  (setUp→ctor, isTrue→Assert.True with reason),
  `binding_pointer_test.dart.md` (group→class + final→var locals),
  `module_activation_test.dart.md` (named-required→named-call-site),
  `rpc_routing_test.dart.md` (triple-quoted GLP source→raw/verbatim).
- Microsoft Learn `Path.GetFullPath` + `FileInfo.FullName` +
  `xunit.Assert.True(bool, string)` + `xunit.Assert.NotNull` +
  `xunit.Assert.Contains(string, string)` + `xunit.Assert.NotEqual` +
  `Interpolated strings (Reference)` + `Named and Optional Arguments` +
  xunit.net `Shared Context between Tests` + `Capturing Output`.
- CLAUDE.md (the conversion-toolchain identity; out of scope for
  decision-content here).

Three idioms recorded as NEW in the convspec (already ratified):
- `rf-dart-expect-contains-substring-to-xunit-assert-contains`
- `rf-dart-expect-isnot-value-to-xunit-assert-notequal`
- `rf-dart-expect-istrue-to-xunit-asserttrue` extended with
  reason-with-interpolation pattern.

## 5. Consistency Pass

fixed — derived from convspec `.codeconv/conversion-specs/test/engine/glp_engine_test.dart.md`
(escalations: []). Every plan-construct above mirrors the convspec's
`constructs:` list one-to-one; the `conversion_units:` list in the
convspec maps one-to-one to cu-1..cu-13 above; the three Section 1
counts (97 lines, 5 test cases, 1 outer group) match direct source
inspection AND the convspec's introductory paragraph. The Section 2
target shapes (PascalCase method names, ARG-FLIP for `Assert.Contains`
and `Assert.NotEqual`, ExecutionStatus.Failed PascalCase,
`Path.GetFullPath` preferred over `FileInfo.FullName`, named-arg
call-site preserved) all mirror the convspec verbatim. Threading-model
question INHERITED from heap_fcp.dart.md per FR-013 (no double-escalation).
`Console.WriteLine` vs `ITestOutputHelper.WriteLine` is a documented
taste decision at codegen time; both are authoritative per the
convspec's rationale. No NEW research, no spec-drift.

## 6. Escalations

None.
