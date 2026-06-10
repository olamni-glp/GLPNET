---
description: "Task list for Evidence-Based Constitution"
---

# Tasks: Evidence-Based Constitution

**Input**: Design documents from `/specs/028-evidence-based-constitution/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/constitution-structure.md, quickstart.md

**Tests**: No automated test tasks. The spec does not request a test harness (FR-015 forbids one); validation is a one-time negative-control demonstration (FR-016) + a before/after `/buildkit-analyze` baseline (FR-017), captured as evidence.

**Organization**: Grouped by user story. ⚠️ Ordering is load-bearing and inverts the usual MVP order: US1's deliverable (the populated constitution) is *gated behind* US3 grounding and the US2 owner walkthrough — nothing is written to `constitution.md` until every principle is approved (FR-013).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no dependencies)
- File paths are relative to repo root `D:\BSTDEV\research\GLP\GLPNET\`

---

## Phase 1: Setup

- [X] T001 Create `specs/028-evidence-based-constitution/evidence/` directory for captured-evidence notes (analyze-before.md, analyze-after.md, negative-control.md).

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The fresh grounding scan — every principle's Evidence must be re-verified on disk before any walkthrough or write. **No user story write can begin until this completes.** (This is US3 work, but it blocks US1/US2, so it runs in the foundational phase.)

- [X] T002 [US3] Re-verify each candidate Evidence anchor on disk *now* (read-only, Claude-only — FR-011, FR-012): `docs/DISCIPLINE.md` §1.1/§1.2/§1.4/§1.8/§1.12/§1.13/§1.14; `CLAUDE.md` Spec-First / SRSW-`skipSRSW` / Language-Authority / Preserve-Working-Code / Test-Protocol / Single-source-of-truth; `codeconv/tests/test_migration_*_single_head.py` (incl. `_0010_`) + head `0010_marathon_schema.py`; `specs/012-codeconv-runner/contracts/bridge_lifecycle.md`. Record resolved/dropped per anchor.
- [X] T003 [US3] For any anchor that fails to resolve, drop the Evidence line (never fabricate — FR-011) and either re-ground the principle on a located artifact or mark it unsupported for the walkthrough.
- [X] T004 [US3] Freeze the candidate principle set and its numbering (default 8; numerals III/IV/V/VI stable — FR-007) and assign each a gate-ability label per research Decision 2; verify no label overstates determinism.

**Checkpoint**: Every candidate principle now has a resolved (or explicitly dropped) Evidence anchor + label. Grounding complete.

---

## Phase 3: User Story 1 (P1) — The Constitution Check becomes a real gate 🎯 MVP

**Goal**: Replace the placeholder template with the frozen, MUST-bearing constitution so `/buildkit-analyze` extracts real principles. **Depends on US3 (Phase 2) grounding AND the US2 walkthrough (Phase 4) approval — the write task T008 must not run before T011.**

**Independent Test**: before/after `/buildkit-analyze` pair on feature 027 (or 026) — before=0 MUSTs, after≥6 MUSTs.

- [X] T005 [US1] Capture the **before** baseline: run `/buildkit-analyze` against feature 027 (owner may choose 026) with the constitution still the pristine template; save the Constitution-Check section verbatim to `specs/028-evidence-based-constitution/evidence/analyze-before.md`. Expected: 0 MUSTs / vacuous pass (FR-017, SC-001).
- [X] T006 [US1] Draft the full constitution content **in working memory only** (not on disk) per `contracts/constitution-structure.md`: each approved principle with normative MUST/SHOULD, resolved Evidence, buildkit-analog-or-omitted, one gate-ability label; III/V/VI-a worded as analyze-LM scan instructions scoped to artifacts-under-review with the self-mention boundary (FR-004, FR-005); VII + VIII roadmap-clause = advisory (FR-006); Governance section + non-elevation note for DISCIPLINE §1.12/§1.13 (FR-009, FR-010); semantic `Version: 1.0.0` + `Ratified`/`Last Amended` 2026-06-10 (FR-008).

> ⛔ GATE: T008 (the write) is blocked until Phase 4 (US2 walkthrough) approval is complete — see T011.

- [X] T007 [US1] Freeze the approved principle count (6–8); if approvals would drop below the floor of 6, surface it rather than write (FR-001; US2 sc.4).
- [X] T008 [US1] Overwrite `.specify/memory/constitution.md` in place with the approved frozen set (single atomic write, no partial pre-approval write — FR-013).
- [X] T009 [US1] Capture the **after** baseline: re-run `/buildkit-analyze` on the same feature; save to `specs/028-evidence-based-constitution/evidence/analyze-after.md`. Expected: ≥6 MUSTs extracted + reasoned (FR-017, SC-001). Confirm the constitution's own token mentions did not self-flag (SC-005).
- [X] T010 [US1] Negative-control demonstration (FR-016, SC-002): in a throwaway artifact-under-review, plant a `skipSRSW` fragment and (separately) an `OPENAI_API_KEY` fragment; confirm each is flagged CRITICAL (III / V); record to `specs/028-evidence-based-constitution/evidence/negative-control.md`. Do not commit a recurring test.

**Checkpoint**: Gate is real — before/after pair + negative control captured.

---

## Phase 4: User Story 2 (P1) — Per-principle owner walkthrough before any write

**Goal**: Obtain explicit per-principle owner approval before the file is written. **Runs after T006 draft, before T008 write.**

**Independent Test**: no write to `constitution.md` occurs until every principle is individually presented and approved; a rejected principle does not appear in the file.

- [X] T011 [US2] Walk Gabi through every candidate principle one at a time — normative statement + resolved Evidence line + buildkit analog + gate-ability label — and record approve / edit / reject per principle (FR-013, US2 sc.1/2). If an edit removes a literal scan token, downgrade that principle's gate-ability label accordingly (Edge Case). Confirm running count vs the floor of 6 (US2 sc.4). **This task unblocks T007/T008.**

**Checkpoint**: Approved set frozen; write may proceed.

---

## Phase 5: User Story 3 (P2) — Evidence grounded, freshly verified, dropped if absent

**Goal**: Guarantee every written Evidence line resolves on disk. Core grounding is in Phase 2 (T002–T004, blocking); this phase is the final audit on the *written* file.

**Independent Test**: every Evidence line in the written file resolves to an existing file + anchor; a planted wrong anchor is dropped.

- [X] T012 [US3] Audit the written `constitution.md`: confirm 100% of Evidence lines resolve on disk; 0 fabricated/unresolved (SC-003). Spot-check by planting one deliberately-wrong anchor in a scratch candidate and confirming the scan would drop it (US3 sc.2) — recorded, not committed.

---

## Phase 6: Polish & Scope Verification

- [X] T013 Verify scope (FR-018, SC-006): `git diff --stat` confined to `.specify/memory/constitution.md` + `specs/028-evidence-based-constitution/**`; confirm no GLP runtime/`.glp`/language-definition file touched, `/buildkit-analyze` skill unmodified, no grep harness added.
- [X] T014 Verify governance gates (SC-007): no pipeline command auto-invoked by the feature; no write to `constitution.md` occurred before full owner approval.
- [ ] T015 Commit by name only (`.specify/memory/constitution.md` + the feature spec-dir artifacts) per VII; then ship via buildkit GitFlow.

---

## Dependencies & Execution Order

- **Phase 1 (Setup)** → **Phase 2 (Foundational grounding, T002–T004)**: blocks everything.
- **T005** (before-baseline) must run while the file is still the template — i.e. before T008.
- **T006** (draft) after T004; **T011** (walkthrough, Phase 4) after T006.
- ⛔ **T007/T008** (freeze + write) blocked until **T011** approval complete (FR-013).
- **T009/T010** (after-baseline + negative control) after T008.
- **T012** (written-file audit) after T008.
- **T013/T014/T015** (scope/gates/ship) last.

### Critical path

T001 → T002 → T003 → T004 → T005 → T006 → **T011 (owner gate)** → T007 → T008 → T009 → T010 → T012 → T013 → T014 → T015

### Parallel opportunities

- T002 anchor re-verifications are independent reads (logically [P] within T002).
- T009 and T012 can proceed in parallel after T008 (different evidence outputs).

---

## Notes

- The owner-walkthrough gate (T011) is the safety mechanism that makes freezing acceptable — **never** write before it (FR-013, memory: safety-ask-first).
- Evidence notes ARE committed (under `evidence/`); the negative-control is a one-time demonstration, NOT a recurring harness (FR-015/FR-016).
- This is governance documentation only — no GLP code, `.glp`, or language-definition changes (FR-018).
