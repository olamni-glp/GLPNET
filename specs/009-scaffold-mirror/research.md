# Research — D2NET.Scaffold Source-Tree Mirror

This document resolves the implementation questions surfaced by reading the existing `tools/d2net/src/D2Net.Scaffold/` source after `/speckit-clarify`.

## R1 — Schema-migration mechanism for the two new `dart_files` columns

**Decision**: Add the columns once on first scaffold run via `ALTER TABLE dart_files ADD COLUMN IF NOT EXISTS target_parent_dir TEXT; ALTER TABLE dart_files ADD COLUMN IF NOT EXISTS target_workdir_name TEXT;` issued inside the scaffold's main write transaction, before any `UPDATE dart_files SET ...` statements. Both columns are nullable initially because pre-scaffold rows (created by init) won't have values; scaffold's update populates them.

**Rationale**: PGLite supports `ADD COLUMN IF NOT EXISTS` (Postgres dialect); no separate migration script is needed. Putting the DDL inside the write transaction means the column existence and the row updates commit atomically — there is never a moment where the columns exist with all-NULL values for a partially-completed scaffold.

**Alternatives considered**:
- *Update the schema in `tools/d2net/src/D2Net.Init/Schema/db-schema.sql` and have init create the columns from day one*. Rejected: every existing workspace from the post-007/008 era would still lack the columns, so scaffold needs the runtime ALTER anyway. Updating `db-schema.sql` for new workspaces created by future inits is a follow-up cleanup, out of scope here.
- *Run the ALTER in a separate transaction before the main one*. Rejected: opens a window where the columns exist with NULLs.

## R2 — Tracker storage shape: `setting` row vs `scaffold_tracker` table

**Decision**: A small `scaffold_tracker` table with one row per scaffold-managed source-path, NOT a single `setting` row. Schema (created by scaffold on first run via `CREATE TABLE IF NOT EXISTS`):

```sql
CREATE TABLE IF NOT EXISTS scaffold_tracker (
    source_path        TEXT PRIMARY KEY,            -- forward-slash repo-root-relative; same shape as dart_files.full_path
    is_dart            BOOLEAN NOT NULL,             -- true => __workdir was also created
    target_parent_dir  TEXT NOT NULL,                -- native separators, absolute (mirrors dart_files column)
    last_scaffold_at   TIMESTAMPTZ NOT NULL DEFAULT now()
);
```

The table is used by FR-010/FR-011 (idempotency + reconciliation) and FR-012 (is-this-target-ours decision: target tree is scaffold-managed iff this table has at least one row whose `target_parent_dir` falls under the configured target root).

**Rationale**: a single hash-row would make FR-011 reconciliation expensive — to compute "what to add / remove" you'd need to walk the full target tree and recompute the inventory. A per-source-path table gives O(N) reconciliation by simple SQL set difference between the live source walk and the table contents. The cost is one row per copied file, which for the SC-001 ceiling of ~6,000 files is trivial.

**Alternatives considered**:
- *Single `setting` row with `value` = JSON-encoded inventory*. Rejected: row-level UPDATE on a large JSON blob is slower than per-row UPDATE on a normalised table; query patterns (set difference) are cleaner with rows.
- *Encode the tracker as one row per scaffolded directory rather than per file*. Rejected: file-level granularity is needed because FR-006 creates a per-file `__<basename>/` directory; per-directory granularity would force scaffold to re-walk to discover them.

## R3 — Source-tree snapshot mechanism

**Decision**: Re-walk the source tree on every scaffold invocation. Use `tools/d2net/src/D2Net.Init/DartFileScanner.Scan(repoRoot, sourceDir, postExclusionList)` to get the canonical list of `.dart` files (already used by 007/008 for the same purpose), and a fresh recursive directory walk for the non-`.dart` files. Both walks honour the same exclusion-aware skip rules as init's scanner.

The `dart_files` rows in the database are NOT used as the source-of-truth for what files exist — they're a projection of the source state, but they may be one *-exclude invocation behind. Scaffold's job is to project the *current* source state (minus the *current* exclusions) into the target tree, so it walks fresh and updates `dart_files` rows that need new column values.

**Rationale**: correctness over speed. The cost is one extra source-tree walk on every scaffold (a few hundred ms for the SC-001 ceiling). The risk of using `dart_files` as snapshot is that any drift (e.g. a `.dart` file appeared in source after the last `--add-exclude` run) would cause scaffold to silently miss the new file. A fresh walk avoids this entirely.

**Alternatives considered**:
- *Trust `dart_files` for the `.dart` set + walk for non-`.dart` files*. Rejected: half-trust creates a confusing dual-source-of-truth model.
- *Run an internal "refresh dart_files" pass before scaffolding*. Rejected: that would be re-implementing init's scanner inside scaffold; just call the scanner.

## R4 — `__<basename>` collision detection ordering

**Decision**: Pre-walk classification, before any staging-directory writes. The `TargetTreePlanner` step builds a complete plan of (source path → target path → optional __workdir path) tuples; if any planned `__workdir` path conflicts with a real file or non-empty directory in the source tree at the same relative location (which would force the target to be inconsistent with the source), the plan fails and scaffold exits with the working-dir-collision exit code before any IO.

