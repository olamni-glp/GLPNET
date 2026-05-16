# Contract: `codeconv depgraph` CLI surface

This document specifies the Typer-app CLI surface of the new `codeconv depgraph` tool, the thin-slash-wrapper relationship to `.claude/skills/codeconv-depgraph/SKILL.md`, and the JSON output shape. The implementation in `codeconv/src/codeconv/tools/depgraph/__init__.py` and `workflow.py` follows it exactly.

## Source of truth references

- Spec FRs covered: FR-001 (tool registration), FR-002 (slash wrapper), FR-006a (mark subcommands), FR-007 (JSON artefact), FR-008 (atomic-per-run write), FR-009 (idempotence), FR-010 (empty-inventory error), FR-011 (write surface restriction), FR-012 (--json / --quiet / --dry-run), FR-013 (--repo-root / --data-dir / --json-out), FR-014 (stamp-tombstones)
- Research notes: R2 (no auto-recompute), R3 (rebuild surface), R4 (schema_version)
- Feature 012 contract: `specs/012-codeconv-runner/contracts/codeconv_tool_contract.md` — tool subpackage MUST export `app: typer.Typer` and optionally `register_workflows(dbos_app)`.

## CLI command tree

```text
codeconv depgraph                                      # alias for `compute` (FR-007 default behaviour)
codeconv depgraph compute [flags]
codeconv depgraph mark-started <path> [--sha256 <hex>] [flags]
codeconv depgraph mark-completed <path> [--target <path>] [flags]
codeconv depgraph stamp-tombstones [flags]
codeconv depgraph rebuild-conversions-from-tombstones [flags]
```

The subcommand list is fixed by FR-006a, FR-014, and R3. No other subcommands are added in v1.

## Top-level flags inherited from `codeconv`

These come from the `codeconv` console-script entry point (feature 012 FR-013) and propagate via `typer.Context`:

| Flag | Source | Default | Effect |
|---|---|---|---|
| `--repo-root <path>` | `codeconv/src/codeconv/cli.py` | `Path.cwd()` | Override the repo root. |
| `--data-dir <path>` | `codeconv/src/codeconv/cli.py` | `<repo-root>/.pgdb` | Override the PGLite cluster location (mandatory on D: per `project_012_codeconv_runner_status.md`). |
| `--quiet` | top-level | off | Suppress per-step logging across all subcommands. |
| `--json` | top-level | off | Emit JSON summary on stdout (in addition to side-effecting writes). |

## Per-subcommand flags

### `compute`

| Flag | Default | Effect |
|---|---|---|
| `--json-out <path>` | `<repo-root>/.codeconv/depgraph.json` | Override the JSON artefact path. |
| `--dry-run` | off | Compute everything; write nothing. No DB writes, no JSON file write, no tombstone writes. |
| `--quiet` | off | Suppress per-file logging on stdout (still emits the final summary). |
| `--json` | off | Emit JSON summary on stdout in lieu of human-readable. |

**Behaviour**:

1. Acquire-or-discover the bridge daemon (feature 012 `bridge_client.acquire_or_discover`).
2. Read `codeconv.dart_files` (all paths) and `codeconv.dart_imports` (all edges).
3. If `dart_files` is empty: exit 2 (per FR-010), UNCONDITIONALLY — including under `--json`. In human mode print the error `"No inventoried files. Run /codeconv-discover first."` to stderr. In `--json` mode emit the machine-readable error object `{"ok": false, "exit_code": 2, "error": "No inventoried files. Run /codeconv-discover first.", "files_total": 0}` on stdout AND set the PROCESS exit code to 2 (the `exit_code` field inside the JSON does NOT replace the process exit status — a prior bug returned process-exit 0 here; that is a contract violation).
4. Read `codeconv.dart_conversions` (all rows; may be empty).
4a. REFERENTIAL COMPLETENESS (Amendment v3 — option A′): `dart_imports` is a
    faithful record of the source's import directives and MAY contain a
    dangling edge — one whose `from_path` or `to_path` is not in the
    `dart_files` node set (an in-subtree import of a file that does not yet
    exist; see `specs/012-codeconv-runner/contracts/codeconv_discover_cli.md`
    § Steps (normal mode) step 5). `algorithm.compute` contractually raises
    `ValueError` on a dangling endpoint (`contracts/depgraph_algorithm.md`
    § Algorithm step 2, test obligation 8). Therefore compute MUST filter
    `edges` to those whose BOTH endpoints are inventoried nodes BEFORE calling
    `algorithm.compute`. The number filtered is reported as
    `dangling_edges_dropped` in the summary and JSON `metadata`. This is
    non-destructive (no DB mutation) and self-healing: once the missing target
    is inventoried by a later discover, the edge re-enters the graph
    automatically with no importer edit.
