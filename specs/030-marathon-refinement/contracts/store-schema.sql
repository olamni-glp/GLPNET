-- Contract DDL — per-run isolated marathon store (greenfield, FR-029)
-- Created by store/schema.py::ensure_schema in the per-run PGLite cluster (NOT the shared Alembic chain, D2).
-- Idempotent (CREATE ... IF NOT EXISTS). Schema name `marathon` inside the isolated cluster.

CREATE SCHEMA IF NOT EXISTS marathon;

-- run -------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS marathon.run (
    id                      text PRIMARY KEY,
    title                   text NOT NULL DEFAULT '',
    status                  text NOT NULL DEFAULT 'in_progress',
    head_commit             text,
    budget_ceiling          bigint,
    budget_spent            bigint NOT NULL DEFAULT 0,
    budget_unit             text,
    preauth_commit_push     boolean NOT NULL DEFAULT false,
    preauth_workflow_optin  boolean NOT NULL DEFAULT false,
    preauth_revoked_at      timestamptz,
    auto_mode               boolean NOT NULL DEFAULT false,
    created_at              timestamptz NOT NULL DEFAULT NOW(),
    CONSTRAINT run_status_ck  CHECK (status IN ('in_progress','finalized')),
    CONSTRAINT run_budget_ck  CHECK (budget_ceiling IS NULL OR budget_spent <= budget_ceiling)
);

-- stage (data-driven; NO hard-coded stage vocabulary CHECK) ------------------
CREATE TABLE IF NOT EXISTS marathon.stage (
    id                bigserial PRIMARY KEY,
    run_id            text NOT NULL REFERENCES marathon.run(id),
    stage_index       integer NOT NULL,
    order_key         double precision NOT NULL,
    name              text NOT NULL,
    origin            text NOT NULL,
    item_id           bigint REFERENCES marathon.item(id),
    mini_kind         text,
    status            text NOT NULL DEFAULT 'pending',
    started_at        timestamptz,
    completed_at      timestamptz,
    last_sequence_no  bigint NOT NULL DEFAULT 0,
    CONSTRAINT stage_origin_ck CHECK (origin IN ('registered','dynamic','mini')),
    CONSTRAINT stage_mini_ck   CHECK (mini_kind IS NULL OR mini_kind IN
        ('mini_specify','mini_clarify','mini_plan','mini_tasks','mini_analyze')),
    CONSTRAINT stage_status_ck CHECK (status IN
        ('pending','awaiting_approval','running','complete','failed','escalated')),
    -- a row is either a mini-stage (item_id AND mini_kind) or an ordinary stage (neither)
    CONSTRAINT stage_mini_pair_ck CHECK ((item_id IS NULL) = (mini_kind IS NULL)),
    CONSTRAINT stage_name_uq  UNIQUE (run_id, name),
    CONSTRAINT stage_index_uq UNIQUE (run_id, stage_index)
);
CREATE INDEX IF NOT EXISTS stage_order_idx ON marathon.stage (run_id, order_key);

-- item (arising / emergent work) ---------------------------------------------
CREATE TABLE IF NOT EXISTS marathon.item (
    id              bigserial PRIMARY KEY,
    run_id          text NOT NULL REFERENCES marathon.run(id),
    kind            text NOT NULL,
    title           text NOT NULL,
    description     text,
    blocks_stage_id bigint REFERENCES marathon.stage(id),
    status          text NOT NULL DEFAULT 'open',
    artifacts_dir   text NOT NULL,
    created_at      timestamptz NOT NULL DEFAULT NOW(),
    CONSTRAINT item_kind_ck   CHECK (kind IN
        ('latent_requirement','issue','bug','missing_prerequisite')),
    CONSTRAINT item_status_ck CHECK (status IN ('open','done'))
);

-- checkpoint (append-only; sole source of truth for resume; commit folded in) -
CREATE TABLE IF NOT EXISTS marathon.checkpoint (
    id               bigserial PRIMARY KEY,
    run_id           text NOT NULL REFERENCES marathon.run(id),
    stage_id         bigint NOT NULL REFERENCES marathon.stage(id),
    sequence_no      bigint NOT NULL,
    wip_unit         text,
    completed_units  jsonb NOT NULL DEFAULT '[]'::jsonb,
    remaining_units  jsonb NOT NULL DEFAULT '[]'::jsonb,
    budget_spent     bigint NOT NULL DEFAULT 0,
    committed_paths  jsonb NOT NULL DEFAULT '[]'::jsonb,
    commit_sha       text,
    pushed           boolean NOT NULL DEFAULT false,
    push_escalation  text,
    workflow_run_id  text,
    store_origin     text NOT NULL,
    created_at       timestamptz NOT NULL DEFAULT NOW(),
    CONSTRAINT checkpoint_seq_uq   UNIQUE (run_id, sequence_no),
    CONSTRAINT checkpoint_store_ck CHECK (store_origin IN ('primary','fallback'))
);
CREATE INDEX IF NOT EXISTS checkpoint_resume_idx ON marathon.checkpoint (run_id, sequence_no DESC);

