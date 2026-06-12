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

## Active now (2026-06-12)

- **Harness**: `030-marathon-refinement` (refines 024) — **shipped to main `v2026.06.12.1`**;
  Phase 8 polish in flight on branch `030-marathon-refinement`. The durable, restart-safe
  harness is `codeconv.marathon`, refined **data-driven**: registrable + growable per-run
  stage list, emergent-work intake (5-stage mini-pipeline), per-run isolated store OUTSIDE
  any repo (default `C:/pglite/marathon/<run-id>`; per-run PGLite cluster + JSON mirror,
  background keeper). Contracts: `specs/030-marathon-refinement/contracts/`. 024's
  shared-cluster `marathon` schema (Alembic `0010`) is inert history — never read or
  written (VIII).
- **How to locate position now**: after the roadmap→pipeline→tasks order above, run
  `codeconv/.venv/Scripts/python.exe -m codeconv.cli marathon resume --run <run-id>`
  (`--data-dir <store-root>` for a non-default store; `--feature` is a deprecated 024 alias)
  — the position derives from durable rows alone, never a summary; a store fork exits 2.
  See `/marathon-stage-harness`.
- **Marathon it unblocks**: epic `distributed-glp-connectivity` → `multi-protocol-link-layer`
  — driven end-to-end by the harness (in progress).

## History (do not resume these — they are done/parked)

- `023-glptutorial-run` **SHIPPED** `v2026.06.04.1` (merged PR #20).
- `020-trace-equivalence-fidelity` parked, branch local-only (not on the roadmap).

Once the marathon-stage-harness exists, it maintains the durable checkpoint that makes the
"where in the feature" answer automatic — this file stays a thin pointer.
