# Phase 1 Data Model: Marathon Stage Harness

**Feature**: 024-marathon-stage-harness | **Date**: 2026-06-05

All harness domain tables live in a new PostgreSQL schema **`marathon`** in the shared
PGLite cluster (`C:/pglite/research/glpnet`), created by Alembic migration
`0010_marathon_schema.py`. DBOS runtime tables remain in the `dbos` schema (auto-created
by `dbos.launch()` — out of Alembic scope). The on-disk **JSON fallback** mirrors the
same logical records (one file per record, carrying the identical `sequence_no`) under
`<repo>/.codeconv/marathon/<marathon_id>/`.

Conventions reused from `codeconv`: append-only history where the spec requires it;
deterministic ids where DBOS replay needs them; the two-phase
`{phase}_started_at / {phase}_completed_at` durable-write idiom; JSON columns for
flexible payloads.

---

## Entity: Marathon  → `marathon.marathons`

The long multi-stage feature being driven end-to-end. (Spec Key Entity: *Marathon*.)

| Column | Type | Notes |
|---|---|---|
| `id` | text PK | deterministic, e.g. `mlink` for `multi-protocol-link-layer` |
| `feature_slug` | text NOT NULL | e.g. `multi-protocol-link-layer` |
| `feature_branch` | text NOT NULL | e.g. `0NN-multi-protocol-link-layer` |
| `budget_ceiling` | bigint NULL | token ceiling; NULL = unbounded (FR-012) |
| `budget_spent` | bigint NOT NULL DEFAULT 0 | running total |
| `preauth_commit_push` | boolean NOT NULL DEFAULT false | standing grant #1 (FR-014/D10) |
| `preauth_workflow_optin` | boolean NOT NULL DEFAULT false | standing grant #2 (FR-023/D10) |
| `preauth_revoked_at` | timestamptz NULL | Gabi revocation (either grant) |
| `auto_mode` | boolean NOT NULL DEFAULT false | unattended vs attended |
| `created_at` | timestamptz NOT NULL | |

**Rules**: `budget_spent` MUST NOT exceed `budget_ceiling` when set (FR-012/SC-006);
the two preauth flags are the **only** standing grants (D10/FR-023).

## Entity: Stage-block (= logical block)  → `marathon.stage_blocks`

The cadence grouping mapped 1:1 to one Workflow run; the unit of checkpoint + commit/push.
(Spec Key Entity: *Stage-block*; FR-019/D9.)

| Column | Type | Notes |
|---|---|---|
| `id` | text PK | deterministic: `{marathon_id}:{stage}:{ordinal}` |
| `marathon_id` | text FK → marathons.id | |
| `stage` | text NOT NULL | one of specify/clarify/plan/task/analyze/implement/review |
| `block_kind` | text NOT NULL | `specify` \| `clarify` \| `plan_task_analyze` \| `implement_session` \| `review` (D9) |
| `ordinal` | int NOT NULL | order within marathon; implement sessions increment |
| `workflow_run_id` | text NULL | run-linkage to the Workflow run (FR-009, US4) |
| `status` | text NOT NULL | `pending` \| `awaiting_approval` \| `running` \| `done` \| `failed` \| `escalated` |
| `last_sequence_no` | bigint NOT NULL DEFAULT 0 | highest checkpoint seq for this block |
| `started_at` | timestamptz NULL | two-phase |
| `completed_at` | timestamptz NULL | two-phase |

**Rules**: a block enters `running` only after an approved gate (FR-004/022); the
checkpoint boundary == the commit/push boundary == this block (FR-019).

## Entity: Checkpoint  → `marathon.checkpoints`  *(append-only)*

Durable snapshot of position; sufficient to resume with no context loss and to arbitrate
store divergence. (Spec Key Entity: *Checkpoint*; FR-001/002/003/021.)

| Column | Type | Notes |
|---|---|---|
| `id` | bigserial PK | |
| `block_id` | text FK → stage_blocks.id | |
| `marathon_id` | text FK → marathons.id | |
| `sequence_no` | bigint NOT NULL UNIQUE | **strictly monotonic across the whole marathon** (FR-021/D5) |
| `stage` | text NOT NULL | redundant-with-block for fast resume-locate |
| `wip_unit` | text NULL | the in-progress unit (e.g. a task id / subagent label) |
| `completed_units` | jsonb NOT NULL | units already done (skip on resume — FR-003/SC-002) |
| `remaining_units` | jsonb NOT NULL | units still to do |
| `workflow_run_id` | text NULL | run-linkage at this checkpoint |
| `budget_spent` | bigint NOT NULL | snapshot of spend (FR-012) |
| `store_origin` | text NOT NULL | `primary` \| `fallback` — which store wrote it (FR-020) |
| `created_at` | timestamptz NOT NULL | |

**Rules**: append-only (never overwritten); `sequence_no` strictly increases marathon-wide
and is the reconciliation arbiter (D5); resume reads the **max(sequence_no)** checkpoint.

## Entity: Approval gate  → `marathon.approvals`  *(append-only)*