-- issue ----------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS marathon.issue (
    id          bigserial PRIMARY KEY,
    run_id      text NOT NULL REFERENCES marathon.run(id),
    stage_id    bigint REFERENCES marathon.stage(id),
    summary     text NOT NULL,
    status      text NOT NULL DEFAULT 'open',
    created_at  timestamptz NOT NULL DEFAULT NOW(),
    CONSTRAINT issue_status_ck CHECK (status IN ('open','resolved'))
);

-- approval (per-stage gate; append-only) -------------------------------------
CREATE TABLE IF NOT EXISTS marathon.approval (
    id                 bigserial PRIMARY KEY,
    stage_id           bigint NOT NULL REFERENCES marathon.stage(id),
    presented_plan_ref text NOT NULL,
    outcome            text,
    decided_by         text,
    decided_at         timestamptz,
    supersedes_id      bigint REFERENCES marathon.approval(id),
    created_at         timestamptz NOT NULL DEFAULT NOW(),
    CONSTRAINT approval_outcome_ck CHECK (outcome IS NULL OR outcome IN ('approve','change'))
);
CREATE INDEX IF NOT EXISTS approval_stage_idx ON marathon.approval (stage_id, id);

-- verification_trace (append-only iteration substrate) -----------------------
CREATE TABLE IF NOT EXISTS marathon.verification_trace (
    id               bigserial PRIMARY KEY,
    run_id           text NOT NULL REFERENCES marathon.run(id),
    subject          text NOT NULL,
    refine_seq       integer NOT NULL,
    experiment_input jsonb NOT NULL DEFAULT '{}'::jsonb,
    metric_score     double precision,
    decision         text NOT NULL,
    created_at       timestamptz NOT NULL DEFAULT NOW(),
    CONSTRAINT verification_trace_uq          UNIQUE (run_id, subject, refine_seq),
    CONSTRAINT verification_trace_decision_ck CHECK (decision IN ('accept','reject'))
);

-- escalation -----------------------------------------------------------------
CREATE TABLE IF NOT EXISTS marathon.escalation (
    id           bigserial PRIMARY KEY,
    run_id       text NOT NULL REFERENCES marathon.run(id),
    stage_id     bigint REFERENCES marathon.stage(id),
    kind         text NOT NULL,
    detail       jsonb NOT NULL DEFAULT '{}'::jsonb,
    resolved_at  timestamptz,
    created_at   timestamptz NOT NULL DEFAULT NOW(),
    CONSTRAINT escalation_kind_ck CHECK (kind IN
        ('non_retryable_failure','store_divergence','push_blocked',
         'budget_exceeded','stage_flagged','prereq_against_completed_stage'))
);
CREATE INDEX IF NOT EXISTS escalation_open_idx ON marathon.escalation (run_id) WHERE resolved_at IS NULL;

-- status_report --------------------------------------------------------------
CREATE TABLE IF NOT EXISTS marathon.status_report (
    id               bigserial PRIMARY KEY,
    run_id           text NOT NULL REFERENCES marathon.run(id),
    stage_id         bigint REFERENCES marathon.stage(id),
    done             jsonb NOT NULL DEFAULT '[]'::jsonb,
    issues           jsonb NOT NULL DEFAULT '[]'::jsonb,
    tokens_spent     bigint NOT NULL DEFAULT 0,
    tokens_remaining bigint,
    todo             jsonb NOT NULL DEFAULT '[]'::jsonb,
    created_at       timestamptz NOT NULL DEFAULT NOW()
);
CREATE INDEX IF NOT EXISTS status_report_idx ON marathon.status_report (run_id, created_at DESC);

-- NOTE: stage.item_id → item(id) and item.blocks_stage_id → stage(id) are mutually referential.
-- ensure_schema creates tables then adds the stage.item_id FK last (ALTER TABLE ADD CONSTRAINT IF NOT
-- EXISTS), or defers the constraint, to avoid an ordering deadlock on first create.
