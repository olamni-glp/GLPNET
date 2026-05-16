---
description: "Tasks for feature 015 — codeconv-depgraph: topologically sorted Dart dependency graph + conversion-readiness oracle"
---

# Tasks: codeconv-depgraph — topologically sorted Dart dependency graph and conversion-readiness oracle

**Input**: Design documents from `specs/015-codeconv-depgraph/`
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, contracts/ (depgraph_algorithm.md, depgraph_cli.md, depgraph_schema.md, tombstone_format_delta.md), quickstart.md (all present)

**Tests**: Tests are REQUIRED for this feature. Spec SC-001 through SC-008 are measurable outcomes that demand verification, the spec sets multiple test obligations explicitly (US1 acceptance scenarios 1–3, US2 acceptance scenarios 1–4, US3 acceptance scenarios 1–3), and the contracts (`depgraph_algorithm.md` § "Test obligations", `depgraph_schema.md` § "Test obligations", `tombstone_format_delta.md` § "Test obligations") enumerate the test obligations exhaustively. Tests are written BEFORE implementation per DISCIPLINE.md §2.4.

**Organization**: Tasks are grouped by user story (US1 = P1 leaves identification; US2 = P2 frontier advancement; US3 = P2 cycle handling) so each story can be implemented and verified independently. US1 alone is a viable MVP: it delivers the entire ordering + JSON + table without any conversion-state writes; US2 adds the mark + status lifecycle; US3 adds SCC condensation handling for cycles.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Different files, no dependencies on incomplete tasks → can run in parallel
- **[Story]**: US1 (P1 — leaves identification), US2 (P2 — frontier advancement), or US3 (P2 — cycle handling)
- File paths are absolute-relative to repo root `D:\BSTDEV\research\GLP\GLPNET\`

## Path Conventions

- Python source: `codeconv/src/codeconv/tools/depgraph/` (new tool subpackage)
- Python tests: `codeconv/tests/`
- Alembic migrations: `codeconv/src/codeconv/db/migrations/versions/`
- Tombstone artefacts: `.codeconv/tombstones/` (refreshed by `stamp-tombstones` subcommand)
- Slash skill: `.claude/skills/codeconv-depgraph/`
- Spec artefacts: `specs/015-codeconv-depgraph/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Establish a green test baseline; confirm working environment; snapshot pre-feature state.

