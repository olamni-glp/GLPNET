# Research — D2NET.Init `--remove-exclude`

This document resolves the implementation questions surfaced by reading the existing source after `/speckit-clarify`.

## R1 — File-walk implementation: reuse `DartFileScanner`

**Decision**: Reuse `tools/d2net/src/D2Net.Init/DartFileScanner.cs` (the existing init-time walker) for the remove-exclude file walk. Call it with `excludedRelPaths` set to the **post-removal** exclusion list — i.e., the current exclusions minus the paths being removed but plus any surviving-ancestor exclusions. Because the scanner already returns `DartFileEntry(filename, full_path)` records that match the schema produced by init, the rows it yields are guaranteed byte-identical to those init would have inserted for the same source state.

**Rationale**: writing a new walker would risk subtly different semantics (e.g., symlink handling, dotfile skipping). The scanner is the canonical authority for "which `.dart` files are in scope given an exclusion set". Reusing it makes the remove-exclude inserts indistinguishable from init's, which is exactly what FR-005 requires.

**Alternatives considered**:
- *Write a new walker scoped to a single removed path*. Rejected: the surviving-ancestor case requires knowing the full exclusion set anyway, so we'd duplicate `DartFileScanner`'s logic.
- *Issue a Postgres recursive query against a directory listing*. Not applicable; PGLite holds metadata, not the source tree.

## R2 — Ancestor-survival check ordering: pre-walk

**Decision**: Determine ancestor-survival **before** the file walk, using the post-removal exclusion list (current ∪ already-surviving minus paths being removed AND whose `kind` allows removal). The walk then naturally skips any subtree that remains under a surviving ancestor, so no `dart_files` row is produced for it.

Concretely, if the operator removes `bin/archive` while `bin` survives:
1. Pre-walk classification: `bin/archive` is `removed`; `bin` survives.
2. Post-removal exclusion list: `[bin, ...]` (still includes `bin`).
3. `DartFileScanner.Scan(repoRoot, sourceDir, postRemovalExclusions)` skips everything under `bin`, so nothing under `bin/archive` is walked. Zero rows produced.
4. The run summary reports `bin/archive: covered-by-ancestor "bin"`.

**Rationale**: pre-walk classification is correct, cheap (one in-memory comparison per supplied path against the existing exclusion set), and avoids wasted IO on potentially huge trees. Post-walk-with-ON-CONFLICT would still produce the right database state but would scan directories whose results we'd then discard.

**Alternatives considered**:
- *Walk first, dedup via INSERT ON CONFLICT (full_path) DO NOTHING*. Rejected: wastes IO on trees that won't contribute rows. Also, the run summary needs to name surviving ancestors per supplied path — pre-walk already has this information.
- *Two-pass approach: walk full subtree, then filter in C#*. Same waste; rejected.

## R3 — Kind-aware preflight: single SELECT before transaction

**Decision**: Issue one `SELECT path, kind FROM excluded_directories WHERE path = ANY(@paths)` **before** opening the write transaction. The result is consumed by:

1. **FR-004a kind-validation**: any returned row whose `kind != 'manual'` causes a refusal (unless `--allow-system-exclusions` was supplied). All offending paths and their kinds are collected, surfaced verbatim in stderr, and the entire invocation exits with `RemoveExcludeSystemKindRefused` (21). No partial application.
2. **FR-009 not-currently-excluded classification**: any supplied path that produces no row in the SELECT result is classified `not-currently-excluded` and reported in the summary.
3. **FR-006 ancestor-survival classification**: paths classified `to-remove` are checked against the post-removal exclusion list; any whose subtree is still covered by a surviving ancestor are reported as `covered-by-ancestor` (the row removal still happens).

Only after these classifications does the write transaction begin. The transaction itself uses parameterised DELETE statements per accepted path and a single `DartFileScanner.Scan` call to produce the new `dart_files` rows.

**Rationale**: doing the kind+presence check pre-transaction keeps the transaction short, catches the safety case without touching the database, and produces a clean run summary even on the no-op path. Doing it inside the transaction would force a longer write lock and complicate the rollback path.

**Alternatives considered**:
- *Do the kind check inside the transaction with FOR UPDATE*. Rejected: longer lock window, more complex error handling, no benefit for our concurrency model (single-writer PGLite).
- *Skip the SELECT and rely on DELETE ... RETURNING kind*. Rejected: that would either delete a tool/pattern row before validation (bad) or require an extra SELECT after a deferred-constraint trick (more complex).

## R4 — Atomicity (carries forward from feature 007 R4)

**Decision**: Same write-temp-then-rename + transaction pattern as feature 007's `--add-exclude`. Sequence:

1. Read current settings JSON snapshot.
2. Run pre-walk classifications (R3).
3. Compute the post-removal exclusion list.
4. Run `DartFileScanner.Scan(...)` with the post-removal list to enumerate new `dart_files` rows for non-ancestor-covered removals.
5. Write the new settings JSON to a sibling temp file with `fsync`.
6. Open bridge, connect via Npgsql, `BEGIN`.
7. `DELETE FROM excluded_directories WHERE path = ANY(@accepted)`.
8. `INSERT INTO dart_files (filename, full_path) VALUES (@f, @p) ON CONFLICT (full_path) DO NOTHING` for each scanner row.
9. `COMMIT`.
10. Atomic rename `D2NET-Settings.json.tmp` → `D2NET-Settings.json`.
11. Bridge shutdown.

Failure paths mirror 007 exactly. The narrow rename-after-commit window emits `RemoveExcludeSettingsWriteFailed` (18) and is recoverable by re-running the same invocation (the DELETE is idempotent on already-removed rows; the INSERT is idempotent via `ON CONFLICT`).

**Rationale**: 007 already proved this pattern in production code and tests. Using a different pattern here would be unwarranted divergence.

## R5 — Concurrent invocation lock detection (carries forward from 007 R5)

**Decision**: Reuse the lock-contention pattern set established by 007. `RemoveExcludeRunner` checks `BridgeStartException.LastBridgeError` against the same substring list (`"EBUSY"`, `"EACCES"`, `"data directory in use"`, `"could not lock"`, `"another process"`). A match maps to `RemoveExcludeWorkspaceLocked` (20).

**Rationale**: identical lock semantics; identical detection. No reason to diverge.

## R6 — `--allow-system-exclusions` flag placement and parsing

**Decision**: A binary flag accepted only in `--remove-exclude` mode. The parser rejects it when supplied alongside init flags, inspection flags, or `--add-exclude`. It has no positional argument. Default state: false. Stored in `RemoveExcludeOptions.AllowSystemExclusions`.

When true, the kind-validation step in R3 still runs (so the run summary can name which non-manual rows were removed), but a non-manual `kind` does not trigger refusal — the row is removed alongside any manual rows in the same transaction.

**Rationale**: keeping the flag scoped to remove-exclude prevents accidental misuse in init or add-exclude contexts. Surfacing the kind in the summary even on the override path lets the operator audit what was removed.

**Alternatives considered**:
- *Make `--allow-system-exclusions` a global flag*. Rejected: scope creep; no other mode benefits.
- *Use a positional list of "kinds to allow" (e.g., `--allow-kinds tool,pattern`)*. Rejected: over-engineered; the binary flag is sufficient given there are only two non-manual kinds.
