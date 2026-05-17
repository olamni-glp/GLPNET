# Contract — Status & Trace (FR-017 / SC-009 + D1=a trace)

## Unified per-file state (single vocabulary — FR-022)

`not_started｜blocked_on_deps｜analysed｜specced｜scaffolded｜converted｜
complete`, plus `escalated` (reachable from any non-terminal, resolves back).
**Projection only** — derived by one join over `dart_depgraph` (order/SCC/
readiness, read-only), `dart_convspecs`, `dart_plans`, `dart_conversions`,
escalation counts. Never a separately-stored field ⇒ cannot diverge from
durable truth (FR-017/FR-019).

## `builder status`

Returns per-file state + aggregate counts that **reconcile exactly** with
durable state in **< 5 s** on a warm bridge (SC-009). `test_status_projection.py`
asserts reconciliation + the latency budget.

## Tombstone ↔ DB divergence (FR-019)

Before processing, compare tombstone state keys + `sha256` vs DB. Divergence
(state mismatch or sha drift) ⇒ exit code 4, escalate "stale — rebuild
required", **refuse to proceed silently**. `test_tombstone_divergence.py`.

## `builder trace` — DBOS workflow-trace analysis (D1=a)

Read-only projection over DBOS's own `dbos.workflow_status` /
`dbos.operation_outputs`, joined to files/runs via
`builder_runs.outer_workflow_id`:

- `--run ID` → every child workflow + step (stage, status, started, finished,
  attempt count) for debugging/planning.
- `--file R` → that file's step history across runs.

No competing event store is built (D2 — DBOS already persists this).
`test_builder_trace.py` asserts per-file/per-run step history is exposed and
joins correctly after a kill/resume cycle.
