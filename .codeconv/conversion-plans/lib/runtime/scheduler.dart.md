---
path: lib/runtime/scheduler.dart
cycle_group_id: 46
scc_siblings: []
generated_at: 2026-05-21T16:30:00Z
source_sha256: 6e6b012c5e9b5262847879644915ac2b9e75fd509e8cc5be8f6882a8515f2b03
schema_version: 1
---

# Conversion Plan: lib/runtime/scheduler.dart

## 1. Source Analysis

`glp_runtime_net/lib/runtime/scheduler.dart` is a ~400-line synchronous
drain-loop scheduler. Top-level constructs (inspected directly):

- Four imports: `dart:async`, `runtime.dart`, `package:glp_runtime/bytecode/runner.dart`, `terms.dart`.
- `enum ExecutionStatus { succeeded, failed, suspended }` — plain
  three-member tag-only enum.
- `class DrainResult` — immutable 4-field result bundle with
  `List<int> goalsRan`, `ExecutionStatus status`, `List<String>
  suspendedGoals`, `Set<int> blockingReaders` (optional positional
  default `const {}`). All `final`; NO `==`/`hashCode` override.
- `class Scheduler` — reference-identity mutable container:
  - `final GlpRuntime rt`, `final Map<Object?, BytecodeRunner> runners`
  - `void Function(String)? traceSink` (mutable nullable single-subscriber delegate)
  - Private pretty-printer state: `Map<int, String> _queryVarNames = {}`,
    `Map<int, int> _varDisplayMap = {}`, `int _nextDisplayId = 1`
  - Single named-only constructor with `required this.rt`, optional
    `runner` (single-program), optional `runners` (multi-program),
    optional `traceSink`; initialiser-list ternary builds runners map.
- Methods:
  - `setQueryVarNames(Map<String, int> varWriters)` — clear + foreach
    inverse-fill in place.
  - `_getVarDisplayName(int addr)` — reader→writer lookup +
    query-name lookup + `putIfAbsent(addr, () => _nextDisplayId++)`
    lazy counter-bump returning `'X$displayId'`.
  - `resetDisplayNumbering()` — clear both maps, reset counter to 1.
  - `_formatTerm(Term term, {bool markReaders = true, Set<int>? path})`
    — ~90-line recursive term formatter with dereference-loop + cycle
    detection, dispatching by `ConstTerm`/`VarRef`/`StructTerm` with
    list (functor `.` arity 2) and conjunction (functor `,` arity 2)
    special cases.
  - `_formatGoal(int goalId, String procName, CallEnv? env)` — scans
    up to 10 args from `CallEnv`, breaks on first null, formats
    `procName(arg1, ...)`.
  - `formatBinding(int varId, dynamic value)` — `dynamic` dispatch
    via `is Term`/`is String`/`== null || == 'nil'`/else; strips
    `Const(...)` wrapper via `substring(6, length - 1)`.
  - `_trace(String line)` — nullable-delegate dispatch with `print`
    fallback.
  - `drainWithStatus({maxCycles, debug, showBindings, debugOutput})`
    — CENTRAL synchronous drain loop: dequeue `rt.gq`, runner lookup
    (with fall-back to `rt.runners`), build `RunnerContext` with
    `termFormatter` + `onReduction` lambdas (closures over
    `hadReduction`/`suspendedGoals`), dispatch `runner.runWithStatus`,
    classify `RunResult.suspended`/`.terminated`, apply
    `Map.from(suspendedGoals)..removeWhere(...)` cascade copy-and-filter
    to exclude `rt.infrastructureGoalIds`, classify final
    `ExecutionStatus`, collect `rt.suspended.keys.toSet()` as blocking
    readers, return `DrainResult`.
  - `drain({...})` — legacy back-compat returning `goalsRan` only.
  - `drainAsyncWithStatus({...}) async` — the ONE async surface:
    outer loop calls sync `drainWithStatus` then `await
    Future.delayed(Duration(milliseconds: 10))` until pending timers
    fire.
  - `drainAsync({...}) async` — legacy async back-compat.
