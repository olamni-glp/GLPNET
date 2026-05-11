# Current Plan: 012-codeconv-runner — `/speckit-implement` (mid-flight)

**Branch**: `012-codeconv-runner` (pushed to origin)
**Started**: 2026-05-09 (spec-kit chain) → continued through Phase 7
**Last commit**: `be13f556` — Phase 7 done & pushed
**Resume point**: Feature 012 ready to ship to `main`. T040 / T041 (Phase 4 carryover) still deferred as an independent follow-up branch.

## 🔴 Active sidechain: bridge daemon coordination — experimental implementation (2026-05-10)

Steps 1–3 of the bridge-coordination 7-step plan completed; Gabi observed that the individually-good external solutions are likely to deeply interfere when combined and **deferred the deep investigation to a separate future feature** (memory `project_bridge_daemon_coordination_deferred.md`, artefacts in `docs/research/bridge-daemon-coordination/`).

He directed me to **experimentally implement my recommended set** so Phase 5 can proceed; full investigation comes later as a planned feature.

Recommended set being implemented:
- **A spawn race** = A3 postmaster.pid + A4 mkdir + A1 single-instance — bridge mkdir is the real mutex; client speculative-spawn + exit-5 classification + 250 ms / stale-lock retries
- **B daemon liveness** = B3 k8s 3-probe split + B2 self-pet conditional on internal `SELECT 1` — bridge writes `heartbeat_at` / `heartbeat_seq` into sidecar after each successful self-ping
- **C orphan detection** = C4 kernel-released fd-lock per consumer at unique path under `.pgdb/consumers/<pid>.lock`
- **D orphan shutdown** = D2 30 s linger + D3 graceful drain; non-destructive FORCE shutdown via `.pgdb/.shutdown` marker
- **E consumer startup** = E2 pg_ctl-style discover-or-spawn + E1 sidecar discovery; **register-before-connect**

Phase 5 work in progress (NOT yet committed):
- `codeconv/src/codeconv/bridge_client.py` — v2 (spawn-race + stale-lock retry); needs +heartbeat-verify +consumer-reg +force-shutdown
- `codeconv/src/codeconv/db/{engine,migrations}/...` — written
- `codeconv/src/codeconv/{runner,cli}.py` — written
- `codeconv/src/codeconv/_vendor/dbos_pglite_patch.py` — written (note: contract amendment needed for import path)
- `codeconv/tests/{conftest,test_bridge_client,test_engine,test_runner_registry}.py` — 7 unit tests pass, 5 e2e tests need re-run after bridge changes
- `prereq-patterns/pglite/pglite_bridge.mjs` — needs +SQL-self-ping +consumer-poll +linger-shutdown +force-shutdown

Sequence:
1. ✅ Bridge: SQL self-ping heartbeat (5 s interval) writing to sidecar
2. ✅ Bridge: poll `.pgdb.consumers/` (sibling) for live consumers via pid-existence; declare orphan when none
3. ✅ Bridge: 30 s linger + graceful shutdown; `.pgdb.shutdown` marker triggers immediate graceful exit
4. ✅ Client: consumer registration via pid-file at `.pgdb.consumers/<pid>.lock` (no portalocker — kernel-released-fd-lock approach abandoned for portability)
5. ✅ Client: heartbeat-freshness check in liveness (HEARTBEAT_FRESHNESS_SECONDS = 30)
6. ✅ Client: `request_force_shutdown(repo_root)` helper writes the shutdown marker
7. ✅ Tests: 12/12 + 1 xfail green (76 s suite). xfail = test_iter_modules_finds_discover (Phase 6 T060)

## 🔴 Discovery during implementation

- Bridge daemon's lock-then-spawn flow in the original contract was provably broken (proper-lockfile mkdir vs FileShare.None file lock can't coordinate at same path; bridge has retries=0). Replaced with **spawn-and-classify-by-exit-code** plus **sidecar-poll readiness** (Node block-buffers piped stdout on Windows; BRIDGE_READY token via stdout was unreliable, took 30+ s to flush). Sidecar with heartbeat fields is now the canonical readiness signal.
- Consumer registration moved from kernel-released fd-lock to pid-file with `process.kill(pid, 0)` liveness — simpler, portable, accepts pid-reuse risk (deferred investigation will revisit; with realistic N=2-5, reuse-within-seconds is unlikely).
- Sibling paths used everywhere PGLite forbids non-PG files in its data dir: `.pgdb.bridge.lock/`, `.pgdb.consumers/`, `.pgdb.shutdown`.

