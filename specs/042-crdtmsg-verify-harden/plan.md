# Implementation Plan: Verify + Harden F1/F2/F3 Against Their Own 3-Role Method Specs

**Branch**: `042-crdtmsg-verify-harden` | **Date**: 2026-07-04 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/042-crdtmsg-verify-harden/spec.md`

## Summary

Adjudicative documentation feature — no production code. Three research deliverables of the
`crdt-multiformat-messaging` epic (F1 `priorart-sibling-scan.md`, F2 `webresearch-corpus.md`,
F3 `buildingblocks-synthesis.md`) were produced by 3-role team pipelines whose frozen methods +
execution records ARE the specs (owner ruling, codify note `cn-20260704T064008-c1de4c16`). This
feature (a) reconstructs each frozen method from in-repo records marking RECORDED vs
RECONSTRUCTED elements, (b) audits each deliverable against every element of its own method
producing per-feature conformance ledgers, (c) hardens the known-weak points (9 single-family
survivor blocks, 3 coverage ledgers, 4 scanner-C drift items, E1–E9 ruling propagation),
(d) closes the 8-row PROVISIONAL register against current reality (041 MVP shipped), and
(e) makes the evidence record self-contained (every pointer resolves in-repo or carries a
disposition). All hardening edits land in the three documents in place with per-document change
logs; one consolidated verification report lands in the epic's research corpus. Verification
labor is executed by Claude agents reusing the 3-role formalism (blind re-scanners → curator →
owner escalation) for the targeted re-executions mandated by FR-014.

## Technical Context

**Language/Version**: Markdown (documentation corpus); PowerShell 5.1 + git for baseline
extraction (`git show <commit>:<path>`); Claude agents (Agent-tool seams) as the verification
workforce — no new runtime code.
**Primary Dependencies**: the three deliverables under `docs/research/crdt-multiformat-messaging/`;
`docs/research/three-role-agent-teams/METHOD-AND-DOGFOOD.md` (method-formalism context);
git history (delivery baselines); shipped 041 artifacts (`csharp/glp_crdtmsg*`,
`specs/041-crdtmsg-mvp/`, tag `v2026.07.04.4`) as PROVISIONAL-trigger evidence; web access for
F2 bibliographic spot-checks.
**Storage**: files only — hardened docs in place; verification report + materialized evidence
under `docs/research/crdt-multiformat-messaging/` (see Project Structure). No database writes;
no migrations; no PGLite schema changes.
**Testing**: acceptance = SC-001..SC-009 checked against the produced artifacts (ledger
completeness counts, register closure counts, pointer-resolution sweep, change-log sampling).
No code test suites are touched; the repo's suites stay green trivially (docs-only diff) but the
Test Protocol baseline/re-test bracketing is still observed.
**Target Platform**: this repo's documentation tree (Windows host); sibling-repo manifest rows
are host-blocked and recorded as such (spec Assumptions).
**Project Type**: documentation verification + hardening pass (adjudicative, not constructive).
**Performance Goals**: N/A (one-shot audit pass). Bounded effort: fresh blind re-scans ONLY for
the 9 single-family survivors + contested/discrepant claims (FR-014); F2 re-verification is
bibliographic spot-check depth, not a re-read of ~150 sources.
**Constraints**: zero silent edits (FR-011); no net-new implementation (FR-009 — roadmap
follow-ups instead); 041 code never modified (FR-013); owner escalation for judgment calls
(F3's zero-self-decision rule; FR-008/FR-013); every finding labeled with its baseline
(FR-015 hybrid ruling).
**Scale/Scope**: 3 documents (~309 + ~592 + ~238 lines); ~40–50 blocks in the F3 catalog;
9 singleton re-adjudications; 8 PROVISIONAL rows; 4 drift items; 9 rulings to propagate;
148 external sources (spot-check); 3 coverage ledgers to re-derive.

### Verification baselines (operationalizing the FR-005/FR-015 hybrid ruling)

| Baseline | Commit | Used for |
|---|---|---|
| F1/F2 delivery-time | `c20317ce` (2026-07-03) | F1/F2 method-conformance + F1 §12 / F2 §11 ledger re-derivation |
| F3 delivery-time | `ee94a04f` (initial delivery) → `6ecc975f` (final delivered state incl. E1–E9 encoding, 2026-07-04) | F3 method-conformance + §3/§4 ledger re-derivation; conformance judged against `6ecc975f` |
| F3 scanner-C repo view | branch `037-virtual-3270-term` post-040-implement (per F3 provenance L4) — resolve exact commit during the pass | re-deriving what scanner C could see |
| Current HEAD | branch `042-crdtmsg-verify-harden` HEAD at execution time | hardening, PROVISIONAL closure, drift dispositions, evidence materialization |
| 041 ship evidence | tag `v2026.07.04.4` = `0945c29a` | PROVISIONAL trigger adjudication (US3) |

## Constitution Check

*GATE: evaluated against constitution v1.1.0. Re-checked post-Phase-1: PASS.*

| Principle | Verdict | Rationale |
|---|---|---|
| I. Spec-First | PASS | The owner's ruling designates the frozen methods + execution records as the specs; this feature IS the spec-conformance check. Where a method element is under-recorded, the pass reconstructs and marks it RECONSTRUCTED (FR-001) rather than inventing authority. |
| II. Bug-Protocol / No-Workarounds | PASS | Findings are recorded + classified, never silently normalized (spec Edge Cases); contradictions with shipped 041 escalate to the owner, never patched (FR-013). No workaround surface exists — the pass changes documents only via logged amendments. |
| III. SRSW machine scan | PASS | No GLP code is written. Artifact scan: zero occurrences of the forbidden token in spec/plan/tasks. |
| IV-a. Language Authority | PASS | No GLP language surface touched. The E6 experimental GLP guard is 041/roadmap scope; this pass only verifies its recorded status propagation. |
| IV-b. Preserve Working Internals | PASS | No runtime code touched at all. |
| V. Claude-Only LM machine scan | PASS | All verification/re-scan labor runs as Claude agents via the Agent tool (3-role formalism). No external LM API on any path; artifact scan clean. |
| VI-a. Additive-only persistence | PASS | No migrations; no DB writes. |
| VI-b. Single PGLite cluster | PASS | No new cluster; no bridge use required. |
| VII. Test-gated, commit-scoped shipping | PASS | Docs-only diffs; suites baselined green before and re-run after (trivial); files staged by name; ship via buildkit GitFlow. |
| VIII. Single Source of Truth | PASS | Hardening lands in the three living documents in place (FR-011) — no parallel addendum docs; the verification report is a new artifact (the pass's own output), referenced from the three docs (SC-009), duplicating none of them. |

No violations → Complexity Tracking not required.

## Project Structure

### Documentation (this feature)

```text
specs/042-crdtmsg-verify-harden/
├── plan.md              # This file
├── research.md          # Phase 0: baselines, method-reconstruction sources, known anomalies, decisions
├── data-model.md        # Phase 1: verification-record entities (ledgers, verdicts, dispositions)
├── quickstart.md        # Phase 1: how to execute + how to consume the outputs
├── contracts/           # Phase 1: report + ledger + change-log format contracts
│   ├── verification-report.md
│   ├── conformance-ledger.md
│   └── amendment-changelog.md
└── tasks.md             # Phase 2 (/bk-tasks — NOT created by /bk-plan)
```

### Source Code (repository root)

```text
docs/research/crdt-multiformat-messaging/
├── priorart-sibling-scan.md        # F1 — hardened IN PLACE + change-log section (FR-011)
├── webresearch-corpus.md           # F2 — hardened IN PLACE + change-log section (FR-011)
├── buildingblocks-synthesis.md     # F3 — hardened IN PLACE + change-log section (FR-011)
├── verification-report-042.md      # NEW — the consolidated report (FR-012, SC-009)
└── evidence/                       # NEW — materialized evidence artifacts (FR-010, US4)
    ├── f3-rescan-<block-id>.md     #   fresh blind re-scan records for the 9 singletons (FR-014)
    ├── f3-merge-rederivation.md    #   re-derived merge decisions from in-doc data (FR-014)
    └── ...                         #   any other recoverable artifacts materialized in-repo
