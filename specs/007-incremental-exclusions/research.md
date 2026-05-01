# Research — D2NET.Init Incremental Exclusion Updates

This document resolves the deferred design questions identified in the spec's Assumptions section and the implementation questions surfaced by reading the existing `tools/d2net/src/D2Net.Init/` source. After Phase 0, no `NEEDS CLARIFICATION` markers remain.

## R1 — Within-batch path subsumption

**Decision**: When a single invocation supplies a path P and another supplied path Q where Q lies under P, both are evaluated as if P were processed first: P is treated as the new exclusion, and Q is reported as redundant in the run summary. Order of `--add-exclude` flags on the command line is irrelevant — paths are sorted ancestor-first before being classified.

**Rationale**: This matches the inter-invocation redundancy semantics already required by FR-008 (re-supplying a path under an already-excluded ancestor is a no-op for that path and is reported as redundant). Treating cross-supplied subsumption identically to cross-invocation subsumption removes a category of confusing edge-case behaviour ("why does running this once differ from running it twice?"). It also avoids the reverse error mode in which we record a redundant child row that becomes orphaned data once a re-init pass reads `excluded_directories` and finds two rows representing the same coverage.

**Alternatives considered**:
- *Insert all rows verbatim including redundant children*. Rejected: leaves the database with redundant rows that must be cleaned up by some future operation, and gives the operator no signal that the invocation contained a redundant pair.
- *Reject the invocation as a usage error*. Rejected: the user's `/D2NET-init` skill batch flow could plausibly emit overlapping batches across approval steps; rejecting them forces the skill to do its own intra-batch dedup.

## R2 — File-vs-directory detection (FR-015)

**Decision**: Validation is path-existence-aware:
- If the supplied path exists at validation time and is a file, reject with the new `AddExcludePathIsFile` exit code and a stderr message naming the file.
- If the supplied path exists and is a directory, accept.
- If the supplied path does not exist, accept (forward-looking exclusion as in User Story 1 acceptance scenario 4).

A pure suffix heuristic (`.dart`, `.zip`, `.exe`) is **not** used as the only check, but it is used as a *second-line* sanity filter: if the path does not exist on disk and ends in a known file suffix, reject as a likely typo. The whitelist of "known file suffixes" is the union of the source-file suffixes the binary recognises today plus common archive / binary suffixes: `.dart`, `.zip`, `.exe`, `.dll`, `.so`, `.dylib`, `.json`, `.txt`, `.md`, `.lock`. The list lives in `PathValidator.LikelyFileSuffixes` and is documented inline.

**Rationale**: Stat-based validation is the strongest correct-by-construction signal available, but the spec explicitly admits forward-looking exclusions (a path that does not yet exist). The suffix heuristic catches the most common typo class (a developer pointing at a file by mistake) without requiring the path to exist yet. Files with no extension (`Makefile`, `Dockerfile`, `LICENSE`) bypass the suffix filter and would be accepted as forward-looking exclusions if they don't exist; if they do exist, the stat check rejects them. This is acceptable: our exclusion semantics are directory-only and an extensionless file pretending to be a directory is a sufficiently rare misuse that we accept the small false-negative window for forward-looking exclusions.

**Alternatives considered**:
- *Stat only*. Rejected: forward-looking exclusions become impossible.
- *Suffix only*. Rejected: misses the common case of a developer with a real file like `bin/foo.exe` who expected the binary to know it's a file.

## R3 — Exit code numbering

**Decision**: Allocate the next five contiguous values after the existing 0–11 catalogue.

| Exit code | Constant | Meaning |
|---|---|---|
| 6 (reused) | `WorkspaceMissingForInspection` (renamed in comments to `WorkspaceMissingForOperation` for breadth) | No `.D2NET/` workspace at the current working directory; applies to inspection and add-exclude alike. |
| 12 (new) | `AddExcludePathOutsideSource` | One or more `--add-exclude` paths resolves outside the configured source directory. |
| 13 (new) | `AddExcludeSettingsWriteFailed` | `D2NET-Settings.json` could not be rewritten (rename failure, IO error, permission denied). |
| 14 (new) | `AddExcludeDbWriteFailed` | The Postgres transaction failed (insert, delete, or commit). Distinct from `DbOpenFailed` (8) which fires before any transaction begins. |
| 15 (new) | `AddExcludeWorkspaceLocked` | The PGLite data directory is already locked by another process; the bridge subprocess could not open it. |
| 16 (new) | `AddExcludePathIsFile` | One or more `--add-exclude` paths refers to a regular file, or has a known file-suffix and does not exist. |

The constant identifier name still carries the `WorkspaceMissingForInspection` text on disk so existing tests and callers continue to work; only the XML doc comment is broadened.

**Rationale**: Contiguous numbering keeps the catalogue simple to document. Reusing 6 for the workspace-missing case is consistent with FR-002's wording ("distinct, documented exit code") because 6 is already distinct from every other code; the spec does not require a *new* code, only a *distinct documented* one. New values 12–16 do not collide with existing ones and have no special meaning on Windows or POSIX (the conventional 128+signal range is well above 16).

**Alternatives considered**:
- *Allocate a brand-new code for workspace-missing in add-exclude mode*. Rejected: would force two near-identical codes the calling skill would have to OR together.
- *Pack lock-contention onto an existing code*. Rejected: the spec's clarification 2026-05-01 explicitly required a distinct contention exit code so the calling skill can branch on retry-vs-bail.

