---
name: marathon-stage-harness
description: Drive one long buildkit feature (the "marathon") through the full pipeline across many sessions with durable, restart-safe checkpoints, a per-stage approval gate, budget-bounded auto-mode, and preauthorized per-block commit/push. Use when the user types `/marathon-stage-harness`, asks to start/resume a marathon, or asks where a multi-session feature stands. Roots the Restart-Resume protocol in CLAUDE.md.
---

# /marathon-stage-harness

Orchestration glue for the durable stage harness (feature 024). The durable
state lives in `codeconv.marathon` (PGLite schema `marathon` + JSON fallback);
this skill is the per-stage hook layer that maps each buildkit pipeline stage to
a **marathon block** and composes the Claude Code **Workflow tool** for
in-session execution. Compose, don't reinvent (FR-009): the Workflow tool
supplies fan-out, per-agent journals, `resumeFromRunId` cached-prefix resume,
and `budget.spent()/remaining()`; the harness adds only cross-session durable
checkpointing, the approval gate, and the JSON fallback.

All commands run through the codeconv venv with the canonical cluster:
`codeconv/.venv/Scripts/python.exe -m codeconv.cli --data-dir C:/pglite/research/glpnet marathon <sub>`.

## Restart-Resume order (MUST match CLAUDE.md verbatim)

On every session start / after compaction / after a crash, locate position
**objectively from durable state — never a conversation summary**:

1. **Roadmap** — `buildkit-roadmap next` → what feature + stage.
2. **Buildkit pipeline state** (DBOS + PGLite) → where in the feature.
3. **spec/plan/tasks** → the WIP unit.
4. Then `marathon resume --feature <slug>` → the **max(sequence_no)** checkpoint
   (the exact WIP unit, completed/remaining units, recorded approval). On store
   divergence it exits `2` + escalates (never a silent pick).

## Stage → block cadence (FR-019 / D9)

| buildkit stage | block_kind | blocks |
|---|---|---|
| `/buildkit-specify` | `specify` | 1, then **restart** |
| `/buildkit-clarify` | `clarify` | 1 |
| `/buildkit-plan` + `/buildkit-tasks` + `/buildkit-analyze` | `plan_task_analyze` | **1** (the three collapse) |
| `/buildkit-implement` | `implement_session` | a **series**, each session = 1 block |
| review | `review` | 1 |

The block is the checkpoint boundary AND the commit/push boundary — they never
drift.

## Per-stage hook protocol

For each stage, in order:

1. **Locate** — `marathon resume` (objective; never a summary). If the report
   has `commit_push_pending: true` (a crash landed between a completed block's
   final checkpoint and its commit/push), **re-drive the commit/push for that
   block first** — never read `block_complete` as "nothing left to do".
2. **Gate** — for a mutating block, `marathon gate --block <id>` presents the
   plan and blocks for approval; a recorded `approve` short-circuits on resume
   (no re-ask — SC-004). Record with `marathon gate --block <id> --approve --by gabi`.
3. **Run** — execute the stage as **one** Workflow run (`Workflow({script})`);
   record its `runId` as run-linkage; `marathon` checkpoints accumulate as
   sub-steps complete. On a failed-subagent re-run, pass the `workflow_run_id`
   that `marathon rerun` echoes as the Workflow `resumeFromRunId`, so only the
   failed unit re-executes (succeeded siblings return cached — FR-007/FR-009).
4. **Status** — `marathon status --emit` at each checkpoint (the ~5-min cadence
   driver): the standardized four fields — done / issues / tokens (spent +
   remaining) / to-do (SC-005).
5. **Checkpoint + commit/push** — at block completion, the final checkpoint then
   the preauthorized commit/push of **only this block's files** (never
   `git add -A`, never force-push, never bypass hooks); a blocked push escalates
   `push_blocked`. On resume, honor `marathon resume`'s `commit_push_pending`
   flag to re-drive a commit/push a crash interrupted after the final checkpoint
   (FR-014/SC-010 crash window).
6. **Boundary** — for `specify`, restart after the block (cadence).

## Auto-mode: exactly two block-points (FR-022 / D11)

Inside an approved block everything proceeds automatically (subagent fan-out,
retryable re-runs, checkpoint writes, status emission, and — under the standing
grants — Workflow runs and per-block commit/push). The harness blocks for Gabi
at **only**: (a) the per-stage plan-approval gate; (b) escalations
(`non_retryable_failure`, `store_divergence`, `push_blocked`, `stage_flagged`).
On either while unattended → durably checkpoint and wait (exit `2`); never
auto-approve, never auto-resolve.

## Standing preauthorizations (the only two — FR-023 / D10)

Granted once at `marathon start`, both revocable: (1) **commit + push per block**;
(2) the **Workflow-tool opt-in**. Neither relaxes the gate or any escalation.

## Commands

| Command | Effect |
|---|---|
| `marathon start --feature <slug> [--branch] [--budget] [--auto] [--preauth-commit-push] [--preauth-workflow]` | Create/re-attach the marathon + record the two grants (idempotent). |
| `marathon verify-spike` | FR-011: verify cached-prefix resume + budget observability, record a `verification_traces` row (run FIRST). |
| `marathon resume [--feature]` | Objective restart-safe resume from durable state. |
| `marathon gate --block <id> [--approve｜--change --plan <ref>] [--by]` | Present/record the approval decision. |
| `marathon status [--feature] [--emit]` | The standardized four-field report. |
| `marathon rerun --block <id> [--subagent <label>]` | Per-stage / per-subagent re-run on failure; echoes the block's `workflow_run_id` — pass it as the Workflow `resumeFromRunId` so succeeded siblings stay cached. |
| `marathon reconcile [--feature]` | Reconcile primary vs JSON fallback by sequence_no. |
| `marathon trace --subject <s> --input <json> [--score] (--accept｜--reject)` | Append a verification-trace record (substrate only). |
| `marathon doctor [--feature]` | Bridge reachability, active store, last seq per store, open escalations, budget headroom. |

## Memory-chain rooting (FR-018)

This harness is the owner named in CLAUDE.md's *Multi-Stage Task Persistence &
Restart-Resume* section. The durable position lives in `codeconv.marathon`, NOT a
hand-written restart prompt. `docs/current_plan.md` is a thin pointer to the
roadmap + pipeline state + this harness.
