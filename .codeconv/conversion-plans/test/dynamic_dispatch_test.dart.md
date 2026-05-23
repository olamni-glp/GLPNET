---
path: test/dynamic_dispatch_test.dart
cycle_group_id: 123
scc_siblings: []
generated_at: 2026-05-21T16:50:04Z
source_sha256: ca1921987062da8ddae88f306a4001b46751dc870b0fcf0d2ad133a9c529d2a4
schema_version: 1
---

# Conversion Plan: test/dynamic_dispatch_test.dart

## 1. Source Analysis

`glp_runtime_net/test/dynamic_dispatch_test.dart` is a 209-line `package:test`
integration suite exercising the full dynamic-module-dispatch chain
`caller → channel → serve → _activate → procedure`. It contains a single
`void main()` entrypoint whose body is structured as follows:

- Imports: 11 lines — `dart:io`, `package:test/test.dart`, and 9
  `package:glp_runtime/...` imports (two of which carry a `show`-clause
  restricting the imported symbol set to `setPreludeUnitClauseSource` and
  `setPreludeEnvironmentSource` respectively).
- `void main()` body opens with a conditional prelude-source bootstrap that
  reads `../programs/self.glp` from disk (if present) and threads the source
  into two process-global setter functions
  (`setPreludeUnitClauseSource(source); setPreludeEnvironmentSource(source);`).
- A `main`-scope `final ddDir = '../programs/tests/dynamic_dispatch';`
  is then read inside the four end-to-end tests via Dart closure capture.
- Two sibling top-level `group(...)` blocks follow:
  - `group('serve/2', ...)` containing one test that constructs a
    `GlpEngine` and asserts `engine.serveBytecode.labels.containsKey('serve/2')`.
  - `group('end-to-end dispatch', ...)` containing four tests:
    1. `'activate module and dispatch double(5, F) → F = 10'` — full
       per-test allocation of `GlpCompiler` + `GlpRuntime` + `GlpEngine`,
       compile-and-merge of `math_service.glp` with `self.glp`,
       `activateModule(...)` on a `GlpChannelHandle`, drain with
       `Scheduler.drainWithStatus(maxCycles: 300)` asserted
       `ExecutionStatus.succeeded`, allocate fresh writer via
       `final (fWriter, _) = rt.heap.allocateVariable();`,
       construct `StructTerm('double', [ConstTerm(5), VarRef(fWriter)])`,
       `handle.send(goal)`, `for (final g in woken) { rt.gq.enqueue(g); }`,
       second drain (`maxCycles: 10000`), dereference via
       `rt.heap.dereference(VarRef(fWriter))`, cast via `(fValue as ConstTerm)`,
       and assert `.value` equals 10.
    2. `'activate module and dispatch triple(4, F) → F = 12'` — structurally
       identical to (1) but with `'triple'` functor, `ConstTerm(4)`, and
       expected `.value == 12`. Result variable is `final result = ...;`
       (single-assignment) versus `var result = ...;` in test 1.
    3. `'unknown goal does not crash (fallback)'` — constructs
       `StructTerm('nonexistent', [ConstTerm(42)])`, asserts the post-drain
       `result.status` is `ExecutionStatus.succeeded` (no crash from the
       `_activate` fallback path). Does NOT allocate a writer; does NOT
       call `heap.dereference`.
    4. `'single_export module: dispatch inc(7, F) → F = 8'` — uses
       `single_export.glp` as the module source (not `math_service.glp`),
       `moduleName: 'single'`, functor `'inc'`, expected `.value == 8`.

The file uses ZERO `async`/`await`/`Future` constructs — every `test()`
callback is synchronous. The file uses ZERO `setUp`/`setUpAll`/`tearDown`
hooks — each test re-allocates its own runtime state.

## 2. Dart → C#/.NET Conversion Plan

