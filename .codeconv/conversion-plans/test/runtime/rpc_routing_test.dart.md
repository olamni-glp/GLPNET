---
path: test/runtime/rpc_routing_test.dart
cycle_group_id: 157
scc_siblings: []
generated_at: 2026-05-21T16:44:48Z
source_sha256: 3dedc5b118a3b9b0a1a2e94a6ddc7abceb28811e6c7d07f381ff1493ae5a98bb
schema_version: 1
---

# Conversion Plan: test/runtime/rpc_routing_test.dart

## 1. Source Analysis

`test/runtime/rpc_routing_test.dart` is a 305-line synchronous
`package:test` file that exercises Phase 5 RPC routing via GLP
channels. Imports: `package:test/test.dart` plus seven
`package:glp_runtime/...` imports (`compiler/compiler.dart`,
`runtime/runtime.dart`, `runtime/terms.dart`,
`runtime/machine_state.dart`, `runtime/scheduler.dart`,
`runtime/glp_activation.dart`, `bytecode/runner.dart`).

File-level construct inventory (verified against the source):

- One top-level `const serveSource` triple-quoted GLP-source string
  (lines 11–24) carrying the `serve/2` system predicate definition.
- One `void main()` entrypoint (line 26) containing exactly one
  `group('Phase 5: RPC routing via GLP channels', () { ... })` block
  with five `test(...)` calls — all synchronous closures (no `async`,
  no `await`, no `Future`).
- Test 1 (lines 28–98) "Distribute routes via GLP channel when target
  is activated" — compiles three GLP sources (B exported `process/1`,
  `serveSource`, A `caller/1` with cross-module call
  `target_b # process(X?)`); calls `activateModule(rt:, serveBytecode:,
  moduleBytecode:, moduleName: 'target_b')` to register a channel;
  asserts `rt.glpChannels.containsKey('target_b')` and
  `expect(rt.glpChannels['target_b'], same(channel))`; drains scheduler
  twice (maxCycles 100 then 500); sets up `ReplModuleContext` with
  importIndex 1 → `ReplModuleTarget('target_b', bBytecode)`; lazy-
  registers `BytecodeRunner` keyed by `aBytecode`; enqueues caller
  goal; asserts `succeeded` status with a `reason:` narrative.
- Test 2 (lines 100–162) same shape as Test 1 + a `trace = <String>[]`
  list captured via the `traceSink: (s) => trace.add(s)` named
  argument on `Scheduler` ctor + the second drain runs `debug: true`;
  asserts `trace.join('\n')` contains `'serve'`.
- Test 3 (lines 164–219) "multiple Distribute RPCs route through GLP
  channel" — module B exports both `greet/1` and `farewell/1`; module
  A is `run_both(X, Y) :- ground(X?), ground(Y?) | target_b #
  greet(X?), target_b # farewell(Y?).`; two heap-stored args
  `'alice'` and `'bob'`; `CallEnv(args: {0:, 1:})` with two entries;
  enqueue `run_both/2`; drain `maxCycles: 1000`; assert succeeded.
- Test 4 (lines 221–243) "activateModule registers channel in
  glpChannels" — asserts `rt.glpChannels.isEmpty` BEFORE
  `activateModule`, then asserts `containsKey('my_module')` and
  `same(channel)` AFTER.
- Test 5 (lines 245–302) "close channel after RPC routing, serve
  terminates" — same RPC flow as Test 1 but afterwards calls
  `channel.close()` which returns `activations` (a `List<GoalRef>`);
  `for (final act in activations) rt.gq.enqueue(act);`; final drain
  `maxCycles: 200`; asserts `serve` terminates with `succeeded`.

