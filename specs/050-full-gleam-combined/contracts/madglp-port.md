# Contract: madGLP→Gleam Phase-A port + distinguished-mailbox-channel I/O seam

**Status:** design synthesized by 3rtask run `20260714T072542Z-a84b` (3 blind Builders on disjoint slices, codex Critic; 83 CONFIRM / 2 REFUTE / 5 ESCALATE; budget_stop cycle 1). Strategic decision: memory `050-madglp-gleam-port-A-then-B`. This is the normative design for the T050 decomposition (T050.A0–A4 + T050.B) in `tasks.md`.

**Fidelity anchor (NON-NEGOTIABLE):** Ehud Shapiro's Dart implementation + `docs/ma/madGLP-spec.md` v5.3 (CGLP §7) + corpus `10-cglp-madglp-section-shapiro`/`11-madglp-correctness-theorem-n-agent`; GHC machine spec fallback where the GLP spec has gaps. GLP-visible deviations STOP and escalate (Language Authority §1.14).

## Why (the blocker T050 surfaced)

Link primitives are effectful kernels on the madGLP layer. In Gleam that layer is a **stub** (`multiagent.gleam`), and the engine is **immutable/pure**: body kernels are `KernelOutcome{KSuccess(heap,woken,output)|KAbort(detail)}` (kernels.gleam:59-62); `dispatch(heap,name,arity,args)` has no engine-state access (kernels.gleam:101-106); `_send`/`_now` are deliberately unregistered (kernels.gleam:24-27); the heap has **no `onBind`** — reactivation is via woken `GoalRef`s (heap.gleam:313-332, scheduler.gleam:363-378). madGLP (Phase A) is the prerequisite; a BEAM/AtomVM process refactor (Phase B) follows without changing semantics.

## Frozen madGLP semantics to reproduce EXACTLY

- **Globalize `T_p↑` (§5.1):** writer Y → entry `(Y,q)` at next index i, becomes `_w(p,i)`, NO spawn; reader Y? → spawn `global_send(Y?,_r(p,i),q)`, becomes `_r(p,i)`, NO entry.
- **Localize `T_q↓` (§5.2):** `_w(p,i)` → fresh pair, writer, spawn `global_send(Y_q?,_w(p,i),p)`, NO entry; `_r(p,i)` → fresh pair, reader, add entry `(Z_q,p,i)`, NO spawn.
- **W_p lifecycle (§9.3/§13):** entry created once (agent is RECEIVER), removed once (by Receive), never modified between; index-0 serializer entry permanent (updated, never removed).
- **Index (§3.2/§13; global_writers_table.dart:75):** single per-agent counter, shared globalize/localize, start 1 (0=serializer), never reused.
- **`_send` (§11.5; body_kernels.dart:658-745):** globalize T for Q, add message; serializer index-0 wraps `[T↑|_w(q,0)]`, normal `G:=T↑`; aborts unless G is `_w/2`/`_r/2`.
- **Transactions (§8):** Reduce (unary; writer-assign → known reader → fires `global_send`), Send (drain M_p → channel), Receive (3 cases incl serializer; localize + W_p lookup + bind + reactivate).
- **Ordering (§13):** FIFO between any agent pair (per-link/per-channel; NO cross-channel order — matches `link_options.gleam:14`).

## Gleam integration (confirmed)

- **Hook:** BODY Spawn label-miss → `kernels.dispatch` at **runner.gleam:1860-1873**.
- **W_p immutable model:** `suspension.SuspensionTable` (suspension.gleam:41-89). M_p/index thread like `output` (KSuccess.output→ctx.output→Reduced.output→Engine.output, scheduler.gleam:69-71).
- **Reactivation:** localize/Receive binds a local writer via `heap.bind_writer` → woken `GoalRef`s → `scheduler.reactivate` (dedup per goal_id+generation, types.gleam:82-97) — integrates for free.
- **Parser reuse:** `SpawnGoal(inner,agent_id)` (`Goal@AgentId`), `RemoteGoal(target,inner)` (`Var#Goal`/`Module#Goal`) already emitted (parser.gleam:984-1003).
- **Two gaps to build:** `StepReduced` has no outbound-msg field (scheduler.gleam:107-112); `KAbort→RunnerError` is fatal (runner.gleam:1886) — `_send` non-fatal failures need a path.

## Interfaces (A2 seam RATIFIED 2026-07-14)

```gleam
// A0
pub type GlobalName { WriterName(agent: Term, index: Int)   // _w(p,i)
                      ReaderName(agent: Term, index: Int) } // _r(p,i)   — polarity is load-bearing
pub opaque type WritersTable   // immutable; entries (X,q) | (X,q,i); permanent index-0 serializer; counter from 1
pub type Message { Message(name: GlobalName, term: Term, dest: Term) }

// A2 — parallel effectful path (NOT widening KernelOutcome, which touches ~30 dispatch arms + runner + scheduler)
pub type MadOutcome { MadEffect(heap: Heap, woken: List(GoalRef), output: List(String),
                                w_p: WritersTable, m_p: List(Message), index: Int, spawned: List(SpawnReq)) }
// RunnerContext extended to carry W_p/M_p/index (dispatch runs deep inside runner.reduce — runner.gleam:170-216)

// A3
pub type MadEngine { MadEngine(engine: Engine, w_p: WritersTable, m_p: List(Message), index: Int) }
pub fn step(me: MadEngine, budget: Int) -> #(MadEngine, StepOutcome, List(Message))  // 3rd = Send-drained M_p
```

## Distinguished-channel registry (Gabi-approved: logical identity first-class, multiplexing either)

- Identity = **separate lightweight `(role, channel-tag/index)` namespace ABOVE transports**, NOT `LinkId` reuse (LinkId=one physical bilateral link; nonces are carrier facts — loopback.gleam:227, tcp.gleam:102). Preserve `NonceInt`≠`NonceStr` distinction if any tag maps to a term.
- Roles ⊇ {input, output, fault/monitor}; **multiple channels per role**; multiplex tagged-on-one-carrier (needs a demux reader — loopback recv parks ONE reader, loopback.gleam:62) OR distinct-carriers. Seam is term-agnostic (transport.gleam:12-13) so either satisfies the contract.
- **stderr-equivalent = the fault/monitor stream** (`link_monitor/2`, independently observable — link-primitives.md §2.7); fault-NEVER-fail (FR-043/044) must survive multiplexing.
- **no-OTP** (gleam.toml:7-10): Phase B uses process.spawn/new_subject/receive only (loopback.gleam:77-266); NO gleam_otp supervisors/gen_server.

## Escalations — RATIFIED by Gabi 2026-07-14

1. **E5 seam shape → PARALLEL `MadOutcome` + extend `RunnerContext`.** Widening `KernelOutcome` is rejected (engine-core change across ~30 dispatch arms + runner + scheduler). This is the ratified A2 engine-surface change (§1.14).
2. **Dart duplicate-delivery absorption → spec-v5.3-PURE Phase A.** Do NOT replicate Dart's beyond-spec reliability in Phase A; defer to the link reliability sublayer (T052).
3. **`bindAny` vs pure local-pair → PURE LOCAL-PAIR model (spec §11.3).** All variables are local pairs; do not mirror Dart's `bindAny` seam.

Channel questions (identity namespace / stderr / multiplexing) were resolved by evidence above.
