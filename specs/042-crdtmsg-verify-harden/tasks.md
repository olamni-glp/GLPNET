# Tasks: Verify + Harden F1/F2/F3 Against Their Own 3-Role Method Specs

**Input**: Design documents from `/specs/042-crdtmsg-verify-harden/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: Not requested — this is an adjudicative documentation feature; acceptance is the
SC-001..SC-009 checklist (T031), not code tests. The repo Test Protocol bracketing (baseline
green → docs-only change → re-green) is T003/T030.

**Organization**: Tasks grouped by user story (US1 P1 conformance, US2 P2 hardening,
US3 P3 PROVISIONAL closure, US4 P4 evidence record). Docs paths abbreviated:
`CORPUS/` = `docs/research/crdt-multiformat-messaging/`.

**Conventions binding every task**: every finding/record carries its baseline label
`DELIVERY(<commit>)` or `HEAD(<commit>)` (FR-015); every in-place doc edit gets exactly one
change-log row per `contracts/amendment-changelog.md` (FR-011/SC-008); contested decisions are
never self-ruled — they land in report §9 (FR-013); all verification labor is Claude agents
(Constitution V).

## Phase 1: Setup

- [X] T001 Create `CORPUS/evidence/` with `evidence-index.md` skeleton (columns per data-model.md EvidencePointer) and `CORPUS/verification-report-042.md` skeleton with all 12 section headers + baselines table per `contracts/verification-report.md`; add the `## Change log — 042 verification pass` skeleton section to each of the three corpus docs per `contracts/amendment-changelog.md`, each with row 1 logging its own addition (contract rule 4) — so all later tasks append rows to one existing section
- [X] T002 Resolve the F3 scanner-C pinned repo view (branch `037-virtual-3270-term`, post-040-implement — F3 provenance L4) to an exact commit via git history; record it in the report baselines table; record current-HEAD commit likewise
- [X] T003 [P] Baseline checkpoint per Test Protocol: `git log -1`, run `bash test/run_all_tests.sh`, confirm green, note result in report frontmatter (docs-only feature — this pins the pre-change baseline)

**Checkpoint**: report + evidence skeletons exist; all 5 baseline rows pinned.

---

## Phase 2: Foundational (blocks all user stories)

**Purpose**: FR-001 method reconstruction — US1 audits against it, US2's re-scans use its
family manifests, US3/US4 cite its element refs.

- [X] T004 [P] Reconstruct F1's frozen method (5 elements, RECORDED/RECONSTRUCTED per data-model.md MethodElement) from `CORPUS/priorart-sibling-scan.md` at `DELIVERY(c20317ce)` + `docs/research/three-role-agent-teams/METHOD-AND-DOGFOOD.md` as context; write report §1.1 (expect RECONSTRUCTED-heavy per research.md R2 — absent elements recorded as absent, not backfilled)
- [X] T005 [P] Reconstruct F2's frozen method likewise from `CORPUS/webresearch-corpus.md` at `DELIVERY(c20317ce)`; write report §1.2
- [X] T006 [P] Reconstruct F3's frozen method from `CORPUS/buildingblocks-synthesis.md` at `DELIVERY(6ecc975f)` (incl. authority order, corroboration rule, cycle/stop protocol as additional elements); note the claim-schema field list is external (research.md R2); write report §1.3

**Checkpoint**: report §1 complete — 15+ MethodElement rows, each RECORDED or RECONSTRUCTED with inference stated.

---

## Phase 3: User Story 1 — Method-conformance verification (Priority: P1) 🎯 MVP

**Goal**: per-deliverable conformance ledgers — every method element PASS/GAP/DEVIATION with
verbatim evidence (FR-002), deviations classified with affected claims (FR-003).

**Independent Test**: pick F3 — its ledger has one row per §1.3 element, zero empty-evidence
rows, every DEVIATION classified (spec US1 independent test; SC-001 arithmetic per ledger).

