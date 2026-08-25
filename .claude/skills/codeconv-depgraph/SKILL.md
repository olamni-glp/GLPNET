---
name: codeconv-depgraph
description: Compute the topologically-ordered Dart dependency graph + conversion-readiness oracle, and record/stamp/rebuild per-file conversion state. Use when the user types `/codeconv-depgraph` or asks "what should I convert next?", to mark a file's conversion started/completed, or to stamp/rebuild depgraph + conversion state through tombstones.
---

# /codeconv-depgraph

Thin wrapper over `codeconv depgraph`. Forwards arguments verbatim.

## What this skill does

1. Resolve the codeconv venv (`codeconv/.venv/Scripts/python.exe` on Windows, `codeconv/.venv/bin/python` on POSIX). If absent, instruct Gabi to run `python -m venv codeconv/.venv && codeconv/.venv/Scripts/python.exe -m pip install -e codeconv[dev]` first.
2. Invoke `codeconv depgraph <args verbatim>` from the repo root.
3. Show stdout/stderr from the run.

## Subcommands and flags

`/codeconv-depgraph [subcommand] [flags]`

| Subcommand | Purpose |
|---|---|
| `compute` (default — bare `/codeconv-depgraph` runs it) | Read `dart_files` + `dart_imports` + `dart_conversions`, compute topo order + SCC condensation + `status`, write `codeconv.dart_depgraph` + `.codeconv/depgraph.json` (atomic-per-run). |
| `mark-started <path>` | Record that conversion of `<path>` has started. |
| `mark-completed <path>` | Record that conversion of `<path>` has completed. |
| `stamp-tombstones` | Embed the six depgraph/conversion keys into every tombstone's YAML frontmatter. |
| `rebuild-conversions-from-tombstones` | Inverse of stamp — repopulate `codeconv.dart_conversions` from tombstone YAML. |

| Flag | Applies to | Default | Effect |
|---|---|---|---|
| `--json-out <path>` | compute | `<repo>/.codeconv/depgraph.json` | Override the JSON artefact path. |
| `--dry-run` | compute / stamp / rebuild | off | Compute everything; write nothing (no DB, no JSON, no tombstones). |
| `--sha256 <hex>` | mark-started | (auto from `dart_files.sha256`) | Override the recorded sha256-at-start. |
| `--target <path>` | mark-completed | NULL | Record the produced C# / .NET artefact path (write-once per FR-006a). |
| `--no-tombstone-update` | mark-started / mark-completed | off | Skip the tombstone YAML update (testing only). |
| `--quiet` | all | off | Suppress per-step logging. |
| `--json` | all | off | Emit a JSON summary on stdout. |
| `--data-dir <path>` | all (top-level) | `<repo>/.pgdb` | Override the PGLite cluster — **the canonical repo-local cluster (checkout is NTFS)**: pass `--data-dir D:/bstdev/research/glp/glpnet/.pgdb`. |

## Pre-execution checks

- The unified bridge daemon must be reachable. `codeconv depgraph` calls `acquire_or_discover` which auto-spawns it; the first invocation in a fresh repo pays a ~7 s PGLite cold-init penalty (memory `project_pglite_cold_init_windows.md`).
- Schema migrations must have run at least once (`/codeconv-runner migrate`).
- `compute` requires a populated inventory — run `/codeconv-discover` first. Empty inventory ⇒ exit 2.
- `stamp-tombstones` requires a prior `compute` (non-empty `dart_depgraph`) ⇒ else exit 2.

## What depgraph writes

| Target | Content |
|---|---|
| `codeconv.dart_depgraph` | One row per inventoried file: `topo_level`, `cycle_group_id`, `ready`, `status`, counts (atomic-per-run, FR-008). |
| `codeconv.dart_conversions` | Two-phase per-file conversion state (`started_at`, `completed_at`, `sha256_of_dart_at_start`, `target_path`). |
| `codeconv.depgraph_runs` | One row per invocation (mode, metrics). |
| `.codeconv/depgraph.json` | Local "what should I convert next?" artefact (gitignored, recomputable, R10). |
| `.codeconv/tombstones/<rel>.dart.md` | `stamp-tombstones` embeds the six feature-015 keys; `mark-*` update the conversion keys. |

## Status lifecycle (FR-006)

`pending` → `ready` (computed: all SCC-external deps `converted`) ; `mark-started` → `in_progress` ; `mark-completed` → `converted`. SCC members advance as a batch. `mark-*` does NOT auto-recompute — run `compute` again to see updated `status` (R2).

## Idempotence (SC-002)

- A re-`compute` on unchanged inventory + conversions is byte-identical modulo `metadata.generated_at`.
- A re-`stamp-tombstones` on unchanged DB state produces zero tombstone diff.
- `--dry-run` writes nothing.

## Examples

- `/codeconv-depgraph` → compute; write `.codeconv/depgraph.json` + `codeconv.dart_depgraph`.
- `/codeconv-depgraph mark-started lib/runtime/heap_fcp.dart` → record start.
- `/codeconv-depgraph mark-completed lib/runtime/heap_fcp.dart --target out/HeapFcp.cs` → record completion + target.
- `/codeconv-depgraph stamp-tombstones` → embed depgraph + conversion state into every tombstone.
- `/codeconv-depgraph rebuild-conversions-from-tombstones` → restore `dart_conversions` from tombstones after a DB wipe.

## What this skill does NOT do

- Does NOT translate Dart to C# / .NET (out of scope).
- Does NOT modify `dart_files`, `dart_imports`, `dart_callers`, `dart_files_orphaned`, or `discover_runs` (FR-011).
- Does NOT auto-recompute after `mark-*` (R2).

## Contract

`specs/015-codeconv-depgraph/contracts/depgraph_cli.md` is the source of truth. This skill MUST stay in sync with that contract; if you change behavior here, update the contract first.