Each construct below is reproduced verbatim from the RATIFIED convspec
`.codeconv/conversion-specs/test/dynamic_dispatch_test.dart.md`. The plan
preserves the spec's per-construct decisions; no construct is altered.

### C1. `dart.dart_io.import_directive`

- Source: `import 'dart:io';`
- Decision: drop the Dart import; emit file-scope `using System.IO;`.
- Carries `File.ReadAllText(string)` and `File.Exists(string)` static-API
  surface (sync variants only — the source uses `readAsStringSync` /
  `existsSync`).
- Nuance: relative-path literals (`'../programs/self.glp'`) are preserved
  verbatim — `dart:io` and `System.IO.File` both accept forward-slash paths
  on Windows; no `Path.Combine` normalisation at this level.

### C2. `dart.package_test.import_directive`

- Source: `import 'package:test/test.dart';`
- Decision: drop the Dart import; emit `using Xunit;`. Project-wide xUnit
  pinning (cached idiom — `rf-dart-package-test-import-to-xunit-using`).
- Namespace: `<RootNs>.Test` (mirrors the Dart `test/` directory).

### C3. `dart.package_under_test.import_directive_multi_runtime`

- Source: ten `package:glp_runtime/...` imports (two with `show` clauses).
- Decision: collapse to the C# `using` directives covering the SUT-spec-
  pinned namespaces:
  - `using <RootNs>.Compiler;` — `GlpCompiler`, `PartialEvaluator`
    (host static class for `SetPreludeUnitClauseSource`).
  - `using <RootNs>.Analysis.TypeChecker;` — `TypeEnvironmentBuilder`
    (host static class for `SetPreludeEnvironmentSource`).
  - `using <RootNs>.Engine;` — `GlpEngine`.
  - `using <RootNs>.Runtime;` — `GlpRuntime`, `Scheduler`,
    `ExecutionStatus`, `GlpActivation` (host for `ActivateModule`),
    `GlpChannelHandle`, `Term`/`StructTerm`/`ConstTerm`/`VarRef`,
    machine-state surface.
  - `using <RootNs>.Bytecode;` — `BytecodeProgram`.
- Nuance: Dart `show <symbol>` has no C# `using` counterpart — the
  restriction is dropped (semantically lossless: no namespace collisions
  exist). Top-level Dart functions become `public static` methods on
  host static classes (names owned by SUT specs).

### C4. `dart.package_test.main_entrypoint_with_pre_group_setup`

- Source: `void main()` body containing the conditional prelude bootstrap
  + `final ddDir = ...;` + two `group(...)` calls.
- Decision: eliminate `main` entirely. Emit:
  - A `static DynamicDispatchTest()` static constructor on the test
    class running the prelude bootstrap once per AppDomain:
    `if (File.Exists("../programs/self.glp")) { var source = File.ReadAllText("../programs/self.glp"); PartialEvaluator.SetPreludeUnitClauseSource(source); TypeEnvironmentBuilder.SetPreludeEnvironmentSource(source); }`.
  - A `private const string DdDir = "../programs/tests/dynamic_dispatch";`
    field hoisted to class scope (replaces the closure-captured Dart
    `final ddDir` local).
- Nuance: static constructor is the simplest faithful translation of
  Dart `main`'s per-process one-shot init (matches "runs exactly once
  before the first test of the class executes"). `IAssemblyFixture` would
  also work but is overkill for a single-file init. The `if (existsSync)`
  guard is preserved verbatim in C# via `if (File.Exists(...))`.

### C5. `dart.package_test.group_block_two_sibling_groups`

- Source: two sibling `group(...)` blocks — `'serve/2'` (1 test) and
  `'end-to-end dispatch'` (4 tests).
