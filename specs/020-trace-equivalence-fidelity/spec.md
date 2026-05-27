# Feature Specification: Trace-Equivalence-Driven Codegen Fidelity

**Feature Branch**: `020-trace-equivalence-fidelity`
**Created**: 2026-05-27
**Status**: Draft
**Input**: User description: "GEPA-driven Dart→C# codegen fidelity verified by behavioural and execution-trace equivalence to the original GLP runtime across all GLP source suites."

## Context

Feature 019 (`codeconv-codegen`) shipped a deterministic, replay-safe Dart→C# codegen engine: a build-gated production tool (`tools/codegen/`), an offline prompt optimizer (`tools/codegen_opt/`), the `dart_codegen` table (migration `0007`), a durable `codegen` step, and an escalation/review/promotion loop. Its fidelity signal is **compile + ported-test pass-rate + sampled human review**.

That signal is necessary but not sufficient. The Dart code being converted **is** the GLP language implementation (compiler, bytecode runner, heap, runtime, multiagent layer). A converted C# runtime is only correct if, for every GLP source program, it **behaves identically to the Dart original** — same outcome **and** an equivalent execution trace. Feature 020 raises the fidelity bar to **behavioural + execution-trace equivalence** and makes a real `dspy.GEPA` optimizer drive codegen toward it, per subsystem, easy-to-hard.

019's (C)-hybrid invariant is preserved verbatim: the production/durable path stays deterministic and LM-free; all LM use stays in the offline optimizer. The new equivalence harness is **deterministic and LM-free**, so it composes with the durable path; GEPA (LM-bearing) stays offline and consumes the harness's divergence reports as reflective feedback.

## Clarifications

### Session 2026-05-27

All design decisions below were settled with Gabi before specification (zero open clarifications):

1. **Q: Optimizer real GEPA or the 019 reflective loop?** → A: Wire **real `dspy.GEPA`** (replace the hand-rolled `dspy.Predict` reflective loop in `codegen_opt`).
2. **Q: What is the fidelity objective?** → A: **Behavioural + execution-trace equivalence** of the converted C# GLP runtime to the Dart original, across all GLP source suites.
3. **Q: Optimizer-first or convert-on-baseline-first?** → A: **Optimizer-first**, but bootstrapped on the signal available at each stage (build + module back-tests before a runnable runtime exists; trace-equivalence once it does) — a co-evolution loop, not one offline pass.
4. **Q: Fidelity metric scale?** → A: Tiered. **0.0** for non-compiling (hard floor); a **low band** for compiling-but-unreviewed with no equivalence evidence; a **high band strictly below 1.0** for back-tested + trace-captured code that is not yet full-trace-equivalent across all sources; **1.0 reserved exclusively for total trace-equivalence**. No partial state — not compile, not human approval, not passing back-tests alone — reaches 1.0.
5. **Q: Trace-equivalence relation?** → A: **Causal / partial-order equivalence.** REQUIRE identical sequence of: unification outcomes, suspension/reactivation events, writer-binding order, and bytecode ops. ABSTRACT over: heap addresses (canonicalized to logical variable identities) and the scheduling order of independent (causally unordered) goals. The **bytecode-op sequence is the spine** — same opcodes at the same logical PCs.
6. **Q: GEPA prompt granularity?** → A: **Per-subsystem prompts** (heap / bytecode / compiler+type+partial-eval+SRSW / runtime-core / multiagent), sharing a common optimized base that transfers forward across the curriculum.
7. **Q: Equivalence corpus?** → A: **All suites** — unified REPL (384), book (141), bonds plays — run through both REPLs; plus the 374 Dart unit tests **ported to C#** for module-level back-tests. **Bonds plays are outcome-equivalence only** (they legitimately suspend on escrow timers; their interleaving is not trace-compared).
8. **Q: GEPA curriculum order?** → A: **Highly deterministic subsystems first, dynamic subsystem(s) last** — when codegen is already well-optimized. The dynamic multiagent tier is attacked from strength, with a mature prompt; its pin-canonical-schedule-vs-partial-order verification mode is decided when that tier is reached, with empirical divergence data.

