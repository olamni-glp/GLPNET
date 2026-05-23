---
path: lib/multiagent/mad_context.dart
cycle_group_id: 36
scc_siblings: [lib/bytecode/runner.dart, lib/runtime/body_kernels.dart, lib/runtime/glp_activation.dart, lib/runtime/runtime.dart, lib/runtime/system_predicates.dart]
generated_at: 2026-05-21T16:07:13Z
source_sha256: 2c0667ab13bae6919551df9dce3ba00ec6ac90d4037534aa7f62a39c486cca88
schema_version: 1
---

# Conversion Plan: lib/multiagent/mad_context.dart

## 1. Source Analysis

`mad_context.dart` (567 lines) is the per-agent **owner** for madGLP multi-agent execution. It binds together a `GlpRuntime`, a `GlobalWritersTable` (W_p), a `MessageQueue` (M_p), a `GlobalSendRegistry`, and a `PayloadSerializer`, and exposes the *agent-level* API consumed by the isolate manager / engine / body kernels.

**Top-level declarations:**
- `library;` (bare library directive, anchors file-level doc-comment).
- Six `import 'package:glp_runtime/...'` imports — three runtime peers (`runtime.dart`, `terms.dart`, `machine_state.dart`) and five multiagent siblings (`message_queue.dart`, `payload_serializer.dart`, `global_send.dart`, `global_writers_table.dart`, `mad_helpers.dart`).
- `typedef MessageDeliveryCallback = void Function(String destination, OutboundMessage message);` — top-level named function-type alias.
- `class MadContext` — single top-level class.

**`MadContext` fields:**
- Five eager `final` fields: `agentId: String`, `runtime: GlpRuntime`, `wp: GlobalWritersTable`, `mp: MessageQueue`, `globalSendRegistry: GlobalSendRegistry`.
- One `late final _serializer: PayloadSerializer` (assigned in ctor body, not initialiser list).
- Two nullable public callbacks: `MessageDeliveryCallback? onMessageReady`, `void Function(String)? traceSink`.

**Constructor:**
- Named-required: `MadContext({required this.agentId, required this.runtime})` with initialiser-list derivations `wp = GlobalWritersTable(agentId)`, `mp = MessageQueue()`, `globalSendRegistry = GlobalSendRegistry(agentId)` and a body assignment `_serializer = PayloadSerializer(agentId);`.

**Instance methods (13 total + ctor):**
1. `_trace(String)` — private helper; silently no-op if `traceSink == null`.
2. `onWriterBound(int writerId, Term value)` — public dispatcher; logs + delegates to `_fireGlobalSendGoalIfExists`. Every heap `onBind` callback in this file points to it.
3. `flushMessages() -> int` — drains M_p destinations through `onMessageReady`; snapshots `mp.destinations` via `List<String>.from(...)` before iterate (Poll mutates Destinations).
4. `_fireGlobalSendGoalIfExists(int writerAddr, Term value)` — load-bearing per-writer-bind dispatcher. Calls `globalSendRegistry.onWriterBound(...)` with an inline lambda for `extractVariables`; if a goal fires, globalizes the value, serializes payload, queues message, installs onBind callbacks for newly-spawned reader goals.
5. `_lookupVariableForSerialization(int addr) -> ({String creator, int creatorLocalId, bool isReader})` — Dart record return.
6. `_extractTermVarsRecursive(Term term, List<TermVar> result)` — recursive pattern-match over `Term` subclasses; uses null-coalesce fallback `?? term.addr`.
7. `registerGlobalSendSpawns(List<GlobalSendSpawn>)` — public; for each spawn, registers `GlobalSendGoal.fromSpawn(spawn)` and installs an onBind callback.
8. `handleMadAssignment({required GlobalName globalName, required Term value, required String fromAgent})` — public three-case dispatcher implementing spec §8.3.
9. `_handleSerializerAssignment(Term value, String fromAgent)` — case `_w(p,0)`; reads `wp.serializerWriterAddr`, list-cell unwrap, localize nested names, allocate fresh `(writer, reader)` pair, build `StructTerm('.', [content, VarRef(freshReader)])`, bind, update permanent entry (never remove).
10. `_handleWriterAssignment(GlobalName, Term, String)` — case `_w(p,i)` with i>0; `wp.lookupByIndex`, localize, bind, reactivate, **remove** transient entry.
11. `_handleReaderAssignment(GlobalName, Term, String)` — case `_r(p,i)`; `wp.findByRemote(agent, index)` linear scan, localize, bind, reactivate, **remove** transient entry.
12. `handleMadAssignmentWithGlobalNames({...})` — public extended dispatcher; pre-localize externally-supplied nested names then double-dispatch to `handleMadAssignment`.
13. `exportTerm(Term)` — public; extracts vars, installs onBind **only for writers** (DELIBERATE asymmetry).
14. `processSuspension(Set<int>)` — public; LOG-ONLY (push-model — no request message sent).
15. `send(Term term, bool isWriter, String gnAgent, int gnIndex, String destAgent)` — public; implements `'_send'(T,G,Q)` per spec §11.5. Positional params (unlike other multi-arg methods). NO onBind for globalize-writer entries (spec §5.1 writer-side-passive). Serializer-vs-normal branch picks `_serializer.createSerializerPayload` vs `_serializer.createGlobalSendPayload`.

