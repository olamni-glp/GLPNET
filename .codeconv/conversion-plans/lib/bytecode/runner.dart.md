---
path: lib/bytecode/runner.dart
cycle_group_id: 36
scc_siblings: [lib/multiagent/mad_context.dart, lib/runtime/body_kernels.dart, lib/runtime/glp_activation.dart, lib/runtime/runtime.dart, lib/runtime/system_predicates.dart]
generated_at: 2026-05-21T16:07:26Z
source_sha256: 7fdcc6faa358f2dacdfe6c63bf69d43b58bed08dc1f1ec6bfcefbf2d6aa4030a
schema_version: 1
---

# Conversion Plan: lib/bytecode/runner.dart

## 1. Source Analysis

The file is the FCP/WAM bytecode VM — the LARGEST file in the corpus
(4864 lines). Direct inspection confirms the convspec's structural
inventory:

**Top-level declarations (in source order)**

1. `import 'dart:async' show Timer;` — narrowed import; used by the
   `wait` / `wait_until` guards (`Timer(Duration(milliseconds: ...),
   () { heap.bindWriterConst(...); enqueueReactivatedGoal(...); })`).
2. Eight package-internal imports — `runtime/runtime.dart`,
   `runtime/machine_state.dart`, `runtime/terms.dart`,
   `runtime/commit.dart`, `runtime/cells.dart`,
   `runtime/system_predicates.dart`, `runtime/body_kernels.dart`,
   `multiagent/variable_table.dart show VariableEntry`.
3. Two same-directory imports — `opcodes.dart` and
   `opcodes_v2.dart as opv2` (prefix-aliased; the runner consults
   ~11 `opv2.<X>` types — `Unknown`, `HeadVariable`, `UnifyVariable`,
   `SetVariable`, `PutVariable` and v2-only opcode subclasses).
4. Three plain enums: `RunResult { terminated, suspended, yielded,
   outOfReductions }`, `UnifyMode { read, write }`,
   `GuardResult { success, failure, suspend }` (the `Suspend` member
   is documented-but-currently-unreached — the upstream
   `_dereferenceWithTracking` materialises suspension via Si before
   `_evaluateGuard` is called).
5. `typedef LabelName = String;`
6. Simple data classes:
   - `ReplModuleTarget { final String name; final BytecodeProgram
     program; }` — positional ctor.
   - `ReplModuleContext { final String moduleName; final Map<int,
     ReplModuleTarget> imports; final BytecodeProgram?
     combinedProgram; final String programKey; }` — required-named
     ctor + default `programKey = 'main'`.
   - `CallEnv { final Map<int, Term> argBySlot; }` — wraps a Map;
     exposes `arg(slot)` and `update(newArgs)` (clear + addAll).
   - `EnvironmentFrame { final EnvironmentFrame? parent; final int
     continuationPointer; final List<Object?> permanentVars; }` —
     ctor takes `size`, initialises `List.filled(size, null)`; 1-
     indexed `getY` / `setY`.
   - `_ParentContext { final Object? structure; final int s; final
     UnifyMode mode; final Object? writerId; }` — file-private
     (`_` prefix), used as `parentStack` element.
7. `class BytecodeProgram { final List<dynamic> ops; final
   Map<LabelName, int> labels; ... merge(other); toDisassembly();
   _instructionToString(op); static _indexLabels(ops); }` —
   heterogeneous v1+v2 opcode list; first-occurrence label indexing
   (multi-clause procedures share a label name; only the first index
   wins).
8. `class RunnerContext` — the BIG per-goal mutable state block:
   `rt`, `goalId`, mutable `kappa` (re-pointed by Requeue for tail
   calls), `env`, `sigmaHat` (Map<int, Object?> — tentative writer
   bindings staging area), `Si` (Set<int> — clause-level preliminary
   suspension set), `U` (Set<int> — goal-level accumulated suspension
   set), `inBody`, WAM-style `mode`/`S`/`currentStructure`,
   `clauseVars`, `parentStack`, `argSlots`, `guardArgSlot`,
   `reductionBudget`/`reductionsUsed`, environment-frame `E`/`CP`,
   trace hooks (`onActivation`, `onReduction`, `termFormatter`),
   `goalHead`/`goalProcName`, `showBindings`/`debugOutput`,
   `moduleContext` (`Object?`), `spawnedGoals` (trace list).
   Methods: `reformatHead()` (walks env arg slots 0..9, breaks on
   first null, formats with `termFormatter` if present) and
   `clearClause()` (resets clause-local: sigmaHat, Si, inBody,
   mode, S, currentStructure, clauseVars, guardArgSlot,
   parentStack — but NOT U).
9. `class BytecodeRunner { final BytecodeProgram prog; ... void
   run(cx); RunResult runWithStatus(cx); ... helpers ... }` — the
   dispatch engine.

**`runWithStatus` dispatch loop** — a tight `while (pc <
prog.ops.length)` loop with:
- A reduction-budget head check (`if (cx.reductionBudget != null &&
  cx.reductionsUsed >= cx.reductionBudget!) return
  RunResult.outOfReductions;`) + `cx.reductionsUsed++`.
- A 44+-arm `if (op is X) { ... continue; }` cascade in source
  order. Arms (categorised):
  - **Control / no-ops**: `Label`, `ClauseTry`, `GuardFail`,
    `Otherwise`, `Nop`.
  - **Structure-traversal save/restore**: `Push`, `Pop` (use
    `_StructureState` as the stash element in `clauseVars`).
  - **HEAD-phase unification**: `UnifyStructure`, `HeadConstant`,
    `HeadStructure`, `UnifyConstant`, `UnifyVoid`, `HeadNil`,
    `HeadList`.
  - **v2 unified HEAD instructions**: `opv2.Unknown`,
    `opv2.HeadVariable`, `opv2.UnifyVariable`, `opv2.SetVariable`,
    `opv2.PutVariable`.
  - **Mode selection (arg pre-flight)**: `RequireWriterArg`,
    `RequireReaderArg`.
  - **GET class (argument loading)**: `GetVariable`, `GetValue`.
  - **Guard family**: `Guard`, `Ground`, `Known`, `NoReaders`,
    `GroundEqual`.
  - **Commit (two-phase resolve)**: `Commit` — resolve Si against
    σ̂w, soft-fail-to-next-clause if any Si entry is unresolved;
    else convert `_TentativeStruct` → `StructTerm` (recursive walk
    handling `_ClauseVar` placeholders + fresh-variable allocation
    via `cx.rt.heap.allocateVariable()`), enforce WxW prohibition
    (throw `StateError` on writer→writer binding), call
    `CommitOps.applySigmaHatFCP(...)` (returns reactivated
    `GoalRef`s), enqueue + fire onActivation, reset clause state,
    set `inBody = true`.
  - **Clause control**: `ClauseNext` (union Si into U + clear
    clause state + jump-to-label), `TryNextClause` (soft-fail),
    `NoMoreClauses` (if U non-empty: `cx.rt.suspendGoalFCP(...)`
    + return `RunResult.suspended`; else `RunResult.terminated`),
    `UnionSiAndGoto`, `ResetAndGoto`, `SuspendEnd` (legacy).
  - **BODY-phase write dispatch**: `HeadBindWriter`,
    `HeadBindWriterArg`, `BodySetConst`,
    `BodySetStructConstArgs`, `BodySetConstArg`, `PutConstant`,
    `PutStructure`, `SetConstant`, `SetValue`, `SetVariable`
    (v1), `TailStep`, `PutNil`, `PutBoundConst`, `PutBoundNil`,
    `PutList`. All `bindWriterConst` / `bindWriterStruct` calls
    return a `List<GoalRef>` of reactivations that the runner
    enqueues + fires onActivation for.
  - **Goal control**: `Spawn(procedureLabel, arity)` — label
    lookup with body-kernel inline fallback (via
    `cx.rt.bodyKernels.lookup(name, arity)`); fresh goal id +
    `CallEnv`-copy + program/infrastructure-goal inheritance;
    `Requeue(procedureLabel)` — tail call: re-use `cx.goalId`,
    rebuild env from argSlots, manual state-reset, set
    `cx.kappa = entryPc`.
  - **Module RPC**: `Distribute(importIndex, functor, arity)`
    (static — via `ReplModuleContext.imports[importIndex]`),
    `Transmit(moduleVarIndex, functor, arity)` (dynamic —
    resolves module name from `clauseVars`). Both use
    `cx.rt.glpChannels[name]` to send.
  - **Environment + utility**: `Allocate(size)`, `Deallocate`,
    `Halt`, `Proceed`.
