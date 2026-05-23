---
path: test/test_agent_init_goal.dart
cycle_group_id: 160
scc_siblings: []
generated_at: 2026-05-21T16:44:58Z
source_sha256: 7733bef617eea001d86bc8a9e045b14a83c5490d03ed9ba20318d1090b09d122
schema_version: 1
---

# Conversion Plan: test/test_agent_init_goal.dart

## 1. Source Analysis

Direct inspection of `glp_runtime_net/test/test_agent_init_goal.dart`
(119 source lines, sha256
`7733bef617eea001d86bc8a9e045b14a83c5490d03ed9ba20318d1090b09d122`):

- File-header `///` doc comment (line 1): "Test to debug agent_init goal
  setup - mimics Flutter app behavior".
- Eight imports (lines 2–9): one `dart:io` (exercised: `File(...).
  readAsStringSync()` only) + seven `package:glp_runtime/...` internal
  imports — `runtime/runtime.dart` (GlpRuntime), `runtime/terms.dart`
  (Term/VarRef/ConstTerm), `runtime/external_io.dart` (createExternalChannel,
  buildChannelTerm), `runtime/machine_state.dart` (GoalRef/CallEnv
  re-export), `runtime/scheduler.dart` (Scheduler), `bytecode/runner.dart`
  (BytecodeRunner, canonical CallEnv), `compiler/compiler.dart` (GlpCompiler).
- Single top-level `void main() async` entrypoint (line 11) — `async`
  keyword present BUT body contains zero `await` calls (verified by
  full-file inspection: lines 12–118 contain no `await` token).
- Body shape (debug-script — NO `package:test` import, NO
  `test(...)`/`group(...)`/`expect(...)`, NO matchers):
  - Lines 12, 26, 28, 29, 41–45, 51, 52, 58–61, 66–69, 74–77, 86, 90,
    106, 113–117: ~25 `print(...)` calls — banner + per-arg
    diagnostics + scheduler-trace banner + result-fields. All
    `print` arguments are either plain strings or interpolated
    strings (`'... $x ...'` or `'... ${expr} ...'`).
  - Line 15: `final glpSource = File('../programs/multiagent/
    social_agent_v2.glp').readAsStringSync();` — sync file read via
    `dart:io File` ctor + instance method.
  - Lines 18–19: `final userCompiler = GlpCompiler(); final
    userProgram = userCompiler.compile(glpSource);` — implicit-new
    constructor + instance method call.
  - Line 22: `final combinedProgram = userProgram;` — redundant
    alias rebind, kept verbatim (documents Dart-source intent).
  - Line 25: `final entryPC = combinedProgram.labels['agent_init/3'];`
    — Dart `Map<String,int>.operator[]` returns `int?`.
  - Lines 27–31: `if (entryPC == null) { ...; return; }` — early
    return with diagnostic prints and `combinedProgram.labels.keys.
    take(20)` interpolation.
  - Lines 34–35: `final rt = GlpRuntime(); final heap = rt.heap;`.
  - Lines 38–39: two `createExternalChannel(heap, 'user'|'net')`
    calls.
  - Lines 48–49: two `buildChannelTerm(...)` calls.
  - Lines 56, 64, 72: three positional-record-destructuring
    `final (arg<i>Writer, arg<i>Reader) = heap.allocateVariable();`
    locals (Dart 3 records).
  - Lines 57, 65, 73: three `heap.bindVariable(<writer>, <term>);`
    statement-expressions — return value `List<SuspensionRecord>`
    DISCARDED.
  - Lines 80–84: `final argSlots = <int, Term>{ 0: VarRef(arg0Reader),
    1: ..., 2: ... };` — typed map literal with three entries.
  - Lines 87–92: `for (final entry in argSlots.entries) { final
    term = entry.value; if (term is VarRef) { print('  ${entry.key}:
    VarRef(${term.addr}), isReader=${heap.isReader(term.addr)}'); } }`
    — map-entries foreach with type-test flow-narrowing.
  - Lines 95–97: `final env = CallEnv(args: argSlots); rt.setGoalEnv(
    100, env); rt.setGoalProgram(100, 'main');` — named-arg ctor +
    two runtime setters with `100` goal-id literal.
  - Lines 100–101: `final runner = BytecodeRunner(combinedProgram);
    final scheduler = Scheduler(rt: rt, runners: {'main': runner});`
    — implicit-new + named args + inline map literal.
  - Line 104: `rt.gq.enqueue(GoalRef(100, entryPC));` — goal-queue
    enqueue with `GoalRef` ctor (positional `(int kappa, int pc)`);
    `entryPC` is null-narrowed by the earlier early-return guard.
  - Lines 107–111: `final result = scheduler.drainWithStatus(
    maxCycles: 100, debug: true, debugOutput: true);` — synchronous
    drain (NOT `drainAsyncWithStatus`).
  - Lines 114–117: four `print('<label>: ${result.<field>}')`
    diagnostics (Status / GoalsRan / SuspendedGoals / BlockingReaders).
  - End-of-function fall-through (no final `return;`).

