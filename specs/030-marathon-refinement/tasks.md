---
description: "Task list — Marathon Refinement (feature 030)"
---

# Tasks: Marathon Refinement

**Input**: Design documents from `/specs/030-marathon-refinement/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: INCLUDED. The spec is behaviour-driven (every user story has an Independent Test; SC-009 requires
the five preserved 024 capabilities to pass their existing behavioural checks unchanged), and glpnet
discipline mandates regression tests for ported/refined behaviour. pytest in `codeconv/tests/`,
`--test-concurrency=1` mandatory; cluster-dependent tests gated `@needs_bridge`; pure derivations tested
bridge-free.

**Organization**: by user story (US1–US5), in spec priority order. The harness rewrites
`codeconv/src/codeconv/marathon/` in place; 024's *durable rows* (shared-cluster `marathon.*`) are left inert
(greenfield, FR-029) — code modules are reorganised.

## Format: `[ID] [P?] [Story] Description`
- **[P]**: parallelizable (different file, no dependency on an incomplete task)
- **[Story]**: US1–US5 for story-phase tasks only

## Path Conventions
- Source: `codeconv/src/codeconv/marathon/`
- Tests: `codeconv/tests/`
- Contracts referenced: `specs/030-marathon-refinement/contracts/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Module scaffolding for the greenfield, workload-agnostic rewrite.

- [X] T001 Verify no live 024 marathon is in flight before any rewrite: query the shared-cluster `marathon.marathons`/`stage_blocks` for any non-final run and record the result in `specs/030-marathon-refinement/research.md` (FR-029 precondition — greenfield only if none in flight). **DONE 2026-06-12**: 2 stale rows (025/027, both shipped to main), no LIVE run; recorded in research.md.
- [X] T002 Reorganise the package skeleton in `codeconv/src/codeconv/marathon/` for the data-driven model: create empty/stub `env.py`, `keeper.py`, `stages.py`, `position.py`, `intake.py`, and `store/__init__.py`, `store/schema.py`, `store/repository.py`; keep `__init__.py` exporting the existing `marathon_app` Typer sub-app so `codeconv marathon --help` still loads. **DONE 2026-06-12**: 8 stubs created; removed obsolete `store.py`/`cadence.py`/`verify_spike.py`; `__init__.py` reduced to app+exit-codes+glue (024 commands stripped, wired per phase); `cli.py` registration help → feature 030; `codeconv marathon --help` loads.
- [X] T003 [P] Rewrite `codeconv/src/codeconv/marathon/models.py`: define dataclasses (`MarathonRun`, `StageRow`, `CheckpointRow`, `Item`, `Issue`, `Approval`, `VerificationTrace`, `Escalation`, `StatusReport`, `ResumePosition`, `Endpoint`, `MarathonEnv`, `ReconcileResult`) and vocabulary tuples (`ITEM_KINDS`, `MINI_KINDS` = 5 stages, `STAGE_ORIGINS`, `STAGE_STATUSES`, `ESCALATION_KINDS`); **delete** the hard-coded `STAGES` tuple and the `_STAGE_TO_BLOCK_KIND`/`_CANONICAL_STAGE` cadence maps (FR-001, D3). **DONE 2026-06-12**: pure module; all 13 dataclasses + 11 value-domain tuples; `STAGES`/cadence maps gone (verified by import assert).
- [X] T004 [P] Add a `codeconv/tests/conftest.py` fixture `marathon_store` (per `quickstart.md`): an isolated per-run store root on an NTFS tmp path with auto-teardown of the per-run bridge (mirror the existing `discover_repo` teardown pattern); plus a `marathon_run` helper to invoke `codeconv marathon …` in a subprocess. **DONE 2026-06-12**: `marathon_store` fixture + `marathon_run` helper + `kill_marathon_bridge` teardown added; obsolete 024 fixtures removed. Collection clean (609 tests, no import errors).

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: A working per-run isolated store reachable via `codeconv.bridge_client`, with schema + repository
+ JSON mirror — everything every user story persists through. **⚠️ No user story can begin until this is done.**

