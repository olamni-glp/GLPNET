# Curator Report — madGLP→Gleam Phase-A port + distinguished-mailbox-channel I/O seam

**Run:** 20260714T072542Z-a84b · task_type=plan · 3 blind Builders on disjoint slices · codex Critic (full cross-provider independence) · **budget_stop at cycle 1** (574k ≥ 500k; the disjoint survey was complete and adversarially adjudicated — a 2nd blind re-survey of disjoint slices adds little).

**Adjudication:** 90 cited claims → **83 CONFIRM, 2 REFUTE, 5 ESCALATE**. Slices disjoint by construction ⇒ singletons cross-verified by the non-blind Critic (not averaged away).

---

## 1. Resolved design questions (evidence-backed; Gabi's "logical identity first-class, multiplexing either" applied)

- **Channel identity (Q10a):** a **separate lightweight `(role, channel-tag/index)` namespace ABOVE the transports**, NOT a reuse of `LinkId`. `LinkId(scheme,endpoint,nonce)` identifies one physical bilateral link (link_id.gleam:24-26; link-primitives.md §5); nonces are **carrier facts** (loopback hub-counter — loopback.gleam:227; TCP port — tcp.gleam:102). Deriving logical identity from those would leak physical multiplexing into logical identity (builder-3 RISK).
- **stderr-equivalent (Q10b):** the **fault/monitor stream** (`link_monitor/2`, independently observable — link-primitives.md §2.7; Endpoint.faults:Subject + LinkFaultKind{Closed,Transient,Permanent} — link_fault.gleam:14-33). No distinct diagnostic channel.
- **Physical multiplexing (Q10c):** EITHER tagged-on-one-carrier OR distinct-carriers satisfies the contract because the seam is **term-agnostic/opaque-frame** (transport.gleam:12-13); tagged-on-one-carrier is supported by the one-self-delimiting-frame-in-submission-order contract (FR-018; endpoint.gleam:24-33). Caveat: loopback recv parks exactly ONE reader (loopback.gleam:62,136) — tagged multiplexing needs a demux reader.
- **no-OTP (confirmed):** gleam.toml:7-10 (gleam_otp intentionally absent — proc_lib outside AtomVM). Phase-B primitives already demonstrated in-subset (loopback process.spawn/new_subject/receive — loopback.gleam:77-266). **RISK:** Phase B must NOT reach for gleam_otp supervisors/gen_server.

## 2. Frozen madGLP semantics the port must reproduce EXACTLY (builder-1, fidelity)

- **Globalize (spec §5.1):** writer Y → entry `(Y,q)` at next index i, becomes `_w(p,i)`, NO spawn; reader Y? → spawn `global_send(Y?,_r(p,i),q)`, becomes `_r(p,i)`, NO entry.
- **Localize (spec §5.2):** `_w(p,i)` → fresh pair, writer, spawn `global_send(Y_q?,_w(p,i),p)`, NO entry; `_r(p,i)` → fresh pair, reader, add entry `(Z_q,p,i)`, NO spawn.
- **W_p lifecycle (spec §9.3/§13):** entry created once (globalize-writer OR localize-reader = agent is RECEIVER), removed once (by Receive), never modified between; index-0 serializer entry is permanent (updated, never removed).
- **Index (spec §3.2/§13; global_writers_table.dart:75):** SINGLE per-agent counter, shared globalize/localize, start at 1 (0=serializer), never reused.
- **`_send` builtin (spec §11.5; body_kernels.dart:658-745):** globalize T for Q, add message; serializer index-0 wraps `[T↑|_w(q,0)]`, normal sends `G:=T↑` direct; ABORTS unless G is `_w/2`/`_r/2`.
- **Transactions (spec §8):** Reduce (unary; assign writer → known reader → fires global_send), Send (drain M_p → channel), Receive (3 cases incl serializer; localize + W_p lookup + bind + reactivate).
- **Message ordering (spec §13):** FIFO between any agent pair (per-link/per-channel only; NO cross-channel order — consistent with link_options "no HOL blocking across independent links", link_options.gleam:14).

## 3. Gleam engine seam (builder-2, integration) — CONFIRMED

- **Kernel model:** `KernelOutcome{KSuccess(heap,woken,output)|KAbort(detail)}` (kernels.gleam:59-62); pure, heap-only; `dispatch(heap,name,arity,args)` has NO W_p/M_p/index access (kernels.gleam:101-106); `_send`/`_now` deliberately unregistered (kernels.gleam:24-27).
- **Hook point:** BODY Spawn label-miss → `kernels.dispatch` at **runner.gleam:1860-1873**.
- **Heap immutability replaces onBind:** armed suspensions on WriterCell + `heap.activate` → GoalRefs on `bind_writer` (heap.gleam:313-332); woken GoalRefs re-enqueued by `scheduler.reactivate` (scheduler.gleam:363-378); dedup per goal_id+generation (types.gleam:82-97).
- **W_p immutable model READY:** `suspension.SuspensionTable` = opaque `Dict` with immutable ops (suspension.gleam:41-89) — the template for W_p. M_p/index thread like `output` does (KSuccess.output→ctx.output→Reduced.output→Engine.output, scheduler.gleam:69-71).
- **Parser already emits madGLP shapes:** `SpawnGoal(inner,agent_id)` from `Goal@AgentId`, `RemoteGoal(target,inner)` from `Var#Goal`/`Module#Goal` (parser.gleam:984-1003) — reuse for A0/A1.
- **Two completeness gaps to build:** (i) `StepReduced` has no outbound-message field (scheduler.gleam:107-112) — M_p emissions need a path up; (ii) `KAbort→RunnerError` is fatal (runner.gleam:1886) — non-fatal `_send` failure modes need a path.

