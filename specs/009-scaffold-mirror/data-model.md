# Data Model — D2NET.Scaffold Source-Tree Mirror

## Storage entities touched

### 1. `excluded_directories` (read-only)

```sql
CREATE TABLE excluded_directories (
    path TEXT PRIMARY KEY,
    kind TEXT NOT NULL CHECK (kind IN ('tool', 'pattern', 'manual'))
);
```

- **Read by scaffold**: `SELECT path FROM excluded_directories ORDER BY path` to obtain the post-`*-exclude` exclusion list. Fed into `DartFileScanner.Scan` and the non-`.dart` directory walker.
- **Not modified**.

### 2. `dart_files` (modified — schema additions + row updates)

```sql
CREATE TABLE dart_files (
    id        BIGSERIAL PRIMARY KEY,
    filename  TEXT NOT NULL,
    full_path TEXT NOT NULL UNIQUE
);
```

**Schema additions** (issued on first scaffold run inside the write transaction):

```sql
ALTER TABLE dart_files ADD COLUMN IF NOT EXISTS target_parent_dir   TEXT;
ALTER TABLE dart_files ADD COLUMN IF NOT EXISTS target_workdir_name TEXT;
```

- `target_parent_dir`: native-separator absolute path to the parent directory of the copied `.dart` file in the target tree (clarified 2026-05-01). Example on Windows: `D:\BSTDEV\RESEARCH\glp\glpnet\glp_runtime_net\bin`. Nullable (init-era rows may not have it yet).
- `target_workdir_name`: literal `__<basename>` (extension stripped, `__` prefix). Example: `__glp_repl`. Nullable.

**Row updates** (per scaffolded `.dart` file):

```sql
UPDATE dart_files
   SET target_parent_dir   = @parent,
       target_workdir_name = @workdir
 WHERE full_path = @full_path;
```

- `@full_path` is the existing forward-slash repo-rooted path; matches uniquely.
- If the row doesn't exist (drift case — file appeared in source after the last init / *-exclude), scaffold INSERTs it with the same shape init produces, plus the two new columns.

### 3. `scaffold_tracker` (NEW)

```sql
CREATE TABLE IF NOT EXISTS scaffold_tracker (
    source_path        TEXT PRIMARY KEY,
    is_dart            BOOLEAN NOT NULL,
    target_parent_dir  TEXT NOT NULL,
    last_scaffold_at   TIMESTAMPTZ NOT NULL DEFAULT now()
);
```

- **Created on first scaffold run** (idempotent `CREATE TABLE IF NOT EXISTS`).
- **One row per scaffolded source path** (both `.dart` files and non-`.dart` files).
- `source_path`: forward-slash repo-rooted (mirrors `dart_files.full_path` shape). PK ensures one row per path.
- `is_dart`: true iff a `__<basename>/` working directory was created next to this file in the target tree.
- `target_parent_dir`: native-separator absolute path to the parent in the target tree.
- `last_scaffold_at`: timestamp of the last successful scaffold run that touched this row.

**Used by**:
- **FR-010 idempotency**: re-running scaffold compares the live source state against `scaffold_tracker` rows; if equal, no UPDATE / DELETE / INSERT statements fire and the staging directory mirrors the live target byte-for-byte.
- **FR-011 reconciliation**: rows whose `source_path` is no longer in the live source tree (or now falls under an exclusion) are DELETEd; new live source paths get INSERTed.
- **FR-012 "is target ours?"**: target tree is scaffold-managed iff at least one `scaffold_tracker` row references a `target_parent_dir` that lies under the configured target root.

**On `--FORCE --DELETE-TARGET`** (after operator confirmation):
- All `scaffold_tracker` rows are DELETEd before the new scaffold inventory is INSERTed.
- The two new `dart_files` columns for affected rows are set to NULL.
- All commits happen in the same transaction as the staging-rename's atomic point.

### 4. `phase_sequence` and `phase_status`

```sql
CREATE TABLE phase_sequence (
    phase    TEXT PRIMARY KEY,
    sequence INTEGER NOT NULL
);
CREATE TABLE phase_status (
    phase        TEXT PRIMARY KEY,
    status       TEXT NOT NULL,
    last_updated TIMESTAMPTZ NOT NULL DEFAULT now()
);
```

- `phase = 'scaffold'`: scaffold UPSERTs at start (status = `'IN_PROGRESS'`) and UPDATEs at end (status = `'COMPLETED'` on success, `'FAILED'` best-effort on failure). `phase_sequence` row inserted only on first scaffold run.
- All other phase rows: untouched. Asserted by `ScaffoldPhaseInvarianceTests`.

### 5. `D2NET-Settings.json` (read-only)

- `source_dir`, `target_dir`, `target_extension`: read at scaffold start, used to construct paths.
- `excluded_directories` array: not consulted directly (the DB table is the source of truth post-007/008); the JSON is kept consistent by the *-exclude features.

### 6. Filesystem entities

- **Source tree**: read-only walk via `DartFileScanner` + a non-`.dart` directory walker. Honours exclusions.
- **Live target tree** (`<repo>/<target_dir>/`): created on first scaffold run via atomic rename of the staging directory. Read on subsequent runs to verify the FR-012 invariant.
- **Staging directory** (`<repo>/<target_dir>.d2net-tmp/`): scaffold writes the entire planned output here, then atomically renames over the live target after DB COMMIT.
- **Sentinel file** (`<target>/.d2net-scaffold-tracker`): empty file, non-semantic operator visibility hint per spec clarification Q2/D.

