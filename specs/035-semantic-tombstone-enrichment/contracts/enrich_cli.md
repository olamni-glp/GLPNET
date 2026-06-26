# Contract: `codeconv enrich` CLI

**Feature**: 035 | Subpackage: `codeconv/src/codeconv/tools/enrich/`

## Discovery
- Exports `app: typer.Typer` and `register_workflows(dbos_app)` (no-op
  placeholder). Auto-discovered by `tool_registry()` (`runner.py:85-133`); no
  edits to `runner.py`/`cli.py` (FR-016 zero-edit registry).
- `__all__ = ["app", "register_workflows"]`.

## Command: `codeconv enrich run`

| Option | Type | Default | Meaning |
|---|---|---|---|
| `--path <glob/prefix>` | str (repeatable) | all | Scope to candidates under this path (FR-012) |
| `--dry-run` | flag | false | Compute candidates/inferences; write nothing |
| `--json` | flag | false | Emit the §6 run-summary JSON to stdout |
| `--quiet` | flag | false | Suppress progress logging |
| `--data-dir <path>` | path | repo `.pgdb` | Bridge data-dir passthrough (matches discover/depgraph) |

## Behavior (maps to FRs)
1. Acquire bridge + engine via `acquire_or_discover(repo_root,
   ready_timeout=30.0, data_dir=…)` + `build_engine(endpoint)` — the **shared**
   bridge, no second consumer (FR + Constitution VI-b).
2. Enumerate candidates: in-scope, non-orphan, `purpose`/`key_idea` blank
   (`*_source == absent`), honoring `--path` (FR-001/012/013).
3. For each candidate: read the **current** Dart source; if tombstone `sha256`
   ≠ current file hash → skip-and-warn (stale; FR-007 edge). Else call
   `infer_fn` (R-004 seam).
4. On grounded result: write `purpose`/`key_idea` + `*_source: inferred` to
   **both** the tombstone (via `write_tombstone`, preserving `_FIELD_ORDER`)
   and the `dart_files` row, in one per-file transaction (FR-002/004/015).
5. On `grounded == False` / empty / over-long → `low_confidence`: tombstone
   unchanged, reason recorded (FR-009). On exception → `failed`: tombstone
   unchanged, reason recorded; continue with other files (FR-010).
6. Stamp provenance (`doc`/`absent`) into in-scope non-candidate tombstones
   without altering their `purpose`/`key_idea` text (research R-008; keeps
   markdown⇔DB agreement, FR-004; does not violate FR-006).
7. Emit the run summary (FR-011) and a durable run log.

## Exit codes
- `0` — run completed (including runs with per-file failures; failures are in
  the report, FR-010).
- `2` — **no Claude-backed `infer_fn` injected** (bare CLI invocation): print
  the "drive me through the `/codeconv-enrich` skill" message and exit 2.
  Mirrors `codegen_opt/__init__.py:120-129`. NO external-API fallback.
- Non-zero (other) — bridge/migration unavailable (pre-flight failure), no
  tombstone mutated.

## Invariants
- `--dry-run` mutates nothing (no tombstone bytes, no DB rows).
- A no-source-change re-run performs **zero** `infer_fn` calls and leaves the
  tombstone set byte-identical (SC-002).
- Never enriches `tombstones/.orphaned/` (FR-013).
