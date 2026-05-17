# Tasks — codeconv-builder (018)

**Feature**: Unified, DBOS-durable Conversion Workbench
**Branch**: `018-codeconv-builder` | **Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

Tests are generated: the spec defines per-story Independent Tests and
`CLAUDE.md` mandates baseline-before / re-run-after. All paths absolute from
repo root `D:\BSTDEV\research\GLP\GLPNET\`. Invoke as `pytest codeconv/tests/`
— **no `--data-dir`** (not a pytest option; conftest registers only
`--run-perf`). `@needs_bridge` tests get a throwaway cluster under pytest's
`tmp_path` via the `isolated_repo` fixture (OS temp dir — never `<repo>/.pgdb`,
verified `codeconv/tests/conftest.py`), so CLAUDE.md's `--data-dir` *CLI*
mandate is N/A to the pytest run; bridge tests serial (012 lock; PGLite
cold-init ~7 s).

**Governing constraint (D2, Gabi emphatic):** no task may rewrite or replace a
proven 015/016/017 flow; existing tool entrypoints are called verbatim inside
DBOS steps; the durable layer is additive and isolated to `codeconv/src/codeconv/durable/`.

---

## Phase 1: Setup

- [X] T001 Baseline: run `pytest codeconv/tests/` and record green/known-fail counts in `specs/018-codeconv-builder/quickstart.md` (Test baseline — CLAUDE.md §Test Protocol) before any change — recorded 2026-05-17: no-bridge guard 62 pass/1 skip/0 fail; bridge tests green per-test, full-suite contention is pre-existing harness defect (not 018)
- [X] T002 Confirm `dbos` is a resolvable dependency in `codeconv/.venv` and `codeconv/pyproject.toml`; no version bump (no new dependency per plan Technical Context) — verified: dbos 2.21.0 in venv + pyproject deps, no pin/bump

## Phase 2: Foundational (BLOCKING — `codeconv migrate` is currently broken)

- [X] T003 Fix migration chain: in `codeconv/src/codeconv/db/migrations/versions/0003_dart_plans.py` change `revision "0003"→"0004"` and `down_revision "0002"→"0003"` only (filename unchanged), per `contracts/migration_linearization.md` — done; docstring header synced; offline-verified single head
- [X] T004 Create `codeconv/src/codeconv/db/migrations/versions/0005_codeconv_builder.py` (`revision "0005"`, `down_revision "0004"`) with `CREATE TABLE IF NOT EXISTS` for `builder_runs`, `research_findings` (`construct_key` **UNIQUE** — cache invariant FR-012/FR-024; insert via `ON CONFLICT (construct_key) DO NOTHING`), `conversion_idioms`, `dart_convspecs` + partial index, per `contracts/builder_schema.md` and `data-model.md` §2; downgrade drops in reverse order — done; offline `ScriptDirectory` check: HEADS=['0005'], linear 0001→0002→0003→0004→0005
- [X] T005 [P] Test `codeconv/tests/test_migration_single_head.py` (@needs_bridge): fresh cluster → `alembic upgrade head` exit 0, exactly one head `0005`, linear history, re-run idempotent (FR-015/SC-004)
- [X] T006 [P] Test `codeconv/tests/test_schema_isolation.py` (@needs_bridge): after `0005` every new relation is in `codeconv` schema; zero Alembic-authored `public`/`dbos` objects
- [X] T007 Create shared `codeconv/src/codeconv/workspace.py` — single read facade over `codeconv.workspace_settings`/`excluded_directories`/`phase_*` (016), delegating, NOT changing what tools read (FR-006/FR-022, D2) — done; mirrors init/workflow `_read_settings` SQL verbatim; read-only, no mutation
- [X] T008 Create shared `codeconv/src/codeconv/status.py` — unified per-file state enum + escalation vocabulary `{not_started｜blocked_on_deps｜analysed｜specced｜scaffolded｜converted｜escalated｜complete}` as a pure projection helper (FR-017/FR-022), per `contracts/status_trace_contract.md` — done; pure `project_file_state(FileFacts)` encodes data-model §5; smoke 9/9
- [X] T009 Create `codeconv/src/codeconv/durable/__init__.py` — DBOS workflow/step registry + deterministic workflow-id derivation (`builder:{ws}:{epoch}`, `file:{h}`, `scc:{h}`), per `contracts/dbos_workflow_model.md` §Taxonomy/R9
- [X] T010 [P] Test `codeconv/tests/test_workflow_id_determinism.py` (pure, no bridge): id derivation stable across processes/inputs (FR-004/SC-002)
- [X] T011 Create `codeconv/src/codeconv/durable/steps.py` — `@DBOS.step` wrappers that call the existing discover/depgraph/scaffold/init/mirror/planagents entrypoints **verbatim** and write the existing two-phase + tombstone projection (R1/R8, D2; replay-safe — no LLM/network in step)
- [X] T012 Create `codeconv/src/codeconv/durable/workflows.py` — outer builder `@DBOS.workflow` + per-file/per-SCC child `@DBOS.workflow`; SCC = one indivisible unit (FR-002/FR-003)
- [X] T013 Create `codeconv/src/codeconv/durable/queue.py` — DBOS `Queue` (default concurrency 1, serial through 012 bridge lock — R12, D2), startup `recover_pending_workflows` + explicit-resume helper
- [X] T014 Create `codeconv/src/codeconv/durable/trace.py` — read-only projection over `dbos.workflow_status`/`operation_outputs` joined via `builder_runs` (D1=a trace), per `contracts/status_trace_contract.md`
- [X] T015 Replace the no-op `register()` in `codeconv/src/codeconv/tools/{discover,depgraph,init,scaffold,mirror,planagents}/workflow.py` with delegation to `durable/` registration — behaviour of each entrypoint UNCHANGED (FR-016/SC-005, D2 hard gate)
- [X] T016 Extend `codeconv/src/codeconv/tools/discover/tombstone.py` `_FIELD_ORDER` append-only with `convspec_started_at｜convspec_completed_at｜spec_path｜convspec_open_escalation_count｜builder_outer_workflow_id｜builder_file_state` AFTER 017's keys (data-model §3) — done; `_FEATURE_018_KEYS` added to `_PRESERVED_APPENDED_KEYS`; test_tombstone.py 4/4 green (append-only, no regression)
- [X] T017 [P] Test `codeconv/tests/test_tombstone_stamp_rebuild.py` (@needs_bridge): append-only `_FIELD_ORDER` stamp→rebuild→stamp is a fixed point (FR-021)
- [X] T018 [P] Test `codeconv/tests/test_capability_preservation.py` (@needs_bridge): every 015/016/017 tool entrypoint still reachable + unchanged behaviour after T015 (FR-016/SC-005)

**Checkpoint**: migration single-head + DBOS scaffolding + shared model in place; no story logic yet.

## Phase 3: User Story 1 — One durable command drives the whole pipeline (P1)

**Goal**: single resumable `codeconv builder` over the 015 topo/SCC order.
**Independent test**: kill mid-run, re-run, 0 completed files redone, final == uninterrupted.

- [X] T019 [US1] Create `codeconv/src/codeconv/tools/builder/__init__.py` — Typer `app` (`run｜resume｜status｜trace｜retry｜redrive｜aggregate-escalations`) + `register_workflows`, auto-discovered by runner (012 FR-006), per `contracts/builder_cli.md`
- [X] T020 [US1] Create `codeconv/src/codeconv/tools/builder/orchestrate.py` — deterministic frontier driver consuming feature-015 `dart_depgraph` read-only (MUST NOT recompute order/SCC/status), emitting next ready batch in topo+SCC order
- [X] T021 [US1] Create `codeconv/src/codeconv/tools/builder/workflow.py` — `register()` activates the outer/child workflows via `durable/` (no longer a no-op)
- [X] T022 [US1] Implement `builder run` / `builder resume` with deterministic workflow-id reuse (resume not restart) + `nothing-to-convert` clean exit code 0 (FR-004/FR-020) and `--restart-run` explicit non-default (R13)
- [ ] T023 [P] [US1] Test `codeconv/tests/test_builder_frontier.py` (@needs_bridge): files processed in dep order; no file before its deps/SCC group (FR-002/SC-003)
- [ ] T024 [P] [US1] Test `codeconv/tests/test_builder_resume.py` (@needs_bridge): kill mid-step → recovery skips completed steps, resumes at interrupted stage (FR-003)
- [ ] T025 [P] [US1] Test `codeconv/tests/test_builder_idempotent_rerun.py` (@needs_bridge): resumed run state == uninterrupted run (SC-002)
- [ ] T026 [P] [US1] Test `codeconv/tests/test_builder_nothing_to_do.py` (@needs_bridge): empty/again-complete subtree exits "nothing to convert", code 0 (FR-020)
- [ ] T027 [US1] Create `.claude/skills/codeconv-builder/SKILL.md` — venv/repo-root resolver + durable-orchestration loop + **awaiting-agent** handler (detects `needs_agent_work` via `builder status`/exit code, not a caught exception), per `contracts/builder_cli.md` (justified deviation, plan Complexity Tracking)

**Checkpoint**: US1 independently testable — durable pipeline resumes; convspec stage may still return `needs_agent_work` (US2 wires the agent).

## Phase 4: User Story 2 — convspec deep analysis + research → researched spec (P1)

**Goal**: per-file deep analysis + official-docs research → structured+rationale spec; growing idiom KB.
**Independent test**: file with `Stream`/async, no idiom → spec cites analysis + official-doc research, records idiom; 2nd file reuses idiom, not re-researched.

- [ ] T028 [US2] Create `codeconv/src/codeconv/tools/convspec/__init__.py` — Typer `app` (`status｜next｜ingest｜record-idiom｜aggregate-escalations`) + `register_workflows`, auto-discovered
- [ ] T029 [US2] Create `codeconv/src/codeconv/tools/convspec/readiness.py` — pure convspec-readiness predicate + SCC batch (parallels 017 readiness; no bridge)
- [ ] T030 [P] [US2] Test `codeconv/tests/test_convspec_readiness.py` (pure): predicate + SCC batch correctness
- [ ] T031 [US2] Create `codeconv/src/codeconv/tools/convspec/idioms.py` — KB lookup-before-research, record, idiom↔research & idiom↔idiom conflict detection, per `contracts/convspec_idiom_schema.md` (FR-012/013/014/024)
- [ ] T032 [US2] Create `codeconv/src/codeconv/tools/convspec/artefact.py` — structured-block + embedded-rationale artifact path/validation; spec-only, rejects any C# emission (FR-011/FR-023), per `contracts/convspec_artifact_format.md`
- [ ] T033 [US2] Create `codeconv/src/codeconv/tools/convspec/workflow.py` — `register()` exposes the deterministic convspec `@DBOS.step`: idiom lookup → artifact present? record+return : **return `needs_agent_work` sentinel** (a successful replay-safe step output, NOT a raised exception — the workflow surfaces a durable awaiting-agent status; R3)
- [ ] T034 [US2] Implement `convspec ingest` + two-phase `dart_convspecs` writes (`*_completed_at` terminal-only) + drift `sha256` + `--respec` (FR-003/FR-019)
- [ ] T035 [P] [US2] Test `codeconv/tests/test_convspec_ingest_step.py` (@needs_bridge): deterministic ingest; `needs_agent_work` result (not a raised exception); replay-safe; no C# accepted (FR-009/FR-023)
- [ ] T036 [P] [US2] Test `codeconv/tests/test_convspec_idiom_kb.py` (@needs_bridge): lookup-before-research; reuse; ≥95% recurring via idiom (FR-012/SC-007)
- [ ] T037 [P] [US2] Test `codeconv/tests/test_convspec_idiom_conflict.py` (@needs_bridge): idiom↔research & idiom↔idiom → escalation, 0 silent guesses (FR-013/FR-014/SC-008)
- [ ] T038 [P] [US2] Test `codeconv/tests/test_convspec_research_provenance.py` (@needs_bridge): authoritative=official-docs; cached construct never re-researched; offline-reproducible (FR-024)
- [ ] T039 [US2] Create `.claude/skills/codeconv-convspec/SKILL.md` — analysis sub-agent + SEPARATE research sub-agent prompt contracts (escalate-don't-guess, official-docs-authoritative, verbatim provenance), per `contracts/agent_orchestration.md`
- [ ] T040 [US2] Wire builder skill **awaiting-agent** handler (detects `needs_agent_work` via `builder status`/exit code) → spawn analysis (≤`--limit`, SCC-batched) + on-KB-miss separate research sub-agent; re-drive recovers same workflow ids (FR-002/FR-009/FR-010)

**Checkpoint**: US1+US2 deliver the MVP — durable pipeline with researched per-file specs + idiom KB.

## Phase 5: User Story 3 — One coherent surface replaces 015/016/017 fragmentation (P2)

**Goal**: one workspace/schema/status surface; single migration head; no capability lost.
**Independent test**: fresh PG17 → single-head migrate; each child tool runs through unified surface with consistent workspace/status.

- [ ] T041 [US3] Route builder + convspec workspace reads through `workspace.py`; assert no per-tool ad-hoc workspace read remains on the builder path (FR-006/FR-022, D2 — tools' own reads unchanged)
- [ ] T042 [US3] Route all stage status through `status.py` single vocabulary; `convspec aggregate-escalations` + `builder aggregate-escalations` emit one `.codeconv/conversion-idioms/_escalations-report.md` (FR-013/FR-014)
- [ ] T043 [P] [US3] Test `codeconv/tests/test_status_projection.py` (@needs_bridge): unified state reconciles durable state, snapshot <5 s (FR-017/SC-009)
- [ ] T044 [P] [US3] Extend `test_capability_preservation.py` assertions: every 015/016/017 capability reachable via the unified surface (FR-016/SC-005)

**Checkpoint**: consolidation defects removed; unified surface verified.

## Phase 6: User Story 4 — Per-file progress, escalations, recovery observable (P3)

**Goal**: query state, retry one file, re-drive frontier without corrupting others.
**Independent test**: mid-run query reconciles; force one escalation, rest of frontier still progresses.

- [ ] T045 [US4] Implement `builder status` (per-file + counts) and `builder trace --file/--run` over `durable/trace.py` (DBOS history; D1=a), per `contracts/status_trace_contract.md`
- [ ] T046 [US4] Implement `builder retry --file` / `builder redrive` — single file/SCC re-drive without disturbing other files' durable state (FR-018)
- [ ] T047 [US4] Implement tombstone↔DB divergence check on entry: drift → exit 4, escalate "stale — rebuild required", refuse to proceed (FR-019)
- [ ] T048 [P] [US4] Test `codeconv/tests/test_builder_trace.py` (@needs_bridge): per-file/per-run step history exposed; joins correctly after kill/resume (D1=a)
- [ ] T049 [P] [US4] Test `codeconv/tests/test_tombstone_divergence.py` (@needs_bridge): DB↔tombstone drift detected; refuses stale (FR-019)

## Phase 6b: Analyze Remedies (E1–E5, applied 2026-05-17)

- [ ] T054 [US1] **R12 gate (E1, HIGH)**: `codeconv/tests/test_dbos_throughput_smoke.py` (@needs_bridge) — bounded sustained-throughput smoke: drive ≥20 files through the durable pipeline with the default serial worker, assert no bridge-lock starvation, every step checkpointed, and `builder status` still < 5 s; **US1 is not accepted until this passes** (plan R12 / human-gate item)
- [ ] T055 [US1] **E2**: `codeconv/tests/test_pipeline_stage_order.py` (@needs_bridge) — assert `discover` is the entry stage and `scaffold` records each produced target path into per-file conversion-tracking state within the builder workflow (FR-007/FR-008), entrypoints unchanged (D2)
- [ ] T056 [P] [US2] **E3**: `codeconv/tests/test_convspec_both_bases.py` (@needs_bridge) — for a file with ≥1 non-trivial construct, assert the persisted spec records **both** a deep-analysis basis and a researched-pattern basis (or an idiom_id) for each such construct (SC-006, 0 unjustified decisions)
- [ ] T057 [P] [US1] **E4**: `codeconv/tests/test_mid_run_code_change.py` (@needs_bridge) — recovered workflow replays new code: completed steps not re-run, remaining steps run new code; `--restart-run` opt-in deterministic (R13 / spec edge case)
- [ ] T058 [P] [US3] **E5**: `codeconv/tests/test_reproducible_from_durable.py` (@needs_bridge) — wipe derived artifacts, re-drive: inventory/depgraph/specs/idioms regenerate from the durable source of truth identically (FR-021, no live-data dependency)

## Phase 7: Polish & Cross-Cutting

- [ ] T050 Re-run full `pytest codeconv/tests/`; compare to T001 baseline — zero regressions (CLAUDE.md §Test Protocol)
- [ ] T051 [P] Run `quickstart.md` acceptance smoke 1–7 end-to-end against a fresh PG17 cluster; record outcomes
- [ ] T052 [P] Verify no Dart/.NET/Node/`glp_runtime/` file changed (scope guard, plan Structure Decision)
- [ ] T053 Update memory `project_018_codeconv_builder_status.md` (status: plan+tasks done; D1=a, D2-hardened; next state) — no CLAUDE.md duplication

---

## Dependencies / Story Completion Order

- **Phase 2 (T003–T018) BLOCKS everything** — `codeconv migrate` is broken until T003/T004; durable scaffolding + shared model underpin all stories.
- **US1 (P1)** depends only on Phase 2 → first independently testable increment.
- **US2 (P1)** depends on Phase 2 + US1's builder/skill (T019/T027 for the awaiting-agent handler) → MVP = US1+US2.
- **US3 (P2)** depends on Phase 2 + US1/US2 surfaces.
- **US4 (P3)** depends on US1 (durable runs) + US3 (`status.py`).
- Polish last.

## Parallel Execution Examples

- Phase 2 tests: T005, T006, T010, T017, T018 in parallel (distinct files).
- US1 tests: T023, T024, T025, T026 in parallel after T022.
- US2 tests: T030, T035, T036, T037, T038 in parallel after their impl tasks.
- US3/US4 tests: T043, T044 ‖ ; T048, T049 ‖ .

## Implementation Strategy

**MVP = US1 + US2** (both P1, co-critical per spec): a single resumable
command that drives the pipeline and produces researched per-file conversion
specs with a growing idiom KB. US3 removes the consolidation defects; US4 adds
observability/retry. Deliver Phase 2 → US1 → US2, demo, then US3, US4, polish.

## Analyze remedies applied (2026-05-17)

E1–E5 from `/speckit-analyze` applied as T054–T058 (Phase 6b). **T054 is a
US1 acceptance gate** (R12 DBOS sustained-throughput — the human-gate item).
LOW finding I1 (spec "Conversion Workflow Run" ≡ data-model `builder_runs`):
treated as the same entity; no rename (cosmetic, not worth churn).

## Format validation

All 58 tasks (T001–T058): `- [ ]` checkbox + sequential Txxx ID + `[P]` where
parallel + `[US#]` on story-phase tasks only (none on Setup/Foundational/
Polish) + explicit file path. ✔