- Two `RegExp(r'(\w+)/\d+\(')` instances + `replaceAllMapped` calls
  strip `/arity` from trace output.

Cross-file dependencies (per tombstone): `lib/bytecode/runner.dart`
(`BytecodeRunner` / `RunnerContext` / `RunResult` / `CallEnv`),
`lib/runtime/runtime.dart` (`GlpRuntime` and its `gq` / `runners` /
`suspended` / `infrastructureGoalIds` / `pendingTimers` /
`getGoalEnv` / `getGoalProgram` / `getGoalModuleContext` surfaces),
`lib/runtime/terms.dart` (`Term` / `VarRef` / `ConstTerm` /
`StructTerm`).

## 2. Dart → C#/.NET Conversion Plan

Mirroring the ratified convspec construct-by-construct:

- **Imports** → `using` directives in `lib/runtime/scheduler.cs`:
  `using System;` (TimeSpan, Array.Empty), `using
  System.Collections.Generic;` (Dictionary, HashSet, List,
  IReadOnlyList, IReadOnlySet, IReadOnlyDictionary), `using
  System.Linq;` (Where, Select, ToDictionary), `using
  System.Text.RegularExpressions;` (Regex, MatchEvaluator), `using
  System.Threading.Tasks;` (Task, Task.Delay), `using <root>.Runtime;`,
  `using <root>.Bytecode;`. Carry-forward
  `rf-dart-import-relative-to-csharp-using-namespace`.

- **`enum ExecutionStatus`** → `public enum ExecutionStatus {
  Succeeded, Failed, Suspended }` (PascalCase per .NET naming
  guideline; underlying type `int`; no `[Flags]`; XML doc comments
  preserved). Cache hit on `rf-dart-enum-plain-to-csharp-enum`.

- **`class DrainResult`** → `public sealed class DrainResult` with
  get-only auto-properties `GoalsRan: IReadOnlyList<int>`,
  `Status: ExecutionStatus`, `SuspendedGoals: IReadOnlyList<string>`,
  `BlockingReaders: IReadOnlySet<int>`. Single ctor `public
  DrainResult(IReadOnlyList<int> goalsRan, ExecutionStatus status,
  IReadOnlyList<string> suspendedGoals, IReadOnlySet<int>?
  blockingReaders = null)` with body assignment `BlockingReaders =
  blockingReaders ?? new HashSet<int>();`. NOT `record` / `record
  class` / `struct` / `record struct`. Idiom
  `rf-dart-immutable-value-bundle-no-eq-to-csharp-sealed-class-getonly`.

- **`DefaultProgramKey` shared sentinel** → namespace-level `internal
  static readonly object DefaultProgramKey = new object();` (e.g. on
  a `RuntimeKeys` static class), SHARED with runtime.dart.md
  `Runners` table to handle `null` keys in
  `Dictionary<object?, BytecodeRunner>` (Microsoft Learn
  `Dictionary<TKey,TValue>.Add` throws `ArgumentNullException` on
  null keys).

- **`class Scheduler`** → `public class Scheduler` (not `sealed`,
  consistency with `GlpRuntime`) with:
  - `public GlpRuntime Rt { get; }` (init-only via ctor)
  - `public Dictionary<object?, BytecodeRunner> Runners { get; }` (init-only)
  - `public Action<string>? TraceSink { get; set; }` (mutable
    nullable delegate; carry-forward
    `rf-dart-void-function-question-to-csharp-action-nullable`)
  - `private Dictionary<int, string> _queryVarNames = new();`
  - `private Dictionary<int, int> _varDisplayMap = new();`
  - `private int _nextDisplayId = 1;`
  - `private static readonly Regex StripArityRegex = new(@"(\w+)/\d+\(");`
    (pre-compiled; Microsoft Learn best practice for repeated use)
  - Single positional ctor `public Scheduler(GlpRuntime rt,
    BytecodeRunner? runner = null, Dictionary<object?, BytecodeRunner>?
    runners = null, Action<string>? traceSink = null)` with body:
    `Rt = rt; Runners = runners ?? (runner != null ? new
    Dictionary<object?, BytecodeRunner> { { DefaultProgramKey, runner } }
    : new Dictionary<object?, BytecodeRunner>()); TraceSink = traceSink;`
  - Reference-identity equality preserved; NOT `record`/`struct`.
  - Cache hit on
    `rf-dart-mutable-state-class-identity-equality-to-csharp-class`.

