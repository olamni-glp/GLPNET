# Dart `runner.dart` architecture map — porting reference for T021 (feature 050)

**Purpose.** A navigation map of `glp_runtime/lib/bytecode/runner.dart` (≈5570 lines,
the bytecode execution engine) to guide the faithful 1:1 Gleam port in
`glp_gleam/src/glp/engine/runner.gleam` (task **T021**). Derived 2026-07-12 by a
structural survey of the file.

**🔴 Use discipline.** This is a *navigation aid*, not gospel. Line numbers are from
the frozen Dart oracle and are stable, but **verify each handler against the actual
`runner.dart` source at port time** before porting it (CLAUDE.md: don't relay a
survey as truth for load-bearing semantics; frozen language semantics — any gap
STOPs and escalates). The Dart is the operational oracle; where this map and the
source disagree, the source wins.

**Port status (see also `runner.gleam` header + `specs/050-full-gleam-combined/tasks.md` T021 note).**
Slice **21a/b DONE** (`d38d65ae`): control spine + HEAD-constant + Commit + suspend,
running `flip` end-to-end. **Remaining:** 21c (HEAD structures + GetVariable/GetValue),
21d (BODY put_*/spawn + reactivation), then T023 guards, T024 kernels. Unported
opcodes currently return `RunnerError(Unimplemented)` — surfaced, never skipped.

---

## 0. Top-level types (context for the port)

- `enum RunResult { terminated, suspended, yielded, outOfReductions }` — L17. Loop return type.
- `enum UnifyMode { read, write }` — L42. WAM read/write mode for structure traversal.
- `enum GuardResult { success, failure, suspend }` — L45.
- `enum _DGMatch { ok, fail, suspend }` — L52. Per-arg outcome for runtime-defined-guard head matching.
- `class BytecodeProgram` — L56. `ops: List<dynamic>` (mixed v1 `Op` + v2 `OpV2`), `labels: Map<String,int>` (first-occurrence label→index, `_indexLabels` L67), `definedGuards: Map<String,GuardProcSpec>` (049 runtime-defined-guard side table). `merge()` prepends another program (L81).
- `class CallEnv` — L124. `argBySlot: Map<int,Term>`; `arg(slot)` (L131), `update(newArgs)` clears+replaces (L134). Goal-call arguments (A-registers as Terms).
- `class EnvironmentFrame` — L142. WAM Y-registers: `parent`, `continuationPointer:int`, `permanentVars: List<Object?>`; `getY/setY` 1-indexed (L154/157). Allocate/Deallocate only.
- `class _ParentContext` — L161. Nested-structure-build save record: `structure`, `s:int`, `mode:UnifyMode`, `writerId`. Pushed on `parentStack`.
- `class ReplModuleTarget` / `ReplModuleContext` — L20 / L27. Module-RPC routing for Distribute/Transmit.
- `class BytecodeRunner` — L277. The engine. Holds `prog` (final) and static config: `policyGuardForm` (L286, 'a'/'b'), `systemDefinedGuards` (L296, embedded `satisfiable/2` + `$sat:*` helper clause specs), `_definedGuardMaxDepth = 5000` (L459). Entry points: `run(cx)` L366 → `runWithStatus(cx)` L847.

Helper classes at file bottom: `_ArgInfo` (L5495, vestigial), `_TentativeStruct` (L5506), `_ClauseVar` (L5518), `_ListStruct` (L5529, unused), `_StructureState` (L5540, Push/Pop save), free fn `_convertTentativeToStruct` (L5552).

---

## 1. Execution state

Main class `BytecodeRunner` (L277). Per-run/per-goal context: `RunnerContext` (L175–275).

