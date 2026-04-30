# Data Model — D2NET.Init

**Feature**: `002-d2net-init` — see [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md)

This document captures (a) the in-memory entities used by `D2NET.Init` while it runs, and (b) the on-disk artifacts the run produces — the JSON settings file and the five PGLite Postgres tables. The authoritative DDL lives in `contracts/db-schema.sql`; the JSON settings shape is defined in `contracts/settings-schema.json`.

---

## 1. In-memory entities

### `InitOptions` (record)

Parsed CLI inputs for the **init** path (default mode and `--FORCE --DELETE-EXISTING`). Immutable after parsing.

| Field | Type | Source | Notes |
|-------|------|--------|-------|
| `RepoRoot` | `string` (absolute) | `Directory.GetCurrentDirectory()` | Per FR-002 (Q2). Validated to look like a repo root. |
| `SourceDir` | `string?` | `--source <name>` or interactive | Validated to exist as a direct subdirectory of `RepoRoot` (FR-004). |
| `TargetExtension` | `string?` | `--target-extension <ext>` or interactive | Free text; user-controlled. |
| `TargetDir` | `string?` | `--target <name>` or interactive | Free text; recorded only. Init does not create or touch the target tree. |
| `ManualExclusions` | `IReadOnlyList<string>` | repeated `--exclude <path>` | Paths relative to `SourceDir`. |
| `AcceptSuggestedExclusions` | `bool` | `--accept-suggested-exclusions` | If true, all auto-detected exclusions are kept without prompting. |
| `Force` | `bool` | `--FORCE` flag | Required together with `DeleteExisting` to overwrite an existing workspace (FR-003). |
| `DeleteExisting` | `bool` | `--DELETE-EXISTING` flag | Required together with `Force`. |
| `NonInteractive` | `bool` | `--non-interactive` | If true, missing inputs become errors instead of prompts. |
| `BridgePort` | `int` | `--bridge-port <n>`, default `54329` | The local TCP port the bridge will bind on for this invocation (FR-011a). |

### `InspectOptions` (record)

Parsed CLI inputs for the **inspection** path (`--list`, `--Exclusions`, `--current-phase`). Immutable after parsing. Mutually exclusive with the init flags.

| Field | Type | Source | Notes |
|-------|------|--------|-------|
| `RepoRoot` | `string` (absolute) | `Directory.GetCurrentDirectory()` | If no `.D2NET/` exists here, exit non-zero (FR-020). |
| `Mode` | `enum InspectMode { List, Exclusions, CurrentPhase }` | The flag the user supplied | Exactly one is permitted per invocation. |
| `Json` | `bool` | `--json` | Switches stdout to compact JSON (FR-019a). |
| `BridgePort` | `int` | `--bridge-port <n>`, default `54329` | Same as `InitOptions`. |

### `WorkspaceLayout` (record)

Immutable resolved paths for the workspace. Built from `InitOptions.RepoRoot` after CWD validation succeeds.

| Field | Example |
|-------|---------|
| `RepoRoot` | `D:\BSTDEV\RESEARCH\glp\glpnet` |
| `WorkspaceDir` | `<RepoRoot>\.D2NET` |
| `SettingsFile` | `<WorkspaceDir>\D2NET-Settings.json` |
| `PgDir` | `<WorkspaceDir>\pgdb` |
| `BridgeScript` | `<install-root>\pgbridge\server.mjs` (vendored next to the executable) |

For the temp-staging pattern (R7), an init run also computes `WorkspaceDirTemp = <RepoRoot>\.D2NET.tmp.<guid>` and uses a parallel `WorkspaceLayout` rooted at it.

### `ProposedExclusion` (record)

Built by `ExclusionDetector` after walking the source tree.

| Field | Type | Notes |
|-------|------|-------|
| `Path` | `string` | Path relative to `SourceDir`, forward slashes. |
| `Kind` | `enum { Tool, Pattern, Manual }` | `Tool` = matched the well-known tool list (R8). `Pattern` = matched the archive/backup/old heuristic (R4). `Manual` = supplied via `--exclude`. |
| `Reason` | `string` | Human-readable, e.g. `".git is a Git metadata directory"` or `"matches archive marker"`. |

### `ApprovedExclusionList` (record)

The result of the prompt cycle (FR-008). One per init invocation.

| Field | Type |
|-------|------|
| `Items` | `IReadOnlyList<ProposedExclusion>` |
| `ApprovedAt` | `DateTimeOffset` (UTC, captured at approval time, used as `phase_status.last_updated` default in later phases) |

