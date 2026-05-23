---
path: test/bytecode/fairness_scheduler_loop_test.dart
cycle_group_id: 112
scc_siblings: []
generated_at: 2026-05-21T16:29:44Z
source_sha256: 15f2e909b0910c86fb911fa212eb0812ca811197cf88c5e6cc0c7d9cf981eba8
schema_version: 1
---

# Conversion Plan: test/bytecode/fairness_scheduler_loop_test.dart

## 1. Source Analysis

The Dart source (29 lines, sha256 `15f2e909…1eba8`) is a single-test xUnit-bound
fairness probe for the bytecode scheduler. Inspection of the actual file:

- **Imports (lines 1-6)**:
  - `import 'package:test/test.dart';` — test framework.
  - `import 'package:glp_runtime/bytecode/opcodes.dart';` — supplies `Label`,
    `TailStep`.
  - `import 'package:glp_runtime/bytecode/runner.dart';` — supplies
    `BytecodeProgram`, `BytecodeRunner`.
  - `import 'package:glp_runtime/runtime/runtime.dart';` — supplies
    `GlpRuntime`, `GoalRef`.
  - `import 'package:glp_runtime/runtime/machine_state.dart';` — co-located
    machine-state types (no symbol named in this file, kept for transitive
    visibility per the sibling SUT specs).
  - `import 'package:glp_runtime/runtime/scheduler.dart';` — supplies
    `Scheduler`.
- **Top-level `void main()` (line 8)** — Dart test-registration root; no
  parameters; void return.
- **Single `test(...)` registration (line 9)** — display name `'Two goals
  alternate due to 26-step tail yield'`; synchronous closure body.
- **Six `final` locals in the closure (lines 10, 12, 16, 17, 22, 25)**:
  - `final rt = GlpRuntime();` — runtime construction, no args.
  - `final p = BytecodeProgram([Label('LOOP'), TailStep('LOOP')]);` —
    positional list literal of two heterogeneous-but-related opcode
    constructors passed as the sole positional arg.
  - `final runner = BytecodeRunner(p);` — positional arg.
  - `final sched = Scheduler(rt: rt, runner: runner);` — two named args.
  - `final ran1 = sched.drain(maxCycles: 2);` — one named arg.
  - `final ran2 = sched.drain(maxCycles: 2);` — one named arg, second call.
- **Two `enqueue` calls (lines 19, 20)** — property-chain method call shape
  `rt.gq.enqueue(GoalRef(N, p.labels['LOOP']!))` with Dart `!` non-null
  assertion on a map indexer.
- **Two `expect` calls (lines 23, 26)** — bare-value matcher form
  `expect(actual, [1, 2], reason: '...')`; second is the same shape with a
  different `reason:` text.

No async surface, no `Future` / `Stream` / isolate / `Completer` / `Timer`,
no `late` / `mixin` / `extension` / generics / sealed/abstract / bitwise /
shift. Null-safety nuance fires exactly once (the `!` on
`p.labels['LOOP']`).

## 2. Dart → C#/.NET Conversion Plan

Each construct is enumerated mirroring the ratified convspec block §`constructs:`.

- `import 'package:test/test.dart';` → drop the directive; emit `using Xunit;`
  at file scope (REUSE `rf-dart-package-test-to-dotnet-xunit`).
- `import 'package:glp_runtime/bytecode/opcodes.dart';` +
  `import 'package:glp_runtime/bytecode/runner.dart';` → collapse into a
  single `using <RootNs>.Bytecode;` directive (per the
  `bytecode/opcodes.dart` and `bytecode/runner.dart` SUT specs;
  REUSE `rf-dart-internal-package-import-to-csharp-using`).
- `import 'package:glp_runtime/runtime/runtime.dart';` +
  `import 'package:glp_runtime/runtime/machine_state.dart';` +
  `import 'package:glp_runtime/runtime/scheduler.dart';` → collapse into a
  single `using <RootNs>.Runtime;` directive (per the `runtime.dart`,
  `machine_state.dart`, `scheduler.dart` SUT specs;
  REUSE `rf-dart-internal-package-import-to-csharp-using`).
