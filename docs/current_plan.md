# Restart pointer — NOT a work ledger

> This file is intentionally thin. Do **not** write a full multi-step plan here and
> "resume from the current step" — that mechanism drifted stale (it once pointed
> restarts at already-shipped work). The **roadmap + buildkit pipeline state** are the
> source of truth. See CLAUDE.md § *Multi-Stage Task Persistence & Restart-Resume*.

## How to locate yourself on any restart (fresh / post-compaction / post-crash)

1. **What feature / what stage?** → `buildkit-roadmap next` (or `buildkit-roadmap status`)
   — the active/next roadmap feature, its state, and the exact `/buildkit-specify` command.
2. **In progress?** → a feature with a spec dir (`.specify/feature.json` → `specs/<NNN>/`)
   has entered the pipeline.
3. **Where in the feature (WIP position)?** → the buildkit pipeline stage state
   (DBOS + PGLite, per-feature) + the feature's `spec.md`/`plan.md`/`tasks.md`.

## Active now (2026-06-05)

- **Roadmap feature**: `marathon-stage-harness` — state **promoted**, **not yet specified**
  (no spec dir → not yet in the pipeline). It is the durable, restart-safe workflow backing
  for long multi-stage features, and the hard prerequisite for the connectivity marathon.
- **Marathon it unblocks**: epic `distributed-glp-connectivity` → `multi-protocol-link-layer`
  (state `captured`, `blocked-by: marathon-stage-harness`).
- **Next action**: run
  `/buildkit-specify "Marathon stage harness — durable, restart-safe workflow backing for long multi-stage features"`
  (full brief: `python -m buildkit_cli.roadmap brief marathon-stage-harness`).

## History (do not resume these — they are done/parked)

- `023-glptutorial-run` **SHIPPED** `v2026.06.04.1` (merged PR #20).
- `020-trace-equivalence-fidelity` parked, branch local-only (not on the roadmap).

Once the marathon-stage-harness exists, it maintains the durable checkpoint that makes the
"where in the feature" answer automatic — this file stays a thin pointer.
