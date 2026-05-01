# Feature Specification: D2NET.Init — Non-Destructive Incremental Exclusion Updates

**Feature Branch**: `007-incremental-exclusions`
**Created**: 2026-05-01
**Status**: Draft
**Input**: User description: "Amend the d2net-init CLI tool to support non-destructive incremental addition of directory exclusions to an EXISTING D2NET workspace, so the /D2NET-init skill can drive interactive 'review and approve in batches' flows without forcing a --FORCE --DELETE-EXISTING rebuild. Adding excluded folders should also check and remove .dart file targets that fall under those folders from the dart-file target list. Pre-conditions: workspace must exist; paths must resolve under source root; reject otherwise. Atomic, idempotent, distinct exit codes for workspace-missing / path-outside-source / settings-write-failed / db-update-failed. Do not touch phase_sequence or phase_status. Out of scope: removing exclusions, file-level exclusions, changes to init-mode --exclude. Acceptance: documented in --help; removes exactly N rows for a directory containing N indexed dart files; second run is a no-op; mid-run failure leaves workspace bit-identical; downstream phase state untouched; --list / --Exclusions / --current-phase reflect the new state immediately."

## Clarifications

### Session 2026-05-01

- Q: Should the new mode be invoked via a repeatable `--add-exclude <path>` flag, an `add-exclude <path>...` subcommand, or both? → A: Repeatable flag form (`d2net-init --add-exclude <path> [--add-exclude <path> ...]`). Confirmed: matches the existing init-mode `--exclude` shape, keeps `d2net-init` flat with no subcommand grammar, and preserves the parser model already used by inspection flags.
- Q: When two `d2net-init --add-exclude` invocations race for the workspace lock, should the loser wait, fail with a contention error, or wait indefinitely? → A: Fail fast with a distinct contention exit code. Matches the per-invocation PGLite bridge model (feature 005) — the second bridge subprocess cannot open the locked data dir, so the natural mode is fail-fast. Avoids indefinite CLI hangs and gives the calling skill a clean retry-or-bail signal.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Add a directory to the exclusion list of an existing workspace (Priority: P1)

A developer running the D2NET toolkit on a Dart codebase has already initialised the workspace (`.D2NET/` exists, `dart_files` is populated, and downstream phase work has begun). They identify one or more directories that should not have been indexed — for example archived test folders or documentation trees that were missed by the initial suggested-exclusions pass. They invoke `d2net-init` in a new "add-exclude" mode, naming those directories. The tool records the new exclusions in `D2NET-Settings.json`, removes every `dart_files` row whose path falls under any newly excluded directory, and leaves the rest of the workspace — including the `phase_sequence` and `phase_status` tables that downstream phases have already been writing to — completely untouched. The developer can resume the in-flight phase work without losing any progress.

**Why this priority**: This is the entire MVP. Without it, the only way to fix an over-broad indexing pass is to wipe `.D2NET/` and rerun init from scratch, which destroys every downstream phase row and forces a full re-scan. That is unacceptable mid-workflow. Every other story in this feature builds on this single capability.

**Independent Test**: From a workspace that was initialised with `glp_runtime` as the source and contains 200 rows in `dart_files`, including 11 rows whose `full_path` starts with `glp_runtime/test_archive/`, invoke `d2net-init --add-exclude test_archive`. Verify (a) the command exits 0; (b) `D2NET-Settings.json` now lists `test_archive` as an excluded directory; (c) `dart_files` contains exactly 189 rows, none of which start with `glp_runtime/test_archive/`; (d) `phase_sequence` and `phase_status` are byte-identical to their pre-run state; (e) `--Exclusions` reports the new directory and `--list` no longer enumerates the removed files.

**Acceptance Scenarios**:

1. **Given** an existing workspace with source `glp_runtime` and 200 indexed `.dart` files (11 of them under `test_archive/`), **When** the developer runs `d2net-init --add-exclude test_archive`, **Then** the command exits 0; `D2NET-Settings.json` lists `test_archive` in the exclusion array; `dart_files` shrinks from 200 to 189 rows; no row whose path begins with `test_archive/` remains; the contents of `phase_sequence` and `phase_status` are unchanged.
2. **Given** an existing workspace, **When** the developer runs `d2net-init --add-exclude glp --add-exclude docs --add-exclude test/programs`, **Then** all three directories are added to the exclusion list in a single invocation, and every `dart_files` row whose path falls under any of them is removed in one transaction.
3. **Given** an existing workspace where the `bin/` directory is already excluded, **When** the developer runs `d2net-init --add-exclude bin/archive`, **Then** the command reports the path as redundant (because `bin/archive` lies under the already-excluded `bin/`), exits 0, and makes no changes to `D2NET-Settings.json` or `dart_files`.
4. **Given** an existing workspace, **When** the developer runs `d2net-init --add-exclude does_not_exist_yet/`, **Then** the command accepts the directory, records it in the exclusion list, removes zero rows from `dart_files` (because no such rows existed), and exits 0 — exclusion entries are forward-looking metadata, not a filesystem assertion.

---

### User Story 2 - Drive incremental exclusions from the /D2NET-init skill (Priority: P2)

The /D2NET-init skill conducts an interactive "review and approve in small batches" survey with the developer: it scans the source tree, presents 5 candidate directories at a time with notes and recommendations, and collects the developer's approvals one batch at a time. After each batch is approved, the skill invokes `d2net-init` with the corresponding `--add-exclude` flags so the workspace reflects the latest decisions before the next batch is presented. At no point does the skill need to invoke the destructive `--FORCE --DELETE-EXISTING` rebuild path, and the developer's downstream phase work is never disturbed.

**Why this priority**: The single biggest pain in the existing skill is that the only way to add an exclusion is a destructive rebuild. With this story complete, the interactive multi-batch flow becomes a first-class workflow rather than a workaround. The skill contract amendment is a separate task — this story only requires that the binary expose the capability.

**Independent Test**: Initialise a workspace, then invoke `d2net-init --add-exclude` three times in succession with different sets of paths (simulating three approved batches). Verify that the exclusion list grows monotonically, no exclusion is lost between batches, the cumulative `dart_files` reduction equals the sum of per-batch reductions, and the exit code is 0 on every invocation. Then invoke `d2net-init --current-phase` and confirm the row that was current before the three batches is still current and unchanged.

**Acceptance Scenarios**:

1. **Given** a workspace at indexing-complete state with no phases running, **When** three successive `d2net-init --add-exclude ...` invocations apply batches of 5, 5, and 4 directories respectively, **Then** after the third invocation the exclusion list contains the union of all 14 directories, every `dart_files` row whose path falls under any of them has been removed, and no exclusion from any batch has been silently dropped.
2. **Given** a workspace where downstream phase work has set `phase_status` for phases `analyze` and `port` to `IN_PROGRESS`, **When** the developer applies one or more `--add-exclude` invocations, **Then** the `phase_status` rows for `analyze` and `port` retain their `IN_PROGRESS` status and `last_updated` timestamps unchanged.

---

### User Story 3 - Inspect, diagnose, and recover from misuse (Priority: P3)

A developer or skill invokes `d2net-init --add-exclude` against a directory that does not exist yet, against a path that escapes the configured source root, or in a directory where no `.D2NET/` workspace has been initialised. In each case, the developer needs an unambiguous, machine-readable signal of what went wrong so that scripts and skills can branch on the failure. Each error condition produces a distinct exit code and a stderr message that names the offending path. After a failure, the workspace is left exactly as it was before the invocation.

**Why this priority**: Invocation-time validation catches misuse early. Distinct exit codes let the /D2NET-init skill (and any future automation) react intelligently — for example, by suggesting `d2net-init` (without `--add-exclude`) when no workspace exists, or by surfacing the offending path verbatim when validation fails. Without this, all failures collapse to a generic non-zero exit.

**Independent Test**: From a directory with no `.D2NET/`, invoke `d2net-init --add-exclude foo` and confirm a workspace-missing exit code distinct from any other exit code in the binary's existing error catalogue. From an initialised workspace with source `glp_runtime`, invoke `d2net-init --add-exclude ../outside/foo` and `d2net-init --add-exclude /etc` and confirm both produce the path-outside-source exit code with a stderr message naming the rejected path. After every failure, confirm `D2NET-Settings.json` and the workspace database are byte-identical to their pre-invocation state.