### `DartFileEntry` (record)

Built by `DartFileScanner` after pruning excluded directories.

| Field | Type | Notes |
|-------|------|-------|
| `Filename` | `string` | The leaf filename (e.g. `runner.dart`). |
| `FullPath` | `string` | Path from `RepoRoot`, **forward-slash separators on every OS** (FR-014). |

### `RunSummary` (record)

Fields used by the FR-021 stdout summary at the end of a successful init.

| Field | Type |
|-------|------|
| `WorkspaceDir` | `string` |
| `SettingsFile` | `string` |
| `PgDir` | `string` |
| `DbFile` | `string` (absolute path to `workspace.sqlite`) |
| `SourceDir` | `string` |
| `TargetExtension` | `string` |
| `TargetDir` | `string` |
| `ApprovedExclusions` | `IReadOnlyList<ProposedExclusion>` |
| `DartFileCount` | `int` |
| `CreatedAt` | `DateTimeOffset` |

---

## 2. On-disk: `D2NET-Settings.json`

Located at `<WorkspaceDir>/D2NET-Settings.json`. Validated against `contracts/settings-schema.json`.

```json
{
  "schema_version": 1,
  "source_dir": "glp_runtime",
  "target_extension": "_net",
  "target_dir": "glp_runtime_net",
  "excluded_directories": [
    ".git",
    ".dart_tool",
    "archive_2024",
    "lib/legacy"
  ],
  "connection": {
    "engine": "sqlite",
    "db_file": "D:\\repo\\.D2NET\\pgdb\\workspace.sqlite",
    "connection_string": "Data Source=D:\\repo\\.D2NET\\pgdb\\workspace.sqlite"
  },
  "created_at": "2026-04-30T12:34:56Z"
}
```

### Field rules

| Field | Type | Rules |
|-------|------|-------|
| `schema_version` | int | Always `1` for this MVP. Allows future versions to be detected by readers. |
| `source_dir` | string | Required. Matches `setting` row `source_dir`. Must be a relative directory name (no slashes, no `..`). |
| `target_extension` | string | Required. May be empty string. |
| `target_dir` | string | Required. Same constraints as `source_dir`. |
| `excluded_directories` | string[] | Sorted ascending. Entries are forward-slash relative paths under `source_dir`. Mirrors the `excluded_directories` table (just paths; `kind` is in the DB only). |
| `connection.engine` | string | Always `"sqlite"` in this MVP. |
| `connection.db_file` | string | Absolute path to the SQLite database file (`<repo-root>/.D2NET/pgdb/workspace.sqlite`). |
| `connection.connection_string` | string | `Microsoft.Data.Sqlite`-compatible connection string of the form `Data Source=<db_file>`. |
| `created_at` | string (ISO-8601 UTC) | Recorded once, on a fresh init. Re-init under `--FORCE --DELETE-EXISTING` writes a fresh `created_at`. |

---

## 3. On-disk: SQLite tables

Authoritative DDL is in `contracts/db-schema.sql`. The five tables and their invariants:

### 3.1 `setting`

```sql
CREATE TABLE setting (
    key   TEXT PRIMARY KEY,
    value TEXT NOT NULL
);
```

Required rows after a fresh init (6 keys, all strings):

| key | value |
|-----|-------|
| `source_dir` | the source directory name (must match JSON `source_dir`) |
| `target_extension` | the target extension (must match JSON `target_extension`) |
| `target_dir` | the target directory name (must match JSON `target_dir`) |
| `db_engine` | always `sqlite` in this MVP |
| `db_file` | absolute path to `workspace.sqlite` |
| `db_connection_string` | the full `Data Source=...` string |

Rule: every `db_*` row in `setting` MUST equal the corresponding JSON `connection.*` field.

### 3.2 `excluded_directories`

```sql
CREATE TABLE excluded_directories (
    path TEXT PRIMARY KEY,
    kind TEXT NOT NULL CHECK (kind IN ('tool', 'pattern', 'manual'))
);
```

One row per approved exclusion. `path` is a forward-slash relative path under `source_dir`. `kind` records *why* the exclusion exists (R5):
- `tool` — well-known tool subdirectory the user opted into (R8).
- `pattern` — matched the archive/backup/old heuristic (R4).
- `manual` — supplied via `--exclude` flag.

If the same path is suggested by multiple sources (e.g. `.git` is both well-known and could match a marker), the row is written once with `kind = 'tool'` (highest specificity).