- Q: Fidelity band numeric boundaries and within-band shape? → A: **Anchored + high-band monotonic.** `0.0` non-compile (hard floor); flat `0.25` low band (compiles, no equivalence evidence); high band = `0.5 + 0.5·(fraction of in-scope sources trace-equivalent)`, clamped strictly `< 1.0` until the fraction reaches 100%, which snaps to exactly `1.0`. Continuous gradient on the optimization frontier (high band); discrete elsewhere; `1.0` reserved for total trace-equivalence.
- Q: How is each subsystem's equivalence corpus split for GEPA train vs held-out scoring? → A: **Designated split via a checked-in, versioned manifest** assigning each subsystem's in-scope GLP sources to train (~70%) vs held-out (~30%); deterministic and reproducible so SC-003 is a fixed, auditable measurement (no run-to-run wobble).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Differential equivalence oracle over GLP sources (Priority: P1) — MVP

A conversion engineer can take any GLP source program, run it through the Dart REPL (golden) and the converted C# REPL (candidate), and get a verdict: **equivalent** or **divergent**, with the first divergence pinpointed. Equivalence is judged under the causal/partial-order relation (decision 5): outcomes must match; normalized traces must match on dependent events; independent-goal interleaving and heap addresses are abstracted away. A cheaper **bytecode-emission diff** runs first as an early checkpoint. Bonds plays are judged outcome-only.

**Why this priority**: Nothing downstream has a fidelity signal without this. The oracle is the measurement instrument the metric and GEPA both depend on; it is independently valuable as a conformance harness even before any optimization.