Zero assertions, zero matchers, zero `package:test` surface. Pure
diagnostic-script shape. The doc-comment "mimics Flutter app
behavior" is descriptive — the file is a CLI `dart run` script, not
a Flutter-runtime artefact.

## 2. Dart → C#/.NET Conversion Plan

Each construct from the ratified convspec is restated here verbatim
in mirror with its target C# decision. The host shape is `public
static class TestAgentInitGoal` with a `public static int Main(string[]
args)` entrypoint (the debug_negative.dart precedent), NOT a `[Fact]`
xUnit class (no `package:test` surface to convert).

### 2.1. File-header doc comment

- Construct: `dart.doc_comment.file_header_triple_slash`.
- Dart: `/// Test to debug agent_init goal setup - mimics Flutter app behavior`
- C#/.NET: single-line `///` XML-doc comment placed verbatim above
  `public static class TestAgentInitGoal`. No `<summary>` wrapping
  (the original is one-line preamble).
- Idiom: `rf-dart-tripleslash-doc-to-csharp-xml-doc`.

### 2.2. `dart:io` import (only `File(...).readAsStringSync()` exercised)

- Construct: `dart.import.dart_io_file_only_sync_read`.
- Dart: `import 'dart:io';`
- C#/.NET: `using System.IO;` at file scope; call site emitted as
  `File.ReadAllText("../programs/multiagent/social_agent_v2.glp")`
  (static method, .NET-canonical synchronous UTF-8 file-text reader).
  The Dart two-step (construct `File` handle + call instance method)
  collapses into the C# one-step (static call). Sync-vs-async
  preserved: emit `File.ReadAllText`, NOT
  `await File.ReadAllTextAsync`.
- Idiom: `rf-dart-dart-io-file-readasstringsync-to-system-io-file-readalltext`.

### 2.3. Seven `package:glp_runtime/...` internal imports

- Construct: `dart.import.package_internal_eight_imports`.
- Dart: seven `package:` imports (five `runtime/`, one `bytecode/runner.dart`,
  one `compiler/compiler.dart`).
- C#/.NET: collapse to three `using` directives in cu-1:
  `using <RootNs>.Runtime;` (all five `runtime/` files share that
  namespace per their SUT convspecs), `using <RootNs>.Bytecode;`,
  `using <RootNs>.Compiler;`. The `CallEnv` ambiguity (canonical
  in `bytecode/runner.dart`, re-exported from `machine_state.dart`)
  resolves at the namespace level — both resolve to
  `<RootNs>.Bytecode.CallEnv` (single source of truth per
  runner.dart.md). No `as` alias needed.
- Idiom: `rf-dart-internal-package-import-to-csharp-using`.

### 2.4. `void main() async` entrypoint

- Construct: `dart.test_file.void_main_async_as_dart_run_entrypoint`.
- Dart: `void main() async { ... }` — `async` present, ZERO `await`
  in body (verified).
- C#/.NET: `public static int Main(string[] args) { ... return 0; }`
  inside `public static class TestAgentInitGoal`. The Dart `async`
  keyword is DROPPED because the body contains no `await` (faithful
  per Microsoft Learn 'Main method and command-line arguments').
  NOT `async Task<int> Main(...)` (would allocate an
  async-state-machine for nothing). NOT `[Fact]` (no assertions).
  Codegen verifies zero-`await` precondition before dropping — check
  passes for this file.
- Idiom: `rf-dart-debug-script-main-to-csharp-static-main`.

### 2.5. `print(...)` calls (~25 occurrences)