### 3.3 `dart_files`

```sql
CREATE TABLE dart_files (
    id        INTEGER PRIMARY KEY AUTOINCREMENT,
    filename  TEXT NOT NULL,
    full_path TEXT NOT NULL UNIQUE
);
```

One row per `.dart` file in the source tree outside any excluded directory.
- `id`: auto-generated by SQLite via `INTEGER PRIMARY KEY AUTOINCREMENT`.
- `filename`: leaf filename (e.g. `runner.dart`). NOT unique on its own — `runner.dart` may appear in many directories.
- `full_path`: path from `RepoRoot`, forward slashes (FR-014). Unique across the table.

### 3.4 `phase_sequence`

```sql
CREATE TABLE phase_sequence (
    phase    TEXT PRIMARY KEY,
    sequence INTEGER NOT NULL
);
```

Created **empty** by `D2NET.Init`. Rows are inserted by downstream D2NET commands (e.g. `D2NET.Scaffold`, `D2NET.Analyze`). `sequence` orders phases ascending; lower numbers come first.

### 3.5 `phase_status`

```sql
CREATE TABLE phase_status (
    phase        TEXT PRIMARY KEY,
    status       TEXT NOT NULL,
    last_updated TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%SZ', 'now'))
);
```

Created **empty** by `D2NET.Init`. Status values are not constrained by D2NET.Init; downstream commands write whatever values they need. Only the literal string `COMPLETED` has special meaning, and only inside the `--current-phase` query (FR-019). `last_updated` is stored as ISO-8601 UTC text so it round-trips into the `--current-phase` output without any timezone conversion.

---

## 4. Relationships

```
InitOptions ────► WorkspaceLayout
                       │
                       ▼
ExclusionDetector ───► ProposedExclusion[]
                       │
                       ▼
InteractivePrompter ── ApprovedExclusionList
                       │
                       ▼
DartFileScanner ─────► DartFileEntry[]
                       │
                       ▼
SchemaInitializer ───► (5 empty tables)
SettingsWriter ──────► setting rows + D2NET-Settings.json
ExclusionsWriter ────► excluded_directories rows
DartFilesWriter ─────► dart_files rows
                       │
                       ▼
RunSummary (stdout)
```

`BridgeProcess` is held by `InitRunner` for the duration of all DB-touching steps; it owns the Node subprocess and is `Dispose`d before `InitRunner.Run` returns.

---

## 5. Validation rules (mapped to spec FRs)

| Rule | Spec | Enforcement point |
|------|------|------|
| CWD looks like a repo root (has `.git/` or `.D2NET/`, OR has a subdir matching the supplied source name) | FR-002 | `WorkspaceLayout` factory |
| `SourceDir` exists as a direct subdirectory of `RepoRoot` | FR-004 | `InitOptions` validation |
| `.D2NET/` does not exist, OR `(Force && DeleteExisting)` is set | FR-003 | `InitRunner` pre-check |
| Both `--FORCE` and `--DELETE-EXISTING` required together (or neither) | FR-003 | `InitOptions` validation |
| SQLite database file is openable at start of every command | FR-011b | `SqliteConnection.Open` (returns ExitCode 8 on failure) |
| All connections released before directory move | FR-022 | `SqliteConnection.ClearAllPools()` in `InitRunner` |
| `db_*` setting rows agree with JSON `connection.*` | FR-009, FR-012 | `SettingsWriter` writes from a single `DbConnectionSettings` |
| `dart_files.full_path` uses forward slashes on every OS | FR-014 | `DartFileScanner` (calls `path.Replace('\\', '/')`) |
| `phase_sequence` and `phase_status` created empty | FR-015, FR-016 | `SchemaInitializer` (no INSERTs into these tables) |
| `--current-phase` returns the lowest-sequence non-COMPLETED row | FR-019 | `CurrentPhaseInspector` SQL |
| Inspection options modify zero bytes under `.D2NET/` | FR-017–FR-019, SC-009 | Inspectors only run `SELECT`; verified by tests |
| In `--json` mode, stdout is JSON-only and stderr carries diagnostics | FR-019a, FR-020 | `OutputFormat` |
| On any abort during fresh init, no `.D2NET/` is left | FR-022 | Temp-staging pattern (R7) |
| On abort during `--FORCE --DELETE-EXISTING`, the previous workspace is restored | FR-022 | Rename-aside pattern (R7) |