- **`setQueryVarNames`** → `public void SetQueryVarNames(
  IReadOnlyDictionary<string, int> varWriters) {
  _queryVarNames.Clear(); foreach (var kv in varWriters)
  _queryVarNames[kv.Value] = kv.Key; }` — in-place mutation preserved.
  Idiom `rf-dart-map-clear-and-fill-inverse-to-csharp-dictionary-clear-foreach`.

- **`_getVarDisplayName`** → `private string GetVarDisplayName(int
  addr) { if (Rt.Heap.IsReader(addr)) { var writerAddr =
  Rt.Heap.TryWriterForReader(addr); if (writerAddr is int w &&
  _queryVarNames.TryGetValue(w, out var wname)) return wname; } if
  (_queryVarNames.TryGetValue(addr, out var qname)) return qname; if
  (!_varDisplayMap.TryGetValue(addr, out var displayId)) { displayId
  = _nextDisplayId++; _varDisplayMap[addr] = displayId; } return
  $"X{displayId}"; }`. The `putIfAbsent` lazy-counter-bump becomes
  `TryGetValue`+`++`+index-assign; NRT-clean (no `!` bang). Idiom
  `rf-dart-putifabsent-counter-to-csharp-tryget-add-and-bump`.

- **`resetDisplayNumbering`** → `public void ResetDisplayNumbering()
  { _queryVarNames.Clear(); _varDisplayMap.Clear(); _nextDisplayId =
  1; }`. Idiom
  `rf-dart-map-clear-and-counter-reset-to-csharp-dictionary-clear-assign`.

- **`_formatTerm`** → `private string FormatTerm(Term term, bool
  markReaders = true, HashSet<int>? path = null)` — preserves the
  deref-loop with cycle-check BEFORE deref-call, `is T name`
  pattern-match dispatch, list special-case (functor `.` arity 2)
  with circular-tail detection via `path.Contains(addr)`, conjunction
  special-case (functor `,` arity 2), general-struct recursion via
  `string.Join(", ", st.Args.Select(a => FormatTerm(a, markReaders,
  path)))`. Body-side `path ??= new HashSet<int>();`. Final fallback
  `current.ToString() ?? string.Empty` (NRT-aware; Microsoft Learn
  `Object.ToString`). Idiom
  `rf-dart-recursive-term-formatter-with-cycle-detection-to-csharp-recursive-pattern-match`.

- **`_formatGoal`** → `private string FormatGoal(int goalId, string
  procName, CallEnv? env) { if (env == null) return procName; var
  args = new List<string>(); for (int i = 0; i < 10; i++) { var arg =
  env.Arg(i); if (arg != null) args.Add(FormatTerm(arg)); else break;
  } if (args.Count == 0) return procName; return
  $"{procName}({string.Join(", ", args)})"; }`. Hard-coded `10`
  preserved. Idiom
  `rf-dart-args-scan-with-nullable-and-join-to-csharp-list-loop-and-stringjoin`.

- **`formatBinding`** → `public string FormatBinding(int varId,
  object? value) { var name = GetVarDisplayName(varId); string
  valueStr; if (value is Term t) valueStr = FormatTerm(t,
  markReaders: false); else if (value is string s) valueStr = s; else
  if (value == null || object.Equals(value, "nil")) valueStr = "[]";
  else valueStr = value.ToString() ?? string.Empty; if
  (valueStr.StartsWith("Const(") && valueStr.EndsWith(")")) valueStr
  = valueStr[6..^1]; return $"{name} = {valueStr}"; }`.
  `dynamic`→`object?` (cache hit `rf-dart-dynamic-to-csharp-object`);
  `object.Equals(value, "nil")` for cross-type structural equality;
  range-indexer `[6..^1]` for Dart `substring(6, length-1)`. Idiom
  `rf-dart-dynamic-typed-format-with-prefix-suffix-strip-to-csharp-object-pattern-and-range-index`.

