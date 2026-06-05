# Contract: Marathon CLI

**Surface**: `codeconv marathon <subcommand>` (statically registered Typer app; NOT in
the conversion `codeconv list` registry). All subcommands honor the global
`--data-dir C:/pglite/research/glpnet` convention and reuse `codeconv.bridge_client`.

Output: human-readable by default; `--json` emits a machine-readable object. Exit codes:
`0` success, `2` escalation/awaiting-Gabi (blocked, not an error), `64` usage/filesystem
guard, `70` internal failure.

---

## `marathon start --feature <slug> [--branch <b>] [--budget <tokens>] [--auto] [--preauth-commit-push] [--preauth-workflow]`

Create (or re-attach to) the marathon record and record the two standing preauthorizations.
Idempotent: re-running with the same `--feature` re-attaches, never duplicates.

- **Writes**: `marathon.marathons` (+ JSON mirror).
- **Returns** (`--json`): `{marathon_id, feature_slug, auto_mode, preauth_commit_push, preauth_workflow_optin, budget_ceiling}`.
- **FR**: FR-014/023, D10.

## `marathon resume [--feature <slug>]`

Objectively locate position and resume. Reads in the established order: roadmap → buildkit
pipeline state → spec/plan/tasks (D4/FR-002), then the **max(sequence_no)** checkpoint.
Never reads a conversation summary.

- **Reads**: `marathon.checkpoints` (max seq), `stage_blocks`, `approvals`.
- **Returns** (`--json`): `{stage, block_id, block_kind, wip_unit, completed_units, remaining_units, workflow_run_id, store_origin, approval_state}`.
- **Behavior**: completed units are NOT re-executed (SC-002); recorded approvals are
  honored, not re-asked (SC-004). On store divergence → exit `2` + escalation (D5).
- **FR**: FR-002/003/005/020/021.

## `marathon gate --block <id> [--approve | --change --plan <ref>] [--by <who>]`

Present/record the per-stage approval decision.

- **No flags**: presents the block's plan and the current gate state; if already approved,
  reports "approved" and exits `0` (no re-ask — SC-004).
- **`--approve`**: records an `approve` row.
- **`--change --plan <ref>`**: records a `change` row superseding the prior (retained).
- **Writes**: `marathon.approvals` (append-only).
- **FR**: FR-004/005, D6.

## `marathon verify-spike [--feature <slug>]`

Run the FR-011 first-implementation verification spike: a small multi-step Workflow run
exercising `resumeFromRunId` cached-prefix resume + `budget.spent()/remaining()`. Records
the verification result durably as a `verification_traces` row (`subject=workflow-spike`).

- **Reads/Writes**: `marathon.verification_traces`; composes the Workflow tool.
- **Returns** (`--json`): `{cached_prefix_ok, budget_observed_ok, first_reexecuted_step, recorded_trace_id}`.
- **FR**: FR-011, US4, SC-008. See `workflow-composition.md`.

## `marathon status [--feature <slug>] [--emit]`

Show / emit the standardized four-field report (done / issues / tokens spent+remaining /
to-do). `--emit` persists a new `status_reports` row (the ~5-min cadence driver calls
this).

- **Reads**: checkpoints, escalations, budget; **Writes** (with `--emit`): `status_reports`.
- **FR**: FR-013, D8, SC-005.

## `marathon rerun --block <id> [--subagent <label>]`

Re-run a failed stage from its last checkpoint, or a single failed subagent in isolation.

- **`--block` only**: restart the block from its last checkpoint (not marathon start) —
  FR-006.
- **`--block --subagent <label>`**: re-execute only that subagent; succeeded siblings are
  untouched — FR-007/SC-003.
- **Behavior**: failure history is preserved alongside the eventual success (append-only —
  FR-008). Changed-input units are treated as new work, not stale cache (edge case).
- **Writes**: `checkpoints` (new seq), `stage_blocks.status`.

## `marathon trace --subject <s> --input <json> [--score <f>] (--accept | --reject)`

Append a verification-trace record (substrate only — no optimizer). Preserves
`(subject, refine_seq)` order; never overwrites earlier iterations.

- **Writes**: `marathon.verification_traces` (append-only).
- **FR**: FR-016/017, US7.

## `marathon reconcile [--feature <slug>]`

Compare primary (PGLite) and JSON-fallback stores by `sequence_no`. Strictly-higher wins
and fast-forwards the stale store; a true fork → exit `2` + escalation (never silently
picks).

- **FR**: FR-020/021, D5, SC-007.

## `marathon doctor [--feature <slug>]`

Diagnose: bridge reachable? primary vs fallback in use? last checkpoint seq on each store?
open escalations? budget headroom? Mirrors `codeconv doctor` style. Read-only, exit `0`.

---

## Composition note

`start`/`resume`/`rerun` internally compose the **Workflow tool** (one stage-block = one
Workflow run; see `workflow-composition.md`). The CLI is the durable-state + control
surface; the Workflow tool is the in-session execution surface. The buildkit-stage skill
(`buildkit-hooks.md`) calls these subcommands at each pipeline stage.
