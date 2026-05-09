# Changelog

All notable changes to GLPNET. Versions follow the CalVer convention defined in
[`docs/VERSIONING.md`](docs/VERSIONING.md): tags are `vYYYY.MM.DD[-N]` where the
optional `-N` suffix increments per same-day release.

## [v2026.05.09] — 2026-05-09

### Added

- **`prereq-patterns/` catalog**. New top-level peer of `specs/`, `docs/`,
  `programs/`, `glp_runtime/`, `glp_multiagent/`, `test/`, holding curated
  prerequisite implementations any future glpnet feature can adopt without
  re-deriving the design. Lands three governance files (`directory.md`,
  `howto.md`, `policies.md`) plus eight pattern sub-directories — `pglite`
  (active), `dbos`, `flask-sqlalchemy-alembic-api`, `pglite-backup-restore`,
  `blazor-spa-bg-api`, `background-task-manager`, `local-secrets-store`,
  `secure-signatures` (all `draft`) — each with its required
  `description.md`, `applicability.md`, `sources.md`. `policies.md` carries
  Policy 1 (no cleartext auth tokens; secret-material hashes restricted to
  `{Argon2id, scrypt, bcrypt}`) and Policy 2 (operational data routes to
  `D:/BSTDEV/research/glpnet-datalake/<pattern-or-app>/<data-class>/<partition>.parquet`).

- **Merged pglite bridge** at `prereq-patterns/pglite/pglite_bridge.mjs`.
  Single canonical implementation consolidating glpnet's no-pg-gateway
  hand-rolled wire-protocol bridge (Npgsql / psqlODBC compatible; two
  diagnosed bug fixes — PGLite implicit-Sync after `execProtocolRaw`;
  pg-gateway 0.3.0-beta.4 response-corruption avoidance) with AIGRID's
  `globalWorkChain` global FIFO, per-connection `workChain`,
  `endsAtFlushBoundary()`, synthetic-`ROLLBACK` startup handshake, Windows
  `DETACHED_PROCESS` lifecycle (via the cited Python sidecar), `sidecar.json`
  discovery, and `@electric-sql/pglite@0.2.17` pin (sibling
  `package.json`).  `COPY ... FROM STDIN` interception is dropped with
  rationale — PGLite WASM does not implement COPY-IN over the wire.

- **Format contracts** at `specs/011-prereq-patterns-catalog/contracts/`. Six
  format contracts copied verbatim from AIGRID
  (`@004a-opskit-sidecar-autospawn`, SHA `83b60585...`) and scrubbed of
  AIGRID-only references per FR-011: `description_md_format.md`,
  `applicability_md_format.md`, `sources_md_format.md`, `directory_md_format.md`,
  `howto_md_format.md`, `policies_md_format.md`.

- **Pglite merge analysis** at
  `specs/011-prereq-patterns-catalog/pglite-merge-analysis.md`. Classifies
  every distinguishing feature of both pre-merge bridges (16 from glpnet
  `bridge-direct.mjs`, 18 from AIGRID `pglite_bridge.mjs`) as
  `present-in-merged` / `superseded-with-rationale` / `dropped-with-rationale`.
  Zero unclassified.

- **Conformance script** at
  `specs/011-prereq-patterns-catalog/conformance-check.ps1`. Pure PowerShell,
  no third-party dependency. Implements C1 (three-files-per-pattern), C2
  (lifecycle agreement), C3 (catalog self-containment), C4 (no live AIGRID
  cross-references), C5 (format-contract reachability), C6 (migration-analysis
  completeness). Final pre-merge gate: PASS on all six checks.

- **`docs/research/pgbridge-reference/MIGRATED.md`** — forwarding note from
  the archival pre-merge investigation directory to the canonical merged
  bridge under `prereq-patterns/pglite/`.

### Validated

- **Catalog conformance gate**. `conformance-check.ps1` ran from the repo
  root with exit code `0`: 109 internal markdown links resolve inside glpnet,
  75 grep hits for `breenlake|aigrid|opskit` all in allowed contexts
  (`sources.md` files or "external sibling" footnote in `policies.md`), 34
  classification rows across 2 tables in `pglite-merge-analysis.md` all
  with valid classifications and non-empty rationales, and "Unclassified:
  0" assertion present.

### Deferred

- **SC-003 (Npgsql / psqlODBC connectivity, 100 sequential cycles)** and
  **SC-004 (psycopg-style concurrent-pipeline invariant)**. Buildable success
  criteria intentionally NOT verified by this catalog-import feature —
  documented verbatim in `prereq-patterns/pglite/sources.md` (Flow D1 / D2)
  for the first glpnet feature that *adopts* the merged bridge to run as part
  of its own work.

### References

- Spec: `specs/011-prereq-patterns-catalog/spec.md`
- Plan: `specs/011-prereq-patterns-catalog/plan.md`
- Tasks: `specs/011-prereq-patterns-catalog/tasks.md`
- Handover: `specs/011-prereq-patterns-catalog/handover.md`

## [v2026.05.02] — 2026-05-02

### Validated

- **`/D2NET-scaffold` in-session smoke walks**. Rows 1, 2, 3, 4, 5, 8 + the
  T013 idempotent re-run from `specs/010-scaffold-skill/validation.md` executed
  in-session against the binary at `tools/d2net/src/D2Net.Scaffold/bin/Release/
  net8.0/d2net-scaffold.exe` (version `0.2.0+a89bed71`) and the
  `glp_runtime → glp_runtime_net (_net)` workspace. All seven walks PASS:
  `--help`, `--version`, default scaffold (empty input), `--json` (verbatim,
  recap suppressed), `--json --bridge-port 55001` (pass-through, recap
  suppressed), `please scaffold quickly` (FR-010a → `--help`), and the
  reconciliation-block check (`added_paths: 0, removed_paths: 0`). The
  remaining 9 rows (T012, T012a, T014, T018–T022, T029) require an
  operator-driven session — fresh repo, deleted binary, destructive
  `yes/no` confirmations, or fresh-Claude-Code-session discoverability —
  and stay PENDING in `validation.md`.