Cross-cutting Dart constructs observed: synchronous closure bodies
only (no `async`/`Future`/`await`); five `expect(..., equals(
ExecutionStatus.succeeded))` calls (three carry `reason:` messages);
two `expect(..., same(channel))` reference-identity assertions; one
`expect(traceStr, contains('serve'), reason: ...)`; one
`Map[K]!` null-assertion on `aBytecode.labels['caller/1']!` /
`labels['run_both/2']!`; lazy-add idiom `if (!rt.runners.containsKey(
aBytecode)) rt.runners[aBytecode] = BytecodeRunner(aBytecode);` (3
occurrences); `rt.nextGoalId++` post-increment (4 occurrences); map
literals `{0: VarRef(...)}` and `{1: ReplModuleTarget(...)}`;
`for (final act in activations)` foreach loop; one `trace.join('\n')`
iterable-join.

No async, no streams, no isolates, no Completer, no Future.Run-shape
is purely synchronous-blocking observation of `drainWithStatus`
return value followed by `Assert.Equal`-style enum assertion.

## 2. Dart → C#/.NET Conversion Plan

Each construct below mirrors the ratified convspec (`schema_version:
1`, `source_sha256: 3dedc5b118a3b9b0a1a2e94a6ddc7abceb28811e6c7d07f
381ff1493ae5a98bb`).

- **C1. `import 'package:test/test.dart';`** → `using Xunit;` at file
  scope. xUnit is the project-pinned framework (cached idiom — every
  prior `package:test` convspec). Synchronous tests: no `[Fact] async
  Task` shape required.

- **C2. Seven `package:glp_runtime/...` imports** → collapse to ≤3
  `using` directives: the five `runtime/*.dart` imports collapse to
  one `using <RootNs>.Runtime;`; `compiler/compiler.dart` → `using
  <RootNs>.Compiler;`; `bytecode/runner.dart` → `using <RootNs>.
  Bytecode;`. `ReplModuleContext` / `ReplModuleTarget` live in the
  `Bytecode` namespace per `bytecode/runner.dart.md`. Exact
  namespace strings are owned by the SUT specs.

- **C3. `const serveSource = '''...''';`** → `private const string
  serveSource = @"...";` declared on the test class
  `RpcRoutingTest`. C# `@"..."` verbatim form preserves newlines
  byte-for-byte; GLP source uses inner SINGLE quotes (`'_activate'`)
  so no `""` escape needed. Codegen MUST NOT use interpolated
  `$@"..."` (no `$` in the Dart literal). Authoritative: Microsoft
  Learn `verbatim` tokens documentation.

- **C4. `void main() { group(...) }`** → eliminated; xUnit discovers
  `[Fact]` methods by reflection. No per-file entrypoint.

- **C5. `group('Phase 5: RPC routing via GLP channels', () { test×5 })`**
  → ONE PascalCase xUnit test class `RpcRoutingTest` (file-name
  mirror) containing five `[Fact]`-decorated methods. Group label
  preserved verbatim on every method as
  `[Trait("Group", "Phase 5: RPC routing via GLP channels")]`.
  No nested groups.

- **C6. Five `test(label, () { ... })` synchronous closures** →
  five `public void` instance methods, each decorated with
  `[Fact(DisplayName = "<original label>")]` + the shared
  `[Trait]`. Method names (PascalCased, identifier-safe):
  `DistributeRoutesViaGlpChannelWhenTargetIsActivated`,
  `DistributeRoutesViaGlpChannelWithDebugTrace`,
  `MultipleDistributeRpcsRouteThroughGlpChannel`,
  `ActivateModuleRegistersChannelInGlpChannels`,
  `CloseChannelAfterRpcRoutingServeTerminates`. NO constructor /
  shared field; per-test isolation via xUnit's per-test class
  instantiation contract — each method allocates its own
  `var compiler = new GlpCompiler();` / `var rt = new GlpRuntime();`.

- **C7. `final compiler = GlpCompiler();`** → `var compiler = new
  GlpCompiler();` (method-local; single-assignment preserved by
  source shape).

- **C8. `compiler.compile('''...''')`** →
  `compiler.Compile(@"...")` — PascalCase method, verbatim literal.
  Converted signature (owned by `compiler.dart.md`):
  `BytecodeProgram Compile(string source)` synchronous.