- [X] T005 Implement `codeconv/src/codeconv/marathon/env.py`: `MarathonEnv` (store_root, engine, repo_dir) and `resolve_env(run_id, *, data_dir=None)` — default marathon root = a guaranteed-NTFS user-level path (research F4); **assert the store root is OUTSIDE the repo git tree** (FR-027) and refuse otherwise. **DONE 2026-06-12**: default root `C:/pglite/marathon/<run_id>` (POSIX `~/.pglite/marathon`); `data_dir` (when given) IS the store root; refusal = `StoreRootInsideRepoError` on any git-tree ancestor; reuses `_check_data_dir_filesystem` fail-fast.
- [X] T006 Implement `codeconv/src/codeconv/marathon/store/schema.py::ensure_schema(engine)` — idempotent DDL exactly per `contracts/store-schema.sql` (all 9 tables, CHECKs, indexes); handle the mutually-referential `stage.item_id` ⇄ `item.blocks_stage_id` FK ordering (create tables, add the `stage.item_id` FK last) (D2). **DONE 2026-06-12**: contract-faithful DDL; `stage_item_fk` added last via guarded `DO` block (no `ADD CONSTRAINT IF NOT EXISTS` in PG); verified twice-clean + 9 tables live.
- [X] T007 Implement the per-run bridge composition in `codeconv/src/codeconv/marathon/store/repository.py`: a `_connect(env)` that calls `codeconv.bridge_client.acquire_or_discover(repo_root=store_root, data_dir=store_root/'pgdb')`, builds a single-writer SQLAlchemy engine (`pool_size=1`) on the sidecar endpoint, and runs `ensure_schema` once (D1, FR-028). **DONE 2026-06-12**: via the canonical `codeconv.db.engine.connect` accessor (pool_size=1 default); engine cached on `env.engine`; `DataDirFilesystemError` propagates distinctly, everything else → `StoreUnavailable` (chained).
- [X] T008 Implement repository CRUD in `codeconv/src/codeconv/marathon/store/repository.py` for `run`, `stage`, `checkpoint`, `item`, `issue` (insert/select/update), all writes inside `engine.begin()` transactions (single-writer). **DONE 2026-06-12**: `Repository` class; run upsert = idempotent re-attach (ON CONFLICT DO NOTHING); allowlisted updates; `insert_item` defaults `artifacts_dir=<store_root>/items/<id>` in-txn (dir creation stays T021's).
- [X] T009 Implement the **JSON mirror dual-write** in `repository.py`: every write also serialises to `<store_root>/json/<entity>/…` (layout per `data-model.md`); reads prefer primary, fall back to JSON when the per-run bridge is unreachable (FR-020 carry-over, FR-027 mirror). **DONE 2026-06-12**: mirror written from the RETURNING row (content-identical by construction); fallback reads for all five entities; fallback **write** supported for checkpoints (the append-only resume substrate), other entities surface `StoreUnavailable`. NOTE: `issue` rows mirror to `json/issues/<id>.json` — data-model.md's tree omits an issues/ dir while T009 mandates every-write mirroring (doc nit, flagged).
- [X] T010 Implement monotonic checkpoint sequencing in `repository.py`: `next_sequence_no = max(primary_max, fallback_max)+1`, UNIQUE `(run_id, sequence_no)`; record `store_origin` (D6, contract I1). **DONE 2026-06-12**: writer stamps `store_origin` (`primary`/`fallback`); fallback max scanned from `json/checkpoints/*.json` filtered by run_id.
- [X] T011 [P] `codeconv/tests/test_marathon_store_foundation.py` `@needs_bridge`: assert `ensure_schema` is idempotent (run twice, clean), a row written to PGLite is mirrored to JSON at the same content, and sequence_no is monotonic across primary+fallback. **DONE 2026-06-12**: 3/3 PASSED (incl. bridge-free FR-027 refusal test); collection clean (612). Per Gabi's 2026-06-12 directive, full-suite regression deferred to the end (T058) — only strategic/critical tests run per phase until Sunday 2026-06-14.

**Checkpoint**: per-run store stands up, persists, mirrors, and sequences. User stories can begin.

---

## Phase 3: User Story 1 — Run any stage shape, grow it mid-run (Priority: P1) 🎯 MVP

**Goal**: Register an arbitrary ordered stage list, append stages mid-run, and report progress against the
*current* total. **Independent Test**: register `[a,b,c]`; complete `a`; append `d`; resume reports
`done=1/4` and names `b` next — no harness code change to use a different stage list.

### Tests for US1 ⚠️ (write first, ensure they fail)
- [X] T012 [P] [US1] `codeconv/tests/test_marathon_stages.py` `@needs_bridge`: register `[a,b,c]` → `done=0/3`; complete `a`, append `d` → total becomes 4 and resume reports `done=1/4` next=`b` (FR-001/002/003, SC-002). **DONE 2026-06-12**: PASSED incl. CLI subprocess `resume --json` round-trip (context-loss path) + a second test for finalize/re-open (T017 edge).
- [X] T013 [P] [US1] `codeconv/tests/test_marathon_resume_unit.py` (bridge-free): unit-test the pure `derive_position(run, stages, checkpoints, open_issues)` for: started-not-complete ≠ done (FR-004); empty stage list → next=`register stages`; all-complete → next=`finalise run` (SC-008 determinism, edge cases). **DONE 2026-06-12**: 6/6 PASSED (+ order_key-governs-order, mini naming, determinism).

### Implementation for US1
- [X] T014 [US1] Implement `codeconv/src/codeconv/marathon/stages.py`: `register_run(run_id, stages=…, title, budget_ceiling, budget_unit, env)` (writes `run` + initial `stage` rows, `origin='registered'`; declarative manifest registration is deferred — out of scope) and `append_stage(run_id, name, env)` (`stage_index=max+1`, `order_key`=max+1) (FR-001/002). **DONE 2026-06-12**: idempotent re-attach skips existing names; duplicate names in one registration = usage error; head_commit captured best-effort from env.repo_dir.
- [X] T015 [US1] Implement `start_stage` and `checkpoint` in `codeconv/src/codeconv/marathon/checkpoint.py`: status flips pending→running→complete, set started_at/completed_at, append checkpoint with budget delta; `remaining_units==[]` ⇒ stage complete; update `stage.last_sequence_no` (FR-004). **DONE 2026-06-12**: re-start of running = idempotent; start of complete refused (rerun is US5); budget delta accumulates (substrate CHECK guards SC-006 until T046's halt path); `message` reserved for the US4 commit boundary; 024's pure `units_to_execute` preserved for US5 rerun. Replaced the dangling 024 `resume`/`ResumeReport` (dead store API).
- [X] T016 [US1] Implement the pure `derive_position(...)` and the I/O wrapper `resume_position(run_id, env)` in `codeconv/src/codeconv/marathon/position.py` per `contracts/resume-position.md` rules 1,2,5,6 (done/current-total/next-action; not-complete≠done) (FR-003/004, SC-008). **DONE 2026-06-12**: + `resume` alias (library-api name); mini naming ("run mini-plan for item-7") in rule 2; checkpoints param accepted for the US4/US5 refinements (T037).
- [X] T017 [US1] Implement `finalize(run_id, env)` in `stages.py`/`checkpoint.py`: set status finalized only when every current stage is complete; re-open cleanly if a later append/capture occurs (edge case "append during finalisation"). **DONE 2026-06-12**: in stages.py; empty-run finalise refused (rule 5); append_stage + capture_item both re-open a finalized run.
- [X] T018 [US1] Wire CLI subcommands `register`, `append-stage`, `stage-start`, `checkpoint`, `resume`, `position`, `finalize` in `codeconv/src/codeconv/marathon/__init__.py` as thin 1:1 wrappers over the library functions (FR-025), with exit codes per `contracts/cli.md`. **DONE 2026-06-12**: `_execute` guard maps usage/fs-guard→64, escalation→2 (payload still emitted), unexpected→70; library imports stay inside command bodies (help stays light); `position` = alias of `resume`.

**Checkpoint**: US1 fully functional — workload-agnostic stages, growable total, deterministic resume.

---

## Phase 4: User Story 2 — Capture emergent work, route through a mini-pipeline (Priority: P1)

**Goal**: Capture typed mid-run items; each expands into a 5-stage mini-pipeline feeding the marathon's
implement; blocking prereqs route ahead; advisory/default-deny. **Independent Test**: capture a
`missing-prerequisite` blocking stage `c`; resume names `mini-specify` for that item next and orders its
mini-stages before `c`; total grew by 5; nothing auto-advances.

### Tests for US2 ⚠️
- [X] T019 [P] [US2] `codeconv/tests/test_marathon_intake.py` `@needs_bridge`: capture a blocking `missing-prerequisite` → 5 mini-`stage` rows with `order_key` strictly below the blocked stage; resume next=`run mini-specify for item-N` *before* the blocked stage; total grew by 5; no auto-advance (FR-005/006/009/010, SC-003/SC-004). **DONE 2026-06-12**: PASSED (+ FR-008 artifacts-dir assertion).
- [X] T020 [P] [US2] `codeconv/tests/test_marathon_intake_edge.py` `@needs_bridge`: stacked blocking items on the same stage get distinct fractional bands (deterministic, no collision); a non-blocking item orders after the current stage; capturing against an already-complete stage raises/escalates `prereq_against_completed_stage` (edge cases). **DONE 2026-06-12**: PASSED — 10 distinct keys, item2 band strictly after item1's, both ahead of `c`; escalation row written, NO item row created.
- [X] T020a [P] [US2] `codeconv/tests/test_marathon_intake_resume.py` `@needs_bridge`: interrupt between two mini-stages of an item, then assert resume names the exact next incomplete mini-stage and re-runs no completed mini-stage; assert items + mini-stages enjoy the same checkpoint / scoped-commit / reconcile / resume guarantees as ordinary stages (FR-011, edge case "Resume mid-mini-pipeline"). **DONE 2026-06-12**: PASSED — fresh-env + fresh-subprocess resume identical; drives all 5 minis in order; mini_analyze → item `done` → next = blocked stage. (Scoped-commit/reconcile parity verified when those land — US4/US5.)

### Implementation for US2
- [X] T021 [US2] Implement `capture_item(run_id, kind, title, description, blocks_stage, env)` in `codeconv/src/codeconv/marathon/intake.py`: insert `item`, create `<store_root>/items/<id>/` (FR-008), validate `kind ∈ ITEM_KINDS` (FR-005). **DONE 2026-06-12**: hyphenated CLI kinds normalised; `--blocks` with a non-missing-prerequisite kind = usage error; capture on a finalized run re-opens it.
- [X] T022 [US2] Implement the 5-stage mini-expansion in `intake.py`: append `mini_specify…mini_analyze` `stage` rows (`origin='mini'`, `item_id`, `mini_kind`); **no per-item implement** (FR-006, D4). **DONE 2026-06-12**: stage names `item-<id>:<mini_kind>`.
- [X] T023 [US2] Implement fractional `order_key` routing in `intake.py`: blocking missing-prerequisite → evenly spaced keys in the gap *before* the blocked stage; non-blocking → after current max; already-complete target → escalate (FR-010, `contracts/emergent-intake.md`). **DONE 2026-06-12**: gap floor = highest existing key below the blocked stage ⇒ stacked items get distinct deterministic bands; escalation written via new `Repository.insert_escalation`/`list_escalations` substrate (policy port stays T048) + `PrereqAgainstCompletedStage` (CLI exit 2).
- [X] T024 [US2] Extend `position.py` (rules 2,3,4) to name an item's next incomplete mini-stage and surface blocking mini-stages ahead of the blocked stage; mark item `done` when its `mini_analyze` checkpoints, exposing its artifacts to the marathon implement stage (FR-007/009). **DONE 2026-06-12**: rule-2 mini naming in `derive_position`; rules 3/4 emergent from capture-time order_keys; item-done flip in `checkpoint.py`.
- [X] T025 [US2] Wire CLI `capture` subcommand in `__init__.py` (1:1 with `capture_item`); resume/position already surface mini-stages (FR-025). **DONE 2026-06-12**: exit 2 on `prereq_against_completed_stage` with the escalation id emitted.

**Checkpoint**: US2 functional — emergent items captured, mini-pipelined, routed, advisory; durability parity (FR-011) inherited from the foundational store and **explicitly verified by T020a**.

---

## Phase 5: User Story 3 — Durable store survives crashes & stale locks (Priority: P2)

**Goal**: Background keeper publishes an endpoint, stops gracefully, auto-recovers stale residue, and refuses
a second writer. **Independent Test**: start (endpoint published); kill abruptly; next op recovers with no
manual deletion; a second concurrent writer is refused with a message distinct from a recoverable stale
condition.

### Tests for US3 ⚠️
- [X] T026 [P] [US3] `codeconv/tests/test_marathon_keeper.py` `@needs_bridge`: `start_keeper` publishes a reachable endpoint; `stop_keeper` flushes so the next start needs no recovery; after an abrupt kill leaving stale residue, the next op auto-recovers (no manual delete) (FR-012/013/014, SC-005). **DONE 2026-06-12**: PASSED (+ doctor live-store assertions; durable rows survive the kill). 🔴 **Exposed a latent `bridge_client` bug**: `request_force_shutdown` wrote `<data_dir>/.shutdown` (inside) while the bridge polls the SIBLING `${pgdir}.shutdown` — marker never seen, bridge never exited. Fixed client-side to the sibling path (matches the 012 lock/consumers sibling convention + PGLite data-dir purity); function was unexercised by any test until now.
- [X] T027 [P] [US3] `codeconv/tests/test_marathon_keeper_singlewriter.py` `@needs_bridge`: a second concurrent writer is refused/serialised (`ConcurrentWriter`) with a message **distinct** from a stale-residue condition; a *live* bridge child is never killed during recovery (FR-015/016, SC-006). **DONE 2026-06-12**: PASSED — subprocess write exits 2 ("concurrent…writer", no "stale"); subprocess READ stays legal; recover on a live bridge returns the same pid.

### Implementation for US3
- [X] T028 [US3] Implement `start_keeper(run_id, env)` in `codeconv/src/codeconv/marathon/keeper.py`: `bridge_client.acquire_or_discover(..., data_dir=store_root/'pgdb')`, surface the sidecar endpoint as `Endpoint(host,port,pid,data_dir)`, register this process as consumer (FR-012, D5). **DONE 2026-06-12**.
- [X] T029 [US3] Implement `stop_keeper(run_id, env)`: `bridge_client.request_force_shutdown(data_dir=…)` (non-destructive `.shutdown` marker; bridge flushes and exits on next tick) so the next start needs no recovery (FR-013). **DONE 2026-06-12**: also disposes this process's engine + releases its writer lock; waits (bounded 30s) for the sidecar unlink so "no recovery needed" is observable (~1s in practice).
- [X] T030 [US3] Implement `recover_keeper(run_id, env)` + the recovery path inside `engine_for`: re-`acquire_or_discover` to clear stale heartbeat/lock residue automatically; refuse recovery if a live bridge child is attached (raise `ConcurrentWriter`, do not kill) (FR-014/015, edge case "endpoint stale but process dead"). **DONE 2026-06-12**: stale residue cleared by acquire's heartbeat+TCP checks + stale-lock retry; a live bridge is fast-path CONSUMED (same pid) — the refuse-don't-kill invariant; engine_for pings a cached engine and re-acquires a dead one.
- [X] T031 [US3] Implement single-writer + error taxonomy in `keeper.py`/`repository.py`: consumer kernel-fd lock = single writer; surface `StoreUnavailable`/`IntegrityFailure` distinctly from recoverable stale-residue, and fail fast (exit 64) on non-NTFS/ReFS `data_dir` via the existing `_check_data_dir_filesystem` guard (FR-015/016). **DONE 2026-06-12**: per-run kernel-fd `writer.lock` at the store root, acquired lazily on FIRST write, held for process life (kernel-released on crash ⇒ no stale lock residue possible); reads never take it; `ConcurrentWriter` → CLI exit 2.
- [X] T032 [US3] Wire CLI `keeper start|stop|recover` and `doctor` subcommands in `__init__.py` (endpoint, active store, last-seq per store, open escalations, budget headroom) (FR-025, `contracts/cli.md`). **DONE 2026-06-12**: `keeper` is a nested Typer app; `doctor` is read-only and NEVER spawns (builds an engine on the live sidecar only; `Repository(env, autostart=False)` diagnostics mode).

**Checkpoint**: US3 functional — self-healing per-run keeper, single-writer, clear error taxonomy.

---

## Phase 6: User Story 4 — Commit boundaries & status cadence on the new model (Priority: P2)

**Goal**: Each completed block (ordinary/dynamic/mini) is a scoped commit boundary; resume re-drives an
uncommitted-but-complete block; a parseable status line reports against the current total. **Independent
Test**: complete a dynamic stage and a mini-stage — each scoped-commits only its paths; status line reports
`done=k/N`; interrupt after complete-before-commit → resume re-drives that one commit.

### Tests for US4 ⚠️
- [X] T033 [P] [US4] `codeconv/tests/test_marathon_commit.py` `@needs_bridge`: a checkpoint stages ONLY its named paths (no `git add -A`, no force, no `--no-verify`); a non-FF push writes `push_blocked` and does not force; a complete checkpoint with `commit_sha IS NULL` is re-driven on resume before new work (FR-017/018, SC-007). **DONE 2026-06-12**: PASSED — scratch work-repo + bare origin; unrelated file stays untracked; remote's advance survives (never forced); re-drive named first then cleared by `redrive_commit`.
- [X] T034 [P] [US4] `codeconv/tests/test_marathon_status.py` (bridge-free where possible): the status line matches the fixed grammar in `contracts/status-line.md`, with `<n>` = current total (FR-019, SC-002). **DONE 2026-06-12**: 3/3 bridge-free (contract example byte-exact, budget-unset, split-on-pipes order).

### Implementation for US4
- [X] T035 [US4] Port the scoped-commit mechanism into `codeconv/src/codeconv/marathon/gitblock.py` over `checkpoint` rows: `commit_block` stages only `committed_paths` (requires `preauth_commit_push`, hooks run, never blanket-add/force/rewrite/`--no-verify`); record `commit_sha`+`committed_paths` on the checkpoint (D9, FR-017). **DONE 2026-06-12**: rewrite over `Repository.update_checkpoint`; crash-after-commit re-drive detects already-clean paths and records HEAD; `redrive_commit` drives exactly one pending boundary, idempotent.
- [X] T036 [US4] Implement `push_block` + `push_blocked` escalation in `gitblock.py` (never `--force`/`--force-with-lease`; non-FF → escalation, set `push_escalation`) (FR-017). **DONE 2026-06-12**.
- [X] T037 [US4] Implement the re-drive guard in `position.py`/`checkpoint.py`: complete checkpoint with `commit_sha IS NULL` ⇒ next action `re-drive scoped commit for <stage>` before new work (FR-018, `contracts/checkpoint-commit.md`). **DONE 2026-06-12**: pure `_pending_commit_stage` (rule 2a) gated on the standing grant + non-empty `committed_paths` (no false re-drive for path-less or ungranted checkpoints); takes priority over all other next actions.
- [X] T038 [US4] Implement `status_line` + `emit_status` in `codeconv/src/codeconv/marathon/status.py` per `contracts/status-line.md` (four fields, current total, single next action); emit at every stage boundary + on demand (FR-019). **DONE 2026-06-12**: pure `build_status_line` (bridge-free testable); `emit_status` persists via new `Repository.insert_status_report` (mirror `status/<id>.json`); `checkpoint` emits at every block completion (024 D8 doctrine: cadence = the orchestration loop).
- [X] T039 [US4] Wire CLI `status` (`--emit`) and ensure `checkpoint` invokes the scoped commit boundary; exit 2 on `push_blocked` (FR-025, `contracts/cli.md`). **DONE 2026-06-12**: `status` prints the BARE line (parse contract) or JSON-wraps; `checkpoint` payload carries commit_sha/pushed/push_escalation and exits 2 on `push_blocked`.

**Checkpoint**: US4 functional — commit boundary + status reconciled with dynamic/mini stages.

---

## Phase 7: User Story 5 — Preserve 024 strengths under the new model (Priority: P3)

**Goal**: Approval gate, per-block/subagent re-run, budget ceiling, verification-trace substrate, and
dual-store reconciliation all work over the refined stage model — zero regressions (SC-009). **Independent
Test**: run an existing-style marathon end-to-end; gate, re-run, budget ceiling, trace, reconciliation all
behave as in 024, now over registrable/dynamic/mini stages.

### Tests for US5 ⚠️ (regression-guarding)
- [X] T040 [P] [US5] `codeconv/tests/test_marathon_gate.py` `@needs_bridge`: gate decision durably recorded (append-only, supersedes chain); resume short-circuits an already-`approved` gate (FR-020, AS1). **DONE 2026-06-12**: PASSED — present idempotent (1 pending row), change retained, re-present supersedes the change row, approve → resume names "run a" + re-present reports approved without a new row.
- [X] T041 [P] [US5] `codeconv/tests/test_marathon_rerun.py` `@needs_bridge`: `rerun_block` resumes from the block's last checkpoint (not run start); `rerun_subagent` re-runs only the named subagent and reports untouched siblings (FR-021, AS2). **DONE 2026-06-12**: PASSED (+ changed-input unit treated as new work; workflow runId echoed via `record_run_linkage` for the cached-prefix cascade).
- [X] T042 [P] [US5] `codeconv/tests/test_marathon_budget.py` (bridge-free `Budget` + `@needs_bridge` halt path): advancing past the ceiling halts and writes `budget_exceeded` (exit 2), never overruns; substrate CHECK enforces `spent<=ceiling` (FR-022, SC-006, AS3). **DONE 2026-06-12**: PASSED — refusal leaves spend untouched; safe checkpoint written; store CHECK raises IntegrityError on overrun.
- [X] T043 [P] [US5] `codeconv/tests/test_marathon_trace.py` `@needs_bridge`: `write_trace` is append-only (UNIQUE `(run,subject,refine_seq)`, never overwrites); `list_traces` ordered by `(subject, refine_seq)` (FR-023). **DONE 2026-06-12**: PASSED — per-subject auto refine_seq; duplicate refused; earlier iteration intact; mirror carries every trace.
- [X] T044 [P] [US5] `codeconv/tests/test_marathon_reconcile.py` `@needs_bridge`: PGLite-vs-JSON-mirror fast-forwards the stale store; a true fork writes `store_divergence` and never silently picks (exit 2) (FR-024, AS4, SC-007 fork case). **DONE 2026-06-12**: PASSED — in_sync → FF primary from fallback (provenance preserved, stores converge byte-wise) → true fork escalates; neither store rewritten; resume reports `diverged` (exit-2 signal).

### Implementation for US5
- [X] T045 [US5] Port `codeconv/src/codeconv/marathon/gate.py` onto `stage`/`approval` rows: `present_gate`, `record_decision`, `approval_state` (approved|changed|awaiting|None); resume short-circuit when `approved` (FR-020). **DONE 2026-06-12**: stage-status linkage = resume rule 2b ("approve gate for <stage>" while awaiting_approval; approve flips back to pending — short-circuit is pure durable-row state).
- [X] T046 [US5] Port `codeconv/src/codeconv/marathon/orchestrate.py`: `Budget` (ceiling/remaining/add-raises), `advance_budget_or_halt` (checkpoint + `budget_exceeded` escalation), `rerun_block`, `rerun_subagent`, `require_workflow_optin` — all keyed off the new `stage`/`checkpoint` rows (FR-021/022). **DONE 2026-06-12**: per-STAGE last checkpoint (no marathon-wide-position ambiguity); `budget_exceeded` is now the dedicated kind (024 borrowed `stage_flagged`); 024's `finalize_block` dropped — D9 folded the commit boundary onto `checkpoint()` itself.
- [X] T047 [US5] Port `codeconv/src/codeconv/marathon/trace.py`: `write_trace` (append-only, auto `refine_seq`), `list_traces` ordered (FR-023). **DONE 2026-06-12**: over new `Repository.insert_trace`/`list_traces` (mirror `traces/<subject>-<seq>.json`).
- [X] T048 [US5] Port `codeconv/src/codeconv/marathon/escalation.py`: `write_escalation` (dual-store mirror), `open_escalations`, auto-decision policy; add the new kinds `budget_exceeded` and `prereq_against_completed_stage` (FR-022). **DONE 2026-06-12**: decision table extended for both new kinds; budget_ceiling now maps to `budget_exceeded`.
- [X] T049 [US5] Implement `reconcile(run_id)` in `repository.py`: compare per-run PGLite vs per-run JSON mirror by `sequence_no`; fast-forward the stale store (FK-safe); escalate a true fork (`store_divergence`); never silently pick (FR-024, D6). **DONE 2026-06-12**: shared-seq content mismatch = fork; FF-primary inserts preserve seq/store_origin/created_at then re-mirror so stores converge byte-wise; `resume_position` reconciles FIRST (rule 7: fork ⇒ `diverged` ⇒ CLI exit 2).
- [X] T050 [US5] Wire CLI `gate`, `rerun`, `trace`, `reconcile` subcommands in `__init__.py` (1:1 parity; exit 2 on escalation/fork) (FR-025). **DONE 2026-06-12**: `--stage` takes the stage NAME (consistent with stage-start/checkpoint; cli.md's `<id>` resolved via `--run`+name — noted as deliberate). Full marathon set after the resume change: 26/26 green.

**Checkpoint**: US5 functional — all five 024 strengths preserved over the refined model.

---

## Phase 8: Polish & Cross-Cutting Concerns

- [X] T051 [P] Add `codeconv/tests/test_marathon_cli_parity.py`: assert every CLI subcommand maps to exactly one public library function and vice-versa (FR-025). **DONE 2026-06-12**: parity table = contracts/cli.md verbatim; mechanical both-direction check via Typer introspection + callback-source import scan (bridge-free); `position` handled as the one documented alias; flag-variant rows (gate/rerun/status) carry their per-variant pair. 3/3; marathon set 29/29.
- [X] T052 [P] Add `codeconv/tests/test_marathon_resume_determinism.py`: the resume position is byte-identical when computed twice from durable state (simulating full-context vs total-context-loss) (SC-008). **DONE 2026-06-12**: canonical-JSON byte compare across (1) warm in-process, (2) fresh env + engine cache (`reset_engine_cache_for_tests`), (3) a separate Python process sharing only the durable store; state exercises rules 2/3 (blocking minis ahead of `c`), budget + open issue. 1/1.
- [ ] T053 Add the single-head regression guard `codeconv/tests/test_migration_marathon_no_new_head.py`: assert the shared-repo Alembic head is still `0010` (the refined harness adds **no** shared-cluster migration — Constitution VI-a, D2).
- [ ] T054 [P] Confirm no LM/API path is introduced: a grep test asserting zero `OPENAI_API_KEY`/`litellm`/`openai` tokens on any marathon code path (Constitution V).
- [ ] T055 Update `codeconv` docs / `CLAUDE.md` "marathon-stage-harness" references to the refined data-driven model + per-run isolated store (point at this feature's contracts); note 024 schema is inert history (VIII single-source-of-truth).
- [ ] T056 [P] Update the `/marathon-stage-harness` skill (`.claude/skills/`) to drive the refined CLI (`register`/`append-stage`/`capture`/`keeper`), keeping the Restart-Resume protocol pointer accurate.
- [ ] T057 Run `quickstart.md` end-to-end on a scratch run to validate the full drive (register → grow → capture → crash/recover → preserved strengths → finalize); record results.
- [ ] T058 Full suite green gate: run `codeconv` pytest (`--test-concurrency=1`) and the REPL suite baseline per CLAUDE.md Test Protocol; confirm zero regressions before ship.

---

## Dependencies & Execution Order

### Phase dependencies
- **Setup (P1)** → no deps.
- **Foundational (P2)** → depends on Setup; **BLOCKS all user stories** (every story persists through the per-run store).
- **US1 (P3 phase)** → after Foundational. MVP.
- **US2** → after Foundational; depends on US1 (mini-stages *are* dynamic stages — uses `append`/`order_key`/`position` from US1).
- **US3** → after Foundational; largely independent of US1/US2 (hardens the keeper the foundational store already uses). Can run in parallel with US1/US2.
- **US4** → after Foundational + US1 (commit boundary fires at checkpoint; status line reads the position). Independent of US2/US3.
- **US5** → after Foundational + US1 (ports key off `stage`/`checkpoint`). Independent of US2/US3/US4.
- **Polish (P8)** → after all targeted stories.

### Critical-path note
US1 is the linchpin: US2/US4/US5 all build on its `stage`/`checkpoint`/`position` primitives. US3 is the one
story that can proceed fully in parallel with US1 once Foundational is done.

### Within each story
Tests first (must fail) → models/store → library functions → CLI wiring.

### Parallel opportunities
- Setup: T003, T004 in parallel.
- Foundational test T011 parallels nothing destructive.
- Within a story, all `[P]` test tasks run together; library files in different modules marked `[P]` parallel.
- US3 phase can run concurrently with US1/US2 (different files: `keeper.py` vs `stages.py`/`intake.py`).

---

## Parallel Example: User Story 1
```
# Tests together (write first, ensure fail):
Task: T012 stage register/append/grow integration test (test_marathon_stages.py)
Task: T013 derive_position pure unit tests (test_marathon_resume_unit.py)
```

---

## Implementation Strategy

### MVP (US1 only)
1. Phase 1 Setup → 2. Phase 2 Foundational (CRITICAL) → 3. Phase 3 US1 → **STOP & validate**: a second
   workload could adopt the harness with its own stage list, unchanged (SC-001). Demo.

### Incremental delivery
Foundation → US1 (MVP) → US2 (emergent work) → US3 (keeper robustness) → US4 (commit/status) → US5
(preserved strengths). Each story is independently testable and adds value without breaking the prior.

### Greenfield guardrails (cross-cutting)
- Never read 024's shared-cluster `marathon.*` rows (T001 precondition; D8).
- No new shared-cluster Alembic migration (T053; D2).
- No LM/external-API path (T054; Constitution V).
- Every commit stages only named paths (T033; SC-007).

## Notes
- `[P]` = different file, no incomplete-task dependency.
- `--test-concurrency=1` mandatory (PGLite cold-init ~7 s on Windows).
- Telemetry mirroring is out of scope / no-op for this feature (research F3) — do not take a hard dependency.
- Commit after each task or logical group (scoped paths only).