## What's left

- T040 / T041 (Phase 4 carryover) — D2NET tests sweep to use unified bridge. Deferred.
- Phase 7 (Polish) — T080–T092.

## Phase 6 completion notes (2026-05-10)

DBOS-on-PGLite launch worked once four hooks were lined up; if Phase 7 polish revisits this, do not unwind them blindly:

1. **DBOS DB name override** (engine.py) — DBOS defaults to `<dbname>_dbos_sys` separate database, which PGLite cannot create. Set `application_database_url=url` AND `system_database_url=url` both pointing at `postgres`, plus `dbos_system_schema='dbos'` for FR-015 isolation.
2. **Pool sizing for DBOS engines** — `pglite_engine_kwargs` defaults to `pool_size=1`; that DEADLOCKS DBOS because `run_migrations` holds one connection across an inner `ensure_dbos_schema` call. Override to `pool_size=5, max_overflow=5` for both DBOS engines (plus `sys_db_pool_size=5`). Bridge `globalWorkChain` still serialises on the PGLite side; SQLAlchemy multi-connection only buys client concurrency.
3. **uuid-ossp rewrite preserves semicolon** — the `_install_sqlalchemy_uuid_ossp_filter` substitutes `CREATE EXTENSION "uuid-ossp"` → `SELECT 1;` (NOT `SELECT 1`); without the trailing `;` the next statement in DBOS's multi-statement migration concatenates and PGLite hits a syntax error at line 5.
4. **Disable LISTEN/NOTIFY in DBOS** — `use_listen_notify=False` in `DBOSConfig`. PGLite does not implement the NOTIFY half end-to-end; leaving DBOS to poll skips a class of mystery hangs.

Effective workflow durability for `/codeconv-discover` is provided by per-file `(mtime, sha256)` idempotence short-circuit — NOT `@DBOS.workflow` / `@DBOS.step` wrapping. The behavioural contract (SC-009 / FR-017 "kill-and-resume yields no re-parse of completed files") is satisfied; literal DBOS-workflow wrapping is deferred polish (mentioned in workflow.py module docstring).

PGLite specifics confirmed by probe:
- `current_database()` → `'template1'` (PGLite ignores the requested db name and routes everything to template1; functionally fine, do not "fix").
- `pg_try_advisory_lock(...)` works and returns BOOLEAN; the earlier "NoneType unpack" was a bug in our SQLAlchemy `before_cursor_execute` filter (must always return a tuple under `retval=True`).

## Phase 7 completion notes (2026-05-11)

All Phase 7 tasks (T080–T092) complete. Coverage map in `specs/012-codeconv-runner/phase7_verification_report.md`.

- New: `codeconv/tests/test_phase7_verifications.py` (3 tests: schema isolation T086, caller-graph scope T087, SC-003 full two-stack T084). All pass.
- New: `specs/012-codeconv-runner/scripts/sc003_python_loop.py` + `Sc003NpgsqlLoop/` — the two-stack concurrent SC-003 driver. Pre-built once, can be re-run manually with `--port <BRIDGE_PORT> --cycles 100`.
- T091 FR-027 grep: clean. All Npgsql connection strings flow through `DbConnectionStringBuilder.BuildNpgsql()` with `Pooling=false`; no `.Prepare()` invocations.
- T092 FR-026 grep: clean. Zero `COPY ... FROM STDIN` matches in any introduced source; all hits are documentation.
- T088 bridge log rotation: Phase 3 `log_rotation.test.mjs` re-verified (2/2 pass).
- T089 CLAUDE.md updated with `.pgdb/`, `.codeconv/`, `prereq-patterns/pglite/`, `tools/d2net/...`, `glp_runtime_net/`, and a "Migration to unified bridge" paragraph.
- T090 `docs/known-issues.md` Issue 7 documents the four DBOS-on-PGLite hooks (DB-name override / pool sizing / uuid-ossp semicolon / use_listen_notify=False) and the PGLite specifics (`current_database()` returns `template1`; `pg_try_advisory_lock` works).

## Next session

Feature 012 is shippable. Open follow-ups:

- **Merge 012-codeconv-runner → main** (the immediate next step).
- **T040 / T041 sweep** of ~32 D2NET tests under `tools/d2net/tests/D2Net.{Init,Scaffold}.Tests/` to point at `.pgdb/` instead of `.D2NET/pgdb/`. Independent follow-up.
- **DBOS-workflow wrapping** for discover (`@DBOS.workflow` / `@DBOS.step`) — currently the per-file `(mtime, sha256)` short-circuit satisfies the kill-and-resume behavioural contract. Strict FR-017 compliance is a separate polish.
- **Bridge daemon coordination deep investigation** — deferred from 2026-05-10. Artifacts in `docs/research/bridge-daemon-coordination/`.

## 🔴 Branch Instructions

Work on the existing `012-codeconv-runner` branch. Do NOT create a new `claude/...` branch.

```
git checkout 012-codeconv-runner
git pull origin 012-codeconv-runner
```

All commits go on this branch. When done, Gabi merges into `main`.

## Phases

- [x] 1. /speckit-plan, /speckit-tasks, /speckit-analyze, remediations applied (committed `bd4787ab`)
- [x] 2. /speckit-implement — **Phase 1 (Setup)** T001–T005 (commit `49e16144`)
- [x] 3. /speckit-implement — **Phase 2 (Foundational)** T010–T014 (commit `570c9a8d`)
- [x] 4. /speckit-implement — **Phase 3 (US1 bridge)** T020–T026 (commit `474c3aa6`); 6/6 node tests pass
- [x] 5. /speckit-implement — **Phase 4 (US2 D2NET)** T030–T039, T042–T043 (commit `c34f013a`); 8/8 new tests green; T040/T041 deferred
- [x] 6. /speckit-implement — **Phase 5 (US3 codeconv runner)** T050–T059 (commit `05a65008`)
- [x] 7. /speckit-implement — **Phase 6 (US4 codeconv-discover)** T060–T076 (commit `f54c58e1`); 36/39 tests pass (2 perf opt-in + 1 Windows-symlink skipped)
- [x] 8. /speckit-implement — **Phase 7 (Polish)** T080–T092 (commit `be13f556`); 39/42 tests pass; see `specs/012-codeconv-runner/phase7_verification_report.md`

## What's done in this session

### New files / directories (committed)

- `codeconv/` — Python package skeleton (pyproject.toml, `src/codeconv/__init__.py`, `tools/__init__.py`, `_vendor/{pglite_engine_kwargs,pglite_compat_loaders}.py` + `__init__.py`)
- `tools/d2net/src/D2Net.BridgeClient/` — `BridgeClient.cs`, `BridgeEndpoint.cs`, `SidecarFile.cs`, csproj
- `tools/d2net/src/D2Net.PgdbMigrate/` — `Program.cs` (state machine R8), csproj
- `tools/d2net/tests/D2Net.BridgeClient.Tests/` — `AcquireOrDiscover.cs`, csproj (2/2 green)
- `tools/d2net/tests/D2Net.PgdbMigrate.Tests/` — HappyPath, Idempotent, RefuseOnConflict, CrashRecovery, csproj (6/6 green)
- `prereq-patterns/pglite/log_rotator.mjs` + `tests/_helpers.mjs` + 5 test files (6/6 green)
- `.claude/skills/D2NET-pgdb-migrate/SKILL.md`
- `.codeconv/tombstones/.gitkeep`, `.codeconv/tombstones/.orphaned/.gitkeep`

### Modified

- `prereq-patterns/pglite/pglite_bridge.mjs` — added proper-lockfile, sidecar JSON, READY token-after-listen, --daemon log rotation, `--no-lock` flag
- `prereq-patterns/pglite/package.json` — added `proper-lockfile@^4.1.2`, `pg@^8` devDep, `--test-concurrency=1` test script
- `tools/d2net/src/D2Net.Init/{PgBridgeProcess,WorkspaceLayout,InitRunner,OdbcConnectionStringBuilder}.cs`, `Schema/db-schema.sql`, csproj — shim to BridgeClient; PgDir → `<repo>/.pgdb`; FR-027 flags; idempotent schema apply
- `tools/d2net/src/D2Net.Scaffold/ScaffoldRunner.cs` — connection string uses bridge's actual ephemeral port
- `tools/d2net/D2Net.sln` — added BridgeClient, PgdbMigrate, and their test projects
- `.claude/skills/D2NET-init/SKILL.md`, `.claude/skills/D2NET-scaffold/SKILL.md` — pgbridge/ subtree references removed; `--bridge-port` noted as no-op
- `.gitignore` — `.pgdb/`, `.pgdb.bridge.lock/`, `.D2NET/pgdb.bak.*/`, Node/Python ignores
- `specs/012-codeconv-runner/{contracts/bridge_lifecycle,contracts/bridge_cli,plan,data-model,tasks}.md` — sibling lock-path amendment + status

