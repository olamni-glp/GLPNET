# Data Model: 014-package-self-import-resolution

## TL;DR — no schema change, no new entities

This feature **does not introduce any new tables, columns, indexes, on-disk artefacts, tombstone fields, sidecar JSON shapes, or migration steps**. The data model from `specs/012-codeconv-runner/data-model.md` (sections 1.1 through 4.5, plus § 7's D2NET schema audit) is preserved verbatim.

This document exists so the cross-artefact analyzer (`/speckit-analyze`) can verify the spec's "Key Entities (no new entities introduced)" claim against the Phase 1 design — i.e. confirm there is genuinely nothing here to model.

## Diff against feature 012's data model

| Section of `specs/012-codeconv-runner/data-model.md` | This feature changes anything? | Note |
|---|---|---|
| 1.1 `codeconv.dart_files` (columns: path, name, purpose, key_idea, mtime, sha256, discovered_at) | NO | Per-row content unchanged. Same columns, same types, same primary key. |
| 1.2 `codeconv.dart_imports` (columns: from_path, to_path; UNIQUE (from_path, to_path) per FR-019) | **Content only**, not shape | Row count rises (~146 → ~400-600 expected on `glp_runtime_net/`); same column types; same uniqueness constraint. The uniqueness constraint already covers FR-007's collapse-package-and-relative-form rule by construction. |
| 1.3 `codeconv.dart_callers` | **Content only**, not shape | Same as 1.2 — denormalised inverse. |
| 1.4 `codeconv.dart_files_orphaned` | NO | No file-level lifecycle change in this feature. |
| 1.5 `codeconv.discover_runs` (columns: id, started_at, completed_at, mode, files_total, files_processed, files_skipped_idempotent, warnings) | NO | The new `pubspec_missing` warning is appended into the existing `warnings jsonb` column — no new column. |
| 2 `.pgdb/bridge.json` | NO | Bridge lifecycle untouched. |
| 3 `.pgdb/.migration-record.json` | NO | Migration tool untouched. |
| 4.1 Tombstone YAML frontmatter (path, name, purpose, key_idea, dependencies, callers, mtime, sha256) | **Content only**, not shape | `dependencies` and `callers` lists become longer for files that use `package:glp_runtime/...` form; same field set; same field order; same YAML block scalar style. |
| 4.4 `.codeconv/tombstones/.orphaned/...` | NO | Orphan tree untouched. |
| 4.5 `.pgdb.bridge.lock/` | NO | Bridge lock untouched. |
| 5 State transitions | NO | Same transitions; no new states. |
| 7 D2NET schemas (`public.{setting, excluded_directories, dart_files, phase_sequence, phase_status}`) | NO | D2NET schemas untouched per FR-015 (carried forward). |

## Why no schema change is needed

The feature changes how `extract_imports` *populates* `dart_imports.to_path` (and transitively how `_backfill_tombstone_callers` populates the `callers` field), but it does NOT change the path encoding. Per spec FR-009: resolved package-form imports MUST appear as `lib/<rest>` — exactly the same POSIX path shape that relative-path-resolved imports already produce today. Downstream consumers cannot tell from the on-disk form whether a row's `to_path` came from a `package:glp_runtime/` rewrite or from a relative `../runtime/foo.dart` import. The data-model contract is unchanged.

## What is NOT in this document and why

- **The new `pubspec_missing` warning shape** lives in `research.md` § R16 and `contracts/workflow_contract.md`. It is a workflow-summary warning, not a persisted entity, so it has no place here.
- **The `extract_imports` signature change** lives in `contracts/parser_contract.md`. It is an internal Python API surface, not a data model.
- **The `pubspec.py` module's `read_package_name` return shape** lives in `research.md` § R15 and `contracts/workflow_contract.md`. Internal module API, not persisted state.

## Verification

After this feature lands and `/codeconv-discover` is re-run on `glp_runtime_net/`:

- `\d codeconv.dart_files`, `\d codeconv.dart_imports`, `\d codeconv.dart_callers`, `\d codeconv.dart_files_orphaned`, `\d codeconv.discover_runs` against the live PGLite cluster MUST produce byte-identical output to the same commands run before this feature lands. Tested by Flow G of `quickstart.md`.
- A diff of `.codeconv/tombstones/<file>.dart.md` frontmatter field-set (i.e. the YAML keys, not the values) against the pre-feature snapshot MUST be empty. The tombstone-refresh commit changes VALUES of `dependencies` / `callers` lists; it does NOT change the frontmatter schema.