- [X] T001 Confirm features 012 and 014 are merged: `git log --oneline | findstr 014` shows feature 014's commits; `codeconv/src/codeconv/tools/discover/pubspec.py` exists; tombstones contain post-feature-014 dependencies (sanity-check one: `.codeconv/tombstones/lib/runtime/heap_fcp.dart.md` has 4 deps)
- [X] T002 Run baseline `pytest codeconv/tests/` and confirm it is green per memory `project_012_codeconv_runner_status.md`; if reds appear that are not in the known-skip list, STOP and report. *(Done 2026-05-11; 56 passed, 3 skipped. Required bumping `bridge_client.READY_TIMEOUT_DEFAULT_SECONDS` 10→30s to cover Windows cold-spawn — 7 tests were timing out at the old 10s default. `--test-concurrency=1` is not a real pytest flag and was dropped from the invocation.)*
- [X] T003 Confirm `--data-dir` override is wired: `codeconv --help | findstr data-dir` (top-level option, not per-subcommand)
- [X] T004 Snapshot current state into `specs/015-codeconv-depgraph/baseline.json`: 128 / 443 / 6 confirmed (matches spec expectation exactly). Cluster location: `C:\pglite\research\glpnet\` (NTFS — repo D: is exFAT).
- [X] T005 Snapshot pre-feature `\dn`, `\dt codeconv.*`, `\dt public.*` outputs into `specs/015-codeconv-depgraph/pre_feature_schema_snapshot.txt` for SC-007 verification. Pre-feature state: schemas `{codeconv, dbos, public}`; codeconv has `dart_callers, dart_files, dart_files_orphaned, dart_imports, discover_runs`; public has only `alembic_version`.

**Checkpoint**: Setup green → Foundational phase begins.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Land the Alembic migration so all subsequent tests can read/write the three new tables. This is the ONLY blocking prerequisite for all three user stories.

- [x] T006 Create `codeconv/src/codeconv/db/migrations/versions/0002_dart_depgraph.py` with the exact DDL specified in `contracts/depgraph_schema.md` § `upgrade()` SQL: three `CREATE TABLE IF NOT EXISTS` blocks (`depgraph_runs`, `dart_conversions`, `dart_depgraph`) and two `CREATE INDEX IF NOT EXISTS` statements. The `downgrade()` is three `DROP TABLE IF EXISTS … CASCADE` in reverse order. Revision metadata: `revision = "0002"`, `down_revision = "0001"`
- [x] T007 Add test `codeconv/tests/test_depgraph_schema_isolation.py` per `contracts/depgraph_schema.md` § "Test obligations" items 1–5: schema isolation (only `codeconv` schema affected); three new tables created; CHECK constraints catch typos (`status='completed'`) and consistency violations (`ready=true, status='pending'`); FK CASCADE works on `dart_files` delete; downgrade-then-upgrade is idempotent. Gate with `@needs_bridge`
- [x] T008 Run `codeconv migrate` (or the equivalent alembic command) against `.pgdb`; confirm the three new tables appear under `codeconv` schema and nothing new appears under `public` or `dbos`
- [x] T009 Run `pytest codeconv/tests/test_depgraph_schema_isolation.py --test-concurrency=1`; verify green

**Checkpoint**: Foundational complete → all user stories can begin. The bridge sees three new empty tables under `codeconv.*`. US1 begins immediately.

---

## Phase 3: User Story 1 — Identify the first wave of conversion candidates (Priority: P1) 🎯 MVP

**Goal**: `codeconv depgraph compute` reads `dart_imports` + `dart_files`, computes a topological ordering with Tarjan SCC + condensation, writes the result to `codeconv.dart_depgraph` and `.codeconv/depgraph.json`. The JSON's top-level `ready` array equals the SQL-derived set of files with zero in-subtree dependencies AND no completed-conversion blocker (initially: every leaf file).

**Independent Test**: On the live `glp_runtime_net/` checkout with `/codeconv-discover` already run, invoke `codeconv depgraph compute`. The JSON file `.codeconv/depgraph.json` exists; its `ready` array is non-empty (≥6 paths corresponding to the 6 isolated files in the post-feature-014 baseline); the `codeconv.dart_depgraph` table has 128 rows (one per inventoried file). Spec US1 acceptance scenarios 1–3 verified per `quickstart.md` Flow H steps 1–3, 5.

### Tests for User Story 1 (REQUIRED — write FIRST, ensure they FAIL before implementation)

- [x] T010 [P] [US1] Create `codeconv/tests/test_depgraph_algorithm.py` with the 8 pure-stdlib unit tests enumerated in `contracts/depgraph_algorithm.md` § "Test obligations" items 1–8: linear chain, diamond, 3-cycle, 3-cycle plus tail, self-loop, isolated nodes, determinism, unknown edge endpoint. No bridge needed
- [x] T011 [P] [US1] Create `codeconv/tests/test_depgraph_compute.py` with `test_compute_writes_jsonfile_and_table`, `test_empty_inventory_exits_2`, `test_ready_set_matches_sql_query`, `test_topo_invariant_holds_for_every_edge`, `test_compute_respects_json_out_flag` (FR-013 — `--json-out <path>` overrides default location), `test_compute_quiet_suppresses_per_file_logging` (FR-012) per `contracts/depgraph_cli.md` § `compute` behaviour. Gate with `@needs_bridge`
- [x] T012 [P] [US1] Create `codeconv/tests/test_depgraph_idempotence.py` with `test_two_consecutive_computes_byte_identical_modulo_generated_at`, `test_dry_run_writes_nothing` per SC-002 / SC-008 and `contracts/depgraph_cli.md` § "Behaviour" step 10 (dry-run). Gate with `@needs_bridge`

### Implementation for User Story 1

- [x] T013 [P] [US1] Create `codeconv/src/codeconv/tools/depgraph/algorithm.py` per `contracts/depgraph_algorithm.md` § Module surface + § Algorithm: iterative Tarjan SCC, condensation DAG, Kahn-style level assignment with determinism rules 1–4 from R1. Export `DepgraphResult` dataclass and `compute(nodes, edges)` function. Pure-stdlib (no SQLAlchemy, no psycopg, no DBOS)
- [x] T014 [P] [US1] Create `codeconv/src/codeconv/tools/depgraph/json_writer.py` — emit the JSON shape from `contracts/depgraph_cli.md` § "JSON output shape" with `schema_version: 1`, top-level key order (metadata, ready, files), `ready` array sorted lex, `files` array sorted by (topo_level, cycle_group_id, path). Use `json.dumps(obj, indent=2, sort_keys=True, default=str)` for inner-row determinism. Atomic write (temp-file + rename)
- [x] T015 [US1] Create `codeconv/src/codeconv/tools/depgraph/workflow.py` with `run_compute(repo_root, *, data_dir, json_out, dry_run, quiet)` per `contracts/depgraph_cli.md` § `compute` behaviour steps 1–10: bridge acquire, read `dart_files` + `dart_imports` + `dart_conversions`, call `algorithm.compute`, derive `status` per FR-006, INSERT `depgraph_runs` row, DELETE `dart_depgraph`, bulk INSERT ... ON CONFLICT, UPDATE `depgraph_runs` with counts, COMMIT, write JSON. All inside one SQLAlchemy transaction (atomic-per-run, FR-008)
- [x] T016 [US1] Create `codeconv/src/codeconv/tools/depgraph/__init__.py` with Typer app exporting `app` and `register_workflows`. Wire the `compute` subcommand AND make it the default (no-args `codeconv depgraph` → `compute`). Flags per `contracts/depgraph_cli.md` § "Per-subcommand flags / compute": `--json-out`, `--dry-run`, `--quiet`, `--json`
- [x] T017 [US1] Run `pytest codeconv/tests/test_depgraph_algorithm.py --test-concurrency=1`; verify T010 passes. Then `pytest codeconv/tests/test_depgraph_compute.py codeconv/tests/test_depgraph_idempotence.py --test-concurrency=1`; verify T011 and T012 pass
- [x] T018 [US1] Run `pytest codeconv/tests/ --test-concurrency=1`; verify the existing feature-012/-014 tests still pass (regression-free). The new tool's auto-registration is transparent to all existing tests

**Checkpoint**: US1 complete → MVP shippable. Acceptance scenarios 1–3 from spec.md US1 verified. A developer can now invoke `codeconv depgraph` and read the `ready` array.

---

## Phase 4: User Story 2 — Advance the conversion frontier (Priority: P2)

**Goal**: `mark-started` and `mark-completed` subcommands populate `codeconv.dart_conversions` and update the corresponding tombstone YAML. A subsequent `compute` re-derives `status` correctly: `pending` → `ready` (when all deps converted) → `in_progress` (after `mark-started`) → `converted` (after `mark-completed`). The four spec US2 acceptance scenarios pass end-to-end.

**Independent Test**: After at least one file has been marked through both phases (`mark-started` → `mark-completed`), re-run `compute`. Verify: (a) the marked file has `status='converted'` in the JSON; (b) its immediate dependents (if any) advance from `status='pending'` to `status='ready'`; (c) files mid-conversion (started but not completed) have `status='in_progress'` AND DO NOT unblock downstream files. Spec US2 acceptance scenarios 1–4 verified per `quickstart.md` Flow H step 6.

### Tests for User Story 2 (REQUIRED — write FIRST, ensure they FAIL before implementation)

- [x] T019 [P] [US2] Create `codeconv/tests/test_depgraph_mark.py` with: `test_mark_started_inserts_row_with_started_at`, `test_mark_started_uses_dart_files_sha256_when_no_arg`, `test_mark_started_uses_sha256_arg_when_provided`, `test_mark_started_idempotent_warns_when_already_started`, `test_mark_started_idempotent_warns_when_already_completed`, `test_mark_started_errors_on_nonexistent_path`, `test_mark_completed_updates_completed_at_and_target_path`, `test_mark_completed_errors_if_never_started`, `test_mark_completed_idempotent_warns_when_already_completed`, `test_mark_completed_idempotent_does_not_overwrite_target_path` (FR-006a clarified 2026-05-11 — `target_path` and `completed_at` are write-once), `test_mark_completed_errors_on_nonexistent_path`, `test_mark_started_updates_tombstone_yaml`, `test_mark_completed_updates_tombstone_yaml` per `contracts/depgraph_cli.md` § `mark-started` / `mark-completed`. Gate with `@needs_bridge`
- [x] T020 [P] [US2] Add `test_status_lifecycle_pending_inprogress_converted` and `test_inprogress_does_not_unblock_downstream` to `codeconv/tests/test_depgraph_compute.py` covering spec US2 acceptance scenarios 1–4 end-to-end via the chain A→B→C fixture: mark-started A → compute → status lifecycle assertions

### Implementation for User Story 2

- [x] T021 [US2] Create `codeconv/src/codeconv/tools/depgraph/tombstone_writer.py` with `read_tombstone(path) -> dict`, `update_conversion_keys(fields, started_at, completed_at, target_path) -> dict`, `update_depgraph_keys(fields, topo_level, cycle_group_id, status) -> dict`. Reuses feature 012's existing `write_tombstone` from `codeconv/src/codeconv/tools/discover/tombstone.py` to preserve the canonical YAML emitter
- [x] T022 [US2] Modify `codeconv/src/codeconv/tools/discover/tombstone.py::_FIELD_ORDER` per `contracts/tombstone_format_delta.md` § "Change to `_FIELD_ORDER`": append five new keys (`topo_level`, `cycle_group_id`, `status`, `conversion_started_at`, `conversion_completed_at`). Verify `_canonicalise(fields)` preserves YAML-null for None values — adjust the emitter if it strips None-valued keys today (the existing helper may need a single-line tweak; see `contracts/tombstone_format_delta.md` § "_canonicalise adjustment")
- [x] T023 [US2] Add `run_mark_started(repo_root, *, data_dir, path, sha256, no_tombstone_update)` and `run_mark_completed(repo_root, *, data_dir, path, target, no_tombstone_update)` to `codeconv/src/codeconv/tools/depgraph/workflow.py` per `contracts/depgraph_cli.md` § `mark-started` / `mark-completed` behaviour. Each opens a single transaction: validate path exists in `dart_files`; SELECT existing `dart_conversions` row; INSERT or UPDATE per the state machine in `data-model.md` § 3; INSERT `depgraph_runs` row; UPDATE tombstone YAML; COMMIT
- [x] T024 [US2] Add `mark-started <path>` and `mark-completed <path>` subcommands to `codeconv/src/codeconv/tools/depgraph/__init__.py` Typer app, with the flag surface from `contracts/depgraph_cli.md` § "Per-subcommand flags" (--sha256, --target, --no-tombstone-update). Wire to T023's workflow functions
- [x] T025 [US2] Update `workflow.py::run_compute` (T015) to use `dart_conversions` data when deriving `status` per FR-006: `pending` (no row) → `ready` (no row AND all SCC-external deps have `dart_conversions.completed_at IS NOT NULL`) → `in_progress` (row with `completed_at IS NULL`) → `converted` (row with `completed_at IS NOT NULL`). Initial implementation in T015 may have used a stub; finalise here
- [x] T026 [US2] Run `pytest codeconv/tests/test_depgraph_mark.py codeconv/tests/test_depgraph_compute.py --test-concurrency=1`; verify T019 and T020 pass. Run full `pytest codeconv/tests/ --test-concurrency=1`; verify regression-free

**Checkpoint**: US2 complete. Acceptance scenarios 1–4 from spec.md US2 verified. The conversion-frontier-advance flow is end-to-end functional.

---

## Phase 5: User Story 3 — Convert a cycle as a single batch (Priority: P2)

**Goal**: When the import graph contains a multi-file SCC (cycle), all members share the same `cycle_group_id` and `topo_level`, and become `status='ready'` together when their SCC-external dependencies are all converted. A developer can `mark-started` all SCC members, convert them as a batch, then `mark-completed` all members; downstream files then advance correctly.

**Independent Test**: Construct a 3-file cycle fixture (A→B→C→A, plus D→A). Run `compute`. Verify: (a) A, B, C share one `cycle_group_id` and one `topo_level`; (b) D has a higher `topo_level` and a distinct `cycle_group_id`; (c) A/B/C all have `status='ready'` simultaneously; (d) D has `status='pending'`. After `mark-completed` on all of A, B, C: D advances to `status='ready'`. Spec US3 acceptance scenarios 1–3 verified per `quickstart.md` § "US3 — 3-file cycle fixture".

### Tests for User Story 3 (REQUIRED — write FIRST, ensure they FAIL before implementation)

- [x] T027 [P] [US3] Create `codeconv/tests/test_depgraph_cycle_fixture.py` with: `test_three_file_cycle_shares_one_cycle_group_id`, `test_three_file_cycle_shares_one_topo_level`, `test_cycle_count_metric_equals_one_for_3cycle`, `test_d_above_cycle_at_higher_topo_level`, `test_d_becomes_ready_when_all_cycle_members_converted`, `test_mark_started_on_one_cycle_member_keeps_others_ready` per spec US3 acceptance scenarios 1–3. Gate with `@needs_bridge`
- [x] T028 [P] [US3] Add the synthetic cycle fixture under `specs/015-codeconv-depgraph/scripts/cycle_fixture/`: three minimal `.dart` files A.dart, B.dart, C.dart in a 3-cycle, plus D.dart depending on A. Each file is ≤10 lines of Dart with the relevant `import` directives. Include a `pubspec.yaml` if discover requires one (verify against feature 014's pubspec rewrite — likely yes)

### Implementation for User Story 3

(US3 reuses US1's algorithm — no new algorithm code. SCC handling is built into Tarjan from T013. US3's only new code is the cycle fixture and the dedicated test surface.)

- [x] T029 [US3] Verify that `algorithm.compute` (T013) already handles the cycle fixture correctly: pure-stdlib unit test in `test_depgraph_algorithm.py` (T010 item 3 "3-cycle" + item 4 "3-cycle plus tail") covers this. If it does not, return to T013 — the algorithm contract was incompletely implemented
- [x] T030 [US3] Verify that `workflow.run_compute` (T015) correctly derives `status='ready'` for ALL members of a multi-file SCC when their SCC-external dependencies are all `converted` — the FR-006 eligibility rule explicitly ignores intra-SCC edges. If the initial implementation in T015 / T025 used a simplistic per-node dependency check, replace with the SCC-aware check
- [x] T031 [US3] Run `pytest codeconv/tests/test_depgraph_cycle_fixture.py --test-concurrency=1`; verify T027 passes. Run full suite; verify regression-free

**Checkpoint**: US3 complete. Acceptance scenarios 1–3 from spec.md US3 verified. The cycle fixture demonstrates SCC-aware ordering end-to-end.

---

## Phase 6: Slash skill + auxiliary subcommands

**Purpose**: Land the `/codeconv-depgraph` slash skill, the `stamp-tombstones` subcommand (FR-014), and the `rebuild-conversions-from-tombstones` subcommand (R3). These are cross-cutting; depend on US1–US3 being complete because they reference state computed by `compute` and mutated by `mark-*`.

### Tests for auxiliary subcommands

- [x] T032 [P] Create `codeconv/tests/test_depgraph_stamp.py` with the 5 tests from `contracts/tombstone_format_delta.md` § "Test obligations" `test_depgraph_stamp.py`: initial stamp adds five keys; re-stamp idempotence; pending / in_progress / converted status YAML representations. Gate with `@needs_bridge`
- [x] T033 [P] Create `codeconv/tests/test_depgraph_rebuild_conversions.py` with the 3 tests from `contracts/tombstone_format_delta.md` § "Test obligations" `test_depgraph_rebuild_conversions.py`: round-trip; missing-key tolerance; null-value distinguishability. Gate with `@needs_bridge`

### Implementation

- [x] T034 Add `run_stamp_tombstones(repo_root, *, data_dir, dry_run, quiet)` to `codeconv/src/codeconv/tools/depgraph/workflow.py` per `contracts/depgraph_cli.md` § `stamp-tombstones` behaviour: read `dart_depgraph` (error if empty); read `dart_conversions`; for each file, read tombstone, update five new YAML keys per `contracts/tombstone_format_delta.md` § "Writer behaviour by subcommand / stamp-tombstones"; write back; INSERT `depgraph_runs` row
- [x] T035 Add `run_rebuild_conversions_from_tombstones(repo_root, *, data_dir, dry_run, quiet)` to `codeconv/src/codeconv/tools/depgraph/workflow.py` per `contracts/depgraph_cli.md` § `rebuild-conversions-from-tombstones` behaviour: walk `.codeconv/tombstones/` (skipping `.orphaned/`); for each tombstone with `conversion_started_at` non-null, UPSERT into `dart_conversions`. Handle the sha256-round-trip caveat per `contracts/depgraph_cli.md` § "sha256 round-trip caveat"
- [x] T036 Add `stamp-tombstones` and `rebuild-conversions-from-tombstones` subcommands to `codeconv/src/codeconv/tools/depgraph/__init__.py` Typer app, wired to T034 / T035
- [x] T037 Create `.claude/skills/codeconv-depgraph/SKILL.md` structurally copied from `.claude/skills/codeconv-discover/SKILL.md` (frontmatter: name, description matching `/codeconv-depgraph`'s purpose; body: venv resolution, repo-root cwd, pre-execution checks, stdout/stderr passthrough). Reference `contracts/depgraph_cli.md` as the contract source of truth
- [x] T038 Run `pytest codeconv/tests/test_depgraph_stamp.py codeconv/tests/test_depgraph_rebuild_conversions.py --test-concurrency=1`; verify T032 and T033 pass

**Checkpoint**: All five subcommands shipped; the slash skill works. The feature is complete except for polish.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Confirm performance budgets, schema isolation, idempotence; refresh tombstones once on the feature branch; final regression run.

- [x] T039 Run `quickstart.md` Flow H steps 0–11 against the live `glp_runtime_net/` checkout; capture results inline in a temp scratch file (do NOT commit). Verify SC-001 warm budget (< 5 s — `Measure-Command { codeconv depgraph compute --data-dir .pgdb }`), SC-002 (idempotence), SC-003 (edge invariant), SC-004 (ready set matches SQL), SC-005 (top-level `ready` array readable), SC-007 (schema isolation), SC-008 (dry-run produces no changes)
- [x] T039a Verify SC-001 cold-bridge budget (≤ 15 s): kill the bridge daemon (`taskkill /F /IM node.exe` filtered to the bridge PID from `.pgdb/bridge.json`), then run `Measure-Command { codeconv depgraph compute --data-dir .pgdb }`; assert TotalSeconds < 15. The first invocation includes the bridge cold-spawn + ~7 s PGLite cold-init. Repeat 3× and record median to avoid flake
- [x] T040 Run `quickstart.md` § US3 cycle fixture steps; verify SC-006 (cycle_count metric, shared cycle_group_id, shared topo_level)
- [x] T041 [P] Verify FR-026 (no `COPY ... FROM STDIN`) and FR-027 (no client-side prepared-statement caching) greps stay clean: extend `codeconv/tests/test_phase7_verifications.py` (from feature 012) with assertions that the new `codeconv/src/codeconv/tools/depgraph/` subtree contains no occurrences of `"COPY"`, `"copy_expert"`, or `prepared_statement_cache_size`
- [x] T042 [P] Add `.codeconv/depgraph.json` to `.gitignore` per research R10 (one-line addition under the existing `.codeconv/` entries)
- [x] T043 Refresh tombstones via the canonical recipe: (a) run `codeconv depgraph compute --data-dir .pgdb`; (b) run `codeconv depgraph stamp-tombstones --data-dir .pgdb`; (c) commit the `.codeconv/tombstones/` diff with message `"Stamp tombstones with depgraph + conversion state (feature 015)"`. This is the one-time refresh that lands the five new YAML keys in every existing tombstone
- [x] T044 [P] Update `docs/known-issues.md` if any new edge case surfaced during T039–T043 (likely: none)
- [x] T045 Final full suite: `pytest codeconv/tests/ --test-concurrency=1`; record pass/skip count in PR description; if not at least the baseline + at least 35 new tests (8 algorithm + 4 compute + 2 idempotence + 12 mark + 6 cycle fixture + 5 stamp + 3 rebuild + the schema isolation suite), STOP and triage

**Checkpoint**: All success criteria SC-001 through SC-008 verified; feature ready for `/speckit-implement` to merge into `main` per the CalVer release flow.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately.
- **Foundational (Phase 2)**: Depends on Setup. BLOCKS Phases 3–6 (all user stories need the new tables).
- **US1 (Phase 3)**: Depends on Foundational completion. Internally: tests (T010–T012) before implementation (T013–T016); validation (T017–T018) after implementation.
- **US2 (Phase 4)**: Depends on US1 (T013 algorithm + T015 workflow.run_compute) because mark-* + status derivation reuses them.
- **US3 (Phase 5)**: Depends on US1 (Tarjan correctness on cycles is established in T013/T010 item 3). US3 reuses everything; only adds the cycle fixture and dedicated tests.
- **Phase 6 (slash skill + stamp + rebuild)**: Depends on US1, US2, US3 (stamp-tombstones needs all five YAML keys present; rebuild-conversions needs the mark-* round-trip).
- **Phase 7 (polish)**: Depends on Phase 6 complete. T043 (tombstone refresh) runs LAST so it captures both depgraph and conversion state for every file.

### Within Each User Story

- **TDD ordering**: tests in T010–T012, T019–T020, T027, T032–T033 are written FIRST and MUST FAIL against the unmodified codebase before implementation tasks (T013–T018, T021–T026, T029–T031, T034–T037) are touched.
- **Implementation ordering within US1**: T013 (algorithm) and T014 (json_writer) are independent and can be done in parallel. T015 (workflow.run_compute) depends on both. T016 (Typer app) depends on T015.
- **Implementation ordering within US2**: T022 (extend _FIELD_ORDER) is independent. T021 (tombstone_writer.py) depends on T022. T023 (workflow.run_mark_*) depends on T021. T024 (Typer subcommands) depends on T023. T025 (status derivation in run_compute) is independent of T021–T024 but touches the same file as T015.
- **Implementation ordering within US3**: T028 (fixture) is independent. T029–T030 are verifications, not new code.
- **Implementation ordering within Phase 6**: T034–T035 (workflow functions) depend on US2's tombstone_writer.py and feature-012's tombstone.py. T036 wires them into the Typer app. T037 (skill) is independent of all the Python code.

### Parallel Opportunities

- All Phase 1 setup tasks (T001–T005) can run in parallel — different artefacts, no shared state.
- T010, T011, T012 (US1 tests) are different files → parallel.
- T013, T014 (algorithm + json_writer) are different files → parallel.
- T019, T020 (US2 tests) — T019 is its own file (test_depgraph_mark.py); T020 appends to test_depgraph_compute.py created in T011. T019 is [P] with most US2 work; T020 conflicts with US1's T011 (same file) so must be sequenced after T011 completes.
- T032, T033 (Phase 6 tests) are different files → parallel.
- T041, T042, T044 in Phase 7 — different concerns, different files → parallel.

---

## Parallel Example: User Story 1 tests

```bash
# Launch all US1 tests together (different files, can run in parallel):
Task: "Create codeconv/tests/test_depgraph_algorithm.py with 8 pure-stdlib unit tests per contracts/depgraph_algorithm.md"
Task: "Create codeconv/tests/test_depgraph_compute.py with 4 @needs_bridge tests per contracts/depgraph_cli.md"
Task: "Create codeconv/tests/test_depgraph_idempotence.py with 2 @needs_bridge tests per SC-002 / SC-008"