**Rationale**: fail-fast and zero-IO-on-rejection. Catches the case described in spec edge case "A `.dart` file whose basename collides with an existing non-`.dart` directory in the source tree" without ever creating a staging directory.

**Alternatives considered**:
- *Detect at staging-write-time*. Rejected: would still require rolling back the staging directory, which is wasted IO; pre-walk is strictly better.
- *Require operators to manually delete colliding paths*. Rejected: scaffold should diagnose, not require manual intervention.

## R5 — Sentinel file content and location

**Decision**: An empty file at `<target>/.d2net-scaffold-tracker` (no extension, no content). Per the clarified hybrid tracker model, this file has no semantic meaning — it's purely a hint to operators browsing the target tree on disk. The authoritative tracker is the `scaffold_tracker` table (per R2).

**Rationale**: empty file = simplest implementation; minimal IO; minimal risk of stale content getting out of sync with the DB. The presence-vs-absence is the only signal, and even that's purely visual.

**Alternatives considered**:
- *JSON content with a hash or last-scaffold timestamp*. Rejected: any content is a potential out-of-sync surface that operators might mis-trust.
- *No sentinel file at all*. Rejected: the hybrid model in clarification Q2 explicitly chose D (DB authoritative + sentinel file for visibility); no sentinel means we're back to plain C.

## R6 — Exit code allocation

**Decision**: Allocate the next contiguous range after feature 008's 17–21. Proposed:

| Exit code | Constant | Meaning |
|---|---|---|
| 22 | `ScaffoldWorkspaceMissing` | No `.D2NET/` workspace at CWD. (May reuse 6 if cross-tool consistency is preferred — see below.) |
| 23 | `ScaffoldSourceMissing` | The configured source directory does not exist on disk. |
| 24 | `ScaffoldTargetNotEmptyAndNotManaged` | Target directory exists with content not produced by a prior scaffold run, and `--FORCE --DELETE-TARGET` was not supplied. |
| 25 | `ScaffoldWorkdirCollision` | A planned `__<basename>/` working directory collides with a pre-existing real file or non-empty directory at that path. |
| 26 | `ScaffoldCopyError` | Filesystem IO failure during the staging copy or the atomic rename. |
| 27 | `ScaffoldDbWriteFailed` | The Postgres transaction failed (DDL / UPDATE / UPSERT / COMMIT). |
| 28 | `ScaffoldWorkspaceLocked` | Another `d2net-init` or `d2net-scaffold` holds the workspace lock. |
| 29 | `ScaffoldOperatorCancelledTargetDeletion` | Operator declined the `--FORCE --DELETE-TARGET` confirmation prompt. |

The `ScaffoldWorkspaceMissing` (22) intentionally duplicates the semantic of `WorkspaceMissingForInspection` (6). Rationale: keeping per-tool codes makes the calling skill's branching simpler ("any 22 means scaffold workspace missing") even at the cost of one extra constant. The contract also documents that 6 may appear if the workspace is queried by an inspection helper inside scaffold.

**Rationale**: contiguous numbering keeps the catalogue simple. The split into 8 codes covers every documented failure path in FR-016 plus the FR-012a operator-cancelled case.

**Alternatives considered**:
- *Reuse `WorkspaceMissingForInspection` (6) for the workspace-missing case*. Reasonable; would give 7 new codes instead of 8. Implementation may choose either; the contract documents both options.
- *Combine `ScaffoldTargetNotEmptyAndNotManaged` (24) and `ScaffoldWorkdirCollision` (25) into a single "target-collision" code*. Rejected: they represent different operational situations (the former is a wholesale "this isn't ours" check; the latter is a per-file naming clash) and the calling skill benefits from telling them apart.

## R7 — Phase-row update semantics

**Decision**: At scaffold start, UPSERT a `phase_status` row with phase = `'scaffold'`, status = `'IN_PROGRESS'`, last_updated = now(). At successful completion, UPDATE the same row to status = `'COMPLETED'`. On any non-zero exit before the main transaction commits, UPDATE the row to status = `'FAILED'` (best effort — if the bridge is unreachable this update is skipped and the row remains `'IN_PROGRESS'`, which is acceptable because the next scaffold run will UPSERT it again).

`phase_sequence` rows are touched ONLY if no row for `'scaffold'` exists yet, in which case scaffold inserts one with sequence = (max(sequence) over the table) + 1, or sequence = 1 if the table is empty. Scaffold never UPDATEs an existing `phase_sequence` row.

**Rationale**: matches the convention established by feature 002 (`phase_status` and `phase_sequence` exist for tools to record their own progress). Other phases (analyze, port, etc.) maintain their own rows; scaffold owns only the `'scaffold'` row.

**Alternatives considered**:
- *Don't touch phase tables at all*. Rejected: scaffold IS a downstream phase and the convention from spec 002 expects each tool to record its progress.
- *Always re-INSERT the phase_sequence row*. Rejected: would violate the primary key constraint on re-runs.
