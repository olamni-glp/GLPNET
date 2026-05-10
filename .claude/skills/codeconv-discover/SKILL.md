---
name: codeconv-discover
description: Walk `glp_runtime_net/` and inventory every `.dart` file into the `codeconv` schema + `.codeconv/tombstones/`. Use when the user types `/codeconv-discover` or asks for a Dart-source inventory, doc-comment scrape, or import/caller graph for the runtime.
---

# /codeconv-discover

Thin wrapper over `codeconv discover`. Forwards arguments verbatim.

## What this skill does

1. Resolve the codeconv venv (`codeconv/.venv/Scripts/python.exe` on Windows, `codeconv/.venv/bin/python` on POSIX). If absent, instruct Gabi to run `python -m venv codeconv/.venv && codeconv/.venv/Scripts/python.exe -m pip install -e codeconv[dev]` first.
2. Invoke `codeconv discover run <args verbatim>` from the repo root.
3. Show stdout/stderr from the run.

## Subcommand and flags

`/codeconv-discover [run] [flags]`

| Flag | Default | Effect |
|---|---|---|
| `--from-tombstones` | off | Reconstruct inventory from `.codeconv/tombstones/` only. No `.dart` source is read (FR-022). |
| `--root <path>` | `glp_runtime_net` | Override the discover subtree (testing). |
| `--quiet` | off | Suppress per-file logging. |
| `--json` | off | Emit JSON summary instead of human-readable. |
| `--dry-run` | off | Walk + parse but do not touch DB or tombstones. |
| `--no-orphan-revival` | off | Skip the FR-025 revival step (testing only). |

## Pre-execution checks

- The unified bridge daemon must be reachable. `codeconv discover` calls `acquire_or_discover` which auto-spawns the daemon if needed. The first invocation in a fresh repo pays a ~7 s PGLite cold-init penalty (memory `project_pglite_cold_init_windows.md`).
- Schema migrations must have run at least once. If `codeconv migrate` has never been invoked in this repo, the tool will fail with an "undefined table" error. Recommended preflight: `/codeconv-runner migrate`.

## What discover writes

| Target | Content |
|---|---|
| `codeconv.dart_files` | One row per inside-subtree `.dart` file (path, name, purpose, key_idea, mtime, sha256). |
| `codeconv.dart_imports` | Edges `(from_path, to_path)` for every in-subtree `import` directive (FR-019, deduped). |
| `codeconv.dart_callers` | Inverse view of `dart_imports` — inside-only (FR-023). |
| `codeconv.dart_files_orphaned` | Files that disappeared since the last run (FR-025). |
| `codeconv.discover_runs` | One row per invocation; `files_total`, `files_processed`, `files_skipped_idempotent`, `warnings`. |
| `.codeconv/tombstones/<rel>.dart.md` | Markdown + YAML frontmatter, one per inventoried file. Checked in. |
| `.codeconv/tombstones/.orphaned/<rel>.dart.md` | Tombstones for orphaned files. Checked in. |

## Idempotence + resume (SC-008 / SC-009 / FR-017)

- A re-run on unchanged source state produces zero diff in the schema and zero diff in tombstones; the per-file step short-circuits on `(mtime, sha256)` match.
- Killing a discover mid-flight and re-invoking the same command resumes — already-processed files are skipped via the same `(mtime, sha256)` short-circuit.

## Examples

- `/codeconv-discover` → walks `<repo>/glp_runtime_net/` and writes everything.
- `/codeconv-discover --json` → same, with a JSON summary instead of human-readable.
- `/codeconv-discover --from-tombstones` → drops + rebuilds `codeconv` schema rows from `.codeconv/tombstones/` only; SC-007 round-trip.
- `/codeconv-discover --dry-run --quiet` → walks + parses + computes would-be writes; no DB or tombstone changes.

## What this skill does NOT do

- Does NOT enrich `purpose`/`key_idea` semantically — they are populated mechanically only (FR-020). Engineer- or AI-curated semantic enrichment is reserved for a future codeconv-* tool.
- Does NOT translate Dart to C# / .NET — out of scope for this feature (FR-028).
- Does NOT record caller edges for `.dart` files OUTSIDE `glp_runtime_net/` — those are warned only, not edged (FR-023).

## Contract

`specs/012-codeconv-runner/contracts/codeconv_discover_cli.md` is the source of truth. This skill MUST stay in sync with that contract; if you change behavior here, update the contract first.