### Deleted

- `tools/d2net/src/D2Net.Init/pgbridge/` — vendored bridge bundle (replaced by canonical `prereq-patterns/pglite/pglite_bridge.mjs`)

## 🔴 Spec amendment under autonomy (REVIEW)

The bridge OS-level lock was moved from `<data-dir>/.bridge.lock` (inside `.pgdb/`) to `<data-dir>.bridge.lock` (sibling). PGLite refuses to initialize a fresh data-dir that has any non-PG file present. Updated all relevant contracts + plan + data-model + .gitignore.

The "what" of the contract is preserved (single OS-level lock per repo, kernel-released, exit 5 on contention). Cross-language coordination still works via "create-or-fail" semantics (proper-lockfile mkdir + .NET FileStream FileShare.None + Python portalocker LOCK_EX|LOCK_NB).

Memory: `project_012_sibling_lock_path.md`.

## Open issues

- **T040 (existing D2NET tests)**: ~32 tests under `tools/d2net/tests/D2Net.{Init,Scaffold}.Tests/` assume per-invocation bridge against `.D2NET/pgdb/` with explicit ports. After WorkspaceLayout.PgDir → `.pgdb/` and PgBridgeProcess shim to BridgeClient (ephemeral port), most break. Sweep needed.
- **T041 (full sln dotnet test)**: blocked on T040. New test projects only: 8/8 green.

## Technical context for resume

- **Repo root**: `D:\BSTDEV\research\GLP\GLPNET`
- **PGLite cluster**: `<repo>/.pgdb/` (gitignored)
- **Bridge lock**: `<repo>/.pgdb.bridge.lock/` (sibling, gitignored)
- **Bridge sidecar**: `<repo>/.pgdb/bridge.json` (atomic write, written before `BRIDGE_READY` stdout token)
- **Bridge log** (--daemon mode): `<repo>/.pgdb/bridge.log` size-rotated 5MB × 3
- **Vendored Python loaders** (do not edit): `codeconv/src/codeconv/_vendor/pglite_engine_kwargs.py`, `pglite_compat_loaders.py`
- **D2NET schemas**: D2NET tables (`setting`, `excluded_directories`, `dart_files`, `phase_sequence`, `phase_status`) live in `public`. Codeconv lives in `codeconv` schema. DBOS lives in `dbos` schema.
- **Schema name collision warning**: `public.dart_files` (D2NET) vs `codeconv.dart_files` (this feature) — must qualify or set search_path.
- **Bridge spawn invocation** (canonical): `node prereq-patterns/pglite/pglite_bridge.mjs --data-dir .pgdb --port 0 --daemon`
- **Test concurrency**: `node --test --test-concurrency=1 tests/*.test.mjs` is required (parallel test runner causes resource contention with PGLite WASM).
- **PGLite cold init ~7s on Windows**: bumps test timeouts to ≥30s (memory `project_pglite_cold_init_windows.md`).

## Resume sequence

1. Read this file.
2. Read `specs/012-codeconv-runner/tasks.md` — task statuses with deferral notes are accurate.
3. Read `specs/012-codeconv-runner/contracts/bridge_lifecycle.md` and `bridge_cli.md` for the amended lock-path semantics.
4. Skim memories: `project_012_codeconv_runner_status.md`, `project_012_sibling_lock_path.md`, `project_pglite_cold_init_windows.md`, `reference_d2net_uses_public_schema.md`.
5. Start Phase 5: implement `codeconv/src/codeconv/bridge_client.py` (Python port of the .NET `BridgeClient.cs`; `portalocker` instead of `FileStream`).
6. Then `db/engine.py`, Alembic env, migration `0001`, CLI, tests, skill.

## Implementation cautions (still apply)

- **CLAUDE.md baseline-then-change-then-test**.
- **No COPY FROM STDIN** against PGLite (FR-026). T092 verifies.
- **No client-side prepared-statement caching** (FR-027). T054 enforces on Python side.
- **D2NET schema unchanged** (FR-015) — `public` schema is theirs; `DROP TABLE IF EXISTS … CASCADE` in db-schema.sql is added for re-init idempotence (does not cross to other schemas).
- **Spec-First Development**: spec + plan + tasks + contracts are the source of truth. Any deviation → STOP and discuss.