- **C9. `expect(<bool>, isTrue)`** → `Assert.True(<bool>);`. The
  `rt.glpChannels.isEmpty` form (Test 4) MAY be tightened to
  `Assert.Empty(rt.GlpChannels);` for clearer diagnostic.

- **C10. `expect(rt.glpChannels['target_b'], same(channel));`** →
  `Assert.Same(channel, rt.GlpChannels["target_b"]);` — REFERENCE-
  IDENTITY assertion, argument order flipped (xUnit expected
  first). Authoritative: xunit.net `Assert.Same`; Dart `identical`.
  This assertion is RESOLVED — see §6: parent rulings #4 (heap_fcp
  single-owning-context) + #5 (Channel<IsolateMessage> in-process
  references) preserve `GlpChannelHandle` reference identity in
  the plain `Dictionary<string, GlpChannelHandle>` and the
  Channel<T> mailbox never marshals handles.

- **C11. `rt.glpChannels['target_b']` (Map indexer)** → C#
  `rt.GlpChannels["target_b"]` (indexer; guarded by the
  immediately-preceding `ContainsKey` assertion so the C# throw-
  on-miss contract never fires here).

- **C12. `final rt = GlpRuntime();`** → `var rt = new GlpRuntime();`.
  Reference-type class per `runtime.dart.md`. Per-test allocation.

- **C13. `activateModule(rt:, serveBytecode:, moduleBytecode:,
  moduleName:)`** → `GlpActivation.ActivateModule(rt: rt,
  serveBytecode: serveBytecode, moduleBytecode: bBytecode,
  moduleName: "target_b");` — Dart top-level function → C# static
  method on the host class `GlpActivation` (owned by
  `glp_activation.dart.md`). Named-argument call syntax (C# 7.2+)
  mirrors Dart shape exactly. Synchronous return of
  `GlpChannelHandle`.

- **C14. `Scheduler(rt: rt)` and `Scheduler(rt: rt, traceSink: (s)
  => trace.add(s))`** → `new Scheduler(rt: rt)` and `new Scheduler(
  rt: rt, traceSink: s => trace.Add(s))`. `traceSink` is Dart
  `void Function(String)` → C# `Action<string>` (Microsoft Learn
  `System.Action<T>`). Lambda captures local `trace` by-reference.

- **C15. `scheduler.drainWithStatus(maxCycles: N)` and
  `drainWithStatus(maxCycles: 500, debug: true)`** →
  `scheduler.DrainWithStatus(maxCycles: N)` /
  `scheduler.DrainWithStatus(maxCycles: 500, debug: true)`.
  SYNCHRONOUS (NOT `async Task<DrainResult>`) per
  `scheduler.dart.md`. Reassignment shape preserved:
  `var result = scheduler.DrainWithStatus(...); result =
  scheduler.DrainWithStatus(...);` (NOT a fresh `var` on the
  second assignment).

- **C16. `ExecutionStatus.succeeded`** → `ExecutionStatus.Succeeded`
  (PascalCase enum member per `scheduler.dart.md`). Five callsites
  in this file all get the same rename.

- **C17. `expect(result.status, equals(ExecutionStatus.succeeded));`
  (no reason)** → `Assert.Equal(ExecutionStatus.Succeeded,
  result.Status);` — argument-order flipped (expected first).

- **C18. `expect(result.status, equals(ExecutionStatus.succeeded),
  reason: '<msg>');` (three occurrences: Tests 1, 3, 5)** →
  `Assert.True(result.Status == ExecutionStatus.Succeeded,
  "<msg>");` — xUnit `Assert.Equal` has NO `userMessage` overload;
  the documented form to preserve the diagnostic narrative is
  `Assert.True(<bool>, <message>)`. Trade-off (acceptable for enum):
  loses `Assert.Equal` type-diff but preserves the provenance
  message at runtime. Authoritative: xunit.net documentation.

