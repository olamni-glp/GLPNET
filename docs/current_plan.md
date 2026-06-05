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

- **Roadmap feature**: `024-marathon-stage-harness` — **implemented** on branch
  `024-marathon-stage-harness` (spec dir `specs/024-marathon-stage-harness/`). The durable,
  restart-safe harness now lives in `codeconv.marathon` (PGLite schema `marathon` via Alembic
  `0010` + JSON fallback). It is the durable checkpoint that makes the "where in the feature"
  answer automatic.
- **How to locate position now**: after the roadmap→pipeline→tasks order above, run
  `codeconv/.venv/Scripts/python.exe -m codeconv.cli --data-dir C:/pglite/research/glpnet marathon resume --feature <slug>`
  — the max(sequence_no) checkpoint, never a summary. See `/marathon-stage-harness`.
- **Marathon it unblocks**: epic `distributed-glp-connectivity` → `multi-protocol-link-layer`
  (state `captured`, `blocked-by: marathon-stage-harness`) — now drivable end-to-end by the harness.

## History (do not resume these — they are done/parked)

- `023-glptutorial-run` **SHIPPED** `v2026.06.04.1` (merged PR #20).
- `020-trace-equivalence-fidelity` parked, branch local-only (not on the roadmap).

Once the marathon-stage-harness exists, it maintains the durable checkpoint that makes the
"where in the feature" answer automatic — this file stays a thin pointer.
