# Phase 1 Data Model — Marathon Refinement

All entities live in a **per-run isolated PGLite cluster** (one cluster per marathon run, outside the repo)
created by `store/schema.py::ensure_schema`. Every row is also mirrored to a per-run JSON tree inside the
same store root for dual-store reconciliation (FR-024/FR-027). The schema is **greenfield** — it does not
read or migrate 024's shared-cluster `marathon.*` tables (FR-029).

Schema name within the per-run cluster: `marathon` (a fresh database, so no collision with the inert 024
shared-cluster schema of the same name). DDL contract: [`contracts/store-schema.sql`](./contracts/store-schema.sql).

## Store root layout (per run, outside the repo)

```text
<marathon-root>/<run_id>/
├── pgdb/                      # the per-run isolated PGLite cluster (data_dir for bridge_client)
├── pgdb.bridge.lock/         # OS lock (sibling of data_dir, bridge_client convention)
├── <bridge sidecar/heartbeat/consumers>   # published endpoint reused by subsequent ops (FR-012)
├── json/                     # JSON mirror (dual-store fallback)
│   ├── run.json
│   ├── stages/<stage_index>.json
│   ├── checkpoints/<sequence_no>.json
│   ├── items/<item_id>.json
│   ├── approvals/<approval_id>.json
│   ├── escalations/<escalation_id>.json
│   ├── traces/<subject>-<refine_seq>.json
│   └── status/<status_id>.json
└── items/<item_id>/          # per-item mini-artifacts (compact; NO specs/NNN dir) — FR-008
```

## Entities

### `run`  *(the marathon)*
The long, multi-session unit of work.

| Column | Type | Notes |
|---|---|---|
| `id` | text PK | run id (stable; derived from feature slug or caller-supplied) |
| `title` | text | human label |
| `status` | text | `in_progress` \| `finalized`; CHECK |
| `head_commit` | text NULL | repo HEAD captured at registration |
| `budget_ceiling` | bigint NULL | token ceiling (NULL = unbounded) |
| `budget_spent` | bigint NOT NULL DEFAULT 0 | CHECK `ceiling IS NULL OR spent <= ceiling` (SC-006 substrate guard) |
| `budget_unit` | text NULL | e.g. `tokens` (status-line unit) |
| `preauth_commit_push` | boolean DEFAULT false | standing grant #1 (US5 / FR-017) |
| `preauth_workflow_optin` | boolean DEFAULT false | standing grant #2 (Workflow opt-in) |
| `preauth_revoked_at` | timestamptz NULL | revocation timestamp |
| `auto_mode` | boolean DEFAULT false | budget-bounded auto-advance enabled |
| `created_at` | timestamptz DEFAULT NOW() | |

Idempotent upsert on `start` (re-attach an existing run).

### `stage`  *(data-driven — no fixed vocabulary)*
A named unit of work; the ordered, growable list. **No hard-coded stage CHECK** (FR-001).

| Column | Type | Notes |
|---|---|---|
| `id` | bigserial PK | |
| `run_id` | text FK→run | |
| `stage_index` | integer NOT NULL | monotonic 1-based; append = `max+1` (FR-002) |
| `order_key` | double precision NOT NULL | sort key; fractional inserts place blocking mini-stages *ahead* (FR-010) |
| `name` | text NOT NULL | unique within run |
| `origin` | text NOT NULL | `registered` \| `dynamic` \| `mini`; CHECK (manifest origin deferred with the manifest registration path) |
| `item_id` | bigint NULL FK→item | set iff this is a mini-stage |
| `mini_kind` | text NULL | `mini_specify`\|`mini_clarify`\|`mini_plan`\|`mini_tasks`\|`mini_analyze`; CHECK |
| `status` | text NOT NULL DEFAULT 'pending' | `pending`\|`awaiting_approval`\|`running`\|`complete`\|`failed`\|`escalated`; CHECK |
| `started_at` | timestamptz NULL | set by `start_stage` (FR-004: started≠complete) |
| `completed_at` | timestamptz NULL | |
| `last_sequence_no` | bigint DEFAULT 0 | latest checkpoint seq for this stage |

Invariant CHECK: `(item_id IS NULL) = (mini_kind IS NULL)` — a row is either a mini-stage (both set) or an
ordinary stage (both null). Unique `(run_id, name)` and `(run_id, stage_index)`.

### `checkpoint`  *(sole source of truth for resume; append-only)*
A durable record of a stage's completion, plus its scoped-commit outcome (commit folded in per D9).

| Column | Type | Notes |
|---|---|---|
| `id` | bigserial PK | |
| `run_id` | text FK→run | |
| `stage_id` | bigint FK→stage | |
| `sequence_no` | bigint NOT NULL | UNIQUE `(run_id, sequence_no)`; monotonic = `max(primary,fallback)+1` (I1) |
| `wip_unit` | text NULL | unit in flight (NULL when block complete) |
| `completed_units` | jsonb DEFAULT '[]' | for per-subagent re-run (FR-021) |
| `remaining_units` | jsonb DEFAULT '[]' | empty ⇒ block complete |
| `budget_spent` | bigint DEFAULT 0 | budget delta snapshot |
| `committed_paths` | jsonb DEFAULT '[]' | exact paths staged (FR-017; SC-007) |
| `commit_sha` | text NULL | NULL after complete ⇒ resume re-drives the commit (D9 crash-window guard) |
| `pushed` | boolean DEFAULT false | |
| `push_escalation` | text NULL | `push_blocked` linkage (never `--force`) |
| `workflow_run_id` | text NULL | for cached-prefix re-run cascade |
| `store_origin` | text NOT NULL | `primary`\|`fallback`; CHECK (which store wrote it) |
| `created_at` | timestamptz DEFAULT NOW() | |