Stored engineer decision per block; superseded decisions retained. (Spec Key Entity:
*Approval gate*; FR-004/005/D6; US2-AS3.)

| Column | Type | Notes |
|---|---|---|
| `id` | bigserial PK | |
| `block_id` | text FK → stage_blocks.id | |
| `presented_plan_ref` | text NOT NULL | pointer to the plan presented (path/digest) |
| `outcome` | text NULL | `approve` \| `change`; NULL while awaiting decision |
| `decided_by` | text NULL | e.g. `gabi` |
| `decided_at` | timestamptz NULL | |
| `supersedes_id` | bigint NULL FK → approvals.id | the prior decision this one replaces |
| `created_at` | timestamptz NOT NULL | |

**Rules**: append-only; a `change` creates a new row pointing at `supersedes_id`; an
existing `approve` row short-circuits the gate on resume (no re-ask — SC-004).

## Entity: Status report  → `marathon.status_reports`

Standardized periodic snapshot. (Spec Key Entity: *Status report*; FR-013/D8; SC-005.)

| Column | Type | Notes |
|---|---|---|
| `id` | bigserial PK | |
| `marathon_id` | text FK → marathons.id | |
| `block_id` | text NULL FK → stage_blocks.id | |
| `done` | jsonb NOT NULL | what is done |
| `issues` | jsonb NOT NULL | current issues |
| `tokens_spent` | bigint NOT NULL | from Workflow budget |
| `tokens_remaining` | bigint NULL | NULL if unbounded |
| `todo` | jsonb NOT NULL | what is still to do |
| `created_at` | timestamptz NOT NULL | cadence ~5 min during active work |

**Rules**: every report MUST contain all four fields (SC-005); machine-readable.

## Entity: Verification-trace record  → `marathon.verification_traces`  *(append-only)*

Generic restart-safe iteration/verification substrate — NOT the optimizer. (Spec Key
Entity: *Verification-trace record*; FR-016/017/D-trace; US7.)

| Column | Type | Notes |
|---|---|---|
| `id` | bigserial PK | |
| `marathon_id` | text FK → marathons.id | |
| `subject` | text NOT NULL | stage or primitive name |
| `refine_seq` | int NOT NULL | ordered iteration index (earlier never overwritten — US7-AS2) |
| `experiment_input` | jsonb NOT NULL | |
| `metric_score` | double precision NULL | |
| `decision` | text NOT NULL | `accept` \| `reject` |
| `created_at` | timestamptz NOT NULL | |

**Rules**: append-only; `(subject, refine_seq)` ordered history is preserved; an external
optimizer (out of scope) can reconstruct iteration history without harness internals
(US7-AS3).

## Entity: Git block record  → `marathon.git_blocks`

Per-block commit/push outcome under the preauthorization. (FR-014/015/D10; SC-010.)

| Column | Type | Notes |
|---|---|---|
| `id` | bigserial PK | |
| `block_id` | text FK → stage_blocks.id | |
| `commit_sha` | text NULL | the commit (NULL if escalated before commit) |
| `staged_files` | jsonb NOT NULL | exactly the block's files (no sweeping — SC-010) |
| `pushed` | boolean NOT NULL DEFAULT false | |
| `escalation` | text NULL | reason if push blocked / non-fast-forward (FR-015) |
| `created_at` | timestamptz NOT NULL | |

**Rules**: stages only this block's files; never force-push; a blocked push sets
`escalation` and stops (FR-015/SC-010).

## Entity: Escalation  → `marathon.escalations`

Durable record of an auto-mode block-point requiring Gabi. (FR-022/D11.)

| Column | Type | Notes |
|---|---|---|
| `id` | bigserial PK | |
| `marathon_id` | text FK → marathons.id | |
| `block_id` | text NULL FK → stage_blocks.id | |
| `kind` | text NOT NULL | `non_retryable_failure` \| `store_divergence` \| `push_blocked` \| `stage_flagged` |
| `detail` | jsonb NOT NULL | context for the decision |
| `resolved_at` | timestamptz NULL | Gabi's resolution |
| `created_at` | timestamptz NOT NULL | |

**Rules**: created → harness durably checkpoints and waits; never auto-resolved (FR-022).

---

## Relationships

```text
marathons 1───* stage_blocks 1───* checkpoints   (max sequence_no = resume point)
                       │
                       ├──* approvals      (append-only; supersedes chain)
                       └──1 git_blocks      (one commit/push per block)
marathons 1───* status_reports
marathons 1───* verification_traces  (ordered by subject, refine_seq)
marathons 1───* escalations
```

## JSON fallback layout (mirror)

```text
<repo>/.codeconv/marathon/<marathon_id>/
├── marathon.json            # marathons row mirror
├── blocks/<block_id>.json   # stage_blocks row mirror
├── checkpoints/<seq>.json    # one file per checkpoint, named by sequence_no
├── approvals/<id>.json
├── status/<id>.json
├── traces/<subject>-<refine_seq>.json
└── git/<block_id>.json
```

Each mirror file carries `sequence_no` (where applicable) so reconciliation (D5) can
compare the two stores by strict sequence and fast-forward / escalate accordingly.
