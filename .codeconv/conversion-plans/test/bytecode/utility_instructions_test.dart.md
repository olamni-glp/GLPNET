---
path: test/bytecode/utility_instructions_test.dart
cycle_group_id: 114
scc_siblings: []
generated_at: 2026-05-21T16:29:55Z
source_sha256: e17beae5607cc699811d9b457a38aed9c19cba512552730e9189fa854b1c5c63
schema_version: 1
---

# Conversion Plan: test/bytecode/utility_instructions_test.dart

## 1. Source Analysis

A 130-line `package:test` xUnit-shaped Dart test file that exercises two
utility bytecode opcodes (`nop` and `halt`) plus a `halt`-vs-`PROCEED`
equivalence sanity check. Inspected directly from
`glp_runtime_net/test/bytecode/utility_instructions_test.dart`
(sha256 `e17beae5607cc699811d9b457a38aed9c19cba512552730e9189fa854b1c5c63`).

Structural inventory (verbatim from the source file):

- **Imports (lines 1–8)**: `package:test/test.dart` plus seven internal
  `package:glp_runtime/...` imports — four under `runtime/`
  (`runtime.dart`, `cells.dart`, `machine_state.dart`, `scheduler.dart`)
  and three under `bytecode/` (`runner.dart`, `opcodes.dart`,
  `asm.dart`).
- **Top-level `void main()` (lines 10–130)** containing exactly three
  `test('<label>', () { ... });` calls — NO `group(...)`. Labels:
  1. `'Nop: no operation, just advances PC'` (lines 11–45)
  2. `'Halt: terminates execution'` (lines 47–78)
  3. `'Halt vs Proceed: both terminate'` (lines 80–129)
- **Per-test body (Test 1, lines 12–44)**:
  - `print('\n=== NOP TEST: No operation ===');`
  - `final rt = GlpRuntime();`
  - `final prog = BC.prog([BC.L('p/0'), BC.TRY(), BC.COMMIT(),
    BC.nop(), BC.nop(), BC.nop(), BC.PROCEED(), BC.L('p/0_end'),
    BC.SUSP()]);`
  - `final runner = BytecodeRunner(prog);`
  - `final sched = Scheduler(rt: rt, runner: runner);`
  - `const goalId = 100;`
  - `final env = CallEnv();`
  - `rt.setGoalEnv(goalId, env);`
  - `rt.gq.enqueue(GoalRef(goalId, prog.labels['p/0']!));`
  - `print('Starting p with 3 nops');`
  - `final ran = sched.drain(maxCycles: 10);`
  - `print('Goals executed: ${ran.length}');`
  - `expect(ran.length, 1, reason: 'Should execute through all nops and
    succeed');`
  - `print('✓ Nop instruction works correctly\n');`
- **Per-test body (Test 2, lines 48–77)**: same structure with a
  shorter program (`BC.halt()` between `BC.COMMIT()` and the
  `'p/0_end'` label), goalId `100`, env name `env`,
  `expect(ran.length, 1, reason: 'Should halt and terminate');`,
  and `print` narration tuned to "HALT TEST".
- **Per-test body (Test 3, lines 81–128)**: two parallel programs
  (`progHalt` with `BC.halt()` body, `progProceed` with `BC.PROCEED()`
  body — note: no `'p/0_end'` / `BC.SUSP()` tail in either), two
  parallel `BytecodeRunner` instances, two parallel `Scheduler`
  instances against TWO `GlpRuntime` instances (`rt` and `rt2`), two
  parallel `CallEnv` instances (`env1`, `env2`), two parallel `goalId`
  constants (`goalId1 = 100`, `goalId2 = 200`), two parallel
  `setGoalEnv` / `gq.enqueue` / `drain(maxCycles: 10)` calls, and two
  bare-form `expect(<list>.length, 1);` assertions (NO `reason:`).
- **Diagnostic narration**: 14 `print(...)` callsites total across the
  three tests, including two with `${ran.length}` interpolation.
- **UTF glyph**: `✓` (U+2713) appears five times in `print` strings;
  `\n` escape character appears six times.
- **Null-assertion `!`**: applied three times to
  `<prog>.labels['p/0']!` lookups.

No `async`/`await`/`Future`/`Stream`/`isolate`/`group`/`setUp`/
`tearDown` surface; every `[Fact]` body is purely synchronous.