### Fixed

- **T013 misstatement** in `specs/010-scaffold-skill/tasks.md` and
  `validation.md`. The task previously expected the recap to show
  `0 files copied; 0 working directories created; 0 dart_files rows updated`
  on idempotent re-run. The binary's `files_copied / workdirs_created /
  dart_files_updated` fields are per-run write totals (always equal to the
  full source-tree count on a successful scaffold), not net deltas — only
  the reconciliation block (`added_paths / removed_paths`) carries the net
  change. The corrected expectation references spec 009 User Story 2
  Acceptance Scenario 3 ("zero net additions and zero net removals") and
  the reconciliation summary's `0 added paths; 0 removed paths`.

## [v2026.05.01] — 2026-05-01

### Added

- **`/D2NET-scaffold` Claude Code skill.** Wraps the spec-009 `d2net-scaffold`
  CLI as a slash command, sibling to `/D2NET-init`. Empty input
  (`/D2NET-scaffold`) runs the scaffold operation in default mode; the binary
  takes no positional arguments — its inputs are the workspace populated by an
  earlier `/D2NET-init`. Supports raw flag pass-through (`--json`,
  `--bridge-port <N>`, `--FORCE --DELETE-TARGET`) and natural-language markers
  (`as json` / `in json` / `structured` → `--json`; `bridge port N` /
  `bridge-port=N` → `--bridge-port N`; the closed destructive-marker word list
  `force` / `delete` / `rebuild` / `reset` / `recreate` / `reinitialise` /
  `reinitialize` / `nuke` / `wipe` / `redo` triggers the destructive gate).
  Help / version verbs (`help` / `--help` / `-h` / `version` / `--version`)
  short-circuit. Unrecognized non-empty input routes to `--help` (FR-010a).
  Auto-builds the binary on user confirmation when missing or stale.
- **Two-confirmation destructive safety flow.** Destructive invocations
  (`force delete target` or the literal `--FORCE --DELETE-TARGET` pair) require
  both (a) a skill-layer confirmation prompt naming the absolute target path,
  and (b) the binary's own interactive prompt — driven by piping `yes\n` to the
  binary's stdin only after the skill-layer confirmation has resolved
  affirmatively. The cache key is the **target directory's absolute path**
  (clarified Q2), parsed from `<cwd>/.D2NET/D2NET-Settings.json`'s `target`
  field. Already-confirmed paths skip the skill-layer prompt within the same
  conversation but ALWAYS still drive the binary's prompt (the binary
  re-prompts every invocation by design — spec 009 FR-012a hard safety gate).
  Unbalanced flag pair (only one of `--FORCE` / `--DELETE-TARGET` supplied) is
  passed through to the binary's `ArgParser` for exit 1 with the
  argument-error hint (FR-016).
- **Output handling.** JSON outputs (`--json` in resolved flag set) are
  surfaced verbatim regardless of size and the Claude-side recap is
  **suppressed entirely** (clarified Q1) so downstream tooling (`jq`, smoke
  tests) consumes the response cleanly. Plain-text outputs over 50 lines are
  truncated with the standard "show all / filter <substring>" footer; recap
  appended on success: `Target at <path>; <N> files copied; <M> working
  directories created; <K> dart_files rows updated; <T>s wall-clock.`
- **Exit-code hints.** 22 (`ScaffoldWorkspaceMissing` → "Run /D2NET-init
  first"), 23 (`ScaffoldSourceMissing`), 24 (`ScaffoldTargetNotEmptyAndNotManaged`
  → suggest `/D2NET-scaffold force delete target`), 25 (`ScaffoldWorkdirCollision`),
  26 (`ScaffoldCopyError` — idempotency note), 27 (`ScaffoldDbWriteFailed`),
  28 (`ScaffoldWorkspaceLocked`), 29 (`ScaffoldOperatorCancelledTargetDeletion`),
  1 (`ArgumentError`).
- **Casing requirement.** The skill directory and frontmatter `name` are
  exactly `D2NET-scaffold` (uppercase `D2NET`, lowercase `scaffold`). Matches
  the casing precedent of `/D2NET-init`.
- Spec under [`specs/010-scaffold-skill/`](specs/010-scaffold-skill/):
  spec.md (5 clarifications resolved — JSON suppresses recap; cache key =
  target absolute path; show-all/filter via conversation context; empty
  input = run scaffold; unrecognized non-empty = run `--help`), plan.md,
  research.md (11 R-decisions covering all spec-time deferrals), data-model.md,
  contracts/skill-contract.md, quickstart.md, tasks.md, validation.md (smoke
  walkthrough seed; PENDING rows filled at operator-driven validation time).

### Notes

- The skill is purely additive — no changes to `tools/d2net/` or any existing
  test. The shipped D2Net.Init and D2Net.Scaffold test suites continue to pass
  unchanged.
- Bridge-port auto-retry from `/D2NET-init` (3-attempt walk-forward ladder) is
  **deliberately not** implemented for `/D2NET-scaffold`. Scaffold's exit-code
  catalogue does not include a dedicated `BridgePortInUse` code; collisions
  surface as exit 27 / 28 depending on which subsystem fails first. Auto-retry
  across these would be a guess rather than a precise recovery; operators
  diagnose root cause manually (research.md R8).

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