- Default fall-through `pc++;` for unmatched ops.

**Private helpers** (instance methods on `BytecodeRunner`):
- `_findNextClauseTry(fromPc)` — scan forward for `ClauseNext` /
  `ClauseTry` / `SuspendEnd` / `NoMoreClauses` (the first wins).
- `_softFailToNextClause(cx, currentPc)` — `cx.U.addAll(cx.Si);
  cx.clearClause();` (does NOT clear U).
- `_finalUnboundVar(cx, addr)` — `derefAddr`-driven; if final var
  is a writer, returns the paired reader via `pairedReaderAddr`.
- `_suspendAndFail(cx, readerId, pc)` — add reader to U + soft-
  fail + return next PC.
- `_suspendAndFailMulti(cx, readerIds, pc)` — same, multi.
- `_getArg(cx, argSlot)` — read from `cx.env.arg(slot)` (or
  `cx.argSlots[slot]` for guard-arg paths).

**Static helpers**:
- `_formatTerm(rt, term, {bool markReaders = true})` — recursive
  pretty-printer; `nil` → `[]`; null → `<null>`; bound reader →
  format value (no `?`); unbound reader → `Xid?` (if
  markReaders); writer → `Xid` (with display-id adjustment for
  ids ≥ 1000: `displayId = id - 1000`); list rendering with
  cycle detection via `HashSet<int> visited`.
- `_dereferenceWithTracking(term, cx)` — returns `(Object?
  deref, Set<int> trackedUnboundReaders)`; recursive walk
  consulting `clauseVars` → `sigmaHat` → heap (`isReaderBound` /
  `isFullyBound`) → recurse. Unwraps `ConstTerm` to primitive on
  the way out. Local closure `Dereference(t)` captures the
  `trackedUnboundReaders` set.
- `_isArithmeticOp(functor)` — `functor` ∈ {`+`, `-`, `*`, `/`,
  `mod`, `neg`}.
- `_evaluateArithmetic(op, args)` — assumed-ground arithmetic;
  throws `StateError` on non-numeric or arity mismatch.
- `_evaluateGuard(predicateName, args, cx)` — the BIG guard
  switch: arithmetic comparisons (`<`, `>`, `=<`, `>=`, `=:=`,
  `=\=`) using a local `evaluateNumeric(term)` recursive
  StructTerm-arithmetic walker; type guards (`ground`, `known`,
  `integer`, `string`, `constant`, `number`, `list`, `compound`,
  `module`); meta guards (`is_mutual_ref`, `unknown`, `otherwise`);
  time guards (`wait`, `wait_until` — both allocate a fresh
  reader/writer pair, schedule `Timer(Duration(ms), () {
  heap.bindWriterConst(writerId, true);
  enqueueReactivatedGoal(...); })`, and use a state-machine on
  `cx.rt.getWaitReader(cx.goalId)` to track first-call vs
  resume); structural-equality `=?=`; default arm
  (`print('[WARN] Unknown guard predicate: $predicateName')` +
  return `failure`).
- `_termsEqual(a, b, cx, [Set<(int,int)>? visited])` — recursive
  structural equality with VarRef dereferencing and cycle
  detection via address-pair tuples.
- `_convertTentativeToStruct(tentative, cx)` — recursive: turns
  `_TentativeStruct` into `StructTerm`; handles `_ClauseVar`
  placeholders (look up in `clauseVars`; if not yet resolved,
  allocate fresh writer/reader pair via
  `cx.rt.heap.allocateVariable()` and thread back into
  `clauseVars`).

**File-private helper classes** (leading `_`):
- `_TentativeStruct { final String functor; final int arity;
  final List<Object?> args; ... toString() => ...; }` — open
  mutable slot vector during HEAD WRITE phase.
- `_ClauseVar { final int varIndex; final bool isWriter; ... }`
  — placeholder for clause-variable position in a tentative
  structure.
- `_ListStruct { final Object? head; final Object? tail; }` —
  list cell (largely vestigial).
- `_StructureState { final int S; final UnifyMode mode; final
  dynamic currentStructure; }` — Push/Pop save state.
- `_ArgInfo { final int? writerId; final int? readerId; bool
  get isWriter; bool get isReader; }` — argument-mode wrapper
  (vestigial — not heavily used).