- Construct: `dart.core.print`.
- Dart: each `print(<string-or-interpolated>)`.
- C#/.NET: each maps to `Console.WriteLine(<same>)` (`using System;`).
  Routed to `Console.WriteLine` (NOT `ITestOutputHelper.WriteLine`)
  because the host is `static Main` console-exe, not `[Fact]`.
  Embedded literal `\n` characters inside string arguments preserved
  verbatim (WriteLine appends an additional newline — identical
  observable behaviour to Dart `print`).
- Idiom: `rf-dart-print-in-console-exe-to-console-writeline`.

### 2.6. String interpolation

- Construct: `dart.string.interpolation`.
- Dart: `'... $x ...'` and `'... ${expr} ...'`.
- C#/.NET: `$"... {X} ..."` and `$"... {Expr} ..."`. Every camelCase
  identifier inside an interpolation expression is RE-EMITTED with
  the PascalCased property name decided by its OWNING SUT convspec:
  `userChannel.InputWriterAddr` / `InputReaderAddr` /
  `OutputWriterAddr` / `OutputReaderAddr` (external_io.dart.md);
  `Heap.IsWriterBound` / `Heap.IsReaderBound` / `Heap.GetReaderValue`
  / `Heap.IsReader` (heap_fcp.dart.md);
  `combinedProgram.Labels.Keys.Take(20)` (runner.dart.md + LINQ —
  requires `using System.Linq;`); `term.Addr` (terms.dart.md);
  `result.Status` / `GoalsRan` / `SuspendedGoals` / `BlockingReaders`
  (scheduler.dart.md DrainResult); `entry.Key` (KeyValuePair).
- Idiom: `rf-dart-string-interpolation-to-csharp-interpolated-string`.

### 2.7. `final` local with inferred type

- Construct: `dart.local_var.final_inferred_type`.
- Dart: each `final <name> = <expr>;`.
- C#/.NET: `var <name> = <expr>;`. Every local in this file is
  single-assignment (verified) so `var` is faithful. The three
  record-destructuring locals are handled by §2.10 below.
- Idiom: `rf-dart-final-local-to-csharp-var-local`.

### 2.8. Implicit-`new` constructor calls

- Construct: `dart.constructor_call.implicit_new`.
- Dart: `GlpCompiler()`, `GlpRuntime()`, `BytecodeRunner(...)`,
  `Scheduler(...)`, `CallEnv(...)`, `VarRef(...)`, `ConstTerm('alice')`,
  `GoalRef(100, entryPC)` (and `File(...)` — collapsed to static
  call per §2.2).
- C#/.NET: `new T(...)` with positional ordering. Concrete emissions:
  `new GlpCompiler()`, `new GlpRuntime()`,
  `new BytecodeRunner(combinedProgram)`, `new Scheduler(rt: rt,
  runners: new Dictionary<string, BytecodeRunner> { { "main",
  runner } })`, `new CallEnv(argSlots)`, `new VarRef(arg<i>Reader)`,
  `new ConstTerm("alice")`, `new GoalRef(100, entryPC)` (where
  `entryPC` is the `int` out-binding from `TryGetValue` per §2.13).
  Codegen MUST consult each owning SUT convspec for the canonical
  ctor signature (do not mechanically copy Dart positional/named
  shape).
- Idiom: `rf-dart-implicit-new-to-csharp-explicit-or-targettyped-new`.

### 2.9. Single-quoted string literals

- Construct: `dart.string.single_quoted_literal`.
- Dart: `'user'`, `'net'`, `'alice'`, `'agent_init/3'`, `'main'`,
  `'../programs/multiagent/social_agent_v2.glp'`.
- C#/.NET: each `"..."` (double-quoted). Path literal preserved
  verbatim (forward slashes are accepted on Windows by
  `File.ReadAllText`). No raw / verbatim string treatment needed.
- Idiom: `rf-dart-single-quoted-string-to-csharp-double-quoted-string`.

### 2.10. Positional-record destructuring (3 sites)

- Construct: `dart.tuple.record_destructuring_two_int_addresses`.
- Dart: `final (arg<i>Writer, arg<i>Reader) = heap.allocateVariable();`
  (three sites).
- C#/.NET: `var (arg<i>Writer, arg<i>Reader) = heap.AllocateVariable();`
  (three sites). Per heap_fcp.dart.md the return type is
  `(long, long)` `ValueTuple<long, long>`; the destructured locals
  are `long`-typed (the int-width-identity invariant per
  cells.dart.md / heap_fcp.dart.md). The downstream `VarRef`
  constructor accepts `long`.