# Then in implementation:
Task: "Create codeconv/src/codeconv/tools/depgraph/algorithm.py per contracts/depgraph_algorithm.md"
Task: "Create codeconv/src/codeconv/tools/depgraph/json_writer.py per contracts/depgraph_cli.md JSON output shape"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Phase 1: Setup baseline (T001–T005).
2. Phase 2: Migration + schema isolation test (T006–T009).
3. Phase 3: US1 (T010–T018).
4. **STOP and VALIDATE**: SC-001 (warm < 5 s), SC-002 (idempotence), SC-003 (edge invariant), SC-005 (`ready` array readable), SC-007 (schema isolation), SC-008 (dry-run no-op) verified by hand running `quickstart.md` Flow H steps 0–8 (skipping steps 6 / 9 / 10 which require US2/Phase 6).
5. If only US1 is shipped (US2 deferred), the feature still delivers the headline value: a developer reads the `ready` array and starts converting leaves. They cannot mark progress (mark-* not yet shipped), but the ordering is complete and authoritative.

### Full Delivery

1. US1 → US2 → US3 → Phase 6 → Phase 7, sequential per the Phase Dependencies above.
2. T043's tombstone refresh runs LAST — it captures depgraph + conversion state for every file in one commit.
3. PR contains a small number of logical commits:
   - Migration commit (`codeconv/src/codeconv/db/migrations/versions/0002_dart_depgraph.py`)
   - Algorithm + tests commit (`codeconv/src/codeconv/tools/depgraph/algorithm.py`, `test_depgraph_algorithm.py`)
   - Workflow + CLI commit (`workflow.py`, `__init__.py`, `json_writer.py`, plus `test_depgraph_compute.py`, `test_depgraph_idempotence.py`)
   - Mark + tombstone-writer commit (US2: `tombstone_writer.py`, `_FIELD_ORDER` extension, `run_mark_*`, plus `test_depgraph_mark.py`)
   - Cycle fixture + tests commit (US3: `cycle_fixture/`, `test_depgraph_cycle_fixture.py`)
   - Stamp + rebuild commit (Phase 6: `run_stamp_tombstones`, `run_rebuild_conversions_from_tombstones`, plus tests + the `SKILL.md`)
   - Tombstone refresh commit (T043, `.codeconv/tombstones/` only)
   - `.gitignore` update commit (T042)
4. Single PR onto `main` per `docs/BRANCHING.md`. Same-day CalVer tag minted on merge per `docs/VERSIONING.md`.

---

## Notes

- [P] tasks = different files (or different test functions in test files with no shared fixture state), no dependencies on incomplete tasks.
- [Story] label maps task to its user story for traceability and independent verification.
- Tests MUST be written and verified-failing BEFORE the corresponding implementation task starts (TDD per DISCIPLINE.md §2.4).
- Commit after each logical group; do not let interim test runs accidentally rewrite tombstones into the commit-staged set before T043.
- T043 is the ONLY commit that touches `.codeconv/tombstones/` after the feature lands.
- The Alembic revision `0002_dart_depgraph.py` is the only file that touches the database schema. No other migration is added in this feature.
- Avoid: vague tasks; edits to feature-012/-014 surfaces beyond the `_FIELD_ORDER` extension in `tombstone.py`; modification of `dart_files`, `dart_imports`, `dart_callers`, `dart_files_orphaned`, or `discover_runs` (FR-011).