- **C19. `final callerGoalId = rt.nextGoalId++;`** →
  `var callerGoalId = rt.NextGoalId++;`. C# post-increment is
  identical semantics (returns old value, increments in place).
  Width: `long NextGoalId` per `runtime.dart.md`. Threading-model:
  inherited single-owning-context (option A) makes the non-atomic
  RMW safe — codegen MUST NOT substitute `Interlocked.Increment`.

- **C20. `rt.heap.storeTermOnHeap(ConstTerm(<lit>))`** →
  `rt.Heap.StoreTermOnHeap(new ConstTerm(<lit>));` returning `long`.
  Literal boxing per `terms.dart.md`: `ConstTerm(42)` boxes int;
  `ConstTerm('alice')` boxes string (Dart `'alice'` → C# `"alice"`).

- **C21. `CallEnv(args: {0: VarRef(argAddr)})` and the two-entry
  variant `{0: VarRef(arg0Addr), 1: VarRef(arg1Addr)}`** →
  `new CallEnv(args: new Dictionary<int, VarRef> { { 0, new
  VarRef(argAddr) } });` and the two-entry form. Codegen MAY use
  the C# 9 indexer-initialiser form `{ [0] = new VarRef(...) }`.
  Key width `int` (NOT `long` — argument-slot indices) per
  `machine_state.dart.md`.

- **C22. `rt.setGoalEnv` / `rt.setGoalProgram` /
  `rt.setGoalModuleContext`** → `rt.SetGoalEnv(callerGoalId, env);`
  / `rt.SetGoalProgram(callerGoalId, aBytecode);` /
  `rt.SetGoalModuleContext(callerGoalId, replCtx);` — PascalCase
  synchronous instance methods per `runtime.dart.md`.

- **C23. `ReplModuleContext(moduleName: 'caller_a', imports: {1:
  ReplModuleTarget('target_b', bBytecode)})`** → `new
  ReplModuleContext(moduleName: "caller_a", imports: new
  Dictionary<int, ReplModuleTarget> { { 1, new ReplModuleTarget(
  "target_b", bBytecode) } });`. Four occurrences (Tests 1, 2, 3,
  5). `ReplModuleTarget(string, BytecodeProgram)` is positional
  per the Dart call shape; defer to `runner.dart.md` for final
  ruling. PascalCased types.

- **C24. `if (!rt.runners.containsKey(aBytecode)) rt.runners[
  aBytecode] = BytecodeRunner(aBytecode);`** → preferred
  `rt.Runners.TryAdd(aBytecode, new BytecodeRunner(aBytecode));`
  (single-method idiom — Microsoft Learn
  `Dictionary<TKey,TValue>.TryAdd`). Verbatim form
  `if (!rt.Runners.ContainsKey(aBytecode)) rt.Runners[aBytecode]
  = new BytecodeRunner(aBytecode);` is also correct.
  Reference-type key (`BytecodeProgram`) — both Dart and C# default
  to reference identity for keying (no `==` override per
  `runtime.dart.md` / `runner.dart.md`). NO `ConcurrentDictionary`
  substitution — owning-context invariant.

- **C25. `rt.gq.enqueue(GoalRef(callerGoalId, callerPc));`** →
  `rt.Gq.Enqueue(new GoalRef(callerGoalId, callerPc));` —
  PascalCase property `Gq` per `runtime.dart.md` (codegen MAY
  consult that spec if the abbreviation expands to `GoalQueue`).
  `GoalRef` is positional ctor on a reference-type `class` (NOT
  record/struct; identity equality per `machine_state.dart.md`).

- **C26. `aBytecode.labels['caller/1']!` and `labels['run_both/2']!`** →
  `aBytecode.Labels["caller/1"]` and `aBytecode.Labels["run_both/2"]`.
  Dart `!` is the null-assertion operator (runtime throw on null);
  C# `Dictionary[K]` throws `KeyNotFoundException` on miss —
  IDENTICAL observable throw contract. The `!` is REDUNDANT in C#
  and codegen MUST DROP it (must not translate to C#'s null-
  forgiving `!` which has different — compile-time only —
  semantics).

- **C27. `final trace = <String>[];`** → `var trace = new List<
  string>();` (typed empty list literal → `List<string>` ctor).

- **C28. `final traceStr = trace.join('\n');`** →
  `var traceStr = string.Join("\n", trace);` — ARGUMENT ORDER
  FLIPPED (static `string.Join(separator, values)`, separator
  first). NOT verbatim `@"\n"` — the interpreted-escape form
  `"\n"` is required to emit an actual newline character.
  Authoritative: Microsoft Learn `string.Join`.

- **C29. `expect(traceStr, contains('serve'), reason: 'Trace should
  show serve reduction');`** → `Assert.True(traceStr.Contains(
  "serve"), "Trace should show serve reduction");` —
  message-preservation form (same trade-off as C18). Plain
  `Assert.Contains("serve", traceStr)` is the message-less
  alternative; the `reason:` here is narrative provenance and
  should survive into runtime failure output.

- **C30. `final activations = channel.close(); for (final act in
  activations) { rt.gq.enqueue(act); }`** → `var activations =
  channel.Close(); foreach (var act in activations) { rt.Gq.
  Enqueue(act); }`. `Close()` returns `List<GoalRef>` per
  `glp_activation.dart.md` (mutable list preserved; consumer
  enqueues each element, does not mutate the returned list).

## 3. Decomposed Task Units

- T1. Emit file-scope `using Xunit;` directive (C1) — done
- T2. Collapse 7 `package:glp_runtime/...` imports to ≤3 `using` directives (C2) — done
- T3. Emit `namespace <RootNs>.Test.Runtime { ... }` mirroring path (cu-2) — done
- T4. Emit `public class RpcRoutingTest { ... }` enclosing all five tests (C5, cu-3) — done
- T5. Emit `private const string serveSource = @"...";` field preserving GLP source byte-for-byte (C3, cu-4) — done
- T6. Omit constructor; per-test allocation only (C6 nuance, cu-5) — done
- T7. Emit 5 `[Fact(DisplayName="...")] [Trait("Group", "...")] public void M() { ... }` methods (C5, C6, cu-6) — done
- T8. Per-method allocate `var compiler = new GlpCompiler(); var rt = new GlpRuntime();` (C7, C12, cu-7) — done
- T9. Translate `compiler.compile(@"...")` calls byte-faithful per test (C8) — done
- T10. Translate `GlpActivation.ActivateModule(rt:, serveBytecode:, moduleBytecode:, moduleName:)` named-arg static call (C13) — done
- T11. Translate `new Scheduler(rt: rt)` and `new Scheduler(rt: rt, traceSink: s => trace.Add(s))` (C14) — done
- T12. Translate `var result = scheduler.DrainWithStatus(maxCycles: N);` with reassignment preserving `var` once (C15, cu-8) — done
- T13. Rename `ExecutionStatus.succeeded` → `ExecutionStatus.Succeeded` at every callsite (C16) — done
- T14. Flip argument order on `Assert.Equal(expected, actual)` calls without `reason:` (C17, cu-9) — done
- T15. Emit `Assert.True(actual == expected, "<msg>")` form for the three `expect(..., reason:)` calls (C18, cu-10) — done
- T16. Emit `Assert.True(rt.GlpChannels.ContainsKey("..."))` / `Assert.Empty(rt.GlpChannels)` per cu (C9) — done
- T17. Emit `Assert.Same(channel, rt.GlpChannels["..."])` (argument flip) for the two reference-identity assertions (C10, cu-11) — done
- T18. Emit `var callerGoalId = rt.NextGoalId++;` byte-faithful (C19) — done
- T19. Emit `rt.Heap.StoreTermOnHeap(new ConstTerm(<lit>))` with literal boxing (C20) — done
- T20. Emit `new CallEnv(args: new Dictionary<int, VarRef> { { 0, new VarRef(...) }, ... });` (C21) — done
- T21. Emit `rt.SetGoalEnv` / `rt.SetGoalProgram` / `rt.SetGoalModuleContext` calls (C22) — done
- T22. Emit nested `new ReplModuleContext(moduleName:, imports: new Dictionary<int, ReplModuleTarget> { { 1, new ReplModuleTarget("target_b", bBytecode) } });` (C23, cu-13) — done
- T23. Emit `rt.Runners.TryAdd(aBytecode, new BytecodeRunner(aBytecode));` (preferred) OR verbatim ContainsKey + indexer-set (C24, cu-14) — done
- T24. Emit `rt.Gq.Enqueue(new GoalRef(callerGoalId, aBytecode.Labels["caller/1"]));` (C25) — done
- T25. Drop Dart `!` on Map indexer in Labels lookup (C26) — done
- T26. Emit `var trace = new List<string>();` (C27) — done
- T27. Emit `var traceStr = string.Join("\n", trace);` (argument flip) (C28) — done
- T28. Emit `Assert.True(traceStr.Contains("serve"), "Trace should show serve reduction");` (C29, cu-12) — done
- T29. Emit `var activations = channel.Close(); foreach (var act in activations) { rt.Gq.Enqueue(act); }` (C30, cu-15) — done
- T30. Verify no `async`/`Task`/`await`/`Channel<T>`/`Task.Run` introduced anywhere — synchronous surface only (convspec async/Future/Stream/Isolate section + C18 + C15) — done

## 4. Research Findings

none required — all decisions derive from the ratified convspec
(schema_version 1, sha 3dedc5b1…ae5a98bb) which carries 24
`research_finding_id` references already validated upstream
(rf-dart-package-test-to-dotnet-xunit, rf-dart-internal-package-
import-to-csharp-using, rf-dart-top-level-const-string-multiline-
to-csharp-const-verbatim, rf-dart-test-main-to-xunit-class-with-
facts, rf-dart-package-test-group-to-xunit-class, rf-dart-test-
callback-to-xunit-method-body, rf-dart-final-local-to-csharp-var,
rf-dart-method-call-snake-to-pascal, rf-dart-expect-istrue-to-
xunit-asserttrue, rf-dart-expect-same-to-xunit-assert-same,
rf-dart-map-indexer-nullable-to-csharp-dictionary-indexer-or-
trygetvalue, rf-dart-no-args-constructor-call-to-csharp-new,
rf-dart-named-arg-to-csharp-named-arg, rf-dart-plain-enum-to-
csharp-enum, rf-dart-expect-equals-to-xunit-assertequal,
rf-dart-expect-equals-with-reason-to-xunit-assert-true-with-
message, rf-dart-post-increment-mutable-field-to-csharp,
rf-dart-bind-writer-family-callsite-to-csharp-pascalcase-methods,
rf-dart-map-literal-int-to-vref-to-csharp-dictionary-init,
rf-dart-map-tryadd-pattern-to-csharp-dictionary-tryadd,
rf-dart-null-assertion-on-map-indexer-to-csharp-dictionary-
indexer, rf-dart-sumleaf-no-eq-to-csharp-class-no-record,
rf-dart-typed-list-literal-empty-to-csharp-list-of-T-new,
rf-dart-iterable-join-to-csharp-string-join, rf-dart-for-in-loop-
to-csharp-foreach, rf-dart-expect-string-contains-to-xunit-
assert-contains). The threading-model concern inherited from
heap_fcp.dart.md escalations[0] is RESOLVED upstream (parent
ruling #4 — single-owning-context). The reference-identity sub-
escalation that this convspec carried (escalations[0]) is
RESOLVED upstream (parent rulings #4 + #5; see convspec lines
985–998). No new research required.

## 5. Consistency Pass

- C1 — fixed — derived from convspec `dart.package_test.
  import_directive` (lines 33–49) and project-wide xUnit policy.
- C2 — fixed — derived from convspec `dart.package_under_test.
  import_directive` (lines 50–85).
- C3 — fixed — derived from convspec `dart.top_level_const_string.
  triple_quoted_glp_source_template` (lines 86–143) +
  authoritative Microsoft Learn `verbatim` documentation cited
  there.
- C4 — fixed — derived from convspec `dart.package_test.
  main_entrypoint` (lines 144–161).
- C5 — fixed — derived from convspec `dart.package_test.
  group_block_single_with_label_phase_prefix` (lines 162–203).
- C6 — fixed — derived from convspec `dart.package_test.
  test_call_synchronous_closure` (lines 204–236) + cu-5 / cu-6 /
  cu-7.
- C7 — fixed — derived from convspec `dart.local_var.final_
  constructor_glp_compiler` (lines 237–254).
- C8 — fixed — derived from convspec `dart.method_call.glp_
  compiler_compile_triple_quoted_source` (lines 255–286).
- C9 — fixed — derived from convspec `dart.package_test.expect_
  member_exists_containsKey` (lines 287–317).
- C10 — fixed — derived from convspec `dart.package_test.expect_
  same_reference_identity` (lines 318–356) + RESOLVED 2026-05-21
  note (lines 985–998).
- C11 — fixed — derived from convspec `dart.indexer_access.map_
  string_to_value_returning_nullable` (lines 357–389).
- C12 — fixed — derived from convspec `dart.constructor_call.
  glp_runtime_no_args` (lines 390–402).
- C13 — fixed — derived from convspec `dart.named_arg_call.
  activate_module_required_named_params` (lines 403–446).
- C14 — fixed — derived from convspec `dart.constructor_call.
  scheduler_with_optional_trace_sink` (lines 447–481).
- C15 — fixed — derived from convspec `dart.method_call.
  scheduler_drainWithStatus_named_args` (lines 482–525).
- C16 — fixed — derived from convspec `dart.enum_member_access.
  execution_status_succeeded` (lines 526–549).
- C17 — fixed — derived from convspec `dart.package_test.expect_
  equals_enum_member` (lines 550–570).
- C18 — fixed — derived from convspec `dart.package_test.expect_
  equals_with_reason_message` (lines 571–614).
- C19 — fixed — derived from convspec `dart.field_access.runtime_
  state_mutable_int_counter` (lines 615–648) + parent ruling #4
  (single-owning-context).
- C20 — fixed — derived from convspec `dart.method_call.heap_
  store_term_on_heap` (lines 649–673).
- C21 — fixed — derived from convspec `dart.constructor_call.
  call_env_with_named_map_literal` (lines 674–704).
- C22 — fixed — derived from convspec `dart.method_call.runtime_
  setGoalEnv_setGoalProgram_setGoalModuleContext` (lines 705–729).
- C23 — fixed — derived from convspec `dart.constructor_call.
  repl_module_context_with_named_map_literal_imports` (lines
  730–760).
- C24 — fixed — derived from convspec `dart.if_not_contains_then_
  assign.runtime_runners_lazy_registration` (lines 761–799) +
  parent ruling #4 (single-owning-context — no ConcurrentDictionary).
- C25 — fixed — derived from convspec `dart.method_call.goal_
  queue_enqueue_with_goal_ref` (lines 800–820).
- C26 — fixed — derived from convspec `dart.indexer_access_with_
  bang.bytecode_labels_lookup` (lines 821–858).
- C27 — fixed — derived from convspec `dart.local_var.empty_list_
  typed_string` (lines 875–891).
- C28 — fixed — derived from convspec `dart.method_call.list_
  string_join` (lines 892–913).
- C29 — fixed — derived from convspec `dart.package_test.expect_
  string_contains_substring` (lines 914–935).
- C30 — fixed — derived from convspec `dart.method_call.glp_
  channel_handle_close_returns_list_of_goal_ref` (lines 936–965).

## 6. Escalations

None.