- Idiom: `rf-dart-record-return-to-csharp-valuetuple`.

### 2.11. `compiler.compile(...)` instance call

- Construct: `dart.method_call.compiler_compile`.
- Dart: `final userProgram = userCompiler.compile(glpSource);`.
- C#/.NET: `var userProgram = userCompiler.Compile(glpSource);` —
  PascalCase rename per compiler.dart.md (`BytecodeProgram
  Compile(string source)` signature). Optional second parameter not
  exercised.
- Idiom: `rf-dart-instance-method-camelcase-to-csharp-pascalcase`.

### 2.12. Heap query methods family

- Construct: `dart.method_call.heap_query_returning_bool_or_term_or_unit`.
- Dart: `heap.bindVariable(...)` (3 calls, return value discarded),
  `heap.isWriterBound(...)`, `heap.isReaderBound(...)`,
  `heap.getReaderValue(...)`, `heap.isReader(...)`.
- C#/.NET: `Heap.BindVariable(...)` (statement-expression form —
  the returned `List<SuspensionRecord>` is discarded, NOT
  captured into a local; scheduler drains activations via
  `Gq` on next step), `Heap.IsWriterBound(...) → bool`,
  `Heap.IsReaderBound(...) → bool`,
  `Heap.GetReaderValue(...) → Term?`,
  `Heap.IsReader(...) → bool`. All PascalCase per heap_fcp.dart.md.
- Idiom: `rf-dart-instance-method-camelcase-to-csharp-pascalcase`
  (with `rf-dart-bind-writer-family-callsite-to-csharp-pascalcase-methods`
  research finding for the bind-discard nuance).

### 2.13. `Map<K,V>.operator[]` nullable indexer + early return

- Construct: `dart.map_lookup.nullable_return_from_string_keyed_map`.
- Dart: `final entryPC = combinedProgram.labels['agent_init/3']; if
  (entryPC == null) { ...prints...; return; }`.
- C#/.NET: LOAD-BEARING — DO NOT use the `Dictionary[K]` indexer
  (it throws `KeyNotFoundException` on miss). Emit:
  ```csharp
  if (!combinedProgram.Labels.TryGetValue("agent_init/3", out var entryPC))
  {
      Console.WriteLine("ERROR: agent_init/3 not found!");
      Console.WriteLine($"Available labels: {string.Join(", ", combinedProgram.Labels.Keys.Take(20))}...");
      return 0;
  }
  ```
  After this, `entryPC` is non-null `int` for the remainder of
  `Main`. `string.Join(", ", ...)` preserves the Dart-print
  fidelity (latent enhancement; alternative is direct
  `IEnumerable.ToString()` which would print
  `"System.Linq.Enumerable+TakeIterator..."`).
- Idiom: `rf-dart-map-indexer-nullable-to-csharp-trygetvalue`.

### 2.14. Field/property chains (camelCase → PascalCase)

- Construct: `dart.member_access.field_chain`.
- Dart: `rt.heap`, `rt.gq`, `userChannel.input{Writer,Reader}Addr` /
  `output{Writer,Reader}Addr`, `term.addr`, `entry.key` / `value`,
  `result.status` / `goalsRan` / `suspendedGoals` / `blockingReaders`,
  `combinedProgram.labels`.
- C#/.NET: PascalCase property reads per each owning SUT convspec:
  `rt.Heap`, `rt.Gq`, `userChannel.InputWriterAddr` /
  `InputReaderAddr` / `OutputWriterAddr` / `OutputReaderAddr`,
  `term.Addr`, `entry.Key` / `entry.Value`,
  `result.Status` / `GoalsRan` / `SuspendedGoals` /
  `BlockingReaders`, `combinedProgram.Labels`.
- Idiom: `rf-dart-camelcase-field-to-csharp-pascalcase-property`.

### 2.15. Top-level external-io helpers

- Construct: `dart.function_call.top_level_external_io_helpers`.
- Dart: `createExternalChannel(heap, 'user'|'net')` (2 calls),
  `buildChannelTerm(<ch>)` (2 calls).
- C#/.NET: hoisted onto the `ExternalIo` host static class per
  external_io.dart.md:
  `ExternalIo.CreateExternalChannel(heap, "user")`,
  `ExternalIo.CreateExternalChannel(heap, "net")`,
  `ExternalIo.BuildChannelTerm(userChannel)`,
  `ExternalIo.BuildChannelTerm(netChannel)`.
