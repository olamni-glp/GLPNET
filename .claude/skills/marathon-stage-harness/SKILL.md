---
name: marathon-stage-harness
description: Drive one long feature (the "marathon") across many sessions with durable, restart-safe checkpoints, a per-stage approval gate, budget-bounded auto-mode, and preauthorized per-block scoped commit/push. Use when the user types `/marathon-stage-harness`, asks to start/resume a marathon, or asks where a multi-session feature stands. Roots the Restart-Resume protocol in CLAUDE.md.
---

# /marathon-stage-harness

Orchestration glue for the durable stage harness — **refined by feature 030**
(`specs/030-marathon-refinement/contracts/`). The harness is **workload-agnostic
and data-driven**: the stage list is data you register, not a fixed buildkit
vocabulary. Durable state lives in a **per-run isolated store OUTSIDE any repo**
(`<store_root>/pgdb` PGLite cluster + `<store_root>/json` mirror), owned by a
background **keeper**. The 024 shared-cluster schema (`marathon` via Alembic
`0010`) is **inert history** — per-run stores are provisioned by `ensure_schema`,
never by a shared migration. This skill maps work units to stages/checkpoints
and composes the Claude Code **Workflow tool** for in-session execution
(fan-out, per-agent journals, `resumeFromRunId` cached-prefix resume,
`budget.spent()/remaining()`); the harness adds cross-session durable
checkpointing, the approval gate, intake, and the dual-store mirror.

All commands run through the codeconv venv against the per-run store
(`PYTHONUTF8=1`; the store root must be off-repo NTFS — FR-027, guard exit 64):

```
codeconv/.venv/Scripts/python.exe -m codeconv.cli --data-dir D:/pglite/marathon/<run-id> marathon <sub> --run <run-id> ...
```

**Provisioning a new store root**: junction-link the repo's `prereq-patterns/`
into it so the keeper can spawn the per-run bridge:
`cmd /c mklink /J <store_root>\prereq-patterns <repo>\prereq-patterns`.

## Restart-Resume order (MUST match CLAUDE.md verbatim)

On every session start / after compaction / after a crash, locate position
**objectively from durable state — never a conversation summary**:

1. **Roadmap** — `buildkit-roadmap next` → what feature + stage.
2. **Buildkit pipeline state** (DBOS + PGLite) → where in the feature.
3. **spec/plan/tasks** → the WIP unit.
4. Then `marathon resume --run <run-id>` → the objective four-field position
   (done/total, outstanding issues, budget spent, the single `next_action`),
   derived solely from durable rows (SC-008). It reconciles first; a store
   fork exits `2` + escalates (never a silent pick). A complete stage whose
   last checkpoint has `commit_sha IS NULL` surfaces
   **"re-drive scoped commit for <stage>"** before any new work (FR-018).

## Stage model (data-driven — FR-001/002)

- `register --stages a,b,c` sets the initial ordered list; `append-stage` grows
  it mid-flight (the total grows; done/total is always reported against the
  **current** total).
- A buildkit feature typically registers its pipeline stages or its tasks
  (e.g. `T051,…,T058`) — the harness no longer hard-codes either vocabulary.
- The **stage** is the checkpoint boundary AND the scoped-commit boundary —
  they never drift. `checkpoint --remaining []` completes the stage.

## Emergent work (US2 — FR-005/006/009/010)

`capture --kind latent-requirement|issue|bug|missing-prerequisite --title …
[--blocks <stage>]` records the item and expands a 5-stage mini-pipeline
(`mini-specify…mini-analyze`; **no per-item implement**). A blocking
missing-prerequisite routes its mini-stages strictly BEFORE the blocked stage;
resume names the item's next mini-stage. Advisory / default-deny — the harness
never auto-advances. Item artifacts live in `<store_root>/items/<id>/`.

## Per-stage hook protocol

1. **Locate** — `marathon resume --run <id>` (objective; never a summary).
   Honor its `next_action` — especially the re-drive-commit guard.
2. **Gate** — for a mutating stage, `marathon gate --run <id> --stage <name>`
   presents the plan and blocks for approval; a recorded `approve`
   short-circuits on resume (no re-ask — FR-020). Record with
   `marathon gate --run <id> --stage <name> --approve --by gabi`.
