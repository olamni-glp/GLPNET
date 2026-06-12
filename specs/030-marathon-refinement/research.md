# Phase 0 Research — Marathon Refinement

The spec's four clarification forks (store model, packaging, mini-pipeline implement semantics, migration)
were all resolved in the 2026-06-11 clarification session — so there are **no open `NEEDS CLARIFICATION`**.
This Phase 0 instead grounds the plan in the two reference implementations and records the load-bearing
technical decisions and the reconciliation findings that the design depends on.

## Reference implementations surveyed

- **glpnet 024** — `codeconv/src/codeconv/marathon/` (11 modules) + shared-cluster schema in Alembic
  `0010_marathon_schema.py` (8 tables in PGLite schema `marathon`: `marathons`, `stage_blocks`,
  `checkpoints`, `approvals`, `status_reports`, `verification_traces`, `git_blocks`, `escalations`).
  Hard-codes a 7-stage tuple and a canonical-collapse cadence. In-process; no keeper; dual store =
  shared PGLite + JSON fallback under `<repo>/.codeconv/marathon/<id>/`.
- **sibling `crucible_marathon`** — `D:/bstdev/research/crucible-xyz/src/crucible_marathon/` (≈16 modules)
  + `specs/012-work-marathon-stage-harness/`. Data-driven `wm_stage` model; background keeper over a
  **per-run isolated PGLite cluster** (`PGliteSupervisor`, endpoint published to `<store_root>/keeper.json`);
  `intake.py` with `capture_item` and a **six**-stage mini-pipeline (incl. per-item `implement`).
  Schema (`wm_run`, `wm_stage`, `wm_checkpoint`, `wm_issue`, `wm_item`) created via ORM `ensure_schema`.

## Decisions

### D1 — Per-run isolated store reached via `codeconv.bridge_client` (not a re-ported PGliteSupervisor)
- **Decision**: Each marathon run gets its own PGLite cluster at a per-run `data_dir` *outside* the repo
  (e.g. a user-level marathon root on NTFS/ReFS). It is reached through the existing, hardened
  `codeconv.bridge_client.acquire_or_discover(..., data_dir=<per-run-path>)` and torn down via
  `request_force_shutdown(..., data_dir=<per-run-path>)`.
- **Rationale**: Satisfies FR-027 (per-run isolation, outside repo) **and** FR-028 (compose codeconv's
  bridge/durable libraries) at once. `bridge_client` already provides the exact primitives US3 needs:
  speculative spawn with a `mkdir` mutex, a sidecar endpoint with a heartbeat, a consumer registration
  (single-writer via a kernel-fd lock), heartbeat-freshness staleness detection, and a non-destructive
  `.shutdown` marker. The `_check_data_dir_filesystem` guard (exit 64) protects the per-run path too.
- **Alternatives rejected**: (a) Re-port the sibling's `PGliteSupervisor` + custom `keeper.json` — rejected:
  duplicates infrastructure FR-028 says to reuse and forks the lock/heartbeat semantics. (b) Keep state in
  the shared `<repo>/.pgdb/` cluster (024 model) — rejected by clarification (no isolation, no keeper, a
  crash wedges the shared cluster).

### D2 — Per-run schema via ORM `ensure_schema`, NOT the shared Alembic chain
- **Decision**: The greenfield per-run tables are created by an idempotent `store/schema.py::ensure_schema`
  run against the per-run cluster on first connect. The shared-repo Alembic head stays `0010`.
- **Rationale**: The per-run cluster is a *different database* from `<repo>/.pgdb/`; the repo's Alembic
  chain governs only the shared cluster. Adding a `0011` to the shared chain would create shared-cluster
  tables the refined harness never uses (greenfield, FR-029) and would muddy Constitution VI-a's single-
  head test family. ORM `ensure_schema` is the sibling's proven approach for an isolated per-run store.
- **Alternatives rejected**: A shared-cluster `0011_marathon_v2_schema` migration — rejected: wrong home
  (the data lives per-run, off-repo), and it would imply migrating/coexisting with 024 rows that FR-029
  says to leave inert.

