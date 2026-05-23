---
path: test/bytecode/arithmetic_test.dart
cycle_group_id: 111
scc_siblings: []
generated_at: 2026-05-21T16:39:45Z
source_sha256: 6c536bfb10977451326c73eaa01a2b0537043da88cfc65d6f3c36fe05b39c11a
schema_version: 1
---

# Conversion Plan: test/bytecode/arithmetic_test.dart

## 1. Source Analysis

The Dart file `test/bytecode/arithmetic_test.dart` (342 lines) is a `package:test` test file exercising the GLP body-kernel arithmetic surface and the end-to-end `:=` system predicate.

Top-level structure:

- 8 imports: `package:test/test.dart`; six `package:glp_runtime/...` imports spanning three sub-paths (`compiler/compiler.dart`; `bytecode/runner.dart`; five `runtime/*.dart` — `runtime.dart`, `machine_state.dart`, `terms.dart`, `body_kernels.dart`, `scheduler.dart`); `dart:io`.
- `void main()` body containing:
  - a `late BytecodeProgram stdlibProg;` file-scoped variable;
  - one `setUpAll(() { ... })` that compiles `../programs/self.glp` once via `File(...).readAsStringSync()` + `GlpCompiler().compile(...)` and prints an instruction count;
  - two sibling `group(...)` blocks:
    - `'Arithmetic via := system predicate'` — 8 `test(...)` callbacks (kernel direct-call tests for `_add`/3, `_sub`/3, `_mul`/3, `_div`/3, `_div`/3 abort-on-div-by-zero, `_neg`/2, `_sqrt`/2, and a registration-completeness check of 25 kernel keys);
    - `'End-to-end := system predicate'` — 3 `test(...)` callbacks (compile-and-merge assertions; SRSW user-clause compile; full execute-and-bind drive of `compute_sum(Z?) :- Z := 5 + 3.` through `BytecodeRunner` + `Scheduler.drain` + `CallEnv` + `GoalRef`).

Surface inventory (line-by-line scan):

- 8 calls to `rt.heap.allocateVariable()` returning positional records `(int, int)` destructured as `(writer, reader)` — three of those use `_` discard for the unused reader.
- 10 calls to `rt.heap.bindVariableConst(<writer>, <intLiteral>)` with literals `5, 3, 10, 4, 7, 6, 15, 0, 42, 16`.
- 7 `rt.bodyKernels.lookup('<name>', <arity>)` calls + 1 `expect(kernel, isNotNull, reason: '...')` + 7 `kernel!(rt, [VarRef..., VarRef..., VarRef...])` invocations.
- 25 `expect(rt.bodyKernels.has('<name>', <arity>), isTrue)` calls.
- 11 `BodyKernelResult.success`/`.abort` comparisons via `expect(result, equals(...))`.
- 8 `print(...)` callsites — six with `$`-interpolation and two with plain or `\n`-escaped strings; one contains the UTF-8 glyph `✓`.
- 2 explicit `GlpCompiler()` constructions plus 1 implicit one in `setUpAll`; 1 `BytecodeRunner(mergedProg)`; 1 `Scheduler(rt: rt, runner: runner)` (named args); 1 `CallEnv(args: {0: VarRef(resultWriter)})` (map-literal arg); 1 `GoalRef(goalId, entryPc!)`.
- 1 C-style `for (var id = 10000; id < rt.nextGoalId; id++) { ... }` loop iterating spawned goals.
- 1 `sched.drain(maxCycles: 100, debug: true, debugOutput: true)` named-arg invocation.
- 1 `userProg.merge(stdlibProg)` + 3 `mergedProg.labels.containsKey('<key>')` + 1 `mergedProg.labels['compute_sum/1']` indexer access (then `entryPc!`).
- 1 `rt.heap.isWriterBound(resultWriter)` boolean check guarding an `if/else` whose `else` branch is `fail('Result variable should be bound after execution');`.
- `expect(value, isA<ConstTerm>())` + `expect((value as ConstTerm).value, equals(<num>))` three-step matcher pattern repeated 5 times (sub/mul/div/neg/sqrt + the end-to-end Z=8 case).

