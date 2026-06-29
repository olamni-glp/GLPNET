# Pipeline Status Index — GLP → Gleam/AtomVM Baseline Program (T003)

**Feature** `036-glp-gleam-baseline-program` · **Marathon run** `mrun-5611c436ba95` · **Authored** 2026-06-29 (T003, Phase A).

Status table for every research pipeline (P1…P8 + the ANTLR deep-dive). Each pipeline is a Claude
**Workflow** (ground → web? → ≥2 design lenses → adversarial review → synthesis, text-only returns)
honoring `../../../specs/036-glp-gleam-baseline-program/contracts/pipeline-contract.md`; there is no
committed "script" file — the Workflow is authored at run time and its result is extracted to the
artifact path below (the P1/P5 extraction is the precedent). The durable unit of progress is the
marathon run, not a script on disk.

Corpus sources: [`../CORPUS-INDEX.md`](../CORPUS-INDEX.md). Proof invocation:
[`../PROOF-HARNESS.md`](../PROOF-HARNESS.md). Ratified architecture (fixed input ED-1…ED-6):
[`P5-il-machine-language/DECISIONS.md`](./P5-il-machine-language/DECISIONS.md).

## Status table

| Pipeline | Task | Phase | Status | Artifact path | Verification gate |
|---|---|---|---|---|---|
| **P5 — IL / machine-language** | (pre-done) | B | ✅ **DONE** (ED-1…ED-6, spike-verified) | `P5-il-machine-language/{DOSSIER,DECISIONS}.md` | PASSED — byte-identical + execution-equivalent + verifiers fire (`spike/p5-il-merge/SPIKE-RESULT.md`). |
| *merge/3 IL spike* | (pre-done) | B | ✅ **DONE** (ED-5) | `spike/p5-il-merge/` (root) | PASSED — see SPIKE-RESULT.md. |
| **P1 — realignment (original)** | (head-start) | B | ⛔ **SUPERSEDED** | `P1/` (`P1-dispositions.json`, `P1-synthesis.md`, `P1-critique.md`, …) | FAILED its own bar — single-pass triage against a self-synthesized "fastest-path" rubric produced confident, ungrounded "drop" verdicts on ANTLR/IL/separation. **Replaced by P1b (T006).** Read for *why it failed*, not for conclusions. |
| **P4 — faithfulness-proofs** | T004, T005 | B | ⬜ PENDING (runs FIRST in Phase B) | `P4-faithfulness/PARITY-BAR.md` + `P4-faithfulness/PROOFS/` | 100% M1/M2 criteria cite a primary source (page/`file:line`); every load-bearing invariant recorded proved/refuted/open, none silently skipped (FR-003/FR-004). |
| **P1b — corrected realignment** | T006 | B | ⬜ PENDING | `P1b-realignment/DISPOSITIONS.md` | Every disposition cited; **no fastest-path rubric**; the ANTLR/IL/separation cluster judged on the verified architecture (ED-1…ED-6) + the P4 bar, not pre-dropped. *Depends T004–T005.* |
| **ANTLR-integration deep-dive** | T008 | B | ⬜ PENDING | `ANTLR-integration/DOSSIER.md` | ≥1 integration option **built/run**, extending `spike/p5-il-merge/` (FR-005). |
| **P6 — Gleam/AtomVM impl-strategy** | T009 | B | ⬜ PENDING | `P6-gleam-impl/DOSSIER.md` | Each material claim cited; covers GLP→BEAM concurrency mapping, heap model, persistence, AtomVM constraints (no `gleam_otp`) (FR-006). |
| **P7 — QHSM/YngeniOS integration** | T010 | B | ⬜ PENDING | `P7-qhsm-yngenios/DOSSIER.md` | Concrete packaging design citing the sibling repos; ⚠️ mind the `MSTACK/docs/diana` gap + `qhstate-Yngenios` stub (CORPUS-INDEX §H) — mark provisional where grounding is missing (FR-007). |
| **P2 — concerns register** | T011 | B | ⬜ PENDING | `P2-concerns/REGISTER.md` | Each item has evidence + severity + affected features; discovery ran **loop-until-dry** (FR-002). |
| **P3 — opportunities register** | T012 | B | ⬜ PENDING | `P3-opportunities/REGISTER.md` | Each item names the BEAM/AtomVM capability + what it lets the design delete/simplify, with evidence (FR-002). |
| **P8 — synthesis** | T007 | B | ⬜ PENDING | `P8-synthesis/RECONFIGURATION.md` | Two epics; Full-Gleam fully scored + **valid topological order** (no forward dep), each feature tied to ≥1 P4 criterion + the ED-6 obligations; genuine forks as owner options; + advisory migration mapping (FR-008/FR-009). *Depends T004–T006 AND T008–T012.* |

## Phase-B execution order (from `tasks.md` Dependencies + `quickstart.md`)

```
P4 (T004→T005)                         ← foundational; runs first (the parity bar everything cites)
  └─► P1b realignment (T006)
        ┊  (in parallel, independent, distinct output files:)
        ├─ ANTLR deep-dive (T008)
        ├─ P6 Gleam/AtomVM   (T009)
        ├─ P7 QHSM/YngeniOS  (T010)
        ├─ P2 concerns       (T011)
        └─ P3 opportunities  (T012)
              └─► P8 synthesis (T007)   ← converges T004–T006 AND T008–T012
                    └─► T013 completeness-critic
                          └─► T014 DISCHARGE GATE (owner approval — the only mutation point)
                                └─► T015 [owner-approved only] epic migration via buildkit-roadmap
```

## Discharge gate (FR-011 — the only mutation)

Nothing on the live roadmap/specs/code (or any sibling repo) moves before **T014 owner approval** of
the P8 reconfiguration + advisory migration plan. Recorded as the marathon discharge item
`mdi-019f064b-880b-7413-9019-314bfc5bf4bf`. On approval only: create *Optional features* + *Full
Gleam implementation* and migrate the recombined features (T015) via `buildkit-roadmap`.

## Legend

✅ DONE · ⬜ PENDING · ⛔ SUPERSEDED · Phase A = build machinery · Phase B = run it.