3. **Run** — `stage-start`, then execute (optionally as **one** Workflow run;
   on a failed-subagent re-run pass the prior `runId` as `resumeFromRunId` so
   succeeded siblings return cached; `marathon rerun --stage <name>
   [--subagent <label>]` gives the isolated re-run picture — FR-021).
4. **Status** — `marathon status --run <id> [--emit]` at each checkpoint: the
   parseable four-field line (FR-019); `--emit` persists the report row.
5. **Checkpoint + scoped commit** — `checkpoint <stage> --remaining []
   --paths <only this block's files> -m "<msg>"`. With the standing grant the
   harness stages **exactly** the named paths and commits/pushes (hooks run,
   never `--force`); a rejected push escalates `push_blocked` (exit 2).
   Without the grant the paths are informational — commit/push explicitly,
   same scope discipline (never `git add -A`).
6. **Finalize** — `finalize --run <id>` only when every current stage is
   complete; a later append/capture re-opens the run cleanly.

## Auto-mode: exactly two block-points

Inside an approved stage everything proceeds automatically (fan-out, retryable
re-runs, checkpoint writes, status emission, and — under the standing grants —
Workflow runs and per-block scoped commit/push). The harness blocks for Gabi at
**only**: (a) the per-stage approval gate; (b) escalations (`push_blocked`,
`store_divergence` fork, `budget_exceeded`, `prereq_against_completed_stage`,
`concurrent_writer`). On either while unattended → durably checkpoint and wait
(exit `2`); never auto-approve, never auto-resolve.

## Standing preauthorizations (the only two)

Both live on the run row (`preauth_commit_push`, `preauth_workflow_optin` —
set via the library `Repository.update_run`, revocable via
`preauth_revoked_at`): (1) **scoped commit + push per block**; (2) the
**Workflow-tool opt-in**. Neither relaxes the gate or any escalation. Budget:
`register --budget <n> --budget-unit tokens` sets a hard ceiling — advancing
past it halts with a `budget_exceeded` escalation (zero overruns).

## Commands (contracts/cli.md — 1:1 with the library, FR-025)

| Command | Effect |
|---|---|
| `register --run <id> [--stages a,b,c] [--title] [--budget n] [--budget-unit u]` | Create/re-attach a run with its ordered stage list (idempotent). |
| `append-stage --run <id> <name>` | Append a dynamically-discovered stage; the total grows. |
| `stage-start --run <id> <name>` | Flip pending→running (started ≠ complete). |
| `checkpoint --run <id> <name> [--completed json] [--remaining json] [--wip u] [--budget n] [--paths a,b] [--issues …] [-m msg]` | Durable checkpoint; `--remaining []` completes the stage; scoped commit boundary. |
| `capture --run <id> --kind <k> --title <t> [--blocks <stage>]` | Capture emergent work; expands the 5-stage mini-pipeline. |
| `resume --run <id>` / `position --run <id>` | The objective four-field position (fork ⇒ exit 2). |
| `status --run <id> [--emit]` | The parseable status line; `--emit` persists it. |
| `gate --run <id> --stage <name> [--approve｜--change --plan <ref>] [--by]` | Present/record the per-stage approval. |
| `rerun --run <id> --stage <name> [--subagent <label>]` | Per-block / isolated per-subagent re-run picture. |
| `trace --run <id> --subject <s> --input <json> [--score] (--accept｜--reject)` | Append a verification-trace record (append-only). |
| `reconcile --run <id>` | PGLite ↔ JSON mirror: fast-forward or escalate a fork (exit 2). |
| `finalize --run <id>` | Finalize — only when every current stage is complete. |
| `keeper start｜stop｜recover --run <id>` | Per-run store keeper lifecycle (spawn/flush/auto-recover; a live bridge is consumed, never killed). |
| `doctor --run <id>` | Read-only health: endpoint, active store, last-seq per store, escalations, budget. |

`--feature <slug>` is a **deprecated 024 alias** for `--run`.

## Memory-chain rooting

This harness is the owner named in CLAUDE.md's *Multi-Stage Task Persistence &
Restart-Resume* section. The durable position lives in the per-run store, NOT a
hand-written restart prompt. `docs/current_plan.md` is a thin pointer to the
roadmap + pipeline state + this harness.