No `async`/`Future`/`Stream`/`Completer`/`Timer`/`isolate`/`mixin`/`extension`/generics-decl/sealed/abstract/bitwise surface. Null-safety surface limited to three `!` non-null assertions (`kernel!`, `entryPc!`, plus the implicit one inside `value as ConstTerm`).

## 2. Dart → C#/.NET Conversion Plan

Each row below mirrors a convspec construct row (same construct_key, same target decision; abbreviated, no re-derivation):

- `dart.package_test.import_directive` → drop `import 'package:test/test.dart';`; emit `using Xunit;`. KB-reuse `rf-dart-package-test-to-dotnet-xunit`. `.csproj` wiring OUT OF SCOPE.
- `dart.internal_package_import.same_package` → collapse seven `package:glp_runtime/...` imports into THREE `using` directives: `using <RootNs>.Compiler;`, `using <RootNs>.Bytecode;`, `using <RootNs>.Runtime;` (Runtime brings in `runtime.dart`, `machine_state.dart`, `terms.dart`, `body_kernels.dart`, `scheduler.dart`). KB-reuse `rf-dart-internal-package-import-to-csharp-using`. Brings into scope: `GlpCompiler` (Compiler); `BytecodeProgram`, `BytecodeRunner` (Bytecode); `GlpRuntime`, `BodyKernelResult`, `VarRef`, `ConstTerm`, `CallEnv`, `GoalRef`, `Scheduler` (Runtime).
- `dart.import_directive.dart_io_to_csharp_using_system_io` → emit `using System.IO;` (covers `File`, `Path`). KB-reuse `rf-dart-dart-io-to-csharp-system-io`. Use STATIC `File.ReadAllText(...)`; route the `../programs/self.glp` relative path via `Path.Combine(AppContext.BaseDirectory, "..", "programs", "self.glp")` (load-bearing CWD-resolution nuance — Dart resolves against test-runner CWD `glp_runtime/`, C# resolves against `dotnet test`'s process CWD).
- `dart.test_file.void_main_as_test_registration_root` → eliminate `void main()`; emit outer `public class ArithmeticTest`; lift the two `group(...)` blocks into two nested `[Collection("ArithmeticPrelude")]` test classes: `ArithmeticViaAssignSystemPredicate` (carries `[Trait("Group", "Arithmetic via := system predicate")]`) and `EndToEndAssignSystemPredicate` (`[Trait("Group", "End-to-end := system predicate")]`). KB-reuse `rf-dart-test-main-to-xunit-class-with-facts` + `rf-dart-package-test-group-to-xunit-class`. Identifier-legalisation: `:=` glyph → `Assign` (per `lib/runtime/system_predicates.dart.md` SUT spec).
- `dart.package_test.setUpAll_lifecycle_hook` → lift to `public class StdlibProgFixture { public BytecodeProgram StdlibProg { get; } public StdlibProgFixture() { var stdlibSource = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "programs", "self.glp")); StdlibProg = new GlpCompiler().Compile(stdlibSource); } }`; add `[CollectionDefinition("ArithmeticPrelude")] public class ArithmeticPreludeCollection : ICollectionFixture<StdlibProgFixture> { }`. The two nested test classes accept the fixture via ctor injection. The diagnostic `print(...)` in `setUpAll` is OMITTED (non-load-bearing). KB-reuse `rf-dart-setupall-to-xunit-class-fixture`.
- `dart.local.late_typed_variable_declaration` → eliminate `late BytecodeProgram stdlibProg;` at file scope; replaced by `public BytecodeProgram StdlibProg { get; }` get-only auto-property on `StdlibProgFixture`. KB-reuse `rf-dart-late-variable-to-csharp-init-only-property`.
- `dart.test_callback.parameterless_arrow_or_block` → each `test('<name>', () { ... })` → `[Fact(DisplayName = "<name>")] public void <PascalCaseMethod>() { ... }` on the appropriate nested class. Identifier renaming per convspec table: `Add3BodyKernelExecutesDirectly`, `Sub3BodyKernel`, `Mul3BodyKernel`, `Div3BodyKernel`, `Div3BodyKernelAbortsOnDivisionByZero`, `Neg2BodyKernel`, `SqrtKernel2BodyKernel`, `AllStandardBodyKernelsAreRegistered`, `AssignGlpCompilesAndMergesCorrectly`, `UserProgramWithAssignCompilesCorrectlyWithSRSW`, `ZAssign5Plus3ExecutesAndBindsZTo8`. DisplayName preserves the exact original Dart glyph sequence (incl. `:=`, `+`, `/`, `.`, spaces). KB-reuse `rf-dart-test-callback-to-xunit-method-body`.
- `dart.record_destructuring.positional_pair` → `var (xWriter, xReader) = rt.Heap.AllocateVariable();` (and `var (resultWriter, _) = rt.Heap.AllocateVariable();` for discard cases). KB-reuse `rf-dart-record-destructuring-to-csharp-valuetuple-deconstruction`.
- `dart.local.final_typed_constructor_invocation` → `final <name> = <Ctor>(...)` → `var <name> = new <Ctor>(...);` for `GlpRuntime`, `GlpCompiler`, `BytecodeRunner(mergedProg)`, `Scheduler(rt: rt, runner: runner)`. Named-args preserved via C# `name: value`. KB-reuse `rf-dart-final-local-to-csharp-var` + `rf-dart-constructor-invocation-implicit-new-to-csharp-new` + `rf-dart-named-arg-to-csharp-named-arg`.
- `dart.method_call.heap_writer_const_bind` → `rt.heap.bindVariableConst(<w>, <int>);` → `rt.Heap.BindVariableConst(<w>, <int>);` (PascalCase member rename; integer-literal width per SUT spec). KB-reuse `rf-dart-camel-to-csharp-pascal-method-rename`.
- `dart.constructor_invocation.implicit_new_varref` → `VarRef(<int>)` → `new VarRef(<int>)`; kernel-arg list `[VarRef(...), VarRef(...), VarRef(...)]` → `new[] { new VarRef(...), new VarRef(...), new VarRef(...) }`. KB-reuse `rf-dart-constructor-invocation-implicit-new-to-csharp-new` + `rf-dart-list-literal-of-constructors-to-csharp-array-init`.
- `dart.method_call.body_kernels_lookup_then_invoke` → `rt.BodyKernels.Lookup("<name>", <arity>)`; `expect(kernel, isNotNull, reason: '...')` → `Assert.NotNull(kernel); // <msg>`; `kernel!(rt, [...])` → `kernel!(rt, new[] { ... })` (delegate-call form per SUT `BodyKernel` typedef → C# `delegate`). KB-reuse `rf-dart-expect-isnotnull-to-xunit-assertnotnull` + `rf-dart-bang-null-assertion-to-csharp-null-forgiving`. Load-bearing semantic note: the preceding `Assert.NotNull` recovers Dart's `!` runtime-throw intent (C# `!` alone is compile-time-only).
- `dart.package_test.expect_equality_against_enum` → `expect(result, equals(BodyKernelResult.success))` → `Assert.Equal(BodyKernelResult.Success, result);` (EXPECTED-FIRST swap; enum-member PascalCase). KB-reuse `rf-dart-expect-equals-to-xunit-assertequal`.
- `dart.package_test.expect_get_value_then_isA` → compound `expect(value, isNotNull); expect(value, isA<ConstTerm>()); expect((value as ConstTerm).value, equals(<n>));` collapses to `var value = rt.Heap.GetValue(resultWriter); Assert.NotNull(value); var constValue = Assert.IsType<ConstTerm>(value); Assert.Equal(<n>, constValue.Value);`. Floating-point operands (`3.75` for div/3, `4.0` for sqrt/2) — spec default emits strict-equality; recommend the `Assert.Equal(double, double, int precision)` precision overload only where the SUT spec records the operand as computed-floating-point. KB-reuse `rf-dart-expect-isa-to-xunit-istype`.
- `dart.package_test.expect_boolean_predicate_istrue` → `expect(<bool-expr>, isTrue)` → `Assert.True(<bool-expr>)`; `expect(<bool-expr>, isTrue, reason: '<msg>')` → `Assert.True(<bool-expr>, "<msg>")` (xUnit `Assert.True(bool, string)` user-message overload). Chained method names PascalCase: `BodyKernels.Has("_add", 3)`, `Labels.ContainsKey("compute_sum/1")`, `Ops.Count > 0` (replaces Dart `ops.isNotEmpty` — portability over `using System.Linq;`). KB-reuse `rf-dart-expect-istrue-to-xunit-assert-true` + `rf-dart-camel-to-csharp-pascal-method-rename`.
- `dart.string_interpolation.simple_expression` → `'...${expr}...'` → `$"...{expr}..."`; `print(...)` → `_output.WriteLine($"...")` via xUnit `ITestOutputHelper` (injected into each nested test class ctor alongside the fixture). Interpolated members PascalCase per SUT (`stdlibProg.ops.length` → `_fixture.StdlibProg.Ops.Count`; `ran.length` → `ran.Count`). UTF-8 glyph `✓` and `\n` escape survive unchanged. KB-reuse `rf-dart-string-interpolation-to-csharp-dollar-string` + `rf-dart-print-to-xunit-itestoutputhelper-writeline`.
- `dart.package_test.fail_call` → `fail('Result variable should be bound after execution');` → `Assert.Fail("Result variable should be bound after execution");` in the `else` branch. KB-reuse `rf-dart-fail-call-to-xunit-assert-fail`.
- `dart.method_call.bytecode_program_merge_then_label_lookup` → `userProg.Merge(stdlibProg)`; `Assert.True(mergedProg.Labels.ContainsKey(":=/2"), "<msg>");` (string literal `":=/2"` is a legal C# string); `Assert.True(mergedProg.Labels.ContainsKey("hello/0"), "<msg>");`; for the indexer-then-bang path emit `Assert.True(mergedProg.Labels.ContainsKey("compute_sum/1"), "compute_sum/1 should exist"); var entryPc = mergedProg.Labels["compute_sum/1"];` — indexer-throws semantics match Dart `!` runtime-throw intent (C# `IDictionary<K,V>` indexer throws `KeyNotFoundException` on miss). KB-reuse `rf-dart-camel-to-csharp-pascal-method-rename` + `rf-dart-expect-istrue-to-xunit-assert-true` + `rf-dart-bang-null-assertion-to-csharp-null-forgiving`.
- `dart.method_call.gq_enqueue_with_goalref` → `rt.gq.enqueue(GoalRef(goalId, entryPc!));` → `rt.Gq.Enqueue(new GoalRef(goalId, entryPc));` (since `entryPc` is `int` after the indexer-throws lookup — `!` no longer needed). KB-reuse `rf-dart-property-chain-method-call-to-csharp`.
- `dart.method_call.scheduler_drain_with_debug` → `sched.drain(maxCycles: 100, debug: true, debugOutput: true)` → `sched.Drain(maxCycles: 100, debug: true, debugOutput: true)` (verbatim named-args; `Drain` PascalCase). KB-reuse `rf-dart-named-arg-to-csharp-named-arg`.
- `dart.for_loop.c_style_int_index` → `for (var id = 10000; id < rt.NextGoalId; id++) { var env = rt.GetGoalEnv(id); if (env != null) { _output.WriteLine($"  Goal {id} env: {env.ArgBySlot}"); } }`. Loop-variable width per SUT-recorded `int`/`long` decision. KB-reuse (FIRST-SEEN on this file) `rf-dart-c-style-for-loop-to-csharp-for-loop`.
- `dart.map_literal.int_to_varref_arg_map` → `CallEnv(args: {0: VarRef(resultWriter)})` → `new CallEnv(args: new Dictionary<int, VarRef> { [0] = new VarRef(resultWriter) });` (index-initialiser form). Inline `// Pass writer to head position Z` comment preserved verbatim. KB-reuse `rf-dart-map-literal-to-csharp-dictionary-initializer`.
- `dart.const_local.typed_int_literal` → `final goalId = 1;` → `var goalId = 1;`. KB-reuse `rf-dart-final-local-to-csharp-var`.

Additional file-level conversion units (mirror convspec `conversion_units:`):

- File rename: `arithmetic_test.dart` → `ArithmeticTest.cs`.
- Outer `public class ArithmeticTest` (container only; the `[Fact]`s live on the two nested classes).
- `StdlibProgFixture` (class fixture) + `ArithmeticPreludeCollection` (collection-definition marker).
- Two `[Collection("ArithmeticPrelude")]` nested classes, each with ctor `(StdlibProgFixture fixture, ITestOutputHelper output)` storing `_fixture` and `_output` fields.
- NO `void Main()` equivalent — xUnit discovery is attribute-driven.

## 3. Decomposed Task Units

- T1: Emit `using Xunit;` directive (drop `package:test` import). done
- T2: Emit `using System.IO;` directive (covers `File`, `Path`). done
- T3: Collapse seven `package:glp_runtime/...` imports into three `using <RootNs>.{Compiler|Bytecode|Runtime};` directives. done
- T4: Add `using Xunit.Abstractions;` for `ITestOutputHelper`. done
- T5: Emit `public class StdlibProgFixture` with get-only `BytecodeProgram StdlibProg { get; }` and ctor that compiles `Path.Combine(AppContext.BaseDirectory, "..", "programs", "self.glp")`. done
- T6: Emit `[CollectionDefinition("ArithmeticPrelude")] public class ArithmeticPreludeCollection : ICollectionFixture<StdlibProgFixture> { }`. done
- T7: Emit outer `public class ArithmeticTest` container (file-level shell, no members). done
- T8: Emit nested `[Collection("ArithmeticPrelude")] public class ArithmeticViaAssignSystemPredicate` with `[Trait("Group", "Arithmetic via := system predicate")]`, fixture+output ctor, and private `_fixture`/`_output` fields. done
- T9: Emit `[Fact(DisplayName = "add/3 body kernel executes directly")] public void Add3BodyKernelExecutesDirectly()` — allocate three vars, bind 5 + 3, lookup `_add`/3, `Assert.NotNull(kernel)`, invoke, `Assert.Equal(BodyKernelResult.Success, result)`, `Assert.IsType<ConstTerm>(value)`, `Assert.Equal(8, constValue.Value)`. done
- T10: Emit `Sub3BodyKernel` (operands 10/4, expected 6, `_` discard reader). done
- T11: Emit `Mul3BodyKernel` (operands 7/6, expected 42, `_` discard reader). done
- T12: Emit `Div3BodyKernel` (operands 15/4, expected 3.75; consider precision overload per SUT-recorded float width). done
- T13: Emit `Div3BodyKernelAbortsOnDivisionByZero` (operands 10/0, expected `BodyKernelResult.Abort`; NO post-result value check). done
- T14: Emit `Neg2BodyKernel` (unary; operand 42, expected -42). done
- T15: Emit `SqrtKernel2BodyKernel` (unary; operand 16, expected 4.0; consider precision overload). done
- T16: Emit `AllStandardBodyKernelsAreRegistered` — 25 `Assert.True(rt.BodyKernels.Has("_<name>", <arity>));` calls verbatim across binary arithmetic / unary / math functions / type conversions. done
- T17: Emit nested `[Collection("ArithmeticPrelude")] public class EndToEndAssignSystemPredicate` with `[Trait("Group", "End-to-end := system predicate")]`, fixture+output ctor, and private `_fixture`/`_output` fields. done
- T18: Emit `AssignGlpCompilesAndMergesCorrectly` — compile `_fixture.StdlibProg`-equivalent (re-read or reuse fixture); compile `hello.`; `userProg.Merge(stdlibProg)`; `Assert.True(mergedProg.Labels.ContainsKey(":=/2"), "<msg>");` + `Assert.True(mergedProg.Labels.ContainsKey("hello/0"), "<msg>");`. done
- T19: Emit `UserProgramWithAssignCompilesCorrectlyWithSRSW` — compile `compute_sum(Z?) :- Z := 5 + 3.`; `Assert.True(prog.Ops.Count > 0)`; `Assert.True(prog.Labels.ContainsKey("compute_sum/1"))`. done
- T20: Emit `ZAssign5Plus3ExecutesAndBindsZTo8` — full end-to-end: re-compile stdlib, compile user, merge, allocate result var, build `CallEnv(args: new Dictionary<int, VarRef> { [0] = new VarRef(resultWriter) })`, `rt.SetGoalEnv(goalId, env)`, look up `compute_sum/1` entry PC via `Assert.True(ContainsKey)` + `Labels[...]`, `rt.Gq.Enqueue(new GoalRef(goalId, entryPc))`, `sched.Drain(maxCycles: 100, debug: true, debugOutput: true)`, `for (var id = 10000; id < rt.NextGoalId; id++)` diagnostic loop, `rt.Heap.IsWriterBound(resultWriter)` guard, success branch asserts `Assert.IsType<ConstTerm>(value)` + `Assert.Equal(8, constValue.Value)`, else branch `Assert.Fail("Result variable should be bound after execution")`. done
- T21: Replace all `print(...)` callsites with `_output.WriteLine($"...")`; preserve UTF-8 glyph `✓` and `\n` escapes verbatim. done
- T22: Verify all `expect(actual, equals(expected))` are emitted EXPECTED-FIRST swapped (`Assert.Equal(expected, actual)`). done
- T23: Verify all 11 `[Fact]` `DisplayName` strings preserve the original Dart name verbatim (incl. `:=`, `+`, `/`, `.`, spaces, digits). done
- T24: Verify all `_` discard tuple destructurings are preserved in C# (`var (resultWriter, _) = ...`). done
- T25: Verify the `entryPc` lookup avoids the `IDictionary` `KeyNotFoundException` foot-gun by routing through `Assert.True(ContainsKey)` + indexer access (matches Dart `!` runtime-throw intent). done

## 4. Research Findings

none required (every construct is a KB-reuse from sibling specs — `bytecode/fairness_scheduler_loop_test.dart.md`, `bytecode/utility_instructions_test.dart.md`, `smoke_test.dart.md`, `glp_runtime_test.dart.md`, `mad_transactions_test.dart.md`, `moded_head_test.dart.md`, `module_hierarchy_test.dart.md`, `cssg_modules_test.dart.md`, `boot_loader_test.dart.md`, `well_typed_clause_test.dart.md`, `partial_evaluator_test.dart.md`, `test_channel_construction.dart.md` — per FR-012 / SC-007 KB-reuse decision order; the one FIRST-SEEN row `rf-dart-c-style-for-loop-to-csharp-for-loop` is authoritative-supported by both Dart `language/loops#for-loops` and Microsoft Learn `iteration-statements#the-for-statement` references quoted verbatim in the convspec).

## 5. Consistency Pass

fixed — derived from `.codeconv/conversion-specs/test/bytecode/arithmetic_test.dart.md` (convspec, ratified; `escalations: []`) and the SUT specs cited within: `.codeconv/conversion-specs/lib/compiler/compiler.dart.md`, `.codeconv/conversion-specs/lib/bytecode/runner.dart.md`, `.codeconv/conversion-specs/lib/runtime/runtime.dart.md`, `.codeconv/conversion-specs/lib/runtime/heap_fcp.dart.md`, `.codeconv/conversion-specs/lib/runtime/machine_state.dart.md`, `.codeconv/conversion-specs/lib/runtime/terms.dart.md`, `.codeconv/conversion-specs/lib/runtime/body_kernels.dart.md`, `.codeconv/conversion-specs/lib/runtime/scheduler.dart.md`, `.codeconv/conversion-specs/lib/runtime/goal_queue.dart.md`. Every construct row in §2 mirrors a convspec construct row 1:1; every task in §3 corresponds to one or more conversion units listed in the convspec `conversion_units:` block; no new decisions introduced beyond what is verbatim-derivable from those artifacts and from CLAUDE.md (file-naming, identifier-casing, glyph-legalisation).

## 6. Escalations

None.
