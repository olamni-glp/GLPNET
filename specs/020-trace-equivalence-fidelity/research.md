# Phase 0 Research — Trace-Equivalence-Driven Codegen Fidelity (020)

All decisions below were settled with Gabi in the spec clarification session (2026-05-27, 10 Q&A) — this document records the *technical* resolution of each into an implementable design. Zero open NEEDS CLARIFICATION remain.

---

## R1 — Normalized trace model (FR-002)

**Decision**: A normalized trace is an ordered sequence of semantic events, each carrying a kind, payload, and a set of causal (data-dependence) predecessor edges. Heap addresses are relabeled to **logical variable identities** at capture time, by first-occurrence canonicalization (the i-th distinct heap address seen becomes logical var `v_i`), consistently within a run.

**Event kinds** (the only ones compared): `unify(outcome=success|suspend|fail, vars…)`, `suspend(reader_var, goal_id)`, `reactivate(writer_var, goal_id)`, `writer_bind(writer_var, value_shape)`, `bytecode_op(opcode, logical_pc)`. The bytecode-op subsequence is the **spine** (FR-003).

**Rationale**: These are precisely the observable events of the GLP three-phase model (HEAD tentative unification → GUARD tests → BODY mutations) plus suspension/reactivation and writer-MGU — the semantics the converted runtime must reproduce. Addresses are an implementation accident; logical identities are the invariant.

**Alternatives considered**: (a) raw `:trace` text diff — rejected: address noise + interleaving produce false divergences. (b) Final-heap snapshot only — rejected: that is outcome-equivalence, loses the execution-trace dimension the feature exists for.

---

## R2 — Causal / partial-order equivalence relation (FR-003)

**Decision**: Two normalized traces are equivalent iff (1) outcomes match, (2) the **dependent-event projection** is identical — i.e. for every pair of causally-ordered events the same relative order holds and the same events are present, and (3) the bytecode-op spine matches (same opcodes at same logical PCs). Causally-**independent** events (no data-dependence edge between them) may appear in any relative order without divergence. Heap-address values are never compared (already abstracted in R1).

**Strict-tier specialization**: deterministic subsystems (`heap`, `bytecode`, `compiler`, `runtime-core`) have a single causal linearization, so the relation degenerates to **total-order equality** — the stricter, cheaper check (FR-008). Only `multiagent` uses the full partial-order machinery (FR-009).

**First-divergence**: the comparison returns the first event (in golden causal order) where candidate has no matching event admissible under the relation — kind, causal position, expected vs actual (FR-003, the trace-divergence record).

**Rationale**: Matches decision 5 exactly. The partial-order relaxation is only paid where genuine nondeterminism exists; everywhere else equality is both correct and cheaper (SC-007/SC-002 confidence).

**Alternatives**: full partial-order everywhere — rejected: needless cost + weaker check on subsystems that are in fact deterministic.

---

## R3 — Bytecode-emission diff as early checkpoint (FR-004, SC-007)

**Decision**: Before any runtime executes, compare the bytecode the **C# compiler** emits for a GLP source against the Dart-emitted bytecode (same opcodes, same logical PCs). This is available as soon as the `compiler` subsystem is converted, prior to a runnable end-to-end C# runtime. It is the cheapest gate and the spine of the full trace comparison.

**Rationale**: Decision in spec US1/FR-004. Lets the strict tier be verified incrementally during bootstrapping (edge case: no runnable runtime yet). `dump_bytecode` disassembly (sibling repo tooling) is the reference format.

**Alternatives**: skip until full runtime — rejected: loses the bootstrap-window signal and SC-007.

---

## R4 — Tiered fidelity metric (FR-013, decision 4 + boundary clarification)

**Decision**: One pure function `fidelity.py:score(file_state) -> float`:
- non-compiling ⇒ `0.0` (hard floor).
- compiles, no equivalence evidence (unreviewed, no back-test/trace) ⇒ flat `0.25`.
- back-tested + trace-captured, not fully trace-equivalent ⇒ `0.5 + 0.5 · frac`, where `frac ∈ [0,1)` is the fraction of the file's in-scope sources that are trace-equivalent; **clamped strictly `< 1.0`** until `frac == 1.0`.
- `frac == 1.0` (total trace-equivalence; outcome-equivalence for bonds) ⇒ exactly `1.0`.

High band is **monotonic** in `frac` → GEPA sees a continuous optimization gradient. Elsewhere discrete. `1.0` reserved exclusively for total trace-equivalence (SC-004).