- **`_trace`** → `private void Trace(string line) { var cb =
  TraceSink; if (cb != null) cb(line); else
  System.Console.WriteLine(line); }`. Snapshot-then-invoke nullable-
  delegate (cache hit on mad_context.dart.md discipline) +
  `rf-dart-print-to-csharp-console-writeline` cache hit.

- **`drainWithStatus`** → `public DrainResult DrainWithStatus(int
  maxCycles = 1000, bool debug = false, bool showBindings = true,
  bool debugOutput = false)`. Body preserves:
  - dequeue `Rt.Gq.Dequeue()` (returns `GoalRef?` per machine_state)
    with `if (act == null) break;` guard, then `.Value.Id` /
    `.Value.Pc` field access;
  - runner lookup via `DefaultProgramKey` sentinel: `var runner =
    Runners.TryGetValue(program ?? DefaultProgramKey, out var r1) ?
    r1 : null; runner ??= Rt.Runners.TryGetValue(program ??
    DefaultProgramKey, out var r2) ? r2 : null;`
  - `throw new InvalidOperationException(...)` for missing runner
    (carry-forward `rf-dart-stateerror-to-csharp-invalidoperationexception`);
  - procName scan via `foreach (var entry in runner.Prog.Labels) { if
    (entry.Value == act.Value.Pc) { procName = entry.Key; break; } }`;
  - `var hadReduction = false;` local CAPTURED BY LAMBDA — Microsoft
    Learn "Lambda expressions" documents compiler-generated display
    class makes mutations visible to outer scope after lambda returns;
  - `var cx = new RunnerContext(rt: Rt, goalId: act.Value.Id, kappa:
    act.Value.Pc, env: env, goalHead: goalStr, goalProcName: procName,
    showBindings: showBindings, debugOutput: debugOutput,
    moduleContext: moduleContext, termFormatter: (term, markReaders)
    => FormatTerm(term, markReaders), onReduction: (goalId, head,
    body) => { if (head.Contains("query__")) { hadReduction = true;
    suspendedGoals.Remove(goalId); return; } if (debug) { var
    cleanHead = StripArityRegex.Replace(head, m =>
    $"{m.Groups[1].Value}("); var cleanBody =
    StripArityRegex.Replace(body, m => $"{m.Groups[1].Value}(");
    Trace($"{cleanHead} :- {cleanBody}"); } hadReduction = true;
    suspendedGoals.Remove(goalId); });`
  - `var result = runner.RunWithStatus(cx);`
  - status-classification arms: `RunResult.Suspended` →
    `suspendedGoals[act.Value.Id] = goalStr;` + optional debug trace;
    `RunResult.Terminated` + `!hadReduction && !isQueryWrapper` →
    optional debug trace + `hasFailed = true; suspendedGoals.Remove
    (act.Value.Id); break;`; `RunResult.Terminated` +
    `hadReduction || isQueryWrapper` → `suspendedGoals.Remove
    (act.Value.Id);`;
  - filter via LINQ: `var userSuspendedGoals = suspendedGoals.Where(
    kv => !Rt.InfrastructureGoalIds.Contains(kv.Key)).ToDictionary(kv
    => kv.Key, kv => kv.Value);` (yields fresh non-aliased
    dictionary; Dart cascade `Map.from(...)..removeWhere(...)`
    semantic equivalent);
  - final classification: `ExecutionStatus status; if (hasFailed)
    status = ExecutionStatus.Failed; else if (userSuspendedGoals.Count
    > 0) status = ExecutionStatus.Suspended; else status =
    ExecutionStatus.Succeeded;`
  - suspended-list cleanup: `var suspendedList =
    userSuspendedGoals.Values.Select(g => StripArityRegex.Replace(g,
    m => $"{m.Groups[1].Value}(")).ToList();`
  - blocking-readers: `var blockingReaders = status ==
    ExecutionStatus.Suspended ? new HashSet<int>(Rt.Suspended.Keys) :
    new HashSet<int>();`
  - `return new DrainResult(ran, status, suspendedList,
    blockingReaders);`
  - Idiom
    `rf-dart-central-drain-loop-with-closure-capture-and-status-classification-to-csharp-while-loop-with-display-class`.

