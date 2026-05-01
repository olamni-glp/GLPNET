# Feature Specification: D2NET.Init — Non-Destructive Exclusion Removal (`--remove-exclude`)

**Feature Branch**: `008-remove-exclude`
**Created**: 2026-05-01
**Status**: Draft
**Input**: User description: "Amend the d2net-init CLI tool with an inverse to --add-exclude: a non-destructive --remove-exclude mode that removes one or more directories from an EXISTING D2NET workspace's exclusion list and re-indexes any .dart files that fall under those directories. This closes the gap exposed by feature 007 (incremental-exclusions): exclusions could be added incrementally but not removed without a destructive --FORCE --DELETE-EXISTING rebuild. The /D2NET-init skill cannot be safely used for batch review-and-approve flows until --remove-exclude exists, because mistakes are not recoverable."

## Clarifications

### Session 2026-05-01

- Q: Should `--remove-exclude` be allowed to remove rows whose `kind` is `'tool'` or `'pattern'` (auto-detected by init's heuristics, e.g. `.git`, `.dart_tool`, `build`, `bin`, archive-marker directories)? → A: Refuse non-manual rows by default. Allow the operator to override via an explicit `--allow-system-exclusions` flag. Reasoning: removing a tool/pattern exclusion almost always re-indexes a large irrelevant tree (`.git/objects/`, `node_modules/`, `build/native_assets/`, etc.); the safe default is to protect those rows. Operators who genuinely intend to remove a system exclusion supply the explicit flag, which acts as a safety acknowledgment.
- Q: When a supplied path is NOT currently in the exclusion list, should `--remove-exclude` no-op + report it, or treat it as an error? → A: No-op + report it as `not-currently-excluded` in the run summary; exit 0. Confirmed: mirrors feature 007's redundancy semantics, lets the calling skill safely re-issue without branching on exit code, and matches the operator's intent ("ensure this path is not in the exclusion list").
- Q: When a removed path's `.dart` files remain logically covered by a surviving ancestor exclusion (e.g. removing `bin/archive` while `bin` is still excluded), what is the exit / reporting behaviour? → A: Exit 0; the run summary explicitly names the path with `covered-by-ancestor: <ancestor>`; no rows are inserted into `dart_files`. Confirmed: the operator's literal request (remove the named row) is fulfilled, and the data-model consequence is reported transparently so the operator can decide whether to also remove the ancestor.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Undo a wrongly-applied exclusion (Priority: P1)

A developer running the D2NET toolkit on a Dart codebase has already initialised the workspace and added one or more exclusions via the `--add-exclude` mode shipped in feature 007. After applying an exclusion they realise it was wrong — for example, they excluded `test/programs` thinking it held only generated fixtures, then discover it actually holds 89 critical typechecker test programs. They invoke `d2net-init` in a new "remove-exclude" mode, naming the wrongly-applied directory. The tool removes the directory from `D2NET-Settings.json` and from the workspace's `excluded_directories` table, walks the on-disk source tree under that directory, and inserts one row into `dart_files` per `.dart` file found. The rest of the workspace — including `phase_sequence` and `phase_status` — is left completely untouched. The developer can resume the in-flight phase work without losing any progress, and the wrongly-applied exclusion is fully reversed without a destructive rebuild.

**Why this priority**: This is the entire MVP. Feature 007 is asymmetric: exclusions can be added but not removed without `--FORCE --DELETE-EXISTING`, which destroys downstream phase state. Until this feature ships, the `/D2NET-init` interactive batch-review flow is unsafe — every approved batch is irrevocable, and an over-eager click forces a destructive rebuild. With this story complete, batch review becomes a fully reversible workflow.

**Independent Test**: From a workspace with source `glp_runtime` where `test/programs` was previously excluded via `--add-exclude` and contains 0 `.dart` files (i.e. the on-disk sub-tree under `test/programs` has no `.dart` files at all — say it holds `.glp` test programs only), invoke `d2net-init --remove-exclude test/programs`. Verify (a) the command exits 0; (b) `D2NET-Settings.json` no longer lists `test/programs`; (c) the `excluded_directories` table no longer contains a row for it; (d) `dart_files` row count is unchanged (zero files were re-indexed because none existed); (e) `phase_sequence` and `phase_status` are byte-identical to their pre-run state; (f) `--Exclusions` no longer reports the directory.

Then: from a workspace where `lib/old/` was previously excluded and the on-disk sub-tree contains 12 `.dart` files, invoke `d2net-init --remove-exclude lib/old`. Verify (a) exit 0; (b) the row is gone from `excluded_directories`; (c) `dart_files` grew by exactly 12 rows whose `full_path` starts with `glp_runtime/lib/old/`; (d) phase tables unchanged.

**Acceptance Scenarios**:

1. **Given** an existing workspace with source `glp_runtime` where `test/programs` is in the exclusion list and contains 0 `.dart` files on disk, **When** the developer runs `d2net-init --remove-exclude test/programs`, **Then** the command exits 0; the `excluded_directories` row for `test/programs` is removed; `dart_files` row count is unchanged; the contents of `phase_sequence` and `phase_status` are unchanged.
2. **Given** an existing workspace where `lib/legacy` is excluded and contains 12 `.dart` files on disk, **When** the developer runs `d2net-init --remove-exclude lib/legacy`, **Then** the command exits 0; the exclusion is removed; `dart_files` grows by exactly 12 rows whose `full_path` matches the on-disk files; `phase_sequence` and `phase_status` are unchanged.
3. **Given** an existing workspace, **When** the developer runs `d2net-init --remove-exclude foo --remove-exclude bar --remove-exclude baz`, **Then** all three exclusions are processed in a single transaction; if any path is invalid the entire invocation is rejected and no changes are applied.
4. **Given** an existing workspace where `test/programs` is NOT in the exclusion list, **When** the developer runs `d2net-init --remove-exclude test/programs`, **Then** the command exits 0; the run summary reports the path as `not-currently-excluded` (named explicitly); no rows are removed and no rows are inserted.

---

### User Story 2 - Drive interactive review-then-undo flows from the /D2NET-init skill (Priority: P2)

The `/D2NET-init` skill conducts an interactive "review and approve in small batches" survey with the developer (the same workflow that motivated feature 007). After applying a batch of exclusions, the developer realises one or more were wrong. The skill invokes `d2net-init` with the corresponding `--remove-exclude` flags so the workspace reverts to the desired state before the next batch is presented. The full add-then-undo round trip is non-destructive; downstream phase work is never disturbed; and the skill can offer "undo last batch" as a first-class option.

**Why this priority**: This story is the operational reason `/D2NET-init` cannot run in auto mode safely today. Without `--remove-exclude`, every accepted batch is irrevocable, and the operator cannot recover from a mistake without a destructive rebuild. With this story complete, the skill can offer a fully reversible batch-review workflow.

**Independent Test**: Initialise a workspace, run three successive `d2net-init --add-exclude ...` invocations to apply 14 directories across batches, then run `d2net-init --remove-exclude ...` to remove 5 of them in a single invocation. Verify (a) the cumulative exclusion list contains exactly 9 entries (the 14 originally added minus the 5 removed); (b) `dart_files` count equals the original count minus the dart files that remain under the 9 surviving exclusions; (c) the exit code is 0 on every invocation; (d) `phase_sequence` and `phase_status` are byte-identical to their pre-test state.

**Acceptance Scenarios**:

1. **Given** a workspace where 14 directories have been added in three earlier batches, **When** the developer runs `d2net-init --remove-exclude` with 5 of those 14 paths in a single invocation, **Then** all 5 are removed in one transaction; the cumulative exclusion list shrinks to 9; the dart files under the 5 removed paths are inserted back into `dart_files`; phase tables are unchanged.
2. **Given** a workspace where downstream phase work has set `phase_status` for phases `analyze` and `port` to `IN_PROGRESS` after some exclusions were added, **When** the developer applies one or more `--remove-exclude` invocations, **Then** the `phase_status` rows for `analyze` and `port` retain their `IN_PROGRESS` status and `last_updated` timestamps unchanged.

---

### User Story 3 - Inspect, diagnose, and recover from misuse (Priority: P3)

A developer or skill invokes `d2net-init --remove-exclude` against a path that escapes the source root, in a directory where no `.D2NET/` workspace has been initialised, or with a path that is currently excluded but covered by a surviving ancestor exclusion (so removing it would not actually re-introduce its `.dart` files). In each case, the developer needs an unambiguous, machine-readable signal so that scripts and skills can branch on the outcome. Each error condition produces a distinct exit code and a stderr message that names the offending path. The ancestor-survival case is reported in the success summary (not as an error), so the operator can tell why no rows were re-indexed.

**Why this priority**: Ergonomics. The interactive batch-review flow benefits from clear failure signals (so the skill knows whether to retry, abort, or surface to the user). Distinct codes also let scripts react intelligently — for example, treating a "not currently excluded" report as a no-op rather than a hard failure.

**Independent Test**: From a directory with no `.D2NET/`, invoke `d2net-init --remove-exclude foo` and confirm the workspace-missing exit code (6, reused from inspection and `--add-exclude`). From an initialised workspace, invoke `d2net-init --remove-exclude ../outside/foo` and confirm a distinct path-outside-source exit code with a stderr message naming the path. Construct a workspace where both `bin` and `bin/archive` are excluded, then invoke `d2net-init --remove-exclude bin/archive` and confirm exit 0, with the run summary explicitly reporting that `bin/archive` was removed but its `.dart` files remain covered by ancestor `bin`.

**Acceptance Scenarios**:

1. **Given** the current directory has no `.D2NET/` subfolder, **When** the developer runs `d2net-init --remove-exclude foo`, **Then** the command exits with code 6 (`WorkspaceMissingForInspection`), prints a stderr message instructing the developer to run init first, and creates no files.
2. **Given** an existing workspace, **When** the developer runs `d2net-init --remove-exclude ../somewhere_else`, **Then** the command exits with the path-outside-source code, names the rejected path in stderr, and makes no changes.
3. **Given** an existing workspace with both `bin` and `bin/archive` in the exclusion list, **When** the developer runs `d2net-init --remove-exclude bin/archive`, **Then** the command exits 0; the row for `bin/archive` is removed; the run summary explicitly reports `bin/archive` as `covered-by-ancestor: bin`; zero rows are inserted into `dart_files` (because they remain covered by the surviving `bin` ancestor); `phase_sequence` and `phase_status` are unchanged.
4. **Given** an existing workspace, **When** another `d2net-init` process holds the workspace lock, **Then** the developer's `--remove-exclude` invocation exits with the lock-contention code (mirroring `AddExcludeWorkspaceLocked` from feature 007) and fails fast — no waiting, no partial application.

---

### Edge Cases

- **Empty argument set**: `d2net-init --remove-exclude` with no path argument is a usage error (exit 1).
- **Duplicate paths in one invocation**: `--remove-exclude foo --remove-exclude foo` collapses to one removal; the second instance is reported as already-handled within the same run.
- **Path is a file, not a directory**: rejected with the same file-vs-directory rule as `--add-exclude` (exit code mirroring `AddExcludePathIsFile`). This case can occur if the operator passes a path that exists on disk as a file.
- **Path with mixed separators or trailing slashes**: canonicalised internally so that `bin`, `bin/`, and `bin\` all refer to the same exclusion entry, mirroring `--add-exclude`'s canonicalisation rule.
- **Removing a path that, on disk, contains symlinks or junctions**: the directory walk follows the same rules as init's source-tree walk (do not follow symlinks across roots; treat junctions as ordinary directories on Windows). This preserves consistency between the dart_files inserted by remove-exclude and those originally inserted by init.
- **Mid-write failure (pre-COMMIT)**: a process kill or storage error before the database transaction commits leaves the workspace bit-identical to its pre-run state. Inserted dart_files rows roll back; the temp settings file is deleted in `finally`.
- **Mid-write failure (post-COMMIT, pre-rename)**: the same narrow window as `--add-exclude`. The database is updated but the on-disk JSON is stale. The binary exits with the documented `RemoveExcludeSettingsWriteFailed` code and a stderr message advising the operator to re-run the same command, which is idempotent.
- **A `.dart` file that already exists in `dart_files` (drift case)**: the insert uses `ON CONFLICT (full_path) DO NOTHING`, so a path that was somehow not removed during the original add (a drift artefact) does not produce a primary-key violation. The summary's "rows inserted" counter reflects only rows that were actually new.
- **Concurrent invocation**: same fail-fast semantics as feature 007 — the loser exits with the lock-contention code, no waiting.
- **System-kind exclusion attempted without override**: if any supplied path is currently excluded with `kind='tool'` or `kind='pattern'`, and `--allow-system-exclusions` was not supplied, the entire invocation is rejected with the dedicated `system-exclusion-refused` exit code. Stderr names every offending path AND its `kind` so the operator can decide whether to omit those paths from the next invocation or to re-issue with `--allow-system-exclusions`. No partial application.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `d2net-init` MUST expose a new invocation mode that accepts one or more directory paths to remove from an existing workspace's exclusion list, repeatable in a single invocation.
- **FR-002**: The new mode MUST require an existing `.D2NET/` workspace at the current working directory and MUST refuse to auto-initialise one. Absence of a workspace MUST exit with the existing code 6 (`WorkspaceMissingForInspection`) and a stderr message instructing the user to run init first.
- **FR-003**: Every supplied path MUST resolve to a location inside the source root recorded in `D2NET-Settings.json`. Any path that escapes the source root MUST cause the entire invocation to be rejected (all-or-nothing) with a distinct, documented exit code mirroring `--add-exclude`'s `AddExcludePathOutsideSource`.
- **FR-004**: For every accepted path that is currently in the workspace's exclusion list AND has `kind = 'manual'`, the new mode MUST remove the corresponding row from the workspace database's `excluded_directories` table AND remove the entry from the `excluded_directories` array in `D2NET-Settings.json`.
- **FR-004a**: For an accepted path that is currently in the workspace's exclusion list but has `kind` other than `'manual'` (i.e. `'tool'` or `'pattern'`, inserted by init's auto-detection heuristics), the new mode MUST refuse the removal by default. The entire invocation MUST be rejected (all-or-nothing) with a distinct, documented exit code and a stderr message that names the offending path AND its `kind`. The operator can override this protection by supplying the explicit flag `--allow-system-exclusions` on the same invocation, which causes such paths to be removed alongside any `kind='manual'` paths in the same transaction.
- **FR-005**: For every accepted path that is currently excluded AND whose `.dart` files are NOT covered by a surviving ancestor exclusion, the new mode MUST walk the on-disk source tree under that path and insert one row into `dart_files` per `.dart` file found, using the same forward-slash repo-root-relative path format and auto-generated id semantics as init. The insert MUST use `ON CONFLICT (full_path) DO NOTHING` to be idempotent against any drift between settings JSON and the database.
- **FR-006**: The new mode MUST NOT insert `dart_files` rows for paths under a removed exclusion when a strictly-shorter ancestor exclusion still covers them. The run summary MUST report this case explicitly, naming the surviving ancestor.
- **FR-007**: The new mode MUST NOT modify `phase_sequence` or `phase_status`. The `last_updated` column on every existing `phase_status` row MUST remain unchanged.
- **FR-008**: The `excluded_directories` deletion(s) and the `dart_files` insertion(s) MUST commit as a single all-or-nothing database transaction. The settings-file (`D2NET-Settings.json`) update happens via write-temp-then-rename and is sequenced after the database COMMIT, mirroring feature 007's research R4 pattern. The narrow rename-after-commit window is the only documented exception to bit-identical-to-pre-run-state, and recovery is to re-run the same invocation (the database operations are idempotent).
- **FR-009**: Re-supplying a path that is NOT currently in the exclusion list MUST be a no-op for that path and MUST NOT cause the invocation to fail. Such paths MUST be reported in the run summary as `not-currently-excluded` rather than silently dropped.
- **FR-010**: Re-supplying a path that has already been processed earlier in the same invocation (intra-batch duplicate) MUST collapse to one removal and report the duplicate as redundant within the same run.
- **FR-011**: The new mode MUST print a concise human-readable summary on success: number of exclusions removed (named individually), number of paths reported as `not-currently-excluded` (named individually), number of paths whose `.dart` files remain covered by a surviving ancestor (named individually with the surviving ancestor), and number of `dart_files` rows inserted grouped by the removed exclusion that triggered the insert.
- **FR-012**: The new mode MUST accept a `--json` flag that switches the success summary to a stable structured JSON document, consistent in style with feature 007's `--add-exclude` JSON output. Suggested fields: `result`, `removed`, `not_present`, `covered_by_ancestor`, `inserted_rows`, `totals`. Empty arrays MUST be emitted as `[]` rather than omitted.
- **FR-013**: The new mode MUST use distinct, documented, non-zero exit codes for at least the following failure conditions: path-outside-source, settings-write-failed, db-write-failed, workspace-lock-contention, and system-exclusion-refused (the FR-004a default-protection case, raised when one or more supplied paths have `kind != 'manual'` AND `--allow-system-exclusions` was not supplied). None of these codes MAY collide with feature 007's codes 12–16; the proposed range is 17–21. The file-path rejection case from FR-017 reuses feature 007's existing `AddExcludePathIsFile` (16), because the semantic is identical (a file path was supplied to a directory-only exclusion mode); allocating a new code would be needless duplication.
- **FR-014**: The new mode MUST be documented in the binary's `--help` output, alongside `--add-exclude` and the existing inspection / init flags.
- **FR-015**: After the new mode returns success, the existing `--list`, `--Exclusions`, and `--current-phase` inspection commands MUST report the post-update state immediately, with no caching or staleness window.
- **FR-016**: The new mode MUST NOT alter the behaviour of init-mode `--exclude`, of `--add-exclude` (feature 007), or of the destructive `--FORCE --DELETE-EXISTING` rebuild path.
- **FR-017**: The new mode MUST NOT support file-level paths (e.g. paths ending in `.dart`, `.zip`, `.exe`). Existing exclusion semantics are directory-only at both add-time (feature 007) and remove-time (this feature). Supplying a file path MUST be rejected with a clear stderr message and a distinct exit code (mirroring `AddExcludePathIsFile`).
- **FR-018**: The new mode MUST canonicalise supplied paths using the same rules as feature 007's `--add-exclude` (forward-slash separators, trailing slashes stripped, backslashes normalised, user-supplied form preserved verbatim in stderr).

### Key Entities *(include if feature involves data)*

- **Exclusion entry**: a directory path stored as one row in `excluded_directories` (PK `path`, plus `kind` per the existing CHECK constraint) and as one entry in the `excluded_directories` array of `D2NET-Settings.json`. Both stores must agree on the new state after a successful remove.
- **Dart file target row**: a row in `dart_files` representing one indexed `.dart` source file with auto-generated id, bare filename, and forward-slash repo-root-relative `full_path`. The new mode inserts rows for `.dart` files found under removed exclusions whose path is not covered by a surviving ancestor.
- **Phase row**: a row in `phase_sequence` (phase, integer sequence) or `phase_status` (phase, status, last_updated). The new mode never reads, writes, or deletes phase rows.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Removing one or more exclusions from a workspace and re-indexing the resulting source files completes in a developer-acceptable time on a developer workstation. The wall-clock target is dominated by the per-invocation PGLite bridge cold-start (research R4 of feature 007 observed ~12 s on a cold cache), so the practical budget is "comparable to a single inspection invocation". Measurable target: under 15 seconds for a workspace where the removed exclusion covers up to 1,000 `.dart` files.
- **SC-002**: After removing a directory exclusion that covers exactly N `.dart` files on disk and is NOT covered by a surviving ancestor, `dart_files` grows by exactly N rows with correct `full_path` values, with zero false-positive insertions and zero false-negative omissions, in every test run.
- **SC-003**: For every successful or failed invocation of the new mode, `phase_sequence` and `phase_status` rows are byte-identical to their pre-invocation state in 100 % of runs.
- **SC-004**: A second invocation with the same arguments as a successful first invocation results in zero new removals, zero `dart_files` rows inserted, and an exit code of 0 in 100 % of runs (every supplied path is reported as `not-currently-excluded`).
- **SC-005**: A simulated process kill or storage error mid-run leaves `D2NET-Settings.json` and the workspace database in their exact pre-invocation state in 100 % of induced-failure tests, except for the documented narrow post-COMMIT rename window which exits with the dedicated `RemoveExcludeSettingsWriteFailed` code and is recoverable by re-running the same invocation.
- **SC-006**: Each of the five documented error conditions (path-outside-source, settings-write-failed, db-write-failed, workspace-lock-contention, system-exclusion-refused) produces a distinct, documented exit code in 100 % of triggering runs, and the offending path (where applicable) appears verbatim in the stderr message in 100 % of path-rejection runs. For the system-exclusion-refused case, the stderr also names the row's `kind` so the operator can act on the diagnosis.
- **SC-007**: Removing an exclusion whose `.dart` files are still covered by a surviving ancestor inserts zero `dart_files` rows, removes the exact row from `excluded_directories`, and emits a clearly-named ancestor-survival entry in the run summary in 100 % of runs.
- **SC-008**: A developer who has applied a sequence of exclusions across multiple batches can perform any combination of additions and removals across at least 10 round-trip iterations without losing any phase progress, measured by `phase_status` row equality before the first iteration and after the tenth.

## Assumptions

- **CLI shape**: the new mode is invoked via a repeatable `--remove-exclude <path>` flag, mirroring feature 007's `--add-exclude`. A subcommand-style invocation was considered and rejected for the same reason as in feature 007 (the binary is otherwise flat). The clarify session may revisit this; default = flag form.
- **`not-currently-excluded` semantics**: a no-op + summary report rather than an error. Confirmed in clarification session 2026-05-01. This mirrors feature 007's redundancy semantics for `--add-exclude` and lets scripts safely re-issue the same command without branching on exit code.
- **Ancestor-survival semantics**: removing an exclusion whose `.dart` files are still covered by a surviving ancestor exclusion succeeds with exit 0, removes the named row, and emits the case in the run summary without re-indexing. Confirmed in clarification session 2026-05-01.
- **Atomicity scope**: a single invocation is one transaction across all supplied paths. Per-path commits (where some paths apply and others don't) were considered and rejected for symmetry with `--add-exclude`'s all-or-nothing guarantee.
- **Path canonicalisation**: identical to `--add-exclude`'s rule (forward-slash separators, trailing slashes stripped, backslashes normalised). The user-supplied form is preserved verbatim in stderr messages.
- **Workspace storage engine**: single-user PGLite at `.D2NET/pgdb/`, accessed via the per-invocation Node.js subprocess `bridge-direct.mjs` established by feature 005 and reused by feature 007. No new storage infrastructure introduced.
- **Schemas (read-only by this spec)**:
  - `excluded_directories(path TEXT PK, kind TEXT CHECK in ('tool','pattern','manual'))` per the canonical schema in feature 007.
  - `dart_files(id BIGSERIAL PK, filename TEXT NOT NULL, full_path TEXT NOT NULL UNIQUE)` per the canonical schema in feature 007.
- **Source-root invariance**: this feature does not modify the configured source directory, target directory, target extension, or connection details. Only the exclusion list and `dart_files` table are mutated.
- **Skill contract amendment is separate**: the `/D2NET-init` skill remains invocation-only and never edits `D2NET-Settings.json` directly. A future skill-contract amendment will add a verb mapping for `--remove-exclude` (e.g. "unexclude `<path>`") so the skill can drive add/remove batch flows without destructive rebuilds.
- **Out of scope for this feature**: changes to `--add-exclude`, init-mode `--exclude`, or the destructive rebuild path; bulk operations such as "remove all manual exclusions"; pattern-based or glob-based path matching; re-running the suggested-exclusion auto-detection heuristics; file-level exclusions; any change to the `/D2NET-init` skill contract.