### D3 — Stage vocabulary becomes data (adopt the sibling `wm_stage` model)
- **Decision**: Replace 024's `STAGES` tuple + `_STAGE_TO_BLOCK_KIND` + `_CANONICAL_STAGE` collapse with a
  `stage` table carrying `stage_index` (monotonic), `order_key` (numeric, fractional inserts allowed),
  `name` (unique within run), and `origin ∈ {registered, manifest, dynamic, mini}`. `register_run(stages=[…])`
  writes the initial list; `append_stage(name)` takes `max(stage_index)+1`. Remove the hard-coded stage
  `CHECK` constraint entirely.
- **Rationale**: FR-001/002/003 require an arbitrary, growable, vocabulary-independent stage list, with
  progress reported against the *current* total. Fractional `order_key` is what makes blocking-prerequisite
  insertion *ahead of* a stage (FR-010) clean and collision-free for stacked items (edge case).
- **Alternatives rejected**: Keep the fixed tuple and add an "extra stages" side-table — rejected: re-creates
  the coupling US1 removes and complicates ordering.

### D4 — Mini-pipeline = FIVE stages feeding the marathon's single implement (divergence from sibling)
- **Decision**: A captured item expands into exactly `mini-specify → mini-clarify → mini-plan → mini-tasks
  → mini-analyze` (5). There is **no** per-item `implement`; the planning output feeds the marathon's own
  single `implement` stage, and the item is marked `done` when its mini-analyze completes and its artifacts
  are available to implement.
- **Rationale**: Explicitly confirmed in clarification Q3. Emergent work is implemented *together* in the
  marathon's implement stage, not piecemeal per item.
- **Alternatives rejected**: The sibling's six-stage `MINI_KINDS` (with per-item `implement`) — rejected by
  clarification.

### D5 — Keeper = thin lifecycle over `bridge_client`; stale-residue recovery reuses heartbeat + force-shutdown
- **Decision**: `keeper.py` exposes `start_keeper` (acquire/spawn the per-run bridge, surface its endpoint),
  `stop_keeper` (graceful — `request_force_shutdown` writes the `.shutdown` marker; the bridge flushes and
  exits on its next tick), and `recover_keeper` (a fresh `acquire_or_discover` clears stale residue: a dead
  endpoint with a stale heartbeat is re-spawned via the `mkdir` mutex; a *live* bridge child is refused as a
  concurrent writer, not killed). Single-writer = the consumer kernel-fd lock; a second writer is refused
  with a message distinct from a recoverable stale condition (FR-015/016).
- **Rationale**: Maps every US3 acceptance scenario onto a primitive `bridge_client` already implements,
  honouring II (no workaround) and FR-028 (reuse).
- **Alternatives rejected**: Bespoke lockfile + PID liveness in the marathon module — rejected: re-implements
  bridge_client and risks divergent semantics.

### D6 — Dual-store reconciliation rebased onto the per-run store (PGLite ↔ its own JSON mirror)
- **Decision**: Keep 024's reconciliation logic (compare by `sequence_no`; fast-forward the stale store;
  escalate a true fork with `store_divergence`; never silently pick) but rebind it to **the per-run PGLite
  cluster vs a JSON mirror inside the same per-run store root** — not against the shared codeconv cluster.
- **Rationale**: FR-027 explicitly rebases FR-024 onto the isolated store; the JSON mirror is the keeper-
  independent fallback that lets work continue (and reconcile) if the per-run bridge is transiently down.
- **Alternatives rejected**: Drop the JSON mirror now that the store is isolated — rejected: US5/FR-024
  requires reconciliation to be *preserved*, and the mirror is the offline-resilience path (FR-020 carry-over).

### D7 — Preserve 024's five strengths by porting them onto the new stage model
- **Decision**: `gate.py`, `orchestrate.py` (Budget + rerun), `trace.py`, `escalation.py`, and the
  reconciliation in `repository.py` are ported to key off the data-driven `stage`/`checkpoint` rows in the
  per-run store. The append-only / supersedes / short-circuit / halt-escalate / never-overwrite semantics
  are unchanged (US5 = zero regressions, SC-009).
- **Rationale**: The sibling lacks all five; the refinement must not drop them.

### D8 — Greenfield: never read 024's shared-cluster `marathon` schema
- **Decision**: The refined harness reads/writes only per-run isolated stores. 024's `marathon.*` rows in
  `<repo>/.pgdb/` are inert history; no migration, no dual-read.
- **Rationale**: FR-029; verified no live 024 run (025 / 027 / harness-verify records all complete).