**Acceptance Scenarios**:

1. **Given** the current directory has no `.D2NET/` subfolder, **When** the developer runs `d2net-init --add-exclude foo`, **Then** the command exits with the workspace-missing code, prints a stderr message instructing the developer to run init first, and creates no files.
2. **Given** an existing workspace with source `glp_runtime`, **When** the developer runs `d2net-init --add-exclude ../somewhere_else`, **Then** the command exits with the path-outside-source code, names the rejected path in stderr, and makes no changes.
3. **Given** an existing workspace, **When** the developer supplies three `--add-exclude` paths and the second one escapes the source root, **Then** the command rejects the entire invocation (all-or-nothing), exits with the path-outside-source code, names the offending path in stderr, and leaves all three paths un-applied.

---

### Edge Cases

- **Empty argument set**: `d2net-init --add-exclude` with no path argument must be treated as a usage error (existing argument-parsing behaviour applies).
- **Duplicate paths in one invocation**: `d2net-init --add-exclude foo --add-exclude foo` collapses to a single exclusion; the second instance is reported as redundant within the same run.
- **Path is the source root itself**: excluding the configured source directory (e.g. `glp_runtime/`) effectively empties `dart_files`. This is permitted but reported in the summary as removing all rows; behaviour must remain correct rather than producing a special-case error.
- **Path is a file, not a directory**: must be rejected with a distinct stderr message. Existing exclusion semantics are directory-only and this story does not relax that invariant.
- **Path with mixed separators or trailing slashes**: must be canonicalised internally so that `bin`, `bin/`, and `bin\` all refer to the same exclusion; user-supplied form is preserved in stderr messages but storage is normalised.
- **Concurrent invocation**: if two `d2net-init --add-exclude` invocations race against the same workspace, the workspace database file lock and the settings-file rename pattern must serialise them so that no partial state is observable. The losing invocation MUST fail fast with a distinct contention exit code (no waiting, no timeout). The caller is responsible for retrying. Partial application is not permitted.
- **Mid-write failure**: there are two phases.
  - *Pre-COMMIT*: a process kill or storage error before the database transaction commits MUST leave the workspace bit-identical to its pre-run state. The temp settings file is deleted in `finally`. No recovery action required.
  - *Post-COMMIT, pre-rename*: a rare narrow window. If the database commits but the atomic settings-file rename then fails (e.g. the temp file is unwritable, the parent directory is missing), the database carries the new exclusions while the on-disk JSON is stale. The binary MUST exit with code 13 (`AddExcludeSettingsWriteFailed`) and a stderr message that the operator can re-run the same invocation to resync. On the re-run, the database insert is idempotent (no rows added) and the JSON rewrite retries. This window is documented and recoverable; it is the only deviation from "bit-identical to pre-run state" permitted by FR-007.
- **`dart_files` row whose `full_path` is exactly the excluded directory string**: cannot occur in practice (rows are always file paths, exclusions are always directories), but the SQL pattern used must guard against accidentally interpreting `bin` as matching `binary.dart`.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `d2net-init` MUST expose a new invocation mode that accepts one or more directory paths to add to an existing workspace's exclusion list, repeatable in a single invocation.
- **FR-002**: The new mode MUST require an existing `.D2NET/` workspace at the current working directory and MUST refuse to auto-initialise one. Absence of a workspace MUST produce a distinct, documented exit code and a stderr message instructing the user to run init first.
- **FR-003**: Every supplied path MUST resolve to a location inside the source root recorded in `D2NET-Settings.json`. Any path that escapes the source root MUST cause the entire invocation to be rejected with a distinct, documented exit code and a stderr message that names the offending path.
- **FR-004**: For every accepted exclusion path that is not already covered by an existing exclusion, the new mode MUST add the path to the workspace's exclusion list as persisted in `D2NET-Settings.json`.
- **FR-005**: For every accepted exclusion path, the new mode MUST remove every row from the `dart_files` table whose path lies under that directory. Removal MUST use a path-prefix match that respects directory boundaries (so that excluding `bin` does not match `binary.dart`).
- **FR-006**: The new mode MUST NOT modify `phase_sequence` or `phase_status`. The `last_updated` column on every existing `phase_status` row MUST remain unchanged.
- **FR-007**: The `excluded_directories` insert and the `dart_files` deletions MUST commit as a single all-or-nothing database transaction. On any error before COMMIT, the workspace MUST be left bit-identical to its pre-run state. The settings-file (`D2NET-Settings.json`) update happens via write-temp-then-rename and is **sequenced after** the database COMMIT so that the database (the source of truth) is consistent before its JSON projection. There is one documented narrow window: if the rename of the temp settings file fails *after* a successful COMMIT, the database is updated but the JSON file is stale. In that case the binary MUST exit with the documented `AddExcludeSettingsWriteFailed` code (13) and a stderr message advising the operator to re-run the same invocation; on that re-run, the database insert is a no-op (idempotent per FR-008) and the JSON rewrite retries. Partial application within the transaction is not permitted.
- **FR-008**: Re-supplying a path that is already present in the exclusion list (or is a sub-path of an already-excluded ancestor) MUST be a no-op for that path and MUST NOT cause the invocation to fail. Such paths MUST be reported in the run summary as redundant rather than silently dropped.
- **FR-009**: The new mode MUST print a concise human-readable summary on success: number of new exclusions added, number of redundant or already-present exclusions skipped (named individually), and number of `dart_files` rows removed grouped by the new exclusion that caused the removal.
- **FR-010**: The new mode MUST accept a `--json` flag that switches the success summary to a stable structured JSON document, consistent in style with the existing `--list`, `--Exclusions`, and `--current-phase` JSON outputs.
- **FR-011**: The new mode MUST use distinct, documented, non-zero exit codes for at least the following failure conditions: workspace-missing, path-outside-source, settings-write-failed, database-update-failed, and workspace-lock-contention. None of these codes MAY collide with the existing `WorkspaceAlreadyExists` (3) code, because the new mode requires the workspace to exist. The workspace-lock-contention code is emitted when another process holds the workspace database lock at the moment this invocation attempts to acquire it; the binary MUST fail fast (no waiting) so the caller can retry or bail.
- **FR-012**: The new mode MUST be documented in the binary's `--help` output, alongside the existing inspection and init-mode flags.
- **FR-013**: After the new mode returns success, the existing `--list`, `--Exclusions`, and `--current-phase` inspection commands MUST report the post-update state immediately, with no caching or staleness window.
- **FR-014**: The new mode MUST NOT alter the behaviour of init-mode `--exclude`. The init-time and incremental-time exclusion mechanisms are independent code paths sharing only the persisted exclusion list as their common state.
- **FR-015**: The new mode MUST NOT support file-level paths (e.g. paths ending in `.dart`, `.zip`, `.exe`). Existing exclusion semantics are directory-only and this feature preserves that invariant. Supplying a file path MUST be rejected with a clear stderr message.
- **FR-016**: When the supplied path is logically equal to an entry already in the exclusion list (after canonicalisation of trailing separators and path-separator characters), the new mode MUST treat the two as the same exclusion and report the supplied path as already-present.

### Key Entities *(include if feature involves data)*

- **Exclusion entry**: a directory path, expressed relative to the source root, that the workspace database will exclude from indexing. Stored as one row in the workspace settings (specifically the excluded-directories collection of `D2NET-Settings.json`, which the existing `setting`/`excluded_directories` representation backs in the workspace database).
- **Dart file target row**: a row in the `dart_files` table representing one indexed `.dart` source file with an auto-generated id, the bare filename, and the file's full path expressed with forward slashes relative to the repo root. The new mode deletes rows whose `full_path` lies under any newly added exclusion.
- **Phase row**: a row in `phase_sequence` (phase, integer sequence) or `phase_status` (phase, status, last_updated). The new mode never reads, writes, or deletes phase rows.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Adding one or more directory exclusions to a workspace whose `dart_files` table holds up to 10,000 rows completes in under 2 seconds on a developer workstation, measured from process start to process exit.
- **SC-002**: After adding a directory exclusion that covers exactly N pre-existing `dart_files` rows, the table contains exactly the original count minus N rows, with zero false-positive deletions and zero false-negative retentions, in every test run.
- **SC-003**: For every successful or failed invocation of the new mode, `phase_sequence` and `phase_status` rows are byte-identical to their pre-invocation state in 100 % of runs.
- **SC-004**: A second invocation with the same arguments as a successful first invocation results in zero new exclusions added, zero `dart_files` rows removed, and an exit code of 0 in 100 % of runs.
- **SC-005**: A simulated process kill or storage error mid-run (for example, between the settings-file rename and the database commit) leaves `D2NET-Settings.json` and the workspace database in their exact pre-invocation state in 100 % of induced-failure tests.
- **SC-006**: Each of the five documented error conditions (workspace-missing, path-outside-source, settings-write-failed, database-update-failed, workspace-lock-contention) produces a distinct, documented exit code in 100 % of triggering runs, and the offending path (where applicable) appears verbatim in the stderr message in 100 % of path-rejection runs.
- **SC-007**: A developer who has already invested phase work in a workspace can complete an end-to-end "review and approve in 5-item batches" exclusion update for ten batches without losing any phase progress in any batch, measured by `phase_status` row equality before the first batch and after the tenth.

## Assumptions

- **CLI shape**: the new mode is invoked via a repeatable `--add-exclude <path>` flag, mirroring the existing `--exclude` flag used by init mode. Confirmed in clarification session 2026-05-01. A subcommand-style invocation (`d2net-init add-exclude <path>...`) was considered and rejected because it would introduce subcommand grammar to a binary that is otherwise flat (only flag-style arguments).
- **Atomicity scope**: a single invocation is one transaction across all supplied paths. If any path is invalid or any storage write fails, none of the supplied paths take effect. Per-path commits (where some paths apply and others don't) were considered and rejected because they would violate the all-or-nothing guarantee that downstream skills depend on.
- **Path canonicalisation rule**: trailing slashes and path-separator characters (`/` vs `\`) are normalised internally, but the user-supplied form is preserved verbatim in stderr messages. A path that lies under an already-excluded ancestor is reported as redundant and skipped (not silently dropped). The deeper redundancy-detection rule — for example whether the new mode should also detect that a previously approved sibling exclusion is now subsumed by a broader one supplied in this invocation — is deferred to plan.
- **Workspace storage engine**: the underlying database is single-user PGLite (WASM-backed Postgres) at `.D2NET/pgdb/`, accessed via the per-invocation Node.js subprocess `bridge-direct.mjs` established by feature 005 (D2NET PGLite bridge). All database mutations and reads happen over the Postgres wire protocol via Npgsql against the bridge's local TCP port. The legacy SQLite layout from feature 002 is detected by `WorkspaceLayout.LegacySqliteFileName` (`workspace.sqlite`) and is out of scope for this feature.
- **`dart_files` schema**: the table has columns `id` (BIGSERIAL primary key), `filename` (bare, NOT NULL), and `full_path` (forward-slash, repo-root-relative, UNIQUE) per the canonical schema in `tools/d2net/src/D2Net.Init/Schema/db-schema.sql`. The new mode's deletion query relies on a path-prefix match against `full_path` with explicit directory-boundary handling (so `bin` does not match `binary.dart`).
- **`excluded_directories` schema**: the table has columns `path` (TEXT primary key, source-relative forward-slash) and `kind` (TEXT, one of `'tool'`, `'pattern'`, `'manual'` per the existing CHECK constraint). The new mode inserts new rows with `kind = 'manual'` (consistent with the existing `--exclude` flag's manual-exclusion semantics).
- **Source-root invariance**: this feature does not modify the configured source directory, target directory, or target extension. Only the excluded-directories list and the `dart_files` table are mutated.
- **No config-file editing by the skill**: the /D2NET-init skill remains invocation-only and never edits `D2NET-Settings.json` directly. All settings mutations continue to happen through the binary, which now exposes incremental exclusion as a first-class operation rather than only via destructive rebuild.
- **Out of scope for this feature**: `--remove-exclude` (removal of an exclusion); file-level exclusions (`.zip`, `.dart`, `.DS_Store`); changes to init-mode `--exclude` behaviour; any change to the /D2NET-init skill contract (which is a separate feature track).