**Rationale**: Verbatim from the boundary clarification. A single function imported by both `tools/codegen/` (production gate) and `codegen_opt/metric.py` (GEPA) guarantees SC-004 "they agree by construction."

**Alternatives**: separate gate vs metric implementations — rejected: drift risk; SC-004 demands identical computation.

---

## R5 — Real `dspy.GEPA` wiring + per-subsystem prompts (FR-010, FR-011)

**Decision**: Replace 019's hand-rolled `dspy.Predict` reflective loop with `dspy.GEPA`. The GEPA metric callable returns `dspy.Prediction(score=…, feedback=…)` where `feedback` is the **actual divergence text**: compiler error, failing back-test assertion, or the specific trace-divergence record from `tools/equiv/relation.py`. Prompts are **per-subsystem**, all descending from a shared optimized `_base.md`; each subsystem's optimized prompt seeds the next (carry-forward). Production `prompt.load(subsystem)` selects by the file's subsystem and makes no LM/network call.

**Rationale**: FR-010/FR-011. GEPA's reflective Pareto evolution is exactly designed to consume textual feedback — the trace-divergence record is a far richer signal than a scalar, which is the point of using GEPA over a numeric optimizer.

**Alternatives**: keep the hand-rolled loop — rejected by FR-010. Single global prompt — rejected by decision 6 (per-subsystem; the hard dynamic tier needs its own).

---

## R6 — Budget/rollout cap, offline-only (FR-012, SC-006)

**Decision**: GEPA driven with a hard `--budget` (max metric-calls / rollouts; each call may run a `dotnet build` + REPL trace). On cap, return **best-so-far** per-subsystem prompt (usable artifact, no runaway). The optimizer imports `litellm`/`openai`; `OPENAI_API_KEY` from env; lives ONLY in `tools/codegen_opt/`. Neither `tools/equiv/`, `tools/codegen/`, nor any DBOS step imports dspy/litellm/openai (test asserts this — SC-008).

**Rationale**: 019 (C)-hybrid invariant preserved verbatim. SC-006 budget guarantee.

---

## R7 — Equivalence corpus + bonds outcome-only (FR-005, FR-006)

**Decision**: Corpus = unified REPL suite (384) + book suite (141), trace-compared; bonds plays, **outcome-only** (succeed/suspend/fail + bindings; no interleaving diff); plus the 374 Dart unit tests **ported to C#** for module-level back-test signal. GLP `.glp` sources are executed in place under `programs/` against both REPLs — never copied (single source of truth). `corpus.py` enumerates sources and tags each with its comparison mode (trace | outcome-only) and subsystem.

**Rationale**: Decision 7. Bonds legitimately suspend on escrow timers — their interleaving is not a fidelity signal, their outcome is.

**Alternatives**: trace-compare bonds — rejected: escrow-timer suspension is a valid outcome, interleaving is environmental.

---

## R8 — Subsystem classification + curriculum order (FR-007, decision 8)

**Decision**: Five subsystems with tiers — `heap` (strict), `bytecode` (strict), `compiler` [incl. type-checker + partial-evaluator + SRSW] (strict), `runtime-core` [single-computation] (strict), `multiagent` (dynamic). Curriculum order: the four strict subsystems first (in dependency order from `dart_depgraph`), `multiagent` last, attacked from strength with a mature prompt. Classification is a checked-in mapping in `.codeconv/equiv-manifest/subsystems.yml`, keyed by source path prefix, validated against `dart_depgraph` so it stays consistent with the topo/SCC order (015, read-only).

**Rationale**: Decision 8 + FR-007. The dynamic-tier verification mode (pinned-schedule vs accept-any-causal-schedule) is **deferred to when that tier is reached, with empirical divergence data** (FR-009, US4 acceptance 3) — recorded in `contracts/subsystem_curriculum.md` before bulk dynamic generation, not now.

**Edge case (strict-tier nondeterminism)**: if a "strict" program reveals scheduling nondeterminism, reclassify it to the dynamic tier (partial-order) rather than force exact equality (spec edge case).

---

## R9 — Train / held-out manifest (FR, SC-003)

**Decision**: A checked-in, versioned `subsystems.yml` assigns each subsystem's in-scope GLP sources to **train (~70%)** and **held-out (~30%)** splits, deterministically (stable hash of source path → bucket, recorded explicitly so it never wobbles). GEPA generates + reflects on train; SC-003 improvement is measured on held-out. `manifest.py` loads and validates (every in-scope source assigned exactly once; ratios within tolerance).

