# Data Model — D2NET.Init `--remove-exclude`

This document describes the entities, tables, and read-modify-write sequence used by the new `--remove-exclude` mode. No DDL changes are introduced.

## Storage entities touched

### 1. `excluded_directories` (Postgres / PGLite)

```sql
CREATE TABLE excluded_directories (
    path TEXT PRIMARY KEY,
    kind TEXT NOT NULL CHECK (kind IN ('tool', 'pattern', 'manual'))
);
```

- **Read by remove-exclude**: a single `SELECT path, kind FROM excluded_directories WHERE path = ANY(@paths)` before the write transaction (research R3) feeds kind-validation, not-currently-excluded classification, and ancestor-survival classification.
- **Deleted by remove-exclude**: rows whose `path` matches one of the accepted-for-removal paths. SQL: `DELETE FROM excluded_directories WHERE path = ANY(@accepted);`. No CASCADE — only this table is affected.
- **Kind-validation rule**: a row with `kind` other than `'manual'` is refused unless `--allow-system-exclusions` was supplied. Refusal collects every offending path-and-kind pair, names them in stderr, and exits with code 21. No partial application.

### 2. `dart_files` (Postgres / PGLite)

```sql
CREATE TABLE dart_files (
    id        BIGSERIAL PRIMARY KEY,
    filename  TEXT NOT NULL,
    full_path TEXT NOT NULL UNIQUE
);
```

- **Inserted by remove-exclude**: one row per `.dart` file enumerated by `DartFileScanner.Scan(repoRoot, sourceDir, postRemovalExclusions)`. Insert uses `ON CONFLICT (full_path) DO NOTHING` so any pre-existing row (drift artefact) does not cause a primary-key violation. Auto-generated `id` continues monotonically from PGLite's BIGSERIAL sequence — re-inserts get fresh ids, which is consistent with init-time semantics.
- **Path format**: forward-slash, repo-root-relative, identical to init's. Identical to feature 007.

### 3. `phase_sequence` and `phase_status` (Postgres / PGLite)

- **Untouched by remove-exclude**: no SELECT, no INSERT, no UPDATE, no DELETE. The transaction explicitly avoids these tables. Row-level invariance is asserted by `RemoveExcludePhaseInvarianceTests`.

### 4. `D2NET-Settings.json` (file projection)

- **`excluded_directories` array rewritten**: post-removal list (existing minus accepted-for-removal). Order is ascending lexicographic to match the existing init/add-exclude invariant.
- **All other fields unchanged**: `schema_version`, `source_dir`, `target_extension`, `target_dir`, `connection.*`, `created_at`. Round-trip through the existing `SettingsJsonRoot` POCO via `SettingsWriter.PrepareTempSettingsWithExclusions` (reused from feature 007).
- **Atomicity**: write-temp-then-rename, sequenced after `COMMIT`.

## Read-modify-write sequence

```
parse args                                    ; ArgParser
load WorkspaceLayout, verify .D2NET/ exists   ; existing
load D2NET-Settings.json snapshot             ; SettingsWriter.TryReadSnapshot
read source_dir from snapshot                 ; snapshot.SourceDir
canonicalise + path-validate every path       ; PathValidator.Canonicalise + ResolveUnderSource
file-vs-dir check                             ; PathValidator.LooksLikeFilePath
intra-batch dedupe                            ; PathValidator.ClassifyBatch (reuse)
[REJECTION POINT 1]                           ; outside-source / file-path / no-workspace
start bridge                                  ; PgBridgeProcess.StartAsync
open NpgsqlConnection                         ; existing
SELECT path, kind FROM excluded_directories
  WHERE path = ANY(@supplied)                 ; R3 preflight
classify each supplied path:                  ; R3
  - not in result   -> not-currently-excluded
  - kind=='manual'  -> to-remove
  - kind!='manual'  -> system-kind (refuse OR allow-with-override)
[REJECTION POINT 2]                           ; system-kind without override
compute post-removal exclusion list           ; existing minus to-remove (sorted)
classify ancestor-survival per to-remove path ; PathValidator.IsUnder against post-removal list
walk source tree with post-removal list       ; DartFileScanner.Scan
prepare D2NET-Settings.json.tmp + fsync       ; SettingsWriter.PrepareTempSettingsWithExclusions
BEGIN transaction                             ; new transaction in RemoveExcludeRunner
DELETE FROM excluded_directories
  WHERE path = ANY(@accepted)                 ; one DELETE
for each scanner row:
  INSERT INTO dart_files (filename, full_path)
    VALUES (@f, @p)
    ON CONFLICT (full_path) DO NOTHING        ; capture aggregate insert count
COMMIT
File.Replace tmp -> D2NET-Settings.json
emit success summary (text or --json)
dispose bridge
exit 0
```

Failure branches (mirror 007):
- Outside-source / file-path → exit 17 / 18 (TBD numbers; spec says 17 for outside-source). Transaction never opened. No temp file.
- No workspace → exit 6.
- System-kind without override → exit 21. SELECT happened; no transaction. No temp file.
- Bridge lock-contention → exit 20.
- DELETE / INSERT / COMMIT throws → rollback, delete temp JSON, exit 19.
- File.Replace throws → rollback already complete; database is updated but settings stale; warning to stderr; exit 18.

## Validation rules (consolidated)

| Rule | Source | Enforcement point |
|---|---|---|
| Path is non-empty after trim | usability | `PathValidator.Canonicalise` (reuse) |
| Path resolves under source root | FR-003 | `PathValidator.ResolveUnderSource` (reuse) |
| Path is not an existing file | FR-017 | `PathValidator.LooksLikeFilePath` (reuse) |
| Workspace exists | FR-002 | `RemoveExcludeRunner.Preflight` |
| Path canonicalisation converges | FR-018 | `PathValidator.Canonicalise` (reuse) |
| Same path twice in one invocation collapses | edge case | `PathValidator.ClassifyBatch` (reuse, adapted) |
| Path not in current exclusion list reported | FR-009 | `RemoveExcludeRunner` consumes R3 SELECT result |
| Non-manual rows refused unless override | FR-004a | `RemoveExcludeRunner` consumes R3 SELECT result |
| Ancestor-survival reported, no rows inserted | FR-006 | pre-walk classification + post-removal walk |

## Concurrency invariants (carry forward from 007)

- Single-writer; PGLite lock-contention detected at bridge startup (research R5 of 007).
- Crash safety: temp-JSON-then-rename + Postgres transaction; the narrow post-COMMIT rename window is recoverable by re-running.
- No phase mutations: same SQL audit constraint as 007 (`RemoveExcludeRunner` MUST emit no SQL referencing `phase_sequence` or `phase_status`).

## Out of scope

- New columns, indexes, or tables.
- Schema migrations.
- Adding exclusions (`--add-exclude` exists in feature 007).
- File-level removals.
- Pattern-based or glob-based path matching.
- Bulk operations such as "remove all manual exclusions".