```

Nothing under `csharp/`, `glp_runtime/`, `codeconv/`, or `programs/` is modified (FR-009/FR-013).

**Structure Decision**: documentation-tree-only feature. The three deliverables remain the
epic's single source of truth and are amended in place; all NEW artifacts (report, evidence)
live beside them under the epic's research directory so downstream consumers
(`crdtmsg-xsd-style-schema-language`, `glp-policy-guard`, post-MVP features) find everything in
one directory. Pipeline artifacts stay in `specs/042-crdtmsg-verify-harden/`.

## Execution Design (how the pass runs)

Five work packages mapping 1:1 to the user stories + the report:

1. **WP1 Method reconstruction + conformance audit (US1, FR-001..003, FR-015)** — per
   deliverable: reconstruct the frozen method (5 elements each, RECORDED/RECONSTRUCTED),
   audit the shipped doc against each element at its delivery-time baseline, emit a
   conformance ledger (PASS/GAP/DEVIATION + verbatim evidence), classify deviations.
   F1/F2 have materially weaker method records than F3 (see research.md R2) — expect
   RECONSTRUCTED-heavy ledgers there; that is a recorded property, not a defect to hide.
2. **WP2 Hardening (US2, FR-004..007)** — re-derive the 3 coverage ledgers against
   delivery-time state; derive the authoritative 9-singleton list from F3 §1 sources column
   (only 7 are named in §8 — research.md R3), then run fresh blind re-scans per singleton
   (3-role: blind scanner agents per source family → curator → escalation); disposition the
   4 scanner-C drift items; sweep E1–E9 propagation across all three docs.
3. **WP3 PROVISIONAL closure (US3, FR-008/FR-009)** — adjudicate the 8 register rows against
   current HEAD + shipped 041; mechanically-met triggers self-promote with quoted evidence,
   batch-listed for owner review; ambiguous ones escalate; net-new-work closures become
   proposed roadmap follow-ups.
4. **WP4 Evidence materialization (US4, FR-010, FR-014)** — enumerate every evidence pointer
   in the three docs; resolve/materialize into `evidence/` or record availability
   disposition; the dangling "session transcripts" pointer for the 86-claim sets is replaced
   by the targeted re-execution records + an explicit unrecoverable-disposition note.
5. **WP5 Report + change logs (FR-011, FR-012, SC-008/009)** — every amendment from WP1–WP4
   lands via a per-document change-log entry; the consolidated report assembles the ledgers,
   verdicts, dispositions, and materialization outcomes, each finding baseline-labeled
   (FR-015); the three docs gain a reference to the report.

Ordering: WP1 → WP2 → WP3 → WP4 → WP5 for report assembly, but WP4's pointer enumeration can
start in parallel with WP1 (read-only sweep). All owner escalations are batched into the report
(plus the FR-008 promotion batch-list); nothing self-ruled.

## Complexity Tracking

No constitution violations — table not required.