- [X] T007 [P] [US1] Audit F1 against report §1.1 elements at `DELIVERY(c20317ce)`; emit conformance ledger (finding ids CF-F1-*) per `contracts/conformance-ledger.md` into report §2.1
- [X] T008 [P] [US1] Audit F2 against §1.2 at `DELIVERY(c20317ce)`; ledger CF-F2-* into report §2.2 (expected GAP: no query manifest, undefined seminal/strong/ok tiers — research.md R2)
- [X] T009 [P] [US1] Audit F3 against §1.3 at `DELIVERY(6ecc975f)`; ledger CF-F3-* into report §2.3, adjudicating the planning-time anomalies as audit inputs: §8 names only 7 of 9 singletons; §8 family-count arithmetic vs actual §1 catalog row count (research.md R3.1/R3.2)
- [X] T010 [US1] Classify every DEVIATION across the three ledgers (harmless / weakens-a-claim / invalidates-a-claim) and enumerate affected downstream claims/blocks per FR-003; record execution-record GAP dispositions (what missing, where said to live, disposition chosen — e.g. FR-014 targeted re-execution, in-doc-summary baseline) in the ledger rows
- [X] T011 [US1] Verify SC-001 arithmetic (totals line per ledger: elements = PASS+GAP+DEVIATION, zero omitted) and write the per-ledger totals lines in report §2

**Checkpoint**: SC-001 met — 3 complete conformance ledgers; the verification contract itself is discharged.

---

## Phase 4: User Story 2 — Harden merged decisions (Priority: P2)

**Goal**: singletons re-adjudicated, coverage ledgers re-derived, drift dispositioned,
E1–E9 propagation consistent (FR-004..FR-007).

**Independent Test**: the 9 singletons alone — each has confirmed/demoted/promoted verdict with
independent-family evidence or an explicit no-further-evidence ruling (spec US2 independent test).

- [X] T012 [US2] Derive the authoritative 9-block single-family survivor list from F3 §1 Sources column at `DELIVERY(6ecc975f)` (only 7 named in §8 — research.md R3.1); reconcile against §8's "14/17/9" family histogram and the actual §1 row count; write the derivation into report §3 (discrepancies here are CF-F3 DEVIATION cross-refs from T009)
- [X] T013 [P] [US2] Re-derive F1 §12 signal×repo matrix from its sources at `DELIVERY(c20317ce)`; REPRODUCED-EXACTLY or full discrepancy enumeration (direction-labeled: missed-coverage vs overclaimed) into report §4.1; corrections applied to `CORPUS/priorart-sibling-scan.md` + change-log rows
- [X] T014 [P] [US2] Re-derive F2 §11 gap-coverage table (9 gaps × verdicts) at `DELIVERY(c20317ce)` likewise into report §4.2; corrections to `CORPUS/webresearch-corpus.md` + change-log rows
- [X] T015 [P] [US2] Re-derive F3 §3 constraint matrix + §4 closure ledger (28/28 claim) from S1–S8/gap1–9/OC-1..4/C1–7 sources at `DELIVERY(6ecc975f)` into report §4.3/§4.4; corrections to `CORPUS/buildingblocks-synthesis.md` + change-log rows (SC-004)
- [ ] T016 [US2] Re-derive F3 merge decisions for all multi-family corroborated blocks from in-doc data only (§1 sources + §2 catalog + §4 ledger — FR-014 in-doc baseline); write `CORPUS/evidence/f3-merge-rederivation.md`; contested/discrepant blocks found here join the T017 re-scan queue
- [ ] T017 [US2] Execute blind family-targeted re-scans per research.md R4 protocol: for each of the 9 singletons (+ any T016 contested), spawn one blind Claude scanner agent per non-corroborating family (given family manifest + claim TOPIC only, never F3's verdict); write per-block records to `CORPUS/evidence/f3-rescan-<block-id>.md`
- [ ] T018 [US2] Curate the re-scan results into 9(+) SingletonAdjudication verdicts (confirmed/demoted/promoted/no-further-evidence, evidence-quoted, `HEAD` baseline) in report §3; genuine conflicts → report §9 escalation, never self-ruled; apply status changes to F3 §1/§2 rows + change-log rows (SC-002)
- [X] T019 [P] [US2] Disposition the 4 scanner-C drift items (F3 §8: mesh routes JSON-only; payloadType constants duplicated; spec-vs-plan store naming; 037 @name promise) against `HEAD`: corrected-in-corpus / roadmap-follow-up / obsolete, evidence-checked; report §5 + any doc corrections + change-log rows (SC-005)
- [X] T020 [P] [US2] Sweep E1–E9 propagation: for each ruling's touched blocks (F3 §6), check every appearance across all three docs for post-ruling status consistency (incl. §6's "ruled register supersedes §1 ESC bins" note L212); correct inconsistencies + change-log rows; report §6 with final count = 0 (SC-006)