| Field | Dart type | Role |
|---|---|---|
| `rt` | `GlpRuntime` | Shared machine state: `rt.heap`, `rt.gq` (goal queue), `rt.bodyKernels`, `rt.glpChannels`, `nextGoalId`, `suspendGoalFCP`, `enqueueReactivatedGoal`. RunnerContext is the per-goal slice. (L176) |
| `goalId` | `int` | This goal's id. (L177) |
| `kappa` | `int` mut | Goal's procedure entry PC (κ) — the resume/suspend address; **not** the live PC (that's local `pc` in `runWithStatus`). Mutated by `Requeue` (L178, L3362). |
| `env` | `CallEnv` | Argument registers A1..An as Terms. (L181) |
| `sigmaHat` | `Map<int,Object?>` | **σ̂w — tentative writer bindings** (writerAddr→value), applied at Commit. Value may be `_TentativeStruct`/`ConstTerm`/`StructTerm`/`VarRef`/`null`. (L180) |
| `Si` | `Set<int>` | Clause-level preliminary suspension set (reader addrs). Resolved vs σ̂w at Commit; merged into U on soft-fail. (L181) |
| `U` | `Set<int>` | Goal-level suspension set (reader addrs). Accumulates across clauses; consumed by NoMoreClauses. (L182) |
| `inBody` | `bool` | Phase flag: false=HEAD/GUARD, true=BODY (set by Commit L2850). (L183) |
| `mode` | `UnifyMode` | Read/write mode for structure traversal. (L186) |
| `S` | `int` | Structure pointer — index into `currentStructure.args`. (L187) |
| `currentStructure` | `Object?` | Structure being traversed/built (`StructTerm` READ/BODY; `_TentativeStruct` HEAD-build). (L188) |
| `clauseVars` | `Map<int,Object?>` | Clause var bindings varIndex→value (int addr / VarRef / Term / `_ClauseVar` / `_TentativeStruct` / `_StructureState`). **Negative sentinel keys:** `-1`=current structure-build target writer; `-2`=target argSlot. (L189) |
| `parentStack` | `List<_ParentContext>` | Nested structure building stack. (L192) |
| `argSlots` | `Map<int,Term>` | BODY-phase output arg registers for the next Spawn/Requeue/Guard; cleared after each call and at Commit. (L196) |
| `guardArgSlot` | `int?` | When set, structure being built is a pre-commit guard arg → stored to `argSlots[guardArgSlot]` not heap-bound. (L199) |
| `reductionBudget`/`reductionsUsed` | `int?`/`int` | Reduction limit + counter (`outOfReductions`). (L202/203) |
| `E`/`CP` | `EnvironmentFrame?`/`int?` | WAM env + continuation pointers (Allocate/Deallocate). (L206/207) |
| `onActivation` | `void Function(GoalRef)?` | Host log hook per reactivated/spawned goal. (L209) |
| trace fields | assorted | `spawnedGoals`, `goalHead`, `onReduction`, `showBindings`, `termFormatter`, `moduleContext`, `reformatHead()` L221. (L212–246) |

`clearClause()` (L264): clears σ̂w, Si, resets inBody=false, mode=read, S=0, currentStructure=null, clauseVars, guardArgSlot, parentStack. **Does NOT clear U.**

Mapping to port items: X-registers = `env`/`CallEnv` (call args) + `clauseVars` (temporaries) + `argSlots` (outgoing); heap/σ̂ = `rt.heap` + `sigmaHat`; Si=`Si`; U=`U`; tentative structs = `_TentativeStruct` values in σ̂w/currentStructure; PC = local `pc` (κ persisted in `kappa`); phase = `inBody`+`mode`.

**Gleam adaptation already taken (slice 21a/b):** Si/U carry **writer** addresses (not reader addrs) because the Gleam foundation is writer-keyed (`heap.suspend_on_writer`/`bind_writer` reactivate on writer binding), and `deref` of an unbound reader already yields its terminal writer. Same observable behaviour; documented in `runner.gleam` header.

---

## 2. The three phases (HEAD / GUARD / BODY)

No explicit phase opcodes; phase is implicit in ordering + two flags:

- **HEAD/GUARD:** `inBody == false`. HEAD ops (`HeadConstant`, `HeadStructure`, `HeadNil`, `HeadList`, `UnifyVariable/Constant/Void`, v2 `GetVariable/GetValue`, `opv2.HeadVariable`) write only into `sigmaHat`/`clauseVars`/`Si`. GUARD ops (`Guard`, `Ground`, `Known`, `NoReaders`, `GroundEqual`, `GuardNeedReader*`) are pure tests over σ̂w+heap.
- **HEAD→(GUARD)→BODY transition at `Commit` (L2703).** No separate GUARD marker — guards run between last HEAD op and Commit. Commit: (1) resolve `Si` vs σ̂w (L2704–2722); (2) convert `_TentativeStruct` in σ̂w → real `StructTerm` (L2738–2808); (3) enforce WxW (L2816–2823); (4) apply σ̂w to heap via `CommitOps.applySigmaHatFCP` (L2827), enqueuing woken goals; (5) clear σ̂w+argSlots, reset structure state, **`inBody=true`** (L2842–2850).
- **BODY:** `inBody == true`. Body ops (`Put*`, `Set*`, `PutStructure`, `BodySet*`, `Spawn`, `Requeue`, `Distribute`, `Transmit`) allocate heap vars + bind writers directly, enqueuing activations immediately (no σ̂w). Most body handlers guard on `if (cx.inBody)`.

### `_ClauseVar` (L5518) — HEAD-phase unresolved variable placeholder
Fields `varIndex:int`, `isWriter:bool`. **Created** during WRITE-mode structure building when a clause var has no value yet (`opv2.HeadVariable` L1066; `opv2.UnifyVariable` fallback L1906). Stored into both the tentative struct arg slot and `clauseVars[varIndex]`. **Resolved** at Commit while converting a `_TentativeStruct`: look up `clauseVars[varIndex]`; if VarRef use with mode correction (writer↔reader via `pairedReaderAddr`/`tryWriterForReader`, L2751–2773); if unresolved, allocate a fresh heap var (L2778–2787). **Discarded** by `clearClause()` on clause failure.

### `_TentativeStruct` (L5506) — HEAD-phase structure being built
Fields `functor:String`, `arity:int`, `args:List<Object?>`. **Created** when a HEAD op meets an unbound writer where a structure is required (mode→WRITE): `UnifyStructure` L970, `HeadStructure` L1281/1309/1424/1449 (`HeadList` uses a plain `StructTerm`, L4422). Recorded as `sigmaHat[writerAddr] = tentativeStruct`, set `currentStructure`, mode=write, S=0. Filled by `UnifyVariable/UnifyConstant/UnifyStructure/UnifyVoid`. **Completed** when `S >= arity`. **Converted** to `StructTerm` at Commit (L2743–2803, recursively via `_convertTentativeToStruct` L5552). **Discarded** by `clearClause()`.

`_StructureState` (L5540): Push (L892) saves `{S,mode,currentStructure}` into `clauseVars[regIndex]`; Pop (L904) writes the built `currentStructure` into `clauseVars[regIndex]` then restores S/mode/currentStructure.

---

## 3. Main dispatch loop

**`RunResult runWithStatus(RunnerContext cx)` — L847–4588** (`run` L366 is a void wrapper).

- **PC init:** `var pc = cx.kappa;` (L848) — entry PC, not 0.
- **Loop:** `while (pc < prog.ops.length)` (L856). Per iteration: budget check → `outOfReductions` (L858–860); `cx.reductionsUsed++` (L861); `final op = prog.ops[pc];` (L863).
- **Dispatch:** a long **`if (op is X) { … continue; }` type-test chain** (~60 arms), each ending `pc++; continue;` or `pc = <target>; continue;` or `return`. Default fall-through L4585 (`pc++`).
- **Backtrack to next clause:**
  - `_findNextClauseTry(fromPc)` L370 — scans forward for next `ClauseNext | ClauseTry | SuspendEnd | NoMoreClauses`, returns its index (or `ops.length`).
  - `_softFailToNextClause(cx, pc)` L381 — `U.addAll(Si)` then `clearClause()`. Called on any HEAD/GUARD mismatch.
  - Idiom: `_softFailToNextClause(cx, pc); pc = _findNextClauseTry(pc); continue;`.
  - `ClauseTry` (L869) `cx.clearClause()`. `ClauseNext` (L2858) `U.addAll(Si)+clearClause` then jump. `TryNextClause` (L2867) = soft-fail. `Commit` ends selection. `NoMoreClauses` (L2875)/`SuspendEnd` (L2907) terminate the try-chain.
