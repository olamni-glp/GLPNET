# Marathon block 07 — `m57f4c46e:implement:7` (implement_session)

**Feature**: 027-refinement-verification-framework
**Block kind**: `implement_session`
**Scope**: **Polish / closure** (T025–T028) — the success-criteria close-out + deferral-trail update.
No new real-tool runs; this is verification + record-keeping that the feature's claims are met.
**Owner-authorized** 2026-06-10: Gabi "yes pl proceed with block 07 (Polish) to finish the feature."

## Units (tasks.md)
- **T025 [P]** Final no-API gate — re-run `grep -rEi 'OPENAI_API_KEY|litellm|(^|[^a-z])openai'` over
  ALL framework artifacts **+ every spike harness/example** (now incl. `harness.py`, `proof.lean`,
  `front_back.pml`, `*.lean`); zero matches on any refinement/verification path; update
  `reconciliation/NO-API-GATE.md` with the T025 result (SC-003).
- **T026 [P]** Done-ness check vs `quickstart.md`: six tooling slots (SC-004), five Shapiro criteria
  mapped (SC-005), template + worked example (SC-001/008), armoury ≥7 tools (SC-012), and the three
  `RESULT.md` recorded against real tools + reproducible (SC-006/007/009/010/011). Record a
  `reconciliation/DONE-NESS.md` traceability check.
- **T027** Update `DEFERRALS.md` statuses: DEF-B1/DEF-H1 "partly de-risked" (minimal real-Lean /
  real-MLIR spikes delivered; full proofs/MLIR-infra still at #4/#11/#12); DEF-A3 anchored (full
  protocol model at #5/#6); DEF-B2 still open (citation). Rows are never deleted — closure is the trail.
- **T028** Final framework review: confirm the five entity→requirement coverages (`data-model.md`
  map) are satisfied and SC-010's three highest-risk claims each have empirical evidence from a
  real-tool run (MLIR round-trip, Lean proof, SPIN model-check — all ✅ recorded). Record in DONE-NESS.md.

## Method (spec-first; honest verification)
Each SC is checked by reading the actual artifact + (where applicable) confirming the recorded
real-tool RESULT.md exists and reproduces. A criterion that is NOT met is reported as a real gap —
not papered over. Done inline (no Workflow) — closure checks I verify directly.

## Out of scope
- Shipping the feature (buildkit GitFlow) — a separate owner decision AFTER 28/28 + review stage.
- Any new real-tool run or framework-doc authoring.

## Boundary
Checkpoint == commit/push boundary. On completion: final checkpoint, then commit + push ONLY this
block's files (`NO-API-GATE.md`, `DONE-NESS.md`, `DEFERRALS.md`, this plan, tasks.md).

## Escalation triggers
- A success criterion cannot be confirmed met from the artifacts → `stage_flagged`, report the gap to
  Gabi (do not mark done-ness green on an unmet criterion).