- Decision: flatten to a single PascalCase test class
  `DynamicDispatchTest` containing five `[Fact]` methods. Each method
  carries `[Trait("Group", "<group label>")]` (cached idiom
  `rf-dart-package-test-group-to-xunit-class`) — two distinct trait
  values: `"serve/2"` and `"end-to-end dispatch"`. The PascalCased method
  names: `Serve2CompilesAndHasLabel`,
  `ActivateModuleAndDispatchDouble5FEquals10`,
  `ActivateModuleAndDispatchTriple4FEquals12`,
  `UnknownGoalDoesNotCrashFallback`,
  `SingleExportModuleDispatchInc7FEquals8`.
- Nuance: each `[Fact(DisplayName = "<original label>")]` preserves the
  Dart label verbatim INCLUDING the literal Unicode right-arrow `→`
  (e.g. `"activate module and dispatch double(5, F) → F = 10"`). The
  `.cs` file MUST be UTF-8 so the glyph survives the build.

### C6. `dart.package_test.test_call_simple_synchronous`

- Source: five `test('<label>', () { ... })` calls — all synchronous
  closures, no `skip:` / no `timeout:` / no `async`.
- Decision: each becomes a `public void` instance method decorated with
  `[Fact(DisplayName = "<label>")]` + the group-specific `[Trait]`. NO
  method is `async Task` (no `async`/`await`/`Future` anywhere in the
  source).
- Nuance: no shared instance state — every test allocates its own
  per-method locals (xUnit constructs a fresh class instance per
  `[Fact]`, providing the isolation guarantee).

### C7. `dart.dart_io.file_constructor`

- Source: `File('../programs/self.glp')`,
  `File('../programs/self.glp').absolute.path`,
  `File('$ddDir/math_service.glp')`, `File('$ddDir/single_export.glp')`.
- Decision: Dart `File(<path>)` is an INSTANCE constructor; C#
  `System.IO.File` is a STATIC class with no constructor. Eliminate the
  instance and pass the path string directly to each static method:
  - `File.ReadAllText(path)` for `readAsStringSync`.
  - `File.Exists(path)` for `existsSync`.
  - `Path.GetFullPath(path)` for `File(...).absolute.path`.
- Nuance: hoist the path string instead of capturing a `File` instance
  (e.g. `var rootSelfGlpPath = "../programs/self.glp";` referenced twice
  in the static constructor). Use C# interpolated strings for the
  `ddDir` substitution: `File.ReadAllText($"{DdDir}/math_service.glp")`.

### C8. `dart.dart_io.file_exists_sync`

- Source: `rootSelfGlp.existsSync()`.
- Decision: `File.Exists("../programs/self.glp")` returning `bool` (sync,
  non-throwing on permission errors).

### C9. `dart.dart_io.file_read_as_string_sync`

- Source: four callsites reading `self.glp`, `math_service.glp`,
  `single_export.glp` (UTF-8, default encoding).
- Decision: `File.ReadAllText(path)` returning `string`. Sync variant
  (NOT `ReadAllTextAsync`). UTF-8 default matches Dart's
  `readAsStringSync` UTF-8 default; BOM auto-skipping matches.

### C10. `dart.top_level_function_call.set_prelude_unit_clause_source_set_prelude_environment_source`

- Source: `setPreludeUnitClauseSource(source); setPreludeEnvironmentSource(source);`
- Decision: emit `PartialEvaluator.SetPreludeUnitClauseSource(source);` and
  `TypeEnvironmentBuilder.SetPreludeEnvironmentSource(source);` inside
  the static constructor body.
- Nuance: process-global state mutation — SUT specs pin storage as
  `private static` field on each host class. Idempotent under same-value
  re-invocation; overwriting on different value (not exercised here).

### C11. `dart.local_var.string_literal_for_relative_dir_path`

- Source: `final ddDir = '../programs/tests/dynamic_dispatch';`
- Decision: `private const string DdDir = "../programs/tests/dynamic_dispatch";`
  as a class-scope constant. PascalCase per .NET naming convention.
- Nuance: `const` (vs `static readonly`) because the value is a compile-
  time string literal. Closure capture across Dart `test()` closures
  becomes class-field access across xUnit methods.