- **`drain`** (legacy) → `public IReadOnlyList<int> Drain(int
  maxCycles = 1000, bool debug = false, bool showBindings = true,
  bool debugOutput = false) => DrainWithStatus(maxCycles, debug,
  showBindings, debugOutput).GoalsRan;`. Expression-bodied delegate.
  Idiom
  `rf-dart-named-arg-delegating-method-to-csharp-positional-expression-bodied-delegate`.

- **`drainAsyncWithStatus`** → `public async Task<DrainResult>
  DrainAsyncWithStatus(int maxCycles = 1000, bool debug = false,
  bool showBindings = true, bool debugOutput = false)`. Outer while
  drains via sync `DrainWithStatus(maxCycles - totalCycles, debug,
  showBindings, debugOutput)`, accumulates `ran.AddRange(result.GoalsRan)`,
  updates `totalCycles += result.GoalsRan.Count`, captures
  `lastStatus`/`lastSuspended`/`lastBlockingReaders`; breaks on
  `Failed` or `Rt.PendingTimers <= 0`; emits `[DEBUG] Waiting for
  {Rt.PendingTimers} pending timer(s)...` via
  `System.Console.WriteLine`; inner `while (Rt.Gq.Length == 0 &&
  Rt.PendingTimers > 0 && totalCycles < maxCycles) { await
  Task.Delay(10); }`. Initialise `lastSuspended =
  Array.Empty<string>()` and `lastBlockingReaders = new HashSet<int>()`.
  NO `CancellationToken` (FR-013 — escalate later if needed); NO
  `ConfigureAwait(false)` (single-owning-context invariant inherited
  from heap_fcp.dart.md escalations[0]). Idiom
  `rf-dart-future-delayed-to-csharp-task-delay`.

- **`drainAsync`** (legacy) → `public async Task<IReadOnlyList<int>>
  DrainAsync(int maxCycles = 1000, bool debug = false, bool
  showBindings = true, bool debugOutput = false) { var result =
  await DrainAsyncWithStatus(maxCycles, debug, showBindings,
  debugOutput); return result.GoalsRan; }`. Idiom
  `rf-dart-async-await-delegating-method-to-csharp-async-await`.

## 3. Decomposed Task Units

- T1: Emit `using` directives — done.
- T2: Emit `public enum ExecutionStatus { Succeeded, Failed, Suspended }` — done.
- T3: Emit `public sealed class DrainResult` with four get-only props + ctor + null-coalesce empty-set — done.
- T4: Emit shared `internal static readonly object DefaultProgramKey = new object();` (on namespace static class `RuntimeKeys` shared with runtime.dart.md) — done.
- T5: Emit `public class Scheduler` declaration + `Rt`/`Runners`/`TraceSink` properties + private state fields + `StripArityRegex` — done.
- T6: Emit Scheduler ctor with positional-with-defaults parameters and `DefaultProgramKey`-keyed single-runner convenience path — done.
- T7: Emit `SetQueryVarNames(IReadOnlyDictionary<string, int>)` clear+foreach in-place fill — done.
- T8: Emit `private string GetVarDisplayName(int addr)` with reader→writer lookup + `TryGetValue`+counter-bump — done.
- T9: Emit `public void ResetDisplayNumbering()` clear+reset-counter — done.
- T10: Emit `private string FormatTerm(Term, bool, HashSet<int>?)` recursive formatter with cycle-detection, deref-loop, list/conjunction special-cases — done.
- T11: Emit `private string FormatGoal(int, string, CallEnv?)` up-to-10-arg scan with break-on-null — done.
- T12: Emit `public string FormatBinding(int, object?)` with pattern-match dispatch + `object.Equals(value, "nil")` + range-indexer `[6..^1]` Const-strip — done.
- T13: Emit `private void Trace(string)` snapshot-then-invoke TraceSink else `Console.WriteLine` — done.
- T14: Emit `public DrainResult DrainWithStatus(...)` central drain loop with closure-captured `hadReduction`/`suspendedGoals`, RunnerContext lambdas, status classification, LINQ Where+ToDictionary filter, blocking-readers HashSet construction — done.
- T15: Emit `public IReadOnlyList<int> Drain(...)` expression-bodied legacy delegate returning `.GoalsRan` — done.
- T16: Emit `public async Task<DrainResult> DrainAsyncWithStatus(...)` poll-with-delay loop using `Task.Delay(10)`; no CancellationToken / ConfigureAwait — done.
- T17: Emit `public async Task<IReadOnlyList<int>> DrainAsync(...)` legacy async delegate returning `.GoalsRan` — done.
- T18: Threading-model invariant inherited from heap_fcp.dart.md escalations[0]; non-concurrent collections, default `ConfigureAwait(true)`; not re-decided — done.