## R4 — Settings JSON atomicity and crash window

**Decision**: Write-temp-then-rename for `D2NET-Settings.json`, with the atomic rename happening **after** the Postgres transaction commits. The full sequence inside `AddExcludeRunner.Run` is:

1. Read current settings JSON. Compute the new excluded-directories list (existing ∪ new minus redundant duplicates).
2. Write the new JSON to a sibling temp file `D2NET-Settings.json.tmp` and `fsync` it.
3. Open the bridge, connect via Npgsql, `BEGIN`.
4. `INSERT INTO excluded_directories (path, kind) VALUES (@path, 'manual')` for each new path. Skip silently if the path already exists (`ON CONFLICT (path) DO NOTHING` to avoid PK violation when settings JSON and DB rows ever drift).
5. `DELETE FROM dart_files WHERE full_path LIKE @sourceDir || '/' || @path || '/%' OR full_path = @sourceDir || '/' || @path` — the boundary-aware prefix match. Capture `RowsAffected` per path for the run summary.
6. `COMMIT`.
7. Atomic rename `D2NET-Settings.json.tmp` → `D2NET-Settings.json`. On Windows use `File.Replace` (which is atomic enough for this purpose); on POSIX use `File.Move(overwrite: true)` which surfaces as `rename(2)`.
8. Bridge shutdown via `PgBridgeProcess.Dispose` (stages the staged shutdown from feature 005).

If step 6 fails: rollback transaction, delete temp JSON, exit code 14.
If step 7 fails: emit a stderr warning that the database was updated but the settings file was not; exit code 13. The user can recover by re-running add-exclude with the same arguments — step 4's `ON CONFLICT DO NOTHING` makes the second run a no-op for the database, and step 7 retries the JSON rename. If the rename keeps failing, the underlying filesystem issue must be diagnosed.

**Rationale**: The Postgres transaction is the source-of-truth boundary. Putting the rename after `COMMIT` guarantees the database is consistent before the JSON projection is updated. The narrow rename-failure window (between commit and rename) is documented and recoverable. Two-phase commit between JSON file IO and Postgres is overkill for a CLI tool.

**Alternatives considered**:
- *In-place rewrite with `fsync`*. Rejected: a process kill mid-write leaves a half-written JSON file that subsequent `--list` invocations would fail to parse.
- *Backup-and-restore wrapper*. Rejected: extra IO, more code paths, and the rename pattern already gives us the atomicity guarantee we need.
- *Rename JSON before commit*. Rejected: a commit failure after rename leaves the JSON ahead of the database — the JSON would claim exclusions that the database has not actually applied.

## R5 — Concurrent invocation lock detection

**Decision**: Detect contention at bridge startup. Process B's bridge subprocess will fail because PGLite cannot open the locked data directory; the bridge prints `BRIDGE_ERROR` with a payload that includes the substring `EBUSY`, `EACCES`, `data directory in use`, or the PGLite-specific lockfile message. `PgBridgeProcess.LastBridgeError` already exposes the verbatim message; the new `AddExcludeRunner` checks the message text against a stable substring set (`["EBUSY", "EACCES", "data directory in use", "could not lock", "another process"]`) and maps a hit to exit code 15 (`AddExcludeWorkspaceLocked`). Any other `BRIDGE_ERROR` payload still maps to the existing `BridgeStartFailed` (7) or `DbOpenFailed` (8) per the feature 005 contract.

**Rationale**: PGLite's lockfile is the canonical lock authority for the data directory; checking it is the correct semantic boundary. Detecting via the bridge's `BRIDGE_ERROR` payload reuses an existing diagnostic surface — no new side-channel locks (.NET file locks, sentinel files) are introduced. The pattern set is stable across PGLite minor versions because all of them emit one of those phrases when a lock contest fails.

**Alternatives considered**:
- *Acquire a .NET `FileStream(...FileShare.None)` lock on `.D2NET/D2NET-Settings.json` at the start of add-exclude*. Rejected: would solve the C#-side serialisation but not the underlying PGLite data-dir collision; a parallel inspection invocation could still trip the bridge.
- *Probe `postmaster.pid` directly inside the data dir from C#*. Rejected: cross-platform PID probing is brittle, and the bridge subprocess is the canonical entry point.

## R6 — `excluded_directories.kind` value for incremental adds

**Decision**: Insert with `kind = 'manual'`.

**Rationale**: Init-mode `--exclude <path>` uses `ExclusionKind.Manual` (verified in `ExclusionsWriter.ToKindText`). The semantics are identical: an operator-supplied directory exclusion. Using the same kind keeps the schema simple and means that `--Exclusions` continues to work unchanged. Introducing a new kind such as `'incremental'` would force a CHECK-constraint migration and provide no operational value — the `kind` column exists to distinguish *why* the exclusion was added (tool/pattern/manual) and an incremental add is just another flavour of manual.

**Alternatives considered**:
- *Add a new `'incremental'` kind*. Rejected: requires DDL migration and inspector changes for no observable user benefit.
- *Do not record the kind for incremental adds*. Rejected: violates the existing `NOT NULL` + `CHECK` constraint.