**Error-handling pattern** — across Spawn / Requeue / Distribute
/ Transmit and the guard default arm: `print('ERROR: ...'); return
RunResult.terminated;` (graceful per-goal exit, NOT a thrown
exception — the scheduler's "one bad goal does not take down the
runtime" contract).

## 2. Dart → C#/.NET Conversion Plan

This section mirrors the convspec verbatim (FR-024 cache hit; no
re-derivation). Construct → target decision (one bullet per
construct):

- **`import 'dart:async' show Timer;`** → `using System.Threading;`
  (Dart `Timer` → .NET `System.Threading.Timer`, one-shot via
  `dueTime: TimeSpan.FromMilliseconds(duration)`, `period:
  Timeout.InfiniteTimeSpan`). The `show Timer` allow-list has no
  .NET parallel (per `heap_fcp.dart.md`). LOAD-BEARING
  concurrency nuance: Dart Timer callbacks fire on the OWNING
  isolate's single-threaded event loop; .NET Timer callbacks
  fire on ThreadPool threads. The wait/wait_until callback
  performs heap-binding + goal-queue enqueue — both are
  HeapFCP/goal-queue mutations covered by the INHERITED
  concurrency escalation from `heap_fcp.dart.md` escalations[0]
  (NOT re-escalated here). Under recommended Option A (single-
  owner-thread per isolate-manager port — already ratified for
  this SCC per the threading model: commits `497428c8` /
  `12a468f5`), the timer callback MUST marshal back to the
  owning scheduler before touching `cx.Rt.Heap` /
  `cx.Rt.EnqueueReactivatedGoal` — concrete mechanism deferred
  to the multiagent isolate-manager port. The runner stays fully
  synchronous (no `async Task<RunResult>`).

- **Eight package-internal imports + two same-directory imports
  → `using <root>.Runtime;` + `using <root>.Multiagent;` +
  `using <root>.Bytecode;`** (per `heap_fcp.dart.md`
  rf-dart-import-relative-to-csharp-using-namespace cache hit).
  The Dart `show VariableEntry` allow-list has no .NET
  counterpart (`using` imports the full namespace surface). The
  prefix-import `import 'opcodes_v2.dart' as opv2;` maps to
  namespace-qualified references (`V2.HeadVariable`,
  `V2.PutVariable`, ...) — codegen MUST KEEP v1 and v2 opcode
  types in disjoint namespaces (per
  `opcodes_v2.dart.md`) so that the runner's `if (op is
  V2.HeadVariable)` arms remain distinguishable from
  `if (op is V1.HeadVariable)`.

- **Three plain enums** (`RunResult`, `UnifyMode`, `GuardResult`)
  → C# plain enums in declaration order, members PascalCased
  (`Terminated`/`Suspended`/`Yielded`/`OutOfReductions`,
  `Read`/`Write`, `Success`/`Failure`/`Suspend`). Casing nuance:
  these are NOT spec-named identifiers (unlike
  `WrtTag`/`RoTag`/`ValueTag` in `cells.dart.md`), so Microsoft
  naming conventions apply. Underlying type `int`; no `[Flags]`
  (mutually exclusive). `GuardResult.Suspend` member RETAINED
  even though `_evaluateGuard` never returns it today — the
  three-valued shape is the load-bearing surface.

- **`typedef LabelName = String;`** → C# `using LabelName =
  string;` file-scoped using alias (per `opcodes.dart.md`
  cache hit). NOT a record-struct wrapper (no value-semantic
  distinction in source).

- **Five simple-data classes** (`ReplModuleTarget`,
  `ReplModuleContext`, `CallEnv`, `EnvironmentFrame`,
  `_ParentContext`) → reference `class` (NOT `record class` /
  `struct` / `record struct`) per `heap_fcp.dart.md`
  rf-dart-final-field-class-to-csharp-getonly-class. Get-only
  auto-properties for `final` fields. Required-named ctor
  params → regular C# ctor params per `opcodes_v2.dart.md`.
  `EnvironmentFrame` constructor body initialises a
  `List<object?>` of length `size` filled with `null` (mirror
  of `List.filled(size, null)`). `_ParentContext` (file-
  private) → `internal sealed class ParentContext` (or `file
  class` if C# 11+ target).

- **`BytecodeProgram`** → reference class with
  `IReadOnlyList<object> Ops { get; }` (`object` NOT `dynamic`
  per the rf-dart-dynamic-list-of-sum-types-to-csharp-list-of-
  object NEW research finding — C# `dynamic` is DLR-overhead
  and out-of-character for the hot-path bytecode dispatcher; the
  source already uses `if (op is X)` pattern-matches), `Dictionary<string,
  int> Labels { get; }`, `Merge(other)` returning a fresh
  program with `[..other.Ops, ..this.Ops]` (other FIRST —
  "prepend stdlib"), `ToDisassembly()` using `StringBuilder`,
  `_instructionToString(op)` private static using C# pattern-
  match on the four V2 opcode types
  (`PutVariable`/`HeadVariable`/`UnifyVariable`/`SetVariable`)
  with the same `isReader ? "reader" : "writer"` mapping;
  fallback `op.ToString()!`. `IndexLabels` preserves first-
  occurrence-wins semantics.

- **`RunnerContext`** → reference class with public-setter
  auto-properties for every mutable field (`Kappa`, `InBody`,
  `Mode`, `S`, `CurrentStructure`, `GuardArgSlot`,
  `ReductionBudget`, `ReductionsUsed`, `E`, `CP`, `GoalHead`,
  `GoalProcName`). `final` collection fields → get-only
  properties holding mutable collections (`Dictionary<int,
  object?> SigmaHat`, `HashSet<int> Si`, `HashSet<int> U`,
  `Dictionary<int, object?> ClauseVars`, `Stack<ParentContext>
  ParentStack`, `Dictionary<int, Term> ArgSlots`, `List<string>
  SpawnedGoals`). Function-typed fields → `Action<GoalRef>?
  OnActivation`, `Action<int, string, string>? OnReduction`,
  `Func<Term, bool, string>? TermFormatter`. `ModuleContext`
  → `object?` (no `IModuleContext` interface introduced —
  `is ReplModuleContext` pattern-match is the faithful
  translation). `ClearClause()` is a void method that mutates
  collections via `.Clear()` + property assignment.

- **`BytecodeRunner`** → reference class with `Prog` field +
  `RunWithStatus(cx)` method + private instance helpers
  (`_findNextClauseTry`, `_softFailToNextClause`,
  `_finalUnboundVar`, `_suspendAndFail`, `_suspendAndFailMulti`,
  `_getArg`) and private static helpers (`_FormatTerm`,
  `_DereferenceWithTracking`, `_IsArithmeticOp`,
  `_EvaluateArithmetic`, `_EvaluateGuard`, `_TermsEqual`,
  `_ConvertTentativeToStruct`). NOT async (synchronous
  dispatch).

- **`RunWithStatus` dispatch loop** → `while (pc < ops.Count)`
  with a 44+-arm `if (op is X opx) { ... pc++; continue; }`
  cascade in SOURCE ORDER (frequently-reached arms first —
  `Label`, `ClauseTry`, `GuardFail` lead). Reduction-budget
  head check uses `is int budget` pattern-match. Dart
  `prog.labels[name]!` → C# `prog.Labels[name]` (throws
  `KeyNotFoundException` — semantically equivalent to Dart
  `Map[k]!` throwing on null). Dart `print(...)` debug calls
  → `Console.WriteLine(...)` guarded by `cx.DebugOutput`.

- **HEAD-phase opcode arms** (~24 arms — listed in §1) → 1:1
  pattern-match arms with the SAME control flow as Dart;
  tentative bindings into `cx.SigmaHat[addr] = ConstTerm(value)`
  or `cx.SigmaHat[wid] = nested` (where `nested` is a new
  `TentativeStruct(functor, arity, new object?[arity])`); Si
  membership via `cx.Si.Add(addr); pc++; continue;` (two-phase
  semantics — DO NOT eagerly soft-fail); soft-fail via
  `_softFailToNextClause(cx, pc); pc =
  _findNextClauseTry(pc); continue;`. WAM mode transitions
  (`cx.Mode = UnifyMode.Write; cx.CurrentStructure = nested;
  cx.S = 0;`) preserved verbatim. The open-coded `while (value
  is VarRef) { ... }` dereference loops translate verbatim to
  C# `while (value is VarRef vr) { ... }`.

- **Commit arm** → 1:1 translation:
  1. **Phase 2 resolve**: `var resolvedSi = new HashSet<int>();
     foreach (var readerAddr in cx.Si) { var writerAddr =
     cx.Rt.Heap.TryWriterForReader(readerAddr); if (writerAddr
     is null || !cx.SigmaHat.ContainsKey(writerAddr.Value))
     resolvedSi.Add(readerAddr); }`. If `resolvedSi.Count > 0`:
     `cx.U.UnionWith(resolvedSi); cx.Si.Clear();
     _softFailToNextClause(cx, pc); pc =
     _findNextClauseTry(pc); continue;`. Else
     `cx.Si.Clear();`.
  2. **Tentative→Struct conversion** via
     `_ConvertTentativeToStruct(tentative, cx)` (recursive,
     handles `_ClauseVar` placeholders + fresh-variable
     allocation via `cx.Rt.Heap.AllocateVariable()` returning a
     ValueTuple `(int Writer, int Reader)` per
     `heap_fcp.dart.md` cache hit).
  3. **WxW enforcement**: `foreach (var kvp in
     convertedSigmaHat) { if (kvp.Value is VarRef vr &&
     cx.Rt.Heap.IsWriter(vr.Addr)) throw new
     InvalidOperationException(...); }` (`StateError` →
     `InvalidOperationException` per `heap_fcp.dart.md` cache).
  4. **Apply**: `var acts = CommitOps.ApplySigmaHatFCP(heap:
     cx.Rt.Heap, sigmaHat: convertedSigmaHat);`.
  5. **Reactivation enqueue**: `foreach (var a in acts) {
     cx.Rt.Gq.Enqueue(a); if (cx.OnActivation is { } onA)
     onA(a); }`.
  6. **State reset**: `cx.SigmaHat.Clear(); cx.ArgSlots.Clear();
     cx.CurrentStructure = null; cx.S = 0; cx.Mode =
     UnifyMode.Read; cx.ParentStack.Clear(); cx.InBody = true;`.
  Iteration-order nuance: Dart Map iteration is INSERTION ORDER
  per the Dart language spec; .NET `Dictionary` is insertion-
  order in practice on .NET 5+ but not contractually. Codegen
  SHOULD prefer `OrderedDictionary<TKey,TValue>` (.NET 9+) if
  target framework supports it; else `Dictionary<int, object?>`
  with the observed insertion-order pattern is faithful for
  the current behaviour.

- **Clause-control arms** (`ClauseNext`, `TryNextClause`,
  `NoMoreClauses`, `UnionSiAndGoto`, `ResetAndGoto`,
  `SuspendEnd`) → 1:1 translation. `NoMoreClauses` is the
  suspension gate: `if (cx.U.Count > 0) {
  cx.Rt.SuspendGoalFcp(goalId: cx.GoalId, kappa: cx.Kappa,
  readerVarIds: cx.U); cx.U.Clear(); cx.InBody = false;
  return RunResult.Suspended; } cx.InBody = false; return
  RunResult.Terminated;`.

- **BODY-phase arms** (~14 arms) → 1:1 translation. `BindWriter*`
  calls return `IReadOnlyList<GoalRef>` activations (per
  `heap_fcp.dart.md`); codegen MUST NOT drop the return value
  (would silently lose reactivations). PutNil sets `cx.ArgSlots
  [slot] = new ConstTerm("nil");` (the `nil` atom convention
  per `terms.dart.md`). PutList builds `new StructTerm(".",
  new[]{head, tail})` cons cell.

- **Goal-control arms (Spawn, Requeue)** → Spawn: label lookup
  via `prog.Labels.GetValueOrDefault(op.ProcedureLabel, -1)`;
  if negative, try body-kernel inline via
  `cx.Rt.BodyKernels.Lookup(name, op.Arity)` (returns a
  delegate; `BodyKernelResult.Abort` → `Console.WriteLine + return
  RunResult.Terminated`); else fresh goal id from
  `cx.Rt.NextGoalId++`, fresh `CallEnv(new Dictionary<int,
  Term>(cx.ArgSlots))` (copy ctor mirrors Dart `Map<int,
  Term>.from(...)`), inherit goal-program (`cx.Rt.GetGoalProgram
  (cx.GoalId)`) + infrastructure-goal status, enqueue
  `GoalRef(newGoalId, entryPc)`. Requeue (tail call): `cx.Kappa
  = entryPc; pc = entryPc; continue;` with manual state-reset
  preserved verbatim (`cx.SigmaHat.Clear(); cx.U.Clear();
  cx.ClauseVars.Clear(); cx.InBody = false; cx.Mode =
  UnifyMode.Read; cx.S = 0; cx.CurrentStructure = null;`).
  Codegen MUST NOT rely on .NET tail-call optimisation (none
  in the JIT for arbitrary calls); the tail call IS just a pc
  / kappa reassignment in this interpreter.

- **Module-RPC arms (Distribute, Transmit)** → 1:1 translation;
  pattern-match gate `cx.ModuleContext is ReplModuleContext
  replCtx`; static lookup via `replCtx.Imports.GetValueOrDefault
  (op.ImportIndex)`; channel send `cx.Rt.GlpChannels[name]
  .Send(goalTerm)` returns activations synchronously; enqueue +
  fire OnActivation. Codegen MUST NOT introduce async/await for
  module RPC — synchronous in source. Transmit additionally
  resolves the module name from `cx.ClauseVars[op.ModuleVarIndex]`
  (dereferencing VarRef + extracting ConstTerm.Value).

- **Environment + utility arms** (`Allocate`, `Deallocate`,
  `Nop`, `Halt`, `Proceed`) → 1:1 translation. `Allocate`: `cx.E
  = new EnvironmentFrame(parent: cx.E, continuationPointer:
  cx.CP ?? 0, size: op.Size);`. `Deallocate`: `cx.CP = cx.E
  ?.ContinuationPointer; cx.E = cx.E?.Parent;`. `Halt`: `return
  RunResult.Terminated;`. `Proceed`: invoke `cx.OnReduction?
  .Invoke(cx.GoalId, cx.GoalHead ?? "?", "");` then `return
  RunResult.Terminated;`.

- **Five file-private helper classes** (`_TentativeStruct`,
  `_ClauseVar`, `_ListStruct`, `_StructureState`, `_ArgInfo`)
  → `internal sealed class` (or `file class` on C# 11+).
  `_TentativeStruct.args` field becomes `public IList<object?>
  Args { get; }` (reference immutable, contents mutated in
  place). `_StructureState.currentStructure` typed `dynamic` →
  `object?` per the carry-forward dynamic-vs-object nuance.
  `ToString()` overrides preserved verbatim.

- **`_FormatTerm`** → private static method returning `string`,
  uses `StringBuilder` for the general-structure case, `HashSet
  <int> visited` allocated per top-level call (cycle-detection
  scope). `markReaders` becomes an optional parameter with
  default `true` (per `opcodes.dart.md`). All literals (`"[]"`,
  `"<null>"`, `"?"` suffix, `"<circular>"`, `"$functor($args)"`)
  preserved byte-for-byte.

- **`_DereferenceWithTracking`** → private static method
  returning `(object? Deref, HashSet<int> UnboundReaders)`
  (ValueTuple per `heap_fcp.dart.md` cache hit). Internal local
  function `Dereference(object? t)` captures the
  `unboundReaders` HashSet via closure (per Microsoft Learn
  local functions). The order of consultation `clauseVars →
  sigmaHat → heap` preserved verbatim. ConstTerm-unwrap on the
  way out preserved (caller sees the primitive, not the
  wrapper).

- **`_IsArithmeticOp`** → `private static bool IsArithmeticOp
  (string functor) => functor is "+" or "-" or "*" or "/" or
  "mod" or "neg";` (C# pattern-matching `or` keyword).

- **`_EvaluateArithmetic`** → 1:1 translation; throws
  `InvalidOperationException` (`StateError` → mapping per
  `heap_fcp.dart.md`).

- **`_EvaluateGuard`** → 1:1 translation; private static method
  returning `GuardResult`; top-level `switch (predicateName)`
  with one `case "<":` etc. arm per Dart `case '<':`. Local
  function `evaluateNumeric(term)` recursively walks
  `StructTerm` arithmetic with a `case '+' / '-' / '*' / '/' /
  '//' / 'mod' / 'neg'` switch. Dart `num` → C# `double` via a
  helper `static bool TryAsNum(object? v, out double result)`
  that handles `int` / `double` / `ConstTerm` wrappers
  uniformly. Dart `~/` (integer division) → C# `(int)(a / b)`;
  Dart `%` (mod) → C# `%`. `=:=` arithmetic-equality is `da ==
  db` (exact double equality). `wait` / `wait_until` allocate
  variable via `cx.Rt.Heap.AllocateVariable()` (ValueTuple),
  set up `System.Threading.Timer` (one-shot:
  `Timeout.InfiniteTimeSpan` period, `dueTime: TimeSpan.
  FromMilliseconds(duration)`), callback marshals back to the
  owning scheduler (per the import-construct nuance) before
  binding writer + enqueueing reactivations. `=?=` ground-
  equality → `_TermsEqual` (recursive structural equality with
  cycle detection).

- **`_TermsEqual`** → recursive `private static bool` with
  `HashSet<(int, int)>? visited = null;` default param and
  `visited ??= new HashSet<(int, int)>();` initialiser. C#
  ValueTuple supports `==` and `HashSet<ValueTuple<T1,T2>>`
  element-acceptance via `IEquatable<ValueTuple<T1,T2>>`
  (Microsoft Learn). Address-pair semantic preserved.

- **`_ConvertTentativeToStruct`** → private static method on
  `BytecodeRunner` (Dart's top-level free function maps to a
  static class member — C# has no top-level functions outside
  C# 9 top-level statements). Recursive walk preserved.

- **Error-handling pattern** → `Console.WriteLine("ERROR: ...");
  return RunResult.Terminated;`. Codegen MUST NOT promote to
  `throw new InvalidOperationException(...)` — the source
  DELIBERATELY returns `Terminated` (graceful per-goal exit;
  the scheduler's "one bad goal does not take down the runtime"
  contract).

- **`argSlots` Dictionary perf idiom** → `Dictionary<int, Term>`
  with `TryGetValue(slot, out var t)` for missing-key reads,
  `Clear()` for reset, copy-ctor for cloning. `Span<T>` NOT
  applicable (Dictionary is hash-backed, not contiguous);
  `permanentVars` on `EnvironmentFrame` MAY be a future
  `object?[]` + `Span<object?>` optimisation but OUT OF SCOPE
  for this faithful-translation spec.

## 3. Decomposed Task Units

- **T1**: Emit namespace declaration + using directives
  (`System.Threading`, `<root>.Runtime`, `<root>.Multiagent`,
  `<root>.Bytecode`, `<root>.Bytecode.V2`) at top of
  `runner.cs`.
- **T2**: Emit `RunResult` enum (4 members,
  Terminated/Suspended/Yielded/OutOfReductions).
- **T3**: Emit `UnifyMode` enum (Read/Write).
- **T4**: Emit `GuardResult` enum (Success/Failure/Suspend —
  retain Suspend).
- **T5**: Emit `using LabelName = string;` file-scoped alias.
- **T6**: Emit `ReplModuleTarget` class (positional ctor, two
  get-only props).
- **T7**: Emit `ReplModuleContext` class (required-named-style
  ctor + default `programKey = "main"`).
- **T8**: Emit `CallEnv` class (Dictionary<int, Term> get-only
  property + `Arg(slot)`, `Update(newArgs)` methods).
- **T9**: Emit `EnvironmentFrame` class (`parent`/`CP`/
  `permanentVars` triple, 1-indexed `GetY`/`SetY`).
- **T10**: Emit `ParentContext` class (`internal sealed` or
  `file class`).
- **T11**: Emit `BytecodeProgram` class (`Ops` +
  `Labels` + `Merge` + `ToDisassembly` + `IndexLabels` static).
- **T12**: Emit `RunnerContext` class (per-goal state block —
  every mutable field as public-setter auto-property; every
  collection field as get-only property holding mutable
  collection; constructor with required + optional-default
  params; `ClearClause()` + `ReformatHead()` methods).
- **T13**: Emit `BytecodeRunner` class shell (`Prog` field,
  ctor, `Run(cx)` wrapper, `RunWithStatus(cx)` method
  signature, private helper method signatures).
- **T14**: Emit `RunWithStatus` dispatch loop (`while (pc <
  ops.Count)` + reduction-budget head check + 44+-arm `if (op
  is X opx)` cascade in SOURCE ORDER — frequently-reached arms
  first).
- **T15**: Emit HEAD-phase opcode arms (~24 arms listed in §1
  / §2; pattern-match arms; tentative bindings into SigmaHat;
  Si accumulation; soft-fail dispatch; WAM mode transitions).
- **T16**: Emit Guard family arms (`Guard`, `Ground`, `Known`,
  `NoReaders`, `GroundEqual`) — gather args, call
  `_DereferenceWithTracking`, accumulate Si, call
  `_EvaluateGuard`, soft-fail on failure.
- **T17**: Emit `Commit` arm (Phase 2 Si resolve →
  tentative-struct convert → WxW prohibition → ApplySigmaHatFCP
  → reactivation enqueue → state reset).
- **T18**: Emit clause-control arms (`ClauseNext`,
  `TryNextClause`, `NoMoreClauses`, `UnionSiAndGoto`,
  `ResetAndGoto`, `SuspendEnd`).
- **T19**: Emit BODY-phase arms (~14 arms — `HeadBindWriter`,
  `BodySetConst`, `BodySetStructConstArgs`, `BodySetConstArg`,
  `PutConstant`, `PutStructure`, `SetConstant`, `SetValue`,
  `SetVariable` (v1), `TailStep`, `PutNil`, `PutBoundConst`,
  `PutBoundNil`, `PutList`). Preserve `BindWriter*` return-
  value capture + reactivation enqueue.
- **T20**: Emit goal-control arms (`Spawn` with body-kernel
  inline fallback path; `Requeue` tail-call with manual state-
  reset).
- **T21**: Emit module-RPC arms (`Distribute` static via
  `ReplModuleContext.Imports`; `Transmit` dynamic via
  `ClauseVars` module-name resolution; both call
  `cx.Rt.GlpChannels[name].Send(...)` synchronously).
- **T22**: Emit environment + utility arms (`Allocate`,
  `Deallocate`, `Nop`, `Halt`, `Proceed`,
  `RequireWriterArg`, `RequireReaderArg`).
- **T23**: Emit private instance helpers (`_findNextClauseTry`,
  `_softFailToNextClause`, `_finalUnboundVar`,
  `_suspendAndFail`, `_suspendAndFailMulti`, `_getArg`).
- **T24**: Emit `_FormatTerm` static helper (recursive term
  pretty-printer with cycle detection).
- **T25**: Emit `_DereferenceWithTracking` static helper
  (ValueTuple return + local-function closure over
  unboundReaders set).
- **T26**: Emit `_IsArithmeticOp` + `_EvaluateArithmetic`
  static helpers.
- **T27**: Emit `_EvaluateGuard` static helper (BIG switch
  with arithmetic / type / control / time / `=?=` arms + local
  `evaluateNumeric` recursive walker + `TryAsNum` numeric
  helper + Timer-based wait/wait_until with marshalling
  callback).
- **T28**: Emit `_TermsEqual` static helper (recursive
  structural equality with `HashSet<(int,int)>` cycle
  detection).
- **T29**: Emit `_ConvertTentativeToStruct` static helper
  (recursive `_TentativeStruct` → `StructTerm` with
  `_ClauseVar` placeholder handling + fresh-variable
  allocation).
- **T30**: Emit five internal-sealed helper classes
  (`TentativeStruct`, `ClauseVar`, `ListStruct`,
  `StructureState`, `ArgInfo`).
- **T31**: Emit error-handling sites (graceful
  `Console.WriteLine("ERROR: ...") + return
  RunResult.Terminated;` pattern at Spawn/Requeue/Distribute/
  Transmit/guard-default).
- **T32**: Emit Timer-callback marshalling shim for
  wait/wait_until (concrete mechanism inherits the
  isolate-manager port — see §6 — but the shim site must be
  emitted with a TODO/marker referencing the
  cross-SCC contract so codegen can fill in the
  marshalling primitive once the multiagent port lands).

## 4. Research Findings

None required. All research findings were already produced by
the upstream convspec and are cached / cross-referenced (FR-024
cache hits). The convspec's 17 listed `research_finding_id`s
break down as:

- **Cache hits** (no re-research needed): `rf-dart-import-
  relative-to-csharp-using-namespace` (from `heap_fcp.dart.md`),
  `rf-dart-plain-enum-to-csharp-enum` (from `heap_fcp.dart.md` /
  `cells.dart.md` / `machine_state.dart.md` / `opcodes.dart.md`),
  `rf-dart-typedef-string-to-csharp-using-alias` (from
  `opcodes.dart.md`), `rf-dart-final-field-class-to-csharp-
  getonly-class` (from multiple priors), `rf-dart-mutable-state-
  class-identity-equality-to-csharp-class` (from
  `heap_fcp.dart.md`), `rf-dart-record-return-to-csharp-
  valuetuple` (from `heap_fcp.dart.md`), `rf-dart-tostring-
  interp-to-csharp-tostring-interp` (from `opcodes.dart.md`),
  `rf-dart-print-and-terminate-to-csharp-equivalent` (cache
  hit on debug-print mapping).
- **NEW research findings recorded in the convspec** (already
  ratified): `rf-dart-timer-to-csharp-system-threading-timer`
  (Microsoft Learn `System.Threading.Timer`), `rf-dart-dynamic-
  list-of-sum-types-to-csharp-list-of-object` (Microsoft Learn
  `dynamic` keyword DLR-overhead documentation), `rf-dart-
  pattern-match-cascade-dispatch-to-csharp-is-pattern-cascade`
  (Microsoft Learn pattern-matching JIT lowering), `rf-dart-
  three-valued-unification-dispatch-arm-to-csharp-equivalent`
  (GLP runtime spec + Microsoft Learn HashSet/Dictionary),
  `rf-dart-two-phase-commit-operator-to-csharp-equivalent`
  (FCP commit operator spec + Microsoft Learn
  `OrderedDictionary`), `rf-dart-body-phase-write-dispatch-to-
  csharp-equivalent`, `rf-dart-goal-spawning-and-rpc-dispatch-
  to-csharp-equivalent`, `rf-dart-guard-evaluation-with-suspend-
  tracking-to-csharp-equivalent`, `rf-dart-environment-frame-
  and-utility-dispatch-to-csharp-equivalent`, `rf-dart-private-
  class-with-mutable-list-args-to-csharp-internal-sealed-class`,
  `rf-dart-recursive-static-helper-with-cycle-detection-to-
  csharp-equivalent`, `rf-dart-map-int-key-as-sparse-array-to-
  csharp-dictionary`.

All findings cite Microsoft Learn URLs or in-repo
`docs/glp-runtime-spec.txt` / `docs/wam.pdf`. No further research
needed.

## 5. Consistency Pass

Cross-checks performed against convspec + sibling convspecs +
CLAUDE.md + the threading-model ratification commits
(`497428c8` heap_fcp single-owning-context; `12a468f5`
isolate_manager Channel<T> mailbox):

- **σ̂w (sigmaHat) / Si / U two-phase semantics** — derived
  from `docs/glp-runtime-spec.txt` (§ FCP three-phase
  execution) + `commit.dart.md` (Phase 2 Si resolve) +
  `suspension.dart.md` (U accumulation). CONSISTENT.
- **WAM read/write mode + structure traversal** — derived from
  `docs/wam.pdf` (Warren Abstract Machine, §3 unification
  modes). CONSISTENT with the runner's `cx.Mode = UnifyMode.X`
  / `cx.S` / `cx.CurrentStructure` cursor.
- **Reference-vs-record/struct for every helper class** —
  inherits from `heap_fcp.dart.md` rf-dart-mutable-state-class-
  identity-equality-to-csharp-class. CONSISTENT — every type
  here is held by reference from `RunnerContext` /
  `BytecodeRunner` / σ̂w slots and mutated in place.
- **`List<dynamic> ops` → `List<object>` + `is X` cascade** —
  derived from the rf-dart-dynamic-list-of-sum-types-to-csharp-
  list-of-object NEW finding + Microsoft Learn pattern-matching.
  CONSISTENT — the source already uses `if (op is X)` everywhere;
  the C# `dynamic` would be a regression to DLR overhead.
- **v1 vs v2 opcode namespacing** — from `opcodes.dart.md` /
  `opcodes_v2.dart.md` (separate marker interfaces `IOp` and
  `IOpV2`, no shared base). CONSISTENT — Dart prefix-import
  `opv2.X` → C# namespace-qualified `V2.X`.
- **`StateError` → `InvalidOperationException`** — from
  `heap_fcp.dart.md` rf-dart-staterror-to-csharp-
  invalidoperationexception. CONSISTENT throughout.
- **`StringBuffer` → `StringBuilder`** — from
  `heap_fcp.dart.md` cache. CONSISTENT in `ToDisassembly` and
  `_FormatTerm`.
- **`bindWriterConst` / `bindWriterStruct` return value** —
  from `heap_fcp.dart.md` — returns `IReadOnlyList<GoalRef>`
  reactivations. CONSISTENT — the runner enqueues + fires
  OnActivation for every returned activation.
- **Synchronous dispatch (no async Task<RunResult>)** —
  derived from the source (no `async`/`await` in
  `runWithStatus` or any opcode handler; suspension via
  `RunResult.Suspended` return value, NOT awaiting a Future).
  CONSISTENT — codegen MUST NOT promote to async.
- **Module-RPC synchronous channel send** — derived from the
  source (channel `send(goalTerm)` returns
  `List<GoalRef>` synchronously). CONSISTENT — no async.
- **Concurrency / threading model** — INHERITED from
  `heap_fcp.dart.md` escalations[0], NOW RATIFIED via commit
  `497428c8` (single-owning-context for heap_fcp/mad_context)
  + `12a468f5` (Channel<T> mailbox for isolate_manager). The
  runner's RunnerContext is owned by exactly one OS thread /
  Task scheduler at a time; mutators are single-writer per
  goal. Timer callback in wait/wait_until MUST marshal back to
  the owning scheduler before touching `cx.Rt.Heap` /
  `cx.Rt.EnqueueReactivatedGoal`. CONSISTENT — the convspec
  documents this; the SCC's ratified threading model preserves
  the contract by giving every goal/agent a single-owning Task
  on a per-agent Channel<T>. CONSISTENT also with `mad_context`
  (single-owning-context invariant) and `isolate_manager`
  (Channel<T> mailbox — the runner's Spawn / Requeue / module-
  RPC arms become message sends from the agent's mailbox-Task
  context).
- **`GuardResult.Suspend` retained** — from the convspec; the
  three-valued shape mirrors GLP's three-valued unification.
  CONSISTENT — even though `_evaluateGuard` never returns it
  today, pruning would silently break a future re-introduction.
- **Dictionary insertion-order nuance for σ̂w iteration** —
  documented in the convspec. CONSISTENT — `OrderedDictionary
  <TKey,TValue>` (.NET 9+) is the contractually-ordered
  alternative; current `Dictionary<int, object?>` preserves
  insertion-order in practice on .NET 5+.

**SCC coherence check** (cross-references with the 5 siblings —
see also §7):

- **runner.dart ↔ runtime.dart**: `cx.rt` is `GlpRuntime`;
  the runner calls `cx.rt.gq.enqueue(a)` (goal queue),
  `cx.rt.heap.*` (HeapFCP — escalation #4 ratified),
  `cx.rt.suspendGoalFCP(...)`,
  `cx.rt.enqueueReactivatedGoal(...)`, `cx.rt.bodyKernels`
  (kernel registry — see body_kernels sibling),
  `cx.rt.glpChannels[name]` (module-RPC channels),
  `cx.rt.nextGoalId++`, `cx.rt.infrastructureGoalIds`,
  `cx.rt.getGoalProgram(goalId)`/`setGoalProgram(...)`,
  `cx.rt.getWaitReader(goalId)`. All these are members of
  GlpRuntime in `runtime.dart` — co-dependent on the runtime
  convspec / plan. **Co-dependent decisions**: GlpRuntime as
  reference class; goalQueue type; channels type;
  bodyKernels registry type. The runtime plan MUST land a
  `GlpRuntime` class shape that matches the runner's expected
  member signatures.
- **runner.dart ↔ body_kernels.dart**: `cx.rt.bodyKernels.lookup
  (name, arity)` returns a delegate that the Spawn arm calls
  inline. The body kernel delegate signature + return type
  (`BodyKernelResult` enum with `Abort` member, or void with
  exception-based abort?) is co-dependent. **Co-dependent
  decisions**: body kernel delegate type, registry shape,
  abort return convention.
- **runner.dart ↔ glp_activation.dart**: `GoalRef` (which the
  runner allocates in Spawn, enqueues for reactivations) is
  defined in glp_activation. The constructor signature
  (positional vs named; field types) is co-dependent.
  **Co-dependent decisions**: GoalRef record/class shape + ctor
  signature; the runner emits `new GoalRef(newGoalId, entryPc)`
  in Spawn and `cx.OnActivation?.Invoke(a)` for each reactivated
  GoalRef — both must match the glp_activation plan's
  type definition.
- **runner.dart ↔ system_predicates.dart**: imported via
  `import 'package:glp_runtime/runtime/system_predicates.dart';`.
  The runner does not call them DIRECTLY (the dispatcher
  handles guards inline via `_evaluateGuard`), but the system-
  predicate table feeds into guard dispatch and the type
  taxonomy (`ground`/`known`/`integer`/`string`/`constant`/
  `number`/`list`/`compound`/`module`/`is_mutual_ref`/
  `unknown`/`otherwise`/`wait`/`wait_until`/`=?=`). **Co-
  dependent decisions**: the guard-name string literals
  (codegen MUST preserve the exact predicate-name strings used
  in `_evaluateGuard`'s switch arms — these are observed by
  the compiler and any test that constructs Guard ops).
- **runner.dart ↔ mad_context.dart**: `cx.moduleContext` is
  `Object?` and pattern-matches `is ReplModuleContext`;
  mad_context defines the multi-agent context type that
  module-RPC arms may dispatch into (Transmit's dynamic
  module-name resolution). **Co-dependent decisions**: the
  multi-agent context type's shape (whether mad_context
  implements `IModuleContext` or stays `object?` per the
  current spec) — currently NO `IModuleContext` interface is
  introduced; codegen uses `object?` + `is X` discrimination.
  Single-owning-context invariant (commit `497428c8`) is
  shared by both files.

All consistency checks pass. No gaps require resolution.

## 6. Escalations

None.

The single threading-model concern is INHERITED from
`heap_fcp.dart.md` escalations[0] and `isolate_manager.dart.md`
escalations (both NOW RATIFIED via commits `497428c8` and
`12a468f5`); per task instruction the threading model is NOT
re-decided in this plan. The Timer-callback marshalling
mechanism (T32) is a deferred codegen detail covered by the
ratified single-owning-context invariant — the concrete
primitive (e.g., the per-agent `Channel<T>` mailbox post or a
`SynchronizationContext` send) is selected by the isolate-
manager plan, which the runner plan inherits.

Every other decision is verbatim-derivable from
runner.dart's convspec, the cached idioms / research findings
across the corpus, the GLP runtime spec, the FCP commit
operator spec, and CLAUDE.md's threading-model ratification
commits. No silent guesses (SC-008).

## 7. Cycle Siblings

This SCC member is one of six co-dependent files. No member
can be converted in isolation; the co-dependent type/interface
decisions below MUST stay consistent across all six plans
(FR-011).

### Sibling: `lib/multiagent/mad_context.dart`

**Co-dependent decisions:**
- **Multi-agent context type shape**: the runner's
  `cx.ModuleContext` is `object?` + pattern-matches `is
  ReplModuleContext` (currently); if mad_context introduces
  an `IModuleContext` interface that both `ReplModuleContext`
  and the multi-agent context implement, the runner's
  `ModuleContext` property type would need to change. Both
  plans MUST converge on whether to introduce
  `IModuleContext` OR keep `object?` + runtime type-discrimination.
  Current convspec keeps `object?` (faithful translation).
- **Single-owning-context invariant** (commit `497428c8`):
  mad_context is one of the files that anchors this invariant.
  The runner inherits it transitively (every RunnerContext is
  owned by exactly one OS thread / Task scheduler). Both plans
  MUST emit code that preserves the invariant — no shared
  ConcurrentDictionary / lock / Interlocked on RunnerContext or
  HeapFCP state.
- **Agent-context lifecycle**: if mad_context defines an
  `AgentContext` type that owns its `RunnerContext` (and the
  Channel<T> mailbox per `isolate_manager`), the runner's
  Spawn / Requeue / module-RPC arms become message sends from
  the agent's mailbox-Task body. The runner does NOT directly
  depend on AgentContext (the runner sees only
  `cx.Rt` = GlpRuntime + `cx.ModuleContext` = object?), but
  the AgentContext wires the runtime in. Cross-plan
  consistency: the AgentContext plan must instantiate
  RunnerContext on the agent's owning Task; the runner plan
  must NOT emit any code that escapes RunnerContext from its
  owning context.

### Sibling: `lib/runtime/body_kernels.dart`

**Co-dependent decisions:**
- **Body kernel delegate signature**: the runner's Spawn arm
  calls `cx.Rt.BodyKernels.Lookup(name, arity)` and invokes
  the returned delegate inline. The delegate signature
  (parameters: `GlpRuntime rt, object?[] args` — or different
  positional/typed shape?) MUST match across both plans.
- **`BodyKernelResult` enum**: the runner checks `if (result
  == BodyKernelResult.Abort) { Console.WriteLine(...); return
  RunResult.Terminated; }`. The enum's members + the abort
  semantic (graceful per-goal exit, not exception) MUST be
  stable across both plans.
- **Registry data structure**: `BodyKernels` is a member of
  `GlpRuntime`; both the runtime plan (which defines it) and
  the body_kernels plan (which defines its registry shape)
  MUST agree on the `Lookup(name, arity)` signature.

### Sibling: `lib/runtime/glp_activation.dart`

**Co-dependent decisions:**
- **`GoalRef` type shape**: the runner allocates `new
  GoalRef(newGoalId, entryPc)` in Spawn; enqueues `GoalRef`s
  from `BindWriter*` reactivations; passes them to
  `cx.OnActivation?.Invoke(a)` and `cx.Rt.Gq.Enqueue(a)`. The
  `GoalRef` constructor parameter order + field types MUST
  match the glp_activation plan's record/class definition.
- **`GoalRef` value-vs-reference**: if glp_activation makes
  `GoalRef` a `record class` with value equality, the runner's
  enqueue + activation paths still work (reference identity
  not required for `GoalRef` itself — only for RunnerContext /
  BytecodeProgram / heap cells). If it's a `record struct`,
  the runner's `Action<GoalRef>?` typing still works (value
  type).
- **`OnActivation` host hook signature**: the runner's
  `cx.OnActivation` is `Action<GoalRef>?`; glp_activation MUST
  define `GoalRef` as a publicly accessible non-nested type so
  the runner's property typing is valid.

### Sibling: `lib/runtime/runtime.dart`

**Co-dependent decisions:**
- **`GlpRuntime` class shape**: the runner accesses ~12
  members on `cx.Rt` (see §5 SCC coherence check). The runtime
  plan MUST emit a `GlpRuntime` class with at minimum:
  `Heap` (HeapFCP), `Gq` (goal queue), `BodyKernels`
  (kernel registry), `GlpChannels` (Dictionary<string, ...>
  for module RPC), `NextGoalId` (int with public setter or
  `Interlocked.Increment` — but per single-owning-context
  invariant, plain `++` is correct), `InfrastructureGoalIds`
  (HashSet<int>), `GetGoalProgram(int)` / `SetGoalProgram(int,
  BytecodeProgram)`, `GetWaitReader(int)`, `SuspendGoalFcp(int,
  int, IEnumerable<int>)`, `EnqueueReactivatedGoal(...)`.
- **Goal queue type**: the runner enqueues `GoalRef`s; the
  runtime plan defines the queue type (likely `GoalQueue` —
  see `goal_queue.dart.md`). Both plans MUST agree on
  `Gq.Enqueue(GoalRef)` signature.
- **`GlpChannels` type**: the runner does `cx.Rt.GlpChannels
  [name].Send(goalTerm)`. The channels dictionary's value
  type and `Send` method signature (synchronous, returning
  `IReadOnlyList<GoalRef>` activations) MUST be defined in the
  runtime plan and stay consistent.

### Sibling: `lib/runtime/system_predicates.dart`

**Co-dependent decisions:**
- **Guard predicate name registry**: the runner's
  `_EvaluateGuard` switch arms have string literals
  (`"<"`, `">"`, `"=<"`, `">="`, `"=:="`, `"=\\="`, `"ground"`,
  `"known"`, `"integer"`, `"string"`, `"constant"`, `"number"`,
  `"list"`, `"compound"`, `"module"`, `"is_mutual_ref"`,
  `"unknown"`, `"otherwise"`, `"wait"`, `"wait_until"`,
  `"=?="`). These string literals MUST match the
  system_predicates plan's registry (the compiler emits Guard
  ops with these names; the runner pattern-matches against
  them). Codegen MUST NOT case-fold, abbreviate, or otherwise
  transform.
- **Type taxonomy semantics**: `ground` (all args are
  ground), `known` (all args are bound — may have unbound
  writers as long as readers are bound), `integer` /
  `string` / `constant` / `number` / `list` / `compound` —
  each corresponds to a type test on the dereferenced value.
  The semantics MUST stay consistent with whatever
  system_predicates documents.
- **`wait` / `wait_until` semantics**: the runner schedules a
  `System.Threading.Timer` with the duration extracted from
  the guard arg; on fire it binds a writer and enqueues
  reactivations. system_predicates documents the predicate
  signature (`wait(ms)`, `wait_until(timestamp)`) and the
  state-machine using `cx.rt.getWaitReader(goalId)` — both
  plans MUST agree on the runtime-side method's signature
  and the state-machine semantics.