- Idiom: `rf-dart-top-level-function-callsite-to-csharp-static-method`.

### 2.16. Named arguments at ctor / method call sites

- Construct: `dart.named_argument.constructor_or_method_call`.
- Dart: `CallEnv(args: argSlots)`, `Scheduler(rt: rt, runners:
  {...})`, `scheduler.drainWithStatus(maxCycles: 100, debug: true,
  debugOutput: true)`.
- C#/.NET: spec preference — emit positional for `CallEnv` (single
  param, naming adds no clarity): `new CallEnv(argSlots)`; emit
  NAMED-arg syntax for `Scheduler` and `DrainWithStatus` (parameter
  names carry meaning): `new Scheduler(rt: rt, runners: new
  Dictionary<string, BytecodeRunner> { { "main", runner } })` and
  `scheduler.DrainWithStatus(maxCycles: 100, debug: true,
  debugOutput: true)`. NOT object-initialiser/`init`-only properties.
- Idiom: `rf-dart-named-arguments-to-csharp-named-arguments-or-positional`.

### 2.17. Typed map literal `<int, Term>{...}`

- Construct: `dart.map_literal.typed_int_term_with_constructor_calls`.
- Dart: `final argSlots = <int, Term>{ 0: VarRef(arg0Reader),
  1: ..., 2: ... };`.
- C#/.NET:
  ```csharp
  var argSlots = new Dictionary<int, Term>
  {
      { 0, new VarRef(arg0Reader) },
      { 1, new VarRef(arg1Reader) },
      { 2, new VarRef(arg2Reader) },
  };
  ```
  Key type `int` (slot index — not a heap address; the address-width
  `long` rule applies only to heap addresses per cells.dart.md).
  Value type `Term` (terms.dart.md sum-type hierarchy); `VarRef` is a
  `Term` subtype and boxes transparently.
- Idiom: `rf-dart-typed-map-literal-to-csharp-dictionary-collection-init`.

### 2.18. Runtime per-goal setters

- Construct: `dart.method_call.set_goal_env_and_program_on_runtime`.
- Dart: `rt.setGoalEnv(100, env);  rt.setGoalProgram(100, 'main');`.
- C#/.NET: `rt.SetGoalEnv(100, env);  rt.SetGoalProgram(100, "main");`.
  PascalCase rename per runtime.dart.md. The `100` goal-id is `int`
  (not `long`; goal-ids are bounded counter values per runtime.dart.md).
- Idiom: `rf-dart-instance-method-camelcase-to-csharp-pascalcase`.

### 2.19. Goal-queue enqueue

- Construct: `dart.method_call.gq_enqueue_goalref`.
- Dart: `rt.gq.enqueue(GoalRef(100, entryPC));`.
- C#/.NET: `rt.Gq.Enqueue(new GoalRef(100, entryPC));`. The `entryPC`
  is the non-null `int` from the `TryGetValue` `out var` binding per
  §2.13 — no `.Value` access needed. `GoalRef` positional ctor
  `(int kappa, int pc)` per machine_state.dart.md.
- Idiom: `rf-dart-instance-method-camelcase-to-csharp-pascalcase`.

### 2.20. `Map.entries` foreach with type-test flow-narrowing

- Construct: `dart.foreach.iterate_map_entries_with_destructure_print`
  (+ inner `dart.var_loop_local`).
- Dart:
  ```dart
  for (final entry in argSlots.entries) {
    final term = entry.value;
    if (term is VarRef) {
      print('  ${entry.key}: VarRef(${term.addr}), isReader=${heap.isReader(term.addr)}');
    }
  }
  ```
- C#/.NET:
  ```csharp
  foreach (var entry in argSlots)
  {
      var term = entry.Value;
      if (term is VarRef varRef)
      {
          Console.WriteLine($"  {entry.Key}: VarRef({varRef.Addr}), isReader={Heap.IsReader(varRef.Addr)}");
      }
  }
  ```
  Note `.entries` is DROPPED (C# `Dictionary<K,V>` IS
  `IEnumerable<KeyValuePair<K,V>>`); `if (term is VarRef varRef)`
  declares the narrowed local in one step.
- Idiom: `rf-dart-map-entries-iteration-to-csharp-dictionary-foreach`
  (+ `rf-dart-final-local-to-csharp-var-local` for the loop locals).

### 2.21. Synchronous `scheduler.drainWithStatus(...)`