- **Suspension (two ways):** (1) incrementally in HEAD/GUARD (add to Si or drive `_suspendAndFail`/`_suspendAndFailMulti`→U); (2) final at `NoMoreClauses` L2875: if U non-empty → `rt.suspendGoalFCP(goalId, kappa, readerVarIds: U)`, `U.clear()`, `inBody=false`, **`return suspended`**; else **`return terminated`** (definitive failure).
- **Reactivation** is external/writer-driven: when a writer paired to a suspended reader binds (BODY or Commit), the heap returns woken `GoalRef`s, `gq.enqueue`d; scheduler re-runs `runWithStatus` from `cx.kappa`.
- **Suspend helpers:** `_finalUnboundVar(cx,addr)` L397 (deref chain → reader addr, writer→paired reader via `pairedReaderAddr`); `_suspendAndFail(cx,readerId,pc)` L423 = `U.add`+soft-fail+findNext; `_suspendAndFailMulti` L432.
- **Fairness:** `TailStep` (L3208) `rt.tailReduce(goalId)`; yield → re-enqueue `GoalRef(goalId,kappa)` + `return yielded`; else jump to `op.label`.

---

## 4. Per-opcode handlers (line ranges + effect)

### Control / clause selection
- **Label** (868) `pc++`. **ClauseTry** (869–873) `clearClause()`. **ClauseNext** (2858–2863) `U.addAll(Si)`, `clearClause()`, jump `labels[label]`. **TryNextClause** (2867) soft-fail.
- **Commit** (2703–2852) — HEAD→BODY (see §2): two-phase Si resolution (L2704), tentative→StructTerm incl. `_ClauseVar` resolution (L2743), WxW guard throw (L2821), `CommitOps.applySigmaHatFCP` binds+returns woken (L2827), `inBody=true`.
- **NoMoreClauses** (2875–2895) suspend-or-fail terminal. **SuspendEnd** (2907–2925, legacy) same. **UnionSiAndGoto** (2898)/**ResetAndGoto** (2904) legacy: clearClause+jump.
- **Otherwise** (877–889) — succeeds (`pc++`) iff U empty; else soft-fail (GUARD-like). **GuardFail** (874) `pc++` no-op.
- **Proceed** (4575–4583) `onReduction` + `return terminated`. **Halt** (4570) `return terminated`. **Nop** (4564) `pc++`. **TailStep** (3208) fairness yield.

### HEAD (inBody==false)
- **HeadConstant** (1126–1220) — match arg vs `op.value`. Writer arg: bound→deref+compare (mismatch→soft-fail); unbound→`sigmaHat[argAddr]=ConstTerm(value)` (L1180). Reader arg: unbound→`Si.add(finalUnboundVar)` (L1187); bound→compare. Ground path TODO stub (L1216).
- **HeadStructure** (1222–1522) — largest handler. `argSlot>=10` ⇒ clause var (nested), else call arg. Bound matching struct→READ (`currentStructure=value,mode=read,S=0`); unbound writer→WRITE mode-convert: `_TentativeStruct`, `sigmaHat[wid]=struct`, mode=write (L1281/1309/1424/1449); unbound reader→`Si.add` (L1326/1466); mismatch→soft-fail. Throws on unexpected arg (L1521).
- **HeadNil** (4186–4379) — match `[]` (=`ConstTerm('nil')`). Unbound writer→`sigmaHat[addr]=ConstTerm('nil')`; unbound reader→`Si.add(finalUnboundVar)`; struct/other→soft-fail.
- **HeadList** (4381–4455) — match `[H|T]` (`'[|]'/2`, also `'.'` for ValueTag). Bound list→READ; unbound writer→WRITE with `StructTerm('[|]',[])`; unbound reader→`Si.add`.
- **opv2.HeadVariable** (1045–1107) — structure-arg var at S. WRITE: place value or new `_ClauseVar` into `struct.args[S]` (L1066), `S++`. READ: extract `struct.args[S]`; first occ→clauseVars, else unify-by-equality (mismatch→soft-fail).
- **UnifyVariable** (`opv2.UnifyVariable`, 1834–2198) — core three-valued structure-subterm handler. WRITE into `_TentativeStruct` (HEAD L1841)/`StructTerm` (BODY L1910): place clause var with correct writer/reader mode (`pairedReaderAddr`/`tryWriterForReader`), alloc fresh on first occ (L1899/1965); on `S>=len` in BODY bind target writer (`bindWriterStruct`)+enqueue, or store to `argSlots[guardArgSlot]`; then parentStack unwind (L2000–2073) recursively completes ancestors. **READ (L2077):** Reader×Reader→**FAIL** (L2093); Reader×Writer→bind/capture (L2099); Reader×ground→alloc fresh in σ̂w (L2127). Writer mode: **WxW** both unbound→soft-fail (L2148); else `sigmaHat[clauseVarAddr]=value`.
- **UnifyConstant** (1660–1813) — WRITE: place `op.value` into struct arg; on completion `bindWriterStruct` (L1684) or guard arg slot (L1707). READ: compare at S — match→`S++`; unbound writer→`sigmaHat[wid]=ConstTerm(value)` (L1767); unbound reader→`Si.add(rid)`,`S++` (L1790); mismatch→soft-fail.
- **UnifyVoid** (1815–1831) — WRITE: fill `op.count` void (`null`) slots; READ: `S += count`.
- **UnifyStructure** (923–1012) — nested struct at S. READ: match/enter; unbound writer→WRITE `_TentativeStruct` (L963); unbound reader→`U.add`+soft-fail (**uses U directly**, L986); mismatch→soft-fail. WRITE: nested `_TentativeStruct` in parent slot.
- **GetVariable** (v1, 1525–1555) — load arg into `clauseVars[varIndex]` (writer→addr; reader→VarRef **without suspending** L1545; ground→term).
- **GetValue** (v1, 1557–1657) — unify arg with `clauseVars[varIndex]`; reader-unbound→`_suspendAndFail` (L1602/1648).
- **opv2.GetVariable** (2201–2359) — unified first-occurrence load. Writer mode: bind existing writer via σ̂w or store goal writer/value; reader mode: goal-writer→store addr, **goal-reader×head-reader→FAIL** (L2338), const/struct→store.
- **opv2.GetValue** (2362–2519) — unified subsequent-occurrence unify. Writer mode: compare/bind into σ̂w (unbound reader alias L2450); reader mode: bind goal writer to stored (unbound→`_suspendAndFail` L2495) or compare reader addrs (`tryWriterForReader`).
- **opv2.Unknown** (1017–1042) — succeed iff clause var unbound; else soft-fail.
- **RequireWriterArg** (1110)/**RequireReaderArg** (1117) — mode gate; wrong-mode→jump `labels[failLabel]`.
- **HeadBindWriter** (2667)/**HeadBindWriterArg** (2672) — legacy: `sigmaHat[wid]=null`.
- **Push** (892–901)/**Pop** (904–920) — save/restore structure-traversal state via `_StructureState`.

### GUARD (pure, inBody==false)
- **GuardNeedReader** (2679)/**GuardNeedReaderArg** (2689) — require reader bound; else `_suspendAndFail`.
- **Guard** (3503–3654) — general guard call. Deref args via `_dereferenceWithTracking` (+ nested-in-compound via `_collectUnboundReaders`). **049 runtime-defined guards first** (L3568): dispatch `satisfiable/2` to `systemDefinedGuards` when `policyGuardForm!='a'`, else `prog.definedGuards`; via `_evalDefinedGuardCall`. Otherwise: any unbound reader (and not `unknown`)→`_suspendAndFailMulti` (L3620); else `_evaluateGuard` (L3625). Negation inverts success↔failure, suspend unchanged (L3628).
- **Ground** (3656–3794) — `ground(X)`. Walk term (`collectUnbound`, cycle-safe): unbound writer=FAIL / unbound reader=SUSPEND / ground=SUCCEED (writer presence dominates — SRSW can't wait on a writer). Negation inverts.
- **Known** (3796–3916) — bound=SUCCEED / unbound reader=SUSPEND / unbound writer=FAIL. X itself only, not subterms.
- **NoReaders** (3918–4047) — readers present=SUSPEND on them (never fails); none=SUCCEED. Negation inverts.
- **GroundEqual** (4049–4183) — `X =?= Y`. `collectUnbound` both; unbound writer=FAIL, unbound reader=SUSPEND, else `_termsEqual` (cycle-safe). Negation inverts equality only.

### BODY (inBody==true)
- **opv2.PutVariable** (2971–3046) — place clause var into `argSlots[argSlot]` with writer/reader mode. VarRef→ValueTag on heap (`storeTermOnHeap`) L2991; mode via `pairedReaderAddr`/`tryWriterForReader`; `_ClauseVar`→alloc fresh (L3017); StructTerm/ConstTerm reader mode→alloc+bind fresh (L3022/3027); null→alloc (L3032).
- **PutConstant** (3048–3055) — alloc var, `bindWriterConst(op.value)`, `argSlots[argSlot]=VarRef(reader)`.
- **PutNil** (4458)/**PutBoundConst** (4470)/**PutBoundNil** (4480) — alloc fresh var bound to nil/const, store reader VarRef.
- **PutStructure** (3058–3107) — BODY: alloc writer, push parent if nested, `clauseVars[-1]=writerAddr`, `clauseVars[-2]=argSlot`, build `StructTerm(functor,[ConstTerm(null)*arity])`, mode=write, S=0. Pre-commit (guard-arg) branch: no heap alloc, set `guardArgSlot`.
- **PutList** (4490–4513) — like PutStructure for `'[|]'/2` on env writer (`clauseVars[-1]`).
- **SetConstant** (3109–3205) — write const into struct at S; on completion `bindWriterStruct(clauseVars[-1])`+enqueue+parentStack unwind (stores to argSlot).
- **opv2.SetVariable** (2522–2664) — write clause var into BODY struct at S (mode-corrected); on completion same bind+unwind. (L2630/2641 use raw `+1` reader arithmetic — legacy.)
- **BodySetConst** (2928–2938) — `bindWriterConst(writerId,value)`+enqueue. **BodySetStructConstArgs** (2939–2953) — `bindWriterStruct`+enqueue. **BodySetConstArg** (2954–2966) — bind env-slot writer to const.
- **Spawn** (3220–3306) — if label found: build `CallEnv` from argSlots, `newGoalId=rt.nextGoalId++`, register env+program, `gq.enqueue(GoalRef(newGoalId,entryPc))`, clear argSlots. If not found: **body kernel** (`rt.bodyKernels.lookup`), execute inline; abort→`terminated`.
- **Requeue** (3308–3369) — tail call: `env.update(argSlots)`, clear argSlots/spawnedGoals, reset σ̂w/U/clauseVars/inBody/mode/S/currentStructure, **`cx.kappa=entryPc`**, `pc=entryPc`.
- **Distribute** (3375–3424) — static module RPC: resolve `imports[importIndex]`, send `StructTerm(functor,args)` on `rt.glpChannels[target.name]`, enqueue returns.
- **Transmit** (3426–3479) — dynamic RPC: resolve module name from `clauseVars[moduleVarIndex]`.
- **Allocate** (4516–4539) — push `EnvironmentFrame(parent=E,CP,size=op.slots)`; inBody only. **Deallocate** (4541–4561) — restore CP/E (no jump).

---

## 5. Writer-MGU discipline

Writers bind ONLY into `sigmaHat` (tentative, HEAD/GUARD) or via `heap.bindWriter*` (BODY). Readers never bind. Enforced at three places:
- **WxW at Commit** (L2816–2823): before applying σ̂w, a σ̂w value that is a `VarRef` to an unbound writer → `throw StateError('WxW …')`. Heap application = `CommitOps.applySigmaHatFCP(heap, sigmaHat)` (L2827, `commit.dart`).
- **Writer×Writer READ-mode** in `UnifyVariable` writer mode (L2144–2154): both unbound writers → soft-fail.
- **Reader×Reader FAIL** in `UnifyVariable` reader mode (L2093) and `opv2.GetVariable` reader mode (L2338): two readers can't be equated by a writers-only substitution → soft-fail. (Dual of the writer-MGU rule.)
- Reader-addr derivations go through `heap.pairedReaderAddr`/`tryWriterForReader` (spec v3.2, replacing raw `+1`/`-1`) — L1059/1869/2111 etc. — except legacy BODY spots L2630/2641/2772/3000/3006/3016.

σ̂w binding sites (non-exhaustive): L1167/1180 (HeadConstant), L974/1282/1310/1425/1450 (tentative structs), L1767 (UnifyConstant), L2106/2116/2130/2153/2159 (UnifyVariable), L2227/2254/2269/2281 (opv2.GetVariable), L2407/2428/2435/2444/2457/2488/2493 (opv2.GetValue), L4245/4289/4343/4423 (HeadNil/HeadList).

---

## 6. Suspension & reactivation

**Suspend on unbound reader — two mechanisms:**
1. **HEAD two-phase:** an unbound reader where a value is required → `Si.add` and *continue* (`pc++`), not fail (HeadConstant L1187, HeadStructure L1326/1466, UnifyConstant L1790, HeadNil L4267/4355). At **Commit** (L2704–2722) each Si reader is re-checked vs σ̂w (`tryWriterForReader`); still-unresolved → move to U + soft-fail. Lets an *indeterminate* (not contradicted) HEAD defer to suspension rather than premature failure.
2. **GUARD/direct:** `_suspendAndFail`(L423)/`_suspendAndFailMulti`(L432) — add reader(s) to U, `_softFailToNextClause` (merges Si→U), jump next.

`_softFailToNextClause` (L381) always `U.addAll(Si)` before `clearClause()`. **U durable; Si per-clause.**

**Final commit** at NoMoreClauses (L2880)/SuspendEnd (L2912): `rt.suspendGoalFCP(goalId, kappa, readerVarIds: U)` registers the goal (resumable at `kappa`) against every reader addr in U; then `U.clear()`, `return suspended`.

**Reactivation is writer-driven, external to this file:** any writer bind — at Commit (`applySigmaHatFCP` L2827) or BODY (`bindWriterConst`/`bindWriterStruct` L1684/1717/1993/2024/2574/2618/3124/3158/2931/2946/2959, wait timers L3205/5205/5252) — returns woken `GoalRef`s of goals suspended on the paired reader, `gq.enqueue`d; scheduler re-invokes `runWithStatus` at `pc=cx.kappa`. `wait`/`wait_until` (L5164/5218) are timer-based self-suspension: alloc reader/writer pair, `rt.setWaitReader`, add reader to U, fail; a `Timer` binds the writer later.

---

## 7. Port-relevant subtleties to preserve

- `clauseVars` negative-key sentinels (`-1` target writer, `-2` target argSlot) are load-bearing across PutStructure/Set*/UnifyVariable completion + parentStack unwind (L2000, L2593, L3132) — not real clause variables.
- Structure completion is detected purely by `cx.S >= struct.args.length` after each `S++`; nested-completion `while` loops recursively bind ancestors.
- Deref helpers: `_dereferenceWithTracking` (L4602, reader tracking + register-index-vs-heap-addr disambiguation L4613), `_collectUnboundReaders` (L4711, cycle-safe nested), `_termsEqual` (L5292, cycle-safe via visited pairs), `_compareTerms`/`_orderRank` (L5400/5384, standard order Number<String<compound — must stay byte-identical to the C# port), `_evaluateArithmetic`/`evaluateNumeric` (L4770/4815).
- `_evaluateGuard` (L4806) is a big `switch` on predicate name: comparisons `< > =< >= =:= =\=`, term-order `@< @> @=< @>=`, type tests `ground known integer atom/string constant number list/is_list compound/tuple module is_mutual_ref unknown`, `=?=`, `wait`/`wait_until`. Unknown predicate → `[WARN]` + failure (L5284).
- Runtime-defined-guard interpreter (L461–764): `_evalDefinedGuardCall`/`_evalDefinedGuardConjunct`/`_dgMatchTerm`/`_dgTestEqual`/`_dgResolve`/`_dgDeref`/`_dgCollectUnbound` — clause-spec three-valued eval (any-clause-success⇒success; else any-suspend⇒suspend on union; else fail; fail dominates suspend within a clause). Depth cap 5000 (L459) + StackOverflow catch (L3592).