### D9 — Commit boundary + status reconciled, folding push state onto the checkpoint
- **Decision**: Reuse the project's scoped-commit mechanism (explicit paths, hooks run, never force / never
  blanket-add / never history-rewrite). Record `commit_sha`, `committed_paths`, and push state on the
  `checkpoint` row (sibling style), with a `git_block`-equivalent escalation linkage retained for
  `push_blocked`. Resume re-drives the scoped commit when a checkpoint is durably complete but
  `commit_sha IS NULL` (crash-window guard). Status line keeps the parseable four-field grammar, computed
  against the *current* total.
- **Rationale**: FR-017/018/019 say reconcile, not duplicate; folding commit onto the checkpoint matches the
  sibling and removes 024's separate `git_blocks` round-trip while preserving the crash-window guarantee.

## Reconciliation findings (carried into `/buildkit-analyze`)

- **F1 — US2 "headline differentiator beyond the sibling package" is overstated.** The sibling
  `crucible_marathon` *already implements* emergent-work capture + a mini-pipeline (`intake.py`). glpnet's
  genuine divergence is the **5-stage (vs 6) mini-pipeline feeding a shared implement** (D4), and applying
  glpnet's gate/rerun/budget/trace to mini-stages. Recommend softening the spec's framing to "extends and
  diverges from the sibling's mini-pipeline" to keep the spec accurate. (Spec Assumptions already note the
  6-vs-5 divergence, so this is a wording nit, not a design gap.)
- **F2 — VI-b deviation** (per-run cluster outside the repo) is real and intentional; documented in the plan's
  Complexity Tracking and surfaced here so analyze treats it as a *justified* judgement-gate deviation, not a
  silent violation. **Resolved 2026-06-11**: the constitution was amended to **v1.1.0** — VI-b now scopes the
  single-cluster rule to the repo's working-data cluster and explicitly exempts per-run marathon stores;
  FR-027 is compliant, no deviation remains.
- **F3 — Telemetry** (spec Assumptions: fail-safe mirroring): the sibling mirrors to `crucible_obs`. glpnet
  has no such sink; treat telemetry as **out of scope / no-op** for this feature unless a fail-safe local
  sink is trivially available. Recorded so tasks don't accidentally take a hard dependency on it.
- **F4 — "isolated store outside the repo" vs the NTFS guard.** The per-run path must be NTFS/ReFS or
  `_check_data_dir_filesystem` fails (exit 64). The marathon root default must therefore be a guaranteed-
  NTFS user-level path (consistent with the canonical `C:/pglite/...` convention), not, e.g., a path the user
  might place on exFAT removable media. Captured as a quickstart/contract note.

## T001 — Greenfield precondition verification (2026-06-12, implement Phase 1)

Queried the shared cluster (`--data-dir C:/pglite/research/glpnet`, schema `marathon`) directly for any
in-flight 024 run before beginning the in-place rewrite (FR-029 precondition).

**Raw finding** — schema `marathon` exists with its 8 024 tables. **2** `marathons` rows, **27**
`checkpoints`, and **9 non-final `stage_blocks`** (status `<> 'done'`):

| marathon_id | feature_slug | non-final blocks | budget_spent |
|---|---|---|---|
| `ma75cf583` | multi-protocol-link-layer (025) | 1 `awaiting_approval` + 3 `running` | 394 418 |
| `m57f4c46e` | 027-refinement-verification-framework | 5 `running` (2 `done`) | 200 000 |

**Determination: NO live 024 marathon is in flight.** Both non-final rows are **stale residue from features
already shipped to `main`** — they are *exactly* the never-advanced-row defect feature 030 exists to remedy,
not live work:
- **027** — merged to `main` via PR #30 (merge commit `a43d1280`), feature complete 28/28, yet its row still
  shows 5 `running` blocks. Tag `v2026.06.10.1`.
- **025** — shipped under tag `v2026.06.08.1`; its commit is an ancestor of the current `030-marathon-refinement`
  branch. Row left at `awaiting_approval`/`running`.

This **refines D8's wording**: the records are *not* "all complete" at the harness level — the *work* shipped
but the *rows* are stale-not-final. The greenfield rewrite is unaffected: 030's per-run isolated store never
reads these `marathon.*` rows; they remain inert history (D8/FR-029). Precondition satisfied → rewrite proceeds.
