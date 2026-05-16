# Contract: `/codeconv-discover` (and `codeconv discover`)

Source: spec FR-018, FR-019, FR-020, FR-021, FR-022, FR-023, FR-024, FR-025; clarifications Q7, Q9, Q12, Q13, Q14, Q15; research R7, R11, R12, R15.

## Invocation

Slash form: `/codeconv-discover [flags]` → CLI form: `codeconv discover [flags]`.

## Flags

| Flag | Type | Default | Semantics |
|---|---|---|---|
| `--from-tombstones` | bool | false | Reconstruct inventory from `.codeconv/tombstones/` only. No `.dart` source is read (FR-022; no-source guarantee scoped to this mode). Mutually exclusive with `--verify-tombstones`. |
| `--verify-tombstones` | bool | false | Read-only source-truth audit (feature-015 reconciliation). Reads `.dart` sources; NO DB writes, NO tombstone rewrites; bridge NOT required (delayed/skipped acquisition). Mutually exclusive with `--from-tombstones`. |
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
1. Walk .codeconv/tombstones/**/*.dart.md (excluding .orphaned/) → tombstone set T.
2. PARSE + STRUCTURAL-VALIDATE — BEFORE any bridge acquisition, before the
   discover_runs row, before any DB/migrate path (so a bad input leaves the
   inventory untouched):
   a. Parse every tombstone's YAML frontmatter (NOT the body). Any unparseable
      tombstone, OR a tombstone missing/!type-valid on a REQUIRED feature-012
      field → ABORT with an actionable stderr message naming the file(s);
      exit 65; ZERO inventory mutation. [hard-fail]
   b. An optional feature-015 key (topo_level, cycle_group_id, status,
      conversion_started_at, conversion_completed_at, target_path) that is
      present-but-type-invalid → record a warning and ingest the base
      inventory row anyway with that one field treated as null. [warn; the
      base 012 row is NEVER dropped for a bad optional 015 field]
3. REFERENTIAL COMPLETENESS (still before any mutation): build the full path
   set of T. Any path appearing in some tombstone's `dependencies`/`callers`
   with no tombstone in T → the offending EDGE is dropped and counted as a
   warning ("missing tombstone: <path> referenced by <referrer>"). Documented
   SC-007 caveat: dropped dangling edges are an accepted, warned divergence;
   the dart_files/dart_imports/dart_callers content otherwise matches a
   normal-mode run.
4. Acquire bridge endpoint; open discover_runs row (mode='from_tombstones').
5. Reconcile in ONE transaction (atomic-per-run; a crash/error rolls the whole
   thing back — inventory never left half-wiped):
   - dart_imports, dart_callers: TRUNCATE then bulk-reinsert from T (these
     have no inbound FK).
   - dart_files: UPSERT each path from T (`INSERT … ON CONFLICT (path) DO
     UPDATE`), then DELETE only dart_files rows whose path is absent from T.
     dart_files is NOT blanket-TRUNCATEd, so feature-015's
     dart_conversions / dart_depgraph (FK → dart_files ON DELETE CASCADE) are
     PRESERVED for every surviving file. A path genuinely absent from T is a
     vanished file; its DELETE cascades its conversion/depgraph row away,
     which is correct. (Option B — chosen over a blanket TRUNCATE precisely
     so a from-tombstones inventory rebuild does not destroy conversion
     progress; depgraph is recomputed by `codeconv depgraph compute` anyway.)
6. For each tombstone in .codeconv/tombstones/.orphaned/:
   - UPSERT codeconv.dart_files_orphaned row from frontmatter.
7. Mark completed_at; summarise (warnings include type-invalid-015-field and
   dropped-dangling-edge entries).

NO `.dart` source is read in `--from-tombstones` mode (FR-022; this no-source
guarantee is scoped to THIS mode only — contrast `--verify-tombstones` below).
The rebuilt dart_files / dart_imports / dart_callers MUST equal what a
normal-mode run on the same source state would produce (SC-007), modulo the
documented dropped-dangling-edge warnings. dart_conversions / dart_depgraph are
out of SC-007 scope and are preserved across the rebuild, not recomputed by
discover.
```

## Steps (`--verify-tombstones` mode)

`codeconv discover --verify-tombstones` is a READ-ONLY source-truth audit added
for the feature-015 reconciliation. It DOES read `.dart` sources (this is the
operation that needs source truth; the FR-022 "no `.dart`" guarantee applies
only to `--from-tombstones`, never here). NO DB writes, NO tombstone rewrites.
The bridge is NOT required: bridge acquisition is delayed past mode dispatch and
skipped entirely for this flag (a CLI change from "acquire before dispatch").
Mutually exclusive with `--from-tombstones`.

```
1. Walk .codeconv/tombstones/**/*.dart.md, SKIPPING .codeconv/tombstones/.orphaned/
   (intentional orphans must not be reported as "missing source"). → set T.
2. Parse every tombstone's YAML. Any unparseable / format-invalid tombstone →
   abort, exit 65 (same code + now-broadened table wording as
   `--from-tombstones`).
3. Build the FULL in-subtree import graph ONCE by scanning all present `.dart`
   sources (callers are the inverse of the whole import graph — they cannot be
   verified per-file in isolation), then for each tombstone whose `.dart`
   source is present: recompute sha256 and compare recorded sha256 + the
   tombstone's `dependencies`/`callers` against the freshly-derived values.
   Any mismatch → warning ("stale tombstone: <path> differs from source
   (<fields>)"); continue. [warn-and-continue]
4. Filesystem reconciliation warnings (no abort):
   - tombstone in T whose `.dart` source is absent on disk → "missing source: <path>".
   - `.dart` under the subtree with no tombstone in T → "missing tombstone: <path>".
5. If ZERO `.dart` sources exist under the subtree → exit 1 with an actionable
   stderr message (verification needs sources; exit 1 = generic precondition
   error, deliberately NOT 2 which means "bridge unreachable"). Otherwise exit
   0 even when warnings were emitted.
6. Summarise: counts {verified_clean, stale, missing_source, missing_tombstone}
   + the warnings list (`--json` mirrors the discover summary shape).
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
| 1 | Generic error during workflow; ALSO `--verify-tombstones` with zero `.dart` sources under the subtree (precondition failure — deliberately not 2) |
| 2 | Bridge unreachable |
| 65 | `--from-tombstones` OR `--verify-tombstones` mode encountered a malformed / unparseable / required-field-invalid tombstone (abort before any inventory mutation) |
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
