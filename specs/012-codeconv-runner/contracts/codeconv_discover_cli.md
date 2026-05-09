# Contract: `/codeconv-discover` (and `codeconv discover`)

Source: spec FR-018, FR-019, FR-020, FR-021, FR-022, FR-023, FR-024, FR-025; clarifications Q7, Q9, Q12, Q13, Q14, Q15; research R7, R11, R12, R15.

## Invocation

Slash form: `/codeconv-discover [flags]` → CLI form: `codeconv discover [flags]`.

## Flags

| Flag | Type | Default | Semantics |
|---|---|---|---|
| `--from-tombstones` | bool | false | Reconstruct inventory from `.codeconv/tombstones/` only. No `.dart` source is read. Per FR-022. |
| `--root <path>` | path | `glp_runtime_net` (relative to repo root) | Override the discover subtree. Default matches FR-018; override only for testing. |
| `--quiet` | bool | false | Suppress per-file logging. |
| `--json` | bool | false | Emit JSON summary instead of human-readable. |
| `--dry-run` | bool | false | Walk, parse, compute would-be writes; do NOT touch DB or tombstones. Useful for CI. |
| `--no-orphan-revival` | bool | false | Skip the FR-025 revival step (testing only; default behavior is to revive). |

## Subtree scope (FR-018)

Discover walks `<root>/**/*.dart`. It MUST exclude:

- Any path containing a `.dart_tool/` segment.
- Any path containing a `build/` segment.
- Any file matching `*.g.dart`, `*.freezed.dart`, `*.gen.dart`.
- Symlinks pointing outside the subtree (treated as outside; not followed).

It MUST NOT process any `.dart` file outside `<root>` even if reachable through symlinks or relative imports.

## Steps (normal mode)

```
1. Acquire bridge endpoint (codeconv runner startup).
2. Open or create discover_runs row (mode='normal').
3. Walker phase:
   - Recursively enumerate `.dart` files under <root> with the exclusion rules.
   - Compute total file count → discover_runs.files_total.
4. Per-file DBOS step (workflow checkpoint per file):
   - Compute mtime + sha256.
   - Idempotence short-circuit: if dart_files row exists with matching (mtime, sha256), bump files_skipped_idempotent and continue.
   - Read leading doc-comment block (R11).
   - Parse import directives, resolve, filter to inside-subtree only (R12).
   - UPSERT codeconv.dart_files row.
   - Replace dart_imports rows for from_path = this file with the deduplicated set.
   - Replace dart_callers rows for to_path = this file BY recomputing from dart_imports.
     (Note: dart_callers is consistent only after all per-file steps complete; the per-file step
     writes the rows it KNOWS about; a final phase 5 reconciles.)
   - Write tombstone .codeconv/tombstones/<rel>.dart.md.
   - Bump files_processed.
5. Reconciliation phase (single DBOS step):
   - Recompute dart_callers from the now-complete dart_imports table (idempotent rewrite).
   - Find files in dart_files that are no longer present on disk → orphan: move to dart_files_orphaned, move tombstone to .codeconv/tombstones/.orphaned/.
   - Find files in dart_files_orphaned that are present on disk again → revive (FR-025): move row back, move tombstone back, refresh mtime + sha256, recompute edges.
   - Detect imports BY files OUTSIDE the subtree pointing INTO the subtree (FR-023): emit warnings; do NOT record edges.
   - Detect duplicate import directives (FR-019): emit warnings.
6. Mark discover_runs.completed_at = NOW(); summarise.
```

## Steps (`--from-tombstones` mode)

```
1. Acquire bridge endpoint.
2. Open or create discover_runs row (mode='from_tombstones').
3. TRUNCATE codeconv.dart_files, dart_imports, dart_callers (preserve dart_files_orphaned).
4. Walk .codeconv/tombstones/**/*.dart.md (excluding .orphaned/).
5. For each tombstone:
   - Parse YAML frontmatter (NOT the body).
   - INSERT codeconv.dart_files row from frontmatter fields.
   - INSERT codeconv.dart_imports rows from `dependencies` list.
   - INSERT codeconv.dart_callers rows from `callers` list.
6. For each tombstone in .codeconv/tombstones/.orphaned/:
   - INSERT codeconv.dart_files_orphaned row from frontmatter (if not already present).
7. Mark completed_at; summarise.

NO `.dart` source is read in this mode. The result MUST equal what a normal-mode run on the same source state would produce (SC-007).
```

## Stdout / output shape

Human-readable summary (default):

```
codeconv-discover: glp_runtime_net/ (root)
  walked:                  128 files
  processed:               112 files
  skipped (idempotent):     16 files
  imports recorded:        413 edges
  callers recorded:        413 edges (mirror)
  warnings:                  3
  orphaned this run:         0 files
  revived this run:          0 files
  duration:                42.1s

  warnings:
    - duplicate import 'foo.dart' in runtime/bar.dart (deduped)
    - outside-subtree caller: glp_runtime/legacy.dart imports glp_runtime_net/heap_fcp.dart (NOT recorded as caller edge)
```

JSON shape (`--json`):

```json
{
  "root": "glp_runtime_net",
  "mode": "normal",
  "files_walked": 128,
  "files_processed": 112,
  "files_skipped_idempotent": 16,
  "imports": 413,
  "callers": 413,
  "orphaned": 0,
  "revived": 0,
  "warnings": [
    {"kind": "duplicate_import", "file": "runtime/bar.dart", "import": "foo.dart"},
    {"kind": "outside_caller", "outside_file": "glp_runtime/legacy.dart", "inside_file": "runtime/heap_fcp.dart"}
  ],
  "duration_seconds": 42.1
}
```

## Exit codes

| Code | Meaning |
|---|---|
| 0 | Success (with or without warnings) |
| 1 | Generic error during workflow |
| 2 | Bridge unreachable |
| 65 | `--from-tombstones` mode encountered a malformed tombstone |
| 73 | All files skipped (per Edge Case: workflow exit code is non-zero only if all files were skipped) |

## Idempotence (SC-008)

A second run on an unchanged source state MUST:

- Produce zero diff in `codeconv.dart_files`, `dart_imports`, `dart_callers`.
- Produce zero diff in `.codeconv/tombstones/`.
- Take ≤ 5 s wallclock per SC-013.

The mechanism is the per-file (mtime, sha256) idempotence short-circuit in Step 4.

## Performance (SC-013)

- Fresh checkout (128 files): ≤ 60 s.
- Idempotent re-run: ≤ 5 s.
- Wide latency margin per R15. CI gate uses these as hard upper bounds.

## Resume after kill (SC-009 + FR-017)

DBOS provides workflow durability. Killing `/codeconv-discover` after N files have been processed and re-invoking the same command MUST resume from file N+1; files 1..N MUST NOT be re-processed (DBOS's per-step checkpointing). The reconciliation phase (Step 5) re-runs from scratch on resume — it is internally idempotent.
