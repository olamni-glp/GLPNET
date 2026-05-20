# Conversion Spec — test/bytecode/utility_instructions_test.dart

> Conversion-spec artifact for test/bytecode/utility_instructions_test.dart (FR-011).
> Spec-only (FR-023): describes the Dart->C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.
>
> A small (130-line) xUnit-shaped test file that exercises two utility
> bytecode opcodes (`nop` / `halt`) plus a `halt`-vs-`PROCEED`
> equivalence sanity check, by hand-assembling three tiny
> `BytecodeProgram`s via the `BC` assembler facade (`asm.dart`),
> instantiating a `BytecodeRunner` + `Scheduler`, manually populating
> the runtime's goal queue (`rt.gq.enqueue(GoalRef(...))`) and per-goal
> environment (`rt.setGoalEnv(...)`), draining a bounded number of
> cycles (`sched.drain(maxCycles: 10)`), and asserting how many goals
> ran. Every cross-file type reference (`GlpRuntime`, `CallEnv`,
> `Scheduler`, `BytecodeRunner`, `BC`, `GoalRef`, opcode/label tables)
> is REUSED from the corresponding sibling convspec (FR-024 cache hit),
> never re-derived. Every test-framework / assertion / `print` /
> top-level-`main` decision is REUSED from the prior batch of test
> convspecs (`test/smoke_test.dart.md`,
> `test/glp_runtime_test.dart.md`,
> `test/heap/varref_pointer_test.dart.md`,
> `test/conformance/fairness_26_test.dart.md`,
> `test/test_channel_construction.dart.md`, ...). No escalations.

