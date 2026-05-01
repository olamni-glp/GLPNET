# Data Model — D2NET.Init Incremental Exclusion Updates

This document describes the entities, tables, and read-modify-write sequences used by the new `--add-exclude` mode. No DDL changes are introduced; the existing schema in `tools/d2net/src/D2Net.Init/Schema/db-schema.sql` is fully sufficient.

## Storage entities touched

### 1. `excluded_directories` (Postgres / PGLite)

```sql
CREATE TABLE excluded_directories (
    path TEXT PRIMARY KEY,
    kind TEXT NOT NULL CHECK (kind IN ('tool', 'pattern', 'manual'))
);
```

- **Inserted by add-exclude**: one row per accepted, non-redundant new path. `kind = 'manual'` (research R6).
- **Path format**: forward-slash relative path under the configured source directory. Trailing separators stripped, backslashes normalised to forward slashes (research R2 + R1).
- **Uniqueness**: `path` is the primary key. `INSERT ... ON CONFLICT (path) DO NOTHING` is used to make the insert step idempotent against drift between settings JSON and database state.

### 2. `dart_files` (Postgres / PGLite)

```sql
CREATE TABLE dart_files (
    id        BIGSERIAL PRIMARY KEY,
    filename  TEXT NOT NULL,
    full_path TEXT NOT NULL UNIQUE
);
```

- **Deleted by add-exclude**: every row whose `full_path` lies under any newly inserted exclusion. The deletion query uses a boundary-aware prefix match:

  ```sql
  DELETE FROM dart_files
   WHERE full_path = @sourceDir || '/' || @excl
      OR full_path LIKE @sourceDir || '/' || @excl || '/%';
  ```

  - `@sourceDir` is read from the `setting` table (`source_dir`).
  - `@excl` is the new exclusion path.
  - The `full_path = @sourceDir || '/' || @excl` arm covers the unlikely case where `full_path` happens to equal the directory string itself (defensive — should not occur in practice because rows always represent files).
  - The `LIKE` arm requires an explicit trailing `/` so that excluding `bin` does not match `binary.dart` (FR-005 directory boundary).

- **Counts collected**: per-exclusion `RowsAffected` is captured for the success summary (FR-009).

### 3. `phase_sequence` and `phase_status` (Postgres / PGLite)

- **Untouched by add-exclude**: no SELECT, no INSERT, no UPDATE, no DELETE. The transaction explicitly avoids these tables. Row-level invariance is enforced by xUnit `AddExcludePhaseInvarianceTests` which snapshots all rows and the `last_updated` timestamps before and after a successful add-exclude run.

### 4. `D2NET-Settings.json` (file projection)

- **`excluded_directories` array**: the only field rewritten by add-exclude. The array is the union of the pre-run array and the new accepted paths, with redundant entries (sub-paths of an already-listed ancestor, after canonicalisation) removed. The order is **ascending lexicographic** to match the existing init-mode invariant in `SettingsWriter`.
- **All other fields unchanged**: `schema_version`, `source_dir`, `target_extension`, `target_dir`, `connection.*`, `created_at`. The rewrite is a load-modify-save round-trip through the existing `SettingsJsonRoot` POCO so any unknown JSON property would be lost — this is consistent with the existing init writer behaviour.
- **Atomicity**: write-temp-then-rename (research R4).

## Read-modify-write sequence

```
parse args                              ; ArgParser
load + validate WorkspaceLayout         ; existing
load D2NET-Settings.json                ; SettingsReader (new helper, or inline)
read source_dir from settings           ; settings.source_dir
canonicalise + validate paths           ; PathValidator
classify redundancy intra-batch         ; PathValidator (R1)
classify redundancy vs existing exclusions ; PathValidator (FR-008)
prepare new excluded_directories list   ; ascending lexicographic
write D2NET-Settings.json.tmp + fsync   ; SettingsWriter
start PgBridgeProcess                   ; existing
open NpgsqlConnection                   ; existing
BEGIN                                   ; new transaction in AddExcludeRunner
INSERT new exclusions ON CONFLICT NOTHING
for each new exclusion:
    DELETE prefix match from dart_files ; capture RowsAffected
COMMIT
File.Replace tmp -> D2NET-Settings.json
emit success summary (text or --json)
dispose PgBridgeProcess
exit 0
```

Failure branches:
- `BridgeStartFailed` payload matches lock-contention pattern → exit 15
- `BridgeStartFailed` other payload → exit 7 (existing) or 8 (existing) per feature 005's mapping
- Path validation fail → exit 12 or 16 (research R3); transaction never opened; tmp JSON deleted
- INSERT/DELETE/COMMIT throws → rollback (auto), delete tmp JSON, exit 14
- File.Replace throws → rollback already complete (commit fired); database is updated but settings JSON is stale → emit warning, exit 13

## Validation rules (consolidated)

| Rule | Source | Enforcement point |
|---|---|---|
| Path is non-empty after trim | usability | `PathValidator.ValidateRaw` |
| Path resolves under source root | FR-003, R2 | `PathValidator.RelativeToSourceRoot` |
| Path is not an existing file | FR-015, R2 stat case | `PathValidator.NotAnExistingFile` |
| Path with known file suffix doesn't pretend to be a directory | R2 suffix-fallback case | `PathValidator.NotALikelyFileSuffix` |
| Path is not the workspace folder itself (`.D2NET`) | safety | `PathValidator.NotWorkspaceFolder` |
| Workspace exists | FR-002 | `AddExcludeRunner.Preflight` |
| Path canonicalisation converges | FR-016, R1 | `PathValidator.Canonicalise` |
| Same path twice in one invocation collapses to one | edge case | `PathValidator.DedupeIntraBatch` |
| Path under an already-excluded ancestor is reported redundant | FR-008 | `PathValidator.ClassifyRedundancy` |

## Concurrency invariants

- **Single-writer**: PGLite is single-user; only one bridge subprocess can hold the data directory at a time. The contention case is detected at bridge startup (research R5).
- **Crash safety**: temp-JSON-then-rename gives atomicity for the JSON file. The Postgres transaction gives atomicity for the database. The narrow window between commit and rename is documented and recoverable (research R4).
- **No phase mutations**: add-exclude touches only `excluded_directories` and `dart_files`. Snapshot-equality on `phase_sequence` and `phase_status` row sets is asserted by tests.

## Out of scope

- New columns, indexes, or tables.
- Schema migrations.
- Removing exclusions (`--remove-exclude`) — explicit out-of-scope per spec.
- File-level exclusions — explicit out-of-scope per spec.
- Changes to init-mode `--exclude` — explicit out-of-scope per spec.
