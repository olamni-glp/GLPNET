---
name: marathon-stage-harness
description: Drive one long feature (the "marathon") across many sessions with durable, restart-safe checkpoints, a per-stage approval gate, budget-bounded auto-mode, and preauthorized per-block scoped commit/push. Use when the user types `/marathon-stage-harness`, asks to start/resume a marathon, or asks where a multi-session feature stands. Roots the Restart-Resume protocol in CLAUDE.md.
---

# /marathon-stage-harness

Orchestration glue for the durable stage harness (feature 030 `marathon-refinement`,
refining 024). The durable state lives in `codeconv.marathon` over a **per-run
isolated store outside any repo** — default `C:/pglite/marathon/<run-id>`: a
per-run PGLite cluster at `<store>/pgdb` + a JSON mirror, owned by a background
**keeper** (024's shared-cluster `marathon` schema is inert history). The stage
list is **data-driven**: registered per run, growable mid-flight, with emergent-
work intake expanding a 5-stage mini-pipeline. Contracts:
`specs/030-marathon-refinement/contracts/`. Compose, don't reinvent: the Claude
Code **Workflow tool** supplies fan-out, per-agent journals, `resumeFromRunId`
cached-prefix resume, and `budget.spent()/remaining()`; the harness adds only
cross-session durable checkpointing, the gate, and the dual-store reconciliation.

All commands run through the codeconv venv:
`codeconv/.venv/Scripts/python.exe -m codeconv.cli marathon <sub> --run <run-id>`.
`--run <id>` is canonical (`--feature` is a deprecated 024 alias). The per-run
store defaults under `C:/pglite/marathon/<run-id>`; pass a global
`--data-dir <store-root>` for an explicit store. Exit codes: 0 OK · 2 escalation
(blocked, not an error) · 64 usage/filesystem guard · 70 internal.

## Restart-Resume order (MUST match CLAUDE.md verbatim)

On every session start / after compaction / after a crash, locate position
**objectively from durable state — never a conversation summary**:

1. **Roadmap** — `buildkit-roadmap next` → what feature + stage.
2. **Buildkit pipeline state** (DBOS + PGLite) → where in the feature.
3. **spec/plan/tasks** → the WIP unit.
4. Then `marathon resume --run <id>` → the four-field position (`done/total`,
   `next_action`, outstanding issues, budget) derived from durable rows alone
   (byte-identical with full context or after total context loss — SC-008).
   Resume **reconciles first**; on a store fork it exits `2` + escalates
   (never a silent pick). If `next_action` is `re-drive scoped commit for
   <stage>`, a crash landed between a block's final checkpoint and its scoped
   commit — re-drive that ONE commit before any new work.

## Stages are data-driven (FR-001/002)

Register the run with its own ordered stage list; grow it mid-flight:

- `marathon register --run <id> --stages specify,clarify,plan_task_analyze,implement,review`
  (any list fits the workload — the buildkit cadence above is the convention,
  not a schema).
- `marathon append-stage <name> --run <id>` when work emerges that deserves a
  full stage (origin `dynamic`; the total grows, FR-003).
- `marathon capture --run <id> --kind <latent-requirement|issue|bug|missing-prerequisite>
  --title <t> [--blocks <stage>]` for emergent work: expands the 5-stage
  mini-pipeline (mini-specify…mini-analyze) for the item; a
  `missing-prerequisite --blocks <stage>` places its mini-stages strictly
  BEFORE the blocked stage via fractional order keys (FR-010). Never
  auto-advances (default-deny).

The stage is the checkpoint boundary AND the scoped-commit boundary — they
never drift.

## Per-stage hook protocol

For each stage, in order:

1. **Locate** — `marathon resume --run <id>` (objective; never a summary).
   Honor a `re-drive scoped commit for <stage>` next-action first (rule 2a).
2. **Gate** — for a mutating stage, `marathon gate --run <id> --stage <name>
   --plan <ref>` presents the plan and blocks for approval; a recorded
   `approve` short-circuits on resume (no re-ask). Record with
   `marathon gate --run <id> --stage <name> --approve --by gabi`.
3. **Run** — `marathon stage-start <name> --run <id>`, then execute the stage
   as **one** Workflow run (`Workflow({script})`); checkpoints accumulate as
   sub-steps complete. On a failed-subagent re-run, pass the
   `workflow_run_id` that `marathon rerun` echoes as the Workflow
   `resumeFromRunId`, so only the failed unit re-executes.
4. **Status** — `marathon status --run <id> --emit` at every boundary (the
   ~5-min cadence driver): done / issues / tokens / to-do, one parseable line.
5. **Checkpoint + scoped commit** — `marathon checkpoint <name> --run <id>
   [--completed <json>] [--remaining <json>] [--paths a,b] [--budget n] [-m msg]`;
   `--remaining []` completes the stage. Under the standing grant the harness
   commits **only the named paths** (never `git add -A`, never force-push,
   never bypass hooks); a blocked push escalates `push_blocked` (exit 2).
6. **Keeper hygiene** — the first store touch auto-starts the keeper;
   `marathon keeper stop --run <id>` flushes at session end so the next start
   needs no recovery; after an abrupt kill, the next op auto-recovers stale
   residue (a LIVE second writer is refused as `concurrent_writer` — distinct
   from stale residue, exit 2).

## Auto-mode: exactly two block-points

Inside an approved stage everything proceeds automatically (subagent fan-out,
retryable re-runs, checkpoint writes, status emission, and — under the standing
grants — Workflow runs and per-block scoped commit/push). The harness blocks
for Gabi at **only**: (a) the per-stage plan-approval gate; (b) escalations
(`store fork`, `push_blocked`, `budget_exceeded`, `prereq_against_completed_stage`,
`concurrent_writer`). On either while unattended → durably checkpoint and wait
(exit `2`); never auto-approve, never auto-resolve.

## Standing preauthorizations (the only two)

Recorded on the run row (library: `Repository.update_run(run_id,
preauth_commit_push=True, preauth_workflow_optin=True)`), both revocable
(`preauth_revoked_at`). Neither relaxes the gate or any escalation. Without
the commit grant, named paths without a sha are informational — no re-drive.

## Commands (contracts/cli.md — 1:1 library parity, FR-025)

| Command | Effect |
|---|---|
| `marathon register --run <id> [--stages a,b,c] [--title] [--budget n] [--budget-unit]` | Create/re-attach the run + its ordered stage list (idempotent). |
| `marathon append-stage <name> --run <id>` | Grow the stage list mid-flight (origin `dynamic`). |
| `marathon stage-start <name> --run <id>` | Flip pending→running (started ≠ done, FR-004). |
| `marathon checkpoint <name> --run <id> [--completed] [--remaining] [--wip] [--paths] [--budget] [--issues] [-m]` | Durable checkpoint; `--remaining []` completes the stage + drives the scoped commit. |
| `marathon capture --run <id> --kind <k> --title <t> [--blocks <stage>]` | Emergent-work intake → 5-stage mini-pipeline (FR-005/006/010). |
| `marathon resume --run <id>` / `marathon position --run <id>` | Objective four-field position from durable rows; fork → exit 2. |
| `marathon status --run <id> [--emit]` | The parseable status line; `--emit` persists the report. |
| `marathon gate --run <id> --stage <name> [--approve｜--change --plan <ref>] [--by]` | Present/record the per-stage approval (`--stage` takes the stage NAME). |
| `marathon rerun --run <id> --stage <name> [--subagent <label>] [--units <json>]` | Per-block / isolated per-subagent re-run; echoes `workflow_run_id` for `resumeFromRunId`. |
| `marathon trace --run <id> --subject <s> --input <json> [--score] (--accept｜--reject)` | Append a verification-trace record (append-only). |
| `marathon reconcile --run <id>` | PGLite ↔ JSON mirror: in_sync / fast-forward / fork (exit 2). |
| `marathon finalize --run <id>` | Finalize — only when every current stage is complete; a later append re-opens. |
| `marathon keeper start｜stop｜recover --run <id>` | Per-run store keeper lifecycle (publish endpoint / flush / clear stale residue). |
| `marathon doctor --run <id>` | Read-only health: endpoint, active store, last-seq, escalations, budget headroom. |

## Memory-chain rooting

This harness is the owner named in CLAUDE.md's *Multi-Stage Task Persistence &
Restart-Resume* section. The durable position lives in the per-run
`codeconv.marathon` store, NOT a hand-written restart prompt.
`docs/current_plan.md` is a thin pointer to the roadmap + pipeline state + this
harness.
