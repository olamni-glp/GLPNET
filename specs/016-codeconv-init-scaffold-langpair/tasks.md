---
description: "Tasks for feature 016 — codeconv init + scaffold behind a pluggable language-pair registry (Dart→C#)"
---

# Tasks: codeconv init + scaffold behind a pluggable language-pair registry

**Input**: Design documents from `specs/016-codeconv-init-scaffold-langpair/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/{langpair_plugin_contract,codeconv_init_cli,codeconv_scaffold_cli}.md, quickstart.md (all present)

**Tests**: REQUIRED (spec FR-024/FR-025/FR-026; DISCIPLINE §2.4 TDD). Test tasks are written and verified-failing BEFORE the corresponding implementation.

**Organization**: By user story (US1 init = P1 MVP; US2 scaffold = P1; US3 registry-extensibility = P2; US4 exclusions = P2; US5 full-pipeline regression = P2).

**Path base**: repo root `D:\BSTDEV\research\GLP\GLPNET\`. Tool contract: `specs/012-codeconv-runner/contracts/codeconv_tool_contract.md`.

## 🔴 Branch & dependency

Work on `016-codeconv-init-scaffold-langpair` (branched off `015-codeconv-depgraph` HEAD). Feature 016 **depends on feature 015**'s tombstone `target_path` + `codeconv.tools.depgraph.tombstone_writer` (D4/FR-015). Land after 015 merges to `main` (or with 015 merged in). On this exFAT checkout every live invocation passes `--data-dir C:/pglite/research/glpnet`; tests use `tmp_path`/`discover_repo` (fresh 0.4.5 cluster).

---

## Phase 1: Setup

- [ ] T001 Confirm the feature-015 substrate is present on this branch: `codeconv/src/codeconv/tools/depgraph/tombstone_writer.py` exposes `write_tombstone_with_extras` and `codeconv.tools.discover.tombstone._FEATURE_015_KEYS` includes `target_path` (6 keys). If absent, STOP — 015 must be merged/available first.
- [ ] T002 Confirm tool-contract surface: re-read `specs/012-codeconv-runner/contracts/codeconv_tool_contract.md`; confirm `codeconv/src/codeconv/cli.py` auto-discovers `codeconv/src/codeconv/tools/*` via the runner registry (so new `tools/init`, `tools/scaffold` register automatically).
- [ ] T003 Baseline: `codeconv/.venv/Scripts/python.exe -m pytest codeconv/tests/ -q` (synchronous; PGLite cold-init ~7s). Record pass/skip/known-flake counts; if reds beyond the documented bridge-exhaustion flake, STOP and report.
- [ ] T004 Snapshot pre-016 schema: capture `\dn` + `\dt codeconv.*` + `\dt public.*` into `specs/016-codeconv-init-scaffold-langpair/pre_feature_schema_snapshot.txt` for the SC schema-isolation check.

**Checkpoint**: green baseline + 015 substrate confirmed → Foundational begins.

---

## Phase 2: Foundational (Blocking Prerequisites)

**⚠️ Blocks ALL user stories.** Per `contracts/langpair_plugin_contract.md` + `data-model.md` + research R1/R2/R6.

- [ ] T005 Create `codeconv/src/codeconv/langpairs/base.py`: `LangPair` Protocol (source/target/identity hook groups per `contracts/langpair_plugin_contract.md`) + `UnknownLangPair` exception (actionable message lists known pairs).
- [ ] T006 Create `codeconv/src/codeconv/langpairs/__init__.py`: registry `register()/get()/list_pairs()/resolve_workspace_pair(engine)` per the contract's "Stage enforcement" + auto-import of every production pair package.
- [ ] T007 [P] Create `codeconv/src/codeconv/langpairs/dart_csharp/source_dart.py`: factor the Dart source logic from `tools/discover/{walker,parse,pubspec}.py` (extensions, tool-exclusion globs, `extract_imports`, `extract_leading_doc`, `read_package_name`) — output byte-faithful to today (regression oracle = 012/014/015 suites).
- [ ] T008 [P] Create `codeconv/src/codeconv/langpairs/dart_csharp/target_csharp.py`: `target_extension()='.cs'`, `target_for()` (ext-swap, mirrored dirs), `workdir_name()='__<base>'` (D2Net.Scaffold `TargetTreePlanner` parity).
- [ ] T009 Create `codeconv/src/codeconv/langpairs/dart_csharp/__init__.py`: `DartCSharp` LangPair binding T007+T008; `register(DartCSharp())`.
- [ ] T010 Create `codeconv/src/codeconv/db/migrations/versions/0003_d2net_into_codeconv.py` per `data-model.md` §1: 4 `CREATE TABLE IF NOT EXISTS` (`workspace_settings, excluded_directories(+CHECK kind), phase_sequence, phase_status`); `downgrade()` = 4 `DROP TABLE IF EXISTS … CASCADE` reverse; `revision="0003"`, `down_revision="0002"`. NO `public.*`, NO `scaffold_tracker`.
- [ ] T011 Repoint `tools/discover/workflow.py` to source Dart-specifics from the selected pair (default `dart_csharp` when no workspace) via `langpairs`; keep `parse.py`/`pubspec.py`/`walker.py` as thin shims OR delete + repoint imports (minimal-diff). Behaviour for the default Dart path MUST be byte-identical.
- [ ] T012 Regression gate: `pytest codeconv/tests/` — the feature-012/014/015 discover+depgraph suites MUST stay green (SC-005); `codeconv --data-dir <tmp> migrate` applies 0001→0003 cleanly; `\dt codeconv.*` gains exactly the 4 new tables, `public`/`dbos` unchanged.

**Checkpoint**: registry + dart_csharp + migration 0003 + pair-generic discover landed, zero regression → user stories begin.

---

## Phase 3: User Story 1 — Initialize a conversion workspace (Priority: P1) 🎯 MVP

**Goal**: `codeconv init` configures the workspace (pair, paths, exclusions, phase tables) and delegates the inventory to discover.

**Independent Test**: clean checkout → `codeconv init --source glp_runtime_net --target out/csharp --source-lang dart --target-lang csharp --accept-suggested-exclusions --non-interactive` ⇒ `workspace_settings` bound to dart/csharp, exclusions+phase tables seeded, `codeconv.dart_files` populated by delegated discover; re-init idempotent; unregistered pair → exit 5; invalid path → exit 2 (no state).

### Tests for User Story 1 (write FIRST, ensure FAIL)

- [ ] T013 [P] [US1] Create `codeconv/tests/test_init.py` per `contracts/codeconv_init_cli.md`: `test_init_writes_workspace_settings_and_pair`, `test_init_seeds_exclusions_and_phase_tables`, `test_init_delegates_inventory_to_discover` (dart_files populated, no second scan), `test_init_idempotent_already_initialized`, `test_init_rejects_unregistered_pair_exit5_no_state`, `test_init_rejects_invalid_source_path_exit2_no_state`, `test_init_rebuild_requires_confirmation`. `@needs_bridge`, `discover_repo` fixture.

### Implementation for User Story 1

- [ ] T014 [US1] Create `codeconv/src/codeconv/tools/init/workflow.py`: `run_init(...)` per `contracts/codeconv_init_cli.md` §run behaviour 1–7 (registry resolve→exit5; path validate→exit2; one txn UPSERT `workspace_settings`/`excluded_directories`/seed `phase_*`; in-process `run_discover` delegation; idempotent; `--rebuild` gated). Plus `run_add_exclude`/`run_remove_exclude`/`run_list`/`run_inspect`.
- [ ] T015 [US1] Create `codeconv/src/codeconv/tools/init/__init__.py`: Typer `app` (+ `register_workflows` no-op) with `run` default + `add-exclude`/`remove-exclude`/`list`/`inspect` subcommands and the flag surface from `contracts/codeconv_init_cli.md`. Wire to T014.
- [ ] T016 [US1] Create `.claude/skills/codeconv-init/SKILL.md`: mirror `.claude/skills/codeconv-discover/SKILL.md` + `/D2NET-init` destructive-confirm (prompt before `--rebuild`, cache by target@timestamp). Contract source = `contracts/codeconv_init_cli.md`.
- [ ] T017 [US1] Run `pytest codeconv/tests/test_init.py`; verify T013 passes. Run full `pytest codeconv/tests/`; regression-free.

**Checkpoint**: US1 = MVP — a configured, inventoried Dart→C# workspace in one command.

---

## Phase 4: User Story 2 — Scaffold the target tree (Priority: P1)

**Goal**: `codeconv scaffold` mirrors the in-scope source tree into the target with the pair's extension + workdir convention, records `target_path` into tombstones, advances the scaffold phase.

**Independent Test**: after US1, `codeconv scaffold` ⇒ `out/csharp/**` mirrors non-excluded source with `.cs` + `__<base>/`; every scaffolded tombstone has `target_path`; phase_status['scaffold']=COMPLETE; re-run idempotent; non-empty target without `--force-delete-target` refused; pair mismatch refused.

### Tests for User Story 2 (write FIRST, ensure FAIL)

- [ ] T018 [P] [US2] Create `codeconv/tests/test_scaffold.py` per `contracts/codeconv_scaffold_cli.md`: `test_scaffold_mirrors_tree_with_target_ext_and_workdir`, `test_scaffold_records_target_path_in_tombstone`, `test_scaffold_missing_tombstone_warns_not_fails`, `test_scaffold_idempotent_no_churn`, `test_scaffold_staging_atomic_failure_leaves_target_untouched`, `test_scaffold_refuses_nonempty_target_without_force`, `test_scaffold_refuses_before_init_exit2`, `test_scaffold_refuses_pair_mismatch`, `test_scaffold_advances_phase_status`. `@needs_bridge`.

### Implementation for User Story 2

- [ ] T019 [P] [US2] Create `codeconv/src/codeconv/tools/scaffold/planner.py`: build the target-tree plan from `codeconv.dart_files` minus `excluded_directories`, using the selected pair's `target_for()`/`workdir_name()`.
- [ ] T020 [US2] Create `codeconv/src/codeconv/tools/scaffold/workflow.py`: `run_scaffold(...)` per `contracts/codeconv_scaffold_cli.md` behaviour 1–9 — resolve+enforce pair; prerequisite check; stage under `<target>.codeconv-scaffold-tmp/`; atomic move; per-file `target_path` via `codeconv.tools.depgraph.tombstone_writer` (missing tombstone → warn); upsert `phase_status`/`phase_sequence`; one txn for DB.
- [ ] T021 [US2] Create `codeconv/src/codeconv/tools/scaffold/__init__.py`: Typer `app` (+ `register_workflows` no-op), `run` default + flags (`--force-delete-target`, `--no-tombstone-update`, `--quiet`, `--json`). Wire to T020.
- [ ] T022 [US2] Create `.claude/skills/codeconv-scaffold/SKILL.md`: mirror discover + `/D2NET-scaffold` destructive gate (prompt/drive before `--force-delete-target`).
- [ ] T023 [US2] Run `pytest codeconv/tests/test_scaffold.py`; verify T018 passes. Run full suite; regression-free.

**Checkpoint**: US1+US2 = the working init→scaffold replacement of the D2NET workflow.

---

## Phase 5: User Story 3 — Registry extensibility (Priority: P2)

**Goal**: A new `(source,target)` pair slots in via one new plugin package with **zero** stage-tool edits (FR-003/SC-003).

**Independent Test**: register a test-only second pair; `list_pairs()` shows it; `init --source-lang X --target-lang Y` binds it; `git diff` touches no file under `tools/{init,scaffold,discover,depgraph}/`.

### Tests for User Story 3 (write FIRST, ensure FAIL)

- [ ] T024 [P] [US3] Create `codeconv/tests/test_langpair_registry.py` per `contracts/langpair_plugin_contract.md` §"Test obligations" 1–5: registry CRUD + duplicate/conflict; `UnknownLangPair` lists known; `dart_csharp` hook exact-values (pos) + negative controls; `extract_imports`/`extract_leading_doc` parity vs legacy `tools/discover`; a registered test-only second pair proving zero stage-tool edits. Pure unit, NO bridge.

### Implementation for User Story 3

- [ ] T025 [US3] Add the test-only second pair as a fixture inside `test_langpair_registry.py` (no production pair added); assert `list_pairs()`/`get()`/selection work and that the registry indirection from Phase 2 needs no stage-tool change to accept it.
- [ ] T026 [US3] Run `pytest codeconv/tests/test_langpair_registry.py`; verify T024 passes (fast, no bridge).

**Checkpoint**: pluggability proven; Dart→C# remains the only production pair.

---

## Phase 6: User Story 4 — Exclusion management (Priority: P2)

**Goal**: add/remove exclusions on an existing workspace; inventory stays consistent.

**Independent Test**: `init add-exclude <dir>` ⇒ files under it leave `codeconv.dart_files` (discover re-synced) + persist; `init remove-exclude <dir>` ⇒ they return.

### Tests for User Story 4 (write FIRST, ensure FAIL)

- [ ] T027 [P] [US4] Add to `codeconv/tests/test_init.py`: `test_add_exclude_drops_files_and_persists`, `test_remove_exclude_restores_files`, `test_exclude_kind_recorded_manual`. `@needs_bridge`.

### Implementation for User Story 4

- [ ] T028 [US4] Finalise `run_add_exclude`/`run_remove_exclude` in `tools/init/workflow.py` (T014) to mutate `excluded_directories` then re-delegate `run_discover` so `codeconv.dart_files` matches the exclusion set (FR-011). Wire the subcommands in `tools/init/__init__.py` if stubbed in T015.
- [ ] T029 [US4] Run `pytest codeconv/tests/test_init.py`; verify T027 passes. Full suite regression-free.

**Checkpoint**: incremental exclusion management functional.

---

## Phase 7: User Story 5 — Full Dart→C# pipeline regression (Priority: P2)

**Goal**: init→discover→depgraph→scaffold interoperate with shared inventory/tombstone/phase state; no 012/014/015 regression.

**Independent Test**: run the four stages in order on a synthetic subtree; assert cross-stage consistency + existing suites green.

### Tests for User Story 5 (write FIRST, ensure FAIL)

- [ ] T030 [P] [US5] Create `codeconv/tests/test_pipeline_dart_csharp.py`: synthetic subtree → `init` → `depgraph compute` → `scaffold`; assert (a) `workspace_settings` pair, (b) `dart_files` populated, (c) every scaffolded tombstone `target_path` == produced `.cs` path, (d) `phase_status` reflects scaffold complete, (e) `depgraph compute` still consistent. `@needs_bridge`.

### Implementation for User Story 5

- [ ] T031 [US5] Run `pytest codeconv/tests/test_pipeline_dart_csharp.py` + full `pytest codeconv/tests/`; verify green (SC-004/SC-005); the documented bridge-exhaustion flake re-run isolated if it appears.

**Checkpoint**: end-to-end pipeline verified.

---

## Phase 8: Polish & Cross-Cutting Concerns

- [ ] T032 [P] Remove `tools/d2net/` (D2Net.Init, D2Net.Scaffold, D2Net.PgdbMigrate, D2Net.BridgeClient + the `.sln`) — FR-022/D1/D2 (git history is the archive).
- [ ] T033 [P] Remove `.claude/skills/D2NET-init/`, `.claude/skills/D2NET-scaffold/`, `.claude/skills/D2NET-pgdb-migrate/` — FR-022.
- [ ] T034 Update docs to drop the D2NET toolchain and point at `codeconv init`/`codeconv scaffold`: `CLAUDE.md` "Migration to unified bridge" paragraph + directory-structure tree, `.gitignore` D2NET-backup line, `docs/known-issues.md`/README pointers; one-line note that the legacy `.D2NET/pgdb/` migration is historically complete and intentionally not ported (SC-006).
- [ ] T035 [P] Extend `codeconv/tests/test_phase7_verifications.py`'s SQL-safety grep (the **feature-012/015 carry-forward** — no `COPY ... FROM STDIN`, no client-side prepared-statement cache; distinct from this feature's own FR-026 = pipeline regression) to also scan `codeconv/src/codeconv/tools/{init,scaffold}/` + `codeconv/src/codeconv/langpairs/` for `COPY`/`copy_expert`/`prepared_statement_cache_size`.
- [ ] T036 Run `quickstart.md` Flow I/II/III against the live `glp_runtime_net/` checkout with `--data-dir C:/pglite/research/glpnet` (after the 015 live-cluster (a)-then-(b) decision is executed); verify SC-001/SC-002/SC-007/SC-008 + schema isolation. Capture to a temp scratch file (do NOT commit).
- [ ] T037 Final full suite `pytest codeconv/tests/`; record counts in the PR description; confirm baseline + ≥ the new tests (init + scaffold + registry + exclusions + pipeline) green; open PR onto `main` per `docs/BRANCHING.md` + same-day CalVer tag per `docs/VERSIONING.md`.

**Checkpoint**: all SCs verified; one toolchain; feature ready to merge.

---

## Dependencies & Execution Order

- **Setup (P1)** → no deps.
- **Foundational (P2)** → depends on Setup; **BLOCKS all stories** (registry+plugin+migration+pair-generic discover).
- **US1 init (Ph3)** → depends on Foundational. MVP.
- **US2 scaffold (Ph4)** → depends on Foundational + US1 (needs an initialised workspace + inventory).
- **US3 registry (Ph5)** → depends on Foundational only (pure unit; proves the Phase-2 registry). Independent of US1/US2.
- **US4 exclusions (Ph6)** → depends on US1 (extends `tools/init`).
- **US5 pipeline (Ph7)** → depends on US1+US2 (+ feature-015 depgraph).
- **Polish (Ph8)** → depends on US1+US2 complete; T032/T033/T035 are [P]; T036 also gated on the 015 live-cluster decision; T037 last.

### Within each story
- Tests written and verified-failing BEFORE implementation (DISCIPLINE §2.4).
- langpairs base/registry before dart_csharp; migration independent [P]; discover repoint after the pair exists.
- Commit per logical group; do not let interim runs rewrite tombstones into a staged diff.

## Parallel Opportunities

- T007/T008 (source vs target plugin halves) — different files → [P].
- T013/T018/T024/T030 (per-story test files) — different files → [P].
- T032/T033/T035 (Polish removals + grep) — different concerns → [P].
- US3 (pure-unit, no bridge) can run alongside US1/US2 bridge work.

## Implementation Strategy

MVP = Setup + Foundational + US1 (a configured, inventoried workspace). Then US2 (scaffold) completes the day-to-day replacement. US3 proves pluggability; US4 adds exclusion ergonomics; US5 is the cross-stage regression guard. Polish removes the .NET toolchain and finalises docs. PR = a small number of logical commits (foundational; US1; US2; US3; US4; US5; polish/removal), single PR onto `main`.