## Read-modify-write sequence

```
parse args                                  ; Program.cs ArgParser
load WorkspaceLayout, verify .D2NET/ exists ; PreflightChecker
load D2NET-Settings.json snapshot           ; SettingsWriter.TryReadSnapshot
verify <source_dir>/ exists on disk         ; PreflightChecker
[REJECTION POINT 1]                         ; workspace-missing / source-missing
spawn bridge                                ; PgBridgeProcess (lock-contention -> exit 28)
open NpgsqlConnection                       ; existing
read excluded_directories list              ; SELECT path FROM excluded_directories ORDER BY path
walk source tree (filtered by exclusions)   ; DartFileScanner (.dart) + recursive walk (non-.dart)
read scaffold_tracker (if exists)           ; SELECT * FROM scaffold_tracker
classify FR-012 "is target ours?"           ; live target dir state vs scaffold_tracker rows
[REJECTION POINT 2]                         ; target-not-empty-and-not-managed (exit 24)
                                            ;   unless --FORCE --DELETE-TARGET supplied
if --FORCE --DELETE-TARGET supplied AND target exists:
    interactive confirmation prompt         ; DestructiveTargetGate
[REJECTION POINT 3]                         ; operator-cancelled (exit 29)
plan target tree:
  - add-set: source paths newly in scope
  - remove-set: scaffold_tracker rows no longer in scope
  - dart-set: every .dart file -> __workdir name
  - non-dart-set: every non-.dart file -> verbatim copy
  - check __workdir collisions (FR-013, R4)
[REJECTION POINT 4]                         ; workdir-collision (exit 25)
delete <target>.d2net-tmp/ if leftover from prior aborted run
mkdir <target>.d2net-tmp/
copy non-dart files to staging              ; FileCopier
copy .dart files + create __workdir/        ; FileCopier + Directory.Create
write empty sentinel file in staging        ; <staging>/.d2net-scaffold-tracker
[REJECTION POINT 5]                         ; copy-error (exit 26)
BEGIN transaction
ALTER TABLE dart_files ADD COLUMN IF NOT EXISTS target_parent_dir   TEXT
ALTER TABLE dart_files ADD COLUMN IF NOT EXISTS target_workdir_name TEXT
CREATE TABLE IF NOT EXISTS scaffold_tracker (...)
if --FORCE --DELETE-TARGET took the destructive path:
    DELETE FROM scaffold_tracker
    UPDATE dart_files SET target_parent_dir = NULL, target_workdir_name = NULL
DELETE FROM scaffold_tracker WHERE source_path IN (remove-set)
INSERT INTO scaffold_tracker (...) VALUES (...) for every add-set row
                                            ; ON CONFLICT (source_path) DO UPDATE SET target_parent_dir = excluded.target_parent_dir, last_scaffold_at = now()
UPDATE dart_files SET target_parent_dir, target_workdir_name for every dart-set row
UPSERT phase_status (phase = 'scaffold', status = 'IN_PROGRESS')
COMMIT
[REJECTION POINT 6]                         ; db-write-failed (exit 27); delete staging
if live target exists:
    rmdir <target>/                          ; or rename to <target>.d2net-old/ then delete
rename <target>.d2net-tmp/ -> <target>/      ; atomic on POSIX; near-atomic on Windows
[REJECTION POINT 7]                         ; copy-error (exit 26); manually compensable
BEGIN transaction
UPDATE phase_status SET status = 'COMPLETED' WHERE phase = 'scaffold'
COMMIT
emit success summary (text or --json)
dispose bridge
exit 0
```

Failure semantics summary:
- Pre-bridge rejections (1) leave nothing on disk.
- Pre-plan rejections (2, 3) make no DB changes, no FS changes.
- Plan rejections (4) make no DB changes, no FS changes.
- Staging rejections (5) leave only `<target>.d2net-tmp/` which is deleted in `finally`.
- DB rejections (6) leave only `<target>.d2net-tmp/` which is deleted in `finally`; live target unchanged.
- Rename failure (7): rare; the staging dir survives for the operator to investigate.
- Post-COMMIT phase-status update failure: best-effort; doesn't fail the whole run.

## Validation rules

| Rule | Source | Enforcement point |
|---|---|---|
| Workspace exists | FR-002 | `PreflightChecker` |
| Source dir exists on disk | FR-003 | `PreflightChecker` |
| Target tree is scaffold-managed (or absent, or override given) | FR-012 | `TargetTreePlanner` (consults `scaffold_tracker`) |
| `--FORCE --DELETE-TARGET` requires both flags | FR-012a | `ArgParser` |
| `--FORCE --DELETE-TARGET` requires interactive confirmation | FR-012a | `DestructiveTargetGate` |
| `__<basename>/` collisions are pre-walk-detected | FR-013, research R4 | `TargetTreePlanner` |
| Atomic all-or-nothing | FR-014 | staging dir + DB transaction |
| Phase rows untouched (except scaffold) | FR-009 | `ScaffoldPhaseInvarianceTests` static SQL audit + runtime test |

## Out of scope

- Conversion of `.dart` to `_net` artefacts (downstream phases populate `__<basename>/`).
- Schema migrations beyond the two additive `dart_files` columns and the `scaffold_tracker` table.
- File-level exclusions or pattern-based reconciliation.
