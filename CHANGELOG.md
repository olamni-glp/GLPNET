# Changelog

All notable changes to GLPNET. Versions follow the CalVer convention defined in
[`docs/VERSIONING.md`](docs/VERSIONING.md): tags are `vYYYY.MM.DD[-N]` where the
optional `-N` suffix increments per same-day release.

## [v2026.04.30-5] — 2026-04-30

### Added

- **`/D2NET-init` Claude Code skill.** Wraps the spec-005 `d2net-init` CLI as a
  slash command for one-line invocation from any Claude Code session in this
  repo. Supports raw flag pass-through, key-value natural-language
  (`source=X extension=Y target=Z`), positional verbs (`init`, `list`,
  `exclusions`, `current-phase`, `help`, `version`), and a single-token
  shortcut (`/D2NET-init glp_runtime` derives `_net` defaults after
  confirmation). Auto-builds the binary on user confirmation when missing or
  stale. Confirms before destructive operations
  (`--FORCE --DELETE-EXISTING`); confirmed paths skip re-prompts within the
  same conversation. Surfaces JSON outputs verbatim regardless of size;
  plain-text outputs over 50 lines are truncated with a "show all" footer.
  Hints recovery actions for `BridgePortInUse`, `pglite_init_failed`,
  `NodeMissing`, and `WorkspaceAlreadyExists` exit codes. Casing is exactly
  `D2NET-init` (filesystem path, frontmatter, slash-command name).
- Spec under [`specs/006-d2net-init-skill/`](specs/006-d2net-init-skill/):
  spec.md (3 clarifications resolved — auto-build with single confirmation,
  JSON output bypasses truncation, single-token shortcut), plan.md,
  research.md (10 R-decisions), data-model.md, contracts/skill-contract.md,
  quickstart.md, tasks.md, validation.md.

### Notes

- The skill is purely additive — no changes to `tools/d2net/` or any existing
  test. The 89 D2Net.Init tests + 34 D2Net.Scaffold tests continue to pass
  unchanged.

## [v2026.04.30-4] — 2026-04-30

### Changed

- **`D2NET.Init` storage swap: SQLite → PGLite WASM via direct Postgres-wire bridge.**
  The shipped 002 `D2NET.Init` (v2026.04.30-2) ran on embedded SQLite via
  `Microsoft.Data.Sqlite` after the original PGLite + `pg-gateway` + ODBC stack
  failed end-to-end. The follow-up RCA (v2026.04.30-3) shipped a working
  hand-rolled bridge as a reference artefact. **This release integrates that
  bridge into D2NET.Init.** The five-table schema, all CLI flags, the
  temp-staging + atomic-rename safety pattern, and the prompt/exclusion flow
  are preserved unchanged from 002; only the storage engine and the persisted
  connection contract change. See
  [`specs/005-d2net-pglite-bridge/spec.md`](specs/005-d2net-pglite-bridge/spec.md).
- **`D2Net.Init.csproj`**: removed `Microsoft.Data.Sqlite`; added `Npgsql 8.0.3`.
  An MSBuild target now runs `npm ci` inside `pgbridge/` before compilation;
  the resulting tree (~256 MB, dominated by PGLite's bundled Postgres contrib
  extensions) is excluded from git via `pgbridge/.gitignore` but bundled into
  the build output via `<None CopyToOutputDirectory="PreserveNewest" />`.
