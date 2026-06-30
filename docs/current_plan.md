# Restart pointer — NOT a work ledger

> This file is intentionally thin. Do **not** write a full multi-step plan here and
> "resume from the current step" — that mechanism drifted stale (it once pointed
> restarts at already-shipped work). The **roadmap + buildkit pipeline / marathon state**
> are the source of truth. See CLAUDE.md § *Multi-Stage Task Persistence & Restart-Resume*.

## How to locate yourself on any restart (fresh / post-compaction / post-crash)

1. **What feature / what stage?** → `buildkit-roadmap next` (or `buildkit-roadmap status`).
2. **In progress?** → a feature with a spec dir (`.specify/feature.json` → `specs/<NNN>/`)
   has entered the pipeline; a `bk-marathon` run drives the bigger ones.
3. **Where in the feature (WIP position)?** → the marathon durable rows (objective) +
   the feature's `tasks.md`.

## Active now (2026-06-30)

- **PROGRAM `036-glp-gleam-baseline-program`** (branch `036-glp-gleam-baseline-program`, pushed to
  origin) — the GLP→Gleam/AtomVM research/verification/reconfiguration program, run under
  `/bk-marathon` (run **`mrun-5611c436ba95`**). **All work tasks T001–T013 are COMPLETE +
  checkpointed; the T014 discharge gate is APPROVED by Gabi 2026-06-30** (recorded: marathon
  trace #38 `accept` + discharge item `mdi-019f064b…` satisfied). **The ONLY remaining task is
  T015 — the single roadmap mutation — to run in this NEW session.**
- **Resume objectively:** `PYTHONUTF8=1 python -m buildkit_cli.marathon resume --run mrun-5611c436ba95`
  (or `… status` / `position`) → derives **done 13/14, discharge 1/1, next = T015** step
  `mstep-019f15dc-8687-7755-8408-e8a3ab28f794` (left UNSTARTED on purpose). Position comes from the
  durable rows, never this file.
- **T015 = migrate via `buildkit-roadmap`:** create epics **Optional features** + **Full Gleam
  implementation**; add the 24 features (15 full-gleam scored + topo-ordered incl. the PROPOSED-NEW
  M2-0 + 7 optional) per the **ADVISORY MIGRATION MAPPING** in
  `docs/research/glp-gleam-baseline/pipelines/P8-synthesis/RECONFIGURATION.md`; then
  `marathon discharge` to close the run. The 16 owner-decisions **D1–D16** (incl. language-authority
  gates **D6** `ground/1` SRSW relaxation + **D7** new self-prove predicate, and **D12** M2-seam
  trust boundary) are feature-level — resolve **after** the migration, not before.
- **Deliverables (all committed + pushed under `docs/research/glp-gleam-baseline/`):** `CORPUS-INDEX`,
  `PROOF-HARNESS`, `pipelines/INDEX`; `pipelines/P4-faithfulness/` (PARITY-BAR = 66 cited criteria +
  PROOFS = 3 proved/3 open), `P1b-realignment/DISPOSITIONS` (24 features), `ANTLR-integration/`,
  `P6-gleam-impl/`, `P7-qhsm-yngenios/`, `P2-concerns/` (218, non-saturated), `P3-opportunities/`
  (70, saturated), `P8-synthesis/` (RECONFIGURATION + COMPLETENESS).

## History (do not resume these — they are done/parked)

- `034-glp-gleam-core-terms-and-heap` (gleam-atomvm F4) **SHIPPED** `v2026.06.25.1`.
- `030-marathon-refinement` **SHIPPED** (`v2026.06.12.1` + Phase 8 `v2026.06.19.1`).
- `035-semantic-tombstone-enrichment` **SHIPPED** `v2026.06.26.1`.