## 2. Dart → C#/.NET Conversion Plan

Mirrors the ratified convspec (16 constructs, 0 escalations) verbatim.

1. **`dart.package_test.import_directive`**
   `import 'package:test/test.dart';` →
   `using Xunit;` + `using Xunit.Abstractions;` at file scope.
   (Idiom `rf-dart-package-test-to-dotnet-xunit`; framework choice
   settled batch-wide in `test/smoke_test.dart.md`. `.csproj`-level
   NuGet wiring is out of scope for this per-file artifact.)

2. **`dart.internal_package_import.same_package_multi`**
   The seven `import 'package:glp_runtime/...';` directives collapse
   into two C# `using` directives:
   - `using <RootNs>.Runtime;` (covers `GlpRuntime`, `CallEnv`,
     `GoalId`, `GoalRef`, `Scheduler`, `Cells` surface)
   - `using <RootNs>.Bytecode;` (covers `BytecodeRunner`, the `BC`
     static-class assembler facade, opcode types)
   (Idiom `rf-dart-internal-package-import-to-csharp-using`. Sub-
   namespace boundary owned by the SUT specs `lib/runtime/*.dart.md`
   and `lib/bytecode/*.dart.md`. No `using static BC;` needed —
   every callsite is already qualified `BC.<Member>(...)`.)