- `void main() { test('...', () { ... }); }` → eliminate `void main()`; lift
  the `test(...)` call into a single
  `[Fact(DisplayName = "Two goals alternate due to 26-step tail yield")]`-
  attributed `public void TwoGoalsAlternateDueTo26StepTailYield()` instance
  method on `public class FairnessSchedulerLoopTest`
  (REUSE `rf-dart-test-main-to-xunit-class-with-facts`). Synchronous `void`,
  not `async Task` — no async surface.
- `final rt = GlpRuntime();` → `var rt = new GlpRuntime();`
  (REUSE `rf-dart-final-local-to-csharp-var`).
- `final p = BytecodeProgram([Label('LOOP'), TailStep('LOOP')]);` →
  `var p = new BytecodeProgram(new[] { new Label("LOOP"), new TailStep("LOOP") });`
  (REUSE `rf-dart-list-literal-of-constructors-to-csharp-array-init`;
  element type `Op` inferred from the SUT opcode hierarchy; substitute
  `new List<Op> { ... }` or C#-12 collection-expression `[...]` if the
  `bytecode/runner.dart` SUT spec records a non-array parameter type).
- `final runner = BytecodeRunner(p);` → `var runner = new BytecodeRunner(p);`
  (REUSE `rf-dart-final-local-to-csharp-var`).
- `final sched = Scheduler(rt: rt, runner: runner);` →
  `var sched = new Scheduler(rt: rt, runner: runner);`
  (REUSE `rf-dart-named-arg-to-csharp-named-arg`; named-arg colon-form is
  identical in both languages; parameter names stay camelCase per C# Coding
  Conventions).
- `rt.gq.enqueue(GoalRef(1, p.labels['LOOP']!));` →
  `rt.Gq.Enqueue(new GoalRef(1, p.Labels["LOOP"]!));`
  (REUSE `rf-dart-property-chain-method-call-to-csharp`; PascalCased public
  member names per SUT specs; Dart `!` runtime-throw is faithfully encoded
  at compile-time-nullability level by C# `!`, with the runtime-throw
  semantics carried by the SUT-side `IDictionary<TKey,TValue>` indexer's
  `KeyNotFoundException` behaviour).
- `rt.gq.enqueue(GoalRef(2, p.labels['LOOP']!));` →
  `rt.Gq.Enqueue(new GoalRef(2, p.Labels["LOOP"]!));`
  (same rule, second goal id).
- `final ran1 = sched.drain(maxCycles: 2);` →
  `var ran1 = sched.Drain(maxCycles: 2);`
  (REUSE `rf-dart-final-local-to-csharp-var` + `rf-dart-named-arg-to-csharp-named-arg`;
  method name PascalCased per `scheduler.dart` SUT spec).
- `expect(ran1, [1, 2], reason: 'each goal runs until its first yield');` →
  `// each goal runs until its first yield` (inline comment carrying the
  Dart `reason:` text) immediately above
  `Assert.Equal(new[] { 1, 2 }, ran1);`
  (REUSE `rf-dart-list-equality-to-xunit-assertequal-collection` composed
  with the `reason:`-to-inline-comment routing from
  `rf-dart-expect-bare-value-int-to-xunit-assert-equal`;
  EXPECTED-FIRST per the smoke_test.dart-recorded swap; element type
  `int` inferred — switch to `long` / `GoalId[]` if the scheduler SUT spec
  records that width).
- `final ran2 = sched.drain(maxCycles: 2);` →
  `var ran2 = sched.Drain(maxCycles: 2);` (same shape).
- `expect(ran2, [1, 2], reason: 'after re-enqueue, order remains FIFO');` →
  `// after re-enqueue, order remains FIFO` then
  `Assert.Equal(new[] { 1, 2 }, ran2);` (same rule).
- `void main() {}` registration root → no C# equivalent — xUnit discovery is
  attribute-driven; the Dart `main()` is dropped entirely.

## 3. Decomposed Task Units