**Independent Test**: Feed the oracle a known-equivalent pair (same program, faithful C#) → verdict equivalent; feed a deliberately corrupted C# trace (e.g. a suspended writer bound eagerly) → verdict divergent with the divergence event identified. Verify heap-address relabeling and independent-goal reordering do not produce false divergences.

**Acceptance Scenarios**:

1. **Given** a GLP source whose C# conversion reproduces Dart's behaviour, **When** the oracle runs both REPLs, **Then** it reports equivalent (outcome + normalized trace match) and exits 0.
2. **Given** a C# conversion that diverges at a suspension/reactivation event, **When** the oracle compares, **Then** it reports the first divergent event with its causal position, and the file is marked not-equivalent.
3. **Given** two runs that differ only in heap-address values and in the interleaving of independent goals, **When** the oracle compares, **Then** it reports equivalent (those dimensions are abstracted).
4. **Given** a bonds play that suspends on an escrow timer, **When** the oracle runs in outcome-only mode, **Then** it compares final outcome only and does not trace-diff the interleaving.

---

### User Story 2 - Strict-tier subsystem conversion verified by exact equivalence (Priority: P1)

The deterministic subsystems — `heap_fcp`, bytecode runner/dispatch, compiler + type-checker + partial-evaluator + SRSW transform, and single-computation runtime core — are converted to C# and each file is gated by build **and** exact equivalence: identical emitted bytecode and total-order trace equality against Dart (these subsystems are deterministic, so the partial-order relaxation is unnecessary — equality is the stricter, cheaper check). Conversion proceeds in dependency/curriculum order; a file counts as converted only when it builds and is exact-equivalent, else it escalates (never guesses).

**Why this priority**: This is the load-bearing majority of the runtime and, being deterministic and structurally close to the Dart source, the highest-confidence and fastest to verify. It yields a behaviourally-faithful C# core — a viable MVP runtime — and is the corpus GEPA learns the GLP→C# idioms on first.

**Independent Test**: Convert `heap_fcp` and the bytecode runner; run the unified + book suites through the C# REPL; confirm emitted-bytecode diff is empty and total-order traces equal Dart for the deterministic programs; confirm a non-faithful conversion is rejected by the gate.

**Acceptance Scenarios**:

1. **Given** a deterministic subsystem file with its dependencies already converted, **When** its C# is ingested, **Then** the gate runs build → bytecode-emission diff → total-order trace diff, and accepts only on all-pass.
2. **Given** a converted compiler, **When** a GLP source is compiled by both runtimes, **Then** the emitted bytecode sequences are identical (same opcodes, same logical PCs).
3. **Given** a strict-tier file whose C# diverges in trace, **When** ingested, **Then** it is not marked converted; the divergence is recorded as feedback and the file returns for one bounded repair, then escalates.

---

### User Story 3 - Real GEPA per-subsystem prompt optimization (Priority: P2)

The offline optimizer uses real `dspy.GEPA` to evolve **per-subsystem** codegen prompts. GEPA's metric returns a score **and textual feedback** — the actual divergence (compiler error, failing back-test assertion, or the specific trace-divergence event). Optimization runs the curriculum easy-to-hard; each subsystem's optimized prompt seeds the next via a shared base. Runs are budget-capped (best-so-far on cap) and offline-only; the production path consumes the exported per-subsystem prompt artifacts and performs no optimization itself.

**Why this priority**: This is the mechanism that drives fidelity up; it depends on US1 (the signal) and benefits from US2 (the strict tier provides the cleanest early training signal). Per-subsystem + curriculum is what makes the hard tier tractable.

**Independent Test**: On a held-out equivalence eval set for one subsystem, run `dspy.GEPA` with a mocked LM and confirm the optimized prompt scores ≥ baseline and that the budget cap halts with best-so-far; confirm the production path imports no LM/dspy and selects the per-subsystem prompt by subsystem.

**Acceptance Scenarios**:

1. **Given** a subsystem with a divergence-labelled eval set, **When** `dspy.GEPA` optimizes within budget, **Then** it produces a prompt scoring ≥ baseline on the held-out split and reflects on the textual divergence feedback.
2. **Given** the budget/rollout cap is reached, **When** optimization stops, **Then** it returns the best-so-far prompt (no runaway, usable artifact).
3. **Given** an exported per-subsystem prompt set, **When** production codegen runs for a file in that subsystem, **Then** it loads that subsystem's prompt and makes no LM/network call.

---

### User Story 4 - Dynamic multiagent tier converted last under causal equivalence (Priority: P2)

The `lib/multiagent/` subsystem — `isolate_manager`, channels, rpc-routing — is converted **last**, with the mature curriculum prompt. Because cross-agent scheduling is bounded but not always deterministic, it is verified under the causal/partial-order relation plus outcome-equivalence (not exact total-order). The verification mode for genuinely concurrent dynamics (pin a canonical verification-schedule in both runtimes vs. accept any causally-valid schedule) is decided at this point using observed divergence data.

**Why this priority**: It is the only genuinely nondeterministic tier and the hardest to verify, so it is sequenced last to benefit from a fully-optimized prompt and the harness proven on the strict tier. Lower priority than the strict tier because it is a minority of files and depends on the rest of the runtime being faithful first.

**Independent Test**: Convert `isolate_manager` and run the multiagent/CSSN plays through both REPLs; confirm causal-order events (writer-bind → reader-reactivation across agents) match and outcomes match, while independent cross-agent interleaving is not flagged divergent.

**Acceptance Scenarios**:

1. **Given** the strict tier is fully converted and the prompt is frozen-mature, **When** the dynamic tier is generated, **Then** it is gated by build + causal/partial-order trace-equivalence + outcome-equivalence.
2. **Given** a multiagent run where independent agents interleave differently between Dart and C#, **When** compared, **Then** it is equivalent provided all data-dependent events and outcomes match.
3. **Given** the dynamic-tier verification mode decision, **When** recorded, **Then** the chosen mode (pinned-schedule or partial-order) is captured in the spec/contract with its rationale before bulk dynamic-tier generation.

---

### User Story 5 - Tiered fidelity metric and corpus-wide promotion (Priority: P3)

The fidelity score is tiered per decision 4: 0.0 non-compile floor; low band for compiling-unreviewed; high band strictly below 1.0 for back-tested + trace-captured; 1.0 only at total trace-equivalence across all in-scope sources. A subsystem (and ultimately the runtime) is promoted to "converted" only when its corpus is fully trace-equivalent (outcome-equivalent for bonds). The metric is computed identically by the production gate and the GEPA optimizer so they agree by construction.

**Why this priority**: It formalizes the gate the other stories feed; it can be developed and unit-tested independently (pure scoring math) and layered on once the harness and conversion exist.

**Independent Test**: Unit-test the scoring function across all tiers; confirm a compiling-but-not-trace-equivalent file scores in the high band but below 1.0, and only a fully trace-equivalent corpus promotes.

**Acceptance Scenarios**:

1. **Given** a file that compiles and passes back-tests but is not yet trace-equivalent, **When** scored, **Then** its score is below 1.0.
2. **Given** a subsystem corpus fully trace-equivalent to Dart (bonds outcome-equivalent), **When** the promotion gate runs, **Then** it promotes; otherwise it does not.

---

### Edge Cases

- **Bootstrapping (no runnable C# runtime yet)**: the very first strict-tier files cannot be trace-verified end-to-end; they fall back to build + module back-test equivalence until enough of the runtime compiles to run a GLP program. The metric must not award 1.0 in this window.
- **Trace nondeterminism inside the "deterministic" tier**: if a strict-tier program reveals scheduling nondeterminism, it is reclassified to the dynamic tier (partial-order) rather than forced into exact equality.
- **Divergence that is a Dart bug**: if Dart and C# differ because the Dart original is itself wrong per spec, this is a GLP bug — STOP and report per CLAUDE.md Bug Protocol; do not "fix" the C# to match a wrong oracle.
- **Bonds suspension as outcome**: a suspended outcome is a valid, expected result for escrow-timer plays; outcome-equivalence treats suspend-vs-succeed as a real difference but does not trace-diff the interleaving.
- **Budget exhaustion mid-curriculum**: optimization halts with best-so-far for the current subsystem; later subsystems still run on the last frozen base prompt.
- **Source drift**: a Dart source changing after conversion invalidates the recorded equivalence (stale); re-verification is required, not assumed.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST provide a deterministic, LM-free differential equivalence oracle that runs a GLP source through both the Dart (golden) and converted C# (candidate) REPLs and produces a verdict (equivalent | divergent) with the first divergent event identified.
- **FR-002**: The oracle MUST capture a **normalized execution trace** from each runtime in which heap addresses are canonicalized to logical variable identities and trace events carry their causal (data-dependence) ordering.
- **FR-003**: Trace-equivalence MUST be judged under the causal/partial-order relation: identical sequences of unification outcomes, suspension/reactivation events, writer-binding order, and bytecode ops are REQUIRED; heap-address values and the relative order of causally-independent goals MUST be abstracted. The bytecode-op sequence (same opcodes at same logical PCs) is the primary spine of the comparison.
- **FR-004**: The system MUST provide a bytecode-emission diff as an early, cheap equivalence checkpoint comparing the bytecode the C# compiler emits for a GLP source against the Dart-emitted bytecode, available before a full runtime is runnable.
- **FR-005**: The system MUST support outcome-equivalence-only comparison for designated sources (bonds plays), comparing final outcome (succeed/suspend/fail + bindings) without trace-diffing interleaving.
- **FR-006**: The equivalence corpus MUST comprise the unified REPL suite (384), the book suite (141), and the bonds plays (outcome-only); plus the 374 Dart unit tests ported to C# for module-level back-test signal. The system MUST run the GLP `.glp` sources against the C# REPL without copying them (single source of truth under `programs/`).
- **FR-007**: Conversion MUST proceed by subsystem in curriculum order — deterministic subsystems first (`heap_fcp`; bytecode runner/dispatch; compiler + type-checker + partial-evaluator + SRSW; single-computation runtime core), the dynamic `lib/multiagent/` subsystem last.
- **FR-008**: For deterministic (strict-tier) subsystems, the conversion gate MUST require build success AND exact equivalence (empty bytecode-emission diff AND total-order trace equality); a file is "converted" only when both pass, else it escalates without guessing.
- **FR-009**: For the dynamic (multiagent) subsystem, the gate MUST require build success AND causal/partial-order trace-equivalence AND outcome-equivalence; the chosen verification mode (pinned canonical schedule vs. accept-any-causal-schedule) MUST be recorded with rationale before bulk dynamic-tier generation.
- **FR-010**: The offline optimizer MUST use real `dspy.GEPA` (replacing 019's hand-rolled reflective `dspy.Predict` loop). The GEPA metric MUST return both a scalar score and textual feedback derived from the actual divergence (compiler error, failing back-test, or specific trace-divergence event).
- **FR-011**: The system MUST maintain **per-subsystem** codegen prompt artifacts that share a common optimized base; the base/learned idioms MUST transfer forward across the curriculum; production `prompt.load()` MUST select the prompt by the file's subsystem.
- **FR-012**: Optimization MUST be offline-only and budget/rollout-capped, returning best-so-far on cap; it MUST NOT be invoked on the production/durable path, and the production path MUST import no LM/dspy/litellm/openai (019's (C)-hybrid invariant preserved).
- **FR-013**: The fidelity metric MUST be tiered with these exact boundaries: `0.0` for non-compiling (hard floor); a flat `0.25` low band for compiling-unreviewed with no equivalence evidence; a high band computed as `0.5 + 0.5·(fraction of in-scope sources trace-equivalent)` for back-tested + trace-captured but not fully trace-equivalent code, clamped strictly below `1.0` until the equivalent fraction reaches 100%; `1.0` reserved exclusively for total trace-equivalence over the file's in-scope corpus (the fraction reaching 100% snaps the score to exactly `1.0`). The high band is monotonic in the trace-equivalent fraction so GEPA sees a continuous gradient on the optimization frontier. The production gate and the GEPA metric MUST compute the same score.
- **FR-014**: Promotion of a subsystem (and the runtime) to "converted" MUST require full trace-equivalence across its in-scope corpus (outcome-equivalence for bonds); compile, human approval, or back-test pass alone MUST NOT promote.
- **FR-015**: The optimizer-first co-evolution loop MUST, per subsystem: optimize-before-generate on the currently-available signal → generate → run the available equivalence gate → reflect divergences into GEPA → regenerate weak files → freeze the subsystem prompt → carry the base forward.
- **FR-016**: The system MUST detect Dart source drift after conversion and mark affected equivalence results stale, requiring explicit re-verification rather than assuming prior equivalence holds.
- **FR-017**: When Dart and C# diverge because the Dart original violates the GLP spec, the system MUST surface it as a suspected GLP bug (per CLAUDE.md Bug Protocol) rather than altering C# to match a wrong oracle.
- **FR-018**: The system MUST reuse 019's deterministic engine (`tools/codegen/`, `dart_codegen`, durable `codegen` step) and 018/earlier upstream artifacts read-only, extending rather than recomputing them; equivalence/trace state MUST persist additively (new migration chained after `0007`).
- **FR-019**: All capabilities MUST remain reachable: 015–019 entrypoints and the 019 test baseline (104 pure + 73 codegen suite, green as of 2026-05-27) MUST continue to pass.

### Key Entities

- **Normalized trace**: an ordered-with-causal-edges sequence of semantic events (unification outcome, suspension, reactivation, writer-binding, bytecode op @ logical PC) with heap addresses relabeled to logical variable identities.
- **Trace-divergence record**: the first point at which candidate and golden normalized traces differ under the equivalence relation — event kind, causal position, expected vs. actual — used both as gate evidence and as GEPA reflective feedback.
- **Subsystem**: a curriculum unit (`heap`, `bytecode`, `compiler` [incl. type/partial-eval/SRSW], `runtime-core`, `multiagent`) with a determinism tier (strict | dynamic) and an associated per-subsystem prompt artifact.
- **Per-subsystem prompt artifact**: a checked-in, GEPA-optimized instruction set keyed by subsystem, descending from a shared base, with provenance (optimizer, metric score, dataset hash, model, generated-at).
- **Equivalence-eval dataset**: per-subsystem GLP sources + expected normalized traces (or outcomes for bonds), partitioned by a checked-in, versioned manifest into a train split (~70%, used for GEPA generation + reflection) and a held-out split (~30%, used for GEPA scoring / SC-003). The split is deterministic and reproducible.
- **Fidelity score**: the tiered metric value for a file/subsystem/corpus, with 1.0 reserved for total trace-equivalence.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of in-scope GLP sources (unified 384 + book 141) executed on the converted C# runtime yield a trace-equivalent verdict to Dart; 100% of bonds plays yield an outcome-equivalent verdict.
- **SC-002**: ≥ 95% of in-scope production runtime files reach build + exact/causal equivalence without manual editing (escalations excluded), per the appropriate tier.
- **SC-003**: For every subsystem, the GEPA-optimized per-subsystem prompt scores measurably higher on the tiered fidelity metric than the baseline (and than the prior subsystem's base) on the manifest-designated held-out (~30%) equivalence eval split.
- **SC-004**: No file scores 1.0 unless it is fully trace-equivalent (outcome-equivalent for bonds); compiling, back-test-passing, or human-approved-but-not-trace-equivalent files all score below 1.0.
- **SC-005**: The equivalence oracle produces zero false divergences on heap-address relabeling and independent-goal reordering (verified on constructed cases), and zero false equivalences on a seeded divergence battery.
- **SC-006**: Optimization never exceeds its configured budget/rollout cap; a capped run still yields a usable best-so-far per-subsystem prompt.
- **SC-007**: The bytecode-emission diff is empty for 100% of deterministic-tier sources once their compiler subsystem is converted (the spine holds before execution).
- **SC-008**: The production/durable path remains LM-free (no dspy/litellm/openai import), and the 019 baseline test suite stays green.

## Assumptions

- The 130 ratified conversion plans + convspecs and the 019 deterministic engine are the conversion substrate; 020 adds the fidelity layer, not a new codegen engine.
- The Dart runtime is the behavioural oracle (golden); where it is wrong per spec, that is a separately-reported GLP bug, not a conversion target.
- The GLP REPL test suites are runtime-agnostic GLP sources and can be pointed at the C# REPL without modification; only the Dart **unit** tests (374) require porting to C#.
- Single-computation suspension/reactivation is deterministic given a fixed goal-queue order and therefore belongs to the strict tier; genuine nondeterminism is confined to `lib/multiagent/`.
- The offline optimizer's LM backend (litellm/OpenAI) and `OPENAI_API_KEY` remain confined to `tools/codegen_opt/`; transmitting Dart source to the LM offline is the accepted IP tradeoff from 019.
- `dspy.GEPA` (DSPy ≥ 3.2, `gepa`) is available in the optimizer extras; the C# target is .NET 10 (`dotnet` ≥ 10 on PATH), consistent with 019.
- The C# runtime must emit a structured trace comparable to Dart's `:trace`; adding equivalent trace instrumentation to the converted C# REPL is in scope.

## Dependencies

- **Builds on 019** (`codeconv-codegen`): `tools/codegen/`, `tools/codegen_opt/`, `dart_codegen` (migration `0007`), durable `codegen` step.
- **Reuses 015–018** read-only: depgraph/topo-SCC order, convspecs, conversion plans, scaffolded `out/csharp/` tree.
- **New migration** chained after `0007` for equivalence/trace/per-subsystem state.