### C12. `dart.local_var.final_constructor_invocation_glp_compiler`

- Source: `final compiler = GlpCompiler();`
- Decision: `var compiler = new GlpCompiler();` (method-local).
  Per-method allocation preserved (four allocations across four
  end-to-end tests).

### C13. `dart.local_var.final_constructor_invocation_glp_runtime`

- Source: `final rt = GlpRuntime();`
- Decision: `var rt = new GlpRuntime();` (method-local, fresh heap +
  goal-queue + registries per test, trivially safe under the inherited
  single-owning-context invariant from heap_fcp.dart.md escalations[0]).

### C14. `dart.constructor_call.glp_engine_with_named_arg_root_self_glp_path`

- Source: `GlpEngine(rootSelfGlpPath: File('../programs/self.glp').absolute.path);`
- Decision: `new GlpEngine(rootSelfGlpPath: Path.GetFullPath("../programs/self.glp"))`.
  Named-argument syntax preserved for clarity. Used twice in the file (test
  1 and inside each end-to-end test that reads `engine.serveBytecode`).
- Nuance: `Path.GetFullPath` uses `Directory.GetCurrentDirectory` as the
  base for relative paths — matches Dart's `File.absolute.path` contract.
  Non-nullable `string` parameter (no `string?`).

### C15. `dart.method_call.glp_compiler_compile_string`

- Source: three callsites compiling `self.glp` / `mathSource` / `source`.
- Decision: `compiler.Compile(<string>)` returning `BytecodeProgram`.
  PascalCase method rename. Sync; no `await`.

### C16. `dart.method_call.bytecode_program_merge`

- Source: four callsites `compiler.compile(<src>).merge(rootSelfBytecode)`.
- Decision: `compiler.Compile(<src>).Merge(rootSelfBytecode)` — fluent
  chain preserved. Returns a NEW `BytecodeProgram` (immutable merge per
  lib/bytecode/runner.dart.md).

### C17. `dart.member_access.glp_engine_serve_bytecode_getter`

- Source: `engine.serveBytecode` (5×).
- Decision: `engine.ServeBytecode` — read-only PascalCase property
  (`{ get; }` only).

### C18. `dart.member_access.bytecode_program_labels_contains_key`

- Source: `engine.serveBytecode.labels.containsKey('serve/2')`.
- Decision: `engine.ServeBytecode.Labels.ContainsKey("serve/2")` —
  `Dictionary<string, long>.ContainsKey` (or
  `IReadOnlyDictionary<string, long>.ContainsKey` if the SUT property
  is read-only-typed). `int` PC offsets widened to `long` per the cached
  `rf-dart-int-to-csharp-long-width` precedent.

### C19. `dart.constructor_call.struct_term_with_const_term_var_ref_args`

- Source: four `StructTerm(<functor>, [<ConstTerm>, <VarRef>])` constructions.
- Decision: `new StructTerm("<functor>", new List<Term> { new ConstTerm(<lit>), new VarRef(<addr>) })`
  for the binary-args cases (`double`, `triple`, `inc`) and
  `new StructTerm("nonexistent", new List<Term> { new ConstTerm(42) })`
  for the unary fallback case.