**Cross-cutting properties:**
- Synchronous throughout (no `async`/`Future`/`Stream`/`Completer`).
- All trace output goes through `_trace` (silent if `traceSink == null` — no `print()` fallback).
- Spec §8.3 dispatch order is LOAD-BEARING (`isWriter && index == 0` MUST test first).
- Spec §5.1 writer-passive / reader-active asymmetry is LOAD-BEARING (documented in `send` and `exportTerm`).
- Network input stream is represented as heap list-cells (`StructTerm('.', [h, t])`), NOT Dart `Stream<T>`.
- No `==`/`hashCode` override → identity equality.
- No mixin / sealed / abstract / extension.

## 2. Dart → C#/.NET Conversion Plan

Mirrors the ratified convspec (17 constructs, 0 escalations) verbatim.

| # | Dart construct | C#/.NET decision |
|---|---|---|
| 1 | `library;` bare directive | Dropped (no .NET counterpart). File-level `///` doc-comments rendered as `//` file-header above `namespace lib.multiagent`. |
| 2 | 6 × `import 'package:glp_runtime/...'` | Intra-multiagent imports collapse silently into same target namespace `lib.multiagent`. Cross-namespace runtime peer imports collapse into single `using GlpRuntime.Runtime;`. |
| 3 | `typedef MessageDeliveryCallback = void Function(String, OutboundMessage);` | `public delegate void MessageDeliveryCallback(string destination, OutboundMessage message);` — named delegate preserves domain intent; NOT `Action<string, OutboundMessage>`; NOT an `event`. |
| 4 | `class MadContext` — 5 eager `final` + 1 `late final` + 2 nullable callbacks + named-required ctor with initialiser list | `public class MadContext` in `lib.multiagent`; 5 get-only auto-properties (`AgentId`, `Runtime`, `Wp`, `Mp`, `GlobalSendRegistry`); `private readonly PayloadSerializer _serializer;` assigned in ctor body; `public MessageDeliveryCallback? OnMessageReady;`, `public Action<string>? TraceSink;`; single positional ctor `public MadContext(string agentId, GlpRuntime runtime)` with all field-initialisation collapsed into the body. NOT a record. NOT sealed. |
| 5 | `void _trace(String msg) { if (traceSink != null) traceSink!(msg); }` | `private void Trace(string msg) => TraceSink?.Invoke(msg);` — expression-bodied null-conditional invoke (silent if null). |
| 6 | `void onWriterBound(int, Term)` thin dispatcher | `public void OnWriterBound(int writerId, Term value) { Trace($"[MAD {AgentId}] onWriterBound: writerId={writerId}, value={value}"); _fireGlobalSendGoalIfExists(writerId, value); }`. |
| 7 | `int flushMessages()` — snapshot `mp.destinations`, drain via `while(true)/break`, count, return | `public int FlushMessages()` — early-return on null callback (capture into local `var cb = OnMessageReady;` before the read for NRT-safe flow); `var destinations = Mp.Destinations.ToList();` (LOAD-BEARING snapshot — `Poll` mutates `Destinations`); `foreach` + `while (true) { var msg = Mp.Poll(dest); if (msg == null) break; ... cb.Invoke(dest, msg); count++; }`; return count. |
| 8 | `void _fireGlobalSendGoalIfExists(int, Term)` — inline lambda for `extractVariables`, `as Term` cast, method tear-offs, foreach-per-iteration onBind installation | `private void _fireGlobalSendGoalIfExists(int writerAddr, Term value)` — lambda `(object? val) => { var vars = new List<TermVar>(); if (val is Term term) _ExtractTermVarsRecursive(term, vars); return vars; }` typed as `Func<object?, IReadOnlyList<TermVar>>`. Early-return if `result == null`. Explicit cast `(Term)result.Value!`. Method-group conversions: `Runtime.Heap.IsReader`, `_LookupVariableForSerialization`. Foreach over `result.NewGoals` with explicit per-iteration local `var capturedAddr = newGoal.ReaderAddr;` and lambda `(Term v) => OnWriterBound(capturedAddr, v)`. |
| 9 | `({String creator, int creatorLocalId, bool isReader}) _lookupVariableForSerialization(int)` | `private (string Creator, int CreatorLocalId, bool IsReader) _LookupVariableForSerialization(int addr) => (Creator: AgentId, CreatorLocalId: addr, IsReader: Runtime.Heap.IsReader(addr));` — named ValueTuple with element names. |
| 10 | `void _extractTermVarsRecursive(Term, List<TermVar>)` — recursive pattern-match | `private void _ExtractTermVarsRecursive(Term term, List<TermVar> result)` — `if (term is VarRef varRef) { ... } else if (term is StructTerm structTerm) { foreach (var arg in structTerm.Args) _ExtractTermVarsRecursive(arg, result); }`. Null-coalesce `writerAddr ?? varRef.Addr`. Preserves trailing `// ConstTerm has no variables` comment verbatim. |
| 11 | `void registerGlobalSendSpawns(List<GlobalSendSpawn>)` | `public void RegisterGlobalSendSpawns(IReadOnlyList<GlobalSendSpawn> spawns)` — widened parameter; explicit per-iteration `var capturedReaderAddr = spawn.ReaderAddr;`; foreach Register + OnBind installation. |
| 12 | `void handleMadAssignment({named-required ×3})` — three-case dispatch | `public void HandleMadAssignment(GlobalName globalName, Term value, string fromAgent)` — positional params; `if (globalName.IsWriter && globalName.Index == 0) { _HandleSerializerAssignment(...); } else if (globalName.IsWriter) { _HandleWriterAssignment(...); } else { _HandleReaderAssignment(...); }`. ORDER LOAD-BEARING. NOT a `switch` expression. |
| 13 | `void _handleSerializerAssignment(Term, String)` — null-check `serializerWriterAddr` → throw; list-cell unwrap; localize-spawn-replace; `(freshWriter, freshReader)` destructure; build `StructTerm('.', [content, VarRef(freshReader)])`; bind; update permanent entry; reactivate | `private void _HandleSerializerAssignment(Term value, string fromAgent)` — `if (currentWriter == null) throw new InvalidOperationException("Serializer entry not initialized at index 0");`; `if (value is StructTerm structValue && structValue.Functor == "." && structValue.Args.Count == 2) { content = structValue.Args[0]; }`; nested localize-then-`RegisterGlobalSendSpawns`-then-`LocalizeTermWithResult`; `var (freshWriter, freshReader) = Runtime.Heap.AllocateVariable();`; `var listCell = new StructTerm(".", new List<Term> { content, new VarRef(freshReader) });`; `var activations = Runtime.Heap.BindVariable(currentWriter.Value, listCell);`; `Wp.UpdateSerializerWriter(freshWriter);`; foreach reactivate. PERMANENT — never `RemoveGlobalizeEntry`. |
| 14 | `void _handleWriterAssignment(GlobalName, Term, String)` — `wp.lookupByIndex`, localize, bind, reactivate, **remove** | `private void _HandleWriterAssignment(GlobalName globalName, Term value, string fromAgent)` — `if (entry == null) throw new InvalidOperationException($"No GlobalizeEntry at index {globalName.Index} for {globalName}");`; identical 4-step localize sequence; `Wp.RemoveGlobalizeEntry(globalName.Index);` (reached only when Index > 0; index-0 guard structurally bypassed). |
| 15 | `void _handleReaderAssignment(GlobalName, Term, String)` — `wp.findByRemote` linear scan, localize, bind, reactivate, **remove** | `private void _HandleReaderAssignment(GlobalName globalName, Term value, string fromAgent)` — `if (entry == null) throw new InvalidOperationException($"No LocalizeEntry for {globalName}: expected entry with (remoteAgent={globalName.Agent}, remoteIndex={globalName.Index})");` (multi-line error message preserved); identical 4-step localize; `Wp.RemoveLocalizeEntry(globalName.Agent, globalName.Index);` (RemoveAll-by-predicate). |
| 16 | `void handleMadAssignmentWithGlobalNames({named-required ×4})` — double-dispatch | `public void HandleMadAssignmentWithGlobalNames(GlobalName globalName, Term value, IReadOnlyList<GlobalName> nestedGlobalNames, string fromAgent)` — outer-localize (order LOAD-BEARING) then `HandleMadAssignment(globalName, value, fromAgent);`. |
| 17 | `void exportTerm(Term)` — writer-only-filter onBind installation | `public void ExportTerm(Term term)` — `var vars = new List<TermVar>(); _ExtractTermVarsRecursive(term, vars); foreach (var v in vars) { if (v.IsWriter) { var capturedAddr = v.Addr; Runtime.Heap.OnBind(v.Addr, (Term value) => OnWriterBound(capturedAddr, value)); Trace(...); } }`. WRITER-ONLY asymmetry preserved verbatim. |
| 18 | `void processSuspension(Set<int>)` — LOG-ONLY (push model) | `public void ProcessSuspension(ISet<int> blockingReaders)` — `foreach (var readerId in blockingReaders) Trace(...);`. Trailing `// No explicit request messages needed in madGLP push model` preserved verbatim. NO side effects beyond logging. |
| 19 | `void send(Term, bool, String, int, String)` — positional 5-arg; spec §11.5 with serializer-vs-normal branch | `public void Send(Term term, bool isWriter, string gnAgent, int gnIndex, string destAgent)` — positional. Extract vars; `Globalize(variables: vars, localAgent: AgentId, remoteAgent: destAgent, table: Wp)`; `RegisterGlobalSendSpawns(globalizeResult.Spawns)`; **preserve the spec §5.1 comment verbatim** (writer-side-passive, no onBind here); `var globalizedTerm = GlobalizeTermWithResult(...)`; `var globalName = isWriter ? GlobalName.Writer(gnAgent, gnIndex) : GlobalName.Reader(gnAgent, gnIndex);` ternary; `List<byte> payload; if (isWriter && gnIndex == 0) { payload = _serializer.CreateSerializerPayload(...); } else { payload = _serializer.CreateGlobalSendPayload(...); }`; `Mp.Add(new OutboundMessage(destination: destAgent, type: MessageType.Assignment, payload: payload));`. |