## 4. Research Findings

none required. All idiom IDs are cache hits or refinements of
prior ratified findings (heap_fcp.dart.md, machine_state.dart.md,
runtime.dart.md, body_kernels.dart.md, mad_context.dart.md,
bytecode/runner.dart.md, terms.dart.md). New idioms recorded in the
convspec (no external WebFetch/WebSearch needed — all authoritative
Microsoft Learn URLs already cited in the convspec rationale section):

- `rf-dart-immutable-value-bundle-no-eq-to-csharp-sealed-class-getonly`
  — Microsoft Learn records-vs-classes guidance + `IReadOnlyList<T>`
  / `IReadOnlySet<T>` BCL interfaces.
- `rf-dart-putifabsent-counter-to-csharp-tryget-add-and-bump` —
  Microsoft Learn `Dictionary<TKey,TValue>.TryGetValue` documents
  lazy-bump shape.
- `rf-dart-recursive-term-formatter-with-cycle-detection-to-csharp-recursive-pattern-match`
  — Microsoft Learn pattern-matching reference + `HashSet<T>` +
  `String.Join` + `Enumerable.Select` + `Object.ToString` nullability.
- `rf-dart-args-scan-with-nullable-and-join-to-csharp-list-loop-and-stringjoin`
  — Microsoft Learn `String.Join(String, IEnumerable<String>)`.
- `rf-dart-dynamic-typed-format-with-prefix-suffix-strip-to-csharp-object-pattern-and-range-index`
  — Microsoft Learn `Object.Equals(Object, Object)` + "Indices and ranges".
- `rf-dart-central-drain-loop-with-closure-capture-and-status-classification-to-csharp-while-loop-with-display-class`
  — Microsoft Learn "Lambda expressions" capture semantics + LINQ
  `Enumerable.ToDictionary` + Regex best practices +
  `HashSet<T>(IEnumerable<T>)` ctor.
- `rf-dart-future-delayed-to-csharp-task-delay` — Microsoft Learn
  `Task.Delay(Int32)` + async/await guide.
- `rf-dart-map-clear-and-fill-inverse-to-csharp-dictionary-clear-foreach`,
  `rf-dart-map-clear-and-counter-reset-to-csharp-dictionary-clear-assign`,
  `rf-dart-named-arg-delegating-method-to-csharp-positional-expression-bodied-delegate`,
  `rf-dart-async-await-delegating-method-to-csharp-async-await` —
  trivial recorded idioms.

Cache hits (FR-012, NOT re-researched):
`rf-dart-import-relative-to-csharp-using-namespace`,
`rf-dart-enum-plain-to-csharp-enum`,
`rf-dart-mutable-state-class-identity-equality-to-csharp-class`,
`rf-dart-void-function-question-to-csharp-action-nullable`,
`rf-dart-print-to-csharp-console-writeline`,
`rf-dart-dynamic-to-csharp-object`,
`rf-dart-stateerror-to-csharp-invalidoperationexception`,
`rf-dart-nullable-int-return-with-type-test-to-csharp-pattern-match`.

## 5. Consistency Pass

- Imports: fixed — derived from convspec construct
  `dart.import_directive.dart_async_full_and_package_internal_and_relative`
  (`rf-dart-import-relative-to-csharp-using-namespace`).