**Rationale**: Decision 10 (designated split). Reproducible measurement → SC-003 is a fixed, auditable number, not run-to-run noise.

**Alternatives**: random per-run split — rejected: non-reproducible SC-003.

---

## R10 — C# REPL trace instrumentation (assumption: in scope)

**Decision**: The converted C# REPL must emit a structured trace comparable to Dart's `:trace`, covering the R1 event kinds. This instrumentation is added to the **converted candidate** (`out/csharp/` REPL), not the Dart golden. `normalize.py` parses both into the common normalized model. Where the Dart `:trace` format under-specifies an event needed for comparison, the **normalizer adapts to what Dart emits** — we do NOT modify the Dart runtime (it is the read-only oracle); if Dart genuinely cannot emit a needed event, that is a spec gap to STOP and report (CLAUDE.md), not patch around.

**Rationale**: Spec assumption "adding equivalent trace instrumentation to the converted C# REPL is in scope." Keeps the golden untouched.

---

## R11 — Persistence: new table + migration 0008, additive (FR-018)

**Decision**: New `codeconv.dart_equivalence` table (two-phase, like `dart_codegen`) recording per-(file × source) verdict, the trace-divergence record, comparison mode, fidelity inputs, golden/candidate trace hashes, subsystem, tier, and a **stale** flag (FR-016). Migration `0008_equivalence.py`, `down_revision = '0007'`, single head, `CREATE TABLE IF NOT EXISTS`, no `public`/`dbos` objects (012 schema isolation). 019's `dart_codegen` + upstream tables are read-only inputs; equiv state is purely additive (extends, never recomputes — FR-018).

**Migration-head note**: the versions dir has a historical split at `0003` (`0003_d2net_into_codeconv.py` + `0003_dart_plans.py`) that was already linearized through `0005`/`0006`/`0007`; `0008` chains cleanly off the single `0007` head. The plan's data-model includes the single-head proof.

---

## R12 — DBOS replay-safety for the equiv step (019 R3 carry-forward, HARD GATE)

**Decision**: The durable `equiv` step is a **deterministic ingest** of recorded normalized traces + the computed verdict — it does **not** spawn a REPL or run a comparison with side effects inside the step. Trace capture (nondeterministic: process spawn, timing) happens in the agent/CLI layer (`/codeconv-equiv` skill + `equiv capture`); the step ingests the checked-in/recorded trace artifacts and applies the pure `relation.py` + `fidelity.py` (deterministic given inputs). Absent recorded traces ⇒ typed `needs_agent_work` sentinel (never raises) — the 019/convspec R3 pattern verbatim.

**Rationale**: Any nondeterminism (REPL spawn, wall-clock, scheduling) inside a DBOS step breaks replay. Confining capture to the agent layer and making the step a pure function of recorded inputs preserves replay-safety — the same discipline that let 019 keep codegen durable.

---

## Consolidated decisions table

| # | Decision | Rationale | Rejected alternative |
|---|----------|-----------|----------------------|
| R1 | Normalized trace = causal-edged semantic events; heap→logical relabel | invariant under address/interleaving | raw text diff; final-snapshot only |
| R2 | Causal/partial-order relation; total-order specialization on strict tier | matches decision 5; cheaper where deterministic | partial-order everywhere |
| R3 | Bytecode-emission diff first | bootstrap-window signal; SC-007 spine | defer to full runtime |
| R4 | Single tiered `fidelity.py` (0/0.25/0.5+0.5·frac/1.0) | SC-004 by construction | separate gate vs metric |
| R5 | Real `dspy.GEPA`; per-subsystem prompts on shared base | FR-010/FR-011; textual feedback | hand-rolled loop; single prompt |
| R6 | Budget cap, best-so-far, offline-only | FR-012/SC-006; (C)-hybrid | unbounded; in-package LM |
| R7 | All-suites corpus; bonds outcome-only; sources in place | FR-006; escrow suspension is environmental | copy sources; trace-diff bonds |
| R8 | 5 subsystems, strict→dynamic curriculum; dynamic mode deferred | decision 8; attack hard tier from strength | dynamic-first; decide mode now |
| R9 | Versioned 70/30 manifest | reproducible SC-003 | random split |
| R10 | Instrument candidate C# REPL; normalizer adapts to Dart | golden read-only | modify Dart trace |
| R11 | `dart_equivalence` + migration 0008, additive | FR-018; 012 schema isolation | recompute upstream |
| R12 | Durable equiv step = deterministic verdict ingest | replay-safety (019 R3) | REPL spawn inside step |