**Checkpoint**: SC-002/004/005/006 met — a block's stated status is trustworthy at face value.

---

## Phase 5: User Story 3 — Close the PROVISIONAL register (Priority: P3)

**Goal**: 8 register rows adjudicated vs current reality; met triggers closed with evidence,
rest re-affirmed; nothing stale (FR-008/FR-009).

**Independent Test**: adjudicate the register alone — zero rows with already-met triggers still
marked PROVISIONAL (spec US3 independent test; SC-003).

- [ ] T021 [US3] Gather current-HEAD trigger evidence for the 8 rows (F3 §5) per research.md R5: 041 shipped surfaces (`csharp/glp_crdtmsg*`, `specs/041-crdtmsg-mvp/`, tag `v2026.07.04.4`) for BB-ENC-7 (also E3-promoted), BB-CRDT-7 (rich-text Fugue/Peritext), BB-VER-5 (check whether 041 shipped any restructuring migration), BB-SCH-3 (runtime-codec count); repo state for BB-ENC-8, BB-SIG-4, BB-CRDT-10, BB-RTE-4 — quoted artifact refs per row
- [ ] T022 [US3] Adjudicate all 8 rows: mechanically-met → self-promote (block status updated, register row resolved, evidence quoted); not-met → re-affirm with trigger restated against current reality (corrected wording where stale/ambiguous); ambiguous/judgment → escalate; apply edits to F3 §5 (+§1/§2 status echoes) + change-log rows
- [ ] T023 [US3] Write report §7 with the two mandatory batch lists: §7a promotions-for-owner-review (every self-promoted row + evidence), §7b escalations; cross-check zero met-trigger rows remain PROV (SC-003)
- [ ] T024 [P] [US3] Capture closures needing net-new implementation as proposed roadmap follow-ups in report §10 (named, one paragraph, explicitly NOT implemented — FR-009); include any drift-item follow-ups from T019

**Checkpoint**: SC-003 met — register has zero already-met triggers still PROVISIONAL.

---

## Phase 6: User Story 4 — Self-contained evidence record (Priority: P4)

**Goal**: every evidence pointer resolves in-repo or carries an explicit availability
disposition (FR-010); the 86-claim transcript pointer superseded (FR-014).

**Independent Test**: enumerate all pointers; each has exactly one resolution/disposition —
zero dangling (spec US4 independent test; SC-007).

- [X] T025 [P] [US4] Enumerate every evidence pointer in the three docs (in-repo, sibling-repo, external-url, session-transcript, named-corpus classes per data-model.md) into `CORPUS/evidence/evidence-index.md` with EP-* ids (read-only — may start any time after T001)
- [ ] T026 [US4] Resolve/disposition every EP row: verify in-repo refs at `HEAD`; sibling-repo refs → host-blocked disposition (spec Assumptions); materialize recoverable artifacts into `CORPUS/evidence/`; the "session transcripts" 86-claim pointer (F3 §8 L237) → unrecoverable disposition (what was lost, what survives in-doc, confidence impact) superseded by T016/T017 re-execution records; update doc pointer text + change-log rows
- [ ] T027 [US4] F2 bibliographic re-verification per research.md R6: Tier 1 — every load-bearing paper in F2 §11 verified (title/authors/venue/URL resolve); Tier 2 — best-effort existence sweep of remaining ~148 entries (incl. the 2026-dated arXiv ids, R3.5); link-rot recorded as disposition, never silently fixed; results into evidence-index + report §8 + any F2 corrections with change-log rows
- [ ] T028 [US4] Write report §8 census totals (class × resolution) and verify SC-007: 100% of pointers resolved or dispositioned, zero dangling transcript pointers

