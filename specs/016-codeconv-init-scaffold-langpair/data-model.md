# Data Model: 016-codeconv-init-scaffold-langpair

## TL;DR

One new Alembic migration (`0003`) introduces **four tables in the `codeconv` schema** (the de-branded D2NET workspace tables). **No `public.*` table** is created; `public.dart_files` / `public.scaffold_tracker` are deliberately not recreated. No change to any feature-012/014/015 table or to the tombstone field set — scaffold reuses the existing feature-015 tombstone `target_path`. The language-pair is an in-memory plugin entity (no table); the *selected* pair is persisted as rows in `workspace_settings`.

## 1. New tables (all under `codeconv` schema — migration `0003_d2net_into_codeconv.py`, `down_revision="0002"`)

### 1.1 `codeconv.workspace_settings` — flat key/value workspace configuration

(De-brand of D2NET `public.setting`; flat key/value shape preserved to keep the migration a rename, not a redesign.)

| Column | Type | Constraints | Notes |
|---|---|---|---|
| `key` | text | PRIMARY KEY | |
| `value` | text | NULL | |

**Well-known keys** (the single authority for "which pair is this workspace bound to" — FR-004/FR-006):

| Key | Meaning |
|---|---|
| `source_lang` | selected source language id (e.g. `dart`) |
| `target_lang` | selected target language id (e.g. `csharp`) |
| `source_path` | repo-relative source subtree (e.g. `glp_runtime_net`) |
| `target_path` | repo-relative target tree root |
| `bridge_port` / other options | optional tool options (carried from D2NET settings) |

`(source_lang, target_lang)` MUST resolve to a registered pair (FR-005); the row pair is written once at `init` and read (never silently rewritten) by every stage (FR-004).

### 1.2 `codeconv.excluded_directories` — in-scope boundary

| Column | Type | Constraints | Notes |
|---|---|---|---|
| `path` | text | PRIMARY KEY | repo/source-relative POSIX directory |
| `kind` | text | NOT NULL CHECK (kind IN ('tool','pattern','manual')) | `tool` = from the pair's `tool_exclusion_globs()`; `manual` = user-added |

Defines the file set discover/scaffold treat as in-scope (FR-007/FR-011).

### 1.3 `codeconv.phase_sequence` — conversion-phase ordering

| Column | Type | Constraints | Notes |
|---|---|---|---|
| `phase` | text | PRIMARY KEY | e.g. `discover`, `scaffold` |
| `sequence` | integer | NOT NULL | ordering |

### 1.4 `codeconv.phase_status` — per-phase progress

| Column | Type | Constraints | Notes |
|---|---|---|---|
| `phase` | text | PRIMARY KEY | |
| `status` | text | NOT NULL | e.g. `PENDING`, `IN_PROGRESS`, `COMPLETE` |
| `last_updated` | timestamptz | NOT NULL DEFAULT NOW() | |

`init` seeds `phase_sequence`/`phase_status`; `scaffold` upserts `phase='scaffold'` (FR-008/FR-016).

**Migration contract**: `upgrade()` = four `CREATE TABLE IF NOT EXISTS` (+ the two CHECKs). `downgrade()` = four `DROP TABLE IF EXISTS … CASCADE` reverse order. No data migration (legacy `public.*` not consulted — D1/R6). Idempotent; isolation-safe (SC verification: `\dt codeconv.*` gains exactly these four; `public`/`dbos` unchanged).

## 2. In-memory entities (no table)

### 2.1 LangPair plugin

Identity `(source: str, target: str)`. Aggregates per-stage hooks:

| Hook group | Hooks |
|---|---|
| source | `source_extensions()`, `tool_exclusion_globs()`, `extract_imports(path, subtree_root, package_name)`, `extract_leading_doc(path)`, `read_package_name(subtree_root)` |
| target | `target_extension()`, `target_for(source_rel)`, `workdir_name(source_rel)` |
| identity | `key() -> (source, target)` |

`dart_csharp` is the only production instance (source side factored from `tools/discover/{walker,parse,pubspec}.py`; target side = `.cs` extension + `__<basename>` working-dir convention ported from D2Net.Scaffold's `TargetTreePlanner`).

### 2.2 Language-pair registry

Process-wide map `(source,target) → LangPair`; `register()`, `get()`, `list_pairs()`; `resolve_workspace_pair(engine)` reads `workspace_settings` and returns the bound pair or raises an actionable error (unset/unknown/mismatch → FR-004/FR-005/FR-018/SC-008).

## 3. Reused — per-file conversion record (NO new shape)

The produced target path for a scaffolded source file is written into the **existing feature-015 tombstone `target_path`** (the 6th appended key; `tombstone_format_delta.md`) via `codeconv.tools.depgraph.tombstone_writer`. No `scaffold_tracker` table, no new tombstone key, no `dart_files` column. Inventory rows remain exactly `codeconv.dart_files` from discover.

## 4. Diff against prior data models

| Prior model | Change | Note |
|---|---|---|
| feature-012 `codeconv.{dart_files,dart_imports,dart_callers,dart_files_orphaned,discover_runs}` | NONE | discover behaviour byte-identical for the default Dart path (R2) |
| feature-015 `codeconv.{dart_depgraph,dart_conversions,depgraph_runs}` + 6 tombstone keys | NONE (reuse only) | scaffold writes the existing `target_path`; no schema/key change |
| D2NET `public.{setting,excluded_directories,dart_files,phase_sequence,phase_status,scaffold_tracker}` | MOVED/RENAMED into `codeconv.*`; `dart_files`+`scaffold_tracker` DROPPED | folded into `codeconv.dart_files` + tombstone `target_path` (D4/D5) |
| `public` schema | NO new objects | FR-020; D2NET `public.*` left in place but unused (not migrated, not deleted by this feature) |

## 5. New on-disk artefacts

| Path | Status | Lifetime | Reason |
|---|---|---|---|
| `<target>/**` (scaffolded tree) | NEW (user/build artefact) | persistent (build output) | the C# target skeleton produced by `scaffold` |
| `<target>.codeconv-scaffold-tmp/` | TRANSIENT | per-scaffold-run | staging dir; atomically moved into `<target>`; never committed |
| `.codeconv/tombstones/<rel>.dart.md` | MODIFIED (checked in) | persistent | gains `target_path` value (existing feature-015 key) when scaffolded |

## 6. Verification

- `\dt codeconv.*` after `0003` shows exactly the four new tables added; `\dn` unchanged; `public`/`dbos` byte-identical to pre-016 (SC schema isolation).
- `workspace_settings` has exactly one `(source_lang,target_lang)` resolving to a registered pair after `init`; every stage refuses on unset/unknown/mismatch (SC-008).
- Every scaffolded file's tombstone `target_path` equals the produced artefact path (SC-007).
- feature-012/014/015 suites green post-016 (SC-005).