**Threading model (already ratified — DO NOT re-decide):**
- Per-agent **single-owning-context** invariant (escalation #4, commit `497428c8` for `heap_fcp`; same invariant applies to `MadContext` per the convspec's `rf-dart-isolate-singlethread-to-csharp-actor-or-pinned-thread` REUSED finding).
- Isolate manager uses `Channel<T>` mailbox (escalation #5, commit `12a468f5`). `MadContext` methods are invoked **only** from the per-agent consumer Task that drains the agent's `Channel<IsolateMessage>` — atomicity for compound check-then-act sequences (`HandleMadAssignment` → `_Handle*Assignment`; `FlushMessages` snapshot-then-drain; `_fireGlobalSendGoalIfExists` lookup-then-spawn) is end-to-end at the mailbox-consumption boundary.
- The C# port MUST NOT add `lock`/`SemaphoreSlim`/`Monitor`/`ConcurrentDictionary`/`ConcurrentBag`/`Interlocked` inside any `MadContext` method (would advertise a thread-safety property at the method boundary that the surrounding logic does not enforce at its own boundary).

## 3. Decomposed Task Units

- T1. Emit `namespace lib.multiagent;` plus single `using GlpRuntime.Runtime;` (intra-multiagent imports collapse silently).
- T2. Emit top-level `public delegate void MessageDeliveryCallback(string destination, OutboundMessage message);`.
- T3. Emit class shell `public class MadContext` with five get-only properties (`AgentId`, `Runtime`, `Wp`, `Mp`, `GlobalSendRegistry`), `private readonly PayloadSerializer _serializer;`, two nullable public callback fields (`OnMessageReady`, `TraceSink`).
- T4. Emit positional ctor `public MadContext(string agentId, GlpRuntime runtime)` with body-only field initialisation (six fields).
- T5. Emit `private void Trace(string msg) => TraceSink?.Invoke(msg);`.
- T6. Emit `public void OnWriterBound(int writerId, Term value)` (Trace + delegate).
- T7. Emit `public int FlushMessages()` with local-capture of `OnMessageReady`, `Mp.Destinations.ToList()` snapshot, nested while-true/break drain loop, counter return.
- T8. Emit `private void _fireGlobalSendGoalIfExists(int writerAddr, Term value)` — inline lambda for `extractVariables`, explicit `(Term)result.Value!` cast, method-group conversions, foreach with per-iteration `capturedAddr` capture for newGoals onBind installation.
- T9. Emit `private (string Creator, int CreatorLocalId, bool IsReader) _LookupVariableForSerialization(int addr)` as expression-bodied ValueTuple-return method.
- T10. Emit `private void _ExtractTermVarsRecursive(Term term, List<TermVar> result)` with `is X x` pattern-matching and `?? varRef.Addr` null-coalesce fallback; preserve `// ConstTerm has no variables` comment.
- T11. Emit `public void RegisterGlobalSendSpawns(IReadOnlyList<GlobalSendSpawn> spawns)` with explicit per-iteration `capturedReaderAddr` capture.
- T12. Emit `public void HandleMadAssignment(GlobalName globalName, Term value, string fromAgent)` three-case dispatcher (ORDER LOAD-BEARING).
- T13. Emit `private void _HandleSerializerAssignment(Term value, string fromAgent)` — null-check throw, list-cell unwrap pattern, localize sequence, ValueTuple deconstruction, list-cell construction, bind + update + reactivate (NEVER remove).
- T14. Emit `private void _HandleWriterAssignment(GlobalName globalName, Term value, string fromAgent)` — LookupByIndex, throw with verbatim message, localize sequence, bind + reactivate + Remove (transient).
- T15. Emit `private void _HandleReaderAssignment(GlobalName globalName, Term value, string fromAgent)` — FindByRemote, throw with multi-line message verbatim, localize sequence, bind + reactivate + Remove (transient).
- T16. Emit `public void HandleMadAssignmentWithGlobalNames(GlobalName globalName, Term value, IReadOnlyList<GlobalName> nestedGlobalNames, string fromAgent)` — double-dispatch (outer-localize first, then delegate).
- T17. Emit `public void ExportTerm(Term term)` — writer-only-filter onBind installation with explicit per-iteration capture.
- T18. Emit `public void ProcessSuspension(ISet<int> blockingReaders)` — LOG-ONLY; preserve trailing push-model comment verbatim.
- T19. Emit `public void Send(Term term, bool isWriter, string gnAgent, int gnIndex, string destAgent)` — positional; preserve spec §5.1 writer-side-passive comment verbatim; ternary `GlobalName.Writer/Reader`; serializer-vs-normal payload branch; `Mp.Add(new OutboundMessage(...))`.
- T20. Preserve all `///` XML-doc comments with spec §4 / §5.1 / §8.3 / §11.5 citations as `<remarks>` blocks on the corresponding C# methods.

## 4. Research Findings

None required. All twelve research findings cited by the convspec (`rf-dart-library-directive-to-csharp-namespace-no-counterpart`, `rf-dart-import-to-csharp-using`, `rf-dart-typedef-function-to-csharp-delegate`, `rf-dart-named-required-ctor-with-initialiser-list-to-csharp-positional-ctor`, `rf-dart-nullable-callback-invocation-to-csharp-null-conditional-invoke`, `rf-dart-method-with-string-interpolation-to-csharp-method-with-interpolation`, `rf-dart-snapshot-collection-before-mutating-iteration-to-csharp-tolist`, `rf-dart-method-group-tearoff-to-csharp-method-group-conversion`, `rf-dart-record-and-function-typed-param-to-csharp-valuetuple-and-func`, `rf-dart-is-test-flow-narrowing-to-csharp-is-pattern-with-decl`, `rf-dart-record-destructuring-to-csharp-valuetuple-deconstruction`, `rf-dart-stateerror-to-csharp-invalidoperationexception`, `rf-dart-named-required-to-csharp-positional-with-named-call-site`, `rf-dart-set-int-to-csharp-iset-int`, `rf-dart-ternary-with-static-factory-to-csharp-ternary-with-static-factory`) are grounded in Microsoft Learn citations recorded in the convspec's "Rationale and research provenance" section, or REUSED verbatim from sibling multiagent convspecs (`global_writers_table.dart.md`, `global_send.dart.md`, `message_queue.dart.md`, `mad_helpers.dart.md`, `payload_serializer.dart.md`, `repl_play_runner.dart.md`). The threading model (per-agent single-owning-context inherited from heap_fcp escalation #4, with isolate-manager `Channel<T>` mailbox from escalation #5) is already ratified at the project level.

## 5. Consistency Pass

- **Convspec mirror.** Every construct in the convspec's `constructs:` array (17 entries) maps to exactly one entry in §2 of this plan. Conversion-unit decisions match verbatim. Zero escalations carried forward.
- **Threading model coherence.** The single-owning-context invariant is consistent with: `GlobalWritersTable.dart.md` (canonical recording), `GlobalSendRegistry` (sibling — `global_send.dart`), `MessageQueue` (sibling — `message_queue.dart`), `PayloadSerializer` (sibling — `payload_serializer.dart`), `MadHelpers` (sibling — `mad_helpers.dart`). All concur on no-locks-inside-MadContext-methods, atomicity at the isolate-mailbox boundary.
- **Heap-callback contract.** `Runtime.Heap.OnBind(addr, callback)` is the canonical heap-callback installation point — `MadContext` installs callbacks at three sites (`_fireGlobalSendGoalIfExists` for newGoals; `RegisterGlobalSendSpawns` for spawns; `ExportTerm` for writers). Cross-references against `lib/runtime/heap_fcp.dart` (the heap implementation file — SCC sibling of *this* file via runtime.dart; see §7) for the heap's single-owning-context invariant ratified in escalation #4 (commit `497428c8`).
- **Term hierarchy coherence.** `Term`/`VarRef`/`StructTerm`/`ConstTerm` come from `lib/runtime/terms.dart` — pattern matching uses `is X x` declarations consistent with all other multiagent files that pattern-match on Terms.
- **Spec §5.1 / §8.3 / §11.5 invariants.** Writer-passive / reader-active asymmetry, three-case dispatch order, and the `'_send'` builtin's serializer-vs-normal branch are all preserved verbatim.
- **List-cell heap representation.** The convspec mandates `new StructTerm(".", new List<Term> { content, new VarRef(freshReader) })` for stream extension — NOT `IAsyncEnumerable<Term>`/`IObservable<Term>`. Cross-references against `body_kernels.dart` (SCC sibling — see §7) for list-cell construction conventions.
- **SCC coherence.** All five SCC siblings are read by `MadContext` either directly (`runtime.dart` for `GlpRuntime` field type, `body_kernels.dart` listed as a *caller* in the tombstone — `body_kernels.dart` calls into MadContext methods) or transitively (`glp_activation.dart` for the activation type that `Runtime.Heap.BindVariable` returns and `EnqueueReactivatedGoal` consumes, `runner.dart` for the bytecode-runner that drives the agent's runtime, `system_predicates.dart` for the `'_send'` builtin that calls `MadContext.Send`). See §7 for per-sibling cross-references.
- **No code emitted in this plan** (per FR-023 — spec/plan is structural, not compilable). Codegen consumes both the convspec and this plan.

## 6. Escalations

None.

## 7. Cycle Siblings

This file participates in cycle group 36 alongside five other files. Because madGLP composes the GLP runtime (an agent OWNS a `GlpRuntime`) and is *driven by* the body kernels / system predicates (which invoke `MadContext.Send` and `MadContext.HandleMadAssignment` on the agent's owning execution context), there is a structural cycle through the runtime peer files. The cycle is broken at code generation time by emitting all six files together in the same compilation unit (or by emitting forward-declared interfaces for the cross-references). The decisions below are co-dependent and must be co-emitted.

### lib/runtime/runtime.dart (`GlpRuntime`)

**Co-dependency:** `MadContext.Runtime` is a get-only property typed `GlpRuntime`; the `runtime` field is a constructor parameter; ALL bind / reactivate / allocate calls go through `Runtime.Heap.*` and `Runtime.EnqueueReactivatedGoal`.

**Co-dependent decisions:**
- `Runtime.Heap` must be a get-only property returning the heap interface that exposes `IsReader(int) -> bool`, `TryWriterForReader(int) -> int?`, `PairedReaderAddr(int) -> int?`, `AllocateVariable() -> (int, int)`, `BindVariable(int, Term) -> IEnumerable<TActivation>`, `OnBind(int, Action<Term>) -> void` (interface contract used as method groups and lambda targets throughout this file).
- `Runtime.EnqueueReactivatedGoal(TActivation)` must accept the element type returned by `Heap.BindVariable` (the activation enumeration). Whatever the runtime's convspec settles on for this element type, this plan defers to that decision.
- The single-owning-context invariant applies SYMMETRICALLY to `GlpRuntime` — i.e. `Runtime.Heap.*` and `Runtime.EnqueueReactivatedGoal` are invoked from the agent's owning execution context (the per-agent Channel<T> consumer Task). No locks inside `GlpRuntime` methods either; the project-level ratification covers both.

### lib/runtime/body_kernels.dart

**Co-dependency:** `body_kernels.dart` is listed as a CALLER of `mad_context.dart` in the tombstone (`callers:` field). The `'_send'` and message-handling kernels invoke `MadContext.Send`, `MadContext.HandleMadAssignment`, `MadContext.HandleMadAssignmentWithGlobalNames`, `MadContext.OnWriterBound`, `MadContext.ProcessSuspension`. The list-cell heap representation `StructTerm('.', [h, t])` is also constructed in body kernels (e.g. for list-cons goals), so the functor string `"."` is a shared invariant.

**Co-dependent decisions:**
- The agent context handed to body kernels at execution time must be `MadContext` — body_kernels emits calls like `madContext.HandleMadAssignment(...)` with positional arguments matching this plan's signatures.
- List-cell functor `"."` is shared verbatim between `_HandleSerializerAssignment` here and any list-construction in body kernels.
- Single-owning-context invariant — body kernels execute on the agent's owning Task; calls to `MadContext` methods are direct (no marshalling needed).

### lib/runtime/glp_activation.dart

**Co-dependency:** `Runtime.Heap.BindVariable` returns an enumeration of activations; `_HandleSerializerAssignment`, `_HandleWriterAssignment`, `_HandleReaderAssignment` iterate that enumeration and pass each element to `Runtime.EnqueueReactivatedGoal`. The activation type is defined in `glp_activation.dart`.

**Co-dependent decisions:**
- Whatever the activation type's name and shape is in the C# port (e.g. `GlpActivation`, or some equivalent struct/class), `BindVariable` must return `IEnumerable<TActivation>` (or `IReadOnlyList<TActivation>`) and `EnqueueReactivatedGoal(TActivation)` must accept the same element type. The foreach loops in this file (`foreach (var act in activations) Runtime.EnqueueReactivatedGoal(act);`) are agnostic to the specific shape.
- Activation enumeration is synchronous (no `IAsyncEnumerable`) — co-dependent with this file's synchronous bind-then-reactivate idiom.

### lib/bytecode/runner.dart

**Co-dependency:** The bytecode runner drives the GLP runtime that `MadContext` owns. `MadContext` is constructed once per agent and threaded through the runner via the agent's owning Task. The runner's instruction set includes the `'_send'` and message-receive opcodes (per spec §11) that ultimately invoke `MadContext.Send` / `MadContext.HandleMadAssignment`.

**Co-dependent decisions:**
- The runner's loop runs on the agent's owning execution context (Channel<T> consumer Task per escalation #5); when the runner needs to invoke madGLP operations, it does so by calling methods on the `MadContext` it was given. No cross-thread marshalling.
- The runner must not invoke `MadContext` methods from any thread other than the agent's owning Task — this is enforced structurally by the Channel-mailbox design (only the consumer Task drains messages and executes bytecode).
- Suspension on unbound readers (`processSuspension`) is invoked by the runner; the push-model semantics (LOG-ONLY in `ProcessSuspension`) are co-dependent with the runner's suspension protocol (the runner does not expect any message-queue side effect from this call).

### lib/runtime/system_predicates.dart

**Co-dependency:** `system_predicates.dart` implements the GLP builtins `'_send'(T, G, Q)` and `'_recv'(...)` (or however message receipt is surfaced as a builtin) per spec §11.5. These builtins delegate directly to `MadContext.Send` (for `'_send'`) and to the message-pump loop that calls `MadContext.HandleMadAssignment` / `MadContext.HandleMadAssignmentWithGlobalNames` (for receive).

**Co-dependent decisions:**
- The `'_send'(T, G, Q)` builtin's parameter unpacking must match `MadContext.Send(Term term, bool isWriter, string gnAgent, int gnIndex, string destAgent)` — i.e. system_predicates is responsible for extracting `isWriter`/`gnAgent`/`gnIndex` from the GLP-level `GlobalName` representation `G` and passing them positionally.
- `GlobalName.Writer(gnAgent, gnIndex)` vs `GlobalName.Reader(gnAgent, gnIndex)` are the static factories used here; they must be in agreement with whatever system_predicates uses to construct `GlobalName` values from the heap's representation of `G`.
- The serializer-vs-normal branch (`isWriter && gnIndex == 0`) in `Send` is the same dispatch condition used in `HandleMadAssignment` — both must remain in sync. If system_predicates introduces any new global-name shape (e.g. `GlobalName.Stream`), the dispatch must be extended consistently across all three files (`mad_context.dart`, `system_predicates.dart`, `mad_helpers.dart`).