- **`d2net-init` version bumped to `0.2.0`** to signal the storage-engine swap.
- **Default `--bridge-port`** is now `54400` (matching
  `docs/research/pgbridge-reference/`'s example). On init, the chosen port is
  persisted to `D2NET-Settings.json`'s `connection.port` and the `db_port` row
  in the `setting` table. On inspection commands, the persisted port is the
  default; `--bridge-port` on a non-init invocation overrides only the live
  run and does NOT modify settings (per FR-012 / Q3 clarification).
- **Settings JSON `connection` block reshaped**: `engine` flips from `sqlite`
  to `pglite`; `db_file` removed; `host`, `port`, `database`, `user`,
  `password`, `data_dir`, `connection_string` (Npgsql), and
  `connection_string_odbc` (`PostgreSQL ODBC Driver(UNICODE)`-style) are added.
  The `setting` table mirrors these as `db_*` keys.
- **Pre-existing SQLite-format `.D2NET` workspaces** (a `pgdb/workspace.sqlite`
  file or a settings JSON with `connection.engine != "pglite"`) are detected
  by the existing-workspace gate and refused without `--FORCE
  --DELETE-EXISTING`. No automatic data migration — re-init rebuilds from the
  source tree.

### Added

- **`tools/d2net/src/D2Net.Init/PgBridgeProcess.cs`** — IDisposable lifecycle
  wrapper for the per-invocation Node.js bridge subprocess. Spawns `node`,
  waits up to 15 s for `BRIDGE_READY`, runs the FR-006 staged shutdown on
  dispose (close stdin → 5 s → SIGTERM → 2 s → kill).
- **Vendored bridge bundle** at `tools/d2net/src/D2Net.Init/pgbridge/`:
  `bridge-direct.mjs` (verbatim port from `docs/research/pgbridge-reference/`
  with the smoke-seed `t (x INT)` table removed to preserve the
  inspection-modifies-zero-bytes invariant), `package.json` pinning
  `@electric-sql/pglite@0.2.17` as the only runtime dep, and a
  `.gitignore` for the materialized `node_modules`.
- **`scripts/verify-pgbridge-deps.ps1`** — build-time guardrail wired into
  `D2Net.Init.csproj` that walks the materialized `node_modules` and fails
  the build if `pg-gateway` is anywhere in the transitive tree (FR-008 +
  SC-010).
- **New exit codes** for bridge failures: `BridgePortInUse` (5),
  `BridgeStartFailed` (7), `NodeMissing` (10), `BridgeBundleMissing` (11).
  Pre-existing exit-code numbering preserved.
- **19 new test cases** across `PgBridgeProcessTests`,
  `BridgeStartupTests`, `InspectionPortLifecycleTests`,
  `SqliteEraDetectionTests`, `ExternalClientTests`, plus extended
  `WorkspaceLayoutTests` for SQLite-era detection. Total D2Net.Init test
  count: 89/89 passing. `D2Net.Scaffold.Tests` unaffected (34/34 passing).

### Speckit artefacts

- Full set under
  [`specs/005-d2net-pglite-bridge/`](specs/005-d2net-pglite-bridge/): spec.md
  with 5 clarifications resolved, plan.md, research.md (10 R-decisions),
  data-model.md, contracts/ (4 files: db-schema.sql, settings-schema.json,
  cli-contract.md, pgbridge-contract.md), quickstart.md, tasks.md (with
  in-flight remediations from `/speckit-analyse`), checklists/.

## [v2026.04.30-3] — 2026-04-30

### Documentation

- **PGLite + pg-gateway + ODBC root-cause analysis.** Documents the
  deep-dive that followed the 002-d2net-init SQLite pivot. Identifies
  PGLite's implicit-`Sync`-on-`execProtocolRaw` behaviour and the
  response-stream corruption in `pg-gateway` 0.3.0-beta.4 as the joint
  root cause of the Npgsql `ReadyForQuery while expecting
  BindCompleteMessage` and the psqlODBC `STATUS_STACK_BUFFER_OVERRUN`
  failures. Ships a working hand-rolled minimal Postgres-wire bridge
  (`docs/research/pgbridge-reference/bridge-direct.mjs`, ~150 lines) as
  a reference artefact: any future feature that wants to revive PGLite
  should start from this rather than re-introducing pg-gateway. See
  [`docs/research/pglite-pg-gateway-odbc-failure-analysis.md`](docs/research/pglite-pg-gateway-odbc-failure-analysis.md).
- No behavioural change to any shipped code path.

## [v2026.04.30-2] — 2026-04-30

### Added

- **`D2NET.Init`** — companion CLI to `D2NET.Scaffold` under
  `tools/d2net/src/D2Net.Init`. Creates a hidden `.D2NET` workspace at
  the repo root (CWD is the repo root; no walk-up to find `.git`),
  writes `D2NET-Settings.json`, and populates an embedded single-user
  SQLite database at `.D2NET/pgdb/workspace.sqlite` with five tables:
  `setting`, `excluded_directories`, `dart_files`, `phase_sequence`,
  `phase_status`. Inspection options `--list`, `--Exclusions`,
  `--current-phase` (each with TSV plain-text default and a stable
  `--json` schema). Force-delete re-init via `--FORCE
  --DELETE-EXISTING` using a temp-stage + atomic-rename pattern.
- 70 new xUnit integration tests in `tools/d2net/tests/D2Net.Init.Tests`
  — all green; `D2Net.Scaffold.Tests` (34 tests) unaffected.
- Full speckit artefact set under
  [`specs/002-d2net-init/`](specs/002-d2net-init/) — spec (with six
  recorded clarifications including the Q6 SQLite pivot), plan,
  research, data-model, contracts, tasks, quickstart, and requirements
  checklist.

### Changed

- The original spec called for PGLite (WASM Postgres) accessed via a
  Node.js bridge using `pg-gateway` and reached from .NET via psqlODBC.
  That stack proved fundamentally fragile in implementation; the Q6
  clarification pivots the storage engine to embedded SQLite. The
  five-table schema is identical in shape — only PostgreSQL-specific
  types translated to SQLite equivalents (`BIGSERIAL` → `INTEGER
  PRIMARY KEY AUTOINCREMENT`, `TIMESTAMPTZ` → ISO-8601 `TEXT`).

## [v2026.04.30] — 2026-04-30

### Added

- **`D2NET.Scaffold` MVP toolkit** — copies the `glp_runtime` Dart tree
  into `glp_runtime_net`, preserving every `.dart` file as
  `<name>.dart.src`, generating nine companion stubs (`.cs`, `.ana`,
  `.tst`, `.con`, `.dep`, `.cgn`, `.iss`, `.sta`, `.ver`) per Dart
  file, and writing a `d2net-tracker.json` JSON inventory at the target
  root. Pre-flight collision detection; `--refresh` mode that updates
  source-derived files while preserving in-progress companion edits and
  the tracker. 34 xUnit tests.
- Speckit workflow scaffolding — `.specify/`, `specs/001-d2net-scaffold/`,
  hooks, integrations.
- CalVer + branching conventions — [`docs/VERSIONING.md`](docs/VERSIONING.md),
  [`docs/BRANCHING.md`](docs/BRANCHING.md). Cloned from the sibling GLP
  repository.