```yaml
schema_version: 1
source_path: test/bytecode/utility_instructions_test.dart
source_sha256: e17beae5607cc699811d9b457a38aed9c19cba512552730e9189fa854b1c5c63
target_code_unit: test/bytecode/UtilityInstructionsTest.cs
constructs:
  - construct_key: dart.package_test.import_directive
    source_form: "import 'package:test/test.dart';"
    target_decision: >-
      Drop `import 'package:test/test.dart';` and emit `using Xunit;`
      and (for `print(...)` diagnostic-trace capture) inject
      `ITestOutputHelper output` in the test class constructor with
      `using Xunit.Abstractions;`. REUSE the batch-wide test-framework
      idiom from `test/smoke_test.dart.md` (and every subsequent test
      convspec in the batch — `glp_runtime_test`, `test/heap/*`,
      `test/conformance/*`, `test/multiagent/*`,
      `test/analysis/*`, `test/module/*`,
      `test/test_channel_construction.dart`). Per FR-012 / SC-007 this
      construct is NOT re-researched here. The .NET test project's
      `.csproj` (referencing `xunit`, `xunit.runner.visualstudio`,
      `Microsoft.NET.Test.Sdk`) is OUT OF SCOPE for this per-file
      artifact — same langpair-level emission concern recorded in the
      sibling specs.
    idiom_id: rf-dart-package-test-to-dotnet-xunit
    research_finding_id: rf-dart-package-test-to-dotnet-xunit
    nuance: >-
      Framework-choice reuse (explicitly addressed, no re-derivation):
      xUnit settled in `smoke_test.dart`; every subsequent test file in
      the batch reuses it. Top-level `test()` ⇒ `[Fact]` instance
      method, fresh test-class instance per `[Fact]` (xunit.net
      "Shared Context between Tests"), no top-level function surface in
      xUnit. No async / `Future` / `Stream` / isolate surface in this
      file — `[Fact]` methods are synchronous `void` (NOT `async Task`).
      Strict-bool / strict-equality semantics unaffected by the import
      itself. The `print(...)` calls in every test introduce a separate
      `ITestOutputHelper` injection (see the `dart.core.print`
      construct below) — that pulls `using Xunit.Abstractions;` into
      this file as well.
  - construct_key: dart.internal_package_import.same_package_multi
    source_form: >-
      "import 'package:glp_runtime/runtime/runtime.dart';
       import 'package:glp_runtime/runtime/cells.dart';
       import 'package:glp_runtime/runtime/machine_state.dart';
       import 'package:glp_runtime/runtime/scheduler.dart';
       import 'package:glp_runtime/bytecode/runner.dart';
       import 'package:glp_runtime/bytecode/opcodes.dart';
       import 'package:glp_runtime/bytecode/asm.dart';"
    target_decision: >-
      Drop all seven Dart `import 'package:glp_runtime/...';`
      directives and collapse them into TWO file-level C# `using`
      directives: `using <RootNs>.Runtime;` (covers `GlpRuntime`,
      `CallEnv`, `GoalId`, `GoalRef`, `Scheduler`, `Cells` surface — the
      first four imports plus the `Scheduler` import all lift into the
      same `<RootNs>.Runtime` sub-namespace per the SUT specs
      `lib/runtime/runtime.dart.md`, `lib/runtime/cells.dart.md`,
      `lib/runtime/machine_state.dart.md`,
      `lib/runtime/scheduler.dart.md`, `lib/runtime/goal_queue.dart.md`)
      and `using <RootNs>.Bytecode;` (covers `BytecodeRunner` / the
      `BC` static assembler facade / opcode types per
      `lib/bytecode/runner.dart.md`, `lib/bytecode/asm.dart.md`,
      `lib/bytecode/opcodes.dart.md`). Per FR-012 / SC-007 this
      construct is NOT re-researched here — REUSE the
      `rf-dart-internal-package-import-to-csharp-using` finding
      recorded in `test/heap/*` and `test/conformance/fairness_26_test`
      where multiple runtime `package:` imports collapsed into one
      `using`. The test assembly's `.csproj` must reference the
      converted-SUT assembly — langpair-level concern, OUT OF SCOPE.
    idiom_id: rf-dart-internal-package-import-to-csharp-using
    research_finding_id: rf-dart-internal-package-import-to-csharp-using
    nuance: >-
      Cross-file dependency nuance (explicitly addressed, KB reuse):
      Dart `package:` imports are per-file path-based; C# `using` is
      per-namespace. Seven Dart files into two converted sub-namespaces
      ⇒ two C# `using` directives — NOT seven. Sub-namespace boundary
      from the SUT specs: `runtime/*.dart` ⇒ `<RootNs>.Runtime`,
      `bytecode/*.dart` ⇒ `<RootNs>.Bytecode`. No `using static` is
      needed — every referenced identifier is reachable as a namespaced
      member (`GlpRuntime`, `Scheduler`, `BytecodeRunner`, `BC.Prog`,
      `BC.L`, `BC.TRY`, etc.). The `BC` class IS a namespace-of-statics
      (see `lib/bytecode/asm.dart.md`
      rf-dart-namespace-class-of-statics-to-csharp-static-class) and is
      called as `BC.Prog(...)` / `BC.L(...)` / `BC.TRY()` at the test
      callsites — the test source already names it qualified, so no
      `using static BC;` is needed. No cross-package, cross-isolate, or
      transitive-export semantics apply. Visibility: every imported
      identifier is library-public on the Dart side ⇒ `public` on the
      C# side per the SUT specs.
  - construct_key: dart.test_file.void_main_with_multiple_test_calls_no_group
    source_form: >-
      "void main() {
         test('Nop: no operation, just advances PC', () { ... });
         test('Halt: terminates execution', () { ... });
         test('Halt vs Proceed: both terminate', () { ... });
       }"
    target_decision: >-
      Eliminate the Dart `void main()` function entirely and lift each
      of the three top-level `test(...)` calls into one
      `[Fact]`-attributed public instance method on a single
      `public class UtilityInstructionsTest` (mirroring the file name
      `utility_instructions_test.dart` ⇒ `UtilityInstructionsTest.cs`).
      No `group(...)` in source ⇒ no inner-class topology; flat
      multi-`[Fact]` class. Per-test fresh-instance lifecycle (xUnit
      creates one instance per `[Fact]`) is benign — there is no
      cross-test shared mutable state at the file level. Each Dart
      test label becomes the method identifier (PascalCased,
      punctuation-stripped) with `[Fact(DisplayName = "<original
      label>")]` preserving the source label verbatim for the test
      reporter:
        - `'Nop: no operation, just advances PC'`
          ⇒ `[Fact(DisplayName = "Nop: no operation, just advances PC")]
             public void NopNoOperationJustAdvancesPC()`
        - `'Halt: terminates execution'`
          ⇒ `[Fact(DisplayName = "Halt: terminates execution")]
             public void HaltTerminatesExecution()`
        - `'Halt vs Proceed: both terminate'`
          ⇒ `[Fact(DisplayName = "Halt vs Proceed: both terminate")]
             public void HaltVsProceedBothTerminate()`.
      REUSE the test-main-drop idiom recorded in the sibling
      `smoke_test.dart`, `glp_runtime_test.dart`,
      `fairness_26_test.dart`, and `test/heap/*` specs — same
      structural lift; no re-research (FR-012).
    idiom_id: rf-dart-test-main-to-xunit-class-with-facts
    research_finding_id: rf-dart-test-main-to-xunit-class-with-facts
    nuance: >-
      Discovery-model nuance (explicitly addressed, carry-forward from
      siblings): xUnit discovers tests by reflection over `[Fact]`
      attributes with a FRESH instance of the test class per `[Fact]`
      (xunit.net "Shared Context between Tests" —
      `https://xunit.net/docs/shared-context`). Dart `main()`
      registration pass has no xUnit equivalent and is dropped. Three
      `test(...)` calls, NO `group(...)` ⇒ one test class with three
      `[Fact]` methods (NOT the three-classes shape of
      `varref_pointer_test` which had three `group`s). Identifier
      PascalCasing: split on whitespace and punctuation
      (`:` / `-` / `,`), drop the punctuation, uppercase the first
      letter of each token, concatenate. `'PC'` (acronym) preserves all
      uppercase per Microsoft's C# coding conventions for two-letter
      acronyms (Microsoft Learn "Capitalization Conventions" — short
      acronyms remain uppercase). `DisplayName` preserves the
      human-readable form verbatim so the reporter shows the source
      label. No setUp / tearDown / group / `late` field — synchronous
      `void` `[Fact]`, no constructor body beyond the
      `ITestOutputHelper` injection introduced by the `dart.core.print`
      construct below, no `IDisposable.Dispose` / `IAsyncLifetime`.
  - construct_key: dart.core.print
    source_form: >-
      "print('\\n=== NOP TEST: No operation ===');
       print('Starting p with 3 nops');
       print('Goals executed: ${ran.length}');
       print('✓ Nop instruction works correctly\\n');
       print('\\n=== HALT TEST: Terminate execution ===');
       print('Starting p with halt');
       print('Goals executed: ${ran.length}');
       print('✓ Halt instruction terminates correctly\\n');
       print('\\n=== HALT VS PROCEED TEST ===');
       print('Testing halt...');
       print('✓ Halt terminated after 1 execution');
       print('Testing proceed...');
       print('✓ Proceed terminated after 1 execution');
       print('✓ Both Halt and Proceed terminate execution\\n');"
    target_decision: >-
      Dart top-level `print(String)` writes to stdout with a trailing
      newline. The xUnit-idiomatic target is
      `ITestOutputHelper.WriteLine(...)` — xUnit captures per-test
      stdout for the test reporter via a constructor-injected
      `ITestOutputHelper`. Codegen MUST emit (i) a constructor
      `public UtilityInstructionsTest(ITestOutputHelper output) {
      _output = output; }`, (ii) a `private readonly
      ITestOutputHelper _output;` field, and (iii) translate every
      `print(<arg>)` callsite to `_output.WriteLine(<arg>);`.
      `Console.WriteLine` is a viable but INFERIOR fallback (xUnit does
      NOT capture `Console.Out` — output would not surface under the
      test in VS Test Explorer, `dotnet test --logger trx`, or Rider).
      REUSE the precedent recorded in
      `test/test_channel_construction.dart.md` (idiom
      `rf-dart-print-to-xunit-itestoutputhelper-writeline`) and
      `test/multiagent/mad_cold_call_isolate_test.dart.md` — KB cache
      hit per FR-012 / SC-007, NO re-research.
    idiom_id: rf-dart-print-to-xunit-itestoutputhelper-writeline
    research_finding_id: rf-dart-print-to-xunit-itestoutputhelper-writeline
    nuance: >-
      Diagnostic-output nuance (explicitly addressed, well-known xUnit
      footgun, carry-forward): xUnit deliberately does NOT capture
      `Console.Out` (xunit.net `https://xunit.net/docs/capturing-output`
      — "xUnit.net captures output via the `ITestOutputHelper` interface
      injected into the constructor"). Per-test isolation: the helper is
      unique per test-class instance; output from one `[Fact]` cannot
      bleed into another's report. Escape-character nuance: the `\\n`
      sequences in the source (e.g.
      `'\\n=== NOP TEST: No operation ==='` and
      `'✓ Nop instruction works correctly\\n'`) are processed as a
      newline by Dart's string-literal parser; C# string literals also
      process `\\n` identically — the payload is byte-identical after
      escape processing. Codegen emits the equivalent C# string literal
      with `\\n` verbatim. UTF-8 checkmark `✓` (U+2713) survives
      unchanged because both Dart and C# string literals accept the
      literal glyph encoded directly (Dart string literals are UTF-16
      code-unit sequences; C# string literals are UTF-16 code-unit
      sequences — same encoding). The `print` calls are pure observers
      (NO assertion-load-bearing role) — every assertion in this file
      is a separate `expect(...)` call (see the
      `dart.package_test.expect_value_equals_matcher_with_reason`
      construct below and its bare-form sibling). Dropping them is
      observably equivalent at the assertion layer but loses the
      per-test trace narration the source author wrote — preserving
      them via `ITestOutputHelper.WriteLine` is the faithful
      conversion.
  - construct_key: dart.string.interpolation.simple_expression
    source_form: "'Goals executed: ${ran.length}'"
    target_decision: >-
      Translate the two Dart interpolated string literals
      (`'Goals executed: ${ran.length}'`, appearing once in each of the
      first two tests) to C# interpolated string literals
      `$"Goals executed: {ran.Count}"`. Dart uses `${expr}` inside a
      single-quoted (or double-quoted) literal; C# uses `{expr}` inside
      a `$"..."` literal. The `.length` accessor on a Dart `List<E>`
      ⇒ `.Count` on C# `IReadOnlyList<E>` per the SUT spec's
      converted `Scheduler.drain(...)` return-type decision (see
      `lib/runtime/scheduler.dart.md` — `drainWithStatus` returns a
      `DrainResult` value bundle whose `goalsRan` field carries the
      executed-goals list; the test's `final ran = sched.drain(...)`
      shape, and the property used on `ran`, are owned by that SUT
      spec — `.length` ⇒ either `.Count` (`IReadOnlyList<T>.Count`) or
      `.Length` (`T[].Length`) per the SUT spec's collection-shape
      decision; spec default is `.Count` because the SUT spec converts
      `List<T>` ⇒ `List<T>` / `IReadOnlyList<T>` with `Count`, per the
      Dart-`List<T>`-to-C#-`List<T>` carry-forward recorded throughout
      the batch). REUSE the
      `rf-dart-string-interpolation-to-csharp-dollar-string` finding
      from `fairness_26_test.dart` and
      `test_channel_construction.dart`.
    idiom_id: rf-dart-string-interpolation-to-csharp-dollar-string
    research_finding_id: rf-dart-string-interpolation-to-csharp-dollar-string
    nuance: >-
      Interpolation-syntax nuance (explicitly addressed, carry-forward):
      Dart `${expr}` ⇒ C# `{expr}` inside `$"..."`. Property-name
      casing: Dart `ran.length` (camelCase property on the converted
      `List<T>` / record-returning-list) ⇒ C# `ran.Count`
      (PascalCased per Microsoft's C# coding conventions, owned by the
      SUT scheduler spec). Brace-escape: C# `{{`/`}}` vs Dart `\\$`
      — neither needed here. Implicit `toString()`: Dart calls
      `Object.toString()` on the embedded expression; C# calls
      `Object.ToString()` (or `IFormattable.ToString`). For an `int`
      `Count` value both produce the canonical decimal representation;
      no culture-sensitivity hazard.
  - construct_key: dart.local_var.final_typed_constructor_invocation
    source_form: >-
      "final rt = GlpRuntime();
       final rt2 = GlpRuntime();
       final runner = BytecodeRunner(prog);
       final runnerHalt = BytecodeRunner(progHalt);
       final runnerProceed = BytecodeRunner(progProceed);
       final sched = Scheduler(rt: rt, runner: runner);
       final schedHalt = Scheduler(rt: rt, runner: runnerHalt);
       final schedProceed = Scheduler(rt: rt2, runner: runnerProceed);
       final env = CallEnv();
       final env1 = CallEnv();
       final env2 = CallEnv();"
    target_decision: >-
      Emit `var <name> = new <Ctor>(...);` in the C# `[Fact]` method
      body. `final` on a Dart local that is never reassigned ⇒ C# `var`
      (NOT `readonly` — `readonly` applies to fields, not locals; C#
      has no method-local `readonly`). `<Ctor>()` ⇒ `new <Ctor>()` —
      C# requires `new`. The `Scheduler` constructor uses Dart
      NAMED parameters `(rt: rt, runner: runner)`; C# supports named
      arguments natively — emit
      `new Scheduler(rt: rt, runner: runner)`. The converted classes
      live in `<RootNs>.Runtime` / `<RootNs>.Bytecode` (already
      brought into scope by the file-level `using`s).
    idiom_id: rf-dart-final-local-to-csharp-var
    research_finding_id: rf-dart-final-local-to-csharp-var
    nuance: >-
      Local-variable mutability nuance (carry-forward, KB cache hit
      per FR-012 / SC-007): Dart `final <name> = expr;` ⇒ C# `var`
      (precedents: `fairness_26_test.dart`,
      `varref_pointer_test.dart`, and many more). The single-
      assignment intent is lost at the language level — a later edit
      could reassign — but the generated body does not reassign. Named
      arguments: Dart `Scheduler(rt: rt, runner: runner)` ⇒ C#
      `new Scheduler(rt: rt, runner: runner)` — Microsoft Learn "Named
      and optional arguments"
      (`https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/named-and-optional-arguments`)
      — direct semantic match. The `Scheduler` parameter names (`rt`,
      `runner`) are owned by the SUT `lib/runtime/scheduler.dart.md`
      spec — same identifiers in C# per the SUT spec's parameter-name
      carry-forward (lowerCamelCase ⇒ lowerCamelCase for parameter
      names per Microsoft's C# coding conventions). `Reference-vs-
      value`: every class here (`GlpRuntime`, `Scheduler`,
      `BytecodeRunner`, `CallEnv`) is a reference type in both Dart
      and C# per the SUT specs.
  - construct_key: dart.const_local.typed_int_literal
    source_form: >-
      "const goalId = 100;
       const goalId1 = 100;
       const goalId2 = 200;"
    target_decision: >-
      Emit `const GoalId <name> = <int>;` in the C# `[Fact]` method
      body. C# `const` on a method local with an integer literal is
      semantically equivalent to Dart `const` on a local with an
      integer literal. The Dart source omits an explicit type — type
      inference assigns `int` — but the value flows into APIs that
      expect `GoalId` (the `typedef GoalId = int;` from
      `lib/runtime/machine_state.dart.md`); the C# emission MAY use the
      converted `GoalId` type alias (`using GoalId = System.Int32;` per
      the SUT spec) or plain `int` / `long` depending on the SUT spec's
      recorded integer-width decision (see
      `rf-dart-int-to-csharp-long-width` carry-forward). Spec default
      = emit the SUT-spec-decided `GoalId` shape verbatim. REUSE the
      `rf-dart-const-local-typed-int-to-csharp-const` finding from
      `fairness_26_test.dart`.
    idiom_id: rf-dart-const-local-typed-int-to-csharp-const
    research_finding_id: rf-dart-const-local-typed-int-to-csharp-const
    nuance: >-
      `const` semantics nuance (carry-forward): Dart `const` on a
      local with a literal initialiser ⇒ compile-time constant; C#
      `const` on a method local with a literal initialiser ⇒
      compile-time constant (Microsoft Learn `const` keyword
      `https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/const`).
      `readonly` is the WRONG mapping (fields only). Integer-width:
      Dart `int` literals `100` / `200` are well within both `int`
      and `long` ranges — no truncation hazard. The `goalId` /
      `goalId1` / `goalId2` identifiers stay lowerCamelCase per the
      Dart source's stylistic choice for locals (C# allows
      lowerCamelCase on locals per Microsoft's coding conventions —
      local-name casing is intentionally lenient).
  - construct_key: dart.expression_statement.method_call_void_setter_pair
    source_form: >-
      "rt.setGoalEnv(goalId, env);
       rt2.setGoalEnv(goalId2, env2);
       rt.setGoalEnv(goalId1, env1);"
    target_decision: >-
      Emit `rt.SetGoalEnv(goalId, env);` (etc.) — direct verbatim
      transliteration with the method name PascalCased
      (`setGoalEnv` ⇒ `SetGoalEnv`) per Microsoft's C# coding
      conventions for public methods (camelCase ⇒ PascalCase). The
      `SetGoalEnv` signature shape is owned by
      `lib/runtime/runtime.dart.md`'s SUT spec (per-goal env
      registration on the `GlpRuntime` instance). No null-safety hazard
      (both arguments are non-nullable value-/ref-types per the SUT
      spec). REUSE `rf-dart-camel-to-csharp-pascal-method-rename` (the
      batch-wide naming convention recorded throughout the lib + test
      specs — KB cache hit per FR-012 / SC-007).
    idiom_id: rf-dart-camel-to-csharp-pascal-method-rename
    research_finding_id: rf-dart-camel-to-csharp-pascal-method-rename
    nuance: >-
      Method-naming nuance (carry-forward, batch-wide): every public
      Dart member name (camelCase) PascalCases in C# per the SUT specs
      (Microsoft Learn "C# Coding Conventions"
      `https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions`
      — "Use PascalCase for ... methods"). Argument-order verbatim:
      `(goalId, env)` ⇒ `(goalId, env)` — Dart positional arguments map
      1-to-1 to C# positional arguments. Return type: `void` in both
      languages — no propagation hazard. Side-effect timing: the
      method's writes to the runtime's per-goal env table are
      synchronous in both languages — no async / `await` reshape
      needed.
  - construct_key: dart.expression_statement.qualified_method_call_with_nested_constructor
    source_form: >-
      "rt.gq.enqueue(GoalRef(goalId, prog.labels['p/0']!));
       rt.gq.enqueue(GoalRef(goalId1, progHalt.labels['p/0']!));
       rt2.gq.enqueue(GoalRef(goalId2, progProceed.labels['p/0']!));"
    target_decision: >-
      Translate the qualified two-level method call
      `rt.gq.enqueue(GoalRef(goalId, prog.labels['p/0']!))` to
      `rt.Gq.Enqueue(new GoalRef(goalId, prog.Labels["p/0"]!));`. The
      transformations are: (i) `.gq` ⇒ `.Gq` (the `GoalQueue` field on
      `GlpRuntime` PascalCases per the SUT spec
      `lib/runtime/runtime.dart.md`); (ii) `.enqueue` ⇒ `.Enqueue`
      (method PascalCases per the SUT spec
      `lib/runtime/goal_queue.dart.md`); (iii) `GoalRef(...)` ⇒
      `new GoalRef(...)` (C# requires `new`); (iv) `.labels[...]` ⇒
      `.Labels[...]` (the `labels` map on `BytecodeProgram`
      PascalCases per the SUT spec `lib/bytecode/runner.dart.md` —
      `BytecodeProgram.labels` is a `Map<Label, int>` ⇒
      `Dictionary<Label, int>` or `IReadOnlyDictionary<string, int>`
      with the `Labels` PascalCased getter); (v) Dart's single-quoted
      key `'p/0'` ⇒ C# double-quoted `"p/0"`; (vi) the bang-operator
      `!` (Dart null-assertion that asserts the lookup did not return
      `null`) ⇒ C# null-forgiving `!` (same operator, same semantics)
      — Microsoft Learn "Null-forgiving operator"
      (`https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/null-forgiving`).
    idiom_id: rf-dart-bang-null-assertion-to-csharp-null-forgiving
    research_finding_id: rf-dart-bang-null-assertion-to-csharp-null-forgiving
    nuance: >-
      Null-assertion nuance (explicitly addressed): Dart `!` is the
      null-check operator — for non-nullable target types it asserts
      "I know this is not null; throw if it is" (Dart language tour
      `https://dart.dev/null-safety#null-check-operator`). C# `!` is
      the null-forgiving operator — for nullable reference types it
      tells the compiler "I know this is not null; do not warn"
      (Microsoft Learn link above). FAILURE-MODE DIFFERENCE
      (load-bearing, NOT silently glossed): Dart `!` THROWS at runtime
      if the value is null; C# `!` is a COMPILE-TIME annotation that
      does NOT throw at runtime — if the value is null, the next
      dereference throws `NullReferenceException` instead. For a
      `Dictionary<K,V>` lookup the failure modes are: Dart `labels['p/0']`
      returns `null` if absent ⇒ `!` throws `TypeError`; C#
      `Labels["p/0"]` (indexer) THROWS `KeyNotFoundException` if
      absent (Microsoft Learn `Dictionary<TKey,TValue>.this[TKey]`
      `https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.dictionary-2.item`)
      — so the `!` is REDUNDANT on the C# side (the indexer already
      throws on miss), but emitting `!` keeps the source's
      "must-not-be-null" intent visible AND silences any nullable-
      reference-type warning if the SUT spec converts the `Labels`
      indexer return type as nullable. Spec default: KEEP the `!` for
      readability + forward compatibility with nullable-aware Labels
      shape. Map-lookup semantics: both throw on absent key — same
      observable failure for this test (the `'p/0'` label is always
      present in the program built by `BC.prog`).
  - construct_key: dart.list_literal.bytecode_program_assembly
    source_form: >-
      "BC.prog([
         BC.L('p/0'),
         BC.TRY(),
         BC.COMMIT(),
         BC.nop(),
         BC.nop(),
         BC.nop(),
         BC.PROCEED(),
         BC.L('p/0_end'),
         BC.SUSP(),
       ])  // also BC.halt(), and three similar prog([...]) builders"
    target_decision: >-
      Translate each `BC.prog([<expr>, <expr>, ...])` call to
      `BC.Prog(new List<Op> { <expr>, <expr>, ... })` — or, if the SUT
      `lib/bytecode/asm.dart.md` records `BC.Prog`'s signature as
      taking `IEnumerable<Op>` / `params Op[]` / `IReadOnlyList<Op>`,
      to the corresponding C# collection-literal shape (C# 12
      collection expressions: `BC.Prog([<expr>, ...])`; or
      `params Op[]`: `BC.Prog(<expr>, <expr>, ...)` with the brackets
      dropped). Spec default = the verbose `new List<Op> { ... }`
      shape because it is the most-portable C# version and matches the
      Dart source's bracketed-list intent verbatim. Inner list
      elements: each `BC.<methodName>(<args>)` Dart call PascalCases
      the method name per the `BC` static-class spec
      (`lib/bytecode/asm.dart.md`). Specifically:
        - `BC.L('p/0')` ⇒ `BC.L("p/0")` (the `L` alias method is
          preserved as `L` per
          `rf-dart-uppercase-alias-method-naming` —
          `lib/bytecode/asm.dart.md`);
        - `BC.TRY()` ⇒ `BC.TRY()` (uppercase alias preserved);
        - `BC.COMMIT()` ⇒ `BC.COMMIT()` (uppercase alias preserved);
        - `BC.nop()` ⇒ `BC.Nop()` (lowerCamelCase form PascalCases —
          this method has both forms per the asm spec; the source uses
          the lowerCamelCase form here, but C# requires Pascal — emit
          `BC.Nop()`, which the asm spec also records);
        - `BC.halt()` ⇒ `BC.Halt()` (same PascalCase rule);
        - `BC.PROCEED()` ⇒ `BC.PROCEED()` (uppercase alias preserved);
        - `BC.SUSP()` ⇒ `BC.SUSP()` (uppercase alias preserved).
      The parallel uppercase-vs-lowercase naming surface from the asm
      spec governs which call-site form is preserved; the test source
      here uses a MIX (uppercase `TRY` / `COMMIT` / `PROCEED` / `SUSP`
      / `L` AND lowercase `nop` / `halt`), and codegen preserves the
      source's per-callsite form.
    idiom_id: rf-dart-list-literal-to-csharp-list-initializer
    research_finding_id: rf-dart-list-literal-to-csharp-list-initializer
    nuance: >-
      List-literal nuance (explicitly addressed, carry-forward from
      `varref_pointer_test.dart`): Dart `[<expr>, <expr>, ...]` is a
      heterogeneous-or-homogeneous list literal with element-type
      inferred from context. The C# counterparts are
      `new List<T> { ... }` (collection initializer), the C# 12
      collection-expression `[ ... ]`, or `new T[] { ... }` /
      `params T[]` depending on the receiver signature — Microsoft
      Learn "Object and collection initializers"
      (`https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/object-and-collection-initializers`)
      and "Collection expressions"
      (`https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/collection-expressions`).
      Element-type binding: the list's element type is the converted
      `Op` interface (per `lib/bytecode/opcodes.dart.md` — every
      opcode class implements `Op`); the C# emission MAY annotate
      `new List<Op> { ... }` to make the binding explicit, since each
      `BC.<method>()` returns a different concrete `Op` subtype and
      list inference might widen to `object`. Spec default: emit the
      explicit element type for clarity. Lowercase-vs-UPPERCASE alias
      preservation: per `lib/bytecode/asm.dart.md`
      `rf-dart-uppercase-alias-method-naming`, both naming surfaces
      are preserved in the converted `BC` static class — `BC.TRY`,
      `BC.COMMIT`, `BC.PROCEED`, `BC.SUSP`, `BC.L`, `BC.Nop`,
      `BC.Halt` all exist in C# (the analyzer-suppression footgun is
      handled by the asm spec, not here). No analyzer-directive
      emission at this callsite.
  - construct_key: dart.method_call.scheduler_drain_with_named_max_cycles
    source_form: >-
      "final ran = sched.drain(maxCycles: 10);
       final ranHalt = schedHalt.drain(maxCycles: 10);
       final ranProceed = schedProceed.drain(maxCycles: 10);"
    target_decision: >-
      Translate `sched.drain(maxCycles: 10)` to
      `var ran = sched.Drain(maxCycles: 10);` — (i) `final` ⇒ `var`
      per `rf-dart-final-local-to-csharp-var`; (ii) `.drain` ⇒
      `.Drain` (method PascalCases); (iii) the Dart NAMED argument
      `maxCycles: 10` ⇒ C# named argument `maxCycles: 10` (Microsoft
      Learn "Named and optional arguments"
      `https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/named-and-optional-arguments`).
      The return type of `Scheduler.Drain` is owned by the SUT spec
      `lib/runtime/scheduler.dart.md` — Dart `List<GoalId>` (or a
      `DrainResult` whose `.goalsRan` is a `List<GoalId>`) ⇒ C#
      `List<GoalId>` / `IReadOnlyList<GoalId>` per the SUT spec's
      recorded shape; the test asserts on `.length` ⇒ `.Count` on the
      C# side (see the `dart.string.interpolation.simple_expression`
      construct above for the parallel use in `print`).
      ASSUMPTION/PRECONDITION: this convspec uses the Dart-side
      `Scheduler.drain(...)` shape from the test source verbatim. If
      the SUT scheduler spec converts `drain` to `drainWithStatus`
      (or a `DrainResult`-returning shape) the test conversion picks
      up the SUT-decided shape (see SUT-spec scheduler.dart.md notes
      on `drainWithStatus`). Spec default: emit the same source-side
      method name (`Drain`) and let the SUT spec resolve the
      return-type shape.
    idiom_id: rf-dart-method-call-with-named-arg-to-csharp-method-call-with-named-arg
    research_finding_id: rf-dart-method-call-with-named-arg-to-csharp-method-call-with-named-arg
    nuance: >-
      Named-argument nuance (explicitly addressed, carry-forward):
      Dart `drain(maxCycles: 10)` ⇒ C# `Drain(maxCycles: 10)` — direct
      semantic match. C# named-argument syntax requires the parameter
      name to match the converted-parameter-name on the SUT side
      (`maxCycles` ⇒ `maxCycles` per the SUT spec's parameter-name
      carry-forward — lowerCamelCase preserved for parameters per
      Microsoft's C# conventions). Argument-order: Dart named
      arguments are unordered; C# named arguments are unordered after
      any positional arguments (Microsoft Learn link above) — single
      argument here, no ordering concern. Return-type binding: handled
      by the SUT spec (the converted `Drain` return type owns the
      `.length` ⇒ `.Count` decision).
  - construct_key: dart.member_access.list_length_property
    source_form: "ran.length"
    target_decision: >-
      Translate `ran.length` (the `length` getter on a Dart `List<E>`)
      to `ran.Count` (the `Count` property on C# `List<E>` /
      `IReadOnlyList<E>` per the SUT-spec recorded collection-shape
      decision). The two usage sites are: (i) inside the interpolated
      `print` string (`'Goals executed: ${ran.length}'`) — see the
      `dart.string.interpolation.simple_expression` construct above;
      (ii) inside the `expect(ran.length, 1, reason: ...)` assertion
      — see the `dart.package_test.expect_value_equals_matcher_with_
      reason` construct below.
    idiom_id: rf-dart-list-length-to-csharp-list-count
    research_finding_id: rf-dart-list-length-to-csharp-list-count
    nuance: >-
      Collection-property nuance (explicitly addressed): Dart's
      `Iterable<E>.length` getter (Dart core library
      `https://api.dart.dev/stable/dart-core/Iterable/length.html`)
      maps to C# `ICollection<T>.Count` / `IReadOnlyCollection<T>.Count`
      (Microsoft Learn
      `https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.icollection-1.count`)
      — DIFFERENT NAME, same semantics (returns the number of elements).
      The Dart-`List`-`length`-property ⇒ C#-`List`-`Count`-property
      mapping is universal in the batch (every test convspec touching
      a list `.length` has reused this finding). Reference-vs-value:
      `List<E>` is a reference type in both languages — no copy at
      `.Count` access. O(1) cost on both sides. The other Dart
      `String.length` mapping ⇒ C# `string.Length` (capital `L`) is a
      SEPARATE idiom (not used in this file — every `.length` here
      is on a list, not a string).
  - construct_key: dart.package_test.expect_value_equals_matcher_with_reason
    source_form: "expect(ran.length, 1, reason: 'Should execute through all nops and succeed');"
    target_decision: >-
      Translate the bare-value `expect(actual, value, reason: msg)`
      form to xUnit `Assert.Equal(<value>, <actual>);` — EXPECTED-FIRST
      argument order (the OPPOSITE of Dart's ACTUAL-FIRST `expect`
      shape). xUnit's `Assert.Equal<T>` has NO `userMessage` overload
      (xunit.net Assert API reference; deliberate xUnit design — the
      value diff IS the diagnostic). Spec routes the Dart `reason:`
      text to an inline `// ...` comment ABOVE or beside the
      assertion so the author's rationale survives review even though
      xUnit cannot surface it at runtime. The `1` literal binds to
      `T = int` for the generic `Assert.Equal<T>` (the SUT
      scheduler-spec records `drain`'s returned list as a `List<GoalId>`
      / `IReadOnlyList<GoalId>` whose `Count` returns `int`; the
      EXPECTED literal `1` is an `int` on both sides). Two callsites in
      this file: `expect(ran.length, 1, reason: 'Should execute through
      all nops and succeed')` and `expect(ran.length, 1, reason:
      'Should halt and terminate')` — both route through the same
      `Assert.Equal(1, ran.Count); // <reason>` shape. REUSE the
      `rf-dart-expect-bare-value-int-to-xunit-assert-equal` finding
      from `glp_runtime_test.dart` and `fairness_26_test.dart`.
    idiom_id: rf-dart-expect-bare-value-int-to-xunit-assert-equal
    research_finding_id: rf-dart-expect-bare-value-int-to-xunit-assert-equal
    nuance: >-
      EXPECTED-FIRST argument-order footgun (load-bearing, carry-
      forward from `glp_runtime_test.dart` and `fairness_26_test.dart`):
      Dart `expect(actual, value)` is ACTUAL-FIRST; xUnit
      `Assert.Equal<T>(expected, actual)` is EXPECTED-FIRST — a naive
      textual transposition that preserved positional order would emit
      `Assert.Equal(ran.Count, 1)`, which would still PASS on success
      but produce REVERSED diagnostic output on failure. The
      conversion explicitly swaps. `reason:` lossiness nuance
      (carry-forward): xUnit's `Assert.Equal<T>` does NOT accept a
      `userMessage` argument — spec routes the Dart `reason:` text to
      an inline comment so the author's rationale survives review.
      Alternative `Assert.True(ran.Count == 1, "<reason>")` was
      considered (preserves the message at the cost of the value diff)
      and rejected because the value diff is the primary diagnostic.
      Bare-value matcher semantics (Dart): `package:test_api`'s
      `expect` documents that non-`Matcher` second arguments are
      wrapped in `equals(value)` — the bare `1` IS `equals(1)`. Strict
      equality on `int` is value-equality in both languages. Failure
      exception: Dart `TestFailure` vs xUnit
      `Xunit.Sdk.EqualException` — both runner-caught, semantically
      equivalent.
  - construct_key: dart.package_test.expect_value_equals_matcher_bare
    source_form: >-
      "expect(ranHalt.length, 1);
       expect(ranProceed.length, 1);"
    target_decision: >-
      Translate the bare-value `expect(actual, value)` form (no
      `reason:`) to `Assert.Equal(1, ranHalt.Count);` and
      `Assert.Equal(1, ranProceed.Count);` — EXPECTED-FIRST per the
      recorded swap, no inline-comment emission. Same matcher-routing
      row as the previous construct, with the `reason:` field absent ⇒
      no inline-comment emission needed.
    idiom_id: rf-dart-expect-bare-value-int-to-xunit-assert-equal
    research_finding_id: rf-dart-expect-bare-value-int-to-xunit-assert-equal
    nuance: >-
      Same EXPECTED-FIRST argument-order swap as the reason-bearing
      sibling above. No `reason:` ⇒ no inline-comment emission. The
      `.length` ⇒ `.Count` carry-forward applies identically. Both
      assertions live in the third `[Fact]` method
      (`HaltVsProceedBothTerminate`) — the test compares `halt` and
      `PROCEED` outcomes side by side, and both assertions check the
      ran-goal count is exactly 1.
conversion_units:
  - "using Xunit; (file-level using directive replacing `import 'package:test/test.dart';`)"
  - "using Xunit.Abstractions; (file-level using directive — required for the `ITestOutputHelper` injection introduced by the `dart.core.print` construct)"
  - "using <RootNs>.Runtime; (file-level using directive collapsing four Dart runtime imports — `runtime.dart`, `cells.dart`, `machine_state.dart`, `scheduler.dart` — into the converted `Runtime` sub-namespace per the SUT specs)"
  - "using <RootNs>.Bytecode; (file-level using directive collapsing three Dart bytecode imports — `runner.dart`, `opcodes.dart`, `asm.dart` — into the converted `Bytecode` sub-namespace per the SUT specs)"
  - "public class UtilityInstructionsTest { ... } (single public test class, name mirrors the .dart file name `utility_instructions_test.dart` ⇒ `UtilityInstructionsTest`, no base class needed)"
  - "private readonly ITestOutputHelper _output; public UtilityInstructionsTest(ITestOutputHelper output) { _output = output; } (constructor injection for xUnit per-test stdout capture, required to translate the `print(...)` calls — same pattern as test/test_channel_construction.dart.md)"
  - "[Fact(DisplayName = \"Nop: no operation, just advances PC\")] public void NopNoOperationJustAdvancesPC() { ... } (first Fact, body holds the nop-program assembly + scheduler + drain + Assert.Equal pattern)"
  - "[Fact(DisplayName = \"Halt: terminates execution\")] public void HaltTerminatesExecution() { ... } (second Fact, body holds the halt-program variant)"
  - "[Fact(DisplayName = \"Halt vs Proceed: both terminate\")] public void HaltVsProceedBothTerminate() { ... } (third Fact, body holds two parallel program assemblies and two parallel drains + assertions)"
  - "method bodies: print(...) ⇒ _output.WriteLine(...) at every callsite (14 conversions in total); `${expr}` ⇒ `{expr}` inside `$\"...\"` for the two interpolated diagnostic strings; `\\n` and `✓` characters preserved verbatim"
  - "method bodies: `final rt = GlpRuntime();` ⇒ `var rt = new GlpRuntime();` (and analogously for rt2, env*, runner*, sched*) — `var` + `new` per the carry-forward final-local idiom"
  - "method bodies: `final prog = BC.prog([BC.L('p/0'), BC.TRY(), BC.COMMIT(), BC.nop(), BC.nop(), BC.nop(), BC.PROCEED(), BC.L('p/0_end'), BC.SUSP()])` ⇒ `var prog = BC.Prog(new List<Op> { BC.L(\"p/0\"), BC.TRY(), BC.COMMIT(), BC.Nop(), BC.Nop(), BC.Nop(), BC.PROCEED(), BC.L(\"p/0_end\"), BC.SUSP() });` (and analogously for progHalt + progProceed). The uppercase alias surface is preserved per the asm spec's `rf-dart-uppercase-alias-method-naming`."
  - "method bodies: `final runner = BytecodeRunner(prog);` ⇒ `var runner = new BytecodeRunner(prog);` (and analogously for runnerHalt + runnerProceed)"
  - "method bodies: `final sched = Scheduler(rt: rt, runner: runner);` ⇒ `var sched = new Scheduler(rt: rt, runner: runner);` (named arguments preserved; analogously for schedHalt + schedProceed with rt2 in the proceed case)"
  - "method bodies: `const goalId = 100;` ⇒ `const GoalId goalId = 100;` (and goalId1=100, goalId2=200) — `const` on a method local is a C# compile-time constant; `GoalId` type alias from the SUT machine_state spec"
  - "method bodies: `rt.setGoalEnv(goalId, env);` ⇒ `rt.SetGoalEnv(goalId, env);` (PascalCased method name, args verbatim)"
  - "method bodies: `rt.gq.enqueue(GoalRef(goalId, prog.labels['p/0']!));` ⇒ `rt.Gq.Enqueue(new GoalRef(goalId, prog.Labels[\"p/0\"]!));` (Gq + Enqueue + Labels PascalCased; `new GoalRef(...)`; `!` null-forgiving preserved for forward compatibility with nullable-Labels indexer shape)"
  - "method bodies: `final ran = sched.drain(maxCycles: 10);` ⇒ `var ran = sched.Drain(maxCycles: 10);` (Drain PascalCased; named arg preserved); SUT spec owns the return-type shape"
  - "method bodies: `expect(ran.length, 1, reason: '...');` ⇒ `// <reason>` + `Assert.Equal(1, ran.Count);` (EXPECTED-FIRST swap; `.length` ⇒ `.Count`; reason routed to inline comment because Assert.Equal has no userMessage overload)"
  - "method bodies: `expect(ranHalt.length, 1);` and `expect(ranProceed.length, 1);` ⇒ `Assert.Equal(1, ranHalt.Count);` and `Assert.Equal(1, ranProceed.Count);` (bare bare-value EXPECTED-FIRST swap, no reason)"
  - "NO equivalent of Dart's void main() — xUnit discovery is attribute-driven; registration-via-main is dropped entirely (same as every other test-file conversion in this batch)"
escalations: []
```

## Rationale and research provenance (per non-trivial construct)

### rf-dart-package-test-to-dotnet-xunit — `package:test` ⇒ xUnit framework choice (REUSED)

- **KB reuse (FR-012 / SC-007)**: authoritatively researched and
  recorded in the first test-file spec of this batch
  (`test/smoke_test.dart.md`); every subsequent test convspec
  (`glp_runtime_test`, `test/heap/*`, `test/multiagent/*`,
  `test/analysis/*`, `test/module/*`, `test/conformance/*`,
  `test/test_channel_construction.dart`) reuses it. Authoritative
  sources cited in the originator: Microsoft Learn
  `unit-testing-csharp-with-xunit`, xunit.net,
  pub.dev/package:test, pub.dev/documentation/test/latest/test/test.html.
- **Conclusion**: drop `import 'package:test/test.dart';`, emit
  `using Xunit;` plus (for `print` capture) `using
  Xunit.Abstractions;`. The `.csproj`-level NuGet wiring (xunit,
  xunit.runner.visualstudio, Microsoft.NET.Test.Sdk) is out of scope
  for this per-file artifact (langpair-level emission). Zero
  escalation.

### rf-dart-internal-package-import-to-csharp-using — collapse seven `package:` imports into two `using`s (REUSED)

- **KB reuse (FR-012 / SC-007)**: recorded in the
  `test/conformance/fairness_26_test.dart.md` and `test/heap/*`
  siblings where multiple `package:glp_runtime/runtime/...` imports
  collapsed into a single `using <RootNs>.Runtime;`. Same rule applies
  here for the four runtime imports; an analogous rule applies for
  the three bytecode imports ⇒ one `using <RootNs>.Bytecode;`.
- **Authoritative Dart**: Dart language tour at
  `https://dart.dev/tools/pub/dependencies` and
  `https://dart.dev/guides/libraries/create-packages` — `package:`
  imports are per-file path-based.
- **Authoritative .NET**: Microsoft Learn's C# `using directive`
  reference at
  `https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/using-directive`
  — `using <namespace>;` is per-namespace; multiple Dart imports into
  the same converted namespace collapse to one C# `using`.
- **Conclusion**: emit two `using` directives — one per converted
  sub-namespace (`Runtime`, `Bytecode`). Zero escalation.

### rf-dart-test-main-to-xunit-class-with-facts — `void main() { test(...); test(...); test(...); }` ⇒ class with three `[Fact]`s (REUSED)

- **KB reuse (FR-012 / SC-007)**: structural lift recorded in
  `smoke_test.dart`, `glp_runtime_test.dart`, and
  `fairness_26_test.dart`. Same rule, generalised here from one
  `[Fact]` to three. No `group(...)` in source ⇒ no
  multi-class topology (`varref_pointer_test.dart` uses three classes
  because of three `group`s; here all three tests live on one class).
- **Authoritative sources** (cited in the originators): Microsoft
  Learn xUnit tutorial, xunit.net "Shared Context between Tests",
  pub.dev `test` API reference, Dart language tour.
- **File-specific application**: `utility_instructions_test.dart` ⇒
  `UtilityInstructionsTest.cs` ⇒ `public class UtilityInstructionsTest`;
  three `[Fact]` methods with `DisplayName` preserving the original
  human-readable labels (`'Nop: no operation, just advances PC'`,
  `'Halt: terminates execution'`, `'Halt vs Proceed: both terminate'`).
  PascalCasing strips `: ` / `,` / spaces and uppercases each token;
  the two-letter acronym `PC` stays uppercase per Microsoft's
  "Capitalization Conventions". Zero escalation.

### rf-dart-print-to-xunit-itestoutputhelper-writeline — `print(...)` ⇒ `_output.WriteLine(...)` (REUSED)

- **KB reuse (FR-012 / SC-007)**: recorded in
  `test/test_channel_construction.dart.md` and
  `test/multiagent/mad_cold_call_isolate_test.dart.md`. Same rule
  here for the 14 `print(...)` callsites across the three tests.
- **Authoritative .NET**: xunit.net `Capturing Output` documentation
  at `https://xunit.net/docs/capturing-output` — "xUnit.net captures
  output via the `ITestOutputHelper` interface injected into the
  constructor"; xUnit deliberately does NOT capture `Console.Out` (so
  `Console.WriteLine` is silently swallowed by the test reporter).
- **Authoritative Dart**: Dart core library `print()` documentation at
  `https://api.dart.dev/stable/dart-core/print.html` — "Prints an
  object to the console" (writes to stdout with a trailing newline).
- **Conclusion**: inject `ITestOutputHelper` via the test class
  constructor; store in a `_output` field; emit
  `_output.WriteLine(...)` at every `print(...)` callsite. Zero
  escalation. The `\\n` escape characters and the `✓` UTF-16 glyph
  survive unchanged in C# string literals.

### rf-dart-string-interpolation-to-csharp-dollar-string — Dart `'${expr}'` ⇒ C# `$"{expr}"` (REUSED)

- **KB reuse (FR-012 / SC-007)**: recorded in
  `fairness_26_test.dart.md` and
  `test_channel_construction.dart.md`. Same rule here for the two
  `'Goals executed: ${ran.length}'` interpolations.
- **Authoritative Dart**: Dart language tour
  `https://dart.dev/language/built-in-types#strings`.
- **Authoritative .NET**: Microsoft Learn "Interpolated string
  expressions" at
  `https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/tokens/interpolated`.
- **Conclusion**: emit `$"Goals executed: {ran.Count}"`. The
  `.length` ⇒ `.Count` mapping is owned by
  `rf-dart-list-length-to-csharp-list-count`. Zero escalation.

### rf-dart-final-local-to-csharp-var — `final <local> = <expr>;` ⇒ `var <local> = <expr>;` (REUSED)

- **KB reuse (FR-012 / SC-007)**: recorded in
  `fairness_26_test.dart.md`, `varref_pointer_test.dart.md`,
  `restart_clause1_test.dart.md`, and many more. Same rule for the
  eleven `final` locals in this file (`rt`, `rt2`, `prog`, `progHalt`,
  `progProceed`, `runner`, `runnerHalt`, `runnerProceed`, `sched`,
  `schedHalt`, `schedProceed`, `env`, `env1`, `env2`, `ran`,
  `ranHalt`, `ranProceed`).
- **Authoritative Dart**: Dart language tour at
  `https://dart.dev/language/variables#final-and-const`.
- **Authoritative .NET**: Microsoft Learn `var` reference at
  `https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/statements/declarations`.
- **Conclusion**: `var <name> = new <Ctor>(...);` everywhere. Named
  arguments (`Scheduler(rt: rt, runner: runner)`) preserved verbatim
  per Microsoft Learn "Named and optional arguments". Zero
  escalation.

### rf-dart-const-local-typed-int-to-csharp-const — `const goalId = 100;` ⇒ C# `const` local (REUSED)

- **KB reuse (FR-012 / SC-007)**: recorded in
  `fairness_26_test.dart.md` and `restart_clause1_test.dart.md`. Same
  rule for `goalId` / `goalId1` / `goalId2`.
- **Authoritative Dart**: Dart language tour at
  `https://dart.dev/language/variables#const`.
- **Authoritative .NET**: Microsoft Learn `const` reference at
  `https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/const`.
- **Conclusion**: emit `const GoalId goalId = 100;` (and analogously
  for the other two) — `GoalId` type alias from the SUT machine_state
  spec. Zero escalation.

### rf-dart-camel-to-csharp-pascal-method-rename — `rt.setGoalEnv(...)` ⇒ `rt.SetGoalEnv(...)` (REUSED)

- **KB reuse (FR-012 / SC-007)**: the batch-wide naming convention
  recorded throughout the lib + test specs. Same rule for
  `setGoalEnv` / `gq` / `enqueue` / `labels` / `drain` / `length`
  → `SetGoalEnv` / `Gq` / `Enqueue` / `Labels` / `Drain` / `Count`
  (`length` ⇒ `Count` is a separate idiom row — see below).
- **Authoritative .NET**: Microsoft Learn "C# Coding Conventions" at
  `https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions`
  — "Use PascalCase for ... methods".
- **Conclusion**: PascalCase every public method/property reference.
  Zero escalation.

### rf-dart-bang-null-assertion-to-csharp-null-forgiving — Dart `!` ⇒ C# `!` (FIRST-SEEN in test batch; recorded as a new active idiom)

- **Deep analysis**: the source uses Dart's null-check operator `!`
  on a Dart `Map<Label, int>` indexer lookup
  (`prog.labels['p/0']!`). Dart's `!` asserts "this value is not
  null; throw `TypeError` if it is" — relevant because Dart
  `Map[K]` returns `V?` (nullable) for absent keys. C# has a
  parallel `!` operator (the null-forgiving operator) that suppresses
  nullable-reference-type warnings — but C# `!` is a COMPILE-TIME
  annotation that does NOT throw at runtime.
- **Authoritative Dart**: Dart language tour at
  `https://dart.dev/null-safety#null-check-operator` — "use the
  postfix `!` operator (the null assertion operator) to cast a
  nullable value to its non-nullable underlying type. ... It throws
  if the value is null."
- **Authoritative .NET**: Microsoft Learn "Null-forgiving operator"
  at
  `https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/null-forgiving`
  — "The null-forgiving operator has no effect at run time. It only
  affects the compiler's static flow analysis."
- **Failure-mode nuance (load-bearing, explicitly addressed)**: Dart
  `!` THROWS at runtime on null; C# `!` does NOT throw. For a
  `Dictionary<K,V>` lookup the C# indexer ALREADY THROWS
  `KeyNotFoundException` on absent key (Microsoft Learn
  `Dictionary<TKey,TValue>.this[TKey]` at
  `https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.dictionary-2.item`),
  so the `!` is REDUNDANT on the C# side at runtime. Spec keeps `!`
  for readability AND because if the SUT `BytecodeProgram.Labels`
  property is converted with a nullable-reference-type indexer (e.g.
  `string?`-returning lookup wrapper), the `!` silences the compiler
  warning. The test's `'p/0'` label is always present in the
  hand-assembled program, so neither failure mode fires in practice.
- **Conclusion**: emit C# `!` verbatim (`prog.Labels["p/0"]!`).
  Zero escalation.

### rf-dart-list-literal-to-csharp-list-initializer — `BC.prog([...])` ⇒ `BC.Prog(new List<Op> { ... })` (REUSED)

- **KB reuse (FR-012 / SC-007)**: recorded in
  `varref_pointer_test.dart.md` and elsewhere in the batch. Same
  rule for the three `BC.prog([...])` callsites here.
- **Authoritative Dart**: Dart language tour at
  `https://dart.dev/language/collections#lists` — `[<expr>, <expr>,
  ...]` literal.
- **Authoritative .NET**: Microsoft Learn "Object and collection
  initializers" at
  `https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/object-and-collection-initializers`
  and "Collection expressions" at
  `https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/collection-expressions`.
- **Conclusion**: emit `BC.Prog(new List<Op> { BC.L("p/0"), BC.TRY(),
  ... })`. The `Op` element-type binding is the converted opcode
  interface (per `lib/bytecode/opcodes.dart.md`). The
  UPPERCASE-vs-lowerCase alias surface inside the list elements is
  preserved per the asm spec's `rf-dart-uppercase-alias-method-naming`.
  Zero escalation.

### rf-dart-method-call-with-named-arg-to-csharp-method-call-with-named-arg — `sched.drain(maxCycles: 10)` ⇒ `sched.Drain(maxCycles: 10)`

- **Deep analysis**: Dart supports named arguments at the call site
  (`drain(maxCycles: 10)`); C# also supports named arguments at the
  call site since C# 4.0.
- **Authoritative Dart**: Dart language tour at
  `https://dart.dev/language/functions#named-parameters`.
- **Authoritative .NET**: Microsoft Learn "Named and optional
  arguments" at
  `https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/named-and-optional-arguments`.
- **Conclusion**: emit `Drain(maxCycles: 10)` verbatim. Parameter
  name `maxCycles` carries through unchanged (parameter names stay
  lowerCamelCase per the SUT scheduler spec). Zero escalation.

### rf-dart-list-length-to-csharp-list-count — `<list>.length` ⇒ `<list>.Count` (REUSED)

- **KB reuse (FR-012 / SC-007)**: batch-wide carry-forward; the
  Dart-`List`-`length`-property ⇒ C#-`List`-`Count`-property mapping
  appears in many sibling specs.
- **Authoritative Dart**: Dart core library `Iterable.length` at
  `https://api.dart.dev/stable/dart-core/Iterable/length.html`.
- **Authoritative .NET**: Microsoft Learn
  `ICollection<T>.Count` at
  `https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.icollection-1.count`
  (and `IReadOnlyCollection<T>.Count` /
  `List<T>.Count`).
- **Conclusion**: `ran.length` ⇒ `ran.Count`. O(1) on both sides.
  Note that for `String` the equivalent C# property is
  `string.Length` (capital `L`) — DIFFERENT idiom, not used here.
  Zero escalation.

### rf-dart-expect-bare-value-int-to-xunit-assert-equal — `expect(actual, value [, reason: msg])` ⇒ `Assert.Equal(value, actual)` (REUSED)

- **KB reuse (FR-012 / SC-007)**: recorded in
  `glp_runtime_test.dart.md` and `fairness_26_test.dart.md`. Same
  rule for the four `expect(<list>.length, 1, ...)` callsites here
  (two with `reason:`, two without).
- **Authoritative Dart**: pub.dev
  `https://pub.dev/documentation/test_api/latest/expect/expect.html`
  — bare-value second argument is wrapped in `equals(value)`.
- **Authoritative .NET**: xunit.net `Assert` API reference for
  `Equal<T>(T expected, T actual)` — verbatim EXPECTED-FIRST argument
  order; no `userMessage` overload (deliberate xUnit design — the
  value diff IS the diagnostic).
- **EXPECTED-FIRST swap (load-bearing)**: Dart `expect(actual,
  value)` is ACTUAL-FIRST; xUnit `Assert.Equal<T>(expected, actual)`
  is EXPECTED-FIRST. The conversion explicitly swaps the argument
  order — a naive textual transposition would still pass on success
  but produce reversed diagnostics on failure.
- **`reason:` handling**: the two `reason:`-bearing calls here have
  their `reason:` text routed to an inline `// ...` comment because
  xUnit's `Assert.Equal<T>` has no `userMessage` overload — same
  treatment as `fairness_26_test.dart.md`'s
  `expect(rt.budgetOf(g), 26, reason: 'budget resets after
  yielding')` ⇒ `// budget resets after yielding` +
  `Assert.Equal(26, rt.BudgetOf(g));`.
- **Conclusion**: `// Should execute through all nops and succeed` +
  `Assert.Equal(1, ran.Count);` (first Fact);
  `// Should halt and terminate` + `Assert.Equal(1, ran.Count);`
  (second Fact); bare `Assert.Equal(1, ranHalt.Count);` and
  `Assert.Equal(1, ranProceed.Count);` (third Fact). Zero escalation.

## Notes

- No async / `Future` / `Stream` / isolate / `Completer` / `Timer`
  surface in this file — every `[Fact]` method is synchronous `void`
  (NOT `async Task`). The well-known async-Dart-vs-.NET-async nuance
  is deliberately not asserted here (does not apply to this file's
  source surface). NOTE: `Scheduler.drain(...)` is the SYNCHRONOUS
  drain entry point on the SUT side; `drainAsyncWithStatus` exists
  per the SUT scheduler spec but is NOT called here.
- No `late`, `mixin`, `extension`, generics on the test surface,
  sealed/abstract, bitwise/shift, isolate, or null-safety variance —
  all absent in this file's source. The `!` null-assertion operator
  DOES appear (three callsites — one per `BC.prog` builder), handled
  by its dedicated construct above.
- The file exercises the bytecode-VM utility-opcode surface
  (`nop`, `halt`, `PROCEED`) plus the scheduler/runner harness. Every
  SUT-side type/name (`GlpRuntime`, `Scheduler`, `BytecodeRunner`,
  `BC`, `BytecodeProgram.labels`, `GoalRef`, `CallEnv`,
  `GlpRuntime.setGoalEnv` / `.gq`, `Scheduler.drain`) is owned by the
  corresponding SUT convspec — this convspec REUSES every shape via
  the namespace `using`s and the PascalCased member-name carry-
  forward; it does NOT re-derive any SUT-side decision.
- Reference-vs-value: every test-side object (`GlpRuntime`,
  `Scheduler`, `BytecodeRunner`, `BC` is a static helper class so
  has no instance, `CallEnv`, `BytecodeProgram`) is a reference type
  in both Dart and C# per the SUT specs. The `GoalRef` and
  `GoalId` types may be value types in C# (per the SUT
  `goal_queue.dart.md` / `machine_state.dart.md` shape) but this
  test's positional/named uses of them are reference-identity-
  insensitive — no equality comparison, no identity assertion.
- Idiom reuse summary: 11 of the 12 non-trivial construct rows
  REUSE existing batch-wide idioms via the KB
  (`rf-dart-package-test-to-dotnet-xunit`,
  `rf-dart-internal-package-import-to-csharp-using`,
  `rf-dart-test-main-to-xunit-class-with-facts`,
  `rf-dart-print-to-xunit-itestoutputhelper-writeline`,
  `rf-dart-string-interpolation-to-csharp-dollar-string`,
  `rf-dart-final-local-to-csharp-var`,
  `rf-dart-const-local-typed-int-to-csharp-const`,
  `rf-dart-camel-to-csharp-pascal-method-rename`,
  `rf-dart-list-literal-to-csharp-list-initializer`,
  `rf-dart-method-call-with-named-arg-to-csharp-method-call-with-named-arg`,
  `rf-dart-list-length-to-csharp-list-count`,
  `rf-dart-expect-bare-value-int-to-xunit-assert-equal`). The one
  first-seen-in-this-file idiom is
  `rf-dart-bang-null-assertion-to-csharp-null-forgiving` — the
  Dart `!` null-assertion operator was not used in earlier test
  convspecs in the batch (this is its first occurrence), so this
  spec defines it as a new active idiom row with its failure-mode
  nuance recorded.
- Zero escalations: every construct in this file is
  authoritative-supported on both sides; 11 reuse idioms from the
  KB and 1 defines a new active idiom with both-side authoritative
  citations.