5. Call `algorithm.compute(nodes, edges)` to get `DepgraphResult` (edges here
   are the referentially-complete subset from step 4a).
6. For each path: compute `status` from `(cycle_group_id, dependencies, dart_conversions)` per FR-006.
7. If not `--dry-run`: open a transaction; INSERT into `codeconv.depgraph_runs` (mode='compute', started_at=NOW(), ...); `DELETE FROM codeconv.dart_depgraph`; bulk INSERT 128 rows; UPDATE `depgraph_runs` set completed_at=NOW() and metric columns; COMMIT.
8. If not `--dry-run`: write `.codeconv/depgraph.json` (atomic via temp-file rename).
9. Emit summary to stdout (`--json` shape or human-readable per `--json` flag).
10. Exit 0 on success.

**Exit codes**:

- `0` — success.
- `2` — empty inventory (FR-010).
- `1` — any other unexpected error.

### `mark-started <path>`

| Flag | Default | Effect |
|---|---|---|
| `--sha256 <hex>` | (auto from `dart_files.sha256`) | Override the recorded sha256-at-start. |
| `--no-tombstone-update` | off | Skip the tombstone YAML update (testing only). |

**Behaviour** (per FR-006a + research R2):

1. Acquire-or-discover the bridge.
2. Validate `path` exists in `codeconv.dart_files`. If not: print error to stderr; exit 2.
3. SELECT existing row from `codeconv.dart_conversions WHERE path = ?`. If present:
   - If `completed_at IS NULL`: print warning "already started" to stderr; exit 0 (idempotent).
   - If `completed_at IS NOT NULL`: print warning "already completed" to stderr; exit 0 (idempotent).
4. Else INSERT row: `(path, started_at=NOW(), sha256_of_dart_at_start=<arg or dart_files.sha256>, completed_at=NULL, target_path=NULL, marked_started_run_id=<this run>)`.
5. INSERT into `depgraph_runs` (mode='mark-started', ...).
6. If not `--no-tombstone-update`: read the corresponding `.codeconv/tombstones/<path>.md`, update the `conversion_started_at` YAML key, write back. The same canonical YAML emitter as feature 012 is used (no idempotence regression).
7. Exit 0.

**Does NOT auto-recompute `dart_depgraph`** (R2). The user runs `codeconv depgraph compute` separately to see updated `status`.

### `mark-completed <path>`

| Flag | Default | Effect |
|---|---|---|
| `--target <path>` | NULL | Record the produced C# / .NET artefact path. |
| `--no-tombstone-update` | off | Skip the tombstone YAML update (testing only). |

**Behaviour**:

1. Acquire-or-discover the bridge.
2. Validate `path` exists in `codeconv.dart_files`. If not: print error; exit 2.
3. SELECT existing row from `dart_conversions WHERE path = ?`.
   - If absent: print error "must call mark-started first"; exit 2.
   - If present and `completed_at IS NOT NULL`: print warning "already completed"; exit 0 (idempotent).
4. UPDATE the row: `completed_at = NOW()`, `target_path = <arg or NULL>`, `marked_completed_run_id = <this run>`.
5. INSERT into `depgraph_runs` (mode='mark-completed', ...).
6. If not `--no-tombstone-update`: update tombstone YAML `conversion_completed_at` (and `target_path` if provided).
7. Exit 0.

### `stamp-tombstones`

| Flag | Default | Effect |
|---|---|---|
| `--dry-run` | off | Compute would-be tombstone updates; write nothing. |
| `--quiet` | off | Suppress per-file logging. |

**Behaviour** (FR-014):

1. Acquire-or-discover the bridge.
2. Read `dart_depgraph` (must be non-empty; if empty, print error "run compute first"; exit 2).
3. Read `dart_conversions`.
4. For each file: read existing `.codeconv/tombstones/<path>.md`; update the six new YAML keys (`topo_level`, `cycle_group_id`, `status`, `conversion_started_at`, `conversion_completed_at`, `target_path`) with the current values; write back. (`target_path` from `dart_conversions.target_path`; emitted YAML-null when the row exists but the column is NULL, absent when there is no `dart_conversions` row.)
5. INSERT into `depgraph_runs` (mode='stamp-tombstones', ...).
6. Exit 0.