3. **`dart.test_file.void_main_with_multiple_test_calls_no_group`**
   `void main() { test(...); test(...); test(...); }` →
   delete `main`; lift each `test(...)` to a public
   `[Fact(DisplayName = "<original label>")]`-attributed instance
   method on a single `public class UtilityInstructionsTest`. Three
   `[Fact]` methods:
   - `[Fact(DisplayName = "Nop: no operation, just advances PC")]
     public void NopNoOperationJustAdvancesPC()`
   - `[Fact(DisplayName = "Halt: terminates execution")]
     public void HaltTerminatesExecution()`
   - `[Fact(DisplayName = "Halt vs Proceed: both terminate")]
     public void HaltVsProceedBothTerminate()`
   No `group(...)` ⇒ no inner-class topology. xUnit creates a fresh
   test-class instance per `[Fact]` (xunit.net "Shared Context
   between Tests"); no shared mutable state at the file level.
   (Idiom `rf-dart-test-main-to-xunit-class-with-facts`. "PC"
   stays uppercase per Microsoft "Capitalization Conventions"
   short-acronym rule.)

4. **`dart.core.print`**
   Every `print(<arg>)` (14 callsites) → `_output.WriteLine(<arg>);`.
   Test class gains:
   ```
   private readonly ITestOutputHelper _output;
   public UtilityInstructionsTest(ITestOutputHelper output)
   { _output = output; }
   ```
   `\n` escapes and the `✓` (U+2713) glyph survive verbatim in C#
   string literals (both languages parse `\n` identically and both
   accept UTF-16 code-unit sequences).
   (Idiom `rf-dart-print-to-xunit-itestoutputhelper-writeline`.
   xUnit does NOT capture `Console.Out`; `ITestOutputHelper` is the
   only path that surfaces under the test in VS Test Explorer /
   `dotnet test --logger trx` / Rider.)

5. **`dart.string.interpolation.simple_expression`**
   The two `'Goals executed: ${ran.length}'` callsites →
   `$"Goals executed: {ran.Count}"`. `.length` ⇒ `.Count` mapping
   owned by the dedicated construct below (construct 12).
   (Idiom `rf-dart-string-interpolation-to-csharp-dollar-string`.)

6. **`dart.local_var.final_typed_constructor_invocation`**
   Every `final <name> = <Ctor>(...);` → `var <name> = new
   <Ctor>(...);`. Named-argument constructor calls
   (`Scheduler(rt: rt, runner: runner)`) preserved verbatim with the
   `rt:` / `runner:` named-argument syntax. Specifically:
   - `final rt = GlpRuntime();` → `var rt = new GlpRuntime();`
   - `final rt2 = GlpRuntime();` → `var rt2 = new GlpRuntime();`
   - `final runner = BytecodeRunner(prog);` →
     `var runner = new BytecodeRunner(prog);`
   - `final runnerHalt = BytecodeRunner(progHalt);` →
     `var runnerHalt = new BytecodeRunner(progHalt);`
   - `final runnerProceed = BytecodeRunner(progProceed);` →
     `var runnerProceed = new BytecodeRunner(progProceed);`
   - `final sched = Scheduler(rt: rt, runner: runner);` →
     `var sched = new Scheduler(rt: rt, runner: runner);`
   - `final schedHalt = Scheduler(rt: rt, runner: runnerHalt);` →
     `var schedHalt = new Scheduler(rt: rt, runner: runnerHalt);`
   - `final schedProceed = Scheduler(rt: rt2, runner: runnerProceed);`
     → `var schedProceed = new Scheduler(rt: rt2, runner:
       runnerProceed);`
   - `final env = CallEnv();` → `var env = new CallEnv();`
   - `final env1 = CallEnv();` → `var env1 = new CallEnv();`
   - `final env2 = CallEnv();` → `var env2 = new CallEnv();`
   (Idiom `rf-dart-final-local-to-csharp-var`. `readonly` is the
   wrong mapping — fields only.)

7. **`dart.const_local.typed_int_literal`**
   `const goalId = 100;` → `const GoalId goalId = 100;` (and
   `const GoalId goalId1 = 100;`, `const GoalId goalId2 = 200;`).
   `GoalId` is the SUT-spec-owned alias from
   `lib/runtime/machine_state.dart.md` (`using GoalId = System.Int32;`
   per the SUT spec). `100` and `200` literals are well within `int`
   range; no truncation hazard. Local-name lowerCamelCase preserved
   per Microsoft's lenient local-name convention.
   (Idiom `rf-dart-const-local-typed-int-to-csharp-const`.)

8. **`dart.expression_statement.method_call_void_setter_pair`**
   Each `<rt>.setGoalEnv(<goalId>, <env>);` →
   `<rt>.SetGoalEnv(<goalId>, <env>);` — direct verbatim
   transliteration with the method name PascalCased. Three callsites:
   `rt.SetGoalEnv(goalId, env);`,
   `rt.SetGoalEnv(goalId1, env1);`,
   `rt2.SetGoalEnv(goalId2, env2);`.
   (Idiom `rf-dart-camel-to-csharp-pascal-method-rename`.)

9. **`dart.expression_statement.qualified_method_call_with_nested_constructor`**
   Each `<rt>.gq.enqueue(GoalRef(<goalId>, <prog>.labels['p/0']!));`
   →
   `<rt>.Gq.Enqueue(new GoalRef(<goalId>, <prog>.Labels["p/0"]!));`.
   Transformations: `.gq` ⇒ `.Gq` (PascalCased field per SUT
   `lib/runtime/runtime.dart.md`); `.enqueue` ⇒ `.Enqueue`
   (PascalCased method per SUT `lib/runtime/goal_queue.dart.md`);
   `GoalRef(...)` ⇒ `new GoalRef(...)` (C# requires `new`);
   `.labels[...]` ⇒ `.Labels[...]` (PascalCased getter per SUT
   `lib/bytecode/runner.dart.md`); `'p/0'` ⇒ `"p/0"`; `!` preserved
   as C# null-forgiving operator.
   (Idiom `rf-dart-bang-null-assertion-to-csharp-null-forgiving`.
   FAILURE-MODE DIFFERENCE acknowledged in the convspec nuance:
   Dart `!` throws at runtime if null; C# `!` is a compile-time
   annotation; the `Dictionary<K,V>` indexer already throws
   `KeyNotFoundException` on miss, making `!` redundant but kept
   for readability and forward-compatibility with nullable-aware
   indexer shapes.)

10. **`dart.list_literal.bytecode_program_assembly`**
    Each `BC.prog([<expr>, <expr>, ...])` →
    `BC.Prog(new List<Op> { <expr>, <expr>, ... })`. Element-type
    annotation kept explicit (`new List<Op>`) so inference does not
    widen to `object` when the receiver signature varies. Per-call
    transliterations:
    - Test 1 nop program:
      `BC.Prog(new List<Op> { BC.L("p/0"), BC.TRY(), BC.COMMIT(),
      BC.Nop(), BC.Nop(), BC.Nop(), BC.PROCEED(), BC.L("p/0_end"),
      BC.SUSP() })`
    - Test 2 halt program:
      `BC.Prog(new List<Op> { BC.L("p/0"), BC.TRY(), BC.COMMIT(),
      BC.Halt(), BC.L("p/0_end"), BC.SUSP() })`
    - Test 3 progHalt:
      `BC.Prog(new List<Op> { BC.L("p/0"), BC.TRY(), BC.COMMIT(),
      BC.Halt() })`
    - Test 3 progProceed:
      `BC.Prog(new List<Op> { BC.L("p/0"), BC.TRY(), BC.COMMIT(),
      BC.PROCEED() })`
    Per `lib/bytecode/asm.dart.md`'s
    `rf-dart-uppercase-alias-method-naming`, the UPPERCASE alias
    surface (`TRY`, `COMMIT`, `PROCEED`, `SUSP`, `L`) is preserved
    in C# verbatim; the lowerCamelCase forms (`nop` / `halt`)
    PascalCase to `Nop` / `Halt`.
    (Idiom `rf-dart-list-literal-to-csharp-list-initializer`. C# 12
    collection-expression `[ ... ]` and `params Op[]` are viable
    alternatives if the SUT signature dictates; spec default =
    `new List<Op> { ... }`.)

11. **`dart.method_call.scheduler_drain_with_named_max_cycles`**
    Each `final ran = sched.drain(maxCycles: 10);` →
    `var ran = sched.Drain(maxCycles: 10);` (and similarly for
    `ranHalt`, `ranProceed`). `final` ⇒ `var`; `.drain` ⇒ `.Drain`;
    named arg `maxCycles: 10` preserved as C# `maxCycles: 10`.
    Return-type shape (`List<GoalId>` / `IReadOnlyList<GoalId>` /
    a `DrainResult` bundle) is owned by the SUT scheduler spec; the
    test's `.length` / `Count` access (constructs 5 + 12 below) is
    valid against any of these shapes per the convspec.
    (Idiom
    `rf-dart-method-call-with-named-arg-to-csharp-method-call-with-named-arg`.)

12. **`dart.member_access.list_length_property`**
    `<ran>.length` → `<ran>.Count` at every callsite — five total:
    two inside interpolated `print` strings (construct 5 above) and
    three inside `expect` assertions (constructs 13 + 14 below).
    Different name, identical semantics, O(1) on both sides.
    Reference-vs-value: `List<E>` is a reference type in both
    languages — no copy at `.Count` access.
    (Idiom `rf-dart-list-length-to-csharp-list-count`. Distinct from
    the `String.length` ⇒ `string.Length` mapping — every `.length`
    in this file is on a list, not a string.)

13. **`dart.package_test.expect_value_equals_matcher_with_reason`**
    Two callsites (one in Test 1, one in Test 2):
    - `expect(ran.length, 1, reason: 'Should execute through all nops
      and succeed');` →
      `// Should execute through all nops and succeed`
      `Assert.Equal(1, ran.Count);`
    - `expect(ran.length, 1, reason: 'Should halt and terminate');` →
      `// Should halt and terminate`
      `Assert.Equal(1, ran.Count);`
    EXPECTED-FIRST argument-order swap is load-bearing — Dart
    `expect(actual, value)` is ACTUAL-FIRST; xUnit `Assert.Equal<T>
    (expected, actual)` is EXPECTED-FIRST. The `1` literal binds
    `T = int` (the `Count` property's return type). `Assert.Equal<T>`
    has NO `userMessage` overload, so the `reason:` text is routed
    to an inline `//` comment.
    (Idiom `rf-dart-expect-bare-value-int-to-xunit-assert-equal`.)

14. **`dart.package_test.expect_value_equals_matcher_bare`**
    Two callsites in Test 3 (bare form, no `reason:`):
    - `expect(ranHalt.length, 1);` → `Assert.Equal(1, ranHalt.Count);`
    - `expect(ranProceed.length, 1);` →
      `Assert.Equal(1, ranProceed.Count);`
    Same EXPECTED-FIRST swap as the reason-bearing sibling above; no
    inline-comment emission needed.
    (Idiom `rf-dart-expect-bare-value-int-to-xunit-assert-equal`,
    bare-form row.)

Per-file synthesis (single C# compilation unit
`test/bytecode/UtilityInstructionsTest.cs`): file-level `using`s for
xUnit + xUnit.Abstractions + the two converted runtime/bytecode
sub-namespaces; a single `public class UtilityInstructionsTest` with
a constructor-injected `ITestOutputHelper` field; three
`[Fact(DisplayName = ...)]` synchronous `void` methods each holding
the per-test body verbatim per the construct-level mappings above.

## 3. Decomposed Task Units

- T1: emit file-scope `using Xunit;` + `using Xunit.Abstractions;` (construct 1) — done in §2.
- T2: collapse the seven internal Dart `package:glp_runtime/...` imports into two C# `using` directives (`using <RootNs>.Runtime;`, `using <RootNs>.Bytecode;`) (construct 2) — done in §2.
- T3: emit `public class UtilityInstructionsTest { ... }` shell with no base class (construct 3) — done in §2.
- T4: emit private readonly `ITestOutputHelper _output;` field + matching constructor (constructs 3 + 4) — done in §2.
- T5: emit `[Fact(DisplayName = "Nop: no operation, just advances PC")] public void NopNoOperationJustAdvancesPC()` shell (construct 3) — done in §2.
- T6: emit `[Fact(DisplayName = "Halt: terminates execution")] public void HaltTerminatesExecution()` shell (construct 3) — done in §2.
- T7: emit `[Fact(DisplayName = "Halt vs Proceed: both terminate")] public void HaltVsProceedBothTerminate()` shell (construct 3) — done in §2.
- T8: translate the 14 `print(...)` callsites to `_output.WriteLine(...)` with `\n` escapes and `✓` glyph preserved (construct 4) — done in §2.
- T9: translate the two `'Goals executed: ${ran.length}'` interpolations to `$"Goals executed: {ran.Count}"` (constructs 5 + 12) — done in §2.
- T10: translate the eleven `final <name> = <Ctor>(...);` locals to `var <name> = new <Ctor>(...);` (construct 6) — done in §2.
- T11: translate the three `const goalId = 100/200;` locals to `const GoalId <name> = <int>;` (construct 7) — done in §2.
- T12: translate the three `<rt>.setGoalEnv(<goalId>, <env>);` calls to `<rt>.SetGoalEnv(<goalId>, <env>);` (construct 8) — done in §2.
- T13: translate the three `<rt>.gq.enqueue(GoalRef(<goalId>, <prog>.labels['p/0']!));` calls to `<rt>.Gq.Enqueue(new GoalRef(<goalId>, <prog>.Labels["p/0"]!));` (construct 9) — done in §2.
- T14: assemble the four `BC.prog([...])` programs (nop / halt / progHalt / progProceed) as `BC.Prog(new List<Op> { ... })` with the uppercase-alias surface preserved and `nop`/`halt` PascalCased to `Nop`/`Halt` (construct 10) — done in §2.
- T15: translate the three `<sched>.drain(maxCycles: 10)` calls to `<sched>.Drain(maxCycles: 10)` (construct 11) — done in §2.
- T16: translate every `<ran>.length` (5 callsites) to `<ran>.Count` (construct 12) — done in §2.
- T17: translate the two reason-bearing `expect(ran.length, 1, reason: '...')` to inline-comment + `Assert.Equal(1, ran.Count);` (EXPECTED-FIRST swap) (construct 13) — done in §2.
- T18: translate the two bare `expect(<ran>.length, 1)` to `Assert.Equal(1, <ran>.Count);` (EXPECTED-FIRST swap) (construct 14) — done in §2.
- T19: drop the Dart `void main()` registration block entirely (construct 3) — done in §2.

## 4. Research Findings

none required — every construct in §2 is verbatim-derived from the
ratified mirror convspec
`.codeconv/conversion-specs/test/bytecode/utility_instructions_test.dart.md`
(16 constructs, 0 escalations, all idioms already KB-recorded by
prior batch members per FR-012 / SC-007). No WebSearch / WebFetch /
Agent invocation needed; no new idioms introduced.

## 5. Consistency Pass

fixed — derived from
`.codeconv/conversion-specs/test/bytecode/utility_instructions_test.dart.md`
(ratified mirror convspec, sha256-matched at
`e17beae5607cc699811d9b457a38aed9c19cba512552730e9189fa854b1c5c63`,
escalations: 0). All 16 construct mappings in §2 mirror the spec's
`constructs:` block verbatim; the §3 task decomposition mirrors the
spec's `conversion_units:` list one-to-one; the §1 source analysis
quotes the Dart file directly (lines 1–130, 16 KB on disk).

## 6. Escalations

None.
