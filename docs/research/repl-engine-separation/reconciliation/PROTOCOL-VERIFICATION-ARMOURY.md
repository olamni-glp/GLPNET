# Protocol / Concurrency Verification Armoury — Specification

**Feature**: 027 refinement-verification-framework · **Story**: US5 (P1) · **Task**: T021
**Status**: AUTHORITATIVE for the *front↔back wire-protocol* verification tool selection.
**Requirements**: FR-076, FR-077, FR-078, FR-079; SC-012. Consistent with
[`REFINEMENT-METHOD.md`](REFINEMENT-METHOD.md) §4, formal-tooling **slot 6**.
**Decisions**: ratifies [`DECISIONS-LOG.md`](DECISIONS-LOG.md) **R14** (SPIN required default) +
**R15** (the armoury). Deferral it creates: **DEF-A3** (full protocol model at #5/#6) — see
[`DEFERRALS.md`](DEFERRALS.md).
**Empirical backing**: SPIN-as-default is validated by a **real-SPIN** spike on a minimal front↔back
handshake. The minimal model ([`../spikes/spin/front_back.pml`](../spikes/spin/front_back.pml)) is
authored here at #1a; the spike is **run and recorded at T024 (block 06)** in
[`../spikes/spin/RESULT.md`](../spikes/spin/RESULT.md) — empirical, not desk research (R13/R14,
FR-070/080).

---

## 1. Purpose

The engine-separation epic splits one REPL into a **front-end** (thin client) and a **back-end**
(engine) talking over a wire protocol. Concurrency/protocol bugs — deadlock, lost or reordered
messages, stuck progress, unspecified receptions — are exactly what model checking catches and unit
tests miss. This document fixes:

1. the **REQUIRED pragmatic-tier default** tool (Promela/SPIN) and the rule that it is **mandatory**
   in the metric tables of the wire-protocol seeds **#2, #5, #6** (§3, FR-076/077); and
2. the **armoury** of alternative checkers a seed escalates to when its protocol type calls for it,
   each documented with its modeling paradigm, verification engine, primary strength, and best-for
   use case (§2, FR-078); plus the **seed-type selection guidance** that maps protocol shape → tool
   (§4, FR-079).

This is a *specification of the tool slot*, not the verification work itself: the full Promela model
of the complete wire protocol / result envelope is **out of scope here** and deferred to #5/#6
(DEF-A3, FR-081). The #1a deliverable is the armoury doc + a minimal-handshake real-SPIN spike.

---

## 2. The armoury — tool matrix (FR-078, SC-012)

SPIN/Promela is the ratified default (R14); the remaining six are the R15 alternatives a seed
selects by protocol type. Each row: **modeling paradigm · verification engine · primary strength ·
best-for use case**.

| Tool | Modeling paradigm | Verification engine | Primary strength | Best-for use case |
|---|---|---|---|---|
| **SPIN / Promela** *(default)* | Asynchronous communicating processes (`proctype`) + channels; properties in LTL or `assert`/`never` claims | Explicit-state on-the-fly reachability (`spin -a` → `pan.c` → compiled C verifier), partial-order reduction, bitstate hashing | Fast, mature explicit-state checking of message-passing protocols with named safety + liveness | **Default for the front↔back wire protocol**: network/communication protocols & algorithms; deadlock-freedom, unspecified-reception detection, request→response progress |
| **TLA+ / PlusCal** | High-level state machine over math (sets/functions/sequences); `TLA+` directly or `PlusCal` compiled to TLA+ | TLC explicit-state model checker (also Apalache symbolic, TLAPS proofs) | Reasoning about *system-level* invariants & refinement across many components and message interleavings | High-level **distributed systems & consensus** — Raft/Paxos-style multi-client coordination, leader election, log replication (e.g. **#13** multi-client) |
| **UPPAAL** | Networks of **timed automata** with real-valued clocks, guards, invariants | Symbolic zone-based reachability over clock regions; CTL-subset queries | First-class **dense real-time**: clock constraints, timeouts, deadlines, urgency | **Real-time / timed protocols** — timeouts, retransmission timers, escrow/expiry windows, clock-bound handshakes (e.g. **#7/#8** timer/escrow/liveness logic) |
| **NuSMV / nuXMV** | Synchronous finite-state machines (SMV modules); fairness constraints | **Symbolic** BDD model checking + SAT/IC3 bounded & unbounded (nuXMV adds infinite-state via SMT) | Crushing **large state spaces** symbolically where explicit-state would blow up | **Symbolic / large state spaces**, synchronous state-machine protocols, CTL+LTL over wide state vectors |
| **mCRL2** | **Process algebra** (ACP-style) with abstract data types; modal-µ-calculus properties | Linearisation → labelled transition system → bisimulation reduction + µ-calculus model checking | Rich modeling of **complex concurrent communication** with data, plus behavioural-equivalence checking | **Process-algebra / data-rich asynchronous** interaction where behavioural equivalence and abstract data both matter |
| **FDR4** | **CSP** (Communicating Sequential Processes) — events, channels, parallel composition | **Refinement checking** (traces / failures / failures-divergences) between CSP processes | Decisive **deadlock & livelock** analysis and *spec-refines-impl* checks in the CSP idiom | **CSP refinement / deadlock-livelock** freedom of communicating processes; checking an implementation refines an abstract protocol spec |
| **CADP** | LOTOS / LNT process descriptions (value-passing process algebra) | Toolbox: explicit + on-the-fly + compositional verification, equivalence checking, distributed state-space generation | Scaling to **asynchronous, large-scale distributed** protocols via compositional & distributed exploration | **Asynchronous, large-scale distributed** protocols where compositional/distributed state-space construction is needed |

> All seven satisfy the project no-API rule trivially: each is a **deterministic local checker**
> (model in → verdict/counterexample out). No LM sits on the verification path — the model checker is
> the oracle, exactly as the Lean kernel and the MLIR round-trip oracle are (FR-073).

---

## 3. The rule — SPIN protocol validation is MANDATORY for #2/#5/#6 (FR-076/077)

**R14, ratified.** Promela/SPIN is the **REQUIRED pragmatic-tier default** for validating the
front-end↔back-end wire protocol. For every wire-protocol seed — **#2** (result-envelope-and-deep-
resolve), **#5** (result-codec-and-framecodec-ride), **#6** (repl-engine-process-split-mvp) — a
Promela/SPIN protocol-validation row is **mandatory** in the seed's metric-combination table
(`name | kind (pragmatic|formal) | tool | threshold`, see [`REFINEMENT-METHOD.md`](REFINEMENT-METHOD.md) §2),
of kind **pragmatic**, tool **SPIN/Promela**.

The mandatory check covers, at minimum:

- **deadlock-freedom** — no invalid end state (SPIN reports no reachable deadlock);
- **absence of unspecified receptions** — no process receives a message it has no transition for;
- a **progress / liveness** property — stated as an LTL `never`/`ltl` claim and/or `progress` labels.

**FR-077 — the properties MUST be named.** A seed's metric table does not merely cite "SPIN"; it
**names the specific safety and liveness properties** its model check covers. Canonical examples for
the front↔back protocol:

- *Safety*: `no deadlock`; `no unspecified reception`; `no message reordering observable to the
  client`.
- *Liveness/progress*: `every request eventually receives a response or a typed error`.

A wire-touching seed whose table lacks a SPIN row, or whose SPIN row names no safety+liveness
property, is a **spec defect** surfaced at `/buildkit-analyze` and fixed by amending the table — not
by relaxing the mandate (spec Edge Cases).

The threshold-shape for this slot (a seed instantiates the concrete threshold at its interactive
spec step): **deadlock-freedom + no unspecified receptions + the named progress/liveness property
all hold under real SPIN (`spin -a` → `pan`), or a counterexample trace is surfaced**
([`REFINEMENT-METHOD.md`](REFINEMENT-METHOD.md) §4, slot 6).

---

## 4. Seed-type selection guidance (FR-079)

At a wire-protocol seed's interactive spec step (FR-060), the agent **proposes the armoury tool fit
to that seed's protocol type, records the choice and its rationale**, and the owner confirms or
amends. SPIN is the default; escalate only when the protocol shape genuinely calls for an
alternative's strength:

| Protocol shape of the seed | Selected tool | Why |
|---|---|---|
| Front↔back request/response, message-passing protocols & algorithms *(the common case)* | **SPIN/Promela** *(default)* | Ratified default (R14); explicit-state checking of exactly this message-passing shape |
| Distributed **consensus** / multi-client coordination (leader election, log replication, agreement) — e.g. **#13** | **TLA+/PlusCal** | System-level invariants & refinement over many interleavings; the idiom for consensus |
| **Timed** logic — timeouts, retransmission timers, escrow/expiry windows, clock constraints — e.g. **#7/#8** | **UPPAAL** | First-class dense real-time clocks; the others cannot reason about real time |
| **Large / symbolic** synchronous state spaces where explicit-state blows up | **NuSMV/nuXMV** | Symbolic BDD/SAT/IC3 scales past explicit enumeration |
| Rich **process-algebra** modeling with abstract data; behavioural-equivalence needs | **mCRL2** | Process algebra + ADTs + bisimulation reduction |
| **CSP refinement**; decisive deadlock/livelock in communicating processes; impl-refines-spec | **FDR4** | CSP failures-divergences refinement checking |
| **Asynchronous, large-scale distributed** protocols needing compositional/distributed exploration | **CADP** | Compositional + distributed state-space construction at scale |

**Default-first discipline.** SPIN is chosen unless a concrete trigger above makes an alternative
genuinely better; the seed records *which* tool and *why*. This mirrors the proof-assistant policy
(§3 of [`REFINEMENT-METHOD.md`](REFINEMENT-METHOD.md)): name a primary best-fit, keep the
alternative only where a real trigger makes it superior — do not stock tools speculatively into a
seed's table.

---

## 5. Scope boundary (DEF-A3, FR-081)

In scope at **#1a**: this armoury document **+** a minimal-handshake **real-SPIN** validation spike
([`../spikes/spin/RESULT.md`](../spikes/spin/RESULT.md), T024) demonstrating deadlock-freedom +
progress on a two-proctype request/response model — the smallest model that can actually exhibit a
deadlock or lost-progress counterexample (research §3, HANDSHAKE-1).

**Out of scope here**, deferred to the wire-protocol seeds: the **complete** Promela/SPIN model of
the full wire protocol / result envelope is owned by **#5/#6** (DEF-A3). The #1a spike covers a
**minimal handshake only**; each wire/protocol seed then models its full protocol and selects from
this armoury at its interactive spec step.

---

## 6. Traceability

| Requirement | Where satisfied |
|---|---|
| FR-076 (SPIN required default; mandatory #2/#5/#6) | §3 |
| FR-077 (named safety + liveness properties per seed) | §3 |
| FR-078 (≥7-tool matrix: paradigm/engine/strength/best-for) | §2 |
| FR-079 (seed-type selection at the interactive spec step) | §4 |
| SC-012 (≥7 tools documented + selection guidance) | §2 + §4 |
| R14 / R15 ([`DECISIONS-LOG.md`](DECISIONS-LOG.md)) | §1–§4 |
| DEF-A3 ([`DEFERRALS.md`](DEFERRALS.md)) — full model at #5/#6 | §5 |
| Empirical backing (real-SPIN spike, FR-080) | §5 → [`../spikes/spin/RESULT.md`](../spikes/spin/RESULT.md) |