- Nuance: `List<Term>` accepts both `ConstTerm` and `VarRef` (both
  inherit `Term` per terms.dart.md sealed-leaf decision). Reference-
  identity preserved (sealed C# classes).

### C20. `dart.constructor_call.const_term_int_literal`

- Source: `ConstTerm(5)`, `ConstTerm(4)`, `ConstTerm(42)`, `ConstTerm(7)`.
- Decision: `new ConstTerm(5)`, `new ConstTerm(4)`, `new ConstTerm(42)`,
  `new ConstTerm(7)`. The `int` literal boxes into `object? Value`.

### C21. `dart.constructor_call.var_ref_long_address`

- Source: `VarRef(fWriter)` (3×).
- Decision: `new VarRef(fWriter)` — `Term` subtype carrying a `long`
  heap address. `fWriter` is the destructured first element of the
  `(long, long)` tuple returned by `rt.Heap.AllocateVariable()`.

### C22. `dart.dart_3_destructuring_pattern.heap_allocate_variable`

- Source: `final (fWriter, _) = rt.heap.allocateVariable();` (3×).
- Decision: `var (fWriter, _) = rt.Heap.AllocateVariable();` —
  C# 7+ tuple deconstruction with `_` discard.
- Nuance: SUT lib/runtime/heap_fcp.dart.md MUST declare
  `AllocateVariable()` returning a positional tuple `(long Writer, long Reader)`;
  the destructured local names (`fWriter`) are method-local and need
  not match the tuple-element names.

### C23. `dart.method_call.heap_dereference_var_ref`

- Source: `rt.heap.dereference(VarRef(fWriter))` (3×).
- Decision: `rt.Heap.Dereference(new VarRef(fWriter))` returning `Term`.
  PascalCase method rename. Result is asserted to be a `ConstTerm` via
  `Assert.IsType<ConstTerm>(...)` followed by `((ConstTerm)fValue).Value`.

### C24. `dart.member_access.glp_channel_handle_send`

- Source: `final woken = handle.send(goal);` + `handle.send(goal)` (4×).
- Decision: `var woken = handle.Send(goal);` returning
  `List<GoalRef>`. Mutates `_writerAddr` in place per
  lib/runtime/glp_activation.dart.md.

### C25. `dart.statement.for_in_loop_over_woken_iterable`

- Source: `for (final g in woken) { rt.gq.enqueue(g); }` (4×).
- Decision: `foreach (var g in woken) { rt.Gq.Enqueue(g); }`. Cached
  idiom (`rf-dart-for-in-final-to-csharp-foreach-var`).

### C26. `dart.method_call.goal_queue_enqueue`

- Source: `rt.gq.enqueue(g);`
- Decision: `rt.Gq.Enqueue(g);` (or `rt.GoalQueue.Enqueue(g);` —
  property name owned by runtime.dart.md; working assumption `Gq`).
  `GoalRef` is a `readonly record struct` per machine_state.dart.md.

### C27. `dart.function_call.activate_module_named_args`

- Source: `activateModule(rt: rt, serveBytecode: serveBytecode, moduleBytecode: <bc>, moduleName: '<name>')` (4×).
- Decision: `GlpActivation.ActivateModule(rt: rt, serveBytecode: serveBytecode, moduleBytecode: <bc>, moduleName: "<name>")`
  returning `GlpChannelHandle`. Top-level Dart function -> static method
  on `GlpActivation` static host class per lib/runtime/glp_activation.dart.md.
  `moduleName` varies: `"math_service"` (3×) and `"single"` (1×).

### C28. `dart.member_access.glp_runtime_glp_channels_contains_key`

- Source: `rt.glpChannels.containsKey('math_service')`.
- Decision: `rt.GlpChannels.ContainsKey("math_service")` —
  `Dictionary<string, GlpChannelHandle>.ContainsKey` (or
  `IReadOnlyDictionary` per the SUT type pinning).

### C29. `dart.constructor_call.scheduler_with_rt_named_arg`

- Source: `final scheduler = Scheduler(rt: rt);` (4×).
- Decision: `var scheduler = new Scheduler(rt: rt);` — `runner` /
  `runners` / `traceSink` optional params default to null (NOT used
  here — distinct from rpc_routing_test which used `traceSink`).

### C30. `dart.method_call.scheduler_drain_with_status_max_cycles`

- Source: five callsites with `maxCycles: 300 / 5000 / 10000`.
- Decision: `scheduler.DrainWithStatus(maxCycles: <int>)` returning
  `DrainResult` (sync). Named-argument syntax preserved. `int`
  parameter — no `long` widening (bounded-recursion budget, not a
  heap address).
- Nuance: `var result = ...;` reassignable; the `'unknown goal'` test
  uses `final result = ...;` single-assignment — both translate to
  `var result = ...;` (no `readonly` modifier on locals).

### C31. `dart.member_access.drain_result_status`

- Source: `result.status`.
- Decision: `result.Status` — read-only `ExecutionStatus` property
  (`{ get; }` only) per scheduler.dart.md.

### C32. `dart.enum_member_access.execution_status_succeeded`

- Source: `ExecutionStatus.succeeded` (3×).
- Decision: `ExecutionStatus.Succeeded` — PascalCase enum member per
  scheduler.dart.md.

### C33. `dart.package_test.expect_equals`

- Source: assertions over `bool` (via `isTrue`) and `equals(...)` over
  `ExecutionStatus` and boxed `int`.
- Decision (per-callsite):
  - `expect(rt.glpChannels.containsKey('math_service'), isTrue)` →
    `Assert.True(rt.GlpChannels.ContainsKey("math_service"));`
  - `expect(engine.serveBytecode.labels.containsKey('serve/2'), isTrue)` →
    `Assert.True(engine.ServeBytecode.Labels.ContainsKey("serve/2"));`
  - `expect(result.status, equals(ExecutionStatus.succeeded))` (3×) →
    `Assert.Equal(ExecutionStatus.Succeeded, result.Status);`
  - `expect((fValue as ConstTerm).value, equals(10))` →
    `Assert.Equal(10, ((ConstTerm)fValue).Value);`
    (and the 12, 8 sibling callsites).
- Nuance: argument-order flip (`expect(actual, equals(expected))` →
  `Assert.Equal(expected, actual)`) — cached footgun. Boxed-int equality
  preserved by `EqualityComparer<object>.Default` (`int`-boxed payload
  per the SUT spec for system_predicates / heap_fcp arith results;
  `Assert.Equal(10, ...)` matches the source's `int 10` literal).

### C34. `dart.package_test.expect_equals_with_reason`

- Source: `expect(fValue, isA<ConstTerm>(), reason: 'F should be bound to a constant (10)');`
- Decision: `Assert.IsType<ConstTerm>(fValue); // reason: F should be bound to a constant (10)`
  — xUnit's `Assert.IsType<T>` has no message overload; comment-
  retention strategy preserves the documentation provenance (cached
  idiom `rf-dart-expect-with-reason-to-xunit-comment-or-asserttrue`).

### C35. `dart.package_test.expect_isA_runtime_type`

- Source: `expect(fValue, isA<ConstTerm>(), reason: ...);` and
  `expect(fValue, isA<ConstTerm>());` (×2).
- Decision: `Assert.IsType<ConstTerm>(fValue);` (3×). Strict exact-type
  match is correct because `ConstTerm` is a sealed Term leaf per
  terms.dart.md — `IsType` and `IsAssignableFrom` are observably
  equivalent under the sealed-leaf invariant.
- Nuance: if a future conversion exercises `isA<Term>` (base class),
  codegen MUST switch to `Assert.IsAssignableFrom<Term>(actual)` —
  this file does not.

### C36. `dart.cast_operator.as_term_subtype`

- Source: `(fValue as ConstTerm).value` (3×).
- Decision: `((ConstTerm)fValue).Value` — parenthesised C# cast
  (throws `InvalidCastException` on mismatch, matching Dart `as`'s
  throw-on-mismatch contract).
- Nuance: LOAD-BEARING FOOTGUN — Dart `as T` and C# `as T` have
  INVERTED semantics on mismatch (Dart throws; C# returns null).
  Codegen MUST translate Dart `<expr> as T` to C# `(T)<expr>`
  (parenthesised cast), NEVER to C# `as`.

### C37. `dart.member_access.const_term_value_getter`

- Source: `(fValue as ConstTerm).value`.
- Decision: `((ConstTerm)fValue).Value` — PascalCase property
  returning `object?` per terms.dart.md.

## 3. Decomposed Task Units

- T1. Emit C# file header + `using` directives — C1, C2, C3. Done.
- T2. Declare namespace `<RootNs>.Test` + class `DynamicDispatchTest` — C5. Done.
- T3. Emit `private const string DdDir = "../programs/tests/dynamic_dispatch";` field — C11. Done.
- T4. Emit `static DynamicDispatchTest()` static constructor with conditional prelude bootstrap — C4, C7, C8, C9, C10. Done.
- T5. Emit `Serve2CompilesAndHasLabel` `[Fact]` method (group `"serve/2"`) — C6, C13, C14, C17, C18, C33. Done.
- T6. Emit `ActivateModuleAndDispatchDouble5FEquals10` `[Fact]` method — C6, C12, C13, C14, C15, C16, C17, C22, C19, C20, C21, C24, C25, C26, C27, C28, C29, C30, C31, C32, C33, C34, C35, C36, C37. Done.
- T7. Emit `ActivateModuleAndDispatchTriple4FEquals12` `[Fact]` method — same construct set as T6 minus the `Assert.True(GlpChannels.ContainsKey)` and minus the reason-carrying assertion. Done.
- T8. Emit `UnknownGoalDoesNotCrashFallback` `[Fact]` method — C6, C12, C13, C14, C15, C16, C17, C19, C20, C24, C25, C26, C27, C29, C30, C31, C32, C33 (no writer allocation, no `Heap.Dereference`, no cast). Done.
- T9. Emit `SingleExportModuleDispatchInc7FEquals8` `[Fact]` method — same construct set as T6 with `moduleName: "single"` and `single_export.glp`. Done.
- T10. Apply `[Trait("Group", "serve/2")]` to T5 and `[Trait("Group", "end-to-end dispatch")]` to T6/T7/T8/T9 — C5. Done.
- T11. Apply `[Fact(DisplayName = "<verbatim Dart label including → arrow>")]` to each method; ensure `.cs` UTF-8 encoding — C5, C6. Done.

## 4. Research Findings

none required.

All idioms in this file resolve via the RATIFIED convspec, which itself
records first-seen idioms with their authoritative basis (Dart official
docs, Microsoft Learn, xunit.net docs) and cached idioms from sibling
exemplars (`module_activation_test.dart.md`, `rpc_routing_test.dart.md`,
`binding_pointer_test.dart.md`). The convspec embeds the authoritative
citations directly per-construct; this plan inherits them verbatim
without re-research. The inherited threading-model ruling
(heap_fcp.dart.md escalations[0] — single-owning-context, option A) is
preserved without re-escalation per FR-013.

## 5. Consistency Pass

fixed — derived from `.codeconv/conversion-specs/test/dynamic_dispatch_test.dart.md`
(RATIFIED convspec, source_sha256 `ca1921987062da8ddae88f306a4001b46751dc870b0fcf0d2ad133a9c529d2a4`).

Cross-checks performed:
- Source sha256 in plan front-matter equals convspec `source_sha256` —
  PASS (both `ca1921...d2a4`).
- Every construct C1..C37 above mirrors a `constructs[]` entry in the
  convspec YAML block (37 constructs total; ordering matches the
  convspec's top-down enumeration). PASS.
- Threading-model inheritance preserved (no re-escalation at this
  file's boundary) — PASS, matches convspec prose §"Threading-model
  inheritance".
- `conversion_units: [test/DynamicDispatchTest.cs]` (single target
  C# file) preserved — PASS.
- `escalations: []` preserved — PASS (no open escalations introduced
  by this file).
- Cycle membership: `cycle_group_id: 123, scc_siblings: []` per the
  prompt — recorded in plan front-matter.

## 6. Escalations

None.