- T1. Emit `using Xunit;` file-level directive (replaces `package:test`).
- T2. Emit `using <RootNs>.Bytecode;` collapsing the two bytecode imports.
- T3. Emit `using <RootNs>.Runtime;` collapsing the three runtime imports.
- T4. Declare `public class FairnessSchedulerLoopTest` (no base class).
- T5. Declare `[Fact(DisplayName = "Two goals alternate due to 26-step tail yield")] public void TwoGoalsAlternateDueTo26StepTailYield()`.
- T6. Body line 1: `var rt = new GlpRuntime();`.
- T7. Body lines 2-5: `var p = new BytecodeProgram(new[] { new Label("LOOP"), new TailStep("LOOP") });`.
- T8. Body line 6: `var runner = new BytecodeRunner(p);`.
- T9. Body line 7: `var sched = new Scheduler(rt: rt, runner: runner);`.
- T10. Body line 8: `rt.Gq.Enqueue(new GoalRef(1, p.Labels["LOOP"]!));`.
- T11. Body line 9: `rt.Gq.Enqueue(new GoalRef(2, p.Labels["LOOP"]!));`.
- T12. Body line 10: `var ran1 = sched.Drain(maxCycles: 2);`.
- T13. Body line 11: emit inline comment `// each goal runs until its first yield`.
- T14. Body line 12: `Assert.Equal(new[] { 1, 2 }, ran1);` (EXPECTED-FIRST).
- T15. Body line 13: `var ran2 = sched.Drain(maxCycles: 2);`.
- T16. Body line 14: emit inline comment `// after re-enqueue, order remains FIFO`.
- T17. Body line 15: `Assert.Equal(new[] { 1, 2 }, ran2);` (EXPECTED-FIRST).
- T18. Drop `void main()` entirely (no xUnit equivalent).

## 4. Research Findings

none required — all seven `research_finding_id`s in the convspec
(`rf-dart-package-test-to-dotnet-xunit`,
`rf-dart-internal-package-import-to-csharp-using`,
`rf-dart-test-main-to-xunit-class-with-facts`,
`rf-dart-final-local-to-csharp-var`,
`rf-dart-list-literal-of-constructors-to-csharp-array-init`,
`rf-dart-named-arg-to-csharp-named-arg`,
`rf-dart-property-chain-method-call-to-csharp`,
`rf-dart-list-equality-to-xunit-assertequal-collection`) are KB-reused from
sibling specs per FR-012 / SC-007. The compound `reason:`-to-inline-comment
nuance composes the collection-equality finding with the
`rf-dart-expect-bare-value-int-to-xunit-assert-equal` routing already
recorded in the sibling `test/conformance/fairness_26_test.dart.md`. No
WebSearch/WebFetch invocation needed.

## 5. Consistency Pass

- T1 fixed — derived from convspec construct
  `dart.package_test.import_directive` (REUSE
  `rf-dart-package-test-to-dotnet-xunit`).
- T2, T3 fixed — derived from convspec construct
  `dart.internal_package_import.same_package` (REUSE
  `rf-dart-internal-package-import-to-csharp-using`); two-namespace collapse
  authoritative per the cited SUT specs.
- T4, T5, T18 fixed — derived from convspec construct
  `dart.test_file.void_main_as_test_registration_root` (REUSE
  `rf-dart-test-main-to-xunit-class-with-facts`); class-name and
  method-name mappings literally restated.
- T6, T8, T12, T15 fixed — derived from convspec construct
  `dart.local_var.final_typed_constructor_invocation` (REUSE
  `rf-dart-final-local-to-csharp-var`).
- T7 fixed — derived from convspec construct
  `dart.list_literal.struct_elements_as_ctor_arg` (REUSE
  `rf-dart-list-literal-of-constructors-to-csharp-array-init`); the SUT
  parameter-type-driven array-vs-List substitution is recorded verbatim.
- T9 fixed — derived from convspec construct
  `dart.method_call.named_argument` (REUSE
  `rf-dart-named-arg-to-csharp-named-arg`); colon-form preserved verbatim;
  parameter names stay camelCase.
- T10, T11 fixed — derived from convspec construct
  `dart.property_chain.field_method_call` (REUSE
  `rf-dart-property-chain-method-call-to-csharp`); the Dart-`!`-throws vs
  C#-`!`-compile-time nuance is recorded as a load-bearing carry-forward
  satisfied by the SUT-side `IDictionary` indexer's
  `KeyNotFoundException`.
- T13, T14, T16, T17 fixed — derived from convspec construct
  `dart.package_test.expect_value_equals_matcher_list_literal_with_reason`
  (REUSE `rf-dart-list-equality-to-xunit-assertequal-collection` composed
  with `rf-dart-expect-bare-value-int-to-xunit-assert-equal`); EXPECTED-FIRST
  swap and inline-comment `reason:` routing are both recorded verbatim.

## 6. Escalations

None.
