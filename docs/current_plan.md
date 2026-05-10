# Current Plan: 012-codeconv-runner — `/speckit-implement` (mid-flight)

**Branch**: `012-codeconv-runner` (pushed to origin)
**Started**: 2026-05-09 (spec-kit chain) → continued through Phase 4
**Last commit**: `c34f013a` — Phase 4 done & pushed
**Resume point**: Phase 5 (US3 codeconv runner — Python package + DBOS + Alembic)

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
- [ ] 6. /speckit-implement — **Phase 5 (US3 codeconv runner)** T050–T059 ← **CURRENT**
- [ ] 7. /speckit-implement — **Phase 6 (US4 codeconv-discover)** T060–T076
- [ ] 8. /speckit-implement — **Phase 7 (Polish)** T080–T092

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
