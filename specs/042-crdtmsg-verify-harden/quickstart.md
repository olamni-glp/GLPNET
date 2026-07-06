# Quickstart: 042-crdtmsg-verify-harden

## What this feature does

Audits + hardens the three `crdt-multiformat-messaging` research deliverables against their own
frozen 3-role methods (the specs, per owner ruling), then closes the known-weak points. Output:
three amended documents (in place, change-logged), a consolidated verification report, and a
materialized evidence directory. No production code is written or modified.

## Inputs (all in-repo)

- `docs/research/crdt-multiformat-messaging/{priorart-sibling-scan,webresearch-corpus,buildingblocks-synthesis}.md`
- Delivery baselines: `git show c20317ce:<path>` (F1/F2), `git show 6ecc975f:<path>` (F3)
- 041 ship evidence: tag `v2026.07.04.4`, `csharp/glp_crdtmsg*`, `specs/041-crdtmsg-mvp/`
- Method-formalism context: `docs/research/three-role-agent-teams/METHOD-AND-DOGFOOD.md`
- Ruling provenance: `.specify/codify/notes/cn-20260704T064008-c1de4c16.md`

## Execution order (tasks.md is authoritative)

1. **WP1** — reconstruct 3 frozen methods (RECORDED/RECONSTRUCTED) → 3 conformance ledgers.
2. **WP2** — re-derive 4 coverage artifacts (delivery baseline); derive the authoritative
   9-singleton list from F3 §1; blind family-targeted re-scans → 9 verdicts; disposition
   4 drift items; sweep E1–E9 propagation.
3. **WP3** — adjudicate 8 PROVISIONAL rows vs current HEAD; mechanical promotions
   batch-listed for owner review; ambiguous → escalate.
4. **WP4** — evidence-pointer census → `evidence/evidence-index.md`; materialize or
   disposition every pointer.
5. **WP5** — assemble `verification-report-042.md`; write per-doc change logs; add the
   report reference to each doc; check SC-001..SC-009.

## Verifying the outcome (SC checklist)

```powershell
# report exists and is referenced from all three docs (SC-009)
ls docs/research/crdt-multiformat-messaging/verification-report-042.md
Select-String -Path docs/research/crdt-multiformat-messaging/*.md -Pattern "verification-report-042"
# no dangling transcript pointers remain un-dispositioned (SC-007)
Select-String -Path docs/research/crdt-multiformat-messaging/*-*.md -Pattern "session transcripts"
```

Then read report §12 (success-criteria checklist) — every SC row must be PASS with its
measured value.

## Hard boundaries

- Never modify anything under `csharp/`, `glp_runtime/`, `codeconv/`, `programs/` (FR-009/FR-013).
- Never self-rule a contested decision — escalate in report §9.
- Every doc edit gets a change-log row (SC-008); zero silent edits.