**Idempotence**: re-running on unchanged source state produces zero diff in the tombstones (the canonical YAML writer guarantees this).

### `rebuild-conversions-from-tombstones`

| Flag | Default | Effect |
|---|---|---|
| `--dry-run` | off | Compute would-be DB writes; write nothing. |
| `--quiet` | off | Suppress per-file logging. |

**Behaviour** (R3):

1. Acquire-or-discover the bridge.
2. Walk `.codeconv/tombstones/` recursively (skipping `.orphaned/`); for each `<rel>.dart.md`, read frontmatter.
3. If `conversion_started_at` is missing or YAML-null: skip (no conversion record).
4. Else INSERT … ON CONFLICT (path) DO UPDATE … the row into `dart_conversions` with:
   - `path = <rel>.dart`
   - `started_at = <conversion_started_at from YAML>`
   - `completed_at = <conversion_completed_at from YAML>` (may be NULL)
   - `sha256_of_dart_at_start` — see § "sha256 round-trip"
   - `target_path = <target_path from YAML, if present>`
5. INSERT into `depgraph_runs` (mode='rebuild-conversions-from-tombstones', ...).
6. Exit 0.

**sha256 round-trip caveat**: the YAML frontmatter does not (today) carry `sha256_of_dart_at_start`. On rebuild, this column is populated from the CURRENT `dart_files.sha256` for that path. If the file has been edited since `mark-started`, the snapshot at-start is lost. A future feature may add a `sha256_at_start` YAML key for full round-trip; v1 deliberately ships without it, with the trade-off documented in `data-model.md` § 1.2.

## JSON output shape (FR-007 / R4)

`.codeconv/depgraph.json` schema (`"schema_version": 1`):

```json
{
  "schema_version": 1,
  "metadata": {
    "generated_at": "2026-05-11T14:32:08Z",
    "inventory_files_total": 128,
    "inventory_edges_total": 443,
    "ready_count": 6,
    "in_progress_count": 0,
    "converted_count": 0,
    "cycle_count": 0,
    "last_discover_run_id": "8a3f2e9c-..."
  },
  "ready": [
    "lib/runtime/heap_fcp.dart",
    "lib/runtime/terms.dart",
    "..."
  ],
  "files": [
    {
      "path": "lib/runtime/abandon.dart",
      "topo_level": 2,
      "cycle_group_id": 17,
      "depends_on": ["lib/runtime/terms.dart"],
      "depended_on_by": ["lib/runtime/commit.dart"],
      "ready": false,
      "status": "pending",
      "conversion_started_at": null,
      "conversion_completed_at": null,
      "target_path": null
    },
    ...
  ]
}
```

**Key ordering**: top-level keys appear in the order shown (`schema_version`, `metadata`, `ready`, `files`). Inside `metadata` and inside each `files[]` row, keys are sorted alphabetically by the JSON emitter (per R8). The `ready` array and the `depends_on` / `depended_on_by` arrays are sorted lexicographically. The `files` array is sorted by `(topo_level ASC, cycle_group_id ASC, path ASC)` per R1 rule 5.

## Slash skill wrapper

`.claude/skills/codeconv-depgraph/SKILL.md` is structurally copied from `.claude/skills/codeconv-discover/SKILL.md`. It MUST:

1. Resolve the codeconv venv (`codeconv/.venv/Scripts/python.exe` on Windows; `codeconv/.venv/bin/python` on POSIX).
2. Invoke `codeconv depgraph <args verbatim>` from the repo root.
3. Pass stdout/stderr through unchanged.

The skill defines no business logic; the CLI is the authoritative surface. Adding flags requires editing the Typer app, NOT the skill markdown.

## What this CLI does NOT do

- Does NOT convert `.dart` to C# / .NET (out of scope per spec line 142).
- Does NOT modify `dart_files`, `dart_imports`, `dart_callers`, `dart_files_orphaned`, or `discover_runs` (FR-011).
- Does NOT auto-trigger after `mark-*` (R2). Auto-recompute is a future enhancement.
- Does NOT widen the in-subtree edge filter (FR-019 / FR-023 carry-forward).