## 4. RECOMMENDED seam shape (ESCALATE E5 — Gabi ratifies)

**Parallel effectful path, NOT widening `KernelOutcome`** (builder-2, kernels.gleam:285-304, runner.gleam:75-80): widening touches ~30 dispatch arms + runner Spawn handler + `ReduceOutcome.Reduced` + `scheduler.step` — an engine-core change conflicting with the no-rewrite feasibility lens. Proposed:
```
pub type MadOutcome { MadEffect(heap: Heap, woken: List(GoalRef), output: List(String),
                                w_p: WritersTable, m_p: List(Message), index: Int, spawned: List(SpawnReq)) }
pub type MadEngine  { MadEngine(engine: Engine, w_p: WritersTable, m_p: List(Message), index: Int) }
```
**Refinement (builder-2, runner.gleam:170-216, scheduler.gleam:260-266):** effectful state must be visible AT dispatch time inside `runner.reduce` (deep in `scheduler.step`), so `RunnerContext` (or the `reduce` signature) must be extended to carry W_p/M_p/index — `MadEngine` cannot wrap `Engine` as an opaque black box for `_send`. This is the one genuine engine-surface touch and the reason E5 is Gabi's to ratify.

## 5. Corrections adopted (2 REFUTEs)

- **Global name is a sum type carrying polarity:** `GlobalName { WriterName(agent, index) | ReaderName(agent, index) }` (i.e. `_w(p,i)`/`_r(p,i)`), NOT a flat `GlobalVarId(agent,local)` — madGLP routing depends on the `_w` vs `_r` kind.
- **Globalize/Localize are host-level term traversals**, not GLP-clause-level (do not rely on user-level GLP predicates).

## 6. Staged task schedule (injectable into tasks.md / marathon under T050)

| Task | Scope | Deps | Oracle | Spec § | Acceptance / checkpoint |
|---|---|---|---|---|---|
| **A0** | `glp/mad/{global_name,global_writers_table,message}.gleam` — `GlobalName{WriterName\|ReaderName}`, immutable W_p (SuspensionTable-style, index-0 serializer, single counter), M_p `Message(GlobalName,Term,agent)` | T045-T049 done | global_writers_table.dart; suspension.gleam:41-89 | §2,§3,§6 | gleam test green; W_p lifecycle unit tests from spec §5.4/§10; checkpoint |
| **A1** | `glp/mad/{globalize,localize}.gleam` — host-level term traversals over heap+W_p+counter | A0 | mad_context.dart; spec §5,§10 | §5 | spec §10 worked-scenario tests (byte/term flow); checkpoint |
| **A2** | effectful-dispatch seam (parallel `MadOutcome`; extend `RunnerContext` w/ W_p/M_p/index) + `_send` kernel | A1; **E5 ratified** | body_kernels.dart:658-745; runner.gleam:1860-1873,170-216 | §11.5 | `_send` serializer + normal cases; non-fatal-fail path; checkpoint |
| **A3** | `glp/mad/mad_engine.gleam` (wraps Engine) + Send (drain M_p; StepReduced msg field) + Receive (3 cases) + boot serializer entry | A2 | mad_context.dart handleMadAssignment; agent_runtime.dart | §7,§8 | Receive 3-case tests; boot c₀; checkpoint |
| **A4** | prelude `global_send/3`,`send_to_net/1`,`send_to_ui/1` load + multi-agent parity tests | A3 | self.glp/mad prelude; spec §12 | §4,§12 | spec §10 client-monitor + friend-intro parity vs Dart; checkpoint |
| **Phase B** | process-per-agent BEAM/AtomVM (each agent = process owning its MadEngine; Send/Receive = inter-process msgs); **no-OTP** (process.spawn/new_subject/receive only) | A4; **channel-registry design** | loopback.gleam:77-266 | — | many-small-backends footprint; semantics unchanged; per-channel FIFO preserved |

**Distinguished-channel registry** (feeds A3/Phase B): `(role, channel-tag)` namespace above transports; role ∈ {input, output, fault/monitor}; multiple per role; multiplex tagged-on-one-carrier (demux reader) OR distinct-carriers; identity independent of LinkId nonce.

## 7. ESCALATIONS — Gabi's to resolve before seam-dependent contracts finalize

1. **E5 / seam shape** — parallel `MadOutcome` + extend `RunnerContext` (RECOMMEND) vs widen `KernelOutcome` (engine-core). *Recommend: parallel.*
2. **Dart duplicate-delivery absorption** (beyond-spec reliability) — replicate in Phase A, or stay spec-v5.3-pure and defer to the link reliability sublayer (T052)? *Recommend: spec-pure Phase A; reliability extras later.*
3. **`bindAny` seam vs pure local-pair model** — mirror Dart's `bindAny`, or keep the pure local-pair model (spec §11.3 "all variables are local pairs")? *Recommend: pure local-pair per spec §11.3.*

Channel questions (a/b/c) are **resolved by evidence** (§1) — not open.

---
## Run footer

- run: `20260714T072542Z-a84b`  verdict: **budget_stop**  cycles: 1
- critic: codex
- terminal review: skipped — terminal_review disabled by policy (--terminal-review off)