**Checkpoint**: SC-007 met — corpus stands on durable evidence.

---

## Phase 7: Polish & Report Assembly

- [ ] T029 Assemble the report: §9 consolidated owner escalations (incl. any hardened-verdict-vs-041 contradictions per FR-013), §11 amendment index (per-doc change-log counts, every finding_id resolves into §1–§8), §12 SC-001..SC-009 checklist with measured values; add the `verification-report-042.md` reference to all three docs (+ change-log rows — SC-009)
- [ ] T030 Self-check SC-008: sample min(10, all) change-log rows across the three docs, verify each maps 1:1 to an actual applied edit and cites a report finding id; verify zero un-logged diffs by comparing `git diff` of the three docs against the union of change-log rows; then re-run `bash test/run_all_tests.sh` (Test Protocol re-test bracket)
- [ ] T031 Final commit of all pass artifacts by name (3 hardened docs, report, evidence/, spec-dir updates) per commit-scope discipline; verify report §12 shows all nine SC rows PASS (or the enumerated-and-corrected alternative for SC-004)

---

## Dependencies & Execution Order

- **Phase 1 → Phase 2 → user stories**: T004–T006 (method reconstruction) block US1; T001/T002 block everything (report/evidence targets + baselines).
- **US1 (P1)**: T007–T009 [P] after T004–T006; T010 after T007–T009; T011 after T010.
- **US2 (P2)**: T012 after T009 (anomaly adjudication feeds the derivation); T013–T015 [P] anytime after Phase 2; T016 after T012; T017 after T012+T016; T018 after T017; T019/T020 [P] anytime after Phase 2.
- **US3 (P3)**: T021 → T022 → T023; T024 after T019+T022. US3 is independent of US2's re-scans but T022's F3 §5 edits must not race T018/T020 edits to the same file — serialize F3 writes (see Notes).
- **US4 (P4)**: T025 [P] can start right after T001 (read-only); T026 after T016+T017 (needs the re-execution records to supersede the transcript pointer) and T025; T027 [P] after T025; T028 after T026+T027.
- **Polish**: T029 after all stories; T030 after T029; T031 last.

### Parallel Opportunities

- T004/T005/T006 — three method reconstructions (different report subsections).
- T007/T008/T009 — three conformance audits (different ledgers).
- T013/T014/T015 — three ledger re-derivations (different docs).
- T017's per-block blind scans — up to 9(+) scanner agents concurrently (independent evidence files).
- T019, T020, T025 run alongside other US2 work; T027 alongside T026's non-F2 rows.

## Parallel Example: Phase 2 + early US4

```text
Agent A: T004 reconstruct F1 method     Agent D: T025 pointer census (read-only)
Agent B: T005 reconstruct F2 method
Agent C: T006 reconstruct F3 method
```

## Implementation Strategy

**MVP = US1** (the verification contract): Phases 1–3 alone deliver the first objective
statement of whether each deliverable implements its own method (SC-001). Then US2 (retires
the delivery-flagged weak points), US3 (register closure), US4 (evidence durability) —
each independently checkpointed. If effort must be cut, cut from the tail (US4 materialization
breadth), never from US1 evidence discipline.

## Notes

- **Single-writer per document**: T013/T018/T020/T022/T026 all edit `buildingblocks-synthesis.md` — apply edits serially (one open editing task at a time per doc) even where analysis ran in parallel; change-log rows keep the audit trail.
- All Claude-agent fan-out (T007–T009, T017) returns structured records; the curator/main session writes the docs — agents never edit the corpus directly.
- Commit after each phase checkpoint (files by name, single-line messages).
- Never touch `csharp/`, `glp_runtime/`, `codeconv/`, `programs/` (FR-009/FR-013).