- `ExecutionStatus` enum: fixed — derived from convspec construct
  `dart.enum.plain_marker_three_member_execution_status`
  (`rf-dart-enum-plain-to-csharp-enum`).
- `DrainResult` immutable bundle: fixed — derived from convspec
  construct `dart.class.drainresult_immutable_value_bundle_no_eq_override_with_optional_positional_default_const_set`
  (`rf-dart-immutable-value-bundle-no-eq-to-csharp-sealed-class-getonly`).
- `DefaultProgramKey` shared sentinel: fixed — derived from convspec
  `Scheduler` ctor nuance forwarding to runtime.dart.md
  rf-dart-null-key (Microsoft Learn `Dictionary<TKey,TValue>.Add`).
- `Scheduler` reference class + ctor + state fields: fixed — derived
  from convspec construct
  `dart.class.scheduler_reference_identity_mutable_per_query_state_with_nullable_trace_sink`
  (`rf-dart-mutable-state-class-identity-equality-to-csharp-class`).
- `SetQueryVarNames`: fixed — derived from convspec construct
  `dart.method.set_query_var_names_named_inverse_map_build`
  (`rf-dart-map-clear-and-fill-inverse-to-csharp-dictionary-clear-foreach`).
- `GetVarDisplayName`: fixed — derived from convspec construct
  `dart.method.get_var_display_name_putifabsent_counter_lazy_with_reader_writer_lookup`
  (`rf-dart-putifabsent-counter-to-csharp-tryget-add-and-bump`).
- `ResetDisplayNumbering`: fixed — derived from convspec construct
  `dart.method.reset_display_numbering_clear_three_fields`
  (`rf-dart-map-clear-and-counter-reset-to-csharp-dictionary-clear-assign`).
- `FormatTerm`: fixed — derived from convspec construct
  `dart.method.format_term_recursive_with_cycle_detection_named_optional_default_set_and_dereference_loop_and_list_special_case_and_conj_special_case`
  (`rf-dart-recursive-term-formatter-with-cycle-detection-to-csharp-recursive-pattern-match`).
- `FormatGoal`: fixed — derived from convspec construct
  `dart.method.format_goal_args_loop_with_call_env_arg_lookup_and_break_on_null`
  (`rf-dart-args-scan-with-nullable-and-join-to-csharp-list-loop-and-stringjoin`).
- `FormatBinding`: fixed — derived from convspec construct
  `dart.method.format_binding_dynamic_value_dispatch_with_const_wrapper_strip`
  (`rf-dart-dynamic-typed-format-with-prefix-suffix-strip-to-csharp-object-pattern-and-range-index`).
- `Trace`: fixed — derived from convspec construct
  `dart.method.trace_sink_dispatch_with_print_fallback`
  (`rf-dart-void-function-question-to-csharp-action-nullable` +
  `rf-dart-print-to-csharp-console-writeline`).
- `DrainWithStatus`: fixed — derived from convspec construct
  `dart.method.drain_with_status_sync_dispatch_loop_with_max_cycles_and_reduction_callback_and_status_classification_and_blocking_readers`
  (`rf-dart-central-drain-loop-with-closure-capture-and-status-classification-to-csharp-while-loop-with-display-class`).
- `Drain` (legacy): fixed — derived from convspec construct
  `dart.method.drain_legacy_delegating_to_drain_with_status_and_returning_goals_ran_only`
  (`rf-dart-named-arg-delegating-method-to-csharp-positional-expression-bodied-delegate`).
- `DrainAsyncWithStatus`: fixed — derived from convspec construct
  `dart.method.drain_async_with_status_async_await_future_delayed_poll_loop_with_pending_timers`
  (`rf-dart-future-delayed-to-csharp-task-delay`); threading-model
  resumption-context inheritance from heap_fcp.dart.md escalations[0]
  (NOT re-decided per ratified policy).
- `DrainAsync` (legacy): fixed — derived from convspec construct
  `dart.method.drain_async_legacy_delegating_to_drain_async_with_status_and_returning_goals_ran_only`
  (`rf-dart-async-await-delegating-method-to-csharp-async-await`).

## 6. Escalations

None.
