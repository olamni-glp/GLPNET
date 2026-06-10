# Marathon block 05 — `m57f4c46e:implement:5` (implement_session)

**Feature**: 027-refinement-verification-framework
**Block kind**: `implement_session`
**Scope**: all remaining **non-real-tool-run** work — US1 (template/MVP), US2 (loop + no-API gate),
and the **doc+subject** halves of US3 (Lean) and US5 (SPIN). The real-tool spike RUNS (Lean
T014/T015, SPIN T024) and Polish go in blocks 06/07. **Owner-authorized** 2026-06-10: Gabi directed
"US1 · US2 · US3 Lean · US5 SPIN · Polish … in parallel where possible step by step otherwise."

## Execution strategy (honoring the directive)
- **PARALLEL** (independent files, no inter-dependency) — one Workflow fan-out under the standing
  preauth-workflow grant: T006, T007 (US1) · T011, T013 (US3) · T021, T023 (US5). Six distinct
  output files → no write conflict, no worktree needed.
- **SERIAL** (dependent / verification) — after the authoring batch + a consistency review:
  T008 (depends T006) · T009 (read `codegen_opt/optimize.py:257–335`) · T010 (no-API grep gate).

## Units (tasks.md)
- **T006 [US1]** `reconciliation/METRIC-COMBINATION-TEMPLATE.md` — the `name|kind|tool|threshold`
  template + filled #5 worked example (≥1 formal row, byte contract) + host/infra rule (R9). FR-003.
- **T007 [US1]** `reconciliation/INTERACTIVE-SPEC-STEP.md` — owner-confirmation protocol (FR-060) +
  PRE-SPECIFY pointer surfacing DECISIONS-LOG + DEFERRALS (FR-061).
- **T011 [US3]** `reconciliation/LEAN-TACTIC-LOOP.md` — bounded Claude-over-MCP tactic loop, budget
  20/tuned, sorry-isolation + escalation, Lean primary/Rocq alt (DEF-F-tooling), WSL2 setup. FR-030–034.
- **T013 [US3]** `spikes/lean/<Property>.lean` — SRSW-preservation on a toy clause (fallback:
  unification soundness, PROP-1); a real Lean 4 theorem with `sorry` (discharged at T014/T015).
- **T021 [US5]** `reconciliation/PROTOCOL-VERIFICATION-ARMOURY.md` — ≥7-tool matrix (SPIN default,
  TLA+, UPPAAL, nuXMV, mCRL2, FDR4, CADP) + seed-type selection + SPIN-mandatory-in-#2/#5/#6 rule.
- **T023 [US5]** `spikes/spin/front_back.pml` — minimal front↔back handshake, named safety
  (deadlock-freedom, no unspecified receptions) + named liveness; minimal ONLY (DEF-A3). HANDSHAKE-1.
- **T008 [US1]** validate the template (well-formedness + formal-tier presence) — SC-001/008.
- **T009 [US2]** confirm loop↔precedent seam map vs `optimize.py:257–335` (candidate↔generate_fn,
  proposer↔propose_fn, evaluator↔score_instructions, budget↔BudgetCounter) — FR-011, SC-002.
- **T010 [US2]** no-API grep gate over all framework artifacts → zero matches; record — FR-012, SC-003.

## Quality gate (spec-first discipline)
The six authored docs encode ALREADY-RATIFIED decisions (R1–R15, DEFERRALS, REFINEMENT-METHOD). After
the Workflow fan-out, **review each file** against its anchors and fix any drift BEFORE the dependent
verification + commit. Subagents must NOT invent new decisions — flag gaps, never fabricate.

## Out of scope (later blocks)
- T014/T015 (Lean real run), T024 (SPIN real run) — block 06 (observed real-tool runs).
- T025–T028 (Polish) — block 07.

## Boundary
Checkpoint == commit/push boundary. On completion: final checkpoint, then commit + push ONLY this
block's files (the six new artifacts + tasks.md + this plan), under the standing grant.

## Escalation triggers
- A subagent cannot satisfy a required FR/AC without inventing a decision → it flags a gap; I escalate
  `stage_flagged` to Gabi rather than fabricate (spec-first; no guessing).
- T009 finds an unmatched loop seam, or T010 finds any API hit on a refinement/verification path →
  that is a real finding: report, do not paper over.