Index `(run_id, sequence_no DESC)` — resume reads the max-seq checkpoint (I3).

### `item`  *(arising / emergent work)*
An emergent work-item captured mid-run (US2).

| Column | Type | Notes |
|---|---|---|
| `id` | bigserial PK | |
| `run_id` | text FK→run | |
| `kind` | text NOT NULL | `latent_requirement`\|`issue`\|`bug`\|`missing_prerequisite`; CHECK (FR-005) |
| `title` | text NOT NULL | |
| `description` | text NULL | |
| `blocks_stage_id` | bigint NULL FK→stage | set ⇒ blocking; mini-stages route *ahead* (FR-010) |
| `status` | text NOT NULL DEFAULT 'open' | `open`\|`done`; CHECK. `done` when mini-analyze completes (D4) |
| `artifacts_dir` | text NOT NULL | `<store_root>/items/<id>/` (FR-008) |
| `created_at` | timestamptz DEFAULT NOW() | |

Capturing an item appends **5** mini-`stage` rows (FR-006, D4).

### `issue`  *(outstanding concern)*
| Column | Type | Notes |
|---|---|---|
| `id` | bigserial PK | |
| `run_id` | text FK→run | |
| `stage_id` | bigint NULL FK→stage | where raised |
| `summary` | text NOT NULL | |
| `status` | text DEFAULT 'open' | `open`\|`resolved`; CHECK. Open issues surface in position + status line |
| `created_at` | timestamptz DEFAULT NOW() | |

### `approval`  *(per-stage plan-approval gate — ported from 024, append-only)*
| Column | Type | Notes |
|---|---|---|
| `id` | bigserial PK | |
| `stage_id` | bigint FK→stage | |
| `presented_plan_ref` | text NOT NULL | |
| `outcome` | text NULL | NULL=pending, `approve`, `change`; CHECK |
| `decided_by` | text NULL | |
| `decided_at` | timestamptz NULL | |
| `supersedes_id` | bigint NULL FK→approval | decision chain (re-plan) |
| `created_at` | timestamptz DEFAULT NOW() | |

`approval_state(stage)` ∈ {approved, changed, awaiting, None}; `approved` short-circuits resume (FR-020).

### `verification_trace`  *(append-only iteration substrate — ported from 024)*
| Column | Type | Notes |
|---|---|---|
| `id` | bigserial PK | |
| `run_id` | text FK→run | |
| `subject` | text NOT NULL | stage or primitive under refinement |
| `refine_seq` | integer NOT NULL | iteration number |
| `experiment_input` | jsonb DEFAULT '{}' | |
| `metric_score` | double precision NULL | |
| `decision` | text NOT NULL | `accept`\|`reject`; CHECK |
| `created_at` | timestamptz DEFAULT NOW() | |

UNIQUE `(run_id, subject, refine_seq)` — never overwrites (FR-023).

### `escalation`  *(auto-mode block-point requiring the engineer — ported from 024)*
| Column | Type | Notes |
|---|---|---|
| `id` | bigserial PK | |
| `run_id` | text FK→run | |
| `stage_id` | bigint NULL FK→stage | |
| `kind` | text NOT NULL | `non_retryable_failure`\|`store_divergence`\|`push_blocked`\|`budget_exceeded`\|`stage_flagged`\|`prereq_against_completed_stage`; CHECK |
| `detail` | jsonb DEFAULT '{}' | |
| `resolved_at` | timestamptz NULL | partial index WHERE resolved_at IS NULL |
| `created_at` | timestamptz DEFAULT NOW() | |

New kinds vs 024: `budget_exceeded` (clearer than `stage_flagged`), `prereq_against_completed_stage` (edge
case: item captured against an already-done stage — surface, don't reorder finished work).

### `status_report`  *(standardised four-field snapshot — ported from 024)*
| Column | Type | Notes |
|---|---|---|
| `id` | bigserial PK | |
| `run_id` | text FK→run | |
| `stage_id` | bigint NULL FK→stage | |
| `done` | jsonb DEFAULT '[]' | |
| `issues` | jsonb DEFAULT '[]' | |
| `tokens_spent` | bigint DEFAULT 0 | |
| `tokens_remaining` | bigint NULL | |
| `todo` | jsonb DEFAULT '[]' | |
| `created_at` | timestamptz DEFAULT NOW() | |

## Derived (not stored): Resume position
Computed read-only from `run` + `stage` + `checkpoint` + open `issue` (+ approval/commit state). Fields:
`done` (= count of complete stages), `total` (= count of `stage` rows — *current*, may have grown),
`outstanding_issues`, `budget_spent`, and the **single next action**. Ordering rules in
[`contracts/resume-position.md`](./contracts/resume-position.md). Identical with or without conversation
context (SC-008) because it depends solely on durable rows.

## Entity provenance (adopt / port / new)

| Entity | Source | Change |
|---|---|---|
| `run` | sibling `wm_run` + 024 `marathons` | merge: add 024 budget/preauth/auto fields |
| `stage` | sibling `wm_stage` | adopt; add 024 statuses (`awaiting_approval`/`running`/`failed`/`escalated`) |
| `checkpoint` | sibling `wm_checkpoint` + 024 `checkpoints` + `git_blocks` | merge; fold commit/push in (D9) |
| `item` | sibling `wm_item` | adopt; 5-stage expansion (D4) |
| `issue` | sibling `wm_issue` | adopt |
| `approval` | 024 `approvals` | port (sibling lacks) |
| `verification_trace` | 024 `verification_traces` | port (sibling lacks) |
| `escalation` | 024 `escalations` | port + 2 new kinds |
| `status_report` | 024 `status_reports` | port |
