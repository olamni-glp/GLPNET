# Three-Role Orchestration Protocol (glpnet)

**Status**: ACTIVE (063 US3, T026). **Seeded by** the recorded
method-and-dogfood document
(`docs/research/three-role-agent-teams/METHOD-AND-DOGFOOD.md` — the external
grounding, dogfood metrics, and design rationale live THERE) and **executed
through** the installed buildkit capability `/bk-3rtask` (spec-051 — the
mechanical contract lives in the installed skill; this protocol duplicates
neither). This document is the operator's runbook: every step an engagement
takes is named here (contract acceptance,
`specs/063-wave-5-consolidated-captured-triad/contracts/three-role-engagement.md`).

## 1. The two triads

Run in sequence; the **curator** is the only writer of the shared artifact.

**PLANNING triad — generator → validator → curator** (designs the method,
never executes it):

| Role | Charter |
|---|---|
| generator | Proposes the method as an addressable artifact — source partition, questions, rubric, gates; every element gets an id. |
| validator | Works **BLIND** to the generator. Adversarially red-teams each element: CONFIRM / REFUTE / ESCALATE. Its job is to *break* the partition/rubric, not to agree. |
| curator | Deterministic merge into ONE canonical method; resolves what it can mechanically; **freezes** the source manifest, rubric, cycle cap, and token budget before execution starts. |

**EXECUTION triad — builder(scanner) ×N → critic(evaluator) → curator**
(runs the frozen method):

| Role | Charter |
|---|---|
| builders ×N (≥2, default 3) | Each pinned to a **pairwise-disjoint** evidence slice/lens, each **BLIND** to the others. Emit structured, attributed claims: `{claim, source_citation, confidence, tag}`. |
| critic | **MECHANICAL** set-op merge — never judgment: intersection ⇒ corroborated (promote); symmetric difference ⇒ singletons (candidate-miss set, re-examined via counter-query into the other slices); conflict ⇒ **ESCALATE** to the engineer. Scores on the frozen evidence-gated rubric. |
| curator | Synthesizes the grounded report from the critic's matrix; surfaces every open ESCALATE; **never self-decides a genuine conflict**. |

## 2. The load-bearing rules

1. **Blind-then-cross-verify.** Independence BEFORE comparison is real (no
   shared draft, no shared slice); comparison AFTER is mechanical (set ops on
   claim sets). The symmetric-difference step makes a single builder's unique
   find visible instead of averaging it away.
2. **False-consensus guard.** Builders over the SAME evidence family cannot
   corroborate each other — corroboration requires agreement across
   *different* families (the dogfood's frozen decision; error de-correlation
   is the theoretical warrant, method doc §2 #10).
3. **Authority order** (dogfood decision, adapted per engagement): brief
   constraints > repository head (the artifacts under review) > primary
   evidence family > secondary family > inference. A lower authority never
   overrides a higher one silently.
4. **Convergence loop.** ≥2 cycles, hard cap 10 (engineer approves the count
   up front); a cycle with zero fresh findings after cross-verify ends the
   loop early.
5. **Evidence & attribution.** Every claim carries its builder, slice, and
   citation (file:line for code/plan reviews). Unattributed claims are
   discarded by the critic, not merged.
6. **Engineer decision gates.** The engineer decides at: method freeze,
   cycle-count approval, every ESCALATE, and final acceptance. Conflicting
   claims reach the engineer VISIBLY (FR-013) — no silent merge, ever.
7. **Token-budget etiquette.** Declared before execution; default split per
   the method doc §4 (builders 25% / critic 10% / curators 25% / planning
   40%); expect 3–15× single-agent cost — engage only on high-value,
   genuinely-parallel subjects. Per-role/per-cycle usage lands in the
   spec-020 ledger (the installed capability does this).
8. **Constitution V.** All LM work runs through the installed Claude-side
   capability and its local codex CLI (critic preference, LOUD degradation to
   Claude with a recorded reduced-independence warning) — never an external
   LM API.

## 3. Running an engagement (operator steps)

1. **Choose the adapter** (method doc §3): code | plan | strategy | research
   — fixes the disjoint lenses and cross-verify test. Plan review lenses:
   completeness/coverage · feasibility/effort · risk/dependencies/consistency.
   Code review lenses: correctness/logic · security/trust-boundary ·
   spec-conformance/tests — over a size-invariant BRIEF (spec + changed-file
   list, never the diff body).
2. **Invoke the installed capability** (`/bk-3rtask`) with the subject brief,
   task type, and budget — or, where the capability's runtime is unavailable,
   run the roles as separate blind Claude agents following §1 verbatim and
   record the degradation in the engagement record.
3. **Freeze the method** at the curator gate (engineer ack).
4. **Execute**: blind builders → mechanical critic merge → curator synthesis;
   iterate to convergence within the approved cycle count.
5. **Decide** every ESCALATE explicitly; record the decision inline.
6. **Record** the engagement in `engagements/EN-<slug>.md` with: participants
   & roles, inputs, attributed claims, critic verdicts
   (CONFIRM/REFUTE/ESCALATE), escalations raised, engineer decisions, outcome.
7. **Terminal code review** (code adapter only) is DELEGATED to the shipped
   `/bk-codexreview` loop and referenced from the record, per spec-051.

## 4. What this protocol is NOT

Advisory only: it never pushes, never merges, never auto-invokes a pipeline
stage. It is not a GLP-native agent system (explicitly out of scope by the
roadmap record); building triads *in* GLP is a different, unclaimed feature.
