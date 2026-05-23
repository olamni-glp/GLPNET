---
path: test/multiagent/output_kernel_test.dart
cycle_group_id: 154
scc_siblings: []
generated_at: 2026-05-21T16:50:26Z
source_sha256: c9a1c6ecd561b433029f9130f9006732643ca9915524c539454e9d3e09753a06
schema_version: 1
---

# Conversion Plan: test/multiagent/output_kernel_test.dart

## 1. Source Analysis

Direct inspection of `glp_runtime_net/test/multiagent/output_kernel_test.dart` (96 lines):

- **Header doc-comment** (line 1): `/// Tests for '_output'/1 kernel and send_to_user/1 GLP predicate.`
- **Imports** (lines 2–4):
  - `import 'dart:io';` — used only for `File(...).absolute.path` path-resolution inside `setUp`.
  - `import 'package:test/test.dart';` — `group` / `setUp` / `test` / `expect` / `isTrue` matcher.
  - `import 'package:glp_runtime/engine/glp_engine.dart';` — the single SUT import; provides `GlpEngine`, transitively `GlpRuntime.outputCallback`, `ExecutionResult`.
- **`void main()` entrypoint** (line 6) contains exactly TWO sibling `group(...)` calls — no top-level statements outside the groups.
- **Group 1: `'_output kernel'`** (lines 7–49) — declares `late GlpEngine engine;` + `late List<String> outputLines;`, registers a `setUp` (4 statements: construct `GlpEngine` with `rootSelfGlpPath:` named-arg and `..strictTypes = false` cascade; `outputLines = []`; install `outputCallback` arrow lambda), and three async `test(...)` callbacks:
  1. `'prints a constant'` — loads triple-quoted GLP fixture invoking `'_output'(hello)`, awaits `engine.runGoal('test')`, asserts `result.succeeded` true and `outputLines == ['hello']`.
  2. `'prints a struct'` — fixture invokes `'_output'(msg(alice, bob, text(hi)))`, expects `['msg(alice, bob, text(hi))']`.
  3. `'prints a list'` — fixture invokes `'_output'([a, b, c])`, expects `['[a, b, c]']`.
- **Group 2: `'send_to_user'`** (lines 51–94) — identical `late` fields and textually-identical `setUp` body; two async `test(...)` callbacks:
  4. `'consumes a ground stream and prints each term'` — fixture defines `send_to_user/1` GLP procedure inline (per the leading comment), calls `send_to_user([hello, world, msg(a, b)])`, expects `['hello', 'world', 'msg(a, b)']`.
  5. `'waits for stream elements to become ground'` — fixture binds tail variable after issuing the call (`send_to_user([hello | Tail?]), Tail = [world]`), expects `['hello', 'world']`.
- **No `tearDown`, no nested groups, no `skip:` arguments, no `dart:async` import, no `Stream`, no `Completer`, no isolate construction.**

The file's purpose is to verify the GLP `_output/1` body-kernel and the `send_to_user/1` GLP predicate via a Dart-side capture buffer installed through `GlpRuntime.outputCallback`.

## 2. Dart → C#/.NET Conversion Plan

Mirroring the ratified convspec's `constructs:` block 1:1:

- **`dart.import.dart_io`** → drop `import 'dart:io';`, replace with file-scope `using System.IO;` (covers `System.IO.Path.GetFullPath`). NO `FileInfo` constructed in the target — the Dart `File('../programs/self.glp').absolute.path` collapses to a single `Path.GetFullPath("../programs/self.glp")` call (carve-out (i) from rf-dart-file-absolute-path-to-csharp-path-getfullpath because the Dart code does NOT retain the `File` handle). Cross-file PROJECT-WIRING invariant: the test .csproj MUST set `WorkingDirectory` so the relative `../programs/self.glp` still resolves to the repo-root `programs/self.glp` (CWD parity is not preserved automatically — xUnit runner default CWD is the test assembly's `bin/Debug/` folder).
- **`dart.package_test.import_directive`** → drop `import 'package:test/test.dart';`, replace with `using Xunit;` at file scope. Co-add `using System.Collections.Generic;` (for `List<string>`) and `using System.Threading.Tasks;` (for `Task`, LOAD-BEARING because every `[Fact]` here is `async Task`-returning). Framework pin: xUnit, carried verbatim from the multiagent-test batch (rf-dart-package-test-to-dotnet-xunit).
- **`dart.package_test.import_sut_relative_package`** → drop `import 'package:glp_runtime/engine/glp_engine.dart';`, replace with `using <RootNs>.Engine;` (and optionally `using <RootNs>.Runtime;` if `GlpRuntime` is in a sibling namespace per `lib/runtime/runtime.dart.md`). The single SUT import reaches `GlpEngine` (ctor + `LoadSource` + `RunGoalAsync` + `Runtime` + `StrictTypes`), `GlpRuntime.OutputCallback`, and `ExecutionResult`. Per-pair `<ProjectReference>` wiring is a project-skeleton concern.
- **`dart.package_test.main_entrypoint`** → drop `void main()` entirely. xUnit discovers `[Fact]` methods by reflection; no per-file entrypoint emitted. The two sibling `group(...)` calls inside `main` become two sibling test classes at the file's namespace scope.
- **`dart.package_test.group_block_with_setUp`** → each `group(label, body)` becomes ONE `public class <Label>Tests`:
  - `group('_output kernel', ...)` → `public class OutputKernelTests` (leading underscore stripped + PascalCased; original label preserved verbatim via `[Trait("Group", "_output kernel")]`).
  - `group('send_to_user', ...)` → `public class SendToUserTests` (`[Trait("Group", "send_to_user")]`).
  Each class holds two private fields, a constructor (the setUp body), and N `[Fact]` methods (3 for `OutputKernelTests`, 2 for `SendToUserTests`). No nested groups, no `tearDown`, so no `IDisposable.Dispose`. Per-test fresh-instance lifecycle is observably identical to Dart `setUp`.
- **`dart.package_test.late_field_in_group`** → each `late T x;` becomes `private T _x = null!;` on the test class:
  - `late GlpEngine engine;` → `private GlpEngine _engine = null!;`
  - `late List<String> outputLines;` → `private List<string> _outputLines = null!;` (Dart `String` → C# `string` per cached rf-dart-string-to-csharp-string).
  `null!` is the non-nullable "assigned-later" idiom that mirrors Dart `late` semantics under xUnit's constructor-per-test guarantee.
- **`dart.package_test.setUp_block`** → the Dart `setUp(() { ... })` body maps to the class CONSTRUCTOR body (NOT `[SetUp]`/`[TestInitialize]`). Emitted per class:
  ```
  public <ClassName>()
  {
      var path = Path.GetFullPath("../programs/self.glp");
      _engine = new GlpEngine(path);
      _engine.StrictTypes = false;
      _outputLines = new List<string>();
      _engine.Runtime.OutputCallback = line => _outputLines.Add(line);
  }
  ```
  The cascade `..strictTypes = false` is UNROLLED into a separate `_engine.StrictTypes = false;` statement. Both classes carry textually-identical constructor bodies; no shared base class refactor (recorded as optional forward note, NOT a conversion decision).
- **`dart.expression.file_absolute_path_resolution`** → `File('../programs/self.glp').absolute.path` → `Path.GetFullPath("../programs/self.glp")` (carve-out (i) — no transient `FileInfo` object; the Dart code does not retain the `File` handle). Single-quote → double-quote string-literal conversion. CWD-sensitivity is preserved on both sides (resolves against `Directory.GetCurrentDirectory()` / `Directory.current`).
- **`dart.expression.cascade_operator_assignment`** → the Dart `..strictTypes = false` cascade UNROLLS into two statements: `_engine = new GlpEngine(path);` followed by `_engine.StrictTypes = false;`. C# has no cascade operator; the object-initializer form `new GlpEngine(path) { StrictTypes = false }` is recorded as an acceptable alternative but the unroll form is the default for consistency with the rest of the constructor body. Footgun explicitly addressed (Dart cascades return the receiver, NOT the assignment value as C# `=` does).
- **`dart.runtime.runtime_outputcallback_assign`** → `engine.runtime.outputCallback = (line) => outputLines.add(line);` → `_engine.Runtime.OutputCallback = line => _outputLines.Add(line);` per cached idioms (rf-dart-camelcase-field-to-csharp-pascalcase-property + rf-dart-arrow-lambda-to-csharp-lambda + rf-dart-void-function-question-to-csharp-action-nullable). The lambda parameter `line` is inferred `string` from the target `Action<string>?`. Captures `_outputLines` from `this`; per-test-fresh-instance ensures each test's callback writes to a NEW `List<string>`.
- **`dart.package_test.test_call_async`** → each `test('<label>', () async { ... })` becomes a `public async Task <PascalLabel>()` method decorated with `[Fact(DisplayName = "<original label>")]`. Method names:
  - `'prints a constant'` → `PrintsAConstant`
  - `'prints a struct'` → `PrintsAStruct`
  - `'prints a list'` → `PrintsAList`
  - `'consumes a ground stream and prints each term'` → `ConsumesAGroundStreamAndPrintsEachTerm`
  - `'waits for stream elements to become ground'` → `WaitsForStreamElementsToBecomeGround`
  Each body emits: `LoadSource(<rawStr>);` → `var result = await _engine.RunGoalAsync("test");` → `Assert.True(result.Succeeded);` → `Assert.Equal(new List<string> { ... }, _outputLines);`. The `-Async` suffix on `RunGoalAsync` follows Microsoft Framework Design Guidelines as pinned by the SUT spec.
- **`dart.expression.final_local_variable_with_initializer_await`** → `final result = await engine.runGoal('test');` → `var result = await _engine.RunGoalAsync("test");` (cached rf-dart-final-local-to-csharp-var-local). The C# enclosing method MUST be `async Task` for the `await` to compile; this is satisfied by the test-method shape above.
- **`dart.method.engine_load_source_invocation`** → `engine.loadSource('''...''');` → `_engine.LoadSource("""..."""");` per cached rf-dart-method-to-csharp-method (lowerCamelCase → PascalCase). The optional `filename` second parameter (per SUT spec, default `null`) is OMITTED — no call site supplies it. The `bool` return is DISCARDED at every call site (statement-as-expression). IDE0058 ("Expression value is never used") is a project-skeleton-level `.editorconfig` concern; the conversion does not emit `_ =`.
- **`dart.string.triple_quoted_literal`** → each `'''<newline>...'''` Dart fixture becomes a C# 11 raw-string literal `"""<newline>..."""`. Codegen MUST emit the closing `"""` at column 0 so the literal payload is byte-identical to the Dart source. The leading-newline-is-ignored rule applies identically on both sides; the GLP fixture bytes are preserved verbatim.
- **`dart.package_test.expect_isTrue_matcher`** → `expect(result.succeeded, isTrue);` → `Assert.True(result.Succeeded);` (cached rf-dart-expect-isTrue-to-xunit-assert-true). `succeeded` getter → `Succeeded` property (rf-dart-getter-to-csharp-property).
- **`dart.package_test.expect_list_equality`** → each `expect(outputLines, [<elements>]);` flips argument order to `Assert.Equal(new List<string> { <elements> }, _outputLines);` (expected-first per xUnit; Dart `expect(actual, equals(expected))` is actual-first). xUnit `Assert.Equal<T>(IEnumerable<T>, IEnumerable<T>)` is element-wise + order-sensitive — matches Dart `equals` over a `List<String>`. Bare-list-literal auto-wrap (Dart sugar for `equals(...)`) is semantically identical to the explicit form. The five concrete expectations:
  - `['hello']` → `new List<string> { "hello" }`
  - `['msg(alice, bob, text(hi))']` → `new List<string> { "msg(alice, bob, text(hi))" }`
  - `['[a, b, c]']` → `new List<string> { "[a, b, c]" }`
  - `['hello', 'world', 'msg(a, b)']` → `new List<string> { "hello", "world", "msg(a, b)" }`
  - `['hello', 'world']` → `new List<string> { "hello", "world" }`
  GLP-printer-format invariant: the expected string bytes (e.g. `[a, b, c]`, `msg(alice, bob, text(hi))`) are exactly what the GLP printer produces; preserving the expectations verbatim relies on the converted C# GLP printer (compiler/glp_printer.dart's port) producing byte-identical output — a CROSS-FILE INVARIANT recorded here.

## 3. Decomposed Task Units

- T1: Emit file-scope using directives (`using Xunit;`, `using System.IO;`, `using System.Collections.Generic;`, `using System.Threading.Tasks;`, `using <RootNs>.Engine;` and optional `using <RootNs>.Runtime;`).
- T2: Emit namespace declaration mirroring the test/multiagent path (e.g. `namespace <RootNs>.Test.Multiagent;`).
- T3: Emit `public class OutputKernelTests` with `[Trait("Group", "_output kernel")]` attribute, two private fields (`_engine`, `_outputLines`), and constructor body unrolling the setUp.
- T4: Emit three `[Fact(DisplayName="...")]` `public async Task` methods in `OutputKernelTests`: `PrintsAConstant`, `PrintsAStruct`, `PrintsAList`.
- T5: Emit `public class SendToUserTests` with `[Trait("Group", "send_to_user")]` attribute, two private fields (`_engine`, `_outputLines`), and constructor body textually identical to `OutputKernelTests`'.
- T6: Emit two `[Fact(DisplayName="...")]` `public async Task` methods in `SendToUserTests`: `ConsumesAGroundStreamAndPrintsEachTerm`, `WaitsForStreamElementsToBecomeGround`.
- T7: Emit per-method `LoadSource("""...""")` calls with raw-string fixture payloads at column 0, byte-identical to the Dart `'''...'''` payload.
- T8: Emit per-method `var result = await _engine.RunGoalAsync("test");` followed by `Assert.True(result.Succeeded);`.
- T9: Emit per-method `Assert.Equal(new List<string> { ... }, _outputLines);` with element lists matching the Dart expectations verbatim (expected-first argument order).

## 4. Research Findings

none required — every construct in this plan is verbatim-derivable from the ratified convspec (`source_sha256` match), the SUT spec `lib/engine/glp_engine.dart.md` (signatures for `LoadSource` / `RunGoalAsync` / `Runtime` / `StrictTypes` / `ExecutionResult.Succeeded`), and the cached idioms cited in the convspec's `research_finding_id` rows (xUnit `[Fact]` / `async Task` / `Assert.Equal` argument order; `Path.GetFullPath` carve-out; cascade unroll; raw-string literal). No new official-docs lookup is required for this artefact.

## 5. Consistency Pass

- xUnit framework choice: fixed — derived from convspec rf-dart-package-test-to-dotnet-xunit and the sibling-test batch pin (mad_error_handling_test.dart.md and successors).
- Two-sibling-groups → two-sibling-classes (NOT nested-group encoding): fixed — derived from convspec dart.package_test.group_block_with_setUp + the global_send_test.dart.md / mad_scenarios_test.dart.md precedent.
- `'_output kernel'` underscore-stripped class name + `[Trait]` preservation: fixed — derived from convspec dart.package_test.group_block_with_setUp name-mangling nuance.
- `..strictTypes = false` cascade unroll (NOT object-initializer): fixed — derived from convspec dart.expression.cascade_operator_assignment (object-initializer recorded as alternative only).
- `File(...).absolute.path` → `Path.GetFullPath(...)` carve-out (i): fixed — derived from convspec dart.expression.file_absolute_path_resolution + rf-dart-file-absolute-path-to-csharp-path-getfullpath carve-out rule between (i) and (ii) (sibling project_linker_test.dart.md takes (ii)).
- `async Task` `[Fact]` shape (NOT `void`, NOT bare `Task`): fixed — derived from convspec dart.package_test.test_call_async + dart.expression.final_local_variable_with_initializer_await.
- `RunGoalAsync` `-Async` suffix: fixed — derived from convspec + SUT spec `lib/engine/glp_engine.dart.md`.
- Triple-quoted fixtures → raw-string literals at column 0: fixed — derived from convspec dart.string.triple_quoted_literal + Microsoft Learn raw-string leading-newline / common-indent rule.
- `Assert.Equal` argument-order flip (expected-first): fixed — derived from convspec dart.package_test.expect_list_equality argument-order footgun nuance.
- CWD/`WorkingDirectory` cross-file invariant: fixed — derived from convspec dart.import.dart_io nuance + dart.expression.file_absolute_path_resolution nuance (recorded as project-wiring invariant, not an in-file decision).
- GLP-printer byte-identical output cross-file invariant: fixed — derived from convspec dart.package_test.expect_list_equality GLP-printing-format nuance (preserved verbatim because the GLP printer is converted separately under its own convspec).

## 6. Escalations

None.