- Construct: `dart.scheduler.drain_with_status_named_args_synchronous`.
- Dart: `final result = scheduler.drainWithStatus(maxCycles: 100,
  debug: true, debugOutput: true);`.
- C#/.NET: `var result = scheduler.DrainWithStatus(maxCycles: 100,
  debug: true, debugOutput: true);` — synchronous, NOT
  `await scheduler.DrainWithStatusAsync(...)` (only the SEPARATE
  `drainAsyncWithStatus` per scheduler.dart.md is async; this file
  uses the sync variant). Returns the reference `DrainResult`.
  Threading-model inheritance: `Main` runs on the process's main
  thread which IS the owning context for the single-agent
  diagnostic (no thread-marshalling required); inherited from
  heap_fcp.dart.md escalations[0] per FR-013.
- Idiom: `rf-dart-instance-method-camelcase-to-csharp-pascalcase`.

### 2.22. Early-return `return;` in `void main()` + final fall-through

- Construct: `dart.early_return.bare_return_in_void_main`.
- Dart: `return;` (line 31, inside the null-`entryPC` branch); plus
  natural end-of-function fall-through after the final print
  (line 117).
- C#/.NET: `int Main` requires explicit int return — emit `return 0;`
  for BOTH the mid-function early-return (preserves Dart's exit
  code 0 semantics) AND the final fall-through (preserves the
  Dart-side clean-exit on the success path). NOT `return 1;` — Dart's
  bare `return` yields exit code 0 by default.
- Idiom: `rf-dart-void-main-bare-return-to-csharp-int-main-return-zero`.

### 2.23. Conversion-unit (target-file) layout (cu-1 … cu-15)

Per the convspec, the C# target file `test/TestAgentInitGoal.cs` is
laid out in 15 conversion-units (cu-1 = file-scope `using`s, cu-2 =
namespace, cu-3 = XML-doc, cu-4 = host class, cu-5 = Main signature,
cu-6 = source-load + label-lookup branch, cu-7 = runtime/channel
setup, cu-8 = three writer/reader allocate+bind+diagnostic blocks,
cu-9 = argSlots map, cu-10 = argSlots foreach diagnostic, cu-11 =
CallEnv + per-goal setters, cu-12 = runner + Scheduler, cu-13 =
queue-enqueue, cu-14 = drain banner + DrainWithStatus call, cu-15 =
result diagnostics + final `return 0;`). The plan preserves this
layout verbatim — codegen consumes the convspec's `conversion_units:`
list as the structural outline for `test/TestAgentInitGoal.cs`.

## 3. Decomposed Task Units

- T1: Compute and pin source sha256
  `7733bef617eea001d86bc8a9e045b14a83c5490d03ed9ba20318d1090b09d122` (done).
- T2: Choose host shape — `public static class TestAgentInitGoal`
  with `public static int Main(string[] args)` entrypoint
  (debug_negative.dart precedent — done; explicitly addressed
  via construct §2.4).
- T3: Drop the Dart `async` keyword on `main()` after verifying
  zero `await` occurrences in the body (verified — done).
- T4: Emit cu-1 file-scope `using` directives — `using System;`,
  `using System.Collections.Generic;`, `using System.IO;`,
  `using System.Linq;`, `using <RootNs>.Runtime;`, `using <RootNs>.
  Bytecode;`, `using <RootNs>.Compiler;` (done).
- T5: Emit cu-3 XML-doc above the host class verbatim
  (`/// Test to debug agent_init goal setup - mimics Flutter app
  behavior`) (done).
- T6: Route every `print(...)` to `Console.WriteLine(...)`
  (host-shape-conditional — `static Main` not `[Fact]`) (done).
- T7: Map `File('path').readAsStringSync()` to
  `File.ReadAllText("path")` static call (done).
- T8: Map nullable `Map[K]` indexer to
  `Dictionary<K,V>.TryGetValue(K, out var v)` with the early-return
  branch emitting `Console.WriteLine` diagnostics + `return 0;`
  (done — LOAD-BEARING semantic correction).
- T9: Map the three `final (writer, reader) = heap.allocateVariable();`
  destructuring lines to C# `var (writer, reader) =
  heap.AllocateVariable();` with `long` addresses (done).
- T10: Map `<int, Term>{0: VarRef(...), 1: ..., 2: ...}` to
  `new Dictionary<int, Term> { { 0, new VarRef(arg0Reader) }, ... }`
  (int key — slot index, not heap address) (done).
- T11: Map `for (final entry in argSlots.entries)` to
  `foreach (var entry in argSlots)` (drop `.entries`) and
  `if (term is VarRef)` to `if (term is VarRef varRef)`
  (type-test flow-narrowing) (done).
- T12: Map `Scheduler(rt: rt, runners: {...})` and
  `drainWithStatus(maxCycles: 100, debug: true, debugOutput: true)`
  using NAMED-argument C# syntax (spec preference for
  meaning-carrying parameter names) (done).
- T13: Map `rt.gq.enqueue(GoalRef(100, entryPC))` to
  `rt.Gq.Enqueue(new GoalRef(100, entryPC))` — `entryPC` is the
  non-null `int` out-binding from T8 (done).
- T14: Apply PascalCase rename to every field/property/method
  identifier per each owning SUT convspec (done — exhaustively
  listed across §2.6, §2.12, §2.14, §2.18).
- T15: Emit `return 0;` for the mid-function early-return AND for
  the final fall-through to preserve Dart's exit-code-0
  semantics (done).
- T16: Apply `string.Join(", ", ...)` inside the
  `combinedProgram.Labels.Keys.Take(20)` interpolation to preserve
  Dart-print diagnostic fidelity (latent enhancement per
  convspec Notes (a); done).
- T17: Preserve the redundant `var combinedProgram = userProgram;`
  alias rebind (no-op the C# compiler optimises away; documents
  Dart-source intent per convspec Notes) (done).
- T18: Verify zero LOCAL undecidable points — confirmed:
  threading-model inherits from heap_fcp.dart.md escalations[0]
  per FR-013, NOT re-escalated here (done).

## 4. Research Findings

none required — all 23 constructs resolve via cached KB idioms
(`rf-dart-internal-package-import-to-csharp-using`,
`rf-dart-debug-script-main-to-csharp-static-main`,
`rf-dart-print-in-console-exe-to-console-writeline`,
`rf-dart-final-local-to-csharp-var-local`,
`rf-dart-implicit-new-to-csharp-explicit-or-targettyped-new`,
`rf-dart-record-return-to-csharp-valuetuple`,
`rf-dart-single-quoted-string-to-csharp-double-quoted-string`,
`rf-dart-string-interpolation-to-csharp-interpolated-string`,
`rf-dart-instance-method-camelcase-to-csharp-pascalcase`,
`rf-dart-bind-writer-family-callsite-to-csharp-pascalcase-methods`,
`rf-dart-top-level-function-callsite-to-csharp-static-method`,
`rf-dart-camelcase-field-to-csharp-pascalcase-property`,
`rf-dart-tripleslash-doc-to-csharp-xml-doc`) plus six NEW idioms
introduced by this file's convspec that each have a single
authoritative target shape from Microsoft Learn documentation
(`rf-dart-dart-io-file-readasstringsync-to-system-io-file-readalltext`,
`rf-dart-map-indexer-nullable-to-csharp-trygetvalue`,
`rf-dart-named-arguments-to-csharp-named-arguments-or-positional`,
`rf-dart-typed-map-literal-to-csharp-dictionary-collection-init`,
`rf-dart-map-entries-iteration-to-csharp-dictionary-foreach`,
`rf-dart-void-main-bare-return-to-csharp-int-main-return-zero`). All
decisions are verbatim-derivable from the convspec + cited SUT
convspecs + Microsoft Learn pages referenced in the convspec's
"Rationale + research provenance" section. Inherited threading-model
escalation from heap_fcp.dart.md escalations[0] is NOT a new finding
(FR-013 + documented sibling-multiagent precedent).

## 5. Consistency Pass

fixed — derived from the convspec
`.codeconv/conversion-specs/test/test_agent_init_goal.dart.md` (23
constructs, `escalations: []`, ratified mirror) and from the cited
SUT convspecs (heap_fcp.dart.md, terms.dart.md, runner.dart.md,
compiler.dart.md, runtime.dart.md, external_io.dart.md,
scheduler.dart.md, machine_state.dart.md, cells.dart.md,
mad_context.dart.md) and from CLAUDE.md (spec-first development,
GLP/runtime architecture). Every C# decision in §2 mirrors a
construct in the convspec verbatim; no novel design decisions were
introduced in this plan.

## 6. Escalations

None.
