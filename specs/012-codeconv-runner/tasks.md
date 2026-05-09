---
description: "Implementation tasks for 012-codeconv-runner — codeconv-runner harness with unified .pgdb backing"
---

# Tasks: codeconv-runner — overarching codeconv harness with unified `.pgdb` backing

**Input**: Design documents from `specs/012-codeconv-runner/`
**Prerequisites**: `plan.md` ✓ · `spec.md` ✓ · `research.md` ✓ · `data-model.md` ✓ · `contracts/` (7 files) ✓ · `quickstart.md` ✓
**Tests**: INCLUDED — spec defines 13 measurable success criteria (SC-001 through SC-013) with explicit verification procedures.
**Organization**: Tasks grouped by user story so each story can be implemented and tested independently.

## Format: `[ID] [P?] [Story] Description`

- **[P]** = parallel-safe (different files, no order dependency).
- **[Story]** = US1 / US2 / US3 / US4, or no tag for shared phases.
- Paths are absolute or repo-relative (rooted at `D:\BSTDEV\research\GLP\GLPNET\`).

---

## Phase 1: Setup (shared infrastructure)

**Purpose**: Bring the project skeletons into existence so subsequent phases can land code into them.

- [X] **T001** Create directory skeleton: `codeconv/src/codeconv/{tools,db,_vendor}/`, `codeconv/tests/`, `codeconv/db/migrations/`, `tools/d2net/src/{D2Net.BridgeClient,D2Net.PgdbMigrate}/`, `prereq-patterns/pglite/tests/`, `.codeconv/tombstones/.orphaned/.gitkeep`. Repo paths only — no code yet.
- [X] **T002** [P] Author `codeconv/pyproject.toml` declaring deps (`dbos`, `sqlalchemy>=2.0`, `psycopg[binary]>=3.1`, `typer`, `PyYAML`, `python-frontmatter`, `portalocker>=2.8`), the `codeconv` console script entry point, and pytest config.
- [X] **T003** [P] Add `proper-lockfile@^4.1.2` to `prereq-patterns/pglite/package.json` dependencies. Run `npm install` once, commit `package-lock.json`.
- [X] **T004** [P] Author `tools/d2net/src/D2Net.BridgeClient/D2Net.BridgeClient.csproj` (.NET 8 class library, no external deps). Add `tools/d2net/src/D2Net.PgdbMigrate/D2Net.PgdbMigrate.csproj` (executable, references BridgeClient). Add both to `tools/d2net/D2Net.sln`.
- [X] **T005** [P] Append to repo-root `.gitignore` per FR-029: `.pgdb/`, `.D2NET/pgdb.bak.*/`. Verify `.codeconv/tombstones/` and `.codeconv/tombstones/.orphaned/` are NOT ignored (must be checked in).

**Checkpoint**: Project skeletons exist; nothing runs yet.

---

## Phase 2: Foundational (blocking prerequisites)

**Purpose**: Vendored libraries + shared infrastructure that EVERY user story depends on. No US can start until this phase is green.

- [X] **T010** Copy `pglite_engine_kwargs.py` verbatim from `D:/BREENDEV/aigrid/AWS-Infra/src/opskit/_vendor/pglite_engine_kwargs.py` → `codeconv/src/codeconv/_vendor/pglite_engine_kwargs.py`. Preserve module docstring. Add `# Vendored from <upstream-path>; do not edit locally.` header.
- [X] **T010a** [P] Inspect `tools/d2net/src/D2Net.Init/SchemaInitializer.cs` and `tools/d2net/src/D2Net.Init/Schema/` to determine which schema(s) D2NET currently uses against `.D2NET/pgdb/`. Document the actual schema name(s) in `data-model.md` under a new subsection "§ D2NET schemas". Read-only task — no code changes (FR-015 explicitly forbids D2NET schema rewrite). Per research R14.
- [X] **T011** [P] Copy `pglite_compat_loaders.py` verbatim from `D:/BREENDEV/aigrid/AWS-Infra/src/opskit/_vendor/pglite_compat_loaders.py` → `codeconv/src/codeconv/_vendor/pglite_compat_loaders.py`. Same header convention.
- [X] **T012** [P] Author `codeconv/src/codeconv/_vendor/__init__.py` (empty marker so the package imports cleanly). Same in `codeconv/src/codeconv/__init__.py`, `codeconv/src/codeconv/tools/__init__.py`.
- [X] **T013** Amend `prereq-patterns/pglite/description.md` per FR-012: add a section clarifying that the canonical bridge IS the live deployment for repo-wide `.pgdb/` use; the "copy the bridge into your feature working tree" guidance from feature 011 still applies for feature-private PGLite deployments only.
- [X] **T014** [P] Verify `dotnet build tools/d2net/D2Net.sln` succeeds (empty BridgeClient + PgdbMigrate skeletons must compile). Note any pre-existing warnings; do NOT fix unrelated issues.

**Checkpoint**: Foundation ready. User-story phases can begin.

---

## Phase 3: US1 — Repo-wide unified PGLite with cross-process exclusion (Priority: P1) 🎯 MVP precondition

**Goal**: Single bridge per repo, OS-level lock, sidecar JSON, auto-spawn protocol, log rotation. EVERY downstream PGLite consumer depends on this.

**Independent Test**: SC-001 (parallel start race), SC-002 (post-kill restart), SC-003 (concurrent two-stack 100-cycle).

### Tests for User Story 1 ⚠️

- [X] **T020** [P] [US1] Write `prereq-patterns/pglite/tests/lock_single_writer.test.mjs` (`node --test`): spawns two bridges in parallel against a temp data-dir; expects exactly one `BRIDGE_READY`, the other exits 5 within 1 s. Maps to SC-001.
- [X] **T021** [P] [US1] Write `prereq-patterns/pglite/tests/sidecar_roundtrip.test.mjs`: spawns one bridge, reads `bridge.json`, opens TCP, sends `SELECT 1;`, expects `1`.
- [X] **T022** [P] [US1] Write `prereq-patterns/pglite/tests/post_kill_restart.test.mjs`: spawns bridge, force-kills (SIGKILL/Stop-Process), verifies a fresh start succeeds within 1 s with no manual lock cleanup. Maps to SC-002.
- [X] **T023** [P] [US1] Write `prereq-patterns/pglite/tests/log_rotation.test.mjs`: writes >5 MB of synthetic log content, verifies `bridge.log.1`, `.log.2`, `.log.3` rotation per FR-030 + R9.

### Implementation for User Story 1

- [X] **T024** [US1] Modify `prereq-patterns/pglite/pglite_bridge.mjs` per `contracts/bridge_lifecycle.md` and `contracts/bridge_cli.md`:
   - Import `proper-lockfile`.
   - Acquire `<data-dir>/.bridge.lock` BEFORE `PGlite.create()`. On failure: read `<data-dir>/bridge.json` if present, log `[bridge] BRIDGE_LOCK_HELD pid=<n> at <host>:<port>`, exit 5.
   - Default `--port 0` (ephemeral); log resolved port on `BRIDGE_READY`.
   - Atomic-write `<data-dir>/bridge.json` (tmp + rename) AFTER `listen()` AND BEFORE `BRIDGE_READY` stdout token.
   - Emit `BRIDGE_READY port=<n> pid=<p>\n` exactly once.
   - With `--daemon`: redirect stdout/stderr to size-rotated `<data-dir>/bridge.log` (5 MB × 3, R9). Implementation inline; no log lib.
   - On SIGTERM/SIGINT/beforeExit: `server.close()`, best-effort `unlink(bridge.json)`, exit 0.
   - Add `--no-lock` flag (skips lock; tests only).
   - Preserve all existing behaviour (`globalWorkChain`, `endsAtFlushBoundary`, synthetic ROLLBACK on startup, 0.2.x API surface). NO regression of FR-005 invariants.
- [X] **T025** [P] [US1] Run `node --test prereq-patterns/pglite/tests/` and confirm T020–T023 pass.
- [X] **T026** [P] [US1] Add `prereq-patterns/pglite/tests/concurrent_two_stack.test.mjs` smoke harness: launches bridge, fires 100 cycles of `psql`-based `SELECT 1` from a child process. Maps to SC-003 first half. (The full SC-003 needs Python + .NET clients; covered by integration test in Phase 7.)

**Checkpoint**: US1 complete. Bridge enforces single-process invariant; SC-001 / SC-002 / SC-003 (bridge-side) green.

---

## Phase 4: US2 — D2NET migrates to the unified bridge with zero data loss (Priority: P1)

**Goal**: `.D2NET/pgdb/` → `.pgdb/` migration; D2NET tools become unified-bridge clients; zero behaviour regression.

**Independent Test**: SC-004 (row counts preserved), SC-005 (D2NET commands regression-free).

### Tests for User Story 2 ⚠️

- [ ] **T030** [P] [US2] Write `tools/d2net/tests/D2Net.BridgeClient.Tests/AcquireOrDiscover.cs` (xunit): spawns lock-winner client; second client reads sidecar; both end with same `(host, port)`. Mirrors `codeconv/tests/test_bridge_client.py::test_lock_race_fallback`.
- [ ] **T031** [P] [US2] Write `tools/d2net/tests/D2Net.PgdbMigrate.Tests/HappyPath.cs`: source present, target absent → backup taken, move succeeds, row counts in source vs target match (SC-004).
- [ ] **T032** [P] [US2] Write `tools/d2net/tests/D2Net.PgdbMigrate.Tests/Idempotent.cs`: re-invoke after success → no-op (FR-009).
- [ ] **T033** [P] [US2] Write `tools/d2net/tests/D2Net.PgdbMigrate.Tests/RefuseOnConflict.cs`: both source and target present non-empty → exit 78 without `--force` (FR-008).
- [ ] **T034** [P] [US2] Write `tools/d2net/tests/D2Net.PgdbMigrate.Tests/CrashRecovery.cs`: simulate mid-move kill, re-run, verify clean state.

### Implementation for User Story 2

- [ ] **T035** [US2] Implement `tools/d2net/src/D2Net.BridgeClient/BridgeClient.cs` per `contracts/bridge_lifecycle.md`:
   - `BridgeEndpoint AcquireOrDiscover(string repoRoot, TimeSpan readyTimeout)`.
   - Lock via `FileStream(.pgdb/.bridge.lock, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)`.
   - On lock-won: spawn detached `node prereq-patterns/pglite/pglite_bridge.mjs --data-dir .pgdb --port 0 --daemon` with Windows `DETACHED_PROCESS | CREATE_NEW_PROCESS_GROUP` flags; read `BRIDGE_READY` line from piped stdout within `readyTimeout`; close pipes; return endpoint.
   - On lock-lost: read `.pgdb/bridge.json`; on absence retry once after 250 ms; return endpoint.
   - `IDisposable`: release the lock if owned; do NOT terminate the bridge.
- [ ] **T036** [P] [US2] Implement `tools/d2net/src/D2Net.BridgeClient/SidecarFile.cs`: `Read()`, `Write(...)` (atomic via `tmp + rename`), shape per `data-model.md` § 2.
- [ ] **T037** [US2] Implement `tools/d2net/src/D2Net.PgdbMigrate/Program.cs` per `contracts/d2net_pgdb_migration_cli.md`:
   - State machine R8 (4 cases: absent / present-tgt-absent / present-tgt-empty / present-tgt-nonempty).
   - Backup via `robocopy /MIR` on Windows, `cp -r` on POSIX.
   - Atomic rename for same-volume; copy+delete for cross-volume.
   - Write `.pgdb/.migration-record.json`.
   - Exit codes: 0 / 1 / 64 / 73 / 78.
- [ ] **T038** [US2] Modify `tools/d2net/src/D2Net.Init/PgBridgeProcess.cs` to delegate to `D2Net.BridgeClient.AcquireOrDiscover(...)` instead of self-launching its own bridge. Remove `tools/d2net/src/D2Net.Init/pgbridge/` (the vendored bridge copy is no longer the source of truth — FR-012). Remove any pgbridge bundle path-resolution code. **Preserve FR-027 connection-string flags** for any psqlODBC / Npgsql connection D2NET opens against the unified bridge: `Pooling=false` (Npgsql), and `Pooling=false; UseDeclareFetch=0` (psqlODBC) per `OdbcConnectionStringBuilder.cs`. Do NOT call `NpgsqlCommand.Prepare()` anywhere.
- [ ] **T039** [US2] If `tools/d2net/src/D2Net.Scaffold/` currently launches its own bridge, modify it the same way as T038 to consume `D2Net.BridgeClient`. (If it always delegated to `D2Net.Init`'s bridge, no change needed — verify by reading `D2Net.Scaffold/Program.cs`.)
- [ ] **T040** [US2] Update D2NET tests (`tools/d2net/tests/`) to point existing integration tests at `.pgdb/` instead of `.D2NET/pgdb/`. Verify all pre-existing D2NET tests still pass — SC-005.
- [ ] **T041** [US2] Run `dotnet test tools/d2net/D2Net.sln`; confirm T030–T034 plus all pre-existing D2NET tests are green.
- [ ] **T042** [P] [US2] Author `.claude/skills/D2NET-pgdb-migrate/SKILL.md` per `contracts/d2net_pgdb_migration_cli.md` § slash skill behaviour. Thin wrapper, mirrors `/D2NET-init` shape; inserts confirmation gate when `--force` is in args.
- [ ] **T043** [US2] Modify `.claude/skills/D2NET-init/SKILL.md` and `.claude/skills/D2NET-scaffold/SKILL.md` to reflect the new unified-bridge target (e.g., paths from `.D2NET/pgdb/` → `.pgdb/` in any examples; remove any "bridge port" references that no longer apply now that auto-spawn handles it). Do NOT change Step protocols beyond what FR-010/FR-011 require.

**Checkpoint**: US2 complete. D2NET runs against `.pgdb/`; existing D2NET behaviour preserved (SC-005); SC-004 row-count parity verified.

---

## Phase 5: US3 — `/codeconv-runner` skill + Python tool with DBOS-on-PGLite (Priority: P2)

**Goal**: Python CLI `codeconv` with file-system tool registry, DBOS-on-PGLite engine, sibling `/codeconv-<name>` skills.

**Independent Test**: `codeconv list` shows `discover`; `codeconv doctor` reports green; new tool addition requires no runner edits (FR-016).

### Tests for User Story 3 ⚠️

- [ ] **T050** [P] [US3] Write `codeconv/tests/test_bridge_client.py`:
   - `test_acquire_or_discover_lock_winner` — owner gets endpoint with `owned=True`.
   - `test_acquire_or_discover_lock_loser` — second caller reads sidecar, `owned=False`, same `(host, port)`.
   - `test_post_kill_restart` — kill bridge, fresh acquire succeeds within 1 s (SC-002 Python parity).
   - `test_ready_timeout` — slow / hung bridge → raise `BridgeStartupTimeout`.
- [ ] **T051** [P] [US3] Write `codeconv/tests/test_runner_registry.py`:
   - `test_iter_modules_finds_discover` — `pkgutil.iter_modules` lists `discover` after Phase 6 lands.
   - `test_missing_app_attribute_warns` — drop a malformed `tools/_broken/__init__.py` without `app`; runner warns and continues.
- [ ] **T052** [P] [US3] Write `codeconv/tests/test_engine.py`:
   - `test_engine_kwargs_applied` — verify `pool_size=1`, `prepare_threshold=None`, `application_name='codeconv'`.
   - `test_apply_to_engine_installed` — read a `timestamptz` column from `dbos.workflow_status` (or any DBOS table); does not crash.
   - `test_dbos_compat_patch_applied_before_launch` — assertion-style: monkey-patch `_apply_pglite_compat_patch` and confirm it's called before `dbos.launch()`.

### Implementation for User Story 3

- [ ] **T053** [US3] Implement `codeconv/src/codeconv/bridge_client.py` per `contracts/bridge_lifecycle.md`:
   - `acquire_or_discover(repo_root, ready_timeout=10) -> BridgeEndpoint` named tuple.
   - Lock via `portalocker>=2.8` (declared in T002): `portalocker.Lock(.pgdb/.bridge.lock, mode='wb', flags=portalocker.LOCK_EX|portalocker.LOCK_NB)` — kernel-managed release on process exit, mirrors Node-side `proper-lockfile` semantics.
   - Detached spawn shape per R2.
   - Atomic sidecar read.
- [ ] **T054** [US3] Implement `codeconv/src/codeconv/db/engine.py`:
   - `build_engine(endpoint) -> Engine` — `create_engine(url, **pglite_engine_kwargs(application_name='codeconv'))` then `apply_to_engine(engine)`.
   - `setup_dbos(endpoint) -> DBOS` — `_apply_pglite_compat_patch()` THEN `DBOS(config=DBOSConfig(database_url=url, db_engine_kwargs=..., schema='dbos'))` THEN `dbos.launch()` THEN `apply_to_engine(dbos.app_db.engine)`.
- [ ] **T055** [US3] Implement `codeconv/src/codeconv/db/migrations/` Alembic skeleton with `env.py` using `NullPool + AUTOCOMMIT` per applicability.md § Alembic. Migration `0001_codeconv_schema.py` creates the `codeconv` schema and tables per `data-model.md` § 1: `dart_files`, `dart_imports` (UNIQUE `(from_path, to_path)`), `dart_callers` (same UNIQUE), `dart_files_orphaned`, `discover_runs`.
- [ ] **T056** [US3] Implement `codeconv/src/codeconv/runner.py`:
   - `get_dbos()` accessor (singleton initialised at CLI start).
   - `tool_registry()` returns `[(name, app, register_workflows_or_None), ...]` via `pkgutil.iter_modules` per R10.
- [ ] **T057** [US3] Implement `codeconv/src/codeconv/cli.py`:
   - Top-level Typer app; global flags per `contracts/codeconv_runner_cli.md`.
   - Commands: `list`, `doctor`, `migrate`, plus dynamic `add_typer(tool.app, name=tool.name)` for each registered tool.
   - The `migrate` command runs Alembic upgrade head FIRST (creates `codeconv` schema + tables), THEN `dbos.migrate()` (creates `dbos` schema + tables). Re-running `migrate` against an already-migrated DB MUST be a no-op (Alembic's standard behaviour; DBOS migrations are also idempotent).
   - Exit codes per contract.
- [ ] **T058** [P] [US3] Run `pytest codeconv/tests/`; confirm T050, T052 pass. (T051 will pass after T060 lands the discover tool — note dependency in test fixtures.)
- [ ] **T059** [P] [US3] Author `.claude/skills/codeconv-runner/SKILL.md` per `contracts/codeconv_runner_cli.md`. Thin wrapper; mirrors `/opskit-init` and `/D2NET-init` shape; forwards args verbatim to `codeconv` console script.

**Checkpoint**: US3 complete. `codeconv list` works (returns empty registry until US4 lands `discover`); `codeconv doctor` reports green; runner architecture is FR-016 compliant.

---

## Phase 6: US4 — `/codeconv-discover` builds the Dart inventory + tombstones (Priority: P2)

**Goal**: First registered codeconv tool. Walks `glp_runtime_net/`, populates `codeconv` schema, writes `.codeconv/tombstones/`. Round-trips via `--from-tombstones`.

**Independent Test**: SC-006 (128 rows + 128 tombstones), SC-007 (rebuild bit-for-bit), SC-008 (idempotence), SC-009 (resume after kill), SC-013 (perf).

### Tests for User Story 4 ⚠️

- [ ] **T060** [P] [US4] Write `codeconv/tests/test_walker.py`:
   - `test_walks_dart_files_only` — `*.py`, `*.txt` ignored.
   - `test_excludes_generated` — `*.g.dart`, `*.freezed.dart`, `*.gen.dart` excluded.
   - `test_excludes_dart_tool_and_build` — paths under `.dart_tool/` and `build/` excluded.
   - `test_does_not_follow_outward_symlinks` — symlink to outside subtree → not followed.
- [ ] **T061** [P] [US4] Write `codeconv/tests/test_parse.py`:
   - `test_extracts_leading_doc_comment_triple_slash` — `///` block captured verbatim.
   - `test_extracts_leading_doc_comment_block` — `/** */` block captured.
   - `test_no_doc_comment_returns_empty` — file with no leading doc → `purpose=''`.
   - `test_extracts_imports_relative` — `import 'foo.dart';` resolved correctly.
   - `test_skips_package_and_dart_imports` — `import 'package:foo/bar.dart';` skipped.
   - `test_dedupes_duplicate_imports` — same import twice → one row, warning logged.
- [ ] **T062** [P] [US4] Write `codeconv/tests/test_tombstone.py`:
   - `test_write_then_read_roundtrip` — tombstone writes then reads frontmatter back identically.
   - `test_yaml_field_ordering_stable` — emitted YAML field order matches contract.
   - `test_path_uses_posix_separators` — Windows paths in YAML use forward slashes (R7).
   - `test_dependencies_sorted_lexically` — for diff stability.
- [ ] **T063** [P] [US4] Write `codeconv/tests/test_discover_idempotence.py`:
   - `test_second_run_zero_diff_db` — re-run produces zero row diff in `dart_files`/`dart_imports`/`dart_callers`. SC-008.
   - `test_second_run_zero_diff_tombstones` — re-run produces zero file diff in `.codeconv/tombstones/`. SC-008.
- [ ] **T064** [P] [US4] Write `codeconv/tests/test_discover_orphan_revival.py`:
   - `test_orphan_on_delete` — file gone → row moved to `dart_files_orphaned`, tombstone moved to `.orphaned/`.
   - `test_revive_on_reappear` — orphan reappears → row moved back, tombstone moved back, mtime + sha256 refreshed (FR-025).
   - `test_orphan_edges_recomputed` — revived file's import + caller edges are recomputed.
- [ ] **T065** [P] [US4] Write `codeconv/tests/test_from_tombstones.py`:
   - `test_rebuild_from_tombstones_equals_normal` — drop `codeconv` schema, run `--from-tombstones`, dump structurally identical to a normal-mode dump (SC-007).
   - `test_from_tombstones_does_not_read_dart` — instrumented file-read counter shows zero `.dart` reads.
- [ ] **T066** [P] [US4] Write `codeconv/tests/test_outside_subtree_warning.py`:
   - `test_outside_caller_warns_no_edge` — synthetic outside `.dart` file imports an inside file; discover emits warning, NOT a caller edge (FR-023).
- [ ] **T067** [P] [US4] Write `codeconv/tests/test_resume_after_kill.py`:
   - `test_resume_skips_processed_files` — kill workflow mid-run, re-invoke; instrumented step counter shows files 1..N not re-parsed (SC-009 + FR-017).
- [ ] **T068** [P] [US4] Write `codeconv/tests/test_discover_perf.py` (marked `@pytest.mark.perf`; opt-in via `--run-perf` flag):
   - `test_fresh_checkout_under_60s` — fresh DB + fresh tombstone tree on `glp_runtime_net/` (128 files): completes ≤ 60 s. SC-013.
   - `test_idempotent_under_5s` — re-run on unchanged source: completes ≤ 5 s. SC-013.

### Implementation for User Story 4

- [ ] **T069** [US4] Implement `codeconv/src/codeconv/tools/discover/walker.py` per `contracts/codeconv_discover_cli.md` § Subtree scope. Returns iterator of `(absolute_path, relative_path)` tuples, POSIX-normalised relative paths.
- [ ] **T070** [US4] Implement `codeconv/src/codeconv/tools/discover/parse.py`:
   - `extract_leading_doc(path) -> str` per R11 (200-line cap).
   - `extract_imports(path, repo_root_glp_runtime_net) -> list[str]` per R12 (skip `package:`/`dart:`, resolve relative, filter to inside-subtree).
- [ ] **T071** [US4] Implement `codeconv/src/codeconv/tools/discover/tombstone.py`:
   - `write_tombstone(rel_path, fields)` — writes `.codeconv/tombstones/<rel>.dart.md` per `contracts/tombstone_format.md`. Pinned YAML emitter settings.
   - `read_tombstone(path) -> dict` — frontmatter only.
   - `move_to_orphaned(rel_path)`, `move_from_orphaned(rel_path)`.
- [ ] **T072** [US4] Implement `codeconv/src/codeconv/tools/discover/workflow.py`:
   - DBOS `@workflow` `discover_workflow(run_id, mode, root)`.
   - DBOS `@step` `process_file(rel_path)` — idempotence short-circuit on (mtime, sha256), parse, UPSERT `dart_files`, replace `dart_imports`/`dart_callers` for this file, write tombstone.
   - Reconciliation phase: recompute `dart_callers` from full `dart_imports`; orphan files no longer present; revive previously-orphaned files now present (FR-025); emit warnings.
   - `--from-tombstones` mode: TRUNCATE then read `.codeconv/tombstones/**/*.dart.md` and INSERT.
   - `register(dbos_app)` — register workflows.
- [ ] **T073** [US4] Implement `codeconv/src/codeconv/tools/discover/__init__.py`:
   - Export `app: typer.Typer` per `contracts/codeconv_tool_contract.md`.
   - Commands: `run` (default; with `--from-tombstones`, `--root`, `--quiet`, `--json`, `--dry-run`, `--no-orphan-revival`).
   - Export `register_workflows(dbos_app)`.
- [ ] **T074** [P] [US4] Author `.claude/skills/codeconv-discover/SKILL.md` per `contracts/codeconv_tool_contract.md` § companion skill. Thin wrapper; forwards args verbatim to `codeconv discover`.
- [ ] **T075** [US4] Run full `pytest codeconv/tests/` (excluding perf marks); confirm all tests T060–T067 + T051 from Phase 5 pass.
- [ ] **T076** [US4] Run perf tests with `--run-perf`; confirm T068 passes (≤ 60 s fresh, ≤ 5 s idempotent on `glp_runtime_net/` 128 files). SC-013.

**Checkpoint**: US4 complete. Discover produces SC-006 inventory; SC-007 / SC-008 / SC-009 / SC-013 verified.

---

## Phase 7: Polish & cross-cutting concerns

**Purpose**: Verification across stories, documentation alignment, end-to-end smoke.

- [ ] **T080** [P] Run `quickstart.md` Flow A end-to-end on a fresh checkout. Confirm all assertions hold. Capture any deviations as bugs (NOT silent fixes — per CLAUDE.md Bug Protocol).
- [ ] **T081** [P] Run `quickstart.md` Flow B end-to-end. SC-004 + SC-005 verified.
- [ ] **T082** [P] Run `quickstart.md` Flow C end-to-end. SC-006 + SC-008 + SC-010 + SC-013 verified.
- [ ] **T083** [P] Run `quickstart.md` Flow D end-to-end. SC-007 verified (logical dump bit-for-bit).
- [ ] **T084** Run `quickstart.md` Flow E (concurrent two-stack 100 cycles, Python + .NET against the same bridge). SC-003 fully verified. **Note**: requires drafting `specs/012-codeconv-runner/scripts/sc003_python_loop.py` and `specs/012-codeconv-runner/scripts/Sc003NpgsqlLoop/`. Author both as part of this task.
- [ ] **T085** [P] Run `quickstart.md` Flow F (resume after kill). SC-009 verified.
- [ ] **T086** [P] Inspect PGLite schema list via `\dn` (or `codeconv doctor --schemas`). Confirm at minimum `dbos`, `codeconv`, plus whatever D2NET uses. Confirm zero codeconv tables outside `codeconv`, zero DBOS tables outside `dbos`. SC-012.
- [ ] **T087** [P] Query `codeconv.dart_callers` for any `to_path` outside `glp_runtime_net/` — expect zero rows. SC-011.
- [ ] **T088** [P] Inspect bridge log: confirm no leakage to spawning client terminals across all flows; confirm `bridge.log.1`/`.log.2`/`.log.3` rotation actually happened on a stress test.
- [ ] **T089** Update CLAUDE.md spec section to note `.pgdb/`, `.codeconv/`, and `tools/d2net/src/D2Net.BridgeClient/` as new repo-level directories. Add a one-paragraph "Migration to unified bridge" note under "Project Coordination" or analogous heading.
- [ ] **T090** Update `docs/known-issues.md` with any caveats discovered during T080–T088 (e.g., proper-lockfile Windows-specific behaviours; DBOS-on-PGLite gotchas not already in applicability.md).
- [ ] **T091** [P] Grep all .NET source under `tools/d2net/src/D2Net.BridgeClient/`, `tools/d2net/src/D2Net.PgdbMigrate/`, and any modified files in `tools/d2net/src/D2Net.Init/` and `tools/d2net/src/D2Net.Scaffold/` for connection strings missing `Pooling=false` (Npgsql) or missing `Pooling=false; UseDeclareFetch=0` (psqlODBC), and for any `.Prepare()` invocation on Npgsql commands. Each match is an FR-027 violation; fix before merge.
- [ ] **T092** [P] Grep all introduced source (`codeconv/`, `tools/d2net/src/D2Net.BridgeClient/`, `tools/d2net/src/D2Net.PgdbMigrate/`) and modified bridge file `prereq-patterns/pglite/pglite_bridge.mjs` for `COPY ... FROM STDIN` (case-insensitive). Any match is an FR-026 violation; fix before merge.

**Final Checkpoint**: Every spec acceptance scenario verified. Every SC-001 through SC-013 has a passing test or quickstart flow attached.

---

## Dependencies & execution order

### Phase dependencies

- Phase 1 (Setup) → no dependencies; can start immediately.
- Phase 2 (Foundational) → depends on Phase 1.
- Phase 3 (US1) → depends on Phase 2; BLOCKS US2, US3, US4.
- Phase 4 (US2) → depends on Phase 3.
- Phase 5 (US3) → depends on Phase 3.
- Phase 6 (US4) → depends on Phase 5.
- Phase 7 (Polish) → depends on US1 + US2 + US3 + US4.

### User-story parallelism

After Phase 3 (US1) lands, US2 and US3 are independent and can be staffed in parallel by different developers if available. US4 depends on US3.

### Within each user story

- Tests MUST be written and asserted to FAIL (red) before implementation tasks land. Per Spec-First Development discipline (CLAUDE.md).
- Models/schema before workflow code.
- Workflow code before CLI surface.
- CLI surface before slash skill wrapper.
- Story complete (all SC items green for that story) before moving to next story.

### Parallel opportunities

- T002, T003, T004, T005 in Phase 1 — different files, parallel.
- T010, T011, T012, T013, T014 in Phase 2 — different files (T012 also depends on existence of T010+T011 file paths but the empty `__init__.py` files can land first; treat as parallel).
- T020–T023 (US1 tests) — parallel with each other.
- T030–T034 (US2 tests) — parallel.
- T050–T052 (US3 tests) — parallel.
- T060–T068 (US4 tests) — parallel.
- T080–T088 (Phase 7 verifications) — parallel.

### Cross-phase scheduling notes

- T024 (bridge changes) MUST land before T035 (D2Net.BridgeClient) and T053 (Python bridge_client.py) start integration testing — both depend on the bridge actually behaving per the new contract.
- T055 (Alembic codeconv migration) MUST run successfully (creating the schema in `.pgdb/`) before T072 (workflow that writes to those tables) can pass tests.

---

## Implementation strategy

### MVP-first path

1. Complete Phase 1 + Phase 2 (foundation).
2. Complete Phase 3 (US1) — bridge with cross-process exclusion. **STOP and verify SC-001 / SC-002 / SC-003 (bridge-side) green.**
3. Complete Phase 4 (US2) — D2NET migrated. **STOP and verify SC-004 / SC-005 green.** This is the precondition layer + first regression check; it is the practical MVP gate.
4. Complete Phase 5 (US3) — codeconv runner + DBOS. Verify `codeconv list` and `codeconv doctor`.
5. Complete Phase 6 (US4) — discover tool. Verify SC-006 / SC-007 / SC-008 / SC-009 / SC-013.
6. Complete Phase 7 (Polish).

### Incremental delivery

Each story's checkpoint is independently demoable:

- After US1: "the unified bridge starts and refuses double-bridge attempts."
- After US2: "D2NET still works; data didn't move location… well, it did, but it works."
- After US3: "`/codeconv-runner` shows registered tools."
- After US4: "`/codeconv-discover` emits a complete inventory + tombstones; `--from-tombstones` reproduces it bit-for-bit."

---

## Notes

- Tests are NOT optional for this feature — spec defines 13 measurable SCs with explicit verification, and CLAUDE.md mandates baseline-then-change-then-test discipline.
- Commit after each task or logical group. Branch is `012-codeconv-runner`. Stage by file name; never `git add -A` (per CLAUDE.md git workflow).
- Stop at any checkpoint to validate independently. Per CLAUDE.md, the user's "stop"/"wait" overrides any in-flight task.
- Avoid: vague tasks (every task above names files), same-file conflicts (parallel tasks touch different files), cross-story dependencies that break independence.
- If `proper-lockfile` exhibits unexpected Windows behaviour during T024 implementation, STOP and escalate per spec Assumptions before lowering the lock guarantee.
